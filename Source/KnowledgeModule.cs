using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // What the knower could see in the contact's hands. Unknown means the fact did
    // not come from direct sight (for example, the knower was hit from off-screen),
    // or the pawn has no equipment tracker whose contents can be observed.
    public enum ContactWeaponCategory
    {
        Unknown,
        Unarmed,
        Melee,
        Ranged
    }

    public enum ContactEvidenceSource
    {
        Visual,
        Damage,
        Sensor
    }

    // The last state this knower honestly observed for a remembered contact.
    // Active is the only state that combat consumers may treat as a threat;
    // the other states remain knowledge for aftermath reasoning and census.
    public enum ThreatContactState
    {
        Active,
        Downed,
        Dead,
        Captured,
        Nonhostile,
        Nonthreatening,
        Destroyed
    }

    // Canonical provenance for one contact fact. Source identifies how the fact
    // was minted; delivery identifies how this knower received it. ReporterId is
    // the immediate teller on a relay and the knower on direct acquisition.
    public readonly struct ContactEvidence
    {
        public readonly ContactEvidenceSource Source;
        public readonly int SourceId;
        public readonly float Confidence;
        public readonly float Uncertainty;
        public readonly CommunicationChannel DeliveryChannel;
        public readonly int ReporterId;

        public bool IsDirect => DeliveryChannel == CommunicationChannel.None;

        public ContactEvidence(ContactEvidenceSource source, int sourceId,
            float confidence, float uncertainty,
            CommunicationChannel deliveryChannel, int reporterId)
        {
            Source = source;
            SourceId = sourceId;
            Confidence = confidence < 0f ? 0f : confidence > 1f ? 1f : confidence;
            Uncertainty = uncertainty < 0f ? 0f : uncertainty;
            DeliveryChannel = deliveryChannel;
            ReporterId = reporterId;
        }

        public ContactEvidence RelayedThrough(CommunicationChannel channel,
            int reporterId)
        {
            return new ContactEvidence(Source, SourceId, Confidence, Uncertainty,
                channel, reporterId);
        }
    }

    // Read-only copy of one remembered contact. Callers can reason from knowledge
    // without receiving the component's mutable Contact objects or lists.
    public readonly struct ThreatContactSnapshot
    {
        public readonly int HostileId;
        public readonly IntVec3 Cell;
        public readonly int SourceTick;
        public readonly int AcquiredTick;
        public readonly bool DirectlyAcquired;
        public readonly ContactWeaponCategory WeaponCategory;
        public readonly ContactEvidence Evidence;
        public readonly ThreatContactState State;
        public readonly int StateTick;

        public ThreatContactSnapshot(int hostileId, IntVec3 cell, int sourceTick,
            int acquiredTick, bool directlyAcquired,
            ContactWeaponCategory weaponCategory)
            : this(hostileId, cell, sourceTick, acquiredTick, weaponCategory,
                new ContactEvidence(ContactEvidenceSource.Visual, hostileId,
                    directlyAcquired ? 1f : 0.75f,
                    directlyAcquired ? 0f : 2f,
                    directlyAcquired ? CommunicationChannel.None : CommunicationChannel.Voice,
                    0), ThreatContactState.Active, sourceTick)
        {
        }

        public ThreatContactSnapshot(int hostileId, IntVec3 cell, int sourceTick,
            int acquiredTick, ContactWeaponCategory weaponCategory,
            ContactEvidence evidence)
            : this(hostileId, cell, sourceTick, acquiredTick, weaponCategory,
                evidence, ThreatContactState.Active, sourceTick)
        {
        }

        public ThreatContactSnapshot(int hostileId, IntVec3 cell, int sourceTick,
            int acquiredTick, ContactWeaponCategory weaponCategory,
            ContactEvidence evidence, ThreatContactState state, int stateTick)
        {
            HostileId = hostileId;
            Cell = cell;
            SourceTick = sourceTick;
            AcquiredTick = acquiredTick;
            DirectlyAcquired = evidence.IsDirect;
            WeaponCategory = weaponCategory;
            Evidence = evidence;
            State = state;
            StateTick = stateTick;
        }
    }

    // Pillar 2 - Knowledge, first fact type: THREAT CONTACTS. A pawn knows a hostile
    // exists because they SAW it (firsthand) or were TOLD (relayed). Behaviors read
    // what a pawn KNOWS, never global map truth: the alarm spreads person to person -
    // by local voice or across the role-shaped headset/mental command graph.
    // Contacts fade: an unrefreshed report is stale in about three in-game hours.
    // Session-only by design - a contact is transient information, not a save fact.
    public partial class KnowledgeMapComponent : MapComponent
    {
        private class Contact
        {
            public int hostileId;
            public IntVec3 lastKnown;
            public int tick;
            public int acquiredTick;
            public ContactWeaponCategory weaponCategory;
            public ContactEvidence evidence;
            public ThreatContactState state;
            public int stateTick;
            public Pawn directlyObservedPawn;
        }

        private sealed class ContactDelivery
        {
            public Pawn listener;
            public int hostileId;
            public IntVec3 cell;
            public int sourceTick;
            public ContactWeaponCategory weaponCategory;
            public ContactEvidence evidence;
        }

        // knower pawnId -> known contacts
        private readonly Dictionary<int, List<Contact>> known = new Dictionary<int, List<Contact>>();
        private int observeCooldown;
        private int relayCooldown;
        private bool contactObserveFailureReported;
        private bool welfareObserveFailureReported;
        private bool contactRelayFailureReported;
        private bool welfareRelayFailureReported;
        private bool contactExpiryFailureReported;
        private bool welfareExpiryFailureReported;
        private bool accountabilityFailureReported;

        private const float ShoutRange = 11.9f;
        private const int StaleTicks = 7500;

        public KnowledgeMapComponent(Map map) : base(map) { }

        public static KnowledgeMapComponent For(Map map)
        {
            return map != null ? map.GetComponent<KnowledgeMapComponent>() : null;
        }

        public bool KnowsAnyThreat(Pawn p)
        {
            if (p == null) return false;
            List<Contact> list;
            if (!known.TryGetValue(p.thingIDNumber, out list)) return false;
            int now = Find.TickManager.TicksGame;
            RefreshObservedStates(p, list, now);
            for (int i = 0; i < list.Count; i++)
                if (now - list[i].tick <= StaleTicks
                    && list[i].state == ThreatContactState.Active) return true;
            return false;
        }

        // A fresh, still-actionable fact about one specific hostile. The returned
        // value is a copy; resolved facts remain available through the remembered
        // contact surface below.
        public bool TryGetFreshContact(Pawn p, int hostileId,
            out ThreatContactSnapshot snapshot)
        {
            snapshot = default;
            List<Contact> list;
            if (p == null || !known.TryGetValue(p.thingIDNumber, out list)) return false;
            int now = Find.TickManager.TicksGame;
            RefreshObservedStates(p, list, now);
            for (int i = 0; i < list.Count; i++)
            {
                var contact = list[i];
                if (contact.hostileId != hostileId
                    || now - contact.tick > StaleTicks
                    || contact.state != ThreatContactState.Active) continue;
                snapshot = SnapshotOf(contact);
                return true;
            }
            return false;
        }

        // The newest still-fresh active threat this pawn knows. The returned value
        // is a copy.
        public bool TryGetFreshestContact(Pawn p, out ThreatContactSnapshot snapshot)
        {
            snapshot = default;
            List<Contact> list;
            if (p == null || !known.TryGetValue(p.thingIDNumber, out list)) return false;
            int now = Find.TickManager.TicksGame;
            RefreshObservedStates(p, list, now);
            Contact best = null;
            for (int i = 0; i < list.Count; i++)
            {
                var contact = list[i];
                if (now - contact.tick > StaleTicks
                    || contact.state != ThreatContactState.Active) continue;
                if (best == null || contact.tick > best.tick) best = contact;
            }
            if (best == null) return false;
            snapshot = SnapshotOf(best);
            return true;
        }

        // Copies every still-fresh active threat fact this pawn knows. Consumers
        // that reason about several threats must not enumerate live map pawns to
        // rediscover which remembered facts still exist.
        public List<ThreatContactSnapshot> FreshContacts(Pawn p)
        {
            var result = new List<ThreatContactSnapshot>();
            List<Contact> list;
            if (p == null || !known.TryGetValue(p.thingIDNumber, out list)) return result;
            int now = Find.TickManager.TicksGame;
            RefreshObservedStates(p, list, now);
            for (int i = 0; i < list.Count; i++)
            {
                // RETUNE memory floor: an unidentified glimpse (weapon
                // unread, no direct observation) is low-confidence by
                // construction and decays at HALF the window - shapes
                // fade; faces persist.
                bool weak = list[i].weaponCategory
                        == ContactWeaponCategory.Unknown
                    && list[i].directlyObservedPawn == null;
                int window = weak ? StaleTicks / 2 : StaleTicks;
                if (now - list[i].tick <= window
                    && list[i].state == ThreatContactState.Active)
                    result.Add(SnapshotOf(list[i]));
            }
            return result;
        }

        // Raw remembered facts include contacts whose observed state has resolved.
        // This is the aftermath/census surface; combat uses the active-only methods
        // above so retaining knowledge never keeps a combat posture alive.
        public List<ThreatContactSnapshot> RememberedContacts(Pawn p)
        {
            var result = new List<ThreatContactSnapshot>();
            List<Contact> list;
            if (p == null || !known.TryGetValue(p.thingIDNumber, out list)) return result;
            int now = Find.TickManager.TicksGame;
            RefreshObservedStates(p, list, now);
            for (int i = 0; i < list.Count; i++)
                if (now - list[i].tick <= StaleTicks)
                    result.Add(SnapshotOf(list[i]));
            return result;
        }

        public bool TryGetRememberedContact(Pawn p, int hostileId,
            out ThreatContactSnapshot snapshot)
        {
            snapshot = default;
            List<Contact> list;
            if (p == null || !known.TryGetValue(p.thingIDNumber, out list)) return false;
            int now = Find.TickManager.TicksGame;
            RefreshObservedStates(p, list, now);
            for (int i = 0; i < list.Count; i++)
            {
                Contact contact = list[i];
                if (contact.hostileId != hostileId
                    || now - contact.tick > StaleTicks) continue;
                snapshot = SnapshotOf(contact);
                return true;
            }
            return false;
        }

        public bool TryGetFreshestRememberedContact(Pawn p,
            out ThreatContactSnapshot snapshot)
        {
            snapshot = default;
            List<Contact> list;
            if (p == null || !known.TryGetValue(p.thingIDNumber, out list)) return false;
            int now = Find.TickManager.TicksGame;
            RefreshObservedStates(p, list, now);
            Contact best = null;
            for (int i = 0; i < list.Count; i++)
            {
                Contact contact = list[i];
                if (now - contact.tick > StaleTicks) continue;
                if (best == null || contact.tick > best.tick) best = contact;
            }
            if (best == null) return false;
            snapshot = SnapshotOf(best);
            return true;
        }

        private static ThreatContactSnapshot SnapshotOf(Contact contact)
        {
            return new ThreatContactSnapshot(contact.hostileId, contact.lastKnown,
                contact.tick, contact.acquiredTick, contact.weaponCategory,
                contact.evidence, contact.state, contact.stateTick);
        }

        public int RememberedContactCount(Pawn p)
        {
            if (p == null) return 0;
            List<Contact> list;
            if (!known.TryGetValue(p.thingIDNumber, out list)) return 0;
            int now = Find.TickManager.TicksGame;
            RefreshObservedStates(p, list, now);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (now - list[i].tick <= StaleTicks) n++;
            return n;
        }

        // The last place p KNOWS a hostile to be - for anticipation, not tracking.
        public IntVec3 FreshestContactCell(Pawn p)
        {
            ThreatContactSnapshot snapshot;
            return TryGetFreshestContact(p, out snapshot) ? snapshot.Cell : IntVec3.Invalid;
        }

        // Firsthand sight: live position, current tick.
        public void Note(Pawn knower, Pawn hostile, bool firsthand)
        {
            if (knower == null || hostile == null) return;
            if (!firsthand)
            {
                NoteFact(knower, hostile.thingIDNumber, hostile.Position,
                    Find.TickManager.TicksGame, firsthand: false,
                    label: hostile.LabelShort);
                return;
            }

            var evidence = new ContactEvidence(ContactEvidenceSource.Visual,
                hostile.thingIDNumber, 1f, 0f, CommunicationChannel.None,
                knower.thingIDNumber);
            NoteEvidence(knower, hostile.thingIDNumber, hostile.Position,
                Find.TickManager.TicksGame, evidence, hostile.LabelShort,
                VisibleWeaponCategory(hostile));
            RememberDirectlyObservedPawn(knower, hostile);
        }

        private void NoteVisualPerception(Pawn knower, Pawn hostile,
            CAVisualPerception perception)
        {
            if (knower == null || hostile == null || !perception.Detected)
                return;
            bool alreadyKnown = false;
            List<Contact> contacts;
            if (known.TryGetValue(knower.thingIDNumber, out contacts))
                for (int i = 0; i < contacts.Count; i++)
                    if (contacts[i].hostileId == hostile.thingIDNumber)
                    {
                        alreadyKnown = true;
                        break;
                    }

            var evidence = new ContactEvidence(ContactEvidenceSource.Visual,
                hostile.thingIDNumber, perception.Confidence,
                perception.UncertaintyCells, CommunicationChannel.None,
                knower.thingIDNumber);
            NoteEvidence(knower, hostile.thingIDNumber, hostile.Position,
                Find.TickManager.TicksGame, evidence,
                perception.Identified ? hostile.LabelShort
                    : "an unresolved hostile silhouette",
                perception.Identified && perception.Confidence >= 0.62f
                    ? VisibleWeaponCategory(hostile)
                    : ContactWeaponCategory.Unknown);
            RememberDirectlyObservedPawn(knower, hostile);
            if (!alreadyKnown)
                CATrace.Pawn(knower, "battlefield perception ACQUIRED "
                    + (perception.Identified ? hostile.LabelShort
                        : "an unresolved hostile silhouette") + " - "
                    + perception.TraceText(), contact: hostile.Position,
                    anchor: knower.Position);
        }

        private void RememberDirectlyObservedPawn(Pawn knower, Pawn hostile)
        {
            List<Contact> list;
            if (knower == null || hostile == null
                || !known.TryGetValue(knower.thingIDNumber, out list)) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].hostileId != hostile.thingIDNumber) continue;
                list[i].directlyObservedPawn = hostile;
                return;
            }
        }

        // The fact itself - what a teller REMEMBERS, not the live pawn. A relay
        // carries the teller's (cell, tick), so information AGE propagates through
        // chains and a rumor can never outlive or out-position real sight. Merge
        // rule: the newer tick wins; a relay never advances a tick beyond its source.
        public void NoteFact(Pawn knower, int hostileId, IntVec3 cell, int sourceTick,
            bool firsthand, string label,
            ContactWeaponCategory weaponCategory = ContactWeaponCategory.Unknown)
        {
            var evidence = new ContactEvidence(ContactEvidenceSource.Visual,
                hostileId, firsthand ? 1f : 0.75f, firsthand ? 0f : 2f,
                firsthand ? CommunicationChannel.None : CommunicationChannel.Voice,
                firsthand && knower != null ? knower.thingIDNumber : 0);
            NoteEvidence(knower, hostileId, cell, sourceTick, evidence, label,
                weaponCategory);
        }

        // Canonical ingress for contact facts. Existing Note/NoteFact callers keep
        // their API while new sources carry explicit confidence and provenance.
        public void NoteEvidence(Pawn knower, int hostileId, IntVec3 cell,
            int sourceTick, ContactEvidence evidence, string label,
            ContactWeaponCategory weaponCategory = ContactWeaponCategory.Unknown)
        {
            if (knower == null) return;
            List<Contact> list;
            if (!known.TryGetValue(knower.thingIDNumber, out list))
            {
                list = new List<Contact>();
                known[knower.thingIDNumber] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].hostileId != hostileId) continue;
                if (sourceTick > list[i].tick)
                {
                    bool reacquired = Find.TickManager.TicksGame - list[i].tick > StaleTicks;
                    list[i].lastKnown = cell;
                    list[i].tick = sourceTick;
                    if (reacquired) list[i].acquiredTick = Find.TickManager.TicksGame;
                    list[i].weaponCategory = weaponCategory;
                    list[i].evidence = evidence;
                    list[i].state = ThreatContactState.Active;
                    list[i].stateTick = sourceTick;
                }
                else if (sourceTick == list[i].tick)
                {
                    if (EvidenceIsBetter(evidence, list[i].evidence))
                    {
                        list[i].lastKnown = cell;
                        list[i].evidence = evidence;
                    }
                    if (list[i].weaponCategory == ContactWeaponCategory.Unknown
                        && weaponCategory != ContactWeaponCategory.Unknown)
                        list[i].weaponCategory = weaponCategory;
                }
                return;
            }
            list.Add(new Contact
            {
                hostileId = hostileId,
                lastKnown = cell,
                tick = sourceTick,
                acquiredTick = Find.TickManager.TicksGame,
                weaponCategory = weaponCategory,
                evidence = evidence,
                state = ThreatContactState.Active,
                stateTick = sourceTick
            });
            string action = evidence.DeliveryChannel != CommunicationChannel.None
                ? "is told about "
                : evidence.Source == ContactEvidenceSource.Damage
                    ? "takes fire from "
                    : evidence.Source == ContactEvidenceSource.Sensor
                        ? "detects " : "SPOTS ";
            CATrace.Pawn(knower, action + label
                + " [hostile-id " + hostileId
                + ", copied-cell " + cell
                + ", source-tick " + sourceTick
                + ", acquired-tick " + Find.TickManager.TicksGame
                + ", evidence " + evidence.Source
                + ", source-id " + evidence.SourceId
                + ", confidence " + evidence.Confidence.ToString("F2")
                + ", uncertainty " + evidence.Uncertainty.ToString("F2")
                + ", delivery " + evidence.DeliveryChannel
                + ", reporter-id " + evidence.ReporterId
                + ", state " + ThreatContactState.Active + "]",
                contact: cell);
        }

        // Future sensor implementations enter through the same evidence contract
        // without acquiring access to the component's mutable contact storage.
        public void NoteSensorContact(Pawn knower, int hostileId, IntVec3 cell,
            int sourceTick, int sensorId, float confidence, float uncertainty,
            string label = "a sensor contact",
            ContactWeaponCategory weaponCategory = ContactWeaponCategory.Unknown)
        {
            if (knower == null) return;
            var evidence = new ContactEvidence(ContactEvidenceSource.Sensor,
                sensorId, confidence, uncertainty, CommunicationChannel.None,
                knower.thingIDNumber);
            NoteEvidence(knower, hostileId, cell, sourceTick, evidence, label,
                weaponCategory);
        }

        private static bool EvidenceIsBetter(ContactEvidence candidate,
            ContactEvidence current)
        {
            if (candidate.IsDirect != current.IsDirect) return candidate.IsDirect;
            if (candidate.Confidence != current.Confidence)
                return candidate.Confidence > current.Confidence;
            if (candidate.Uncertainty != current.Uncertainty)
                return candidate.Uncertainty < current.Uncertainty;
            return (int)candidate.Source < (int)current.Source;
        }

        // Damage records the engine's actual instigator. Projectile launchers from
        // manned weapons are already the ManningPawn; a genuinely non-pawn hostile
        // remains a non-pawn source instead of being reassigned to a nearby pawn.
        public void NoteDamageFrom(Pawn victim, Thing instigator)
        {
            if (victim == null || instigator == null) return;
            if (!GenHostility.HostileTo(instigator, victim)) return;
            if (instigator.MapHeld != map) return;
            IntVec3 sourceCell = instigator.PositionHeld;
            if (!sourceCell.IsValid || !sourceCell.InBounds(map)) return;

            bool pawnSource = instigator is Pawn;
            var evidence = new ContactEvidence(ContactEvidenceSource.Damage,
                instigator.thingIDNumber, pawnSource ? 0.92f : 0.70f,
                pawnSource ? 1.5f : 4f, CommunicationChannel.None,
                victim.thingIDNumber);
            NoteEvidence(victim, instigator.thingIDNumber, sourceCell,
                Find.TickManager.TicksGame, evidence, instigator.LabelShort,
                ContactWeaponCategory.Unknown);

            // Record the strike when combat knowledge is recorded. The act
            // record states circumstances only; political beliefs judge it later.
            // Witnesses and reports retain their own knowledge source.
            if (pawnSource && CAViolenceSite.BothPeople(victim,
                (Pawn)instigator))
            {
                Pawn striker = (Pawn)instigator;
                // The site DESCRIBES what it can see, in the same
                // words the death seam uses. It asks whether the
                // victim was ALREADY down rather than whether they
                // are down now: this postfix runs after the engine
                // has applied this very blow's knockdown, so reading
                // Downed directly would report every ordinary
                // knockdown as striking the fallen.
                int now = Find.TickManager.TicksGame;
                CAActLedger.Current?.Emit("violence",
                    CAViolenceSite.OrgKeyOf(striker),
                    striker.thingIDNumber, victim.thingIDNumber,
                    striker.LabelShort + " struck "
                    + victim.LabelShort, map, victim.PositionHeld,
                    forceUsed: true,
                    circumstance: CAViolenceSite.Describe(victim,
                        striker,
                        CAViolenceSite.WasAlreadyDown(victim, now)));
            }
        }

        public override void MapComponentTick()
        {
            var s = AwarenessMod.Settings;
            if (s == null) return;

            if (--observeCooldown <= 0)
            {
                observeCooldown = 30;
                if (s.knowledgeContacts)
                {
                    try { Observe(); }
                    catch (System.Exception ex)
                    {
                        ReportKnowledgeFailure("contact observation", ex,
                            ref contactObserveFailureReported, 104923741);
                    }
                }
                try
                {
                    ObserveWelfare();
                }
                catch (System.Exception ex)
                {
                    ReportKnowledgeFailure("welfare observation", ex,
                        ref welfareObserveFailureReported, 104923742);
                }
            }
            if (--relayCooldown <= 0)
            {
                relayCooldown = 60;
                if (s.knowledgeContacts)
                {
                    try { Propagate(); }
                    catch (System.Exception ex)
                    {
                        ReportKnowledgeFailure("contact relay", ex,
                            ref contactRelayFailureReported, 104923743);
                    }
                }
                try
                {
                    PropagateWelfare();
                }
                catch (System.Exception ex)
                {
                    ReportKnowledgeFailure("welfare relay", ex,
                        ref welfareRelayFailureReported, 104923744);
                }
                try { Expire(); }
                catch (System.Exception ex)
                {
                    ReportKnowledgeFailure("contact expiry", ex,
                        ref contactExpiryFailureReported, 104923745);
                }
                try { ExpireWelfare(); }
                catch (System.Exception ex)
                {
                    ReportKnowledgeFailure("welfare expiry", ex,
                        ref welfareExpiryFailureReported, 104923746);
                }
            }
            try { TickAccountability(); }
            catch (System.Exception ex)
            {
                ReportKnowledgeFailure("accountability", ex,
                    ref accountabilityFailureReported, 104923747);
            }
        }

        private void ReportKnowledgeFailure(string lane,
            System.Exception exception, ref bool traceReported, int logKey)
        {
            Log.ErrorOnce("[Colonist Awareness] " + lane
                + " failed on map " + map.uniqueID + ": " + exception,
                logKey);
            if (traceReported) return;
            traceReported = true;
            CATrace.Log(lane + " FAILED on map " + map.uniqueID + " - "
                + exception.GetType().Name + ": " + exception.Message);
        }

        // Firsthand sight: any awake humanlike combatant can see a pawn that is
        // actually hostile to them. No faction receives privileged map truth.
        private void Observe()
        {
            var all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (!IsAwakeHumanlike(p)) continue;
                if (!p.IsColonistPlayerControlled
                    && p.WorkTagIsDisabled(WorkTags.Violent)) continue;
                List<Contact> facts;
                if (known.TryGetValue(p.thingIDNumber, out facts))
                    RefreshObservedStates(p, facts, Find.TickManager.TicksGame);
                for (int j = 0; j < all.Count; j++)
                {
                    var h = all[j];
                    // Existing CA concealment remains an epistemic gate. Ordinary
                    // battlefield perception is now independent of weapon range and
                    // resolves hard structure, soft flora/cover, smoke, weather,
                    // light, eyesight, and bounded skill contribution in one place.
                    CAVisualPerception perception;
                    if (!CABattlefieldPerception.TryObserveHostile(p, h,
                            out perception)) continue;
                    NoteVisualPerception(p, h, perception);
                }
            }
        }

        // The alarm spreads over the one shared knowledge-channel contract. Every
        // delivery preserves the source cell and tick and records its immediate
        // Voice/Radio/Mental edge and reporter.
        private void Propagate()
        {
            var all = map.mapPawns.AllPawnsSpawned;
            int now = Find.TickManager.TicksGame;
            var deliveries = new List<ContactDelivery>();
            for (int i = 0; i < all.Count; i++)
            {
                var teller = all[i];
                // The dead, the downed, and the ASLEEP don't shout.
                if (!IsAwakeHumanlike(teller) || teller.Faction == null
                    || !KnowsAnyThreat(teller)) continue;
                List<Contact> facts = known[teller.thingIDNumber];
                // Snapshot the teller's live facts ONCE - the relay passes the
                // remembered (cell, tick), never the live pawn.
                var fresh = new List<Contact>();
                for (int f = 0; f < facts.Count; f++)
                    if (now - facts[f].tick <= StaleTicks
                        && facts[f].state == ThreatContactState.Active)
                        fresh.Add(facts[f]);
                if (fresh.Count == 0) continue;

                for (int j = 0; j < all.Count; j++)
                {
                    var listener = all[j];
                    if (listener == teller || !IsAwakeHumanlike(listener)) continue;
                    CommunicationChannel routineChannel;
                    CommunicationChannel strategicChannel;
                    bool routineLinked = CommsModule.TryGetKnowledgeChannel(teller,
                        listener, ShoutRange, out routineChannel);
                    bool strategicLinked = CommsModule.TryGetStrategicChannel(teller,
                        listener, ShoutRange, out strategicChannel);
                    if (!routineLinked && !strategicLinked) continue;
                    for (int k = 0; k < fresh.Count; k++)
                    {
                        // A firsthand identified hostile is the current mayday: it
                        // may use the member-to-squad-leader strategic line instead
                        // of waiting for routine fire-team relays. Once told, the
                        // same fact is no longer direct and resumes routine routing.
                        bool direct = fresh[k].evidence.IsDirect;
                        bool linked = direct ? strategicLinked : routineLinked;
                        if (!linked) continue;
                        CommunicationChannel channel = direct
                            ? strategicChannel : routineChannel;
                        deliveries.Add(new ContactDelivery
                        {
                            listener = listener,
                            hostileId = fresh[k].hostileId,
                            cell = fresh[k].lastKnown,
                            sourceTick = fresh[k].tick,
                            weaponCategory = fresh[k].weaponCategory,
                            evidence = fresh[k].evidence.RelayedThrough(channel,
                                teller.thingIDNumber)
                        });
                    }
                }
            }

            // Apply after every teller has been sampled. A report can travel one
            // communication edge per 60-tick relay pass, never an entire pawn-order
            // chain inside one loop merely because of list ordering.
            for (int i = 0; i < deliveries.Count; i++)
            {
                var delivery = deliveries[i];
                NoteEvidence(delivery.listener, delivery.hostileId, delivery.cell,
                    delivery.sourceTick, delivery.evidence, "a contact",
                    delivery.weaponCategory);
            }
        }

        private static bool IsAwakeHumanlike(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed
                && pawn.RaceProps != null && pawn.RaceProps.Humanlike && pawn.Awake();
        }

        private static bool CanUseVision(Pawn pawn)
        {
            return IsAwakeHumanlike(pawn)
                && !PawnUtility.IsBiologicallyOrArtificiallyBlind(pawn);
        }

        private void RefreshObservedStates(Pawn knower, List<Contact> contacts,
            int now)
        {
            if (IsAwakeHumanlike(knower) && knower.Map == map)
            {
                for (int i = 0; i < contacts.Count; i++)
                {
                    Contact contact = contacts[i];

                    Pawn subject;
                    Thing visibleThing;
                    if (TryFindKnownPawn(contact, out subject, out visibleThing))
                    {
                        if (!CanCurrentlyObserveKnownPawn(knower, subject,
                                visibleThing)) continue;
                        contact.directlyObservedPawn = subject;
                        SetObservedState(knower, contact, subject,
                            visibleThing.Position, ObservedState(knower, subject),
                            now);
                        continue;
                    }

                    // A directly observed pawn can later be destroyed without leaving
                    // a corpse. Treat that as observed only when the destruction cell
                    // is still the knower's last-known cell and remains in direct sight.
                    // The retained object reference alone is never map-truth authority.
                    subject = contact.directlyObservedPawn;
                    if (subject != null && subject.Destroyed
                        && subject.Position == contact.lastKnown
                        && CanCurrentlyObserveCell(knower, contact.lastKnown,
                            subject))
                        SetObservedState(knower, contact, subject,
                            contact.lastKnown, ThreatContactState.Destroyed, now);
                }
            }
            ReconcileContactStatesFromWelfare(knower, contacts, now);
        }

        private bool TryFindKnownPawn(Contact contact, out Pawn pawn,
            out Thing visibleThing)
        {
            pawn = null;
            visibleThing = null;

            Pawn direct = contact.directlyObservedPawn;
            if (direct != null && direct.thingIDNumber == contact.hostileId)
            {
                if (direct.Spawned && direct.Map == map)
                {
                    pawn = direct;
                    visibleThing = direct;
                    return true;
                }
                Corpse corpse = direct.Corpse;
                if (corpse != null && corpse.Spawned && corpse.Map == map)
                {
                    pawn = direct;
                    visibleThing = corpse;
                    return true;
                }
            }

            var all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].thingIDNumber != contact.hostileId) continue;
                pawn = all[i];
                visibleThing = all[i];
                return true;
            }

            List<Thing> corpses = map.listerThings.ThingsInGroup(
                ThingRequestGroup.Corpse);
            for (int i = 0; i < corpses.Count; i++)
            {
                Corpse corpse = corpses[i] as Corpse;
                if (corpse == null || corpse.InnerPawn == null
                    || corpse.InnerPawn.thingIDNumber != contact.hostileId) continue;
                pawn = corpse.InnerPawn;
                visibleThing = corpse;
                return true;
            }
            return false;
        }

        private static bool CanCurrentlyObserveKnownPawn(Pawn observer,
            Pawn subject, Thing visibleThing)
        {
            return visibleThing != null && visibleThing.Spawned
                && visibleThing.Map == observer.Map
                && CanCurrentlyObserveCell(observer, visibleThing.Position, subject);
        }

        private static bool CanCurrentlyObserveCell(Pawn observer, IntVec3 cell,
            Pawn subject)
        {
            CAVisualPerception perception;
            Thing visible = subject != null && subject.Spawned
                ? (Thing)subject : subject?.Corpse;
            return visible != null && visible.Position == cell
                && CABattlefieldPerception.TryObserveVisibleThing(observer,
                    visible, subject, float.PositiveInfinity, out perception);
        }

        private static ThreatContactState ObservedState(Pawn observer,
            Pawn subject)
        {
            if (subject.Destroyed) return ThreatContactState.Destroyed;
            if (subject.Dead) return ThreatContactState.Dead;
            if (subject.IsPrisoner) return ThreatContactState.Captured;
            if (subject.Downed) return ThreatContactState.Downed;
            if (!subject.HostileTo(observer)) return ThreatContactState.Nonhostile;
            if (!AnimalThreatRelevance.IsRelevantContact(observer, subject))
                return ThreatContactState.Nonthreatening;
            return ThreatContactState.Active;
        }

        private static void SetObservedState(Pawn knower, Contact contact,
            Pawn subject, IntVec3 cell, ThreatContactState state, int now)
        {
            if (contact.state != ThreatContactState.Active
                && state == ThreatContactState.Active)
                contact.acquiredTick = now;
            contact.lastKnown = cell;
            contact.tick = now;
            contact.state = state;
            contact.stateTick = now;
            contact.evidence = new ContactEvidence(ContactEvidenceSource.Visual,
                contact.hostileId, 1f, 0f, CommunicationChannel.None,
                knower.thingIDNumber);
            contact.weaponCategory = VisibleWeaponCategory(subject);
        }

        // One observation predicate for stored firsthand contacts, disabled-mode
        // immediate perception, and live-pawn resolution from remembered contacts.
        // Geometric LOS alone is not sight: sleeping observers and concealed targets
        // remain epistemically unavailable.
        internal static bool CanCurrentlySeeHostile(Pawn observer, Pawn hostile)
        {
            CAVisualPerception perception;
            return CABattlefieldPerception.TryObserveHostile(observer,
                hostile, out perception);
        }

        // A bounded caller may still ask about one local envelope. The bound limits
        // the shared perception resolver; it no longer substitutes raw radius + LOS
        // for environmental awareness.
        internal static bool CanCurrentlySeeHostile(Pawn observer, Pawn hostile,
            float radius)
        {
            CAVisualPerception perception;
            return CABattlefieldPerception.TryObserveHostile(observer,
                hostile, radius, out perception);
        }

        private static ContactWeaponCategory VisibleWeaponCategory(Pawn hostile)
        {
            if (hostile == null || hostile.equipment == null)
                return ContactWeaponCategory.Unknown;
            var weapon = hostile.equipment.Primary;
            if (weapon == null) return ContactWeaponCategory.Unarmed;
            if (weapon.def.IsRangedWeapon) return ContactWeaponCategory.Ranged;
            if (weapon.def.IsMeleeWeapon) return ContactWeaponCategory.Melee;
            return ContactWeaponCategory.Unknown;
        }

        private void Expire()
        {
            int now = Find.TickManager.TicksGame;
            var dead = new List<int>();
            foreach (var kv in known)
            {
                kv.Value.RemoveAll(c => now - c.tick > StaleTicks);
                if (kv.Value.Count == 0) dead.Add(kv.Key);
            }
            for (int i = 0; i < dead.Count; i++) known.Remove(dead[i]);
        }
    }

    // The damage seam: taking a hit tells you, unmistakably, that someone is out
    // there - the one channel that reaches through sight range (snipers, sieges).
    public static class KnowledgePatch
    {
        public static void TryInstall(HarmonyLib.Harmony harmony)
        {
            try
            {
                var target = HarmonyLib.AccessTools.Method(
                    typeof(Pawn_MindState), "Notify_DamageTaken");
                harmony.Patch(target, postfix: new HarmonyLib.HarmonyMethod(
                    typeof(KnowledgePatch), nameof(DamagePostfix)));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] knowledge damage seam stood down: " + e.Message);
            }
        }

        public static void DamagePostfix(Pawn_MindState __instance, DamageInfo dinfo)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.knowledgeContacts) return;
                var victim = __instance.pawn;
                if (victim == null || victim.Map == null || victim.RaceProps == null
                    || !victim.RaceProps.Humanlike) return;
                if (dinfo.Instigator == null) return;
                var know = KnowledgeMapComponent.For(victim.Map);
                if (know != null) know.NoteDamageFrom(victim, dinfo.Instigator);
            }
            catch { }
        }
    }
}
