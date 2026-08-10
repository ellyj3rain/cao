using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Verification instruments: one-click dev-mode readouts of the pillar state, so a
    // manufactured scenario (dev-spawned raider, ordered group action) can be PROVEN
    // against what pawns actually know, are, and obey - not eyeballed. Dev mode only;
    // ships inert.
    public static partial class CADebugActions
    {
        internal const string StoragePersistenceSaveName =
            "CA_Agent_Storage_Persistence_a2_64";
        internal const string SettlementContextPersistenceSaveName =
            "CA_Agent_Settlement_Context_a2_78";
        internal const string ToxicWasteRelocationWorkingSaveName =
            "CA-a2-88-native-waste-relocation-working-mountain";
        internal const string ToxicWasteRelocationActiveSaveName =
            "CA-a2-88-native-haul-active-reservations-clean-checkpoint-mountain";
        internal const string ToxicWasteRelocationCompletedSaveName =
            "CA-a2-88-native-haul-completed-placement-mountain";
        internal const string ToxicWasteRelocationResumedSaveName =
            "CA-a2-88-native-haul-resumed-after-reload-mountain";
        internal const string SpatialControlledSaveName =
            "ca-a2-86-controlled-kitchen-requirement-";
        internal const string SpatialDevelopedMountainSaveName =
            "CA-a2-88-native-waste-relocation-working-mountain";
        internal const string SpatialAbovegroundSaveName =
            "CA-corpus-aboveground";
        internal const string SpatialAuthoredBarracksSaveName = "Autosave-5";
        internal const string SpatialRoomPreplacementSaveName =
            "CA-a2-93-native-bed-facility-preplacement-barracks";
        internal const string SpatialRoomWorkingSaveName =
            "CA-a2-93-native-bed-facility-working-barracks";
        internal const string SpatialRoomDollyPreplacementSaveName =
            "CA-a2-93-dolly-restored-eight-bed-barracks-preplacement";
        internal const string SpatialRoomDollyWorkingSaveName =
            "CA-a2-93-dolly-restored-functional-barracks-working";
        internal const string BedroomRequirementControlledSaveName =
            "CA-a2-96-authored-bedroom-requirement-functional-controlled";
        internal const string BedroomConstructionCauseUnmetSaveName =
            "CA-a2-106-authored-bedroom-one-missing-bed-controlled";
        internal const string BedroomConstructionCauseActiveSaveName =
            "CA-a2-109-authored-bedroom-native-supply-targeted-bed-active";
        internal const string BedroomConstructionCauseCompletedSaveName =
            "CA-a2-109-authored-bedroom-native-supply-targeted-bed-completed";

        private static int expectedSettlementContextRevision = -1;
        private static int expectedSettlementContextFirstObservedTick = -1;
        private static string expectedSettlementContextSignature;

        private static readonly IntVec3[] FireFixtureDirections =
        {
            new IntVec3(1, 0, 0),
            new IntVec3(0, 0, 1),
            new IntVec3(-1, 0, 0),
            new IntVec3(0, 0, -1),
            new IntVec3(1, 0, 1),
            new IntVec3(-1, 0, 1),
            new IntVec3(-1, 0, -1),
            new IntVec3(1, 0, -1)
        };

        // Session-local references identify only things these actions created. After
        // a reload they are forgotten. A hash-bound isolation action without such a
        // reference must refuse ambiguous populations and receipt its exact target;
        // no action broadly deletes map contents.
        private static Map fireFixtureMap;
        private static IntVec3 fireFixtureCell = IntVec3.Invalid;
        private static Building fireFixtureWall;
        private static Map homePlanningFixtureMap;
        private static Thing homePlanningFixtureMaterial;
        private static Fire fireFixtureFire;
        private static int fireFixturePawnId = -1;
        private static bool fireFixtureOriginalHome;
        private static bool fireFixtureAppliedHome;
        private static Map animalFeedFixtureMap;
        private static Pawn animalFeedFixtureHandler;
        private static Pawn animalFeedFixtureAnimal;
        private static Thing animalFeedFixtureFood;

        [DebugAction("Colonist Awareness", "Log disposition (selected)",
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogDisposition()
        {
            foreach (var obj in Find.Selector.SelectedObjects)
            {
                var p = obj as Pawn;
                if (p == null || !p.IsColonistPlayerControlled) continue;
                var d = Disposition.Of(p);
                Log.Message(string.Format(
                    "[CA] {0}: courage {1:F2}  discipline {2:F2}  aggression {3:F2}  empathy {4:F2}  conformity {5:F2}  initiative {6:F2}  skepticism {7:F2}",
                    p.LabelShort, d.courage, d.discipline, d.aggression, d.empathy, d.conformity, d.initiative, d.skepticism));
            }
        }

        [DebugAction("Colonist Awareness", "Immediate combat census (selected)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ImmediateCombatCensus()
        {
            bool any = false;
            foreach (object obj in Find.Selector.SelectedObjects)
            {
                Pawn pawn = obj as Pawn;
                if (pawn == null || pawn.RaceProps == null
                    || !pawn.RaceProps.Humanlike || pawn.Map == null) continue;
                any = true;

                IntVec3 copiedThreat;
                Pawn target = VisibleCombatTarget(pawn, out copiedThreat);
                target = CAImmediateCombat.ImmediateVisibleTarget(pawn, target);

                CASelfPreservationAssessment survival;
                AwarenessSettings settings = AwarenessMod.Settings;
                bool mayFight = pawn.IsColonistPlayerControlled
                    ? JobGiver_CACombatReaction.PlayerMaySelfReact(pawn, settings)
                    : !pawn.WorkTagIsDisabled(WorkTags.Violent)
                        && pawn.TryGetAttackVerb(null,
                            CAImmediateCombat.AllowManualCombatVerb(pawn)) != null;
                bool hasSurvival = CAImmediateCombat.AssessSelfPreservation(
                    pawn, target, copiedThreat, mayFight, out survival);

                CACombatCellAssessment current = new CACombatCellAssessment
                    { cell = IntVec3.Invalid };
                CACombatCellAssessment best = new CACombatCellAssessment
                    { cell = IntVec3.Invalid };
                bool improves = false;
                if (target != null)
                    improves = CAImmediateCombat.TryFindBetterFirePosition(
                        pawn, target, false, out current, out best);

                string survivalText = hasSurvival
                    ? "harm age " + survival.harmAge + " ticks, pressure "
                        + survival.pressure.ToString("F2") + ", composure "
                        + survival.composure.ToString("F2") + ", cover "
                        + survival.currentCover.ToString("F2") + ", can engage "
                        + (survival.canEngage ? "yes" : "no") + ", decision "
                        + survival.decision + "; "
                        + survival.condition.TraceText()
                    : "no recent harm episode; "
                        + CACombatConditionSnapshot.Capture(pawn).TraceText();
                string positionText = current.cell.IsValid
                    ? "current " + CellAssessment(current) + "; best "
                        + CellAssessment(best) + "; move "
                        + (improves ? "yes" : "no")
                    : target != null ? "no ranged firing solution"
                    : "no currently visible hostile";

                Log.Message("[CA] " + pawn.LabelShort
                    + " immediate combat: target "
                    + (target != null ? target.LabelShort : "none") + "; "
                    + survivalText + "; " + positionText);
            }
            if (!any)
                Log.Message("[CA] immediate combat census: select a humanlike pawn");
        }

        private static string CellAssessment(CACombatCellAssessment assessment)
        {
            if (!assessment.cell.IsValid) return "none";
            return assessment.cell + " score " + assessment.score.ToString("F2")
                + " shot " + assessment.shotQuality.ToString("F2")
                + " self-cover " + assessment.selfCover.ToString("F2")
                + " lane-risk " + assessment.friendlyLaneRisk.ToString("F2")
                + " friendly-sector "
                + assessment.friendlySectorRisk.ToString("F2")
                + " route-sector-cells "
                + assessment.friendlySectorRouteCells
                + " route-sector-max "
                + assessment.friendlySectorRouteRisk.ToString("F2")
                + " range " + assessment.rangeFit.ToString("F2")
                + " travel " + assessment.travel.ToString("F1")
                + " support " + assessment.mutualSupport.ToString("F2")
                + " pressure " + assessment.localPressure.ToString("F2")
                + " hazard " + assessment.hazard.ToString("F2");
        }

        [DebugAction("Colonist Awareness", "Mission triage census (selected)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MissionTriageCensus()
        {
            bool any = false;
            foreach (object obj in Find.Selector.SelectedObjects)
            {
                Pawn pawn = obj as Pawn;
                if (pawn == null || pawn.Map == null) continue;
                MissionCasualtyKnowledgeMapComponent mission =
                    MissionCasualtyKnowledgeMapComponent.For(pawn.Map);
                var facts = new List<MissionCasualtyFact>();
                if (mission == null
                    || !mission.TryGetFacts(pawn, facts)) continue;
                any = true;

                CATriageAssessment assessment;
                CAMissionTriage.TryAssess(pawn, out assessment);
                var sb = new StringBuilder("[CA] mission triage: ")
                    .Append(pawn.LabelShort)
                    .Append(" self actual bleed death ")
                    .Append(TriageTickLabel(
                        HealthUtility.TicksUntilDeathDueToBloodLoss(pawn)))
                    .Append(", self perceived ")
                    .Append(TriageTickLabel(assessment.SelfBleedDeathTicks))
                    .Append(", current job ")
                    .Append(pawn.CurJobDef != null
                        ? pawn.CurJobDef.defName : "none")
                    .Append(", clinical assessment ").Append(assessment.Decision)
                    .AppendLine();
                int now = Find.TickManager.TicksGame;
                for (int i = 0; i < facts.Count; i++)
                {
                    MissionCasualtyFact fact = facts[i];
                    Pawn casualty = fact.beneficiary;
                    int perceived = fact.estimatedDeathTick == int.MaxValue
                        ? int.MaxValue
                        : Mathf.Max(0, fact.estimatedDeathTick - now);
                    sb.Append("  ").Append(casualty.LabelShort)
                        .Append(": state ").Append(fact.state)
                        .Append(", last-known ").Append(fact.lastKnownCell)
                        .Append(", diagnostic tier ").Append(fact.diagnosticTier)
                        .Append(", assessed age ")
                        .Append(fact.assessedTick >= 0
                            ? (now - fact.assessedTick) + " ticks" : "never")
                        .Append(", perceived bleed death ")
                        .Append(TriageTickLabel(perceived))
                        .Append(", developer actual ")
                        .Append(TriageTickLabel(
                            HealthUtility.TicksUntilDeathDueToBloodLoss(casualty)))
                        .Append(", developer health ")
                        .Append(casualty.health.summaryHealth
                            .SummaryHealthPercent.ToString("F2"))
                        .Append(", observed downed ")
                        .Append(fact.observedDowned ? "yes" : "no")
                        .Append(", observed needs tend ")
                        .Append(fact.observedNeedsTend ? "yes" : "no")
                        .AppendLine();
                }
                Log.Message(sb.ToString());
            }
            if (!any)
                Log.Message("[CA] mission triage census: selected pawn has no incident casualty mission");
        }

        private static string TriageTickLabel(int ticks)
        {
            return ticks == int.MaxValue ? "not projected" : ticks + " ticks";
        }

        [DebugAction("Colonist Awareness",
            "Welfare and accountability census (selected)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WelfareAndAccountabilityCensus()
        {
            bool any = false;
            foreach (object obj in Find.Selector.SelectedObjects)
            {
                Pawn pawn = obj as Pawn;
                if (pawn == null || pawn.Map == null) continue;
                any = true;
                KnowledgeMapComponent knowledge =
                    KnowledgeMapComponent.For(pawn.Map);
                var welfare = new List<WelfareFactSnapshot>();
                var responsibility = new List<AccountabilitySnapshot>();
                knowledge?.CopyFreshWelfare(pawn, welfare);
                knowledge?.CopyAccountability(pawn, responsibility);
                int now = Find.TickManager.TicksGame;
                var sb = new StringBuilder("[CA] welfare/accountability: ")
                    .Append(pawn.LabelShort).Append(" knows ")
                    .Append(welfare.Count).Append(" welfare fact(s), owns ")
                    .Append(responsibility.Count)
                    .Append(" active/recent responsibility record(s)")
                    .AppendLine();
                for (int i = 0; i < welfare.Count; i++)
                {
                    WelfareFactSnapshot fact = welfare[i];
                    Pawn subject = PawnById(pawn.Map, fact.SubjectId);
                    sb.Append("  welfare ")
                        .Append(subject != null ? subject.LabelShort
                            : "pawn " + fact.SubjectId)
                        .Append(": state ").Append(fact.State)
                        .Append(", last-known ").Append(fact.Cell)
                        .Append(", source age ")
                        .Append(now - fact.SourceTick).Append(" ticks")
                        .Append(", revision ").Append(fact.Revision)
                        .Append(", source ").Append(fact.Evidence.Source)
                        .Append(", delivery ")
                        .Append(fact.Evidence.DeliveryChannel)
                        .Append(", reporter ").Append(fact.Evidence.ReporterId)
                        .Append(", downed ")
                        .Append(fact.ObservedDowned ? "yes" : "no")
                        .Append(", temperature danger ")
                        .Append(fact.ObservedTemperatureDanger ? "yes" : "no")
                        .Append(", needs tend ")
                        .Append(fact.ObservedNeedsTend ? "yes" : "no")
                        .Append(", perceived bleed death ")
                        .Append(fact.EstimatedDeathTick == int.MaxValue
                            ? "not projected"
                            : Mathf.Max(0, fact.EstimatedDeathTick - now)
                                + " ticks")
                        .AppendLine();
                }
                for (int i = 0; i < responsibility.Count; i++)
                {
                    AccountabilitySnapshot record = responsibility[i];
                    Pawn subject = PawnById(pawn.Map, record.SubjectId);
                    sb.Append("  responsibility ")
                        .Append(subject != null ? subject.LabelShort
                            : "pawn " + record.SubjectId)
                        .Append(": ").Append(record.State)
                        .Append(", last confirmed ")
                        .Append(record.LastConfirmedCell.IsValid
                            ? record.LastConfirmedCell.ToString() : "unknown")
                        .Append(", age ")
                        .Append(record.LastConfirmedTick >= 0
                            ? (now - record.LastConfirmedTick) + " ticks"
                            : "never")
                        .Append(", grace ").Append(record.GraceTicks)
                        .Append(" ticks, revision ").Append(record.Revision)
                        .AppendLine();
                }
                Log.Message(sb.ToString());
            }
            if (!any)
                Log.Message("[CA] welfare/accountability census: select a pawn");
        }

        private static Pawn VisibleCombatTarget(Pawn pawn, out IntVec3 copiedThreat)
        {
            copiedThreat = IntVec3.Invalid;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings != null && settings.knowledgeContacts)
            {
                KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
                ThreatContactSnapshot contact;
                if (knowledge == null
                    || !knowledge.TryGetFreshestContact(pawn, out contact)) return null;
                copiedThreat = contact.Cell;
                Pawn known = PawnById(pawn.Map, contact.HostileId);
                return known != null && KnowledgeMapComponent.CanCurrentlySeeHostile(
                    pawn, known, 42f) ? known : null;
            }

            Pawn nearest = null;
            float distance = float.MaxValue;
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (!KnowledgeMapComponent.CanCurrentlySeeHostile(
                    pawn, candidate, 42f)) continue;
                float candidateDistance = pawn.Position.DistanceToSquared(
                    candidate.Position);
                if (candidateDistance >= distance) continue;
                distance = candidateDistance;
                nearest = candidate;
            }
            if (nearest != null) copiedThreat = nearest.Position;
            return nearest;
        }

        private static Pawn PawnById(Map map, int id)
        {
            if (map == null) return null;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == id) return pawns[i];
            return null;
        }

        [DebugAction("Colonist Awareness", "Log animal disposition (selected)",
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogAnimalDisposition()
        {
            foreach (var obj in Find.Selector.SelectedObjects)
            {
                Pawn pawn = obj as Pawn;
                if (pawn == null || pawn.RaceProps == null
                    || !pawn.RaceProps.Animal) continue;
                AnimalDispositionProfile d = AnimalDisposition.Of(pawn);
                TrainabilityDef trainability = TrainableUtility.GetTrainability(pawn);
                Pawn anchor = AnimalDisposition.AnchorFor(pawn);
                Log.Message(string.Format(
                    "[CA] {0}: vigilance {1:F2}  nerve {2:F2}  attachment {3:F2}  defensive drive {4:F2}; wildness {5:F2}, trainability {6}, anchor {7}, active threat {8}",
                    pawn.LabelShort, d.vigilance, d.nerve, d.attachment,
                    d.defensiveDrive, pawn.GetStatValue(StatDefOf.Wildness),
                    trainability != null ? trainability.defName : "none",
                    anchor != null ? anchor.LabelShort : "none",
                    AnimalThreatRelevance.IsActiveThreat(pawn) ? "yes" : "no"));
            }
        }

        [DebugAction("Colonist Awareness",
            "Animal feed fixture: downed/no bed (selected)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AnimalFeedFixtureDownedNoBed()
        {
            Pawn handler = Find.Selector.SingleSelectedThing as Pawn;
            Map map = Find.CurrentMap;
            if (handler == null || map == null || handler.Map != map
                || !handler.IsColonistPlayerControlled || handler.Downed
                || handler.Drafted || handler.InMentalState
                || handler.WorkTagIsDisabled(WorkTags.Animals)
                || PawnUtility.PlayerForcedJobNowOrSoon(handler))
            {
                AnimalFeedFixtureFailure(
                    "select one available, undrafted player colonist capable of Animals work");
                return;
            }

            Find.TickManager.Pause();
            CleanTrackedAnimalFeedFixture();

            IntVec3 animalCell;
            IntVec3 foodCell;
            if (!TryFindAnimalFeedFixtureCells(
                handler, out animalCell, out foodCell))
            {
                AnimalFeedFixtureFailure(
                    "no empty safely reachable animal-and-food cells were available near "
                    + handler.LabelShort);
                return;
            }

            Pawn animal = null;
            Thing food = null;
            try
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("Muffalo");
                animal = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer);
                GenSpawn.Spawn(animal, animalCell, map, WipeMode.Vanish);
                if (animal.needs?.food == null)
                    throw new System.InvalidOperationException(
                        "generated animal has no food need");
                animal.needs.food.CurLevelPercentage = 0.01f;
                if (!HealthUtility.TryAnesthetize(animal) || !animal.Downed)
                    throw new System.InvalidOperationException(
                        "native anesthetic did not down the animal");

                food = ThingMaker.MakeThing(ThingDefOf.Kibble);
                float nutrition = food.GetStatValue(StatDefOf.Nutrition);
                food.stackCount = Mathf.Clamp(
                    FoodUtility.WillIngestStackCountOf(
                        animal, food.def, nutrition),
                    1,
                    food.def.stackLimit);
                GenSpawn.Spawn(food, foodCell, map, WipeMode.Vanish);
                food.SetForbidden(false, false);

                var giver = new WorkGiver_FeedHungryAnimals();
                Job job = giver.JobOnThing(handler, animal, false);
                if (job == null || job.def != CA_Defs.FeedDownedAnimal)
                    throw new System.InvalidOperationException(
                        "the animal-care giver did not produce CA_FeedDownedAnimal");

                animalFeedFixtureMap = map;
                animalFeedFixtureHandler = handler;
                animalFeedFixtureAnimal = animal;
                animalFeedFixtureFood = food;
                handler.jobs.StartJob(job, JobCondition.InterruptForced);
                if (handler.CurJob != job)
                    throw new System.InvalidOperationException(
                        "the selected handler did not retain the fixture job");

                CameraJumper.TryJump(animal, CameraJumper.MovementMode.Cut);
                Find.Selector.ClearSelection();
                Find.Selector.Select(animal, false, true);
                Log.Message("[CA] animal feed fixture: handler "
                    + handler.LabelShort + " [" + handler.ThingID + "]"
                    + ", animal " + animal.LabelShort + " ["
                    + animal.ThingID + "] at " + animalCell
                    + ", downed " + YesNo(animal.Downed)
                    + ", bed " + ThingFact(animal.CurrentBed())
                    + ", food level "
                    + animal.needs.food.CurLevelPercentage.ToString("F3")
                    + ", native ShouldBeFedBySomeone "
                    + YesNo(FoodUtility.ShouldBeFedBySomeone(animal))
                    + ", food " + food.LabelShort + " [" + food.ThingID
                    + "] at " + foodCell + ", job "
                    + handler.CurJobDef.defName + " ["
                    + handler.CurJob.GetUniqueLoadID() + "]");
            }
            catch (System.Exception ex)
            {
                if (animalFeedFixtureMap == null)
                {
                    if (food != null && food.Spawned)
                        food.Destroy(DestroyMode.Vanish);
                    if (animal != null && animal.Spawned)
                        animal.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    CleanTrackedAnimalFeedFixture();
                }
                Log.Error("[CA] animal feed fixture exception: " + ex);
                AnimalFeedFixtureFailure(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool TryFindAnimalFeedFixtureCells(
            Pawn handler, out IntVec3 animalCell, out IntVec3 foodCell)
        {
            Map map = handler.Map;
            for (int radius = 4; radius <= 10; radius++)
            {
                for (int i = 0; i < FireFixtureDirections.Length; i++)
                {
                    IntVec3 direction = FireFixtureDirections[i];
                    IntVec3 candidate = handler.Position + new IntVec3(
                        direction.x * radius, 0, direction.z * radius);
                    if (!AnimalFeedFixtureCellIsValid(handler, candidate))
                        continue;
                    for (int j = 0; j < GenAdj.AdjacentCells.Length; j++)
                    {
                        IntVec3 adjacent = candidate + GenAdj.AdjacentCells[j];
                        if (!adjacent.InBounds(map) || adjacent.Fogged(map)
                            || !adjacent.Standable(map)
                            || adjacent.GetThingList(map).Count != 0
                            || !handler.CanReach(
                                adjacent, PathEndMode.OnCell, Danger.Some))
                            continue;
                        animalCell = candidate;
                        foodCell = adjacent;
                        return true;
                    }
                }
            }
            animalCell = IntVec3.Invalid;
            foodCell = IntVec3.Invalid;
            return false;
        }

        private static bool AnimalFeedFixtureCellIsValid(
            Pawn handler, IntVec3 cell)
        {
            Map map = handler.Map;
            return map != null && cell.InBounds(map) && !cell.Fogged(map)
                && cell.Standable(map) && cell.GetThingList(map).Count == 0
                && handler.CanReach(cell, PathEndMode.Touch, Danger.Some);
        }

        private static void CleanTrackedAnimalFeedFixture()
        {
            Map map = animalFeedFixtureMap;
            Pawn handler = animalFeedFixtureHandler;
            Pawn animal = animalFeedFixtureAnimal;
            Thing food = animalFeedFixtureFood;
            try
            {
                if (map != null && Current.Game != null && !map.Disposed
                    && Find.Maps.Contains(map))
                {
                    if (handler != null && handler.Map == map
                        && handler.CurJobDef == CA_Defs.FeedDownedAnimal
                        && handler.CurJob?.targetB.Pawn == animal)
                        handler.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    if (handler?.carryTracker?.CarriedThing == food)
                        handler.carryTracker.DestroyCarriedThing();
                    if (food != null && food.Spawned && food.Map == map)
                        food.Destroy(DestroyMode.Vanish);
                    if (animal != null && animal.Spawned && animal.Map == map)
                        animal.Destroy(DestroyMode.Vanish);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning("[CA] animal feed fixture cleanup failed ("
                    + ex.GetType().Name + ")");
            }
            finally
            {
                animalFeedFixtureMap = null;
                animalFeedFixtureHandler = null;
                animalFeedFixtureAnimal = null;
                animalFeedFixtureFood = null;
            }
        }

        private static void AnimalFeedFixtureFailure(string reason)
        {
            string text = "[CA] animal feed fixture: failed - " + reason;
            Log.Warning(text);
            Messages.Message(text, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Colonist Awareness", "Equipment readiness census",
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EquipmentReadinessCensus()
        {
            bool any = false;
            foreach (var obj in Find.Selector.SelectedObjects)
            {
                Pawn pawn = obj as Pawn;
                if (pawn == null || !pawn.IsColonistPlayerControlled) continue;
                any = true;
                string weaponReason;
                ThingWithComps weapon = JobGiver_CAOperationalEquipment
                    .BestPermittedWeapon(pawn, out weaponReason);
                string apparelReason;
                Apparel apparel = JobGiver_CAOperationalEquipment
                    .BestPermittedApparel(pawn, out apparelReason);
                Thing medicine = BestPermittedMedicine(pawn);
                Log.Message("[CA] " + pawn.LabelShort + " equipment readiness: autonomy "
                    + AutonomyComponent.LevelNames[AutonomyComponent.LevelOf(pawn)]
                    + ", operational access "
                    + (AwarenessMod.Settings != null
                        && AwarenessMod.Settings.operationalAccess ? "on" : "off")
                    + ", identified threat "
                    + (JobGiver_CAOperationalEquipment.ThreatKnown(pawn) ? "yes" : "no")
                    + ", current weapon "
                    + (pawn.equipment != null && pawn.equipment.Primary != null
                        ? pawn.equipment.Primary.LabelShort : "none")
                    + ", permitted ground weapon "
                    + (weapon != null ? weapon.LabelShort : "none (" + weaponReason + ")")
                    + ", permitted protection "
                    + (apparel != null ? apparel.LabelShort : "none (" + apparelReason + ")")
                    + ", permitted medicine "
                    + (medicine != null ? medicine.LabelShort : "none"));
            }
            if (!any)
                Log.Message("[CA] equipment readiness census: select a player colonist");
        }

        [DebugAction("Colonist Awareness", "Operational access census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OperationalAccessCensus()
        {
            OperationalAccessComponent component = OperationalAccessComponent.Instance;
            Log.Message(component != null
                ? component.Census(Find.CurrentMap)
                : "[CA] operational access: no game component");
        }

        internal static string OperationalAccessReceipt()
        {
            OperationalAccessComponent component = OperationalAccessComponent.Instance;
            string receipt = component != null
                ? "[CA] agent receipt: operational-access-census\n"
                    + component.CategorizedCensus(Find.CurrentMap)
                : "[CA] agent receipt: operational access has no game component";
            Log.Message(receipt);
            return receipt;
        }

        [DebugAction("Colonist Awareness", "Fire response census (selected)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FireResponseCensus()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Message("[CA] fire response census: no current map");
                return;
            }

            AwarenessSettings settings = AwarenessMod.Settings;
            FireResponseMapComponent component =
                map.GetComponent<FireResponseMapComponent>();
            WorkGiver_Scanner fireWorker =
                WorkGiverDefOf.FightFires.Worker as WorkGiver_Scanner;
            List<Thing> fires = map.listerThings
                .ThingsInGroup(ThingRequestGroup.Fire);
            bool any = false;

            foreach (object obj in Find.Selector.SelectedObjects)
            {
                Pawn pawn = obj as Pawn;
                if (pawn == null || pawn.Map != map
                    || !pawn.IsColonistPlayerControlled) continue;
                any = true;

                int autonomy = AutonomyComponent.LevelOf(pawn);
                Job current = pawn.CurJob;
                bool currentForced = current != null && current.playerForced;
                bool queuedForced = pawn.jobs != null
                    && pawn.jobs.jobQueue != null
                    && pawn.jobs.jobQueue.AnyPlayerForced;
                bool forcedNowOrSoon = PawnUtility.PlayerForcedJobNowOrSoon(pawn);
                int firefighterPriority = pawn.workSettings != null
                    ? pawn.workSettings.GetPriority(WorkTypeDefOf.Firefighter)
                    : -1;
                bool emergencyContainsFireWorker = fireWorker != null
                    && pawn.workSettings != null
                    && pawn.workSettings.WorkGiversInOrderEmergency
                        .Contains(fireWorker);
                string dutyHook = pawn.mindState?.duty?.def != null
                    ? pawn.mindState.duty.def.hook.ToString()
                    : "none";
                string duty = pawn.mindState?.duty?.def != null
                    ? pawn.mindState.duty.def.defName
                    : "none";
                string jobLord = current != null && current.lord != null
                    ? current.lord.LordJob != null
                        ? current.lord.LordJob.GetType().Name
                        : "present (no LordJob)"
                    : "none";
                Lord pawnLord = pawn.GetLord();

                var sb = new StringBuilder("[CA] fire response census: ")
                    .Append(pawn.LabelShort).AppendLine()
                    .Append("  settings present ").Append(YesNo(settings != null))
                    .Append(", fire response ")
                    .Append(YesNo(settings != null && settings.fireResponse))
                    .Append(", map component ").Append(YesNo(component != null))
                    .AppendLine()
                    .Append("  autonomy ").Append(autonomy).Append(" (")
                    .Append(autonomy >= 0
                        && autonomy < AutonomyComponent.LevelNames.Length
                            ? AutonomyComponent.LevelNames[autonomy]
                            : "unknown")
                    .Append("), downed ").Append(YesNo(pawn.Downed))
                    .Append(", drafted ").Append(YesNo(pawn.Drafted))
                    .Append(", mental state ").Append(YesNo(pawn.InMentalState))
                    .Append(", awake ").Append(YesNo(pawn.Awake()))
                    .AppendLine()
                    .Append("  Firefighting tag disabled ")
                    .Append(YesNo(pawn.WorkTagIsDisabled(WorkTags.Firefighting)))
                    .Append(", Firefighter priority ")
                    .Append(firefighterPriority >= 0
                        ? firefighterPriority.ToString() : "unavailable")
                    .Append(", emergency list contains FightFires worker ")
                    .Append(YesNo(emergencyContainsFireWorker))
                    .AppendLine()
                    .Append("  player forced: current ").Append(YesNo(currentForced))
                    .Append(", queued ").Append(YesNo(queuedForced))
                    .Append(", now-or-soon helper ").Append(YesNo(forcedNowOrSoon))
                    .AppendLine()
                    .Append("  current job ")
                    .Append(current != null && current.def != null
                        ? current.def.defName : "none")
                    .Append(", joy kind ")
                    .Append(current?.def?.joyKind != null
                        ? current.def.joyKind.defName : "none")
                    .Append(", job playerForced ").Append(YesNo(currentForced))
                    .Append(", mindState.IsIdle ")
                    .Append(YesNo(pawn.mindState != null
                        && pawn.mindState.IsIdle))
                    .AppendLine()
                    .Append("  current-job lord ").Append(jobLord)
                    .Append(", pawn lord ")
                    .Append(pawnLord?.LordJob != null
                        ? pawnLord.LordJob.GetType().Name : "none")
                    .Append(", duty ").Append(duty)
                    .Append(", duty hook ").Append(dutyHook)
                    .AppendLine();

                int activeFireCount = 0;
                for (int i = 0; i < fires.Count; i++)
                {
                    Fire fire = fires[i] as Fire;
                    if (fire == null || !fire.Spawned) continue;
                    activeFireCount++;

                    bool home = map.areaManager.Home[fire.Position];
                    Building building = fire.Position.GetFirstBuilding(map);
                    bool forbidden = fire.IsForbidden(pawn);
                    bool nativeHasJob = fireWorker != null
                        && fireWorker.HasJobOnThing(pawn, fire, false);
                    bool nativeReachable = fireWorker != null
                        && pawn.CanReach(fire, fireWorker.PathEndMode,
                            fireWorker.MaxPathDanger(pawn));
                    bool offHomeReachable = pawn.CanReach(fire,
                        PathEndMode.Touch, Danger.Some);
                    Pawn respectedReserver = map.reservationManager
                        .FirstRespectedReserver(fire, pawn);
                    bool beingHandled = respectedReserver != null
                        && respectedReserver.Position.InHorDistOf(
                            fire.Position, 5f);
                    bool distant = (pawn.Position - fire.Position)
                        .LengthHorizontalSquared > 225;
                    bool reservationGate = !distant || pawn.CanReserve(fire);
                    bool offHomeCandidate = !home && fire.parent == null
                        && building != null
                        && building.Faction == Faction.OfPlayer;

                    sb.Append("  fire ").Append(activeFireCount)
                        .Append(" at ").Append(fire.Position)
                        .Append(": home ").Append(YesNo(home))
                        .Append(", parent ").Append(ThingFact(fire.parent))
                        .Append(", building ").Append(ThingFact(building))
                        .Append(", fire faction ")
                        .Append(FactionFact(fire.Faction))
                        .Append(", building faction ")
                        .Append(FactionFact(building?.Faction))
                        .Append(", forbidden ").Append(YesNo(forbidden))
                        .AppendLine()
                        .Append("    native HasJobOnThing ")
                        .Append(YesNo(nativeHasJob))
                        .Append(", native reachability ")
                        .Append(YesNo(nativeReachable))
                        .Append(", off-home reachability ")
                        .Append(YesNo(offHomeReachable))
                        .Append(", off-home candidate ")
                        .Append(YesNo(offHomeCandidate))
                        .Append(", being handled ").Append(YesNo(beingHandled))
                        .Append(", distant reservation gate ")
                        .Append(YesNo(reservationGate))
                        .AppendLine();
                }
                if (activeFireCount == 0)
                    sb.AppendLine("  active fires: none");

                Log.Message(sb.ToString());
            }

            if (!any)
                Log.Message("[CA] fire response census: select a player colonist on the current map");
        }

        [DebugAction("Colonist Awareness", "Fire response fixture: Home (selected)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FireResponseFixtureHome()
        {
            MakeFireResponseFixture(true);
        }

        [DebugAction("Colonist Awareness", "Fire response fixture: off-Home (selected)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FireResponseFixtureOffHome()
        {
            MakeFireResponseFixture(false);
        }

        private static void MakeFireResponseFixture(bool home)
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            Map map = Find.CurrentMap;
            if (pawn == null || map == null || pawn.Map != map
                || !pawn.IsColonistPlayerControlled)
            {
                FireFixtureFailure(
                    "select exactly one spawned player colonist on the current map");
                return;
            }

            Find.TickManager.Pause();

            IntVec3 preferred = IntVec3.Invalid;
            if (fireFixtureMap == map)
            {
                if (fireFixturePawnId == pawn.thingIDNumber)
                    preferred = fireFixtureCell;
            }
            if (fireFixtureMap != null)
                CleanTrackedFireFixture();

            IntVec3 cell;
            if (!TryFindFireFixtureCell(pawn, preferred, out cell))
            {
                FireFixtureFailure(
                    "no empty, isolated, safely reachable wall cell was available near "
                    + pawn.LabelShort);
                return;
            }

            bool originalHome = map.areaManager.Home[cell];
            Building wall = ThingMaker.MakeThing(
                ThingDefOf.Wall, ThingDefOf.WoodLog) as Building;
            if (wall == null)
            {
                FireFixtureFailure("the native wooden-wall Thing could not be made");
                return;
            }

            wall.SetFaction(Faction.OfPlayer);
            Thing spawnedWall = null;
            PlaySettings playSettings = Find.PlaySettings;
            bool originalAutoHomeArea = playSettings.autoHomeArea;
            try
            {
                // Building.SpawnSetup calls AutoHomeAreaMaker. Suppress that native
                // 9x9 expansion only for this synchronous fixture spawn.
                playSettings.autoHomeArea = false;
                spawnedWall = GenSpawn.Spawn(
                    wall, cell, map, Rot4.North, WipeMode.Vanish);
            }
            catch (System.Exception ex)
            {
                if (!wall.Destroyed) wall.Destroy(DestroyMode.Vanish);
                Log.Error("[CA] fire fixture: wooden-wall spawn exception: " + ex);
                FireFixtureFailure(
                    "the wooden wall spawn raised " + ex.GetType().Name);
                return;
            }
            finally
            {
                playSettings.autoHomeArea = originalAutoHomeArea;
            }
            if (spawnedWall != wall || !wall.Spawned)
            {
                if (!wall.Destroyed) wall.Destroy(DestroyMode.Vanish);
                FireFixtureFailure("the wooden wall could not be spawned at " + cell);
                return;
            }

            map.areaManager.Home[cell] = home;
            if (!FireUtility.TryStartFireIn(cell, map, 0.1f, null))
            {
                wall.Destroy(DestroyMode.Vanish);
                map.areaManager.Home[cell] = originalHome;
                FireFixtureFailure("the native FireUtility rejected " + cell);
                return;
            }

            Fire fire = ParentlessFireAt(cell, map);
            bool listed = fire != null && map.listerThings
                .ThingsInGroup(ThingRequestGroup.Fire).Contains(fire);
            if (fire == null || !fire.Spawned || fire.parent != null || !listed)
            {
                if (fire != null && fire.Spawned)
                    fire.Destroy(DestroyMode.Vanish);
                wall.Destroy(DestroyMode.Vanish);
                map.areaManager.Home[cell] = originalHome;
                FireFixtureFailure(
                    "native ignition did not produce a parentless listed Fire at "
                    + cell);
                return;
            }

            // Keep the requested Home state authoritative even if spawn-time hooks
            // touched the cell while the synchronous fixture was being assembled.
            map.areaManager.Home[cell] = home;
            fireFixtureMap = map;
            fireFixtureCell = cell;
            fireFixtureWall = wall;
            fireFixtureFire = fire;
            fireFixturePawnId = pawn.thingIDNumber;
            fireFixtureOriginalHome = originalHome;
            fireFixtureAppliedHome = home;

            Log.Message("[CA] fire fixture: case "
                + (home ? "Home" : "off-Home") + ", cell " + cell
                + ", Home " + YesNo(map.areaManager.Home[cell])
                + ", wall " + wall.ThingID + " (wood), spawned-fire "
                + fire.ThingID + " (parentless; listed " + YesNo(listed) + ")");
        }

        private static bool TryFindFireFixtureCell(
            Pawn pawn, IntVec3 preferred, out IntVec3 result)
        {
            if (preferred.IsValid && FireFixtureCellIsValid(pawn, preferred))
            {
                result = preferred;
                return true;
            }

            for (int radius = 5; radius <= 12; radius++)
            {
                for (int i = 0; i < FireFixtureDirections.Length; i++)
                {
                    IntVec3 direction = FireFixtureDirections[i];
                    IntVec3 candidate = pawn.Position + new IntVec3(
                        direction.x * radius, 0, direction.z * radius);
                    if (!FireFixtureCellIsValid(pawn, candidate)) continue;
                    result = candidate;
                    return true;
                }
            }

            result = IntVec3.Invalid;
            return false;
        }

        private static bool FireFixtureCellIsValid(Pawn pawn, IntVec3 cell)
        {
            Map map = pawn.Map;
            if (map == null || !cell.InBounds(map) || cell.Fogged(map)
                || !cell.Standable(map)
                || !cell.SupportsStructureType(
                    map, ThingDefOf.Wall.terrainAffordanceNeeded)
                || cell.GetThingList(map).Count != 0
                || !GenSpawn.CanSpawnAt(
                    ThingDefOf.Wall, cell, map, Rot4.North, false))
                return false;

            bool reachable = false;
            for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                IntVec3 adjacent = cell + GenAdj.AdjacentCells[i];
                if (!adjacent.InBounds(map) || !adjacent.Walkable(map)
                    || adjacent.GetFirstBuilding(map) != null
                    || adjacent.GetFirstItem(map) != null
                    || adjacent.GetFirstPawn(map) != null)
                    return false;
                if (i < 4 && pawn.CanReach(
                    adjacent, PathEndMode.OnCell, Danger.Some))
                    reachable = true;
            }
            return reachable;
        }

        private static Fire ParentlessFireAt(IntVec3 cell, Map map)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Fire fire = things[i] as Fire;
                if (fire != null && fire.parent == null) return fire;
            }
            return null;
        }

        private static void CleanTrackedFireFixture()
        {
            Map map = fireFixtureMap;
            if (map == null)
            {
                ForgetTrackedFireFixture();
                return;
            }
            if (Current.Game == null || map.Disposed
                || !Find.Maps.Contains(map) || map.areaManager == null
                || map.areaManager.Home == null)
            {
                Log.Warning(
                    "[CA] fire fixture: exact tracked cleanup skipped; prior map is no longer active");
                ForgetTrackedFireFixture();
                return;
            }

            bool exact = true;
            try
            {
                if (fireFixtureFire != null && fireFixtureFire.Spawned)
                {
                    if (fireFixtureFire.Map == map)
                        fireFixtureFire.Destroy(DestroyMode.Vanish);
                    else
                        exact = false;
                }
                if (fireFixtureWall != null && fireFixtureWall.Spawned)
                {
                    if (fireFixtureWall.Map == map)
                        fireFixtureWall.Destroy(DestroyMode.Vanish);
                    else
                        exact = false;
                }
                if (fireFixtureCell.IsValid && fireFixtureCell.InBounds(map))
                {
                    if (map.areaManager.Home[fireFixtureCell]
                        == fireFixtureAppliedHome)
                        map.areaManager.Home[fireFixtureCell] =
                            fireFixtureOriginalHome;
                    else
                        exact = false;
                }
                else
                {
                    exact = false;
                }
                if (!exact)
                    Log.Warning(
                        "[CA] fire fixture: exact tracked cleanup was partial; tracked state changed externally");
            }
            catch (System.Exception ex)
            {
                Log.Warning(
                    "[CA] fire fixture: exact tracked cleanup failed ("
                    + ex.GetType().Name + ")");
            }
            finally
            {
                ForgetTrackedFireFixture();
            }
        }

        private static void ForgetTrackedFireFixture()
        {
            fireFixtureMap = null;
            fireFixtureCell = IntVec3.Invalid;
            fireFixtureWall = null;
            fireFixtureFire = null;
            fireFixturePawnId = -1;
            fireFixtureOriginalHome = false;
            fireFixtureAppliedHome = false;
        }

        private static void FireFixtureFailure(string reason)
        {
            string text = "[CA] fire fixture: failed - " + reason;
            Log.Warning(text);
            Messages.Message(text, MessageTypeDefOf.RejectInput, false);
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        private static string ThingFact(Thing thing)
        {
            if (thing == null) return "none";
            return thing.LabelShort + " ["
                + (thing.def != null ? thing.def.defName : thing.GetType().Name)
                + "]";
        }

        private static string FactionFact(Faction faction)
        {
            if (faction == null) return "none";
            return faction.Name.NullOrEmpty()
                ? faction.def.defName : faction.Name;
        }

        private static Thing BestPermittedMedicine(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return null;
            List<Thing> medicines = pawn.Map.listerThings
                .ThingsInGroup(ThingRequestGroup.Medicine);
            Thing best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < medicines.Count; i++)
            {
                Thing medicine = medicines[i];
                if (medicine == null || !medicine.Spawned || medicine.IsForbidden(pawn)
                    || medicine.IsBurning()
                    || !pawn.CanReserveAndReach(medicine, PathEndMode.ClosestTouch,
                        Danger.Some))
                    continue;
                float score = medicine.def.GetStatValueAbstract(StatDefOf.MedicalPotency)
                    - pawn.Position.DistanceTo(medicine.Position) * 0.002f;
                if (score > bestScore) { bestScore = score; best = medicine; }
            }
            return best;
        }

        [DebugAction("Colonist Awareness", "Knowledge census",
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void KnowledgeCensus()
        {
            var map = Find.CurrentMap;
            var know = KnowledgeMapComponent.For(map);
            if (know == null) { Log.Message("[CA] no knowledge component"); return; }
            var sb = new StringBuilder("[CA] knowledge census:\n");
            var pawns = map.mapPawns.AllPawnsSpawned;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (p.RaceProps == null || !p.RaceProps.Humanlike) continue;
                int n = know.RememberedContactCount(p);
                ThreatContactSnapshot contact;
                bool hasContact = know.TryGetFreshestRememberedContact(
                    p, out contact);
                string faction = p.Faction != null ? p.Faction.def.defName : "no-faction";
                sb.AppendLine("  " + p.LabelShort + " [" + faction + "]: "
                    + n + " remembered contact(s)"
                    + (hasContact ? ", freshest at " + contact.Cell
                        + ", state " + contact.State.ToString().ToLowerInvariant()
                        + ", state age " + (now - contact.StateTick) + " ticks"
                        + ", actionable "
                        + (contact.State == ThreatContactState.Active ? "yes" : "no")
                        + ", source " + contact.Evidence.Source.ToString().ToLowerInvariant()
                        + "#" + contact.Evidence.SourceId
                        + ", source age " + (now - contact.SourceTick) + " ticks"
                        + ", acquired age " + (now - contact.AcquiredTick) + " ticks"
                        + ", confidence " + contact.Evidence.Confidence.ToString("F2")
                        + ", uncertainty " + contact.Evidence.Uncertainty.ToString("F1")
                        + ", channel " + contact.Evidence.DeliveryChannel.ToString().ToLowerInvariant()
                        + ", reporter #" + contact.Evidence.ReporterId
                        : ""));
            }
            Log.Message(sb.ToString());
        }

        [DebugAction("Colonist Awareness", "Audible cue census",
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AudibleCueCensus()
        {
            var map = Find.CurrentMap;
            var cues = AudibleCueMapComponent.For(map);
            if (cues == null) { Log.Message("[CA] no audible-cue component"); return; }
            var sb = new StringBuilder("[CA] audible cue census:\n");
            var pawns = map.mapPawns.AllPawnsSpawned;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn.RaceProps == null
                    || (!pawn.RaceProps.Humanlike && !pawn.RaceProps.Animal))
                    continue;
                AudibleCueSnapshot cue;
                bool hasCue = cues.TryGetFreshestCue(pawn, out cue);
                // Keep wildlife from flooding the census, while still surfacing
                // every animal that actually retained a cue.
                if (pawn.RaceProps.Animal && pawn.Faction != Faction.OfPlayer
                    && !hasCue) continue;
                if (!hasCue)
                {
                    sb.AppendLine("  " + pawn.LabelShort + ": no fresh audible cue");
                    continue;
                }
                sb.AppendLine("  " + pawn.LabelShort + ": "
                    + cue.AcousticClass.ToString().ToLowerInvariant()
                    + " " + cue.Kind.ToString().ToLowerInvariant()
                    + " near " + cue.ApproximateCell
                    + " +/-" + cue.UncertaintyRadius.ToString("F1")
                    + ", " + cue.PulseCount + " pulse(s), age "
                    + (now - cue.HeardTick) + " ticks, "
                    + cue.Provenance.ToString().ToLowerInvariant()
                    + ", channel " + cue.DeliveryChannel.ToString().ToLowerInvariant()
                    + ", reporter #" + cue.ReporterId
                    + (cue.ExpectedActivity ? ", expected" : ", unexplained")
                    + (cue.Considered ? ", considered" : ", pending"));
            }
            Log.Message(sb.ToString());
        }

        [DebugAction("Colonist Awareness", "Communications matrix",
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CommunicationsMatrix()
        {
            var map = Find.CurrentMap;
            var pawns = map.mapPawns.AllPawnsSpawned;
            var humans = new System.Collections.Generic.List<Pawn>();
            var selectedHumans = new System.Collections.Generic.List<Pawn>();
            foreach (var selectedObject in Find.Selector.SelectedObjects)
            {
                Pawn selected = selectedObject as Pawn;
                if (selected != null && selected.Map == map && selected.Faction != null
                    && selected.RaceProps != null && selected.RaceProps.Humanlike)
                    selectedHumans.Add(selected);
            }
            var sb = new StringBuilder("[CA] communications matrix:\n");
            sb.AppendLine("  CA comms "
                + (AwarenessMod.Settings != null && AwarenessMod.Settings.commsSystem
                    ? "enabled" : "disabled")
                + "; engine network outage "
                + (CommsModule.NetworkOutage(map) ? "yes" : "no"));
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike
                    || pawn.Faction == null) continue;
                humans.Add(pawn);
                int squad = SquadComponent.SquadOf(pawn);
                int team = SquadComponent.FireteamOf(pawn);
                string role;
                if (pawn.Faction != Faction.OfPlayer)
                    role = pawn.GetLord() != null
                        ? "Lord group peer" : "no CA remote group";
                else
                    role = squad > 0 && SquadComponent.LeaderPawn(squad, map) == pawn
                        ? "squad leader"
                        : squad > 0 && team > 0
                            && SquadComponent.FireteamLeadPawn(squad, team, map) == pawn
                            ? "fire-team lead"
                            : "member";
                sb.AppendLine("  " + pawn.LabelShort
                    + " [" + pawn.Faction.def.defName + "]"
                    + " squad " + squad + "/team " + team + " " + role
                    + "; headset " + CommsModule.HeadsetLabel(pawn)
                    + " (radio lines " + CommsModule.RadioLineCapacity(pawn) + ")"
                    + "; mechlink installed "
                    + (CommsModule.HasMechlink(pawn) ? "yes" : "no")
                    + ", active mental endpoint "
                    + (CommsModule.HasActiveMechlink(pawn) ? "yes" : "no")
                    + "; native free mech bandwidth "
                    + CommsModule.FreeMentalBandwidth(pawn)
                    + " (diagnostic only, not human mental-route allocation)"
                    + "; provisional voice/radio command span "
                    + CommsModule.PsychCapacityOf(pawn));
            }

            sb.AppendLine("  same-faction pair receipts (delivery and obedience are separate):");
            for (int i = 0; i < humans.Count; i++)
            {
                Pawn teller = humans[i];
                var selectedOrder = new System.Collections.Generic.List<Pawn>();
                if (selectedHumans.Contains(teller))
                {
                    for (int selectedIndex = 0;
                        selectedIndex < selectedHumans.Count; selectedIndex++)
                    {
                        Pawn selected = selectedHumans[selectedIndex];
                        if (selected.Faction == teller.Faction)
                            selectedOrder.Add(selected);
                    }
                }
                for (int j = 0; j < humans.Count; j++)
                {
                    Pawn listener = humans[j];
                    if (teller == listener || teller.Faction != listener.Faction) continue;
                    CommunicationChannel routine;
                    CommunicationChannel strategic;
                    bool hasRoutine = CommsModule.TryGetKnowledgeChannel(teller, listener,
                        -1f, out routine);
                    bool hasStrategic = CommsModule.TryGetStrategicChannel(teller, listener,
                        -1f, out strategic);
                    bool selectedSetOrder = selectedOrder.Count > 1
                        && selectedOrder.Contains(listener);
                    CommandRouteReceipt orderReceipt;
                    bool orderRoute;
                    if (selectedSetOrder)
                        orderRoute = CommsModule.CanRelayOrder(teller, listener,
                            selectedOrder, out orderReceipt);
                    else
                        orderRoute = CommsModule.CanRelayOrder(teller, listener,
                            null, out orderReceipt);
                    bool voice = CommsModule.HasVoiceLine(teller, listener,
                        CommsModule.VoiceRange);
                    string obedienceReason;
                    Obedience obedience = Authority.Check(teller, listener,
                        out obedienceReason);
                    sb.AppendLine("    " + teller.LabelShort + " -> " + listener.LabelShort
                        + ": routine " + (hasRoutine ? routine.ToString().ToLowerInvariant() : "none")
                        + ", strategic " + (hasStrategic ? strategic.ToString().ToLowerInvariant() : "none")
                        + (selectedSetOrder ? ", selected-set order "
                            : ", single-recipient order ")
                        + (orderRoute ? "DELIVERED via "
                            + orderReceipt.Channel.ToString().ToLowerInvariant()
                            : "BLOCKED [" + orderReceipt.Failure + "]")
                        + " - " + orderReceipt.Detail
                        + "; mental "
                        + (CommsModule.HasMentalLine(teller, listener)
                            ? "available" : "unavailable")
                        + ", headset radio "
                        + (CommsModule.HasRadioLine(teller, listener)
                            ? "available" : "unavailable")
                        + (voice ? ", voice available" : ", voice unavailable")
                        + ", gesture "
                        + (CommsModule.HasLocalCommandGesture(teller, listener)
                            ? "available" : "unavailable")
                        + "; obedience if delivered "
                        + obedience.ToString().ToLowerInvariant()
                        + (obedienceReason != null ? " (" + obedienceReason + ")" : ""));
                }
            }
            Log.Message(sb.ToString());
        }

        [DebugAction("Colonist Awareness", "Home planning census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HomePlanningCensus()
        {
            AutonomousHomeMapComponent component =
                AutonomousHomeMapComponent.For(Find.CurrentMap);
            Log.Message(component != null
                ? component.Census()
                : "[CA] home planning: no map component");
        }

        [DebugAction("Colonist Awareness", "Home prerequisite census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HomePrerequisiteCensus()
        {
            CAHomePrerequisiteMapComponent component =
                CAHomePrerequisiteMapComponent.For(Find.CurrentMap);
            Log.Message(component != null
                ? component.Census()
                : "[CA] home prerequisites: no map component");
        }

        [DebugAction("Colonist Awareness", "Storage-program census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StorageProgramCensus()
        {
            CAStorageProgramMapComponent component =
                CAStorageProgramMapComponent.For(Find.CurrentMap);
            Log.Message(component != null
                ? component.Census()
                : "[CA] storage programs: no map component");
        }

        [DebugAction("Colonist Awareness", "Space-program census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PlannedUseCensus()
        {
            PlannedUseMapComponent component =
                PlannedUseMapComponent.For(Find.CurrentMap);
            Log.Message(component != null
                ? component.Census()
                : "[CA] space programs: no map component");
        }

        [DebugAction("Colonist Awareness", "Settlement planning context",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SettlementPlanningContextCensus()
        {
            Log.Message(SettlementPlanningContextReceipt());
        }

        internal static string SettlementPlanningContextReceipt()
        {
            CASettlementPlanningContextMapComponent component =
                CASettlementPlanningContextMapComponent.For(Find.CurrentMap);
            return component != null
                ? component.Census()
                : "[CA] settlement planning context: no map component";
        }

        [DebugAction("Colonist Awareness", "Facility siting census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FacilitySitingCensus()
        {
            FacilitySitingReceipt();
        }

        internal static string FacilitySitingReceipt()
        {
            return CAFacilitySitingModule.Census(Find.CurrentMap);
        }

        internal static string FacilityRequirementReceipt()
        {
            return CAFacilitySitingModule.EvaluatePlayerFacilityPrograms(
                Find.CurrentMap);
        }

        [DebugAction("Colonist Awareness", "Spatial furnishing opportunities",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpatialFurnishingOpportunities()
        {
            Log.Message(SpatialFurnishingReceipt());
        }

        internal static string SpatialFurnishingReceipt()
        {
            return CASpatialFurnishingModule.Evaluate(Find.CurrentMap);
        }

        [DebugAction("Colonist Awareness",
            "Authored Bedroom requirement comparison",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BedroomRequirementComparison()
        {
            Log.Message(BedroomRequirementReceipt());
        }

        internal static string BedroomRequirementReceipt()
        {
            return CASpatialFurnishingModule
                .EvaluatePlayerBedroomRequirements(Find.CurrentMap);
        }

        [DebugAction("Colonist Awareness",
            "Authored Bedroom construction causes",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BedroomConstructionCauses()
        {
            Log.Message(BedroomConstructionCauseReceipt());
        }

        internal static string BedroomConstructionCauseReceipt()
        {
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(Find.CurrentMap);
            return home != null
                ? home.EvaluatePlayerBedroomConstructionCauses()
                : "[CA] authored Bedroom construction causes: home planning "
                    + "component unavailable";
        }

        [DebugAction("Colonist Awareness", "Animal infrastructure evaluation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AnimalInfrastructureEvaluation()
        {
            Log.Message(AnimalInfrastructureReceipt());
        }

        internal static string AnimalInfrastructureReceipt()
        {
            return CAAnimalInfrastructureModule.Evaluate(Find.CurrentMap);
        }

        [DebugAction("Colonist Awareness", "Critical storage evidence",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CriticalStorageEvidence()
        {
            Log.Message(CriticalStorageEvidenceReceipt());
        }

        internal static string CriticalStorageEvidenceReceipt()
        {
            return CACriticalStorageEvidenceModule.Evaluate(Find.CurrentMap);
        }

        internal static string LoadSpatialObservationReceipt(
            string commandName, string saveName,
            bool exactTargetAndCurrentGuard = false)
        {
            string savePath = GenFilePaths.FilePathForSavedGame(saveName);
            if (!File.Exists(savePath))
                return "[CA] agent receipt: " + commandName + "\n"
                    + "refused; fixed observation save is absent: " + savePath;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            GameDataSaveLoader.LoadGame(saveName);
            return "[CA] agent receipt: " + commandName + "\n"
                + "queued native load of fixed observation save " + savePath
                + "; SHA256 " + FileSha256(savePath)
                + (exactTargetAndCurrentGuard
                    ? "; exact target and current disk identities were authorized"
                    : "") + "; this command writes no save";
        }

        internal static string PrepareControlledBedroomRequirementReceipt()
        {
            Map map = Find.CurrentMap;
            const string command = "bedroom-requirement-prepare-controlled";
            if (map == null)
                return "[CA] agent receipt: " + command
                    + "\nrefused; no loaded map";

            var anchor = new IntVec3(28, 0, 182);
            Room room = anchor.InBounds(map) ? anchor.GetRoom(map) : null;
            var expected = new HashSet<IntVec3>();
            for (int x = 25; x <= 31; x++)
                for (int z = 179; z <= 185; z++)
                    expected.Add(new IntVec3(x, 0, z));
            if (room == null || !room.ProperRoom || room.IsDoorway
                || room.PsychologicallyOutdoors || room.Fogged
                || !room.Cells.ToHashSet().SetEquals(expected))
                return "[CA] agent receipt: " + command
                    + "\nrefused; the exact historical 49-cell controlled "
                    + "Bedroom room is unavailable at " + anchor;

            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            if (component == null || expected.Any(cell =>
                    component.ProgramAt(cell) != null))
                return "[CA] agent receipt: " + command
                    + "\nrefused; the exact controlled room is already "
                    + "programmed; no authority was replaced";

            Pawn dolly = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactDollyIdentity);
            Pawn luc = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactLucIdentity);
            if (dolly == null || luc == null || dolly.GetFirstSpouse() != luc
                || luc.GetFirstSpouse() != dolly
                || !LovePartnerRelationUtility.LovePartnerRelationExists(
                    dolly, luc) || !BedUtility.WillingToShareBed(dolly, luc))
                return "[CA] agent receipt: " + command
                    + "\nrefused; exact reciprocal Dolly-Luc spouse identity "
                    + "and native sharing willingness are required";

            ThingDef doubleBed = DefDatabase<ThingDef>
                .GetNamedSilentFail("DoubleBed");
            ThingDef stuff = doubleBed == null ? null
                : GenStuff.DefaultStuffFor(doubleBed);
            var target = new IntVec3(28, 0, 185);
            Rot4 rotation = Rot4.Invalid;
            foreach (Rot4 candidate in new[]
            {
                Rot4.North, Rot4.East, Rot4.South, Rot4.West
            })
            {
                if (doubleBed == null || stuff == null) break;
                CellRect footprint = GenAdj.OccupiedRect(target, candidate,
                    doubleBed.Size);
                if (!footprint.Cells.All(expected.Contains)
                    || footprint.Cells.Any(cell => cell.GetThingList(map)
                        .Any(thing => thing is Building
                            || thing is Blueprint || thing is Frame
                            || thing is Pawn
                            || thing.def.category == ThingCategory.Item
                            || thing.def.category == ThingCategory.Plant)))
                    continue;
                AcceptanceReport placement = GenConstruct.CanPlaceBlueprintAt(
                    doubleBed, target, candidate, map, godMode: false, null,
                    null, stuff);
                if (!placement.Accepted) continue;
                rotation = candidate;
                break;
            }
            if (doubleBed == null || stuff == null || !rotation.IsValid)
                return "[CA] agent receipt: " + command
                    + "\nrefused; the historically proven DoubleBed position "
                    + target + " no longer accepts the loaded native definition";

            CASpaceProgram program = null;
            Building_Bed bed = null;
            Building_Bed priorLucBed = luc.ownership?.OwnedBed;
            Building_Bed priorDollyBed = dolly.ownership?.OwnedBed;
            CASpaceProgram priorLucProgram = component.ResidentProgramFor(luc);
            CASpaceProgram priorDollyProgram = component.ResidentProgramFor(
                dolly);
            var priorRosterAuthors = new Dictionary<CASpaceProgram,
                CAResidentRosterAuthor>();
            if (priorLucProgram != null)
                priorRosterAuthors[priorLucProgram] =
                    priorLucProgram.residentAuthor;
            if (priorDollyProgram != null)
                priorRosterAuthors[priorDollyProgram] =
                    priorDollyProgram.residentAuthor;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            try
            {
                bed = ThingMaker.MakeThing(doubleBed, stuff) as Building_Bed;
                if (bed == null)
                    throw new InvalidOperationException(
                        "loaded DoubleBed did not create Building_Bed");
                bed.SetFactionDirect(Faction.OfPlayer);
                CompQuality quality = bed.TryGetComp<CompQuality>();
                quality?.SetQuality(QualityCategory.Normal,
                    ArtGenerationContext.Colony);
                GenSpawn.Spawn(bed, target, map, rotation, WipeMode.Vanish);

                var draft = new CASpaceProgramDraft
                {
                    label = "Controlled functional Bedroom",
                    purpose = CASpacePurpose.Bedroom,
                    style = CASpaceStyle.Adaptive,
                    maxOccupants = 2,
                    requiresSleep = true
                };
                program = component.CreateProgram(draft, expected);
                string reason;
                if (!component.SetResident(program, luc, assigned: true,
                        out reason))
                    throw new InvalidOperationException("Luc assignment: "
                        + reason);
                if (!component.SetResident(program, dolly, assigned: true,
                        out reason))
                    throw new InvalidOperationException("Dolly assignment: "
                        + reason);

                List<Building_Bed> roomBeds = room.ContainedAndAdjacentThings
                    .OfType<Building_Bed>().Where(candidate => candidate.Spawned
                        && candidate.def.building?.bed_humanlike == true
                        && candidate.def.building
                            .bed_countsForBedroomOrBarracks).ToList();
                if (program.author != CASpaceAuthor.Player
                    || program.purpose != CASpacePurpose.Bedroom
                    || program.residentAuthor != CAResidentRosterAuthor.Player
                    || program.residents.Count != 2
                    || !program.residents.Contains(luc)
                    || !program.residents.Contains(dolly)
                    || bed.OwnersForReading.Count != 2
                    || !bed.OwnersForReading.Contains(luc)
                    || !bed.OwnersForReading.Contains(dolly)
                    || !RoomRoleWorker_Bedroom.IsBedroom(roomBeds)
                    || room.Role != RoomRoleDefOf.Bedroom)
                    throw new InvalidOperationException(
                        "native Bedroom role, authority, residents, or bed "
                        + "ownership did not reconcile");

                return "[CA] agent receipt: " + command + "\nprepared the "
                    + "exact previously exercised 49-cell controlled room as "
                    + "player-author program #" + program.id
                    + " with reciprocal spouses Luc and Dolly owning native "
                    + doubleBed.defName + " #" + bed.thingIDNumber + " at "
                    + target + " facing " + rotation + "; native role "
                    + room.Role.defName + ". This fixture setup is separate "
                    + "from the read-only evaluator, remains paused, and has "
                    + "not saved or changed the retained source checkpoint.";
            }
            catch (Exception exception)
            {
                bool programCleared = true;
                bool lucProgramRestored = false;
                bool dollyProgramRestored = false;
                bool rosterAuthorsRestored = false;
                bool lucRestored = false;
                bool dollyRestored = false;
                bool fixtureBedRemoved = false;
                string cleanupFailure = null;
                try
                {
                    if (program?.cells != null && program.cells.Count > 0)
                    {
                        List<IntVec3> claimed = program.cells.ToList();
                        component.ClearCells(claimed);
                        programCleared = claimed.All(cell =>
                            component.ProgramAt(cell) != program);
                    }
                    // Restore exact ownership before SetResident: that API may
                    // otherwise claim a different empty bed in the prior
                    // program while rebuilding membership.
                    lucRestored = RestoreFixtureOwnedBed(luc, priorLucBed);
                    dollyRestored = RestoreFixtureOwnedBed(dolly,
                        priorDollyBed);
                    lucProgramRestored = RestoreFixtureResidentProgram(
                        component, luc, priorLucProgram);
                    dollyProgramRestored = RestoreFixtureResidentProgram(
                        component, dolly, priorDollyProgram);
                    foreach (KeyValuePair<CASpaceProgram,
                            CAResidentRosterAuthor> prior in
                        priorRosterAuthors)
                        prior.Key.residentAuthor = prior.Value;
                    rosterAuthorsRestored = priorRosterAuthors.All(prior =>
                        prior.Key.residentAuthor == prior.Value);
                    // SetResident can claim an available program bed when the
                    // snapshot bed was outside that program or absent. Release
                    // both unexpected claims before reclaiming either exact
                    // snapshot so even a swapped-bed cycle is recoverable.
                    bool lucUnexpectedReleased = ReleaseUnexpectedOwnedBed(
                        luc, priorLucBed);
                    bool dollyUnexpectedReleased = ReleaseUnexpectedOwnedBed(
                        dolly, priorDollyBed);
                    lucRestored = lucRestored && lucUnexpectedReleased
                        && ClaimExactOwnedBed(luc, priorLucBed);
                    dollyRestored = dollyRestored && dollyUnexpectedReleased
                        && ClaimExactOwnedBed(dolly, priorDollyBed);
                    if (bed == null || !bed.Spawned)
                    {
                        fixtureBedRemoved = true;
                    }
                    else
                    {
                        bed.Destroy(DestroyMode.Vanish);
                        fixtureBedRemoved = !bed.Spawned;
                    }
                }
                catch (Exception cleanupException)
                {
                    cleanupFailure = cleanupException.Message;
                }
                bool rollbackComplete = programCleared && lucRestored
                    && dollyRestored && lucProgramRestored
                    && dollyProgramRestored && rosterAuthorsRestored
                    && fixtureBedRemoved
                    && cleanupFailure == null;
                return "[CA] agent receipt: " + command
                    + "\nrefused after fixture reconciliation failed: "
                    + exception.Message + "; transactional rollback "
                    + (rollbackComplete ? "completed" : "INCOMPLETE")
                    + " [program cleared " + programCleared
                    + ", Luc prior program restored " + lucProgramRestored
                    + ", Dolly prior program restored "
                    + dollyProgramRestored + ", prior roster authors restored "
                    + rosterAuthorsRestored
                    + ", Luc prior bed restored " + lucRestored
                    + ", Dolly prior bed restored " + dollyRestored
                    + ", fixture bed removed " + fixtureBedRemoved
                    + (cleanupFailure == null ? "" : ", cleanup error "
                        + cleanupFailure)
                    + "]. No save or asynchronous reload was issued.";
            }
        }

        private static bool RestoreFixtureResidentProgram(
            PlannedUseMapComponent component, Pawn pawn,
            CASpaceProgram priorProgram)
        {
            if (component == null || pawn == null) return false;
            CASpaceProgram current = component.ResidentProgramFor(pawn);
            if (current == priorProgram) return true;
            if (current != null && !component.SetResident(current, pawn,
                    assigned: false, out _))
                return false;
            if (priorProgram == null)
                return component.ResidentProgramFor(pawn) == null;
            return component.SetResident(priorProgram, pawn, assigned: true,
                    out _)
                && component.ResidentProgramFor(pawn) == priorProgram;
        }

        private static bool RestoreFixtureOwnedBed(Pawn pawn,
            Building_Bed priorBed)
        {
            return ReleaseUnexpectedOwnedBed(pawn, priorBed)
                && ClaimExactOwnedBed(pawn, priorBed);
        }

        private static bool ReleaseUnexpectedOwnedBed(Pawn pawn,
            Building_Bed priorBed)
        {
            if (pawn?.ownership == null) return priorBed == null;
            Building_Bed current = pawn.ownership.OwnedBed;
            if (current == null || current == priorBed) return true;
            pawn.ownership.UnclaimBed();
            return pawn.ownership.OwnedBed == null;
        }

        private static bool ClaimExactOwnedBed(Pawn pawn,
            Building_Bed priorBed)
        {
            if (pawn?.ownership == null) return priorBed == null;
            Building_Bed current = pawn.ownership.OwnedBed;
            if (priorBed == null) return current == null;
            if (current == priorBed) return true;
            return current == null && priorBed.Spawned && !priorBed.Medical
                && pawn.ownership.ClaimBedIfNonMedical(priorBed);
        }

        internal static string SaveControlledBedroomRequirementReceipt()
        {
            Map map = Find.CurrentMap;
            const string command = "bedroom-requirement-save-controlled";
            if (!TryResolveControlledBedroomRequirement(map,
                    out CASpaceProgram program, out Building_Bed bed,
                    out string failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;

            string path = GenFilePaths.FilePathForSavedGame(
                BedroomRequirementControlledSaveName);
            if (File.Exists(path))
            {
                var existing = new FileInfo(path);
                return "[CA] agent receipt: " + command
                    + "\nrefused; the specific retained checkpoint already "
                    + "exists and was not overwritten: " + path + "; verified "
                    + existing.Length + " bytes, SHA256 "
                    + FileSha256(path);
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            GameDataSaveLoader.SaveGame(BedroomRequirementControlledSaveName);
            if (!File.Exists(path) || new FileInfo(path).Length <= 0)
                return "[CA] agent receipt: " + command
                    + "\nrefused; native SaveGame returned without producing "
                    + "the specific checkpoint at " + path;
            var saved = new FileInfo(path);
            return "[CA] agent receipt: " + command + "\nnative save verified "
                + "of player-authored Bedroom program #" + program.id
                + " with two actual residents and native " + bed.def.defName
                + " #" + bed.thingIDNumber + " to specific checkpoint "
                + path + "; " + saved.Length + " bytes, SHA256 "
                + FileSha256(path)
                + "; no baseline or retained source was overwritten";
        }

        private static string FileSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", "");
        }

        private static bool TryResolveControlledBedroomRequirement(Map map,
            out CASpaceProgram program, out Building_Bed bed,
            out string failure)
        {
            program = null;
            bed = null;
            failure = "no loaded map";
            if (map == null) return false;
            var anchor = new IntVec3(28, 0, 182);
            Room room = anchor.InBounds(map) ? anchor.GetRoom(map) : null;
            var expected = new HashSet<IntVec3>();
            for (int x = 25; x <= 31; x++)
                for (int z = 179; z <= 185; z++)
                    expected.Add(new IntVec3(x, 0, z));
            program = PlannedUseMapComponent.For(map)?
                .ProgramsForObservation.FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.author == CASpaceAuthor.Player
                    && candidate.purpose == CASpacePurpose.Bedroom
                    && candidate.cells != null
                    && candidate.cells.ToHashSet().SetEquals(expected));
            Pawn dolly = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactDollyIdentity);
            Pawn luc = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactLucIdentity);
            bed = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>().FirstOrDefault(candidate =>
                    candidate != null && candidate.Spawned
                    && candidate.def.defName == "DoubleBed"
                    && candidate.Position == new IntVec3(28, 0, 185)
                    && candidate.OccupiedRect().Cells.All(expected.Contains));
            if (room == null || !room.ProperRoom
                || !room.Cells.ToHashSet().SetEquals(expected)
                || room.Role != RoomRoleDefOf.Bedroom || program == null
                || program.residents == null || program.residents.Count != 2
                || dolly == null || luc == null
                || !program.residents.Contains(dolly)
                || !program.residents.Contains(luc) || bed == null
                || bed.OwnersForReading.Count != 2
                || !bed.OwnersForReading.Contains(dolly)
                || !bed.OwnersForReading.Contains(luc))
            {
                failure = "the exact functional controlled Bedroom, two "
                    + "residents, native role, or shared bed ownership is absent";
                return false;
            }
            failure = null;
            return true;
        }

        internal static string PrepareControlledBedroomConstructionCauseReceipt()
        {
            Map map = Find.CurrentMap;
            const string command =
                "bedroom-construction-cause-prepare-unmet";
            if (!TryResolveControlledBedroomRequirement(map,
                    out CASpaceProgram program, out Building_Bed sharedBed,
                    out string failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;

            Pawn luc = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactLucIdentity);
            Pawn dolly = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactDollyIdentity);
            if (luc == null || dolly == null
                || sharedBed.OwnersForReading.Count != 2
                || !sharedBed.OwnersForReading.Contains(luc)
                || !sharedBed.OwnersForReading.Contains(dolly))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + "the functional control no longer has exact Luc-Dolly "
                    + "shared ownership";

            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            if (home == null || spatial == null
                || home.HasPendingPlanForVerification
                || spatial.HasPendingPlan
                || CAHomePrerequisiteMapComponent.For(map)?.HasDemand == true)
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + "the functional control already has a CA construction "
                    + "commitment";

            ThingDef singleDef = ThingDefOf.Bed;
            ThingDef stuff = sharedBed.Stuff
                ?? GenStuff.DefaultStuffFor(singleDef);
            IntVec3 position = sharedBed.Position;
            Rot4 rotation = sharedBed.Rotation;
            ThingDef sharedDef = sharedBed.def;
            ThingDef sharedStuff = sharedBed.Stuff;
            QualityCategory sharedQuality = sharedBed
                .TryGetComp<CompQuality>()?.Quality ?? QualityCategory.Normal;
            Building_Bed singleBed = null;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            try
            {
                luc.ownership?.UnclaimBed();
                dolly.ownership?.UnclaimBed();
                sharedBed.Destroy(DestroyMode.Vanish);

                if (singleDef == null || stuff == null
                    || GenAdj.OccupiedRect(position, rotation,
                        singleDef.Size).Cells.Any(cell =>
                            !program.cells.Contains(cell)
                            || cell.GetThingList(map).Any(thing =>
                                thing is Building || thing is Blueprint
                                || thing is Frame || thing is Pawn
                                || thing.def.category == ThingCategory.Item
                                || thing.def.category == ThingCategory.Plant)))
                    throw new InvalidOperationException(
                        "the exact replacement footprint is unavailable");
                AcceptanceReport placement = GenConstruct.CanPlaceBlueprintAt(
                    singleDef, position, rotation, map, godMode: false, null,
                    null, stuff);
                if (!placement.Accepted)
                    throw new InvalidOperationException(
                        "native Bed placement rejected the exact replacement: "
                        + placement.Reason);

                singleBed = ThingMaker.MakeThing(singleDef, stuff)
                    as Building_Bed;
                if (singleBed == null)
                    throw new InvalidOperationException(
                        "the loaded Bed definition did not create Building_Bed");
                singleBed.SetFactionDirect(Faction.OfPlayer);
                singleBed.TryGetComp<CompQuality>()?.SetQuality(
                    QualityCategory.Normal, ArtGenerationContext.Colony);
                GenSpawn.Spawn(singleBed, position, map, rotation,
                    WipeMode.Vanish);
                if (luc.ownership == null
                    || !luc.ownership.ClaimBedIfNonMedical(singleBed))
                    throw new InvalidOperationException(
                        "native ownership refused Luc on the replacement Bed");
                singleBed.NotifyRoomAssignedPawnsChanged();
                map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

                if (!TryResolveControlledBedroomConstructionCause(map,
                        out CASpaceProgram resolvedProgram,
                        out Building_Bed resolvedBed,
                        out Pawn targetResident, out failure))
                    throw new InvalidOperationException(failure);
                return "[CA] agent receipt: " + command + "\nprepared a "
                    + "separate unsaved positive fixture from the retained "
                    + "functional control: Player-authored Bedroom program #"
                    + resolvedProgram.id + " still has exact residents Luc and "
                    + "Dolly at maximum 2, while native "
                    + resolvedBed.def.defName + " #"
                    + resolvedBed.thingIDNumber + " at "
                    + resolvedBed.Position + " facing "
                    + resolvedBed.Rotation + " has one slot owned by Luc; "
                    + targetResident.LabelShort + " is the one concrete "
                    + "resident lacking owned sleeping capacity inside the "
                    + "49-cell footprint. Native room role remains Bedroom. "
                    + "No optional facility, blueprint, frame, authorization, "
                    + "or save was created.";
            }
            catch (Exception exception)
            {
                bool restored = RestoreControlledFunctionalBedroom(map,
                    program, luc, dolly, singleBed, sharedDef, sharedStuff,
                    position, rotation, sharedQuality, out string rollback);
                return "[CA] agent receipt: " + command
                    + "\nrefused after the causal fixture transition failed: "
                    + exception.Message + "; semantic rollback "
                    + (restored ? "completed" : "INCOMPLETE") + " ["
                    + rollback + "]. The retained source save was not written.";
            }
        }

        internal static string SaveControlledBedroomConstructionCauseReceipt()
        {
            Map map = Find.CurrentMap;
            const string command = "bedroom-construction-cause-save-unmet";
            if (!TryResolveControlledBedroomConstructionCause(map,
                    out CASpaceProgram program, out Building_Bed bed,
                    out Pawn targetResident, out string failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;

            string path = GenFilePaths.FilePathForSavedGame(
                BedroomConstructionCauseUnmetSaveName);
            if (File.Exists(path))
            {
                var existing = new FileInfo(path);
                return "[CA] agent receipt: " + command
                    + "\nrefused; the specific unmet-capacity checkpoint "
                    + "already exists and was not overwritten: " + path
                    + "; verified " + existing.Length + " bytes, SHA256 "
                    + FileSha256(path);
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            GameDataSaveLoader.SaveGame(
                BedroomConstructionCauseUnmetSaveName);
            if (!File.Exists(path) || new FileInfo(path).Length <= 0)
                return "[CA] agent receipt: " + command
                    + "\nrefused; native SaveGame returned without producing "
                    + "the specific checkpoint at " + path;
            string controlPath = GenFilePaths.FilePathForSavedGame(
                BedroomRequirementControlledSaveName);
            var saved = new FileInfo(path);
            return "[CA] agent receipt: " + command
                + "\nnative save verified of Player-authored Bedroom program #"
                + program.id + " with Luc owning one completed "
                + bed.def.defName + " and concrete target resident "
                + targetResident.LabelShort + " lacking one owned slot, to "
                + "specific derived checkpoint " + path + "; "
                + saved.Length + " bytes, SHA256 " + FileSha256(path)
                + ". The retained functional control remains separate at "
                + controlPath + (File.Exists(controlPath)
                    ? "; SHA256 " + FileSha256(controlPath) : "; file absent")
                + "; no baseline was overwritten.";
        }

        internal static string BedroomConstructionFunctionalControlReceipt()
        {
            Map map = Find.CurrentMap;
            const string command =
                "bedroom-construction-cause-functional-control";
            if (!TryResolveControlledBedroomRequirement(map,
                    out CASpaceProgram program, out Building_Bed bed,
                    out string failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            if (home == null || spatial == null)
                return "[CA] agent receipt: " + command
                    + "\nrefused; causal components are unavailable";

            int priorLevel = spatial.LevelFor(program);
            var outcomes = new List<string>();
            bool pass = true;
            try
            {
                for (int level = 0; level < 4; level++)
                {
                    spatial.SetLevel(program, level);
                    bool selected = home
                        .TrySelectPlayerBedroomConstructionCause(program,
                            out CAHomePlan plan, out Pawn planner,
                            out Pawn resident, out int current,
                            out int target, out string action,
                            out string blocker);
                    bool levelPass = !selected && current == 2 && target == 2
                        && resident == null && plan.def == null
                        && blocker.Contains("already owns compatible sleeping "
                            + "capacity");
                    pass &= levelPass;
                    outcomes.Add(AutonomyComponent.LevelNames[level] + "="
                        + (levelPass ? "zero-work" : "FAIL") + " ["
                        + blocker + "]");
                }
            }
            finally
            {
                spatial.SetLevel(program, priorLevel);
            }
            bool ownershipPreserved = bed.Spawned
                && bed.OwnersForReading.Count == 2
                && program.residents.Count == 2
                && !map.listerThings.AllThings.Any(thing => thing != null
                    && thing.Spawned
                    && (thing is Blueprint_Build || thing is Frame)
                    && (thing.def.entityDefToBuild as ThingDef)?.IsBed == true
                    && thing.OccupiedRect().Cells.Any(
                        program.cells.Contains));
            pass &= ownershipPreserved
                && !home.HasPendingPlanForVerification
                && !spatial.HasPendingPlanForObservation;
            return "[CA] agent receipt: " + command + "\n"
                + (pass ? "pass" : "fail") + "; exact functional Bedroom #"
                + program.id + " retained two concrete residents, one native "
                + bed.def.defName + " with two owners, and zero Bed work under "
                + "every space ceiling\n" + string.Join("\n", outcomes)
                + "\nownership and construction state preserved "
                + ownershipPreserved + "; optional Dresser, EndTable, "
                + "SleepAccelerator, other facility capacity, and Beauty "
                + "supplied no cause; prior ceiling restored to "
                + AutonomyComponent.LevelNames[priorLevel]
                + "; no save written";
        }

        internal static string BedroomConstructionCeilingsRegressionReceipt()
        {
            Map map = Find.CurrentMap;
            const string command =
                "bedroom-construction-cause-ceilings-regression";
            if (!TryResolveControlledBedroomConstructionCause(map,
                    out CASpaceProgram program, out Building_Bed bed,
                    out Pawn targetResident, out string failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            if (home == null || spatial == null
                || !TryPrepareBedroomCauseBuilder(map, out Pawn prepared,
                    out int priorAutonomy, out bool woke, out failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + (failure ?? "causal components are unavailable");

            int priorLevel = spatial.LevelFor(program);
            TimeSpeed priorSpeed = Find.TickManager.CurTimeSpeed;
            var outcomes = new List<string>();
            bool pass = true;
            IntVec3 selectedCell = IntVec3.Invalid;
            Rot4 selectedRotation = Rot4.Invalid;
            string selectedEvidence = null;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                for (int level = 0; level < 4; level++)
                {
                    spatial.SetLevel(program, level);
                    bool selected = home
                        .TrySelectPlayerBedroomConstructionCause(program,
                            out CAHomePlan plan, out Pawn planner,
                            out Pawn resident, out int current,
                            out int target, out string action,
                            out string blocker);
                    bool shouldSelect = level >= 2;
                    bool levelPass = selected == shouldSelect
                        && current == 1 && target == 2
                        && resident == targetResident
                        && (!selected || (plan.kind == CAHomePlanKind.Bed
                            && plan.def == ThingDefOf.Bed
                            && plan.targetResidentId
                                == targetResident.thingIDNumber
                            && action.Contains("construction candidate")
                            && plan.placementEvidence.Contains(
                                "protected existing bed approaches")
                            && plan.placementEvidence.Contains(
                                "protected doorway approaches")
                            && plan.placementEvidence.Contains(
                                "clear negative space")))
                        && (selected || blocker.Contains(
                            "cannot select a Bedroom action"));
                    pass &= levelPass;
                    if (selected)
                    {
                        if (!selectedCell.IsValid)
                        {
                            selectedCell = plan.cell;
                            selectedRotation = plan.rotation;
                            selectedEvidence = plan.placementEvidence;
                        }
                        else
                            pass &= selectedCell == plan.cell
                                && selectedRotation == plan.rotation;
                    }
                    outcomes.Add(AutonomyComponent.LevelNames[level] + "="
                        + (levelPass ? selected ? "selected Bed"
                            : "no action" : "FAIL") + " ["
                        + (selected ? action + " at " + plan.cell + " facing "
                            + plan.rotation : blocker) + "]");
                }
            }
            finally
            {
                spatial.SetLevel(program, priorLevel);
                AutonomyComponent.SetLevel(prepared, priorAutonomy);
                Find.TickManager.CurTimeSpeed = priorSpeed;
            }
            bool statePreserved = bed.Spawned
                && bed.OwnersForReading.Count == 1
                && bed.OwnersForReading.Contains(program.residents
                    .First(resident => resident != targetResident))
                && targetResident.ownership?.OwnedBed != bed
                && program.residents.Count == 2
                && !home.HasPendingPlanForVerification
                && !spatial.HasPendingPlanForObservation;
            pass &= statePreserved;
            return "[CA] agent receipt: " + command + "\n"
                + (pass ? "pass" : "fail") + "; exact resident cause "
                + targetResident.LabelShort + " #"
                + targetResident.thingIDNumber + ", slots 1/2; deterministic "
                + "native Bed candidate " + selectedCell + " facing "
                + selectedRotation + "\n" + string.Join("\n", outcomes)
                + "\nspatial evidence: "
                + (selectedEvidence ?? "none")
                + "\nresident roster, Luc ownership, empty construction "
                + "commitment, and prior ceiling preserved " + statePreserved
                + (woke ? "; disposable builder was awakened" : "")
                + "; no blueprint, optional facility, or save created";
        }

        internal static string BedroomConstructionPlayerPrecedenceReceipt()
        {
            Map map = Find.CurrentMap;
            const string command =
                "bedroom-construction-cause-player-precedence-regression";
            if (!TryResolveControlledBedroomConstructionCause(map,
                    out CASpaceProgram program, out Building_Bed bed,
                    out Pawn targetResident, out string failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            if (home == null || spatial == null
                || !TryPrepareBedroomCauseBuilder(map, out Pawn author,
                    out int priorAutonomy, out bool woke, out failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + (failure ?? "causal components are unavailable");

            int priorLevel = spatial.LevelFor(program);
            TimeSpeed priorSpeed = Find.TickManager.CurTimeSpeed;
            Thing construction = null;
            Blueprint_Build blueprint = null;
            string blueprintBlocker = "not evaluated";
            string frameBlocker = "not evaluated";
            bool blueprintBlocked = false;
            bool frameBlocked = false;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                spatial.SetLevel(program, 2);
                if (!home.TrySelectPlayerBedroomConstructionCause(program,
                        out CAHomePlan plan, out Pawn planner,
                        out Pawn resident, out int current, out int target,
                        out string action, out string blocker)
                    || plan.def != ThingDefOf.Bed
                    || resident != targetResident || current != 1
                    || target != 2)
                    return "[CA] agent receipt: " + command
                        + "\nrefused; no clean native Bed candidate: "
                        + blocker;

                blueprint = GenConstruct.PlaceBlueprintForBuild(plan.def,
                    plan.cell, map, plan.rotation, Faction.OfPlayer,
                    plan.stuff, null, Faction.OfPlayer.ideos?.PrimaryIdeo?
                        .GetStyleFor(plan.def));
                construction = blueprint;
                if (blueprint == null)
                    return "[CA] agent receipt: " + command
                        + "\nfailed; native player blueprint placement "
                        + "returned null";
                if (plan.def.PlaceWorkers != null)
                    for (int i = 0; i < plan.def.PlaceWorkers.Count; i++)
                        plan.def.PlaceWorkers[i].PostPlace(map, plan.def,
                            plan.cell, plan.rotation);
                bool selectedBesideBlueprint = home
                    .TrySelectPlayerBedroomConstructionCause(program,
                        out CAHomePlan ignoredBlueprintPlan,
                        out Pawn ignoredBlueprintPlanner,
                        out Pawn ignoredBlueprintResident,
                        out int ignoredBlueprintCurrent,
                        out int ignoredBlueprintTarget,
                        out string ignoredBlueprintAction,
                        out blueprintBlocker);
                blueprintBlocked = !selectedBesideBlueprint
                    && blueprintBlocker.Contains(
                        "player Bed construction already has precedence")
                    && blueprintBlocker.Contains("blueprint");

                bool transitioned = blueprint.TryReplaceWithSolidThing(author,
                    out Thing createdThing, out bool jobEnded);
                construction = createdThing ?? construction;
                bool frameCreated = transitioned && !jobEnded
                    && createdThing is Frame && createdThing.Spawned
                    && (createdThing.def.entityDefToBuild as ThingDef)
                        == ThingDefOf.Bed;
                if (frameCreated)
                {
                    bool selectedBesideFrame = home
                        .TrySelectPlayerBedroomConstructionCause(program,
                            out CAHomePlan ignoredFramePlan,
                            out Pawn ignoredFramePlanner,
                            out Pawn ignoredFrameResident,
                            out int ignoredFrameCurrent,
                            out int ignoredFrameTarget,
                            out string ignoredFrameAction,
                            out frameBlocker);
                    frameBlocked = !selectedBesideFrame
                        && frameBlocker.Contains(
                            "player Bed construction already has precedence")
                        && frameBlocker.Contains("frame");
                }
                else
                    frameBlocker = "native blueprint-to-frame transition failed";

                if (construction != null && !construction.Destroyed)
                    construction.Destroy(DestroyMode.Cancel);
                construction = null;
                blueprint = null;
                bool cleanup = !map.listerThings.AllThings.Any(thing =>
                    thing != null && thing.Spawned
                    && (thing is Blueprint_Build || thing is Frame)
                    && (thing.def.entityDefToBuild as ThingDef)?.IsBed == true
                    && thing.OccupiedRect().Cells.Any(
                        program.cells.Contains));
                bool ownershipPreserved = bed.Spawned
                    && bed.OwnersForReading.Count == 1
                    && targetResident.ownership?.OwnedBed != bed
                    && program.residents.Count == 2;
                bool pass = blueprintBlocked && frameBlocked && cleanup
                    && ownershipPreserved
                    && !home.HasPendingPlanForVerification;
                return "[CA] agent receipt: " + command + "\n"
                    + (pass ? "pass" : "fail") + "; exact Player-authored "
                    + "Bedroom #" + program.id + " native Bed candidate at "
                    + plan.cell + " facing " + plan.rotation
                    + "\nblueprint precedence " + blueprintBlocked + ": "
                    + blueprintBlocker + "\nframe precedence "
                    + frameBlocked + ": " + frameBlocker
                    + "\ncleanup " + cleanup + ", roster and existing "
                    + "ownership preserved " + ownershipPreserved
                    + "; optional facilities supplied no cause; no save written"
                    + (woke ? "; disposable builder was awakened" : "");
            }
            finally
            {
                if (construction != null && !construction.Destroyed)
                    construction.Destroy(DestroyMode.Cancel);
                if (blueprint != null && !blueprint.Destroyed)
                    blueprint.Destroy(DestroyMode.Cancel);
                spatial.SetLevel(program, priorLevel);
                AutonomyComponent.SetLevel(author, priorAutonomy);
                Find.TickManager.CurTimeSpeed = priorSpeed;
            }
        }

        internal static string StartBedroomConstructionCauseReceipt()
        {
            Map map = Find.CurrentMap;
            const string command = "bedroom-construction-cause-start";
            if (!TryResolveControlledBedroomConstructionCause(map,
                    out CASpaceProgram program, out Building_Bed bed,
                    out Pawn targetResident, out string failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            CAHomePrerequisiteMapComponent prerequisites =
                CAHomePrerequisiteMapComponent.For(map);
            AwarenessSettings settings = AwarenessMod.Settings;
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            if (home == null || spatial == null || prerequisites == null
                || settings == null || bridge == null)
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + (failure ?? "causal components are unavailable");
            if (bridge.HasActiveBedroomCausePlanningScope)
                return "[CA] agent receipt: " + command
                    + "\nrefused; another exact Bedroom-cause scope is already active: "
                    + bridge.BedroomCausePlanningScopeEvidence;
            if (!TryPrepareBedroomCauseBuilder(map, out Pawn prepared,
                    out int priorAutonomy, out bool woke, out failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + (failure ?? "no exact Bedroom-cause builder is available");

            int priorLevel = spatial.LevelFor(program);
            TimeSpeed priorSpeed = Find.TickManager.CurTimeSpeed;
            Thing createdConstruction = null;
            bool committed = false;
            bool futureSupplyPhase = false;
            bool planningScopeRecorded = false;
            bool demandExistedBeforeAttempt = prerequisites.HasDemand;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                spatial.SetLevel(program, 2);
                if (!home.TrySelectPlayerBedroomConstructionCause(program,
                        out CAHomePlan candidate, out Pawn planner,
                        out Pawn resident, out int current, out int target,
                        out string action, out string blocker)
                    || candidate.def != ThingDefOf.Bed
                    || resident != targetResident || current != 1
                    || target != 2)
                    return "[CA] agent receipt: " + command
                        + "\nrefused; no exact one-resident native Bed cause: "
                        + blocker;
                bool placed = home.DebugPlacePlayerBedroomConstructionCause(
                    program, out string placementOutcome);
                List<Thing> constructions = BedConstructionInProgram(map,
                    program);
                createdConstruction = constructions.Count == 1
                    ? constructions[0] : null;
                bool exactBlueprint = placed && constructions.Count == 1
                    && createdConstruction is Blueprint_Build
                    && (createdConstruction.def.entityDefToBuild as ThingDef)
                        == ThingDefOf.Bed;
                bool homeCommitment = exactBlueprint
                    && home.PendingPlanMatchesForVerification(program.id,
                        targetResident.thingIDNumber, createdConstruction);
                string retainedState = null;
                string retainedEvidence = null;
                bool retainedMaterialCause = !placed
                    && CAHomePrerequisiteMapComponent.For(map)?.HasDemand == true
                    && home.TryVerifyContextualBedPlan(program,
                        out retainedState, out retainedEvidence)
                    && retainedState == "objective retained";
                bool spatialAttempt = spatial.DebugPlanNow(
                    out string spatialOutcome);
                bool spatialBlocked = !spatialAttempt
                    && (spatialOutcome.Contains(
                            "waiting for active home construction objective")
                        || spatialOutcome.Contains(
                            "waiting for the retained home construction objective"));
                bool secondSelected = home
                    .TrySelectPlayerBedroomConstructionCause(program,
                        out CAHomePlan ignoredPlan, out Pawn ignoredPlanner,
                        out Pawn ignoredResident, out int ignoredCurrent,
                        out int ignoredTarget, out string ignoredAction,
                        out string commitmentBlocker);
                bool causeBlocked = !secondSelected
                    && commitmentBlocker != null
                    && (commitmentBlocker.Contains(
                            "waiting for active home construction objective")
                        || commitmentBlocker.Contains(
                            "waiting for the retained home construction objective"));
                bool authorityPreserved = program.residents.Count == 2
                    && program.residents.Contains(targetResident)
                    && bed.Spawned && bed.OwnersForReading.Count == 1
                    && targetResident.ownership?.OwnedBed != bed;
                bool causeCommitted = ((exactBlueprint && homeCommitment)
                        || retainedMaterialCause)
                    && spatialBlocked && causeBlocked && authorityPreserved;
                if (causeCommitted)
                    planningScopeRecorded = bridge
                        .RecordBedroomCausePlanningScope(map, program.id,
                            targetResident.thingIDNumber,
                            exactBlueprint ? createdConstruction.ThingID : null,
                            requiresScopedLease:
                                !settings.autonomousHomePlanning);
                committed = causeCommitted && planningScopeRecorded;
                futureSupplyPhase = committed && retainedMaterialCause;
                return "[CA] agent receipt: " + command + "\n"
                    + (committed ? "pass" : "fail")
                    + "; selected native action " + action + ", definition "
                    + candidate.def.defName + ", concrete resident "
                    + targetResident.LabelShort + " #"
                    + targetResident.thingIDNumber + ", slots 1/2, at "
                    + candidate.cell + " facing " + candidate.rotation
                    + "\nplacement/material phase: " + placementOutcome
                    + "\nhome blueprint commitment " + homeCommitment + ": "
                    + home.PendingPlanForVerification
                    + "\nretained material cause " + retainedMaterialCause
                    + ": " + (retainedEvidence ?? "none")
                    + "\nspatial consumer blocked " + spatialBlocked + ": "
                    + spatialOutcome + "\nsecond Bedroom cause blocked "
                    + causeBlocked + ": " + commitmentBlocker
                    + "\nresident roster and existing ownership preserved "
                    + authorityPreserved + "; initiative ceiling now "
                    + AutonomyComponent.LevelNames[spatial.LevelFor(program)]
                    + "; home planning "
                    + (committed
                        ? bridge.BedroomCausePlanningScopeEvidence
                        : "no cause-bound lease was recorded because the exact commitment did not reconcile")
                    + "; optional facilities supplied no cause; no material "
                    + "spawned, no job forced, and no save written; time "
                    + (futureSupplyPhase
                        ? "set Normal for native supply work"
                        : "left at its prior speed")
                    + (woke ? "; fixture author was awakened" : "");
            }
            finally
            {
                AutonomyComponent.SetLevel(prepared, priorAutonomy);
                Find.TickManager.CurTimeSpeed = futureSupplyPhase
                    ? TimeSpeed.Normal : priorSpeed;
                if (!committed)
                {
                    if (!demandExistedBeforeAttempt)
                        prerequisites.CancelUnselectedObjective(
                            "the bounded Bedroom-cause regression did not reconcile");
                    if (createdConstruction != null
                        && !createdConstruction.Destroyed)
                        createdConstruction.Destroy(DestroyMode.Cancel);
                    spatial.SetLevel(program, priorLevel);
                }
            }
        }

        internal static string SaveActiveBedroomConstructionCauseReceipt()
        {
            Map map = Find.CurrentMap;
            const string command = "bedroom-construction-cause-save-active";
            if (!TryResolveControlledBedroomConstructionCause(map,
                    out CASpaceProgram program, out Building_Bed bed,
                    out Pawn targetResident, out string failure,
                    allowPendingBedConstruction: true))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            List<Thing> constructions = BedConstructionInProgram(map,
                program);
            string originThingId = home?
                .PendingOriginThingIdForVerification;
            bool planningScope = bridge?.BedroomCausePlanningScopeMatches(
                map, program.id, targetResident.thingIDNumber,
                originThingId) == true;
            bool active = constructions.Count == 1
                && (constructions[0] is Blueprint_Build
                    || constructions[0] is Frame)
                && (constructions[0].def.entityDefToBuild as ThingDef)
                    == ThingDefOf.Bed
                && home?.PendingPlanMatchesForVerification(program.id,
                    targetResident.thingIDNumber, constructions[0]) == true
                && spatial?.LevelFor(program) >= 2
                && spatial.HasPendingPlanForObservation == false
                && planningScope;
            if (!active)
                return "[CA] agent receipt: " + command
                    + "\nrefused; the exact targeted native Bed blueprint/frame "
                    + "commitment or cause-bound planning lease is not active; "
                    + (bridge?.BedroomCausePlanningScopeEvidence ?? "none");

            string path = GenFilePaths.FilePathForSavedGame(
                BedroomConstructionCauseActiveSaveName);
            if (File.Exists(path))
            {
                var existing = new FileInfo(path);
                return "[CA] agent receipt: " + command
                    + "\nrefused; the specific active checkpoint already "
                    + "exists and was not overwritten: " + path
                    + "; verified " + existing.Length + " bytes, SHA256 "
                    + FileSha256(path);
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            GameDataSaveLoader.SaveGame(
                BedroomConstructionCauseActiveSaveName);
            if (!File.Exists(path) || new FileInfo(path).Length <= 0)
                return "[CA] agent receipt: " + command
                    + "\nrefused; native SaveGame produced no checkpoint at "
                    + path;
            var saved = new FileInfo(path);
            return "[CA] agent receipt: " + command
                + "\nnative save verified of targeted "
                + constructions[0].ThingID + " Bed "
                + (constructions[0] is Frame ? "frame" : "blueprint") + " for "
                + targetResident.LabelShort + " #"
                + targetResident.thingIDNumber + " in Bedroom program #"
                + program.id + " to specific active checkpoint " + path
                + "; " + saved.Length + " bytes, SHA256 "
                + FileSha256(path) + "; planning scope "
                + bridge.BedroomCausePlanningScopeEvidence
                + "; the unmet and functional controls "
                + "remain separate and were not overwritten";
        }

        internal static string ReconcileActiveBedroomConstructionCauseReceipt()
        {
            Map map = Find.CurrentMap;
            const string command =
                "bedroom-construction-cause-reconcile-active";
            if (!TryResolveControlledBedroomConstructionCause(map,
                    out CASpaceProgram program, out Building_Bed bed,
                    out Pawn targetResident, out string failure,
                    allowPendingBedConstruction: true))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            List<Thing> constructions = BedConstructionInProgram(map,
                program);
            bool pending = constructions.Count == 1
                && (constructions[0] is Blueprint_Build
                    || constructions[0] is Frame)
                && (constructions[0].def.entityDefToBuild as ThingDef)
                    == ThingDefOf.Bed;
            bool homeReconciled = pending
                && home?.PendingPlanMatchesForVerification(program.id,
                    targetResident.thingIDNumber, constructions[0]) == true;
            bool planningScopeReconciled = pending
                && bridge?.BedroomCausePlanningScopeMatches(map, program.id,
                    targetResident.thingIDNumber,
                    home?.PendingOriginThingIdForVerification) == true;
            CAHomePlan ignoredPlan = default(CAHomePlan);
            Pawn ignoredPlanner = null;
            Pawn resident = null;
            int current = 0;
            int target = 0;
            string ignoredAction = null;
            string blocker = "home planning component unavailable";
            bool selected = home != null
                && home.TrySelectPlayerBedroomConstructionCause(program,
                    out ignoredPlan, out ignoredPlanner, out resident,
                    out current, out target, out ignoredAction, out blocker);
            bool commitmentReconciled = !selected
                && resident == targetResident && current == 1 && target == 2
                && blocker != null && blocker.Contains(
                    "waiting for active home construction objective");
            bool statePreserved = bed.Spawned
                && bed.OwnersForReading.Count == 1
                && program.residents.Count == 2
                && spatial?.LevelFor(program) >= 2
                && spatial.HasPendingPlanForObservation == false;
            bool activeSaveReloaded = bridge?
                .LoadedSaveMatchesForVerification(
                    BedroomConstructionCauseActiveSaveName) == true;
            bool pass = pending && homeReconciled
                && planningScopeReconciled && commitmentReconciled
                && statePreserved && activeSaveReloaded;
            string path = GenFilePaths.FilePathForSavedGame(
                BedroomConstructionCauseActiveSaveName);
            return "[CA] agent receipt: " + command + "\n"
                + (pass ? "pass" : "fail") + "; active checkpoint reload "
                + activeSaveReloaded + "; observed native "
                + (pending ? constructions[0].GetType().Name + " "
                    + constructions[0].ThingID : "construction absent")
                + " for concrete resident " + targetResident.LabelShort
                + " #" + targetResident.thingIDNumber
                + "\nhome pending target reconciled " + homeReconciled
                + ": " + (home?.PendingPlanForVerification ?? "none")
                + "\ncause-bound planning scope reconciled "
                + planningScopeReconciled + ": "
                + (bridge?.BedroomCausePlanningScopeEvidence ?? "none")
                + "\none map-wide commitment reconciled "
                + commitmentReconciled + ": " + blocker
                + "\nroster, existing ownership, Proactive+ ceiling, and "
                + "empty spatial commitment preserved " + statePreserved
                + "; optional facilities supplied no cause; active checkpoint "
                + (File.Exists(path) ? "SHA256 " + FileSha256(path)
                    : "file absent") + "; no save written";
        }

        internal static string RunNativeBedroomConstructionCauseReceipt()
        {
            Map map = Find.CurrentMap;
            const string command = "bedroom-construction-cause-run-native";
            if (!TryResolveControlledBedroomConstructionCause(map,
                    out CASpaceProgram program, out Building_Bed existingBed,
                    out Pawn targetResident, out string failure,
                    allowPendingBedConstruction: true))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            List<Thing> constructions = BedConstructionInProgram(map,
                program);
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            if (constructions.Count != 1
                || (!(constructions[0] is Blueprint_Build)
                    && !(constructions[0] is Frame))
                || (constructions[0].def.entityDefToBuild as ThingDef)
                    != ThingDefOf.Bed
                || home?.PendingPlanMatchesForVerification(program.id,
                    targetResident.thingIDNumber, constructions[0]) != true
                || bridge?.BedroomCausePlanningScopeMatches(map, program.id,
                    targetResident.thingIDNumber,
                    home?.PendingOriginThingIdForVerification) != true)
                return "[CA] agent receipt: " + command
                    + "\nrefused; the exact targeted native Bed blueprint/frame or cause-bound planning lease is not active; "
                    + (bridge?.BedroomCausePlanningScopeEvidence ?? "none");
            if (bridge == null)
                return "[CA] agent receipt: " + command
                    + "\nrefused; the exact debug bridge material audit is unavailable";
            if (bridge.HasBedroomCauseMaterialAudit)
            {
                bridge.TryGetBedroomCauseMaterialAudit(program.id,
                    targetResident.thingIDNumber,
                    out string auditedConstruction,
                    out int auditedCount);
                return "[CA] agent receipt: " + command
                    + "\nrefused; native material observation is already armed for construction origin "
                    + (auditedConstruction ?? "other") + " with required count "
                    + auditedCount + "; no material was spawned and no job was forced";
            }
            if (!TrySelectNativeBedroomConstructionAvailabilityPawn(map,
                    constructions[0].Position, out Pawn availabilityPawn,
                    out failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            ThingDef stuff = constructions[0].Stuff
                ?? GenStuff.DefaultStuffFor(ThingDefOf.Bed);
            int required = ThingDefOf.Bed.CostListAdjusted(stuff,
                    errorOnNullStuff: false)
                .Where(cost => cost?.thingDef == ThingDefOf.WoodLog)
                .Sum(cost => cost.count);
            int woodAtAuditStart = CountMapThingsRecursivelyForAudit(map,
                ThingDefOf.WoodLog);
            Frame activeFrame = constructions[0] as Frame;
            int woodAlreadyInFrame = activeFrame?.resourceContainer
                .TotalStackCountOfDef(ThingDefOf.WoodLog) ?? 0;
            bool additionalResourceProbe = GenConstruct.CanGetResources_NewTemp(
                constructions[0], availabilityPawn, forced: false);
            bool aggregateMaterialPresent = required > 0
                && woodAtAuditStart >= required;
            if (!aggregateMaterialPresent)
                return "[CA] agent receipt: " + command
                    + "\nrefused; the active checkpoint does not preserve the full native Bed Wood vector across existing map resources and exact Frame contents; return to the retained supply phase rather than fabricating material; audit pawn "
                    + availabilityPawn.LabelShort + " #"
                    + availabilityPawn.thingIDNumber
                    + "; required " + required + ", recursive map Wood "
                    + woodAtAuditStart + ", already in exact Frame "
                    + woodAlreadyInFrame + ", raw native additional-resource probe "
                    + additionalResourceProbe
                    + "; no material was spawned and no job was forced";
            string originThingId = home.PendingOriginThingIdForVerification;
            if (originThingId.NullOrEmpty())
                return "[CA] agent receipt: " + command
                    + "\nrefused; the exact pending blueprint-to-frame origin is unavailable; no material was spawned and no job was forced";
            bridge.ArmBedroomCauseMaterialAudit(program.id,
                targetResident.thingIDNumber, originThingId, required,
                woodAtAuditStart);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            return "[CA] agent receipt: " + command
                + "\npass; native construction observation armed for "
                + constructions[0].ThingID + " at "
                + constructions[0].Position + " targeting "
                + targetResident.LabelShort + " #"
                + targetResident.thingIDNumber + "; blueprint-to-frame origin "
                + originThingId + "; native Bed Wood requirement "
                + required + " is covered by recursive map Wood "
                + woodAtAuditStart + ", including "
                + woodAlreadyInFrame + " already in the exact Frame; RimWorld "
                + "GenConstruct additional-resource probe is "
                + additionalResourceProbe + " for the audit pawn (diagnostic only; false can mean a full Frame or all remaining need already enroute); present aggregate capacity and the raw "
                + "additional-resource probe remain separate; exact Frame contents, the synchronous "
                + "Frame completion transaction, and the completing native job "
                + "will be observed; availability checked from "
                + availabilityPawn.LabelShort + " #"
                + availabilityPawn.thingIDNumber
                + " without assigning that pawn; existing Bed "
                + existingBed.ThingID + " and roster preserved; time set Normal; "
                + "cause-bound planning scope "
                + bridge.BedroomCausePlanningScopeEvidence + "; "
                + "the audited Frame completion will defer the home planner, "
                + "retire the exact map/cause lease without changing the global "
                + "Home setting, and pause before "
                + "a later objective can replace the receipt window; no material "
                + "spawned, no job forced, and no save written";
        }

        internal static string CompletedBedroomConstructionCauseReceipt()
        {
            const string command = "bedroom-construction-cause-completion";
            if (!TryResolveControlledBedroomConstructionCompletion(
                    Find.CurrentMap, out CASpaceProgram program,
                    out Building_Bed lucBed, out Building_Bed dollyBed,
                    out Pawn luc, out Pawn dolly, out string evidence,
                    out string failure))
                return "[CA] agent receipt: " + command + "\nfail; "
                    + failure;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Room room = dollyBed.GetRoom();
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            return "[CA] agent receipt: " + command
                + "\npass; native Bed " + dollyBed.ThingID + " at "
                + dollyBed.Position + " completed for concrete resident "
                + dolly.LabelShort + " #" + dolly.thingIDNumber
                + " through exact CA record; existing Bed " + lucBed.ThingID
                + " remains owned by " + luc.LabelShort + " #"
                + luc.thingIDNumber
                + "\nprogram #" + program.id
                + " roster preserved 2/2; distinct native ownership 2/2; room "
                + room.Role.defName + "; no Bed blueprint/frame and no active "
                + "map-wide CA construction commitment; exact native Frame "
                + "inventory immediately before completion ["
                + (bridge?.BedroomCauseConsumedMaterialEvidence ?? "none")
                + "] totaled the native Bed cost; recursive Wood across the "
                + "synchronous Frame.CompleteConstruction transaction "
                + bridge?.BedroomCauseWoodCountBeforeFrameCompletion + " -> "
                + bridge?.BedroomCauseWoodCountAfterFrameCompletion
                + " with delta " + bridge?.BedroomCauseFrameWoodDelta
                + "; audit-start recursive Wood "
                + bridge?.BedroomCauseWoodCountAtAuditStart
                + "; no source-stack identity was prescribed"
                + "\ncompleting native job: "
                + (bridge?.BedroomCauseCompletionJobEvidence ?? "none")
                + "; native resource-job starts observed as supplemental evidence "
                + (bridge?.BedroomCauseNativeResourceJobCount ?? 0)
                + "; forced resource job observed "
                + (bridge?.BedroomCauseObservedForcedResourceJob == true)
                + "; "
                + (bridge?.BedroomCauseNativeResourceJobEvidence ?? "none")
                + "\nhome-planning scope: "
                + (bridge?.BedroomCausePlanningScopeEvidence ?? "none")
                + "\nplacement proof preserved: " + evidence
                + "\noptional facilities and Beauty supplied no cause; "
                + "time paused; no save written";
        }

        internal static string SaveCompletedBedroomConstructionCauseReceipt()
        {
            const string command =
                "bedroom-construction-cause-save-completed";
            if (!TryResolveControlledBedroomConstructionCompletion(
                    Find.CurrentMap, out CASpaceProgram program,
                    out Building_Bed lucBed, out Building_Bed dollyBed,
                    out Pawn luc, out Pawn dolly, out string evidence,
                    out string failure))
                return "[CA] agent receipt: " + command + "\nrefused; "
                    + failure;
            string path = GenFilePaths.FilePathForSavedGame(
                BedroomConstructionCauseCompletedSaveName);
            if (File.Exists(path))
            {
                var existing = new FileInfo(path);
                return "[CA] agent receipt: " + command
                    + "\nrefused; the specific completed checkpoint already "
                    + "exists and was not overwritten: " + path + "; verified "
                    + existing.Length + " bytes, SHA256 " + FileSha256(path);
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            GameDataSaveLoader.SaveGame(
                BedroomConstructionCauseCompletedSaveName);
            if (!File.Exists(path) || new FileInfo(path).Length <= 0)
                return "[CA] agent receipt: " + command
                    + "\nrefused; native SaveGame produced no checkpoint at "
                    + path;
            var saved = new FileInfo(path);
            return "[CA] agent receipt: " + command
                + "\nnative save verified of completed targeted Bed "
                + dollyBed.ThingID + " for " + dolly.LabelShort + " #"
                + dolly.thingIDNumber + " in Bedroom program #" + program.id
                + " to specific completed checkpoint " + path + "; "
                + saved.Length + " bytes, SHA256 " + FileSha256(path)
                + "; functional and active checkpoints remain separate and "
                + "were not overwritten";
        }

        private static bool TryResolveControlledBedroomConstructionCompletion(
            Map map, out CASpaceProgram program, out Building_Bed lucBed,
            out Building_Bed dollyBed, out Pawn luc, out Pawn dolly,
            out string evidence, out string failure)
        {
            program = null;
            lucBed = null;
            dollyBed = null;
            luc = null;
            dolly = null;
            evidence = null;
            failure = "no loaded map";
            if (map == null) return false;
            var expected = new HashSet<IntVec3>();
            for (int x = 25; x <= 31; x++)
                for (int z = 179; z <= 185; z++)
                    expected.Add(new IntVec3(x, 0, z));
            Room room = new IntVec3(28, 0, 182).GetRoom(map);
            program = PlannedUseMapComponent.For(map)?
                .ProgramsForObservation.FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.author == CASpaceAuthor.Player
                    && candidate.purpose == CASpacePurpose.Bedroom
                    && candidate.RequiresSleep && candidate.maxOccupants == 2
                    && candidate.residentAuthor
                        == CAResidentRosterAuthor.Player
                    && candidate.cells != null
                    && candidate.cells.ToHashSet().SetEquals(expected));
            luc = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactLucIdentity);
            dolly = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactDollyIdentity);
            List<Building_Bed> beds = map.listerBuildings
                .allBuildingsColonist.OfType<Building_Bed>()
                .Where(candidate => candidate != null && candidate.Spawned
                    && candidate.def == ThingDefOf.Bed
                    && candidate.Faction == Faction.OfPlayer
                    && !candidate.Medical && candidate.ForColonists
                    && candidate.OccupiedRect().Cells.All(expected.Contains))
                .OrderBy(candidate => candidate.thingIDNumber).ToList();
            Pawn resolvedLuc = luc;
            Pawn resolvedDolly = dolly;
            lucBed = beds.FirstOrDefault(candidate => resolvedLuc != null
                && candidate.OwnersForReading.Contains(resolvedLuc));
            dollyBed = beds.FirstOrDefault(candidate => resolvedDolly != null
                && candidate.OwnersForReading.Contains(resolvedDolly));
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            string auditedConstructionId = null;
            int auditedMaterialCount = 0;
            bool auditTracked = bridge != null
                && bridge.TryGetBedroomCauseMaterialAudit(program?.id ?? 0,
                    dolly?.thingIDNumber ?? 0, out auditedConstructionId,
                    out auditedMaterialCount);
            string completedEvidence = null;
            string completedOriginId = null;
            string consumingFrameId = null;
            string consumedMaterialId = null;
            string consumedMaterialEvidence = null;
            int consumedMaterialCount = 0;
            bool exactRecord = home != null
                && home.TryGetCompletedPlanEvidenceForVerification(program?.id
                        ?? 0, dolly?.thingIDNumber ?? 0, dollyBed,
                    out completedEvidence, out completedOriginId,
                    out consumingFrameId, out consumedMaterialId,
                    out consumedMaterialEvidence,
                    out consumedMaterialCount);
            bool exactConsumption = auditTracked && exactRecord
                && completedOriginId == auditedConstructionId
                && !consumedMaterialEvidence.NullOrEmpty()
                && consumedMaterialCount == auditedMaterialCount
                && !consumingFrameId.NullOrEmpty()
                && bridge.BedroomCauseMaterialConsumptionMatches(program.id,
                    dolly.thingIDNumber, completedOriginId,
                    consumingFrameId, consumedMaterialEvidence,
                    consumedMaterialCount);
            int woodBeforeFrame = bridge?
                .BedroomCauseWoodCountBeforeFrameCompletion ?? -1;
            int woodAfterFrame = bridge?
                .BedroomCauseWoodCountAfterFrameCompletion ?? -1;
            bool exactFrameTransaction = woodBeforeFrame >= 0
                && woodAfterFrame >= 0
                && woodBeforeFrame - woodAfterFrame == consumedMaterialCount;
            bool planningScopeReconciled = bridge?
                .CompletedBedroomCausePlanningScopeMatches(map,
                    program?.id ?? 0, dolly?.thingIDNumber ?? 0,
                    completedOriginId) == true;
            evidence = completedEvidence;
            bool placementPreserved = !evidence.NullOrEmpty()
                && evidence.Contains("protected existing bed approaches")
                && evidence.Contains("protected doorway approaches")
                && evidence.Contains("clear negative space")
                && evidence.Contains("functional approach minimum");
            bool exactCompletion = program != null && luc != null
                && dolly != null && room != null && room.ProperRoom
                && room.Cells.ToHashSet().SetEquals(expected)
                && beds.Count == 2 && lucBed != null && dollyBed != null
                && lucBed != dollyBed
                && luc.ownership?.OwnedBed == lucBed
                && dolly.ownership?.OwnedBed == dollyBed
                && lucBed.OwnersForReading.Count == 1
                && dollyBed.OwnersForReading.Count == 1
                && program.residents != null && program.residents.Count == 2
                && program.residents.Contains(luc)
                && program.residents.Contains(dolly)
                && room.Role == RoomRoleDefOf.Bedroom
                && RoomRoleWorker_Bedroom.IsBedroom(beds)
                && BedConstructionInProgram(map, program).Count == 0
                && home?.HasPendingPlanForObservation == false
                && CAHomePrerequisiteMapComponent.For(map)?.HasDemand != true
                && CASpatialInitiativeMapComponent.For(map)?
                    .HasPendingPlanForObservation != true
                && exactConsumption && exactFrameTransaction
                && planningScopeReconciled
                && exactRecord
                && placementPreserved;
            if (!exactCompletion)
            {
                failure = "the exact two-Bed native completion, Luc/Dolly "
                    + "ownership, saved roster, Bedroom role, CA target record, "
                    + "clear construction state, or functional placement "
                    + "evidence is not yet present; observed beds " + beds.Count
                    + ", Luc bed " + (lucBed?.ThingID ?? "none")
                    + ", Dolly bed " + (dollyBed?.ThingID ?? "none")
                    + ", role " + (room?.Role?.defName ?? "none")
                    + ", pending "
                    + (home?.HasPendingPlanForObservation == true)
                    + ", retained material demand "
                    + (CAHomePrerequisiteMapComponent.For(map)?.HasDemand
                        == true)
                    + ", spatial commitment "
                    + (CASpatialInitiativeMapComponent.For(map)?
                        .HasPendingPlanForObservation == true)
                    + ", material audit tracked " + auditTracked
                    + " construction "
                    + (auditedConstructionId ?? "none") + " required count "
                    + auditedMaterialCount + " audit-start recursive Wood "
                    + (bridge?.BedroomCauseWoodCountAtAuditStart ?? -1)
                    + ", record origin "
                    + (completedOriginId ?? "none") + " consuming frame "
                    + (consumingFrameId ?? "none") + " consumed material ["
                    + (consumedMaterialEvidence ?? "none") + "] count "
                    + consumedMaterialCount
                    + ", exact frame consumption " + exactConsumption
                    + ", immediate recursive Wood " + woodBeforeFrame + " -> "
                    + woodAfterFrame + " transaction " + exactFrameTransaction
                    + ", completion job "
                    + (bridge?.BedroomCauseCompletionJobEvidence ?? "none")
                    + ", native resource-job starts (supplemental) "
                    + (bridge?.BedroomCauseNativeResourceJobCount ?? 0)
                    + ", forced resource job observed "
                    + (bridge?.BedroomCauseObservedForcedResourceJob == true)
                    + ", resource-job evidence "
                    + (bridge?.BedroomCauseNativeResourceJobEvidence ?? "none")
                    + ", home-planning scope "
                    + (bridge?.BedroomCausePlanningScopeEvidence ?? "none")
                    + ", scope reconciled " + planningScopeReconciled
                    + ", evidence " + (evidence ?? "none");
                return false;
            }
            failure = null;
            return true;
        }

        internal static int CountMapThingsRecursivelyForAudit(Map map,
            ThingDef def)
        {
            if (map == null || def == null) return -1;
            var things = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(map,
                ThingRequest.ForDef(def), things, allowUnreal: true,
                passCheck: null, alsoGetSpawnedThings: true);
            return things.Where(thing => thing != null && !thing.Destroyed)
                .Sum(thing => thing.stackCount);
        }

        private static List<Thing> BedConstructionInProgram(Map map,
            CASpaceProgram program)
        {
            if (map == null || program == null) return new List<Thing>();
            var authority = new HashSet<IntVec3>(program.cells);
            return map.listerThings.AllThings.Where(thing => thing != null
                    && thing.Spawned
                    && (thing is Blueprint_Build || thing is Frame)
                    && (thing.def.entityDefToBuild as ThingDef)?.IsBed == true
                    && GenAdj.OccupiedRect(thing.Position, thing.Rotation,
                        (thing.def.entityDefToBuild as ThingDef).Size).Cells
                        .All(authority.Contains))
                .OrderBy(thing => thing.thingIDNumber).ToList();
        }

        private static bool TryPrepareBedroomCauseBuilder(Map map,
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
                : AutonomyComponent.LevelOf(author);
            woke = author != null && !author.Awake();
            if (author == null)
            {
                failure = "the controlled fixture has no wakeable, "
                    + "threat-unaware construction author";
                return false;
            }
            if (woke)
                author.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (!author.Awake())
            {
                failure = "the selected construction author could not be "
                    + "awakened for the bounded regression";
                return false;
            }
            AutonomyComponent.SetLevel(author, 3);
            failure = null;
            return true;
        }

        private static bool TrySelectNativeBedroomConstructionAvailabilityPawn(
            Map map, IntVec3 constructionCell, out Pawn availabilityPawn,
            out string failure)
        {
            List<Pawn> candidates = map?.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn != null && !pawn.Downed && !pawn.Drafted
                    && !pawn.InMentalState
                    && pawn.health?.capacities?.CanBeAwake == true
                    && pawn.CurJob?.playerForced != true
                    && pawn.workSettings != null
                    && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    && pawn.workSettings.WorkIsActive(
                        WorkTypeDefOf.Construction)
                    && KnowledgeMapComponent.For(map)?.KnowsAnyThreat(pawn)
                        != true
                    && pawn.CanReach(constructionCell, PathEndMode.Touch,
                        Danger.Some))
                .OrderByDescending(pawn => pawn.Awake())
                .ThenByDescending(pawn => pawn.skills?
                    .GetSkill(SkillDefOf.Construction)?.Level ?? 0)
                .ThenBy(pawn => pawn.thingIDNumber).ToList()
                ?? new List<Pawn>();
            availabilityPawn = candidates.FirstOrDefault();
            if (availabilityPawn == null)
            {
                failure = "no non-forced, threat-unaware constructor can "
                    + "reach the exact targeted Bed blueprint/frame; no pawn was "
                    + "assigned or awakened and no material was spawned";
                return false;
            }
            failure = null;
            return true;
        }

        private static bool TryResolveControlledBedroomConstructionCause(
            Map map, out CASpaceProgram program, out Building_Bed bed,
            out Pawn targetResident, out string failure,
            bool allowPendingBedConstruction = false)
        {
            program = null;
            bed = null;
            targetResident = null;
            failure = "no loaded map";
            if (map == null) return false;
            var expected = new HashSet<IntVec3>();
            for (int x = 25; x <= 31; x++)
                for (int z = 179; z <= 185; z++)
                    expected.Add(new IntVec3(x, 0, z));
            Room room = new IntVec3(28, 0, 182).GetRoom(map);
            program = PlannedUseMapComponent.For(map)?
                .ProgramsForObservation.FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.author == CASpaceAuthor.Player
                    && candidate.purpose == CASpacePurpose.Bedroom
                    && candidate.RequiresSleep
                    && candidate.maxOccupants == 2
                    && candidate.residentAuthor
                        == CAResidentRosterAuthor.Player
                    && candidate.cells != null
                    && candidate.cells.ToHashSet().SetEquals(expected));
            Pawn luc = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactLucIdentity);
            Pawn dolly = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(
                CAPrepareCarefullyFixtureBridge.IsExactDollyIdentity);
            List<Building_Bed> controlledBeds = map.listerBuildings
                .allBuildingsColonist.OfType<Building_Bed>().Where(candidate =>
                    candidate != null && candidate.Spawned
                    && candidate.def == ThingDefOf.Bed
                    && candidate.Faction == Faction.OfPlayer
                    && !candidate.Medical && candidate.ForColonists
                    && candidate.OccupiedRect().Cells.All(expected.Contains))
                .ToList();
            bed = controlledBeds.Count == 1 ? controlledBeds[0] : null;
            List<Building_Bed> nativeBeds = room?.ContainedAndAdjacentThings
                .OfType<Building_Bed>().Where(candidate => candidate != null
                    && candidate.Spawned
                    && candidate.def.building?.bed_humanlike == true
                    && candidate.def.building
                        .bed_countsForBedroomOrBarracks).ToList()
                ?? new List<Building_Bed>();
            bool pendingBedConstruction = map.listerThings.AllThings.Any(thing =>
                thing != null && thing.Spawned
                && (thing is Blueprint_Build || thing is Frame)
                && (thing.def.entityDefToBuild as ThingDef)?.IsBed == true
                && GenAdj.OccupiedRect(thing.Position, thing.Rotation,
                    (thing.def.entityDefToBuild as ThingDef).Size).Cells
                    .All(expected.Contains));
            if (program == null || room == null || !room.ProperRoom
                || !room.Cells.ToHashSet().SetEquals(expected)
                || room.Role != RoomRoleDefOf.Bedroom
                || !RoomRoleWorker_Bedroom.IsBedroom(nativeBeds)
                || program.residents == null || program.residents.Count != 2
                || luc == null || dolly == null
                || !program.residents.Contains(luc)
                || !program.residents.Contains(dolly)
                || luc.GetFirstSpouse() != dolly
                || dolly.GetFirstSpouse() != luc || bed == null
                || bed.SleepingSlotsCount != 1
                || bed.OwnersForReading.Count != 1
                || !bed.OwnersForReading.Contains(luc)
                || luc.ownership?.OwnedBed != bed
                || (dolly.ownership?.OwnedBed != null
                    && dolly.ownership.OwnedBed.OccupiedRect().Cells
                        .All(expected.Contains))
                || (!allowPendingBedConstruction
                    && pendingBedConstruction))
            {
                failure = "the exact one-slot controlled Bedroom, Luc "
                    + "ownership, Dolly deficit, native Bedroom role, or clean "
                    + "construction state is absent";
                return false;
            }
            targetResident = dolly;
            failure = null;
            return true;
        }

        private static bool RestoreControlledFunctionalBedroom(Map map,
            CASpaceProgram program, Pawn luc, Pawn dolly,
            Building_Bed replacement, ThingDef sharedDef,
            ThingDef sharedStuff, IntVec3 position, Rot4 rotation,
            QualityCategory quality, out string detail)
        {
            try
            {
                if (replacement != null && replacement.Spawned)
                    replacement.Destroy(DestroyMode.Vanish);
                luc?.ownership?.UnclaimBed();
                dolly?.ownership?.UnclaimBed();
                Building_Bed restored = ThingMaker.MakeThing(sharedDef,
                    sharedStuff) as Building_Bed;
                if (restored == null)
                {
                    detail = "shared definition did not recreate Building_Bed";
                    return false;
                }
                restored.SetFactionDirect(Faction.OfPlayer);
                restored.TryGetComp<CompQuality>()?.SetQuality(quality,
                    ArtGenerationContext.Colony);
                GenSpawn.Spawn(restored, position, map, rotation,
                    WipeMode.Vanish);
                Pawn first = null;
                Pawn second = null;
                PlannedUseMapComponent programs =
                    PlannedUseMapComponent.For(map);
                bool assigned = programs != null
                    && programs.TryAssignSharedBedroomBed(program, restored,
                        out first, out second);
                map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
                bool valid = assigned && first != null && second != null
                    && restored.OwnersForReading.Contains(luc)
                    && restored.OwnersForReading.Contains(dolly)
                    && restored.GetRoom()?.Role == RoomRoleDefOf.Bedroom;
                detail = "shared bed respawned " + restored.ThingID
                    + ", native spouse ownership restored " + assigned
                    + ", Bedroom role restored "
                    + (restored.GetRoom()?.Role == RoomRoleDefOf.Bedroom);
                return valid;
            }
            catch (Exception exception)
            {
                detail = exception.Message;
                return false;
            }
        }

        internal static string PrepareDevelopedBedroomRequirementReceipt()
        {
            Map map = Find.CurrentMap;
            const string command = "bedroom-requirement-prepare-developed";
            if (map == null)
                return "[CA] agent receipt: " + command
                    + "\nrefused; no loaded map";
            var anchor = new IntVec3(66, 0, 107);
            Room room = anchor.InBounds(map) ? anchor.GetRoom(map) : null;
            if (room == null || !room.ProperRoom
                || room.PsychologicallyOutdoors || room.CellCount != 174
                || room.Role != RoomRoleDefOf.Barracks)
                return "[CA] agent receipt: " + command
                    + "\nrefused; exact developed 174-cell mixed Barracks "
                    + "fixture is unavailable at " + anchor;

            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            List<IntVec3> cells = room.Cells.OrderBy(cell => cell.z)
                .ThenBy(cell => cell.x).ToList();
            if (component == null || cells.Any(cell =>
                    component.ProgramAt(cell) != null))
                return "[CA] agent receipt: " + command
                    + "\nrefused; the exact developed room already intersects "
                    + "an authored program; no authority was replaced";

            Building_Bed shared = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>().FirstOrDefault(candidate =>
                    candidate != null && candidate.Spawned
                    && candidate.def.defName == "DoubleBed"
                    && candidate.Position == new IntVec3(72, 0, 118)
                    && candidate.GetRoom() == room);
            if (shared == null)
                return "[CA] agent receipt: " + command
                    + "\nrefused; the developed room's exact existing "
                    + "DoubleBed is absent";
            List<int> priorOwnerIds = shared.OwnersForReading
                .Where(owner => owner != null).Select(owner =>
                    owner.thingIDNumber).OrderBy(id => id).ToList();

            var draft = new CASpaceProgramDraft
            {
                label = "Developed mixed-room Bedroom comparison",
                purpose = CASpacePurpose.Bedroom,
                style = CASpaceStyle.Adaptive,
                maxOccupants = 2,
                requiresSleep = true
            };
            CASpaceProgram program = component.CreateProgram(draft, cells);
            List<int> currentOwnerIds = shared.OwnersForReading
                .Where(owner => owner != null).Select(owner =>
                    owner.thingIDNumber).OrderBy(id => id).ToList();
            if (room.Role != RoomRoleDefOf.Barracks
                || !priorOwnerIds.SequenceEqual(currentOwnerIds))
            {
                component.ClearCells(program.cells.ToList());
                return "[CA] agent receipt: " + command
                    + "\nrefused; the transient authority declaration changed "
                    + "native room role or bed ownership; program cells were "
                    + "cleared";
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            return "[CA] agent receipt: " + command + "\ncreated exact "
                + "transient player-author Bedroom program #" + program.id
                + " over the pre-existing 174-cell developed room while "
                + "native DoubleBed #" + shared.thingIDNumber + " retained "
                + priorOwnerIds.Count + " existing owner reference(s). The "
                + "program roster remains explicitly empty, so the evaluator "
                + "must not infer residents from bed ownership. Native role "
                + "remains "
                + room.Role.defName + " because unrelated beds and uses remain; "
                + "that conflict is test evidence, not a room-normalization "
                + "request. No save, construction, furnishing, or inventory "
                + "change occurred; reload discards this declaration.";
        }

        [DebugAction("Colonist Awareness", "Toxic-waste lifecycle receipt",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToxicWasteLifecycleCensus()
        {
            ToxicWasteLifecycleReceipt();
        }

        internal static string ToxicWasteLifecycleReceipt()
        {
            CAToxicWasteLifecycleMapComponent component =
                CAToxicWasteLifecycleMapComponent.For(Find.CurrentMap);
            return component != null
                ? component.RefreshAndReceipt()
                : "[CA] toxic-waste lifecycle objective: no map component";
        }

        internal static string IsolateToxicWasteRelocationFixtureReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] agent receipt: toxic-waste-relocation-isolate-hostile\n"
                    + "refused; no loaded map";

            List<Pawn> observedHostiles = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && !pawn.Destroyed
                    && pawn.Faction != null
                    && pawn.Faction.HostileTo(Faction.OfPlayer))
                .OrderBy(pawn => pawn.ThingID, StringComparer.Ordinal)
                .ToList();
            List<Pawn> activeHostiles = observedHostiles
                .Where(pawn => !pawn.IsPrisonerOfColony && !pawn.Downed
                    && pawn.mindState?.duty?.def != null)
                .ToList();
            string observed = observedHostiles.Count == 0 ? "none" : string.Join(
                "; ", observedHostiles.Select(pawn => pawn.ThingID + " "
                    + pawn.LabelShortCap + " faction "
                    + pawn.Faction?.Name + " at " + pawn.Position
                    + ", prisoner " + (pawn.IsPrisonerOfColony ? "yes" : "no")
                    + ", downed " + (pawn.Downed ? "yes" : "no")
                    + " duty "
                    + (pawn.mindState?.duty?.def?.defName ?? "none")));
            if (activeHostiles.Count != 1)
                return "[CA] agent receipt: toxic-waste-relocation-isolate-hostile\n"
                    + "refused; exact isolation requires one and only one "
                    + "spawned, non-prisoner, standing player-hostile pawn with "
                    + "an active lord duty; observed " + observedHostiles.Count
                    + " hostile-faction pawn(s), " + activeHostiles.Count
                    + " eligible active combatant(s): " + observed;

            if (Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Pawn hostile = activeHostiles[0];
            hostile.Destroy(DestroyMode.Vanish);
            List<Pawn> remainingHostiles = map.mapPawns.AllPawnsSpawned.Where(pawn =>
                pawn != null && !pawn.Destroyed && pawn.Faction != null
                && pawn.Faction.HostileTo(Faction.OfPlayer)).ToList();
            int remainingActive = remainingHostiles.Count(pawn =>
                !pawn.IsPrisonerOfColony && !pawn.Downed
                && pawn.mindState?.duty?.def != null);
            bool removed = hostile.Destroyed && remainingActive == 0;
            string receipt = "[CA] agent receipt: "
                + "toxic-waste-relocation-isolate-hostile\n"
                + (removed ? "pass; " : "fail; ")
                + "hash-bound verification isolation selected exactly one "
                + "eligible active player-hostile pawn and vanished it through "
                + "Pawn.Destroy: "
                + hostile.ThingID + " " + hostile.LabelShortCap
                + "; remaining eligible active combatants " + remainingActive
                + "; preserved hostile-faction prisoners/noncombatants "
                + remainingHostiles.Count + "; game paused. This receipt does "
                + "not infer how the removed hostile entered the source save. "
                + "Pre-isolation observation: " + observed;
            Log.Message(receipt);
            return receipt;
        }

        internal static string SaveToxicWasteRelocationWorkingReceipt()
        {
            if (Find.CurrentMap == null)
                return "[CA] agent receipt: toxic-waste-relocation-save-working\n"
                    + "refused; no loaded map";
            string savePath = GenFilePaths.FilePathForSavedGame(
                ToxicWasteRelocationWorkingSaveName);
            bool overwrote = File.Exists(savePath);
            int tick = Find.TickManager?.TicksGame ?? -1;
            GameDataSaveLoader.SaveGame(ToxicWasteRelocationWorkingSaveName);
            bool exists = File.Exists(savePath);
            string receipt = "[CA] agent receipt: "
                + "toxic-waste-relocation-save-working\n"
                + (exists ? "pass; " : "fail; ")
                + (overwrote ? "overwrote" : "created")
                + " the continuing working test save at tick " + tick
                + ": " + savePath;
            Log.Message(receipt);
            return receipt;
        }

        internal static string LoadToxicWasteRelocationWorkingReceipt()
        {
            string savePath = GenFilePaths.FilePathForSavedGame(
                ToxicWasteRelocationWorkingSaveName);
            if (!File.Exists(savePath))
                return "[CA] agent receipt: toxic-waste-relocation-load-working\n"
                    + "refused; working test save is absent: " + savePath;
            GameDataSaveLoader.LoadGame(ToxicWasteRelocationWorkingSaveName);
            return "[CA] agent receipt: toxic-waste-relocation-load-working\n"
                + "queued native reload of continuing working test save "
                + savePath;
        }

        [DebugAction("Colonist Awareness",
            "Legacy fixture: authorize one toxic-waste relocation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StartToxicWasteRelocationFixture()
        {
            StartToxicWasteRelocationFixtureReceipt();
        }

        internal static string StartToxicWasteRelocationFixtureReceipt()
        {
            Map map = Find.CurrentMap;
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(map);
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            ThingDef wastepackDef = DefDatabase<ThingDef>
                .GetNamedSilentFail("Wastepack");
            if (map == null || lifecycle == null || programs == null
                || wastepackDef == null)
                return "[CA] agent receipt: toxic-waste-relocation-fixture-start\n"
                    + "refused; loaded map, lifecycle, player programs, or "
                    + "Wastepack Def unavailable";

            lifecycle.RefreshAndReceipt();
            if (lifecycle.HasRelocationAuthorization)
                return "[CA] agent receipt: toxic-waste-relocation-fixture-start\n"
                    + "refused; this objective already has an authorization "
                    + "record, so the fixture will not create another source\n"
                    + lifecycle.Census();

            Pawn hauler;
            IntVec3 sourceCell;
            if (!TryFindToxicWasteRelocationFixtureCell(map, wastepackDef,
                programs.ProgramsForObservation, out hauler, out sourceCell))
                return "[CA] agent receipt: toxic-waste-relocation-fixture-start\n"
                    + "refused; no deterministic empty non-storage source cell "
                    + "was reachable by an eligible native Hauling pawn";

            Thing wastepack = ThingMaker.MakeThing(wastepackDef);
            wastepack.stackCount = 1;
            Thing spawned = GenSpawn.Spawn(wastepack, sourceCell, map,
                WipeMode.Vanish);
            spawned.SetForbidden(false, warnOnFail: false);
            lifecycle.RefreshAndReceipt();
            CASpaceProgram destination = programs.ProgramsForObservation
                .FirstOrDefault(program => lifecycle
                    .RelocationActionAvailableFor(program));
            if (destination == null)
            {
                if (!spawned.Destroyed) spawned.Destroy(DestroyMode.Vanish);
                lifecycle.RefreshAndReceipt();
                return "[CA] agent receipt: toxic-waste-relocation-fixture-start\n"
                    + "refused; the exact spawned source produced no present-ready "
                    + "player-authored destination; the tracked fixture source was "
                    + "removed\n" + lifecycle.Census();
            }

            const string origin =
                CAToxicWasteLifecycleMapComponent
                    .VerificationFixtureAuthorizationOrigin;
            string authorization = lifecycle.ToggleRelocationAuthorization(
                destination, origin);
            bool active = lifecycle.RelocationAuthorizationActiveFor(destination)
                && lifecycle.CurrentAuthorizationIsVerificationFixture;
            if (active && Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            string receipt = "[CA] agent receipt: "
                + "toxic-waste-relocation-fixture-start\n"
                + (active ? "pass; " : "fail; ") + "spawned exact source "
                + spawned.ThingID + " (1 unit) at " + sourceCell
                + "; reachable hauler " + hauler.ThingID
                + "; destination player program " + destination.label + " #"
                + destination.id + "; authorization origin " + origin
                + "; native speed "
                + (Find.TickManager?.CurTimeSpeed.ToString() ?? "unavailable")
                + "\n" + authorization + "\n"
                + lifecycle.RefreshAndReceipt();
            Log.Message(receipt);
            return receipt;
        }

        [DebugAction("Colonist Awareness",
            "Fixture: observe autonomous Wastepack arrival response",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StartToxicWasteArrivalFixture()
        {
            StartToxicWasteArrivalFixtureReceipt();
        }

        internal static string ToxicWasteArrivalReceipt()
        {
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(Find.CurrentMap);
            if (lifecycle == null)
                return "[CA] agent receipt: toxic-waste-arrival-receipt\n"
                    + "refused; no loaded map lifecycle";
            string census = lifecycle.RefreshAndReceipt();
            string result = lifecycle.HasCompletedVerificationNativeWasteResponse
                ? "pass; the fixed arrival fixture completed through an "
                    + "autonomous CA WorkGiver and RimWorld's native haul driver "
                    + "into an existing destination"
                : (lifecycle.HasActiveNativeWasteResponse
                    ? "pending; an autonomous native response is active"
                    : (lifecycle.HasPendingNativeWasteResponse
                        ? "pending; one real allowed arrival awaits autonomous work"
                        : "observed; no fixed arrival response is pending, active, "
                            + "or proven complete"));
            string receipt = "[CA] agent receipt: "
                + "toxic-waste-arrival-receipt\n" + result + "\n" + census;
            Log.Message(receipt);
            return receipt;
        }

        internal static string StartToxicWasteArrivalFixtureReceipt()
        {
            Map map = Find.CurrentMap;
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(map);
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            ThingDef wastepackDef = DefDatabase<ThingDef>
                .GetNamedSilentFail("Wastepack");
            if (map == null || lifecycle == null || programs == null
                || wastepackDef == null)
                return "[CA] agent receipt: toxic-waste-arrival-fixture-start\n"
                    + "refused; loaded map, lifecycle, player programs, or "
                    + "Wastepack Def unavailable";

            lifecycle.RefreshAndReceipt();
            if (lifecycle.HasPendingNativeWasteResponse
                || lifecycle.HasActiveNativeWasteResponse)
                return "[CA] agent receipt: toxic-waste-arrival-fixture-start\n"
                    + "refused; an earlier native arrival response is still "
                    + "pending or active\n" + lifecycle.Census();
            int destinationProgramId =
                lifecycle.ContainmentDestinationProgramId;
            CASpaceProgram destination = programs.FindProgram(
                destinationProgramId);
            if (destination == null)
                return "[CA] agent receipt: toxic-waste-arrival-fixture-start\n"
                    + "refused; no existing player-authored roofed containment "
                    + "destination is available before source placement\n"
                    + lifecycle.Census();

            Pawn reachableHauler;
            IntVec3 sourceCell;
            if (!TryFindToxicWasteRelocationFixtureCell(map, wastepackDef,
                programs.ProgramsForObservation,
                out reachableHauler, out sourceCell))
                return "[CA] agent receipt: toxic-waste-arrival-fixture-start\n"
                    + "refused; no empty non-storage source cell was allowed and "
                    + "reachable "
                    + "for an eligible Hauling pawn";

            Thing wastepack = ThingMaker.MakeThing(wastepackDef);
            wastepack.stackCount = 1;
            Thing spawned = GenSpawn.Spawn(wastepack, sourceCell, map,
                WipeMode.Vanish);
            spawned.SetForbidden(false, warnOnFail: false);
            lifecycle.RefreshAndReceipt();
            int freeCapacity =
                lifecycle.ContainmentDestinationFreeCapacityUnits;
            destinationProgramId = lifecycle
                .ContainmentDestinationProgramId;
            destination = programs.FindProgram(destinationProgramId);
            string registration = lifecycle.RegisterNativeWasteArrival(spawned,
                CAToxicWasteLifecycleMapComponent
                    .VerificationFixtureArrivalOrigin, reachableHauler);
            bool registered = destination != null
                && (lifecycle.HasPendingNativeWasteResponse
                    || lifecycle.HasActiveNativeWasteResponse
                    || lifecycle.HasCompletedVerificationNativeWasteResponse);
            if (!registered)
            {
                if (!spawned.Destroyed) spawned.Destroy(DestroyMode.Vanish);
                lifecycle.RefreshAndReceipt();
                string refusal = "[CA] agent receipt: "
                    + "toxic-waste-arrival-fixture-start\n"
                    + "fail; the exact spawned source was removed because no "
                    + "existing roofed containment destination could receive the "
                    + "bounded proof\n" + registration + "\n"
                    + lifecycle.Census();
                Log.Warning(refusal);
                return refusal;
            }

            if (Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            string receipt = "[CA] agent receipt: "
                + "toxic-waste-arrival-fixture-start\npass; placed one real, "
                + "allowed Wastepack " + spawned.ThingID + " at " + sourceCell
                + " outside all authored programs and native storage; eligible "
                + "reachable, allowed-area hauler witness "
                + reachableHauler.ThingID
                + "; existing destination " + destination.label + " #"
                + destination.id + " exposes " + freeCapacity
                + " free unit(s). The fixed fixture emulates an allowed delivery "
                + "and claims no quest provenance. It created no facility, "
                + "program, storage filter, priority, job, or second "
                + "authorization. The CA response WorkGiver may prioritize an "
                + "existing valid storage cell; RimWorld's native haul driver owns "
                + "reservations, carrying, and placement. "
                + "Native speed "
                + (Find.TickManager?.CurTimeSpeed.ToString() ?? "unavailable")
                + ".\n" + registration + "\n" + lifecycle.Census();
            Log.Message(receipt);
            return receipt;
        }

        internal static string SaveToxicWasteArrivalWorkingReceipt()
        {
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(Find.CurrentMap);
            if (lifecycle == null)
                return "[CA] agent receipt: toxic-waste-arrival-save-working\n"
                    + "refused; no loaded map lifecycle";
            string census = lifecycle.RefreshAndReceipt();
            if (!lifecycle.HasCompletedVerificationNativeWasteResponse)
                return "[CA] agent receipt: toxic-waste-arrival-save-working\n"
                    + "refused; the exact fixed arrival has not completed through "
                    + "the autonomous response WorkGiver and native haul driver\n"
                    + census;

            string savePath = GenFilePaths.FilePathForSavedGame(
                ToxicWasteRelocationWorkingSaveName);
            bool overwrote = File.Exists(savePath);
            int tick = Find.TickManager?.TicksGame ?? -1;
            GameDataSaveLoader.SaveGame(ToxicWasteRelocationWorkingSaveName);
            bool exists = File.Exists(savePath);
            string receipt = "[CA] agent receipt: "
                + "toxic-waste-arrival-save-working\n"
                + (exists ? "pass; " : "fail; ")
                + (overwrote ? "overwrote" : "created")
                + " the derived continuing working save at tick " + tick
                + ": " + savePath + "\n" + census;
            Log.Message(receipt);
            return receipt;
        }

        internal static string LoadToxicWasteArrivalWorkingReceipt()
        {
            string savePath = GenFilePaths.FilePathForSavedGame(
                ToxicWasteRelocationWorkingSaveName);
            if (!File.Exists(savePath))
                return "[CA] agent receipt: toxic-waste-arrival-load-working\n"
                    + "refused; derived working test save is absent: " + savePath;
            GameDataSaveLoader.LoadGame(ToxicWasteRelocationWorkingSaveName);
            return "[CA] agent receipt: toxic-waste-arrival-load-working\n"
                + "queued native reload of derived continuing working save "
                + savePath;
        }

        internal static string SaveActiveToxicWasteRelocationReceipt()
        {
            return SaveToxicWasteRelocationMilestoneReceipt(
                "toxic-waste-relocation-save-active",
                ToxicWasteRelocationActiveSaveName, requireCompleted: false,
                requireReloadedCheckpoint: false);
        }

        internal static string ResumeToxicWasteRelocationReceipt()
        {
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(Find.CurrentMap);
            if (lifecycle == null)
                return "[CA] agent receipt: toxic-waste-relocation-resume\n"
                    + "refused; no loaded map lifecycle";
            string census = lifecycle.RefreshAndReceipt();
            if (!lifecycle.HasActiveAuthorizedRelocation)
                return "[CA] agent receipt: toxic-waste-relocation-resume\n"
                    + "refused; no exact active authorized job with both "
                    + "reservations is available to resume\n" + census;
            if (!lifecycle.CurrentAuthorizationIsVerificationFixture)
                return "[CA] agent receipt: toxic-waste-relocation-resume\n"
                    + "refused; the active authorization origin is '"
                    + (lifecycle.CurrentAuthorizationOriginForVerification
                        ?? "none") + "', not the fixed verification fixture\n"
                    + census;
            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            string checkpoint;
            if (bridge == null || !bridge.ReloadedCheckpointMatches(
                lifecycle.CurrentAuthorizationIdForVerification,
                lifecycle.CurrentAuthorizationOriginForVerification,
                out checkpoint))
                return "[CA] agent receipt: toxic-waste-relocation-resume\n"
                    + "refused; the active operation was not loaded from its "
                    + "saved verification checkpoint\n" + census;
            if (Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            string receipt = "[CA] agent receipt: "
                + "toxic-waste-relocation-resume\npass; retained the saved "
                + "authorization and native job; speed "
                + (Find.TickManager?.CurTimeSpeed.ToString() ?? "unavailable")
                + "; " + checkpoint + "\n"
                + lifecycle.RefreshAndReceipt();
            Log.Message(receipt);
            return receipt;
        }

        internal static string LoadActiveToxicWasteRelocationCheckpointReceipt()
        {
            string savePath = GenFilePaths.FilePathForSavedGame(
                ToxicWasteRelocationActiveSaveName);
            if (!File.Exists(savePath))
                return "[CA] agent receipt: toxic-waste-relocation-load-active\n"
                    + "refused; active checkpoint save is absent: " + savePath;
            GameDataSaveLoader.LoadGame(ToxicWasteRelocationActiveSaveName);
            return "[CA] agent receipt: toxic-waste-relocation-load-active\n"
                + "queued native reload of " + savePath;
        }

        internal static string SaveCompletedToxicWasteRelocationReceipt()
        {
            return SaveToxicWasteRelocationMilestoneReceipt(
                "toxic-waste-relocation-save-completed",
                ToxicWasteRelocationCompletedSaveName, requireCompleted: true,
                requireReloadedCheckpoint: false);
        }

        internal static string SaveResumedToxicWasteRelocationReceipt()
        {
            return SaveToxicWasteRelocationMilestoneReceipt(
                "toxic-waste-relocation-save-resumed",
                ToxicWasteRelocationResumedSaveName, requireCompleted: true,
                requireReloadedCheckpoint: true);
        }

        private static string SaveToxicWasteRelocationMilestoneReceipt(
            string commandName, string saveName, bool requireCompleted,
            bool requireReloadedCheckpoint)
        {
            Map map = Find.CurrentMap;
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(map);
            if (map == null || lifecycle == null)
                return "[CA] agent receipt: " + commandName + "\n"
                    + "refused; no loaded map lifecycle";
            string census = lifecycle.RefreshAndReceipt();
            if (!lifecycle.CurrentAuthorizationIsVerificationFixture)
                return "[CA] agent receipt: " + commandName + "\n"
                    + "refused; the current authorization origin is '"
                    + (lifecycle.CurrentAuthorizationOriginForVerification
                        ?? "none") + "', not the fixed verification fixture\n"
                    + census;
            bool milestoneReady = requireCompleted
                ? lifecycle.HasCompletedAuthorizedRelocation
                : lifecycle.HasActiveAuthorizedRelocation;
            if (!milestoneReady)
                return "[CA] agent receipt: " + commandName + "\n"
                    + "refused; the exact authorized native relocation has not "
                    + (requireCompleted
                        ? "completed with at least one successful and zero failed job\n"
                        : "reached an active job with both reservations and zero failures\n")
                    + census;

            CAAgentDebugBridge bridge = CAAgentDebugBridge.ForCurrentGame();
            string checkpoint = null;
            if (requireReloadedCheckpoint
                && (bridge == null || !bridge.ReloadedCheckpointMatches(
                    lifecycle.CurrentAuthorizationIdForVerification,
                    lifecycle.CurrentAuthorizationOriginForVerification,
                    out checkpoint)))
                return "[CA] agent receipt: " + commandName + "\n"
                    + "refused; completion was not reached after loading the "
                    + "matching active-operation checkpoint\n" + census;

            string savePath = GenFilePaths.FilePathForSavedGame(saveName);
            if (File.Exists(savePath))
                return "[CA] agent receipt: " + commandName + "\n"
                    + "refused to overwrite existing " + savePath + "\n"
                    + census;
            int tick = Find.TickManager?.TicksGame ?? -1;
            if (!requireCompleted)
            {
                if (bridge == null)
                    return "[CA] agent receipt: " + commandName + "\n"
                        + "refused; verification bridge unavailable\n" + census;
                bridge.ArmActiveCheckpoint(
                    lifecycle.CurrentAuthorizationIdForVerification,
                    lifecycle.CurrentAuthorizationOriginForVerification,
                    saveName, tick);
            }
            GameDataSaveLoader.SaveGame(saveName);
            bool exists = File.Exists(savePath);
            if (!exists && !requireCompleted)
                bridge?.ClearActiveCheckpoint();
            string receipt = "[CA] agent receipt: " + commandName + "\n"
                + (exists ? "pass; " : "fail; ") + "saved "
                + (requireCompleted ? "completed placement" : "active reservations")
                + " at tick " + tick + " to " + savePath + "\n"
                + (checkpoint.NullOrEmpty() ? string.Empty
                    : checkpoint + "\n")
                + census;
            Log.Message(receipt);
            return receipt;
        }

        private static bool TryFindToxicWasteRelocationFixtureCell(Map map,
            ThingDef wastepackDef, IReadOnlyList<CASpaceProgram> programs,
            out Pawn hauler, out IntVec3 result)
        {
            var programCells = new HashSet<IntVec3>();
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program?.cells == null) continue;
                for (int c = 0; c < program.cells.Count; c++)
                    programCells.Add(program.cells[c]);
            }
            List<Pawn> haulers = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead
                    && !pawn.Downed && !pawn.Drafted && !pawn.InMentalState
                    && pawn.Faction == Faction.OfPlayer
                    && (pawn.IsColonist || pawn.IsColonyMech
                        || pawn.IsColonySubhuman)
                    && pawn.workSettings?.WorkIsActive(
                        WorkTypeDefOf.Hauling) == true
                    && pawn.health?.capacities.CapableOf(
                        PawnCapacityDefOf.Manipulation) == true)
                .OrderBy(pawn => pawn.IsColonyMech ? 0 : 1)
                .ThenBy(pawn => pawn.thingIDNumber).ToList();
            for (int p = 0; p < haulers.Count; p++)
            {
                Pawn candidateHauler = haulers[p];
                for (int radius = 4; radius <= 30; radius++)
                {
                    for (int offset = -radius; offset <= radius; offset++)
                    {
                        IntVec3 north = candidateHauler.Position
                            + new IntVec3(offset, 0, radius);
                        if (ToxicWasteRelocationFixtureCellIsValid(map,
                            wastepackDef, candidateHauler, north, programCells))
                        {
                            hauler = candidateHauler;
                            result = north;
                            return true;
                        }
                        IntVec3 south = candidateHauler.Position
                            + new IntVec3(offset, 0, -radius);
                        if (ToxicWasteRelocationFixtureCellIsValid(map,
                            wastepackDef, candidateHauler, south, programCells))
                        {
                            hauler = candidateHauler;
                            result = south;
                            return true;
                        }
                    }
                    for (int offset = -radius + 1;
                        offset <= radius - 1; offset++)
                    {
                        IntVec3 east = candidateHauler.Position
                            + new IntVec3(radius, 0, offset);
                        if (ToxicWasteRelocationFixtureCellIsValid(map,
                            wastepackDef, candidateHauler, east, programCells))
                        {
                            hauler = candidateHauler;
                            result = east;
                            return true;
                        }
                        IntVec3 west = candidateHauler.Position
                            + new IntVec3(-radius, 0, offset);
                        if (ToxicWasteRelocationFixtureCellIsValid(map,
                            wastepackDef, candidateHauler, west, programCells))
                        {
                            hauler = candidateHauler;
                            result = west;
                            return true;
                        }
                    }
                }
            }
            hauler = null;
            result = IntVec3.Invalid;
            return false;
        }

        private static bool ToxicWasteRelocationFixtureCellIsValid(Map map,
            ThingDef wastepackDef, Pawn hauler, IntVec3 cell,
            HashSet<IntVec3> programCells)
        {
            return cell.InBounds(map) && !cell.Fogged(map)
                && cell.Standable(map) && cell.GetThingList(map).Count == 0
                && cell.GetSlotGroup(map) == null
                && !programCells.Contains(cell)
                && cell.InAllowedArea(hauler)
                && GenSpawn.CanSpawnAt(wastepackDef, cell, map, Rot4.North,
                    canWipeEdifices: false)
                && hauler.CanReach(cell, PathEndMode.Touch, Danger.Some);
        }

        internal static string ControlledKitchenRequirementReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] controlled Kitchen requirement receipt: no map";

            // Fixture-only evidence. This is the exact exposed Kitchen room the
            // operator selected in the immutable playground. The verifier reads
            // that room and an existing player-authored program; it never paints
            // cells or selects a substitute.
            var anchor = new IntVec3(28, 0, 182);
            Room room = anchor.InBounds(map) ? anchor.GetRoom(map) : null;
            if (room == null || !room.ProperRoom || room.IsDoorway
                || room.PsychologicallyOutdoors || room.Fogged)
                return "[CA] controlled Kitchen requirement receipt: refused; "
                    + "operator Kitchen anchor " + anchor
                    + " is not a proper room";

            var expected = new HashSet<IntVec3>();
            for (int x = 25; x <= 31; x++)
                for (int z = 179; z <= 185; z++)
                    expected.Add(new IntVec3(x, 0, z));
            HashSet<IntVec3> roomCells = room.Cells.ToHashSet();
            if (!roomCells.SetEquals(expected))
                return "[CA] controlled Kitchen requirement receipt: refused; "
                    + "fixture room geometry changed at " + anchor
                    + "; expected the exact 7x7, 49-cell room";

            CASpaceProgram program = PlannedUseMapComponent.For(map)
                ?.ProgramsForObservation
                .FirstOrDefault(candidate => candidate != null
                    && candidate.author == CASpaceAuthor.Player
                    && candidate.purpose == CASpacePurpose.Kitchen
                    && candidate.cells != null
                    && candidate.cells.Contains(anchor));
            if (program == null)
                return "[CA] controlled Kitchen requirement receipt: refused; "
                    + "no player-authored Kitchen contains operator anchor "
                    + anchor;

            HashSet<IntVec3> actual = program.cells.ToHashSet();
            if (program.cells.Count != expected.Count
                || !actual.SetEquals(expected))
                return "[CA] controlled Kitchen requirement receipt: refused; "
                    + "program " + program.label + " #" + program.id
                    + " has " + program.cells.Count + " authored cells/"
                    + actual.Count + " unique cells but the fixed operator "
                    + "Kitchen fixture requires exactly " + expected.Count
                    + " unique declarations; no substitute footprint inferred";

            string evaluation = CAFacilitySitingModule
                .EvaluatePlayerFacilityPrograms(map);
            return "[CA] controlled Kitchen requirement receipt: accepted "
                + "the exact operator-authored Kitchen footprint at " + anchor
                + " (" + expected.Count + " cells). This verifier proves that "
                + "Kitchen fixture only; protected-store evidence belongs to "
                + "separate Storage or Freezer evaluations. Fixture coordinates "
                + "are test evidence, not a portable siting rule.\n"
                + evaluation;
        }

        [DebugAction("Colonist Awareness",
            "Receipt: contextual Bed in marked Barracks",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SettlementContextBedRankingCensus()
        {
            Log.Message(RunSettlementContextBedRankingReceipt());
        }

        internal static string RunSettlementContextBedRankingReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] contextual Bed receipt: no current map";

            // This is the room the operator previously identified and accepted as
            // one of the two parallel Barracks in the immutable playground. The
            // fixture refuses any changed geometry instead of selecting a room.
            var anchor = new IntVec3(40, 0, 171);
            Room room = anchor.InBounds(map) ? anchor.GetRoom(map) : null;
            if (room == null || !room.ProperRoom || room.IsDoorway
                || room.Fogged)
                return "[CA] contextual Bed receipt: operator-marked Barracks "
                    + "room is unavailable at " + anchor;

            var cells = new List<IntVec3>();
            foreach (IntVec3 cell in room.Cells)
                if (map.areaManager.Home[cell]) cells.Add(cell);
            if (cells.Count != 60)
                return "[CA] contextual Bed receipt: refused changed operator-"
                    + "marked Barracks geometry at " + anchor + "; expected 60 "
                    + "claimed cells, observed " + cells.Count;

            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            if (programs == null)
                return "[CA] contextual Bed receipt: space-program component "
                    + "unavailable";
            CASpaceProgram program = programs.ProgramAt(anchor);
            if (program == null)
            {
                for (int i = 0; i < cells.Count; i++)
                    if (programs.ProgramAt(cells[i]) != null)
                        return "[CA] contextual Bed receipt: marked Barracks "
                            + "overlaps an existing program";
                program = programs.CreateProgram(new CASpaceProgramDraft
                {
                    label = "Operator-marked Barracks A",
                    purpose = CASpacePurpose.Barracks,
                    style = CASpaceStyle.Adaptive,
                    maxOccupants = map.mapPawns.FreeColonistsSpawnedCount,
                    requiresSleep = true
                }, cells);
            }
            if (program.author != CASpaceAuthor.Player
                || program.purpose != CASpacePurpose.Barracks
                || !program.RequiresSleep || program.cells.Count != 60)
                return "[CA] contextual Bed receipt: existing marked program no "
                    + "longer matches the player-authored Barracks contract";
            var roomCells = new HashSet<IntVec3>(cells);
            if (program.cells.Any(cell => !roomCells.Contains(cell))
                || cells.Any(cell => !program.cells.Contains(cell)))
                return "[CA] contextual Bed receipt: existing program footprint "
                    + "does not exactly equal the operator-marked Barracks";

            SetColonyAutonomous();
            CASettlementPlanningContextMapComponent context =
                CASettlementPlanningContextMapComponent.For(map);
            context?.Refresh();
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            string outcome = "home-planning component unavailable";
            home?.DebugPlanNow(out outcome);
            string verifiedState = "receipt rejected";
            string verifiedDetail = "home-planning component unavailable";
            if (home == null || !home.TryVerifyContextualBedPlan(program,
                out verifiedState, out verifiedDetail))
                return "[CA] contextual Bed receipt: rejected non-target plan for "
                    + "operator-marked Barracks at " + anchor + "\nplanner: "
                    + outcome + "\nverification: " + verifiedDetail;
            CameraJumper.TryJump(anchor, map, CameraJumper.MovementMode.Cut);
            string receipt = "[CA] contextual Bed receipt: "
                + verifiedState
                + " in exact operator-marked 60-cell player Barracks at "
                + anchor + "\nverified target: " + verifiedDetail
                + "\nplanner: " + outcome + "\n"
                + (home?.Census() ?? "[CA] home planning: unavailable")
                + "\n" + (CAHomePrerequisiteMapComponent.For(map)?.Census()
                    ?? "[CA] home prerequisites: unavailable")
                + "\n" + (context?.Census()
                    ?? "[CA] settlement planning context: unavailable");
            return receipt;
        }

        [DebugAction("Colonist Awareness",
            "Receipt: marked Barracks composition",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MarkedBarracksCompositionCensus()
        {
            Log.Message(MarkedBarracksCompositionReceipt());
        }

        internal static string MarkedBarracksCompositionReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] marked Barracks composition: no current map";
            var anchor = new IntVec3(40, 0, 171);
            CASpaceProgram program = PlannedUseMapComponent.For(map)
                ?.ProgramAt(anchor);
            if (program == null)
                return "[CA] marked Barracks composition: no program at "
                    + anchor;

            var beds = new List<string>();
            var intrudingTables = new List<string>();
            var intrudingSeats = new List<string>();
            var colonyTables = new List<string>();
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                CASpaceProgram actual = programs?.ProgramAt(building.Position);
                if (building is Building_Bed && actual == program)
                    beds.Add(building.def.defName + " " + building.Position
                        + " facing " + building.Rotation);
                if (building.def.IsTable)
                {
                    int adjacentSeats = 0;
                    List<IntVec3> adjacent = GenAdjFast
                        .AdjacentCellsCardinal(building);
                    for (int c = 0; c < adjacent.Count; c++)
                    {
                        Building seat = adjacent[c].GetEdifice(map);
                        if (seat?.def.building?.isSittable == true)
                            adjacentSeats++;
                    }
                    string location = actual == null ? "unprogrammed"
                        : actual.label + " #" + actual.id;
                    string entry = building.def.defName + " "
                        + building.Position + " facing " + building.Rotation
                        + " in " + location + " with " + adjacentSeats
                        + " adjacent seats";
                    colonyTables.Add(entry);
                    if (actual == program) intrudingTables.Add(entry);
                }
                if (!(building is Building_Bed)
                    && building.def.building?.isSittable == true
                    && actual == program)
                    intrudingSeats.Add(building.def.defName + " "
                        + building.Position + " facing " + building.Rotation);
            }
            beds.Sort(StringComparer.Ordinal);
            intrudingTables.Sort(StringComparer.Ordinal);
            intrudingSeats.Sort(StringComparer.Ordinal);
            colonyTables.Sort(StringComparer.Ordinal);
            return "[CA] marked Barracks composition: " + program.label
                + " #" + program.id + " at " + anchor
                + "; beds " + beds.Count + " ["
                + (beds.Count == 0 ? "none" : string.Join(", ", beds.ToArray()))
                + "]; table intrusion "
                + (intrudingTables.Count == 0 ? "none" : string.Join(", ",
                    intrudingTables.ToArray()))
                + "; seat intrusion "
                + (intrudingSeats.Count == 0 ? "none" : string.Join(", ",
                    intrudingSeats.ToArray()))
                + "; colony tables "
                + (colonyTables.Count == 0 ? "none" : string.Join(", ",
                    colonyTables.ToArray()));
        }

        internal static string SaveSettlementPlanningContextReceipt()
        {
            Map map = Find.CurrentMap;
            CASettlementPlanningContextMapComponent component =
                CASettlementPlanningContextMapComponent.For(map);
            if (component == null)
                return "[CA] settlement context persistence: no map component";
            string savePath = GenFilePaths.FilePathForSavedGame(
                SettlementContextPersistenceSaveName);
            if (File.Exists(savePath))
                return "[CA] settlement context persistence: refused to overwrite "
                    + savePath;

            string before = component.Census();
            expectedSettlementContextRevision = component.Revision;
            expectedSettlementContextFirstObservedTick =
                component.FirstObservedTick;
            expectedSettlementContextSignature = component.EvidenceSignature;
            GameDataSaveLoader.SaveGame(SettlementContextPersistenceSaveName);
            string receipt = "[CA] settlement context persistence save: "
                + (File.Exists(savePath) ? "pass" : "fail")
                + "; revision " + expectedSettlementContextRevision
                + ", first observed "
                + expectedSettlementContextFirstObservedTick
                + "\n" + before;
            Log.Message(receipt);
            return receipt;
        }

        internal static string LoadSettlementPlanningContextReceipt()
        {
            string savePath = GenFilePaths.FilePathForSavedGame(
                SettlementContextPersistenceSaveName);
            if (!File.Exists(savePath))
                return "[CA] settlement context persistence load: fixture save is absent";
            GameDataSaveLoader.LoadGame(SettlementContextPersistenceSaveName);
            return "[CA] settlement context persistence load: queued native reload of "
                + savePath;
        }

        internal static string CensusSettlementPlanningContextPersistenceReceipt()
        {
            CASettlementPlanningContextMapComponent component =
                CASettlementPlanningContextMapComponent.For(Find.CurrentMap);
            if (component == null)
                return "[CA] settlement context persistence census: no map component";
            string census = component.Census();
            bool hasExpectation = expectedSettlementContextRevision >= 0
                && expectedSettlementContextSignature != null;
            bool pass = hasExpectation
                && component.Revision == expectedSettlementContextRevision
                && component.FirstObservedTick
                    == expectedSettlementContextFirstObservedTick
                && component.EvidenceSignature
                    == expectedSettlementContextSignature;
            string receipt = "[CA] settlement context persistence census: "
                + (pass ? "pass" : "fail")
                + "; expected revision "
                + expectedSettlementContextRevision
                + ", loaded revision " + component.Revision
                + ", expected first observed "
                + expectedSettlementContextFirstObservedTick
                + ", loaded first observed " + component.FirstObservedTick
                + ", signature "
                + (component.EvidenceSignature
                    == expectedSettlementContextSignature ? "preserved" : "changed")
                + "\n" + census;
            Log.Message(receipt);
            return receipt;
        }

        [DebugAction("Colonist Awareness", "Open space-program editor",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenPlannedUseSelector()
        {
            var designator = new Designator_CAPlannedUse();
            designator.ProcessInput(new Event());
            Log.Message("[CA] space-program editor opened through the native designator");
        }

        [DebugAction("Colonist Awareness", "Paint new adaptive Barracks",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PaintNewAdaptiveBarracks()
        {
            var designator = new Designator_CAPlannedUse();
            designator.SelectBarracksForDebug();
            Find.DesignatorManager.Select(designator);
            Log.Message("[CA] space-program painter active: new adaptive Barracks");
        }

        [DebugAction("Colonist Awareness",
            "Create austere Barracks from room under cursor",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CreateAustereBarracksFromCursorRoom()
        {
            CreateSleepProgramFromCursorRoom(CASpacePurpose.Barracks,
                CASpaceStyle.Austere, "Cursor Barracks");
        }

        [DebugAction("Colonist Awareness",
            "Create adaptive Bedroom from room under cursor",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CreateAdaptiveBedroomFromCursorRoom()
        {
            CreateSleepProgramFromCursorRoom(CASpacePurpose.Bedroom,
                CASpaceStyle.Adaptive, "Cursor Bedroom");
        }

        private static void CreateSleepProgramFromCursorRoom(
            CASpacePurpose purpose, CASpaceStyle style, string label)
        {
            Map map = Find.CurrentMap;
            IntVec3 cursor = UI.MouseCell();
            Room room = cursor.InBounds(map) ? cursor.GetRoom(map) : null;
            if (room == null || !room.ProperRoom || room.IsDoorway
                || room.Fogged)
            {
                Log.Message("[CA] cursor sleep-program fixture: no proper visible "
                    + "room at " + cursor);
                return;
            }

            var cells = new List<IntVec3>();
            foreach (IntVec3 cell in room.Cells)
                if (map.areaManager.Home[cell]) cells.Add(cell);
            if (cells.Count == 0)
            {
                Log.Message("[CA] cursor sleep-program fixture: room at " + cursor
                    + " has no claimed Home cells");
                return;
            }

            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            if (component == null)
            {
                Log.Message("[CA] cursor sleep-program fixture: no space-program "
                    + "component");
                return;
            }
            int people = Mathf.Max(1, map.mapPawns.FreeColonistsSpawnedCount);
            var draft = new CASpaceProgramDraft
            {
                label = label,
                purpose = purpose,
                style = style,
                maxOccupants = purpose == CASpacePurpose.Bedroom
                    ? Mathf.Min(2, people) : people,
                requiresSleep = true
            };
            CASpaceProgram program = component.CreateProgram(draft, cells);
            component.MarkForDraw();
            Log.Message("[CA] cursor sleep-program fixture: created "
                + purpose + " program "
                + program.id + " from " + cells.Count + " claimed room cells at "
                + cursor + ", maximum occupants " + program.maxOccupants);
        }

        [DebugAction("Colonist Awareness",
            "Fixture: assign first colonist to first sleep program",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AssignFirstColonistToFirstSleepProgram()
        {
            PlannedUseMapComponent component =
                PlannedUseMapComponent.For(Find.CurrentMap);
            CASpaceProgram program = component?.Programs
                .Where(candidate => candidate != null
                    && candidate.RequiresSleep
                    && CASpacePurposeInfo.CanRequireSleep(candidate.purpose))
                .OrderBy(candidate => candidate.id).FirstOrDefault();
            Pawn resident = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned
                ?.Where(pawn => pawn.needs?.rest != null
                    && !pawn.DevelopmentalStage.Baby())
                .OrderBy(pawn => pawn.thingIDNumber).FirstOrDefault();
            if (program == null || resident == null)
            {
                Log.Message("[CA] resident fixture: no sleep program or eligible colonist");
                return;
            }
            string reason = "The space-program component is unavailable.";
            if (!component.SetResident(program, resident, true, out reason))
            {
                Log.Message("[CA] resident fixture: " + reason);
                return;
            }
            Log.Message("[CA] resident fixture: assigned " + resident.LabelShort
                + " to " + program.label + " using the production program API");
        }

        [DebugAction("Colonist Awareness",
            "Inspect first sleep-program residents",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void InspectFirstSleepProgramResidents()
        {
            PlannedUseMapComponent component =
                PlannedUseMapComponent.For(Find.CurrentMap);
            CASpaceProgram program = component?.Programs
                .Where(candidate => candidate != null
                    && candidate.RequiresSleep
                    && CASpacePurposeInfo.CanRequireSleep(candidate.purpose))
                .OrderBy(candidate => candidate.id).FirstOrDefault();
            if (program == null)
            {
                Log.Message("[CA] resident inspector: no sleep program");
                return;
            }
            Find.WindowStack.Add(new Dialog_CAProgramResidents(component,
                program));
            Log.Message("[CA] resident inspector: opened production roster for "
                + program.label);
        }

        [DebugAction("Colonist Awareness",
            "Fixture: let first sleep program residents decide",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LetFirstSleepProgramResidentsDecide()
        {
            PlannedUseMapComponent component =
                PlannedUseMapComponent.For(Find.CurrentMap);
            CASpaceProgram program = component?.Programs
                .Where(candidate => candidate != null
                    && candidate.RequiresSleep
                    && CASpacePurposeInfo.CanRequireSleep(candidate.purpose))
                .OrderBy(candidate => candidate.id).FirstOrDefault();
            string reason = "The space-program component is unavailable.";
            if (program == null || !component.SetResidentRosterAuthor(program,
                CAResidentRosterAuthor.Pawn, out reason))
            {
                Log.Message("[CA] resident fixture: "
                    + (program == null ? "no sleep program" : reason));
                return;
            }
            Log.Message("[CA] resident fixture: " + program.label
                + " roster handed to residents");
        }

        [DebugAction("Colonist Awareness",
            "Negotiate space-program residents",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void NegotiateSpaceProgramResidents()
        {
            PlannedUseMapComponent component =
                PlannedUseMapComponent.For(Find.CurrentMap);
            string outcome;
            if (component == null)
            {
                Log.Message("[CA] resident negotiation: no space-program component");
                return;
            }
            component.TryNegotiateResidents(out outcome);
            Log.Message("[CA] resident negotiation: " + outcome);
        }

        [DebugAction("Colonist Awareness",
            "Fixture: restore exact Dolly from Prepare Carefully",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RestoreExactDollyFromPrepareCarefully()
        {
            Pawn dolly;
            string outcome;
            bool restored = CAPrepareCarefullyFixtureBridge.TryRestoreDolly(
                Find.CurrentMap, out dolly, out outcome);
            Log.Message("[CA] exact Dolly fixture: " + outcome);
            if (!restored)
            {
                Messages.Message("Colonist Awareness: " + outcome + ".",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Find.Selector.ClearSelection();
            Find.Selector.Select(dolly);
            CameraJumper.TryJump(dolly, CameraJumper.MovementMode.Cut);
            Messages.Message("Colonist Awareness: exact Dolly restored; "
                + "fixture remains unsaved.", new TargetInfo(dolly),
                MessageTypeDefOf.PositiveEvent, historical: false);
        }

        [DebugAction("Colonist Awareness", "Focus current home plan",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FocusCurrentHomePlan()
        {
            Map map = Find.CurrentMap;
            AutonomousHomeMapComponent component =
                AutonomousHomeMapComponent.For(map);
            IntVec3 cell;
            string label;
            if (component == null
                || !component.TryGetCurrentPlanTarget(out cell, out label))
            {
                Messages.Message("Colonist Awareness: no automatic home plan to focus.",
                    MessageTypeDefOf.SilentInput, historical: false);
                return;
            }

            CameraJumper.TryJump(cell, map, CameraJumper.MovementMode.Cut);
            string text = "[CA] home planning focus: " + label + " at " + cell;
            Log.Message(text);
            Messages.Message("Colonist Awareness: focused " + label + ".",
                new TargetInfo(cell, map), MessageTypeDefOf.SilentInput,
                historical: false);
        }

        [DebugAction("Colonist Awareness", "Home planning fixture: materials",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HomePlanningMaterialFixture()
        {
            Map map = Find.CurrentMap;
            if (homePlanningFixtureMap == map
                && homePlanningFixtureMaterial != null
                && homePlanningFixtureMaterial.Spawned)
            {
                Messages.Message("Colonist Awareness: the home-planning material "
                    + "fixture already exists.",
                    new TargetInfo(homePlanningFixtureMaterial.Position, map),
                    MessageTypeDefOf.SilentInput, historical: false);
                return;
            }

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            Pawn anchor = null;
            for (int i = 0; i < colonists.Count; i++)
            {
                if (colonists[i] != null && colonists[i].Spawned)
                {
                    anchor = colonists[i];
                    break;
                }
            }
            if (anchor == null)
            {
                Messages.Message("Colonist Awareness: no spawned colonist can "
                    + "anchor the home-planning material fixture.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Thing material = ThingMaker.MakeThing(ThingDefOf.WoodLog);
            material.stackCount = material.def.stackLimit;
            Thing placed;
            if (!GenPlace.TryPlaceThing(material, anchor.Position, map,
                ThingPlaceMode.Near, out placed))
            {
                material.Destroy();
                Messages.Message("Colonist Awareness: no nearby cell can hold "
                    + "the home-planning material fixture.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Thing reserveMaterial = ThingMaker.MakeThing(ThingDefOf.WoodLog);
            reserveMaterial.stackCount = reserveMaterial.def.stackLimit;
            Thing reservePlaced;
            if (!GenPlace.TryPlaceThing(reserveMaterial, anchor.Position, map,
                ThingPlaceMode.Near, out reservePlaced))
            {
                reserveMaterial.Destroy();
                placed.Destroy();
                Messages.Message("Colonist Awareness: no nearby cell can hold "
                    + "the second home-planning material stack.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            placed.SetForbidden(false, warnOnFail: false);
            reservePlaced.SetForbidden(false, warnOnFail: false);
            homePlanningFixtureMap = map;
            homePlanningFixtureMaterial = placed;
            string text = "[CA] home planning fixture: placed "
                + (placed.stackCount + reservePlaced.stackCount) + " wood at "
                + placed.Position + " and " + reservePlaced.Position;
            Log.Message(text);
            Messages.Message("Colonist Awareness: home-planning materials placed.",
                new TargetInfo(placed.Position, map),
                MessageTypeDefOf.SilentInput, historical: false);
        }

        [DebugAction("Colonist Awareness",
            "Home planning fixture: reserve loose building stock",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReserveLooseBuildingStockFixture()
        {
            Map map = Find.CurrentMap;
            int stacks = 0;
            int units = 0;
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || !thing.Spawned
                    || thing.def.category != ThingCategory.Item
                    || !thing.def.IsStuff
                    || (thing.Faction != null && thing.Faction != Faction.OfPlayer)
                    || thing.TryGetComp<CompForbiddable>() == null)
                    continue;

                OperationalAccessComponent.RecordPlayerPolicy(thing,
                    forbidden: true);
                thing.SetForbidden(true, warnOnFail: false);
                stacks++;
                units += thing.stackCount;
            }

            string text = "[CA] home planning stock-reserve fixture: player-reserved "
                + stacks + " loose stuff stacks (" + units + " units); future "
                + "production remains available";
            Log.Message(text);
            Messages.Message("Colonist Awareness: reserved " + stacks
                + " current loose building-material stacks.",
                MessageTypeDefOf.SilentInput, historical: false);
        }

        [DebugAction("Colonist Awareness", "Run home planner now",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunHomePlannerNow()
        {
            AutonomousHomeMapComponent component =
                AutonomousHomeMapComponent.For(Find.CurrentMap);
            string outcome = "no map component";
            bool planned = component != null && component.DebugPlanNow(out outcome);
            Log.Message("[CA] home planner now: " + outcome);
            if (!planned)
                Messages.Message("Colonist Awareness: " + outcome + ".",
                    MessageTypeDefOf.SilentInput, historical: false);
        }

        internal static string RunHomeStorageReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] agent receipt: no current map";

            SetColonyAutonomous();
            ReserveLooseBuildingStockFixture();

            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            string outcome = "no home-planning component";
            bool planned = home != null && home.DebugPlanNow(out outcome);
            string receipt = "[CA] agent receipt: home-storage\n"
                + "planner: " + (planned ? "planned" : "blocked") + "; "
                + outcome + "\n"
                + (home?.Census() ?? "[CA] home planning: unavailable") + "\n"
                + (CAHomePrerequisiteMapComponent.For(map)?.Census()
                    ?? "[CA] home prerequisites: unavailable") + "\n"
                + (CAStorageProgramMapComponent.For(map)?.Census()
                    ?? "[CA] storage programs: unavailable");
            Log.Message(receipt);
            return receipt;
        }

        internal static string RunStorageVetoReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] agent receipt: no current map";

            CAStorageProgramMapComponent storage =
                CAStorageProgramMapComponent.For(map);
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            string deletion = "storage component unavailable";
            bool vetoRecorded = storage != null
                && storage.DebugDeleteFirstActiveProjection(out deletion);

            string vetoOutcome = "no home-planning component";
            bool plannedWhileVetoed = home != null
                && home.DebugPlanNow(out vetoOutcome);
            int activeWhileVetoed = -1;
            int vetoCount = -1;
            storage?.DebugCounts(out activeWhileVetoed, out vetoCount);
            bool vetoPass = vetoRecorded && !plannedWhileVetoed
                && activeWhileVetoed == 0 && vetoCount == 1;
            string vetoCensus = storage?.Census()
                ?? "[CA] storage programs: unavailable";

            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null)
                return "[CA] agent receipt: settings unavailable after "
                    + deletion;
            settings.autonomousHomePlanning = false;
            home?.NotifyPlanningSettingChanged(false,
                settings.autonomousHomePlanningResetGeneration);
            storage?.NotifyPlanningSettingChanged(false,
                settings.autonomousHomePlanningResetGeneration);
            settings.autonomousHomePlanningResetGeneration++;
            settings.autonomousHomePlanning = true;
            home?.NotifyPlanningSettingChanged(true,
                settings.autonomousHomePlanningResetGeneration);
            storage?.NotifyPlanningSettingChanged(true,
                settings.autonomousHomePlanningResetGeneration);

            string renewedOutcome = "no home-planning component";
            bool plannedAfterRenewal = home != null
                && home.DebugPlanNow(out renewedOutcome);
            int activeAfterRenewal = -1;
            int vetoesAfterRenewal = -1;
            storage?.DebugCounts(out activeAfterRenewal,
                out vetoesAfterRenewal);
            bool renewalPass = activeAfterRenewal == 1
                && vetoesAfterRenewal == 0;
            string renewedCensus = storage?.Census()
                ?? "[CA] storage programs: unavailable";

            string receipt = "[CA] agent receipt: storage-veto\n"
                + "delete: " + (vetoRecorded ? "pass; " : "fail; ")
                + deletion + "\n"
                + "while vetoed: " + (vetoPass ? "pass; " : "fail; ")
                + (plannedWhileVetoed ? "blueprint placed; "
                    : "objective waiting; ")
                + vetoOutcome + "\n" + vetoCensus + "\n"
                + "after authority renewal: "
                + (renewalPass ? "pass; " : "fail; ")
                + (plannedAfterRenewal ? "blueprint placed; "
                    : "objective waiting; ")
                + renewedOutcome + "\n" + renewedCensus;
            Log.Message(receipt);
            return receipt;
        }

        internal static string RunStorageEditReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] agent receipt: no current map";

            CAStorageProgramMapComponent storage =
                CAStorageProgramMapComponent.For(map);
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            string edit = "storage component unavailable";
            bool transferred = storage != null
                && storage.DebugEditFirstActiveProjection(out edit);
            int activeAfterEdit = -1;
            int vetoesAfterEdit = -1;
            storage?.DebugCounts(out activeAfterEdit, out vetoesAfterEdit);

            string plannerOutcome = "no home-planning component";
            bool blueprintPlaced = home != null
                && home.DebugPlanNow(out plannerOutcome);
            int activeAfterPlanner = -1;
            int vetoesAfterPlanner = -1;
            storage?.DebugCounts(out activeAfterPlanner,
                out vetoesAfterPlanner);
            bool pass = transferred && activeAfterEdit == 0
                && vetoesAfterEdit == 1 && activeAfterPlanner == 0
                && vetoesAfterPlanner == 1
                && plannerOutcome.IndexOf("existing native storage",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            string receipt = "[CA] agent receipt: storage-edit\n"
                + "transfer: " + (pass ? "pass; " : "fail; ") + edit + "\n"
                + "planner: " + (blueprintPlaced ? "blueprint placed; "
                    : "objective waiting; ") + plannerOutcome + "\n"
                + (storage?.Census()
                    ?? "[CA] storage programs: unavailable");
            Log.Message(receipt);
            return receipt;
        }

        internal static string StartStorageFlowReceipt()
        {
            int before = Find.TickManager?.TicksGame ?? -1;
            string setup = RunHomeStorageReceipt();
            if (Find.TickManager != null)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            string receipt = "[CA] agent receipt: storage-flow-start\n"
                + "tick " + before + "; native speed Fast\n" + setup;
            Log.Message(receipt);
            return receipt;
        }

        internal static string StorageFlowCensusReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] agent receipt: no current map";
            string receipt = "[CA] agent receipt: storage-flow-census\n"
                + "tick " + Find.TickManager.TicksGame + "; speed "
                + Find.TickManager.CurTimeSpeed + "\n"
                + (AutonomousHomeMapComponent.For(map)?.Census()
                    ?? "[CA] home planning: unavailable") + "\n"
                + (CAHomePrerequisiteMapComponent.For(map)?.Census()
                    ?? "[CA] home prerequisites: unavailable") + "\n"
                + (CAStorageProgramMapComponent.For(map)?.Census()
                    ?? "[CA] storage programs: unavailable");
            Log.Message(receipt);
            return receipt;
        }

        internal static string SaveStoragePersistenceReceipt()
        {
            string savePath = GenFilePaths.FilePathForSavedGame(
                StoragePersistenceSaveName);
            if (File.Exists(savePath))
                return "[CA] agent receipt: storage-persistence-save\n"
                    + "refused to overwrite existing " + savePath;

            string setup = RunHomeStorageReceipt();
            int tick = Find.TickManager?.TicksGame ?? -1;
            GameDataSaveLoader.SaveGame(StoragePersistenceSaveName);
            bool exists = File.Exists(savePath);
            string receipt = "[CA] agent receipt: storage-persistence-save\n"
                + (exists ? "pass; " : "fail; ") + "saved tick " + tick
                + " to " + savePath + "\n" + setup;
            Log.Message(receipt);
            return receipt;
        }

        internal static string LoadStoragePersistenceReceipt()
        {
            string savePath = GenFilePaths.FilePathForSavedGame(
                StoragePersistenceSaveName);
            if (!File.Exists(savePath))
                return "[CA] agent receipt: storage-persistence-load\n"
                    + "fixture save is absent";
            GameDataSaveLoader.LoadGame(StoragePersistenceSaveName);
            return "[CA] agent receipt: storage-persistence-load\n"
                + "queued native reload of " + savePath;
        }

        internal static string CensusStoragePersistenceReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "[CA] agent receipt: no current map";
            CAStorageProgramMapComponent storage =
                CAStorageProgramMapComponent.For(map);
            int active = -1;
            int vetoes = -1;
            storage?.DebugCounts(out active, out vetoes);
            string storageCensus = storage?.Census()
                ?? "[CA] storage programs: unavailable";
            string prerequisiteCensus =
                CAHomePrerequisiteMapComponent.For(map)?.Census()
                ?? "[CA] home prerequisites: unavailable";
            bool pass = active == 1 && vetoes == 0
                && storageCensus.IndexOf("demand 45, deficit 45",
                    StringComparison.OrdinalIgnoreCase) >= 0
                && prerequisiteCensus.IndexOf("WoodLog 0/45 (deficit 45)",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            string receipt = "[CA] agent receipt: storage-persistence-census\n"
                + (pass ? "pass; " : "fail; ") + "tick "
                + Find.TickManager.TicksGame + "; active " + active
                + "; vetoes " + vetoes + "\n" + prerequisiteCensus
                + "\n" + storageCensus;
            Log.Message(receipt);
            return receipt;
        }

        internal static string InventoryStorageReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null) return "[CA] agent receipt: no current map";
            CAStorageProgramMapComponent storage =
                CAStorageProgramMapComponent.For(map);
            string receipt = storage?.DebugEnsureInventoryStorage()
                ?? "[CA] agent receipt: storage component unavailable";
            OperationalAccessComponent access = OperationalAccessComponent.Instance;
            if (access != null)
                receipt += "\n" + access.CategorizedCensus(map);
            Log.Message(receipt);
            return receipt;
        }

        internal static string FocusInventoryStorageReceipt()
        {
            string receipt = CAStorageProgramMapComponent.For(Find.CurrentMap)
                ?.DebugFocusInventoryStorage()
                ?? "[CA] agent receipt: storage component unavailable";
            Log.Message(receipt);
            return receipt;
        }

        internal static string RunInventoryStorageReceipt()
        {
            string receipt = CAStorageProgramMapComponent.For(Find.CurrentMap)
                ?.DebugRunInventoryStorage()
                ?? "[CA] agent receipt: storage component unavailable";
            Log.Message(receipt);
            return receipt;
        }

        internal static string RunArmoryStorageReceipt()
        {
            string receipt = CAStorageProgramMapComponent.For(Find.CurrentMap)
                ?.DebugRunArmoryStorage()
                ?? "[CA] agent receipt: storage component unavailable";
            Log.Message(receipt);
            return receipt;
        }

        internal static string RunHomePlanningReceipt()
        {
            Map map = Find.CurrentMap;
            if (map == null) return "[CA] agent receipt: no current map";
            SetColonyAutonomous();
            AutonomousHomeMapComponent home =
                AutonomousHomeMapComponent.For(map);
            string outcome = "no home-planning component";
            bool planned = home != null && home.DebugPlanNow(out outcome);
            string receipt = "[CA] agent receipt: home-planning\nplanner: "
                + (planned ? "planned; " : "waiting; ") + outcome + "\n"
                + (home?.Census() ?? "[CA] home planning: unavailable")
                + "\n" + (CAHomePrerequisiteMapComponent.For(map)?.Census()
                    ?? "[CA] home prerequisites: unavailable");
            Log.Message(receipt);
            return receipt;
        }

        [DebugAction("Colonist Awareness", "Enable home planning; set colony Autonomous",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SetColonyAutonomous()
        {
            bool homePlanningWasEnabled =
                AwarenessMod.Settings.autonomousHomePlanning;
            AutonomousHomeMapComponent component =
                AutonomousHomeMapComponent.For(Find.CurrentMap);
            if (!homePlanningWasEnabled)
                AwarenessMod.Settings.autonomousHomePlanningResetGeneration++;
            AwarenessMod.Settings.autonomousHomePlanning = true;
            if (!homePlanningWasEnabled)
            {
                component?.NotifyPlanningSettingChanged(true,
                    AwarenessMod.Settings.autonomousHomePlanningResetGeneration);
                CAStorageProgramMapComponent.For(Find.CurrentMap)
                    ?.NotifyPlanningSettingChanged(true,
                        AwarenessMod.Settings
                            .autonomousHomePlanningResetGeneration);
            }
            List<Pawn> colonists = Find.CurrentMap.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                AutonomyComponent.SetLevel(colonists[i], 3);
            Messages.Message("Colonist Awareness: Home planning enabled; "
                + colonists.Count + " colonists set to Autonomous.",
                MessageTypeDefOf.SilentInput,
                historical: false);
        }

        [DebugAction("Colonist Awareness", "Obedience matrix",
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ObedienceMatrix()
        {
            var map = Find.CurrentMap;
            var sb = new StringBuilder("[CA] obedience matrix (leader -> member):\n");
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var leader = colonists[i];
                int sq = SquadComponent.SquadOf(leader);
                if (sq <= 0 || SquadComponent.LeaderPawn(sq, map) != leader) continue;
                for (int j = 0; j < colonists.Count; j++)
                {
                    var m = colonists[j];
                    if (m == leader || SquadComponent.SquadOf(m) != sq) continue;
                    string reason;
                    var verdict = Authority.Check(leader, m, out reason);
                    sb.AppendLine("  " + leader.LabelShort + " -> " + m.LabelShort + ": " + verdict
                        + (reason != null ? " (" + reason + ")" : "")
                        + "  [command standing "
                        + Authority.CommandStanding(leader, m).ToString("F2")
                        + "]");
                }
            }
            Log.Message(sb.ToString());
        }
    }

    // Dev-mode-only command/receipt bridge for background verification. It
    // deliberately accepts a tiny fixed command vocabulary and operates on the
    // operator-selected controlled game so automated receipts do not require
    // foreground focus, pointer ownership, or a parallel input driver.
    internal sealed class CAAgentDebugBridge : GameComponent
    {
        private const string CommandFileName =
            "ColonistAwareness.debug-command.txt";
        private const string ReceiptFileName =
            "ColonistAwareness.debug-receipt.txt";
        private float nextPollRealtime;
        private static string pendingNativeLoadSaveName;
        private string loadedSaveName;
        private string activeCheckpointAuthorizationId;
        private string activeCheckpointAuthorizationOrigin;
        private string activeCheckpointSaveName;
        private int activeCheckpointSavedTick = -1;
        private bool activeCheckpointReloadObserved;
        private string bedroomCauseMaterialAuditOriginId;
        private int bedroomCauseMaterialAuditProgramId;
        private int bedroomCauseMaterialAuditTargetResidentId;
        private int bedroomCauseRequiredMaterialCount;
        private int bedroomCauseWoodCountAtAuditStart = -1;
        private string bedroomCauseConsumingFrameId;
        private string bedroomCauseConsumedMaterialEvidence;
        private int bedroomCauseConsumedMaterialCount;
        private int bedroomCauseWoodCountBeforeFrameCompletion = -1;
        private int bedroomCauseWoodCountAfterFrameCompletion = -1;
        private string bedroomCauseCompletionJobDefName;
        private string bedroomCauseCompletionJobEvidence;
        private bool bedroomCauseCompletionJobTargetMatched;
        private bool bedroomCauseObservedForcedCompletionJob;
        private List<string> bedroomCauseNativeResourceJobEvidence =
            new List<string>();
        private bool bedroomCauseObservedForcedResourceJob;
        private string bedroomCausePlanningScopeEvidence;
        private int bedroomCausePlanningScopeMapId = -1;
        private int bedroomCausePlanningScopeProgramId;
        private int bedroomCausePlanningScopeTargetResidentId;
        private string bedroomCausePlanningScopeOriginId;
        private bool bedroomCausePlanningScopeRequiresLease;
        private bool bedroomCausePlanningScopeActive;
        private bool bedroomCausePlanningScopeCompleted;
        private int bedroomCausePlanningScopeAuthorityGeneration = -1;

        public CAAgentDebugBridge(Game game) { }

        internal static CAAgentDebugBridge ForCurrentGame()
        {
            return Verse.Current.Game?.components?
                .OfType<CAAgentDebugBridge>().FirstOrDefault();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref activeCheckpointAuthorizationId,
                "CA_agentActiveCheckpointAuthorizationId");
            Scribe_Values.Look(ref activeCheckpointAuthorizationOrigin,
                "CA_agentActiveCheckpointAuthorizationOrigin");
            Scribe_Values.Look(ref activeCheckpointSaveName,
                "CA_agentActiveCheckpointSaveName");
            Scribe_Values.Look(ref activeCheckpointSavedTick,
                "CA_agentActiveCheckpointSavedTick", -1);
            Scribe_Values.Look(ref activeCheckpointReloadObserved,
                "CA_agentActiveCheckpointReloadObserved", false);
            Scribe_Values.Look(ref bedroomCauseMaterialAuditOriginId,
                "CA_agentBedroomCauseMaterialAuditOriginId");
            Scribe_Values.Look(ref bedroomCauseMaterialAuditProgramId,
                "CA_agentBedroomCauseMaterialAuditProgramId", 0);
            Scribe_Values.Look(ref bedroomCauseMaterialAuditTargetResidentId,
                "CA_agentBedroomCauseMaterialAuditTargetResidentId", 0);
            Scribe_Values.Look(ref bedroomCauseRequiredMaterialCount,
                "CA_agentBedroomCauseRequiredMaterialCount", 0);
            Scribe_Values.Look(ref bedroomCauseWoodCountAtAuditStart,
                "CA_agentBedroomCauseWoodCountAtAuditStart", -1);
            Scribe_Values.Look(ref bedroomCauseConsumingFrameId,
                "CA_agentBedroomCauseConsumingFrameId");
            Scribe_Values.Look(ref bedroomCauseConsumedMaterialEvidence,
                "CA_agentBedroomCauseConsumedMaterialEvidence");
            Scribe_Values.Look(ref bedroomCauseConsumedMaterialCount,
                "CA_agentBedroomCauseConsumedMaterialCount", 0);
            Scribe_Values.Look(ref bedroomCauseWoodCountBeforeFrameCompletion,
                "CA_agentBedroomCauseWoodCountBeforeFrameCompletion", -1);
            Scribe_Values.Look(ref bedroomCauseWoodCountAfterFrameCompletion,
                "CA_agentBedroomCauseWoodCountAfterFrameCompletion", -1);
            Scribe_Values.Look(ref bedroomCauseCompletionJobDefName,
                "CA_agentBedroomCauseCompletionJobDefName");
            Scribe_Values.Look(ref bedroomCauseCompletionJobEvidence,
                "CA_agentBedroomCauseCompletionJobEvidence");
            Scribe_Values.Look(ref bedroomCauseCompletionJobTargetMatched,
                "CA_agentBedroomCauseCompletionJobTargetMatched", false);
            Scribe_Values.Look(ref bedroomCauseObservedForcedCompletionJob,
                "CA_agentBedroomCauseObservedForcedCompletionJob", false);
            Scribe_Collections.Look(ref bedroomCauseNativeResourceJobEvidence,
                "CA_agentBedroomCauseNativeResourceJobEvidence",
                LookMode.Value);
            Scribe_Values.Look(ref bedroomCauseObservedForcedResourceJob,
                "CA_agentBedroomCauseObservedForcedResourceJob", false);
            Scribe_Values.Look(ref bedroomCausePlanningScopeEvidence,
                "CA_agentBedroomCausePlanningScopeEvidence");
            Scribe_Values.Look(ref bedroomCausePlanningScopeMapId,
                "CA_agentBedroomCausePlanningScopeMapId", -1);
            Scribe_Values.Look(ref bedroomCausePlanningScopeProgramId,
                "CA_agentBedroomCausePlanningScopeProgramId", 0);
            Scribe_Values.Look(ref bedroomCausePlanningScopeTargetResidentId,
                "CA_agentBedroomCausePlanningScopeTargetResidentId", 0);
            Scribe_Values.Look(ref bedroomCausePlanningScopeOriginId,
                "CA_agentBedroomCausePlanningScopeOriginId");
            Scribe_Values.Look(ref bedroomCausePlanningScopeRequiresLease,
                "CA_agentBedroomCausePlanningScopeRequiresLease", false);
            Scribe_Values.Look(ref bedroomCausePlanningScopeActive,
                "CA_agentBedroomCausePlanningScopeActive", false);
            Scribe_Values.Look(ref bedroomCausePlanningScopeCompleted,
                "CA_agentBedroomCausePlanningScopeCompleted", false);
            Scribe_Values.Look(
                ref bedroomCausePlanningScopeAuthorityGeneration,
                "CA_agentBedroomCausePlanningScopeAuthorityGeneration", -1);
            if (bedroomCauseNativeResourceJobEvidence == null)
                bedroomCauseNativeResourceJobEvidence = new List<string>();
        }

        public override void LoadedGame()
        {
            loadedSaveName = ConsumeNativeLoadSaveName();
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(Find.CurrentMap);
            activeCheckpointReloadObserved =
                !activeCheckpointAuthorizationId.NullOrEmpty()
                && !activeCheckpointAuthorizationOrigin.NullOrEmpty()
                && !activeCheckpointSaveName.NullOrEmpty()
                && loadedSaveName == activeCheckpointSaveName
                && activeCheckpointSavedTick >= 0
                && lifecycle != null
                && lifecycle.HasActiveAuthorizedRelocation
                && lifecycle.CurrentAuthorizationIdForVerification
                    == activeCheckpointAuthorizationId
                && lifecycle.CurrentAuthorizationOriginForVerification
                    == activeCheckpointAuthorizationOrigin;
        }

        public override void StartedNewGame()
        {
            loadedSaveName = null;
            ClearActiveCheckpoint();
        }

        internal static void CaptureNativeLoadSaveName(string saveName)
        {
            pendingNativeLoadSaveName = saveName;
        }

        internal bool LoadedSaveMatchesForVerification(string saveName)
        {
            return !saveName.NullOrEmpty() && loadedSaveName == saveName;
        }

        private static string ConsumeNativeLoadSaveName()
        {
            string saveName = pendingNativeLoadSaveName;
            pendingNativeLoadSaveName = null;
            return saveName;
        }

        internal void ArmActiveCheckpoint(string authorizationId,
            string authorizationOrigin, string saveName, int tick)
        {
            activeCheckpointAuthorizationId = authorizationId;
            activeCheckpointAuthorizationOrigin = authorizationOrigin;
            activeCheckpointSaveName = saveName;
            activeCheckpointSavedTick = tick;
            activeCheckpointReloadObserved = false;
        }

        internal void ClearActiveCheckpoint()
        {
            activeCheckpointAuthorizationId = null;
            activeCheckpointAuthorizationOrigin = null;
            activeCheckpointSaveName = null;
            activeCheckpointSavedTick = -1;
            activeCheckpointReloadObserved = false;
        }

        internal bool TryGetBedroomCauseMaterialAudit(int programId,
            int targetResidentId, out string originThingId,
            out int requiredMaterialCount)
        {
            originThingId = bedroomCauseMaterialAuditOriginId;
            requiredMaterialCount = bedroomCauseRequiredMaterialCount;
            return !originThingId.NullOrEmpty()
                && requiredMaterialCount > 0
                && bedroomCauseMaterialAuditProgramId == programId
                && bedroomCauseMaterialAuditTargetResidentId
                    == targetResidentId;
        }

        internal bool HasBedroomCauseMaterialAudit =>
            !bedroomCauseMaterialAuditOriginId.NullOrEmpty();

        internal int BedroomCauseWoodCountAtAuditStart =>
            bedroomCauseWoodCountAtAuditStart;

        internal string BedroomCauseConsumedMaterialEvidence =>
            bedroomCauseConsumedMaterialEvidence;

        internal int BedroomCauseWoodCountBeforeFrameCompletion =>
            bedroomCauseWoodCountBeforeFrameCompletion;

        internal int BedroomCauseWoodCountAfterFrameCompletion =>
            bedroomCauseWoodCountAfterFrameCompletion;

        internal int BedroomCauseFrameWoodDelta =>
            bedroomCauseWoodCountBeforeFrameCompletion >= 0
                && bedroomCauseWoodCountAfterFrameCompletion >= 0
                ? bedroomCauseWoodCountBeforeFrameCompletion
                    - bedroomCauseWoodCountAfterFrameCompletion
                : -1;

        internal string BedroomCauseCompletionJobEvidence =>
            bedroomCauseCompletionJobEvidence ?? "none";

        internal int BedroomCauseNativeResourceJobCount =>
            bedroomCauseNativeResourceJobEvidence?.Count ?? 0;

        internal bool BedroomCauseObservedForcedResourceJob =>
            bedroomCauseObservedForcedResourceJob;

        internal string BedroomCausePlanningScopeEvidence =>
            bedroomCausePlanningScopeEvidence ?? "none";

        internal bool HasActiveBedroomCausePlanningScope =>
            bedroomCausePlanningScopeActive;

        internal bool RecordBedroomCausePlanningScope(Map map,
            int programId, int targetResidentId, string originThingId,
            bool requiresScopedLease)
        {
            if (bedroomCausePlanningScopeActive)
                return false;
            bedroomCausePlanningScopeMapId = map?.uniqueID ?? -1;
            bedroomCausePlanningScopeProgramId = programId;
            bedroomCausePlanningScopeTargetResidentId = targetResidentId;
            bedroomCausePlanningScopeOriginId = originThingId;
            bedroomCausePlanningScopeRequiresLease = requiresScopedLease;
            bedroomCausePlanningScopeActive = map != null && programId > 0
                && targetResidentId > 0;
            bedroomCausePlanningScopeCompleted = false;
            bedroomCausePlanningScopeAuthorityGeneration =
                AwarenessMod.Settings?
                    .autonomousHomePlanningResetGeneration ?? -1;
            bedroomCausePlanningScopeEvidence = requiresScopedLease
                ? "exact map/Bedroom-cause lease active; global Home planning remained disabled"
                : "exact Bedroom cause tracked; already-enabled global Home planning was not changed";
            bedroomCausePlanningScopeEvidence += "; authority generation "
                + bedroomCausePlanningScopeAuthorityGeneration;
            return bedroomCausePlanningScopeActive;
        }

        internal bool AllowsBedroomCausePlanningLease(Map map)
        {
            return bedroomCausePlanningScopeActive
                && bedroomCausePlanningScopeRequiresLease
                && map != null
                && bedroomCausePlanningScopeMapId == map.uniqueID
                && BedroomCausePlanningAuthorityMatches;
        }

        internal bool HasActiveBedroomCausePlanningScopeForMap(Map map)
        {
            return bedroomCausePlanningScopeActive && map != null
                && bedroomCausePlanningScopeMapId == map.uniqueID;
        }

        internal bool BedroomCausePlanningScopeMatches(Map map,
            int programId, int targetResidentId, string originThingId)
        {
            return BedroomCausePlanningScopeIdentityMatches(map, programId,
                    targetResidentId, originThingId)
                && BedroomCausePlanningAuthorityMatches;
        }

        internal bool CompletedBedroomCausePlanningScopeMatches(Map map,
            int programId, int targetResidentId, string originThingId)
        {
            return bedroomCausePlanningScopeCompleted
                && !bedroomCausePlanningScopeActive
                && BedroomCausePlanningScopeFieldsMatch(map, programId,
                    targetResidentId, originThingId);
        }

        private bool BedroomCausePlanningScopeIdentityMatches(Map map,
            int programId, int targetResidentId, string originThingId)
        {
            return bedroomCausePlanningScopeActive
                && BedroomCausePlanningScopeFieldsMatch(map, programId,
                    targetResidentId, originThingId);
        }

        private bool BedroomCausePlanningScopeFieldsMatch(Map map,
            int programId, int targetResidentId, string originThingId)
        {
            return map != null
                && bedroomCausePlanningScopeMapId == map.uniqueID
                && bedroomCausePlanningScopeProgramId == programId
                && bedroomCausePlanningScopeTargetResidentId
                    == targetResidentId
                && !originThingId.NullOrEmpty()
                && bedroomCausePlanningScopeOriginId == originThingId;
        }

        private bool BedroomCausePlanningAuthorityMatches =>
            AwarenessMod.Settings != null
            && bedroomCausePlanningScopeAuthorityGeneration
                == AwarenessMod.Settings
                    .autonomousHomePlanningResetGeneration
            && AwarenessMod.Settings.autonomousHomePlanning
                == !bedroomCausePlanningScopeRequiresLease;

        internal void TransferBedroomCausePlanningScopeToConstruction(
            Map map, int programId, int targetResidentId,
            string originThingId)
        {
            if (!bedroomCausePlanningScopeActive || map == null
                || bedroomCausePlanningScopeMapId != map.uniqueID
                || bedroomCausePlanningScopeProgramId != programId
                || bedroomCausePlanningScopeTargetResidentId
                    != targetResidentId
                || !bedroomCausePlanningScopeOriginId.NullOrEmpty()
                || originThingId.NullOrEmpty())
                return;
            bedroomCausePlanningScopeOriginId = originThingId;
            bedroomCausePlanningScopeEvidence +=
                "; transferred to exact native construction "
                + originThingId;
        }

        internal void CompleteBedroomCausePlanningScope(Map map,
            int programId, int targetResidentId, string originThingId)
        {
            if (!BedroomCausePlanningScopeIdentityMatches(map, programId,
                    targetResidentId, originThingId))
                return;
            AutonomousHomeMapComponent.For(map)?
                .DeferPlanningForBedroomCauseVerification();
            bool authorityPreserved =
                BedroomCausePlanningAuthorityMatches;
            bedroomCausePlanningScopeActive = false;
            bedroomCausePlanningScopeCompleted = authorityPreserved;
            bedroomCausePlanningScopeEvidence += authorityPreserved
                ? "; exact cause completed and lease retired; global Home planning left "
                    + (AwarenessMod.Settings?.autonomousHomePlanning == true
                        ? "enabled" : "disabled") + " without mutation"
                : "; exact cause completed but the lease proof was invalidated by a later Home-planning authority state";
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        }

        internal void RetireBedroomCausePlanningScope(Map map,
            int programId, int targetResidentId, string originThingId,
            string reason)
        {
            if (!bedroomCausePlanningScopeActive || map == null
                || bedroomCausePlanningScopeMapId != map.uniqueID
                || bedroomCausePlanningScopeProgramId != programId
                || bedroomCausePlanningScopeTargetResidentId
                    != targetResidentId
                || (originThingId.NullOrEmpty()
                    ? !bedroomCausePlanningScopeOriginId.NullOrEmpty()
                    : bedroomCausePlanningScopeOriginId != originThingId))
                return;
            RetireBedroomCausePlanningScope(reason);
        }

        internal void RetireBedroomCausePlanningScopeForMap(Map map,
            string reason)
        {
            if (!bedroomCausePlanningScopeActive || map == null
                || bedroomCausePlanningScopeMapId != map.uniqueID)
                return;
            RetireBedroomCausePlanningScope(reason);
        }

        private void RetireBedroomCausePlanningScope(string reason)
        {
            bedroomCausePlanningScopeActive = false;
            bedroomCausePlanningScopeCompleted = false;
            bedroomCausePlanningScopeEvidence += "; lease retired without "
                + "changing global Home planning: "
                + (reason ?? "exact cause ended");
        }

        internal string BedroomCauseNativeResourceJobEvidence =>
            bedroomCauseNativeResourceJobEvidence.NullOrEmpty()
                ? "none"
                : string.Join(" | ", bedroomCauseNativeResourceJobEvidence);

        internal void ArmBedroomCauseMaterialAudit(int programId,
            int targetResidentId, string originThingId,
            int requiredMaterialCount, int woodCountAtAuditStart)
        {
            bedroomCauseMaterialAuditProgramId = programId;
            bedroomCauseMaterialAuditTargetResidentId = targetResidentId;
            bedroomCauseMaterialAuditOriginId = originThingId;
            bedroomCauseRequiredMaterialCount = requiredMaterialCount;
            bedroomCauseWoodCountAtAuditStart = woodCountAtAuditStart;
            bedroomCauseConsumingFrameId = null;
            bedroomCauseConsumedMaterialEvidence = null;
            bedroomCauseConsumedMaterialCount = 0;
            bedroomCauseWoodCountBeforeFrameCompletion = -1;
            bedroomCauseWoodCountAfterFrameCompletion = -1;
            bedroomCauseCompletionJobDefName = null;
            bedroomCauseCompletionJobEvidence = null;
            bedroomCauseCompletionJobTargetMatched = false;
            bedroomCauseObservedForcedCompletionJob = false;
            bedroomCauseNativeResourceJobEvidence.Clear();
            bedroomCauseObservedForcedResourceJob = false;
        }

        internal void ObserveBedroomCauseNativeResourceJobStart(
            JobDriver driver)
        {
            Pawn pawn = driver?.pawn;
            Job job = driver?.job;
            Thing construction = job?.targetC.Thing;
            if (construction == null || pawn == null || job == null
                || job.def != JobDefOf.HaulToContainer
                || bedroomCauseMaterialAuditOriginId.NullOrEmpty()
                || bedroomCauseMaterialAuditProgramId <= 0
                || bedroomCauseMaterialAuditTargetResidentId <= 0
                || AutonomousHomeMapComponent.For(construction.Map)?
                    .PendingPlanMatchesForVerification(
                        bedroomCauseMaterialAuditProgramId,
                        bedroomCauseMaterialAuditTargetResidentId,
                        construction) != true)
                return;
            bedroomCauseObservedForcedResourceJob |= job.playerForced;
            if (bedroomCauseNativeResourceJobEvidence.Count >= 32) return;
            string sources = job.targetA.Thing?.ThingID ?? "none";
            if (!job.targetQueueA.NullOrEmpty())
                sources += "," + string.Join(",", job.targetQueueA
                    .Select(target => target.Thing?.ThingID ?? "none"));
            bedroomCauseNativeResourceJobEvidence.Add(
                pawn.LabelShort + " #" + pawn.thingIDNumber + " started "
                + sources
                + " count " + job.count + " toward tracked construction "
                + construction.ThingID + "; job.playerForced "
                + job.playerForced + "; start observation only, not transfer attribution");
        }

        internal void ConfirmBedroomCauseMaterialConsumption(int programId,
            int targetResidentId, string originThingId,
            string consumingFrameThingId, string consumedMaterialEvidence,
            int consumedMaterialCount, int woodCountImmediatelyBefore,
            Pawn completingWorker)
        {
            if (bedroomCauseMaterialAuditProgramId != programId
                || bedroomCauseMaterialAuditTargetResidentId
                    != targetResidentId
                || bedroomCauseMaterialAuditOriginId != originThingId
                || consumedMaterialEvidence.NullOrEmpty()
                || bedroomCauseRequiredMaterialCount != consumedMaterialCount
                || woodCountImmediatelyBefore < consumedMaterialCount)
                return;
            bedroomCauseConsumingFrameId = consumingFrameThingId;
            bedroomCauseConsumedMaterialEvidence =
                consumedMaterialEvidence;
            bedroomCauseConsumedMaterialCount = consumedMaterialCount;
            bedroomCauseWoodCountBeforeFrameCompletion =
                woodCountImmediatelyBefore;
            bedroomCauseWoodCountAfterFrameCompletion = -1;
            Job completingJob = completingWorker?.CurJob;
            bedroomCauseCompletionJobDefName = completingJob?.def?.defName;
            bedroomCauseCompletionJobTargetMatched =
                completingJob?.targetA.Thing?.ThingID
                    == consumingFrameThingId;
            bedroomCauseObservedForcedCompletionJob =
                completingJob?.playerForced == true;
            bedroomCauseCompletionJobEvidence = completingWorker == null
                ? "no completing worker"
                : completingWorker.LabelShort + " #"
                    + completingWorker.thingIDNumber + " invoked "
                    + consumingFrameThingId + " completion through job "
                    + (bedroomCauseCompletionJobDefName ?? "none")
                    + "; targetA exact frame "
                    + bedroomCauseCompletionJobTargetMatched
                    + "; job.playerForced "
                    + bedroomCauseObservedForcedCompletionJob;
        }

        internal void CompleteBedroomCauseMaterialConsumption(Map map,
            string consumingFrameThingId)
        {
            if (map == null || consumingFrameThingId.NullOrEmpty()
                || bedroomCauseConsumingFrameId != consumingFrameThingId
                || bedroomCauseWoodCountBeforeFrameCompletion < 0)
                return;
            bedroomCauseWoodCountAfterFrameCompletion =
                CADebugActions.CountMapThingsRecursivelyForAudit(map,
                    ThingDefOf.WoodLog);
        }

        internal bool BedroomCauseMaterialConsumptionMatches(int programId,
            int targetResidentId, string originThingId,
            string consumingFrameThingId, string consumedMaterialEvidence,
            int consumedMaterialCount)
        {
            return bedroomCauseMaterialAuditProgramId == programId
                && bedroomCauseMaterialAuditTargetResidentId
                    == targetResidentId
                && bedroomCauseMaterialAuditOriginId == originThingId
                && bedroomCauseRequiredMaterialCount == consumedMaterialCount
                && bedroomCauseWoodCountAtAuditStart
                    >= bedroomCauseRequiredMaterialCount
                && bedroomCauseConsumingFrameId == consumingFrameThingId
                && bedroomCauseConsumedMaterialEvidence
                    == consumedMaterialEvidence
                && bedroomCauseConsumedMaterialCount == consumedMaterialCount
                && bedroomCauseWoodCountBeforeFrameCompletion >= 0
                && bedroomCauseWoodCountAfterFrameCompletion >= 0
                && bedroomCauseWoodCountBeforeFrameCompletion
                    - bedroomCauseWoodCountAfterFrameCompletion
                    == consumedMaterialCount
                && bedroomCauseCompletionJobDefName
                    == JobDefOf.FinishFrame.defName
                && !bedroomCauseCompletionJobEvidence.NullOrEmpty()
                && bedroomCauseCompletionJobTargetMatched
                && !bedroomCauseObservedForcedCompletionJob;
        }

        internal void RetireBedroomCauseMaterialAudit(
            int programId, int targetResidentId, string originThingId)
        {
            if (bedroomCauseMaterialAuditProgramId != programId
                || bedroomCauseMaterialAuditTargetResidentId
                    != targetResidentId
                || bedroomCauseMaterialAuditOriginId != originThingId)
                return;
            bedroomCauseMaterialAuditOriginId = null;
            bedroomCauseMaterialAuditProgramId = 0;
            bedroomCauseMaterialAuditTargetResidentId = 0;
            bedroomCauseRequiredMaterialCount = 0;
            bedroomCauseWoodCountAtAuditStart = -1;
            bedroomCauseConsumingFrameId = null;
            bedroomCauseConsumedMaterialEvidence = null;
            bedroomCauseConsumedMaterialCount = 0;
            bedroomCauseWoodCountBeforeFrameCompletion = -1;
            bedroomCauseWoodCountAfterFrameCompletion = -1;
            bedroomCauseCompletionJobDefName = null;
            bedroomCauseCompletionJobEvidence = null;
            bedroomCauseCompletionJobTargetMatched = false;
            bedroomCauseObservedForcedCompletionJob = false;
            bedroomCauseNativeResourceJobEvidence.Clear();
            bedroomCauseObservedForcedResourceJob = false;
        }

        internal bool ReloadedCheckpointMatches(string authorizationId,
            string authorizationOrigin, out string receipt)
        {
            bool matches = activeCheckpointReloadObserved
                && !authorizationId.NullOrEmpty()
                && activeCheckpointAuthorizationId == authorizationId
                && !authorizationOrigin.NullOrEmpty()
                && activeCheckpointAuthorizationOrigin == authorizationOrigin
                && !activeCheckpointSaveName.NullOrEmpty()
                && loadedSaveName == activeCheckpointSaveName
                && activeCheckpointSavedTick >= 0;
            receipt = matches
                ? "reloaded active checkpoint for authorization "
                    + activeCheckpointAuthorizationId + " saved at tick "
                    + activeCheckpointSavedTick + " from "
                    + activeCheckpointSaveName
                : "no matching reloaded active checkpoint";
            return matches;
        }

        public override void GameComponentUpdate()
        {
            if (!Prefs.DevMode || Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap == null
                || Time.realtimeSinceStartup < nextPollRealtime)
                return;
            nextPollRealtime = Time.realtimeSinceStartup + 0.5f;

            string commandPath = Path.Combine(GenFilePaths.DevOutputFolderPath,
                CommandFileName);
            if (!File.Exists(commandPath)) return;

            string receiptPath = Path.Combine(GenFilePaths.DevOutputFolderPath,
                ReceiptFileName);
            bool debugLogWasOpen = Find.WindowStack.IsOpen(
                typeof(EditWindow_Log));
            bool priorOpenOnMessage = Log.openOnMessage;
            Log.openOnMessage = false;
            EditWindow_Log.wantsToOpen = false;
            string receipt;
            try
            {
                string command = File.ReadAllText(commandPath).Trim();
                File.Delete(commandPath);
                string commandName;
                string targetFailure;
                if (!TryResolveCommandTarget(command, out commandName,
                    out targetFailure))
                {
                    receipt = targetFailure;
                }
                else switch (commandName)
                {
                    case "agent-close-debug-log":
                        EditWindow_Log.wantsToOpen = false;
                        bool removed = Find.WindowStack.TryRemove(
                            typeof(EditWindow_Log), doCloseSound: false);
                        receipt = "[CA] agent receipt: debug log "
                            + (removed ? "closed" : "was not open")
                            + "; no game window geometry or focus changed";
                        break;
                    case "operational-access-census":
                        receipt = CADebugActions.OperationalAccessReceipt();
                        break;
                    case "inventory-storage-receipt":
                        receipt = CADebugActions.InventoryStorageReceipt();
                        break;
                    case "inventory-storage-focus":
                        receipt = CADebugActions.FocusInventoryStorageReceipt();
                        break;
                    case "inventory-storage-run":
                        receipt = CADebugActions.RunInventoryStorageReceipt();
                        break;
                    case "inventory-armory-run":
                        receipt = CADebugActions.RunArmoryStorageReceipt();
                        break;
                    case "home-planning-receipt":
                        receipt = CADebugActions.RunHomePlanningReceipt();
                        break;
                    case "home-storage-receipt":
                        receipt = CADebugActions.RunHomeStorageReceipt();
                        break;
                    case "storage-veto-receipt":
                        receipt = CADebugActions.RunStorageVetoReceipt();
                        break;
                    case "storage-edit-receipt":
                        receipt = CADebugActions.RunStorageEditReceipt();
                        break;
                    case "storage-flow-start":
                        receipt = CADebugActions.StartStorageFlowReceipt();
                        break;
                    case "storage-flow-census":
                        receipt = CADebugActions.StorageFlowCensusReceipt();
                        break;
                    case "storage-persistence-save":
                        receipt = CADebugActions.SaveStoragePersistenceReceipt();
                        break;
                    case "storage-persistence-load":
                        receipt = CADebugActions.LoadStoragePersistenceReceipt();
                        break;
                    case "storage-persistence-census":
                        receipt = CADebugActions.CensusStoragePersistenceReceipt();
                        break;
                    case "settlement-planning-context":
                        receipt = CADebugActions
                            .SettlementPlanningContextReceipt();
                        break;
                    case "facility-siting-census":
                        receipt = CADebugActions.FacilitySitingReceipt();
                        break;
                    case "facility-requirement-evaluation":
                        receipt = CADebugActions.FacilityRequirementReceipt();
                        break;
                    case "spatial-furnishing-evaluation":
                        receipt = CADebugActions.SpatialFurnishingReceipt();
                        break;
                    case "bedroom-requirement-evaluation":
                        receipt = CADebugActions.BedroomRequirementReceipt();
                        break;
                    case "bedroom-construction-cause-evaluation":
                        receipt = CADebugActions
                            .BedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-requirement-prepare-controlled":
                        receipt = CADebugActions
                            .PrepareControlledBedroomRequirementReceipt();
                        break;
                    case "bedroom-requirement-save-controlled":
                        receipt = CADebugActions
                            .SaveControlledBedroomRequirementReceipt();
                        break;
                    case "bedroom-requirement-load-controlled":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .BedroomRequirementControlledSaveName);
                        break;
                    case "bedroom-requirement-prepare-developed":
                        receipt = CADebugActions
                            .PrepareDevelopedBedroomRequirementReceipt();
                        break;
                    case "bedroom-construction-cause-prepare-unmet":
                        receipt = CADebugActions
                            .PrepareControlledBedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-construction-cause-save-unmet":
                        receipt = CADebugActions
                            .SaveControlledBedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-construction-cause-load-unmet":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .BedroomConstructionCauseUnmetSaveName,
                            exactTargetAndCurrentGuard: true);
                        break;
                    case "bedroom-construction-cause-functional-control":
                        receipt = CADebugActions
                            .BedroomConstructionFunctionalControlReceipt();
                        break;
                    case "bedroom-construction-cause-ceilings-regression":
                        receipt = CADebugActions
                            .BedroomConstructionCeilingsRegressionReceipt();
                        break;
                    case "bedroom-construction-cause-player-precedence-regression":
                        receipt = CADebugActions
                            .BedroomConstructionPlayerPrecedenceReceipt();
                        break;
                    case "bedroom-construction-cause-start":
                        receipt = CADebugActions
                            .StartBedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-construction-cause-save-active":
                        receipt = CADebugActions
                            .SaveActiveBedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-construction-cause-load-active":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .BedroomConstructionCauseActiveSaveName,
                            exactTargetAndCurrentGuard: true);
                        break;
                    case "bedroom-construction-cause-reconcile-active":
                        receipt = CADebugActions
                            .ReconcileActiveBedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-construction-cause-run-native":
                        receipt = CADebugActions
                            .RunNativeBedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-construction-cause-completion":
                        receipt = CADebugActions
                            .CompletedBedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-construction-cause-save-completed":
                        receipt = CADebugActions
                            .SaveCompletedBedroomConstructionCauseReceipt();
                        break;
                    case "bedroom-construction-cause-load-completed":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .BedroomConstructionCauseCompletedSaveName,
                            exactTargetAndCurrentGuard: true);
                        break;
                    case "animal-infrastructure-evaluation":
                        receipt = CADebugActions
                            .AnimalInfrastructureReceipt();
                        break;
                    case "critical-storage-evaluation":
                        receipt = CADebugActions
                            .CriticalStorageEvidenceReceipt();
                        break;
                    case "spatial-initiative-census":
                        receipt = CADebugActions.SpatialInitiativeReceipt();
                        break;
                    case "combat-topology-regression-receipt":
                        receipt = CACombatSpatialLogComponent
                            .TopologyRegressionReceipt();
                        break;
                    case "combat-topology-export":
                        receipt = CACombatSpatialLogComponent
                            .TopologyExportReceipt()
                            + "\n" + CACombatFlightRecorder.ExportReceipt();
                        break;
                    case "combat-flight-export":
                        receipt = CACombatFlightRecorder.ExportReceipt();
                        break;
                    case "welfare-memory-regression-receipt":
                        receipt = CAClinicalObservation
                            .MemoryStabilityContractReceipt();
                        break;
                    case "spatial-initiative-mountain-start":
                        receipt = CADebugActions
                            .StartSpatialInitiativeMountainReceipt();
                        break;
                    case "spatial-initiative-aboveground-start":
                        receipt = CADebugActions
                            .StartSpatialInitiativeAbovegroundReceipt();
                        break;
                    case "spatial-initiative-save-preplacement":
                        receipt = CADebugActions
                            .SaveSpatialShelfPreplacementReceipt();
                        break;
                    case "spatial-initiative-save-working":
                        receipt = CADebugActions
                            .SaveSpatialInitiativeWorkingReceipt();
                        break;
                    case "spatial-room-load-authored-barracks":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .SpatialAuthoredBarracksSaveName);
                        break;
                    case "spatial-room-load-preplacement":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .SpatialRoomPreplacementSaveName);
                        break;
                    case "spatial-room-save-preplacement":
                        receipt = CADebugActions
                            .SaveSpatialRoomPreplacementReceipt();
                        break;
                    case "spatial-room-barracks-start":
                        receipt = CADebugActions
                            .StartSpatialRoomBarracksReceipt();
                        break;
                    case "spatial-room-save-working":
                        receipt = CADebugActions
                            .SaveSpatialRoomWorkingReceipt();
                        break;
                    case "spatial-room-load-working":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .SpatialRoomWorkingSaveName);
                        break;
                    case "spatial-room-dolly-restore-start":
                        receipt = CADebugActions
                            .RestoreDollyAndStartEighthBedReceipt();
                        break;
                    case "spatial-room-dolly-bed-census":
                        receipt = CADebugActions.DollyBarracksCensusReceipt();
                        break;
                    case "spatial-room-dolly-save-preplacement":
                        receipt = CADebugActions
                            .SaveDollyBarracksPreplacementReceipt();
                        break;
                    case "spatial-room-dolly-load-preplacement":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .SpatialRoomDollyPreplacementSaveName);
                        break;
                    case "spatial-room-dolly-facilities-start":
                        receipt = CADebugActions
                            .StartDollyBarracksFacilitiesReceipt();
                        break;
                    case "spatial-room-dolly-save-working":
                        receipt = CADebugActions
                            .SaveDollyBarracksWorkingReceipt();
                        break;
                    case "spatial-room-dolly-load-working":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .SpatialRoomDollyWorkingSaveName);
                        break;
                    case "spatial-room-authority-player-intent-regression":
                        receipt = CADebugActions
                            .DollyPlayerIntentAuthorityRegressionReceipt();
                        break;
                    case "spatial-room-authority-pending-regression":
                        receipt = CADebugActions
                            .DollyPendingAuthorityRegressionReceipt();
                        break;
                    case "spatial-room-authority-completed-regression":
                        receipt = CADebugActions
                            .DollyCompletedAuthorityRegressionReceipt();
                        break;
                    case "spatial-initiative-load-preplacement":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions
                                .SpatialShelfPreplacementSaveName);
                        break;
                    case "spatial-furnishing-load-controlled":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions.SpatialControlledSaveName);
                        break;
                    case "spatial-furnishing-load-developed-mountain":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName,
                            CADebugActions.SpatialDevelopedMountainSaveName);
                        break;
                    case "spatial-furnishing-load-aboveground":
                        receipt = CADebugActions.LoadSpatialObservationReceipt(
                            commandName, CADebugActions.SpatialAbovegroundSaveName);
                        break;
                    case "controlled-kitchen-requirement-receipt":
                        receipt = CADebugActions
                            .ControlledKitchenRequirementReceipt();
                        break;
                    case "toxic-waste-lifecycle-receipt":
                        receipt = CADebugActions.ToxicWasteLifecycleReceipt();
                        break;
                    case "toxic-waste-arrival-receipt":
                        receipt = CADebugActions.ToxicWasteArrivalReceipt();
                        break;
                    case "toxic-waste-arrival-fixture-start":
                        receipt = CADebugActions
                            .StartToxicWasteArrivalFixtureReceipt();
                        break;
                    case "toxic-waste-arrival-save-working":
                        receipt = CADebugActions
                            .SaveToxicWasteArrivalWorkingReceipt();
                        break;
                    case "toxic-waste-arrival-load-working":
                        receipt = CADebugActions
                            .LoadToxicWasteArrivalWorkingReceipt();
                        break;
                    case "toxic-waste-relocation-isolate-hostile":
                        receipt = CADebugActions
                            .IsolateToxicWasteRelocationFixtureReceipt();
                        break;
                    case "toxic-waste-relocation-save-working":
                        receipt = CADebugActions
                            .SaveToxicWasteRelocationWorkingReceipt();
                        break;
                    case "toxic-waste-relocation-load-working":
                        receipt = CADebugActions
                            .LoadToxicWasteRelocationWorkingReceipt();
                        break;
                    case "toxic-waste-relocation-fixture-start":
                        receipt = CADebugActions
                            .StartToxicWasteRelocationFixtureReceipt();
                        break;
                    case "toxic-waste-relocation-save-active":
                        receipt = CADebugActions
                            .SaveActiveToxicWasteRelocationReceipt();
                        break;
                    case "toxic-waste-relocation-resume":
                        receipt = CADebugActions
                            .ResumeToxicWasteRelocationReceipt();
                        break;
                    case "toxic-waste-relocation-load-active":
                        receipt = CADebugActions
                            .LoadActiveToxicWasteRelocationCheckpointReceipt();
                        break;
                    case "toxic-waste-relocation-save-completed":
                        receipt = CADebugActions
                            .SaveCompletedToxicWasteRelocationReceipt();
                        break;
                    case "toxic-waste-relocation-save-resumed":
                        receipt = CADebugActions
                            .SaveResumedToxicWasteRelocationReceipt();
                        break;
                    case "context-bed-ranking-receipt":
                        receipt = CADebugActions
                            .RunSettlementContextBedRankingReceipt();
                        break;
                    case "barracks-composition-receipt":
                        receipt = CADebugActions
                            .MarkedBarracksCompositionReceipt();
                        break;
                    case "settlement-context-persistence-save":
                        receipt = CADebugActions
                            .SaveSettlementPlanningContextReceipt();
                        break;
                    case "settlement-context-persistence-load":
                        receipt = CADebugActions
                            .LoadSettlementPlanningContextReceipt();
                        break;
                    case "settlement-context-persistence-census":
                        receipt = CADebugActions
                            .CensusSettlementPlanningContextPersistenceReceipt();
                        break;
                    default:
                        receipt = "[CA] agent receipt: unknown command '"
                            + commandName + "'";
                        break;
                }
            }
            catch (Exception exception)
            {
                receipt = "[CA] agent receipt: command failed: " + exception;
            }

            try
            {
                Directory.CreateDirectory(GenFilePaths.DevOutputFolderPath);
                File.WriteAllText(receiptPath, receipt);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("[CA] agent receipt file failure: "
                    + exception);
            }
            Log.openOnMessage = priorOpenOnMessage;
            EditWindow_Log.wantsToOpen = false;
            if (!debugLogWasOpen)
                Find.WindowStack.TryRemove(typeof(EditWindow_Log),
                    doCloseSound: false);
        }

        private bool TryResolveCommandTarget(string command,
            out string commandName, out string failure)
        {
            string[] parts = command.Split('|');
            commandName = parts[0].Trim();
            string expectedHash = parts.Length > 1 ? parts[1].Trim() : null;
            string expectedCurrentHash = parts.Length > 2
                ? parts[2].Trim() : null;
            bool exactCheckpointLoad = commandName
                    == "bedroom-construction-cause-load-unmet"
                || commandName == "bedroom-construction-cause-load-active"
                || commandName == "bedroom-construction-cause-load-completed";
            if (exactCheckpointLoad)
            {
                if (parts.Length != 3 || !ValidSha256(expectedHash)
                    || !ValidSha256(expectedCurrentHash))
                {
                    failure = "[CA] agent receipt: " + commandName + "\n"
                        + "refused; exact checkpoint loads require target-save and current-save SHA256 values as command|targetSHA|currentSHA";
                    return false;
                }
                string targetSaveName = commandName.EndsWith("load-unmet",
                        StringComparison.Ordinal)
                    ? CADebugActions.BedroomConstructionCauseUnmetSaveName
                    : commandName.EndsWith("load-active",
                        StringComparison.Ordinal)
                        ? CADebugActions.BedroomConstructionCauseActiveSaveName
                        : CADebugActions
                            .BedroomConstructionCauseCompletedSaveName;
                string targetPath = GenFilePaths.FilePathForSavedGame(
                    targetSaveName);
                if (!File.Exists(targetPath))
                {
                    failure = "[CA] agent receipt: " + commandName + "\n"
                        + "refused; exact target checkpoint is absent: "
                        + targetPath;
                    return false;
                }
                string targetHash = BridgeFileSha256(targetPath);
                if (!targetHash.Equals(expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failure = "[CA] agent receipt: " + commandName + "\n"
                        + "refused; expected target-save SHA256 "
                        + expectedHash.ToUpperInvariant() + " but "
                        + targetSaveName + " is " + targetHash;
                    return false;
                }
                if (loadedSaveName.NullOrEmpty())
                {
                    failure = "[CA] agent receipt: " + commandName + "\n"
                        + "refused; the current loaded-save identity is unavailable, so discarding live state is not authorized";
                    return false;
                }
                string currentPath = GenFilePaths.FilePathForSavedGame(
                    loadedSaveName);
                if (!File.Exists(currentPath))
                {
                    failure = "[CA] agent receipt: " + commandName + "\n"
                        + "refused; current loaded save is absent: "
                        + currentPath;
                    return false;
                }
                string currentHash = BridgeFileSha256(currentPath);
                if (!currentHash.Equals(expectedCurrentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failure = "[CA] agent receipt: " + commandName + "\n"
                        + "refused; expected current-save SHA256 "
                        + expectedCurrentHash.ToUpperInvariant() + " but "
                        + loadedSaveName + " is " + currentHash
                        + "; live-state replacement was not authorized";
                    return false;
                }
                failure = null;
                return true;
            }
            bool exactSaveTargetRequired = commandName.StartsWith(
                    "toxic-waste-relocation-", StringComparison.Ordinal)
                || commandName == "toxic-waste-arrival-fixture-start"
                || commandName == "toxic-waste-arrival-save-working"
                || commandName == "toxic-waste-arrival-load-working"
                || commandName == "spatial-initiative-mountain-start"
                || commandName == "spatial-initiative-aboveground-start"
                || commandName == "spatial-initiative-save-preplacement"
                || commandName == "spatial-initiative-save-working"
                || commandName == "spatial-room-save-preplacement"
                || commandName == "spatial-room-barracks-start"
                || commandName == "spatial-room-save-working"
                || commandName == "spatial-room-dolly-restore-start"
                || commandName == "spatial-room-dolly-save-preplacement"
                || commandName == "spatial-room-dolly-facilities-start"
                || commandName == "spatial-room-dolly-save-working"
                || commandName == "bedroom-requirement-prepare-controlled"
                || commandName == "bedroom-requirement-save-controlled"
                || commandName == "bedroom-requirement-prepare-developed"
                || commandName
                    == "bedroom-construction-cause-prepare-unmet"
                || commandName
                    == "bedroom-construction-cause-save-unmet"
                || commandName
                    == "bedroom-construction-cause-functional-control"
                || commandName
                    == "bedroom-construction-cause-ceilings-regression"
                || commandName
                    == "bedroom-construction-cause-player-precedence-regression"
                || commandName == "bedroom-construction-cause-start"
                || commandName == "bedroom-construction-cause-save-active"
                || commandName
                    == "bedroom-construction-cause-reconcile-active"
                || commandName == "bedroom-construction-cause-run-native"
                || commandName == "bedroom-construction-cause-completion"
                || commandName
                    == "bedroom-construction-cause-save-completed"
                || commandName
                    == "spatial-room-authority-player-intent-regression"
                || commandName
                    == "spatial-room-authority-pending-regression"
                || commandName
                    == "spatial-room-authority-completed-regression";
            if (!exactSaveTargetRequired)
            {
                failure = null;
                return true;
            }
            if (parts.Length != 2 || !ValidSha256(expectedHash))
            {
                failure = "[CA] agent receipt: " + commandName + "\n"
                    + "refused; mutating fixture commands require the exact "
                    + "loaded-save SHA256 after '|'";
                return false;
            }

            string saveName = loadedSaveName;
            if (saveName.NullOrEmpty())
            {
                failure = "[CA] agent receipt: " + commandName + "\n"
                    + "refused; the loaded save identity is unavailable";
                return false;
            }
            string path = GenFilePaths.FilePathForSavedGame(saveName);
            if (!File.Exists(path))
            {
                failure = "[CA] agent receipt: " + commandName + "\n"
                    + "refused; loaded save file is absent: " + path;
                return false;
            }
            string actualHash = BridgeFileSha256(path);
            if (!actualHash.Equals(expectedHash,
                StringComparison.OrdinalIgnoreCase))
            {
                failure = "[CA] agent receipt: " + commandName + "\n"
                    + "refused; expected loaded-save SHA256 "
                    + expectedHash.ToUpperInvariant() + " but " + saveName
                    + " is " + actualHash;
                return false;
            }
            failure = null;
            return true;
        }

        private static bool ValidSha256(string value)
        {
            return !value.NullOrEmpty() && value.Length == 64
                && value.All(Uri.IsHexDigit);
        }

        private static string BridgeFileSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", "");
        }
    }

    // Supplemental read-only evidence at the native JobDriver start boundary.
    // It records a HaulToContainer job aimed at the exact pending construction;
    // completion consumption is established separately by the Frame inventory
    // and synchronous completion transaction. It never changes the Job or pawn.
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Notify_Starting))]
    internal static class Patch_CAAgentBedroomCauseNativeResourceJobStart
    {
        private static void Postfix(JobDriver __instance)
        {
            CAAgentDebugBridge.ForCurrentGame()?
                .ObserveBedroomCauseNativeResourceJobStart(__instance);
        }
    }

    [HarmonyPatch(typeof(SavedGameLoaderNow),
        nameof(SavedGameLoaderNow.LoadGameFromSaveFileNow))]
    internal static class Patch_CAAgentDebugBridgeNativeLoadIdentity
    {
        [HarmonyPrefix]
        internal static void Prefix(string fileName)
        {
            CAAgentDebugBridge.CaptureNativeLoadSaveName(fileName);
        }
    }
}
