using System;
using System.Collections.Generic;

namespace ColonistAwareness
{
    // Deterministic, engine-free settlement layout generator. It grows built
    // areas, carves streets, places lots, and adds form-specific boundaries.
    //
    //   Tribal            round huts, loose pack, partial palisade
    //   Medieval          dense irregular pack, full wall, gated streets
    //   Industrial        carved street grid, blocky pack, no circuit
    //   FrontierHomestead farmhouse + barn + fenced field, one family
    //   districts > 1     joined districts with through-streets
    //   Outpost variants  fort, watchpost, checkpoint, or tent camp
    public enum CAMorphCell : byte
    {
        Empty = 0, Floor = 1, Wall = 2, Door = 3, Street = 4,
        Field = 5, Fence = 6,
        // a tent's footprint: the adapter pitches shelter here
        // instead of raising walls - the TENTCAMP outpost's mark
        TentFloor = 7
    }

    public enum CAMorphForm
    {
        Tribal = 0, Medieval = 1, Industrial = 2,
        FrontierHomestead = 3,
        // Cabin: one small dwelling in the wild - the SEED of a
        // homestead. A structure form only: its inhabitant is
        // whoever the simulation generates, on every free axis.
        Cabin = 4,
        // Outpost: a patrol base - the satellite a higher-
        // technology settlement posts over its approaches. Wears
        // one of the CAOutpostVariant faces, derived from the seed.
        Outpost = 5
    }

    // The faces an Outpost wears - security infrastructure as a
    // category. Derived from the seed by OutpostVariantFor (a fixed
    // mix, never a stream draw - the FORT face reproduces the
    // pre-variant outpost byte for byte), forceable for demos:
    //   Fort        the full wall ring around the packed base
    //   Watchpost   no wall at all - 1-2 structures + a floored
    //               platform pad, its ring and light spawned in-game
    //   Checkpoint  one road straight THROUGH the body edge to
    //               edge, a door ON the street mid-body, two short
    //               wall stubs flanking the gate
    //   TentCamp    2-4 TentFloor footprints instead of buildings,
    //               walls omitted, at most a scrap of fence
    public enum CAOutpostVariant
    {
        Fort = 0, Watchpost = 1, Checkpoint = 2, TentCamp = 3
    }

    public sealed class CAMorphLot
    {
        public List<int> cells = new List<int>();
        public int doorIndex = -1;
        public int area;
    }

    public sealed class CAMorphResult
    {
        public int w;
        public int h;
        public byte[] cells;
        public List<CAMorphLot> lots = new List<CAMorphLot>();
        // which CAOutpostVariant face this plan wears; -1 for every
        // form that is not an Outpost.
        public int outpostVariant = -1;
        // street-derivation receipt: how many lots walked and how many
        // street cells their walks laid -- distinct from the adapter's
        // terrain counter, which only counts cells whose ground it changed
        public int desireStreetCells;
        public int desireWalkedLots;
        // WATCHPOST: the platform pad's rim cells (the barricade
        // line the in-game spawner raises, already gapped toward
        // the camp) and the watch point its light stands on.
        public List<int> watchRing = new List<int>();
        public int watchPoint = -1;
        // TENTCAMP: one pitch cell per tent footprint.
        public List<int> tentSpots = new List<int>();
        public byte At(int x, int z) => cells[x + z * w];
    }

    public static class CASettlementMorphology
    {
        private struct Rng
        {
            private ulong s;
            public Rng(int seed)
            {
                s = (ulong)(uint)seed * 2685821657736338717UL
                    + 0x9E3779B97F4A7C15UL;
                Next();
            }
            public uint Next()
            {
                s ^= s << 13; s ^= s >> 7; s ^= s << 17;
                return (uint)(s >> 32);
            }
            public int Range(int min, int maxExclusive)
            {
                if (maxExclusive <= min) return min;
                return min + (int)(Next() % (uint)(maxExclusive - min));
            }
            public bool Chance(float p) => (Next() % 10000) < p * 10000f;
        }

