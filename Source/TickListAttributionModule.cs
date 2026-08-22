using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // listNormal is the largest remaining bucket in the quiet regional tick
    // and, unlike steady, it is bimodal on an unchanged fixture: consecutive
    // 300-tick windows measured 8.79, 14.86, 8.79, 14.44 and 9.18 ms/tick.
    // A ~6 ms swing with nothing changing is not ordinary load, and even a
    // perfect steady-effects fix leaves the slow windows near 15 ms against a
    // 16.67 ms budget for 60 TPS. So the swing has to be named before the
    // remaining gap can be closed.
    //
    // TickerType.Normal has TickInterval 1, so thingLists holds exactly one
    // bucket, BucketOf always resolves to index 0, and every registered thing
    // ticks every tick. That makes a faithful timed replacement of the loop
    // cheap and low-risk: the native ordering, the Destroyed skip and the
    // per-thing exception isolation are all reproduced exactly, including the
    // dev-mode/ErrorOnce split and its hash key, so a throwing thing behaves
    // identically to vanilla. Only the Normal list is intercepted; Rare and
    // Long keep the native method.
    //
    // Armed only by CA_LISTNORMAL_SUBPROBE=1. The per-thing Stopwatch pair is
    // real overhead on the hottest loop in the game, so absolute ms/tick from
    // an armed run are inflated; read the split and the counts.
    internal static class CATickListAttribution
    {
        private const int ReportEveryTicks = 300;
        private const int TopTypes = 8;

        internal static readonly bool Enabled = string.Equals(
            Environment.GetEnvironmentVariable("CA_LISTNORMAL_SUBPROBE"),
            "1", StringComparison.Ordinal);

        private static AccessTools.FieldRef<TickList, TickerType> TickTypeField;
        private static AccessTools.FieldRef<TickList, List<List<Thing>>> ListsField;
        private static AccessTools.FieldRef<TickList, List<Thing>> RegisterField;
        private static AccessTools.FieldRef<TickList, List<Thing>> DeregisterField;

        private static readonly Dictionary<string, long> costs =
            new Dictionary<string, long>();
        private static readonly Dictionary<string, int> counts =
            new Dictionary<string, int>();
        private static int ticksSeen;
        private static long totalAcc;
        private static int tickedAcc;

        internal static void Arm(Harmony harmony)
        {
            if (!Enabled) return;
            TickTypeField = AccessTools.FieldRefAccess<TickList, TickerType>("tickType");
            ListsField = AccessTools.FieldRefAccess<TickList, List<List<Thing>>>("thingLists");
            RegisterField = AccessTools.FieldRefAccess<TickList, List<Thing>>("thingsToRegister");
            DeregisterField = AccessTools.FieldRefAccess<TickList, List<Thing>>("thingsToDeregister");
            harmony.Patch(AccessTools.Method(typeof(TickList), nameof(TickList.Tick)),
                prefix: new HarmonyMethod(typeof(CATickListAttribution),
                    nameof(TimedTick)));
            Log.Message("[CA][ListNormal] armed: TickerType.Normal attributed "
                + "per thing type every " + ReportEveryTicks + " ticks; "
                + "per-thing bracketing inflates absolute ms -- read the split");
        }

        private static bool TimedTick(TickList __instance)
        {
            if (TickTypeField(__instance) != TickerType.Normal) return true;

            List<Thing> toRegister = RegisterField(__instance);
            List<Thing> toDeregister = DeregisterField(__instance);
            List<List<Thing>> lists = ListsField(__instance);
            // TickInterval is 1 for Normal, so BucketOf(t) is always lists[0]
            // and TicksGame % TickInterval is always 0.
            List<Thing> bucket = lists[0];

            for (int i = 0; i < toRegister.Count; i++) bucket.Add(toRegister[i]);
            toRegister.Clear();
            for (int i = 0; i < toDeregister.Count; i++) bucket.Remove(toDeregister[i]);
            toDeregister.Clear();

            if (DebugSettings.fastEcology)
            {
                Find.World.tileTemperatures.ClearCaches();
                for (int k = 0; k < lists.Count; k++)
                {
                    List<Thing> plants = lists[k];
                    for (int l = 0; l < plants.Count; l++)
                        if (plants[l].def.category == ThingCategory.Plant)
                            plants[l].TickLong();
                }
            }

            long loopStart = Stopwatch.GetTimestamp();
            for (int m = 0; m < bucket.Count; m++)
            {
                Thing thing = bucket[m];
                if (thing.Destroyed) continue;
                long started = Stopwatch.GetTimestamp();
                try
                {
                    thing.DoTick();
                }
                catch (Exception arg2)
                {
                    string arg = (thing.Spawned ? $" (at {thing.Position})" : "");
                    if (Prefs.DevMode)
                    {
                        Log.Error($"Exception ticking {thing.ToStringSafe()}{arg}: {arg2}");
                    }
                    else
                    {
                        Log.ErrorOnce($"Exception ticking {thing.ToStringSafe()}{arg}. Suppressing further errors. Exception: {arg2}", thing.thingIDNumber ^ 0x22627165);
                    }
                }
                string key = Key(thing);
                costs.TryGetValue(key, out long sum);
                costs[key] = sum + (Stopwatch.GetTimestamp() - started);
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;
                tickedAcc++;
            }
            totalAcc += Stopwatch.GetTimestamp() - loopStart;

            if (++ticksSeen >= ReportEveryTicks) Report();
            return false;
        }

        // Pawns dominate this list and their class alone says little, so name
        // the faction stance too: a hostile raider and a settlement resident
        // cost very different amounts and the bimodality may be exactly that.
        private static string Key(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                string faction = pawn.Faction == null ? "wild"
                    : pawn.Faction.IsPlayer ? "player"
                    : pawn.HostileTo(Faction.OfPlayerSilentFail) ? "hostile"
                    : "other";
                return "Pawn:" + faction;
            }
            return thing.GetType().Name;
        }

        private static void Report()
        {
            double scale = 1000d / Stopwatch.Frequency;
            string ms(long v) => (v * scale / ticksSeen)
                .ToString("F2", CultureInfo.InvariantCulture);
            var top = new System.Text.StringBuilder();
            foreach (var pair in costs.OrderByDescending(item => item.Value)
                         .Take(TopTypes))
            {
                counts.TryGetValue(pair.Key, out int n);
                top.Append("; ").Append(pair.Key).Append(' ')
                    .Append(ms(pair.Value))
                    .Append(" x").Append(n / ticksSeen);
            }
            Log.Message("[CA][ListNormal] " + ticksSeen + " ticks, "
                + (tickedAcc / ticksSeen) + " things/tick, ms/tick: "
                + "loop " + ms(totalAcc) + top);
            ticksSeen = 0;
            totalAcc = 0;
            tickedAcc = 0;
            costs.Clear();
            counts.Clear();
        }
    }
}
