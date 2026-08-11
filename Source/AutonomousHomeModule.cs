using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal enum CAHomePlanKind
    {
        Bed,
        Table,
        Seat,
        Light,
        Recreation
    }

    internal struct CAHomePlan
    {
        public ThingDef def;
        public ThingDef stuff;
        public IntVec3 cell;
        public Rot4 rotation;
        public string reason;
        public CAHomePlanKind kind;
        public CASpaceProgram program;
        public CAInitiativeTier minimumInitiative;
        // Zero is the legacy/no-concrete-target sentinel. Positive values bind
        // an authored Bedroom cause to one exact saved resident across build.
        public int targetResidentId;
        public bool outdoor;
        public bool seatForJoy;
        public string placementEvidence;
        public string behaviorKey;
        public int episodeId;
        public CAIntentOrigin intentOrigin;
        public CAIntentController intentController;
        public CAAuthorityOrigin authorityOrigin;
        public string authorityIdentity;
        public string ownershipScope;
        public int issuerId;
        public int ownerId;
        public int createdTick;
        public CAInitiativeTier creationTier;
        public string targetOrDemand;
        public string terminationCondition;
    }

    internal struct CABedFengShuiEvidence
    {
        public int existingBeds;
        public int headWalls;
        public int openApproaches;
        public int alignedRows;
        public int matchingBankIntervals;
        public int irregularBankIntervals;
        public int protectedExistingApproaches;
        public int protectedDoorwayApproaches;
        public int clearNegativeSpaceBefore;
        public int clearNegativeSpaceAfter;
        public int requiredNegativeSpace;

        public float PatternScore => alignedRows * 7f
            + matchingBankIntervals * 5f
            - irregularBankIntervals * 6f;

        public float Total => headWalls * 12f
            + Mathf.Min(openApproaches, 4) * 1.5f
            + PatternScore;

        public string Receipt()
        {
            return "Feng Shui bed layout " + Total.ToString("F2")
                + " (existing beds " + existingBeds
                + ", wall-aligned heads " + headWalls
                + ", open approaches " + openApproaches
                + ", aligned opposite rows " + alignedRows
                + ", matching bank intervals " + matchingBankIntervals
                + ", irregular bank intervals " + irregularBankIntervals
                + ", protected existing bed approaches "
                + protectedExistingApproaches
                + ", protected doorway approaches "
                + protectedDoorwayApproaches
                + ", clear negative space " + clearNegativeSpaceBefore
                + " -> " + clearNegativeSpaceAfter
                + " (functional approach minimum "
                + requiredNegativeSpace + ")"
                + ", pattern " + PatternScore.ToString("F2") + ")";
        }
    }

    internal enum CAHomeSelection
    {
        None,
        Planned,
        Blocked
    }

    public class CAHomeBuiltRecord : IExposable
    {
        public string defName;
        public string kind;
        public string thingId;
        public IntVec3 cell = IntVec3.Invalid;
        public int programId;
        public int targetResidentId;
        public string placementEvidence;
        public string originThingId;
        public string consumingFrameThingId;
        public string consumedMaterialThingId;
        public string consumedMaterialEvidence;
        public int consumedMaterialCount;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref programId, "programId", 0);
            Scribe_Values.Look(ref targetResidentId, "targetResidentId", 0);
            Scribe_Values.Look(ref placementEvidence, "placementEvidence");
            Scribe_Values.Look(ref originThingId, "originThingId");
            Scribe_Values.Look(ref consumingFrameThingId,
                "consumingFrameThingId");
            Scribe_Values.Look(ref consumedMaterialThingId,
                "consumedMaterialThingId");
            Scribe_Values.Look(ref consumedMaterialEvidence,
                "consumedMaterialEvidence");
            Scribe_Values.Look(ref consumedMaterialCount,
                "consumedMaterialCount", 0);
        }
    }

    // Proactive and Autonomous colonists can originate a bounded domestic plan.
    // The component creates only the same native blueprints the build designator
    // creates. RimWorld's ordinary construction work, materials, reservations,
    // skill checks, areas, and player intervention remain the execution authority.
    public class AutonomousHomeMapComponent : MapComponent
    {
        private const int InitialDelayTicks = 300;
        private const int CheckIntervalTicks = 600;
        private int nextCheckTick;
        private string pendingDefName;
        private string pendingKind;
        private IntVec3 pendingCell = IntVec3.Invalid;
        private string pendingThingId;
        private string pendingOriginThingId;
        private string pendingConsumingFrameThingId;
        private string pendingConsumedMaterialThingId;
        private string pendingConsumedMaterialEvidence;
        private int pendingConsumedMaterialCount;
        private int pendingSinceTick = -1;
        private int pendingProgramId;
        private int pendingTargetResidentId;
        private string pendingBehaviorKey;
        private int pendingEpisodeId;
        private int pendingIntentOrigin;
        private int pendingIntentController;
        private int pendingAuthorityOrigin;
        private string pendingAuthorityIdentity;
        private string pendingOwnershipScope;
        private int pendingIssuerId = -1;
        private int pendingOwnerId = -1;
        private int pendingCreatedTick = -1;
        private int pendingCreationTier;
        private string pendingTargetOrDemand;
        private string pendingTerminationCondition;
        private Dictionary<string, int> suppressedKinds =
            new Dictionary<string, int>();
        private List<CAHomeBuiltRecord> completedAutoBuildings =
            new List<CAHomeBuiltRecord>();
        private int observedResetGeneration;

        private int lastPlannerId = -1;
        private string lastPlannedDef;
        private string lastPlannedStuff;
        private IntVec3 lastPlannedCell = IntVec3.Invalid;
        private int lastPlannedTick = -1;
        private string lastReason;
        private string lastPlacementEvidence;
        private string lastOutcome = "not evaluated";
        private readonly Dictionary<Room, List<Building_Bed>>
            placementBedsByRoom = new Dictionary<Room, List<Building_Bed>>();
        private readonly Dictionary<Room, HashSet<IntVec3>>
            placementSleepCirculationByRoom =
                new Dictionary<Room, HashSet<IntVec3>>();
        private readonly Dictionary<Room, List<Building>>
            placementDiningConflictsByRoom =
                new Dictionary<Room, List<Building>>();

        public AutonomousHomeMapComponent(Map map) : base(map) { }

        public static AutonomousHomeMapComponent For(Map map)
        {
            return map != null ? map.GetComponent<AutonomousHomeMapComponent>() : null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextCheckTick, "CA_homeNextCheckTick", 0);
            Scribe_Values.Look(ref pendingDefName, "CA_homePendingDef");
            Scribe_Values.Look(ref pendingKind, "CA_homePendingKind");
            Scribe_Values.Look(ref pendingCell, "CA_homePendingCell", IntVec3.Invalid);
            Scribe_Values.Look(ref pendingThingId, "CA_homePendingThingId");
            Scribe_Values.Look(ref pendingOriginThingId,
                "CA_homePendingOriginThingId");
            Scribe_Values.Look(ref pendingConsumingFrameThingId,
                "CA_homePendingConsumingFrameThingId");
            Scribe_Values.Look(ref pendingConsumedMaterialThingId,
                "CA_homePendingConsumedMaterialThingId");
            Scribe_Values.Look(ref pendingConsumedMaterialEvidence,
                "CA_homePendingConsumedMaterialEvidence");
            Scribe_Values.Look(ref pendingConsumedMaterialCount,
                "CA_homePendingConsumedMaterialCount", 0);
            Scribe_Values.Look(ref pendingSinceTick, "CA_homePendingSinceTick", -1);
            Scribe_Values.Look(ref pendingProgramId, "CA_homePendingProgramId", 0);
            Scribe_Values.Look(ref pendingTargetResidentId,
                "CA_homePendingTargetResidentId", 0);
            Scribe_Values.Look(ref pendingBehaviorKey,
                "CA_homePendingBehaviorKey");
            Scribe_Values.Look(ref pendingEpisodeId,
                "CA_homePendingEpisodeId", 0);
            Scribe_Values.Look(ref pendingIntentOrigin,
                "CA_homePendingIntentOrigin", 0);
            Scribe_Values.Look(ref pendingIntentController,
                "CA_homePendingIntentController", 0);
            Scribe_Values.Look(ref pendingAuthorityOrigin,
                "CA_homePendingAuthorityOrigin", 0);
            Scribe_Values.Look(ref pendingAuthorityIdentity,
                "CA_homePendingAuthorityIdentity");
            Scribe_Values.Look(ref pendingOwnershipScope,
                "CA_homePendingOwnershipScope");
            Scribe_Values.Look(ref pendingIssuerId,
                "CA_homePendingIssuerId", -1);
            Scribe_Values.Look(ref pendingOwnerId,
                "CA_homePendingOwnerId", -1);
            Scribe_Values.Look(ref pendingCreatedTick,
                "CA_homePendingCreatedTick", -1);
            Scribe_Values.Look(ref pendingCreationTier,
                "CA_homePendingCreationTier", 0);
            Scribe_Values.Look(ref pendingTargetOrDemand,
                "CA_homePendingTargetOrDemand");
            Scribe_Values.Look(ref pendingTerminationCondition,
                "CA_homePendingTerminationCondition");
            Scribe_Collections.Look(ref suppressedKinds, "CA_homeSuppressedKinds",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref completedAutoBuildings,
                "CA_homeCompletedBuildings", LookMode.Deep);
            Scribe_Values.Look(ref observedResetGeneration,
                "CA_homeObservedResetGeneration", 0);
            Scribe_Values.Look(ref lastPlannerId, "CA_homeLastPlannerId", -1);
            Scribe_Values.Look(ref lastPlannedDef, "CA_homeLastPlannedDef");
            Scribe_Values.Look(ref lastPlannedStuff, "CA_homeLastPlannedStuff");
            Scribe_Values.Look(ref lastPlannedCell, "CA_homeLastPlannedCell", IntVec3.Invalid);
            Scribe_Values.Look(ref lastPlannedTick, "CA_homeLastPlannedTick", -1);
            Scribe_Values.Look(ref lastReason, "CA_homeLastReason");
            Scribe_Values.Look(ref lastPlacementEvidence,
                "CA_homeLastPlacementEvidence");
            if (suppressedKinds == null)
                suppressedKinds = new Dictionary<string, int>();
            if (completedAutoBuildings == null)
                completedAutoBuildings = new List<CAHomeBuiltRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CACombatIntent.ObserveEpisode(pendingEpisodeId);
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
            TryPlanNow(out _);
        }

        public bool DebugPlanNow(out string outcome)
        {
            nextCheckTick = Find.TickManager.TicksGame + CheckIntervalTicks;
            bool result = TryPlanNow(out outcome);
            return result;
        }

        internal bool HasPendingPlanForVerification =>
            PendingPlanStillExists();

        internal string PendingPlanForVerification
        {
            get
            {
                if (!PendingPlanStillExists()) return "none";
                return (pendingKind.NullOrEmpty() ? "unclassified" : pendingKind)
                    + " " + pendingDefName + " at " + pendingCell + " since "
                    + pendingSinceTick + ", program "
                    + (pendingProgramId > 0 ? pendingProgramId.ToString() : "none")
                    + ", target resident "
                    + (pendingTargetResidentId > 0
                        ? pendingTargetResidentId.ToString() : "legacy/none")
                    + ", behavior "
                    + (pendingBehaviorKey ?? "legacy/unregistered")
                    + ", episode " + pendingEpisodeId
                    + ", controller "
                    + ((CAIntentController)pendingIntentController)
                    + ", exact construction "
                    + (pendingThingId.NullOrEmpty()
                        ? "legacy/untracked" : pendingThingId);
            }
        }

        internal bool PendingPlanMatchesForVerification(int programId,
            int targetResidentId, Thing construction)
        {
            return construction != null
                && TryObservePendingHomeConstruction(out string ignoredPending)
                && pendingProgramId == programId
                && pendingTargetResidentId == targetResidentId
                && !pendingThingId.NullOrEmpty()
                && pendingThingId == construction.ThingID;
        }

        internal bool HasPendingPlanForObservation =>
            TryObservePendingHomeConstruction(out string ignoredPending);

        internal string PendingOriginThingIdForVerification =>
            TryObservePendingHomeConstruction(out string ignoredPending)
                ? pendingOriginThingId : null;

        internal void DeferPlanningForBedroomCauseVerification()
        {
            int afterReceiptWindow = Find.TickManager.TicksGame
                + CheckIntervalTicks;
            if (nextCheckTick < afterReceiptWindow)
                nextCheckTick = afterReceiptWindow;
        }

        internal bool TryGetCompletedPlanEvidenceForVerification(
            int programId, int targetResidentId, Building building,
            out string evidence, out string originThingId,
            out string consumingFrameThingId,
            out string consumedMaterialThingId,
            out string consumedMaterialEvidence,
            out int consumedMaterialCount)
        {
            evidence = null;
            originThingId = null;
            consumingFrameThingId = null;
            consumedMaterialThingId = null;
            consumedMaterialEvidence = null;
            consumedMaterialCount = 0;
            if (building == null || building.Destroyed) return false;
            for (int i = 0; i < completedAutoBuildings.Count; i++)
            {
                CAHomeBuiltRecord record = completedAutoBuildings[i];
                if (record != null && record.thingId == building.ThingID
                    && record.programId == programId
                    && record.targetResidentId == targetResidentId)
                {
                    evidence = record.placementEvidence;
                    originThingId = record.originThingId;
                    consumingFrameThingId = record.consumingFrameThingId;
                    consumedMaterialThingId = record.consumedMaterialThingId;
                    consumedMaterialEvidence =
                        record.consumedMaterialEvidence;
                    consumedMaterialCount = record.consumedMaterialCount;
                    return true;
                }
            }
            return false;
        }

        internal bool TryVerifyContextualBedPlan(CASpaceProgram program,
            out string state, out string detail)
        {
            state = "receipt rejected";
            detail = "no matching active home objective exists";
            if (program == null)
            {
                detail = "no target program exists";
                return false;
            }

            if (!pendingDefName.NullOrEmpty()
                && pendingKind == CAHomePlanKind.Bed.ToString()
                && pendingProgramId == program.id
                && pendingCell.IsValid
                && program.cells.Contains(pendingCell)
                && TryObservePendingHomeConstruction(out string ignoredPending)
                && CAHomePrerequisiteMapComponent
                    .HasContextualBedPlacementEvidence(lastPlacementEvidence))
            {
                state = "blueprint placed";
                detail = pendingDefName + " at " + pendingCell + " in program "
                    + pendingProgramId + " [" + lastPlacementEvidence + "]";
                return true;
            }

            CAHomePrerequisiteMapComponent prerequisites =
                CAHomePrerequisiteMapComponent.For(map);
            if (prerequisites != null
                && prerequisites.TryVerifyContextualBedDemand(program,
                    out detail))
            {
                state = "objective retained";
                return true;
            }

            detail = "neither the active blueprint nor retained demand is an "
                + "exact contextual Bed for program " + program.id + "; "
                + detail;
            return false;
        }

        public void NotifyPlanningSettingChanged(bool enabled, int resetGeneration)
        {
            if (!enabled)
            {
                CAAgentDebugBridge bridge =
                    CAAgentDebugBridge.ForCurrentGame();
                if (bridge?.HasActiveBedroomCausePlanningScopeForMap(map)
                        == true)
                {
                    bridge.RetireBedroomCausePlanningScopeForMap(map,
                        "operator-disabled global Home planning ended the tracked cause");
                    if (!pendingOriginThingId.NullOrEmpty())
                        bridge.RetireBedroomCauseMaterialAudit(
                            pendingProgramId, pendingTargetResidentId,
                            pendingOriginThingId);
                }
                CAHomePrerequisiteMapComponent.For(map)
                    ?.CancelUnselectedObjective("Home planning was disabled");
                return;
            }
            ApplyResetGeneration(resetGeneration);
        }

        private void ApplyResetGeneration(int resetGeneration)
        {
            if (observedResetGeneration == resetGeneration) return;
            suppressedKinds.Clear();
            observedResetGeneration = resetGeneration;
            lastOutcome = "Home planning was reset; player vetoes were cleared";
        }

        private bool TryPlanNow(out string outcome)
        {
            outcome = "no plan";
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null)
            {
                return Finish("home planning settings are unavailable", out outcome);
            }
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            bool trackedBedroomCause = bridge?
                .HasActiveBedroomCausePlanningScopeForMap(map) == true;
            if (!settings.autonomousHomePlanning)
            {
                if (trackedBedroomCause)
                {
                    bridge.RetireBedroomCausePlanningScopeForMap(map,
                        "global Home planning was disabled");
                    if (!pendingOriginThingId.NullOrEmpty())
                        bridge.RetireBedroomCauseMaterialAudit(
                            pendingProgramId, pendingTargetResidentId,
                            pendingOriginThingId);
                }
                CAHomePrerequisiteMapComponent.For(map)
                    ?.CancelUnselectedObjective("Home planning is disabled");
                return Finish("home planning is disabled", out outcome);
            }
            if (settings.autonomousHomePlanning)
                ApplyResetGeneration(
                    settings.autonomousHomePlanningResetGeneration);
            if (!map.IsPlayerHome)
            {
                if (bridge?.HasActiveBedroomCausePlanningScopeForMap(map)
                    == true)
                {
                    bridge.RetireBedroomCausePlanningScopeForMap(map,
                        "the map is no longer an authorized player home");
                    if (!pendingOriginThingId.NullOrEmpty())
                        bridge.RetireBedroomCauseMaterialAudit(
                            pendingProgramId, pendingTargetResidentId,
                            pendingOriginThingId);
                }
                CAHomePrerequisiteMapComponent.For(map)
                    ?.CancelUnselectedObjective(
                        "the map is no longer an authorized player home");
                return Finish("this map is not a player home", out outcome);
            }
            PruneCompletedRecords();
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            placementBedsByRoom.Clear();
            placementSleepCirculationByRoom.Clear();
            placementDiningConflictsByRoom.Clear();
            if (HasAutonomousResident())
            {
                string negotiation;
                PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
                if (programs != null
                    && programs.TryNegotiateResidents(out negotiation))
                    Log.Message("[CA] resident negotiation: " + negotiation);
            }
            if (PendingPlanStillExists())
            {
                return Finish("waiting for the pending " + pendingDefName
                    + " at " + pendingCell, out outcome);
            }
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            if (spatial?.HasPendingPlan == true)
            {
                return Finish("waiting for the active shared spatial-initiative "
                    + "construction objective before originating another CA "
                    + "blueprint", out outcome);
            }
            ClearPending();

            CAHomePrerequisiteMapComponent prerequisites =
                CAHomePrerequisiteMapComponent.For(map);
            if (prerequisites?.HasDemand == true)
            {
                CAHomePlan retainedPlan;
                string retainedProblem;
                if (!prerequisites.TryRehydrate(out retainedPlan,
                    out retainedProblem))
                {
                    prerequisites.CancelUnselectedObjective(
                        "saved objective became invalid: " + retainedProblem);
                }
                else
                {
                    if (RetainedProvisionExists(retainedPlan))
                    {
                        prerequisites.CancelUnselectedObjective(
                            "the exact retained provision now exists");
                    }
                    else
                    {
                        string authorization;
                        if (!RetainedNeedStillAuthorized(retainedPlan,
                            out authorization))
                        {
                            prerequisites.CancelUnselectedObjective(authorization);
                        }
                        else if (RetainedFootprintClaimedByPlayer(retainedPlan))
                        {
                            prerequisites.CancelUnselectedObjective(
                                "a later player-authored building superseded the retained footprint");
                        }
                        else
                        {
                            Pawn responsible = ChoosePlanner(
                                retainedPlan.minimumInitiative
                                    < CAInitiativeTier.Proactive
                                    ? CAInitiativeTier.Proactive
                                    : retainedPlan.minimumInitiative);
                            if (responsible == null)
                            {
                                prerequisites.RetainObjective(
                                    "no available threat-unaware construction author can resume the exact objective");
                                return Finish("the exact material objective is retained until a qualified, threat-unaware construction author is available",
                                    out outcome);
                            }
                            string validation;
                            if (!RetainedPlanCanResume(responsible, retainedPlan,
                                out validation))
                            {
                                prerequisites.RetainObjective(validation);
                                return Finish(validation, out outcome);
                            }
                            return PrepareAndPlace(responsible, retainedPlan,
                                out outcome);
                        }
                    }
                }
            }

            Pawn proactivePlanner = ChoosePlanner(CAInitiativeTier.Proactive);
            if (proactivePlanner == null)
            {
                CAHomePrerequisiteMapComponent.For(map)
                    ?.RetainObjective(
                        "no available threat-unaware Proactive+ construction author");
                return Finish("no awake, threat-unaware Proactive+ colonist has construction work enabled",
                    out outcome);
            }

            CAHomePlan plan;
            string blocker;
            CAHomeSelection essential = TrySelectEssentialPlan(
                proactivePlanner, out plan, out blocker);
            if (essential == CAHomeSelection.Planned)
            {
                plan.minimumInitiative = CAInitiativeTier.Proactive;
                return PrepareAndPlace(proactivePlanner, plan, out outcome);
            }
            if (essential == CAHomeSelection.Blocked)
            {
                CAHomePrerequisiteMapComponent.For(map)
                    ?.CancelUnselectedObjective(blocker);
                return Finish(blocker, out outcome);
            }

            Pawn autonomousPlanner = ChoosePlanner(
                CAInitiativeTier.Autonomous);
            if (autonomousPlanner == null)
            {
                CAHomePrerequisiteMapComponent.For(map)
                    ?.RetainObjective(
                        "no available threat-unaware Autonomous comfort author");
                return Finish("survival furnishings are covered; no awake, threat-unaware Autonomous colonist will add comfort furnishings",
                    out outcome);
            }

            if (TrySelectAutonomousPlan(autonomousPlanner, out plan, out blocker))
            {
                plan.minimumInitiative = CAInitiativeTier.Autonomous;
                return PrepareAndPlace(autonomousPlanner, plan, out outcome);
            }

            prerequisites = CAHomePrerequisiteMapComponent.For(map);
            if (blocker.NullOrEmpty())
                prerequisites?.CancelUnselectedObjective(
                    "the authorized furnishing slice is satisfied");
            else
                prerequisites?.RetainObjective(blocker);
            return Finish(blocker.NullOrEmpty()
                ? "the claimed home already has this slice's furnishings"
                : blocker, out outcome);
        }

        private bool Finish(string text, out string outcome)
        {
            lastOutcome = text;
            outcome = text;
            return false;
        }

        private Pawn ChoosePlanner(CAInitiativeTier minimumInitiative)
        {
            string behaviorKey = minimumInitiative
                    >= CAInitiativeTier.Autonomous
                ? "spatial.home_comfort" : "spatial.home_essentials";
            Pawn best = null;
            float bestScore = float.MinValue;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Downed || pawn.Drafted || pawn.InMentalState
                    || !pawn.Awake()
                    || !CABehaviorGate.StableProfileAllows(pawn,
                        behaviorKey))
                    continue;
                if (KnowledgeMapComponent.For(map)?.KnowsAnyThreat(pawn) == true)
                    continue;
                if (pawn.workSettings == null
                    || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Construction))
                    continue;

                DispositionProfile disposition = Disposition.Of(pawn);
                int construction = SkillLevel(pawn, SkillDefOf.Construction);
                int intellectual = SkillLevel(pawn, SkillDefOf.Intellectual);
                float score = disposition.initiative * 0.42f
                    + disposition.discipline * 0.18f
                    + construction / 20f * 0.30f
                    + intellectual / 20f * 0.10f;
                // Stable tie-break: the same people make the same choice after load.
                score += (pawn.thingIDNumber % 997) * 0.000001f;
                if (best == null || score > bestScore)
                {
                    best = pawn;
                    bestScore = score;
                }
            }
            return best;
        }

        private bool HasAutonomousResident()
        {
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && !pawn.Downed && !pawn.InMentalState
                    && CABehaviorGate.StableProfileAllows(pawn,
                        "spatial.resident_roster_negotiation"))
                    return true;
            }
            return false;
        }

        // Read-only causal selector for one exact player-authored Bedroom. A
        // successful result may be either a native ownership assignment for an
        // already completed bed or a construction candidate. Callers must read
        // actionClass before interpreting plan; this method never assigns a bed
        // and never places a blueprint.
        internal bool TrySelectPlayerBedroomConstructionCause(
            CASpaceProgram program, out CAHomePlan plan, out Pawn planner,
            out Pawn targetResident, out int currentSlots,
            out int targetSlots, out string actionClass, out string blocker)
        {
            plan = default(CAHomePlan);
            planner = null;
            targetResident = null;
            currentSlots = 0;
            targetSlots = 0;
            actionClass = "none";
            blocker = null;

            if (program == null || program.author != CASpaceAuthor.Player
                || program.purpose != CASpacePurpose.Bedroom
                || !program.RequiresSleep || program.cells == null
                || program.cells.Count == 0)
            {
                blocker = "the exact Player-authored, sleep-required Bedroom authority is unavailable";
                return false;
            }
            if (!map.IsPlayerHome)
            {
                blocker = "the Bedroom is not on an authorized player-home map";
                return false;
            }

            List<Pawn> residents = EligibleBedroomResidents(program);
            targetSlots = Mathf.Min(program.maxOccupants, residents.Count);
            // Present sleeping readiness is completed capacity only. A native
            // blueprint or frame is future capacity and is receipted separately
            // through the exact construction-stage commitment below.
            currentSlots = CountCompletedBedSlotsInProgram(program);
            if (!program.HasResidentRoster)
            {
                blocker = "the Bedroom has no saved resident roster, so no concrete resident supplies construction cause";
                return false;
            }
            if (targetSlots == 0)
            {
                blocker = "the saved Bedroom roster has no eligible resident on this map; absence supplies no construction cause";
                return false;
            }

            List<Pawn> unserved = new List<Pawn>();
            for (int i = 0; i < residents.Count && i < targetSlots; i++)
                if (!ResidentOwnsCompatibleProgramBed(residents[i], program))
                    unserved.Add(residents[i]);
            if (unserved.Count == 0)
            {
                blocker = "every eligible saved Bedroom resident already owns compatible sleeping capacity inside the exact authored footprint";
                return false;
            }
            targetResident = unserved[0];

            string commitment;
            if (HasMapWideCAConstructionCommitment(out commitment))
            {
                blocker = commitment;
                return false;
            }
            Thing playerBedConstruction;
            ThingDef playerBedDef;
            if (TryFindPlayerBedConstructionInProgram(program,
                    out playerBedConstruction, out playerBedDef))
            {
                blocker = "player Bed construction already has precedence: "
                    + playerBedDef.defName + " "
                    + (playerBedConstruction is Frame ? "frame" : "blueprint")
                    + " #" + playerBedConstruction.thingIDNumber + " at "
                    + playerBedConstruction.Position
                    + " intersects the exact authored Bedroom footprint";
                return false;
            }

            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            CAInitiativeTier ceiling = spatial?.TierFor(program)
                ?? CAInitiativeTier.Standard;
            if (ceiling < CAInitiativeTier.Proactive)
            {
                blocker = "the Bedroom initiative ceiling is "
                    + CAInitiativePresentation.Label(ceiling)
                    + "; Standard cannot select a discretionary Bedroom action";
                return false;
            }
            planner = ChoosePlannerForProgram(program,
                CAInitiativeTier.Proactive);
            if (planner == null)
            {
                blocker = "no awake, threat-unaware construction author has Proactive+ effective initiative in this Bedroom";
                return false;
            }

            Building_Bed assignableBed = FindCompatibleFreeProgramBed(
                program, targetResident);
            if (assignableBed != null)
            {
                plan = new CAHomePlan
                {
                    def = assignableBed.def,
                    stuff = assignableBed.Stuff,
                    cell = assignableBed.Position,
                    rotation = assignableBed.Rotation,
                    reason = targetResident.LabelShort + " #"
                        + targetResident.thingIDNumber
                        + " lacks owned sleeping capacity inside "
                        + program.label + ", while this completed native bed has a compatible free slot",
                    kind = CAHomePlanKind.Bed,
                    program = program,
                    minimumInitiative = CAInitiativeTier.Proactive,
                    targetResidentId = targetResident.thingIDNumber,
                    placementEvidence = "native ownership candidate: completed "
                        + assignableBed.def.defName + " #"
                        + assignableBed.thingIDNumber + " at "
                        + assignableBed.Position + " has "
                        + assignableBed.SleepingSlotsCount + " slot(s), "
                        + assignableBed.OwnersForReading.Count
                        + " current owner(s), RestUtility.CanUseBedNow accepted, CompAssignableToPawn_Bed.CanAssignTo accepted, and ideoligion sharing did not forbid assignment; no construction is selected"
                };
                actionClass = "native ownership assignment";
                blocker = null;
                return true;
            }

            if (currentSlots >= targetSlots)
            {
                blocker = "physical Bedroom capacity is " + currentSlots
                    + " of " + targetSlots + ", but no completed free slot passes native use and assignment semantics for "
                    + targetResident.LabelShort
                    + "; that ownership conflict does not authorize redundant construction";
                return false;
            }

            if (!TryMakeProgramSleepPlan(planner, program, currentSlots,
                    targetSlots, out plan, out blocker))
            {
                blocker = "the concrete deficit for "
                    + targetResident.LabelShort
                    + " has no valid native sleeping provision inside the exact authored Bedroom"
                    + BlockerSuffix(blocker);
                return false;
            }
            if (!PlanAddressesResident(plan, program, targetResident))
            {
                plan = default(CAHomePlan);
                blocker = "the candidate sleeping provision did not pass native resident compatibility for "
                    + targetResident.LabelShort;
                return false;
            }

            plan.minimumInitiative = CAInitiativeTier.Proactive;
            plan.targetResidentId = targetResident.thingIDNumber;
            plan.reason += "; concrete unmet resident "
                + targetResident.LabelShort + " #"
                + targetResident.thingIDNumber;
            plan.placementEvidence = (plan.placementEvidence.NullOrEmpty()
                    ? "" : plan.placementEvidence + "; ")
                + "resident cause: " + targetResident.LabelShort + " #"
                + targetResident.thingIDNumber
                + " lacks compatible owned sleeping capacity inside exact Player-authored Bedroom #"
                + program.id + "; candidate passes RestUtility.CanUseBedEver and addresses only the receipted sleep-capacity deficit";
            actionClass = "native Bed construction candidate (receipt only)";
            blocker = null;
            return true;
        }

        // Deterministic, non-mutating comparison across exact authored Bedroom
        // programs. It exposes the grounded action class without enabling the
        // background home planner or placing a native blueprint.
        internal string EvaluatePlayerBedroomConstructionCauses()
        {
            var builder = new StringBuilder();
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> observed = programs
                ?.ProgramsForObservation ?? Array.Empty<CASpaceProgram>();
            var bedrooms = new List<CASpaceProgram>();
            for (int i = 0; i < observed.Count; i++)
            {
                CASpaceProgram candidate = observed[i];
                if (candidate != null
                    && candidate.author == CASpaceAuthor.Player
                    && candidate.purpose == CASpacePurpose.Bedroom
                    && candidate.RequiresSleep)
                    bedrooms.Add(candidate);
            }
            bedrooms.Sort((left, right) => left.id.CompareTo(right.id));
            placementBedsByRoom.Clear();
            placementSleepCirculationByRoom.Clear();
            placementDiningConflictsByRoom.Clear();

            builder.Append("[CA] authored Bedroom construction causes: read-only; exact Player-authored sleep-required programs ")
                .Append(bedrooms.Count).AppendLine()
                .AppendLine("  authority: this evaluator assigns no owner, places no blueprint, changes no program or initiative ceiling, and enables no production Player-authored Bedroom Bed-cause consumer; separately ratified cross-room domestic consumers are unchanged.")
                .AppendLine("  causality: a concrete saved resident's unmet native sleeping capacity may select one ownership or Bed action. Dresser, EndTable, SleepAccelerator, other optional bed facilities, and native Beauty remain non-causal capacity or telemetry.");
            if (bedrooms.Count == 0)
            {
                builder.Append("  none observed; no authored authority originates work");
                return builder.ToString();
            }

            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            for (int i = 0; i < bedrooms.Count; i++)
            {
                CASpaceProgram program = bedrooms[i];
                CAHomePlan plan;
                Pawn planner;
                Pawn resident;
                int current;
                int target;
                string action;
                string blocker;
                bool selected = TrySelectPlayerBedroomConstructionCause(
                    program, out plan, out planner, out resident, out current,
                    out target, out action, out blocker);
                CAInitiativeTier ceiling = spatial?.TierFor(program)
                    ?? CAInitiativeTier.Standard;
                CAInitiativeTier effective = planner == null
                    ? CAInitiativeTier.Standard
                    : spatial?.EffectiveTier(planner, program)
                        ?? AutonomyComponent.TierOf(planner);
                builder.Append("  program #").Append(program.id).Append(" '")
                    .Append(program.label.NullOrEmpty() ? "Bedroom"
                        : program.label).AppendLine("':")
                    .Append("    present readiness completed slots current/target ").Append(current)
                    .Append("/").Append(target).Append(", resident ")
                    .Append(resident == null ? "none"
                        : resident.LabelShort + " #"
                            + resident.thingIDNumber)
                    .Append(", planner ").Append(planner == null ? "none"
                        : planner.LabelShort + " #" + planner.thingIDNumber)
                    .Append(", ceiling ")
                    .Append(CAInitiativePresentation.Label(ceiling))
                    .Append(", effective ")
                    .Append(planner == null ? "not selected"
                        : CAInitiativePresentation.Label(effective))
                    .AppendLine()
                    .AppendLine("    construction stage: blueprint/frame capacity is future capacity and is excluded from present readiness; it is evaluated separately through exact CA commitment and player-construction precedence evidence")
                    .Append("    result: ").Append(selected ? "selected " : "blocked ")
                    .Append(selected ? action : blocker).AppendLine();
                if (selected)
                {
                    builder.Append("    action: ").Append(action)
                        .Append(", definition ")
                        .Append(plan.def?.defName ?? "none").Append(", cell ")
                        .Append(plan.cell).Append(", rotation ")
                        .Append(plan.rotation).Append(", stuff ")
                        .Append(plan.stuff?.defName ?? "not required")
                        .AppendLine()
                        .Append("    spatial placement evidence: ")
                        .AppendLine(plan.placementEvidence.NullOrEmpty()
                            ? "not applicable to completed ownership assignment"
                            : plan.placementEvidence);
                }
                builder.AppendLine("    optional facility and Beauty cause: none; available bonuses do not manufacture a requirement");
            }
            return builder.ToString().TrimEnd();
        }

        // Debug-regression transition for one already selected exact cause.
        // It is not called by MapComponentTick and does not enable the general
        // Bedroom consumer.
        internal bool DebugPlacePlayerBedroomConstructionCause(
            CASpaceProgram program, out string outcome)
        {
            if (!TrySelectPlayerBedroomConstructionCause(program,
                    out CAHomePlan plan, out Pawn planner,
                    out Pawn targetResident, out int currentSlots,
                    out int targetSlots, out string actionClass,
                    out string blocker))
                return Finish("authored Bedroom cause was not selected: "
                    + blocker, out outcome);
            if (!actionClass.Contains("construction candidate")
                || plan.kind != CAHomePlanKind.Bed || plan.def == null
                || targetResident == null || plan.targetResidentId
                    != targetResident.thingIDNumber)
                return Finish("the selected Bedroom action was " + actionClass
                    + ", not one concrete native Bed construction cause",
                    out outcome);
            if (plan.minimumInitiative < CAInitiativeTier.Proactive)
                plan.minimumInitiative = CAInitiativeTier.Proactive;
            bool placed = PrepareAndPlace(planner, plan, out outcome);
            if (placed)
                outcome += "; debug regression crossed only the receipted "
                    + currentSlots + "/" + targetSlots
                    + " sleeping-capacity deficit for resident "
                    + targetResident.LabelShort + " #"
                    + targetResident.thingIDNumber;
            return placed;
        }

        private List<Pawn> EligibleBedroomResidents(CASpaceProgram program)
        {
            var residents = new List<Pawn>();
            if (program?.residents == null) return residents;
            var seen = new HashSet<Pawn>();
            for (int i = 0; i < program.residents.Count; i++)
            {
                Pawn pawn = program.residents[i];
                if (pawn == null || !seen.Add(pawn) || pawn.Dead
                    || pawn.Destroyed || !pawn.Spawned || pawn.Map != map
                    || !pawn.IsFreeColonist || pawn.needs?.rest == null
                    || pawn.DevelopmentalStage.Baby()) continue;
                residents.Add(pawn);
            }
            residents.Sort((left, right) => left.thingIDNumber.CompareTo(
                right.thingIDNumber));
            return residents;
        }

        private bool ResidentOwnsCompatibleProgramBed(Pawn resident,
            CASpaceProgram program)
        {
            Building_Bed bed = resident?.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned || bed.Map != map
                || !ThingInsideProgram(bed, bed.def, program)
                || !RestUtility.CanUseBedEver(resident, bed.def)) return false;
            CompAssignableToPawn_Bed assignable =
                bed.CompAssignableToPawn as CompAssignableToPawn_Bed;
            return assignable != null && assignable.CanAssignTo(resident).Accepted
                && !assignable.IdeoligionForbids(resident);
        }

        private Building_Bed FindCompatibleFreeProgramBed(
            CASpaceProgram program, Pawn resident)
        {
            Building_Bed best = null;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building_Bed bed = buildings[i] as Building_Bed;
                if (bed == null || !bed.Spawned
                    || bed.Faction != Faction.OfPlayer
                    || bed.def.building?.bed_humanlike != true
                    || !bed.def.building.bed_countsForBedroomOrBarracks
                    || !bed.ForColonists || bed.ForPrisoners || bed.Medical
                    || !bed.AnyUnownedSleepingSlot
                    || !ThingInsideProgram(bed, bed.def, program)) continue;
                CompAssignableToPawn_Bed assignable =
                    bed.CompAssignableToPawn as CompAssignableToPawn_Bed;
                if (assignable == null
                    || !assignable.CanAssignTo(resident).Accepted
                    || assignable.IdeoligionForbids(resident)
                    || !RestUtility.CanUseBedNow(bed, resident,
                        checkSocialProperness: true)) continue;
                if (best == null
                    || bed.thingIDNumber < best.thingIDNumber) best = bed;
            }
            return best;
        }

        private bool PlanAddressesResident(CAHomePlan plan,
            CASpaceProgram program, Pawn resident)
        {
            if (plan.def == null || !plan.def.IsBed
                || !RestUtility.CanUseBedEver(resident, plan.def)) return false;
            int slots = BedUtility.GetSleepingSlotsCount(plan.def.Size);
            if (slots <= 1) return true;
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            return programs != null
                && programs.TryGetShareableBedroomPair(program,
                    out Pawn first, out Pawn second)
                && (resident == first || resident == second);
        }

        private Pawn ChoosePlannerForProgram(CASpaceProgram program,
            CAInitiativeTier minimumEffectiveTier)
        {
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            Pawn best = null;
            float bestScore = float.MinValue;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null) continue;
                CAInitiativeTier effective = spatial?.EffectiveTier(pawn,
                    program) ?? CAInitiativeTier.Standard;
                if (pawn.Downed || pawn.Drafted
                    || pawn.InMentalState || !pawn.Awake()
                    || effective < minimumEffectiveTier) continue;
                if (KnowledgeMapComponent.For(map)?.KnowsAnyThreat(pawn) == true)
                    continue;
                if (pawn.workSettings == null
                    || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    || !pawn.workSettings.WorkIsActive(
                        WorkTypeDefOf.Construction)) continue;
                DispositionProfile disposition = Disposition.Of(pawn);
                int construction = SkillLevel(pawn, SkillDefOf.Construction);
                int intellectual = SkillLevel(pawn, SkillDefOf.Intellectual);
                float score = disposition.initiative * 0.42f
                    + disposition.discipline * 0.18f
                    + construction / 20f * 0.30f
                    + intellectual / 20f * 0.10f
                    + (pawn.thingIDNumber % 997) * 0.000001f;
                if (best == null || score > bestScore)
                {
                    best = pawn;
                    bestScore = score;
                }
            }
            return best;
        }

        private bool HasMapWideCAConstructionCommitment(out string detail)
        {
            if (TryObservePendingHomeConstruction(out string homePending))
            {
                detail = "waiting for active home construction objective "
                    + homePending;
                return true;
            }
            if (CAHomePrerequisiteMapComponent.For(map)?.HasDemand == true)
            {
                detail = "waiting for the retained home construction objective before selecting a Bedroom action";
                return true;
            }
            if (CASpatialInitiativeMapComponent.For(map)?
                    .HasPendingPlanForObservation == true)
            {
                detail = "waiting for the active spatial-initiative construction objective before selecting a Bedroom action";
                return true;
            }
            detail = null;
            return false;
        }

        private bool TryObservePendingHomeConstruction(out string detail)
        {
            detail = null;
            if (pendingDefName.NullOrEmpty() || pendingThingId.NullOrEmpty()
                || !pendingCell.IsValid
                || !pendingCell.InBounds(map)) return false;
            List<Thing> things = pendingCell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!(thing is Blueprint_Build) && !(thing is Frame))
                    continue;
                if (thing.ThingID != pendingThingId) continue;
                ThingDef entity = thing.def.entityDefToBuild as ThingDef;
                if (entity == null || entity.defName != pendingDefName)
                    continue;
                detail = (pendingKind.NullOrEmpty()
                        ? "unclassified" : pendingKind)
                    + " " + pendingDefName + " at " + pendingCell
                    + ", program " + (pendingProgramId > 0
                        ? pendingProgramId.ToString() : "none")
                    + ", target resident " + (pendingTargetResidentId > 0
                        ? pendingTargetResidentId.ToString()
                        : "legacy/none") + ", exact construction "
                    + pendingThingId;
                return true;
            }
            return false;
        }

        private bool TryFindPlayerBedConstructionInProgram(
            CASpaceProgram program, out Thing construction,
            out ThingDef bedDef)
        {
            construction = null;
            bedDef = null;
            ThingRequestGroup[] groups =
                { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame };
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            for (int group = 0; group < groups.Length; group++)
            {
                List<Thing> things = map.listerThings.ThingsInGroup(groups[group]);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    ThingDef def = thing?.def?.entityDefToBuild as ThingDef;
                    if (thing == null || !thing.Spawned || thing.Map != map
                        || thing.Faction != Faction.OfPlayer || def == null
                        || !def.IsBed || def.building?.bed_humanlike != true)
                        continue;
                    bool intersects = false;
                    foreach (IntVec3 occupied in GenAdj.OccupiedRect(
                        thing.Position, thing.Rotation, def.Size))
                    {
                        if (programs?.ProgramAt(occupied) != program) continue;
                        intersects = true;
                        break;
                    }
                    if (!intersects) continue;
                    if (construction == null
                        || thing.thingIDNumber < construction.thingIDNumber)
                    {
                        construction = thing;
                        bedDef = def;
                    }
                }
            }
            return construction != null;
        }

        private CAHomeSelection TrySelectEssentialPlan(
            Pawn planner, out CAHomePlan plan, out string blocker)
        {
            plan = default(CAHomePlan);
            blocker = null;

            int programSlots;
            int programTarget;
            CASpaceProgram sleepProgram = FindSleepProgramNeedingCapacity(
                out programSlots, out programTarget);
            if (sleepProgram != null)
            {
                if (TryMakeProgramSleepPlan(planner, sleepProgram,
                    programSlots, programTarget, out plan, out blocker))
                    return CAHomeSelection.Planned;
                blocker = sleepProgram.label + " provides " + programSlots
                    + " of " + programTarget
                    + " currently required sleeping places, but no valid provision fits its authored cells"
                    + BlockerSuffix(blocker);
                return CAHomeSelection.Blocked;
            }

            int people;
            int bedSlots;
            int unservedCouples;
            GetUnprogrammedSleepNeed(out people, out bedSlots,
                out unservedCouples);
            if (bedSlots < people)
            {
                ThingDef singleBed = ThingDefOf.Bed;
                ThingDef doubleBed = DefDatabase<ThingDef>.GetNamedSilentFail("DoubleBed");
                string bedBlocker = null;
                if (people - bedSlots >= 2 && unservedCouples > 0 && doubleBed != null
                    && TryMakeIndoorPlan(planner, doubleBed, CAHomePlanKind.Bed,
                        "a resident couple still lacks shared sleeping space",
                        null, out plan, out bedBlocker))
                    return CAHomeSelection.Planned;
                if (TryMakeIndoorPlan(planner, singleBed, CAHomePlanKind.Bed,
                    "the colony has " + bedSlots + " sleeping place"
                        + (bedSlots == 1 ? "" : "s") + " for " + people + " people",
                    null, out plan, out blocker))
                    return CAHomeSelection.Planned;
                blocker = "sleeping space is missing, but no valid provision fits a claimed enclosed placement"
                    + BlockerSuffix(blocker ?? bedBlocker);
                return CAHomeSelection.Blocked;
            }

            if (CountBuildsAndPlans(def => def != null && def.IsTable) == 0)
            {
                CASpaceProgram tableProgram = PreferredProgramFor(
                    CAHomePlanKind.Table);
                // A Barracks admits a table as a compatible fallback; it is not
                // a dining designation. Prefer a valid unprogrammed room before
                // consuming its sleeping and circulation field.
                if (tableProgram?.purpose == CASpacePurpose.Barracks
                    && TryMakeTablePlanForProgram(planner, people, null,
                        out plan, out blocker))
                    return CAHomeSelection.Planned;
                if (TryMakeTablePlanForProgram(planner, people, tableProgram,
                    out plan, out blocker)) return CAHomeSelection.Planned;
                blocker = "an eating surface is missing, but no valid provision fits a claimed enclosed placement"
                    + BlockerSuffix(blocker);
                return CAHomeSelection.Blocked;
            }

            int seats = CountTableSeatsAndPlans();
            int proactiveTarget = Mathf.Min(people, 2);
            if (seats < proactiveTarget)
            {
                if (TryMakeSeatPlan(planner, ThingDefOf.Stool,
                    "the eating area has only " + seats + " usable seat"
                        + (seats == 1 ? "" : "s"), out plan, out blocker))
                    return CAHomeSelection.Planned;
                blocker = "minimal table seating is missing, but no valid provision fits an adjacent claimed placement"
                    + BlockerSuffix(blocker);
                return CAHomeSelection.Blocked;
            }

            return CAHomeSelection.None;
        }

        private bool TryMakeTablePlanForProgram(Pawn planner, int people,
            CASpaceProgram program, out CAHomePlan plan, out string blocker)
        {
            ThingDef preferred = people >= 5
                ? ThingDefOf.Table2x2c : ThingDefOf.Table1x2c;
            if (TryMakeIndoorPlan(planner, preferred, CAHomePlanKind.Table,
                "the colony has nowhere to eat together", null, program,
                out plan, out blocker)) return true;
            if (preferred != ThingDefOf.Table1x2c
                && TryMakeIndoorPlan(planner, ThingDefOf.Table1x2c,
                    CAHomePlanKind.Table,
                    "the colony has nowhere to eat together", null, program,
                    out plan, out blocker)) return true;
            return false;
        }

        private bool TryMakeProgramSleepPlan(Pawn planner,
            CASpaceProgram program, int currentSlots, int targetOccupants,
            out CAHomePlan plan, out string blocker)
        {
            plan = default(CAHomePlan);
            blocker = null;
            ThingDef singleBed = ThingDefOf.Bed;
            ThingDef singleSpot = ThingDefOf.SleepingSpot;
            var candidates = new List<ThingDef>();

            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            Pawn sharedFirst = null;
            Pawn sharedSecond = null;
            bool sharedBedroom = currentSlots == 0 && targetOccupants == 2
                && program.style != CASpaceStyle.Austere
                && programs != null
                && programs.TryGetShareableBedroomPair(program,
                    out sharedFirst, out sharedSecond);

            if (program.style == CASpaceStyle.Austere)
            {
                if (singleSpot != null) candidates.Add(singleSpot);
            }
            else
            {
                if (sharedBedroom)
                {
                    ThingDef doubleBed = DefDatabase<ThingDef>
                        .GetNamedSilentFail("DoubleBed");
                    if (doubleBed != null) candidates.Add(doubleBed);
                }
                if (singleBed != null) candidates.Add(singleBed);
                if (singleSpot != null) candidates.Add(singleSpot);
            }

            string reason = sharedBedroom
                ? program.label + " has no sleeping provision for share-willing "
                    + sharedFirst.LabelShort + " and " + sharedSecond.LabelShort
                : program.label + " provides " + currentSlots
                    + " of " + targetOccupants
                    + " currently required sleeping places"
                    + " (maximum occupancy " + program.maxOccupants + ")";
            string lastBlocker = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                ThingDef candidate = candidates[i];
                if (candidate == null) continue;
                if (TryMakeIndoorPlan(planner, candidate, CAHomePlanKind.Bed,
                    reason, null, program, out plan, out lastBlocker))
                    return true;
            }
            blocker = lastBlocker;
            return false;
        }

        private bool TrySelectAutonomousPlan(
            Pawn planner, out CAHomePlan plan, out string blocker)
        {
            plan = default(CAHomePlan);
            blocker = null;
            int people = RestingColonistCount();
            Building joyNeedsSeat = FindJoyBuildingNeedingSeat();
            if (joyNeedsSeat != null)
            {
                if (TryMakeSeatBeside(planner, ThingDefOf.DiningChair, joyNeedsSeat,
                    joyNeedsSeat.LabelCap + " needs a seat before anyone can use it",
                    out plan, out blocker))
                    return true;
                if (TryMakeSeatBeside(planner, ThingDefOf.Stool, joyNeedsSeat,
                    joyNeedsSeat.LabelCap + " needs a seat before anyone can use it",
                    out plan, out blocker))
                    return true;
            }

            int seats = CountTableSeatsAndPlans();
            int autonomousTarget = Mathf.Min(people, 6);
            if (seats < autonomousTarget)
            {
                ThingDef chair = ThingDefOf.DiningChair;
                if (TryMakeSeatPlan(planner, chair,
                    "the household can seat only " + seats + " of " + people + " people",
                    out plan, out blocker))
                    return true;
                if (TryMakeSeatPlan(planner, ThingDefOf.Stool,
                    "the household can seat only " + seats + " of " + people + " people",
                    out plan, out blocker))
                    return true;
            }

            Room darkRoom = FindFurnishedRoomNeedingLightFixture();
            if (darkRoom != null)
            {
                string lightReason = "a furnished claimed room has no light fixture";
                CASpaceProgram lightProgram = ProgramInRoom(darkRoom);
                if (TryMakeIndoorPlan(planner, ThingDefOf.StandingLamp,
                    CAHomePlanKind.Light, lightReason,
                    room => room == darkRoom, lightProgram,
                    out plan, out blocker))
                    return true;
                if (TryMakeIndoorPlan(planner, ThingDefOf.TorchLamp,
                    CAHomePlanKind.Light, lightReason,
                    room => room == darkRoom, lightProgram,
                    out plan, out blocker))
                    return true;
            }

            if (CountUsableRecreationAndPlans() == 0)
            {
                ThingDef horseshoes = DefDatabase<ThingDef>.GetNamedSilentFail("HorseshoesPin");
                ThingDef chess = DefDatabase<ThingDef>.GetNamedSilentFail("ChessTable");
                int physical = SkillLevel(planner, SkillDefOf.Shooting)
                    + SkillLevel(planner, SkillDefOf.Melee);
                int reflective = SkillLevel(planner, SkillDefOf.Intellectual)
                    + SkillLevel(planner, SkillDefOf.Artistic);
                CASpaceProgram recreationProgram = PreferredProgramFor(
                    CAHomePlanKind.Recreation);

                if (physical > reflective && horseshoes != null)
                {
                    if (TryMakeOutdoorPlan(planner, horseshoes,
                        "the household lacks recreation and " + planner.LabelShort
                            + " favors a physical game", recreationProgram,
                        out plan, out blocker))
                        return true;
                    if (chess != null && TryMakeIndoorPlan(planner, chess,
                        CAHomePlanKind.Recreation,
                        "the household lacks recreation", null,
                        recreationProgram,
                        out plan, out blocker))
                        return true;
                }
                else
                {
                    if (chess != null && TryMakeIndoorPlan(planner, chess,
                        CAHomePlanKind.Recreation,
                        "the household lacks recreation and " + planner.LabelShort
                            + " favors a thinking game", null, recreationProgram,
                        out plan, out blocker))
                        return true;
                    if (horseshoes != null && TryMakeOutdoorPlan(planner, horseshoes,
                        "the household lacks recreation", recreationProgram,
                        out plan, out blocker))
                        return true;
                }
            }

            return false;
        }

        private bool TryMakeIndoorPlan(Pawn planner, ThingDef def,
            CAHomePlanKind kind, string reason, Predicate<Room> roomFilter,
            out CAHomePlan plan, out string blocker)
        {
            return TryMakeIndoorPlan(planner, def, kind, reason, roomFilter,
                null, out plan, out blocker);
        }

        private bool TryMakeIndoorPlan(Pawn planner, ThingDef def,
            CAHomePlanKind kind, string reason, Predicate<Room> roomFilter,
            CASpaceProgram program, out CAHomePlan plan, out string blocker)
        {
            plan = default(CAHomePlan);
            if (NeedIsSuppressed(kind, out blocker)) return false;
            ThingDef stuff;
            if (!TryResolveBuild(planner, def, out stuff, out blocker)) return false;

            IntVec3 cell;
            Rot4 rotation;
            string placementEvidence;
            if (!TryFindIndoorPlacement(planner, def, stuff, kind, roomFilter,
                program, out cell, out rotation, out placementEvidence))
            {
                blocker = "no valid cell in a claimed enclosed room";
                return false;
            }

            plan = new CAHomePlan
            {
                def = def,
                stuff = stuff,
                cell = cell,
                rotation = rotation,
                reason = reason,
                kind = kind,
                program = program,
                placementEvidence = placementEvidence
            };
            return true;
        }

        private bool TryMakeSeatPlan(Pawn planner, ThingDef def, string reason,
            out CAHomePlan plan, out string blocker)
        {
            plan = default(CAHomePlan);
            if (NeedIsSuppressed(CAHomePlanKind.Seat, out blocker)) return false;
            ThingDef stuff;
            if (!TryResolveBuild(planner, def, out stuff, out blocker)) return false;

            IntVec3 cell;
            Rot4 rotation;
            if (!TryFindSeatPlacement(planner, def, stuff, out cell, out rotation))
            {
                blocker = "no valid table-adjacent cell";
                return false;
            }
            plan = new CAHomePlan
            {
                def = def,
                stuff = stuff,
                cell = cell,
                rotation = rotation,
                reason = reason,
                kind = CAHomePlanKind.Seat,
                program = PlannedUseMapComponent.For(map)?.ProgramAt(cell)
            };
            return true;
        }

        private bool TryMakeSeatBeside(Pawn planner, ThingDef def, Building target,
            string reason, out CAHomePlan plan, out string blocker)
        {
            plan = default(CAHomePlan);
            if (NeedIsSuppressed(CAHomePlanKind.Seat, out blocker)) return false;
            ThingDef stuff;
            if (!TryResolveBuild(planner, def, out stuff, out blocker)) return false;
            IntVec3 cell;
            Rot4 rotation;
            if (!TryFindSeatBeside(planner, def, stuff, target, out cell, out rotation))
            {
                blocker = "no valid cell beside " + target.LabelShort;
                return false;
            }
            plan = new CAHomePlan
            {
                def = def,
                stuff = stuff,
                cell = cell,
                rotation = rotation,
                reason = reason,
                kind = CAHomePlanKind.Seat,
                program = PlannedUseMapComponent.For(map)?.ProgramAt(cell),
                seatForJoy = true
            };
            return true;
        }

        private bool TryMakeOutdoorPlan(Pawn planner, ThingDef def, string reason,
            CASpaceProgram program, out CAHomePlan plan, out string blocker)
        {
            plan = default(CAHomePlan);
            if (NeedIsSuppressed(CAHomePlanKind.Recreation, out blocker)) return false;
            ThingDef stuff;
            if (!TryResolveBuild(planner, def, out stuff, out blocker)) return false;
            IntVec3 cell;
            Rot4 rotation;
            if (!TryFindOutdoorPlacement(planner, def, stuff, program,
                out cell, out rotation))
            {
                blocker = "no valid unroofed cell in the claimed home area";
                return false;
            }
            plan = new CAHomePlan
            {
                def = def,
                stuff = stuff,
                cell = cell,
                rotation = rotation,
                reason = reason,
                kind = CAHomePlanKind.Recreation,
                program = program,
                outdoor = true
            };
            return true;
        }

        private bool NeedIsSuppressed(CAHomePlanKind kind, out string blocker)
        {
            blocker = null;
            int now = Find.TickManager.TicksGame;
            string key = kind.ToString();
            int until;
            if (!suppressedKinds.TryGetValue(key, out until)) return false;
            if (until != int.MaxValue && now >= until)
            {
                suppressedKinds.Remove(key);
                return false;
            }
            blocker = kind.ToString().ToLowerInvariant() + " planning is vetoed"
                + (until == int.MaxValue ? " until Home planning is reset"
                    : " for " + (until - now) + " more ticks");
            return true;
        }

        private bool TryResolveBuild(Pawn planner, ThingDef def, out ThingDef stuff,
            out string blocker)
        {
            stuff = null;
            blocker = null;
            if (def == null || def.blueprintDef == null)
            {
                blocker = "building definition is unavailable";
                return false;
            }
            if (!def.IsResearchFinished)
            {
                blocker = def.label + " research is unfinished";
                return false;
            }
            TechLevel tech = Faction.OfPlayer.def.techLevel;
            if ((def.minTechLevelToBuild != TechLevel.Undefined
                    && tech < def.minTechLevelToBuild)
                || (def.maxTechLevelToBuild != TechLevel.Undefined
                    && tech > def.maxTechLevelToBuild))
            {
                blocker = def.label + " is outside the colony's technology";
                return false;
            }
            if (!HasCapableBuilder(def))
            {
                blocker = "no enabled constructor meets " + def.label
                    + "'s skill requirement";
                return false;
            }

            if (!def.MadeFromStuff)
            {
                return true;
            }

            ThingDef defaultStuff = GenStuff.DefaultStuffFor(def);
            if (defaultStuff != null && HasResourcesFor(planner, def, defaultStuff))
            {
                stuff = defaultStuff;
                return true;
            }

            // Follow the same suggestion boundary as RimWorld's build
            // designator: currency and prestige stuffs are valid manual choices,
            // but are not unattended substitutes for practical construction.
            // When the native wood default can be obtained through the governed
            // forestry prerequisite, retain that objective instead of consuming
            // a merely abundant alternative.
            if (defaultStuff == ThingDefOf.WoodLog
                && CAHomePrerequisiteMapComponent.For(map)
                    ?.HasAuthorizedWoodSupply() == true)
            {
                stuff = defaultStuff;
                return true;
            }

            ThingDef best = null;
            float bestSuitability = float.MinValue;
            foreach (ThingDef candidate in GenStuff.AllowedStuffsFor(def))
            {
                if (candidate?.stuffProps == null
                    || !candidate.stuffProps.canSuggestUseDefaultStuff
                    || !HasResourcesFor(planner, def, candidate)) continue;
                float suitability = candidate.stuffProps.commonality * 100f
                    - candidate.BaseMarketValue;
                if (best == null || suitability > bestSuitability
                    || (Mathf.Approximately(suitability, bestSuitability)
                        && string.CompareOrdinal(candidate.defName,
                            best.defName) < 0))
                {
                    best = candidate;
                    bestSuitability = suitability;
                }
            }
            if (best != null)
            {
                stuff = best;
                return true;
            }

            // Placement is solved before acquisition. A material deficit becomes
            // a saved prerequisite request rather than erasing the originating
            // construction objective.
            if (defaultStuff != null)
            {
                stuff = defaultStuff;
                return true;
            }
            foreach (ThingDef candidate in GenStuff.AllowedStuffsFor(def))
            {
                stuff = candidate;
                return true;
            }
            blocker = def.label + " has no permitted construction material";
            return false;
        }

        private bool HasResourcesFor(Pawn planner, ThingDef def, ThingDef stuff)
        {
            if (planner == null || planner.Map != map) return false;
            List<ThingDefCountClass> costs = def.CostListAdjusted(stuff,
                errorOnNullStuff: false);
            for (int i = 0; i < costs.Count; i++)
            {
                ThingDefCountClass cost = costs[i];
                if (cost.thingDef == null
                    || AvailableCountFor(planner, cost.thingDef, cost.count)
                        < cost.count)
                    return false;
            }
            return true;
        }

        private int AvailableCountFor(Pawn planner, ThingDef def, int stopAt)
        {
            if (planner == null || def == null) return 0;
            int count = 0;
            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || !thing.Spawned || thing.IsForbidden(planner))
                    continue;
                count += thing.stackCount;
                if (count >= stopAt) break;
            }
            return count;
        }

        private bool HasCapableBuilder(ThingDef def)
        {
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Downed || pawn.Drafted
                    || pawn.InMentalState || !pawn.Awake() || pawn.workSettings == null
                    || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Construction))
                    continue;
                if (SkillLevel(pawn, SkillDefOf.Construction)
                    >= def.constructionSkillPrerequisite)
                    return true;
            }
            return false;
        }

        private bool CanUsePlacement(Pawn planner, ThingDef def, ThingDef stuff,
            IntVec3 cell, Rot4 rot)
        {
            if (planner == null
                || !cell.InAllowedArea(planner)
                || !planner.CanReach(cell, PathEndMode.Touch, Danger.Some))
                return false;

            if (def == ThingDefOf.StandingLamp
                && map.powerNetGrid.TransmittedPowerNetAt(cell) == null)
                return false;

            bool reachableBuilder = false;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Downed || pawn.Drafted
                    || pawn.InMentalState || !pawn.Awake() || pawn.workSettings == null
                    || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Construction)
                    || SkillLevel(pawn, SkillDefOf.Construction)
                        < def.constructionSkillPrerequisite)
                    continue;
                if (cell.InAllowedArea(pawn)
                    && pawn.CanReach(cell, PathEndMode.Touch, Danger.Some))
                {
                    reachableBuilder = true;
                    break;
                }
            }
            if (!reachableBuilder) return false;

            foreach (IntVec3 occupied in GenAdj.OccupiedRect(cell, rot, def.Size))
            {
                List<Thing> things = occupied.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing existing = things[i];
                    if (!(existing is Building) && !(existing is Blueprint)
                        && !(existing is Frame))
                        continue;
                    if (GenConstruct.CanReplace(def, existing.def, stuff,
                        existing.Stuff))
                        return false;
                }
            }
            return true;
        }

        private bool TryFindIndoorPlacement(Pawn planner, ThingDef def, ThingDef stuff,
            CAHomePlanKind kind, Predicate<Room> roomFilter,
            CASpaceProgram program,
            out IntVec3 bestCell, out Rot4 bestRot,
            out string placementEvidence)
        {
            bestCell = IntVec3.Invalid;
            bestRot = Rot4.Invalid;
            placementEvidence = null;
            float bestScore = float.MinValue;
            float bestBaseScore = float.MinValue;
            CASettlementPlacementEvidence bestContextEvidence =
                default(CASettlementPlacementEvidence);
            CABedFengShuiEvidence bestBedFengShui =
                default(CABedFengShuiEvidence);
            IntVec3 baselineCell = IntVec3.Invalid;
            Rot4 baselineRot = Rot4.Invalid;
            float baselineScore = float.MinValue;
            CASettlementPlacementEvidence baselineContextEvidence =
                default(CASettlementPlacementEvidence);
            IntVec3 regularCell = IntVec3.Invalid;
            Rot4 regularRot = Rot4.Invalid;
            float regularCombinedScore = float.MinValue;
            float regularBaseScore = float.MinValue;
            CASettlementPlacementEvidence regularContextEvidence =
                default(CASettlementPlacementEvidence);
            CABedFengShuiEvidence regularBedFengShui =
                default(CABedFengShuiEvidence);
            CASettlementPlanningContextMapComponent settlementContext =
                kind == CAHomePlanKind.Bed
                    && program != null
                    && program.author == CASpaceAuthor.Player
                    && CABehaviorGate.StableProfileAllows(planner,
                        "spatial.home_comfort")
                ? CASettlementPlanningContextMapComponent.For(map) : null;
            settlementContext?.Refresh();
            for (int pass = 0; pass < 2; pass++)
            {
                IReadOnlyList<Room> rooms = map.regionGrid.AllRooms;
                for (int i = 0; i < rooms.Count; i++)
                {
                    Room room = rooms[i];
                    if (!EligibleHomeRoom(room)
                        || (kind == CAHomePlanKind.Bed
                            && (!BedRoomIsCompatible(room)
                                || RoomShortSpan(room) < 3
                                || RoomDoorCount(room) >= 3))
                        || (roomFilter != null && !roomFilter(room)))
                        continue;
                    IntVec3 center = RoomCenter(room);
                    float roomScore = ScoreRoom(room, kind);
                    int stride = pass == 0
                        ? Mathf.Max(1, room.CellCount / 320) : 1;
                    int visited = 0;
                    foreach (IntVec3 cell in room.Cells)
                    {
                        if (visited++ % stride != 0) continue;
                        foreach (Rot4 rot in CandidateRotations(def))
                        {
                            if (!FootprintInsideClaimedRoom(def, cell, rot, room)) continue;
                            if (!PlacementMatchesSpaceProgram(
                                def, cell, rot, kind, program)) continue;
                            if (kind == CAHomePlanKind.Bed
                                && !BedPlacementHasClearance(def, cell, rot,
                                    room, program))
                                continue;
                            if (kind == CAHomePlanKind.Table
                                && !TablePlacementHasClearance(def, cell, rot,
                                    room, program)) continue;
                            if (!CanUsePlacement(planner, def, stuff, cell, rot)) continue;
                            if (!GenConstruct.CanPlaceBlueprintAt(def, cell, rot, map,
                                godMode: false, null, null, stuff).Accepted) continue;
                            CABedFengShuiEvidence bedFengShui;
                            float baseScore = roomScore
                                + ScoreCell(def, cell, rot, center, room, kind,
                                    program, out bedFengShui)
                                + StableTie(cell, rot);
                            CASettlementPlacementEvidence contextEvidence =
                                settlementContext != null
                                ? settlementContext.ScoreSleepPlacement(planner,
                                    program, room, cell, rot)
                                : default(CASettlementPlacementEvidence);
                            float score = baseScore + contextEvidence.Total;
                            if (!baselineCell.IsValid || baseScore > baselineScore)
                            {
                                baselineCell = cell;
                                baselineRot = rot;
                                baselineScore = baseScore;
                                baselineContextEvidence = contextEvidence;
                            }
                            if (!bestCell.IsValid || score > bestScore)
                            {
                                bestCell = cell;
                                bestRot = rot;
                                bestScore = score;
                                bestBaseScore = baseScore;
                                bestContextEvidence = contextEvidence;
                                bestBedFengShui = bedFengShui;
                            }
                            if (kind == CAHomePlanKind.Bed
                                && bedFengShui.existingBeds > 0
                                && (!regularCell.IsValid
                                    || BedPatternOutranks(bedFengShui,
                                        regularBedFengShui)
                                    || (!BedPatternOutranks(regularBedFengShui,
                                            bedFengShui)
                                        && score > regularCombinedScore)))
                            {
                                regularCell = cell;
                                regularRot = rot;
                                regularCombinedScore = score;
                                regularBaseScore = baseScore;
                                regularContextEvidence = contextEvidence;
                                regularBedFengShui = bedFengShui;
                            }
                        }
                    }
                }
                if (bestCell.IsValid || pass == 1) break;
            }
            bool regularityEnforced = false;
            string actionableDeparture = null;
            if (kind == CAHomePlanKind.Bed && bestCell.IsValid
                && regularCell.IsValid
                && BedPatternOutranks(regularBedFengShui,
                    bestBedFengShui))
            {
                if (!TryGetActionableBedDeparture(program, bestCell,
                    out actionableDeparture))
                {
                    bestCell = regularCell;
                    bestRot = regularRot;
                    bestScore = regularCombinedScore;
                    bestBaseScore = regularBaseScore;
                    bestContextEvidence = regularContextEvidence;
                    bestBedFengShui = regularBedFengShui;
                    regularityEnforced = true;
                }
            }

            string fengShuiEvidence = null;
            if (bestCell.IsValid && kind == CAHomePlanKind.Bed)
                fengShuiEvidence = BedFengShuiSummary(bestBedFengShui,
                    regularityEnforced, actionableDeparture)
                    + " [" + bestBedFengShui.Receipt() + "]";
            else if (bestCell.IsValid && kind == CAHomePlanKind.Table)
                fengShuiEvidence = TableFengShuiReceipt(def, bestCell,
                    bestRot, bestCell.GetRoom(map), program);

            if (bestCell.IsValid && settlementContext != null)
            {
                bool changed = bestCell != baselineCell
                    || bestRot != baselineRot;
                string contextEvidence = "settlement context revision "
                    + settlementContext.Revision
                    + " re-ranked Autonomous Bed candidates inside player-authored "
                    + program.label + ": pre-context winner " + baselineCell
                    + " facing " + baselineRot + " score "
                    + baselineScore.ToString("F2") + " with context ["
                    + baselineContextEvidence.Receipt() + "]; selected "
                    + bestCell + " facing " + bestRot + " pre-context "
                    + bestBaseScore.ToString("F2") + ", combined "
                    + bestScore.ToString("F2") + " ["
                    + bestContextEvidence.Receipt() + "]; ranking changed "
                    + (changed ? "yes" : "no")
                    + "; completed construction and the authored program's "
                    + "footprint and parameters were not changed by re-ranking";
                placementEvidence = fengShuiEvidence.NullOrEmpty()
                    ? contextEvidence : fengShuiEvidence + "; "
                        + contextEvidence;
            }
            else if (bestCell.IsValid)
                placementEvidence = fengShuiEvidence;
            return bestCell.IsValid;
        }

        private bool TryFindSeatPlacement(Pawn planner, ThingDef def, ThingDef stuff,
            out IntVec3 bestCell, out Rot4 bestRot)
        {
            bestCell = IntVec3.Invalid;
            bestRot = Rot4.Invalid;
            float bestScore = float.MinValue;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building table = buildings[i];
                if (!table.def.IsTable || !ThingInsideClaimedShelter(table)
                    || !AnyResidentCanReach(table)) continue;
                Room room = table.GetRoom();
                if (!EligibleHomeRoom(room)) continue;
                CASpaceProgram program = PlannedUseMapComponent.For(map)
                    ?.ProgramAt(table.Position);
                if (program != null
                    && !CASpacePurposeInfo.Allows(program,
                        CAHomePlanKind.Seat)) continue;
                CellRect ring = table.OccupiedRect().ExpandedBy(1);
                foreach (IntVec3 cell in ring.EdgeCells)
                {
                    if (ring.IsCorner(cell) || !cell.InBounds(map)
                        || !map.areaManager.Home[cell] || cell.GetRoom(map) != room)
                        continue;
                    Rot4 rot = cell.x == ring.minX ? Rot4.East
                        : cell.x == ring.maxX ? Rot4.West
                        : cell.z == ring.minZ ? Rot4.North : Rot4.South;
                    if (!CellMatchesSpaceProgram(
                            CAHomePlanKind.Seat, cell, program)
                        || !SeatPreservesSleepCirculation(cell, room, program)
                        || !CanUsePlacement(planner, def, stuff, cell, rot)
                        || !GenConstruct.CanPlaceBlueprintAt(def, cell, rot, map,
                        godMode: false, null, null, stuff).Accepted) continue;
                    float score = 20f
                        + (program == null ? 12f
                            : ProgramPriority(program.purpose,
                                CAHomePlanKind.Seat))
                        - DistanceToNearestDoor(cell, 4)
                        + StableTie(cell, rot);
                    if (!bestCell.IsValid || score > bestScore)
                    {
                        bestCell = cell;
                        bestRot = rot;
                        bestScore = score;
                    }
                }
            }
            return bestCell.IsValid;
        }

        private bool TryFindSeatBeside(Pawn planner, ThingDef def, ThingDef stuff,
            Building target, out IntVec3 bestCell, out Rot4 bestRot)
        {
            bestCell = IntVec3.Invalid;
            bestRot = Rot4.Invalid;
            CASpaceProgram program = PlannedUseMapComponent.For(map)
                ?.ProgramAt(target.Position);
            if (program != null
                && !CASpacePurposeInfo.Allows(program,
                    CAHomePlanKind.Seat)) return false;
            List<IntVec3> cells = GenAdjFast.AdjacentCellsCardinal(target);
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                if (!cell.InBounds(map) || !map.areaManager.Home[cell]
                    || cell.GetRoom(map) != target.GetRoom()) continue;
                Rot4 rot = cell.x < target.Position.x ? Rot4.East
                    : cell.x > target.Position.x ? Rot4.West
                    : cell.z < target.Position.z ? Rot4.North : Rot4.South;
                if (!CellMatchesSpaceProgram(
                        CAHomePlanKind.Seat, cell, program)
                    || !SeatPreservesSleepCirculation(cell, target.GetRoom(),
                        program)
                    || !CanUsePlacement(planner, def, stuff, cell, rot)
                    || !GenConstruct.CanPlaceBlueprintAt(def, cell, rot, map,
                    godMode: false, null, null, stuff).Accepted) continue;
                bestCell = cell;
                bestRot = rot;
                return true;
            }
            return false;
        }

        private bool TryFindOutdoorPlacement(Pawn planner, ThingDef def,
            ThingDef stuff, CASpaceProgram program,
            out IntVec3 bestCell, out Rot4 bestRot)
        {
            bestCell = IntVec3.Invalid;
            bestRot = def.defaultPlacingRot;
            float bestScore = float.MinValue;
            foreach (IntVec3 cell in map.areaManager.Home.ActiveCells)
            {
                if (!cell.InBounds(map) || cell.Fogged(map) || cell.Roofed(map)
                    || cell.CloseToEdge(map, 5)) continue;
                Rot4 rot = def.defaultPlacingRot;
                if (!PlacementMatchesSpaceProgram(def, cell, rot,
                        CAHomePlanKind.Recreation, program)
                    || !CanUsePlacement(planner, def, stuff, cell, rot)
                    || !GenConstruct.CanPlaceBlueprintAt(def, cell, rot, map,
                    godMode: false, null, null, stuff).Accepted) continue;
                float score = -cell.DistanceToSquared(planner.Position) * 0.01f
                    - DistanceToNearestDoor(cell, 3) + StableTie(cell, rot);
                if (!bestCell.IsValid || score > bestScore)
                {
                    bestCell = cell;
                    bestScore = score;
                }
            }
            return bestCell.IsValid;
        }

        private bool EligibleHomeRoom(Room room)
        {
            if (room == null || !room.ProperRoom || room.IsDoorway || room.IsPrisonCell
                || room.Fogged || room.CellCount < 6)
                return false;
            foreach (IntVec3 cell in room.Cells)
            {
                if (map.areaManager.Home[cell]) return true;
            }
            return false;
        }

        private bool FootprintInsideClaimedRoom(ThingDef def, IntVec3 center,
            Rot4 rot, Room room)
        {
            foreach (IntVec3 cell in GenAdj.OccupiedRect(center, rot, def.Size))
            {
                if (!cell.InBounds(map) || !map.areaManager.Home[cell]
                    || cell.GetRoom(map) != room || !cell.Roofed(map))
                    return false;
            }
            return true;
        }

        private IEnumerable<Rot4> CandidateRotations(ThingDef def)
        {
            if (!def.rotatable)
            {
                yield return def.defaultPlacingRot;
                yield break;
            }
            yield return Rot4.North;
            yield return Rot4.East;
            yield return Rot4.South;
            yield return Rot4.West;
        }

        private bool PlacementMatchesSpaceProgram(ThingDef def, IntVec3 cell,
            Rot4 rotation, CAHomePlanKind kind, CASpaceProgram program)
        {
            return PlannedUseMapComponent.For(map)?.PlacementMatches(
                program, def, cell, rotation, kind) != false;
        }

        private bool CellMatchesSpaceProgram(CAHomePlanKind kind,
            IntVec3 cell, CASpaceProgram program)
        {
            return PlannedUseMapComponent.For(map)?.CellMatches(
                program, cell, kind) != false;
        }

        private CASpaceProgram FindSleepProgramNeedingCapacity(
            out int currentSlots, out int targetOccupants)
        {
            currentSlots = 0;
            targetOccupants = 0;
            Dictionary<int, int> targets;
            List<CASpaceProgram> sleepPrograms = CurrentSleepProgramTargets(
                out targets);
            if (sleepPrograms.Count == 0) return null;

            CASpaceProgram best = null;
            int bestSlots = 0;
            int bestTarget = 0;
            int bestMissing = 0;
            for (int i = 0; i < sleepPrograms.Count; i++)
            {
                CASpaceProgram program = sleepPrograms[i];
                if (program.purpose == CASpacePurpose.Bedroom)
                    continue;
                int target = targets[program.id];
                int slots = CountBedSlotsInProgram(program);
                int missing = target - slots;
                if (missing <= 0) continue;
                if (best == null || missing > bestMissing
                    || (missing == bestMissing && program.id < best.id))
                {
                    best = program;
                    bestSlots = slots;
                    bestTarget = target;
                    bestMissing = missing;
                }
            }
            currentSlots = bestSlots;
            targetOccupants = bestTarget;
            return best;
        }

        private List<CASpaceProgram> CurrentSleepProgramTargets(
            out Dictionary<int, int> targets)
        {
            targets = new Dictionary<int, int>();
            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            if (component == null) return new List<CASpaceProgram>();
            var sleepPrograms = new List<CASpaceProgram>();
            foreach (CASpaceProgram program in component.Programs)
                if (program != null && program.cells.Count > 0
                    && CASpacePurposeInfo.CanRequireSleep(program.purpose)
                    && program.RequiresSleep)
                    sleepPrograms.Add(program);
            sleepPrograms.Sort((left, right) => left.id.CompareTo(right.id));
            var explicitResidents = new HashSet<Pawn>();
            for (int i = 0; i < sleepPrograms.Count; i++)
            {
                CASpaceProgram program = sleepPrograms[i];
                int residentTarget = component.ResidentCount(program,
                    spawnedOnly: true);
                targets[program.id] = Mathf.Min(program.maxOccupants,
                    residentTarget);
                if (program.residents == null) continue;
                for (int r = 0; r < program.residents.Count; r++)
                {
                    Pawn resident = program.residents[r];
                    if (resident != null && resident.Spawned
                        && resident.Map == map && resident.needs?.rest != null
                        && !resident.DevelopmentalStage.Baby())
                        explicitResidents.Add(resident);
                }
            }
            int remaining = Mathf.Max(0, RestingColonistCount()
                - explicitResidents.Count
                - CountBedSlotsOutsidePrograms(sleepPrograms,
                    explicitResidents));
            while (remaining > 0)
            {
                bool assigned = false;
                for (int i = 0; i < sleepPrograms.Count && remaining > 0; i++)
                {
                    CASpaceProgram program = sleepPrograms[i];
                    if (program.HasResidentRoster
                        || program.purpose == CASpacePurpose.Bedroom)
                        continue;
                    int target = targets[program.id];
                    if (target >= program.maxOccupants) continue;
                    targets[program.id] = target + 1;
                    remaining--;
                    assigned = true;
                }
                if (!assigned) break;
            }
            return sleepPrograms;
        }

        private CASpaceProgram PreferredProgramFor(CAHomePlanKind kind)
        {
            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            if (component == null) return null;
            CASpaceProgram best = null;
            int bestPriority = int.MinValue;
            foreach (CASpaceProgram program in component.Programs)
            {
                if (program == null || program.cells.Count == 0
                    || !CASpacePurposeInfo.Allows(program, kind)) continue;
                int priority = ProgramPriority(program.purpose, kind);
                if (best == null || priority > bestPriority
                    || (priority == bestPriority && program.id < best.id))
                {
                    best = program;
                    bestPriority = priority;
                }
            }
            return best;
        }

        private int ProgramPriority(CASpacePurpose purpose,
            CAHomePlanKind kind)
        {
            if (kind == CAHomePlanKind.Table || kind == CAHomePlanKind.Seat)
            {
                if (purpose == CASpacePurpose.Dining) return 30;
                if (purpose == CASpacePurpose.Kitchen) return 25;
                if (purpose == CASpacePurpose.Recreation) return 15;
                if (purpose == CASpacePurpose.Barracks) return 10;
            }
            if (kind == CAHomePlanKind.Recreation)
            {
                if (purpose == CASpacePurpose.Recreation) return 30;
                if (purpose == CASpacePurpose.Barracks) return 20;
            }
            return 0;
        }

        private CASpaceProgram ProgramInRoom(Room room)
        {
            if (room == null) return null;
            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            if (component == null) return null;
            foreach (IntVec3 cell in room.Cells)
            {
                CASpaceProgram program = component.ProgramAt(cell);
                if (program != null) return program;
            }
            return null;
        }

        private float ScoreRoom(Room room, CAHomePlanKind kind)
        {
            bool hasBed = RoomHas(room, def => def.IsBed);
            bool hasTable = RoomHas(room, def => def.IsTable);
            switch (kind)
            {
                case CAHomePlanKind.Bed:
                    return (hasBed ? 55f : 0f) - Mathf.Abs(room.CellCount - 18) * 0.6f
                        - (hasTable ? 25f : 0f);
                case CAHomePlanKind.Table:
                    return Mathf.Min(room.CellCount, 80) * 0.35f
                        - (hasBed ? 12f : 0f)
                        - DiningConflictsInRoom(room).Count * 45f;
                case CAHomePlanKind.Light:
                    return (hasTable ? 30f : 0f) + (hasBed ? 20f : 0f)
                        + Mathf.Min(room.CellCount, 60) * 0.15f;
                case CAHomePlanKind.Recreation:
                    return (hasTable ? 20f : 0f) + Mathf.Min(room.CellCount, 100) * 0.25f;
                default:
                    return 0f;
            }
        }

        private float ScoreCell(ThingDef def, IntVec3 cell, Rot4 rot,
            IntVec3 roomCenter, Room room, CAHomePlanKind kind,
            CASpaceProgram program, out CABedFengShuiEvidence bedFengShui)
        {
            bedFengShui = default(CABedFengShuiEvidence);
            CellRect rect = GenAdj.OccupiedRect(cell, rot, def.Size);
            int wallEdges = 0;
            int doorPenalty = 0;
            foreach (IntVec3 edge in rect.ExpandedBy(1).EdgeCells)
            {
                if (!edge.InBounds(map)) continue;
                Building edifice = edge.GetEdifice(map);
                if (edifice == null) continue;
                if (edifice is Building_Door) doorPenalty += 5;
                else if (edifice.def.passability == Traversability.Impassable) wallEdges++;
            }
            float centerDistance = Mathf.Sqrt(cell.DistanceToSquared(roomCenter));
            if (kind == CAHomePlanKind.Bed)
            {
                bedFengShui = BedFengShui(def, cell, rot, room, program);
                return bedFengShui.Total
                    - doorPenalty - centerDistance * 0.15f;
            }
            if (kind == CAHomePlanKind.Table
                && program?.purpose == CASpacePurpose.Barracks)
                return -wallEdges * 0.5f - doorPenalty
                    + centerDistance * 0.65f
                    + Mathf.Min(DistanceToNearestBed(cell, room), 8) * 0.60f
                    + Mathf.Min(DistanceToNearestDiningConflict(rect, room,
                        out _), 12) * 1.75f;
            if (kind == CAHomePlanKind.Table)
                return -wallEdges * 1.5f - doorPenalty
                    - centerDistance * 0.35f
                    + Mathf.Min(DistanceToNearestDiningConflict(rect, room,
                        out _), 12) * 1.75f;
            if (kind == CAHomePlanKind.Recreation)
                return -wallEdges * 1.5f - doorPenalty - centerDistance * 0.55f;
            if (kind == CAHomePlanKind.Light)
                return wallEdges * 2f - doorPenalty - centerDistance * 0.30f;
            return -doorPenalty;
        }

        private int RoomShortSpan(Room room)
        {
            if (room == null) return 0;
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minZ = int.MaxValue;
            int maxZ = int.MinValue;
            foreach (IntVec3 cell in room.Cells)
            {
                minX = Math.Min(minX, cell.x);
                maxX = Math.Max(maxX, cell.x);
                minZ = Math.Min(minZ, cell.z);
                maxZ = Math.Max(maxZ, cell.z);
            }
            if (minX == int.MaxValue) return 0;
            return Math.Min(maxX - minX + 1, maxZ - minZ + 1);
        }

        private int RoomDoorCount(Room room)
        {
            if (room == null) return 0;
            HashSet<int> doors = new HashSet<int>();
            foreach (IntVec3 cell in room.Cells)
            {
                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 adjacent = cell + GenAdj.CardinalDirections[i];
                    if (!adjacent.InBounds(map)) continue;
                    Building_Door door = adjacent.GetDoor(map);
                    if (door != null) doors.Add(door.thingIDNumber);
                }
            }
            return doors.Count;
        }

        private bool BedRoomIsCompatible(Room room)
        {
            if (room == null) return false;
            RoomRoleDef role = room.Role;
            if (role == RoomRoleDefOf.PrisonCell
                || role == RoomRoleDefOf.PrisonBarracks
                || role == RoomRoleDefOf.Hospital
                || role == RoomRoleDefOf.Workshop
                || role == RoomRoleDefOf.Laboratory
                || role?.defName == "Kitchen"
                || role?.defName == "Tomb"
                || role?.defName == "Barn"
                || role?.defName == "ContainmentCell")
                return false;

            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                Building building = things[i] as Building;
                if (building == null || building.GetRoom() != room) continue;
                if (building.def.IsWorkTable
                    || building.TryGetComp<CompPowerBattery>() != null
                    || building.TryGetComp<CompPowerPlant>() != null
                    || building is Building_MechCharger
                    || building is Building_Turret)
                    return false;
            }
            return true;
        }

        private bool BedPlacementHasClearance(ThingDef def, IntVec3 cell,
            Rot4 rot, Room room, CASpaceProgram program)
        {
            CellRect rect = GenAdj.OccupiedRect(cell, rot, def.Size);
            HashSet<IntVec3> existingApproaches =
                SleepCirculationCells(room);
            HashSet<IntVec3> doorwayApproaches =
                BedroomDoorwayApproaches(room, program);
            foreach (IntVec3 occupied in rect)
                if (existingApproaches.Contains(occupied)
                    || doorwayApproaches.Contains(occupied)) return false;

            HashSet<IntVec3> requiredApproaches;
            if (!TryGetBedFunctionalApproaches(def, cell, rot, room,
                    program, out requiredApproaches)) return false;
            int clearBefore = BedroomClearNegativeSpace(room, program);
            int occupiedClear = 0;
            foreach (IntVec3 footprintCell in rect)
                if (BedroomCellIsClearNegativeSpace(footprintCell,
                    room, program)) occupiedClear++;
            // The bound is functional rather than proportional to room size:
            // the clear field after construction must still contain every
            // distinct per-slot foot approach and one distinct side approach.
            return clearBefore - occupiedClear >= requiredApproaches.Count;
        }

        private bool TryGetBedFunctionalApproaches(ThingDef def,
            IntVec3 cell, Rot4 rot, Room room, CASpaceProgram program,
            out HashSet<IntVec3> required)
        {
            required = new HashSet<IntVec3>();
            CellRect rect = GenAdj.OccupiedRect(cell, rot, def.Size);
            int slots = BedUtility.GetSleepingSlotsCount(def.Size);
            for (int i = 0; i < slots; i++)
            {
                IntVec3 feet = BedUtility.GetFeetSlotPos(i, cell, rot,
                    def.Size) + rot.FacingCell;
                if (!UsableBedApproach(feet, rect, room)
                    || !BedroomCellIsClearNegativeSpace(feet, room,
                        program)) return false;
                required.Add(feet);
            }

            IntVec3 left = rot.Rotated(
                RotationDirection.Counterclockwise).FacingCell;
            IntVec3 right = rot.Rotated(
                RotationDirection.Clockwise).FacingCell;
            foreach (IntVec3 occupied in rect)
            {
                IntVec3 leftCell = occupied + left;
                if (UsableBedApproach(leftCell, rect, room)
                    && BedroomCellIsClearNegativeSpace(leftCell, room,
                        program) && required.Add(leftCell)) return true;
                IntVec3 rightCell = occupied + right;
                if (UsableBedApproach(rightCell, rect, room)
                    && BedroomCellIsClearNegativeSpace(rightCell, room,
                        program) && required.Add(rightCell)) return true;
            }
            return false;
        }

        private HashSet<IntVec3> BedroomDoorwayApproaches(Room room,
            CASpaceProgram program)
        {
            var approaches = new HashSet<IntVec3>();
            if (room == null) return approaches;
            foreach (IntVec3 cell in room.Cells)
            {
                if (!BedroomAuthorityContains(program, cell)) continue;
                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 adjacent = cell + GenAdj.CardinalDirections[i];
                    if (adjacent.InBounds(map)
                        && adjacent.GetDoor(map) != null)
                    {
                        approaches.Add(cell);
                        break;
                    }
                }
            }
            return approaches;
        }

        private int BedroomClearNegativeSpace(Room room,
            CASpaceProgram program)
        {
            if (room == null) return 0;
            int count = 0;
            foreach (IntVec3 cell in room.Cells)
                if (BedroomCellIsClearNegativeSpace(cell, room, program))
                    count++;
            return count;
        }

        private bool BedroomCellIsClearNegativeSpace(IntVec3 cell,
            Room room, CASpaceProgram program)
        {
            if (!cell.InBounds(map) || cell.GetRoom(map) != room
                || !BedroomAuthorityContains(program, cell)
                || !map.areaManager.Home[cell] || !cell.Roofed(map)
                || !cell.Standable(map)) return false;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Building || things[i] is Blueprint
                    || things[i] is Frame) return false;
            return true;
        }

        private bool BedroomAuthorityContains(CASpaceProgram program,
            IntVec3 cell)
        {
            return program == null
                || PlannedUseMapComponent.For(map)?.ProgramAt(cell) == program;
        }

        private bool TablePlacementHasClearance(ThingDef def, IntVec3 cell,
            Rot4 rot, Room room, CASpaceProgram program)
        {
            CellRect tableRect = GenAdj.OccupiedRect(cell, rot, def.Size);
            List<Building> diningConflicts = DiningConflictsInRoom(room);
            for (int i = 0; i < diningConflicts.Count; i++)
                if (tableRect.ExpandedBy(2).Overlaps(
                    diningConflicts[i].OccupiedRect())) return false;
            var protectedSleepCells = SleepCirculationCells(room);
            foreach (IntVec3 occupied in tableRect)
                if (protectedSleepCells.Contains(occupied)) return false;

            int serviceCells = 0;
            CellRect ring = tableRect.ExpandedBy(1);
            foreach (IntVec3 serviceCell in ring.EdgeCells)
            {
                if (ring.IsCorner(serviceCell)
                    || !ServiceCellAvailable(serviceCell, room, program)
                    || protectedSleepCells.Contains(serviceCell)) continue;
                serviceCells++;
            }
            int requiredServiceCells = def.Size.x * def.Size.z >= 4 ? 4 : 2;
            if (serviceCells < requiredServiceCells) return false;

            if (program?.purpose != CASpacePurpose.Barracks) return true;
            if (tableRect.Contains(RoomCenter(room))) return false;
            List<Building_Bed> beds = BedsInRoom(room);
            for (int i = 0; i < beds.Count; i++)
            {
                Building_Bed bed = beds[i];
                if (tableRect.ExpandedBy(1).Overlaps(bed.OccupiedRect()))
                    return false;
            }
            return true;
        }

        private List<Building> DiningConflictsInRoom(Room room)
        {
            List<Building> cached;
            if (room != null && placementDiningConflictsByRoom.TryGetValue(
                room, out cached)) return cached;
            var conflicts = new List<Building>();
            if (room != null)
            {
                List<Building> buildings =
                    map.listerBuildings.allBuildingsColonist;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    if (building != null && building.Spawned
                        && building.GetRoom() == room
                        && IsDiningConflict(building)) conflicts.Add(building);
                }
                placementDiningConflictsByRoom[room] = conflicts;
            }
            return conflicts;
        }

        private static bool IsDiningConflict(Building building)
        {
            return building is Building_MechCharger
                || building is Building_Turret
                || building.def.IsWorkTable
                || building.TryGetComp<CompPowerBattery>() != null
                || building.TryGetComp<CompPowerPlant>() != null;
        }

        private int DistanceToNearestDiningConflict(CellRect footprint,
            Room room, out Building nearest)
        {
            nearest = null;
            int best = int.MaxValue;
            List<Building> conflicts = DiningConflictsInRoom(room);
            for (int i = 0; i < conflicts.Count; i++)
            {
                Building conflict = conflicts[i];
                int distance = RectDistance(footprint,
                    conflict.OccupiedRect());
                if (distance >= best) continue;
                best = distance;
                nearest = conflict;
            }
            return best == int.MaxValue ? 12 : best;
        }

        private static int RectDistance(CellRect first, CellRect second)
        {
            int dx = first.maxX < second.minX
                ? second.minX - first.maxX
                : second.maxX < first.minX ? first.minX - second.maxX : 0;
            int dz = first.maxZ < second.minZ
                ? second.minZ - first.maxZ
                : second.maxZ < first.minZ ? first.minZ - second.maxZ : 0;
            return dx + dz;
        }

        private int TableServiceCellCount(ThingDef def, IntVec3 cell,
            Rot4 rot, Room room, CASpaceProgram program)
        {
            int count = 0;
            CellRect tableRect = GenAdj.OccupiedRect(cell, rot, def.Size);
            CellRect ring = tableRect.ExpandedBy(1);
            HashSet<IntVec3> protectedSleepCells =
                SleepCirculationCells(room);
            foreach (IntVec3 serviceCell in ring.EdgeCells)
                if (!ring.IsCorner(serviceCell)
                    && ServiceCellAvailable(serviceCell, room, program)
                    && !protectedSleepCells.Contains(serviceCell)) count++;
            return count;
        }

        private string TableFengShuiReceipt(ThingDef def, IntVec3 cell,
            Rot4 rot, Room room, CASpaceProgram program)
        {
            Building nearest;
            int fixtureDistance = DistanceToNearestDiningConflict(
                GenAdj.OccupiedRect(cell, rot, def.Size), room, out nearest);
            int serviceCells = TableServiceCellCount(def, cell, rot, room,
                program);
            string fixture = nearest == null
                ? "no incompatible functional fixture in the room"
                : fixtureDistance + " cells from " + nearest.def.label
                    + " at " + nearest.Position;
            return "Feng Shui: placed dining in "
                + (program == null ? "unprogrammed shelter" : program.label)
                + " with " + serviceCells + " usable service cells and "
                + fixture + "; room conflicts "
                + DiningConflictsInRoom(room).Count;
        }

        private bool ServiceCellAvailable(IntVec3 cell, Room room,
            CASpaceProgram program)
        {
            if (!cell.InBounds(map) || !map.areaManager.Home[cell]
                || cell.GetRoom(map) != room || !cell.Standable(map)
                || !CellMatchesSpaceProgram(CAHomePlanKind.Seat, cell, program))
                return false;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Building || things[i] is Blueprint
                    || things[i] is Frame) return false;
            return true;
        }

        private bool SeatPreservesSleepCirculation(IntVec3 cell, Room room,
            CASpaceProgram program)
        {
            if (program?.purpose != CASpacePurpose.Barracks) return true;
            return cell != RoomCenter(room)
                && !SleepCirculationCells(room).Contains(cell);
        }

        private HashSet<IntVec3> SleepCirculationCells(Room room)
        {
            if (room == null) return new HashSet<IntVec3>();
            HashSet<IntVec3> cached;
            if (placementSleepCirculationByRoom.TryGetValue(room, out cached))
                return cached;
            var cells = new HashSet<IntVec3>();
            List<Building_Bed> beds = BedsInRoom(room);
            for (int i = 0; i < beds.Count; i++)
            {
                Building_Bed bed = beds[i];
                CellRect rect = bed.OccupiedRect();
                int slots = BedUtility.GetSleepingSlotsCount(bed.def.Size);
                for (int slot = 0; slot < slots; slot++)
                {
                    IntVec3 feet = BedUtility.GetFeetSlotPos(slot,
                        bed.Position, bed.Rotation, bed.def.Size);
                    AddSleepCirculationCell(cells,
                        feet + bed.Rotation.FacingCell, rect, room);
                }
                IntVec3 left = bed.Rotation
                    .Rotated(RotationDirection.Counterclockwise).FacingCell;
                IntVec3 right = bed.Rotation
                    .Rotated(RotationDirection.Clockwise).FacingCell;
                foreach (IntVec3 occupied in rect)
                {
                    AddSleepCirculationCell(cells, occupied + left, rect, room);
                    AddSleepCirculationCell(cells, occupied + right, rect, room);
                }
            }
            placementSleepCirculationByRoom[room] = cells;
            return cells;
        }

        private List<Building_Bed> BedsInRoom(Room room)
        {
            List<Building_Bed> cached;
            if (room != null && placementBedsByRoom.TryGetValue(room, out cached))
                return cached;
            var beds = new List<Building_Bed>();
            if (room != null)
            {
                List<Building> buildings = map.listerBuildings.allBuildingsColonist;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building_Bed bed = buildings[i] as Building_Bed;
                    if (bed != null && bed.Spawned && bed.GetRoom() == room)
                        beds.Add(bed);
                }
                placementBedsByRoom[room] = beds;
            }
            return beds;
        }

        private void AddSleepCirculationCell(HashSet<IntVec3> cells,
            IntVec3 cell, CellRect bedRect, Room room)
        {
            if (cell.InBounds(map) && !bedRect.Contains(cell)
                && cell.GetRoom(map) == room && cell.Standable(map))
                cells.Add(cell);
        }

        private int DistanceToNearestBed(IntVec3 cell, Room room)
        {
            int nearest = int.MaxValue;
            List<Building_Bed> beds = BedsInRoom(room);
            for (int i = 0; i < beds.Count; i++)
            {
                Building_Bed bed = beds[i];
                nearest = Math.Min(nearest,
                    Mathf.RoundToInt(cell.DistanceTo(bed.Position)));
            }
            return nearest == int.MaxValue ? 8 : nearest;
        }

        private bool UsableBedApproach(IntVec3 cell, CellRect footprint, Room room)
        {
            return cell.InBounds(map) && !footprint.Contains(cell)
                && map.areaManager.Home[cell] && cell.Roofed(map)
                && cell.GetRoom(map) == room && cell.Standable(map);
        }

        private CABedFengShuiEvidence BedFengShui(ThingDef def,
            IntVec3 cell, Rot4 rot, Room room, CASpaceProgram program)
        {
            var evidence = new CABedFengShuiEvidence();
            List<Building_Bed> beds = BedsInRoom(room);
            evidence.existingBeds = beds.Count;
            evidence.protectedExistingApproaches =
                SleepCirculationCells(room).Count;
            evidence.protectedDoorwayApproaches =
                BedroomDoorwayApproaches(room, program).Count;
            evidence.clearNegativeSpaceBefore = BedroomClearNegativeSpace(
                room, program);
            CellRect candidateRect = GenAdj.OccupiedRect(cell, rot, def.Size);
            int occupiedClear = 0;
            foreach (IntVec3 occupied in candidateRect)
                if (BedroomCellIsClearNegativeSpace(occupied, room, program))
                    occupiedClear++;
            evidence.clearNegativeSpaceAfter = Math.Max(0,
                evidence.clearNegativeSpaceBefore - occupiedClear);
            HashSet<IntVec3> requiredApproaches;
            evidence.requiredNegativeSpace = TryGetBedFunctionalApproaches(
                def, cell, rot, room, program, out requiredApproaches)
                ? requiredApproaches.Count : 0;
            int slots = BedUtility.GetSleepingSlotsCount(def.Size);
            for (int i = 0; i < slots; i++)
            {
                IntVec3 head = BedUtility.GetSleepingSlotPos(i, cell, rot, def.Size);
                IntVec3 behind = head - rot.FacingCell;
                Building edifice = behind.InBounds(map) ? behind.GetEdifice(map) : null;
                if (edifice != null && !(edifice is Building_Door)
                    && edifice.def.passability == Traversability.Impassable)
                    evidence.headWalls++;
            }

            CellRect rect = GenAdj.OccupiedRect(cell, rot, def.Size);
            IntVec3 left = rot.Rotated(RotationDirection.Counterclockwise).FacingCell;
            IntVec3 right = rot.Rotated(RotationDirection.Clockwise).FacingCell;
            foreach (IntVec3 occupied in rect)
            {
                if (UsableBedApproach(occupied + left, rect, room))
                    evidence.openApproaches++;
                if (UsableBedApproach(occupied + right, rect, room))
                    evidence.openApproaches++;
            }

            bool horizontal = rot.IsHorizontal;
            int candidateAlong = horizontal ? cell.z : cell.x;
            int candidateBank = horizontal ? cell.x : cell.z;
            for (int i = 0; i < beds.Count; i++)
            {
                Building_Bed bed = beds[i];
                if (bed.Rotation.IsHorizontal != horizontal) continue;
                int bedAlong = horizontal ? bed.Position.z : bed.Position.x;
                int bedBank = horizontal ? bed.Position.x : bed.Position.z;
                if (bed.Rotation == rot && bedBank == candidateBank)
                {
                    int interval = Math.Abs(candidateAlong - bedAlong);
                    if (interval == 2) evidence.matchingBankIntervals++;
                    else if (interval == 3)
                        evidence.irregularBankIntervals++;
                }
                else if (bed.Rotation == rot.Opposite
                    && bedAlong == candidateAlong)
                {
                    evidence.alignedRows++;
                }
            }
            return evidence;
        }

        private static bool BedPatternOutranks(
            CABedFengShuiEvidence candidate,
            CABedFengShuiEvidence incumbent)
        {
            bool candidateClean = candidate.irregularBankIntervals == 0;
            bool incumbentClean = incumbent.irregularBankIntervals == 0;
            if (candidateClean != incumbentClean) return candidateClean;
            if (Mathf.Abs(candidate.PatternScore - incumbent.PatternScore) > 0.01f)
                return candidate.PatternScore > incumbent.PatternScore;
            if (candidate.irregularBankIntervals
                != incumbent.irregularBankIntervals)
                return candidate.irregularBankIntervals
                    < incumbent.irregularBankIntervals;
            int candidatePatternEvidence = candidate.alignedRows
                + candidate.matchingBankIntervals;
            int incumbentPatternEvidence = incumbent.alignedRows
                + incumbent.matchingBankIntervals;
            return candidatePatternEvidence > incumbentPatternEvidence;
        }

        private string BedFengShuiSummary(CABedFengShuiEvidence evidence,
            bool regularityEnforced, string actionableDeparture)
        {
            if (!actionableDeparture.NullOrEmpty())
                return "Feng Shui exception: " + actionableDeparture;
            if (evidence.existingBeds == 0)
                return "Feng Shui: established the first wall-aligned bed bank with "
                    + evidence.openApproaches + " open approach cells";
            if (regularityEnforced)
                return "Feng Shui: preserved the strongest available bed-bank pattern because no grounded, actionable exception existed";
            if (evidence.irregularBankIntervals > 0)
                return "Feng Shui: room geometry left no stronger valid regular pattern; retained circulation with "
                    + evidence.openApproaches + " open approach cells";
            return "Feng Shui: continued the bed-bank pattern with "
                + evidence.alignedRows + " aligned opposite row"
                + (evidence.alignedRows == 1 ? "" : "s") + " and "
                + evidence.matchingBankIntervals + " matching interval"
                + (evidence.matchingBankIntervals == 1 ? "" : "s");
        }

        private bool TryGetActionableBedDeparture(CASpaceProgram program,
            IntVec3 cell, out string justification)
        {
            justification = null;
            Pawn resident = NextProgramResidentNeedingBed(program);
            if (resident?.relations == null) return false;
            Pawn bondedAnimal = resident.relations.GetFirstDirectRelationPawn(
                PawnRelationDefOf.Bond, animal => animal != null
                    && !animal.Dead && animal.Spawned && animal.Map == map
                    && animal.RaceProps.Animal);
            Building_Bed animalBed = bondedAnimal?.ownership?.OwnedBed;
            if (animalBed == null || !animalBed.Spawned
                || animalBed.Map != map || cell.DistanceTo(animalBed.Position) > 4f)
                return false;
            justification = resident.LabelShort
                + " is the next unserved program resident and this placement stays near "
                + bondedAnimal.LabelShort + "'s existing assigned "
                + animalBed.def.label + " at " + animalBed.Position
                + ", which is the realized spatial anchor";
            return true;
        }

        private Pawn NextProgramResidentNeedingBed(CASpaceProgram program)
        {
            List<Pawn> residents = EligibleBedroomResidents(program);
            for (int i = 0; i < residents.Count; i++)
            {
                Pawn resident = residents[i];
                if (!ResidentOwnsCompatibleProgramBed(resident, program))
                    return resident;
            }
            return null;
        }

        private int DistanceToNearestDoor(IntVec3 cell, int radius)
        {
            int nearest = radius + 1;
            int limit = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 c = cell + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || !(c.GetEdifice(map) is Building_Door)) continue;
                nearest = Mathf.Min(nearest, Mathf.RoundToInt(c.DistanceTo(cell)));
            }
            // Larger is better; callers subtract this only where they explicitly
            // want an edge bias. The indoor scorer directly penalizes nearby doors.
            return nearest <= radius ? radius + 1 - nearest : 0;
        }

        private IntVec3 RoomCenter(Room room)
        {
            long x = 0;
            long z = 0;
            int count = 0;
            foreach (IntVec3 cell in room.Cells)
            {
                x += cell.x;
                z += cell.z;
                count++;
            }
            return count > 0
                ? new IntVec3((int)(x / count), 0, (int)(z / count))
                : IntVec3.Invalid;
        }

        private float StableTie(IntVec3 cell, Rot4 rot)
        {
            int hash = cell.x * 73856093 ^ cell.z * 19349663 ^ rot.AsInt * 83492791;
            return (hash & 1023) * 0.00001f;
        }

        private bool PrepareAndPlace(Pawn planner, CAHomePlan plan,
            out string outcome)
        {
            string authorization;
            if (!TryAuthorizeHomePlan(planner, ref plan, out authorization))
                return Finish(authorization, out outcome);
            CAHomePrerequisiteMapComponent prerequisites =
                CAHomePrerequisiteMapComponent.For(map);
            if (prerequisites == null)
                return Finish("the material prerequisite service is unavailable",
                    out outcome);
            string materialOutcome;
            if (!prerequisites.Prepare(planner, plan, out materialOutcome))
                return Finish(materialOutcome, out outcome);
            bool placed = PlacePlan(planner, plan, out outcome);
            if (placed) prerequisites.NotifyBlueprintPlaced(plan);
            return placed;
        }

        private bool TryAuthorizeHomePlan(Pawn planner, ref CAHomePlan plan,
            out string outcome)
        {
            string expectedKey = plan.minimumInitiative
                    >= CAInitiativeTier.Autonomous
                ? "spatial.home_comfort" : "spatial.home_essentials";
            if (!plan.behaviorKey.NullOrEmpty()
                && plan.behaviorKey != expectedKey)
            {
                outcome = "the retained home objective has behavior identity "
                    + plan.behaviorKey + " but now requires " + expectedKey;
                return false;
            }

            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            CAInitiativeTier ceiling = spatial?.TierFor(plan.program)
                ?? CAInitiativeTier.Standard;
            bool authoredProgram = plan.program == null
                || plan.program.author == CASpaceAuthor.Player;
            bool retainedAuthority = plan.episodeId <= 0
                || plan.authorityOrigin == CAAuthorityOrigin.PlayerDelegated
                || plan.authorityOrigin == CAAuthorityOrigin.Continuation
                || plan.authorityOrigin == CAAuthorityOrigin.SaveRestore;
            bool foreignPlayerWork = planner != null
                && CATactical.HasForeignPlayerForcedJob(planner);
            bool deficitKnown = TryGetHomeDeficitEvidence(plan,
                out string deficitBasis);
            string authorityBasis = plan.program != null
                ? "player-authored space program #" + plan.program.id
                    + " and its initiative ceiling"
                : "player Home designation and the default spatial initiative ceiling";
            CAAuthorityOrigin evaluationOrigin = plan.episodeId > 0
                ? CAAuthorityOrigin.Continuation
                : CAAuthorityOrigin.PlayerDelegated;
            var context = new CABehaviorContext(planner,
                CAActorContext.PlayerSpatialAuthority,
                planner != null ? AutonomyComponent.TierOf(planner)
                    : CAInitiativeTier.Standard,
                evaluationOrigin,
                authoritySatisfied: map.IsPlayerHome && authoredProgram
                    && retainedAuthority,
                knowledgeSatisfied: deficitKnown,
                knowledgeFresh: deficitKnown,
                liveValidated: planner?.Spawned == true
                    && planner.Map == map,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                knowledgeConfidence: deficitKnown ? 1f : 0f,
                knowledgeUncertainty: deficitKnown ? 0f : 1f,
                capabilitySatisfied: planner != null && !planner.Downed
                    && planner.workSettings != null
                    && !planner.WorkTypeIsDisabled(
                        WorkTypeDefOf.Construction),
                materialSatisfied: plan.def?.blueprintDef != null,
                currentIntentCompatible: !foreignPlayerWork,
                directPlayerOwnership: foreignPlayerWork,
                authorityCeiling: ceiling,
                authorityBasis: plan.episodeId > 0
                    && !plan.authorityIdentity.NullOrEmpty()
                        ? plan.authorityIdentity : authorityBasis,
                knowledgeBasis: deficitBasis,
                owner: nameof(AutonomousHomeMapComponent));
            CABehaviorDecision decision = CABehaviorGate.Evaluate(
                expectedKey, context);
            CABehaviorIntentMapComponent.For(map)?.ObserveDecision(
                planner, decision);
            if (!decision.Allowed)
            {
                outcome = expectedKey + " blocked: "
                    + decision.PrimaryReason;
                return false;
            }

            if (plan.episodeId <= 0)
            {
                CAIntentContext intent = CACombatIntent.Authorized(planner,
                    CAIntentController.Logistics, expectedKey,
                    CAAuthorityOrigin.PlayerDelegated, authorityBasis,
                    "delegated home objective",
                    plan.def.defName + " at " + plan.cell);
                plan.behaviorKey = intent.BehaviorKey;
                plan.episodeId = intent.EpisodeId;
                plan.intentOrigin = intent.Origin;
                plan.intentController = intent.Controller;
                plan.authorityOrigin = intent.AuthorityOrigin;
                plan.authorityIdentity = intent.AuthorityIdentity;
                plan.ownershipScope = intent.OwnershipScope;
                plan.issuerId = intent.IssuerId;
                plan.ownerId = intent.OwnerId;
                plan.createdTick = intent.CreatedTick;
                plan.creationTier = intent.CreationTier;
                plan.targetOrDemand = intent.TargetOrDemand;
                plan.terminationCondition = intent.TerminationCondition;
            }
            else
            {
                if (plan.intentController != CAIntentController.Logistics)
                {
                    outcome = expectedKey + " blocked: retained controller "
                        + plan.intentController + " is not Logistics";
                    return false;
                }
                plan.behaviorKey = expectedKey;
                CACombatIntent.ObserveEpisode(plan.episodeId);
            }
            outcome = expectedKey + " authorized as episode "
                + plan.episodeId;
            return true;
        }

        private bool TryGetHomeDeficitEvidence(CAHomePlan plan,
            out string basis)
        {
            basis = "no current furnishing deficit was established";
            if (plan.def == null || !plan.cell.IsValid
                || !plan.cell.InBounds(map)) return false;
            if (RetainedProvisionExists(plan))
            {
                basis = plan.def.label + " already exists at " + plan.cell;
                return false;
            }
            if (!RetainedNeedStillAuthorized(plan, out string refusal))
            {
                basis = refusal ?? "the selected furnishing need is no longer current";
                return false;
            }
            basis = "current missing " + plan.def.defName + " at "
                + plan.cell + " for " + (plan.reason.NullOrEmpty()
                    ? plan.kind.ToString() : plan.reason);
            return true;
        }

        private bool RetainedPlanCanResume(Pawn planner, CAHomePlan plan,
            out string problem)
        {
            problem = null;
            if (!plan.cell.IsValid || !plan.cell.InBounds(map))
            {
                problem = "the exact retained objective waits because its authored cell is invalid";
                return false;
            }
            if (!PlacementMatchesSpaceProgram(plan.def, plan.cell,
                plan.rotation, plan.kind, plan.program))
            {
                problem = "the exact retained objective waits because its authored space program no longer admits that provision";
                return false;
            }
            if (plan.outdoor)
            {
                foreach (IntVec3 occupied in GenAdj.OccupiedRect(plan.cell,
                    plan.rotation, plan.def.Size))
                {
                    if (!occupied.InBounds(map)
                        || !map.areaManager.Home[occupied]
                        || occupied.Fogged(map) || occupied.Roofed(map))
                    {
                        problem = "the exact retained outdoor recreation objective waits for its authored Home footprint";
                        return false;
                    }
                }
            }
            else
            {
                Room room = plan.cell.GetRoom(map);
                if (!EligibleHomeRoom(room)
                    || !FootprintInsideClaimedRoom(plan.def, plan.cell,
                        plan.rotation, room))
                {
                    problem = "the exact retained objective waits for its authored claimed room footprint";
                    return false;
                }
            }
            if (!CanUsePlacement(planner, plan.def, plan.stuff,
                    plan.cell, plan.rotation)
                || !GenConstruct.CanPlaceBlueprintAt(plan.def, plan.cell,
                    plan.rotation, map, godMode: false, null, null,
                    plan.stuff).Accepted)
            {
                problem = "the exact retained objective waits until its authored placement is natively buildable";
                return false;
            }
            return true;
        }

        private bool RetainedNeedStillAuthorized(CAHomePlan plan,
            out string reason)
        {
            reason = null;
            string suppression;
            if (NeedIsSuppressed(plan.kind, out suppression))
            {
                reason = "the retained " + plan.kind.ToString().ToLowerInvariant()
                    + " objective lost authorization: " + suppression;
                return false;
            }
            switch (plan.kind)
            {
                case CAHomePlanKind.Bed:
                    if (plan.program != null)
                    {
                        if (plan.program.purpose == CASpacePurpose.Bedroom
                            && (plan.program.author != CASpaceAuthor.Player
                                || plan.targetResidentId <= 0))
                        {
                            reason = "Bedroom Bed objective is outside the exact Player-authored concrete-resident path and retires without resuming construction";
                            return false;
                        }
                        if (plan.targetResidentId > 0)
                        {
                            Pawn concrete = EligibleBedroomResidents(
                                    plan.program).FirstOrDefault(resident =>
                                        resident.thingIDNumber
                                            == plan.targetResidentId);
                            CAInitiativeTier ceiling =
                                CASpatialInitiativeMapComponent.For(map)?
                                    .TierFor(plan.program)
                                ?? CAInitiativeTier.Standard;
                            if (plan.program.author != CASpaceAuthor.Player
                                || plan.program.purpose
                                    != CASpacePurpose.Bedroom
                                || ceiling < CAInitiativeTier.Proactive
                                || concrete == null
                                || ResidentOwnsCompatibleProgramBed(concrete,
                                    plan.program)
                                || !PlanAddressesResident(plan, plan.program,
                                    concrete))
                            {
                                reason = "the retained targeted Bedroom "
                                    + "objective lost its concrete resident, "
                                    + "Proactive+ space ceiling, or native "
                                    + "compatibility";
                                return false;
                            }
                        }
                        Dictionary<int, int> targets;
                        CurrentSleepProgramTargets(out targets);
                        int target;
                        if (!targets.TryGetValue(plan.program.id, out target)
                            || CountBedSlotsInProgram(plan.program) >= target)
                        {
                            reason = "the retained sleeping objective was satisfied or its roster no longer requires capacity";
                            return false;
                        }
                        if (plan.program.style == CASpaceStyle.Austere
                            && plan.def != ThingDefOf.SleepingSpot)
                        {
                            reason = "the retained sleeping objective lost authorization when its program became Austere";
                            return false;
                        }
                        ThingDef doubleBed = DefDatabase<ThingDef>
                            .GetNamedSilentFail("DoubleBed");
                        if (plan.def == doubleBed)
                        {
                            int missing = target
                                - CountBedSlotsInProgram(plan.program);
                            PlannedUseMapComponent programs =
                                PlannedUseMapComponent.For(map);
                            Pawn first;
                            Pawn second;
                            if (missing < 2 || programs == null
                                || !programs.TryGetShareableBedroomPair(
                                    plan.program, out first, out second))
                            {
                                reason = "the retained shared-bed objective lost current relationship or ideoligion consent";
                                return false;
                            }
                        }
                        return true;
                    }
                    GetUnprogrammedSleepNeed(out int unassignedPeople,
                        out int unprogrammedSlots,
                        out int unservedUnprogrammedCouples);
                    if (unprogrammedSlots >= unassignedPeople)
                    {
                        reason = "the retained unprogrammed sleeping objective was satisfied by capacity reserved for genuinely unassigned residents";
                        return false;
                    }
                    ThingDef unprogrammedDouble = DefDatabase<ThingDef>
                        .GetNamedSilentFail("DoubleBed");
                    if (plan.def == unprogrammedDouble
                        && (unassignedPeople - unprogrammedSlots < 2
                            || unservedUnprogrammedCouples <= 0))
                    {
                        reason = "the retained shared-bed objective lost current relationship or ideoligion consent";
                        return false;
                    }
                    return true;
                case CAHomePlanKind.Table:
                    if (CountBuildsAndPlans(def => def != null && def.IsTable) == 0)
                        return true;
                    reason = "the retained eating-surface objective was satisfied";
                    return false;
                case CAHomePlanKind.Seat:
                    int targetSeats = Mathf.Min(RestingColonistCount(),
                        plan.minimumInitiative >= CAInitiativeTier.Autonomous
                            ? 6 : 2);
                    if (plan.seatForJoy)
                    {
                        if (RetainedSeatSupportsCurrentJoyNeed(plan.cell))
                            return true;
                    }
                    else if (CountTableSeatsAndPlans() < targetSeats
                        && RetainedSeatTouchesUsableTable(plan.cell)) return true;
                    reason = "the retained seating objective was satisfied";
                    return false;
                case CAHomePlanKind.Light:
                    Room room = plan.cell.GetRoom(map);
                    bool furnished = EligibleHomeRoom(room)
                        && RoomHas(room, def => def.IsBed || def.IsTable
                            || def.defName == "ChessTable");
                    if (furnished && !RoomHasLightFixture(room)
                        && !RoomHasPlannedLight(room)) return true;
                    reason = "the retained lighting objective was satisfied or its room is no longer furnished";
                    return false;
                case CAHomePlanKind.Recreation:
                    if (CountUsableRecreationAndPlans() == 0) return true;
                    reason = "the retained recreation objective was satisfied";
                    return false;
                default:
                    reason = "the retained objective kind is no longer authorized";
                    return false;
            }
        }

        private bool HasShareableResidentCouple()
        {
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                for (int j = i + 1; j < pawns.Count; j++)
                {
                    if (LovePartnerRelationUtility.LovePartnerRelationExists(
                            pawns[i], pawns[j])
                        && BedUtility.WillingToShareBed(pawns[i], pawns[j]))
                        return true;
                }
            }
            return false;
        }

        private bool RetainedSeatSupportsCurrentJoyNeed(IntVec3 cell)
        {
            Building joy = FindJoyBuildingNeedingSeat();
            if (joy == null) return false;
            List<IntVec3> adjacent = GenAdjFast.AdjacentCellsCardinal(joy);
            for (int i = 0; i < adjacent.Count; i++)
                if (adjacent[i] == cell) return true;
            return false;
        }

        private bool RetainedSeatTouchesUsableTable(IntVec3 cell)
        {
            var tables = new List<Building>();
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building table = buildings[i];
                if (table.def.IsTable && ThingInsideClaimedShelter(table)
                    && AnyResidentCanReach(table)) tables.Add(table);
            }
            return SeatTouchesAnyTable(cell, tables);
        }

        private bool RetainedFootprintClaimedByPlayer(CAHomePlan plan)
        {
            foreach (IntVec3 occupied in GenAdj.OccupiedRect(plan.cell,
                plan.rotation, plan.def.Size))
            {
                if (!occupied.InBounds(map)) continue;
                List<Thing> things = occupied.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if ((thing is Blueprint || thing is Frame
                            || (thing is Building
                                && thing.Faction == Faction.OfPlayer))
                        && !(thing.Position == plan.cell
                            && thing.Rotation == plan.rotation
                            && thing.Stuff == plan.stuff
                            && (thing.def == plan.def
                                || thing.def.entityDefToBuild == plan.def)))
                        return true;
                }
            }
            return false;
        }

        private bool RetainedProvisionExists(CAHomePlan plan)
        {
            if (!plan.cell.IsValid || !plan.cell.InBounds(map)) return false;
            List<Thing> things = plan.cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                bool defMatches = thing?.def == plan.def
                    || thing?.def?.entityDefToBuild == plan.def;
                if (defMatches && thing.Position == plan.cell
                    && thing.Rotation == plan.rotation
                    && thing.Stuff == plan.stuff)
                    return true;
            }
            return false;
        }

        private bool PlacePlan(Pawn planner, CAHomePlan plan, out string outcome)
        {
            ThingStyleDef style = Faction.OfPlayer.ideos?.PrimaryIdeo?.GetStyleFor(plan.def);
            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(
                plan.def, plan.cell, map, plan.rotation, Faction.OfPlayer,
                plan.stuff, null, style);
            if (blueprint == null)
                return Finish("the native blueprint placement returned no plan", out outcome);
            if (plan.def.PlaceWorkers != null)
            {
                for (int i = 0; i < plan.def.PlaceWorkers.Count; i++)
                    plan.def.PlaceWorkers[i].PostPlace(map, plan.def, plan.cell,
                        plan.rotation);
            }

            pendingDefName = plan.def.defName;
            pendingKind = plan.kind.ToString();
            pendingCell = plan.cell;
            pendingThingId = blueprint.ThingID;
            pendingOriginThingId = blueprint.ThingID;
            pendingConsumingFrameThingId = null;
            pendingConsumedMaterialThingId = null;
            pendingConsumedMaterialEvidence = null;
            pendingConsumedMaterialCount = 0;
            pendingSinceTick = Find.TickManager.TicksGame;
            pendingProgramId = plan.program?.id ?? 0;
            pendingTargetResidentId = plan.targetResidentId > 0
                ? plan.targetResidentId : 0;
            pendingBehaviorKey = plan.behaviorKey;
            pendingEpisodeId = plan.episodeId;
            pendingIntentOrigin = (int)plan.intentOrigin;
            pendingIntentController = (int)plan.intentController;
            pendingAuthorityOrigin = (int)plan.authorityOrigin;
            pendingAuthorityIdentity = plan.authorityIdentity;
            pendingOwnershipScope = plan.ownershipScope;
            pendingIssuerId = plan.issuerId;
            pendingOwnerId = plan.ownerId;
            pendingCreatedTick = plan.createdTick;
            pendingCreationTier = (int)plan.creationTier;
            pendingTargetOrDemand = plan.targetOrDemand;
            pendingTerminationCondition = plan.terminationCondition;
            lastPlannerId = planner.thingIDNumber;
            lastPlannedDef = plan.def.defName;
            lastPlannedStuff = plan.stuff?.defName;
            lastPlannedCell = plan.cell;
            lastPlannedTick = pendingSinceTick;
            lastReason = plan.reason;
            lastPlacementEvidence = plan.placementEvidence;
            Room plannedRoom = plan.cell.GetRoom(map);
            CASpaceProgram spaceProgram = plan.program
                ?? PlannedUseMapComponent.For(map)?.ProgramAt(plan.cell);
            lastOutcome = planner.LabelShort + " planned " + plan.def.label
                + " at " + plan.cell + " facing " + plan.rotation + ": "
                + plan.reason + (plannedRoom != null
                    ? " [room " + plannedRoom.Role?.defName + ", cells "
                        + plannedRoom.CellCount + ", short span "
                        + RoomShortSpan(plannedRoom) + ", doors "
                        + RoomDoorCount(plannedRoom) + "]" : "")
                + (spaceProgram != null
                    ? " [space program " + spaceProgram.label + ", "
                        + CASpacePurposeInfo.Label(spaceProgram.purpose) + ", "
                        + CASpacePurposeInfo.StyleLabel(spaceProgram.style)
                        + ", max " + spaceProgram.maxOccupants
                        + (spaceProgram.RequiresSleep ? ", requires sleep" : "")
                        + "]"
                    : "")
                + (lastPlacementEvidence.NullOrEmpty() ? ""
                    : " [" + lastPlacementEvidence + "]");
            outcome = lastOutcome;
            Log.Message("[CA] " + lastOutcome);
            string visiblePlacement = ConcisePlacementEvidence(
                plan.placementEvidence);
            Messages.Message("Colonist Awareness: " + planner.LabelShort
                + " planned " + plan.def.label + " - " + plan.reason
                + (visiblePlacement.NullOrEmpty() ? "." : ". "
                    + visiblePlacement + "."),
                new TargetInfo(plan.cell, map), MessageTypeDefOf.SilentInput,
                historical: false);
            return true;
        }

        private static string ConcisePlacementEvidence(string evidence)
        {
            if (evidence.NullOrEmpty()) return null;
            int end = evidence.IndexOf(';');
            return end > 0 ? evidence.Substring(0, end) : evidence;
        }

        private bool PendingPlanStillExists()
        {
            // Legacy pending metadata has no exact construction identity. It is
            // deliberately retired without touching anything at the old cell;
            // a same-def player replacement must never be adopted as CA work.
            if (pendingDefName.NullOrEmpty() || pendingThingId.NullOrEmpty()
                || !pendingCell.IsValid) return false;
            List<Thing> things = pendingCell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing.ThingID != pendingThingId) continue;
                if (thing is Blueprint_Build || thing is Frame)
                {
                    ThingDef entity = thing.def.entityDefToBuild as ThingDef;
                    if (entity != null && entity.defName == pendingDefName)
                    {
                        if (pendingKind == CAHomePlanKind.Bed.ToString()
                            && pendingProgramId > 0
                            && pendingTargetResidentId > 0)
                        {
                            PlannedUseMapComponent targetPrograms =
                                PlannedUseMapComponent.For(map);
                            CASpaceProgram targetProgram = targetPrograms
                                ?.FindProgram(pendingProgramId);
                            Pawn targetResident = null;
                            List<Pawn> eligible = EligibleBedroomResidents(
                                targetProgram);
                            for (int residentIndex = 0;
                                residentIndex < eligible.Count;
                                residentIndex++)
                                if (eligible[residentIndex].thingIDNumber
                                    == pendingTargetResidentId)
                                {
                                    targetResident = eligible[residentIndex];
                                    break;
                                }
                            bool targetStillGroundsPlan = targetProgram != null
                                && targetProgram.author == CASpaceAuthor.Player
                                && targetProgram.purpose
                                    == CASpacePurpose.Bedroom
                                && targetProgram.RequiresSleep
                                && (CASpatialInitiativeMapComponent.For(map)?
                                    .TierFor(targetProgram)
                                    ?? CAInitiativeTier.Standard)
                                        >= CAInitiativeTier.Proactive
                                && targetResident != null
                                && RestUtility.CanUseBedEver(targetResident,
                                    entity)
                                && !ResidentOwnsCompatibleProgramBed(
                                    targetResident, targetProgram);
                            if (targetStillGroundsPlan
                                && BedUtility.GetSleepingSlotsCount(
                                    entity.Size) > 1)
                            {
                                targetStillGroundsPlan = targetPrograms
                                    .TryGetShareableBedroomPair(targetProgram,
                                        out Pawn targetFirst,
                                        out Pawn targetSecond)
                                    && (targetResident == targetFirst
                                        || targetResident == targetSecond);
                            }
                            if (!targetStillGroundsPlan)
                            {
                                IntVec3 canceledCell = thing.Position;
                                thing.Destroy(DestroyMode.Cancel);
                                lastOutcome = "targeted Bed at "
                                    + canceledCell
                                    + " was canceled after concrete resident #"
                                    + pendingTargetResidentId
                                    + " stopped grounding the exact authored Bedroom deficit";
                                Log.Message("[CA] " + lastOutcome);
                                return false;
                            }
                        }
                        ThingDef nativeDefault = GenStuff.DefaultStuffFor(entity);
                        if (pendingKind == CAHomePlanKind.Bed.ToString()
                            && thing.Stuff != null && thing.Stuff != nativeDefault
                            && thing.Stuff.stuffProps != null
                            && !thing.Stuff.stuffProps.canSuggestUseDefaultStuff)
                        {
                            string retiredStuff = thing.Stuff.defName;
                            IntVec3 retiredCell = thing.Position;
                            thing.Destroy(DestroyMode.Cancel);
                            lastOutcome = "retired CA-owned " + retiredStuff
                                + " " + entity.defName + " plan at "
                                + retiredCell + " after practical material "
                                + "selection superseded availability-only choice";
                            Log.Message("[CA] " + lastOutcome);
                            return false;
                        }
                        if (pendingProgramId > 0
                            && pendingDefName == "DoubleBed")
                        {
                            PlannedUseMapComponent programs =
                                PlannedUseMapComponent.For(map);
                            CASpaceProgram program = programs
                                ?.FindProgram(pendingProgramId);
                            if (program == null
                                || !programs.TryGetShareableBedroomPair(
                                    program, out Pawn first, out Pawn second))
                            {
                                IntVec3 canceledCell = thing.Position;
                                thing.Destroy(DestroyMode.Cancel);
                                lastOutcome = "shared DoubleBed at "
                                    + canceledCell
                                    + " was canceled after its Bedroom pair stopped passing native sharing consent; single-slot planning will reevaluate";
                                Log.Message("[CA] " + lastOutcome);
                                return false;
                            }
                        }
                        return true;
                    }
                }
                Building building = thing as Building;
                if (building != null && building.def.defName == pendingDefName)
                {
                    RecordCompleted(building);
                    return false;
                }
            }
            return false;
        }

        private void RecordCompleted(Building building)
        {
            if (building == null || pendingKind.NullOrEmpty()) return;
            for (int i = 0; i < completedAutoBuildings.Count; i++)
            {
                CAHomeBuiltRecord existing = completedAutoBuildings[i];
                if (existing != null && existing.thingId == building.ThingID)
                {
                    if (existing.programId == 0 && pendingProgramId > 0)
                        existing.programId = pendingProgramId;
                    if (existing.targetResidentId == 0
                        && pendingTargetResidentId > 0)
                        existing.targetResidentId = pendingTargetResidentId;
                    if (existing.placementEvidence.NullOrEmpty()
                        && !lastPlacementEvidence.NullOrEmpty())
                        existing.placementEvidence = lastPlacementEvidence;
                    if (existing.originThingId.NullOrEmpty()
                        && !pendingOriginThingId.NullOrEmpty())
                        existing.originThingId = pendingOriginThingId;
                    if (existing.consumingFrameThingId.NullOrEmpty()
                        && !pendingConsumingFrameThingId.NullOrEmpty())
                        existing.consumingFrameThingId =
                            pendingConsumingFrameThingId;
                    if (existing.consumedMaterialThingId.NullOrEmpty()
                        && !pendingConsumedMaterialThingId.NullOrEmpty())
                        existing.consumedMaterialThingId =
                            pendingConsumedMaterialThingId;
                    if (existing.consumedMaterialEvidence.NullOrEmpty()
                        && !pendingConsumedMaterialEvidence.NullOrEmpty())
                        existing.consumedMaterialEvidence =
                            pendingConsumedMaterialEvidence;
                    if (existing.consumedMaterialCount == 0
                        && pendingConsumedMaterialCount > 0)
                        existing.consumedMaterialCount =
                            pendingConsumedMaterialCount;
                    return;
                }
            }
            completedAutoBuildings.Add(new CAHomeBuiltRecord
            {
                defName = building.def.defName,
                kind = pendingKind,
                thingId = building.ThingID,
                cell = building.Position,
                programId = pendingProgramId,
                targetResidentId = pendingTargetResidentId,
                placementEvidence = lastPlacementEvidence,
                originThingId = pendingOriginThingId,
                consumingFrameThingId = pendingConsumingFrameThingId,
                consumedMaterialThingId = pendingConsumedMaterialThingId,
                consumedMaterialEvidence = pendingConsumedMaterialEvidence,
                consumedMaterialCount = pendingConsumedMaterialCount
            });
            AssignCompletedBedToProgramResident(building);
        }

        private void AssignCompletedBedToProgramResident(Building building)
        {
            Building_Bed bed = building as Building_Bed;
            if (bed == null || pendingProgramId <= 0
                || pendingKind != CAHomePlanKind.Bed.ToString()
                || !bed.def.building.bed_humanlike || !bed.ForColonists
                || bed.Medical || bed.OwnersForReading.Count > 0) return;
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            CASpaceProgram program = programs?.FindProgram(pendingProgramId);
            if (program == null || !program.HasResidentRoster
                || !ThingInsideProgram(bed, bed.def, program)) return;

            if (pendingTargetResidentId > 0)
            {
                Pawn target = null;
                List<Pawn> eligible = EligibleBedroomResidents(program);
                for (int i = 0; i < eligible.Count; i++)
                    if (eligible[i].thingIDNumber == pendingTargetResidentId)
                    {
                        target = eligible[i];
                        break;
                    }
                if (target == null)
                {
                    lastOutcome += "; targeted native ownership was not reassigned because resident #"
                        + pendingTargetResidentId
                        + " no longer belongs to the eligible saved roster";
                    Log.Message("[CA] " + lastOutcome);
                    return;
                }
                if (ResidentOwnsCompatibleProgramBed(target, program))
                {
                    lastOutcome += "; targeted resident " + target.LabelShort
                        + " already owns compatible capacity inside the exact Bedroom";
                    Log.Message("[CA] " + lastOutcome);
                    return;
                }
                CompAssignableToPawn_Bed targetedAssignable =
                    bed.CompAssignableToPawn as CompAssignableToPawn_Bed;
                if (targetedAssignable == null
                    || !targetedAssignable.CanAssignTo(target).Accepted
                    || targetedAssignable.IdeoligionForbids(target)
                    || !RestUtility.CanUseBedNow(bed, target,
                        checkSocialProperness: true))
                {
                    lastOutcome += "; completed Bed retained without reassignment because native use or assignment semantics no longer accept targeted resident "
                        + target.LabelShort;
                    Log.Message("[CA] " + lastOutcome);
                    return;
                }
                if (bed.SleepingSlotsCount == 2)
                {
                    if (programs.TryGetShareableBedroomPair(program,
                            out Pawn expectedFirst, out Pawn expectedSecond)
                        && (target == expectedFirst || target == expectedSecond)
                        && programs.TryAssignSharedBedroomBed(program, bed,
                            out Pawn assignedFirst, out Pawn assignedSecond))
                    {
                        lastOutcome += "; native bed ownership assigned to targeted resident "
                            + target.LabelShort + " and share-willing resident "
                            + (target == assignedFirst
                                ? assignedSecond.LabelShort
                                : assignedFirst.LabelShort);
                        Log.Message("[CA] space program " + program.label
                            + " assigned completed " + bed.def.defName + " to "
                            + assignedFirst.LabelShort + " and "
                            + assignedSecond.LabelShort
                            + " through target-preserving native bed ownership");
                    }
                    else
                    {
                        lastOutcome += "; completed shared Bed retained without reassignment because the targeted resident pair no longer passes native sharing consent";
                        Log.Message("[CA] " + lastOutcome);
                    }
                    return;
                }
                if (bed.SleepingSlotsCount == 1
                    && target.ownership != null
                    && target.ownership.ClaimBedIfNonMedical(bed))
                {
                    bed.NotifyRoomAssignedPawnsChanged();
                    lastOutcome += "; native bed ownership assigned to targeted resident "
                        + target.LabelShort;
                    Log.Message("[CA] space program " + program.label
                        + " assigned completed " + bed.def.defName + " to "
                        + target.LabelShort
                        + " through target-preserving native bed ownership");
                }
                return;
            }

            // Legacy plans predate concrete resident cause. Retain the prior
            // deterministic fallback only when no target ID was persisted.
            if (bed.SleepingSlotsCount == 2
                && programs.TryAssignSharedBedroomBed(program, bed,
                    out Pawn first, out Pawn second))
            {
                lastOutcome += "; native bed ownership assigned to "
                    + first.LabelShort + " and " + second.LabelShort;
                Log.Message("[CA] space program " + program.label
                    + " assigned completed " + bed.def.defName + " to "
                    + first.LabelShort + " and " + second.LabelShort
                    + " through native bed ownership");
                return;
            }
            if (bed.SleepingSlotsCount != 1) return;
            for (int i = 0; i < program.residents.Count; i++)
            {
                Pawn resident = program.residents[i];
                if (resident == null || resident.Dead || !resident.Spawned
                    || resident.Map != map || resident.needs?.rest == null
                    || resident.DevelopmentalStage.Baby()) continue;
                Building_Bed owned = resident.ownership?.OwnedBed;
                if (owned != null && ThingInsideProgram(owned, owned.def,
                    program)) continue;
                CompAssignableToPawn_Bed assignable =
                    bed.CompAssignableToPawn as CompAssignableToPawn_Bed;
                if (assignable == null || !assignable.CanAssignTo(resident).Accepted
                    || assignable.IdeoligionForbids(resident)) continue;
                if (!resident.ownership.ClaimBedIfNonMedical(bed)) continue;
                lastOutcome += "; native bed ownership assigned to "
                    + resident.LabelShort;
                Log.Message("[CA] space program " + program.label
                    + " assigned completed " + bed.def.defName + " to "
                    + resident.LabelShort + " through native bed ownership");
                return;
            }
        }

        public void NotifyBlueprintReplaced(string sourceThingId,
            Thing createdThing)
        {
            if (createdThing == null || sourceThingId.NullOrEmpty()
                || pendingThingId != sourceThingId
                || pendingDefName.NullOrEmpty()
                || createdThing.Position != pendingCell) return;
            if (createdThing is Frame)
            {
                ThingDef frameDef = createdThing.def.entityDefToBuild as ThingDef;
                if (frameDef == null || frameDef.defName != pendingDefName)
                    return;
                pendingThingId = createdThing.ThingID;
                return;
            }
            Building building = createdThing as Building;
            if (building == null || building.def.defName != pendingDefName)
                return;
            CompleteBedroomCausePlanningScopeForPending();
            pendingThingId = building.ThingID;
            RecordCompleted(building);
        }

        public void NotifyFrameCompleted(string sourceThingId, IntVec3 cell,
            string defName)
        {
            if (sourceThingId.NullOrEmpty() || pendingThingId != sourceThingId
                || pendingDefName.NullOrEmpty() || defName != pendingDefName
                || cell != pendingCell) return;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Building building = things[i] as Building;
                if (building != null && building.def.defName == defName)
                {
                    CompleteBedroomCausePlanningScopeForPending();
                    pendingThingId = building.ThingID;
                    RecordCompleted(building);
                    return;
                }
            }
        }

        public void NotifyFrameWillComplete(Frame frame, Pawn worker)
        {
            if (frame == null || pendingThingId.NullOrEmpty()
                || pendingThingId != frame.ThingID
                || pendingOriginThingId.NullOrEmpty()
                || pendingProgramId <= 0 || pendingTargetResidentId <= 0)
                return;
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            if (bridge == null
                || !bridge.TryGetBedroomCauseMaterialAudit(
                    pendingProgramId, pendingTargetResidentId,
                    out string auditedOriginId,
                    out int auditedMaterialCount)
                || auditedOriginId != pendingOriginThingId)
                return;
            int requiredMaterialCount = 0;
            List<ThingDefCountClass> costs = frame.BuildDef.CostListAdjusted(
                frame.Stuff, errorOnNullStuff: false);
            for (int i = 0; i < costs.Count; i++)
                if (costs[i]?.thingDef == ThingDefOf.WoodLog)
                    requiredMaterialCount += costs[i].count;
            var materials = new List<Thing>();
            for (int i = 0; i < frame.resourceContainer.Count; i++)
            {
                Thing material = frame.resourceContainer[i];
                if (material?.def == ThingDefOf.WoodLog)
                    materials.Add(material);
            }
            materials.Sort((left, right) => string.CompareOrdinal(
                left.ThingID, right.ThingID));
            int containedMaterialCount = materials.Sum(material =>
                material.stackCount);
            if (auditedMaterialCount != requiredMaterialCount
                || containedMaterialCount != requiredMaterialCount
                || materials.Count == 0)
                return;
            string materialEvidence = string.Join(", ", materials.Select(
                material => material.ThingID + " x"
                    + material.stackCount));
            pendingConsumingFrameThingId = frame.ThingID;
            pendingConsumedMaterialThingId = materials.Count == 1
                ? materials[0].ThingID : null;
            pendingConsumedMaterialEvidence = materialEvidence;
            pendingConsumedMaterialCount = containedMaterialCount;
            int woodCountImmediatelyBefore =
                CADebugActions.CountMapThingsRecursivelyForAudit(frame.Map,
                    ThingDefOf.WoodLog);
            bridge.ConfirmBedroomCauseMaterialConsumption(
                pendingProgramId, pendingTargetResidentId,
                pendingOriginThingId, frame.ThingID, materialEvidence,
                containedMaterialCount, woodCountImmediatelyBefore, worker);
        }

        public void NotifyExplicitPlanCancel(Thing thing)
        {
            if (thing == null || pendingDefName.NullOrEmpty()
                || pendingKind.NullOrEmpty() || pendingThingId.NullOrEmpty()
                || thing.ThingID != pendingThingId
                || thing.Position != pendingCell)
                return;
            BuildableDef entity = thing.def.entityDefToBuild;
            if (entity == null || entity.defName != pendingDefName) return;
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            bridge?.RetireBedroomCausePlanningScope(map, pendingProgramId,
                pendingTargetResidentId, pendingOriginThingId,
                "the player explicitly canceled the exact construction");
            bridge?.RetireBedroomCauseMaterialAudit(pendingProgramId,
                pendingTargetResidentId, pendingOriginThingId);
            suppressedKinds[pendingKind] = Find.TickManager.TicksGame
                + GenDate.TicksPerDay;
            lastOutcome = pendingDefName + " at " + pendingCell
                + " was explicitly canceled; " + pendingKind.ToLowerInvariant()
                + " planning is suppressed for one day";
            Messages.Message("Colonist Awareness: " + pendingKind.ToLowerInvariant()
                + " planning paused for one day after you canceled "
                + pendingDefName + ".", new TargetInfo(pendingCell, map),
                MessageTypeDefOf.SilentInput, historical: false);
        }

        public void NotifyExplicitDeconstruct(Thing thing)
        {
            Building building = thing?.GetInnerIfMinified() as Building;
            if (building == null) return;
            for (int i = completedAutoBuildings.Count - 1; i >= 0; i--)
            {
                CAHomeBuiltRecord record = completedAutoBuildings[i];
                if (record == null || record.thingId.NullOrEmpty()
                    || record.thingId != building.ThingID)
                    continue;
                suppressedKinds[record.kind] = int.MaxValue;
                completedAutoBuildings.RemoveAt(i);
                lastOutcome = building.def.label + " at " + building.Position
                    + " was marked for deconstruction; "
                    + record.kind.ToLowerInvariant()
                    + " planning is vetoed until Home planning is reset";
                Messages.Message("Colonist Awareness: deconstructing this automatic "
                    + building.def.label + " vetoes further "
                    + record.kind.ToLowerInvariant()
                    + " planning until Home planning is reset.",
                    new TargetInfo(thing.Position, map),
                    MessageTypeDefOf.SilentInput, historical: false);
                break;
            }
        }

        private void PruneCompletedRecords()
        {
            for (int i = completedAutoBuildings.Count - 1; i >= 0; i--)
            {
                CAHomeBuiltRecord record = completedAutoBuildings[i];
                if (record == null || record.thingId.NullOrEmpty())
                {
                    completedAutoBuildings.RemoveAt(i);
                    continue;
                }
                bool found = false;
                List<Thing> things = map.listerThings.AllThings;
                for (int j = 0; j < things.Count; j++)
                {
                    Building building = things[j].GetInnerIfMinified() as Building;
                    if (building != null && !record.thingId.NullOrEmpty()
                        && building.ThingID == record.thingId)
                    {
                        record.cell = things[j].Position;
                        record.defName = building.def.defName;
                        found = true;
                        break;
                    }
                }
                if (!found) completedAutoBuildings.RemoveAt(i);
            }
        }

        private void ClearPending()
        {
            bool completedLineage = !pendingOriginThingId.NullOrEmpty()
                && completedAutoBuildings.Any(record => record != null
                    && record.originThingId == pendingOriginThingId
                    && record.programId == pendingProgramId
                    && record.targetResidentId == pendingTargetResidentId);
            if (!completedLineage && !pendingOriginThingId.NullOrEmpty())
            {
                CAAgentDebugBridge bridge =
                    CAAgentDebugBridge.ForCurrentGame();
                bridge?.RetireBedroomCausePlanningScope(map,
                    pendingProgramId, pendingTargetResidentId,
                    pendingOriginThingId,
                    "the exact native construction was retired");
                bridge?.RetireBedroomCauseMaterialAudit(
                    pendingProgramId, pendingTargetResidentId,
                    pendingOriginThingId);
            }
            pendingDefName = null;
            pendingKind = null;
            pendingCell = IntVec3.Invalid;
            pendingThingId = null;
            pendingOriginThingId = null;
            pendingConsumingFrameThingId = null;
            pendingConsumedMaterialThingId = null;
            pendingConsumedMaterialEvidence = null;
            pendingConsumedMaterialCount = 0;
            pendingSinceTick = -1;
            pendingProgramId = 0;
            pendingTargetResidentId = 0;
            pendingBehaviorKey = null;
            pendingEpisodeId = 0;
            pendingIntentOrigin = 0;
            pendingIntentController = 0;
            pendingAuthorityOrigin = 0;
            pendingAuthorityIdentity = null;
            pendingOwnershipScope = null;
            pendingIssuerId = -1;
            pendingOwnerId = -1;
            pendingCreatedTick = -1;
            pendingCreationTier = 0;
            pendingTargetOrDemand = null;
            pendingTerminationCondition = null;
        }

        private void CompleteBedroomCausePlanningScopeForPending()
        {
            CAAgentDebugBridge.ForCurrentGame()?
                .CompleteBedroomCausePlanningScope(map, pendingProgramId,
                    pendingTargetResidentId, pendingOriginThingId);
        }

        private int RestingColonistCount()
        {
            int count = 0;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.needs?.rest != null && !pawn.DevelopmentalStage.Baby()) count++;
            }
            return count;
        }

        private int CountBedSlotsAndPlans()
        {
            int slots = 0;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building_Bed bed = buildings[i] as Building_Bed;
                if (bed != null && bed.def.building.bed_humanlike
                    && bed.ForColonists && !bed.Medical
                    && ThingInsideClaimedShelter(bed))
                    slots += bed.SleepingSlotsCount;
            }
            slots += CountPlannedCapacity(def => def != null && def.IsBed
                && def.building != null && def.building.bed_humanlike);
            return slots;
        }

        private int CountBedSlotsInProgram(CASpaceProgram program)
        {
            return CountBedSlotsInProgram(program, includePlanned: true);
        }

        private int CountCompletedBedSlotsInProgram(CASpaceProgram program)
        {
            return CountBedSlotsInProgram(program, includePlanned: false);
        }

        private int CountBedSlotsInProgram(CASpaceProgram program,
            bool includePlanned)
        {
            if (program == null) return 0;
            int slots = 0;
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building_Bed bed = buildings[i] as Building_Bed;
                if (bed != null && bed.def.building.bed_humanlike
                    && bed.ForColonists && !bed.Medical
                    && ThingInsideProgram(bed, bed.def, program))
                {
                    if (!program.HasResidentRoster)
                    {
                        int available = bed.SleepingSlotsCount;
                        for (int owner = 0; owner < bed.OwnersForReading.Count;
                            owner++)
                        {
                            CASpaceProgram residentProgram = programs
                                ?.ResidentProgramFor(bed.OwnersForReading[owner]);
                            if (residentProgram != null
                                && residentProgram != program) available--;
                        }
                        slots += Mathf.Max(0, available);
                    }
                    else if (bed.SleepingSlotsCount == 1)
                    {
                        if (bed.OwnersForReading.Count == 0
                            || program.residents.Contains(
                                bed.OwnersForReading[0])) slots++;
                    }
                    else
                    {
                        for (int owner = 0; owner < bed.OwnersForReading.Count;
                            owner++)
                            if (program.residents.Contains(
                                bed.OwnersForReading[owner])) slots++;
                    }
                }
            }

            if (!includePlanned) return slots;

            ThingRequestGroup[] groups =
                { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame };
            for (int g = 0; g < groups.Length; g++)
            {
                List<Thing> things = map.listerThings.ThingsInGroup(groups[g]);
                for (int i = 0; i < things.Count; i++)
                {
                    ThingDef def = things[i].def.entityDefToBuild as ThingDef;
                    if (def == null || !def.IsBed || def.building == null
                        || !def.building.bed_humanlike
                        || !ThingInsideProgram(things[i], def, program)) continue;
                    int plannedSlots = BedUtility.GetSleepingSlotsCount(def.Size);
                    if (!program.HasResidentRoster || plannedSlots == 1
                        || (plannedSlots == 2
                            && programs != null
                            && programs.TryGetShareableBedroomPair(program,
                                out Pawn first, out Pawn second)))
                        slots += plannedSlots;
                }
            }
            return slots;
        }

        private int CountBedSlotsOutsidePrograms(
            List<CASpaceProgram> sleepPrograms,
            HashSet<Pawn> explicitResidents)
        {
            var programIds = new HashSet<int>();
            for (int i = 0; i < sleepPrograms.Count; i++)
                programIds.Add(sleepPrograms[i].id);
            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            int slots = 0;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building_Bed bed = buildings[i] as Building_Bed;
                if (bed != null && bed.def.building.bed_humanlike
                    && bed.ForColonists && !bed.Medical
                    && ThingOutsidePrograms(bed, bed.def, programIds,
                        component)
                    && ThingInsideClaimedShelter(bed))
                {
                    int available = bed.SleepingSlotsCount;
                    if (explicitResidents != null)
                        for (int owner = 0; owner < bed.OwnersForReading.Count;
                            owner++)
                            if (explicitResidents.Contains(
                                bed.OwnersForReading[owner])) available--;
                    slots += Mathf.Max(0, available);
                }
            }

            ThingRequestGroup[] groups =
                { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame };
            for (int g = 0; g < groups.Length; g++)
            {
                List<Thing> things = map.listerThings.ThingsInGroup(groups[g]);
                for (int i = 0; i < things.Count; i++)
                {
                    ThingDef def = things[i].def.entityDefToBuild as ThingDef;
                    if (def == null || !def.IsBed || def.building == null
                        || !def.building.bed_humanlike
                        || !ThingOutsidePrograms(things[i], def, programIds,
                            component)
                        || !ThingInsideClaimedShelter(things[i], def)) continue;
                    slots += BedUtility.GetSleepingSlotsCount(def.Size);
                }
            }
            return slots;
        }

        private bool ThingOutsidePrograms(Thing thing, ThingDef builtDef,
            HashSet<int> programIds, PlannedUseMapComponent component)
        {
            if (thing == null || builtDef == null || component == null)
                return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(thing.Position,
                thing.Rotation, builtDef.Size))
            {
                CASpaceProgram program = component.ProgramAt(cell);
                if (program != null && programIds.Contains(program.id))
                    return false;
            }
            return true;
        }

        private bool ThingInsideProgram(Thing thing, ThingDef builtDef,
            CASpaceProgram program)
        {
            if (thing == null || builtDef == null || program == null
                || !thing.Spawned || thing.Map != map) return false;
            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            if (component == null) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(
                thing.Position, thing.Rotation, builtDef.Size))
                if (component.ProgramAt(cell) != program) return false;
            return true;
        }

        private void GetUnprogrammedSleepNeed(out int people,
            out int bedSlots, out int unservedCouples)
        {
            Dictionary<int, int> ignoredTargets;
            List<CASpaceProgram> sleepPrograms = CurrentSleepProgramTargets(
                out ignoredTargets);
            var assignedResidents = new HashSet<Pawn>();
            for (int i = 0; i < sleepPrograms.Count; i++)
            {
                List<Pawn> residents = sleepPrograms[i].residents;
                if (residents == null) continue;
                for (int r = 0; r < residents.Count; r++)
                {
                    Pawn resident = residents[r];
                    if (resident != null && resident.Spawned
                        && resident.Map == map && resident.IsFreeColonist
                        && resident.needs?.rest != null
                        && !resident.DevelopmentalStage.Baby())
                        assignedResidents.Add(resident);
                }
            }
            var unassigned = new List<Pawn>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn.needs?.rest != null
                    && !pawn.DevelopmentalStage.Baby()
                    && !assignedResidents.Contains(pawn))
                    unassigned.Add(pawn);
            }
            people = unassigned.Count;
            bedSlots = CountBedSlotsOutsidePrograms(sleepPrograms,
                assignedResidents);
            ThingDef doubleBed = DefDatabase<ThingDef>
                .GetNamedSilentFail("DoubleBed");
            unservedCouples = Mathf.Max(0, CountResidentCouples(unassigned)
                - CountExactDefAndPlansOutsidePrograms(doubleBed,
                    sleepPrograms));
        }

        private static int CountResidentCouples(List<Pawn> pawns)
        {
            int count = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                for (int j = i + 1; j < pawns.Count; j++)
                {
                    if (LovePartnerRelationUtility.LovePartnerRelationExists(
                        pawns[i], pawns[j])) count++;
                }
            }
            return count;
        }

        private int CountExactDefAndPlansOutsidePrograms(ThingDef def,
            List<CASpaceProgram> programs)
        {
            if (def == null) return 0;
            var programIds = new HashSet<int>();
            for (int i = 0; i < programs.Count; i++)
                programIds.Add(programs[i].id);
            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            int count = 0;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].def == def
                    && ThingOutsidePrograms(buildings[i], def, programIds,
                        component)
                    && ThingInsideClaimedShelter(buildings[i]))
                    count++;
            ThingRequestGroup[] groups =
                { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame };
            for (int g = 0; g < groups.Length; g++)
            {
                List<Thing> things = map.listerThings.ThingsInGroup(groups[g]);
                for (int i = 0; i < things.Count; i++)
                {
                    ThingDef planned = things[i].def.entityDefToBuild as ThingDef;
                    if (planned == def
                        && ThingOutsidePrograms(things[i], planned, programIds,
                            component)
                        && ThingInsideClaimedShelter(things[i], planned))
                        count++;
                }
            }
            return count;
        }

        private int CountTableSeatsAndPlans()
        {
            var counted = new HashSet<int>();
            var tables = new List<Building>();
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].def.IsTable
                    && ThingInsideClaimedShelter(buildings[i])
                    && AnyResidentCanReach(buildings[i]))
                    tables.Add(buildings[i]);
            }
            int count = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building seat = buildings[i];
                if (seat.def.building == null || !seat.def.building.isSittable
                    || !ThingInsideClaimedShelter(seat)
                    || !counted.Add(seat.thingIDNumber)) continue;
                if (SeatTouchesUsableTable(seat, tables)) count++;
            }
            count += CountPlannedAtTables(def => def != null && def.building != null
                && def.building.isSittable, tables);
            return count;
        }

        private bool SeatTouchesAnyTable(IntVec3 cell, List<Building> tables)
        {
            for (int i = 0; i < tables.Count; i++)
            {
                CellRect ring = tables[i].OccupiedRect().ExpandedBy(1);
                if (ring.Contains(cell) && !tables[i].OccupiedRect().Contains(cell)
                    && !ring.IsCorner(cell)) return true;
            }
            return false;
        }

        private bool SeatTouchesUsableTable(Building seat, List<Building> tables)
        {
            for (int i = 0; i < tables.Count; i++)
            {
                Building table = tables[i];
                CellRect ring = table.OccupiedRect().ExpandedBy(1);
                if (ring.Contains(seat.Position)
                    && !table.OccupiedRect().Contains(seat.Position)
                    && !ring.IsCorner(seat.Position)
                    && AnyResidentCanReachBoth(table, seat)) return true;
            }
            return false;
        }

        private int CountBuildsAndPlans(Predicate<ThingDef> predicate)
        {
            int count = 0;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
                if (ThingInsideClaimedShelter(buildings[i])
                    && predicate(buildings[i].def)) count++;
            count += CountPlanned(predicate);
            return count;
        }

        private int CountPlanned(Predicate<ThingDef> predicate)
        {
            int count = 0;
            CountPlannedGroup(ThingRequestGroup.Blueprint, predicate, ref count, false);
            CountPlannedGroup(ThingRequestGroup.BuildingFrame, predicate, ref count, false);
            return count;
        }

        private int CountPlannedCapacity(Predicate<ThingDef> predicate)
        {
            int count = 0;
            CountPlannedGroup(ThingRequestGroup.Blueprint, predicate, ref count, true);
            CountPlannedGroup(ThingRequestGroup.BuildingFrame, predicate, ref count, true);
            return count;
        }

        private void CountPlannedGroup(ThingRequestGroup group,
            Predicate<ThingDef> predicate, ref int count, bool sleepingCapacity)
        {
            List<Thing> things = map.listerThings.ThingsInGroup(group);
            for (int i = 0; i < things.Count; i++)
            {
                ThingDef def = things[i].def.entityDefToBuild as ThingDef;
                if (def == null || !ThingInsideClaimedShelter(things[i], def)
                    || !predicate(def)) continue;
                count += sleepingCapacity ? Mathf.Max(1, def.size.x) : 1;
            }
        }

        private int CountPlannedAtTables(Predicate<ThingDef> predicate,
            List<Building> tables)
        {
            int count = 0;
            ThingRequestGroup[] groups =
                { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame };
            for (int g = 0; g < groups.Length; g++)
            {
                List<Thing> things = map.listerThings.ThingsInGroup(groups[g]);
                for (int i = 0; i < things.Count; i++)
                {
                    ThingDef def = things[i].def.entityDefToBuild as ThingDef;
                    if (def != null && ThingInsideClaimedShelter(things[i], def)
                        && predicate(def)
                        && SeatTouchesAnyTable(things[i].Position, tables)) count++;
                }
            }
            return count;
        }

        private Room FindFurnishedRoomNeedingLightFixture()
        {
            Room best = null;
            float bestScore = float.MinValue;
            IReadOnlyList<Room> rooms = map.regionGrid.AllRooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (!EligibleHomeRoom(room)) continue;
                bool furnished = RoomHas(room, def => def.IsBed || def.IsTable
                    || def.defName == "ChessTable");
                if (!furnished || RoomHasLightFixture(room)
                    || RoomHasPlannedLight(room)) continue;
                float score = ScoreRoom(room, CAHomePlanKind.Light);
                if (best == null || score > bestScore)
                {
                    best = room;
                    bestScore = score;
                }
            }
            return best;
        }

        private bool RoomHasLightFixture(Room room)
        {
            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                Building building = things[i] as Building;
                if (building == null || building.Faction != Faction.OfPlayer
                    || !IsLightDef(building.def)
                    || !ThingInsideClaimedShelter(building)) continue;
                return true;
            }
            return false;
        }

        private bool RoomHasPlannedLight(Room room)
        {
            ThingRequestGroup[] groups =
                { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame };
            for (int g = 0; g < groups.Length; g++)
            {
                List<Thing> things = map.listerThings.ThingsInGroup(groups[g]);
                for (int i = 0; i < things.Count; i++)
                {
                    ThingDef def = things[i].def.entityDefToBuild as ThingDef;
                    if (def != null && IsLightDef(def)
                        && things[i].Position.GetRoom(map) == room
                        && ThingInsideClaimedShelter(things[i], def))
                        return true;
                }
            }
            return false;
        }

        private int CountLightFixturesAndPlans()
        {
            int count = 0;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (IsLightDef(building.def)
                    && ThingInsideClaimedShelter(building)) count++;
            }
            count += CountPlanned(IsLightDef);
            return count;
        }

        private Building FindJoyBuildingNeedingSeat()
        {
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                string defName = building.def.defName;
                if (defName != "ChessTable"
                    || !ThingInsideClaimedShelter(building)
                    || !AnyResidentCanReach(building)) continue;
                if (!JoyTableHasSeat(building)) return building;
            }
            return null;
        }

        private bool ThingInsideClaimedShelter(Thing thing, ThingDef builtDef = null)
        {
            if (thing == null || !thing.Spawned || thing.Map != map) return false;
            ThingDef def = builtDef ?? thing.def;
            Room room = thing.Position.GetRoom(map);
            if (def == null || !EligibleHomeRoom(room)) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(
                thing.Position, thing.Rotation, def.Size))
            {
                if (!cell.InBounds(map) || !map.areaManager.Home[cell]
                    || !cell.Roofed(map) || cell.GetRoom(map) != room)
                    return false;
            }
            return true;
        }

        private bool ThingInsideHomeArea(Thing thing, ThingDef builtDef = null)
        {
            if (thing == null || !thing.Spawned || thing.Map != map) return false;
            ThingDef def = builtDef ?? thing.def;
            if (def == null) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(
                thing.Position, thing.Rotation, def.Size))
            {
                if (!cell.InBounds(map) || !map.areaManager.Home[cell]) return false;
            }
            return true;
        }

        private int CountUsableRecreationAndPlans()
        {
            int count = 0;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                string defName = building.def.defName;
                if (defName != "ChessTable" && defName != "HorseshoesPin")
                    continue;
                bool properLocation = defName == "ChessTable"
                    ? ThingInsideClaimedShelter(building)
                    : ThingInsideHomeArea(building);
                if (!properLocation || !BuildingIsOperational(building)
                    || !AnyResidentCanReach(building))
                    continue;
                if (defName == "ChessTable" && !JoyTableHasSeat(building)) continue;
                count++;
            }

            ThingRequestGroup[] groups =
                { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame };
            for (int g = 0; g < groups.Length; g++)
            {
                List<Thing> things = map.listerThings.ThingsInGroup(groups[g]);
                for (int i = 0; i < things.Count; i++)
                {
                    ThingDef def = things[i].def.entityDefToBuild as ThingDef;
                    if (def == null || (def.defName != "ChessTable"
                        && def.defName != "HorseshoesPin")) continue;
                    bool properLocation = def.defName == "ChessTable"
                        ? ThingInsideClaimedShelter(things[i], def)
                        : ThingInsideHomeArea(things[i], def);
                    if (properLocation) count++;
                }
            }
            return count;
        }

        private bool JoyTableHasSeat(Building building)
        {
            List<IntVec3> cells = GenAdjFast.AdjacentCellsCardinal(building);
            for (int i = 0; i < cells.Count; i++)
            {
                Building seat = cells[i].GetEdifice(map);
                if (seat != null && seat.def.building != null
                    && seat.def.building.isSittable
                    && ThingInsideClaimedShelter(seat)
                    && AnyResidentCanReachBoth(building, seat)) return true;
            }
            return false;
        }

        private bool BuildingIsOperational(Building building)
        {
            if (building == null || building.IsBrokenDown()
                || !FlickUtility.WantsToBeOn(building)) return false;
            CompPowerTrader power = building.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return false;
            CompRefuelable fuel = building.TryGetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel) return false;
            return true;
        }

        private bool AnyResidentCanReach(Building building)
        {
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Downed
                    || pawn.InMentalState || !pawn.Awake()
                    || building.IsForbidden(pawn)
                    || !building.Position.InAllowedArea(pawn)) continue;
                if (pawn.CanReach(building, PathEndMode.Touch, Danger.Some)) return true;
            }
            return false;
        }

        private bool AnyResidentCanReachBoth(Building first, Building second)
        {
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Downed
                    || pawn.InMentalState || !pawn.Awake()
                    || first.IsForbidden(pawn) || second.IsForbidden(pawn)
                    || !first.Position.InAllowedArea(pawn)
                    || !second.Position.InAllowedArea(pawn)) continue;
                if (pawn.CanReach(first, PathEndMode.Touch, Danger.Some)
                    && pawn.CanReach(second, PathEndMode.Touch, Danger.Some))
                    return true;
            }
            return false;
        }

        private bool RoomHas(Room room, Predicate<ThingDef> predicate)
        {
            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                Building building = things[i] as Building;
                if (building != null && building.Faction == Faction.OfPlayer
                    && ThingInsideClaimedShelter(building)
                    && predicate(building.def)) return true;
            }
            return false;
        }

        private bool IsLightDef(ThingDef def)
        {
            return def == ThingDefOf.StandingLamp || def == ThingDefOf.TorchLamp;
        }

        private int SkillLevel(Pawn pawn, SkillDef skill)
        {
            return pawn?.skills != null ? pawn.skills.GetSkill(skill).Level : 0;
        }

        private string BlockerSuffix(string blocker)
        {
            return blocker.NullOrEmpty() ? "" : " (" + blocker + ")";
        }

        public bool TryGetCurrentPlanTarget(out IntVec3 cell, out string label)
        {
            if (!pendingDefName.NullOrEmpty() && pendingCell.IsValid)
            {
                cell = pendingCell;
                label = "pending " + pendingDefName;
                return true;
            }
            if (!lastPlannedDef.NullOrEmpty() && lastPlannedCell.IsValid)
            {
                cell = lastPlannedCell;
                label = "last planned " + lastPlannedDef;
                return true;
            }
            cell = IntVec3.Invalid;
            label = "no automatic home plan";
            return false;
        }

        public string Census()
        {
            PruneCompletedRecords();
            int people = RestingColonistCount();
            int beds = CountBedSlotsAndPlans();
            int tables = CountBuildsAndPlans(def => def != null && def.IsTable);
            int seats = CountTableSeatsAndPlans();
            int lights = CountLightFixturesAndPlans();
            int joy = CountUsableRecreationAndPlans();
            string pending = pendingDefName.NullOrEmpty() ? "none"
                : pendingDefName + " at " + pendingCell + " since " + pendingSinceTick;
            var suppressedEntries = new List<string>();
            foreach (KeyValuePair<string, int> entry in suppressedKinds)
                suppressedEntries.Add(entry.Key + " until "
                    + (entry.Value == int.MaxValue ? "reset" : entry.Value.ToString()));
            string suppressed = suppressedEntries.Count == 0 ? "none"
                : string.Join(", ", suppressedEntries.ToArray());
            string last = lastPlannedDef.NullOrEmpty() ? "none"
                : lastPlannedDef + (lastPlannedStuff.NullOrEmpty() ? ""
                    : " using " + lastPlannedStuff) + " at "
                    + lastPlannedCell + " on tick "
                    + lastPlannedTick + " by pawn " + lastPlannerId + ": " + lastReason;
            string placement = lastPlacementEvidence.NullOrEmpty()
                ? "none" : lastPlacementEvidence;
            string plannedUses = PlannedUseMapComponent.For(map)?.Census()
                ?? "[CA] space programs: unavailable";
            string prerequisites = CAHomePrerequisiteMapComponent.For(map)?.Census()
                ?? "[CA] home prerequisites: unavailable";
            return "[CA] home planning: people " + people + ", sleeping slots " + beds
                + ", tables " + tables + ", table seats " + seats + ", lights "
                + lights + ", usable/in-progress recreation " + joy
                + "; pending " + pending
                + (pendingBehaviorKey.NullOrEmpty() ? ""
                    : "; pending behavior " + pendingBehaviorKey
                        + " episode " + pendingEpisodeId + " authority "
                        + (pendingAuthorityIdentity ?? "unknown")
                        + " controller "
                        + ((CAIntentController)pendingIntentController))
                + "; suppressed " + suppressed + "; last " + last
                + "; placement evidence " + placement
                + "; outcome " + lastOutcome + "; " + plannedUses
                + "; " + prerequisites;
        }
    }

    [HarmonyPatch(typeof(Designator_Cancel), nameof(Designator_Cancel.DesignateThing))]
    internal static class Patch_CAHomePlanCancel
    {
        private static void Prefix(Thing t)
        {
            AutonomousHomeMapComponent.For(t?.Map)?.NotifyExplicitPlanCancel(t);
            CAHomePrerequisiteMapComponent.For(t?.Map)?.NotifyPlayerCancel(t);
        }
    }

    [HarmonyPatch(typeof(Designator_Cancel),
        nameof(Designator_Cancel.DesignateSingleCell))]
    internal static class Patch_CAHomeSourceCellCancel
    {
        private static void Prefix(Designator_Cancel __instance, IntVec3 c)
        {
            CAHomePrerequisiteMapComponent.For(__instance?.Map)
                ?.NotifyPlayerCancelAt(c);
        }
    }

    [HarmonyPatch(typeof(Designator_Deconstruct),
        nameof(Designator_Deconstruct.DesignateThing))]
    internal static class Patch_CAHomeExplicitDeconstruct
    {
        private static void Prefix(Thing t)
        {
            AutonomousHomeMapComponent.For(t?.Map)?.NotifyExplicitDeconstruct(t);
        }
    }

    [HarmonyPatch(typeof(Blueprint), nameof(Blueprint.TryReplaceWithSolidThing))]
    internal static class Patch_CAHomeBlueprintCompleted
    {
        private static void Prefix(Blueprint __instance, out string __state)
        {
            __state = __instance?.ThingID;
        }

        private static void Postfix(bool __result, Thing createdThing,
            string __state)
        {
            if (!__result || createdThing == null) return;
            AutonomousHomeMapComponent.For(createdThing.Map)
                ?.NotifyBlueprintReplaced(__state, createdThing);
        }
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    internal static class Patch_CAHomeFrameCompleted
    {
        private struct CompletionState
        {
            public Map map;
            public IntVec3 cell;
            public string defName;
            public string thingId;
        }

        private static void Prefix(Frame __instance, Pawn worker,
            out CompletionState __state)
        {
            AutonomousHomeMapComponent.For(__instance.Map)
                ?.NotifyFrameWillComplete(__instance, worker);
            __state = new CompletionState
            {
                map = __instance.Map,
                cell = __instance.Position,
                defName = __instance.BuildDef?.defName,
                thingId = __instance.ThingID
            };
        }

        private static void Postfix(CompletionState __state)
        {
            CAAgentDebugBridge.ForCurrentGame()?
                .CompleteBedroomCauseMaterialConsumption(__state.map,
                    __state.thingId);
            AutonomousHomeMapComponent.For(__state.map)
                ?.NotifyFrameCompleted(__state.thingId, __state.cell,
                    __state.defName);
        }
    }
}