        // densityScale carries the settlement's authored development standing.
        // BodyArea scaled a settlement purely by FORM, so a hamlet and a
        // regional capital of the same form covered the same ground and every
        // authored development axis was invisible. 1f reproduces the previous
        // extent exactly, so an unauthored caller is unchanged.
        // The causes arrive SEPARATELY, because they are separate:
        //   targetBodyCells   floor from the settlement's own program extents
        //                     (what its functions physically need), ceiling
        //                     from its land capacity. -1 keeps the legacy
        //                     form-fraction sizing for callers with no
        //                     represented requirements (the demo).
        //   accretion         historical development: 0 reads planned and
        //                     regular, 1 reads accumulated and irregular.
        //   wallKindOverride  -1 keeps the form's own idiom; 0 forces open,
        //                     1 partial, 2 full -- set by a represented
        //                     defense cause, afforded by economy.
        //   plazaCells        open market ground demanded by a trade
        //                     program; 0 when no such program exists.
        //   portSide          0..3 = map edge the water lies on when a
        //                     trade/transport program has waterfront; one
        //                     district core is biased toward that edge.
        public static CAMorphResult Generate(CAMorphForm form, int seed,
            int w, int h, bool[] passable = null, int districts = 1,
            int outpostVariant = -1, float densityScale = 1f,
            int targetBodyCells = -1, float accretion = 0.35f,
            int wallKindOverride = -1, int plazaCells = 0,
            int portSide = -1)
        {
            var m = new CAMorphResult { w = w, h = h, cells = new byte[w * h] };
            var rng = new Rng(seed);

            // 0. THE FACE an outpost wears - derived from the seed
            // outside the rng stream (FORT therefore reproduces the
            // pre-variant outpost exactly); a caller may force one
            // (the demo harness does). -1 for every other form.
            int variant = -1;
            if (form == CAMorphForm.Outpost)
            {
                variant = outpostVariant >= 0 && outpostVariant <= 3
                    ? outpostVariant
                    : (int)OutpostVariantFor(seed);
                m.outpostVariant = variant;
            }

            // 1. THE BODY: one blob per district, grown to union.
            var blob = new bool[w * h];
            var cores = new List<(int x, int z)>();
            int span = Math.Min(w, h);
            int per = targetBodyCells > 0
                ? Math.Min(targetBodyCells / Math.Max(1, districts),
                    span * span * 4 / (5 * Math.Max(1, districts)))
                : districts > 1
                ? Math.Min((int)(span * span * 0.55f * densityScale
                        / districts),
                    span * span * 4 / (5 * Math.Max(1, districts)))
                : Math.Min((int)(BodyArea(form, span, ref rng)
                        * densityScale),
                    span * span * 4 / 5);
            for (int i = 0; i < Math.Max(1, districts); i++)
            {
                double a = Math.PI * 2.0 * i / Math.Max(1, districts)
                    + rng.Range(0, 40) * Math.PI / 180.0;
                int r = districts > 1 ? span / 5 : 0;
                int cx = w / 2 + (int)(Math.Cos(a) * r) + rng.Range(-2, 3);
                int cz = h / 2 + (int)(Math.Sin(a) * r) + rng.Range(-2, 3);
                // the port quarter stands where the water is: the LAST
                // district core is pulled toward the watered edge, so the
                // trade fabric grows against its own shoreline
                if (portSide >= 0 && districts > 1
                    && i == Math.Max(1, districts) - 1)
                {
                    int pull = span / 3;
                    if (portSide == 0) cz = h / 2 + pull;
                    else if (portSide == 1) cx = w / 2 + pull;
                    else if (portSide == 2) cz = h / 2 - pull;
                    else cx = w / 2 - pull;
                }
                cores.Add((cx, cz));
                GrowBlob(blob, w, h, passable, cx, cz, per, ref rng);
            }

            // 2. STREETS carved through the body: core to rim, and core
            // to core when metropolitan. A CHECKPOINT is the exception:
            // its single street is a ROAD run straight through the body
            // edge to edge, gated where it runs deepest - stubs and door
            // land now so no building packs into the gate.
            var streetCells = new List<int>();
            // MARKET GROUND is a trade program's demand: an open square held
            // clear beside the principal core before anything packs, ringed
            // by the desire lines that cross it. No trade program, no plaza.
            if (plazaCells > 0)
            {
                int side = Math.Max(3, (int)Math.Sqrt(plazaCells));
                int px = cores[0].x + 2, pz = cores[0].z + 2;
                for (int dz = 0; dz < side; dz++)
                    for (int dx = 0; dx < side; dx++)
                    {
                        int cx2 = px + dx, cz2 = pz + dz;
                        if (cx2 < 0 || cz2 < 0 || cx2 >= w || cz2 >= h)
                            continue;
                        int idx2 = cx2 + cz2 * w;
                        if (!blob[idx2]) continue;
                        if (m.cells[idx2] == (byte)CAMorphCell.Empty)
                        {
                            m.cells[idx2] = (byte)CAMorphCell.Street;
                            streetCells.Add(idx2);
                        }
                    }
            }

            if (variant == (int)CAOutpostVariant.Checkpoint)
                CarveCheckpointRoad(m, blob, cores[0], ref rng,
                    streetCells);
            else foreach (var core in cores)
            {
                int rays = form == CAMorphForm.Cabin ? 1
                    : form == CAMorphForm.Outpost ? 1
                    : form == CAMorphForm.FrontierHomestead ? 1
                    : form == CAMorphForm.Industrial ? 0
                    : 2 + rng.Range(0, 2);
                for (int i = 0; i < rays; i++)
                {
                    double a = Math.PI * 2.0 * i / rays
                        + rng.Range(0, 50) * Math.PI / 180.0;
                    CarveStreet(m, blob, core.x, core.z, a, form,
                        ref rng, streetCells);
                }
            }
            for (int i = 1; i < cores.Count; i++)
                CarveBetween(m, blob, cores[i - 1], cores[i], form,
                    ref rng, streetCells);
            // Industrial draws no network here: its streets are DERIVED
            // from the finished buildings' doors after the pack (3b below).
            // The core-to-core carve above stays -- cores are real
            // destinations -- but gates at bearings and branches to
            // "underserved fabric" were still geometry before use, which is
            // why the network read as incomplete: doors it never connected,
            // space it never crossed for anyone.

            // 3. BUILDINGS packed into the body, densest by the streets,
            // interiors subdivided into rooms. A body too small to hold
            // a street packs from its own core - a cabin needs no road.
            // A TENTCAMP packs no building at all: its structures are
            // tent footprints, and each registers as a lot so the
            // adapter's no-lot contract still reads "a claim stands".
            if (streetCells.Count == 0)
            {
                int cc = cores[0].x + cores[0].z * w;
                if (cc >= 0 && cc < blob.Length && blob[cc])
                {
                    m.cells[cc] = (byte)CAMorphCell.Street;
                    streetCells.Add(cc);
                }
            }
            if (variant == (int)CAOutpostVariant.TentCamp)
                PlaceTentFootprints(m, blob, ref rng, streetCells);
            else
                PackBuildings(m, blob, form, ref rng, streetCells,
                    OutpostLotCap(variant, ref rng), accretion);

            // 3b. STREETS FOLLOW USE. Every finished lot's door walks to its
            // nearest core; the walks overlap and the overlaps are the
            // trunks; the strongest trunk continues past the buildings to
            // the body's rim as the settlement's exit. The network is
            // complete by construction -- a street exists exactly where
            // doors use it, and every door is connected.
            if (form == CAMorphForm.Industrial)
                DeriveDesireStreets(m, blob, cores, streetCells);

            // 4. THE WALL wraps the body itself - a single circuit on
            // the union's own rim; gates where streets leave. Of the
            // outpost faces only the FORT walls its rim: a watchpost
            // keeps open ground, a checkpoint holds only its stubs,
            // a tent camp at most a scrap of fence.
            if (wallKindOverride == 0)
            {
                // a represented cause says OPEN: no circuit, whatever the
                // form's habit
            }
            else if (wallKindOverride == 1)
                RimWall(m, blob, full: false, ref rng);
            else if (wallKindOverride == 2)
                RimWall(m, blob, full: true, ref rng);
            else if (form == CAMorphForm.Medieval)
                RimWall(m, blob, full: true, ref rng);
            else if (form == CAMorphForm.Outpost)
            {
                if (variant == (int)CAOutpostVariant.Fort)
                    RimWall(m, blob, full: true, ref rng);
                else if (variant == (int)CAOutpostVariant.TentCamp)
                    FenceScrap(m, blob, ref rng);
            }
            else if (form == CAMorphForm.Tribal)
                RimWall(m, blob, full: false, ref rng);
            else if (form == CAMorphForm.FrontierHomestead)
                RimWall(m, blob, full: true, ref rng, fence: true);

            // 4b. The WATCHPOST's platform: a floored pad pushed
            // toward the rim (overwatch faces OUT), its barricade
            // ring and light recorded for the in-game spawner.
            if (variant == (int)CAOutpostVariant.Watchpost)
                PlaceWatchPlatform(m, blob);

            // 5. WORKED LAND joined to the body at a gate, never adrift.
            if (form != CAMorphForm.Industrial
                && form != CAMorphForm.Outpost)
                AttachFields(m, blob, form, ref rng,
                    form == CAMorphForm.Cabin
                        ? (rng.Chance(0.6f) ? 1 : 0)
                        : form == CAMorphForm.FrontierHomestead ? 1
                        : 1 + rng.Range(0, 2));
            return m;
        }

