using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal sealed class CAStorageProfile
    {
        public bool roofPreferred;
        public bool rottable;
        public bool dissolutionSensitive;
        public bool hazardous;
        public bool medical;
        public float deteriorationRate;
        public float flammability;

        public static CAStorageProfile For(ThingDef def)
        {
            if (def == null) return new CAStorageProfile();
            bool rottable = def.GetCompProperties<CompProperties_Rottable>() != null;
            bool dissolving = def.GetCompProperties<CompProperties_Dissolution>()
                != null;
            bool gasOnDamage = def.GetCompProperties<CompProperties_GasOnDamage>()
                != null;
            float deterioration = def.GetStatValueAbstract(
                StatDefOf.DeteriorationRate);
            return new CAStorageProfile
            {
                roofPreferred = deterioration > 0.1f || rottable || dissolving,
                rottable = rottable,
                dissolutionSensitive = dissolving,
                hazardous = dissolving || gasOnDamage,
                medical = IsMedicalSupply(def),
                deteriorationRate = deterioration,
                flammability = def.GetStatValueAbstract(StatDefOf.Flammability)
            };
        }

        internal static bool IsMedicalSupply(ThingDef def)
        {
            return def != null && (def.IsMedicine
                || def.IsDrug && !def.IsNonMedicalDrug);
        }

        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (dissolutionSensitive)
                    parts.Add("dissolution-sensitive; freezing pauses dissolution");
                if (rottable)
                    parts.Add("rottable; temperature changes rot rate");
                if (roofPreferred) parts.Add("exposure-sensitive");
                if (hazardous) parts.Add("hazard-sensitive");
                if (medical) parts.Add("medical");
                if (flammability >= 0.8f) parts.Add("high flammability");
                return parts.Count == 0 ? "outdoor-tolerant"
                    : string.Join(", ", parts.ToArray());
            }
        }
    }

    internal sealed class CAStorageProjection : IExposable
    {
        public int zoneId = -1;
        public string objectiveKey;
        public string materialDefName;
        public string expectedLabel;
        public int requiredUnits;
        public int deficitUnits;
        public int createdTick = -1;
        public bool objectiveClosed;
        public int closedTick = -1;
        public List<IntVec3> cells = new List<IntVec3>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref zoneId, "zoneId", -1);
            Scribe_Values.Look(ref objectiveKey, "objectiveKey");
            Scribe_Values.Look(ref materialDefName, "materialDefName");
            Scribe_Values.Look(ref expectedLabel, "expectedLabel");
            Scribe_Values.Look(ref requiredUnits, "requiredUnits", 0);
            Scribe_Values.Look(ref deficitUnits, "deficitUnits", 0);
            Scribe_Values.Look(ref createdTick, "createdTick", -1);
            Scribe_Values.Look(ref objectiveClosed, "objectiveClosed", false);
            Scribe_Values.Look(ref closedTick, "closedTick", -1);
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            if (cells == null) cells = new List<IntVec3>();
        }
    }

    // Saved provenance for exact-objective construction reserves. This component decides
    // whether an authorized material objective has a destination; RimWorld still
    // owns slot selection, hauling jobs, reservations, and carried stacks.
    internal sealed partial class CAStorageProgramMapComponent : MapComponent
    {
        private const float SearchRadius = 24.9f;
        private const int MaximumBufferCells = 6;
        private const int ClosedBufferGraceTicks = 2500;

        private List<CAStorageProjection> projections =
            new List<CAStorageProjection>();
        private List<string> vetoedObjectiveKeys = new List<string>();
        private string lastOutcome = "no exact-objective construction reserve";
        private int nextAuditTick;
        private int observedResetGeneration;
        private int internallyDeletingZoneId = -1;

        public CAStorageProgramMapComponent(Map map) : base(map) { }

        public static CAStorageProgramMapComponent For(Map map)
        {
            return map?.GetComponent<CAStorageProgramMapComponent>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref projections, "CA_storageProjections",
                LookMode.Deep);
            Scribe_Collections.Look(ref vetoedObjectiveKeys,
                "CA_storageObjectiveVetoes", LookMode.Value);
            Scribe_Values.Look(ref lastOutcome, "CA_storageLastOutcome",
                "no exact-objective construction reserve");
            Scribe_Values.Look(ref nextAuditTick, "CA_storageNextAudit", 0);
            Scribe_Values.Look(ref observedResetGeneration,
                "CA_storageObservedResetGeneration", 0);
            if (projections == null)
                projections = new List<CAStorageProjection>();
            if (vetoedObjectiveKeys == null)
                vetoedObjectiveKeys = new List<string>();
            ExposeInventoryData();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ApplyResetGeneration(AwarenessMod.Settings?
                .autonomousHomePlanningResetGeneration ?? 0);
            AuditProjections();
            RetireLegacyInventoryProjectionOwnership();
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextAuditTick) return;
            nextAuditTick = now + 250;
            AuditProjections();
        }

        public bool EnsureConstructionStaging(CAHomeMaterialDemand demand,
            CAHomeMaterialRequirement requirement, Pawn planner,
            out string outcome)
        {
            if (demand == null || requirement == null || planner == null
                || planner.Map != map)
            {
                outcome = "construction staging lacks an objective or planner";
                lastOutcome = outcome;
                return false;
            }
            ApplyResetGeneration(AwarenessMod.Settings?
                .autonomousHomePlanningResetGeneration ?? 0);
            ThingDef material = DefDatabase<ThingDef>.GetNamedSilentFail(
                requirement.defName);
            if (material == null || !material.EverStorable(willMinifyIfPossible: false))
            {
                outcome = "material " + requirement.defName
                    + " cannot use native storage";
                lastOutcome = outcome;
                return false;
            }

            AuditProjections();
            string key = ObjectiveKey(demand, material);
            CAStorageProjection projection = projections.FirstOrDefault(candidate =>
                candidate != null && candidate.objectiveKey == key);
            if (projection != null)
            {
                Zone_Stockpile retainedZone = FindZone(projection.zoneId);
                if (retainedZone != null)
                {
                    projection.requiredUnits = requirement.required;
                    projection.deficitUnits = requirement.deficit;
                    projection.objectiveClosed = false;
                    projection.closedTick = -1;
                    outcome = "retained " + projection.expectedLabel + " at "
                        + projection.cells.FirstOrDefault();
                    lastOutcome = outcome;
                    return true;
                }
            }

            CAStorageProfile profile = CAStorageProfile.For(material);
            IntVec3 existingCell;
            string existingLabel;
            if (TryFindAcceptingDestination(material, requirement.deficit,
                planner, out existingCell, out existingLabel))
            {
                Zone_Stockpile existingZone = map.zoneManager.ZoneAt(existingCell)
                    as Zone_Stockpile;
                CAStorageProjection reusable = projections.FirstOrDefault(candidate =>
                    candidate != null && existingZone != null
                        && candidate.zoneId == existingZone.ID
                        && candidate.objectiveClosed);
                if (reusable != null)
                {
                    reusable.objectiveKey = key;
                    reusable.requiredUnits = requirement.required;
                    reusable.deficitUnits = requirement.deficit;
                    reusable.objectiveClosed = false;
                    reusable.closedTick = -1;
                    outcome = "reused " + reusable.expectedLabel + " at "
                        + existingCell + " for " + material.label;
                    lastOutcome = outcome;
                    return true;
                }
                outcome = "existing native storage " + existingLabel
                    + ", beginning at " + existingCell + ", accepts "
                    + material.label + "; native filters, aggregate free "
                    + "capacity, and reachability establish availability; this "
                    + "construction-reserve check does not infer thermal readiness";
                lastOutcome = outcome;
                return true;
            }
            if (vetoedObjectiveKeys.Contains(key))
            {
                outcome = "player vetoed an exact-objective construction "
                    + "reserve for this " + material.label + " objective";
                lastOutcome = outcome;
                return false;
            }

            List<IntVec3> cells;
            int cellCount = Mathf.Clamp(Mathf.CeilToInt(requirement.deficit
                / (float)Mathf.Max(1, material.stackLimit)), 1,
                MaximumBufferCells);
            var plannedFootprint = new HashSet<IntVec3> { demand.cell };
            ThingDef provision = DefDatabase<ThingDef>.GetNamedSilentFail(
                demand.provisionDefName);
            if (provision != null)
            {
                plannedFootprint.Clear();
                foreach (IntVec3 cell in GenAdj.OccupiedRect(demand.cell,
                    new Rot4(demand.rotation), provision.Size))
                    plannedFootprint.Add(cell);
            }
            if (!TryChooseBufferCells(demand.cell, planner, profile, cellCount,
                plannedFootprint, out cells))
            {
                outcome = "no safe reachable native stockpile footprint near "
                    + demand.cell + " for " + material.label;
                lastOutcome = outcome;
                return false;
            }

            string label = "Construction reserve: " + material.LabelCap;
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,
                map.zoneManager)
            {
                label = label
            };
            zone.settings.filter.SetDisallowAll();
            zone.settings.filter.SetAllow(material, allow: true);
            zone.settings.Priority = StoragePriority.Important;
            map.zoneManager.RegisterZone(zone);
            for (int i = 0; i < cells.Count; i++) zone.AddCell(cells[i]);

            projection = new CAStorageProjection
            {
                zoneId = zone.ID,
                objectiveKey = key,
                materialDefName = material.defName,
                expectedLabel = label,
                requiredUnits = requirement.required,
                deficitUnits = requirement.deficit,
                createdTick = Find.TickManager.TicksGame,
                objectiveClosed = false,
                closedTick = -1,
                cells = new List<IntVec3>(cells)
            };
            projections.Add(projection);
            outcome = "created native " + label + " with " + cells.Count
                + " cells at " + cells[0] + " (" + profile.Summary + ")";
            lastOutcome = outcome;
            Log.Message("[CA] storage program: " + outcome);
            Messages.Message("Colonist Awareness: " + label + " established.",
                new TargetInfo(cells[0], map), MessageTypeDefOf.SilentInput,
                historical: false);
            return true;
        }

        public void NotifyObjectiveClosed(CAHomeMaterialDemand demand)
        {
            if (demand?.requirements == null) return;
            for (int i = 0; i < demand.requirements.Count; i++)
            {
                ThingDef material = DefDatabase<ThingDef>.GetNamedSilentFail(
                    demand.requirements[i].defName);
                if (material == null) continue;
                string key = ObjectiveKey(demand, material);
                CAStorageProjection projection = projections.FirstOrDefault(
                    candidate => candidate != null
                        && candidate.objectiveKey == key);
                if (projection != null)
                {
                    projection.objectiveClosed = true;
                    projection.closedTick = Find.TickManager.TicksGame;
                }
            }
            AuditProjections();
        }

        public void NotifyPlanningSettingChanged(bool enabled,
            int resetGeneration)
        {
            if (enabled) ApplyResetGeneration(resetGeneration);
        }

        public void NotifyZoneDeleting(Zone zone)
        {
            if (zone == null || zone.Map != map
                || zone.ID == internallyDeletingZoneId) return;
            CAStorageProjection projection = projections.FirstOrDefault(candidate =>
                candidate != null && candidate.zoneId == zone.ID);
            if (projection == null)
            {
                return;
            }
            if (!projection.objectiveClosed
                && !vetoedObjectiveKeys.Contains(projection.objectiveKey))
                vetoedObjectiveKeys.Add(projection.objectiveKey);
            projections.Remove(projection);
            lastOutcome = projection.objectiveClosed
                ? "player removed a completed construction reserve"
                : "player vetoed " + projection.expectedLabel
                    + " for its exact objective";
        }

        public string Census()
        {
            AuditProjections();
            var projectionText = new List<string>();
            for (int i = 0; i < projections.Count; i++)
            {
                CAStorageProjection projection = projections[i];
                Zone_Stockpile zone = FindZone(projection.zoneId);
                projectionText.Add(projection.expectedLabel + " zone "
                    + projection.zoneId + " cells " + projection.cells.Count
                    + ", demand " + projection.requiredUnits + ", deficit "
                    + projection.deficitUnits + ", "
                    + (projection.objectiveClosed ? "objective closed" : "active")
                    + ", held " + (zone?.slotGroup.HeldThings.Sum(thing =>
                        thing.stackCount) ?? 0));
            }

            int nativeStockpiles = map.zoneManager.AllZones
                .Count(zone => zone is Zone_Stockpile);
            return "[CA] storage programs: native stockpiles "
                + nativeStockpiles + "; exact-objective construction reserves "
                + projections.Count + " [" + (projectionText.Count == 0
                    ? "none" : string.Join("; ", projectionText.ToArray()))
                + "]; player vetoes " + vetoedObjectiveKeys.Count
                + "; last " + lastOutcome + "; " + InventoryCensus();
        }

        internal bool DebugDeleteFirstActiveProjection(out string outcome)
        {
            AuditProjections();
            CAStorageProjection projection = projections.FirstOrDefault(candidate =>
                candidate != null && !candidate.objectiveClosed);
            Zone_Stockpile zone = projection == null
                ? null : FindZone(projection.zoneId);
            if (projection == null || zone == null)
            {
                outcome = "no active exact-objective construction reserve";
                return false;
            }

            string key = projection.objectiveKey;
            string label = projection.expectedLabel;
            zone.Delete(playSound: false);
            bool vetoed = vetoedObjectiveKeys.Contains(key);
            outcome = "deleted " + label + " through native Zone.Delete; exact "
                + "objective veto " + (vetoed ? "recorded" : "missing");
            return vetoed;
        }

        internal bool DebugEditFirstActiveProjection(out string outcome)
        {
            AuditProjections();
            CAStorageProjection projection = projections.FirstOrDefault(candidate =>
                candidate != null && !candidate.objectiveClosed);
            Zone_Stockpile zone = projection == null
                ? null : FindZone(projection.zoneId);
            if (projection == null || zone == null)
            {
                outcome = "no active exact-objective construction reserve";
                return false;
            }

            int zoneId = zone.ID;
            string key = projection.objectiveKey;
            zone.label = "Player-owned construction reserve";
            AuditProjections();
            bool transferred = FindZone(zoneId) != null
                && projections.All(candidate => candidate == null
                    || candidate.zoneId != zoneId)
                && vetoedObjectiveKeys.Contains(key);
            outcome = "renamed native zone " + zoneId
                + "; player-authority transfer "
                + (transferred ? "recorded" : "missing");
            return transferred;
        }

        internal void DebugCounts(out int activeProjections,
            out int vetoCount)
        {
            AuditProjections();
            activeProjections = projections.Count(candidate => candidate != null
                && !candidate.objectiveClosed);
            vetoCount = vetoedObjectiveKeys.Count;
        }

        private void AuditProjections()
        {
            for (int i = projections.Count - 1; i >= 0; i--)
            {
                CAStorageProjection projection = projections[i];
                if (projection == null)
                {
                    projections.RemoveAt(i);
                    continue;
                }
                Zone_Stockpile zone = FindZone(projection.zoneId);
                if (zone == null)
                {
                    if (!projection.objectiveClosed
                        && !vetoedObjectiveKeys.Contains(projection.objectiveKey))
                        vetoedObjectiveKeys.Add(projection.objectiveKey);
                    projections.RemoveAt(i);
                    continue;
                }
                if (!ProjectionStillOwned(projection, zone))
                {
                    if (!projection.objectiveClosed
                        && !vetoedObjectiveKeys.Contains(projection.objectiveKey))
                        vetoedObjectiveKeys.Add(projection.objectiveKey);
                    lastOutcome = projection.expectedLabel
                        + " transferred to player authority after an edit";
                    projections.RemoveAt(i);
                    continue;
                }
                if (!projection.objectiveClosed
                    || zone.slotGroup.HeldThings.Any()) continue;
                if (projection.closedTick < 0
                    || Find.TickManager.TicksGame < projection.closedTick
                        + ClosedBufferGraceTicks)
                    continue;
                internallyDeletingZoneId = zone.ID;
                try
                {
                    zone.Delete(playSound: false);
                }
                finally
                {
                    internallyDeletingZoneId = -1;
                }
                projections.RemoveAt(i);
                lastOutcome = projection.expectedLabel
                    + " retired after its objective closed and the buffer emptied";
            }
        }

        private bool ProjectionStillOwned(CAStorageProjection projection,
            Zone_Stockpile zone)
        {
            ThingDef material = DefDatabase<ThingDef>.GetNamedSilentFail(
                projection.materialDefName);
            if (material == null || zone.label != projection.expectedLabel
                || zone.settings.Priority != StoragePriority.Important
                || zone.settings.filter.AllowedDefCount != 1
                || !zone.settings.filter.Allows(material)
                || zone.cells.Count != projection.cells.Count)
                return false;
            var expected = new HashSet<IntVec3>(projection.cells);
            for (int i = 0; i < zone.cells.Count; i++)
                if (!expected.Contains(zone.cells[i])) return false;
            return true;
        }

        private bool TryFindAcceptingDestination(ThingDef material,
            int requiredCapacity, Pawn planner, out IntVec3 cell,
            out string label)
        {
            Thing sample = ThingMaker.MakeThing(material);
            IntVec3 firstCell = IntVec3.Invalid;
            string firstLabel = null;
            int acceptingCapacity = 0;
            int acceptingGroups = 0;
            List<SlotGroup> groups = map.haulDestinationManager
                .AllGroupsListInPriorityOrder;
            for (int i = 0; i < groups.Count; i++)
            {
                SlotGroup group = groups[i];
                if (group == null || !group.parent.HaulDestinationEnabled
                    || !group.Settings.AllowedToAccept(material)) continue;
                bool groupContributes = false;
                List<IntVec3> cells = group.CellsList;
                for (int j = 0; j < cells.Count; j++)
                {
                    IntVec3 candidate = cells[j];
                    if (!StoreUtility.IsGoodStoreCell(candidate, map, sample,
                            planner, Faction.OfPlayer)) continue;
                    int capacity = candidate.GetItemStackSpaceLeftFor(map,
                        material);
                    if (capacity <= 0) continue;
                    if (!groupContributes)
                    {
                        acceptingGroups++;
                        groupContributes = true;
                    }
                    if (!firstCell.IsValid)
                    {
                        firstCell = candidate;
                        firstLabel = group.GetName();
                    }
                    acceptingCapacity += capacity;
                    if (acceptingCapacity < requiredCapacity) continue;
                    cell = firstCell;
                    label = acceptingGroups + " accepting native destination"
                        + (acceptingGroups == 1 ? "" : "s") + ", first '"
                        + firstLabel + "' (" + acceptingCapacity
                        + " aggregate free units)";
                    return true;
                }
            }
            cell = IntVec3.Invalid;
            label = null;
            return false;
        }

        private bool TryChooseBufferCells(IntVec3 objective, Pawn planner,
            CAStorageProfile profile, int count,
            HashSet<IntVec3> plannedFootprint, out List<IntVec3> result)
        {
            var candidates = new List<IntVec3>();
            int limit = GenRadial.NumCellsInRadius(SearchRadius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = objective + GenRadial.RadialPattern[i];
                if (EligibleBufferCell(cell, planner, plannedFootprint))
                    candidates.Add(cell);
            }
            candidates.Sort((left, right) => BufferScore(left, objective, profile)
                .CompareTo(BufferScore(right, objective, profile)));
            var candidateSet = new HashSet<IntVec3>(candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                var selected = new List<IntVec3> { candidates[i] };
                var selectedSet = new HashSet<IntVec3>(selected);
                while (selected.Count < count)
                {
                    IntVec3 next = IntVec3.Invalid;
                    float bestScore = float.MaxValue;
                    for (int j = 0; j < selected.Count; j++)
                    {
                        for (int direction = 0; direction < 4; direction++)
                        {
                            IntVec3 adjacent = selected[j]
                                + GenAdj.CardinalDirections[direction];
                            if (!candidateSet.Contains(adjacent)
                                || selectedSet.Contains(adjacent)) continue;
                            float score = BufferScore(adjacent, objective, profile);
                            if (score < bestScore)
                            {
                                bestScore = score;
                                next = adjacent;
                            }
                        }
                    }
                    if (!next.IsValid) break;
                    selected.Add(next);
                    selectedSet.Add(next);
                }
                if (selected.Count == count)
                {
                    result = selected;
                    return true;
                }
            }
            result = null;
            return false;
        }

        private bool EligibleBufferCell(IntVec3 cell, Pawn planner,
            HashSet<IntVec3> plannedFootprint)
        {
            if (!cell.InBounds(map) || plannedFootprint.Contains(cell)
                || cell.Fogged(map)
                || map.zoneManager.ZoneAt(cell) != null
                || !Designator_ZoneAdd.IsZoneableCell(cell, map).Accepted
                || cell.GetTerrain(map).passability == Traversability.Impassable
                || cell.GetEdifice(map) != null || cell.GetFirstItem(map) != null
                || cell.GetPlant(map) != null || cell.ContainsStaticFire(map)
                || cell.IsForbidden(planner)
                || !planner.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                return false;
            CASpaceProgram program = PlannedUseMapComponent.For(map)?.ProgramAt(cell);
            if (program != null && program.purpose != CASpacePurpose.Storage)
                return false;
            return true;
        }

        private float BufferScore(IntVec3 cell, IntVec3 objective,
            CAStorageProfile profile)
        {
            float score = cell.DistanceToSquared(objective);
            CASpaceProgram program = PlannedUseMapComponent.For(map)?.ProgramAt(cell);
            if (program?.purpose == CASpacePurpose.Storage) score -= 80f;
            if (!map.areaManager.Home[cell]) score += 18f;
            bool roofed = cell.Roofed(map);
            bool stagingRoofPreferred = profile.rottable
                || profile.dissolutionSensitive || profile.hazardous || profile.medical
                || profile.deteriorationRate > 1f;
            if (stagingRoofPreferred && !roofed) score += 24f;
            if (!stagingRoofPreferred && roofed) score += 4f;
            Room room = cell.GetRoom(map);
            if (program == null && room != null && !room.UsesOutdoorTemperature)
                score += 12f;
            score += Mathf.Max(0f,
                cell.GetTerrain(map).extraDeteriorationFactor) * 30f;
            score += StableTie(cell);
            return score;
        }

        private Zone_Stockpile FindZone(int id)
        {
            if (id < 0) return null;
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
                if (zones[i]?.ID == id) return zones[i] as Zone_Stockpile;
            return null;
        }

        private void ApplyResetGeneration(int resetGeneration)
        {
            if (observedResetGeneration == resetGeneration) return;
            vetoedObjectiveKeys.Clear();
            observedResetGeneration = resetGeneration;
            lastOutcome = "Home planning authority renewed; exact-objective "
                + "construction-reserve vetoes cleared";
        }

        private static string ObjectiveKey(CAHomeMaterialDemand demand,
            ThingDef material)
        {
            return demand.provisionDefName + "|" + demand.kind + "|"
                + demand.programId + "|" + demand.cell.x + "|" + demand.cell.z
                + "|" + demand.rotation + "|" + material.defName
                + (demand.targetResidentId > 0
                    ? "|resident:" + demand.targetResidentId : "");
        }

        private static float StableTie(IntVec3 cell)
        {
            int hash = cell.x * 73856093 ^ cell.z * 19349663;
            return (hash & 1023) * 0.00001f;
        }
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.Delete),
        new Type[] { typeof(bool) })]
    internal static class Patch_CAStorageZoneDelete
    {
        private static void Prefix(Zone __instance)
        {
            CAStorageProgramMapComponent.For(__instance?.Map)
                ?.NotifyZoneDeleting(__instance);
        }
    }
}
