using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal sealed class CAHomeMaterialRequirement : IExposable
    {
        public string defName;
        public int required;
        public int available;
        public int deficit;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref required, "required", 0);
            Scribe_Values.Look(ref available, "available", 0);
            Scribe_Values.Look(ref deficit, "deficit", 0);
        }
    }

    internal sealed class CAHomeMaterialDemand : IExposable
    {
        private const int CurrentInitiativeSchema = 1;
        public string provisionDefName;
        public string stuffDefName;
        public string kind;
        public int programId;
        public IntVec3 cell = IntVec3.Invalid;
        public int rotation;
        public string reason;
        public string placementEvidence;
        public int targetResidentId;
        public int authorPawnId = -1;
        public int authorFactionId = -1;
        public int producerPawnId = -1;
        public CAInitiativeTier minimumInitiative =
            CAInitiativeTier.Proactive;
        private int initiativeSchema = CurrentInitiativeSchema;
        private int legacyMinimumAutonomy = 2;
        public bool outdoor;
        public bool seatForJoy;
        public int createdTick = -1;
        public int updatedTick = -1;
        public string status;
        public string behaviorKey;
        public int episodeId;
        public CAIntentOrigin intentOrigin;
        public CAIntentController intentController;
        public CAAuthorityOrigin authorityOrigin;
        public string authorityIdentity;
        public string ownershipScope;
        public int issuerId = -1;
        public int ownerId = -1;
        public int intentCreatedTick = -1;
        public CAInitiativeTier creationTier = CAInitiativeTier.Standard;
        public string targetOrDemand;
        public string terminationCondition;
        public List<CAHomeMaterialRequirement> requirements =
            new List<CAHomeMaterialRequirement>();

        public bool Matches(CAHomePlan plan)
        {
            return plan.def != null && provisionDefName == plan.def.defName
                && stuffDefName == plan.stuff?.defName
                && kind == plan.kind.ToString()
                && programId == (plan.program?.id ?? 0)
                && cell == plan.cell && rotation == plan.rotation.AsInt
                && targetResidentId == plan.targetResidentId
                && behaviorKey == plan.behaviorKey
                && episodeId == plan.episodeId
                && intentController == plan.intentController
                && minimumInitiative == (plan.minimumInitiative
                    < CAInitiativeTier.Proactive
                        ? CAInitiativeTier.Proactive
                        : plan.minimumInitiative)
                && outdoor == plan.outdoor
                && seatForJoy == plan.seatForJoy;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref provisionDefName, "provisionDefName");
            Scribe_Values.Look(ref stuffDefName, "stuffDefName");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref programId, "programId", 0);
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref rotation, "rotation", 0);
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref placementEvidence, "placementEvidence");
            Scribe_Values.Look(ref targetResidentId, "targetResidentId", 0);
            Scribe_Values.Look(ref authorPawnId, "authorPawnId", -1);
            Scribe_Values.Look(ref authorFactionId, "authorFactionId", -1);
            Scribe_Values.Look(ref producerPawnId, "producerPawnId", -1);
            Scribe_Values.Look(ref initiativeSchema, "initiativeSchema", 0);
            if (Scribe.mode == LoadSaveMode.Saving
                || initiativeSchema >= CurrentInitiativeSchema)
            {
                Scribe_Values.Look(ref minimumInitiative,
                    "minimumInitiative", CAInitiativeTier.Proactive);
            }
            else
            {
                Scribe_Values.Look(ref legacyMinimumAutonomy,
                    "minimumAutonomy", 2);
            }
            Scribe_Values.Look(ref outdoor, "outdoor", false);
            Scribe_Values.Look(ref seatForJoy, "seatForJoy", false);
            Scribe_Values.Look(ref createdTick, "createdTick", -1);
            Scribe_Values.Look(ref updatedTick, "updatedTick", -1);
            Scribe_Values.Look(ref status, "status");
            Scribe_Values.Look(ref behaviorKey, "behaviorKey");
            Scribe_Values.Look(ref episodeId, "episodeId", 0);
            Scribe_Values.Look(ref intentOrigin, "intentOrigin",
                CAIntentOrigin.Unknown);
            Scribe_Values.Look(ref intentController, "intentController",
                CAIntentController.Unknown);
            Scribe_Values.Look(ref authorityOrigin, "authorityOrigin",
                CAAuthorityOrigin.None);
            Scribe_Values.Look(ref authorityIdentity, "authorityIdentity");
            Scribe_Values.Look(ref ownershipScope, "ownershipScope");
            Scribe_Values.Look(ref issuerId, "issuerId", -1);
            Scribe_Values.Look(ref ownerId, "ownerId", -1);
            Scribe_Values.Look(ref intentCreatedTick, "intentCreatedTick", -1);
            Scribe_Values.Look(ref creationTier, "creationTier",
                CAInitiativeTier.Standard);
            Scribe_Values.Look(ref targetOrDemand, "targetOrDemand");
            Scribe_Values.Look(ref terminationCondition,
                "terminationCondition");
            Scribe_Collections.Look(ref requirements, "requirements",
                LookMode.Deep);
            if (requirements == null)
                requirements = new List<CAHomeMaterialRequirement>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (initiativeSchema < CurrentInitiativeSchema)
                {
                    minimumInitiative = AutonomyComponent
                        .MigrateLegacyLevel(legacyMinimumAutonomy);
                    initiativeSchema = CurrentInitiativeSchema;
                }
                minimumInitiative = AutonomyComponent.Normalize(
                    minimumInitiative);
                CACombatIntent.ObserveEpisode(episodeId);
            }
        }
    }

    internal sealed class CAHomeTreeCandidate
    {
        public Plant plant;
        public int expectedWood;
        public float score;
    }

    // Saved settlement-objective prerequisites. This component owns neither the
    // originating need nor any pawn job. It records an exact material vector and
    // exposes bounded native designations that ordinary work may satisfy.
    internal sealed class CAHomePrerequisiteMapComponent : MapComponent
    {
        private const float HomeSecurityBuffer = 6.9f;
        private const float DefenseProgramBuffer = 8.9f;
        private const float DefensiveWorkBuffer = 5.9f;
        private const int MaximumTreeSources = 12;

        private CAHomeMaterialDemand demand;
        private List<string> ownedTreeSourceIds = new List<string>();
        private List<string> playerVetoedTreeIds = new List<string>();
        private string lastOutcome = "no material demand";
        private int nextSafetyCheckTick;

        public CAHomePrerequisiteMapComponent(Map map) : base(map) { }

        public bool HasDemand => demand != null;

        internal bool TryVerifyContextualBedDemand(CASpaceProgram program,
            out string detail)
        {
            if (demand == null)
            {
                detail = "no retained material demand exists";
                return false;
            }
            if (program == null || demand.kind != CAHomePlanKind.Bed.ToString()
                || demand.programId != program.id
                || !program.cells.Contains(demand.cell))
            {
                detail = "the retained demand is not a Bed inside program "
                    + (program?.id ?? 0);
                return false;
            }
            if (!HasContextualBedPlacementComparisonEvidence(
                demand.placementEvidence))
            {
                detail = "the retained Bed lacks a receipted settlement-context "
                    + "comparison";
                return false;
            }
            detail = DemandSummary(demand);
            return true;
        }

        internal static bool HasContextualBedPlacementEvidence(string evidence)
        {
            return HasContextualBedPlacementComparisonEvidence(evidence)
                && evidence.Contains("; ranking changed yes;");
        }

        internal static bool HasContextualBedPlacementComparisonEvidence(
            string evidence)
        {
            return !evidence.NullOrEmpty()
                && evidence.Contains("settlement context revision ")
                && evidence.Contains(" re-ranked Autonomous Bed candidates "
                    + "inside player-authored ")
                && evidence.Contains("; selected ")
                && evidence.Contains("; ranking changed ");
        }

        public static CAHomePrerequisiteMapComponent For(Map map)
        {
            return map?.GetComponent<CAHomePrerequisiteMapComponent>();
        }

        internal bool HasAuthorizedWoodSupply()
        {
            Pawn forester = ChooseForester();
            if (forester == null
                || KnowledgeMapComponent.For(map)?.KnowsAnyThreat(forester)
                    == true)
                return false;
            if (ExpectedDesignatedWood(forester, ownedOnly: true) > 0
                || ExpectedDesignatedWood(forester, ownedOnly: false) > 0)
                return true;

            List<CAHomeTreeCandidate> candidates = RankedTreeCandidates(forester);
            int reachChecks = 0;
            for (int i = 0; i < candidates.Count && reachChecks < 96; i++)
            {
                reachChecks++;
                if (forester.CanReach(candidates[i].plant, PathEndMode.Touch,
                    Danger.Some)) return true;
            }
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref demand, "CA_homeMaterialDemand");
            Scribe_Collections.Look(ref ownedTreeSourceIds,
                "CA_homeOwnedTreeSources", LookMode.Value);
            Scribe_Collections.Look(ref playerVetoedTreeIds,
                "CA_homePlayerVetoedTrees", LookMode.Value);
            Scribe_Values.Look(ref lastOutcome, "CA_homeMaterialLastOutcome",
                "no material demand");
            Scribe_Values.Look(ref nextSafetyCheckTick,
                "CA_homeMaterialNextSafetyCheck", 0);
            if (ownedTreeSourceIds == null)
                ownedTreeSourceIds = new List<string>();
            if (playerVetoedTreeIds == null)
                playerVetoedTreeIds = new List<string>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            PruneSourceProvenance();
            PruneVetoes();
        }

        public override void MapComponentTick()
        {
            if (demand == null || ownedTreeSourceIds.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            if (now < nextSafetyCheckTick) return;
            nextSafetyCheckTick = now + 30;
            if (!map.IsPlayerHome)
            {
                CancelUnselectedObjective(
                    "the map is no longer an authorized player home");
                return;
            }
            Pawn producer = FindPawn(demand.producerPawnId);
            RevalidateOwnedSources(producer);
            if (ownedTreeSourceIds.Count == 0) return;
            if (producer != null
                && KnowledgeMapComponent.For(map)?.KnowsAnyThreat(producer) == true)
            {
                CancelOwnedSources(producer.LabelShort
                    + " knows an active threat");
                demand.status = producer.LabelShort
                    + " suspended forestry on a fresh threat fact";
                lastOutcome = DemandSummary(demand);
            }
        }

        public bool Prepare(Pawn planner, CAHomePlan plan, out string outcome)
        {
            if (planner == null || planner.Map != map || plan.def == null)
            {
                outcome = "the material objective has no valid author or provision";
                lastOutcome = outcome;
                return false;
            }

            var proposedDeficits = new List<string>();
            List<ThingDefCountClass> proposedCosts = plan.def.CostListAdjusted(
                plan.stuff, errorOnNullStuff: false);
            for (int i = 0; i < proposedCosts.Count; i++)
            {
                ThingDefCountClass cost = proposedCosts[i];
                if (cost?.thingDef == null || cost.count <= 0) continue;
                int available = AvailableCountFor(planner, cost.thingDef,
                    cost.count);
                if (available < cost.count)
                    proposedDeficits.Add(cost.thingDef.defName + " "
                        + available + "/" + cost.count + " available");
            }
            bool hasDeficit = proposedDeficits.Count > 0;
            if (hasDeficit)
            {
                CASpaceProgram program = plan.program;
                CAInitiativeTier ceiling = program != null
                    ? CASpatialInitiativeMapComponent.For(map)?.TierFor(program)
                        ?? CAInitiativeTier.Standard
                    : CAInitiativeTier.Standard;
                bool foreignPlayerWork = CATactical
                    .HasForeignPlayerForcedJob(planner);
                var stagingContext = new CABehaviorContext(planner,
                    CAActorContext.PlayerPawn
                        | CAActorContext.PlayerSpatialAuthority,
                    AutonomyComponent.TierOf(planner),
                    CAAuthorityOrigin.PlayerDelegated,
                    authoritySatisfied: program != null
                        && program.author == CASpaceAuthor.Player,
                    knowledgeSatisfied: hasDeficit,
                    knowledgeFresh: hasDeficit,
                    liveValidated: planner.Spawned && planner.Map == map,
                    knowledgeRelayed: false,
                    knowledgeAgeTicks: 0, knowledgeConfidence: 1f,
                    knowledgeUncertainty: 0f,
                    capabilitySatisfied: proposedCosts.Count > 0,
                    materialSatisfied: true,
                    currentIntentCompatible: !foreignPlayerWork,
                    directPlayerOwnership: foreignPlayerWork,
                    authorityCeiling: ceiling,
                    authorityBasis: "player-authored space program #"
                        + (program?.id ?? 0),
                    knowledgeBasis: "exact native construction cost deficit: "
                        + string.Join(", ", proposedDeficits.ToArray()),
                    owner: "home material objective");
                CABehaviorDecision stagingDecision = CABehaviorGate.Evaluate(
                    "logistics.material_staging", stagingContext);
                if (!stagingDecision.Allowed)
                {
                    outcome = "material staging blocked: "
                        + stagingDecision.PrimaryReason;
                    lastOutcome = outcome;
                    return false;
                }
            }

            if (demand == null || !demand.Matches(plan))
            {
                if (demand != null)
                {
                    CAAgentDebugBridge.ForCurrentGame()?
                        .RetireBedroomCausePlanningScope(map,
                            demand.programId, demand.targetResidentId,
                            null, "the originating material demand changed");
                    CAStorageProgramMapComponent.For(map)
                        ?.NotifyObjectiveClosed(demand);
                }
                CancelOwnedSources("the originating objective changed");
                demand = NewDemand(planner, plan);
            }
            else
            {
                demand.updatedTick = Find.TickManager.TicksGame;
            }

            RefreshRequirements(planner, plan);
            if (demand.requirements.All(requirement => requirement.deficit <= 0))
            {
                CancelOwnedSources("the exact material demand is now available");
                demand.status = "material vector satisfied; native blueprint may be placed";
                lastOutcome = DemandSummary(demand);
                outcome = lastOutcome;
                return true;
            }

            var unsupported = new List<string>();
            for (int i = 0; i < demand.requirements.Count; i++)
            {
                CAHomeMaterialRequirement requirement = demand.requirements[i];
                if (requirement.deficit <= 0) continue;
                ThingDef material = DefDatabase<ThingDef>.GetNamedSilentFail(
                    requirement.defName);
                if (material != ThingDefOf.WoodLog)
                    unsupported.Add(requirement.defName + " "
                        + requirement.deficit);
            }

            if (unsupported.Count > 0)
            {
                CancelOwnedSources(
                    "the objective has an unsupported material deficit");
                demand.status = "no authorized producer for "
                    + string.Join(", ", unsupported.ToArray());
                demand.updatedTick = Find.TickManager.TicksGame;
                lastOutcome = DemandSummary(demand);
                outcome = lastOutcome;
                return false;
            }
            for (int i = 0; i < demand.requirements.Count; i++)
            {
                CAHomeMaterialRequirement requirement = demand.requirements[i];
                if (requirement.deficit <= 0
                    || DefDatabase<ThingDef>.GetNamedSilentFail(
                        requirement.defName) != ThingDefOf.WoodLog) continue;
                string storage = "storage component unavailable";
                CAStorageProgramMapComponent storageComponent =
                    CAStorageProgramMapComponent.For(map);
                if (storageComponent == null
                    || !storageComponent.EnsureConstructionStaging(demand,
                        requirement, planner, out storage))
                {
                    CancelOwnedSources("the exact material demand lacks authorized storage");
                    demand.status = "wood deficit " + requirement.deficit + "; "
                        + storage;
                    demand.updatedTick = Find.TickManager.TicksGame;
                    lastOutcome = DemandSummary(demand);
                    outcome = lastOutcome;
                    return false;
                }
                string forestry;
                AdvanceWoodProducer(requirement.deficit, out forestry);
                demand.status = forestry + "; storage " + storage;
            }
            demand.updatedTick = Find.TickManager.TicksGame;
            lastOutcome = DemandSummary(demand);
            outcome = lastOutcome;
            return false;
        }

        public void NotifyBlueprintPlaced(CAHomePlan plan)
        {
            if (demand == null || !demand.Matches(plan)) return;
            CAAgentDebugBridge.ForCurrentGame()?
                .TransferBedroomCausePlanningScopeToConstruction(map,
                    demand.programId, demand.targetResidentId,
                    AutonomousHomeMapComponent.For(map)?
                        .PendingOriginThingIdForVerification);
            CancelOwnedSources("the native blueprint now owns the objective");
            CAStorageProgramMapComponent.For(map)?.NotifyObjectiveClosed(demand);
            lastOutcome = "material demand transferred to native "
                + demand.provisionDefName + " blueprint at " + demand.cell;
            demand = null;
        }

        public void CancelUnselectedObjective(string reason)
        {
            if (demand == null && ownedTreeSourceIds.Count == 0) return;
            if (demand != null)
                CAAgentDebugBridge.ForCurrentGame()?
                    .RetireBedroomCausePlanningScope(map, demand.programId,
                        demand.targetResidentId, null, reason);
            CancelOwnedSources(reason);
            CAStorageProgramMapComponent.For(map)?.NotifyObjectiveClosed(demand);
            demand = null;
            lastOutcome = reason;
        }

        public void RetainObjective(string reason)
        {
            if (demand == null) return;
            demand.status = reason + "; retained from its authorized objective";
            demand.updatedTick = Find.TickManager.TicksGame;
            lastOutcome = DemandSummary(demand);
        }

        public bool TryRehydrate(out CAHomePlan plan, out string problem)
        {
            plan = default(CAHomePlan);
            problem = null;
            if (demand == null) return false;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(
                demand.provisionDefName);
            ThingDef stuff = demand.stuffDefName.NullOrEmpty() ? null
                : DefDatabase<ThingDef>.GetNamedSilentFail(demand.stuffDefName);
            CAHomePlanKind kind;
            if (def == null)
            {
                problem = "provision " + demand.provisionDefName
                    + " is unavailable";
                return false;
            }
            if (!demand.stuffDefName.NullOrEmpty() && stuff == null)
            {
                problem = "material " + demand.stuffDefName + " is unavailable";
                return false;
            }
            if (!Enum.TryParse(demand.kind, out kind))
            {
                problem = "provision kind " + demand.kind + " is unavailable";
                return false;
            }
            CASpaceProgram program = null;
            if (demand.programId != 0)
            {
                program = PlannedUseMapComponent.For(map)
                    ?.FindProgram(demand.programId);
                if (program == null)
                {
                    problem = "authored space program " + demand.programId
                        + " no longer exists";
                    return false;
                }
            }
            plan = new CAHomePlan
            {
                def = def,
                stuff = stuff,
                kind = kind,
                program = program,
                cell = demand.cell,
                rotation = new Rot4(demand.rotation),
                reason = demand.reason,
                placementEvidence = demand.placementEvidence,
                targetResidentId = demand.targetResidentId,
                minimumInitiative = demand.minimumInitiative
                    < CAInitiativeTier.Proactive
                        ? CAInitiativeTier.Proactive
                        : demand.minimumInitiative,
                outdoor = demand.outdoor,
                seatForJoy = demand.seatForJoy,
                behaviorKey = demand.behaviorKey,
                episodeId = demand.episodeId,
                intentOrigin = demand.intentOrigin,
                intentController = demand.intentController,
                authorityOrigin = demand.authorityOrigin,
                authorityIdentity = demand.authorityIdentity,
                ownershipScope = demand.ownershipScope,
                issuerId = demand.issuerId,
                ownerId = demand.ownerId,
                createdTick = demand.intentCreatedTick,
                creationTier = demand.creationTier,
                targetOrDemand = demand.targetOrDemand,
                terminationCondition = demand.terminationCondition
            };
            CACombatIntent.ObserveEpisode(plan.episodeId);
            return true;
        }

        public void NotifyPlayerCancel(Thing thing)
        {
            if (thing == null || thing.Map != map
                || !ownedTreeSourceIds.Remove(thing.ThingID)) return;
            if (!playerVetoedTreeIds.Contains(thing.ThingID))
                playerVetoedTreeIds.Add(thing.ThingID);
            if (demand != null)
                demand.status = thing.LabelShort
                    + " removed from automatic forestry by player veto";
            lastOutcome = demand != null ? DemandSummary(demand)
                : "player vetoed automatic forestry on " + thing.LabelShort;
        }

        public void NotifyPlayerCancelAt(IntVec3 cell)
        {
            if (!cell.InBounds(map) || ownedTreeSourceIds.Count == 0) return;
            List<Thing> things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = things[i];
                if (thing != null
                    && ownedTreeSourceIds.Contains(thing.ThingID)
                    && map.designationManager.DesignationOn(thing,
                        DesignationDefOf.HarvestPlant) != null)
                    NotifyPlayerCancel(thing);
            }
        }

        public void NotifyPlayerPlantDesignation(Thing thing)
        {
            if (thing == null || thing.Map != map) return;
            if (ownedTreeSourceIds.Remove(thing.ThingID) && demand != null)
            {
                demand.status = thing.LabelShort
                    + " passed from CA source provenance to player designation";
                lastOutcome = DemandSummary(demand);
            }
        }

        public string Census()
        {
            PruneSourceProvenance();
            PruneVetoes();
            var sources = new List<string>();
            List<Plant> sourcePlants = OwnedSourcePlants();
            for (int i = 0; i < sourcePlants.Count; i++)
                sources.Add(sourcePlants[i].LabelShort + " "
                    + sourcePlants[i].Position);
            Pawn producer = demand != null ? FindPawn(demand.producerPawnId) : null;
            string producerDetail = producer == null ? "none"
                : producer.LabelShort + " Plants "
                    + SkillLevel(producer, SkillDefOf.Plants) + ", Intellectual "
                    + SkillLevel(producer, SkillDefOf.Intellectual)
                    + ", judgment " + ForestryJudgment(producer).ToString("F2");
            return "[CA] home prerequisites: " + (demand == null
                ? "none"
                : DemandSummary(demand)) + "; CA tree sources "
                + ownedTreeSourceIds.Count + " ["
                + (sources.Count == 0 ? "none" : string.Join(", ", sources.ToArray()))
                + "]; forester " + producerDetail + "; player-vetoed trees "
                + playerVetoedTreeIds.Count + "; last " + lastOutcome;
        }

        // Read-only form used by the settlement behavior census. Lifecycle
        // repair remains owned by component ticks and the existing detailed
        // developer receipt.
        internal string CensusForObservation()
        {
            return demand == null ? "no active material demand; last "
                + lastOutcome : DemandSummary(demand) + "; CA tree source "
                + "records " + ownedTreeSourceIds.Count + "; player vetoes "
                + playerVetoedTreeIds.Count;
        }

        private CAHomeMaterialDemand NewDemand(Pawn planner, CAHomePlan plan)
        {
            int now = Find.TickManager.TicksGame;
            return new CAHomeMaterialDemand
            {
                provisionDefName = plan.def.defName,
                stuffDefName = plan.stuff?.defName,
                kind = plan.kind.ToString(),
                programId = plan.program?.id ?? 0,
                cell = plan.cell,
                rotation = plan.rotation.AsInt,
                reason = plan.reason,
                placementEvidence = plan.placementEvidence,
                targetResidentId = plan.targetResidentId,
                authorPawnId = planner.thingIDNumber,
                authorFactionId = planner.Faction?.loadID ?? -1,
                minimumInitiative = plan.minimumInitiative
                    < CAInitiativeTier.Proactive
                        ? CAInitiativeTier.Proactive
                        : plan.minimumInitiative,
                outdoor = plan.outdoor,
                seatForJoy = plan.seatForJoy,
                behaviorKey = plan.behaviorKey,
                episodeId = plan.episodeId,
                intentOrigin = plan.intentOrigin,
                intentController = plan.intentController,
                authorityOrigin = plan.authorityOrigin,
                authorityIdentity = plan.authorityIdentity,
                ownershipScope = plan.ownershipScope,
                issuerId = plan.issuerId,
                ownerId = plan.ownerId,
                intentCreatedTick = plan.createdTick,
                creationTier = plan.creationTier,
                targetOrDemand = plan.targetOrDemand,
                terminationCondition = plan.terminationCondition,
                createdTick = now,
                updatedTick = now,
                status = "material vector created"
            };
        }

        private void RefreshRequirements(Pawn planner, CAHomePlan plan)
        {
            demand.requirements.Clear();
            List<ThingDefCountClass> costs = plan.def.CostListAdjusted(plan.stuff,
                errorOnNullStuff: false);
            for (int i = 0; i < costs.Count; i++)
            {
                ThingDefCountClass cost = costs[i];
                if (cost?.thingDef == null || cost.count <= 0) continue;
                int available = AvailableCountFor(planner, cost.thingDef,
                    cost.count);
                demand.requirements.Add(new CAHomeMaterialRequirement
                {
                    defName = cost.thingDef.defName,
                    required = cost.count,
                    available = available,
                    deficit = Mathf.Max(0, cost.count - available)
                });
            }
        }

        private int AvailableCountFor(Pawn planner, ThingDef def, int stopAt)
        {
            int count = 0;
            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || !thing.Spawned || thing.IsForbidden(planner)
                    || !planner.CanReach(thing, PathEndMode.Touch, Danger.Some))
                    continue;
                count += thing.stackCount;
                if (count >= stopAt) break;
            }
            return count;
        }

        private void AdvanceWoodProducer(int deficit, out string outcome)
        {
            PruneSourceProvenance();
            PruneVetoes();
            Pawn forester = ChooseForester();
            if (forester == null)
            {
                demand.producerPawnId = -1;
                outcome = "wood deficit " + deficit
                    + "; no awake Proactive+ pawn has Plant Cutting enabled";
                return;
            }
            demand.producerPawnId = forester.thingIDNumber;
            RevalidateOwnedSources(forester);
            if (KnowledgeMapComponent.For(map)?.KnowsAnyThreat(forester) == true)
            {
                CancelOwnedSources(forester.LabelShort
                    + " knows an active threat");
                outcome = "wood deficit " + deficit + "; "
                    + forester.LabelShort
                    + " suspended forestry on a fresh threat fact";
                return;
            }

            int incomingOwned = ExpectedDesignatedWood(forester, ownedOnly: true);
            int incomingPlayer = ExpectedDesignatedWood(forester,
                ownedOnly: false);
            int remaining = deficit - incomingOwned - incomingPlayer;
            if (remaining <= 0)
            {
                outcome = "wood deficit " + deficit + "; awaiting about "
                    + incomingOwned + " CA-designated and " + incomingPlayer
                    + " player-designated wood through native plant work";
                return;
            }

            var selected = OwnedSourcePlants();
            int added = 0;
            int expectedAdded = 0;
            int reachChecks = 0;
            Plant firstAdded = null;
            List<CAHomeTreeCandidate> candidates = RankedTreeCandidates(forester);
            CASpaceProgram program = PlannedUseMapComponent.For(map)
                ?.FindProgram(demand.programId);
            CAInitiativeTier ceiling = program != null
                ? CASpatialInitiativeMapComponent.For(map)?.TierFor(program)
                    ?? CAInitiativeTier.Standard
                : CAInitiativeTier.Standard;
            bool foreignPlayerWork = CATactical
                .HasForeignPlayerForcedJob(forester);
            var sourceContext = new CABehaviorContext(forester,
                CAActorContext.PlayerPawn
                    | CAActorContext.PlayerSpatialAuthority,
                AutonomyComponent.TierOf(forester),
                CAAuthorityOrigin.PlayerDelegated,
                authoritySatisfied: program != null
                    && program.author == CASpaceAuthor.Player,
                knowledgeSatisfied: deficit > 0, knowledgeFresh: true,
                liveValidated: forester.Spawned && forester.Map == map,
                knowledgeRelayed: false,
                knowledgeAgeTicks: 0, knowledgeConfidence: 1f,
                knowledgeUncertainty: 0f,
                capabilitySatisfied: candidates.Count > 0,
                materialSatisfied: candidates.Count > 0,
                currentIntentCompatible: !foreignPlayerWork,
                directPlayerOwnership: foreignPlayerWork,
                authorityCeiling: ceiling,
                authorityBasis: "player-authored material source for program #"
                    + demand.programId,
                knowledgeBasis: "persisted exact wood deficit " + deficit,
                owner: "home material source objective");
            CABehaviorDecision sourceDecision = CABehaviorGate.Evaluate(
                "logistics.material_source", sourceContext);
            if (!sourceDecision.Allowed)
            {
                outcome = "wood deficit " + deficit + "; source blocked: "
                    + sourceDecision.PrimaryReason;
                return;
            }
            for (int i = 0; i < candidates.Count
                && remaining > expectedAdded
                && ownedTreeSourceIds.Count < MaximumTreeSources; i++)
            {
                CAHomeTreeCandidate candidate = candidates[i];
                if (TooCloseToSelected(candidate.plant, selected,
                    ForestrySpacing(forester))) continue;
                reachChecks++;
                if (reachChecks > 96) break;
                if (!forester.CanReach(candidate.plant, PathEndMode.Touch,
                    Danger.Some)) continue;
                map.designationManager.AddDesignation(new Designation(
                    candidate.plant,
                    DesignationDefOf.HarvestPlant));
                if (firstAdded == null) firstAdded = candidate.plant;
                ownedTreeSourceIds.Add(candidate.plant.ThingID);
                selected.Add(candidate.plant);
                expectedAdded += candidate.expectedWood;
                added++;
            }

            int expected = incomingOwned + incomingPlayer + expectedAdded;
            if (added == 0)
            {
                outcome = "wood deficit " + deficit + "; "
                    + forester.LabelShort
                    + " found no eligible tree outside authored, cultivated, "
                    + "important, Home-perimeter, or defensive cover";
                return;
            }
            outcome = "wood deficit " + deficit + "; " + forester.LabelShort
                + " authored " + added + " native harvest designation"
                + (added == 1 ? "" : "s") + " for about " + expected
                + " wood including existing designated sources";
            Log.Message("[CA] home prerequisite: " + outcome);
            Messages.Message("Colonist Awareness: " + forester.LabelShort
                + " marked " + added + " tree" + (added == 1 ? "" : "s")
                + " for an exact " + deficit + " wood deficit.",
                new TargetInfo(firstAdded.Position, map),
                MessageTypeDefOf.SilentInput, historical: false);
        }

        private Pawn ChooseForester()
        {
            Pawn best = null;
            float bestScore = float.MinValue;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Downed || pawn.Drafted
                    || pawn.InMentalState || !pawn.Awake()
                    || !CABehaviorGate.StableProfileAllows(pawn,
                        "logistics.material_source")
                    || pawn.workSettings == null
                    || pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)
                    || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.PlantCutting))
                    continue;
                if (ModsConfig.IdeologyActive
                    && !IdeoUtility.DoerWillingToDo(HistoryEventDefOf.CutTree,
                        pawn)) continue;

                int plants = SkillLevel(pawn, SkillDefOf.Plants);
                int intellectual = SkillLevel(pawn, SkillDefOf.Intellectual);
                DispositionProfile disposition = Disposition.Of(pawn);
                float score = plants / 20f * 0.55f
                    + intellectual / 20f * 0.15f
                    + disposition.initiative * 0.20f
                    + disposition.discipline * 0.10f
                    + (pawn.thingIDNumber % 997) * 0.000001f;
                if (best == null || score > bestScore)
                {
                    best = pawn;
                    bestScore = score;
                }
            }
            return best;
        }

        private List<CAHomeTreeCandidate> RankedTreeCandidates(Pawn forester)
        {
            var result = new List<CAHomeTreeCandidate>();
            float judgment = ForestryJudgment(forester);
            List<Thing> plants = map.listerThings.ThingsInGroup(
                ThingRequestGroup.Plant);
            for (int i = 0; i < plants.Count; i++)
            {
                Plant plant = plants[i] as Plant;
                if (!EligibleTree(plant, forester)) continue;
                int yield = ExpectedWood(plant, forester);
                float distance = forester.Position.DistanceTo(plant.Position);
                int density = NearbyTreeCount(plant.Position, 4.9f);
                float score = yield * 3.8f
                    + plant.Growth * Mathf.Lerp(12f, 22f, judgment)
                    + Mathf.Min(density, 12) * Mathf.Lerp(0.35f, 1.1f, judgment)
                    - distance * Mathf.Lerp(0.52f, 0.34f, judgment)
                    + StableTie(plant.Position);
                result.Add(new CAHomeTreeCandidate
                {
                    plant = plant,
                    expectedWood = yield,
                    score = score
                });
            }
            result.Sort((left, right) => right.score.CompareTo(left.score));
            return result;
        }

        private bool EligibleTree(Plant plant, Pawn forester)
        {
            return SourceStillAuthorized(plant, forester)
                && !map.designationManager.AllDesignationsOn(plant).Any();
        }

        private bool TooCloseToSelected(Plant plant, List<Plant> selected,
            int spacing)
        {
            for (int i = 0; i < selected.Count; i++)
                if (selected[i] != null && selected[i].Spawned
                    && selected[i].Position.DistanceTo(plant.Position) < spacing)
                    return true;
            return false;
        }

        private int ExpectedDesignatedWood(Pawn forester, bool ownedOnly)
        {
            int total = 0;
            List<Thing> plants = map.listerThings.ThingsInGroup(
                ThingRequestGroup.Plant);
            for (int i = 0; i < plants.Count; i++)
            {
                Plant plant = plants[i] as Plant;
                if (plant == null || !plant.Spawned || !plant.HarvestableNow
                    || plant.def.plant?.harvestedThingDef != ThingDefOf.WoodLog
                    || plant.IsBurning() || plant.Position.Fogged(map)
                    || plant.IsForbidden(forester)
                    || !plant.Position.InAllowedArea(forester)
                    || !forester.CanReach(plant, PathEndMode.Touch, Danger.Some))
                    continue;
                bool owned = ownedTreeSourceIds.Contains(plant.ThingID);
                if (ownedOnly != owned) continue;
                if (map.designationManager.DesignationOn(plant,
                        DesignationDefOf.HarvestPlant) == null
                    && map.designationManager.DesignationOn(plant,
                        DesignationDefOf.CutPlant) == null) continue;
                total += ExpectedWood(plant, forester);
            }
            return total;
        }

        private int ExpectedWood(Plant plant, Pawn forester)
        {
            float value = plant.def.plant.harvestYield;
            float maturity = Mathf.InverseLerp(plant.def.plant.harvestMinGrowth,
                1f, plant.Growth);
            value *= 0.5f + maturity * 0.5f;
            value *= Mathf.Lerp(0.5f, 1f,
                plant.HitPoints / (float)Mathf.Max(1, plant.MaxHitPoints));
            if (plant.def.plant.harvestYieldAffectedByDifficulty)
                value *= Find.Storyteller.difficulty.cropYieldFactor;
            int result = Mathf.CeilToInt(value);
            float harvestYield = forester.GetStatValue(StatDefOf.PlantHarvestYield);
            if (harvestYield > 1f)
                result = Mathf.CeilToInt(result * harvestYield);
            return Mathf.Max(1, result);
        }

        private bool SourceStillAuthorized(Plant plant, Pawn forester)
        {
            if (plant == null || !plant.Spawned || plant.Map != map
                || plant.def.plant == null || !plant.def.plant.IsTree
                || plant.def.plant.isStump || !plant.HarvestableNow
                || !plant.CanYieldNow()
                || plant.def.plant.harvestedThingDef != ThingDefOf.WoodLog
                || plant.IsBurning() || plant.Position.Fogged(map)
                || plant.def.plant.warnIfMarkedForCut
                || plant.DeliberatelyCultivated()
                || map.zoneManager.ZoneAt(plant.Position) is Zone_Growing
                || playerVetoedTreeIds.Contains(plant.ThingID))
                return false;
            CompPlantPreventCutting prevent =
                plant.TryGetComp<CompPlantPreventCutting>();
            if (prevent != null && prevent.PreventCutting) return false;
            if (forester != null && (plant.IsForbidden(forester)
                || !plant.Position.InAllowedArea(forester))) return false;

            float judgment = forester != null ? ForestryJudgment(forester) : 0.5f;
            Faction faction = forester?.Faction ?? Faction.OfPlayer;
            if (PlannedUseMapComponent.For(map)?.ProgramAt(plant.Position) != null
                || NearHome(plant.Position, HomeSecurityBuffer)
                || NearDefenseProgram(plant.Position,
                    DefenseProgramBuffer + judgment * 2f)
                || NearDefensiveWork(plant.Position,
                    DefensiveWorkBuffer + judgment * 1.5f, faction))
                return false;
            return true;
        }

        private void RevalidateOwnedSources(Pawn forester)
        {
            if (ownedTreeSourceIds.Count == 0) return;
            List<Plant> sources = OwnedSourcePlants();
            var live = new HashSet<string>();
            for (int i = 0; i < sources.Count; i++)
            {
                Plant source = sources[i];
                Designation designation = map.designationManager.DesignationOn(
                    source, DesignationDefOf.HarvestPlant);
                if (designation == null) continue;
                if (!SourceStillAuthorized(source, forester))
                {
                    map.designationManager.RemoveDesignation(designation);
                    continue;
                }
                live.Add(source.ThingID);
            }
            int removed = ownedTreeSourceIds.RemoveAll(id => !live.Contains(id));
            if (removed > 0 && demand != null)
            {
                demand.status = removed + " CA forestry source"
                    + (removed == 1 ? " was" : "s were")
                    + " withdrawn after its protection or validity changed";
                lastOutcome = DemandSummary(demand);
            }
        }

        private float ForestryJudgment(Pawn pawn)
        {
            if (pawn == null) return 0.5f;
            DispositionProfile disposition = Disposition.Of(pawn);
            return Mathf.Clamp01(SkillLevel(pawn, SkillDefOf.Plants) / 20f * 0.55f
                + SkillLevel(pawn, SkillDefOf.Intellectual) / 20f * 0.15f
                + disposition.initiative * 0.20f
                + disposition.discipline * 0.10f);
        }

        private int ForestrySpacing(Pawn pawn)
        {
            return Mathf.RoundToInt(Mathf.Lerp(3f, 6f, ForestryJudgment(pawn)));
        }

        private bool NearHome(IntVec3 root, float radius)
        {
            int limit = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && map.areaManager.Home[cell]) return true;
            }
            return false;
        }

        private bool NearDefenseProgram(IntVec3 root, float radius)
        {
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            if (programs == null) return false;
            int limit = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                if (cell.InBounds(map)
                    && programs.PurposeAt(cell) == CASpacePurpose.Defense)
                    return true;
            }
            return false;
        }

        private bool NearDefensiveWork(IntVec3 root, float radius,
            Faction faction)
        {
            int limit = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) continue;
                List<Thing> things = cell.GetThingList(map);
                for (int j = 0; j < things.Count; j++)
                {
                    Building building = things[j] as Building;
                    if (building == null || building.Faction != faction
                        || building.def.building == null) continue;
                    if (building is Building_Trap
                        || building.def.building.IsTurret
                        || building.def.fillPercent >= 0.30f)
                        return true;
                }
            }
            return false;
        }

        private int NearbyTreeCount(IntVec3 root, float radius)
        {
            int count = 0;
            int limit = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                Plant plant = cell.InBounds(map) ? cell.GetPlant(map) : null;
                if (plant?.def.plant?.IsTree == true) count++;
            }
            return count;
        }

        private List<Plant> OwnedSourcePlants()
        {
            var result = new List<Plant>();
            List<Thing> plants = map.listerThings.ThingsInGroup(
                ThingRequestGroup.Plant);
            for (int i = 0; i < plants.Count; i++)
            {
                Plant plant = plants[i] as Plant;
                if (plant != null && ownedTreeSourceIds.Contains(plant.ThingID))
                    result.Add(plant);
            }
            return result;
        }

        private void CancelOwnedSources(string reason)
        {
            if (ownedTreeSourceIds.Count == 0) return;
            List<Plant> sources = OwnedSourcePlants();
            for (int i = 0; i < sources.Count; i++)
            {
                Designation designation = map.designationManager.DesignationOn(
                    sources[i], DesignationDefOf.HarvestPlant);
                if (designation != null)
                    map.designationManager.RemoveDesignation(designation);
            }
            ownedTreeSourceIds.Clear();
            if (demand != null) demand.status = reason;
        }

        private void PruneSourceProvenance()
        {
            if (ownedTreeSourceIds.Count == 0) return;
            var live = new HashSet<string>();
            List<Thing> plants = map.listerThings.ThingsInGroup(
                ThingRequestGroup.Plant);
            for (int i = 0; i < plants.Count; i++)
            {
                Plant plant = plants[i] as Plant;
                if (plant != null && plant.Spawned
                    && map.designationManager.DesignationOn(plant,
                        DesignationDefOf.HarvestPlant) != null)
                    live.Add(plant.ThingID);
            }
            ownedTreeSourceIds.RemoveAll(id => !live.Contains(id));
        }

        private void PruneVetoes()
        {
            if (playerVetoedTreeIds.Count == 0) return;
            var live = new HashSet<string>();
            List<Thing> plants = map.listerThings.ThingsInGroup(
                ThingRequestGroup.Plant);
            for (int i = 0; i < plants.Count; i++)
                if (plants[i] is Plant plant && plant.Spawned)
                    live.Add(plant.ThingID);
            playerVetoedTreeIds.RemoveAll(id => !live.Contains(id));
        }

        private int SkillLevel(Pawn pawn, SkillDef skill)
        {
            return pawn?.skills != null ? pawn.skills.GetSkill(skill).Level : 0;
        }

        private Pawn FindPawn(int thingId)
        {
            if (thingId < 0) return null;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i] != null && pawns[i].thingIDNumber == thingId)
                    return pawns[i];
            return null;
        }

        private float StableTie(IntVec3 cell)
        {
            int hash = cell.x * 73856093 ^ cell.z * 19349663;
            return (hash & 1023) * 0.00001f;
        }

        private string DemandSummary(CAHomeMaterialDemand value)
        {
            var parts = new List<string>();
            for (int i = 0; i < value.requirements.Count; i++)
            {
                CAHomeMaterialRequirement requirement = value.requirements[i];
                parts.Add(requirement.defName + " " + requirement.available
                    + "/" + requirement.required + " (deficit "
                    + requirement.deficit + ")");
            }
            return value.provisionDefName + " at " + value.cell + " for "
                + value.kind + " program " + value.programId
                + (value.targetResidentId > 0 ? " targetResidentId "
                    + value.targetResidentId : "")
                + " by pawn " + value.authorPawnId + " faction "
                + value.authorFactionId
                + "; behavior " + (value.behaviorKey ?? "unregistered")
                + " episode " + value.episodeId + " authority "
                + (value.authorityIdentity ?? "unknown")
                + " producer " + value.producerPawnId + ": "
                + (parts.Count == 0 ? "no materials" : string.Join(", ",
                    parts.ToArray()))
                + (value.placementEvidence.NullOrEmpty() ? ""
                    : "; placement [" + value.placementEvidence + "]")
                + "; " + value.status;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Designator_Plants),
        nameof(Designator_Plants.DesignateThing))]
    internal static class Patch_CAHomePlayerPlantDesignation
    {
        private static void Prefix(Thing t)
        {
            CAHomePrerequisiteMapComponent.For(t?.Map)
                ?.NotifyPlayerPlantDesignation(t);
        }
    }
}