        private static int BodyArea(CAMorphForm form, int span, ref Rng rng)
        {
            float f = form == CAMorphForm.Cabin ? 0.04f
                : form == CAMorphForm.Outpost ? 0.08f
                : form == CAMorphForm.FrontierHomestead ? 0.10f
                : form == CAMorphForm.Tribal ? 0.30f
                : form == CAMorphForm.Medieval ? 0.38f : 0.42f;
            return (int)(span * span * f) + rng.Range(0, span);
        }

        // Which face a seed deals an outpost. A fixed avalanche mix
        // OUTSIDE the rng stream, so the roll costs the stream no
        // draw: FORT at seed S is byte-identical to the pre-variant
        // outpost at seed S. Public so the wiring layer can know a
        // site's face without generating the whole plan.
        public static CAOutpostVariant OutpostVariantFor(int seed)
        {
            uint hv = (uint)seed * 2654435761u;
            hv ^= hv >> 15; hv *= 2246822519u; hv ^= hv >> 13;
            return (CAOutpostVariant)(hv % 4u);
        }

        // FORT keeps the outpost's native three-lot pack; a
        // WATCHPOST is 1-2 structures beside its platform; a
        // CHECKPOINT packs its guard pair beside the road. The
        // watchpost draw happens only on the watchpost path, so
        // every other face's stream is untouched by it.
        private static int OutpostLotCap(int variant, ref Rng rng)
        {
            if (variant == (int)CAOutpostVariant.Watchpost)
                return rng.Chance(0.5f) ? 2 : 1;
            if (variant == (int)CAOutpostVariant.Checkpoint) return 2;
            return 3;
        }

        // Accretion: candidates with more built neighbors join first -
        // compact mass, organically ragged edge.
        private static void GrowBlob(bool[] blob, int w, int h,
            bool[] passable, int cx, int cz, int target, ref Rng rng)
        {
            if (cx < 3) cx = 3; if (cx > w - 4) cx = w - 4;
            if (cz < 3) cz = 3; if (cz > h - 4) cz = h - 4;
            int start = cx + cz * w;
            if (!blob[start]) blob[start] = true;
            var frontier = new List<int> { start };
            int grown = 1, guard = target * 30;
            while (grown < target && guard-- > 0 && frontier.Count > 0)
            {
                int pick = frontier[rng.Range(0, frontier.Count)];
                int px = pick % w, pz = pick / w;
                int dir = rng.Range(0, 4);
                int nx = px + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                int nz = pz + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                if (nx < 3 || nz < 3 || nx >= w - 3 || nz >= h - 3)
                    continue;
                int n = nx + nz * w;
                if (blob[n]) continue;
                if (passable != null && !passable[n]) continue;
                int support = 0;
                if (nx > 0 && blob[n - 1]) support++;
                if (nx < w - 1 && blob[n + 1]) support++;
                if (nz > 0 && blob[n - w]) support++;
                if (nz < h - 1 && blob[n + w]) support++;
                // compactness: lone fingers rarely, pockets eagerly.
                // Tiny bodies (cabins, outposts) grow as solid
                // clearings - a sponge cannot seat even one building.
                if (target >= 140 && support == 1
                    && !rng.Chance(0.55f)) continue;
                blob[n] = true; grown++;
                frontier.Add(n);
                if (frontier.Count > 600)
                    frontier.RemoveAt(rng.Range(0, 300));
            }
        }

        private static void CarveStreet(CAMorphResult m, bool[] blob,
            int x, int z, double angle, CAMorphForm form, ref Rng rng,
            List<int> street)
        {
            double dx = Math.Cos(angle), dz = Math.Sin(angle);
            double px = x, pz = z;
            int wdt = form == CAMorphForm.Industrial ? 2 : 1;
            for (int step = 0; step < Math.Max(m.w, m.h); step++)
            {
                if (form != CAMorphForm.Industrial && rng.Chance(0.30f))
                {
                    double turn = (rng.Range(0, 40) - 20) * Math.PI / 180.0;
                    double na = Math.Atan2(dz, dx) + turn;
                    dx = Math.Cos(na); dz = Math.Sin(na);
                }
                px += dx; pz += dz;
                int ix = (int)Math.Round(px), iz = (int)Math.Round(pz);
                if (ix < 1 || iz < 1 || ix >= m.w - 1 || iz >= m.h - 1)
                    return;
                int idx = ix + iz * m.w;
                if (!blob[idx]) return;          // street ends at the rim
                for (int o = 0; o < wdt; o++)
                {
                    int c = Math.Min(idx + o, m.cells.Length - 1);
                    if (m.cells[c] == (byte)CAMorphCell.Empty)
                    { m.cells[c] = (byte)CAMorphCell.Street; street.Add(c); }
                }
            }
        }

