using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public enum CASpacePurpose : byte
    {
        None = 0,
        Barracks = 1,
        Dining = 2,
        Recreation = 3,
        Kitchen = 4,
        Freezer = 5,
        Storage = 6,
        Workshop = 7,
        Medical = 8,
        Animal = 9,
        Utility = 10,
        Defense = 11,
        Bedroom = 12
    }

    public enum CASpaceStyle : byte
    {
        Adaptive,
        Austere,
        Practical,
        Comfortable
    }

    [Flags]
    public enum CASpaceRequirement : byte
    {
        None = 0,
        Sleep = 1
    }

    public enum CASpaceAuthor : byte
    {
        Player,
        Pawn
    }

    public enum CAResidentRosterAuthor : byte
    {
        None,
        Player,
        Pawn
    }

    internal static class CASpacePurposeInfo
    {
        // Storage remains a serialized purpose for existing programs, but new
        // storage intent is authored through RimWorld's native stockpile zones.
        private static readonly CASpacePurpose[] paintable =
        {
            CASpacePurpose.Barracks,
            CASpacePurpose.Bedroom,
            CASpacePurpose.Dining,
            CASpacePurpose.Recreation,
            CASpacePurpose.Kitchen,
            CASpacePurpose.Freezer,
            CASpacePurpose.Workshop,
            CASpacePurpose.Medical,
            CASpacePurpose.Animal,
            CASpacePurpose.Utility,
            CASpacePurpose.Defense
        };

        public static IEnumerable<CASpacePurpose> Paintable => paintable;

        public static string Label(CASpacePurpose purpose)
        {
            switch (purpose)
            {
                case CASpacePurpose.Barracks: return "Barracks";
                case CASpacePurpose.Bedroom: return "Bedroom";
                case CASpacePurpose.Dining: return "Dining";
                case CASpacePurpose.Recreation: return "Recreation";
                case CASpacePurpose.Kitchen: return "Kitchen";
                case CASpacePurpose.Freezer: return "Freezer";
                case CASpacePurpose.Storage: return "Storage";
                case CASpacePurpose.Workshop: return "Workshop";
                case CASpacePurpose.Medical: return "Medical";
                case CASpacePurpose.Animal: return "Animal";
                case CASpacePurpose.Utility: return "Utility / mech";
                case CASpacePurpose.Defense: return "Defense";
                default: return "Unprogrammed";
            }
        }

        public static string Description(CASpacePurpose purpose)
        {
            switch (purpose)
            {
                case CASpacePurpose.Barracks:
                    return "Shared living space. Its requirements can call for sleep capacity; seating and recreation are compatible uses.";
                case CASpacePurpose.Bedroom:
                    return "Private living space. Its requirements can call for sleep capacity; its roster may be player-assigned or chosen by residents from native love clusters.";
                case CASpacePurpose.Dining:
                    return "A shared eating program for tables and seating.";
                case CASpacePurpose.Recreation:
                    return "A recreation program that may also use tables and seating.";
                case CASpacePurpose.Kitchen:
                    return "Food preparation and adjacent eating space. Tables and seating are compatible in the current planner.";
                case CASpacePurpose.Freezer:
                    return "Temperature-managed storage. Player-selected filters "
                        + "and actual contents may be mixed.";
                case CASpacePurpose.Storage:
                    return "Legacy room-scale logistics context retained for "
                        + "existing saves. New storage intent belongs to native "
                        + "stockpile filters, priority, and footprint.";
                case CASpacePurpose.Workshop:
                    return "Production and crafting.";
                case CASpacePurpose.Medical:
                    return "Assessment, treatment, and recovery.";
                case CASpacePurpose.Animal:
                    return "Animals and their support spaces.";
                case CASpacePurpose.Utility:
                    return "Power, infrastructure, and mechs.";
                case CASpacePurpose.Defense:
                    return "Defensive positions and support.";
                default:
                    return "No authored space program.";
            }
        }

        public static Color Color(CASpacePurpose purpose)
        {
            switch (purpose)
            {
                case CASpacePurpose.Barracks: return new Color(0.25f, 0.48f, 0.95f);
                case CASpacePurpose.Bedroom: return new Color(0.30f, 0.70f, 0.88f);
                case CASpacePurpose.Dining: return new Color(0.95f, 0.70f, 0.20f);
                case CASpacePurpose.Recreation: return new Color(0.72f, 0.38f, 0.92f);
                case CASpacePurpose.Kitchen: return new Color(0.96f, 0.43f, 0.16f);
                case CASpacePurpose.Freezer: return new Color(0.22f, 0.82f, 0.92f);
                case CASpacePurpose.Storage: return new Color(0.62f, 0.48f, 0.28f);
                case CASpacePurpose.Workshop: return new Color(0.62f, 0.65f, 0.70f);
                case CASpacePurpose.Medical: return new Color(0.48f, 0.92f, 0.58f);
                case CASpacePurpose.Animal: return new Color(0.70f, 0.50f, 0.32f);
                case CASpacePurpose.Utility: return new Color(0.35f, 0.42f, 0.48f);
                case CASpacePurpose.Defense: return new Color(0.92f, 0.24f, 0.22f);
                default: return new Color(0.82f, 0.82f, 0.82f);
            }
        }

        public static string StyleLabel(CASpaceStyle style)
        {
            switch (style)
            {
                case CASpaceStyle.Austere: return "Austere";
                case CASpaceStyle.Practical: return "Practical";
                case CASpaceStyle.Comfortable: return "Comfortable";
                default: return "Adaptive";
            }
        }

        public static bool Allows(CASpaceProgram program, CAHomePlanKind kind)
        {
            if (program == null) return true;
            if (kind == CAHomePlanKind.Light) return true;
            switch (program.purpose)
            {
                case CASpacePurpose.Barracks:
                    return (kind == CAHomePlanKind.Bed && program.RequiresSleep)
                        || kind == CAHomePlanKind.Table
                        || kind == CAHomePlanKind.Seat
                        || kind == CAHomePlanKind.Recreation;
                case CASpacePurpose.Bedroom:
                    return kind == CAHomePlanKind.Bed && program.RequiresSleep;
                case CASpacePurpose.Dining:
                case CASpacePurpose.Kitchen:
                    return kind == CAHomePlanKind.Table
                        || kind == CAHomePlanKind.Seat;
                case CASpacePurpose.Recreation:
                    return kind == CAHomePlanKind.Recreation
                        || kind == CAHomePlanKind.Table
                        || kind == CAHomePlanKind.Seat;
                default:
                    return false;
            }
        }

        public static bool CanRequireSleep(CASpacePurpose purpose)
        {
            return purpose == CASpacePurpose.Barracks
                || purpose == CASpacePurpose.Bedroom;
        }
    }

    public class CASpaceProgram : IExposable
    {
        public int id;
        public string label;
        public CASpacePurpose purpose = CASpacePurpose.Barracks;
        public CASpaceStyle style = CASpaceStyle.Adaptive;
        public int maxOccupants = 1;
        public CASpaceRequirement requirements = CASpaceRequirement.Sleep;
        public CASpaceAuthor author = CASpaceAuthor.Player;
        public CAResidentRosterAuthor residentAuthor =
            CAResidentRosterAuthor.None;
        public List<IntVec3> cells = new List<IntVec3>();
        public List<Pawn> residents = new List<Pawn>();

        public bool RequiresSleep =>
            (requirements & CASpaceRequirement.Sleep) != 0;

        public bool HasResidentRoster =>
            residentAuthor != CAResidentRosterAuthor.None;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref purpose, "purpose", CASpacePurpose.Barracks);
            Scribe_Values.Look(ref style, "style", CASpaceStyle.Adaptive);
            Scribe_Values.Look(ref maxOccupants, "maxOccupants", 1);
            Scribe_Values.Look(ref requirements, "requirements",
                CASpaceRequirement.Sleep);
            Scribe_Values.Look(ref author, "author", CASpaceAuthor.Player);
            Scribe_Values.Look(ref residentAuthor, "residentAuthor",
                CAResidentRosterAuthor.None);
            Scribe_Collections.Look(ref cells, "cells", LookMode.Undefined);
            Scribe_Collections.Look(ref residents, "residents", LookMode.Reference);
            if (cells == null) cells = new List<IntVec3>();
            if (residents == null) residents = new List<Pawn>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                residents.RemoveAll(pawn => pawn == null);
                if (residentAuthor == CAResidentRosterAuthor.None
                    && residents.Count > 0)
                    residentAuthor = CAResidentRosterAuthor.Player;
            }
            maxOccupants = Mathf.Clamp(maxOccupants, 1, 999);
        }
    }

    internal sealed class CASpaceProgramDraft
    {
        public string label;
        public CASpacePurpose purpose = CASpacePurpose.Barracks;
        public CASpaceStyle style = CASpaceStyle.Adaptive;
        public int maxOccupants = 1;
        public bool requiresSleep = true;

        public static CASpaceProgramDraft From(CASpaceProgram program,
            int defaultOccupants)
        {
            if (program == null)
            {
                return new CASpaceProgramDraft
                {
                    label = "Barracks",
                    maxOccupants = Mathf.Max(1, defaultOccupants)
                };
            }
            return new CASpaceProgramDraft
            {
                label = program.label,
                purpose = program.purpose,
                style = program.style,
                maxOccupants = program.maxOccupants,
                requiresSleep = program.RequiresSleep
            };
        }
    }

    internal sealed class CAResidentRosterIntent : IExposable
    {
        public int residentId = -1;
        public int programId;
        public string behaviorKey;
        public int episodeId;
        public CAIntentOrigin intentOrigin;
        public CAIntentController intentController;
        public int issuerId = -1;
        public CAAuthorityOrigin authorityOrigin;
        public string authorityIdentity;
        public string ownershipScope;
        public int ownerId = -1;
        public int createdTick;
        public CAInitiativeTier creationTier;
        public string targetOrDemand;
        public string terminationCondition;

        public void ExposeData()
        {
            Scribe_Values.Look(ref residentId, "residentId", -1);
            Scribe_Values.Look(ref programId, "programId");
            Scribe_Values.Look(ref behaviorKey, "behaviorKey");
            Scribe_Values.Look(ref episodeId, "episodeId");
            Scribe_Values.Look(ref intentOrigin, "intentOrigin",
                CAIntentOrigin.Unknown);
            Scribe_Values.Look(ref intentController, "intentController",
                CAIntentController.Unknown);
            Scribe_Values.Look(ref issuerId, "issuerId", -1);
            Scribe_Values.Look(ref authorityOrigin, "authorityOrigin",
                CAAuthorityOrigin.None);
            Scribe_Values.Look(ref authorityIdentity, "authorityIdentity");
            Scribe_Values.Look(ref ownershipScope, "ownershipScope");
            Scribe_Values.Look(ref ownerId, "ownerId", -1);
            Scribe_Values.Look(ref createdTick, "createdTick");
            Scribe_Values.Look(ref creationTier, "creationTier",
                CAInitiativeTier.Standard);
            Scribe_Values.Look(ref targetOrDemand, "targetOrDemand");
            Scribe_Values.Look(ref terminationCondition,
                "terminationCondition");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CACombatIntent.ObserveEpisode(episodeId);
        }

        public CAIntentContext Context()
        {
            return new CAIntentContext(episodeId, intentOrigin,
                intentController, issuerId, behaviorKey, authorityOrigin,
                authorityIdentity, ownershipScope, ownerId, creationTier,
                targetOrDemand, terminationCondition, createdTick);
        }
    }

    // Space programs remain independent of RimWorld ZoneManager because its
    // one-zone-per-cell grid would collide with stockpiles and growing zones.
    public class PlannedUseMapComponent : MapComponent
    {
        private List<CASpaceProgram> programs = new List<CASpaceProgram>();
        private List<CAResidentRosterIntent> residentRosterIntents =
            new List<CAResidentRosterIntent>();
        private int nextProgramId = 1;
        private byte[] legacyPlannedUses;
        private CASpaceProgram[] programGrid;
        private CellBoolDrawer drawer;

        public PlannedUseMapComponent(Map map) : base(map) { }

        public static PlannedUseMapComponent For(Map map)
        {
            return map?.GetComponent<PlannedUseMapComponent>();
        }

        public IReadOnlyList<CASpaceProgram> Programs
        {
            get
            {
                EnsurePrograms();
                return programs;
            }
        }

        // Receipt-only readers must not normalize durable program state merely
        // by observing it. Gameplay callers use Programs and the component
        // lifecycle continues to own repair/migration.
        internal IReadOnlyList<CASpaceProgram> ProgramsForObservation =>
            programs != null ? programs
                : (IReadOnlyList<CASpaceProgram>)Array.Empty<CASpaceProgram>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref programs, "CA_spacePrograms", LookMode.Deep);
            Scribe_Collections.Look(ref residentRosterIntents,
                "CA_residentRosterIntents", LookMode.Deep);
            Scribe_Values.Look(ref nextProgramId, "CA_spaceProgramNextId", 1);
            DataExposeUtility.LookByteArray(ref legacyPlannedUses,
                "CA_plannedUseGrid");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                EnsurePrograms();
                if (residentRosterIntents == null)
                    residentRosterIntents = new List<CAResidentRosterIntent>();
                programGrid = null;
                drawer = null;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            EnsurePrograms();
            NormalizeResidentAssignments();
            PruneResidentRosterIntents();
            MigrateLegacyGrid();
            RebuildGrid();
        }

        public override void MapComponentUpdate()
        {
            drawer?.CellBoolDrawerUpdate();
        }

        public CASpaceProgram ProgramAt(IntVec3 cell)
        {
            if (!cell.InBounds(map)) return null;
            EnsureGrid();
            return programGrid[map.cellIndices.CellToIndex(cell)];
        }

        public CASpacePurpose PurposeAt(IntVec3 cell)
        {
            return ProgramAt(cell)?.purpose ?? CASpacePurpose.None;
        }

        public CASpaceProgram FindProgram(int id)
        {
            EnsurePrograms();
            for (int i = 0; i < programs.Count; i++)
                if (programs[i] != null && programs[i].id == id)
                    return programs[i];
            return null;
        }

        public int ResidentCount(CASpaceProgram program, bool spawnedOnly = false)
        {
            if (program?.residents == null) return 0;
            int count = 0;
            for (int i = 0; i < program.residents.Count; i++)
            {
                Pawn pawn = program.residents[i];
                if (!ValidResident(pawn)) continue;
                if (spawnedOnly && (!pawn.Spawned || pawn.Map != map)) continue;
                count++;
            }
            return count;
        }

        public string ResidentSummary(CASpaceProgram program)
        {
            if (program?.residents == null || program.residents.Count == 0)
                return "None";
            var names = new List<string>();
            for (int i = 0; i < program.residents.Count; i++)
            {
                Pawn pawn = program.residents[i];
                if (pawn != null) names.Add(pawn.LabelShort);
            }
            return names.Count == 0 ? "None" : string.Join(", ", names.ToArray());
        }

        public string ResidentAuthorLabel(CASpaceProgram program)
        {
            switch (program?.residentAuthor ?? CAResidentRosterAuthor.None)
            {
                case CAResidentRosterAuthor.Player: return "Player assigned";
                case CAResidentRosterAuthor.Pawn: return "Residents decide";
                default: return "Unassigned";
            }
        }

        public CASpaceProgram ResidentProgramFor(Pawn pawn)
        {
            if (pawn == null) return null;
            EnsurePrograms();
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program?.residents?.Contains(pawn) == true)
                    return program;
            }
            return null;
        }

        public bool TryGetShareableBedroomPair(CASpaceProgram program,
            out Pawn first, out Pawn second)
        {
            first = null;
            second = null;
            if (program == null || program.purpose != CASpacePurpose.Bedroom
                || program.style == CASpaceStyle.Austere
                || !program.RequiresSleep || program.residents == null)
                return false;
            List<Pawn> eligible = program.residents
                .Where(pawn => ValidResident(pawn) && pawn.Spawned
                    && pawn.Map == map && pawn.DevelopmentalStage.Adult())
                .ToList();
            if (eligible.Count != 2 || ResidentCount(program,
                spawnedOnly: true) != 2) return false;
            first = eligible[0];
            second = eligible[1];
            if (!LovePartnerRelationUtility.LovePartnerRelationExists(
                    first, second)
                || !BedUtility.WillingToShareBed(first, second))
            {
                first = null;
                second = null;
                return false;
            }
            return true;
        }

        public bool TryAssignSharedBedroomBed(CASpaceProgram program,
            Building_Bed bed, out Pawn first, out Pawn second)
        {
            first = null;
            second = null;
            if (!TryGetShareableBedroomPair(program, out first, out second)
                || bed == null || !bed.Spawned || bed.Map != map
                || !bed.def.building.bed_humanlike || !bed.ForColonists
                || bed.Medical || bed.SleepingSlotsCount != 2
                || bed.OwnersForReading.Count != 0
                || !ThingInsideProgram(bed, program)
                || first.ownership == null || second.ownership == null)
                return false;
            CompAssignableToPawn_Bed assignable =
                bed.CompAssignableToPawn as CompAssignableToPawn_Bed;
            if (assignable == null
                || !assignable.CanAssignTo(first).Accepted
                || !assignable.CanAssignTo(second).Accepted
                || assignable.IdeoligionForbids(first)
                || assignable.IdeoligionForbids(second)) return false;

            Building_Bed previousFirst = first.ownership.OwnedBed;
            if (!first.ownership.ClaimBedIfNonMedical(bed)) return false;
            if (!TryGetShareableBedroomPair(program, out Pawn currentFirst,
                    out Pawn currentSecond)
                || currentFirst != first || currentSecond != second
                || !assignable.CanAssignTo(second).Accepted
                || assignable.IdeoligionForbids(second)
                || !second.ownership.ClaimBedIfNonMedical(bed))
            {
                first.ownership.UnclaimBed();
                if (previousFirst != null && previousFirst.Spawned
                    && !previousFirst.Medical)
                    first.ownership.ClaimBedIfNonMedical(previousFirst);
                first = null;
                second = null;
                return false;
            }
            bed.NotifyRoomAssignedPawnsChanged();
            return true;
        }

        public bool SetResident(CASpaceProgram program, Pawn pawn,
            bool assigned, out string reason)
        {
            reason = null;
            EnsurePrograms();
            if (program == null || !programs.Contains(program) || pawn == null)
            {
                reason = "The space program or resident is unavailable.";
                return false;
            }
            if (!CASpacePurposeInfo.CanRequireSleep(program.purpose)
                || !program.RequiresSleep)
            {
                reason = "Only sleep-requiring Barracks and Bedroom programs have residents.";
                return false;
            }
            if (program.residents == null) program.residents = new List<Pawn>();
            if (!assigned)
            {
                program.residents.Remove(pawn);
                program.residentAuthor = CAResidentRosterAuthor.Player;
                RemoveResidentRosterIntents(program);
                MarkDirty();
                return true;
            }
            if (!ValidResident(pawn))
            {
                reason = pawn.LabelShort + " is not an eligible colony resident.";
                return false;
            }
            if (program.residents.Contains(pawn)) return true;
            if (ResidentCount(program) >= program.maxOccupants)
            {
                reason = program.label + " is already at its maximum occupancy of "
                    + program.maxOccupants + ".";
                return false;
            }
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram other = programs[i];
                if (other != null && other != program && other.residents != null)
                {
                    other.residents.Remove(pawn);
                    RemoveResidentRosterIntent(pawn, other);
                }
            }
            program.residents.Add(pawn);
            program.residentAuthor = CAResidentRosterAuthor.Player;
            RemoveResidentRosterIntents(program);
            if (!TryClaimExistingSharedBed(program))
                TryClaimExistingSingleBed(program, pawn);
            MarkDirty();
            return true;
        }

        public bool SetResidentRosterAuthor(CASpaceProgram program,
            CAResidentRosterAuthor author, out string reason)
        {
            reason = null;
            EnsurePrograms();
            if (program == null || !programs.Contains(program))
            {
                reason = "The space program is unavailable.";
                return false;
            }
            if (!program.RequiresSleep
                || !CASpacePurposeInfo.CanRequireSleep(program.purpose))
            {
                reason = "Only sleep-requiring Barracks and Bedroom programs have resident rosters.";
                return false;
            }
            if (author == CAResidentRosterAuthor.None)
            {
                reason = "Choose player assignment or resident negotiation.";
                return false;
            }
            program.residentAuthor = author;
            if (author == CAResidentRosterAuthor.Player)
                RemoveResidentRosterIntents(program);
            MarkDirty();
            return true;
        }

        public bool TryNegotiateResidents(out string outcome)
        {
            EnsurePrograms();
            PruneResidentRosterIntents();
            outcome = "resident rosters are stable";
            var sleepPrograms = programs.Where(program => program != null
                && program.cells != null && program.cells.Count > 0
                && program.RequiresSleep
                && CASpacePurposeInfo.CanRequireSleep(program.purpose))
                .OrderBy(program => program.id).ToList();
            var negotiable = sleepPrograms.Where(program =>
                program.residentAuthor != CAResidentRosterAuthor.Player)
                .ToList();
            if (negotiable.Count == 0)
            {
                outcome = "all sleep-program resident rosters are player assigned";
                return false;
            }

            bool changed = false;

            var playerResidents = new HashSet<Pawn>();
            for (int i = 0; i < sleepPrograms.Count; i++)
            {
                CASpaceProgram program = sleepPrograms[i];
                if (program.residentAuthor != CAResidentRosterAuthor.Player
                    || program.residents == null) continue;
                for (int r = 0; r < program.residents.Count; r++)
                    if (ValidResident(program.residents[r]))
                        playerResidents.Add(program.residents[r]);
            }

            List<Pawn> candidates = map.mapPawns.FreeColonistsSpawned
                .Where(ValidResident)
                .Where(pawn => !playerResidents.Contains(pawn))
                .OrderBy(pawn => pawn.thingIDNumber).ToList();
            var assigned = new HashSet<Pawn>();
            for (int i = 0; i < negotiable.Count; i++)
            {
                CASpaceProgram program = negotiable[i];
                if (program.residents == null) continue;
                for (int r = 0; r < program.residents.Count; r++)
                    if (ValidResident(program.residents[r]))
                        assigned.Add(program.residents[r]);
            }

            var additions = new List<string>();
            var coveredOutsidePrograms = new HashSet<Pawn>();
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn pawn = candidates[i];
                if (assigned.Contains(pawn)) continue;
                Building_Bed owned = pawn.ownership?.OwnedBed;
                if (owned == null || !owned.Spawned || owned.Map != map
                    || !owned.ForColonists || owned.Medical) continue;
                CASpaceProgram target = sleepPrograms.FirstOrDefault(program =>
                    ThingInsideProgram(owned, program));
                if (negotiable.Contains(target)
                    && CanJoinNegotiatedProgram(target, pawn)
                    && TryAuthorizeResidentChoice(pawn, target,
                        "their owned " + owned.def.defName + " at "
                        + owned.Position + " is inside the authored sleeping program",
                        out CAIntentContext residentIntent)
                    && AddNegotiatedResident(target, pawn, residentIntent))
                {
                    assigned.Add(pawn);
                    additions.Add(pawn.LabelShort + " kept their owned bed in "
                        + target.label + " #" + target.id);
                    changed = true;
                }
                else
                {
                    coveredOutsidePrograms.Add(pawn);
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn pawn = candidates[i];
                if (assigned.Contains(pawn)
                    || coveredOutsidePrograms.Contains(pawn)) continue;
                Pawn partner = LovePartnerRelationUtility
                    .ExistingMostLikedLovePartner(pawn, allowDead: false);
                if (partner == null || !assigned.Contains(partner)) continue;
                CASpaceProgram target = ResidentProgramFor(partner);
                if (!CanJoinNegotiatedProgram(target, pawn)) continue;
                if (TryAuthorizeResidentChoice(pawn, target,
                        "their current native love relationship with "
                        + partner.LabelShort + " supports joining that resident's authored sleeping program",
                        out CAIntentContext residentIntent)
                    && AddNegotiatedResident(target, pawn, residentIntent))
                {
                    assigned.Add(pawn);
                    additions.Add(pawn.LabelShort + " joined " + partner.LabelShort
                        + " in " + target.label + " #" + target.id);
                    changed = true;
                }
            }

            List<CASpaceProgram> bedrooms = negotiable
                .Where(program => program.purpose == CASpacePurpose.Bedroom
                    && ResidentCount(program) == 0
                    && program.maxOccupants >= 2).ToList();
            for (int b = 0; b < bedrooms.Count; b++)
            {
                Pawn bestFirst = null;
                Pawn bestSecond = null;
                int bestScore = int.MinValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    Pawn first = candidates[i];
                    if (assigned.Contains(first)
                        || coveredOutsidePrograms.Contains(first)) continue;
                    for (int j = i + 1; j < candidates.Count; j++)
                    {
                        Pawn second = candidates[j];
                        if (assigned.Contains(second)
                            || coveredOutsidePrograms.Contains(second)
                            || !LovePartnerRelationUtility
                                .LovePartnerRelationExists(first, second)) continue;
                        int score = first.relations.OpinionOf(second)
                            + second.relations.OpinionOf(first);
                        if (bestFirst == null || score > bestScore
                            || (score == bestScore
                                && first.thingIDNumber < bestFirst.thingIDNumber))
                        {
                            bestFirst = first;
                            bestSecond = second;
                            bestScore = score;
                        }
                    }
                }
                if (bestFirst == null) continue;
                string pairBasis = "their current mutual native love relationship supports choosing the same empty authored Bedroom";
                if (!TryAuthorizeResidentChoice(bestFirst, bedrooms[b],
                        pairBasis, out CAIntentContext firstIntent)
                    || !TryAuthorizeResidentChoice(bestSecond, bedrooms[b],
                        pairBasis, out CAIntentContext secondIntent))
                    continue;
                if (!AddNegotiatedResident(bedrooms[b], bestFirst,
                        firstIntent, claimBed: false)
                    || !AddNegotiatedResident(bedrooms[b], bestSecond,
                        secondIntent, claimBed: false))
                {
                    bedrooms[b].residents?.Remove(bestFirst);
                    bedrooms[b].residents?.Remove(bestSecond);
                    RemoveResidentRosterIntent(bestFirst, bedrooms[b]);
                    RemoveResidentRosterIntent(bestSecond, bedrooms[b]);
                    continue;
                }
                if (!TryClaimExistingSharedBed(bedrooms[b]))
                {
                    TryClaimExistingSingleBed(bedrooms[b], bestFirst);
                    TryClaimExistingSingleBed(bedrooms[b], bestSecond);
                }
                assigned.Add(bestFirst);
                assigned.Add(bestSecond);
                additions.Add(bestFirst.LabelShort + " and "
                    + bestSecond.LabelShort + " chose " + bedrooms[b].label
                    + " #" + bedrooms[b].id + " as a love cluster");
                changed = true;
            }

            List<CASpaceProgram> barracks = negotiable
                .Where(program => program.purpose == CASpacePurpose.Barracks)
                .ToList();
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn pawn = candidates[i];
                if (assigned.Contains(pawn)
                    || coveredOutsidePrograms.Contains(pawn)) continue;
                CASpaceProgram best = null;
                float bestScore = float.MinValue;
                for (int p = 0; p < barracks.Count; p++)
                {
                    CASpaceProgram program = barracks[p];
                    if (ResidentCount(program) >= program.maxOccupants) continue;
                    float score = BarracksFit(program, pawn);
                    if (best == null || score > bestScore
                        || (Mathf.Approximately(score, bestScore)
                            && program.id < best.id))
                    {
                        best = program;
                        bestScore = score;
                    }
                }
                if (best == null) continue;
                if (TryAuthorizeResidentChoice(pawn, best,
                        "the authored Barracks has current capacity and a native social fit of "
                        + bestScore.ToString("F1"),
                        out CAIntentContext residentIntent)
                    && AddNegotiatedResident(best, pawn, residentIntent))
                {
                    assigned.Add(pawn);
                    additions.Add(pawn.LabelShort + " chose " + best.label
                        + " #" + best.id + " (fit "
                        + bestScore.ToString("F1") + ")");
                    changed = true;
                }
            }

            if (!changed) return false;
            MarkDirty();
            outcome = additions.Count == 0
                ? "resident-authored rosters removed unavailable residents"
                : string.Join("; ", additions.ToArray());
            return true;
        }

        internal CASpaceProgram CreateProgram(CASpaceProgramDraft draft,
            IEnumerable<IntVec3> cells)
        {
            EnsurePrograms();
            var program = new CASpaceProgram { id = nextProgramId++ };
            ApplyDraft(program, draft);
            programs.Add(program);
            AddCells(program, cells, CASpaceAuthor.Player);
            return program;
        }

        internal void UpdateProgram(CASpaceProgram program,
            CASpaceProgramDraft draft)
        {
            if (program == null || draft == null) return;
            ApplyDraft(program, draft);
            program.author = CASpaceAuthor.Player;
            MarkDirty();
        }

        public void AddCells(CASpaceProgram program, IEnumerable<IntVec3> cells,
            CASpaceAuthor author)
        {
            if (program == null || cells == null) return;
            EnsureGrid();
            var touched = new HashSet<CASpaceProgram>();
            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(map)) continue;
                int index = map.cellIndices.CellToIndex(cell);
                CASpaceProgram previous = programGrid[index];
                if (previous == program) continue;
                if (previous != null)
                {
                    previous.cells.Remove(cell);
                    touched.Add(previous);
                }
                if (!program.cells.Contains(cell)) program.cells.Add(cell);
                programGrid[index] = program;
            }
            program.author = author;
            for (int i = programs.Count - 1; i >= 0; i--)
                if (programs[i] == null || programs[i].cells.Count == 0)
                    programs.RemoveAt(i);
            PruneResidentRosterIntents();
            MarkDirty();
        }

        public void ClearCells(IEnumerable<IntVec3> cells)
        {
            if (cells == null) return;
            EnsureGrid();
            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(map)) continue;
                int index = map.cellIndices.CellToIndex(cell);
                CASpaceProgram program = programGrid[index];
                if (program == null) continue;
                program.cells.Remove(cell);
                programGrid[index] = null;
            }
            for (int i = programs.Count - 1; i >= 0; i--)
                if (programs[i] == null || programs[i].cells.Count == 0)
                    programs.RemoveAt(i);
            PruneResidentRosterIntents();
            MarkDirty();
        }

        internal bool PlacementMatches(CASpaceProgram target, ThingDef def,
            IntVec3 center, Rot4 rotation, CAHomePlanKind kind)
        {
            if (def == null) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(center, rotation, def.Size))
            {
                CASpaceProgram actual = ProgramAt(cell);
                if (target != null)
                {
                    if (actual != target) return false;
                }
                else if (actual != null)
                {
                    return false;
                }
            }
            return target == null || CASpacePurposeInfo.Allows(target, kind);
        }

        internal bool CellMatches(CASpaceProgram target, IntVec3 cell,
            CAHomePlanKind kind)
        {
            CASpaceProgram actual = ProgramAt(cell);
            if (target != null)
                return actual == target && CASpacePurposeInfo.Allows(target, kind);
            return actual == null;
        }

        public void MarkForDraw()
        {
            EnsureDrawer();
            drawer.MarkForDraw();
        }

        public string Census()
        {
            EnsurePrograms();
            PruneResidentRosterIntents();
            if (programs.Count == 0)
                return "[CA] space programs: none; resident choice episodes 0";
            var entries = new List<string>();
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program == null || program.cells.Count == 0) continue;
                entries.Add(program.label + " #" + program.id + " ["
                    + CASpacePurposeInfo.Label(program.purpose)
                    + ", " + CASpacePurposeInfo.StyleLabel(program.style)
                    + ", max " + program.maxOccupants
                    + (program.RequiresSleep ? ", requires sleep" : "")
                    + (program.HasResidentRoster ? ", roster "
                        + ResidentAuthorLabel(program).ToLowerInvariant()
                        + ", residents " + ResidentSummary(program) : "")
                    + ", " + program.author.ToString().ToLowerInvariant()
                    + ", cells " + program.cells.Count + "]");
            }
            return "[CA] space programs: "
                + string.Join("; ", entries.ToArray())
                + "; resident choice episodes "
                + residentRosterIntents.Count;
        }

        private void ApplyDraft(CASpaceProgram program,
            CASpaceProgramDraft draft)
        {
            program.label = draft.label.NullOrEmpty()
                ? CASpacePurposeInfo.Label(draft.purpose) : draft.label.Trim();
            program.purpose = draft.purpose;
            program.style = draft.style;
            program.maxOccupants = Mathf.Clamp(draft.maxOccupants, 1, 999);
            program.requirements = draft.requiresSleep
                ? CASpaceRequirement.Sleep : CASpaceRequirement.None;
            if (!program.RequiresSleep
                || !CASpacePurposeInfo.CanRequireSleep(program.purpose))
            {
                program.residents?.Clear();
                program.residentAuthor = CAResidentRosterAuthor.None;
                RemoveResidentRosterIntents(program);
            }
            program.author = CASpaceAuthor.Player;
        }

        private void EnsurePrograms()
        {
            if (programs == null) programs = new List<CASpaceProgram>();
            for (int i = programs.Count - 1; i >= 0; i--)
                if (programs[i] == null) programs.RemoveAt(i);
            int highest = 0;
            for (int i = 0; i < programs.Count; i++)
            {
                if (programs[i].cells == null)
                    programs[i].cells = new List<IntVec3>();
                if (programs[i].residents == null)
                    programs[i].residents = new List<Pawn>();
                highest = Math.Max(highest, programs[i].id);
            }
            nextProgramId = Math.Max(nextProgramId, highest + 1);
        }

        private void NormalizeResidentAssignments()
        {
            var assigned = new HashSet<Pawn>();
            var ordered = programs.Where(program => program != null)
                .OrderBy(program => program.residentAuthor
                    == CAResidentRosterAuthor.Player ? 0 : 1)
                .ThenBy(program => program.id).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                CASpaceProgram program = ordered[i];
                if (program.residents == null)
                {
                    program.residents = new List<Pawn>();
                    continue;
                }
                var withinProgram = new HashSet<Pawn>();
                program.residents.RemoveAll(pawn => pawn == null
                    || !withinProgram.Add(pawn) || !assigned.Add(pawn));
            }
        }

        private bool ValidResident(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed
                && pawn.Faction == Faction.OfPlayer && pawn.needs?.rest != null
                && !pawn.DevelopmentalStage.Baby();
        }

        private bool CanJoinNegotiatedProgram(CASpaceProgram program,
            Pawn pawn)
        {
            if (program == null || pawn == null
                || program.residentAuthor == CAResidentRosterAuthor.Player
                || !program.RequiresSleep
                || !CASpacePurposeInfo.CanRequireSleep(program.purpose)
                || ResidentCount(program) >= program.maxOccupants)
                return false;
            if (program.purpose == CASpacePurpose.Barracks) return true;
            if (program.purpose != CASpacePurpose.Bedroom) return false;
            List<Pawn> current = program.residents
                ?.Where(ValidResident).ToList() ?? new List<Pawn>();
            if (current.Count == 0) return true;
            if (pawn.DevelopmentalStage.Juvenile())
            {
                Pawn mother = pawn.GetMother();
                Pawn father = pawn.GetFather();
                return current.Contains(mother) || current.Contains(father);
            }
            Pawn adult = current.FirstOrDefault(resident =>
                !resident.DevelopmentalStage.Juvenile());
            if (adult == null)
            {
                for (int i = 0; i < current.Count; i++)
                    if (current[i].GetMother() == pawn
                        || current[i].GetFather() == pawn) return true;
                return false;
            }
            return adult.GetLoveCluster().Contains(pawn);
        }

        private bool TryAuthorizeResidentChoice(Pawn resident,
            CASpaceProgram program, string knowledgeBasis,
            out CAIntentContext intent)
        {
            const string behaviorKey = "spatial.resident_roster_negotiation";
            intent = default(CAIntentContext);
            if (resident == null || program == null
                || !CABehaviorGate.StableProfileAllows(resident,
                    behaviorKey)) return false;

            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            CAInitiativeTier ceiling = spatial?.TierFor(program)
                ?? CAInitiativeTier.Standard;
            bool foreignPlayerWork = CATactical
                .HasForeignPlayerForcedJob(resident);
            bool live = resident.Spawned && resident.Map == map
                && !resident.Downed && !resident.InMentalState
                && resident.Awake();
            bool authority = map.IsPlayerHome
                && program.author == CASpaceAuthor.Player
                && program.residentAuthor != CAResidentRosterAuthor.Player;
            bool compatible = CanJoinNegotiatedProgram(program, resident);
            bool material = program.cells != null
                && program.cells.Count > 0 && program.RequiresSleep
                && ResidentCount(program) < program.maxOccupants;
            string authorityBasis = "resident choice inside player-authored "
                + "sleeping program #" + program.id;
            var context = new CABehaviorContext(resident,
                CAActorContext.PlayerPawn
                    | CAActorContext.PlayerSpatialAuthority,
                AutonomyComponent.TierOf(resident),
                CAAuthorityOrigin.PlayerDelegated,
                authoritySatisfied: authority,
                knowledgeSatisfied: !knowledgeBasis.NullOrEmpty(),
                knowledgeFresh: !knowledgeBasis.NullOrEmpty(),
                liveValidated: live,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                knowledgeConfidence: knowledgeBasis.NullOrEmpty() ? 0f : 1f,
                knowledgeUncertainty: knowledgeBasis.NullOrEmpty() ? 1f : 0f,
                capabilitySatisfied: compatible,
                materialSatisfied: material,
                currentIntentCompatible: !foreignPlayerWork,
                directPlayerOwnership: foreignPlayerWork,
                authorityCeiling: ceiling,
                authorityBasis: authorityBasis,
                knowledgeBasis: knowledgeBasis,
                owner: nameof(PlannedUseMapComponent));
            CABehaviorDecision decision = CABehaviorGate.EvaluateForSelection(
                behaviorKey, context);
            if (!decision.SelectionApproved) return false;

            intent = CACombatIntent.Authorized(resident,
                CAIntentController.Logistics, behaviorKey,
                context.AuthorityOrigin, authorityBasis,
                "resident roster", program.label + " #" + program.id);
            return intent.IsValid;
        }

        private bool AddNegotiatedResident(CASpaceProgram program, Pawn pawn,
            CAIntentContext intent, bool claimBed = true)
        {
            if (!intent.IsValid || intent.OwnerId != pawn?.thingIDNumber
                || intent.BehaviorKey
                    != "spatial.resident_roster_negotiation"
                || intent.Controller != CAIntentController.Logistics
                || !CanJoinNegotiatedProgram(program, pawn)) return false;
            if (program.residents == null) program.residents = new List<Pawn>();
            if (program.residents.Contains(pawn)) return false;
            program.residents.Add(pawn);
            program.residentAuthor = CAResidentRosterAuthor.Pawn;
            RecordResidentRosterIntent(program, pawn, intent);
            if (claimBed && !TryClaimExistingSharedBed(program))
                TryClaimExistingSingleBed(program, pawn);
            return true;
        }

        private void RecordResidentRosterIntent(CASpaceProgram program,
            Pawn resident, CAIntentContext intent)
        {
            if (program == null || resident == null || !intent.IsValid)
                return;
            if (residentRosterIntents == null)
                residentRosterIntents = new List<CAResidentRosterIntent>();
            RemoveResidentRosterIntent(resident, program);
            residentRosterIntents.Add(new CAResidentRosterIntent
            {
                residentId = resident.thingIDNumber,
                programId = program.id,
                behaviorKey = intent.BehaviorKey,
                episodeId = intent.EpisodeId,
                intentOrigin = intent.Origin,
                intentController = intent.Controller,
                issuerId = intent.IssuerId,
                authorityOrigin = intent.AuthorityOrigin,
                authorityIdentity = intent.AuthorityIdentity,
                ownershipScope = intent.OwnershipScope,
                ownerId = intent.OwnerId,
                createdTick = intent.CreatedTick,
                creationTier = intent.CreationTier,
                targetOrDemand = intent.TargetOrDemand,
                terminationCondition = intent.TerminationCondition
            });
        }

        private void RemoveResidentRosterIntent(Pawn resident,
            CASpaceProgram program)
        {
            if (residentRosterIntents == null || resident == null
                || program == null) return;
            int residentId = resident.thingIDNumber;
            int programId = program.id;
            residentRosterIntents.RemoveAll(record => record == null
                || (record.residentId == residentId
                    && record.programId == programId));
        }

        private void RemoveResidentRosterIntents(CASpaceProgram program)
        {
            if (residentRosterIntents == null || program == null) return;
            int programId = program.id;
            residentRosterIntents.RemoveAll(record => record == null
                || record.programId == programId);
        }

        private void PruneResidentRosterIntents()
        {
            if (residentRosterIntents == null)
            {
                residentRosterIntents = new List<CAResidentRosterIntent>();
                return;
            }
            if (programs == null)
            {
                residentRosterIntents.Clear();
                return;
            }
            residentRosterIntents.RemoveAll(record =>
            {
                if (record == null || record.episodeId <= 0
                    || record.behaviorKey
                        != "spatial.resident_roster_negotiation"
                    || record.intentController
                        != CAIntentController.Logistics)
                    return true;
                CASpaceProgram program = programs.FirstOrDefault(candidate =>
                    candidate != null && candidate.id == record.programId);
                if (program == null
                    || program.residentAuthor != CAResidentRosterAuthor.Pawn
                    || program.residents == null) return true;
                return !program.residents.Any(resident => resident != null
                    && resident.thingIDNumber == record.residentId);
            });
        }

        private float BarracksFit(CASpaceProgram program, Pawn pawn)
        {
            if (program?.residents == null || pawn?.relations == null) return 0f;
            int total = 0;
            int count = 0;
            for (int i = 0; i < program.residents.Count; i++)
            {
                Pawn resident = program.residents[i];
                if (!ValidResident(resident) || resident == pawn
                    || resident.relations == null) continue;
                total += pawn.relations.OpinionOf(resident)
                    + resident.relations.OpinionOf(pawn);
                count += 2;
            }
            float opinion = count > 0 ? (float)total / count : 0f;
            return opinion - (2f * ResidentCount(program));
        }

        private bool TryClaimExistingSingleBed(CASpaceProgram program, Pawn pawn)
        {
            Building_Bed owned = pawn?.ownership?.OwnedBed;
            if (owned != null && ThingInsideProgram(owned, program)) return false;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building_Bed bed = buildings[i] as Building_Bed;
                if (bed == null || !bed.def.building.bed_humanlike
                    || !bed.ForColonists || bed.Medical
                    || bed.SleepingSlotsCount != 1
                    || bed.OwnersForReading.Count > 0
                    || !ThingInsideProgram(bed, program)) continue;
                CompAssignableToPawn_Bed assignable =
                    bed.CompAssignableToPawn as CompAssignableToPawn_Bed;
                if (assignable == null || !assignable.CanAssignTo(pawn).Accepted
                    || assignable.IdeoligionForbids(pawn)) continue;
                return pawn.ownership.ClaimBedIfNonMedical(bed);
            }
            return false;
        }

        private bool TryClaimExistingSharedBed(CASpaceProgram program)
        {
            if (!TryGetShareableBedroomPair(program, out Pawn first,
                    out Pawn second)) return false;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building_Bed bed = buildings[i] as Building_Bed;
                if (bed == null || bed.OwnersForReading.Count != 0
                    || !ThingInsideProgram(bed, program)) continue;
                if (!TryAssignSharedBedroomBed(program, bed,
                        out first, out second)) continue;
                Log.Message("[CA] space program " + program.label + " #"
                    + program.id + " assigned existing " + bed.def.defName
                    + " to " + first.LabelShort + " and " + second.LabelShort
                    + " through native bed ownership");
                return true;
            }
            return false;
        }

        private bool ThingInsideProgram(Thing thing, CASpaceProgram program)
        {
            if (thing == null || program == null || !thing.Spawned
                || thing.Map != map) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(thing.Position,
                thing.Rotation, thing.def.Size))
                if (ProgramAt(cell) != program) return false;
            return true;
        }

        private void EnsureGrid()
        {
            int expected = map.cellIndices.NumGridCells;
            if (programGrid != null && programGrid.Length == expected) return;
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            EnsurePrograms();
            programGrid = new CASpaceProgram[map.cellIndices.NumGridCells];
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                for (int j = program.cells.Count - 1; j >= 0; j--)
                {
                    IntVec3 cell = program.cells[j];
                    if (!cell.InBounds(map))
                    {
                        program.cells.RemoveAt(j);
                        continue;
                    }
                    int index = map.cellIndices.CellToIndex(cell);
                    CASpaceProgram previous = programGrid[index];
                    if (previous != null) previous.cells.Remove(cell);
                    programGrid[index] = program;
                }
            }
            drawer = null;
        }

        private void MigrateLegacyGrid()
        {
            if (legacyPlannedUses == null || legacyPlannedUses.Length == 0
                || programs.Count > 0)
            {
                legacyPlannedUses = null;
                return;
            }
            int limit = Math.Min(legacyPlannedUses.Length,
                map.cellIndices.NumGridCells);
            var visited = new bool[limit];
            for (int index = 0; index < limit; index++)
            {
                byte value = legacyPlannedUses[index];
                if (value == 0 || value > (byte)CASpacePurpose.Defense
                    || visited[index]) continue;
                var queue = new Queue<IntVec3>();
                var cells = new List<IntVec3>();
                IntVec3 start = map.cellIndices.IndexToCell(index);
                visited[index] = true;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    IntVec3 cell = queue.Dequeue();
                    cells.Add(cell);
                    for (int d = 0; d < GenAdj.CardinalDirections.Length; d++)
                    {
                        IntVec3 next = cell + GenAdj.CardinalDirections[d];
                        if (!next.InBounds(map)) continue;
                        int nextIndex = map.cellIndices.CellToIndex(next);
                        if (nextIndex < 0 || nextIndex >= limit || visited[nextIndex]
                            || legacyPlannedUses[nextIndex] != value) continue;
                        visited[nextIndex] = true;
                        queue.Enqueue(next);
                    }
                }
                CASpacePurpose purpose = (CASpacePurpose)value;
                var draft = new CASpaceProgramDraft
                {
                    label = CASpacePurposeInfo.Label(purpose),
                    purpose = purpose,
                    style = CASpaceStyle.Adaptive,
                    maxOccupants = Mathf.Max(1,
                        map.mapPawns?.FreeColonistsSpawnedCount ?? 1),
                    requiresSleep = purpose == CASpacePurpose.Barracks
                };
                CreateProgram(draft, cells);
            }
            legacyPlannedUses = null;
        }

        private void MarkDirty()
        {
            EnsureDrawer();
            drawer.SetDirty();
        }

        private void EnsureDrawer()
        {
            EnsureGrid();
            if (drawer != null) return;
            drawer = new CellBoolDrawer(
                index => programGrid != null && index >= 0
                    && index < programGrid.Length && programGrid[index] != null,
                () => Color.white,
                index => CASpacePurposeInfo.Color(
                    programGrid[index]?.purpose ?? CASpacePurpose.None),
                map.Size.x, map.Size.z, 3655, 0.38f);
        }
    }

    public class Designator_CAPlannedUse : Designator_Cells
    {
        private CASpaceProgramDraft draft;
        private int activeProgramId;
        private bool clearCells;

        public override bool DragDrawMeasurements => true;
        public override DrawStyleCategoryDef DrawStyleCategory =>
            DrawStyleCategoryDefOf.Areas;
        public override Color IconDrawColor => clearCells ? Color.white
            : CASpacePurposeInfo.Color(draft?.purpose ?? CASpacePurpose.Barracks);

        public Designator_CAPlannedUse()
        {
            defaultLabel = "Plan colony space";
            defaultDesc = "Create or edit an authored space program with a purpose, planning style, occupancy limit, and requirements. Programs coexist with stockpiles, growing zones, Home, and allowed areas.";
            icon = ContentFinder<Texture2D>.Get("UI/Designators/HomeAreaOn");
            soundDragSustain = SoundDefOf.Designate_DragAreaAdd;
            soundDragChanged = SoundDefOf.Designate_DragZone_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneAdd;
            useMouseIcon = true;
            tutorTag = "CAPlannedUse";
        }

        public override void ProcessInput(Event ev)
        {
            if (!CheckCanInteract()) return;
            Find.WindowStack.Add(new Dialog_CASpaceProgram(this,
                PlannedUseMapComponent.For(Map)));
        }

        internal void BeginPainting(CASpaceProgramDraft newDraft,
            int programId, bool clearing)
        {
            draft = newDraft;
            activeProgramId = programId;
            clearCells = clearing;
            UpdateMouseText();
            Find.DesignatorManager.Select(this);
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 cell)
        {
            if (!cell.InBounds(Map)) return false;
            PlannedUseMapComponent component = PlannedUseMapComponent.For(Map);
            if (component == null) return false;
            if (clearCells) return component.ProgramAt(cell) != null;
            CASpaceProgram active = component.FindProgram(activeProgramId);
            return active == null || component.ProgramAt(cell) != active;
        }

        public override void DesignateSingleCell(IntVec3 cell)
        {
            DesignateMultiCell(new[] { cell });
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            PlannedUseMapComponent component = PlannedUseMapComponent.For(Map);
            if (component == null) return;
            List<IntVec3> valid = cells.Where(c => c.InBounds(Map)).ToList();
            if (clearCells)
            {
                component.ClearCells(valid);
                Finalize(valid.Count > 0);
                return;
            }
            CASpaceProgram program = component.FindProgram(activeProgramId);
            if (program == null)
            {
                program = component.CreateProgram(draft, valid);
                activeProgramId = program.id;
            }
            else
            {
                if (program.purpose == CASpacePurpose.Storage
                    && draft?.purpose == CASpacePurpose.Storage)
                {
                    Messages.Message("Colonist Awareness: legacy Storage "
                        + "programs are read-only. Choose another room purpose "
                        + "before converting or expanding this footprint.",
                        MessageTypeDefOf.RejectInput, historical: false);
                    Finalize(false);
                    return;
                }
                component.UpdateProgram(program, draft);
                component.AddCells(program, valid, CASpaceAuthor.Player);
            }
            UpdateMouseText();
            Finalize(valid.Count > 0);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            PlannedUseMapComponent.For(Map)?.MarkForDraw();
        }

        internal void SelectBarracksForDebug()
        {
            draft = new CASpaceProgramDraft
            {
                label = "Barracks",
                purpose = CASpacePurpose.Barracks,
                style = CASpaceStyle.Adaptive,
                maxOccupants = Mathf.Max(1,
                    Map?.mapPawns?.FreeColonistsSpawnedCount ?? 1),
                requiresSleep = true
            };
            activeProgramId = 0;
            clearCells = false;
            UpdateMouseText();
        }

        private void UpdateMouseText()
        {
            mouseText = clearCells ? "Clear space program cells"
                : "Space program: " + (draft?.label ?? "Barracks")
                    + " — " + CASpacePurposeInfo.StyleLabel(
                        draft?.style ?? CASpaceStyle.Adaptive)
                    + " — max " + (draft?.maxOccupants ?? 1)
                    + (draft?.requiresSleep == true ? " — sleep required" : "");
        }
    }

    internal sealed class Dialog_CASpaceProgram : Window
    {
        private readonly Designator_CAPlannedUse designator;
        private readonly PlannedUseMapComponent component;
        private CASpaceProgram selected;
        private CASpaceProgramDraft draft;
        private string occupancyBuffer;

        public override Vector2 InitialSize => new Vector2(560f, 600f);

        public Dialog_CASpaceProgram(Designator_CAPlannedUse designator,
            PlannedUseMapComponent component)
        {
            this.designator = designator;
            this.component = component;
            int people = Find.CurrentMap?.mapPawns?.FreeColonistsSpawnedCount ?? 1;
            draft = CASpaceProgramDraft.From(null, people);
            occupancyBuffer = draft.maxOccupants.ToString();
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f),
                "Plan colony space");
            Text.Font = GameFont.Small;
            var listing = new Listing_Standard();
            listing.Begin(new Rect(0f, 42f, inRect.width, inRect.height - 102f));

            string selector = selected == null ? "New program" : selected.label;
            if (listing.ButtonTextLabeled("Program", selector))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("New program", delegate
                    {
                        selected = null;
                        int people = Find.CurrentMap?.mapPawns?.FreeColonistsSpawnedCount ?? 1;
                        draft = CASpaceProgramDraft.From(null, people);
                        occupancyBuffer = draft.maxOccupants.ToString();
                    })
                };
                if (component != null)
                {
                    foreach (CASpaceProgram program in component.Programs)
                    {
                        CASpaceProgram captured = program;
                        options.Add(new FloatMenuOption(program.label, delegate
                        {
                            selected = captured;
                            draft = CASpaceProgramDraft.From(captured, 1);
                            occupancyBuffer = draft.maxOccupants.ToString();
                        }));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            draft.label = listing.TextEntryLabeled("Name", draft.label ?? "");
            if (listing.ButtonTextLabeled("Purpose",
                CASpacePurposeInfo.Label(draft.purpose), tooltip:
                CASpacePurposeInfo.Description(draft.purpose)))
            {
                var options = new List<FloatMenuOption>();
                foreach (CASpacePurpose purpose in CASpacePurposeInfo.Paintable)
                {
                    CASpacePurpose captured = purpose;
                    options.Add(new FloatMenuOption(
                        CASpacePurposeInfo.Label(captured), delegate
                        {
                            draft.purpose = captured;
                            draft.requiresSleep =
                                CASpacePurposeInfo.CanRequireSleep(captured);
                        }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (listing.ButtonTextLabeled("Planning style",
                CASpacePurposeInfo.StyleLabel(draft.style)))
            {
                var options = new List<FloatMenuOption>();
                foreach (CASpaceStyle style in Enum.GetValues(typeof(CASpaceStyle)))
                {
                    CASpaceStyle captured = style;
                    options.Add(new FloatMenuOption(
                        CASpacePurposeInfo.StyleLabel(captured),
                        delegate { draft.style = captured; }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.TextFieldNumericLabeled("Maximum occupants",
                ref draft.maxOccupants, ref occupancyBuffer, 1, 999);
            if (CASpacePurposeInfo.CanRequireSleep(draft.purpose))
            {
                listing.CheckboxLabeled("Require sleep capacity",
                    ref draft.requiresSleep,
                    "A requirement asks the planner to provide places to sleep; it does not require material beds.");
            }
            else
            {
                draft.requiresSleep = false;
                listing.Label("Production sleep-capacity origination currently operates for Barracks; Player-authored Bedroom Bed causes remain evaluation/debug-regression only.");
            }
            bool residentEditorReady = selected != null
                && selected.purpose == draft.purpose
                && selected.RequiresSleep == draft.requiresSleep
                && selected.maxOccupants == draft.maxOccupants
                && selected.RequiresSleep
                && CASpacePurposeInfo.CanRequireSleep(selected.purpose);
            if (residentEditorReady)
            {
                if (listing.ButtonTextLabeled("Residents",
                    component?.ResidentSummary(selected) ?? "None"))
                    Find.WindowStack.Add(new Dialog_CAProgramResidents(
                        component, selected));
            }
            else if (selected == null && draft.requiresSleep)
            {
                listing.Label("Create and paint this program, then reopen it to assign residents.");
            }
            else if (selected != null && draft.requiresSleep)
            {
                listing.Label("Save purpose or requirement changes before editing residents.");
            }
            bool roomInitiativeReady = selected != null
                && selected.purpose == draft.purpose
                && selected.RequiresSleep == draft.requiresSleep
                && CASpatialFurnishingModule
                    .CanOriginateNewRoomFacility(selected);
            if (roomInitiativeReady)
            {
                listing.GapLine();
                CASpatialInitiativeMapComponent spatial =
                    CASpatialInitiativeMapComponent.For(Find.CurrentMap);
                CAInitiativeTier tier = spatial?.TierFor(selected)
                    ?? CAInitiativeTier.Standard;
                if (listing.ButtonTextLabeled("CA initiative",
                    CAInitiativePresentation.Label(tier), tooltip:
                    "The shared initiative ceiling for this authored room. "
                    + "Effective initiative is the lower of this value and "
                    + "the acting colonist's autonomy."))
                {
                    var options = new List<FloatMenuOption>();
                    for (int i = 0;
                        i < CAInitiativePresentation.ActiveTiers.Length; i++)
                    {
                        CAInitiativeTier captured =
                            CAInitiativePresentation.ActiveTiers[i];
                        string option = (tier == captured ? "* " : "")
                            + CAInitiativePresentation.Label(captured);
                        options.Add(new FloatMenuOption(option, delegate
                        {
                            spatial?.SetTier(selected, captured);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                listing.Label("Standard originates no discretionary room "
                    + "construction. Proactive may add one loaded, positive "
                    + "native bed facility for a coherent shared group. It does "
                    + "not force one object to cover the whole bed bank. "
                    + "Autonomous may also address a single exact missing bed "
                    + "link within that family. Once a facility family exists, "
                    + "CA may complete it but does not originate another merely "
                    + "because another native bonus is available. Functional "
                    + "posture, protected travel lanes, bed-bank "
                    + "intervals, native room role, residents, bed ownership, "
                    + "research, materials, construction work, negative space, "
                    + "culture/style, and later player edits remain authoritative. "
                    + "Native Beauty is telemetry, not an initiative objective.");
            }
            bool toxicStagingProgram = selected != null
                && selected.purpose == draft.purpose
                && (selected.purpose == CASpacePurpose.Freezer
                    || selected.purpose == CASpacePurpose.Storage
                    || selected.purpose == CASpacePurpose.Utility);
            if (toxicStagingProgram)
            {
                listing.GapLine();
                CAToxicWasteLifecycleMapComponent lifecycle =
                    CAToxicWasteLifecycleMapComponent.For(Find.CurrentMap);
                listing.Label("Toxic waste: "
                    + (lifecycle?.RelocationAuthoritySummary(selected)
                        ?? "No loaded-map lifecycle is available."));
            }
            listing.GapLine();
            listing.Label("A resident roster sets required capacity. Player edits preserve the roster; an unassigned roster may be filled by Autonomous residents, or the player can hand a current roster back to them. Native love clusters seed Bedrooms and mutual opinion guides Barracks choices without evicting valid residents. A share-willing two-adult love cluster in a non-austere Bedroom may use one native double bed; Barracks, austere Bedrooms, unwilling pairs, and partially provided Bedrooms retain single-slot provisions. Existing or completed CA-planned provisions receive residents through native bed ownership. Privacy beyond native love clusters and pawn-authored programs is not part of this slice.");
            listing.End();

            float y = inRect.height - 45f;
            if (Widgets.ButtonText(new Rect(0f, y, 170f, 40f),
                "Clear painted cells"))
            {
                designator.BeginPainting(null, 0, true);
                Close();
            }
            bool legacyStorageReadOnly = selected?.purpose
                    == CASpacePurpose.Storage
                && draft.purpose == CASpacePurpose.Storage;
            string actionLabel = legacyStorageReadOnly
                ? "Choose purpose to convert"
                : selected == null ? "Create and paint" : "Save and expand";
            if (Widgets.ButtonText(new Rect(inRect.width - 180f, y, 180f, 40f),
                actionLabel))
            {
                if (legacyStorageReadOnly)
                {
                    Messages.Message("Colonist Awareness: legacy Storage "
                        + "programs remain available for receipts but cannot "
                        + "be edited or expanded as storage. Choose another "
                        + "room purpose to convert this program.",
                        MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
                draft.maxOccupants = Mathf.Clamp(draft.maxOccupants, 1, 999);
                int assignedResidents = selected == null ? 0
                    : component?.ResidentCount(selected) ?? 0;
                if (assignedResidents > draft.maxOccupants)
                {
                    Messages.Message("Colonist Awareness: remove residents before lowering maximum occupancy below "
                        + assignedResidents + ".", MessageTypeDefOf.RejectInput,
                        historical: false);
                    return;
                }
                if (draft.label.NullOrEmpty())
                    draft.label = CASpacePurposeInfo.Label(draft.purpose);
                if (selected != null) component?.UpdateProgram(selected, draft);
                designator.BeginPainting(draft, selected?.id ?? 0, false);
                Close();
            }
        }
    }

    internal sealed class Dialog_CAProgramResidents : Window
    {
        private const float RowHeight = 38f;
        private readonly PlannedUseMapComponent component;
        private readonly CASpaceProgram program;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(520f, 500f);

        public Dialog_CAProgramResidents(PlannedUseMapComponent component,
            CASpaceProgram program)
        {
            this.component = component;
            this.program = program;
            doCloseButton = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f),
                (program?.label ?? "Space program") + " residents");
            Text.Font = GameFont.Small;
            int assigned = component?.ResidentCount(program) ?? 0;
            Widgets.Label(new Rect(0f, 35f, inRect.width, 48f),
                assigned + " of " + (program?.maxOccupants ?? 0)
                + " maximum occupants. Assigning a pawn here transfers them from another Barracks or Bedroom program.");

            CAResidentRosterAuthor author = program?.residentAuthor
                ?? CAResidentRosterAuthor.None;
            Widgets.Label(new Rect(0f, 78f, 250f, 32f), "Roster: "
                + (component?.ResidentAuthorLabel(program) ?? "Unavailable"));
            string authorityAction = author == CAResidentRosterAuthor.Player
                ? "Let residents decide"
                : (assigned == 0 ? "Keep empty" : "Keep current roster");
            if (Widgets.ButtonText(new Rect(inRect.width - 190f, 75f,
                185f, 32f), authorityAction))
            {
                CAResidentRosterAuthor desired =
                    author == CAResidentRosterAuthor.Player
                    ? CAResidentRosterAuthor.Pawn
                    : CAResidentRosterAuthor.Player;
                string reason = "The space-program component is unavailable.";
                if (component == null || !component.SetResidentRosterAuthor(
                    program, desired, out reason))
                    Messages.Message("Colonist Awareness: " + reason,
                        MessageTypeDefOf.RejectInput, historical: false);
            }

            List<Pawn> pawns = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned
                ?.Where(pawn => pawn.needs?.rest != null
                    && !pawn.DevelopmentalStage.Baby())
                .OrderBy(pawn => pawn.LabelShort).ToList()
                ?? new List<Pawn>();
            Rect outRect = new Rect(0f, 118f, inRect.width,
                inRect.height - 178f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, pawns.Count * RowHeight));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
                if (i % 2 == 1) Widgets.DrawLightHighlight(row);
                Rect icon = new Rect(row.x + 2f, row.y + 2f,
                    RowHeight - 4f, RowHeight - 4f);
                Widgets.ThingIcon(icon, pawn);
                bool isAssigned = program?.residents?.Contains(pawn) == true;
                Rect button = new Rect(row.xMax - 130f, row.y + 3f,
                    125f, RowHeight - 6f);
                if (Widgets.ButtonText(button,
                    isAssigned ? "Unassign" : "Assign"))
                {
                    string reason = "The space-program component is unavailable.";
                    if (component == null || !component.SetResident(program,
                        pawn, !isAssigned,
                        out reason))
                        Messages.Message("Colonist Awareness: " + reason,
                            MessageTypeDefOf.RejectInput, historical: false);
                }
                Rect label = new Rect(icon.xMax + 10f, row.y,
                    button.xMin - icon.xMax - 20f, RowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(label, pawn.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;
            }
            Widgets.EndScrollView();
        }
    }
}
