using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    public static class CA_AssaultDefs
    {
        private static DutyDef assaultAware;
        private static JobDef safeIgnite;
        private static JobDef searchKnownContact;

        public static DutyDef AssaultAware
        {
            get
            {
                return assaultAware ?? (assaultAware =
                    DefDatabase<DutyDef>.GetNamed("CA_AssaultAware"));
            }
        }

        public static JobDef SafeIgnite
        {
            get
            {
                return safeIgnite ?? (safeIgnite =
                    DefDatabase<JobDef>.GetNamed("CA_SafeIgnite"));
            }
        }

        public static JobDef SearchKnownContact
        {
            get
            {
                return searchKnownContact ?? (searchKnownContact =
                    DefDatabase<JobDef>.GetNamed("CA_SearchKnownContact"));
            }
        }
    }

    // Session-only, map-scoped state. Knowledge contacts themselves are not save
    // facts, so completed searches and the short handoff from giver to driver use
    // the same lifetime and cannot bleed across maps or loaded games.
    public class AssaultSearchMapComponent : MapComponent
    {
        private struct ContactPair : IEquatable<ContactPair>
        {
            public readonly int PawnId;
            public readonly int HostileId;

            public ContactPair(int pawnId, int hostileId)
            {
                PawnId = pawnId;
                HostileId = hostileId;
            }

            public bool Equals(ContactPair other)
            {
                return PawnId == other.PawnId && HostileId == other.HostileId;
            }

            public override bool Equals(object obj)
            {
                return obj is ContactPair && Equals((ContactPair)obj);
            }

            public override int GetHashCode()
            {
                return unchecked((PawnId * 397) ^ HostileId);
            }
        }

        private struct PendingFact
        {
            public readonly int HostileId;
            public readonly int SourceTick;

            public PendingFact(int hostileId, int sourceTick)
            {
                HostileId = hostileId;
                SourceTick = sourceTick;
            }
        }

        private struct SearchedFact
        {
            public readonly int SourceTick;
            public readonly IntVec3 Cell;

            public SearchedFact(int sourceTick, IntVec3 cell)
            {
                SourceTick = sourceTick;
                Cell = cell;
            }
        }

        private struct ObservationFact
        {
            public readonly IntVec3 Cell;
            public readonly int EligibleAgainTick;

            public ObservationFact(IntVec3 cell, int eligibleAgainTick)
            {
                Cell = cell;
                EligibleAgainTick = eligibleAgainTick;
            }
        }

        private readonly Dictionary<ContactPair, SearchedFact> searchedFacts =
            new Dictionary<ContactPair, SearchedFact>();
        private readonly Dictionary<int, PendingFact> pendingJobs =
            new Dictionary<int, PendingFact>();
        private readonly Dictionary<int, ObservationFact> observations =
            new Dictionary<int, ObservationFact>();

        public AssaultSearchMapComponent(Map map) : base(map) { }

        public bool WasSearched(Pawn pawn, ThreatContactSnapshot contact)
        {
            var key = new ContactPair(pawn.thingIDNumber, contact.HostileId);
            SearchedFact searched;
            if (!searchedFacts.TryGetValue(key, out searched))
                return false;
            if (contact.SourceTick > searched.SourceTick)
            {
                searchedFacts.Remove(key);
                return false;
            }
            return contact.SourceTick < searched.SourceTick
                || contact.Cell == searched.Cell;
        }

        public void MarkSearched(int pawnId, int hostileId, int sourceTick,
            IntVec3 cell)
        {
            searchedFacts[new ContactPair(pawnId, hostileId)] =
                new SearchedFact(sourceTick, cell);
        }

        public void RegisterJob(Job job, ThreatContactSnapshot contact)
        {
            pendingJobs[job.loadID] = new PendingFact(
                contact.HostileId, contact.SourceTick);
        }

        public bool TryTakeJobFact(int jobId, out int hostileId,
            out int sourceTick)
        {
            PendingFact fact;
            if (!pendingJobs.TryGetValue(jobId, out fact))
            {
                hostileId = -1;
                sourceTick = -1;
                return false;
            }
            pendingJobs.Remove(jobId);
            hostileId = fact.HostileId;
            sourceTick = fact.SourceTick;
            return true;
        }

        public void MarkObservation(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || !cell.IsValid) return;
            observations[pawn.thingIDNumber] = new ObservationFact(cell,
                Find.TickManager.TicksGame + 900);
        }

        public bool RecentlyObserved(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null) return false;
            ObservationFact fact;
            if (!observations.TryGetValue(pawn.thingIDNumber, out fact))
                return false;
            if (Find.TickManager.TicksGame >= fact.EligibleAgainTick)
            {
                observations.Remove(pawn.thingIDNumber);
                return false;
            }
            return fact.Cell == cell;
        }

        // This live lookup is only an honest-visibility interrupt. Search targets
        // are always copied snapshot cells and never the resolved pawn position.
        public static bool CanHonestlySeeTrackedHostile(Pawn observer,
            int hostileId)
        {
            if (observer == null || observer.Map == null) return false;
            IReadOnlyList<Pawn> all = observer.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn candidate = all[i];
                if (candidate.thingIDNumber == hostileId
                    && KnowledgeMapComponent.CanCurrentlySeeHostile(
                        observer, candidate, 65f))
                    return true;
            }
            return false;
        }
    }

    // LordToil_AssaultColony exposes no duty-selection seam. This prefix changes
    // only the exact generic assault toil inside the exact generic assault LordJob;
    // the original Lord, graph, transitions, and all other assault families remain.
    public static class AssaultAwarenessPatch
    {
        private static FieldInfo attackDownedIfStarvingField;
        private static FieldInfo canPickUpOpportunisticWeaponsField;

        public static void TryInstall(Harmony harmony)
        {
            try
            {
                Type toilType = typeof(LordToil_AssaultColony);
                attackDownedIfStarvingField = AccessTools.Field(
                    toilType, "attackDownedIfStarving");
                canPickUpOpportunisticWeaponsField = AccessTools.Field(
                    toilType, "canPickUpOpportunisticWeapons");
                if (attackDownedIfStarvingField == null
                    || canPickUpOpportunisticWeaponsField == null)
                    throw new MissingFieldException(toilType.FullName,
                        "assault duty option fields");

                MethodInfo target = AccessTools.Method(
                    toilType, nameof(LordToil_AssaultColony.UpdateAllDuties));
                MethodInfo prefix = AccessTools.Method(
                    typeof(AssaultAwarenessPatch), nameof(UpdateAllDutiesPrefix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            }
            catch (Exception e)
            {
                Log.Warning("[Colonist Awareness] assault-awareness duty stood down: "
                    + e.Message);
            }
        }

        public static bool UpdateAllDutiesPrefix(LordToil_AssaultColony __instance)
        {
            Lord lord = __instance != null ? __instance.lord : null;
            if (__instance == null
                || __instance.GetType() != typeof(LordToil_AssaultColony)
                || lord == null
                || lord.CurLordToil != __instance
                || lord.LordJob == null
                || lord.LordJob.GetType() != typeof(LordJob_AssaultColony))
                return true;

            bool attackDownedIfStarving =
                (bool)attackDownedIfStarvingField.GetValue(__instance);
            bool canPickUpOpportunisticWeapons =
                (bool)canPickUpOpportunisticWeaponsField.GetValue(__instance);

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn.mindState == null) continue;

                PawnDuty duty = pawn.mindState.duty;
                if (duty != null && duty.def == CA_AssaultDefs.AssaultAware)
                {
                    // Preserve the two private vanilla constructor options even
                    // when duties are refreshed without causing job churn.
                    duty.attackDownedIfStarving = attackDownedIfStarving;
                    duty.pickupOpportunisticWeapon = canPickUpOpportunisticWeapons;
                    continue;
                }

                duty = new PawnDuty(CA_AssaultDefs.AssaultAware);
                duty.attackDownedIfStarving = attackDownedIfStarving;
                duty.pickupOpportunisticWeapon = canPickUpOpportunisticWeapons;
                pawn.mindState.duty = duty;
                pawn.TryGetComp<CompCanBeDormant>()?.WakeUp();
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }
            return false;
        }
    }

    // Deliberate objective selection follows visible combat in the duty tree.
    // It never resolves a remembered pawn id to a live pawn: lost contacts are
    // approached only through their copied remembered cell.
    public class JobGiver_CAAssaultDecision : ThinkNode_JobGiver
    {
        private const float SabotageCommitRange = 24f;
        private const float ProjectedFireRadius = 6f;
        private const float EgressSearchRadius = 15f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!IsExactGenericAssaulter(pawn)) return null;

            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings != null && settings.knowledgeContacts)
            {
                KnowledgeMapComponent knowledge =
                    KnowledgeMapComponent.For(pawn.Map);
                AssaultSearchMapComponent searches =
                    pawn.Map.GetComponent<AssaultSearchMapComponent>();
                List<ThreatContactSnapshot> contacts = knowledge != null
                    ? knowledge.FreshContacts(pawn)
                    : new List<ThreatContactSnapshot>();
                contacts.Sort(delegate(ThreatContactSnapshot a,
                    ThreatContactSnapshot b)
                {
                    int byTick = b.SourceTick.CompareTo(a.SourceTick);
                    return byTick != 0 ? byTick
                        : a.HostileId.CompareTo(b.HostileId);
                });

                for (int i = 0; i < contacts.Count; i++)
                {
                    ThreatContactSnapshot contact = contacts[i];
                    if (!contact.Cell.IsValid
                        || !contact.Cell.InBounds(pawn.Map)
                        || AssaultSearchMapComponent.CanHonestlySeeTrackedHostile(
                            pawn, contact.HostileId)
                        || searches.WasSearched(pawn, contact))
                        continue;

                    var current = pawn.jobs != null
                        ? pawn.jobs.curDriver as JobDriver_CASearchKnownContact
                        : null;
                    if (current != null && current.Matches(contact))
                        return pawn.CurJob;

                    Job search;
                    if (TryMakeContactSearch(pawn, contact, searches, out search))
                        return search;

                    // An unreachable remembered area is a completed attempt for
                    // this exact fact; do not regenerate it every think pass.
                    searches.MarkSearched(pawn.thingIDNumber,
                        contact.HostileId, contact.SourceTick, contact.Cell);
                }
            }

            Building sabotageTarget;
            IntVec3 approach;
            IntVec3 egress;
            if (TryFindSafeSabotage(pawn, out sabotageTarget,
                out approach, out egress))
            {
                Job ignite = JobMaker.MakeJob(
                    CA_AssaultDefs.SafeIgnite, sabotageTarget, approach, egress);
                ignite.expiryInterval = 900;
                ignite.checkOverrideOnExpire = true;
                ignite.collideWithPawns = true;
                return ignite;
            }

            Building visibleAnchor = FindVisibleColonyAnchor(pawn);
            if (visibleAnchor != null)
            {
                IntVec3 observation;
                if (TryFindObservationCell(pawn, visibleAnchor.Position, 16f,
                    requireProgress: true, out observation))
                    return ReconJob(pawn, observation, visibleAnchor.Position,
                        pawn.Map.GetComponent<AssaultSearchMapComponent>());
            }

            IntVec3 exploration;
            if (TryFindExplorationStep(pawn, out exploration))
                return GotoJob(exploration, 300);

            return WatchJob(pawn, pawn.Map.Center, 180);
        }

        private static bool IsExactGenericAssaulter(Pawn pawn)
        {
            Lord lord = pawn != null ? pawn.GetLord() : null;
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed
                && lord != null
                && lord.LordJob != null
                && lord.LordJob.GetType() == typeof(LordJob_AssaultColony)
                && lord.CurLordToil != null
                && lord.CurLordToil.GetType() == typeof(LordToil_AssaultColony);
        }

        private static Job ReconJob(Pawn pawn, IntVec3 destination,
            IntVec3 watch, AssaultSearchMapComponent searches)
        {
            if (pawn.Position == destination)
            {
                searches?.MarkObservation(pawn, destination);
                return WatchJob(pawn, watch, 120);
            }
            return GotoJob(destination, 360);
        }

        private static Job GotoJob(IntVec3 destination, int expiry)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Goto, destination);
            job.expiryInterval = expiry;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.collideWithPawns = true;
            return job;
        }

        private static readonly IntVec3[] SearchDirections =
        {
            new IntVec3(0, 0, 1),
            new IntVec3(1, 0, 1),
            new IntVec3(1, 0, 0),
            new IntVec3(1, 0, -1),
            new IntVec3(0, 0, -1),
            new IntVec3(-1, 0, -1),
            new IntVec3(-1, 0, 0),
            new IntVec3(-1, 0, 1)
        };

        private static bool TryMakeContactSearch(Pawn pawn,
            ThreatContactSnapshot contact, AssaultSearchMapComponent searches,
            out Job result)
        {
            result = null;
            int age = Math.Max(0,
                Find.TickManager.TicksGame - contact.SourceTick);
            float uncertainty = contact.Evidence.Uncertainty
                + (1f - contact.Evidence.Confidence) * 2f;
            int radius = Math.Max(2, Math.Min(8,
                2 + (int)Math.Ceiling(uncertainty) + age / 1200));
            int desiredCount = Math.Max(3, Math.Min(7,
                3 + (int)Math.Ceiling(uncertainty * 0.5f) + age / 1800));

            var cells = new List<IntVec3>(desiredCount);
            IntVec3 cell;
            if (TryFindSearchCellNear(pawn, contact.Cell, cells, out cell))
                cells.Add(cell);

            int start = unchecked((pawn.thingIDNumber * 397
                ^ contact.HostileId ^ contact.SourceTick) & int.MaxValue)
                % SearchDirections.Length;
            for (int i = 0; i < SearchDirections.Length
                && cells.Count < desiredCount; i++)
            {
                IntVec3 direction = SearchDirections[
                    (start + i) % SearchDirections.Length];
                IntVec3 intended = new IntVec3(
                    contact.Cell.x + direction.x * radius, 0,
                    contact.Cell.z + direction.z * radius);
                if (TryFindSearchCellNear(pawn, intended, cells, out cell))
                    cells.Add(cell);
            }
            if (cells.Count == 0) return false;

            Job search = JobMaker.MakeJob(
                CA_AssaultDefs.SearchKnownContact, cells[0], contact.Cell);
            search.targetQueueA = new List<LocalTargetInfo>();
            for (int i = 1; i < cells.Count; i++)
                search.targetQueueA.Add(cells[i]);
            search.locomotionUrgency = LocomotionUrgency.Jog;
            search.collideWithPawns = true;
            searches.RegisterJob(search, contact);
            result = search;
            return true;
        }

        private static bool TryFindSearchCellNear(Pawn pawn, IntVec3 intended,
            List<IntVec3> used, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            int bestDistance = int.MaxValue;
            foreach (IntVec3 candidate in CellRect.CenteredOn(intended, 2)
                .ClipInsideMap(pawn.Map))
            {
                if (used.Contains(candidate)
                    || !candidate.WalkableBy(pawn.Map, pawn)
                    || candidate.ContainsStaticFire(pawn.Map)
                    || !pawn.CanReach(candidate, PathEndMode.OnCell,
                        Danger.Deadly))
                    continue;
                int distance = candidate.DistanceToSquared(intended);
                if (distance < bestDistance
                    || (distance == bestDistance
                        && CellOrder(candidate, result) < 0))
                {
                    bestDistance = distance;
                    result = candidate;
                }
            }
            return result.IsValid;
        }

        private static Job WatchJob(Pawn pawn, IntVec3 watch, int expiry)
        {
            // Reconnaissance without a known contact is a finite observation, not
            // the known-contact combat posture whose driver correctly ends when no
            // threat fact exists. Native maintain-posture gives higher-priority
            // AIFightEnemies a clean interrupt while this purpose runs to expiry.
            Job job = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
            job.expiryInterval = expiry;
            job.checkOverrideOnExpire = true;
            job.overrideFacing = watch.IsValid
                ? LordJob_CAStackBreach.FacingFromTo(pawn.Position, watch)
                : Rot4.Invalid;
            return job;
        }

        private static bool TryFindObservationCell(Pawn pawn, IntVec3 anchor,
            float preferredDistance, bool requireProgress, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            Map map = pawn.Map;
            float currentDistance = pawn.Position.DistanceTo(anchor);
            float searchRadius = Math.Min(25f, preferredDistance + 5f);
            float bestScore = float.MinValue;
            AssaultSearchMapComponent searches =
                map.GetComponent<AssaultSearchMapComponent>();

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(
                anchor, searchRadius, useCenter: true))
            {
                if (!cell.InBounds(map) || !cell.WalkableBy(map, pawn)) continue;
                if (searches != null && searches.RecentlyObserved(pawn, cell))
                    continue;
                if (((cell.x + cell.z) & 1) != (pawn.thingIDNumber & 1)
                    && cell != pawn.Position) continue;

                float fromAnchor = cell.DistanceTo(anchor);
                if (fromAnchor < 7f) continue;
                if (requireProgress && currentDistance > preferredDistance + 3f
                    && fromAnchor >= currentDistance - 2f) continue;
                if (!GenSight.LineOfSight(cell, anchor, map, true)) continue;
                if (!FireClear(cell, map, 2f)) continue;
                if (!map.pawnDestinationReservationManager.CanReserve(cell, pawn))
                    continue;

                float cover = CoverUtility.CalculateOverallBlockChance(
                    cell, anchor, map);
                float travel = pawn.Position.DistanceTo(cell);
                float score = cover * 12f
                    - Math.Abs(fromAnchor - preferredDistance) * 0.55f
                    - travel * 0.025f;
                if (!cell.Roofed(map)) score += 0.2f;
                if (cell == pawn.Position) score += 0.1f;
                if (score <= bestScore) continue;
                if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                    continue;

                bestScore = score;
                result = cell;
            }
            return result.IsValid;
        }

        private static bool TryFindSafeSabotage(Pawn pawn,
            out Building target, out IntVec3 approach, out IntVec3 egress)
        {
            target = null;
            approach = IntVec3.Invalid;
            egress = IntVec3.Invalid;
            if (pawn.natives == null || pawn.natives.IgniteVerb == null
                || !pawn.natives.IgniteVerb.IsStillUsableBy(pawn)) return false;

            float bestScore = float.MinValue;
            List<Building> buildings = pawn.Map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building candidate = buildings[i];
                if (!IsMeaningfulVisibleIgnitionTarget(pawn, candidate)) continue;
                if (!candidate.Position.InHorDistOf(
                    pawn.Position, SabotageCommitRange)) continue;

                IntVec3 candidateApproach;
                if (!TryFindDeterministicTouchCell(
                    pawn, candidate, out candidateApproach))
                    continue;
                IntVec3 candidateEgress;
                if (!TryFindEgress(pawn, candidate,
                    candidateApproach, out candidateEgress))
                    continue;

                float score = MeaningScore(candidate)
                    - pawn.Position.DistanceTo(candidate.Position) * 0.08f;
                if (score > bestScore
                    || (Math.Abs(score - bestScore) < 0.001f
                        && (target == null
                            || candidate.thingIDNumber < target.thingIDNumber)))
                {
                    bestScore = score;
                    target = candidate;
                    approach = candidateApproach;
                    egress = candidateEgress;
                }
            }
            return target != null && approach.IsValid && egress.IsValid;
        }

        private static bool IsMeaningfulVisibleIgnitionTarget(Pawn pawn, Building b)
        {
            if (b == null || b.Destroyed || b.Map != pawn.Map
                || b.Faction != Faction.OfPlayer || b.def.building == null
                || !b.def.useHitPoints || b.def.building.ai_neverTrashThis
                || b.def.building.isTrap || b.def.building.isInert
                || b.def.building.isFence || b.def.building.isWall
                || b.def.building.isPowerConduit || b.def.IsFrame
                || b is Building_Door || b.IsBurning() || !b.FlammableNow
                || !pawn.HostileTo(b)) return false;
            if (!pawn.CanReserve(b, 1, -1, null, false)) return false;

            CompCanBeDormant dormant = b.TryGetComp<CompCanBeDormant>();
            if (dormant != null && !dormant.Awake) return false;
            if (!GenSight.LineOfSightToThing(pawn.Position, b, pawn.Map, true))
                return false;
            if (FireUtility.GetEffectiveVacuumForFire(b.Position, pawn.Map) > 0f)
                return false;
            if (!FireClear(b.Position, pawn.Map, 3f)) return false;
            if (!pawn.CanReach(b, PathEndMode.Touch, Danger.Some)) return false;

            return b.MarketValue >= 100f
                || b.def.building.IsTurret
                || b.def.building.isMealSource
                || b is Building_Bed
                || b.TryGetComp<CompPowerTrader>() != null;
        }

        private static float MeaningScore(Building b)
        {
            float score = Math.Min(b.MarketValue, 2500f) / 100f;
            if (b.def.building.IsTurret) score += 12f;
            if (b.TryGetComp<CompPowerTrader>() != null) score += 8f;
            if (b.def.building.isMealSource) score += 6f;
            if (b is Building_Bed) score += 3f;
            return score;
        }

        private static bool TryFindDeterministicTouchCell(Pawn pawn,
            Building target, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            int bestDistance = int.MaxValue;
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(target))
            {
                if (!cell.InBounds(pawn.Map) || !cell.WalkableBy(pawn.Map, pawn)
                    || !FireClear(cell, pawn.Map, 1f)) continue;
                if (!pawn.CanReserve(cell, 1, -1, null, false)) continue;
                if (!ReachabilityImmediate.CanReachImmediate(
                    cell, target, pawn.Map, PathEndMode.Touch, pawn)) continue;
                if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                    continue;

                int distance = pawn.Position.DistanceToSquared(cell);
                if (distance < bestDistance
                    || (distance == bestDistance && CellOrder(cell, result) < 0))
                {
                    bestDistance = distance;
                    result = cell;
                }
            }
            return result.IsValid;
        }

        private static bool TryFindEgress(Pawn pawn, Building target,
            IntVec3 approach, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            float bestScore = float.MinValue;
            Map map = pawn.Map;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(
                target.Position, EgressSearchRadius, useCenter: false))
            {
                if (!cell.InBounds(map) || !cell.WalkableBy(map, pawn)) continue;
                float fireDistance = target.OccupiedRect()
                    .ClosestCellTo(cell).DistanceTo(cell);
                if (fireDistance < ProjectedFireRadius) continue;
                if (cell.Roofed(map) || !FireClear(cell, map, 2.5f)) continue;
                if (!pawn.CanReserve(cell, 1, -1, null, false)) continue;
                if (!map.reachability.CanReach(approach, cell,
                    PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly)))
                    continue;

                float score = -Math.Abs(fireDistance - 9f) * 0.4f
                    - approach.DistanceTo(cell) * 0.035f
                    + CoverUtility.TotalSurroundingCoverScore(cell, map) * 0.25f;
                if (score > bestScore
                    || (Math.Abs(score - bestScore) < 0.001f
                        && CellOrder(cell, result) < 0))
                {
                    bestScore = score;
                    result = cell;
                }
            }
            return result.IsValid;
        }

        private static Building FindVisibleColonyAnchor(Pawn pawn)
        {
            Building best = null;
            float bestScore = float.MinValue;
            List<Building> buildings = pawn.Map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building b = buildings[i];
                if (b == null || b.Destroyed || b.Faction != Faction.OfPlayer
                    || b.def.building == null || !b.def.useHitPoints
                    || b.IsBurning() || !GenSight.LineOfSightToThing(
                        pawn.Position, b, pawn.Map, true)) continue;

                float score = -pawn.Position.DistanceTo(b.Position);
                if (!b.def.building.isFence && !b.def.building.isWall) score += 5f;
                if (score > bestScore
                    || (Math.Abs(score - bestScore) < 0.001f
                        && (best == null || b.thingIDNumber < best.thingIDNumber)))
                {
                    bestScore = score;
                    best = b;
                }
            }
            return best;
        }

        // Strategic ingress uses only the map's geometric center. It is not a
        // query for a hidden pawn, building, room, home-area cell, or live target.
        private static bool TryFindExplorationStep(Pawn pawn, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            IntVec3 center = pawn.Map.Center;
            if (pawn.Position.InHorDistOf(center, 8f)) return false;

            int dx = Math.Sign(center.x - pawn.Position.x);
            int dz = Math.Sign(center.z - pawn.Position.z);
            IntVec3 intended = pawn.Position + new IntVec3(dx * 16, 0, dz * 16);
            float best = float.MaxValue;
            foreach (IntVec3 cell in CellRect.CenteredOn(intended, 4)
                .ClipInsideMap(pawn.Map))
            {
                if (!cell.WalkableBy(pawn.Map, pawn)
                    || !FireClear(cell, pawn.Map, 2f)
                    || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                    continue;
                float score = cell.DistanceTo(center)
                    - CoverUtility.TotalSurroundingCoverScore(cell, pawn.Map) * 0.3f;
                if (score < best
                    || (Math.Abs(score - best) < 0.001f
                        && CellOrder(cell, result) < 0))
                {
                    best = score;
                    result = cell;
                }
            }
            return result.IsValid;
        }

        private static bool FireClear(IntVec3 center, Map map, float radius)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(
                center, radius, useCenter: true))
            {
                if (!cell.InBounds(map) || cell.ContainsStaticFire(map))
                    return false;
            }
            return true;
        }

        private static int CellOrder(IntVec3 a, IntVec3 b)
        {
            if (!b.IsValid) return -1;
            int byX = a.x.CompareTo(b.x);
            return byX != 0 ? byX : a.z.CompareTo(b.z);
        }
    }

    // Target A and its queue are copied search cells; target B is the remembered
    // contact cell. The tracked pawn id is used only to interrupt on honest sight,
    // never to replace those cells with a live position.
    public class JobDriver_CASearchKnownContact : JobDriver
    {
        private const TargetIndex SearchTarget = TargetIndex.A;
        private const int SurveyTicks = 270;

        private int hostileId = -1;
        private int sourceTick = -1;
        private bool completedSweep;

        public override void ExposeData()
        {
            base.ExposeData();
            // The native-record preflight requires both fields present;
            // the pre-toil defaults must still serialize.
            Scribe_Values.Look(ref hostileId, "hostileId", -1,
                forceSave: true);
            Scribe_Values.Look(ref sourceTick, "sourceTick", -1,
                forceSave: true);
            Scribe_Values.Look(ref completedSweep, "completedSweep", false);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (hostileId >= 0 && sourceTick >= 0) return true;
            AssaultSearchMapComponent searches = pawn.Map != null
                ? pawn.Map.GetComponent<AssaultSearchMapComponent>() : null;
            return searches != null && searches.TryTakeJobFact(
                job.loadID, out hostileId, out sourceTick);
        }

        // The giver returns the current Job object for the same fact. Any distinct
        // search job therefore represents newer or different contact evidence and
        // must not inherit JobDriver's unconditional continuation default.
        public override bool IsContinuation(Job nextJob)
        {
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(delegate
            {
                return ShouldReplan()
                    ? JobCondition.InterruptForced : JobCondition.Ongoing;
            });
            AddFinishAction(delegate(JobCondition condition)
            {
                bool exhausted = (condition == JobCondition.Succeeded
                        && completedSweep)
                    || condition == JobCondition.ErroredPather;
                if (!exhausted || pawn.Map == null) return;
                pawn.Map.GetComponent<AssaultSearchMapComponent>()
                    .MarkSearched(pawn.thingIDNumber, hostileId, sourceTick,
                        job.targetB.Cell);
            });

            Toil go = Toils_Goto.GotoCell(SearchTarget, PathEndMode.OnCell);
            yield return go;

            // Hidden discovery checks on a 250-tick cadence. A 270-tick survey
            // gives every reached waypoint one full opportunity to reacquire.
            Toil survey = Toils_General.Wait(SurveyTicks);
            survey.socialMode = RandomSocialMode.Off;
            yield return survey;

            Toil next = ToilMaker.MakeToil("CAAdvanceContactSearch");
            next.initAction = delegate
            {
                if (job.targetQueueA != null && job.targetQueueA.Count > 0)
                {
                    job.targetA = job.targetQueueA[0];
                    job.targetQueueA.RemoveAt(0);
                    JumpToToil(go);
                    return;
                }
                completedSweep = true;
            };
            yield return next;
        }

        private bool ShouldReplan()
        {
            if (pawn == null || pawn.Map == null) return true;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.knowledgeContacts) return true;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
            ThreatContactSnapshot current;
            if (knowledge == null
                || !knowledge.TryGetFreshContact(pawn, hostileId, out current))
                return true;
            // A fact that merely re-timestamped (combat relays refresh
            // continuously) is not a new situation. Replan only when the
            // quarry MEANINGFULLY moved; absorb bookkeeping updates in place
            // so the search commits instead of re-deciding every tick.
            if (current.Cell.DistanceTo(job.targetB.Cell) > 5.9f)
                return true;
            sourceTick = current.SourceTick;
            return AssaultSearchMapComponent.CanHonestlySeeTrackedHostile(
                pawn, hostileId);
        }

        internal bool Matches(ThreatContactSnapshot contact)
        {
            // Same quarry within the committed search area = the running job
            // already answers this fact; tick and small drift are absorbed by
            // the driver.
            return hostileId == contact.HostileId
                && job != null
                && job.targetB.Cell.DistanceTo(contact.Cell) <= 5.9f;
        }
    }

    // Target A is the one visible, meaningful sabotage objective; target B is the
    // exact validated touch cell and target C the matching open-air egress.
    // ThinkNode_Duty attaches the existing assault Lord to this job, so the pawn
    // never leaves or replaces the original graph.
    public class JobDriver_CASafeIgnite : JobDriver
    {
        private const TargetIndex IgniteTarget = TargetIndex.A;
        private const TargetIndex ApproachTarget = TargetIndex.B;
        private const TargetIndex EgressTarget = TargetIndex.C;

        private Thing TargetThing => job.GetTarget(IgniteTarget).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!job.targetB.Cell.IsValid || !job.targetC.Cell.IsValid
                || TargetThing == null) return false;
            if (!pawn.Reserve(TargetThing, job, 1, -1, null, errorOnFailed))
                return false;
            if (!pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed))
                return false;
            return pawn.Reserve(job.targetC, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(IgniteTarget);

            Toil approach = Toils_Goto.GotoCell(
                ApproachTarget, PathEndMode.OnCell)
                .FailOnBurningImmobile(IgniteTarget)
                .FailOnCannotReach(ApproachTarget, PathEndMode.OnCell);
            yield return approach;

            Toil ignite = ToilMaker.MakeToil("CASafeIgnite");
            ignite.initAction = delegate
            {
                if (!SafeToIgniteFromPlannedCell()
                    || !pawn.natives.TryStartIgnite(TargetThing))
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
            };
            yield return ignite;

            // The path may initialize during the ignite verb's busy stance; RimWorld
            // holds movement until that stance completes, then follows the stored exit.
            Toil withdraw = Toils_Goto.GotoCell(EgressTarget, PathEndMode.OnCell);
            withdraw.socialMode = RandomSocialMode.Off;
            yield return withdraw;
        }

        private bool SafeToIgniteFromPlannedCell()
        {
            Thing target = TargetThing;
            if (target == null || target.Destroyed || target.IsBurning()
                || pawn.Map == null || pawn.Position != job.targetB.Cell)
                return false;
            if (!ReachabilityImmediate.CanReachImmediate(
                pawn.Position, target, pawn.Map, PathEndMode.Touch, pawn))
                return false;

            IntVec3 exit = job.targetC.Cell;
            if (!exit.IsValid || !exit.InBounds(pawn.Map)
                || !exit.WalkableBy(pawn.Map, pawn) || exit.Roofed(pawn.Map)
                || exit.ContainsStaticFire(pawn.Map)
                || target.OccupiedRect().ClosestCellTo(exit)
                    .DistanceTo(exit) < 6f)
                return false;
            return pawn.CanReach(exit, PathEndMode.OnCell, Danger.Deadly);
        }
    }
}