        // CHECKPOINT: the street is a ROAD and the base straddles
        // it. The road is a straight chord pivoted on the blob cell
        // nearest the body's centroid (the core can sit in a
        // terrain pocket; the mass cannot), on the best of eight
        // bearings fanned from one seeded draw - best means both
        // arms run far before leaving the body, so the line runs
        // THROUGH it edge to edge, never a dead end. Then the gate:
        // the pivot cell turns Door (a gate ON the road) and two
        // short wall stubs walk away from it, snapped to the
        // perpendicular's dominant axis so each stub is one
        // straight wall, not a diagonal scatter.
        private static void CarveCheckpointRoad(CAMorphResult m,
            bool[] blob, (int x, int z) core, ref Rng rng,
            List<int> street)
        {
            int w = m.w, h = m.h;
            long sx = 0, sz = 0; int n = 0;
            for (int i = 0; i < blob.Length; i++)
                if (blob[i]) { sx += i % w; sz += i / w; n++; }
            if (n == 0) return;
            double cx = sx / (double)n, cz = sz / (double)n;
            int pivot = -1; double pivotD = double.MaxValue;
            for (int i = 0; i < blob.Length; i++)
            {
                if (!blob[i]) continue;
                double d = Sq(i % w - cx) + Sq(i / w - cz);
                if (d < pivotD - 1e-9) { pivotD = d; pivot = i; }
            }
            if (pivot < 0) return;
            int px0 = pivot % w, pz0 = pivot / w;

            int Reach(double dx, double dz, int sgn)
            {
                double px = px0, pz = pz0; int len = 0;
                for (int step = 0; step < Math.Max(w, h); step++)
                {
                    px += sgn * dx; pz += sgn * dz;
                    int ix = (int)Math.Round(px);
                    int iz = (int)Math.Round(pz);
                    if (ix < 1 || iz < 1 || ix >= w - 1
                        || iz >= h - 1) break;
                    if (!blob[ix + iz * w]) break;
                    len++;
                }
                return len;
            }

            double baseA = rng.Range(0, 180) * Math.PI / 180.0;
            double bestA = baseA; int bestScore = -1;
            for (int k = 0; k < 8; k++)
            {
                double a = baseA + k * Math.PI / 8.0;
                double dx = Math.Cos(a), dz = Math.Sin(a);
                int fwd = Reach(dx, dz, 1), back = Reach(dx, dz, -1);
                int score = Math.Min(fwd, back) * 1000 + fwd + back;
                if (score > bestScore) { bestScore = score; bestA = a; }
            }

            double rdx = Math.Cos(bestA), rdz = Math.Sin(bestA);
            if (m.cells[pivot] == (byte)CAMorphCell.Empty)
            {
                m.cells[pivot] = (byte)CAMorphCell.Street;
                street.Add(pivot);
            }
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double px = px0, pz = pz0;
                for (int step = 0; step < Math.Max(w, h); step++)
                {
                    px += sgn * rdx; pz += sgn * rdz;
                    int ix = (int)Math.Round(px);
                    int iz = (int)Math.Round(pz);
                    if (ix < 1 || iz < 1 || ix >= w - 1
                        || iz >= h - 1) break;
                    int idx = ix + iz * w;
                    if (!blob[idx]) break;   // rim reached: road done
                    if (m.cells[idx] == (byte)CAMorphCell.Empty)
                    {
                        m.cells[idx] = (byte)CAMorphCell.Street;
                        street.Add(idx);
                    }
                }
            }

