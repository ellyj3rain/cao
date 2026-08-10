using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module: animal care awareness. Vanilla only hand-feeds animal PATIENTS (Doctor work,
    // downed/bedridden), so a starving-but-standing animal gets nothing, and a downed one
    // waits on doctors who may never come. Handlers now carry both: food brought to
    // starving colony animals - patient-fed if downed, dropped at their feet otherwise -
    // and downed animals rescued to open animal beds.
    public class WorkGiver_FeedHungryAnimals : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode { get { return PathEndMode.Touch; } }

        public override bool Prioritized { get { return true; } }

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            var animal = t.Thing as Pawn;
            if (animal == null || animal.needs == null || animal.needs.food == null) return 0f;
            float pri = animal.needs.food.Starving ? 50000f : 20000f;
            return pri - pawn.Position.DistanceTo(t.Cell);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.animalCare) yield break;
            if (AutonomyComponent.LevelOf(pawn) < 1) yield break;
            var list = pawn.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (!a.RaceProps.Animal || a.Dead || a.needs == null || a.needs.food == null) continue;
                var food = a.needs.food;
                if (food.Starving || food.CurCategory == HungerCategory.UrgentlyHungry)
                    yield return a;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var animal = t as Pawn;
            if (animal == null || !animal.RaceProps.Animal || animal.Faction != Faction.OfPlayer) return null;
            if (animal.Dead || !animal.Spawned || animal.needs == null || animal.needs.food == null) return null;
            var need = animal.needs.food;
            if (!need.Starving)
            {
                // Urgently hungry is preemptive care - Proactive+. Starving is everyone's problem.
                if (need.CurCategory != HungerCategory.UrgentlyHungry) return null;
                if (AutonomyComponent.LevelOf(pawn) < 2) return null;
            }
            if (!pawn.CanReach(animal, PathEndMode.Touch, Danger.Some)) return null;

            Thing feed = FindFeed(pawn, animal);
            if (feed == null) return null;
            float nutrPer = feed.GetStatValue(StatDefOf.Nutrition);
            if (nutrPer <= 0.001f) return null;
            int wanted = UnityEngine.Mathf.CeilToInt(need.NutritionWanted / nutrPer);
            int count = UnityEngine.Mathf.Clamp(wanted, 1, feed.stackCount);

            if (animal.Downed)
            {
                if (!pawn.CanReserve(animal)) return null;
                int ingest = FoodUtility.WillIngestStackCountOf(animal, feed.def, nutrPer);
                JobDef jobDef = FoodUtility.ShouldBeFedBySomeone(animal)
                    ? JobDefOf.FeedPatient
                    : CA_Defs.FeedDownedAnimal;
                Job job = JobMaker.MakeJob(jobDef, feed, animal);
                job.count = UnityEngine.Mathf.Clamp(ingest, 1, count);
                return job;
            }

            // Mobile animals can feed themselves once the food is in reach.
            IntVec3 dropCell;
            if (!TryFindDropCell(pawn, animal, out dropCell)) return null;
            // One meal delivery at a time: food already at or next to the animal means
            // someone else got here first - don't pile on stacks.
            if (FoodNearby(animal)) return null;
            Job haul = JobMaker.MakeJob(JobDefOf.HaulToCell, feed, dropCell);
            haul.count = count;
            haul.haulMode = HaulMode.ToCellNonStorage;
            return haul;
        }

        // Animal feed first (hay, kibble, raw), proper meals only as a last resort -
        // preferability is the game's own ranking of exactly that.
        private static Thing FindFeed(Pawn hauler, Pawn animal)
        {
            var things = hauler.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            Thing best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < things.Count; i++)
            {
                var f = things[i];
                if (f == null || !f.Spawned || f.def.ingestible == null) continue;
                if (!f.def.IsNutritionGivingIngestible) continue;
                if (f.IsForbidden(hauler) || HiddenThingsComponent.IsStashed(f)) continue;
                if (!animal.RaceProps.CanEverEat(f)) continue;
                if (!hauler.CanReserve(f)) continue;
                if (!hauler.CanReach(f, PathEndMode.ClosestTouch, Danger.Some)) continue;
                float score = -100f * (int)f.def.ingestible.preferability
                    - hauler.Position.DistanceTo(f.Position);
                if (score > bestScore) { bestScore = score; best = f; }
            }
            return best;
        }

        private static bool FoodNearby(Pawn animal)
        {
            var map = animal.Map;
            var cells = GenAdjFast.AdjacentCells8Way(animal.Position);
            for (int i = -1; i < cells.Count; i++)
            {
                var c = i < 0 ? animal.Position : cells[i];
                if (!c.InBounds(map)) continue;
                var item = c.GetFirstItem(map);
                if (item != null && item.def.IsNutritionGivingIngestible
                    && animal.RaceProps.CanEverEat(item)) return true;
            }
            return false;
        }

        private static bool TryFindDropCell(Pawn hauler, Pawn animal, out IntVec3 cell)
        {
            var map = animal.Map;
            cell = animal.Position;
            if (cell.Standable(map) && cell.GetFirstItem(map) == null) return true;
            var adj = GenAdjFast.AdjacentCells8Way(animal.Position);
            for (int i = 0; i < adj.Count; i++)
            {
                var c = adj[i];
                if (!c.InBounds(map) || !c.Standable(map) || c.GetFirstItem(map) != null) continue;
                if (!hauler.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                cell = c;
                return true;
            }
            return false;
        }
    }

    // Vanilla's patient-feeding toils are correct for a downed animal once the
    // handler has accepted responsibility. Its driver adds a policy failure when
    // the animal is not already in a player bed; this driver retains the native
    // reservation, pickup, carry, feeding, ingestion, and infection seams without
    // repeating that bed-only eligibility decision.
    public class JobDriver_CAFeedDownedAnimal : JobDriver
    {
        private const TargetIndex FoodInd = TargetIndex.A;
        private const TargetIndex AnimalInd = TargetIndex.B;

        private Thing Food { get { return job.targetA.Thing; } }
        private Pawn Animal { get { return job.targetB.Pawn; } }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Animal == null || !pawn.Reserve(
                Animal, job, 1, -1, null, errorOnFailed)) return false;
            if (Food == null) return false;
            int maxAmount = FoodUtility.GetMaxAmountToPickup(
                Food, pawn, job.count);
            if (maxAmount <= 0 || !pawn.Reserve(
                Food, job, 10, maxAmount, null, errorOnFailed)) return false;
            job.count = maxAmount;
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(AnimalInd);
            this.FailOn(() => Animal == null || Animal.Dead || !Animal.Downed
                || Animal.Faction != Faction.OfPlayer
                || Animal.RaceProps == null || !Animal.RaceProps.Animal
                || Animal.needs?.food == null);

            yield return Toils_Goto.GotoThing(
                FoodInd, PathEndMode.ClosestTouch).FailOnForbidden(FoodInd);
            yield return Toils_Ingest.PickupIngestible(FoodInd, Animal);
            yield return Toils_Goto.GotoThing(AnimalInd, PathEndMode.Touch);
            yield return Toils_Ingest.ChewIngestible(
                Animal, 1.5f, FoodInd).FailOnCannotTouch(
                    AnimalInd, PathEndMode.Touch);
            Toil finish = Toils_Ingest.FinalizeIngest(Animal, FoodInd);
            finish.finishActions = new List<Action>
            {
                delegate
                {
                    if (ModsConfig.AnomalyActive && Rand.Chance(0.3f)
                        && MetalhorrorUtility.IsInfected(pawn))
                        MetalhorrorUtility.Infect(
                            Animal, pawn, "FeedingImplant");
                }
            };
            yield return finish;
        }
    }

    public class WorkGiver_RescueDownedAnimals : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode { get { return PathEndMode.Touch; } }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.animalCare) yield break;
            if (AutonomyComponent.LevelOf(pawn) < 1) yield break;
            var list = pawn.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a.RaceProps.Animal && a.Downed && !a.Dead && a.CurrentBed() == null)
                    yield return a;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var animal = t as Pawn;
            if (animal == null || !animal.RaceProps.Animal || animal.Faction != Faction.OfPlayer) return null;
            if (!animal.Downed || animal.Dead || !animal.Spawned || animal.CurrentBed() != null) return null;
            // Vanilla's rescue safety gates: don't grab caravan-loading or
            // slaughter-marked animals, and don't walk into enemy guns for it.
            if (!HealthAIUtility.WantsToBeRescued(animal)) return null;
            if (GenAI.EnemyIsNear(animal, 25f)) return null;
            if (!pawn.CanReserve(animal)) return null;
            if (!pawn.CanReach(animal, PathEndMode.Touch, Danger.Some)) return null;
            var bed = RestUtility.FindBedFor(animal, pawn, false, false, null);
            if (bed == null) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Rescue, animal, bed);
            job.count = 1;
            return job;
        }
    }
}
