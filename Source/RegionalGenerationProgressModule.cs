using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // The native Terrain genstep is one ordered cell loop. On a 400x6
    // regional map it can run for about 53 seconds without changing the long
    // event text. Inject one side-effect-free counter after the native terrain
    // commit; the loop, terrain choice, RNG order, and patch-maker cleanup stay
    // native and composable with other transpilers.
    [HarmonyPatch(typeof(GenStep_Terrain), nameof(GenStep_Terrain.Generate))]
    internal static class CARegionalTerrainProgressPatch
    {
        private const long PulseMilliseconds = 25000;
        private const int CheckMask = 16383;

        [ThreadStatic] private static Map activeMap;
        [ThreadStatic] private static Stopwatch timer;
        [ThreadStatic] private static int completedCells;
        [ThreadStatic] private static int totalCells;
        [ThreadStatic] private static long nextPulse;

        [HarmonyPrefix]
        private static void Prefix(Map map, out bool __state)
        {
            __state = map != null
                && Current.ProgramState == ProgramState.MapInitializing
                && MapGenerator.mapBeingGenerated == map
                && CARegionalRiverPatchUtility.Active(map);
            if (!__state) return;
            activeMap = map;
            completedCells = 0;
            totalCells = map.cellIndices.NumGridCells;
            nextPulse = PulseMilliseconds;
            timer = Stopwatch.StartNew();
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,
            bool __state)
        {
            if (!__state) return __exception;
            activeMap = null;
            timer = null;
            completedCells = 0;
            totalCells = 0;
            nextPulse = 0;
            try
            {
                LongEventHandler.SetCurrentEventText(
                    "GeneratingMap".Translate());
            }
            catch (Exception) { }
            return __exception;
        }

        internal static void TerrainCommitted(Map map)
        {
            if (!ReferenceEquals(map, activeMap) || timer == null) return;
            int completed = ++completedCells;
            if ((completed & CheckMask) != 0
                || timer.ElapsedMilliseconds < nextPulse)
                return;

            int percent = totalCells > 0
                ? Math.Min(100, completed * 100 / totalCells) : 0;
            long elapsed = timer.ElapsedMilliseconds;
            nextPulse = elapsed + PulseMilliseconds;
            string status = "Generating terrain: " + percent + "%";
            LongEventHandler.SetCurrentEventText(status);
            Log.Message("[CA][Regional][Progress] " + status + " ("
                + completed + "/" + totalCells + " cells after "
                + elapsed + " ms) for " + map.Size.x + "x" + map.Size.z
                + " map " + map.uniqueID);
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var source = new List<CodeInstruction>(instructions);
            MethodInfo setTerrain = AccessTools.Method(typeof(TerrainGrid),
                nameof(TerrainGrid.SetTerrain), new[]
                {
                    typeof(IntVec3), typeof(TerrainDef)
                });
            MethodInfo committed = AccessTools.Method(
                typeof(CARegionalTerrainProgressPatch),
                nameof(TerrainCommitted));
            int matches = 0;
            for (int i = 0; i < source.Count; i++)
                if (source[i].Calls(setTerrain)) matches++;
            if (matches != 1)
            {
                Log.Warning("[CA][Regional][Progress] terrain progress "
                    + "injection refused: expected one SetTerrain call, "
                    + "found " + matches + "; native terrain retained");
                return source;
            }

            var result = new List<CodeInstruction>(source.Count + 2);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(source[i]);
                if (!source[i].Calls(setTerrain)) continue;
                result.Add(new CodeInstruction(OpCodes.Ldarg_1));
                result.Add(new CodeInstruction(OpCodes.Call, committed));
            }
            return result;
        }
    }
}
