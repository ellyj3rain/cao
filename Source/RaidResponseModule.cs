using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Module 6: when danger is high, undrafted Autonomous colonists act. Armed pawns take
    // positions at cover; others shelter. With a smart squad leader (Intellectual 6+) and
    // fire teams set, the defense SPLITS by geography: the primary line faces the threat,
    // Fire team B holds the rear cover - three at the river, two at the back entrance.
    public class RaidResponseMapComponent : MapComponent
    {
        private const int IntervalTicks = 250;
        private const int ShelterSearchRadius = 50;
        private const int ShelterCandidateLimit = 24;
        private const int ShelterCandidatesPerRoom = 4;
        private readonly HashSet<IntVec3> claimed = new HashSet<IntVec3>();
        private readonly List<IntVec3> coverPrimary = new List<IntVec3>();
        private readonly List<IntVec3> coverSecondary = new List<IntVec3>();
        private readonly Dictionary<IntVec3, float> coverScores = new Dictionary<IntVec3, float>();
        private readonly List<IntVec3> defenseThreatCells =
            new List<IntVec3>();
        private IntVec3 threatCentroid = IntVec3.Invalid;
        // Convene-plan-disperse: on attack, squadded Autonomous fighters rally first,
        // plan (fast with a smart leader, slow milling without), then disperse to sectors.
        private bool convenedThisRaid;
        private int convenedUntil;
        private IntVec3 rallyCell = IntVec3.Invalid;
        private IntVec3 conveneThreatCentroid = IntVec3.Invalid;
        private int calmSince = -1;
        // Shelter is a finite threat-episode commitment, not a one-shot Goto. The
        // actor id, destination, and causal episode survive save/load; the job may
        // yield to the player's hand or an emergency without losing the safety
        // envelope it must resume while the active threat remains. Hunger remains
        // inside that envelope while the alarm is active.
        private Dictionary<int, IntVec3> shelterCells =
            new Dictionary<int, IntVec3>();
        private Dictionary<int, int> shelterEpisodes =
            new Dictionary<int, int>();

        private sealed class ShelterRoute
        {
            public IntVec3 Cell = IntVec3.Invalid;
            public int Length;
            public float PathCost;
            public int UnroofedCells;
            public int KnownHostileLosCells;
            public int HazardCells;
            public int NewWaterEntries;
            public bool InHome;
            public bool Enclosed;
            public bool CurrentRoom;
            public bool EmptyCell;
            public bool DestinationExposed;
            public bool DestinationUnroofed;
            public bool DestinationKnownHostileLos;
            public bool DestinationHazard;
            public bool DestinationWater;
            public bool CurrentCellEnclosedHome;
            public bool CurrentCellUnroofed;
            public bool CurrentCellKnownHostileLos;
            public bool CurrentCellHazard;
            public bool CurrentCellWater;
            public bool StayedAtCurrentCell;
            public int ShortlistCount;
            public int ViablePathCount;
            public int PathFailureCount;
            public int RejectedExposedCount;
            public int LowerScoredCount;
            public int AlternativesWithUnroofed;
            public int AlternativesWithKnownHostileLos;
            public int AlternativesWithHazard;
            public int AlternativesWithNewWater;
            public float Score;

            public bool Exposed
            {
                get
                {
                    return UnroofedCells > 0 || KnownHostileLosCells > 0
                        || HazardCells > 0 || NewWaterEntries > 0;
                }
            }

            public string Metrics
            {
                get
                {
                    return "route cells=" + Length
                        + ", cost=" + PathCost.ToString("0")
                        + ", unroofed=" + UnroofedCells
                        + ", known-hostile-los=" + KnownHostileLosCells
                        + ", hazards=" + HazardCells
                        + ", new-water-entry=" + NewWaterEntries
                        + ", Home=" + InHome
                        + ", enclosed=" + Enclosed
                        + ", current-room=" + CurrentRoom
                        + ", shortlist=" + ShortlistCount
                        + ", viable=" + ViablePathCount
                        + ", path-failed=" + PathFailureCount
                        + ", exposed-rejected=" + RejectedExposedCount
                        + ", lower-scored=" + LowerScoredCount
                        + ", alternative-risk[unroofed="
                        + AlternativesWithUnroofed
                        + ", known-hostile-los="
                        + AlternativesWithKnownHostileLos
                        + ", hazard=" + AlternativesWithHazard
                        + ", new-water=" + AlternativesWithNewWater
                        + "], decision=" + SafetyComparison;
                }
            }

            public string SafetyComparison
            {
                get
                {
                    if (StayedAtCurrentCell && CurrentCellEnclosedHome
                        && !CurrentCellUnroofed
                        && !CurrentCellKnownHostileLos
                        && !CurrentCellHazard)
                        return "current enclosed Home cell retained; no outside route";

                    var removed = new List<string>();
                    if (CurrentCellUnroofed && !DestinationUnroofed)
                        removed.Add("unroofed exposure");
                    if (CurrentCellKnownHostileLos
                        && !DestinationKnownHostileLos)
                        removed.Add("actor-known hostile LOS");
                    if (CurrentCellHazard && !DestinationHazard)
                        removed.Add("environmental hazard");
                    if (CurrentCellWater && !DestinationWater)
                        removed.Add("water occupancy");
                    if (removed.Count > 0)
                        return "safer than current cell: endpoint removes "
                            + string.Join(", ", removed.ToArray());
                    if (!CurrentCellEnclosedHome && Enclosed && InHome)
                        return "safer than current cell: endpoint enters roofed enclosed Home; route minimizes measured exposure";
                    return "selected by the lowest measured route exposure and length among retained alternatives";
                }
            }
        }

        public RaidResponseMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref shelterCells, "CA_shelterCells",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref shelterEpisodes, "CA_shelterEpisodes",
                LookMode.Value, LookMode.Value);
            if (shelterCells == null)
                shelterCells = new Dictionary<int, IntVec3>();
            if (shelterEpisodes == null)
                shelterEpisodes = new Dictionary<int, int>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (int episode in shelterEpisodes.Values)
                CACombatIntent.ObserveEpisode(episode);
        }

        public override void MapComponentTick()
        {
            AwarenessSettings s = AwarenessMod.Settings;
            // The fast watcher: hidden pawns sense and act on a half-second pulse.
            if (Find.TickManager.TicksGame % 30 == 0)
            {
                try { HiddenRegistry.MaintainFast(map); } catch { }
                try
                {
                    if (s != null && s.raidResponse)
                        MaintainShelters(s);
                    else ClearShelters("raid response disabled");
                }
                catch { }
            }
            if (Find.TickManager.TicksGame % IntervalTicks != 0) return;
            // The survival pump (discovery, stash completion, choke progression, aftermath)
            // serves the ambush/hide/stash features and runs regardless of raid response -
            // its own per-feature gates live inside Maintain.
            try { HiddenRegistry.Maintain(map); } catch { }
            if (s == null || !s.raidResponse)
            {
                ReleaseAutomaticDefenses();
                ClearShelters("raid response disabled");
                return;
            }
            try
            {
                bool activeThreat = CACombatThreat.MapHasActiveThreat(map);
                if (!activeThreat)
                {
                    if (calmSince < 0) calmSince = Find.TickManager.TicksGame;
                    // DangerWatcher itself is cached for 101 ticks. Require a real
                    // calm interval before tearing down an automatic posture.
                    if (Find.TickManager.TicksGame - calmSince >= 120)
                    {
                        ReleaseAutomaticDefenses(preserveDueAccountability: true);
                        HiddenRegistry.ClearEpisode();
                        convenedThisRaid = false;
                        convenedUntil = 0;
                        rallyCell = IntVec3.Invalid;
                        conveneThreatCentroid = IntVec3.Invalid;
                        ClearShelters("no active hostile threat remains");
                        return;
                    }
                }
                else calmSince = -1;

                // The convene clock starts when the COLONY first knows, not when the
                // map does - a plan window burned before anyone has seen the enemy is
                // a convene that visibly never happens.
                var know = KnowledgeMapComponent.For(map);
                bool gateOn = s.knowledgeContacts;
                // This is deliberately coarse. It says only that the colony has
                // raised the raid alarm; it carries no hostile id or contact cell.
                // With the Knowledge feature disabled, the setting's legacy
                // behavior treats the live raid itself as the alarm.
                bool colonyRaidAlarm = !gateOn;
                var colonists = map.mapPawns.FreeColonistsSpawned;
                PruneIneligibleAutomaticDefenses(colonists);
                claimed.Clear();
                SeedClaimedAutomaticDefenseCells(colonists);
                SeedClaimedShelterCells();
                if (gateOn && know != null)
                {
                    for (int i = 0; i < colonists.Count; i++)
                        if (know.KnowsAnyThreat(colonists[i]))
                        {
                            colonyRaidAlarm = true;
                            break;
                        }
                }
                if (!colonyRaidAlarm)
                {
                    ReleaseAutomaticDefenses(
                        preserveDueAccountability: calmSince >= 0);
                    return;
                }

                if (!convenedThisRaid && TrySetupConvene(know, gateOn))
                    convenedThisRaid = true;
                bool convening = rallyCell.IsValid && Find.TickManager.TicksGame < convenedUntil;

                for (int i = 0; i < colonists.Count; i++)
                {
                    var p = colonists[i];
                    if (!CanAutonomouslyReact(p))
                    {
                        DropAutomaticDefense(p);
                        continue;
                    }

                    var withdrawal = map.GetComponent<WithdrawalMapComponent>();
                    if (withdrawal != null && withdrawal.InPlan(p))
                    {
                        DropAutomaticDefense(p);
                        continue;
                    }

                    bool fighter = IsAvailableFighter(p);
                    if (!fighter) DropAutomaticDefense(p);
                    else ReleaseShelter(p, "actor is an available fighter",
                        endJob: false);

                    if (HiddenRegistry.IsHiddenOrOrdered(p)) continue;

                    // A quiet automatic defender with a due direct report keeps the
                    // underlying duty token while the constant-think lane launches or
                    // completes its finite check. Fresh personal danger restores the
                    // defensive planner immediately.
                    if (fighter && CATactical.IsAutomaticDefense(p)
                        && !JobGiver_CAWelfareResponse.PerceivesActiveDanger(p, s)
                        && know != null
                        && know.ShouldRetainAutomaticDefenseForAccountability(p))
                        continue;

                    if (IsEmergencyJob(p, p.CurJob))
                    {
                        DropAutomaticDefense(p);
                        continue;
                    }

                    // Native combat owns this pulse. If it came from an automatic
                    // defense, its seeded order and claim remain; otherwise the slow
                    // responder waits for combat to finish before assigning a post.
                    if (IsNativeAttackJob(p.CurJob)) continue;

                    // Field medicine now runs on the native constant-think lane
                    // from pawn-private welfare facts. This slower group planner
                    // never enumerates live casualties or reads remote bleed rates.

                    // An already-owned civilian envelope is the stable answer to the
                    // same alarm. Do not reselect a destination every slow pulse.
                    if (!fighter && TryContinueShelter(p)) continue;

                    // Identified contact geometry remains actor-local: firsthand,
                    // relayed, or (when Knowledge is disabled) currently visible to
                    // this pawn. A colony alarm is evaluated separately below and
                    // never manufactures an entry in this list.
                    List<IntVec3> actorKnownThreats =
                        PerceivedThreatCells(p, know, gateOn);
                    bool hasPersonalContact = actorKnownThreats.Count > 0;
                    if (!hasPersonalContact)
                    {
                        if (fighter)
                        {
                            DropAutomaticDefense(p);
                            CATrace.Skip(p, "raid response",
                                gateOn ? "knows no personal threat contact; colony alarm cannot assign a fighter"
                                    : "has no personally visible threat contact; colony alarm cannot assign a fighter");
                            continue;
                        }

                        // The exception is deliberately narrow: a generic alarm may
                        // cause a nonfighter to enter enclosed Home shelter. It does
                        // not grant identity, a last-known cell, a firing sector,
                        // concealment direction, or a militant rear-defense post.
                        CATrace.Skip(p, "hostile identity grant",
                            "generic colony raid alarm authorizes civilian shelter only; no hostile identity or cell granted",
                            anchor: p.Position);
                        ShelterRoute alarmRoute;
                        if (TryFindShelter(p, actorKnownThreats, true,
                            out alarmRoute))
                        {
                            claimed.Add(alarmRoute.Cell);
                            AssignShelter(p, alarmRoute, IntVec3.Invalid, true);
                        }
                        continue;
                    }

                    bool hasThreat = BuildCoverSectors(actorKnownThreats);
                    if (!hasThreat)
                    {
                        DropAutomaticDefense(p);
                        continue;
                    }
                    IntVec3 knownContact = threatCentroid;

                    // Convene: Autonomous squadded fighters under a live commanding leader
                    // form up at the rally and hold while the plan is made - unless an
                    // enemy is already on top of them, in which case they fight.
                    if (convening && fighter && AutonomyComponent.LevelOf(p) >= 3
                        && !TryNearestPerceivedThreatCell(p, 6f, out _))
                    {
                        int csq = SquadComponent.SquadOf(p);
                        var cleader = csq > 0 ? SquadComponent.LeaderPawn(csq, map) : null;
                        if (cleader != null && cleader.Spawned && !cleader.Dead
                            && CommsModule.CanCommand(cleader, p))
                        {
                            IntVec3 conveneWatch = conveneThreatCentroid.IsValid
                                ? conveneThreatCentroid : knownContact;
                            CATactical.AssignAutomaticDefense(p, rallyCell, conveneWatch);
                            continue;
                        }
                    }

                    IntVec3 anchor = p.Position;
                    bool rearSector = false;
                    int sq = SquadComponent.SquadOf(p);
                    if (fighter && sq > 0)
                    {
                        var leader = SquadComponent.LeaderPawn(sq, map);
                        int ft = SquadComponent.FireteamOf(p);
                        bool commanded = leader != null && leader.Spawned && CommsModule.CanCommand(leader, p);

                        if (ft > 0 && !SquadComponent.IsFireteamLeader(p))
                        {
                            var ftLead = SquadComponent.FireteamLeadPawn(sq, ft, map);
                            if (ftLead != null && ftLead.Spawned && commanded) anchor = ftLead.Position;
                        }
                        else if (commanded)
                        {
                            anchor = leader.Position;
                        }

                        // Sector split: a smart, connected leader sends Fire team B to the rear line.
                        if (commanded && hasThreat && ft == 2 && coverSecondary.Count > 0
                            && leader.skills != null
                            && leader.skills.GetSkill(SkillDefOf.Intellectual).Level >= 6)
                        {
                            rearSector = true;
                        }
                    }

                    // Survival response for non-fighters: freeze (rare, mood-driven),
                    // a personal-contact militant posture, enclosed Home shelter, and
                    // only then outdoor concealment as the fallback.
                    IntVec3 dest;
                    if (fighter)
                    {
                        // A rifleman doesn't stand and take the charge: hostile inside 5 cells
                        // and a ranged weapon in hand means fall back to cover with distance.
                        IntVec3 closingCell;
                        bool kite = TryNearestPerceivedThreatCell(p, 5f, out closingCell)
                            && p.equipment.Primary.def.IsRangedWeapon;

                        var sector = rearSector ? coverSecondary : coverPrimary;
                        if (kite)
                        {
                            if (!TryFindCoverAwayFrom(p, anchor, sector, closingCell, 8f, out dest))
                            {
                                if (!TryFindCoverPosition(p, anchor, sector, out dest))
                                {
                                    DropAutomaticDefense(p);
                                    continue;
                                }
                            }
                        }
                        else if (!TryFindCoverPosition(p, anchor, sector, out dest))
                        {
                            DropAutomaticDefense(p);
                            continue;
                        }

                        // Planning changes the focus of a persistent native duty. Even
                        // when the selected cover is the pawn's current cell, assigning
                        // the duty establishes an armed posture instead of treating
                        // positioning as the whole response.
                        if (CATactical.AssignAutomaticDefense(p, dest, knownContact))
                            claimed.Add(dest);
                        continue;
                    }
                    else if (s.survivalResponses)
                    {
                        DropAutomaticDefense(p);
                        if (HiddenRegistry.IsHidden(p)) continue;

                        // Freeze: once per raid, and it is COURAGE that decides - the
                        // disposition vector already folds in mood, nerves, and pain.
                        float courage = Disposition.Of(p).courage;
                        if (!HiddenRegistry.FrozeThisRaid.Contains(p.thingIDNumber)
                            && courage < 0.4f
                            && SquadComponent.CombatLiability(p)
                            && Rand.Chance(0.2f * (0.4f - courage)))
                        {
                            HiddenRegistry.FrozeThisRaid.Add(p.thingIDNumber);
                            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                            wait.expiryInterval = 350;
                            p.jobs.StartJob(wait, JobCondition.InterruptForced);
                            continue;
                        }

                        bool child = p.DevelopmentalStage == DevelopmentalStage.Child;
                        // Who the child IS decides: a ten-plus child of a militant creed
                        // stands with their people at the rear line instead of hiding.
                        bool militantChild = child && p.ageTracker != null
                            && p.ageTracker.AgeBiologicalYears >= 10 && HasMilitantIdeo(p);
                        bool hideProfile = (child && !militantChild) || (SquadComponent.CombatLiability(p)
                            && !p.Position.Roofed(map));
                        if (militantChild)
                        {
                            ReleaseShelter(p,
                                "militant child joined the rear defense",
                                endJob: true);
                            var rearLine = coverSecondary.Count > 0 ? coverSecondary : coverPrimary;
                            if (TryFindCoverPosition(p, p.Position, rearLine, out dest))
                            {
                                if (p.Position != dest)
                                {
                                    claimed.Add(dest);
                                    Job stand = JobMaker.MakeJob(JobDefOf.Goto, dest);
                                    stand.locomotionUrgency = LocomotionUrgency.Sprint;
                                    stand.expiryInterval = 600;
                                    p.jobs.StartJob(stand, JobCondition.InterruptForced);
                                }
                                continue;
                            }
                        }

                        ShelterRoute shelterRoute;
                        if (TryFindShelter(p, actorKnownThreats, false,
                            out shelterRoute))
                        {
                            claimed.Add(shelterRoute.Cell);
                            AssignShelter(p, shelterRoute, knownContact, false);
                            continue;
                        }

                        if (hideProfile && HiddenRegistry.TryFindConcealment(p, threatCentroid, hasThreat, out dest))
                        {
                            ReleaseShelter(p, "concealment posture selected",
                                endJob: true);
                            if (p.Position == dest)
                            {
                                HiddenRegistry.SetHidden(p, dest);
                                continue;
                            }
                            claimed.Add(dest);
                            Job go = JobMaker.MakeJob(JobDefOf.Goto, dest);
                            go.locomotionUrgency = LocomotionUrgency.Sprint;
                            go.expiryInterval = 600;
                            p.jobs.StartJob(go, JobCondition.InterruptForced);
                            continue;
                        }
                        continue;
                    }
                    else
                    {
                        DropAutomaticDefense(p);
                        ShelterRoute shelterRoute;
                        if (!TryFindShelter(p, actorKnownThreats, false,
                            out shelterRoute)) continue;
                        claimed.Add(shelterRoute.Cell);
                        AssignShelter(p, shelterRoute, knownContact, false);
                    }
                }
            }
            catch { }
        }

        // Build one decision-maker's defense geometry. With Knowledge enabled the
        // only inputs are this pawn's copied facts; a leader plan calls the same method
        // with the actual selected leader. Candidate cells survive only when cover is
        // directionally meaningful against that perceived centroid.
        private bool BuildCoverSectors(Pawn knower, KnowledgeMapComponent know, bool gateOn)
        {
            return BuildCoverSectors(PerceivedThreatCells(knower, know, gateOn));
        }

        private bool BuildCoverSectors(List<IntVec3> threats)
        {
            coverPrimary.Clear();
            coverSecondary.Clear();
            coverScores.Clear();
            defenseThreatCells.Clear();

            if (threats.Count == 0)
            {
                threatCentroid = IntVec3.Invalid;
                return false;
            }

            IntVec3 threat = ThreatCentroidOf(threats);
            threatCentroid = threat;
            defenseThreatCells.AddRange(threats);

            var home = map.areaManager.Home;
            var all = new List<IntVec3>();
            var seenCells = new HashSet<IntVec3>();
            var buildings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.Faction != Faction.OfPlayer) continue;
                if (b.def.fillPercent < 0.3f || b.def.fillPercent > 0.8f) continue;
                var cells = GenAdj.CellsAdjacentCardinal(b);
                foreach (var c in cells)
                {
                    if (!c.InBounds(map) || !c.Standable(map)) continue;
                    if (home != null && !home[c]) continue;
                    if (!seenCells.Add(c)) continue;
                    float cover = CoverUtility.CalculateOverallBlockChance(c, threat, map);
                    if (cover <= 0f) continue;
                    coverScores[c] = cover;
                    all.Add(c);
                }
            }

            if (all.Count == 0) return true;
            all.Sort((a, b) => a.DistanceToSquared(threat).CompareTo(b.DistanceToSquared(threat)));
            int primaryCount = UnityEngine.Mathf.Max(1, (all.Count * 3) / 5);
            for (int i = 0; i < all.Count; i++)
            {
                if (i < primaryCount) coverPrimary.Add(all[i]);
                else coverSecondary.Add(all[i]);
            }
            return true;
        }

        private List<IntVec3> PerceivedThreatCells(Pawn knower,
            KnowledgeMapComponent know, bool gateOn)
        {
            var result = new List<IntVec3>();
            if (gateOn)
            {
                if (know == null || knower == null) return result;
                var latest = new Dictionary<int, ThreatContactSnapshot>();
                var contacts = know.FreshContacts(knower);
                for (int i = 0; i < contacts.Count; i++)
                {
                    ThreatContactSnapshot old;
                    var contact = contacts[i];
                    if (!latest.TryGetValue(contact.HostileId, out old)
                        || contact.SourceTick > old.SourceTick
                        || (contact.SourceTick == old.SourceTick
                            && contact.AcquiredTick > old.AcquiredTick))
                        latest[contact.HostileId] = contact;
                }
                foreach (var contact in latest.Values)
                    if (contact.Cell.IsValid && contact.Cell.InBounds(map))
                        result.Add(contact.Cell);
                return result;
            }

            // Knowledge disabled: consumers retain immediate sight, but receive no
            // remembered or relayed positions from the disabled substrate.
            if (knower == null) return result;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var hostile = pawns[i];
                if (!KnowledgeMapComponent.CanCurrentlySeeHostile(
                    knower, hostile)) continue;
                result.Add(hostile.Position);
            }
            return result;
        }

        private static IntVec3 ThreatCentroidOf(List<IntVec3> threats)
        {
            if (threats == null || threats.Count == 0)
                return IntVec3.Invalid;
            float x = 0f;
            float z = 0f;
            for (int i = 0; i < threats.Count; i++)
            {
                x += threats[i].x;
                z += threats[i].z;
            }
            return new IntVec3((int)(x / threats.Count), 0,
                (int)(z / threats.Count));
        }

        private bool TryNearestPerceivedThreatCell(Pawn p, float dist,
            out IntVec3 threatCell)
        {
            threatCell = IntVec3.Invalid;
            float bestD = dist;
            var settings = AwarenessMod.Settings;
            var know = KnowledgeMapComponent.For(map);
            bool gate = settings != null && settings.knowledgeContacts;
            var threats = PerceivedThreatCells(p, know, gate);
            for (int i = 0; i < threats.Count; i++)
            {
                float d = p.Position.DistanceTo(threats[i]);
                if (d <= bestD) { bestD = d; threatCell = threats[i]; }
            }
            return threatCell.IsValid;
        }

        private bool TryFindCoverAwayFrom(Pawn p, IntVec3 anchor, List<IntVec3> sector, IntVec3 threat, float minDist, out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            float bestCover = -1f;
            float bestDistance = float.MaxValue;
            IntVec3 ownCell;
            bool hasOwnCell = TryAutomaticDefenseCell(p, out ownCell);
            for (int i = 0; i < sector.Count; i++)
            {
                var c = sector[i];
                if (claimed.Contains(c) && (!hasOwnCell || c != ownCell)) continue;
                if (c.DistanceTo(threat) < minDist) continue;
                if (!p.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                if (!CanDefendKnownContactFrom(p, c, defenseThreatCells))
                    continue;
                float d = anchor.DistanceToSquared(c);
                float cover;
                if (!coverScores.TryGetValue(c, out cover) || cover <= 0f) continue;
                if (cover > bestCover + 0.0001f
                    || (UnityEngine.Mathf.Abs(cover - bestCover) <= 0.0001f
                        && d < bestDistance))
                {
                    bestCover = cover;
                    bestDistance = d;
                    dest = c;
                }
            }
            return dest.IsValid;
        }

        // Militant creed per the game's own meme labels on the pawn's actual ideoligion.
        private static bool HasMilitantIdeo(Pawn p)
        {
            try
            {
                var ideo = p.Ideo;
                if (ideo == null) return false;
                foreach (var m in ideo.memes)
                {
                    var d = m.defName;
                    if (d == "Raider" || d == "Supremacist" || d == "PainIsVirtue") return true;
                }
            }
            catch { }
            return false;
        }

        // The convene setup: a rally behind the eventual line, plan time scaled by the
        // best commanding leader's Intellectual. No squads with a live commanding leader
        // and Autonomous members means no convene - unstructured colonies skip straight
        // to individual scrambling, and structured-but-dull ones mill the longest.
        private bool TrySetupConvene(KnowledgeMapComponent know, bool gateOn)
        {
            int bestInt = -1;
            Pawn bestLeader = null;
            var consideredLeaders = new HashSet<int>();
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var p = colonists[i];
                if (AutonomyComponent.LevelOf(p) < 3) continue;
                if (p.equipment == null || p.equipment.Primary == null) continue;
                if (p.WorkTagIsDisabled(WorkTags.Violent) || SquadComponent.CombatLiability(p)) continue;
                int sq = SquadComponent.SquadOf(p);
                if (sq <= 0) continue;
                var leader = SquadComponent.LeaderPawn(sq, map);
                if (leader == null || !leader.Spawned || leader.Dead || leader.Downed
                    || leader.InMentalState || HasDirectPlayerControl(leader)) continue;
                if (!CommsModule.CanCommand(leader, p)) continue;
                if (!consideredLeaders.Add(leader.thingIDNumber)) continue;
                if (gateOn && (know == null || !know.KnowsAnyThreat(leader))) continue;
                int li = leader.skills != null ? leader.skills.GetSkill(SkillDefOf.Intellectual).Level : 0;
                if (li > bestInt)
                {
                    bestInt = li;
                    bestLeader = leader;
                }
            }
            if (bestLeader == null) return false; // no structure worth convening

            // A convened plan is the selected leader's plan. Its geometry comes from
            // that leader's own copied contacts, never a colony-wide union.
            if (!BuildCoverSectors(bestLeader, know, gateOn)) return false;

            // rally: pulled back from the primary line, away from the threat
            if (coverPrimary.Count == 0 || !threatCentroid.IsValid) return false;
            var sum = IntVec3.Zero;
            for (int i = 0; i < coverPrimary.Count; i++) sum += coverPrimary[i];
            var lineCentroid = new IntVec3(sum.x / coverPrimary.Count, 0, sum.z / coverPrimary.Count);
            var away = (lineCentroid.ToVector3() - threatCentroid.ToVector3()).normalized;
            var rough = lineCentroid + IntVec3.FromVector3(away * 9f);
            var cell = CellFinder.StandableCellNear(rough, map, 8f, null);
            if (!cell.IsValid) return false;

            rallyCell = cell;
            conveneThreatCentroid = threatCentroid;
            int planTicks = UnityEngine.Mathf.Clamp(750 - bestInt * 45, 150, 750);
            convenedUntil = Find.TickManager.TicksGame + planTicks;
            Messages.Message("The squads convene to plan the defense.",
                new LookTargets(rallyCell, map), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        private bool CanAutonomouslyReact(Pawn p)
        {
            if (p == null || p.Dead || p.Downed || !p.Spawned) return false;
            if (p.InMentalState) return false;
            if (p.DevelopmentalStage == DevelopmentalStage.Baby) return false;
            if (AutonomyComponent.LevelOf(p) < 2) return false;
            return !HasDirectPlayerControl(p);
        }

        private static bool IsAvailableFighter(Pawn p)
        {
            return p != null && AutonomyComponent.LevelOf(p) >= 2
                && p.equipment != null && p.equipment.Primary != null
                && !p.WorkTagIsDisabled(WorkTags.Violent)
                && !SquadComponent.CombatLiability(p);
        }

        private bool TryContinueShelter(Pawn pawn)
        {
            if (pawn == null) return false;
            IntVec3 cell;
            int episode;
            Room assignedRoom;
            if (!shelterCells.TryGetValue(pawn.thingIDNumber, out cell)
                || !shelterEpisodes.TryGetValue(pawn.thingIDNumber,
                    out episode) || episode <= 0
                || !IsOwnedShelterCell(cell, out assignedRoom))
                return false;
            Room currentRoom;
            if (IsOwnedShelterCell(pawn.Position, out currentRoom)
                && currentRoom != assignedRoom) return false;
            claimed.Add(cell);
            StartShelterJob(pawn, cell, episode,
                "existing active-threat shelter ownership");
            return true;
        }

        private bool CanKeepAutomaticDefense(Pawn p)
        {
            if (!CanAutonomouslyReact(p)) return false;
            if (p.equipment == null || p.equipment.Primary == null) return false;
            if (p.WorkTagIsDisabled(WorkTags.Violent) || SquadComponent.CombatLiability(p))
                return false;
            var withdrawal = map.GetComponent<WithdrawalMapComponent>();
            if (withdrawal != null && withdrawal.InPlan(p)) return false;
            if (HiddenRegistry.IsHiddenOrOrdered(p)) return false;
            IntVec3 assigned;
            if (!TryAutomaticDefenseCell(p, out assigned)) return false;
            AwarenessSettings settings = AwarenessMod.Settings;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
            bool gateOn = settings != null && settings.knowledgeContacts;
            List<IntVec3> threats = PerceivedThreatCells(p, knowledge, gateOn);
            return threats.Count > 0
                && CanDefendKnownContactFrom(p, assigned, threats);
        }

        private static bool HasDirectPlayerControl(Pawn p)
        {
            if (p == null || p.Drafted) return true;
            if (p.CurJob != null && p.CurJob.playerForced) return true;
            return p.jobs != null && p.jobs.jobQueue != null
                && p.jobs.jobQueue.AnyPlayerForced;
        }

        private static bool IsNativeAttackJob(Job job)
        {
            return job != null
                && (job.def == JobDefOf.AttackMelee
                    || job.def == JobDefOf.AttackStatic
                    || job.def == CA_Defs.BoundedMeleeDefense
                    || job.def == CA_Defs.BoundedRangedDefense);
        }

        private bool IsEmergencyJob(Pawn pawn, Job job)
        {
            if (job == null) return false;
            string ignored;
            if (job.def == JobDefOf.BeatFire
                || job.def == JobDefOf.ExtinguishFiresNearby)
                return !BlocksOptionalFireResponse(pawn, job, out ignored);
            return job.def == JobDefOf.TendPatient
                || job.def == JobDefOf.TendEntity
                || job.def == JobDefOf.Rescue
                || job.def == CA_Defs.EmergencySelfTend
                || job.def == CA_Defs.AssessCasualty
                || job.def == CA_Defs.CheckWelfare
                || job.def == CA_Defs.WaitForWelfareSupport
                || job.def == CA_Defs.FightingWithdrawal
                || job.def == CA_Defs.CombatRecovery
                || job.def == CA_Defs.CombatMove
                || job.def == JobDefOf.ExtinguishSelf
                || job.def == JobDefOf.Flee
                || job.def == JobDefOf.FleeAndCower
                || job.def == JobDefOf.FleeAndCowerShort;
        }

        internal bool BlocksOptionalFireResponse(Pawn pawn, Job candidate,
            out string reason)
        {
            reason = null;
            if (pawn == null || candidate == null
                || candidate.def != JobDefOf.BeatFire
                    && candidate.def != JobDefOf.ExtinguishFiresNearby
                || pawn.Map != map
                || !CACombatThreat.MapHasActiveThreat(map)
                || candidate.playerForced
                || PawnUtility.PlayerForcedJobNowOrSoon(pawn))
                return false;

            Job current = pawn.CurJob;
            if (current != null && (current.def == JobDefOf.Flee
                || current.def == JobDefOf.FleeAndCower
                || current.def == JobDefOf.FleeAndCowerShort))
            {
                reason = "active hostile-threat flight has precedence over optional firefighting";
                return true;
            }

            IntVec3 fireCell = candidate.targetA.IsValid
                ? candidate.targetA.Cell : IntVec3.Invalid;
            float fireDistance = fireCell.IsValid
                ? pawn.Position.DistanceTo(fireCell) : float.MaxValue;
            if (CAImmediateCombat.RequiresCombatRecoveryNow(pawn)
                && fireDistance > 6.9f)
            {
                reason = "health-adjusted combat recovery blocks remote optional firefighting during an active hostile threat";
                return true;
            }
            if (CACombatThreat.PerceivesActiveThreat(pawn)
                && fireDistance > 12.9f)
            {
                reason = "a personally perceived active contact blocks remote optional firefighting; local fire remains eligible";
                return true;
            }

            if (!HasShelter(pawn)) return false;
            if (fireCell.IsValid && IsInsideShelterEnvelope(pawn, fireCell))
                return false;
            reason = "retained civilian shelter permits firefighting only inside the owned enclosed shelter room during an active hostile threat";
            return true;
        }

        private void PruneIneligibleAutomaticDefenses(List<Pawn> colonists)
        {
            for (int i = 0; i < colonists.Count; i++)
                if (CATactical.IsAutomaticDefense(colonists[i])
                    && !CanKeepAutomaticDefense(colonists[i]))
                    CATactical.ReleaseAutomaticDefense(colonists[i]);
        }

        private void SeedClaimedAutomaticDefenseCells(List<Pawn> colonists)
        {
            for (int i = 0; i < colonists.Count; i++)
            {
                IntVec3 cell;
                if (TryAutomaticDefenseCell(colonists[i], out cell)) claimed.Add(cell);
            }
        }

        private void SeedClaimedShelterCells()
        {
            foreach (IntVec3 cell in shelterCells.Values)
                if (cell.IsValid && cell.InBounds(map)) claimed.Add(cell);
        }

        private static bool TryAutomaticDefenseCell(Pawn p, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            if (job == null) return false;
            int kind;
            IntVec3 watch;
            return job.TryGetOrder(p, out kind, out cell, out watch)
                && kind == LordJob_CATactical.KindRaidDefense && cell.IsValid;
        }

        private void DropAutomaticDefense(Pawn p)
        {
            IntVec3 cell;
            bool hadCell = TryAutomaticDefenseCell(p, out cell);
            CATactical.ReleaseAutomaticDefense(p);
            if (!hadCell) return;

            claimed.Remove(cell);
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                IntVec3 otherCell;
                if (colonists[i] != p
                    && TryAutomaticDefenseCell(colonists[i], out otherCell)
                    && otherCell == cell)
                {
                    claimed.Add(cell);
                    break;
                }
            }
        }

        private void ReleaseAutomaticDefenses(
            bool preserveDueAccountability = false)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned;
            KnowledgeMapComponent knowledge = preserveDueAccountability
                ? KnowledgeMapComponent.For(map) : null;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (preserveDueAccountability
                    && CanKeepAutomaticDefense(pawn)
                    && knowledge != null
                    && knowledge.ShouldRetainAutomaticDefenseForAccountability(
                        pawn)) continue;
                CATactical.ReleaseAutomaticDefense(pawn);
            }
        }

        private bool TryFindCoverPosition(Pawn p, IntVec3 anchor, List<IntVec3> sector, out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            float bestCover = -1f;
            float bestDistance = float.MaxValue;
            IntVec3 ownCell;
            bool hasOwnCell = TryAutomaticDefenseCell(p, out ownCell);
            for (int i = 0; i < sector.Count; i++)
            {
                var c = sector[i];
                if (claimed.Contains(c) && (!hasOwnCell || c != ownCell)) continue;
                if (!p.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                if (!CanDefendKnownContactFrom(p, c, defenseThreatCells))
                    continue;
                float d = anchor.DistanceToSquared(c);
                float cover;
                if (!coverScores.TryGetValue(c, out cover) || cover <= 0f) continue;
                if (cover > bestCover + 0.0001f
                    || (UnityEngine.Mathf.Abs(cover - bestCover) <= 0.0001f
                        && d < bestDistance))
                {
                    bestCover = cover;
                    bestDistance = d;
                    dest = c;
                }
            }
            return dest.IsValid;
        }

        private static bool CanDefendKnownContactFrom(Pawn pawn,
            IntVec3 cell, List<IntVec3> threats)
        {
            if (pawn == null || pawn.Map == null || !cell.IsValid
                || threats == null || threats.Count == 0) return false;
            Verb verb = pawn.TryGetAttackVerb(null,
                allowManualCastWeapons: true);
            if (verb == null) return false;
            for (int i = 0; i < threats.Count; i++)
            {
                IntVec3 threat = threats[i];
                if (!threat.IsValid || !threat.InBounds(pawn.Map)) continue;
                if (verb.verbProps.IsMeleeAttack)
                {
                    if (cell.InHorDistOf(threat, 1.9f)
                        && GenSight.LineOfSight(cell, threat, pawn.Map, true))
                        return true;
                    continue;
                }
                if (verb.CanHitTargetFrom(cell, new LocalTargetInfo(threat)))
                    return true;
            }
            return false;
        }

        private bool TryFindShelter(Pawn pawn,
            List<IntVec3> actorKnownThreats, bool genericAlarm,
            out ShelterRoute selected)
        {
            selected = null;
            if (pawn == null || pawn.Map != map || !pawn.Position.InBounds(map))
                return false;

            Room currentRoom;
            if (!IsProperEnclosedHomeCell(pawn.Position, out currentRoom))
                currentRoom = null;
            List<IntVec3> candidates = ShelterCandidates(pawn, currentRoom);
            bool fallback = false;
            IntVec3 contact = ThreatCentroidOf(actorKnownThreats);
            if (candidates.Count == 0)
            {
                candidates = FallbackShelterCandidates(pawn);
                fallback = candidates.Count > 0;
                if (!fallback)
                {
                    CATrace.Skip(pawn, "civilian shelter selection",
                        (genericAlarm
                            ? "generic alarm granted no hostile identity or cell; "
                            : "")
                        + (currentRoom != null
                            ? "current enclosed Home room has no unclaimed standable shelter cell"
                            : "no roofed Home fallback exists in the bounded search"),
                        contact: contact.IsValid ? (IntVec3?)contact : null,
                        anchor: pawn.Position);
                    return false;
                }
                CATrace.Pawn(pawn,
                    "civilian shelter FALLBACK: no proper enclosed Home cell remains; retaining the safest roofed Home room instead of reopening ordinary work during the active raid",
                    contact: contact.IsValid ? (IntVec3?)contact : null,
                    anchor: pawn.Position);
            }

            var routes = new List<ShelterRoute>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                ShelterRoute route;
                if (TryMeasureShelterRoute(pawn, candidates[i], currentRoom,
                    actorKnownThreats, out route)) routes.Add(route);
            }
            if (routes.Count == 0 && !fallback)
            {
                candidates = FallbackShelterCandidates(pawn);
                fallback = candidates.Count > 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    ShelterRoute route;
                    if (TryMeasureShelterRoute(pawn, candidates[i],
                            currentRoom, actorKnownThreats, out route))
                        routes.Add(route);
                }
                if (routes.Count > 0)
                    CATrace.Pawn(pawn,
                        "civilian shelter FALLBACK: proper enclosed Home candidates had no traversable route; retaining the safest reachable roofed Home room instead of reopening ordinary work during the active raid",
                        contact: contact.IsValid ? (IntVec3?)contact : null,
                        anchor: pawn.Position);
            }
            if (routes.Count == 0)
            {
                CATrace.Skip(pawn, "civilian shelter selection",
                    (genericAlarm
                        ? "generic alarm granted no hostile identity or cell; "
                        : "")
                    + "all " + candidates.Count
                    + " enclosed Home candidates failed actual PawnPath construction",
                    contact: contact.IsValid ? (IntVec3?)contact : null,
                    anchor: pawn.Position);
                return false;
            }

            bool hasUnexposed = false;
            for (int i = 0; i < routes.Count; i++)
                if (!routes[i].Exposed) { hasUnexposed = true; break; }

            int rejectedExposed = 0;
            for (int i = 0; i < routes.Count; i++)
            {
                ShelterRoute route = routes[i];
                if (hasUnexposed && route.Exposed)
                {
                    rejectedExposed++;
                    continue;
                }
                if (selected == null || route.Score > selected.Score)
                    selected = route;
            }
            if (selected == null) return false;
            selected.ShortlistCount = candidates.Count;
            selected.ViablePathCount = routes.Count;
            selected.PathFailureCount = candidates.Count - routes.Count;
            selected.RejectedExposedCount = rejectedExposed;
            selected.LowerScoredCount = routes.Count - rejectedExposed - 1;
            for (int i = 0; i < routes.Count; i++)
            {
                ShelterRoute alternative = routes[i];
                if (alternative == selected) continue;
                if (alternative.UnroofedCells > 0)
                    selected.AlternativesWithUnroofed++;
                if (alternative.KnownHostileLosCells > 0)
                    selected.AlternativesWithKnownHostileLos++;
                if (alternative.HazardCells > 0)
                    selected.AlternativesWithHazard++;
                if (alternative.NewWaterEntries > 0)
                    selected.AlternativesWithNewWater++;
            }
            return true;
        }

        private List<IntVec3> FallbackShelterCandidates(Pawn pawn)
        {
            var result = new List<IntVec3>(ShelterCandidateLimit);
            int limit = GenRadial.NumCellsInRadius(ShelterSearchRadius);
            for (int i = 0; i < limit && result.Count < ShelterCandidateLimit;
                i++)
            {
                IntVec3 cell = pawn.Position + GenRadial.RadialPattern[i];
                Room room;
                if (!IsFallbackShelterCell(cell, out room)
                    || ShelterCellClaimedByOther(pawn, cell)) continue;
                result.Add(cell);
            }
            return result;
        }

        private List<IntVec3> ShelterCandidates(Pawn pawn, Room currentRoom)
        {
            var result = new List<IntVec3>(ShelterCandidateLimit);
            var roomCounts = new Dictionary<int, int>();
            int limit = GenRadial.NumCellsInRadius(ShelterSearchRadius);
            for (int i = 0; i < limit && result.Count < ShelterCandidateLimit; i++)
            {
                IntVec3 cell = pawn.Position + GenRadial.RadialPattern[i];
                Room room;
                if (!IsProperEnclosedHomeCell(cell, out room)) continue;
                if (currentRoom != null && room != currentRoom) continue;
                if (ShelterCellClaimedByOther(pawn, cell)) continue;

                if (currentRoom == null)
                {
                    int count;
                    roomCounts.TryGetValue(room.ID, out count);
                    if (count >= ShelterCandidatesPerRoom) continue;
                    roomCounts[room.ID] = count + 1;
                }
                result.Add(cell);
            }
            return result;
        }

        private bool IsProperEnclosedHomeCell(IntVec3 cell, out Room room)
        {
            room = null;
            var home = map.areaManager.Home;
            if (!cell.IsValid || !cell.InBounds(map) || !cell.Standable(map)
                || !cell.Roofed(map) || home == null || !home[cell])
                return false;
            room = cell.GetRoom(map);
            return room != null && room.ProperRoom && !room.IsDoorway
                && !room.PsychologicallyOutdoors && !room.TouchesMapEdge;
        }

        private bool IsFallbackShelterCell(IntVec3 cell, out Room room)
        {
            room = null;
            var home = map.areaManager.Home;
            if (!cell.IsValid || !cell.InBounds(map) || !cell.Standable(map)
                || !cell.Roofed(map) || home == null || !home[cell])
                return false;
            room = cell.GetRoom(map);
            return room != null && room.ProperRoom && !room.IsDoorway
                && !room.TouchesMapEdge;
        }

        private bool IsOwnedShelterCell(IntVec3 cell, out Room room)
        {
            return IsProperEnclosedHomeCell(cell, out room)
                || IsFallbackShelterCell(cell, out room);
        }

        private bool ShelterCellClaimedByOther(Pawn pawn, IntVec3 cell)
        {
            IntVec3 own;
            bool hasOwn = shelterCells.TryGetValue(pawn.thingIDNumber, out own);
            if (claimed.Contains(cell) && cell != pawn.Position
                && (!hasOwn || own != cell)) return true;
            foreach (KeyValuePair<int, IntVec3> pair in shelterCells)
                if (pair.Key != pawn.thingIDNumber && pair.Value == cell)
                    return true;
            return false;
        }

        private bool TryMeasureShelterRoute(Pawn pawn, IntVec3 destination,
            Room currentRoom, List<IntVec3> actorKnownThreats,
            out ShelterRoute route)
        {
            route = null;
            using (PawnPath path = map.pathFinder.FindPathNow(
                pawn.Position, destination, pawn, null, PathEndMode.OnCell))
            {
                if (path == null || !path.Found
                    || path.NodesReversed == null
                    || path.NodesReversed.Count == 0) return false;

                var measured = new ShelterRoute();
                measured.Cell = destination;
                measured.Length = path.NodesReversed.Count > 0
                    ? path.NodesReversed.Count - 1 : 0;
                measured.PathCost = path.TotalCost;
                Room currentCellRoom;
                measured.CurrentCellEnclosedHome =
                    IsProperEnclosedHomeCell(pawn.Position,
                        out currentCellRoom);
                measured.CurrentCellUnroofed = !pawn.Position.Roofed(map);
                measured.CurrentCellKnownHostileLos =
                    ActorKnownHostileLosAt(pawn.Position,
                        actorKnownThreats);
                measured.CurrentCellHazard = IsRouteHazard(pawn,
                    pawn.Position);
                measured.CurrentCellWater = pawn.Position
                    .GetTerrain(map).IsWater;
                measured.StayedAtCurrentCell = destination == pawn.Position;
                bool previousWater = pawn.Position.GetTerrain(map).IsWater;
                int forwardStep = 0;
                for (int i = path.NodesReversed.Count - 1; i >= 0;
                    i--, forwardStep++)
                {
                    IntVec3 cell = path.NodesReversed[i];
                    if (!cell.InBounds(map)) return false;
                    if (currentRoom != null && cell.GetRoom(map) != currentRoom)
                        return false;

                    bool water = cell.GetTerrain(map).IsWater;
                    bool newWater = water && !previousWater;
                    // The origin is unavoidable for a moving route. A stay-put
                    // route has no later step, so its current cell is the decision
                    // and must still be assessed.
                    bool assess = path.NodesReversed.Count == 1 || forwardStep > 0;
                    bool unroofed = !cell.Roofed(map);
                    bool hostileLos = ActorKnownHostileLosAt(
                        cell, actorKnownThreats);
                    bool hazard = IsRouteHazard(pawn, cell);
                    if (assess)
                    {
                        if (unroofed) measured.UnroofedCells++;
                        if (hostileLos) measured.KnownHostileLosCells++;
                        if (hazard) measured.HazardCells++;
                        if (newWater) measured.NewWaterEntries++;
                    }
                    if (i == 0)
                    {
                        measured.DestinationUnroofed = unroofed;
                        measured.DestinationKnownHostileLos = hostileLos;
                        measured.DestinationHazard = hazard;
                        measured.DestinationWater = water;
                        measured.DestinationExposed = unroofed || hostileLos
                            || hazard || newWater;
                    }
                    previousWater = water;
                }

                Room destinationRoom;
                measured.InHome = map.areaManager.Home != null
                    && map.areaManager.Home[destination];
                measured.Enclosed = IsProperEnclosedHomeCell(destination,
                    out destinationRoom);
                measured.CurrentRoom = currentRoom != null
                    && destinationRoom == currentRoom;
                measured.EmptyCell = destination.GetEdifice(map) == null;
                measured.Score = (measured.InHome ? 60f : 0f)
                    + (measured.Enclosed ? 60f : 0f)
                    + (measured.CurrentRoom ? 30f : 0f)
                    + (measured.EmptyCell ? 2f : 0f)
                    + (!measured.DestinationExposed ? 18f : 0f)
                    + (destination == pawn.Position
                        && !measured.DestinationExposed ? 35f : 0f)
                    - measured.Length * 1.5f
                    - measured.PathCost * 0.002f
                    - measured.UnroofedCells * 8f
                    - measured.KnownHostileLosCells * 18f
                    - measured.HazardCells * 24f
                    - measured.NewWaterEntries * 14f
                    - (destinationRoom != null
                        ? destinationRoom.OpenRoofCount * 0.05f : 0f);
                route = measured;
                return true;
            }
        }

        private bool ActorKnownHostileLosAt(IntVec3 cell,
            List<IntVec3> actorKnownThreats)
        {
            if (actorKnownThreats == null || actorKnownThreats.Count == 0)
                return false;
            for (int i = 0; i < actorKnownThreats.Count; i++)
                if (actorKnownThreats[i].IsValid
                    && GenSight.LineOfSight(cell, actorKnownThreats[i],
                        map, true)) return true;
            return false;
        }

        private bool IsRouteHazard(Pawn pawn, IntVec3 cell)
        {
            TerrainDef terrain = cell.GetTerrain(map);
            return cell.GetDangerFor(pawn, map) != Danger.None
                || terrain != null && terrain.dangerous
                || PawnUtility.KnownDangerAt(cell, map, pawn)
                || cell.ContainsStaticFire(map);
        }

        private void AssignShelter(Pawn pawn, ShelterRoute route,
            IntVec3 knownContact, bool genericAlarm)
        {
            if (pawn == null || route == null || !route.Cell.IsValid) return;
            IntVec3 cell = route.Cell;
            int id = pawn.thingIDNumber;
            int episode = 0;
            IntVec3 existing;
            bool hadOwnership = shelterCells.TryGetValue(id, out existing)
                && shelterEpisodes.TryGetValue(id, out episode)
                && episode > 0;
            bool same = hadOwnership && existing == cell;
            if (!same)
            {
                if (!hadOwnership) episode = CACombatIntent.NewEpisode();
                shelterCells[id] = cell;
                shelterEpisodes[id] = episode;
                CAIntentContext context = hadOwnership
                    ? new CAIntentContext(episode,
                        CAIntentOrigin.Continuation,
                        CAIntentController.Shelter, id)
                    : CACombatIntent.Autonomous(pawn,
                        CAIntentController.Shelter, episode);
                if (genericAlarm)
                    CATrace.Pawn(pawn,
                        "generic colony raid alarm accepted for civilian shelter; no hostile identity or cell granted",
                        destination: cell, anchor: pawn.Position,
                        intent: context);
                CATrace.Pawn(pawn, "shelter route SELECTED - "
                    + route.Metrics,
                    contact: knownContact.IsValid
                        ? (IntVec3?)knownContact : null,
                    destination: cell, anchor: pawn.Position,
                    intent: context);
                CATrace.Pawn(pawn, "shelter intent "
                    + (hadOwnership ? "RELOCATED within active threat"
                        : "ESTABLISHED in enclosed Home room"),
                    contact: knownContact.IsValid
                        ? (IntVec3?)knownContact : null,
                    destination: cell, anchor: pawn.Position,
                    intent: context);
            }
            StartShelterJob(pawn, cell, episode,
                "established; " + route.Metrics);
        }

        private void MaintainShelters(AwarenessSettings settings)
        {
            if (shelterCells.Count == 0) return;
            if (!CACombatThreat.MapHasActiveThreat(map))
            {
                ClearShelters("no active hostile threat remains");
                return;
            }

            var ids = new List<int>(shelterCells.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                Pawn pawn = PawnById(id);
                IntVec3 cell;
                int episode;
                if (pawn == null)
                {
                    shelterCells.Remove(id);
                    shelterEpisodes.Remove(id);
                    continue;
                }
                if (pawn.Dead || pawn.Downed || !pawn.Spawned
                    || pawn.InMentalState || AutonomyComponent.LevelOf(pawn) < 2
                    || !shelterCells.TryGetValue(id, out cell)
                    || !shelterEpisodes.TryGetValue(id, out episode))
                {
                    // Removing the durable ownership record alone leaves the goto
                    // toil free to carry the pawn all the way to an obsolete shelter.
                    ReleaseShelter(pawn,
                        "actor no longer qualifies for this shelter intent",
                        endJob: true);
                    continue;
                }
                if (IsAvailableFighter(pawn))
                {
                    ReleaseShelter(pawn, "actor is an available fighter",
                        endJob: true);
                    continue;
                }

                CAIntentContext continuation = new CAIntentContext(episode,
                    CAIntentOrigin.Continuation,
                    CAIntentController.Shelter, id);
                if (HasDirectPlayerControl(pawn))
                {
                    CATrace.Skip(pawn, "shelter job decision",
                        "direct player control has precedence; shelter ownership retained",
                        destination: cell, anchor: pawn.Position,
                        intent: continuation);
                    continue;
                }
                if (IsEmergencyJob(pawn, pawn.CurJob))
                {
                    CATrace.Skip(pawn, "shelter job decision",
                        "emergency work has precedence; shelter ownership retained",
                        destination: cell, anchor: pawn.Position,
                        intent: continuation);
                    continue;
                }

                Room ignoredRoom;
                bool invalidCell = !IsOwnedShelterCell(cell,
                    out ignoredRoom);
                bool insideEnvelope = IsInsideShelterEnvelope(pawn,
                    pawn.Position);
                bool followingOwnedRoute = pawn.CurJob != null
                    && pawn.CurJob.def == CA_Defs.Shelter
                    && pawn.CurJob.targetA.Cell == cell;
                if (invalidCell || !insideEnvelope && !followingOwnedRoute)
                {
                    KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
                    bool gateOn = settings != null
                        && settings.knowledgeContacts;
                    List<IntVec3> actorKnownThreats =
                        PerceivedThreatCells(pawn, knowledge, gateOn);
                    IntVec3 contact = ThreatCentroidOf(actorKnownThreats);
                    ShelterRoute replacement;
                    if (!TryFindShelter(pawn, actorKnownThreats,
                        actorKnownThreats.Count == 0, out replacement))
                    {
                        if (invalidCell)
                            ReleaseShelter(pawn,
                                "assigned enclosed Home cell became invalid and no replacement route exists",
                                endJob: true);
                        continue;
                    }
                    shelterCells[id] = replacement.Cell;
                    cell = replacement.Cell;
                    CATrace.Pawn(pawn, "shelter route SELECTED for "
                        + (invalidCell ? "RELOCATION" : "RESUMPTION")
                        + " - " + replacement.Metrics
                        + (actorKnownThreats.Count == 0
                            ? "; generic alarm granted no hostile identity or cell"
                            : ""),
                        contact: contact.IsValid ? (IntVec3?)contact : null,
                        destination: cell, anchor: pawn.Position,
                        intent: continuation);
                }
                StartShelterJob(pawn, cell, episode, "persistent active-threat envelope");
            }
        }

        private void StartShelterJob(Pawn pawn, IntVec3 cell, int episode,
            string reason)
        {
            CAIntentContext continuation = new CAIntentContext(episode,
                CAIntentOrigin.Continuation,
                CAIntentController.Shelter, pawn.thingIDNumber);
            if (pawn.jobs == null)
            {
                CATrace.Skip(pawn, "shelter job decision",
                    "pawn has no job tracker", destination: cell,
                    anchor: pawn.Position, intent: continuation);
                return;
            }
            if (HasDirectPlayerControl(pawn))
            {
                CATrace.Skip(pawn, "shelter job decision",
                    "direct player control has precedence; shelter ownership retained",
                    destination: cell, anchor: pawn.Position,
                    intent: continuation);
                return;
            }
            if (IsEmergencyJob(pawn, pawn.CurJob))
            {
                CATrace.Skip(pawn, "shelter job decision",
                    "emergency work has precedence; shelter ownership retained",
                    destination: cell, anchor: pawn.Position,
                    intent: continuation);
                return;
            }

            // Once the actor is inside the owned room, the room is the shelter.
            // Retarget the stationary job to the actor's current cell instead of
            // making them cross or leave a proper enclosed Home room for an old
            // point destination.
            if (cell != pawn.Position
                && IsInsideShelterEnvelope(pawn, pawn.Position))
            {
                shelterCells[pawn.thingIDNumber] = pawn.Position;
                cell = pawn.Position;
                CATrace.Pawn(pawn,
                    "shelter room RETAINED at current cell; no route outside the owned enclosed Home room",
                    destination: cell, anchor: pawn.Position,
                    intent: continuation);
            }

            bool urgentHunger = pawn.needs?.food != null
                && pawn.needs.food.CurCategory
                    >= HungerCategory.UrgentlyHungry;
            if (pawn.CurJob != null && pawn.CurJob.def == CA_Defs.Shelter
                && pawn.CurJob.targetA.Cell == cell)
            {
                if (pawn.Position == cell
                    || pawn.pather != null && pawn.pather.Moving)
                {
                    if (urgentHunger)
                        CATrace.Skip(pawn,
                            "shelter release for urgent hunger",
                            "active raid retains the civilian in the enclosed Home shelter; hunger may not send the pawn outside",
                            destination: cell, anchor: pawn.Position,
                            intent: continuation);
                    return;
                }
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            string priorJob = pawn.CurJobDef != null
                ? pawn.CurJobDef.defName : "none";
            Job job = JobMaker.MakeJob(CA_Defs.Shelter, cell);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.expiryInterval = 900;
            job.checkOverrideOnExpire = true;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            if (pawn.CurJob == job)
                CATrace.Pawn(pawn, "shelter job STARTED - "
                    + (urgentHunger
                        ? "active threat retains shelter through urgent hunger; "
                        : "")
                    + "replaced " + priorJob + "; " + reason,
                    destination: cell, anchor: pawn.Position,
                    intent: continuation);
            else
                CATrace.Skip(pawn, "shelter job decision",
                    "StartJob returned without shelter ownership; current job is "
                    + (pawn.CurJobDef != null
                        ? pawn.CurJobDef.defName : "none"),
                    destination: cell, anchor: pawn.Position,
                    intent: continuation);
        }

        internal bool OwnsShelter(Pawn pawn, IntVec3 cell)
        {
            IntVec3 assigned;
            return pawn != null && shelterCells.TryGetValue(
                pawn.thingIDNumber, out assigned) && assigned == cell;
        }

        internal bool HasShelter(Pawn pawn)
        {
            return pawn != null
                && shelterCells.ContainsKey(pawn.thingIDNumber);
        }

        internal bool IsInsideShelterEnvelope(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || !cell.InBounds(map) || !cell.Roofed(map))
                return false;
            IntVec3 assigned;
            if (!shelterCells.TryGetValue(pawn.thingIDNumber, out assigned)
                || !assigned.InBounds(map)) return false;
            Room room;
            return IsOwnedShelterCell(assigned, out room)
                && cell.GetRoom(map) == room;
        }

        private void ReleaseShelter(Pawn pawn, string reason, bool endJob)
        {
            if (pawn == null) return;
            int id = pawn.thingIDNumber;
            IntVec3 cell;
            int episode;
            if (!shelterCells.TryGetValue(id, out cell)) return;
            shelterEpisodes.TryGetValue(id, out episode);
            shelterCells.Remove(id);
            shelterEpisodes.Remove(id);
            CATrace.Pawn(pawn, "shelter intent ENDED - " + reason,
                destination: cell, anchor: pawn.Position,
                intent: episode > 0 ? (CAIntentContext?)new CAIntentContext(
                    episode, CAIntentOrigin.Continuation,
                    CAIntentController.Shelter, id) : null);
            if (endJob && pawn.jobs != null && pawn.CurJobDef == CA_Defs.Shelter)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        private void ClearShelters(string reason)
        {
            if (shelterCells.Count == 0) return;
            var ids = new List<int>(shelterCells.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                Pawn pawn = PawnById(ids[i]);
                if (pawn != null) ReleaseShelter(pawn, reason, endJob: true);
                else
                {
                    shelterCells.Remove(ids[i]);
                    shelterEpisodes.Remove(ids[i]);
                }
            }
        }

        private Pawn PawnById(int id)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == id) return pawns[i];
            return null;
        }
    }

    public class JobDriver_CAShelter : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            Toil shelter = ToilMaker.MakeToil("CAShelter");
            shelter.initAction = delegate { pawn.pather?.StopDead(); };
            shelter.tickAction = delegate
            {
                if (!pawn.IsHashIntervalTick(30)) return;
                RaidResponseMapComponent component = pawn.Map
                    .GetComponent<RaidResponseMapComponent>();
                if (component == null
                    || !component.OwnsShelter(pawn, job.targetA.Cell)
                    || !CACombatThreat.MapHasActiveThreat(pawn.Map))
                    EndJobWith(JobCondition.Succeeded);
                else if (!component.IsInsideShelterEnvelope(pawn,
                    pawn.Position) && (pawn.pather == null
                        || !pawn.pather.Moving))
                    EndJobWith(JobCondition.Incompletable);
            };
            shelter.defaultCompleteMode = ToilCompleteMode.Never;
            shelter.socialMode = RandomSocialMode.Off;
            yield return shelter;
        }
    }

    // Native emergency work can propose BeatFire between CA's shelter pulses.
    // Keep player-forced fire orders untouched, but do not let optional fire work
    // replace an active flee or carry a sheltered civilian across the battlefield.
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class Patch_CAShelterFirePrecedence
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Pawn_JobTracker __instance, Job newJob)
        {
            if (newJob == null || newJob.def != JobDefOf.BeatFire
                && newJob.def != JobDefOf.ExtinguishFiresNearby)
                return true;
            Pawn pawn = CACombatFlightRecorder.PawnOf(__instance);
            RaidResponseMapComponent component = pawn?.Map
                ?.GetComponent<RaidResponseMapComponent>();
            string reason;
            if (component == null
                || !component.BlocksOptionalFireResponse(pawn, newJob,
                    out reason))
                return true;

            IntVec3 fireCell = newJob.targetA.IsValid
                ? newJob.targetA.Cell : IntVec3.Invalid;
            CATrace.Skip(pawn, "optional fire response",
                reason, destination: fireCell.IsValid
                    ? (IntVec3?)fireCell : null, anchor: pawn.Position);
            return false;
        }
    }
}
