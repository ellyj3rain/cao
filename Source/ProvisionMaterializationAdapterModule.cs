using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal sealed class CAProvisionMaterialEvidence
    {
        internal List<CAFacilityHolding> Nodes =
            new List<CAFacilityHolding>();
        internal List<CAStartingStockRecord> Stock =
            new List<CAStartingStockRecord>();
        internal bool Complete;
        internal string Failure;
    }

    internal sealed class CAProvisionMaterializationPlan
    {
        internal CAProvisionArrangement Arrangement;
        internal CAResolvedProvisionOperator Operator;
        internal ThingDef AuthoredStockDef;
        internal Thing ObservedStock;
        internal int StockCount;
        internal int Nodes;
    }

    // Provision materialization owns the exact boundary between an arrangement
    // and its native material facts. Preflight resolves the operator, funding,
    // eligible residents, material provenance, and exact stock before spawning
    // anything. Observe later validates the typed receipts; it never infers an
    // operator from proximity or redirects a missing identity.
    internal static class CAProvisionMaterializationAdapter
    {
        internal static bool TryPlan(CARegionalSettlementRecord record,
            Map map, CAProvisionArrangement arrangement,
            ISet<int> reservedObservedStockIds,
            out CAProvisionMaterializationPlan plan, out string failure)
        {
            plan = null;
            failure = null;
            if (record == null || map == null || arrangement == null)
            {
                failure = "provision preflight lacks settlement, map, or arrangement";
                return false;
            }
            CAResolvedProvisionOperator resolved =
                CAProvisionOperatorResolver.Resolve(record, map, arrangement);
            if (!resolved.Resolved)
            {
                failure = resolved.Failure;
                return false;
            }
            if (!CAProvisionFundingResolver.IsFunded(record, arrangement,
                    resolved, out failure)) return false;
            if (resolved.Consumers.Count == 0)
            {
                failure = "the provision operator has no eligible residents";
                return false;
            }
            if (!DirectSource(arrangement.materialSource))
            {
                failure = "material nodes lack authored or observed provenance";
                return false;
            }
            if (!DirectSource(arrangement.accessSource)
                || !DirectSource(arrangement.laborSource)
                || !DirectSource(arrangement.knowledgeSource))
            {
                failure = "access, labor, and knowledge require authored or observed evidence";
                return false;
            }

            var result = new CAProvisionMaterializationPlan
            {
                Arrangement = arrangement,
                Operator = resolved,
                Nodes = Math.Max(1, arrangement.nodes)
            };
            if (TryAuthoredStock(arrangement.stockSource,
                    out ThingDef stockDef, out int count))
            {
                result.AuthoredStockDef = stockDef;
                result.StockCount = count;
            }
            else if (TryObservedStock(record, map, resolved,
                    arrangement.stockSource, reservedObservedStockIds,
                    out Thing stock))
            {
                if (result.Nodes != 1)
                {
                    failure = "one observed stock identity cannot supply several material nodes";
                    return false;
                }
                result.ObservedStock = stock;
                result.StockCount = stock.stackCount;
            }
            else
            {
                failure = "stock source must be authored:<ThingDef>:<count> or observed:thing:<id>";
                return false;
            }
            plan = result;
            return true;
        }

        internal static CAProvisionMaterialEvidence Observe(
            CARegionalSettlementRecord record, Map map,
            CAProvisionArrangement arrangement)
        {
            var result = new CAProvisionMaterialEvidence();
            if (record == null || map == null || arrangement == null
                || arrangement.operatorIdentity.NullOrEmpty())
            {
                result.Failure = "material observation lacks exact identity";
                return result;
            }
            string identity = arrangement.operatorIdentity;
            string programKey = CASettlementProgramRegistry.ProgramKeyFor(
                arrangement.operatorKind);
            CASettlementProgramEntry entry = record.settlementProgram
                ?.Entry(programKey, identity);
            if (entry == null)
            {
                result.Failure = "no exact provision program exists for the arrangement kind and operator";
                return result;
            }
            List<CASettlementProgramAssetReceipt> receipts =
                CASettlementProgramAssets.ReceiptsFor(record, entry)
                    .Where(item => item.provisionArrangementKey
                        == arrangement.key).ToList();
            result.Nodes = (CAOrganizationRelationsWorldComponent.Current
                    ?.Holdings ?? new List<CAFacilityHolding>())
                .Where(item => item != null && item.mapId == map.uniqueID
                    && item.provisionArrangementKey == arrangement.key
                    && item.programKey == programKey
                    && item.programSignature == entry.signature
                    && (item.operatorIdentity == identity
                        || item.operatorOrgKey == identity
                        || item.ownerOrgKey == identity))
                .ToList();
            result.Stock = (record.startingStock
                    ?? new List<CAStartingStockRecord>())
                .Where(item => item != null && item.thingId >= 0
                    && item.providerIdentity == identity
                    && item.provisionArrangementKey == arrangement.key)
                .Where(item => map.listerThings.AllThings.Any(thing =>
                    thing != null && !thing.Destroyed
                    && thing.Spawned && record.localRect.Contains(
                        thing.Position)
                    && thing.thingIDNumber == item.thingId
                    && thing.def?.defName == item.thingDefName
                    && thing.stackCount >= item.count))
                .ToList();
            int nodes = Math.Max(1, arrangement.nodes);
            int rolesPerNode = Math.Max(1,
                entry.selectedCandidates?.Count ?? 0);
            int expectedAssets = nodes * rolesPerNode;
            bool uniqueStock = (record.startingStock
                    ?? new List<CAStartingStockRecord>())
                .Where(item => item != null && item.thingId >= 0)
                .GroupBy(item => item.thingId)
                .All(group => group.Count() == 1);
            result.Complete = receipts.Count == expectedAssets
                && receipts.All(receipt =>
                    CASettlementProgramAssets.LiveThing(record, map,
                        receipt) != null)
                && result.Nodes.Count == expectedAssets
                && result.Nodes.All(holding => receipts.Any(receipt =>
                    receipt.assetRole == holding.assetRole
                    && receipt.thingId == map.listerThings.AllThings
                        .FirstOrDefault(thing => thing != null
                            && thing.thingIDNumber == holding.thingId)
                        ?.ThingID))
                && result.Nodes.Select(item =>
                    item.provisionNodeIndex).Distinct().Count() == nodes
                && result.Stock.Select(item => item.provisionNodeIndex)
                    .Distinct().Count() == nodes
                && result.Stock.Count == nodes
                && uniqueStock
                && result.Nodes.All(item =>
                    !item.allocationRule.NullOrEmpty());
            if (!result.Complete)
                result.Failure = "provider-bound material nodes, stock, or allocation are incomplete";
            return result;
        }

        internal static string AllocationRule(CAProvisionAccess access)
        {
            return access == CAProvisionAccess.Universal
                ? CAAllocationRules.OpenAccess
                : access == CAProvisionAccess.Charitable
                    ? CAAllocationRules.Assignment
                    : CAAllocationRules.Entitlement;
        }

        internal static string CapitalSource(CAProvisionFunding funding)
        {
            return funding == CAProvisionFunding.Taxation
                ? CACapitalSources.Treasury
                : funding == CAProvisionFunding.SharedWork
                    ? CACapitalSources.SharedWork
                    : CACapitalSources.Private;
        }

        private static bool DirectSource(string source)
        {
            return source?.StartsWith("authored:",
                       StringComparison.Ordinal) == true
                || source?.StartsWith("observed:",
                       StringComparison.Ordinal) == true;
        }

        private static bool TryAuthoredStock(string source,
            out ThingDef def, out int count)
        {
            def = null;
            count = 0;
            string[] parts = source?.Split(new[] { ':' },
                StringSplitOptions.None);
            if (parts == null || parts.Length != 3
                || parts[0] != "authored"
                || !int.TryParse(parts[2], out count) || count <= 0)
                return false;
            def = DefDatabase<ThingDef>.GetNamedSilentFail(parts[1]);
            return def != null && def.category == ThingCategory.Item
                && count <= def.stackLimit;
        }

        private static bool TryObservedStock(
            CARegionalSettlementRecord record, Map map,
            CAResolvedProvisionOperator resolved, string source,
            ISet<int> reservedObservedStockIds, out Thing stock)
        {
            stock = null;
            string[] parts = source?.Split(new[] { ':' },
                StringSplitOptions.None);
            if (parts == null || parts.Length != 3
                || parts[0] != "observed" || parts[1] != "thing"
                || !int.TryParse(parts[2], out int thingId)) return false;
            if (reservedObservedStockIds?.Contains(thingId) == true
                || (record.startingStock
                    ?? new List<CAStartingStockRecord>())
                    .Any(item => item != null && item.thingId == thingId))
                return false;
            stock = map.listerThings.AllThings.FirstOrDefault(thing =>
                thing != null && !thing.Destroyed && thing.Spawned
                && thing.thingIDNumber == thingId
                && thing.def?.category == ThingCategory.Item
                && thing.stackCount > 0
                && record.localRect.Contains(thing.Position)
                && (resolved?.Consumers ?? new List<Pawn>()).Any(pawn =>
                    pawn != null && pawn.Spawned && pawn.Map == map
                    && pawn.CanReach(thing, PathEndMode.Touch,
                        Danger.Some)));
            return stock != null;
        }
    }
}
