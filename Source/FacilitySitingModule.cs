using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal static class CASettlementTopology
    {
        internal static bool IsExteriorIngress(Map map, IntVec3 root)
        {
            if (map == null || !root.InBounds(map)) return false;
            bool interior = false;
            bool exterior = false;
            for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
            {
                IntVec3 cell = root + GenAdj.CardinalDirections[i];
                if (!cell.InBounds(map) || !cell.Walkable(map)) continue;
                Room room = cell.GetRoom(map);
                if (room != null && !room.PsychologicallyOutdoors)
                    interior = true;
                else exterior = true;
            }
            return interior && exterior;
        }
    }

    internal sealed class CAFacilityRoomEvidence
    {
        internal Room room;
        internal IntVec3 anchor = IntVec3.Invalid;
        internal int cells;
        internal int claimedCells;
        internal int roofedCells;
        internal int naturalRoofCells;
        internal int mineableBoundaryCells;
        internal int doors;
        internal int exteriorIngressDoors;
        internal int stockpileCells;
        internal int stockpileZones;
        internal int playerBuildings;
        internal int storageBuildings;
        internal int beds;
        internal int temperatureControls;
        internal int wastepackStacks;
        internal int wastepackUnits;
        internal int poweredCells;
        internal int nearestPower = int.MaxValue;
        internal int nearestHabitation = int.MaxValue;
        internal int nearestKitchen = int.MaxValue;
        internal int nearestExteriorIngress = int.MaxValue;
        internal int nearestTurret = int.MaxValue;
        internal int nearestDefenseProgram = int.MaxValue;
        internal bool reachable;
        internal float temperature;
        internal readonly List<CASpaceProgram> programs =
            new List<CASpaceProgram>();

        internal float RoofedShare => cells > 0
            ? roofedCells / (float)cells : 0f;

        internal float NaturalRoofShare => cells > 0
            ? naturalRoofCells / (float)cells : 0f;

        internal string Receipt()
        {
            string programSummary = programs.Count == 0
                ? "none"
                : string.Join(", ", programs
                    .OrderBy(program => program.id)
                    .Select(program => (program.label.NullOrEmpty()
                            ? CASpacePurposeInfo.Label(program.purpose)
                            : program.label)
                        + " #" + program.id + " ("
                        + CASpacePurposeInfo.Label(program.purpose) + ")")
                    .ToArray());
            string role = room?.Role?.defName ?? "none";
            return "  room #" + (room?.ID ?? -1) + " at " + anchor
                + ": cells " + cells + ", Home " + claimedCells
                + ", roof " + roofedCells + "/" + cells + " ("
                + Percent(RoofedShare) + "), natural roof "
                + naturalRoofCells + "/" + cells + " ("
                + Percent(NaturalRoofShare) + "), open roof "
                + (room?.OpenRoofCount ?? 0) + ", mineable boundary "
                + mineableBoundaryCells + ", temperature "
                + temperature.ToString("F1") + " C, native role " + role
                + "\n    access: doors " + doors + " (exterior ingress "
                + exteriorIngressDoors + "), nearest colony exterior ingress "
                + Distance(nearestExteriorIngress) + ", reachable "
                + YesNo(reachable)
                + "; services: grid cells " + poweredCells
                + ", nearest grid " + Distance(nearestPower)
                + ", temperature controls " + temperatureControls
                + "\n    strategic relations: habitation "
                + Distance(nearestHabitation) + ", Kitchen program "
                + Distance(nearestKitchen) + ", player turret "
                + Distance(nearestTurret) + ", Defense program "
                + Distance(nearestDefenseProgram)
                + "\n    present use: programs " + programSummary
                + "; stockpile cells " + stockpileCells + " in "
                + stockpileZones + " zone(s); buildings " + playerBuildings
                + " (storage " + storageBuildings + ", beds " + beds
                + "); wastepacks " + wastepackStacks + " stack(s)/"
                + wastepackUnits + " unit(s)";
        }

        private static string Distance(int value)
        {
            return value == int.MaxValue ? "none observed" : value + " cells";
        }

        private static string Percent(float value)
        {
            return (value * 100f).ToString("F0") + "%";
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }
    }

    // Read-only evidence for future facility siting and exact existing authored
    // facility footprints. It reports separate facts for exposure, mountain
    // protection, access, power, habitation, defense, refrigeration, and
    // bounded expansion; it does not collapse them into a universal "deeper is
    // better" score or author a facility purpose.
    internal static class CAFacilitySitingModule
    {
        private static readonly CASpacePurpose[] FacilityPurposes =
        {
            CASpacePurpose.Kitchen,
            CASpacePurpose.Freezer,
            CASpacePurpose.Storage,
            CASpacePurpose.Utility,
            CASpacePurpose.Defense
        };

        private sealed class CAProgramMapEvidence
        {
            internal readonly HashSet<IntVec3> habitation =
                new HashSet<IntVec3>();
            internal readonly HashSet<IntVec3> kitchens =
                new HashSet<IntVec3>();
            internal readonly HashSet<IntVec3> defenses =
                new HashSet<IntVec3>();
            internal readonly List<IntVec3> turrets =
                new List<IntVec3>();
            internal readonly List<IntVec3> exteriorIngresses =
                new List<IntVec3>();
            internal readonly List<IntVec3> poweredCells =
                new List<IntVec3>();
            internal int defensePrograms;
            internal int planningContextRevision;
        }

        private sealed class CAProgramFacilityEvidence
        {
            internal CASpaceProgram program;
            internal IntVec3 anchor = IntVec3.Invalid;
            internal int declaredCells;
            internal int uniqueCells;
            internal int inBoundsCells;
            internal int homeCells;
            internal int enclosedCells;
            internal int roofedCells;
            internal int naturalRoofCells;
            internal int excavatedNaturalRoofCells;
            internal int mineableFootprintCells;
            internal int adjacentMineableSeamCells;
            internal int standableCells;
            internal int frozenCells;
            internal int rooms;
            internal int stockpileCells;
            internal int stockpileZones;
            internal int playerBuildings;
            internal int storageBuildings;
            internal int beds;
            internal int foodPreparationTables;
            internal int usableFoodPreparationTables;
            internal int temperatureControls;
            internal int coolingControls;
            internal int heatingControls;
            internal int otherTemperatureControls;
            internal int poweredTemperatureControls;
            internal int operatingTemperatureControls;
            internal int poweredCells;
            internal int powerTraders;
            internal int poweredOnTraders;
            internal int powerProducers;
            internal int batteries;
            internal int turrets;
            internal int blueprints;
            internal int frames;
            internal int itemStacks;
            internal int itemUnits;
            internal int roofSensitiveUnits;
            internal int rottableUnits;
            internal int dissolutionSensitiveUnits;
            internal int hazardousUnits;
            internal int highlyFlammableUnits;
            internal readonly Dictionary<string, int> itemDefinitionUnits =
                new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly HashSet<string> itemProfileSignals =
                new HashSet<string>(StringComparer.Ordinal);
            internal int exteriorIngressRelations;
            internal int nearestPower = int.MaxValue;
            internal int nearestPowerFromAnchor = int.MaxValue;
            internal int nearestHabitation = int.MaxValue;
            internal int nearestHabitationFromAnchor = int.MaxValue;
            internal int nearestKitchen = int.MaxValue;
            internal int nearestKitchenFromAnchor = int.MaxValue;
            internal int nearestExteriorIngress = int.MaxValue;
            internal int nearestExteriorIngressFromAnchor = int.MaxValue;
            internal int nearestTurret = int.MaxValue;
            internal int nearestTurretFromAnchor = int.MaxValue;
            internal int nearestDefenseProgram = int.MaxValue;
            internal int nearestDefenseProgramFromAnchor = int.MaxValue;
            internal int reachableCells;
            internal float minimumTemperature = float.MaxValue;
            internal float maximumTemperature = float.MinValue;
            internal float minimumTemperatureTarget = float.MaxValue;
            internal float maximumTemperatureTarget = float.MinValue;
            internal string nativeRoles = "none";
            internal string nativeStockpilePolicies = "none";
            internal string cultureEvidence = "none observed";
        }

        // Evaluates only existing player-authored facility programs. The
        // authored CASpaceProgram footprint is the target; this path never
        // paints cells, changes a program, chooses a substitute room, or
        // authorizes work.
        internal static string EvaluatePlayerFacilityPrograms(Map map)
        {
            if (map == null)
                return "[CA] facility requirements: no current map";

            PlannedUseMapComponent plannedUse = PlannedUseMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> programs =
                plannedUse?.ProgramsForObservation;
            if (programs == null)
                return "[CA] facility requirements: no space-program component";

            List<CASpaceProgram> targets = programs
                .Where(program => program != null
                    && program.author == CASpaceAuthor.Player
                    && FacilityPurposes.Contains(program.purpose))
                .OrderBy(program => program.id)
                .ToList();
            int pawnAuthoredTargets = programs.Count(program => program != null
                && program.author == CASpaceAuthor.Pawn
                && FacilityPurposes.Contains(program.purpose));
            int otherPrograms = programs.Count - targets.Count
                - pawnAuthoredTargets;
            CAProgramMapEvidence mapEvidence = InspectProgramMap(map, programs);

            var builder = new StringBuilder();
            builder.Append("[CA] facility requirements: read-only; existing "
                    + "player-authored target programs ")
                .Append(targets.Count).Append("; pawn-authored target programs ")
                .Append(pawnAuthoredTargets).Append(" ignored; other programs ")
                .Append(otherPrograms).AppendLine()
                .AppendLine("  authority boundary: each evaluation reads the "
                    + "exact existing authored program footprint. It creates "
                    + "no program, "
                    + "selects no substitute room, paints no cell, and enables "
                    + "no construction or inventory mutation.")
                .AppendLine("  interpretation boundary: loss, hazard, services, "
                    + "logistics, habitation, defensive topology, footprint, "
                    + "expansion, present use, culture, and construction stage "
                    + "remain separate axes. Present readiness is reported "
                    + "separately from future site potential; neither is a "
                    + "universal facility score.");

            if (targets.Count == 0)
            {
                builder.Append("  no existing player-authored Kitchen, Freezer, "
                    + "Storage, Utility, or Defense footprint is available to "
                    + "evaluate; receipt refused to invent one");
            }
            else
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    CAProgramFacilityEvidence evidence = InspectProgram(map,
                        targets[i], mapEvidence);
                    builder.AppendLine().Append(ProgramReceipt(evidence,
                        mapEvidence));
                }
            }

            string receipt = builder.ToString();
            Log.Message(receipt);
            return receipt;
        }

        internal static string Census(Map map)
        {
            if (map == null) return "[CA] facility siting: no current map";

            var habitation = new HashSet<IntVec3>();
            var kitchens = new HashSet<IntVec3>();
            var defenses = new HashSet<IntVec3>();
            PlannedUseMapComponent plannedUse = PlannedUseMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> observedPrograms =
                plannedUse?.ProgramsForObservation
                ?? Array.Empty<CASpaceProgram>();
            if (plannedUse != null)
            {
                IReadOnlyList<CASpaceProgram> programs = observedPrograms;
                for (int i = 0; i < programs.Count; i++)
                {
                    CASpaceProgram program = programs[i];
                    if (program?.cells == null) continue;
                    HashSet<IntVec3> target = null;
                    if (program.purpose == CASpacePurpose.Barracks
                        || program.purpose == CASpacePurpose.Bedroom)
                        target = habitation;
                    else if (program.purpose == CASpacePurpose.Kitchen)
                        target = kitchens;
                    else if (program.purpose == CASpacePurpose.Defense)
                        target = defenses;
                    if (target == null) continue;
                    for (int c = 0; c < program.cells.Count; c++)
                        if (program.cells[c].InBounds(map))
                            target.Add(program.cells[c]);
                }
            }

            var turrets = new List<IntVec3>();
            var exteriorIngresses = new List<IntVec3>();
            int atomizers = 0;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null || !building.Spawned
                    || building.Faction != Faction.OfPlayer) continue;
                Building_Bed bed = building as Building_Bed;
                if (bed != null) habitation.Add(bed.Position);
                if (building.def.building?.IsTurret == true)
                    turrets.Add(building.Position);
                Building_Door door = building as Building_Door;
                if (door != null && CASettlementTopology.IsExteriorIngress(
                    map, door.Position))
                    exteriorIngresses.Add(door.Position);
                if (building.def.defName == "WastepackAtomizer") atomizers++;
            }

            var poweredCells = new List<IntVec3>();
            foreach (IntVec3 cell in map.AllCells)
                if (map.powerNetGrid.TransmittedPowerNetAt(cell) != null)
                    poweredCells.Add(cell);

            ThingDef wastepackDef = DefDatabase<ThingDef>
                .GetNamedSilentFail("Wastepack");
            int wastepackStacks = 0;
            int wastepackUnits = 0;
            int frozenWastepackUnits = 0;
            int exposedWastepackUnits = 0;
            if (wastepackDef != null)
            {
                List<Thing> wastepacks = map.listerThings
                    .ThingsOfDef(wastepackDef);
                for (int i = 0; i < wastepacks.Count; i++)
                {
                    Thing wastepack = wastepacks[i];
                    if (wastepack == null || !wastepack.Spawned) continue;
                    wastepackStacks++;
                    wastepackUnits += wastepack.stackCount;
                    if (GenTemperature.GetTemperatureForCell(
                        wastepack.Position, map) <= 0f)
                        frozenWastepackUnits += wastepack.stackCount;
                    if (!wastepack.Position.Roofed(map))
                        exposedWastepackUnits += wastepack.stackCount;
                }
            }

            var evidence = new List<CAFacilityRoomEvidence>();
            IReadOnlyList<Room> rooms = map.regionGrid.AllRooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (room == null || !room.ProperRoom || room.IsDoorway
                    || room.PsychologicallyOutdoors || room.Fogged) continue;
                CAFacilityRoomEvidence roomEvidence = InspectRoom(map, room,
                    observedPrograms, habitation, kitchens, defenses, turrets,
                    exteriorIngresses, poweredCells, wastepackDef);
                if (roomEvidence.claimedCells == 0
                    && roomEvidence.playerBuildings == 0
                    && roomEvidence.programs.Count == 0
                    && roomEvidence.stockpileCells == 0) continue;
                evidence.Add(roomEvidence);
            }
            evidence.Sort((left, right) =>
            {
                int x = left.anchor.x.CompareTo(right.anchor.x);
                return x != 0 ? x : left.anchor.z.CompareTo(right.anchor.z);
            });

            string kitchenComparison = KitchenComparison(evidence);
            string protectedStoreComparison = ProtectedStoreComparison(evidence,
                defenses.Count > 0);
            string toxicComparison = ToxicComparison(evidence, wastepackUnits,
                habitation.Count > 0, defenses.Count > 0);

            var builder = new StringBuilder();
            builder.Append("[CA] facility siting census: read-only; candidate rooms ")
                .Append(evidence.Count).Append("; player turrets ")
                .Append(turrets.Count).Append("; Defense program cells ")
                .Append(defenses.Count).Append("; exterior ingress doors ")
                .Append(exteriorIngresses.Count).AppendLine()
                .Append("  toxic-waste state: ").Append(wastepackStacks)
                .Append(" stack(s)/").Append(wastepackUnits)
                .Append(" unit(s), frozen ").Append(frozenWastepackUnits)
                .Append(", unroofed ").Append(exposedWastepackUnits)
                .Append(", atomizers ").Append(atomizers).AppendLine()
                .AppendLine("  interpretation boundary: room footprint, "
                    + "mineable boundary, refrigeration, services, access, "
                    + "habitation distance, and defensive evidence remain "
                    + "separate facts. Comparative candidates are advisory; no "
                    + "beachhead or facility purpose is authored, and no "
                    + "relocation or faction offloading strategy is selected.")
                .AppendLine(kitchenComparison)
                .AppendLine(protectedStoreComparison)
                .AppendLine(toxicComparison);
            if (evidence.Count == 0)
                builder.Append("  no claimed or programmed proper rooms observed");
            else
                for (int i = 0; i < evidence.Count; i++)
                {
                    if (i > 0) builder.AppendLine();
                    builder.Append(evidence[i].Receipt());
                }
            string receipt = builder.ToString();
            Log.Message(receipt);
            return receipt;
        }

        private static string KitchenComparison(
            List<CAFacilityRoomEvidence> evidence)
        {
            List<CAFacilityRoomEvidence> viable = evidence
                .Where(candidate => BasicFacilitySite(candidate)
                    && candidate.cells >= 16
                    && !ResidentialConflict(candidate))
                .OrderBy(candidate => ProgramMatchRank(candidate,
                    CASpacePurpose.Kitchen))
                .ThenBy(candidate => candidate.NaturalRoofShare)
                .ThenBy(candidate => ExistingUseBurden(candidate,
                    CASpacePurpose.Kitchen))
                .ThenBy(candidate => candidate.mineableBoundaryCells)
                .ThenBy(candidate => Math.Abs(candidate.cells - 48))
                .ThenBy(candidate => candidate.nearestPower)
                .ThenBy(candidate => candidate.anchor.x)
                .ThenBy(candidate => candidate.anchor.z)
                .ToList();
            if (viable.Count == 0)
                return "  Kitchen comparison: no room passes enclosure, reach, "
                    + "grid-within-8, minimum-footprint, and nonresidential gates.";
            CAFacilityRoomEvidence selected = viable[0];
            return "  Kitchen comparison: advisory room #" + selected.room.ID
                + " at " + selected.anchor + " from " + viable.Count
                + " viable room(s); priority was authored Kitchen match, lower "
                + "natural-roof opportunity cost, less conflicting present use, "
                + "less mineable-boundary consumption, 48-cell scale fit, then "
                + "grid distance. Evidence: natural roof "
                + Percent(selected.NaturalRoofShare) + ", present-use burden "
                + ExistingUseBurden(selected, CASpacePurpose.Kitchen)
                + ", mineable boundary " + selected.mineableBoundaryCells
                + ", cells " + selected.cells + ", nearest grid "
                + selected.nearestPower + ".";
        }

        private static string ProtectedStoreComparison(
            List<CAFacilityRoomEvidence> evidence, bool defenseProgramObserved)
        {
            List<CAFacilityRoomEvidence> viable = evidence
                .Where(candidate => BasicFacilitySite(candidate)
                    && candidate.cells >= 16
                    && !ResidentialConflict(candidate))
                .OrderBy(candidate => ProgramMatchRank(candidate,
                    CASpacePurpose.Freezer, CASpacePurpose.Storage))
                .ThenByDescending(candidate => candidate.NaturalRoofShare)
                .ThenByDescending(candidate => KnownDistance(
                    candidate.nearestExteriorIngress))
                .ThenBy(candidate => candidate.nearestPower)
                .ThenBy(candidate => ExistingUseBurden(candidate,
                    CASpacePurpose.Freezer, CASpacePurpose.Storage))
                .ThenBy(candidate => Math.Abs(candidate.cells - 80))
                .ThenBy(candidate => candidate.anchor.x)
                .ThenBy(candidate => candidate.anchor.z)
                .ToList();
            if (viable.Count == 0)
                return "  Protected-store comparison: no room passes enclosure, "
                    + "reach, grid-within-8, minimum-footprint, and "
                    + "nonresidential gates.";
            CAFacilityRoomEvidence selected = viable[0];
            string defensiveBoundary = defenseProgramObserved
                ? "Defense-program distance remains evidence, not a directional "
                    + "behind-the-line claim."
                : "No Defense program exists; turret distance alone cannot prove "
                    + "a beachhead or that the room is behind it.";
            return "  Protected-store comparison: advisory room #"
                + selected.room.ID + " at " + selected.anchor + " from "
                + viable.Count + " viable room(s); priority was authored "
                + "Freezer/Storage match, greater natural-roof share, greater "
                + "observed ingress distance, grid distance, present-use burden, "
                + "then 80-cell scale fit. Evidence: natural roof "
                + Percent(selected.NaturalRoofShare) + ", ingress "
                + Distance(selected.nearestExteriorIngress) + ", nearest grid "
                + selected.nearestPower + ", present-use burden "
                + ExistingUseBurden(selected, CASpacePurpose.Freezer,
                    CASpacePurpose.Storage) + ". " + defensiveBoundary;
        }

        private static string ToxicComparison(
            List<CAFacilityRoomEvidence> evidence, int wastepackUnits,
            bool habitationObserved, bool defenseProgramObserved)
        {
            if (wastepackUnits <= 0)
                return "  Toxic-waste comparison: no current waste inventory, so "
                    + "the census selects no staging or offloading objective.";
            List<CAFacilityRoomEvidence> frozen = evidence
                .Where(candidate => BasicFacilitySite(candidate)
                    && candidate.RoofedShare >= 0.999f
                    && candidate.temperature <= 0f
                    && !ResidentialConflict(candidate))
                .OrderByDescending(candidate => KnownDistance(
                    candidate.nearestHabitation))
                .ThenBy(candidate => candidate.nearestPower)
                .ThenBy(candidate => ExistingUseBurden(candidate,
                    CASpacePurpose.Freezer, CASpacePurpose.Storage,
                    CASpacePurpose.Utility))
                .ToList();
            if (frozen.Count == 0)
                return "  Toxic-waste comparison: waste exists, but no fully "
                    + "roofed, reachable, grid-near, currently frozen, "
                    + "nonresidential room is ready for staging.";
            CAFacilityRoomEvidence selected = frozen[0];
            string maturity = !habitationObserved
                ? "Habitation evidence is absent, so mature detachment cannot be "
                    + "assessed."
                : (!defenseProgramObserved
                    ? "Defense-program evidence is absent, so mature facility "
                        + "protection cannot be assessed."
                    : "Detachment and defensive relations require their separate "
                        + "threshold receipt before mature status.");
            return "  Toxic-waste comparison: operational staging candidate room #"
                + selected.room.ID + " at " + selected.anchor + "; temperature "
                + selected.temperature.ToString("F1") + " C, habitation "
                + Distance(selected.nearestHabitation) + ", grid "
                + selected.nearestPower + ". " + maturity;
        }

        private static bool BasicFacilitySite(CAFacilityRoomEvidence candidate)
        {
            return candidate != null && candidate.RoofedShare >= 0.95f
                && candidate.reachable && candidate.nearestPower <= 8;
        }

        private static bool ResidentialConflict(CAFacilityRoomEvidence candidate)
        {
            return candidate.beds > 0 || candidate.programs.Any(program =>
                program.purpose == CASpacePurpose.Barracks
                || program.purpose == CASpacePurpose.Bedroom);
        }

        private static int ProgramMatchRank(CAFacilityRoomEvidence candidate,
            params CASpacePurpose[] purposes)
        {
            bool match = candidate.programs.Any(program =>
                purposes.Contains(program.purpose));
            if (match) return 0;
            return candidate.programs.Count == 0 ? 1 : 2;
        }

        private static int ExistingUseBurden(CAFacilityRoomEvidence candidate,
            params CASpacePurpose[] compatiblePurposes)
        {
            int incompatiblePrograms = candidate.programs.Count(program =>
                !compatiblePurposes.Contains(program.purpose));
            return incompatiblePrograms * 1000 + candidate.stockpileCells * 4
                + candidate.playerBuildings;
        }

        private static int KnownDistance(int distance)
        {
            return distance == int.MaxValue ? -1 : distance;
        }

        private static string Distance(int distance)
        {
            return distance == int.MaxValue ? "none observed"
                : distance + " cells";
        }

        private static string Percent(float value)
        {
            return (value * 100f).ToString("F0") + "%";
        }

        private static CAProgramMapEvidence InspectProgramMap(Map map,
            IReadOnlyList<CASpaceProgram> programs)
        {
            var evidence = new CAProgramMapEvidence();
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program?.cells == null) continue;
                HashSet<IntVec3> target = null;
                if (program.purpose == CASpacePurpose.Barracks
                    || program.purpose == CASpacePurpose.Bedroom)
                    target = evidence.habitation;
                else if (program.author != CASpaceAuthor.Player)
                    continue;
                else if (program.purpose == CASpacePurpose.Kitchen)
                    target = evidence.kitchens;
                else if (program.purpose == CASpacePurpose.Defense)
                {
                    target = evidence.defenses;
                    evidence.defensePrograms++;
                }
                if (target == null) continue;
                for (int c = 0; c < program.cells.Count; c++)
                    if (program.cells[c].InBounds(map))
                        target.Add(program.cells[c]);
            }

            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null || !building.Spawned
                    || building.Faction != Faction.OfPlayer) continue;
                if (building is Building_Bed)
                    evidence.habitation.Add(building.Position);
                if (building.def.building?.IsTurret == true)
                    evidence.turrets.Add(building.Position);
                Building_Door door = building as Building_Door;
                if (door != null && CASettlementTopology.IsExteriorIngress(
                    map, door.Position))
                    evidence.exteriorIngresses.Add(door.Position);
            }

            foreach (IntVec3 cell in map.AllCells)
                if (map.powerNetGrid.TransmittedPowerNetAt(cell) != null)
                    evidence.poweredCells.Add(cell);
            evidence.planningContextRevision =
                CASettlementPlanningContextMapComponent.For(map)?.Revision ?? 0;
            return evidence;
        }

        private static CAProgramFacilityEvidence InspectProgram(Map map,
            CASpaceProgram program, CAProgramMapEvidence mapEvidence)
        {
            var evidence = new CAProgramFacilityEvidence
            {
                program = program,
                declaredCells = program?.cells?.Count ?? 0
            };
            var footprint = new HashSet<IntVec3>();
            if (program?.cells != null)
                for (int i = 0; i < program.cells.Count; i++)
                    if (program.cells[i].InBounds(map))
                        footprint.Add(program.cells[i]);
            evidence.uniqueCells = program?.cells == null
                ? 0 : new HashSet<IntVec3>(program.cells).Count;
            evidence.inBoundsCells = footprint.Count;
            evidence.anchor = CenterCell(footprint);

            var rooms = new HashSet<Room>();
            var roomRoles = new HashSet<string>(StringComparer.Ordinal);
            var stockpiles = new Dictionary<int, Zone_Stockpile>();
            var things = new HashSet<Thing>();
            var adjacentMineables = new HashSet<IntVec3>();

            foreach (IntVec3 cell in footprint)
            {
                if (map.areaManager.Home[cell]) evidence.homeCells++;
                if (cell.Standable(map)) evidence.standableCells++;
                RoofDef roof = cell.GetRoof(map);
                if (roof != null) evidence.roofedCells++;
                if (roof?.isNatural == true) evidence.naturalRoofCells++;

                Thing mineable = cell.GetFirstMineable(map);
                if (mineable != null) evidence.mineableFootprintCells++;

                Room room = cell.GetRoom(map);
                bool enclosed = room != null && room.ProperRoom
                    && !room.IsDoorway && !room.PsychologicallyOutdoors;
                if (enclosed)
                {
                    evidence.enclosedCells++;
                    rooms.Add(room);
                    roomRoles.Add(room.Role?.defName ?? "none");
                    if (roof?.isNatural == true && mineable == null)
                        evidence.excavatedNaturalRoofCells++;
                }

                float temperature = GenTemperature.GetTemperatureForCell(
                    cell, map);
                evidence.minimumTemperature = Mathf.Min(
                    evidence.minimumTemperature, temperature);
                evidence.maximumTemperature = Mathf.Max(
                    evidence.maximumTemperature, temperature);
                if (temperature <= 0f) evidence.frozenCells++;
                if (map.powerNetGrid.TransmittedPowerNetAt(cell) != null)
                    evidence.poweredCells++;

                Zone_Stockpile stockpile = map.zoneManager.ZoneAt(cell)
                    as Zone_Stockpile;
                if (stockpile != null)
                {
                    evidence.stockpileCells++;
                    stockpiles[stockpile.ID] = stockpile;
                }

                List<Thing> cellThings = cell.GetThingList(map);
                for (int t = 0; t < cellThings.Count; t++)
                    things.Add(cellThings[t]);

                for (int d = 0; d < GenAdj.CardinalDirections.Length; d++)
                {
                    IntVec3 adjacent = cell + GenAdj.CardinalDirections[d];
                    if (!adjacent.InBounds(map)
                        || footprint.Contains(adjacent)) continue;
                    if (adjacent.GetFirstMineable(map) != null)
                        adjacentMineables.Add(adjacent);
                }
            }

            evidence.rooms = rooms.Count;
            evidence.nativeRoles = roomRoles.Count == 0 ? "none"
                : string.Join(", ", roomRoles.OrderBy(role => role,
                    StringComparer.Ordinal).ToArray());
            evidence.stockpileZones = stockpiles.Count;
            evidence.nativeStockpilePolicies = stockpiles.Count == 0 ? "none"
                : string.Join("; ", stockpiles.Values.OrderBy(zone => zone.ID)
                    .Select(zone => "#" + zone.ID + " '" + zone.label
                        + "' priority " + zone.settings.Priority.Label()
                        + ", allowed loaded definitions "
                        + zone.settings.filter.AllowedDefCount).ToArray());
            evidence.adjacentMineableSeamCells = adjacentMineables.Count;

            foreach (Thing thing in things)
            {
                if (thing == null || !thing.Spawned) continue;
                if (thing is Blueprint)
                {
                    evidence.blueprints++;
                    continue;
                }
                if (thing is Frame)
                {
                    evidence.frames++;
                    continue;
                }

                Building building = thing as Building;
                if (building != null && building.Faction == Faction.OfPlayer)
                {
                    evidence.playerBuildings++;
                    if (building is Building_Storage)
                        evidence.storageBuildings++;
                    if (building is Building_Bed) evidence.beds++;
                    if (building.def.building?.IsTurret == true)
                        evidence.turrets++;

                    Building_WorkTable workTable = building
                        as Building_WorkTable;
                    if (workTable != null
                        && IsFoodPreparationTable(workTable.def))
                    {
                        evidence.foodPreparationTables++;
                        if (workTable.CurrentlyUsableForBills())
                            evidence.usableFoodPreparationTables++;
                    }

                    CompPowerTrader power =
                        building.TryGetComp<CompPowerTrader>();
                    if (power != null)
                    {
                        evidence.powerTraders++;
                        if (power.PowerOn) evidence.poweredOnTraders++;
                        if (power.PowerOutput > 0f)
                            evidence.powerProducers++;
                    }
                    if (building.TryGetComp<CompPowerBattery>() != null)
                        evidence.batteries++;
                    continue;
                }

                if (thing.def.category != ThingCategory.Item) continue;
                int units = Mathf.Max(1, thing.stackCount);
                evidence.itemStacks++;
                evidence.itemUnits += units;
                CAStorageProfile profile = CAStorageProfile.For(thing.def);
                int definitionUnits;
                evidence.itemDefinitionUnits.TryGetValue(thing.def.defName,
                    out definitionUnits);
                evidence.itemDefinitionUnits[thing.def.defName] = definitionUnits
                    + units;
                if (profile.roofPreferred)
                {
                    evidence.roofSensitiveUnits += units;
                    evidence.itemProfileSignals.Add("roof-exposure-sensitive");
                }
                if (profile.rottable)
                {
                    evidence.rottableUnits += units;
                    evidence.itemProfileSignals.Add("rottable");
                }
                if (profile.dissolutionSensitive)
                {
                    evidence.dissolutionSensitiveUnits += units;
                    evidence.itemProfileSignals.Add("dissolution-sensitive");
                }
                if (profile.hazardous)
                {
                    evidence.hazardousUnits += units;
                    evidence.itemProfileSignals.Add("hazard-sensitive");
                }
                if (profile.flammability >= 0.8f)
                {
                    evidence.highlyFlammableUnits += units;
                    evidence.itemProfileSignals.Add("high-flammability");
                }
            }

            InspectServingTemperatureControls(map, footprint, rooms, evidence);

            var otherKitchens = new HashSet<IntVec3>(mapEvidence.kitchens);
            var otherDefenses = new HashSet<IntVec3>(mapEvidence.defenses);
            if (program.purpose == CASpacePurpose.Kitchen)
                otherKitchens.ExceptWith(footprint);
            if (program.purpose == CASpacePurpose.Defense)
                otherDefenses.ExceptWith(footprint);

            evidence.nearestPower = NearestDistance(footprint,
                mapEvidence.poweredCells);
            evidence.nearestPowerFromAnchor = NearestDistance(evidence.anchor,
                mapEvidence.poweredCells);
            evidence.nearestHabitation = NearestDistance(footprint,
                mapEvidence.habitation);
            evidence.nearestHabitationFromAnchor = NearestDistance(
                evidence.anchor, mapEvidence.habitation);
            evidence.nearestKitchen = NearestDistance(footprint,
                otherKitchens);
            evidence.nearestKitchenFromAnchor = NearestDistance(
                evidence.anchor, otherKitchens);
            evidence.nearestExteriorIngress = NearestDistance(footprint,
                mapEvidence.exteriorIngresses);
            evidence.nearestExteriorIngressFromAnchor = NearestDistance(
                evidence.anchor, mapEvidence.exteriorIngresses);
            evidence.nearestTurret = NearestDistance(footprint,
                mapEvidence.turrets);
            evidence.nearestTurretFromAnchor = NearestDistance(
                evidence.anchor, mapEvidence.turrets);
            evidence.nearestDefenseProgram = NearestDistance(footprint,
                otherDefenses);
            evidence.nearestDefenseProgramFromAnchor = NearestDistance(
                evidence.anchor, otherDefenses);
            for (int i = 0; i < mapEvidence.exteriorIngresses.Count; i++)
                if (DistanceTo(footprint,
                    mapEvidence.exteriorIngresses[i]) <= 1)
                    evidence.exteriorIngressRelations++;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            foreach (IntVec3 cell in footprint)
            {
                if (!cell.Standable(map)) continue;
                for (int i = 0; i < colonists.Count; i++)
                    if (colonists[i].CanReach(cell, PathEndMode.OnCell,
                        Danger.Some))
                    {
                        evidence.reachableCells++;
                        break;
                    }
            }
            evidence.cultureEvidence = CultureEvidence(map, program);
            return evidence;
        }

        private static void InspectServingTemperatureControls(Map map,
            HashSet<IntVec3> footprint, HashSet<Room> rooms,
            CAProgramFacilityEvidence evidence)
        {
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null || !building.Spawned
                    || building.Faction != Faction.OfPlayer) continue;
                CompTempControl control =
                    building.TryGetComp<CompTempControl>();
                if (control == null || !TemperatureControlServesProgram(
                    building, map, footprint, rooms)) continue;

                evidence.temperatureControls++;
                if (building is Building_Cooler)
                    evidence.coolingControls++;
                else if (building is Building_Heater)
                    evidence.heatingControls++;
                else evidence.otherTemperatureControls++;

                CompPowerTrader power = building.TryGetComp<CompPowerTrader>();
                bool powered = power?.PowerOn == true;
                if (powered) evidence.poweredTemperatureControls++;
                if (powered && control.operatingAtHighPower)
                    evidence.operatingTemperatureControls++;
                evidence.minimumTemperatureTarget = Mathf.Min(
                    evidence.minimumTemperatureTarget,
                    control.TargetTemperature);
                evidence.maximumTemperatureTarget = Mathf.Max(
                    evidence.maximumTemperatureTarget,
                    control.TargetTemperature);
            }
        }

        private static bool TemperatureControlServesProgram(
            Building building, Map map, HashSet<IntVec3> footprint,
            HashSet<Room> rooms)
        {
            if (building is Building_Cooler)
            {
                IntVec3 coldCell = building.Position
                    + IntVec3.South.RotatedBy(building.Rotation);
                if (!coldCell.InBounds(map)) return false;
                Room coldRoom = coldCell.GetRoom(map);
                return footprint.Contains(coldCell)
                    || (coldRoom != null && rooms.Contains(coldRoom));
            }

            if (building is Building_Heater)
            {
                Room heatedRoom = building.Position.GetRoom(map);
                return footprint.Contains(building.Position)
                    || (heatedRoom != null && rooms.Contains(heatedRoom));
            }

            foreach (IntVec3 cell in building.OccupiedRect())
                if (footprint.Contains(cell)) return true;
            Room rootRoom = building.Position.GetRoom(map);
            return rootRoom != null && rooms.Contains(rootRoom);
        }

        private static string ProgramReceipt(CAProgramFacilityEvidence evidence,
            CAProgramMapEvidence mapEvidence)
        {
            CASpaceProgram program = evidence.program;
            string label = program.label.NullOrEmpty()
                ? CASpacePurposeInfo.Label(program.purpose) : program.label;
            string duplicateState = evidence.declaredCells == evidence.uniqueCells
                ? "none" : (evidence.declaredCells - evidence.uniqueCells)
                    + " duplicate declaration(s)";
            return "  program " + label + " #" + program.id + " ("
                + CASpacePurposeInfo.Label(program.purpose)
                + ", player-authored) at " + evidence.anchor
                + "\n    loss criticality: " + LossCriticality(evidence)
                + "\n    hazard: actual footprint contents are "
                + evidence.itemStacks + " item stack(s)/" + evidence.itemUnits
                + " unit(s); " + ItemComposition(evidence)
                + "; roof-exposure-sensitive " + evidence.roofSensitiveUnits
                + ", rottable " + evidence.rottableUnits
                + ", dissolution-sensitive "
                + evidence.dissolutionSensitiveUnits + ", hazardous "
                + evidence.hazardousUnits + ", highly flammable "
                + evidence.highlyFlammableUnits + ". Purpose alone adds no "
                + "unobserved inventory hazard, and mixed co-storage is not "
                + "itself a purpose violation."
                + "\n    service/grid access: transmitted grid in footprint "
                + evidence.poweredCells + " cell(s), nearest grid from "
                + "footprint edge " + Distance(evidence.nearestPower)
                + " (representative anchor "
                + Distance(evidence.nearestPowerFromAnchor)
                + "); power traders "
                + evidence.powerTraders + " (on "
                + evidence.poweredOnTraders + ", currently exporting "
                + evidence.powerProducers + "), batteries "
                + evidence.batteries + ", room-serving temperature controls "
                + evidence.temperatureControls + " (coolers "
                + evidence.coolingControls + ", heaters "
                + evidence.heatingControls + ", other "
                + evidence.otherTemperatureControls + ", powered "
                + evidence.poweredTemperatureControls + ", operating-high "
                + evidence.operatingTemperatureControls + ", "
                + TemperatureTargetRange(evidence) + ")."
                + "\n    logistics: reachable standable cells "
                + evidence.reachableCells + "/" + evidence.standableCells
                + " by at least one spawned free colonist"
                + ", exterior ingress from footprint edge "
                + Distance(evidence.nearestExteriorIngress)
                + " (representative anchor "
                + Distance(evidence.nearestExteriorIngressFromAnchor)
                + "), other Kitchen program from footprint edge "
                + Distance(evidence.nearestKitchen)
                + " (representative anchor "
                + Distance(evidence.nearestKitchenFromAnchor) + ")"
                + ", native stockpile footprint " + evidence.stockpileCells
                + " cell(s) in " + evidence.stockpileZones + " zone(s), "
                + "storage buildings " + evidence.storageBuildings + "."
                + "\n    native storage authority: "
                + evidence.nativeStockpilePolicies + ". Native stockpile "
                + "filters and priority determine item acceptance and ordering; "
                + "the facility-program label does not duplicate them."
                + "\n    habitation separation: nearest authored sleep "
                + "footprint or existing bed from footprint edge "
                + Distance(evidence.nearestHabitation)
                + " (representative anchor "
                + Distance(evidence.nearestHabitationFromAnchor)
                + "); beds inside "
                + evidence.beds + "."
                + "\n    defensive topology: "
                + DefensiveTopology(evidence, mapEvidence)
                + "\n    bounded footprint: authored cells "
                + evidence.declaredCells + ", unique " + evidence.uniqueCells
                + ", in bounds " + evidence.inBoundsCells + ", duplicate state "
                + duplicateState + ", enclosed cells "
                + evidence.enclosedCells + ", proper rooms " + evidence.rooms
                + " (native roles " + evidence.nativeRoles + ")."
                + "\n    expansion opportunity: excavated enclosed natural-"
                + "roof cells " + evidence.excavatedNaturalRoofCells
                + "; unexcavated mineable cells inside the authored footprint "
                + evidence.mineableFootprintCells
                + "; adjacent mineable seam cells outside it "
                + evidence.adjacentMineableSeamCells + "."
                + "\n    current-use state: buildings "
                + evidence.playerBuildings + " (food-preparation "
                + evidence.foodPreparationTables + ", storage "
                + evidence.storageBuildings + ", beds " + evidence.beds
                + ", turrets " + evidence.turrets + "), stockpile cells "
                + evidence.stockpileCells + ", item units "
                + evidence.itemUnits + ". These are occupation and possible "
                + "transition-burden observations, not proof of conflict or "
                + "misuse; transition or eviction is not authorized."
                + "\n    culture: planning style "
                + CASpacePurposeInfo.StyleLabel(program.style)
                + "; current " + evidence.cultureEvidence
                + "; observed settlement-context revision "
                + mapEvidence.planningContextRevision
                + ". A facility-specific cultural constraint is not yet "
                + "authored."
                + "\n    construction stage: non-mineable footprint cells "
                + (evidence.inBoundsCells - evidence.mineableFootprintCells)
                + ", standable " + evidence.standableCells + ", roofed "
                + evidence.roofedCells + " (natural "
                + evidence.naturalRoofCells + "), Home "
                + evidence.homeCells + ", completed player buildings "
                + evidence.playerBuildings + ", blueprints "
                + evidence.blueprints + ", frames " + evidence.frames
                + ". This is the local program snapshot, not a colony-wide "
                + "maturity verdict; developed systems elsewhere do not make "
                + "this facility complete."
                + "\n    present readiness: " + PresentReadiness(evidence)
                + "\n    future site potential: " + FuturePotential(evidence)
                + " No construction consumer is enabled.";
        }

        private static string ItemComposition(
            CAProgramFacilityEvidence evidence)
        {
            if (evidence.itemDefinitionUnits.Count == 0)
                return "loaded definitions 0; profile signals none";
            const int ReceiptLimit = 12;
            List<KeyValuePair<string, int>> definitions = evidence
                .itemDefinitionUnits.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal).ToList();
            string visible = string.Join(", ", definitions.Take(ReceiptLimit)
                .Select(pair => pair.Key + " " + pair.Value).ToArray());
            if (definitions.Count > ReceiptLimit)
                visible += ", +" + (definitions.Count - ReceiptLimit) + " more";
            string signals = evidence.itemProfileSignals.Count == 0 ? "none"
                : string.Join(", ", evidence.itemProfileSignals.OrderBy(signal =>
                    signal, StringComparer.Ordinal).ToArray());
            return "loaded definitions " + definitions.Count + " [" + visible
                + "]; non-exclusive profile signals "
                + evidence.itemProfileSignals.Count + " [" + signals + "]";
        }

        private static string LossCriticality(
            CAProgramFacilityEvidence evidence)
        {
            string realized = "realized burden is " + evidence.playerBuildings
                + " building(s) and " + evidence.itemUnits + " item unit(s)";
            switch (evidence.program.purpose)
            {
                case CASpacePurpose.Kitchen:
                    return "service continuity and realized replacement burden; "
                        + "natural-roof protection is not a purpose gate. An "
                        + "exposed enclosed Kitchen may remain correct when its "
                        + "loss costs less than consuming protected space; "
                        + realized + ".";
                case CASpacePurpose.Freezer:
                    return "temperature-management continuity and the actual "
                        + "accepted inventory determine loss burden; " + realized
                        + ", including " + evidence.rottableUnits
                        + " rottable and " + evidence.dissolutionSensitiveUnits
                        + " dissolution-sensitive unit(s). Mixed contents do "
                        + "not reduce the authored program to one item type.";
                case CASpacePurpose.Storage:
                    return "the actual accepted contents determine protection "
                        + "burden rather than the Storage label; " + realized
                        + ".";
                case CASpacePurpose.Utility:
                    return "service dependency and cascade burden remain "
                        + "separate from market value; " + realized + ", with "
                        + evidence.powerProducers
                        + " currently exporting power trader(s) and "
                        + evidence.batteries + " battery/batteries.";
                case CASpacePurpose.Defense:
                    return "defensive coverage and access continuity; "
                        + realized + ", with " + evidence.turrets
                        + " turret(s).";
                default:
                    return realized + ".";
            }
        }

        private static string DefensiveTopology(
            CAProgramFacilityEvidence evidence,
            CAProgramMapEvidence mapEvidence)
        {
            string turret = "nearest turret from footprint edge "
                + Distance(evidence.nearestTurret) + " (representative anchor "
                + Distance(evidence.nearestTurretFromAnchor)
                + ") is proximity evidence only";
            if (mapEvidence.defensePrograms == 0)
                return "no authored Defense program exists; " + turret
                    + ". Beachhead and defended-side relations are unavailable.";
            if (evidence.program.purpose == CASpacePurpose.Defense)
            {
                string relation = evidence.exteriorIngressRelations > 0
                    ? "the exact footprint is within one cell of "
                        + evidence.exteriorIngressRelations
                        + " observed exterior ingress(es)"
                    : "the exact footprint has no direct observed exterior-"
                        + "ingress relation";
                return relation + "; " + turret + ". This does not by itself "
                    + "establish a beachhead, coverage, or a defended side.";
            }
            return "nearest other authored Defense footprint from footprint "
                + "edge " + Distance(evidence.nearestDefenseProgram)
                + " (representative anchor "
                + Distance(evidence.nearestDefenseProgramFromAnchor) + "); "
                + turret
                + ". No route-cut or side relation is established, so no "
                + "behind-the-defenses claim is made.";
        }

        private static string PresentReadiness(
            CAProgramFacilityEvidence evidence)
        {
            string envelope = evidence.enclosedCells + "/"
                + evidence.inBoundsCells + " enclosed, "
                + evidence.roofedCells + "/" + evidence.inBoundsCells
                + " roofed, reachable standable cells "
                + evidence.reachableCells + "/" + evidence.standableCells
                + " by at least one spawned free colonist";
            string temperature = TemperatureRange(evidence) + ", frozen cells "
                + evidence.frozenCells + "/" + evidence.inBoundsCells;
            switch (evidence.program.purpose)
            {
                case CASpacePurpose.Kitchen:
                    return envelope + "; food-preparation worktables "
                        + evidence.foodPreparationTables + " (currently usable "
                        + evidence.usableFoodPreparationTables + ").";
                case CASpacePurpose.Freezer:
                    return envelope + "; " + temperature
                        + "; room-serving coolers "
                        + evidence.coolingControls + ", powered controllers "
                        + evidence.poweredTemperatureControls
                        + ", operating-high controllers "
                        + evidence.operatingTemperatureControls + ", "
                        + TemperatureTargetRange(evidence) + ". This sampled "
                        + "thermal state is outcome evidence, not a permanent "
                        + "success invariant or content-purity rule.";
                case CASpacePurpose.Storage:
                    return envelope + "; native destinations are "
                        + evidence.stockpileZones + " stockpile zone(s) and "
                        + evidence.storageBuildings + " storage building(s); "
                        + temperature + ". Mixed contents remain valid when "
                        + "the player-authored filters accept them; the sampled "
                        + "thermal state remains separate risk evidence.";
                case CASpacePurpose.Utility:
                    return envelope + "; realized infrastructure is "
                        + evidence.powerTraders + " power trader(s), "
                        + evidence.powerProducers
                        + " currently exporting power trader(s), "
                        + evidence.batteries + " battery/batteries, and "
                        + evidence.temperatureControls
                        + " temperature controller(s).";
                case CASpacePurpose.Defense:
                    return envelope + "; realized defenses are "
                        + evidence.turrets + " turret(s), with "
                        + evidence.exteriorIngressRelations
                        + " direct exterior-ingress relation(s).";
                default:
                    return envelope + ".";
            }
        }

        private static string FuturePotential(
            CAProgramFacilityEvidence evidence)
        {
            string grid = evidence.nearestPower == int.MaxValue
                ? "no transmitted grid observed"
                : (evidence.nearestPower == 0
                    ? "transmitted grid already crosses the footprint"
                    : "transmitted grid lies " + evidence.nearestPower
                        + " cell(s) from the footprint edge"
                        + (evidence.nearestPower <= 8
                            ? " inside the existing 8-cell advisory band"
                            : " outside the existing 8-cell advisory band"));
            string expansion = evidence.adjacentMineableSeamCells > 0
                ? evidence.adjacentMineableSeamCells
                    + " adjacent mineable seam cell(s) remain outside the "
                    + "excavated footprint"
                : "no adjacent mineable seam is observed";
            switch (evidence.program.purpose)
            {
                case CASpacePurpose.Kitchen:
                    return grid + "; " + expansion
                        + ". Protected-space depth is not required merely by "
                        + "the Kitchen label.";
                case CASpacePurpose.Freezer:
                    return grid + "; " + expansion + ". Current cold remains "
                        + TemperatureRange(evidence) + "; grid proximity can "
                        + "support future potential but cannot be reported as "
                        + "present refrigeration or freezing.";
                case CASpacePurpose.Storage:
                    return grid + "; " + expansion
                        + ". Future suitability remains conditional on the "
                        + "actual storage profiles accepted there.";
                case CASpacePurpose.Utility:
                    return grid + "; " + expansion
                        + ". Service potential does not assert a built or "
                        + "operating utility system.";
                case CASpacePurpose.Defense:
                    return grid + "; " + expansion
                        + ". A future defensive relation still requires a "
                        + "direct topology receipt rather than turret proximity.";
                default:
                    return grid + "; " + expansion + ".";
            }
        }

        private static string CultureEvidence(Map map,
            CASpaceProgram program)
        {
            var pawns = new List<Pawn>();
            if (program?.residents != null)
                for (int i = 0; i < program.residents.Count; i++)
                {
                    Pawn pawn = program.residents[i];
                    if (pawn != null && !pawn.Dead) pawns.Add(pawn);
                }
            string source = "program-resident evidence";
            if (pawns.Count == 0)
            {
                pawns.AddRange(map.mapPawns.FreeColonistsSpawned);
                source = "settlement-resident evidence";
            }

            var counts = new Dictionary<Ideo, int>();
            int unaffiliated = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                Ideo ideoligion = pawns[i].Ideo;
                if (ideoligion == null)
                {
                    unaffiliated++;
                    continue;
                }
                int count;
                counts.TryGetValue(ideoligion, out count);
                counts[ideoligion] = count + 1;
            }
            var entries = counts.OrderBy(pair => pair.Key.id)
                .Select(pair => (pair.Key.name.NullOrEmpty()
                        ? "Ideoligion #" + pair.Key.id : pair.Key.name)
                    + " " + pair.Value
                    + (pair.Key.culture == null ? ""
                        : " (culture " + pair.Key.culture.defName + ")"))
                .ToList();
            if (unaffiliated > 0)
                entries.Add("unaffiliated " + unaffiliated);
            return source + " " + (entries.Count == 0 ? "none observed"
                : string.Join(", ", entries.ToArray()));
        }

        private static bool IsFoodPreparationTable(ThingDef def)
        {
            if (def?.designationCategory != DesignationCategoryDefOf.Production)
                return false;
            List<RecipeDef> recipes = def.AllRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe?.products == null) continue;
                for (int p = 0; p < recipe.products.Count; p++)
                {
                    ThingDef product = recipe.products[p].thingDef;
                    if (product?.IsNutritionGivingIngestible == true
                        && product.ingestible.HumanEdible)
                        return true;
                }
            }
            return false;
        }

        private static IntVec3 CenterCell(IEnumerable<IntVec3> cells)
        {
            if (cells == null) return IntVec3.Invalid;
            List<IntVec3> ordered = cells.OrderBy(cell => cell.x)
                .ThenBy(cell => cell.z).ToList();
            if (ordered.Count == 0) return IntVec3.Invalid;
            long x = 0;
            long z = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                x += ordered[i].x;
                z += ordered[i].z;
            }
            var mean = new IntVec3((int)(x / ordered.Count), 0,
                (int)(z / ordered.Count));
            IntVec3 bestCell = ordered[0];
            int bestDistance = bestCell.DistanceToSquared(mean);
            for (int i = 1; i < ordered.Count; i++)
            {
                int distance = ordered[i].DistanceToSquared(mean);
                if (distance >= bestDistance) continue;
                bestCell = ordered[i];
                bestDistance = distance;
            }
            return bestCell;
        }

        private static int NearestDistance(IEnumerable<IntVec3> roots,
            IEnumerable<IntVec3> targets)
        {
            if (roots == null || targets == null) return int.MaxValue;
            List<IntVec3> targetList = targets.Where(target => target.IsValid)
                .ToList();
            if (targetList.Count == 0) return int.MaxValue;
            int best = int.MaxValue;
            foreach (IntVec3 root in roots)
            {
                if (!root.IsValid) continue;
                for (int i = 0; i < targetList.Count; i++)
                {
                    int distance = root.DistanceToSquared(targetList[i]);
                    if (distance < best) best = distance;
                }
            }
            return best == int.MaxValue
                ? int.MaxValue : Mathf.RoundToInt(Mathf.Sqrt(best));
        }

        private static int DistanceTo(IEnumerable<IntVec3> roots,
            IntVec3 target)
        {
            return NearestDistance(roots, new[] { target });
        }

        private static string TemperatureRange(
            CAProgramFacilityEvidence evidence)
        {
            if (evidence.inBoundsCells == 0
                || evidence.minimumTemperature == float.MaxValue)
                return "no current temperature sample";
            return "current temperature "
                + evidence.minimumTemperature.ToString("F1") + " to "
                + evidence.maximumTemperature.ToString("F1") + " C";
        }

        private static string TemperatureTargetRange(
            CAProgramFacilityEvidence evidence)
        {
            if (evidence.temperatureControls == 0
                || evidence.minimumTemperatureTarget == float.MaxValue)
                return "no controller target observed";
            if (Mathf.Approximately(evidence.minimumTemperatureTarget,
                evidence.maximumTemperatureTarget))
                return "controller target "
                    + evidence.minimumTemperatureTarget.ToString("F1")
                    + " C";
            return "controller targets "
                + evidence.minimumTemperatureTarget.ToString("F1") + " to "
                + evidence.maximumTemperatureTarget.ToString("F1") + " C";
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        private static CAFacilityRoomEvidence InspectRoom(Map map, Room room,
            IReadOnlyList<CASpaceProgram> observedPrograms,
            HashSet<IntVec3> habitation, HashSet<IntVec3> kitchens,
            HashSet<IntVec3> defenses, List<IntVec3> turrets,
            List<IntVec3> exteriorIngresses, List<IntVec3> poweredCells,
            ThingDef wastepackDef)
        {
            var evidence = new CAFacilityRoomEvidence
            {
                room = room,
                cells = room.CellCount,
                temperature = room.Temperature
            };
            var cells = new List<IntVec3>();
            var cellSet = new HashSet<IntVec3>();
            var adjacentDoors = new HashSet<IntVec3>();
            var mineableBoundary = new HashSet<IntVec3>();
            var stockpiles = new HashSet<int>();
            var programs = new Dictionary<int, CASpaceProgram>();
            long totalX = 0;
            long totalZ = 0;
            foreach (IntVec3 cell in room.Cells)
            {
                cells.Add(cell);
                cellSet.Add(cell);
                totalX += cell.x;
                totalZ += cell.z;
                if (map.areaManager.Home[cell]) evidence.claimedCells++;
                RoofDef roof = cell.GetRoof(map);
                if (roof != null) evidence.roofedCells++;
                if (roof?.isNatural == true) evidence.naturalRoofCells++;
                if (map.powerNetGrid.TransmittedPowerNetAt(cell) != null)
                    evidence.poweredCells++;
                Zone_Stockpile stockpile = map.zoneManager.ZoneAt(cell)
                    as Zone_Stockpile;
                if (stockpile != null)
                {
                    evidence.stockpileCells++;
                    stockpiles.Add(stockpile.ID);
                }
                if (wastepackDef != null)
                {
                    List<Thing> things = cell.GetThingList(map);
                    for (int t = 0; t < things.Count; t++)
                        if (things[t].def == wastepackDef)
                        {
                            evidence.wastepackStacks++;
                            evidence.wastepackUnits += things[t].stackCount;
                        }
                }
                for (int d = 0; d < GenAdj.CardinalDirections.Length; d++)
                {
                    IntVec3 adjacent = cell + GenAdj.CardinalDirections[d];
                    if (!adjacent.InBounds(map)) continue;
                    if (adjacent.GetDoor(map) != null)
                        adjacentDoors.Add(adjacent);
                    if (adjacent.GetFirstMineable(map) != null)
                        mineableBoundary.Add(adjacent);
                }
            }
            for (int i = 0; i < observedPrograms.Count; i++)
            {
                CASpaceProgram program = observedPrograms[i];
                if (program?.cells == null) continue;
                for (int c = 0; c < program.cells.Count; c++)
                    if (cellSet.Contains(program.cells[c]))
                    {
                        programs[program.id] = program;
                        break;
                    }
            }
            evidence.stockpileZones = stockpiles.Count;
            evidence.programs.AddRange(programs.Values);
            evidence.mineableBoundaryCells = mineableBoundary.Count;
            evidence.doors = adjacentDoors.Count;
            foreach (IntVec3 door in adjacentDoors)
                if (CASettlementTopology.IsExteriorIngress(map, door))
                    evidence.exteriorIngressDoors++;

            if (cells.Count > 0)
            {
                var mean = new IntVec3((int)(totalX / cells.Count), 0,
                    (int)(totalZ / cells.Count));
                evidence.anchor = cells[0];
                int best = evidence.anchor.DistanceToSquared(mean);
                for (int i = 1; i < cells.Count; i++)
                {
                    int distance = cells[i].DistanceToSquared(mean);
                    if (distance >= best) continue;
                    best = distance;
                    evidence.anchor = cells[i];
                }
            }

            List<Thing> roomThings = room.ContainedAndAdjacentThings;
            for (int i = 0; i < roomThings.Count; i++)
            {
                Building building = roomThings[i] as Building;
                if (building == null || !building.Spawned
                    || building.Faction != Faction.OfPlayer
                    || !cellSet.Contains(building.Position)) continue;
                evidence.playerBuildings++;
                if (building is Building_Storage) evidence.storageBuildings++;
                if (building is Building_Bed) evidence.beds++;
                if (building.TryGetComp<CompTempControl>() != null)
                    evidence.temperatureControls++;
            }

            evidence.nearestPower = NearestDistance(evidence.anchor,
                poweredCells);
            evidence.nearestHabitation = NearestDistance(evidence.anchor,
                habitation);
            evidence.nearestKitchen = NearestDistance(evidence.anchor,
                kitchens);
            evidence.nearestExteriorIngress = NearestDistance(evidence.anchor,
                exteriorIngresses);
            evidence.nearestTurret = NearestDistance(evidence.anchor, turrets);
            evidence.nearestDefenseProgram = NearestDistance(evidence.anchor,
                defenses);
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                if (colonists[i].CanReach(evidence.anchor,
                    PathEndMode.OnCell, Danger.Some))
                {
                    evidence.reachable = true;
                    break;
                }
            return evidence;
        }

        private static int NearestDistance(IntVec3 root,
            IEnumerable<IntVec3> targets)
        {
            if (!root.IsValid || targets == null) return int.MaxValue;
            int best = int.MaxValue;
            foreach (IntVec3 target in targets)
            {
                if (!target.IsValid) continue;
                int distance = root.DistanceToSquared(target);
                if (distance < best) best = distance;
            }
            return best == int.MaxValue
                ? int.MaxValue : Mathf.RoundToInt(Mathf.Sqrt(best));
        }
    }
}
