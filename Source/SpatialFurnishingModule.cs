using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Read-only evidence for furnishing work inside existing player authority.
    // Native stockpile policy, native room classification, native RoomStatDefs,
    // and the CompAffectedByFacilities graph remain the schema. This evaluator
    // does not add a second inventory or room-role ontology, persist initiative,
    // place a blueprint, change a zone, or authorize a pawn.
    internal static class CASpatialFurnishingModule
    {
        private sealed class CAPlacementProbe
        {
            internal ThingDef def;
            internal Thing focus;
            internal Room room;
            internal int total;
            internal int accepted;
            internal int outsideAuthority;
            internal int otherZoneConflict;
            internal int doorwayConflict;
            internal int serviceConflict;
            internal int sleepConflict;
            internal int facilityLinkConflict;
            internal int noStuff;
            internal readonly Dictionary<string, int> nativeRejections =
                new Dictionary<string, int>(StringComparer.Ordinal);
            internal IntVec3 exampleCell = IntVec3.Invalid;
            internal Rot4 exampleRotation = Rot4.Invalid;
            internal ThingDef exampleStuff;
            internal int exampleZoneTransitionCells;
            internal int exampleItemStacks;
            internal int exampleItemUnits;
            internal int exampleBuildingCells;
            internal int exampleOpenApproaches;
            internal int exampleWallContacts;
            internal int exampleClearCellsAfter;
            internal int existingSameDef;
            internal int parallelSameDef;
            internal int nearestSameDef = int.MaxValue;
            internal RoomRoleDef predictedRole;
        }

        // One construction-ready storage transition selected from the same
        // native definition and spatial evidence used by the read-only census.
        // The consumer keeps these axes separate and orders them
        // lexicographically; it never turns them into a universal score.
        internal sealed class CAStoragePlan
        {
            internal ThingDef def;
            internal ThingDef stuff;
            internal IntVec3 cell = IntVec3.Invalid;
            internal Rot4 rotation = Rot4.Invalid;
            internal int floorSlots;
            internal int storageSlots;
            internal int capacityDelta;
            internal int itemStacks;
            internal int itemUnits;
            internal int openApproaches;
            internal int wallContacts;
            internal int clearCellsAfter;
            internal int existingSameDef;
            internal int parallelSameDef;
            internal int nearestSameDef = int.MaxValue;
            internal int zoneTransitionCells;

            internal string Receipt()
            {
                return def.defName + " at " + cell + " facing " + rotation
                    + ", stuff " + (stuff?.defName ?? "not required")
                    + "; density [floor slots " + floorSlots
                    + ", storage slots " + storageSlots + ", delta "
                    + capacityDelta + "]; current use [item stacks "
                    + itemStacks + ", units " + itemUnits
                    + "]; Feng Shui [open cardinal approaches "
                    + openApproaches + ", wall contacts " + wallContacts
                    + ", clear negative-space cells after "
                    + clearCellsAfter + ", same-definition pattern "
                    + existingSameDef + ", parallel " + parallelSameDef
                    + ", nearest " + (nearestSameDef == int.MaxValue
                        ? "none" : nearestSameDef + " cells")
                    + "]; native transition [zone cells removed "
                    + zoneTransitionCells + ", source "
                    + SourceReceipt(def) + "]";
            }
        }

        // One construction-ready native facility relationship inside an
        // existing player-authored Bedroom or Barracks. The plan retains the
        // affected beds and every spatial/capability axis that justified it;
        // the initiative component, not this evaluator, owns authorization.
        internal sealed class CARoomFacilityPlan
        {
            internal ThingDef def;
            internal ThingDef stuff;
            internal ThingStyleDef nativeStyle;
            internal Room room;
            internal IntVec3 cell = IntVec3.Invalid;
            internal Rot4 rotation = Rot4.Invalid;
            internal readonly List<int> focusIds = new List<int>();
            internal readonly List<string> focusThingIds = new List<string>();
            internal int relevantBeds;
            internal int missingLinksBefore;
            internal int linksServed;
            internal int residentBedsServed;
            internal int ownedBedsServed;
            internal int openApproaches;
            internal int wallContacts;
            internal int clearCellsAfter;
            internal int circulationCellsBefore;
            internal int circulationCellsAfter;
            internal int reachableSleepBefore;
            internal int reachableSleepAfter;
            internal int protectedTrafficCells;
            internal int establishedBedIntervalCells;
            internal int wallBackedCells;
            internal int usableFaceCells;
            internal string functionalPosture;
            internal string facilityFamilyReceipt;
            internal int existingSameDef;
            internal int parallelSameDef;
            internal int nearestSameDef = int.MaxValue;
            internal int footprintCells;
            internal int costUnits;
            internal float workToBuild;
            internal RoomRoleDef roleBefore;
            internal RoomRoleDef roleAfter;
            internal string costReceipt;
            internal string cultureReceipt;

            internal string Receipt(CASpaceProgram program)
            {
                return def.defName + " at " + cell + " facing " + rotation
                    + ", stuff " + (stuff?.defName ?? "not required")
                    + "; native missing relationship [relevant beds "
                    + relevantBeds + ", missing same-definition links "
                    + missingLinksBefore + ", served by this facility "
                    + linksServed + ", resident-owned "
                    + residentBedsServed + ", otherwise owned "
                    + ownedBedsServed + ", affected Thing IDs "
                    + (focusThingIds.Count == 0 ? "none" : string.Join(
                        ", ", focusThingIds.ToArray())) + ", positive offsets "
                    + FacilityOffsets(def.GetCompProperties<
                        CompProperties_Facility>()) + "]"
                    + "; residents and ownership [program residents "
                    + (program?.residents?.Count(pawn => pawn != null) ?? 0)
                    + ", no bed assignment changed]"
                    + "; Feng Shui [open cardinal approaches "
                    + openApproaches + ", wall contacts " + wallContacts
                    + ", clear negative-space cells after "
                    + clearCellsAfter + ", circulation "
                    + circulationCellsBefore + " -> "
                    + circulationCellsAfter + " reachable cells, sleep "
                    + "approaches " + reachableSleepBefore + " -> "
                    + reachableSleepAfter + ", same-definition pattern "
                    + existingSameDef + ", parallel " + parallelSameDef
                    + ", nearest " + (nearestSameDef == int.MaxValue
                        ? "none" : nearestSameDef + " cells") + "]"
                    + "; functional composition [" + functionalPosture
                    + "; protected traffic-lane cells "
                    + protectedTrafficCells + ", occupied 0; established "
                    + "bed-bank interval cells " + establishedBedIntervalCells
                    + ", occupied 0; wall-backed side cells "
                    + wallBackedCells + ", usable room-facing cells "
                    + usableFaceCells + "; facility family "
                    + facilityFamilyReceipt + "]"
                    + "; native room role ["
                    + (roleBefore?.defName ?? "none") + " -> "
                    + (roleAfter?.defName ?? "none") + "]"
                    + "; capability [footprint " + footprintCells
                    + " cell(s), work " + workToBuild.ToString("F0")
                    + ", spawned material/cost " + costReceipt + "]"
                    + "; culture/style [program "
                    + CASpacePurposeInfo.StyleLabel(program?.style
                        ?? CASpaceStyle.Adaptive) + "; " + cultureReceipt
                    + "]"
                    + "; source " + SourceReceipt(def);
            }
        }

        private sealed class CASpatialField
        {
            internal readonly HashSet<IntVec3> authority;
            internal readonly HashSet<IntVec3> doorwayApproaches =
                new HashSet<IntVec3>();
            internal readonly HashSet<IntVec3> serviceCells =
                new HashSet<IntVec3>();
            internal readonly HashSet<IntVec3> sleepApproaches =
                new HashSet<IntVec3>();
            internal readonly HashSet<IntVec3> trafficLanes =
                new HashSet<IntVec3>();
            internal readonly HashSet<IntVec3> bedBankIntervals =
                new HashSet<IntVec3>();
            internal readonly int clearCells;

            internal CASpatialField(Map map, IEnumerable<IntVec3> cells)
            {
                authority = new HashSet<IntVec3>(cells.Where(cell =>
                    cell.InBounds(map)));
                foreach (IntVec3 cell in authority)
                {
                    for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                    {
                        IntVec3 adjacent = cell + GenAdj.CardinalDirections[i];
                        if (adjacent.InBounds(map)
                            && adjacent.GetDoor(map) != null)
                            doorwayApproaches.Add(cell);
                    }
                }

                var beds = new List<Building_Bed>();
                List<Building> buildings = map.listerBuildings.allBuildingsColonist;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    if (building == null || !building.Spawned
                        || !authority.Contains(building.Position)) continue;
                    if (building.def.HasSingleOrMultipleInteractionCells)
                    {
                        List<IntVec3> interactionCells = building.InteractionCells;
                        for (int j = 0; j < interactionCells.Count; j++)
                            if (authority.Contains(interactionCells[j]))
                                serviceCells.Add(interactionCells[j]);
                    }
                    Building_Bed bed = building as Building_Bed;
                    if (bed == null) continue;
                    beds.Add(bed);
                    List<IntVec3> bedCells = bed.InteractionCells;
                    for (int j = 0; j < bedCells.Count; j++)
                        if (authority.Contains(bedCells[j]))
                            sleepApproaches.Add(bedCells[j]);
                }

                AddProtectedTrafficLanes(map, this);
                AddEstablishedBedBankIntervals(this, beds);
                clearCells = authority.Count(cell => cell.Standable(map)
                    && !HasSpatialObstructionAt(map, cell));
            }
        }

        internal static string Evaluate(Map map)
        {
            if (map == null)
                return "[CA] spatial furnishing opportunities: no current map";

            CASettlementDevelopmentProposal storageFact =
                CASettlementAssetRegistry.BuildDemandFact(
                    CASettlementDemandKind.Storage);
            List<ThingDef> storageCandidates = storageFact
                .Candidates(CASettlementDemandKind.Storage).ToList();
            List<ThingDef> ambientContributors = DefDatabase<ThingDef>
                .AllDefsListForReading.Where(IsAmbientContributor)
                .OrderBy(def => def.defName).ToList();
            int facilityDefinitions = DefDatabase<ThingDef>
                .AllDefsListForReading.Count(def =>
                    def?.GetCompProperties<CompProperties_Facility>() != null);
            int affectedDefinitions = DefDatabase<ThingDef>
                .AllDefsListForReading.Count(def => def?
                    .GetCompProperties<CompProperties_AffectedByFacilities>()
                    != null);

            PlannedUseMapComponent plannedUse = PlannedUseMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> observedPrograms =
                plannedUse?.ProgramsForObservation
                ?? Array.Empty<CASpaceProgram>();
            List<CASpaceProgram> playerPrograms = observedPrograms
                .Where(program => program != null
                    && program.author == CASpaceAuthor.Player)
                .OrderBy(program => program.id).ToList();
            List<Zone_Stockpile> stockpiles = map.zoneManager.AllZones
                .OfType<Zone_Stockpile>().OrderBy(zone => zone.ID).ToList();

            var builder = new StringBuilder();
            builder.Append("[CA] spatial furnishing opportunities: read-only; ")
                .Append("native stockpiles ").Append(stockpiles.Count)
                .Append(", player-authored room programs ")
                .Append(playerPrograms.Count).AppendLine()
                .AppendLine("  authority boundary: Zone_Stockpile owns durable "
                    + "storage policy. CASpaceProgram owns an authored room "
                    + "footprint. Native Room.Role, RoomStatDef, and facility "
                    + "links supply classification and effect evidence; they "
                    + "are not duplicated as CA room or item categories.")
                .AppendLine("  storage-form boundary: floor stockpile and "
                    + "Building_Storage are coexisting native forms of the same "
                    + "storage domain. A shelf may increase density over its "
                    + "bounded footprint while the surrounding stockpile "
                    + "continues to hold bulk output; neither form invalidates "
                    + "the other.")
                .AppendLine("  initiative boundary: native stockpiles and "
                    + "player-authored room programs share one saved three-tier "
                    + "ceiling, default Standard, which may restrict but never "
                    + "raise pawn autonomy. Native stockpiles and authored "
                    + "Barracks expose their controls only with their operative "
                    + "consumers. Authored Bedroom evidence is read-only. This "
                    + "evaluator places no blueprint, changes no zone or "
                    + "program, and authorizes no work.")
                .Append("  loaded native schema: room roles ")
                .Append(DefDatabase<RoomRoleDef>.DefCount).Append(", room stats ")
                .Append(DefDatabase<RoomStatDef>.DefCount)
                .Append(", facility definitions ").Append(facilityDefinitions)
                .Append(", affected definitions ").Append(affectedDefinitions)
                .Append(", capacity-improving storage definitions ")
                .Append(storageCandidates.Count).Append(", native positive-"
                    + "Beauty-stat furnishing definitions ")
                .Append(ambientContributors.Count)
                .AppendLine(". Placement probes use native blueprint acceptance "
                    + "plus separate doorway, service-cell, sleep-approach, "
                    + "current-use, negative-space, pattern, access, technology, "
                    + "and material axes; they do not collapse them into one score.")
                .AppendLine("  beauty boundary: RimWorld's Beauty stat remains "
                    + "engine telemetry. Colonist Awareness does not treat a "
                    + "positive scalar as beauty, infer an ambient furnishing "
                    + "need from it, or optimize placement toward it. Functional "
                    + "and cultural coherence are receipted without producing a "
                    + "replacement beauty score.");
            builder.AppendLine("  content-pack boundary: candidate discovery is "
                + "definition-driven. Core, expansion, and modded furniture "
                + "participate through the same loaded ThingDef class, room "
                + "stat, facility-link, storage-capacity, research, and placement "
                + "contracts; CA contains no hard-coded visual catalog or "
                + "required furniture pack. A visual-only prop with no native "
                + "stat, facility, storage, or construction contract remains "
                + "visual vocabulary; appearance alone is not fabricated as "
                + "utility evidence.");

            builder.AppendLine("  native stockpile authorities:");
            if (stockpiles.Count == 0)
                builder.AppendLine("    none observed; no storage footprint invented");
            for (int i = 0; i < stockpiles.Count; i++)
                AppendStockpile(builder, map, stockpiles[i], storageCandidates);

            builder.AppendLine("  authored room authorities:");
            if (playerPrograms.Count == 0)
                builder.AppendLine("    none observed; no room program invented");
            var programmedRooms = new HashSet<Room>();
            for (int i = 0; i < playerPrograms.Count; i++)
                AppendProgram(builder, map, playerPrograms[i],
                    ambientContributors,
                    programmedRooms);

            List<Room> nativeOnlyRooms = map.regionGrid.AllRooms
                .Where(room => IsObservedNativeRoom(map, room)
                    && !programmedRooms.Contains(room))
                .OrderBy(room => room.ID).ToList();
            builder.Append("  native classified rooms without authored room "
                    + "authority: ").Append(nativeOnlyRooms.Count).AppendLine();
            for (int i = 0; i < nativeOnlyRooms.Count; i++)
            {
                Room room = nativeOnlyRooms[i];
                List<IntVec3> cells = room.Cells.Where(cell =>
                    cell.InBounds(map)).ToList();
                builder.Append("    observed-only ");
                AppendRoomEvidence(builder, map, room, cells, null,
                    ambientContributors, probeCandidates: false);
            }
            if (nativeOnlyRooms.Count == 0)
                builder.AppendLine("    none; absence is explicit");

            return builder.ToString().TrimEnd();
        }

        // Exact, read-only comparison of authored Bedroom requirements. This
        // path deliberately reads ProgramsForObservation so opening the receipt
        // cannot normalize, create, repaint, or otherwise mutate durable room
        // authority.
        internal static string EvaluatePlayerBedroomRequirements(Map map)
        {
            if (map == null)
                return "[CA] player-authored Bedroom requirements: no current map";

            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            List<CASpaceProgram> bedrooms = (component?.ProgramsForObservation
                    ?? Array.Empty<CASpaceProgram>())
                .Where(program => program != null
                    && program.author == CASpaceAuthor.Player
                    && program.purpose == CASpacePurpose.Bedroom)
                .OrderBy(program => program.id).ToList();
            var builder = new StringBuilder();
            builder.Append("[CA] player-authored Bedroom requirements: read-only; ")
                .Append("exact player Bedroom programs ")
                .Append(bedrooms.Count).AppendLine()
                .AppendLine("  authority boundary: only existing Player-authored "
                    + "CASpaceProgram Bedroom footprints are observed. The "
                    + "evaluator creates no program, room, resident, ownership, "
                    + "facility, blueprint, designation, or authorization and "
                    + "does not normalize saved program state.")
                .AppendLine("  comparison boundary: present native function and "
                    + "future optional capacity remain separate multi-axis "
                    + "receipts. A missing optional facility link is available "
                    + "capacity, not a Bedroom requirement; native Beauty is "
                    + "telemetry and never an optimization objective.");
            if (bedrooms.Count == 0)
            {
                builder.AppendLine("  none observed; no test room selected or "
                    + "painted");
                return builder.ToString().TrimEnd();
            }

            for (int i = 0; i < bedrooms.Count; i++)
                AppendBedroomRequirement(builder, map, bedrooms[i]);
            return builder.ToString().TrimEnd();
        }

        private static void AppendBedroomRequirement(StringBuilder builder,
            Map map, CASpaceProgram program)
        {
            List<IntVec3> cells = OrderedCells(program.cells, map);
            var authority = new HashSet<IntVec3>(cells);
            List<Pawn> residents = (program.residents ?? new List<Pawn>())
                .Where(pawn => pawn != null).Distinct()
                .OrderBy(pawn => pawn.thingIDNumber).ToList();
            Dictionary<Room, List<IntVec3>> rooms = cells
                .Select(cell => new { cell, room = cell.GetRoom(map) })
                .Where(entry => entry.room != null)
                .GroupBy(entry => entry.room)
                .ToDictionary(group => group.Key,
                    group => group.Select(entry => entry.cell)
                        .OrderBy(cell => cell.z).ThenBy(cell => cell.x)
                        .ToList());
            int serializedCells = program.cells?.Count ?? 0;
            int noRoom = cells.Count(cell => cell.GetRoom(map) == null);
            string name = program.label.NullOrEmpty()
                ? CASpacePurposeInfo.Label(program.purpose) : program.label;
            builder.Append("  program #").Append(program.id).Append(" '")
                .Append(name).AppendLine("':")
                .Append("    authority envelope: author Player, purpose Bedroom, "
                    + "serialized/in-bounds distinct cells ")
                .Append(serializedCells).Append("/").Append(cells.Count)
                .Append(", native rooms ").Append(rooms.Count)
                .Append(", cells without native room ").Append(noRoom)
                .Append(", sleep requirement ").Append(YesNo(program.RequiresSleep))
                .Append(", declared maximum occupants ")
                .Append(program.maxOccupants).Append(", program style ")
                .Append(CASpacePurposeInfo.StyleLabel(program.style))
                .AppendLine()
                .Append("    resident roster: author ")
                .Append(program.residentAuthor).Append(", actual saved resident "
                    + "references ").Append(residents.Count)
                .AppendLine();
            if (residents.Count == 0)
                builder.AppendLine("      none; absence is explicit");
            for (int i = 0; i < residents.Count; i++)
            {
                Pawn resident = residents[i];
                Ideo ideo = resident.Ideo;
                Building_Bed ownedBed = resident.ownership?.OwnedBed;
                bool ownedBedInside = ownedBed != null && ownedBed.Spawned
                    && ownedBed.Map == map && ownedBed.OccupiedRect().Cells
                        .All(authority.Contains);
                builder.Append("      ").Append(resident.LabelShort)
                    .Append(" #").Append(resident.thingIDNumber)
                    .Append(": spawned/on this map ")
                    .Append(YesNo(resident.Spawned && resident.Map == map))
                    .Append(", free colonist ").Append(YesNo(
                        resident.IsFreeColonist))
                    .Append(", ideoligion ")
                    .Append(ideo == null ? "none" : ideo.name + " [Ideo_"
                        + ideo.id + "]")
                    .Append(", culture ")
                    .Append(ideo?.culture?.defName ?? "none")
                    .Append(", owned bed ")
                    .Append(ownedBed == null ? "none" : ownedBed.def.defName
                        + " #" + ownedBed.thingIDNumber + " at "
                        + ownedBed.Position)
                    .Append(", owned bed inside exact program ")
                    .Append(YesNo(ownedBedInside)).AppendLine();
            }

            builder.AppendLine("    love/family structure and sharing willingness:");
            int pairCount = 0;
            for (int first = 0; first < residents.Count; first++)
                for (int second = first + 1; second < residents.Count; second++)
                {
                    pairCount++;
                    Pawn a = residents[first];
                    Pawn b = residents[second];
                    List<string> direct = a.relations?.DirectRelations
                        .Where(relation => relation?.otherPawn == b
                            && relation.def != null)
                        .Select(relation => relation.def.defName).Distinct()
                        .OrderBy(value => value, StringComparer.Ordinal).ToList()
                        ?? new List<string>();
                    bool sameLoveCluster = a.GetLoveCluster().Contains(b);
                    string parentage = a.GetMother() == b ? b.LabelShort
                            + " is mother of " + a.LabelShort
                        : a.GetFather() == b ? b.LabelShort
                            + " is father of " + a.LabelShort
                        : b.GetMother() == a ? a.LabelShort
                            + " is mother of " + b.LabelShort
                        : b.GetFather() == a ? a.LabelShort
                            + " is father of " + b.LabelShort
                        : "no direct parent-child relation";
                    builder.Append("      ").Append(a.LabelShort).Append(" / ")
                        .Append(b.LabelShort).Append(": direct relations [")
                        .Append(direct.Count == 0 ? "none"
                            : string.Join(", ", direct.ToArray()))
                        .Append("], same native love cluster ")
                        .Append(YesNo(sameLoveCluster)).Append(", ")
                        .Append(parentage).Append("; BedUtility willing to share ")
                        .Append(YesNo(BedUtility.WillingToShareBed(a, b)))
                        .AppendLine();
                }
            if (pairCount == 0)
                builder.AppendLine("      no resident pair; sharing willingness "
                    + "is not inferred");

            if (rooms.Count == 0)
            {
                builder.AppendLine("    present readiness: no native room "
                    + "intersects the exact authority; future potential remains "
                    + "unasserted");
                return;
            }
            foreach (KeyValuePair<Room, List<IntVec3>> entry in rooms
                .OrderBy(pair => pair.Key.ID))
                AppendBedroomRoomRequirement(builder, map, program, residents,
                    entry.Key, entry.Value);
        }

        private static void AppendBedroomRoomRequirement(StringBuilder builder,
            Map map, CASpaceProgram program, List<Pawn> residents, Room room,
            List<IntVec3> authorityCells)
        {
            var field = new CASpatialField(map, authorityCells);
            var residentSet = new HashSet<Pawn>(residents);
            List<Building_Bed> nativeBeds = room.ContainedAndAdjacentThings
                .OfType<Building_Bed>().Where(bed => bed != null && bed.Spawned
                    && bed.def.building?.bed_humanlike == true
                    && bed.def.building.bed_countsForBedroomOrBarracks)
                .OrderBy(bed => bed.thingIDNumber).ToList();
            List<Building_Bed> authoredBeds = nativeBeds.Where(bed =>
                    bed.OccupiedRect().Cells.All(field.authority.Contains))
                .ToList();
            List<Pawn> nativeOwners = nativeBeds.SelectMany(bed =>
                    bed.OwnersForReading).Where(pawn => pawn != null).Distinct()
                .OrderBy(pawn => pawn.thingIDNumber).ToList();
            bool bedOwnerComposition = nativeBeds.Count > 0
                && RoomRoleWorker_Bedroom.IsBedroom(nativeBeds);
            bool bedroomWorkerPositive = RoomRoleDefOf.Bedroom.Worker
                .GetScore(room) > 0f;
            bool currentRoleBedroom = room.Role == RoomRoleDefOf.Bedroom;
            bool nativeFunctional = room.ProperRoom && currentRoleBedroom
                && bedroomWorkerPositive && nativeBeds.Count > 0;
            bool allRosterResidentsOwnAuthoredBed = residents.Count > 0
                && residents.All(resident => authoredBeds.Any(bed =>
                    bed.OwnersForReading.Contains(resident)));
            int roomCellsInside = room.Cells.Count(field.authority.Contains);
            int roomCellsOutside = room.CellCount - roomCellsInside;
            HashSet<IntVec3> reachable = ReachableCirculation(map, field, null);
            int blockedTraffic = field.trafficLanes.Count(cell =>
                HasSpatialObstructionAt(map, cell));
            int blockedIntervals = field.bedBankIntervals.Count(cell =>
                HasSpatialObstructionAt(map, cell));

            builder.Append("    room #").Append(room.ID).Append(" at ")
                .Append(RoomAnchor(room)).AppendLine(":")
                .Append("      authority envelope: authored intersection ")
                .Append(field.authority.Count).Append(", native room cells ")
                .Append(room.CellCount).Append(", native cells inside/outside "
                    + "authority ").Append(roomCellsInside).Append("/")
                .Append(roomCellsOutside).Append(", complete native-room "
                    + "envelope ").Append(YesNo(roomCellsOutside == 0))
                .AppendLine()
                .Append("      native Bedroom classification: proper room ")
                .Append(YesNo(room.ProperRoom)).Append(", psychologically "
                    + "outdoors ").Append(YesNo(room.PsychologicallyOutdoors))
                .Append(", current role ").Append(room.Role?.defName ?? "none")
                .Append(", native Bedroom bed/owner composition ")
                .Append(YesNo(bedOwnerComposition)).Append(", complete Bedroom "
                    + "worker score positive ").Append(YesNo(
                        bedroomWorkerPositive)).AppendLine()
                .Append("      room-stat axes: ").Append(RoomStatsReceipt(room))
                .AppendLine()
                .Append("      spatial axes: doorway approaches ")
                .Append(field.doorwayApproaches.Count).Append(", service cells ")
                .Append(field.serviceCells.Count).Append(", sleep approaches ")
                .Append(field.sleepApproaches.Count).Append(", reachable "
                    + "circulation cells ").Append(reachable.Count)
                .Append(", reachable sleep approaches ")
                .Append(field.sleepApproaches.Count(reachable.Contains))
                .Append(", clear negative-space cells ").Append(field.clearCells)
                .Append(", protected traffic lanes total/obstructed ")
                .Append(field.trafficLanes.Count).Append("/")
                .Append(blockedTraffic).Append(", established bed-bank interval "
                    + "cells total/obstructed ")
                .Append(field.bedBankIntervals.Count).Append("/")
                .Append(blockedIntervals).AppendLine();

            builder.Append("      bed capacity/ownership: native beds ")
                .Append(nativeBeds.Count).Append(", inside exact authority ")
                .Append(authoredBeds.Count).Append(", sleeping slots ")
                .Append(nativeBeds.Sum(bed => bed.SleepingSlotsCount))
                .Append(", native owners ").Append(nativeOwners.Count)
                .Append(" [").Append(nativeOwners.Count == 0 ? "none"
                    : string.Join(", ", nativeOwners.Select(pawn =>
                        pawn.LabelShort + " #" + pawn.thingIDNumber).ToArray()))
                .Append("], roster residents owning an authored bed ")
                .Append(residents.Count(resident => authoredBeds.Any(bed =>
                    bed.OwnersForReading.Contains(resident))))
                .Append(", owners outside roster ")
                .Append(nativeOwners.Count(owner => !residentSet.Contains(owner)))
                .AppendLine();
            for (int i = 0; i < nativeBeds.Count; i++)
            {
                Building_Bed bed = nativeBeds[i];
                CompAffectedByFacilities affected = bed
                    .TryGetComp<CompAffectedByFacilities>();
                List<Thing> linked = affected?.LinkedFacilitiesListForReading
                    .Where(thing => thing != null).OrderBy(thing =>
                        thing.def.defName, StringComparer.Ordinal)
                    .ThenBy(thing => thing.thingIDNumber).ToList()
                    ?? new List<Thing>();
                builder.Append("        ").Append(bed.def.defName).Append(" #")
                    .Append(bed.thingIDNumber).Append(" at ").Append(bed.Position)
                    .Append(": inside authority ").Append(YesNo(
                        authoredBeds.Contains(bed))).Append(", slots ")
                    .Append(bed.SleepingSlotsCount).Append(", owners [")
                    .Append(bed.OwnersForReading.Count == 0 ? "none"
                        : string.Join(", ", bed.OwnersForReading.Where(pawn =>
                            pawn != null).Select(pawn => pawn.LabelShort + " #"
                                + pawn.thingIDNumber).ToArray()))
                    .Append("], medical/prisoner ").Append(YesNo(bed.Medical))
                    .Append("/").Append(YesNo(bed.ForPrisoners))
                    .Append(", stuff ").Append(bed.Stuff?.defName ?? "none")
                    .Append(", native style ")
                    .Append(bed.StyleDef?.defName ?? "definition default")
                    .Append(", live facilities [")
                    .Append(linked.Count == 0 ? "none" : string.Join(", ",
                        linked.Select(thing => thing.def.defName + " #"
                            + thing.thingIDNumber + " active "
                            + YesNo(affected.IsFacilityActive(thing))).ToArray()))
                    .AppendLine("]");
            }

            AppendBedroomConstructionStages(builder, map, room, field);
            AppendBedroomFacilityPotential(builder, map, field, authoredBeds);
            builder.Append("      present readiness axes: native functional "
                    + "Bedroom ").Append(YesNo(nativeFunctional))
                .Append(", non-outdoors enclosure ")
                .Append(YesNo(!room.PsychologicallyOutdoors))
                .Append(", exact room envelope complete ")
                .Append(YesNo(roomCellsOutside == 0))
                .Append(", program sleep requirement ")
                .Append(YesNo(program.RequiresSleep))
                .Append(", resident roster present ")
                .Append(YesNo(residents.Count > 0))
                .Append(", every roster resident owns an authored bed ")
                .Append(YesNo(allRosterResidentsOwnAuthoredBed)).AppendLine()
                .Append("      functional preservation: ")
                .Append(nativeFunctional
                    ? "this room is already a native functional Bedroom"
                    : "this room is not presently a native functional Bedroom")
                .AppendLine(". Optional native facility bonuses and unused link "
                    + "capacity do not change that result, do not constitute a "
                    + "missing object, and authorize no construction.");
        }

        private static void AppendBedroomConstructionStages(
            StringBuilder builder, Map map, Room room, CASpatialField field)
        {
            List<Building> completed = map.listerBuildings.allBuildingsColonist
                .Where(building => building != null && building.Spawned
                    && building.GetRoom() == room
                    && building.OccupiedRect().Cells.All(
                        field.authority.Contains))
                .OrderBy(building => building.thingIDNumber).ToList();
            IReadOnlyList<Thing> allThings = map.listerThings.AllThings;
            List<Blueprint_Build> blueprints = allThings.OfType<Blueprint_Build>()
                .Where(blueprint => blueprint != null && blueprint.Spawned
                    && blueprint.Faction == Faction.OfPlayer
                    && blueprint.BuildDef != null
                    && GenAdj.OccupiedRect(blueprint.Position,
                        blueprint.Rotation, blueprint.BuildDef.Size).Cells
                        .All(field.authority.Contains))
                .OrderBy(blueprint => blueprint.thingIDNumber).ToList();
            List<Frame> frames = allThings.OfType<Frame>()
                .Where(frame => frame != null && frame.Spawned
                    && frame.Faction == Faction.OfPlayer
                    && frame.BuildDef != null
                    && GenAdj.OccupiedRect(frame.Position, frame.Rotation,
                        frame.BuildDef.Size).Cells.All(field.authority.Contains))
                .OrderBy(frame => frame.thingIDNumber).ToList();

            builder.Append("      construction stages: completed buildings ")
                .Append(completed.Count).Append(", player blueprints ")
                .Append(blueprints.Count).Append(", player frames ")
                .Append(frames.Count).AppendLine();
            for (int i = 0; i < completed.Count; i++)
            {
                Building building = completed[i];
                CompAffectedByFacilities affected = building
                    .TryGetComp<CompAffectedByFacilities>();
                CompFacility facility = building.TryGetComp<CompFacility>();
                string affectedLinks = affected == null ? "not affected"
                    : affected.LinkedFacilitiesListForReading.Count + " live";
                string facilityLinks = facility == null ? "not a facility"
                    : facility.LinkedBuildings.Count + " reciprocal";
                builder.Append("        completed ").Append(building.def.defName)
                    .Append(" #").Append(building.thingIDNumber).Append(" at ")
                    .Append(building.Position).Append(", stuff ")
                    .Append(building.Stuff?.defName ?? "none")
                    .Append(", style ")
                    .Append(building.StyleDef?.defName ?? "definition default")
                    .Append(", Beauty telemetry ")
                    .Append(building.GetStatValue(StatDefOf.Beauty)
                        .ToString("F2"))
                    .Append(", open cardinal approaches ")
                    .Append(OpenApproaches(map, field,
                        building.OccupiedRect()))
                    .Append(", facility graph ").Append(affectedLinks)
                    .Append(" / ").Append(facilityLinks).AppendLine();
            }
            for (int i = 0; i < blueprints.Count; i++)
            {
                Blueprint_Build blueprint = blueprints[i];
                builder.Append("        blueprint ")
                    .Append(blueprint.BuildDef.defName).Append(" #")
                    .Append(blueprint.thingIDNumber).Append(" at ")
                    .Append(blueprint.Position).Append(", stuff ")
                    .Append(blueprint.EntityToBuildStuff()?.defName ?? "none")
                    .Append(", style ")
                    .Append(blueprint.EntityToBuildStyle()?.defName
                        ?? "definition default")
                    .Append(", material/cost [")
                    .Append(ConstructionCostReceipt(map, blueprint))
                    .AppendLine("]");
            }
            for (int i = 0; i < frames.Count; i++)
            {
                Frame frame = frames[i];
                builder.Append("        frame ").Append(frame.BuildDef.defName)
                    .Append(" #").Append(frame.thingIDNumber).Append(" at ")
                    .Append(frame.Position).Append(", complete ")
                    .Append(frame.PercentComplete.ToString("P1"))
                    .Append(", stuff ").Append(frame.Stuff?.defName ?? "none")
                    .Append(", style ")
                    .Append(frame.StyleDef?.defName ?? "definition default")
                    .Append(", material/cost [")
                    .Append(ConstructionCostReceipt(map, frame))
                    .AppendLine("]");
            }
            float roomBeauty = room.GetStat(RoomStatDefOf.Beauty);
            builder.Append("      style/Beauty telemetry: program-independent "
                    + "native room Beauty ").Append(roomBeauty.ToString("F2"))
                .AppendLine("; completed definitions, stuff, and actual native "
                    + "styles are listed above. No aesthetic verdict, target, "
                    + "or placement preference is inferred from this scalar.");
        }

        private static string ConstructionCostReceipt(Map map,
            Thing construction)
        {
            List<ThingDefCountClass> costs;
            try
            {
                Blueprint_Build blueprint = construction as Blueprint_Build;
                Frame frame = construction as Frame;
                costs = blueprint != null ? blueprint.TotalMaterialCost()
                    : frame?.TotalMaterialCost();
            }
            catch
            {
                return "unavailable from loaded definition";
            }
            if (costs == null || costs.Count == 0) return "none";
            return string.Join(", ", costs.OrderBy(entry =>
                    entry.thingDef.defName, StringComparer.Ordinal)
                .Select(entry => entry.thingDef.defName + " spawned "
                    + SpawnedCount(map, entry.thingDef) + "/cost "
                    + entry.count).ToArray());
        }

        private static void AppendBedroomFacilityPotential(
            StringBuilder builder, Map map, CASpatialField field,
            List<Building_Bed> beds)
        {
            List<ThingDef> definitions = beds.SelectMany(bed => bed.def
                    .GetCompProperties<CompProperties_AffectedByFacilities>()?
                    .linkableFacilities ?? new List<ThingDef>())
                .Where(def => def != null).Distinct()
                .OrderBy(def => def.defName, StringComparer.Ordinal).ToList();
            builder.Append("      future potential axes: optional loaded bed-"
                    + "facility definitions ").Append(definitions.Count)
                .Append(", clear negative-space cells ").Append(field.clearCells)
                .Append(", protected traffic lanes ")
                .Append(field.trafficLanes.Count).Append(", bed-bank intervals ")
                .Append(field.bedBankIntervals.Count).AppendLine();
            if (definitions.Count == 0)
            {
                builder.AppendLine("        none loaded for the authored beds; no "
                    + "facility potential or requirement invented");
                return;
            }
            for (int i = 0; i < definitions.Count; i++)
            {
                ThingDef def = definitions[i];
                CompProperties_Facility props = def
                    .GetCompProperties<CompProperties_Facility>();
                int linkedBeds = beds.Count(bed => bed
                    .TryGetComp<CompAffectedByFacilities>()?
                    .LinkedFacilitiesListForReading.Any(thing =>
                        thing != null && thing.def == def) == true);
                int availableBedLinks = beds.Count(bed =>
                {
                    CompAffectedByFacilities affected = bed
                        .TryGetComp<CompAffectedByFacilities>();
                    int current = affected?.LinkedFacilitiesListForReading
                        .Count(thing => thing != null && thing.def == def) ?? 0;
                    return current < (props?.maxSimultaneous ?? 0);
                });
                ThingDef affordableStuff;
                bool resourceCountSufficient = TryResolveAffordableStuff(map, def,
                    out affordableStuff);
                ThingDef costStuff = resourceCountSufficient ? affordableStuff
                    : ResolveStuff(def);
                string costs;
                int costUnits;
                CostEvidence(def, costStuff, out costs, out costUnits);
                builder.Append("        ").Append(def.defName).Append(" [source ")
                    .Append(SourceReceipt(def)).Append("]: effect ")
                    .Append(FacilityOffsets(props)).Append(", beds with live "
                        + "same-definition link ").Append(linkedBeds)
                    .Append("/").Append(beds.Count)
                    .Append(", beds with unused native link capacity ")
                    .Append(availableBedLinks).Append("/").Append(beds.Count)
                    .Append(", player-buildable ")
                    .Append(YesNo(def.BuildableByPlayer))
                    .Append(", research ready ")
                    .Append(YesNo(def.IsResearchFinished))
                    .Append(", colony tech ready ").Append(YesNo(
                        TechAllows(map, def))).Append(", map resource-count "
                        + "sufficient ")
                    .Append(YesNo(resourceCountSufficient))
                    .Append(" (reachability, forbiddance, and reservations not "
                        + "asserted), selected stuff ")
                    .Append(affordableStuff?.defName ?? (def.MadeFromStuff
                        ? "none" : "not required"))
                    .Append(", cost basis stuff ")
                    .Append(costStuff?.defName ?? (def.MadeFromStuff
                        ? "unresolved" : "not required"))
                    .Append(", cost units ").Append(costUnits)
                    .Append(" [").Append(costs).AppendLine("]; capability is "
                        + "optional evidence only. No protected placement or "
                        + "construction need is inferred.");
            }
        }

        internal static bool TrySelectStoragePlan(Map map,
            Zone_Stockpile zone, Pawn planner,
            IReadOnlyList<Building_Storage> relatedStorage,
            out CAStoragePlan selected, out string blocker)
        {
            selected = null;
            blocker = "no eligible storage transition was evaluated";
            if (map == null || zone == null || zone.Map != map
                || zone.settings == null || zone.cells == null
                || zone.cells.Count == 0)
            {
                blocker = "the native stockpile authority is unavailable";
                return false;
            }
            if (planner == null || !planner.Spawned || planner.Map != map)
            {
                blocker = "no construction author is available";
                return false;
            }

            var field = new CASpatialField(map, zone.cells);
            List<ThingDef> definitions = OperativeStorageDefinitions();
            int researched = 0;
            int affordable = 0;
            int placementProbes = 0;
            int nativeAccepted = 0;
            int incompatibleCurrentUse = 0;
            for (int definitionIndex = 0;
                definitionIndex < definitions.Count; definitionIndex++)
            {
                ThingDef def = definitions[definitionIndex];
                if (!def.IsResearchFinished || !TechAllows(map, def)
                    || !CapableBuilder(planner, def)) continue;
                researched++;
                ThingDef stuff;
                if (!TryResolveAffordableStuff(map, def, out stuff))
                    continue;
                affordable++;

                List<Building_Storage> same = (relatedStorage
                    ?? Array.Empty<Building_Storage>()).Where(building =>
                        building != null && building.Spawned
                        && building.Map == map && building.def == def)
                    .OrderBy(building => building.thingIDNumber).ToList();
                List<IntVec3> centers = field.authority
                    .OrderBy(cell => cell.z).ThenBy(cell => cell.x).ToList();
                for (int centerIndex = 0; centerIndex < centers.Count;
                    centerIndex++)
                {
                    IntVec3 center = centers[centerIndex];
                    foreach (Rot4 rotation in CandidateRotations(def))
                    {
                        placementProbes++;
                        CellRect footprint = GenAdj.OccupiedRect(center,
                            rotation, def.Size);
                        List<IntVec3> occupied = footprint.Cells.ToList();
                        if (occupied.Any(cell =>
                                !field.authority.Contains(cell))
                            || occupied.Count >= field.authority.Count
                            || occupied.Any(cell => map.zoneManager.ZoneAt(cell)
                                != zone)
                            || occupied.Any(field.doorwayApproaches.Contains)
                            || occupied.Any(field.serviceCells.Contains)
                            || occupied.Any(field.sleepApproaches.Contains))
                            continue;
                        if (!center.InAllowedArea(planner)
                            || !planner.CanReach(center, PathEndMode.Touch,
                                Danger.Some)) continue;

                        List<Thing> items = occupied.SelectMany(cell =>
                                cell.GetThingList(map)).Where(thing =>
                                thing != null && thing.Spawned
                                && thing.def.category == ThingCategory.Item)
                            .Distinct().ToList();
                        StorageSettings fixedSettings = def.building
                            .fixedStorageSettings
                            ?? StorageSettings.EverStorableFixedSettings();
                        if (items.Any(item =>
                            !zone.settings.AllowedToAccept(item)
                            || !fixedSettings.AllowedToAccept(item)))
                        {
                            incompatibleCurrentUse++;
                            continue;
                        }

                        AcceptanceReport native = CASettlementSitingConstraints
                            .CanPlaceNativeBlueprint(map, def, center,
                                rotation, stuff);
                        if (!native.Accepted) continue;
                        nativeAccepted++;

                        int floorSlots = occupied.Sum(cell =>
                            cell.GetMaxItemsAllowedInCell(map));
                        int storageSlots = def.building.maxItemsInCell
                            * def.Size.Area;
                        if (storageSlots <= floorSlots) continue;
                        var plan = new CAStoragePlan
                        {
                            def = def,
                            stuff = stuff,
                            cell = center,
                            rotation = rotation,
                            floorSlots = floorSlots,
                            storageSlots = storageSlots,
                            capacityDelta = storageSlots - floorSlots,
                            itemStacks = items.Count,
                            itemUnits = items.Sum(item => item.stackCount),
                            openApproaches = OpenApproaches(map, field,
                                footprint),
                            wallContacts = WallContacts(map, footprint),
                            clearCellsAfter = Math.Max(0, field.clearCells
                                - occupied.Count(cell => cell.Standable(map)
                                    && !HasConstructionAt(map, cell))),
                            existingSameDef = same.Count,
                            parallelSameDef = same.Count(building =>
                                building.Rotation == rotation
                                || building.Rotation == rotation.Opposite),
                            nearestSameDef = same.Count == 0 ? int.MaxValue
                                : same.Min(building => Math.Abs(
                                    building.Position.x - center.x)
                                    + Math.Abs(building.Position.z - center.z)),
                            zoneTransitionCells = occupied.Count
                        };
                        if (selected == null || Prefer(plan, selected))
                            selected = plan;
                    }
                }
            }

            if (selected != null)
            {
                blocker = null;
                return true;
            }
            blocker = "loaded furniture-storage definitions "
                + definitions.Count + ", researched and skilled " + researched
                + ", materially ready " + affordable + ", placement probes "
                + placementProbes + ", native accepted " + nativeAccepted
                + ", incompatible occupied footprints "
                + incompatibleCurrentUse
                + "; no positive-density transition preserves every spatial, "
                + "current-use, access, technology, and material axis";
            return false;
        }

        internal static bool IsOperativeRoomProgram(CASpaceProgram program)
        {
            return program != null && program.author == CASpaceAuthor.Player
                && program.RequiresSleep && program.cells != null
                && program.cells.Count > 0
                && (program.purpose == CASpacePurpose.Bedroom
                    || program.purpose == CASpacePurpose.Barracks);
        }

        // Existing Bedroom associations still validate through
        // IsOperativeRoomProgram, but the proven construction consumer has only
        // earned authority to originate new facility work inside Barracks.
        internal static bool CanOriginateNewRoomFacility(CASpaceProgram program)
        {
            return IsOperativeRoomProgram(program)
                && program.purpose == CASpacePurpose.Barracks;
        }

        internal static bool TrySelectRoomFacilityPlan(Map map,
            CASpaceProgram program, Pawn planner,
            CAInitiativeTier effectiveTier,
            out CARoomFacilityPlan selected, out string blocker)
        {
            selected = null;
            blocker = "no eligible native bed-facility relationship was evaluated";
            if (map == null || !map.IsPlayerHome
                || !IsOperativeRoomProgram(program))
            {
                blocker = "the authored Bedroom or Barracks authority is unavailable";
                return false;
            }
            if (planner == null || !planner.Spawned || planner.Map != map)
            {
                blocker = "no construction author is available";
                return false;
            }

            var authority = new HashSet<IntVec3>(program.cells.Where(cell =>
                cell.InBounds(map)));
            Dictionary<Room, List<IntVec3>> rooms = authority
                .Select(cell => new { cell, room = cell.GetRoom(map) })
                .Where(entry => entry.room != null && entry.room.ProperRoom
                    && !entry.room.PsychologicallyOutdoors)
                .GroupBy(entry => entry.room)
                .ToDictionary(group => group.Key,
                    group => group.Select(entry => entry.cell).ToList());
            int roomCount = rooms.Count;
            int bedCount = 0;
            int relevantBedCount = 0;
            int loadedDefinitions = 0;
            int positiveDefinitions = 0;
            int researchedDefinitions = 0;
            int affordableDefinitions = 0;
            int placementProbes = 0;
            int nativeAccepted = 0;
            int circulationRejected = 0;
            int currentUseRejected = 0;
            int trafficLaneRejected = 0;
            int bedPatternRejected = 0;
            int functionalPostureRejected = 0;
            int sharedRelationshipRejected = 0;
            int ungroundedFacilityFamilyRejected = 0;
            int pendingPlayerFacilityConstruction = 0;

            foreach (KeyValuePair<Room, List<IntVec3>> entry in rooms
                .OrderBy(pair => pair.Key.ID))
            {
                Room room = entry.Key;
                RoomRoleDef role = room.Role;
                if (role != RoomRoleDefOf.Bedroom
                    && role != RoomRoleDefOf.Barracks) continue;
                var field = new CASpatialField(map, entry.Value);
                List<Building_Bed> beds = map.listerBuildings
                    .allBuildingsColonist.OfType<Building_Bed>()
                    .Where(bed => bed != null && bed.Spawned
                        && bed.Faction == Faction.OfPlayer && !bed.Medical
                        && !bed.ForPrisoners
                        && bed.def.building?.bed_humanlike == true
                        && bed.def.building.bed_countsForBedroomOrBarracks
                        && bed.GetRoom() == room
                        && bed.OccupiedRect().Cells.All(cell =>
                            field.authority.Contains(cell)))
                    .OrderBy(bed => bed.thingIDNumber).ToList();
                bedCount += beds.Count;
                if (beds.Count == 0) continue;

                var residents = new HashSet<Pawn>((program.residents
                    ?? new List<Pawn>()).Where(pawn => pawn != null));
                List<Building_Bed> residentOwned = beds.Where(bed =>
                    bed.OwnersForReading.Any(residents.Contains)).ToList();
                List<Building_Bed> relevant = residentOwned.Count > 0
                    ? residentOwned : beds;
                relevantBedCount += relevant.Count;

                List<ThingDef> definitions = relevant.SelectMany(bed => bed.def
                        .GetCompProperties<CompProperties_AffectedByFacilities>()?
                        .linkableFacilities ?? new List<ThingDef>())
                    .Where(def => def != null).Distinct()
                    .OrderBy(def => def.defName, StringComparer.Ordinal)
                    .ToList();
                loadedDefinitions += definitions.Count;
                var establishedDefinitions = new HashSet<ThingDef>(definitions
                    .Where(def => map.listerBuildings.allBuildingsColonist
                        .Any(building => building != null && building.Spawned
                            && building.Faction == Faction.OfPlayer
                            && building.def == def && building.GetRoom() == room
                            && field.authority.Contains(building.Position))));
                var pendingDefinitions = new HashSet<ThingDef>();
                IReadOnlyList<Thing> allThings = map.listerThings.AllThings;
                for (int thingIndex = 0; thingIndex < allThings.Count;
                    thingIndex++)
                {
                    Thing thing = allThings[thingIndex];
                    if (thing == null || !thing.Spawned
                        || thing.Faction != Faction.OfPlayer
                        || !(thing is Blueprint_Build || thing is Frame))
                        continue;
                    ThingDef entity = thing.def.entityDefToBuild as ThingDef;
                    if (entity == null || !definitions.Contains(entity))
                        continue;
                    CellRect pendingFootprint = GenAdj.OccupiedRect(
                        thing.Position, thing.Rotation, entity.Size);
                    if (pendingFootprint.Cells.All(cell =>
                        field.authority.Contains(cell)
                        && cell.GetRoom(map) == room))
                        pendingDefinitions.Add(entity);
                }
                establishedDefinitions.UnionWith(pendingDefinitions);
                if (pendingDefinitions.Count > 0)
                {
                    pendingPlayerFacilityConstruction +=
                        pendingDefinitions.Count;
                    continue;
                }
                for (int definitionIndex = 0;
                    definitionIndex < definitions.Count; definitionIndex++)
                {
                    ThingDef def = definitions[definitionIndex];
                    if (establishedDefinitions.Count > 0
                        && !establishedDefinitions.Contains(def))
                    {
                        ungroundedFacilityFamilyRejected++;
                        continue;
                    }
                    CompProperties_Facility facility = def
                        .GetCompProperties<CompProperties_Facility>();
                    if (facility?.statOffsets == null
                        || !facility.statOffsets.Any(offset => offset != null
                            && offset.stat != null && offset.value > 0f)
                        || !def.BuildableByPlayer || def.blueprintDef == null
                        || def.building == null) continue;
                    positiveDefinitions++;
                    if (!def.IsResearchFinished || !TechAllows(map, def)
                        || !CapableBuilder(planner, def)) continue;
                    researchedDefinitions++;
                    ThingDef stuff;
                    if (!TryResolveAffordableStuff(map, def, out stuff))
                        continue;
                    affordableDefinitions++;

                    List<Building_Bed> missing = relevant.Where(bed =>
                    {
                        CompAffectedByFacilities affected = bed
                            .TryGetComp<CompAffectedByFacilities>();
                        if (affected == null) return false;
                        int current = affected.LinkedFacilitiesListForReading
                            .Count(thing => thing != null
                                && thing.def == def);
                        return current < facility.maxSimultaneous;
                    }).ToList();
                    if (missing.Count == 0) continue;

                    List<Building> same = map.listerBuildings
                        .allBuildingsColonist.Where(building => building != null
                            && building.Spawned && building.Faction
                                == Faction.OfPlayer && building.def == def
                            && building.GetRoom() == room
                            && field.authority.Contains(building.Position))
                        .OrderBy(building => building.thingIDNumber).ToList();
                    List<IntVec3> centers = field.authority
                        .OrderBy(cell => cell.z).ThenBy(cell => cell.x)
                        .ToList();
                    for (int centerIndex = 0; centerIndex < centers.Count;
                        centerIndex++)
                    {
                        IntVec3 center = centers[centerIndex];
                        foreach (Rot4 rotation in CandidateRotations(def))
                        {
                            placementProbes++;
                            CellRect footprint = GenAdj.OccupiedRect(center,
                                rotation, def.Size);
                            List<IntVec3> occupied = footprint.Cells.ToList();
                            if (occupied.Any(cell =>
                                    !field.authority.Contains(cell)
                                    || cell.GetRoom(map) != room)
                                || occupied.Count >= field.authority.Count
                                || occupied.Any(cell => map.zoneManager
                                    .ZoneAt(cell) != null)
                                || occupied.Any(field.doorwayApproaches.Contains)
                                || occupied.Any(field.serviceCells.Contains)
                                || occupied.Any(field.sleepApproaches.Contains))
                                continue;
                            if (occupied.Any(cell => cell.GetThingList(map)
                                .Any(thing => thing != null && thing.Spawned
                                    && (thing is Building || thing is Blueprint
                                        || thing is Frame
                                        || thing.def.category
                                            == ThingCategory.Item
                                        || thing.def.category
                                            == ThingCategory.Plant))))
                            {
                                currentUseRejected++;
                                continue;
                            }
                            if (!center.InAllowedArea(planner)
                                || !planner.CanReach(center, PathEndMode.Touch,
                                    Danger.Some)) continue;

                            List<Building_Bed> served = missing.Where(bed =>
                                bed.TryGetComp<CompAffectedByFacilities>()
                                    .CanPotentiallyLinkTo(def, center,
                                        rotation)).ToList();
                            if (served.Count == 0) continue;
                            // Proactive may act on a coherent shared group. It
                            // does not force one object to cover an entire bank,
                            // because that pressure can turn circulation or the
                            // established bed rhythm into furniture space.
                            // Autonomous may additionally address one exact
                            // missing relationship.
                            int proactiveMinimum = Math.Min(2, missing.Count);
                            if (effectiveTier == CAInitiativeTier.Proactive
                                && served.Count < proactiveMinimum)
                            {
                                sharedRelationshipRejected++;
                                continue;
                            }

                            AcceptanceReport native =
                                CASettlementSitingConstraints
                                    .CanPlaceNativeBlueprint(map, def, center,
                                        rotation, stuff);
                            if (!native.Accepted) continue;
                            nativeAccepted++;
                            if (occupied.Any(field.trafficLanes.Contains))
                            {
                                trafficLaneRejected++;
                                continue;
                            }
                            bool nativeBedHeadPosture = UsesNativeBedHeadPosture(
                                facility);
                            if (!nativeBedHeadPosture && occupied.Any(
                                field.bedBankIntervals.Contains))
                            {
                                bedPatternRejected++;
                                continue;
                            }
                            int wallBackedCells;
                            int usableFaceCells;
                            string functionalPosture;
                            if (!TryFunctionalPosture(map, field, def, facility,
                                footprint, rotation, nativeBedHeadPosture,
                                out wallBackedCells, out usableFaceCells,
                                out functionalPosture))
                            {
                                functionalPostureRejected++;
                                continue;
                            }
                            int circulationBefore;
                            int circulationAfter;
                            int sleepBefore;
                            int sleepAfter;
                            if (!PreservesCirculation(map, field, footprint,
                                out circulationBefore, out circulationAfter,
                                out sleepBefore, out sleepAfter))
                            {
                                circulationRejected++;
                                continue;
                            }

                            string costReceipt;
                            int costUnits;
                            CostEvidence(def, stuff, out costReceipt,
                                out costUnits);
                            string cultureReceipt;
                            ThingStyleDef nativeStyle = ResolveRoomNativeStyle(
                                program, def, out cultureReceipt);
                            var plan = new CARoomFacilityPlan
                            {
                                def = def,
                                stuff = stuff,
                                nativeStyle = nativeStyle,
                                room = room,
                                cell = center,
                                rotation = rotation,
                                relevantBeds = relevant.Count,
                                missingLinksBefore = missing.Count,
                                linksServed = served.Count,
                                residentBedsServed = served.Count(bed =>
                                    bed.OwnersForReading.Any(residents.Contains)),
                                ownedBedsServed = served.Count(bed =>
                                    bed.OwnersForReading.Count > 0),
                                openApproaches = OpenApproaches(map, field,
                                    footprint),
                                wallContacts = WallContacts(map, footprint),
                                clearCellsAfter = Math.Max(0, field.clearCells
                                    - occupied.Count(cell => cell.Standable(map)
                                        && !HasSpatialObstructionAt(map, cell))),
                                circulationCellsBefore = circulationBefore,
                                circulationCellsAfter = circulationAfter,
                                reachableSleepBefore = sleepBefore,
                                reachableSleepAfter = sleepAfter,
                                protectedTrafficCells = field.trafficLanes.Count,
                                establishedBedIntervalCells = field
                                    .bedBankIntervals.Count,
                                wallBackedCells = wallBackedCells,
                                usableFaceCells = usableFaceCells,
                                functionalPosture = functionalPosture,
                                facilityFamilyReceipt = establishedDefinitions
                                    .Count == 0
                                    ? "originates one bounded native family; "
                                        + "other available bonuses remain "
                                        + "affordances, not requirements"
                                    : "continues established " + def.defName
                                        + "; no additional family originated",
                                existingSameDef = same.Count,
                                parallelSameDef = same.Count(building =>
                                    building.Rotation == rotation
                                    || building.Rotation == rotation.Opposite),
                                nearestSameDef = same.Count == 0 ? int.MaxValue
                                    : same.Min(building => Math.Abs(
                                        building.Position.x - center.x)
                                        + Math.Abs(building.Position.z
                                            - center.z)),
                                footprintCells = occupied.Count,
                                costUnits = costUnits,
                                workToBuild = def.GetStatValueAbstract(
                                    StatDefOf.WorkToBuild, stuff),
                                roleBefore = role,
                                roleAfter = room.GetRoomRoleIfBuildingPlaced(def),
                                costReceipt = costReceipt,
                                cultureReceipt = cultureReceipt
                            };
                            if (plan.roleAfter != role) continue;
                            for (int i = 0; i < served.Count; i++)
                            {
                                plan.focusIds.Add(served[i].thingIDNumber);
                                plan.focusThingIds.Add(served[i].ThingID);
                            }
                            if (selected == null || PreferRoom(plan, selected,
                                program.style)) selected = plan;
                        }
                    }
                }
            }

            if (pendingPlayerFacilityConstruction > 0)
                selected = null;
            if (selected != null)
            {
                blocker = null;
                return true;
            }
            blocker = "proper authored native sleep rooms " + roomCount
                + ", existing eligible beds " + bedCount
                + ", currently relevant beds " + relevantBedCount
                + ", loaded bed-link definitions " + loadedDefinitions
                + ", positive native stat facilities " + positiveDefinitions
                + ", researched and skilled " + researchedDefinitions
                + ", materially ready " + affordableDefinitions
                + ", placement probes " + placementProbes
                + ", native accepted " + nativeAccepted
                + ", occupied-footprint rejections " + currentUseRejected
                + ", circulation rejections " + circulationRejected
                + ", protected-traffic-lane rejections "
                + trafficLaneRejected + ", established-bed-interval rejections "
                + bedPatternRejected + ", functional-posture rejections "
                + functionalPostureRejected
                + ", Proactive non-shared-relationship rejections "
                + sharedRelationshipRejected
                + ", ungrounded additional-facility-family rejections "
                + ungroundedFacilityFamilyRejected
                + ", player-authored facility families already under "
                + "construction " + pendingPlayerFacilityConstruction
                + "; no missing positive native bed relationship preserves "
                + "the exact room authority, residents, ownership, room role, "
                + "current use, functional posture, protected travel lanes, "
                + "bed-bank intervals, service and sleep approaches, "
                + "circulation, negative space, research, and materials";
            return false;
        }

        private static bool PreferRoom(CARoomFacilityPlan candidate,
            CARoomFacilityPlan incumbent, CASpaceStyle style)
        {
            int comparison = candidate.residentBedsServed.CompareTo(
                incumbent.residentBedsServed);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.linksServed.CompareTo(
                incumbent.linksServed);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.existingSameDef.CompareTo(
                incumbent.existingSameDef);
            if (comparison != 0) return comparison > 0;
            if (style == CASpaceStyle.Austere
                || style == CASpaceStyle.Practical)
            {
                comparison = incumbent.footprintCells.CompareTo(
                    candidate.footprintCells);
                if (comparison != 0) return comparison > 0;
                comparison = incumbent.costUnits.CompareTo(
                    candidate.costUnits);
                if (comparison != 0) return comparison > 0;
                comparison = incumbent.workToBuild.CompareTo(
                    candidate.workToBuild);
                if (comparison != 0) return comparison > 0;
            }
            // Comfortable is not a request to maximize RimWorld's Beauty
            // scalar. Culture/style may select a truthful native style, while
            // placement remains a functional composition rather than an
            // optimizable aesthetic score.
            comparison = candidate.wallBackedCells.CompareTo(
                incumbent.wallBackedCells);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.usableFaceCells.CompareTo(
                incumbent.usableFaceCells);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.parallelSameDef.CompareTo(
                incumbent.parallelSameDef);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.wallContacts.CompareTo(
                incumbent.wallContacts);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.openApproaches.CompareTo(
                incumbent.openApproaches);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.circulationCellsAfter.CompareTo(
                incumbent.circulationCellsAfter);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.clearCellsAfter.CompareTo(
                incumbent.clearCellsAfter);
            if (comparison != 0) return comparison > 0;
            comparison = incumbent.nearestSameDef.CompareTo(
                candidate.nearestSameDef);
            if (comparison != 0) return comparison > 0;
            comparison = incumbent.footprintCells.CompareTo(
                candidate.footprintCells);
            if (comparison != 0) return comparison > 0;
            comparison = string.CompareOrdinal(candidate.def.defName,
                incumbent.def.defName);
            if (comparison != 0) return comparison < 0;
            comparison = candidate.cell.z.CompareTo(incumbent.cell.z);
            if (comparison != 0) return comparison < 0;
            comparison = candidate.cell.x.CompareTo(incumbent.cell.x);
            if (comparison != 0) return comparison < 0;
            return candidate.rotation.AsInt < incumbent.rotation.AsInt;
        }

        internal static List<ThingDef> OperativeStorageDefinitions()
        {
            CASettlementDevelopmentProposal fact = CASettlementAssetRegistry
                .BuildDemandFact(CASettlementDemandKind.Storage);
            return fact.Candidates(CASettlementDemandKind.Storage).ToList();
        }

        private static bool Prefer(CAStoragePlan candidate,
            CAStoragePlan incumbent)
        {
            int comparison = candidate.itemStacks.CompareTo(
                incumbent.itemStacks);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.capacityDelta.CompareTo(
                incumbent.capacityDelta);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.parallelSameDef.CompareTo(
                incumbent.parallelSameDef);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.wallContacts.CompareTo(
                incumbent.wallContacts);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.openApproaches.CompareTo(
                incumbent.openApproaches);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.clearCellsAfter.CompareTo(
                incumbent.clearCellsAfter);
            if (comparison != 0) return comparison > 0;
            comparison = incumbent.nearestSameDef.CompareTo(
                candidate.nearestSameDef);
            if (comparison != 0) return comparison > 0;
            comparison = string.CompareOrdinal(candidate.def.defName,
                incumbent.def.defName);
            if (comparison != 0) return comparison < 0;
            comparison = candidate.cell.z.CompareTo(incumbent.cell.z);
            if (comparison != 0) return comparison < 0;
            comparison = candidate.cell.x.CompareTo(incumbent.cell.x);
            if (comparison != 0) return comparison < 0;
            return candidate.rotation.AsInt < incumbent.rotation.AsInt;
        }

        private static bool TryResolveAffordableStuff(Map map, ThingDef def,
            out ThingDef selected)
        {
            selected = null;
            if (def == null || map == null) return false;
            if (!def.MadeFromStuff)
                return CostsAvailable(map, def, null);
            ThingDef defaultStuff = GenStuff.DefaultStuffFor(def);
            if (defaultStuff != null
                && CostsAvailable(map, def, defaultStuff))
            {
                selected = defaultStuff;
                return true;
            }

            // Match RimWorld's unattended/default suggestion boundary. Rare
            // prestige and strategic stuffs remain valid manual choices, but
            // mere availability never makes them an automatic substitute for
            // practical room or storage furniture.
            List<ThingDef> candidates = GenStuff.AllowedStuffsFor(def)
                .Where(stuff => stuff?.stuffProps != null
                    && stuff.stuffProps.canSuggestUseDefaultStuff
                    && CostsAvailable(map, def, stuff))
                .OrderByDescending(stuff => stuff.stuffProps.commonality)
                .ThenBy(stuff => stuff.BaseMarketValue)
                .ThenByDescending(stuff => MaterialSurplus(map, def, stuff))
                .ThenBy(stuff => stuff.defName, StringComparer.Ordinal)
                .ToList();
            selected = candidates.FirstOrDefault();
            return selected != null;
        }

        private static int MaterialSurplus(Map map, ThingDef def,
            ThingDef stuff)
        {
            List<ThingDefCountClass> costs;
            try
            {
                costs = def.CostListAdjusted(stuff,
                    errorOnNullStuff: false) ?? new List<ThingDefCountClass>();
            }
            catch
            {
                return int.MinValue;
            }
            ThingDefCountClass material = costs.FirstOrDefault(entry =>
                entry.thingDef == stuff);
            int required = material?.count ?? 0;
            return map.resourceCounter.GetCount(stuff) - required;
        }

        private static bool CostsAvailable(Map map, ThingDef def,
            ThingDef stuff)
        {
            List<ThingDefCountClass> costs;
            try
            {
                costs = def.CostListAdjusted(stuff,
                    errorOnNullStuff: false) ?? new List<ThingDefCountClass>();
            }
            catch
            {
                return false;
            }
            for (int i = 0; i < costs.Count; i++)
                if (map.resourceCounter.GetCount(costs[i].thingDef)
                    < costs[i].count) return false;
            return true;
        }

        private static void AppendStockpile(StringBuilder builder, Map map,
            Zone_Stockpile zone, List<ThingDef> candidates)
        {
            List<IntVec3> cells = OrderedCells(zone.cells, map);
            var field = new CASpatialField(map, cells);
            List<Thing> held = zone.slotGroup?.HeldThings.ToList()
                ?? new List<Thing>();
            int stackSlots = cells.Sum(cell =>
                cell.GetMaxItemsAllowedInCell(map));
            int occupiedStacks = held.Count;
            int units = held.Sum(thing => thing.stackCount);
            List<Building_Storage> storage = map.listerBuildings
                .allBuildingsColonist.OfType<Building_Storage>()
                .Where(building => building.OccupiedRect().Cells.Any(cell =>
                    field.authority.Contains(cell))).OrderBy(building =>
                    building.thingIDNumber).ToList();
            string contents = DefinitionSummary(held, 10);
            string roles = string.Join(", ", cells.Select(cell =>
                    cell.GetRoom(map)).Where(room => room != null)
                .Distinct().OrderBy(room => room.ID).Select(room => "#"
                    + room.ID + " " + (room.Role?.defName ?? "none"))
                .ToArray());

            builder.Append("    zone #").Append(zone.ID).Append(" '")
                .Append(zone.label).Append("': native priority ")
                .Append(zone.settings.Priority.Label()).Append(", cells ")
                .Append(cells.Count).Append(", allowed loaded definitions ")
                .Append(zone.settings.filter.AllowedDefCount).AppendLine()
                .Append("      present storage: ").Append(occupiedStacks)
                .Append(" stack(s)/").Append(units).Append(" unit(s), native "
                    + "stack slots ").Append(stackSlots).Append(", storage "
                    + "buildings touching current footprint ")
                .Append(storage.Count).Append("; contents [")
                .Append(contents).AppendLine("]")
                .Append("      spatial axes: doorway approaches ")
                .Append(field.doorwayApproaches.Count).Append(", existing "
                    + "service cells ").Append(field.serviceCells.Count)
                .Append(", sleep approaches ")
                .Append(field.sleepApproaches.Count).Append(", clear negative "
                    + "space cells ").Append(field.clearCells)
                .Append("; native rooms ").Append(roles.NullOrEmpty()
                    ? "none" : roles).AppendLine();

            if (candidates.Count == 0)
            {
                builder.AppendLine("      capacity candidates: no loaded, "
                    + "player-buildable Building_Storage definition with more "
                    + "than one stack per cell; no candidate invented");
                return;
            }

            builder.AppendLine("      capacity candidates (each remains "
                + "advisory; mixed contents and empty cells are not defects):");
            for (int i = 0; i < candidates.Count; i++)
            {
                ThingDef def = candidates[i];
                CAPlacementProbe probe = Probe(map, field, def, null, null,
                    zone);
                int nativeCapacity = def.building.maxItemsInCell
                    * def.Size.Area;
                builder.Append("        ").Append(def.defName).Append(" (")
                    .Append(def.label).Append(", source ")
                    .Append(SourceReceipt(def)).Append("): native capacity ")
                    .Append(nativeCapacity).Append(" stack(s) per building; ")
                    .Append(ProbeReceipt(map, field, probe, zone))
                    .AppendLine();
            }
        }

        private static void AppendProgram(StringBuilder builder, Map map,
            CASpaceProgram program, List<ThingDef> ambientContributors,
            HashSet<Room> programmedRooms)
        {
            List<IntVec3> cells = OrderedCells(program.cells, map);
            Dictionary<Room, List<IntVec3>> rooms = cells
                .Select(cell => new { cell, room = cell.GetRoom(map) })
                .Where(entry => entry.room != null)
                .GroupBy(entry => entry.room)
                .ToDictionary(group => group.Key,
                    group => group.Select(entry => entry.cell).ToList());
            foreach (Room room in rooms.Keys) programmedRooms.Add(room);
            int noRoom = cells.Count(cell => cell.GetRoom(map) == null);
            string name = program.label.NullOrEmpty()
                ? CASpacePurposeInfo.Label(program.purpose) : program.label;
            builder.Append("    program #").Append(program.id).Append(" '")
                .Append(name).Append("' (")
                .Append(CASpacePurposeInfo.Label(program.purpose))
                .Append("): exact in-bounds cells ").Append(cells.Count)
                .Append(", native rooms ").Append(rooms.Count)
                .Append(", cells without a native room ").Append(noRoom)
                .Append(", style ").Append(CASpacePurposeInfo.StyleLabel(
                    program.style)).Append(", room consumer ")
                .Append(CanOriginateNewRoomFacility(program)
                    ? "operative" : program.purpose == CASpacePurpose.Bedroom
                        ? "read-only; construction disabled"
                        : "not operative").AppendLine();
            if (rooms.Count == 0)
            {
                builder.AppendLine("      no native room intersects this exact "
                    + "authored footprint; classification and furnishing "
                    + "opportunities remain absent");
                return;
            }
            foreach (KeyValuePair<Room, List<IntVec3>> entry in rooms
                .OrderBy(pair => pair.Key.ID))
            {
                builder.Append("      ");
                AppendRoomEvidence(builder, map, entry.Key, entry.Value,
                    program, ambientContributors, probeCandidates: true);
            }
        }

        private static void AppendRoomEvidence(StringBuilder builder, Map map,
            Room room, List<IntVec3> authorityCells, CASpaceProgram program,
            List<ThingDef> ambientContributors, bool probeCandidates)
        {
            var field = new CASpatialField(map, authorityCells);
            RoomRoleDef role = room.Role;
            string authority = program == null
                ? "no CASpaceProgram authority"
                : "program #" + program.id + " owns "
                    + authorityCells.Count + " exact cell(s)";
            builder.Append("room #").Append(room.ID).Append(" at ")
                .Append(RoomAnchor(room)).Append(": native role ")
                .Append(role?.defName ?? "none").Append("; ")
                .Append(authority).AppendLine()
                .Append("        room-stat axes: ")
                .Append(RoomStatsReceipt(room)).AppendLine()
                .Append("        spatial axes: room cells ")
                .Append(room.CellCount).Append(", authority cells ")
                .Append(field.authority.Count).Append(", doorway approaches ")
                .Append(field.doorwayApproaches.Count).Append(", service cells ")
                .Append(field.serviceCells.Count).Append(", sleep approaches ")
                .Append(field.sleepApproaches.Count).Append(", clear negative "
                    + "space ").Append(field.clearCells).AppendLine();

            List<Building> buildings = map.listerBuildings.allBuildingsColonist
                .Where(building => building != null && building.Spawned
                    && building.GetRoom() == room
                    && field.authority.Contains(building.Position))
                .OrderBy(building => building.thingIDNumber).ToList();
            List<Building> roleTables = buildings.Where(building =>
                building.def.building?.workTableRoomRole != null).ToList();
            if (roleTables.Count == 0)
                builder.AppendLine("        native role multipliers: no "
                    + "worktable with a required native room role observed");
            else
            {
                builder.AppendLine("        native role multipliers:");
                for (int i = 0; i < roleTables.Count; i++)
                {
                    Building table = roleTables[i];
                    bool penalty = StatPart_WorkTableRoomRole.Applies(table);
                    builder.Append("          ").Append(table.def.defName)
                        .Append(" #").Append(table.thingIDNumber).Append(" at ")
                        .Append(table.Position).Append(": requires ")
                        .Append(table.def.building.workTableRoomRole.defName)
                        .Append(", current ").Append(role?.defName ?? "none")
                        .Append(", native out-of-role factor ")
                        .Append(table.def.building.workTableNotInRoomRoleFactor
                            .ToString("F2"))
                        .Append(", penalty active ").Append(YesNo(penalty))
                        .AppendLine();
                }
            }

            List<Thing> focuses = buildings.Cast<Thing>().Where(thing => thing
                .TryGetComp<CompAffectedByFacilities>() != null).ToList();
            if (focuses.Count == 0)
                builder.AppendLine("        native facility graph: no affected "
                    + "building observed in this authority footprint");
            else
            {
                builder.AppendLine("        native facility graph:");
                for (int i = 0; i < focuses.Count; i++)
                    AppendFocus(builder, map, field, room, focuses[i],
                        probeCandidates);
            }

            List<Thing> beautyContributors = room.ContainedAndAdjacentThings
                .Where(thing => thing != null && thing.Spawned
                    && field.authority.Contains(thing.Position)
                    && thing.GetStatValue(StatDefOf.Beauty) > 0f)
                .OrderBy(thing => thing.thingIDNumber).ToList();
            builder.Append("        native Beauty-stat telemetry: positive "
                    + "contributors ").Append(beautyContributors.Count)
                .Append(" [").Append(DefinitionSummary(beautyContributors, 8))
                .AppendLine("]; no aesthetic verdict inferred");
            builder.AppendLine("        beauty boundary: no ambient furnishing "
                + "requirement or placement candidate is derived from native "
                + "Beauty. Beauty is not a scalar objective for CA initiative.");
            if (!probeCandidates)
            {
                builder.AppendLine("        candidate boundary: native room "
                    + "classification is observed, but no authored room "
                    + "authority exists; no placement candidate produced");
            }
        }

        private static void AppendFocus(StringBuilder builder, Map map,
            CASpatialField field, Room room, Thing focus,
            bool probeCandidates)
        {
            CompAffectedByFacilities affected =
                focus.TryGetComp<CompAffectedByFacilities>();
            CompProperties_AffectedByFacilities props = focus.def
                .GetCompProperties<CompProperties_AffectedByFacilities>();
            List<Thing> linked = affected.LinkedFacilitiesListForReading
                .Where(thing => thing != null).OrderBy(thing => thing.def.defName)
                .ThenBy(thing => thing.thingIDNumber).ToList();
            builder.Append("          focus ").Append(focus.def.defName)
                .Append(" [source ").Append(SourceReceipt(focus.def))
                .Append("]")
                .Append(" #").Append(focus.thingIDNumber).Append(" at ")
                .Append(focus.Position).Append(": linked [")
                .Append(linked.Count == 0 ? "none" : string.Join(", ", linked
                    .Select(thing => thing.def.defName + " #"
                        + thing.thingIDNumber + " active "
                        + YesNo(affected.IsFacilityActive(thing))).ToArray()))
                .AppendLine("]");
            if (props?.linkableFacilities == null
                || props.linkableFacilities.Count == 0)
            {
                builder.AppendLine("            loaded linkable definitions: "
                    + "none; no relationship invented");
                return;
            }
            foreach (ThingDef facilityDef in props.linkableFacilities
                .Where(def => def != null).OrderBy(def => def.defName))
            {
                CompProperties_Facility facility = facilityDef
                    .GetCompProperties<CompProperties_Facility>();
                int current = linked.Count(thing => thing.def == facilityDef);
                int maximum = facility?.maxSimultaneous ?? 0;
                string offsets = FacilityOffsets(facility);
                builder.Append("            ").Append(facilityDef.defName)
                    .Append(" [source ").Append(SourceReceipt(facilityDef))
                    .Append("]")
                    .Append(": current links ").Append(current).Append("/")
                    .Append(maximum).Append(", loaded effect ")
                    .Append(offsets).Append(", research ready ")
                    .Append(YesNo(facilityDef.IsResearchFinished));
                if (current >= maximum)
                {
                    builder.AppendLine("; native link capacity already met");
                    continue;
                }
                if (!probeCandidates)
                {
                    builder.AppendLine("; missing link observed, but no authored "
                        + "room authority permits a candidate probe");
                    continue;
                }
                CAPlacementProbe probe = Probe(map, field, facilityDef, focus,
                    room, null);
                builder.Append("; ").Append(ProbeReceipt(map, field, probe,
                    null)).AppendLine();
            }
        }

        private static CAPlacementProbe Probe(Map map, CASpatialField field,
            ThingDef def, Thing focus, Room room, Zone_Stockpile owningZone)
        {
            var probe = new CAPlacementProbe
            {
                def = def,
                focus = focus,
                room = room
            };
            ThingDef stuff = ResolveStuff(def);
            List<IntVec3> cells = field.authority.OrderBy(cell => cell.z)
                .ThenBy(cell => cell.x).ToList();
            foreach (IntVec3 center in cells)
            {
                foreach (Rot4 rotation in CandidateRotations(def))
                {
                    probe.total++;
                    CellRect footprint = GenAdj.OccupiedRect(center, rotation,
                        def.Size);
                    List<IntVec3> occupied = footprint.Cells.ToList();
                    if (occupied.Any(cell => !field.authority.Contains(cell)
                        || room != null && cell.GetRoom(map) != room))
                    {
                        probe.outsideAuthority++;
                        continue;
                    }
                    if (occupied.Any(cell =>
                    {
                        Zone zone = map.zoneManager.ZoneAt(cell);
                        return zone != null && zone != owningZone;
                    }))
                    {
                        probe.otherZoneConflict++;
                        continue;
                    }
                    if (occupied.Any(field.doorwayApproaches.Contains))
                    {
                        probe.doorwayConflict++;
                        continue;
                    }
                    if (occupied.Any(field.serviceCells.Contains))
                    {
                        probe.serviceConflict++;
                        continue;
                    }
                    if (occupied.Any(field.sleepApproaches.Contains))
                    {
                        probe.sleepConflict++;
                        continue;
                    }
                    CompAffectedByFacilities affected = focus?
                        .TryGetComp<CompAffectedByFacilities>();
                    if (affected != null && !affected.CanPotentiallyLinkTo(def,
                        center, rotation))
                    {
                        probe.facilityLinkConflict++;
                        continue;
                    }
                    if (def.MadeFromStuff && stuff == null)
                    {
                        probe.noStuff++;
                        continue;
                    }
                    AcceptanceReport native = CASettlementSitingConstraints
                        .CanPlaceNativeBlueprint(map, def, center, rotation,
                            stuff);
                    if (!native.Accepted)
                    {
                        string reason = NativeReason(native.Reason);
                        int count;
                        probe.nativeRejections.TryGetValue(reason, out count);
                        probe.nativeRejections[reason] = count + 1;
                        continue;
                    }

                    probe.accepted++;
                    if (probe.exampleCell.IsValid) continue;
                    probe.exampleCell = center;
                    probe.exampleRotation = rotation;
                    probe.exampleStuff = stuff;
                    probe.exampleZoneTransitionCells = !def.CanOverlapZones
                        ? occupied.Count(cell =>
                            map.zoneManager.ZoneAt(cell) == owningZone
                            && owningZone != null) : 0;
                    for (int i = 0; i < occupied.Count; i++)
                    {
                        List<Thing> things = occupied[i].GetThingList(map);
                        probe.exampleItemStacks += things.Count(thing =>
                            thing.def.category == ThingCategory.Item);
                        probe.exampleItemUnits += things.Where(thing =>
                            thing.def.category == ThingCategory.Item)
                            .Sum(thing => thing.stackCount);
                        if (things.Any(thing => thing is Building
                            || thing is Blueprint || thing is Frame))
                            probe.exampleBuildingCells++;
                    }
                    probe.exampleOpenApproaches = OpenApproaches(map, field,
                        footprint);
                    probe.exampleWallContacts = WallContacts(map, footprint);
                    probe.exampleClearCellsAfter = Math.Max(0,
                        field.clearCells - occupied.Count(cell =>
                            cell.Standable(map) && !HasConstructionAt(map,
                                cell)));
                    List<Building> same = map.listerBuildings
                        .allBuildingsColonist.Where(building => building != null
                            && building.Spawned && building.def == def
                            && field.authority.Contains(building.Position))
                        .ToList();
                    probe.existingSameDef = same.Count;
                    probe.parallelSameDef = same.Count(building =>
                        building.Rotation == rotation
                        || building.Rotation == rotation.Opposite);
                    if (same.Count > 0)
                        probe.nearestSameDef = same.Min(building =>
                            Math.Abs(building.Position.x - center.x)
                            + Math.Abs(building.Position.z - center.z));
                    if (room != null)
                        probe.predictedRole = room
                            .GetRoomRoleIfBuildingPlaced(def);
                }
            }
            return probe;
        }

        private static string ProbeReceipt(Map map, CASpatialField field,
            CAPlacementProbe probe, Zone_Stockpile owningZone)
        {
            var builder = new StringBuilder();
            builder.Append("probes ").Append(probe.total).Append(", native- and "
                    + "spatially-feasible ").Append(probe.accepted)
                .Append("; primary rejection partition [outside exact authority ")
                .Append(probe.outsideAuthority).Append(", other native zone ")
                .Append(probe.otherZoneConflict).Append(", doorway approach ")
                .Append(probe.doorwayConflict).Append(", service cell ")
                .Append(probe.serviceConflict).Append(", sleep approach ")
                .Append(probe.sleepConflict).Append(", native link geometry/")
                .Append("link capacity ").Append(probe.facilityLinkConflict)
                .Append(", no permitted stuff ").Append(probe.noStuff)
                .Append(", native placement ")
                .Append(probe.nativeRejections.Values.Sum()).Append(" ")
                .Append(DictionaryReceipt(probe.nativeRejections)).Append("]");
            if (!probe.exampleCell.IsValid)
            {
                builder.Append("; no example placement selected; capability [")
                    .Append(CapabilityReceipt(map, probe.def,
                        IntVec3.Invalid)).Append("]");
                return builder.ToString();
            }

            CellRect footprint = GenAdj.OccupiedRect(probe.exampleCell,
                probe.exampleRotation, probe.def.Size);
            int baseSlots = owningZone == null ? 0 : footprint.Cells.Sum(cell =>
                cell.GetMaxItemsAllowedInCell(map));
            int candidateSlots = probe.def.building != null
                && typeof(Building_Storage).IsAssignableFrom(probe.def.thingClass)
                ? probe.def.building.maxItemsInCell * probe.def.Size.Area : 0;
            builder.Append("; deterministic example ").Append(probe.exampleCell)
                .Append(" facing ").Append(probe.exampleRotation)
                .Append("; current-use [item stacks ")
                .Append(probe.exampleItemStacks).Append("/units ")
                .Append(probe.exampleItemUnits).Append(", construction cells ")
                .Append(probe.exampleBuildingCells).Append("]")
                .Append("; Feng Shui axes [open cardinal approaches ")
                .Append(probe.exampleOpenApproaches).Append(", wall contacts ")
                .Append(probe.exampleWallContacts).Append(", clear negative "
                    + "space after ").Append(probe.exampleClearCellsAfter)
                .Append(", same-definition pattern ")
                .Append(probe.existingSameDef).Append(", parallel ")
                .Append(probe.parallelSameDef).Append(", nearest ")
                .Append(probe.nearestSameDef == int.MaxValue
                    ? "none" : probe.nearestSameDef + " cells").Append("]");
            if (owningZone != null)
            {
                builder.Append("; native storage transition [current footprint "
                        + "slots ").Append(baseSlots).Append(", candidate slots ")
                    .Append(candidateSlots).Append(", delta ")
                    .Append(candidateSlots - baseSlots).Append(", blueprint "
                        + "CanOverlapZones ").Append(YesNo(probe.def
                            .blueprintDef?.CanOverlapZones ?? probe.def
                            .CanOverlapZones)).Append(", zone cells immediately "
                        + "removed on spawn ")
                    .Append(probe.exampleZoneTransitionCells)
                    .Append(", resulting Building_Storage owns independent "
                        + "native settings, remaining floor stockpile cells "
                        + "coexist; no exact-cell policy handoff authorized]");
            }
            if (probe.room != null)
                builder.Append("; predicted native room role after definition ")
                    .Append(probe.predictedRole?.defName ?? "none");
            builder.Append("; capability [").Append(CapabilityReceipt(map,
                probe.def, probe.exampleCell)).Append("]");
            return builder.ToString();
        }

        private static string RoomStatsReceipt(Room room)
        {
            RoomRoleDef role = room?.Role;
            if (room == null || role == null) return "native role absent";
            List<RoomStatDef> direct = DefDatabase<RoomStatDef>
                .AllDefsListForReading.Where(def => !def.isHidden
                    && role.IsStatRelated(def)).OrderBy(def => def.defName)
                .ToList();
            List<RoomStatDef> dependent = DefDatabase<RoomStatDef>
                .AllDefsListForReading.Where(def => def.isHidden
                    && def.inputStat != null
                    && role.IsStatRelated(def.inputStat))
                .OrderBy(def => def.defName).ToList();
            string directText = direct.Count == 0 ? "none declared"
                : string.Join(", ", direct.Select(def => def.defName + " "
                    + def.ScoreToString(room.GetStat(def))).ToArray());
            string dependentText = dependent.Count == 0 ? "none declared"
                : string.Join(", ", dependent.Select(def => def.defName + " "
                    + room.GetStat(def).ToString("F2") + " from "
                    + def.inputStat.defName).ToArray());
            return "related [" + directText + "]; loaded dependent factors ["
                + dependentText + "] (values are evidence; an active consumer "
                + "is reported separately)";
        }

        private static string CapabilityReceipt(Map map, ThingDef def,
            IntVec3 cell)
        {
            bool tech = TechAllows(map, def);
            List<Pawn> builders = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => CapableBuilder(pawn, def)).ToList();
            int reachable = cell.IsValid ? builders.Count(pawn =>
                cell.InAllowedArea(pawn)
                && pawn.CanReach(cell, PathEndMode.Touch, Danger.Some)) : 0;
            ThingDef stuff = ResolveStuff(def);
            List<ThingDefCountClass> costs;
            try
            {
                costs = def.CostListAdjusted(stuff,
                    errorOnNullStuff: false) ?? new List<ThingDefCountClass>();
            }
            catch
            {
                costs = new List<ThingDefCountClass>();
            }
            string cost = costs.Count == 0 ? "none"
                : string.Join(", ", costs.Select(entry => entry.thingDef.defName
                    + " " + SpawnedCount(map, entry.thingDef) + "/"
                    + entry.count).ToArray());
            return "player-buildable " + YesNo(def.BuildableByPlayer)
                + ", research " + YesNo(def.IsResearchFinished)
                + ", colony tech " + YesNo(tech) + ", enabled skilled builders "
                + builders.Count + (cell.IsValid ? ", reachable " + reachable
                    : ", reach not probed") + ", stuff "
                + (stuff?.defName ?? (def.MadeFromStuff
                    ? "none resolved" : "not required"))
                + ", spawned loose material/cost [" + cost + "]";
        }

        private static bool IsAmbientContributor(ThingDef def)
        {
            if (def == null || def.category != ThingCategory.Building
                || def.building == null
                || !def.BuildableByPlayer || def.blueprintDef == null)
                return false;
            if (def.GetCompProperties<CompProperties_Facility>() != null
                || def.GetCompProperties<
                    CompProperties_AffectedByFacilities>() != null
                || (def.thingClass != null && typeof(Building_Storage)
                    .IsAssignableFrom(def.thingClass)))
                return false;
            ThingCategoryDef furniture = DefDatabase<ThingCategoryDef>
                .GetNamedSilentFail("BuildingsFurniture");
            ThingCategoryDef art = ThingCategoryDefOf.BuildingsArt;
            if ((furniture == null || !def.IsWithinCategory(furniture))
                && (art == null || !def.IsWithinCategory(art)))
                return false;
            ThingDef stuff = ResolveStuff(def);
            float buildingBeauty = def.GetStatValueAbstract(StatDefOf.Beauty,
                stuff);
            ThingDef plant = def.building.defaultPlantToGrow;
            float plantBeauty = plant?.GetStatValueAbstract(StatDefOf.Beauty)
                ?? 0f;
            return buildingBeauty > 0f || plantBeauty > 0f;
        }

        private static bool IsObservedNativeRoom(Map map, Room room)
        {
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors)
                return false;
            RoomRoleDef role = room.Role;
            if (role == null || role == RoomRoleDefOf.None
                || role.defName == "Room") return false;
            bool playerUse = room.Cells.Any(cell => cell.InBounds(map)
                && map.areaManager.Home[cell]);
            if (playerUse) return true;
            List<Thing> things = room.ContainedAndAdjacentThings;
            return things.Any(thing => thing?.Faction == Faction.OfPlayer);
        }

        private static List<IntVec3> OrderedCells(IEnumerable<IntVec3> cells,
            Map map)
        {
            return (cells ?? Enumerable.Empty<IntVec3>())
                .Where(cell => cell.InBounds(map)).Distinct()
                .OrderBy(cell => cell.z).ThenBy(cell => cell.x).ToList();
        }

        private static IEnumerable<Rot4> CandidateRotations(ThingDef def)
        {
            if (!def.rotatable)
            {
                yield return def.defaultPlacingRot;
                yield break;
            }
            yield return Rot4.North;
            yield return Rot4.East;
            yield return Rot4.South;
            yield return Rot4.West;
        }

        private static ThingDef ResolveStuff(ThingDef def)
        {
            if (def == null || !def.MadeFromStuff) return null;
            ThingDef stuff = GenStuff.DefaultStuffFor(def);
            if (stuff != null) return stuff;
            return GenStuff.AllowedStuffsFor(def).OrderBy(candidate =>
                candidate.defName).FirstOrDefault();
        }

        private static bool TechAllows(Map map, ThingDef def)
        {
            return Faction.OfPlayer != null
                && CATechnologicalKnowledgeRuntime.CanConstruct(
                    Faction.OfPlayer, def, out _, map);
        }

        private static bool CapableBuilder(Pawn pawn, ThingDef def)
        {
            return pawn != null && pawn.Spawned && !pawn.Downed
                && !pawn.Drafted && !pawn.InMentalState && pawn.Awake()
                && pawn.workSettings != null
                && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Construction)
                && pawn.skills?.GetSkill(SkillDefOf.Construction)?.Level
                    >= def.constructionSkillPrerequisite;
        }

        private static int SpawnedCount(Map map, ThingDef def)
        {
            return map.listerThings.ThingsOfDef(def).Where(thing =>
                thing != null && thing.Spawned).Sum(thing => thing.stackCount);
        }

        private static ThingStyleDef ResolveRoomNativeStyle(
            CASpaceProgram program, ThingDef def, out string evidence)
        {
            List<Pawn> residents = (program?.residents ?? new List<Pawn>())
                .Where(pawn => pawn != null).ToList();
            List<IGrouping<string, Pawn>> ideoligions = residents
                .Where(pawn => pawn.Ideo != null)
                .GroupBy(pawn => pawn.Ideo.name + " / "
                    + (pawn.Ideo.culture?.defName ?? "no culture"))
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal).ToList();
            List<IGrouping<ThingStyleDef, Pawn>> styleVotes = residents
                .Where(pawn => pawn.Ideo?.GetStyleFor(def) != null)
                .GroupBy(pawn => pawn.Ideo.GetStyleFor(def))
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key.defName,
                    StringComparer.Ordinal).ToList();
            ThingStyleDef selected = styleVotes.FirstOrDefault()?.Key;
            string basis = "resident vote";
            if (selected == null)
            {
                selected = Faction.OfPlayer.ideos?.PrimaryIdeo?
                    .GetStyleFor(def);
                basis = selected == null ? "native default"
                    : "faction-primary fallback";
            }
            string ideoligionReceipt = ideoligions.Count == 0 ? "none"
                : string.Join(", ", ideoligions.Select(group => group.Key
                    + " x" + group.Count()).ToArray());
            string voteReceipt = styleVotes.Count == 0 ? "none"
                : string.Join(", ", styleVotes.Select(group =>
                    group.Key.defName + " x" + group.Count()).ToArray());
            evidence = "resident ideoligions/cultures [" + ideoligionReceipt
                + "], native style votes [" + voteReceipt + "], selected "
                + (selected?.defName ?? "definition default") + " by "
                + basis;
            return selected;
        }

        private static void CostEvidence(ThingDef def, ThingDef stuff,
            out string receipt, out int units)
        {
            List<ThingDefCountClass> costs;
            try
            {
                costs = def.CostListAdjusted(stuff,
                    errorOnNullStuff: false) ?? new List<ThingDefCountClass>();
            }
            catch
            {
                costs = new List<ThingDefCountClass>();
            }
            units = costs.Sum(entry => entry.count);
            receipt = costs.Count == 0 ? "none"
                : string.Join(", ", costs.OrderBy(entry =>
                        entry.thingDef.defName, StringComparer.Ordinal)
                    .Select(entry => entry.thingDef.defName + " "
                        + entry.count).ToArray());
        }

        private static bool UsesNativeBedHeadPosture(
            CompProperties_Facility facility)
        {
            return facility != null
                && (facility.mustBePlacedAdjacentCardinalToBedHead
                    || facility.mustBePlacedAdjacentCardinalToAndFacingBedHead);
        }

        private static bool TryFunctionalPosture(Map map,
            CASpatialField field, ThingDef def,
            CompProperties_Facility facility, CellRect footprint,
            Rot4 rotation, bool nativeBedHeadPosture,
            out int wallBackedCells, out int usableFaceCells,
            out string receipt)
        {
            wallBackedCells = 0;
            usableFaceCells = 0;
            receipt = "no loaded functional posture";
            if (nativeBedHeadPosture)
            {
                wallBackedCells = WallContacts(map, footprint);
                usableFaceCells = OpenApproaches(map, field, footprint);
                receipt = "native bed-head adjacency supplies the object's "
                    + "functional relationship";
                return true;
            }
            if (facility.mustBePlacedAdjacent
                || facility.mustBePlacedFacingThingLinear)
            {
                wallBackedCells = WallContacts(map, footprint);
                usableFaceCells = OpenApproaches(map, field, footprint);
                receipt = "native adjacency/facing geometry supplies the "
                    + "object's functional relationship";
                return true;
            }

            // A distance-only, non-interacted, pass-through furnishing has no
            // native job cell that explains how it belongs in the room. Treat
            // it as edge furniture only when its back is fully supported by a
            // solid room boundary and its oriented face opens into usable room
            // space. Definitions without either native relational geometry or
            // this grounded physical posture remain manual rather than being
            // assigned an invented use by the autonomous consumer.
            if (def.HasSingleOrMultipleInteractionCells
                || def.passability != Traversability.PassThroughOnly
                || facility.maxDistance <= 0f)
                return false;

            List<IntVec3> back = SideAdjacent(footprint,
                rotation.Opposite.FacingCell).ToList();
            List<IntVec3> front = SideAdjacent(footprint,
                rotation.FacingCell).ToList();
            wallBackedCells = back.Count(cell => IsSolidRoomBacking(
                map, field, cell));
            usableFaceCells = front.Count(cell => IsUsableRoomFace(
                map, field, cell));
            if (back.Count == 0 || wallBackedCells != back.Count
                || front.Count == 0 || usableFaceCells != front.Count)
                return false;
            receipt = "distance-linked edge furnishing, fully wall-backed "
                + wallBackedCells + "/" + back.Count + " with a clear "
                + "room-facing side " + usableFaceCells + "/" + front.Count;
            return true;
        }

        private static bool IsSolidRoomBacking(Map map,
            CASpatialField field, IntVec3 cell)
        {
            if (field.authority.Contains(cell)) return false;
            if (!cell.InBounds(map)) return true;
            Building edifice = cell.GetEdifice(map);
            return edifice != null
                && edifice.def.passability == Traversability.Impassable;
        }

        private static bool IsUsableRoomFace(Map map,
            CASpatialField field, IntVec3 cell)
        {
            return cell.InBounds(map) && field.authority.Contains(cell)
                && cell.Standable(map) && !HasSpatialObstructionAt(map, cell);
        }

        private static IEnumerable<IntVec3> SideAdjacent(CellRect footprint,
            IntVec3 direction)
        {
            var cells = new HashSet<IntVec3>();
            foreach (IntVec3 occupied in footprint.Cells)
            {
                IntVec3 adjacent = occupied + direction;
                if (!footprint.Contains(adjacent)) cells.Add(adjacent);
            }
            return cells.OrderBy(cell => cell.z).ThenBy(cell => cell.x);
        }

        private static void AddProtectedTrafficLanes(Map map,
            CASpatialField field)
        {
            var available = new HashSet<IntVec3>(field.authority.Where(cell =>
                cell.Standable(map) && !HasSpatialObstructionAt(map, cell)));
            List<IntVec3> doors = field.doorwayApproaches
                .Where(available.Contains).OrderBy(cell => cell.z)
                .ThenBy(cell => cell.x).ToList();
            List<IntVec3> destinations = field.sleepApproaches
                .Concat(field.serviceCells).Where(available.Contains).Distinct()
                .OrderBy(cell => cell.z).ThenBy(cell => cell.x).ToList();
            for (int i = 0; i < doors.Count; i++)
                field.trafficLanes.Add(doors[i]);

            for (int destinationIndex = 0;
                destinationIndex < destinations.Count; destinationIndex++)
            {
                List<IntVec3> best = null;
                for (int doorIndex = 0; doorIndex < doors.Count; doorIndex++)
                {
                    List<IntVec3> path = ShortestRoomPath(available,
                        doors[doorIndex], destinations[destinationIndex]);
                    if (path.Count == 0) continue;
                    if (best == null || path.Count < best.Count) best = path;
                }
                if (best != null)
                    for (int cellIndex = 0; cellIndex < best.Count; cellIndex++)
                        field.trafficLanes.Add(best[cellIndex]);
            }

            // Door-to-door movement is a primary room route even when no
            // furnishing or bed advertises an interaction cell on that route.
            for (int first = 0; first < doors.Count; first++)
                for (int second = first + 1; second < doors.Count; second++)
                {
                    List<IntVec3> path = ShortestRoomPath(available,
                        doors[first], doors[second]);
                    for (int cellIndex = 0; cellIndex < path.Count; cellIndex++)
                        field.trafficLanes.Add(path[cellIndex]);
                }
        }

        private static List<IntVec3> ShortestRoomPath(
            HashSet<IntVec3> available, IntVec3 start, IntVec3 destination)
        {
            var empty = new List<IntVec3>();
            if (!available.Contains(start) || !available.Contains(destination))
                return empty;
            var visited = new HashSet<IntVec3> { start };
            var previous = new Dictionary<IntVec3, IntVec3>();
            var queue = new Queue<IntVec3>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                IntVec3 cell = queue.Dequeue();
                if (cell == destination) break;
                IEnumerable<IntVec3> ordered = GenAdj.CardinalDirections
                    .Select(direction => cell + direction)
                    .Where(available.Contains)
                    .OrderBy(next => Math.Abs(next.x - destination.x)
                        + Math.Abs(next.z - destination.z))
                    .ThenBy(next => next.z).ThenBy(next => next.x);
                foreach (IntVec3 next in ordered)
                    if (visited.Add(next))
                    {
                        previous[next] = cell;
                        queue.Enqueue(next);
                    }
            }
            if (!visited.Contains(destination)) return empty;
            var path = new List<IntVec3>();
            IntVec3 current = destination;
            path.Add(current);
            while (current != start)
            {
                current = previous[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        private static void AddEstablishedBedBankIntervals(
            CASpatialField field, List<Building_Bed> beds)
        {
            IEnumerable<IGrouping<string, Building_Bed>> banks = beds
                .Where(bed => bed != null && bed.Spawned)
                .GroupBy(bed =>
                {
                    CellRect rect = bed.OccupiedRect();
                    bool horizontal = rect.Width >= rect.Height;
                    return bed.def.defName + "|" + bed.Rotation.AsInt + "|"
                        + (horizontal
                            ? "H|" + rect.minX + "|" + rect.maxX
                            : "V|" + rect.minZ + "|" + rect.maxZ);
                });
            foreach (IGrouping<string, Building_Bed> bank in banks)
            {
                List<CellRect> rects = bank.Select(bed => bed.OccupiedRect())
                    .ToList();
                if (rects.Count < 3) continue;
                bool horizontal = rects[0].Width >= rects[0].Height;
                rects = horizontal
                    ? rects.OrderBy(rect => rect.minZ).ToList()
                    : rects.OrderBy(rect => rect.minX).ToList();
                var gaps = new List<int>();
                for (int i = 0; i < rects.Count - 1; i++)
                {
                    int gap = horizontal
                        ? rects[i + 1].minZ - rects[i].maxZ - 1
                        : rects[i + 1].minX - rects[i].maxX - 1;
                    if (gap > 0) gaps.Add(gap);
                }
                IGrouping<int, int> established = gaps.GroupBy(gap => gap)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key).FirstOrDefault();
                if (established == null || established.Count() < 2) continue;
                int regularGap = established.Key;
                for (int i = 0; i < rects.Count - 1; i++)
                {
                    CellRect first = rects[i];
                    CellRect second = rects[i + 1];
                    int gap = horizontal
                        ? second.minZ - first.maxZ - 1
                        : second.minX - first.maxX - 1;
                    if (gap != regularGap) continue;
                    if (horizontal)
                        for (int z = first.maxZ + 1; z < second.minZ; z++)
                            for (int x = first.minX; x <= first.maxX; x++)
                                field.bedBankIntervals.Add(
                                    new IntVec3(x, 0, z));
                    else
                        for (int x = first.maxX + 1; x < second.minX; x++)
                            for (int z = first.minZ; z <= first.maxZ; z++)
                                field.bedBankIntervals.Add(
                                    new IntVec3(x, 0, z));
                }
            }
        }

        private static bool PreservesCirculation(Map map,
            CASpatialField field, CellRect footprint, out int beforeCount,
            out int afterCount, out int sleepBefore, out int sleepAfter)
        {
            var blocked = new HashSet<IntVec3>(footprint.Cells);
            HashSet<IntVec3> before = ReachableCirculation(map, field, null);
            HashSet<IntVec3> after = ReachableCirculation(map, field, blocked);
            beforeCount = before.Count;
            afterCount = after.Count;
            sleepBefore = field.sleepApproaches.Count(before.Contains);
            sleepAfter = field.sleepApproaches.Count(after.Contains);
            int occupiedBefore = blocked.Count(before.Contains);
            return sleepAfter == sleepBefore
                && afterCount >= beforeCount - occupiedBefore;
        }

        private static HashSet<IntVec3> ReachableCirculation(Map map,
            CASpatialField field, HashSet<IntVec3> blocked)
        {
            var available = new HashSet<IntVec3>(field.authority.Where(cell =>
                cell.Standable(map) && !HasSpatialObstructionAt(map, cell)
                && (blocked == null || !blocked.Contains(cell))));
            List<IntVec3> seeds = field.doorwayApproaches
                .Where(available.Contains).OrderBy(cell => cell.z)
                .ThenBy(cell => cell.x).ToList();
            if (seeds.Count == 0)
                seeds = available.Where(cell => GenAdj.CardinalDirections
                        .Any(direction => !field.authority.Contains(
                            cell + direction)))
                    .OrderBy(cell => cell.z).ThenBy(cell => cell.x).ToList();
            if (seeds.Count == 0 && available.Count > 0)
                seeds.Add(available.OrderBy(cell => cell.z)
                    .ThenBy(cell => cell.x).First());

            var visited = new HashSet<IntVec3>();
            var queue = new Queue<IntVec3>();
            for (int i = 0; i < seeds.Count; i++)
                if (visited.Add(seeds[i])) queue.Enqueue(seeds[i]);
            while (queue.Count > 0)
            {
                IntVec3 cell = queue.Dequeue();
                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 next = cell + GenAdj.CardinalDirections[i];
                    if (available.Contains(next) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }
            return visited;
        }

        private static int OpenApproaches(Map map, CASpatialField field,
            CellRect footprint)
        {
            var approaches = new HashSet<IntVec3>();
            foreach (IntVec3 cell in CardinalAdjacent(footprint))
                if (field.authority.Contains(cell) && cell.Standable(map)
                    && !HasSpatialObstructionAt(map, cell)) approaches.Add(cell);
            return approaches.Count;
        }

        private static int WallContacts(Map map, CellRect footprint)
        {
            int count = 0;
            foreach (IntVec3 cell in CardinalAdjacent(footprint))
            {
                if (!cell.InBounds(map)) continue;
                Building edifice = cell.GetEdifice(map);
                if (edifice != null
                    && edifice.def.passability == Traversability.Impassable)
                    count++;
            }
            return count;
        }

        private static IEnumerable<IntVec3> CardinalAdjacent(
            CellRect footprint)
        {
            var cells = new HashSet<IntVec3>();
            foreach (IntVec3 occupied in footprint)
                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 adjacent = occupied
                        + GenAdj.CardinalDirections[i];
                    if (!footprint.Contains(adjacent)) cells.Add(adjacent);
                }
            return cells.OrderBy(cell => cell.z).ThenBy(cell => cell.x);
        }

        private static bool HasConstructionAt(Map map, IntVec3 cell)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Building || things[i] is Blueprint
                    || things[i] is Frame) return true;
            return false;
        }

        private static bool HasSpatialObstructionAt(Map map, IntVec3 cell)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing is Blueprint || thing is Frame) return true;
                Building building = thing as Building;
                if (building != null && building.def.passability
                    != Traversability.Standable) return true;
            }
            return false;
        }

        private static string FacilityOffsets(CompProperties_Facility props)
        {
            if (props?.statOffsets == null || props.statOffsets.Count == 0)
                return "none declared";
            return string.Join(", ", props.statOffsets.OrderBy(offset =>
                offset.stat.defName).Select(offset => offset.stat.defName + " "
                    + offset.value.ToString("+0.##;-0.##;0")).ToArray());
        }

        private static string DefinitionSummary(IEnumerable<Thing> things,
            int limit)
        {
            List<IGrouping<string, Thing>> groups = things
                .GroupBy(thing => thing.def.defName)
                .OrderByDescending(group => group.Sum(thing =>
                    thing.stackCount)).ThenBy(group => group.Key).ToList();
            if (groups.Count == 0) return "none";
            string text = string.Join(", ", groups.Take(limit).Select(group =>
                group.Key + " " + group.Sum(thing => thing.stackCount))
                .ToArray());
            return text + (groups.Count > limit
                ? ", +" + (groups.Count - limit) + " more" : "");
        }

        private static string DictionaryReceipt(Dictionary<string, int> values)
        {
            if (values.Count == 0) return "{}";
            return "{" + string.Join(", ", values.OrderBy(pair => pair.Key)
                .Select(pair => pair.Key + " " + pair.Value).ToArray()) + "}";
        }

        private static string NativeReason(string reason)
        {
            if (reason.NullOrEmpty()) return "unspecified";
            return reason.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static string SourceReceipt(Def def)
        {
            ModContentPack pack = def?.modContentPack;
            if (pack == null) return "unknown loaded pack";
            return pack.Name + " / " + pack.PackageIdPlayerFacing;
        }

        private static IntVec3 RoomAnchor(Room room)
        {
            return room == null ? IntVec3.Invalid : room.Cells
                .OrderBy(cell => cell.z).ThenBy(cell => cell.x)
                .FirstOrDefault();
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }
    }
}
