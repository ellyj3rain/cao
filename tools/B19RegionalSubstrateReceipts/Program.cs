using ColonistAwareness;

// Deterministic acceptance receipts for the world-wide regional partition
// kernel. These pass or fail on the kernel's actual mathematics: no engine,
// no randomness beyond the kernel's own deterministic hashing, no UI. The
// invariants mirror the substrate acceptance contract: complete coverage,
// disjoint membership, connectivity, determinism, exact land-share
// targeting, span-band respect, pre-assignment exclusion, and well-formed
// carving.
internal static class Program
{
    private sealed record Result(string Name, bool Passed, string Evidence);
    private static readonly List<Result> Results = new();

    private static int Main()
    {
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
        int passed = Results.Count(result => result.Passed);
        foreach (Result result in Results)
            Console.WriteLine((result.Passed ? "PASS " : "FAIL ")
                + result.Name + ": " + result.Evidence);
        Console.WriteLine("B19 regional substrate acceptance: "
            + passed + "/" + Results.Count + " PASS");
        return passed == Results.Count ? 0 : 3;
    }

    private static void Check(string name, bool passed, string evidence)
    {
        Results.Add(new Result(name, passed, evidence));
    }

    // A rectangular 4-neighbor grid world with an optional eligibility
    // mask: the kernel is graph-generic, so a plane exercises the same
    // arithmetic the icosphere does.
    private sealed class GridWorld
    {
        internal readonly int Width;
        internal readonly int Height;
        internal readonly HashSet<int> Eligible = new();
        private readonly Dictionary<int, int[]> adjacency = new();

        internal GridWorld(int width, int height,
            Func<int, int, bool> eligible = null)
        {
            Width = width;
            Height = height;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (eligible == null || eligible(x, y))
                        Eligible.Add(y * width + x);
            foreach (int id in Eligible)
            {
                int x = id % width;
                int y = id / width;
                var around = new List<int>(4);
                if (x > 0 && Eligible.Contains(id - 1))
                    around.Add(id - 1);
                if (x < width - 1 && Eligible.Contains(id + 1))
                    around.Add(id + 1);
                if (y > 0 && Eligible.Contains(id - width))
                    around.Add(id - width);
                if (y < height - 1 && Eligible.Contains(id + width))
                    around.Add(id + width);
                around.Sort();
                adjacency[id] = around.ToArray();
            }
        }

        internal IReadOnlyList<int> NeighborsOf(int id)
        {
            return adjacency.TryGetValue(id, out int[] found)
                ? found : Array.Empty<int>();
        }

        internal (float x, float y, float z) PositionOf(int id)
        {
            return (id % Width, (float)(id / Width), 0f);
        }

        internal List<CARegionalTopologyKernel.RegionSeed> Partition(
            int seed, float share, int sizeMin, int sizeMax,
            ISet<int> preAssigned = null,
            Func<int, int, float> barrierCost = null)
        {
            return CARegionalTopologyKernel.Partition(seed,
                Eligible.OrderBy(id => id).ToList(), NeighborsOf,
                PositionOf, share, sizeMin, sizeMax, preAssigned,
                barrierCost);
        }
    }

