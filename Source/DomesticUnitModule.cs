using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // A pre-materialization population has a real need for domestic
    // provision, but it does not yet have pawn identities from which a
    // domestic unit can be established. The demand remains unresolved until
    // the native population exists.
    public sealed class CADomesticProvisionDemand : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string demandKey;
        public int populationGroupKey = -1;
        public int representedResidents;
        public string needSource;
        public string state = "unresolved";
        public string resolutionSource;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref demandKey, "demandKey");
            Scribe_Values.Look(ref populationGroupKey,
                "populationGroupKey", -1);
            Scribe_Values.Look(ref representedResidents,
                "representedResidents", 0);
            Scribe_Values.Look(ref needSource, "needSource");
            Scribe_Values.Look(ref state, "state", "unresolved");
            Scribe_Values.Look(ref resolutionSource, "resolutionSource");
        }

        internal CADomesticProvisionDemand Copy()
        {
            return new CADomesticProvisionDemand
            {
                schemaVersion = schemaVersion,
                demandKey = demandKey,
                populationGroupKey = populationGroupKey,
                representedResidents = representedResidents,
                needSource = needSource,
                state = state,
                resolutionSource = resolutionSource
            };
        }
    }

    public enum CADomesticUnitKind : byte
    {
        Individual,
        Family,
        SharedBed
    }

    public sealed class CADomesticMembership : IExposable
    {
        public int pawnId = -1;
        public string sourceKind;
        public string sourceIdentity;
        public bool active = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref sourceKind, "sourceKind");
            Scribe_Values.Look(ref sourceIdentity, "sourceIdentity");
            Scribe_Values.Look(ref active, "active", true);
        }
    }

    public sealed class CADomesticMembershipTransition : IExposable
    {
        public int sequence;
        public string unitIdentity;
        public int pawnId = -1;
        public string transition;
        public string evidence;
        // A tick is chronology only. It never participates in unit identity.
        public int recordedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sequence, "sequence", 0);
            Scribe_Values.Look(ref unitIdentity, "unitIdentity");
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref transition, "transition");
            Scribe_Values.Look(ref evidence, "evidence");
            Scribe_Values.Look(ref recordedTick, "recordedTick", -1);
        }
    }

    // Persistent factual unit. Membership comes only from a represented pawn
    // relation or shared owned bed. A resident without either remains an
    // individual; no ordering, group size, hash, or UI read can make a unit.
    public sealed class CADomesticUnit : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string unitIdentity;
        public string settlementIdentity;
        public int formationSequence;
        public int continuityPawnId = -1;
        public CADomesticUnitKind kind;
        public bool active = true;
        public string status = "active";
        public string formationSource;
        public int formedTick = -1;
        public int dissolvedTick = -1;
        public string residentialScope;
        public List<string> sharedProvisionNodeKeys = new List<string>();
        public List<CADomesticMembership> memberships =
            new List<CADomesticMembership>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref unitIdentity, "unitIdentity");
            Scribe_Values.Look(ref settlementIdentity,
                "settlementIdentity");
            Scribe_Values.Look(ref formationSequence,
                "formationSequence", 0);
            Scribe_Values.Look(ref continuityPawnId,
                "continuityPawnId", -1);
            Scribe_Values.Look(ref kind, "kind",
                CADomesticUnitKind.Individual);
            Scribe_Values.Look(ref active, "active", true);
            Scribe_Values.Look(ref status, "status", "active");
            Scribe_Values.Look(ref formationSource, "formationSource");
            Scribe_Values.Look(ref formedTick, "formedTick", -1);
            Scribe_Values.Look(ref dissolvedTick, "dissolvedTick", -1);
            Scribe_Values.Look(ref residentialScope,
                "residentialScope");
            Scribe_Collections.Look(ref sharedProvisionNodeKeys,
                "sharedProvisionNodeKeys", LookMode.Value);
            Scribe_Collections.Look(ref memberships, "memberships",
                LookMode.Deep);
        }

        internal bool Contains(int pawnId)
        {
            return memberships.Any(item => item != null && item.active
                && item.pawnId == pawnId);
        }
    }

    internal static class CADomesticUnitFormation
    {
        internal static string SettlementIdentity(
            CARegionalSettlementRecord record)
        {
            return record == null ? null : record.regionalId + "#"
                + record.slot;
        }

        // Called from the simulation materialization/reconciliation path only.
        // UI code has no route to this mutator.
        internal static void Reconcile(CARegionalSettlementRecord record,
            Map map)
        {
            if (record == null || map == null) return;
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.DomesticUnitTransition))
            {
            CADomesticUnitState.Normalize(record);
            int transitionsBefore = record.domesticMembershipTransitions.Count;

            // Physical proximity and faction identity do not establish
            // residence. Only pawns already assigned to this settlement's
            // saved population are eligible for domestic reconciliation.
            List<Pawn> residents = CADomesticResidenceAdapter
                .AssignedResidents(record, map);
            Dictionary<int, string> formerUnitByPawn = record.domesticUnits
                .Where(unit => unit != null && unit.active)
                .SelectMany(unit => unit.memberships.Where(member =>
                    member != null && member.active).Select(member =>
                        new { member.pawnId, unit.unitIdentity }))
                .GroupBy(item => item.pawnId)
                .ToDictionary(group => group.Key,
                    group => group.First().unitIdentity);
            var residentById = residents.ToDictionary(
                pawn => pawn.thingIDNumber);
            List<List<Pawn>> components = ConnectedComponents(residents,
                residentById);
            var claimedUnits = new HashSet<string>(StringComparer.Ordinal);
            var desiredMemberIds = new HashSet<int>();
            components = components.OrderByDescending(component =>
                    record.domesticUnits.Any(unit => unit != null
                        && unit.active && component.Any(pawn =>
                            pawn.thingIDNumber == unit.continuityPawnId)))
                .ThenBy(component => component.Min(pawn =>
                    pawn.thingIDNumber)).ToList();
            foreach (List<Pawn> component in components)
            {
                foreach (Pawn pawn in component)
                    desiredMemberIds.Add(pawn.thingIDNumber);
                List<CADomesticUnit> overlaps = record.domesticUnits
                    .Where(unit => unit != null && unit.active
                        && !claimedUnits.Contains(unit.unitIdentity)
                        && component.Any(pawn => unit.Contains(
                            pawn.thingIDNumber)))
                    .OrderByDescending(unit => component.Any(pawn =>
                        pawn.thingIDNumber == unit.continuityPawnId))
                    .ThenBy(unit => unit.formationSequence)
                    .ThenBy(unit => unit.unitIdentity,
                        StringComparer.Ordinal).ToList();
                CADomesticUnit target = overlaps.FirstOrDefault();
                if (target == null)
                {
                    int sequence = Math.Max(1,
                        record.nextDomesticUnitSequence);
                    record.nextDomesticUnitSequence = sequence + 1;
                    int continuityPawn = component.Min(pawn =>
                        pawn.thingIDNumber);
                    string predecessor = component.Select(pawn =>
                            formerUnitByPawn.TryGetValue(pawn.thingIDNumber,
                                out string oldUnit) ? oldUnit : null)
                        .FirstOrDefault(value => !value.NullOrEmpty());
                    target = new CADomesticUnit
                    {
                        settlementIdentity = SettlementIdentity(record),
                        formationSequence = sequence,
                        continuityPawnId = continuityPawn,
                        unitIdentity = SettlementIdentity(record)
                            + ":domestic-unit:" + sequence,
                        formationSource = !predecessor.NullOrEmpty()
                            ? "split from " + predecessor
                            : component.Count == 1
                            ? "individual resident"
                            : ComponentEvidence(component),
                        kind = UnitKind(component),
                        formedTick = Find.TickManager?.TicksGame ?? -1,
                        residentialScope = ResidentialScope(component)
                    };
                    record.domesticUnits.Add(target);
                }
                claimedUnits.Add(target.unitIdentity);
                foreach (CADomesticUnit merged in overlaps.Skip(1))
                {
                    merged.active = false;
                    merged.status = "merged into " + target.unitIdentity;
                    merged.dissolvedTick = Find.TickManager?.TicksGame ?? -1;
                    foreach (CADomesticMembership member in
                        merged.memberships.Where(item => item != null
                            && item.active))
                    {
                        member.active = false;
                        RecordTransition(record, target.unitIdentity,
                            member.pawnId, "merged",
                            "represented domestic relations joined "
                                + merged.unitIdentity + " to "
                                + target.unitIdentity);
                    }
                }

                var componentIds = new HashSet<int>(component.Select(
                    pawn => pawn.thingIDNumber));
                foreach (CADomesticMembership old in target.memberships
                    .Where(item => item != null && item.active
                        && !componentIds.Contains(item.pawnId)).ToList())
                {
                    CASettlementResidenceAssignment residence =
                        CASettlementResidenceState.Active(record,
                            old.pawnId);
                    // A caravan or other temporary absence does not dissolve
                    // residence or domestic identity. Only a typed residence
                    // exit or a represented split closes membership.
                    if (residence != null
                        && !residentById.ContainsKey(old.pawnId))
                        continue;
                    old.active = false;
                    string transition = residentById.ContainsKey(old.pawnId)
                        ? "split" : ResidenceExit(record, map, old.pawnId);
                    RecordTransition(record, target.unitIdentity,
                        old.pawnId, transition,
                        "the represented relation or shared-bed evidence "
                            + "no longer binds this resident");
                }
                foreach (Pawn pawn in component)
                {
                    if (target.Contains(pawn.thingIDNumber)) continue;
                    MembershipEvidence(component, pawn, out string kind,
                        out string identity);
                    target.memberships.Add(new CADomesticMembership
                    {
                        pawnId = pawn.thingIDNumber,
                        sourceKind = kind,
                        sourceIdentity = identity,
                        active = true
                    });
                    RecordTransition(record, target.unitIdentity,
                        pawn.thingIDNumber, "joined", kind + ": " + identity);
                }
                target.kind = UnitKind(component);
                target.active = true;
                target.status = "active";
                target.dissolvedTick = -1;
                target.residentialScope = ResidentialScope(component);
            }

            foreach (CADomesticUnit unit in record.domesticUnits.Where(unit =>
                unit != null && unit.active
                && !claimedUnits.Contains(unit.unitIdentity)).ToList())
            {
                bool retainedAway = unit.memberships.Any(member =>
                    member != null && member.active
                    && CASettlementResidenceState.Active(record,
                        member.pawnId) != null);
                if (retainedAway)
                {
                    unit.status = "members away";
                    continue;
                }
                unit.active = false;
                unit.status = "dissolved";
                unit.dissolvedTick = Find.TickManager?.TicksGame ?? -1;
                RecordTransition(record, unit.unitIdentity, -1,
                    "dissolved", "no represented resident membership remains");
                foreach (CADomesticMembership member in unit.memberships
                    .Where(item => item != null && item.active))
                {
                    member.active = false;
                    string transition = desiredMemberIds.Contains(member.pawnId)
                        ? "split" : ResidenceExit(record, map,
                            member.pawnId);
                    RecordTransition(record, unit.unitIdentity,
                        member.pawnId, transition,
                        desiredMemberIds.Contains(member.pawnId)
                            ? "membership moved by represented domestic evidence"
                            : "resident is no longer present");
                }
            }
            foreach (IGrouping<string, KeyValuePair<int, string>> former in
                formerUnitByPawn.GroupBy(pair => pair.Value))
            {
                string[] successors = former.Select(pair => record.domesticUnits
                        .FirstOrDefault(unit => unit != null && unit.active
                            && unit.Contains(pair.Key))?.unitIdentity)
                    .Where(value => !value.NullOrEmpty()).Distinct().ToArray();
                if (successors.Length > 1)
                    RecordTransition(record, former.Key, -1, "split",
                        "successor units: " + string.Join(",", successors));
            }
            CAModuleProfiler.Observe(
                CAModuleProfileKey.DomesticUnitTransition,
                objectsExamined: residents.Count,
                candidatesAccepted: Math.Max(0,
                    record.domesticMembershipTransitions.Count
                        - transitionsBefore));
            }
        }

        private static List<List<Pawn>> ConnectedComponents(
            List<Pawn> residents, Dictionary<int, Pawn> residentById)
        {
            var remaining = new HashSet<int>(residentById.Keys);
            var result = new List<List<Pawn>>();
            while (remaining.Count > 0)
            {
                int start = remaining.Min();
                var queue = new Queue<int>();
                var component = new List<Pawn>();
                queue.Enqueue(start);
                remaining.Remove(start);
                while (queue.Count > 0)
                {
                    Pawn pawn = residentById[queue.Dequeue()];
                    component.Add(pawn);
                    foreach (Pawn other in residents)
                    {
                        if (!remaining.Contains(other.thingIDNumber)
                            || !RepresentedDomesticLink(pawn, other))
                            continue;
                        remaining.Remove(other.thingIDNumber);
                        queue.Enqueue(other.thingIDNumber);
                    }
                }
                result.Add(component);
            }
            return result;
        }

        private static string ResidenceExit(CARegionalSettlementRecord record,
            Map map, int pawnId)
        {
            CASettlementResidenceAssignment assignment = record
                ?.residenceAssignments?.LastOrDefault(item => item != null
                    && item.pawnId == pawnId && !item.active);
            if (assignment != null && !assignment.exitKind.NullOrEmpty())
                return assignment.exitKind;
            Pawn corpsePawn = map?.listerThings?
                .ThingsInGroup(ThingRequestGroup.Corpse)
                .OfType<Corpse>().Select(corpse => corpse.InnerPawn)
                .FirstOrDefault(pawn => pawn?.thingIDNumber == pawnId);
            Pawn worldPawn = Find.WorldPawns?.AllPawnsAliveOrDead?
                .FirstOrDefault(pawn => pawn?.thingIDNumber == pawnId);
            return corpsePawn?.Dead == true || worldPawn?.Dead == true
                ? "death" : "left";
        }

        private static bool RepresentedDomesticLink(Pawn left, Pawn right)
        {
            if (left == null || right == null || left == right) return false;
            if (left.ownership?.OwnedBed != null
                && left.ownership.OwnedBed == right.ownership?.OwnedBed)
                return true;
            if (LovePartnerRelationUtility.LovePartnerRelationExists(left,
                    right)) return true;
            return left.relations?.DirectRelations.Any(relation =>
                relation?.otherPawn == right
                && relation.def?.familyByBloodRelation == true) == true;
        }

        private static CADomesticUnitKind UnitKind(List<Pawn> members)
        {
            if (members.Count <= 1) return CADomesticUnitKind.Individual;
            bool family = members.Any(left => members.Any(right =>
                left != right && (LovePartnerRelationUtility
                    .LovePartnerRelationExists(left, right)
                    || left.relations?.DirectRelations.Any(relation =>
                        relation?.otherPawn == right
                        && relation.def?.familyByBloodRelation == true)
                        == true)));
            return family ? CADomesticUnitKind.Family
                : CADomesticUnitKind.SharedBed;
        }

        private static string ResidentialScope(List<Pawn> members)
        {
            Building_Bed shared = members.Select(pawn =>
                    pawn.ownership?.OwnedBed).Where(bed => bed != null)
                .GroupBy(bed => bed.thingIDNumber)
                .Where(group => group.Count() > 1)
                .Select(group => group.First()).FirstOrDefault();
            return shared == null ? "residence not yet materialized"
                : "shared owned bed:" + shared.thingIDNumber;
        }

        private static string ComponentEvidence(List<Pawn> members)
        {
            foreach (Pawn left in members)
                foreach (Pawn right in members)
                    if (left != right
                        && RepresentedDomesticLink(left, right))
                    {
                        MembershipEvidence(members, left, out string kind,
                            out string identity);
                        return kind + ": " + identity;
                    }
            return "individual resident";
        }

        private static void MembershipEvidence(List<Pawn> members, Pawn pawn,
            out string sourceKind, out string sourceIdentity)
        {
            if (members.Count <= 1)
            {
                sourceKind = "individual";
                sourceIdentity = "pawn:" + pawn.thingIDNumber;
                return;
            }
            foreach (Pawn other in members)
            {
                if (other == pawn) continue;
                DirectPawnRelation relation = pawn.relations?.DirectRelations
                    .FirstOrDefault(item => item?.otherPawn == other
                        && (item.def?.familyByBloodRelation == true
                            || LovePartnerRelationUtility
                                .IsLovePartnerRelation(item.def)));
                if (relation != null)
                {
                    sourceKind = "pawn relation";
                    sourceIdentity = relation.def.defName + ":"
                        + other.thingIDNumber;
                    return;
                }
                if (pawn.ownership?.OwnedBed != null
                    && pawn.ownership.OwnedBed == other.ownership?.OwnedBed)
                {
                    sourceKind = "shared owned bed";
                    sourceIdentity = "thing:"
                        + pawn.ownership.OwnedBed.thingIDNumber;
                    return;
                }
            }
            sourceKind = "individual";
            sourceIdentity = "pawn:" + pawn.thingIDNumber;
        }

        private static void RecordTransition(
            CARegionalSettlementRecord record, string unitIdentity,
            int pawnId, string transition, string evidence)
        {
            int sequence = record.domesticMembershipTransitions.Count == 0
                ? 1 : record.domesticMembershipTransitions.Max(item =>
                    item?.sequence ?? 0) + 1;
            record.domesticMembershipTransitions.Add(
                new CADomesticMembershipTransition
                {
                    sequence = sequence,
                    unitIdentity = unitIdentity,
                    pawnId = pawnId,
                    transition = transition,
                    evidence = evidence,
                    recordedTick = Find.TickManager?.TicksGame ?? -1
                });
        }
    }
}
