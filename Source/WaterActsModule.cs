using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // THE BASELINE ACTS [Bad Hygiene port, step 2]. What a person does
    // about thirst, filth and a full bladder when they have nothing but
    // the ground and whatever they are carrying.
    //
    // These are the LOW ends of three ladders, not three features. A
    // body drinks from the stream, washes in it, and goes behind a bush
    // - which is what people did before plumbing and what they still do
    // when the plumbing is somewhere else. Fixtures will beat all three
    // on speed, safety and dignity; none of them will replace the fact
    // that a body with no fixture still has to do something.
    //
    // They hang off the engine's own seam for satisfying a need: a
    // JobGiver in the think tree, which is how vanilla satisfies hunger
    // and rest and how this mod already reacts in combat. Nothing here
    // reaches into anyone from outside.
    internal static class CAWaterGround
    {
        internal const float SearchRadius = 40f;

        // SALT IS NOT A CONTAMINANT YOU CAN BOIL OUT. Untreated fresh
        // water makes a person ill and keeps them alive; sea water
        // takes more water out of a body than it puts in, and boiling
        // it concentrates the salt. Drinking it is not a risk, it is a
        // faster death - so it is not a source, and no amount of the
        // campfire recipe makes it one. Getting fresh water out of the
        // sea is desalination, which is a machine, and machines arrive
        // with the fixtures.
        //
        // The engine names them for us: every salt terrain carries
        // "Ocean". Rivers, lakes and marsh do not.
        internal static bool IsWater(IntVec3 c, Map map)
        {
            TerrainDef t = c.GetTerrain(map);
            return t != null && t.IsWater;
        }

        internal static bool IsSalt(IntVec3 c, Map map)
        {
            TerrainDef t = c.GetTerrain(map);
            return t != null && t.IsWater && t.defName.Contains("Ocean");
        }

        // Fresh: fit to drink from and to fill from.
        internal static bool IsSource(IntVec3 c, Map map)
        {
            return IsWater(c, map) && !IsSalt(c, map);
        }

        // Washing is another matter - people have always washed in the
        // sea. It gets the dirt off; it is drinking it that kills.
        internal static bool IsWashable(IntVec3 c, Map map)
        {
            return IsWater(c, map);
        }

        // The nearest water a pawn can actually get to the edge of.
        internal static bool TryFindWater(Pawn pawn, out IntVec3 found,
            bool freshOnly = true)
        {
            found = IntVec3.Invalid;
            Map map = pawn.Map;
            if (map == null) return false;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(
                pawn.Position, SearchRadius, true))
            {
                if (!c.InBounds(map)) continue;
                if (freshOnly ? !IsSource(c, map) : !IsWashable(c, map))
                    continue;
                if (c.Fogged(map)) continue;
                if (!pawn.CanReach(c, PathEndMode.Touch, Danger.Some))
                    continue;
                found = c;
                return true;
            }
            return false;
        }

        // Drinkable water this pawn is carrying or can pick up.
        internal static Thing FindDrink(Pawn pawn)
        {
            ThingDef clean = CAWaterDefOf.Clean;
            ThingDef dirty = CAWaterDefOf.Dirty;
            if (pawn.inventory?.innerContainer != null)
            {
                foreach (Thing t in pawn.inventory.innerContainer)
                    if (t.def == clean) return t;
                foreach (Thing t in pawn.inventory.innerContainer)
                    if (t.def == dirty) return t;
            }
            Thing near = Nearest(pawn, clean);
            return near ?? Nearest(pawn, dirty);
        }

        private static Thing Nearest(Pawn pawn, ThingDef def)
        {
            if (def == null || pawn.Map == null) return null;
            return GenClosest.ClosestThingReachable(pawn.Position,
                pawn.Map, ThingRequest.ForDef(def), PathEndMode.Touch,
                TraverseParms.For(pawn), SearchRadius,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }
    }

    [DefOf]
    public static class CAWaterDefOf
    {
        public static ThingDef CA_WaterPotable;
        public static ThingDef CA_WaterContaminated;
        public static ThingDef CA_WaterBrackish;
        public static ThingDef CA_WaterSaline;
        public static JobDef CA_DrinkFromSource;
        public static JobDef CA_WashAtWater;
        public static JobDef CA_RelieveOutdoors;
        public static JobDef CA_WaterPatient;
        public static JobDef CA_DrawWater;
        public static JobDef CA_DrawFromWell;
        public static JobDef CA_DrawFromCistern;
        public static JobDef CA_DrinkFromVessel;
        public static NeedDef CA_Thirst;
        public static NeedDef CA_Hygiene;
        public static NeedDef CA_Bladder;
        public static StatDef CA_ThirstRateMultiplier;
        public static StatDef CA_BladderRateMultiplier;

        static CAWaterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CAWaterDefOf));
        }

        internal static ThingDef Clean => CA_WaterPotable;
        internal static ThingDef Dirty => CA_WaterContaminated;
    }

    // ---- thirst ----------------------------------------------------

    public class JobGiver_CADrink : ThinkNode_JobGiver
    {
        // Below this a body starts looking for water rather than
        // getting on with what it was doing.
        private const float Seek = 0.4f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Need need = pawn.needs?.TryGetNeed(CAWaterDefOf.CA_Thirst);
            if (need == null || need.CurLevel > Seek) return null;

            // what is on your belt, first - that is what it is for
            if (CAVessels.FirstDrinkable(pawn) != null)
                return JobMaker.MakeJob(CAWaterDefOf.CA_DrinkFromVessel);

            Thing drink = CAWaterGround.FindDrink(pawn);
            if (drink != null)
            {
                Job ingest = JobMaker.MakeJob(JobDefOf.Ingest, drink);
                ingest.count = 1;
                return ingest;
            }
            // nothing to hand: the stream, and whatever is in it
            IntVec3 water;
            if (!CAWaterGround.TryFindWater(pawn, out water)) return null;
            return JobMaker.MakeJob(CAWaterDefOf.CA_DrinkFromSource,
                water);
        }
    }

    // Drinking straight from the ground. It works, and it is how the
    // ported diseases get into a body.
    public class JobDriver_CADrinkFromSource : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFail)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.A,
                PathEndMode.Touch);
            Toil drink = Toils_General.Wait(220);
            drink.WithProgressBarToilDelay(TargetIndex.A);
            drink.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return drink;
            Toil finish = ToilMaker.MakeToil("CADrinkFinish");
            finish.initAction = delegate
            {
                Pawn p = finish.actor;
                Need thirst = p.needs?.TryGetNeed(CAWaterDefOf.CA_Thirst);
                if (thirst != null) thirst.CurLevel += 0.55f;
                // untreated, on purpose - see the untreated water item
                if (Rand.Value < 0.12f) Sicken(p, "CA_Dysentery", 0.15f);
                if (Rand.Value < 0.02f) Sicken(p, "CA_Cholera", 0.10f);
                // and you fill what you carry while you are down there
                CAWaterDraw.TopUp(p, CAWaterSalinity.Fresh, true);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        internal static void Sicken(Pawn p, string hediff, float severity)
        {
            HediffDef def = DefDatabase<HediffDef>
                .GetNamedSilentFail(hediff);
            if (def == null || p.health == null) return;
            if (p.health.hediffSet.GetFirstHediffOfDef(def) != null)
                return;
            Hediff h = HediffMaker.MakeHediff(def, p);
            h.Severity = severity;
            p.health.AddHediff(h);
        }
    }

    // ---- washing ---------------------------------------------------

    public class JobGiver_CAWash : ThinkNode_JobGiver
    {
        private const float Seek = 0.25f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Need need = pawn.needs?.TryGetNeed(CAWaterDefOf.CA_Hygiene);
            if (need == null || need.CurLevel > Seek) return null;
            IntVec3 water;
            // the sea will do for this
            if (!CAWaterGround.TryFindWater(pawn, out water, false))
                return null;
            return JobMaker.MakeJob(CAWaterDefOf.CA_WashAtWater, water);
        }
    }

    public class JobDriver_CAWashAtWater : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFail)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A,
                PathEndMode.Touch);
            Toil wash = Toils_General.Wait(500);
            wash.WithProgressBarToilDelay(TargetIndex.A);
            wash.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return wash;
            Toil done = ToilMaker.MakeToil("CAWashFinish");
            done.initAction = delegate
            {
                Pawn p = done.actor;
                Need hyg = p.needs?.TryGetNeed(CAWaterDefOf.CA_Hygiene);
                if (hyg != null) hyg.CurLevel = 1f;
                // open water in the open air is cold water
                ThoughtDef cold = DefDatabase<ThoughtDef>
                    .GetNamedSilentFail("CA_ColdWater");
                if (cold != null && p.AmbientTemperature < 18f)
                    p.needs?.mood?.thoughts?.memories
                        ?.TryGainMemory(cold);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    // ---- relief ----------------------------------------------------

    public class JobGiver_CARelieve : ThinkNode_JobGiver
    {
        private const float Seek = 0.15f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Need need = pawn.needs?.TryGetNeed(CAWaterDefOf.CA_Bladder);
            if (need == null || need.CurLevel > Seek) return null;
            IntVec3 spot;
            if (!TryFindSpot(pawn, out spot)) return null;
            return JobMaker.MakeJob(CAWaterDefOf.CA_RelieveOutdoors,
                spot);
        }

        // Somewhere out of the way: off the beaten ground, not indoors,
        // not where the water is drawn from.
        private bool TryFindSpot(Pawn pawn, out IntVec3 spot)
        {
            spot = IntVec3.Invalid;
            Map map = pawn.Map;
            if (map == null) return false;
            IntVec3 best = IntVec3.Invalid;
            float bestScore = float.MinValue;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(
                pawn.Position, 22f, true))
            {
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if (c.Fogged(map) || c.Roofed(map)) continue;
                if (CAWaterGround.IsSource(c, map)) continue;
                if (c.GetRoom(map) != null
                    && !c.GetRoom(map).PsychologicallyOutdoors) continue;
                float away = c.DistanceTo(pawn.Position);
                float seen = 0f;
                foreach (Pawn other in map.mapPawns.AllPawnsSpawned)
                {
                    if (other == pawn || !other.RaceProps.Humanlike)
                        continue;
                    float d = other.Position.DistanceTo(c);
                    if (d < 12f) seen += 12f - d;
                }
                float score = away * 0.5f - seen;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (!best.IsValid) return false;
            spot = best;
            return true;
        }
    }

    public class JobDriver_CARelieveOutdoors : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFail)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A,
                PathEndMode.OnCell);
            Toil go = Toils_General.Wait(300);
            go.WithProgressBarToilDelay(TargetIndex.A);
            go.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return go;
            Toil done = ToilMaker.MakeToil("CARelieveFinish");
            done.initAction = delegate
            {
                Pawn p = done.actor;
                Need bladder = p.needs?.TryGetNeed(
                    CAWaterDefOf.CA_Bladder);
                if (bladder != null) bladder.CurLevel = 1f;
                ThingDef filth = DefDatabase<ThingDef>
                    .GetNamedSilentFail("CA_FilthFaeces");
                if (p.Spawned && filth != null)
                    FilthMaker.TryMakeFilth(p.Position, p.Map, filth,
                        p.LabelShort);
                ThoughtDef open = DefDatabase<ThoughtDef>
                    .GetNamedSilentFail("CA_OpenDefecation");
                if (open != null)
                    p.needs?.mood?.thoughts?.memories
                        ?.TryGainMemory(open);
                // and it costs a little cleanliness
                Need hyg = p.needs?.TryGetNeed(CAWaterDefOf.CA_Hygiene);
                if (hyg != null) hyg.CurLevel -= 0.08f;
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
