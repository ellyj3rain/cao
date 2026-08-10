using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // The standing tactical lord: one per map, owning every pawn with a standing
    // individual order (hold, overwatch, ambush, hide). Duties only run for pawns in
    // a Lord - this is the vehicle that lets those orders ride the game's native duty
    // pipeline instead of job-stomping loops. Members with no active fight live their
    // normal lives THROUGH the duty subtree (needs inside the leash); drafting or a
    // direct player order shadows the duty and it resumes when the hand lifts. The
    // lord scribes, so standing orders now survive saves natively.
    public class LordJob_CATactical : LordJob
    {
        private const int CurrentOrderSchemaVersion = 2;
        public const int KindHold = 0;
        public const int KindAmbush = 1;
        public const int KindHide = 2;
        public const int KindAmbushSubdue = 3;
        public const int KindRaidDefense = 4;

        // Hold doctrine: how much punishment a holder absorbs before the
        // deviation triggers may break the hold. Standard is the historical
        // behavior; Last stand never self-releases (starvation, downing, and
        // the player's hand still end it).
        public const int GritYielding = 0;
        public const int GritStandard = 1;
        public const int GritStubborn = 2;
        public const int GritLastStand = 3;
        public const float DefaultHoldEnvelope = 6f;

        private List<Pawn> orderPawns = new List<Pawn>();
        private List<int> orderKinds = new List<int>();
        private List<IntVec3> orderCells = new List<IntVec3>();
        private List<IntVec3> orderWatches = new List<IntVec3>();
        private List<int> orderEpisodes = new List<int>();
        private List<int> orderOrigins = new List<int>();
        private List<int> orderControllers = new List<int>();
        private List<int> orderIssuers = new List<int>();
        // True only when CA changed Fire-at-will from on to off for this
        // concealment order. The ownership bit is scribed so a load can restore
        // the player's original setting when the concealment ends.
        private List<bool> orderFireSuppressed = new List<bool>();
        // Per-hold customization: engagement envelope radius and doctrine.
        // Rows exist for every order; only KindHold consumes them.
        private List<float> orderEnvelopes = new List<float>();
        private List<int> orderGrits = new List<int>();
        // True once the player pins a profile by hand; the Autonomous advisor
        // only writes unpinned profiles.
        private List<bool> orderProfilePinned = new List<bool>();
        private int orderSchemaVersion = CurrentOrderSchemaVersion;
        private bool legacyFireSuppressionUnknown;

        private LordToil_CATactical toil;

        public override bool CanAutoAddPawns => false;

        public int OrderCount => orderPawns.Count;

        public bool TryGetOrder(Pawn p, out int kind, out IntVec3 cell, out IntVec3 watch)
        {
            int i = orderPawns.IndexOf(p);
            if (i >= 0 && i < orderKinds.Count)
            {
                kind = orderKinds[i];
                cell = orderCells[i];
                watch = orderWatches[i];
                return true;
            }
            kind = -1;
            cell = IntVec3.Invalid;
            watch = IntVec3.Invalid;
            return false;
        }

        public void SetOrder(Pawn p, int kind, IntVec3 cell, IntVec3 watch,
            CAIntentContext context)
        {
            int i = orderPawns.IndexOf(p);
            if (i >= 0)
            {
                orderKinds[i] = kind;
                orderCells[i] = cell;
                orderWatches[i] = watch;
                EnsureContextRows();
                orderEpisodes[i] = context.EpisodeId;
                orderOrigins[i] = (int)context.Origin;
                orderControllers[i] = (int)context.Controller;
                orderIssuers[i] = context.IssuerId;
                // Updating an existing concealment row preserves the Fire-at-will
                // change CA already owns. Reveal/release is the transaction that
                // restores the player setting and clears this persisted bit.
            }
            else
            {
                orderPawns.Add(p);
                orderKinds.Add(kind);
                orderCells.Add(cell);
                orderWatches.Add(watch);
                orderEpisodes.Add(context.EpisodeId);
                orderOrigins.Add((int)context.Origin);
                orderControllers.Add((int)context.Controller);
                orderIssuers.Add(context.IssuerId);
                orderFireSuppressed.Add(false);
                orderEnvelopes.Add(DefaultHoldEnvelope);
                orderGrits.Add(GritStandard);
                orderProfilePinned.Add(false);
            }
        }

        public bool IsHoldProfilePinned(Pawn p)
        {
            int i = orderPawns.IndexOf(p);
            EnsureContextRows();
            return i >= 0 && i < orderProfilePinned.Count
                && orderProfilePinned[i];
        }

        public void PinHoldProfile(Pawn p)
        {
            int i = orderPawns.IndexOf(p);
            EnsureContextRows();
            if (i >= 0 && i < orderProfilePinned.Count)
                orderProfilePinned[i] = true;
        }

        // Updating an existing row (re-anchoring a hold) keeps its envelope and
        // doctrine; only a brand-new order starts from the defaults.
        public bool TryGetHoldProfile(Pawn p, out float envelope, out int grit)
        {
            int i = orderPawns.IndexOf(p);
            EnsureContextRows();
            if (i >= 0 && i < orderEnvelopes.Count && i < orderGrits.Count)
            {
                envelope = orderEnvelopes[i];
                grit = orderGrits[i];
                return true;
            }
            envelope = DefaultHoldEnvelope;
            grit = GritStandard;
            return false;
        }

        public void SetHoldProfile(Pawn p, float envelope, int grit)
        {
            int i = orderPawns.IndexOf(p);
            EnsureContextRows();
            if (i < 0 || i >= orderEnvelopes.Count || i >= orderGrits.Count)
                return;
            orderEnvelopes[i] = UnityEngine.Mathf.Clamp(envelope, 1f, 20f);
            orderGrits[i] = UnityEngine.Mathf.Clamp(grit, GritYielding,
                GritLastStand);
            AssignDuty(p);
        }

        public bool TryGetContext(Pawn p, out CAIntentContext context)
        {
            int i = orderPawns.IndexOf(p);
            EnsureContextRows();
            if (i >= 0 && i < orderEpisodes.Count && orderEpisodes[i] > 0)
            {
                context = new CAIntentContext(orderEpisodes[i],
                    (CAIntentOrigin)orderOrigins[i],
                    (CAIntentController)orderControllers[i], orderIssuers[i]);
                return true;
            }
            context = default(CAIntentContext);
            return false;
        }

        public bool FireSuppressionOwned(Pawn p)
        {
            int i = orderPawns.IndexOf(p);
            EnsureContextRows();
            return i >= 0 && i < orderFireSuppressed.Count
                && orderFireSuppressed[i];
        }

        public bool FireSuppressionOwnershipKnown =>
            !legacyFireSuppressionUnknown;

        public void SetFireSuppressionOwned(Pawn p, bool owned)
        {
            int i = orderPawns.IndexOf(p);
            EnsureContextRows();
            if (i >= 0 && i < orderFireSuppressed.Count)
                orderFireSuppressed[i] = owned;
        }

        public void RemoveOrder(Pawn p)
        {
            int i = orderPawns.IndexOf(p);
            if (i < 0) return;
            EnsureContextRows();
            orderPawns.RemoveAt(i);
            orderKinds.RemoveAt(i);
            orderCells.RemoveAt(i);
            orderWatches.RemoveAt(i);
            orderEpisodes.RemoveAt(i);
            orderOrigins.RemoveAt(i);
            orderControllers.RemoveAt(i);
            orderIssuers.RemoveAt(i);
            orderFireSuppressed.RemoveAt(i);
            if (i < orderEnvelopes.Count) orderEnvelopes.RemoveAt(i);
            if (i < orderGrits.Count) orderGrits.RemoveAt(i);
            if (i < orderProfilePinned.Count) orderProfilePinned.RemoveAt(i);
        }

        // Snapshot for state rebuilds after load (registries are session-only).
        public void CopyOrders(List<Pawn> pawns, List<int> kinds, List<IntVec3> cells, List<IntVec3> watches)
        {
            pawns.AddRange(orderPawns);
            kinds.AddRange(orderKinds);
            cells.AddRange(orderCells);
            watches.AddRange(orderWatches);
        }

        public void AssignDuty(Pawn p)
        {
            int kind;
            IntVec3 cell, watch;
            if (!TryGetOrder(p, out kind, out cell, out watch) || p.mindState == null) return;
            CAIntentContext context;
            bool hasContext = TryGetContext(p, out context);
            PawnDuty duty;
            if (kind == KindHold || kind == KindRaidDefense)
            {
                duty = new PawnDuty(kind == KindRaidDefense
                    ? CA_Defs.DefensivePosture : CA_Defs.HoldPosition, cell);
                float envelope = DefaultHoldEnvelope;
                int grit;
                if (kind == KindHold) TryGetHoldProfile(p, out envelope, out grit);
                duty.radius = envelope;
                duty.locomotion = LocomotionUrgency.Sprint;
                if (watch.IsValid)
                {
                    // focusSecond alone: the engagement duty scans toward the
                    // watch point. overrideFacing is deliberately NOT set - a
                    // duty-wide facing lock makes the pawn moonwalk backwards
                    // through the whole approach; combat stances face targets
                    // on their own.
                    duty.focusSecond = watch;
                }
            }
            else
            {
                duty = new PawnDuty(CA_Defs.StackPosition, cell);
                duty.locomotion = LocomotionUrgency.Sprint;
            }
            if (DutyMatches(p.mindState.duty, duty)) return;
            p.mindState.duty = duty;
            CATrace.Pawn(p, (kind == KindHold ? "hold"
                : kind == KindRaidDefense ? "raid defense"
                : kind == KindHide ? "hide" : "ambush")
                + " duty -> " + cell, destination: cell, anchor: cell,
                contact: watch.IsValid ? (IntVec3?)watch : null,
                intent: hasContext ? (CAIntentContext?)context : null);
            LordJob_CAStackBreach.PushDuty(p);
        }

        private static bool DutyMatches(PawnDuty current, PawnDuty proposed)
        {
            if (current == null || proposed == null
                || current.def != proposed.def
                || current.focus.IsValid != proposed.focus.IsValid
                || current.focusSecond.IsValid != proposed.focusSecond.IsValid
                || current.overrideFacing != proposed.overrideFacing
                || current.locomotion != proposed.locomotion
                || UnityEngine.Mathf.Abs(current.radius - proposed.radius)
                    > 0.001f) return false;
            if (current.focus.IsValid
                && current.focus.Cell != proposed.focus.Cell) return false;
            if (current.focusSecond.IsValid
                && current.focusSecond.Cell != proposed.focusSecond.Cell)
                return false;
            return true;
        }

        public override StateGraph CreateGraph()
        {
            var g = new StateGraph();
            toil = new LordToil_CATactical();
            g.AddToil(toil);
            return g;
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            RemoveOrder(p);
        }

        public override string GetReport(Pawn pawn)
        {
            int kind;
            IntVec3 cell, watch;
            if (!TryGetOrder(pawn, out kind, out cell, out watch)) return null;
            if (kind == KindHold)
            {
                float envelope;
                int grit;
                TryGetHoldProfile(pawn, out envelope, out grit);
                string report = watch.IsValid ? "Holding - overwatch"
                    : "Holding position";
                if (grit != GritStandard)
                    report += " (" + HoldMapComponent.GritLabel(grit).ToLower() + ")";
                return report;
            }
            if (kind == KindRaidDefense) return "Defending against a known contact";
            if (kind == KindHide) return "Hiding";
            return "In ambush";
        }

        public override IEnumerable<Gizmo> GetPawnGizmos(Pawn p)
        {
            int kind;
            IntVec3 cell, watch;
            if (!TryGetOrder(p, out kind, out cell, out watch)) yield break;
            if (kind == KindRaidDefense) yield break;
            yield return new Command_Action
            {
                defaultLabel = kind == KindHold ? "Release hold" : "Break off",
                defaultDesc = "End the standing order - back to their own work.",
                icon = TexCommand.ClearPrioritizedWork,
                action = delegate
                {
                    if (kind == KindHold)
                    {
                        HoldMapComponent hold = p.Map != null
                            ? p.Map.GetComponent<HoldMapComponent>() : null;
                        if (hold != null) hold.Release(p);
                        else CATactical.Release(p);
                    }
                    else HiddenRegistry.Reveal(p);
                }
            };
        }

        public override bool EndPawnJobOnCleanup(Pawn p)
        {
            return p.CurJob == null || !p.CurJob.playerForced;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref orderPawns, "orderPawns", LookMode.Reference);
            Scribe_Collections.Look(ref orderKinds, "orderKinds", LookMode.Value);
            Scribe_Collections.Look(ref orderCells, "orderCells", LookMode.Value);
            Scribe_Collections.Look(ref orderWatches, "orderWatches", LookMode.Value);
            Scribe_Collections.Look(ref orderEpisodes, "orderEpisodes", LookMode.Value);
            Scribe_Collections.Look(ref orderOrigins, "orderOrigins", LookMode.Value);
            Scribe_Collections.Look(ref orderControllers, "orderControllers", LookMode.Value);
            Scribe_Collections.Look(ref orderIssuers, "orderIssuers", LookMode.Value);
            Scribe_Collections.Look(ref orderFireSuppressed, "orderFireSuppressed",
                LookMode.Value);
            Scribe_Collections.Look(ref orderEnvelopes, "orderEnvelopes",
                LookMode.Value);
            Scribe_Collections.Look(ref orderGrits, "orderGrits", LookMode.Value);
            Scribe_Collections.Look(ref orderProfilePinned, "orderProfilePinned",
                LookMode.Value);
            Scribe_Values.Look(ref orderSchemaVersion, "orderSchemaVersion", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Fire-suppression rows arrived with schema 1; hold profiles with
                // schema 2 and simply backfill defaults, so only pre-1 saves carry
                // unknown fire-suppression ownership.
                legacyFireSuppressionUnknown = orderSchemaVersion < 1;
                orderSchemaVersion = CurrentOrderSchemaVersion;
                if (orderPawns == null) orderPawns = new List<Pawn>();
                if (orderKinds == null) orderKinds = new List<int>();
                if (orderCells == null) orderCells = new List<IntVec3>();
                if (orderWatches == null) orderWatches = new List<IntVec3>();
                if (orderEpisodes == null) orderEpisodes = new List<int>();
                if (orderOrigins == null) orderOrigins = new List<int>();
                if (orderControllers == null) orderControllers = new List<int>();
                if (orderIssuers == null) orderIssuers = new List<int>();
                if (orderFireSuppressed == null)
                    orderFireSuppressed = new List<bool>();
                if (orderEnvelopes == null) orderEnvelopes = new List<float>();
                if (orderGrits == null) orderGrits = new List<int>();
                if (orderProfilePinned == null)
                    orderProfilePinned = new List<bool>();
                // Drop rows whose pawn reference died with the save.
                for (int i = orderPawns.Count - 1; i >= 0; i--)
                {
                    if (orderPawns[i] != null && i < orderKinds.Count
                        && i < orderCells.Count && i < orderWatches.Count) continue;
                    if (i < orderPawns.Count) orderPawns.RemoveAt(i);
                    if (i < orderKinds.Count) orderKinds.RemoveAt(i);
                    if (i < orderCells.Count) orderCells.RemoveAt(i);
                    if (i < orderWatches.Count) orderWatches.RemoveAt(i);
                    if (i < orderEpisodes.Count) orderEpisodes.RemoveAt(i);
                    if (i < orderOrigins.Count) orderOrigins.RemoveAt(i);
                    if (i < orderControllers.Count) orderControllers.RemoveAt(i);
                    if (i < orderIssuers.Count) orderIssuers.RemoveAt(i);
                    if (i < orderFireSuppressed.Count)
                        orderFireSuppressed.RemoveAt(i);
                    if (i < orderEnvelopes.Count) orderEnvelopes.RemoveAt(i);
                    if (i < orderGrits.Count) orderGrits.RemoveAt(i);
                    if (i < orderProfilePinned.Count)
                        orderProfilePinned.RemoveAt(i);
                }
                EnsureContextRows();
                for (int i = 0; i < orderEpisodes.Count; i++)
                {
                    if (orderEpisodes[i] <= 0 && orderPawns[i] != null)
                    {
                        CAIntentContext restored = CACombatIntent.Restored(
                            orderPawns[i], CATactical.ControllerFor(orderKinds[i]), 0);
                        orderEpisodes[i] = restored.EpisodeId;
                        orderOrigins[i] = (int)restored.Origin;
                        orderControllers[i] = (int)restored.Controller;
                        orderIssuers[i] = restored.IssuerId;
                    }
                    CACombatIntent.ObserveEpisode(orderEpisodes[i]);
                }
            }
        }

        private void EnsureContextRows()
        {
            if (orderEpisodes == null) orderEpisodes = new List<int>();
            if (orderOrigins == null) orderOrigins = new List<int>();
            if (orderControllers == null) orderControllers = new List<int>();
            if (orderIssuers == null) orderIssuers = new List<int>();
            if (orderFireSuppressed == null)
                orderFireSuppressed = new List<bool>();
            if (orderEnvelopes == null) orderEnvelopes = new List<float>();
            if (orderGrits == null) orderGrits = new List<int>();
            if (orderProfilePinned == null) orderProfilePinned = new List<bool>();
            while (orderEpisodes.Count < orderPawns.Count) orderEpisodes.Add(0);
            while (orderOrigins.Count < orderPawns.Count) orderOrigins.Add(0);
            while (orderControllers.Count < orderPawns.Count) orderControllers.Add(0);
            while (orderIssuers.Count < orderPawns.Count) orderIssuers.Add(-1);
            while (orderFireSuppressed.Count < orderPawns.Count)
                orderFireSuppressed.Add(false);
            while (orderEnvelopes.Count < orderPawns.Count)
                orderEnvelopes.Add(DefaultHoldEnvelope);
            while (orderGrits.Count < orderPawns.Count)
                orderGrits.Add(GritStandard);
            while (orderProfilePinned.Count < orderPawns.Count)
                orderProfilePinned.Add(false);
            while (orderEpisodes.Count > orderPawns.Count)
                orderEpisodes.RemoveAt(orderEpisodes.Count - 1);
            while (orderOrigins.Count > orderPawns.Count)
                orderOrigins.RemoveAt(orderOrigins.Count - 1);
            while (orderControllers.Count > orderPawns.Count)
                orderControllers.RemoveAt(orderControllers.Count - 1);
            while (orderIssuers.Count > orderPawns.Count)
                orderIssuers.RemoveAt(orderIssuers.Count - 1);
            while (orderFireSuppressed.Count > orderPawns.Count)
                orderFireSuppressed.RemoveAt(orderFireSuppressed.Count - 1);
            while (orderEnvelopes.Count > orderPawns.Count)
                orderEnvelopes.RemoveAt(orderEnvelopes.Count - 1);
            while (orderGrits.Count > orderPawns.Count)
                orderGrits.RemoveAt(orderGrits.Count - 1);
            while (orderProfilePinned.Count > orderPawns.Count)
                orderProfilePinned.RemoveAt(orderProfilePinned.Count - 1);
        }
    }

    // Single steady state: keep every member's duty matched to their standing order.
    public class LordToil_CATactical : LordToil
    {
        private LordJob_CATactical Job => (LordJob_CATactical)lord.LordJob;

        public override void UpdateAllDuties()
        {
            foreach (Pawn p in lord.ownedPawns)
                Job.AssignDuty(p);
        }
    }

    // The order surface: join/leave the map's standing tactical lord.
    public static class CATactical
    {
        public static Lord LordOf(Map map)
        {
            if (map == null) return null;
            var lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                if (lords[i].CurLordToil is LordToil_End) continue;
                if (lords[i].LordJob is LordJob_CATactical && lords[i].faction == Faction.OfPlayer)
                    return lords[i];
            }
            return null;
        }

        public static LordJob_CATactical JobOf(Map map)
        {
            var lord = LordOf(map);
            return lord != null ? lord.LordJob as LordJob_CATactical : null;
        }

        public static void Assign(Pawn p, int kind, IntVec3 cell, IntVec3 watch,
            CAIntentContext context)
        {
            if (p == null || p.Map == null) return;
            var map = p.Map;
            if (!context.IsValid)
            {
                CATrace.Skip(p, "standing tactical order",
                    "untagged order refused; causal origin is required",
                    destination: cell, anchor: p.Position);
                return;
            }

            // The newest explicit order wins: strip the pawn from any other CA lord
            // (a forming stack, a previous tactical membership on another map) first.
            var current = p.GetLord();
            if (current != null)
            {
                if (current.LordJob is LordJob_CAStackBreach
                    || (current.LordJob is LordJob_CATactical && current != LordOf(map)))
                {
                    current.Notify_PawnLost(p, PawnLostCondition.ForcedByPlayerAction);
                    if (p.CurJob != null && p.CurJob.lord == current)
                        p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    current = null;
                }
                else if (!(current.LordJob is LordJob_CATactical))
                {
                    // Ritual, caravan - genuinely spoken for; the order is refused.
                    Messages.Message(p.LabelShort + " is committed elsewhere - order not taken.",
                        p, MessageTypeDefOf.RejectInput, false);
                    return;
                }
            }

            var lord = LordOf(map);
            LordJob_CATactical job;
            if (lord == null)
            {
                job = new LordJob_CATactical();
                job.SetOrder(p, kind, cell, watch, context);
                lord = LordMaker.MakeNewLord(Faction.OfPlayer, job, map, new List<Pawn> { p });
            }
            else
            {
                job = (LordJob_CATactical)lord.LordJob;
                job.SetOrder(p, kind, cell, watch, context);
                if (!lord.ownedPawns.Contains(p)) lord.AddPawn(p);
            }
            job.AssignDuty(p);
        }

        // Hold-profile surface: the customization lives on the lord's scribed
        // rows; these helpers only locate the row for the pawn's current hold.
        public static bool TryGetHoldProfile(Pawn p, out float envelope,
            out int grit)
        {
            envelope = LordJob_CATactical.DefaultHoldEnvelope;
            grit = LordJob_CATactical.GritStandard;
            if (p == null || p.Map == null) return false;
            var job = JobOf(p.Map);
            return job != null && job.TryGetHoldProfile(p, out envelope, out grit);
        }

        public static void SetHoldProfile(Pawn p, float envelope, int grit,
            bool pin = false)
        {
            if (p == null || p.Map == null) return;
            var job = JobOf(p.Map);
            if (job == null) return;
            job.SetHoldProfile(p, envelope, grit);
            if (pin) job.PinHoldProfile(p);
        }

        public static bool IsHoldProfilePinned(Pawn p)
        {
            if (p == null || p.Map == null) return false;
            var job = JobOf(p.Map);
            return job != null && job.IsHoldProfilePinned(p);
        }

        internal static CAIntentController ControllerFor(int kind)
        {
            if (kind == LordJob_CATactical.KindHold) return CAIntentController.Hold;
            if (kind == LordJob_CATactical.KindHide) return CAIntentController.Hide;
            if (kind == LordJob_CATactical.KindRaidDefense)
                return CAIntentController.RaidDefense;
            return CAIntentController.Ambush;
        }

        // Automatic raid defense shares the standing Lord vehicle but is not a
        // standing player order. It may create/update only its own row; any explicit
        // hold/ambush/hide, stack drill, ritual, caravan, or other Lord wins.
        public static bool AssignAutomaticDefense(Pawn p, IntVec3 cell, IntVec3 watch)
        {
            if (p == null || p.Map == null || !cell.IsValid) return false;
            var map = p.Map;
            var current = p.GetLord();
            Lord lord = LordOf(map);

            if (current != null)
            {
                if (current != lord || !(current.LordJob is LordJob_CATactical)) return false;
                var currentJob = (LordJob_CATactical)current.LordJob;
                int kind;
                IntVec3 oldCell, oldWatch;
                if (!currentJob.TryGetOrder(p, out kind, out oldCell, out oldWatch)
                    || kind != LordJob_CATactical.KindRaidDefense) return false;
                if (oldCell == cell && oldWatch == watch) return true;
                CAIntentContext context;
                if (!currentJob.TryGetContext(p, out context))
                    context = CACombatIntent.AutomaticDefense(p);
                currentJob.SetOrder(p, LordJob_CATactical.KindRaidDefense, cell,
                    watch, context);
                currentJob.AssignDuty(p);
                return true;
            }

            LordJob_CATactical job;
            CAIntentContext automaticContext = CACombatIntent.AutomaticDefense(p);
            if (lord == null)
            {
                job = new LordJob_CATactical();
                job.SetOrder(p, LordJob_CATactical.KindRaidDefense, cell, watch,
                    automaticContext);
                LordMaker.MakeNewLord(Faction.OfPlayer, job, map, new List<Pawn> { p });
            }
            else
            {
                job = (LordJob_CATactical)lord.LordJob;
                job.SetOrder(p, LordJob_CATactical.KindRaidDefense, cell, watch,
                    automaticContext);
                lord.AddPawn(p);
            }
            job.AssignDuty(p);
            return true;
        }

        public static bool IsAutomaticDefense(Pawn p)
        {
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            if (job == null) return false;
            int kind;
            IntVec3 cell, watch;
            return job.TryGetOrder(p, out kind, out cell, out watch)
                && kind == LordJob_CATactical.KindRaidDefense;
        }

        public static bool HasExplicitOrder(Pawn p)
        {
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            if (job == null) return false;
            int kind;
            IntVec3 cell, watch;
            return job.TryGetOrder(p, out kind, out cell, out watch)
                && kind != LordJob_CATactical.KindRaidDefense;
        }

        public static bool IsHold(Pawn p)
        {
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            if (job == null) return false;
            int kind;
            IntVec3 cell, watch;
            return job.TryGetOrder(p, out kind, out cell, out watch)
                && kind == LordJob_CATactical.KindHold;
        }

        public static bool IsConcealmentOrder(Pawn p)
        {
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            if (job == null) return false;
            int kind;
            IntVec3 cell, watch;
            return job.TryGetOrder(p, out kind, out cell, out watch)
                && (kind == LordJob_CATactical.KindAmbush
                    || kind == LordJob_CATactical.KindAmbushSubdue
                    || kind == LordJob_CATactical.KindHide);
        }

        public static bool MatchesOrder(Pawn p, int episodeId,
            int kindA, int kindB = -1)
        {
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            if (job == null) return false;
            int kind;
            IntVec3 cell, watch;
            CAIntentContext context;
            return job.TryGetOrder(p, out kind, out cell, out watch)
                && (kind == kindA || kind == kindB)
                && job.TryGetContext(p, out context)
                && context.EpisodeId == episodeId;
        }

        public static bool FireSuppressionOwned(Pawn p)
        {
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            return job != null && job.FireSuppressionOwned(p);
        }

        public static bool FireSuppressionOwnershipKnown(Pawn p)
        {
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            return job == null || job.FireSuppressionOwnershipKnown;
        }

        public static void SetFireSuppressionOwned(Pawn p, bool owned)
        {
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            if (job != null) job.SetFireSuppressionOwned(p, owned);
        }

        public static bool TryGetContext(Pawn p, out CAIntentContext context)
        {
            context = default(CAIntentContext);
            var lord = p != null ? p.GetLord() : null;
            var job = lord != null ? lord.LordJob as LordJob_CATactical : null;
            return job != null && job.TryGetContext(p, out context);
        }

        // A direct order issued after a standing CA order has no tactical-Lord stamp
        // and remains sovereign. Jobs created to execute the current standing order
        // carry that exact Lord, so constant-duty thinking may continue them without
        // mistaking its own approach for a later player override.
        public static bool HasForeignPlayerForcedJob(Pawn p)
        {
            if (p == null || p.jobs == null) return false;
            Lord lord = p.GetLord();
            Job current = p.CurJob;
            bool ownedQueued = false;
            bool foreignQueued = false;
            bool foreignAfterOwned = false;
            if (p.jobs.jobQueue == null)
                return current != null && current.playerForced
                    && (lord == null || current.lord != lord);
            foreach (QueuedJob queued in p.jobs.jobQueue)
            {
                if (queued?.job == null || !queued.job.playerForced) continue;
                if (lord != null && queued.job.lord == lord) ownedQueued = true;
                else
                {
                    foreignQueued = true;
                    if (ownedQueued) foreignAfterOwned = true;
                }
            }
            // Queue enumeration is execution order. A foreign entry before CA's
            // owned marker predates the standing order; a foreign entry after it is
            // a later player command and cancels the standing order.
            if (foreignQueued && (!ownedQueued || foreignAfterOwned)) return true;
            return current != null && current.playerForced
                && (lord == null || current.lord != lord) && !ownedQueued;
        }

        // TryTakeOrderedJob must queue a fresh standing-order entry behind native
        // force-complete work. Constant duty bypasses the normal queue, so it must
        // recognize that owned pending command and wait for RimWorld to start it.
        public static bool HasPendingOwnedPlayerForcedJob(Pawn p)
        {
            if (p == null || p.jobs == null || p.jobs.jobQueue == null) return false;
            Lord lord = p.GetLord();
            if (lord == null || !(lord.LordJob is LordJob_CATactical)) return false;
            foreach (QueuedJob queued in p.jobs.jobQueue)
                if (queued?.job != null && queued.job.playerForced
                    && queued.job.lord == lord) return true;
            return false;
        }

        // A fresh standing order enters through one player-forced job stamped with
        // its tactical Lord. Constant-duty thinking must not bypass that approach;
        // once it succeeds, ordinary duty jobs are no longer player-forced.
        public static bool HasCurrentOwnedPlayerForcedJob(Pawn p)
        {
            if (p == null || p.jobs == null) return false;
            Lord lord = p.GetLord();
            Job current = p.CurJob;
            return lord != null && lord.LordJob is LordJob_CATactical
                && current != null && current.playerForced
                && current.lord == lord;
        }

        public static void ReleaseAutomaticDefense(Pawn p,
            bool startNewJob = true)
        {
            if (!IsAutomaticDefense(p)) return;
            Release(p, startNewJob);
        }

        public static void Release(Pawn p, bool startNewJob = true,
            string reason = null)
        {
            if (p == null) return;
            var lord = p.GetLord();
            if (lord == null || !(lord.LordJob is LordJob_CATactical)) return;
            CAIntentContext context;
            bool hasContext = ((LordJob_CATactical)lord.LordJob)
                .TryGetContext(p, out context);
            if (!reason.NullOrEmpty())
                CATrace.Pawn(p, "standing order RELEASED - " + reason,
                    anchor: p.Position,
                    intent: hasContext ? (CAIntentContext?)context : null);
            // Remove only approach/posture jobs stamped by this standing order. A
            // later arbitrary player queue remains sovereign and must not be cleared.
            if (p.jobs != null && p.jobs.jobQueue != null)
                p.jobs.jobQueue.RemoveAll(p,
                    queued => queued != null && queued.lord == lord);
            lord.Notify_PawnLost(p, PawnLostCondition.ForcedByPlayerAction);
            if (p.mindState != null) p.mindState.duty = null;
            if (p.CurJob != null && p.CurJob.lord == lord)
                p.jobs.EndCurrentJob(JobCondition.InterruptForced,
                    startNewJob: startNewJob);
        }

        public static bool IsMember(Pawn p)
        {
            var lord = p != null ? p.GetLord() : null;
            return lord != null && lord.LordJob is LordJob_CATactical;
        }
    }
}