            if (m.cells[pivot] != (byte)CAMorphCell.Street) return;
            m.cells[pivot] = (byte)CAMorphCell.Door;
            int rx, rz;
            if (Math.Abs(-rdz) >= Math.Abs(rdx))
            { rx = Math.Sign(-rdz); rz = 0; }
            else { rx = 0; rz = Math.Sign(rdx); }
            if (rx == 0 && rz == 0) rx = 1;
            int stub = 2 + rng.Range(0, 2);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int k = 1; k <= stub; k++)
                {
                    int gx = px0 + sgn * k * rx;
                    int gz = pz0 + sgn * k * rz;
                    if (gx < 1 || gz < 1 || gx >= w - 1
                        || gz >= h - 1) break;
                    int idx = gx + gz * w;
                    if (!blob[idx]) break;
                    if (m.cells[idx] != (byte)CAMorphCell.Empty)
                        break;
                    m.cells[idx] = (byte)CAMorphCell.Wall;
                }
        }

        private static void CarveBetween(CAMorphResult m, bool[] blob,
            (int x, int z) a, (int x, int z) b, CAMorphForm form,
            ref Rng rng, List<int> street)
        {
            int x = a.x, z = a.z;
            int guard = m.w * 4;
            while ((x != b.x || z != b.z) && guard-- > 0)
            {
                if (x != b.x && (z == b.z || rng.Chance(0.55f)))
                    x += Math.Sign(b.x - x);
                else if (z != b.z) z += Math.Sign(b.z - z);
                int idx = x + z * m.w;
                if (idx < 0 || idx >= m.cells.Length) return;
                byte c = m.cells[idx];
                if (c == (byte)CAMorphCell.Empty && blob[idx])
                { m.cells[idx] = (byte)CAMorphCell.Street; street.Add(idx); }
                else if (c == (byte)CAMorphCell.Wall)
                    m.cells[idx] = (byte)CAMorphCell.Door;
            }
        }

        // STREETS FOLLOW USE: desire lines from every door.
        //
        // Buildings pack first, from the cores. Each finished lot's door then
        // walks greedily toward its nearest core through open ground,
        // deflecting around anything standing, and every cell a walk crosses
        // accumulates traffic. Cells with traffic become streets; cells where
        // several walks agree widen into trunks; and the heaviest trunk is
        // continued outward to the rim so the town has the exit its own
        // circulation earned. Nothing is drawn that no door uses.
        private static void DeriveDesireStreets(CAMorphResult m, bool[] blob,
            List<(int x, int z)> cores, List<int> street)
        {
            int w = m.w, h = m.h;
            if (m.lots.Count == 0 || cores.Count == 0) return;
            var traffic = new int[w * h];
            foreach (int s0 in street)
                if (s0 >= 0 && s0 < traffic.Length) traffic[s0]++;

            bool Open(int idx)
            {
                byte c = m.cells[idx];
                return c == (byte)CAMorphCell.Empty
                    || c == (byte)CAMorphCell.Street;
            }

            foreach (CAMorphLot lot in m.lots)
            {
                if (lot == null) continue;
                int doorIdx = lot.doorIndex >= 0 ? lot.doorIndex
                    : (lot.cells != null && lot.cells.Count > 0
                        ? lot.cells[0] : -1);
                if (doorIdx < 0) continue;
                int dx0 = doorIdx % w, dz0 = doorIdx / w;
                // step out of the door onto open ground
                int x = -1, z = -1;
                foreach (var (nx, nz) in Neigh(dx0, dz0))
                {
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int n = nx + nz * w;
                    // a door on the body's rim opens onto ground outside the
                    // blob; the walk may STEP there -- only street MARKING is
                    // confined to the body
                    if (!Open(n)) continue;
                    x = nx; z = nz; break;
                }
                if (x < 0) continue;
                var goal = cores[0];
                int best = int.MaxValue;
                for (int i = 0; i < cores.Count; i++)
                {
                    int cx = cores[i].x - x, cz = cores[i].z - z;
                    int d = cx * cx + cz * cz;
                    if (d < best) { best = d; goal = cores[i]; }
                }
                int guard = w * 4;
                int lastDx = 0, lastDz = 0;
                while ((x != goal.x || z != goal.z) && guard-- > 0)
                {
                    int idx = x + z * w;
                    if (blob[idx]) traffic[idx]++;
                    int sx = Math.Sign(goal.x - x);
                    int sz = Math.Sign(goal.z - z);
                    // primary axis first, deflect around obstruction
                    var steps = Math.Abs(goal.x - x) >= Math.Abs(goal.z - z)
                        ? new[] { (sx, 0), (0, sz), (0, -sz), (-sx, 0) }
                        : new[] { (0, sz), (sx, 0), (-sx, 0), (0, -sz) };
                    // Prefer steps INSIDE the body -- the walk is the
                    // town's own circulation. Freeing movement entirely let
                    // walks leave the body and skirt to the core through
                    // unobstructed outside ground, marking nothing (run 7:
                    // 0 cells from 14 lots). Outside steps remain only as
                    // the doorway escape when no inside step exists.
                    bool moved = false;
                    for (int pass = 0; pass < 2 && !moved; pass++)
                        foreach (var (mx, mz) in steps)
                        {
                            if (mx == 0 && mz == 0) continue;
                            if (mx == -lastDx && mz == -lastDz) continue;
                            int nx = x + mx, nz = z + mz;
                            if (nx < 0 || nz < 0 || nx >= w || nz >= h)
                                continue;
                            int n = nx + nz * w;
                            if (!Open(n)) continue;
                            if (pass == 0 && !blob[n]) continue;
                            x = nx; z = nz; lastDx = mx; lastDz = mz;
                            moved = true;
                            break;
                        }
                    if (!moved) break;
                }
            }

            // walked ground becomes street; agreement becomes trunk
            int laid = 0;
            for (int i = 0; i < traffic.Length; i++)
            {
                if (traffic[i] <= 0 || !blob[i]) continue;
                if (m.cells[i] == (byte)CAMorphCell.Empty)
                {
                    m.cells[i] = (byte)CAMorphCell.Street;
                    street.Add(i);
                    laid++;
                }
            }
            m.desireStreetCells = laid;
            m.desireWalkedLots = m.lots.Count;
            for (int i = 0; i < traffic.Length; i++)
            {
                if (traffic[i] < 3) continue;
                int cx = i % w, cz = i / w;
                foreach (var (nx, nz) in Neigh(cx, cz))
                {
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int n = nx + nz * w;
                    if (!blob[n]
                        || m.cells[n] != (byte)CAMorphCell.Empty) continue;
                    m.cells[n] = (byte)CAMorphCell.Street;
                    street.Add(n);
                    break;
                }
            }

            // the exit the circulation earned: continue the heaviest street
            // adjacent to the busiest core outward to the rim
            int busiest = -1, load = 0;
            var hub = cores[0];
            foreach (var core in cores)
            {
                int total = 0;
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = core.x + dx, nz = core.z + dz;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                        total += traffic[nx + nz * w];
                    }
                if (total > load) { load = total; hub = core; busiest = total; }
            }
            if (busiest <= 0) return;
            // dominant outward direction = away from the body's centroid
            long sxAcc = 0, szAcc = 0; int nBody = 0;
            for (int i = 0; i < blob.Length; i++)
                if (blob[i]) { sxAcc += i % w; szAcc += i / w; nBody++; }
            if (nBody == 0) return;
            int ox = Math.Sign(hub.x - (int)(sxAcc / nBody));
            int oz = Math.Sign(hub.z - (int)(szAcc / nBody));
            if (ox == 0 && oz == 0) ox = 1;
            int ex = hub.x, ez = hub.z;
            int guard2 = w * 2;
            while (guard2-- > 0)
            {
                int nx = ex + ox, nz = ez + oz;
                if (nx < 0 || nz < 0 || nx >= w || nz >= h) break;
                int n = nx + nz * w;
                if (!blob[n]) break;
                if (m.cells[n] == (byte)CAMorphCell.Empty)
                { m.cells[n] = (byte)CAMorphCell.Street; street.Add(n); }
                else if (m.cells[n] != (byte)CAMorphCell.Street) break;
                ex = nx; ez = nz;
            }
        }

        // Distance-to-street field (bounded BFS) - shared by the
        // building pack and the tent camp's footprint placement.
        private static int[] StreetDistance(CAMorphResult m,
            bool[] blob, List<int> street, int horizon = 14)
        {
            int w = m.w, h = m.h;
            var dist = new int[w * h];
            for (int i = 0; i < dist.Length; i++) dist[i] = 9999;
            var q = new Queue<int>();
            foreach (int s in street) { dist[s] = 0; q.Enqueue(s); }
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                int cx = c % w, cz = c / w;
                foreach (var (nx, nz) in Neigh(cx, cz))
                {
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int n = nx + nz * w;
                    if (!blob[n] || dist[n] <= dist[c] + 1) continue;
                    dist[n] = dist[c] + 1;
                    if (dist[n] < horizon) q.Enqueue(n);
                }
            }
            return dist;
        }

        // Dense packing: walk the body sorted by street-distance, raise
        // buildings with alleys of one cell, subdivide the big ones.
        private static void PackBuildings(CAMorphResult m, bool[] blob,
            CAMorphForm form, ref Rng rng, List<int> street,
            int outpostCap = 3, float accretion = 0.35f)
        {
            int w = m.w, h = m.h;
            // Industrial packs from its cores and derives streets from the
            // finished doors afterwards, so its distance field must reach the
            // whole body: the 14-cell horizon that suits a pre-carved network
            // collapsed a core-seeded town to whatever stood within fourteen
            // cells of one point.
            int horizon = form == CAMorphForm.Industrial
                ? Math.Max(w, h) : 14;
            int[] dist = StreetDistance(m, blob, street, horizon);
            var order = new List<int>();
            for (int i = 0; i < blob.Length; i++)
                if (blob[i] && m.cells[i] == (byte)CAMorphCell.Empty
                    && dist[i] > 0 && dist[i] < horizon - 1) order.Add(i);
            // near streets first, stable shuffle within bands
            order.Sort((a, b) => dist[a] != dist[b] ? dist[a] - dist[b]
                : ((a * 2654435761u) % 97).CompareTo(
                    (b * 2654435761u) % 97));

            int budgetGuard = 4000;
            foreach (int anchor in order)
            {
                if (budgetGuard-- <= 0) break;
                if (m.cells[anchor] != (byte)CAMorphCell.Empty) continue;
                // ACCUMULATED settlements are irregular: history leaves odd
                // gaps and mismatched frontages that a planned town lacks.
                if (accretion > 0.05f
                    && rng.Chance(Math.Min(0.35f, accretion * 0.3f)))
                    continue;
                int bw, bh;
                switch (form)
                {
                    case CAMorphForm.Tribal:
                        bw = 4 + rng.Range(0, 3); bh = 4 + rng.Range(0, 2);
                        break;
                    case CAMorphForm.FrontierHomestead:
                        bw = 7 + rng.Range(0, 3); bh = 6 + rng.Range(0, 2);
                        break;
                    case CAMorphForm.Cabin:
                        bw = 5 + rng.Range(0, 2); bh = 4 + rng.Range(0, 2);
                        break;
                    case CAMorphForm.Outpost:
                        bw = 6 + rng.Range(0, 3); bh = 5 + rng.Range(0, 2);
                        break;
                    case CAMorphForm.Industrial:
                        bw = 6 + rng.Range(0, 5); bh = 5 + rng.Range(0, 4);
                        break;
                    default:
                        bw = 5 + rng.Range(0, 5); bh = 5 + rng.Range(0, 3);
                        break;
                }
                TryRaise(m, blob, form, anchor % w, anchor / w, bw, bh,
                    ref rng);
                if (form == CAMorphForm.FrontierHomestead
                    && m.lots.Count >= 3) break;
                if (form == CAMorphForm.Cabin && m.lots.Count >= 1)
                    break;
                if (form == CAMorphForm.Outpost
                    && m.lots.Count >= outpostCap) break;
            }

            // Guaranteed seat: a body that packed nothing shrinks its
            // footprint until one building lands - no empty claims.
            if (m.lots.Count == 0)
            {
                var smalls = new (int bw, int bh)[]
                    { (5, 4), (4, 4), (4, 3) };
                for (int i = 0; i < blob.Length && m.lots.Count == 0; i++)
                {
                    if (!blob[i]) continue;
                    foreach (var sz in smalls)
                    {
                        TryRaise(m, blob, form, i % w, i / w,
                            sz.bw, sz.bh, ref rng);
                        if (m.lots.Count > 0) break;
                    }
                }
            }
        }

        private static void TryRaise(CAMorphResult m, bool[] blob,
            CAMorphForm form, int bx, int bz, int bw, int bh, ref Rng rng)
        {
            int w = m.w, h = m.h;
            if (bx + bw >= w - 1 || bz + bh >= h - 1) return;
            // footprint must sit in the body with a one-cell alley clear
            for (int x = -1; x <= bw; x++)
                for (int z = -1; z <= bh; z++)
                {
                    int gx = bx + x, gz = bz + z;
                    if (gx < 0 || gz < 0 || gx >= w || gz >= h) return;
                    int idx = gx + gz * w;
                    bool inside = x >= 0 && z >= 0 && x < bw && z < bh;
                    if (inside && !blob[idx]) return;
                    if (m.cells[idx] != (byte)CAMorphCell.Empty
                        && m.cells[idx] != (byte)CAMorphCell.Street)
                        return;
                    if (inside
                        && m.cells[idx] == (byte)CAMorphCell.Street)
                        return;
                }

            bool round = form == CAMorphForm.Tribal;
            bool[,] shape = new bool[bw, bh];
            for (int x = 0; x < bw; x++)
                for (int z = 0; z < bh; z++)
                {
                    if (round)
                    {
                        double nx = (x - (bw - 1) / 2.0) / (bw / 2.0);
                        double nz = (z - (bh - 1) / 2.0) / (bh / 2.0);
                        shape[x, z] = nx * nx + nz * nz <= 1.15;
                    }
                    else shape[x, z] = true;
                }
            if (!round && rng.Chance(0.45f))
            {   // bite one corner - the irregular eave
                int cw = 1 + rng.Range(0, bw / 2);
                int ch = 1 + rng.Range(0, bh / 2);
                bool left = rng.Chance(0.5f), bottom = rng.Chance(0.5f);
                for (int x = 0; x < cw; x++)
                    for (int z = 0; z < ch; z++)
                        shape[left ? x : bw - 1 - x,
                            bottom ? z : bh - 1 - z] = false;
            }

            for (int x = 0; x < bw; x++)
                for (int z = 0; z < bh; z++)
                {
                    if (!shape[x, z]) continue;
                    int gx = bx + x, gz = bz + z;
                    bool edge = x == 0 || z == 0 || x == bw - 1
                        || z == bh - 1
                        || !shape[x - 1, z] || !shape[x + 1, z]
                        || !shape[x, z - 1] || !shape[x, z + 1];
                    m.cells[gx + gz * w] = edge ? (byte)CAMorphCell.Wall
                        : (byte)CAMorphCell.Floor;
                }

            // subdivide interiors: real rooms inside real houses
            if (!round && bw >= 7 && bh >= 6)
            {
                int cutX = bx + 2 + rng.Range(0, bw - 4);
                for (int z = bz + 1; z < bz + bh - 1; z++)
                {
                    int idx = cutX + z * w;
                    if (m.cells[idx] == (byte)CAMorphCell.Floor)
                        m.cells[idx] = (byte)CAMorphCell.Wall;
                }
                int doorZ = bz + 1 + rng.Range(0, bh - 2);
                if (m.cells[cutX + doorZ * w] == (byte)CAMorphCell.Wall
                    && cutX > bx && cutX < bx + bw - 1)
                    m.cells[cutX + doorZ * w] = (byte)CAMorphCell.Door;
                if (bh >= 9 && rng.Chance(0.6f))
                {
                    int cutZ = bz + 2 + rng.Range(0, bh - 4);
                    for (int x = bx + 1; x < bx + bw - 1; x++)
                    {
                        int idx = x + cutZ * w;
                        if (m.cells[idx] == (byte)CAMorphCell.Floor)
                            m.cells[idx] = (byte)CAMorphCell.Wall;
                    }
                    int doorX = bx + 1 + rng.Range(0, bw - 2);
                    if (m.cells[doorX + cutZ * w] == (byte)CAMorphCell.Wall)
                        m.cells[doorX + cutZ * w] = (byte)CAMorphCell.Door;
                }
            }

            // lots: flood the interior into rooms; door each room chain
            CollectLots(m, bx, bz, bw, bh);
            // exterior door: wall cell adjacent to floor inside and
            // street/alley outside, nearest to any street
            int best = -1; int bestScore = int.MaxValue;
            for (int x = 0; x < bw; x++)
                for (int z = 0; z < bh; z++)
                {
                    int gx = bx + x, gz = bz + z;
                    int idx = gx + gz * w;
                    if (m.cells[idx] != (byte)CAMorphCell.Wall) continue;
                    bool inFloor = false, outOpen = false;
                    int score = int.MaxValue;
                    foreach (var (nx, nz) in Neigh(gx, gz))
                    {
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h)
                            continue;
                        byte nc = m.cells[nx + nz * w];
                        if (nc == (byte)CAMorphCell.Floor) inFloor = true;
                        if (nc == (byte)CAMorphCell.Street)
                        { outOpen = true; score = 0; }
                        else if (nc == (byte)CAMorphCell.Empty)
                        { outOpen = true; score = Math.Min(score, 1); }
                    }
                    if (inFloor && outOpen && score < bestScore)
                    { bestScore = score; best = idx; }
                }
            if (best >= 0) m.cells[best] = (byte)CAMorphCell.Door;
        }

        private static void CollectLots(CAMorphResult m, int bx, int bz,
            int bw, int bh)
        {
            int w = m.w;
            var seen = new HashSet<int>();
            for (int x = 1; x < bw - 1; x++)
                for (int z = 1; z < bh - 1; z++)
                {
                    int start = (bx + x) + (bz + z) * w;
                    if (m.cells[start] != (byte)CAMorphCell.Floor
                        || seen.Contains(start)) continue;
                    var lot = new CAMorphLot();
                    var q = new Queue<int>();
                    q.Enqueue(start); seen.Add(start);
                    while (q.Count > 0)
                    {
                        int c = q.Dequeue();
                        lot.cells.Add(c);
                        int cx = c % w, cz = c / w;
                        foreach (var (nx, nz) in Neigh(cx, cz))
                        {
                            int n = nx + nz * w;
                            if (seen.Contains(n)) continue;
                            if (m.cells[n] == (byte)CAMorphCell.Floor)
                            { seen.Add(n); q.Enqueue(n); }
                        }
                    }
                    lot.area = lot.cells.Count;
                    if (lot.area >= 4) m.lots.Add(lot);
                }
        }

        private static IEnumerable<(int, int)> Neigh(int x, int z)
        {
            yield return (x + 1, z); yield return (x - 1, z);
            yield return (x, z + 1); yield return (x, z - 1);
        }

        // TENT CAMP: the structures ARE tents. 2-4 footprints of
        // TentFloor laid nearest the street with one-cell lanes,
        // each registered as a lot (the adapter's no-lot contract:
        // no lots grown = no claim) with no door - a tent's door
        // belongs to the tent, not the plan. One pitch cell per
        // footprint is recorded for the in-game tent spawner.
        private static void PlaceTentFootprints(CAMorphResult m,
            bool[] blob, ref Rng rng, List<int> street)
        {
            int w = m.w;
            int[] dist = StreetDistance(m, blob, street);
            var order = new List<int>();
            for (int i = 0; i < blob.Length; i++)
                if (blob[i] && m.cells[i] == (byte)CAMorphCell.Empty
                    && dist[i] > 0 && dist[i] < 13) order.Add(i);
            order.Sort((a, b) => dist[a] != dist[b] ? dist[a] - dist[b]
                : ((a * 2654435761u) % 97).CompareTo(
                    (b * 2654435761u) % 97));

            int want = 2 + rng.Range(0, 3);
            int bw = 4 + rng.Range(0, 2), bh = 3 + rng.Range(0, 2);
            foreach (int anchor in order)
            {
                if (m.lots.Count >= want) break;
                if (TryPitch(m, blob, anchor % w, anchor / w, bw, bh))
                { bw = 4 + rng.Range(0, 2); bh = 3 + rng.Range(0, 2); }
            }
            // guaranteed pitch: shrink until one footprint lands -
            // a camp with no tents would be no claim at all.
            if (m.lots.Count == 0)
                for (int i = 0; i < blob.Length && m.lots.Count == 0;
                    i++)
                {
                    if (!blob[i]) continue;
                    if (!TryPitch(m, blob, i % w, i / w, 4, 3))
                        TryPitch(m, blob, i % w, i / w, 3, 3);
                }
        }

        private static bool TryPitch(CAMorphResult m, bool[] blob,
            int bx, int bz, int bw, int bh)
        {
            int w = m.w, h = m.h;
            if (bx + bw >= w - 1 || bz + bh >= h - 1) return false;
            for (int x = -1; x <= bw; x++)
                for (int z = -1; z <= bh; z++)
                {
                    int gx = bx + x, gz = bz + z;
                    if (gx < 0 || gz < 0 || gx >= w || gz >= h)
                        return false;
                    int idx = gx + gz * w;
                    bool inside = x >= 0 && z >= 0 && x < bw
                        && z < bh;
                    if (inside && (!blob[idx] || m.cells[idx]
                        != (byte)CAMorphCell.Empty)) return false;
                    if (!inside && m.cells[idx]
                        == (byte)CAMorphCell.TentFloor)
                        return false;   // one-cell lane between tents
                }
            var lot = new CAMorphLot();
            for (int x = 0; x < bw; x++)
                for (int z = 0; z < bh; z++)
                {
                    int idx = (bx + x) + (bz + z) * w;
                    m.cells[idx] = (byte)CAMorphCell.TentFloor;
                    lot.cells.Add(idx);
                }
            lot.area = lot.cells.Count;
            m.lots.Add(lot);
            m.tentSpots.Add((bx + bw / 2) + (bz + bh / 2) * w);
            return true;
        }

        // WATCHPOST: no wall at all. The engine floors a small pad
        // pushed as far from the body's centroid as it fits - the
        // overwatch faces OUT - rings the pad's rim for the
        // barricade line the in-game spawner raises, sheds the ring
        // cells nearest the camp for the entry gap, and marks the
        // watch point the light stands on. Pure argmax, no draws.
        private static void PlaceWatchPlatform(CAMorphResult m,
            bool[] blob)
        {
            int w = m.w, h = m.h;
            long sx = 0, sz = 0; int n = 0;
            for (int i = 0; i < blob.Length; i++)
                if (blob[i]) { sx += i % w; sz += i / w; n++; }
            if (n == 0) return;
            double cx = sx / (double)n, cz = sz / (double)n;
            for (int size = 4; size >= 3; size--)
            {
                int best = -1; double bestD = -1.0;
                for (int i = 0; i < blob.Length; i++)
                {
                    int bx = i % w, bz = i / w;
                    if (bx + size >= w - 1 || bz + size >= h - 1)
                        continue;
                    if (!PadFits(m, blob, bx, bz, size)) continue;
                    double px = bx + (size - 1) / 2.0 - cx;
                    double pz = bz + (size - 1) / 2.0 - cz;
                    double d = px * px + pz * pz;
                    if (d > bestD + 1e-9) { bestD = d; best = i; }
                }
                if (best < 0) continue;
                int ax = best % w, az = best / w;
                var ring = new List<int>();
                for (int x = 0; x < size; x++)
                    for (int z = 0; z < size; z++)
                    {
                        int idx = (ax + x) + (az + z) * w;
                        m.cells[idx] = (byte)CAMorphCell.Floor;
                        if (x == 0 || z == 0 || x == size - 1
                            || z == size - 1) ring.Add(idx);
                    }
                ring.Sort((a, b) =>
                {
                    double da = Sq(a % w - cx) + Sq(a / w - cz);
                    double db = Sq(b % w - cx) + Sq(b / w - cz);
                    return da != db ? da.CompareTo(db)
                        : a.CompareTo(b);
                });
                for (int i = size - 1; i < ring.Count; i++)
                    m.watchRing.Add(ring[i]);
                m.watchPoint = (ax + size / 2) + (az + size / 2) * w;
                return;
            }
        }

        private static double Sq(double v) => v * v;

        private static bool PadFits(CAMorphResult m, bool[] blob,
            int bx, int bz, int size)
        {
            int w = m.w, h = m.h;
            for (int x = -1; x <= size; x++)
                for (int z = -1; z <= size; z++)
                {
                    int gx = bx + x, gz = bz + z;
                    if (gx < 0 || gz < 0 || gx >= w || gz >= h)
                        return false;
                    int idx = gx + gz * w;
                    bool inside = x >= 0 && z >= 0 && x < size
                        && z < size;
                    byte c = m.cells[idx];
                    if (inside && (!blob[idx]
                        || c != (byte)CAMorphCell.Empty)) return false;
                    if (!inside && c != (byte)CAMorphCell.Empty
                        && c != (byte)CAMorphCell.Street) return false;
                }
            return true;
        }

        // The TENT CAMP's maybe-fence: one short arc on the rim - a
        // scrap of pale somebody started, never a circuit.
        private static void FenceScrap(CAMorphResult m, bool[] blob,
            ref Rng rng)
        {
            if (!rng.Chance(0.65f)) return;
            int w = m.w, h = m.h;
            long sx = 0, sz = 0; int n = 0;
            for (int i = 0; i < blob.Length; i++)
                if (blob[i]) { sx += i % w; sz += i / w; n++; }
            if (n == 0) return;
            double cx = sx / (double)n, cz = sz / (double)n;
            double at = rng.Range(0, 360) * Math.PI / 180.0;
            for (int i = 0; i < blob.Length; i++)
            {
                if (!blob[i]) continue;
                if (m.cells[i] != (byte)CAMorphCell.Empty) continue;
                int px = i % w, pz = i / w;
                bool rim = false;
                foreach (var (nx, nz) in Neigh(px, pz))
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h
                        || !blob[nx + nz * w]) { rim = true; break; }
                if (!rim) continue;
                double da = Math.Atan2(pz - cz, px - cx) - at;
                while (da > Math.PI) da -= Math.PI * 2.0;
                while (da < -Math.PI) da += Math.PI * 2.0;
                if (Math.Abs(da) < 0.6)
                    m.cells[i] = (byte)CAMorphCell.Fence;
            }
        }

        // One ring on the body's own rim. Gates where streets touch it.
        private static void RimWall(CAMorphResult m, bool[] blob,
            bool full, ref Rng rng, bool fence = false)
        {
            int w = m.w, h = m.h;
            int gates = 0;
            for (int i = 0; i < blob.Length; i++)
            {
                if (!blob[i]) continue;
                int cx = i % w, cz = i / w;
                bool rim = false;
                foreach (var (nx, nz) in Neigh(cx, cz))
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h
                        || !blob[nx + nz * w]) { rim = true; break; }
                if (!rim) continue;
                byte c = m.cells[i];
                if (c == (byte)CAMorphCell.Street)
                {
                    if (gates < 4) { gates++; continue; }
                    m.cells[i] = fence ? (byte)CAMorphCell.Fence
                        : (byte)CAMorphCell.Wall;
                    continue;
                }
                if (c != (byte)CAMorphCell.Empty) continue;
                if (!full && ((cx * 7 + cz * 13) % 11) < 4) continue;
                m.cells[i] = fence ? (byte)CAMorphCell.Fence
                    : (byte)CAMorphCell.Wall;
            }
        }

        // Fields joined to the body: start just outside a gate or rim
        // opening and bloom outward - worked land at the door.
        private static void AttachFields(CAMorphResult m, bool[] blob,
            CAMorphForm form, ref Rng rng, int count)
        {
            int w = m.w, h = m.h;
            var rim = new List<int>();
            for (int i = 0; i < blob.Length; i++)
            {
                if (blob[i]) continue;
                int cx = i % w, cz = i / w;
                foreach (var (nx, nz) in Neigh(cx, cz))
                    if (nx >= 0 && nz >= 0 && nx < w && nz < h
                        && blob[nx + nz * w]) { rim.Add(i); break; }
            }
            for (int f = 0; f < count && rim.Count > 0; f++)
            {
                int start = rim[rng.Range(0, rim.Count)];
                int grown = 0;
                int target = 45 + rng.Range(0, 60);
                var q = new List<int> { start };
                var mine = new HashSet<int> { start };
                while (grown < target && q.Count > 0)
                {
                    int c = q[rng.Range(0, q.Count)];
                    int cx = c % w, cz = c / w;
                    if (m.cells[c] == (byte)CAMorphCell.Empty && !blob[c])
                    { m.cells[c] = (byte)CAMorphCell.Field; grown++; }
                    foreach (var (nx, nz) in Neigh(cx, cz))
                    {
                        if (nx < 2 || nz < 2 || nx >= w - 2
                            || nz >= h - 2) continue;
                        int n = nx + nz * w;
                        if (mine.Contains(n) || blob[n]) continue;
                        if (m.cells[n] != (byte)CAMorphCell.Empty)
                            continue;
                        mine.Add(n); q.Add(n);
                    }
                    q.Remove(c);
                }
                if (form == CAMorphForm.FrontierHomestead)
                    foreach (int c in mine)
                    {
                        if (m.cells[c] != (byte)CAMorphCell.Field)
                            continue;
                        int cx = c % w, cz = c / w;
                        bool edge = false;
                        foreach (var (nx, nz) in Neigh(cx, cz))
                            if (nx >= 0 && nz >= 0 && nx < w && nz < h
                                && m.cells[nx + nz * w]
                                    == (byte)CAMorphCell.Empty
                                && !blob[nx + nz * w])
                            { edge = true; break; }
                        if (edge)
                            m.cells[c] = (byte)CAMorphCell.Fence;
                    }
            }
        }
    }
}
