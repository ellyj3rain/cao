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
    internal enum CASettlementDemandKind : byte
    {
        Unknown = 0,
        FoodPreparation = 1,
        Storage = 2,
        Medicine = 3,
        Production = 4,
        Custody = 5,
        Dining = 6,
        Research = 7,
        Defense = 8,
        Maintenance = 9,
        Access = 10,
        Cultivation = 11
    }

    // A settlement proposal names needs and native assets before authority is
    // evaluated. It is the common factual input for player spatial planning and
    // established-settlement development; neither path gains permission merely
    // because a candidate exists.
    internal sealed class CASettlementDevelopmentProposal
    {
        internal readonly List<CASettlementDemandKind> Demands =
            new List<CASettlementDemandKind>();
        internal readonly List<string> AssetCandidates =
            new List<string>();
        // Creation is governed by the open settlement-program keyspace.
        // These receipts are not collapsed into the later-development demand
        // enum, which remains a separate runtime work vocabulary.
        internal readonly List<string> ProgramKeys = new List<string>();
        internal readonly List<string> ProgramAssetRoles = new List<string>();
        internal readonly List<string> ProgramSpatialRequirements =
            new List<string>();
        internal bool RequiresGroupProvisionRoom;
        internal string FundingBasis;
        internal string MaterialBasis;
        internal bool FundingFeasible;
        internal bool MaterialFeasible;
        internal string LaborBasis;

        internal bool HasCandidate(CASettlementDemandKind demand)
        {
            string prefix = ((int)demand) + "|";
            return AssetCandidates.Any(candidate => candidate != null
                && candidate.StartsWith(prefix, StringComparison.Ordinal));
        }

        internal IEnumerable<ThingDef> Candidates(
            CASettlementDemandKind demand)
        {
            string prefix = ((int)demand) + "|";
            foreach (string candidate in AssetCandidates)
            {
                if (candidate == null || !candidate.StartsWith(prefix,
                        StringComparison.Ordinal)) continue;
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(
                    candidate.Substring(prefix.Length));
                if (def != null) yield return def;
            }
        }

        internal string StableSignature()
        {
            return string.Join(",", ProgramKeys.OrderBy(key => key,
                    StringComparer.Ordinal).ToArray())
                + ":roles=" + string.Join(",", ProgramAssetRoles
                    .OrderBy(role => role, StringComparer.Ordinal).ToArray())
                + ":spatial=" + string.Join(",", ProgramSpatialRequirements
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray())
                + ":groupProvisionRoom=" + RequiresGroupProvisionRoom
                + ":demands=" + string.Join(",", Demands.OrderBy(demand => (int)demand)
                    .Select(demand => ((int)demand).ToString()).ToArray())
                + ":" + string.Join(",", AssetCandidates
                    .OrderBy(candidate => candidate, StringComparer.Ordinal)
                    .ToArray())
                + ":funding=" + FundingFeasible
                + ":material=" + MaterialFeasible;
        }
    }

    internal static class CASettlementAssetRegistry
    {
        private static readonly Dictionary<CASettlementDemandKind, string[]>
            Preferred = new Dictionary<CASettlementDemandKind, string[]>
            {
                { CASettlementDemandKind.FoodPreparation,
                    new[] { "Campfire", "FueledStove", "TableButcher" } },
                { CASettlementDemandKind.Storage,
                    new[] { "Shelf" } },
                { CASettlementDemandKind.Medicine,
                    new[] { "Bedroll", "Bed", "HospitalBed" } },
                { CASettlementDemandKind.Production,
                    new[] { "CraftingSpot", "FueledSmithy" } },
                { CASettlementDemandKind.Custody,
                    new[] { "Bedroll", "Bed" } },
                { CASettlementDemandKind.Dining,
                    new[] { "Table2x2c", "Stool", "DiningChair" } },
                { CASettlementDemandKind.Research,
                    new[] { "SimpleResearchBench" } },
                { CASettlementDemandKind.Defense,
                    new[] { "Sandbags", "Barricade" } }
            };

        internal static List<ThingDef> Candidates(
            CASettlementDemandKind demand)
        {
            if (demand == CASettlementDemandKind.Storage)
            {
                ThingCategoryDef furniture = DefDatabase<ThingCategoryDef>
                    .GetNamedSilentFail("BuildingsFurniture");
                return DefDatabase<ThingDef>.AllDefsListForReading.Where(def =>
                        def != null && def.category == ThingCategory.Building
                        && def.thingClass != null
                        && typeof(Building_Storage).IsAssignableFrom(
                            def.thingClass)
                        && def.BuildableByPlayer && def.blueprintDef != null
                        && def.blueprintDef.thingClass != null
                        && typeof(Blueprint_Storage).IsAssignableFrom(
                            def.blueprintDef.thingClass)
                        && def.building != null
                        && def.building.maxItemsInCell > 1
                        && (furniture == null
                            || def.IsWithinCategory(furniture)))
                    .OrderBy(def => def.defName, StringComparer.Ordinal)
                    .ToList();
            }

            if (!Preferred.TryGetValue(demand, out string[] names))
                return new List<ThingDef>();
            var result = new List<ThingDef>();
            for (int i = 0; i < names.Length; i++)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(
                    names[i]);
                if (def == null || def.category != ThingCategory.Building
                    || !def.BuildableByPlayer || def.blueprintDef == null)
                    continue;
                result.Add(def);
            }
            return result;
        }

        internal static CASettlementDemandKind DemandFor(string defName)
        {
            if (defName.NullOrEmpty()) return CASettlementDemandKind.Unknown;
            foreach (KeyValuePair<CASettlementDemandKind, string[]> pair in
                Preferred)
                if (pair.Value.Contains(defName)) return pair.Key;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def?.thingClass != null
                && typeof(Building_Storage).IsAssignableFrom(def.thingClass))
                return CASettlementDemandKind.Storage;
            return CASettlementDemandKind.Unknown;
        }

        // The open settlement-program key remains the creation authority.
        // This projection exposes only the narrower semantic demands that the
        // later development and player-planning paths genuinely share. An
        // unmapped program stays explicit instead of being coerced into a
        // convenient demand kind.
        internal static CASettlementDemandKind DemandForProgram(
            string programKey)
        {
            if (programKey == CASettlementProgramRegistry.FoodPreparation
                || programKey
                    == CASettlementProgramRegistry.DomesticProvision)
                return CASettlementDemandKind.FoodPreparation;
            if (programKey == CASettlementProgramRegistry.Storage
                || programKey
                    == CASettlementProgramRegistry.AuthorityProvision)
                return CASettlementDemandKind.Storage;
            if (programKey == CASettlementProgramRegistry.Medicine)
                return CASettlementDemandKind.Medicine;
            if (programKey == CASettlementProgramRegistry.Production
                || programKey
                    == CASettlementProgramRegistry.SpecializedIndustry)
                return CASettlementDemandKind.Production;
            if (programKey == CASettlementProgramRegistry.Custody)
                return CASettlementDemandKind.Custody;
            if (programKey == CASettlementProgramRegistry.Gathering
                || programKey
                    == CASettlementProgramRegistry.CommunalProvision)
                return CASettlementDemandKind.Dining;
            if (programKey == CASettlementProgramRegistry.Research)
                return CASettlementDemandKind.Research;
            if (programKey == CASettlementProgramRegistry.Defense)
                return CASettlementDemandKind.Defense;
            if (programKey == CASettlementProgramRegistry.Trade
                || programKey == CASettlementProgramRegistry.Communications
                || programKey == CASettlementProgramRegistry.Transport)
                return CASettlementDemandKind.Access;
            return CASettlementDemandKind.Unknown;
        }

        internal static ThingDef Resolve(CASettlementDemandKind demand,
            string preferredDefName)
        {
            // An explicit loaded def is evidence, even when no CA demand mapping
            // knows its name. Preserve it and let native placement decide. An
            // unknown or unloaded explicit name is a blocker, never a request to
            // substitute storage furniture.
            if (!preferredDefName.NullOrEmpty())
            {
                ThingDef explicitDef = DefDatabase<ThingDef>
                    .GetNamedSilentFail(preferredDefName);
                if (explicitDef != null
                    && explicitDef.category == ThingCategory.Building
                    && explicitDef.BuildableByPlayer
                    && explicitDef.blueprintDef != null)
                    return explicitDef;
                return null;
            }
            if (demand == CASettlementDemandKind.Unknown) return null;
            List<ThingDef> candidates = Candidates(demand);
            return candidates.FirstOrDefault();
        }

        // The authority-neutral typed fact used by player spatial planning and
        // NPC institutional planning. The consumers decide authority separately.
        internal static CASettlementDevelopmentProposal BuildDemandFact(
            CASettlementDemandKind demand)
        {
            var fact = new CASettlementDevelopmentProposal
            {
                FundingFeasible = true,
                FundingBasis = "funding is evaluated by the consuming planning authority",
                MaterialBasis = "loaded native buildable definitions"
            };
            fact.Demands.Add(demand);
            foreach (ThingDef def in Candidates(demand))
                fact.AssetCandidates.Add(((int)demand) + "|" + def.defName);
            fact.MaterialFeasible = fact.HasCandidate(demand)
                && fact.Candidates(demand).Any(def => !def.MadeFromStuff
                    || GenStuff.DefaultStuffFor(def) != null);
            return fact;
        }

        internal static CASettlementDevelopmentProposal BuildCreationProposal(
            CASettlementProgram program, int landCapacity,
            IEnumerable<CAProvisionArrangement> provisions = null)
        {
            List<CASettlementProgramEntry> activeEntries = (program?.entries
                    ?? new List<CASettlementProgramEntry>())
                .Where(entry => entry != null && entry.blocker.NullOrEmpty())
                .ToList();
            string[] fundingSources = activeEntries
                .Where(entry => entry.requiresFunding)
                .Select(entry => entry.fundingSource)
                .Where(source => !source.NullOrEmpty()).Distinct().ToArray();
            string[] materialSources = activeEntries
                .Where(entry => entry.requiresMaterial)
                .Select(entry => entry.materialSource)
                .Where(source => !source.NullOrEmpty()).Distinct().ToArray();
            var proposal = new CASettlementDevelopmentProposal();
            proposal.FundingFeasible = activeEntries.Count > 0
                && activeEntries.All(entry => !entry.requiresFunding
                    || !entry.fundingSource.NullOrEmpty());
            proposal.FundingBasis = activeEntries.Count == 0
                ? "no active settlement program"
                : fundingSources.Length == 0
                    ? "active programs require no separate funding contract"
                    : string.Join("; ", fundingSources);
            proposal.MaterialBasis = landCapacity > 0
                ? (materialSources.Length == 0
                    ? "no complete material source is recorded"
                    : string.Join("; ", materialSources))
                : "no usable authored settlement ground";
            proposal.RequiresGroupProvisionRoom = (provisions
                    ?? Enumerable.Empty<CAProvisionArrangement>()).Any(item =>
                        item != null && item.active
                        && item.populationGroupKey >= 0);
            foreach (CASettlementProgramEntry entry in activeEntries)
            {
                if (entry == null || !entry.blocker.NullOrEmpty()) continue;
                proposal.ProgramKeys.Add(entry.programKey);
                CASettlementProgramDef definition =
                    CASettlementProgramRegistry.Find(entry.programKey);
                CASettlementDemandKind demand = DemandForProgram(
                    entry.programKey);
                if (demand != CASettlementDemandKind.Unknown
                    && !proposal.Demands.Contains(demand))
                    proposal.Demands.Add(demand);
                if (definition?.MaterializeSpatialContract == true)
                    proposal.ProgramSpatialRequirements.Add(entry.programKey
                        + "|" + Math.Max(entry.count, entry.extent));
                int repetitions = Math.Max(1,
                    Math.Max(entry.count, entry.extent));
                for (int repetition = 0; repetition < repetitions;
                    repetition++)
                {
                    int role = 0;
                    foreach (string candidate in entry.selectedCandidates
                        ?? new List<string>())
                    {
                        proposal.ProgramAssetRoles.Add(entry.programKey + "|"
                            + repetition + "|" + role++ + "|" + candidate
                            + "|" + (entry.scope ?? "settlement"));
                        ThingDef resolved = Resolve(demand, candidate);
                        string demandCandidate = ((int)demand) + "|"
                            + resolved?.defName;
                        if (demand != CASettlementDemandKind.Unknown
                            && resolved != null
                            && !proposal.AssetCandidates.Contains(
                                demandCandidate))
                            proposal.AssetCandidates.Add(demandCandidate);
                    }
                }
            }

            proposal.MaterialFeasible = landCapacity > 0
                && proposal.ProgramKeys.Count > 0
                && activeEntries.All(entry => !entry.requiresMaterial
                    || !entry.materialSource.NullOrEmpty())
                && proposal.ProgramKeys.All(key =>
                    CASettlementProgramRegistry.Find(key) != null)
                && proposal.ProgramAssetRoles.All(role =>
                {
                    string[] parts = role.Split(new[] { '|' },
                        StringSplitOptions.None);
                    string defName = parts.Length > 3 ? parts[3] : null;
                    ThingDef def = Resolve(DemandForProgram(parts[0]),
                        defName);
                    return def != null && def.category == ThingCategory.Building
                        && def.BuildableByPlayer && def.blueprintDef != null
                        && (!def.MadeFromStuff
                            || GenStuff.DefaultStuffFor(def) != null);
                });
            return proposal;
        }

        internal static CASettlementDevelopmentProposal CreationFromRecord(
            CARegionalSettlementRecord record)
        {
            if (record == null) return new CASettlementDevelopmentProposal
            {
                FundingBasis = "no creation record",
                MaterialBasis = "no creation record"
            };
            return BuildCreationProposal(record.settlementProgram,
                record.landCapacity, record.provisionArrangements);
        }

        // Creation history is deterministically derived from the confirmed
        // settlement program. It never rewrites the independent facts
        // recorded for later institutional development.
        internal static bool ReconcileRecord(CARegionalSettlementRecord record,
            out string correction)
        {
            correction = null;
            if (record == null) return false;
            CASettlementDevelopmentProposal expected = BuildCreationProposal(
                record.settlementProgram,
                record.landCapacity,
                record.provisionArrangements);
            string signature = expected.StableSignature();
            bool mismatch = record.creationProposalSignature != signature
                || record.creationMaterialFeasible
                    != expected.MaterialFeasible;
            if (mismatch)
            {
                correction = "settlement-program candidates and creation signature were reconciled";
            }
            record.creationProposalSignature = signature;
            record.creationMaterialFeasible = expected.MaterialFeasible;
            record.creationTargetOrDemand = signature;
            return mismatch;
        }

        internal static bool CanSiteCreationDemands(Map map, CellRect rect,
            CASettlementDevelopmentProposal proposal, out string blocker)
        {
            blocker = null;
            if (map == null || rect == CellRect.Empty || proposal == null)
            {
                blocker = "no materialized settlement ground";
                return false;
            }
            var reserved = new HashSet<IntVec3>();
            List<Room> insideRooms = rect.Cells
                .Where(cell => cell.InBounds(map) && cell.Roofed(map))
                .Select(cell => cell.GetRoom(map)).Where(room => room != null
                    && !room.PsychologicallyOutdoors && !room.IsDoorway
                    && room.CellCount >= 6).Distinct().ToList();
            if (proposal.RequiresGroupProvisionRoom
                && insideRooms.Count < 2)
            {
                blocker = "group-specific provision requires a second "
                    + "realized indoor room";
                return false;
            }
            foreach (IGrouping<string, string> contract in proposal
                .ProgramAssetRoles.GroupBy(role =>
                {
                    string[] parts = role.Split(new[] { '|' },
                        StringSplitOptions.None);
                    return (parts.Length > 0 ? parts[0] : "program") + "|"
                        + (parts.Length > 1 ? parts[1] : "0");
                }).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                string firstRole = contract.First();
                string[] firstParts = firstRole.Split(new[] { '|' },
                    StringSplitOptions.None);
                string programKey = firstParts[0];
                string scope = firstParts.Length > 4 ? firstParts[4]
                    : "settlement";
                bool indoor = scope != "settlement perimeter"
                    && scope != "usable settlement ground"
                    && scope != "workable settlement ground";
                IEnumerable<Room> candidateRooms = indoor
                    ? insideRooms : new Room[] { null };
                bool contractFound = false;
                foreach (Room contractRoom in candidateRooms)
                {
                    var trial = new HashSet<IntVec3>(reserved);
                    IntVec3 powerAnchor = IntVec3.Invalid;
                    bool rolesFit = true;
                    foreach (string role in contract.OrderBy(item =>
                    {
                        string[] parts = item.Split(new[] { '|' },
                            StringSplitOptions.None);
                        ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(
                            parts.Length > 3 ? parts[3] : null);
                        return d?.EverTransmitsPower == true ? 0 : 1;
                    }).ThenByDescending(item =>
                    {
                        string[] parts = item.Split(new[] { '|' },
                            StringSplitOptions.None);
                        return DefDatabase<ThingDef>.GetNamedSilentFail(
                            parts.Length > 3 ? parts[3] : null)?.size.Area ?? 0;
                    }))
                    {
                        string[] parts = role.Split(new[] { '|' },
                            StringSplitOptions.None);
                        string defName = parts.Length > 3 ? parts[3] : null;
                        ThingDef def = DefDatabase<ThingDef>
                            .GetNamedSilentFail(defName);
                        if (def == null) { rolesFit = false; break; }
                        ThingDef stuff = def.MadeFromStuff
                            ? GenStuff.DefaultStuffFor(def) : null;
                        IEnumerable<IntVec3> cells = scope
                                == "settlement perimeter"
                            ? rect.Cells.Where(cell =>
                                cell.x <= rect.minX + 2
                                || cell.x >= rect.maxX - 2
                                || cell.z <= rect.minZ + 2
                                || cell.z >= rect.maxZ - 2)
                            : scope == "usable settlement ground"
                                || scope == "workable settlement ground"
                                ? rect.Cells.Where(cell => cell.InBounds(map)
                                    && !cell.Roofed(map))
                                : contractRoom?.Cells
                                    ?? Enumerable.Empty<IntVec3>();
                        if (powerAnchor.IsValid)
                            cells = cells.Where(cell =>
                                cell.DistanceToSquared(powerAnchor) <= 36)
                                .OrderBy(cell => cell.DistanceToSquared(
                                    powerAnchor));
                        IntVec3 selected = IntVec3.Invalid;
                        foreach (IntVec3 cell in cells)
                        {
                            CellRect footprint = GenAdj.OccupiedRect(cell,
                                Rot4.South, def.size);
                            if (footprint.Cells.Any(trial.Contains)) continue;
                            if (!CASettlementSitingConstraints
                                .CanPlaceNativeBlueprint(map, def, cell,
                                    Rot4.South, stuff).Accepted) continue;
                            selected = cell;
                            foreach (IntVec3 occupied in footprint)
                                trial.Add(occupied);
                            break;
                        }
                        if (!selected.IsValid) { rolesFit = false; break; }
                        if (def.EverTransmitsPower) powerAnchor = selected;
                    }
                    if (!rolesFit) continue;
                    reserved = trial;
                    contractFound = true;
                    break;
                }
                if (!contractFound)
                {
                    blocker = "no same-site native placement for "
                        + programKey + " contract";
                    return false;
                }
            }
            foreach (string requirement in proposal
                .ProgramSpatialRequirements)
            {
                string[] parts = requirement.Split(new[] { '|' },
                    StringSplitOptions.None);
                string key = parts[0];
                int extent = parts.Length > 1
                    && int.TryParse(parts[1], out int parsed) ? parsed : 1;
                if (key == CASettlementProgramRegistry.Agriculture)
                {
                    int wanted = Mathf.Clamp(extent * 6, 6, 30);
                    int suitable = rect.Cells.Count(cell => cell.InBounds(map)
                        && cell.Standable(map) && !cell.Roofed(map)
                        && map.zoneManager.ZoneAt(cell) == null
                        && cell.GetFertility(map) >= 0.7f
                        && !reserved.Contains(cell)
                        && !cell.GetThingList(map).Any(thing => thing.def
                            .category == ThingCategory.Building));
                    if (suitable < wanted)
                    {
                        blocker = "no native cultivation ground for " + key;
                        return false;
                    }
                }
            }
            return true;
        }

        // Later development consumes exact work that is available now. Saved
        // program contracts continue to authorize their own research,
        // cultivation, and transport work; damaged or missing represented
        // assets create a maintenance demand. Generic presence creates none.
        internal static CASettlementDevelopmentProposal
            BuildInstitutionalProposal(CARegionalSettlementRecord record,
                CAOrganization organization, Map map)
        {
            var proposal = new CASettlementDevelopmentProposal();
            bool validGround = record != null && organization != null
                && map != null && record.faction != null
                && !record.faction.IsPlayer
                && record.localRect != CellRect.Empty;
            List<Pawn> residents = validGround
                ? CAPopulationProjection.Residents(record, map)
                    .Where(pawn => pawn != null && !pawn.Dead
                        && pawn.Faction == record.faction
                        && pawn.RaceProps.Humanlike && !pawn.IsPrisoner)
                    .ToList()
                : new List<Pawn>();
            int usableCells = validGround ? record.localRect.Cells.Count(
                cell => cell.InBounds(map) && cell.Walkable(map)) : 0;
            int maintenanceTargets = validGround
                ? MaintenanceTargetCount(record, map) : 0;
            CASettlementProgramEntry research = OperationalProgram(record,
                CASettlementProgramRegistry.Research);
            bool qualifiedResearcher = residents.Any(pawn =>
                pawn.skills?.GetSkill(SkillDefOf.Intellectual)?.Level > 0);
            bool researchBench = research != null && HasPlacedAsset(record, map,
                research, thing => thing is Building
                    && thing.def?.defName == "SimpleResearchBench"
                    && thing.Faction == record.faction);
            CASettlementProgramEntry agriculture = OperationalProgram(record,
                CASettlementProgramRegistry.Agriculture);
            bool cultivation = agriculture != null
                && HasPlacedCultivation(record, map, agriculture);
            CASettlementProgramEntry transport = OperationalProgram(record,
                CASettlementProgramRegistry.Transport);

            proposal.FundingFeasible = organization?.treasury > 0f;
            proposal.FundingBasis = organization == null
                ? "no current settlement organization"
                : organization.treasury <= 0f
                    ? "the current settlement organization has no recorded funds"
                : "current institutional treasury "
                    + organization.treasury.ToString("F0")
                    + "; job-specific costs remain action-bound";
            if (maintenanceTargets > 0)
            {
                proposal.Demands.Add(CASettlementDemandKind.Maintenance);
                proposal.AssetCandidates.Add(((int)
                    CASettlementDemandKind.Maintenance)
                    + "|represented-targets:" + maintenanceTargets);
            }
            if (research != null && researchBench && qualifiedResearcher)
            {
                proposal.Demands.Add(CASettlementDemandKind.Research);
                proposal.AssetCandidates.Add(((int)
                    CASettlementDemandKind.Research)
                    + "|program:" + research.signature);
            }
            if (agriculture != null && cultivation)
            {
                proposal.Demands.Add(CASettlementDemandKind.Cultivation);
                proposal.AssetCandidates.Add(((int)
                    CASettlementDemandKind.Cultivation)
                    + "|program:" + agriculture.signature);
            }
            if (transport != null)
            {
                proposal.Demands.Add(CASettlementDemandKind.Access);
                proposal.AssetCandidates.Add(((int)
                    CASettlementDemandKind.Access)
                    + "|program:" + transport.signature);
            }
            proposal.MaterialFeasible = validGround && residents.Count > 0
                && usableCells > 0 && proposal.Demands.Count > 0;
            proposal.LaborBasis = residents.Count == 0
                ? "no typed current settlement residents"
                : "typed settlement residence: " + string.Join(", ",
                    residents.Select(pawn => "pawn:" + pawn.thingIDNumber)
                        .OrderBy(value => value, StringComparer.Ordinal));
            proposal.MaterialBasis = !validGround
                ? "no current materialized settlement ground"
                : "current settlement ground with " + residents.Count
                    + " typed resident workers, " + maintenanceTargets
                    + " maintenance targets, and " + usableCells
                    + " usable cells";
            return proposal;
        }

        private static CASettlementProgramEntry OperationalProgram(
            CARegionalSettlementRecord record, string key)
        {
            return record?.settlementProgram?.Entries(key).FirstOrDefault(
                entry => entry.blocker.NullOrEmpty()
                    && (entry.materializationState == "materialized"
                        || entry.materializationState
                            == "present in saved geography"));
        }

        private static bool HasPlacedAsset(CARegionalSettlementRecord record,
            Map map,
            CASettlementProgramEntry entry, Func<Thing, bool> predicate)
        {
            var ids = new HashSet<string>(
                CASettlementProgramAssets.AssetIds(record, entry),
                StringComparer.Ordinal);
            return ids.Count > 0 && map.listerThings.AllThings.Any(thing =>
                thing != null && ids.Contains(thing.ThingID)
                    && predicate(thing));
        }

        private static bool HasPlacedCultivation(
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramEntry entry)
        {
            var ids = new HashSet<int>(CASettlementProgramAssets
                .AssetIds(record, entry).Where(value => value != null
                        && value.StartsWith("zone:",
                            StringComparison.Ordinal))
                .Select(value => int.TryParse(value.Substring(5),
                    out int id) ? id : -1).Where(id => id >= 0));
            return ids.Count > 0 && map.zoneManager.AllZones.Any(zone =>
                zone is Zone_Growing && ids.Contains(zone.ID));
        }

        private static int MaintenanceTargetCount(
            CARegionalSettlementRecord record, Map map)
        {
            var represented = new HashSet<string>((record.settlementProgram
                    ?.entries ?? new List<CASettlementProgramEntry>())
                .Where(entry => entry != null)
                .SelectMany(entry => CASettlementProgramAssets.AssetIds(
                    record, entry)).Where(value => value != null
                    && !value.StartsWith("zone:",
                        StringComparison.Ordinal)), StringComparer.Ordinal);
            int damaged = map.listerThings.AllThings.OfType<Building>().Count(
                building => represented.Contains(building.ThingID)
                    && building.Faction == record.faction
                    && building.def.useHitPoints
                    && building.HitPoints < building.MaxHitPoints);
            int missing = represented.Count(id => !map.listerThings.AllThings
                .Any(thing => thing?.ThingID == id));
            return damaged + missing;
        }

        internal static bool CanExerciseInstitutionalDevelopment(Map map,
            CARegionalSettlementRecord record,
            CASettlementDevelopmentProposal proposal, out string blocker)
        {
            blocker = null;
            if (map == null || record == null
                || record.localRect == CellRect.Empty || proposal == null)
            {
                blocker = "no current materialized settlement ground";
                return false;
            }
            if (proposal.Demands.Count == 0)
            {
                blocker = "the current settlement has no actionable institutional demand";
                return false;
            }
            foreach (IntVec3 cell in record.localRect)
                if (cell.InBounds(map) && cell.Walkable(map)) return true;
            blocker = "the current settlement has no usable institutional ground";
            return false;
        }

        internal static void RecordInstitutionalFacts(
            CARegionalSettlementRecord record,
            CASettlementDevelopmentProposal proposal, bool sitingFeasible,
            string sitingBlocker)
        {
            if (record == null || proposal == null) return;
            record.developmentDemandKinds = proposal.Demands
                .Select(demand => (int)demand).ToList();
            record.developmentAssetCandidates = proposal.AssetCandidates
                .ToList();
            record.developmentProposalSignature = proposal.StableSignature();
            record.developmentFundingFeasible = proposal.FundingFeasible;
            record.developmentMaterialFeasible = proposal.MaterialFeasible;
            record.developmentFundingBasis = proposal.FundingBasis;
            record.developmentMaterialBasis = proposal.MaterialBasis;
            record.developmentSitingEvaluated = true;
            record.developmentSitingFeasible = sitingFeasible;
            if (!sitingFeasible) record.developmentBlocker = sitingBlocker;
        }

    }

    // Confirmed creation history and later NPC development are separate causal
    // surfaces. Creation reads the authored starting state. Institutional work
    // reads the settlement, people, ground, and material means that exist now.
    internal static class CASettlementInstitutionalAuthorization
    {
        internal static bool TryAuthorizeCreationHistory(
            CARegionalPlan region, CARegionalSettlementPlan settlement,
            CASettlementDevelopmentProposal proposal,
            out CABehaviorDecision decision, out CAIntentContext intent)
        {
            intent = default(CAIntentContext);
            bool confirmed = region != null && region.confirmed
                && settlement != null;
            string candidate = region?.candidateId ?? "unidentified candidate";
            var context = new CABehaviorContext(null,
                CAActorContext.CreationAuthor, CAInitiativeTier.Standard,
                CAAuthorityOrigin.WorldAuthoring,
                authoritySatisfied: confirmed,
                knowledgeSatisfied: proposal != null,
                knowledgeFresh: true, liveValidated: confirmed,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                knowledgeConfidence: confirmed ? 1f : 0f,
                knowledgeUncertainty: confirmed ? 0f : 1f,
                capabilitySatisfied: settlement != null,
                materialSatisfied: proposal != null
                    && proposal.FundingFeasible
                    && proposal.MaterialFeasible,
                currentIntentCompatible: true,
                directPlayerOwnership: false,
                authorityBasis: "confirmed starting-region candidate "
                    + candidate,
                knowledgeBasis: proposal?.StableSignature(),
                owner: "creation author");
            decision = CABehaviorGate.EvaluateForSelection(
                "spatial.creation_authoring", context);
            if (!decision.SelectionApproved) return false;
            intent = CACombatIntent.Authorized(null,
                CAIntentController.SettlementDevelopment,
                "spatial.creation_authoring", CAAuthorityOrigin.WorldAuthoring,
                context.AuthorityBasis, "creation author",
                proposal.StableSignature(), intentOrigin:
                    CAIntentOrigin.WorldAuthoring);
            return true;
        }

        internal static bool TryAuthorizeJob(CARegionalSettlementRecord record,
            CASettlementProgramEntry program, Pawn worker, Job job,
            CASettlementDemandKind demand, string targetOrDemand,
            bool requireMaterializedAssets,
            out CABehaviorDecision decision, out CAIntentContext intent)
        {
            intent = default(CAIntentContext);
            CAOrganization organization = record == null ? null
                : CAOrganizationWorldComponent.Current?.ByKey(
                    record.regionalId + "#" + record.slot);
            Map currentMap = worker?.Map;
            CASettlementDevelopmentProposal proposal =
                CASettlementAssetRegistry.BuildInstitutionalProposal(record,
                    organization, currentMap);
            bool currentSiting = CASettlementAssetRegistry
                .CanExerciseInstitutionalDevelopment(currentMap, record,
                    proposal, out string sitingBlocker);
            CASettlementAssetRegistry.RecordInstitutionalFacts(record,
                proposal, currentSiting, sitingBlocker);
            bool validAuthority = record != null && record.developmentAuthorized
                && record.developmentEpisodeId > 0
                && !record.developmentAuthorityIdentity.NullOrEmpty();
            bool material = proposal.FundingFeasible
                && proposal.MaterialFeasible && currentSiting;
            bool exactDemand = proposal.Demands.Contains(demand);
            bool exactProgram = CASettlementProgramRuntimeContract.TryResolve(
                record, currentMap, program, requireMaterializedAssets,
                out CASettlementProgramRuntimeResolution runtime);
            bool residentWorker = exactProgram
                && CASettlementProgramRuntimeContract.WorkerAuthorized(
                    runtime, worker);
            bool qualified = demand != CASettlementDemandKind.Research
                || worker?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level > 0;
            var context = CABehaviorContext.ForPawn(worker,
                CAAuthorityOrigin.Continuation,
                authoritySatisfied: validAuthority,
                knowledgeSatisfied: exactDemand && exactProgram,
                knowledgeFresh: true, liveValidated: true,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                knowledgeConfidence: 1f, knowledgeUncertainty: 0f,
                capabilitySatisfied: residentWorker && qualified
                    && job != null,
                materialSatisfied: material && exactDemand && exactProgram,
                currentIntentCompatible: worker != null
                    && !worker.Drafted && !worker.InMentalState,
                directPlayerOwnership: false,
                authorityBasis: record?.developmentAuthorityIdentity,
                knowledgeBasis: exactDemand && exactProgram
                    ? demand + " under program " + program.signature
                    : "no current " + demand + " contract",
                owner: record?.developmentOwner
                    ?? "existing settlement institution");
            return CABehaviorJobOrigin.TryAuthorizeAndRegister(worker, job,
                "spatial.npc_settlement_development",
                CAIntentController.SettlementDevelopment, context,
                record?.developmentEpisodeId ?? 0, out decision, out intent,
                targetOrDemand, record?.developmentOwner,
                lifetimeTicks: 12000);
        }

        // Native completion may satisfy the demand that originally justified
        // work, so completion reauthorization checks the exact saved commitment
        // and the institution that still owns it rather than rerolling demand.
        // A job-backed completion additionally requires the central owned-job
        // receipt restored by CABehaviorIntentMapComponent.
        internal static bool TryReauthorizeCompletion(
            CARegionalSettlementRecord record, Map map, Pawn worker, Job job,
            string programKey, string operatorIdentity,
            string programSignature, bool requireMaterializedAssets,
            string behaviorKey, int episodeId,
            CAAuthorityOrigin savedAuthorityOrigin,
            string authorityIdentity, string owner, string targetOrDemand,
            out CABehaviorDecision decision)
        {
            behaviorKey = behaviorKey
                ?? "spatial.npc_settlement_development";
            CAOrganization organization = record == null ? null
                : CAOrganizationWorldComponent.Current?.ByKey(
                    record.regionalId + "#" + record.slot);
            CAAuthorityOrigin currentOrigin = record == null
                ? CAAuthorityOrigin.None
                : (CAAuthorityOrigin)record.developmentAuthorityOrigin;
            CAAuthorityOrigin institutionalOrigins =
                CAAuthorityOrigin.Institutional
                | CAAuthorityOrigin.Household
                | CAAuthorityOrigin.Organization;
            bool savedOriginValid = (savedAuthorityOrigin
                    & (institutionalOrigins | CAAuthorityOrigin.Continuation
                        | CAAuthorityOrigin.SaveRestore)) != 0;
            CASettlementProgramEntry exactProgram = record?.settlementProgram
                ?.Entries(programKey).FirstOrDefault(entry => entry != null
                    && entry.operatorIdentity == operatorIdentity
                    && entry.signature == programSignature);
            bool programOperational =
                CASettlementProgramRuntimeContract.TryResolve(record, map,
                    exactProgram, requireMaterializedAssets,
                    out CASettlementProgramRuntimeResolution runtime)
                && (worker == null
                    || CASettlementProgramRuntimeContract.WorkerAuthorized(
                        runtime, worker));
            bool currentAuthority = record != null && map != null
                && organization != null && record.faction != null
                && !record.faction.IsPlayer && record.developmentAuthorized
                && record.developmentBehaviorKey == behaviorKey
                && record.developmentEpisodeId == episodeId
                && episodeId > 0
                && savedOriginValid
                && (currentOrigin & institutionalOrigins) != 0
                && record.developmentAuthorityIdentity == authorityIdentity
                && record.developmentOwner == owner
                && organization.organizationKey == owner
                && !authorityIdentity.NullOrEmpty()
                && !owner.NullOrEmpty()
                && programOperational
                && CommitmentStillExists(record, map, targetOrDemand);

            bool ownedJob = job == null && worker == null;
            if (job != null || worker != null)
            {
                CAIntentContext saved = default(CAIntentContext);
                ownedJob = worker != null && job != null
                    && worker.Map == map && worker.Faction == record?.faction
                    && CABehaviorIntentMapComponent.For(map)
                        ?.TryGet(worker, job, out saved) == true
                    && saved.BehaviorKey == behaviorKey
                    && saved.EpisodeId == episodeId
                    && saved.AuthorityOrigin == savedAuthorityOrigin
                    && saved.AuthorityIdentity == authorityIdentity
                    && saved.OwnershipScope == owner
                    && saved.OwnerId == worker.thingIDNumber;
            }

            CAActorContext actorContext = worker == null
                ? CAActorContext.NPCSettlement | CAActorContext.NPCInstitution
                : CAActorContext.NPCPawn;
            var context = new CABehaviorContext(worker, actorContext,
                CAInitiativeTier.Standard, currentOrigin,
                authoritySatisfied: currentAuthority && ownedJob,
                knowledgeSatisfied: currentAuthority && ownedJob,
                knowledgeFresh: true, liveValidated: true,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                knowledgeConfidence: currentAuthority && ownedJob ? 1f : 0f,
                knowledgeUncertainty: currentAuthority && ownedJob ? 0f : 1f,
                capabilitySatisfied: job == null
                    ? map != null : worker != null && !worker.Dead
                        && !worker.Downed && !worker.InMentalState,
                materialSatisfied: true,
                currentIntentCompatible: currentAuthority && ownedJob,
                directPlayerOwnership: false,
                authorityBasis: authorityIdentity,
                knowledgeBasis: "exact saved institutional commitment for "
                    + (targetOrDemand ?? "native completion"),
                owner: owner);
            decision = CABehaviorGate.EvaluateForSelection(behaviorKey,
                context);
            return decision.SelectionApproved;
        }

        internal static bool TryAuthorizeLaterDevelopment(
            CARegionalSettlementRecord record, CAOrganization organization,
            CASettlementDevelopmentProposal proposal,
            out CABehaviorDecision decision, out CAIntentContext intent)
        {
            intent = default(CAIntentContext);
            string authorityIdentity = record?.faction?.Name
                ?? organization?.name ?? "unnamed settlement";
            bool valid = record != null && organization != null
                && record.faction != null && !record.faction.IsPlayer
                && proposal?.Demands?.Count > 0;
            var context = new CABehaviorContext(null,
                CAActorContext.NPCSettlement | CAActorContext.NPCInstitution,
                CAInitiativeTier.Standard, CAAuthorityOrigin.Institutional,
                authoritySatisfied: valid,
                knowledgeSatisfied: proposal != null
                    && proposal.Demands.Count > 0,
                knowledgeFresh: true, liveValidated: true,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                knowledgeConfidence: 1f, knowledgeUncertainty: 0f,
                capabilitySatisfied: valid && proposal.MaterialFeasible,
                materialSatisfied: proposal != null
                    && proposal.FundingFeasible
                    && proposal.MaterialFeasible
                    && record?.developmentSitingFeasible == true,
                currentIntentCompatible: true,
                directPlayerOwnership: false,
                authorityBasis: authorityIdentity
                    + " settlement institution",
                knowledgeBasis: proposal?.StableSignature(),
                owner: organization?.organizationKey);
            decision = CABehaviorGate.EvaluateForSelection(
                "spatial.npc_settlement_development", context);
            if (!decision.SelectionApproved) return false;
            bool existing = record.developmentAuthorized
                && record.developmentEpisodeId > 0
                && record.developmentBehaviorKey
                    == "spatial.npc_settlement_development"
                && record.developmentOwner == organization.organizationKey
                && !record.developmentAuthorityIdentity.NullOrEmpty();
            if (existing)
            {
                CACombatIntent.ObserveEpisode(record.developmentEpisodeId);
                intent = new CAIntentContext(record.developmentEpisodeId,
                    CAIntentOrigin.Institutional,
                    CAIntentController.SettlementDevelopment,
                    record.faction.loadID,
                    behaviorKey: "spatial.npc_settlement_development",
                    authorityOrigin: CAAuthorityOrigin.Institutional,
                    authorityIdentity: record.developmentAuthorityIdentity,
                    ownershipScope: organization.organizationKey,
                    ownerId: record.faction.loadID,
                    targetOrDemand: proposal.StableSignature(),
                    createdTick: record.developmentCreatedTick);
            }
            else
            {
                intent = CACombatIntent.Authorized(null,
                    CAIntentController.SettlementDevelopment,
                    "spatial.npc_settlement_development",
                    CAAuthorityOrigin.Institutional, context.AuthorityBasis,
                    organization.organizationKey, proposal.StableSignature(),
                    intentOrigin: CAIntentOrigin.Institutional,
                    ownerId: record.faction.loadID);
            }
            return true;
        }

        private static bool CommitmentStillExists(
            CARegionalSettlementRecord record, Map map,
            string targetOrDemand)
        {
            if (record == null || map == null) return false;
            string target = targetOrDemand ?? "";
            string key = target.IndexOf("research",
                    StringComparison.OrdinalIgnoreCase) >= 0
                ? CASettlementProgramRegistry.Research
                : target.IndexOf("cultivation",
                    StringComparison.OrdinalIgnoreCase) >= 0
                    ? CASettlementProgramRegistry.Agriculture
                    : target.IndexOf("road",
                        StringComparison.OrdinalIgnoreCase) >= 0
                        ? CASettlementProgramRegistry.Transport : null;
            if (key != null)
                return record.settlementProgram?.Entries(key).Any(entry =>
                    entry.blocker.NullOrEmpty()) == true;
            // Repair and rebuilding commitments are represented by their saved
            // program-asset ledger and survive completion of the work itself.
            if (target.IndexOf("repair", StringComparison.OrdinalIgnoreCase)
                    >= 0
                || target.IndexOf("rebuild",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return record.seededAssets?.Count > 0;
            return false;
        }
    }

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
        public float culturalExpression;
        public float ideoligionExpression;
        public float environmentalFit;
        public float operationalCoherence;
        public float historicalContinuity;
        public float strategicTopology;

        public float Total => semanticLegibility + socialFit
            + culturalExpression
            + ideoligionExpression + environmentalFit
            + operationalCoherence + historicalContinuity
            + strategicTopology;

        internal string Receipt()
        {
            return "legibility " + Signed(semanticLegibility)
                + ", social " + Signed(socialFit)
                + ", culture " + Signed(culturalExpression)
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

    // Both player-authored furnishing and established-settlement development
    // cross this native material boundary. Semantic ranking may differ above
    // it; neither authority path can make an invalid footprint buildable.
    internal static class CASettlementSitingConstraints
    {
        internal static bool HasMaterialFootprint(Map map, ThingDef def,
            IntVec3 center, Rot4 rotation)
        {
            if (map == null || def == null || !center.IsValid) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(center, rotation,
                def.Size))
                if (!cell.InBounds(map) || !cell.Standable(map))
                    return false;
            return true;
        }

        internal static AcceptanceReport CanPlaceNativeBlueprint(Map map,
            ThingDef def, IntVec3 center, Rot4 rotation, ThingDef stuff)
        {
            if (!HasMaterialFootprint(map, def, center, rotation))
                return "terrain cannot support the requested footprint";
            return GenConstruct.CanPlaceBlueprintAt(def, center, rotation,
                map, godMode: false, null, null, stuff);
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
        private int sampledCultivableCells;

        private string environmentalBiomes;
        private int environmentalSourceTiles;
        private int environmentalGrowingTwelfths;
        private float environmentalAverageTemperature;
        private float environmentalMinimumTemperature;
        private float environmentalMaximumTemperature;
        private float environmentalRainfall;
        private float environmentalForageability;
        private float environmentalPlantDensity;
        private float environmentalDiseasePerYear;
        private float environmentalFoodSupport;
        private float environmentalThermalPressure;
        private int environmentalLandCapacity;
        private int environmentalHabitatRequirementMask;
        private int environmentalRequiredCapabilityTier;
        private int environmentalFoodRoute;
        private int environmentalSourceHash;

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

        internal bool PreferIndoorActivity(Pawn pawn)
        {
            if (pawn == null) return environmentalThermalPressure >= 0.55f;
            FloatRange safe = pawn.SafeTemperatureRange();
            return environmentalThermalPressure >= 0.55f
                || !safe.Includes(map.mapTemperature.OutdoorTemp);
        }

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

            // Current room temperature and the represented tiles' full
            // seasonal range are independent facts. The first protects the
            // resident now; the second lets placement anticipate a climate
            // that is temporarily mild without inventing a new plan.
            if (planner != null)
            {
                FloatRange comfort = planner.ComfortableTemperatureRange();
                float roomTemperature = room.Temperature;
                if (comfort.Includes(roomTemperature))
                    result.environmentalFit += 0.75f;
                else
                {
                    float distance = roomTemperature < comfort.min
                        ? comfort.min - roomTemperature
                        : roomTemperature - comfort.max;
                    result.environmentalFit -= Mathf.Min(3f,
                        0.12f * distance);
                }
            }
            if (environmentalThermalPressure > 0f)
                result.environmentalFit += room.UsesOutdoorTemperature
                    ? -1.50f * environmentalThermalPressure
                    : 0.80f * environmentalThermalPressure;

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
            // Persisted lived practices rank otherwise valid placement
            // choices. Culture never grants permission, expands the authored
            // program, supplies material, or bypasses a native constraint.
            CACulture culture = CACultureLongitudinalMapComponent.For(map)
                ?.PlayerLocalCulture;
            CACulturalMeaningResolution shared = CACultureModel.Resolve(culture,
                CASocialSubjectRegistry.PublicGathering);
            CACulturalMeaningResolution defense = CACultureModel.Resolve(culture,
                CASocialSubjectRegistry.DefendedBoundary);
            result.culturalExpression = Mathf.Clamp01(shared.Salience / 100f)
                    * Mathf.Max(0f, result.semanticLegibility) * 0.28f
                + Mathf.Clamp01(defense.Salience / 100f)
                    * Mathf.Max(0f, result.strategicTopology) * 0.18f;
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
            Scribe_Values.Look(ref sampledCultivableCells,
                "CA_settlementContextSampledCultivableCells", 0);
            Scribe_Values.Look(ref environmentalBiomes,
                "CA_settlementContextEnvironmentalBiomes");
            Scribe_Values.Look(ref environmentalSourceTiles,
                "CA_settlementContextEnvironmentalSourceTiles", 0);
            Scribe_Values.Look(ref environmentalGrowingTwelfths,
                "CA_settlementContextEnvironmentalGrowingTwelfths", 0);
            Scribe_Values.Look(ref environmentalAverageTemperature,
                "CA_settlementContextEnvironmentalAverageTemperature", 0f);
            Scribe_Values.Look(ref environmentalMinimumTemperature,
                "CA_settlementContextEnvironmentalMinimumTemperature", 0f);
            Scribe_Values.Look(ref environmentalMaximumTemperature,
                "CA_settlementContextEnvironmentalMaximumTemperature", 0f);
            Scribe_Values.Look(ref environmentalRainfall,
                "CA_settlementContextEnvironmentalRainfall", 0f);
            Scribe_Values.Look(ref environmentalForageability,
                "CA_settlementContextEnvironmentalForageability", 0f);
            Scribe_Values.Look(ref environmentalPlantDensity,
                "CA_settlementContextEnvironmentalPlantDensity", 0f);
            Scribe_Values.Look(ref environmentalDiseasePerYear,
                "CA_settlementContextEnvironmentalDiseasePerYear", 0f);
            Scribe_Values.Look(ref environmentalFoodSupport,
                "CA_settlementContextEnvironmentalFoodSupport", 0f);
            Scribe_Values.Look(ref environmentalThermalPressure,
                "CA_settlementContextEnvironmentalThermalPressure", 0f);
            Scribe_Values.Look(ref environmentalLandCapacity,
                "CA_settlementContextEnvironmentalLandCapacity", 0);
            Scribe_Values.Look(ref environmentalHabitatRequirementMask,
                "CA_settlementContextEnvironmentalHabitatRequirementMask", 0);
            Scribe_Values.Look(ref environmentalRequiredCapabilityTier,
                "CA_settlementContextEnvironmentalRequiredCapabilityTier", 0);
            Scribe_Values.Look(ref environmentalFoodRoute,
                "CA_settlementContextEnvironmentalFoodRoute", 0);
            Scribe_Values.Look(ref environmentalSourceHash,
                "CA_settlementContextEnvironmentalSourceHash", 0);
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
            sampledCultivableCells = 0;
            var environmentalCells = new List<IntVec3>();
            for (int x = sampleMinX; x <= sampleMaxX; x++)
            {
                for (int z = sampleMinZ; z <= sampleMaxZ; z++)
                {
                    var cell = new IntVec3(x, 0, z);
                    environmentalCells.Add(cell);
                    sampledContextCells++;
                    if (cell.GetTerrain(map).IsWater) sampledWaterCells++;
                    if (cell.GetRoof(map)?.isNatural == true)
                        sampledNaturalRoofCells++;
                    if (!cell.GetTerrain(map).IsWater
                        && !cell.Roofed(map)
                        && cell.GetFertility(map) >= 0.7f)
                        sampledCultivableCells++;
                }
            }

            CASettlementEnvironmentFacts environment =
                CASettlementEnvironment.ForMap(map, environmentalCells);
            environmentalBiomes = environment.Biomes;
            environmentalSourceTiles = environment.SourceTiles;
            environmentalGrowingTwelfths = environment.GrowingTwelfths;
            environmentalAverageTemperature =
                environment.AverageTemperature;
            environmentalMinimumTemperature =
                environment.MinimumTemperature;
            environmentalMaximumTemperature =
                environment.MaximumTemperature;
            environmentalRainfall = environment.Rainfall;
            environmentalForageability = environment.Forageability;
            environmentalPlantDensity = environment.PlantDensity;
            environmentalDiseasePerYear = environment.DiseasePerYear;
            environmentalFoodSupport = environment.FoodSupport;
            environmentalThermalPressure =
                environment.SeasonalThermalPressure;
            environmentalLandCapacity = environment.LandCapacity;
            environmentalHabitatRequirementMask =
                environment.HabitatRequirementMask;
            environmentalRequiredCapabilityTier =
                environment.RequiredCapabilityTier;
            environmentalFoodRoute = (int)environment.FoodRoute;
            environmentalSourceHash = environment.SourceHash;
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
                .Append(sampledNaturalRoofCells).Append('|')
                .Append(sampledCultivableCells).Append('|')
                .Append(environmentalBiomes).Append('|')
                .Append(environmentalSourceTiles).Append('|')
                .Append(environmentalGrowingTwelfths).Append('|')
                .Append(environmentalAverageTemperature.ToString("F2",
                    System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalMinimumTemperature.ToString(
                    "F2", System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalMaximumTemperature.ToString(
                    "F2", System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalRainfall.ToString("F2",
                    System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalForageability.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalPlantDensity.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalDiseasePerYear.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalFoodSupport.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalThermalPressure.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(environmentalLandCapacity).Append('|')
                .Append(environmentalHabitatRequirementMask).Append('|')
                .Append(environmentalRequiredCapabilityTier).Append('|')
                .Append(environmentalFoodRoute).Append('|')
                .Append(environmentalSourceHash);
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
                + ", water " + sampledWaterCells + ", cultivable "
                + sampledCultivableCells + ")"
                + "\n  biome/climate evidence: "
                + (environmentalBiomes ?? "unknown") + " across "
                + environmentalSourceTiles + " represented source tile"
                + (environmentalSourceTiles == 1 ? "" : "s")
                + "; annual "
                + environmentalMinimumTemperature.ToString("F1") + " to "
                + environmentalMaximumTemperature.ToString("F1") + "C"
                + ", current "
                + map.mapTemperature.OutdoorTemp.ToString("F1")
                + "C, outdoor growing "
                + (environmentalGrowingTwelfths * 5) + "/60 days, rain "
                + environmentalRainfall.ToString("F0") + "mm, forage "
                + environmentalForageability.ToStringPercent()
                + ", plant density "
                + environmentalPlantDensity.ToStringPercent()
                + ", disease "
                + environmentalDiseasePerYear.ToString("F1")
                + "/year, food support "
                + environmentalFoodSupport.ToStringPercent()
                + ", site capacity " + environmentalLandCapacity + "/3"
                + "\n  habitat requirements: "
                + string.Join(", ", CAHabitatViabilityCausalKernel
                    .Enumerate(environmentalHabitatRequirementMask)
                    .Select(CAHabitatViabilityCausalKernel.RequirementWords)
                    .ToArray())
                + "; food route " + CAHabitatViabilityCausalKernel
                    .FoodRouteWords((CAHabitatFoodRoute)
                        environmentalFoodRoute)
                + "; minimum capability tier "
                + environmentalRequiredCapabilityTier
                + "\n  historical-continuity evidence: saved context revision "
                + revision + " since tick " + firstObservedTick
                + "; new Autonomous Bed proposals inside player-authored sleep "
                + "programs consume the context only to re-rank valid cells";
        }
    }
}
