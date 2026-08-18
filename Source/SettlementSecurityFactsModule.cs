using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    // Live read model over exact Defense-program assets and assigned guards.
    // It never treats a saved ID or a generic faction pawn as current security.
    internal static class CASettlementSecurityFacts
    {
        internal static void Read(string settlementKey, out int defenses,
            out int assignedGuards)
        {
            defenses = 0;
            assignedGuards = 0;
            CARegionalSettlementRecord record =
                CARegionalWorldComponent.Current?.Records.FirstOrDefault(
                    candidate => candidate != null
                        && candidate.regionalId + "#" + candidate.slot
                            == settlementKey);
            Map map = record == null ? null : Find.Maps.FirstOrDefault(item =>
                item != null && item.uniqueID == record.lastMapId);
            if (record == null || map == null) return;
            List<CASettlementProgramEntry> entries = record.settlementProgram
                ?.Entries(CASettlementProgramRegistry.Defense).ToList()
                ?? new List<CASettlementProgramEntry>();
            defenses = Math.Min(5, entries.SelectMany(entry =>
                    CASettlementProgramAssets.ReceiptsFor(record, entry))
                .Count(receipt => CASettlementProgramAssets.LiveThing(
                    record, map, receipt) != null));
            assignedGuards = Math.Min(5,
                CASettlementSecurityAssignments.LiveGuardIds(record, map)
                    .Count);
        }
    }
}
