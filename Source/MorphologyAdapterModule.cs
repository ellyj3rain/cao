using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Materialize the deterministic settlement layout as walls, doors, floors,
    // streets, fields, fences, roofs, and shore facilities. Existing buildings
    // and pawns are preserved; blocked cells are skipped.
    // WHAT THE REPRESENTED SETTLEMENT PHYSICALLY REQUIRES.
    //
    // Derived per cause from the record -- no scalar stands between the
    // simulation and the ground:
    //   program entries (count, extent)  -> body-cell floor: the footprint
    //                                       its own functions need
    //   landCapacity                     -> body-cell ceiling: the ground it
    //                                       actually holds
    //   quarter-bearing programs         -> districts: industry from
    //                                       production/specialized industry,
    //                                       a port quarter from
    //                                       trade/transport WITH waterfront;
    //                                       never a scalar threshold
    //   defense program / security role  -> wall, afforded by economy (full
    //                                       when it can be held, partial
    //                                       when it cannot); absent cause,
    //                                       the form's own idiom stands
    //   historicalDevelopment            -> accretion: accumulated fabric
    //                                       reads irregular, planned reads
    //                                       regular
    //   trade program                    -> market ground beside the core
    //   geography                        -> which edge the port quarter
    //                                       grows against
    //   economicCapacity                 -> infrastructure tier (paving
    //                                       verge width)
    public sealed class CASettlementPhysicalRequirements
    {
        public int BodyCells = -1;
        public int Quarters = 1;
        public int WallKind = -1;
        public float Accretion = 0.35f;
        public int PlazaCells;
        public int PortSide = -1;
        public int InfrastructureTier = 1;
        public string Provenance = "no represented requirements";

        public static CASettlementPhysicalRequirements For(
            CARegionalSettlementRecord record, Map map, CellRect rect)
        {
            var need = new CASettlementPhysicalRequirements();
            if (record?.settlementProgram?.entries == null) return need;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            int programCells = 0;
            foreach (CASettlementProgramEntry entry in
                record.settlementProgram.entries)
            {
                if (entry == null || entry.programKey.NullOrEmpty())
                    continue;
                keys.Add(entry.programKey);
                // each program needs standing room: its own extent, its
                // count of instances, and the circulation around them
                programCells += Math.Max(1, entry.count)
                    * Math.Max(9, entry.extent * 9);
            }
            if (programCells <= 0) return need;

            // RESIDENTIAL GROUND IS ITS OWN CAUSE. The fixture proved the
            // entries' count/extent are unit counts (one kitchen, one
            // infirmary), not population-scaled footprints: summing them
            // alone starved a ten-resident settlement to 75 cells and two
            // lots. Dwellings for the people who live here enter as the
            // resident population's own term, beside -- never inside -- the
            // functional footprints.
            int residents = record.residentPopulation >= 18
                    && record.residentPopulation <= 1200
                ? record.residentPopulation
                : record.populationBaseline > 0
                    && record.populationBaseline <= 1200
                ? record.populationBaseline : 10;
            // Empirically ~95 blob cells stand one building with its
            // circulation, so ground is sized by the LOTS the settlement
            // needs: one dwelling per household of ~3, plus one lot per
            // program entry, plus the shared hall the provision receipts
            // already stand.
            int households = (residents + 2) / 3;
            int residentialCells = households * 95;

            // floor from function and habitation, ceiling from land held
            int landBand = record.landCapacity < 0 ? 1
                : Math.Min(4, record.landCapacity);
            int ceiling = (int)(rect.Area * (0.30f + 0.13f * landBand));
            int programLots = record.settlementProgram.entries
                .Count(e => e != null && !e.programKey.NullOrEmpty());
            need.BodyCells = Math.Min(
                residentialCells + (programLots + 1) * 95
                    + programCells * 2, ceiling);

            bool industry = keys.Contains(
                    CASettlementProgramCausalKernel.Production)
                || keys.Contains(CASettlementProgramCausalKernel
                    .SpecializedIndustry);
            bool trading = keys.Contains(
                    CASettlementProgramCausalKernel.Trade)
                || keys.Contains(
                    CASettlementProgramCausalKernel.Transport);
            need.PortSide = trading ? WaterSide(map, rect) : -1;
            need.Quarters = 1 + (industry ? 1 : 0)
                + (need.PortSide >= 0 ? 1 : 0);

            int economy = record.economicCapacity < 0 ? 1
                : Math.Min(4, record.economicCapacity);
            bool defense = keys.Contains(
                    CASettlementProgramCausalKernel.Defense)
                || (record.operationalRoleMask & 1) != 0;
            need.WallKind = defense ? (economy >= 2 ? 2 : 1) : -1;

            int history = record.historicalDevelopment < 0 ? 1
                : Math.Min(4, record.historicalDevelopment);
            need.Accretion = history / 4f;

            need.PlazaCells = keys.Contains(
                    CASettlementProgramCausalKernel.Trade)
                ? Math.Max(12, 6 * Math.Max(1,
                    record.settlementProgram.entries
                        .Where(e => e != null && e.programKey
                            == CASettlementProgramCausalKernel.Trade)
                        .Sum(e => Math.Max(1, e.extent))))
                : 0;
            need.InfrastructureTier = economy >= 3 ? 2
                : economy >= 1 ? 1 : 0;

            need.Provenance = "households " + households
                + " x95 + program lots " + (programLots + 1)
                + " x95 + extents " + programCells
                + " x2 vs land ceiling " + ceiling
                + " -> body " + need.BodyCells
                + "; quarters " + need.Quarters
                + (industry ? " (+industry)" : "")
                + (need.PortSide >= 0 ? " (+port@side" + need.PortSide + ")"
                    : "")
                + "; wall " + (need.WallKind == 2 ? "full (defense, afforded)"
                    : need.WallKind == 1 ? "partial (defense, unafforded)"
                    : "form idiom")
                + "; accretion " + need.Accretion.ToString("F2")
                + " (history " + history + ")"
                + "; plaza " + need.PlazaCells
                + "; infrastructure " + need.InfrastructureTier
                + " (economy " + economy + ")";
            return need;
        }

        // The edge of the rect beyond which the water actually lies; -1 when
        // no edge is watered. Geography decides, never a toggle.
        private static int WaterSide(Map map, CellRect rect)
        {
            if (map == null) return -1;
            var probes = new[]
            {
                new IntVec3(rect.CenterCell.x, 0, rect.maxZ + 8),
                new IntVec3(rect.maxX + 8, 0, rect.CenterCell.z),
                new IntVec3(rect.CenterCell.x, 0, rect.minZ - 8),
                new IntVec3(rect.minX - 8, 0, rect.CenterCell.z),
            };
            int best = -1, bestWater = 0;
            for (int side = 0; side < 4; side++)
            {
                int water = 0;
                for (int spread = -6; spread <= 6; spread += 3)
                {
                    IntVec3 c = probes[side];
                    if (side == 0 || side == 2) c.x += spread;
                    else c.z += spread;
                    try
                    {
                        if (c.InBounds(map)
                            && c.GetTerrain(map)?.IsWater == true) water++;
                    }
                    catch { }
                }
                if (water > bestWater) { bestWater = water; best = side; }
            }
            return bestWater >= 2 ? best : -1;
        }
    }

    public static class CAMorphologyAdapter
    {
        public static bool Materialize(Map map, CellRect rect,
            CAMorphForm form, int seed, Faction faction,
            int districts = 1, float densityScale = 1f,
            CARegionalSettlementRecord record = null)
        {
            var caSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (map == null || rect == CellRect.Empty) return false;
                rect = rect.ClipInsideMap(map);
                if (rect.Width < 12 || rect.Height < 12) return false;

                // plan grid sized to the rect, capped and centered
                int w = Math.Min(rect.Width, 120);
                int h = Math.Min(rect.Height, 120);
                int offX = rect.minX + (rect.Width - w) / 2;
                int offZ = rect.minZ + (rect.Height - h) / 2;

                bool[] passable = PassableMask(map, offX, offZ, w, h);
                CASettlementPhysicalRequirements need =
                    CASettlementPhysicalRequirements.For(record, map, rect);
                int quarters = Math.Max(districts < 1 ? 1 : districts,
                    need.Quarters);
                // A CA settlement GROWS from its represented state --
                // households and program facilities placed one at a time,
                // each ranked by the corpus's learned relationships against
                // everything already standing, trips wearing the streets
                // the next placements front. The one-shot morphology
                // remains for outposts, forms without a record, and as the
                // CA_SETTLEMENT_GROWER=0 fallback.
                CAMorphResult plan = record != null
                        && !CASettlementGrowth.Disabled
                    ? CASettlementGrowth.Generate(record, seed, w, h,
                        passable, need, map)
                    : CASettlementMorphology.Generate(
                        form, seed, w, h, passable, quarters, -1,
                        densityScale, need.BodyCells, need.Accretion,
                        need.WallKind, need.PlazaCells, need.PortSide);
                if (record != null)
                    Log.Message("[CA][Settlement][Requirements] "
                        + (record.name ?? "?") + ": " + need.Provenance);
                // no lots grown = no claim: the caller's
                // fallback takes over; never an empty stamp.
                if (plan.lots.Count == 0) return false;
                if (plan?.cells == null) return false;

                Palette palette = PaletteFor(map, faction, record);
                // the settlement body, reconstructed from the plan:
                // structural cells grown by two. The ground-truth
                // pass, the street gradient and the pier all read
                // this blob.
                bool[] body = BodyMask(plan);
                var receipt = new Receipt();
                int built = SpawnPlan(map, plan, palette, faction,
                    offX, offZ, body, receipt);
                int roofed = RoofLots(map, plan, offX, offZ);
                // [security variants] tents, watch rings and lights for
                // the non-fort outpost faces - real assets, spawned by
                // the ported machinery, no-op for every other form.
                CASecurityStructures.MaterializeVariantExtras(map, plan,
                    rect, faction,
                    Math.Max(0, Math.Min(3,
                        CATechnologicalKnowledgeRuntime.CanonicalRank(faction,
                            CATechnologyDomains.Weapons,
                            CATechnologyCompetencies.Construct) - 1)));
                GroundCulture(map, plan, body, palette, seed,
                    offX, offZ, receipt, need.InfrastructureTier);
                // Piers are DERIVED, not toggled. BuildPier already has to
                // find navigable water adjacent to the settlement before it
                // can place anything, so real waterfront geography is the
                // condition; a global on/off in front of it only ever
                // suppressed docks on settlements that genuinely had a
                // shore.
                BuildPier(map, plan, body, palette, faction,
                    offX, offZ, receipt);
                // Harbor works are the TRADE consequence of a pier: a
                // settlement whose program carries trade or transport earns
                // storage at the shore end of the deck, because goods land
                // there. A pier alone is just fishing.
                if (receipt.pierCells > 0
                    && record?.settlementProgram?.entries?.Any(entry =>
                        entry != null && (entry.programKey
                                == CASettlementProgramCausalKernel.Trade
                            || entry.programKey
                                == CASettlementProgramCausalKernel
                                    .Transport)) == true)
                    BuildHarborWorks(map, palette, faction, receipt);
                Log.Message("[CA] morphology " + form + " ["
                    + caSw.ElapsedMilliseconds + " ms] grown over "
                    + rect + " for " + (faction?.Name ?? "no faction")
                    + ": " + built + " cells materialized, "
                    + plan.lots.Count + " lots, " + roofed
                    + " cells roofed, " + quarters
                    + " district(s), seed " + seed
                    + "; ground: " + receipt.pavedCells + " paved, "
                    + receipt.purgedPlants + " wild plants purged, "
                    + receipt.gardens + " garden(s), "
                    + receipt.gradientCells + " gradient street cells, "
                    + "desire streets " + plan.desireStreetCells
                    + " cells from " + plan.desireWalkedLots + " lots; "
                    + (receipt.pierCells > 0
                        ? "pier built (" + receipt.pierCells + " cells)"
                        : "no pier")
                    + (receipt.harborCells > 0
                        ? "; harbor works (" + receipt.harborCells
                            + " placed)"
                        : ""));
                return built > 0;
            }
            catch (Exception e)
            {
                Log.Warning("[CA] morphology adapter failed over " + rect
                    + ": " + e.Message);
                return false;
            }
        }

        // Form follows organization: the same tier boundaries the
        // settlement program materializer uses the same technology tier.
        // The form a FACTION would build by default. Settlements must
        // use the overload below - a settlement's form is its own axis
        // and may have been authored away from what its technology
        // implies (DR-84).
        internal static CAMorphForm FormFor(Faction faction)
        {
            return CASettlementAxes.Form(CASettlementAxes.Derive,
                CATechnologicalKnowledgeRuntime.CanonicalBuildTechLevel(
                    faction));
        }

        // A SETTLEMENT'S form, as resolved when it was created.
        internal static CAMorphForm FormFor(
            CARegionalSettlementRecord record)
        {
            if (record == null) return CAMorphForm.Tribal;
            if (record.settlementForm >= 0)
                return CASettlementAxes.Form(record.settlementForm,
                    TechLevelFor(record));
            return CASettlementAxes.Form(CASettlementAxes.Derive,
                TechLevelFor(record));
        }

        private static TechLevel TechLevelFor(
            CARegionalSettlementRecord record)
        {
            if (record?.faction != null)
                return CATechnologicalKnowledgeRuntime
                    .CanonicalBuildTechLevel(record.faction);
            return CATechnologicalKnowledgeModel.CompatibilityTechLevel(
                record?.localSociety?.technologicalKnowledge);
        }

        // ---- passable mask: where the settlement may grow ----
        // Impassable cells, water, roofed cells (native rooms and
        // overhead mountain) and cells holding any existing building
        // are closed to the plan; the morphology grows around them.
        private static bool[] PassableMask(Map map, int offX, int offZ,
            int w, int h)
        {
            var passable = new bool[w * h];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    var c = new IntVec3(offX + x, 0, offZ + z);
                    bool open = false;
                    try
                    {
                        TerrainDef terrain;
                        open = c.InBounds(map) && c.Walkable(map)
                            && !c.Roofed(map)
                            && (terrain = c.GetTerrain(map)) != null
                            && !terrain.IsWater
                            && !HoldsBuilding(c, map);
                    }
                    catch { }
                    passable[x + z * w] = open;
                }
            return passable;
        }

        // ---- spawn the plan into the map, cell by guarded cell ----
        private static int SpawnPlan(Map map, CAMorphResult plan,
            Palette palette, Faction faction, int offX, int offZ,
            bool[] body, Receipt receipt)
        {
            int built = 0;
            for (int z = 0; z < plan.h; z++)
                for (int x = 0; x < plan.w; x++)
                {
                    byte cell = plan.cells[x + z * plan.w];
                    if (cell == (byte)CAMorphCell.Empty) continue;
                    var c = new IntVec3(offX + x, 0, offZ + z);
                    try
                    {
                        if (!c.InBounds(map)) continue;
                        TerrainDef terrain = c.GetTerrain(map);
                        if (terrain == null || terrain.IsWater) continue;
                        // an existing building owns its cell - skip
                        if (HoldsBuilding(c, map)) continue;
                        switch ((CAMorphCell)cell)
                        {
                            case CAMorphCell.Wall:
                                built += SpawnEdifice(map, c,
                                    palette.wall, palette.wallStuff,
                                    faction, palette.culture);
                                break;
                            case CAMorphCell.Door:
                                built += SpawnEdifice(map, c,
                                    palette.door, palette.doorStuff,
                                    faction, palette.culture);
                                break;
                            case CAMorphCell.Fence:
                                built += SpawnEdifice(map, c,
                                    palette.fence, palette.fenceStuff,
                                    faction, palette.culture);
                                break;
                            case CAMorphCell.Floor:
                                if (palette.floor == null) continue;
                                if (!CATechnologicalKnowledgeRuntime
                                        .CanConstructCanonical(faction,
                                            palette.floor, out _)) continue;
                                ClearCover(map, c);
                                map.terrainGrid.SetTerrain(c,
                                    palette.floor);
                                built++;
                                break;
                            case CAMorphCell.Street:
                            {
                                // cultural gradient: the stretch
                                // where a street meets the body rim
                                // steps down one tier - paved to
                                // flagstone to packed dirt - so
                                // concrete never walks out into
                                // the wild on its own.
                                TerrainDef laid = palette.street;
                                if (palette.streetOutside != null
                                    && palette.streetOutside
                                        != palette.street
                                    && NearMaskEdge(body, plan.w,
                                        plan.h, x, z, 4))
                                    laid = palette.streetOutside;
                                if (laid == null) continue;
                                if (!CATechnologicalKnowledgeRuntime
                                        .CanConstructCanonical(faction, laid,
                                            out _)) continue;
                                ClearCover(map, c);
                                map.terrainGrid.SetTerrain(c, laid);
                                if (laid != palette.street)
                                    receipt.gradientCells++;
                                built++;
                                break;
                            }
                            case CAMorphCell.Field:
                                built += SowField(map, c, palette.crop,
                                    faction);
                                break;
                        }
                    }
                    catch { }
                }
            return built;
        }

        private static int SpawnEdifice(Map map, IntVec3 c, ThingDef def,
            ThingDef stuff, Faction faction, CACulture culture = null)
        {
            if (def == null) return 0;
            if (!CATechnologicalKnowledgeRuntime.CanConstructCanonical(
                    faction, def, out _)) return 0;
            List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing present = things[i];
                if (present == null) continue;
                // never build over a pawn, never wipe an item that is
                // not cheap natural cover (loot stays where it lies)
                if (present is Pawn) return 0;
                if (present.def.category == ThingCategory.Item
                    && !IsCheapCover(present)) return 0;
            }
            ClearCover(map, c);
            ThingDef madeOf = def.MadeFromStuff
                ? (stuff ?? GenStuff.DefaultStuffFor(def)) : null;
            Thing thing = ThingMaker.MakeThing(def, madeOf);
            CAConstructionMaterials.ApplyStyle(thing, culture);
            GenSpawn.Spawn(thing, c, map);
            if (faction != null) thing.SetFaction(faction);
            return 1;
        }

        private static int SowField(Map map, IntVec3 c, ThingDef crop,
            Faction faction)
        {
            if (crop == null) return 0;
            if (!CATechnologicalKnowledgeRuntime.CanGrowCanonical(
                    faction, crop, out _)) return 0;
            ClearCover(map, c);
            if (!crop.CanEverPlantAt(c, map)) return 0;
            var plant = ThingMaker.MakeThing(crop) as Plant;
            if (plant == null) return 0;
            // deterministic stagger - a worked field, not a stamp
            plant.Growth = 0.25f
                + 0.5f * (((c.x * 61 + c.z * 89) % 97) / 96f);
            plant.sown = true;
            GenSpawn.Spawn(plant, c, map);
            return 1;
        }

        // ---- roofs: constructed roof over each grown lot ----
        private static int RoofLots(Map map, CAMorphResult plan,
            int offX, int offZ)
        {
            int roofed = 0;
            if (plan.lots == null || RoofDefOf.RoofConstructed == null)
                return 0;
            foreach (CAMorphLot lot in plan.lots)
            {
                if (lot?.cells == null) continue;
                for (int i = 0; i < lot.cells.Count; i++)
                {
                    int idx = lot.cells[i];
                    var c = new IntVec3(offX + idx % plan.w, 0,
                        offZ + idx / plan.w);
                    try
                    {
                        if (!c.InBounds(map) || c.Roofed(map)) continue;
                        // only where the raised walls (or anything
                        // standing) can actually hold it up
                        if (!RoofCollapseUtility.WithinRangeOfRoofHolder(
                            c, map)) continue;
                        map.roofGrid.SetRoof(c,
                            RoofDefOf.RoofConstructed);
                        roofed++;
                    }
                    catch { }
                }
            }
            return roofed;
        }

        // ---- the body, reconstructed from the plan ----
        // Union of the structural cells (floor, wall, door, street)
        // grown by two - the blob the engine grew, read back from
        // its printed plan. Fields and fences stay outside: worked
        // land is not town ground.
        private static bool[] BodyMask(CAMorphResult plan)
        {
            int w = plan.w, h = plan.h;
            var body = new bool[w * h];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    byte cell = plan.cells[x + z * w];
                    if (cell != (byte)CAMorphCell.Floor
                        && cell != (byte)CAMorphCell.Wall
                        && cell != (byte)CAMorphCell.Door
                        && cell != (byte)CAMorphCell.Street)
                        continue;
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx, nz = z + dz;
                            if (nx < 0 || nz < 0 || nx >= w
                                || nz >= h) continue;
                            body[nx + nz * w] = true;
                        }
                }
            return body;
        }

        // True near the mask's edge: some cell within the given
        // ring is outside the body (or off the plan entirely).
        private static bool NearMaskEdge(bool[] body, int w, int h,
            int x, int z, int r)
        {
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h)
                        return true;
                    if (!body[nx + nz * w]) return true;
                }
            return false;
        }

        // A body cell with a non-body cardinal neighbor: the rim.
        private static bool OnMaskRim(bool[] body, int w, int h,
            int x, int z)
        {
            if (x <= 0 || z <= 0 || x >= w - 1 || z >= h - 1)
                return true;
            return !body[(x - 1) + z * w] || !body[(x + 1) + z * w]
                || !body[x + (z - 1) * w] || !body[x + (z + 1) * w];
        }

        // ---- ground truth: the body's open ground by culture ----
        // Tribal keeps bare earth - no artificial terrain outside
        // its structures. Medieval flagstones the open ground and
        // deliberately spares one or two green patches: managed
        // gardens, not oversights. Industrial pours its pad solid
        // - concrete with worn paved patches - and purges the wild
        // growth that would poke through it.
        // Distance, over body cells, from anything that carries traffic: the
        // streets and the doors buildings open onto. Bounded, because only the
        // first few cells matter -- beyond a verge the ground is not walked on
        // and has no reason to be floored.
        private static int[] UseDistance(CAMorphResult plan, bool[] body)
        {
            int w = plan.w, h = plan.h;
            var dist = new int[w * h];
            for (int i = 0; i < dist.Length; i++) dist[i] = 9999;
            var queue = new System.Collections.Generic.Queue<int>();
            for (int i = 0; i < plan.cells.Length; i++)
            {
                byte c = plan.cells[i];
                if (c != (byte)CAMorphCell.Street
                    && c != (byte)CAMorphCell.Door) continue;
                dist[i] = 0;
                queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (dist[at] >= 4) continue;
                int ax = at % w, az = at / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = ax + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int nz = az + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int n = nx + nz * w;
                    if (!body[n] || dist[n] <= dist[at] + 1) continue;
                    dist[n] = dist[at] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        private static void GroundCulture(Map map, CAMorphResult plan,
            bool[] body, Palette palette, int seed, int offX, int offZ,
            Receipt receipt, int infrastructureTier = -1)
        {
            try
            {
                if (palette.tier <= 0) return;
                bool[] garden = palette.tier == 1
                    ? MarkGardens(plan, body, seed, receipt) : null;
                // GROUND FOLLOWS USE. This paved EVERY empty cell inside the
                // grown body, so all the interstitial space between buildings
                // was floored and the settlement filled into a solid slab --
                // 750-950 paved cells against 66 street cells on a 65x65 rect.
                // The edge of that paved region is the hard border that made
                // every settlement read as a square no matter how its
                // buildings were arranged. Nothing drew that border on
                // purpose; it is where floor stopped meeting grass.
                //
                // Paving now traces where people actually move: a verge along
                // the streets and a threshold at the doors. Ground beyond that
                // stays what the terrain already was, so the settlement ends
                // where its fabric ends instead of at the rim of a slab.
                int[] use = UseDistance(plan, body);
                int verge = infrastructureTier >= 0
                    ? Math.Max(1, infrastructureTier)
                    : (palette.tier >= 2 ? 2 : 1);
                for (int z = 0; z < plan.h; z++)
                    for (int x = 0; x < plan.w; x++)
                    {
                        int idx = x + z * plan.w;
                        if (!body[idx] || plan.cells[idx]
                            != (byte)CAMorphCell.Empty) continue;
                        if (garden != null && garden[idx]) continue;
                        if (use[idx] > verge) continue;
                        var c = new IntVec3(offX + x, 0, offZ + z);
                        try
                        {
                            if (!c.InBounds(map)) continue;
                            TerrainDef terrain = c.GetTerrain(map);
                            if (terrain == null || terrain.IsWater)
                                continue;
                            if (map.terrainGrid.FoundationAt(c)
                                != null) continue;
                            if (HoldsBuilding(c, map)) continue;
                            // a worked field is not open ground -
                            // native sown crops keep their soil
                            if (HoldsSownCrop(c, map)) continue;
                            TerrainDef ground = palette.tier == 1
                                ? palette.floor
                                : (((c.x * 31 + c.z * 17) % 89) < 13
                                    ? palette.street : palette.floor);
                            if (ground == null || terrain == ground)
                                continue;
                            receipt.purgedPlants +=
                                PurgeWildPlants(map, c);
                            map.terrainGrid.SetTerrain(c, ground);
                            receipt.pavedCells++;
                        }
                        catch { }
                    }
            }
            catch { }
        }

        // Medieval gardens: one or two small patches of the body's
        // open interior stay green on purpose. Chosen by hash, not
        // by Rand - the same seed always keeps the same lawns.
        private static bool[] MarkGardens(CAMorphResult plan,
            bool[] body, int seed, Receipt receipt)
        {
            int w = plan.w, h = plan.h;
            var garden = new bool[w * h];
            var candidates = new List<int>();
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    int idx = x + z * w;
                    if (!body[idx] || plan.cells[idx]
                        != (byte)CAMorphCell.Empty) continue;
                    // gardens live inside the town, never out on
                    // the apron along the rim
                    if (NearMaskEdge(body, w, h, x, z, 4)) continue;
                    candidates.Add(idx);
                }
            if (candidates.Count == 0) return garden;
            int want = 1 + (int)(((uint)seed * 2654435761u >> 16) & 1);
            var centers = new List<int>();
            for (int g = 0; g < want; g++)
            {
                int best = -1;
                uint bestHash = uint.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    int idx = candidates[i];
                    bool clear = true;
                    for (int j = 0; j < centers.Count; j++)
                    {
                        int dx = Math.Abs(idx % w - centers[j] % w);
                        int dz = Math.Abs(idx / w - centers[j] / w);
                        if (Math.Max(dx, dz) < 10)
                        { clear = false; break; }
                    }
                    if (!clear) continue;
                    uint hash = (uint)idx * 2654435761u
                        ^ (uint)seed * 2246822519u;
                    if (hash < bestHash)
                    { bestHash = hash; best = idx; }
                }
                if (best < 0) break;
                centers.Add(best);
                int bx = best % w, bz = best / w;
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = bx + dx, nz = bz + dz;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h)
                            continue;
                        int n = nx + nz * w;
                        if (body[n] && plan.cells[n]
                            == (byte)CAMorphCell.Empty)
                            garden[n] = true;
                    }
                receipt.gardens++;
            }
            return garden;
        }

        // Storage where the goods land: shelves on the land cells around
        // the pier's shore anchor, from the settlement's own palette.
        // Derived like the pier itself: no shore anchor, no works.
        private static void BuildHarborWorks(Map map, Palette palette,
            Faction faction, Receipt receipt)
        {
            try
            {
                if (receipt.pierShore == IntVec3.Invalid) return;
                ThingDef shelf = DefDatabase<ThingDef>
                    .GetNamedSilentFail("Shelf");
                if (shelf == null) return;
                IntVec3 landward = receipt.pierShore - receipt.pierDir;
                var perp = new IntVec3(receipt.pierDir.z, 0,
                    receipt.pierDir.x);
                var spots = new[]
                {
                    landward, landward + perp, landward - perp,
                };
                foreach (IntVec3 c in spots)
                {
                    try
                    {
                        if (!c.InBounds(map) || HoldsBuilding(c, map))
                            continue;
                        TerrainDef here = c.GetTerrain(map);
                        if (here == null || here.IsWater) continue;
                        receipt.harborCells += SpawnEdifice(map, c, shelf,
                            palette.wallStuff, faction, palette.culture);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ---- docks: a pier where the rim meets open water ----
        // A real waterfront (a run of shore cells on the body rim)
        // earns one pier: a two-wide bridge deck run 4-8 cells over
        // bridgeable water, a fire or a torch at its head. Deep
        // water refuses the bridge affordance and the pier simply
        // stays home. Bridge terrain is a foundation def, so
        // SetTerrain routes it onto the foundation grid and the
        // engine keeps the water beneath the deck.
        private static void BuildPier(Map map, CAMorphResult plan,
            bool[] body, Palette palette, Faction faction,
            int offX, int offZ, Receipt receipt)
        {
            try
            {
                if (palette.pier == null) return;
                if (!CATechnologicalKnowledgeRuntime.CanConstructCanonical(
                        faction, palette.pier, out _)) return;
                TerrainAffordanceDef needed =
                    palette.pier.terrainAffordanceNeeded;
                var dirs = new[] { IntVec3.North, IntVec3.East,
                    IntVec3.South, IntVec3.West };
                int waterfront = 0, bestRun = 0;
                IntVec3 site = IntVec3.Invalid;
                IntVec3 dir = IntVec3.Invalid;
                for (int z = 0; z < plan.h; z++)
                    for (int x = 0; x < plan.w; x++)
                    {
                        if (!body[x + z * plan.w]
                            || !OnMaskRim(body, plan.w, plan.h, x, z))
                            continue;
                        var c = new IntVec3(offX + x, 0, offZ + z);
                        try
                        {
                            if (!c.InBounds(map)) continue;
                            TerrainDef here = c.GetTerrain(map);
                            if (here == null || here.IsWater)
                                continue;
                            bool shore = false;
                            for (int d = 0; d < dirs.Length; d++)
                            {
                                int run = WaterRun(map, c, dirs[d],
                                    needed);
                                if (run > 0) shore = true;
                                if (run > bestRun)
                                {
                                    bestRun = run;
                                    site = c;
                                    dir = dirs[d];
                                }
                            }
                            if (shore) waterfront++;
                        }
                        catch { }
                    }
                if (waterfront < 4 || bestRun < 4 || !site.IsValid)
                    return;
                // the second row hangs off whichever side carries
                // water as far as the first
                var perp = new IntVec3(dir.z, 0, dir.x);
                int off = WaterRun(map, site + perp, dir, needed);
                int other = WaterRun(map, site - perp, dir, needed);
                if (other > off)
                {
                    perp = new IntVec3(-perp.x, 0, -perp.z);
                    off = other;
                }
                int len = Math.Min(Math.Min(bestRun, off), 8);
                if (len < 4) return;
                receipt.pierShore = site;
                receipt.pierDir = dir;
                // deterministic length inside 4..len, by site hash
                len = 4 + (int)((uint)(site.x * 73 + site.z * 131)
                    % (uint)(len - 3));
                for (int k = 1; k <= len; k++)
                {
                    var deck = site + dir * k;
                    try
                    {
                        map.terrainGrid.SetTerrain(deck,
                            palette.pier);
                        receipt.pierCells++;
                    }
                    catch { }
                    try
                    {
                        var side = deck + perp;
                        map.terrainGrid.SetTerrain(side,
                            palette.pier);
                        receipt.pierCells++;
                    }
                    catch { }
                }
                if (receipt.pierCells == 0) return;
                // the head light: at the deck's far end, falling
                // back to the shore cell if the end refuses it
                var head = site + dir * len;
                try
                {
                    if (HoldsBuilding(head, map)
                        || SpawnEdifice(map, head, palette.pierHead,
                            null, faction, palette.culture) == 0)
                    {
                        if (!HoldsBuilding(site, map))
                            SpawnEdifice(map, site,
                                palette.pierHead, null, faction,
                                palette.culture);
                    }
                }
                catch { }
                // [naval] a dock with nothing tied to it is scenery:
                // the settlement keeps a hull suited to what it is.
                try
                {
                    CANavalBerth.TryBerth(map, faction, head, dir,
                        palette.tier, palette.tier + 1);
                }
                catch { }
            }
            catch { }
        }

        // Consecutive bridgeable open water outward from a cell,
        // capped at the longest deck this pass will ever lay.
        private static int WaterRun(Map map, IntVec3 from, IntVec3 dir,
            TerrainAffordanceDef needed)
        {
            int run = 0;
            for (int k = 1; k <= 8; k++)
            {
                var c = from + dir * k;
                try
                {
                    if (!c.InBounds(map)) break;
                    TerrainDef t = c.GetTerrain(map);
                    if (t == null || !t.IsWater) break;
                    if (needed != null && (t.affordances == null
                        || !t.affordances.Contains(needed))) break;
                    if (map.terrainGrid.UnderTerrainAt(c) != null
                        || map.terrainGrid.FoundationAt(c) != null)
                        break;
                    if (HoldsBuilding(c, map)) break;
                    run++;
                }
                catch { break; }
            }
            return run;
        }

        // ---- shared cell predicates ----
        private static bool HoldsBuilding(IntVec3 c, Map map)
        {
            List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i]?.def?.category == ThingCategory.Building)
                    return true;
            return false;
        }

        private static void ClearCover(Map map, IntVec3 c)
        {
            List<Thing> things = c.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = i < things.Count ? things[i] : null;
                if (thing == null || thing.Destroyed
                    || !IsCheapCover(thing)) continue;
                try { thing.Destroy(DestroyMode.Vanish); } catch { }
            }
        }

        private static bool IsCheapCover(Thing thing)
        {
            ThingCategory category = thing.def.category;
            if (category == ThingCategory.Plant
                || category == ThingCategory.Filth) return true;
            return category == ThingCategory.Item
                && ThingCategoryDefOf.Chunks != null
                && thing.def.IsWithinCategory(ThingCategoryDefOf.Chunks);
        }

        private static bool HoldsSownCrop(IntVec3 c, Map map)
        {
            List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Plant plant && plant.sown)
                    return true;
            return false;
        }

        // Wild growth only: a sown crop is somebody's work and
        // stays. Purged before SetTerrain so the count is ours,
        // not the engine's fertility sweep.
        private static int PurgeWildPlants(Map map, IntVec3 c)
        {
            int purged = 0;
            List<Thing> things = c.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = i < things.Count ? things[i] : null;
                if (thing == null || thing.Destroyed
                    || thing.def.category != ThingCategory.Plant)
                    continue;
                if (thing is Plant plant && plant.sown) continue;
                try { thing.Destroy(DestroyMode.Vanish); purged++; }
                catch { }
            }
            return purged;
        }

        // Receipt for the ground-truth passes - the numbers the
        // materialize log line carries out to the caller's records.
        private sealed class Receipt
        {
            internal int pavedCells;
            internal int harborCells;
            internal IntVec3 pierShore = IntVec3.Invalid;
            internal IntVec3 pierDir = IntVec3.Invalid;
            internal int purgedPlants;
            internal int gardens;
            internal int gradientCells;
            internal int pierCells;
        }

        // ---- material palette by technology tier ----
        private sealed class Palette
        {
            internal int tier;
            internal ThingDef wall;
            internal ThingDef door;
            internal ThingDef fence;
            internal ThingDef wallStuff;
            internal ThingDef doorStuff;
            internal ThingDef fenceStuff;
            internal TerrainDef floor;
            internal TerrainDef street;
            // street material past the body rim - one tier down,
            // so town paving never invades the wild
            internal TerrainDef streetOutside;
            internal TerrainDef pier;
            internal ThingDef pierHead;
            internal ThingDef crop;
            // Whose walls these are. Travels with the material so the
            // shell of a settlement is styled like the assets inside it.
            internal CACulture culture;
        }

        private static Palette PaletteFor(Map map, Faction faction,
            CARegionalSettlementRecord record = null)
        {
            int tier = CASettlementProgramMaterializer
                .CanonicalTechTier(faction);
            // material coherence: every stone this adapter lays is
            // native to the tile. Walk the tile's own rock kinds
            // for one with cut blocks and a flagstone before any
            // foreign fallback - granite only when no local rock
            // ships a worked form at all.
            ThingDef blocks = null;
            TerrainDef flagstone = null;
            try
            {
                foreach (ThingDef r in
                    Find.World.NaturalRockTypesIn(map.Tile))
                {
                    if (r == null) continue;
                    if (blocks == null)
                        blocks = DefDatabase<ThingDef>
                            .GetNamedSilentFail("Blocks" + r.defName);
                    if (flagstone == null)
                        flagstone = DefDatabase<TerrainDef>
                            .GetNamedSilentFail(
                                "Flagstone" + r.defName);
                    if (blocks != null && flagstone != null) break;
                }
            }
            catch { }
            if (blocks == null)
                blocks = DefDatabase<ThingDef>.GetNamedSilentFail(
                    "BlocksGranite");
            if (flagstone == null)
                flagstone = DefDatabase<TerrainDef>.GetNamedSilentFail(
                    "FlagstoneGranite");
            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail(
                "WoodLog");
            ThingDef steel = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Steel");

            var palette = new Palette
            {
                tier = tier,
                wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall"),
                door = DefDatabase<ThingDef>.GetNamedSilentFail("Door"),
                fence = DefDatabase<ThingDef>.GetNamedSilentFail("Fence"),
                fenceStuff = wood,
                crop = DefDatabase<ThingDef>.GetNamedSilentFail(
                    "Plant_Potato"),
                // the pier deck is bridge terrain. HeavyBridge is
                // absent from this install's Data (verified), so
                // the silent-fail lands on Bridge for every tier
                // until some def ships a heavier deck.
                pier = tier >= 2
                    ? (DefDatabase<TerrainDef>.GetNamedSilentFail(
                            "HeavyBridge")
                        ?? DefDatabase<TerrainDef>.GetNamedSilentFail(
                            "Bridge"))
                    : DefDatabase<TerrainDef>.GetNamedSilentFail(
                        "Bridge"),
                pierHead = tier == 0
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(
                        "Campfire")
                    : DefDatabase<ThingDef>.GetNamedSilentFail(
                        "TorchLamp")
            };
            if (tier == 0)
            {
                palette.wallStuff = wood;
                palette.doorStuff = wood;
                palette.floor = DefDatabase<TerrainDef>
                    .GetNamedSilentFail("WoodPlankFloor");
                palette.street = DefDatabase<TerrainDef>
                    .GetNamedSilentFail("PackedDirt");
                // dirt is already the bottom of the gradient
                palette.streetOutside = null;
            }
            else if (tier == 1)
            {
                palette.wallStuff = blocks ?? wood;
                palette.doorStuff = blocks ?? wood;
                palette.floor = flagstone;
                palette.street = flagstone;
                palette.streetOutside = DefDatabase<TerrainDef>
                    .GetNamedSilentFail("PackedDirt");
            }
            else
            {
                palette.wallStuff = steel ?? blocks ?? wood;
                palette.doorStuff = steel ?? blocks ?? wood;
                palette.floor = DefDatabase<TerrainDef>
                    .GetNamedSilentFail("Concrete");
                palette.street = DefDatabase<TerrainDef>
                    .GetNamedSilentFail("PavedTile");
                palette.streetOutside = flagstone;
            }
            // The tier bands above are this module's own historical
            // answer. Where the settlement has a record, the shared
            // construction authority decides instead, reading its
            // knowledge, ground, people and trade rather than a tech
            // tier alone - so one settlement is one material across
            // morphology, fit-out and frontier rather than three.
            if (record != null)
            {
                var materials = new CAConstructionContext(map,
                    CASiteState.Knowledge(record),
                    record.realizedScale, record.residentPopulation,
                    record.economicCapacity, record.tradeConnectivity,
                    record.regionalId + ":" + record.slot,
                    culture: CASiteState.Culture(record));
                palette.culture = materials.Culture;
                string wallBasis = null;
                ThingDef resolvedWall = palette.wall == null ? null
                    : CAConstructionMaterials.ChooseFor(palette.wall,
                        materials, out wallBasis);
                if (resolvedWall != null)
                {
                    palette.wallStuff = resolvedWall;
                    Log.Message("[CA][Settlement][Material] "
                        + (record.name ?? record.regionalId)
                        + " builds in " + wallBasis);
                }
                ThingDef resolvedDoor = palette.door == null ? null
                    : CAConstructionMaterials.ChooseFor(palette.door,
                        materials, out _);
                if (resolvedDoor != null) palette.doorStuff = resolvedDoor;
                // The ground follows the walls. Floors were still chosen
                // by tech tier while the walls had moved to the resolved
                // material, so a settlement that builds in cut stone
                // could stand on plank floors and read as two different
                // places at once. A stone-building settlement flagstones
                // its ground; a timber one keeps its planks.
                if (resolvedWall != null
                    && resolvedWall.defName.StartsWith("Blocks",
                        StringComparison.Ordinal))
                {
                    TerrainDef localFlag = DefDatabase<TerrainDef>
                        .GetNamedSilentFail("Flagstone"
                            + resolvedWall.defName.Substring(6));
                    if (localFlag != null)
                    {
                        palette.floor = localFlag;
                        if (palette.street != null) palette.street = localFlag;
                    }
                }
            }

            return palette;
        }
    }
}
