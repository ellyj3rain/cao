using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    internal struct CACombatCellAssessment
    {
        public IntVec3 cell;
        public float score;
        public float shotQuality;
        public float selfCover;
        public float friendlyLaneRisk;
        public float friendlySectorRisk;
        public float friendlySectorRouteRisk;
        public int friendlySectorRouteCells;
        public float rangeFit;
        public float travel;
        public float mutualSupport;
        public float localPressure;
        public float crowding;
        public float friendlyPower;
        public float hostilePower;
        public float hazard;
        public bool hasLineOfFire;
    }

    // One materialized friendly weapon sector. The endpoints come from an active
    // ranged stance, target-committed ranged job, or drafted native enemy target;
    // they are not inferred from a roster, remembered enemy, or desired formation.
    internal readonly struct CAFriendlyFireSectorSnapshot
    {
        public readonly int ShooterId;
        public readonly int TargetId;
        public readonly IntVec3 Source;
        public readonly IntVec3 Destination;
        public readonly float Corridor;

        public CAFriendlyFireSectorSnapshot(int shooterId, int targetId,
            IntVec3 source, IntVec3 destination, float corridor)
        {
            ShooterId = shooterId;
            TargetId = targetId;
            Source = source;
            Destination = destination;
            Corridor = corridor;
        }

        public float RiskAt(IntVec3 cell)
        {
            Vector2 start = new Vector2(Source.x, Source.z);
            Vector2 end = new Vector2(Destination.x, Destination.z);
            Vector2 point = new Vector2(cell.x, cell.z);
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.01f) return 0f;
            float along = Vector2.Dot(point - start, segment) / lengthSquared;
            if (along <= 0.02f || along >= 1f) return 0f;
            float perpendicular = Vector2.Distance(point,
                start + segment * along);
            if (perpendicular >= Corridor) return 0f;
            return Mathf.Clamp01(1f - perpendicular / Corridor);
        }
    }

    internal readonly struct CAFriendlyFireAuthorizationDiagnosticState
    {
        public readonly int Tick;
        public readonly string Phase;
        public readonly string Outcome;
        public readonly string Ownership;
        public readonly int JobId;
        public readonly string JobDef;
        public readonly int TargetId;
        public readonly int BlockerId;
        public readonly float LaneRisk;

        public CAFriendlyFireAuthorizationDiagnosticState(int tick,
            string phase, string outcome, string ownership, int jobId,
            string jobDef,
            int targetId, int blockerId, float laneRisk)
        {
            Tick = tick;
            Phase = phase;
            Outcome = outcome;
            Ownership = ownership;
            JobId = jobId;
            JobDef = jobDef;
            TargetId = targetId;
            BlockerId = blockerId;
            LaneRisk = laneRisk;
        }
    }

    internal struct CASelfPreservationAssessment
    {
        public bool recentHarm;
        public int harmEpisodeId;
        public int harmAge;
        public IntVec3 threatCell;
        public float composure;
        public float pressure;
        public float currentCover;
        public bool canEngage;
        public bool criticalCondition;
        public CACombatConditionSnapshot condition;
        public string decision;
    }

    // Fixed-size detached state for the flight recorder. The backing dictionaries
    // remain private and mutable only through the recovery controller.
    internal readonly struct CACombatRecoveryDiagnosticState
    {
        public readonly bool IsRecovering;
        public readonly int EpisodeId;
        public readonly bool HasMoveCooldown;
        public readonly int NextMoveTick;
        public readonly int MoveCooldownTicksRemaining;
        public readonly bool HasOperatorOverride;
        public readonly int OperatorOverrideHarmTick;
        public readonly int OperatorOverrideUntilTick;
        public readonly int OperatorOverrideTicksRemaining;
        public readonly float OperatorOverrideCombatPower;
        public readonly float OperatorOverrideBloodLoss;
        public readonly float OperatorOverrideBleedRate;
        public readonly int OperatorOverrideDeathTicks;

        public CACombatRecoveryDiagnosticState(bool isRecovering,
            int episodeId, bool hasMoveCooldown, int nextMoveTick,
            int moveCooldownTicksRemaining, bool hasOperatorOverride,
            int operatorOverrideHarmTick, int operatorOverrideUntilTick,
            int operatorOverrideTicksRemaining,
            float operatorOverrideCombatPower,
            float operatorOverrideBloodLoss, float operatorOverrideBleedRate,
            int operatorOverrideDeathTicks)
        {
            IsRecovering = isRecovering;
            EpisodeId = episodeId;
            HasMoveCooldown = hasMoveCooldown;
            NextMoveTick = nextMoveTick;
            MoveCooldownTicksRemaining = moveCooldownTicksRemaining;
            HasOperatorOverride = hasOperatorOverride;
            OperatorOverrideHarmTick = operatorOverrideHarmTick;
            OperatorOverrideUntilTick = operatorOverrideUntilTick;
            OperatorOverrideTicksRemaining = operatorOverrideTicksRemaining;
            OperatorOverrideCombatPower = operatorOverrideCombatPower;
            OperatorOverrideBloodLoss = operatorOverrideBloodLoss;
            OperatorOverrideBleedRate = operatorOverrideBleedRate;
            OperatorOverrideDeathTicks = operatorOverrideDeathTicks;
        }
    }

    internal readonly struct CADraftedSupportDiagnosticState
    {
        public readonly bool PendingAttempt;
        public readonly int AttemptJobId;
        public readonly IntVec3 AttemptOrigin;
        public readonly int TargetId;
        public readonly int SourceTick;
        public readonly int CooldownUntilTick;
        public readonly IntVec3 Anchor;
        public readonly IntVec3 TargetCell;

        public CADraftedSupportDiagnosticState(bool pendingAttempt,
            int attemptJobId, IntVec3 attemptOrigin, int targetId,
            int sourceTick, int cooldownUntilTick, IntVec3 anchor,
            IntVec3 targetCell)
        {
            PendingAttempt = pendingAttempt;
            AttemptJobId = attemptJobId;
            AttemptOrigin = attemptOrigin;
            TargetId = targetId;
            SourceTick = sourceTick;
            CooldownUntilTick = cooldownUntilTick;
            Anchor = anchor;
            TargetCell = targetCell;
        }
    }

    internal struct CAReportedSupportCommit
    {
        public int targetId;
        public int sourceTick;
        public int reporterId;
        public int selectedTick;
        public IntVec3 targetCell;
        public IntVec3 anchor;
        public IntVec3 origin;
        public IntVec3 destination;
        public string deliveryChannel;
        public string reporterLabel;
        public float move;
        public float approach;
        public float currentCover;
        public float destinationCover;
        public float currentSectorRisk;
        public float destinationSectorRisk;
        public bool casualtyReorganization;
        public int casualtyId;
        public int casualtySourceTick;

        public bool IsValid => targetId > 0 && sourceTick >= 0
            && targetCell.IsValid && anchor.IsValid && origin.IsValid
            && destination.IsValid;
    }

    internal sealed class CAReportedSupportEvaluation : IExposable
    {
        public int Tick = -1;
        public string Stage;
        public string Reason;
        public int CandidateCount;
        public int TargetId = -1;
        public int ReporterId = -1;
        public int SourceTick = -1;
        public IntVec3 ContactCell = IntVec3.Invalid;
        public IntVec3 Destination = IntVec3.Invalid;
        public float Move = -1f;
        public float Approach = -1f;
        public float CurrentCover = -1f;
        public float DestinationCover = -1f;
        public float CurrentSectorRisk = -1f;
        public float DestinationSectorRisk = -1f;
        public float RouteSectorRisk = -1f;
        public bool CasualtyReorganization;
        public int CasualtyId = -1;
        public int CasualtySourceTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick", -1);
            Scribe_Values.Look(ref Stage, "stage");
            Scribe_Values.Look(ref Reason, "reason");
            Scribe_Values.Look(ref CandidateCount, "candidateCount", 0);
            Scribe_Values.Look(ref TargetId, "targetId", -1);
            Scribe_Values.Look(ref ReporterId, "reporterId", -1);
            Scribe_Values.Look(ref SourceTick, "sourceTick", -1);
            Scribe_Values.Look(ref ContactCell, "contactCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref Destination, "destination",
                IntVec3.Invalid);
            Scribe_Values.Look(ref Move, "move", -1f);
            Scribe_Values.Look(ref Approach, "approach", -1f);
            Scribe_Values.Look(ref CurrentCover, "currentCover", -1f);
            Scribe_Values.Look(ref DestinationCover, "destinationCover", -1f);
            Scribe_Values.Look(ref CurrentSectorRisk,
                "currentSectorRisk", -1f);
            Scribe_Values.Look(ref DestinationSectorRisk,
                "destinationSectorRisk", -1f);
            Scribe_Values.Look(ref RouteSectorRisk, "routeSectorRisk", -1f);
            Scribe_Values.Look(ref CasualtyReorganization,
                "casualtyReorganization", false);
            Scribe_Values.Look(ref CasualtyId, "casualtyId", -1);
            Scribe_Values.Look(ref CasualtySourceTick,
                "casualtySourceTick", -1);
        }
    }

    public class CACombatRecoveryMapComponent : MapComponent
    {
        private Dictionary<int, int> episodes =
            new Dictionary<int, int>();
        private Dictionary<int, int> nextMoveTicks =
            new Dictionary<int, int>();
        // A direct player job ends the current recovery episode. The same injury
        // cannot silently recreate it as soon as that job finishes; a later hit is
        // new evidence and may open a new episode.
        private Dictionary<int, int> operatorOverrideHarmTicks =
            new Dictionary<int, int>();
        private Dictionary<int, int> operatorOverrideUntilTicks =
            new Dictionary<int, int>();
        private Dictionary<int, float> operatorOverrideCombatPower =
            new Dictionary<int, float>();
        private Dictionary<int, float> operatorOverrideBloodLoss =
            new Dictionary<int, float>();
        private Dictionary<int, float> operatorOverrideBleedRate =
            new Dictionary<int, float>();
        private Dictionary<int, int> operatorOverrideDeathTicks =
            new Dictionary<int, int>();
        // Job load ids are runtime diagnostics, not durable intent identity.
        private readonly Dictionary<int, int> reportedInterruptionJobs =
            new Dictionary<int, int>();

        // A direct job gets a real grace period, not permanent ownership of an
        // injury. Once that bounded interval has elapsed, a still-degraded drafted
        // pawn may reorganize as soon as the player-authored job itself is over.
        private const int OperatorOverrideGraceTicks = 600;

        public CACombatRecoveryMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref episodes, "CA_combatRecoveryEpisodes",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref nextMoveTicks,
                "CA_combatRecoveryNextMove", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref operatorOverrideHarmTicks,
                "CA_combatRecoveryOperatorOverrideHarmTicks",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref operatorOverrideUntilTicks,
                "CA_combatRecoveryOperatorOverrideUntilTicks",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref operatorOverrideCombatPower,
                "CA_combatRecoveryOperatorOverrideCombatPower",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref operatorOverrideBloodLoss,
                "CA_combatRecoveryOperatorOverrideBloodLoss",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref operatorOverrideBleedRate,
                "CA_combatRecoveryOperatorOverrideBleedRate",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref operatorOverrideDeathTicks,
                "CA_combatRecoveryOperatorOverrideDeathTicks",
                LookMode.Value, LookMode.Value);
            if (episodes == null) episodes = new Dictionary<int, int>();
            if (nextMoveTicks == null)
                nextMoveTicks = new Dictionary<int, int>();
            if (operatorOverrideHarmTicks == null)
                operatorOverrideHarmTicks = new Dictionary<int, int>();
            if (operatorOverrideUntilTicks == null)
                operatorOverrideUntilTicks = new Dictionary<int, int>();
            if (operatorOverrideCombatPower == null)
                operatorOverrideCombatPower = new Dictionary<int, float>();
            if (operatorOverrideBloodLoss == null)
                operatorOverrideBloodLoss = new Dictionary<int, float>();
            if (operatorOverrideBleedRate == null)
                operatorOverrideBleedRate = new Dictionary<int, float>();
            if (operatorOverrideDeathTicks == null)
                operatorOverrideDeathTicks = new Dictionary<int, int>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (int episode in episodes.Values)
                CACombatIntent.ObserveEpisode(episode);
        }

        internal bool TryGet(Pawn pawn, out int episode)
        {
            episode = -1;
            return pawn != null && episodes.TryGetValue(
                pawn.thingIDNumber, out episode);
        }

        internal bool TryGetDiagnosticState(Pawn pawn,
            out CACombatRecoveryDiagnosticState state)
        {
            state = default(CACombatRecoveryDiagnosticState);
            if (pawn == null) return false;
            int id = pawn.thingIDNumber;
            int episode;
            bool recovering = episodes.TryGetValue(id, out episode);
            int nextMove;
            bool hasMoveCooldown = nextMoveTicks.TryGetValue(id,
                out nextMove);
            int overrideHarm;
            bool hasOperatorOverride = operatorOverrideHarmTicks.TryGetValue(
                id, out overrideHarm);
            int overrideUntil = -1;
            if (hasOperatorOverride)
                operatorOverrideUntilTicks.TryGetValue(id, out overrideUntil);
            float overridePower = 0f;
            float overrideBloodLoss = 0f;
            float overrideBleed = 0f;
            int overrideDeathTicks = int.MaxValue;
            if (hasOperatorOverride)
            {
                operatorOverrideCombatPower.TryGetValue(id,
                    out overridePower);
                operatorOverrideBloodLoss.TryGetValue(id,
                    out overrideBloodLoss);
                operatorOverrideBleedRate.TryGetValue(id,
                    out overrideBleed);
                int storedDeathTicks;
                if (operatorOverrideDeathTicks.TryGetValue(id,
                        out storedDeathTicks))
                    overrideDeathTicks = storedDeathTicks;
            }
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            state = new CACombatRecoveryDiagnosticState(recovering,
                recovering ? episode : -1, hasMoveCooldown,
                hasMoveCooldown ? nextMove : -1,
                hasMoveCooldown && now >= 0
                    ? Mathf.Max(0, nextMove - now) : -1,
                hasOperatorOverride,
                hasOperatorOverride ? overrideHarm : -1,
                hasOperatorOverride ? overrideUntil : -1,
                hasOperatorOverride && now >= 0 && overrideUntil >= 0
                    ? Mathf.Max(0, overrideUntil - now) : -1,
                overridePower, overrideBloodLoss, overrideBleed,
                overrideDeathTicks);
            return recovering || hasMoveCooldown || hasOperatorOverride;
        }

        internal void Begin(Pawn pawn, int episode)
        {
            if (pawn == null || episode <= 0) return;
            episodes[pawn.thingIDNumber] = episode;
            ClearOperatorOverride(pawn.thingIDNumber);
        }

        internal void End(Pawn pawn)
        {
            if (pawn == null) return;
            ClearAllState(pawn.thingIDNumber);
        }

        internal void EndForOperator(Pawn pawn)
        {
            if (pawn == null) return;
            int id = pawn.thingIDNumber;
            episodes.Remove(id);
            nextMoveTicks.Remove(id);
            reportedInterruptionJobs.Remove(id);
            CACombatConditionSnapshot condition =
                CACombatConditionSnapshot.Capture(pawn);
            operatorOverrideHarmTicks[id] = pawn.mindState != null
                ? pawn.mindState.lastHarmTick : -1;
            operatorOverrideUntilTicks[id] = Find.TickManager != null
                ? Find.TickManager.TicksGame + OperatorOverrideGraceTicks
                : OperatorOverrideGraceTicks;
            operatorOverrideCombatPower[id] = condition.CombatPower;
            operatorOverrideBloodLoss[id] = condition.BloodLoss;
            operatorOverrideBleedRate[id] = condition.BleedRate;
            operatorOverrideDeathTicks[id] = condition.DeathInTicks;
        }

        internal bool OperatorOverrideStillOwns(Pawn pawn,
            CACombatConditionSnapshot condition)
        {
            if (pawn == null) return false;
            int harmAtOverride;
            if (!operatorOverrideHarmTicks.TryGetValue(pawn.thingIDNumber,
                    out harmAtOverride)) return false;
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : int.MaxValue;
            int until;
            if (!operatorOverrideUntilTicks.TryGetValue(pawn.thingIDNumber,
                    out until) || now >= until)
            {
                ClearOperatorOverride(pawn.thingIDNumber);
                return false;
            }
            int currentHarm = pawn.mindState != null
                ? pawn.mindState.lastHarmTick : -1;
            if (currentHarm > harmAtOverride)
            {
                ClearOperatorOverride(pawn.thingIDNumber);
                return false;
            }

            float power;
            float bloodLoss;
            float bleedRate;
            int deathTicks;
            if (!operatorOverrideCombatPower.TryGetValue(
                    pawn.thingIDNumber, out power)
                || !operatorOverrideBloodLoss.TryGetValue(
                    pawn.thingIDNumber, out bloodLoss)
                || !operatorOverrideBleedRate.TryGetValue(
                    pawn.thingIDNumber, out bleedRate)
                || !operatorOverrideDeathTicks.TryGetValue(
                    pawn.thingIDNumber, out deathTicks))
            {
                ClearOperatorOverride(pawn.thingIDNumber);
                return false;
            }
            bool deathHorizonWorsened = condition.DeathInTicks < 12000
                && (deathTicks == int.MaxValue
                    || condition.DeathInTicks < deathTicks - 1800);
            bool materiallyWorsened = condition.CombatPower < power - 0.08f
                || condition.BloodLoss > bloodLoss + 0.06f
                || condition.BleedRate > bleedRate + 0.06f
                || deathHorizonWorsened;
            if (materiallyWorsened)
            {
                ClearOperatorOverride(pawn.thingIDNumber);
                return false;
            }
            return true;
        }

        internal bool HasAnyRecovery => episodes.Count > 0;

        private void ClearOperatorOverride(int id)
        {
            operatorOverrideHarmTicks.Remove(id);
            operatorOverrideUntilTicks.Remove(id);
            operatorOverrideCombatPower.Remove(id);
            operatorOverrideBloodLoss.Remove(id);
            operatorOverrideBleedRate.Remove(id);
            operatorOverrideDeathTicks.Remove(id);
        }

        private void ClearAllState(int id)
        {
            episodes.Remove(id);
            nextMoveTicks.Remove(id);
            reportedInterruptionJobs.Remove(id);
            ClearOperatorOverride(id);
        }

        internal bool MayMove(Pawn pawn, int now)
        {
            int next;
            return pawn != null && (!nextMoveTicks.TryGetValue(
                pawn.thingIDNumber, out next) || now >= next);
        }

        internal void DeferMove(Pawn pawn, int untilTick)
        {
            if (pawn != null)
                nextMoveTicks[pawn.thingIDNumber] = untilTick;
        }

        internal bool ShouldReportInterruption(Pawn pawn, Job job)
        {
            if (pawn == null || job == null) return false;
            int previous;
            if (reportedInterruptionJobs.TryGetValue(pawn.thingIDNumber,
                    out previous) && previous == job.loadID) return false;
            reportedInterruptionJobs[pawn.thingIDNumber] = job.loadID;
            return true;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager.TicksGame % 60 != 0) return;
            int now = Find.TickManager.TicksGame;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            var live = new Dictionary<int, Pawn>();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && pawn.Spawned && !pawn.Dead)
                    live[pawn.thingIDNumber] = pawn;
            }

            var tracked = new HashSet<int>(episodes.Keys);
            tracked.UnionWith(nextMoveTicks.Keys);
            tracked.UnionWith(reportedInterruptionJobs.Keys);
            tracked.UnionWith(operatorOverrideHarmTicks.Keys);
            tracked.UnionWith(operatorOverrideUntilTicks.Keys);
            tracked.UnionWith(operatorOverrideCombatPower.Keys);
            tracked.UnionWith(operatorOverrideBloodLoss.Keys);
            tracked.UnionWith(operatorOverrideBleedRate.Keys);
            tracked.UnionWith(operatorOverrideDeathTicks.Keys);
            foreach (int id in tracked)
                if (!live.ContainsKey(id)) ClearAllState(id);

            var ids = new List<int>(operatorOverrideHarmTicks.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                Pawn pawn;
                live.TryGetValue(ids[i], out pawn);
                int until;
                if (pawn == null || pawn.Dead || pawn.Downed
                    || !operatorOverrideUntilTicks.TryGetValue(ids[i],
                        out until) || now >= until)
                    ClearOperatorOverride(ids[i]);
            }
        }
    }

    // Immediate individual judgment on the native constant-think lane. This does
    // not plan a squad or infer shared knowledge: it compares the actor's current
    // condition, pawn-private contacts, visible local actors, learned structure,
    // terrain, and non-hostiles in the prospective firing corridor.
    internal static class CAImmediateCombat
    {
        private struct HarmEpisode
        {
            public int id;
            public int tick;
            public int mapId;
            public int instigatorId;
            public IntVec3 sourceCell;
        }

        private struct PositionClock
        {
            public int nextTick;
            public int targetId;
            public IntVec3 targetCell;
            public IntVec3 actorCell;
            public IntVec3 anchorCell;
            public bool targetRetreating;
        }

        private sealed class FirePositionContext
        {
            public CACombatConditionSnapshot actorCondition;
            public readonly Dictionary<int, float> combatPower =
                new Dictionary<int, float>();
            public readonly List<Pawn> supporters = new List<Pawn>();
            public readonly List<Pawn> visibleAllies = new List<Pawn>();
            public readonly List<Pawn> visibleHostiles = new List<Pawn>();
            public readonly List<Pawn> visibleDownedAllies = new List<Pawn>();
            public readonly List<Pawn> visibleLaneBodies = new List<Pawn>();
            public readonly List<CAFriendlyFireSectorSnapshot>
                activeFriendlySectors =
                    new List<CAFriendlyFireSectorSnapshot>();
        }

        private sealed class EscapeSafetyContext
        {
            public CACombatConditionSnapshot actorCondition;
            public readonly List<Pawn> visibleHostiles = new List<Pawn>();
            public readonly HashSet<int> visibleHostileIds = new HashSet<int>();
            public readonly List<ThreatContactSnapshot> knownHostiles =
                new List<ThreatContactSnapshot>();
            public readonly List<Pawn> visibleAllies = new List<Pawn>();
            public readonly Dictionary<int, float> combatPower =
                new Dictionary<int, float>();
            public readonly List<CAFriendlyFireSectorSnapshot>
                activeFriendlySectors =
                    new List<CAFriendlyFireSectorSnapshot>();
        }

        private struct EscapeRouteCandidate
        {
            public IntVec3 cell;
            public float destinationScore;
            public int exposedCells;
            public int pathCells;
            public int waterEntries;
            public float maxHazard;
            public float maxHostilePressure;
            public float cumulativeHostilePressure;
            public float hostilePressure;
            public float friendlySectorRisk;
            public int friendlySectorCells;
            public float maxFriendlySectorRisk;
            public float mutualSupport;
            public float structuralShelter;
            public int shelteredRouteCells;
        }

        private static readonly Dictionary<int, HarmEpisode> recentHarm =
            new Dictionary<int, HarmEpisode>();
        private static readonly Dictionary<int, int> handledHarmEpisodes =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, PositionClock> positionClocks =
            new Dictionary<int, PositionClock>();
        private static readonly Dictionary<int,
            CAFriendlyFireAuthorizationDiagnosticState>
                friendlyFireAuthorizations =
                    new Dictionary<int,
                        CAFriendlyFireAuthorizationDiagnosticState>();
        private static readonly Dictionary<int, int>
            nextFriendlyFireTraceTicks = new Dictionary<int, int>();
        private static readonly Dictionary<int, int>
            nextRecoveryDefenseTraceTicks = new Dictionary<int, int>();
        private static int nextHarmEpisodeId;

        private const int RecentHarmTicks = 180;
        private const int KnownThreatFreshTicks = 7500;
        private const float FriendlySectorUnsafeRisk = 0.05f;

        internal static void ClearTransient()
        {
            recentHarm.Clear();
            handledHarmEpisodes.Clear();
            positionClocks.Clear();
            friendlyFireAuthorizations.Clear();
            nextFriendlyFireTraceTicks.Clear();
            nextRecoveryDefenseTraceTicks.Clear();
            nextHarmEpisodeId = 0;
        }

        internal static void NoteHarm(Pawn pawn, DamageInfo dinfo)
        {
            if (pawn == null || pawn.Map == null || pawn.RaceProps == null
                || !pawn.RaceProps.Humanlike
                || !dinfo.Def.ExternalViolenceFor(pawn)) return;

            CACombatIntent.NoteHarmAt(pawn);
            HiddenRegistry.NotifyHarmed(pawn);
            Thing instigator = dinfo.Instigator;
            IntVec3 source = instigator != null && instigator.MapHeld == pawn.Map
                ? instigator.PositionHeld : IntVec3.Invalid;
            recentHarm[pawn.thingIDNumber] = new HarmEpisode
            {
                id = ++nextHarmEpisodeId,
                tick = Find.TickManager.TicksGame,
                mapId = pawn.Map.uniqueID,
                instigatorId = instigator != null ? instigator.thingIDNumber : -1,
                sourceCell = source
            };
            Disposition.Invalidate(pawn);
        }

        internal static bool HarmedRecently(Pawn pawn)
        {
            HarmEpisode episode;
            return TryRecentHarm(pawn, out episode);
        }

        internal static bool TrySelfPreservationJob(Pawn pawn, Pawn visibleTarget,
            IntVec3 copiedThreatCell, bool mayFight, out Job result,
            out CASelfPreservationAssessment assessment)
        {
            result = null;
            visibleTarget = ImmediateVisibleTarget(pawn, visibleTarget);
            if (!AssessSelfPreservation(pawn, visibleTarget, copiedThreatCell, mayFight,
                out assessment)) return false;

            // A live fighting-withdrawal driver already owns a bounded survival
            // route and alternates movement with covering fire. Reprocessing the
            // same harm through this actor-local lane can cancel that route for a
            // shorter generic move—even back toward the pocket it was escaping.
            // Incoming explosives remain a separate higher-priority physical
            // deadline, and downing/native failure still terminates the driver.
            WithdrawalMapComponent withdrawal = pawn.Map
                .GetComponent<WithdrawalMapComponent>();
            if (withdrawal != null && withdrawal.InPlan(pawn)
                && pawn.CurJob != null
                && pawn.CurJob.def == CA_Defs.FightingWithdrawal)
            {
                handledHarmEpisodes[pawn.thingIDNumber] =
                    assessment.harmEpisodeId;
                CATrace.Skip(pawn, "fresh-harm survival replanning",
                    "the current bounded fighting withdrawal already owns escape continuity; the same harm episode cannot replace it with a second destination",
                    contact: assessment.threatCell,
                    destination: pawn.CurJob.targetA.IsValid
                        ? (IntVec3?)pawn.CurJob.targetA.Cell : null,
                    anchor: pawn.Position);
                return false;
            }

            // One concrete impact owns at most one emitted survival job, regardless
            // of whether the native thinker or drafted initiative pump asks first.
            // A later impact receives a new episode id and can still change the plan.
            int handledEpisode;
            if (handledHarmEpisodes.TryGetValue(pawn.thingIDNumber,
                    out handledEpisode)
                && handledEpisode == assessment.harmEpisodeId) return false;

            if (assessment.decision == "reposition" && visibleTarget != null)
            {
                CACombatCellAssessment current;
                CACombatCellAssessment better;
                if (TryFindBetterFirePosition(pawn, visibleTarget, true,
                    out current, out better))
                {
                    CAIntentContext context = CACombatIntent.Autonomous(pawn,
                        CAIntentController.SelfPreservation);
                    result = PositionJob(pawn, better.cell);
                    if (result == null) return false;
                    CACombatIntent.RecordMovement(pawn, context, pawn.Position,
                        better.cell, visibleTarget);
                    CATrace.Pawn(pawn, "breaks exposure for firing position "
                        + better.cell + " (score " + current.score.ToString("F2")
                        + " -> " + better.score.ToString("F2") + "; "
                        + assessment.condition.TraceText() + ")",
                        target: visibleTarget, contact: assessment.threatCell,
                        destination: better.cell, anchor: pawn.Position,
                        intent: context);
                    handledHarmEpisodes[pawn.thingIDNumber] =
                        assessment.harmEpisodeId;
                    return true;
                }
            }

            if (assessment.decision != "break contact") return false;
            IntVec3 retreat;
            if (TryFindSaferCell(pawn, assessment.threatCell, out retreat))
                result = PositionJob(pawn, retreat);
            if (result == null && visibleTarget != null)
            {
                result = FleeUtility.FleeJob(pawn, visibleTarget,
                    Mathf.Clamp(10 + Mathf.RoundToInt(
                        (assessment.pressure - assessment.composure) * 6f), 10, 16));
            }
            if (result == null) return false;

            CAIntentContext breakContext = CACombatIntent.Autonomous(pawn,
                CAIntentController.SelfPreservation);
            result.locomotionUrgency = LocomotionUrgency.Sprint;
            int breakContactExpiry = BreakContactExpiryTicks(pawn, result,
                assessment.condition);
            result.expiryInterval = breakContactExpiry;
            result.checkOverrideOnExpire = true;
            CATrace.Pawn(pawn, "breaks contact after harm (pressure "
                + assessment.pressure.ToString("F2") + ", composure "
                + assessment.composure.ToString("F2") + "; "
                + assessment.condition.TraceText()
                + (result.targetA.IsValid
                    ? "; " + EscapeTopologyText(pawn, result.targetA.Cell,
                        assessment.threatCell) : "")
                + "; route-aware expiry " + breakContactExpiry + " ticks)",
                target: visibleTarget, contact: assessment.threatCell,
                destination: result.targetA.IsValid
                    ? (IntVec3?)result.targetA.Cell : null,
                anchor: pawn.Position, intent: breakContext);
            if (result.targetA.IsValid)
                CACombatIntent.RecordMovement(pawn, breakContext, pawn.Position,
                    result.targetA.Cell, visibleTarget);
            if (assessment.criticalCondition)
                BeginPersistentRecoveryAfterHarm(pawn, assessment.condition);
            handledHarmEpisodes[pawn.thingIDNumber] = assessment.harmEpisodeId;
            return true;
        }

        private static int BreakContactExpiryTicks(Pawn pawn, Job job,
            CACombatConditionSnapshot condition)
        {
            if (pawn == null || job == null || !job.targetA.IsValid)
                return 240;
            int routeCells = Mathf.CeilToInt(
                pawn.Position.DistanceTo(job.targetA.Cell));
            if (!job.targetQueueA.NullOrEmpty())
                routeCells = Mathf.Max(routeCells, job.targetQueueA.Count);
            float moving = Mathf.Max(0.25f, condition.Moving);
            int travelBudget = Mathf.CeilToInt(routeCells * 24f / moving);
            return Mathf.Clamp(travelBudget + 180, 240, 1200);
        }

        private static void BeginPersistentRecoveryAfterHarm(Pawn pawn,
            CACombatConditionSnapshot condition)
        {
            CACombatRecoveryMapComponent recovery = pawn?.Map
                ?.GetComponent<CACombatRecoveryMapComponent>();
            if (recovery == null) return;
            int existing;
            if (recovery.TryGet(pawn, out existing)) return;
            int episode = CACombatIntent.NewEpisode();
            recovery.Begin(pawn, episode);
            CATrace.Pawn(pawn,
                "fresh-harm survival move opens persistent combat recovery ("
                + condition.TraceText() + ")", anchor: pawn.Position,
                intent: CACombatIntent.Autonomous(pawn,
                    CAIntentController.CombatRecovery, episode));
        }

        internal static bool AssessSelfPreservation(Pawn pawn, Pawn visibleTarget,
            IntVec3 copiedThreatCell, bool mayFight,
            out CASelfPreservationAssessment assessment)
        {
            assessment = default(CASelfPreservationAssessment);
            HarmEpisode harm;
            if (!TryRecentHarm(pawn, out harm)) return false;

            IntVec3 threat = harm.sourceCell.IsValid
                ? harm.sourceCell : copiedThreatCell;
            if (!threat.IsValid && visibleTarget != null) threat = visibleTarget.Position;
            if (!threat.IsValid || pawn == null || pawn.Map == null
                || !threat.InBounds(pawn.Map)) return false;

            Verb verb = visibleTarget != null
                ? pawn.TryGetAttackVerb(visibleTarget,
                    AllowManualCombatVerb(pawn))
                : null;
            bool ranged = verb != null && !verb.verbProps.IsMeleeAttack;
            bool canEngage = mayFight && verb != null && (ranged
                ? verb.CanHitTargetFrom(pawn.Position, visibleTarget)
                : visibleTarget != null
                    && visibleTarget.Position.InHorDistOf(pawn.Position, 4.5f));
            if (!canEngage && mayFight && verb != null
                && verb.verbProps.IsMeleeAttack
                && ValidBoundedMeleeCommit(pawn, visibleTarget))
                canEngage = true;
            int skill = CombatSkill(pawn, ranged);
            DispositionProfile disposition = Disposition.Of(pawn);
            CACombatConditionSnapshot condition =
                CACombatConditionSnapshot.Capture(pawn);
            float pain = condition.Pain;
            float healthLoss = 1f - condition.Health;
            float bleed = Mathf.Clamp01(condition.BleedRate / 0.5f);
            float capacityLoss = 1f - condition.CombatPower;
            float cover = CoverUtility.CalculateOverallBlockChance(
                pawn.Position, threat, pawn.Map);

            float composure = Mathf.Clamp01(disposition.courage * 0.50f
                + disposition.discipline * 0.20f
                + disposition.initiative * 0.10f
                + Mathf.Clamp01(skill / 20f) * 0.20f);
            float pressure = Mathf.Clamp01(0.35f
                + (1f - cover) * 0.20f
                + pain * 0.20f
                + healthLoss * 0.10f
                + bleed * 0.20f
                + capacityLoss * 0.28f
                + condition.BloodLoss * 0.12f
                + (canEngage ? 0f : 0.20f));
            bool critical = condition.RequiresCombatRecovery(ranged);
            bool breakContact = !canEngage || critical
                || pressure > composure + 0.05f;
            string decision = breakContact ? "break contact"
                : ranged && cover < 0.20f ? "reposition" : "hold and engage";

            assessment = new CASelfPreservationAssessment
            {
                recentHarm = true,
                harmEpisodeId = harm.id,
                harmAge = Find.TickManager.TicksGame - harm.tick,
                threatCell = threat,
                composure = composure,
                pressure = pressure,
                currentCover = cover,
                canEngage = canEngage,
                criticalCondition = critical,
                condition = condition,
                decision = decision
            };
            return true;
        }

        internal static bool TryPersistentRecoveryJob(Pawn pawn,
            out Job result)
        {
            result = null;
            if (pawn == null || pawn.Map == null || pawn.health == null)
                return false;
            // Enforce operator authority at the generator boundary as well as in
            // its callers. A player-forced job can arrive between the outer think
            // gate and this recovery pass; without this local check an existing
            // recovery episode can replace that newer command in the same tick.
            bool currentPlayerForced = pawn.CurJob != null
                && pawn.CurJob.playerForced;
            bool queuedPlayerForced = pawn.jobs != null
                && pawn.jobs.jobQueue != null
                && pawn.jobs.jobQueue.AnyPlayerForced;
            if (currentPlayerForced || queuedPlayerForced)
            {
                EndRecoveryForOperatorControl(pawn);
                return false;
            }
            bool ranged = HasRangedWeaponRole(pawn);
            CACombatConditionSnapshot condition =
                CACombatConditionSnapshot.Capture(pawn);
            CACombatRecoveryMapComponent recovery = pawn.Map
                .GetComponent<CACombatRecoveryMapComponent>();
            if (recovery == null) return false;
            int episode;
            bool recovering = recovery.TryGet(pawn, out episode);
            bool activeCombat = CACombatThreat.PerceivesActiveThreat(pawn);
            bool requiresRecovery = condition.RequiresCombatRecovery(ranged);
            Job casualtyResponse;
            if (!activeCombat && requiresRecovery
                && TryVisiblePeerCasualtyResponse(pawn, condition,
                    out casualtyResponse))
            {
                if (recovering)
                {
                    recovery.End(pawn);
                    CATrace.Pawn(pawn,
                        "combat recovery YIELDS to an executable current visible peer-casualty response; the actor remains sufficiently viable for that bounded action ("
                        + condition.TraceText() + ")", anchor: pawn.Position,
                        intent: new CAIntentContext(episode,
                            CAIntentOrigin.Continuation,
                            CAIntentController.CombatRecovery,
                            pawn.thingIDNumber));
                }
                result = casualtyResponse;
                return true;
            }
            if (recovering && !activeCombat && !requiresRecovery)
            {
                recovery.End(pawn);
                CATrace.Pawn(pawn,
                    "combat recovery ENDED - no actor-local active combat problem remains; native care and ordinary work resume",
                    anchor: pawn.Position,
                    intent: new CAIntentContext(episode,
                        CAIntentOrigin.Continuation,
                        CAIntentController.CombatRecovery,
                        pawn.thingIDNumber));
                return false;
            }
            if (recovering && !RecoveryStillRequired(pawn, recovery, episode,
                    ranged, condition)) return false;
            if (!recovering)
            {
                // Drafted pawns do not regain ordinary medical work merely because
                // their last actor-local contact went stale. A materially degraded
                // fighter therefore remains eligible to reorganize or self-tend
                // between contacts; current/queued player jobs are excluded by the
                // caller before this lane is reached.
                if (!requiresRecovery
                    || !activeCombat && (!pawn.Drafted
                        || !CanTakeEmergencySelfTendStep(pawn)))
                    return false;
                if (recovery.OperatorOverrideStillOwns(pawn, condition))
                {
                    CATrace.Skip(pawn, "combat recovery",
                        "explicit operator interruption still owns this injury episode; a fresh hit may reopen recovery",
                        anchor: pawn.Position);
                    return false;
                }
                episode = CACombatIntent.NewEpisode();
                recovery.Begin(pawn, episode);
                CATrace.Pawn(pawn, "combat recovery ENTERED with hysteresis ("
                    + (activeCombat ? "actor-local threat; "
                        : "degraded drafted fighter between contacts; ")
                    + condition.TraceText() + ")", anchor: pawn.Position,
                    intent: CACombatIntent.Autonomous(pawn,
                        CAIntentController.CombatRecovery, episode));
            }

            Job current = pawn.CurJob;
            // Every CA_CombatMove is finite and expiring. The job def is a durable
            // ownership token even when the transient movement-context dictionary
            // has been cleared by save/load, so recovery must let the current route
            // finish (or fail naturally) before reasserting.
            bool boundedCombatMoveOwns = current != null
                && current.def == CA_Defs.CombatMove;
            bool quietEmergencySelfTendOwns = !activeCombat
                && current != null
                && current.def == CA_Defs.EmergencySelfTend;
            if (boundedCombatMoveOwns || quietEmergencySelfTendOwns)
            {
                // Finish the owned recovery move, immediate break-contact move, or
                // finite self-tend instead of manufacturing a replacement. Renewed
                // active danger may still interrupt treatment on the next pulse.
                result = current;
                return true;
            }

            // Once the actor-local exchange has gone quiet, a materially degraded
            // drafted fighter should use the finite emergency self-tend action
            // instead of holding an inert recovery stance indefinitely. Recovery
            // retains the episode while the native tend calculation runs.
            if (!activeCombat)
            {
                bool unresolvedBattle = CACombatThreat.MapHasActiveThreat(
                    pawn.Map);
                bool urgentDuringBattle = condition.BleedRate > 0.001f
                    || condition.DeathInTicks < int.MaxValue;
                Job selfTend = !unresolvedBattle || urgentDuringBattle
                    ? CAMissionTriage.TryEmergencySelfTendJob(pawn) : null;
                if (selfTend != null)
                {
                    result = selfTend;
                    CATrace.Pawn(pawn,
                        "combat recovery ADOPTS bounded emergency self-tend; "
                        + condition.TraceText(), anchor: pawn.Position,
                        intent: new CAIntentContext(episode,
                            CAIntentOrigin.Continuation,
                            CAIntentController.CombatRecovery,
                            pawn.thingIDNumber));
                    return true;
                }
                if (unresolvedBattle && !urgentDuringBattle
                    && pawn.health.HasHediffsNeedingTend())
                    CATrace.Skip(pawn,
                        "nonurgent combat self-treatment",
                        "the map battle is unresolved and the actor has no current bleeding or bleed-out horizon; recovery posture remains available without spending the exchange tending stable wounds",
                        anchor: pawn.Position,
                        intent: new CAIntentContext(episode,
                            CAIntentOrigin.Continuation,
                            CAIntentController.CombatRecovery,
                            pawn.thingIDNumber));
            }

            // A finite recovery move owns its exact route. The stationary recovery
            // wait does not: every pump must be able to replace it when a newly
            // reachable safer pocket appears or the local exchange worsens.
            if (RecoveryMoveOwns(current, episode))
            {
                result = current;
                return true;
            }

            IntVec3 threat = IntVec3.Invalid;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
            ThreatContactSnapshot contact;
            if (knowledge != null
                && knowledge.TryGetFreshestContact(pawn, out contact))
                threat = contact.Cell;
            int now = Find.TickManager.TicksGame;
            if (CACombatThreat.PerceivesActiveThreat(pawn) && threat.IsValid
                && recovery.MayMove(pawn, now))
            {
                IntVec3 safer;
                if (TryFindSaferCell(pawn, threat, out safer))
                {
                    result = PositionJob(pawn, safer);
                    if (result == null) return false;
                    result.count = episode;
                    recovery.DeferMove(pawn, now + 180);
                    CAIntentContext context = new CAIntentContext(episode,
                        CAIntentOrigin.Continuation,
                        CAIntentController.CombatRecovery,
                        pawn.thingIDNumber);
                    CACombatIntent.RecordMovement(pawn, context,
                        pawn.Position, safer);
                    CATrace.Pawn(pawn,
                        "health-adjusted reorganization MOVES away from contact; "
                        + condition.TraceText() + "; "
                        + EscapeTopologyText(pawn, safer, threat),
                        contact: threat,
                        destination: safer, anchor: pawn.Position,
                        intent: context);
                    return true;
                }
            }

            if (current != null && current.def != CA_Defs.CombatRecovery
                && recovery.ShouldReportInterruption(pawn, current))
                CATrace.Pawn(pawn,
                    "combat recovery REASSERTS after non-owned job "
                    + current.def.defName + "#" + current.loadID
                    + " (player-forced " + (current.playerForced ? "yes" : "no")
                    + ", giver " + (current.jobGiver?.GetType().Name ?? "none")
                    + ")", anchor: pawn.Position,
                    intent: new CAIntentContext(episode,
                        CAIntentOrigin.Continuation,
                        CAIntentController.CombatRecovery,
                        pawn.thingIDNumber));

            result = JobMaker.MakeJob(CA_Defs.CombatRecovery);
            result.count = episode;
            CATrace.Pawn(pawn,
                "health-adjusted recovery OWNS this position until viability or explicit interruption; "
                + condition.TraceText()
                + (threat.IsValid ? "; current hold baseline; "
                    + EscapeTopologyText(pawn, pawn.Position, threat) : ""),
                contact: threat.IsValid ? (IntVec3?)threat : null,
                anchor: pawn.Position,
                intent: new CAIntentContext(episode,
                    CAIntentOrigin.Continuation,
                    CAIntentController.CombatRecovery,
                    pawn.thingIDNumber));
            return true;
        }

        internal static bool EndRecoveryForOperatorControl(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.jobs == null)
                return false;
            Job current = pawn.CurJob;
            bool currentDirect = current != null && current.playerForced;
            bool queuedDirect = pawn.jobs.jobQueue != null
                && pawn.jobs.jobQueue.AnyPlayerForced;
            if (!currentDirect && !queuedDirect) return false;
            CACombatRecoveryMapComponent recovery = pawn.Map
                .GetComponent<CACombatRecoveryMapComponent>();
            int episode;
            if (recovery == null || !recovery.TryGet(pawn, out episode))
                return false;

            CAIntentContext tacticalContext;
            bool hasTacticalContext = CATactical.TryGetContext(pawn,
                out tacticalContext);
            Lord currentLord = pawn.GetLord();
            bool untaggedPlayerInterruption = current != null
                && current.playerForced
                && (currentLord == null || current.lord != currentLord);
            if (pawn.jobs.jobQueue != null)
                foreach (QueuedJob queued in pawn.jobs.jobQueue)
                    if (queued?.job != null && queued.job.playerForced)
                    {
                        bool taggedCaOrder = currentLord != null
                            && (currentLord.LordJob is LordJob_CAStackBreach
                                || currentLord.LordJob is LordJob_CATactical)
                            && queued.job.lord == currentLord;
                        if (!taggedCaOrder)
                            untaggedPlayerInterruption = true;
                    }
            CAIntentContext? interruptionIntent = untaggedPlayerInterruption
                ? (CAIntentContext?)new CAIntentContext(episode,
                    CAIntentOrigin.OperatorDirect,
                    CAIntentController.CombatRecovery,
                    pawn.thingIDNumber)
                : hasTacticalContext
                    ? (CAIntentContext?)tacticalContext : null;
            recovery.EndForOperator(pawn);
            CATrace.Pawn(pawn,
                "combat recovery ENDED - explicit player-forced interruption (current job "
                + (current?.def?.defName ?? "none") + "#"
                + (current != null ? current.loadID.ToString() : "none")
                + ", queued direct " + (queuedDirect ? "yes" : "no") + ")",
                anchor: pawn.Position,
                intent: interruptionIntent);
            if (current != null && current.def == CA_Defs.CombatRecovery
                && queuedDirect)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced,
                    startNewJob: true);
            return true;
        }

        internal static void ReleaseStandingCombatForSurvival(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return;
            if (HiddenRegistry.IsHiddenOrOrdered(pawn))
                HiddenRegistry.CancelForReplacement(pawn,
                    "immediate survival response superseded concealment");
            if (CATactical.IsHold(pawn))
            {
                HoldMapComponent hold = pawn.Map
                    .GetComponent<HoldMapComponent>();
                if (hold != null) hold.Release(pawn, startNewJob: false);
                else CATactical.Release(pawn, startNewJob: false);
            }
            else if (CATactical.IsAutomaticDefense(pawn))
                CATactical.ReleaseAutomaticDefense(pawn,
                    startNewJob: false);

            Lord lord = pawn.GetLord();
            if (lord != null && lord.LordJob is LordJob_CAStackBreach)
            {
                lord.Notify_PawnLost(pawn,
                    PawnLostCondition.ForcedByPlayerAction);
                if (pawn.mindState != null) pawn.mindState.duty = null;
                if (pawn.CurJob != null && pawn.CurJob.lord == lord)
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced,
                        startNewJob: false);
            }
        }

        // An incoming explosive temporarily owns movement, not the player's
        // standing Hold. Retaining the Hold's Lord row lets its local duty resume
        // after the finite evasion route clears. Other standing states still break:
        // leaving concealment invalidates an ambush, while automatic defense should
        // be replanned from the post-evasion geometry.
        internal static void PrepareStandingCombatForImmediateEvasion(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return;
            if (CATactical.IsHold(pawn))
            {
                CAIntentContext context;
                CATrace.Pawn(pawn,
                    "explicit hold RETAINED across immediate explosive evasion; the finite hazard route temporarily owns movement and the authored local envelope resumes after clearance",
                    anchor: pawn.Position,
                    intent: CATactical.TryGetContext(pawn, out context)
                        ? (CAIntentContext?)context : null);
                return;
            }
            ReleaseStandingCombatForSurvival(pawn);
        }

        internal static bool IsRecovering(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return false;
            CACombatRecoveryMapComponent recovery = pawn.Map
                .GetComponent<CACombatRecoveryMapComponent>();
            int episode;
            return recovery != null && recovery.TryGet(pawn, out episode);
        }

        internal static bool RecoveryStillRequired(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.health == null)
                return false;
            CACombatRecoveryMapComponent recovery = pawn.Map
                .GetComponent<CACombatRecoveryMapComponent>();
            int episode;
            if (recovery == null || !recovery.TryGet(pawn, out episode))
                return false;
            bool ranged = HasRangedWeaponRole(pawn);
            return RecoveryStillRequired(pawn, recovery, episode, ranged,
                CACombatConditionSnapshot.Capture(pawn));
        }

        internal static bool RequiresCombatRecoveryNow(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.health == null
                || pawn.Dead || pawn.Downed) return false;
            bool ranged = HasRangedWeaponRole(pawn);
            return CACombatConditionSnapshot.Capture(pawn)
                .RequiresCombatRecovery(ranged);
        }

        internal static bool HasRangedWeaponRole(Pawn pawn)
        {
            ThingWithComps primary = pawn?.equipment?.Primary;
            if (primary != null && primary.def.IsRangedWeapon) return true;
            ThingWithComps offhand = OffhandComponent.GetOffhand(pawn);
            return offhand != null && offhand.def.IsRangedWeapon;
        }

        internal static void TryRecoveryHoldDefense(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Dead || pawn.Downed
                || !pawn.Spawned || !pawn.Awake() || pawn.InMentalState
                || pawn.WorkTagIsDisabled(WorkTags.Violent)
                || pawn.stances == null || pawn.stances.FullBodyBusy
                || pawn.IsCarryingPawn() || pawn.CurJob == null
                || pawn.CurJob.def != CA_Defs.CombatRecovery) return;
            AwarenessSettings settings = AwarenessMod.Settings;
            bool mayDefend = pawn.Drafted
                ? pawn.drafter != null && pawn.drafter.FireAtWill
                : pawn.IsColonistPlayerControlled
                    && JobGiver_CACombatReaction.PlayerMaySelfReact(pawn,
                        settings);
            if (!mayDefend) return;

            CACombatConditionSnapshot condition =
                CACombatConditionSnapshot.Capture(pawn);
            if (condition.CombatPower < 0.36f
                || condition.Consciousness < 0.50f
                || condition.Manipulation < 0.45f
                || condition.Breathing < 0.50f
                || condition.BloodPumping < 0.50f
                || condition.DeathInTicks < 1800) return;
            Verb verb = pawn.CurrentEffectiveVerb;
            if (verb == null || verb.verbProps.IsMeleeAttack) return;
            TargetScanFlags flags = TargetScanFlags.NeedLOSToAll
                | TargetScanFlags.NeedThreat
                | TargetScanFlags.NeedAutoTargetable;
            if (verb.IsIncendiary_Ranged())
                flags |= TargetScanFlags.NeedNonBurning;
            Thing target = (Thing)AttackTargetFinder
                .BestShootTargetFromCurrentPosition(pawn, flags);
            if (target == null) return;

            CACombatRecoveryMapComponent recovery = pawn.Map
                .GetComponent<CACombatRecoveryMapComponent>();
            int episode;
            CAIntentContext context = recovery != null
                    && recovery.TryGet(pawn, out episode)
                ? new CAIntentContext(episode, CAIntentOrigin.Continuation,
                    CAIntentController.CombatRecovery, pawn.thingIDNumber)
                : CACombatIntent.Autonomous(pawn,
                    CAIntentController.CombatRecovery);
            string unsafeReason;
            if (!RecoveryPocketCanDefend(pawn, target, condition,
                    out unsafeReason))
            {
                int now = Find.TickManager.TicksGame;
                int next;
                if (!nextRecoveryDefenseTraceTicks.TryGetValue(
                        pawn.thingIDNumber, out next) || now >= next)
                {
                    nextRecoveryDefenseTraceTicks[pawn.thingIDNumber] =
                        now + 180;
                    CATrace.Skip(pawn, "health-adjusted recovery defense",
                        unsafeReason, target: target,
                        contact: target.Position, anchor: pawn.Position,
                        intent: context);
                }
                return;
            }
            if (!pawn.TryStartAttack(target)) return;
            CATrace.Pawn(pawn,
                "health-adjusted recovery hold DEFENDS the local pocket",
                target: target, contact: target.Position,
                anchor: pawn.Position, intent: context);
        }

        private static bool RecoveryPocketCanDefend(Pawn pawn, Thing target,
            CACombatConditionSnapshot condition, out string reason)
        {
            reason = null;
            if (pawn == null || pawn.Map == null || target == null
                || !target.Position.IsValid)
            {
                reason = "the current recovery pocket cannot be assessed";
                return false;
            }

            IntVec3 threat = target.Position;
            EscapeSafetyContext safety = BuildEscapeSafetyContext(pawn);
            float cover = CoverUtility.CalculateOverallBlockChance(
                pawn.Position, threat, pawn.Map);
            float pressure = EscapeThreatPressure(pawn, pawn.Position,
                threat, safety);
            float sectorRisk = FriendlySectorRisk(pawn.Position,
                safety.activeFriendlySectors);
            float support = RecoveryMutualSupport(pawn, pawn.Position,
                safety);
            float shelter = StructuralRecoveryShelter(pawn, pawn.Position,
                threat, safety);
            int exits = EscapeExitCount(pawn, pawn.Position, threat, safety);

            bool crossesFriendlySector = sectorRisk
                >= FriendlySectorUnsafeRisk;
            bool unsupportedExposure = cover < 0.15f && shelter < 0.30f
                && support < 0.15f && pressure >= 0.35f;
            bool degradedUnderPressure = condition.CombatPower < 0.70f
                && pressure >= 0.50f
                && (cover < 0.25f || support < 0.20f);
            bool contained = exits < 2 && pressure >= 0.45f
                && shelter < 0.40f;
            if (!crossesFriendlySector && !unsupportedExposure
                && !degradedUnderPressure && !contained) return true;

            reason = "the current cell fails the recovery-hold safety gate"
                + " (cover " + cover.ToString("F2")
                + ", hostile pressure " + pressure.ToString("F2")
                + ", mutual support " + support.ToString("F2")
                + ", structural shelter " + shelter.ToString("F2")
                + ", escape exits " + exits
                + ", friendly-sector risk " + sectorRisk.ToString("F2")
                + "); stationary fire cannot substitute for safer-position arbitration";
            return false;
        }

        private static bool RecoveryStillRequired(Pawn pawn,
            CACombatRecoveryMapComponent recovery, int episode, bool ranged,
            CACombatConditionSnapshot condition)
        {
            if (!condition.RecoveredCombatViability(ranged))
            {
                if (CACombatThreat.PerceivesActiveThreat(pawn)
                    || CanTakeEmergencySelfTendStep(pawn)) return true;
                recovery.End(pawn);
                CATrace.Pawn(pawn,
                    "combat recovery ENDED - no further bounded recovery action remains at the treated/degraded plateau; native care and rest resume ("
                    + condition.TraceText() + ")", anchor: pawn.Position,
                    intent: new CAIntentContext(episode,
                        CAIntentOrigin.Continuation,
                        CAIntentController.CombatRecovery,
                        pawn.thingIDNumber));
                return false;
            }
            recovery.End(pawn);
            CATrace.Pawn(pawn, "combat recovery ENDED - viability restored ("
                + condition.TraceText() + ")", anchor: pawn.Position,
                intent: new CAIntentContext(episode,
                    CAIntentOrigin.Continuation,
                    CAIntentController.CombatRecovery,
                    pawn.thingIDNumber));
            return false;
        }

        private static bool TryVisiblePeerCasualtyResponse(Pawn pawn,
            CACombatConditionSnapshot condition, out Job response)
        {
            response = null;
            if (!CanPerformBoundedWelfareResponse(pawn, condition)) return false;

            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(
                pawn.Map);
            if (knowledge == null) return false;
            response = JobGiver_CAWelfareResponse
                .TryGiveVisiblePeerResponseDuringRecovery(pawn);
            return response != null;
        }

        internal static bool CanPerformBoundedWelfareResponse(Pawn pawn)
        {
            return pawn != null && pawn.health != null
                && CanPerformBoundedWelfareResponse(pawn,
                    CACombatConditionSnapshot.Capture(pawn));
        }

        private static bool CanPerformBoundedWelfareResponse(Pawn pawn,
            CACombatConditionSnapshot condition)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (pawn == null || pawn.Map == null || settings == null
                || !settings.fieldMedicine && !settings.lifeSafety
                || CACombatThreat.PerceivesActiveThreat(pawn)
                || AutonomyComponent.LevelOf(pawn) < 2
                || pawn.WorkTagIsDisabled(WorkTags.Caring)
                || pawn.health?.capacities == null
                || !pawn.health.capacities.CapableOf(
                    PawnCapacityDefOf.Manipulation))
                return false;

            // A casualty does not make a critically failing rescuer expendable.
            // This is the same health-adjusted vocabulary used by recovery, with a
            // conservative floor and a meaningful self-treatment horizon.
            return condition.CombatPower >= 0.45f
                && condition.Consciousness >= 0.55f
                && condition.Moving >= 0.40f
                && condition.Manipulation >= 0.50f
                && condition.Breathing >= 0.60f
                && condition.BloodPumping >= 0.60f
                && condition.DeathInTicks >= 12000;
        }

        private static bool CanTakeEmergencySelfTendStep(Pawn pawn)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            return settings != null && settings.fieldMedicine
                && pawn != null && pawn.Map != null && pawn.health != null
                && !pawn.Dead && !pawn.Downed && pawn.Awake()
                && !pawn.InMentalState
                && AutonomyComponent.LevelOf(pawn) >= 2
                && !pawn.WorkTagIsDisabled(WorkTags.Caring)
                && pawn.health.capacities != null
                && pawn.health.capacities.CapableOf(
                    PawnCapacityDefOf.Manipulation)
                && HealthAIUtility.ShouldBeTendedNowByPlayer(pawn)
                && pawn.health.HasHediffsNeedingTend()
                && pawn.Map.reservationManager.CanReserve(pawn, pawn);
        }

        private static bool RecoveryMoveOwns(Job job, int episode)
        {
            return job != null && job.count == episode
                && job.def == CA_Defs.CombatMove;
        }

        private static bool ValidBoundedMeleeCommit(Pawn pawn, Pawn visibleTarget)
        {
            Job job = pawn != null ? pawn.CurJob : null;
            if (job == null || job.def != CA_Defs.BoundedMeleeDefense
                || visibleTarget == null || !job.targetA.HasThing
                || job.targetA.Thing != visibleTarget
                || !visibleTarget.Spawned) return false;
            IntVec3 anchor;
            float radius;
            if (job.targetC.IsValid && job.count > 0)
            {
                anchor = job.targetC.Cell;
                radius = job.count;
            }
            else
            {
                PawnDuty duty = pawn.mindState != null ? pawn.mindState.duty : null;
                if (duty == null || !duty.focus.IsValid) return false;
                anchor = duty.focus.Cell;
                radius = Mathf.Max(1f, duty.radius);
            }
            return pawn.Position.InHorDistOf(anchor, radius + 1.5f)
                && visibleTarget.Position.InHorDistOf(anchor, radius);
        }

        internal static bool TryFirePositionJob(Pawn pawn, Pawn target,
            out Job result, out CACombatCellAssessment current,
            out CACombatCellAssessment better)
        {
            result = null;
            current = InvalidCellAssessment();
            better = InvalidCellAssessment();
            if (pawn == null || target == null || pawn.Map == null
                || IsRecovering(pawn)) return false;

            int now = Find.TickManager.TicksGame;
            PositionClock clock;
            // One actor-wide clock survives target changes. A new visible enemy is not
            // permission to zig-zag, but a different matchup or a target that has moved
            // materially does require a fresh evaluation. Recent unsafe-cell memory
            // below prevents that evaluation from undoing a survival move.
            bool hadClock = positionClocks.TryGetValue(pawn.thingIDNumber,
                out clock);
            bool matchupChanged = !hadClock
                || clock.targetId != target.thingIDNumber
                || !target.Position.InHorDistOf(clock.targetCell, 4.9f);
            if (hadClock
                && now < clock.nextTick
                && clock.targetId == target.thingIDNumber
                && target.Position.InHorDistOf(clock.targetCell, 4.9f))
                return false;

            CARangedCoordinationCompetence competence =
                CACompetence.RangedCoordination(pawn);
            DispositionProfile disposition = Disposition.Of(pawn);
            float tactical = Mathf.Clamp01(
                competence.TacticalSynthesis * 0.75f
                + disposition.initiative * 0.15f
                + disposition.discipline * 0.10f);
            bool sameMatchup = hadClock
                && clock.targetId == target.thingIDNumber;
            IntVec3 anchor = sameMatchup && clock.anchorCell.IsValid
                ? clock.anchorCell : pawn.Position;
            bool targetRetreating = sameMatchup && clock.actorCell.IsValid
                && clock.targetCell.IsValid
                && clock.actorCell.DistanceTo(target.Position)
                    >= clock.actorCell.DistanceTo(clock.targetCell) + 1.5f;
            positionClocks[pawn.thingIDNumber] = new PositionClock
            {
                nextTick = now + Mathf.RoundToInt(Mathf.Lerp(150f, 45f, tactical)),
                targetId = target.thingIDNumber,
                targetCell = target.Position,
                actorCell = pawn.Position,
                anchorCell = anchor,
                targetRetreating = targetRetreating
            };

            if (!TryFindBetterFirePosition(pawn, target, false,
                out current, out better))
            {
                if (matchupChanged && current.cell.IsValid)
                {
                    CAIntentContext holdContext = CACombatIntent.Continuation(pawn,
                        CAIntentController.FirePosition);
                    CATrace.Pawn(pawn,
                        (current.hasLineOfFire
                            ? "holds current firing position after matchup revalidation"
                            : "cannot recover a firing solution after matchup revalidation")
                        + " (target distance "
                        + pawn.Position.DistanceTo(target.Position).ToString("F1")
                        + ", cover " + current.selfCover.ToString("F2")
                        + ", lane risk "
                        + current.friendlyLaneRisk.ToString("F2")
                        + ", friendly-sector risk "
                        + current.friendlySectorRisk.ToString("F2")
                        + ", support " + current.mutualSupport.ToString("F2")
                        + ", pressure " + current.localPressure.ToString("F2")
                        + ", crowding " + current.crowding.ToString("F2")
                        + ", force " + current.friendlyPower.ToString("F2")
                        + "/" + current.hostilePower.ToString("F2")
                        + ", hazard " + current.hazard.ToString("F2") + "; "
                        + competence.TraceText() + "; "
                        + CACombatConditionSnapshot.Capture(pawn).TraceText() + ")",
                        target: target, contact: target.Position,
                        destination: pawn.Position, anchor: pawn.Position,
                        intent: holdContext);
                }
                return false;
            }
            result = PositionJob(pawn, better.cell);
            if (result == null) return false;
            StampTacticalOwner(pawn, result);
            CAIntentContext context = CACombatIntent.Continuation(pawn,
                CAIntentController.FirePosition);
            CACombatIntent.RecordMovement(pawn, context, pawn.Position,
                better.cell, target);
            CATrace.Pawn(pawn, "improves firing position to " + better.cell
                + " (score " + current.score.ToString("F2") + " -> "
                + better.score.ToString("F2") + ", lane risk "
                + current.friendlyLaneRisk.ToString("F2") + " -> "
                + better.friendlyLaneRisk.ToString("F2")
                + ", friendly-sector risk "
                + current.friendlySectorRisk.ToString("F2") + " -> "
                + better.friendlySectorRisk.ToString("F2")
                + " (route cells " + better.friendlySectorRouteCells
                + ", route max "
                + better.friendlySectorRouteRisk.ToString("F2") + "), cover "
                + current.selfCover.ToString("F2") + " -> "
                + better.selfCover.ToString("F2") + ", support "
                + current.mutualSupport.ToString("F2") + " -> "
                + better.mutualSupport.ToString("F2") + ", pressure "
                + current.localPressure.ToString("F2") + " -> "
                + better.localPressure.ToString("F2") + ", hazard "
                + current.hazard.ToString("F2") + " -> "
                + better.hazard.ToString("F2") + ", crowding "
                + current.crowding.ToString("F2") + " -> "
                + better.crowding.ToString("F2") + ", force "
                + current.friendlyPower.ToString("F2") + "/"
                + current.hostilePower.ToString("F2") + " -> "
                + better.friendlyPower.ToString("F2") + "/"
                + better.hostilePower.ToString("F2") + "; "
                + competence.TraceText() + "; "
                + CACombatConditionSnapshot.Capture(pawn).TraceText() + ")",
                target: target, contact: target.Position,
                destination: better.cell, anchor: pawn.Position,
                intent: context);
            return true;
        }

        // A close remembered contact behind one obstruction is not a shooting
        // target, but neither is it permission to wander into welfare work. Use the
        // same cover, force-balance, crossfire, hazard, and learned-map scorer to
        // make one finite move that restores a firing line to the copied cell. The
        // copied cell remains the only geometry input; this does not track the live
        // hostile or authorize fire without reacquisition.
        internal static bool TryCloseKnownContactPositionJob(Pawn pawn,
            ThreatContactSnapshot contact, out Job result)
        {
            result = null;
            if (pawn == null || pawn.Map == null || IsRecovering(pawn)
                || contact.State != ThreatContactState.Active
                || !contact.Cell.IsValid || !contact.Cell.InBounds(pawn.Map)
                || contact.Evidence.Confidence < 0.55f)
                return false;
            Verb contactVerb = RangedVerbForCopiedCell(pawn, contact.Cell);
            if (contactVerb == null) return false;
            float contactDistance = pawn.Position.DistanceTo(contact.Cell);
            float contactLimit = Mathf.Min(18f,
                Mathf.Max(1f, contactVerb.EffectiveRange));
            if (contactDistance > contactLimit) return false;
            int now = Find.TickManager.TicksGame;
            int age = now - contact.SourceTick;
            if (age < 0 || age > 1800) return false;

            PositionClock clock;
            if (positionClocks.TryGetValue(pawn.thingIDNumber, out clock)
                && clock.targetId == contact.HostileId
                && now < clock.nextTick
                && contact.Cell.InHorDistOf(clock.targetCell, 1.5f))
                return false;

            CARangedCoordinationCompetence competence =
                CACompetence.RangedCoordination(pawn);
            CACombatCellAssessment current;
            CACombatCellAssessment better;
            bool found = TryFindBetterFirePositionAtCopiedContact(pawn,
                    contact.HostileId, contact.Cell, true,
                    out current, out better);
            if (!found || current.hasLineOfFire || !better.hasLineOfFire)
            {
                positionClocks[pawn.thingIDNumber] = new PositionClock
                {
                    nextTick = now + competence.DecisionDelayTicks,
                    targetId = contact.HostileId,
                    targetCell = contact.Cell,
                    actorCell = pawn.Position,
                    anchorCell = pawn.Position,
                    targetRetreating = false
                };
                CATrace.Skip(pawn, "close copied-contact reacquisition",
                    (current.hasLineOfFire
                        ? "current cell already has a firing solution"
                        : !found
                            ? "no safe geometry-scored firing-line move exists"
                            : "candidate did not restore a firing solution")
                    + " (contact age " + age + " ticks, confidence "
                    + contact.Evidence.Confidence.ToString("F2")
                    + ", distance " + contactDistance.ToString("F1")
                    + "/" + contactLimit.ToString("F1") + ", "
                    + competence.TraceText() + ")",
                    contact: contact.Cell, anchor: pawn.Position);
                return false;
            }
            result = PositionJob(pawn, better.cell);
            if (result == null) return false;
            StampTacticalOwner(pawn, result);

            positionClocks[pawn.thingIDNumber] = new PositionClock
            {
                nextTick = now + competence.DecisionDelayTicks,
                targetId = contact.HostileId,
                targetCell = contact.Cell,
                actorCell = pawn.Position,
                anchorCell = pawn.Position,
                targetRetreating = false
            };
            CAIntentContext context = CACombatIntent.Continuation(pawn,
                CAIntentController.FirePosition);
            Pawn target = SpawnedPawnById(pawn.Map, contact.HostileId);
            CACombatIntent.RecordMovement(pawn, context, pawn.Position,
                better.cell, target);
            CATrace.Pawn(pawn,
                "reacquires close copied contact with bounded firing-line move "
                + better.cell + " (contact age " + age + " ticks, confidence "
                + contact.Evidence.Confidence.ToString("F2") + ", distance "
                + contactDistance.ToString("F1") + "/"
                + contactLimit.ToString("F1")
                + ", " + competence.TraceText() + ", cover "
                + current.selfCover.ToString("F2") + " -> "
                + better.selfCover.ToString("F2") + ", pressure "
                + current.localPressure.ToString("F2") + " -> "
                + better.localPressure.ToString("F2") + ")",
                target: target, contact: contact.Cell,
                destination: better.cell, anchor: pawn.Position,
                intent: context);
            return true;
        }

        // A drafted pawn in inert watch may make one bounded support move from a
        // very fresh relayed contact, but only while the immediate reporter still
        // sees that same hostile at the reported cell. This closes the drafted /
        // undrafted communication seam without converting remembered contact into
        // pursuit or live map tracking.
        internal static bool TryReportedSupportPositionJob(Pawn pawn,
            AwarenessSettings settings, out Job result,
            out CAReportedSupportCommit commit)
        {
            result = null;
            commit = default(CAReportedSupportCommit);
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            var evaluation = new CAReportedSupportEvaluation { Tick = now };
            if (pawn == null || settings == null || !settings.knowledgeContacts
                || pawn.Map == null || !pawn.Drafted || IsRecovering(pawn)
                || pawn.WorkTagIsDisabled(WorkTags.Violent))
                return RejectReportedSupport(pawn, evaluation,
                    "actor-or-feature-ineligible");
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
            if (knowledge == null)
                return RejectReportedSupport(pawn, evaluation,
                    "knowledge-component-unavailable");
            List<ThreatContactSnapshot> contacts = knowledge.FreshContacts(pawn);
            evaluation.CandidateCount = contacts.Count;
            ThreatContactSnapshot selected = default(ThreatContactSnapshot);
            Pawn selectedReporter = null;
            WelfareFactSnapshot casualtyFact = default(WelfareFactSnapshot);
            bool casualtyReorganization = false;
            bool found = false;
            for (int i = 0; i < contacts.Count; i++)
            {
                ThreatContactSnapshot contact = contacts[i];
                string rejection = null;
                if (contact.Evidence.IsDirect)
                    rejection = "contact-is-direct-not-relayed";
                else if (contact.Evidence.ReporterId <= 0)
                    rejection = "relayed-contact-has-no-reporter";
                else if (now - contact.SourceTick < 0)
                    rejection = "contact-source-tick-is-in-the-future";
                else if (now - contact.SourceTick > 180)
                    rejection = "relayed-contact-older-than-180-ticks";
                else if (now - contact.AcquiredTick
                    < JobGiver_CACombatReaction.ReactionDelay(pawn))
                    rejection = "actor-reaction-delay-not-finished";
                if (rejection != null)
                {
                    NoteRejectedSupportContact(evaluation, contact,
                        rejection);
                    continue;
                }
                Pawn target = SpawnedPawnById(pawn.Map, contact.HostileId);
                Pawn reporter = SpawnedPawnById(pawn.Map,
                    contact.Evidence.ReporterId);
                if (target == null) rejection = "reported-target-unavailable";
                else if (reporter == null)
                    rejection = "immediate-reporter-unavailable";
                else if (target.Dead) rejection = "reported-target-dead";
                else if (target.Downed) rejection = "reported-target-downed";
                else if (!pawn.HostileTo(target))
                    rejection = "reported-target-not-hostile-to-actor";
                else if (!target.Position.InHorDistOf(contact.Cell, 1.5f))
                    rejection = "reported-target-left-copied-contact-cell";
                else if (reporter == pawn)
                    rejection = "reporter-is-the-actor";
                else if (reporter.Dead) rejection = "reporter-dead";
                else if (reporter.Downed) rejection = "reporter-downed";
                else if (!reporter.Awake()) rejection = "reporter-not-awake";
                else if (reporter.InMentalState)
                    rejection = "reporter-in-mental-state";
                else if (reporter.Faction != pawn.Faction)
                    rejection = "reporter-not-same-faction";
                else if (!KnowledgeMapComponent.CanCurrentlySeeHostile(
                    reporter, target))
                    rejection = "reporter-no-longer-sees-target";
                if (rejection != null)
                {
                    NoteRejectedSupportContact(evaluation, contact,
                        rejection);
                    continue;
                }
                ThreatContactSnapshot reporterContact;
                if (!knowledge.TryGetFreshContact(reporter,
                        target.thingIDNumber, out reporterContact))
                    rejection = "reporter-has-no-fresh-contact";
                else if (!reporterContact.Evidence.IsDirect)
                    rejection = "reporter-contact-is-not-direct";
                else if (reporterContact.SourceTick < contact.SourceTick)
                    rejection = "reporter-direct-evidence-is-older-than-relay";
                else if (!reporterContact.Cell.InHorDistOf(
                    target.Position, 1.5f))
                    rejection = "reporter-direct-cell-no-longer-matches-target";
                if (rejection != null)
                {
                    NoteRejectedSupportContact(evaluation, contact,
                        rejection);
                    continue;
                }
                if (!found || contact.SourceTick > selected.SourceTick
                    || contact.SourceTick == selected.SourceTick
                        && contact.HostileId < selected.HostileId)
                {
                    found = true;
                    selected = contact;
                    selectedReporter = reporter;
                }
            }
            if (CATactical.IsHold(pawn))
            {
                ThreatContactSnapshot casualtyContact;
                Pawn casualtyReporter;
                WelfareFactSnapshot freshCasualty;
                if (TrySelectCasualtyReorganizationContact(pawn, contacts,
                        evaluation, out casualtyContact,
                        out casualtyReporter, out freshCasualty))
                {
                    // A fresh collapse is a higher-order change in the squad's
                    // local force balance. Prefer its bounded reorganization lane
                    // even when a healthy reporter still supplies an ordinary
                    // contact that the current Hold cannot reach.
                    casualtyReorganization = true;
                    selected = casualtyContact;
                    selectedReporter = casualtyReporter;
                    casualtyFact = freshCasualty;
                    found = true;
                }
            }
            if (!found)
                return RejectReportedSupport(pawn, evaluation,
                    evaluation.Reason ?? "no-fresh-relayed-support-contact");

            evaluation.TargetId = selected.HostileId;
            evaluation.ReporterId = selectedReporter.thingIDNumber;
            evaluation.SourceTick = selected.SourceTick;
            evaluation.ContactCell = selected.Cell;
            evaluation.CasualtyReorganization = casualtyReorganization;
            evaluation.CasualtyId = casualtyReorganization
                ? casualtyFact.SubjectId : -1;
            evaluation.CasualtySourceTick = casualtyReorganization
                ? casualtyFact.SourceTick : -1;

            CADraftedCombatInitiativeMapComponent initiative = pawn.Map
                .GetComponent<CADraftedCombatInitiativeMapComponent>();
            IntVec3 supportAnchor;
            string envelopeRejection;
            if (initiative == null)
                return RejectReportedSupport(pawn, evaluation,
                    "drafted-initiative-component-unavailable");
            if (!initiative.TryGetSupportEnvelope(pawn,
                    selected.HostileId, selected.SourceTick, selected.Cell,
                    out supportAnchor, out envelopeRejection))
                return RejectReportedSupport(pawn, evaluation,
                    envelopeRejection ?? "support-envelope-rejected");

            CACombatCellAssessment current;
            CACombatCellAssessment better;
            CARangedCoordinationCompetence competence =
                CACompetence.RangedCoordination(pawn);
            float casualtyApproachLimit = casualtyReorganization
                ? competence.ReorganizationApproachLimit : 4.5f;
            if (casualtyReorganization)
            {
                if (!TryFindCasualtyReorganizationStage(pawn, selected.Cell,
                        casualtyApproachLimit, out current, out better))
                    return RejectReportedSupport(pawn, evaluation,
                        "no-safe-casualty-reorganization-stage");
            }
            else if (!TryFindBetterFirePositionAtCopiedContact(pawn,
                         selected.HostileId, selected.Cell, false, out current,
                         out better))
                return RejectReportedSupport(pawn, evaluation,
                    "no-better-fire-position-at-copied-contact");
            float move = pawn.Position.DistanceTo(better.cell);
            float approach = supportAnchor.DistanceTo(selected.Cell)
                - better.cell.DistanceTo(selected.Cell);
            bool covered = better.selfCover >= 0.20f
                || better.selfCover >= current.selfCover + 0.12f;
            evaluation.Destination = better.cell;
            evaluation.Move = move;
            evaluation.Approach = approach;
            evaluation.CurrentCover = current.selfCover;
            evaluation.DestinationCover = better.selfCover;
            evaluation.CurrentSectorRisk = current.friendlySectorRisk;
            evaluation.DestinationSectorRisk =
                better.friendlySectorRisk;
            evaluation.RouteSectorRisk = better.friendlySectorRouteRisk;
            if (move > 9f)
                return RejectReportedSupport(pawn, evaluation,
                    "support-move-exceeds-nine-cells");
            if (!better.cell.InHorDistOf(supportAnchor, 12f))
                return RejectReportedSupport(pawn, evaluation,
                    "destination-leaves-support-envelope");
            if (approach > casualtyApproachLimit)
                return RejectReportedSupport(pawn, evaluation,
                    casualtyReorganization
                        ? "casualty-stage-exceeds-skill-bounded-approach"
                        : "support-move-approaches-more-than-4.5-cells");
            if (!casualtyReorganization && !covered)
                return RejectReportedSupport(pawn, evaluation,
                    "destination-cover-insufficient");
            if (better.friendlySectorRisk >= FriendlySectorUnsafeRisk)
                return RejectReportedSupport(pawn, evaluation,
                    "destination-inside-active-friendly-fire-sector");
            if (better.friendlySectorRouteRisk >= FriendlySectorUnsafeRisk)
                return RejectReportedSupport(pawn, evaluation,
                    "route-enters-active-friendly-fire-sector");

            result = PositionJob(pawn, better.cell);
            if (result == null)
                return RejectReportedSupport(pawn, evaluation,
                    "locked-support-route-could-not-be-built");
            StampTacticalOwner(pawn, result);
            commit = new CAReportedSupportCommit
            {
                targetId = selected.HostileId,
                sourceTick = selected.SourceTick,
                reporterId = selectedReporter.thingIDNumber,
                selectedTick = now,
                targetCell = selected.Cell,
                anchor = supportAnchor,
                origin = pawn.Position,
                destination = better.cell,
                deliveryChannel = selected.Evidence.DeliveryChannel.ToString(),
                reporterLabel = selectedReporter.LabelShort,
                move = move,
                approach = approach,
                currentCover = current.selfCover,
                destinationCover = better.selfCover,
                currentSectorRisk = current.friendlySectorRisk,
                destinationSectorRisk = better.friendlySectorRisk,
                casualtyReorganization = casualtyReorganization,
                casualtyId = casualtyReorganization
                    ? casualtyFact.SubjectId : -1,
                casualtySourceTick = casualtyReorganization
                    ? casualtyFact.SourceTick : -1
            };
            evaluation.Stage = "proposed";
            evaluation.Reason = casualtyReorganization
                ? "bounded-casualty-reorganization-proposed"
                : "bounded-support-move-proposed";
            RecordReportedSupportEvaluation(pawn, evaluation);
            return true;
        }

        private static bool RejectReportedSupport(Pawn pawn,
            CAReportedSupportEvaluation evaluation, string reason)
        {
            if (evaluation != null)
            {
                evaluation.Tick = Find.TickManager != null
                    ? Find.TickManager.TicksGame : -1;
                evaluation.Stage = "rejected";
                evaluation.Reason = reason;
                RecordReportedSupportEvaluation(pawn, evaluation);
            }
            return false;
        }

        private static void NoteRejectedSupportContact(
            CAReportedSupportEvaluation evaluation,
            ThreatContactSnapshot contact, string reason)
        {
            if (evaluation == null || contact.SourceTick
                    < evaluation.SourceTick) return;
            evaluation.TargetId = contact.HostileId;
            evaluation.ReporterId = contact.Evidence.ReporterId;
            evaluation.SourceTick = contact.SourceTick;
            evaluation.ContactCell = contact.Cell;
            evaluation.Reason = reason;
        }

        // A completed local Hold is not an instruction to ignore a newly reported
        // squad collapse forever. This pairs one fresh, actionable downed-teammate
        // fact with one still-active hostile contact at that casualty pocket. It
        // does not infer the attacker, expose live map state, authorize a shot, or
        // select a distant pawn merely because they exist on the map.
        private static bool TrySelectCasualtyReorganizationContact(Pawn pawn,
            List<ThreatContactSnapshot> contacts,
            CAReportedSupportEvaluation evaluation,
            out ThreatContactSnapshot selected, out Pawn casualty,
            out WelfareFactSnapshot casualtyFact)
        {
            selected = default(ThreatContactSnapshot);
            casualty = null;
            casualtyFact = default(WelfareFactSnapshot);
            if (pawn == null || pawn.Map == null || contacts == null)
                return false;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(
                pawn.Map);
            if (knowledge == null) return false;
            var welfare = new List<WelfareFactSnapshot>();
            knowledge.CopyFreshWelfare(pawn, welfare);
            if (evaluation != null)
                evaluation.CandidateCount += welfare.Count;
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            int reactionDelay = JobGiver_CACombatReaction.ReactionDelay(pawn);
            bool found = false;
            float selectedSeparation = float.MaxValue;

            for (int factIndex = 0; factIndex < welfare.Count; factIndex++)
            {
                WelfareFactSnapshot fact = welfare[factIndex];
                int casualtyAge = now - fact.SourceTick;
                if (!fact.Actionable || !fact.ObservedDowned
                    || !fact.Cell.IsValid || !fact.Cell.InBounds(pawn.Map)
                    || fact.Evidence.Confidence < 0.70f
                    || casualtyAge < reactionDelay || casualtyAge > 600)
                    continue;
                Pawn subject = SpawnedPawnById(pawn.Map, fact.SubjectId);
                if (subject == null || subject == pawn || subject.Dead
                    || !subject.Downed || subject.Faction != pawn.Faction)
                    continue;

                for (int contactIndex = 0; contactIndex < contacts.Count;
                    contactIndex++)
                {
                    ThreatContactSnapshot contact = contacts[contactIndex];
                    int contactAge = now - contact.SourceTick;
                    if (contact.State != ThreatContactState.Active
                        || contactAge < reactionDelay || contactAge > 360
                        || contact.Evidence.Confidence < 0.65f
                        || !contact.Cell.IsValid
                        || !contact.Cell.InBounds(pawn.Map)
                        || contact.SourceTick < fact.SourceTick - 360
                        || contact.SourceTick > fact.SourceTick + 600)
                        continue;
                    Pawn target = SpawnedPawnById(pawn.Map,
                        contact.HostileId);
                    if (target == null || target.Dead || target.Downed
                        || !pawn.HostileTo(target)
                        || !target.Position.InHorDistOf(contact.Cell, 1.5f))
                        continue;
                    float separation = contact.Cell.DistanceTo(fact.Cell);
                    if (separation > 14f) continue;
                    bool newer = !found
                        || fact.SourceTick > casualtyFact.SourceTick
                        || fact.SourceTick == casualtyFact.SourceTick
                            && contact.SourceTick > selected.SourceTick
                        || fact.SourceTick == casualtyFact.SourceTick
                            && contact.SourceTick == selected.SourceTick
                            && separation < selectedSeparation - 0.001f
                        || fact.SourceTick == casualtyFact.SourceTick
                            && contact.SourceTick == selected.SourceTick
                            && Mathf.Abs(separation - selectedSeparation)
                                <= 0.001f
                            && contact.HostileId < selected.HostileId;
                    if (!newer) continue;
                    found = true;
                    selected = contact;
                    casualty = subject;
                    casualtyFact = fact;
                    selectedSeparation = separation;
                }
            }
            return found;
        }

        private static bool TryFindCasualtyReorganizationStage(Pawn pawn,
            IntVec3 targetCell, float approachLimit,
            out CACombatCellAssessment current,
            out CACombatCellAssessment better)
        {
            current = InvalidCellAssessment();
            better = InvalidCellAssessment();
            if (pawn == null || pawn.Map == null || !targetCell.IsValid
                || !targetCell.InBounds(pawn.Map)) return false;
            Map map = pawn.Map;
            CASpatialCombatMemoryMapComponent.For(map)?.ObserveNow(pawn);
            EscapeSafetyContext safety = BuildEscapeSafetyContext(pawn);
            float currentDistance = pawn.Position.DistanceTo(targetCell);
            if (!TryScoreCasualtyReorganizationCell(pawn, targetCell,
                    pawn.Position, currentDistance, safety, out current))
                return false;
            better = current;
            int limit = GenRadial.NumCellsInRadius(approachLimit + 0.1f);
            for (int i = 1; i < limit; i++)
            {
                IntVec3 cell = pawn.Position + GenRadial.RadialPattern[i];
                float progress = currentDistance - cell.DistanceTo(targetCell);
                if (progress < 1.5f || progress > approachLimit + 0.05f
                    || !cell.InBounds(map) || !cell.WalkableBy(map, pawn)
                    || !cell.InAllowedArea(pawn) || cell.ContainsStaticFire(map)
                    || !CASpatialCombatMemoryMapComponent.KnowsCell(pawn, cell)
                    || !map.pawnDestinationReservationManager.CanReserve(
                        cell, pawn)
                    || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)
                    || CellOccupiedByOtherPawn(cell, map, pawn)
                    || CACombatIntent.IsRecentlyUnsafe(pawn, cell)) continue;
                CACombatCellAssessment candidate;
                if (!TryScoreCasualtyReorganizationCell(pawn, targetCell,
                        cell, currentDistance, safety, out candidate))
                    continue;
                EscapeRouteCandidate route;
                if (!TryAssessEscapeRoute(pawn, cell, targetCell, safety,
                        out route)) continue;
                candidate.friendlySectorRouteCells =
                    route.friendlySectorCells;
                candidate.friendlySectorRouteRisk =
                    route.maxFriendlySectorRisk;
                candidate.score -= route.exposedCells * 0.12f
                    + route.cumulativeHostilePressure * 0.12f
                    + route.maxHostilePressure * 0.40f
                    + route.maxHazard * 0.90f
                    + route.friendlySectorCells * 0.45f
                    + route.maxFriendlySectorRisk * 0.90f;
                if (candidate.hazard > current.hazard + 0.08f
                    || route.maxHazard > current.hazard + 0.10f
                    || candidate.localPressure
                        > current.localPressure + 0.10f
                    || route.maxHostilePressure
                        > current.localPressure + 0.12f)
                    continue;
                if (current.friendlySectorRisk < FriendlySectorUnsafeRisk
                    && (candidate.friendlySectorRisk
                            >= FriendlySectorUnsafeRisk
                        || route.maxFriendlySectorRisk
                            >= FriendlySectorUnsafeRisk)) continue;
                if (current.friendlySectorRisk >= FriendlySectorUnsafeRisk
                    && candidate.friendlySectorRisk
                        >= current.friendlySectorRisk - 0.05f) continue;
                bool betterScore = candidate.score > better.score + 0.02f
                    || Mathf.Abs(candidate.score - better.score) <= 0.02f
                        && cell.DistanceTo(targetCell)
                            < better.cell.DistanceTo(targetCell) - 0.001f
                    || Mathf.Abs(candidate.score - better.score) <= 0.02f
                        && Mathf.Abs(cell.DistanceTo(targetCell)
                            - better.cell.DistanceTo(targetCell)) <= 0.001f
                        && CellOrder(cell, better.cell) < 0;
                if (betterScore) better = candidate;
            }
            return better.cell.IsValid && better.cell != pawn.Position;
        }

        private static bool TryScoreCasualtyReorganizationCell(Pawn pawn,
            IntVec3 targetCell, IntVec3 cell, float currentDistance,
            EscapeSafetyContext safety,
            out CACombatCellAssessment assessment)
        {
            assessment = InvalidCellAssessment();
            if (pawn == null || pawn.Map == null || !cell.InBounds(pawn.Map))
                return false;
            float cover = CoverUtility.CalculateOverallBlockChance(cell,
                targetCell, pawn.Map);
            float pressure = EscapeThreatPressure(pawn, cell, targetCell,
                safety);
            float sector = FriendlySectorRisk(cell,
                safety.activeFriendlySectors);
            float support = RecoveryMutualSupport(pawn, cell, safety);
            float shelter = StructuralRecoveryShelter(pawn, cell, targetCell,
                safety);
            float hazard = CACombatIntent.HazardScore(pawn, cell);
            float travel = pawn.Position.DistanceTo(cell);
            float progress = currentDistance - cell.DistanceTo(targetCell);
            float score = progress * 0.45f + cover * 0.55f
                + support * 0.40f + shelter * 0.35f
                - pressure * 1.25f - sector * 1.50f
                - hazard * 2f - travel * 0.03f;
            assessment = new CACombatCellAssessment
            {
                cell = cell,
                score = score,
                selfCover = cover,
                friendlySectorRisk = sector,
                friendlySectorRouteRisk = sector,
                travel = travel,
                mutualSupport = support,
                localPressure = pressure,
                hazard = hazard
            };
            return true;
        }

        private static void RecordReportedSupportEvaluation(Pawn pawn,
            CAReportedSupportEvaluation evaluation)
        {
            if (pawn == null || pawn.Map == null || evaluation == null) return;
            pawn.Map.GetComponent<CADraftedCombatInitiativeMapComponent>()
                ?.RecordSupportEvaluation(pawn, evaluation);
        }

        internal static void BeginReportedSupportPositionJob(Pawn pawn,
            Job job, CAReportedSupportCommit commit)
        {
            if (pawn == null || pawn.Map == null || job == null
                || !commit.IsValid) return;
            CADraftedCombatInitiativeMapComponent initiative = pawn.Map
                .GetComponent<CADraftedCombatInitiativeMapComponent>();
            if (initiative == null) return;
            initiative.BeginSupportAttempt(pawn, job, commit);
            CAIntentContext context = CACombatIntent.Continuation(pawn,
                CAIntentController.FirePosition);
            CARangedCoordinationCompetence competence =
                CACompetence.RangedCoordination(pawn);
            Pawn target = SpawnedPawnById(pawn.Map, commit.targetId);
            CACombatIntent.RecordMovement(pawn, context, commit.origin,
                commit.destination, target);
            CATrace.Pawn(pawn,
                (commit.casualtyReorganization
                    ? "drafted idle watch STARTS casualty-driven reorganization after fresh teammate collapse "
                        + commit.reporterLabel + "#" + commit.casualtyId
                        + " at source tick " + commit.casualtySourceTick
                        + "; active " + commit.deliveryChannel
                        + " contact remains in that casualty pocket, with bounded geometry-aware stage "
                    : "drafted idle watch STARTS support attempt for fresh "
                        + commit.deliveryChannel + " contact from "
                        + commit.reporterLabel + " with bounded covered move ")
                + commit.destination + " scored against copied contact cell "
                + commit.targetCell + " (source age "
                + (Find.TickManager.TicksGame - commit.sourceTick)
                + " ticks, move " + commit.move.ToString("F1")
                + ", approach " + commit.approach.ToString("F1")
                + ", cover " + commit.currentCover.ToString("F2") + " -> "
                + commit.destinationCover.ToString("F2")
                + ", friendly-sector risk "
                + commit.currentSectorRisk.ToString("F2") + " -> "
                + commit.destinationSectorRisk.ToString("F2") + ", "
                + competence.TraceText() + ")",
                target: target, contact: commit.targetCell,
                destination: commit.destination, anchor: commit.origin,
                intent: context);
        }

        internal static void ConfirmReportedSupportProgress(Pawn pawn,
            Job job)
        {
            if (pawn == null || pawn.Map == null || job == null) return;
            CADraftedCombatInitiativeMapComponent initiative = pawn.Map
                .GetComponent<CADraftedCombatInitiativeMapComponent>();
            CADraftedSupportDiagnosticState state;
            if (initiative == null || !initiative.TryCommitSupportAttempt(
                    pawn, job, out state)) return;
            positionClocks[pawn.thingIDNumber] = new PositionClock
            {
                nextTick = Find.TickManager.TicksGame + 180,
                targetId = state.TargetId,
                targetCell = state.TargetCell,
                actorCell = state.AttemptOrigin,
                anchorCell = state.Anchor,
                targetRetreating = false
            };
            CAIntentContext context = CACombatIntent.Continuation(pawn,
                CAIntentController.FirePosition);
            Pawn target = SpawnedPawnById(pawn.Map, state.TargetId);
            CATrace.Pawn(pawn,
                "drafted support attempt COMMITS after first actual movement"
                + " (job " + job.loadID + ", source tick "
                + state.SourceTick + ", contact " + state.TargetCell + ")",
                target: target, contact: state.TargetCell,
                destination: job.targetA.Cell, anchor: state.AttemptOrigin,
                intent: context);
        }

        internal static void AbortReportedSupportAttempt(Pawn pawn, Job job)
        {
            if (pawn == null || pawn.Map == null || job == null) return;
            CADraftedCombatInitiativeMapComponent initiative = pawn.Map
                .GetComponent<CADraftedCombatInitiativeMapComponent>();
            CADraftedSupportDiagnosticState state;
            if (initiative == null || !initiative.TryAbortSupportAttempt(
                    pawn, job, out state)) return;
            CATrace.Pawn(pawn,
                "drafted support attempt ABORTS before any movement; contact remains eligible for reevaluation"
                + " (job " + job.loadID + ", source tick "
                + state.SourceTick + ")",
                contact: state.TargetCell, destination: job.targetA.Cell,
                anchor: state.AttemptOrigin);
        }

        internal static bool TryFindBetterFirePosition(Pawn pawn, Pawn target,
            bool urgent, out CACombatCellAssessment current,
            out CACombatCellAssessment better)
        {
            current = InvalidCellAssessment();
            better = InvalidCellAssessment();
            if (pawn == null || target == null || pawn.Map == null
                || target.Map != pawn.Map) return false;

            Verb verb = pawn.TryGetAttackVerb(target, AllowManualCombatVerb(pawn));
            if (verb == null || verb.verbProps.IsMeleeAttack) return false;
            return TryFindBetterFirePositionCore(pawn, target.thingIDNumber,
                target.Position, new LocalTargetInfo(target), target, verb,
                urgent, out current, out better);
        }

        private static bool TryFindBetterFirePositionAtCopiedContact(
            Pawn pawn, int targetId, IntVec3 targetCell, bool urgent,
            out CACombatCellAssessment current,
            out CACombatCellAssessment better)
        {
            current = InvalidCellAssessment();
            better = InvalidCellAssessment();
            if (pawn == null || pawn.Map == null || targetId <= 0
                || !targetCell.IsValid || !targetCell.InBounds(pawn.Map))
                return false;
            Verb verb = RangedVerbForCopiedCell(pawn, targetCell);
            if (verb == null) return false;
            return TryFindBetterFirePositionCore(pawn, targetId, targetCell,
                new LocalTargetInfo(targetCell), null, verb, urgent,
                out current, out better);
        }

        private static bool TryFindBetterFirePositionCore(Pawn pawn,
            int targetId, IntVec3 targetCell, LocalTargetInfo targetInfo,
            Pawn exactTarget, Verb verb, bool urgent,
            out CACombatCellAssessment current,
            out CACombatCellAssessment better)
        {
            current = InvalidCellAssessment();
            better = InvalidCellAssessment();

            CARangedCoordinationCompetence competence =
                CACompetence.RangedCoordination(pawn);
            DispositionProfile disposition = Disposition.Of(pawn);
            float tactical = Mathf.Clamp01(
                competence.TacticalSynthesis * 0.75f
                + disposition.initiative * 0.15f
                + disposition.discipline * 0.10f);
            float radius = competence.SearchRadius;
            IntVec3 tacticalAnchor;
            float tacticalRadius;
            bool insideTacticalPosture = TryTacticalEnvelope(pawn,
                out tacticalAnchor, out tacticalRadius);
            FirePositionContext context = BuildFirePositionContext(pawn,
                exactTarget, targetCell);
            PositionClock clock;
            bool hasClock = positionClocks.TryGetValue(pawn.thingIDNumber,
                out clock) && clock.targetId == targetId;
            bool degraded = context.actorCondition.RequiresCombatRecovery(
                ranged: true);
            bool restrictForward = degraded
                || hasClock && clock.targetRetreating;
            IntVec3 movementAnchor = hasClock && clock.anchorCell.IsValid
                ? clock.anchorCell : pawn.Position;
            float currentThreatDistance = pawn.Position.DistanceTo(
                targetCell);
            if (!TryScoreCell(pawn, targetInfo, targetCell, exactTarget,
                verb, pawn.Position, radius, tactical, true, false, context,
                out current)) return false;

            better = current;
            bool scanTacticalEnvelope = insideTacticalPosture
                && !current.hasLineOfFire;
            IntVec3 candidateCenter = scanTacticalEnvelope
                ? tacticalAnchor : pawn.Position;
            float candidateRadius = scanTacticalEnvelope
                ? tacticalRadius : radius;
            int limit = GenRadial.NumCellsInRadius(candidateRadius);
            int firstCandidate = scanTacticalEnvelope ? 0 : 1;
            for (int i = firstCandidate; i < limit; i++)
            {
                if (!ShouldEvaluateCandidate(i, limit)) continue;
                IntVec3 cell = candidateCenter + GenRadial.RadialPattern[i];
                if (CACombatIntent.IsRecentlyUnsafe(pawn, cell)) continue;
                if (restrictForward && cell.DistanceTo(targetCell)
                    < currentThreatDistance - 0.25f) continue;
                if (restrictForward
                    && !cell.InHorDistOf(movementAnchor, 12f)) continue;
                if (insideTacticalPosture
                    && !cell.InHorDistOf(tacticalAnchor, tacticalRadius)) continue;
                CACombatCellAssessment candidate;
                if (!TryScoreCell(pawn, targetInfo, targetCell, exactTarget,
                    verb, cell, radius, tactical, false, true, context,
                    out candidate)) continue;
                bool preliminarilyBetter = candidate.score
                        > better.score + 0.0001f
                    || Mathf.Abs(candidate.score - better.score) <= 0.0001f
                        && CellOrder(candidate.cell, better.cell) < 0;
                if (!preliminarilyBetter) continue;
                if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                    continue;
                if (insideTacticalPosture
                    && !PathStaysInsideTacticalEnvelope(pawn, cell,
                        tacticalAnchor, tacticalRadius)) continue;
                int sectorCells;
                float routeSectorRisk;
                if (!TryAssessFriendlySectorRoute(pawn, cell,
                        context.activeFriendlySectors, out sectorCells,
                        out routeSectorRisk)) continue;
                candidate.friendlySectorRouteCells = sectorCells;
                candidate.friendlySectorRouteRisk = routeSectorRisk;
                candidate.score -= sectorCells * 0.42f
                    + routeSectorRisk * 0.85f;
                // Do not create a new crossfire crossing for an otherwise safe
                // actor. If already inside a sector, a bounded route may exit it
                // only when the destination materially reduces that exposure.
                if (routeSectorRisk >= FriendlySectorUnsafeRisk
                    && current.friendlySectorRisk
                        < FriendlySectorUnsafeRisk) continue;
                if (current.friendlySectorRisk >= FriendlySectorUnsafeRisk
                    && candidate.friendlySectorRisk
                        >= current.friendlySectorRisk - 0.05f) continue;
                if (candidate.score > better.score + 0.0001f
                    || Mathf.Abs(candidate.score - better.score) <= 0.0001f
                        && CellOrder(candidate.cell, better.cell) < 0)
                    better = candidate;
            }

            if (better.cell == pawn.Position) return false;
            bool restoresLine = !current.hasLineOfFire && better.hasLineOfFire;
            bool improvesCover = better.selfCover >= current.selfCover + 0.12f;
            bool improvesSupport = better.mutualSupport
                >= current.mutualSupport + 0.18f;
            bool disperses = current.crowding >= 0.22f
                && better.crowding <= current.crowding - 0.12f;
            bool relievesCollapse = current.localPressure >= 0.42f
                && better.localPressure <= current.localPressure - 0.10f;
            bool clearsLane = current.friendlyLaneRisk >= 0.25f
                && better.friendlyLaneRisk <= current.friendlyLaneRisk - 0.12f;
            bool clearsFriendlySector = current.friendlySectorRisk
                    >= FriendlySectorUnsafeRisk
                && better.friendlySectorRisk
                    <= current.friendlySectorRisk - 0.05f;
            bool escapesHazard = current.hazard >= 0.10f
                && better.hazard <= current.hazard - 0.08f;
            // A short-ranged weapon does not, by itself, authorize a solo advance.
            // If the actor already has a shot, closing toward the contact must not
            // buy that range by worsening exposure, discarding cover, or crossing
            // into an enemy-favoured local pocket while mutual support falls. This
            // is deliberately a veto on bad trades, not a blanket ban on advances:
            // a covered, supported move may still close and fire.
            float currentDistance = pawn.Position.DistanceTo(targetCell);
            float destinationDistance = better.cell.DistanceTo(targetCell);
            bool forwardDisplacement = current.hasLineOfFire
                && destinationDistance < currentDistance - 1.5f;
            bool losesCoverForward = better.selfCover
                < current.selfCover - 0.05f;
            bool worsensExposureForward = better.localPressure
                    > current.localPressure + 0.05f
                && better.selfCover < current.selfCover + 0.20f;
            bool entersUnsupportedPocket = better.mutualSupport
                    < current.mutualSupport - 0.05f
                && better.friendlyPower < better.hostilePower;
            if (forwardDisplacement && !clearsFriendlySector
                && !escapesHazard && (losesCoverForward
                    || worsensExposureForward || entersUnsupportedPocket))
            {
                CATrace.Skip(pawn, "forward fire-position displacement",
                    "an existing firing line does not justify closing into a worse exposure or unsupported local force balance"
                    + " (distance " + currentDistance.ToString("F1") + " -> "
                    + destinationDistance.ToString("F1") + ", cover "
                    + current.selfCover.ToString("F2") + " -> "
                    + better.selfCover.ToString("F2") + ", support "
                    + current.mutualSupport.ToString("F2") + " -> "
                    + better.mutualSupport.ToString("F2") + ", pressure "
                    + current.localPressure.ToString("F2") + " -> "
                    + better.localPressure.ToString("F2") + ", force "
                    + current.friendlyPower.ToString("F2") + "/"
                    + current.hostilePower.ToString("F2") + " -> "
                    + better.friendlyPower.ToString("F2") + "/"
                    + better.hostilePower.ToString("F2") + ")",
                    target: exactTarget, contact: targetCell,
                    destination: better.cell, anchor: pawn.Position);
                return false;
            }
            // Keep the axes explicit: a scalar win without an operational reason is
            // not enough to make a pawn abandon their present ground.
            if (!restoresLine && !improvesCover && !improvesSupport
                && !relievesCollapse && !clearsLane
                && !clearsFriendlySector && !escapesHazard
                && !disperses) return false;
            float threshold = urgent ? 0.015f
                : Mathf.Lerp(0.22f, 0.055f, tactical);
            if (restoresLine) threshold = Mathf.Min(threshold, 0.025f);
            if (relievesCollapse) threshold *= 0.45f;
            if (improvesCover || improvesSupport || clearsLane
                || clearsFriendlySector || disperses)
                threshold *= 0.75f;
            if (current.friendlyLaneRisk >= 0.25f) threshold *= 0.5f;
            if (current.friendlySectorRisk >= FriendlySectorUnsafeRisk)
                threshold *= 0.5f;
            if (escapesHazard) threshold = Mathf.Min(threshold, 0.01f);
            return better.score >= current.score + threshold;
        }

        private static bool TryScoreCell(Pawn pawn,
            LocalTargetInfo targetInfo, IntVec3 targetCell, Pawn exactTarget,
            Verb verb, IntVec3 cell, float searchRadius, float tactical,
            bool current, bool requireLineOfFire,
            FirePositionContext context, out CACombatCellAssessment assessment)
        {
            assessment = default(CACombatCellAssessment);
            Map map = pawn.Map;
            if (!cell.InBounds(map) || !cell.WalkableBy(map, pawn)
                || cell.ContainsStaticFire(map)
                || !GenSight.LineOfSight(pawn.Position, cell, map, true))
                return false;
            bool hasLineOfFire = verb.CanHitTargetFrom(cell, targetInfo);
            if (requireLineOfFire && !hasLineOfFire) return false;

            if (!current)
            {
                if (!cell.InAllowedArea(pawn)
                    || !map.pawnDestinationReservationManager.CanReserve(cell, pawn)
                    || PawnUtility.KnownDangerAt(cell, map, pawn)
                    || CellOccupiedByOtherPawn(cell, map, pawn)) return false;
            }

            float distance = cell.DistanceTo(targetCell);
            float targetCover = exactTarget != null
                ? CoverUtility.CalculateOverallBlockChance(exactTarget,
                    cell, map)
                : CoverUtility.CalculateOverallBlockChance(targetCell,
                    cell, map);
            float shotQuality = hasLineOfFire
                ? Mathf.Clamp01(ShotReport.HitFactorFromShooter(pawn, distance)
                    * verb.verbProps.GetHitChanceFactor(
                        verb.EquipmentSource, distance)
                    * (1f - targetCover)) : 0f;
            float selfCover = CoverUtility.CalculateOverallBlockChance(
                cell, targetCell, map);
            float optimal = Mathf.Max(5f, verb.EffectiveRange * 0.72f);
            float rangeFit = 1f - Mathf.Clamp01(
                Mathf.Abs(distance - optimal) / optimal);
            float laneRisk = hasLineOfFire
                ? FriendlyLaneRisk(targetInfo, verb, cell,
                    context.visibleLaneBodies) : 1f;
            float sectorRisk = FriendlySectorRisk(cell,
                context.activeFriendlySectors);
            float travel = pawn.Position.DistanceTo(cell);
            float travelNorm = Mathf.Clamp01(travel / Mathf.Max(1f, searchRadius));
            float support = VisibleMutualSupport(context, cell);
            float friendlyPower = VisibleFriendlyPower(context, cell);
            float hostilePower;
            float pressure = VisibleLocalPressure(pawn, context, cell,
                out hostilePower);
            float crowding = VisibleCrowding(context, cell);
            float hazard = CACombatIntent.HazardScore(pawn, cell);
            float forceTotal = Mathf.Max(0.01f,
                friendlyPower + hostilePower);
            float forceBalance = Mathf.Clamp(
                (friendlyPower - hostilePower) / forceTotal, -1f, 1f);
            DispositionProfile disposition = Disposition.Of(pawn);
            float impairedTravel = Mathf.Lerp(1.45f, 1f,
                context.actorCondition.Moving);
            float impairedPressure = Mathf.Lerp(1.35f, 1f,
                context.actorCondition.CombatPower);
            float score = shotQuality * Mathf.Lerp(0.45f, 0.80f, tactical)
                + selfCover * Mathf.Lerp(0.75f, 0.55f, disposition.courage)
                + rangeFit * 0.12f
                + support * Mathf.Lerp(0.18f, 0.30f, tactical)
                + forceBalance * (forceBalance >= 0f
                    ? 0.14f : 0.30f * impairedPressure)
                - pressure * Mathf.Lerp(0.30f, 0.48f, tactical) * impairedPressure
                - crowding * 0.78f
                - laneRisk * Mathf.Lerp(0.90f, 1.50f, tactical)
                - sectorRisk * 1.65f
                - travelNorm * Mathf.Lerp(0.18f, 0.08f, tactical) * impairedTravel
                - hazard * 1.20f;

            assessment = new CACombatCellAssessment
            {
                cell = cell,
                score = score,
                shotQuality = shotQuality,
                selfCover = selfCover,
                friendlyLaneRisk = laneRisk,
                friendlySectorRisk = sectorRisk,
                rangeFit = rangeFit,
                travel = travel,
                mutualSupport = support,
                localPressure = pressure,
                crowding = crowding,
                friendlyPower = friendlyPower,
                hostilePower = hostilePower,
                hazard = hazard,
                hasLineOfFire = hasLineOfFire
            };
            return true;
        }

        private static bool TryTacticalEnvelope(Pawn pawn, out IntVec3 anchor,
            out float radius)
        {
            anchor = IntVec3.Invalid;
            radius = 0f;
            if (pawn == null || (!CATactical.IsHold(pawn)
                && !CATactical.IsAutomaticDefense(pawn))
                || pawn.mindState == null || pawn.mindState.duty == null
                || !pawn.mindState.duty.focus.IsValid) return false;
            anchor = pawn.mindState.duty.focus.Cell;
            radius = Mathf.Max(1f, pawn.mindState.duty.radius);
            return true;
        }

        private static bool PathStaysInsideTacticalEnvelope(Pawn pawn,
            IntVec3 destination, IntVec3 anchor, float radius)
        {
            if (pawn == null || pawn.Map == null || !destination.IsValid
                || !anchor.IsValid || radius < 1f) return false;
            using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                pawn.Position, destination, pawn,
                PathFinderCostTuning.For(pawn), PathEndMode.OnCell))
            {
                if (path == null || !path.Found) return false;
                List<IntVec3> nodes = path.NodesReversed;
                for (int i = 0; i < nodes.Count; i++)
                    if (!nodes[i].InHorDistOf(anchor, radius + 0.5f))
                        return false;
                return true;
            }
        }

        private static bool ShouldEvaluateCandidate(int radialIndex, int limit)
        {
            const int Budget = 96;
            const int NearCells = 32;
            if (radialIndex < NearCells || limit <= Budget + 1) return true;
            int farStart = Mathf.Min(NearCells, limit);
            int farCount = limit - farStart;
            int farBudget = Mathf.Max(1, Budget - farStart + 1);
            int farIndex = radialIndex - farStart;
            return (farIndex + 1) * farBudget / farCount
                > farIndex * farBudget / farCount;
        }

        private static FirePositionContext BuildFirePositionContext(Pawn pawn,
            Pawn exactTarget, IntVec3 targetCell)
        {
            FirePositionContext context = new FirePositionContext();
            context.actorCondition = CACombatConditionSnapshot.Capture(pawn);
            context.activeFriendlySectors.AddRange(
                ActiveFriendlyFireSectors(pawn));
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == null || other == pawn || other.Dead) continue;
                if (pawn.HostileTo(other))
                {
                    if (KnowledgeMapComponent.CanCurrentlySeeHostile(
                        pawn, other))
                    {
                        context.visibleHostiles.Add(other);
                        context.combatPower[other.thingIDNumber] =
                            CACombatConditionSnapshot.Capture(other)
                                .CombatPower;
                    }
                    continue;
                }

                bool visible = GenSight.LineOfSight(pawn.Position,
                    other.Position, pawn.Map, true);
                if (!visible) continue;
                context.visibleLaneBodies.Add(other);
                if (other.Faction != pawn.Faction) continue;
                context.combatPower[other.thingIDNumber] =
                    CACombatConditionSnapshot.Capture(other).CombatPower;
                if (other.Downed)
                {
                    context.visibleDownedAllies.Add(other);
                    continue;
                }
                if (!other.Awake() || other.InMentalState
                    || other.WorkTagIsDisabled(WorkTags.Violent)) continue;
                context.visibleAllies.Add(other);
                Verb allyVerb = exactTarget != null
                    ? other.TryGetAttackVerb(exactTarget,
                        allowManualCastWeapons: !other.IsColonist)
                    : RangedVerbForCopiedCell(other, targetCell);
                if (allyVerb == null) continue;
                bool canSupport = allyVerb.verbProps.IsMeleeAttack
                    ? other.Position.InHorDistOf(targetCell, 4.5f)
                    : allyVerb.CanHitTargetFrom(other.Position,
                        exactTarget != null
                            ? new LocalTargetInfo(exactTarget)
                            : new LocalTargetInfo(targetCell));
                if (canSupport) context.supporters.Add(other);
            }
            return context;
        }

        private static Verb RangedVerbForCopiedCell(Pawn pawn,
            IntVec3 targetCell)
        {
            if (pawn == null || pawn.Map == null || !targetCell.IsValid)
                return null;
            List<Verb> candidates = MaterializedThreatVerbs(pawn);
            List<ThingWithComps> equipment = pawn.equipment?
                .AllEquipmentListForReading;
            if (equipment != null)
                for (int i = 0; i < equipment.Count; i++)
                {
                    Verb verb = equipment[i]
                        ?.TryGetComp<CompEquippable>()?.PrimaryVerb;
                    if (verb != null && !candidates.Contains(verb))
                        candidates.Add(verb);
                }
            Verb best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                Verb verb = candidates[i];
                if (verb?.verbProps == null || verb.verbProps.IsMeleeAttack)
                    continue;
                bool canHit = verb.CanHitTargetFrom(pawn.Position,
                    new LocalTargetInfo(targetCell));
                if (best == null || canHit && !best.CanHitTargetFrom(
                        pawn.Position, new LocalTargetInfo(targetCell))
                    || canHit == best.CanHitTargetFrom(pawn.Position,
                            new LocalTargetInfo(targetCell))
                        && verb.EffectiveRange > best.EffectiveRange)
                    best = verb;
            }
            return best;
        }

        // Behavior-side physical-sector capture. This may consult weapon and
        // shoot-line APIs while selecting a movement response; the passive flight
        // recorder has its own materialized-state reader and never calls this path.
        internal static List<CAFriendlyFireSectorSnapshot>
            ActiveFriendlyFireSectors(Pawn actor)
        {
            var result = new List<CAFriendlyFireSectorSnapshot>();
            if (actor == null || actor.Map == null || !actor.Spawned)
                return result;
            IReadOnlyList<Pawn> pawns = actor.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn shooter = pawns[i];
                if (shooter == null || shooter == actor || shooter.Dead
                    || shooter.Downed || !shooter.Awake()
                    || actor.HostileTo(shooter) || shooter.HostileTo(actor))
                    continue;
                // The actor can avoid a sector they can presently see being used.
                // A future shared-fire-control fact may broaden this honestly; a
                // same-faction roster alone does not.
                if (!GenSight.LineOfSight(actor.Position, shooter.Position,
                    actor.Map, true)) continue;
                CAFriendlyFireSectorSnapshot sector;
                if (TryReadActiveFriendlyFireSector(shooter, shooter.stances,
                        allowJobFallback: true, out sector))
                    result.Add(sector);
                Pawn_StanceTracker offhandTracker;
                if (OffhandComponent.TryGetExistingTracker(shooter,
                        out offhandTracker)
                    && offhandTracker != shooter.stances
                    && TryReadActiveFriendlyFireSector(shooter,
                        offhandTracker, allowJobFallback: false, out sector))
                    result.Add(sector);
            }
            return result;
        }

        private static bool TryReadActiveFriendlyFireSector(Pawn shooter,
            Pawn_StanceTracker tracker, bool allowJobFallback,
            out CAFriendlyFireSectorSnapshot sector)
        {
            sector = default(CAFriendlyFireSectorSnapshot);
            if (shooter == null || shooter.Map == null) return false;
            Verb verb = null;
            LocalTargetInfo focus = LocalTargetInfo.Invalid;
            Stance_Busy busy = tracker?.curStance as Stance_Busy;
            if (busy != null)
            {
                // A materialized stance is the exact present commitment. Never
                // fabricate a primary-rifle sector from the underlying job while
                // the pawn is actually in melee, cooldown, or another busy verb.
                if (busy.verb == null || busy.verb.verbProps == null
                    || busy.verb.verbProps.IsMeleeAttack
                    || (!(busy is Stance_Warmup) && !busy.verb.Bursting)
                    || !busy.focusTarg.IsValid
                    || !busy.focusTarg.HasThing) return false;
                verb = busy.verb;
                focus = busy.focusTarg;
            }
            else
            {
                if (!allowJobFallback) return false;
                Job job = shooter.CurJob;
                bool targetCommittedJob = job != null
                    && job.targetA.IsValid && job.targetA.HasThing
                    && (job.def == JobDefOf.AttackStatic
                        || job.def == CA_Defs.BoundedRangedDefense);
                if (targetCommittedJob)
                {
                    focus = job.targetA;
                    verb = job.verbToUse;
                }
                else
                {
                    Thing nativeTarget = shooter.Drafted
                        && shooter.drafter != null
                        && shooter.drafter.FireAtWill
                        ? shooter.mindState?.enemyTarget : null;
                    if (nativeTarget == null) return false;
                    focus = nativeTarget;
                }
                if (verb == null)
                    verb = shooter.equipment?.Primary
                        ?.TryGetComp<CompEquippable>()?.PrimaryVerb;
            }
            Thing target = focus.Thing;
            Pawn targetPawn = target as Pawn;
            if (verb == null || verb.verbProps.IsMeleeAttack
                || verb.ProjectileFliesOverhead() || target == null
                || target.Destroyed || !target.Spawned
                || target.Map != shooter.Map || !shooter.HostileTo(target)
                || targetPawn != null && (targetPawn.Dead || targetPawn.Downed))
                return false;
            ShootLine line;
            if (!verb.TryFindShootLineFromTo(shooter.Position, focus,
                    out line)) return false;
            float forcedMiss = VerbUtility.CalculateAdjustedForcedMiss(
                verb.verbProps.ForcedMissRadius,
                line.Dest - line.Source);
            sector = new CAFriendlyFireSectorSnapshot(
                shooter.thingIDNumber, target.thingIDNumber,
                line.Source, line.Dest, Mathf.Max(2.5f, forcedMiss));
            return true;
        }

        private static float FriendlySectorRisk(IntVec3 cell,
            List<CAFriendlyFireSectorSnapshot> sectors)
        {
            float risk = 0f;
            for (int i = 0; i < sectors.Count; i++)
                risk = Mathf.Max(risk, sectors[i].RiskAt(cell));
            return risk;
        }

        internal static bool CombatMoveStepIsStaticallyExecutable(Pawn pawn,
            IntVec3 next)
        {
            return pawn != null && pawn.Map != null && pawn.Spawned
                && next.IsValid && next.InBounds(pawn.Map)
                && next.WalkableBy(pawn.Map, pawn)
                && next.InAllowedArea(pawn);
        }

        internal static bool CombatMoveStepStillSectorSafe(Pawn pawn,
            IntVec3 next)
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned
                || !next.IsValid || !next.InBounds(pawn.Map)) return false;
            List<CAFriendlyFireSectorSnapshot> sectors =
                ActiveFriendlyFireSectors(pawn);
            for (int i = 0; i < sectors.Count; i++)
                if (!FriendlySectorStepAllowed(
                        sectors[i].RiskAt(pawn.Position),
                        sectors[i].RiskAt(next))) return false;
            return true;
        }

        private static bool FriendlySectorStepAllowed(float currentRisk,
            float nextRisk)
        {
            // A clear actor may not enter any individual sector. An actor already
            // inside one may take only equal-or-improving steps through that same
            // sector, and this rule is evaluated independently for every shooter.
            return currentRisk < FriendlySectorUnsafeRisk
                ? nextRisk < FriendlySectorUnsafeRisk
                : nextRisk <= currentRisk + 0.001f;
        }

        private static bool TryAssessFriendlySectorRoute(Pawn pawn,
            IntVec3 destination,
            List<CAFriendlyFireSectorSnapshot> sectors,
            out int exposedCells, out float maxRisk)
        {
            exposedCells = 0;
            maxRisk = 0f;
            if (pawn == null || pawn.Map == null) return false;
            using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                pawn.Position, destination, pawn,
                PathFinderCostTuning.For(pawn), PathEndMode.OnCell))
            {
                if (path == null || !path.Found) return false;
                List<IntVec3> nodes = path.NodesReversed;
                var previousRisks = new float[sectors.Count];
                for (int sector = 0; sector < sectors.Count; sector++)
                    previousRisks[sector] = sectors[sector].RiskAt(
                        pawn.Position);
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    IntVec3 cell = nodes[i];
                    if (cell == pawn.Position || !cell.InBounds(pawn.Map))
                        continue;
                    for (int sector = 0; sector < sectors.Count; sector++)
                    {
                        float nextRisk = sectors[sector].RiskAt(cell);
                        if (!FriendlySectorStepAllowed(
                                previousRisks[sector], nextRisk)) return false;
                        previousRisks[sector] = nextRisk;
                    }
                    float risk = FriendlySectorRisk(cell, sectors);
                    maxRisk = Mathf.Max(maxRisk, risk);
                    if (risk >= FriendlySectorUnsafeRisk) exposedCells++;
                }
                return true;
            }
        }

        // Mutual support is actor-local: only an ally the pawn can presently see and
        // who can presently affect this target contributes. This is not a squad-wide
        // roster or remote health feed.
        private static float VisibleMutualSupport(FirePositionContext context,
            IntVec3 candidate)
        {
            float support = 0f;
            for (int i = 0; i < context.supporters.Count; i++)
            {
                Pawn ally = context.supporters[i];
                float distance = candidate.DistanceTo(ally.Position);
                if (distance > 14f) continue;
                float power = ContextCombatPower(context, ally);
                float band = distance < 3.5f
                    ? Mathf.Lerp(0.05f, 0.20f, distance / 3.5f)
                    : distance <= 9f ? 0.42f
                    : Mathf.Lerp(0.42f, 0.10f,
                        Mathf.Clamp01((distance - 9f) / 5f));
                support += band * power;
            }
            return Mathf.Clamp01(support);
        }

        private static float VisibleFriendlyPower(FirePositionContext context,
            IntVec3 candidate)
        {
            float power = context.actorCondition.CombatPower;
            for (int i = 0; i < context.visibleAllies.Count; i++)
            {
                Pawn ally = context.visibleAllies[i];
                float weight = LocalForceWeight(
                    candidate.DistanceTo(ally.Position));
                if (weight <= 0f) continue;
                power += ContextCombatPower(context, ally) * weight;
            }
            return power;
        }

        private static float LocalForceWeight(float distance)
        {
            if (distance <= 18f) return 1f;
            if (distance >= 28f) return 0f;
            return 1f - ((distance - 18f) / 10f);
        }

        private static float VisibleCrowding(FirePositionContext context,
            IntVec3 candidate)
        {
            float crowding = 0f;
            for (int i = 0; i < context.visibleAllies.Count; i++)
            {
                Pawn ally = context.visibleAllies[i];
                float distance = candidate.DistanceTo(ally.Position);
                if (distance > 4.5f) continue;
                float power = ContextCombatPower(context, ally);
                crowding += (distance <= 2.5f ? 0.62f : 0.28f) * power;
            }
            return Mathf.Clamp01(crowding);
        }

        // Collapse pressure uses only currently visible hostiles and currently visible
        // downed allies. Candidate scoring can therefore react to an observed local
        // overmatch without learning remote pawns or inventing a shared battlefield.
        private static float VisibleLocalPressure(Pawn pawn,
            FirePositionContext context, IntVec3 candidate,
            out float hostilePower)
        {
            float pressure = 0f;
            hostilePower = 0f;
            for (int i = 0; i < context.visibleHostiles.Count; i++)
            {
                Pawn hostile = context.visibleHostiles[i];
                float distance = candidate.DistanceTo(hostile.Position);
                float power = ContextCombatPower(context, hostile);
                hostilePower += power * LocalForceWeight(distance);
                Verb hostileVerb = hostile.TryGetAttackVerb(pawn,
                    allowManualCastWeapons: false);
                if (hostileVerb == null) continue;
                bool canAffect = hostileVerb.verbProps.IsMeleeAttack
                    ? distance <= 5.9f
                    : hostileVerb.CanHitTargetFrom(hostile.Position,
                        new LocalTargetInfo(candidate));
                if (!canAffect) continue;
                float exposure = 1f - CoverUtility.CalculateOverallBlockChance(
                    candidate, hostile.Position, pawn.Map);
                float proximity = 1f - Mathf.Clamp01(distance
                    / Mathf.Max(6f, hostileVerb.EffectiveRange));
                pressure += (0.18f + exposure * 0.28f
                    + proximity * 0.18f) * power;
                if (distance <= 5f) pressure += 0.20f * power;
            }
            for (int i = 0; i < context.visibleDownedAllies.Count; i++)
            {
                Pawn other = context.visibleDownedAllies[i];
                if (!candidate.InHorDistOf(other.Position, 7f)) continue;
                pressure += 0.18f;
            }
            return Mathf.Clamp01(pressure);
        }

        private static float ContextCombatPower(FirePositionContext context,
            Pawn pawn)
        {
            if (context == null || pawn == null) return 0f;
            float power;
            if (context.combatPower.TryGetValue(pawn.thingIDNumber,
                out power)) return power;
            power = CACombatConditionSnapshot.Capture(pawn).CombatPower;
            context.combatPower[pawn.thingIDNumber] = power;
            return power;
        }

        private static float FriendlyLaneRisk(LocalTargetInfo target, Verb verb,
            IntVec3 origin, List<Pawn> visibleLaneBodies)
        {
            Pawn ignored;
            return FriendlyLaneRisk(target, verb, origin, visibleLaneBodies,
                out ignored);
        }

        private static float FriendlyLaneRisk(LocalTargetInfo target, Verb verb,
            IntVec3 origin, List<Pawn> visibleLaneBodies,
            out Pawn primaryBlocker)
        {
            primaryBlocker = null;
            if (verb.ProjectileFliesOverhead()) return 0f;
            ShootLine shootLine;
            if (!verb.TryFindShootLineFromTo(origin, target, out shootLine))
                return 1f;

            Vector2 start = new Vector2(shootLine.Source.x, shootLine.Source.z);
            Vector2 end = new Vector2(shootLine.Dest.x, shootLine.Dest.z);
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.01f) return 0f;

            float forcedMiss = VerbUtility.CalculateAdjustedForcedMiss(
                verb.verbProps.ForcedMissRadius,
                shootLine.Dest - shootLine.Source);
            float corridor = Mathf.Max(1.5f, forcedMiss);
            float risk = 0f;
            float largestContribution = 0f;
            for (int i = 0; i < visibleLaneBodies.Count; i++)
            {
                Pawn other = visibleLaneBodies[i];
                Vector2 point = new Vector2(other.Position.x, other.Position.z);
                float along = Vector2.Dot(point - start, segment) / lengthSquared;
                if (along <= 0.02f || along >= 1f) continue;
                float perpendicular = Vector2.Distance(point, start + segment * along);
                if (perpendicular >= corridor) continue;

                float intercept = VerbUtility.InterceptChanceFactorFromDistance(
                    shootLine.Source.ToVector3Shifted(), other.Position);
                float body = other.RaceProps != null && other.RaceProps.Humanlike
                    ? 1f : 0.4f;
                // RimWorld makes physical interception exactly zero for the first
                // five cells. That engine concession must not make firing directly
                // through a nearby ally look like sound geometry.
                float lanePresence = Mathf.Lerp(0.65f, 1f, intercept);
                float contribution = (1f - perpendicular / corridor)
                    * lanePresence * body;
                risk += contribution;
                if (contribution > largestContribution)
                {
                    largestContribution = contribution;
                    primaryBlocker = other;
                }
            }
            return Mathf.Clamp01(risk);
        }

        // Shared by coordinated ambush release. A ranged ambusher may only fire
        // when the same local lane test used by ordinary combat positioning says
        // the shot does not run through a visible non-hostile body.
        internal static bool HasSafeRangedLane(Pawn shooter, Thing target,
            Verb verb)
        {
            float ignoredRisk;
            Pawn ignoredBlocker;
            bool ignoredImpactEnvelope;
            return HasSafeRangedLane(shooter, target, verb,
                out ignoredRisk, out ignoredBlocker,
                out ignoredImpactEnvelope);
        }

        internal static bool HasSafeRangedLane(Pawn shooter, Thing target,
            Verb verb, out float risk, out Pawn primaryBlocker)
        {
            bool ignoredImpactEnvelope;
            return HasSafeRangedLane(shooter, target, verb, out risk,
                out primaryBlocker, out ignoredImpactEnvelope);
        }

        internal static bool HasSafeRangedLane(Pawn shooter, Thing target,
            Verb verb, out float risk, out Pawn primaryBlocker,
            out bool impactEnvelopeUnsafe)
        {
            risk = 1f;
            primaryBlocker = null;
            impactEnvelopeUnsafe = false;
            if (shooter == null || target == null || verb == null
                || shooter.Map == null || !target.Spawned
                || target.Map != shooter.Map) return false;
            var bodies = new List<Pawn>();
            IReadOnlyList<Pawn> pawns = shooter.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == null || other == shooter || other == target
                    || other.Dead || shooter.HostileTo(other)) continue;
                if (!GenSight.LineOfSight(shooter.Position, other.Position,
                    shooter.Map, true)) continue;
                bodies.Add(other);
            }
            risk = FriendlyLaneRisk(new LocalTargetInfo(target), verb,
                shooter.Position, bodies, out primaryBlocker);
            if (risk >= 0.25f) return false;
            float impactRisk;
            Pawn impactBlocker;
            if (!TryFindImpactEnvelopeBlocker(shooter, target, verb, bodies,
                    out impactBlocker, out impactRisk)) return true;
            impactEnvelopeUnsafe = true;
            primaryBlocker = impactBlocker;
            risk = Mathf.Max(0.25f, impactRisk);
            return false;
        }

        private static bool TryFindImpactEnvelopeBlocker(Pawn shooter,
            Thing target, Verb verb, List<Pawn> visibleNonHostiles,
            out Pawn blocker, out float risk)
        {
            blocker = null;
            risk = 0f;
            ThingDef projectileDef = verb?.GetProjectile();
            ProjectileProperties props = projectileDef?.projectile;
            if (shooter == null || target == null || props == null
                || shooter.Map == null || target.Map != shooter.Map)
                return false;
            float adjustedMiss = VerbUtility.CalculateAdjustedForcedMiss(
                verb.verbProps.ForcedMissRadius,
                target.Position - shooter.Position);
            float harmfulRadius = Mathf.Max(props.explosionRadius,
                verb.verbProps.ai_AvoidFriendlyFireRadius);
            if (props.postExplosionGasType == GasType.ToxGas)
                harmfulRadius = Mathf.Max(harmfulRadius, 1.9f);
            float envelope = harmfulRadius + adjustedMiss;
            if (envelope <= 0.01f) return false;

            float nearest = float.MaxValue;
            for (int index = -1; index < visibleNonHostiles.Count; index++)
            {
                Pawn other = index < 0 ? shooter
                    : visibleNonHostiles[index];
                if (other == null || other.Dead
                    || !CACombatIntent.ProjectileHazardousToPawn(
                        props, other)) continue;
                float distance = other.Position.DistanceTo(target.Position);
                if (distance > envelope) continue;
                if (distance <= harmfulRadius
                    && !GenSight.LineOfSight(target.Position, other.Position,
                        shooter.Map, true)) continue;
                if (distance >= nearest) continue;
                nearest = distance;
                blocker = other;
                risk = Mathf.Clamp01(1f - distance
                    / Mathf.Max(0.1f, envelope));
            }
            return blocker != null;
        }

        internal static bool ShouldProtectAutonomousRangedShot(Pawn shooter,
            Thing target, Verb verb, out string ownership)
        {
            ownership = null;
            if (shooter == null || target == null || verb == null
                || shooter.Map == null || target.Map != shooter.Map
                || verb.verbProps == null || verb.verbProps.IsMeleeAttack
                || verb.ProjectileFliesOverhead() || !shooter.HostileTo(target))
                return false;
            Job job = shooter.CurJob;
            if (job == null) return false;
            if (MovingFire.OwnsCurrentAutonomousShot(shooter, target,
                    job.loadID))
            {
                ownership = "ca-moving-fire";
                return true;
            }
            if (OffhandPatches.OwnsAutonomousFollowup(shooter, target, verb))
            {
                // The player may own the main-hand shot, but CA independently
                // joins the offhand. Protect that added shot without rewriting
                // the provenance of the operator-authored main attack.
                ownership = "ca-offhand-followup";
                return true;
            }
            if (job.def == CA_Defs.BoundedRangedDefense
                || job.def == CA_Defs.CombatMove
                || job.def == CA_Defs.FightingWithdrawal
                || job.def == CA_Defs.StackPosture
                || job.def == CA_Defs.CombatRecovery)
            {
                ownership = "ca-job-def";
                return true;
            }
            // Direct native AttackStatic/forced-fire jobs remain exactly the
            // player's shot. Only independently proved CA target ownership above
            // may coexist with a player-authored movement or tactical order.
            if (job.playerForced || job.playerInterruptedForced) return false;
            if (job.def == JobDefOf.Wait_Combat)
            {
                AwarenessSettings settings = AwarenessMod.Settings;
                if (settings == null
                    || !settings.raidResponse
                        && !settings.survivalResponses) return false;
                if (!shooter.IsColonistPlayerControlled || !shooter.Drafted
                    || shooter.drafter == null
                    || !shooter.drafter.FireAtWill) return false;
                ownership = "drafted-fire-at-will";
                return true;
            }
            if (job.def != JobDefOf.AttackStatic) return false;
            if (job.jobGiver is JobGiver_CACombatReaction)
            {
                ownership = "ca-combat-reaction";
                return true;
            }
            if (!IsRegisteredAutonomousRangedJob(shooter, job)) return false;
            ownership = "ca-registered-ranged-job";
            return true;
        }

        internal static void RegisterAutonomousRangedJob(Pawn pawn, Job job)
        {
            if (pawn == null || job == null || pawn.Map == null) return;
            CADraftedCombatInitiativeMapComponent component = pawn.Map
                .GetComponent<CADraftedCombatInitiativeMapComponent>();
            component?.RegisterAutonomousRangedJob(pawn, job);
        }

        private static bool IsRegisteredAutonomousRangedJob(Pawn pawn,
            Job job)
        {
            if (pawn == null || job == null || pawn.Map == null) return false;
            CADraftedCombatInitiativeMapComponent component = pawn.Map
                .GetComponent<CADraftedCombatInitiativeMapComponent>();
            return component != null
                && component.OwnsAutonomousRangedJob(pawn, job);
        }

        internal static void RecordFriendlyFireAuthorization(Pawn shooter,
            Thing target, Pawn blocker, float laneRisk, string phase,
            string outcome, string ownership)
        {
            if (shooter == null) return;
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            Job job = shooter.CurJob;
            friendlyFireAuthorizations[shooter.thingIDNumber] =
                new CAFriendlyFireAuthorizationDiagnosticState(now, phase,
                    outcome, ownership, job != null ? job.loadID : -1,
                    job?.def?.defName, target != null
                        ? target.thingIDNumber : -1,
                    blocker != null ? blocker.thingIDNumber : -1, laneRisk);
            if (outcome == "authorized-protected") return;
            int next;
            if (nextFriendlyFireTraceTicks.TryGetValue(
                    shooter.thingIDNumber, out next) && now < next) return;
            nextFriendlyFireTraceTicks[shooter.thingIDNumber] = now + 60;
            bool impactEnvelope = outcome != null
                && outcome.Contains("impact-envelope");
            CATrace.Pawn(shooter,
                (phase == "warmup-recheck"
                    ? "autonomous ranged warmup INTERRUPTS"
                    : "autonomous ranged fire HOLDS")
                + " because "
                + (blocker != null ? blocker.LabelShort : "a non-hostile")
                + (impactEnvelope
                    ? " occupies the harmful impact/miss envelope (risk "
                    : " occupies the current firing corridor (lane risk ")
                + laneRisk.ToString("F2") + ")",
                target: target,
                contact: blocker != null
                    ? (IntVec3?)blocker.Position : null,
                anchor: shooter.Position);
        }

        internal static bool TryGetFriendlyFireAuthorizationDiagnostic(
            Pawn pawn, out CAFriendlyFireAuthorizationDiagnosticState state)
        {
            state = default(CAFriendlyFireAuthorizationDiagnosticState);
            return pawn != null && friendlyFireAuthorizations.TryGetValue(
                pawn.thingIDNumber, out state);
        }

        private static bool TryFindSaferCell(Pawn pawn, IntVec3 threat,
            out IntVec3 result)
        {
            result = IntVec3.Invalid;
            if (pawn == null || pawn.Map == null || !threat.IsValid) return false;
            Map map = pawn.Map;
            CASpatialCombatMemoryMapComponent.For(map)?.ObserveNow(pawn);
            float currentDistance = pawn.Position.DistanceTo(threat);
            float currentCover = CoverUtility.CalculateOverallBlockChance(
                pawn.Position, threat, map);
            float currentHazard = CACombatIntent.HazardScore(pawn, pawn.Position);
            bool currentWater = pawn.Position.GetTerrain(map).IsWater;
            EscapeSafetyContext safety = BuildEscapeSafetyContext(pawn);
            float currentHostilePressure = EscapeThreatPressure(pawn,
                pawn.Position, threat, safety);
            float currentFriendlySectorRisk = FriendlySectorRisk(
                pawn.Position, safety.activeFriendlySectors);
            float currentSupport = RecoveryMutualSupport(pawn,
                pawn.Position, safety);
            float currentShelter = StructuralRecoveryShelter(pawn,
                pawn.Position, threat, safety);
            int currentExits = EscapeExitCount(pawn, pawn.Position, threat,
                safety);
            var currentAssessment = new EscapeRouteCandidate
            {
                cell = pawn.Position,
                destinationScore = EscapeDestinationScore(pawn, pawn.Position,
                    currentDistance, currentCover, currentHazard,
                    currentHostilePressure, currentFriendlySectorRisk,
                    currentSupport, currentShelter, currentExits, safety),
                hostilePressure = currentHostilePressure,
                friendlySectorRisk = currentFriendlySectorRisk,
                mutualSupport = currentSupport,
                structuralShelter = currentShelter,
                pathCells = 1,
                exposedCells = currentHostilePressure >= 0.25f ? 1 : 0,
                maxHazard = currentHazard,
                maxHostilePressure = currentHostilePressure,
                cumulativeHostilePressure = currentHostilePressure,
                friendlySectorCells = currentFriendlySectorRisk
                    >= FriendlySectorUnsafeRisk ? 1 : 0,
                maxFriendlySectorRisk = currentFriendlySectorRisk,
                shelteredRouteCells = currentShelter >= 0.55f ? 1 : 0
            };
            EscapeRouteCandidate assessedCurrent;
            if (TryAssessEscapeRoute(pawn, pawn.Position, threat, safety,
                    out assessedCurrent) && assessedCurrent.pathCells > 0)
            {
                assessedCurrent.destinationScore =
                    currentAssessment.destinationScore;
                assessedCurrent.hostilePressure = currentHostilePressure;
                assessedCurrent.friendlySectorRisk =
                    currentFriendlySectorRisk;
                assessedCurrent.mutualSupport = currentSupport;
                assessedCurrent.structuralShelter = currentShelter;
                currentAssessment = assessedCurrent;
            }
            var shortlist = new List<EscapeRouteCandidate>();
            int limit = GenRadial.NumCellsInRadius(12f);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = pawn.Position + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map) || !cell.WalkableBy(map, pawn)
                    || !cell.InAllowedArea(pawn) || cell.ContainsStaticFire(map)
                    || !CASpatialCombatMemoryMapComponent.KnowsCell(pawn, cell)
                    || !map.pawnDestinationReservationManager.CanReserve(cell, pawn)
                    || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)
                    || CellOccupiedByOtherPawn(cell, map, pawn)
                    || CACombatIntent.IsRecentlyUnsafe(pawn, cell)) continue;
                if (!currentWater && cell.GetTerrain(map).IsWater) continue;
                float distance = cell.DistanceTo(threat);
                float cover = CoverUtility.CalculateOverallBlockChance(cell, threat, map);
                float hazard = CACombatIntent.HazardScore(pawn, cell);
                float hostilePressure = EscapeThreatPressure(pawn, cell,
                    threat, safety);
                float friendlySectorRisk = FriendlySectorRisk(cell,
                    safety.activeFriendlySectors);
                float support = RecoveryMutualSupport(pawn, cell, safety);
                float shelter = StructuralRecoveryShelter(pawn, cell,
                    threat, safety);
                int exits = EscapeExitCount(pawn, cell, threat, safety);
                if (exits < 2) continue;
                // Moving away from one source is not safer if it materially worsens
                // exposure to another visible or remembered hostile. Likewise, do not
                // enter a friendly sector from a presently clear cell.
                if (hostilePressure > currentHostilePressure + 0.12f)
                    continue;
                if (currentFriendlySectorRisk < FriendlySectorUnsafeRisk
                    && friendlySectorRisk >= FriendlySectorUnsafeRisk)
                    continue;
                bool supportImproves = support >= currentSupport + 0.12f;
                bool shelterImproves = shelter >= currentShelter + 0.12f;
                if (distance < currentDistance + 1.5f
                    && cover < currentCover + 0.15f
                    && hazard > currentHazard - 0.12f
                    && !supportImproves && !shelterImproves) continue;
                float score = EscapeDestinationScore(pawn, cell, distance,
                    cover, hazard, hostilePressure, friendlySectorRisk,
                    support, shelter, exits, safety);
                AddEscapeShortlist(shortlist, new EscapeRouteCandidate
                {
                    cell = cell,
                    destinationScore = score,
                    hostilePressure = hostilePressure,
                    friendlySectorRisk = friendlySectorRisk,
                    mutualSupport = support,
                    structuralShelter = shelter
                }, 12);
            }

            // Holding is a real candidate. Recovery must not turn the least-bad
            // movement option into an order when the actor's learned wall pocket,
            // mutual support, and current firing-sector exposure are already safer.
            int bestFriendlySectorCells = currentAssessment.friendlySectorCells;
            float bestHostileRouteDanger = EscapeRouteHostileDanger(
                currentAssessment);
            float bestScore = EscapeRouteScore(currentAssessment);
            for (int i = 0; i < shortlist.Count; i++)
            {
                EscapeRouteCandidate candidate = shortlist[i];
                float destinationScore = candidate.destinationScore;
                float destinationPressure = candidate.hostilePressure;
                float destinationSectorRisk = candidate.friendlySectorRisk;
                float destinationSupport = candidate.mutualSupport;
                float destinationShelter = candidate.structuralShelter;
                if (!TryAssessEscapeRoute(pawn, candidate.cell, threat,
                    safety, out candidate)) continue;
                candidate.destinationScore = destinationScore;
                candidate.hostilePressure = destinationPressure;
                candidate.friendlySectorRisk = destinationSectorRisk;
                candidate.mutualSupport = destinationSupport;
                candidate.structuralShelter = destinationShelter;
                float routeScore = EscapeRouteScore(candidate);
                float hostileRouteDanger = EscapeRouteHostileDanger(candidate);
                if (candidate.friendlySectorCells
                        > bestFriendlySectorCells
                    || candidate.friendlySectorCells
                            == bestFriendlySectorCells
                        && hostileRouteDanger
                            > bestHostileRouteDanger + 0.01f
                    || candidate.friendlySectorCells
                            == bestFriendlySectorCells
                        && Mathf.Abs(hostileRouteDanger
                            - bestHostileRouteDanger) <= 0.01f
                        && routeScore <= bestScore + 0.08f) continue;
                bestFriendlySectorCells = candidate.friendlySectorCells;
                bestHostileRouteDanger = hostileRouteDanger;
                bestScore = routeScore;
                result = candidate.cell;
            }
            return result.IsValid;
        }

        private static float EscapeDestinationScore(Pawn pawn, IntVec3 cell,
            float distance, float cover, float hazard, float hostilePressure,
            float friendlySectorRisk, float support, float shelter, int exits,
            EscapeSafetyContext safety)
        {
            float travelPenalty = Mathf.Lerp(0.16f, 0.06f,
                safety.actorCondition.CombatPower);
            return distance * 0.16f + cover * 4f
                - pawn.Position.DistanceTo(cell) * travelPenalty
                - hazard * 6f - hostilePressure * 7f
                - friendlySectorRisk * 8f
                + Mathf.Min(exits, 5) * 0.18f
                + support * 3.20f + shelter * 2.60f;
        }

        private static float EscapeRouteScore(EscapeRouteCandidate candidate)
        {
            return candidate.destinationScore
                - candidate.exposedCells * 0.42f
                - candidate.cumulativeHostilePressure * 0.55f
                - candidate.maxHostilePressure * 1.50f
                - candidate.pathCells * 0.018f
                - candidate.waterEntries * 1.25f
                - candidate.maxHazard * 5f
                - candidate.friendlySectorCells * 0.70f
                - candidate.maxFriendlySectorRisk * 1.25f
                + candidate.shelteredRouteCells * 0.10f;
        }

        private static float EscapeRouteHostileDanger(
            EscapeRouteCandidate candidate)
        {
            return candidate.maxHostilePressure * 2f
                + candidate.cumulativeHostilePressure * 0.35f
                + candidate.exposedCells * 0.20f;
        }

        private static void AddEscapeShortlist(
            List<EscapeRouteCandidate> shortlist,
            EscapeRouteCandidate candidate, int capacity)
        {
            if (shortlist.Count < capacity)
            {
                shortlist.Add(candidate);
                return;
            }
            int worst = 0;
            for (int i = 1; i < shortlist.Count; i++)
                if (shortlist[i].destinationScore
                    < shortlist[worst].destinationScore) worst = i;
            if (candidate.destinationScore > shortlist[worst].destinationScore)
                shortlist[worst] = candidate;
        }

        private static bool TryAssessEscapeRoute(Pawn pawn, IntVec3 destination,
            IntVec3 threat, out EscapeRouteCandidate assessment)
        {
            return TryAssessEscapeRoute(pawn, destination, threat,
                BuildEscapeSafetyContext(pawn), out assessment);
        }

        private static bool TryAssessEscapeRoute(Pawn pawn, IntVec3 destination,
            IntVec3 threat, EscapeSafetyContext safety,
            out EscapeRouteCandidate assessment)
        {
            assessment = new EscapeRouteCandidate { cell = destination };
            using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                pawn.Position, destination, pawn,
                PathFinderCostTuning.For(pawn), PathEndMode.OnCell))
            {
                if (path == null || !path.Found) return false;
                List<IntVec3> nodes = path.NodesReversed;
                bool previousWater = pawn.Position.GetTerrain(pawn.Map).IsWater;
                var previousSectorRisks = new float[
                    safety.activeFriendlySectors.Count];
                for (int sector = 0;
                    sector < safety.activeFriendlySectors.Count; sector++)
                    previousSectorRisks[sector] = safety
                        .activeFriendlySectors[sector].RiskAt(pawn.Position);
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    IntVec3 cell = nodes[i];
                    if (!cell.InBounds(pawn.Map)) continue;
                    if (!CASpatialCombatMemoryMapComponent.KnowsCell(pawn,
                            cell)) return false;
                    for (int sector = 0;
                        sector < safety.activeFriendlySectors.Count; sector++)
                    {
                        float nextRisk = safety.activeFriendlySectors[sector]
                            .RiskAt(cell);
                        if (!FriendlySectorStepAllowed(
                                previousSectorRisks[sector], nextRisk))
                            return false;
                        previousSectorRisks[sector] = nextRisk;
                    }
                    assessment.pathCells++;
                    bool water = cell.GetTerrain(pawn.Map).IsWater;
                    if (water && !previousWater) assessment.waterEntries++;
                    previousWater = water;
                    assessment.maxHazard = Mathf.Max(assessment.maxHazard,
                        CACombatIntent.HazardScore(pawn, cell));
                    float hostilePressure = EscapeThreatPressure(pawn, cell,
                        threat, safety);
                    assessment.maxHostilePressure = Mathf.Max(
                        assessment.maxHostilePressure, hostilePressure);
                    assessment.cumulativeHostilePressure += hostilePressure;
                    if (hostilePressure >= 0.25f)
                        assessment.exposedCells++;
                    float sectorRisk = FriendlySectorRisk(cell,
                        safety.activeFriendlySectors);
                    assessment.maxFriendlySectorRisk = Mathf.Max(
                        assessment.maxFriendlySectorRisk, sectorRisk);
                    if (sectorRisk >= FriendlySectorUnsafeRisk)
                        assessment.friendlySectorCells++;
                    if (StructuralRecoveryShelter(pawn, cell, threat,
                            safety) >= 0.55f)
                        assessment.shelteredRouteCells++;
                }
                return true;
            }
        }

        private static EscapeSafetyContext BuildEscapeSafetyContext(Pawn pawn)
        {
            var context = new EscapeSafetyContext();
            if (pawn == null || pawn.Map == null) return context;
            context.actorCondition = CACombatConditionSnapshot.Capture(pawn);
            context.activeFriendlySectors.AddRange(
                ActiveFriendlyFireSectors(pawn));
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn hostile = pawns[i];
                if (hostile == null || hostile == pawn || hostile.Dead)
                    continue;
                if (pawn.HostileTo(hostile))
                {
                    if (hostile.Downed
                        || !KnowledgeMapComponent.CanCurrentlySeeHostile(
                            pawn, hostile)) continue;
                    context.visibleHostiles.Add(hostile);
                    context.visibleHostileIds.Add(hostile.thingIDNumber);
                    context.combatPower[hostile.thingIDNumber] =
                        CACombatConditionSnapshot.Capture(hostile).CombatPower;
                    continue;
                }
                if (hostile.Faction != pawn.Faction || hostile.Downed
                    || !hostile.Awake() || hostile.InMentalState
                    || hostile.WorkTagIsDisabled(WorkTags.Violent)
                    || !GenSight.LineOfSight(pawn.Position, hostile.Position,
                        pawn.Map, true)) continue;
                context.visibleAllies.Add(hostile);
                context.combatPower[hostile.thingIDNumber] =
                    CACombatConditionSnapshot.Capture(hostile).CombatPower;
            }
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
            if (knowledge != null)
                context.knownHostiles.AddRange(knowledge.FreshContacts(pawn));
            return context;
        }

        private static float EscapeThreatPressure(Pawn pawn, IntVec3 cell,
            IntVec3 primaryThreat, EscapeSafetyContext context)
        {
            if (pawn == null || pawn.Map == null || !cell.InBounds(pawn.Map))
                return 1f;
            float pressure = 0f;
            bool primaryRepresented = false;
            List<Pawn> visibleHostiles = context != null
                ? context.visibleHostiles : null;
            if (visibleHostiles == null) visibleHostiles = new List<Pawn>();
            for (int i = 0; i < visibleHostiles.Count; i++)
            {
                Pawn hostile = visibleHostiles[i];
                if (hostile == null || hostile.Dead || hostile.Downed) continue;
                float power;
                if (context == null || !context.combatPower.TryGetValue(
                        hostile.thingIDNumber, out power))
                    power = CACombatConditionSnapshot.Capture(hostile)
                        .CombatPower;
                if (hostile.Position.InHorDistOf(primaryThreat, 1.5f))
                    primaryRepresented = true;
                float distance = hostile.Position.DistanceTo(cell);
                float cover = CoverUtility.CalculateOverallBlockChance(cell,
                    hostile.Position, pawn.Map);
                List<Verb> hostileVerbs = MaterializedThreatVerbs(hostile);
                if (hostileVerbs.Count == 0)
                {
                    Verb fallback = hostile.TryGetAttackVerb(pawn,
                        allowManualCastWeapons: false);
                    if (fallback != null) hostileVerbs.Add(fallback);
                }
                float contribution = 0f;
                if (hostileVerbs.Count == 0 && distance <= 5.9f)
                    contribution = 0.18f + (1f - cover) * 0.30f;
                for (int verbIndex = 0; verbIndex < hostileVerbs.Count;
                    verbIndex++)
                {
                    Verb hostileVerb = hostileVerbs[verbIndex];
                    bool canAffect = hostileVerb.verbProps.IsMeleeAttack
                        ? distance <= 5.9f
                        : hostileVerb.CanHitTargetFrom(hostile.Position,
                            new LocalTargetInfo(cell));
                    if (!canAffect) continue;
                    float proximity = 1f - Mathf.Clamp01(distance
                        / Mathf.Max(6f, hostileVerb.EffectiveRange));
                    contribution = Mathf.Max(contribution,
                        0.18f + (1f - cover) * 0.30f
                        + proximity * 0.20f);
                }
                if (contribution <= 0f) continue;
                pressure += contribution * power;
                if (distance <= 5f) pressure += 0.22f * power;
            }
            List<ThreatContactSnapshot> known = context != null
                ? context.knownHostiles : null;
            if (known != null)
                for (int i = 0; i < known.Count; i++)
                {
                    ThreatContactSnapshot contact = known[i];
                    if (context.visibleHostileIds.Contains(contact.HostileId))
                        continue;
                    if (contact.Cell.InHorDistOf(primaryThreat, 1.5f))
                        primaryRepresented = true;
                    pressure += RememberedThreatPressure(pawn, cell, contact);
                }
            if (!primaryRepresented && primaryThreat.IsValid
                && primaryThreat.InBounds(pawn.Map)
                && GenSight.LineOfSight(cell, primaryThreat, pawn.Map, true))
            {
                float cover = CoverUtility.CalculateOverallBlockChance(cell,
                    primaryThreat, pawn.Map);
                pressure += 0.20f + (1f - cover) * 0.30f;
            }
            // Preserve additive pressure. Clamping made two distinct multi-shooter
            // positions both read as 1.0, hiding material worsening and improvement.
            return pressure;
        }

        private static float RememberedThreatPressure(Pawn pawn, IntVec3 cell,
            ThreatContactSnapshot contact)
        {
            if (pawn == null || pawn.Map == null || !contact.Cell.IsValid
                || !contact.Cell.InBounds(pawn.Map)
                || contact.State != ThreatContactState.Active) return 0f;
            int age = Mathf.Max(0, Find.TickManager.TicksGame
                - contact.SourceTick);
            float freshness = 1f - Mathf.Clamp01(age
                / (float)KnownThreatFreshTicks);
            float reliability = Mathf.Clamp01(contact.Evidence.Confidence
                * Mathf.Lerp(0.30f, 1f, freshness)
                / (1f + contact.Evidence.Uncertainty / 10f));
            if (reliability <= 0.04f) return 0f;
            float distance = contact.Cell.DistanceTo(cell);
            bool closeThreat = contact.WeaponCategory == ContactWeaponCategory.Melee
                || contact.WeaponCategory == ContactWeaponCategory.Unarmed;
            float range = closeThreat ? 5.9f
                : contact.WeaponCategory == ContactWeaponCategory.Ranged
                    ? 42f : 30f;
            if (distance > range) return 0f;
            if (!closeThreat && !GenSight.LineOfSight(contact.Cell, cell,
                    pawn.Map, true)) return 0f;
            float cover = CoverUtility.CalculateOverallBlockChance(cell,
                contact.Cell, pawn.Map);
            float proximity = 1f - Mathf.Clamp01(distance
                / Mathf.Max(6f, range));
            float contribution = (0.16f + (1f - cover) * 0.28f
                + proximity * 0.18f) * reliability;
            if (distance <= 5f) contribution += 0.20f * reliability;
            return contribution;
        }

        private static float RecoveryMutualSupport(Pawn pawn, IntVec3 cell,
            EscapeSafetyContext context)
        {
            if (pawn == null || pawn.Map == null || context == null)
                return 0f;
            float support = 0f;
            for (int i = 0; i < context.visibleAllies.Count; i++)
            {
                Pawn ally = context.visibleAllies[i];
                if (ally == null || ally.Dead || ally.Downed
                    || !GenSight.LineOfSight(cell, ally.Position, pawn.Map,
                        true)) continue;
                float distance = cell.DistanceTo(ally.Position);
                if (distance > 14f) continue;
                float power;
                if (!context.combatPower.TryGetValue(ally.thingIDNumber,
                        out power)) power = 0.5f;
                float band = distance < 2f ? 0.16f
                    : distance <= 8f ? 0.50f
                    : Mathf.Lerp(0.50f, 0.12f,
                        Mathf.Clamp01((distance - 8f) / 6f));
                support += band * power;
            }
            return Mathf.Clamp01(support);
        }

        private static float StructuralRecoveryShelter(Pawn pawn, IntVec3 cell,
            IntVec3 primaryThreat, EscapeSafetyContext context)
        {
            if (pawn == null || pawn.Map == null || !cell.InBounds(pawn.Map))
                return 0f;
            float threatShelter = 0f;
            int threatCount = 0;
            if (context != null)
            {
                for (int i = 0; i < context.visibleHostiles.Count; i++)
                {
                    Pawn hostile = context.visibleHostiles[i];
                    if (hostile == null || hostile.Dead || hostile.Downed)
                        continue;
                    threatShelter += ShelterFrom(cell, hostile.Position,
                        pawn.Map);
                    threatCount++;
                }
                for (int i = 0; i < context.knownHostiles.Count; i++)
                {
                    ThreatContactSnapshot known = context.knownHostiles[i];
                    if (context.visibleHostileIds.Contains(known.HostileId)
                        || !known.Cell.IsValid || !known.Cell.InBounds(pawn.Map))
                        continue;
                    threatShelter += ShelterFrom(cell, known.Cell, pawn.Map)
                        * Mathf.Clamp01(known.Evidence.Confidence);
                    threatCount++;
                }
            }
            if (threatCount == 0 && primaryThreat.IsValid
                && primaryThreat.InBounds(pawn.Map))
            {
                threatShelter = ShelterFrom(cell, primaryThreat, pawn.Map);
                threatCount = 1;
            }
            float directional = threatCount > 0
                ? threatShelter / threatCount : 0f;

            int probes = 0;
            int open = 0;
            for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                IntVec3 probe = cell + GenAdj.AdjacentCells[i] * 4;
                if (!probe.InBounds(pawn.Map)
                    || !CASpatialCombatMemoryMapComponent.KnowsCell(pawn,
                        probe)) continue;
                probes++;
                if (GenSight.LineOfSight(cell, probe, pawn.Map, true)) open++;
            }
            float enclosure = probes > 0 ? 1f - open / (float)probes : 0f;
            return Mathf.Clamp01(directional * 0.72f + enclosure * 0.28f);
        }

        private static float ShelterFrom(IntVec3 cell, IntVec3 threat,
            Map map)
        {
            if (!GenSight.LineOfSight(cell, threat, map, true)) return 1f;
            return CoverUtility.CalculateOverallBlockChance(cell, threat, map);
        }

        private static List<Verb> MaterializedThreatVerbs(Pawn pawn)
        {
            var result = new List<Verb>();
            Stance_Busy main = pawn?.stances?.curStance as Stance_Busy;
            if (main?.verb != null) result.Add(main.verb);
            Pawn_StanceTracker offhandTracker;
            Stance_Busy offhand = OffhandComponent.TryGetExistingTracker(pawn,
                    out offhandTracker)
                ? offhandTracker.curStance as Stance_Busy : null;
            if (offhand?.verb != null && !result.Contains(offhand.verb))
                result.Add(offhand.verb);
            Verb jobVerb = pawn?.CurJob?.verbToUse;
            if (jobVerb != null && !result.Contains(jobVerb))
                result.Add(jobVerb);
            return result;
        }

        private static int EscapeExitCount(Pawn pawn, IntVec3 cell,
            IntVec3 threat, EscapeSafetyContext safety)
        {
            int exits = 0;
            float currentDistance = cell.DistanceTo(threat);
            float currentPressure = EscapeThreatPressure(pawn, cell,
                threat, safety);
            for (int i = 0; i < GenAdj.AdjacentCellsAndInside.Length; i++)
            {
                IntVec3 next = cell + GenAdj.AdjacentCellsAndInside[i];
                if (next == cell || !next.InBounds(pawn.Map)
                    || !next.WalkableBy(pawn.Map, pawn)
                    || !next.InAllowedArea(pawn) || next.ContainsStaticFire(pawn.Map)
                    || CACombatIntent.HazardScore(pawn, next) >= 0.55f
                    || next.DistanceTo(threat) + 0.25f < currentDistance) continue;
                float nextPressure = EscapeThreatPressure(pawn, next,
                    threat, safety);
                if (nextPressure > currentPressure + 0.12f) continue;
                bool sectorStepAllowed = true;
                for (int sector = 0;
                    sector < safety.activeFriendlySectors.Count; sector++)
                {
                    CAFriendlyFireSectorSnapshot active =
                        safety.activeFriendlySectors[sector];
                    if (FriendlySectorStepAllowed(active.RiskAt(cell),
                            active.RiskAt(next))) continue;
                    sectorStepAllowed = false;
                    break;
                }
                if (!sectorStepAllowed) continue;
                exits++;
            }
            return exits;
        }

        private static string EscapeTopologyText(Pawn pawn, IntVec3 cell,
            IntVec3 threat)
        {
            if (pawn == null || pawn.Map == null || !cell.InBounds(pawn.Map))
                return "escape topology unavailable";
            EscapeSafetyContext safety = BuildEscapeSafetyContext(pawn);
            EscapeRouteCandidate route;
            bool hasRoute = TryAssessEscapeRoute(pawn, cell, threat,
                safety, out route);
            return "escape exits " + EscapeExitCount(pawn, cell, threat,
                    safety)
                + ", water " + (cell.GetTerrain(pawn.Map).IsWater ? "yes" : "no")
                + ", cover " + CoverUtility.CalculateOverallBlockChance(
                    cell, threat, pawn.Map).ToString("F2")
                + ", hazard " + CACombatIntent.HazardScore(pawn, cell).ToString("F2")
                + ", known threats " + safety.knownHostiles.Count
                + ", mutual support "
                + RecoveryMutualSupport(pawn, cell, safety).ToString("F2")
                + ", structural shelter "
                + StructuralRecoveryShelter(pawn, cell, threat, safety)
                    .ToString("F2")
                + (hasRoute ? ", route cells " + route.pathCells
                    + ", exposed cells " + route.exposedCells
                    + ", route hostile max "
                    + route.maxHostilePressure.ToString("F2")
                    + ", route hostile integral "
                    + route.cumulativeHostilePressure.ToString("F2")
                    + ", friendly-sector cells "
                    + route.friendlySectorCells
                    + ", route friendly-sector max "
                    + route.maxFriendlySectorRisk.ToString("F2")
                    + ", water entries " + route.waterEntries
                    + ", sheltered route cells " + route.shelteredRouteCells
                    + ", route max hazard " + route.maxHazard.ToString("F2")
                    : ", route unavailable");
        }

        private static bool TryRecentHarm(Pawn pawn, out HarmEpisode episode)
        {
            episode = default(HarmEpisode);
            if (pawn == null || pawn.Map == null
                || !recentHarm.TryGetValue(pawn.thingIDNumber, out episode))
                return false;
            int age = Find.TickManager.TicksGame - episode.tick;
            if (episode.mapId == pawn.Map.uniqueID && age >= 0
                && age <= RecentHarmTicks) return true;
            recentHarm.Remove(pawn.thingIDNumber);
            return false;
        }

        internal static bool TryVisibleHarmInstigator(Pawn pawn, out Pawn target)
        {
            target = null;
            HarmEpisode harm;
            if (!TryRecentHarm(pawn, out harm) || harm.instigatorId < 0)
                return false;

            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == null || candidate.thingIDNumber != harm.instigatorId
                    || !pawn.HostileTo(candidate)) continue;
                if (!KnowledgeMapComponent.CanCurrentlySeeHostile(
                    pawn, candidate)) return false;
                target = candidate;
                return true;
            }
            return false;
        }

        internal static Pawn ImmediateVisibleTarget(Pawn pawn, Pawn knownVisibleTarget)
        {
            Pawn impactTarget;
            if (TryVisibleHarmInstigator(pawn, out impactTarget)) return impactTarget;
            // During a fresh impact episode, do not evaluate the ability to engage
            // some older visible contact against pressure arriving from an unseen
            // source. If the actual instigator is not visible, break exposure first.
            return HarmedRecently(pawn) ? null : knownVisibleTarget;
        }

        private static CACombatCellAssessment InvalidCellAssessment()
        {
            return new CACombatCellAssessment { cell = IntVec3.Invalid };
        }

        private static Pawn SpawnedPawnById(Map map, int id)
        {
            if (map == null || id <= 0) return null;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i] != null && pawns[i].thingIDNumber == id)
                    return pawns[i];
            return null;
        }

        private static int CombatSkill(Pawn pawn, bool ranged)
        {
            if (pawn == null || pawn.skills == null) return 0;
            return pawn.skills.GetSkill(ranged
                ? SkillDefOf.Shooting : SkillDefOf.Melee).Level;
        }

        internal static bool AllowManualCombatVerb(Pawn pawn)
        {
            return pawn != null && !pawn.IsColonist && !pawn.IsColonySubhuman;
        }

        private static Job PositionJob(Pawn pawn, IntVec3 cell)
        {
            return MakeLockedCombatMoveJob(pawn, cell, 240, null);
        }

        internal static Job MakeLockedCombatMoveJob(Pawn pawn, IntVec3 cell,
            int expiryInterval, IList<IntVec3> assessedRoute)
        {
            if (pawn == null || pawn.Map == null || !cell.IsValid)
                return null;
            Job job = JobMaker.MakeJob(CA_Defs.CombatMove, cell);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.expiryInterval = Mathf.Max(1, expiryInterval);
            job.checkOverrideOnExpire = true;
            job.targetQueueA = new List<LocalTargetInfo>();
            if (!TryBuildLockedCombatRoute(pawn, cell, assessedRoute,
                    job.targetQueueA)) return null;
            return job;
        }

        internal static bool EnsureLockedCombatRoute(Pawn pawn, Job job)
        {
            if (pawn == null || job == null || job.def != CA_Defs.CombatMove
                || !job.targetA.IsValid) return false;
            if (!job.targetQueueA.NullOrEmpty()) return true;
            if (pawn.Position == job.targetA.Cell)
            {
                if (job.targetQueueA == null)
                    job.targetQueueA = new List<LocalTargetInfo>();
                return true;
            }
            var route = new List<LocalTargetInfo>();
            if (!TryBuildLockedCombatRoute(pawn, job.targetA.Cell, null,
                    route)) return false;
            job.targetQueueA = route;
            return true;
        }

        private static bool TryBuildLockedCombatRoute(Pawn pawn,
            IntVec3 destination, IList<IntVec3> assessedRoute,
            List<LocalTargetInfo> result)
        {
            if (pawn == null || pawn.Map == null || result == null
                || !destination.IsValid || !destination.InBounds(pawn.Map))
                return false;
            var steps = new List<IntVec3>();
            if (assessedRoute != null)
            {
                for (int i = 0; i < assessedRoute.Count; i++)
                    steps.Add(assessedRoute[i]);
            }
            else
            {
                using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                    pawn.Position, destination, pawn,
                    PathFinderCostTuning.For(pawn),
                    PathEndMode.OnCell))
                {
                    if (path == null || !path.Found
                        || path.NodesReversed == null) return false;
                    List<IntVec3> nodes = path.NodesReversed;
                    for (int i = nodes.Count - 1; i >= 0; i--)
                        if (nodes[i] != pawn.Position) steps.Add(nodes[i]);
                }
            }

            IntVec3 previous = pawn.Position;
            for (int i = 0; i < steps.Count; i++)
            {
                IntVec3 step = steps[i];
                if (step == previous) continue;
                if (!step.IsValid || !step.InBounds(pawn.Map)
                    || !previous.AdjacentTo8WayOrInside(step)
                    || !step.WalkableBy(pawn.Map, pawn)
                    || !step.InAllowedArea(pawn)) return false;
                result.Add(new LocalTargetInfo(step));
                previous = step;
            }
            return previous == destination;
        }

        private static void StampTacticalOwner(Pawn pawn, Job job)
        {
            if (pawn == null || job == null
                || (!CATactical.IsHold(pawn)
                    && !CATactical.IsAutomaticDefense(pawn))) return;
            Lord lord = pawn.GetLord();
            if (lord != null && lord.LordJob is LordJob_CATactical)
                job.lord = lord;
        }

        private static bool CellOccupiedByOtherPawn(IntVec3 cell, Map map,
            Pawn actor)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn && things[i] != actor) return true;
            return false;
        }

        private static int CellOrder(IntVec3 a, IntVec3 b)
        {
            int x = a.x.CompareTo(b.x);
            return x != 0 ? x : a.z.CompareTo(b.z);
        }
    }

    // HumanlikeConstant deliberately omits drafted pawns, but drafted is a control
    // posture rather than a command to remain tactically inert. This narrow pump owns
    // bounded drafted initiative between weapon cycles and the every-tick deadline for
    // an already observed incoming explosive. It never toggles Moving Fire, invents a
    // target, or supersedes current or queued player-forced work.
    public class CADraftedCombatInitiativeMapComponent : MapComponent
    {
        private static readonly Dictionary<int, int> watchDiagnosticTicks =
            new Dictionary<int, int>();
        private Dictionary<int, int> draftedResponseJobIds =
            new Dictionary<int, int>();
        // Explicit, save-stable ownership for CA jobs started outside a think
        // giver (currently synchronized ambush release). Native AttackStatic is
        // shared by the game and other mods, so its def alone is not provenance.
        private Dictionary<int, int> autonomousRangedJobIds =
            new Dictionary<int, int>();
        private Dictionary<int, int> supportTargetIds =
            new Dictionary<int, int>();
        private Dictionary<int, int> supportSourceTicks =
            new Dictionary<int, int>();
        private Dictionary<int, int> supportCooldownUntilTicks =
            new Dictionary<int, int>();
        private Dictionary<int, IntVec3> supportAnchors =
            new Dictionary<int, IntVec3>();
        private Dictionary<int, IntVec3> supportTargetCells =
            new Dictionary<int, IntVec3>();
        private Dictionary<int, int> supportAttemptJobIds =
            new Dictionary<int, int>();
        private Dictionary<int, IntVec3> supportAttemptOrigins =
            new Dictionary<int, IntVec3>();
        private Dictionary<int, CAReportedSupportEvaluation>
            supportEvaluations =
                new Dictionary<int, CAReportedSupportEvaluation>();
        public CADraftedCombatInitiativeMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref draftedResponseJobIds,
                "CA_draftedResponseJobIds", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref autonomousRangedJobIds,
                "CA_autonomousRangedJobIds", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref supportTargetIds,
                "CA_supportTargetIds", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref supportSourceTicks,
                "CA_supportSourceTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref supportCooldownUntilTicks,
                "CA_supportCooldownUntilTicks", LookMode.Value,
                LookMode.Value);
            Scribe_Collections.Look(ref supportAnchors,
                "CA_supportAnchors", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref supportTargetCells,
                "CA_supportTargetCells", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref supportAttemptJobIds,
                "CA_supportAttemptJobIds", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref supportAttemptOrigins,
                "CA_supportAttemptOrigins", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref supportEvaluations,
                "CA_supportEvaluations", LookMode.Value, LookMode.Deep);
            if (draftedResponseJobIds == null)
                draftedResponseJobIds = new Dictionary<int, int>();
            if (autonomousRangedJobIds == null)
                autonomousRangedJobIds = new Dictionary<int, int>();
            if (supportTargetIds == null)
                supportTargetIds = new Dictionary<int, int>();
            if (supportSourceTicks == null)
                supportSourceTicks = new Dictionary<int, int>();
            if (supportCooldownUntilTicks == null)
                supportCooldownUntilTicks = new Dictionary<int, int>();
            if (supportAnchors == null)
                supportAnchors = new Dictionary<int, IntVec3>();
            if (supportTargetCells == null)
                supportTargetCells = new Dictionary<int, IntVec3>();
            if (supportAttemptJobIds == null)
                supportAttemptJobIds = new Dictionary<int, int>();
            if (supportAttemptOrigins == null)
                supportAttemptOrigins = new Dictionary<int, IntVec3>();
            if (supportEvaluations == null)
                supportEvaluations =
                    new Dictionary<int, CAReportedSupportEvaluation>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            CACombatIntent.RebuildIncomingProjectiles(map);
        }

        internal void RegisterAutonomousRangedJob(Pawn pawn, Job job)
        {
            if (pawn == null || job == null || pawn.Map != map
                || job.def != JobDefOf.AttackStatic) return;
            autonomousRangedJobIds[pawn.thingIDNumber] = job.loadID;
        }

        internal bool OwnsAutonomousRangedJob(Pawn pawn, Job job)
        {
            if (pawn == null || job == null) return false;
            int jobId;
            return autonomousRangedJobIds.TryGetValue(
                    pawn.thingIDNumber, out jobId)
                && jobId == job.loadID;
        }

        internal bool TryGetSupportEnvelope(Pawn pawn, int targetId,
            int sourceTick, IntVec3 targetCell, out IntVec3 anchor,
            out string rejection)
        {
            anchor = IntVec3.Invalid;
            rejection = null;
            if (pawn == null || targetId <= 0 || sourceTick < 0
                || !targetCell.IsValid)
            {
                rejection = "invalid-support-envelope-input";
                return false;
            }
            int id = pawn.thingIDNumber;
            if (supportAttemptJobIds.ContainsKey(id))
            {
                rejection = "support-attempt-already-in-progress";
                return false;
            }
            int existingTarget;
            bool hasEpisode = supportTargetIds.TryGetValue(id,
                out existingTarget);
            int now = Find.TickManager.TicksGame;
            int until;
            if (hasEpisode && supportCooldownUntilTicks.TryGetValue(id,
                    out until) && now < until)
            {
                rejection = "support-envelope-cooldown";
                return false;
            }
            if (hasEpisode && existingTarget == targetId)
            {
                int handledSource;
                if (supportSourceTicks.TryGetValue(id, out handledSource)
                    && sourceTick <= handledSource)
                {
                    rejection = "contact-source-already-handled";
                    return false;
                }
                IntVec3 handledCell;
                if (supportTargetCells.TryGetValue(id, out handledCell)
                    && handledCell.IsValid
                    && targetCell.InHorDistOf(handledCell, 4.9f))
                {
                    rejection = "contact-cell-still-inside-handled-envelope";
                    return false;
                }
                if (!supportAnchors.TryGetValue(id, out anchor)
                    || !anchor.IsValid || !anchor.InBounds(map))
                    anchor = pawn.Position;
                return true;
            }
            anchor = pawn.Position;
            return true;
        }

        internal void BeginSupportAttempt(Pawn pawn, Job job,
            CAReportedSupportCommit commit)
        {
            if (pawn == null || job == null || !commit.IsValid) return;
            int id = pawn.thingIDNumber;
            supportTargetIds[id] = commit.targetId;
            supportSourceTicks[id] = commit.sourceTick;
            supportTargetCells[id] = commit.targetCell;
            supportAnchors[id] = commit.anchor;
            supportCooldownUntilTicks.Remove(id);
            supportAttemptJobIds[id] = job.loadID;
            supportAttemptOrigins[id] = commit.origin;
            CAReportedSupportEvaluation evaluation;
            if (supportEvaluations.TryGetValue(id, out evaluation)
                && evaluation != null)
            {
                evaluation.Tick = Find.TickManager.TicksGame;
                evaluation.Stage = "started";
                evaluation.Reason = "support-move-job-started";
            }
        }

        internal bool TryCommitSupportAttempt(Pawn pawn, Job job,
            out CADraftedSupportDiagnosticState state)
        {
            state = default(CADraftedSupportDiagnosticState);
            if (pawn == null || job == null) return false;
            int id = pawn.thingIDNumber;
            int attemptJobId;
            IntVec3 origin;
            if (!supportAttemptJobIds.TryGetValue(id, out attemptJobId)
                || attemptJobId != job.loadID
                || !supportAttemptOrigins.TryGetValue(id, out origin)
                || pawn.Position == origin
                || !TryGetSupportDiagnosticState(pawn, out state))
                return false;
            supportAttemptJobIds.Remove(id);
            supportAttemptOrigins.Remove(id);
            supportCooldownUntilTicks[id] = Find.TickManager.TicksGame + 600;
            CAReportedSupportEvaluation evaluation;
            if (supportEvaluations.TryGetValue(id, out evaluation)
                && evaluation != null)
            {
                evaluation.Tick = Find.TickManager.TicksGame;
                evaluation.Stage = "committed";
                evaluation.Reason = "first-actual-cell-transition";
            }
            return true;
        }

        internal bool TryAbortSupportAttempt(Pawn pawn, Job job,
            out CADraftedSupportDiagnosticState state)
        {
            state = default(CADraftedSupportDiagnosticState);
            if (pawn == null || job == null) return false;
            int id = pawn.thingIDNumber;
            int attemptJobId;
            if (!supportAttemptJobIds.TryGetValue(id, out attemptJobId)
                || attemptJobId != job.loadID
                || !TryGetSupportDiagnosticState(pawn, out state))
                return false;
            CAReportedSupportEvaluation evaluation;
            if (supportEvaluations.TryGetValue(id, out evaluation)
                && evaluation != null)
            {
                evaluation.Tick = Find.TickManager.TicksGame;
                evaluation.Stage = "aborted";
                evaluation.Reason = "support-job-ended-before-cell-transition";
            }
            ClearSupportEnvelope(id);
            return true;
        }

        internal bool TryGetSupportDiagnosticState(Pawn pawn,
            out CADraftedSupportDiagnosticState state)
        {
            state = default(CADraftedSupportDiagnosticState);
            if (pawn == null) return false;
            int id = pawn.thingIDNumber;
            int targetId;
            if (!supportTargetIds.TryGetValue(id, out targetId)) return false;
            int sourceTick;
            int cooldownUntil;
            IntVec3 anchor;
            IntVec3 targetCell;
            if (!supportSourceTicks.TryGetValue(id, out sourceTick))
                sourceTick = -1;
            if (!supportCooldownUntilTicks.TryGetValue(id,
                    out cooldownUntil)) cooldownUntil = -1;
            if (!supportAnchors.TryGetValue(id, out anchor))
                anchor = IntVec3.Invalid;
            if (!supportTargetCells.TryGetValue(id, out targetCell))
                targetCell = IntVec3.Invalid;
            int attemptJobId;
            IntVec3 attemptOrigin;
            bool pending = supportAttemptJobIds.TryGetValue(id,
                out attemptJobId);
            if (!supportAttemptOrigins.TryGetValue(id, out attemptOrigin))
                attemptOrigin = IntVec3.Invalid;
            state = new CADraftedSupportDiagnosticState(pending,
                pending ? attemptJobId : -1, attemptOrigin, targetId,
                sourceTick, cooldownUntil, anchor, targetCell);
            return true;
        }

        internal void RecordSupportEvaluation(Pawn pawn,
            CAReportedSupportEvaluation evaluation)
        {
            if (pawn == null || evaluation == null) return;
            supportEvaluations[pawn.thingIDNumber] = evaluation;
        }

        internal bool TryGetSupportEvaluation(Pawn pawn,
            out CAReportedSupportEvaluation evaluation)
        {
            evaluation = null;
            return pawn != null && supportEvaluations.TryGetValue(
                pawn.thingIDNumber, out evaluation) && evaluation != null;
        }

        private void ClearSupportEnvelope(Pawn pawn)
        {
            if (pawn == null) return;
            ClearSupportEnvelope(pawn.thingIDNumber);
        }

        private void MarkPendingSupportAttemptAborted(int id, string reason)
        {
            if (!supportAttemptJobIds.ContainsKey(id)) return;
            CAReportedSupportEvaluation evaluation;
            if (!supportEvaluations.TryGetValue(id, out evaluation)
                || evaluation == null) return;
            evaluation.Tick = Find.TickManager.TicksGame;
            evaluation.Stage = "aborted";
            evaluation.Reason = reason;
        }

        private void ClearSupportEnvelope(int id)
        {
            supportTargetIds.Remove(id);
            supportSourceTicks.Remove(id);
            supportCooldownUntilTicks.Remove(id);
            supportAnchors.Remove(id);
            supportTargetCells.Remove(id);
            supportAttemptJobIds.Remove(id);
            supportAttemptOrigins.Remove(id);
        }

        private void PruneOrphanedState()
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            var live = new Dictionary<int, Pawn>();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && pawn.Spawned && !pawn.Dead)
                    live[pawn.thingIDNumber] = pawn;
            }

            var tracked = new HashSet<int>(draftedResponseJobIds.Keys);
            tracked.UnionWith(autonomousRangedJobIds.Keys);
            tracked.UnionWith(supportTargetIds.Keys);
            tracked.UnionWith(supportSourceTicks.Keys);
            tracked.UnionWith(supportCooldownUntilTicks.Keys);
            tracked.UnionWith(supportAnchors.Keys);
            tracked.UnionWith(supportTargetCells.Keys);
            tracked.UnionWith(supportAttemptJobIds.Keys);
            tracked.UnionWith(supportAttemptOrigins.Keys);
            tracked.UnionWith(supportEvaluations.Keys);
            foreach (int id in tracked)
            {
                Pawn pawn;
                if (!live.TryGetValue(id, out pawn))
                {
                    draftedResponseJobIds.Remove(id);
                    autonomousRangedJobIds.Remove(id);
                    ClearSupportEnvelope(id);
                    supportEvaluations.Remove(id);
                    watchDiagnosticTicks.Remove(id);
                    continue;
                }
                int responseJobId;
                if (draftedResponseJobIds.TryGetValue(id,
                        out responseJobId)
                    && (pawn.CurJob == null
                        || pawn.CurJob.loadID != responseJobId))
                    draftedResponseJobIds.Remove(id);
                int autonomousJobId;
                if (autonomousRangedJobIds.TryGetValue(id,
                        out autonomousJobId)
                    && (pawn.CurJob == null
                        || pawn.CurJob.loadID != autonomousJobId))
                    autonomousRangedJobIds.Remove(id);
                int attemptJobId;
                bool staleAttempt = supportAttemptJobIds.TryGetValue(id,
                        out attemptJobId)
                    && (pawn.CurJob == null
                        || pawn.CurJob.loadID != attemptJobId);
                if (!pawn.Drafted)
                {
                    MarkPendingSupportAttemptAborted(id,
                        "actor-undrafted-before-cell-transition");
                    ClearSupportEnvelope(id);
                }
                else if (staleAttempt)
                {
                    MarkPendingSupportAttemptAborted(id,
                        "job-no-longer-current-before-cell-transition");
                    ClearSupportEnvelope(id);
                }
            }
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 == 0)
                PruneOrphanedState();
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null) return;
            bool combatEnabled = settings.raidResponse
                || settings.survivalResponses;
            if (Find.TickManager.TicksGame % 30 == 0)
                SquadComponent.MaintainPropagatedDrafts(map,
                    settings.draftChain);
            // Launch-time enrollment is not enough: actors can enter an impact
            // envelope or gain line of sight while a projectile is already in
            // flight. A five-tick map scan is bounded by the live projectile list
            // and only adds actor-local facts that direct targeting or current LOS
            // actually supports.
            if (settings.survivalResponses
                && Find.TickManager.TicksGame % 5 == 0)
                CACombatIntent.RebuildIncomingProjectiles(map);
            if (!combatEnabled && !settings.eatSmart
                && !settings.draftChain
                && draftedResponseJobIds.Count == 0)
            {
                CACombatRecoveryMapComponent recovery = map
                    .GetComponent<CACombatRecoveryMapComponent>();
                if (recovery == null || !recovery.HasAnyRecovery) return;
            }
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == null) continue;
                if (!pawn.Drafted)
                {
                    MarkPendingSupportAttemptAborted(pawn.thingIDNumber,
                        "actor-undrafted-before-cell-transition");
                    ClearSupportEnvelope(pawn);
                }
                CAImmediateCombat.EndRecoveryForOperatorControl(pawn);
                if (settings.eatSmart
                    && Patch_EatSmart.CancelUnsafeCurrentMeal(pawn)) continue;

                // Cleanup and danger release belong to the exact job this pump
                // acquired, not to today's feature eligibility. They therefore run
                // before combat-setting, autonomy, role, and standing-order gates.
                if (pawn != null && pawn.IsHashIntervalTick(30))
                {
                    bool ownsResponse = OwnsDraftedResponseJob(
                        pawn, pawn.CurJob);
                    if (!pawn.Drafted || pawn.CurJob == null
                        || !ownsResponse)
                    {
                        draftedResponseJobIds.Remove(pawn.thingIDNumber);
                        ownsResponse = false;
                    }
                    if (ownsResponse
                        && CACombatThreat.PerceivesActiveThreat(pawn)
                        && MayReleaseOwnedDraftedResponse(pawn))
                    {
                        CATriageAssessment activeAssessment;
                        Job release = CAMissionTriage.TryGiveJob(pawn,
                            out activeAssessment, allowDraftedIdle: true,
                            currentOwnedByDraftedResponse: true);
                        if (StartIfNew(pawn, release))
                        {
                            CATrace.Pawn(pawn,
                                "drafted casualty/welfare response YIELDS to renewed active danger",
                                anchor: pawn.Position);
                            continue;
                        }
                    }
                }
                if (!combatEnabled) continue;

                bool mayFight = JobGiver_CACombatReaction.PlayerMaySelfReact(
                    pawn, settings);
                bool mayPreserve = JobGiver_CACombatReaction.PlayerMaySelfPreserve(
                    pawn, settings);
                // Incoming explosive geometry is a short-lived physical deadline,
                // so check it every tick before ordinary protected-job and movement
                // gates. Direct or queued player work remains sovereign.
                bool mayObserveImmediateSurvival =
                    MayObserveImmediateSurvival(pawn);
                if (mayObserveImmediateSurvival && mayPreserve)
                {
                    Job evasion;
                    CAIncomingEvasionResult evasionState = CACombatIntent
                        .ResolveIncomingHazardEvasion(pawn, out evasion);
                    if (evasionState == CAIncomingEvasionResult.Active)
                    {
                        if (evasion != null && evasion != pawn.CurJob)
                        {
                            CAImmediateCombat
                                .PrepareStandingCombatForImmediateEvasion(pawn);
                            pawn.jobs.StartJob(evasion,
                                JobCondition.InterruptForced);
                        }
                        // An already-running escape remains sovereign. Do not fall
                        // into fresh-harm or recovery logic merely because no new Job
                        // object had to be emitted this tick.
                        continue;
                    }
                    if (evasionState == CAIncomingEvasionResult.Pending)
                        continue;
                }
                if (!pawn.Drafted || !pawn.IsHashIntervalTick(30)) continue;

                // A fresh hit is likewise allowed to supersede an automatic
                // recovery or position move. It remains bounded to the one harm
                // episode inside TrySelfPreservationJob.
                if (mayObserveImmediateSurvival && mayPreserve)
                {
                    Job survival;
                    CASelfPreservationAssessment assessment;
                    if (CAImmediateCombat.TrySelfPreservationJob(pawn, null,
                        IntVec3.Invalid, mayFight, out survival,
                        out assessment))
                    {
                        ReleaseTacticalPostForSurvival(pawn);
                        pawn.jobs.StartJob(survival,
                            JobCondition.InterruptForced);
                        continue;
                    }
                }


                // A CA-owned outward casualty or welfare response may have begun
                // while the actor was viable. Quiet blood loss or capacity loss can
                // cross the recovery threshold without a fresh damage callback, so
                // actor-local recovery receives one preemption gate before ordinary
                // protected-job and weapon-cycle checks. Direct and queued operator
                // work remain excluded by MayReleaseOwnedDraftedResponse.
                Job outward = pawn.CurJob;
                if (OwnsDraftedResponseJob(pawn, outward)
                    && outward != null
                    && outward.def != CA_Defs.EmergencySelfTend
                    && MayReleaseOwnedDraftedResponse(pawn)
                    && CAImmediateCombat.RequiresCombatRecoveryNow(pawn))
                {
                    string outwardName = outward.def.defName;
                    int outwardId = outward.loadID;
                    Job actorRecovery;
                    if (CAImmediateCombat.TryPersistentRecoveryJob(pawn,
                            out actorRecovery)
                        && StartIfNew(pawn, actorRecovery))
                    {
                        CATrace.Pawn(pawn,
                            "drafted outward response " + outwardName + "#"
                            + outwardId
                            + " YIELDS to actor-local combat deterioration",
                            anchor: pawn.Position);
                        continue;
                    }
                }

                if (!MayJudge(pawn)) continue;
                bool activeThreat = CACombatThreat.PerceivesActiveThreat(pawn);
                // Drafted initiative is bounded between commitments. A warmup,
                // burst, melee intercept, or current move completes before this
                // periodic lane may replace the pawn's job.
                if (!MayRepositionNow(pawn)) continue;

                bool idleWatch = IsIdleDraftedWatch(pawn);
                Job recovery;
                if (CAImmediateCombat.TryPersistentRecoveryJob(pawn,
                    out recovery))
                {
                    StartIfNew(pawn, recovery);
                    // Actor-local recovery is evaluated before outward casualty or
                    // welfare work. A finite self-tend ending is not permission to
                    // walk away while the same actor remains below viability.
                    continue;
                }

                CAWelfareThresholdSupportMapComponent welfareSupport =
                    CAWelfareThresholdSupportMapComponent.For(map);
                if (idleWatch && welfareSupport != null)
                {
                    Job supportMove;
                    if (welfareSupport.TryGiveSupportMove(pawn,
                            out supportMove)
                        && StartIfNew(pawn, supportMove))
                        continue;
                    if (welfareSupport.RetainsSupportPost(pawn))
                        continue;
                }

                if (idleWatch && !activeThreat)
                {
                    CATriageAssessment triageAssessment;
                    Job triage = CAMissionTriage.TryGiveJob(pawn,
                        out triageAssessment, allowDraftedIdle: true);
                    if (StartIfNew(pawn, triage,
                        draftedResponse: true))
                    {
                        TraceDraftedWatchYield(pawn, triage,
                            "casualty triage");
                        continue;
                    }
                    Job welfare = JobGiver_CAWelfareResponse.TryGiveResponse(
                        pawn, allowDraftedIdle: true);
                    if (StartIfNew(pawn, welfare,
                        draftedResponse: true))
                    {
                        TraceDraftedWatchYield(pawn, welfare,
                            "welfare response");
                        continue;
                    }
                    Job aftermath = CACombatAftermath.TryGiveJob(pawn,
                        allowDraftedIdle: true);
                    if (StartIfNew(pawn, aftermath,
                        draftedResponse: true))
                    {
                        TraceDraftedWatchYield(pawn, aftermath,
                            "post-contact field custody");
                        continue;
                    }
                }

                if (!mayFight)
                {
                    if (idleWatch) TraceDraftedWatchEvaluation(pawn);
                    continue;
                }

                Pawn target;
                ThreatContactSnapshot contact;
                if (!JobGiver_CACombatReaction.TryVisiblePositionContact(
                    pawn, settings, out target, out contact))
                {
                    Job support;
                    CAReportedSupportCommit supportCommit;
                    if (idleWatch && CAImmediateCombat
                        .TryReportedSupportPositionJob(pawn, settings,
                            out support, out supportCommit))
                    {
                        bool retireHold = supportCommit.casualtyReorganization
                            && CATactical.IsHold(pawn);
                        if (retireHold)
                        {
                            // The move was scored while the Hold still supplied its
                            // provenance. Clear the old ownership before starting
                            // it so a later Hold release cannot terminate the new
                            // finite commitment. If StartJob unexpectedly refuses,
                            // the Hold remains intact rather than disappearing.
                            support.lord = null;
                        }
                        if (StartIfNew(pawn, support))
                        {
                            if (retireHold)
                            {
                                HoldMapComponent hold = pawn.Map
                                    .GetComponent<HoldMapComponent>();
                                const string reason =
                                    "local matchup resolved; a fresh teammate collapse and active hostile contact require bounded squad reorganization";
                                if (hold != null)
                                    hold.Release(pawn, startNewJob: false,
                                        reason: reason);
                                else
                                    CATactical.Release(pawn,
                                        startNewJob: false, reason: reason);
                            }
                            CAImmediateCombat.BeginReportedSupportPositionJob(
                                pawn, support, supportCommit);
                            continue;
                        }
                    }
                    if (idleWatch) TraceDraftedWatchEvaluation(pawn);
                    continue;
                }

                Job position;
                CACombatCellAssessment current;
                CACombatCellAssessment better;
                if (CAImmediateCombat.TryFirePositionJob(pawn, target,
                    out position, out current, out better))
                {
                    // Every direct/queued order was excluded immediately above. The
                    // remaining Wait_Combat or standing posture is safe to replace with
                    // this one finite move; after arrival native planted fire resumes.
                    pawn.jobs.StartJob(position, JobCondition.InterruptForced);
                    continue;
                }

                // Drafted melee actors also miss HumanlikeConstant. Give them the same
                // bounded, support-aware commitment as the normal reaction lane, but
                // leave tactical Holds/automatic posts to their authored duty envelope.
                if (CATactical.IsHold(pawn)
                    || CATactical.IsAutomaticDefense(pawn))
                {
                    if (idleWatch) TraceDraftedWatchEvaluation(pawn);
                    continue;
                }
                Verb verb = pawn.TryGetAttackVerb(target,
                    CAImmediateCombat.AllowManualCombatVerb(pawn));
                if (verb == null || !verb.verbProps.IsMeleeAttack) continue;
                Job engage = JobGiver_CACombatReaction.PlayerImmediateFight(
                    pawn, target, contact);
                if (engage != null)
                    pawn.jobs.StartJob(engage, JobCondition.InterruptForced);
                else if (idleWatch) TraceDraftedWatchEvaluation(pawn);
            }
        }

        private bool StartIfNew(Pawn pawn, Job job,
            bool draftedResponse = false)
        {
            if (pawn == null || job == null) return false;
            if (pawn.CurJob == job) return false;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            bool started = pawn.CurJob == job
                || pawn.CurJob != null && pawn.CurJob.loadID == job.loadID;
            if (!started) return false;
            if (draftedResponse)
                draftedResponseJobIds[pawn.thingIDNumber] = job.loadID;
            else
                draftedResponseJobIds.Remove(pawn.thingIDNumber);
            return true;
        }

        private bool OwnsDraftedResponseJob(Pawn pawn, Job job)
        {
            if (pawn == null || job == null) return false;
            int loadId;
            if (draftedResponseJobIds.TryGetValue(pawn.thingIDNumber,
                    out loadId) && loadId == job.loadID) return true;
            // The recovery episode is the durable owner across save/load; the
            // session-only job-id index may be empty after restoration.
            return job.def == CA_Defs.EmergencySelfTend
                && CAImmediateCombat.IsRecovering(pawn);
        }

        private static bool MayReleaseOwnedDraftedResponse(Pawn pawn)
        {
            return pawn != null && pawn.jobs != null
                && (pawn.CurJob == null || !pawn.CurJob.playerForced)
                && (pawn.jobs.jobQueue == null
                    || !pawn.jobs.jobQueue.AnyPlayerForced);
        }

        internal static bool IsIdleDraftedWatch(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            return job == null || job.def == JobDefOf.Wait_Combat
                || job.def == JobDefOf.Wait
                || job.def == CA_Defs.CombatPosture
                || job.def == CA_Defs.CombatRecovery;
        }

        private static void TraceDraftedWatchYield(Pawn pawn, Job replacement,
            string lane)
        {
            CATrace.Pawn(pawn,
                "drafted idle watch YIELDS to adopted " + lane + " job "
                + (replacement?.def?.defName ?? "unknown") + "#"
                + (replacement != null ? replacement.loadID.ToString() : "none"),
                anchor: pawn.Position);
        }

        private static void TraceDraftedWatchEvaluation(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;
            int last;
            if (watchDiagnosticTicks.TryGetValue(pawn.thingIDNumber, out last)
                && now - last < 300) return;
            watchDiagnosticTicks[pawn.thingIDNumber] = now;
            Job current = pawn.CurJob;
            Lord lord = pawn.GetLord();
            PawnDuty duty = pawn.mindState?.duty;
            int draftLeader, draftEpisode;
            bool propagated = SquadComponent.TryGetPropagatedDraft(pawn,
                out draftLeader, out draftEpisode);
            CATrace.Pawn(pawn,
                "drafted idle watch EVALUATED recovery, casualty, welfare, visible contact, and fresh team support - no actionable replacement adopted; combat evaluation continues"
                + " (job " + (current?.def?.defName ?? "none")
                + ", player-forced " + (current != null && current.playerForced
                    ? "yes" : "no")
                + ", expiry " + (current != null
                    ? current.expiryInterval.ToString() : "none")
                + ", lord " + (lord?.LordJob?.GetType().Name ?? "none")
                + ", duty " + (duty?.def?.defName ?? "none")
                + ", draft-owner " + (propagated
                    ? "CA leader " + draftLeader + " episode " + draftEpisode
                    : "not CA-propagated") + ")",
                anchor: pawn.Position);
        }

        private bool MayJudge(Pawn pawn)
        {
            if (pawn.Dead || pawn.Downed || !pawn.Spawned || !pawn.Awake()
                || pawn.InMentalState || pawn.jobs == null
                || CATactical.HasForeignPlayerForcedJob(pawn)) return false;
            if (pawn.CurJob != null && pawn.CurJob.playerForced) return false;
            if (pawn.jobs.jobQueue != null
                && pawn.jobs.jobQueue.AnyPlayerForced) return false;

            WithdrawalMapComponent withdrawal =
                map.GetComponent<WithdrawalMapComponent>();
            if (withdrawal != null && withdrawal.InPlan(pawn)) return false;
            if (HiddenRegistry.IsHiddenOrOrdered(pawn)) return false;
            if (CATactical.HasExplicitOrder(pawn) && !CATactical.IsHold(pawn))
                return false;
            Lord lord = pawn.GetLord();
            if (lord != null && lord.LordJob is LordJob_CAStackBreach) return false;

            Job job = pawn.CurJob;
            // CA_CombatRecovery is a persistent controller, not an opaque protected
            // terminal job. Its wait driver can defend the current pocket, while
            // this 30-tick pump must still arbitrate renewed movement, urgent
            // self-treatment, and episode completion. Without this exception the
            // protected-job gate prevents TryPersistentRecoveryJob from ever being
            // called again, so an injured pawn can remain in recovery forever.
            if (job != null && job.def == CA_Defs.CombatRecovery
                && CAImmediateCombat.IsRecovering(pawn)) return true;
            return !IsProtectedJob(job)
                || pawn.Drafted && OwnsDraftedResponseJob(pawn, job)
                    && CACombatThreat.PerceivesActiveThreat(pawn);
        }

        private static bool MayObserveImmediateSurvival(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned
                || !pawn.Awake() || pawn.InMentalState || pawn.jobs == null
                || CATactical.HasForeignPlayerForcedJob(pawn)) return false;
            Lord lord = pawn.GetLord();
            Job current = pawn.CurJob;
            // A current untagged native job is the player's direct hand. A
            // player-forced job stamped by the pawn's current CA lord is the
            // approach/placement leg of that CA order and may be aborted by an
            // actual explosive or harm event. HasForeignPlayerForcedJob above
            // separately protects a later untagged queued order.
            return current == null || !current.playerForced
                || lord != null && current.lord == lord;
        }

        private static bool MayRepositionNow(Pawn pawn)
        {
            if (IsTargetCommitment(pawn.CurJob)) return false;
            if (pawn.pather != null && pawn.pather.Moving) return false;
            Stance_Busy busy = pawn.stances != null
                ? pawn.stances.curStance as Stance_Busy : null;
            return busy == null || busy.verb == null;
        }

        private static void ReleaseTacticalPostForSurvival(Pawn pawn)
        {
            CAImmediateCombat.ReleaseStandingCombatForSurvival(pawn);
        }

        private static bool IsTargetCommitment(Job job)
        {
            return job != null && (job.def == JobDefOf.AttackStatic
                || job.def == JobDefOf.AttackMelee
                || job.def == CA_Defs.BoundedRangedDefense
                || job.def == CA_Defs.BoundedMeleeDefense);
        }

        private static bool IsProtectedJob(Job job)
        {
            return job != null && (job.def == JobDefOf.TendPatient
                || job.def == JobDefOf.TendEntity
                || job.def == JobDefOf.Rescue
                || job.def == CA_Defs.EmergencySelfTend
                || job.def == CA_Defs.AssessCasualty
                || job.def == CA_Defs.CheckWelfare
                || job.def == CA_Defs.FightingWithdrawal
                || job.def == CA_Defs.Shelter
                || job.def == CA_Defs.CombatRecovery
                || job.def == CA_Defs.WaitForWelfareSupport
                || job.def == JobDefOf.BeatFire
                || job.def == JobDefOf.ExtinguishSelf
                || job.def == JobDefOf.ExtinguishFiresNearby
                || job.def == JobDefOf.Flee
                || job.def == JobDefOf.FleeAndCower
                || job.def == JobDefOf.FleeAndCowerShort);
        }
    }

    // Each movement decision carries the exact path that was assessed for hostile
    // pressure and friendly weapon sectors. Native Goto would recompute that route
    // after selection, so this driver starts one native path and validates every
    // next cell against the locked adjacent sequence. Geometry drift aborts instead
    // of silently substituting a route, without adding per-cell repath latency.
    public class JobDriver_CACombatMove : JobDriver
    {
        private bool pathStarted;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pathStarted, "pathStarted", false);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn?.Map == null || !job.targetA.IsValid
                || !pawn.Map.pawnDestinationReservationManager.CanReserve(
                    job.targetA.Cell, pawn)) return false;
            pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job,
                job.targetA.Cell);
            return true;
        }

        public override bool IsContinuation(Job candidate)
        {
            return candidate != null && candidate.def == job.def
                && candidate.targetA.Cell == job.targetA.Cell;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(delegate
            {
                pawn.pather?.StopDead();
                CAImmediateCombat.AbortReportedSupportAttempt(pawn, job);
            });
            Toil move = ToilMaker.MakeToil("CALockedCombatMove");
            move.initAction = delegate
            {
                pawn.pather?.StopDead();
                bool missingRoute = job.targetQueueA.NullOrEmpty()
                    && pawn.Position != job.targetA.Cell;
                if (!CAImmediateCombat.EnsureLockedCombatRoute(pawn, job))
                {
                    CATrace.Pawn(pawn,
                        "locked combat route ABORTS - no executable assessed route reaches the destination",
                        destination: job.targetA.Cell, anchor: pawn.Position);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (missingRoute)
                    CATrace.Pawn(pawn,
                        "locked combat route RESTORES a fresh bounded path for a legacy job without serialized assessed cells",
                        destination: job.targetA.Cell, anchor: pawn.Position);
                if (pawn.Position != job.targetA.Cell)
                {
                    pathStarted = true;
                    pawn.pather.StartPath(job.targetA.Cell,
                        PathEndMode.OnCell);
                }
                TickLockedRoute();
            };
            move.tickAction = TickLockedRoute;
            move.defaultCompleteMode = ToilCompleteMode.Never;
            move.socialMode = RandomSocialMode.Off;
            yield return move;
        }

        public override void Notify_PatherArrived()
        {
            TickLockedRoute();
        }

        public override void Notify_PatherFailed()
        {
            IntVec3 next = job?.targetQueueA != null
                && job.targetQueueA.Count > 0
                    ? job.targetQueueA[0].Cell : IntVec3.Invalid;
            CATrace.Pawn(pawn,
                "locked combat route ABORTS - native pather failed before assessed next cell "
                + next,
                destination: job != null ? job.targetA.Cell : IntVec3.Invalid,
                anchor: pawn != null ? pawn.Position : IntVec3.Invalid);
            base.Notify_PatherFailed();
        }

        private void TickLockedRoute()
        {
            if (pawn == null || pawn.Map == null || pawn.pather == null
                || pawn.jobs == null || pawn.CurJob != job)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            List<LocalTargetInfo> route = job.targetQueueA;
            while (route != null && route.Count > 0
                && route[0].Cell == pawn.Position)
                route.RemoveAt(0);
            CAImmediateCombat.ConfirmReportedSupportProgress(pawn, job);
            if (route == null || route.Count == 0)
            {
                bool arrived = pawn.Position == job.targetA.Cell;
                if (!arrived)
                    CATrace.Pawn(pawn,
                        "locked combat route ABORTS - assessed route is absent before destination",
                        destination: job.targetA.Cell, anchor: pawn.Position);
                EndJobWith(arrived ? JobCondition.Succeeded
                    : JobCondition.Incompletable);
                return;
            }

            IntVec3 next = route[0].Cell;
            bool contiguous = pawn.Position.AdjacentTo8WayOrInside(next);
            bool staticallyExecutable = contiguous && CAImmediateCombat
                .CombatMoveStepIsStaticallyExecutable(pawn, next);
            bool urgentExplosive = CACombatIntent
                .HasImmediateIncomingHazard(pawn);
            bool sectorSafe = staticallyExecutable && (urgentExplosive
                || CAImmediateCombat.CombatMoveStepStillSectorSafe(pawn, next));
            if (!contiguous || !staticallyExecutable || !sectorSafe)
            {
                pawn.pather.StopDead();
                CATrace.Pawn(pawn, !contiguous
                        ? "locked combat route ABORTS - assessed next cell is no longer contiguous"
                        : !staticallyExecutable
                        ? "locked combat route ABORTS - assessed next cell became blocked or disallowed"
                        : "locked combat route ABORTS - active friendly firing sector occupies the next step",
                    destination: next, anchor: pawn.Position);
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            if (pawn.pather.Moving)
            {
                IntVec3 destination = pawn.pather.Destination.Cell;
                IntVec3 nativeNext = pawn.pather.nextCell;
                if (destination != job.targetA.Cell
                    || nativeNext.IsValid && nativeNext != pawn.Position
                        && nativeNext != next)
                {
                    pawn.pather.StopDead();
                    CATrace.Pawn(pawn,
                        "locked combat route ABORTS - native pather attempted an unassessed step",
                        destination: nativeNext.IsValid
                            ? (IntVec3?)nativeNext : destination,
                        anchor: pawn.Position);
                    EndJobWith(JobCondition.Incompletable);
                }
                return;
            }
            if (pathStarted)
            {
                CATrace.Pawn(pawn,
                    "locked combat route ABORTS - native path stopped before the assessed destination",
                    destination: job.targetA.Cell, anchor: pawn.Position);
                EndJobWith(JobCondition.Incompletable);
            }
        }
    }

    public class JobDriver_CACombatRecovery : JobDriver_Wait
    {
        public override bool IsContinuation(Job candidate)
        {
            return candidate != null && candidate.def == job.def
                && candidate.count == job.count;
        }

        public override void DecorateWaitToil(Toil wait)
        {
            base.DecorateWaitToil(wait);
            wait.tickIntervalAction += delegate(int delta)
            {
                if (GenTicks.IsTickIntervalDelta(pawn.thingIDNumber, 4,
                        delta))
                    CAImmediateCombat.TryRecoveryHoldDefense(pawn);
                if (GenTicks.IsTickIntervalDelta(pawn.thingIDNumber, 30, delta)
                    && !CAImmediateCombat.RecoveryStillRequired(pawn))
                    EndJobWith(JobCondition.Succeeded);
            };
        }
    }

    // Native drafted FireAtWill and CA-owned ranged commitments select targets
    // autonomously. Authorize those shots against the current physical lane at
    // cast start, and set the engine's projectile-level friendly-fire protection
    // for later dispersion or movement. Direct player-authored attack jobs remain
    // untouched.
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new[]
    {
        typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool),
        typeof(bool), typeof(bool), typeof(bool)
    })]
    public static class Patch_CAAutonomousFriendlyFireCastStart
    {
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(Verb __instance, LocalTargetInfo castTarg,
            ref bool preventFriendlyFire, ref bool __result)
        {
            Pawn shooter = __instance?.CasterPawn;
            Thing target = castTarg.Thing;
            string ownership;
            if (!CAImmediateCombat.ShouldProtectAutonomousRangedShot(
                    shooter, target, __instance, out ownership)) return true;
            float risk;
            Pawn blocker;
            bool impactEnvelopeUnsafe;
            bool safe = CAImmediateCombat.HasSafeRangedLane(shooter, target,
                __instance, out risk, out blocker,
                out impactEnvelopeUnsafe);
            preventFriendlyFire = true;
            CAImmediateCombat.RecordFriendlyFireAuthorization(shooter, target,
                blocker, risk, "cast-start", safe
                    ? "authorized-protected"
                    : impactEnvelopeUnsafe
                        ? "held-unsafe-impact-envelope"
                        : "held-unsafe-lane",
                ownership);
            if (safe) return true;
            __result = false;
            return false;
        }
    }

    // Recheck the same bounded authorization throughout warmup. An ally who enters
    // after target acquisition cancels the autonomous shot before launch; the
    // protected projectile flag remains a final guard during an active burst.
    [HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceTick))]
    public static class Patch_CAAutonomousFriendlyFireWarmup
    {
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(Stance_Warmup __instance)
        {
            Pawn shooter = __instance?.stanceTracker?.pawn;
            Thing target = __instance != null
                ? __instance.focusTarg.Thing : null;
            Verb verb = __instance?.verb;
            string ownership;
            if (!CAImmediateCombat.ShouldProtectAutonomousRangedShot(
                    shooter, target, verb, out ownership)) return true;
            float risk;
            Pawn blocker;
            bool impactEnvelopeUnsafe;
            if (CAImmediateCombat.HasSafeRangedLane(shooter, target, verb,
                    out risk, out blocker, out impactEnvelopeUnsafe))
                return true;
            CAImmediateCombat.RecordFriendlyFireAuthorization(shooter, target,
                blocker, risk, "warmup-recheck", impactEnvelopeUnsafe
                    ? "interrupted-unsafe-impact-envelope"
                    : "interrupted-unsafe-lane",
                ownership);
            __instance.Interrupt();
            return false;
        }
    }

    // Pawn_HealthTracker asks the JobTracker for a damage override before
    // Pawn_MindState updates lastHarmTick. Capture the stimulus at that native
    // ordering seam so the same override pass can make a survival decision.
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.Notify_DamageTaken))]
    public static class Patch_CAImmediateCombatHarm
    {
        public static void Prefix(Pawn ___pawn, DamageInfo dinfo)
        {
            CAImmediateCombat.NoteHarm(___pawn, dinfo);
        }
    }
}
