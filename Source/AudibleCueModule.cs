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
    public enum AudibleCueKind
    {
        Gunfire
    }

    public enum CueAcousticClass
    {
        Quiet,
        Ordinary,
        Heavy
    }

    public enum CueProvenance
    {
        DirectHearing,
        RelayedReport
    }

    // Pawn-readable evidence. It deliberately contains no shooter id, faction,
    // target, weapon identity, or exact emitter cell.
    public readonly struct AudibleCueSnapshot
    {
        public readonly int EventId;
        public readonly AudibleCueKind Kind;
        public readonly IntVec3 ApproximateCell;
        public readonly float UncertaintyRadius;
        public readonly int SourceTick;
        public readonly int HeardTick;
        public readonly int AcquiredTick;
        public readonly int PulseCount;
        public readonly float Confidence;
        public readonly CueProvenance Provenance;
        public readonly CommunicationChannel DeliveryChannel;
        // The pawn who supplied this knower's immediate evidence edge. For
        // firsthand hearing this is the knower themself; for a report this is
        // the immediate teller, never the unknown shooter.
        public readonly int ReporterId;
        public readonly CueAcousticClass AcousticClass;
        public readonly bool ExpectedActivity;
        public readonly bool Considered;

        public AudibleCueSnapshot(int eventId, AudibleCueKind kind,
            IntVec3 approximateCell, float uncertaintyRadius, int sourceTick,
            int heardTick, int acquiredTick, int pulseCount, float confidence,
            CueProvenance provenance, CueAcousticClass acousticClass,
            bool expectedActivity, bool considered)
            : this(eventId, kind, approximateCell, uncertaintyRadius, sourceTick,
                heardTick, acquiredTick, pulseCount, confidence, provenance,
                provenance == CueProvenance.DirectHearing
                    ? CommunicationChannel.None : CommunicationChannel.Voice,
                -1, acousticClass, expectedActivity, considered)
        {
        }

        public AudibleCueSnapshot(int eventId, AudibleCueKind kind,
            IntVec3 approximateCell, float uncertaintyRadius, int sourceTick,
            int heardTick, int acquiredTick, int pulseCount, float confidence,
            CueProvenance provenance, CommunicationChannel deliveryChannel,
            int reporterId, CueAcousticClass acousticClass, bool expectedActivity,
            bool considered)
        {
            EventId = eventId;
            Kind = kind;
            ApproximateCell = approximateCell;
            UncertaintyRadius = uncertaintyRadius;
            SourceTick = sourceTick;
            HeardTick = heardTick;
            AcquiredTick = acquiredTick;
            PulseCount = pulseCount;
            Confidence = confidence;
            Provenance = provenance;
            DeliveryChannel = deliveryChannel;
            ReporterId = reporterId;
            AcousticClass = acousticClass;
            ExpectedActivity = expectedActivity;
            Considered = considered;
        }
    }

    // Acoustic behavior belongs to the weapon Def, not to sound-name guessing.
    // Core BaseGun receives an ordinary profile in Patches/WeaponAcoustics.xml;
    // descendants may replace it with a quieter, heavier, or zero-radius profile.
    public class CAWeaponAcoustics : DefModExtension
    {
        public float radius = 70f;
        public CueAcousticClass acousticClass = CueAcousticClass.Ordinary;
    }

    // Knowledge-pillar fact store for ambiguous audible evidence. It is separate
    // from ThreatContact: hearing reports can be dismissed, relayed, investigated,
    // or prepared for, but never identify or relocate a hostile on their own.
    public class AudibleCueMapComponent : MapComponent
    {
        private sealed class CueFact
        {
            public int eventId;
            public AudibleCueKind kind;
            public IntVec3 approximateCell;
            public float uncertaintyRadius;
            public int sourceTick;
            public int heardTick;
            public int acquiredTick;
            public int pulseCount;
            public int consideredPulseCount;
            public bool eventConsidered;
            public float confidence;
            public CueProvenance provenance;
            public CommunicationChannel deliveryChannel;
            public int reporterId;
            public CueAcousticClass acousticClass;
            public bool expectedActivity;
        }

        // Emitter identity exists only in this map-private physical clustering
        // index. It never enters pawn facts, relay deliveries, or snapshots.
        // Updating lastPulseTick on every shot makes the 300-tick window roll;
        // an event therefore cannot split merely because a fixed time bucket
        // boundary passed between two otherwise-continuous pulses.
        private sealed class PhysicalCueCluster
        {
            public int eventId;
            public int lastPulseTick;
        }

        // Relay queues copy immutable scalar state. They never retain CueFact or
        // Pawn references that can mutate while the pass is still being sampled.
        private readonly struct CueDelivery
        {
            public readonly int listenerId;
            public readonly int eventId;
            public readonly AudibleCueKind kind;
            public readonly IntVec3 approximateCell;
            public readonly float uncertaintyRadius;
            public readonly int sourceTick;
            public readonly int heardTick;
            public readonly int pulseCount;
            public readonly float confidence;
            public readonly CueAcousticClass acousticClass;
            public readonly CommunicationChannel deliveryChannel;
            public readonly int reporterId;

            public CueDelivery(int listenerId, int reporterId, CueFact report,
                CommunicationChannel deliveryChannel)
            {
                this.listenerId = listenerId;
                eventId = report.eventId;
                kind = report.kind;
                approximateCell = report.approximateCell;
                uncertaintyRadius = report.uncertaintyRadius;
                sourceTick = report.sourceTick;
                heardTick = report.heardTick;
                pulseCount = report.pulseCount;
                confidence = report.confidence;
                acousticClass = report.acousticClass;
                this.deliveryChannel = deliveryChannel;
                this.reporterId = reporterId;
            }
        }

        private readonly Dictionary<int, List<CueFact>> known =
            new Dictionary<int, List<CueFact>>();
        private readonly Dictionary<int, PhysicalCueCluster> physicalClusters =
            new Dictionary<int, PhysicalCueCluster>();
        private int nextPhysicalEventSequence = 1;
        private int relayCooldown;

        private const int ClusterTicks = 300;
        private const int FreshTicks = 1800;
        private const float VoiceReportRange = 11.9f;

        public AudibleCueMapComponent(Map map) : base(map) { }

        public static AudibleCueMapComponent For(Map map)
        {
            return map != null ? map.GetComponent<AudibleCueMapComponent>() : null;
        }

        // How much of a sound survives the straight path from source to
        // hearer. Each filled edifice crossed multiplies: an aperture by
        // its own transparency (an open hole passes nearly everything, a
        // closed shutter almost nothing), bare wall by a hard muffle.
        // Bounded walk, floor at 5% - sound is never fully deniable.
        private static float AcousticTransmission(Map map, IntVec3 from,
            IntVec3 to)
        {
            if (map == null || !from.InBounds(map) || !to.InBounds(map))
                return 1f;
            float factor = 1f;
            int walked = 0;
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(from, to))
            {
                if (++walked > 40 || factor <= 0.05f) break;
                if (!cell.InBounds(map) || cell == from || cell == to)
                    continue;
                Building edifice = cell.GetEdifice(map);
                if (edifice == null) continue;
                var aperture = edifice as Building_CAAperture;
                if (aperture != null)
                    factor *= Mathf.Lerp(0.25f, 1f,
                        aperture.AwarenessTransparency);
                else if (edifice.def.Fillage == FillCategory.Full)
                    factor *= 0.25f;
            }
            return Mathf.Max(0.05f, factor);
        }

        public void NoteDirectGunfire(Pawn hearer, Thing emitter, IntVec3 exactCell,
            float acousticRadius, CueAcousticClass acousticClass,
            bool expectedActivity)
        {
            if (!CanRetainCue(hearer) || emitter == null || hearer == emitter
                || IsManningPawn(hearer, emitter)) return;

            int now = Find.TickManager.TicksGame;
            int emitterId = emitter.thingIDNumber;
            float hearing = Mathf.Clamp01(hearer.health.capacities
                .GetLevel(PawnCapacityDefOf.Hearing));
            float effectiveRadius = Mathf.Max(1f, acousticRadius * hearing);
            // [apertures] The building fabric between source and hearer
            // attenuates: sound crosses an OPEN aperture nearly whole, is
            // muffled by glazing, and is nearly stopped by a solid wall
            // or a CLOSED shutter - so shuttering the windows genuinely
            // changes what the people inside hear.
            effectiveRadius = Mathf.Max(1f, effectiveRadius
                * AcousticTransmission(hearer.Map, exactCell,
                    hearer.Position));
            float distance = hearer.Position.DistanceTo(exactCell);
            float ratio = Mathf.Clamp01(distance / effectiveRadius);
            float uncertainty = Mathf.Clamp(1f + ratio * 8f
                + (1f - hearing) * 6f, 1f, 14f);
            int eventId = ResolvePhysicalEventId(emitterId, now);
            IntVec3 approximate = FuzzedCell(exactCell, hearer.thingIDNumber,
                eventId, uncertainty);
            float confidence = Mathf.Clamp01(1f - uncertainty
                / Mathf.Max(8f, acousticRadius * 0.30f));

            List<CueFact> facts = FactsFor(hearer, create: true);
            CueFact cluster = null;
            for (int i = 0; i < facts.Count; i++)
            {
                CueFact candidate = facts[i];
                if (candidate.kind == AudibleCueKind.Gunfire
                    && candidate.eventId == eventId)
                {
                    cluster = candidate;
                    break;
                }
            }

            if (cluster == null)
            {
                facts.Add(new CueFact
                {
                    eventId = eventId,
                    kind = AudibleCueKind.Gunfire,
                    approximateCell = approximate,
                    uncertaintyRadius = uncertainty,
                    sourceTick = now,
                    heardTick = now,
                    acquiredTick = now,
                    pulseCount = 1,
                    consideredPulseCount = expectedActivity ? 1 : 0,
                    confidence = confidence,
                    provenance = CueProvenance.DirectHearing,
                    deliveryChannel = CommunicationChannel.None,
                    reporterId = hearer.thingIDNumber,
                    acousticClass = acousticClass,
                    expectedActivity = expectedActivity
                });
            }
            else
            {
                // A pawn may receive a report before personally hearing a later
                // pulse from the same event. Promote that fact in place instead of
                // retaining duplicate direct and relayed entries.
                cluster.provenance = CueProvenance.DirectHearing;
                cluster.deliveryChannel = CommunicationChannel.None;
                cluster.reporterId = hearer.thingIDNumber;
                cluster.heardTick = now;
                cluster.pulseCount++;
                bool sharper = uncertainty < cluster.uncertaintyRadius
                    || confidence > cluster.confidence;
                cluster.uncertaintyRadius = Mathf.Min(
                    cluster.uncertaintyRadius, uncertainty);
                if (sharper)
                {
                    cluster.approximateCell = approximate;
                }
                cluster.confidence = Mathf.Max(cluster.confidence, confidence);
                cluster.acousticClass = acousticClass;
                // Directly observing routine activity can explain later pulses,
                // but an unexplained pulse never erases an earlier anomaly.
                cluster.expectedActivity = cluster.expectedActivity
                    && expectedActivity;
                if (cluster.expectedActivity)
                    cluster.consideredPulseCount = cluster.pulseCount;
            }
        }

        private static bool IsManningPawn(Pawn hearer, Thing emitter)
        {
            CompMannable mannable = emitter.TryGetComp<CompMannable>();
            return mannable != null && mannable.ManningPawn == hearer;
        }

        public bool TryGetFreshestCue(Pawn pawn, out AudibleCueSnapshot snapshot)
        {
            snapshot = default;
            List<CueFact> facts = FactsFor(pawn, create: false);
            if (facts == null) return false;
            int now = Find.TickManager.TicksGame;
            CueFact best = null;
            for (int i = 0; i < facts.Count; i++)
            {
                CueFact cue = facts[i];
                if (now - cue.heardTick > FreshTicks) continue;
                if (best == null || cue.heardTick > best.heardTick) best = cue;
            }
            if (best == null) return false;
            snapshot = SnapshotOf(best);
            return true;
        }

        public bool TryGetPendingCue(Pawn pawn, out AudibleCueSnapshot snapshot)
        {
            snapshot = default;
            List<CueFact> facts = FactsFor(pawn, create: false);
            if (facts == null) return false;
            int now = Find.TickManager.TicksGame;
            CueFact best = null;
            for (int i = 0; i < facts.Count; i++)
            {
                CueFact cue = facts[i];
                if (cue.expectedActivity || cue.eventConsidered
                    || now - cue.heardTick > FreshTicks
                    || cue.pulseCount <= cue.consideredPulseCount) continue;
                if (best == null || cue.heardTick > best.heardTick) best = cue;
            }
            if (best == null) return false;
            snapshot = SnapshotOf(best);
            return true;
        }

        public bool TryGetCue(Pawn pawn, int eventId,
            out AudibleCueSnapshot snapshot)
        {
            snapshot = default;
            List<CueFact> facts = FactsFor(pawn, create: false);
            if (facts == null) return false;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < facts.Count; i++)
            {
                CueFact cue = facts[i];
                if (cue.eventId != eventId
                    || now - cue.heardTick > FreshTicks) continue;
                snapshot = SnapshotOf(cue);
                return true;
            }
            return false;
        }

        public int FreshCueCount(Pawn pawn)
        {
            List<CueFact> facts = FactsFor(pawn, create: false);
            if (facts == null) return 0;
            int now = Find.TickManager.TicksGame;
            int count = 0;
            for (int i = 0; i < facts.Count; i++)
                if (now - facts[i].heardTick <= FreshTicks) count++;
            return count;
        }

        // Detached recorder view of every still-fresh pawn-private acoustic fact.
        // The returned structs contain no mutable CueFact reference and this read
        // neither marks a pulse considered nor creates a missing fact list.
        internal List<AudibleCueSnapshot> CopyFreshCues(Pawn pawn)
        {
            var result = new List<AudibleCueSnapshot>();
            List<CueFact> facts = FactsFor(pawn, create: false);
            if (facts == null) return result;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < facts.Count; i++)
                if (now - facts[i].heardTick <= FreshTicks)
                    result.Add(SnapshotOf(facts[i]));
            result.Sort(delegate(AudibleCueSnapshot a,
                AudibleCueSnapshot b)
            {
                int heard = a.HeardTick.CompareTo(b.HeardTick);
                return heard != 0 ? heard : a.EventId.CompareTo(b.EventId);
            });
            return result;
        }

        public void MarkConsidered(Pawn pawn, int eventId)
        {
            List<CueFact> facts = FactsFor(pawn, create: false);
            if (facts == null) return;
            for (int i = 0; i < facts.Count; i++)
            {
                if (facts[i].eventId != eventId) continue;
                facts[i].consideredPulseCount = facts[i].pulseCount;
            }
        }

        // A completed animal response consumes the physical acoustic event, not
        // merely its latest pulse. Automatic fire may escalate a non-response,
        // but it cannot create another job after the animal has already reacted.
        public void MarkEventConsidered(Pawn pawn, int eventId)
        {
            List<CueFact> facts = FactsFor(pawn, create: false);
            if (facts == null) return;
            for (int i = 0; i < facts.Count; i++)
            {
                if (facts[i].eventId != eventId) continue;
                facts[i].consideredPulseCount = facts[i].pulseCount;
                facts[i].eventConsidered = true;
            }
        }

        public override void MapComponentTick()
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.knowledgeContacts) return;
            if (--relayCooldown > 0) return;
            relayCooldown = 60;
            try
            {
                PropagateReports();
                Expire();
            }
            catch { }
        }

        // A voice, radio, or mental report remains an ambiguous report. It preserves the
        // teller's original age, area, pulse count, and uncertainty; the relay can
        // never turn hearing into identification or improve directional accuracy.
        private void PropagateReports()
        {
            int now = Find.TickManager.TicksGame;
            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            var deliveries = new List<CueDelivery>();
            for (int i = 0; i < all.Count; i++)
            {
                Pawn teller = all[i];
                if (!CanSpeakReport(teller)) continue;
                List<CueFact> tellerFacts = FactsFor(teller, create: false);
                if (tellerFacts == null) continue;

                for (int j = 0; j < all.Count; j++)
                {
                    Pawn listener = all[j];
                    if (listener == teller || !CanReceiveReport(listener)) continue;
                    CommunicationChannel channel;
                    if (!CommsModule.TryGetKnowledgeChannel(teller, listener,
                        VoiceReportRange, out channel)) continue;

                    for (int k = 0; k < tellerFacts.Count; k++)
                    {
                        CueFact cue = tellerFacts[k];
                        if (cue.expectedActivity
                            || now - cue.heardTick > FreshTicks
                            || !ShouldRelay(teller, cue)) continue;
                        deliveries.Add(new CueDelivery(listener.thingIDNumber,
                            teller.thingIDNumber, cue, channel));
                    }
                }
            }

            for (int i = 0; i < deliveries.Count; i++)
                NoteRelayed(deliveries[i]);
        }

        private void NoteRelayed(CueDelivery report)
        {
            List<CueFact> facts = FactsFor(report.listenerId, create: true);
            for (int i = 0; i < facts.Count; i++)
            {
                CueFact existing = facts[i];
                if (existing.eventId != report.eventId) continue;
                if (existing.provenance == CueProvenance.DirectHearing)
                {
                    // A firsthand fact cannot absorb pulses learned only through
                    // relay while continuing to claim firsthand provenance.
                    return;
                }
                // A newer relay may carry more pulses, but it cannot sharpen the
                // listener's earlier relayed location or confidence.
                if (report.pulseCount > existing.pulseCount)
                {
                    existing.pulseCount = report.pulseCount;
                    existing.heardTick = report.heardTick;
                    existing.acousticClass = report.acousticClass;
                    existing.deliveryChannel = report.deliveryChannel;
                    existing.reporterId = report.reporterId;
                }
                return;
            }

            facts.Add(new CueFact
            {
                eventId = report.eventId,
                kind = report.kind,
                approximateCell = report.approximateCell,
                uncertaintyRadius = report.uncertaintyRadius,
                sourceTick = report.sourceTick,
                heardTick = report.heardTick,
                acquiredTick = Find.TickManager.TicksGame,
                pulseCount = report.pulseCount,
                consideredPulseCount = 0,
                confidence = report.confidence * 0.85f,
                provenance = CueProvenance.RelayedReport,
                deliveryChannel = report.deliveryChannel,
                reporterId = report.reporterId,
                acousticClass = report.acousticClass,
                expectedActivity = false
            });
        }

        private static bool ShouldRelay(Pawn teller, CueFact cue)
        {
            DispositionProfile disposition = Disposition.Of(teller);
            float repetition = Mathf.Min(3,
                Math.Max(0, cue.pulseCount - 1)) * 0.12f;
            float score = 0.10f
                + disposition.initiative * 0.28f
                + disposition.discipline * 0.24f
                + disposition.conformity * 0.12f
                + repetition
                + cue.confidence * 0.06f;
            return score >= 0.55f;
        }

        private List<CueFact> FactsFor(Pawn pawn, bool create)
        {
            if (pawn == null) return null;
            return FactsFor(pawn.thingIDNumber, create);
        }

        private List<CueFact> FactsFor(int pawnId, bool create)
        {
            List<CueFact> facts;
            if (known.TryGetValue(pawnId, out facts)) return facts;
            if (!create) return null;
            facts = new List<CueFact>();
            known[pawnId] = facts;
            return facts;
        }

        private static bool CanRetainCue(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed
                && pawn.RaceProps != null
                && (pawn.RaceProps.Humanlike || pawn.RaceProps.Animal)
                && pawn.health != null;
        }

        private static bool CanSpeakReport(Pawn pawn)
        {
            return CanReceiveReport(pawn) && pawn.Awake()
                && pawn.Faction != null;
        }

        private static bool CanReceiveReport(Pawn pawn)
        {
            return CanRetainCue(pawn) && pawn.RaceProps.Humanlike;
        }

        private int ResolvePhysicalEventId(int emitterId, int now)
        {
            PhysicalCueCluster cluster;
            if (physicalClusters.TryGetValue(emitterId, out cluster)
                && now - cluster.lastPulseTick <= ClusterTicks)
            {
                cluster.lastPulseTick = now;
                return cluster.eventId;
            }

            // The public event key is deliberately allocated without emitterId;
            // it correlates reports of one event without becoming a reversible
            // encoding of the shooter or weapon Thing.
            int eventId = Gen.HashCombineInt(map.uniqueID,
                nextPhysicalEventSequence++, now, 4406599);
            physicalClusters[emitterId] = new PhysicalCueCluster
            {
                eventId = eventId,
                lastPulseTick = now
            };
            return eventId;
        }

        private IntVec3 FuzzedCell(IntVec3 exact, int hearerId, int eventId,
            float uncertainty)
        {
            int radius = Mathf.Max(1, Mathf.CeilToInt(uncertainty));
            int count = Math.Max(1, GenRadial.NumCellsInRadius(radius));
            int seed = Gen.HashCombineInt(hearerId, eventId, map.uniqueID,
                7131989);
            int index = (int)((uint)seed % (uint)count);
            IntVec3 cell = exact + GenRadial.RadialPattern[index];
            return new IntVec3(Mathf.Clamp(cell.x, 0, map.Size.x - 1), 0,
                Mathf.Clamp(cell.z, 0, map.Size.z - 1));
        }

        private static AudibleCueSnapshot SnapshotOf(CueFact cue)
        {
            return new AudibleCueSnapshot(cue.eventId, cue.kind,
                cue.approximateCell, cue.uncertaintyRadius, cue.sourceTick,
                cue.heardTick, cue.acquiredTick, cue.pulseCount, cue.confidence,
                cue.provenance, cue.deliveryChannel,
                cue.reporterId, cue.acousticClass, cue.expectedActivity,
                cue.eventConsidered
                    || cue.consideredPulseCount >= cue.pulseCount);
        }

        private void Expire()
        {
            int now = Find.TickManager.TicksGame;
            List<int> empty = new List<int>();
            foreach (KeyValuePair<int, List<CueFact>> pair in known)
            {
                pair.Value.RemoveAll(cue => now - cue.heardTick > FreshTicks);
                if (pair.Value.Count == 0) empty.Add(pair.Key);
            }
            for (int i = 0; i < empty.Count; i++) known.Remove(empty[i]);

            List<int> staleClusters = new List<int>();
            foreach (KeyValuePair<int, PhysicalCueCluster> pair in physicalClusters)
                if (now - pair.Value.lastPulseTick > FreshTicks)
                    staleClusters.Add(pair.Key);
            for (int i = 0; i < staleClusters.Count; i++)
                physicalClusters.Remove(staleClusters[i]);
        }
    }

    public static class CA_AudibleDefs
    {
        private static JobDef investigateGunfire;

        public static JobDef InvestigateGunfire
        {
            get
            {
                return investigateGunfire ?? (investigateGunfire =
                    DefDatabase<JobDef>.GetNamed("CA_InvestigateGunfire"));
            }
        }
    }

    public static class AudibleCueResponse
    {
        public static bool ShouldInvestigate(Pawn pawn,
            AudibleCueSnapshot cue, out float score)
        {
            DispositionProfile disposition = Disposition.Of(pawn);
            float distance = pawn.Position.DistanceTo(cue.ApproximateCell);
            float proximity = Mathf.Clamp01((48f - distance) / 40f);
            float repetition = Mathf.Min(3, Math.Max(0, cue.PulseCount - 1)) * 0.14f;
            float context = 0f;
            if (pawn.IsColonistPlayerControlled && NearHome(cue.ApproximateCell, pawn.Map, 12f))
                context += 0.16f;
            if (IsExactGenericAssaulter(pawn)) context += 0.10f;

            // Skepticism here means questioning the easy benign explanation. It
            // raises verification, not certainty that the report was hostile.
            score = 0.08f
                + disposition.initiative * 0.26f
                + disposition.skepticism * 0.20f
                + disposition.discipline * 0.14f
                + disposition.courage * 0.08f
                + proximity * 0.12f
                + repetition
                + context
                + cue.Confidence * 0.05f;
            return score >= 0.62f;
        }

        public static bool TryFindVantage(Pawn pawn, AudibleCueSnapshot cue,
            out IntVec3 result)
        {
            result = IntVec3.Invalid;
            Map map = pawn.Map;
            if (map == null || !cue.ApproximateCell.InBounds(map)) return false;

            float courage = Disposition.Of(pawn).courage;
            float standoff = cue.UncertaintyRadius + Mathf.Lerp(9f, 4f, courage);
            float searchRadius = Mathf.Min(24f, standoff + 6f);
            float bestScore = float.MinValue;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(
                cue.ApproximateCell, searchRadius, useCenter: true))
            {
                if (!cell.InBounds(map) || !cell.WalkableBy(map, pawn)
                    || cell.ContainsStaticFire(map)) continue;
                float fromCue = cell.DistanceTo(cue.ApproximateCell);
                if (fromCue < standoff) continue;
                if (!GenSight.LineOfSight(cell, cue.ApproximateCell, map, true))
                    continue;
                // Selection and job startup must consult the same reservation
                // authority. The investigation driver claims target A through the
                // ordinary ReservationManager; checking only destination
                // reservations here let several actors select one vantage and then
                // endlessly lose the real reservation at job start.
                if (!map.pawnDestinationReservationManager.CanReserve(cell, pawn)
                    || !pawn.CanReserve(cell, 1, -1, null, false))
                    continue;
                if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)) continue;

                float cover = CoverUtility.CalculateOverallBlockChance(
                    cell, cue.ApproximateCell, map);
                float score = cover * 11f
                    - Math.Abs(fromCue - standoff) * 0.45f
                    - pawn.Position.DistanceTo(cell) * 0.035f;
                if (cell == pawn.Position) score += 0.35f;
                if (score <= bestScore) continue;
                bestScore = score;
                result = cell;
            }
            return result.IsValid;
        }

        public static Job InvestigationJob(Pawn pawn, AudibleCueSnapshot cue,
            CAIntentContext context)
        {
            IntVec3 vantage;
            if (!TryFindVantage(pawn, cue, out vantage)) vantage = pawn.Position;
            Job job = JobMaker.MakeJob(CA_AudibleDefs.InvestigateGunfire,
                vantage, cue.ApproximateCell);
            job.expiryInterval = 600;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.count = cue.EventId;
            StampInvestigationIntent(job, context);
            return job;
        }

        internal static void StampInvestigationIntent(Job job,
            CAIntentContext context)
        {
            if (job == null || !context.IsValid) return;
            // CA_InvestigateGunfire never uses vanilla count queues. Its custom
            // driver owns this four-int durable envelope so the cue event remains
            // in job.count while the causal episode survives save/load.
            job.countQueue = new List<int>
            {
                context.EpisodeId,
                (int)context.Origin,
                (int)context.Controller,
                context.IssuerId
            };
        }

        internal static bool TryReadInvestigationIntent(Job job,
            out CAIntentContext context)
        {
            context = default(CAIntentContext);
            List<int> values = job?.countQueue;
            if (job?.def != CA_AudibleDefs.InvestigateGunfire
                || values == null || values.Count < 4 || values[0] <= 0)
                return false;
            context = new CAIntentContext(values[0],
                (CAIntentOrigin)values[1], (CAIntentController)values[2],
                values[3]);
            return context.IsValid
                && context.Controller ==
                    CAIntentController.AcousticInvestigation;
        }

        internal static CAIntentContext InvestigationIntent(Pawn pawn,
            Job job, out bool restored)
        {
            CAIntentContext context;
            if (TryReadInvestigationIntent(job, out context))
            {
                CACombatIntent.ObserveEpisode(context.EpisodeId);
                restored = false;
                return context;
            }
            context = CACombatIntent.Restored(pawn,
                CAIntentController.AcousticInvestigation, 0);
            StampInvestigationIntent(job, context);
            restored = true;
            return context;
        }

        private static bool NearHome(IntVec3 center, Map map, float radius)
        {
            if (map == null || !center.InBounds(map)) return false;
            Area home = map.areaManager.Home;
            if (home == null) return false;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(
                center, radius, useCenter: true))
                if (cell.InBounds(map) && home[cell]) return true;
            return false;
        }

        private static bool IsExactGenericAssaulter(Pawn pawn)
        {
            Lord lord = pawn != null ? pawn.GetLord() : null;
            return lord != null && lord.LordJob != null
                && lord.LordJob.GetType() == typeof(LordJob_AssaultColony)
                && lord.CurLordToil != null
                && lord.CurLordToil.GetType() == typeof(LordToil_AssaultColony);
        }
    }

    public class JobDriver_CAInvestigateGunfire : JobDriver
    {
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            CAIntentContext context;
            if (AudibleCueResponse.TryReadInvestigationIntent(job,
                    out context))
                CACombatIntent.ObserveEpisode(context.EpisodeId);
        }

        public override bool IsContinuation(Job candidate)
        {
            CAIntentContext currentContext;
            CAIntentContext candidateContext;
            return candidate != null && candidate.def == job.def
                && candidate.count == job.count
                && candidate.targetA.Cell == job.targetA.Cell
                && candidate.targetB.Cell == job.targetB.Cell
                && AudibleCueResponse.TryReadInvestigationIntent(job,
                    out currentContext)
                && AudibleCueResponse.TryReadInvestigationIntent(candidate,
                    out candidateContext)
                && currentContext.EpisodeId == candidateContext.EpisodeId;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            bool reserved = pawn.Position == job.targetA.Cell
                || pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
            if (reserved)
            {
                AudibleCueMapComponent cues = AudibleCueMapComponent.For(pawn.Map);
                AudibleCueSnapshot cue = default(AudibleCueSnapshot);
                bool hasCue = cues != null
                    && cues.TryGetCue(pawn, job.count, out cue);
                if (cues != null) cues.MarkConsidered(pawn, job.count);
                bool restored;
                CAIntentContext context = AudibleCueResponse
                    .InvestigationIntent(pawn, job, out restored);
                CATrace.Pawn(pawn,
                    "gunfire investigation STARTED for acoustic event "
                    + job.count + " near " + job.targetB.Cell
                    + (hasCue ? " (" + cue.AcousticClass.ToString()
                        .ToLowerInvariant() + ", " + cue.PulseCount + " pulse"
                        + (cue.PulseCount == 1 ? "" : "s") + ", source tick "
                        + cue.SourceTick + ", channel " + cue.DeliveryChannel
                        + ", reporter " + cue.ReporterId + ")" : "")
                    + (restored
                        ? "; legacy/restored job acquired a durable episode"
                        : "; accepted episode preserved from selection"),
                    contact: job.targetB.Cell, destination: job.targetA.Cell,
                    anchor: pawn.Position, intent: context);
            }
            return reserved;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (pawn.Position != job.targetA.Cell)
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil survey = ToilMaker.MakeToil("CAInvestigateGunfire");
            survey.initAction = delegate { pawn.pather?.StopDead(); };
            survey.tickAction = delegate
            {
                if (job.targetB.Cell.IsValid)
                    pawn.rotationTracker.FaceTarget(job.targetB.Cell);
            };
            survey.handlingFacing = true;
            survey.socialMode = RandomSocialMode.Off;
            survey.defaultDuration = 300;
            survey.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return survey;
        }
    }

    // Exact successful-projectile seam. The callback overload preserves the
    // engine's hearing-capacity scaling, room traversal, and closed-door blocking
    // without invoking vanilla Impact clamor side effects or inventing hostility.
    public static class GunshotKnowledgePatch
    {
        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var target = AccessTools.Method(typeof(Verb_LaunchProjectile),
                    "TryCastShot");
                if (target == null) throw new MissingMethodException(
                    typeof(Verb_LaunchProjectile).FullName, "TryCastShot");
                harmony.Patch(target, postfix: new HarmonyMethod(
                    typeof(GunshotKnowledgePatch), nameof(ShotPostfix)));
            }
            catch (Exception e)
            {
                Log.Warning("[Colonist Awareness] gunfire hearing seam stood down: "
                    + e.Message);
            }
        }

        public static void ShotPostfix(Verb_LaunchProjectile __instance,
            bool __result)
        {
            try
            {
                AwarenessSettings settings = AwarenessMod.Settings;
                if (!__result || settings == null || !settings.knowledgeContacts
                    || __instance == null || __instance.verbProps == null
                    || __instance.verbProps.soundCast == null) return;

                Thing source = __instance.Caster;
                if (source == null || source.MapHeld == null) return;

                float radius;
                CueAcousticClass acousticClass;
                if (!TryResolveProfile(__instance, out radius, out acousticClass)
                    || radius <= 0f) return;

                Map map = source.MapHeld;
                IntVec3 origin = source.PositionHeld;
                AudibleCueMapComponent cues = AudibleCueMapComponent.For(map);
                if (cues == null) return;

                GenClamor.DoClamor(source, origin, radius,
                    delegate(Thing emitter, Pawn hearer)
                    {
                        bool expected = IsObservedRoutineActivity(
                            hearer, emitter as Pawn);
                        cues.NoteDirectGunfire(hearer, emitter, origin,
                            radius, acousticClass, expected);
                    });
            }
            catch { }
        }

        private static bool TryResolveProfile(Verb_LaunchProjectile verb,
            out float radius, out CueAcousticClass acousticClass)
        {
            radius = 0f;
            acousticClass = CueAcousticClass.Quiet;
            ThingWithComps equipment = verb.EquipmentSource;
            ThingDef def = equipment != null ? equipment.def : null;
            CAWeaponAcoustics profile = null;
            if (def != null && def.modExtensions != null)
            {
                // Concrete descendants may append an override after BaseGun.
                for (int i = def.modExtensions.Count - 1; i >= 0; i--)
                {
                    profile = def.modExtensions[i] as CAWeaponAcoustics;
                    if (profile != null) break;
                }
            }
            if (profile != null)
            {
                radius = profile.radius;
                acousticClass = profile.acousticClass;
                return true;
            }

            // noiseRadius is dormant vanilla metadata. Only an explicit non-default
            // value is treated as a conservative opt-in for an unannotated mod gun;
            // the default 3 never turns bows and other projectiles into gunshots.
            if (verb.verbProps.noiseRadius <= 3.01f) return false;
            radius = Mathf.Clamp(verb.verbProps.noiseRadius * 6f, 8f, 30f);
            return true;
        }

        private static bool IsObservedRoutineActivity(Pawn hearer, Pawn shooter)
        {
            if (hearer == null || shooter == null || hearer == shooter
                || hearer.Faction == null || hearer.Faction != shooter.Faction
                || hearer.Map != shooter.Map) return false;
            if (!GenSight.LineOfSight(hearer.Position, shooter.Position,
                hearer.Map, true)) return false;

            JobDef current = shooter.CurJobDef;
            return current == JobDefOf.Hunt
                || (current != null && (current.defName == "CA_WarGamesJob"
                    || current.defName == "CA_PracticeHuntJob"));
        }
    }
}
