using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // SETTLEMENTS GROW; THEY ARE NOT STAMPED.
    //
    //   represented state -> requirements/capabilities -> candidate
    //   spatial structures -> corpus-informed relational ranking ->
    //   materialization -> actual use -> persistent history -> later
    //   development
    //
    // This grower replaces the one-shot organic-blob morphology for CA
    // settlements. The build queue derives from the represented state:
    // the founding provision core, then one dwelling per household of the
    // realized population, interleaved with the facilities each
    // operational program contributes, spread over historicalDevelopment+1
    // eras. Every placement is chosen among natively valid candidate
    // footprints by the relationships that function owns in the learned
    // corpus evidence -- and every accepted placement changes the state
    // later placements are evaluated against: buildings occupy ground,
    // trips between related functions wear paths, worn paths become
    // streets, and later buildings front their doors onto them. Districts,
    // circulation, looseness, and accretion are emergent state, not
    // authored rules, and no body mask exists for anything to terminate
    // at. The output is a CAMorphResult so the existing materialization
    // (spawn, roofs, use-based verge, pier, harbor) consumes it unchanged.
    internal static class CASettlementGrowth
    {
        internal static readonly bool Disabled = string.Equals(
            Environment.GetEnvironmentVariable("CA_SETTLEMENT_GROWER"),
            "0", StringComparison.Ordinal);

        private sealed class Placed
        {
            internal string Function;
            internal CellRect Box;
            internal int DoorIndex;
            internal IntVec2 Door;
        }

        private sealed class GrowthState
        {
            internal int W, H;
            internal byte[] Cells;
            internal bool[] Passable;
            internal int[] Traffic;
            internal readonly List<Placed> Buildings = new List<Placed>();
            internal int Walks;
            internal int StreetCells;
            internal string BiomeGroup;
            internal string Quartile;
            internal System.Random Rng;
        }

        internal static CAMorphResult Generate(
            CARegionalSettlementRecord record, int seed, int w, int h,
            bool[] passable, CASettlementPhysicalRequirements need,
            Map map)
        {
            var state = new GrowthState
            {
                W = w,
                H = h,
                Cells = new byte[w * h],
                Passable = passable ?? Enumerable.Repeat(true, w * h)
                    .ToArray(),
                Traffic = new int[w * h],
                Rng = new System.Random(seed),
                BiomeGroup = CASpatialRelationshipEvidence
                    .BiomeGroupFor(map),
            };

            int residents = record.residentPopulation >= 18
                    && record.residentPopulation <= 1200
                ? record.residentPopulation : 18;
            int households = Mathf.Clamp((residents + 2) / 3, 3, 120);
            List<string> facilities = FacilityQueue(record);
            state.Quartile = CASpatialRelationshipEvidence.QuartileFor(
                households * 20 + facilities.Count * 30);

            int eras = Mathf.Clamp(record.historicalDevelopment, 0, 3) + 1;
            var evidenceSamples = new List<string>();

            // Era structure IS the history: each era places its share of
            // households and the facilities whose programs it carries,
            // then its trips wear into streets that the next era fronts.
            int placedDwellings = 0, facilityCursor = 0;
            for (int era = 0; era < eras; era++)
            {
                int dwellingTarget = households * (era + 1) / eras;
                int facilityTarget = facilities.Count * (era + 1) / eras;
                bool progress = true;
                while (progress)
                {
                    progress = false;
                    if (facilityCursor < facilityTarget)
                    {
                        if (PlaceOne(state, facilities[facilityCursor],
                                need, evidenceSamples))
                            facilityCursor++;
                        else facilityTarget = facilityCursor;
                        progress = true;
                    }
                    if (placedDwellings < dwellingTarget)
                    {
                        if (PlaceOne(state,
                                CASettlementProgramCausalKernel.Housing,
                                need, evidenceSamples))
                            placedDwellings++;
                        else dwellingTarget = placedDwellings;
                        progress = true;
                    }
                }
            }

            var plan = new CAMorphResult
            {
                w = w,
                h = h,
                cells = state.Cells,
                desireStreetCells = state.StreetCells,
                desireWalkedLots = state.Walks
            };
            foreach (Placed placed in state.Buildings)
            {
                var lot = new CAMorphLot { doorIndex = placed.DoorIndex };
                foreach (IntVec3 c in placed.Box.ContractedBy(1))
                    lot.cells.Add(c.x + c.z * w);
                lot.area = lot.cells.Count;
                plan.lots.Add(lot);
            }

            Log.Message("[CA][Settlement][Growth] "
                + (record.name ?? "settlement") + ": " + eras + " era(s), "
                + state.Buildings.Count + " buildings ("
                + placedDwellings + " dwellings of " + households
                + " households, " + facilityCursor + "/" + facilities.Count
                + " facilities [" + string.Join(", ", facilities.Take(
                    facilityCursor).Select(Short)) + "]), "
                + state.StreetCells + " street cells worn by "
                + state.Walks + " walks; conditioned "
                + state.BiomeGroup + "/" + state.Quartile
                + (CASpatialRelationshipEvidence.Available
                    ? "" : "; EVIDENCE ABSENT, validity-only placement"));
            foreach (string sample in evidenceSamples.Take(4))
                Log.Message("[CA][Settlement][Growth][Rank] "
                    + (record.name ?? "settlement") + ": " + sample);
            return plan;
        }

        private static string Short(string key)
        {
            return key.Replace("ca.settlement.", "");
        }

        // Facilities are the programs' ground: each operational entry
        // asks for its own building, in composition order.
        private static List<string> FacilityQueue(
            CARegionalSettlementRecord record)
        {
            var queue = new List<string>();
            var entries = record.settlementProgram?.entries
                ?? new List<CASettlementProgramEntry>();
            void add(string key, int count)
            {
                for (int i = 0; i < count; i++) queue.Add(key);
            }
            bool has(string key) => entries.Any(e => e != null
                && e.programKey == key);
            int count(string key) => entries.Where(e => e != null
                && e.programKey == key).Sum(e => Math.Max(1, e.count));

            // the founding core first: gathering hall and shared kitchen
            if (has(CASettlementProgramCausalKernel.CommunalProvision)
                || has(CASettlementProgramCausalKernel.Gathering))
                add(CASettlementProgramCausalKernel.Gathering, 1);
            if (has(CASettlementProgramCausalKernel.CommunalProvision)
                || has(CASettlementProgramCausalKernel.FoodPreparation))
                add(CASettlementProgramCausalKernel.FoodPreparation, 1);
            if (has(CASettlementProgramCausalKernel.Agriculture)
                || has(CASettlementProgramCausalKernel.Storage))
                add(CASettlementProgramCausalKernel.Storage, Math.Max(1,
                    (count(CASettlementProgramCausalKernel.Storage)
                        + count(CASettlementProgramCausalKernel
                            .Agriculture) + 1) / 2));
            if (has(CASettlementProgramCausalKernel.Medicine))
                add(CASettlementProgramCausalKernel.Medicine, 1);
            if (has(CASettlementProgramCausalKernel.Production))
                add(CASettlementProgramCausalKernel.Production,
                    count(CASettlementProgramCausalKernel.Production));
            if (has(CASettlementProgramCausalKernel.Trade))
                add(CASettlementProgramCausalKernel.Trade, 1);
            if (has(CASettlementProgramCausalKernel.Research))
                add(CASettlementProgramCausalKernel.Research, 1);
            if (has(CASettlementProgramCausalKernel.Custody))
                add(CASettlementProgramCausalKernel.Custody, 1);
            return queue;
        }

        private static IntVec2 SizeFor(GrowthState state, string function)
        {
            if (function == CASettlementProgramCausalKernel.Housing)
            {
                switch (state.Rng.Next(4))
                {
                    case 0: return new IntVec2(5, 4);
                    case 1: return new IntVec2(6, 4);
                    case 2: return new IntVec2(6, 5);
                    default: return new IntVec2(7, 5);
                }
            }
            if (function == CASettlementProgramCausalKernel.Gathering)
                return new IntVec2(10, 8);
            if (function == CASettlementProgramCausalKernel
                    .FoodPreparation)
                return new IntVec2(7, 6);
            if (function == CASettlementProgramCausalKernel.Storage)
                return new IntVec2(8, 7);
            if (function == CASettlementProgramCausalKernel.Production)
                return new IntVec2(8, 7);
            if (function == CASettlementProgramCausalKernel.Trade)
                return new IntVec2(6, 5);
            return new IntVec2(7, 6);
        }

        private static bool PlaceOne(GrowthState state, string function,
            CASettlementPhysicalRequirements need,
            List<string> evidenceSamples)
        {
            IntVec2 size = SizeFor(state, function);
            int bestScore = int.MinValue;
            CellRect bestBox = CellRect.Empty;
            IntVec2 bestDoor = default;
            string bestWhy = null;
            int considered = 0;

            int attempts = state.Buildings.Count == 0 ? 24 : 20;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (state.Rng.Next(2) == 0)
                    size = new IntVec2(size.z, size.x);
                CellRect box;
                if (state.Buildings.Count == 0)
                {
                    int cx = state.W / 2 + state.Rng.Next(-state.W / 6,
                        state.W / 6);
                    int cz = state.H / 2 + state.Rng.Next(-state.H / 6,
                        state.H / 6);
                    box = new CellRect(cx - size.x / 2, cz - size.z / 2,
                        size.x, size.z);
                }
                else
                {
                    Placed anchor = state.Buildings[
                        state.Rng.Next(state.Buildings.Count)];
                    int gap = 1 + state.Rng.Next(3);
                    int side = state.Rng.Next(4);
                    int jitter = state.Rng.Next(-3, 4);
                    switch (side)
                    {
                        case 0:
                            box = new CellRect(anchor.Box.maxX + 1 + gap,
                                anchor.Box.minZ + jitter, size.x, size.z);
                            break;
                        case 1:
                            box = new CellRect(
                                anchor.Box.minX - gap - size.x,
                                anchor.Box.minZ + jitter, size.x, size.z);
                            break;
                        case 2:
                            box = new CellRect(anchor.Box.minX + jitter,
                                anchor.Box.maxZ + 1 + gap, size.x, size.z);
                            break;
                        default:
                            box = new CellRect(anchor.Box.minX + jitter,
                                anchor.Box.minZ - gap - size.z,
                                size.x, size.z);
                            break;
                    }
                }
                if (!Valid(state, box)) continue;
                considered++;
                IntVec2 door = DoorFor(state, box);
                int score = ScoreCandidate(state, function, box, door,
                    out string why);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestBox = box;
                    bestDoor = door;
                    bestWhy = why;
                }
            }
            if (bestBox == CellRect.Empty) return false;

            Commit(state, function, bestBox, bestDoor);
            if (bestWhy != null && evidenceSamples.Count < 12
                && (state.Buildings.Count <= 3
                    || state.Buildings.Count % 17 == 0))
                evidenceSamples.Add(Short(function) + " at ("
                    + bestBox.minX + "," + bestBox.minZ + ") score "
                    + bestScore + " over " + considered + " candidates: "
                    + bestWhy);
            return true;
        }

        private static bool Valid(GrowthState state, CellRect box)
        {
            if (box.minX < 2 || box.minZ < 2 || box.maxX >= state.W - 2
                || box.maxZ >= state.H - 2) return false;
            for (int z = box.minZ; z <= box.maxZ; z++)
                for (int x = box.minX; x <= box.maxX; x++)
                {
                    int i = x + z * state.W;
                    if (!state.Passable[i]) return false;
                    if (state.Cells[i] != (byte)CAMorphCell.Empty)
                        return false;
                }
            return true;
        }

        private static IntVec2 DoorFor(GrowthState state, CellRect box)
        {
            // the door faces the busiest adjacent ground: an existing
            // street or worn path when one runs nearby, otherwise the
            // settlement's current mass.
            var sides = new[]
            {
                new IntVec2(box.CenterCell.x, box.minZ),
                new IntVec2(box.CenterCell.x, box.maxZ),
                new IntVec2(box.minX, box.CenterCell.z),
                new IntVec2(box.maxX, box.CenterCell.z)
            };
            IntVec2 best = sides[0];
            int bestPull = int.MinValue;
            foreach (IntVec2 side in sides)
            {
                int pull = 0;
                for (int d = 1; d <= 4; d++)
                {
                    int ox = side.x == box.minX ? -d
                        : side.x == box.maxX ? d : 0;
                    int oz = side.z == box.minZ ? -d
                        : side.z == box.maxZ ? d : 0;
                    int x = side.x + ox, z = side.z + oz;
                    if (x < 0 || z < 0 || x >= state.W || z >= state.H)
                        break;
                    int i = x + z * state.W;
                    if (state.Cells[i] == (byte)CAMorphCell.Street)
                        pull += 12 - d;
                    else pull += Math.Min(6, state.Traffic[i]);
                }
                foreach (Placed other in state.Buildings)
                {
                    int distance = Math.Max(
                        Math.Abs(other.Door.x - side.x),
                        Math.Abs(other.Door.z - side.z));
                    pull += Math.Max(0, 12 - distance) / 4;
                }
                if (pull > bestPull) { bestPull = pull; best = side; }
            }
            return best;
        }

        private static int ScoreCandidate(GrowthState state,
            string function, CellRect box, IntVec2 door, out string why)
        {
            var parts = new List<string>();
            int total = 0;

            void relate(string key, string targetFunction)
            {
                Placed target = Nearest(state, targetFunction, door);
                if (target == null) return;
                if (!CASpatialRelationshipEvidence.TryBand(key,
                    state.BiomeGroup, state.Quartile,
                    out CASpatialEvidenceBand band)) return;
                double distance = Math.Max(
                    Math.Abs(target.Door.x - door.x),
                    Math.Abs(target.Door.z - door.z));
                int score = CASpatialRelationshipEvidence.Score(distance,
                    band);
                total += score;
                parts.Add(CASpatialRelationshipEvidence.Describe(key,
                    band, distance, score));
            }

            if (function == CASettlementProgramCausalKernel.Housing)
            {
                relate("bed-to-foodprep",
                    CASettlementProgramCausalKernel.FoodPreparation);
                relate("bed-to-dining",
                    CASettlementProgramCausalKernel.Gathering);
                if (Aligned(state, function, door))
                { total += 1; parts.Add("aligned +1"); }
            }
            else if (function
                == CASettlementProgramCausalKernel.FoodPreparation)
            {
                relate("foodprep-to-dining",
                    CASettlementProgramCausalKernel.Gathering);
                relate("foodprep-to-cold",
                    CASettlementProgramCausalKernel.Storage);
            }
            else if (function == CASettlementProgramCausalKernel.Storage)
            {
                relate("foodprep-to-cold",
                    CASettlementProgramCausalKernel.FoodPreparation);
            }
            else if (function
                == CASettlementProgramCausalKernel.Production)
            {
                relate("hazard-to-bed",
                    CASettlementProgramCausalKernel.Housing);
            }
            else if (function == CASettlementProgramCausalKernel.Trade)
            {
                relate("foodprep-to-dining",
                    CASettlementProgramCausalKernel.Gathering);
            }

            // the corpus's looseness: local built density stays inside
            // the learned envelope band, which is what keeps yards and
            // gaps real instead of packing into a slab
            if (CASpatialRelationshipEvidence.TryBand("envelope-density",
                state.BiomeGroup, state.Quartile,
                out CASpatialEvidenceBand density))
            {
                int built = 0, area = 0;
                for (int z = Math.Max(0, box.minZ - 10);
                    z <= Math.Min(state.H - 1, box.maxZ + 10); z++)
                    for (int x = Math.Max(0, box.minX - 10);
                        x <= Math.Min(state.W - 1, box.maxX + 10); x++)
                    {
                        area++;
                        if (state.Cells[x + z * state.W]
                            != (byte)CAMorphCell.Empty) built++;
                    }
                double local = area == 0 ? 0
                    : (built + box.Area) / (double)area;
                int score = CASpatialRelationshipEvidence.Score(local,
                    density);
                total += score;
                parts.Add(CASpatialRelationshipEvidence.Describe(
                    "envelope-density", density, local, score));
            }

            // fronting a street or worn path is how circulation stays
            // usable as the settlement thickens
            int doorIdx = door.x + door.z * state.W;
            for (int d = 0; d < 4; d++)
            {
                int nx = door.x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                int nz = door.z + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nz < 0 || nx >= state.W || nz >= state.H)
                    continue;
                int n = nx + nz * state.W;
                if (state.Cells[n] == (byte)CAMorphCell.Street
                    || state.Traffic[n] >= 2)
                { total += 1; parts.Add("fronts path +1"); break; }
            }

            why = parts.Count > 0 ? string.Join("; ", parts)
                : "validity only";
            return total;
        }

        private static bool Aligned(GrowthState state, string function,
            IntVec2 door)
        {
            int matches = 0;
            foreach (Placed other in state.Buildings)
            {
                if (other.Function != function) continue;
                if (other.Door.x == door.x || other.Door.z == door.z)
                    matches++;
                if (matches >= 2) return true;
            }
            return false;
        }

        private static Placed Nearest(GrowthState state, string function,
            IntVec2 from)
        {
            Placed best = null;
            int bestDistance = int.MaxValue;
            foreach (Placed placed in state.Buildings)
            {
                if (placed.Function != function) continue;
                int distance = Math.Max(Math.Abs(placed.Door.x - from.x),
                    Math.Abs(placed.Door.z - from.z));
                if (distance < bestDistance)
                { bestDistance = distance; best = placed; }
            }
            return best;
        }

        private static void Commit(GrowthState state, string function,
            CellRect box, IntVec2 door)
        {
            for (int z = box.minZ; z <= box.maxZ; z++)
                for (int x = box.minX; x <= box.maxX; x++)
                {
                    int i = x + z * state.W;
                    bool edge = x == box.minX || x == box.maxX
                        || z == box.minZ || z == box.maxZ;
                    state.Cells[i] = edge ? (byte)CAMorphCell.Wall
                        : (byte)CAMorphCell.Floor;
                }
            int doorIdx = door.x + door.z * state.W;
            state.Cells[doorIdx] = (byte)CAMorphCell.Door;
            var placed = new Placed
            {
                Function = function,
                Box = box,
                Door = door,
                DoorIndex = doorIdx
            };
            state.Buildings.Add(placed);

            // ACTUAL USE: the new household or facility walks to what it
            // depends on, the walk wears the ground, and worn ground
            // becomes street -- circulation is accumulated use.
            foreach (string target in WalkTargets(function))
            {
                Placed destination = Nearest(state, target, door);
                if (destination == null || destination == placed) continue;
                Wear(state, door, destination.Door);
            }
        }

        private static IEnumerable<string> WalkTargets(string function)
        {
            if (function == CASettlementProgramCausalKernel.Housing)
            {
                yield return CASettlementProgramCausalKernel
                    .FoodPreparation;
                yield return CASettlementProgramCausalKernel.Gathering;
            }
            else if (function
                == CASettlementProgramCausalKernel.FoodPreparation)
                yield return CASettlementProgramCausalKernel.Storage;
            else if (function == CASettlementProgramCausalKernel.Storage)
                yield return CASettlementProgramCausalKernel
                    .FoodPreparation;
            else
                yield return CASettlementProgramCausalKernel.Gathering;
        }

        private static void Wear(GrowthState state, IntVec2 from,
            IntVec2 to)
        {
            state.Walks++;
            int x = from.x, z = from.z;
            for (int step = 0; step < 400; step++)
            {
                if (x == to.x && z == to.z) break;
                int dx = Math.Sign(to.x - x), dz = Math.Sign(to.z - z);
                // prefer the already-worn neighbor among the forward
                // moves: traffic begets traffic, which is how a shared
                // street emerges from independent trips
                var moves = new List<(int nx, int nz)>();
                if (dx != 0) moves.Add((x + dx, z));
                if (dz != 0) moves.Add((x, z + dz));
                if (moves.Count == 0) break;
                (int nx, int nz) chosen = moves[0];
                int chosenWear = -1;
                foreach ((int nx, int nz) move in moves)
                {
                    int i = move.nx + move.nz * state.W;
                    byte cell = state.Cells[i];
                    if (cell == (byte)CAMorphCell.Wall
                        || cell == (byte)CAMorphCell.Floor)
                    { continue; }
                    int wear = state.Traffic[i]
                        + (cell == (byte)CAMorphCell.Street ? 6 : 0);
                    if (wear > chosenWear)
                    { chosenWear = wear; chosen = move; }
                }
                if (chosenWear < 0)
                {
                    // both forward moves blocked: slide along the
                    // obstruction
                    var slides = new (int nx, int nz)[]
                    { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) };
                    bool moved = false;
                    foreach ((int nx, int nz) slide in slides)
                    {
                        if (slide.nx < 0 || slide.nz < 0
                            || slide.nx >= state.W
                            || slide.nz >= state.H) continue;
                        int i = slide.nx + slide.nz * state.W;
                        if (state.Cells[i] == (byte)CAMorphCell.Wall
                            || state.Cells[i] == (byte)CAMorphCell.Floor)
                            continue;
                        chosen = slide;
                        moved = true;
                        break;
                    }
                    if (!moved) break;
                }
                x = chosen.nx;
                z = chosen.nz;
                if (x < 0 || z < 0 || x >= state.W || z >= state.H) break;
                int at = x + z * state.W;
                state.Traffic[at]++;
                if (state.Traffic[at] >= 3
                    && state.Cells[at] == (byte)CAMorphCell.Empty)
                {
                    state.Cells[at] = (byte)CAMorphCell.Street;
                    state.StreetCells++;
                }
            }
        }
    }
}
