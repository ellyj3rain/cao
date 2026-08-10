using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Retained only so saves containing the former differentiated-inventory
    // projections can deserialize without losing their native stockpile zones.
    internal enum CAInventoryStorageKind : byte
    {
        PerishableFood,
        Food,
        Medicine,
        Armory,
        ProtectedMaterials,
        Materials,
        General,
        Hazard,
        Drugs
    }

    internal sealed class CAInventoryStorageProjection : IExposable
    {
        public int zoneId = -1;
        public string objectiveKey;
        public int programId;
        public CAInventoryStorageKind kind;
        public string expectedLabel;
        public StoragePriority expectedPriority = StoragePriority.Normal;
        public List<string> allowedDefNames = new List<string>();
        public List<IntVec3> cells = new List<IntVec3>();
        public int requestedCells;
        public int createdTick = -1;
        public bool provisional;
        public int placementPolicyVersion;

        public void ExposeData()
        {
            Scribe_Values.Look(ref zoneId, "zoneId", -1);
            Scribe_Values.Look(ref objectiveKey, "objectiveKey");
            Scribe_Values.Look(ref programId, "programId", 0);
            Scribe_Values.Look(ref kind, "kind", CAInventoryStorageKind.General);
            Scribe_Values.Look(ref expectedLabel, "expectedLabel");
            Scribe_Values.Look(ref expectedPriority, "expectedPriority",
                StoragePriority.Normal);
            Scribe_Collections.Look(ref allowedDefNames, "allowedDefNames",
                LookMode.Value);
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            Scribe_Values.Look(ref requestedCells, "requestedCells", 0);
            Scribe_Values.Look(ref createdTick, "createdTick", -1);
            Scribe_Values.Look(ref provisional, "provisional", false);
            Scribe_Values.Look(ref placementPolicyVersion,
                "placementPolicyVersion", 0);
            if (allowedDefNames == null) allowedDefNames = new List<string>();
            if (cells == null) cells = new List<IntVec3>();
        }
    }

    internal sealed partial class CAStorageProgramMapComponent
    {
        private List<CAInventoryStorageProjection> inventoryProjections =
            new List<CAInventoryStorageProjection>();
        private List<string> inventoryVetoedObjectiveKeys = new List<string>();
        private bool inventoryProjectionOwnershipRetired;
        private int nextInventoryPlanTick;
        private string lastInventoryOutcome =
            "native stockpiles remain player authority";

        private void ExposeInventoryData()
        {
            Scribe_Collections.Look(ref inventoryProjections,
                "CA_inventoryStorageProjections", LookMode.Deep);
            Scribe_Collections.Look(ref inventoryVetoedObjectiveKeys,
                "CA_inventoryStorageVetoes", LookMode.Value);
            Scribe_Values.Look(ref nextInventoryPlanTick,
                "CA_inventoryStorageNextPlan", 0);
            Scribe_Values.Look(ref lastInventoryOutcome,
                "CA_inventoryStorageLastOutcome",
                "native stockpiles remain player authority");
            Scribe_Values.Look(ref inventoryProjectionOwnershipRetired,
                "CA_inventoryProjectionOwnershipRetired", false);
            if (inventoryProjections == null)
                inventoryProjections = new List<CAInventoryStorageProjection>();
            if (inventoryVetoedObjectiveKeys == null)
                inventoryVetoedObjectiveKeys = new List<string>();
        }

        // The former durable-inventory module manufactured category-named native
        // zones. Native stockpile filters and priorities already own that schema.
        // Retirement removes operative CA ownership while preserving historical
        // zone-origin provenance for audit. It never attributes later item
        // movement. Every extant zone, cell, filter, priority, label, and held
        // item remains untouched.
        private string RetireLegacyInventoryProjectionOwnership()
        {
            int records = inventoryProjections.Count;
            var retainedZoneIds = new HashSet<int>();
            int unresolvedRecords = 0;
            for (int i = 0; i < inventoryProjections.Count; i++)
            {
                CAInventoryStorageProjection projection = inventoryProjections[i];
                if (projection != null && FindZone(projection.zoneId) != null)
                    retainedZoneIds.Add(projection.zoneId);
                else unresolvedRecords++;
            }
            inventoryVetoedObjectiveKeys.Clear();
            inventoryProjectionOwnershipRetired = true;
            if (records > 0)
                lastInventoryOutcome = "retired operative ownership for "
                    + records + " legacy differentiated-inventory record(s); "
                    + "preserved those records as read-only zone-origin "
                    + "evidence, never item-movement authorship; retained "
                    + retainedZoneIds.Count
                    + " distinct native stockpile zone(s) unchanged without "
                    + "calling their original placement player-authored"
                    + (unresolvedRecords > 0
                        ? "; " + unresolvedRecords
                            + " legacy record(s) no longer resolved to a zone"
                        : "");
            else if (lastInventoryOutcome.NullOrEmpty()
                || !lastInventoryOutcome.StartsWith("retired ")
                    && !lastInventoryOutcome.StartsWith("native stockpile"))
                lastInventoryOutcome = "native stockpile filters, priorities, "
                    + "footprints, and mixed contents remain player authority; "
                    + "no differentiated storage zone was created";
            return lastInventoryOutcome;
        }

        internal IReadOnlyList<CAInventoryStorageProjection>
            HistoricalInventoryProvenanceForObservation =>
                inventoryProjections;

        internal bool InventoryProjectionOwnershipRetiredForObservation =>
            inventoryProjectionOwnershipRetired;

        internal string DebugEnsureInventoryStorage()
        {
            return "[CA] agent receipt: inventory-storage-retired\n"
                + lastInventoryOutcome + "\n" + InventoryCensus();
        }

        internal string DebugFocusInventoryStorage()
        {
            return "[CA] agent receipt: inventory-storage-focus-retired\n"
                + "refused to select or focus a CA-owned durable storage zone; "
                + "native stockpiles remain player-owned\n" + InventoryCensus();
        }

        internal string DebugRunInventoryStorage()
        {
            return DebugEnsureInventoryStorage();
        }

        internal string DebugRunArmoryStorage()
        {
            return DebugEnsureInventoryStorage();
        }

        private string InventoryCensus()
        {
            List<Zone_Stockpile> zones = map.zoneManager.AllZones
                .OfType<Zone_Stockpile>().OrderBy(zone => zone.ID).ToList();
            var entries = new List<string>();
            for (int i = 0; i < zones.Count; i++)
            {
                Zone_Stockpile zone = zones[i];
                List<Thing> held = zone.slotGroup?.HeldThings.ToList()
                    ?? new List<Thing>();
                int units = held.Sum(thing => thing.stackCount);
                List<IGrouping<string, Thing>> definitionGroups = held
                    .GroupBy(thing => thing.def.defName)
                    .OrderByDescending(group => group.Sum(thing =>
                        thing.stackCount)).ThenBy(group => group.Key).ToList();
                string contents = definitionGroups.Count == 0 ? "none"
                    : string.Join(", ", definitionGroups.Take(12).Select(group =>
                        group.Key + " " + group.Sum(thing => thing.stackCount))
                        .ToArray()) + (definitionGroups.Count > 12
                            ? ", +" + (definitionGroups.Count - 12) + " more"
                            : "");
                entries.Add("zone " + zone.ID + " '" + zone.label + "' priority "
                    + zone.settings.Priority.Label() + ", cells " + zone.cells.Count
                    + ", allowed loaded definitions "
                    + zone.settings.filter.AllowedDefCount + ", held "
                    + held.Count + " stack(s)/" + units + " unit(s) across "
                    + definitionGroups.Count + " definition(s) [" + contents
                    + "]");
            }
            return "native stockpiles " + zones.Count + " ["
                + (entries.Count == 0 ? "none"
                    : string.Join("; ", entries.ToArray()))
                + "]; CA durable inventory projections disabled; historical "
                + "legacy provenance records " + inventoryProjections.Count
                + "; operative ownership retired "
                + inventoryProjectionOwnershipRetired + "; inventory vetoes "
                + inventoryVetoedObjectiveKeys.Count + "; last "
                + lastInventoryOutcome;
        }
    }
}
