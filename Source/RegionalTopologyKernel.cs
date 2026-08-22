using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // THE WORLD-WIDE REGIONAL PARTITION, as a pure function. Every eligible
    // surface tile receives deterministic membership in exactly one region;
    // ineligible tiles (water, impassable) carry an explicit non-regional
    // status by omission. The kernel owns only arithmetic and graph growth:
    // no Verse, no Unity, no engine state. The world adapter supplies tile
    // ids, adjacency, and positions; receipts supply synthetic graphs. Both
    // consume the same deterministic result, which is what makes the
    // partition testable before it is ever rendered.
    //
    // Determinism contract: the result is a pure function of (seed, the
    // eligible set, adjacency, positions, multiTileLandShare, sizeMin,
    // sizeMax, preAssigned). No global RNG is read; every draw is an FNV
    // fold of the seed and a subject, the same idiom as
    // CAWorldTendencyCausalKernel. Callers must pass deterministic inputs.
    public static class CARegionalTopologyKernel
    {
        public sealed class RegionSeed
        {
            public int RootTileId;
            public List<int> MemberTileIds;
        }

        // FNV-1a fold, matching the project's deterministic-unit idiom.
        private static uint Fold(uint state, int value)
        {
            unchecked
            {
                state ^= (uint)value;
                state *= 16777619u;
                return state;
            }
        }

        public static float Unit(int seed, int subject, int salt)
        {
            uint state = 2166136261u;
            state = Fold(state, seed);
            state = Fold(state, subject);
            state = Fold(state, salt);
            // 24 bits of mantissa; uniform in [0, 1).
            return (state >> 8) * (1f / 16777216f);
        }

        // Partition every eligible tile not already assigned. The realized
        // multi-tile land share is met exactly by construction: a
        // deterministic feedback controller compares the running share of
        // land inside multi-tile regions against the target before every
        // seed decision, so the partition converges on the authored share
        // instead of merely sampling around it.
        public static List<RegionSeed> Partition(int seed,
            IReadOnlyList<int> eligibleTiles,
            Func<int, IReadOnlyList<int>> neighborsOf,
            Func<int, (float x, float y, float z)> positionOf,
            float multiTileLandShare, int sizeMin, int sizeMax,
            ISet<int> preAssigned = null)
        {
            var result = new List<RegionSeed>();
            if (eligibleTiles == null || eligibleTiles.Count == 0)
                return result;
            float share = multiTileLandShare < 0f ? 0f
                : multiTileLandShare > 1f ? 1f : multiTileLandShare;
            int low = Math.Max(1, Math.Min(sizeMin, sizeMax));
            int high = Math.Max(low, Math.Max(sizeMin, sizeMax));

            var eligible = new HashSet<int>(eligibleTiles);
            if (preAssigned != null) eligible.ExceptWith(preAssigned);
            // Deterministic shuffle: seeds visit tiles in hashed order so
            // region roots scatter across the world instead of marching
            // through tile-id order.
            List<int> order = eligible
                .OrderBy(id => Unit(seed, id, 733181))
                .ThenBy(id => id).ToList();

            var assigned = new HashSet<int>();
            long multiLand = 0;
            long totalLand = 0;
            foreach (int rootId in order)
            {
                if (assigned.Contains(rootId)) continue;
                bool multi = totalLand == 0
                    ? Unit(seed, rootId, 415291) < share
                    : multiLand < share * totalLand;
                int target = 1;
                if (multi && high > 1)
                {
                    int span = high - Math.Max(2, low) + 1;
                    target = span <= 0 ? high
                        : Math.Max(2, low) + (int)(Unit(seed, rootId,
                            982451) * span);
                    if (target > high) target = high;
                }
                List<int> members = Grow(rootId, target, eligible, assigned,
                    neighborsOf, positionOf);
                foreach (int id in members) assigned.Add(id);
                result.Add(new RegionSeed
                {
                    RootTileId = rootId,
                    MemberTileIds = members
                });
                totalLand += members.Count;
                if (members.Count > 1) multiLand += members.Count;
            }
            MergeUndersized(result, neighborsOf, low, high);
            return result;
        }

        // Packing leaves crumbs: late seeds grow into pockets smaller than
        // the span band's floor. A joined region below the floor merges
        // into the smallest adjacent region that keeps the result inside
        // the band, repeated to the fixpoint; what remains undersized after
        // that is genuinely blocked geography, not packing debris.
        private static void MergeUndersized(List<RegionSeed> regions,
            Func<int, IReadOnlyList<int>> neighborsOf, int low, int high)
        {
            int floor = Math.Max(2, low);
            if (floor <= 2 && high <= 2) return;
            var ownerOf = new Dictionary<int, RegionSeed>();
            foreach (RegionSeed region in regions)
                foreach (int id in region.MemberTileIds)
                    ownerOf[id] = region;
            bool merged = true;
            while (merged)
            {
                merged = false;
                foreach (RegionSeed region in regions
                    .Where(item => item.MemberTileIds.Count > 1
                        && item.MemberTileIds.Count < floor)
                    .OrderBy(item => item.RootTileId).ToList())
                {
                    if (!regions.Contains(region)
                        || region.MemberTileIds.Count >= floor) continue;
                    RegionSeed best = null;
                    foreach (int memberId in region.MemberTileIds)
                    {
                        IReadOnlyList<int> around = neighborsOf(memberId);
                        for (int i = 0; i < around.Count; i++)
                        {
                            if (!ownerOf.TryGetValue(around[i],
                                    out RegionSeed other)
                                || other == region) continue;
                            int combined = region.MemberTileIds.Count
                                + other.MemberTileIds.Count;
                            if (combined > high) continue;
                            if (best == null
                                || other.MemberTileIds.Count
                                    < best.MemberTileIds.Count
                                || (other.MemberTileIds.Count
                                        == best.MemberTileIds.Count
                                    && other.RootTileId < best.RootTileId))
                                best = other;
                        }
                    }
                    if (best == null) continue;
                    best.MemberTileIds.AddRange(region.MemberTileIds);
                    foreach (int id in region.MemberTileIds)
                        ownerOf[id] = best;
                    regions.Remove(region);
                    merged = true;
                }
            }
        }

        // Cohesion growth, mirroring the composer's bundle semantics:
        // prefer the frontier tile touching the most already-chosen members,
        // then the most compact result, then the lowest tile id.
        private static List<int> Grow(int rootId, int target,
            HashSet<int> eligible, HashSet<int> assigned,
            Func<int, IReadOnlyList<int>> neighborsOf,
            Func<int, (float x, float y, float z)> positionOf)
        {
            var members = new List<int> { rootId };
            if (target <= 1) return members;
            var chosen = new HashSet<int> { rootId };
            var frontier = new HashSet<int>();
            (float x, float y, float z) rootPos = positionOf(rootId);
            AddFrontier(rootId);
            while (members.Count < target && frontier.Count > 0)
            {
                int best = -1;
                int bestTouch = -1;
                float bestSpan = float.MaxValue;
                foreach (int candidate in frontier)
                {
                    int touch = 0;
                    IReadOnlyList<int> around = neighborsOf(candidate);
                    for (int i = 0; i < around.Count; i++)
                        if (chosen.Contains(around[i])) touch++;
                    float span = DistanceSquared(rootPos,
                        positionOf(candidate));
                    if (touch > bestTouch
                        || (touch == bestTouch && span < bestSpan - 1e-9f)
                        || (touch == bestTouch
                            && Math.Abs(span - bestSpan) <= 1e-9f
                            && (best < 0 || candidate < best)))
                    {
                        best = candidate;
                        bestTouch = touch;
                        bestSpan = span;
                    }
                }
                if (best < 0) break;
                frontier.Remove(best);
                chosen.Add(best);
                members.Add(best);
                AddFrontier(best);
            }
            return members;

            void AddFrontier(int from)
            {
                IReadOnlyList<int> around = neighborsOf(from);
                for (int i = 0; i < around.Count; i++)
                {
                    int next = around[i];
                    if (!eligible.Contains(next) || assigned.Contains(next)
                        || chosen.Contains(next)) continue;
                    frontier.Add(next);
                }
            }
        }

        private static float DistanceSquared(
            (float x, float y, float z) a, (float x, float y, float z) b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }

        // Split a member set into connected components, deterministically
        // ordered: largest first, ties by lowest member id. Used when an
        // authored footprint carves tiles out of existing topology regions
        // and the remainders must become well-formed regions again.
        public static List<List<int>> SplitComponents(
            IReadOnlyCollection<int> members,
            Func<int, IReadOnlyList<int>> neighborsOf)
        {
            var remaining = new HashSet<int>(members
                ?? (IReadOnlyCollection<int>)Array.Empty<int>());
            var components = new List<List<int>>();
            while (remaining.Count > 0)
            {
                int start = int.MaxValue;
                foreach (int id in remaining)
                    if (id < start) start = id;
                var component = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                remaining.Remove(start);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    component.Add(current);
                    IReadOnlyList<int> around = neighborsOf(current);
                    for (int i = 0; i < around.Count; i++)
                    {
                        int next = around[i];
                        if (!remaining.Contains(next)) continue;
                        remaining.Remove(next);
                        queue.Enqueue(next);
                    }
                }
                component.Sort();
                components.Add(component);
            }
            return components
                .OrderByDescending(component => component.Count)
                .ThenBy(component => component[0]).ToList();
        }
    }
}