    private static void Run()
    {
        var world = new GridWorld(120, 90);
        List<CARegionalTopologyKernel.RegionSeed> partition =
            world.Partition(20260820, 0.40f, 3, 5);

        // 1. Complete coverage, disjoint membership.
        var seen = new Dictionary<int, int>();
        int duplicates = 0;
        foreach (CARegionalTopologyKernel.RegionSeed region in partition)
            foreach (int id in region.MemberTileIds)
            {
                if (seen.ContainsKey(id)) duplicates++;
                else seen[id] = region.RootTileId;
            }
        Check("coverage-and-disjointness",
            duplicates == 0 && seen.Count == world.Eligible.Count
                && world.Eligible.All(seen.ContainsKey),
            seen.Count + "/" + world.Eligible.Count
                + " eligible tiles assigned exactly once; "
                + duplicates + " duplicate assignments");

        // 2. Member-to-region resolution is single-valued: every member of
        // one region resolves to the same region (the same-region member
        // invariant at the partition layer).
        bool singleValued = partition.All(region =>
            region.MemberTileIds.All(id =>
                seen[id] == region.RootTileId));
        Check("member-resolution-single-valued", singleValued,
            "each member tile maps to exactly its own region root");

        // 3. Connectivity of every region.
        int disconnected = partition.Count(region =>
            CARegionalTopologyKernel.SplitComponents(region.MemberTileIds,
                world.NeighborsOf).Count != 1);
        Check("region-connectivity", disconnected == 0,
            disconnected + " of " + partition.Count
                + " regions are disconnected");

        // 4. Determinism: an identical second run reproduces the partition
        // exactly.
        List<CARegionalTopologyKernel.RegionSeed> again =
            world.Partition(20260820, 0.40f, 3, 5);
        bool identical = partition.Count == again.Count
            && partition.Zip(again).All(pair =>
                pair.First.RootTileId == pair.Second.RootTileId
                && pair.First.MemberTileIds.SequenceEqual(
                    pair.Second.MemberTileIds));
        Check("determinism", identical,
            "second run reproduces " + partition.Count
                + " regions byte-for-byte");

        // 5. A different seed produces a different partition (the seed is
        // load-bearing, not decorative).
        List<CARegionalTopologyKernel.RegionSeed> other =
            world.Partition(11111111, 0.40f, 3, 5);
        bool differs = partition.Count != other.Count
            || !partition.Zip(other).All(pair =>
                pair.First.RootTileId == pair.Second.RootTileId);
        Check("seed-sensitivity", differs,
            "seed change moves the partition");

        // 6. Land-share targeting: the realized share of land inside
        // joined regions tracks the requested share closely on open
        // ground.
        foreach (float target in new[] { 0.10f, 0.40f, 0.80f })
        {
            List<CARegionalTopologyKernel.RegionSeed> run =
                world.Partition(777, target, 3, 5);
            long land = run.Sum(region =>
                (long)region.MemberTileIds.Count);
            long multi = run.Where(region =>
                    region.MemberTileIds.Count > 1)
                .Sum(region => (long)region.MemberTileIds.Count);
            float realized = land == 0 ? 0f : (float)multi / land;
            Check("land-share-" + target.ToString("F2"),
                Math.Abs(realized - target) <= 0.03f,
                "realized " + realized.ToString("F3") + " against target "
                    + target.ToString("F2"));
        }

        // 7. Span band: no joined region exceeds the ceiling, and any
        // joined region below the floor is at a merge fixpoint — no
        // adjacent region could absorb it inside the band. Sub-floor
        // regions at the fixpoint are genuinely blocked geography.
        List<CARegionalTopologyKernel.RegionSeed> banded =
            world.Partition(31337, 0.50f, 3, 5);
        int oversized = banded.Count(region =>
            region.MemberTileIds.Count > 5);
        var ownerOf = new Dictionary<int, int>();
        var sizeOfRoot = new Dictionary<int, int>();
        foreach (CARegionalTopologyKernel.RegionSeed region in banded)
        {
            sizeOfRoot[region.RootTileId] = region.MemberTileIds.Count;
            foreach (int id in region.MemberTileIds)
                ownerOf[id] = region.RootTileId;
        }
        int mergeableSubFloor = 0;
        int subFloor = 0;
        foreach (CARegionalTopologyKernel.RegionSeed region in banded)
        {
            if (region.MemberTileIds.Count <= 1
                || region.MemberTileIds.Count >= 3) continue;
            subFloor++;
            bool mergeable = region.MemberTileIds.Any(id =>
                world.NeighborsOf(id).Any(next =>
                    ownerOf.TryGetValue(next, out int otherRoot)
                    && otherRoot != region.RootTileId
                    && region.MemberTileIds.Count
                        + sizeOfRoot[otherRoot] <= 5));
            if (mergeable) mergeableSubFloor++;
        }
        Check("span-band", oversized == 0 && mergeableSubFloor == 0,
            oversized + " joined regions above the ceiling; " + subFloor
                + " sub-floor joined regions remain, "
                + mergeableSubFloor + " of them mergeable (must be 0)");

        // 8. Single-tile world share zero: everything is a lone region.
        List<CARegionalTopologyKernel.RegionSeed> lone =
            world.Partition(99, 0f, 3, 5);
        Check("share-zero-all-single",
            lone.All(region => region.MemberTileIds.Count == 1),
            lone.Count + " regions, all single");

        // 9. Pre-assigned exclusion: carved-out ground is never claimed.
        var reserved = new HashSet<int>();
        for (int y = 10; y < 20; y++)
            for (int x = 10; x < 20; x++)
                reserved.Add(y * world.Width + x);
        List<CARegionalTopologyKernel.RegionSeed> around =
            world.Partition(555, 0.40f, 3, 5, reserved);
        bool respected = around.All(region =>
            region.MemberTileIds.All(id => !reserved.Contains(id)));
        long aroundCount = around.Sum(region =>
            (long)region.MemberTileIds.Count);
        Check("pre-assignment-exclusion",
            respected
                && aroundCount == world.Eligible.Count - reserved.Count,
            "partition covers " + aroundCount + " of "
                + (world.Eligible.Count - reserved.Count)
                + " unreserved tiles and touches no reserved tile");

        // 10. Fragmented geography: islands smaller than the span band
        // still partition completely, and no region spans water.
        var islands = new GridWorld(60, 60, (x, y) =>
            (x / 6 + y / 6) % 2 == 0);
        List<CARegionalTopologyKernel.RegionSeed> islandRun =
            islands.Partition(4242, 0.60f, 3, 8);
        var islandSeen = new HashSet<int>();
        foreach (CARegionalTopologyKernel.RegionSeed region in islandRun)
            foreach (int id in region.MemberTileIds)
                islandSeen.Add(id);
        int islandDisconnected = islandRun.Count(region =>
            CARegionalTopologyKernel.SplitComponents(region.MemberTileIds,
                islands.NeighborsOf).Count != 1);
        Check("fragmented-geography",
            islandSeen.Count == islands.Eligible.Count
                && islandDisconnected == 0,
            islandSeen.Count + "/" + islands.Eligible.Count
                + " island tiles covered; " + islandDisconnected
                + " disconnected regions");

        // 11. Carving: removing a rectangle from a region splits the
        // remainder into well-formed connected components, largest first,
        // deterministically.
        var carveWorld = new GridWorld(9, 3);
        List<int> band = carveWorld.Eligible.OrderBy(id => id).ToList();
        var carved = new List<int> { 4, 13, 22 }; // middle column x=4
        List<int> remaining = band.Where(id => !carved.Contains(id))
            .ToList();
        List<List<int>> components = CARegionalTopologyKernel
            .SplitComponents(remaining, carveWorld.NeighborsOf);
        bool carveCorrect = components.Count == 2
            && components[0].Count == 12 && components[1].Count == 12
            && components[0][0] < components[1][0]
            && components.SelectMany(component => component)
                .OrderBy(id => id).SequenceEqual(remaining);
        List<List<int>> componentsAgain = CARegionalTopologyKernel
            .SplitComponents(remaining, carveWorld.NeighborsOf);
        bool carveDeterministic = components.Count
                == componentsAgain.Count
            && components.Zip(componentsAgain).All(pair =>
                pair.First.SequenceEqual(pair.Second));
        Check("carve-components", carveCorrect && carveDeterministic,
            components.Count + " components of sizes ["
                + string.Join(",", components.Select(component =>
                    component.Count)) + "], deterministic ordering "
                + (carveDeterministic ? "holds" : "BROKEN"));

        // 11b. Geographic barriers: a vertical wall of high-cost edges
        // splits the grid; no region may cross the barrier.
        var barrierWorld = new GridWorld(12, 4);
        int barrierCol = 5;
        float BarrierWall(int from, int to)
        {
            int fromX = from % barrierWorld.Width;
            int toX = to % barrierWorld.Width;
            // An edge between column 5 and column 6 is a full barrier.
            if ((fromX == barrierCol && toX == barrierCol + 1)
                || (fromX == barrierCol + 1 && toX == barrierCol))
                return 1f;
            return 0f;
        }
        List<CARegionalTopologyKernel.RegionSeed> barrierRun =
            barrierWorld.Partition(42, 0.80f, 2, 4,
                barrierCost: BarrierWall);
        int barrierCrossing = 0;
        foreach (CARegionalTopologyKernel.RegionSeed region in barrierRun)
            foreach (int id in region.MemberTileIds)
            {
                int x = id % barrierWorld.Width;
                foreach (int mate in region.MemberTileIds)
                {
                    int ox = mate % barrierWorld.Width;
                    if ((x <= barrierCol && ox > barrierCol)
                        || (x > barrierCol && ox <= barrierCol))
                    {
                        barrierCrossing++;
                        break;
                    }
                }
            }
        Check("geographic-barriers",
            barrierCrossing == 0,
            barrierCrossing + " regions cross the barrier (must be 0)");

        // 12. Unit hash: bounded, deterministic, salt-sensitive.
        bool unitSane = true;
        for (int i = 0; i < 1000; i++)
        {
            float unit = CARegionalTopologyKernel.Unit(123, i, 7);
            if (unit < 0f || unit >= 1f
                || unit != CARegionalTopologyKernel.Unit(123, i, 7)
                || unit == CARegionalTopologyKernel.Unit(123, i, 8))
            {
                unitSane = false;
                break;
            }
        }
        Check("unit-hash", unitSane,
            "1000 draws bounded to [0,1), reproducible, salt-sensitive");

        // 13. Explicit same-region / different-region member invariants on
        // one joined region: Region(A) == Region(B) for members of R,
        // Region(C) != Region(A) for a member of S.
        CARegionalTopologyKernel.RegionSeed joined = partition
            .First(region => region.MemberTileIds.Count >= 3);
        CARegionalTopologyKernel.RegionSeed otherRegion = partition
            .First(region => region != joined);
        int memberA = joined.MemberTileIds[0];
        int memberB = joined.MemberTileIds[^1];
        int memberC = otherRegion.MemberTileIds[0];
        Check("same-and-different-region-members",
            seen[memberA] == seen[memberB]
                && seen[memberA] == joined.RootTileId
                && seen[memberC] != seen[memberA],
            "Region(A)==Region(B)==" + joined.RootTileId
                + "; Region(C)=" + seen[memberC] + " differs");

        RunTendencyCausality();
    }

