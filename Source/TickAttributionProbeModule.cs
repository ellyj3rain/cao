using System;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // Exercise-session-only tick attribution. The regional performance
    // question needs the actual owner of main-thread tick time, and the
    // bounded CA module profiler only covers CA's own keys; this probe
    // buckets the native DoSingleTick into its major constituents and
    // logs one line per report interval. It is armed exclusively from
    // the convergence exercise's arming path and never patches an
    // ordinary session.
    internal static class CATickAttributionProbe
    {
        private const int ReportEveryTicks = 300;

        private static long tickStart;
        private static long bucketStart;
        private static long innerStart;
        private static long total;
        private static long mapPre;
        private static long listNormal;
        private static long listRare;
        private static long listLong;
        private static long world;
        private static long story;
        private static long mapPost;
        private static long gameComp;
        private static long animalSpawner;
        private static long plantSpawner;
        private static long powerNets;
        private static long steadyEffects;
        private static long componentTick;
        private static long gasGrid;
        private static long pollution;
        private static long lords;
        private static long resources;
        private static long fireWatch;
        private static long flecks;
        private static int ticksSeen;
        private static readonly
            System.Collections.Generic.Dictionary<string, long>
            componentCosts =
                new System.Collections.Generic.Dictionary<string, long>();
        private static readonly
            System.Collections.Generic.Dictionary<string, long>
            postCosts =
                new System.Collections.Generic.Dictionary<string, long>();

        private static AccessTools.FieldRef<TickList, TickerType>
            tickTypeField;

        internal static void Arm(Harmony harmony)
        {
            tickTypeField = AccessTools.FieldRefAccess<TickList, TickerType>(
                "tickType");
            harmony.Patch(AccessTools.Method(typeof(TickManager),
                    nameof(TickManager.DoSingleTick)),
                prefix: Method(nameof(TickBegin)),
                postfix: Method(nameof(TickEnd)));
            harmony.Patch(AccessTools.Method(typeof(Map),
                    nameof(Map.MapPreTick)),
                prefix: Method(nameof(BucketBegin)),
                postfix: Method(nameof(MapPreEnd)));
            harmony.Patch(AccessTools.Method(typeof(Map),
                    nameof(Map.MapPostTick)),
                prefix: Method(nameof(BucketBegin)),
                postfix: Method(nameof(MapPostEnd)));
            harmony.Patch(AccessTools.Method(typeof(TickList),
                    nameof(TickList.Tick)),
                prefix: Method(nameof(BucketBegin)),
                postfix: Method(nameof(TickListEnd)));
            harmony.Patch(AccessTools.Method(typeof(World),
                    nameof(World.WorldTick)),
                prefix: Method(nameof(BucketBegin)),
                postfix: Method(nameof(WorldEnd)));
            harmony.Patch(AccessTools.Method(typeof(StoryWatcher),
                    nameof(StoryWatcher.StoryWatcherTick)),
                prefix: Method(nameof(BucketBegin)),
                postfix: Method(nameof(StoryEnd)));
            harmony.Patch(AccessTools.Method(typeof(Storyteller),
                    nameof(Storyteller.StorytellerTick)),
                prefix: Method(nameof(BucketBegin)),
                postfix: Method(nameof(StoryEnd)));
            harmony.Patch(AccessTools.Method(
                    typeof(GameComponentUtility),
                    nameof(GameComponentUtility.GameComponentTick)),
                prefix: Method(nameof(BucketBegin)),
                postfix: Method(nameof(GameCompEnd)));
            harmony.Patch(AccessTools.Method(typeof(WildAnimalSpawner),
                    nameof(WildAnimalSpawner.WildAnimalSpawnerTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(AnimalSpawnerEnd)));
            harmony.Patch(AccessTools.Method(typeof(WildPlantSpawner),
                    nameof(WildPlantSpawner.WildPlantSpawnerTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(PlantSpawnerEnd)));
            harmony.Patch(AccessTools.Method(typeof(PowerNetManager),
                    nameof(PowerNetManager.PowerNetsTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(PowerNetsEnd)));
            harmony.Patch(AccessTools.Method(
                    typeof(SteadyEnvironmentEffects),
                    nameof(SteadyEnvironmentEffects
                        .SteadyEnvironmentEffectsTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(SteadyEffectsEnd)));
            harmony.Patch(AccessTools.Method(typeof(MapComponentUtility),
                    nameof(MapComponentUtility.MapComponentTick)),
                prefix: Method(nameof(TimedComponentTick)));
            harmony.Patch(AccessTools.Method(typeof(GasGrid),
                    nameof(GasGrid.Tick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(GasEnd)));
            harmony.Patch(AccessTools.Method(typeof(PollutionGrid),
                    nameof(PollutionGrid.PollutionTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(PollutionEnd)));
            harmony.Patch(AccessTools.Method(
                    typeof(Verse.AI.Group.LordManager),
                    nameof(Verse.AI.Group.LordManager.LordManagerTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(LordsEnd)));
            harmony.Patch(AccessTools.Method(typeof(ResourceCounter),
                    nameof(ResourceCounter.ResourceCounterTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(ResourcesEnd)));
            harmony.Patch(AccessTools.Method(typeof(FireWatcher),
                    nameof(FireWatcher.FireWatcherTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(FireWatchEnd)));
            harmony.Patch(AccessTools.Method(typeof(FleckManager),
                    nameof(FleckManager.FleckManagerTick)),
                prefix: Method(nameof(InnerBegin)),
                postfix: Method(nameof(FlecksEnd)));
            foreach ((Type type, string name) in new[]
            {
                (typeof(TempTerrainManager),
                    nameof(TempTerrainManager.Tick)),
                (typeof(DeferredSpawner),
                    nameof(DeferredSpawner.DeferredSpawnerTick)),
                (typeof(PassingShipManager),
                    nameof(PassingShipManager.PassingShipManagerTick)),
                (typeof(DebugCellDrawer),
                    nameof(DebugCellDrawer.DebugDrawerTick)),
                (typeof(VoluntarilyJoinableLordsStarter),
                    nameof(VoluntarilyJoinableLordsStarter
                        .VoluntarilyJoinableLordsStarterTick)),
                (typeof(GameConditionManager),
                    nameof(GameConditionManager.GameConditionManagerTick)),
                (typeof(WeatherManager),
                    nameof(WeatherManager.WeatherManagerTick)),
                (typeof(WeatherDecider),
                    nameof(WeatherDecider.WeatherDeciderTick)),
                (typeof(EffecterMaintainer),
                    nameof(EffecterMaintainer.EffecterMaintainerTick)),
            })
            {
                harmony.Patch(AccessTools.Method(type, name),
                    prefix: Method(nameof(InnerBegin)),
                    postfix: Method(nameof(NamedEnd)));
            }
            Log.Message("[CA][TickProbe] armed: DoSingleTick attribution "
                + "every " + ReportEveryTicks + " ticks");
        }

        private static HarmonyMethod Method(string name)
        {
            return new HarmonyMethod(typeof(CATickAttributionProbe), name);
        }

        private static void TickBegin()
        {
            tickStart = Stopwatch.GetTimestamp();
        }

        private static void BucketBegin()
        {
            bucketStart = Stopwatch.GetTimestamp();
        }

        private static void MapPreEnd()
        {
            mapPre += Stopwatch.GetTimestamp() - bucketStart;
        }

        private static void MapPostEnd()
        {
            mapPost += Stopwatch.GetTimestamp() - bucketStart;
        }

        private static void TickListEnd(TickList __instance)
        {
            long elapsed = Stopwatch.GetTimestamp() - bucketStart;
            switch (tickTypeField(__instance))
            {
                case TickerType.Normal: listNormal += elapsed; break;
                case TickerType.Rare: listRare += elapsed; break;
                default: listLong += elapsed; break;
            }
        }

        private static void WorldEnd()
        {
            world += Stopwatch.GetTimestamp() - bucketStart;
        }

        private static void StoryEnd()
        {
            story += Stopwatch.GetTimestamp() - bucketStart;
        }

        private static void GameCompEnd()
        {
            gameComp += Stopwatch.GetTimestamp() - bucketStart;
        }

        private static void InnerBegin()
        {
            innerStart = Stopwatch.GetTimestamp();
        }

        private static void AnimalSpawnerEnd()
        {
            animalSpawner += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void PlantSpawnerEnd()
        {
            plantSpawner += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void PowerNetsEnd()
        {
            powerNets += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void SteadyEffectsEnd()
        {
            steadyEffects += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void GasEnd()
        {
            gasGrid += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void PollutionEnd()
        {
            pollution += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void LordsEnd()
        {
            lords += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void ResourcesEnd()
        {
            resources += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void FireWatchEnd()
        {
            fireWatch += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void FlecksEnd()
        {
            flecks += Stopwatch.GetTimestamp() - innerStart;
        }

        private static void NamedEnd(System.Reflection.MethodBase
            __originalMethod)
        {
            string key = __originalMethod.DeclaringType?.Name ?? "?";
            postCosts.TryGetValue(key, out long sum);
            postCosts[key] = sum + (Stopwatch.GetTimestamp() - innerStart);
        }

        // Replaces the native component loop with the same loop timed per
        // component type, preserving the native per-component exception
        // isolation exactly.
        private static bool TimedComponentTick(Map map)
        {
            System.Collections.Generic.List<MapComponent> components =
                map.components;
            long loopStart = Stopwatch.GetTimestamp();
            for (int i = 0; i < components.Count; i++)
            {
                long started = Stopwatch.GetTimestamp();
                try
                {
                    components[i].MapComponentTick();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
                string key = components[i].GetType().Name;
                componentCosts.TryGetValue(key, out long sum);
                componentCosts[key] = sum
                    + (Stopwatch.GetTimestamp() - started);
            }
            componentTick += Stopwatch.GetTimestamp() - loopStart;
            return false;
        }

        private static void TickEnd()
        {
            total += Stopwatch.GetTimestamp() - tickStart;
            if (++ticksSeen < ReportEveryTicks) return;
            double scale = 1000d / Stopwatch.Frequency;
            string invariant(long value) => (value * scale / ticksSeen)
                .ToString("F2", CultureInfo.InvariantCulture);
            long accounted = mapPre + listNormal + listRare + listLong
                + world + story + mapPost + gameComp;
            long postNamed = animalSpawner + plantSpawner + powerNets
                + steadyEffects + componentTick + gasGrid + pollution
                + lords + resources + fireWatch + flecks;
            var top = new System.Text.StringBuilder();
            foreach (var pair in postCosts)
            {
                postNamed += pair.Value;
                top.Append("; post:").Append(pair.Key).Append(' ')
                    .Append(invariant(pair.Value));
            }
            int shown = 0;
            foreach (var pair in System.Linq.Enumerable.OrderByDescending(
                componentCosts, item => item.Value))
            {
                if (shown++ >= 6) break;
                top.Append("; comp:").Append(pair.Key).Append(' ')
                    .Append(invariant(pair.Value));
            }
            Log.Message("[CA][TickProbe] " + ticksSeen + " ticks, ms/tick: "
                + "total " + invariant(total)
                + "; mapPre " + invariant(mapPre)
                + "; listNormal " + invariant(listNormal)
                + "; listRare " + invariant(listRare)
                + "; listLong " + invariant(listLong)
                + "; world " + invariant(world)
                + "; story " + invariant(story)
                + "; mapPost " + invariant(mapPost)
                + " [animals " + invariant(animalSpawner)
                + "; plants " + invariant(plantSpawner)
                + "; power " + invariant(powerNets)
                + "; steady " + invariant(steadyEffects)
                + "; components " + invariant(componentTick)
                + "; gas " + invariant(gasGrid)
                + "; pollution " + invariant(pollution)
                + "; lords " + invariant(lords)
                + "; resources " + invariant(resources)
                + "; fireWatch " + invariant(fireWatch)
                + "; flecks " + invariant(flecks)
                + "; postOther " + invariant(mapPost - postNamed) + "]"
                + "; gameComp " + invariant(gameComp)
                + "; other " + invariant(total - accounted)
                + top);
            ticksSeen = 0;
            total = mapPre = listNormal = listRare = listLong = 0;
            world = story = mapPost = gameComp = 0;
            animalSpawner = plantSpawner = powerNets = steadyEffects = 0;
            componentTick = 0;
            gasGrid = pollution = lords = resources = 0;
            fireWatch = flecks = 0;
            componentCosts.Clear();
            postCosts.Clear();
        }
    }
}
