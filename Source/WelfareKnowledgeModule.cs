using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    public enum WelfareState
    {
        Unassessed,
        Assessed,
        Deceased,
        Missing,
        Resolved
    }

    public enum WelfareEvidenceSource
    {
        Visual,
        MissionEvent,
        ClinicalObservation,
        PhysicalCustody,
        SelfObservation
    }

    public readonly struct WelfareEvidence
    {
        public readonly WelfareEvidenceSource Source;
        public readonly int SourceId;
        public readonly float Confidence;
        public readonly int UncertaintyTicks;
        public readonly CommunicationChannel DeliveryChannel;
        public readonly int ReporterId;

        public bool IsDirect => DeliveryChannel == CommunicationChannel.None;
        public bool IsRelayable => Source != WelfareEvidenceSource.MissionEvent;

        public WelfareEvidence(WelfareEvidenceSource source, int sourceId,
            float confidence, int uncertaintyTicks,
            CommunicationChannel deliveryChannel, int reporterId)
        {
            Source = source;
            SourceId = sourceId;
            Confidence = Mathf.Clamp01(confidence);
            UncertaintyTicks = Math.Max(0, uncertaintyTicks);
            DeliveryChannel = deliveryChannel;
            ReporterId = reporterId;
        }

        public WelfareEvidence RelayedThrough(CommunicationChannel channel,
            int reporterId)
        {
            return new WelfareEvidence(Source, SourceId, Confidence,
                UncertaintyTicks, channel, reporterId);
        }
    }

    public readonly struct WelfareFactSnapshot
    {
        public readonly int SubjectId;
        public readonly IntVec3 Cell;
        public readonly int SourceTick;
        public readonly int AcquiredTick;
        public readonly int Revision;
        public readonly WelfareState State;
        public readonly int StateTick;
        public readonly int AssessedTick;
        public readonly int EstimatedDeathTick;
        public readonly int DiagnosticTier;
        public readonly bool ObservedNeedsTend;
        public readonly bool ObservedDowned;
        public readonly bool ObservedTemperatureDanger;
        public readonly WelfareEvidence Evidence;

        public WelfareFactSnapshot(int subjectId, IntVec3 cell,
            int sourceTick, int acquiredTick, int revision, WelfareState state,
            int stateTick, int assessedTick, int estimatedDeathTick, int diagnosticTier,
            bool observedNeedsTend, bool observedDowned,
            bool observedTemperatureDanger, WelfareEvidence evidence)
        {
            SubjectId = subjectId;
            Cell = cell;
            SourceTick = sourceTick;
            AcquiredTick = acquiredTick;
            Revision = revision;
            State = state;
            StateTick = stateTick;
            AssessedTick = assessedTick;
            EstimatedDeathTick = estimatedDeathTick;
            DiagnosticTier = diagnosticTier;
            ObservedNeedsTend = observedNeedsTend;
            ObservedDowned = observedDowned;
            ObservedTemperatureDanger = observedTemperatureDanger;
            Evidence = evidence;
        }

        public bool Actionable => State == WelfareState.Unassessed
            || State == WelfareState.Assessed;
    }

    public enum AccountabilityState
    {
        Monitoring,
        Overdue,
        Checking,
        ConcernReported,
        Resolved
    }

    internal enum WelfareCheckOrigin
    {
        WelfareFact,
        Accountability
    }

    public readonly struct AccountabilitySnapshot
    {
        public readonly int SubjectId;
        public readonly IntVec3 LastConfirmedCell;
        public readonly int ExpectedSinceTick;
        public readonly int LastConfirmedTick;
        public readonly int Revision;
        public readonly int GraceTicks;
        public readonly AccountabilityState State;
        public readonly int StateTick;

        public AccountabilitySnapshot(int subjectId, IntVec3 lastConfirmedCell,
            int expectedSinceTick, int lastConfirmedTick, int revision,
            int graceTicks, AccountabilityState state, int stateTick)
        {
            SubjectId = subjectId;
            LastConfirmedCell = lastConfirmedCell;
            ExpectedSinceTick = expectedSinceTick;
            LastConfirmedTick = lastConfirmedTick;
            Revision = revision;
            GraceTicks = graceTicks;
            State = state;
            StateTick = stateTick;
        }
    }

    public class CAAccountabilityRecord : IExposable
    {
        public int ownerId;
        public int subjectId;
        public IntVec3 lastConfirmedCell = IntVec3.Invalid;
        public int expectedSinceTick;
        public int lastConfirmedTick = -1;
        public int revision = 1;
        public int graceTicks = 1200;
        public AccountabilityState state = AccountabilityState.Monitoring;
        public int stateTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ownerId, "ownerId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref lastConfirmedCell, "lastConfirmedCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref expectedSinceTick, "expectedSinceTick", 0);
            Scribe_Values.Look(ref lastConfirmedTick, "lastConfirmedTick", -1);
            Scribe_Values.Look(ref revision, "revision", 1);
            Scribe_Values.Look(ref graceTicks, "graceTicks", 1200);
            Scribe_Values.Look(ref state, "state",
                AccountabilityState.Monitoring);
            Scribe_Values.Look(ref stateTick, "stateTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && revision < 1)
                revision = 1;
        }
    }

    // Gross welfare and clinical estimates are pawn-private facts. Storage contains
    // scalar identity and remembered cells only: a consumer cannot receive a live
    // Pawn reference and accidentally turn memory into tracking.
    public partial class KnowledgeMapComponent
    {
        private sealed class WelfareFact
        {
            public int subjectId;
            public IntVec3 lastKnown;
            public int sourceTick;
            public int lastRefreshTick;
            public int acquiredTick;
            public int revision = 1;
            public WelfareState state;
            public int stateTick;
            public int assessedTick = -1;
            public int estimatedDeathTick = int.MaxValue;
            public int diagnosticTier;
            public bool observedNeedsTend;
            public bool observedDowned;
            public bool observedTemperatureDanger;
            public WelfareEvidence evidence;
        }

        private sealed class WelfareDelivery
        {
            public Pawn listener;
            public WelfareFactSnapshot fact;
            public WelfareEvidence evidence;
        }

        private readonly struct PendingWelfareCheck
        {
            public readonly int SubjectId;
            public readonly int SourceTick;
            public readonly int StateTick;
            public readonly int Revision;
            public readonly IntVec3 Cell;
            public readonly WelfareCheckOrigin Origin;

            public PendingWelfareCheck(int subjectId, int sourceTick,
                int stateTick, int revision, IntVec3 cell,
                WelfareCheckOrigin origin)
            {
                SubjectId = subjectId;
                SourceTick = sourceTick;
                StateTick = stateTick;
                Revision = revision;
                Cell = cell;
                Origin = origin;
            }
        }

        private readonly Dictionary<int, List<WelfareFact>> knownWelfare =
            new Dictionary<int, List<WelfareFact>>();
        private readonly Dictionary<int, PendingWelfareCheck> pendingWelfareJobs =
            new Dictionary<int, PendingWelfareCheck>();
        private List<CAAccountabilityRecord> accountability =
            new List<CAAccountabilityRecord>();
        private int accountabilityCooldown;

        // Gross state (standing, downed, deceased) can be noticed across clear
        // battlefield geometry. Exact clinical diagnosis remains separately local.
        private static readonly float WelfareSightRange = float.PositiveInfinity;
        private const float ClinicalSightRange = 6.9f;
        private const int WelfareStaleTicks = 7500;
        private const int AccountabilityInterval = 600;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref accountability, "CA_accountability",
                LookMode.Deep);
            if (accountability == null)
                accountability = new List<CAAccountabilityRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                accountability.RemoveAll(r => r == null || r.ownerId <= 0
                    || r.subjectId <= 0 || r.ownerId == r.subjectId);
        }

        public bool TryGetFreshWelfare(Pawn knower, int subjectId,
            out WelfareFactSnapshot fact)
        {
            fact = default;
            WelfareFact stored = FindWelfare(knower, subjectId);
            if (stored == null) return false;
            RefreshVisibleWelfare(knower, stored);
            if (Find.TickManager.TicksGame - stored.lastRefreshTick
                > WelfareStaleTicks)
                return false;
            fact = SnapshotOf(stored);
            return true;
        }

        public void CopyFreshWelfare(Pawn knower,
            List<WelfareFactSnapshot> result)
        {
            result.Clear();
            if (knower == null) return;
            List<WelfareFact> facts;
            if (!knownWelfare.TryGetValue(knower.thingIDNumber, out facts))
                return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < facts.Count; i++)
            {
                RefreshVisibleWelfare(knower, facts[i]);
                if (now - facts[i].lastRefreshTick <= WelfareStaleTicks)
                    result.Add(SnapshotOf(facts[i]));
            }
        }

        public void NoteMissionWelfare(Pawn knower, int subjectId,
            IntVec3 cell, int sourceTick)
        {
            if (knower == null || subjectId <= 0 || !cell.IsValid) return;
            var evidence = new WelfareEvidence(
                WelfareEvidenceSource.MissionEvent, subjectId, 0.9f, 0,
                CommunicationChannel.None, knower.thingIDNumber);
            NoteWelfareEvidence(knower, subjectId, cell, sourceTick,
                WelfareState.Unassessed, sourceTick, -1, int.MaxValue, 0,
                false, true, false, evidence);
        }

        internal void ImportMissionWelfare(Pawn knower,
            MissionCasualtyFact mission)
        {
            if (knower == null || mission == null || mission.beneficiary == null
                || !mission.lastKnownCell.IsValid) return;
            int tick = mission.assessedTick >= 0
                ? mission.assessedTick : mission.eventSourceTick;
            // Pre-a2-14 mission manifests did not save the event tick. Preserve
            // their authorization, but do not manufacture fresh general knowledge.
            if (tick < 0) return;
            WelfareState state = mission.state == MissionCasualtyState.Assessed
                ? WelfareState.Assessed
                : mission.state == MissionCasualtyState.Deceased
                    ? WelfareState.Deceased
                    : mission.state == MissionCasualtyState.Missing
                        ? WelfareState.Missing
                        : mission.state == MissionCasualtyState.Resolved
                            ? WelfareState.Resolved : WelfareState.Unassessed;
            WelfareEvidenceSource source = mission.assessedTick >= 0
                ? WelfareEvidenceSource.ClinicalObservation
                : WelfareEvidenceSource.MissionEvent;
            var evidence = new WelfareEvidence(source,
                mission.beneficiary.thingIDNumber, source ==
                    WelfareEvidenceSource.ClinicalObservation ? 1f : 0.9f,
                0, CommunicationChannel.None, knower.thingIDNumber);
            NoteWelfareEvidence(knower, mission.beneficiary.thingIDNumber,
                mission.lastKnownCell, tick, state, tick, mission.assessedTick,
                mission.estimatedDeathTick, mission.diagnosticTier,
                mission.observedNeedsTend, mission.observedDowned, false,
                evidence);
        }

        public void ForgetWelfare(Pawn knower, int subjectId)
        {
            if (knower == null) return;
            List<WelfareFact> facts;
            if (!knownWelfare.TryGetValue(knower.thingIDNumber, out facts))
                return;
            facts.RemoveAll(f => f.subjectId == subjectId);
            if (facts.Count == 0) knownWelfare.Remove(knower.thingIDNumber);
        }

        internal void MarkMissionWelfareMissing(Pawn knower, int subjectId,
            int eventSourceTick, IntVec3 expectedCell)
        {
            WelfareFact fact = FindWelfare(knower, subjectId);
            if (fact == null
                || fact.evidence.Source != WelfareEvidenceSource.MissionEvent
                || fact.sourceTick != eventSourceTick
                || fact.lastKnown != expectedCell) return;
            fact.state = WelfareState.Missing;
            fact.stateTick = Find.TickManager.TicksGame;
            fact.lastRefreshTick = fact.stateTick;
            fact.revision++;
        }

        internal bool NoteClinicalWelfare(Pawn assessor, Pawn subject,
            Thing visible, bool announce)
        {
            if (assessor == null || subject == null || visible == null
                || !CAClinicalObservation.CanClinicallyObserve(assessor,
                    subject, out Thing observed) || observed != visible)
                return false;

            int now = Find.TickManager.TicksGame;
            bool downed = subject.Downed;
            bool temperatureDanger = downed
                && CALifeSafety.InTemperatureDanger(subject);
            bool needsTend = false;
            WelfareState state;
            int estimatedDeathTick = int.MaxValue;
            int tier = CAClinicalObservation.DiagnosticTier(assessor);
            string diagnosis;
            WelfareFact prior = FindWelfare(assessor,
                subject.thingIDNumber);

            if (subject.Dead)
            {
                state = WelfareState.Deceased;
                downed = true;
                diagnosis = "deceased";
            }
            else
            {
                needsTend = subject.health != null
                    && HealthAIUtility.ShouldBeTendedNowByPlayer(subject)
                    && subject.health.HasHediffsNeedingTend();
                if (!needsTend && !downed && !temperatureDanger)
                {
                    state = WelfareState.Resolved;
                    diagnosis = "no immediate aid apparent";
                }
                else
                {
                    state = WelfareState.Assessed;
                    int estimate = CAClinicalObservation
                        .PerceivedBleedDeathTicks(assessor, subject);
                    estimatedDeathTick = estimate == int.MaxValue
                        ? int.MaxValue
                        : (int)Math.Min((long)int.MaxValue - 1L,
                            (long)now + estimate);
                    diagnosis = CAClinicalObservation.DiagnosisLabel(downed,
                        temperatureDanger, tier, estimate);
                }
            }

            var evidence = new WelfareEvidence(
                assessor.IsCarryingPawn(subject)
                    ? WelfareEvidenceSource.PhysicalCustody
                    : WelfareEvidenceSource.ClinicalObservation,
                subject.thingIDNumber, 1f, tier == 0 ? 15000
                    : tier == 1 ? 7500 : tier == 2 ? 2500 : 0,
                CommunicationChannel.None, assessor.thingIDNumber);
            bool repeatedStableDiagnosis = prior != null
                && prior.state == state
                && prior.lastKnown == visible.Position
                && prior.evidence.Source == evidence.Source
                && prior.evidence.IsDirect
                && prior.evidence.SourceId == evidence.SourceId
                && prior.evidence.ReporterId == evidence.ReporterId
                && prior.estimatedDeathTick == int.MaxValue
                && estimatedDeathTick == int.MaxValue
                && prior.diagnosticTier == tier
                && prior.observedNeedsTend == needsTend
                && prior.observedDowned == downed
                && prior.observedTemperatureDanger == temperatureDanger
                && !temperatureDanger;
            NoteWelfareEvidence(assessor, subject.thingIDNumber,
                visible.Position, now, state, now, now, estimatedDeathTick,
                tier, needsTend, downed, temperatureDanger, evidence);
            if (announce && !repeatedStableDiagnosis
                && visible.Position.IsValid
                && visible.Position.InBounds(assessor.Map))
                MoteMaker.ThrowText(visible.Position.ToVector3Shifted(),
                    assessor.Map, diagnosis,
                    CAClinicalObservation.DiagnosisColor(estimatedDeathTick ==
                        int.MaxValue ? int.MaxValue
                        : Math.Max(0, estimatedDeathTick - now)), 3.5f);
            if (!repeatedStableDiagnosis)
                CATrace.Pawn(assessor, "acquires welfare fact for "
                    + subject.LabelShort + ": " + diagnosis + " (Medical "
                    + CAClinicalObservation.SkillLevel(assessor,
                        SkillDefOf.Medicine) + ", Intellectual "
                    + CAClinicalObservation.SkillLevel(assessor,
                        SkillDefOf.Intellectual) + ")");
            return true;
        }

        internal bool CanActOnCurrentWelfare(Pawn knower, Pawn subject)
        {
            if (knower == null || subject == null) return false;
            WelfareFact fact = FindWelfare(knower, subject.thingIDNumber);
            if (fact == null || !SnapshotOf(fact).Actionable
                || !fact.evidence.IsDirect
                || Find.TickManager.TicksGame - fact.sourceTick > 90)
                return false;
            Thing visible;
            return CanDirectlyObserveSubject(knower, subject,
                    WelfareSightRange, out visible)
                && visible.Position == fact.lastKnown;
        }

        internal bool HasCurrentActionableWelfare(Pawn knower)
        {
            if (knower == null) return false;
            List<WelfareFact> facts;
            if (!knownWelfare.TryGetValue(knower.thingIDNumber, out facts))
                return false;
            for (int i = 0; i < facts.Count; i++)
            {
                WelfareFact fact = facts[i];
                if (fact.subjectId == knower.thingIDNumber) continue;
                if (fact.state != WelfareState.Unassessed
                    && fact.state != WelfareState.Assessed) continue;
                Pawn subject;
                Thing visible;
                if (TryFindPawnById(fact.subjectId, out subject)
                    && CanDirectlyObserveSubject(knower, subject,
                        WelfareSightRange, out visible)
                    && CanActOnCurrentWelfare(knower, subject)) return true;
            }
            return false;
        }

        internal bool TryFindPawnById(int id, out Pawn pawn)
        {
            pawn = null;
            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
                if (all[i].thingIDNumber == id)
                {
                    pawn = all[i];
                    return true;
                }
            List<Thing> corpses = map.listerThings.ThingsInGroup(
                ThingRequestGroup.Corpse);
            for (int i = 0; i < corpses.Count; i++)
            {
                Corpse corpse = corpses[i] as Corpse;
                if (corpse?.InnerPawn == null
                    || corpse.InnerPawn.thingIDNumber != id) continue;
                pawn = corpse.InnerPawn;
                return true;
            }
            return false;
        }

        internal static bool CanDirectlyObserveSubject(Pawn observer,
            Pawn subject, float radius, out Thing visible)
        {
            visible = null;
            if (observer == null || observer.Map == null || !observer.Spawned
                || observer.Dead || observer.Downed || !observer.Awake()
                || observer.RaceProps == null || !observer.RaceProps.Humanlike
                || PawnUtility.IsBiologicallyOrArtificiallyBlind(observer)
                || subject == null
                || HiddenRegistry.IsHidden(subject)
                    && subject.HostileTo(observer))
                return false;
            if (subject.Spawned && subject.Map == observer.Map) visible = subject;
            else if (subject.Corpse != null && subject.Corpse.Spawned
                && subject.Corpse.Map == observer.Map) visible = subject.Corpse;
            CAVisualPerception perception;
            return visible != null
                && CABattlefieldPerception.TryObserveVisibleThing(observer,
                    visible, subject, radius, out perception);
        }

        private void ObserveWelfare()
        {
            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            // A conscious pawn directly knows that they are wounded, impaired, or
            // bleeding even when nobody has performed a clinical assessment. That
            // coarse self fact can be reported over a valid channel; it carries no
            // exact diagnosis or hidden body-part detail.
            for (int i = 0; i < all.Count; i++)
                if (CanSelfReportWelfare(all[i])) NoteSelfWelfare(all[i]);
            for (int i = 0; i < all.Count; i++)
            {
                Pawn observer = all[i];
                if (!IsAwakeHumanlike(observer)) continue;
                for (int j = 0; j < all.Count; j++)
                {
                    Pawn subject = all[j];
                    if (subject == observer || subject.RaceProps == null
                        || !subject.RaceProps.Humanlike) continue;
                    Thing visible;
                    if (!CanDirectlyObserveSubject(observer, subject,
                        WelfareSightRange, out visible)) continue;
                    bool helpless = subject.Downed
                        || subject.DevelopmentalStage == DevelopmentalStage.Baby;
                    bool closeTemperatureDanger = helpless
                        && visible.Position.InHorDistOf(observer.Position,
                            ClinicalSightRange)
                        && CALifeSafety.InTemperatureDanger(subject);
                    if (subject.Downed || closeTemperatureDanger)
                    {
                        NoteVisualWelfare(observer, subject, visible.Position,
                            subject.Downed, closeTemperatureDanger);
                    }
                    else
                    {
                        WelfareFact fact = FindWelfare(observer,
                            subject.thingIDNumber);
                        // Seeing a pawn stand disproves a prior gross downed or
                        // exposure fact; it does not disprove their self-reported
                        // wound, bleeding, or impaired combat capacity.
                        if (fact != null && fact.state != WelfareState.Resolved
                            && (fact.observedDowned
                                || fact.observedTemperatureDanger))
                            SetDirectWelfareState(observer, fact, visible.Position,
                                WelfareState.Resolved, false, false, false);
                    }
                }
            }
        }

        // Downing removes autonomous action, not consciousness or the synchronized
        // mechlink. An awake downed pawn may therefore update only their own coarse
        // welfare fact. They remain excluded from sight-based observation, command
        // execution, and general relay duty.
        private static bool CanSelfReportWelfare(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead
                && pawn.RaceProps != null && pawn.RaceProps.Humanlike
                && pawn.Awake() && !pawn.InMentalState;
        }

        private void NoteSelfWelfare(Pawn subject)
        {
            if (subject == null || subject.health == null || !subject.Spawned)
                return;
            CACombatConditionSnapshot condition =
                CACombatConditionSnapshot.Capture(subject);
            bool needsTend = HealthAIUtility.ShouldBeTendedNowByPlayer(subject)
                && subject.health.HasHediffsNeedingTend();
            bool impaired = subject.Downed
                || condition.RequiresCombatRecovery(
                    CAImmediateCombat.HasRangedWeaponRole(subject));
            WelfareFact prior = FindWelfare(subject, subject.thingIDNumber);
            if (!needsTend && !impaired && (prior == null
                || prior.state == WelfareState.Resolved)) return;

            int now = Find.TickManager.TicksGame;
            WelfareState state = needsTend || impaired
                ? WelfareState.Unassessed : WelfareState.Resolved;
            var evidence = new WelfareEvidence(
                WelfareEvidenceSource.SelfObservation,
                subject.thingIDNumber, 1f, 2500,
                CommunicationChannel.None, subject.thingIDNumber);
            NoteWelfareEvidence(subject, subject.thingIDNumber,
                subject.Position, now, state, now, -1, int.MaxValue, 0,
                needsTend, subject.Downed, false, evidence);
        }

        private void NoteVisualWelfare(Pawn observer, Pawn subject,
            IntVec3 cell, bool downed, bool temperatureDanger)
        {
            int now = Find.TickManager.TicksGame;
            WelfareFact existing = FindWelfare(observer, subject.thingIDNumber);
            if (existing != null && existing.state == WelfareState.Assessed
                && existing.evidence.IsDirect
                && existing.evidence.Source ==
                    WelfareEvidenceSource.ClinicalObservation)
            {
                // Gross sight cannot renew a clinical estimate. Keep the exact
                // assessment age while its gross state and cell still match;
                // any visible change becomes a fresh unassessed visual fact.
                if (existing.lastKnown == cell
                    && existing.observedDowned == downed
                    && existing.observedTemperatureDanger == temperatureDanger)
                    return;
            }
            var evidence = new WelfareEvidence(WelfareEvidenceSource.Visual,
                subject.thingIDNumber, 1f, 0, CommunicationChannel.None,
                observer.thingIDNumber);
            NoteWelfareEvidence(observer, subject.thingIDNumber, cell, now,
                WelfareState.Unassessed, now, -1, int.MaxValue, 0,
                false, downed, temperatureDanger, evidence);
        }

        private void RefreshVisibleWelfare(Pawn knower, WelfareFact fact)
        {
            if (knower == null || fact == null) return;
            Pawn subject;
            Thing visible = null;
            if (!TryFindPawnById(fact.subjectId, out subject)
                || !CanDirectlyObserveSubject(knower, subject,
                    WelfareSightRange, out visible)) return;
            if (subject.Dead)
            {
                SetDirectWelfareState(knower, fact, visible.Position,
                    WelfareState.Deceased, false, true, false);
                return;
            }
            if (!subject.Downed && fact.state != WelfareState.Resolved
                && (fact.observedDowned
                    || fact.observedTemperatureDanger))
            {
                bool babyTemperatureFact = subject.DevelopmentalStage
                        == DevelopmentalStage.Baby
                    && fact.observedTemperatureDanger;
                if (babyTemperatureFact
                    && !visible.Position.InHorDistOf(knower.Position,
                        ClinicalSightRange)) return;
                if (babyTemperatureFact
                    && CALifeSafety.InTemperatureDanger(subject))
                {
                    NoteVisualWelfare(knower, subject, visible.Position,
                        false, true);
                    return;
                }
                SetDirectWelfareState(knower, fact, visible.Position,
                    WelfareState.Resolved, false, false, false);
            }
            else if (subject.Downed && (fact.state == WelfareState.Missing
                || fact.state == WelfareState.Resolved
                || fact.state == WelfareState.Deceased))
                NoteVisualWelfare(knower, subject, visible.Position, true,
                    visible.Position.InHorDistOf(knower.Position,
                        ClinicalSightRange)
                    && CALifeSafety.InTemperatureDanger(subject));
        }

        private void SetDirectWelfareState(Pawn knower, WelfareFact fact,
            IntVec3 cell, WelfareState state, bool needsTend, bool downed,
            bool temperatureDanger)
        {
            int now = Find.TickManager.TicksGame;
            fact.lastKnown = cell;
            fact.sourceTick = now;
            fact.lastRefreshTick = now;
            fact.state = state;
            fact.stateTick = now;
            fact.assessedTick = -1;
            fact.estimatedDeathTick = int.MaxValue;
            fact.diagnosticTier = 0;
            fact.observedNeedsTend = needsTend;
            fact.observedDowned = downed;
            fact.observedTemperatureDanger = temperatureDanger;
            fact.evidence = new WelfareEvidence(WelfareEvidenceSource.Visual,
                fact.subjectId, 1f, 0, CommunicationChannel.None,
                knower.thingIDNumber);
            fact.revision++;
        }

        private WelfareFact FindWelfare(Pawn knower, int subjectId)
        {
            if (knower == null) return null;
            List<WelfareFact> facts;
            if (!knownWelfare.TryGetValue(knower.thingIDNumber, out facts))
                return null;
            for (int i = 0; i < facts.Count; i++)
                if (facts[i].subjectId == subjectId) return facts[i];
            return null;
        }

        // Detached, read-only memory surface for the combat flight recorder.
        // Returning value snapshots keeps telemetry from refreshing or otherwise
        // changing the actor's facts while it observes them.
        internal List<WelfareFactSnapshot> RememberedWelfare(Pawn knower)
        {
            var result = new List<WelfareFactSnapshot>();
            if (knower == null) return result;
            List<WelfareFact> facts;
            if (!knownWelfare.TryGetValue(knower.thingIDNumber, out facts))
                return result;
            for (int i = 0; i < facts.Count; i++)
                if (facts[i] != null) result.Add(SnapshotOf(facts[i]));
            return result;
        }

        // Contact and welfare facts are separate stores, but they cannot leave the
        // same knower treating one identified pawn as both an active threat and a
        // confirmed casualty. Trust only a fresh welfare state at least as new as
        // the contact state; do not turn older rumors into omniscient resolution.
        private void ReconcileContactStatesFromWelfare(Pawn knower,
            List<Contact> contacts, int now)
        {
            if (knower == null || contacts == null || contacts.Count == 0)
                return;
            List<WelfareFact> welfare;
            if (!knownWelfare.TryGetValue(knower.thingIDNumber, out welfare))
                return;
            for (int i = 0; i < contacts.Count; i++)
            {
                Contact contact = contacts[i];
                for (int j = 0; j < welfare.Count; j++)
                {
                    WelfareFact fact = welfare[j];
                    if (fact == null || fact.subjectId != contact.hostileId
                        || now - fact.lastRefreshTick > WelfareStaleTicks
                        || fact.sourceTick < contact.stateTick
                        || fact.evidence.Confidence < 0.70f) continue;
                    ThreatContactState resolved;
                    if (fact.state == WelfareState.Deceased)
                        resolved = ThreatContactState.Dead;
                    else if (fact.observedDowned
                        && (fact.state == WelfareState.Unassessed
                            || fact.state == WelfareState.Assessed))
                        resolved = ThreatContactState.Downed;
                    else continue;
                    if (contact.state == resolved) break;
                    ThreatContactState prior = contact.state;
                    contact.state = resolved;
                    contact.stateTick = fact.sourceTick;
                    contact.tick = Math.Max(contact.tick, fact.sourceTick);
                    contact.lastKnown = fact.lastKnown;
                    CATrace.Pawn(knower, "reconciles contact "
                        + contact.hostileId + " from " + prior + " to "
                        + resolved + " using welfare evidence "
                        + fact.evidence.Source + "/"
                        + fact.evidence.DeliveryChannel + " at source tick "
                        + fact.sourceTick, contact: fact.lastKnown,
                        anchor: knower.Position);
                    break;
                }
            }
        }

        private void NoteWelfareEvidence(Pawn knower, int subjectId,
            IntVec3 cell, int sourceTick, WelfareState state, int stateTick,
            int assessedTick, int estimatedDeathTick, int diagnosticTier,
            bool observedNeedsTend, bool observedDowned,
            bool observedTemperatureDanger, WelfareEvidence evidence)
        {
            if (knower == null || subjectId <= 0 || !cell.IsValid) return;
            List<WelfareFact> facts;
            if (!knownWelfare.TryGetValue(knower.thingIDNumber, out facts))
            {
                facts = new List<WelfareFact>();
                knownWelfare[knower.thingIDNumber] = facts;
            }
            WelfareFact fact = null;
            for (int i = 0; i < facts.Count; i++)
                if (facts[i].subjectId == subjectId)
                {
                    fact = facts[i];
                    break;
            }
            if (fact != null)
            {
                int now = Find.TickManager.TicksGame;
                if (sourceTick < fact.sourceTick) return;
                bool reacquired = now - fact.lastRefreshTick
                    > WelfareStaleTicks;
                // A newer coarse self-report can renew concern, but it cannot
                // erase a still-current clinical/custody assessment merely by
                // arriving on a later tick. Once that assessment goes stale the
                // ordinary reacquisition path may replace it.
                bool currentClinicalAssessment = fact.state
                        == WelfareState.Assessed
                    && (fact.evidence.Source ==
                            WelfareEvidenceSource.ClinicalObservation
                        || fact.evidence.Source ==
                            WelfareEvidenceSource.PhysicalCustody);
                if (!reacquired && currentClinicalAssessment
                    && evidence.Source ==
                        WelfareEvidenceSource.SelfObservation
                    && state == WelfareState.Unassessed) return;
                if (!reacquired && (SameVisualHeartbeat(fact, cell, state,
                        assessedTick, estimatedDeathTick, diagnosticTier,
                        observedNeedsTend, observedDowned,
                        observedTemperatureDanger, evidence)
                    || SameSelfHeartbeat(fact, cell, state, assessedTick,
                        estimatedDeathTick, diagnosticTier,
                        observedNeedsTend, observedDowned,
                        observedTemperatureDanger, evidence)))
                {
                    // Renew the observation's freshness without changing its
                    // semantic identity or restarting an owned assessment.
                    fact.sourceTick = sourceTick;
                    fact.lastRefreshTick = now;
                    return;
                }
                if (!reacquired && TryRenewStableClinicalHeartbeat(fact,
                    cell, sourceTick, state, assessedTick,
                    estimatedDeathTick, diagnosticTier, observedNeedsTend,
                    observedDowned, observedTemperatureDanger, evidence,
                    now))
                    return;
                if (!reacquired && TryRenewRelayedHeartbeat(fact, cell,
                    sourceTick, state, assessedTick, estimatedDeathTick,
                    diagnosticTier, observedNeedsTend, observedDowned,
                    observedTemperatureDanger, evidence, now)) return;
                if (sourceTick == fact.sourceTick
                    && !WelfareEvidenceIsBetter(evidence, fact.evidence)
                    && !SameDirectObserver(evidence, fact.evidence)
                    && !reacquired) return;
                WelfareState priorState = fact.state;
                WelfareEvidence priorEvidence = fact.evidence;
                bool priorDowned = fact.observedDowned;
                IntVec3 priorCell = fact.lastKnown;
                fact.lastKnown = cell;
                fact.sourceTick = sourceTick;
                fact.lastRefreshTick = evidence.IsDirect ? sourceTick : now;
                if (reacquired) fact.acquiredTick = now;
                fact.state = state;
                fact.stateTick = stateTick;
                fact.assessedTick = assessedTick;
                fact.estimatedDeathTick = estimatedDeathTick;
                fact.diagnosticTier = diagnosticTier;
                fact.observedNeedsTend = observedNeedsTend;
                fact.observedDowned = observedDowned;
                fact.observedTemperatureDanger = observedTemperatureDanger;
                fact.evidence = evidence;
                fact.revision++;
                CATrace.Pawn(knower, "updates welfare concern " + subjectId
                    + " - state " + priorState + " -> " + state
                    + ", downed " + priorDowned + " -> " + observedDowned
                    + ", cell " + priorCell + " -> " + cell
                    + ", evidence " + priorEvidence.Source + "/"
                    + priorEvidence.DeliveryChannel + " -> "
                    + evidence.Source + "/" + evidence.DeliveryChannel
                    + ", revision " + fact.revision);
                return;
            }
            facts.Add(new WelfareFact
            {
                subjectId = subjectId,
                lastKnown = cell,
                sourceTick = sourceTick,
                lastRefreshTick = evidence.IsDirect ? sourceTick
                    : Find.TickManager.TicksGame,
                acquiredTick = Find.TickManager.TicksGame,
                state = state,
                stateTick = stateTick,
                assessedTick = assessedTick,
                estimatedDeathTick = estimatedDeathTick,
                diagnosticTier = diagnosticTier,
                observedNeedsTend = observedNeedsTend,
                observedDowned = observedDowned,
                observedTemperatureDanger = observedTemperatureDanger,
                evidence = evidence,
                revision = 1
            });
            CATrace.Pawn(knower, evidence.IsDirect ? "observes welfare concern "
                + subjectId : "receives welfare report " + subjectId);
        }

        private static bool SameVisualHeartbeat(WelfareFact fact, IntVec3 cell,
            WelfareState state, int assessedTick, int estimatedDeathTick,
            int diagnosticTier, bool observedNeedsTend, bool observedDowned,
            bool observedTemperatureDanger, WelfareEvidence evidence)
        {
            return fact.state == WelfareState.Unassessed
                && state == WelfareState.Unassessed
                && fact.lastKnown == cell
                && fact.assessedTick == assessedTick
                && fact.estimatedDeathTick == estimatedDeathTick
                && fact.diagnosticTier == diagnosticTier
                && fact.observedNeedsTend == observedNeedsTend
                && fact.observedDowned == observedDowned
                && fact.observedTemperatureDanger
                    == observedTemperatureDanger
                && fact.evidence.Source == WelfareEvidenceSource.Visual
                && evidence.Source == WelfareEvidenceSource.Visual
                && fact.evidence.SourceId == evidence.SourceId
                && Mathf.Approximately(fact.evidence.Confidence,
                    evidence.Confidence)
                && fact.evidence.UncertaintyTicks == evidence.UncertaintyTicks
                && fact.evidence.DeliveryChannel == evidence.DeliveryChannel
                && fact.evidence.ReporterId == evidence.ReporterId;
        }

        private static bool SameSelfHeartbeat(WelfareFact fact, IntVec3 cell,
            WelfareState state, int assessedTick, int estimatedDeathTick,
            int diagnosticTier, bool observedNeedsTend, bool observedDowned,
            bool observedTemperatureDanger, WelfareEvidence evidence)
        {
            return fact.state == state && fact.lastKnown == cell
                && fact.assessedTick == assessedTick
                && fact.estimatedDeathTick == estimatedDeathTick
                && fact.diagnosticTier == diagnosticTier
                && fact.observedNeedsTend == observedNeedsTend
                && fact.observedDowned == observedDowned
                && fact.observedTemperatureDanger
                    == observedTemperatureDanger
                && fact.evidence.Source
                    == WelfareEvidenceSource.SelfObservation
                && evidence.Source == WelfareEvidenceSource.SelfObservation
                && fact.evidence.SourceId == evidence.SourceId
                && fact.evidence.DeliveryChannel == evidence.DeliveryChannel
                && fact.evidence.ReporterId == evidence.ReporterId;
        }

        private static bool TryRenewStableClinicalHeartbeat(WelfareFact fact,
            IntVec3 cell, int sourceTick, WelfareState state,
            int assessedTick, int estimatedDeathTick, int diagnosticTier,
            bool observedNeedsTend, bool observedDowned,
            bool observedTemperatureDanger, WelfareEvidence evidence,
            int now)
        {
            bool stable = fact.evidence.IsDirect && evidence.IsDirect
                && (fact.evidence.Source ==
                        WelfareEvidenceSource.ClinicalObservation
                    || fact.evidence.Source ==
                        WelfareEvidenceSource.PhysicalCustody)
                && fact.evidence.Source == evidence.Source
                && fact.evidence.SourceId == evidence.SourceId
                && fact.evidence.ReporterId == evidence.ReporterId
                && fact.state == state && fact.lastKnown == cell
                && fact.estimatedDeathTick == int.MaxValue
                && estimatedDeathTick == int.MaxValue
                && fact.diagnosticTier == diagnosticTier
                && fact.observedNeedsTend == observedNeedsTend
                && fact.observedDowned == observedDowned
                && !fact.observedTemperatureDanger
                && !observedTemperatureDanger;
            if (!stable) return false;
            fact.sourceTick = Math.Max(fact.sourceTick, sourceTick);
            fact.lastRefreshTick = now;
            if (assessedTick >= 0)
                fact.assessedTick = Math.Max(fact.assessedTick,
                    assessedTick);
            return true;
        }

        private static bool SameRelayedHeartbeat(WelfareFact fact,
            IntVec3 cell, WelfareState state, int estimatedDeathTick,
            int diagnosticTier, bool observedNeedsTend,
            bool observedDowned, bool observedTemperatureDanger,
            WelfareEvidence evidence)
        {
            return !fact.evidence.IsDirect && !evidence.IsDirect
                && fact.state == state && fact.lastKnown == cell
                && fact.estimatedDeathTick == estimatedDeathTick
                && fact.diagnosticTier == diagnosticTier
                && fact.observedNeedsTend == observedNeedsTend
                && fact.observedDowned == observedDowned
                && fact.observedTemperatureDanger
                    == observedTemperatureDanger
                && fact.evidence.Source == evidence.Source
                && fact.evidence.SourceId == evidence.SourceId
                && Mathf.Approximately(fact.evidence.Confidence,
                    evidence.Confidence)
                && fact.evidence.UncertaintyTicks
                    == evidence.UncertaintyTicks;
        }

        private static bool TryRenewRelayedHeartbeat(WelfareFact fact,
            IntVec3 cell, int sourceTick, WelfareState state,
            int assessedTick, int estimatedDeathTick, int diagnosticTier,
            bool observedNeedsTend, bool observedDowned,
            bool observedTemperatureDanger, WelfareEvidence evidence,
            int now)
        {
            if (!SameRelayedHeartbeat(fact, cell, state,
                estimatedDeathTick, diagnosticTier, observedNeedsTend,
                observedDowned, observedTemperatureDanger, evidence))
                return false;
            // Delivery freshness and evidence-source time are separate. A
            // repeated report may keep a memory socially current without
            // rewriting when the underlying observation occurred.
            fact.sourceTick = Math.Max(fact.sourceTick, sourceTick);
            fact.lastRefreshTick = now;
            if (assessedTick >= 0)
                fact.assessedTick = Math.Max(fact.assessedTick,
                    assessedTick);
            return true;
        }

        private static bool SameDirectObserver(WelfareEvidence candidate,
            WelfareEvidence current)
        {
            return candidate.IsDirect && current.IsDirect
                && candidate.Source == current.Source
                && candidate.SourceId == current.SourceId
                && candidate.ReporterId == current.ReporterId;
        }

        internal static bool WelfareHeartbeatContract(
            out bool stableHeartbeat, out bool relayHeartbeat)
        {
            var direct = new WelfareEvidence(
                WelfareEvidenceSource.ClinicalObservation, 44, 1f, 0,
                CommunicationChannel.None, 11);
            var relayed = direct.RelayedThrough(CommunicationChannel.Voice,
                12);
            var stableFact = new WelfareFact
            {
                subjectId = 44,
                lastKnown = new IntVec3(10, 0, 10),
                sourceTick = 100,
                lastRefreshTick = 100,
                acquiredTick = 100,
                revision = 3,
                state = WelfareState.Assessed,
                stateTick = 100,
                assessedTick = 100,
                estimatedDeathTick = int.MaxValue,
                diagnosticTier = 2,
                observedDowned = true,
                evidence = direct
            };
            stableHeartbeat = TryRenewStableClinicalHeartbeat(stableFact,
                stableFact.lastKnown, 200, WelfareState.Assessed, 200,
                int.MaxValue, 2, false, true, false, direct, 200)
                && stableFact.revision == 3
                && stableFact.sourceTick == 200
                && stableFact.lastRefreshTick == 200;
            var relayFact = new WelfareFact
            {
                subjectId = 44,
                lastKnown = new IntVec3(10, 0, 10),
                sourceTick = 100,
                lastRefreshTick = 100,
                acquiredTick = 100,
                revision = 7,
                state = WelfareState.Assessed,
                stateTick = 100,
                assessedTick = 100,
                estimatedDeathTick = int.MaxValue,
                diagnosticTier = 2,
                observedDowned = true,
                evidence = relayed
            };
            relayHeartbeat = TryRenewRelayedHeartbeat(relayFact,
                relayFact.lastKnown, 100, WelfareState.Assessed, 100,
                int.MaxValue, 2, false, true, false, relayed, 250)
                && relayFact.revision == 7
                && relayFact.sourceTick == 100
                && relayFact.lastRefreshTick == 250;
            return stableHeartbeat && relayHeartbeat;
        }

        private static bool WelfareEvidenceIsBetter(WelfareEvidence candidate,
            WelfareEvidence current)
        {
            if (candidate.IsDirect != current.IsDirect) return candidate.IsDirect;
            if (candidate.Confidence != current.Confidence)
                return candidate.Confidence > current.Confidence;
            if (candidate.UncertaintyTicks != current.UncertaintyTicks)
                return candidate.UncertaintyTicks < current.UncertaintyTicks;
            return (int)candidate.Source > (int)current.Source;
        }

        private static WelfareFactSnapshot SnapshotOf(WelfareFact fact)
        {
            return new WelfareFactSnapshot(fact.subjectId, fact.lastKnown,
                fact.sourceTick, fact.acquiredTick, fact.revision, fact.state,
                fact.stateTick, fact.assessedTick, fact.estimatedDeathTick,
                fact.diagnosticTier, fact.observedNeedsTend,
                fact.observedDowned, fact.observedTemperatureDanger,
                fact.evidence);
        }

        private void PropagateWelfare()
        {
            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            int now = Find.TickManager.TicksGame;
            var deliveries = new List<WelfareDelivery>();
            for (int i = 0; i < all.Count; i++)
            {
                Pawn teller = all[i];
                bool mobileTeller = IsAwakeHumanlike(teller);
                if ((!mobileTeller && !CanSelfReportWelfare(teller))
                    || teller.Faction == null) continue;
                List<WelfareFact> facts;
                if (!knownWelfare.TryGetValue(teller.thingIDNumber, out facts))
                    continue;
                for (int j = 0; j < all.Count; j++)
                {
                    Pawn listener = all[j];
                    if (listener == teller || !IsAwakeHumanlike(listener)
                        || listener.Faction != teller.Faction) continue;
                    CommunicationChannel routine;
                    CommunicationChannel strategic;
                    bool routineLinked = CommsModule.TryGetKnowledgeChannel(
                        teller, listener, 11.9f, out routine);
                    bool strategicLinked = CommsModule.TryGetStrategicChannel(
                        teller, listener, 11.9f, out strategic);
                    for (int f = 0; f < facts.Count; f++)
                    {
                        WelfareFact stored = facts[f];
                        // A downed endpoint can report itself over an available
                        // channel; it does not become a network relay for facts it
                        // happened to know before going down.
                        if (!mobileTeller
                            && stored.subjectId != teller.thingIDNumber)
                            continue;
                        if (now - stored.lastRefreshTick > WelfareStaleTicks
                            || stored.state == WelfareState.Missing
                            || !stored.evidence.IsRelayable
                            || stored.subjectId == listener.thingIDNumber)
                            continue;
                        bool direct = stored.evidence.IsDirect;
                        if (direct ? !strategicLinked : !routineLinked) continue;
                        CommunicationChannel channel = direct
                            ? strategic : routine;
                        deliveries.Add(new WelfareDelivery
                        {
                            listener = listener,
                            fact = SnapshotOf(stored),
                            evidence = stored.evidence.RelayedThrough(channel,
                                teller.thingIDNumber)
                        });
                    }
                }
            }
            for (int i = 0; i < deliveries.Count; i++)
            {
                WelfareDelivery delivery = deliveries[i];
                WelfareFactSnapshot fact = delivery.fact;
                NoteWelfareEvidence(delivery.listener, fact.SubjectId,
                    fact.Cell, fact.SourceTick, fact.State, fact.StateTick,
                    fact.AssessedTick, fact.EstimatedDeathTick,
                    fact.DiagnosticTier, fact.ObservedNeedsTend,
                    fact.ObservedDowned, fact.ObservedTemperatureDanger,
                    delivery.evidence);
            }
        }

        private void ExpireWelfare()
        {
            int now = Find.TickManager.TicksGame;
            var empty = new List<int>();
            foreach (KeyValuePair<int, List<WelfareFact>> pair in knownWelfare)
            {
                pair.Value.RemoveAll(f => now - f.lastRefreshTick
                    > WelfareStaleTicks);
                if (pair.Value.Count == 0) empty.Add(pair.Key);
            }
            for (int i = 0; i < empty.Count; i++)
                knownWelfare.Remove(empty[i]);
        }

        private void TickAccountability()
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.authorityObedience) return;
            if (--accountabilityCooldown > 0) return;
            accountabilityCooldown = AccountabilityInterval;
            MaintainAccountability();
        }

        private void MaintainAccountability()
        {
            int now = Find.TickManager.TicksGame;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            var directIds = new List<int>();
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn owner = colonists[i];
                if (!OwnerHasActiveAccountabilityDuty(owner)) continue;
                SquadComponent.CopyExplicitDirectReportIds(owner, directIds);
                for (int j = 0; j < directIds.Count; j++)
                {
                    int subjectId = directIds[j];
                    CAAccountabilityRecord record = FindAccountability(
                        owner.thingIDNumber, subjectId);
                    if (record == null)
                    {
                        Pawn subject;
                        if (!TryFindPawnById(subjectId, out subject)
                            || !SharesExpectedDuty(owner, subject)) continue;
                        record = new CAAccountabilityRecord
                        {
                            ownerId = owner.thingIDNumber,
                            subjectId = subjectId,
                            expectedSinceTick = now,
                            stateTick = now,
                            graceTicks = AccountabilityGrace(owner, subjectId)
                        };
                        accountability.Add(record);
                        CATrace.Pawn(owner, "accepts accountability for "
                            + subject.LabelShort + " under shared duty");
                    }
                    UpdateAccountability(owner, record, now);
                }
            }

            for (int i = accountability.Count - 1; i >= 0; i--)
            {
                CAAccountabilityRecord record = accountability[i];
                Pawn owner = PawnById(colonists, record.ownerId);
                if (owner != null && OwnerHasActiveAccountabilityDuty(owner)
                    && SquadComponent.HasExplicitResponsibilityFor(owner,
                        record.subjectId)) continue;
                if (record.state != AccountabilityState.Resolved)
                {
                    record.state = AccountabilityState.Resolved;
                    record.stateTick = now;
                    record.revision++;
                }
                if (now - record.stateTick > WelfareStaleTicks)
                    accountability.RemoveAt(i);
            }
        }

        private void UpdateAccountability(Pawn owner,
            CAAccountabilityRecord record, int now)
        {
            Pawn subject;
            Thing visible = null;
            CommunicationChannel channel;
            bool found = TryFindPawnById(record.subjectId, out subject);
            bool witnessed = found && CanDirectlyObserveSubject(owner, subject,
                WelfareSightRange, out visible);
            bool checkedIn = !witnessed && found && subject.Spawned
                && !subject.Dead && !subject.Downed && subject.Awake()
                && CommsModule.TryGetStrategicChannel(subject, owner,
                    CommsModule.VoiceRange, out channel);
            if (witnessed || checkedIn)
            {
                IntVec3 cell = witnessed ? visible.Position : subject.Position;
                AccountabilityState prior = record.state;
                record.lastConfirmedCell = cell;
                record.lastConfirmedTick = now;
                record.state = AccountabilityState.Monitoring;
                record.stateTick = now;
                record.revision++;
                if (witnessed && subject.Downed)
                    NoteVisualWelfare(owner, subject, cell, true,
                        cell.InHorDistOf(owner.Position, ClinicalSightRange)
                        && CALifeSafety.InTemperatureDanger(subject));
                if (prior == AccountabilityState.Overdue
                    || prior == AccountabilityState.Checking
                    || prior == AccountabilityState.ConcernReported)
                    CATrace.Pawn(owner, "confirms " + subject.LabelShort
                        + " by " + (witnessed ? "sight" : "check-in"));
                return;
            }

            int since = record.lastConfirmedTick >= 0
                ? now - record.lastConfirmedTick
                : now - record.expectedSinceTick;
            if (record.state == AccountabilityState.Checking
                && now - record.stateTick > record.graceTicks)
            {
                record.state = AccountabilityState.Overdue;
                record.stateTick = now;
                record.revision++;
            }
            if (since <= record.graceTicks
                || record.state == AccountabilityState.Checking
                || record.state == AccountabilityState.ConcernReported) return;
            if (record.state != AccountabilityState.Overdue)
            {
                record.state = AccountabilityState.Overdue;
                record.stateTick = now;
                record.revision++;
                CATrace.Pawn(owner, "reassesses absent direct report "
                    + record.subjectId + " after missed check-in; last confirmed "
                    + (record.lastConfirmedCell.IsValid
                        ? record.lastConfirmedCell.ToString() : "location unknown"));
            }
        }

        private static bool OwnerHasActiveAccountabilityDuty(Pawn owner)
        {
            if (owner == null || owner.Dead || owner.Downed) return false;
            Lord lord = owner.GetLord();
            return lord != null && (lord.LordJob is LordJob_CATactical
                || lord.LordJob is LordJob_CAStackBreach);
        }

        private static bool SharesExpectedDuty(Pawn owner, Pawn subject)
        {
            Lord lord = owner?.GetLord();
            return lord != null && subject != null && subject.GetLord() == lord
                && (lord.LordJob is LordJob_CATactical
                    || lord.LordJob is LordJob_CAStackBreach);
        }

        private int AccountabilityGrace(Pawn owner, int subjectId)
        {
            DispositionProfile disposition = Disposition.Of(owner);
            float vigilance = (disposition.discipline + disposition.empathy
                + disposition.initiative) / 3f;
            int seed = Gen.HashCombineInt(owner.thingIDNumber, subjectId,
                map.uniqueID, 0xCA239);
            int circumstance = Mathf.FloorToInt(Rand.ValueAsync(seed) * 3f);
            return 600 + Mathf.RoundToInt((1f - vigilance) * 600f)
                + circumstance * 300;
        }

        private CAAccountabilityRecord FindAccountability(int ownerId,
            int subjectId)
        {
            for (int i = 0; i < accountability.Count; i++)
                if (accountability[i].ownerId == ownerId
                    && accountability[i].subjectId == subjectId)
                    return accountability[i];
            return null;
        }

        private static Pawn PawnById(List<Pawn> pawns, int id)
        {
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == id) return pawns[i];
            return null;
        }

        public void CopyAccountability(Pawn owner,
            List<AccountabilitySnapshot> result)
        {
            result.Clear();
            if (owner == null) return;
            for (int i = 0; i < accountability.Count; i++)
            {
                CAAccountabilityRecord record = accountability[i];
                if (record.ownerId != owner.thingIDNumber) continue;
                result.Add(new AccountabilitySnapshot(record.subjectId,
                    record.lastConfirmedCell, record.expectedSinceTick,
                    record.lastConfirmedTick, record.revision, record.graceTicks,
                    record.state, record.stateTick));
            }
        }

        internal bool ShouldRetainAutomaticDefenseForAccountability(Pawn owner)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (owner == null || owner.Map != map || settings == null
                || !settings.authorityObedience
                || AutonomyComponent.LevelOf(owner) < 2
                || !CATactical.IsAutomaticDefense(owner)) return false;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < accountability.Count; i++)
            {
                CAAccountabilityRecord record = accountability[i];
                if (record.ownerId != owner.thingIDNumber
                    || !SquadComponent.HasExplicitResponsibilityFor(owner,
                        record.subjectId)) continue;
                if (record.state == AccountabilityState.Checking)
                {
                    if (record.lastConfirmedCell.IsValid
                        && record.lastConfirmedCell.InBounds(map)) return true;
                    continue;
                }
                bool due = record.state == AccountabilityState.Overdue;
                if (record.state == AccountabilityState.Monitoring)
                {
                    int since = record.lastConfirmedTick >= 0
                        ? now - record.lastConfirmedTick
                        : now - record.expectedSinceTick;
                    due = since > record.graceTicks;
                }
                if (due && CanReachAccountabilityCell(owner, record)) return true;
            }
            return false;
        }

        internal void RegisterWelfareCheck(Job job, int subjectId,
            int sourceTick, int stateTick, int revision, IntVec3 cell,
            WelfareCheckOrigin origin)
        {
            if (job == null || subjectId <= 0 || !cell.IsValid) return;
            pendingWelfareJobs[job.loadID] = new PendingWelfareCheck(subjectId,
                sourceTick, stateTick, revision, cell, origin);
        }

        internal bool TryTakeWelfareCheck(int jobId, out int subjectId,
            out int sourceTick, out int stateTick, out int revision,
            out IntVec3 cell,
            out WelfareCheckOrigin origin)
        {
            PendingWelfareCheck pending;
            if (!pendingWelfareJobs.TryGetValue(jobId, out pending))
            {
                subjectId = -1;
                sourceTick = -1;
                stateTick = -1;
                revision = -1;
                cell = IntVec3.Invalid;
                origin = WelfareCheckOrigin.WelfareFact;
                return false;
            }
            pendingWelfareJobs.Remove(jobId);
            subjectId = pending.SubjectId;
            sourceTick = pending.SourceTick;
            stateTick = pending.StateTick;
            revision = pending.Revision;
            cell = pending.Cell;
            origin = pending.Origin;
            return true;
        }

        internal bool WelfareCheckMatches(Pawn owner, int subjectId,
            int sourceTick, int stateTick, int revision, IntVec3 cell,
            WelfareCheckOrigin origin)
        {
            if (owner == null || owner.Map != map || subjectId <= 0
                || !cell.IsValid) return false;
            if (origin == WelfareCheckOrigin.Accountability)
            {
                if (!OwnerHasActiveAccountabilityDuty(owner)
                    || !SquadComponent.HasExplicitResponsibilityFor(owner,
                        subjectId)) return false;
                CAAccountabilityRecord record = FindAccountability(
                    owner.thingIDNumber, subjectId);
                return record != null
                    && record.state == AccountabilityState.Checking
                    && record.lastConfirmedTick == sourceTick
                    && record.stateTick == stateTick
                    && record.revision == revision
                    && record.lastConfirmedCell == cell;
            }
            WelfareFact fact = FindWelfare(owner, subjectId);
            return fact != null
                && (fact.state == WelfareState.Unassessed
                    || fact.state == WelfareState.Assessed)
                && fact.stateTick == stateTick
                && fact.revision == revision
                && fact.lastKnown == cell;
        }

        internal bool TryBeginAccountabilityCheck(Pawn owner,
            out AccountabilitySnapshot snapshot)
        {
            snapshot = default;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (owner == null || settings == null
                || !settings.authorityObedience
                || AutonomyComponent.LevelOf(owner) < 2
                || !OwnerHasActiveAccountabilityDuty(owner)) return false;
            CAAccountabilityRecord best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < accountability.Count; i++)
            {
                CAAccountabilityRecord record = accountability[i];
                if (record.ownerId != owner.thingIDNumber
                    || record.state != AccountabilityState.Overdue
                    || !SquadComponent.HasExplicitResponsibilityFor(owner,
                        record.subjectId)
                    || !CanReachAccountabilityCell(owner, record)) continue;
                int distance = owner.Position.DistanceToSquared(
                    record.lastConfirmedCell);
                if (best == null || distance < bestDistance)
                {
                    best = record;
                    bestDistance = distance;
                }
            }
            if (best == null) return false;
            best.state = AccountabilityState.Checking;
            best.stateTick = Find.TickManager.TicksGame;
            best.revision++;
            snapshot = new AccountabilitySnapshot(best.subjectId,
                best.lastConfirmedCell, best.expectedSinceTick,
                best.lastConfirmedTick, best.revision, best.graceTicks,
                best.state, best.stateTick);
            return true;
        }

        private bool CanReachAccountabilityCell(Pawn owner,
            CAAccountabilityRecord record)
        {
            if (owner == null || owner.Map != map || record == null
                || !record.lastConfirmedCell.IsValid
                || !record.lastConfirmedCell.InBounds(map)
                || record.lastConfirmedCell.IsForbidden(owner)) return false;
            if (AutonomyComponent.LevelOf(owner) == 2
                && owner.Position.DistanceToSquared(record.lastConfirmedCell) > 900)
                return false;
            return owner.CanReach(record.lastConfirmedCell,
                PathEndMode.Touch, Danger.Deadly);
        }

        internal void CompleteAccountabilityCheck(Pawn owner, int subjectId,
            bool found, IntVec3 observedCell)
        {
            if (owner == null) return;
            CAAccountabilityRecord record = FindAccountability(
                owner.thingIDNumber, subjectId);
            if (record == null) return;
            int now = Find.TickManager.TicksGame;
            if (found)
            {
                record.lastConfirmedCell = observedCell;
                record.lastConfirmedTick = now;
                record.state = AccountabilityState.Monitoring;
                record.stateTick = now;
                record.revision++;
                CATrace.Pawn(owner, "welfare check confirms direct report "
                    + subjectId);
                return;
            }
            record.state = AccountabilityState.ConcernReported;
            record.stateTick = now;
            record.revision++;
            Messages.Message(owner.LabelShort
                + " could not find a direct report at their last confirmed position.",
                new LookTargets(record.lastConfirmedCell, map),
                MessageTypeDefOf.NeutralEvent, historical: false);
            CATrace.Pawn(owner, "reports direct report " + subjectId
                + " not found at last confirmed position");
        }

        internal void CompleteWelfareCheck(Pawn assessor, int subjectId,
            int sourceTick, int stateTick, int revision, IntVec3 expectedCell,
            WelfareCheckOrigin origin)
        {
            if (!WelfareCheckMatches(assessor, subjectId, sourceTick,
                stateTick, revision, expectedCell, origin)) return;
            Pawn subject;
            TryFindPawnById(subjectId, out subject);
            Thing visible = null;
            bool found = subject != null
                && CAClinicalObservation.CanClinicallyObserve(assessor,
                    subject, out visible)
                && visible.Position.InHorDistOf(expectedCell,
                    ClinicalSightRange);
            if (found)
            {
                NoteClinicalWelfare(assessor, subject, visible, announce: true);
                if (origin == WelfareCheckOrigin.Accountability)
                    CompleteAccountabilityCheck(assessor, subjectId, true,
                        visible.Position);
                return;
            }
            if (origin == WelfareCheckOrigin.WelfareFact)
            {
                WelfareFact fact = FindWelfare(assessor, subjectId);
                if (fact != null)
                {
                    fact.state = WelfareState.Missing;
                    fact.stateTick = Find.TickManager.TicksGame;
                    fact.revision++;
                }
            }
            if (expectedCell.IsValid && expectedCell.InBounds(map))
                MoteMaker.ThrowText(expectedCell.ToVector3Shifted(), map,
                    "not found at last-known position", Color.white, 3.5f);
            if (origin == WelfareCheckOrigin.Accountability)
                CompleteAccountabilityCheck(assessor, subjectId,
                    false, expectedCell);
            CATrace.Pawn(assessor, "does not find welfare subject "
                + subjectId + " at " + expectedCell);
        }

        internal void MarkWelfareUnassessed(Pawn knower, int subjectId)
        {
            WelfareFact fact = FindWelfare(knower, subjectId);
            if (fact == null) return;
            fact.state = WelfareState.Unassessed;
            fact.stateTick = Find.TickManager.TicksGame;
            fact.assessedTick = -1;
            fact.estimatedDeathTick = int.MaxValue;
            fact.revision++;
        }
    }

    internal static class CAClinicalObservation
    {
        internal static bool CanClinicallyObserve(Pawn observer, Pawn patient,
            out Thing visible)
        {
            visible = null;
            if (observer == null || observer.Map == null || !observer.Awake()
                || patient == null) return false;
            if (observer.IsCarryingPawn(patient))
            {
                visible = observer;
                return observer.health?.capacities != null
                    && observer.health.capacities.CapableOf(
                        PawnCapacityDefOf.Manipulation);
            }
            if (!TryPresentSubject(observer, patient, 6.9f, out visible))
                return false;
            if (observer.Position.InHorDistOf(visible.Position, 1.9f))
                return observer.Position.AdjacentTo8WayOrInside(visible)
                    && GenSight.LineOfSightToThing(observer.Position, visible,
                        observer.Map, true)
                    && observer.health?.capacities != null
                    && observer.health.capacities.CapableOf(
                        PawnCapacityDefOf.Manipulation);
            return !PawnUtility.IsBiologicallyOrArtificiallyBlind(observer)
                && !HiddenRegistry.IsHidden(patient)
                && GenSight.LineOfSightToThing(observer.Position, visible,
                    observer.Map, true);
        }

        private static bool TryPresentSubject(Pawn observer, Pawn patient,
            float radius, out Thing visible)
        {
            visible = null;
            if (observer == null || observer.Map == null || !observer.Awake()
                || patient == null) return false;
            if (patient.Spawned && patient.Map == observer.Map) visible = patient;
            else if (patient.Corpse != null && patient.Corpse.Spawned
                && patient.Corpse.Map == observer.Map) visible = patient.Corpse;
            return visible != null
                && observer.Position.InHorDistOf(visible.Position, radius);
        }

        internal static int PerceivedBleedDeathTicks(Pawn assessor,
            Pawn patient)
        {
            int actual = HealthUtility.TicksUntilDeathDueToBloodLoss(patient);
            if (actual == int.MaxValue) return int.MaxValue;
            int tier = DiagnosticTier(assessor);
            if (tier == 0) return actual <= 45000 ? 30000 : 60000;
            int bucket = tier == 1 ? 15000 : tier == 2 ? 7500 : 2500;
            long quantized = ((long)actual + bucket / 2L) / bucket * bucket;
            return (int)Math.Min((long)int.MaxValue - 1L,
                Math.Max(bucket / 2L, quantized));
        }

        internal static int DiagnosticTier(Pawn pawn)
        {
            int score = SkillLevel(pawn, SkillDefOf.Medicine) * 3
                + SkillLevel(pawn, SkillDefOf.Intellectual);
            return score >= 52 ? 3 : score >= 32 ? 2 : score >= 16 ? 1 : 0;
        }

        internal static int SkillLevel(Pawn pawn, SkillDef skill)
        {
            return pawn?.skills != null ? pawn.skills.GetSkill(skill).Level : 0;
        }

        internal static string DiagnosisLabel(bool downed,
            bool temperatureDanger, int tier, int estimated)
        {
            if (estimated == int.MaxValue)
            {
                if (temperatureDanger) return "downed; dangerous exposure";
                return downed ? "downed; no bleed-out apparent"
                    : "no bleed-out apparent";
            }
            if (tier == 0) return estimated <= 45000
                ? "heavy bleeding; exact severity unclear"
                : "bleeding; exact severity unclear";
            if (estimated <= 7500) return "immediate bleed-out risk";
            if (estimated <= 15000) return "critical bleeding";
            if (estimated <= 45000) return "serious bleeding";
            return "bleeding; not immediately critical";
        }

        internal static Color DiagnosisColor(int estimated)
        {
            if (estimated <= 15000) return Color.red;
            if (estimated <= 45000) return new Color(1f, 0.65f, 0.2f);
            return Color.white;
        }

        internal static bool AssessmentNeedsRefresh(
            WelfareFactSnapshot fact, int now)
        {
            if (fact.State != WelfareState.Assessed) return false;
            if (fact.EstimatedDeathTick != int.MaxValue
                && fact.EstimatedDeathTick <= now) return true;
            bool timeSensitive = fact.EstimatedDeathTick != int.MaxValue
                || fact.ObservedTemperatureDanger;
            return fact.Evidence.IsDirect && timeSensitive
                && now - fact.SourceTick > 90;
        }

        internal static string MemoryStabilityContractReceipt()
        {
            var direct = new WelfareEvidence(
                WelfareEvidenceSource.ClinicalObservation, 44, 1f, 0,
                CommunicationChannel.None, 11);
            var relayed = direct.RelayedThrough(CommunicationChannel.Voice,
                12);
            var stable = new WelfareFactSnapshot(44, new IntVec3(10, 0, 10),
                100, 100, 1, WelfareState.Assessed, 100, 100,
                int.MaxValue, 2, false, true, false, direct);
            var bleeding = new WelfareFactSnapshot(44,
                new IntVec3(10, 0, 10), 100, 100, 1,
                WelfareState.Assessed, 100, 100, 5000, 2, true, true,
                false, direct);
            var exposure = new WelfareFactSnapshot(44,
                new IntVec3(10, 0, 10), 100, 100, 1,
                WelfareState.Assessed, 100, 100, int.MaxValue, 2, false,
                true, true, direct);
            var expiredRelay = new WelfareFactSnapshot(44,
                new IntVec3(10, 0, 10), 100, 100, 1,
                WelfareState.Assessed, 100, 100, 150, 2, true, true,
                false, relayed);
            bool stableHeld = !AssessmentNeedsRefresh(stable, 1000);
            bool bleedingRefreshes = AssessmentNeedsRefresh(bleeding, 191);
            bool exposureRefreshes = AssessmentNeedsRefresh(exposure, 191);
            bool expiredReportRefreshes = AssessmentNeedsRefresh(
                expiredRelay, 200);
            bool stableHeartbeat;
            bool relayHeartbeat;
            bool heartbeatContract = KnowledgeMapComponent
                .WelfareHeartbeatContract(out stableHeartbeat,
                    out relayHeartbeat);
            bool pass = stableHeld && bleedingRefreshes && exposureRefreshes
                && expiredReportRefreshes && heartbeatContract;
            return "[CA] welfare memory stability contract: "
                + (pass ? "pass" : "fail")
                + "; stable downed/no-bleed direct assessment retained "
                + stableHeld + "; finite bleed assessment refreshes "
                + bleedingRefreshes + "; dangerous exposure refreshes "
                + exposureRefreshes + "; expired relayed projection refreshes "
                + expiredReportRefreshes + "; stable clinical heartbeat renews "
                + "without revision " + stableHeartbeat + "; equal-source "
                + "relayed heartbeat renews delivery freshness while preserving "
                + "source time and revision " + relayHeartbeat
                + ". Floating diagnosis text is emitted only for a materially "
                + "changed stable clinical meaning.";
        }

        internal static Job TendJob(Pawn doctor, Pawn patient)
        {
            bool carried = doctor.IsCarryingPawn(patient);
            if (!carried && (patient.IsForbidden(doctor)
                || !doctor.CanReserveAndReach(patient, PathEndMode.ClosestTouch,
                    Danger.Deadly))) return null;
            if (patient.InAggroMentalState
                && !patient.health.hediffSet.HasHediff(HediffDefOf.Scaria))
                return null;
            Thing medicine = HealthAIUtility.FindBestMedicine(doctor, patient);
            Job tend = medicine != null && medicine.SpawnedParentOrMe != medicine
                ? JobMaker.MakeJob(JobDefOf.TendPatient, patient, medicine,
                    medicine.SpawnedParentOrMe)
                : medicine != null
                    ? JobMaker.MakeJob(JobDefOf.TendPatient, patient, medicine)
                    : JobMaker.MakeJob(JobDefOf.TendPatient, patient);
            tend.count = 1;
            tend.endAfterTendedOnce = true;
            return tend;
        }
    }

    // One native constant-think decision. It may assess a remembered welfare cell,
    // act on a local clinical fact, or check an overdue direct report. It never
    // follows a subject's live position from memory.
    public class JobGiver_CAWelfareResponse : ThinkNode_JobGiver
    {
        private static readonly List<WelfareFactSnapshot> factBuffer =
            new List<WelfareFactSnapshot>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            return TryGiveResponse(pawn, allowDraftedIdle: false);
        }

        internal static Job TryGiveResponse(Pawn pawn,
            bool allowDraftedIdle)
        {
            return TryGiveResponse(pawn, allowDraftedIdle,
                bypassRecoveryBlock: false, visiblePeerOnly: false);
        }

        internal static Job TryGiveVisiblePeerResponseDuringRecovery(Pawn pawn)
        {
            return TryGiveResponse(pawn, allowDraftedIdle: true,
                bypassRecoveryBlock: true, visiblePeerOnly: true);
        }

        private static Job TryGiveResponse(Pawn pawn,
            bool allowDraftedIdle, bool bypassRecoveryBlock,
            bool visiblePeerOnly)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (pawn == null || settings == null || !pawn.IsColonistPlayerControlled
                || !pawn.Spawned || pawn.Dead || pawn.Downed
                || pawn.Drafted && !allowDraftedIdle
                || pawn.InMentalState || !pawn.Awake() || pawn.jobs == null)
                return null;
            bool proactiveHold = CATactical.IsHold(pawn)
                && AutonomyComponent.LevelOf(pawn) >= 2
                && !CATactical.HasForeignPlayerForcedJob(pawn);
            if (pawn.CurJob != null && pawn.CurJob.playerForced
                && !proactiveHold) return null;
            if (pawn.jobs.jobQueue != null && pawn.jobs.jobQueue.AnyPlayerForced)
                return null;
            if (pawn.CurJob != null && (pawn.CurJob.def == JobDefOf.TendPatient
                || pawn.CurJob.def == JobDefOf.TendEntity
                || pawn.CurJob.def == JobDefOf.Rescue
                || pawn.CurJob.def == CA_Defs.AssessCasualty
                || pawn.CurJob.def == CA_Defs.EmergencySelfTend)) return null;
            // A finite tend can end while the actor is still bleeding or otherwise
            // below combat viability. Outward welfare work must not steal the next
            // think-tree pass from that actor-local recovery episode.
            if (!bypassRecoveryBlock
                && CAImmediateCombat.RecoveryStillRequired(pawn)) return null;
            if (HiddenRegistry.IsHiddenOrOrdered(pawn)
                || CATactical.HasExplicitOrder(pawn) && !proactiveHold)
                return null;
            Lord lord = pawn.GetLord();
            if (lord != null && lord.LordJob is LordJob_CAStackBreach) return null;
            WithdrawalMapComponent withdrawal =
                pawn.Map.GetComponent<WithdrawalMapComponent>();
            if (withdrawal != null && withdrawal.InPlan(pawn)) return null;
            MissionCasualtyKnowledgeMapComponent mission =
                MissionCasualtyKnowledgeMapComponent.For(pawn.Map);
            if (mission != null && mission.HasBeneficiaries(pawn)) return null;

            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
            if (knowledge == null) return null;
            knowledge.CopyFreshWelfare(pawn, factBuffer);

            Job clinical = TryClinicalAction(pawn, settings, knowledge,
                factBuffer, visiblePeerOnly);
            if (clinical != null) return clinical;

            bool immediateDanger = PerceivesActiveDanger(pawn, settings);
            if (!immediateDanger)
            {
                Job rescue = TryFriendlyRescueAction(pawn, settings,
                    knowledge, factBuffer);
                if (rescue != null) return rescue;
            }

            if (AutonomyComponent.LevelOf(pawn) >= 2 && !immediateDanger)
            {
                Job check = TryWelfareCheck(pawn, settings, knowledge,
                    factBuffer, visiblePeerOnly);
                if (check != null) return check;

                if (visiblePeerOnly) return null;
                AccountabilitySnapshot accountability;
                if (knowledge.TryBeginAccountabilityCheck(pawn,
                    out accountability))
                {
                    Job job = JobMaker.MakeJob(CA_Defs.CheckWelfare,
                        accountability.LastConfirmedCell);
                    job.count = accountability.SubjectId;
                    job.locomotionUrgency = LocomotionUrgency.Jog;
                    knowledge.RegisterWelfareCheck(job,
                        accountability.SubjectId,
                        accountability.LastConfirmedTick,
                        accountability.StateTick,
                        accountability.Revision,
                        accountability.LastConfirmedCell,
                        WelfareCheckOrigin.Accountability);
                    CATrace.Pawn(pawn, "checks last confirmed position of direct report "
                        + accountability.SubjectId + " at "
                        + accountability.LastConfirmedCell);
                    return job;
                }
            }
            return null;
        }

        private static Job TryClinicalAction(Pawn pawn,
            AwarenessSettings settings, KnowledgeMapComponent knowledge,
            List<WelfareFactSnapshot> facts, bool visiblePeerOnly)
        {
            if (!settings.fieldMedicine || AutonomyComponent.LevelOf(pawn) < 2)
                return null;
            if (pawn.WorkTagIsDisabled(WorkTags.Caring)) return null;
            int medicineSkill = CAClinicalObservation.SkillLevel(pawn,
                SkillDefOf.Medicine);
            if (medicineSkill < 4) return null;
            int now = Find.TickManager.TicksGame;
            WelfareFactSnapshot best = default;
            Pawn bestPatient = null;
            int bestRemaining = int.MaxValue;
            for (int i = 0; i < facts.Count; i++)
            {
                WelfareFactSnapshot fact = facts[i];
                if (fact.State != WelfareState.Assessed
                    || !fact.ObservedNeedsTend) continue;
                if (fact.EstimatedDeathTick != int.MaxValue
                    && fact.EstimatedDeathTick <= now)
                {
                    knowledge.MarkWelfareUnassessed(pawn, fact.SubjectId);
                    continue;
                }
                int remaining = fact.EstimatedDeathTick == int.MaxValue
                    ? int.MaxValue : fact.EstimatedDeathTick - now;
                if (remaining == int.MaxValue) continue;
                if (medicineSkill < 6 && remaining > 45000) continue;
                Pawn patient;
                if (!knowledge.TryFindPawnById(fact.SubjectId, out patient)
                    || !IsSameTeamPeer(pawn, patient)
                    || !knowledge.CanActOnCurrentWelfare(pawn, patient)
                    || visiblePeerOnly && !IsSameTeamPeer(pawn, patient))
                    continue;
                if (bestPatient == null || remaining < bestRemaining)
                {
                    best = fact;
                    bestPatient = patient;
                    bestRemaining = remaining;
                }
            }
            if (bestPatient == null) return null;
            Job tend = CAClinicalObservation.TendJob(pawn, bestPatient);
            if (tend == null) return null;
            knowledge.MarkWelfareUnassessed(pawn, best.SubjectId);
            CATrace.Pawn(pawn, "stabilizes known casualty "
                + bestPatient.LabelShort + " (assessed bleed death "
                + (bestRemaining == int.MaxValue ? "not projected"
                    : bestRemaining + " ticks") + ")");
            return tend;
        }

        private static Job TryFriendlyRescueAction(Pawn rescuer,
            AwarenessSettings settings, KnowledgeMapComponent knowledge,
            List<WelfareFactSnapshot> facts)
        {
            if ((!settings.fieldMedicine && !settings.lifeSafety)
                || rescuer.WorkTagIsDisabled(WorkTags.Caring)) return null;
            Pawn best = null;
            int bestUrgency = int.MaxValue;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < facts.Count; i++)
            {
                WelfareFactSnapshot fact = facts[i];
                if (!fact.Actionable || !fact.ObservedDowned
                    || fact.SubjectId == rescuer.thingIDNumber) continue;
                Pawn patient;
                if (!knowledge.TryFindPawnById(fact.SubjectId, out patient)
                    || !IsSameTeamPeer(rescuer, patient) || !patient.Downed
                    || patient.Dead || patient.InBed()
                    || !HealthAIUtility.WantsToBeRescued(patient)
                    || patient.IsForbidden(rescuer)
                    || !knowledge.CanActOnCurrentWelfare(rescuer, patient))
                    continue;
                int urgency = fact.EstimatedDeathTick == int.MaxValue
                    ? int.MaxValue : Math.Max(0,
                        fact.EstimatedDeathTick - Find.TickManager.TicksGame);
                int distance = rescuer.Position.DistanceToSquared(patient.Position);
                if (best == null || urgency < bestUrgency
                    || urgency == bestUrgency && distance < bestDistance)
                {
                    best = patient;
                    bestUrgency = urgency;
                    bestDistance = distance;
                }
            }
            if (best == null) return null;
            Building_Bed bed = RestUtility.FindBedFor(best, rescuer,
                checkSocialProperness: false, ignoreOtherReservations: false,
                best.GuestStatus);
            if (bed == null || !best.CanReserve(bed)
                || !rescuer.CanReserveAndReach(best, PathEndMode.OnCell,
                    Danger.Deadly)) return null;
            Job rescue = JobMaker.MakeJob(JobDefOf.Rescue, best, bed);
            rescue.count = 1;
            CATrace.Pawn(rescuer, "carries known same-team casualty "
                + best.LabelShort + " to " + bed.LabelShort
                + " before considering hostile aftermath",
                contact: best.Position, destination: bed.Position,
                anchor: rescuer.Position);
            return rescue;
        }

        private static Job TryWelfareCheck(Pawn pawn,
            AwarenessSettings settings, KnowledgeMapComponent knowledge,
            List<WelfareFactSnapshot> facts, bool visiblePeerOnly)
        {
            if (!settings.fieldMedicine && !settings.lifeSafety) return null;
            if (pawn.WorkTagIsDisabled(WorkTags.Caring)) return null;
            int level = AutonomyComponent.LevelOf(pawn);
            WelfareFactSnapshot best = default;
            bool foundBest = false;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < facts.Count; i++)
            {
                WelfareFactSnapshot fact = facts[i];
                if (!fact.Actionable || fact.SubjectId == pawn.thingIDNumber
                    || !fact.Cell.IsValid || !fact.Cell.InBounds(pawn.Map)
                    || fact.Cell.IsForbidden(pawn)) continue;
                Pawn peer;
                if (!knowledge.TryFindPawnById(fact.SubjectId, out peer)
                    || !IsSameTeamPeer(pawn, peer)) continue;
                if (visiblePeerOnly
                    && !knowledge.CanActOnCurrentWelfare(pawn, peer))
                    continue;
                if (!settings.fieldMedicine
                    && !fact.ObservedTemperatureDanger) continue;
                if (fact.State == WelfareState.Assessed)
                {
                    bool staleAssessment = CAClinicalObservation
                        .AssessmentNeedsRefresh(fact,
                            Find.TickManager.TicksGame);
                    if (staleAssessment)
                    {
                        knowledge.MarkWelfareUnassessed(pawn, fact.SubjectId);
                        if (!knowledge.TryGetFreshWelfare(pawn, fact.SubjectId,
                            out fact)) continue;
                    }
                    else if (fact.Evidence.IsDirect) continue;
                }
                int distance = pawn.Position.DistanceToSquared(fact.Cell);
                if (level == 2 && distance > 900) continue;
                if (!pawn.CanReach(fact.Cell, PathEndMode.Touch,
                    Danger.Deadly)) continue;
                if (!foundBest || distance < bestDistance)
                {
                    best = fact;
                    foundBest = true;
                    bestDistance = distance;
                }
            }
            if (!foundBest) return null;
            CAWelfareThresholdSupportMapComponent support =
                CAWelfareThresholdSupportMapComponent.For(pawn.Map);
            if (support != null && !support.AuthorizesOrRequests(pawn, best))
            {
                Job wait;
                return support.TryMakeRequesterWaitJob(pawn, out wait)
                    ? wait : null;
            }
            JobDriver_CACheckWelfare current = pawn.jobs?.curDriver
                as JobDriver_CACheckWelfare;
            if (current != null && current.Matches(best)) return pawn.CurJob;
            Job job = JobMaker.MakeJob(CA_Defs.CheckWelfare, best.Cell);
            job.count = best.SubjectId;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            knowledge.RegisterWelfareCheck(job, best.SubjectId,
                best.SourceTick, best.StateTick, best.Revision, best.Cell,
                WelfareCheckOrigin.WelfareFact);
            support?.MarkRequesterReleased(pawn, job);
            CATrace.Pawn(pawn, "locates and assesses known welfare concern pawn "
                + best.SubjectId + " at " + best.Cell);
            return job;
        }

        private static bool IsSameTeamPeer(Pawn actor, Pawn subject)
        {
            return actor != null && subject != null && actor != subject
                && actor.Faction != null && subject.Faction == actor.Faction;
        }

        internal static bool PerceivesActiveDanger(Pawn pawn,
            AwarenessSettings settings)
        {
            return CACombatThreat.PerceivesActiveThreat(pawn);
        }

    }

    [HarmonyPatch(typeof(WorkGiver_RescueDowned),
        nameof(WorkGiver_RescueDowned.ShouldSkip),
        new Type[] { typeof(Pawn), typeof(bool) })]
    public static class Patch_CARescueDownedShouldSkip
    {
        public static bool Prefix(Pawn pawn, bool forced, ref bool __result)
        {
            if (forced) return true;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null
                || (!settings.lifeSafety && !settings.fieldMedicine)) return true;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn?.Map);
            __result = knowledge == null
                || !knowledge.HasCurrentActionableWelfare(pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_RescueDowned),
        nameof(WorkGiver_RescueDowned.HasJobOnThing),
        new Type[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
    public static class Patch_CARescueDownedHasJob
    {
        public static bool Prefix(Pawn pawn, Thing t, bool forced,
            ref bool __result)
        {
            if (forced) return true;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null
                || (!settings.lifeSafety && !settings.fieldMedicine)) return true;
            Pawn subject = t as Pawn;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn?.Map);
            if (subject != null && knowledge != null
                && knowledge.CanActOnCurrentWelfare(pawn, subject)) return true;
            __result = false;
            CATrace.Skip(pawn, "automatic rescue",
                "no current firsthand welfare fact for target");
            return false;
        }
    }

    public class JobDriver_CACheckWelfare : JobDriver
    {
        private int subjectId = -1;
        private int sourceTick = -1;
        private int stateTick = -1;
        private int revision = -1;
        private IntVec3 expectedCell = IntVec3.Invalid;
        private WelfareCheckOrigin origin;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref subjectId, "subjectId", -1);
            Scribe_Values.Look(ref sourceTick, "sourceTick", -1);
            Scribe_Values.Look(ref stateTick, "stateTick", -1);
            Scribe_Values.Look(ref revision, "revision", -1);
            Scribe_Values.Look(ref expectedCell, "expectedCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref origin, "origin",
                WelfareCheckOrigin.WelfareFact);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (subjectId > 0 && revision > 0 && expectedCell.IsValid)
                return true;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn?.Map);
            return knowledge != null && knowledge.TryTakeWelfareCheck(job.loadID,
                out subjectId, out sourceTick, out stateTick, out revision,
                out expectedCell, out origin);
        }

        public override bool IsContinuation(Job nextJob)
        {
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(delegate
            {
                return ShouldReplan()
                    ? JobCondition.InterruptForced : JobCondition.Ongoing;
            });
            this.FailOn(delegate
            {
                return pawn == null || pawn.Map == null
                    || !expectedCell.IsValid
                    || !expectedCell.InBounds(pawn.Map)
                    || expectedCell.IsForbidden(pawn);
            });
            if (pawn.Position != expectedCell)
                yield return Toils_Goto.GotoCell(TargetIndex.A,
                    PathEndMode.Touch);
            Toil examine = Toils_General.Wait(
                CAMissionTriage.AssessmentDuration(pawn));
            examine.WithProgressBarToilDelay(TargetIndex.A);
            examine.activeSkill = () => SkillDefOf.Medicine;
            examine.handlingFacing = true;
            examine.tickAction = delegate
            {
                pawn.rotationTracker.FaceCell(expectedCell);
            };
            yield return examine;
            Toil record = ToilMaker.MakeToil("CARecordWelfareCheck");
            record.initAction = delegate
            {
                KnowledgeMapComponent.For(pawn.Map)?.CompleteWelfareCheck(
                    pawn, subjectId, sourceTick, stateTick, revision,
                    expectedCell, origin);
            };
            record.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return record;
        }

        private bool ShouldReplan()
        {
            if (pawn == null || pawn.Map == null) return true;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings != null
                && JobGiver_CAWelfareResponse.PerceivesActiveDanger(
                    pawn, settings))
            {
                CAWelfareThresholdSupportMapComponent.For(pawn.Map)
                    ?.AbortRequester(pawn,
                        "requester perceived renewed active danger");
                return true;
            }
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn?.Map);
            return knowledge == null || !knowledge.WelfareCheckMatches(pawn,
                subjectId, sourceTick, stateTick, revision, expectedCell, origin);
        }

        internal bool Matches(WelfareFactSnapshot fact)
        {
            return origin == WelfareCheckOrigin.WelfareFact
                && subjectId == fact.SubjectId
                && stateTick == fact.StateTick
                && revision == fact.Revision
                && expectedCell == fact.Cell;
        }
    }
}
