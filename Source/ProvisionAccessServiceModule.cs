using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Resolves who can actually draw from an arrangement. Access consumes
    // typed residence and organization membership; it never enrolls anyone.
    internal static class CAProvisionAccessService
    {
        internal static List<Pawn> EligibleResidents(
            CARegionalSettlementRecord record, Map map,
            CAProvisionArrangement arrangement,
            CAOrganization organization)
        {
            List<Pawn> residents = CAPopulationProjection.Residents(record,
                map);
            if (arrangement == null) return new List<Pawn>();
            if (arrangement.operatorKind == CAProvisionOperator.DomesticUnit)
                return CADomesticResidenceAdapter.Consumers(record, map,
                    arrangement.operatorIdentity);
            if (arrangement.operatorKind == CAProvisionOperator.Individual)
                return residents.Where(pawn => arrangement.operatorIdentity
                    == "pawn:" + pawn.thingIDNumber
                    || arrangement.operatorIdentity == "individual:"
                        + pawn.thingIDNumber).ToList();
            if (arrangement.access == CAProvisionAccess.Universal
                || arrangement.access == CAProvisionAccess.Charitable)
                return residents;
            if (organization == null) return new List<Pawn>();
            var memberIds = new HashSet<int>(organization.memberPawnIds);
            return residents.Where(pawn => memberIds.Contains(
                pawn.thingIDNumber)).ToList();
        }

        internal static bool HasAccess(CARegionalSettlementRecord record,
            Map map, CAProvisionArrangement arrangement,
            CAResolvedProvisionOperator resolved,
            CAProvisionMaterialEvidence material, out string failure)
        {
            failure = null;
            if (arrangement?.accessSource?.StartsWith("authored:",
                    System.StringComparison.Ordinal) != true
                && arrangement?.accessSource?.StartsWith("observed:",
                    System.StringComparison.Ordinal) != true)
            {
                failure = "access rule lacks authored or observed evidence";
                return false;
            }
            if (resolved?.Resolved != true)
            {
                failure = resolved?.Failure ?? "operator is unresolved";
                return false;
            }
            List<Pawn> eligible = EligibleResidents(record, map, arrangement,
                resolved.Organization);
            bool allocation = material?.Nodes?.Count > 0
                && material.Nodes.All(node => !node.allocationRule.NullOrEmpty());
            bool reachable = allocation && eligible.Any(pawn =>
                material.Nodes.Any(node => node.cell.InBounds(map)
                    && record.localRect.Contains(node.cell)
                    && pawn.CanReach(node.cell, PathEndMode.Touch,
                        Danger.Some))
                && material.Stock.All(stock => map.listerThings.AllThings
                    .Any(thing => thing != null && !thing.Destroyed
                        && thing.thingIDNumber == stock.thingId
                        && record.localRect.Contains(thing.Position)
                        && pawn.CanReach(thing, PathEndMode.Touch,
                            Danger.Some))));
            if (eligible.Count == 0 || !allocation || !reachable)
                failure = eligible.Count == 0
                    ? "no eligible residents resolve through the access rule"
                    : !allocation
                        ? "material nodes have no allocation rule"
                        : "no eligible resident can reach the exact provision nodes and stock";
            return failure == null;
        }
    }
}
