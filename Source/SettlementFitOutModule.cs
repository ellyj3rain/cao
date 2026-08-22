using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // PHYSICAL FIT-OUT IS THE PHENOTYPE OF THE SETTLEMENT PROGRAM.
    //
    // The settlement program already decides WHAT a settlement does: its
    // entries are derived from population, role, institutions, knowledge and
    // ground, each with named operator, labor, funding and material sources.
    // An earlier version of this module ignored that and assigned building
    // functions by size rank -- every settlement got a hall, kitchen and
    // granary because its buildings had sizes, which is a parallel ontology
    // and exactly the kind of template variety the project rejects.
    //
    // Now a building function exists ONLY because the corresponding program
    // entry exists:
    //
    //   hall       Gathering | Governance | provision (communal/authority)
    //   kitchen    FoodPreparation | CommunalProvision
    //   granary    Storage, and a second from Agriculture
    //   infirmary  Medicine
    //   prison     Custody
    //   workshop   Production | SpecializedIndustry
    //   study      Research
    //   commons    Recreation
    //
    // and the remainder are dwellings under the Housing program. A settlement
    // with no custody institution has no prison, whatever its size. Equipment
    // tiers resolve from the faction's technological knowledge DOMAINS
    // (manufacturing, medicine, electrical) rather than one blanket tech
    // level, and everything stuffed shares one material palette read from the
    // rock actually under the map.
    //
    // The provision materializer already places hearths and dining tables and
    // the program materializer places contracted assets, so placement here
    // DEDUPES against what is already standing rather than doubling it.
    //
    // Additive and defensive throughout: only fills cells the morphology left
    // empty, resolves every def by name, and never lets a failure take
    // generation down. Deterministic from the settlement's regional id.
    internal static class CASettlementFitOut
    {
        internal static readonly bool Disabled = string.Equals(
            Environment.GetEnvironmentVariable("CA_SETTLEMENT_FITOUT"),
            "0", StringComparison.Ordinal);

        private enum Use
        {
            Dwelling, Hall, Kitchen, Granary, Infirmary, Prison, Workshop,
            Study, Commons
        }

        private sealed class Building
        {
            internal readonly List<IntVec3> Cells = new List<IntVec3>();
            internal Use Function = Use.Dwelling;
        }

        private sealed class Palette
        {
            internal ThingDef Stuff;
            internal string Name = "none";
            // A settlement's furniture is its people's furniture. The
            // palette already travels to every spawn here, so the style
            // travels with the material rather than being asked for
            // separately and forgotten.
            internal CACulture Culture;
        }

        internal static void Furnish(Map map, CellRect rect,
            CARegionalSettlementRecord record, int seed)
        {
            if (Disabled || map == null || record == null) return;
            try
            {
                rect = rect.ClipInsideMap(map);
                if (rect.Width < 8 || rect.Height < 8) return;
                Rand.PushState(seed ^ 0x5F1700);
                try { Run(map, rect, record); }
                finally { Rand.PopState(); }
            }
            catch (Exception e)
            {
                Log.Error("[CA][Settlement][FitOut] " + record.name
                    + " skipped: " + e);
            }
        }

        private static void Run(Map map, CellRect rect,
            CARegionalSettlementRecord record)
        {
            Faction faction = record.faction;
            TechLevel tech = TechFor(record);
            // ONE ANSWER TO WHAT THIS PLACE BUILDS FROM. The fit-out kept
            // its own local-rock palette while the frontier used wood and
            // morphology used the engine default, so one settlement could
            // be three materials depending which system placed the thing.
            // The shared authority reads the same facts for all of them:
            // this settlement's knowledge, its ground, its people, its
            // trade, and how developed it is becoming.
            var materials = new CAConstructionContext(map,
                CASiteState.Knowledge(record),
                record.realizedScale, record.residentPopulation,
                record.economicCapacity, record.tradeConnectivity,
                record.regionalId + ":" + record.slot,
                culture: CASiteState.Culture(record));
            Palette palette = PaletteFor(map, tech, materials);

            var keys = new HashSet<string>(
                record.settlementProgram?.entries?
                    .Where(e => e != null && !e.programKey.NullOrEmpty())
                    .Select(e => e.programKey)
                ?? Enumerable.Empty<string>(), StringComparer.Ordinal);

            List<List<IntVec3>> interiors = CASettlementSitingConstraints
                .ResolveInteriorRooms(map, rect);
            if (interiors.Count == 0)
            {
                Log.Message("[CA][Settlement][FitOut] " + record.name
                    + ": no enclosed interiors; programs consumed none");
                return;
            }
            List<Building> buildings = interiors
                .Select(cells => { var b = new Building();
                    b.Cells.AddRange(cells); return b; })
                .OrderByDescending(b => b.Cells.Count).ToList();

            // ---- functions demanded by the program, largest rooms first ----
            var demanded = new List<Use>();
            bool has(string key) => keys.Contains(key);
            if (has(CASettlementProgramCausalKernel.Gathering)
                || has(CASettlementProgramCausalKernel.Governance)
                || has(CASettlementProgramCausalKernel.CommunalProvision)
                || has(CASettlementProgramCausalKernel.AuthorityProvision))
                demanded.Add(Use.Hall);
            if (has(CASettlementProgramCausalKernel.FoodPreparation)
                || has(CASettlementProgramCausalKernel.CommunalProvision))
                demanded.Add(Use.Kitchen);
            if (has(CASettlementProgramCausalKernel.Medicine))
                demanded.Add(Use.Infirmary);
            if (has(CASettlementProgramCausalKernel.Storage))
                demanded.Add(Use.Granary);
            if (has(CASettlementProgramCausalKernel.Agriculture))
                demanded.Add(Use.Granary);
            if (has(CASettlementProgramCausalKernel.Production)
                || has(CASettlementProgramCausalKernel.SpecializedIndustry))
                demanded.Add(Use.Workshop);
            if (has(CASettlementProgramCausalKernel.Research))
                demanded.Add(Use.Study);
            if (has(CASettlementProgramCausalKernel.Custody))
                demanded.Add(Use.Prison);
            if (has(CASettlementProgramCausalKernel.Recreation))
                demanded.Add(Use.Commons);

            // Households hold their ground: enough buildings stay
            // dwellings for the people who actually live here (one per
            // household of ~3) before civic demand may take another. Run 6
            // proved the alternative -- six civic functions consumed the
            // eight largest buildings and two dwellings housed ten people.
            int residentsEarly = Mathf.Clamp(
                record.residentPopulation >= 18
                    && record.residentPopulation <= 1200
                    ? record.residentPopulation
                : record.populationBaseline > 0
                    && record.populationBaseline <= 1200
                    ? record.populationBaseline : 10, 1, 60);
            int dwellingsReserved = (residentsEarly + 2) / 3;
            // Function assignment: size order is only the default. Where
            // the corpus carries a learned relationship for a function
            // (kitchens sit a walked distance from dining; cold stores sit
            // by the kitchen), the candidate whose position best fits the
            // conditional band takes the function -- evidence ranking
            // among candidates the reservation already admitted, never a
            // validity gate.
            var dropped = new List<Use>();
            var evidenceLines = new List<string>();
            string biomeGroup = CASpatialRelationshipEvidence
                .BiomeGroupFor(map);
            string quartile = CASpatialRelationshipEvidence.QuartileFor(
                buildings.Sum(b => b.Cells.Count));
            int slot = 0;
            foreach (Use use in demanded)
            {
                if (buildings.Count - slot <= dwellingsReserved)
                { dropped.Add(use); continue; }
                int pick = slot;
                if (use == Use.Kitchen || use == Use.Granary)
                    pick = EvidencePick(buildings, slot,
                        buildings.Count - dwellingsReserved, use,
                        biomeGroup, quartile, evidenceLines);
                if (pick != slot)
                {
                    Building chosen = buildings[pick];
                    buildings.RemoveAt(pick);
                    buildings.Insert(slot, chosen);
                }
                buildings[slot++].Function = use;
            }
            if (evidenceLines.Count > 0)
                Log.Message("[CA][Settlement][Evidence] " + record.name
                    + ": " + string.Join("; ", evidenceLines)
                    + "; source " + CASpatialRelationshipEvidence
                        .Provenance);
            // remainder are dwellings under the housing program; when no
            // housing program exists they stay empty shells, stated below.
            bool housing = has(CASettlementProgramCausalKernel.Housing)
                || has(CASettlementProgramCausalKernel.DomesticProvision);

            // Beds size to the residents this settlement is generated FOR.
            // Live pawns cannot be counted here: fit-out runs inside
            // materialization, before the resident group spawns.
            int residents = residentsEarly;

            // knowledge domains decide equipment tier, not one blanket level
            int manufacturing = Rank(faction,
                CATechnologyDomains.Manufacturing);
            int medicine = Rank(faction, CATechnologyDomains.Medicine);
            int electrical = Rank(faction, CATechnologyDomains.Electrical);

            int fittings = 0, beds = 0;
            var mix = new Dictionary<Use, int>();
            foreach (Building building in buildings)
            {
                if (building.Function == Use.Dwelling && !housing) continue;
                fittings += Fit(map, rect, building, faction, palette,
                    residents, ref beds, manufacturing, medicine, electrical,
                    tech);
                mix.TryGetValue(building.Function, out int had);
                mix[building.Function] = had + 1;
            }

            string stocks = Stock(map, record, buildings, residents, keys,
                manufacturing);

            Log.Message("[CA][Settlement][FitOut] " + record.name
                + ": programs consumed ["
                + string.Join(", ", keys.OrderBy(k => k, StringComparer
                    .Ordinal).Select(k => k.Replace("ca.settlement.", "")))
                + "]; mix " + string.Join(", ", mix
                    .OrderByDescending(p => p.Value)
                    .Select(p => p.Key + " x" + p.Value))
                + (dropped.Count > 0 ? "; dropped for want of buildings ["
                    + string.Join(", ", dropped) + "]" : "")
                + "; fittings " + fittings + "; beds " + beds + "/"
                + residents + (housing ? "" : " (no housing program)")
                + "; stocks [" + stocks + "]"
                + "; material " + palette.Name
                + "; knowledge mfg/med/elec " + manufacturing + "/"
                + medicine + "/" + electrical
                + "; env CA_SETTLEMENT_FITOUT=0 disables");
        }

        // ESTABLISHED RESERVES AND WORKING MATERIALS. What sits in a room
        // is what the represented operations actually supply: provision
        // arrangements land their contracted stock where meals are made,
        // agriculture and storage justify food reserves in the granary,
        // production keeps inputs and finished work at the bench. A
        // settlement without those operations holds correspondingly
        // little; shortage is a legitimate represented state, and the
        // receipt names the operator behind every pile.
        private static string Stock(Map map,
            CARegionalSettlementRecord record, List<Building> buildings,
            int residents, HashSet<string> keys, int manufacturing)
        {
            var lines = new List<string>();
            bool has(string key) => keys.Contains(key);
            Building granary = buildings.FirstOrDefault(b =>
                b.Function == Use.Granary);
            Building kitchen = buildings.FirstOrDefault(b =>
                b.Function == Use.Kitchen);
            Building workshop = buildings.FirstOrDefault(b =>
                b.Function == Use.Workshop);

            foreach (CAProvisionArrangement arrangement in
                record.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
            {
                if (arrangement == null || !arrangement.active) continue;
                if (!TryParseStock(arrangement.stockSource,
                    out ThingDef def, out int count)) continue;
                int laid = Drop(map, kitchen ?? granary, def, count);
                if (laid > 0)
                    lines.Add(def.defName + ":" + laid + " by "
                        + (arrangement.operatorIdentity ?? "?"));
            }

            List<CASettlementProgramEntry> entries =
                record.settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>();
            if (granary != null
                && (has(CASettlementProgramCausalKernel.Agriculture)
                    || has(CASettlementProgramCausalKernel.Storage)))
            {
                CASettlementProgramEntry food = entries.FirstOrDefault(e =>
                    e != null && e.programKey
                        == CASettlementProgramCausalKernel.Agriculture)
                    ?? entries.FirstOrDefault(e => e != null
                        && e.programKey
                            == CASettlementProgramCausalKernel.Storage);
                int operations = entries.Where(e => e != null
                        && (e.programKey == CASettlementProgramCausalKernel
                                .Agriculture
                            || e.programKey
                                == CASettlementProgramCausalKernel.Storage))
                    .Sum(e => Math.Max(1, e.count));
                // reserves the operation justifies: days of food per
                // resident, deeper for old established stores
                int days = Mathf.Clamp(3 + operations * 3
                    + Math.Max(0, record.historicalDevelopment) * 3, 3, 24);
                int amount = Mathf.Clamp(residents * days / 2, 20, 500);
                int laid = Drop(map, granary, Def("RawPotatoes"), amount);
                if (laid > 0)
                    lines.Add("RawPotatoes:" + laid + " (" + days
                        + " days) by " + (food?.operatorIdentity ?? "?"));
            }
            else if (granary == null && residents > 0
                && !has(CASettlementProgramCausalKernel.Gathering))
            {
                lines.Add("no stores: stated deficit");
            }

            if (workshop != null
                && (has(CASettlementProgramCausalKernel.Production)
                    || has(CASettlementProgramCausalKernel
                        .SpecializedIndustry)))
            {
                CASettlementProgramEntry work = entries.FirstOrDefault(e =>
                    e != null && (e.programKey
                            == CASettlementProgramCausalKernel.Production
                        || e.programKey == CASettlementProgramCausalKernel
                            .SpecializedIndustry));
                string worker = work?.operatorIdentity ?? "?";
                int wood = Drop(map, workshop, Def("WoodLog"),
                    30 + manufacturing * 15);
                if (wood > 0) lines.Add("WoodLog:" + wood + " by " + worker);
                if (manufacturing >= 2)
                {
                    int steel = Drop(map, workshop, Def("Steel"),
                        20 + manufacturing * 10);
                    if (steel > 0)
                        lines.Add("Steel:" + steel + " by " + worker);
                }
                if (manufacturing >= 4)
                {
                    int parts = Drop(map, workshop,
                        Def("ComponentIndustrial"), 4);
                    if (parts > 0)
                        lines.Add("ComponentIndustrial:" + parts + " by "
                            + worker);
                }
            }
            return lines.Count > 0 ? string.Join(", ", lines) : "none";
        }

        // stockSource idiom: "authored:Pemmican:40" -- a def name followed
        // by a count somewhere in the colon chain.
        private static bool TryParseStock(string source, out ThingDef def,
            out int count)
        {
            def = null;
            count = 0;
            if (source.NullOrEmpty()) return false;
            string[] parts = source.Split(':');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                ThingDef candidate = Def(parts[i]);
                if (candidate == null
                    || candidate.category != ThingCategory.Item) continue;
                if (!int.TryParse(parts[i + 1], out int parsed)
                    || parsed <= 0) continue;
                def = candidate;
                count = parsed;
                return true;
            }
            return false;
        }

        // Item stacks on the building's open floor, split by stack limit,
        // forbidden like every other settlement possession.
        private static int Drop(Map map, Building building, ThingDef def,
            int count)
        {
            if (building == null || def == null || count <= 0) return 0;
            int laid = 0;
            foreach (IntVec3 c in building.Cells.InRandomOrder())
            {
                if (laid >= count) break;
                if (!Free(map, c)) continue;
                if (c.GetThingList(map).Any(t =>
                    t.def.category == ThingCategory.Item)) continue;
                int stack = Math.Min(count - laid,
                    Math.Max(1, def.stackLimit));
                try
                {
                    Thing thing = ThingMaker.MakeThing(def);
                    thing.stackCount = stack;
                    GenSpawn.Spawn(thing, c, map);
                    thing.SetForbidden(true, false);
                    laid += stack;
                }
                catch (Exception) { break; }
            }
            return laid;
        }

        private static int Fit(Map map, CellRect rect, Building building,
            Faction faction, Palette palette, int residents, ref int beds,
            int manufacturing, int medicine, int electrical, TechLevel tech)
        {
            int n = 0;
            int area = building.Cells.Count;
            switch (building.Function)
            {
                case Use.Hall:
                    n += Put(map, rect, building, Def("Table2x2c")
                        ?? Def("TableShort"), faction, palette,
                        Mathf.Clamp(area / 30, 1, 2), 4);
                    n += Put(map, rect, building, Seat(tech), faction,
                        palette, Mathf.Clamp(area / 12, 2, 8), 12);
                    n += Put(map, rect, building, Light(electrical), faction,
                        palette, 2, 8);
                    break;
                case Use.Kitchen:
                    // provision placement already stands hearths and tables;
                    // only fill what it did not
                    n += Put(map, rect, building, Stove(electrical), faction,
                        palette, 1, 1);
                    n += Put(map, rect, building, Def("TableButcher"),
                        faction, palette, 1, 1);
                    n += Put(map, rect, building, Def("Shelf"), faction,
                        palette, 2, 6);
                    break;
                case Use.Granary:
                    n += Put(map, rect, building, Def("Shelf"), faction,
                        palette, Mathf.Clamp(area / 8, 2, 8), 16);
                    break;
                case Use.Infirmary:
                    n += Put(map, rect, building,
                        medicine >= 3 ? (Def("HospitalBed") ?? Def("Bed"))
                            : BedFor(tech), faction, palette,
                        Mathf.Clamp(area / 16, 1, 4), 6);
                    n += Put(map, rect, building, Light(electrical), faction,
                        palette, 1, 8);
                    break;
                case Use.Prison:
                    n += Put(map, rect, building, BedFor(tech), faction,
                        palette, Mathf.Clamp(area / 16, 1, 4), 99);
                    break;
                case Use.Workshop:
                    n += Put(map, rect, building, Bench(manufacturing),
                        faction, palette, 1, 2);
                    n += Put(map, rect, building, Def("Shelf"), faction,
                        palette, 1, 16);
                    n += Put(map, rect, building, Light(electrical), faction,
                        palette, 1, 8);
                    break;
                case Use.Study:
                    n += Put(map, rect, building,
                        electrical >= 3 ? (Def("HiTechResearchBench")
                            ?? Def("SimpleResearchBench"))
                            : Def("SimpleResearchBench"), faction, palette,
                        1, 1);
                    n += Put(map, rect, building, Light(electrical), faction,
                        palette, 1, 8);
                    break;
                case Use.Commons:
                    n += Put(map, rect, building, Def("ChessTable"), faction,
                        palette, 1, 2);
                    n += Put(map, rect, building, Seat(tech), faction,
                        palette, 2, 12);
                    break;
                default:
                    int want = Mathf.Clamp(area / 14, 1, 3);
                    int placed = Put(map, rect, building, BedFor(tech),
                        faction, palette,
                        Math.Min(want, Math.Max(0, residents - beds)), 99);
                    beds += placed;
                    n += placed;
                    n += Put(map, rect, building, Light(electrical), faction,
                        palette, 1, 99);
                    // Households differ: the same housing program does not
                    // issue identical comforts to every family. The ground a
                    // household holds seeds what it keeps -- and one in four
                    // keeps nothing beyond its beds.
                    int flavor = building.Cells.Count == 0 ? 0
                        : (building.Cells[0].x * 31
                            + building.Cells[0].z * 17) & 3;
                    if (flavor == 0)
                        n += Put(map, rect, building, Def("PlantPot"),
                            faction, palette, 1, 99);
                    else if (flavor == 1)
                    {
                        n += Put(map, rect, building, Def("Table1x2c"),
                            faction, palette, 1, 99);
                        n += Put(map, rect, building, Seat(tech), faction,
                            palette, 1, 99);
                    }
                    else if (flavor == 2)
                        n += Put(map, rect, building, Def("Shelf"), faction,
                            palette, 1, 99);
                    break;
            }
            return n;
        }

        // The corpus's learned trip chains rank which admitted building
        // takes a function: the kitchen candidate whose distance to the
        // hall best fits the foodprep-to-dining band wins the kitchen, and
        // the cold-store candidate is ranked against foodprep-to-cold from
        // the chosen kitchen. Falls back to size order whenever evidence,
        // reference points, or candidates are missing.
        private static int EvidencePick(List<Building> buildings, int from,
            int limit, Use use, string biomeGroup, string quartile,
            List<string> receiptLines)
        {
            if (!CASpatialRelationshipEvidence.Available) return from;
            string key = use == Use.Kitchen ? "foodprep-to-dining"
                : "foodprep-to-cold";
            Building reference = use == Use.Kitchen
                ? buildings.FirstOrDefault(b => b.Function == Use.Hall)
                : buildings.FirstOrDefault(b => b.Function == Use.Kitchen);
            if (reference == null || reference.Cells.Count == 0)
                return from;
            if (!CASpatialRelationshipEvidence.TryBand(key, biomeGroup,
                quartile, out CASpatialEvidenceBand band)) return from;

            IntVec3 anchor = Centroid(reference);
            int bestIndex = from, bestScore = int.MinValue;
            double bestDistance = 0;
            int considered = 0;
            for (int i = from; i < limit && i < buildings.Count; i++)
            {
                Building candidate = buildings[i];
                if (candidate.Cells.Count == 0) continue;
                considered++;
                IntVec3 centroid = Centroid(candidate);
                double distance = Math.Max(
                    Math.Abs(centroid.x - anchor.x),
                    Math.Abs(centroid.z - anchor.z));
                int score = CASpatialRelationshipEvidence.Score(distance,
                    band);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                    bestDistance = distance;
                }
            }
            if (considered > 1)
                receiptLines.Add(use + " by "
                    + CASpatialRelationshipEvidence.Describe(key, band,
                        bestDistance, bestScore)
                    + " over " + considered + " candidates");
            return bestIndex;
        }

        private static IntVec3 Centroid(Building building)
        {
            long x = 0, z = 0;
            foreach (IntVec3 cell in building.Cells)
            { x += cell.x; z += cell.z; }
            int n = Math.Max(1, building.Cells.Count);
            return new IntVec3((int)(x / n), 0, (int)(z / n));
        }

        // Place COUNT of DEF inside the building, but only while the whole
        // rect holds fewer than RECTCAP of it -- the program and provision
        // materializers place first and are not doubled.
        private static int Put(Map map, CellRect rect, Building building,
            ThingDef def, Faction faction, Palette palette, int count,
            int rectCap)
        {
            if (def == null || count <= 0) return 0;
            int standing = 0;
            foreach (IntVec3 c in rect)
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i].def == def && things[i].Position == c)
                        standing++;
                if (standing >= rectCap) return 0;
            }
            int made = 0;
            foreach (IntVec3 c in building.Cells.InRandomOrder())
            {
                if (made >= count || standing + made >= rectCap) break;
                bool fits = true;
                foreach (IntVec3 occupied in GenAdj.OccupiedRect(c,
                             Rot4.North, def.size))
                    if (!Free(map, occupied)) { fits = false; break; }
                if (!fits) continue;
                if (Spawn(map, def, c, faction, palette)) made++;
            }
            return made;
        }

        private static bool Free(Map map, IntVec3 c)
        {
            if (!c.InBounds(map) || !c.Standable(map)) return false;
            if (c.GetEdifice(map) != null) return false;
            List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i].def.category == ThingCategory.Building
                    || things[i] is Pawn) return false;
            return true;
        }

        private static bool Spawn(Map map, ThingDef def, IntVec3 c,
            Faction faction, Palette palette)
        {
            try
            {
                ThingDef stuff = null;
                if (def.MadeFromStuff)
                {
                    stuff = palette.Stuff != null
                        && def.stuffCategories != null
                        && palette.Stuff.stuffProps?.categories != null
                        && def.stuffCategories.Any(category =>
                            palette.Stuff.stuffProps.categories
                                .Contains(category))
                        ? palette.Stuff : GenStuff.DefaultStuffFor(def);
                }
                Thing thing = ThingMaker.MakeThing(def, stuff);
                CAConstructionMaterials.ApplyStyle(thing, palette.Culture);
                if (faction != null && def.CanHaveFaction)
                    thing.SetFaction(faction);
                GenSpawn.Spawn(thing, c, map, Rot4.North);
                return true;
            }
            catch (Exception) { return false; }
        }

        // ---- one material per settlement, from the rock under the map ----
        private static Palette PaletteFor(Map map, TechLevel tech,
            CAConstructionContext materials)
        {
            var palette = new Palette { Culture = materials.Culture };
            // The shared authority answers first; the historical
            // tech-banded palette below remains the fallback for cases it
            // cannot resolve.
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            if (wall != null)
            {
                ThingDef resolved = CAConstructionMaterials.ChooseFor(wall,
                    materials, out string basis);
                if (resolved != null)
                {
                    palette.Stuff = resolved;
                    palette.Name = basis;
                    return palette;
                }
            }
            try
            {
                if (tech <= TechLevel.Medieval)
                {
                    foreach (ThingDef rock in
                        Find.World.NaturalRockTypesIn(map.Tile))
                    {
                        ThingDef blocks = rock == null ? null
                            : Def("Blocks" + rock.defName);
                        if (blocks == null) continue;
                        palette.Stuff = blocks;
                        palette.Name = blocks.defName + " (local rock)";
                        break;
                    }
                    if (palette.Stuff == null)
                    {
                        palette.Stuff = Def("WoodLog");
                        palette.Name = "WoodLog (no local rock)";
                    }
                }
                else
                {
                    palette.Stuff = Def("Steel");
                    palette.Name = "Steel";
                }
            }
            catch (Exception) { }
            return palette;
        }

        // ---- defs, by name, so a modlist can differ -----------------------
        private static ThingDef Def(string name)
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(name);
        }

        internal static ThingDef BedFor(TechLevel tech)
        {
            return tech <= TechLevel.Neolithic
                ? (Def("Bedroll") ?? Def("Bed"))
                : (Def("Bed") ?? Def("Bedroll"));
        }

        private static ThingDef Light(int electrical)
        {
            return electrical >= 3
                ? (Def("StandingLamp") ?? Def("TorchLamp"))
                : (Def("TorchLamp") ?? Def("Torch"));
        }

        private static ThingDef Seat(TechLevel tech)
        {
            return tech >= TechLevel.Medieval
                ? (Def("DiningChair") ?? Def("Stool")) : Def("Stool");
        }

        private static ThingDef Stove(int electrical)
        {
            return electrical >= 3
                ? (Def("ElectricStove") ?? Def("FueledStove"))
                : (Def("FueledStove") ?? Def("Campfire"));
        }

        private static ThingDef Bench(int manufacturing)
        {
            if (manufacturing >= 4)
                return Def("TableMachining") ?? Def("ElectricSmithy")
                    ?? Def("TableSmithing");
            if (manufacturing >= 2)
                return Def("TableSmithing") ?? Def("HandTailoringBench");
            return Def("CraftingSpot");
        }

        private static int Rank(Faction faction, string domain)
        {
            try
            {
                if (faction != null)
                    return CATechnologicalKnowledgeRuntime.CanonicalRank(
                        faction, domain,
                        CATechnologyCompetencies.Construct);
            }
            catch (Exception) { }
            return 2;
        }

        internal static TechLevel TechFor(CARegionalSettlementRecord record)
        {
            try
            {
                if (record?.faction != null)
                    return CATechnologicalKnowledgeRuntime
                        .CanonicalBuildTechLevel(record.faction);
            }
            catch (Exception) { }
            return TechLevel.Medieval;
        }
    }
}
