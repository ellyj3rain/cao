using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // THE LANDING-SWAP HARNESS.
    //
    // The invariant this repository committed to is: the same candidate, the
    // same footprint, a DIFFERENT landing member => identical regional
    // geography and feature state. Only the landing marker, the start
    // position, and genuinely entry-specific state may differ.
    //
    // That claim is worth nothing while it is asserted rather than measured, so
    // this produces a receipt that can FALSIFY it. Two maps generated from the
    // same candidate with different landing members are compared by digest: if
    // the INVARIANT digest differs, the boundary is incomplete and the per-
    // subject lines say exactly which subject moved.
    //
    // The receipt is deliberately blunt. It hashes observable OUTCOMES on the
    // finished map - the terrain that was written, the rock that was placed,
    // the plants that grew, the records that exist - rather than the inputs
    // that were supposed to produce them. Hashing inputs would only re-assert
    // the substitution; hashing outcomes can catch a reader nobody enumerated.
    internal static class CARegionalLandingInvariantHarness
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private struct Line
        {
            internal string subject;
            internal ulong hash;
            internal string detail;
            internal bool invariant;
        }

        private sealed class Digest
        {
            private ulong value = FnvOffset;

            internal void Add(string text)
            {
                if (text == null) text = "\0";
                for (int i = 0; i < text.Length; i++)
                {
                    value = (value ^ text[i]) * FnvPrime;
                }
                value = (value ^ 0x1F) * FnvPrime;
            }

            internal void Add(int number)
            {
                Add(number.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }

            internal void Add(float number)
            {
                // Quantised. Float noise below this threshold is not something
                // a player can observe, and exact bit equality would make the
                // receipt fail for reasons that are not landing choice.
                Add(Mathf.RoundToInt(number * 1000f));
            }

            internal ulong Value { get { return value; } }
        }

        private static string Hex(ulong value)
        {
            return value.ToString("X16");
        }

        // ---- the receipt ---------------------------------------------------

        internal static string Receipt(Map map, string occasion)
        {
            if (map == null) return "[CA][Regional][Invariant] no map.";
            var lines = new List<Line>();
            CARegionalPlan region = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            CARegionalProjectionMapComponent projection = map.GetComponent<
                CARegionalProjectionMapComponent>();
            if (region == null && projection?.Active == true)
                region = projection.Region;

            Anchor(map, region, lines);
            Terrain(map, lines);
            FeaturesAndMutators(map, region, lines);
            StonePalette(map, lines);
            PlantsAndThings(map, lines);
            PollutionAndSnow(map, lines);
            History(region, lines);
            Settlements(map, region, lines);
            Entry(map, region, lines);

            var invariant = new Digest();
            foreach (Line line in lines.Where(item => item.invariant)
                .OrderBy(item => item.subject, StringComparer.Ordinal))
            {
                invariant.Add(line.subject);
                invariant.Add(Hex(line.hash));
            }

            var text = new StringBuilder();
            text.AppendLine("[CA][Regional][Invariant] landing-swap receipt ("
                + occasion + ")");
            text.AppendLine("  PRECONDITION candidate "
                + (region?.candidateId ?? "none")
                + "  <- two receipts are COMPARABLE ONLY if this matches. A "
                + "candidate id is a fresh Guid per created candidate and it "
                + "seeds every feature instance, so two separately built "
                + "candidates over the same footprint differ legitimately. "
                + "Move the landing pin on the SAME pending candidate; do not "
                + "rebuild or reroll it.");
            text.AppendLine("  region " + (region?.regionalId ?? "none")
                + "; footprint anchor " + (region?.bundleRootTileId ?? -1)
                + "; landing " + (region?.startTileId ?? -1)
                + "; members [" + (region?.memberTileIds == null ? ""
                    : string.Join(",", region.memberTileIds)) + "]");
            text.AppendLine("  map " + map.uniqueID + " " + map.Size.x + "x"
                + map.Size.z + "; parented tile " + map.Tile.tileId
                + "; engine root answering intercepted reads "
                + CARegionalEngineRoot.AnchorTileId(map));
            text.AppendLine("  INVARIANT DIGEST " + Hex(invariant.Value)
                + "  <- this value must be IDENTICAL across two landings of "
                + "the same candidate");
            foreach (Line line in lines)
            {
                text.AppendLine("    " + (line.invariant ? "[=] " : "[~] ")
                    + line.subject.PadRight(22) + Hex(line.hash)
                    + (string.IsNullOrEmpty(line.detail)
                        ? "" : "  " + line.detail));
            }
            text.AppendLine("  [=] must not differ between landings; [~] may.");
            text.AppendLine("  NOT COVERED by this receipt, stated rather than "
                + "implied: content this install does not load (only five "
                + "TileMutatorDefs are present, and no Odyssey or Anomaly "
                + "content), anything a mod places after generation, and the "
                + "runtime colony clock - GenLocalDate's hour-of-day family "
                + "still reads the parented tile by design.");
            return text.ToString();
        }

        // ---- subjects ------------------------------------------------------

        private static void Add(List<Line> lines, string subject, Digest digest,
            string detail, bool invariant = true)
        {
            lines.Add(new Line
            {
                subject = subject,
                hash = digest.Value,
                detail = detail,
                invariant = invariant
            });
        }

        private static void Anchor(Map map, CARegionalPlan region,
            List<Line> lines)
        {
            var digest = new Digest();
            digest.Add(region?.regionalId);
            // candidateId is DELIBERATELY absent from this digest. It is a
            // fresh Guid per created candidate (RegionalSetupModule.cs:1072)
            // and it seeds every feature instance through
            // CAFeatureInstance.SeedFor, which is intended - a reroll is meant
            // to yield genuinely different features. Including it would make
            // two runs differ for a reason that is not landing choice, i.e. a
            // guaranteed false failure. It is reported instead as a
            // PRECONDITION: two receipts are only comparable when their
            // candidate ids are equal.
            digest.Add(region?.bundleRootTileId ?? -1);
            digest.Add(region?.mapSize ?? 0);
            digest.Add(region?.FootprintRotation ?? 0);
            digest.Add(map.Size.x);
            digest.Add(map.Size.z);
            foreach (int id in (region?.memberTileIds
                ?? new List<int>()).OrderBy(id => id)) digest.Add(id);
            foreach (int id in (region?.ReservedTileIds
                ?? (IReadOnlyList<int>)new List<int>()).OrderBy(id => id))
                digest.Add(id);
            digest.Add(CARegionalEngineRoot.AnchorTileId(map));
            Add(lines, "footprint+anchor", digest,
                "identity, members, backing size, resolved engine root");
        }

        private static void Terrain(Map map, List<Line> lines)
        {
            var terrain = new Digest();
            var roof = new Digest();
            var fertility = new Digest();
            foreach (IntVec3 cell in map.AllCells)
            {
                TerrainDef under = map.terrainGrid.UnderTerrainAt(cell);
                terrain.Add(map.terrainGrid.TerrainAt(cell)?.defName);
                terrain.Add(under?.defName);
                roof.Add(map.roofGrid.RoofAt(cell)?.defName);
                fertility.Add(map.fertilityGrid.FertilityAt(cell));
            }
            Add(lines, "terrain", terrain, "surface + under terrain, all cells");
            Add(lines, "roof", roof, "roof grid, all cells");
            Add(lines, "fertility", fertility, "fertility grid, all cells");
        }

        private static void FeaturesAndMutators(Map map, CARegionalPlan region,
            List<Line> lines)
        {
            var digest = new Digest();
            // What the engine believes the map's own mutators are - this is
            // the anchored TileInfo, and it decides which extra gensteps ran.
            try
            {
                foreach (TileMutatorDef mutator in map.TileInfo.Mutators
                    .Where(item => item != null)
                    .OrderBy(item => item.defName, StringComparer.Ordinal))
                    digest.Add(mutator.defName);
                digest.Add(map.TileInfo.PrimaryBiome?.defName);
                digest.Add(map.TileInfo.hilliness.ToString());
                digest.Add(map.TileInfo.HillinessForElevationGen.ToString());
                digest.Add(map.TileInfo.WaterCovered ? 1 : 0);
            }
            catch { digest.Add("tileinfo-unavailable"); }

            // And what every constituent carries, which is what CA projects.
            foreach (int id in (region?.ReservedTileIds
                ?? (IReadOnlyList<int>)new List<int>()).OrderBy(id => id))
            {
                digest.Add(id);
                try
                {
                    PlanetTile tile = CARegionalPlanUtility.SurfaceTile(id);
                    if (!tile.Valid) { digest.Add("invalid"); continue; }
                    Tile info = tile.Tile;
                    digest.Add(info?.PrimaryBiome?.defName);
                    digest.Add(info?.hilliness.ToString());
                    foreach (TileMutatorDef mutator in (info?.Mutators
                        ?? Enumerable.Empty<TileMutatorDef>())
                        .Where(item => item != null)
                        .OrderBy(item => item.defName, StringComparer.Ordinal))
                        digest.Add(mutator.defName);
                    Landmark landmark = info?.Landmark;
                    digest.Add(landmark?.def?.defName);
                    digest.Add(landmark?.name);
                }
                catch { digest.Add("member-unreadable"); }
            }
            Add(lines, "features+landmarks", digest,
                "map mutators/biome plus every constituent's mutators and "
                + "landmark");
        }

        private static void StonePalette(Map map, List<Line> lines)
        {
            var present = new Digest();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var positions = new Digest();
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing?.def?.building == null
                    || !thing.def.building.isNaturalRock) continue;
                string name = thing.def.defName;
                int count;
                counts.TryGetValue(name, out count);
                counts[name] = count + 1;
                positions.Add(name);
                positions.Add(thing.Position.x);
                positions.Add(thing.Position.z);
            }
            foreach (KeyValuePair<string, int> entry in counts
                .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                present.Add(entry.Key);
                present.Add(entry.Value);
            }
            // Terrain-side stone matters too: the floor a rock leaves behind
            // is chosen from the same palette.
            var floors = new Digest();
            foreach (IntVec3 cell in map.AllCells)
            {
                TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                if (terrain?.categoryType
                    == TerrainDef.TerrainCategoryType.Stone)
                {
                    floors.Add(terrain.defName);
                    floors.Add(cell.x);
                    floors.Add(cell.z);
                }
            }
            Add(lines, "stone palette", present,
                counts.Count + " natural rock def(s): "
                + string.Join(", ", counts.OrderBy(item => item.Key,
                        StringComparer.Ordinal)
                    .Select(item => item.Key + "x" + item.Value)));
            Add(lines, "stone layout", positions,
                "every natural rock, def and position");
            Add(lines, "stone floors", floors, "stone-category terrain cells");
        }

        private static void PlantsAndThings(Map map, List<Line> lines)
        {
            var plants = new Digest();
            var others = new Digest();
            int plantCount = 0;
            foreach (Thing thing in map.listerThings.AllThings
                .OrderBy(item => item.Position.z * 100000 + item.Position.x)
                .ThenBy(item => item.def?.defName, StringComparer.Ordinal))
            {
                if (thing?.def == null) continue;
                if (thing.def.category == ThingCategory.Plant)
                {
                    plantCount++;
                    plants.Add(thing.def.defName);
                    plants.Add(thing.Position.x);
                    plants.Add(thing.Position.z);
                    var plant = thing as Plant;
                    if (plant != null) plants.Add(plant.Growth);
                    continue;
                }
                if (thing.def.building != null
                    && thing.def.building.isNaturalRock) continue;
                if (thing is Pawn) continue;
                others.Add(thing.def.defName);
                others.Add(thing.Position.x);
                others.Add(thing.Position.z);
                others.Add(thing.stackCount);
            }
            Add(lines, "plants", plants, plantCount + " plant(s), def, "
                + "position and growth");
            Add(lines, "placed things", others,
                "every non-pawn non-rock thing, def, position and stack");
        }

        private static void PollutionAndSnow(Map map, List<Line> lines)
        {
            var pollution = new Digest();
            var snow = new Digest();
            int polluted = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                bool isPolluted = false;
                try { isPolluted = map.pollutionGrid.IsPolluted(cell); }
                catch { }
                if (isPolluted)
                {
                    polluted++;
                    pollution.Add(cell.x);
                    pollution.Add(cell.z);
                }
                try { snow.Add(map.snowGrid.GetDepth(cell)); }
                catch { snow.Add(-1); }
            }
            Add(lines, "pollution", pollution, polluted + " polluted cell(s)");
            Add(lines, "snow", snow, "snow depth, all cells");
        }

        private static void History(CARegionalPlan region, List<Line> lines)
        {
            var digest = new Digest();
            if (region != null)
            {
                foreach (CARegionalFactionPlan group in region
                    .factions.Where(item => item != null)
                    .OrderBy(item => item.key))
                {
                    digest.Add(group.key);
                    digest.Add(group.source.ToString());
                    digest.Add(group.existingFactionLoadId);
                    digest.Add(group.customFactionDefName);
                    digest.Add(group.customName);
                    digest.Add(group.playerRelation.ToString());
                    digest.Add(group.resolvedFaction?.Name);
                }
                foreach (CARegionalRelationPlan pair in region.relations
                    .Where(item => item != null)
                    .OrderBy(item => item.leftFactionKey)
                    .ThenBy(item => item.rightFactionKey))
                {
                    digest.Add(pair.leftFactionKey);
                    digest.Add(pair.rightFactionKey);
                    digest.Add(pair.relation.ToString());
                }
                foreach (CARegionalSettlementPlan settlement in region.settlements
                    .Where(item => item != null).OrderBy(item => item.slot))
                {
                    digest.Add(settlement.slot);
                    digest.Add(settlement.memberTileId);
                    digest.Add(settlement.factionKey);
                    digest.Add(settlement.siteClusterKey);
                    digest.Add(settlement.persistent ? 1 : 0);
                }
            }
            Add(lines, "history", digest,
                "factions, relations, and settlements");
        }

        private static void Settlements(Map map, CARegionalPlan region,
            List<Line> lines)
        {
            var identity = new Digest();
            var layout = new Digest();
            var garrison = new Digest();
            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            List<CARegionalSettlementRecord> records = world == null
                ? new List<CARegionalSettlementRecord>()
                : world.ForMap(map).Where(item => item != null)
                    .OrderBy(item => item.slot).ToList();
            foreach (CARegionalSettlementRecord record in records)
            {
                identity.Add(record.regionalId);
                identity.Add(record.regionKey);
                identity.Add(record.slot);
                identity.Add(record.memberTileId);
                identity.Add(record.name);
                identity.Add(record.factionDefName);
                identity.Add(record.faction?.Name);
                identity.Add(record.factionEra);
                identity.Add(record.settlementForm);
                identity.Add(record.settlementProgram?.sourceSignature);
                identity.Add(record.populationBaseline);

                layout.Add(record.slot);
                layout.Add(record.localRect.minX);
                layout.Add(record.localRect.minZ);
                layout.Add(record.localRect.Width);
                layout.Add(record.localRect.Height);
                if (record.localRect.Area > 0)
                {
                    foreach (IntVec3 cell in record.localRect.Cells)
                    {
                        if (!cell.InBounds(map)) continue;
                        layout.Add(map.terrainGrid.TerrainAt(cell)?.defName);
                        foreach (Thing thing in map.thingGrid
                            .ThingsListAtFast(cell)
                            .Where(item => item?.def != null && !(item is Pawn))
                            .OrderBy(item => item.def.defName,
                                StringComparer.Ordinal))
                        {
                            layout.Add(thing.def.defName);
                            layout.Add(thing.Position.x);
                            layout.Add(thing.Position.z);
                        }
                    }
                }

                garrison.Add(record.slot);
                foreach (string resident in (record.residentIds
                    ?? new List<string>()).OrderBy(item => item,
                        StringComparer.Ordinal))
                    garrison.Add(resident);
                if (record.faction != null)
                {
                    foreach (Pawn pawn in map.mapPawns
                        .SpawnedPawnsInFaction(record.faction)
                        .OrderBy(item => item.Position.z * 100000
                            + item.Position.x))
                    {
                        garrison.Add(pawn.kindDef?.defName);
                        garrison.Add(pawn.Name?.ToStringFull);
                        garrison.Add(pawn.Position.x);
                        garrison.Add(pawn.Position.z);
                    }
                }
            }
            Add(lines, "settlement identity", identity,
                records.Count + " record(s)");
            Add(lines, "settlement layout", layout,
                "terrain and structures inside every settlement rect");
            Add(lines, "settlement garrison", garrison,
                "residents and spawned pawns per settlement faction");
        }

        private static void Entry(Map map, CARegionalPlan region,
            List<Line> lines)
        {
            var digest = new Digest();
            digest.Add(map.Tile.tileId);
            digest.Add(region?.startTileId ?? -1);
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
                .OrderBy(item => item.Position.z * 100000 + item.Position.x)
                .ToList();
            foreach (Pawn pawn in colonists)
            {
                digest.Add(pawn.Position.x);
                digest.Add(pawn.Position.z);
            }
            Add(lines, "entry state", digest,
                "landing tile and colonist start positions - this line is "
                + "EXPECTED to differ", false);
        }
    }

    // The automatic half of the harness. A receipt nobody remembers to ask for
    // is not a falsifier, so a regional map emits one at the end of its own
    // generation whenever developer mode is on. The debug action below is the
    // same measurement taken on demand.
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
    internal static class CARegionalLandingInvariantReceiptPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map __result)
        {
            try
            {
                if (__result == null || !Prefs.DevMode) return;
                if (!CARegionalEngineRoot.IsRegional(__result)) return;
                CARegionalProjectionMapComponent projection = __result
                    .GetComponent<CARegionalProjectionMapComponent>();
                if (projection?.Active == true)
                    Log.Message(projection.MountainFixtureReceipt());
                Log.Message(CARegionalLandingInvariantHarness.Receipt(
                    __result, "map generated"));
            }
            catch (Exception error)
            {
                Log.Warning("[CA][Regional][Invariant] receipt failed: "
                    + error);
            }
        }
    }

    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness",
            "Regional landing-swap invariant receipt",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RegionalLandingInvariantReceipt()
        {
            Map map = Verse.Find.CurrentMap;
            if (map == null)
            {
                Log.Message("[CA][Regional][Invariant] no current map.");
                return;
            }
            Log.Message(CARegionalLandingInvariantHarness.Receipt(map,
                "on demand"));
        }

        [DebugAction("Colonist Awareness",
            "Regional Mountain fixture receipt",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RegionalMountainFixtureReceipt()
        {
            CARegionalProjectionMapComponent projection = Verse.Find.CurrentMap
                ?.GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active != true)
            {
                Log.Message("[CA][Regional][Mountain] the current map is not "
                    + "regional.");
                return;
            }
            Log.Message(projection.MountainFixtureReceipt());
        }
    }
}
