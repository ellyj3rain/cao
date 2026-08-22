using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Initial regional generation can materialize hundreds of thousands of
    // things before later gensteps replace plants, filth, chunks, boulders,
    // ruins, and buildings. Native despawn performs a linear search and a
    // stable shift in every touched ThingOwner and ListerThings list. Keep all
    // removals from one ordered genstep indexed, use O(1) swap removal, and
    // reconstruct the exact stable native order before the genstep returns.
    // Map mutation, callbacks, Rand, spawn order, and genstep order all remain
    // on the main thread.
    internal sealed class CARegionalIndexedThingList
    {
        private static readonly CAReferenceComparer<Thing> Comparer =
            CAReferenceComparer<Thing>.Instance;

        private readonly List<Thing> list;
        private readonly List<Thing> baseline;
        private readonly HashSet<Thing> baselineMembers;
        private readonly HashSet<Thing> removedBaseline;
        private readonly Dictionary<Thing, int> currentIndex;
        private readonly List<Thing> additions = new List<Thing>();
        private readonly Dictionary<Thing, int> lastAddition;

        internal bool Valid { get; private set; }
        internal int SnapshotCount => baseline.Count;

        internal CARegionalIndexedThingList(List<Thing> list)
        {
            this.list = list ?? throw new ArgumentNullException(nameof(list));
            baseline = new List<Thing>(list);
            baselineMembers = new HashSet<Thing>(Comparer);
            removedBaseline = new HashSet<Thing>(Comparer);
            currentIndex = new Dictionary<Thing, int>(list.Count, Comparer);
            lastAddition = new Dictionary<Thing, int>(Comparer);
            Valid = true;
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (thing == null || !baselineMembers.Add(thing)
                    || currentIndex.ContainsKey(thing))
                {
                    Valid = false;
                    continue;
                }
                currentIndex.Add(thing, i);
            }
        }

        internal bool CanRemove(Thing thing)
        {
            if (!Valid || thing == null
                || !currentIndex.TryGetValue(thing, out int index))
                return false;
            return (uint)index < (uint)list.Count
                && ReferenceEquals(list[index], thing);
        }

        internal bool RemoveSwap(Thing thing, bool searchFromEnd,
            out long comparisonsBypassed, out long shiftsBypassed)
        {
            comparisonsBypassed = 0L;
            shiftsBypassed = 0L;
            if (!CanRemove(thing)) return false;
            int index = currentIndex[thing];
            int count = list.Count;
            comparisonsBypassed = searchFromEnd
                ? count - index
                : index + 1L;
            shiftsBypassed = count - index - 1L;
            int lastIndex = count - 1;
            Thing last = list[lastIndex];
            if (index != lastIndex)
            {
                list[index] = last;
                currentIndex[last] = index;
            }
            list.RemoveAt(lastIndex);
            currentIndex.Remove(thing);
            if (baselineMembers.Contains(thing))
                removedBaseline.Add(thing);
            return true;
        }

        internal bool RecordAppend(Thing thing)
        {
            if (!Valid || thing == null || currentIndex.ContainsKey(thing))
                return false;
            int index = list.Count - 1;
            if (index < 0 || !ReferenceEquals(list[index], thing))
                return false;
            currentIndex.Add(thing, index);
            additions.Add(thing);
            lastAddition[thing] = additions.Count - 1;
            return true;
        }

        internal bool RestoreCanonicalOrder()
        {
            if (!Valid) return false;
            var restored = new List<Thing>(list.Count);
            for (int i = 0; i < baseline.Count; i++)
            {
                Thing thing = baseline[i];
                if (!removedBaseline.Contains(thing)
                    && currentIndex.ContainsKey(thing))
                    restored.Add(thing);
            }
            for (int i = 0; i < additions.Count; i++)
            {
                Thing thing = additions[i];
                if (currentIndex.ContainsKey(thing)
                    && lastAddition.TryGetValue(thing, out int last)
                    && last == i)
                    restored.Add(thing);
            }
            if (restored.Count != list.Count) return false;
            var unique = new HashSet<Thing>(restored, Comparer);
            if (unique.Count != restored.Count) return false;
            list.Clear();
            list.AddRange(restored);
            return true;
        }
    }

    internal sealed class CARegionalIndexedLister
    {
        private static readonly AccessTools.FieldRef<ListerThings,
            Dictionary<ThingDef, List<Thing>>> ListsByDef =
                AccessTools.FieldRefAccess<ListerThings,
                    Dictionary<ThingDef, List<Thing>>>("listsByDef");
        private static readonly AccessTools.FieldRef<ListerThings,
            List<Thing>[]> ListsByGroup =
                AccessTools.FieldRefAccess<ListerThings,
                    List<Thing>[]>("listsByGroup");
        private static readonly AccessTools.FieldRef<ListerThings, int[]>
            StateHashByGroup = AccessTools.FieldRefAccess<ListerThings,
                int[]>("stateHashByGroup");
        private static readonly AccessTools.FieldRef<ListerThings,
            List<IHaulSource>> HaulSources =
                AccessTools.FieldRefAccess<ListerThings,
                    List<IHaulSource>>("haulSources");

        private readonly ListerThings lister;
        private readonly Dictionary<List<Thing>, CARegionalIndexedThingList>
            indexed = new Dictionary<List<Thing>,
                CARegionalIndexedThingList>(
                    CAReferenceComparer<List<Thing>>.Instance);
        private readonly List<CARegionalIndexedThingList> targets =
            new List<CARegionalIndexedThingList>(8);

        internal int IndexedListCount => indexed.Count;
        internal long SnapshotEntries => indexed.Values.Sum(
            state => (long)state.SnapshotCount);

        internal CARegionalIndexedLister(ListerThings lister)
        {
            this.lister = lister;
        }

        internal bool TryRemove(Thing thing, out long comparisonsBypassed,
            out long shiftsBypassed)
        {
            comparisonsBypassed = 0L;
            shiftsBypassed = 0L;
            if (thing == null || !ListerThings.EverListable(thing.def,
                    lister.use))
                return false;

            targets.Clear();
            Dictionary<ThingDef, List<Thing>> byDef = ListsByDef(lister);
            if (!byDef.TryGetValue(thing.def, out List<Thing> defList)
                || !Prepare(defList))
                return false;

            ThingRequestGroup[] groups = ThingListGroupHelper.AllGroups;
            List<Thing>[] byGroup = ListsByGroup(lister);
            for (int i = 0; i < groups.Length; i++)
            {
                ThingRequestGroup group = groups[i];
                if ((lister.use == ListerThingsUse.Region
                        && !group.StoreInRegion())
                    || !group.Includes(thing.def))
                    continue;
                if ((uint)i >= (uint)byGroup.Length
                    || !Prepare(byGroup[i]))
                    return false;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (!targets[i].RemoveSwap(thing, false,
                        out long comparisons, out long shifts))
                    return false;
                comparisonsBypassed += comparisons;
                shiftsBypassed += shifts;
                // Native removes a haul source after its per-def list and
                // before its request-group lists. targets[0] is always the
                // per-def list, so preserve that observable order while the
                // large Thing lists use the indexed path.
                if (i == 0 && thing is IHaulSource haulSource)
                    HaulSources(lister).Remove(haulSource);
            }
            int[] hashes = StateHashByGroup(lister);
            for (int i = 0; i < groups.Length; i++)
            {
                ThingRequestGroup group = groups[i];
                if ((lister.use != ListerThingsUse.Region
                        || group.StoreInRegion())
                    && group.Includes(thing.def))
                    hashes[i]++;
            }
            lister.thingListChangedCallbacks?.onThingRemoved?.Invoke(thing);
            return true;
        }

        internal bool RecordAppend(Thing thing)
        {
            foreach (CARegionalIndexedThingList state in indexed.Values)
            {
                List<Thing> list = ListFor(state);
                if (list == null || list.Count == 0
                    || !ReferenceEquals(list[list.Count - 1], thing))
                    continue;
                if (!state.RecordAppend(thing)) return false;
            }
            return true;
        }

        internal bool RestoreCanonicalOrder()
        {
            foreach (CARegionalIndexedThingList state in indexed.Values)
                if (!state.RestoreCanonicalOrder()) return false;
            return true;
        }

        private bool Prepare(List<Thing> list)
        {
            if (list == null) return false;
            if (!indexed.TryGetValue(list,
                    out CARegionalIndexedThingList state))
            {
                state = new CARegionalIndexedThingList(list);
                indexed.Add(list, state);
            }
            if (!state.CanRemove(CARegionalInitialRemovalLifecyclePatch
                    .PendingRemoval))
                return false;
            if (!targets.Contains(state)) targets.Add(state);
            return true;
        }

        private List<Thing> ListFor(CARegionalIndexedThingList wanted)
        {
            foreach (KeyValuePair<List<Thing>,
                CARegionalIndexedThingList> pair in indexed)
                if (ReferenceEquals(pair.Value, wanted)) return pair.Key;
            return null;
        }
    }

    internal sealed class CARegionalInitialRemovalContext
    {
        private delegate void NotifyOwnerRemoved(
            ThingOwner<Thing> owner, Thing thing);

        private static readonly NotifyOwnerRemoved OwnerRemoved =
            CreateOwnerRemovedDelegate();
        private readonly Stopwatch indexWatch = new Stopwatch();
        private readonly Map map;
        private readonly string stepLabel;
        private readonly Dictionary<ThingOwner<Thing>,
            CARegionalIndexedThingList> owners =
                new Dictionary<ThingOwner<Thing>,
                    CARegionalIndexedThingList>(
                        CAReferenceComparer<ThingOwner<Thing>>.Instance);
        private readonly Dictionary<ListerThings, CARegionalIndexedLister>
            listers = new Dictionary<ListerThings,
                CARegionalIndexedLister>(
                    CAReferenceComparer<ListerThings>.Instance);

        private bool disabled;
        private bool restored;
        private bool receiptLogged;
        private bool externalIndexesSuspended;
        private string fallbackReason;
        private bool restoredCanonical;
        private long restoredSnapshotEntries;
        private int restoredIndexedLists;
        private long restoredInMilliseconds;
        private long removedPlants;
        private long ownerRemovals;
        private long listerRemovals;
        private long comparisonsBypassed;
        private long shiftsBypassed;

        internal Map Map => map;
        internal bool Disabled => disabled;

        internal CARegionalInitialRemovalContext(Map map, string stepLabel)
        {
            this.map = map;
            this.stepLabel = stepLabel;
            if (OwnerRemoved == null)
                Disable("ThingOwner.NotifyRemoved delegate unavailable");
            else
                SuspendExternalIndexes();
        }

        internal bool TryRemoveOwner(ThingOwner<Thing> owner, Thing thing)
        {
            if (!EligibleRemoval(thing) || owner == null
                || !ReferenceEquals(owner.Owner, map)
                || !ReferenceEquals(map.spawnedThings, owner))
                return false;
            indexWatch.Start();
            if (!owners.TryGetValue(owner,
                    out CARegionalIndexedThingList state))
            {
                state = new CARegionalIndexedThingList(
                    owner.InnerListForReading);
                owners.Add(owner, state);
            }
            indexWatch.Stop();
            if (!state.CanRemove(thing))
            {
                Disable("spawned-things index mismatch");
                return false;
            }
            thing.holdingOwner = null;
            if (!state.RemoveSwap(thing, true, out long comparisons,
                    out long shifts))
            {
                Disable("spawned-things swap removal failed");
                return false;
            }
            OwnerRemoved(owner, thing);
            ownerRemovals++;
            comparisonsBypassed += comparisons;
            shiftsBypassed += shifts;
            return true;
        }

        internal void RecordOwnerAdd(ThingOwner<Thing> owner, Thing thing)
        {
            if (disabled || owner == null || thing == null
                || !ReferenceEquals(owner.Owner, map)
                || !owners.TryGetValue(owner,
                    out CARegionalIndexedThingList state)) return;
            if (!state.RecordAppend(thing))
                Disable("spawned-things append tracking failed");
        }

        internal bool TryRemoveLister(ListerThings lister, Thing thing)
        {
            if (!EligibleRemoval(thing) || lister == null) return false;
            indexWatch.Start();
            if (!listers.TryGetValue(lister,
                    out CARegionalIndexedLister state))
            {
                state = new CARegionalIndexedLister(lister);
                listers.Add(lister, state);
            }
            CARegionalInitialRemovalLifecyclePatch.PendingRemoval = thing;
            bool removed;
            long comparisons;
            long shifts;
            try
            {
                removed = state.TryRemove(thing, out comparisons,
                    out shifts);
            }
            finally
            {
                CARegionalInitialRemovalLifecyclePatch.PendingRemoval = null;
                indexWatch.Stop();
            }
            if (!removed)
            {
                Disable("lister index mismatch");
                return false;
            }
            listerRemovals++;
            comparisonsBypassed += comparisons;
            shiftsBypassed += shifts;
            return true;
        }

        internal void RecordListerAdd(ListerThings lister, Thing thing)
        {
            if (disabled || lister == null || thing == null
                || !listers.TryGetValue(lister,
                    out CARegionalIndexedLister state)) return;
            if (!state.RecordAppend(thing))
                Disable("lister append tracking failed");
        }

        internal void NotePlantDespawn(Thing thing)
        {
            if (!disabled && thing?.def?.category == ThingCategory.Plant
                && ReferenceEquals(thing.Map, map))
                removedPlants++;
        }

        internal void RestoreAndLog(bool completed)
        {
            FinishCollections();
            if (receiptLogged) return;
            receiptLogged = true;
            if (ownerRemovals == 0L && listerRemovals == 0L
                && fallbackReason == null) return;
            Log.Message("[CA][Regional][Timing][IndexedRemoval] "
                + stepLabel + " " + (completed ? "completed" : "aborted")
                + " with plantDespawns=" + removedPlants
                + " ownerRemovals=" + ownerRemovals
                + " listerRemovals=" + listerRemovals
                + " indexedLists=" + restoredIndexedLists
                + " snapshotEntries=" + restoredSnapshotEntries
                + " indexWork=" + indexWatch.ElapsedMilliseconds + " ms"
                + " restore=" + restoredInMilliseconds + " ms"
                + " comparisonsBypassed=" + comparisonsBypassed
                + " shiftsBypassed=" + shiftsBypassed
                + " canonicalOrder=" + restoredCanonical
                + (fallbackReason == null ? ""
                    : "; FALLBACK " + fallbackReason)
                + " for " + map.Size.x + "x" + map.Size.z + " map "
                + map.uniqueID);
        }

        private bool EligibleRemoval(Thing thing)
        {
            return !disabled && thing != null
                && ReferenceEquals(thing.Map, map);
        }

        private void Disable(string reason)
        {
            if (disabled) return;
            fallbackReason = reason;
            FinishCollections();
            if (!restoredCanonical)
                fallbackReason += "; canonical restore failed";
            disabled = true;
        }

        private void FinishCollections()
        {
            if (restored) return;
            restored = true;
            restoredSnapshotEntries = owners.Values.Sum(
                    state => (long)state.SnapshotCount)
                + listers.Values.Sum(state => state.SnapshotEntries);
            restoredIndexedLists = owners.Count
                + listers.Values.Sum(state => state.IndexedListCount);
            Stopwatch restoreWatch = Stopwatch.StartNew();
            restoredCanonical = RestoreCollections();
            restoreWatch.Stop();
            restoredInMilliseconds = restoreWatch.ElapsedMilliseconds;
            ResumeExternalIndexes();
        }

        private void SuspendExternalIndexes()
        {
            if (externalIndexesSuspended) return;
            CARegionalSpawnPathPatches.SuspendIndexForStep(map);
            CARegionalMapListerIndexPatches.SuspendIndexForStep(map);
            externalIndexesSuspended = true;
        }

        private void ResumeExternalIndexes()
        {
            if (!externalIndexesSuspended) return;
            // The per-step collections have already been restored to their
            // canonical stable order. Long-lived generation indexes may now
            // rebuild from that exact state instead of observing temporary
            // swap positions and degrading for the rest of generation.
            CARegionalSpawnPathPatches.ResumeIndexAfterStep(map);
            CARegionalMapListerIndexPatches.ResumeIndexAfterStep(map);
            externalIndexesSuspended = false;
        }

        private bool RestoreCollections()
        {
            bool valid = true;
            foreach (CARegionalIndexedThingList state in owners.Values)
                valid &= state.RestoreCanonicalOrder();
            foreach (CARegionalIndexedLister state in listers.Values)
                valid &= state.RestoreCanonicalOrder();
            return valid;
        }

        private static NotifyOwnerRemoved CreateOwnerRemovedDelegate()
        {
            try
            {
                MethodInfo method = AccessTools.Method(
                    typeof(ThingOwner<Thing>), "NotifyRemoved");
                return method == null ? null
                    : (NotifyOwnerRemoved)Delegate.CreateDelegate(
                        typeof(NotifyOwnerRemoved), method);
            }
            catch
            {
                return null;
            }
        }
    }

    [HarmonyPatch]
    internal static class CARegionalInitialRemovalLifecyclePatch
    {
        [ThreadStatic] private static Stack<
            CARegionalInitialRemovalContext> active;
        [ThreadStatic] internal static Thing PendingRemoval;

        internal static CARegionalInitialRemovalContext Current(Map map)
        {
            if (map == null || active == null) return null;
            foreach (CARegionalInitialRemovalContext context in active)
                if (context != null && !context.Disabled
                    && ReferenceEquals(context.Map, map)) return context;
            return null;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(type => type != null && !type.IsAbstract
                    && typeof(GenStep).IsAssignableFrom(type))
                .Select(type => AccessTools.DeclaredMethod(type,
                    nameof(GenStep.Generate), new[]
                    {
                        typeof(Map), typeof(GenStepParams)
                    }))
                .Where(method => method != null)
                .Distinct();
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(GenStep __instance,
            MethodBase __originalMethod, Map __0,
            out CARegionalInitialRemovalContext __state)
        {
            if (active == null)
                active = new Stack<CARegionalInitialRemovalContext>();
            __state = null;
            Map map = __0;
            try
            {
                if (map != null && CARegionalRiverPatchUtility.Active(map)
                    && CARegionalInitialRegionRegistrationLifecyclePatch
                        .For(map) != null
                    && Current(map) == null)
                {
                    string type = __originalMethod?.DeclaringType?.FullName
                        ?? __instance?.GetType().FullName ?? "unknown";
                    string defName = __instance?.def?.defName;
                    __state = new CARegionalInitialRemovalContext(map,
                        string.IsNullOrEmpty(defName)
                            ? type : type + " [" + defName + "]");
                }
            }
            catch (Exception error)
            {
                Log.Warning("[CA][Regional][Timing][IndexedRemoval] setup "
                    + "failed; native removal retained: " + error);
                __state = null;
            }
            active.Push(__state);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CARegionalInitialRemovalContext __state)
        {
            __state?.RestoreAndLog(true);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,
            CARegionalInitialRemovalContext __state)
        {
            try
            {
                __state?.RestoreAndLog(__exception == null);
            }
            catch (Exception error)
            {
                Log.Error("[CA][Regional][Timing][IndexedRemoval] final "
                    + "canonical restore failed: " + error);
            }
            finally
            {
                if (active != null && active.Count > 0) active.Pop();
                if (active?.Count == 0) active = null;
                PendingRemoval = null;
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    internal static class CARegionalInitialPlantDespawnReceiptPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Thing __instance)
        {
            Map map = __instance?.Map;
            CARegionalInitialRemovalLifecyclePatch.Current(map)
                ?.NotePlantDespawn(__instance);
        }
    }

    [HarmonyPatch(typeof(ThingOwner<Thing>),
        nameof(ThingOwner<Thing>.Remove))]
    internal static class CARegionalInitialOwnerRemovalPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(ThingOwner<Thing> __instance, Thing item,
            ref bool __result)
        {
            Map map = __instance?.Owner as Map;
            CARegionalInitialRemovalContext context =
                CARegionalInitialRemovalLifecyclePatch.Current(map);
            if (context == null || !context.TryRemoveOwner(__instance, item))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ThingOwner<Thing>),
        nameof(ThingOwner<Thing>.TryAdd),
        new[] { typeof(Thing), typeof(bool) })]
    internal static class CARegionalInitialOwnerAppendPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ThingOwner<Thing> __instance, Thing item,
            bool __result)
        {
            if (!__result || item == null) return;
            Map map = __instance?.Owner as Map;
            CARegionalInitialRemovalLifecyclePatch.Current(map)
                ?.RecordOwnerAdd(__instance, item);
        }
    }

    [HarmonyPatch(typeof(ListerThings), nameof(ListerThings.Remove))]
    internal static class CARegionalInitialListerRemovalPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(ListerThings __instance, Thing t)
        {
            Map map = t?.Map;
            CARegionalInitialRemovalContext context =
                CARegionalInitialRemovalLifecyclePatch.Current(map);
            return context == null
                || !context.TryRemoveLister(__instance, t);
        }
    }

    [HarmonyPatch(typeof(ListerThings), nameof(ListerThings.Add))]
    internal static class CARegionalInitialListerAppendPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ListerThings __instance, Thing t)
        {
            Map map = t?.Map;
            CARegionalInitialRemovalLifecyclePatch.Current(map)
                ?.RecordListerAdd(__instance, t);
        }
    }

    internal sealed class CAReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        internal static readonly CAReferenceComparer<T> Instance =
            new CAReferenceComparer<T>();

        public bool Equals(T left, T right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(T value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }
    }
}
