using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace ColonistAwareness
{
    // Fixed keys keep runtime profiling bounded. Adding a key is an explicit
    // module-governance decision; arbitrary strings never allocate counters.
    public enum CAModuleProfileKey : byte
    {
        BehaviorAuthorization = 0,
        KnowledgePropagation = 1,
        SocialInterpretation = 2,
        SocialAggregation = 3,
        CultureLongitudinalUpdate = 4,
        DomesticUnitLookup = 5,
        DomesticUnitTransition = 6,
        ProvisionResolution = 7,
        SettlementProgramMaintenance = 8,
        AutonomousHomePlanning = 9,
        PopulationResidentLookup = 10,
        OrganizationLookup = 11,
        SpatialSearch = 12,
        SettlementRepair = 13,
        SettlementRebuilding = 14,
        SettlementResearch = 15,
        FullMapScan = 16,
        FullWorldScan = 17,
        CompatibilityPreflight = 18,
        KnowledgeObservation = 19,
        CulturalCognition = 20,
        PoliticalCognition = 21,
        PoliticalCoalition = 22,
        InstitutionalLegitimacy = 23,
        PropositionKnowledge = 24,
        Count = 25
    }

    public sealed class CAModuleMetric
    {
        public CAModuleProfileKey Key { get; internal set; }
        public long CallCount { get; internal set; }
        public long ElapsedTimestampTicks { get; internal set; }
        public long MaximumTimestampTicks { get; internal set; }
        public long ObjectsExamined { get; internal set; }
        public long CandidatesAccepted { get; internal set; }
        public long CacheHits { get; internal set; }
        public long CacheMisses { get; internal set; }
        public long WorkSkippedOrDeferred { get; internal set; }
        public long Failures { get; internal set; }

        public double TotalMilliseconds => ElapsedTimestampTicks * 1000d
            / Stopwatch.Frequency;

        public double MaximumMilliseconds => MaximumTimestampTicks * 1000d
            / Stopwatch.Frequency;

        public double MeanMilliseconds => CallCount <= 0 ? 0d
            : TotalMilliseconds / CallCount;
    }

    public sealed class CAModuleProfileSnapshot
    {
        public bool Enabled { get; internal set; }
        public long CapturedTimestamp { get; internal set; }
        public IReadOnlyList<CAModuleMetric> Metrics { get; internal set; }

        public string ToLogText()
        {
            var text = new StringBuilder();
            text.Append("[CA][Profiler] enabled=").Append(Enabled)
                .Append("; fixed keys=").Append(Metrics.Count);
            for (int i = 0; i < Metrics.Count; i++)
            {
                CAModuleMetric metric = Metrics[i];
                if (metric.CallCount == 0 && metric.ObjectsExamined == 0
                    && metric.CacheHits == 0 && metric.CacheMisses == 0
                    && metric.WorkSkippedOrDeferred == 0
                    && metric.Failures == 0) continue;
                text.AppendLine().Append("  ").Append(metric.Key)
                    .Append(": calls=").Append(metric.CallCount)
                    .Append("; totalMs=").Append(metric.TotalMilliseconds
                        .ToString("F3", CultureInfo.InvariantCulture))
                    .Append("; maxMs=").Append(metric.MaximumMilliseconds
                        .ToString("F3", CultureInfo.InvariantCulture))
                    .Append("; meanMs=").Append(metric.MeanMilliseconds
                        .ToString("F3", CultureInfo.InvariantCulture))
                    .Append("; examined=").Append(metric.ObjectsExamined)
                    .Append("; accepted=").Append(metric.CandidatesAccepted)
                    .Append("; cache=").Append(metric.CacheHits).Append('/')
                    .Append(metric.CacheMisses)
                    .Append("; skipped=")
                    .Append(metric.WorkSkippedOrDeferred)
                    .Append("; failures=").Append(metric.Failures);
            }
            return text.ToString();
        }
    }

    public readonly struct CAModuleProfileScope : IDisposable
    {
        private readonly int key;
        private readonly long started;

        internal CAModuleProfileScope(CAModuleProfileKey key, bool enabled)
        {
            this.key = enabled ? (int)key : -1;
            started = enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        public void Dispose()
        {
            if (key < 0) return;
            CAModuleProfiler.Complete(key, Stopwatch.GetTimestamp() - started);
        }
    }

    // Runtime-only counters. They are never scribed and no simulation branch
    // reads them, so profiling can observe a campaign without entering its
    // causal state.
    public static class CAModuleProfiler
    {
        private static readonly int KeyCount =
            (int)CAModuleProfileKey.Count;
        private static readonly long[] Calls = new long[KeyCount];
        private static readonly long[] Elapsed = new long[KeyCount];
        private static readonly long[] Maximum = new long[KeyCount];
        private static readonly long[] Examined = new long[KeyCount];
        private static readonly long[] Accepted = new long[KeyCount];
        private static readonly long[] CacheHits = new long[KeyCount];
        private static readonly long[] CacheMisses = new long[KeyCount];
        private static readonly long[] Skipped = new long[KeyCount];
        private static readonly long[] Failures = new long[KeyCount];
        private static int enabled;

        public static bool Enabled => Volatile.Read(ref enabled) != 0;

        public static void SetEnabled(bool value, bool reset = false)
        {
            if (reset) Reset();
            Volatile.Write(ref enabled, value ? 1 : 0);
        }

        public static CAModuleProfileScope Measure(CAModuleProfileKey key)
        {
            return new CAModuleProfileScope(key, Enabled);
        }

        internal static void Complete(int key, long elapsed)
        {
            if (key < 0 || key >= KeyCount) return;
            Interlocked.Increment(ref Calls[key]);
            Interlocked.Add(ref Elapsed[key], Math.Max(0L, elapsed));
            UpdateMaximum(ref Maximum[key], Math.Max(0L, elapsed));
        }

        public static void Observe(CAModuleProfileKey key,
            long objectsExamined = 0, long candidatesAccepted = 0,
            long cacheHits = 0, long cacheMisses = 0,
            long workSkippedOrDeferred = 0, long failures = 0)
        {
            if (!Enabled) return;
            int index = (int)key;
            if (index < 0 || index >= KeyCount) return;
            if (objectsExamined != 0)
                Interlocked.Add(ref Examined[index], objectsExamined);
            if (candidatesAccepted != 0)
                Interlocked.Add(ref Accepted[index], candidatesAccepted);
            if (cacheHits != 0)
                Interlocked.Add(ref CacheHits[index], cacheHits);
            if (cacheMisses != 0)
                Interlocked.Add(ref CacheMisses[index], cacheMisses);
            if (workSkippedOrDeferred != 0)
                Interlocked.Add(ref Skipped[index], workSkippedOrDeferred);
            if (failures != 0)
                Interlocked.Add(ref Failures[index], failures);
        }

        public static void RecordFailure(CAModuleProfileKey key)
        {
            Observe(key, failures: 1);
        }

        public static void Reset()
        {
            for (int i = 0; i < KeyCount; i++)
            {
                Interlocked.Exchange(ref Calls[i], 0L);
                Interlocked.Exchange(ref Elapsed[i], 0L);
                Interlocked.Exchange(ref Maximum[i], 0L);
                Interlocked.Exchange(ref Examined[i], 0L);
                Interlocked.Exchange(ref Accepted[i], 0L);
                Interlocked.Exchange(ref CacheHits[i], 0L);
                Interlocked.Exchange(ref CacheMisses[i], 0L);
                Interlocked.Exchange(ref Skipped[i], 0L);
                Interlocked.Exchange(ref Failures[i], 0L);
            }
        }

        public static CAModuleProfileSnapshot Snapshot()
        {
            var metrics = new List<CAModuleMetric>(KeyCount);
            for (int i = 0; i < KeyCount; i++)
            {
                metrics.Add(new CAModuleMetric
                {
                    Key = (CAModuleProfileKey)i,
                    CallCount = Interlocked.Read(ref Calls[i]),
                    ElapsedTimestampTicks = Interlocked.Read(ref Elapsed[i]),
                    MaximumTimestampTicks = Interlocked.Read(ref Maximum[i]),
                    ObjectsExamined = Interlocked.Read(ref Examined[i]),
                    CandidatesAccepted = Interlocked.Read(ref Accepted[i]),
                    CacheHits = Interlocked.Read(ref CacheHits[i]),
                    CacheMisses = Interlocked.Read(ref CacheMisses[i]),
                    WorkSkippedOrDeferred = Interlocked.Read(ref Skipped[i]),
                    Failures = Interlocked.Read(ref Failures[i])
                });
            }
            return new CAModuleProfileSnapshot
            {
                Enabled = Enabled,
                CapturedTimestamp = Stopwatch.GetTimestamp(),
                Metrics = metrics
            };
        }

        private static void UpdateMaximum(ref long target, long value)
        {
            long current = Interlocked.Read(ref target);
            while (value > current)
            {
                long observed = Interlocked.CompareExchange(ref target,
                    value, current);
                if (observed == current) return;
                current = observed;
            }
        }
    }
}
