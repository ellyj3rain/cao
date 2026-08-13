using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    // Adapts native pawns and the settlement population ledger to domestic
    // formation. Presence on a map is not residency.
    internal static class CADomesticResidenceAdapter
    {
        internal static List<Pawn> AssignedResidents(
            CARegionalSettlementRecord record, Map map)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.DomesticUnitLookup))
            {
                if (record == null || map == null) return new List<Pawn>();
                List<Pawn> residents = CAPopulationProjection.Residents(
                    record, map);
                CAModuleProfiler.Observe(
                    CAModuleProfileKey.DomesticUnitLookup,
                    objectsExamined: residents.Count,
                    candidatesAccepted: residents.Count);
                return residents;
            }
        }

        internal static List<Pawn> Consumers(
            CARegionalSettlementRecord record, Map map,
            string operatorIdentity)
        {
            if (record == null || map == null
                || operatorIdentity.NullOrEmpty()) return new List<Pawn>();
            CADomesticUnit unit = record.domesticUnits?.FirstOrDefault(item =>
                item != null && item.active
                && item.unitIdentity == operatorIdentity);
            if (unit == null) return new List<Pawn>();
            var ids = new HashSet<int>(unit.memberships
                .Where(item => item != null && item.active)
                .Select(item => item.pawnId));
            return AssignedResidents(record, map).Where(pawn =>
                ids.Contains(pawn.thingIDNumber)).ToList();
        }
    }
}
