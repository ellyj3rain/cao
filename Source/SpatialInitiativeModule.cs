using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal enum CASpatialAuthorityKind : byte
    {
        NativeStockpile = 0,
        SpaceProgram = 1
    }

    internal sealed class CASpatialInitiativeRecord : IExposable
    {
        private const int CurrentInitiativeSchema = 1;
        internal CASpatialAuthorityKind kind;
        internal int authorityId;
        internal CAInitiativeTier tier = CAInitiativeTier.Standard;
        private int initiativeSchema = CurrentInitiativeSchema;
        private int legacyLevel = 1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind",
                CASpatialAuthorityKind.NativeStockpile);
            Scribe_Values.Look(ref authorityId, "authorityId", 0);
            Scribe_Values.Look(ref initiativeSchema, "initiativeSchema", 0);
            if (Scribe.mode == LoadSaveMode.Saving
                || initiativeSchema >= CurrentInitiativeSchema)
                Scribe_Values.Look(ref tier, "tier",
                    CAInitiativeTier.Standard);
            else
                Scribe_Values.Look(ref legacyLevel, "level", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (initiativeSchema < CurrentInitiativeSchema)
                {
                    tier = AutonomyComponent.MigrateLegacyLevel(legacyLevel);
                    initiativeSchema = CurrentInitiativeSchema;
                }
                tier = AutonomyComponent.Normalize(tier);
            }
        }
    }

    internal sealed class CASpatialBuiltStorageRecord : IExposable
    {
        internal int zoneId;
        internal string thingId;
        internal string defName;
        internal IntVec3 cell = IntVec3.Invalid;

        public void ExposeData()
        {
            Scribe_Values.Look(ref zoneId, "zoneId", 0);
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
        }
    }

    internal sealed class CASpatialBuiltRoomRecord : IExposable
    {
        internal int programId;
        internal string thingId;
        internal string defName;
        internal IntVec3 cell = IntVec3.Invalid;
        internal List<string> expectedFocusThingIds = new List<string>();
        internal int actualLinkedBeds;

        public void ExposeData()
        {
            Scribe_Values.Look(ref programId, "programId", 0);
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Collections.Look(ref expectedFocusThingIds,
                "expectedFocusThingIds", LookMode.Value);
            Scribe_Values.Look(ref actualLinkedBeds, "actualLinkedBeds", 0);
            if (expectedFocusThingIds == null)
                expectedFocusThingIds = new List<string>();
        }
    }

    // The shared spatial initiative parameter. Both native stockpiles and
    // player-authored room programs use the same typed three-tier ladder. Only the
    // stockpile surface and authored Barracks editor expose the parameter
    // where their respective operative consumers exist. The authored Bedroom
    // facility-requirement comparator remains read-only. An exact developer
    // regression may exercise one concrete resident
    // Bed cause; no production/background player-authored Bedroom Bed-cause
    // consumer is enabled.
    public sealed class CASpatialInitiativeMapComponent : MapComponent
    {
        private const CAInitiativeTier DefaultTier =
            CAInitiativeTier.Standard;
        private const int InitialDelayTicks = 300;
        private const int CheckIntervalTicks = 600;
        private const float ProactivePressure = 0.80f;
        private const float AutonomousPressure = 0.50f;

        private List<CASpatialInitiativeRecord> initiatives =
            new List<CASpatialInitiativeRecord>();
        private Dictionary<int, int> suppressedZones =
            new Dictionary<int, int>();
        private Dictionary<int, int> suppressedPrograms =
            new Dictionary<int, int>();
        private List<CASpatialBuiltStorageRecord> completedStorage =
            new List<CASpatialBuiltStorageRecord>();
        private List<CASpatialBuiltRoomRecord> completedRoom =
            new List<CASpatialBuiltRoomRecord>();
        private int nextCheckTick;

        private int pendingZoneId;
        private string pendingDefName;
        private string pendingStuffName;
        private IntVec3 pendingCell = IntVec3.Invalid;
        private Rot4 pendingRotation = Rot4.Invalid;
        private int pendingPlannerId = -1;
        private int pendingSinceTick = -1;
        private int pendingPolicyAllowedDefs;
        private StoragePriority pendingPolicyPriority = StoragePriority.Normal;
        private float pendingPressure;
        private int pendingOccupied;
        private int pendingCapacity;
        private string pendingEvidence;
        private string pendingBehaviorKey;
        private int pendingEpisodeId;
        private int pendingAuthorityOrigin;
        private string pendingAuthorityIdentity;

        private int pendingRoomProgramId;
        private string pendingRoomDefName;
        private string pendingRoomStuffName;
        private IntVec3 pendingRoomCell = IntVec3.Invalid;
        private Rot4 pendingRoomRotation = Rot4.Invalid;
        private int pendingRoomPlannerId = -1;
        private int pendingRoomSinceTick = -1;
        private List<string> pendingRoomFocusThingIds = new List<string>();
        private int pendingRoomMissingBefore;
        private int pendingRoomExpectedLinks;
        private string pendingRoomEvidence;
        private string pendingRoomBehaviorKey;
        private int pendingRoomEpisodeId;
        private int pendingRoomAuthorityOrigin;
        private string pendingRoomAuthorityIdentity;

        private string lastOutcome = "not evaluated";
        private int lastEvaluationTick = -1;

        public CASpatialInitiativeMapComponent(Map map) : base(map) { }

        public static CASpatialInitiativeMapComponent For(Map map)
        {
            return map?.GetComponent<CASpatialInitiativeMapComponent>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref initiatives, "CA_spatialInitiatives",
                LookMode.Deep);
            Scribe_Collections.Look(ref suppressedZones,
                "CA_spatialSuppressedZones", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref suppressedPrograms,
                "CA_spatialSuppressedPrograms", LookMode.Value,
                LookMode.Value);
            Scribe_Collections.Look(ref completedStorage,
                "CA_spatialCompletedStorage", LookMode.Deep);
            Scribe_Collections.Look(ref completedRoom,
                "CA_spatialCompletedRoom", LookMode.Deep);
            Scribe_Values.Look(ref nextCheckTick, "CA_spatialNextCheckTick", 0);
            Scribe_Values.Look(ref pendingZoneId, "CA_spatialPendingZoneId", 0);
            Scribe_Values.Look(ref pendingDefName, "CA_spatialPendingDef");
            Scribe_Values.Look(ref pendingStuffName, "CA_spatialPendingStuff");
            Scribe_Values.Look(ref pendingCell, "CA_spatialPendingCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref pendingRotation, "CA_spatialPendingRotation",
                Rot4.Invalid);
            Scribe_Values.Look(ref pendingPlannerId,
                "CA_spatialPendingPlannerId", -1);
            Scribe_Values.Look(ref pendingSinceTick,
                "CA_spatialPendingSinceTick", -1);
            Scribe_Values.Look(ref pendingPolicyAllowedDefs,
                "CA_spatialPendingPolicyAllowedDefs", 0);
            Scribe_Values.Look(ref pendingPolicyPriority,
                "CA_spatialPendingPolicyPriority", StoragePriority.Normal);
            Scribe_Values.Look(ref pendingPressure,
                "CA_spatialPendingPressure", 0f);
            Scribe_Values.Look(ref pendingOccupied,
                "CA_spatialPendingOccupied", 0);
            Scribe_Values.Look(ref pendingCapacity,
                "CA_spatialPendingCapacity", 0);
            Scribe_Values.Look(ref pendingEvidence,
                "CA_spatialPendingEvidence");
            Scribe_Values.Look(ref pendingBehaviorKey,
                "CA_spatialPendingBehaviorKey");
            Scribe_Values.Look(ref pendingEpisodeId,
                "CA_spatialPendingEpisodeId", 0);
            Scribe_Values.Look(ref pendingAuthorityOrigin,
                "CA_spatialPendingAuthorityOrigin", 0);
            Scribe_Values.Look(ref pendingAuthorityIdentity,
                "CA_spatialPendingAuthorityIdentity");
            Scribe_Values.Look(ref pendingRoomProgramId,
                "CA_spatialPendingRoomProgramId", 0);
            Scribe_Values.Look(ref pendingRoomDefName,
                "CA_spatialPendingRoomDef");
            Scribe_Values.Look(ref pendingRoomStuffName,
                "CA_spatialPendingRoomStuff");
            Scribe_Values.Look(ref pendingRoomCell,
                "CA_spatialPendingRoomCell", IntVec3.Invalid);
            Scribe_Values.Look(ref pendingRoomRotation,
                "CA_spatialPendingRoomRotation", Rot4.Invalid);
            Scribe_Values.Look(ref pendingRoomPlannerId,
                "CA_spatialPendingRoomPlannerId", -1);
            Scribe_Values.Look(ref pendingRoomSinceTick,
                "CA_spatialPendingRoomSinceTick", -1);
            Scribe_Collections.Look(ref pendingRoomFocusThingIds,
                "CA_spatialPendingRoomFocusThingIds", LookMode.Value);
            Scribe_Values.Look(ref pendingRoomMissingBefore,
                "CA_spatialPendingRoomMissingBefore", 0);
            Scribe_Values.Look(ref pendingRoomExpectedLinks,
                "CA_spatialPendingRoomExpectedLinks", 0);
            Scribe_Values.Look(ref pendingRoomEvidence,
                "CA_spatialPendingRoomEvidence");
            Scribe_Values.Look(ref pendingRoomBehaviorKey,
                "CA_spatialPendingRoomBehaviorKey");
            Scribe_Values.Look(ref pendingRoomEpisodeId,
                "CA_spatialPendingRoomEpisodeId", 0);
            Scribe_Values.Look(ref pendingRoomAuthorityOrigin,
                "CA_spatialPendingRoomAuthorityOrigin", 0);
            Scribe_Values.Look(ref pendingRoomAuthorityIdentity,
                "CA_spatialPendingRoomAuthorityIdentity");
            Scribe_Values.Look(ref lastOutcome, "CA_spatialLastOutcome",
                "not evaluated");
            Scribe_Values.Look(ref lastEvaluationTick,
                "CA_spatialLastEvaluationTick", -1);
            if (initiatives == null)
                initiatives = new List<CASpatialInitiativeRecord>();
            if (suppressedZones == null)
                suppressedZones = new Dictionary<int, int>();
            if (suppressedPrograms == null)
                suppressedPrograms = new Dictionary<int, int>();
            if (completedStorage == null)
                completedStorage = new List<CASpatialBuiltStorageRecord>();
            if (completedRoom == null)
                completedRoom = new List<CASpatialBuiltRoomRecord>();
            if (pendingRoomFocusThingIds == null)
                pendingRoomFocusThingIds = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                CACombatIntent.ObserveEpisode(pendingEpisodeId);
                CACombatIntent.ObserveEpisode(pendingRoomEpisodeId);
            }
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (nextCheckTick <= 0)
            {
                nextCheckTick = now + InitialDelayTicks;
                return;
            }
            if (now < nextCheckTick) return;
            nextCheckTick = now + CheckIntervalTicks;
            if (!CABehaviorSettings.IsEnabled(
                CASettingKey.AutonomousHomePlanning,
                AwarenessMod.Settings))
            {
                lastOutcome = "spatial planning permission is disabled";
                lastEvaluationTick = now;
                return;
            }
            TryPlanNow(out _);
        }

        internal void NotifyPlanningSettingChanged(bool enabled,
            int resetGeneration)
        {
            nextCheckTick = (Find.TickManager?.TicksGame ?? 0)
                + InitialDelayTicks;
            if (enabled)
            {
                lastOutcome = "spatial planning permission enabled";
                lastEvaluationTick = Find.TickManager?.TicksGame ?? -1;
                return;
            }

            Thing storage = FindPendingStorageConstruction();
            if (storage != null && !storage.Destroyed)
                storage.Destroy(DestroyMode.Cancel);
            Thing room = FindPendingRoomConstruction();
            if (room != null && !room.Destroyed)
                room.Destroy(DestroyMode.Cancel);
            ClearPending();
            ClearRoomPending();
            lastOutcome = "spatial planning permission disabled; pending CA proposals canceled; completed structures retained";
            lastEvaluationTick = Find.TickManager?.TicksGame ?? -1;
        }

        public CAInitiativeTier TierFor(Zone_Stockpile zone)
        {
            return zone == null ? DefaultTier : TierFor(
                CASpatialAuthorityKind.NativeStockpile, zone.ID);
        }

        public CAInitiativeTier TierFor(CASpaceProgram program)
        {
            if (program == null || program.author != CASpaceAuthor.Player)
                return DefaultTier;
            return TierFor(CASpatialAuthorityKind.SpaceProgram, program.id);
        }

        public CAInitiativeTier EffectiveTier(Pawn pawn, Zone_Stockpile zone)
        {
            CAInitiativeTier pawnTier = AutonomyComponent.TierOf(pawn);
            CAInitiativeTier ceiling = TierFor(zone);
            return pawnTier < ceiling ? pawnTier : ceiling;
        }

        public CAInitiativeTier EffectiveTier(Pawn pawn,
            CASpaceProgram program)
        {
            CAInitiativeTier pawnTier = AutonomyComponent.TierOf(pawn);
            CAInitiativeTier ceiling = TierFor(program);
            return pawnTier < ceiling ? pawnTier : ceiling;
        }

        // Observation-only summary for the behavior census. Reading it never
        // prunes authority records, changes suppression, or schedules work.
        internal string CeilingsForObservation(Pawn pawn)
        {
            var entries = new List<string>();
            IReadOnlyList<Zone> allZones = map?.zoneManager?.AllZones;
            if (allZones != null)
            {
                foreach (Zone_Stockpile zone in allZones
                    .OfType<Zone_Stockpile>().OrderBy(item => item.ID))
                {
                    CAInitiativeTier ceiling = TierFor(zone);
                    entries.Add("stockpile #" + zone.ID + " "
                        + CAInitiativePresentation.Label(ceiling)
                        + (pawn == null ? "" : " (effective "
                            + CAInitiativePresentation.Label(
                                EffectiveTier(pawn, zone)) + ")"));
                }
            }
            IReadOnlyList<CASpaceProgram> programs = PlannedUseMapComponent
                .For(map)?.ProgramsForObservation
                ?? Array.Empty<CASpaceProgram>();
            foreach (CASpaceProgram program in programs
                .Where(item => item != null).OrderBy(item => item.id))
            {
                CAInitiativeTier ceiling = TierFor(program);
                entries.Add("program #" + program.id + " "
                    + CAInitiativePresentation.Label(ceiling)
                    + (pawn == null ? "" : " (effective "
                        + CAInitiativePresentation.Label(
                            EffectiveTier(pawn, program)) + ")"));
            }
            return entries.Count == 0 ? "none" : string.Join("; ",
                entries.ToArray());
        }

        public void SetTier(Zone_Stockpile zone, CAInitiativeTier tier)
        {
            if (zone == null || zone.Map != map) return;
            CAInitiativeTier prior = TierFor(zone);
            tier = AutonomyComponent.Normalize(tier);
            SetTier(CASpatialAuthorityKind.NativeStockpile, zone.ID, tier);
            if (prior != tier)
                suppressedZones.Remove(zone.ID);
            nextCheckTick = Math.Min(nextCheckTick <= 0
                ? int.MaxValue : nextCheckTick,
                Find.TickManager.TicksGame + 60);
            lastOutcome = "player set native stockpile #" + zone.ID
                + " initiative ceiling to "
                + CAInitiativePresentation.Label(TierFor(zone))
                + "; native priority and filter remain unchanged";
            lastEvaluationTick = Find.TickManager.TicksGame;
            CABehaviorRevisions.SpatialAuthorityChanged(map);
        }

        public void SetTier(CASpaceProgram program, CAInitiativeTier tier)
        {
            if (program == null || program.author != CASpaceAuthor.Player)
                return;
            CAInitiativeTier prior = TierFor(program);
            tier = AutonomyComponent.Normalize(tier);
            SetTier(CASpatialAuthorityKind.SpaceProgram, program.id, tier);
            if (prior != tier)
                suppressedPrograms.Remove(program.id);
            nextCheckTick = Math.Min(nextCheckTick <= 0
                ? int.MaxValue : nextCheckTick,
                Find.TickManager.TicksGame + 60);
            lastOutcome = "player set authored room program #" + program.id
                + " initiative ceiling to "
                + CAInitiativePresentation.Label(TierFor(program))
                + "; native room role, residents, bed ownership, and authored "
                + "footprint remain unchanged";
            lastEvaluationTick = Find.TickManager.TicksGame;
            CABehaviorRevisions.SpatialAuthorityChanged(map);
        }

        internal bool DebugPlanZoneNow(Zone_Stockpile zone,
            out string outcome)
        {
            nextCheckTick = Find.TickManager.TicksGame + CheckIntervalTicks;
            return TryPlanForZone(zone, out outcome);
        }

        internal bool DebugPlanNow(out string outcome)
        {
            nextCheckTick = Find.TickManager.TicksGame + CheckIntervalTicks;
            return TryPlanNow(out outcome);
        }

        internal bool DebugPlanProgramNow(CASpaceProgram program,
            out string outcome)
        {
            nextCheckTick = Find.TickManager.TicksGame + CheckIntervalTicks;
            return TryPlanForProgram(program, out outcome);
        }

        internal bool HasPendingPlan
        {
            get
            {
                PruneAuthorityAndStorageRecords();
                return PendingStillExists() || RoomPendingStillExists();
            }
        }

        // Receipt-safe observation of the one map-wide CA construction
        // commitment. Unlike HasPendingPlan, this does not prune associations,
        // cancel construction, or update saved lifecycle state.
        internal bool HasPendingPlanForObservation => PendingStillExists()
            || FindPendingRoomConstruction() != null;

        internal string PendingPlanForObservation
        {
            get
            {
                if (PendingStillExists())
                    return pendingDefName + " at " + pendingCell
                        + " for stockpile #" + pendingZoneId + " by pawn "
                        + pendingPlannerId;
                Thing room = FindPendingRoomConstruction();
                return room == null ? "none" : pendingRoomDefName + " at "
                    + pendingRoomCell + " for program #"
                    + pendingRoomProgramId + " by pawn "
                    + pendingRoomPlannerId;
            }
        }

        internal int CompletedStorageCount
        {
            get
            {
                PruneAuthorityAndStorageRecords();
                return completedStorage.Count;
            }
        }

        internal int CompletedRoomCount
        {
            get
            {
                PruneAuthorityAndStorageRecords();
                return completedRoom.Count;
            }
        }

        private CAInitiativeTier TierFor(CASpatialAuthorityKind kind,
            int authorityId)
        {
            CASpatialInitiativeRecord record = initiatives.FirstOrDefault(
                candidate => candidate != null && candidate.kind == kind
                    && candidate.authorityId == authorityId);
            return record == null ? DefaultTier
                : AutonomyComponent.Normalize(record.tier);
        }

        private void SetTier(CASpatialAuthorityKind kind, int authorityId,
            CAInitiativeTier tier)
        {
            tier = AutonomyComponent.Normalize(tier);
            initiatives.RemoveAll(candidate => candidate == null);
            CASpatialInitiativeRecord record = initiatives.FirstOrDefault(
                candidate => candidate.kind == kind
                    && candidate.authorityId == authorityId);
            if (tier == DefaultTier)
            {
                if (record != null) initiatives.Remove(record);
                return;
            }
            if (record == null)
            {
                record = new CASpatialInitiativeRecord
                {
                    kind = kind,
                    authorityId = authorityId
                };
                initiatives.Add(record);
            }
            record.tier = tier;
        }

        private bool TryPlanNow(out string outcome)
        {
            outcome = "no plan";
            lastEvaluationTick = Find.TickManager.TicksGame;
            if (!map.IsPlayerHome)
                return Finish("this map is not an authorized player home",
                    out outcome);
            PruneAuthorityAndStorageRecords();
            if (PendingStillExists())
                return Finish("waiting for pending " + pendingDefName + " at "
                    + pendingCell + " for native stockpile #" + pendingZoneId,
                    out outcome);
            if (RoomPendingStillExists())
                return Finish("waiting for pending " + pendingRoomDefName
                    + " at " + pendingRoomCell + " for authored room program #"
                    + pendingRoomProgramId, out outcome);
            string homeCommitment;
            if (HasHomeConstructionCommitment(out homeCommitment))
                return Finish(homeCommitment, out outcome);
            if (!pendingDefName.NullOrEmpty())
                ClearPending();
            if (!pendingRoomDefName.NullOrEmpty())
                ClearRoomPending();

            List<Zone_Stockpile> zoneCandidates = map.zoneManager.AllZones
                .OfType<Zone_Stockpile>().Where(zone => TierFor(zone)
                    >= CAInitiativeTier.Proactive)
                .OrderByDescending(zone => DomainFor(zone).Pressure)
                .ThenBy(zone => zone.ID).ToList();
            string firstBlocker = null;
            for (int i = 0; i < zoneCandidates.Count; i++)
            {
                if (TryPlanForZone(zoneCandidates[i], out outcome)) return true;
                if (firstBlocker == null) firstBlocker = outcome;
            }

            IReadOnlyList<CASpaceProgram> observed = PlannedUseMapComponent
                .For(map)?.ProgramsForObservation
                ?? Array.Empty<CASpaceProgram>();
            List<CASpaceProgram> roomCandidates = observed.Where(program =>
                    CASpatialFurnishingModule
                        .CanOriginateNewRoomFacility(program)
                    && TierFor(program) >= CAInitiativeTier.Proactive)
                .OrderBy(program => program.id).ToList();
            for (int i = 0; i < roomCandidates.Count; i++)
            {
                if (TryPlanForProgram(roomCandidates[i], out outcome))
                    return true;
                if (firstBlocker == null) firstBlocker = outcome;
            }
            if (zoneCandidates.Count == 0 && roomCandidates.Count == 0)
                return Finish("no native stockpile or operative authored "
                    + "Barracks has a Proactive or Autonomous "
                    + "initiative ceiling; Standard remains the default",
                    out outcome);
            return Finish(firstBlocker
                ?? "no authorized spatial initiative was ready", out outcome);
        }

        private bool TryPlanForZone(Zone_Stockpile zone, out string outcome)
        {
            outcome = "no plan";
            lastEvaluationTick = Find.TickManager.TicksGame;
            if (zone == null || zone.Map != map || !map.IsPlayerHome)
                return Finish("the target native stockpile is unavailable",
                    out outcome);
            if (PendingStillExists())
                return Finish("waiting for pending " + pendingDefName + " at "
                    + pendingCell, out outcome);
            if (RoomPendingStillExists())
                return Finish("waiting for pending " + pendingRoomDefName
                    + " at " + pendingRoomCell, out outcome);
            string homeCommitment;
            if (HasHomeConstructionCommitment(out homeCommitment))
                return Finish(homeCommitment, out outcome);
            if (!pendingDefName.NullOrEmpty()) ClearPending();
            if (!pendingRoomDefName.NullOrEmpty()) ClearRoomPending();

            int suppression;
            if (suppressedZones.TryGetValue(zone.ID, out suppression))
            {
                if (suppression != int.MaxValue
                    && suppression <= Find.TickManager.TicksGame)
                    suppressedZones.Remove(zone.ID);
                else
                    return Finish("native stockpile #" + zone.ID
                        + (suppression == int.MaxValue
                            ? " is vetoed until its initiative ceiling changes"
                            : " is paused after an explicit cancellation until tick "
                                + suppression), out outcome);
            }

            CAInitiativeTier effective;
            Pawn planner = ChoosePlanner(zone, out effective);
            if (planner == null || effective < CAInitiativeTier.Proactive)
                return Finish("native stockpile #" + zone.ID
                    + " has no awake, threat-unaware construction author whose "
                    + "pawn autonomy and space ceiling combine to Proactive+",
                    out outcome);
            CAStorageDomain domain = DomainFor(zone);
            float threshold = effective >= CAInitiativeTier.Autonomous
                ? AutonomousPressure : ProactivePressure;
            if (domain.Capacity <= 0 || domain.Pressure < threshold)
                return Finish("native stockpile #" + zone.ID + " pressure "
                    + Percent(domain.Pressure) + " (" + domain.OccupiedStacks
                    + "/" + domain.Capacity + " stack slots) is below the "
                    + CAInitiativePresentation.Label(effective) + " evidence "
                    + "threshold " + Percent(threshold)
                    + "; mixed contents and empty space are not defects",
                    out outcome);

            List<Building_Storage> related = PatternStorageForZone(zone);
            CASpatialFurnishingModule.CAStoragePlan plan;
            string blocker;
            if (!CASpatialFurnishingModule.TrySelectStoragePlan(map, zone,
                planner, related, out plan, out blocker))
                return Finish("native stockpile #" + zone.ID + " justified "
                    + "capacity work at " + Percent(domain.Pressure) + ", but "
                    + blocker, out outcome);
            CABehaviorContext context = SpatialContext(planner, effective,
                TierFor(zone), authoritySatisfied: zone.Map == map,
                knowledgeSatisfied: domain.Capacity > 0
                    && domain.Pressure >= threshold,
                materialSatisfied: plan.def != null,
                authorityBasis: "native stockpile #" + zone.ID
                    + " initiative ceiling",
                knowledgeBasis: domain.OccupiedStacks + "/"
                    + domain.Capacity + " occupied stack slots");
            CABehaviorDecision decision = CABehaviorGate.EvaluateForSelection(
                "logistics.storage_capacity", context);
            if (!decision.SelectionApproved)
                return Finish("native stockpile #" + zone.ID
                    + " planning blocked: " + decision.PrimaryReason,
                    out outcome);
            CAIntentContext intent = CACombatIntent.Authorized(planner,
                CAIntentController.Unknown, "logistics.storage_capacity",
                context.AuthorityOrigin, context.AuthorityBasis,
                "delegated spatial proposal", zone.label);
            return PlacePlan(planner, zone, effective, domain, plan,
                intent, out outcome);
        }

        private bool PlacePlan(Pawn planner, Zone_Stockpile zone,
            CAInitiativeTier effectiveTier, CAStorageDomain domain,
            CASpatialFurnishingModule.CAStoragePlan plan,
            CAIntentContext intent, out string outcome)
        {
            outcome = "no plan";
            if (!(plan.def.blueprintDef?.thingClass != null
                && typeof(Blueprint_Storage).IsAssignableFrom(
                    plan.def.blueprintDef.thingClass)))
                return Finish("selected storage definition has no native "
                    + "Blueprint_Storage policy carrier", out outcome);

            StoragePriority priority = zone.settings.Priority;
            int allowed = zone.settings.filter.AllowedDefCount;
            ThingStyleDef style = Faction.OfPlayer.ideos?.PrimaryIdeo?
                .GetStyleFor(plan.def);
            GenSpawn.WipeExistingThings(plan.cell, plan.rotation,
                plan.def.blueprintDef, map, DestroyMode.Deconstruct);
            Blueprint_Build placed = GenConstruct.PlaceBlueprintForBuild(
                plan.def, plan.cell, map, plan.rotation, Faction.OfPlayer,
                plan.stuff, null, style);
            Blueprint_Storage storageBlueprint = placed as Blueprint_Storage;
            if (storageBlueprint == null)
                return Finish("native blueprint placement returned no storage "
                    + "policy carrier", out outcome);
            storageBlueprint.settings.CopyFrom(zone.settings);
            if (plan.def.PlaceWorkers != null)
                for (int i = 0; i < plan.def.PlaceWorkers.Count; i++)
                    plan.def.PlaceWorkers[i].PostPlace(map, plan.def, plan.cell,
                        plan.rotation);

            pendingZoneId = zone.ID;
            pendingDefName = plan.def.defName;
            pendingStuffName = plan.stuff?.defName;
            pendingCell = plan.cell;
            pendingRotation = plan.rotation;
            pendingPlannerId = planner.thingIDNumber;
            pendingSinceTick = Find.TickManager.TicksGame;
            pendingPolicyPriority = priority;
            pendingPolicyAllowedDefs = allowed;
            pendingPressure = domain.Pressure;
            pendingOccupied = domain.OccupiedStacks;
            pendingCapacity = domain.Capacity;
            pendingEvidence = plan.Receipt();
            pendingBehaviorKey = intent.BehaviorKey;
            pendingEpisodeId = intent.EpisodeId;
            pendingAuthorityOrigin = (int)intent.AuthorityOrigin;
            pendingAuthorityIdentity = intent.AuthorityIdentity;
            lastOutcome = planner.LabelShort + " planned native "
                + plan.def.label + " for stockpile #" + zone.ID + " at "
                + plan.cell + " because " + domain.OccupiedStacks + "/"
                + domain.Capacity + " stack slots (" + Percent(domain.Pressure)
                + ") met its "
                + CAInitiativePresentation.Label(effectiveTier)
                + " evidence threshold; zone policy copied at transition "
                + "[priority " + priority.Label() + ", allowed loaded definitions "
                + allowed + "]; " + pendingEvidence;
            lastEvaluationTick = pendingSinceTick;
            outcome = lastOutcome;
            Log.Message("[CA] " + lastOutcome);
            Messages.Message("Colonist Awareness: " + planner.LabelShort
                + " planned " + plan.def.label + " in " + zone.label
                + " after storage pressure reached "
                + Percent(domain.Pressure) + ". Native construction and the "
                + "copied stockpile policy remain under your control.",
                new TargetInfo(plan.cell, map),
                MessageTypeDefOf.SilentInput, historical: false);
            return true;
        }

        private bool TryPlanForProgram(CASpaceProgram program,
            out string outcome)
        {
            outcome = "no plan";
            lastEvaluationTick = Find.TickManager.TicksGame;
            if (!map.IsPlayerHome
                || !CASpatialFurnishingModule
                    .CanOriginateNewRoomFacility(program))
                return Finish("the target authored Barracks is "
                    + "unavailable", out outcome);
            if (PendingStillExists())
                return Finish("waiting for pending " + pendingDefName + " at "
                    + pendingCell, out outcome);
            if (RoomPendingStillExists())
                return Finish("waiting for pending " + pendingRoomDefName
                    + " at " + pendingRoomCell, out outcome);
            string homeCommitment;
            if (HasHomeConstructionCommitment(out homeCommitment))
                return Finish(homeCommitment, out outcome);
            if (!pendingDefName.NullOrEmpty()) ClearPending();
            if (!pendingRoomDefName.NullOrEmpty()) ClearRoomPending();

            int suppression;
            if (suppressedPrograms.TryGetValue(program.id, out suppression))
            {
                if (suppression != int.MaxValue
                    && suppression <= Find.TickManager.TicksGame)
                    suppressedPrograms.Remove(program.id);
                else
                    return Finish("authored room program #" + program.id
                        + (suppression == int.MaxValue
                            ? " is vetoed until its initiative ceiling changes"
                            : " is paused after an explicit cancellation until tick "
                                + suppression), out outcome);
            }

            CAInitiativeTier effective;
            Pawn planner = ChoosePlanner(program, out effective);
            if (planner == null || effective < CAInitiativeTier.Proactive)
                return Finish("authored room program #" + program.id
                    + " has no awake, threat-unaware construction author whose "
                    + "pawn autonomy and space ceiling combine to Proactive+",
                    out outcome);
            CASpatialFurnishingModule.CARoomFacilityPlan plan;
            string blocker;
            if (!CASpatialFurnishingModule.TrySelectRoomFacilityPlan(map,
                program, planner, effective, out plan, out blocker))
                return Finish("authored "
                    + CASpacePurposeInfo.Label(program.purpose) + " program #"
                    + program.id + " has no authorized facility work: "
                    + blocker, out outcome);
            CABehaviorContext context = SpatialContext(planner, effective,
                TierFor(program), authoritySatisfied:
                    program.author == CASpaceAuthor.Player,
                knowledgeSatisfied: plan.missingLinksBefore > 0,
                materialSatisfied: plan.def != null,
                authorityBasis: "authored space program #" + program.id
                    + " and its initiative ceiling",
                knowledgeBasis: plan.missingLinksBefore
                    + " missing facility relationship(s)");
            CABehaviorDecision decision = CABehaviorGate.EvaluateForSelection(
                "spatial.program_furnishing", context);
            if (!decision.SelectionApproved)
                return Finish("authored room program #" + program.id
                    + " planning blocked: " + decision.PrimaryReason,
                    out outcome);
            CAIntentContext intent = CACombatIntent.Authorized(planner,
                CAIntentController.Unknown, "spatial.program_furnishing",
                context.AuthorityOrigin, context.AuthorityBasis,
                "delegated spatial proposal", program.label);
            return PlaceRoomPlan(planner, program, effective, plan,
                intent, out outcome);
        }

        private bool PlaceRoomPlan(Pawn planner, CASpaceProgram program,
            CAInitiativeTier effectiveTier,
            CASpatialFurnishingModule.CARoomFacilityPlan plan,
            CAIntentContext intent, out string outcome)
        {
            outcome = "no plan";
            GenSpawn.WipeExistingThings(plan.cell, plan.rotation,
                plan.def.blueprintDef, map, DestroyMode.Deconstruct);
            Blueprint_Build placed = GenConstruct.PlaceBlueprintForBuild(
                plan.def, plan.cell, map, plan.rotation, Faction.OfPlayer,
                plan.stuff, null, plan.nativeStyle);
            if (placed == null)
                return Finish("native blueprint placement returned no room "
                    + "facility blueprint", out outcome);
            if (plan.def.PlaceWorkers != null)
                for (int i = 0; i < plan.def.PlaceWorkers.Count; i++)
                    plan.def.PlaceWorkers[i].PostPlace(map, plan.def, plan.cell,
                        plan.rotation);

            pendingRoomProgramId = program.id;
            pendingRoomDefName = plan.def.defName;
            pendingRoomStuffName = plan.stuff?.defName;
            pendingRoomCell = plan.cell;
            pendingRoomRotation = plan.rotation;
            pendingRoomPlannerId = planner.thingIDNumber;
            pendingRoomSinceTick = Find.TickManager.TicksGame;
            pendingRoomFocusThingIds = new List<string>(plan.focusThingIds);
            pendingRoomMissingBefore = plan.missingLinksBefore;
            pendingRoomExpectedLinks = plan.linksServed;
            pendingRoomEvidence = plan.Receipt(program);
            pendingRoomBehaviorKey = intent.BehaviorKey;
            pendingRoomEpisodeId = intent.EpisodeId;
            pendingRoomAuthorityOrigin = (int)intent.AuthorityOrigin;
            pendingRoomAuthorityIdentity = intent.AuthorityIdentity;
            lastOutcome = planner.LabelShort + " planned native "
                + plan.def.label + " for authored "
                + CASpacePurposeInfo.Label(program.purpose) + " program #"
                + program.id + " at " + plan.cell + " under "
                + CAInitiativePresentation.Label(effectiveTier)
                + " effective initiative; " + pendingRoomEvidence;
            lastEvaluationTick = pendingRoomSinceTick;
            outcome = lastOutcome;
            Log.Message("[CA] " + lastOutcome);
            Messages.Message("Colonist Awareness: " + planner.LabelShort
                + " planned " + plan.def.label + " in " + program.label
                + " for " + plan.linksServed + " missing native bed "
                + "relationship" + (plan.linksServed == 1 ? "" : "s")
                + ". Native construction, room ownership, and later player "
                + "edits remain under your control.",
                new TargetInfo(plan.cell, map),
                MessageTypeDefOf.SilentInput, historical: false);
            return true;
        }

        private static CABehaviorContext SpatialContext(Pawn planner,
            CAInitiativeTier effectiveTier,
            CAInitiativeTier authorityCeiling, bool authoritySatisfied,
            bool knowledgeSatisfied, bool materialSatisfied,
            string authorityBasis, string knowledgeBasis)
        {
            Job current = planner?.CurJob;
            return new CABehaviorContext(planner,
                CAActorContext.PlayerSpatialAuthority, effectiveTier,
                CAAuthorityOrigin.PlayerDelegated,
                authoritySatisfied: authoritySatisfied,
                knowledgeSatisfied: knowledgeSatisfied,
                knowledgeFresh: knowledgeSatisfied,
                liveValidated: planner != null && planner.Spawned,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                capabilitySatisfied: planner != null && !planner.Downed,
                materialSatisfied: materialSatisfied,
                currentIntentCompatible: current == null
                    || !current.playerForced,
                directPlayerOwnership: current != null
                    && current.playerForced,
                authorityCeiling: authorityCeiling,
                authorityBasis: authorityBasis,
                knowledgeBasis: knowledgeBasis,
                owner: nameof(CASpatialInitiativeMapComponent));
        }

        private Pawn ChoosePlanner(Zone_Stockpile zone,
            out CAInitiativeTier effectiveTier)
        {
            Pawn best = null;
            effectiveTier = CAInitiativeTier.Standard;
            float bestScore = float.MinValue;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Downed || pawn.Drafted
                    || pawn.InMentalState || !pawn.Awake()
                    || pawn.workSettings == null
                    || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    || !pawn.workSettings.WorkIsActive(
                        WorkTypeDefOf.Construction)
                    || KnowledgeMapComponent.For(map)?.KnowsAnyThreat(pawn)
                        == true) continue;
                CAInitiativeTier effective = EffectiveTier(pawn, zone);
                if (effective < CAInitiativeTier.Proactive) continue;
                DispositionProfile disposition = Disposition.Of(pawn);
                int construction = SkillLevel(pawn,
                    SkillDefOf.Construction);
                int intellectual = SkillLevel(pawn,
                    SkillDefOf.Intellectual);
                float score = disposition.initiative * 0.42f
                    + disposition.discipline * 0.18f
                    + construction / 20f * 0.30f
                    + intellectual / 20f * 0.10f
                    + (pawn.thingIDNumber % 997) * 0.000001f;
                if (best == null || effective > effectiveTier
                    || effective == effectiveTier && score > bestScore)
                {
                    best = pawn;
                    effectiveTier = effective;
                    bestScore = score;
                }
            }
            return best;
        }

        private Pawn ChoosePlanner(CASpaceProgram program,
            out CAInitiativeTier effectiveTier)
        {
            Pawn best = null;
            effectiveTier = CAInitiativeTier.Standard;
            float bestScore = float.MinValue;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Downed || pawn.Drafted
                    || pawn.InMentalState || !pawn.Awake()
                    || pawn.workSettings == null
                    || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    || !pawn.workSettings.WorkIsActive(
                        WorkTypeDefOf.Construction)
                    || KnowledgeMapComponent.For(map)?.KnowsAnyThreat(pawn)
                        == true) continue;
                CAInitiativeTier effective = EffectiveTier(pawn, program);
                if (effective < CAInitiativeTier.Proactive) continue;
                DispositionProfile disposition = Disposition.Of(pawn);
                int construction = SkillLevel(pawn,
                    SkillDefOf.Construction);
                int intellectual = SkillLevel(pawn,
                    SkillDefOf.Intellectual);
                float score = disposition.initiative * 0.42f
                    + disposition.discipline * 0.18f
                    + construction / 20f * 0.30f
                    + intellectual / 20f * 0.10f
                    + (pawn.thingIDNumber % 997) * 0.000001f;
                if (best == null || effective > effectiveTier
                    || effective == effectiveTier && score > bestScore)
                {
                    best = pawn;
                    effectiveTier = effective;
                    bestScore = score;
                }
            }
            return best;
        }

        private static int SkillLevel(Pawn pawn, SkillDef skill)
        {
            return pawn.skills?.GetSkill(skill)?.Level ?? 0;
        }

        private sealed class CAStorageDomain
        {
            internal int ZoneStacks;
            internal int BuiltStorageStacks;
            internal int ZoneCapacity;
            internal int BuiltStorageCapacity;
            internal int OccupiedStacks => ZoneStacks + BuiltStorageStacks;
            internal int Capacity => ZoneCapacity + BuiltStorageCapacity;
            internal float Pressure => Capacity <= 0 ? 0f
                : OccupiedStacks / (float)Capacity;
        }

        private CAStorageDomain DomainFor(Zone_Stockpile zone)
        {
            var domain = new CAStorageDomain();
            if (zone == null) return domain;
            domain.ZoneStacks = zone.slotGroup?.HeldThings.Count() ?? 0;
            domain.ZoneCapacity = zone.cells.Where(cell => cell.InBounds(map))
                .Sum(cell => cell.GetMaxItemsAllowedInCell(map));
            List<Building_Storage> storage = StorageForZone(zone.ID);
            for (int i = 0; i < storage.Count; i++)
            {
                Building_Storage building = storage[i];
                domain.BuiltStorageStacks += building.slotGroup?
                    .HeldThings.Count() ?? 0;
                domain.BuiltStorageCapacity += building.def.building
                    .maxItemsInCell * building.def.Size.Area;
            }
            return domain;
        }

        private List<Building_Storage> StorageForZone(int zoneId)
        {
            var ids = new HashSet<string>(completedStorage.Where(record =>
                    record != null && record.zoneId == zoneId
                    && !record.thingId.NullOrEmpty())
                .Select(record => record.thingId));
            return map.listerBuildings.allBuildingsColonist
                .OfType<Building_Storage>().Where(building => building != null
                    && building.Spawned && ids.Contains(building.ThingID))
                .OrderBy(building => building.thingIDNumber).ToList();
        }

        private List<Building_Storage> PatternStorageForZone(
            Zone_Stockpile zone)
        {
            if (zone == null) return new List<Building_Storage>();
            var cells = new HashSet<IntVec3>(zone.cells);
            var recordedIds = new HashSet<string>(StorageForZone(zone.ID)
                .Select(building => building.ThingID), StringComparer.Ordinal);
            return map.listerBuildings.allBuildingsColonist
                .OfType<Building_Storage>().Where(building => building != null
                    && building.Spawned && building.Faction == Faction.OfPlayer
                    && (recordedIds.Contains(building.ThingID)
                        || building.OccupiedRect().Cells.Any(occupied =>
                            GenAdj.CardinalDirections.Any(direction =>
                                cells.Contains(occupied + direction)))))
                .OrderBy(building => building.thingIDNumber).ToList();
        }

        private bool PendingStillExists()
        {
            return FindPendingStorageConstruction() != null;
        }

        private Thing FindPendingStorageConstruction()
        {
            if (pendingDefName.NullOrEmpty() || !pendingCell.IsValid
                || !pendingCell.InBounds(map)) return null;
            return pendingCell.GetThingList(map).FirstOrDefault(thing =>
            {
                ThingDef entity = thing?.def?.entityDefToBuild as ThingDef;
                return (thing is Blueprint_Build || thing is Frame)
                    && entity?.defName == pendingDefName;
            });
        }

        private bool RoomPendingStillExists()
        {
            Thing pending = FindPendingRoomConstruction();
            if (pending == null) return false;
            CASpaceProgram program = FindOperativeRoomProgram(
                pendingRoomProgramId);
            ThingDef entity = pending.def.entityDefToBuild as ThingDef;
            return ProgramAuthorizesRoomFacility(program, entity,
                pending.Position, pending.Rotation);
        }

        private Thing FindPendingRoomConstruction()
        {
            if (pendingRoomDefName.NullOrEmpty()
                || !pendingRoomCell.IsValid
                || !pendingRoomCell.InBounds(map)) return null;
            return pendingRoomCell.GetThingList(map).FirstOrDefault(thing =>
            {
                ThingDef entity = thing?.def?.entityDefToBuild as ThingDef;
                return (thing is Blueprint_Build || thing is Frame)
                    && entity?.defName == pendingRoomDefName;
            });
        }

        private CASpaceProgram FindOperativeRoomProgram(int programId)
        {
            if (programId <= 0) return null;
            CASpaceProgram program = PlannedUseMapComponent.For(map)
                ?.FindProgram(programId);
            return CASpatialFurnishingModule
                .CanOriginateNewRoomFacility(program)
                ? program : null;
        }

        private bool ProgramAuthorizesRoomFacility(CASpaceProgram program,
            ThingDef facilityDef, IntVec3 cell, Rot4 rotation)
        {
            return CASpatialFurnishingModule
                    .CanOriginateNewRoomFacility(program)
                && ProgramRetainsPendingRoomFacility(program, facilityDef,
                    cell, rotation);
        }

        private bool ProgramRetainsPendingRoomFacility(CASpaceProgram program,
            ThingDef facilityDef, IntVec3 cell, Rot4 rotation)
        {
            if (!CASpatialFurnishingModule.IsOperativeRoomProgram(program)
                || facilityDef == null || !cell.IsValid
                || !cell.InBounds(map)
                || facilityDef.GetCompProperties<CompProperties_Facility>()
                    == null)
                return false;
            var authority = new HashSet<IntVec3>(program.cells.Where(
                candidate => candidate.InBounds(map)));
            CellRect footprint = GenAdj.OccupiedRect(cell, rotation,
                facilityDef.Size);
            if (!footprint.Cells.All(authority.Contains)) return false;
            Room room = cell.GetRoom(map);
            if (room == null || !room.ProperRoom
                || room.PsychologicallyOutdoors
                || (room.Role != RoomRoleDefOf.Bedroom
                    && room.Role != RoomRoleDefOf.Barracks)) return false;
            return map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>().Any(bed => bed != null && bed.Spawned
                    && bed.Faction == Faction.OfPlayer && !bed.Medical
                    && !bed.ForPrisoners
                    && bed.def.building?.bed_humanlike == true
                    && bed.def.building.bed_countsForBedroomOrBarracks
                    && bed.GetRoom() == room
                    && bed.OccupiedRect().Cells.All(authority.Contains)
                    && bed.TryGetComp<CompAffectedByFacilities>()?
                        .CanPotentiallyLinkTo(facilityDef, cell, rotation)
                            == true);
        }

        private bool ProgramRetainsCompletedRoomFacility(
            CASpaceProgram program, Building building)
        {
            if (!CASpatialFurnishingModule.IsOperativeRoomProgram(program)
                || building == null || !building.Spawned
                || building.Faction != Faction.OfPlayer)
                return false;
            var authority = new HashSet<IntVec3>(program.cells.Where(
                candidate => candidate.InBounds(map)));
            if (!building.OccupiedRect().Cells.All(authority.Contains))
                return false;
            Room room = building.GetRoom();
            if (room == null || !room.ProperRoom
                || room.PsychologicallyOutdoors
                || (room.Role != RoomRoleDefOf.Bedroom
                    && room.Role != RoomRoleDefOf.Barracks)) return false;
            CompFacility facility = building.TryGetComp<CompFacility>();
            return facility != null && facility.LinkedBuildings.Any(linked =>
                linked is Building_Bed bed && bed.Spawned
                && bed.Faction == Faction.OfPlayer && !bed.Medical
                && !bed.ForPrisoners
                && bed.def.building?.bed_humanlike == true
                && bed.def.building.bed_countsForBedroomOrBarracks
                && bed.GetRoom() == room
                && bed.OccupiedRect().Cells.All(authority.Contains));
        }

        internal void NotifyExplicitPlanCancel(Thing thing)
        {
            if (thing == null) return;
            ThingDef entity = thing.def.entityDefToBuild as ThingDef;
            if (!pendingDefName.NullOrEmpty() && thing.Position == pendingCell
                && entity?.defName == pendingDefName)
            {
                int zoneId = pendingZoneId;
                suppressedZones[zoneId] = Find.TickManager.TicksGame
                    + GenDate.TicksPerDay;
                lastOutcome = pendingDefName + " at " + pendingCell
                    + " was explicitly canceled; native stockpile #" + zoneId
                    + " is paused for one day";
                lastEvaluationTick = Find.TickManager.TicksGame;
                Messages.Message("Colonist Awareness: shelf planning for "
                    + "stockpile #" + zoneId + " is paused for one day after "
                    + "your cancellation.",
                    new TargetInfo(pendingCell, map),
                    MessageTypeDefOf.SilentInput, historical: false);
                ClearPending();
                return;
            }
            if (pendingRoomDefName.NullOrEmpty()
                || thing.Position != pendingRoomCell
                || entity?.defName != pendingRoomDefName) return;
            int programId = pendingRoomProgramId;
            CASpaceProgram pendingProgram = PlannedUseMapComponent.For(map)
                ?.FindProgram(programId);
            if (!CASpatialFurnishingModule
                .CanOriginateNewRoomFacility(pendingProgram))
            {
                lastOutcome = "retired inert room-facility association for "
                    + pendingRoomDefName + " at " + pendingRoomCell
                    + "; the native player cancellation remains authoritative";
                lastEvaluationTick = Find.TickManager.TicksGame;
                ClearRoomPending();
                return;
            }
            suppressedPrograms[programId] = Find.TickManager.TicksGame
                + GenDate.TicksPerDay;
            lastOutcome = pendingRoomDefName + " at " + pendingRoomCell
                + " was explicitly canceled; authored room program #"
                + programId + " is paused for one day";
            lastEvaluationTick = Find.TickManager.TicksGame;
            Messages.Message("Colonist Awareness: room-facility planning for "
                + "program #" + programId + " is paused for one day after "
                + "your cancellation.",
                new TargetInfo(pendingRoomCell, map),
                MessageTypeDefOf.SilentInput, historical: false);
            ClearRoomPending();
        }

        internal void NotifyExplicitDeconstruct(Thing thing)
        {
            Building_Storage building = thing?.GetInnerIfMinified()
                as Building_Storage;
            if (building != null)
            {
                CASpatialBuiltStorageRecord record = completedStorage
                    .FirstOrDefault(candidate => candidate != null
                        && candidate.thingId == building.ThingID);
                if (record != null)
                {
                    suppressedZones[record.zoneId] = int.MaxValue;
                    completedStorage.Remove(record);
                    lastOutcome = building.def.label + " at "
                        + building.Position + " was marked for deconstruction; "
                        + "native stockpile #" + record.zoneId + " is vetoed "
                        + "until its initiative ceiling changes";
                    lastEvaluationTick = Find.TickManager.TicksGame;
                    Messages.Message("Colonist Awareness: deconstructing this "
                        + "automatic " + building.def.label + " vetoes more "
                        + "shelf planning for stockpile #" + record.zoneId
                        + " until its initiative ceiling changes.",
                        new TargetInfo(thing.Position, map),
                        MessageTypeDefOf.SilentInput, historical: false);
                    return;
                }
            }

            Building roomBuilding = thing?.GetInnerIfMinified() as Building;
            if (roomBuilding == null) return;
            CASpatialBuiltRoomRecord roomRecord = completedRoom
                .FirstOrDefault(candidate => candidate != null
                    && candidate.thingId == roomBuilding.ThingID);
            if (roomRecord == null) return;
            CASpaceProgram roomProgram = PlannedUseMapComponent.For(map)
                ?.FindProgram(roomRecord.programId);
            if (!CASpatialFurnishingModule
                .CanOriginateNewRoomFacility(roomProgram))
            {
                completedRoom.Remove(roomRecord);
                lastOutcome = "retired inert completed-room association for "
                    + roomBuilding.def.label + " at "
                    + roomBuilding.Position + "; native player property and "
                    + "deconstruction authority remain unchanged";
                lastEvaluationTick = Find.TickManager.TicksGame;
                return;
            }
            suppressedPrograms[roomRecord.programId] = int.MaxValue;
            completedRoom.Remove(roomRecord);
            lastOutcome = roomBuilding.def.label + " at "
                + roomBuilding.Position + " was marked for deconstruction; "
                + "authored room program #" + roomRecord.programId
                + " is vetoed until its initiative ceiling changes";
            lastEvaluationTick = Find.TickManager.TicksGame;
            Messages.Message("Colonist Awareness: deconstructing this automatic "
                + roomBuilding.def.label + " vetoes more room-facility "
                + "planning for program #" + roomRecord.programId
                + " until its initiative ceiling changes.",
                new TargetInfo(thing.Position, map),
                MessageTypeDefOf.SilentInput, historical: false);
        }

        internal void NotifyBlueprintTransition(Thing createdThing)
        {
            Building_Storage storage = createdThing as Building_Storage;
            if (storage != null && storage.def.defName == pendingDefName
                && storage.Position == pendingCell)
            {
                RecordCompleted(storage);
                return;
            }
            Building roomBuilding = createdThing as Building;
            if (roomBuilding != null
                && roomBuilding.def.defName == pendingRoomDefName
                && roomBuilding.Position == pendingRoomCell)
                RecordCompletedRoom(roomBuilding);
        }

        internal void NotifyFrameCompleted(IntVec3 cell, string defName)
        {
            if (!pendingDefName.NullOrEmpty() && defName == pendingDefName
                && cell == pendingCell)
            {
                Building_Storage storage = cell.GetThingList(map)
                    .OfType<Building_Storage>().FirstOrDefault(building =>
                        building.def.defName == defName);
                if (storage != null)
                {
                    RecordCompleted(storage);
                    return;
                }
            }
            if (pendingRoomDefName.NullOrEmpty()
                || defName != pendingRoomDefName || cell != pendingRoomCell)
                return;
            Building roomBuilding = cell.GetThingList(map)
                .OfType<Building>().FirstOrDefault(building =>
                    building.def.defName == defName);
            if (roomBuilding != null) RecordCompletedRoom(roomBuilding);
        }

        private void RecordCompleted(Building_Storage storage)
        {
            if (storage == null || pendingZoneId <= 0) return;
            if (!completedStorage.Any(record => record != null
                && record.thingId == storage.ThingID))
                completedStorage.Add(new CASpatialBuiltStorageRecord
                {
                    zoneId = pendingZoneId,
                    thingId = storage.ThingID,
                    defName = storage.def.defName,
                    cell = storage.Position
                });
            CAStorageDomain after = DomainFor(FindZone(pendingZoneId));
            string policy = storage.settings == null
                ? "storage settings unavailable"
                : "priority " + storage.settings.Priority.Label()
                    + ", allowed loaded definitions "
                    + storage.settings.filter.AllowedDefCount;
            lastOutcome = storage.def.label + " completed at "
                + storage.Position + " for native stockpile #" + pendingZoneId
                + "; inherited policy [" + policy + "] from blueprint copy "
                + "[expected priority " + pendingPolicyPriority.Label()
                + ", allowed loaded definitions "
                + pendingPolicyAllowedDefs + "]; storage-domain pressure now "
                + Percent(after.Pressure) + " (" + after.OccupiedStacks + "/"
                + after.Capacity + ")";
            lastEvaluationTick = Find.TickManager.TicksGame;
            Log.Message("[CA] " + lastOutcome);
            ClearPending();
        }

        private void RecordCompletedRoom(Building building)
        {
            if (building == null || pendingRoomProgramId <= 0) return;
            CASpaceProgram program = PlannedUseMapComponent.For(map)
                ?.FindProgram(pendingRoomProgramId);
            if (!CASpatialFurnishingModule
                .CanOriginateNewRoomFacility(program))
            {
                lastOutcome = building.def.label + " completed at "
                    + building.Position + " after its room consumer became "
                    + "inert; the player-owned building remains and no CA "
                    + "completed association was recorded";
                lastEvaluationTick = Find.TickManager.TicksGame;
                Log.Message("[CA] " + lastOutcome);
                ClearRoomPending();
                return;
            }
            CompFacility facility = building.TryGetComp<CompFacility>();
            int actualLinks = facility?.LinkedBuildings.Count(linked =>
                linked is Building_Bed && pendingRoomFocusThingIds.Contains(
                    linked.ThingID)) ?? 0;
            if (!completedRoom.Any(record => record != null
                && record.thingId == building.ThingID))
                completedRoom.Add(new CASpatialBuiltRoomRecord
                {
                    programId = pendingRoomProgramId,
                    thingId = building.ThingID,
                    defName = building.def.defName,
                    cell = building.Position,
                    expectedFocusThingIds = new List<string>(
                        pendingRoomFocusThingIds),
                    actualLinkedBeds = actualLinks
                });
            lastOutcome = building.def.label + " completed at "
                + building.Position + " for authored room program #"
                + pendingRoomProgramId + "; native facility linked "
                + actualLinks + "/" + pendingRoomExpectedLinks
                + " expected bed relationship(s) from "
                + pendingRoomMissingBefore + " missing before construction; "
                + "the completed building, bed ownership, and room remain "
                + "player-owned";
            lastEvaluationTick = Find.TickManager.TicksGame;
            Log.Message("[CA] " + lastOutcome);
            ClearRoomPending();
        }

        internal void NotifyZoneDeleting(Zone zone)
        {
            Zone_Stockpile stockpile = zone as Zone_Stockpile;
            if (stockpile == null || stockpile.Map != map) return;
            int zoneId = stockpile.ID;
            initiatives.RemoveAll(record => record != null
                && record.kind == CASpatialAuthorityKind.NativeStockpile
                && record.authorityId == zoneId);
            suppressedZones.Remove(zoneId);
            completedStorage.RemoveAll(record => record != null
                && record.zoneId == zoneId);
            if (pendingZoneId != zoneId) return;
            Thing pending = pendingCell.IsValid && pendingCell.InBounds(map)
                ? pendingCell.GetThingList(map).FirstOrDefault(thing =>
                    (thing is Blueprint_Build || thing is Frame)
                    && (thing.def.entityDefToBuild as ThingDef)?.defName
                        == pendingDefName)
                : null;
            pending?.Destroy(DestroyMode.Cancel);
            ClearPending();
        }

        private Zone_Stockpile FindZone(int id)
        {
            return map.zoneManager.AllZones.OfType<Zone_Stockpile>()
                .FirstOrDefault(zone => zone.ID == id);
        }

        private void PruneAuthorityAndStorageRecords()
        {
            var zoneIds = new HashSet<int>(map.zoneManager.AllZones
                .OfType<Zone_Stockpile>().Select(zone => zone.ID));
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            Dictionary<int, CASpaceProgram> associationPrograms =
                (programs?.ProgramsForObservation
                    ?? Array.Empty<CASpaceProgram>()).Where(program =>
                        CASpatialFurnishingModule
                            .IsOperativeRoomProgram(program))
                    .GroupBy(program => program.id)
                    .ToDictionary(group => group.Key,
                        group => group.First());
            var programIds = new HashSet<int>(associationPrograms.Keys);
            initiatives.RemoveAll(record => record == null
                || record.kind == CASpatialAuthorityKind.NativeStockpile
                    && !zoneIds.Contains(record.authorityId)
                || record.kind == CASpatialAuthorityKind.SpaceProgram
                    && !programIds.Contains(record.authorityId));
            foreach (int programId in suppressedPrograms.Keys.Where(id =>
                !programIds.Contains(id)).ToList())
                suppressedPrograms.Remove(programId);
            if (pendingRoomProgramId > 0)
            {
                Thing pendingRoom = FindPendingRoomConstruction();
                CASpaceProgram pendingProgram;
                ThingDef pendingEntity = pendingRoom?.def.entityDefToBuild
                    as ThingDef;
                bool retainedProgram = associationPrograms.TryGetValue(
                    pendingRoomProgramId, out pendingProgram);
                bool consumerInert = pendingRoom != null && retainedProgram
                    && !CASpatialFurnishingModule
                        .CanOriginateNewRoomFacility(pendingProgram);
                bool authorized = pendingRoom != null
                    && retainedProgram
                    && ProgramAuthorizesRoomFacility(pendingProgram,
                        pendingEntity, pendingRoom.Position,
                        pendingRoom.Rotation);
                if (consumerInert)
                {
                    lastOutcome = "retired inert CA room-facility tracking for "
                        + pendingRoomDefName + " at " + pendingRoomCell
                        + " after authored Bedroom construction was disabled; "
                        + "the existing player-faction blueprint or frame was "
                        + "left untouched";
                    lastEvaluationTick = Find.TickManager.TicksGame;
                    Log.Message("[CA] " + lastOutcome);
                    ClearRoomPending();
                }
                else if (!authorized)
                {
                    if (pendingRoom != null)
                    {
                        string retired = pendingRoomDefName + " at "
                            + pendingRoomCell;
                        pendingRoom.Destroy(DestroyMode.Cancel);
                        lastOutcome = "canceled orphaned CA room-facility "
                            + retired + " after player-authored program #"
                            + pendingRoomProgramId
                            + " no longer authorized its purpose, footprint, "
                            + "or native bed relationship";
                        lastEvaluationTick = Find.TickManager.TicksGame;
                        Log.Message("[CA] " + lastOutcome);
                    }
                    ClearRoomPending();
                }
            }

            var buildings = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Storage>().Where(building => building != null
                    && building.Spawned).ToDictionary(building =>
                    building.ThingID, StringComparer.Ordinal);
            for (int i = completedStorage.Count - 1; i >= 0; i--)
            {
                CASpatialBuiltStorageRecord record = completedStorage[i];
                Building_Storage building;
                if (record == null || record.thingId.NullOrEmpty()
                    || !zoneIds.Contains(record.zoneId)
                    || !buildings.TryGetValue(record.thingId, out building))
                {
                    completedStorage.RemoveAt(i);
                    continue;
                }
                record.cell = building.Position;
                record.defName = building.def.defName;
            }

            var roomBuildings = map.listerBuildings.allBuildingsColonist
                .Where(building => building != null && building.Spawned)
                .ToDictionary(building => building.ThingID,
                    StringComparer.Ordinal);
            for (int i = completedRoom.Count - 1; i >= 0; i--)
            {
                CASpatialBuiltRoomRecord record = completedRoom[i];
                Building building;
                CASpaceProgram program;
                if (record == null || record.thingId.NullOrEmpty()
                    || !associationPrograms.TryGetValue(record.programId,
                        out program)
                    || !roomBuildings.TryGetValue(record.thingId,
                        out building)
                    || !ProgramRetainsCompletedRoomFacility(program,
                        building))
                {
                    completedRoom.RemoveAt(i);
                    continue;
                }
                record.cell = building.Position;
                record.defName = building.def.defName;
                CompFacility facility = building.TryGetComp<CompFacility>();
                record.actualLinkedBeds = facility?.LinkedBuildings.Count(
                    linked => linked is Building_Bed
                        && record.expectedFocusThingIds.Contains(
                            linked.ThingID)) ?? 0;
            }
        }

        private void ClearPending()
        {
            pendingZoneId = 0;
            pendingDefName = null;
            pendingStuffName = null;
            pendingCell = IntVec3.Invalid;
            pendingRotation = Rot4.Invalid;
            pendingPlannerId = -1;
            pendingSinceTick = -1;
            pendingPolicyAllowedDefs = 0;
            pendingPolicyPriority = StoragePriority.Normal;
            pendingPressure = 0f;
            pendingOccupied = 0;
            pendingCapacity = 0;
            pendingEvidence = null;
            pendingBehaviorKey = null;
            pendingEpisodeId = 0;
            pendingAuthorityOrigin = 0;
            pendingAuthorityIdentity = null;
        }

        private void ClearRoomPending()
        {
            pendingRoomProgramId = 0;
            pendingRoomDefName = null;
            pendingRoomStuffName = null;
            pendingRoomCell = IntVec3.Invalid;
            pendingRoomRotation = Rot4.Invalid;
            pendingRoomPlannerId = -1;
            pendingRoomSinceTick = -1;
            pendingRoomFocusThingIds = new List<string>();
            pendingRoomMissingBefore = 0;
            pendingRoomExpectedLinks = 0;
            pendingRoomEvidence = null;
            pendingRoomBehaviorKey = null;
            pendingRoomEpisodeId = 0;
            pendingRoomAuthorityOrigin = 0;
            pendingRoomAuthorityIdentity = null;
        }

        private bool Finish(string text, out string outcome)
        {
            lastOutcome = text;
            lastEvaluationTick = Find.TickManager?.TicksGame ?? -1;
            outcome = text;
            return false;
        }

        private bool HasHomeConstructionCommitment(out string detail)
        {
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            if (home?.HasPendingPlanForVerification == true)
            {
                detail = "waiting for active home construction objective "
                    + home.PendingPlanForVerification
                    + " before originating spatial-initiative work";
                return true;
            }
            if (CAHomePrerequisiteMapComponent.For(map)?.HasDemand == true)
            {
                detail = "waiting for the retained home construction objective "
                    + "before originating spatial-initiative work";
                return true;
            }
            detail = null;
            return false;
        }

        internal string Census()
        {
            PruneAuthorityAndStorageRecords();
            var builder = new StringBuilder();
            builder.AppendLine("[CA] spatial initiative: Standard, Proactive, "
                + "or Autonomous per-space ceiling; default Standard; "
                + "effective initiative is the lower of pawn initiative and "
                + "the authored space ceiling")
                .AppendLine("  authority: native Zone_Stockpile retains filter, "
                    + "priority, and footprint. CASpaceProgram uses the same "
                    + "saved parameter. An authored Barracks exposes "
                    + "the control only with the operative native bed-facility "
                    + "consumer; an authored Bedroom facility-requirement "
                    + "comparator remains read-only. An exact developer "
                    + "regression may exercise one "
                    + "concrete resident Bed cause; no production/background "
                    + "player-authored Bedroom Bed-cause consumer is enabled.")
                .AppendLine("  operative consumers: one CA-originated native "
                    + "blueprint at a time. Storage Proactive requires 80% "
                    + "domain stack pressure and Autonomous 50%. Room "
                    + "Proactive may serve a coherent shared bed group without "
                    + "forcing one object across the whole bank; Autonomous may "
                    + "also address one exact missing relationship within that "
                    + "facility family. An available bonus is an affordance, not "
                    + "a requirement; CA does not originate a second family "
                    + "without new grounded cause. "
                    + "Functional posture, protected travel lanes, bed-bank "
                    + "intervals, current use, access, research, materials, "
                    + "circulation, service, sleep, and negative space remain "
                    + "separate. Native Beauty is never a placement objective.")
                .Append("  loaded operative furniture-storage definitions: ")
                .Append(CASpatialFurnishingModule
                    .OperativeStorageDefinitions().Count).AppendLine();

            List<Zone_Stockpile> zones = map.zoneManager.AllZones
                .OfType<Zone_Stockpile>().OrderBy(zone => zone.ID).ToList();
            builder.Append("  native stockpiles: ").Append(zones.Count)
                .AppendLine();
            for (int i = 0; i < zones.Count; i++)
            {
                Zone_Stockpile zone = zones[i];
                CAStorageDomain domain = DomainFor(zone);
                CAInitiativeTier ceiling = TierFor(zone);
                CAInitiativeTier effective;
                Pawn planner = ChoosePlanner(zone, out effective);
                int suppression;
                string suppressed = !suppressedZones.TryGetValue(zone.ID,
                    out suppression) ? "none" : suppression == int.MaxValue
                    ? "veto until ceiling changes" : "until tick " + suppression;
                builder.Append("    #").Append(zone.ID).Append(" '")
                    .Append(zone.label).Append("': ceiling ")
                    .Append(CAInitiativePresentation.Label(ceiling))
                    .Append(", best effective ")
                    .Append(planner == null ? "none" :
                        CAInitiativePresentation.Label(effective) + " via "
                        + planner.LabelShort).Append(", pressure ")
                    .Append(Percent(domain.Pressure)).Append(" [zone ")
                    .Append(domain.ZoneStacks).Append("/")
                    .Append(domain.ZoneCapacity).Append(", CA storage ")
                    .Append(domain.BuiltStorageStacks).Append("/")
                    .Append(domain.BuiltStorageCapacity).Append("]")
                    .Append(", native priority ")
                    .Append(zone.settings.Priority.Label())
                    .Append(", allowed loaded definitions ")
                    .Append(zone.settings.filter.AllowedDefCount)
                    .Append(", suppression ").Append(suppressed)
                    .AppendLine();
            }

            IReadOnlyList<CASpaceProgram> observed = PlannedUseMapComponent
                .For(map)?.ProgramsForObservation
                ?? Array.Empty<CASpaceProgram>();
            List<CASpaceProgram> playerPrograms = observed.Where(program =>
                    program != null && program.author == CASpaceAuthor.Player)
                .OrderBy(program => program.id).ToList();
            builder.Append("  authored room programs on shared substrate: ")
                .Append(playerPrograms.Count).AppendLine();
            for (int i = 0; i < playerPrograms.Count; i++)
            {
                CASpaceProgram program = playerPrograms[i];
                bool originates = CASpatialFurnishingModule
                    .CanOriginateNewRoomFacility(program);
                CAInitiativeTier effective = CAInitiativeTier.Standard;
                Pawn planner = originates ? ChoosePlanner(program,
                    out effective) : null;
                int suppression;
                string suppressed = !suppressedPrograms.TryGetValue(program.id,
                    out suppression) ? "none" : suppression == int.MaxValue
                    ? "veto until ceiling changes" : "until tick " + suppression;
                int completed = completedRoom.Count(record => record != null
                    && record.programId == program.id);
                builder.Append("    #").Append(program.id)
                    .Append(" '").Append(program.label)
                    .Append("' (")
                    .Append(CASpacePurposeInfo.Label(program.purpose))
                    .Append("): ceiling ")
                    .Append(CAInitiativePresentation.Label(
                        TierFor(program)))
                    .Append(", consumer ").Append(originates
                        ? "operative; UI visible"
                        : program.purpose == CASpacePurpose.Bedroom
                            ? "read-only requirement comparator; explicit "
                                + "debug regression only; UI hidden"
                            : "not operative; UI hidden")
                    .Append(", best effective ")
                    .Append(planner == null ? "none" :
                        CAInitiativePresentation.Label(effective) + " via "
                        + planner.LabelShort)
                    .Append(", residents ")
                    .Append(program.residents?.Count(pawn => pawn != null) ?? 0)
                    .Append(", completed CA room furnishings ")
                    .Append(completed).Append(", suppression ")
                    .Append(suppressed).AppendLine();
            }

            string pending = pendingDefName.NullOrEmpty() ? "none"
                : pendingDefName + " at " + pendingCell + " facing "
                    + pendingRotation + " for zone #" + pendingZoneId
                    + " since tick " + pendingSinceTick + ", planner "
                    + pendingPlannerId + ", stuff "
                    + (pendingStuffName ?? "not required") + ", pressure "
                    + Percent(pendingPressure) + " [" + pendingOccupied + "/"
                    + pendingCapacity + "], policy ["
                    + pendingPolicyPriority.Label() + ", allowed "
                    + pendingPolicyAllowedDefs + "], evidence ["
                    + pendingEvidence + "]";
            string pendingRoom = pendingRoomDefName.NullOrEmpty() ? "none"
                : pendingRoomDefName + " at " + pendingRoomCell + " facing "
                    + pendingRoomRotation + " for program #"
                    + pendingRoomProgramId + " since tick "
                    + pendingRoomSinceTick + ", planner "
                    + pendingRoomPlannerId + ", stuff "
                    + (pendingRoomStuffName ?? "not required")
                    + ", expected links " + pendingRoomExpectedLinks + "/"
                    + pendingRoomMissingBefore + " missing, focus ["
                    + string.Join(", ", pendingRoomFocusThingIds.ToArray())
                    + "], evidence [" + pendingRoomEvidence + "]";
            builder.Append("  pending storage: ").Append(pending).AppendLine()
                .Append("  pending room: ").Append(pendingRoom).AppendLine()
                .Append("  completed CA storage records: ")
                .Append(completedStorage.Count).AppendLine()
                .Append("  completed CA room records: ")
                .Append(completedRoom.Count).AppendLine();
            for (int i = 0; i < completedRoom.Count; i++)
            {
                CASpatialBuiltRoomRecord record = completedRoom[i];
                builder.Append("    ").Append(record.defName).Append(" ")
                    .Append(record.thingId).Append(" at ").Append(record.cell)
                    .Append(" for program #").Append(record.programId)
                    .Append(", native expected-focus links ")
                    .Append(record.actualLinkedBeds).Append("/")
                    .Append(record.expectedFocusThingIds.Count).AppendLine();
            }
            builder
                .Append("  last evaluation tick ").Append(lastEvaluationTick)
                .Append(": ").Append(lastOutcome);
            return builder.ToString().TrimEnd();
        }

        private static string Percent(float value)
        {
            return (value * 100f).ToString("F1") + "%";
        }
    }

    internal sealed class Command_CASpatialInitiative : Command_Action
    {
        internal Zone_Stockpile zone;

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                CASpatialInitiativeMapComponent component =
                    CASpatialInitiativeMapComponent.For(zone?.Map);
                for (int i = 0;
                    i < CAInitiativePresentation.ActiveTiers.Length; i++)
                {
                    CAInitiativeTier tier =
                        CAInitiativePresentation.ActiveTiers[i];
                    string label = (component?.TierFor(zone) == tier
                        ? "* " : "")
                        + CAInitiativePresentation.Label(tier);
                    yield return new FloatMenuOption(label,
                        delegate { component?.SetTier(zone, tier); });
                }
            }
        }
    }

    [HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.GetGizmos))]
    internal static class Patch_CASpatialInitiativeGizmo
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,
            Zone_Stockpile __instance)
        {
            foreach (Gizmo gizmo in __result) yield return gizmo;
            if (__instance?.Map == null || !__instance.Map.IsPlayerHome)
                yield break;
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(__instance.Map);
            if (component == null) yield break;
            CAInitiativeTier tier = component.TierFor(__instance);
            yield return new Command_CASpatialInitiative
            {
                zone = __instance,
                defaultLabel = "CA initiative: "
                    + CAInitiativePresentation.Label(tier),
                defaultDesc = "The initiative ceiling for this native "
                    + "stockpile. Effective initiative is the lower of this "
                    + "ceiling and the acting colonist's autonomy. Priority, "
                    + "filters, and the remaining floor stockpile stay native.\n\n"
                    + "Standard does not originate shelf work. "
                    + "Proactive may verticalize at 80% stack pressure; "
                    + "Autonomous may do so at 50%. A valid plan must preserve "
                    + "current contents, access, circulation, service and sleep "
                    + "approaches, negative space, research, materials, and "
                    + "native placement.\n\nLeft-click cycles. Right-click picks "
                    + "directly.",
                icon = TexCommand.HoldOpen,
                action = delegate
                {
                    int next = ((int)component.TierFor(__instance) + 1)
                        % CAInitiativePresentation.ActiveTiers.Length;
                    component.SetTier(__instance,
                        CAInitiativePresentation.ActiveTiers[next]);
                }
            };
        }
    }

    [HarmonyPatch(typeof(Designator_Cancel),
        nameof(Designator_Cancel.DesignateThing))]
    internal static class Patch_CASpatialPlanCancel
    {
        private static void Prefix(Thing t)
        {
            CASpatialInitiativeMapComponent.For(t?.Map)
                ?.NotifyExplicitPlanCancel(t);
        }
    }

    [HarmonyPatch(typeof(Designator_Deconstruct),
        nameof(Designator_Deconstruct.DesignateThing))]
    internal static class Patch_CASpatialExplicitDeconstruct
    {
        private static void Prefix(Thing t)
        {
            CASpatialInitiativeMapComponent.For(t?.Map)
                ?.NotifyExplicitDeconstruct(t);
        }
    }

    [HarmonyPatch(typeof(Blueprint),
        nameof(Blueprint.TryReplaceWithSolidThing))]
    internal static class Patch_CASpatialBlueprintTransition
    {
        private static void Postfix(bool __result, Thing createdThing)
        {
            if (__result && createdThing != null)
                CASpatialInitiativeMapComponent.For(createdThing.Map)
                    ?.NotifyBlueprintTransition(createdThing);
        }
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    internal static class Patch_CASpatialFrameCompleted
    {
        private struct CompletionState
        {
            internal Map map;
            internal IntVec3 cell;
            internal string defName;
        }

        private static void Prefix(Frame __instance,
            out CompletionState __state)
        {
            __state = new CompletionState
            {
                map = __instance.Map,
                cell = __instance.Position,
                defName = __instance.BuildDef?.defName
            };
        }

        private static void Postfix(CompletionState __state)
        {
            CASpatialInitiativeMapComponent.For(__state.map)
                ?.NotifyFrameCompleted(__state.cell, __state.defName);
        }
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.Delete),
        new Type[] { typeof(bool) })]
    internal static class Patch_CASpatialZoneDelete
    {
        private static void Prefix(Zone __instance)
        {
            CASpatialInitiativeMapComponent.For(__instance?.Map)
                ?.NotifyZoneDeleting(__instance);
        }
    }

    public static partial class CADebugActions
    {
        internal const string SpatialShelfPreplacementSaveName =
            "CA-a2-92-native-shelf-preplacement-mountain";

        [DebugAction("Colonist Awareness", "Spatial initiative census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpatialInitiativeCensus()
        {
            Log.Message(SpatialInitiativeReceipt());
        }

        internal static string SpatialInitiativeReceipt()
        {
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(Find.CurrentMap);
            return component != null ? component.Census()
                : "[CA] spatial initiative: no current map component";
        }

        internal static string StartSpatialInitiativeMountainReceipt()
        {
            Map map = Find.CurrentMap;
            Zone_Stockpile zone;
            string failure;
            if (!TryResolveMountainShelfFixture(map, out zone, out failure))
                return "[CA] spatial-initiative-mountain-start\nrefused; "
                    + failure;
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(map);
            if (component == null)
                return "[CA] spatial-initiative-mountain-start\nrefused; "
                    + "spatial initiative component is unavailable";
            if (component.HasPendingPlan || component.CompletedStorageCount > 0)
                return "[CA] spatial-initiative-mountain-start\nrefused; "
                    + "the strict preplacement fixture already has CA storage "
                    + "work or a completed CA storage record";

            Pawn author;
            int priorAutonomy;
            bool wokeForFixture;
            if (!TryPrepareDisposableAutonomousBuilder(map, out author,
                out priorAutonomy, out wokeForFixture, out failure))
                return "[CA] spatial-initiative-mountain-start\nrefused; "
                    + failure;

            component.SetTier(zone, CAInitiativeTier.Autonomous);
            string outcome;
            bool placed = component.DebugPlanZoneNow(zone, out outcome);
            AutonomyComponent.SetTier(author,
                (CAInitiativeTier)priorAutonomy);
            if (placed && Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            return "[CA] spatial-initiative-mountain-start\n"
                + "developer fixture only; matched the unique Important "
                + "44-cell, 23-stack, 228-unit native stockpile from the "
                + "developed-mountain corpus; portable behavior depends on "
                + "native authority and evidence, not this identity\n"
                + "disposable author " + author.LabelShort + " autonomy "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + " -> Autonomous for evaluation -> restored "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + (wokeForFixture
                    ? "; native sleep job interrupted for this unsaved proof"
                    : "")
                + "; zone ceiling set Autonomous; blueprint placed "
                + (placed ? "yes" : "no") + "; native stockpile cells now "
                + zone.cells.Count + "; time speed "
                + (Find.TickManager?.CurTimeSpeed.ToString() ?? "unavailable")
                + "\noutcome: " + outcome + "\n" + component.Census();
        }

        internal static string StartSpatialInitiativeAbovegroundReceipt()
        {
            Map map = Find.CurrentMap;
            Zone_Stockpile zone;
            string failure;
            if (!TryResolveAbovegroundShelfFixture(map, out zone, out failure))
                return "[CA] spatial-initiative-aboveground-start\nrefused; "
                    + failure;
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(map);
            if (component == null)
                return "[CA] spatial-initiative-aboveground-start\nrefused; "
                    + "spatial initiative component is unavailable";
            Pawn author;
            int priorAutonomy;
            bool wokeForFixture;
            if (!TryPrepareDisposableAutonomousBuilder(map, out author,
                out priorAutonomy, out wokeForFixture, out failure))
                return "[CA] spatial-initiative-aboveground-start\nrefused; "
                    + failure;
            component.SetTier(zone, CAInitiativeTier.Autonomous);
            string outcome;
            bool placed = component.DebugPlanZoneNow(zone, out outcome);
            AutonomyComponent.SetTier(author,
                (CAInitiativeTier)priorAutonomy);
            return "[CA] spatial-initiative-aboveground-start\n"
                + "developer fixture only; matched the unique 42-cell, "
                + "five-stack aboveground stockpile; portable behavior "
                + "depends on native authority and evidence, not this identity\n"
                + "disposable author " + author.LabelShort + " autonomy "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + " -> Autonomous for evaluation -> restored "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + (wokeForFixture
                    ? "; native sleep job interrupted for this unsaved proof"
                    : "")
                + "; zone ceiling set Autonomous; blueprint placed "
                + (placed ? "yes" : "no")
                + "; expected no because sparse pressure is below 50%\n"
                + "outcome: " + outcome + "\n" + component.Census();
        }

        internal static string SaveSpatialShelfPreplacementReceipt()
        {
            Map map = Find.CurrentMap;
            Zone_Stockpile zone;
            string failure;
            if (!TryResolveMountainShelfFixture(map, out zone, out failure))
                return "[CA] spatial-initiative-save-preplacement\nrefused; "
                    + failure;
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(map);
            if (component == null || component.HasPendingPlan
                || component.CompletedStorageCount != 0)
                return "[CA] spatial-initiative-save-preplacement\nrefused; "
                    + "the exact preplacement state is not clean";
            string path = GenFilePaths.FilePathForSavedGame(
                SpatialShelfPreplacementSaveName);
            if (File.Exists(path))
                return "[CA] spatial-initiative-save-preplacement\nrefused; "
                    + "the specific checkpoint already exists and will not be "
                    + "overwritten: " + path;
            GameDataSaveLoader.SaveGame(SpatialShelfPreplacementSaveName);
            return "[CA] spatial-initiative-save-preplacement\nqueued native "
                + "save of the exact clean 44-cell preplacement state to new "
                + "checkpoint " + path + "; no baseline was overwritten";
        }

        internal static string SaveSpatialInitiativeWorkingReceipt()
        {
            Map map = Find.CurrentMap;
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(map);
            if (component == null)
                return "[CA] spatial-initiative-save-working\nrefused; no "
                    + "current spatial initiative component";
            if (component.HasPendingPlan || component.CompletedStorageCount < 1)
                return "[CA] spatial-initiative-save-working\nrefused; native "
                    + "shelf construction has not completed cleanly";
            string path = GenFilePaths.FilePathForSavedGame(
                SpatialDevelopedMountainSaveName);
            GameDataSaveLoader.SaveGame(SpatialDevelopedMountainSaveName);
            return "[CA] spatial-initiative-save-working\nqueued native save "
                + "of the completed shelf milestone to the continuing derived "
                + "working save " + path + "; no baseline or checkpoint was "
                + "overwritten\n" + component.Census();
        }

        internal static string SaveSpatialRoomPreplacementReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            string failure;
            if (!TryResolveAuthoredBarracksFixture(map, out program,
                out beds, out failure))
                return "[CA] spatial-room-save-preplacement\nrefused; "
                    + failure;
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(map);
            if (component == null || component.HasPendingPlan
                || component.CompletedRoomCount != 0)
                return "[CA] spatial-room-save-preplacement\nrefused; the "
                    + "authored Barracks preplacement state is not clean";
            int existingFacilities = map.listerBuildings
                .allBuildingsColonist.Count(building => building != null
                    && building.Spawned && building.TryGetComp<CompFacility>()
                        != null && building.GetRoom() == beds[0].GetRoom()
                    && program.cells.Contains(building.Position));
            if (existingFacilities != 0)
                return "[CA] spatial-room-save-preplacement\nrefused; expected "
                    + "zero completed native facilities in the authored "
                    + "Barracks, observed " + existingFacilities;
            string path = GenFilePaths.FilePathForSavedGame(
                SpatialRoomPreplacementSaveName);
            if (File.Exists(path))
                return "[CA] spatial-room-save-preplacement\nrefused; the "
                    + "specific checkpoint already exists and will not be "
                    + "overwritten: " + path;
            GameDataSaveLoader.SaveGame(SpatialRoomPreplacementSaveName);
            return "[CA] spatial-room-save-preplacement\nqueued native save "
                + "of the clean player-authored 60-cell Barracks with seven "
                + "resident beds and no linked facility to new checkpoint "
                + path + "; no baseline or autosave was overwritten";
        }

        internal static string StartSpatialRoomBarracksReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            string failure;
            if (!TryResolveAuthoredBarracksFixture(map, out program,
                out beds, out failure))
                return "[CA] spatial-room-barracks-start\nrefused; " + failure;
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(map);
            if (component == null)
                return "[CA] spatial-room-barracks-start\nrefused; spatial "
                    + "initiative component is unavailable";
            if (component.HasPendingPlan || component.CompletedRoomCount > 0)
                return "[CA] spatial-room-barracks-start\nrefused; the strict "
                    + "preplacement fixture already has CA room-facility work "
                    + "or a completed CA room record";

            Pawn author;
            int priorAutonomy;
            bool wokeForFixture;
            if (!TryPrepareDisposableAutonomousBuilder(map, out author,
                out priorAutonomy, out wokeForFixture, out failure))
                return "[CA] spatial-room-barracks-start\nrefused; " + failure;
            component.SetTier(program, CAInitiativeTier.Proactive);
            string outcome;
            bool placed = component.DebugPlanProgramNow(program, out outcome);
            AutonomyComponent.SetTier(author,
                (CAInitiativeTier)priorAutonomy);
            if (placed && Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            return "[CA] spatial-room-barracks-start\n"
                + "developer fixture only; matched the existing player-authored "
                + "Operator-marked Barracks A by its 60 exact cells, seven "
                + "assigned residents, seven completed native beds, and absent "
                + "facility links. Portable behavior depends on authored room "
                + "authority and loaded native definitions, never this name or "
                + "its coordinates\n"
                + "disposable author " + author.LabelShort + " autonomy "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + " -> Autonomous for evaluation -> restored "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + (wokeForFixture
                    ? "; native sleep job interrupted for this derived proof"
                    : "")
                + "; room ceiling set Proactive; blueprint placed "
                + (placed ? "yes" : "no") + "; native beds " + beds.Count
                + "; time speed "
                + (Find.TickManager?.CurTimeSpeed.ToString() ?? "unavailable")
                + "\noutcome: " + outcome + "\n" + component.Census();
        }

        internal static string SaveSpatialRoomWorkingReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            string failure;
            if (!TryResolveAuthoredBarracksFixture(map, out program,
                out beds, out failure))
                return "[CA] spatial-room-save-working\nrefused; " + failure;
            CASpatialInitiativeMapComponent component =
                CASpatialInitiativeMapComponent.For(map);
            if (component == null || component.HasPendingPlan
                || component.CompletedRoomCount < 1)
                return "[CA] spatial-room-save-working\nrefused; native room "
                    + "facility construction has not completed cleanly";
            string path = GenFilePaths.FilePathForSavedGame(
                SpatialRoomWorkingSaveName);
            GameDataSaveLoader.SaveGame(SpatialRoomWorkingSaveName);
            return "[CA] spatial-room-save-working\nqueued native save of "
                + "the completed bed-facility milestone to the continuing "
                + "derived working save " + path + "; no baseline, autosave, "
                + "or retained checkpoint was overwritten\n"
                + component.Census();
        }

        internal static string RestoreDollyAndStartEighthBedReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            string failure;
            if (!TryResolveAuthoredBarracksProgram(map, out program,
                out failure))
                return "[CA] spatial-room-dolly-restore-start\nrefused; "
                    + failure;

            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            if (programs == null || home == null || spatial == null)
                return "[CA] spatial-room-dolly-restore-start\nrefused; "
                    + "one or more production planning components are unavailable";
            if (spatial.HasPendingPlan || spatial.CompletedRoomCount != 0)
                return "[CA] spatial-room-dolly-restore-start\nrefused; the "
                    + "authored Barracks already has room-facility work";

            List<Building_Bed> beds = BedsInProgram(map, program);
            Pawn dolly = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(CAPrepareCarefullyFixtureBridge
                    .IsExactDollyIdentity);
            string restoreOutcome;
            if (dolly == null)
            {
                int residents = program.residents?
                    .Count(pawn => pawn != null) ?? 0;
                if (program.maxOccupants != 7 || residents != 7
                    || beds.Count != 7)
                    return "[CA] spatial-room-dolly-restore-start\nrefused; "
                        + "exact restoration starts from seven residents, a "
                        + "seven-person occupancy limit, and seven completed "
                        + "humanlike beds; observed residents " + residents
                        + ", limit " + program.maxOccupants + ", beds "
                        + beds.Count;
                if (!CAPrepareCarefullyFixtureBridge.TryRestoreDolly(map,
                    out dolly, out restoreOutcome))
                    return "[CA] spatial-room-dolly-restore-start\nrefused; "
                        + restoreOutcome;
            }
            else
            {
                restoreOutcome = "exact Dolly was already present; resumed "
                    + "the derived fixture without spawning a duplicate";
            }

            Pawn luc = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(CAPrepareCarefullyFixtureBridge
                    .IsExactLucIdentity);
            if (luc == null || dolly.GetFirstSpouse() != luc
                || luc.GetFirstSpouse() != dolly)
                return "[CA] spatial-room-dolly-restore-start\nrefused; exact "
                    + "Dolly and Luc do not have the reciprocal native spouse "
                    + "relation required by this fixture";

            int currentResidents = program.residents?
                .Count(pawn => pawn != null) ?? 0;
            if (!program.residents.Contains(dolly))
            {
                if (currentResidents != 7)
                    return "[CA] spatial-room-dolly-restore-start\nrefused; "
                        + "Dolly is absent from a roster that no longer has "
                        + "exactly seven existing residents";
                CASpaceProgramDraft draft = CASpaceProgramDraft.From(program,
                    8);
                draft.maxOccupants = 8;
                programs.UpdateProgram(program, draft);
                if (!programs.SetResident(program, dolly, true, out failure))
                    return "[CA] spatial-room-dolly-restore-start\nrefused; "
                        + "Dolly was restored but could not join the authored "
                        + "Barracks roster: " + failure;
            }
            else if (program.maxOccupants != 8)
            {
                if (currentResidents != 8)
                    return "[CA] spatial-room-dolly-restore-start\nrefused; "
                        + "the resumed Dolly roster is not the exact eight-person fixture";
                CASpaceProgramDraft draft = CASpaceProgramDraft.From(program,
                    8);
                draft.maxOccupants = 8;
                programs.UpdateProgram(program, draft);
            }

            currentResidents = program.residents?
                .Count(pawn => pawn != null) ?? 0;
            beds = BedsInProgram(map, program);
            if (currentResidents != 8 || program.maxOccupants != 8)
                return "[CA] spatial-room-dolly-restore-start\nrefused; the "
                    + "post-restore roster is not exactly eight residents at "
                    + "an eight-person limit";
            if (beds.Count == 8)
                return "[CA] spatial-room-dolly-restore-start\n"
                    + restoreOutcome + "; eighth native bed is already "
                    + "complete\n" + DollyBarracksCensusReceipt();
            if (beds.Count != 7)
                return "[CA] spatial-room-dolly-restore-start\nrefused; "
                    + "expected seven completed native beds before the "
                    + "eighth-bed objective, observed " + beds.Count;

            if (home.HasPendingPlanForVerification)
            {
                string activeState;
                string activeDetail;
                if (home.TryVerifyContextualBedPlan(program, out activeState,
                    out activeDetail))
                    return "[CA] spatial-room-dolly-restore-start\n"
                        + restoreOutcome + "; resumed " + activeState + ": "
                        + activeDetail + "\n" + home.Census();
                if (Find.TickManager != null)
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
                return "[CA] spatial-room-dolly-restore-start\n"
                    + restoreOutcome + "; exact Dolly and Luc spouse relation "
                    + "confirmed; authored Barracks occupancy 7 -> 8; Dolly "
                    + "added through the production resident API\n"
                    + "eighth-bed planning deferred while preserving the "
                    + "pre-existing automatic home objective: "
                    + home.PendingPlanForVerification + "; game speed "
                    + (Find.TickManager?.CurTimeSpeed.ToString()
                        ?? "unavailable") + " so native work can resolve";
            }

            Pawn author;
            int priorAutonomy;
            bool wokeForFixture;
            if (!TryPrepareDisposableAutonomousBuilder(map, out author,
                out priorAutonomy, out wokeForFixture, out failure))
                return "[CA] spatial-room-dolly-restore-start\nrefused; "
                    + failure;
            bool placed;
            string outcome;
            try
            {
                placed = home.DebugPlanNow(out outcome);
            }
            finally
            {
                AutonomyComponent.SetTier(author,
                    (CAInitiativeTier)priorAutonomy);
            }
            string verificationState;
            string verificationDetail;
            bool contextual = home.TryVerifyContextualBedPlan(program,
                out verificationState, out verificationDetail);
            if ((placed || contextual) && Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            return "[CA] spatial-room-dolly-restore-start\n"
                + restoreOutcome + "; exact Dolly and Luc spouse relation "
                + "confirmed; authored Barracks occupancy 7 -> 8; Dolly "
                + "added through the production resident API\n"
                + "disposable author " + author.LabelShort + " autonomy "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + " -> Autonomous for evaluation -> restored "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + (wokeForFixture
                    ? "; native sleep job interrupted for this derived proof"
                    : "")
                + "; eighth-bed plan placed " + (placed ? "yes" : "no")
                + "; contextual verification "
                + (contextual ? verificationState : "rejected") + ": "
                + verificationDetail + "\noutcome: " + outcome + "\n"
                + home.Census();
        }

        internal static string DollyBarracksCensusReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            string failure;
            if (!TryResolveAuthoredBarracksProgram(map, out program,
                out failure))
                return "[CA] spatial-room-dolly-bed-census\nrefused; "
                    + failure;
            List<Building_Bed> beds = BedsInProgram(map, program);
            int plannedBeds = PlannedHumanlikeBedsInProgram(map, program);
            Pawn dolly = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(CAPrepareCarefullyFixtureBridge
                    .IsExactDollyIdentity);
            Pawn luc = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(CAPrepareCarefullyFixtureBridge
                    .IsExactLucIdentity);
            bool reciprocalSpouses = dolly != null && luc != null
                && dolly.GetFirstSpouse() == luc && luc.GetFirstSpouse() == dolly;
            List<Pawn> residents = (program.residents ?? new List<Pawn>())
                .Where(pawn => pawn != null)
                .OrderBy(pawn => pawn.thingIDNumber).ToList();
            int residentBeds = residents.Count(pawn => pawn.ownership?.OwnedBed
                != null && beds.Contains(pawn.ownership.OwnedBed));
            string roster = residents.Count == 0 ? "none" : string.Join(
                ", ", residents.Select(pawn => pawn.LabelShort + "->"
                    + (pawn.ownership?.OwnedBed == null ? "unowned"
                        : pawn.ownership.OwnedBed.ThingID)).ToArray());
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            return "[CA] spatial-room-dolly-bed-census\nprogram #"
                + program.id + " " + program.label + "; residents "
                + residents.Count + "/" + program.maxOccupants
                + "; completed native humanlike beds " + beds.Count
                + "; in-progress native humanlike beds " + plannedBeds
                + "; residents owning program beds " + residentBeds
                + "; exact Dolly present " + (dolly != null)
                + "; reciprocal Dolly-Luc spouses " + reciprocalSpouses
                + "; Dolly rostered "
                + (dolly != null && residents.Contains(dolly))
                + "; home plan "
                + (home?.PendingPlanForVerification ?? "unavailable")
                + "; room facility pending "
                + (spatial?.HasPendingPlan == true)
                + "; completed room facility records "
                + (spatial?.CompletedRoomCount ?? -1)
                + "\nroster and native bed ownership: " + roster;
        }

        internal static string SaveDollyBarracksPreplacementReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            string failure;
            if (!TryResolveAuthoredBarracksFixture(map, 8, 8, out program,
                out beds, out failure))
                return "[CA] spatial-room-dolly-save-preplacement\nrefused; "
                    + failure;
            if (!TryVerifyDollyBarracksOwnership(map, program, beds,
                out failure))
                return "[CA] spatial-room-dolly-save-preplacement\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            if (home == null || home.HasPendingPlanForVerification
                || spatial == null || spatial.HasPendingPlan
                || spatial.CompletedRoomCount != 0)
                return "[CA] spatial-room-dolly-save-preplacement\nrefused; "
                    + "the exact eight-bed preplacement state is not clean";
            if (FacilitiesInProgramRoom(map, program, beds).Count != 0)
                return "[CA] spatial-room-dolly-save-preplacement\nrefused; "
                    + "a linked native room facility already exists";
            string path = GenFilePaths.FilePathForSavedGame(
                SpatialRoomDollyPreplacementSaveName);
            if (File.Exists(path))
                return "[CA] spatial-room-dolly-save-preplacement\nrefused; "
                    + "the specific checkpoint already exists and will not be "
                    + "overwritten: " + path;
            GameDataSaveLoader.SaveGame(SpatialRoomDollyPreplacementSaveName);
            return "[CA] spatial-room-dolly-save-preplacement\nqueued "
                + "native save of exact Dolly restored, reciprocal spouse "
                + "relation intact, eight authored residents owning eight "
                + "completed native Barracks beds, and no linked room "
                + "facility to new checkpoint " + path
                + "; no baseline, autosave, or retained checkpoint was overwritten";
        }

        internal static string StartDollyBarracksFacilitiesReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            string failure;
            if (!TryResolveAuthoredBarracksFixture(map, 8, 8, out program,
                out beds, out failure))
                return "[CA] spatial-room-dolly-facilities-start\nrefused; "
                    + failure;
            if (!TryVerifyDollyBarracksOwnership(map, program, beds,
                out failure))
                return "[CA] spatial-room-dolly-facilities-start\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            if (home == null || home.HasPendingPlanForVerification)
                return "[CA] spatial-room-dolly-facilities-start\nrefused; "
                    + "the eighth-bed home objective has not completed cleanly";
            if (spatial == null || spatial.HasPendingPlan
                || spatial.CompletedRoomCount != 0
                || FacilitiesInProgramRoom(map, program, beds).Count != 0)
                return "[CA] spatial-room-dolly-facilities-start\nrefused; "
                    + "the exact room-facility preplacement state is not clean";

            Pawn author;
            int priorAutonomy;
            bool wokeForFixture;
            if (!TryPrepareDisposableAutonomousBuilder(map, out author,
                out priorAutonomy, out wokeForFixture, out failure))
                return "[CA] spatial-room-dolly-facilities-start\nrefused; "
                    + failure;
            bool homePlanningWasEnabled = AwarenessMod.Settings?
                .autonomousHomePlanning == true;
            if (homePlanningWasEnabled)
            {
                AwarenessMod.Settings.autonomousHomePlanning = false;
                int resetGeneration = AwarenessMod.Settings
                    .autonomousHomePlanningResetGeneration;
                home.NotifyPlanningSettingChanged(false, resetGeneration);
                CAStorageProgramMapComponent.For(map)
                    ?.NotifyPlanningSettingChanged(false, resetGeneration);
            }
            spatial.SetTier(program, CAInitiativeTier.Proactive);
            bool placed;
            string outcome;
            try
            {
                placed = spatial.DebugPlanProgramNow(program, out outcome);
            }
            finally
            {
                AutonomyComponent.SetTier(author,
                    (CAInitiativeTier)priorAutonomy);
            }
            if (placed && Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            return "[CA] spatial-room-dolly-facilities-start\nmatched "
                + "the existing 60-cell player-authored Barracks through its "
                + "eight exact residents and eight completed owned beds; "
                + "portable facility behavior remains program- and evidence-driven\n"
                + "disposable author " + author.LabelShort + " autonomy "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + " -> Autonomous for evaluation -> restored "
                + CAInitiativePresentation.Label((CAInitiativeTier)priorAutonomy)
                + (wokeForFixture
                    ? "; native sleep job interrupted for this derived proof"
                    : "")
                + "; fixture isolation left autonomous home planning "
                + (homePlanningWasEnabled ? "enabled -> disabled" : "disabled")
                + "; room ceiling set Proactive; blueprint placed "
                + (placed ? "yes" : "no") + "\noutcome: " + outcome
                + "\n" + spatial.Census();
        }

        internal static string SaveDollyBarracksWorkingReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            string failure;
            if (!TryResolveAuthoredBarracksFixture(map, 8, 8, out program,
                out beds, out failure))
                return "[CA] spatial-room-dolly-save-working\nrefused; "
                    + failure;
            if (!TryVerifyDollyBarracksOwnership(map, program, beds,
                out failure))
                return "[CA] spatial-room-dolly-save-working\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            List<Building> facilities = FacilitiesInProgramRoom(map, program,
                beds);
            List<ThingDef> facilityFamilies = facilities.Select(facility =>
                    facility.def).Distinct().ToList();
            var facilitySet = new HashSet<Thing>(facilities.Cast<Thing>());
            int linkedBeds = beds.Count(bed => bed
                .TryGetComp<CompAffectedByFacilities>()?
                .LinkedFacilitiesListForReading.Any(facilitySet.Contains)
                    == true);
            int operativeFacilities = facilities.Count(facility => facility
                .TryGetComp<CompFacility>()?.LinkedBuildings.Count(linked =>
                    linked != null && beds.Contains(linked as Building_Bed)) > 0);
            if (home == null || home.HasPendingPlanForVerification
                || spatial == null || spatial.HasPendingPlan
                || facilities.Count < 1
                || spatial.CompletedRoomCount != facilities.Count
                || facilityFamilies.Count != 1
                || operativeFacilities != facilities.Count
                || linkedBeds != beds.Count)
                return "[CA] spatial-room-dolly-save-working\nrefused; "
                    + "the bounded one-family native room facility milestone "
                    + "has not completed cleanly; observed linked facilities "
                    + facilities.Count + ", completed records "
                    + (spatial?.CompletedRoomCount ?? -1) + ", families "
                    + facilityFamilies.Count + ", operative facilities "
                    + operativeFacilities + ", covered resident beds "
                    + linkedBeds + "/" + beds.Count;
            string path = GenFilePaths.FilePathForSavedGame(
                SpatialRoomDollyWorkingSaveName);
            GameDataSaveLoader.SaveGame(SpatialRoomDollyWorkingSaveName);
            return "[CA] spatial-room-dolly-save-working\nqueued native "
                + "save of exact Dolly, eight owned Barracks beds, and one "
                + "completed function-first room facility family across "
                + facilities.Count + " native building(s), covering all "
                + linkedBeds + " beds, to the continuing "
                + "derived working save " + path
                + "; no baseline, autosave, or retained checkpoint was overwritten\n"
                + spatial.Census();
        }

        internal static string DollyPlayerIntentAuthorityRegressionReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            AutonomousHomeMapComponent home;
            CASpatialInitiativeMapComponent spatial;
            string failure;
            if (!TryResolveCleanDollyAuthorityPreplacement(map, out program,
                out beds, out home, out spatial, out failure))
                return "[CA] spatial-room-authority-player-intent-regression\n"
                    + "refused; " + failure;

            Pawn author;
            int priorAutonomy;
            bool wokeForFixture;
            if (!TryPrepareDisposableAutonomousBuilder(map, out author,
                out priorAutonomy, out wokeForFixture, out failure))
                return "[CA] spatial-room-authority-player-intent-regression\n"
                    + "refused; " + failure;

            TimeSpeed priorSpeed = Find.TickManager.CurTimeSpeed;
            CAInitiativeTier priorCeiling = spatial.TierFor(program);
            Blueprint_Build blueprint = null;
            Thing construction = null;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                spatial.SetTier(program, CAInitiativeTier.Proactive);
                CASpatialFurnishingModule.CARoomFacilityPlan plan;
                string blocker;
                if (!CASpatialFurnishingModule.TrySelectRoomFacilityPlan(map,
                    program, author, CAInitiativeTier.Proactive,
                    out plan, out blocker))
                    return "[CA] spatial-room-authority-player-intent-regression\n"
                        + "refused; no clean native facility candidate: "
                        + blocker;

                GenSpawn.WipeExistingThings(plan.cell, plan.rotation,
                    plan.def.blueprintDef, map, DestroyMode.Deconstruct);
                blueprint = GenConstruct.PlaceBlueprintForBuild(plan.def,
                    plan.cell, map, plan.rotation, Faction.OfPlayer,
                    plan.stuff, null, plan.nativeStyle);
                construction = blueprint;
                if (blueprint == null)
                    return "[CA] spatial-room-authority-player-intent-regression\n"
                        + "failed; native player-faction blueprint placement "
                        + "returned null";
                if (plan.def.PlaceWorkers != null)
                    for (int i = 0; i < plan.def.PlaceWorkers.Count; i++)
                        plan.def.PlaceWorkers[i].PostPlace(map, plan.def,
                            plan.cell, plan.rotation);

                bool caPlacedBesideBlueprint = spatial.DebugPlanProgramNow(
                    program, out string blueprintOutcome);
                bool blueprintBlocked = !caPlacedBesideBlueprint
                    && blueprintOutcome.Contains(
                        "player-authored facility families already under "
                        + "construction");

                bool transitioned = blueprint.TryReplaceWithSolidThing(author,
                    out Thing createdThing, out bool jobEnded);
                construction = createdThing ?? construction;
                bool frameCreated = transitioned && !jobEnded
                    && createdThing is Frame && createdThing.Spawned
                    && (createdThing.def.entityDefToBuild as ThingDef)
                        == plan.def;
                string frameOutcome =
                    "native blueprint-to-frame transition failed";
                bool caPlacedBesideFrame = false;
                if (frameCreated)
                    caPlacedBesideFrame = spatial.DebugPlanProgramNow(program,
                        out frameOutcome);
                bool frameBlocked = frameCreated && !caPlacedBesideFrame
                    && frameOutcome.Contains(
                        "player-authored facility families already under "
                        + "construction");

                if (construction != null && !construction.Destroyed)
                    construction.Destroy(DestroyMode.Cancel);
                construction = null;
                blueprint = null;
                bool cleanup = FacilityConstructionInProgram(map, program,
                    beds).Count == 0 && !spatial.HasPendingPlan
                    && spatial.CompletedRoomCount == 0;
                bool pass = blueprintBlocked && frameBlocked && cleanup;
                return "[CA] spatial-room-authority-player-intent-regression\n"
                    + (pass ? "pass" : "fail")
                    + "; exact eight-bed authored Barracks; candidate "
                    + plan.def.defName + " at " + plan.cell + " facing "
                    + plan.rotation + "\nblueprint blocker: "
                    + blueprintBlocked + "; CA placement attempted "
                    + caPlacedBesideBlueprint + "; outcome "
                    + blueprintOutcome + "\nframe transition: "
                    + frameCreated + "; CA placement attempted "
                    + caPlacedBesideFrame + "; blocker " + frameBlocked
                    + "; outcome " + frameOutcome + "\ncleanup: " + cleanup
                    + "; fixture construction canceled, authored program and "
                    + "completed buildings unchanged; no save written"
                    + (wokeForFixture
                        ? "; disposable author was awakened for this proof"
                        : "");
            }
            finally
            {
                if (construction != null && !construction.Destroyed)
                    construction.Destroy(DestroyMode.Cancel);
                if (blueprint != null && !blueprint.Destroyed)
                    blueprint.Destroy(DestroyMode.Cancel);
                spatial.SetTier(program, priorCeiling);
                AutonomyComponent.SetTier(author,
                    (CAInitiativeTier)priorAutonomy);
                Find.TickManager.CurTimeSpeed = priorSpeed;
            }
        }

        internal static string DollyPendingAuthorityRegressionReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            AutonomousHomeMapComponent home;
            CASpatialInitiativeMapComponent spatial;
            string failure;
            if (!TryResolveCleanDollyAuthorityPreplacement(map, out program,
                out beds, out home, out spatial, out failure))
                return "[CA] spatial-room-authority-pending-regression\n"
                    + "refused; " + failure;

            Pawn author;
            int priorAutonomy;
            bool wokeForFixture;
            if (!TryPrepareDisposableAutonomousBuilder(map, out author,
                out priorAutonomy, out wokeForFixture, out failure))
                return "[CA] spatial-room-authority-pending-regression\n"
                    + "refused; " + failure;

            TimeSpeed priorSpeed = Find.TickManager.CurTimeSpeed;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                spatial.SetTier(program, CAInitiativeTier.Proactive);
                bool placed = spatial.DebugPlanProgramNow(program,
                    out string placementOutcome);
                List<Thing> pending = FacilityConstructionInProgram(map,
                    program, beds);
                if (!placed || pending.Count != 1 || !spatial.HasPendingPlan)
                    return "[CA] spatial-room-authority-pending-regression\n"
                        + "failed; expected one tracked CA facility blueprint; "
                        + "placed " + placed + ", construction objects "
                        + pending.Count + ", tracked pending "
                        + spatial.HasPendingPlan + "; outcome "
                        + placementOutcome;

                Thing tracked = pending[0];
                PlannedUseMapComponent programs =
                    PlannedUseMapComponent.For(map);
                programs.UpdateProgram(program, new CASpaceProgramDraft
                {
                    label = program.label,
                    purpose = CASpacePurpose.Kitchen,
                    style = program.style,
                    maxOccupants = program.maxOccupants,
                    requiresSleep = false
                });
                string census = spatial.Census();
                bool canceled = tracked.Destroyed
                    && !spatial.HasPendingPlan
                    && spatial.CompletedRoomCount == 0;
                bool authorityChanged = program.purpose
                    == CASpacePurpose.Kitchen && !program.RequiresSleep
                    && (program.residents?.Count ?? 0) == 0;
                bool bedsPreserved = beds.All(bed => bed.Spawned
                    && !bed.Destroyed);
                bool pass = canceled && authorityChanged && bedsPreserved;
                return "[CA] spatial-room-authority-pending-regression\n"
                    + (pass ? "pass" : "fail")
                    + "; CA blueprint " + tracked.ThingID + " "
                    + (tracked.def.entityDefToBuild as ThingDef)?.defName
                    + " at " + tracked.Position + "; production program edit "
                    + "Barracks -> Kitchen\npending blueprint canceled "
                    + canceled + "; authored authority changed "
                    + authorityChanged + "; all eight native beds preserved "
                    + bedsPreserved + "; completed buildings changed none; "
                    + "no save written; reload the retained preplacement "
                    + "checkpoint to restore the disposable state\n"
                    + census
                    + (wokeForFixture
                        ? "\ndisposable author was awakened for this proof"
                        : "");
            }
            finally
            {
                AutonomyComponent.SetTier(author,
                    (CAInitiativeTier)priorAutonomy);
                Find.TickManager.CurTimeSpeed = priorSpeed;
            }
        }

        internal static string DollyCompletedAuthorityRegressionReceipt()
        {
            Map map = Find.CurrentMap;
            CASpaceProgram program;
            List<Building_Bed> beds;
            string failure;
            if (!TryResolveAuthoredBarracksFixture(map, 8, 8, out program,
                out beds, out failure))
                return "[CA] spatial-room-authority-completed-regression\n"
                    + "refused; " + failure;
            if (!TryVerifyDollyBarracksOwnership(map, program, beds,
                out failure))
                return "[CA] spatial-room-authority-completed-regression\n"
                    + "refused; " + failure;
            PlannedUseMapComponent programs =
                PlannedUseMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            List<Building> facilities = FacilitiesInProgramRoom(map, program,
                beds);
            if (programs == null || spatial == null
                || spatial.HasPendingPlan || facilities.Count < 1
                || spatial.CompletedRoomCount != facilities.Count)
                return "[CA] spatial-room-authority-completed-regression\n"
                    + "refused; the retained working checkpoint lacks a clean "
                    + "completed CA facility association";

            TimeSpeed priorSpeed = Find.TickManager.CurTimeSpeed;
            int programId = program.id;
            List<IntVec3> cells = new List<IntVec3>(program.cells);
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                programs.ClearCells(cells);
                string census = spatial.Census();
                bool programDeleted = programs.FindProgram(programId) == null;
                int survivingFacilities = facilities.Count(facility =>
                    facility != null && facility.Spawned
                    && !facility.Destroyed && facility.Map == map);
                int survivingBeds = beds.Count(bed => bed != null
                    && bed.Spawned && !bed.Destroyed && bed.Map == map);
                int ownedBeds = beds.Count(bed =>
                    bed.OwnersForReading.Count > 0);
                bool associationRetired = spatial.CompletedRoomCount == 0
                    && !spatial.HasPendingPlan;
                bool pass = programDeleted && associationRetired
                    && survivingFacilities == facilities.Count
                    && survivingBeds == beds.Count && ownedBeds == beds.Count;
                return "[CA] spatial-room-authority-completed-regression\n"
                    + (pass ? "pass" : "fail")
                    + "; production ClearCells deleted authored program #"
                    + programId + " across " + cells.Count + " cells; stale CA "
                    + "association retired " + associationRetired
                    + "\nplayer-owned completed facilities survived "
                    + survivingFacilities + "/" + facilities.Count + " ["
                    + string.Join(", ", facilities.Select(facility =>
                        facility.ThingID + " at " + facility.Position)
                        .ToArray()) + "]; native beds survived "
                    + survivingBeds + "/" + beds.Count + ", still owned "
                    + ownedBeds + "/" + beds.Count
                    + "; no building, bed, ownership, or save changed; reload "
                    + "the retained working checkpoint to restore the authored "
                    + "program\n" + census;
            }
            finally
            {
                Find.TickManager.CurTimeSpeed = priorSpeed;
            }
        }

        private static bool TryResolveCleanDollyAuthorityPreplacement(Map map,
            out CASpaceProgram program, out List<Building_Bed> beds,
            out AutonomousHomeMapComponent home,
            out CASpatialInitiativeMapComponent spatial, out string failure)
        {
            home = null;
            spatial = null;
            if (!TryResolveAuthoredBarracksFixture(map, 8, 8, out program,
                out beds, out failure)) return false;
            if (!TryVerifyDollyBarracksOwnership(map, program, beds,
                out failure)) return false;
            home = AutonomousHomeMapComponent.For(map);
            spatial = CASpatialInitiativeMapComponent.For(map);
            if (home == null || home.HasPendingPlanForVerification)
            {
                failure = "the exact home-construction state is not clean";
                return false;
            }
            if (spatial == null || spatial.HasPendingPlan
                || spatial.CompletedRoomCount != 0
                || FacilitiesInProgramRoom(map, program, beds).Count != 0
                || FacilityConstructionInProgram(map, program, beds).Count
                    != 0)
            {
                failure = "the retained eight-bed preplacement checkpoint "
                    + "already contains room-facility work";
                return false;
            }
            failure = null;
            return true;
        }

        private static List<Thing> FacilityConstructionInProgram(Map map,
            CASpaceProgram program, List<Building_Bed> beds)
        {
            if (map == null || program == null || beds == null)
                return new List<Thing>();
            var authority = new HashSet<IntVec3>(program.cells);
            var linkable = new HashSet<ThingDef>(beds.SelectMany(bed =>
                bed.def.GetCompProperties<
                    CompProperties_AffectedByFacilities>()?
                    .linkableFacilities ?? new List<ThingDef>()));
            return map.listerThings.AllThings.Where(thing => thing != null
                    && thing.Spawned
                    && (thing is Blueprint_Build || thing is Frame)
                    && linkable.Contains(thing.def.entityDefToBuild
                        as ThingDef)
                    && thing.OccupiedRect().Cells.All(authority.Contains))
                .OrderBy(thing => thing.thingIDNumber).ToList();
        }

        private static bool TryResolveAuthoredBarracksFixture(Map map,
            out CASpaceProgram program, out List<Building_Bed> beds,
            out string failure)
        {
            return TryResolveAuthoredBarracksFixture(map, 7, 7, out program,
                out beds, out failure);
        }

        private static bool TryResolveAuthoredBarracksFixture(Map map,
            int expectedResidents, int expectedBeds,
            out CASpaceProgram program, out List<Building_Bed> beds,
            out string failure)
        {
            beds = new List<Building_Bed>();
            if (!TryResolveAuthoredBarracksProgram(map, out program,
                out failure)) return false;
            int residents = program.residents?
                .Count(pawn => pawn != null) ?? 0;
            if (residents != expectedResidents
                || program.maxOccupants != expectedResidents)
            {
                failure = "expected exactly " + expectedResidents
                    + " authored residents at an occupancy limit of "
                    + expectedResidents + ", observed residents " + residents
                    + " and limit " + program.maxOccupants;
                return false;
            }
            beds = BedsInProgram(map, program);
            if (beds.Count != expectedBeds)
            {
                failure = "expected " + expectedBeds
                    + " completed native humanlike beds inside the authored "
                    + "fixture, observed " + beds.Count;
                return false;
            }
            Room room = beds[0].GetRoom();
            if (room == null || room.Role != RoomRoleDefOf.Barracks
                || beds.Any(bed => bed.GetRoom() != room))
            {
                failure = "the fixture beds no longer share one native "
                    + "Barracks";
                return false;
            }
            failure = null;
            return true;
        }

        private static bool TryResolveAuthoredBarracksProgram(Map map,
            out CASpaceProgram program, out string failure)
        {
            program = null;
            if (map == null || !map.IsPlayerHome)
            {
                failure = "no authorized player-home map is loaded";
                return false;
            }
            List<CASpaceProgram> matches = (PlannedUseMapComponent.For(map)?
                    .ProgramsForObservation ?? Array.Empty<CASpaceProgram>())
                .Where(candidate => candidate != null
                    && candidate.author == CASpaceAuthor.Player
                    && candidate.purpose == CASpacePurpose.Barracks
                    && candidate.RequiresSleep && candidate.cells.Count == 60
                    && candidate.label == "Operator-marked Barracks A")
                .OrderBy(candidate => candidate.id).ToList();
            if (matches.Count != 1)
            {
                failure = "expected exactly one strict existing 60-cell "
                    + "player-authored Barracks fixture, observed "
                    + matches.Count;
                return false;
            }
            program = matches[0];
            failure = null;
            return true;
        }

        private static List<Building_Bed> BedsInProgram(Map map,
            CASpaceProgram program)
        {
            var authority = new HashSet<IntVec3>(program?.cells
                ?? new List<IntVec3>());
            return map?.listerBuildings?.allBuildingsColonist
                .OfType<Building_Bed>().Where(bed => bed != null
                    && bed.Spawned && bed.Faction == Faction.OfPlayer
                    && !bed.Medical && !bed.ForPrisoners
                    && bed.def.building?.bed_humanlike == true
                    && bed.OccupiedRect().Cells.All(authority.Contains))
                .OrderBy(bed => bed.thingIDNumber).ToList()
                ?? new List<Building_Bed>();
        }

        private static int PlannedHumanlikeBedsInProgram(Map map,
            CASpaceProgram program)
        {
            if (map == null || program == null) return 0;
            var authority = new HashSet<IntVec3>(program.cells);
            int count = 0;
            ThingRequestGroup[] groups =
                { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame };
            for (int g = 0; g < groups.Length; g++)
            {
                List<Thing> things = map.listerThings.ThingsInGroup(groups[g]);
                for (int i = 0; i < things.Count; i++)
                {
                    ThingDef def = things[i].def.entityDefToBuild as ThingDef;
                    if (def != null && def.IsBed
                        && def.building?.bed_humanlike == true
                        && GenAdj.OccupiedRect(things[i].Position,
                            things[i].Rotation, def.Size).Cells
                            .All(authority.Contains)) count++;
                }
            }
            return count;
        }

        private static bool TryVerifyDollyBarracksOwnership(Map map,
            CASpaceProgram program, List<Building_Bed> beds,
            out string failure)
        {
            Pawn dolly = map?.mapPawns?.FreeColonistsSpawned
                .FirstOrDefault(CAPrepareCarefullyFixtureBridge
                    .IsExactDollyIdentity);
            Pawn luc = map?.mapPawns?.FreeColonistsSpawned
                .FirstOrDefault(CAPrepareCarefullyFixtureBridge
                    .IsExactLucIdentity);
            List<Pawn> residents = (program?.residents ?? new List<Pawn>())
                .Where(pawn => pawn != null).ToList();
            if (dolly == null || luc == null || dolly.GetFirstSpouse() != luc
                || luc.GetFirstSpouse() != dolly || !residents.Contains(dolly))
            {
                failure = "exact Dolly, her Barracks roster membership, or "
                    + "the reciprocal native Dolly-Luc spouse relation is missing";
                return false;
            }
            List<Building_Bed> owned = residents
                .Select(pawn => pawn.ownership?.OwnedBed).ToList();
            if (residents.Count != 8 || owned.Any(bed => bed == null
                    || !beds.Contains(bed))
                || owned.Distinct().Count() != residents.Count)
            {
                failure = "all eight authored residents do not own eight "
                    + "distinct completed native beds inside the Barracks";
                return false;
            }
            failure = null;
            return true;
        }

        private static List<Building> FacilitiesInProgramRoom(Map map,
            CASpaceProgram program, List<Building_Bed> beds)
        {
            Room room = beds?.FirstOrDefault()?.GetRoom();
            return map?.listerBuildings?.allBuildingsColonist
                .Where(building => building != null && building.Spawned
                    && building.TryGetComp<CompFacility>() != null
                    && building.GetRoom() == room
                    && program.cells.Contains(building.Position))
                .OrderBy(building => building.thingIDNumber).ToList()
                ?? new List<Building>();
        }

        private static bool TryResolveMountainShelfFixture(Map map,
            out Zone_Stockpile selected, out string failure)
        {
            selected = null;
            if (map == null || !map.IsPlayerHome)
            {
                failure = "no authorized player-home map is loaded";
                return false;
            }
            List<Zone_Stockpile> matches = map.zoneManager.AllZones
                .OfType<Zone_Stockpile>().Where(zone => zone.settings != null
                    && zone.settings.Priority == StoragePriority.Important
                    && zone.cells.Count == 44
                    && (zone.slotGroup?.HeldThings.Count() ?? 0) == 23
                    && (zone.slotGroup?.HeldThings.Sum(thing =>
                        thing.stackCount) ?? 0) == 228)
                .OrderBy(zone => zone.ID).ToList();
            if (matches.Count != 1)
            {
                failure = "expected exactly one Important 44-cell, 23-stack, "
                    + "228-unit native stockpile, observed " + matches.Count;
                return false;
            }
            selected = matches[0];
            failure = null;
            return true;
        }

        private static bool TryResolveAbovegroundShelfFixture(Map map,
            out Zone_Stockpile selected, out string failure)
        {
            selected = null;
            if (map == null || !map.IsPlayerHome)
            {
                failure = "no authorized player-home map is loaded";
                return false;
            }
            List<Zone_Stockpile> zones = map.zoneManager.AllZones
                .OfType<Zone_Stockpile>().OrderBy(zone => zone.ID).ToList();
            List<Zone_Stockpile> matches = zones.Where(zone =>
                    zone.cells.Count == 42
                    && (zone.slotGroup?.HeldThings.Count() ?? 0) == 5)
                .ToList();
            if (zones.Count != 1 || matches.Count != 1)
            {
                failure = "expected the one-zone 42-cell, five-stack "
                    + "aboveground corpus, observed " + zones.Count
                    + " zone(s) and " + matches.Count + " match(es)";
                return false;
            }
            selected = matches[0];
            failure = null;
            return true;
        }

        private static bool TryPrepareDisposableAutonomousBuilder(Map map,
            out Pawn author, out int priorAutonomy, out bool woke,
            out string failure)
        {
            author = map?.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn != null && !pawn.Downed && !pawn.Drafted
                    && !pawn.InMentalState
                    && pawn.health?.capacities?.CanBeAwake == true
                    && pawn.CurJob?.playerForced != true
                    && pawn.workSettings != null
                    && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    && pawn.workSettings.WorkIsActive(
                        WorkTypeDefOf.Construction)
                    && KnowledgeMapComponent.For(map)?.KnowsAnyThreat(pawn)
                        != true)
                .OrderByDescending(pawn => pawn.skills?
                    .GetSkill(SkillDefOf.Construction)?.Level ?? 0)
                .ThenBy(pawn => pawn.thingIDNumber).FirstOrDefault();
            priorAutonomy = author == null ? 1
                : (int)AutonomyComponent.TierOf(author);
            woke = author != null && !author.Awake();
            if (author == null)
            {
                failure = "the disposable corpus has no wakeable, "
                    + "threat-unaware construction author for the bounded proof";
                return false;
            }
            if (woke)
                author.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (!author.Awake())
            {
                failure = "the selected disposable construction author could "
                    + "not be awakened for the bounded proof";
                return false;
            }
            AutonomyComponent.SetTier(author, CAInitiativeTier.Autonomous);
            failure = null;
            return true;
        }
    }
}
