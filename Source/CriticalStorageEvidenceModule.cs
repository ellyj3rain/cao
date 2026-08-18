using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Read-only evidence for strategically important loose or stored assets.
    // Native filters, priorities, shelves, zones, and hauling remain canonical.
    internal static class CACriticalStorageEvidenceModule
    {
        internal static string Evaluate(Map map)
        {
            if (map == null || !map.IsPlayerHome)
                return "[CA] critical storage evidence\nrefused; no authorized "
                    + "player-home map is loaded";

            CAStorageProgramMapComponent storage =
                CAStorageProgramMapComponent.For(map);
            IReadOnlyList<CAInventoryStorageProjection> provenance = storage?
                .HistoricalInventoryProvenanceForObservation
                ?? Array.Empty<CAInventoryStorageProjection>();
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> observedPrograms = programs?
                .ProgramsForObservation ?? Array.Empty<CASpaceProgram>();
            List<CASpaceProgram> medicalPrograms = observedPrograms
                .Where(program => program != null
                    && program.author == CASpaceAuthor.Player
                    && program.purpose == CASpacePurpose.Medical)
                .OrderBy(program => program.id).ToList();
            List<CASpaceProgram> defensePrograms = observedPrograms
                .Where(program => program != null
                    && program.author == CASpaceAuthor.Player
                    && program.purpose == CASpacePurpose.Defense)
                .OrderBy(program => program.id).ToList();
            List<Building_Turret> turrets = map.listerBuildings
                .allBuildingsColonist.OfType<Building_Turret>()
                .Where(turret => turret != null && turret.Spawned)
                .OrderBy(turret => turret.thingIDNumber).ToList();
            Pawn hauler = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn != null && !pawn.Downed
                    && !pawn.Drafted && !pawn.InMentalState
                    && pawn.health?.capacities?.CapableOf(
                        PawnCapacityDefOf.Manipulation) == true)
                .OrderBy(pawn => pawn.thingIDNumber).FirstOrDefault();

            List<Thing> assets = map.listerThings.AllThings
                .Where(thing => thing != null && thing.Spawned
                    && thing.Map == map && !thing.Destroyed
                    && (CAStorageProfile.IsMedicalSupply(thing.def)
                        || thing.def == ThingDefOf.Silver
                        || thing.def == ThingDefOf.WoodLog
                        || IsMinifiedAnimalBed(thing)))
                .OrderBy(thing => AssetOrder(thing))
                .ThenBy(thing => thing.def.defName)
                .ThenBy(thing => thing.thingIDNumber).ToList();

            var builder = new StringBuilder();
            builder.AppendLine("[CA] critical storage evidence")
                .Append("read-only; observed medical supplies ")
                .Append(assets.Count(thing =>
                    CAStorageProfile.IsMedicalSupply(thing.def)))
                .Append(" stack(s), Silver ")
                .Append(assets.Where(thing => thing.def == ThingDefOf.Silver)
                    .Sum(thing => thing.stackCount))
                .Append(" units, WoodLog ")
                .Append(assets.Where(thing => thing.def == ThingDefOf.WoodLog)
                    .Sum(thing => thing.stackCount))
                .Append(" units, minified animal beds ")
                .Append(assets.Count(IsMinifiedAnimalBed)).AppendLine()
                .Append("historical differentiated-inventory zone provenance ")
                .Append(provenance.Count).Append(" record(s); operative "
                    + "projection ownership retired ")
                .Append(storage?
                    .InventoryProjectionOwnershipRetiredForObservation
                    == true).AppendLine();

            if (assets.Count == 0)
                builder.AppendLine("assets: none");
            for (int i = 0; i < assets.Count; i++)
                AppendAsset(builder, map, assets[i], hauler, provenance,
                    medicalPrograms, defensePrograms, turrets);

            builder.Append("interpretation: roof and natural overhang are "
                + "positive protection evidence, not proof of ideal storage. "
                + "Medical criticality, deterioration, temperature, treatment "
                + "access, settlement integration, edge exposure, authored "
                + "defense topology, present destination authority, and "
                + "historical zone origin remain separate axes. A "
                + "better-priority native destination is an available option, "
                + "not an automatic relocation order. No zone, filter, "
                + "priority, item, designation, or program was changed.");
            return builder.ToString();
        }

        private static void AppendAsset(StringBuilder builder, Map map,
            Thing thing, Pawn hauler,
            IReadOnlyList<CAInventoryStorageProjection> provenance,
            List<CASpaceProgram> medicalPrograms,
            List<CASpaceProgram> defensePrograms,
            List<Building_Turret> turrets)
        {
            IntVec3 cell = thing.Position;
            Zone_Stockpile zone = map.zoneManager.ZoneAt(cell)
                as Zone_Stockpile;
            IHaulDestination destination =
                StoreUtility.CurrentHaulDestinationOf(thing);
            RoofDef roof = map.roofGrid.RoofAt(cell);
            Room room = cell.GetRoom(map);
            CAStorageProfile profile = CAStorageProfile.For(thing.def);
            CAInventoryStorageProjection origin = zone == null ? null
                : provenance.FirstOrDefault(record => record != null
                    && record.zoneId == zone.ID);
            bool reachable = hauler != null && hauler.CanReach(thing,
                PathEndMode.Touch, Danger.Deadly);
            StoragePriority currentPriority =
                StoreUtility.CurrentStoragePriorityOf(thing);
            IntVec3 betterCell = IntVec3.Invalid;
            bool betterNativeDestination = hauler != null
                && StoreUtility.TryFindBestBetterStoreCellFor(thing, hauler,
                    map, currentPriority, Faction.OfPlayer, out betterCell,
                    needAccurateResult: true);

            string category;
            if (CAStorageProfile.IsMedicalSupply(thing.def))
                category = "medical supply; potential life-safety loss, exact "
                    + "disease demand not inferred";
            else if (thing.def == ThingDefOf.Silver)
                category = "currency and protected trade value";
            else if (thing.def == ThingDefOf.WoodLog)
                category = "ordinary construction material";
            else
                category = "existing installable animal-bed asset; no "
                    + "installed sleeping function";

            builder.Append("asset ").Append(thing.ThingID).Append(" ")
                .Append(thing.def.defName).Append(" x")
                .Append(thing.stackCount).Append(" at ").Append(cell)
                .AppendLine()
                .Append("  criticality: ").Append(category)
                .Append("; market value ")
                .Append((thing.MarketValue * thing.stackCount)
                    .ToStringMoney()).AppendLine()
                .Append("  environment: roof ")
                .Append(roof == null ? "none" : roof.isNatural
                    ? "natural" : "constructed")
                .Append("; room ")
                .Append(room == null ? "none" : room.Role?.label ?? "unknown")
                .Append("; psychologically outdoors ")
                .Append(room == null || room.PsychologicallyOutdoors)
                .Append("; temperature ")
                .Append(cell.GetTemperature(map).ToStringTemperature())
                .Append("; storage profile ").Append(profile.Summary)
                .AppendLine()
                .Append("  logistics: native destination ")
                .Append(DestinationLabel(destination, zone))
                .Append("; priority ").Append(currentPriority.Label())
                .Append("; in Home ").Append(map.areaManager.Home[cell])
                .Append("; reachable by observation hauler ")
                .Append(reachable)
                .Append("; nearest authored Medical footprint ")
                .Append(Distance(NearestProgramDistance(cell,
                    medicalPrograms)))
                .AppendLine()
                .Append("  protection: map-edge depth ")
                .Append(MapEdgeDepth(cell, map)).Append(" cells; nearest "
                    + "authored Defense footprint ")
                .Append(Distance(NearestProgramDistance(cell,
                    defensePrograms)))
                .Append("; nearest turret ")
                .Append(Distance(NearestBuildingDistance(cell,
                    turrets.Cast<Building>())))
                .Append("; turret distance alone proves no defended-side relation")
                .AppendLine()
                .Append("  provenance: ")
                .Append(Provenance(origin, zone)).AppendLine();

            if (!betterNativeDestination)
            {
                builder.AppendLine("  better-priority accepted destination: "
                    + "none observed through native storage search");
                return;
            }
            RoofDef betterRoof = map.roofGrid.RoofAt(betterCell);
            Zone_Stockpile betterZone = map.zoneManager.ZoneAt(betterCell)
                as Zone_Stockpile;
            builder.Append("  better-priority accepted destination: ")
                .Append(betterCell).Append(" in ")
                .Append(betterZone == null ? "native storage building"
                    : "zone #" + betterZone.ID + " '" + betterZone.label + "'")
                .Append("; roof ")
                .Append(betterRoof == null ? "none" : betterRoof.isNatural
                    ? "natural" : "constructed")
                .Append("; temperature ")
                .Append(betterCell.GetTemperature(map).ToStringTemperature())
                .Append("; in Home ").Append(map.areaManager.Home[betterCell])
                .Append("; map-edge depth ")
                .Append(MapEdgeDepth(betterCell, map)).Append(" cells; Medical "
                    + "footprint distance ")
                .Append(Distance(NearestProgramDistance(betterCell,
                    medicalPrograms)))
                .AppendLine("; advisory only");
        }

        private static string DestinationLabel(IHaulDestination destination,
            Zone_Stockpile zone)
        {
            if (zone != null)
                return "zone #" + zone.ID + " '" + zone.label + "'";
            Thing building = destination as Thing;
            return building == null ? "none"
                : building.def.label + " " + building.ThingID + " at "
                    + building.Position;
        }

        private static string Provenance(
            CAInventoryStorageProjection origin, Zone_Stockpile zone)
        {
            if (origin == null)
                return zone == null ? "loose or non-zone asset; placement "
                    + "author unavailable" : "historical zone origin and "
                    + "current item-movement author unavailable; current "
                    + "native zone remains player-editable";
            return "legacy CA zone-origin objective '"
                + origin.objectiveKey + "', kind " + origin.kind
                + ", created tick " + origin.createdTick + ", provisional "
                + origin.provisional + ", placement policy version "
                + origin.placementPolicyVersion + "; proves only how the "
                + "native zone footprint originated, never who moved or placed "
                + "this item here; operative CA ownership retired while this "
                + "zone-origin evidence remains read-only";
        }

        private static bool IsMinifiedAnimalBed(Thing thing)
        {
            Building_Bed bed = (thing as MinifiedThing)?.InnerThing
                as Building_Bed;
            return bed != null && bed.def?.IsBed == true
                && bed.def.building?.bed_humanlike == false;
        }

        private static int AssetOrder(Thing thing)
        {
            if (CAStorageProfile.IsMedicalSupply(thing.def)) return 0;
            if (thing.def == ThingDefOf.Silver) return 1;
            if (IsMinifiedAnimalBed(thing)) return 2;
            if (thing.def == ThingDefOf.WoodLog) return 3;
            return 4;
        }

        private static int MapEdgeDepth(IntVec3 cell, Map map)
        {
            return Math.Min(Math.Min(cell.x, cell.z),
                Math.Min(map.Size.x - 1 - cell.x,
                    map.Size.z - 1 - cell.z));
        }

        private static int NearestProgramDistance(IntVec3 cell,
            List<CASpaceProgram> programs)
        {
            int nearest = int.MaxValue;
            for (int i = 0; i < programs.Count; i++)
                for (int c = 0; c < programs[i].cells.Count; c++)
                    nearest = Math.Min(nearest,
                        cell.DistanceToSquared(programs[i].cells[c]));
            return nearest == int.MaxValue ? int.MaxValue
                : (int)Math.Round(Math.Sqrt(nearest));
        }

        private static int NearestBuildingDistance(IntVec3 cell,
            IEnumerable<Building> buildings)
        {
            int nearest = int.MaxValue;
            foreach (Building building in buildings)
                nearest = Math.Min(nearest,
                    cell.DistanceToSquared(building.Position));
            return nearest == int.MaxValue ? int.MaxValue
                : (int)Math.Round(Math.Sqrt(nearest));
        }

        private static string Distance(int distance)
        {
            return distance == int.MaxValue ? "absent"
                : distance + " cells";
        }
    }
}
