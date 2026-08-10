using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Awareness substrate, Layer 0: a cheap cached read of colony CONTEXT that behaviors
    // consult to gain the two things vanilla's stimulus-response AI lacks - ANTICIPATION
    // (what is about to happen) and DEFERRED VALUE (a future payoff can beat the best
    // move right now). Refreshed on a slow cadence; consumers read the cache, they never
    // rescan the colony themselves.
    //
    // First fact: meals in progress, with a real ETA read from each cook's remaining work.
    // Threat geometry and ally-intent facts slot into this same blackboard as their
    // consumers arrive - this class is the foundation the rest of the arc reads from.
    public class ColonyContextComponent : MapComponent
    {
        private int cooldown;
        private int mealsCooking;
        private int soonestMealFinishTick = -1; // absolute game tick; -1 = no meal cooking

        public ColonyContextComponent(Map map) : base(map) { }

        public static ColonyContextComponent For(Map map)
        {
            return map == null ? null : map.GetComponent<ColonyContextComponent>();
        }

        public int MealsCooking { get { return mealsCooking; } }

        // Anticipation query: will a meal be ready within `ticks` from now? This is the
        // horizon a hungry pawn weighs against eating something worse right now.
        public bool MealExpectedWithin(int ticks)
        {
            if (soonestMealFinishTick < 0) return false;
            return soonestMealFinishTick - Find.TickManager.TicksGame <= ticks;
        }

        public override void MapComponentTick()
        {
            if (--cooldown > 0) return;
            cooldown = 120; // ~2s - context does not need per-tick freshness
            try { Refresh(); } catch { }
        }

        private void Refresh()
        {
            mealsCooking = 0;
            soonestMealFinishTick = -1;
            int now = Find.TickManager.TicksGame;

            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var p = colonists[i];
                var job = p.CurJob;
                if (job == null || job.bill == null || job.bill.recipe == null) continue;
                var produced = job.bill.recipe.ProducedThingDef;
                if (produced == null || produced.ingestible == null) continue;
                if (produced.ingestible.preferability < FoodPreferability.MealAwful) continue;

                mealsCooking++;

                // ETA from remaining work and the cook's actual work speed for this
                // recipe. workLeft is only initialized once actual recipe work starts
                // (billStartTick > 0); during ingredient hauling it reads 0, which
                // would claim a meal is ready while the cook is still crossing the map.
                int finishTick = now + 600; // hauling-phase / unreadable-progress estimate
                var driver = p.jobs != null ? p.jobs.curDriver as JobDriver_DoBill : null;
                if (driver != null && driver.billStartTick > 0)
                {
                    float speed = 1f;
                    var stat = job.bill.recipe.workSpeedStat;
                    if (stat != null) speed = p.GetStatValue(stat);
                    else speed = p.GetStatValue(StatDefOf.WorkSpeedGlobal);
                    if (speed < 0.1f) speed = 0.1f;
                    int est = Mathf.RoundToInt(driver.workLeft / speed);
                    finishTick = now + Mathf.Clamp(est, 1, 5000);
                }
                if (soonestMealFinishTick < 0 || finishTick < soonestMealFinishTick)
                    soonestMealFinishTick = finishTick;
            }
        }
    }
}
