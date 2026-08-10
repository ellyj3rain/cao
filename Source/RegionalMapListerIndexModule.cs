using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace ColonistAwareness
{
    // ListerThings.Remove performs a linear List.Remove in the per-def list
    // and in every matching request-group list. On a regional map those lists
    // can each contain more than a million things, and every plant wiped by a
    // later scatter or ReGrowth formation repeats those scans. Preserve the
    // native lists, stable RemoveAt shifts, callbacks, hashes, and call order;
    // replace only the equality scan with the same audited append/removal
    // index used for Map.spawnedThings.
    internal sealed class CARegionalMapListerIndexScope
    {
        private const int MinimumTrackedCount = 4096;

        private readonly Dictionary<List<Thing>,
            CARegionalOrderedThingListIndex> indexes =
                new Dictionary<List<Thing>,
                    CARegionalOrderedThingListIndex>(
                        CARegionalThingListReferenceComparer.Instance);
        private readonly HashSet<List<Thing>> disabled =
            new HashSet<List<Thing>>(
                CARegionalThingListReferenceComparer.Instance);
        private readonly List<string> disableReasons = new List<string>();
        private readonly Stopwatch buildWatch = new Stopwatch();
        private bool degraded;
        private string degradeReason;

        internal readonly Map map;
        internal readonly ListerThings mapLister;
        internal long served;
        internal long sampledAudits;
        internal long auditMismatches;
        internal long nativeFallbacks;
        internal long appended;

        internal CARegionalMapListerIndexScope(Map map)
        {
            this.map = map;
            mapLister = map?.listerThings;
        }

        internal void RecordAppend(List<Thing> list, Thing thing)
        {
            if (degraded || list == null || thing == null
                || disabled.Contains(list)
                || !indexes.TryGetValue(list,
                    out CARegionalOrderedThingListIndex index)) return;
            try
            {
                if (!index.NotifyAppended(thing))
                    Disable(list, "append notification rejected");
                else
                    appended++;
            }
            catch (Exception)
            {
                DegradeAll("append tracking failed");
            }
        }

        internal bool Remove(List<Thing> list, Thing thing, int auditStride)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (degraded || thing == null || disabled.Contains(list)
                || list.Count < MinimumTrackedCount
                    && !indexes.ContainsKey(list))
            {
                nativeFallbacks++;
                return list.Remove(thing);
            }

            CARegionalOrderedThingListIndex index;
            if (!indexes.TryGetValue(list, out index))
            {
                try
                {
                    buildWatch.Start();
                    index = new CARegionalOrderedThingListIndex(
                        checked(list.Count + 16));
                    if (!index.Rebuild(list))
                    {
                        buildWatch.Stop();
                        Disable(list, "initial list audit rejected");
                        nativeFallbacks++;
                        return list.Remove(thing);
                    }
                    buildWatch.Stop();
                    indexes.Add(list, index);
                }
                catch (Exception)
                {
                    if (buildWatch.IsRunning) buildWatch.Stop();
                    DegradeAll("index build failed");
                    nativeFallbacks++;
                    return list.Remove(thing);
                }
            }

            bool removed = false;
            try
            {
                if (!index.TryCurrentIndexOf(thing, out int at)
                    || (uint)at >= (uint)list.Count
                    || !ReferenceEquals(list[at], thing))
                {
                    Disable(list, "computed index unavailable");
                    nativeFallbacks++;
                    return list.Remove(thing);
                }
                if (auditStride > 0 && (served + 1) % auditStride == 0)
                {
                    int native = list.IndexOf(thing);
                    sampledAudits++;
                    if (native != at)
                    {
                        auditMismatches++;
                        Disable(list, "sampled audit mismatch");
                        nativeFallbacks++;
                        return list.Remove(thing);
                    }
                }

                list.RemoveAt(at);
                removed = true;
                if (!index.NotifyRemoved(thing))
                    Disable(list, "removal notification rejected");
                served++;
                return true;
            }
            catch (Exception)
            {
                DegradeAll("indexed removal failed");
                if (removed) return true;
                nativeFallbacks++;
                return list.Remove(thing);
            }
        }

        internal void LogFinal(Exception exception)
        {
            Stopwatch auditWatch = Stopwatch.StartNew();
            bool exact = !degraded;
            long entries = 0L;
            foreach (KeyValuePair<List<Thing>,
                CARegionalOrderedThingListIndex> pair in indexes)
            {
                if (degraded || disabled.Contains(pair.Key)) continue;
                entries += pair.Key.Count;
                if (pair.Value.AuditAgainst(pair.Key) != null) exact = false;
            }
            auditWatch.Stop();
            string reasons = degraded
                ? "; globalFallback=" + (degradeReason ?? "unknown")
                : disableReasons.Count == 0 ? ""
                    : "; fallbacks=" + string.Join(" | ",
                        disableReasons.ToArray());
            Log.Message("[CA][Regional][Jobs][MapListerIndex] scope "
                + (exception == null ? "completed" : "aborted after "
                    + exception.GetType().Name)
                + " for map " + map.uniqueID
                + ": trackedLists=" + indexes.Count
                + " activeEntries=" + entries
                + " served=" + served
                + " appended=" + appended
                + " sampledAudits=" + sampledAudits
                + " auditMismatches=" + auditMismatches
                + " nativeFallbacks=" + nativeFallbacks
                + " build=" + buildWatch.ElapsedMilliseconds + " ms"
                + " finalAudit=" + auditWatch.ElapsedMilliseconds + " ms"
                + " exact=" + exact + reasons);
        }

        private void Disable(List<Thing> list, string reason)
        {
            if (list == null || !disabled.Add(list)) return;
            if (disableReasons.Count < 8)
                disableReasons.Add("count=" + list.Count + " " + reason);
        }

        private void DegradeAll(string reason)
        {
            // Callers pass constants so the fallback itself does not allocate
            // after a tracker failure under memory pressure.
            degraded = true;
            degradeReason = reason;
        }
    }

    internal sealed class CARegionalThingListReferenceComparer
        : IEqualityComparer<List<Thing>>
    {
        internal static readonly CARegionalThingListReferenceComparer
            Instance = new CARegionalThingListReferenceComparer();

        public bool Equals(List<Thing> left, List<Thing> right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(List<Thing> value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }
    }

    internal static class CARegionalMapListerIndexPatches
    {
        internal static bool AddTranspilerApplied;
        internal static bool RemoveTranspilerApplied;

        [ThreadStatic] private static Stack<
            CARegionalMapListerIndexScope> generationScopes;
        [ThreadStatic] private static Stack<ListerThings> listerCalls;

        private static readonly int AuditStride = ReadAuditStride();

        private static CARegionalMapListerIndexScope CurrentScope
        {
            get
            {
                return generationScopes != null
                    && generationScopes.Count > 0
                        ? generationScopes.Peek() : null;
            }
        }

        private static ListerThings CurrentLister
        {
            get
            {
                return listerCalls != null && listerCalls.Count > 0
                    ? listerCalls.Peek() : null;
            }
        }

        internal static void ContentsPrefix(Map map, out bool __state)
        {
            __state = false;
            if (generationScopes == null)
                generationScopes = new Stack<
                    CARegionalMapListerIndexScope>();
            generationScopes.Push(map != null
                    && CARegionalRiverPatchUtility.Active(map)
                    && map.listerThings != null
                ? new CARegionalMapListerIndexScope(map) : null);
            __state = true;
        }

        internal static Exception ContentsFinalizer(Exception __exception,
            bool __state)
        {
            if (!__state || generationScopes == null
                || generationScopes.Count == 0)
                return __exception;
            CARegionalMapListerIndexScope scope = generationScopes.Pop();
            if (generationScopes.Count == 0) generationScopes = null;
            if (scope != null)
            {
                try
                {
                    scope.LogFinal(__exception);
                }
                catch (Exception error)
                {
                    Log.Warning("[CA][Regional][Jobs][MapListerIndex] "
                        + "final receipt failed; generation unaffected: "
                        + error);
                }
            }
            return __exception;
        }

        internal static void ListerCallPrefix(ListerThings __instance,
            out bool __state)
        {
            __state = false;
            // ListerThings is global and extremely hot. Outside an active
            // regional generation there is nothing for the routed calls to
            // identify, so avoid touching thread-local state at all. During
            // regional generation keep one cached stack: callbacks can nest
            // map and region listers, but ordinary sequential calls must not
            // allocate a Stack and its backing array for every registration.
            if (CurrentScope == null) return;
            if (listerCalls == null)
                listerCalls = new Stack<ListerThings>(4);
            listerCalls.Push(__instance);
            __state = true;
        }

        internal static Exception ListerCallFinalizer(
            Exception __exception, bool __state)
        {
            if (__state && listerCalls != null && listerCalls.Count > 0)
                listerCalls.Pop();
            return __exception;
        }

        internal static void RoutedAdd(List<Thing> list, Thing thing)
        {
            list.Add(thing);
            CARegionalMapListerIndexScope scope = CurrentScope;
            if (scope != null
                && ReferenceEquals(CurrentLister, scope.mapLister))
                scope.RecordAppend(list, thing);
        }

        internal static bool RoutedRemove(List<Thing> list, Thing thing)
        {
            CARegionalMapListerIndexScope scope = CurrentScope;
            return scope != null
                    && ReferenceEquals(CurrentLister, scope.mapLister)
                ? scope.Remove(list, thing, AuditStride)
                : list.Remove(thing);
        }

        internal static IEnumerable<CodeInstruction> AddTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            AddTranspilerApplied = false;
            MethodInfo native = AccessTools.Method(typeof(List<Thing>),
                nameof(List<Thing>.Add), new[] { typeof(Thing) });
            MethodInfo routed = AccessTools.Method(
                typeof(CARegionalMapListerIndexPatches),
                nameof(RoutedAdd));
            return Rewrite(instructions, native, routed, 2,
                value => AddTranspilerApplied = value, "Add");
        }

        internal static IEnumerable<CodeInstruction> RemoveTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            RemoveTranspilerApplied = false;
            MethodInfo native = AccessTools.Method(typeof(List<Thing>),
                nameof(List<Thing>.Remove), new[] { typeof(Thing) });
            MethodInfo routed = AccessTools.Method(
                typeof(CARegionalMapListerIndexPatches),
                nameof(RoutedRemove));
            return Rewrite(instructions, native, routed, 2,
                value => RemoveTranspilerApplied = value, "Remove");
        }

        private static IEnumerable<CodeInstruction> Rewrite(
            IEnumerable<CodeInstruction> instructions, MethodInfo native,
            MethodInfo routed, int expected, Action<bool> setApplied,
            string label)
        {
            var buffer = new List<CodeInstruction>(instructions);
            int matches = 0;
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i].Calls(native)) matches++;
            if (matches != expected)
            {
                Log.Warning("[CA][Regional][Jobs][MapListerIndex] expected "
                    + expected + " List<Thing>." + label
                    + " sites in ListerThings." + label + ", found "
                    + matches + "; native IL retained");
                setApplied(false);
                return buffer;
            }
            for (int i = 0; i < buffer.Count; i++)
            {
                if (!buffer[i].Calls(native)) continue;
                buffer[i].opcode = OpCodes.Call;
                buffer[i].operand = routed;
            }
            setApplied(true);
            return buffer;
        }

        private static int ReadAuditStride()
        {
            string value = Environment.GetEnvironmentVariable(
                "CA_REGIONAL_LISTER_INDEX_AUDIT");
            return value != null && int.TryParse(value, out int parsed)
                && parsed >= 0 && parsed <= 1000000 ? parsed : 256;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CARegionalMapListerIndexInstaller
    {
        private const string HarmonyId =
            "ellyj3rain.colonistawareness.regionaljobs.maplisterindex";

        static CARegionalMapListerIndexInstaller()
        {
            if (string.Equals(Environment.GetEnvironmentVariable(
                    "CA_REGIONAL_LISTER_INDEX"), "0",
                    StringComparison.Ordinal))
            {
                Log.Message("[CA][Regional][Jobs][MapListerIndex] disabled "
                    + "by CA_REGIONAL_LISTER_INDEX=0");
                return;
            }
            Harmony harmony = null;
            try
            {
                MethodInfo contents = AccessTools.Method(
                    typeof(MapGenerator),
                    nameof(MapGenerator.GenerateContentsIntoMap));
                MethodInfo add = AccessTools.Method(typeof(ListerThings),
                    nameof(ListerThings.Add));
                MethodInfo remove = AccessTools.Method(
                    typeof(ListerThings), nameof(ListerThings.Remove));
                if (contents == null || add == null || remove == null)
                    throw new MissingMethodException(
                        "regional map-lister targets not found");

                harmony = new Harmony(HarmonyId);
                harmony.Patch(contents,
                    prefix: Method(nameof(
                        CARegionalMapListerIndexPatches.ContentsPrefix)),
                    finalizer: Method(nameof(
                        CARegionalMapListerIndexPatches
                            .ContentsFinalizer)));
                harmony.Patch(add,
                    prefix: Method(nameof(
                        CARegionalMapListerIndexPatches
                            .ListerCallPrefix)),
                    finalizer: Method(nameof(
                        CARegionalMapListerIndexPatches
                            .ListerCallFinalizer)),
                    transpiler: Method(nameof(
                        CARegionalMapListerIndexPatches.AddTranspiler)));
                harmony.Patch(remove,
                    prefix: Method(nameof(
                        CARegionalMapListerIndexPatches
                            .ListerCallPrefix)),
                    finalizer: Method(nameof(
                        CARegionalMapListerIndexPatches
                            .ListerCallFinalizer)),
                    transpiler: Method(nameof(
                        CARegionalMapListerIndexPatches
                            .RemoveTranspiler)));

                Log.Message("[CA][Regional][Jobs][MapListerIndex] installed; "
                    + "stable-order Add="
                    + CARegionalMapListerIndexPatches.AddTranspilerApplied
                    + " Remove="
                    + CARegionalMapListerIndexPatches
                        .RemoveTranspilerApplied
                    + "; lists below 4096 entries stay native; env "
                    + "CA_REGIONAL_LISTER_INDEX=0 disables and "
                    + "CA_REGIONAL_LISTER_INDEX_AUDIT=n sets sampling");
            }
            catch (Exception error)
            {
                if (harmony != null)
                {
                    try
                    {
                        harmony.UnpatchAll(HarmonyId);
                    }
                    catch (Exception rollbackError)
                    {
                        Log.Error("[CA][Regional][Jobs][MapListerIndex] "
                            + "rollback failed: " + rollbackError);
                    }
                }
                Log.Error("[CA][Regional][Jobs][MapListerIndex] install "
                    + "failed; native ListerThings retained: " + error);
            }
        }

        private static HarmonyMethod Method(string name)
        {
            return new HarmonyMethod(AccessTools.Method(
                typeof(CARegionalMapListerIndexPatches), name));
        }
    }
}
