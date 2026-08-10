using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public class CASettlementIdeoligionEvidence : IExposable
    {
        public Ideo ideoligion;
        public int ideoligionId = -1;
        public string name;
        public string cultureDefName;
        public string structureMemeDefName;
        public List<string> memeDefNames = new List<string>();
        public int adherents;
        public int assignedRoles;
        public int programResidents;
        public int precepts;
        public int roleSlots;
        public bool primary;

        public void ExposeData()
        {
            Scribe_References.Look(ref ideoligion, "ideoligion");
            Scribe_Values.Look(ref ideoligionId, "ideoligionId", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref cultureDefName, "cultureDefName");
            Scribe_Values.Look(ref structureMemeDefName,
                "structureMemeDefName");
            Scribe_Collections.Look(ref memeDefNames, "memeDefNames",
                LookMode.Value);
            Scribe_Values.Look(ref adherents, "adherents", 0);
            Scribe_Values.Look(ref assignedRoles, "assignedRoles", 0);
            Scribe_Values.Look(ref programResidents, "programResidents", 0);
            Scribe_Values.Look(ref precepts, "precepts", 0);
            Scribe_Values.Look(ref roleSlots, "roleSlots", 0);
            Scribe_Values.Look(ref primary, "primary", false);
            if (memeDefNames == null) memeDefNames = new List<string>();
        }

        internal string StableSignature()
        {
            return ideoligionId + ":" + (name ?? "") + ":"
                + (cultureDefName ?? "") + ":"
                + (structureMemeDefName ?? "") + ":"
                + string.Join(",", memeDefNames.ToArray()) + ":"
                + adherents + ":" + assignedRoles + ":"
                + programResidents + ":" + precepts + ":"
                + roleSlots + ":" + primary;
        }

        internal string CensusEntry()
        {
            string label = name.NullOrEmpty()
                ? "Ideoligion #" + ideoligionId : name;
            string memes = memeDefNames.Count == 0
                ? "no memes recorded"
                : string.Join(", ", memeDefNames.ToArray());
            return label + " [adherents " + adherents
                + ", assigned roles " + assignedRoles
                + ", program residents " + programResidents
                + ", precepts " + precepts
                + ", role slots " + roleSlots
                + (cultureDefName.NullOrEmpty()
                    ? "" : ", culture " + cultureDefName)
                + (structureMemeDefName.NullOrEmpty()
                    ? "" : ", structure " + structureMemeDefName)
                + (primary ? ", current primary" : ", minority")
                + ", memes " + memes + "]";
        }
    }

    internal struct CASettlementPlacementEvidence
    {
        public float semanticLegibility;
        public float socialFit;
        public float ideoligionExpression;
        public float environmentalFit;
        public float operationalCoherence;
        public float historicalContinuity;
        public float strategicTopology;

        public float Total => semanticLegibility + socialFit
            + ideoligionExpression + environmentalFit
            + operationalCoherence + historicalContinuity
            + strategicTopology;

        internal string Receipt()
        {
            return "legibility " + Signed(semanticLegibility)
                + ", social " + Signed(socialFit)
                + ", ideoligion " + Signed(ideoligionExpression)
                + ", environment " + Signed(environmentalFit)
                + ", operations " + Signed(operationalCoherence)
                + ", history " + Signed(historicalContinuity)
                + ", strategy " + Signed(strategicTopology)
                + ", total " + Signed(Total);
        }

        private static string Signed(float value)
        {
            return (value >= 0f ? "+" : "") + value.ToString("F2");
        }
    }

    // This is a saved settlement context. Its first bounded consumer may re-rank
    // a new Autonomous bed proposal inside a player-authored sleep program. It
    // never changes that program's footprint or parameters, a completed
    // building, or a retained objective.
    public class CASettlementPlanningContextMapComponent : MapComponent
    {
        private const int RefreshIntervalTicks = 7500;
        private const int EnvironmentalMargin = 12;

        private int revision;
        private int firstObservedTick = -1;
        private int lastObservedTick = -1;
        private int lastChangedTick = -1;
        private string signature;

        private int population;
        private int unaffiliatedResidents;
        private int lovePairs;
        private int familyPairs;
        private int assignedRoles;
        private int residentAssignments;

        private int programCount;
        private int playerProgramCount;
        private int residentProgramCount;
        private int programmedCells;
        private string programPurposeSummary;

        private int playerBuildings;
        private int blueprints;
        private int enclosedRooms;
        private int growingZones;
        private int stockpileZones;
        private IntVec3 settlementCenter = IntVec3.Invalid;
        private int settlementSpanWidth;
        private int settlementSpanHeight;

        private int homeCells;
        private int roofedHomeCells;
        private int naturalRoofHomeCells;
        private int waterHomeCells;
        private int sampledContextCells;
        private int sampledWaterCells;
        private int sampledNaturalRoofCells;

        private List<CASettlementIdeoligionEvidence> ideoligions =
            new List<CASettlementIdeoligionEvidence>();
        private int nextRefreshTick;

        public CASettlementPlanningContextMapComponent(Map map) : base(map) { }

        public static CASettlementPlanningContextMapComponent For(Map map)
        {
            return map?.GetComponent<CASettlementPlanningContextMapComponent>();
        }

        public int Revision => revision;
        internal int FirstObservedTick => firstObservedTick;
        internal string EvidenceSignature => signature;

        internal CASettlementPlacementEvidence ScoreSleepPlacement(
            Pawn planner, CASpaceProgram program, Room room, IntVec3 cell,
            Rot4 rotation)
        {
            var result = new CASettlementPlacementEvidence();
            if (program == null || room == null || !cell.IsValid)
                return result;

            IntVec3 programCenter = ProgramCenter(program);
            if (programCenter.IsValid)
            {
                float distance = cell.DistanceTo(programCenter);
                result.semanticLegibility = Mathf.Max(-5f,
                    1f - distance * 0.32f);
            }
            if (program.author == CASpaceAuthor.Player)
                result.semanticLegibility += 0.40f;

            int doorDistance = NearestDoorDistance(cell, room);
            if (program.purpose == CASpacePurpose.Bedroom
                && ProgramContainsLovePair(program))
                result.socialFit += Mathf.Min(doorDistance, 8) * 0.28f;

            Building_Bed socialAnchor = NearestResidentBed(program, cell);
            if (socialAnchor != null)
            {
                float distance = cell.DistanceTo(socialAnchor.Position);
                result.socialFit += Mathf.Clamp(
                    1.50f - Mathf.Abs(distance - 4f) * 0.35f, -1f, 1.50f);
            }

            float treeConnection = RelevantMemeShare(
                "TreeConnection", planner, program);
            float transhumanist = RelevantMemeShare(
                "Transhumanist", planner, program);
            result.ideoligionExpression = treeConnection
                    * Mathf.Min(2.40f, NearbyTreeCount(cell, 8f) * 0.30f)
                + transhumanist * PoweredProximity(cell) * 2f;

            float naturalRoofShare = homeCells > 0
                ? naturalRoofHomeCells / (float)homeCells : 0f;
            bool naturalRoof = cell.GetRoof(map)?.isNatural == true;
            if (naturalRoofShare >= 0.25f)
                result.environmentalFit = naturalRoof ? 1.50f : -0.25f;
            else if (naturalRoofShare <= 0.10f)
                result.environmentalFit = naturalRoof ? -0.20f : 0.35f;

            result.operationalCoherence = Mathf.Clamp(
                1.20f - Mathf.Abs(doorDistance - 4f) * 0.25f,
                -1.50f, 1.20f);

            Building_Bed historicalBed = NearestProgramBed(program, cell);
            if (historicalBed != null)
            {
                float distance = cell.DistanceTo(historicalBed.Position);
                result.historicalContinuity = Mathf.Clamp(
                    1.50f - Mathf.Abs(distance - 4f) * 0.30f,
                    -1.50f, 1.50f);
                if (historicalBed.Rotation == rotation)
                    result.historicalContinuity += 1.25f;
            }

            if (settlementCenter.IsValid)
                result.strategicTopology = Mathf.Max(-4f,
                    1f - cell.DistanceTo(settlementCenter) * 0.04f);
            return result;
        }

        private static IntVec3 ProgramCenter(CASpaceProgram program)
        {
            if (program?.cells == null || program.cells.Count == 0)
                return IntVec3.Invalid;
            long x = 0;
            long z = 0;
            for (int i = 0; i < program.cells.Count; i++)
            {
                x += program.cells[i].x;
                z += program.cells[i].z;
            }
            return new IntVec3((int)(x / program.cells.Count), 0,
                (int)(z / program.cells.Count));
        }

        private int NearestDoorDistance(IntVec3 cell, Room room)
        {
            int nearest = int.MaxValue;
            foreach (IntVec3 roomCell in room.Cells)
            {
                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 adjacent = roomCell + GenAdj.CardinalDirections[i];
                    if (!adjacent.InBounds(map)
                        || adjacent.GetDoor(map) == null) continue;
                    nearest = Math.Min(nearest,
                        Mathf.RoundToInt(cell.DistanceTo(adjacent)));
                }
            }
            return nearest == int.MaxValue ? 8 : nearest;
        }

        private static bool ProgramContainsLovePair(CASpaceProgram program)
        {
            if (program?.residents == null) return false;
            for (int i = 0; i < program.residents.Count; i++)
                for (int j = i + 1; j < program.residents.Count; j++)
                    if (program.residents[i] != null
                        && program.residents[j] != null
                        && LovePartnerRelationUtility.LovePartnerRelationExists(
                            program.residents[i], program.residents[j]))
                        return true;
            return false;
        }

        private Building_Bed NearestResidentBed(CASpaceProgram program,
            IntVec3 cell)
        {
            Building_Bed nearest = null;
            float best = float.MaxValue;
            if (program?.residents == null) return null;
            for (int i = 0; i < program.residents.Count; i++)
            {
                Building_Bed bed = program.residents[i]?.ownership?.OwnedBed;
                if (bed == null || !bed.Spawned || bed.Map != map
                    || PlannedUseMapComponent.For(map)?.ProgramAt(bed.Position)
                        != program) continue;
                float distance = cell.DistanceTo(bed.Position);
                if (distance >= best) continue;
                best = distance;
                nearest = bed;
            }
            return nearest;
        }

        private Building_Bed NearestProgramBed(CASpaceProgram program,
            IntVec3 cell)
        {
            Building_Bed nearest = null;
            float best = float.MaxValue;
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building_Bed bed = buildings[i] as Building_Bed;
                if (bed == null || !bed.Spawned
                    || programs?.ProgramAt(bed.Position) != program) continue;
                float distance = cell.DistanceTo(bed.Position);
                if (distance >= best) continue;
                best = distance;
                nearest = bed;
            }
            return nearest;
        }

        private float RelevantMemeShare(string memeDefName, Pawn planner,
            CASpaceProgram program)
        {
            int considered = 0;
            int matching = 0;
            if (program?.residents != null)
            {
                for (int i = 0; i < program.residents.Count; i++)
                {
                    Ideo ideoligion = program.residents[i]?.Ideo;
                    if (ideoligion == null) continue;
                    considered++;
                    if (ideoligion.memes?.Any(meme => meme != null
                        && meme.defName == memeDefName) == true) matching++;
                }
            }
            if (considered == 0 && planner?.Ideo != null)
            {
                considered = 1;
                matching = planner.Ideo.memes?.Any(meme => meme != null
                    && meme.defName == memeDefName) == true ? 1 : 0;
            }
            if (considered > 0) return matching / (float)considered;

            int adherents = 0;
            for (int i = 0; i < ideoligions.Count; i++)
            {
                CASettlementIdeoligionEvidence evidence = ideoligions[i];
                adherents += evidence.adherents;
                if (evidence.memeDefNames.Contains(memeDefName))
                    matching += evidence.adherents;
            }
            return adherents > 0 ? matching / (float)adherents : 0f;
        }

        private int NearbyTreeCount(IntVec3 root, float radius)
        {
            int count = 0;
            int limit = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                Plant plant = cell.InBounds(map) ? cell.GetPlant(map) : null;
                if (plant?.def.plant?.IsTree == true) count++;
            }
            return count;
        }

        private float PoweredProximity(IntVec3 root)
        {
            if (map.powerNetGrid.TransmittedPowerNetAt(root) != null)
                return 1f;
            int limit = GenRadial.NumCellsInRadius(4.9f);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                if (cell.InBounds(map)
                    && map.powerNetGrid.TransmittedPowerNetAt(cell) != null)
                    return 0.55f;
            }
            return 0f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref revision, "CA_settlementContextRevision", 0);
            Scribe_Values.Look(ref firstObservedTick,
                "CA_settlementContextFirstObservedTick", -1);
            Scribe_Values.Look(ref lastObservedTick,
                "CA_settlementContextLastObservedTick", -1);
            Scribe_Values.Look(ref lastChangedTick,
                "CA_settlementContextLastChangedTick", -1);
            Scribe_Values.Look(ref signature, "CA_settlementContextSignature");

            Scribe_Values.Look(ref population, "CA_settlementContextPopulation", 0);
            Scribe_Values.Look(ref unaffiliatedResidents,
                "CA_settlementContextUnaffiliatedResidents", 0);
            Scribe_Values.Look(ref lovePairs, "CA_settlementContextLovePairs", 0);
            Scribe_Values.Look(ref familyPairs,
                "CA_settlementContextFamilyPairs", 0);
            Scribe_Values.Look(ref assignedRoles,
                "CA_settlementContextAssignedRoles", 0);
            Scribe_Values.Look(ref residentAssignments,
                "CA_settlementContextResidentAssignments", 0);

            Scribe_Values.Look(ref programCount,
                "CA_settlementContextProgramCount", 0);
            Scribe_Values.Look(ref playerProgramCount,
                "CA_settlementContextPlayerProgramCount", 0);
            Scribe_Values.Look(ref residentProgramCount,
                "CA_settlementContextResidentProgramCount", 0);
            Scribe_Values.Look(ref programmedCells,
                "CA_settlementContextProgrammedCells", 0);
            Scribe_Values.Look(ref programPurposeSummary,
                "CA_settlementContextProgramPurposeSummary");

            Scribe_Values.Look(ref playerBuildings,
                "CA_settlementContextPlayerBuildings", 0);
            Scribe_Values.Look(ref blueprints,
                "CA_settlementContextBlueprints", 0);
            Scribe_Values.Look(ref enclosedRooms,
                "CA_settlementContextEnclosedRooms", 0);
            Scribe_Values.Look(ref growingZones,
                "CA_settlementContextGrowingZones", 0);
            Scribe_Values.Look(ref stockpileZones,
                "CA_settlementContextStockpileZones", 0);
            Scribe_Values.Look(ref settlementCenter,
                "CA_settlementContextCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref settlementSpanWidth,
                "CA_settlementContextSpanWidth", 0);
            Scribe_Values.Look(ref settlementSpanHeight,
                "CA_settlementContextSpanHeight", 0);

            Scribe_Values.Look(ref homeCells,
                "CA_settlementContextHomeCells", 0);
            Scribe_Values.Look(ref roofedHomeCells,
                "CA_settlementContextRoofedHomeCells", 0);
            Scribe_Values.Look(ref naturalRoofHomeCells,
                "CA_settlementContextNaturalRoofHomeCells", 0);
            Scribe_Values.Look(ref waterHomeCells,
                "CA_settlementContextWaterHomeCells", 0);
            Scribe_Values.Look(ref sampledContextCells,
                "CA_settlementContextSampledCells", 0);
            Scribe_Values.Look(ref sampledWaterCells,
                "CA_settlementContextSampledWaterCells", 0);
            Scribe_Values.Look(ref sampledNaturalRoofCells,
                "CA_settlementContextSampledNaturalRoofCells", 0);
            Scribe_Collections.Look(ref ideoligions,
                "CA_settlementContextIdeoligions", LookMode.Deep);

            if (ideoligions == null)
                ideoligions = new List<CASettlementIdeoligionEvidence>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            nextRefreshTick = Find.TickManager.TicksGame
                + Math.Abs(map.uniqueID % 251);
            Refresh();
        }

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick < nextRefreshTick) return;
            nextRefreshTick = tick + RefreshIntervalTicks
                + Math.Abs(map.uniqueID % 251);
            if (map.mapPawns.FreeColonistsSpawned.Count > 0
                || PlannedUseMapComponent.For(map)?.Programs.Count > 0)
                Refresh();
        }

        public void Refresh()
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn != null && !pawn.Dead)
                .OrderBy(pawn => pawn.thingIDNumber).ToList();
            PlannedUseMapComponent programComponent =
                PlannedUseMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> programs =
                programComponent?.Programs ?? Array.Empty<CASpaceProgram>();

            List<CASettlementIdeoligionEvidence> ideoligionEvidence =
                CollectIdeoligionEvidence(pawns, programs);
            CaptureSocialEvidence(pawns, ideoligionEvidence);
            CaptureProgramEvidence(programs);
            CaptureBuiltAndEnvironmentalEvidence(programs);

            string nextSignature = BuildSignature(ideoligionEvidence);
            if (signature != nextSignature)
            {
                signature = nextSignature;
                revision = Mathf.Max(1, revision + 1);
                lastChangedTick = tick;
            }
            if (firstObservedTick < 0) firstObservedTick = tick;
            lastObservedTick = tick;
            ideoligions = ideoligionEvidence;
        }

        private List<CASettlementIdeoligionEvidence> CollectIdeoligionEvidence(
            List<Pawn> pawns, IReadOnlyList<CASpaceProgram> programs)
        {
            var byIdeoligion = new Dictionary<Ideo,
                CASettlementIdeoligionEvidence>();
            unaffiliatedResidents = 0;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                Ideo ideoligion = pawn.Ideo;
                if (ideoligion == null)
                {
                    unaffiliatedResidents++;
                    continue;
                }
                CASettlementIdeoligionEvidence evidence = GetOrCreate(
                    byIdeoligion, ideoligion);
                evidence.adherents++;
                if (ideoligion.GetRole(pawn) != null)
                    evidence.assignedRoles++;
            }

            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program?.residents == null) continue;
                for (int j = 0; j < program.residents.Count; j++)
                {
                    Pawn resident = program.residents[j];
                    if (resident?.Ideo == null) continue;
                    GetOrCreate(byIdeoligion, resident.Ideo).programResidents++;
                }
            }

            var result = byIdeoligion.Values.ToList();
            result.Sort((left, right) =>
            {
                int order = left.ideoligionId.CompareTo(right.ideoligionId);
                return order != 0 ? order
                    : string.CompareOrdinal(left.name, right.name);
            });
            return result;
        }

        private static CASettlementIdeoligionEvidence GetOrCreate(
            Dictionary<Ideo, CASettlementIdeoligionEvidence> byIdeoligion,
            Ideo ideoligion)
        {
            CASettlementIdeoligionEvidence evidence;
            if (byIdeoligion.TryGetValue(ideoligion, out evidence))
                return evidence;

            var memes = ideoligion.memes == null
                ? new List<string>()
                : ideoligion.memes.Where(meme => meme != null)
                    .Select(meme => meme.defName)
                    .OrderBy(defName => defName, StringComparer.Ordinal)
                    .ToList();
            evidence = new CASettlementIdeoligionEvidence
            {
                ideoligion = ideoligion,
                ideoligionId = ideoligion.id,
                name = ideoligion.name,
                cultureDefName = ideoligion.culture?.defName,
                structureMemeDefName = ideoligion.StructureMeme?.defName,
                memeDefNames = memes,
                precepts = ideoligion.PreceptsListForReading?.Count ?? 0,
                roleSlots = ideoligion.RolesListForReading?.Count ?? 0,
                primary = Faction.OfPlayer?.ideos?.PrimaryIdeo == ideoligion
            };
            byIdeoligion.Add(ideoligion, evidence);
            return evidence;
        }

        private void CaptureSocialEvidence(List<Pawn> pawns,
            List<CASettlementIdeoligionEvidence> ideoligionEvidence)
        {
            population = pawns.Count;
            lovePairs = 0;
            familyPairs = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                for (int j = i + 1; j < pawns.Count; j++)
                {
                    if (LovePartnerRelationUtility.LovePartnerRelationExists(
                        pawns[i], pawns[j])) lovePairs++;
                    if (pawns[i].relations?.FamilyByBlood.Contains(pawns[j])
                        == true) familyPairs++;
                }
            }
            assignedRoles = ideoligionEvidence.Sum(evidence =>
                evidence.assignedRoles);
        }

        private void CaptureProgramEvidence(
            IReadOnlyList<CASpaceProgram> programs)
        {
            programCount = 0;
            playerProgramCount = 0;
            residentProgramCount = 0;
            programmedCells = 0;
            residentAssignments = 0;
            var purposes = new Dictionary<CASpacePurpose, int>();
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program == null || program.cells == null
                    || program.cells.Count == 0) continue;
                programCount++;
                programmedCells += program.cells.Count;
                if (program.author == CASpaceAuthor.Player)
                    playerProgramCount++;
                if (program.author == CASpaceAuthor.Pawn)
                    residentProgramCount++;
                if (program.residents != null)
                    residentAssignments += program.residents.Count(
                        resident => resident != null);
                int count;
                purposes.TryGetValue(program.purpose, out count);
                purposes[program.purpose] = count + 1;
            }
            programPurposeSummary = purposes.Count == 0 ? "none"
                : string.Join(", ", purposes.OrderBy(pair => (int)pair.Key)
                    .Select(pair => CASpacePurposeInfo.Label(pair.Key)
                        + " " + pair.Value).ToArray());
        }

        private void CaptureBuiltAndEnvironmentalEvidence(
            IReadOnlyList<CASpaceProgram> programs)
        {
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            playerBuildings = buildings.Count;
            blueprints = map.listerThings
                .ThingsInGroup(ThingRequestGroup.Blueprint).Count;
            enclosedRooms = 0;
            IReadOnlyList<Room> rooms = map.regionGrid.AllRooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (room != null && room.ProperRoom
                    && !room.PsychologicallyOutdoors
                    && room.ContainedAndAdjacentThings.Any(thing =>
                        thing is Building building
                        && building.Faction == Faction.OfPlayer))
                    enclosedRooms++;
            }

            growingZones = 0;
            stockpileZones = 0;
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] is Zone_Growing) growingZones++;
                if (zones[i] is Zone_Stockpile) stockpileZones++;
            }

            bool hasBounds = false;
            int minX = 0;
            int maxX = 0;
            int minZ = 0;
            int maxZ = 0;
            Action<IntVec3> include = cell =>
            {
                if (!cell.InBounds(map)) return;
                if (!hasBounds)
                {
                    minX = maxX = cell.x;
                    minZ = maxZ = cell.z;
                    hasBounds = true;
                    return;
                }
                minX = Math.Min(minX, cell.x);
                maxX = Math.Max(maxX, cell.x);
                minZ = Math.Min(minZ, cell.z);
                maxZ = Math.Max(maxZ, cell.z);
            };

            for (int i = 0; i < buildings.Count; i++)
                include(buildings[i].Position);
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program?.cells == null) continue;
                for (int j = 0; j < program.cells.Count; j++)
                    include(program.cells[j]);
            }
            for (int i = 0; i < zones.Count; i++)
            {
                Zone zone = zones[i];
                if (!(zone is Zone_Growing) && !(zone is Zone_Stockpile))
                    continue;
                for (int j = 0; j < zone.cells.Count; j++)
                    include(zone.cells[j]);
            }

            if (hasBounds)
            {
                settlementSpanWidth = maxX - minX + 1;
                settlementSpanHeight = maxZ - minZ + 1;
                settlementCenter = new IntVec3((minX + maxX) / 2, 0,
                    (minZ + maxZ) / 2);
            }
            else
            {
                settlementSpanWidth = 0;
                settlementSpanHeight = 0;
                settlementCenter = IntVec3.Invalid;
                minX = maxX = map.Center.x;
                minZ = maxZ = map.Center.z;
            }

            homeCells = 0;
            roofedHomeCells = 0;
            naturalRoofHomeCells = 0;
            waterHomeCells = 0;
            foreach (IntVec3 cell in map.areaManager.Home.ActiveCells)
            {
                homeCells++;
                RoofDef roof = cell.GetRoof(map);
                if (roof != null)
                {
                    roofedHomeCells++;
                    if (roof.isNatural) naturalRoofHomeCells++;
                }
                if (cell.GetTerrain(map).IsWater) waterHomeCells++;
            }

            int sampleMinX = Mathf.Clamp(minX - EnvironmentalMargin,
                0, map.Size.x - 1);
            int sampleMaxX = Mathf.Clamp(maxX + EnvironmentalMargin,
                0, map.Size.x - 1);
            int sampleMinZ = Mathf.Clamp(minZ - EnvironmentalMargin,
                0, map.Size.z - 1);
            int sampleMaxZ = Mathf.Clamp(maxZ + EnvironmentalMargin,
                0, map.Size.z - 1);
            sampledContextCells = 0;
            sampledWaterCells = 0;
            sampledNaturalRoofCells = 0;
            for (int x = sampleMinX; x <= sampleMaxX; x++)
            {
                for (int z = sampleMinZ; z <= sampleMaxZ; z++)
                {
                    var cell = new IntVec3(x, 0, z);
                    sampledContextCells++;
                    if (cell.GetTerrain(map).IsWater) sampledWaterCells++;
                    if (cell.GetRoof(map)?.isNatural == true)
                        sampledNaturalRoofCells++;
                }
            }
        }

        private string BuildSignature(
            List<CASettlementIdeoligionEvidence> ideoligionEvidence)
        {
            var builder = new StringBuilder();
            builder.Append(population).Append('|')
                .Append(unaffiliatedResidents).Append('|')
                .Append(lovePairs).Append('|').Append(familyPairs).Append('|')
                .Append(assignedRoles).Append('|')
                .Append(residentAssignments).Append('|')
                .Append(programCount).Append('|')
                .Append(playerProgramCount).Append('|')
                .Append(residentProgramCount).Append('|')
                .Append(programmedCells).Append('|')
                .Append(programPurposeSummary).Append('|')
                .Append(playerBuildings).Append('|').Append(blueprints).Append('|')
                .Append(enclosedRooms).Append('|').Append(growingZones).Append('|')
                .Append(stockpileZones).Append('|').Append(settlementCenter).Append('|')
                .Append(settlementSpanWidth).Append('|')
                .Append(settlementSpanHeight).Append('|')
                .Append(homeCells).Append('|').Append(roofedHomeCells).Append('|')
                .Append(naturalRoofHomeCells).Append('|')
                .Append(waterHomeCells).Append('|')
                .Append(sampledContextCells).Append('|')
                .Append(sampledWaterCells).Append('|')
                .Append(sampledNaturalRoofCells);
            for (int i = 0; i < ideoligionEvidence.Count; i++)
                builder.Append('|').Append(
                    ideoligionEvidence[i].StableSignature());
            return builder.ToString();
        }

        public string Census()
        {
            Refresh();
            string ideoligionSummary = ideoligions.Count == 0
                ? "none"
                : string.Join("; ", ideoligions.Select(evidence =>
                    evidence.CensusEntry()).ToArray());
            string center = settlementCenter.IsValid
                ? settlementCenter.ToString() : "none";
            return "[CA] settlement planning context: revision " + revision
                + ", observed " + lastObservedTick
                + ", changed " + lastChangedTick
                + ", first observed " + firstObservedTick
                + "\n  identity/ideoligion evidence: population " + population
                + ", unaffiliated " + unaffiliatedResidents
                + ", ideoligions " + ideoligionSummary
                + "\n  social/authority evidence: love pairs " + lovePairs
                + ", family pairs " + familyPairs
                + ", assigned ideoligion roles " + assignedRoles
                + ", program resident assignments " + residentAssignments
                + "\n  semantic-legibility evidence: programs " + programCount
                + " (player " + playerProgramCount + ", pawn-authored "
                + residentProgramCount + "), cells " + programmedCells
                + ", purposes " + (programPurposeSummary ?? "none")
                + "\n  built/operational evidence: player buildings "
                + playerBuildings + ", blueprints " + blueprints
                + ", enclosed rooms " + enclosedRooms
                + ", growing zones " + growingZones
                + ", stockpiles " + stockpileZones
                + "\n  strategic-topology evidence: center " + center
                + ", occupied span " + settlementSpanWidth + "x"
                + settlementSpanHeight
                + "\n  environmental evidence: Home " + homeCells
                + " cells (roofed " + roofedHomeCells + ", natural roof "
                + naturalRoofHomeCells + ", water " + waterHomeCells
                + "); settlement margin " + sampledContextCells
                + " cells (natural roof " + sampledNaturalRoofCells
                + ", water " + sampledWaterCells + ")"
                + "\n  historical-continuity evidence: saved context revision "
                + revision + " since tick " + firstObservedTick
                + "; new Autonomous Bed proposals inside player-authored sleep "
                + "programs consume the context only to re-rank valid cells";
        }
    }
}
