using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    // Persistent identity of one exact material node belonging to one exact
    // saved program contract. This receipt remains authoritative through
    // destruction and rebuilding; Thing presence is only its live state.
    public sealed class CASettlementProgramAssetReceipt : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string programKey;
        public string operatorIdentity;
        public string programSignature;
        public string assetRole;
        public string assetKind = "thing";
        public string thingId;
        public string defName;
        public string stuffDefName;
        public int mapId = -1;
        public IntVec3 cell = IntVec3.Invalid;
        public int provisionArrangementKey;
        public int provisionNodeIndex = -1;
        public string source;

        public string Key => (programSignature ?? "") + "|"
            + (assetRole ?? "");

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref programSignature, "programSignature");
            Scribe_Values.Look(ref assetRole, "assetRole");
            Scribe_Values.Look(ref assetKind, "assetKind", "thing");
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref stuffDefName, "stuffDefName");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref provisionArrangementKey,
                "provisionArrangementKey", 0);
            Scribe_Values.Look(ref provisionNodeIndex,
                "provisionNodeIndex", -1);
            Scribe_Values.Look(ref source, "source");
        }
    }

    internal static class CASettlementProgramAssets
    {
        internal static CASettlementProgramAssetReceipt RecordThing(
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramEntry entry, string role, Thing thing,
            int arrangementKey = 0, int nodeIndex = -1)
        {
            if (record == null || map == null || entry == null
                || role.NullOrEmpty() || thing == null)
                throw new ArgumentException(
                    "A program asset requires an exact settlement, program, role, map, and thing.");
            Ensure(record);
            record.programAssets.RemoveAll(item => item != null
                && item.programSignature == entry.signature
                && item.assetRole == role);
            var receipt = new CASettlementProgramAssetReceipt
            {
                programKey = entry.programKey,
                operatorIdentity = entry.operatorIdentity,
                programSignature = entry.signature,
                assetRole = role,
                assetKind = "thing",
                thingId = thing.ThingID,
                defName = thing.def?.defName,
                stuffDefName = thing.Stuff?.defName,
                mapId = map.uniqueID,
                cell = thing.Position,
                provisionArrangementKey = arrangementKey,
                provisionNodeIndex = nodeIndex,
                source = "materialized:program:" + entry.signature
            };
            record.programAssets.Add(receipt);
            SyncEntryView(record, entry);
            return receipt;
        }

        internal static CASettlementProgramAssetReceipt RecordZone(
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramEntry entry, string role, Zone zone,
            IntVec3 anchor)
        {
            if (record == null || map == null || entry == null
                || zone == null || role.NullOrEmpty())
                throw new ArgumentException(
                    "A program zone requires an exact settlement, program, role, map, and zone.");
            Ensure(record);
            string id = "zone:" + zone.ID;
            record.programAssets.RemoveAll(item => item != null
                && item.programSignature == entry.signature
                && item.assetRole == role);
            var receipt = new CASettlementProgramAssetReceipt
            {
                programKey = entry.programKey,
                operatorIdentity = entry.operatorIdentity,
                programSignature = entry.signature,
                assetRole = role,
                assetKind = "zone",
                thingId = id,
                defName = zone.GetType().FullName,
                mapId = map.uniqueID,
                cell = anchor,
                source = "materialized:program:" + entry.signature
            };
            record.programAssets.Add(receipt);
            SyncEntryView(record, entry);
            return receipt;
        }

        internal static IEnumerable<CASettlementProgramAssetReceipt>
            ReceiptsFor(CARegionalSettlementRecord record,
                CASettlementProgramEntry entry)
        {
            return (record?.programAssets
                    ?? new List<CASettlementProgramAssetReceipt>())
                .Where(item => item != null && entry != null
                    && item.programKey == entry.programKey
                    && item.operatorIdentity == entry.operatorIdentity
                    && item.programSignature == entry.signature);
        }

        internal static CASettlementProgramAssetReceipt FindByThing(
            CARegionalSettlementRecord record, string thingId)
        {
            return (record?.programAssets
                    ?? new List<CASettlementProgramAssetReceipt>())
                .FirstOrDefault(item => item != null
                    && item.assetKind == "thing"
                    && item.thingId == thingId);
        }

        internal static IReadOnlyList<string> AssetIds(
            CARegionalSettlementRecord record,
            CASettlementProgramEntry entry)
        {
            return ReceiptsFor(record, entry)
                .Select(item => item.thingId)
                .Where(value => !value.NullOrEmpty())
                .Distinct(StringComparer.Ordinal).ToList();
        }

        internal static void SyncDerivedViews(
            CARegionalSettlementRecord record)
        {
            if (record?.settlementProgram?.entries == null) return;
            foreach (CASettlementProgramEntry entry in
                record.settlementProgram.entries.Where(item => item != null))
                SyncEntryView(record, entry);
        }

        internal static void RollbackToCount(
            CARegionalSettlementRecord record, int receiptCount)
        {
            Ensure(record);
            int keep = Math.Max(0, Math.Min(receiptCount,
                record.programAssets.Count));
            if (record.programAssets.Count > keep)
                record.programAssets.RemoveRange(keep,
                    record.programAssets.Count - keep);
            SyncDerivedViews(record);
        }

        internal static Thing LiveThing(
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramAssetReceipt receipt)
        {
            if (record == null || map == null || receipt == null
                || receipt.assetKind != "thing"
                || receipt.mapId != map.uniqueID
                || !record.localRect.Contains(receipt.cell)) return null;
            return map.listerThings.AllThings.FirstOrDefault(thing =>
                thing != null && !thing.Destroyed && thing.Spawned
                && thing.ThingID == receipt.thingId
                && thing.def?.defName == receipt.defName
                && (receipt.stuffDefName.NullOrEmpty()
                    || thing.Stuff?.defName == receipt.stuffDefName)
                && thing.Position == receipt.cell
                && record.localRect.Contains(thing.Position));
        }

        internal static Zone LiveZone(CARegionalSettlementRecord record,
            Map map, CASettlementProgramAssetReceipt receipt)
        {
            if (record == null || map == null || receipt == null
                || receipt.assetKind != "zone"
                || receipt.mapId != map.uniqueID
                || receipt.thingId?.StartsWith("zone:",
                    StringComparison.Ordinal) != true
                || !int.TryParse(receipt.thingId.Substring(5),
                    out int zoneId)) return null;
            Zone zone = map.zoneManager.AllZones.FirstOrDefault(item =>
                item != null && item.ID == zoneId
                && item.GetType().FullName == receipt.defName);
            return zone != null && zone.Cells.Any(record.localRect.Contains)
                ? zone : null;
        }

        internal static bool Complete(CARegionalSettlementRecord record,
            Map map, CASettlementProgramEntry entry)
        {
            List<CASettlementProgramAssetReceipt> receipts = ReceiptsFor(
                record, entry).ToList();
            if (receipts.Count == 0) return !entry.requiresMaterial;
            return receipts.Select(item => item.assetRole).Distinct().Count()
                    == receipts.Count
                && receipts.All(item => item.assetKind == "zone"
                    ? LiveZone(record, map, item) != null
                    : LiveThing(record, map, item) != null);
        }

        internal static void Rebind(CARegionalSettlementRecord record,
            Map map, CASettlementProgramEntry entry,
            CASettlementProgramAssetReceipt receipt, Thing rebuilt)
        {
            if (record == null || map == null || entry == null
                || receipt == null || rebuilt == null)
                throw new ArgumentException(
                    "Rebuilding requires the exact saved program asset receipt.");
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger != null && !ledger.TryRebindProgramAsset(
                    entry.signature, receipt.assetRole, rebuilt,
                    out string holdingFailure))
                throw new InvalidOperationException(holdingFailure);
            receipt.thingId = rebuilt.ThingID;
            receipt.defName = rebuilt.def?.defName;
            receipt.stuffDefName = rebuilt.Stuff?.defName;
            receipt.mapId = map.uniqueID;
            receipt.cell = rebuilt.Position;
            SyncEntryView(record, entry);
        }

        private static void Ensure(CARegionalSettlementRecord record)
        {
            if (record.programAssets == null)
                record.programAssets =
                    new List<CASettlementProgramAssetReceipt>();
        }

        private static void SyncEntryView(CARegionalSettlementRecord record,
            CASettlementProgramEntry entry)
        {
            if (entry == null) return;
            entry.placedThingIds = AssetIds(record, entry).ToList();
        }
    }
}
