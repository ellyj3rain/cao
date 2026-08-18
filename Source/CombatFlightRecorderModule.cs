using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Session-local causal evidence for combat playtests. The recorder owns no
    // MapComponent/GameComponent and exposes no IExposable state: observations can
    // never enter a save. All retained values are detached primitive copies.
    public static class CACombatFlightRecorder
    {
        private const int SnapshotIntervalTicks = 30;
        private const int CombatFactTailTicks = 600;
        // Attack-target registration is a conservative passive bootstrap, not an
        // active-threat verdict. A stable set gets at most one minute of frames;
        // projectile, damage, recovery, or a changed target manifest can continue
        // or restart capture with explicit evidence.
        private const int RegistrationBootstrapTicks = 3600;
        private const int MaxJobEvents = 8192;
        private const int MaxBehaviorEvents = 8192;
        // A five-minute specimen with 15-25 humanlikes produces roughly
        // 8,500-14,000 full pawn frames. Keep the complete ordinary run by
        // default; the explicit ring counter still reports exceptional overflow.
        private const int MaxPawnSnapshots = 16384;
        private const int MaxDamageEvents = 4096;
        private const int MaxOperatorObservations = 1024;
        private const int RememberedContactTicks = 7500;
        private const float NearbyCombatRadius = 42f;

        private static readonly object sync = new object();
        private static readonly FieldInfo jobTrackerPawnField =
            AccessTools.Field(typeof(Pawn_JobTracker), "pawn");
        private static readonly FieldInfo knownContactsField =
            AccessTools.Field(typeof(KnowledgeMapComponent), "known");
        private static readonly FieldInfo tacticalOrderPawnsField =
            AccessTools.Field(typeof(LordJob_CATactical), "orderPawns");
        private static readonly FieldInfo tacticalEpisodesField =
            AccessTools.Field(typeof(LordJob_CATactical), "orderEpisodes");
        private static readonly FieldInfo tacticalOriginsField =
            AccessTools.Field(typeof(LordJob_CATactical), "orderOrigins");
        private static readonly FieldInfo tacticalControllersField =
            AccessTools.Field(typeof(LordJob_CATactical), "orderControllers");
        private static readonly FieldInfo tacticalIssuersField =
            AccessTools.Field(typeof(LordJob_CATactical), "orderIssuers");
        private static readonly FieldInfo tacticalFireSuppressedField =
            AccessTools.Field(typeof(LordJob_CATactical), "orderFireSuppressed");
        private static readonly FieldInfo combatMovementsField =
            AccessTools.Field(typeof(CACombatIntent), "movements");
        private static readonly FieldInfo shelterCellsField =
            AccessTools.Field(typeof(RaidResponseMapComponent), "shelterCells");
        private static readonly FieldInfo shelterEpisodesField =
            AccessTools.Field(typeof(RaidResponseMapComponent), "shelterEpisodes");
        private static readonly FieldInfo attackTargetsByFactionField =
            AccessTools.Field(typeof(AttackTargetsCache),
                "targetsHostileToFaction");
        private static readonly FieldInfo roomStatsAndRoleDirtyField =
            AccessTools.Field(typeof(Room), "statsAndRoleDirty");
        private static readonly FieldInfo roomRoleField =
            AccessTools.Field(typeof(Room), "role");
        private static readonly FieldInfo roomCachedCellCountField =
            AccessTools.Field(typeof(Room), "cachedCellCount");
        private static readonly FieldInfo roomCachedOpenRoofCountField =
            AccessTools.Field(typeof(Room), "cachedOpenRoofCount");
        private static readonly FieldInfo hediffSetCachedPainField =
            AccessTools.Field(typeof(HediffSet), "cachedPain");
        private static readonly FieldInfo hediffSetCachedBleedRateField =
            AccessTools.Field(typeof(HediffSet), "cachedBleedRate");
        private static readonly FieldInfo hediffSeverityField =
            AccessTools.Field(typeof(Hediff), "severityInt");
        private static readonly FieldInfo hediffPartField =
            AccessTools.Field(typeof(Hediff), "part");
        private static readonly FieldInfo summaryHealthDirtyField =
            AccessTools.Field(typeof(SummaryHealthHandler), "dirty");
        private static readonly FieldInfo summaryHealthCachedPercentField =
            AccessTools.Field(typeof(SummaryHealthHandler),
                "cachedSummaryHealthPercent");
        private static readonly FieldInfo capacityLevelsField =
            AccessTools.Field(typeof(PawnCapacitiesHandler),
                "cachedCapacityLevels");
        private static readonly Type capacityCacheElementType =
            typeof(PawnCapacitiesHandler).GetNestedType("CacheElement",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo capacityCacheStatusField =
            capacityCacheElementType != null
                ? AccessTools.Field(capacityCacheElementType, "status") : null;
        private static readonly FieldInfo capacityCacheValueField =
            capacityCacheElementType != null
                ? AccessTools.Field(capacityCacheElementType, "value") : null;
        private static readonly Type capacityDefMapType =
            capacityCacheElementType != null
                ? typeof(DefMap<,>).MakeGenericType(typeof(PawnCapacityDef),
                    capacityCacheElementType) : null;
        private static readonly FieldInfo capacityDefMapValuesField =
            capacityDefMapType != null
                ? AccessTools.Field(capacityDefMapType, "values") : null;
        private static readonly FieldInfo statDefWorkerField =
            AccessTools.Field(typeof(StatDef), "workerInt");
        private static readonly FieldInfo statTemporaryCacheField =
            AccessTools.Field(typeof(StatWorker), "temporaryStatCache");
        private static readonly FieldInfo statImmutableCacheField =
            AccessTools.Field(typeof(StatWorker), "immutableStatCache");
        private static readonly FieldInfo statCacheValueField =
            AccessTools.Field(typeof(StatCacheEntry), "statValue");
        private static readonly FieldInfo statCacheTickField =
            AccessTools.Field(typeof(StatCacheEntry), "gameTick");
        private static readonly FieldInfo needCurrentLevelField =
            AccessTools.Field(typeof(Need), "curLevelInt");
        private static readonly FieldInfo verbTrackerVerbsField =
            AccessTools.Field(typeof(VerbTracker), "verbs");
        private static readonly FieldInfo mechDeactivatedField =
            AccessTools.Field(typeof(CompMechanoid), "deactivated");
        private static readonly FieldInfo regionCachedDangersField =
            AccessTools.Field(typeof(Region), "cachedDangers");
        private static readonly FieldInfo regionCachedDangersTickField =
            AccessTools.Field(typeof(Region), "cachedDangersForTick");

        private static RingBuffer<Dictionary<string, object>> jobEvents =
            new RingBuffer<Dictionary<string, object>>(MaxJobEvents);
        private static RingBuffer<Dictionary<string, object>> behaviorEvents =
            new RingBuffer<Dictionary<string, object>>(MaxBehaviorEvents);
        private static RingBuffer<Dictionary<string, object>> pawnSnapshots =
            new RingBuffer<Dictionary<string, object>>(MaxPawnSnapshots);
        private static RingBuffer<Dictionary<string, object>> damageEvents =
            new RingBuffer<Dictionary<string, object>>(MaxDamageEvents);
        private static RingBuffer<Dictionary<string, object>> operatorObservations =
            new RingBuffer<Dictionary<string, object>>(MaxOperatorObservations);
        private static readonly Dictionary<int, int> lastCombatFactTicks =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, long>
            registrationSignatures = new Dictionary<int, long>();
        private static readonly Dictionary<int, int>
            registrationBootstrapStartTicks = new Dictionary<int, int>();
        private static readonly Dictionary<int, ActiveJobMetadata> activeJobs =
            new Dictionary<int, ActiveJobMetadata>();
        private static readonly Dictionary<int, CombatDiagnosticRetention>
            retainedCombatDiagnostics =
                new Dictionary<int, CombatDiagnosticRetention>();
        private static readonly Dictionary<string, long> failuresByContext =
            new Dictionary<string, long>();

        private static Game sessionGame;
        private static string sessionId;
        private static string sessionStartedUtc;
        private static int sessionStartedTick = -1;
        private static int sessionStartedAbsTick = -1;
        private static int lastSnapshotTick = int.MinValue;
        private static long nextSequence;
        private static long observationFailureCount;
        private static long exportFailureCount;
        private static int lastFailureTick = -1;
        private static string lastFailureContext;
        private static string lastFailure;

        [ThreadStatic]
        private static JobStartState currentStartJobScope;
        internal sealed class JobStartState
        {
            internal JobStartState parentScope;
            internal Pawn pawn;
            internal Pawn_JobTracker tracker;
            internal Job requestedReference;
            internal Job previousReference;
            internal Dictionary<string, object> previousJob;
            internal Dictionary<string, object> startedJob;
            internal Dictionary<string, object> actorBefore;
            internal Dictionary<string, object> actorAtStart;
            internal Dictionary<string, object> proposalEvent;
            internal bool? previousFromQueue;
            internal JobCondition requestedCondition;
            internal bool requestedFromQueue;
            internal int ticksGame;
            internal int ticksAbs;
            internal long proposedSequence;
            internal long endSequence;
            internal long startSequence;
            internal long requestedEndSequence;
            internal bool record;
            internal bool retainRequestedLifecycle;
            internal bool completed;
            internal bool startRecorded;
            internal bool requestedEndRecorded;
            internal bool scopeFinished;
        }

        internal sealed class JobEndState
        {
            internal Pawn pawn;
            internal Job previousReference;
            internal Dictionary<string, object> previousJob;
            internal Dictionary<string, object> actorBefore;
            internal bool? previousFromQueue;
            internal int ticksGame;
            internal int ticksAbs;
            internal long sequence;
            internal bool record;
            internal JobStartState startScope;
        }

        private sealed class ActiveJobMetadata
        {
            internal Job job;
            internal bool fromQueue;
        }

        private struct CombatDiagnosticRetention
        {
            internal int mapId;
            internal long signature;
        }

        private sealed class RingBuffer<T>
        {
            private readonly T[] values;
            private int start;
            private int count;

            internal RingBuffer(int capacity)
            {
                values = new T[capacity];
            }

            internal long Dropped { get; private set; }
            internal int Count => count;

            internal void Add(T value)
            {
                if (count < values.Length)
                {
                    values[(start + count) % values.Length] = value;
                    count++;
                    return;
                }
                values[start] = value;
                start = (start + 1) % values.Length;
                Dropped++;
            }

            internal List<T> Copy()
            {
                var result = new List<T>(count);
                for (int i = 0; i < count; i++)
                    result.Add(values[(start + i) % values.Length]);
                return result;
            }
        }

        internal static Pawn PawnOf(Pawn_JobTracker tracker)
        {
            if (tracker == null || jobTrackerPawnField == null) return null;
            try { return jobTrackerPawnField.GetValue(tracker) as Pawn; }
            catch (Exception exception)
            {
                RecordFailure("job-owner", exception);
                return null;
            }
        }

        internal static bool InStartJobScope => currentStartJobScope != null;

        internal static JobStartState BeginStartJob(Pawn_JobTracker tracker,
            Job requestedJob, JobCondition requestedCondition,
            ThinkNode requestedJobGiver, ThinkTreeDef requestedThinkTree,
            bool requestedFromQueue)
        {
            var state = new JobStartState
            {
                parentScope = currentStartJobScope,
                tracker = tracker,
                requestedReference = requestedJob,
                requestedCondition = requestedCondition,
                requestedFromQueue = requestedFromQueue
            };
            currentStartJobScope = state;
            try
            {
                Pawn pawn = PawnOf(tracker);
                state.pawn = pawn;
                state.previousReference = tracker != null
                    ? tracker.curJob : null;
                bool trackedPrior = IsTrackedActiveJob(pawn,
                    state.previousReference);
                bool inCaptureWindow = RelevantHumanlike(pawn)
                    && ShouldRecord(pawn);
                // Once a start was retained, its terminal transition remains part
                // of the specimen even if the hostile/tail gate expires immediately
                // before the replacement. Do not create a proposal outside combat
                // unless it is needed to close that already-recorded lifecycle.
                if (!RelevantHumanlike(pawn)
                    || !inCaptureWindow && !trackedPrior) return state;
                state.record = true;
                // A tracked prior may carry this one boundary just beyond the
                // combat tail, but the replacement must not become a perpetual
                // baton that keeps every later ordinary job in the specimen.
                state.retainRequestedLifecycle = inCaptureWindow;
                CaptureTicks(out state.ticksGame, out state.ticksAbs);
                state.proposedSequence = NextSequence();
                state.endSequence = NextSequence();
                state.startSequence = NextSequence();
                state.requestedEndSequence = NextSequence();
                state.previousJob = CaptureJob(tracker.curJob, pawn);
                state.actorBefore = CaptureEventActor(pawn);
                state.previousFromQueue = ActiveFromQueue(pawn,
                    state.previousReference);
                state.proposalEvent = JobEvent("proposed",
                    state.proposedSequence, state.ticksGame, state.ticksAbs,
                    pawn, state.previousJob, CaptureJob(requestedJob, pawn),
                    requestedCondition, requestedFromQueue,
                    state.actorBefore, state.actorBefore,
                    "Pawn_JobTracker.StartJob prefix");
                state.proposalEvent["lifecycle"] = "proposed";
                state.proposalEvent["outcome"] = "pending";
                state.proposalEvent["proposedJobGiverType"] =
                    requestedJobGiver != null
                        ? requestedJobGiver.GetType().FullName : null;
                state.proposalEvent["proposedThinkTree"] =
                    DefName(requestedThinkTree);
                AddJobEvent(state.proposalEvent);
            }
            catch (Exception exception)
            {
                state.record = false;
                RecordFailure("job-start-prefix", exception);
            }
            return state;
        }

        internal static void CompleteStartJob(Pawn_JobTracker tracker,
            Job newJob, JobCondition lastJobEndCondition, bool fromQueue,
            JobStartState state)
        {
            if (state == null || state.completed) return;
            state.completed = true;
            try
            {
                Pawn pawn = state.pawn ?? PawnOf(tracker);
                Job current = tracker != null ? tracker.curJob : null;
                if (!state.record)
                {
                    if (state.previousReference != null
                        && !ReferenceEquals(state.previousReference,
                            current))
                        RetireActiveJob(pawn, state.previousReference);
                    return;
                }
                bool successful = newJob != null
                    && ReferenceEquals(current, newJob)
                    && tracker.curDriver != null && !tracker.curDriver.ended;
                JobCondition effectiveCondition = lastJobEndCondition;
                if (state.previousReference != null
                    && effectiveCondition == JobCondition.None)
                    effectiveCondition = JobCondition.InterruptForced;

                if (state.previousReference != null
                    && !ReferenceEquals(state.previousReference, current))
                {
                    Dictionary<string, object> transitionJob =
                        state.startRecorded && state.startedJob != null
                            ? state.startedJob : CaptureJob(current, pawn);
                    Dictionary<string, object> transitionActor =
                        state.startRecorded && state.actorAtStart != null
                            ? state.actorAtStart : CaptureEventActor(pawn);
                    AddJobEvent(JobEvent("end", state.endSequence,
                        state.ticksGame, state.ticksAbs, pawn,
                        state.previousJob, transitionJob,
                        effectiveCondition, state.previousFromQueue,
                        state.actorBefore, transitionActor,
                        "StartJob replacement"));
                    RetireActiveJob(pawn, state.previousReference);
                }

                if (state.proposalEvent != null)
                    state.proposalEvent["outcome"] = successful
                        || state.startRecorded ? "started"
                        : current != null && !ReferenceEquals(current, newJob)
                            ? "superseded" : "not-started";
                if (!successful || state.startRecorded) return;
                RecordSuccessfulStart(state, pawn, newJob, fromQueue,
                    effectiveCondition,
                    "Pawn_JobTracker.StartJob completed");
            }
            catch (Exception exception)
            {
                RecordFailure("job-start-postfix", exception);
            }
        }

        internal static void FinishStartJobScope(JobStartState state,
            Exception exception)
        {
            if (exception != null)
            {
                RecordFailure("job-start-finalizer", exception);
                try
                {
                    if (state != null && !state.record)
                    {
                        Pawn unrecordedPawn = state.pawn
                            ?? PawnOf(state.tracker);
                        Job unrecordedCurrent = state.tracker != null
                            ? state.tracker.curJob : null;
                        if (state.previousReference != null
                            && !ReferenceEquals(state.previousReference,
                                unrecordedCurrent))
                            RetireActiveJob(unrecordedPawn,
                                state.previousReference);
                    }
                    if (state != null && state.record)
                    {
                        if (state.proposalEvent != null)
                        {
                            state.proposalEvent["outcome"] =
                                state.startRecorded
                                    ? "started" : "failed-exception";
                            state.proposalEvent["exception"] =
                                FailureText(exception);
                            state.proposalEvent["exceptionAfterStart"] =
                                state.startRecorded;
                        }
                        Pawn pawn = state.pawn ?? PawnOf(state.tracker);
                        Job current = state.tracker != null
                            ? state.tracker.curJob : null;
                        JobCondition condition = state.requestedCondition;
                        if (state.previousReference != null
                            && condition == JobCondition.None)
                            condition = JobCondition.InterruptForced;
                        if (state.previousReference != null
                            && !ReferenceEquals(state.previousReference,
                                current))
                        {
                            Dictionary<string, object> transitionJob =
                                state.startRecorded && state.startedJob != null
                                    ? state.startedJob
                                    : CaptureJob(current, pawn);
                            Dictionary<string, object> transitionActor =
                                state.startRecorded
                                    && state.actorAtStart != null
                                        ? state.actorAtStart
                                        : CaptureEventActor(pawn);
                            AddJobEvent(JobEvent("end", state.endSequence,
                                state.ticksGame, state.ticksAbs, pawn,
                                state.previousJob, transitionJob,
                                condition, state.previousFromQueue,
                                state.actorBefore, transitionActor,
                                "StartJob exception after observed prior transition"));
                            RetireActiveJob(pawn, state.previousReference);
                        }
                        if (state.startRecorded)
                        {
                            if (!ReferenceEquals(state.requestedReference,
                                    current)
                                && !state.requestedEndRecorded)
                            {
                                AddJobEvent(JobEvent("end",
                                    state.requestedEndSequence,
                                    state.ticksGame, state.ticksAbs, pawn,
                                    state.startedJob ?? CaptureJob(
                                        state.requestedReference, pawn),
                                    CaptureJob(current, pawn),
                                    JobCondition.Errored,
                                    state.requestedFromQueue,
                                    state.actorAtStart ?? state.actorBefore,
                                    CaptureEventActor(pawn),
                                    "StartJob exception after successful initial start"));
                                state.requestedEndRecorded = true;
                                RetireActiveJob(pawn,
                                    state.requestedReference);
                            }
                        }
                        else
                        {
                            AddJobEvent(JobEvent("start-failed",
                                state.startSequence, state.ticksGame,
                                state.ticksAbs, pawn, state.previousJob,
                                CaptureJob(current, pawn),
                                JobCondition.Errored,
                                state.requestedFromQueue, state.actorBefore,
                                CaptureEventActor(pawn),
                                "Pawn_JobTracker.StartJob exception"));
                        }
                    }
                }
                catch (Exception reconcileException)
                {
                    RecordFailure("job-start-exception-reconcile",
                        reconcileException);
                }
            }
            FinishStartScope(state);
        }

        // StartJob calls this native boundary only after reservations,
        // Notify_Starting, and SetupToils have completed. Recording immediately
        // before the first toil is authoritative even when an Instant toil ends
        // the job recursively before StartJob itself returns.
        internal static void NoteDriverReadyForFirstToil(JobDriver driver)
        {
            if (driver == null) return;
            JobStartState state = MatchingStartScope(driver.pawn?.jobs,
                driver.job);
            if (state == null || !state.record || state.completed
                || state.startRecorded) return;
            try
            {
                Pawn_JobTracker tracker = state.tracker;
                if (tracker == null
                    || !ReferenceEquals(tracker.curJob,
                        state.requestedReference)
                    || !ReferenceEquals(tracker.curDriver, driver)) return;
                Pawn pawn = state.pawn ?? driver.pawn;
                JobCondition condition = state.requestedCondition;
                if (state.previousReference != null
                    && condition == JobCondition.None)
                    condition = JobCondition.InterruptForced;
                RecordSuccessfulStart(state, pawn,
                    state.requestedReference, state.requestedFromQueue,
                    condition, "JobDriver.ReadyForNextToil initial boundary");
            }
            catch (Exception exception)
            {
                RecordFailure("job-start-ready-for-first-toil", exception);
            }
        }

        private static void RecordSuccessfulStart(JobStartState state,
            Pawn pawn, Job job, bool fromQueue, JobCondition condition,
            string hook)
        {
            if (state == null || state.startRecorded || job == null) return;
            Dictionary<string, object> startedJob = CaptureJob(job, pawn);
            Dictionary<string, object> actorAtStart =
                CaptureEventActor(pawn);
            Dictionary<string, object> value = JobEvent("start",
                state.startSequence, state.ticksGame, state.ticksAbs, pawn,
                state.previousJob, startedJob, condition,
                fromQueue, state.actorBefore, actorAtStart, hook);
            value["lifecycle"] = "started";
            AddJobEvent(value);
            state.startedJob = startedJob;
            state.actorAtStart = actorAtStart;
            state.startRecorded = true;
            if (state.proposalEvent != null)
                state.proposalEvent["outcome"] = "started";
            if (state.retainRequestedLifecycle)
                RememberActiveJob(pawn, job, fromQueue);
        }

        internal static JobEndState BeginEndJob(Pawn_JobTracker tracker,
            string context, bool suppressDuringStart)
        {
            var state = new JobEndState();
            if (suppressDuringStart && InStartJobScope) return state;
            try
            {
                Pawn pawn = PawnOf(tracker);
                Job prior = tracker != null ? tracker.curJob : null;
                state.pawn = pawn;
                state.previousReference = prior;
                JobStartState startScope = MatchingStartScope(tracker,
                    prior);
                // Reservation failure installs a driver temporarily, then calls
                // EndCurrentJob before the authoritative first-toil boundary.
                // That attempted job has no successful start and therefore no
                // lifecycle end to emit.
                if (startScope != null && !startScope.startRecorded)
                    return state;
                if (prior == null || !RelevantHumanlike(pawn)
                    || !ShouldRecord(pawn)
                        && !IsTrackedActiveJob(pawn, prior)) return state;
                state.record = true;
                CaptureTicks(out state.ticksGame, out state.ticksAbs);
                state.startScope = startScope;
                state.sequence = startScope != null
                    ? startScope.requestedEndSequence : NextSequence();
                state.previousJob = CaptureJob(prior, pawn);
                state.actorBefore = CaptureEventActor(pawn);
                state.previousFromQueue = ActiveFromQueue(pawn, prior);
            }
            catch (Exception exception)
            {
                state.record = false;
                RecordFailure(context + "-prefix", exception);
            }
            return state;
        }

        internal static void CompleteEndJob(Pawn_JobTracker tracker,
            JobCondition condition, JobEndState state, string context)
        {
            if (state == null) return;
            try
            {
                Job current = tracker != null ? tracker.curJob : null;
                if (ReferenceEquals(state.previousReference, current)) return;
                Pawn pawn = state.pawn ?? PawnOf(tracker);
                if (state.record)
                {
                    AddJobEvent(JobEvent("end", state.sequence,
                        state.ticksGame, state.ticksAbs, pawn,
                        state.previousJob, CaptureJob(current, pawn),
                        condition, state.previousFromQueue,
                        state.actorBefore, CaptureEventActor(pawn),
                        context));
                    if (state.startScope != null)
                        state.startScope.requestedEndRecorded = true;
                }
                RetireActiveJob(pawn, state.previousReference);
            }
            catch (Exception exception)
            {
                RecordFailure(context + "-postfix", exception);
            }
        }

        private static JobStartState MatchingStartScope(
            Pawn_JobTracker tracker, Job job)
        {
            if (tracker == null || job == null) return null;
            for (JobStartState state = currentStartJobScope;
                state != null; state = state.parentScope)
                if (!state.scopeFinished
                    && ReferenceEquals(state.tracker, tracker)
                    && ReferenceEquals(state.requestedReference, job))
                    return state;
            return null;
        }

        private static void FinishStartScope(JobStartState state)
        {
            if (state == null || state.scopeFinished) return;
            state.scopeFinished = true;
            if (ReferenceEquals(currentStartJobScope, state))
                currentStartJobScope = state.parentScope;
            else
            {
                JobStartState child = currentStartJobScope;
                while (child != null
                    && !ReferenceEquals(child.parentScope, state))
                    child = child.parentScope;
                if (child != null) child.parentScope = state.parentScope;
            }
        }

        internal static void NoteJobHookFailure(string context,
            Exception exception)
        {
            if (exception != null) RecordFailure(context, exception);
        }

        // Pawn.PostApplyDamage runs after the DamageWorker has applied the
        // post-armor hediff but before Pawn_HealthTracker can kill and return
        // without notifying the job tracker. Recording here retains fatal hits
        // as well as ordinary ones. It never changes DamageInfo or pawn state.
        public static void NoteAppliedDamage(Pawn pawn, DamageInfo dinfo,
            float totalDamageDealt)
        {
            try
            {
                if (!RelevantHumanlike(pawn)
                    || !ShouldRecord(pawn,
                        establishCombatFact: true)) return;
                int tick;
                int absTick;
                CaptureTicks(out tick, out absTick);
                Thing instigator = dinfo.Instigator;
                var damage = Record("damage", NextSequence(), tick, absTick);
                damage["source"] = Source("Pawn.PostApplyDamage",
                    "damage", "automatic");
                damage["mapId"] = MapId(pawn.MapHeld);
                damage["victim"] = CapturePawnIdentity(pawn);
                damage["victimState"] = CaptureEventActor(pawn);
                damage["damageDef"] = DefName(dinfo.Def);
                damage["amount"] = dinfo.Amount;
                damage["totalDamageDealt"] = totalDamageDealt;
                damage["armorPenetration"] = dinfo.ArmorPenetrationInt;
                damage["angle"] = dinfo.Angle;
                damage["category"] = dinfo.Category.ToString();
                damage["weaponDef"] = DefName(dinfo.Weapon);
                damage["weaponQuality"] = dinfo.WeaponQuality.ToString();
                damage["hitPart"] = dinfo.HitPart != null
                    ? dinfo.HitPart.Label : null;
                damage["bodyPartHeight"] = dinfo.Height.ToString();
                damage["bodyPartDepth"] = dinfo.Depth.ToString();
                damage["instigator"] = CaptureThing(instigator);
                damage["intendedTarget"] = CaptureThing(dinfo.IntendedTarget);
                damage["instigatorGuilty"] = dinfo.InstigatorGuilty;
                damage["ignoreArmor"] = dinfo.IgnoreArmor;
                damage["checkForJobOverride"] = dinfo.CheckForJobOverride;
                lock (sync) damageEvents.Add(damage);
            }
            catch (Exception exception)
            {
                RecordFailure("damage-hook", exception);
            }
        }

        internal static void Tick()
        {
            try
            {
                if (!CATrace.On || Current.Game == null
                    || Find.TickManager == null) return;
                EnsureSession(Current.Game);
                int now = Find.TickManager.TicksGame;
                List<Map> maps = Find.Maps;
                CaptureCombatDiagnosticTransitions(maps, now);
                if (lastSnapshotTick != int.MinValue
                    && now >= lastSnapshotTick
                    && now - lastSnapshotTick < SnapshotIntervalTicks)
                    return;
                lastSnapshotTick = now;

                for (int i = 0; i < maps.Count; i++)
                {
                    Map map = maps[i];
                    bool registeredAttackTarget;
                    bool registrationBootstrap;
                    int registrationAge;
                    int registeredTargetCount;
                    ObserveRegistrationGate(map, now,
                        out registeredAttackTarget,
                        out registrationBootstrap, out registrationAge,
                        out registeredTargetCount);
                    int last;
                    bool combatFactTail = lastCombatFactTicks.TryGetValue(
                        map.uniqueID,
                        out last) && now >= last
                        && now - last <= CombatFactTailTicks;
                    if (!registrationBootstrap && !combatFactTail) continue;
                    CaptureMap(map, registeredAttackTarget,
                        registrationBootstrap, registrationAge,
                        registeredTargetCount, combatFactTail);
                }
                PruneCaptureMaps(now, maps);
            }
            catch (Exception exception)
            {
                RecordFailure("tick", exception);
            }
        }

        // Writes only to RimWorld's developer-output directory. Building the JSON
        // consumes retained primitive copies and does not query the live game.
        public static string ExportReceipt()
        {
            try
            {
                if (Current.Game != null) EnsureSession(Current.Game);
                string json = BuildJson();
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                string path = Path.Combine(GenFilePaths.DevOutputFolderPath,
                    "ColonistAwareness.combat-flight.json");
                Directory.CreateDirectory(GenFilePaths.DevOutputFolderPath);
                File.WriteAllBytes(path, bytes);
                string hash = Sha256(bytes);
                lock (sync)
                {
                    return "[CA] combat-flight export: PASS; path " + path
                        + "; bytes " + bytes.Length
                        + "; SHA256 " + hash
                        + "; job events " + jobEvents.Count
                        + "; behavior events " + behaviorEvents.Count
                        + "; pawn snapshots " + pawnSnapshots.Count
                        + "; damage events " + damageEvents.Count
                        + "; operator observations "
                            + operatorObservations.Count
                        + "; dropped job events " + jobEvents.Dropped
                        + "; dropped behavior events "
                            + behaviorEvents.Dropped
                        + "; dropped pawn snapshots " + pawnSnapshots.Dropped
                        + "; dropped damage events " + damageEvents.Dropped
                        + "; dropped operator observations "
                            + operatorObservations.Dropped
                        + "; observation failures "
                            + observationFailureCount
                        + "; export failures " + exportFailureCount
                        + "; export is read-only relative to maps, pawns, jobs, combat state, and saves";
                }
            }
            catch (Exception exception)
            {
                lock (sync) exportFailureCount++;
                return "[CA] combat-flight export: FAIL; " + exception;
            }
        }

        // Allows a dev action or test bridge to put an operator-authored visual or
        // gameplay observation on the same two-clock timeline as native evidence.
        public static void NoteOperatorObservation(string observation,
            Pawn pawn = null, string source = "operator")
        {
            if (observation.NullOrEmpty()) return;
            try
            {
                if (Current.Game == null) return;
                EnsureSession(Current.Game);
                int tick;
                int absTick;
                CaptureTicks(out tick, out absTick);
                var value = Record("operator-observation", NextSequence(),
                    tick, absTick);
                value["source"] = Source(source,
                    "operator-observation", "authored");
                value["observation"] = observation;
                value["pawn"] = CapturePawnIdentity(pawn);
                value["actorState"] = pawn != null
                    ? CaptureEventActor(pawn) : null;
                lock (sync) operatorObservations.Add(value);
            }
            catch (Exception exception)
            {
                RecordFailure("operator-observation", exception);
            }
        }

        // CATrace remains the concise human-readable receipt. This parallel
        // structured record puts the same decision or refusal on the automatic
        // two-clock combat timeline without parsing Player.log after the fact.
        internal static void NoteBehaviorTrace(Pawn pawn, string outcome,
            string behavior, string detail, Thing target = null,
            IntVec3? contact = null, IntVec3? destination = null,
            IntVec3? anchor = null, CAIntentContext? intent = null)
        {
            try
            {
                if (!RelevantHumanlike(pawn)
                    || !ShouldRecord(pawn)) return;
                int tick;
                int absTick;
                CaptureTicks(out tick, out absTick);
                var value = Record("ca-behavior", NextSequence(), tick,
                    absTick);
                value["source"] = Source("CATrace",
                    "behavior-decision-receipt", "automatic");
                value["outcome"] = outcome;
                value["behavior"] = behavior;
                value["detail"] = detail;
                value["mapId"] = MapId(pawn.MapHeld);
                value["pawn"] = CapturePawnIdentity(pawn);
                value["actorState"] = CaptureEventActor(pawn);
                value["target"] = CaptureThing(target);
                value["contact"] = contact.HasValue
                    ? Cell(contact.Value) : null;
                value["destination"] = destination.HasValue
                    ? Cell(destination.Value) : null;
                value["anchor"] = anchor.HasValue
                    ? Cell(anchor.Value) : null;
                value["intent"] = intent.HasValue && intent.Value.IsValid
                    ? CaptureIntent(intent.Value) : null;
                lock (sync) behaviorEvents.Add(value);
            }
            catch (Exception exception)
            {
                RecordFailure("behavior-trace", exception);
            }
        }

        // Actorless CA lifecycle messages (for example a breach GO or restored
        // standing-order count) are still part of the causal timeline. They do not
        // pretend to identify a pawn or intent that the caller did not provide.
        internal static void NoteSystemTrace(string detail)
        {
            try
            {
                if (!CATrace.On || Current.Game == null) return;
                EnsureSession(Current.Game);
                int tick;
                int absTick;
                CaptureTicks(out tick, out absTick);
                var value = Record("ca-system", NextSequence(), tick,
                    absTick);
                value["source"] = Source("CATrace.Log",
                    "system-lifecycle-receipt", "automatic");
                value["detail"] = detail;
                // Actorless callers do not establish which loaded map originated
                // the event. Null is evidence-preserving; Find.CurrentMap could be
                // a different visible map than a background lord/component.
                value["mapId"] = null;
                value["pawn"] = null;
                value["intent"] = null;
                lock (sync) behaviorEvents.Add(value);
            }
            catch (Exception exception)
            {
                RecordFailure("system-trace", exception);
            }
        }

        private static Dictionary<string, object> CaptureIntent(
            CAIntentContext context)
        {
            return new Dictionary<string, object>
            {
                { "episodeId", context.EpisodeId },
                { "origin", context.Origin.ToString() },
                { "controller", context.Controller.ToString() },
                { "issuerId", context.IssuerId }
            };
        }

        // Full pawn frames remain on the 30-tick cadence, while these detached
        // fixed-size diagnostics are sampled every tick and emitted only when their
        // meaningful state changes. That preserves one-tick Pending searches and
        // post-load route adoption without turning the behavior ring into a tick log.
        private static void CaptureCombatDiagnosticTransitions(
            List<Map> maps, int now)
        {
            bool prune = now >= 0 && now % 300 == 0;
            HashSet<int> liveHumanlikes = prune ? new HashSet<int>() : null;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                bool registeredAttackTarget;
                bool registrationBootstrap;
                int registrationAge;
                int registeredTargetCount;
                ObserveRegistrationGate(map, now,
                    out registeredAttackTarget, out registrationBootstrap,
                    out registrationAge, out registeredTargetCount);
                int last;
                bool combatFactTail = lastCombatFactTicks.TryGetValue(
                    map.uniqueID,
                    out last) && now >= last
                    && now - last <= CombatFactTailTicks;
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int j = 0; j < pawns.Count; j++)
                {
                    Pawn pawn = pawns[j];
                    if (!RelevantHumanlike(pawn)) continue;
                    if (liveHumanlikes != null)
                        liveHumanlikes.Add(pawn.thingIDNumber);

                    CACombatRecoveryDiagnosticState recoveryState =
                        default(CACombatRecoveryDiagnosticState);
                    CACombatRecoveryMapComponent recovery = map
                        .GetComponent<CACombatRecoveryMapComponent>();
                    bool hasRecovery = recovery != null
                        && recovery.TryGetDiagnosticState(pawn,
                            out recoveryState);
                    CAIncomingEvasionDiagnosticState incomingState;
                    bool hasIncoming = CACombatIntent
                        .TryGetIncomingEvasionDiagnosticState(pawn,
                            out incomingState);
                    int id = pawn.thingIDNumber;
                    CombatDiagnosticRetention retained;
                    bool hadState = retainedCombatDiagnostics.TryGetValue(id,
                        out retained);
                    bool hasState = hasRecovery || hasIncoming;
                    long signature = hasState
                        ? CombatDiagnosticSignature(map.uniqueID,
                            hasRecovery, recoveryState, hasIncoming,
                            incomingState)
                        : 0L;
                    bool transitioned = hasState
                        ? !hadState || retained.mapId != map.uniqueID
                            || retained.signature != signature
                        : hadState;
                    if (transitioned)
                    {
                        // A new, changed, or cleared actor-local recovery/projectile
                        // state is a combat fact. Restart the full bounded window
                        // even when the prior tail or registration bootstrap is still
                        // active; an unchanged persistent state cannot refresh it.
                        lastCombatFactTicks[map.uniqueID] = now;
                        combatFactTail = true;
                    }
                    if (!registrationBootstrap && !combatFactTail)
                    {
                        // Retain the last signature outside the capture window so an
                        // eventual state change or clearance can reopen one final
                        // bounded tail. Dropping it here would make stable state look
                        // newly observed every time the prior tail elapsed.
                        continue;
                    }
                    if (!hasState)
                    {
                        if (hadState)
                        {
                            EmitCombatDiagnosticEvent(pawn, map,
                                "cleared", false, recoveryState, false,
                                incomingState);
                            retainedCombatDiagnostics.Remove(id);
                        }
                        continue;
                    }
                    if (!transitioned) continue;
                    EmitCombatDiagnosticEvent(pawn, map,
                        hadState ? "changed" : "observed", hasRecovery,
                        recoveryState, hasIncoming, incomingState);
                    retainedCombatDiagnostics[id] =
                        new CombatDiagnosticRetention
                        {
                            mapId = map.uniqueID,
                            signature = signature
                        };
                }
            }

            if (liveHumanlikes == null) return;
            var remove = new List<int>();
            foreach (KeyValuePair<int, CombatDiagnosticRetention> pair
                in retainedCombatDiagnostics)
                if (!liveHumanlikes.Contains(pair.Key)) remove.Add(pair.Key);
            for (int i = 0; i < remove.Count; i++)
                retainedCombatDiagnostics.Remove(remove[i]);
        }

        private static void EmitCombatDiagnosticEvent(Pawn pawn, Map map,
            string outcome, bool hasRecovery,
            CACombatRecoveryDiagnosticState recoveryState,
            bool hasIncoming,
            CAIncomingEvasionDiagnosticState incomingState)
        {
            int tick;
            int absTick;
            CaptureTicks(out tick, out absTick);
            var value = Record("ca-combat-state", NextSequence(), tick,
                absTick);
            value["source"] = Source("CACombatFlightRecorder.Tick",
                "combat-diagnostic-transition", "automatic");
            value["outcome"] = outcome;
            value["mapId"] = MapId(map);
            value["pawn"] = CapturePawnIdentity(pawn);
            value["combatRecovery"] = hasRecovery
                ? CaptureRecoveryDiagnostic(recoveryState) : null;
            value["incomingExplosiveEvasion"] = hasIncoming
                ? CaptureIncomingEvasionDiagnostic(incomingState) : null;
            lock (sync) behaviorEvents.Add(value);
        }

        private static long CombatDiagnosticSignature(int mapId,
            bool hasRecovery, CACombatRecoveryDiagnosticState recovery,
            bool hasIncoming, CAIncomingEvasionDiagnosticState incoming)
        {
            unchecked
            {
                long value = 17;
                MixDiagnostic(ref value, mapId);
                MixDiagnostic(ref value, hasRecovery);
                if (hasRecovery)
                {
                    MixDiagnostic(ref value, recovery.IsRecovering);
                    MixDiagnostic(ref value, recovery.EpisodeId);
                    MixDiagnostic(ref value, recovery.HasMoveCooldown);
                    MixDiagnostic(ref value, recovery.NextMoveTick);
                    MixDiagnostic(ref value, recovery.HasOperatorOverride);
                    MixDiagnostic(ref value,
                        recovery.OperatorOverrideHarmTick);
                    MixDiagnostic(ref value,
                        recovery.OperatorOverrideUntilTick);
                    MixDiagnostic(ref value,
                        recovery.OperatorOverrideCombatPower);
                    MixDiagnostic(ref value,
                        recovery.OperatorOverrideBloodLoss);
                    MixDiagnostic(ref value,
                        recovery.OperatorOverrideBleedRate);
                    MixDiagnostic(ref value,
                        recovery.OperatorOverrideDeathTicks);
                }
                MixDiagnostic(ref value, hasIncoming);
                if (hasIncoming)
                {
                    MixDiagnostic(ref value, (int)incoming.Status);
                    MixDiagnostic(ref value, incoming.MapId);
                    MixDiagnostic(ref value, incoming.ObservedHazardCount);
                    MixDiagnostic(ref value, incoming.ActiveHazardCount);
                    MixDiagnostic(ref value, incoming.ProjectileId);
                    MixDiagnostic(ref value, incoming.LauncherId);
                    MixDiagnostic(ref value, incoming.HazardCenter.x);
                    MixDiagnostic(ref value, incoming.HazardCenter.y);
                    MixDiagnostic(ref value, incoming.HazardCenter.z);
                    MixDiagnostic(ref value, incoming.HazardRadius);
                    MixDiagnostic(ref value, incoming.DeadlineTick);
                    MixDiagnostic(ref value, incoming.EpisodeId);
                    MixDiagnostic(ref value, (int)incoming.IntentOrigin);
                    MixDiagnostic(ref value, incoming.Origin.x);
                    MixDiagnostic(ref value, incoming.Origin.y);
                    MixDiagnostic(ref value, incoming.Origin.z);
                    MixDiagnostic(ref value, incoming.Destination.x);
                    MixDiagnostic(ref value, incoming.Destination.y);
                    MixDiagnostic(ref value, incoming.Destination.z);
                    MixDiagnostic(ref value, incoming.Reconstructed);
                    MixDiagnostic(ref value, incoming.SearchActive);
                    MixDiagnostic(ref value,
                        incoming.SearchHazardSignature);
                    MixDiagnostic(ref value, incoming.ClearingCursor);
                    MixDiagnostic(ref value,
                        incoming.ClearingCandidateCount);
                    MixDiagnostic(ref value,
                        incoming.ClearingPathBudgetPerTick);
                    MixDiagnostic(ref value, incoming.ImprovingCursor);
                    MixDiagnostic(ref value,
                        incoming.ImprovingCandidateCount);
                    MixDiagnostic(ref value,
                        incoming.ImprovingPathBudgetPerTick);
                    MixDiagnostic(ref value,
                        incoming.LastFailedEvaluationTick);
                }
                return value;
            }
        }

        private static void MixDiagnostic(ref long value, bool part)
        {
            value = value * 31 + (part ? 1 : 0);
        }

        private static void MixDiagnostic(ref long value, int part)
        {
            value = value * 31 + part;
        }

        private static void MixDiagnostic(ref long value, float part)
        {
            value = value * 31 + part.GetHashCode();
        }

        private static void CaptureMap(Map map, bool registeredAttackTarget,
            bool registrationBootstrap, int registrationAge,
            int registeredTargetCount, bool combatFactTail)
        {
            var pawns = new List<Pawn>(map.mapPawns.AllHumanlike);
            var seen = new HashSet<int>();
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i] != null) seen.Add(pawns[i].thingIDNumber);

            List<Thing> corpses = map.listerThings
                .ThingsInGroup(ThingRequestGroup.Corpse);
            for (int i = 0; i < corpses.Count; i++)
            {
                Corpse corpse = corpses[i] as Corpse;
                Pawn dead = corpse != null ? corpse.InnerPawn : null;
                if (!RelevantHumanlike(dead)
                    || !seen.Add(dead.thingIDNumber)) continue;
                pawns.Add(dead);
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!RelevantHumanlike(pawn)) continue;
                try
                {
                    Dictionary<string, object> snapshot =
                        CapturePawnSnapshot(pawn, map,
                            registeredAttackTarget, registrationBootstrap,
                            registrationAge, registeredTargetCount,
                            combatFactTail);
                    lock (sync) pawnSnapshots.Add(snapshot);
                }
                catch (Exception exception)
                {
                    RecordFailure("pawn-snapshot", exception);
                }
            }
        }

        private static Dictionary<string, object> CapturePawnSnapshot(
            Pawn pawn, Map map, bool registeredAttackTarget,
            bool registrationBootstrap, int registrationAge,
            int registeredTargetCount, bool combatFactTail)
        {
            int tick;
            int absTick;
            CaptureTicks(out tick, out absTick);
            var result = Record("pawn-snapshot", NextSequence(), tick,
                absTick);
            result["source"] = Source("TickManager.DoSingleTick",
                "30-tick bounded-registration/combat-fact-tail map sweep",
                "automatic");
            result["mapId"] = map.uniqueID;
            result["mapRegisteredAttackTarget"] = registeredAttackTarget;
            result["registeredAttackTargetCount"] = registeredTargetCount;
            result["registrationBootstrapActive"] = registrationBootstrap;
            result["registrationBootstrapAgeTicks"] = registrationAge;
            result["registrationBootstrapLimitTicks"] =
                RegistrationBootstrapTicks;
            result["combatFactTailActive"] = combatFactTail;
            result["combatFactTailTicks"] = CombatFactTailTicks;
            result["captureGateActive"] = registrationBootstrap
                || combatFactTail;
            result["captureReason"] = registrationBootstrap
                ? "bounded-registration-bootstrap"
                : "damage-projectile-or-controller-tail";
            // The native actor-local predicate may populate reachability/capacity
            // caches and select an attack verb. The passive recorder therefore
            // names the value unavailable instead of changing simulation state in
            // order to measure it; behavior receipts retain actual decisions.
            result["actorLocalActiveThreat"] = null;
            result["actorLocalActiveThreatUnavailableReason"] =
                "passive recorder does not invoke the stateful native threat predicate";
            result["pawn"] = CapturePawnIdentity(pawn);
            result["status"] = CaptureStatus(pawn);
            result["skills"] = CaptureSkills(pawn);
            result["traits"] = CaptureTraits(pawn);
            result["directRelations"] = CaptureDirectRelations(pawn);
            result["needs"] = CaptureNeeds(pawn);
            result["moodMemories"] = CaptureMoodMemories(pawn);
            result["health"] = CaptureHealth(pawn);
            result["equipment"] = CaptureEquipment(pawn);
            result["apparel"] = CaptureApparel(pawn);
            result["communications"] = CaptureCommunications(pawn, map);
            result["inventory"] = CaptureInventory(pawn);
            result["carried"] = CaptureThing(
                pawn.carryTracker != null
                    ? pawn.carryTracker.CarriedThing : null);
            result["rememberedContacts"] = CaptureRememberedContacts(
                pawn, map, tick);
            result["rememberedWelfare"] = CaptureRememberedWelfare(
                pawn, map, tick);
            result["audibleCues"] = CaptureAudibleCues(pawn, map);
            result["mapConditions"] = CaptureMapConditions(map);
            result["jobs"] = CaptureJobs(pawn);
            result["path"] = CapturePath(pawn);
            result["combatAction"] = CaptureCombatAction(pawn);
            result["nearbyCombatGeometry"] = CaptureNearbyCombatGeometry(
                pawn, map);
            result["activeFriendlyFireSectors"] =
                CaptureActiveFriendlyFireSectors(pawn);
            result["environment"] = CaptureEnvironment(pawn, map);
            result["caState"] = CaptureCAState(pawn, map);
            return result;
        }

        private static Dictionary<string, object> CapturePawnIdentity(Pawn pawn)
        {
            if (pawn == null) return null;
            var result = new Dictionary<string, object>();
            result["thingId"] = pawn.ThingID;
            result["id"] = pawn.thingIDNumber;
            string labelSource;
            result["label"] = ReadPawnLabel(pawn, out labelSource);
            result["labelSource"] = labelSource;
            result["kindDef"] = DefName(pawn.kindDef);
            result["faction"] = pawn.Faction != null
                ? DefName(pawn.Faction.def) : null;
            result["factionLoadId"] = pawn.Faction != null
                ? pawn.Faction.loadID : -1;
            result["hostFaction"] = pawn.HostFaction != null
                ? DefName(pawn.HostFaction.def) : null;
            result["gender"] = pawn.gender.ToString();
            result["isColonist"] = pawn.IsColonist;
            result["isPrisonerOfColony"] = pawn.IsPrisonerOfColony;
            result["isSlaveOfColony"] = pawn.IsSlaveOfColony;
            result["spawned"] = pawn.Spawned;
            result["mapId"] = MapId(pawn.MapHeld);
            result["position"] = Cell(pawn.PositionHeld);
            result["rotation"] = pawn.Rotation.ToString();
            return result;
        }

        private static Dictionary<string, object> CaptureStatus(Pawn pawn)
        {
            var result = new Dictionary<string, object>();
            result["drafted"] = pawn.Drafted;
            result["downed"] = pawn.Downed;
            result["dead"] = pawn.Dead;
            result["destroyed"] = pawn.Destroyed;
            result["mentalState"] = DefName(pawn.MentalStateDef);
            result["inMentalState"] = pawn.InMentalState;
            bool awake;
            bool awakeDirty;
            bool awakeAvailable = TryReadAwake(pawn, out awake,
                out awakeDirty);
            result["awake"] = awakeAvailable ? (object)awake : null;
            result["awakeAvailable"] = awakeAvailable;
            result["awakeDirty"] = awakeDirty;
            result["fireAtWill"] = pawn.drafter != null
                && pawn.drafter.FireAtWill;
            result["lastHarmTick"] = pawn.mindState != null
                ? pawn.mindState.lastHarmTick : -1;
            result["lastMeleeThreatHarmTick"] = pawn.mindState != null
                ? pawn.mindState.lastMeleeThreatHarmTick : -1;
            result["lastEngageTargetTick"] = pawn.mindState != null
                ? pawn.mindState.lastEngageTargetTick : -1;
            result["lastAttackTargetTick"] = pawn.mindState != null
                ? pawn.mindState.lastAttackTargetTick : -1;
            result["lastJobTag"] = pawn.mindState != null
                ? pawn.mindState.lastJobTag.ToString() : null;
            result["enemyTarget"] = CaptureThing(pawn.mindState != null
                ? pawn.mindState.enemyTarget : null);
            return result;
        }

        private static List<object> CaptureTraits(Pawn pawn)
        {
            var result = new List<object>();
            List<Trait> traits = pawn?.story?.traits?.allTraits;
            if (traits == null) return result;
            for (int i = 0; i < traits.Count; i++)
            {
                Trait trait = traits[i];
                if (trait == null) continue;
                result.Add(new Dictionary<string, object>
                {
                    { "def", DefName(trait.def) },
                    { "degree", trait.Degree },
                    { "suppressed", trait.Suppressed },
                    { "scenarioForced", trait.ScenForced },
                    { "sourceGene", DefName(trait.sourceGene?.def) }
                });
            }
            return result;
        }

        private static List<object> CaptureSkills(Pawn pawn)
        {
            var result = new List<object>();
            List<SkillRecord> source = pawn?.skills?.skills;
            if (source == null) return result;
            var records = new List<SkillRecord>(source);
            records.Sort((left, right) => string.CompareOrdinal(
                DefName(left?.def), DefName(right?.def)));
            for (int i = 0; i < records.Count; i++)
            {
                SkillRecord skill = records[i];
                if (skill?.def == null) continue;
                result.Add(new object[]
                {
                    DefName(skill.def), skill.Level, skill.levelInt,
                    skill.Aptitude, skill.passion.ToString(),
                    skill.TotallyDisabled, skill.PermanentlyDisabled
                });
            }
            return result;
        }

        private static List<object> CaptureDirectRelations(Pawn pawn)
        {
            var result = new List<object>();
            List<DirectPawnRelation> relations = pawn?.relations?.DirectRelations;
            if (relations == null) return result;
            for (int i = 0; i < relations.Count; i++)
            {
                DirectPawnRelation relation = relations[i];
                if (relation == null) continue;
                Pawn other = relation.otherPawn;
                result.Add(new Dictionary<string, object>
                {
                    { "def", DefName(relation.def) },
                    { "otherPawnId", other != null
                        ? other.thingIDNumber : -1 },
                    { "otherPawnThingId", other?.ThingID },
                    { "startTick", relation.startTicks }
                });
            }
            return result;
        }

        private static List<object> CaptureAudibleCues(Pawn pawn, Map map)
        {
            var result = new List<object>();
            AudibleCueMapComponent component = AudibleCueMapComponent.For(map);
            if (component == null) return result;
            List<AudibleCueSnapshot> cues = component.CopyFreshCues(pawn);
            for (int i = 0; i < cues.Count; i++)
            {
                AudibleCueSnapshot cue = cues[i];
                result.Add(new Dictionary<string, object>
                {
                    { "eventId", cue.EventId },
                    { "kind", cue.Kind.ToString() },
                    { "approximateCell", Cell(cue.ApproximateCell) },
                    { "uncertaintyRadius", cue.UncertaintyRadius },
                    { "sourceTick", cue.SourceTick },
                    { "heardTick", cue.HeardTick },
                    { "acquiredTick", cue.AcquiredTick },
                    { "pulseCount", cue.PulseCount },
                    { "confidence", cue.Confidence },
                    { "provenance", cue.Provenance.ToString() },
                    { "deliveryChannel", cue.DeliveryChannel.ToString() },
                    { "reporterId", cue.ReporterId },
                    { "acousticClass", cue.AcousticClass.ToString() },
                    { "expectedActivity", cue.ExpectedActivity },
                    { "considered", cue.Considered }
                });
            }
            return result;
        }

        private static List<object> CaptureMapConditions(Map map)
        {
            var result = new List<object>();
            List<GameCondition> conditions = map?.GameConditionManager
                ?.ActiveConditions;
            if (conditions == null) return result;
            for (int i = 0; i < conditions.Count; i++)
            {
                GameCondition condition = conditions[i];
                if (condition == null) continue;
                result.Add(new Dictionary<string, object>
                {
                    { "def", DefName(condition.def) },
                    { "type", condition.GetType().FullName },
                    { "id", condition.uniqueID },
                    { "startTick", condition.startTick },
                    { "permanent", condition.Permanent },
                    { "durationTicks", condition.Permanent
                        ? (object)null : condition.Duration },
                    { "ticksLeft", condition.Permanent
                        ? (object)null : condition.TicksLeft },
                    { "electricityDisabled", condition.ElectricityDisabled }
                });
            }
            return result;
        }

        private static Dictionary<string, object> CaptureCommunications(
            Pawn pawn, Map map)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            var result = new Dictionary<string, object>();
            result["caCommsEnabled"] = settings != null
                && settings.commsSystem;
            result["headset"] = CommsModule.HeadsetLabel(pawn);
            result["hasMechlink"] = CommsModule.HasMechlink(pawn);
            result["hasActiveMechlink"] = CommsModule.HasActiveMechlink(pawn);
            result["networkOutage"] = CommsModule.NetworkOutage(map);
            result["nativeFreeMechBandwidthDiagnostic"] = null;
            result["nativeFreeMechBandwidthUnavailableReason"] =
                "passive recorder does not invoke native stat or mechanitor bandwidth calculations";
            result["nativeMechBandwidthGatesHumanDelivery"] = false;
            result["ordinaryVoiceRange"] = CommsModule.VoiceRange;
            result["concealedQuietVoiceRange"] =
                CommsModule.ConcealedVoiceRange;
            result["routePlanScope"] = "pair-with-actor";

            var peers = new List<object>();
            IReadOnlyList<Pawn> all = map?.mapPawns?.AllPawnsSpawned;
            if (pawn == null || all == null)
            {
                result["pairRoutes"] = peers;
                return result;
            }
            for (int i = 0; i < all.Count; i++)
            {
                Pawn peer = all[i];
                if (peer == null || peer == pawn || peer.Faction == null
                    || peer.Faction != pawn.Faction
                    || peer.RaceProps == null || !peer.RaceProps.Humanlike)
                    continue;
                peers.Add(CapturePassiveCommandRoute(pawn, peer, map));
            }
            result["pairRoutes"] = peers;
            return result;
        }

        private static Dictionary<string, object> CapturePassiveCommandRoute(
            Pawn from, Pawn to, Map map)
        {
            var route = new Dictionary<string, object>();
            string peerLabelSource;
            route["peerId"] = to.thingIDNumber;
            route["peerThingId"] = to.ThingID;
            route["peerLabel"] = ReadPawnLabel(to,
                out peerLabelSource);
            route["peerLabelSource"] = peerLabelSource;
            route["distanceCells"] = from.Position.DistanceTo(to.Position);
            bool fromActiveMechlink = CommsModule.HasActiveMechlink(from);
            bool toActiveMechlink = CommsModule.HasActiveMechlink(to);
            bool outage = CommsModule.NetworkOutage(map);
            bool voice = CommsModule.HasVoiceLine(from, to,
                CommsModule.VoiceRange);
            bool mental = CommsModule.HasMentalLine(from, to);
            bool radio = CommsModule.HasRadioLine(from, to);
            bool fromAwake;
            bool fromAwakeDirty;
            bool fromAwakeAvailable = TryReadAwake(from, out fromAwake,
                out fromAwakeDirty);
            bool toAwake;
            bool toAwakeDirty;
            bool toAwakeAvailable = TryReadAwake(to, out toAwake,
                out toAwakeDirty);
            bool endpointKnown = fromAwakeAvailable && toAwakeAvailable;
            bool endpointsReady = endpointKnown && !from.Dead && !from.Downed
                && !to.Dead && !to.Downed && fromAwake && toAwake
                && !from.InMentalState && !to.InMentalState;
            bool gesture = endpointsReady
                && from.Position.InHorDistOf(to.Position, 2.9f)
                && GenSight.LineOfSight(from.Position, to.Position, map,
                    true);

            route["observationMode"] = "passive physical-route snapshot";
            route["fromAwake"] = fromAwakeAvailable
                ? (object)fromAwake : null;
            route["fromAwakeAvailable"] = fromAwakeAvailable;
            route["fromAwakeDirty"] = fromAwakeDirty;
            route["toAwake"] = toAwakeAvailable ? (object)toAwake : null;
            route["toAwakeAvailable"] = toAwakeAvailable;
            route["toAwakeDirty"] = toAwakeDirty;
            route["fromHasActiveMechlink"] = fromActiveMechlink;
            route["toHasActiveMechlink"] = toActiveMechlink;
            route["networkOutage"] = outage;
            route["gestureAvailable"] = gesture;
            route["voiceAvailable"] = voice;
            route["radioAvailable"] = radio;
            route["mentalAvailable"] = mental;
            route["relayId"] = null;

            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.commsSystem)
            {
                route["delivered"] = true;
                route["channel"] = CommunicationChannel.None.ToString();
                route["failure"] = CommandRouteFailure.None.ToString();
                route["detail"] =
                    "delivery gate bypassed because CA comms is disabled";
                return route;
            }

            if (!endpointKnown)
            {
                route["delivered"] = null;
                route["channel"] = CommunicationChannel.None.ToString();
                route["failure"] = "EndpointReadinessUnavailable";
                route["detail"] = "one or both awake-capacity caches are unavailable or dirty; physical channel potential is reported without claiming delivery";
                return route;
            }
            if (!endpointsReady)
            {
                route["delivered"] = false;
                route["channel"] = CommunicationChannel.None.ToString();
                route["failure"] =
                    CommandRouteFailure.EndpointUnavailable.ToString();
                route["detail"] = "one or both endpoints are dead, downed, asleep, or in a mental state";
                return route;
            }

            CommunicationChannel channel = gesture
                ? CommunicationChannel.Gesture
                : mental ? CommunicationChannel.Mental
                : radio ? CommunicationChannel.Radio
                : voice ? CommunicationChannel.Voice
                : CommunicationChannel.None;
            bool delivered = channel != CommunicationChannel.None;
            route["delivered"] = delivered;
            route["channel"] = channel.ToString();
            route["failure"] = delivered
                ? CommandRouteFailure.None.ToString()
                : fromActiveMechlink && toActiveMechlink && outage
                    ? CommandRouteFailure.MentalNetworkOutage.ToString()
                    : CommandRouteFailure.NoPhysicalRoute.ToString();
            route["detail"] = delivered
                ? "passive snapshot found a usable "
                    + channel.ToString().ToLowerInvariant() + " route"
                : "passive snapshot found no usable gesture, mental, radio, or voice route";
            return route;
        }

        private static string ReadPawnLabel(Pawn pawn, out string source)
        {
            source = "unavailable";
            if (pawn == null) return null;
            Name name = pawn.Name;
            if (name != null)
            {
                source = "Pawn.Name.ToStringShort";
                return name.ToStringShort;
            }
            if (pawn.kindDef != null)
            {
                source = "PawnKindDef.label";
                return pawn.kindDef.label;
            }
            source = "ThingDef.label";
            return pawn.def?.label;
        }

        private static bool TryReadAwake(Pawn pawn, out bool awake,
            out bool dirty)
        {
            awake = false;
            dirty = false;
            if (pawn == null) return false;
            if (pawn.Dead) return true;

            bool canBeAwake;
            if (pawn.RaceProps?.alwaysAwake == true)
                canBeAwake = true;
            else
            {
                float consciousness;
                string status;
                bool capacityDirty;
                if (!TryReadCachedCapacity(pawn,
                        PawnCapacityDefOf.Consciousness,
                        out consciousness, out capacityDirty, out status))
                {
                    dirty = capacityDirty;
                    return false;
                }
                canBeAwake = consciousness >= 0.3f;
            }
            if (!canBeAwake) return true;

            CompMechanoid mech = pawn.TryGetComp<CompMechanoid>();
            if (mech != null && ReadMechDeactivated(mech, pawn)) return true;
            if (pawn.CurJob != null && pawn.jobs?.curDriver != null)
                awake = !pawn.jobs.curDriver.asleep;
            else awake = true;
            return true;
        }

        private static bool ReadMechDeactivated(CompMechanoid mech,
            Pawn pawn)
        {
            if (mech == null || pawn == null) return false;
            bool locallyDeactivated = mechDeactivatedField != null
                && Convert.ToBoolean(mechDeactivatedField.GetValue(mech),
                    CultureInfo.InvariantCulture);
            if (locallyDeactivated) return true;
            Faction mechanoids = Faction.OfMechanoids;
            if (mechanoids == null || !mechanoids.deactivated) return false;
            return pawn.Faction == mechanoids || pawn.Faction == null;
        }

        private static List<object> CaptureNeeds(Pawn pawn)
        {
            var result = new List<object>();
            if (pawn.needs == null) return result;
            List<Need> needs = pawn.needs.AllNeeds;
            for (int i = 0; i < needs.Count; i++)
            {
                Need need = needs[i];
                if (need == null) continue;
                var value = new Dictionary<string, object>();
                value["def"] = DefName(need.def);
                value["type"] = need.GetType().FullName;
                bool levelAvailable = needCurrentLevelField != null;
                float level = levelAvailable
                    ? Convert.ToSingle(needCurrentLevelField.GetValue(need),
                        CultureInfo.InvariantCulture) : 0f;
                value["level"] = levelAvailable ? (object)level : null;
                value["levelAvailable"] = levelAvailable;
                value["levelDirty"] = false;

                float maxLevel = 0f;
                bool maxDirty = false;
                string maxSource;
                bool maxAvailable;
                if (need is Need_Food)
                {
                    int cacheTick;
                    maxAvailable = TryReadFreshCachedStat(pawn,
                        StatDefOf.MaxNutrition, 15, out maxLevel,
                        out maxDirty, out cacheTick);
                    maxSource = "StatDefOf.MaxNutrition worker cache";
                }
                else if (need.GetType().FullName
                    == "RimWorld.Need_MechEnergy")
                {
                    maxLevel = pawn.RaceProps != null
                        ? pawn.RaceProps.maxMechEnergy : 0f;
                    maxAvailable = pawn.RaceProps != null;
                    maxDirty = false;
                    maxSource = "RaceProperties.maxMechEnergy";
                }
                else
                {
                    MethodInfo getter = need.GetType().GetProperty("MaxLevel",
                        BindingFlags.Instance | BindingFlags.Public)?
                            .GetGetMethod();
                    maxAvailable = getter == null
                        || getter.DeclaringType == typeof(Need);
                    maxLevel = maxAvailable ? 1f : 0f;
                    maxDirty = !maxAvailable;
                    maxSource = maxAvailable ? "Need.MaxLevel constant"
                        : "unsupported derived MaxLevel getter";
                }
                value["maxLevel"] = maxAvailable
                    ? (object)maxLevel : null;
                value["maxLevelAvailable"] = maxAvailable;
                value["maxLevelDirty"] = maxDirty;
                value["maxLevelSource"] = maxSource;
                bool percentageAvailable = levelAvailable && maxAvailable
                    && maxLevel > 0f;
                value["percentage"] = percentageAvailable
                    ? (object)(level / maxLevel) : null;
                value["percentageAvailable"] = percentageAvailable;
                value["percentageDirty"] = maxDirty;
                result.Add(value);
            }
            return result;
        }

        private static List<object> CaptureMoodMemories(Pawn pawn)
        {
            var result = new List<object>();
            MemoryThoughtHandler handler = pawn.needs?.mood?.thoughts?.memories;
            if (handler == null) return result;
            List<Thought_Memory> memories = handler.Memories;
            for (int i = 0; i < memories.Count; i++)
            {
                Thought_Memory memory = memories[i];
                if (memory == null) continue;
                var value = new Dictionary<string, object>();
                value["def"] = DefName(memory.def);
                value["type"] = memory.GetType().FullName;
                value["stage"] = memory.CurStageIndex;
                value["ageTicks"] = memory.age;
                value["durationTicks"] = memory.DurationTicks;
                value["permanent"] = memory.permanent;
                value["moodPowerFactor"] = memory.moodPowerFactor;
                value["rawMoodOffset"] = memory.moodOffset;
                value["stageBaseMoodEffect"] = memory.CurStage != null
                    ? memory.CurStage.baseMoodEffect : 0f;
                value["otherPawnId"] = memory.otherPawn != null
                    ? memory.otherPawn.thingIDNumber : -1;
                value["sourcePrecept"] = memory.sourcePrecept != null
                    ? DefName(memory.sourcePrecept.def) : null;
                result.Add(value);
            }
            return result;
        }

        private static bool TryReadFreshCachedStat(Thing thing, StatDef stat,
            int staleAfterTicks, out float value, out bool dirty,
            out int cacheTick)
        {
            value = 0f;
            dirty = true;
            cacheTick = -1;
            if (thing == null || stat == null || statDefWorkerField == null)
                return false;
            // StatDef.Worker would create workerInt. A recorder may inspect only a
            // worker the game has already materialized.
            StatWorker worker = statDefWorkerField.GetValue(stat)
                as StatWorker;
            if (worker == null) return false;

            object raw;
            if (stat.immutable && statImmutableCacheField != null
                && TryReadDictionaryValue(
                    statImmutableCacheField.GetValue(worker), thing,
                    out raw))
            {
                value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                dirty = false;
                return true;
            }
            if (staleAfterTicks < 0 || statTemporaryCacheField == null
                || !TryReadDictionaryValue(
                    statTemporaryCacheField.GetValue(worker), thing,
                    out raw) || raw == null || statCacheValueField == null
                || statCacheTickField == null) return false;
            value = Convert.ToSingle(statCacheValueField.GetValue(raw),
                CultureInfo.InvariantCulture);
            cacheTick = Convert.ToInt32(statCacheTickField.GetValue(raw),
                CultureInfo.InvariantCulture);
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            int age = now >= 0 ? now - cacheTick : int.MaxValue;
            dirty = age < 0 || age >= staleAfterTicks;
            return !dirty;
        }

        private static bool TryReadDictionaryValue(object dictionary,
            object key, out object value)
        {
            value = null;
            if (dictionary == null || key == null) return false;
            IDictionary nonGeneric = dictionary as IDictionary;
            if (nonGeneric != null)
            {
                if (!nonGeneric.Contains(key)) return false;
                value = nonGeneric[key];
                return true;
            }
            IEnumerable entries = dictionary as IEnumerable;
            if (entries == null) return false;
            foreach (object entry in entries)
            {
                if (entry == null) continue;
                Type type = entry.GetType();
                PropertyInfo keyProperty = type.GetProperty("Key");
                PropertyInfo valueProperty = type.GetProperty("Value");
                if (keyProperty == null || valueProperty == null) continue;
                object candidate = keyProperty.GetValue(entry, null);
                if (!ReferenceEquals(candidate, key)
                    && !Equals(candidate, key)) continue;
                value = valueProperty.GetValue(entry, null);
                return true;
            }
            return false;
        }

        private static Dictionary<string, object> CaptureHealth(Pawn pawn)
        {
            var result = new Dictionary<string, object>();
            var states = new Dictionary<string, object>();
            HediffSet set = pawn?.health?.hediffSet;

            float pain = 0f;
            bool painAvailable = TryReadNonnegativeFloat(set,
                hediffSetCachedPainField, out pain);
            pain = Mathf.Clamp01(pain);
            PutObservedAxis(result, states, "pain", painAvailable,
                !painAvailable, pain, "HediffSet.cachedPain");

            float consciousness;
            bool consciousnessDirty;
            string consciousnessStatus;
            bool consciousnessAvailable = TryReadCachedCapacity(pawn,
                PawnCapacityDefOf.Consciousness, out consciousness,
                out consciousnessDirty, out consciousnessStatus);
            PutObservedAxis(result, states, "consciousness",
                consciousnessAvailable, consciousnessDirty,
                Mathf.Clamp01(consciousness), consciousnessStatus);

            float moving;
            bool movingDirty;
            string movingStatus;
            bool movingAvailable = TryReadCachedCapacity(pawn,
                PawnCapacityDefOf.Moving, out moving, out movingDirty,
                out movingStatus);
            PutObservedAxis(result, states, "moving", movingAvailable,
                movingDirty, Mathf.Clamp01(moving), movingStatus);

            float manipulation;
            bool manipulationDirty;
            string manipulationStatus;
            bool manipulationAvailable = TryReadCachedCapacity(pawn,
                PawnCapacityDefOf.Manipulation, out manipulation,
                out manipulationDirty, out manipulationStatus);
            PutObservedAxis(result, states, "manipulation",
                manipulationAvailable, manipulationDirty,
                Mathf.Clamp01(manipulation), manipulationStatus);

            float breathing;
            bool breathingDirty;
            string breathingStatus;
            bool breathingAvailable = TryReadCachedCapacity(pawn,
                PawnCapacityDefOf.Breathing, out breathing,
                out breathingDirty, out breathingStatus);
            PutObservedAxis(result, states, "breathing",
                breathingAvailable, breathingDirty,
                Mathf.Clamp01(breathing), breathingStatus);

            float bloodPumping;
            bool bloodPumpingDirty;
            string bloodPumpingStatus;
            bool bloodPumpingAvailable = TryReadCachedCapacity(pawn,
                PawnCapacityDefOf.BloodPumping, out bloodPumping,
                out bloodPumpingDirty, out bloodPumpingStatus);
            PutObservedAxis(result, states, "bloodPumping",
                bloodPumpingAvailable, bloodPumpingDirty,
                Mathf.Clamp01(bloodPumping), bloodPumpingStatus);

            float rawBleed = 0f;
            bool bleedAvailable = TryReadNonnegativeFloat(set,
                hediffSetCachedBleedRateField, out rawBleed);
            float bleed = Mathf.Clamp01(rawBleed);
            PutObservedAxis(result, states, "bleedRate", bleedAvailable,
                !bleedAvailable, bleed, "HediffSet.cachedBleedRate");

            float bloodLoss = 0f;
            bool vital = false;
            List<Hediff> live = set?.hediffs;
            if (live != null)
            {
                for (int i = 0; i < live.Count; i++)
                {
                    Hediff hediff = live[i];
                    if (hediff == null) continue;
                    float severity = ReadHediffSeverity(hediff);
                    if (hediff.def == HediffDefOf.BloodLoss
                        || hediff.def?.defName == "BloodLoss")
                        bloodLoss = Mathf.Clamp01(severity);
                    BodyPartRecord part = ReadHediffPart(hediff);
                    if (!(hediff is Hediff_Injury) || part?.def?.tags == null)
                        continue;
                    List<BodyPartTagDef> tags = part.def.tags;
                    if (tags.Contains(BodyPartTagDefOf.ConsciousnessSource)
                        || tags.Contains(BodyPartTagDefOf.BreathingSource)
                        || tags.Contains(BodyPartTagDefOf.BloodPumpingSource)
                        || tags.Contains(
                            BodyPartTagDefOf.BloodFiltrationSource)
                        || tags.Contains(
                            BodyPartTagDefOf.BloodFiltrationLiver)
                        || tags.Contains(
                            BodyPartTagDefOf.BloodFiltrationKidney)
                        || tags.Contains(BodyPartTagDefOf.MetabolismSource))
                        vital = true;
                }
            }
            PutObservedAxis(result, states, "bloodLoss", live != null,
                false, bloodLoss, "raw BloodLoss hediff severity");

            float health = 0f;
            bool healthDirty;
            bool healthAvailable = TryReadCachedSummaryHealth(pawn,
                out health, out healthDirty);
            health = Mathf.Clamp01(health);
            PutObservedAxis(result, states, "health", healthAvailable,
                healthDirty, health,
                pawn?.Dead == true ? "dead-state"
                    : "SummaryHealthHandler.cachedSummaryHealthPercent");

            bool deathAvailable = bleedAvailable && live != null;
            int deathInTicks = int.MaxValue;
            if (deathAvailable && rawBleed >= 0.0001f)
            {
                double ticks = (1d - bloodLoss) / rawBleed * 60000d;
                deathInTicks = ticks >= int.MaxValue ? int.MaxValue
                    : ticks <= 0d ? 0 : (int)ticks;
            }
            PutObservedAxis(result, states, "deathInTicks",
                deathAvailable, !deathAvailable, deathInTicks,
                "detached cached-bleed formula");
            PutObservedAxis(result, states, "vitalInjury", live != null,
                false, vital, "raw injury body-part tags");

            bool combatPowerAvailable = painAvailable
                && consciousnessAvailable && movingAvailable
                && manipulationAvailable && breathingAvailable
                && bloodPumpingAvailable && bleedAvailable
                && healthAvailable && live != null;
            float combatPower = 0f;
            if (combatPowerAvailable)
            {
                float capacity = consciousness * 0.22f + moving * 0.20f
                    + manipulation * 0.20f + breathing * 0.12f
                    + bloodPumping * 0.10f + health * 0.16f;
                combatPower = Mathf.Clamp01(capacity
                    * (1f - pain * 0.28f)
                    * (1f - bloodLoss * 0.35f)
                    * (1f - Mathf.Clamp01(bleed / 0.5f) * 0.18f));
            }
            PutObservedAxis(result, states, "combatPower",
                combatPowerAvailable, !combatPowerAvailable, combatPower,
                "detached materialized-axis formula");
            result["cacheState"] = states;

            var hediffs = new List<object>();
            if (live != null)
            {
                for (int i = 0; i < live.Count; i++)
                {
                    Hediff hediff = live[i];
                    if (hediff == null) continue;
                    var value = new Dictionary<string, object>();
                    value["def"] = DefName(hediff.def);
                    value["type"] = hediff.GetType().FullName;
                    value["loadId"] = hediff.loadID;
                    value["severity"] = ReadHediffSeverity(hediff);
                    value["stage"] = hediff.CurStageIndex;
                    value["ageTicks"] = hediff.ageTicks;
                    value["tickAdded"] = hediff.tickAdded;
                    BodyPartRecord part = ReadHediffPart(hediff);
                    value["part"] = part != null ? part.Label : null;
                    value["bleeding"] = hediff.Bleeding;
                    value["bleedRate"] = hediff.BleedRate;
                    value["tendableNow"] = hediff.TendableNow();
                    value["sourceDef"] = DefName(hediff.sourceDef);
                    value["sourceHediffDef"] = DefName(
                        hediff.sourceHediffDef);
                    value["sourceLabel"] = hediff.sourceLabel;
                    hediffs.Add(value);
                }
            }
            result["hediffs"] = hediffs;
            return result;
        }

        private static void PutObservedAxis(
            Dictionary<string, object> result,
            Dictionary<string, object> states, string name, bool available,
            bool dirty, object value, string source)
        {
            result[name] = available ? value : null;
            states[name] = new Dictionary<string, object>
            {
                { "available", available },
                { "dirty", dirty },
                { "source", source }
            };
        }

        private static bool TryReadNonnegativeFloat(object instance,
            FieldInfo field, out float value)
        {
            value = 0f;
            if (instance == null || field == null) return false;
            value = Convert.ToSingle(field.GetValue(instance),
                CultureInfo.InvariantCulture);
            return value >= 0f;
        }

        private static bool TryReadCachedCapacity(Pawn pawn,
            PawnCapacityDef capacity, out float value, out bool dirty,
            out string status)
        {
            value = 0f;
            dirty = true;
            status = "capacity-cache-unavailable";
            if (pawn?.health == null || capacity == null) return false;
            if (pawn.Dead)
            {
                dirty = false;
                status = "dead-state";
                return true;
            }
            PawnCapacitiesHandler capacities = pawn.health.capacities;
            if (capacities == null || capacityLevelsField == null
                || capacityDefMapValuesField == null
                || capacityCacheStatusField == null
                || capacityCacheValueField == null) return false;
            object levels = capacityLevelsField.GetValue(capacities);
            if (levels == null)
            {
                status = "capacity-cache-uncached";
                return false;
            }
            IList values = capacityDefMapValuesField.GetValue(levels)
                as IList;
            int index = capacity.index;
            if (values == null || index < 0 || index >= values.Count)
            {
                status = "capacity-cache-index-unavailable";
                return false;
            }
            object element = values[index];
            object rawStatus = element != null
                ? capacityCacheStatusField.GetValue(element) : null;
            string cacheStatus = rawStatus?.ToString() ?? "Uncached";
            status = "PawnCapacitiesHandler." + cacheStatus;
            if (cacheStatus != "Cached") return false;
            value = Convert.ToSingle(capacityCacheValueField.GetValue(
                element), CultureInfo.InvariantCulture);
            dirty = false;
            return true;
        }

        private static bool TryReadCachedSummaryHealth(Pawn pawn,
            out float value, out bool dirty)
        {
            value = 0f;
            dirty = true;
            if (pawn?.health == null) return false;
            if (pawn.Dead)
            {
                dirty = false;
                return true;
            }
            SummaryHealthHandler summary = pawn.health.summaryHealth;
            if (summary == null || summaryHealthDirtyField == null
                || summaryHealthCachedPercentField == null) return false;
            dirty = Convert.ToBoolean(summaryHealthDirtyField.GetValue(
                summary), CultureInfo.InvariantCulture);
            if (dirty) return false;
            value = Convert.ToSingle(summaryHealthCachedPercentField.GetValue(
                summary), CultureInfo.InvariantCulture);
            return true;
        }

        private static float ReadHediffSeverity(Hediff hediff)
        {
            return hediff != null && hediffSeverityField != null
                ? Convert.ToSingle(hediffSeverityField.GetValue(hediff),
                    CultureInfo.InvariantCulture) : 0f;
        }

        private static BodyPartRecord ReadHediffPart(Hediff hediff)
        {
            return hediff != null && hediffPartField != null
                ? hediffPartField.GetValue(hediff) as BodyPartRecord : null;
        }

        private static List<object> CaptureEquipment(Pawn pawn)
        {
            var result = new List<object>();
            List<ThingWithComps> equipment = pawn.equipment?
                .AllEquipmentListForReading;
            if (equipment == null) return result;
            for (int i = 0; i < equipment.Count; i++)
                result.Add(CaptureThing(equipment[i]));
            return result;
        }

        private static List<object> CaptureApparel(Pawn pawn)
        {
            var result = new List<object>();
            List<Apparel> apparel = pawn.apparel?.WornApparel;
            if (apparel == null) return result;
            for (int i = 0; i < apparel.Count; i++)
                result.Add(CaptureThing(apparel[i]));
            return result;
        }

        private static List<object> CaptureInventory(Pawn pawn)
        {
            var result = new List<object>();
            if (pawn.inventory?.innerContainer == null) return result;
            foreach (Thing thing in pawn.inventory.innerContainer)
                result.Add(CaptureThing(thing));
            return result;
        }

        // The public RememberedContacts surface refreshes observed state as it reads.
        // A recorder must not do that, so this mirrors its stored facts through
        // reflection and copies their public fields without invoking refresh logic.
        private static List<object> CaptureRememberedContacts(Pawn pawn,
            Map map, int now)
        {
            var result = new List<object>();
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
            if (knowledge == null || knownContactsField == null) return result;
            IDictionary known = knownContactsField.GetValue(knowledge)
                as IDictionary;
            if (known == null || !known.Contains(pawn.thingIDNumber))
                return result;
            IEnumerable contacts = known[pawn.thingIDNumber] as IEnumerable;
            if (contacts == null) return result;
            foreach (object contact in contacts)
            {
                if (contact == null) continue;
                Type type = contact.GetType();
                int hostileId = Field<int>(type, contact, "hostileId", -1);
                IntVec3 cell = Field<IntVec3>(type, contact, "lastKnown",
                    IntVec3.Invalid);
                int sourceTick = Field<int>(type, contact, "tick", -1);
                int acquiredTick = Field<int>(type, contact,
                    "acquiredTick", -1);
                ContactWeaponCategory weapon = Field(type, contact,
                    "weaponCategory", ContactWeaponCategory.Unknown);
                ContactEvidence evidence = Field(type, contact, "evidence",
                    default(ContactEvidence));
                ThreatContactState state = Field(type, contact, "state",
                    ThreatContactState.Active);
                int stateTick = Field<int>(type, contact, "stateTick", -1);
                int age = sourceTick >= 0 && now >= sourceTick
                    ? now - sourceTick : -1;
                float freshness = age < 0 ? 0f
                    : Mathf.Clamp01(1f - age
                        / (float)RememberedContactTicks);
                var value = new Dictionary<string, object>();
                value["hostileId"] = hostileId;
                value["cell"] = Cell(cell);
                value["sourceTick"] = sourceTick;
                value["acquiredTick"] = acquiredTick;
                value["ageTicks"] = age;
                value["withinRememberedWindow"] = sourceTick >= 0
                    && now >= sourceTick
                    && now - sourceTick <= RememberedContactTicks;
                value["weaponCategory"] = weapon.ToString();
                value["state"] = state.ToString();
                value["withinStoredActionWindow"] =
                    state == ThreatContactState.Active
                    && age >= 0 && age <= RememberedContactTicks;
                value["liveActionabilityEvaluated"] = false;
                value["stateTick"] = stateTick;
                value["provenance"] = ContactEvidenceValue(evidence);
                value["ageAdjustedConfidence"] =
                    evidence.Confidence * freshness;
                result.Add(value);
            }
            return result;
        }

        private static Dictionary<string, object> ContactEvidenceValue(
            ContactEvidence evidence)
        {
            var result = new Dictionary<string, object>();
            result["source"] = evidence.Source.ToString();
            result["sourceId"] = evidence.SourceId;
            result["confidence"] = evidence.Confidence;
            result["uncertainty"] = evidence.Uncertainty;
            result["deliveryChannel"] = evidence.DeliveryChannel.ToString();
            result["reporterId"] = evidence.ReporterId;
            result["direct"] = evidence.IsDirect;
            return result;
        }

        private static List<object> CaptureRememberedWelfare(Pawn pawn,
            Map map, int now)
        {
            var result = new List<object>();
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
            if (knowledge == null || pawn == null) return result;
            List<WelfareFactSnapshot> facts = knowledge.RememberedWelfare(pawn);
            for (int i = 0; i < facts.Count; i++)
            {
                WelfareFactSnapshot fact = facts[i];
                int age = fact.SourceTick >= 0 && now >= fact.SourceTick
                    ? now - fact.SourceTick : -1;
                int deathRemaining = fact.EstimatedDeathTick == int.MaxValue
                    || now < 0 ? int.MaxValue
                    : Math.Max(0, fact.EstimatedDeathTick - now);
                var value = new Dictionary<string, object>();
                value["subjectId"] = fact.SubjectId;
                value["cell"] = Cell(fact.Cell);
                value["sourceTick"] = fact.SourceTick;
                value["acquiredTick"] = fact.AcquiredTick;
                value["ageTicks"] = age;
                value["revision"] = fact.Revision;
                value["state"] = fact.State.ToString();
                value["stateTick"] = fact.StateTick;
                value["assessedTick"] = fact.AssessedTick;
                value["estimatedDeathTick"] = fact.EstimatedDeathTick;
                value["estimatedDeathTicksRemaining"] = deathRemaining;
                value["diagnosticTier"] = fact.DiagnosticTier;
                value["observedNeedsTend"] = fact.ObservedNeedsTend;
                value["observedDowned"] = fact.ObservedDowned;
                value["observedTemperatureDanger"] =
                    fact.ObservedTemperatureDanger;
                value["actionable"] = fact.Actionable;
                var provenance = new Dictionary<string, object>();
                provenance["source"] = fact.Evidence.Source.ToString();
                provenance["sourceId"] = fact.Evidence.SourceId;
                provenance["confidence"] = fact.Evidence.Confidence;
                provenance["uncertaintyTicks"] =
                    fact.Evidence.UncertaintyTicks;
                provenance["deliveryChannel"] =
                    fact.Evidence.DeliveryChannel.ToString();
                provenance["reporterId"] = fact.Evidence.ReporterId;
                provenance["direct"] = fact.Evidence.IsDirect;
                value["provenance"] = provenance;
                result.Add(value);
            }
            return result;
        }

        private static Dictionary<string, object> CaptureJobs(Pawn pawn)
        {
            var result = new Dictionary<string, object>();
            result["current"] = CaptureJob(pawn.CurJob, pawn);
            var queue = new List<object>();
            if (pawn.jobs?.jobQueue != null)
            {
                foreach (QueuedJob queued in pawn.jobs.jobQueue)
                {
                    var value = new Dictionary<string, object>();
                    value["tag"] = queued?.tag.HasValue == true
                        ? queued.tag.Value.ToString() : null;
                    value["job"] = CaptureJob(queued?.job, pawn);
                    queue.Add(value);
                }
            }
            result["queue"] = queue;
            return result;
        }

        private static Dictionary<string, object> CaptureJob(Job job,
            Pawn pawn)
        {
            if (job == null) return null;
            var result = new Dictionary<string, object>();
            result["def"] = DefName(job.def);
            result["loadId"] = job.loadID;
            result["startTick"] = job.startTick;
            result["playerForced"] = job.playerForced;
            result["playerInterruptedForced"] =
                job.playerInterruptedForced;
            result["preventFriendlyFire"] = job.preventFriendlyFire;
            result["jobGiverType"] = job.jobGiver != null
                ? job.jobGiver.GetType().FullName : null;
            result["thinkTree"] = DefName(job.jobGiverThinkTree);
            result["workGiver"] = DefName(job.workGiverDef);
            result["driverType"] = pawn?.jobs?.curJob == job
                && pawn.jobs.curDriver != null
                    ? pawn.jobs.curDriver.GetType().FullName : null;
            result["dutyTag"] = job.dutyTag;
            result["lord"] = CaptureLord(job.lord);
            result["targetA"] = CaptureTarget(job.targetA);
            result["targetB"] = CaptureTarget(job.targetB);
            result["targetC"] = CaptureTarget(job.targetC);
            result["count"] = job.count;
            result["countQueue"] = job.countQueue != null
                ? (object)new List<int>(job.countQueue) : null;
            var targetQueueA = new List<object>();
            if (job.targetQueueA != null)
                for (int i = 0; i < job.targetQueueA.Count; i++)
                    targetQueueA.Add(CaptureTarget(job.targetQueueA[i]));
            result["targetQueueA"] = targetQueueA;
            result["routeContract"] = job.def == CA_Defs.CombatMove
                ? "assessed locked adjacent cells; list is remaining route"
                : null;
            CAIntentContext investigationIntent;
            result["investigationIntent"] = AudibleCueResponse
                .TryReadInvestigationIntent(job, out investigationIntent)
                    ? CaptureIntent(investigationIntent) : null;
            result["expiryInterval"] = job.expiryInterval;
            result["locomotionUrgency"] = job.locomotionUrgency.ToString();
            result["haulMode"] = job.haulMode.ToString();
            result["globalTarget"] = job.globalTarget.IsValid
                ? job.globalTarget.ToString() : null;
            return result;
        }

        private static Dictionary<string, object> CapturePath(Pawn pawn)
        {
            var result = new Dictionary<string, object>();
            Pawn_PathFollower pather = pawn.pather;
            if (pather == null)
            {
                result["available"] = false;
                return result;
            }
            result["available"] = true;
            result["moving"] = pather.Moving;
            result["movingNow"] = pather.MovingNow;
            result["destination"] = CaptureTarget(pather.Destination);
            result["nextCell"] = Cell(pather.nextCell);
            result["lastPathedTargetPosition"] = Cell(
                pather.lastPathedTargetPosition);
            result["lastMovedTick"] = pather.LastMovedTick;
            result["curPathJobIsStale"] = pather.curPathJobIsStale;
            result["request"] = pather.curPathRequest != null
                ? pather.curPathRequest.ToString() : null;
            var nodes = new List<object>();
            PawnPath path = pather.curPath;
            if (path != null && path.Found)
            {
                int count = path.NodesLeftCount;
                result["nodesLeft"] = count;
                result["nodesConsumed"] = path.NodesConsumedCount;
                result["totalCost"] = path.TotalCost;
                for (int i = 0; i < count; i++)
                    nodes.Add(Cell(path.Peek(i)));
            }
            else
            {
                result["nodesLeft"] = 0;
                result["nodesConsumed"] = 0;
                result["totalCost"] = null;
            }
            result["nodesCurrentToDestination"] = nodes;
            return result;
        }

        private static Dictionary<string, object> CaptureCombatAction(Pawn pawn)
        {
            var result = new Dictionary<string, object>();
            Pawn_StanceTracker tracker = pawn.stances;
            Stance stance = tracker?.curStance;
            result["stanceType"] = stance != null
                ? stance.GetType().FullName : null;
            result["stanceBusy"] = stance != null && stance.StanceBusy;
            result["fullBodyBusy"] = tracker != null
                && tracker.FullBodyBusy;
            result["lastTickFullBodyBusy"] = tracker != null
                ? tracker.lastTickFullBodyBusy : -1;
            result["stunned"] = tracker?.stunner?.Stunned == true;
            result["staggered"] = tracker?.stagger?.Staggered == true;
            result["aimTarget"] = CaptureTarget(
                pawn.TargetCurrentlyAimingAt);

            Stance_Busy busy = stance as Stance_Busy;
            result["stanceTicksLeft"] = busy != null
                ? busy.ticksLeft : 0;
            result["stanceStartedTick"] = busy != null
                ? busy.startedTick : -1;
            result["stanceFocusTarget"] = busy != null
                ? CaptureTarget(busy.focusTarg) : null;
            result["stanceNeverAimWeapon"] = busy != null
                && busy.neverAimWeapon;
            result["stanceVerb"] = CaptureVerb(busy?.verb);
            result["jobVerb"] = CaptureVerb(pawn.CurJob?.verbToUse);

            Pawn_StanceTracker offhandTracker;
            bool offhandTrackerMaterialized = OffhandComponent
                .TryGetExistingTracker(pawn, out offhandTracker);
            result["offhandTrackerMaterialized"] =
                offhandTrackerMaterialized;
            result["offhandCombatAction"] = offhandTrackerMaterialized
                ? CaptureStanceAction(offhandTracker) : null;

            // CurrentEffectiveVerb may roll and cache a melee verb. A recorder must
            // never invoke that gameplay getter. Only copy verb objects the pawn's
            // current stance, job, or equipped primary already owns.
            Verb owned = busy?.verb ?? pawn.CurJob?.verbToUse;
            if (owned == null)
                owned = TryReadMaterializedPrimaryVerb(
                    pawn.equipment?.Primary);
            result["ownedVerb"] = CaptureVerb(owned);
            return result;
        }

        private static Dictionary<string, object> CaptureStanceAction(
            Pawn_StanceTracker tracker)
        {
            if (tracker == null) return null;
            Stance stance = tracker.curStance;
            Stance_Busy busy = stance as Stance_Busy;
            var result = new Dictionary<string, object>();
            result["stanceType"] = stance != null
                ? stance.GetType().FullName : null;
            result["stanceBusy"] = stance != null && stance.StanceBusy;
            result["fullBodyBusy"] = tracker.FullBodyBusy;
            result["lastTickFullBodyBusy"] = tracker.lastTickFullBodyBusy;
            result["stunned"] = tracker.stunner?.Stunned == true;
            result["staggered"] = tracker.stagger?.Staggered == true;
            result["stanceTicksLeft"] = busy != null ? busy.ticksLeft : 0;
            result["stanceStartedTick"] = busy != null
                ? busy.startedTick : -1;
            result["stanceFocusTarget"] = busy != null
                ? CaptureTarget(busy.focusTarg) : null;
            result["stanceNeverAimWeapon"] = busy != null
                && busy.neverAimWeapon;
            result["stanceVerb"] = CaptureVerb(busy?.verb);
            return result;
        }

        private static Verb TryReadMaterializedPrimaryVerb(
            ThingWithComps equipment)
        {
            CompEquippable equippable = equipment?
                .TryGetComp<CompEquippable>();
            VerbTracker tracker = equippable?.verbTracker;
            List<Verb> verbs = tracker != null && verbTrackerVerbsField != null
                ? verbTrackerVerbsField.GetValue(tracker) as List<Verb>
                : null;
            if (verbs == null) return null;
            for (int i = 0; i < verbs.Count; i++)
                if (verbs[i]?.verbProps?.isPrimary == true) return verbs[i];
            return null;
        }

        private static Dictionary<string, object> CaptureVerb(Verb verb)
        {
            if (verb == null) return null;
            var result = new Dictionary<string, object>();
            result["type"] = verb.GetType().FullName;
            result["loadId"] = verb.loadID;
            result["state"] = verb.state.ToString();
            result["currentTarget"] = CaptureTarget(verb.CurrentTarget);
            result["currentDestination"] = CaptureTarget(
                verb.CurrentDestination);
            result["caster"] = CaptureThing(verb.Caster);
            result["equipment"] = CaptureThing(verb.EquipmentSource);
            result["lastShotTick"] = verb.LastShotTick;
            result["warmingUp"] = verb.WarmingUp;
            result["melee"] = verb.IsMeleeAttack;
            result["ranged"] = verb.verbProps != null
                && verb.verbProps.Ranged;
            result["range"] = verb.verbProps != null
                ? verb.verbProps.range : 0f;
            float effectiveRange;
            bool effectiveRangeDirty;
            string effectiveRangeSource;
            bool effectiveRangeAvailable = TryReadEffectiveRange(verb,
                out effectiveRange, out effectiveRangeDirty,
                out effectiveRangeSource);
            result["effectiveRange"] = effectiveRangeAvailable
                ? (object)effectiveRange : null;
            result["effectiveRangeAvailable"] = effectiveRangeAvailable;
            result["effectiveRangeDirty"] = effectiveRangeDirty;
            result["effectiveRangeSource"] = effectiveRangeSource;
            result["minRange"] = verb.verbProps != null
                ? verb.verbProps.minRange : 0f;
            result["burstShotCount"] = verb.verbProps != null
                ? verb.verbProps.burstShotCount : 0;
            result["preventFriendlyFire"] = verb.preventFriendlyFire;
            return result;
        }

        private static bool TryReadEffectiveRange(Verb verb,
            out float value, out bool dirty, out string source)
        {
            value = 0f;
            dirty = false;
            source = "unavailable";
            VerbProperties props = verb?.verbProps;
            Thing caster = verb?.Caster;
            if (props == null || caster == null) return false;
            if (props.rangeStat == null)
            {
                value = props.range;
                source = "VerbProperties.range";
            }
            else
            {
                int cacheTick;
                if (!TryReadFreshCachedStat(caster, props.rangeStat, -1,
                        out value, out dirty, out cacheTick))
                {
                    dirty = true;
                    source = "stat-driven range uncached";
                    return false;
                }
                source = "immutable range-stat cache";
            }

            CompUniqueWeapon unique = verb.EquipmentSource?
                .TryGetComp<CompUniqueWeapon>();
            if (unique?.IgnoreAccuracyMaluses != true)
            {
                Map map = caster.MapHeld;
                if (map?.weatherManager != null)
                {
                    float cap = map.weatherManager.CurWeatherMaxRangeCap;
                    if (cap >= 0f) value = Mathf.Min(value, cap);
                }
            }
            return true;
        }

        private static List<object> CaptureNearbyCombatGeometry(Pawn actor,
            Map map)
        {
            var result = new List<object>();
            IntVec3 origin = actor.PositionHeld;
            if (!actor.Spawned || actor.Map != map || !origin.InBounds(map))
                return result;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            Verb verb = null;
            Stance_Busy busy = actor.stances?.curStance as Stance_Busy;
            if (busy != null) verb = busy.verb;
            if (verb == null) verb = actor.CurJob?.verbToUse;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == null || other == actor) continue;
                float distance = origin.DistanceTo(other.Position);
                if (distance > NearbyCombatRadius) continue;
                bool hostile = actor.HostileTo(other);
                bool reverseHostile = other.HostileTo(actor);
                Faction actorFaction = actor.Faction;
                Faction otherFaction = other.Faction;
                bool sameFaction = actorFaction != null
                    && actorFaction == otherFaction;
                var value = new Dictionary<string, object>();
                value["pawn"] = CapturePawnIdentity(other);
                value["distance"] = distance;
                value["sameFaction"] = sameFaction;
                value["actorHostileToPawn"] = hostile;
                value["pawnHostileToActor"] = reverseHostile;
                // RelationWith(self) is an invalid engine query: it logs an error
                // and returns a dummy Neutral relation. Pawn-level HostileTo above
                // remains authoritative for individual same-faction hostility.
                value["factionRelation"] = actorFaction != null
                    && otherFaction != null && !sameFaction
                        ? actorFaction.RelationKindWith(otherFaction)
                            .ToString() : null;
                value["lineOfSight"] = GenSight.LineOfSight(origin,
                    other.Position, map, skipFirstCell: true);
                value["actorCoverFromPawn"] =
                    CoverUtility.CalculateOverallBlockChance(actor,
                        other.Position, map);
                value["pawnCoverFromActor"] =
                    CoverUtility.CalculateOverallBlockChance(other,
                        origin, map);
                float effectiveRange;
                bool effectiveRangeDirty;
                string effectiveRangeSource;
                bool effectiveRangeAvailable = TryReadEffectiveRange(verb,
                    out effectiveRange, out effectiveRangeDirty,
                    out effectiveRangeSource);
                value["insideActiveVerbRange"] = effectiveRangeAvailable
                    ? (object)(distance <= effectiveRange) : null;
                value["insideActiveVerbRangeAvailable"] =
                    effectiveRangeAvailable;
                value["downed"] = other.Downed;
                value["dead"] = other.Dead;
                value["drafted"] = other.Drafted;
                result.Add(value);
            }
            return result;
        }

        private static List<object> CaptureActiveFriendlyFireSectors(
            Pawn actor)
        {
            var result = new List<object>();
            if (actor == null || actor.Map == null || !actor.Spawned)
                return result;
            IReadOnlyList<Pawn> pawns = actor.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn shooter = pawns[i];
                if (shooter == null || shooter == actor || !shooter.Spawned
                    || shooter.Dead || shooter.Downed
                    || actor.HostileTo(shooter) || shooter.HostileTo(actor)
                    || actor.PositionHeld.DistanceTo(shooter.PositionHeld)
                        > NearbyCombatRadius) continue;

                Stance_Busy busy = shooter.stances?.curStance as Stance_Busy;
                if (busy != null
                    && (busy is Stance_Warmup
                        || busy.verb != null && busy.verb.Bursting)
                    && busy.focusTarg.IsValid
                    && busy.focusTarg.HasThing)
                {
                    AppendPassiveFriendlySector(actor, shooter,
                        busy.focusTarg, busy.verb, "mainStance", result);
                }

                Pawn_StanceTracker offhandTracker;
                Stance_Busy offhand = OffhandComponent
                    .TryGetExistingTracker(shooter, out offhandTracker)
                        ? offhandTracker.curStance as Stance_Busy : null;
                if (offhand != null
                    && (offhand is Stance_Warmup
                        || offhand.verb != null && offhand.verb.Bursting)
                    && offhand.focusTarg.IsValid
                    && offhand.focusTarg.HasThing)
                    AppendPassiveFriendlySector(actor, shooter,
                        offhand.focusTarg, offhand.verb, "offhandStance",
                        result);

                // A materialized busy stance is the exact current commitment,
                // even when it is melee or otherwise lacks ranged geometry.
                // Only a mobile/non-busy tracker may fall back to the job or the
                // drafted native target.
                if (busy == null)
                {
                    Job current = shooter.CurJob;
                    if (current != null && current.targetA.IsValid
                        && current.targetA.HasThing
                        && (current.def == JobDefOf.AttackStatic
                            || current.def == CA_Defs.BoundedRangedDefense))
                    {
                        AppendPassiveFriendlySector(actor, shooter,
                            current.targetA, current.verbToUse, "job", result);
                    }
                    else if (shooter.Drafted && shooter.drafter != null
                        && shooter.drafter.FireAtWill
                        && shooter.mindState != null
                        && shooter.mindState.enemyTarget != null)
                    {
                        AppendPassiveFriendlySector(actor, shooter,
                            new LocalTargetInfo(shooter.mindState.enemyTarget),
                            null, "draftedEnemyTarget", result);
                    }
                }
            }
            return result;
        }

        private static void AppendPassiveFriendlySector(Pawn actor,
            Pawn shooter, LocalTargetInfo focus, Verb verb,
            string commitmentSource, List<object> result)
        {
            if (actor == null || shooter == null || result == null
                || !focus.IsValid || !focus.HasThing) return;
            Thing target = focus.Thing;
            if (target == null || target.Destroyed || !target.Spawned
                || target.Map != actor.Map) return;

            var value = new Dictionary<string, object>();
            value["shooterId"] = shooter.thingIDNumber;
            value["targetId"] = target.thingIDNumber;
            value["commitmentSource"] = commitmentSource;
            value["weaponId"] = verb?.EquipmentSource != null
                ? verb.EquipmentSource.thingIDNumber : -1;
            bool actorCanSeeShooter = GenSight.LineOfSight(
                actor.PositionHeld, shooter.PositionHeld, actor.Map,
                skipFirstCell: true);
            value["actorCanSeeShooter"] = actorCanSeeShooter;
            bool ranged = verb != null && verb.verbProps != null
                && !verb.verbProps.IsMeleeAttack;
            bool overhead = ranged && verb.ProjectileFliesOverhead();
            bool targetHostile = shooter.HostileTo(target);
            Pawn targetPawn = target as Pawn;
            bool targetCombatCapable = targetPawn == null
                || !targetPawn.Dead && !targetPawn.Downed;
            value["projectileFliesOverhead"] = overhead;
            value["targetHostileToShooter"] = targetHostile;
            value["targetCombatCapable"] = targetCombatCapable;
            bool geometryAvailable = actorCanSeeShooter && ranged
                && !overhead && targetHostile && targetCombatCapable;
            value["geometryAvailable"] = geometryAvailable;
            if (!geometryAvailable)
            {
                value["reason"] = !actorCanSeeShooter
                    ? "actor cannot currently see shooter"
                    : verb == null
                        ? "target commitment present but verb is not materialized"
                        : !ranged
                            ? "materialized verb is not ranged"
                            : overhead
                                ? "overhead projectile has no direct friendly-fire corridor"
                                : !targetHostile
                                    ? "committed target is not hostile to shooter"
                                    : "committed pawn target is dead or downed";
                result.Add(value);
                return;
            }

            IntVec3 source = shooter.PositionHeld;
            IntVec3 destination = target.PositionHeld;
            float forcedMiss = VerbUtility.CalculateAdjustedForcedMiss(
                verb.verbProps.ForcedMissRadius, destination - source);
            CAFriendlyFireSectorSnapshot sector =
                new CAFriendlyFireSectorSnapshot(shooter.thingIDNumber,
                    target.thingIDNumber, source, destination,
                    Mathf.Max(2.5f, forcedMiss));
            value["source"] = Cell(source);
            value["destination"] = Cell(destination);
            value["corridor"] = sector.Corridor;
            value["actorRisk"] = sector.RiskAt(actor.PositionHeld);
            value["geometryBasis"] =
                "raw committed endpoints; obstruction not evaluated";
            result.Add(value);
        }

        private static Dictionary<string, object> CaptureEnvironment(
            Pawn pawn, Map map)
        {
            var result = new Dictionary<string, object>();
            IntVec3 cell = pawn.PositionHeld;
            result["cell"] = Cell(cell);
            if (!cell.IsValid || !cell.InBounds(map))
            {
                result["inBounds"] = false;
                return result;
            }
            result["inBounds"] = true;
            TerrainDef terrain = cell.GetTerrain(map);
            RoofDef roof = cell.GetRoof(map);
            Region region = map.regionGrid != null
                ? map.regionGrid.GetValidRegionAt_NoRebuild(cell) : null;
            Room room = region?.Room;
            Thing cover = cell.GetCover(map);
            result["terrain"] = DefName(terrain);
            result["water"] = terrain != null && terrain.IsWater;
            result["dangerousTerrain"] = terrain != null
                && terrain.dangerous;
            result["roof"] = DefName(roof);
            result["roofed"] = roof != null;
            result["homeArea"] = map.areaManager?.Home != null
                && map.areaManager.Home[cell];
            bool topologyDirty = map.regionDirtyer != null
                && map.regionDirtyer.AnyDirty;
            result["roomAvailable"] = room != null;
            result["roomTopologyDirty"] = topologyDirty;
            result["roomId"] = room != null ? room.ID : -1;
            bool roleDirty = room == null || roomStatsAndRoleDirtyField == null
                || Convert.ToBoolean(roomStatsAndRoleDirtyField.GetValue(room),
                    CultureInfo.InvariantCulture);
            RoomRoleDef cachedRole = room != null && roomRoleField != null
                ? roomRoleField.GetValue(room) as RoomRoleDef : null;
            bool roleAvailable = room != null && !roleDirty
                && roomRoleField != null;
            result["roomRole"] = roleAvailable
                ? DefName(cachedRole) : null;
            result["roomRoleAvailable"] = roleAvailable;
            result["roomRoleDirty"] = roleDirty;

            int cachedCellCount = room != null
                && roomCachedCellCountField != null
                    ? Convert.ToInt32(roomCachedCellCountField.GetValue(room),
                        CultureInfo.InvariantCulture) : -1;
            int cachedOpenRoofCount = room != null
                && roomCachedOpenRoofCountField != null
                    ? Convert.ToInt32(
                        roomCachedOpenRoofCountField.GetValue(room),
                        CultureInfo.InvariantCulture) : -1;
            bool cellCountAvailable = cachedCellCount >= 0;
            bool openRoofAvailable = cachedOpenRoofCount >= 0;
            result["roomCellCount"] = cellCountAvailable
                ? (object)cachedCellCount : null;
            result["roomCellCountAvailable"] = cellCountAvailable;
            result["roomCellCountDirty"] = room != null
                && !cellCountAvailable;
            result["roomOpenRoofCount"] = openRoofAvailable
                ? (object)cachedOpenRoofCount : null;
            result["roomOpenRoofCountAvailable"] = openRoofAvailable;
            result["roomOpenRoofCountDirty"] = room != null
                && !openRoofAvailable;

            bool touchesMapEdge = room != null && room.TouchesMapEdge;
            bool outdoorsAvailable = room != null && openRoofAvailable
                && (cachedOpenRoofCount >= 300 || !touchesMapEdge
                    || cellCountAvailable && cachedCellCount > 0);
            bool psychologicallyOutdoors = outdoorsAvailable
                && (cachedOpenRoofCount >= 300
                    || touchesMapEdge && cachedOpenRoofCount
                        / (float)cachedCellCount >= 0.5f);
            result["roomPsychologicallyOutdoors"] = outdoorsAvailable
                ? (object)psychologicallyOutdoors : null;
            result["roomPsychologicallyOutdoorsAvailable"] =
                outdoorsAvailable;
            result["roomPsychologicallyOutdoorsDirty"] = room != null
                && !outdoorsAvailable;
            result["roomTouchesMapEdge"] = room != null
                ? (object)touchesMapEdge : null;

            object cachedDanger;
            bool dangerDirty;
            bool dangerAvailable = TryReadCachedDanger(region, pawn,
                out cachedDanger, out dangerDirty);
            result["danger"] = dangerAvailable
                ? cachedDanger.ToString() : null;
            result["dangerAvailable"] = dangerAvailable;
            result["dangerDirty"] = dangerDirty;
            result["knownDanger"] = PawnUtility.KnownDangerAt(cell, map,
                pawn);
            result["staticFire"] = cell.ContainsStaticFire(map);
            result["polluted"] = map.pollutionGrid != null
                && map.pollutionGrid.IsPolluted(cell);
            result["snowDepth"] = map.snowGrid != null
                ? map.snowGrid.GetDepth(cell) : 0f;
            result["hazardScore"] = CACombatIntent.HazardScore(pawn, cell);
            result["toxicGas"] = map.gasGrid != null
                ? map.gasGrid.DensityPercentAt(cell, GasType.ToxGas) : 0f;
            result["rotStink"] = map.gasGrid != null
                ? map.gasGrid.DensityPercentAt(cell, GasType.RotStink) : 0f;
            result["deadlifeDust"] = map.gasGrid != null
                && ModsConfig.AnomalyActive
                    ? map.gasGrid.DensityPercentAt(cell,
                        GasType.DeadlifeDust) : 0f;
            result["coverThing"] = CaptureThing(cover);
            result["surroundingCoverScore"] =
                CoverUtility.TotalSurroundingCoverScore(cell, map);
            return result;
        }

        private static Dictionary<string, object> CaptureCAState(Pawn pawn,
            Map map)
        {
            var result = new Dictionary<string, object>();
            CAInitiativeTier initiativeTier = AutonomyComponent.TierOf(pawn);
            result["initiativeTier"] = (int)initiativeTier;
            result["initiativeName"] =
                CAInitiativePresentation.Label(initiativeTier);
            result["squad"] = SquadComponent.SquadOf(pawn);
            result["withdrawalPlan"] = map
                .GetComponent<WithdrawalMapComponent>()?.InPlan(pawn) == true;

            RaidResponseMapComponent raid =
                map.GetComponent<RaidResponseMapComponent>();
            Dictionary<string, object> shelter = CaptureShelterState(raid,
                pawn);
            result["hasShelterCommitment"] = shelter != null
                && shelter["cell"] != null;
            bool insideShelter;
            bool shelterDirty;
            bool shelterEnvelopeAvailable = TryReadShelterEnvelope(raid,
                pawn, map, out insideShelter, out shelterDirty);
            result["insideShelterEnvelope"] = shelterEnvelopeAvailable
                ? (object)insideShelter : null;
            result["insideShelterEnvelopeAvailable"] =
                shelterEnvelopeAvailable;
            result["insideShelterEnvelopeDirty"] = shelterDirty;
            result["shelter"] = shelter;

            CACombatRecoveryMapComponent recovery =
                map.GetComponent<CACombatRecoveryMapComponent>();
            CACombatRecoveryDiagnosticState recoveryState =
                default(CACombatRecoveryDiagnosticState);
            bool hasRecoveryState = recovery != null
                && recovery.TryGetDiagnosticState(pawn, out recoveryState);
            result["combatRecoveryEpisode"] = hasRecoveryState
                && recoveryState.IsRecovering
                    ? recoveryState.EpisodeId : 0;
            result["combatRecovery"] = hasRecoveryState
                ? CaptureRecoveryDiagnostic(recoveryState) : null;

            CASpatialCombatMemoryMapComponent spatialMemory =
                CASpatialCombatMemoryMapComponent.For(map);
            CASpatialCombatMemoryDiagnosticState spatialMemoryState =
                default(CASpatialCombatMemoryDiagnosticState);
            bool hasSpatialMemory = spatialMemory != null
                && spatialMemory.TryGetDiagnosticState(pawn,
                    out spatialMemoryState);
            int spatialNow = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            result["spatialCombatMemory"] = hasSpatialMemory
                ? new Dictionary<string, object>
                {
                    { "knownCellCount", spatialMemoryState.KnownCellCount },
                    { "oldestObservationTick",
                        spatialMemoryState.OldestObservationTick },
                    { "oldestObservationAgeTicks", spatialNow >= 0
                        ? spatialNow - spatialMemoryState.OldestObservationTick
                        : -1 },
                    { "newestObservationTick",
                        spatialMemoryState.NewestObservationTick },
                    { "newestObservationAgeTicks", spatialNow >= 0
                        ? spatialNow - spatialMemoryState.NewestObservationTick
                        : -1 }
                } : null;

            CAWelfareThresholdSupportMapComponent welfareSupport =
                CAWelfareThresholdSupportMapComponent.For(map);
            CAWelfareSupportDiagnosticState welfareSupportState =
                default(CAWelfareSupportDiagnosticState);
            bool hasWelfareSupport = welfareSupport != null
                && welfareSupport.TryGetDiagnosticState(pawn,
                    out welfareSupportState);
            result["welfareThresholdSupport"] = hasWelfareSupport
                ? new Dictionary<string, object>
                {
                    { "role", welfareSupportState.RequesterRole
                        ? "requester" : "covering-partner" },
                    { "requesterId", welfareSupportState.RequesterId },
                    { "partnerId", welfareSupportState.PartnerId },
                    { "subjectId", welfareSupportState.SubjectId },
                    { "episodeId", welfareSupportState.EpisodeId },
                    { "createdTick", welfareSupportState.CreatedTick },
                    { "ready", welfareSupportState.Ready },
                    { "readyTick", welfareSupportState.ReadyTick },
                    { "requesterJobId",
                        welfareSupportState.RequesterJobId },
                    { "channel", welfareSupportState.Channel.ToString() },
                    { "welfareCell", Cell(welfareSupportState.WelfareCell) },
                    { "thresholdCell",
                        Cell(welfareSupportState.ThresholdCell) },
                    { "firstExteriorCell",
                        Cell(welfareSupportState.FirstExteriorCell) },
                    { "supportCell", Cell(welfareSupportState.SupportCell) }
                } : null;

            CADraftedCombatInitiativeMapComponent initiative = map
                .GetComponent<CADraftedCombatInitiativeMapComponent>();
            CADraftedSupportDiagnosticState supportState =
                default(CADraftedSupportDiagnosticState);
            bool hasSupportState = initiative != null
                && initiative.TryGetSupportDiagnosticState(pawn,
                    out supportState);
            if (hasSupportState)
            {
                int now = Find.TickManager != null
                    ? Find.TickManager.TicksGame : -1;
                result["draftedSupportEnvelope"] =
                    new Dictionary<string, object>
                    {
                        { "pendingAttempt", supportState.PendingAttempt },
                        { "attemptJobId", supportState.AttemptJobId },
                        { "attemptOrigin", Cell(supportState.AttemptOrigin) },
                        { "targetId", supportState.TargetId },
                        { "sourceTick", supportState.SourceTick },
                        { "cooldownUntilTick",
                            supportState.CooldownUntilTick },
                        { "cooldownTicksRemaining", now >= 0
                            && supportState.CooldownUntilTick >= 0
                                ? Mathf.Max(0,
                                    supportState.CooldownUntilTick - now)
                                : -1 },
                        { "anchor", Cell(supportState.Anchor) },
                        { "targetCell", Cell(supportState.TargetCell) }
                    };
            }
            else result["draftedSupportEnvelope"] = null;

            CAReportedSupportEvaluation supportEvaluation = null;
            bool hasSupportEvaluation = initiative != null
                && initiative.TryGetSupportEvaluation(pawn,
                    out supportEvaluation);
            result["draftedSupportEvaluation"] = hasSupportEvaluation
                ? new Dictionary<string, object>
                {
                    { "tick", supportEvaluation.Tick },
                    { "ageTicks", Find.TickManager != null
                        && supportEvaluation.Tick >= 0
                            ? Mathf.Max(0, Find.TickManager.TicksGame
                                - supportEvaluation.Tick)
                            : -1 },
                    { "stage", supportEvaluation.Stage },
                    { "reason", supportEvaluation.Reason },
                    { "candidateCount", supportEvaluation.CandidateCount },
                    { "targetId", supportEvaluation.TargetId },
                    { "reporterId", supportEvaluation.ReporterId },
                    { "sourceTick", supportEvaluation.SourceTick },
                    { "contactCell", Cell(supportEvaluation.ContactCell) },
                    { "destination", Cell(supportEvaluation.Destination) },
                    { "move", supportEvaluation.Move },
                    { "approach", supportEvaluation.Approach },
                    { "currentCover", supportEvaluation.CurrentCover },
                    { "destinationCover",
                        supportEvaluation.DestinationCover },
                    { "currentSectorRisk",
                        supportEvaluation.CurrentSectorRisk },
                    { "destinationSectorRisk",
                        supportEvaluation.DestinationSectorRisk },
                     { "routeSectorRisk",
                         supportEvaluation.RouteSectorRisk },
                     { "casualtyReorganization",
                         supportEvaluation.CasualtyReorganization },
                     { "casualtyId", supportEvaluation.CasualtyId },
                     { "casualtySourceTick",
                         supportEvaluation.CasualtySourceTick }
                 }
                : null;

            CAFriendlyFireAuthorizationDiagnosticState fireAuthorization;
            bool hasFireAuthorization = CAImmediateCombat
                .TryGetFriendlyFireAuthorizationDiagnostic(pawn,
                    out fireAuthorization);
            result["friendlyFireAuthorization"] = hasFireAuthorization
                ? new Dictionary<string, object>
                {
                    { "tick", fireAuthorization.Tick },
                    { "ageTicks", Find.TickManager != null
                        && fireAuthorization.Tick >= 0
                            ? Mathf.Max(0, Find.TickManager.TicksGame
                                - fireAuthorization.Tick)
                            : -1 },
                    { "phase", fireAuthorization.Phase },
                    { "outcome", fireAuthorization.Outcome },
                    { "ownership", fireAuthorization.Ownership },
                    { "jobId", fireAuthorization.JobId },
                    { "jobDef", fireAuthorization.JobDef },
                    { "targetId", fireAuthorization.TargetId },
                    { "blockerId", fireAuthorization.BlockerId },
                    { "laneRisk", fireAuthorization.LaneRisk }
                }
                : null;

            CAIncomingEvasionDiagnosticState incomingState;
            bool hasIncomingState = CACombatIntent
                .TryGetIncomingEvasionDiagnosticState(pawn,
                    out incomingState);
            result["incomingExplosiveEvasion"] = hasIncomingState
                ? CaptureIncomingEvasionDiagnostic(incomingState) : null;

            Lord lord = pawn.GetLord();
            result["lord"] = CaptureLord(lord);
            result["duty"] = CaptureDuty(pawn.mindState?.duty);
            var tactical = new Dictionary<string, object>();
            LordJob_CATactical tacticalJob = lord?.LordJob
                as LordJob_CATactical;
            int kind = -1;
            IntVec3 orderCell = IntVec3.Invalid;
            IntVec3 watchCell = IntVec3.Invalid;
            bool hasOrder = tacticalJob != null
                && tacticalJob.TryGetOrder(pawn, out kind, out orderCell,
                    out watchCell);
            tactical["hasOrder"] = hasOrder;
            if (hasOrder)
            {
                tactical["kind"] = kind;
                tactical["kindName"] = TacticalKindName(kind);
                tactical["cell"] = Cell(orderCell);
                tactical["watch"] = Cell(watchCell);
                Dictionary<string, object> context;
                bool? fireSuppressed;
                if (TryReadTacticalContext(tacticalJob, pawn, out context,
                    out fireSuppressed))
                {
                    tactical["context"] = context;
                    tactical["fireSuppressionOwned"] = fireSuppressed;
                }
            }
            result["standingTacticalOrder"] = tactical;
            result["movementIntent"] = CaptureMovementIntent(pawn);
            return result;
        }

        private static bool TryReadCachedDanger(Region region, Pawn pawn,
            out object value, out bool dirty)
        {
            value = null;
            dirty = true;
            if (region == null || pawn == null
                || regionCachedDangersField == null
                || regionCachedDangersTickField == null) return false;
            IDictionary cached = regionCachedDangersField.GetValue(region)
                as IDictionary;
            int cacheTick = Convert.ToInt32(
                regionCachedDangersTickField.GetValue(region),
                CultureInfo.InvariantCulture);
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            dirty = now < 0 || cacheTick != now;
            if (dirty || cached == null || !cached.Contains(pawn))
                return false;
            value = cached[pawn];
            return value != null;
        }

        private static Dictionary<string, object> CaptureRecoveryDiagnostic(
            CACombatRecoveryDiagnosticState state)
        {
            var result = new Dictionary<string, object>();
            result["ownership"] = state.IsRecovering
                ? "combat-recovery"
                : state.HasOperatorOverride ? "operator-override"
                    : state.HasMoveCooldown ? "move-cooldown" : "none";
            result["isRecovering"] = state.IsRecovering;
            result["episodeId"] = state.IsRecovering
                ? (object)state.EpisodeId : null;
            result["moveCooldown"] = state.HasMoveCooldown
                ? new Dictionary<string, object>
                {
                    { "nextMoveTick", state.NextMoveTick },
                    { "ticksRemaining",
                        state.MoveCooldownTicksRemaining }
                } : null;
            result["operatorOverride"] = state.HasOperatorOverride
                ? new Dictionary<string, object>
                {
                    { "harmTick", state.OperatorOverrideHarmTick },
                    { "untilTick", state.OperatorOverrideUntilTick },
                    { "ticksRemaining",
                        state.OperatorOverrideTicksRemaining },
                    { "combatPower", state.OperatorOverrideCombatPower },
                    { "bloodLoss", state.OperatorOverrideBloodLoss },
                    { "bleedRate", state.OperatorOverrideBleedRate },
                    { "deathTicks", state.OperatorOverrideDeathTicks }
                } : null;
            return result;
        }

        private static Dictionary<string, object>
            CaptureIncomingEvasionDiagnostic(
                CAIncomingEvasionDiagnosticState state)
        {
            var result = new Dictionary<string, object>();
            string ownership = state.Status
                == CAIncomingEvasionResult.Pending
                    ? "pending-search"
                    : state.Status == CAIncomingEvasionResult.Active
                        ? state.Reconstructed
                            ? "post-load-reconstructed"
                            : state.IntentOrigin
                                == CAIntentOrigin.Continuation
                                ? "reused-route" : "active-route"
                        : state.ObservedHazardCount > 0
                            ? "observed-not-owned" : "none";
            result["ownership"] = ownership;
            result["status"] = state.Status.ToString();
            result["mapId"] = state.MapId;
            result["observedHazardCount"] = state.ObservedHazardCount;
            result["activeHazardCount"] = state.ActiveHazardCount;
            result["episodeId"] = state.EpisodeId > 0
                ? (object)state.EpisodeId : null;
            result["intentOrigin"] = state.IntentOrigin.ToString();
            result["reconstructed"] = state.Reconstructed;
            result["hazard"] = state.ProjectileId >= 0
                ? new Dictionary<string, object>
                {
                    { "projectileId", state.ProjectileId },
                    { "launcherId", state.LauncherId },
                    { "center", Cell(state.HazardCenter) },
                    { "radius", state.HazardRadius },
                    { "deadlineTick", state.DeadlineTick },
                    { "deadlineTicksRemaining",
                        state.DeadlineTicksRemaining },
                    { "lastFailedEvaluationTick",
                        state.LastFailedEvaluationTick }
                } : null;
            result["route"] = state.Origin.IsValid
                || state.Destination.IsValid
                ? new Dictionary<string, object>
                {
                    { "origin", Cell(state.Origin) },
                    { "destination", Cell(state.Destination) }
                } : null;
            result["search"] = state.SearchActive
                ? new Dictionary<string, object>
                {
                    { "hazardSignature", state.SearchHazardSignature },
                    { "clearingCursor", state.ClearingCursor },
                    { "clearingCandidateCount",
                        state.ClearingCandidateCount },
                    { "clearingPathBudgetPerTick",
                        state.ClearingPathBudgetPerTick },
                    { "improvingCursor", state.ImprovingCursor },
                    { "improvingCandidateCount",
                        state.ImprovingCandidateCount },
                    { "improvingPathBudgetPerTick",
                        state.ImprovingPathBudgetPerTick }
                } : null;
            return result;
        }

        private static Dictionary<string, object> CaptureShelterState(
            RaidResponseMapComponent raid, Pawn pawn)
        {
            if (raid == null || pawn == null) return null;
            IDictionary cells = shelterCellsField?.GetValue(raid)
                as IDictionary;
            IDictionary episodes = shelterEpisodesField?.GetValue(raid)
                as IDictionary;
            bool hasCell = cells != null && cells.Contains(pawn.thingIDNumber);
            bool hasEpisode = episodes != null
                && episodes.Contains(pawn.thingIDNumber);
            if (!hasCell && !hasEpisode) return null;
            var result = new Dictionary<string, object>();
            result["cell"] = hasCell && cells[pawn.thingIDNumber] is IntVec3
                ? Cell((IntVec3)cells[pawn.thingIDNumber]) : null;
            result["episodeId"] = hasEpisode
                ? Convert.ToInt32(episodes[pawn.thingIDNumber],
                    CultureInfo.InvariantCulture) : 0;
            return result;
        }

        private static bool TryReadShelterEnvelope(
            RaidResponseMapComponent raid, Pawn pawn, Map map,
            out bool inside, out bool dirty)
        {
            inside = false;
            dirty = false;
            if (raid == null || pawn == null || map == null) return true;
            if (shelterCellsField == null)
            {
                dirty = true;
                return false;
            }
            IDictionary cells = shelterCellsField.GetValue(raid)
                as IDictionary;
            if (cells == null || !cells.Contains(pawn.thingIDNumber))
                return true;
            object stored = cells[pawn.thingIDNumber];
            if (!(stored is IntVec3))
            {
                dirty = true;
                return false;
            }
            IntVec3 assigned = (IntVec3)stored;
            IntVec3 current = pawn.PositionHeld;
            if (!current.InBounds(map) || !current.Roofed(map)
                || !assigned.IsValid || !assigned.InBounds(map))
                return true;
            Room assignedRoom;
            bool assignedProper;
            if (!TryReadProperEnclosedHomeCell(map, assigned,
                    out assignedRoom, out assignedProper, out dirty))
                return false;
            if (!assignedProper) return true;
            Region currentRegion = map.regionGrid?
                .GetValidRegionAt_NoRebuild(current);
            if (currentRegion?.Room == null)
            {
                dirty = map.regionDirtyer != null
                    && map.regionDirtyer.AnyDirty;
                return !dirty;
            }
            inside = ReferenceEquals(assignedRoom, currentRegion.Room);
            return true;
        }

        private static bool TryReadProperEnclosedHomeCell(Map map,
            IntVec3 cell, out Room room, out bool proper, out bool dirty)
        {
            room = null;
            proper = false;
            dirty = false;
            if (map == null || !cell.IsValid || !cell.InBounds(map)
                || !cell.Standable(map) || !cell.Roofed(map)
                || map.areaManager?.Home == null
                || !map.areaManager.Home[cell]) return true;

            if (map.regionDirtyer != null && map.regionDirtyer.AnyDirty)
            {
                dirty = true;
                return false;
            }
            Region region = map.regionGrid?
                .GetValidRegionAt_NoRebuild(cell);
            room = region?.Room;
            if (room == null) return true;

            bool touchesMapEdge = room.TouchesMapEdge;
            if (!room.ProperRoom || room.IsDoorway || touchesMapEdge)
                return true;
            int openRoofCount = roomCachedOpenRoofCountField != null
                ? Convert.ToInt32(
                    roomCachedOpenRoofCountField.GetValue(room),
                    CultureInfo.InvariantCulture) : -1;
            if (openRoofCount < 0)
            {
                dirty = true;
                return false;
            }
            // TouchesMapEdge is already false. In the installed 1.6 getter,
            // the ratio (and therefore CellCount) is consulted only for an
            // edge-touching room, so cachedOpenRoofCount is sufficient here.
            proper = openRoofCount < 300;
            return true;
        }

        private static Dictionary<string, object> CaptureMovementIntent(
            Pawn pawn)
        {
            if (pawn == null || combatMovementsField == null) return null;
            IDictionary movements = combatMovementsField.GetValue(null)
                as IDictionary;
            if (movements == null || !movements.Contains(pawn.thingIDNumber))
                return null;
            object movement = movements[pawn.thingIDNumber];
            if (movement == null) return null;
            Type type = movement.GetType();
            CAIntentContext context = Field(type, movement, "Context",
                default(CAIntentContext));
            var result = new Dictionary<string, object>();
            result["episodeId"] = context.EpisodeId;
            result["origin"] = context.Origin.ToString();
            result["controller"] = context.Controller.ToString();
            result["issuerId"] = context.IssuerId;
            result["valid"] = context.IsValid;
            result["from"] = Cell(Field(type, movement, "From",
                IntVec3.Invalid));
            result["to"] = Cell(Field(type, movement, "To",
                IntVec3.Invalid));
            int recordedTick = Field<int>(type, movement, "Tick", -1);
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            IntVec3 destination = Field(type, movement, "To",
                IntVec3.Invalid);
            result["recordedTick"] = recordedTick;
            result["ageTicks"] = now >= 0 && recordedTick >= 0
                ? (object)Mathf.Max(0, now - recordedTick) : null;
            result["fresh"] = now >= recordedTick && recordedTick >= 0
                && now - recordedTick <= 1200;
            result["matchesCurrentJobDestination"] = pawn.CurJob != null
                && pawn.CurJob.targetA.IsValid
                && pawn.CurJob.targetA.Cell == destination;
            result["targetId"] = Field<int>(type, movement, "TargetId", -1);
            return result;
        }

        private static bool TryReadTacticalContext(
            LordJob_CATactical tactical, Pawn pawn,
            out Dictionary<string, object> context,
            out bool? fireSuppressed)
        {
            context = null;
            fireSuppressed = null;
            if (tactical == null || pawn == null
                || tacticalOrderPawnsField == null) return false;
            IList pawns = tacticalOrderPawnsField.GetValue(tactical) as IList;
            int index = IndexOfReference(pawns, pawn);
            if (index < 0) return false;
            IList episodes = tacticalEpisodesField?.GetValue(tactical)
                as IList;
            IList origins = tacticalOriginsField?.GetValue(tactical) as IList;
            IList controllers = tacticalControllersField?.GetValue(tactical)
                as IList;
            IList issuers = tacticalIssuersField?.GetValue(tactical) as IList;
            IList suppressed = tacticalFireSuppressedField?.GetValue(tactical)
                as IList;
            if (!HasIndex(episodes, index) || !HasIndex(origins, index)
                || !HasIndex(controllers, index) || !HasIndex(issuers, index))
                return false;
            int episode = Convert.ToInt32(episodes[index],
                CultureInfo.InvariantCulture);
            var value = new Dictionary<string, object>();
            value["episodeId"] = episode;
            value["origin"] = ((CAIntentOrigin)Convert.ToInt32(
                origins[index], CultureInfo.InvariantCulture)).ToString();
            value["controller"] = ((CAIntentController)Convert.ToInt32(
                controllers[index], CultureInfo.InvariantCulture)).ToString();
            value["issuerId"] = Convert.ToInt32(issuers[index],
                CultureInfo.InvariantCulture);
            value["valid"] = episode > 0;
            context = value;
            if (HasIndex(suppressed, index))
                fireSuppressed = Convert.ToBoolean(suppressed[index],
                    CultureInfo.InvariantCulture);
            return true;
        }

        private static Dictionary<string, object> CaptureEventActor(Pawn pawn)
        {
            if (pawn == null) return null;
            var result = CapturePawnIdentity(pawn);
            result["status"] = CaptureStatus(pawn);
            result["path"] = CapturePath(pawn);
            result["combatAction"] = CaptureCombatAction(pawn);
            result["lord"] = CaptureLord(pawn.GetLord());
            result["duty"] = CaptureDuty(pawn.mindState?.duty);
            Map map = pawn.MapHeld;
            result["caState"] = map != null
                ? CaptureCAState(pawn, map) : null;
            result["carried"] = CaptureThing(pawn.carryTracker != null
                ? pawn.carryTracker.CarriedThing : null);
            return result;
        }

        private static Dictionary<string, object> CaptureLord(Lord lord)
        {
            if (lord == null) return null;
            var result = new Dictionary<string, object>();
            result["loadId"] = lord.loadID;
            result["jobType"] = lord.LordJob != null
                ? lord.LordJob.GetType().FullName : null;
            result["toilType"] = lord.CurLordToil != null
                ? lord.CurLordToil.GetType().FullName : null;
            result["faction"] = lord.faction != null
                ? DefName(lord.faction.def) : null;
            result["ticksInToil"] = lord.ticksInToil;
            return result;
        }

        private static Dictionary<string, object> CaptureDuty(PawnDuty duty)
        {
            if (duty == null) return null;
            var result = new Dictionary<string, object>();
            result["def"] = DefName(duty.def);
            result["tag"] = duty.tag;
            result["focus"] = CaptureTarget(duty.focus);
            result["focusSecond"] = CaptureTarget(duty.focusSecond);
            result["focusThird"] = CaptureTarget(duty.focusThird);
            result["radius"] = duty.radius;
            result["locomotion"] = duty.locomotion.ToString();
            result["maxDanger"] = duty.maxDanger.ToString();
            result["overrideFacing"] = duty.overrideFacing.ToString();
            return result;
        }

        private static Dictionary<string, object> CaptureTarget(
            LocalTargetInfo target)
        {
            if (!target.IsValid) return null;
            var result = new Dictionary<string, object>();
            result["cell"] = Cell(target.Cell);
            result["thing"] = CaptureThing(target.Thing);
            return result;
        }

        private static Dictionary<string, object> CaptureThing(Thing thing)
        {
            if (thing == null) return null;
            var result = new Dictionary<string, object>();
            result["thingId"] = thing.ThingID;
            result["id"] = thing.thingIDNumber;
            result["def"] = DefName(thing.def);
            Pawn pawn = thing as Pawn;
            string labelSource = null;
            result["label"] = pawn != null
                ? ReadPawnLabel(pawn, out labelSource) : thing.def?.label;
            result["labelSource"] = pawn != null
                ? labelSource : "ThingDef.label";
            result["stackCount"] = thing.stackCount;
            result["hitPoints"] = thing.HitPoints;
            float cachedMaxHitPoints = 0f;
            bool maxHitPointsDirty = true;
            int maxHitPointsCacheTick = -1;
            bool maxHitPointsApplicable = thing.def?.useHitPoints == true;
            bool maxHitPointsAvailable = maxHitPointsApplicable
                && TryReadFreshCachedStat(thing, StatDefOf.MaxHitPoints, 10,
                    out cachedMaxHitPoints, out maxHitPointsDirty,
                    out maxHitPointsCacheTick);
            if (!maxHitPointsApplicable)
            {
                cachedMaxHitPoints = 0f;
                maxHitPointsDirty = false;
                maxHitPointsCacheTick = -1;
            }
            result["maxHitPoints"] = maxHitPointsAvailable
                ? (object)Mathf.RoundToInt(cachedMaxHitPoints) : null;
            result["maxHitPointsAvailable"] = maxHitPointsAvailable;
            result["maxHitPointsDirty"] = maxHitPointsDirty;
            result["maxHitPointsApplicable"] = maxHitPointsApplicable;
            result["maxHitPointsCacheTick"] = maxHitPointsCacheTick >= 0
                ? (object)maxHitPointsCacheTick : null;
            result["stuff"] = DefName(thing.Stuff);
            result["spawned"] = thing.Spawned;
            result["destroyed"] = thing.Destroyed;
            result["mapId"] = MapId(thing.MapHeld);
            result["position"] = Cell(thing.PositionHeld);
            QualityCategory quality;
            result["quality"] = thing.TryGetQuality(out quality)
                ? quality.ToString() : null;
            return result;
        }

        private static Dictionary<string, object> JobEvent(string eventType,
            long sequence, int tick, int absTick, Pawn pawn,
            Dictionary<string, object> previous,
            Dictionary<string, object> next, JobCondition condition,
            bool? fromQueue, Dictionary<string, object> before,
            Dictionary<string, object> after, string hook)
        {
            var result = Record("job-" + eventType, sequence, tick, absTick);
            result["event"] = eventType;
            result["hook"] = hook;
            result["source"] = Source(hook, "job-lifecycle",
                "automatic");
            if (eventType == "proposed") result["lifecycle"] = "proposed";
            else if (eventType == "start") result["lifecycle"] = "started";
            else if (condition == JobCondition.Succeeded)
                result["lifecycle"] = "ended";
            else if (condition == JobCondition.InterruptForced
                || condition == JobCondition.InterruptOptional)
                result["lifecycle"] = "interrupted";
            else result["lifecycle"] = "failed";
            result["mapId"] = MapId(pawn?.MapHeld);
            result["pawn"] = CapturePawnIdentity(pawn);
            result["condition"] = condition.ToString();
            result["fromQueue"] = fromQueue.HasValue
                ? (object)fromQueue.Value : null;
            result["priorJob"] = previous;
            result["newJob"] = next;
            result["actorBefore"] = before;
            result["actorAfter"] = after;
            return result;
        }

        private static Dictionary<string, object> Record(string type,
            long sequence, int tick, int absTick)
        {
            var result = new Dictionary<string, object>();
            result["recordType"] = type;
            result["sequence"] = sequence;
            result["ticksGame"] = tick;
            result["ticksAbs"] = absTick;
            result["source"] = Source("CACombatFlightRecorder",
                type, "automatic");
            return result;
        }

        private static Dictionary<string, object> Source(string producer,
            string boundary, string mode)
        {
            return new Dictionary<string, object>
            {
                { "producer", producer },
                { "boundary", boundary },
                { "mode", mode },
                { "module", "CombatFlightRecorderModule" }
            };
        }

        private static void AddJobEvent(Dictionary<string, object> value)
        {
            if (value == null) return;
            lock (sync) jobEvents.Add(value);
        }

        internal static void NoteProjectileLaunch(Thing launcher)
        {
            try
            {
                if (!CATrace.On || Current.Game == null || launcher == null)
                    return;
                EnsureSession(Current.Game);
                MarkCombatFact(launcher.MapHeld);
            }
            catch (Exception exception)
            {
                RecordFailure("projectile-launch-tail", exception);
            }
        }

        private static bool ShouldRecord(Pawn pawn,
            bool establishCombatFact = false)
        {
            if (!CATrace.On || Current.Game == null || pawn == null)
                return false;
            EnsureSession(Current.Game);
            Map map = pawn.MapHeld;
            if (map == null) return false;
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            if (establishCombatFact)
            {
                if (now >= 0) lastCombatFactTicks[map.uniqueID] = now;
                return true;
            }
            bool registeredAttackTarget;
            bool registrationBootstrap;
            int registrationAge;
            int registeredTargetCount;
            ObserveRegistrationGate(map, now, out registeredAttackTarget,
                out registrationBootstrap, out registrationAge,
                out registeredTargetCount);
            if (registrationBootstrap) return true;
            int last;
            return lastCombatFactTicks.TryGetValue(map.uniqueID, out last)
                && now >= last && now - last <= CombatFactTailTicks;
        }

        private static void MarkCombatFact(Map map)
        {
            if (map == null) return;
            int now = Find.TickManager != null
                ? Find.TickManager.TicksGame : -1;
            if (now >= 0) lastCombatFactTicks[map.uniqueID] = now;
        }

        private static bool RelevantHumanlike(Pawn pawn)
        {
            return pawn != null && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike;
        }

        // The native active-threat predicate reaches Pawn.Awake and, for some map
        // generators, reachability. Those reads can populate capacity and pathing
        // caches. Recorder gating therefore reads only the attack-target set the
        // game already maintains for the player faction. A stable set opens one
        // bounded bootstrap; it cannot refresh capture forever. A changed manifest
        // opens a new bootstrap, while actual damage/projectile/controller facts
        // separately establish the short combat tail.
        private static void ObserveRegistrationGate(Map map, int now,
            out bool registered, out bool bootstrapActive,
            out int bootstrapAge, out int targetCount)
        {
            long signature;
            registered = TryGetRegisteredAttackTargetSignature(map,
                out signature, out targetCount);
            bootstrapActive = false;
            bootstrapAge = -1;
            if (map == null) return;
            int mapId = map.uniqueID;
            if (!registered)
            {
                registrationSignatures.Remove(mapId);
                registrationBootstrapStartTicks.Remove(mapId);
                return;
            }

            long previousSignature;
            int started;
            if (!registrationSignatures.TryGetValue(mapId,
                    out previousSignature)
                || previousSignature != signature
                || !registrationBootstrapStartTicks.TryGetValue(mapId,
                    out started) || now < started)
            {
                registrationSignatures[mapId] = signature;
                registrationBootstrapStartTicks[mapId] = now;
                started = now;
            }
            bootstrapAge = now >= started ? now - started : 0;
            bootstrapActive = now >= 0
                && bootstrapAge <= RegistrationBootstrapTicks;
        }

        private static bool TryGetRegisteredAttackTargetSignature(Map map,
            out long signature, out int count)
        {
            signature = map != null ? map.uniqueID : 0;
            count = 0;
            if (map == null) return false;
            Faction player = Faction.OfPlayer;
            if (player == null) return false;
            if (map.attackTargetsCache == null
                || attackTargetsByFactionField == null) return false;
            IDictionary byFaction = attackTargetsByFactionField.GetValue(
                map.attackTargetsCache) as IDictionary;
            if (byFaction == null || !byFaction.Contains(player)) return false;
            IEnumerable targets = byFaction[player] as IEnumerable;
            if (targets == null) return false;
            foreach (object candidate in targets)
            {
                IAttackTarget target = candidate as IAttackTarget;
                Thing thing = target?.Thing;
                if (thing != null && !thing.Destroyed && thing.Spawned
                    && thing.Map == map)
                {
                    int id = thing.thingIDNumber;
                    signature ^= ((long)(uint)id << 32)
                        | (uint)(id * 397);
                    count++;
                }
            }
            signature ^= (long)count * 0x9E3779B1L;
            return count > 0;
        }

        private static void EnsureSession(Game game)
        {
            if (game == null || ReferenceEquals(sessionGame, game)) return;
            lock (sync)
            {
                if (ReferenceEquals(sessionGame, game)) return;
                sessionGame = game;
                int tick;
                int absTick;
                CaptureTicks(out tick, out absTick);
                sessionStartedTick = tick;
                sessionStartedAbsTick = absTick;
                sessionStartedUtc = DateTime.UtcNow.ToString("o",
                    CultureInfo.InvariantCulture);
                sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss.fff'Z'",
                    CultureInfo.InvariantCulture) + "-" + absTick;
                lastSnapshotTick = int.MinValue;
                nextSequence = 0;
                observationFailureCount = 0;
                exportFailureCount = 0;
                lastFailureTick = -1;
                lastFailureContext = null;
                lastFailure = null;
                jobEvents = new RingBuffer<Dictionary<string, object>>(
                    MaxJobEvents);
                behaviorEvents =
                    new RingBuffer<Dictionary<string, object>>(
                        MaxBehaviorEvents);
                pawnSnapshots = new RingBuffer<Dictionary<string, object>>(
                    MaxPawnSnapshots);
                damageEvents = new RingBuffer<Dictionary<string, object>>(
                    MaxDamageEvents);
                operatorObservations =
                    new RingBuffer<Dictionary<string, object>>(
                        MaxOperatorObservations);
                lastCombatFactTicks.Clear();
                registrationSignatures.Clear();
                registrationBootstrapStartTicks.Clear();
                activeJobs.Clear();
                retainedCombatDiagnostics.Clear();
                failuresByContext.Clear();
            }
        }

        private static long NextSequence()
        {
            lock (sync) return ++nextSequence;
        }

        private static void CaptureTicks(out int tick, out int absTick)
        {
            TickManager manager = Find.TickManager;
            tick = manager != null ? manager.TicksGame : -1;
            absTick = manager != null ? manager.TicksAbs : -1;
        }

        private static void RememberActiveJob(Pawn pawn, Job job,
            bool fromQueue)
        {
            if (pawn == null || job == null) return;
            activeJobs[pawn.thingIDNumber] = new ActiveJobMetadata
            {
                job = job,
                fromQueue = fromQueue
            };
        }

        private static bool? ActiveFromQueue(Pawn pawn, Job job)
        {
            if (pawn == null || job == null) return null;
            ActiveJobMetadata active;
            return activeJobs.TryGetValue(pawn.thingIDNumber, out active)
                && ReferenceEquals(active.job, job)
                    ? (bool?)active.fromQueue : null;
        }

        private static bool IsTrackedActiveJob(Pawn pawn, Job job)
        {
            if (pawn == null || job == null) return false;
            ActiveJobMetadata active;
            return activeJobs.TryGetValue(pawn.thingIDNumber, out active)
                && ReferenceEquals(active.job, job);
        }

        private static void RetireActiveJob(Pawn pawn, Job job)
        {
            if (pawn == null || job == null) return;
            ActiveJobMetadata active;
            if (activeJobs.TryGetValue(pawn.thingIDNumber, out active)
                && ReferenceEquals(active.job, job))
                activeJobs.Remove(pawn.thingIDNumber);
        }

        private static void PruneCaptureMaps(int now, List<Map> maps)
        {
            var live = new HashSet<int>();
            for (int i = 0; i < maps.Count; i++) live.Add(maps[i].uniqueID);
            var remove = new List<int>();
            foreach (KeyValuePair<int, int> pair in lastCombatFactTicks)
                if (!live.Contains(pair.Key)
                    || now < pair.Value
                    || now - pair.Value > CombatFactTailTicks)
                    remove.Add(pair.Key);
            for (int i = 0; i < remove.Count; i++)
                lastCombatFactTicks.Remove(remove[i]);

            remove.Clear();
            foreach (int mapId in registrationSignatures.Keys)
                if (!live.Contains(mapId)) remove.Add(mapId);
            for (int i = 0; i < remove.Count; i++)
            {
                registrationSignatures.Remove(remove[i]);
                registrationBootstrapStartTicks.Remove(remove[i]);
            }
        }

        private static void RecordFailure(string context,
            Exception exception)
        {
            lock (sync)
            {
                observationFailureCount++;
                long count;
                failuresByContext.TryGetValue(context ?? "unknown",
                    out count);
                failuresByContext[context ?? "unknown"] = count + 1;
                lastFailureTick = Find.TickManager != null
                    ? Find.TickManager.TicksGame : -1;
                lastFailureContext = context;
                lastFailure = FailureText(exception);
            }
        }

        private static string FailureText(Exception exception)
        {
            Exception root = exception?.GetBaseException();
            return root == null ? "unknown"
                : root.GetType().Name + ": " + root.Message;
        }

        private static string BuildJson()
        {
            List<Dictionary<string, object>> jobs;
            List<Dictionary<string, object>> behaviors;
            List<Dictionary<string, object>> snapshots;
            List<Dictionary<string, object>> damages;
            List<Dictionary<string, object>> observations;
            Dictionary<string, object> root;
            lock (sync)
            {
                jobs = jobEvents.Copy();
                behaviors = behaviorEvents.Copy();
                snapshots = pawnSnapshots.Copy();
                damages = damageEvents.Copy();
                observations = operatorObservations.Copy();
                var timeline = new List<Dictionary<string, object>>(
                    jobs.Count + behaviors.Count + snapshots.Count
                        + damages.Count
                        + observations.Count);
                timeline.AddRange(jobs);
                timeline.AddRange(behaviors);
                timeline.AddRange(damages);
                timeline.AddRange(snapshots);
                timeline.AddRange(observations);
                SortBySequence(timeline);

                root = new Dictionary<string, object>();
                root["schema"] = "CA-combat-flight-v3";
                root["sessionId"] = sessionId;
                root["sessionStartedUtc"] = sessionStartedUtc;
                root["sessionStartedTick"] = sessionStartedTick;
                root["sessionStartedAbsTick"] = sessionStartedAbsTick;
                root["generatedUtc"] = DateTime.UtcNow.ToString("o",
                    CultureInfo.InvariantCulture);
                root["policy"] = new Dictionary<string, object>
                {
                    { "traceRequired", true },
                    { "snapshotIntervalTicks", SnapshotIntervalTicks },
                    { "combatFactTailTicks", CombatFactTailTicks },
                    { "registrationBootstrapTicks",
                        RegistrationBootstrapTicks },
                    { "jobEventCapacity", MaxJobEvents },
                    { "behaviorEventCapacity", MaxBehaviorEvents },
                    { "pawnSnapshotCapacity", MaxPawnSnapshots },
                    { "damageEventCapacity", MaxDamageEvents },
                    { "operatorObservationCapacity",
                        MaxOperatorObservations },
                    { "skillCapture",
                        "every pawn snapshot records every SkillRecord in def-name order; these are evidence inputs, not inferred intent" },
                    { "skillRecordFields", new object[]
                        {
                            "def", "effectiveLevel", "rawLevel", "aptitude",
                            "passion", "totallyDisabled",
                            "permanentlyDisabled"
                        }
                    },
                    { "saveMutation", false }
                };
                var counters = new Dictionary<string, object>();
                counters["jobEventsRetained"] = jobs.Count;
                counters["behaviorEventsRetained"] = behaviors.Count;
                counters["pawnSnapshotsRetained"] = snapshots.Count;
                counters["damageEventsRetained"] = damages.Count;
                counters["operatorObservationsRetained"] =
                    observations.Count;
                counters["jobEventsDropped"] = jobEvents.Dropped;
                counters["behaviorEventsDropped"] = behaviorEvents.Dropped;
                counters["pawnSnapshotsDropped"] = pawnSnapshots.Dropped;
                counters["damageEventsDropped"] = damageEvents.Dropped;
                counters["operatorObservationsDropped"] =
                    operatorObservations.Dropped;
                counters["observationFailures"] = observationFailureCount;
                counters["exportFailuresBeforeThisAttempt"] =
                    exportFailureCount;
                counters["lastFailureTick"] = lastFailureTick;
                counters["lastFailureContext"] = lastFailureContext;
                counters["lastFailure"] = lastFailure;
                counters["failuresByContext"] =
                    new Dictionary<string, long>(failuresByContext);
                root["counters"] = counters;

                var combatFactMaps = new List<object>();
                foreach (KeyValuePair<int, int> pair in lastCombatFactTicks)
                    combatFactMaps.Add(new Dictionary<string, object>
                    {
                        { "mapId", pair.Key },
                        { "lastCombatFactTick", pair.Value }
                    });
                root["combatFactMaps"] = combatFactMaps;
                var registrationGates = new List<object>();
                foreach (KeyValuePair<int, long> pair in
                    registrationSignatures)
                {
                    int started;
                    registrationBootstrapStartTicks.TryGetValue(pair.Key,
                        out started);
                    registrationGates.Add(new Dictionary<string, object>
                    {
                        { "mapId", pair.Key },
                        { "targetManifestSignature", pair.Value },
                        { "bootstrapStartedTick", started }
                    });
                }
                root["registrationGates"] = registrationGates;
                root["timeline"] = timeline;
            }

            var sb = new StringBuilder(Math.Max(4096,
                (jobs.Count + behaviors.Count + snapshots.Count + damages.Count
                    + observations.Count) * 256));
            AppendJson(sb, root);
            return sb.ToString();
        }

        private static void SortBySequence(
            List<Dictionary<string, object>> records)
        {
            records.Sort(delegate(Dictionary<string, object> left,
                Dictionary<string, object> right)
            {
                return SequenceOf(left).CompareTo(SequenceOf(right));
            });
        }

        private static long SequenceOf(Dictionary<string, object> value)
        {
            object sequence;
            return value != null && value.TryGetValue("sequence", out sequence)
                ? Convert.ToInt64(sequence, CultureInfo.InvariantCulture) : 0L;
        }

        private static void AppendJson(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            string text = value as string;
            if (text != null)
            {
                AppendJsonString(sb, text);
                return;
            }
            if (value is bool)
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }
            if (value is Enum)
            {
                AppendJsonString(sb, value.ToString());
                return;
            }
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                sb.Append('{');
                bool comma = false;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (comma) sb.Append(',');
                    AppendJsonString(sb, Convert.ToString(entry.Key,
                        CultureInfo.InvariantCulture));
                    sb.Append(':');
                    AppendJson(sb, entry.Value);
                    comma = true;
                }
                sb.Append('}');
                return;
            }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                sb.Append('[');
                bool comma = false;
                foreach (object item in enumerable)
                {
                    if (comma) sb.Append(',');
                    AppendJson(sb, item);
                    comma = true;
                }
                sb.Append(']');
                return;
            }
            if (value is float)
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number))
                    sb.Append("null");
                else sb.Append(number.ToString("R",
                    CultureInfo.InvariantCulture));
                return;
            }
            if (value is double)
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number))
                    sb.Append("null");
                else sb.Append(number.ToString("R",
                    CultureInfo.InvariantCulture));
                return;
            }
            if (value is decimal || value is byte || value is sbyte
                || value is short || value is ushort || value is int
                || value is uint || value is long || value is ulong)
            {
                sb.Append(Convert.ToString(value,
                    CultureInfo.InvariantCulture));
                return;
            }
            AppendJsonString(sb, Convert.ToString(value,
                CultureInfo.InvariantCulture));
        }

        private static void AppendJsonString(StringBuilder sb, string value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("X4",
                                CultureInfo.InvariantCulture));
                        }
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes))
                    .Replace("-", "");
        }

        private static Dictionary<string, object> Cell(IntVec3 cell)
        {
            if (!cell.IsValid) return null;
            return new Dictionary<string, object>
            {
                { "x", cell.x },
                { "y", cell.y },
                { "z", cell.z }
            };
        }

        private static int MapId(Map map)
        {
            return map != null ? map.uniqueID : -1;
        }

        private static string DefName(Def def)
        {
            return def != null ? def.defName : null;
        }

        private static string TacticalKindName(int kind)
        {
            if (kind == LordJob_CATactical.KindHold) return "hold";
            if (kind == LordJob_CATactical.KindAmbush) return "ambush";
            if (kind == LordJob_CATactical.KindHide) return "hide";
            if (kind == LordJob_CATactical.KindAmbushSubdue)
                return "ambush-subdue";
            if (kind == LordJob_CATactical.KindRaidDefense)
                return "raid-defense";
            return "unknown";
        }

        private static int IndexOfReference(IList values, object expected)
        {
            if (values == null) return -1;
            for (int i = 0; i < values.Count; i++)
                if (ReferenceEquals(values[i], expected)) return i;
            return -1;
        }

        private static bool HasIndex(IList values, int index)
        {
            return values != null && index >= 0 && index < values.Count;
        }

        private static T Field<T>(Type type, object instance, string name,
            T fallback)
        {
            FieldInfo field = type.GetField(name,
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic);
            if (field == null) return fallback;
            object value = field.GetValue(instance);
            return value is T ? (T)value : fallback;
        }
    }

    [HarmonyPatch]
    internal static class Patch_CACombatFlightRecorderProjectileTail
    {
        internal static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Projectile),
                nameof(Projectile.Launch), new[]
                {
                    typeof(Thing), typeof(Vector3),
                    typeof(LocalTargetInfo), typeof(LocalTargetInfo),
                    typeof(ProjectileHitFlags), typeof(bool),
                    typeof(Thing), typeof(ThingDef)
                });
        }

        internal static void Postfix(Thing launcher)
        {
            CACombatFlightRecorder.NoteProjectileLaunch(launcher);
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    internal static class Patch_CACombatFlightRecorderTick
    {
        private static void Postfix()
        {
            CACombatFlightRecorder.Tick();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage),
        typeof(DamageInfo), typeof(float))]
    internal static class Patch_CACombatFlightRecorderAppliedDamage
    {
        private static void Prefix(Pawn __instance, DamageInfo dinfo,
            float totalDamageDealt)
        {
            CACombatFlightRecorder.NoteAppliedDamage(__instance, dinfo,
                totalDamageDealt);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class Patch_CACombatFlightRecorderStartJob
    {
        private static void Prefix(Pawn_JobTracker __instance, Job newJob,
            JobCondition lastJobEndCondition, ThinkNode jobGiver,
            ThinkTreeDef thinkTree, bool fromQueue,
            out CACombatFlightRecorder.JobStartState __state)
        {
            __state = CACombatFlightRecorder.BeginStartJob(__instance,
                newJob, lastJobEndCondition, jobGiver, thinkTree, fromQueue);
        }

        private static void Postfix(Pawn_JobTracker __instance, Job newJob,
            JobCondition lastJobEndCondition, bool fromQueue,
            CACombatFlightRecorder.JobStartState __state)
        {
            CACombatFlightRecorder.CompleteStartJob(__instance, newJob,
                lastJobEndCondition, fromQueue, __state);
        }

        private static Exception Finalizer(Exception __exception,
            CACombatFlightRecorder.JobStartState __state)
        {
            CACombatFlightRecorder.FinishStartJobScope(__state, __exception);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.ReadyForNextToil))]
    internal static class Patch_CACombatFlightRecorderFirstToil
    {
        private static void Prefix(JobDriver __instance)
        {
            CACombatFlightRecorder.NoteDriverReadyForFirstToil(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker),
        nameof(Pawn_JobTracker.EndCurrentJob))]
    internal static class Patch_CACombatFlightRecorderEndJob
    {
        private static void Prefix(Pawn_JobTracker __instance,
            out CACombatFlightRecorder.JobEndState __state)
        {
            __state = CACombatFlightRecorder.BeginEndJob(__instance,
                "EndCurrentJob", suppressDuringStart: false);
        }

        private static void Postfix(Pawn_JobTracker __instance,
            JobCondition condition,
            CACombatFlightRecorder.JobEndState __state)
        {
            CACombatFlightRecorder.CompleteEndJob(__instance, condition,
                __state, "Pawn_JobTracker.EndCurrentJob");
        }

        private static Exception Finalizer(Exception __exception)
        {
            CACombatFlightRecorder.NoteJobHookFailure(
                "job-end-finalizer", __exception);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker),
        nameof(Pawn_JobTracker.SuspendCurrentJob))]
    internal static class Patch_CACombatFlightRecorderSuspendJob
    {
        private static void Prefix(Pawn_JobTracker __instance,
            out CACombatFlightRecorder.JobEndState __state)
        {
            __state = CACombatFlightRecorder.BeginEndJob(__instance,
                "SuspendCurrentJob", suppressDuringStart: true);
        }

        private static void Postfix(Pawn_JobTracker __instance,
            JobCondition jobPauseReason,
            CACombatFlightRecorder.JobEndState __state)
        {
            CACombatFlightRecorder.CompleteEndJob(__instance,
                jobPauseReason, __state,
                "Pawn_JobTracker.SuspendCurrentJob");
        }

        private static Exception Finalizer(Exception __exception)
        {
            CACombatFlightRecorder.NoteJobHookFailure(
                "job-suspend-finalizer", __exception);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StopAll))]
    internal static class Patch_CACombatFlightRecorderStopAll
    {
        private static void Prefix(Pawn_JobTracker __instance,
            out CACombatFlightRecorder.JobEndState __state)
        {
            __state = CACombatFlightRecorder.BeginEndJob(__instance,
                "StopAll", suppressDuringStart: false);
        }

        private static void Postfix(Pawn_JobTracker __instance,
            CACombatFlightRecorder.JobEndState __state)
        {
            CACombatFlightRecorder.CompleteEndJob(__instance,
                JobCondition.InterruptForced, __state,
                "Pawn_JobTracker.StopAll");
        }

        private static Exception Finalizer(Exception __exception)
        {
            CACombatFlightRecorder.NoteJobHookFailure(
                "job-stop-all-finalizer", __exception);
            return __exception;
        }
    }
}