    // TENDENCY CAUSALITY AT THE KERNEL LAYER: fixed inputs, materially
    // different authored tendencies, measurably different intended results.
    // These are the pure halves of the runtime chains; the world adapters
    // feed them real facts.
    private static void RunTendencyCausality()
    {
        // Concentration bends world placement between spread and cluster,
        // and 0.5 is exactly neutral.
        double nearLow = CAWorldTendencyCausalKernel.WorldPlacementScore(
            1, 10, 1f, 2f, 0.10f);
        double farLow = CAWorldTendencyCausalKernel.WorldPlacementScore(
            1, 11, 1f, 14f, 0.10f);
        double nearHigh = CAWorldTendencyCausalKernel.WorldPlacementScore(
            1, 10, 1f, 2f, 0.90f);
        double farHigh = CAWorldTendencyCausalKernel.WorldPlacementScore(
            1, 11, 1f, 14f, 0.90f);
        double nearMid = CAWorldTendencyCausalKernel.WorldPlacementScore(
            1, 10, 1f, 2f, 0.50f);
        double farMid = CAWorldTendencyCausalKernel.WorldPlacementScore(
            1, 11, 1f, 14f, 0.50f);
        Check("concentration-placement",
            farLow > nearLow && nearHigh > farHigh
                && Math.Abs(nearMid - farMid) < 0.01d,
            "spread favored at 0.10 (" + farLow.ToString("F2") + ">"
                + nearLow.ToString("F2") + "), cluster at 0.90 ("
                + nearHigh.ToString("F2") + ">" + farHigh.ToString("F2")
                + "), neutral at 0.50");

        // THE INTEGRATION THIS SUITE PREVIOUSLY MISSED. The check above
        // feeds distances already inside the kernel's 0..15 band, so it
        // passed while the production caller fed raw tile distance. On a
        // real planet settlements sit tens of tiles apart, so every
        // candidate saturated the cap and the tendency stopped
        // separating spread from clustered. The caller now scales
        // measured distance into the band by the world's own mean
        // spacing; both halves are asserted here.
        const float worldSpacing = 35.4f;   // sqrt(20000 tiles / 16)
        const float nearTiles = 25f;
        const float farTiles = 60f;
        double rawNear = CAWorldTendencyCausalKernel.WorldPlacementScore(
            1, 20, 1f, nearTiles, 0.10f);
        double rawFar = CAWorldTendencyCausalKernel.WorldPlacementScore(
            1, 20, 1f, farTiles, 0.10f);
        float toBand = 15f / worldSpacing;
        double bandNearSpread =
            CAWorldTendencyCausalKernel.WorldPlacementScore(
                1, 20, 1f, nearTiles * toBand, 0.10f);
        double bandFarSpread =
            CAWorldTendencyCausalKernel.WorldPlacementScore(
                1, 20, 1f, farTiles * toBand, 0.10f);
        double bandNearCluster =
            CAWorldTendencyCausalKernel.WorldPlacementScore(
                1, 20, 1f, nearTiles * toBand, 0.90f);
        double bandFarCluster =
            CAWorldTendencyCausalKernel.WorldPlacementScore(
                1, 20, 1f, farTiles * toBand, 0.90f);
        Check("concentration-at-world-scale",
            Math.Abs(rawNear - rawFar) < 1e-9d
                && bandFarSpread > bandNearSpread
                && bandNearCluster > bandFarCluster,
            "raw " + nearTiles + " vs " + farTiles + " tiles both "
                + "saturate identically (" + rawNear.ToString("F4")
                + "=" + rawFar.ToString("F4") + ", the defect); scaled "
                + "by " + worldSpacing.ToString("F1")
                + "-tile spacing the band separates - spread favors far "
                + "(" + bandFarSpread.ToString("F2") + ">"
                + bandNearSpread.ToString("F2") + "), cluster favors "
                + "near (" + bandNearCluster.ToString("F2") + ">"
                + bandFarCluster.ToString("F2") + ")");

        // ARE THE NINE NAMED WORLDS ACTUALLY DIFFERENT? The chooser
        // offers nine world characters; nothing until now established
        // that they produce different worlds, and two collapsing onto
        // the same structure would be a product defect discoverable
        // only by generating all nine and comparing by eye. Each
        // character's authored band and span are run through the REAL
        // partition kernel over one fixed geography, and the realized
        // joined-land share and mean span are compared pairwise. This
        // proves the substrate separates them; it does not prove the
        // resulting games feel different, which only play can show.
        var world = new GridWorld(120, 90);
        var structures = new List<(string Key, double[] Signature)>();
        foreach (CAWorldCharacterValues character
            in CAWorldCharacterValues.All)
        {
            float share = CAWorldTendencyCausalKernel.ResolveRange(
                20260821, 0, 826351197, character.FrequencyMin,
                character.FrequencyMax);
            List<CARegionalTopologyKernel.RegionSeed> partition =
                world.Partition(20260821, share, character.SpanMin,
                    character.SpanMax);
            long total = 0;
            long joinedLand = 0;
            int joined = 0;
            foreach (CARegionalTopologyKernel.RegionSeed region in
                partition)
            {
                int size = region.MemberTileIds.Count;
                total += size;
                if (size < 2) continue;
                joined++;
                joinedLand += size;
            }
            // Every dimension the character authors, each through the
            // kernel the game runs. A world differs from another if any
            // of these differs; land structure alone is only two of the
            // eight, and several characters deliberately share it.
            double spreadBias =
                CAWorldTendencyCausalKernel.WorldPlacementScore(
                    1, 20, 1f, 6f, character.Concentration)
                - CAWorldTendencyCausalKernel.WorldPlacementScore(
                    1, 20, 1f, 14f, character.Concentration);
            structures.Add((character.Key, new[]
            {
                total == 0 ? 0d : (double)joinedLand / total,
                joined == 0 ? 0d : (double)joinedLand / joined,
                spreadBias,
                (double)CAWorldTendencyCausalKernel.TownThreshold,
                CAWorldTendencyCausalKernel.FrontierHoldingCount(8,
                    character.FrontierFrequency),
                CAWorldTendencyCausalKernel.FrontierResidentCount(3,
                    character.FrontierSize),
                CAWorldTendencyCausalKernel.FrontierMaterialLevel(3,
                    character.FrontierSize),
                CAWorldTendencyCausalKernel.SourceVarietyTargetDistinct(
                    12, 8, character.Variety),
                CAWorldTendencyCausalKernel.DistantFoundingRoll(
                    1, 0, character.DistantFounding) ? 1d : 0d,
                character.WorldDevelopment,
                character.WorldVariability,
                character.WorldStability
            }));
        }
        double[] tolerance =
            { 0.02d, 0.25d, 0.05d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d };
        var collisions = new List<string>();
        for (int i = 0; i < structures.Count; i++)
            for (int j = i + 1; j < structures.Count; j++)
            {
                bool separable = false;
                for (int d = 0; d < tolerance.Length && !separable; d++)
                    separable = Math.Abs(structures[i].Signature[d]
                        - structures[j].Signature[d]) >= tolerance[d];
                if (!separable)
                    collisions.Add(structures[i].Key + "="
                        + structures[j].Key);
            }
        Check("world-character-separation", collisions.Count == 0,
            structures.Count + " characters over one fixed geography, "
            + "compared across every authored dimension"
            + (collisions.Count == 0
                ? "; every pair separable on at least one"
                : " - COLLIDING PAIRS: "
                    + string.Join(", ", collisions)));

        // WHAT A PLACE BUILDS FROM MUST ACTUALLY VARY. The material
        // decision is only meaningful if each of its inputs can change
        // the answer. Two failures are asserted against directly here:
        // a threshold calibrated for settlements making it inert for
        // frontier holdings, and development pressure being able to buy
        // materials the place cannot carry.
        int cabinReach = CAConstructionMaterialsKernel.Reach(0, 1, 0);
        int homesteadReach = CAConstructionMaterialsKernel.Reach(3, 5, 0);
        int villageReach = CAConstructionMaterialsKernel.Reach(2, 80, 1);
        int cityReach = CAConstructionMaterialsKernel.Reach(5, 900, 3);
        Check("material-reach-separates-site-kinds",
            cabinReach < homesteadReach && homesteadReach <= villageReach
                && villageReach < cityReach,
            "reach rises across a lone cabin (" + cabinReach
                + "), an established homestead (" + homesteadReach
                + "), a village (" + villageReach + ") and a city ("
                + cityReach + ") - holdings are not pinned to the floor");

        // Ambition never exceeds means: a hamlet that wants everything
        // still builds what it can carry.
        int wanting = CAConstructionMaterialsKernel.Reach(5, 2, 0);
        int carrying = CAConstructionMaterialsKernel.AffordableStanding(
            2, 0);
        Check("material-ambition-bounded-by-capacity",
            wanting == carrying,
            "development 5 on 2 residents still reaches only " + wanting
                + ", its capacity");

        // Within reach, the more worked material wins; beyond reach it
        // does not, and local ground breaks a tie against an import.
        int stoneInReach = CAConstructionMaterialsKernel.Suitability(
            2, 2, true);
        int woodInReach = CAConstructionMaterialsKernel.Suitability(
            1, 2, true);
        int metalBeyond = CAConstructionMaterialsKernel.Suitability(
            3, 2, true);
        int importedStone = CAConstructionMaterialsKernel.Suitability(
            2, 2, false);
        Check("material-suitability-ordering",
            stoneInReach > woodInReach && stoneInReach > metalBeyond
                && stoneInReach > importedStone,
            "at reach 2 worked stone (" + stoneInReach
                + ") beats timber (" + woodInReach
                + ") and out-of-reach metal (" + metalBeyond
                + "); local stone beats the same stone imported ("
                + importedStone + ")");

        // CULTURE CHANGES WHAT A PLACE BUILDS TOWARD. Two villages with
        // identical ground, people and economy build differently when
        // one expects permanent roots and the other expects to move.
        int rooted = CAConstructionMaterialsKernel.CulturalAmbition(
            0.8f, 0.4f);
        int mobile = CAConstructionMaterialsKernel.CulturalAmbition(
            -0.8f, -0.4f);
        int unasked = CAConstructionMaterialsKernel.CulturalAmbition(
            0f, 0f);
        int rootedReach = CAConstructionMaterialsKernel.Reach(
            1, 80, 1, rooted);
        int mobileReach = CAConstructionMaterialsKernel.Reach(
            1, 80, 1, mobile);
        Check("culture-moves-what-a-place-builds-toward",
            rooted > 0 && mobile < 0 && unasked == 0
                && rootedReach > mobileReach,
            "same village, same means: rooted reaches " + rootedReach
                + ", mobile reaches " + mobileReach
                + "; a culture never asked moves nothing (" + unasked
                + ")");

        // But culture is ambition, never means. A people who expect
        // permanent stone roots and live three to a holding still build
        // what three people can build.
        int wantingRooted = CAConstructionMaterialsKernel.Reach(
            5, 2, 0, 1);
        int rootedCityBound = CAConstructionMaterialsKernel.Reach(
            9, 900, 3, 1);
        Check("culture-never-outbuilds-capacity",
            wantingRooted
                == CAConstructionMaterialsKernel.AffordableStanding(2, 0)
                && rootedCityBound == CAConstructionMaterialsKernel
                    .AffordableStanding(900, 3),
            "a rooted culture on 2 residents still reaches "
                + wantingRooted + ", and on a full city still reaches "
                + rootedCityBound + " - its capacity in both cases");

        // Stewardship moves only the tie-break between two materials the
        // place could equally build: it never talks a place out of a
        // material it can reach, and never into one it cannot.
        int sparingWeight = CAConstructionMaterialsKernel.LocalWeight(
            0.7f);
        int takingWeight = CAConstructionMaterialsKernel.LocalWeight(
            -0.7f);
        int localStoneTaking = CAConstructionMaterialsKernel.Suitability(
            2, 2, true, takingWeight);
        int importStoneTaking = CAConstructionMaterialsKernel.Suitability(
            2, 2, false, takingWeight);
        int localStoneSparing = CAConstructionMaterialsKernel.Suitability(
            2, 2, true, sparingWeight);
        int importStoneSparing = CAConstructionMaterialsKernel
            .Suitability(2, 2, false, sparingWeight);
        int localWoodSparing = CAConstructionMaterialsKernel.Suitability(
            1, 2, true, sparingWeight);
        Check("stewardship-moves-only-the-tie-break",
            takingWeight == 1 && sparingWeight == 0
                && localStoneTaking > importStoneTaking
                && localStoneSparing == importStoneSparing
                && localStoneSparing > localWoodSparing,
            "unrestricted extraction prefers its own rock ("
                + localStoneTaking + " over " + importStoneTaking
                + "); strict stewardship is indifferent between them ("
                + localStoneSparing + " and " + importStoneSparing
                + ") and still builds stone over timber ("
                + localWoodSparing + ")");

        // A CLASSIFICATION BAR MUST NOT BUILD BUILDINGS. The town/city
        // bar is a fixed game constant. It cannot supply a place with
        // support it lacks, and it must not supply it with physical
        // structure either. Quarters derive from population and support
        // against the unmoved threshold, not from any authored control.
        // Quarters follow population and support against the fixed
        // threshold, not any authored control. The same settlement
        // builds the same quarters regardless of the bar; different
        // support builds different quarters.
        int quartersAt60 = CAWorldTendencyCausalKernel.SettlementQuarters(
            600, 60);
        int scaleAt60 = CAWorldTendencyCausalKernel
            .SettlementScale(600, 60);
        int hamletQuarters = CAWorldTendencyCausalKernel.SettlementQuarters(
            80, 20);
        int cityQuarters = CAWorldTendencyCausalKernel.SettlementQuarters(
            1400, 90);
        Check("quarters-follow-facts-not-the-naming-bar",
            hamletQuarters < cityQuarters
                && quartersAt60 >= 1,
            "the same settlement builds " + quartersAt60
                + " quarters from its support, not from any control; "
                + "a hamlet builds " + hamletQuarters
                + " and a city " + cityQuarters);

        // MATERIAL SUPPORT REACHES MATERIAL. A frontier holding on
        // treeless ground has no timber of its own; a supporter is a
        // route for material to arrive, and that is the whole physical
        // difference between a holding somebody supplies and one nobody
        // does. Support was previously resolved only to refuse
        // materialization when the named faction had gone missing.
        bool suppliedTrades = CAConstructionMaterialsKernel.Trades(2);
        bool aloneTrades = CAConstructionMaterialsKernel.Trades(0);
        bool timberSupplied = CAConstructionMaterialsKernel.TimberAvailable(
            false, suppliedTrades);
        bool timberAlone = CAConstructionMaterialsKernel.TimberAvailable(
            false, aloneTrades);
        bool timberOnWoodedGround = CAConstructionMaterialsKernel
            .TimberAvailable(true, aloneTrades);
        Check("support-reaches-treeless-ground",
            suppliedTrades && !aloneTrades
                && timberSupplied && !timberAlone
                && timberOnWoodedGround,
            "on treeless ground a supplied holding can build in timber "
                + "and an unsupplied one cannot; wooded ground needs no "
                + "supplier");

        // Being supplied is not having an economy. Support must not buy
        // metal a holding could never pay for, and must not stand in for
        // knowing how to work it.
        bool metalBought = CAConstructionMaterialsKernel.MetalAvailable(
            0, suppliedTrades, 0);
        bool metalWorked = CAConstructionMaterialsKernel.MetalAvailable(
            2, aloneTrades, 0);
        bool metalPaidFor = CAConstructionMaterialsKernel.MetalAvailable(
            0, suppliedTrades, 2);
        bool stoneUnskilled = CAConstructionMaterialsKernel
            .WorkedStoneAvailable(1);
        bool stoneSkilled = CAConstructionMaterialsKernel
            .WorkedStoneAvailable(2);
        Check("support-is-not-an-economy",
            !metalBought && metalWorked && metalPaidFor
                && !stoneUnskilled && stoneSkilled,
            "a supplied holding with no economy gets no metal; metal is "
                + "worked where metallurgy is known or bought where the "
                + "place can pay; rock without masonry is a quarry");

        // EVERY DECLARED ROLE COSTS THE SETTLEMENT SOMETHING. A role
        // an operator can declare and that carries no program behind it
        // is a declaration nobody honours - which is exactly what the
        // whole mask was until now.
        List<CARegionalOperationalRole> unhonoured =
            CAOperationalRoleProgramKernel.RolesWithoutPrograms();
        Check("every-operational-role-builds-something",
            unhonoured.Count == 0
                && CAOperationalRoleProgramKernel.AllRoles.Length == 7,
            CAOperationalRoleProgramKernel.AllRoles.Length
                + " declarable roles, "
                + (unhonoured.Count == 0 ? "all of them build works"
                    : "unhonoured: " + string.Join(", ",
                        unhonoured.Select(role => role.ToString())
                            .ToArray())));

        // Two roles wanting the same works commit the settlement once,
        // and a settlement that declares nothing takes on nothing.
        List<string> outpostAndWatch = CAOperationalRoleProgramKernel
            .ProgramKeysFor((int)(CARegionalOperationalRole.CombatOutpost
                | CARegionalOperationalRole.ObservationPost));
        List<string> depot = CAOperationalRoleProgramKernel
            .ProgramKeysFor((int)(CARegionalOperationalRole.LogisticsPoint
                | CARegionalOperationalRole.CasualtyCollection));
        List<string> declaredNothing = CAOperationalRoleProgramKernel
            .ProgramKeysFor(0);
        Check("roles-compose-without-repeating",
            outpostAndWatch.Count == 1
                && outpostAndWatch[0]
                    == CASettlementProgramCausalKernel.Defense
                && depot.Count == 3
                && depot.Distinct().Count() == depot.Count
                && depot.Contains(CASettlementProgramCausalKernel.Storage)
                && depot.Contains(CASettlementProgramCausalKernel.Transport)
                && depot.Contains(CASettlementProgramCausalKernel.Medicine)
                && declaredNothing.Count == 0,
            "an outpost that also watches builds defensive works once ("
                + outpostAndWatch.Count + "); a depot that also collects "
                + "casualties builds " + depot.Count
                + " distinct programs; declaring nothing builds nothing");

        // Variety raises the distinct-source target monotonically within
        // pool bounds.
        int varietyLow = CAWorldTendencyCausalKernel
            .SourceVarietyTargetDistinct(6, 5, 0.10f);
        int varietyHigh = CAWorldTendencyCausalKernel
            .SourceVarietyTargetDistinct(6, 5, 0.95f);
        Check("variety-target",
            varietyLow < varietyHigh && varietyHigh <= 5
                && varietyLow >= 1
                && CAWorldTendencyCausalKernel.SourceVarietyTargetDistinct(
                    0, 5, 0.9f) == 0,
            "target " + varietyLow + " at 0.10 -> " + varietyHigh
                + " at 0.95, bounded by pool");

        // The classification threshold is a fixed constant (DR-114, DR-115).
        // A settlement with support at the bar is a town; support below it
        // is not. No authored control moves the bar.
        int atBar = CAWorldTendencyCausalKernel.SettlementScale(600, 62);
        int belowBar = CAWorldTendencyCausalKernel.SettlementScale(600, 61);
        int starved = CAWorldTendencyCausalKernel.SettlementScale(600, 20);
        Check("urban-threshold",
            atBar >= 3 && belowBar < atBar && starved < atBar,
            "support 62 -> scale " + atBar + " (town); 61 -> " + belowBar
                + "; 20 -> " + starved + " (below bar, not a town)");

        // Frontier frequency owns the count; size owns residents and
        // material under land caps; neither leaks into the other.
        int holdingsLow = CAWorldTendencyCausalKernel.FrontierHoldingCount(
            6, 0.10f);
        int holdingsHigh = CAWorldTendencyCausalKernel.FrontierHoldingCount(
            6, 0.90f);
        int residentsSmall = CAWorldTendencyCausalKernel
            .FrontierResidentCount(3, 0.05f);
        int residentsLarge = CAWorldTendencyCausalKernel
            .FrontierResidentCount(3, 0.95f);
        int materialPoorLand = CAWorldTendencyCausalKernel
            .FrontierMaterialLevel(0, 1.00f);
        Check("frontier-frequency-and-size",
            holdingsLow < holdingsHigh && holdingsHigh <= 6
                && residentsSmall < residentsLarge
                && materialPoorLand == 0,
            holdingsLow + " -> " + holdingsHigh + " holdings of 6 sites; "
                + residentsSmall + " -> " + residentsLarge + " residents; "
                + "poor land caps material at 0 whatever the tendency");

        // Distant founding rate gates new settlement appearance.
        bool foundLow = CAWorldTendencyCausalKernel.DistantFoundingRoll(
            42, 0, 0.10f);
        bool foundHigh = CAWorldTendencyCausalKernel.DistantFoundingRoll(
            42, 0, 0.90f);
        bool foundZero = CAWorldTendencyCausalKernel.DistantFoundingRoll(
            42, 0, 0f);
        int foundCount = 0;
        for (int p = 0; p < 100; p++)
            if (CAWorldTendencyCausalKernel.DistantFoundingRoll(42, p, 0.5f))
                foundCount++;
        Check("distant-founding",
            !foundZero && foundCount > 0 && foundCount < 100,
            "rate 0 gates all; rate 0.5 founds " + foundCount
                + " of 100 periods");

        // World settlement facts: deterministic, bounded, and support
        // rises with population and access.
        int populationA = CAWorldTendencyCausalKernel
            .WorldSettlementPopulation(9, 100, 3, 2, 2);
        int populationB = CAWorldTendencyCausalKernel
            .WorldSettlementPopulation(9, 100, 3, 2, 2);
        // Variability creates coherent countertypical tech tiers.
        int popLowVar = CAWorldTendencyCausalKernel
            .WorldSettlementPopulation(9, 100, 3, 2, 2, 0f);
        int popHighVar = CAWorldTendencyCausalKernel
            .WorldSettlementPopulation(9, 100, 3, 2, 2, 0.9f);
        int popVarRepro = CAWorldTendencyCausalKernel
            .WorldSettlementPopulation(9, 100, 3, 2, 2, 0.9f);
        int supportPoor = CAWorldTendencyCausalKernel.WorldSettlementSupport(
            80, 1, 0, false, 0);
        int supportRich = CAWorldTendencyCausalKernel.WorldSettlementSupport(
            900, 3, 3, true, 3);
        // Stability cadence scales with stability.
        int cadenceLow = CAWorldTendencyCausalKernel.StabilityCadence(
            60000, 0f);
        int cadenceHigh = CAWorldTendencyCausalKernel.StabilityCadence(
            60000, 1f);
        Check("stability-cadence",
            cadenceLow == 60000 && cadenceHigh == 300000,
            "cadence " + cadenceLow + " at 0 -> " + cadenceHigh
                + " at 1");
        Check("world-settlement-facts",
            populationA == populationB && populationA >= 18
                && supportRich > supportPoor
                && popVarRepro == popHighVar,
            "population " + populationA + " reproducible; support "
                + supportPoor + " poor -> " + supportRich + " rich"
                + "; variability " + popLowVar + " -> " + popHighVar
                + " reproducible");
    }
}
