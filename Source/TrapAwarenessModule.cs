using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Module: trap awareness. Vanilla's smart raids give 30% of pawns full trap
    // knowledge up front (ILordAvoidTraps); this module is the EVENT-driven half - a
    // sprung trap is information, and smart enemies (Intellectual 5+) infer the ground
    // nearby is seeded. Routed through the game's own KnowsOfTrap seam so the vanilla
    // pathing-avoidance machinery does the rest. Memory fades between raids.
    //
    // THREADING: KnowsOfTrap runs on Unity job-system worker threads (the async
    // pathfinder) - SprungNear must be strictly read-only; all mutation happens on the
    // main thread (NoteSpring, the tick sweep).
    public class TrapMemoryMapComponent : MapComponent
    {
        private List<IntVec3> springs = new List<IntVec3>();
        private List<int> springTicks = new List<int>();
        private const int MemoryTicks = 60000; // one day: raid-scale memory, not forever

        public TrapMemoryMapComponent(Map map) : base(map) { }

        // Sprung-trap memory survives save/load - a reload mid-raid keeps the lane hot.
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref springs, "CA_trapSprings", LookMode.Value);
            Scribe_Collections.Look(ref springTicks, "CA_trapSpringTicks", LookMode.Value);
            if (springs == null) springs = new List<IntVec3>();
            if (springTicks == null) springTicks = new List<int>();
            while (springTicks.Count < springs.Count) springTicks.Add(0);
            while (springTicks.Count > springs.Count) springTicks.RemoveAt(springTicks.Count - 1);
        }

        public void NoteSpring(IntVec3 cell)
        {
            springs.Add(cell);
            springTicks.Add(Find.TickManager.TicksGame);
        }

        // Main-thread pruning; the read side never mutates.
        public override void MapComponentTick()
        {
            if (springs.Count == 0 || Find.TickManager.TicksGame % 2500 != 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = springs.Count - 1; i >= 0; i--)
            {
                if (now - springTicks[i] > MemoryTicks)
                {
                    springs.RemoveAt(i);
                    springTicks.RemoveAt(i);
                }
            }
        }

        public bool SprungNear(IntVec3 cell, float radius)
        {
            int now = Find.TickManager.TicksGame;
            // Read-only: iterate a bounded snapshot of the count, mutate nothing -
            // this is called from pathfinder worker threads.
            int n = springs.Count;
            for (int i = 0; i < n && i < springs.Count; i++)
            {
                if (now - springTicks[i] > MemoryTicks) continue;
                if (cell.InHorDistOf(springs[i], radius)) return true;
            }
            return false;
        }
    }

    public static class TrapAwarenessPatch
    {
        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var spring = AccessTools.Method(typeof(Building_Trap), "Spring");
                var knows = AccessTools.Method(typeof(Building_Trap), "KnowsOfTrap");
                if (spring == null || knows == null)
                {
                    Log.Warning("[Colonist Awareness] trap seams not found; trap awareness off");
                    return;
                }
                harmony.Patch(spring,
                    prefix: new HarmonyMethod(typeof(TrapAwarenessPatch), "SpringPrefix"),
                    postfix: new HarmonyMethod(typeof(TrapAwarenessPatch), "SpringPostfix"));
                harmony.Patch(knows, postfix: new HarmonyMethod(typeof(TrapAwarenessPatch), "KnowsPostfix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] trap awareness install failed: " + e.Message);
            }
        }

        // Spike traps DESTROY THEMSELVES inside Spring, before any postfix runs - the
        // map and position must be snapshotted in a prefix or spike springs (the main
        // case) record nothing.
        public struct SpringState
        {
            public Map map;
            public IntVec3 cell;
        }

        public static void SpringPrefix(Building_Trap __instance, out SpringState __state)
        {
            __state = default(SpringState);
            try
            {
                __state.map = __instance.Map;
                __state.cell = __instance.Position;
            }
            catch { }
        }

        public static void SpringPostfix(SpringState __state)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.trapAwareness) return;
                if (__state.map == null) return;
                var mem = __state.map.GetComponent<TrapMemoryMapComponent>();
                if (mem != null) mem.NoteSpring(__state.cell);
            }
            catch { }
        }

        // Inference: a sprung trap nearby means this ground is seeded. The clever ones
        // treat every trap in the lane as known and path around it.
        public static void KnowsPostfix(Building_Trap __instance, Pawn p, ref bool __result)
        {
            try
            {
                if (__result) return;
                var s = AwarenessMod.Settings;
                if (s == null || !s.trapAwareness) return;
                if (p == null || p.RaceProps == null || !p.RaceProps.Humanlike) return;
                if (!p.HostileTo(Faction.OfPlayer)) return;
                if (p.skills == null || p.skills.GetSkill(SkillDefOf.Intellectual).Level < 5) return;
                var map = __instance.Map;
                if (map == null) return;
                var mem = map.GetComponent<TrapMemoryMapComponent>();
                if (mem != null && mem.SprungNear(__instance.Position, 11.9f)) __result = true;
            }
            catch { }
        }
    }
}
