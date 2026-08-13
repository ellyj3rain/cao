using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module: squad mutual support. Squad and fire-team membership becomes
    // BEHAVIOR: when a squadmate's position folds, a capable teammate covers
    // the withdrawal; when a rescuer runs into a contested field, a teammate
    // escorts with support-by-fire (escort-not-veto - the rescue itself is
    // never blocked here); when known threats close on painted objectives,
    // free Autonomous fighters anchor on them; and threat-aware idle
    // Autonomous fighters man the painted defensive line without a human.
    // Taskings ride the automatic-defense vehicle (non-sovereign, releasable,
    // duty-driven); every tasking, refusal, and release is receipted.
    public class SquadSupportMapComponent : MapComponent
    {
        private readonly struct OwnedThreatContact
        {
            internal readonly Pawn Owner;
            internal readonly ThreatContactSnapshot Contact;

            internal OwnedThreatContact(Pawn owner,
                ThreatContactSnapshot contact)
            {
                Owner = owner;
                Contact = contact;
            }
        }

        private readonly struct SupportKnowledgeEvidence
        {
            internal readonly bool Satisfied;
            internal readonly bool Fresh;
            internal readonly bool Relayed;
            internal readonly bool LiveValidated;
            internal readonly int AgeTicks;
            internal readonly float Confidence;
            internal readonly float Uncertainty;
            internal readonly string Basis;
            internal readonly ThreatContactSnapshot? Contact;

            internal SupportKnowledgeEvidence(bool satisfied, bool fresh,
                bool relayed, bool liveValidated, int ageTicks,
                float confidence, float uncertainty, string basis,
                ThreatContactSnapshot? contact = null)
            {
                Satisfied = satisfied;
                Fresh = fresh;
                Relayed = relayed;
                LiveValidated = liveValidated;
                AgeTicks = Math.Max(0, ageTicks);
                Confidence = Mathf.Clamp01(confidence);
                Uncertainty = Math.Max(0f, uncertainty);
                Basis = basis;
                Contact = contact;
            }

            internal static SupportKnowledgeEvidence Missing =>
                new SupportKnowledgeEvidence(false, false, false, false,
                    int.MaxValue, 0f, 1f,
                    "no fact held or delivered to the executing pawn");
        }

        private const int PassInterval = 90;
        private const int TaskingLifetimeTicks = 1800;
        private const int RescueEscortCooldownTicks = 1200;
        private const int ObjectiveCooldownTicks = 900;
        private const int LineStandDownTicks = 2500;

        private int cooldown;

        private sealed class SupportTasking : IExposable
        {
            public int PawnId;
            public int IssuedTick;
            public int ExpiryTick;
            public string Reason;
            public int SupportedId;
            public string BehaviorKey;
            public int EpisodeId;
            public int AuthorityOrigin;
            public string AuthorityIdentity;
            public string OwnershipScope;
            public string TerminationCondition;

            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnId, "pawnId", -1);
                Scribe_Values.Look(ref IssuedTick, "issuedTick");
                Scribe_Values.Look(ref ExpiryTick, "expiryTick");
                Scribe_Values.Look(ref Reason, "reason");
                Scribe_Values.Look(ref SupportedId, "supportedId", -1);
                Scribe_Values.Look(ref BehaviorKey, "behaviorKey");
                Scribe_Values.Look(ref EpisodeId, "episodeId");
                Scribe_Values.Look(ref AuthorityOrigin, "authorityOrigin");
                Scribe_Values.Look(ref AuthorityIdentity, "authorityIdentity");
                Scribe_Values.Look(ref OwnershipScope, "ownershipScope");
                Scribe_Values.Look(ref TerminationCondition,
                    "terminationCondition");
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                    CACombatIntent.ObserveEpisode(EpisodeId);
            }
        }

        // The native tactical lord or native medical job executes the action;
        // this compact row preserves why CA owns it and when that ownership
        // ends. It is scribed so save/load does not erase authority identity.
        private List<SupportTasking> taskings = new List<SupportTasking>();
        private readonly Dictionary<int, int> rescueEscortCooldown =
            new Dictionary<int, int>();
        private readonly Dictionary<int, int> objectiveCooldown =
            new Dictionary<int, int>();
        private readonly Dictionary<int, int> selfAssignedLine =
            new Dictionary<int, int>();
        private readonly Dictionary<int, int> lastThreatTick =
            new Dictionary<int, int>();
        private readonly Dictionary<int, int> rerouteCooldown =
            new Dictionary<int, int>();

        // Task elasticity's visible half: tactical exchanges between squad
        // members surface as brief overhead text at both ends. Clinical
        // language by design - the point is that communication is LEGIBLE,
        // not dramatic.
        private static void VisibleExchange(Pawn from, Pawn to, string text)
        {
            try
            {
                if (from != null && from.Spawned)
                    MoteMaker.ThrowText(from.DrawPos, from.Map,
                        "→ " + (to != null ? to.LabelShort : "squad")
                        + ": " + text, 3.65f);
                if (to != null && to.Spawned && to != from)
                    MoteMaker.ThrowText(to.DrawPos, to.Map,
                        (from != null ? from.LabelShort : "squad")
                        + ": " + text, 3.65f);
            }
            catch { }
        }

        public SquadSupportMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cooldown, "behaviorPassCooldown");
            Scribe_Collections.Look(ref taskings, "ownedSupportIntents",
                LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && taskings == null)
                taskings = new List<SupportTasking>();
        }

        public static SquadSupportMapComponent For(Map map)
        {
            return map?.GetComponent<SquadSupportMapComponent>();
        }

        // ---- fold cover: called by the hold supervisor at deviation time ----

        public void NotifyPositionFold(Pawn folder, IntVec3 origin, IntVec3 dest)
        {
            if (folder == null || folder.Map != map) return;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (!CABehaviorSettings.IsEnabled(CABehaviorCatalog.Get(
                    "support.fold_cover"), settings)) return;
            int squad = SquadComponent.SquadOf(folder);
            Pawn best = null;
            float bestScore = float.MaxValue;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn candidate = colonists[i];
                if (candidate == folder) continue;
                if (SquadComponent.SquadOf(candidate) != squad) continue;
                if (!CABehaviorGate.StableProfileAllows(candidate,
                        "support.fold_cover")) continue;
                string refusal;
                float range;
                if (!FreeForSupport(candidate, out range, out refusal)) continue;
                if (!CommsAware(candidate, folder)) continue;
                float d = candidate.Position.DistanceTo(origin);
                if (d >= bestScore) continue;
                best = candidate;
                bestScore = d;
            }
            if (best == null)
            {
                CATrace.Pawn(folder,
                    "squad support UNAVAILABLE for the fold - no free capable "
                    + "squadmate in comms reach", anchor: folder.Position);
                return;
            }
            SupportKnowledgeEvidence requestEvidence;
            if (!TryDeliveredRequestEvidence(folder, best,
                    "withdrawal-cover request from " + folder.LabelShort,
                    out requestEvidence)) return;
            IssueTasking(best, origin, folder,
                "covering " + folder.LabelShort + "'s withdrawal",
                behaviorKey: "support.fold_cover",
                authorityOrigin: CAAuthorityOrigin.PeerRequest,
                authorityIdentity: "squadmate withdrawal request",
                moteText: "cover my withdrawal",
                explicitEvidence: requestEvidence);
        }

        // -------------------------- periodic passes --------------------------

        public override void MapComponentTick()
        {
            if (--cooldown > 0) return;
            cooldown = PassInterval;
            var settings = AwarenessMod.Settings;
            if (settings == null) return;
            try
            {
                ExpireTaskings();
                if (CABehaviorSettings.IsEnabled(CABehaviorCatalog.Get(
                        "support.rescue_escort"), settings))
                    RescueEscortPass();
                if (CABehaviorSettings.IsEnabled(CABehaviorCatalog.Get(
                        "support.objective_defense"), settings))
                {
                    ObjectiveDefensePass();
                    DefensiveLinePass();
                    RallyReroutePass();
                    FlankGuardPass();
                }
                if (CABehaviorSettings.IsEnabled(CABehaviorCatalog.Get(
                        "welfare.medic_dispatch"), settings))
                    MedicResponsePass();
                if (CABehaviorSettings.IsEnabled(CABehaviorCatalog.Get(
                        "communication.command_delivery"), settings))
                    OrderMenus.CAPendingRelay.Process(map);
            }
            catch { }
        }

        // Delegation: a known casualty must end up in SOMEBODY's capable
        // hands. Knowledge relays spread the fact; this pass closes the loop
        // by ASSIGNING it - the best free medic who personally knows the
        // casualty takes the tend (in place - beds are a comfort, tending is
        // survival) or the rescue-to-any-bed. Epistemics preserved: only a
        // medic whose own knowledge carries the fact responds. Early-game
        // bed scarcity never blocks the tend branch.
        private readonly Dictionary<int, int> medicResponseCooldown =
            new Dictionary<int, int>();

        private void MedicResponsePass()
        {
            var know = KnowledgeMapComponent.For(map);
            if (know == null) return;
            int now = Find.TickManager.TicksGame;
            var colonists = map.mapPawns.FreeColonistsSpawned;

            // Best medics first: skill then proximity decided per casualty.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < colonists.Count; i++)
                {
                    Pawn medic = colonists[i];
                    if (medic.Downed || medic.Dead || medic.InMentalState
                        || medic.Drafted) continue;
                    if (medic.WorkTagIsDisabled(WorkTags.Caring)) continue;
                    int skill = medic.skills != null
                        ? medic.skills.GetSkill(SkillDefOf.Medicine).Level : 0;
                    // First pass: trained hands (4+). Second: anyone caring.
                    if (pass == 0 && skill < 4) continue;
                    if (pass == 1 && skill >= 4) continue;
                    Job current = medic.CurJob;
                    if (HasPlayerOwnedWork(medic)) continue;
                    if (current != null && (current.def == JobDefOf.TendPatient
                        || current.def == JobDefOf.Rescue
                        || current.def == CA_Defs.CombatRecovery
                        || current.def == CA_Defs.EmergencySelfTend
                        || current.def == CA_Defs.FightingWithdrawal))
                        continue;
                    if (CACombatThreat.PerceivesActiveThreat(medic)) continue;

                    Job answer;
                    Pawn casualty;
                    if (!CACombatAftermath.TryBuildPriorityRescueJob(medic,
                        know, out answer, out casualty)) continue;
                    // DOCTRINE: care under fire - SUPPRESS, MOVE, TREAT, in
                    // that order, always. A contested casualty zone (known
                    // hostile within 26) gets NO mover until suppression is
                    // on the threat; if none exists, the FIRST tasking this
                    // pass is the suppressor, and the crossing waits.
                    IntVec3 contestedThreat;
                    int contestedId;
                    ThreatContactSnapshot contestedFact;
                    Pawn contestedFactOwner;
                    if (CasualtyZoneContested(casualty, medic,
                        out contestedThreat, out contestedId, out contestedFact,
                        out contestedFactOwner))
                    {
                        if (!SuppressionOnThreat(contestedId))
                        {
                            ThreatContactSnapshot suppressorFact;
                            string suppressorKnowledgeOwner;
                            Pawn suppressor = BestSupporterForFact(casualty,
                                -1, contestedThreat, "support.rescue_escort",
                                contestedFactOwner, contestedFact,
                                out suppressorFact,
                                out suppressorKnowledgeOwner);
                            if (suppressor != null && suppressor != medic)
                            {
                                IssueTasking(suppressor, contestedThreat,
                                    casualty, "care under fire - suppression "
                                    + "BEFORE the crossing to "
                                    + casualty.LabelShort,
                                    behaviorKey: "support.rescue_escort",
                                    authorityOrigin:
                                        CAAuthorityOrigin.PeerRequest,
                                    authorityIdentity:
                                        "casualty crossing support request",
                                    moteText: "suppress - casualty crossing",
                                    knowledgeFact: suppressorFact,
                                    knowledgeOwner:
                                        suppressorKnowledgeOwner);
                            }
                            int lastHold;
                            if (!medicResponseCooldown.TryGetValue(
                                    -casualty.thingIDNumber - 1000000,
                                    out lastHold)
                                || now - lastHold > 2500)
                            {
                                medicResponseCooldown[
                                    -casualty.thingIDNumber - 1000000] = now;
                                CATrace.Pawn(medic, "casualty response HELD "
                                    + "for " + casualty.LabelShort
                                    + " - suppression first; no rescuer "
                                    + "crosses effective fire uncovered",
                                    contact: contestedThreat,
                                    anchor: medic.Position);
                            }
                            continue;
                        }
                        CATrace.Pawn(medic, "crossing to "
                            + casualty.LabelShort
                            + " UNDER SUPPRESSION - the threat is being "
                            + "engaged; move and treat follow",
                            contact: contestedThreat, anchor: medic.Position);
                    }
                    // A live battle changes who may answer: a violence-
                    // incapable civilian is never sent across a battlefield -
                    // short, local responses only, and only when no known
                    // contact stands near their path. Only the medic's own
                    // fresh contacts decide whether that crossing is unsafe.
                    if (medic.WorkTagIsDisabled(WorkTags.Violent)
                        && ColonyBattleActive(medic))
                    {
                        float run = medic.Position.DistanceTo(
                            casualty.Position);
                        if (run > 25f
                            || ContactNearPath(medic, medic.Position,
                                casualty.Position, 30f))
                        {
                            int lastHeld;
                            if (!medicResponseCooldown.TryGetValue(
                                    -casualty.thingIDNumber, out lastHeld)
                                || now - lastHeld > 2500)
                            {
                                medicResponseCooldown[
                                    -casualty.thingIDNumber] = now;
                                CATrace.Pawn(medic, "medical response "
                                    + "WITHHELD - a civilian does not cross "
                                    + "a live battlefield ("
                                    + run.ToString("0") + " cells to "
                                    + casualty.LabelShort
                                    + "); awaiting capable hands or safe "
                                    + "range", anchor: medic.Position);
                            }
                            continue;
                        }
                    }
                    int last;
                    if (medicResponseCooldown.TryGetValue(
                            casualty.thingIDNumber, out last)
                        && now - last < 900) continue;
                    bool alreadyHandled = false;
                    for (int j = 0; j < colonists.Count; j++)
                    {
                        Job other = colonists[j].CurJob;
                        if (other == null) continue;
                        if ((other.def == JobDefOf.TendPatient
                                || other.def == JobDefOf.Rescue
                                || other.def == answer.def)
                            && other.targetA.Thing == casualty)
                        { alreadyHandled = true; break; }
                    }
                    if (alreadyHandled) continue;
                    bool playerOwnedWork = HasPlayerOwnedWork(medic);
                    CABehaviorContext gateContext =
                        CABehaviorContext.ForPawn(medic,
                            CAAuthorityOrigin.PlayerDelegated,
                            authoritySatisfied: true,
                            knowledgeSatisfied: true,
                            knowledgeFresh: true,
                            liveValidated: casualty.Spawned
                                && casualty.Downed,
                            capabilitySatisfied: answer != null,
                            currentIntentCompatible: !playerOwnedWork,
                            directPlayerOwnership: playerOwnedWork,
                            authorityBasis:
                                "player-delegated field-medicine duty",
                            knowledgeBasis:
                                "fresh casualty fact held by assigned medic",
                            owner: "medical response");
                    CABehaviorDecision medicalDecision;
                    CAIntentContext medicalIntent;
                    if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(medic,
                        answer, "welfare.medic_dispatch",
                        CAIntentController.Welfare, gateContext,
                        out medicalDecision, out medicalIntent,
                        targetOrDemand: "care for "
                            + casualty.LabelShort,
                        ownershipScope: "medical response",
                        lifetimeTicks: TaskingLifetimeTicks))
                    {
                        CATrace.Pawn(medic, "medical response BLOCKED - "
                            + medicalDecision.PrimaryReason,
                            destination: casualty.Position,
                            anchor: medic.Position);
                        continue;
                    }
                    medicResponseCooldown[casualty.thingIDNumber] = now;
                    RegisterTasking(medic, casualty, "medical response for "
                        + casualty.LabelShort, medicalIntent);
                    answer.playerForced = false;
                    medic.jobs.StartJob(answer,
                        JobCondition.InterruptOptional);
                    VisibleExchange(casualty, medic, "needs hands - on it");
                    CATrace.Pawn(medic, "medical response TASKED - "
                        + casualty.LabelShort + " ("
                        + (answer.def == JobDefOf.Rescue
                            ? "carry to bed" : "tend in place")
                        + "; medicine " + skill + ")",
                        destination: casualty.Position,
                        anchor: medic.Position, intent: medicalIntent);
                    return; // one assignment per pass keeps this gentle
                }
            }
        }

        // Due-diligence preemption: a relayed sighting (a hiding noncombatant
        // reporting raiders on a second axis) must produce COVERAGE, not just
        // knowledge - otherwise the held position gets enveloped because
        // nobody acted on the report. Contacts are clustered per approach
        // axis toward home ground; an axis no armed colonist stands near
        // draws one flank guard onto a covered position serving it (painted
        // defensive line preferred when one crosses the axis). Capped at two
        // guards so preemption never strips the main line.
        private void FlankGuardPass()
        {
            var know = KnowledgeMapComponent.For(map);
            var home = map.areaManager?.Home;
            if (know == null || home == null || home.TrueCount == 0) return;
            int active = 0;
            for (int t = 0; t < taskings.Count; t++)
                if (taskings[t].Reason != null
                    && taskings[t].Reason.StartsWith("flank guard"))
                    active++;
            if (active >= 2) return;
            int now = Find.TickManager.TicksGame;

            // Union threat picture across the roster (relays included by the
            // knowledge lane itself), newest fact per hostile.
            var colonists = map.mapPawns.FreeColonistsSpawned;
            var newest = new Dictionary<int, OwnedThreatContact>();
            for (int i = 0; i < colonists.Count; i++)
            {
                var contacts = know.FreshContacts(colonists[i]);
                for (int c = 0; c < contacts.Count; c++)
                {
                    var contact = contacts[c];
                    if (!contact.Cell.IsValid) continue;
                    OwnedThreatContact old;
                    if (!newest.TryGetValue(contact.HostileId, out old)
                        || contact.SourceTick > old.Contact.SourceTick)
                        newest[contact.HostileId] = new OwnedThreatContact(
                            colonists[i], contact);
                }
            }
            if (newest.Count == 0) return;
            IntVec3 homeCentroid = HomeCentroid(home);
            if (!homeCentroid.IsValid) return;

            // Greedy 20-cell clusters.
            var clusterCells = new List<IntVec3>();
            var clusterCounts = new List<int>();
            var clusterRelayed = new List<bool>();
            var clusterEvidence = new List<List<OwnedThreatContact>>();
            foreach (OwnedThreatContact owned in newest.Values)
            {
                ThreatContactSnapshot contact = owned.Contact;
                bool merged = false;
                for (int k = 0; k < clusterCells.Count; k++)
                {
                    if (!contact.Cell.InHorDistOf(clusterCells[k], 20f))
                        continue;
                    clusterCounts[k]++;
                    if (!contact.DirectlyAcquired) clusterRelayed[k] = true;
                    clusterEvidence[k].Add(owned);
                    merged = true;
                    break;
                }
                if (!merged)
                {
                    clusterCells.Add(contact.Cell);
                    clusterCounts.Add(1);
                    clusterRelayed.Add(!contact.DirectlyAcquired);
                    clusterEvidence.Add(new List<OwnedThreatContact>
                        { owned });
                }
            }

            var overlay = CAOverlayMapComponent.For(map);
            for (int k = 0; k < clusterCells.Count && active < 2; k++)
            {
                IntVec3 threat = clusterCells[k];
                int clusterKey = (threat.x / 20) * 100000 + threat.z / 20;
                int last;
                if (objectiveCooldown.TryGetValue(-clusterKey, out last)
                    && now - last < 1500) continue;
                // Covered axis: any armed adult standing near the approach
                // segment already answers it.
                if (AxisCovered(colonists, threat, homeCentroid)) continue;
                objectiveCooldown[-clusterKey] = now;

                ThreatContactSnapshot guardFact;
                Pawn sourceOwner;
                Pawn guard = BestInformedSupporter(clusterEvidence[k],
                    threat, "support.flank_guard", out guardFact,
                    out sourceOwner);
                if (guard == null)
                {
                    CATrace.Log("flank guard UNAVAILABLE - "
                        + clusterCounts[k] + " contact(s) on an uncovered "
                        + "axis at " + threat + " and no free capable fighter");
                    continue;
                }
                IntVec3 anchorHint = IntVec3.Invalid;
                if (overlay != null
                    && overlay.HasLayer(CAOverlayKind.DefensiveLine))
                    anchorHint = overlay.NearestCell(
                        CAOverlayKind.DefensiveLine, threat, 40f);
                if (!anchorHint.IsValid)
                    anchorHint = SegmentPoint(threat, homeCentroid, 20f);
                IssueTasking(guard, threat, null,
                    "flank guard - " + clusterCounts[k]
                    + (clusterRelayed[k] ? " relayed" : " direct")
                    + " contact(s) approaching an uncovered axis",
                    anchorHint,
                    behaviorKey: "support.flank_guard",
                    authorityOrigin: CAAuthorityOrigin.PlayerDelegated,
                    authorityIdentity: "delegated regional defense",
                    moteText: "guard the flank",
                    knowledgeFact: guardFact,
                    knowledgeOwner: sourceOwner?.LabelShort);
                active++;
            }
        }

        private static IntVec3 SegmentPoint(IntVec3 from, IntVec3 to,
            float distanceFromTo)
        {
            UnityEngine.Vector3 direction = (from - to).ToVector3();
            if (direction.sqrMagnitude < 1f) return to;
            direction.Normalize();
            return to + IntVec3.FromVector3(direction * distanceFromTo);
        }

        private bool AxisCovered(List<Pawn> colonists, IntVec3 threat,
            IntVec3 homeCentroid)
        {
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p.Downed || p.InMentalState
                    || p.WorkTagIsDisabled(WorkTags.Violent)
                    || p.equipment?.Primary == null) continue;
                if (DistanceToSegment(p.Position, threat, homeCentroid) <= 20f)
                    return true;
            }
            return false;
        }

        private static float DistanceToSegment(IntVec3 point, IntVec3 a,
            IntVec3 b)
        {
            UnityEngine.Vector3 pv = point.ToVector3();
            UnityEngine.Vector3 av = a.ToVector3();
            UnityEngine.Vector3 bv = b.ToVector3();
            UnityEngine.Vector3 ab = bv - av;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 1f)
                return (pv - av).magnitude;
            float t = UnityEngine.Mathf.Clamp01(
                UnityEngine.Vector3.Dot(pv - av, ab) / lengthSquared);
            return (pv - (av + ab * t)).magnitude;
        }

        private static IntVec3 HomeCentroid(Area home)
        {
            long x = 0, z = 0;
            int n = 0;
            foreach (IntVec3 cell in home.ActiveCells)
            {
                x += cell.x;
                z += cell.z;
                n++;
                if (n >= 4096) break;
            }
            return n == 0 ? IntVec3.Invalid
                : new IntVec3((int)(x / n), 0, (int)(z / n));
        }

        // Task elasticity, adaptive half: a standing assembly holds its
        // members through interleaved tasks, and when contact lands while
        // someone is still inbound, the leader reroutes them - the rally
        // point stretches to a covered join oriented against the threat
        // instead of marching a straggler through it. Comms-gated, member-
        // cooldown 900, and only meaningful moves (>3 cells) are ordered.
        private void RallyReroutePass()
        {
            var know = KnowledgeMapComponent.For(map);
            if (know == null) return;
            int now = Find.TickManager.TicksGame;
            var job = CATactical.JobOf(map);
            if (job == null) return;
            var colonists = map.mapPawns.FreeColonistsSpawned;

            // Rally clusters: automatic-defense rows sharing an anchor.
            var members = new List<Pawn>();
            var anchors = new List<IntVec3>();
            for (int i = 0; i < colonists.Count; i++)
            {
                int kind;
                IntVec3 cell, watch;
                if (!job.TryGetOrder(colonists[i], out kind, out cell,
                        out watch)
                    || kind != LordJob_CATactical.KindRaidDefense) continue;
                members.Add(colonists[i]);
                anchors.Add(cell);
            }
            if (members.Count < 2) return;

            for (int i = 0; i < members.Count; i++)
            {
                Pawn straggler = members[i];
                IntVec3 anchor = anchors[i];
                int shared = 0;
                for (int j = 0; j < members.Count; j++)
                    if (j != i && anchors[j].InHorDistOf(anchor, 6.9f))
                        shared++;
                if (shared == 0) continue; // solo tasking, not a rally
                if (straggler.Position.InHorDistOf(anchor, 15f)) continue;
                int last;
                if (rerouteCooldown.TryGetValue(straggler.thingIDNumber,
                        out last) && now - last < 900) continue;

                int squad = SquadComponent.SquadOf(straggler);
                Pawn leader = SquadComponent.LeaderPawn(squad, map);
                if (leader == null || leader == straggler
                    || leader.Dead || leader.Downed) continue;
                CommunicationChannel rerouteChannel;
                if (!CommsModule.TryGetKnowledgeChannel(leader, straggler,
                    11.9f, out rerouteChannel)) continue;
                // Epistemic honesty: mechlink telemetry is the operator-
                // ratified electromagnetic sense of linked members' positions;
                // over radio the leader names the plan and the MEMBER solves
                // their own approach from where they actually stand; voice
                // means they are close enough to see. The receipt says which.
                string positionBasis =
                    rerouteChannel == CommunicationChannel.Mental
                        ? "mechlink telemetry"
                    : rerouteChannel == CommunicationChannel.Voice
                        ? "voice contact"
                        : "radio - member solves own approach";

                // The leader's threat picture decides whether the join must
                // bend: nearest fresh contact to the straggler's direct path.
                var contacts = know.FreshContacts(leader);
                IntVec3 threat = IntVec3.Invalid;
                float nearestThreat = float.MaxValue;
                ThreatContactSnapshot leaderFact =
                    default(ThreatContactSnapshot);
                for (int c = 0; c < contacts.Count; c++)
                {
                    if (!contacts[c].Cell.IsValid) continue;
                    float d = contacts[c].Cell.DistanceTo(anchor);
                    if (d >= 40f || !FactMeetsBehaviorContract(
                            "support.rally_reroute", contacts[c])) continue;
                    float ranked = d + EvidenceRankingPenalty(
                        "support.rally_reroute", contacts[c]);
                    if (ranked < nearestThreat)
                    {
                        nearestThreat = ranked;
                        threat = contacts[c].Cell;
                        leaderFact = contacts[c];
                    }
                }
                if (!threat.IsValid) continue;

                ThreatContactSnapshot stragglerFact;
                string stragglerKnowledgeOwner;
                if (!TryEnsureTaskingFact(know, leader, straggler,
                        leaderFact, out stragglerFact,
                        out stragglerKnowledgeOwner,
                        preferredChannel: rerouteChannel)) continue;

                float range;
                string refusal;
                if (!FreeForSupport(straggler, out range, out refusal))
                {
                    // Mid-prerequisite (forced job, critical lane): the rally
                    // keeps them; the reroute waits for their hands.
                    continue;
                }
                IntVec3 covered = FindFiringPosition(straggler, threat,
                    range, anchor);
                if (!covered.IsValid
                    || covered.InHorDistOf(anchor, 3f)) continue;
                rerouteCooldown[straggler.thingIDNumber] = now;
                CAIntentContext rerouteIntent;
                CABehaviorDecision rerouteDecision;
                if (!TryAuthorizeTasking(straggler,
                        "support.rally_reroute",
                        CAAuthorityOrigin.PlayerDelegated,
                        "squad rally responsibility", threat,
                        out rerouteIntent, out rerouteDecision,
                        stragglerFact, stragglerKnowledgeOwner))
                    continue;
                if (!CATactical.AssignAutomaticDefense(straggler, covered,
                    threat, rerouteIntent)) continue;
                RegisterTasking(straggler, leader,
                    "rally reroute under cover", rerouteIntent);
                VisibleExchange(leader, straggler,
                    "reroute - assemble under cover against the contact");
                CATrace.Pawn(straggler, "rally REROUTED by "
                    + leader.LabelShort + " (" + positionBasis
                    + ") - covered join at " + covered
                    + " against contact at " + threat
                    + " (was " + anchor + ")",
                    destination: covered, contact: threat,
                    anchor: straggler.Position, intent: rerouteIntent);
            }
        }

        private void ExpireTaskings()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = taskings.Count - 1; i >= 0; i--)
            {
                var tasking = taskings[i];
                Pawn p = PawnById(tasking.PawnId);
                bool medical = tasking.BehaviorKey
                    == "welfare.medic_dispatch";
                bool stillOurs = p != null && (medical
                    ? p.CurJob != null && !p.CurJob.playerForced
                    : CATactical.IsAutomaticDefense(p));
                if (p == null || !stillOurs
                    || now > (tasking.ExpiryTick > 0
                        ? tasking.ExpiryTick
                        : tasking.IssuedTick + TaskingLifetimeTicks))
                {
                    if (p != null && stillOurs && !medical)
                    {
                        CATactical.ReleaseAutomaticDefense(p);
                        CATrace.Pawn(p, "squad support RELEASED - "
                            + tasking.Reason, anchor: p.Position);
                    }
                    taskings.RemoveAt(i);
                }
            }
        }

        private void RescueEscortPass()
        {
            int now = Find.TickManager.TicksGame;
            var know = KnowledgeMapComponent.For(map);
            if (know == null) return;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn rescuer = colonists[i];
                Job job = rescuer.CurJob;
                if (job == null || job.def != JobDefOf.Rescue) continue;
                var casualty = job.targetA.Thing as Pawn;
                if (casualty == null || !casualty.Downed) continue;
                int lastTick;
                if (rescueEscortCooldown.TryGetValue(rescuer.thingIDNumber,
                        out lastTick)
                    && now - lastTick < RescueEscortCooldownTicks) continue;

                var contacts = know.FreshContacts(rescuer);
                IntVec3 threatCell = IntVec3.Invalid;
                ThreatContactSnapshot rescuerFact =
                    default(ThreatContactSnapshot);
                int contested = 0;
                for (int c = 0; c < contacts.Count; c++)
                {
                    if (!contacts[c].Cell.IsValid) continue;
                    if (!contacts[c].Cell.InHorDistOf(casualty.Position, 26f))
                        continue;
                    contested++;
                    if (!threatCell.IsValid
                        || contacts[c].Cell.DistanceTo(casualty.Position)
                            < threatCell.DistanceTo(casualty.Position))
                    {
                        threatCell = contacts[c].Cell;
                        rescuerFact = contacts[c];
                    }
                }
                if (contested == 0 || !threatCell.IsValid) continue;
                rescueEscortCooldown[rescuer.thingIDNumber] = now;

                int squad = SquadComponent.SquadOf(rescuer);
                ThreatContactSnapshot escortFact;
                string escortKnowledgeOwner;
                Pawn escort = BestSupporterForFact(rescuer, squad,
                    threatCell, "support.rescue_escort", rescuer,
                    rescuerFact, out escortFact,
                    out escortKnowledgeOwner);
                if (escort == null)
                {
                    CATrace.Pawn(rescuer,
                        "rescue escort UNAVAILABLE - " + contested
                        + " known hostile(s) near the casualty and no free "
                        + "capable supporter", contact: threatCell,
                        anchor: rescuer.Position);
                    continue;
                }
                IssueTasking(escort, threatCell, rescuer,
                    "support-by-fire for " + rescuer.LabelShort
                    + "'s rescue of " + casualty.LabelShort,
                    behaviorKey: "support.rescue_escort",
                    authorityOrigin: CAAuthorityOrigin.PeerRequest,
                    authorityIdentity: "active rescue support request",
                    moteText: "cover the rescue",
                    knowledgeFact: escortFact,
                    knowledgeOwner: escortKnowledgeOwner);
            }
        }

        private void ObjectiveDefensePass()
        {
            var overlay = CAOverlayMapComponent.For(map);
            var know = KnowledgeMapComponent.For(map);
            if (overlay == null || know == null) return;
            bool primary = overlay.HasLayer(CAOverlayKind.PrimaryObjective);
            bool ordinary = overlay.HasLayer(CAOverlayKind.Objective);
            if (!primary && !ordinary) return;
            int now = Find.TickManager.TicksGame;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn fighter = colonists[i];
                if (!CABehaviorGate.StableProfileAllows(fighter,
                        "support.objective_defense")) continue;
                int lastTick;
                if (objectiveCooldown.TryGetValue(fighter.thingIDNumber,
                        out lastTick)
                    && now - lastTick < ObjectiveCooldownTicks) continue;
                string refusal;
                float range;
                if (!FreeForSupport(fighter, out range, out refusal)) continue;

                var contacts = know.FreshContacts(fighter);
                IntVec3 threatCell = IntVec3.Invalid;
                IntVec3 objectiveCell = IntVec3.Invalid;
                ThreatContactSnapshot objectiveFact =
                    default(ThreatContactSnapshot);
                float bestObjectiveEvidence = float.MaxValue;
                for (int c = 0; c < contacts.Count; c++)
                {
                    if (!contacts[c].Cell.IsValid) continue;
                    IntVec3 near = primary
                        ? overlay.NearestCell(CAOverlayKind.PrimaryObjective,
                            contacts[c].Cell, 30f) : IntVec3.Invalid;
                    if (!near.IsValid && ordinary)
                        near = overlay.NearestCell(CAOverlayKind.Objective,
                            contacts[c].Cell, 30f);
                    if (!near.IsValid) continue;
                    if (!FactMeetsBehaviorContract(
                            "support.objective_defense", contacts[c])) continue;
                    float ranked = EvidenceRankingPenalty(
                        "support.objective_defense", contacts[c]);
                    if (ranked >= bestObjectiveEvidence) continue;
                    bestObjectiveEvidence = ranked;
                    threatCell = contacts[c].Cell;
                    objectiveCell = near;
                    objectiveFact = contacts[c];
                }
                if (!threatCell.IsValid) continue;
                objectiveCooldown[fighter.thingIDNumber] = now;
                IssueTasking(fighter, threatCell, null,
                    "defending the painted objective at " + objectiveCell,
                    objectiveCell,
                    behaviorKey: "support.objective_defense",
                    authorityOrigin: CAAuthorityOrigin.PlayerDelegated,
                    authorityIdentity: "delegated objective defense",
                    knowledgeFact: objectiveFact,
                    knowledgeOwner: fighter.LabelShort);
            }
        }

        private void DefensiveLinePass()
        {
            var overlay = CAOverlayMapComponent.For(map);
            var know = KnowledgeMapComponent.For(map);
            if (overlay == null || know == null
                || !overlay.HasLayer(CAOverlayKind.DefensiveLine)) return;
            var hold = map.GetComponent<HoldMapComponent>();
            if (hold == null) return;
            int now = Find.TickManager.TicksGame;

            int lineLength = overlay.CellsOf(CAOverlayKind.DefensiveLine).Count;
            int slotCount = UnityEngine.Mathf.Clamp(lineLength / 8, 2, 8);
            List<IntVec3> slots = null;
            List<IntVec3> holdCells = null;

            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn fighter = colonists[i];
                bool selfAssigned = selfAssignedLine.ContainsKey(
                    fighter.thingIDNumber);
                // Contact copies are not free; only the pawns this pass can
                // affect pay for one.
                if (!selfAssigned && !CABehaviorGate.StableProfileAllows(
                        fighter, "support.defensive_line"))
                    continue;
                List<ThreatContactSnapshot> fighterContacts =
                    know.FreshContacts(fighter);
                ThreatContactSnapshot lineFact;
                bool aware = TryBestHeldFact(fighter,
                    "support.defensive_line", fighterContacts, out lineFact);
                if (aware) lastThreatTick[fighter.thingIDNumber] = now;

                if (selfAssigned)
                {
                    // Stand down once the threat picture has stayed clear.
                    int last;
                    bool stale = !lastThreatTick.TryGetValue(
                            fighter.thingIDNumber, out last)
                        || now - last > LineStandDownTicks;
                    if (!hold.IsHolding(fighter))
                    {
                        selfAssignedLine.Remove(fighter.thingIDNumber);
                    }
                    else if (stale)
                    {
                        selfAssignedLine.Remove(fighter.thingIDNumber);
                        hold.Release(fighter, reason:
                            "defensive line stand-down: no fresh threats");
                        CATrace.Pawn(fighter, "defensive line STAND-DOWN - "
                            + "threat picture clear", anchor: fighter.Position);
                    }
                    continue;
                }

                if (!aware) continue;
                string refusal;
                float range;
                if (!FreeForSupport(fighter, out range, out refusal)) continue;
                if (hold.IsHolding(fighter)) continue;

                if (slots == null)
                {
                    // Interpreter positions, oriented by this fighter's own
                    // threat picture: cover on the friendly side, fire across.
                    IntVec3 hint = lineFact.Cell;
                    var solutions = CALineInterpreter.Solve(map,
                        CAOverlayKind.DefensiveLine, slotCount, hint);
                    slots = new List<IntVec3>(solutions.Count);
                    for (int s = 0; s < solutions.Count; s++)
                        slots.Add(solutions[s].Cell);
                    holdCells = CollectHoldAnchors();
                }
                IntVec3 slot = NearestOpenSlot(slots, holdCells, fighter);
                if (!slot.IsValid) continue;
                CAIntentContext context;
                CABehaviorDecision lineDecision;
                if (!TryAuthorizeTasking(fighter,
                        "support.defensive_line",
                        CAAuthorityOrigin.PlayerDelegated,
                        "delegated defensive line", slot,
                        out context, out lineDecision, lineFact,
                        fighter.LabelShort))
                    continue;
                if (hold.OrderHold(fighter, slot, context))
                {
                    selfAssignedLine[fighter.thingIDNumber] = now;
                    RegisterTasking(fighter, null,
                        "defensive-line assignment", context);
                    CATrace.Pawn(fighter,
                        "self-assigned to the defensive line",
                        destination: slot, anchor: fighter.Position,
                        intent: context);
                }
            }
        }

        // ------------------------------ helpers ------------------------------

        private List<IntVec3> CollectHoldAnchors()
        {
            var holdCells = new List<IntVec3>();
            var job = CATactical.JobOf(map);
            if (job != null)
            {
                var colonists = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < colonists.Count; i++)
                {
                    int kind;
                    IntVec3 cell, watch;
                    if (job.TryGetOrder(colonists[i], out kind, out cell,
                            out watch)
                        && kind == LordJob_CATactical.KindHold)
                        holdCells.Add(cell);
                }
            }
            return holdCells;
        }

        private IntVec3 NearestOpenSlot(List<IntVec3> slots,
            List<IntVec3> holdCells, Pawn fighter)
        {
            IntVec3 best = IntVec3.Invalid;
            float bestDistance = float.MaxValue;
            for (int s = 0; s < slots.Count; s++)
            {
                bool taken = false;
                for (int h = 0; h < holdCells.Count; h++)
                    if (slots[s].InHorDistOf(holdCells[h], 1.9f))
                    { taken = true; break; }
                if (taken) continue;
                if (!fighter.CanReach(slots[s], PathEndMode.OnCell,
                    Danger.Deadly)) continue;
                float d = fighter.Position.DistanceTo(slots[s]);
                if (d < bestDistance) { bestDistance = d; best = slots[s]; }
            }
            return best;
        }

        private Pawn BestSupporterForFact(Pawn supported, int squad,
            IntVec3 threatCell, string behaviorKey, Pawn reporter,
            ThreatContactSnapshot sourceFact,
            out ThreatContactSnapshot selectedFact,
            out string knowledgeOwner)
        {
            selectedFact = default(ThreatContactSnapshot);
            knowledgeOwner = null;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
            if (knowledge == null || reporter == null
                || sourceFact.State != ThreatContactState.Active) return null;
            Pawn best = null;
            float bestScore = float.MaxValue;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                for (int i = 0; i < colonists.Count; i++)
                {
                    Pawn candidate = colonists[i];
                    if (candidate == supported) continue;
                    if (!CABehaviorGate.StableProfileAllows(candidate,
                            behaviorKey)) continue;
                    bool sameSquad = SquadComponent.SquadOf(candidate) == squad;
                    // First pass: squadmates. Second pass: any Autonomous
                    // fighter - the colony does not watch a rescue die over
                    // an org-chart boundary.
                    if (pass == 0 && !sameSquad) continue;
                    if (pass == 1 && sameSquad) continue;
                    string refusal;
                    float range;
                    if (!FreeForSupport(candidate, out range, out refusal))
                        continue;
                    if (supported != null
                        && !CommsAware(candidate, supported)) continue;
                    ThreatContactSnapshot candidateFact;
                    bool alreadyHeld = knowledge.TryGetFreshContact(candidate,
                        sourceFact.HostileId, out candidateFact);
                    if (!alreadyHeld)
                    {
                        CommunicationChannel channel;
                        if (!TryContactRelayChannel(reporter, candidate,
                                sourceFact, out channel)) continue;
                        candidateFact = RelayedSnapshot(sourceFact, reporter,
                            channel);
                    }
                    if (!FactMeetsBehaviorContract(behaviorKey,
                            candidateFact)) continue;
                    float score = candidate.Position.DistanceTo(threatCell)
                        + EvidenceRankingPenalty(behaviorKey, candidateFact);
                    if (score >= bestScore) continue;
                    best = candidate;
                    bestScore = score;
                }
            }
            if (best == null || !TryEnsureTaskingFact(knowledge, reporter,
                    best, sourceFact, out selectedFact, out knowledgeOwner))
                return null;
            return best;
        }

        private Pawn BestInformedSupporter(
            List<OwnedThreatContact> evidence, IntVec3 threatCell,
            string behaviorKey, out ThreatContactSnapshot selectedFact,
            out Pawn sourceOwner)
        {
            selectedFact = default(ThreatContactSnapshot);
            sourceOwner = null;
            if (evidence == null || evidence.Count == 0) return null;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
            if (knowledge == null) return null;
            Pawn best = null;
            float bestScore = float.MaxValue;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn candidate = colonists[i];
                if (!CABehaviorGate.StableProfileAllows(candidate,
                        behaviorKey)) continue;
                string refusal;
                float range;
                if (!FreeForSupport(candidate, out range, out refusal))
                    continue;
                ThreatContactSnapshot candidateFact =
                    default(ThreatContactSnapshot);
                Pawn candidateSource = null;
                bool informed = false;
                float candidateEvidencePenalty = float.MaxValue;
                for (int e = 0; e < evidence.Count; e++)
                {
                    ThreatContactSnapshot held;
                    if (!knowledge.TryGetFreshContact(candidate,
                        evidence[e].Contact.HostileId, out held)) continue;
                    if (!held.Evidence.IsDirect
                        && held.Evidence.DeliveryChannel
                            == CommunicationChannel.None) continue;
                    if (!FactMeetsBehaviorContract(behaviorKey, held))
                        continue;
                    float heldPenalty = EvidenceRankingPenalty(behaviorKey,
                        held);
                    if (!informed || heldPenalty < candidateEvidencePenalty
                        || Mathf.Abs(heldPenalty - candidateEvidencePenalty)
                            <= 0.0001f
                        && held.SourceTick > candidateFact.SourceTick)
                    {
                        informed = true;
                        candidateFact = held;
                        candidateSource = evidence[e].Owner;
                        candidateEvidencePenalty = heldPenalty;
                    }
                }
                if (!informed) continue;
                float score = candidate.Position.DistanceTo(threatCell)
                    + candidateEvidencePenalty;
                if (score >= bestScore) continue;
                best = candidate;
                bestScore = score;
                selectedFact = candidateFact;
                sourceOwner = candidateSource;
            }
            return best;
        }

        private void IssueTasking(Pawn supporter, IntVec3 watchCell,
            Pawn supported, string reason, IntVec3 anchorHint = default,
            string behaviorKey = "support.reported_contact_reorganization",
            CAAuthorityOrigin authorityOrigin =
                CAAuthorityOrigin.PlayerDelegated,
            string authorityIdentity = "delegated squad support",
            string moteText = null,
            ThreatContactSnapshot? knowledgeFact = null,
            string knowledgeOwner = null,
            SupportKnowledgeEvidence? explicitEvidence = null)
        {
            float range;
            string refusal;
            if (!FreeForSupport(supporter, out range, out refusal))
            {
                CATrace.Pawn(supporter, "squad support REFUSED - " + refusal,
                    contact: watchCell, anchor: supporter.Position);
                return;
            }
            SupportKnowledgeEvidence evidence = explicitEvidence
                ?? (knowledgeFact.HasValue
                    ? EvidenceFromContact(supporter, knowledgeFact.Value,
                        knowledgeOwner)
                    : SupportKnowledgeEvidence.Missing);
            CAIntentContext intent;
            CABehaviorDecision decision;
            if (!TryAuthorizeTasking(supporter, behaviorKey,
                    authorityOrigin, authorityIdentity, watchCell,
                    out intent, out decision, evidence))
            {
                CATrace.Pawn(supporter, "squad support BLOCKED - "
                    + decision.PrimaryReason + " [" + behaviorKey + "]",
                    contact: watchCell, anchor: supporter.Position);
                return;
            }
            IntVec3 coverCell = FindFiringPosition(supporter, watchCell,
                range, anchorHint);
            if (!coverCell.IsValid)
            {
                CATrace.Pawn(supporter,
                    "squad support REFUSED - no firing position with a line "
                    + "to the threat", contact: watchCell,
                    anchor: supporter.Position);
                return;
            }
            if (!CATactical.AssignAutomaticDefense(supporter, coverCell,
                watchCell, intent))
            {
                CATrace.Pawn(supporter,
                    "squad support REFUSED - committed elsewhere",
                    contact: watchCell, anchor: supporter.Position);
                return;
            }
            RegisterTasking(supporter, supported, reason, intent);
            if (moteText != null)
                VisibleExchange(supported, supporter, moteText);
            CATrace.Pawn(supporter, "squad support TASKED - " + reason,
                destination: coverCell, contact: watchCell,
                anchor: supporter.Position, intent: intent);
        }

        private bool TryAuthorizeTasking(Pawn actor, string behaviorKey,
            CAAuthorityOrigin authorityOrigin, string authorityIdentity,
            IntVec3 target, out CAIntentContext intent,
            out CABehaviorDecision decision,
            ThreatContactSnapshot? knowledgeFact = null,
            string knowledgeOwner = null)
        {
            SupportKnowledgeEvidence evidence = knowledgeFact.HasValue
                ? EvidenceFromContact(actor, knowledgeFact.Value,
                    knowledgeOwner)
                : SupportKnowledgeEvidence.Missing;
            return TryAuthorizeTasking(actor, behaviorKey, authorityOrigin,
                authorityIdentity, target, out intent, out decision, evidence);
        }

        private bool TryAuthorizeTasking(Pawn actor, string behaviorKey,
            CAAuthorityOrigin authorityOrigin, string authorityIdentity,
            IntVec3 target, out CAIntentContext intent,
            out CABehaviorDecision decision,
            SupportKnowledgeEvidence evidence)
        {
            bool playerOwnedWork = HasPlayerOwnedWork(actor);
            CABehaviorContext context = CABehaviorContext.ForPawn(actor,
                authorityOrigin, authoritySatisfied:
                    !string.IsNullOrWhiteSpace(authorityIdentity),
                knowledgeSatisfied: target.IsValid && evidence.Satisfied,
                knowledgeFresh: target.IsValid && evidence.Fresh,
                liveValidated: evidence.LiveValidated,
                knowledgeRelayed: evidence.Relayed,
                knowledgeAgeTicks: evidence.AgeTicks,
                knowledgeConfidence: evidence.Confidence,
                knowledgeUncertainty: evidence.Uncertainty,
                capabilitySatisfied: actor != null && !actor.Downed,
                currentIntentCompatible: !playerOwnedWork,
                directPlayerOwnership: playerOwnedWork,
                authorityBasis: authorityIdentity,
                knowledgeBasis: evidence.Basis,
                owner: "squad support");
            decision = CABehaviorGate.EvaluateForSelection(behaviorKey,
                context);
            intent = default(CAIntentContext);
            if (!decision.SelectionApproved) return false;
            intent = CACombatIntent.Authorized(actor,
                CAIntentController.RaidDefense, behaviorKey,
                authorityOrigin, authorityIdentity, "squad support",
                target.ToString());
            return true;
        }

        private SupportKnowledgeEvidence EvidenceFromContact(Pawn actor,
            ThreatContactSnapshot fact, string knowledgeOwner)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int age = Math.Max(0, now - fact.SourceTick);
            bool active = actor != null && actor.Map == map
                && fact.HostileId >= 0 && fact.Cell.IsValid
                && fact.Cell.InBounds(map)
                && fact.State == ThreatContactState.Active
                && now >= fact.SourceTick;
            Pawn hostile = PawnById(fact.HostileId);
            bool live = active && hostile != null
                && KnowledgeMapComponent.CanCurrentlySeeHostile(actor,
                    hostile);
            string delivery = fact.Evidence.IsDirect ? "direct"
                : "relayed via " + fact.Evidence.DeliveryChannel;
            return new SupportKnowledgeEvidence(active, active,
                !fact.Evidence.IsDirect, live, age,
                fact.Evidence.Confidence, fact.Evidence.Uncertainty,
                delivery + " contact held by "
                    + (actor?.LabelShort ?? "unknown actor")
                    + "; source owner "
                    + (knowledgeOwner ?? "unknown") + "; age " + age
                    + " ticks; confidence "
                    + fact.Evidence.Confidence.ToString("F2")
                    + "; uncertainty "
                    + fact.Evidence.Uncertainty.ToString("F2"), fact);
        }

        private bool TryDeliveredRequestEvidence(Pawn reporter, Pawn actor,
            string request, out SupportKnowledgeEvidence evidence)
        {
            evidence = SupportKnowledgeEvidence.Missing;
            if (reporter == null || actor == null || reporter.Map != map
                || actor.Map != map || reporter.Dead || actor.Dead) return false;
            CommunicationChannel channel = CommunicationChannel.None;
            bool direct = reporter == actor;
            if (!direct && !CommsModule.TryGetKnowledgeChannel(reporter,
                    actor, 11.9f, out channel)) return false;
            evidence = new SupportKnowledgeEvidence(true, true, !direct,
                liveValidated: reporter.Spawned && actor.Spawned,
                ageTicks: 0, confidence: 1f, uncertainty: 0f,
                basis: request + " delivered "
                    + (direct ? "directly" : "via " + channel)
                    + " by " + reporter.LabelShort);
            return true;
        }

        private bool TryEnsureTaskingFact(KnowledgeMapComponent knowledge,
            Pawn reporter, Pawn actor, ThreatContactSnapshot sourceFact,
            out ThreatContactSnapshot actorFact, out string knowledgeOwner,
            CommunicationChannel? preferredChannel = null)
        {
            actorFact = default(ThreatContactSnapshot);
            knowledgeOwner = null;
            if (knowledge == null || actor == null || actor.Map != map)
                return false;
            if (knowledge.TryGetFreshContact(actor, sourceFact.HostileId,
                    out actorFact))
            {
                knowledgeOwner = actor.LabelShort + " (already held)";
                return true;
            }
            if (reporter == null || reporter.Map != map) return false;
            ThreatContactSnapshot reporterFact;
            if (!knowledge.TryGetFreshContact(reporter,
                    sourceFact.HostileId, out reporterFact)) return false;
            CommunicationChannel channel;
            if (preferredChannel.HasValue
                && preferredChannel.Value != CommunicationChannel.None)
                channel = preferredChannel.Value;
            else if (!TryContactRelayChannel(reporter, actor, reporterFact,
                    out channel)) return false;
            ContactEvidence delivered = reporterFact.Evidence.RelayedThrough(
                channel, reporter.thingIDNumber);
            knowledge.NoteEvidence(actor, reporterFact.HostileId,
                reporterFact.Cell, reporterFact.SourceTick, delivered,
                "a squad tasking contact", reporterFact.WeaponCategory);
            if (!knowledge.TryGetFreshContact(actor, reporterFact.HostileId,
                    out actorFact)) return false;
            knowledgeOwner = reporter.LabelShort;
            return true;
        }

        private bool TryContactRelayChannel(Pawn reporter, Pawn actor,
            ThreatContactSnapshot fact, out CommunicationChannel channel)
        {
            channel = CommunicationChannel.None;
            if (reporter == null || actor == null || reporter.Map != map
                || actor.Map != map) return false;
            if (reporter == actor) return true;
            return fact.Evidence.IsDirect
                ? CommsModule.TryGetStrategicChannel(reporter, actor,
                    11.9f, out channel)
                : CommsModule.TryGetKnowledgeChannel(reporter, actor,
                    11.9f, out channel);
        }

        private static ThreatContactSnapshot RelayedSnapshot(
            ThreatContactSnapshot source, Pawn reporter,
            CommunicationChannel channel)
        {
            ContactEvidence delivered = source.Evidence.RelayedThrough(channel,
                reporter?.thingIDNumber ?? -1);
            return new ThreatContactSnapshot(source.HostileId, source.Cell,
                source.SourceTick, Find.TickManager?.TicksGame ?? 0,
                source.WeaponCategory, delivered, source.State,
                source.StateTick);
        }

        private static bool FactMeetsBehaviorContract(string behaviorKey,
            ThreatContactSnapshot fact)
        {
            CABehaviorDefinition definition = CABehaviorCatalog.Get(behaviorKey);
            if (definition == null || fact.HostileId < 0 || !fact.Cell.IsValid
                || fact.State != ThreatContactState.Active) return false;
            int now = Find.TickManager?.TicksGame ?? 0;
            int age = now - fact.SourceTick;
            if (age < 0 || definition.MaximumKnowledgeAgeTicks > 0
                && age > definition.MaximumKnowledgeAgeTicks) return false;
            return fact.Evidence.Confidence
                >= definition.MinimumKnowledgeConfidence;
        }

        private static float EvidenceRankingPenalty(string behaviorKey,
            ThreatContactSnapshot fact)
        {
            CABehaviorDefinition definition = CABehaviorCatalog.Get(behaviorKey);
            int now = Find.TickManager?.TicksGame ?? 0;
            int age = Math.Max(0, now - fact.SourceTick);
            float penalty = (1f - fact.Evidence.Confidence) * 12f
                + Mathf.Min(4f, age / 600f);
            if (definition?.UncertaintyAffectsRanking == true)
                penalty += fact.Evidence.Uncertainty * 6f;
            return penalty;
        }

        private static bool TryBestHeldFact(Pawn actor, string behaviorKey,
            List<ThreatContactSnapshot> contacts,
            out ThreatContactSnapshot selected)
        {
            selected = default(ThreatContactSnapshot);
            if (actor == null || contacts == null) return false;
            bool found = false;
            float bestPenalty = float.MaxValue;
            for (int i = 0; i < contacts.Count; i++)
            {
                ThreatContactSnapshot fact = contacts[i];
                if (!FactMeetsBehaviorContract(behaviorKey, fact)) continue;
                float penalty = EvidenceRankingPenalty(behaviorKey, fact);
                if (found && penalty >= bestPenalty) continue;
                found = true;
                bestPenalty = penalty;
                selected = fact;
            }
            return found;
        }

        private void RegisterTasking(Pawn actor, Pawn supported,
            string reason, CAIntentContext intent)
        {
            if (actor == null) return;
            for (int i = taskings.Count - 1; i >= 0; i--)
                if (taskings[i].PawnId == actor.thingIDNumber)
                    taskings.RemoveAt(i);
            int now = Find.TickManager.TicksGame;
            taskings.Add(new SupportTasking
            {
                PawnId = actor.thingIDNumber,
                IssuedTick = now,
                ExpiryTick = now + TaskingLifetimeTicks,
                Reason = reason,
                SupportedId = supported != null
                    ? supported.thingIDNumber : -1,
                BehaviorKey = intent.BehaviorKey,
                EpisodeId = intent.EpisodeId,
                AuthorityOrigin = (int)intent.AuthorityOrigin,
                AuthorityIdentity = intent.AuthorityIdentity,
                OwnershipScope = intent.OwnershipScope,
                TerminationCondition = intent.TerminationCondition
            });
        }

        private IntVec3 FindFiringPosition(Pawn supporter, IntVec3 watchCell,
            float range, IntVec3 anchorHint)
        {
            float usable = UnityEngine.Mathf.Max(9f, range * 0.9f);
            if (supporter.Position.DistanceTo(watchCell) <= usable
                && !CALineInterpreter.HazardousCell(map, supporter.Position)
                && GenSight.LineOfSight(supporter.Position, watchCell, map,
                    true))
                return supporter.Position;
            IntVec3 center = anchorHint.IsValid ? anchorHint
                : supporter.Position;
            int limit = GenRadial.NumCellsInRadius(8.9f);
            for (int r = 0; r < limit; r++)
            {
                IntVec3 c = center + GenRadial.RadialPattern[r];
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if (CALineInterpreter.HazardousCell(map, c)) continue;
                if (c.DistanceTo(watchCell) > usable) continue;
                if (!GenSight.LineOfSight(c, watchCell, map, true)) continue;
                if (!supporter.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                    continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        private bool FreeForSupport(Pawn p, out float range, out string refusal)
        {
            range = 0f;
            refusal = null;
            if (p == null || p.Dead || p.Downed || p.InMentalState
                || p.Drafted)
            { refusal = "unavailable"; return false; }
            if (p.DevelopmentalStage != DevelopmentalStage.Adult
                || p.WorkTagIsDisabled(WorkTags.Violent))
            { refusal = "not a combatant"; return false; }
            if (SquadComponent.CombatLiability(p))
            { refusal = "combat liability"; return false; }
            var verb = p.equipment?.PrimaryEq?.PrimaryVerb;
            if (verb == null || verb.verbProps.IsMeleeAttack)
            { refusal = "no ranged weapon"; return false; }
            range = verb.verbProps.range;
            if (HasPlayerOwnedWork(p))
            { refusal = "owned by direct player work"; return false; }
            Job current = p.CurJob;
            if (current != null && (current.def == CA_Defs.CombatRecovery
                || current.def == CA_Defs.EmergencySelfTend
                || current.def == CA_Defs.AssessCasualty
                || current.def == CA_Defs.CheckWelfare
                || current.def == CA_Defs.WaitForWelfareSupport
                || current.def == CA_Defs.FightingWithdrawal
                || current.def == JobDefOf.Rescue))
            { refusal = "owned by a critical job"; return false; }
            var job = CATactical.JobOf(map);
            int kind;
            IntVec3 cell, watch;
            if (job != null && job.TryGetOrder(p, out kind, out cell, out watch)
                && kind != LordJob_CATactical.KindRaidDefense)
            { refusal = "holds a standing order"; return false; }
            return true;
        }

        // Reachability is the channel system's law, not this module's: mental
        // (mechlink) and radio (headset) channels are map-wide by design —
        // gear is a standard map-size signal — while voice is a physical
        // broadcast at the canonical 11.9-cell shout, narrowed further by
        // concealment. No proximity shortcuts on top.
        private bool CommsAware(Pawn candidate, Pawn subject)
        {
            CommunicationChannel channel;
            return CommsModule.TryGetKnowledgeChannel(subject, candidate,
                11.9f, out channel);
        }

        private bool CasualtyZoneContested(Pawn casualty, Pawn observer,
            out IntVec3 threatCell, out int threatId,
            out ThreatContactSnapshot selectedFact, out Pawn factOwner)
        {
            threatCell = IntVec3.Invalid;
            threatId = -1;
            selectedFact = default(ThreatContactSnapshot);
            factOwner = null;
            var know = KnowledgeMapComponent.For(map);
            if (know == null || casualty == null || observer == null
                || observer.Map != map) return false;
            float best = float.MaxValue;
            List<ThreatContactSnapshot> contacts = know.FreshContacts(observer);
            for (int c = 0; c < contacts.Count; c++)
            {
                if (!contacts[c].Cell.IsValid) continue;
                float d = contacts[c].Cell.DistanceTo(casualty.Position);
                if (d > 26f || !FactMeetsBehaviorContract(
                        "support.rescue_escort", contacts[c])) continue;
                float score = d + EvidenceRankingPenalty(
                    "support.rescue_escort", contacts[c]);
                if (score < best)
                {
                    best = score;
                    threatCell = contacts[c].Cell;
                    threatId = contacts[c].HostileId;
                    selectedFact = contacts[c];
                    factOwner = observer;
                }
            }
            return threatId != -1;
        }

        private bool SuppressionOnThreat(int hostileId)
        {
            var pawns = map.mapPawns.AllPawnsSpawned;
            Pawn threat = null;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == hostileId)
                { threat = pawns[i]; break; }
            if (threat == null || threat.Dead || threat.Downed) return true;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn shooter = colonists[i];
                var busy = shooter.stances?.curStance as Stance_Busy;
                if (busy?.focusTarg.Thing == threat) return true;
                Job current = shooter.CurJob;
                if (current != null
                    && (current.def == JobDefOf.AttackStatic
                        || current.def == JobDefOf.AttackMelee
                        || current.def == JobDefOf.Wait_Combat)
                    && current.targetA.Thing == threat) return true;
            }
            return false;
        }

        private bool ColonyBattleActive(Pawn observer)
        {
            var know = KnowledgeMapComponent.For(map);
            return know != null && observer != null && observer.Map == map
                && know.FreshContacts(observer).Count > 0;
        }

        private bool ContactNearPath(Pawn observer, IntVec3 from, IntVec3 to,
            float radius)
        {
            var know = KnowledgeMapComponent.For(map);
            if (know == null || observer == null || observer.Map != map)
                return false;
            List<ThreatContactSnapshot> contacts = know.FreshContacts(observer);
            for (int c = 0; c < contacts.Count; c++)
            {
                if (!contacts[c].Cell.IsValid) continue;
                if (DistanceToSegment(contacts[c].Cell, from, to)
                    <= radius) return true;
            }
            return false;
        }

        private static bool HasPlayerOwnedWork(Pawn pawn)
        {
            if (pawn?.jobs == null) return false;
            if (pawn.CurJob?.playerForced == true) return true;
            return pawn.jobs.jobQueue != null
                && pawn.jobs.jobQueue.AnyPlayerForced;
        }

        private Pawn PawnById(int id)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == id) return pawns[i];
            return null;
        }
    }
}
