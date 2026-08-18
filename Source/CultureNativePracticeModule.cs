using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    public sealed class CANativeCultureEventRecord : IExposable
    {
        public int schemaVersion =
            CANativeCultureEventPersistenceContract.CurrentRecordSchemaVersion;
        public string packageId;
        public string eventDefName;
        public string practiceKey;
        public string occurrenceKey;
        public string targetIdentity;
        public int tick = -1;
        public int pawnId = -1;
        public int factionLoadId = -1;
        public int mapId = -1;
        public string localityKey;
        public int cellX = -1;
        public int cellZ = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref packageId, "packageId");
            Scribe_Values.Look(ref eventDefName, "eventDefName");
            Scribe_Values.Look(ref practiceKey, "practiceKey");
            Scribe_Values.Look(ref occurrenceKey, "occurrenceKey");
            Scribe_Values.Look(ref targetIdentity, "targetIdentity");
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref factionLoadId, "factionLoadId", -1);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref localityKey, "localityKey");
            Scribe_Values.Look(ref cellX, "cellX", -1);
            Scribe_Values.Look(ref cellZ, "cellZ", -1);
        }

        internal string ValidationFailure()
        {
            if (schemaVersion !=
                CANativeCultureEventPersistenceContract
                    .CurrentRecordSchemaVersion)
                return "unsupported native event schema";
            if (packageId.NullOrEmpty() || eventDefName.NullOrEmpty())
                return "native event identity is incomplete";
            CANativeCultureEventAdapterDef adapter =
                CANativeCultureEventAdapterRegistry.Find(packageId,
                    eventDefName);
            if (adapter == null || adapter.PracticeKey != practiceKey)
                return "native event has no exact supported adapter";
            if (tick < 0 || pawnId < 0 || factionLoadId < 0 || mapId < 0)
                return "native event occurrence identity is incomplete";
            if (occurrenceKey.NullOrEmpty() || localityKey.NullOrEmpty()
                || cellX < 0 || cellZ < 0)
                return "native event act or locality is incomplete";
            return null;
        }

        internal IntVec3 Cell => new IntVec3(cellX, 0, cellZ);
    }

    internal static class CANativeCultureOccurrenceKernel
    {
        internal static string BuildOccurrenceKey(int mapId, int pawnId,
            string practiceKey, int tick,
            CANativeCultureOccurrenceScope occurrenceScope,
            string sharedActIdentity, string targetIdentity)
        {
            switch (occurrenceScope)
            {
                case CANativeCultureOccurrenceScope.Actor:
                    return mapId + ":pawn:" + pawnId + ":"
                        + (practiceKey ?? "") + ":" + tick;
                case CANativeCultureOccurrenceScope.SharedAct:
                    return sharedActIdentity.NullOrEmpty() ? null
                        : mapId + ":act:" + sharedActIdentity + ":"
                            + (practiceKey ?? "");
                case CANativeCultureOccurrenceScope.SharedTargetAtTick:
                    return targetIdentity.NullOrEmpty() ? null
                        : mapId + ":target:" + targetIdentity + ":"
                            + (practiceKey ?? "") + ":" + tick;
                default:
                    return null;
            }
        }

        internal static int DistinctCount(
            IEnumerable<CANativeCultureEventRecord> records)
        {
            return (records ?? Enumerable.Empty<CANativeCultureEventRecord>())
                .Where(value => value != null
                    && !value.occurrenceKey.NullOrEmpty())
                .Select(value => value.occurrenceKey)
                .Distinct(StringComparer.Ordinal).Count();
        }

        internal static string BuildPairedActIdentity(int firstPawnId,
            int firstJobId, int secondPawnId, int secondJobId)
        {
            if (firstPawnId < 0 || secondPawnId < 0 || firstJobId < 0
                || secondJobId < 0 || firstPawnId == secondPawnId)
                return null;
            bool firstLeads = firstPawnId < secondPawnId;
            int lowPawn = firstLeads ? firstPawnId : secondPawnId;
            int lowJob = firstLeads ? firstJobId : secondJobId;
            int highPawn = firstLeads ? secondPawnId : firstPawnId;
            int highJob = firstLeads ? secondJobId : firstJobId;
            return "pair:" + lowPawn + ":" + lowJob + ":" + highPawn
                + ":" + highJob;
        }

        internal static bool Matches(CANativeCultureEventRecord record,
            int factionLoadId, CellRect? locality, int recentStart,
            int observedTick)
        {
            return record != null
                && record.factionLoadId == factionLoadId
                && (!locality.HasValue || locality.Value.Contains(record.Cell))
                && record.tick >= recentStart
                && record.tick <= observedTick;
        }

        // Primitive bounds keep the same production predicate directly
        // executable by the receipt harness without constructing a live Map.
        internal static bool MatchesBounds(CANativeCultureEventRecord record,
            int factionLoadId, bool mapWide, int minX, int minZ, int maxX,
            int maxZ, int recentStart, int observedTick)
        {
            CellRect? locality = mapWide ? (CellRect?)null
                : CellRect.FromLimits(minX, minZ, maxX, maxZ);
            return Matches(record, factionLoadId, locality, recentStart,
                observedTick);
        }
    }

    // Participant-level HistoryEvents emitted inside one native raid or ritual
    // share this exact act identity. A second act completed on the same map tick
    // receives a different identity; events outside a known native act fail
    // closed instead of being guessed into one occurrence.
    internal static class CANativeCultureSharedActContext
    {
        [ThreadStatic]
        private static Stack<string> active;
        private static readonly string sessionIdentity =
            Guid.NewGuid().ToString("N");
        private static long nextIdentity;

        internal static string CurrentIdentity => active != null
            && active.Count > 0 ? active.Peek() : null;

        internal static string Begin(string kind, int tick)
        {
            string identity = BuildIdentity(kind, tick, sessionIdentity,
                Interlocked.Increment(ref nextIdentity));
            if (active == null) active = new Stack<string>();
            active.Push(identity);
            return identity;
        }

        internal static string BuildIdentity(string kind, int tick,
            string sessionNonce, long sequence)
        {
            if (sessionNonce.NullOrEmpty() || sequence <= 0) return null;
            return (kind ?? "native") + ":" + tick + ":"
                + sessionNonce + ":" + sequence;
        }

        internal static void End(string identity)
        {
            if (active == null || active.Count == 0) return;
            if (active.Peek() == identity) active.Pop();
            else active.Clear();
        }
    }

    internal static class CANativeCultureEventRetentionKernel
    {
        internal const int PerLocalityPracticeOccurrenceLimit = 64;

        internal static void AppendBounded(
            List<CANativeCultureEventRecord> records,
            CANativeCultureEventRecord record)
        {
            if (records == null || record == null) return;
            records.Add(record);
            int excess = records.Where(value => SameBucket(value, record))
                .Select(value => value.occurrenceKey ?? "")
                .Distinct(StringComparer.Ordinal).Count()
                - PerLocalityPracticeOccurrenceLimit;
            while (excess > 0)
            {
                string oldestOccurrence = records.Where(value =>
                        SameBucket(value, record))
                    .GroupBy(value => value.occurrenceKey ?? "",
                        StringComparer.Ordinal)
                    .OrderBy(group => group.Min(value => value.tick))
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .First().Key;
                records.RemoveAll(value => SameBucket(value, record)
                    && string.Equals(value.occurrenceKey ?? "",
                        oldestOccurrence, StringComparison.Ordinal));
                excess--;
            }
        }

        internal static void TrimAll(List<CANativeCultureEventRecord> records)
        {
            if (records == null) return;
            var retained = records.Where(value => value != null)
                .GroupBy(value => value.mapId + "\0"
                    + value.factionLoadId + "\0"
                    + (value.localityKey ?? "") + "\0"
                    + (value.practiceKey ?? ""), StringComparer.Ordinal)
                .SelectMany(bucket => bucket.GroupBy(value =>
                        value.occurrenceKey ?? "", StringComparer.Ordinal)
                    .OrderByDescending(occurrence => occurrence.Max(value =>
                        value.tick))
                    .ThenByDescending(occurrence => occurrence.Key,
                        StringComparer.Ordinal)
                    .Take(PerLocalityPracticeOccurrenceLimit)
                    .SelectMany(occurrence => occurrence))
                .OrderBy(value => value.tick).ToList();
            records.Clear();
            records.AddRange(retained);
        }

        internal static string ValidationFailure(
            IEnumerable<CANativeCultureEventRecord> records)
        {
            IGrouping<string, CANativeCultureEventRecord> overflow =
                (records ?? Enumerable.Empty<CANativeCultureEventRecord>())
                .Where(value => value != null)
                .GroupBy(value => value.mapId + "\0"
                    + value.factionLoadId + "\0"
                    + (value.localityKey ?? "") + "\0"
                    + (value.practiceKey ?? ""), StringComparer.Ordinal)
                .FirstOrDefault(group => group.Select(value =>
                        value.occurrenceKey ?? "")
                    .Distinct(StringComparer.Ordinal).Count()
                    > PerLocalityPracticeOccurrenceLimit);
            return overflow == null ? null
                : "native Culture event ledger exceeds its per-map, "
                    + "per-faction, per-locality distinct-practice-occurrence limit";
        }

        private static bool SameBucket(CANativeCultureEventRecord left,
            CANativeCultureEventRecord right) => left != null && right != null
            && left.factionLoadId == right.factionLoadId
            && left.mapId == right.mapId
            && left.localityKey == right.localityKey
            && left.practiceKey == right.practiceKey;
    }

    [HarmonyPatch(typeof(HistoryEventsManager),
        nameof(HistoryEventsManager.RecordEvent),
        new[] { typeof(HistoryEvent), typeof(bool) })]
    internal static class CANativeCultureHistoryEventPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HistoryEvent historyEvent)
        {
            if (historyEvent.def == null) return;
            CANativeCultureEventAdapterDef adapter =
                CANativeCultureEventAdapterRegistry.Find(
                    historyEvent.def.modContentPack?.PackageId,
                    historyEvent.def.defName);
            if (adapter == null
                || !historyEvent.args.TryGetArg(HistoryEventArgsNames.Doer,
                    out Pawn pawn)
                || pawn?.MapHeld == null || pawn.Faction == null)
                return;
            CACultureLongitudinalMapComponent history =
                CACultureLongitudinalMapComponent.For(pawn.MapHeld);
            CANativeCultureEventRecord record = history?.RecordNativeEvent(
                historyEvent, pawn, adapter);
            if (record == null) return;

            CACultureRuntimeContext context = CASocialReactionWorldComponent
                .ContextFor(pawn);
            CASocialReactionWorldComponent.Current?.RecordFact(
                new CASocialFactContext
                {
                    SubjectKey = adapter.PracticeKey,
                    FactIdentity = "native-practice:" + record.occurrenceKey,
                    ActorIdentity = adapter.Provenance
                            == CANativeCultureEventProvenance.Actor
                        || adapter.Provenance
                            == CANativeCultureEventProvenance.Participant
                        ? pawn.thingIDNumber.ToString() : null,
                    TargetIdentity = record.targetIdentity,
                    OrganizationIdentity = context?.ReactionScopeIdentity,
                    KnowledgeSource = adapter.Provenance
                        == CANativeCultureEventProvenance.Observer
                            ? "direct observation"
                        : adapter.Provenance
                            == CANativeCultureEventProvenance.Participant
                                ? "direct participation"
                            : adapter.Provenance
                                == CANativeCultureEventProvenance.Recipient
                                    ? "directly experienced event"
                                : "direct native act",
                    EpistemicSourceIdentity = pawn.thingIDNumber.ToString(),
                    Known = true,
                    Realization = 1,
                    Tick = record.tick
                }, pawn, context, Array.Empty<CASocialContribution>());
        }
    }

    [HarmonyPatch(typeof(IdeoUtility),
        nameof(IdeoUtility.Notify_PlayerRaidedSomeone))]
    internal static class CANativeCultureRaidActPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string __state) => __state =
            CANativeCultureSharedActContext.Begin("raid",
                Find.TickManager?.TicksGame ?? 0);

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,
            string __state)
        {
            CANativeCultureSharedActContext.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(LordJob_Ritual), "AddParticipantThoughts")]
    internal static class CANativeCultureRitualActPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string __state) => __state =
            CANativeCultureSharedActContext.Begin("ritual",
                Find.TickManager?.TicksGame ?? 0);

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,
            string __state)
        {
            CANativeCultureSharedActContext.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(MarriageCeremonyUtility),
        nameof(MarriageCeremonyUtility.Married))]
    internal static class CANativeCultureMarriageActPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ref string __state) => __state =
            CANativeCultureSharedActContext.Begin("marriage",
                Find.TickManager?.TicksGame ?? 0);

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,
            string __state)
        {
            CANativeCultureSharedActContext.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.Settlement),
        nameof(RimWorld.Planet.Settlement.Abandon))]
    internal static class CANativeCultureSettlementAbandonmentActPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ref string __state) => __state =
            CANativeCultureSharedActContext.Begin("settlement-abandonment",
                Find.TickManager?.TicksGame ?? 0);

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,
            string __state)
        {
            CANativeCultureSharedActContext.End(__state);
            return __exception;
        }
    }


    // SoldSlave has two native emitters. Tradeable_Pawn.ResolveTrade uses the
    // negotiator who actually conducts a local sale. The remote transport-pod
    // gift path supplies an unrelated random colonist as Doer. Requiring this
    // exact scope records the former and fails closed for the latter.
    [HarmonyPatch(typeof(Tradeable_Pawn), nameof(Tradeable_Pawn.ResolveTrade))]
    internal static class CANativeCultureSlaveSaleActPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string __state) => __state =
            CANativeCultureSharedActContext.Begin("slave-sale",
                Find.TickManager?.TicksGame ?? 0);

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,
            string __state)
        {
            CANativeCultureSharedActContext.End(__state);
            return __exception;
        }
    }
}
