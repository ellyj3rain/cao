using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    public enum CAIntentOrigin
    {
        Unknown = 0,
        OperatorDirect = 1,
        OperatorRelay = 2,
        Autonomous = 3,
        AutomaticDefense = 4,
        Continuation = 5,
        SaveRestore = 6,
        PeerRelay = 7,
        PlayerDelegated = 8,
        NativeDuty = 9,
        Institutional = 10,
        Household = 11,
        Organization = 12,
        WorldAuthoring = 13
    }

    public enum CAIntentController
    {
        Unknown = 0,
        Ambush = 1,
        Hide = 2,
        Hold = 3,
        Formation = 4,
        Withdrawal = 5,
        SelfPreservation = 6,
        FirePosition = 7,
        ExplosiveEvasion = 8,
        RaidDefense = 9,
        FoodSafety = 10,
        Drill = 11,
        Shelter = 12,
        CombatRecovery = 13,
        DraftCoordination = 14,
        AcousticInvestigation = 15,
        Welfare = 16,
        Logistics = 17,
        SettlementDevelopment = 18,
        Frontier = 19,
        AssaultApproach = 20
    }

    internal enum CAIncomingEvasionResult
    {
        None = 0,
        Pending = 1,
        Active = 2
    }

    // Detached, read-only data for the session-local flight recorder. One fixed-size
    // value describes the actor's earliest active hazard, current escape ownership,
    // and bounded cursor state without exposing or copying the live candidate lists.
    internal readonly struct CAIncomingEvasionDiagnosticState
    {
        public readonly CAIncomingEvasionResult Status;
        public readonly int MapId;
        public readonly int ObservedHazardCount;
        public readonly int ActiveHazardCount;
        public readonly int ProjectileId;
        public readonly int LauncherId;
        public readonly IntVec3 HazardCenter;
        public readonly float HazardRadius;
        public readonly int DeadlineTick;
        public readonly int DeadlineTicksRemaining;
        public readonly int EpisodeId;
        public readonly CAIntentOrigin IntentOrigin;
        public readonly IntVec3 Origin;
        public readonly IntVec3 Destination;
        public readonly bool Reconstructed;
        public readonly bool SearchActive;
        public readonly int SearchHazardSignature;
        public readonly int ClearingCursor;
        public readonly int ClearingCandidateCount;
        public readonly int ClearingPathBudgetPerTick;
        public readonly int ImprovingCursor;
        public readonly int ImprovingCandidateCount;
        public readonly int ImprovingPathBudgetPerTick;
        public readonly int LastFailedEvaluationTick;

        public CAIncomingEvasionDiagnosticState(
            CAIncomingEvasionResult status, int mapId,
            int observedHazardCount, int activeHazardCount,
            int projectileId, int launcherId, IntVec3 hazardCenter,
            float hazardRadius, int deadlineTick, int deadlineTicksRemaining,
            int episodeId, CAIntentOrigin intentOrigin, IntVec3 origin,
            IntVec3 destination, bool reconstructed, bool searchActive,
            int searchHazardSignature, int clearingCursor,
            int clearingCandidateCount, int clearingPathBudgetPerTick,
            int improvingCursor, int improvingCandidateCount,
            int improvingPathBudgetPerTick, int lastFailedEvaluationTick)
        {
            Status = status;
            MapId = mapId;
            ObservedHazardCount = observedHazardCount;
            ActiveHazardCount = activeHazardCount;
            ProjectileId = projectileId;
            LauncherId = launcherId;
            HazardCenter = hazardCenter;
            HazardRadius = hazardRadius;
            DeadlineTick = deadlineTick;
            DeadlineTicksRemaining = deadlineTicksRemaining;
            EpisodeId = episodeId;
            IntentOrigin = intentOrigin;
            Origin = origin;
            Destination = destination;
            Reconstructed = reconstructed;
            SearchActive = searchActive;
            SearchHazardSignature = searchHazardSignature;
            ClearingCursor = clearingCursor;
            ClearingCandidateCount = clearingCandidateCount;
            ClearingPathBudgetPerTick = clearingPathBudgetPerTick;
            ImprovingCursor = improvingCursor;
            ImprovingCandidateCount = improvingCandidateCount;
            ImprovingPathBudgetPerTick = improvingPathBudgetPerTick;
            LastFailedEvaluationTick = lastFailedEvaluationTick;
        }
    }

    // One causal identity carried by an order or decision. The original four
    // fields retain their numeric save identities; the catalog adds identity and
    // authority/ownership facts so UI, trace, persistence and tests name the same
    // commitment.
    public readonly struct CAIntentContext
    {
        public readonly int EpisodeId;
        public readonly CAIntentOrigin Origin;
        public readonly CAIntentController Controller;
        public readonly int IssuerId;
        public readonly string BehaviorKey;
        public readonly CAAuthorityOrigin AuthorityOrigin;
        public readonly string AuthorityIdentity;
        public readonly string OwnershipScope;
        public readonly int OwnerId;
        public readonly int CreatedTick;
        public readonly CAInitiativeTier CreationTier;
        public readonly string TargetOrDemand;
        public readonly string TerminationCondition;

        public CAIntentContext(int episodeId, CAIntentOrigin origin,
            CAIntentController controller, int issuerId,
            string behaviorKey = null,
            CAAuthorityOrigin authorityOrigin = CAAuthorityOrigin.None,
            string authorityIdentity = null,
            string ownershipScope = null, int ownerId = -1,
            CAInitiativeTier creationTier = CAInitiativeTier.Standard,
            string targetOrDemand = null, string terminationCondition = null,
            int createdTick = -1)
        {
            EpisodeId = episodeId;
            Origin = origin;
            Controller = controller;
            IssuerId = issuerId;
            BehaviorKey = string.IsNullOrEmpty(behaviorKey)
                ? CABehaviorCatalog.KeyForController(controller, origin)
                : behaviorKey;
            AuthorityOrigin = authorityOrigin == CAAuthorityOrigin.None
                ? CAIntentAuthority.FromIntentOrigin(origin) : authorityOrigin;
            AuthorityIdentity = authorityIdentity ?? AuthorityOrigin.ToString();
            OwnershipScope = ownershipScope ?? "actor";
            OwnerId = ownerId >= 0 ? ownerId : issuerId;
            CreatedTick = createdTick >= 0 ? createdTick
                : Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            CreationTier = creationTier;
            TargetOrDemand = targetOrDemand;
            CABehaviorDefinition definition = CABehaviorCatalog.Get(BehaviorKey);
            TerminationCondition = terminationCondition
                ?? definition?.CompletionCondition
                ?? "the owning intent completes or stands down";
        }

        public bool IsValid => EpisodeId > 0;
    }

    public static class CAIntentAuthority
    {
        public static CAAuthorityOrigin FromIntentOrigin(CAIntentOrigin origin)
        {
            switch (origin)
            {
                case CAIntentOrigin.OperatorDirect:
                    return CAAuthorityOrigin.OperatorDirect;
                case CAIntentOrigin.OperatorRelay:
                    return CAAuthorityOrigin.OperatorRelay;
                case CAIntentOrigin.Autonomous:
                case CAIntentOrigin.PlayerDelegated:
                    return CAAuthorityOrigin.PlayerDelegated;
                case CAIntentOrigin.AutomaticDefense:
                    return CAAuthorityOrigin.AutomaticDefense;
                case CAIntentOrigin.Continuation:
                    return CAAuthorityOrigin.Continuation;
                case CAIntentOrigin.SaveRestore:
                    return CAAuthorityOrigin.SaveRestore;
                case CAIntentOrigin.PeerRelay:
                    return CAAuthorityOrigin.PeerRequest;
                case CAIntentOrigin.NativeDuty:
                    return CAAuthorityOrigin.NativeDuty;
                case CAIntentOrigin.Institutional:
                    return CAAuthorityOrigin.Institutional;
                case CAIntentOrigin.Household:
                    return CAAuthorityOrigin.Household;
                case CAIntentOrigin.Organization:
                    return CAAuthorityOrigin.Organization;
                case CAIntentOrigin.WorldAuthoring:
                    return CAAuthorityOrigin.WorldAuthoring;
                default:
                    return CAAuthorityOrigin.None;
            }
        }

        public static CAIntentOrigin ToIntentOrigin(CAAuthorityOrigin origin)
        {
            if ((origin & CAAuthorityOrigin.OperatorDirect) != 0)
                return CAIntentOrigin.OperatorDirect;
            if ((origin & CAAuthorityOrigin.OperatorRelay) != 0)
                return CAIntentOrigin.OperatorRelay;
            if ((origin & CAAuthorityOrigin.PlayerDelegated) != 0)
                return CAIntentOrigin.PlayerDelegated;
            if ((origin & CAAuthorityOrigin.NativeDuty) != 0)
                return CAIntentOrigin.NativeDuty;
            if ((origin & CAAuthorityOrigin.PeerRequest) != 0)
                return CAIntentOrigin.PeerRelay;
            if ((origin & CAAuthorityOrigin.AutomaticDefense) != 0)
                return CAIntentOrigin.AutomaticDefense;
            if ((origin & CAAuthorityOrigin.Institutional) != 0)
                return CAIntentOrigin.Institutional;
            if ((origin & CAAuthorityOrigin.Household) != 0)
                return CAIntentOrigin.Household;
            if ((origin & CAAuthorityOrigin.Organization) != 0)
                return CAIntentOrigin.Organization;
            if ((origin & CAAuthorityOrigin.Continuation) != 0)
                return CAIntentOrigin.Continuation;
            if ((origin & CAAuthorityOrigin.SaveRestore) != 0)
                return CAIntentOrigin.SaveRestore;
            if ((origin & CAAuthorityOrigin.WorldAuthoring) != 0)
                return CAIntentOrigin.WorldAuthoring;
            return CAIntentOrigin.Unknown;
        }
    }

    // The combat-power axes the operator asked to distinguish. This is captured only
    // for the pawn making a decision (or a directly visible local participant), never
    // broadcast as colony-wide knowledge.
    public readonly struct CACombatConditionSnapshot
    {
        public readonly float Pain;
        public readonly float Consciousness;
        public readonly float Moving;
        public readonly float Manipulation;
        public readonly float Breathing;
        public readonly float BloodPumping;
        public readonly float BloodLoss;
        public readonly float BleedRate;
        public readonly float Health;
        public readonly int DeathInTicks;
        public readonly bool VitalInjury;
        public readonly float CombatPower;

        private CACombatConditionSnapshot(float pain, float consciousness,
            float moving, float manipulation, float breathing, float bloodPumping,
            float bloodLoss, float bleedRate, float health, int deathInTicks,
            bool vitalInjury, float combatPower)
        {
            Pain = pain;
            Consciousness = consciousness;
            Moving = moving;
            Manipulation = manipulation;
            Breathing = breathing;
            BloodPumping = bloodPumping;
            BloodLoss = bloodLoss;
            BleedRate = bleedRate;
            Health = health;
            DeathInTicks = deathInTicks;
            VitalInjury = vitalInjury;
            CombatPower = combatPower;
        }

        public static CACombatConditionSnapshot Capture(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
                return new CACombatConditionSnapshot(0f, 1f, 1f, 1f, 1f, 1f,
                    0f, 0f, 1f, int.MaxValue, false, 1f);

            HediffSet set = pawn.health.hediffSet;
            float pain = Mathf.Clamp01(set.PainTotal);
            float consciousness = Capacity(pawn, PawnCapacityDefOf.Consciousness);
            float moving = Capacity(pawn, PawnCapacityDefOf.Moving);
            float manipulation = Capacity(pawn, PawnCapacityDefOf.Manipulation);
            float breathing = Capacity(pawn, PawnCapacityDefOf.Breathing);
            float bloodPumping = Capacity(pawn, PawnCapacityDefOf.BloodPumping);
            float health = Mathf.Clamp01(pawn.health.summaryHealth.SummaryHealthPercent);
            float bleed = Mathf.Clamp01(set.BleedRateTotal);
            float bloodLoss = 0f;
            Hediff loss = set.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (loss != null) bloodLoss = Mathf.Clamp01(loss.Severity);
            int deathIn = int.MaxValue;
            if (set.BleedRateTotal > 0.0001f)
            {
                try { deathIn = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn); }
                catch { deathIn = int.MaxValue; }
            }

            bool vital = false;
            List<Hediff> hediffs = set.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (!(h is Hediff_Injury) || h.Part == null || h.Part.def == null)
                    continue;
                List<BodyPartTagDef> tags = h.Part.def.tags;
                if (tags == null) continue;
                if (tags.Contains(BodyPartTagDefOf.ConsciousnessSource)
                    || tags.Contains(BodyPartTagDefOf.BreathingSource)
                    || tags.Contains(BodyPartTagDefOf.BloodPumpingSource)
                    || tags.Contains(BodyPartTagDefOf.BloodFiltrationSource)
                    || tags.Contains(BodyPartTagDefOf.BloodFiltrationLiver)
                    || tags.Contains(BodyPartTagDefOf.BloodFiltrationKidney)
                    || tags.Contains(BodyPartTagDefOf.MetabolismSource))
                {
                    vital = true;
                    break;
                }
            }

            float capacity = consciousness * 0.22f + moving * 0.20f
                + manipulation * 0.20f + breathing * 0.12f
                + bloodPumping * 0.10f + health * 0.16f;
            float combatPower = Mathf.Clamp01(capacity
                * (1f - pain * 0.28f)
                * (1f - bloodLoss * 0.35f)
                * (1f - Mathf.Clamp01(bleed / 0.5f) * 0.18f));
            return new CACombatConditionSnapshot(pain, consciousness, moving,
                manipulation, breathing, bloodPumping, bloodLoss, bleed, health,
                deathIn, vital, combatPower);
        }

        private static float Capacity(Pawn pawn, PawnCapacityDef capacity)
        {
            try
            {
                return Mathf.Clamp01(pawn.health.capacities.GetLevel(capacity));
            }
            catch { return 1f; }
        }

        public string TraceText()
        {
            string horizon = DeathInTicks == int.MaxValue ? "none"
                : DeathInTicks.ToString();
            return "condition pain " + Pain.ToString("F2")
                + ", consciousness " + Consciousness.ToString("F2")
                + ", movement " + Moving.ToString("F2")
                + ", manipulation " + Manipulation.ToString("F2")
                + ", breathing " + Breathing.ToString("F2")
                + ", blood-pumping " + BloodPumping.ToString("F2")
                + ", blood-loss " + BloodLoss.ToString("F2")
                + ", bleed " + BleedRate.ToString("F2")
                + ", vital-injury " + (VitalInjury ? "yes" : "no")
                + ", death-in-ticks " + horizon
                + ", combat-power " + CombatPower.ToString("F2");
        }

        public bool RequiresCombatRecovery(bool ranged)
        {
            return Health <= 0.65f || BleedRate >= 0.35f || Pain >= 0.65f
                || Consciousness < 0.62f || Moving < 0.52f
                || ranged && Manipulation < 0.55f || Breathing < 0.68f
                || BloodPumping < 0.65f || BloodLoss >= 0.38f
                || DeathInTicks < 10000
                || VitalInjury && CombatPower < 0.62f;
        }

        public bool RecoveredCombatViability(bool ranged)
        {
            return Health > 0.72f && BleedRate < 0.18f && Pain < 0.48f
                && Consciousness >= 0.72f && Moving >= 0.64f
                && (!ranged || Manipulation >= 0.64f)
                && Breathing >= 0.76f && BloodPumping >= 0.74f
                && BloodLoss < 0.26f && DeathInTicks >= 18000
                && CombatPower >= 0.62f;
        }
    }

    // Remembered contact answers "what was known". This narrower predicate answers
    // whether the actor is under a combat problem now: recent harm, an observed
    // explosive deadline, or a visible hostile that can presently affect the actor.
    // Welfare and liveness gates use this instead of treating hours-old memory as fire.
    internal static class CACombatThreat
    {
        internal static bool MapHasActiveThreat(Map map)
        {
            return map != null && GenHostility.AnyHostileActiveThreatToPlayer(map);
        }

        internal static bool PerceivesActiveThreat(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Dead || pawn.Downed)
                return false;
            if (CAImmediateCombat.HarmedRecently(pawn)
                || CACombatIntent.HasImmediateIncomingHazard(pawn)
                || HasCloseActionableKnownThreat(pawn)) return true;

            if (pawn.Faction == null) return false;
            HashSet<IAttackTarget> targets = pawn.Map.attackTargetsCache
                .TargetsHostileToFaction(pawn.Faction);
            foreach (IAttackTarget target in targets)
            {
                Thing hostileThing = target?.Thing;
                if (hostileThing == null || !hostileThing.Spawned
                    || hostileThing.Map != pawn.Map
                    || !GenHostility.IsActiveThreatTo(target, pawn.Faction)
                    || !hostileThing.Position.InHorDistOf(pawn.Position, 60f))
                    continue;

                Pawn hostilePawn = hostileThing as Pawn;
                if (hostilePawn != null
                    && !KnowledgeMapComponent.CanCurrentlySeeHostile(
                        pawn, hostilePawn, 60f)) continue;
                if (hostilePawn == null
                    && !GenSight.LineOfSight(pawn.Position,
                        hostileThing.Position, pawn.Map, true)) continue;

                IAttackTargetSearcher searcher = hostileThing
                    as IAttackTargetSearcher;
                Verb verb = hostilePawn != null
                    ? hostilePawn.TryGetAttackVerb(pawn,
                        allowManualCastWeapons: false)
                    : searcher?.CurrentEffectiveVerb;
                if (verb == null) continue;
                if (verb.verbProps.IsMeleeAttack)
                {
                    if (hostileThing.Position.InHorDistOf(
                        pawn.Position, 5.9f))
                        return true;
                }
                else if (verb.CanHitTargetFrom(hostileThing.Position, pawn))
                    return true;
            }
            return false;
        }

        // A nearby copied contact can be an immediate tactical problem even while a
        // wall interrupts raw sight. This is deliberately much narrower than general
        // remembered knowledge: active state, short age, useful confidence, and a
        // cell close enough for one bounded reacquisition move. It suppresses welfare
        // wandering and permits the combat lane to inspect geometry; it does not turn
        // the copied cell into a live pawn or authorize blind fire.
        internal static bool HasCloseActionableKnownThreat(Pawn pawn,
            float radius = 8.9f)
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned
                || pawn.Dead || pawn.Downed) return false;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(
                pawn.Map);
            if (knowledge == null) return false;
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            List<ThreatContactSnapshot> contacts = knowledge.FreshContacts(pawn);
            for (int i = 0; i < contacts.Count; i++)
            {
                ThreatContactSnapshot contact = contacts[i];
                int age = now - contact.SourceTick;
                if (contact.State != ThreatContactState.Active
                    || age < 0 || age > 1800
                    || contact.Evidence.Confidence < 0.55f
                    || !contact.Cell.IsValid
                    || !contact.Cell.InBounds(pawn.Map)
                    || !contact.Cell.InHorDistOf(pawn.Position, radius))
                    continue;
                return true;
            }
            return false;
        }
    }

    public static class CACombatIntent
    {
        private sealed class MovementRecord
        {
            public CAIntentContext Context;
            public IntVec3 From;
            public IntVec3 To;
            public int Tick;
            public int TargetId;
            public bool Reconstructed;
        }

        private sealed class AvoidedCell
        {
            public IntVec3 Cell;
            public int UntilTick;
        }

        private sealed class IncomingHazard
        {
            public int ProjectileId;
            public int MapId;
            public IntVec3 Center;
            public float Radius;
            public int ImpactTick;
            public int LauncherId;
            public IntVec3 OrderedDestination;
            public int OrderedTick;
            public int LastFailedEvaluationTick = -1;
        }

        private sealed class EvasionCandidate
        {
            public IntVec3 Cell;
            public float PreliminaryScore;
            public int RouteTicks;
            public int RouteSteps;
            public int NewWaterEntries;
            public int HazardCells;
            public List<IntVec3> RouteCells;
        }

        private sealed class EvasionSearchState
        {
            public IntVec3 Origin;
            public int HazardSignature;
            public int ClearingCursor;
            public int ImprovingCursor;
            public List<EvasionCandidate> ClearingCandidates;
            public List<EvasionCandidate> ImprovingCandidates;
        }

        internal const int EvasionClearingPathBudgetPerTick = 24;
        internal const int EvasionImprovingPathBudgetPerTick = 8;

        private static int nextEpisodeId;
        private static readonly Dictionary<int, MovementRecord> movements =
            new Dictionary<int, MovementRecord>();
        private static readonly Dictionary<int, List<AvoidedCell>> avoided =
            new Dictionary<int, List<AvoidedCell>>();
        private static readonly Dictionary<int, List<IncomingHazard>> incoming =
            new Dictionary<int, List<IncomingHazard>>();
        private static readonly Dictionary<int, int> evasionEpisodes =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, EvasionSearchState>
            evasionSearches = new Dictionary<int, EvasionSearchState>();
        private static readonly FieldInfo projectileLandedField =
            AccessTools.Field(typeof(Projectile), "landed");
        private static readonly FieldInfo explosiveDelayField =
            AccessTools.Field(typeof(Projectile_Explosive), "ticksToDetonation");

        public static void ClearTransient()
        {
            nextEpisodeId = 0;
            movements.Clear();
            avoided.Clear();
            incoming.Clear();
            evasionEpisodes.Clear();
            evasionSearches.Clear();
        }

        public static int NewEpisode()
        {
            nextEpisodeId++;
            if (nextEpisodeId <= 0) nextEpisodeId = 1;
            return nextEpisodeId;
        }

        public static void ObserveEpisode(int episodeId)
        {
            if (episodeId > nextEpisodeId) nextEpisodeId = episodeId;
        }

        public static CAIntentContext Operator(Pawn issuer, Pawn actor,
            CAIntentController controller, int episodeId = 0,
            string behaviorKey = null, string targetOrDemand = null)
        {
            if (episodeId <= 0) episodeId = NewEpisode();
            return new CAIntentContext(episodeId,
                issuer == actor ? CAIntentOrigin.OperatorDirect
                    : CAIntentOrigin.OperatorRelay,
                controller, issuer != null ? issuer.thingIDNumber : -1,
                behaviorKey: behaviorKey,
                authorityOrigin: issuer == actor
                    ? CAAuthorityOrigin.OperatorDirect
                    : CAAuthorityOrigin.OperatorRelay,
                authorityIdentity: "operator",
                ownershipScope: "operator order",
                ownerId: actor != null ? actor.thingIDNumber : -1,
                creationTier: actor != null
                    ? AutonomyComponent.TierOf(actor)
                    : CAInitiativeTier.Standard,
                targetOrDemand: targetOrDemand);
        }

        public static CAIntentContext Autonomous(Pawn actor,
            CAIntentController controller, int episodeId = 0,
            string behaviorKey = null, string authorityIdentity = null,
            string targetOrDemand = null)
        {
            return Authorized(actor, controller, behaviorKey,
                CAAuthorityOrigin.PlayerDelegated,
                authorityIdentity ?? "player delegation", "pawn behavior",
                targetOrDemand, episodeId,
                CAIntentOrigin.PlayerDelegated);
        }

        public static CAIntentContext Authorized(Pawn actor,
            CAIntentController controller, string behaviorKey,
            CAAuthorityOrigin authorityOrigin, string authorityIdentity,
            string ownershipScope, string targetOrDemand = null,
            int episodeId = 0,
            CAIntentOrigin intentOrigin = CAIntentOrigin.Unknown,
            int issuerId = -1, int ownerId = -1)
        {
            if (episodeId <= 0) episodeId = NewEpisode();
            if (intentOrigin == CAIntentOrigin.Unknown)
                intentOrigin = CAIntentAuthority.ToIntentOrigin(
                    authorityOrigin);
            bool playerActor = actor != null
                && actor.Faction == Faction.OfPlayer;
            int actorId = actor != null ? actor.thingIDNumber : -1;
            return new CAIntentContext(episodeId, intentOrigin, controller,
                issuerId >= 0 ? issuerId : actorId,
                behaviorKey: behaviorKey,
                authorityOrigin: authorityOrigin,
                authorityIdentity: authorityIdentity,
                ownershipScope: ownershipScope ?? "pawn behavior",
                ownerId: ownerId >= 0 ? ownerId : actorId,
                creationTier: playerActor
                    ? AutonomyComponent.TierOf(actor)
                    : CAInitiativeTier.Standard,
                targetOrDemand: targetOrDemand);
        }

        public static CAIntentContext ActorInitiated(Pawn actor,
            CAIntentController controller, string behaviorKey = null,
            string targetOrDemand = null, int episodeId = 0)
        {
            bool playerActor = actor != null
                && actor.Faction == Faction.OfPlayer;
            return Authorized(actor, controller, behaviorKey,
                playerActor ? CAAuthorityOrigin.PlayerDelegated
                    : CAAuthorityOrigin.NativeDuty,
                playerActor ? "player delegation" : "native faction duty",
                playerActor ? "pawn behavior" : "NPC doctrine",
                targetOrDemand, episodeId);
        }

        public static CAIntentContext AutomaticDefense(Pawn actor,
            int episodeId = 0, string behaviorKey = null,
            string authorityIdentity = null, string targetOrDemand = null)
        {
            return Authorized(actor, CAIntentController.RaidDefense,
                behaviorKey, CAAuthorityOrigin.AutomaticDefense,
                authorityIdentity ?? "automatic defense",
                "defensive tasking", targetOrDemand, episodeId,
                CAIntentOrigin.AutomaticDefense);
        }

        public static CAIntentContext Continuation(Pawn actor,
            CAIntentController controller, string behaviorKey = null)
        {
            CAIntentContext parent;
            if (actor != null && CATactical.TryGetContext(actor, out parent)
                && parent.IsValid)
                return new CAIntentContext(parent.EpisodeId,
                    CAIntentOrigin.Continuation, controller, parent.IssuerId,
                    behaviorKey: behaviorKey ?? parent.BehaviorKey,
                    authorityOrigin: CAAuthorityOrigin.Continuation,
                    authorityIdentity: parent.AuthorityIdentity,
                    ownershipScope: parent.OwnershipScope,
                    ownerId: parent.OwnerId,
                    creationTier: parent.CreationTier,
                    targetOrDemand: parent.TargetOrDemand,
                    terminationCondition: parent.TerminationCondition,
                    createdTick: parent.CreatedTick);
            return ActorInitiated(actor, controller,
                behaviorKey: behaviorKey);
        }

        public static CAIntentContext CombatContinuation(Pawn actor,
            Pawn target, CAIntentController controller)
        {
            CAIntentContext parent;
            if (actor != null && CATactical.TryGetContext(actor, out parent)
                && parent.IsValid)
                return new CAIntentContext(parent.EpisodeId,
                    CAIntentOrigin.Continuation, controller, parent.IssuerId,
                    behaviorKey: parent.BehaviorKey,
                    authorityOrigin: CAAuthorityOrigin.Continuation,
                    authorityIdentity: parent.AuthorityIdentity,
                    ownershipScope: parent.OwnershipScope,
                    ownerId: parent.OwnerId,
                    creationTier: parent.CreationTier,
                    targetOrDemand: parent.TargetOrDemand,
                    terminationCondition: parent.TerminationCondition,
                    createdTick: parent.CreatedTick);

            MovementRecord movement;
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (actor != null
                && movements.TryGetValue(actor.thingIDNumber, out movement)
                && movement.Context.IsValid && now - movement.Tick <= 1200
                && (target == null || movement.TargetId < 0
                    || movement.TargetId == target.thingIDNumber))
                return new CAIntentContext(movement.Context.EpisodeId,
                    CAIntentOrigin.Continuation, controller,
                    movement.Context.IssuerId,
                    behaviorKey: movement.Context.BehaviorKey,
                    authorityOrigin: CAAuthorityOrigin.Continuation,
                    authorityIdentity: movement.Context.AuthorityIdentity,
                    ownershipScope: movement.Context.OwnershipScope,
                    ownerId: movement.Context.OwnerId,
                    creationTier: movement.Context.CreationTier,
                    targetOrDemand: movement.Context.TargetOrDemand,
                    terminationCondition: movement.Context.TerminationCondition,
                    createdTick: movement.Context.CreatedTick);
            return ActorInitiated(actor, controller);
        }

        public static CAIntentContext Restored(Pawn actor,
            CAIntentController controller, int episodeId,
            string behaviorKey = null, string authorityIdentity = null,
            string targetOrDemand = null)
        {
            if (episodeId <= 0) episodeId = NewEpisode();
            ObserveEpisode(episodeId);
            return new CAIntentContext(episodeId, CAIntentOrigin.SaveRestore,
                controller, actor != null ? actor.thingIDNumber : -1,
                behaviorKey: behaviorKey,
                authorityOrigin: CAAuthorityOrigin.SaveRestore,
                authorityIdentity: authorityIdentity ?? "restored intent",
                ownershipScope: "restored behavior",
                ownerId: actor != null ? actor.thingIDNumber : -1,
                creationTier: actor != null
                    ? AutonomyComponent.TierOf(actor)
                    : CAInitiativeTier.Standard,
                targetOrDemand: targetOrDemand);
        }

        public static void RecordMovement(Pawn pawn, CAIntentContext context,
            IntVec3 from, IntVec3 to, Pawn target = null)
        {
            if (pawn == null || !to.IsValid) return;
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            movements[pawn.thingIDNumber] = new MovementRecord
            {
                Context = context,
                From = from,
                To = to,
                Tick = now,
                TargetId = target != null ? target.thingIDNumber : -1,
                Reconstructed = false
            };
            if (context.Controller == CAIntentController.SelfPreservation
                && from.IsValid) Avoid(pawn, from, now + 900);
        }

        private static CAIntentContext ReconstructEvasionMovement(Pawn pawn,
            Job current)
        {
            int episode = NewEpisode();
            CAIntentContext context = new CAIntentContext(episode,
                CAIntentOrigin.Continuation,
                CAIntentController.ExplosiveEvasion,
                pawn != null ? pawn.thingIDNumber : -1);
            if (pawn == null || current == null || !current.targetA.IsValid)
                return context;
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            current.count = episode;
            evasionEpisodes[pawn.thingIDNumber] = episode;
            movements[pawn.thingIDNumber] = new MovementRecord
            {
                Context = context,
                From = pawn.Position,
                To = current.targetA.Cell,
                Tick = now,
                TargetId = -1,
                Reconstructed = true
            };
            return context;
        }

        internal static bool TryGetCurrentMovementContext(Pawn pawn, Job job,
            out CAIntentContext context)
        {
            context = default(CAIntentContext);
            if (pawn == null || job == null || !job.targetA.IsValid)
                return false;
            if (CABehaviorIntentMapComponent.For(pawn.Map)?.TryGet(pawn, job,
                    out context) == true && context.IsValid)
            {
                RecordMovement(pawn, context, pawn.Position,
                    job.targetA.Cell);
                return true;
            }
            MovementRecord movement;
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (!movements.TryGetValue(pawn.thingIDNumber, out movement)
                || !movement.Context.IsValid || now < movement.Tick
                || now - movement.Tick > 1200
                || movement.To != job.targetA.Cell) return false;
            context = movement.Context;
            return true;
        }

        // A new hit at the destination is direct evidence that the last supposedly
        // safer cell did not solve the problem. Keep it out of candidate sets long
        // enough for the other combat controller to choose a genuinely different plan.
        public static void NoteHarmAt(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned) return;
            MovementRecord record;
            int now = Find.TickManager.TicksGame;
            if (!movements.TryGetValue(pawn.thingIDNumber, out record)
                || now - record.Tick > 1200
                || !pawn.Position.InHorDistOf(record.To, 2.5f)) return;
            Avoid(pawn, record.To, now + 1200);
        }

        private static void Avoid(Pawn pawn, IntVec3 cell, int untilTick)
        {
            List<AvoidedCell> cells;
            if (!avoided.TryGetValue(pawn.thingIDNumber, out cells))
            {
                cells = new List<AvoidedCell>();
                avoided[pawn.thingIDNumber] = cells;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Cell != cell) continue;
                cells[i].UntilTick = Mathf.Max(cells[i].UntilTick, untilTick);
                return;
            }
            cells.Add(new AvoidedCell { Cell = cell, UntilTick = untilTick });
        }

        public static bool IsRecentlyUnsafe(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || !cell.IsValid) return false;
            List<AvoidedCell> cells;
            if (!avoided.TryGetValue(pawn.thingIDNumber, out cells)) return false;
            int now = Find.TickManager.TicksGame;
            bool found = false;
            for (int i = cells.Count - 1; i >= 0; i--)
            {
                if (cells[i].UntilTick <= now) { cells.RemoveAt(i); continue; }
                if (cells[i].Cell == cell) found = true;
            }
            if (cells.Count == 0) avoided.Remove(pawn.thingIDNumber);
            return found;
        }

        public static float HazardScore(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Map == null || !cell.InBounds(pawn.Map)) return 1f;
            Map map = pawn.Map;
            if (cell.ContainsStaticFire(map)) return 1f;
            float tox = map.gasGrid != null
                ? map.gasGrid.DensityPercentAt(cell, GasType.ToxGas) : 0f;
            float rot = map.gasGrid != null
                ? map.gasGrid.DensityPercentAt(cell, GasType.RotStink) : 0f;
            float deadlife = map.gasGrid != null && ModsConfig.AnomalyActive
                ? map.gasGrid.DensityPercentAt(cell, GasType.DeadlifeDust) : 0f;
            float pollution = map.pollutionGrid != null
                && map.pollutionGrid.IsPolluted(cell) ? 0.18f : 0f;
            return Mathf.Clamp01(tox + rot * 0.55f + deadlife * 0.80f + pollution);
        }

        internal static bool HasImmediateIncomingHazard(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return false;
            List<IncomingHazard> list;
            if (!incoming.TryGetValue(pawn.thingIDNumber, out list)) return false;
            int now = Find.TickManager.TicksGame;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                IncomingHazard hazard = list[i];
                if (hazard.MapId != pawn.Map.uniqueID
                    || now >= hazard.ImpactTick)
                {
                    list.RemoveAt(i);
                    continue;
                }
                if (pawn.Position.InHorDistOf(hazard.Center,
                        hazard.Radius + 0.5f)) return true;
            }
            if (list.Count == 0)
            {
                incoming.Remove(pawn.thingIDNumber);
                evasionEpisodes.Remove(pawn.thingIDNumber);
            }
            return false;
        }

        internal static bool ProjectileHazardousToPawn(
            ProjectileProperties props, Pawn pawn)
        {
            if (props == null || pawn == null) return false;
            DamageDef damage = props.damageDef;
            // Blast danger is physical rather than diplomatic. Friendly and
            // neutral explosives can injure or stun a pawn just as readily as a
            // hostile grenade; smoke/firefoam effects do neither.
            if (props.explosionRadius > 0f && damage != null
                && (damage.harmsHealth || damage.causeStun)) return true;
            if (props.postExplosionGasType != GasType.ToxGas
                || !ModsConfig.BiotechActive) return false;
            // Tox gas is actor-specific. Exposure immunity and complete toxic
            // resistance make it harmless; either remaining exposure or toxic-
            // buildup path is enough to justify evasive enrollment.
            if (GasUtility.IsAffectedByExposure(pawn)) return true;
            return pawn.GetStatValue(StatDefOf.ToxicResistance) < 0.999f
                && pawn.GetStatValue(
                    StatDefOf.ToxicEnvironmentResistance) < 0.999f;
        }

        public static void NoteIncomingProjectile(Projectile projectile,
            Thing launcher, Vector3 origin, LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget, bool alreadyLanded = false,
            int remainingDelayTicks = -1)
        {
            if (projectile == null || projectile.Map == null
                || projectile.def?.projectile == null) return;
            ProjectileProperties props = projectile.def.projectile;
            bool gas = props.postExplosionGasType.HasValue;
            if (props.explosionRadius <= 0f && !gas) return;
            Map map = projectile.Map;
            // usedTarget is the post-miss actual flight destination; intendedTarget
            // remains the evidence for who was deliberately aimed at.
            IntVec3 center = alreadyLanded
                ? projectile.Position : usedTarget.IsValid
                    ? usedTarget.Cell : intendedTarget.IsValid
                    ? intendedTarget.Cell : projectile.Position;
            if (!center.InBounds(map)) return;
            float radius = Mathf.Max(1.9f, props.explosionRadius)
                + (gas ? 1.5f : 0.5f);
            float distance = Vector3.Distance(origin.Yto0(), center.ToVector3Shifted().Yto0());
            float speed = Mathf.Max(0.001f, props.SpeedTilesPerTick);
            int eta = alreadyLanded ? 0
                : Mathf.Max(1, Mathf.CeilToInt(distance / speed));
            int delay = remainingDelayTicks >= 0
                ? remainingDelayTicks : Mathf.Max(0, props.explosionDelay);
            int impactTick = Find.TickManager.TicksGame + eta + delay;

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Awake()
                    || pawn.RaceProps == null || !pawn.RaceProps.Humanlike
                    || !ProjectileHazardousToPawn(props, pawn)
                    || !pawn.Position.InHorDistOf(center, radius + 2.5f)) continue;
                bool directlyTargeted = intendedTarget.HasThing
                    && intendedTarget.Thing == pawn;
                IntVec3 projectileCell = projectile.ExactPosition.ToIntVec3();
                bool seesLaunch = launcher != null && launcher.Spawned
                    && launcher.Map == map
                    && GenSight.LineOfSight(pawn.Position,
                        launcher.PositionHeld, map, true);
                bool seesProjectile = projectileCell.InBounds(map)
                    && GenSight.LineOfSight(pawn.Position,
                        projectileCell, map, true);
                if (!directlyTargeted && !seesLaunch && !seesProjectile) continue;

                List<IncomingHazard> list;
                if (!incoming.TryGetValue(pawn.thingIDNumber, out list))
                {
                    list = new List<IncomingHazard>();
                    incoming[pawn.thingIDNumber] = list;
                }
                IncomingHazard existing = null;
                for (int j = 0; j < list.Count; j++)
                    if (list[j].ProjectileId == projectile.thingIDNumber)
                    {
                        existing = list[j];
                        break;
                    }
                if (existing != null)
                {
                    existing.MapId = map.uniqueID;
                    existing.Center = center;
                    existing.Radius = radius;
                    existing.ImpactTick = impactTick;
                    existing.LauncherId = launcher != null
                        ? launcher.thingIDNumber : -1;
                    continue;
                }
                list.Add(new IncomingHazard
                {
                    ProjectileId = projectile.thingIDNumber,
                    MapId = map.uniqueID,
                    Center = center,
                    Radius = radius,
                    ImpactTick = impactTick,
                    LauncherId = launcher != null
                        ? launcher.thingIDNumber : -1,
                    OrderedDestination = IntVec3.Invalid,
                    OrderedTick = int.MinValue
                });
            }
        }

        // Launch callbacks are transient, while RimWorld scribes projectiles already
        // in flight. Rebuild the same actor-local warnings after load and on the
        // bounded live scan from the actual projectile, launcher, used destination,
        // and remaining visible flight path. The repeated scan can enroll a pawn who
        // newly enters the envelope or gains sight after launch.
        public static void RebuildIncomingProjectiles(Map map)
        {
            if (map == null) return;
            List<Thing> projectiles = map.listerThings
                .ThingsInGroup(ThingRequestGroup.Projectile);
            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i] as Projectile;
                if (projectile == null || projectile.Launcher == null) continue;
                bool landed = false;
                int remainingDelay = -1;
                try
                {
                    if (projectileLandedField != null)
                        landed = (bool)projectileLandedField.GetValue(projectile);
                    if (landed && projectile is Projectile_Explosive
                        && explosiveDelayField != null)
                        remainingDelay = (int)explosiveDelayField.GetValue(projectile);
                }
                catch
                {
                    // A changed engine layout falls back to conservative flight math;
                    // the real-reference compile and runtime patch receipt expose it.
                }
                NoteIncomingProjectile(projectile, projectile.Launcher,
                    projectile.ExactPosition, projectile.usedTarget,
                    projectile.intendedTarget, landed, remainingDelay);
            }
        }

        public static bool TryIncomingHazardEvasion(Pawn pawn, out Job job)
        {
            return ResolveIncomingHazardEvasion(pawn, out job)
                == CAIncomingEvasionResult.Active && job != null;
        }

        internal static CAIncomingEvasionResult ResolveIncomingHazardEvasion(
            Pawn pawn, out Job job)
        {
            job = null;
            if (pawn == null || pawn.Map == null || pawn.jobs == null)
                return CAIncomingEvasionResult.None;
            List<IncomingHazard> list;
            if (!incoming.TryGetValue(pawn.thingIDNumber, out list))
                return CAIncomingEvasionResult.None;
            int now = Find.TickManager.TicksGame;
            var active = new List<IncomingHazard>();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                IncomingHazard candidate = list[i];
                if (candidate.MapId != pawn.Map.uniqueID
                    || now >= candidate.ImpactTick)
                {
                    list.RemoveAt(i);
                    continue;
                }
                bool inside = pawn.Position.InHorDistOf(candidate.Center,
                    candidate.Radius + 0.5f);
                if (!inside)
                {
                    // Actual clearance, rather than job creation, completes this
                    // pawn-local response. Keep the projectile fact until expiry so
                    // a later unforced move back into the envelope can warn again.
                    candidate.OrderedDestination = IntVec3.Invalid;
                    continue;
                }
                active.Add(candidate);
            }
            if (list.Count == 0)
            {
                incoming.Remove(pawn.thingIDNumber);
                evasionEpisodes.Remove(pawn.thingIDNumber);
                evasionSearches.Remove(pawn.thingIDNumber);
            }
            if (active.Count == 0)
            {
                evasionSearches.Remove(pawn.thingIDNumber);
                return CAIncomingEvasionResult.None;
            }

            int earliestImpact = int.MaxValue;
            IncomingHazard earliestHazard = active[0];
            for (int i = 0; i < active.Count; i++)
                if (active[i].ImpactTick < earliestImpact)
                {
                    earliestImpact = active[i].ImpactTick;
                    earliestHazard = active[i];
                }
            int availableTicks = earliestImpact - now;
            if (availableTicks <= 0) return CAIncomingEvasionResult.None;

            // A route already in motion owns the actor even though this evaluation
            // has no new Job to emit. After load, the movement-context dictionary and
            // each projectile's OrderedDestination are transient; reconstruct the
            // ExplosiveEvasion identity when the persisted bounded move still clears
            // or materially improves every currently active envelope.
            Job current = pawn.CurJob;
            bool movingCombatRoute = current != null
                && current.def == CA_Defs.CombatMove
                && current.targetA.IsValid
                && pawn.pather != null && pawn.pather.Moving;
            bool destinationOwns = movingCombatRoute
                && (ClearsEveryEnvelope(current.targetA.Cell, active)
                    || EvasionDestinationOwnsEveryEnvelope(
                        current.targetA.Cell, active)
                    || WorstEnvelopeClearance(current.targetA.Cell, active)
                        > WorstEnvelopeClearance(pawn.Position, active) + 0.5f);
            if (destinationOwns)
            {
                CAIntentContext movementContext;
                bool knownEvasion = TryGetCurrentMovementContext(pawn, current,
                        out movementContext)
                    && movementContext.Controller
                        == CAIntentController.ExplosiveEvasion;
                if (!knownEvasion)
                {
                    movementContext = ReconstructEvasionMovement(pawn, current);
                    CATrace.Pawn(pawn,
                        "re-adopts persisted bounded move as the active incoming-explosive escape",
                        contact: earliestHazard.Center,
                        destination: current.targetA.Cell,
                        anchor: pawn.Position,
                        intent: movementContext);
                }
                else
                    evasionEpisodes[pawn.thingIDNumber] =
                        movementContext.EpisodeId;
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i].OrderedDestination
                        == current.targetA.Cell) continue;
                    active[i].OrderedDestination = current.targetA.Cell;
                    active[i].OrderedTick = now;
                }
                evasionSearches.Remove(pawn.thingIDNumber);

                // A standing CA lord may own the old finite move. Emit a detached
                // replacement so the caller can retire that standing order before
                // starting the survival route. An already-detached evasion remains
                // the current job and must not be restarted every evaluation.
                if (current.lord != null)
                {
                    job = MakeEvasionJob(pawn, current.targetA.Cell,
                        movementContext, earliestImpact, now,
                        CopyRemainingLockedRoute(current));
                }
                else job = current;
                return CAIncomingEvasionResult.Active;
            }

            bool progressing = current != null
                && current.def == CA_Defs.CombatMove
                && current.targetA.IsValid
                && (ClearsEveryEnvelope(current.targetA.Cell, active)
                    || EvasionDestinationOwnsEveryEnvelope(
                        current.targetA.Cell, active))
                && (pawn.pather != null && pawn.pather.Moving
                    || EvasionWasOrderedRecently(current.targetA.Cell,
                        active, now));
            if (progressing)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    // Preserve the issue tick for an already-owned route. Refreshing
                    // it merely because the job remained current could make a
                    // stopped or failed pather look newly progressing forever.
                    if (active[i].OrderedDestination
                        != current.targetA.Cell)
                    {
                        active[i].OrderedDestination = current.targetA.Cell;
                        active[i].OrderedTick = now;
                    }
                }
                job = current;
                return CAIncomingEvasionResult.Active;
            }
            bool retryDue = false;
            for (int i = 0; i < active.Count; i++)
                if (active[i].LastFailedEvaluationTick < 0
                    || now - active[i].LastFailedEvaluationTick >= 15)
                {
                    retryDue = true;
                    break;
                }
            if (!retryDue) return CAIncomingEvasionResult.None;

            int signature = EvasionHazardSignature(active);
            EvasionSearchState search;
            if (!evasionSearches.TryGetValue(pawn.thingIDNumber, out search)
                || search.Origin != pawn.Position
                || search.HazardSignature != signature)
            {
                float currentClearance = WorstEnvelopeClearance(
                    pawn.Position, active);
                var clearingCandidates = new List<EvasionCandidate>();
                var improvingCandidates = new List<EvasionCandidate>();
                float optimisticStepTicks = Mathf.Max(1f,
                    Mathf.Min(pawn.TicksPerMoveCardinal,
                        pawn.TicksPerMoveDiagonal) * 0.75f);
                int limit = GenRadial.NumCellsInRadius(9.9f);
                for (int i = 1; i < limit; i++)
                {
                    IntVec3 cell = pawn.Position
                        + GenRadial.RadialPattern[i];
                    if (!cell.InBounds(pawn.Map)
                        || !cell.InAllowedArea(pawn)
                        || IsRecentlyUnsafe(pawn, cell)) continue;
                    float travel = pawn.Position.DistanceTo(cell);
                    // This is only an optimistic shortlist gate. The stable
                    // candidate cells are revalidated and measured below through
                    // RimWorld's actual pathfinder, including terrain, doors,
                    // detours, hazards, and shoreline crossings.
                    if (travel * optimisticStepTicks
                        > availableTicks * 1.15f) continue;
                    float cover = 0f;
                    for (int j = 0; j < active.Count; j++)
                        cover += CoverUtility.CalculateOverallBlockChance(
                            cell, active[j].Center, pawn.Map);
                    cover /= active.Count;
                    float clearance = WorstEnvelopeClearance(cell, active);
                    bool newWater = !pawn.Position.GetTerrain(pawn.Map).IsWater
                        && cell.GetTerrain(pawn.Map).IsWater;
                    float score = clearance * 0.80f
                        + cover * 2.1f
                        - travel * 0.35f
                        - HazardScore(pawn, cell) * 6f
                        - (newWater ? 1.25f : 0f);
                    if (clearance > 0.4f)
                    {
                        clearingCandidates.Add(new EvasionCandidate
                        {
                            Cell = cell,
                            PreliminaryScore = score
                        });
                    }
                    else if (clearance > currentClearance + 0.5f)
                    {
                        float progressScore = clearance * 1.4f
                            - travel * 0.10f + cover
                            - HazardScore(pawn, cell) * 5f
                            - (newWater ? 1.25f : 0f);
                        improvingCandidates.Add(new EvasionCandidate
                        {
                            Cell = cell,
                            PreliminaryScore = progressScore
                        });
                    }
                }
                clearingCandidates.Sort(CompareEvasionCandidates);
                improvingCandidates.Sort(CompareEvasionCandidates);
                search = new EvasionSearchState
                {
                    Origin = pawn.Position,
                    HazardSignature = signature,
                    ClearingCandidates = clearingCandidates,
                    ImprovingCandidates = improvingCandidates
                };
                evasionSearches[pawn.thingIDNumber] = search;
            }
            int clearingBudget = EvasionClearingPathBudgetPerTick;
            bool clearingExhausted;
            EvasionCandidate selected = BestReachableEvasionRoute(pawn,
                search.ClearingCandidates, availableTicks,
                ref search.ClearingCursor, ref clearingBudget,
                out clearingExhausted);
            bool clearsEnvelope = selected != null;
            int improvingBudget = EvasionImprovingPathBudgetPerTick;
            bool improvingExhausted;
            EvasionCandidate partial = BestReachableEvasionRoute(pawn,
                    search.ImprovingCandidates, availableTicks,
                    ref search.ImprovingCursor, ref improvingBudget,
                    out improvingExhausted);
            if (selected == null) selected = partial;
            if (selected == null
                && (!clearingExhausted || !improvingExhausted))
                return CAIncomingEvasionResult.Pending;
            if (selected == null)
            {
                evasionSearches.Remove(pawn.thingIDNumber);
                for (int i = 0; i < active.Count; i++)
                    active[i].LastFailedEvaluationTick = now;
                CAIntentContext failed = EvasionContext(pawn);
                CATrace.Skip(pawn, "explosive evasion",
                    "no actual path reaches a cell that improves all "
                    + active.Count
                    + " simultaneous predicted envelopes before the earliest deadline",
                    contact: earliestHazard.Center, anchor: pawn.Position,
                    intent: failed);
                return CAIncomingEvasionResult.None;
            }

            evasionSearches.Remove(pawn.thingIDNumber);
            CAIntentContext context = EvasionContext(pawn);
            job = MakeEvasionJob(pawn, selected.Cell, context,
                earliestImpact, now, selected.RouteCells);
            if (job == null)
            {
                for (int i = 0; i < active.Count; i++)
                    active[i].LastFailedEvaluationTick = now;
                CATrace.Skip(pawn, "explosive evasion",
                    "the selected path changed before it could be locked; retry remains eligible",
                    contact: earliestHazard.Center, anchor: pawn.Position,
                    intent: context);
                return CAIncomingEvasionResult.None;
            }
            bool player = pawn.Faction == Faction.OfPlayer;
            var gateContext = CABehaviorContext.ForPawn(pawn,
                player ? CAAuthorityOrigin.PlayerDelegated
                    : CAAuthorityOrigin.NativeDuty,
                authoritySatisfied: true, knowledgeSatisfied: true,
                knowledgeFresh: true, liveValidated: true,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                knowledgeConfidence: 1f, knowledgeUncertainty: 0f,
                authorityBasis: player
                    ? "player immediate-survival permission"
                    : "native immediate-survival duty",
                knowledgeBasis: "direct incoming explosive trajectory",
                owner: player ? "pawn immediate survival"
                    : "NPC immediate survival");
            CABehaviorDecision gateDecision;
            CAIntentContext authorizedContext;
            if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(pawn, job,
                    "survival.immediate_evasion",
                    CAIntentController.ExplosiveEvasion, gateContext,
                    context.EpisodeId, out gateDecision,
                    out authorizedContext,
                    "clear simultaneous explosive envelopes at "
                        + earliestHazard.Center,
                    player ? "pawn immediate survival"
                        : "NPC immediate survival",
                    lifetimeTicks: Math.Max(600,
                        earliestImpact - now + 600)))
            {
                job = null;
                return CAIncomingEvasionResult.None;
            }
            context = authorizedContext;
            for (int i = 0; i < active.Count; i++)
            {
                active[i].OrderedDestination = selected.Cell;
                active[i].OrderedTick = now;
                active[i].LastFailedEvaluationTick = -1;
            }
            RecordMovement(pawn, context, pawn.Position, selected.Cell);
            CATrace.Pawn(pawn, "selects incoming-explosive route to "
                + selected.Cell
                + " (simultaneous envelopes " + active.Count
                + ", earliest impact " + earliestHazard.Center + " in "
                + (earliestImpact - now) + " ticks, "
                + (clearsEnvelope ? "clears every envelope"
                    : "best shared partial clearance")
                + ", actual path " + selected.RouteSteps + " steps / "
                + selected.RouteTicks + " estimated sprint ticks"
                + ", new-water entries " + selected.NewWaterEntries
                + ", hazardous route cells " + selected.HazardCells
                + "; "
                + CACombatConditionSnapshot.Capture(pawn).TraceText() + ")",
                contact: earliestHazard.Center,
                destination: selected.Cell,
                anchor: pawn.Position,
                intent: context);
            return CAIncomingEvasionResult.Active;
        }

        internal static bool TryGetIncomingEvasionDiagnosticState(Pawn pawn,
            out CAIncomingEvasionDiagnosticState state)
        {
            state = default(CAIncomingEvasionDiagnosticState);
            if (pawn == null || pawn.Map == null) return false;

            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            List<IncomingHazard> hazards;
            incoming.TryGetValue(pawn.thingIDNumber, out hazards);
            Job current = pawn.CurJob;
            bool currentMove = current != null
                && current.def == CA_Defs.CombatMove
                && current.targetA.IsValid;
            IntVec3 currentDestination = currentMove
                ? current.targetA.Cell : IntVec3.Invalid;

            int observedCount = 0;
            int activeCount = 0;
            IncomingHazard earliestObserved = null;
            IncomingHazard earliestActive = null;
            IntVec3 orderedDestination = IntVec3.Invalid;
            int lastFailedTick = -1;
            float currentWorst = float.MaxValue;
            float destinationWorst = float.MaxValue;
            bool allActiveOwnCurrentDestination = currentMove;
            bool currentDestinationOrderedRecently = false;
            if (hazards != null)
                for (int i = 0; i < hazards.Count; i++)
                {
                    IncomingHazard hazard = hazards[i];
                    if (hazard.MapId != pawn.Map.uniqueID
                        || now >= 0 && now >= hazard.ImpactTick) continue;
                    observedCount++;
                    if (earliestObserved == null
                        || hazard.ImpactTick < earliestObserved.ImpactTick)
                        earliestObserved = hazard;
                    if (!orderedDestination.IsValid
                        && hazard.OrderedDestination.IsValid)
                        orderedDestination = hazard.OrderedDestination;
                    if (hazard.LastFailedEvaluationTick > lastFailedTick)
                        lastFailedTick = hazard.LastFailedEvaluationTick;
                    if (!pawn.Position.InHorDistOf(hazard.Center,
                            hazard.Radius + 0.5f)) continue;
                    activeCount++;
                    if (earliestActive == null
                        || hazard.ImpactTick < earliestActive.ImpactTick)
                        earliestActive = hazard;
                    currentWorst = Mathf.Min(currentWorst,
                        pawn.Position.DistanceTo(hazard.Center)
                            - hazard.Radius);
                    if (currentMove)
                    {
                        destinationWorst = Mathf.Min(destinationWorst,
                            currentDestination.DistanceTo(hazard.Center)
                                - hazard.Radius);
                        if (hazard.OrderedDestination != currentDestination)
                            allActiveOwnCurrentDestination = false;
                        else if (now >= 0
                            && now - hazard.OrderedTick <= 15)
                            currentDestinationOrderedRecently = true;
                    }
                }

            EvasionSearchState search;
            bool hasSearch = evasionSearches.TryGetValue(
                pawn.thingIDNumber, out search) && search != null;
            int clearingCount = hasSearch && search.ClearingCandidates != null
                ? search.ClearingCandidates.Count : 0;
            int improvingCount = hasSearch && search.ImprovingCandidates != null
                ? search.ImprovingCandidates.Count : 0;
            bool searchPending = hasSearch && activeCount > 0
                && (search.ClearingCursor < clearingCount
                    || search.ImprovingCursor < improvingCount);

            MovementRecord movement;
            bool hasMovement = movements.TryGetValue(pawn.thingIDNumber,
                    out movement)
                && movement != null && movement.Context.IsValid
                && movement.Context.Controller
                    == CAIntentController.ExplosiveEvasion
                && (now < 0 || now >= movement.Tick
                    && now - movement.Tick <= 1200);
            bool moving = pawn.pather != null && pawn.pather.Moving;
            bool routeOwned = currentMove && activeCount > 0
                && (moving && (destinationWorst > 0.4f
                        || destinationWorst > currentWorst + 0.5f
                        || allActiveOwnCurrentDestination)
                    || (destinationWorst > 0.4f
                            || allActiveOwnCurrentDestination)
                        && currentDestinationOrderedRecently);
            CAIncomingEvasionResult status = routeOwned
                ? CAIncomingEvasionResult.Active
                : searchPending ? CAIncomingEvasionResult.Pending
                    : CAIncomingEvasionResult.None;

            IncomingHazard selectedHazard = earliestActive
                ?? earliestObserved;
            int episode = -1;
            CAIntentOrigin intentOrigin = CAIntentOrigin.Unknown;
            IntVec3 origin = hasSearch ? search.Origin : pawn.Position;
            IntVec3 destination = currentMove ? currentDestination
                : orderedDestination;
            bool reconstructed = false;
            if (hasMovement)
            {
                episode = movement.Context.EpisodeId;
                intentOrigin = movement.Context.Origin;
                origin = movement.From;
                if (movement.To.IsValid) destination = movement.To;
                reconstructed = movement.Reconstructed;
            }
            else
            {
                int storedEpisode;
                if (evasionEpisodes.TryGetValue(pawn.thingIDNumber,
                        out storedEpisode))
                {
                    episode = storedEpisode;
                    intentOrigin = CAIntentOrigin.Continuation;
                }
            }

            state = new CAIncomingEvasionDiagnosticState(status,
                pawn.Map.uniqueID, observedCount, activeCount,
                selectedHazard != null ? selectedHazard.ProjectileId : -1,
                selectedHazard != null ? selectedHazard.LauncherId : -1,
                selectedHazard != null
                    ? selectedHazard.Center : IntVec3.Invalid,
                selectedHazard != null ? selectedHazard.Radius : 0f,
                selectedHazard != null ? selectedHazard.ImpactTick : -1,
                selectedHazard != null && now >= 0
                    ? Mathf.Max(0, selectedHazard.ImpactTick - now) : -1,
                episode, intentOrigin, origin, destination, reconstructed,
                hasSearch, hasSearch ? search.HazardSignature : 0,
                hasSearch ? search.ClearingCursor : 0, clearingCount,
                EvasionClearingPathBudgetPerTick,
                hasSearch ? search.ImprovingCursor : 0, improvingCount,
                EvasionImprovingPathBudgetPerTick, lastFailedTick);
            return observedCount > 0 || hasSearch || hasMovement;
        }

        private static Job MakeEvasionJob(Pawn pawn, IntVec3 destination,
            CAIntentContext context, int impactTick, int now,
            IList<IntVec3> assessedRoute)
        {
            Job result = CAImmediateCombat.MakeLockedCombatMoveJob(pawn,
                destination, Mathf.Max(90, impactTick - now + 120),
                assessedRoute);
            if (result == null) return null;
            result.count = context.EpisodeId;
            return result;
        }

        private static List<IntVec3> CopyRemainingLockedRoute(Job job)
        {
            if (job?.targetQueueA == null || job.targetQueueA.Count == 0)
                return null;
            var result = new List<IntVec3>(job.targetQueueA.Count);
            for (int i = 0; i < job.targetQueueA.Count; i++)
                if (job.targetQueueA[i].IsValid)
                    result.Add(job.targetQueueA[i].Cell);
            return result;
        }

        private static EvasionCandidate BestReachableEvasionRoute(Pawn pawn,
            List<EvasionCandidate> candidates, int availableTicks,
            ref int cursor, ref int pathBudget, out bool exhausted)
        {
            exhausted = true;
            if (pawn == null || pawn.Map == null || candidates == null
                || candidates.Count == 0) return null;
            int deadline = Mathf.Max(1,
                Mathf.FloorToInt(availableTicks * 0.90f));
            EvasionCandidate best = null;
            float bestScore = float.MinValue;
            int validRoutes = 0;
            while (cursor < candidates.Count && pathBudget > 0)
            {
                EvasionCandidate candidate = candidates[cursor++];
                if (!candidate.Cell.InBounds(pawn.Map)
                    || !candidate.Cell.WalkableBy(pawn.Map, pawn)
                    || !candidate.Cell.InAllowedArea(pawn)
                    || !pawn.Map.pawnDestinationReservationManager.CanReserve(
                        candidate.Cell, pawn)
                    || !pawn.CanReach(candidate.Cell, PathEndMode.OnCell,
                        Danger.Some)
                    || IsRecentlyUnsafe(pawn, candidate.Cell)) continue;
                pathBudget--;
                using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                    pawn.Position, candidate.Cell, pawn,
                    PathFinderCostTuning.For(pawn),
                    PathEndMode.OnCell))
                {
                    if (path == null || !path.Found
                        || path.NodesReversed == null
                        || path.NodesReversed.Count == 0) continue;
                    int routeTicks = Mathf.CeilToInt(path.TotalCost * 0.75f);
                    if (routeTicks > deadline) continue;

                    int waterEntries = 0;
                    int hazardCells = 0;
                    bool previousWater = pawn.Position
                        .GetTerrain(pawn.Map).IsWater;
                    IntVec3 previousCell = pawn.Position;
                    for (int node = path.NodesReversed.Count - 2;
                        node >= 0; node--)
                    {
                        IntVec3 routeCell = path.NodesReversed[node];
                        if (!routeCell.InBounds(pawn.Map)
                            || !previousCell.AdjacentTo8WayOrInside(routeCell)
                            || !routeCell.WalkableBy(pawn.Map, pawn)
                            || !routeCell.InAllowedArea(pawn))
                        {
                            hazardCells = int.MaxValue;
                            break;
                        }
                        previousCell = routeCell;
                        bool water = routeCell.GetTerrain(pawn.Map).IsWater;
                        if (water && !previousWater) waterEntries++;
                        previousWater = water;
                        if (HazardScore(pawn, routeCell) > 0.01f)
                            hazardCells++;
                    }
                    if (hazardCells == int.MaxValue) continue;
                    float routeScore = candidate.PreliminaryScore
                        - routeTicks / (float)Mathf.Max(1, deadline) * 0.80f
                        - waterEntries * 1.75f
                        - hazardCells * 1.25f;
                    if (routeScore <= bestScore) continue;
                    bestScore = routeScore;
                    candidate.RouteTicks = routeTicks;
                    candidate.RouteSteps = Mathf.Max(0,
                        path.NodesReversed.Count - 1);
                    candidate.NewWaterEntries = waterEntries;
                    candidate.HazardCells = hazardCells;
                    candidate.RouteCells = new List<IntVec3>(
                        candidate.RouteSteps);
                    for (int node = path.NodesReversed.Count - 2;
                        node >= 0; node--)
                        candidate.RouteCells.Add(path.NodesReversed[node]);
                    best = candidate;
                    validRoutes++;
                    // Endpoint scoring has already ordered the shortlist. A few
                    // actual routes are enough to account for route water/hazard
                    // penalties without synchronously pathing every radial cell.
                    if (validRoutes >= 4) break;
                }
            }
            exhausted = cursor >= candidates.Count;
            return best;
        }

        private static int CompareEvasionCandidates(EvasionCandidate left,
            EvasionCandidate right)
        {
            int score = right.PreliminaryScore.CompareTo(
                left.PreliminaryScore);
            return score != 0 ? score : CellOrder(left.Cell, right.Cell);
        }

        private static int EvasionHazardSignature(
            List<IncomingHazard> hazards)
        {
            unchecked
            {
                int signature = 17;
                for (int i = 0; i < hazards.Count; i++)
                {
                    IncomingHazard hazard = hazards[i];
                    signature = signature * 31 + hazard.ProjectileId;
                    signature = signature * 31 + hazard.ImpactTick;
                    signature = signature * 31 + hazard.Center.x;
                    signature = signature * 31 + hazard.Center.z;
                    signature = signature * 31
                        + Mathf.RoundToInt(hazard.Radius * 100f);
                }
                return signature;
            }
        }

        private static int CellOrder(IntVec3 left, IntVec3 right)
        {
            int x = left.x.CompareTo(right.x);
            return x != 0 ? x : left.z.CompareTo(right.z);
        }

        private static bool EvasionWasOrderedRecently(IntVec3 destination,
            List<IncomingHazard> hazards, int now)
        {
            for (int i = 0; i < hazards.Count; i++)
                if (hazards[i].OrderedDestination == destination
                    && now - hazards[i].OrderedTick <= 15) return true;
            return false;
        }

        private static bool ClearsEveryEnvelope(IntVec3 cell,
            List<IncomingHazard> hazards)
        {
            return WorstEnvelopeClearance(cell, hazards) > 0.4f;
        }

        private static bool EvasionDestinationOwnsEveryEnvelope(IntVec3 cell,
            List<IncomingHazard> hazards)
        {
            if (!cell.IsValid || hazards == null || hazards.Count == 0)
                return false;
            for (int i = 0; i < hazards.Count; i++)
                if (hazards[i].OrderedDestination != cell) return false;
            return true;
        }

        private static float WorstEnvelopeClearance(IntVec3 cell,
            List<IncomingHazard> hazards)
        {
            float worst = float.MaxValue;
            for (int i = 0; i < hazards.Count; i++)
                worst = Mathf.Min(worst,
                    cell.DistanceTo(hazards[i].Center) - hazards[i].Radius);
            return worst;
        }

        private static CAIntentContext EvasionContext(Pawn pawn)
        {
            int episode;
            if (pawn != null && evasionEpisodes.TryGetValue(
                    pawn.thingIDNumber, out episode))
                return new CAIntentContext(episode,
                    CAIntentOrigin.Continuation,
                    CAIntentController.ExplosiveEvasion,
                    pawn.thingIDNumber);
            episode = NewEpisode();
            if (pawn != null) evasionEpisodes[pawn.thingIDNumber] = episode;
            return ActorInitiated(pawn,
                CAIntentController.ExplosiveEvasion,
                "survival.immediate_evasion", episodeId: episode);
        }
    }

    [HarmonyPatch]
    public static class Patch_CAIncomingExplosiveLaunch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Projectile), nameof(Projectile.Launch),
                new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo),
                    typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool),
                    typeof(Thing), typeof(ThingDef) });
        }

        public static void Postfix(Projectile __instance, Thing launcher,
            Vector3 origin, LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget)
        {
            try
            {
                CACombatIntent.NoteIncomingProjectile(__instance, launcher,
                    origin, usedTarget, intendedTarget);
            }
            catch { }
        }
    }
}
