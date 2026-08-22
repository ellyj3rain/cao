using System;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // SteadyEnvironmentEffects is the last area-scaled native cost in the
    // quiet regional tick. It visits Mathf.CeilToInt(area * 0.0006f) cells
    // per tick -- 808 on the 400x6 backing map's 1,345,680 cells -- and the
    // outer tick probe attributes 7.44-8.16 ms/tick to it, stable across
    // every measured window. That is ~9.5 microseconds per visit, far more
    // than the grid reads in DoCellSteadyEffects can account for, so the
    // cost has an owner that is not obvious from the call shape.
    //
    // Unlike the freeze scan, none of this work is provably a no-op: snow
    // melt, sand dissipation, filth washing and deterioration are all real.
    // So the owner has to be measured before anything is screened, or the
    // optimization would be a guess that trades away gameplay.
    //
    // This probe brackets the sub-calls that could plausibly own the time
    // and reports their split. It is armed only when CA_STEADY_SUBPROBE=1,
    // because the patches sit on methods the whole game calls and the
    // bracketing cost is paid on every call regardless of the gate.
    //
    // STATED MEASUREMENT DIVERGENCE: the probe inflates what it measures.
    // Each bracketed call pays two Stopwatch reads, so the reported per-
    // bucket figures and the run's own `steady` bucket are both larger
    // than an unprobed run's. Read the split, not the absolute ms; the
    // unprobed `steady` total from the same fixture is the true denominator.
    // `rebuild` is a strict subset of `room`, not a sibling, and is
    // reported separately so the region-rebuild share of the room lookup
    // is visible. Buckets accumulate across all maps; the fixture plays one.
    internal static class CASteadyEffectsSubProbe
    {
        private const int ReportEveryTicks = 300;

        internal static readonly bool Enabled = string.Equals(
            Environment.GetEnvironmentVariable("CA_STEADY_SUBPROBE"),
            "1", StringComparison.Ordinal);

        // Only accumulate while the steady loop is on the stack; these
        // methods are called from everywhere else in the game too.
        private static bool inSteady;
        private static int ticksSeen;
        private static long visits;

        // Depth-guarded timers. TryDoDeteriorate recurses through corpse
        // apparel and GameConditionManager.DoSteadyEffects recurses via
        // Parent, so only the outermost entry may bank the elapsed time or
        // the inner frames would be counted twice.
        private static long cellAcc, roomAcc, rebuildAcc, deterAcc;
        private static long snowAcc, sandAcc, condAcc, gasAcc;
        private static long cellAt, roomAt, rebuildAt, deterAt;
        private static long snowAt, sandAt, condAt, gasAt;
        private static int cellIn, roomIn, rebuildIn, deterIn;
        private static int snowIn, sandIn, condIn, gasIn;

        // Inner frames of the room lookup. RoomAt measured 9.11-9.64
        // microseconds a call while the rebuild inside it measured 0.11, so
        // the cost is somewhere in the DistrictAt -> RegionAt ->
        // GetValidRegionAt descent and guessing which frame owns it has
        // already been wrong once. Each is a strict subset of `room`.
        private static long regionAcc, validAcc;
        private static long regionAt_, validAt;
        private static int regionIn, validIn;

        internal static void Arm(Harmony harmony)
        {
            if (!Enabled) return;
            Patch(typeof(SteadyEnvironmentEffects), "SteadyEnvironmentEffectsTick",
                nameof(LoopBegin), nameof(LoopEnd));
            Patch(typeof(SteadyEnvironmentEffects), "DoCellSteadyEffects",
                nameof(CellBegin), nameof(CellEnd));
            Patch(typeof(RegionAndRoomQuery), nameof(RegionAndRoomQuery.RoomAt),
                nameof(RoomBegin), nameof(RoomEnd));
            Patch(typeof(RegionAndRoomQuery), nameof(RegionAndRoomQuery.RegionAt),
                nameof(RegionBegin), nameof(RegionEnd));
            Patch(typeof(RegionGrid), nameof(RegionGrid.GetValidRegionAt),
                nameof(ValidBegin), nameof(ValidEnd));
            Patch(typeof(RegionAndRoomUpdater),
                nameof(RegionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms),
                nameof(RebuildBegin), nameof(RebuildEnd));
            Patch(typeof(SteadyEnvironmentEffects), "TryDoDeteriorate",
                nameof(DeterBegin), nameof(DeterEnd));
            Patch(typeof(SnowGrid), nameof(SnowGrid.AddDepth),
                nameof(SnowBegin), nameof(SnowEnd));
            Patch(typeof(SandGrid), nameof(SandGrid.AddDepth),
                nameof(SandBegin), nameof(SandEnd));
            Patch(typeof(GameConditionManager),
                nameof(GameConditionManager.DoSteadyEffects),
                nameof(CondBegin), nameof(CondEnd));
            Patch(typeof(GasUtility), nameof(GasUtility.DoSteadyEffects),
                nameof(GasBegin), nameof(GasEnd));
            Log.Message("[CA][SteadySub] armed: DoCellSteadyEffects "
                + "sub-attribution every " + ReportEveryTicks + " ticks; "
                + "bracketing inflates absolute ms -- read the split; "
                + "rebuild is a subset of room");
        }

        // A missing target must be loud rather than a silently absent
        // bucket that reads as "this cost nothing".
        private static void Patch(Type type, string name,
            string prefix, string postfix)
        {
            var target = AccessTools.Method(type, name);
            if (target == null)
            {
                Log.Error("[CA][SteadySub] target not found: "
                    + type.Name + "." + name
                    + "; its bucket will read 0 and must not be trusted");
                return;
            }
            new Harmony("ca.steady.subprobe").Patch(target,
                prefix: new HarmonyMethod(typeof(CASteadyEffectsSubProbe),
                    prefix),
                postfix: new HarmonyMethod(typeof(CASteadyEffectsSubProbe),
                    postfix));
        }

        private static void LoopBegin() { inSteady = true; }

        private static void LoopEnd()
        {
            inSteady = false;
            if (++ticksSeen < ReportEveryTicks) return;
            double scale = 1000d / Stopwatch.Frequency;
            string ms(long v) => (v * scale / ticksSeen)
                .ToString("F2", CultureInfo.InvariantCulture);
            string us(long v) => visits == 0 ? "-"
                : (v * scale * 1000d / visits)
                    .ToString("F2", CultureInfo.InvariantCulture);
            long named = roomAcc + deterAcc + snowAcc + sandAcc
                + condAcc + gasAcc;
            Log.Message("[CA][SteadySub] " + ticksSeen + " ticks, "
                + (visits / ticksSeen) + " visits/tick, ms/tick: "
                + "cell " + ms(cellAcc)
                + "; room " + ms(roomAcc)
                + " [regionAt " + ms(regionAcc)
                + "; validRegion " + ms(validAcc)
                + "; rebuild " + ms(rebuildAcc) + "]"
                + "; deteriorate " + ms(deterAcc)
                + "; snow " + ms(snowAcc)
                + "; sand " + ms(sandAcc)
                + "; conditions " + ms(condAcc)
                + "; gas " + ms(gasAcc)
                + "; unbracketed " + ms(cellAcc - named)
                + " | per visit us: cell " + us(cellAcc)
                + "; room " + us(roomAcc)
                + "; deteriorate " + us(deterAcc));
            ticksSeen = 0;
            visits = 0;
            cellAcc = roomAcc = rebuildAcc = deterAcc = 0;
            regionAcc = validAcc = 0;
            snowAcc = sandAcc = condAcc = gasAcc = 0;
        }

        private static void CellBegin()
        {
            if (!inSteady) return;
            if (cellIn++ == 0) cellAt = Stopwatch.GetTimestamp();
        }

        private static void CellEnd()
        {
            if (!inSteady) return;
            if (--cellIn == 0)
            {
                cellAcc += Stopwatch.GetTimestamp() - cellAt;
                visits++;
            }
        }

        private static void RoomBegin()
        {
            if (!inSteady) return;
            if (roomIn++ == 0) roomAt = Stopwatch.GetTimestamp();
        }

        private static void RoomEnd()
        {
            if (!inSteady) return;
            if (--roomIn == 0)
                roomAcc += Stopwatch.GetTimestamp() - roomAt;
        }

        private static void RegionBegin()
        {
            if (!inSteady) return;
            if (regionIn++ == 0) regionAt_ = Stopwatch.GetTimestamp();
        }

        private static void RegionEnd()
        {
            if (!inSteady) return;
            if (--regionIn == 0)
                regionAcc += Stopwatch.GetTimestamp() - regionAt_;
        }

        private static void ValidBegin()
        {
            if (!inSteady) return;
            if (validIn++ == 0) validAt = Stopwatch.GetTimestamp();
        }

        private static void ValidEnd()
        {
            if (!inSteady) return;
            if (--validIn == 0)
                validAcc += Stopwatch.GetTimestamp() - validAt;
        }

        private static void RebuildBegin()
        {
            if (!inSteady) return;
            if (rebuildIn++ == 0) rebuildAt = Stopwatch.GetTimestamp();
        }

        private static void RebuildEnd()
        {
            if (!inSteady) return;
            if (--rebuildIn == 0)
                rebuildAcc += Stopwatch.GetTimestamp() - rebuildAt;
        }

        private static void DeterBegin()
        {
            if (!inSteady) return;
            if (deterIn++ == 0) deterAt = Stopwatch.GetTimestamp();
        }

        private static void DeterEnd()
        {
            if (!inSteady) return;
            if (--deterIn == 0)
                deterAcc += Stopwatch.GetTimestamp() - deterAt;
        }

        private static void SnowBegin()
        {
            if (!inSteady) return;
            if (snowIn++ == 0) snowAt = Stopwatch.GetTimestamp();
        }

        private static void SnowEnd()
        {
            if (!inSteady) return;
            if (--snowIn == 0)
                snowAcc += Stopwatch.GetTimestamp() - snowAt;
        }

        private static void SandBegin()
        {
            if (!inSteady) return;
            if (sandIn++ == 0) sandAt = Stopwatch.GetTimestamp();
        }

        private static void SandEnd()
        {
            if (!inSteady) return;
            if (--sandIn == 0)
                sandAcc += Stopwatch.GetTimestamp() - sandAt;
        }

        private static void CondBegin()
        {
            if (!inSteady) return;
            if (condIn++ == 0) condAt = Stopwatch.GetTimestamp();
        }

        private static void CondEnd()
        {
            if (!inSteady) return;
            if (--condIn == 0)
                condAcc += Stopwatch.GetTimestamp() - condAt;
        }

        private static void GasBegin()
        {
            if (!inSteady) return;
            if (gasIn++ == 0) gasAt = Stopwatch.GetTimestamp();
        }

        private static void GasEnd()
        {
            if (!inSteady) return;
            if (--gasIn == 0)
                gasAcc += Stopwatch.GetTimestamp() - gasAt;
        }
    }
}
