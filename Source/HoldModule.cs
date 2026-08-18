using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Module: hold position. The player sets intent - "defend this local position" -
    // and the pawn treats the assigned cell as an anchor, reacquiring and repositioning
    // within its duty envelope without turning point defense into pursuit. The autonomy
    // mode governs what happens when holding becomes strategically injudicious:
    //   Standard (draft-piloted): hold until told otherwise. No deviation.
    //   Proactive+: deviation triggers - flanked, melee on top, bleeding out, position
    //   collapsing - break the hold, fall back by suppressive retreat, and REPORT UP.
    // Holds ride the tactical lord's duty pipeline: the order joins the pawn to the
    // map's standing CA lord, the CA_HoldPosition duty does ALL the doing (walk there,
    // fight from it without chasing, needs within a short leash, sector facing), and
    // this component only SUPERVISES - deviation triggers, the player's-hand release,
    // sanity checks. No job re-issuing, no pins. Holds persist through saves via the
    // lord; the local dicts are a rebuildable index.
    public class HoldMapComponent : MapComponent
    {
        private Dictionary<int, IntVec3> holds = new Dictionary<int, IntVec3>();
        // Overwatch: what each holder WATCHES - a sector point, not a frozen stare.
        private readonly Dictionary<int, IntVec3> watches = new Dictionary<int, IntVec3>();
        private readonly HashSet<int> approachCompleted = new HashSet<int>();
        private int cooldown;

        // Task elasticity for explicit orders: the player's hand wins the
        // MOMENT, not the intent. A hold displaced by a direct player errand
        // (equip this, grab that) is remembered here and resumes when the
        // hand lifts - the errand interleaves instead of erasing the order.
        // Session-only; a fresh order or the expiry window clears it.
        private sealed class PendingResume
        {
            public IntVec3 Cell;
            public IntVec3 Watch;
            public float Envelope;
            public int Grit;
            public int EpisodeId;
            public int ExpiryTick;
        }

        private readonly Dictionary<int, PendingResume> pendingResume =
            new Dictionary<int, PendingResume>();
        private readonly Dictionary<int, int> hazardShiftTick =
            new Dictionary<int, int>();

        // Nearest clean standable anchor near the gassed one, preferring
        // cells that keep the overwatch line when a watch point exists.
        private IntVec3 FindCleanAnchor(IntVec3 anchor, IntVec3 watchPoint)
        {
            IntVec3 best = IntVec3.Invalid;
            float bestScore = float.MinValue;
            int limit = GenRadial.NumCellsInRadius(11.9f);
            for (int r = 0; r < limit; r++)
            {
                IntVec3 c = anchor + GenRadial.RadialPattern[r];
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if (CALineInterpreter.HazardousCell(map, c)) continue;
                float score = -c.DistanceTo(anchor);
                if (watchPoint.IsValid
                    && GenSight.LineOfSight(c, watchPoint, map, true))
                    score += 6f;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        private readonly struct PerceivedHoldThreat
        {
            public readonly IntVec3 RememberedCell;
            public readonly Pawn VisiblePawn;
            public readonly int SourceTick;
            public readonly bool Relayed;
            public readonly float Confidence;
            public readonly float Uncertainty;

            public PerceivedHoldThreat(IntVec3 rememberedCell,
                Pawn visiblePawn, int sourceTick, bool relayed,
                float confidence, float uncertainty)
            {
                RememberedCell = rememberedCell;
                VisiblePawn = visiblePawn;
                SourceTick = sourceTick;
                Relayed = relayed;
                Confidence = confidence;
                Uncertainty = uncertainty;
            }
        }

        public HoldMapComponent(Map map) : base(map) { }

        // The lord scribes; the registries do not. Rebuild the index (and the ambush
        // registries) from the lord's scribed order table after load.
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            try
            {
                var job = CATactical.JobOf(map);
                if (job == null) return;
                var pawns = new List<Pawn>();
                var kinds = new List<int>();
                var cells = new List<IntVec3>();
                var watchPts = new List<IntVec3>();
                int restored = 0;
                job.CopyOrders(pawns, kinds, cells, watchPts);
                for (int i = 0; i < pawns.Count; i++)
                {
                    if (pawns[i] == null) continue;
                    if (kinds[i] == LordJob_CATactical.KindHold)
                    {
                        holds[pawns[i].thingIDNumber] = cells[i];
                        if (watchPts[i].IsValid) watches[pawns[i].thingIDNumber] = watchPts[i];
                        restored++;
                    }
                    else if (kinds[i] == LordJob_CATactical.KindRaidDefense)
                    {
                        // Automatic defense is a reaction to session-only contact
                        // knowledge, never a saved player order. The knowledge store
                        // is empty after load, so remove the orphan immediately.
                        CATactical.ReleaseAutomaticDefense(pawns[i]);
                    }
                    else
                    {
                        HiddenRegistry.RestoreOrderedFromSave(pawns[i], cells[i], kinds[i]);
                        restored++;
                    }
                }
                if (restored > 0)
                    CATrace.Log("restored " + restored + " standing orders from save");
            }
            catch { }
        }

        public bool IsHolding(Pawn p)
        {
            return p != null && holds.ContainsKey(p.thingIDNumber);
        }

        public bool OrderHold(Pawn p, IntVec3 cell, CAIntentContext context)
        {
            return OrderHoldInternal(p, cell, IntVec3.Invalid, context);
        }

        private bool OrderHoldInternal(Pawn p, IntVec3 cell,
            IntVec3 watchPoint, CAIntentContext context)
        {
            if (p == null || p.Map != map || p.jobs == null
                || !cell.IsValid || !cell.InBounds(map)
                || !cell.Standable(map)
                || !p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) return false;
            if (!context.IsValid)
            {
                CATrace.Skip(p, "hold order",
                    "untagged order refused; causal origin is required",
                    destination: cell, anchor: p.Position);
                return false;
            }
            // Combat holds are for combatants. Anything else reaching here is an
            // unplanned behavior - refuse it and say so out loud.
            if (p.DevelopmentalStage != DevelopmentalStage.Adult
                || p.WorkTagIsDisabled(WorkTags.Violent))
            {
                Log.Warning("[Colonist Awareness] UNPLANNED: refused combat hold for "
                    + p.LabelShort + " (child or incapable of violence)");
                return false;
            }
            HiddenRegistry.CancelForReplacement(p,
                "replaced by a newer hold order");
            holds[p.thingIDNumber] = cell;
            approachCompleted.Remove(p.thingIDNumber);
            if (watchPoint.IsValid)
                watches[p.thingIDNumber] = watchPoint;
            else
                watches.Remove(p.thingIDNumber);
            var wd = map.GetComponent<WithdrawalMapComponent>();
            if (wd != null) wd.Cancel(p);
            // The duty does the doing from here: walk there, fight from it, hold it.
            CATactical.Assign(p, LordJob_CATactical.KindHold, cell,
                watchPoint, context);
            if (!CATactical.MatchesOrder(p, context.EpisodeId,
                LordJob_CATactical.KindHold))
            {
                holds.Remove(p.thingIDNumber);
                return false;
            }
            if (CABehaviorGate.StableProfileAllows(p,
                    "combat.hold_deviation")
                && !CATactical.IsHoldProfilePinned(p))
                AdviseProfile(p, cell);

            var tacticalLord = p.GetLord();

            // A fresh Hold is itself the player's newest command. Replace an older
            // current/queued order even when the pawn already occupies the anchor;
            // otherwise the supervision pass would mistake that older order for a
            // later player override and discard the Hold. The one owned job only
            // enters the standing duty: the duty remains responsible for subsequent
            // posture, combat, and needs decisions.
            Job entry;
            if (p.Position != cell)
            {
                entry = JobMaker.MakeJob(JobDefOf.Goto, cell);
                entry.locomotionUrgency = PawnUtility.ResolveLocomotion(
                    p, LocomotionUrgency.Sprint);
            }
            else
            {
                entry = JobMaker.MakeJob(CA_Defs.StackPosture);
                entry.expiryInterval = 120;
                entry.checkOverrideOnExpire = true;
            }
            entry.lord = tacticalLord;
            Job currentEntry = p.CurJob;
            if (currentEntry != null && currentEntry.JobIsSameAs(p, entry))
            {
                // TryTakeOrderedJob treats a matching job as success before it clears
                // the old queue or transfers ownership. Adopt the identical native
                // action explicitly so this new Hold, rather than stale prior intent,
                // is what supervision and constant duty observe.
                p.jobs.ClearQueuedJobs();
                currentEntry.lord = tacticalLord;
                currentEntry.playerForced = true;
                currentEntry.locomotionUrgency = entry.locomotionUrgency;
                currentEntry.expiryInterval = entry.expiryInterval;
                currentEntry.checkOverrideOnExpire = entry.checkOverrideOnExpire;
                JobMaker.ReturnToPool(entry);
                CATrace.Pawn(p,
                    "hold order ADOPTED_IDENTICAL current job; queue cleared and ownership transferred",
                    destination: cell, anchor: p.Position, intent: context);
                return true;
            }
            Job blocker = p.CurJob;
            int entryId = entry.loadID;
            bool accepted = p.jobs.TryTakeOrderedJob(entry, JobTag.Misc);
            bool started = accepted && p.CurJob != null
                && p.CurJob.loadID == entryId;
            bool pending = accepted && !started && p.jobs.jobQueue != null
                && p.jobs.jobQueue.Contains(entry);
            CATrace.Pawn(p, "hold order "
                + (!accepted ? "REJECTED"
                    : started ? "STARTED"
                    : pending ? "PENDING behind "
                        + (blocker?.def?.defName ?? "unknown blocker")
                    : "ACCEPTED with engine state unresolved"),
                destination: cell, anchor: p.Position, intent: context);
            if (!accepted)
            {
                Release(p);
                return false;
            }
            return true;
        }

        public void Release(Pawn p, bool startNewJob = true,
            string reason = null)
        {
            if (p == null) return;
            holds.Remove(p.thingIDNumber);
            watches.Remove(p.thingIDNumber);
            approachCompleted.Remove(p.thingIDNumber);
            if (CATactical.IsHold(p))
                CATactical.Release(p, startNewJob, reason);
        }

        // A hold that watches: the sector point is what the holder scans and engages.
        public bool OrderHoldWatching(Pawn p, IntVec3 cell, IntVec3 watchPoint,
            CAIntentContext context)
        {
            return OrderHoldInternal(p, cell, watchPoint, context);
        }

        public override void MapComponentTick()
        {
            if (--cooldown > 0) return;
            cooldown = 45;
            var s = AwarenessMod.Settings;
            if (s == null || !s.holdOrders)
            {
                // The kill switch actually kills: toggling the setting off clears every
                // hold instead of leaving pawns frozen mid-intent.
                if (holds.Count > 0)
                {
                    var ids = new List<int>(holds.Keys);
                    var colonists = map.mapPawns.FreeColonistsSpawned;
                    for (int i = 0; i < ids.Count; i++)
                        for (int j = 0; j < colonists.Count; j++)
                            if (colonists[j].thingIDNumber == ids[i])
                            {
                                if (CATactical.IsHold(colonists[j]))
                                    CATactical.Release(colonists[j]);
                                break;
                            }
                    holds.Clear();
                    watches.Clear();
                    approachCompleted.Clear();
                    Log.Message("[Colonist Awareness] all holds cleared (setting toggled off)");
                }
                return;
            }
            try { ResumeDisplacedHolds(); } catch { }
            if (holds.Count == 0) return;
            try { Maintain(); } catch { }
        }

        private void ResumeDisplacedHolds()
        {
            if (pendingResume.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            var ids = new List<int>(pendingResume.Keys);
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int k = 0; k < ids.Count; k++)
            {
                PendingResume pending = pendingResume[ids[k]];
                Pawn p = null;
                for (int i = 0; i < colonists.Count; i++)
                    if (colonists[i].thingIDNumber == ids[k])
                    { p = colonists[i]; break; }
                if (p == null || p.Dead || now > pending.ExpiryTick)
                {
                    pendingResume.Remove(ids[k]);
                    continue;
                }
                // A newer standing order supersedes the memory entirely.
                if (CATactical.HasExplicitOrder(p)
                    || CATactical.IsAutomaticDefense(p))
                {
                    pendingResume.Remove(ids[k]);
                    continue;
                }
                // The hand is still down: errand running or pawn drafted.
                if (p.Downed || p.InMentalState || p.Drafted
                    || CATactical.HasForeignPlayerForcedJob(p)) continue;
                pendingResume.Remove(ids[k]);
                var context = CACombatIntent.Restored(p,
                    CAIntentController.Hold, pending.EpisodeId);
                bool resumed = pending.Watch.IsValid
                    ? OrderHoldWatching(p, pending.Cell, pending.Watch, context)
                    : OrderHold(p, pending.Cell, context);
                if (resumed)
                {
                    CATactical.SetHoldProfile(p, pending.Envelope,
                        pending.Grit);
                    CATrace.Pawn(p, "hold RESUMED at " + pending.Cell
                        + " after the player's errand ended",
                        destination: pending.Cell, anchor: p.Position,
                        intent: context);
                }
            }
        }

        private void Maintain()
        {
            var stale = new List<int>();
            var staleReasons = new Dictionary<int, string>();
            var colonists = map.mapPawns.FreeColonistsSpawned;
            // A coordinated deviation may release a second holder while this pass
            // is running. Iterate a detached key/cell snapshot so that legitimate
            // paired ownership transitions cannot invalidate enumeration.
            var holdEntries = new List<KeyValuePair<int, IntVec3>>(holds);
            for (int entryIndex = 0; entryIndex < holdEntries.Count; entryIndex++)
            {
                var kv = holdEntries[entryIndex];
                if (!holds.ContainsKey(kv.Key)) continue;
                Pawn p = null;
                for (int i = 0; i < colonists.Count; i++)
                    if (colonists[i].thingIDNumber == kv.Key) { p = colonists[i]; break; }
                if (p == null || p.Dead || p.Downed || p.InMentalState)
                {
                    stale.Add(kv.Key);
                    if (p != null) staleReasons[kv.Key] = p.Dead ? "actor died"
                        : p.Downed ? "actor was downed" : "mental state took control";
                    continue;
                }
                // Sanity watchdog: if a non-combatant somehow holds a position, that is
                // an unplanned state - release them and report it.
                if (p.DevelopmentalStage != DevelopmentalStage.Adult
                    || p.WorkTagIsDisabled(WorkTags.Violent))
                {
                    Log.Warning("[Colonist Awareness] UNPLANNED: " + p.LabelShort
                        + " was holding a position (child or incapable) - released");
                    stale.Add(kv.Key);
                    staleReasons[kv.Key] = "actor became ineligible for combat hold";
                    continue;
                }
                IntVec3 cell = kv.Value;

                // Needs override intent: a starving or collapsing pawn walks off the line.
                if (p.needs != null && p.needs.food != null && p.needs.food.Starving)
                {
                    CAIntentContext starvationContext;
                    ReportBreak(p, "starving",
                        CATactical.TryGetContext(p, out starvationContext)
                            ? (CAIntentContext?)starvationContext : null);
                    stale.Add(kv.Key);
                    staleReasons[kv.Key] = "starvation override";
                    continue;
                }

                var cur = p.CurJob;

                if (!approachCompleted.Contains(kv.Key)
                    && p.Position.InHorDistOf(cell, 0.9f))
                {
                    approachCompleted.Add(kv.Key);
                    CAIntentContext context;
                    CATrace.Pawn(p,
                        "hold approach COMPLETED; engagement duty may now own the local envelope",
                        destination: cell, anchor: p.Position,
                        intent: CATactical.TryGetContext(p, out context)
                            ? (CAIntentContext?)context : null);
                }

                // The player's hand ALWAYS wins. A foreign queued order was authored
                // after this Hold and releases it. A foreign current order releases it
                // unless the fresh Hold's own entry job is queued behind a temporarily
                // uninterruptible action; in that case the Hold is the newer command
                // and remains pending without letting constant duty bypass the queue.
                if (CATactical.HasForeignPlayerForcedJob(p))
                {
                    // Remember the displaced hold: a direct errand interleaves
                    // with the standing order instead of erasing it.
                    float envelope;
                    int grit;
                    CATactical.TryGetHoldProfile(p, out envelope, out grit);
                    CAIntentContext holdContext;
                    bool hasHoldContext = CATactical.TryGetContext(p,
                        out holdContext);
                    IntVec3 watchPoint;
                    watches.TryGetValue(kv.Key, out watchPoint);
                    pendingResume[kv.Key] = new PendingResume
                    {
                        Cell = cell,
                        Watch = watchPoint,
                        Envelope = envelope,
                        Grit = grit,
                        EpisodeId = hasHoldContext ? holdContext.EpisodeId : 0,
                        ExpiryTick = Find.TickManager.TicksGame + 15000
                    };
                    stale.Add(kv.Key);
                    staleReasons[kv.Key] = "newer sovereign player-forced job "
                        + (cur?.def?.defName ?? "queued order")
                        + "; hold will resume when the errand ends";
                    continue;
                }

                // Environmental hazard displacement - Proactive+ only. A tox
                // cloud on the anchor is not a reason to abandon the intent;
                // it is a reason to hold it from clean ground. Standard
                // holders stay put exactly as ordered.
                if (CABehaviorGate.StableProfileAllows(p,
                        "combat.hold_deviation")
                    && (CALineInterpreter.HazardousCell(map, cell)
                        || CALineInterpreter.HazardousCell(map, p.Position)))
                {
                    int lastShift;
                    if (!hazardShiftTick.TryGetValue(kv.Key, out lastShift)
                        || Find.TickManager.TicksGame - lastShift > 600)
                    {
                        hazardShiftTick[kv.Key] =
                            Find.TickManager.TicksGame;
                        IntVec3 watchPoint;
                        watches.TryGetValue(kv.Key, out watchPoint);
                        IntVec3 clean = FindCleanAnchor(cell, watchPoint);
                        if (clean.IsValid && clean != cell)
                        {
                            CAIntentContext parent;
                            bool ownedHold = CATactical.TryGetContext(p,
                                    out parent)
                                && parent.IsValid
                                && CATactical.MatchesOrder(p,
                                    parent.EpisodeId,
                                    LordJob_CATactical.KindHold);
                            bool foreignOrder =
                                CATactical.HasForeignPlayerForcedJob(p);
                            var gateContext = CABehaviorContext.ForPawn(p,
                                CAAuthorityOrigin.PlayerDelegated,
                                authoritySatisfied: ownedHold,
                                knowledgeSatisfied: true,
                                knowledgeFresh: true,
                                liveValidated: true,
                                knowledgeConfidence: 1f,
                                knowledgeUncertainty: 0f,
                                capabilitySatisfied: clean.Standable(map)
                                    && p.CanReach(clean,
                                        PathEndMode.OnCell, Danger.Deadly),
                                materialSatisfied: true,
                                currentIntentCompatible: ownedHold
                                    && !foreignOrder,
                                directPlayerOwnership: foreignOrder,
                                authorityBasis: ownedHold
                                    ? parent.AuthorityIdentity
                                        ?? "player-authored hold"
                                    : null,
                                knowledgeBasis:
                                    "direct local hazardous-cell observation",
                                owner: "hold episode "
                                    + (ownedHold ? parent.EpisodeId : 0));
                            CABehaviorDecision decision =
                                CABehaviorGate.EvaluateForSelection(
                                    "combat.hold_deviation", gateContext);
                            if (decision.SelectionApproved)
                            {
                                holds[kv.Key] = clean;
                                approachCompleted.Remove(kv.Key);
                                CATactical.Assign(p,
                                    LordJob_CATactical.KindHold,
                                    clean, watchPoint,
                                    CACombatIntent.Continuation(p,
                                        CAIntentController.Hold));
                                CATrace.Pawn(p,
                                    "hold DISPLACED out of harmful gas: anchor "
                                    + cell + " -> " + clean
                                    + "; overwatch intent retained",
                                    destination: clean,
                                    anchor: p.Position,
                                    intent: parent);
                                continue;
                            }
                        }
                    }
                }

                // Deviation triggers - Proactive+ only; piloted holds never self-release.
                CAIncomingEvasionDiagnosticState evasion;
                bool immediateEvasionOwns = CACombatIntent
                    .TryGetIncomingEvasionDiagnosticState(p, out evasion)
                    && (evasion.Status == CAIncomingEvasionResult.Active
                        || evasion.Status == CAIncomingEvasionResult.Pending);
                if (!immediateEvasionOwns
                    && CABehaviorGate.StableProfileAllows(p,
                        "combat.hold_deviation") && Deviate(p, cell))
                {
                    stale.Add(kv.Key);
                    staleReasons[kv.Key] = "autonomous deviation threshold crossed";
                    continue;
                }

                // The DOING is the duty's job now - movement, posture, facing, and
                // engagement all live in CA_HoldPosition. Supervision only from here.
                // Safety net: if the pawn somehow lost lord membership without a
                // release (external mod, odd cleanup), the hold is dead - drop it.
                if (!CATactical.IsHold(p))
                {
                    stale.Add(kv.Key);
                    staleReasons[kv.Key] = "tactical lord membership was lost";
                    continue;
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                var colonists2 = map.mapPawns.FreeColonistsSpawned;
                for (int j = 0; j < colonists2.Count; j++)
                    if (colonists2[j].thingIDNumber == stale[i])
                    {
                        if (CATactical.IsHold(colonists2[j]))
                        {
                            string reason;
                            staleReasons.TryGetValue(stale[i], out reason);
                            CATactical.Release(colonists2[j], reason: reason);
                        }
                        break;
                    }
                holds.Remove(stale[i]);
                watches.Remove(stale[i]);
                approachCompleted.Remove(stale[i]);
            }
        }

        // Doctrine presets scale every deviation trigger. Standard is the
        // historical behavior byte-for-byte; Yielding breaks earlier, Stubborn
        // later, Last stand never self-deviates (starvation, downing, mental
        // state, and the player's hand still end the hold in Maintain()).
        internal static string GritLabel(int grit)
        {
            switch (grit)
            {
                case LordJob_CATactical.GritYielding: return "Yielding";
                case LordJob_CATactical.GritStubborn: return "Stubborn";
                case LordJob_CATactical.GritLastStand: return "Last stand";
                default: return "Standard";
            }
        }

        internal static readonly float[] EnvelopePresets = { 3f, 6f, 10f, 14f };

        internal static string EnvelopeLabel(float envelope)
        {
            if (envelope <= 3f) return "Tight";
            if (envelope <= 6f) return "Standard";
            if (envelope <= 10f) return "Elastic";
            return "Wide";
        }

        // Holding turns injudicious: melee on top of a shooter, bleeding out, flanked
        // from off-axis, or the local position collapsing. Break, fall back firing, report.
        private bool Deviate(Pawn p, IntVec3 cell)
        {
            // Remembered geometry may guide an active escape, but a stale contact
            // alone must not pull an injured holder out of calm recovery or a
            // finite emergency self-tend between exchanges.
            if (!CACombatThreat.PerceivesActiveThreat(p)) return false;
            // With Knowledge enabled this is a copied, pawn-private fact set. Live
            // pawns are attached only when that remembered hostile is visible now;
            // remembered cells remain the source for tactical geometry.
            var threats = PerceivedThreats(p);
            if (threats.Count == 0) return false;

            // Profile lookup costs a lord-row scan; pay it only once threats
            // put a deviation genuinely in question.
            float envelope;
            int grit;
            CATactical.TryGetHoldProfile(p, out envelope, out grit);
            if (grit == LordJob_CATactical.GritLastStand) return false;

            string why = null;
            bool yielding = grit == LordJob_CATactical.GritYielding;
            bool stubborn = grit == LordJob_CATactical.GritStubborn;

            // Raw bleed rate is not a retreat order. Use the same continuous combat-
            // power axes as the rest of CA so a manageable wound does not repeatedly
            // cancel a fresh player-authored formation while a vital injury, imminent
            // blood-loss collapse, or severe capacity loss still can.
            CACombatConditionSnapshot condition =
                CACombatConditionSnapshot.Capture(p);
            bool viabilityFailure;
            if (yielding)
                viabilityFailure = condition.DeathInTicks < 27000
                    || condition.CombatPower < 0.58f
                    || condition.Pain >= 0.68f
                    || condition.Consciousness < 0.66f
                    || condition.Moving < 0.46f
                    || condition.Manipulation < 0.46f
                    || condition.Breathing < 0.72f
                    || condition.BloodPumping < 0.70f
                    || condition.VitalInjury && condition.CombatPower < 0.72f;
            else if (stubborn)
                viabilityFailure = condition.DeathInTicks < 9000
                    || condition.CombatPower < 0.36f
                    || condition.Pain >= 0.88f
                    || condition.Consciousness < 0.44f
                    || condition.Moving < 0.26f
                    || condition.Manipulation < 0.26f
                    || condition.Breathing < 0.50f
                    || condition.BloodPumping < 0.50f
                    || condition.VitalInjury && condition.CombatPower < 0.45f;
            else
                viabilityFailure = condition.DeathInTicks < 18000
                    || condition.CombatPower < 0.48f
                    || condition.Pain >= 0.78f
                    || condition.Consciousness < 0.58f
                    || condition.Moving < 0.38f
                    || condition.Manipulation < 0.38f
                    || condition.Breathing < 0.64f
                    || condition.BloodPumping < 0.62f
                    || condition.VitalInjury && condition.CombatPower < 0.60f;
            if (viabilityFailure)
                why = "health-adjusted combat viability failed; "
                    + condition.TraceText();

            // Melee walking onto a shooter.
            if (why == null && p.equipment != null && p.equipment.Primary != null
                && p.equipment.Primary.def.IsRangedWeapon)
            {
                float overrunRange = yielding ? 3.5f : 2.5f;
                for (int i = 0; i < threats.Count; i++)
                {
                    Pawn visible = threats[i].VisiblePawn;
                    if (visible != null && visible.Position.InHorDistOf(p.Position, overrunRange))
                    { why = "overrun"; break; }
                }
            }

            // Flanked: remembered cells define the threat axis; only a hostile in
            // current LOS can establish the immediate off-axis flank.
            if (why == null)
            {
                var centroid = IntVec3.Zero;
                int n = 0;
                for (int i = 0; i < threats.Count; i++)
                    if (threats[i].RememberedCell.InHorDistOf(cell, 45f))
                    { centroid += threats[i].RememberedCell; n++; }
                if (n > 0)
                {
                    float flankRange = yielding ? 12.9f : stubborn ? 6.9f : 9.9f;
                    var axis = ((centroid.ToVector3() / n) - cell.ToVector3()).normalized;
                    for (int i = 0; i < threats.Count; i++)
                    {
                        var h = threats[i].VisiblePawn;
                        if (h == null) continue;
                        if (!h.Position.InHorDistOf(p.Position, flankRange)) continue;
                        var dir = (h.Position.ToVector3() - p.Position.ToVector3()).normalized;
                        if (UnityEngine.Vector3.Dot(dir, axis) > 0.35f) continue; // frontal - the line handles it
                        why = "flanked";
                        break;
                    }
                }
            }

            // Position collapsing: a friendly down beside them with hostiles pressing.
            if (why == null)
            {
                int pressingNeeded = yielding ? 1 : stubborn ? 3 : 2;
                var colonists = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < colonists.Count; i++)
                {
                    var c = colonists[i];
                    if (c == p || !c.Downed) continue;
                    if (!c.Position.InHorDistOf(p.Position, 6.9f)) continue;
                    int pressing = 0;
                    for (int j = 0; j < threats.Count; j++)
                        if (threats[j].RememberedCell.InHorDistOf(p.Position, 14f)) pressing++;
                    if (pressing >= pressingNeeded) { why = "position collapsing"; break; }
                }
            }

            if (why == null) return false;
            if (grit != LordJob_CATactical.GritStandard)
                why += "; doctrine=" + GritLabel(grit).ToLower();

            CAIntentContext priorHoldContext;
            bool hasPriorHoldContext = CATactical.TryGetContext(p,
                out priorHoldContext);

            // Fall back firing - toward the painted fallback line when one
            // exists, otherwise toward home ground away from the pressure.
            IntVec3 threat = NearestThreatCell(threats, p.Position);
            IntVec3 dest = IntVec3.Invalid;
            bool fallbackRoute = false;
            var overlay = CAOverlayMapComponent.For(map);
            if (overlay != null)
            {
                IntVec3 rally = overlay.NearestCell(
                    CAOverlayKind.FallbackLine, p.Position, 60f);
                if (rally.IsValid)
                {
                    IntVec3 stand = rally.Standable(map) ? rally
                        : CellFinder.StandableCellNear(rally, map, 3f, null);
                    if (stand.IsValid && p.CanReach(stand,
                        PathEndMode.OnCell, Danger.Deadly))
                    {
                        dest = stand;
                        fallbackRoute = true;
                    }
                }
            }
            if (!dest.IsValid)
            {
                var awayDir = (p.Position.ToVector3() - threat.ToVector3()).normalized;
                var rough = p.Position + IntVec3.FromVector3(awayDir * 14f);
                dest = CellFinder.StandableCellNear(rough, map, 6f, null);
            }
            if (!dest.IsValid) dest = p.Position;
            var wd = map.GetComponent<WithdrawalMapComponent>();
            PerceivedHoldThreat evidence = default(PerceivedHoldThreat);
            bool hasLiveEvidence = false;
            float nearestEvidence = float.MaxValue;
            for (int i = 0; i < threats.Count; i++)
            {
                if (threats[i].VisiblePawn == null) continue;
                float distance = p.Position.DistanceTo(
                    threats[i].VisiblePawn.Position);
                if (distance >= nearestEvidence) continue;
                nearestEvidence = distance;
                evidence = threats[i];
                hasLiveEvidence = true;
            }
            int now = Find.TickManager?.TicksGame ?? 0;
            int evidenceAge = hasLiveEvidence
                ? System.Math.Max(0, now - evidence.SourceTick) : 0;
            bool ownedHold = hasPriorHoldContext
                && CATactical.MatchesOrder(p,
                    priorHoldContext.EpisodeId,
                    LordJob_CATactical.KindHold);
            bool foreignOrder = CATactical.HasForeignPlayerForcedJob(p);
            var gateContext = CABehaviorContext.ForPawn(p,
                CAAuthorityOrigin.PlayerDelegated,
                authoritySatisfied: ownedHold,
                knowledgeSatisfied: hasLiveEvidence,
                knowledgeFresh: hasLiveEvidence && evidenceAge <= 300,
                liveValidated: hasLiveEvidence,
                knowledgeRelayed: hasLiveEvidence && evidence.Relayed,
                knowledgeAgeTicks: evidenceAge,
                knowledgeConfidence: hasLiveEvidence
                    ? evidence.Confidence : 0f,
                knowledgeUncertainty: hasLiveEvidence
                    ? evidence.Uncertainty : 0f,
                capabilitySatisfied: wd != null && dest != p.Position
                    && wd.CanOfferSolo(p, dest),
                materialSatisfied: dest.IsValid && dest.Standable(map),
                currentIntentCompatible: ownedHold && !foreignOrder,
                directPlayerOwnership: foreignOrder,
                authorityBasis: ownedHold
                    ? priorHoldContext.AuthorityIdentity
                        ?? "player-authored hold"
                    : null,
                knowledgeBasis: hasLiveEvidence
                    ? (evidence.Relayed ? "relayed" : "direct")
                        + " current threat at "
                        + evidence.VisiblePawn.Position
                    : "no currently visible actor-held threat fact",
                owner: "hold episode "
                    + (ownedHold ? priorHoldContext.EpisodeId : 0));
            CABehaviorDecision deviationDecision = CABehaviorGate
                .EvaluateForSelection(
                "combat.hold_deviation", gateContext);
            if (!deviationDecision.SelectionApproved)
            {
                CATrace.Skip(p, "hold deviation",
                    deviationDecision.PrimaryReason,
                    destination: dest, anchor: p.Position,
                    intent: hasPriorHoldContext
                        ? (CAIntentContext?)priorHoldContext : null);
                return false;
            }

            if (wd != null && dest != p.Position)
            {
                if (fallbackRoute) why += "; withdrawing to the fallback line";
                Pawn buddy = wd.FindBuddyForHoldDeviation(p);
                CAIntentContext context = CACombatIntent.Continuation(p,
                    CAIntentController.Withdrawal, buddy != null
                        ? "combat.withdrawal_coordinated"
                        : "combat.withdrawal_self_preservation");
                if (buddy != null)
                    wd.OrderCoveredFromHold(p, buddy, dest, context);
                else wd.OrderSolo(p, dest, context);
            }
            // The fold is a squad event: a capable teammate covers the
            // withdrawal corridor instead of the squad learning by autopsy.
            var support = SquadSupportMapComponent.For(map);
            if (support != null)
                support.NotifyPositionFold(p, p.Position, dest);
            ReportBreak(p, why, hasPriorHoldContext
                ? (CAIntentContext?)priorHoldContext : null);
            return true;
        }

        // Autonomous mode selects its own hold profile: envelope from the
        // weapon's reach, doctrine from the ground - stubborn on a painted
        // objective, yielding when isolated, standard otherwise. The player's
        // gizmo hand pins a profile and the advisor never overwrites it.
        private void AdviseProfile(Pawn p, IntVec3 cell)
        {
            var verb = p.equipment?.PrimaryEq?.PrimaryVerb;
            bool ranged = verb != null && !verb.verbProps.IsMeleeAttack;
            float envelope = !ranged ? 3f
                : verb.verbProps.range >= 30f ? 10f
                : verb.verbProps.range >= 20f ? 6f : 3f;
            // The selected defense posture is the baseline; the situation
            // still speaks - a painted objective hardens any doctrine, and
            // isolation softens it. Law sets the default posture, not fate.
            string holdLaw = CAPolicyLookup.Colony("defense posture");
            int grit = holdLaw == "cautious"
                ? LordJob_CATactical.GritYielding
                : holdLaw == "tenacious"
                ? LordJob_CATactical.GritStubborn
                : LordJob_CATactical.GritStandard;
            var overlay = CAOverlayMapComponent.For(map);
            bool onObjective = overlay != null
                && (overlay.NearestCell(CAOverlayKind.PrimaryObjective,
                        cell, 12f).IsValid
                    || overlay.NearestCell(CAOverlayKind.Objective,
                        cell, 12f).IsValid);
            if (onObjective)
                grit = LordJob_CATactical.GritStubborn;
            else
            {
                bool mateNear = false;
                var colonists = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < colonists.Count; i++)
                {
                    var mate = colonists[i];
                    if (mate == p || mate.Downed
                        || mate.WorkTagIsDisabled(WorkTags.Violent)) continue;
                    if (mate.Position.InHorDistOf(cell, 30f))
                    { mateNear = true; break; }
                }
                if (!mateNear) grit = LordJob_CATactical.GritYielding;
            }
            CATactical.SetHoldProfile(p, envelope, grit);
            CATrace.Pawn(p, "hold profile self-selected: "
                + EnvelopeLabel(envelope).ToLower() + " envelope, "
                + GritLabel(grit).ToLower() + " doctrine"
                + (onObjective ? " (painted objective)" : ""),
                destination: cell, anchor: p.Position);
        }

        private List<PerceivedHoldThreat> PerceivedThreats(Pawn p)
        {
            var result = new List<PerceivedHoldThreat>();
            var settings = AwarenessMod.Settings;
            bool knowledgeOn = settings != null && settings.knowledgeContacts;
            if (knowledgeOn)
            {
                var know = KnowledgeMapComponent.For(map);
                if (know == null) return result;

                // FreshContacts returns snapshots. Dedupe defensively so a repeated
                // hostile id contributes only its newest source fact.
                var latest = new Dictionary<int, ThreatContactSnapshot>();
                var contacts = know.FreshContacts(p);
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
                {
                    if (!contact.Cell.IsValid || !contact.Cell.InBounds(map)) continue;
                    result.Add(new PerceivedHoldThreat(contact.Cell,
                        VisibleHostileById(p, contact.HostileId),
                        contact.SourceTick, !contact.Evidence.IsDirect,
                        contact.Evidence.Confidence,
                        contact.Evidence.Uncertainty));
                }
                return result;
            }

            // Knowledge disabled: tactical geometry may use only hostiles the holder
            // currently sees, without remembered or relayed positions.
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var hostile = pawns[i];
                if (!KnowledgeMapComponent.CanCurrentlySeeHostile(
                    p, hostile, 42f)) continue;
                result.Add(new PerceivedHoldThreat(hostile.Position, hostile,
                    Find.TickManager?.TicksGame ?? 0, false, 1f, 0f));
            }
            return result;
        }

        private Pawn VisibleHostileById(Pawn observer, int hostileId)
        {
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var hostile = pawns[i];
                if (hostile.thingIDNumber != hostileId) continue;
                return KnowledgeMapComponent.CanCurrentlySeeHostile(
                    observer, hostile, 42f) ? hostile : null;
            }
            return null;
        }

        // The report-up surface: the break is announced, through the squad leader when
        // one is in command contact.
        private void ReportBreak(Pawn p, string why,
            CAIntentContext? priorHoldContext)
        {
            string msg = p.LabelShort + " breaks off the hold: " + why + ".";
            var leader = SquadComponent.LeaderPawn(SquadComponent.SquadOf(p), map);
            if (leader != null && leader != p && !leader.Dead && CommsModule.CanCommand(leader, p))
                msg += " Reported to " + leader.LabelShort + ".";
            Messages.Message(msg, p, MessageTypeDefOf.NeutralEvent, false);
            CATrace.Pawn(p, "hold deviation TRIGGERED - " + why
                + (leader != null && leader != p && !leader.Dead
                    && CommsModule.CanCommand(leader, p)
                    ? "; report delivered to actor " + leader.thingIDNumber
                    : "; no command report delivered"),
                anchor: p.Position,
                intent: priorHoldContext);
        }

        private static IntVec3 NearestThreatCell(List<PerceivedHoldThreat> threats, IntVec3 pos)
        {
            IntVec3 best = threats[0].RememberedCell;
            float bd = float.MaxValue;
            for (int i = 0; i < threats.Count; i++)
            {
                float d = pos.DistanceTo(threats[i].RememberedCell);
                if (d < bd) { bd = d; best = threats[i].RememberedCell; }
            }
            return best;
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_HoldGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (var g in gizmos) yield return g;
            var p = __instance;
            var s = AwarenessMod.Settings;
            if (s == null || !s.holdOrders) yield break;
            if (p == null || !p.IsColonistPlayerControlled || p.Map == null) yield break;
            var hold = p.Map.GetComponent<HoldMapComponent>();
            if (hold == null || !hold.IsHolding(p)) yield break;
            float envelope;
            int grit;
            CATactical.TryGetHoldProfile(p, out envelope, out grit);
            yield return new Command_Action
            {
                defaultLabel = "Envelope: "
                    + HoldMapComponent.EnvelopeLabel(envelope)
                    + " (" + ((int)envelope) + ")",
                defaultDesc = "How far from the anchor this holder may drift to "
                    + "engage, take cover, and intercept before returning. "
                    + "Cycles tight (3), standard (6), elastic (10), wide (14).",
                icon = TexCommand.HoldOpen,
                action = delegate
                {
                    int idx = 0;
                    for (int i = 0; i < HoldMapComponent.EnvelopePresets.Length; i++)
                        if (UnityEngine.Mathf.Abs(
                            HoldMapComponent.EnvelopePresets[i] - envelope) < 0.5f)
                        { idx = i; break; }
                    float next = HoldMapComponent.EnvelopePresets[
                        (idx + 1) % HoldMapComponent.EnvelopePresets.Length];
                    CATactical.SetHoldProfile(p, next, grit, pin: true);
                    CATrace.Pawn(p, "hold envelope -> "
                        + HoldMapComponent.EnvelopeLabel(next).ToLower()
                        + " (" + ((int)next) + " cells)", anchor: p.Position);
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "Doctrine: " + HoldMapComponent.GritLabel(grit),
                defaultDesc = "How much punishment this holder absorbs before "
                    + "breaking off on their own. Yielding breaks early, standard "
                    + "is the default judgment, tenacious holds through heavy "
                    + "wounds, last stand never self-releases (starvation, "
                    + "downing, and your direct orders still end it).",
                icon = TexCommand.Draft,
                action = delegate
                {
                    int next = (grit + 1) % 4;
                    CATactical.SetHoldProfile(p, envelope, next, pin: true);
                    CATrace.Pawn(p, "defense posture -> "
                        + HoldMapComponent.GritLabel(next).ToLower(),
                        anchor: p.Position);
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "Release hold",
                defaultDesc = "Stop holding the position and return to normal duties.",
                icon = TexCommand.ClearPrioritizedWork,
                action = delegate { hold.Release(p); }
            };
        }
    }
}
