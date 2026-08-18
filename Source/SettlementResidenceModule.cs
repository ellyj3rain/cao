using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Persistent settlement residence. Population assignment strings remain
    // a derived compatibility/read surface; this typed ledger owns identity,
    // source, and transition history.
    public sealed class CASettlementResidenceAssignment : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public int sequence;
        public int pawnId = -1;
        public int populationGroupKey = -1;
        public bool active = true;
        public string entryKind;
        public string entryEvidence;
        public int enteredTick = -1;
        public string exitKind;
        public string exitEvidence;
        public int exitedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref sequence, "sequence", 0);
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref populationGroupKey,
                "populationGroupKey", -1);
            Scribe_Values.Look(ref active, "active", true);
            Scribe_Values.Look(ref entryKind, "entryKind");
            Scribe_Values.Look(ref entryEvidence, "entryEvidence");
            Scribe_Values.Look(ref enteredTick, "enteredTick", -1);
            Scribe_Values.Look(ref exitKind, "exitKind");
            Scribe_Values.Look(ref exitEvidence, "exitEvidence");
            Scribe_Values.Look(ref exitedTick, "exitedTick", -1);
        }
    }

    internal static class CASettlementResidenceState
    {
        internal static void Normalize(CARegionalSettlementRecord record)
        {
            if (record == null) return;
            if (record.residenceAssignments == null)
                record.residenceAssignments =
                    new List<CASettlementResidenceAssignment>();
            if (record.populationAssignments == null)
                record.populationAssignments = new List<string>();
            RebuildReadModel(record);
        }

        internal static CASettlementResidenceAssignment Active(
            CARegionalSettlementRecord record, int pawnId)
        {
            return record?.residenceAssignments?.LastOrDefault(item =>
                item != null && item.active && item.pawnId == pawnId);
        }

        internal static bool Assign(CARegionalSettlementRecord record,
            Pawn pawn, int populationGroupKey, string entryKind,
            string evidence)
        {
            if (record == null || pawn == null || populationGroupKey < 0
                || record.populationGroups?.Any(group => group != null
                    && group.key == populationGroupKey) != true
                || entryKind.NullOrEmpty() || evidence.NullOrEmpty())
                return false;
            Normalize(record);
            CASettlementResidenceAssignment current = Active(record,
                pawn.thingIDNumber);
            if (current != null
                && current.populationGroupKey == populationGroupKey)
                return false;
            if (current != null)
                Close(current, "reassignment",
                    "reassigned by " + entryKind + ": " + evidence);
            int sequence = record.residenceAssignments.Count == 0 ? 1
                : record.residenceAssignments.Max(item =>
                    item?.sequence ?? 0) + 1;
            record.residenceAssignments.Add(
                new CASettlementResidenceAssignment
                {
                    sequence = sequence,
                    pawnId = pawn.thingIDNumber,
                    populationGroupKey = populationGroupKey,
                    entryKind = entryKind,
                    entryEvidence = evidence,
                    enteredTick = Find.TickManager?.TicksGame ?? -1
                });
            RebuildReadModel(record);
            return true;
        }

        internal static bool Depart(CARegionalSettlementRecord record,
            int pawnId, string exitKind, string evidence)
        {
            CASettlementResidenceAssignment current = Active(record, pawnId);
            if (current == null || exitKind.NullOrEmpty()
                || evidence.NullOrEmpty()) return false;
            Close(current, exitKind, evidence);
            RebuildReadModel(record);
            return true;
        }

        private static void Close(CASettlementResidenceAssignment value,
            string kind, string evidence)
        {
            value.active = false;
            value.exitKind = kind;
            value.exitEvidence = evidence;
            value.exitedTick = Find.TickManager?.TicksGame ?? -1;
        }

        internal static void ReconcileNativeEvents(
            CARegionalSettlementRecord record, Map map)
        {
            if (record == null || map == null) return;
            Normalize(record);
            List<Pawn> spawned = map.mapPawns.AllPawnsSpawned.Where(pawn =>
                pawn != null && !pawn.Dead && pawn.RaceProps?.Humanlike == true
                && record.localRect.ExpandedBy(12).Contains(pawn.Position))
                .ToList();
            foreach (Pawn pawn in spawned)
            {
                if (Active(record, pawn.thingIDNumber) != null) continue;
                if (pawn.ageTracker == null
                    || pawn.ageTracker.AgeBiologicalYearsFloat >= 1f)
                    continue;
                CASettlementResidenceAssignment parent = pawn.relations?
                    .DirectRelations?.Where(relation => relation?.otherPawn
                        != null && relation.def?.familyByBloodRelation == true)
                    .Select(relation => Active(record,
                        relation.otherPawn.thingIDNumber))
                    .FirstOrDefault(value => value != null);
                if (parent != null)
                    Assign(record, pawn, parent.populationGroupKey, "birth",
                        "newborn has a represented family relation to resident "
                            + parent.pawnId);
            }

            foreach (CASettlementResidenceAssignment assignment in
                record.residenceAssignments.Where(item => item != null
                    && item.active).ToList())
            {
                Pawn pawn = map.mapPawns.AllPawns.FirstOrDefault(value =>
                    value?.thingIDNumber == assignment.pawnId)
                    ?? Find.WorldPawns?.AllPawnsAliveOrDead?
                        .FirstOrDefault(value => value?.thingIDNumber
                            == assignment.pawnId);
                if (pawn?.Dead == true)
                    Depart(record, assignment.pawnId, "death",
                        "native pawn death");
            }
        }

        internal static void RebuildReadModel(
            CARegionalSettlementRecord record)
        {
            if (record == null) return;
            if (record.populationAssignments == null)
                record.populationAssignments = new List<string>();
            record.populationAssignments = (record.residenceAssignments
                    ?? new List<CASettlementResidenceAssignment>())
                .Where(item => item != null && item.active)
                .OrderBy(item => item.sequence)
                .Select(item => item.populationGroupKey + ":" + item.pawnId)
                .ToList();
        }
    }

    // A native faction change is an explicit admission/departure event. It is
    // recorded only when the pawn is physically on represented settlement
    // ground; faction membership elsewhere does not imply residence here.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    internal static class CASettlementResidenceFactionChangePatch
    {
        private static void Prefix(Pawn __instance, out Faction __state)
        {
            __state = __instance?.Faction;
        }

        private static void Postfix(Pawn __instance, Faction newFaction,
            Pawn recruiter, Faction __state)
        {
            if (__instance == null || !__instance.Spawned
                || __instance.Map == null || __state == newFaction) return;
            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            if (world == null) return;
            foreach (CARegionalSettlementRecord record in
                world.ForMap(__instance.Map))
            {
                if (record?.localRect.Contains(__instance.Position) != true)
                    continue;
                CASettlementResidenceAssignment existing =
                    CASettlementResidenceState.Active(record,
                        __instance.thingIDNumber);
                if (existing != null && record.faction != newFaction)
                    CASettlementResidenceState.Depart(record,
                        __instance.thingIDNumber, "migration",
                        "native faction membership changed away from the "
                            + "settlement faction");
                if (record.faction != newFaction) continue;

                int groupKey = record.populationGroups?.FirstOrDefault(
                    group => group != null
                        && group.kind == CAPopulationGroupKind.Main)?.key ?? -1;
                CASettlementResidenceAssignment recruiterAssignment =
                    recruiter == null ? null
                    : CASettlementResidenceState.Active(record,
                        recruiter.thingIDNumber);
                if (recruiterAssignment != null)
                    groupKey = recruiterAssignment.populationGroupKey;
                CASettlementResidenceState.Assign(record, __instance,
                    groupKey, "migration",
                    recruiterAssignment == null
                        ? "native faction admission on settlement ground"
                        : "recruited by resident " + recruiter.thingIDNumber);
            }
        }
    }
}
