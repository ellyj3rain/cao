using System.Collections.Generic;
using RimWorld;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Module 15: survival responses and concealment. Pawns who go to ground behind trees,
    // chunks, and rock stop being visible to enemy targeting until discovered (proximity +
    // line of sight) or until they act. Enables ambushes and guerrilla defense.
    // Things deliberately stashed in concealment - invisible to raider theft-scanning.
    // Persisted with the save; a stash unhides the moment the thing moves.
    public class HiddenThingsComponent : GameComponent
    {
        private Dictionary<int, IntVec3> stashes = new Dictionary<int, IntVec3>();
        private Dictionary<int, int> quality = new Dictionary<int, int>();
        public static HiddenThingsComponent Instance;

        public HiddenThingsComponent(Game game)
        {
            Instance = this;
            // A new Game is constructed on every load: the session registries must not
            // leak orders across saves (thingIDNumbers restart per game).
            HiddenRegistry.ClearAllSessionState();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref stashes, "CA_stashes", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref quality, "CA_stashQuality", LookMode.Value, LookMode.Value);
            if (stashes == null) stashes = new Dictionary<int, IntVec3>();
            if (quality == null) quality = new Dictionary<int, int>();
            Instance = this;
        }

        public static bool IsStashed(Thing t)
        {
            var inst = Instance;
            return t != null && inst != null && inst.stashes.ContainsKey(t.thingIDNumber);
        }

        public static int QualityOf(Thing t)
        {
            var inst = Instance;
            if (t == null || inst == null) return 0;
            int q;
            return inst.quality.TryGetValue(t.thingIDNumber, out q) ? q : 0;
        }

        public static bool CellOf(Thing t, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var inst = Instance;
            if (t == null || inst == null) return false;
            return inst.stashes.TryGetValue(t.thingIDNumber, out cell);
        }

        public static void Stash(Thing t, IntVec3 cell, int q)
        {
            if (t == null || Instance == null) return;
            Instance.stashes[t.thingIDNumber] = cell;
            Instance.quality[t.thingIDNumber] = q;
        }

        public static void ValidateAll(Map map)
        {
            var inst = Instance;
            if (inst == null || inst.stashes.Count == 0) return;
            var stale = new List<int>();
            foreach (var kv in inst.stashes)
            {
                bool ok = false;
                var things = kv.Value.InBounds(map) ? kv.Value.GetThingList(map) : null;
                if (things != null)
                    for (int i = 0; i < things.Count; i++)
                        if (things[i].thingIDNumber == kv.Key) { ok = true; break; }
                if (!ok) stale.Add(kv.Key);
            }
            for (int i = 0; i < stale.Count; i++)
            {
                inst.stashes.Remove(stale[i]);
                inst.quality.Remove(stale[i]);
            }
        }
    }

    public static class HiddenRegistry
    {
        // pawnId -> hide cell
        private static readonly Dictionary<int, IntVec3> hidden = new Dictionary<int, IntVec3>();
        // Static registries span loaded maps; each episode is processed only by
        // the MapComponent that owns it.
        private static readonly Dictionary<int, int> hiddenMapIds =
            new Dictionary<int, int>();
        // pawnId -> ordered ambush destination (en route)
        private static readonly Dictionary<int, IntVec3> pendingAmbush = new Dictionary<int, IntVec3>();
        private static readonly Dictionary<int, int> pendingAmbushMapIds =
            new Dictionary<int, int>();
        // pawns whose hide was a direct player order - they spring regardless of mode/skill floor
        private static readonly HashSet<int> ordered = new HashSet<int>();
        // pawns who froze this danger episode (rare, mood-driven, once each)
        public static readonly HashSet<int> FrozeThisRaid = new HashSet<int>();
        // ambush aftermath: ambusherId -> victimId, and the spot to melt back into
        private static readonly Dictionary<int, int> aftermathVictim = new Dictionary<int, int>();
        private static readonly Dictionary<int, IntVec3> aftermathReturn = new Dictionary<int, IntVec3>();
        // things being carried to a stash: thingId -> stash cell
        private static readonly Dictionary<int, IntVec3> pendingStash = new Dictionary<int, IntVec3>();
        // One stable discovery contest per hider/observer/episode. Progress carries
        // across slow observation passes; the seeded threshold does not reroll on
        // every target query or frame.
        private static readonly Dictionary<int, int> hideEpisodeSeeds =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, Dictionary<int, float>> discoveryProgress =
            new Dictionary<int, Dictionary<int, float>>();

        private sealed class AmbushGroupState
        {
            public int EpisodeId;
            public int MapId;
            public int StartedTick;
            public readonly HashSet<int> Members = new HashSet<int>();
            public readonly HashSet<int> Ready = new HashSet<int>();
            // Acoustic environment the order was issued INTO, keyed by cue event id
            // with the closest minimum distance any member already heard it at. An
            // order accepted during an audible firefight must not be aborted by
            // that same continuing firefight on the next maintenance pulse; only a
            // genuinely new gunfire event, or a baselined one closing onto the
            // hide, may compromise the concealment. Session-only, like the group.
            public readonly Dictionary<int, float> BaselineGunfire =
                new Dictionary<int, float>();
            // Closest approach any hostile has made to any hidden member, keyed
            // by hostile id. A target that came through the kill zone and is now
            // opening distance again is a firing solution about to be lost; the
            // ranged spring uses this to fire before the shot expires. Session-
            // only, like the group.
            public readonly Dictionary<int, float> ClosestApproach =
                new Dictionary<int, float>();
            public string AbortReason;
            public bool ReadyLogged;
            public int ReadyDeadlineTick;
            // Lifecycle: when the group went READY, and the last tick any
            // hostile stood within the viability horizon. An ambush owns its
            // own end states - it does not wait for an errand to retire it.
            public int ReadyTick;
            public int LastApproachTick;
            public bool EmplacementGraded;
        }

        private static readonly Dictionary<int, AmbushGroupState> ambushGroups =
            new Dictionary<int, AmbushGroupState>();
        private static readonly Dictionary<int, int> ambushEpisodeByPawn =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, CAIntentContext> ambushContexts =
            new Dictionary<int, CAIntentContext>();

        public static void NoteStrike(Pawn ambusher, Pawn victim, IntVec3 hideSpot)
        {
            if (ambusher == null || victim == null) return;
            aftermathVictim[ambusher.thingIDNumber] = victim.thingIDNumber;
            aftermathReturn[ambusher.thingIDNumber] = hideSpot;
        }

        public static void OrderStash(Pawn p, Thing t, IntVec3 spot)
        {
            if (p == null || t == null) return;
            pendingStash[t.thingIDNumber] = spot;
            Job haul = JobMaker.MakeJob(JobDefOf.HaulToCell, t, spot);
            haul.count = t.stackCount;
            haul.haulMode = HaulMode.ToCellNonStorage;
            p.jobs.StartJob(haul, JobCondition.InterruptForced);
        }

        public static bool IsOrdered(Pawn p)
        {
            return p != null && ordered.Contains(p.thingIDNumber);
        }

        // Session state is keyed by thingIDNumber, which restarts per save - a load must
        // start clean or another colony's pawn ids inherit this one's orders. One
        // function IS the load-reset contract; new session caches register here.
        public static void ClearAllSessionState()
        {
            Disposition.ClearCache();
            AnimalDisposition.ClearCache();
            JobGiver_CACombatReaction.ClearTransientSight();
            MovingFire.ClearTransient();
            CATrace.ClearTransient();
            CACombatIntent.ClearTransient();
            CABattlefieldPerception.ClearTransient();
            hidden.Clear();
            hiddenMapIds.Clear();
            pendingAmbush.Clear();
            pendingAmbushMapIds.Clear();
            ordered.Clear();
            FrozeThisRaid.Clear();
            aftermathVictim.Clear();
            aftermathReturn.Clear();
            pendingStash.Clear();
            hideEpisodeSeeds.Clear();
            discoveryProgress.Clear();
            ambushGroups.Clear();
            ambushEpisodeByPawn.Clear();
            ambushContexts.Clear();
            subdue.Clear();
            chokeVictim.Clear();
            chokeCycles.Clear();
            passive.Clear();
            fireAtWillChanged.Clear();
        }

        public static bool IsHiddenOrOrdered(Pawn p)
        {
            return p != null && (hidden.ContainsKey(p.thingIDNumber) || pendingAmbush.ContainsKey(p.thingIDNumber));
        }

        // The order: sprint to the spot, go to ground, spring on whoever steps close.
        // Subdue state remains only to finish standing orders restored from older saves;
        // a2-115 has no producer for a new subdue ambush.
        private static readonly HashSet<int> subdue = new HashSet<int>();
        private static readonly Dictionary<int, int> chokeVictim = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> chokeCycles = new Dictionary<int, int>();

        private static readonly HashSet<int> passive = new HashSet<int>();

        public static void BeginAmbushGroup(List<Pawn> members, Map map,
            int episodeId)
        {
            if (members == null || map == null || episodeId <= 0) return;
            // Replacement is transactional. Retire every member's older concealment
            // episode and Hold registry before any reverse mapping points at the new
            // episode; otherwise an old group can later spring or abort over it.
            for (int i = 0; i < members.Count; i++)
            {
                Pawn p = members[i];
                if (p == null || p.Map != map) continue;
                int existingEpisode;
                if (!ambushEpisodeByPawn.TryGetValue(p.thingIDNumber,
                        out existingEpisode)
                    || existingEpisode != episodeId)
                    CancelForReplacement(p, "replaced by a newer ambush");
                HoldMapComponent hold = map.GetComponent<HoldMapComponent>();
                if (hold != null && hold.IsHolding(p))
                    hold.Release(p, startNewJob: false);
            }
            AmbushGroupState group;
            if (!ambushGroups.TryGetValue(episodeId, out group))
            {
                group = new AmbushGroupState
                {
                    EpisodeId = episodeId,
                    MapId = map.uniqueID,
                    StartedTick = Find.TickManager.TicksGame,
                    // A single actor has nobody to synchronize with. Zero means
                    // no readiness deadline; the order remains authoritative
                    // until the actor arrives, the operator replaces it, or a
                    // real ambush invalidation occurs. The old zero-tick deadline
                    // was interpreted as already expired on the next maintenance
                    // pulse and released the order back to ordinary needs work.
                    ReadyDeadlineTick = members.Count > 1
                        ? Find.TickManager.TicksGame + 900 : 0
                };
                ambushGroups[episodeId] = group;
            }
            for (int i = 0; i < members.Count; i++)
            {
                Pawn p = members[i];
                if (p == null || p.Map != map) continue;
                if (group.Members.Add(p.thingIDNumber))
                    BaselineAmbientGunfire(group, p);
                ambushEpisodeByPawn[p.thingIDNumber] = episodeId;
            }
            if (group.Members.Count > 1
                && group.ReadyDeadlineTick <= Find.TickManager.TicksGame)
                group.ReadyDeadlineTick = Find.TickManager.TicksGame + 900;
        }

        // Record the gunfire events a joining member can already hear so the
        // compromise audit can tell pre-existing battle noise from materially new
        // reports. The closest already-heard minimum distance is kept per event;
        // consumption stays inside this group and never marks a pulse considered.
        private static void BaselineAmbientGunfire(AmbushGroupState group,
            Pawn member)
        {
            AudibleCueMapComponent component = AudibleCueMapComponent.For(
                member?.Map);
            if (group == null || component == null) return;
            List<AudibleCueSnapshot> cues = component.CopyFreshCues(member);
            int baselined = 0;
            for (int i = 0; i < cues.Count; i++)
            {
                AudibleCueSnapshot cue = cues[i];
                if (cue.Kind != AudibleCueKind.Gunfire
                    || cue.ExpectedActivity
                    || !cue.ApproximateCell.IsValid) continue;
                float minimumDistance = Mathf.Max(0f,
                    member.Position.DistanceTo(cue.ApproximateCell)
                        - cue.UncertaintyRadius);
                float known;
                if (!group.BaselineGunfire.TryGetValue(cue.EventId, out known)
                    || minimumDistance < known)
                    group.BaselineGunfire[cue.EventId] = minimumDistance;
                baselined++;
            }
            if (baselined > 0)
                CATrace.Pawn(member, "ambush baseline HOLDS " + baselined
                    + " pre-existing gunfire event" + (baselined == 1 ? "" : "s")
                    + "; only new gunfire, or a baselined fight closing onto the"
                    + " hide, may compromise this concealment",
                    anchor: member.Position);
        }

        public static void AbortAmbushGroup(Map map, int episodeId,
            string reason)
        {
            AmbushGroupState group;
            if (map == null || !ambushGroups.TryGetValue(episodeId, out group)
                || group.MapId != map.uniqueID) return;
            group.AbortReason = reason;
            AbortAmbushGroup(map, group);
        }

        public static bool OrderAmbush(Pawn p, IntVec3 spot,
            CAIntentContext context)
        {
            if (p == null || p.Map == null) return false;
            if (!context.IsValid)
            {
                CATrace.Skip(p, "ambush order",
                    "untagged order refused; causal origin is required",
                    destination: spot, anchor: p.Position);
                return false;
            }
            bool operatorAuthored = context.Origin == CAIntentOrigin.OperatorDirect
                || context.Origin == CAIntentOrigin.OperatorRelay;
            if (!operatorAuthored && (CATactical.HasForeignPlayerForcedJob(p)
                || CATactical.HasExplicitOrder(p)))
            {
                CATrace.Skip(p, "ambush continuation",
                    "existing player work or standing intent remains authoritative",
                    destination: spot, anchor: p.Position, intent: context);
                return false;
            }
            int registeredEpisode;
            AmbushGroupState registeredGroup;
            if (!ambushEpisodeByPawn.TryGetValue(p.thingIDNumber,
                    out registeredEpisode)
                || registeredEpisode != context.EpisodeId
                || !ambushGroups.TryGetValue(registeredEpisode,
                    out registeredGroup))
                BeginAmbushGroup(new List<Pawn> { p }, p.Map,
                    context.EpisodeId);
            ambushContexts[p.thingIDNumber] = context;
            pendingAmbush[p.thingIDNumber] = spot;
            pendingAmbushMapIds[p.thingIDNumber] = p.Map.uniqueID;
            ordered.Add(p.thingIDNumber);
            passive.Remove(p.thingIDNumber);
            subdue.Remove(p.thingIDNumber);
            // The duty carries them there and holds the posture; this registry keeps
            // the spring/discovery machinery.
            CATactical.Assign(p, LordJob_CATactical.KindAmbush, spot,
                IntVec3.Invalid, context);
            if (!CATactical.MatchesOrder(p, context.EpisodeId,
                LordJob_CATactical.KindAmbush))
            {
                RequestAmbushAbort(p, "member could not accept the tactical order");
                CancelForReplacement(p,
                    "ambush member could not accept the tactical order");
                CATrace.Skip(p, "ambush order",
                    "pawn is committed to another Lord",
                    destination: spot, anchor: p.Position, intent: context);
                return false;
            }
            if (operatorAuthored && !TryStartConcealmentEntry(p, spot))
            {
                RequestAmbushAbort(p,
                    "member could not start the ordered approach");
                CancelForReplacement(p,
                    "ambush approach could not start");
                CATrace.Skip(p, "ambush order",
                    "ordered approach could not start",
                    destination: spot, anchor: p.Position,
                    intent: context);
                return false;
            }
            CATrace.Pawn(p, "ambush ORDERED at " + spot, destination: spot,
                anchor: p.Position, intent: context);
            return true;
        }

        // Pure hide: go to ground and stay quiet - no spring, no strike. The order form
        // of what frightened non-fighters do on their own.
        public static bool OrderHide(Pawn p, IntVec3 spot,
            CAIntentContext context)
        {
            if (p == null || p.Map == null) return false;
            if (!context.IsValid)
            {
                CATrace.Skip(p, "hide order",
                    "untagged order refused; causal origin is required",
                    destination: spot, anchor: p.Position);
                return false;
            }
            CancelForReplacement(p, "replaced by a newer hide");
            HoldMapComponent hold = p.Map.GetComponent<HoldMapComponent>();
            if (hold != null && hold.IsHolding(p))
                hold.Release(p, startNewJob: false);
            pendingAmbush[p.thingIDNumber] = spot;
            pendingAmbushMapIds[p.thingIDNumber] = p.Map.uniqueID;
            ordered.Add(p.thingIDNumber);
            passive.Add(p.thingIDNumber);
            subdue.Remove(p.thingIDNumber);
            CATactical.Assign(p, LordJob_CATactical.KindHide, spot,
                IntVec3.Invalid, context);
            if (!CATactical.MatchesOrder(p, context.EpisodeId,
                LordJob_CATactical.KindHide))
            {
                pendingAmbush.Remove(p.thingIDNumber);
                pendingAmbushMapIds.Remove(p.thingIDNumber);
                ordered.Remove(p.thingIDNumber);
                passive.Remove(p.thingIDNumber);
                CATrace.Skip(p, "hide order",
                    "pawn is committed to another Lord",
                    destination: spot, anchor: p.Position, intent: context);
                return false;
            }
            if (!TryStartConcealmentEntry(p, spot))
            {
                CancelForReplacement(p, "hide approach could not start");
                CATrace.Skip(p, "hide order",
                    "ordered approach could not start",
                    destination: spot, anchor: p.Position,
                    intent: context);
                return false;
            }
            CATrace.Pawn(p, "hide ORDERED at " + spot, destination: spot,
                anchor: p.Position, intent: context);
            return true;
        }

        private static bool TryStartConcealmentEntry(Pawn p, IntVec3 spot)
        {
            if (p == null || p.jobs == null) return false;
            Lord tacticalLord = p.GetLord();
            if (tacticalLord == null
                || !(tacticalLord.LordJob is LordJob_CATactical)) return false;
            Job entry;
            if (p.Position != spot)
            {
                entry = JobMaker.MakeJob(JobDefOf.Goto, spot);
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
            Job current = p.CurJob;
            CAIntentContext context;
            bool hasContext = CATactical.TryGetContext(p, out context);
            if (current != null && current.JobIsSameAs(p, entry))
            {
                p.jobs.ClearQueuedJobs();
                current.lord = tacticalLord;
                current.playerForced = true;
                current.locomotionUrgency = entry.locomotionUrgency;
                current.expiryInterval = entry.expiryInterval;
                current.checkOverrideOnExpire = entry.checkOverrideOnExpire;
                JobMaker.ReturnToPool(entry);
                CATrace.Pawn(p,
                    "concealment order ADOPTED_IDENTICAL current job; queue cleared and ownership transferred",
                    destination: spot, anchor: p.Position,
                    intent: hasContext ? (CAIntentContext?)context : null);
                return true;
            }
            Job blocker = p.CurJob;
            int entryId = entry.loadID;
            bool accepted = p.jobs.TryTakeOrderedJob(entry, JobTag.Misc);
            bool started = accepted && p.CurJob != null
                && p.CurJob.loadID == entryId;
            bool pending = accepted && !started && p.jobs.jobQueue != null
                && p.jobs.jobQueue.Contains(entry);
            CATrace.Pawn(p, "concealment order "
                + (!accepted ? "REJECTED"
                    : started ? "STARTED"
                    : pending ? "PENDING behind "
                        + (blocker?.def?.defName ?? "unknown blocker")
                        + "; readiness deadline remains authoritative"
                    : "ACCEPTED with engine state unresolved"),
                destination: spot, anchor: p.Position,
                intent: hasContext ? (CAIntentContext?)context : null);
            return accepted;
        }

        // Save-load rebuild: the tactical lord scribed the order; re-arm the spring
        // machinery for its members.
        public static void RestoreOrderedFromSave(Pawn p, IntVec3 spot, int kind)
        {
            if (p == null || p.Map == null) return;
            if (kind == LordJob_CATactical.KindAmbush
                || kind == LordJob_CATactical.KindAmbushSubdue)
            {
                CAIntentContext ongoing;
                if (!CATactical.TryGetContext(p, out ongoing))
                    ongoing = CACombatIntent.Restored(p,
                        CAIntentController.Ambush, 0);
                BeginAmbushGroup(new List<Pawn> { p }, p.Map,
                    ongoing.EpisodeId);
                ambushContexts[p.thingIDNumber] = ongoing;
                CAIntentContext receipt = new CAIntentContext(
                    ongoing.EpisodeId, CAIntentOrigin.SaveRestore,
                    CAIntentController.Ambush, ongoing.IssuerId);
                CATrace.Pawn(p, "ambush RESTORED at " + spot,
                    destination: spot, anchor: p.Position, intent: receipt);
            }
            // Register the session-only posture only after BeginAmbushGroup has
            // retired any genuinely older episode. On load these dictionaries are
            // empty; writing them first would make the new restore cancel itself.
            pendingAmbush[p.thingIDNumber] = spot;
            pendingAmbushMapIds[p.thingIDNumber] = p.Map.uniqueID;
            ordered.Add(p.thingIDNumber);
            if (kind == LordJob_CATactical.KindHide)
                passive.Add(p.thingIDNumber);
            if (kind == LordJob_CATactical.KindAmbushSubdue)
                subdue.Add(p.thingIDNumber);
            if (CATactical.FireSuppressionOwned(p))
                fireAtWillChanged[p.thingIDNumber] = p;
            else if (!CATactical.FireSuppressionOwnershipKnown(p)
                && p.Drafted && p.drafter != null && !p.drafter.FireAtWill)
            {
                CAIntentContext ongoing;
                bool hasOngoing = CATactical.TryGetContext(p, out ongoing);
                CATrace.Pawn(p,
                    "legacy concealment RESTORED with Fire-at-will ownership unknown; setting left unchanged",
                    destination: spot, anchor: p.Position,
                    intent: hasOngoing ? (CAIntentContext?)new CAIntentContext(
                        ongoing.EpisodeId, CAIntentOrigin.SaveRestore,
                        ongoing.Controller, ongoing.IssuerId) : null);
            }
        }

        // Concealment near a point, skipping spots already claimed - group placement.
        public static bool TryFindConcealmentNearExcluding(Map map, Pawn p, IntVec3 near,
            float radius, List<IntVec3> exclude, out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            float best = float.MaxValue;
            int limit = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 c = near + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                bool claimed = false;
                for (int j = 0; j < exclude.Count; j++)
                    if (c.InHorDistOf(exclude[j], 1.9f)) { claimed = true; break; }
                if (claimed) continue;
                if (!HasConcealmentAt(map, c)) continue;
                if (!p.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                float d = near.DistanceToSquared(c);
                if (d < best) { best = d; dest = c; }
            }
            return dest.IsValid;
        }

        public static bool TryFindConcealmentNear(Map map, Pawn p, IntVec3 near, float radius, out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            float best = float.MaxValue;
            int limit = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 c = near + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if (!HasConcealmentAt(map, c)) continue;
                if (!p.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                float d = near.DistanceToSquared(c);
                if (d < best) { best = d; dest = c; }
            }
            return dest.IsValid;
        }

        public static bool IsHidden(Pawn p)
        {
            return p != null && hidden.ContainsKey(p.thingIDNumber);
        }

        // Hold-fire state we imposed while hidden, to hand back on reveal.
        private static readonly Dictionary<int, Pawn> fireAtWillChanged = new Dictionary<int, Pawn>();

        public static void SetHidden(Pawn p, IntVec3 cell)
        {
            if (p == null || p.Map == null) return;
            hidden[p.thingIDNumber] = cell;
            hiddenMapIds[p.thingIDNumber] = p.Map.uniqueID;
            hideEpisodeSeeds[p.thingIDNumber] = Gen.HashCombineInt(
                p.thingIDNumber, Find.TickManager.TicksGame);
            discoveryProgress[p.thingIDNumber] = new Dictionary<int, float>();
            int episodeId;
            AmbushGroupState group;
            if (ambushEpisodeByPawn.TryGetValue(p.thingIDNumber, out episodeId)
                && ambushGroups.TryGetValue(episodeId, out group))
                group.Ready.Add(p.thingIDNumber);

            // Going to ground means going quiet: a drafted ambusher on fire-at-will
            // would shoot the approach and blow the hide. Imposed here, restored on reveal.
            try
            {
                if (CATactical.FireSuppressionOwned(p))
                    fireAtWillChanged[p.thingIDNumber] = p;
                if (p.Drafted && p.drafter != null && p.drafter.FireAtWill)
                {
                    p.drafter.FireAtWill = false;
                    fireAtWillChanged[p.thingIDNumber] = p;
                    CATactical.SetFireSuppressionOwned(p, true);
                }
            }
            catch { }

            // Pursuers lose sight: anyone mid-chase on this pawn drops the target and
            // has to reacquire - which the targeting seams now refuse while hidden.
            try
            {
                var map = p.Map;
                if (map == null) return;
                var all = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < all.Count; i++)
                {
                    var h = all[i];
                    if (h.Dead || h.Downed || !h.HostileTo(p)) continue;
                    if (h.mindState != null && ReferenceEquals(h.mindState.enemyTarget, p))
                        h.mindState.enemyTarget = null;
                    var job = h.CurJob;
                    if (job != null
                        && (job.def == JobDefOf.AttackMelee || job.def == JobDefOf.AttackStatic)
                        && job.targetA.Thing == p)
                        h.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
            catch { }
        }

        private static void RevealId(int id)
        {
            hidden.Remove(id);
            hiddenMapIds.Remove(id);
            hideEpisodeSeeds.Remove(id);
            discoveryProgress.Remove(id);
            Pawn p;
            if (fireAtWillChanged.TryGetValue(id, out p))
            {
                fireAtWillChanged.Remove(id);
                try
                {
                    if (p != null && !p.Dead && p.drafter != null)
                        p.drafter.FireAtWill = true;
                    CATactical.SetFireSuppressionOwned(p, false);
                }
                catch { }
            }
        }

        public static void Reveal(Pawn p)
        {
            CancelForReplacement(p, "member was released or re-tasked");
        }

        public static void CancelForReplacement(Pawn p, string reason)
        {
            if (p == null) return;
            int id = p.thingIDNumber;
            int episodeId;
            AmbushGroupState group;
            if (ambushEpisodeByPawn.TryGetValue(id, out episodeId)
                && ambushGroups.TryGetValue(episodeId, out group))
            {
                if (group.AbortReason.NullOrEmpty())
                    group.AbortReason = reason + " (" + p.LabelShort + ")";
                AbortAmbushGroup(p.Map, group, id);
                return;
            }

            bool ownsConcealment = hidden.ContainsKey(id)
                || pendingAmbush.ContainsKey(id) || ordered.Contains(id)
                || passive.Contains(id);
            if (!ownsConcealment) return;
            RevealId(id);
            ordered.Remove(id);
            passive.Remove(id);
            subdue.Remove(id);
            pendingAmbush.Remove(id);
            pendingAmbushMapIds.Remove(id);
            ambushContexts.Remove(id);
            if (CATactical.IsConcealmentOrder(p))
                CATactical.Release(p, startNewJob: false,
                    reason: reason);
        }

        public static void NotifyHarmed(Pawn p)
        {
            RequestAmbushAbort(p, "member was hit before the synchronized release");
        }

        public static void NotifyForeignPlayerOrder(Pawn p)
        {
            if (p == null) return;
            bool concealment = IsHiddenOrOrdered(p)
                || ambushEpisodeByPawn.ContainsKey(p.thingIDNumber);
            HoldMapComponent hold = p.Map != null
                ? p.Map.GetComponent<HoldMapComponent>() : null;
            bool holding = hold != null && hold.IsHolding(p);
            if (!concealment && !holding) return;
            if (concealment)
                CancelForReplacement(p,
                    "a later direct player order replaced the concealment order");
            if (holding)
                hold.Release(p, startNewJob: false);
        }

        private static void RequestAmbushAbort(Pawn p, string reason)
        {
            if (p == null) return;
            int episodeId;
            AmbushGroupState group;
            if (!ambushEpisodeByPawn.TryGetValue(p.thingIDNumber, out episodeId)
                || !ambushGroups.TryGetValue(episodeId, out group)
                || !group.AbortReason.NullOrEmpty()) return;
            group.AbortReason = reason + " (" + p.LabelShort + ")";
        }

        public static void ClearEpisode()
        {
            FrozeThisRaid.Clear();
        }

        [HarmonyPatch(typeof(Pawn_JobTracker),
            nameof(Pawn_JobTracker.TryTakeOrderedJob),
            new[] { typeof(Job), typeof(JobTag?), typeof(bool) })]
        private static class Patch_DirectOrderBreaksConcealment
        {
            private static void Prefix(Pawn ___pawn, Job job, out bool __state)
            {
                __state = false;
                if (___pawn == null || job == null
                    || IsOwnedExecution(___pawn, job)) return;
                // RimWorld returns early when the requested job is identical to the
                // current one. Retire concealment before that comparison so the new
                // direct job becomes the current owner instead of inheriting CA's Lord.
                if (___pawn.CurJob != null
                    && ___pawn.CurJob.JobIsSameAs(___pawn, job))
                {
                    __state = true;
                    NotifyForeignPlayerOrder(___pawn);
                }
            }

            private static void Postfix(Pawn ___pawn, Job job, bool __result,
                bool __state)
            {
                if (!__result || __state || ___pawn == null || job == null
                    || IsOwnedExecution(___pawn, job)) return;
                NotifyForeignPlayerOrder(___pawn);
            }

            private static bool IsOwnedExecution(Pawn pawn, Job job)
            {
                Lord lord = pawn.GetLord();
                // Standing-order entry jobs are CA's own execution, not a later
                // player command. Every other accepted ordered job is sovereign.
                return lord != null && lord.LordJob is LordJob_CATactical
                    && job.lord == lord;
            }
        }

        // Maintain hidden states: moving off the spot, attacking, or being discovered reveals.
        public static void Maintain(Map map)
        {
            HiddenThingsComponent.ValidateAll(map);

            // Pending stashes: the thing arriving at its cell completes the hide.
            if (pendingStash.Count > 0)
            {
                var doneStash = new List<int>();
                foreach (var kv in pendingStash)
                {
                    Thing found = null;
                    var things = kv.Value.InBounds(map) ? kv.Value.GetThingList(map) : null;
                    if (things != null)
                        for (int i = 0; i < things.Count; i++)
                            if (things[i].thingIDNumber == kv.Key) { found = things[i]; break; }
                    if (found != null)
                    {
                        HiddenThingsComponent.Stash(found, kv.Value, ConcealmentQualityAt(map, kv.Value));
                        doneStash.Add(kv.Key);
                    }
                }
                for (int i = 0; i < doneStash.Count; i++) pendingStash.Remove(doneStash[i]);
            }

            // Choke-outs in progress: keep the victim stunned, and after enough cycles
            // put them into anesthetic sleep - downed, capturable, alive. Skilled hands
            // finish faster. Breaks if either party moves apart, downs, or dies.
            if (chokeVictim.Count > 0)
            {
                var doneChoke = new List<int>();
                var spawned = map.mapPawns.AllPawnsSpawned;
                foreach (var kv in chokeVictim)
                {
                    Pawn choker = null, victim = null;
                    for (int i = 0; i < spawned.Count; i++)
                    {
                        if (spawned[i].thingIDNumber == kv.Key) choker = spawned[i];
                        else if (spawned[i].thingIDNumber == kv.Value) victim = spawned[i];
                    }
                    if (choker == null || victim == null || choker.Dead || choker.Downed
                        || victim.Dead || victim.Downed
                        || !victim.Position.InHorDistOf(choker.Position, 2.9f))
                    { doneChoke.Add(kv.Key); continue; }

                    int melee = choker.skills != null ? choker.skills.GetSkill(SkillDefOf.Melee).Level : 0;
                    // Stun must outlast the 250-tick cycle cadence (30 ticks per damage
                    // point) or the victim gets a free walking window between cycles.
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Stun, 9f, 0f, -1f, choker));
                    int cycles;
                    chokeCycles.TryGetValue(kv.Key, out cycles);
                    cycles++;
                    int needed = UnityEngine.Mathf.Max(2, 4 - melee / 6);
                    if (cycles >= needed)
                    {
                        try
                        {
                            var an = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, victim);
                            // Sedated stage starts at 0.8 - below that the victim is
                            // woozy but conscious, and "choked out" would be a lie.
                            an.Severity = 0.9f;
                            victim.health.AddHediff(an);
                            Messages.Message(choker.LabelShort + " choked out " + victim.LabelShort + ".",
                                victim, MessageTypeDefOf.NeutralEvent, false);
                        }
                        catch { }
                        doneChoke.Add(kv.Key);
                    }
                    else
                    {
                        chokeCycles[kv.Key] = cycles;
                        var cur = choker.CurJob;
                        if (cur == null || cur.def != JobDefOf.Wait)
                        {
                            Job pin = JobMaker.MakeJob(JobDefOf.Wait, 600);
                            choker.jobs.StartJob(pin, JobCondition.InterruptForced);
                        }
                    }
                }
                for (int i = 0; i < doneChoke.Count; i++)
                {
                    chokeVictim.Remove(doneChoke[i]);
                    chokeCycles.Remove(doneChoke[i]);
                }
            }

            // Ambush aftermath: victim died and the coast is clear - drag the corpse into
            // the green, then melt back into the hiding spot.
            if (aftermathVictim.Count > 0)
            {
                var doneAftermath = new List<int>();
                var cols = map.mapPawns.FreeColonistsSpawned;
                foreach (var kv in aftermathVictim)
                {
                    Pawn ambusher = null;
                    for (int i = 0; i < cols.Count; i++)
                        if (cols[i].thingIDNumber == kv.Key) { ambusher = cols[i]; break; }
                    if (ambusher == null || ambusher.Dead || ambusher.Downed || ambusher.Drafted)
                    { doneAftermath.Add(kv.Key); continue; }
                    if (ambusher.CurJob != null && (ambusher.CurJob.def == JobDefOf.AttackMelee
                        || ambusher.CurJob.def == JobDefOf.HaulToCell)) continue;

                    bool higher = AutonomyComponent.LevelOf(ambusher) >= 3 || ordered.Contains(kv.Key);
                    if (!higher) { doneAftermath.Add(kv.Key); continue; }

                    Corpse corpse = null;
                    var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
                    for (int i = 0; i < corpses.Count; i++)
                    {
                        var c = corpses[i] as Corpse;
                        if (c != null && c.InnerPawn != null && c.InnerPawn.thingIDNumber == kv.Value)
                        { corpse = c; break; }
                    }
                    if (corpse == null || !corpse.Spawned) { doneAftermath.Add(kv.Key); continue; }

                    bool watched = false;
                    var spawned = map.mapPawns.AllPawnsSpawned;
                    for (int i = 0; i < spawned.Count; i++)
                    {
                        var h = spawned[i];
                        if (h.Dead || h.Downed) continue;
                        if (h.Faction == null || !h.Faction.HostileTo(Faction.OfPlayer)) continue;
                        if (h.Position.InHorDistOf(ambusher.Position, 10f)
                            && GenSight.LineOfSight(h.Position, ambusher.Position, map, skipFirstCell: true))
                        { watched = true; break; }
                    }
                    if (watched) continue;

                    IntVec3 spot;
                    if (TryFindConcealmentNear(map, ambusher, corpse.Position, 8.9f, out spot))
                    {
                        OrderStash(ambusher, corpse, spot);
                        IntVec3 back;
                        if (aftermathReturn.TryGetValue(kv.Key, out back))
                        {
                            var continuation = new CAIntentContext(
                                CACombatIntent.NewEpisode(),
                                CAIntentOrigin.Continuation,
                                CAIntentController.Ambush,
                                ambusher.thingIDNumber);
                            OrderAmbush(ambusher, back, continuation);
                        }
                    }
                    doneAftermath.Add(kv.Key);
                }
                for (int i = 0; i < doneAftermath.Count; i++)
                { aftermathVictim.Remove(doneAftermath[i]); aftermathReturn.Remove(doneAftermath[i]); }
            }

            // Discovery: close, clear observation builds progress against one stable
            // per-observer threshold. Cover, light, distance, sight, and Shooting change
            // how quickly the observer resolves the hide; no frame-by-frame reroll lets
            // one query decide the whole episode. Everything reactive lives in MaintainFast.
            if (hidden.Count == 0) return;
            var toReveal = new List<int>();
            var pawns = map.mapPawns.FreeColonistsSpawned;
            var hostiles = HostilesOf(map);
            foreach (var kv in hidden)
            {
                int ownerMapId;
                if (!hiddenMapIds.TryGetValue(kv.Key, out ownerMapId)
                    || ownerMapId != map.uniqueID)
                    continue;
                Pawn p = null;
                for (int i = 0; i < pawns.Count; i++)
                    if (pawns[i].thingIDNumber == kv.Key) { p = pawns[i]; break; }
                if (p == null || p.Dead || p.Downed) { toReveal.Add(kv.Key); continue; }
                // Darkness shields the hider: spotting range shrinks with the light on
                // their position - unless the spotter's eyes don't care (dark vision
                // gene, night goggles).
                float glow = 1f;
                try { glow = map.glowGrid.GroundGlowAt(p.Position); } catch { }
                int concealment = ConcealmentQualityAt(map, p.Position);
                Dictionary<int, float> observerProgress;
                if (!discoveryProgress.TryGetValue(kv.Key, out observerProgress))
                {
                    observerProgress = new Dictionary<int, float>();
                    discoveryProgress[kv.Key] = observerProgress;
                }
                int episodeSeed;
                if (!hideEpisodeSeeds.TryGetValue(kv.Key, out episodeSeed))
                {
                    episodeSeed = Gen.HashCombineInt(kv.Key,
                        Find.TickManager.TicksGame);
                    hideEpisodeSeeds[kv.Key] = episodeSeed;
                }

                for (int i = 0; i < hostiles.Count; i++)
                {
                    var h = hostiles[i];
                    // Concealment discovery consumes the same resolved observer used by
                    // battlefield perception: actual eyes and injuries, added parts,
                    // native trait/gene/gear distance stats, skill, and generic dark
                    // adaptation. The short hide-search envelope remains distinct from
                    // open-map target recognition.
                    CAVisionProfile vision =
                        CABattlefieldPerception.VisionProfileFor(h);
                    if (!vision.CanUseVision) continue;
                    float unaidedLight = 0.45f + 0.55f * glow;
                    float light = UnityEngine.Mathf.Lerp(unaidedLight, 1f,
                        vision.DarkAdaptation);
                    float range = 8.9f * UnityEngine.Mathf.Clamp(
                            vision.SightCapacity, 0.3f, 1.4f)
                        * (1f + vision.Shooting * 0.02f)
                        * vision.DistanceAcuity * light;
                    if (!h.Position.InHorDistOf(p.Position, range)) continue;
                    if (GenSight.LineOfSight(h.Position, p.Position, map, skipFirstCell: true))
                    {
                        float distance = h.Position.DistanceTo(p.Position);
                        float proximity = 1f - UnityEngine.Mathf.Clamp01(
                            distance / UnityEngine.Mathf.Max(0.1f, range));
                        float gain = (0.35f + 0.65f * proximity)
                            / (0.70f + 0.35f * concealment);
                        float progress;
                        observerProgress.TryGetValue(h.thingIDNumber, out progress);
                        progress += gain;
                        observerProgress[h.thingIDNumber] = progress;
                        int thresholdSeed = Gen.HashCombineInt(
                            episodeSeed, h.thingIDNumber);
                        float threshold = Rand.RangeSeeded(
                            0.85f, 2.25f, thresholdSeed);
                        if (progress < threshold) continue;

                        // Discovered: the hide is blown - a standing order dies with it
                        // so the duty doesn't march them back into plain sight.
                        RequestAmbushAbort(p, "member was discovered before release");
                        CATactical.Release(p);
                        toReveal.Add(kv.Key);
                        break;
                    }
                }
            }
            for (int i = 0; i < toReveal.Count; i++) RevealId(toReveal[i]);
            ProcessAmbushGroups(map);
        }

        // The live watcher: every half second, not every four. An ordered behavior is a
        // pawn WATCHING, not a pawn parked - arrival puts them to ground immediately,
        // acting or leaving reveals immediately, and the spring fires the moment a
        // hostile steps into reach IN THE AMBUSHER'S LINE OF SIGHT - never through a
        // wall or a closed door. (Fog of war, when it lands, becomes this same gate.)
        public static void MaintainFast(Map map)
        {
            ReleaseDisabledAmbushOrders(map);
            // En-route ambushers: arriving puts them to ground; a broken order clears.
            if (pendingAmbush.Count > 0)
            {
                var done = new List<int>();
                var cols = map.mapPawns.FreeColonistsSpawned;
                foreach (var kv in pendingAmbush)
                {
                    int ownerMapId;
                    if (!pendingAmbushMapIds.TryGetValue(kv.Key, out ownerMapId)
                        || ownerMapId != map.uniqueID)
                        continue;
                    Pawn p = null;
                    for (int i = 0; i < cols.Count; i++)
                        if (cols[i].thingIDNumber == kv.Key) { p = cols[i]; break; }
                    if (p == null || p.Dead || p.Downed)
                    {
                        if (p != null) RequestAmbushAbort(p,
                            "member became unable before reaching the ambush");
                        done.Add(kv.Key);
                        ordered.Remove(kv.Key);
                        continue;
                    }
                    if (CATactical.HasForeignPlayerForcedJob(p))
                    {
                        done.Add(kv.Key);
                        RequestAmbushAbort(p,
                            "a later direct player order replaced the ambush");
                        if (!ambushEpisodeByPawn.ContainsKey(kv.Key))
                        {
                            ordered.Remove(kv.Key);
                            passive.Remove(kv.Key);
                            subdue.Remove(kv.Key);
                            CATactical.Release(p, startNewJob: false,
                                reason: "later direct player order");
                        }
                        continue;
                    }
                    // The duty owns the approach; an order is broken only when the pawn
                    // is no longer under the tactical lord (released, re-tasked).
                    int groupEpisode;
                    CAIntentContext currentContext;
                    bool exactOrder = ambushEpisodeByPawn.TryGetValue(
                            kv.Key, out groupEpisode)
                        ? CATactical.MatchesOrder(p, groupEpisode,
                            LordJob_CATactical.KindAmbush,
                            LordJob_CATactical.KindAmbushSubdue)
                        : CATactical.TryGetContext(p, out currentContext)
                            && CATactical.MatchesOrder(p,
                                currentContext.EpisodeId,
                                LordJob_CATactical.KindHide);
                    if (!exactOrder)
                    {
                        done.Add(kv.Key);
                        ordered.Remove(kv.Key);
                        RequestAmbushAbort(p,
                            "member order released before reaching the spot");
                        CAIntentContext context;
                        bool hasContext = ambushContexts.TryGetValue(
                            p.thingIDNumber, out context);
                        CATrace.Skip(p, "ambush",
                            "order released before reaching the spot",
                            intent: hasContext ? (CAIntentContext?)context : null);
                        continue;
                    }
                    if (p.Position.InHorDistOf(kv.Value, 1.5f))
                    {
                        // Through SetHidden, not a raw write: fire-at-will suppression
                        // and pursuer-drop apply to ordered hides exactly like
                        // self-initiated ones.
                        SetHidden(p, kv.Value);
                        done.Add(kv.Key);
                        CAIntentContext context;
                        bool hasContext = ambushContexts.TryGetValue(
                            p.thingIDNumber, out context)
                            || CATactical.TryGetContext(p, out context);
                        CATrace.Pawn(p, "ambush ARRIVED and HIDDEN at " + kv.Value,
                            destination: kv.Value, anchor: p.Position,
                            intent: hasContext ? (CAIntentContext?)context : null);
                    }
                }
                for (int i = 0; i < done.Count; i++)
                {
                    pendingAmbush.Remove(done[i]);
                    pendingAmbushMapIds.Remove(done[i]);
                }
            }

            ProcessAmbushGroups(map);
            if (hidden.Count == 0) return;
            var toReveal = new List<int>();
            var colonists = map.mapPawns.FreeColonistsSpawned;
            List<Pawn> hostiles = null;

            foreach (var kv in hidden)
            {
                int ownerMapId;
                if (!hiddenMapIds.TryGetValue(kv.Key, out ownerMapId)
                    || ownerMapId != map.uniqueID)
                    continue;
                Pawn p = null;
                for (int i = 0; i < colonists.Count; i++)
                    if (colonists[i].thingIDNumber == kv.Key) { p = colonists[i]; break; }
                if (p == null || p.Dead || p.Downed) { toReveal.Add(kv.Key); continue; }

                if (CATactical.HasForeignPlayerForcedJob(p))
                {
                    RequestAmbushAbort(p,
                        "a later direct player order replaced the ambush");
                    if (!ambushEpisodeByPawn.ContainsKey(kv.Key))
                    {
                        CATactical.Release(p, startNewJob: false,
                            reason: "later direct player order");
                        ordered.Remove(kv.Key);
                        passive.Remove(kv.Key);
                        subdue.Remove(kv.Key);
                        toReveal.Add(kv.Key);
                    }
                    continue;
                }

                // Ordered ambushes are coordinated at the group level. Leaving them
                // in this per-pawn spring path recreates the exact staggered release
                // the live raid exposed.
                if (ambushEpisodeByPawn.ContainsKey(kv.Key)) continue;

                bool ordersHere = ordered.Contains(kv.Key);
                var job = p.CurJob;

                // An EXPLICIT order commits the pawn: the duty holds them on station
                // (the posture driver holds ranged fire while hidden - concealment
                // discipline lives there now). A self-initiated (unordered) hide
                // still yields the moment they wander.
                if (!ordersHere)
                {
                    // Self-initiated hide: acting or leaving the spot reveals.
                    if (!p.Position.InHorDistOf(kv.Value, 2.5f)) { toReveal.Add(kv.Key); continue; }
                }
                if (job != null && (job.def == JobDefOf.AttackMelee || job.def == JobDefOf.AttackStatic))
                {
                    GrantFirstStrike(p);
                    if (ordersHere) CATactical.Release(p); // the strike consumes the order
                    toReveal.Add(kv.Key);
                    continue;
                }

                // Pure hiders don't hunt - but a CORNERED hider defends itself: the
                // hunter is on top of them, in sight, and ALONE - stun and break away.
                // A pack closing in means stay down and pray instead.
                if (passive.Contains(kv.Key))
                {
                    if (p.WorkTagIsDisabled(WorkTags.Violent) || p.skills == null) continue;
                    if (hostiles == null) hostiles = HostilesOf(map);
                    for (int i = 0; i < hostiles.Count; i++)
                    {
                        var h = hostiles[i];
                        if (!h.Position.InHorDistOf(p.Position, 2.9f)) continue;
                        if (!GenSight.LineOfSight(p.Position, h.Position, map, skipFirstCell: true)) continue;
                        bool alone = true;
                        for (int j = 0; j < hostiles.Count; j++)
                            if (hostiles[j] != h && hostiles[j].Position.InHorDistOf(h.Position, 12f))
                            { alone = false; break; }
                        if (!alone) continue;
                        int mel = p.skills.GetSkill(SkillDefOf.Melee).Level;
                        h.TakeDamage(new DamageInfo(DamageDefOf.Stun, 6f + mel * 0.5f, 0f, -1f, p));
                        try
                        {
                            if (h.stances != null && h.stances.stagger != null)
                                h.stances.stagger.StaggerFor(60 + mel * 8);
                        }
                        catch { }
                        toReveal.Add(kv.Key);
                        break;
                    }
                    continue;
                }
                bool orderedHere = ordered.Contains(kv.Key);
                var s2 = AwarenessMod.Settings;
                if (s2 == null || !s2.ambushStrikes) continue;
                if (!(orderedHere || AutonomyComponent.LevelOf(p) >= 2)) continue;
                if (p.WorkTagIsDisabled(WorkTags.Violent) || p.skills == null) continue;
                if (!(orderedHere || p.skills.GetSkill(SkillDefOf.Melee).Level >= 4)) continue;

                if (hostiles == null) hostiles = HostilesOf(map);
                for (int i = 0; i < hostiles.Count; i++)
                {
                    var h = hostiles[i];
                    if (!h.Position.InHorDistOf(p.Position, 2.9f)) continue;
                    if (!GenSight.LineOfSight(p.Position, h.Position, map, skipFirstCell: true)) continue;
                    int melee = p.skills.GetSkill(SkillDefOf.Melee).Level;
                    try
                    {
                        if (h.stances != null && h.stances.stagger != null)
                            h.stances.stagger.StaggerFor(60 + melee * 12);
                    }
                    catch { }
                    toReveal.Add(kv.Key);
                    NoteStrike(p, h, kv.Value);
                    if (subdue.Contains(kv.Key))
                    {
                        // Non-lethal: stun cold, then the choke pass takes over.
                        h.TakeDamage(new DamageInfo(DamageDefOf.Stun, 8f + melee * 0.6f, 0f, -1f, p));
                        chokeVictim[kv.Key] = h.thingIDNumber;
                        chokeCycles[kv.Key] = 0;
                        Job pin = JobMaker.MakeJob(JobDefOf.Wait, 600);
                        p.jobs.StartJob(pin, JobCondition.InterruptForced);
                    }
                    else
                    {
                        GrantFirstStrike(p);
                        Job strike = JobMaker.MakeJob(JobDefOf.AttackMelee, h);
                        strike.expiryInterval = 400;
                        p.jobs.StartJob(strike, JobCondition.InterruptForced);
                    }
                    break;
                }
            }
            for (int i = 0; i < toReveal.Count; i++) RevealId(toReveal[i]);
        }

        private static void ProcessAmbushGroups(Map map)
        {
            if (map == null || ambushGroups.Count == 0) return;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings != null && !settings.ambushStrikes)
            {
                ReleaseDisabledAmbushOrders(map);
                return;
            }
            var episodes = new List<int>(ambushGroups.Keys);
            for (int e = 0; e < episodes.Count; e++)
            {
                AmbushGroupState group;
                if (!ambushGroups.TryGetValue(episodes[e], out group)
                    || group.MapId != map.uniqueID) continue;
                if (!group.AbortReason.NullOrEmpty())
                {
                    AbortAmbushGroup(map, group);
                    continue;
                }
                Pawn lead = null;
                var memberIds = new List<int>(group.Members);
                for (int i = 0; i < memberIds.Count; i++)
                {
                    Pawn member = PawnById(map, memberIds[i]);
                    if (member == null || member.Dead || member.Downed)
                    {
                        group.AbortReason = "member became unable before release";
                        break;
                    }
                    if (lead == null) lead = member;
                    if (hidden.ContainsKey(memberIds[i])) group.Ready.Add(memberIds[i]);
                    if (CATactical.HasForeignPlayerForcedJob(member))
                    {
                        group.AbortReason = "a later direct player order replaced the ambush ("
                            + member.LabelShort + ")";
                        break;
                    }
                    if (!CATactical.MatchesOrder(member, group.EpisodeId,
                        LordJob_CATactical.KindAmbush,
                        LordJob_CATactical.KindAmbushSubdue))
                    {
                        group.AbortReason = "a member no longer has the ambush order";
                        break;
                    }
                    AudibleCueSnapshot compromiseCue;
                    int nearbyPulses;
                    if (TryGetAmbushCompromiseCue(member, group,
                        out compromiseCue, out nearbyPulses))
                    {
                        group.AbortReason = "material nearby gunfire invalidated "
                            + "the hidden ambush before release ("
                            + member.LabelShort + " heard " + nearbyPulses
                            + " unexpected pulse" + (nearbyPulses == 1 ? "" : "s")
                            + ", strongest " + compromiseCue.AcousticClass
                            + ", near " + compromiseCue.ApproximateCell + ")";
                        CATrace.Pawn(member,
                            "ambush compromise HEARD - " + nearbyPulses
                            + " unexpected nearby gunfire pulse"
                            + (nearbyPulses == 1 ? "" : "s")
                            + "; synchronized wait will abort and re-evaluate",
                            contact: compromiseCue.ApproximateCell,
                            anchor: member.Position,
                            intent: ambushContexts.TryGetValue(
                                member.thingIDNumber, out CAIntentContext context)
                                    ? (CAIntentContext?)context : null);
                        break;
                    }
                }
                if (!group.AbortReason.NullOrEmpty())
                {
                    AbortAmbushGroup(map, group);
                    continue;
                }
                if (group.ReadyDeadlineTick > 0
                    && Find.TickManager.TicksGame > group.ReadyDeadlineTick
                    && group.Ready.Count < group.Members.Count)
                {
                    group.AbortReason = "readiness deadline expired at "
                        + group.Ready.Count + "/" + group.Members.Count;
                    AbortAmbushGroup(map, group);
                    continue;
                }
                if (group.Ready.Count < group.Members.Count) continue;

                if (!group.ReadyLogged && lead != null)
                {
                    CAIntentContext context;
                    bool hasContext = ambushContexts.TryGetValue(
                        lead.thingIDNumber, out context);
                    CATrace.Pawn(lead, "ambush group READY " + group.Ready.Count
                        + "/" + group.Members.Count,
                        anchor: lead.Position,
                        intent: hasContext ? (CAIntentContext?)context : null);
                    group.ReadyLogged = true;
                    group.ReadyTick = Find.TickManager.TicksGame;
                    group.LastApproachTick = Find.TickManager.TicksGame;
                }

                // Emplacement covertness: a hide whose walk-in was OBSERVED is
                // compromised at birth - the enemy's memory routes around it.
                // Graded once at readiness; the receipt tells the operator at
                // order time instead of leaving a mid-battle mystery.
                if (!group.EmplacementGraded && lead != null)
                {
                    group.EmplacementGraded = true;
                    KnowledgeMapComponent knowledge =
                        KnowledgeMapComponent.For(map);
                    int observers = 0;
                    if (knowledge != null)
                    {
                        List<Pawn> watchers = HostilesOf(map);
                        for (int w = 0; w < watchers.Count; w++)
                        {
                            List<ThreatContactSnapshot> seen =
                                knowledge.FreshContacts(watchers[w]);
                            for (int c = 0; c < seen.Count; c++)
                            {
                                if (!seen[c].Cell.IsValid) continue;
                                if (!group.Members.Contains(seen[c].HostileId))
                                    continue;
                                if (seen[c].Cell.InHorDistOf(lead.Position,
                                        6.9f))
                                { observers++; break; }
                            }
                        }
                    }
                    if (observers > 0)
                        CATrace.Pawn(lead, "ambush emplacement OBSERVED - "
                            + observers + " hostile(s) hold a remembered "
                            + "contact within 7 cells of the hide; the "
                            + "position is compromised at birth and enemy "
                            + "movement will route around it",
                            anchor: lead.Position);
                }

                // Nonviability: no hostile has approached the viability
                // horizon since readiness for a long window, while the
                // colony's battle rages elsewhere - the fight has passed
                // this position by. Release the guns to it.
                {
                    List<Pawn> horizon = HostilesOf(map);
                    for (int h = 0; h < horizon.Count; h++)
                    {
                        if (lead != null && horizon[h].Position.InHorDistOf(
                                lead.Position, 34.9f))
                        {
                            group.LastApproachTick =
                                Find.TickManager.TicksGame;
                            break;
                        }
                    }
                    if (group.ReadyLogged && lead != null
                        && Find.TickManager.TicksGame
                            - group.LastApproachTick > 2500
                        && CACombatThreat.PerceivesActiveThreat(lead) == false
                        && horizon.Count > 0)
                    {
                        CATrace.Pawn(lead, "ambush NONVIABLE - no hostile has "
                            + "approached within 35 cells for 2500 ticks while "
                            + "the battle runs elsewhere; the fight has passed "
                            + "this position by and the guns are released",
                            anchor: lead.Position);
                        group.AbortReason = "nonviable: the fight passed the "
                            + "position by";
                        AbortAmbushGroup(map, group);
                        continue;
                    }
                }

                // The takedown trigger (2.9 cells, any member) is for a target
                // walking onto the hide. A RANGED ambusher additionally springs
                // on a firing solution inside their weapon envelope: a target in
                // the close kill zone, a target that came through the zone and is
                // now walking away toward friendlies (the shot is being lost), or
                // a target actively engaging a friendly. Concealment is a means
                // to the upper hand, not an end - a hidden shooter does not watch
                // hostiles file past to kill allies.
                List<Pawn> hostiles = HostilesOf(map);
                Pawn trigger = null;
                Pawn triggerMember = null;
                float nearest = float.MaxValue;
                string springWhy = null;
                for (int i = 0; i < memberIds.Count; i++)
                {
                    Pawn member = PawnById(map, memberIds[i]);
                    if (member == null) continue;
                    Verb rangedVerb = member.equipment?.PrimaryEq?.PrimaryVerb;
                    float rangedEnvelope =
                        rangedVerb != null && !rangedVerb.verbProps.IsMeleeAttack
                            ? rangedVerb.verbProps.range * 0.85f : 0f;
                    for (int j = 0; j < hostiles.Count; j++)
                    {
                        Pawn hostile = hostiles[j];
                        float distance = member.Position.DistanceTo(hostile.Position);
                        float known;
                        bool tracked = group.ClosestApproach.TryGetValue(
                            hostile.thingIDNumber, out known);
                        if (distance <= 35f && (!tracked || distance < known))
                            group.ClosestApproach[hostile.thingIDNumber] = distance;
                        if (distance >= nearest) continue;
                        string why = null;
                        if (distance <= 2.9f) why = "target on the hide";
                        else if (rangedEnvelope > 0f && distance <= rangedEnvelope)
                        {
                            if (distance <= 8.9f)
                                why = "target inside the close kill zone";
                            else if (HostileEngagingFriendly(hostile))
                                why = "target engaging a friendly inside the fire envelope";
                            else if (tracked && known <= rangedEnvelope * 0.75f
                                && distance > known + 2f)
                                why = "target passing beyond the kill zone; the firing solution is being lost";
                        }
                        if (why == null
                            || !GenSight.LineOfSight(member.Position,
                                hostile.Position, map, true)) continue;
                        trigger = hostile;
                        triggerMember = member;
                        nearest = distance;
                        springWhy = why;
                    }
                }
                if (trigger != null)
                {
                    if (triggerMember != null)
                        CATrace.Pawn(triggerMember, "ambush spring TRIGGERED - "
                            + springWhy, target: trigger,
                            contact: trigger.Position,
                            anchor: triggerMember.Position,
                            intent: ambushContexts.TryGetValue(
                                triggerMember.thingIDNumber,
                                out CAIntentContext springContext)
                                    ? (CAIntentContext?)springContext : null);
                    SpringAmbushGroup(map, group, trigger, hostiles);
                }
            }
        }

        // A hostile currently attacking a player pawn: aiming at one through a
        // busy stance, or committed to an attack job on one.
        private static bool HostileEngagingFriendly(Pawn hostile)
        {
            var busy = hostile.stances?.curStance as Stance_Busy;
            var victim = busy?.focusTarg.Thing as Pawn;
            if (victim != null && victim.Faction == Faction.OfPlayer) return true;
            Job current = hostile.CurJob;
            if (current != null && (current.def == JobDefOf.AttackMelee
                || current.def == JobDefOf.AttackStatic))
            {
                victim = current.targetA.Thing as Pawn;
                if (victim != null && victim.Faction == Faction.OfPlayer)
                    return true;
            }
            return false;
        }

        // A concealed ambush is a plan for an unopened fight. Repeated unexpected
        // reports, or one heavy report, in its local tactical area make continued
        // waiting for a 2.9-cell visual trigger stale ownership. Release the group
        // as one transaction; the pending acoustic fact then reaches the ordinary
        // bounded investigation/combat-reaction lane on the next think pulse.
        // Gunfire attributable to a battle the member already KNOWS about - a cue
        // in the immediate area of a fresh hostile contact - is not unexpected; it
        // joins the baseline and compromises only by closing onto the hide.
        private static bool TryGetAmbushCompromiseCue(Pawn member,
            AmbushGroupState group, out AudibleCueSnapshot representative,
            out int nearbyPulses)
        {
            representative = default(AudibleCueSnapshot);
            nearbyPulses = 0;
            AudibleCueMapComponent component = AudibleCueMapComponent.For(
                member?.Map);
            if (member == null || group == null || component == null)
                return false;
            int startedTick = group.StartedTick;
            List<AudibleCueSnapshot> cues = component.CopyFreshCues(member);
            bool found = false;
            for (int i = 0; i < cues.Count; i++)
            {
                AudibleCueSnapshot cue = cues[i];
                if (cue.Kind != AudibleCueKind.Gunfire
                    || cue.ExpectedActivity || cue.Considered
                    || cue.HeardTick < startedTick
                    || !cue.ApproximateCell.IsValid) continue;
                float minimumDistance = Mathf.Max(0f,
                    member.Position.DistanceTo(cue.ApproximateCell)
                        - cue.UncertaintyRadius);
                if (minimumDistance > 42f) continue;
                // A rolling gunfire event that was already audible when the order
                // was accepted stays fresh on every new pulse of the same fight.
                // The order was knowingly issued into that noise, so it may not
                // retroactively count as "unexpected" unless the baselined fight
                // has closed inside half the audit radius of this hidden member.
                float baselineDistance;
                if (group.BaselineGunfire.TryGetValue(cue.EventId,
                        out baselineDistance)
                    && minimumDistance > 21f) continue;
                // A gunfire cue in the immediate tactical area of a hostile
                // contact this member currently KNOWS is the noise of a fight
                // the member is already tracking, not an unexpected report.
                // The event joins the group baseline exactly as if it had been
                // audible at acceptance, so the existing rule keeps the order
                // alive at range and still compromises the hide once that
                // fight closes inside half the audit radius. Fire already near
                // this hidden member never reaches this attribution and keeps
                // compromising regardless of what is known.
                int knownHostileId;
                if (minimumDistance > 21f && TryFindKnownContactNearCue(
                    member, cue.ApproximateCell, out knownHostileId))
                {
                    group.BaselineGunfire[cue.EventId] = minimumDistance;
                    CATrace.Pawn(member, "ambush compromise SKIPPED - gunfire"
                        + " near " + cue.ApproximateCell + " belongs to the"
                        + " known battle around hostile contact "
                        + knownHostileId + "; a fight this member already"
                        + " tracks is not unexpected; the event joins the"
                        + " baseline and compromises only by closing onto"
                        + " the hide",
                        contact: cue.ApproximateCell,
                        anchor: member.Position);
                    continue;
                }
                nearbyPulses += Mathf.Max(1, cue.PulseCount);
                if (!found || (int)cue.AcousticClass
                        > (int)representative.AcousticClass
                    || cue.AcousticClass == representative.AcousticClass
                        && cue.HeardTick > representative.HeardTick)
                {
                    representative = cue;
                    found = true;
                }
            }
            return found && (nearbyPulses >= 2
                || representative.AcousticClass == CueAcousticClass.Heavy);
        }

        // The knowledge surface that makes distant battle noise attributable:
        // an active, still-fresh hostile contact this member KNOWS within half
        // the 42-cell audit radius of the cue cell. Fresh contacts keep the
        // epistemics honest - only what this pawn saw or was told, never
        // global map truth.
        private static bool TryFindKnownContactNearCue(Pawn member,
            IntVec3 cueCell, out int knownHostileId)
        {
            knownHostileId = -1;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(
                member?.Map);
            if (knowledge == null) return false;
            float best = float.MaxValue;
            ScanContactsNearCue(knowledge.FreshContacts(member), cueCell,
                ref best, ref knownHostileId);
            // Mechlink doctrine: the linked share one threat picture. A
            // hidden member attributes distant gunfire to a battle ANY linked
            // squadmate tracks - Maria does not abort over the sound of a
            // fight the whole network is watching.
            if (knownHostileId == -1
                && CommsModule.HasActiveMechlink(member))
            {
                var colonists = member.Map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < colonists.Count; i++)
                {
                    Pawn mate = colonists[i];
                    if (mate == member || mate.Downed
                        || !CommsModule.HasActiveMechlink(mate)) continue;
                    ScanContactsNearCue(knowledge.FreshContacts(mate),
                        cueCell, ref best, ref knownHostileId);
                }
            }
            return knownHostileId != -1;
        }

        private static void ScanContactsNearCue(
            List<ThreatContactSnapshot> contacts, IntVec3 cueCell,
            ref float best, ref int knownHostileId)
        {
            for (int i = 0; i < contacts.Count; i++)
            {
                ThreatContactSnapshot contact = contacts[i];
                if (!contact.Cell.IsValid) continue;
                float distance = contact.Cell.DistanceTo(cueCell);
                if (distance > 21f || distance >= best) continue;
                best = distance;
                knownHostileId = contact.HostileId;
            }
        }

        private static void ReleaseDisabledAmbushOrders(Map map)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (map == null || settings == null || settings.ambushStrikes)
                return;

            // Live feature disable retires the exact feature-owned episode. It does
            // not touch pure Hide orders, foreign player work, or concealment on a
            // different map.
            var episodes = new List<int>(ambushGroups.Keys);
            for (int i = 0; i < episodes.Count; i++)
            {
                AmbushGroupState group;
                if (!ambushGroups.TryGetValue(episodes[i], out group)
                    || group.MapId != map.uniqueID) continue;
                group.AbortReason = "ambush strikes setting disabled";
                AbortAmbushGroup(map, group);
            }

            var actorIds = new List<int>(ordered);
            for (int i = 0; i < actorIds.Count; i++)
            {
                int id = actorIds[i];
                if (passive.Contains(id)
                    || ambushEpisodeByPawn.ContainsKey(id)) continue;
                Pawn pawn = PawnById(map, id);
                if (pawn == null) continue;
                CAIntentContext context;
                bool hasContext = ambushContexts.TryGetValue(id, out context)
                    || CATactical.TryGetContext(pawn, out context);
                CATrace.Pawn(pawn,
                    "ambush order ABORTED - ambush strikes setting disabled",
                    anchor: pawn.Position,
                    intent: hasContext ? (CAIntentContext?)context : null);
                RevealId(id);
                ordered.Remove(id);
                subdue.Remove(id);
                pendingAmbush.Remove(id);
                pendingAmbushMapIds.Remove(id);
                ambushContexts.Remove(id);
                if (CATactical.IsConcealmentOrder(pawn))
                    CATactical.Release(pawn, startNewJob: true,
                        reason: "ambush strikes setting disabled");
            }
        }

        private static void AbortAmbushGroup(Map map, AmbushGroupState group,
            int replacementId = -1)
        {
            var memberIds = new List<int>(group.Members);
            for (int i = 0; i < memberIds.Count; i++)
            {
                int id = memberIds[i];
                Pawn p = PawnById(map, id);
                CAIntentContext context;
                bool hasContext = ambushContexts.TryGetValue(id, out context);
                int mappedEpisode;
                bool ownsEpisode = ambushEpisodeByPawn.TryGetValue(id,
                    out mappedEpisode) && mappedEpisode == group.EpisodeId;
                if (ownsEpisode)
                {
                    if (p != null)
                        CATrace.Pawn(p, "ambush group ABORTED - "
                            + group.AbortReason, anchor: p.Position,
                            intent: hasContext
                                ? (CAIntentContext?)context : null);
                    RevealId(id);
                    ordered.Remove(id);
                    passive.Remove(id);
                    subdue.Remove(id);
                    pendingAmbush.Remove(id);
                    pendingAmbushMapIds.Remove(id);
                    if (p != null && CATactical.MatchesOrder(p, group.EpisodeId,
                        LordJob_CATactical.KindAmbush,
                        LordJob_CATactical.KindAmbushSubdue))
                        CATactical.Release(p, startNewJob: id != replacementId,
                            reason: "ambush group aborted: " + group.AbortReason);
                }
            }
            CleanupAmbushGroup(group);
        }

        private static void SpringAmbushGroup(Map map, AmbushGroupState group,
            Pawn trigger, List<Pawn> hostiles)
        {
            var memberIds = new List<int>(group.Members);
            int maxMelee = 0;
            Pawn striker = null;
            for (int i = 0; i < memberIds.Count; i++)
            {
                Pawn p = PawnById(map, memberIds[i]);
                if (p?.skills == null) continue;
                int level = p.skills.GetSkill(SkillDefOf.Melee).Level;
                maxMelee = Mathf.Max(maxMelee, level);
                // The takedown hand is whoever is actually ON the target.
                if (p.Position.InHorDistOf(trigger.Position, 2.9f)
                    && (striker == null || level > striker.skills
                        .GetSkill(SkillDefOf.Melee).Level))
                    striker = p;
            }
            try
            {
                if (trigger.stances?.stagger != null)
                    trigger.stances.stagger.StaggerFor(60 + maxMelee * 12);
            }
            catch { }
            // The opening blow: incremental, relative, organic. A concealed
            // striker adjacent at the spring lands one precise strike whose
            // lethality EMERGES from skill vs the target's awareness and
            // armor through the ordinary damage pipeline - no scripted
            // execution. Highly trained vs oblivious becomes mechanical;
            // anything less is a fight started with an edge. Subdue ambushes
            // convert the blow into a longer stun instead of steel.
            if (striker != null && !trigger.Dead)
            {
                try
                {
                    int level = striker.skills.GetSkill(SkillDefOf.Melee).Level;
                    bool alerted = trigger.stances?.curStance is Stance_Busy;
                    float awareness = alerted ? 0.35f : 1f;
                    bool capture = subdue.Contains(striker.thingIDNumber);
                    if (capture)
                    {
                        int stunTicks = (int)((120 + level * 30) * awareness);
                        trigger.stances?.stunner?.StunFor(stunTicks, striker);
                        CATrace.Pawn(striker, "takedown OPENING - subdue stun "
                            + stunTicks + " ticks (melee " + level + " vs "
                            + (alerted ? "alerted" : "unaware") + " target)",
                            target: trigger, anchor: striker.Position);
                    }
                    else
                    {
                        float amount = (8f + level * 2.2f) * awareness;
                        float penetration = 0.10f + level * 0.045f;
                        BodyPartRecord neck = null;
                        foreach (var part in trigger.health.hediffSet
                            .GetNotMissingParts())
                            if (part.def == BodyPartDefOf.Neck)
                            { neck = part; break; }
                        var dinfo = new DamageInfo(DamageDefOf.Stab, amount,
                            penetration, -1f, striker, neck);
                        trigger.TakeDamage(dinfo);
                        float stunChance = Mathf.Clamp01(
                            (0.15f + level * 0.045f) * awareness);
                        bool stunned = !trigger.Dead && !trigger.Downed
                            && Rand.Chance(stunChance);
                        if (stunned)
                            trigger.stances?.stunner?.StunFor(
                                90 + level * 18, striker);
                        CATrace.Pawn(striker, "takedown OPENING STRIKE - "
                            + "melee " + level + " vs "
                            + (alerted ? "alerted" : "unaware")
                            + " target; " + amount.ToString("0.#")
                            + " dmg pen " + penetration.ToString("0.##")
                            + (trigger.Dead ? "; target KILLED"
                                : trigger.Downed ? "; target DOWNED"
                                : stunned ? "; target STUNNED" : ""),
                            target: trigger, anchor: striker.Position);
                    }
                }
                catch { }
            }

            for (int i = 0; i < memberIds.Count; i++)
            {
                int id = memberIds[i];
                Pawn p = PawnById(map, id);
                if (p == null || p.Dead || p.Downed
                    || !CATactical.MatchesOrder(p, group.EpisodeId,
                        LordJob_CATactical.KindAmbush,
                        LordJob_CATactical.KindAmbushSubdue)
                    || CATactical.HasForeignPlayerForcedJob(p)) continue;
                CAIntentContext context;
                bool hasContext = ambushContexts.TryGetValue(id, out context);
                Pawn target;
                Job attack;
                bool melee;
                if (!TryAmbushAttack(p, trigger, hostiles,
                    subdue.Contains(id), out target, out attack, out melee))
                {
                    CATrace.Pawn(p,
                        "ambush SPRINGS with group but HOLDS FIRE - no safe equipped-weapon lane",
                        target: trigger, contact: trigger.Position,
                        anchor: p.Position,
                        intent: hasContext ? (CAIntentContext?)context : null);
                    RevealId(id);
                    ordered.Remove(id);
                    passive.Remove(id);
                    subdue.Remove(id);
                    pendingAmbush.Remove(id);
                    pendingAmbushMapIds.Remove(id);
                    CATactical.Release(p, startNewJob: true,
                        reason: "synchronized ambush released; no safe shot");
                    continue;
                }

                IntVec3 hideSpot;
                if (!hidden.TryGetValue(id, out hideSpot)) hideSpot = p.Position;
                if (melee && target != null) NoteStrike(p, target, hideSpot);
                GrantFirstStrike(p);
                CATrace.Pawn(p, "ambush SPRINGS on " + target.LabelShort
                    + " with " + (p.equipment?.Primary != null
                        ? p.equipment.Primary.LabelShort : "unarmed combat")
                    + " at group tick " + Find.TickManager.TicksGame,
                    target: target, contact: trigger.Position,
                    destination: target.Position, anchor: p.Position,
                    intent: hasContext ? (CAIntentContext?)context : null);
                RevealId(id);
                ordered.Remove(id);
                passive.Remove(id);
                subdue.Remove(id);
                pendingAmbush.Remove(id);
                pendingAmbushMapIds.Remove(id);
                CATactical.Release(p, startNewJob: false,
                    reason: "synchronized ambush sprang");
                CAImmediateCombat.RegisterAutonomousRangedJob(p, attack);
                p.jobs.StartJob(attack, JobCondition.InterruptForced);
            }
            CleanupAmbushGroup(group);
        }

        private static bool TryAmbushAttack(Pawn p, Pawn trigger,
            List<Pawn> hostiles, bool nonLethal, out Pawn target, out Job job,
            out bool melee)
        {
            target = trigger;
            job = null;
            melee = false;
            Verb verb = p.TryGetAttackVerb(trigger, allowManualCastWeapons: false);
            if (verb != null && !verb.verbProps.IsMeleeAttack
                && (!verb.CanHitTarget(trigger)
                    || !CAImmediateCombat.HasSafeRangedLane(p, trigger, verb)))
                verb = null;
            if (verb == null || verb.verbProps.IsMeleeAttack
                && !trigger.Position.InHorDistOf(p.Position, 3.2f))
            {
                target = null;
                verb = null;
                for (int i = 0; i < hostiles.Count; i++)
                {
                    Pawn candidate = hostiles[i];
                    Verb candidateVerb = p.TryGetAttackVerb(candidate,
                        allowManualCastWeapons: false);
                    if (candidateVerb == null) continue;
                    if (candidateVerb.verbProps.IsMeleeAttack)
                    {
                        if (!candidate.Position.InHorDistOf(p.Position, 3.2f)) continue;
                    }
                    else if (!candidateVerb.CanHitTarget(candidate)
                        || !CAImmediateCombat.HasSafeRangedLane(
                            p, candidate, candidateVerb)) continue;
                    target = candidate;
                    verb = candidateVerb;
                    break;
                }
            }
            if (target == null || verb == null) return false;

            melee = verb.verbProps.IsMeleeAttack;
            if (nonLethal)
            {
                if (!target.Position.InHorDistOf(p.Position, 2.9f)) return false;
                int skill = p.skills != null
                    ? p.skills.GetSkill(SkillDefOf.Melee).Level : 0;
                target.TakeDamage(new DamageInfo(DamageDefOf.Stun,
                    8f + skill * 0.6f, 0f, -1f, p));
                chokeVictim[p.thingIDNumber] = target.thingIDNumber;
                chokeCycles[p.thingIDNumber] = 0;
                job = JobMaker.MakeJob(JobDefOf.Wait, 600);
                melee = true;
                return true;
            }
            if (melee)
            {
                job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                job.expiryInterval = 400;
            }
            else
            {
                job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                job.maxNumStaticAttacks = 2;
                job.expiryInterval = 900;
                job.checkOverrideOnExpire = false;
                job.endIfCantShootTargetFromCurPos = true;
                job.preventFriendlyFire = true;
            }
            return true;
        }

        private static Pawn PawnById(Map map, int id)
        {
            if (map == null) return null;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == id) return pawns[i];
            return null;
        }

        private static void CleanupAmbushGroup(AmbushGroupState group)
        {
            var ids = new List<int>(group.Members);
            for (int i = 0; i < ids.Count; i++)
            {
                int mappedEpisode;
                if (ambushEpisodeByPawn.TryGetValue(ids[i], out mappedEpisode)
                    && mappedEpisode == group.EpisodeId)
                    ambushEpisodeByPawn.Remove(ids[i]);
                CAIntentContext context;
                if (ambushContexts.TryGetValue(ids[i], out context)
                    && context.EpisodeId == group.EpisodeId)
                    ambushContexts.Remove(ids[i]);
            }
            ambushGroups.Remove(group.EpisodeId);
        }

        // Generic resolved dark adaptation. Biotech supplies this through the active
        // gene tracker's ignore-darkness contract; CA gear and any compatible modded
        // traits/genes/apparel can supply the custom pawn stat without a defName check.
        public static bool SeesInDark(Pawn p)
        {
            return CABattlefieldPerception.VisionProfileFor(p)
                .DarkAdaptation >= 0.95f;
        }

        private static List<Pawn> HostilesOf(Map map)
        {
            var hostiles = new List<Pawn>();
            var all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (h.Dead || h.Downed) continue;
                // Mental-state-aware: manhunters and berserkers discover and spring too.
                if (GenHostility.HostileTo(h, Faction.OfPlayer)) hostiles.Add(h);
            }
            return hostiles;
        }

        // A concealment cell: standable, next to a tree, chunk, or high-fill natural cover,
        // reachable, biased away from the threat.
        public static bool TryFindConcealment(Pawn p, IntVec3 threat, bool hasThreat, out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            var map = p.Map;
            IntVec3 root = p.Position;
            float best = float.MinValue;
            int limit = GenRadial.NumCellsInRadius(34f);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 c = root + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if (!HasConcealmentAt(map, c)) continue;
                if (!p.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                float score = -root.DistanceTo(c);
                if (hasThreat) score += c.DistanceTo(threat) * 0.8f; // deeper is safer
                if (score > best) { best = score; dest = c; }
            }
            return dest.IsValid;
        }

        // Structural concealment: what's actually hiding the stash. 1 poor .. 4 excellent.
        public static int ConcealmentQualityAt(Map map, IntVec3 c)
        {
            int q = 0;
            var adj = GenAdjFast.AdjacentCells8Way(c);
            for (int i = 0; i < adj.Count; i++)
            {
                var a = adj[i];
                if (!a.InBounds(map)) continue;
                var things = a.GetThingList(map);
                for (int j = 0; j < things.Count; j++)
                {
                    var t = things[j];
                    var plant = t as Plant;
                    if (plant != null && plant.def.plant != null && plant.def.plant.IsTree) q += 2;
                    else if (t.def.category == ThingCategory.Item && t.def.fillPercent >= 0.35f) q += 1;
                    else if (t.def.category == ThingCategory.Building && t.def.building != null
                        && t.def.building.isNaturalRock) q += 2;
                }
            }
            return UnityEngine.Mathf.Clamp(q, 1, 4);
        }

        // Environmental preservation: the ground and climate around the stash, read LIVE.
        // Wet jungle and marsh rot a cache; dry sand, stone, and cold preserve it.
        public static float PreservationAt(Map map, IntVec3 c)
        {
            float f = 1f;
            var adj = GenAdjFast.AdjacentCells8Way(c);
            int wet = 0, dry = 0;
            for (int i = -1; i < adj.Count; i++)
            {
                var cell = i < 0 ? c : adj[i];
                if (!cell.InBounds(map)) continue;
                var terr = cell.GetTerrain(map);
                if (terr == null) continue;
                string d = terr.defName;
                if (terr.IsWater || d.Contains("Marsh") || d.Contains("Mud") || d.Contains("Swamp")
                    || terr.fertility > 1.05f) wet++;
                else if (terr.fertility <= 0.01f) dry++;
            }
            f -= 0.06f * wet;
            f += 0.04f * dry;
            float temp = c.GetTemperature(map);
            if (temp < 0f) f += 0.35f;
            else if (temp < 10f) f += 0.15f;
            else if (temp > 30f) f -= 0.10f;
            return UnityEngine.Mathf.Clamp(f, 0.3f, 1.6f);
        }

        // The first-strike advantage: harder, truer hits for the opening seconds after
        // striking from concealment, scaled to the ambusher's best combat skill.
        public static void GrantFirstStrike(Pawn p)
        {
            try
            {
                var def = DefDatabase<HediffDef>.GetNamedSilentFail("CA_FirstStrike");
                if (def == null || p == null || p.health == null) return;
                int melee = p.skills != null ? p.skills.GetSkill(SkillDefOf.Melee).Level : 0;
                int shoot = p.skills != null ? p.skills.GetSkill(SkillDefOf.Shooting).Level : 0;
                float sev = UnityEngine.Mathf.Clamp(
                    0.2f + 0.045f * UnityEngine.Mathf.Max(melee, shoot), 0.2f, 1f);
                var existing = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (existing != null)
                {
                    existing.Severity = UnityEngine.Mathf.Max(existing.Severity, sev);
                    return;
                }
                var h = HediffMaker.MakeHediff(def, p);
                h.Severity = sev;
                p.health.AddHediff(h);
            }
            catch { }
        }

        public static string CoverGradeLabel(int q)
        {
            switch (q)
            {
                case 1: return "thin cover";
                case 2: return "fair cover";
                case 3: return "good cover";
                default: return "dense cover";
            }
        }

        private static bool HasConcealmentAt(Map map, IntVec3 c)
        {
            // Doorframes and wall corners are takedown positions - hug the frame, spring
            // on whoever comes through. The indoor half of the same mechanic.
            for (int i = 0; i < 4; i++)
            {
                var card = c + GenAdj.CardinalDirections[i];
                if (!card.InBounds(map)) continue;
                if (card.GetDoor(map) != null) return true;
                var ed = card.GetEdifice(map);
                if (ed != null && ed.def.passability == Traversability.Impassable
                    && ed.def.fillPercent >= 1f) return true;
            }

            var adj = GenAdjFast.AdjacentCells8Way(c);
            for (int i = 0; i < adj.Count; i++)
            {
                var a = adj[i];
                if (!a.InBounds(map)) continue;
                var things = a.GetThingList(map);
                for (int j = 0; j < things.Count; j++)
                {
                    var t = things[j];
                    var plant = t as Plant;
                    if (plant != null && plant.def.plant != null && plant.def.plant.IsTree) return true;
                    if (t.def.category == ThingCategory.Item && t.def.fillPercent >= 0.35f) return true;
                    if (t.def.category == ThingCategory.Building && t.def.building != null
                        && t.def.building.isNaturalRock) return true;
                }
            }
            return false;
        }
    }

    // The order surface, 1.6-native: FloatMenuMakerMap.ChoicesAtFor is gone - orders now
    // ship as a FloatMenuOptionProvider subclass, discovered by the game automatically.
    public class FloatMenuOptionProvider_AwarenessOrders : FloatMenuOptionProvider
    {
        protected override bool Drafted { get { return true; } }
        protected override bool Undrafted { get { return true; } }
        protected override bool Multiselect { get { return false; } }

        public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
        {
            var s = AwarenessMod.Settings;
            if (s == null) yield break;
            var pawn = context.FirstSelectedPawn;
            if (pawn == null || !pawn.IsColonistPlayerControlled || pawn.Downed) yield break;
            if (pawn.WorkTagIsDisabled(WorkTags.Violent)) yield break;
            // Match the executors: combat orders refuse non-adults, so don't offer them.
            if (pawn.DevelopmentalStage != DevelopmentalStage.Adult) yield break;
            // This is the player's direct hand, so manual orders are available at every
            // autonomy level whether or not the selected pawn is already drafted. The
            // order itself may establish a combat posture; draft is not an entry fee.
            var map = context.map;
            IntVec3 cell = context.ClickedCell;
            if (map == null || !cell.InBounds(map)) yield break;
            var actor = pawn;
            var direct = new List<FloatMenuOption>();

            // Ordinary tactical verbs live behind one explicitly operator-authored
            // surface. Context-object actions such as dragging a clicked casualty or
            // stashing a clicked item remain immediate top-level choices below.
            if (s.ambushStrikes)
            {
                IntVec3 concealment;
                if (HiddenRegistry.TryFindConcealmentNear(map, pawn, cell, 3.9f,
                    out concealment))
                {
                    var near = cell;
                    direct.Add(new FloatMenuOption("Concealment - hide here...", delegate
                    {
                        OrderMenus.OpenWithWho(actor, map, "Hide", false,
                            delegate (List<Pawn> team)
                            { OrderMenus.GroupHide(actor, team, near, map); });
                    }));
                    direct.Add(new FloatMenuOption("Ambush - "
                        + actor.LabelShort + " alone here", delegate
                    {
                        OrderMenus.IndividualAmbush(actor, near, map);
                    }));
                    direct.Add(new FloatMenuOption(
                        "Ambush - compose synchronized team...", delegate
                    {
                        OrderMenus.OpenAmbushComposer(actor, map, near);
                    }));
                }
            }

            if (s.holdOrders && cell.Standable(map))
            {
                var hold = map.GetComponent<HoldMapComponent>();
                if (hold != null)
                {
                    var holdCell = cell;
                    direct.Add(new FloatMenuOption("Position - hold this point...", delegate
                    {
                        OrderMenus.OpenWithWho(actor, map, "Hold", true,
                            delegate (List<Pawn> team)
                            { OrderMenus.GroupHold(actor, team, holdCell, map); });
                    }));

                    var lineA = cell;
                    direct.Add(new FloatMenuOption("Formation - quick line from here to a second point...", delegate
                    {
                        OrderMenus.OpenWithWho(actor, map, "Line", true,
                            delegate (List<Pawn> team)
                            { LineOrders.BeginLineTargeting(actor, team, lineA, map); });
                    }));
                    direct.Add(new FloatMenuOption("Maneuver - advance on this point...", delegate
                    {
                        OrderMenus.OpenWithWho(actor, map, "Advance", true,
                            delegate (List<Pawn> team)
                            { LineOrders.OrderObjective(actor, team, lineA, map); });
                    }));
                    direct.Add(new FloatMenuOption("Formation - sketch one-off deployment shape...", delegate
                    {
                        OrderMenus.OpenWithWho(actor, map, "Draw", true,
                            delegate (List<Pawn> team)
                            { PaintManager.OpenShapeMenu(actor, team, map); });
                    }));

                    // Arrangements: the acceptance sentence made mechanical.
                    // The selected pawn establishes a named, visible object;
                    // any pawn joins by right-clicking its outline. Fully
                    // manual at every autonomy tier.
                    var arrangementComponent =
                        CAArrangementMapComponent.For(map);
                    if (arrangementComponent != null)
                    {
                        // Contingency plans (PROTOTYPE tier): the PLAYER
                        // names, describes, redraws, and deletes their own
                        // plans - no preset vocabulary, ever.
                        var planComponent = CAPlanMapComponent.For(map);
                        if (planComponent != null)
                        {
                            direct.Add(new FloatMenuOption(actor.LabelShort
                                + ": author a plan (name & describe)...",
                                delegate
                            {
                                Find.WindowStack.Add(
                                    new Dialog_CAPlanAuthor(actor));
                            }));
                            for (int pi = 0; pi < planComponent.All.Count;
                                pi++)
                            {
                                var existingPlan = planComponent.All[pi];
                                direct.Add(new FloatMenuOption(
                                    actor.LabelShort + ": EXECUTE "
                                    + existingPlan.name + " ("
                                    + existingPlan.legs.Count + " legs)"
                                    + (existingPlan.description.NullOrEmpty()
                                        ? "" : " - "
                                        + (existingPlan.description.Length
                                            > 40
                                            ? existingPlan.description
                                                .Substring(0, 40) + "..."
                                            : existingPlan.description)),
                                    delegate
                                {
                                    planComponent.Execute(actor,
                                        existingPlan);
                                }));
                                var planToDrop = existingPlan;
                                direct.Add(new FloatMenuOption(
                                    "  delete plan: " + existingPlan.name,
                                    delegate
                                {
                                    planComponent.All.Remove(planToDrop);
                                    Messages.Message(planToDrop.name
                                        + " plan deleted.",
                                        MessageTypeDefOf.SilentInput, false);
                                }));
                            }
                        }
                        direct.Add(new FloatMenuOption(actor.LabelShort
                            + ": LACE status report (element)", delegate
                        {
                            CAStatusReport.SquadReport(actor, map);
                        }));

                        CAArrangementKind[] creatableKinds =
                        {
                            CAArrangementKind.Formation,
                            CAArrangementKind.Line,
                            CAArrangementKind.Hide,
                            CAArrangementKind.Ambush
                        };
                        // Custom gating: an order family is offerable
                        // only where the organization possesses the concept -
                        // "whether a suggestion is even recognizable as an
                        // order," one level down. Hide is folk knowledge and
                        // stays ungated; the military customs must be
                        // practiced, seeded by veterans, or decreed.
                        CAOrganization colonyOrg =
                            CAOrganizationWorldComponent.Current
                                ?.EnsureColony();
                        for (int ck = 0; ck < creatableKinds.Length; ck++)
                        {
                            var kindToCreate = creatableKinds[ck];
                            if (colonyOrg != null
                                && kindToCreate != CAArrangementKind.Hide)
                            {
                                string need =
                                    kindToCreate == CAArrangementKind.Line
                                        ? "line"
                                        : kindToCreate
                                            == CAArrangementKind.Ambush
                                        ? "ambush" : "formation";
                                if (!colonyOrg.HasCustom(need)) continue;
                            }
                            direct.Add(new FloatMenuOption(actor.LabelShort
                                + ": set up a "
                                + CAArrangementMapComponent.KindNoun(
                                    kindToCreate) + " here...", delegate
                            {
                                Find.DesignatorManager.Select(
                                    new Designator_CAArrangementCreate(actor,
                                        kindToCreate));
                            }));
                        }
                        // LAW AND PRACTICE AS ACTS: speak the law where
                        // people are gathered; teach what you know where
                        // they can watch. One entry each, context-gated,
                        // and nothing happens in a tab.
                        {
                            string whyNot2;
                            if (CAGathering.CanConvene(actor, cell, map,
                                out whyNot2))
                            {
                                IntVec3 gCell = cell;
                                direct.Add(new FloatMenuOption(
                                    actor.LabelShort + ": speak the law"
                                    + " here...", delegate
                                {
                                    var kopts = new List<FloatMenuOption>();
                                    for (int ki = 0;
                                        ki < CAPolicyCatalog.Keys.Length;
                                        ki++)
                                    {
                                        string pk =
                                            CAPolicyCatalog.Keys[ki];
                                        kopts.Add(new FloatMenuOption(pk,
                                            delegate
                                        {
                                            var vopts =
                                                new List<FloatMenuOption>();
                                            string[] vals =
                                                CAPolicyCatalog.ValuesOf(
                                                    pk);
                                            for (int vi = 0;
                                                vi < vals.Length; vi++)
                                            {
                                                string val = vals[vi];
                                                vopts.Add(
                                                    new FloatMenuOption(
                                                        val, delegate
                                                {
                                                    CAGathering.Convene(
                                                        actor, gCell, map,
                                                        pk, val);
                                                }));
                                            }
                                            Find.WindowStack.Add(
                                                new FloatMenu(vopts));
                                        }));
                                    }
                                    Find.WindowStack.Add(
                                        new FloatMenu(kopts));
                                }));
                            }
                            CAOrganization colonyOrg2 =
                                CAOrganizationWorldComponent.Current
                                    ?.EnsureColony();
                            if (colonyOrg2 != null)
                            {
                                List<string> canTeach =
                                    CATeaching.Teachable(actor,
                                        colonyOrg2);
                                if (canTeach.Count > 0)
                                {
                                    IntVec3 tCell = cell;
                                    direct.Add(new FloatMenuOption(
                                        actor.LabelShort + ": teach what"
                                        + " they know here...", delegate
                                    {
                                        var topts =
                                            new List<FloatMenuOption>();
                                        for (int ti = 0;
                                            ti < canTeach.Count; ti++)
                                        {
                                            string tk = canTeach[ti];
                                            topts.Add(new FloatMenuOption(
                                                tk, delegate
                                            {
                                                CATeaching.Teach(actor, tk,
                                                    tCell, map);
                                            }));
                                        }
                                        Find.WindowStack.Add(
                                            new FloatMenu(topts));
                                    }));
                                }
                            }
                        }

                        // THE PARLEY, as a standing possibility: right-
                        // click any hostile on the field and ride out to
                        // them. Terms are the player's choice; the answer
                        // moves real lords. The concept itself is what
                        // matters - that the last moment is still a
                        // moment, and someone with standing can walk into
                        // the open and change what happens next.
                        {
                            Pawn foe = null;
                            List<Thing> atCell = cell.GetThingList(map);
                            for (int ti = 0; ti < atCell.Count; ti++)
                            {
                                Pawn cand = atCell[ti] as Pawn;
                                if (cand == null || cand.Dead
                                    || cand.Faction == null
                                    || cand.Faction == Faction.OfPlayer)
                                    continue;
                                try
                                {
                                    if (cand.Faction.HostileTo(
                                        Faction.OfPlayer))
                                    { foe = cand; break; }
                                }
                                catch { }
                            }
                            var parleyComp = CAParleyMapComponent.For(map);
                            if (foe != null && parleyComp != null
                                && !parleyComp.HasMission(actor)
                                && !actor.Downed)
                            {
                                // ONE entry, not six: the terms and the
                                // channel are a choice made INSIDE the
                                // act, never six lines in the list.
                                Pawn foeF = foe;
                                direct.Add(new FloatMenuOption(
                                    actor.LabelShort + ": parley with "
                                    + foeF.Faction.Name + "...", delegate
                                {
                                    var opts = new List<FloatMenuOption>();
                                    CAParleyTerms[] termsList =
                                    {
                                        CAParleyTerms.Withdraw,
                                        CAParleyTerms.Tribute,
                                        CAParleyTerms.Submission
                                    };
                                    bool voice = CAParleyMapComponent
                                        .HasVoiceChannel(actor);
                                    for (int ti = 0; ti < termsList.Length;
                                        ti++)
                                    {
                                        CAParleyTerms t = termsList[ti];
                                        opts.Add(new FloatMenuOption(
                                            "in person - "
                                            + CAParleyMapComponent
                                                .TermsLabel(t), delegate
                                        {
                                            parleyComp.Begin(actor, foeF,
                                                t, true);
                                        }));
                                        if (voice)
                                            opts.Add(new FloatMenuOption(
                                                "by voice - "
                                                + CAParleyMapComponent
                                                    .TermsLabel(t),
                                                delegate
                                            {
                                                parleyComp.Begin(actor,
                                                    foeF, t, false);
                                            }));
                                    }
                                    Find.WindowStack.Add(
                                        new FloatMenu(opts));
                                }));
                            }
                        }

                        // PORTER DUTY: cargo needs hands. Right-click a
                        // stack with a squad member selected and they
                        // become a porter - the haul goes into their
                        // inventory, so it MARCHES with the column and
                        // arrives where they arrive. Native TakeInventory
                        // job; nothing invented.
                        {
                            List<Thing> here = cell.GetThingList(map);
                            for (int hi = 0; hi < here.Count; hi++)
                            {
                                Thing cargo = here[hi];
                                if (cargo.def == null
                                    || cargo.def.category
                                        != ThingCategory.Item
                                    || cargo.def.EverHaulable == false)
                                    continue;
                                if (actor.inventory == null) break;
                                Thing carg = cargo;
                                int take = carg.stackCount;
                                try
                                {
                                    int cap = MassUtility
                                        .CountToPickUpUntilOverEncumbered(
                                            actor, carg);
                                    if (cap <= 0) break;
                                    take = System.Math.Min(take, cap);
                                }
                                catch { }
                                int takeFinal = take;
                                direct.Add(new FloatMenuOption(
                                    actor.LabelShort + ": carry "
                                    + takeFinal + "x " + carg.LabelNoCount
                                    + " as porter (marches with the"
                                    + " column)", delegate
                                {
                                    Job hj = JobMaker.MakeJob(
                                        JobDefOf.TakeInventory, carg);
                                    hj.count = takeFinal;
                                    hj.playerForced = true;
                                    actor.jobs.TryTakeOrderedJob(hj);
                                    CATrace.Pawn(actor, "porter tasked - "
                                        + takeFinal + "x "
                                        + carg.LabelNoCount
                                        + " into the column's load",
                                        anchor: carg.Position);
                                }));
                                break;
                            }
                        }

                        // THE ARMED CARAVAN LANE: the map is the world, so
                        // a march to another settlement is a real crossing
                        // - the squad moves as an armed column, and the
                        // destination organization NOTICES. Passage rides
                        // agreements: military access or
                        // a protection agreement authorizes the column; Ally stance
                        // welcomes it informally; anything else records an
                        // uninvited armed entry in their ledger. Ambush
                        // risk en route is the world as it already is -
                        // hostiles hunt.
                        {
                            CAOrganizationWorldComponent caraComp =
                                CAOrganizationWorldComponent.Current;
                            CARegionalWorldComponent caraReg =
                                CARegionalWorldComponent.Current;
                            CAOrganization destOrg = null;
                            CARegionalSettlementRecord destRec = null;
                            if (caraComp != null && caraReg != null)
                                for (int ri = 0;
                                    ri < caraReg.Records.Count; ri++)
                                {
                                    var rec = caraReg.Records[ri];
                                    if (rec.lastMapId != map.uniqueID
                                        || rec.localRect == CellRect.Empty
                                        || !rec.localRect.ExpandedBy(5)
                                            .Contains(cell)) continue;
                                    destOrg = caraComp.ByKey(
                                        rec.regionalId + "#" + rec.slot);
                                    destRec = rec;
                                    break;
                                }
                            // DESTINATION TRADE: the caravan arrived, and
                            // arrival MEANS something - their treasury
                            // buys your goods, their stores sell for your
                            // silver. Conservation both ways: their purse
                            // is real, their shelves are real, and the
                            // exchange happens where you stand.
                            if (destOrg != null && destRec != null
                                && actor.Position.InHorDistOf(cell, 20f)
                                && !actor.Downed)
                            {
                                CAOrganization tOrg = destOrg;
                                CARegionalSettlementRecord tRec = destRec;
                                CAOrganization tCol = CAOrganizationWorldComponent
                                    .Current?.EnsureColony();
                                string tStance = CAOrganizationStances
                                    .Between(tCol, tOrg);
                                if (tStance != "Hostile")
                                    direct.Add(new FloatMenuOption(
                                        actor.LabelShort + ": trade with "
                                        + tOrg.name + "...", delegate
                                    {
                                        CADestinationTrade.OpenMenu(actor,
                                            tOrg, tRec, map);
                                    }));
                            }

                            // ASKING THEM IN is a visit: stand on their
                            // ground and say it.
                            {
                                CAOrganizationWorldComponent fComp =
                                    CAOrganizationWorldComponent.Current;
                                if (fComp != null)
                                    for (int fi = 0;
                                        fi < fComp.Organizations.Count;
                                        fi++)
                                    {
                                        CAOrganization fo =
                                            fComp.Organizations[fi];
                                        if (!fo.organizationKey.StartsWith(
                                            "frontier:")
                                            || fo.affiliatedWithPlayer
                                            || fo.memberPawnIds.Count == 0)
                                            continue;
                                        bool near = false;
                                        var sp2 =
                                            map.mapPawns.AllPawnsSpawned;
                                        for (int pi = 0; pi < sp2.Count;
                                            pi++)
                                            if (fo.memberPawnIds.Contains(
                                                    sp2[pi].thingIDNumber)
                                                && sp2[pi].Position
                                                    .InHorDistOf(cell, 12f)
                                                && actor.Position
                                                    .InHorDistOf(cell, 12f))
                                            { near = true; break; }
                                        if (!near) continue;
                                        CAOrganization fOrg = fo;
                                        direct.Add(new FloatMenuOption(
                                            actor.LabelShort + ": ask "
                                            + fOrg.name + " to join us",
                                            delegate
                                        {
                                            CAFrontierAffiliation
                                                .InviteInPerson(actor,
                                                    fOrg);
                                        }));
                                        break;
                                    }
                            }

                            // DIPLOMACY AS AN ACT: send this person to
                            // their ground with a proposal. They walk, they
                            // arrive, they speak - and the answer happens
                            // where they are standing, not in a tab.
                            var envoyComp = CAParleyMapComponent.For(map);
                            if (destOrg != null && envoyComp != null
                                && !envoyComp.HasEnvoy(actor)
                                && !actor.Downed)
                            {
                                CAOrganization eOrg = destOrg;
                                IntVec3 eCell = cell;
                                direct.Add(new FloatMenuOption(
                                    actor.LabelShort + ": carry a proposal"
                                    + " to " + eOrg.name + "...", delegate
                                {
                                    IntVec3 dest = eCell;
                                    CARegionalSettlementRecord eRec2 =
                                        null;
                                    var eWorld2 = CARegionalWorldComponent
                                        .Current;
                                    if (eWorld2 != null)
                                        foreach (var r2 in
                                            eWorld2.ForMap(actor.Map))
                                            if (r2.regionalId + "#"
                                                + r2.slot
                                                == eOrg.organizationKey)
                                            { eRec2 = r2; break; }
                                    if (eRec2?.layout != null)
                                    {
                                        IntVec3 gate = eRec2.layout
                                            .ApproachCell(actor.Position);
                                        if (gate.IsValid) dest = gate;
                                    }
                                    Find.WindowStack.Add(
                                        new Dialog_CAComposeAgreement(eOrg,
                                            actor, dest));
                                }));
                                string warnWhy;
                                if (CAWarningSurface.CanWarn(eOrg,
                                        out warnWhy)
                                    && (actor.Position.InHorDistOf(eCell,
                                            40f)
                                        || CAParleyMapComponent
                                            .HasVoiceChannel(actor)))
                                    direct.Add(new FloatMenuOption(
                                        actor.LabelShort
                                        + ": carry warning to "
                                        + eOrg.name, delegate
                                    {
                                        CAWarningSurface.Send(eOrg, actor);
                                    }));
                                bool playerSuzOf = CAOrganizationWorldComponent
                                    .Current != null
                                    && CAOrganizationWorldComponent.Current
                                        .HasActiveAgreement("player",
                                            eOrg.organizationKey,
                                            "protection")
                                    && MainTabWindow_CAOrganization
                                        .ProtectorOf(
                                            CAOrganizationWorldComponent
                                                .Current,
                                            eOrg.organizationKey)
                                        == "player";
                                if (playerSuzOf
                                    && actor.Position.InHorDistOf(eCell,
                                        20f))
                                    direct.Add(new FloatMenuOption(
                                        actor.LabelShort
                                        + ": speak the imposed law to "
                                        + eOrg.name + "...", delegate
                                    {
                                        var kopts2 =
                                            new List<FloatMenuOption>();
                                        for (int ki2 = 0; ki2
                                            < CAPolicyCatalog.Keys.Length;
                                            ki2++)
                                        {
                                            string pk2 =
                                                CAPolicyCatalog.Keys[ki2];
                                            kopts2.Add(new FloatMenuOption(
                                                pk2, delegate
                                            {
                                                var vopts2 = new
                                                    List<FloatMenuOption>();
                                                string[] vals2 =
                                                    CAPolicyCatalog
                                                        .ValuesOf(pk2);
                                                for (int vi2 = 0;
                                                    vi2 < vals2.Length;
                                                    vi2++)
                                                {
                                                    string val2 =
                                                        vals2[vi2];
                                                    vopts2.Add(
                                                        new FloatMenuOption(
                                                            val2, delegate
                                                    {
                                                        MainTabWindow_CAOrganization
                                                            .ImposePolicy(
                                                            CAOrganizationWorldComponent
                                                                .Current,
                                                            eOrg, pk2,
                                                            val2);
                                                    }));
                                                }
                                                Find.WindowStack.Add(
                                                    new FloatMenu(vopts2));
                                            }));
                                        }
                                        Find.WindowStack.Add(
                                            new FloatMenu(kopts2));
                                    }));
                            }

                            int caraSquad = SquadComponent.SquadOf(actor);
                            if (destOrg != null && caraSquad > 0
                                && (SquadComponent.IsLeader(actor)
                                    || CAOrganizationAuthority
                                        .HasColonyCommand(actor)))
                            {
                                CAOrganization dOrg = destOrg;
                                IntVec3 dCell = cell;
                                direct.Add(new FloatMenuOption(
                                    "Maneuver - caravan march to "
                                    + dOrg.name + " (squad " + caraSquad
                                    + ", armed column)", delegate
                                {
                                    var marchers = new List<Pawn>();
                                    var cols3 = map.mapPawns
                                        .FreeColonistsSpawned;
                                    for (int mi = 0; mi < cols3.Count;
                                        mi++)
                                        if (SquadComponent.SquadOf(
                                                cols3[mi]) == caraSquad
                                            && !cols3[mi].Downed)
                                            marchers.Add(cols3[mi]);
                                    // COLUMN SPACING DOCTRINE: a column
                                    // is not a crowd. The armed take
                                    // POINT and REAR; carriers and the
                                    // unarmed ride the MAIN BODY between
                                    // them; each element walks the same
                                    // road with an interval, so an ambush
                                    // catches part of the column, never
                                    // all of it. Order: armed first
                                    // (point), then burdened/unarmed, then
                                    // the remaining armed (rear).
                                    var point = new List<Pawn>();
                                    var body = new List<Pawn>();
                                    var rear = new List<Pawn>();
                                    for (int mi = 0; mi < marchers.Count;
                                        mi++)
                                    {
                                        Pawn m = marchers[mi];
                                        bool armedM = m.equipment != null
                                            && m.equipment.Primary != null
                                            && !m.WorkTagIsDisabled(
                                                WorkTags.Violent);
                                        bool burdened = false;
                                        try
                                        {
                                            burdened = m.inventory != null
                                                && m.inventory
                                                    .innerContainer
                                                    .Count > 0;
                                        }
                                        catch { }
                                        if (armedM && !burdened)
                                        {
                                            if (point.Count <= rear.Count)
                                                point.Add(m);
                                            else rear.Add(m);
                                        }
                                        else body.Add(m);
                                    }
                                    if (point.Count == 0 && body.Count > 1)
                                    {
                                        point.Add(body[0]);
                                        body.RemoveAt(0);
                                    }
                                    var ordered = new List<Pawn>();
                                    ordered.AddRange(point);
                                    ordered.AddRange(body);
                                    ordered.AddRange(rear);
                                    marchers = ordered;

                                    // ROUTE BY PLAN LEGS: an authored plan
                                    // whose last leg lies near the
                                    // destination IS the road - the column
                                    // walks its waypoints in order and
                                    // arrives last. Roads are drawn, named,
                                    // and reusable; no plan means the
                                    // direct march.
                                    List<IntVec3> route = null;
                                    string routeName = null;
                                    var planComp2 = map.GetComponent
                                        <CAPlanMapComponent>();
                                    if (planComp2 != null)
                                    {
                                        float bestEnd = 9999f;
                                        for (int pi = 0;
                                            pi < planComp2.All.Count; pi++)
                                        {
                                            var pl = planComp2.All[pi];
                                            if (pl.legs == null
                                                || pl.legs.Count < 1)
                                                continue;
                                            float d = pl.legs[
                                                pl.legs.Count - 1]
                                                .DistanceTo(dCell);
                                            if (d < 60f && d < bestEnd)
                                            {
                                                bestEnd = d;
                                                route = pl.legs;
                                                routeName = pl.name;
                                            }
                                        }
                                    }
                                    int placedM = 0;
                                    for (int mi = 0; mi < marchers.Count;
                                        mi++)
                                    {
                                        IntVec3 slot2 = dCell
                                            + GenRadial.RadialPattern[
                                                placedM];
                                        int guard = 0;
                                        while ((!slot2.InBounds(map)
                                            || !slot2.Standable(map))
                                            && guard < 30)
                                        {
                                            placedM++;
                                            guard++;
                                            slot2 = dCell + GenRadial
                                                .RadialPattern[placedM];
                                        }
                                        placedM++;
                                        Pawn walker = marchers[mi];
                                        string element = mi < point.Count
                                            ? "point"
                                            : mi < point.Count + body.Count
                                            ? "main body" : "rear";
                                        // The interval: later elements
                                        // hold before stepping off, so the
                                        // column strings out along the
                                        // road instead of clumping.
                                        int holdTicks = mi < point.Count
                                            ? 0
                                            : mi < point.Count + body.Count
                                            ? 120 : 240;
                                        if (holdTicks > 0)
                                        {
                                            Job wait = JobMaker.MakeJob(
                                                JobDefOf.Wait,
                                                walker.Position);
                                            wait.expiryInterval =
                                                holdTicks;
                                            wait.playerForced = true;
                                            walker.jobs.TryTakeOrderedJob(
                                                wait);
                                        }
                                        CATrace.Pawn(walker,
                                            "column march - " + element
                                            + " element, interval "
                                            + holdTicks + " ticks",
                                            anchor: walker.Position);
                                        if (route != null)
                                        {
                                            for (int li = 0;
                                                li < route.Count; li++)
                                            {
                                                IntVec3 wp = route[li];
                                                if (!wp.InBounds(map)
                                                    || !wp.Standable(map))
                                                    continue;
                                                Job leg = JobMaker.MakeJob(
                                                    JobDefOf.Goto, wp);
                                                leg.playerForced = true;
                                                if (li == 0
                                                    && holdTicks == 0)
                                                    walker.jobs
                                                        .TryTakeOrderedJob(
                                                            leg);
                                                else
                                                    walker.jobs.jobQueue
                                                        .EnqueueLast(leg);
                                            }
                                            Job last = JobMaker.MakeJob(
                                                JobDefOf.Goto, slot2);
                                            last.playerForced = true;
                                            walker.jobs.jobQueue
                                                .EnqueueLast(last);
                                        }
                                        else
                                        {
                                            Job go = JobMaker.MakeJob(
                                                JobDefOf.Goto, slot2);
                                            go.playerForced = true;
                                            if (holdTicks == 0)
                                                walker.jobs
                                                    .TryTakeOrderedJob(go);
                                            else
                                                walker.jobs.jobQueue
                                                    .EnqueueLast(go);
                                        }
                                    }
                                    // PACK TRAIN: trained, non-downed
                                    // colony pack animals with a master
                                    // in the column walk with it - the
                                    // beasts that can actually carry.
                                    int beasts = 0;
                                    var animals = map.mapPawns
                                        .SpawnedColonyAnimals;
                                    for (int ai = 0; ai < animals.Count;
                                        ai++)
                                    {
                                        Pawn beast = animals[ai];
                                        if (beast.Downed
                                            || beast.RaceProps == null
                                            || !beast.RaceProps
                                                .packAnimal) continue;
                                        Pawn master = beast.playerSettings
                                            != null
                                            ? beast.playerSettings.Master
                                            : null;
                                        if (master == null
                                            || !marchers.Contains(master))
                                            continue;
                                        IntVec3 bslot = dCell
                                            + GenRadial.RadialPattern[
                                                placedM];
                                        int bguard = 0;
                                        while ((!bslot.InBounds(map)
                                            || !bslot.Standable(map))
                                            && bguard < 30)
                                        {
                                            placedM++;
                                            bguard++;
                                            bslot = dCell + GenRadial
                                                .RadialPattern[placedM];
                                        }
                                        placedM++;
                                        Job bgo = JobMaker.MakeJob(
                                            JobDefOf.Goto, bslot);
                                        bgo.playerForced = true;
                                        beast.jobs.TryTakeOrderedJob(bgo);
                                        beasts++;
                                    }
                                    CAOrganization colonyOrg =
                                        caraComp.EnsureColony();
                                    bool passage = caraComp
                                        .HasActiveAgreement("player",
                                            dOrg.organizationKey,
                                            "military access",
                                            "protection");
                                    string colStance = CAOrganizationStances
                                        .Between(colonyOrg, dOrg);
                                    if (passage)
                                    {
                                        colonyOrg.Record("relations",
                                            "caravan marched to "
                                            + dOrg.name
                                            + " - passage exercised"
                                            + " under an agreement"
                                            + (routeName != null
                                                ? " via the " + routeName
                                                + " road" : ""));
                                        dOrg.Record("relations",
                                            "an armed column from "
                                            + colonyOrg.name
                                            + " crosses under the"
                                            + " passage agreement");
                                    }
                                    else if (colStance == "Ally")
                                    {
                                        colonyOrg.Record("relations",
                                            "caravan marched to "
                                            + dOrg.name
                                            + " - welcomed as allies");
                                        dOrg.Record("relations",
                                            "an allied column from "
                                            + colonyOrg.name
                                            + " arrives on our ground");
                                    }
                                    else
                                    {
                                        colonyOrg.Record("relations",
                                            "caravan marched to "
                                            + dOrg.name
                                            + " - uninvited, no"
                                            + " passage terms");
                                        dOrg.Record("relations",
                                            "an armed column from "
                                            + colonyOrg.name
                                            + " entered our ground"
                                            + " UNINVITED - no passage"
                                            + " terms stand");
                                    }
                                    Messages.Message("Caravan marching"
                                        + " to " + dOrg.name + " - "
                                        + marchers.Count
                                        + " in the column"
                                        + (beasts > 0
                                            ? " with " + beasts
                                            + " pack animal(s)" : "")
                                        + (routeName != null
                                            ? " via the " + routeName
                                            + " road" : "")
                                        + " - order of march: "
                                        + point.Count + " point, "
                                        + body.Count + " main body, "
                                        + rear.Count + " rear"
                                        + (passage
                                            ? " (passage agreement)"
                                            : colStance == "Ally"
                                            ? " (allied welcome)"
                                            : " (UNINVITED - their"
                                            + " ledger notes it)")
                                        + ".",
                                        MessageTypeDefOf.TaskCompletion,
                                        false);
                                }));
                            }
                        }

                        // Patrol base zones join the palette: right-click
                        // inside one to occupy it - a hold at a free cell,
                        // watching outward from the zone's center.
                        var patrolZone = map.zoneManager.ZoneAt(cell)
                            as Zone_CAPatrolBase;
                        if (patrolZone != null)
                        {
                            var pzHold = map.GetComponent<HoldMapComponent>();
                            Zone_CAPatrolBase pz = patrolZone;
                            if (pzHold != null)
                                direct.Add(new FloatMenuOption(
                                    actor.LabelShort + ": occupy " + pz.label
                                    + " (hold, watch outward)", delegate
                                {
                                    List<IntVec3> pcells = pz.Cells;
                                    IntVec3 slot = IntVec3.Invalid;
                                    for (int pc = 0; pc < pcells.Count; pc++)
                                    {
                                        IntVec3 cand = pcells[pc];
                                        if (!cand.Standable(map)) continue;
                                        bool taken = false;
                                        var cols = map.mapPawns
                                            .FreeColonistsSpawned;
                                        for (int ci = 0; ci < cols.Count;
                                            ci++)
                                            if (cols[ci] != actor
                                                && cols[ci].Position == cand)
                                            { taken = true; break; }
                                        if (!taken) { slot = cand; break; }
                                    }
                                    if (!slot.IsValid) return;
                                    long sx = 0, sz = 0;
                                    for (int pc = 0; pc < pcells.Count; pc++)
                                    {
                                        sx += pcells[pc].x;
                                        sz += pcells[pc].z;
                                    }
                                    IntVec3 centroid = new IntVec3(
                                        (int)(sx / pcells.Count), 0,
                                        (int)(sz / pcells.Count));
                                    IntVec3 dirv = slot - centroid;
                                    IntVec3 watch =
                                        dirv == IntVec3.Zero
                                        ? IntVec3.Invalid
                                        : slot + new IntVec3(
                                            System.Math.Sign(dirv.x) * 10,
                                            0,
                                            System.Math.Sign(dirv.z) * 10);
                                    var pzCtx = CACombatIntent.Operator(
                                        actor, actor,
                                        CAIntentController.Hold);
                                    if (watch.IsValid)
                                        pzHold.OrderHoldWatching(actor,
                                            slot, watch, pzCtx);
                                    else
                                        pzHold.OrderHold(actor, slot,
                                            pzCtx);
                                }));
                        }

                        CAArrangement hit = arrangementComponent.At(cell);
                        if (hit != null)
                        {
                            var hitTarget = hit;
                            var joinCellHit = cell;
                            if (!hit.memberIds.Contains(actor.thingIDNumber))
                                direct.Add(new FloatMenuOption(
                                    actor.LabelShort + ": join "
                                    + hit.name + " ("
                                    + hit.memberIds.Count + " holding)",
                                    delegate
                                {
                                    arrangementComponent.Join(actor,
                                        hitTarget, joinCellHit);
                                }));
                            direct.Add(new FloatMenuOption("Alter - expand "
                                + hit.name + " (drag cells)...", delegate
                            {
                                Find.DesignatorManager.Select(
                                    new Designator_CAArrangementEdit(
                                        hitTarget, false));
                            }));
                            direct.Add(new FloatMenuOption("Alter - carve "
                                + hit.name + " (drag cells)...", delegate
                            {
                                Find.DesignatorManager.Select(
                                    new Designator_CAArrangementEdit(
                                        hitTarget, true));
                            }));
                            direct.Add(new FloatMenuOption("Alter - disband "
                                + hit.name, delegate
                            {
                                arrangementComponent.Disband(hitTarget);
                            }));
                        }
                    }

                    // Painted command layers: deploy to what the player drew
                    // in the Command tab. Options appear only when the layer
                    // exists; clicking ON a painted line offers direct
                    // one-pawn assignment to it.
                    var paintedOverlay = CAOverlayMapComponent.For(map);
                    if (paintedOverlay != null)
                    {
                        var joinCell = cell;
                        CAOverlayKind[] lineKinds =
                        {
                            CAOverlayKind.DefensiveLine,
                            CAOverlayKind.FallbackLine,
                            CAOverlayKind.OffensiveLine
                        };
                        string[] lineNames =
                        { "defensive", "fallback", "offensive" };
                        for (int lk = 0; lk < lineKinds.Length; lk++)
                        {
                            if (!paintedOverlay.Contains(lineKinds[lk],
                                joinCell)) continue;
                            var kindHere = lineKinds[lk];
                            direct.Add(new FloatMenuOption("Position - "
                                + actor.LabelShort + " joins the "
                                + lineNames[lk] + " line here", delegate
                            {
                                OrderMenus.JoinPaintedLineAt(actor, kindHere,
                                    joinCell, map);
                            }));
                            break;
                        }
                    }
                    if (paintedOverlay != null
                        && paintedOverlay.HasLayer(CAOverlayKind.DefensiveLine))
                        direct.Add(new FloatMenuOption(
                            "Formation - man the painted defensive line...",
                            delegate
                        {
                            OrderMenus.OpenWithWho(actor, map, "Man line", true,
                                delegate (List<Pawn> team)
                                { OrderMenus.DeployToPaintedLine(actor, team,
                                    CAOverlayKind.DefensiveLine, map); });
                        }));
                    if (paintedOverlay != null
                        && paintedOverlay.HasLayer(CAOverlayKind.OffensiveLine))
                        direct.Add(new FloatMenuOption(
                            "Maneuver - advance to the painted offensive line...",
                            delegate
                        {
                            OrderMenus.OpenWithWho(actor, map, "Advance line",
                                true, delegate (List<Pawn> team)
                                { OrderMenus.DeployToPaintedLine(actor, team,
                                    CAOverlayKind.OffensiveLine, map); });
                        }));
                    if (paintedOverlay != null
                        && paintedOverlay.HasLayer(CAOverlayKind.FallbackLine))
                        direct.Add(new FloatMenuOption(
                            "Maneuver - collapse to the painted fallback line...",
                            delegate
                        {
                            OrderMenus.OpenWithWho(actor, map, "Collapse",
                                true, delegate (List<Pawn> team)
                                { OrderMenus.DeployToPaintedLine(actor, team,
                                    CAOverlayKind.FallbackLine, map); });
                        }));
                }
            }

            if (s.withdrawals && cell.Standable(map))
            {
                var wd = map.GetComponent<WithdrawalMapComponent>();
                if (wd != null && wd.CanOfferSolo(pawn, cell))
                {
                    var dest = cell;
                    var primary = pawn.equipment != null ? pawn.equipment.Primary : null;
                    if (primary != null && primary.def.IsRangedWeapon)
                        direct.Add(new FloatMenuOption("Maneuver - suppressive movement here (solo)",
                            delegate
                            {
                                wd.OrderSolo(actor, dest,
                                    CACombatIntent.Operator(actor, actor,
                                        CAIntentController.Withdrawal));
                            }));
                    direct.Add(new FloatMenuOption(
                        "Maneuver - covered bounded movement here...", delegate
                        {
                            OrderMenus.OpenWithPartner(actor, map,
                                "Covered movement", true,
                                partner => wd.CoveredPartnerExclusion(
                                    actor, partner, dest),
                                partner => wd.OrderCovered(actor, partner,
                                    dest, CACombatIntent.Operator(actor,
                                        actor,
                                        CAIntentController.Withdrawal)));
                        }));
                }
            }

            if (s.battleDrills)
            {
                var door = cell.GetDoor(map);
                var drills = map.GetComponent<DrillsMapComponent>();
                if (door != null && SquadComponent.IsLeader(pawn))
                {
                    var d2 = door;
                    var ftA = Drills.FireteamOf(pawn, 1, map);
                    var ftB = Drills.FireteamOf(pawn, 2, map);
                    if (ftA.Count > 0)
                        direct.Add(new FloatMenuOption("Fire team A: stack on this door (" + ftA.Count + ")",
                            delegate { Drills.StackOnDoor(ftA, d2, map); }));
                    if (ftB.Count > 0)
                        direct.Add(new FloatMenuOption("Fire team B: stack on this door (" + ftB.Count + ")",
                            delegate { Drills.StackOnDoor(ftB, d2, map); }));
                    if (drills != null && drills.StackCount() >= 2)
                        direct.Add(new FloatMenuOption("Breach all stacks ("
                            + drills.StackCount() + " doors)",
                            delegate { Drills.BreachAllStacks(map); }));
                }
            }

            if (direct.Count > 0)
            {
                var directMenu = direct;
                // The palette is the PAWN'S, not a generic combat menu: the
                // entry names who is being commanded and what authority they
                // carry, and the submenu opens under that identity. This is
                // pawn-palette v1 - role-aware framing; propagation and
                // command-standing checks ride beneath it.
                string role = SquadComponent.IsLeader(pawn)
                    ? "Squad Leader"
                    : SquadComponent.IsFireteamLeader(pawn)
                    ? ("Fire Team " + (SquadComponent.FireteamOf(pawn) == 1
                        ? "A" : "B") + " Lead")
                    : SquadComponent.SquadOf(pawn) > 0
                    ? ("Squad " + SquadComponent.SquadOf(pawn)
                        + (SquadComponent.FireteamOf(pawn) > 0
                            ? ", Team " + (SquadComponent.FireteamOf(pawn) == 1
                                ? "A" : "B") : ""))
                    : AutonomyComponent.LevelNames[
                        AutonomyComponent.LevelOf(pawn)];
                // Information architecture: never the flat everything-list.
                // Options partition into categories by what they ARE -
                // establish (create new intent), act (execute/report/alter),
                // position (join/hold/conceal), maneuver (move) - and only
                // non-empty categories appear. Context already gates entries
                // (joins need geometry under the click, executes need plans).
                var establishOpts = new List<FloatMenuOption>();
                var actOpts = new List<FloatMenuOption>();
                var positionOpts = new List<FloatMenuOption>();
                var maneuverOpts = new List<FloatMenuOption>();
                for (int di = 0; di < directMenu.Count; di++)
                {
                    string lbl = directMenu[di].Label ?? "";
                    if (lbl.Contains(": set up")
                        || lbl.Contains(": author"))
                        establishOpts.Add(directMenu[di]);
                    else if (lbl.Contains(": EXECUTE")
                        || lbl.Contains("delete plan")
                        || lbl.Contains("LACE")
                        || lbl.StartsWith("Alter - "))
                        actOpts.Add(directMenu[di]);
                    else if (lbl.StartsWith("Maneuver - "))
                        maneuverOpts.Add(directMenu[di]);
                    else
                        positionOpts.Add(directMenu[di]);
                }
                var headedMenu = new List<FloatMenuOption>
                {
                    new FloatMenuOption("— " + pawn.LabelShort + " · " + role
                        + " —", null)
                };
                if (establishOpts.Count > 0)
                    headedMenu.Add(new FloatMenuOption("Establish... ("
                        + establishOpts.Count + ")", delegate
                    { Find.WindowStack.Add(new FloatMenu(establishOpts)); }));
                if (positionOpts.Count > 0)
                    headedMenu.Add(new FloatMenuOption("Position... ("
                        + positionOpts.Count + ")", delegate
                    { Find.WindowStack.Add(new FloatMenu(positionOpts)); }));
                if (maneuverOpts.Count > 0)
                    headedMenu.Add(new FloatMenuOption("Maneuver... ("
                        + maneuverOpts.Count + ")", delegate
                    { Find.WindowStack.Add(new FloatMenu(maneuverOpts)); }));
                if (actOpts.Count > 0)
                    headedMenu.Add(new FloatMenuOption("Act & report... ("
                        + actOpts.Count + ")", delegate
                    { Find.WindowStack.Add(new FloatMenu(actOpts)); }));
                yield return new FloatMenuOption(pawn.LabelShort
                    + " — orders (" + role + ")...", delegate
                {
                    Find.WindowStack.Add(new FloatMenu(headedMenu));
                });
            }

            // Item-first stash is earned only by an actual clicked haulable.
            if (s.ambushStrikes)
            {
                var item = cell.GetFirstItem(map);
                if (item != null && item.def.EverHaulable
                    && !HiddenThingsComponent.IsStashed(item))
                {
                    float mass = item.GetStatValue(StatDefOf.Mass) * item.stackCount;
                    if (!(item is Corpse) && mass > 35f)
                        yield return new FloatMenuOption("Cannot stash "
                            + item.LabelShort + ": too bulky to conceal", null);
                    else
                    {
                        var thing = item;
                        yield return new FloatMenuOption("Stash " + thing.LabelShort
                            + " in concealment...",
                            delegate
                            { AmbushOrderPatch.BeginStashTargeting(actor, thing, map); });
                    }
                }
            }

            if (s.dragOrders)
            {
                var clickedPawns = context.ClickedPawns;
                if (clickedPawns != null)
                {
                    for (int i = 0; i < clickedPawns.Count; i++)
                    {
                        var casualty = clickedPawns[i];
                        if (casualty == null || casualty == pawn || casualty.Dead
                            || !casualty.Downed || casualty.InBed()
                            || !pawn.CanReach(casualty, PathEndMode.Touch,
                                Danger.Deadly)) continue;
                        var c2 = casualty;
                        yield return new FloatMenuOption("Drag "
                            + casualty.LabelShort + " to...",
                            delegate { DragOrders.BeginDragTargeting(actor, c2, map); });
                        break;
                    }
                }
            }

            // Dual wield: take a one-handed weapon off the ground into the off hand.
            if (s.dualWield && pawn.equipment != null)
            {
                var wep = cell.GetFirstItem(map) as ThingWithComps;
                if (wep != null && OffhandComponent.CanBeOffhand(wep.def)
                    && wep != pawn.equipment.Primary
                    && pawn.CanReach(wep, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    var thing = wep;
                    yield return new FloatMenuOption("Equip " + wep.LabelShort + " as offhand", delegate
                    {
                        var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CA_EquipOffhand"), thing);
                        job.count = 1;
                        actor.jobs.TryTakeOrderedJob(job);
                    });
                }
            }
        }
    }

    public static class AmbushOrderPatch
    {
        // 1.6: ChoicesAtFor no longer exists; the provider above owns the order surface.
        public static void TryInstall(Harmony harmony) { }

        // The choose-where half of the stash order: the game's own targeter, clicks snap
        // to the nearest concealment, cover and ground quality shown live before committing.
        public static void BeginStashTargeting(Pawn actor, Thing thing, Map map)
        {
            var tp = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false
            };
            Find.Targeter.BeginTargeting(tp,
                delegate (LocalTargetInfo targ)
                {
                    IntVec3 spot;
                    if (targ.Cell.InBounds(map)
                        && HiddenRegistry.TryFindConcealmentNear(map, actor, targ.Cell, 2.9f, out spot))
                        HiddenRegistry.OrderStash(actor, thing, spot);
                    else
                        Messages.Message("No concealment there - stash beside trees, chunks, or rock.",
                            MessageTypeDefOf.RejectInput, false);
                },
                delegate (LocalTargetInfo targ)
                {
                    IntVec3 spot;
                    if (targ.Cell.InBounds(map)
                        && HiddenRegistry.TryFindConcealmentNear(map, actor, targ.Cell, 2.9f, out spot))
                    {
                        int q = HiddenRegistry.ConcealmentQualityAt(map, spot);
                        float pres = HiddenRegistry.PreservationAt(map, spot);
                        string desc = HiddenRegistry.CoverGradeLabel(q);
                        if (pres >= 1.2f) desc += ", preserving ground";
                        else if (pres <= 0.75f) desc += ", spoiling ground";
                        Widgets.MouseAttachedLabel(desc, 0f, 0f, null);
                    }
                    else
                    {
                        Widgets.MouseAttachedLabel("no concealment here", 0f, 0f,
                            new UnityEngine.Color(1f, 0.35f, 0.35f));
                    }
                });
        }
    }

    // Stashed things do not exist for raider theft-scanning.
    public static class StealExclusionPatch
    {
        public static void TryInstall(HarmonyLib.Harmony harmony)
        {
            try
            {
                var m = HarmonyLib.AccessTools.Method(typeof(StealAIUtility), "TryFindBestItemToSteal");
                if (m == null) { Log.Warning("[Colonist Awareness] TryFindBestItemToSteal not found; stash theft-exclusion off"); return; }
                harmony.Patch(m, postfix: new HarmonyLib.HarmonyMethod(typeof(StealExclusionPatch), "Postfix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] stash theft-exclusion install failed: " + e.Message);
            }
        }

        public static void Postfix(ref bool __result, ref Thing item)
        {
            try
            {
                if (!__result || item == null) return;
                if (HiddenThingsComponent.IsStashed(item)) { item = null; __result = false; }
            }
            catch { }
        }
    }

    // A stash decays slower than things left in the open. How much slower depends on
    // what's concealing it and what the ground and climate are doing to it - a cache
    // under dense cover on frozen tundra keeps; the same cache in a hot marsh barely does.
    // Seam: the innermost FinalDeteriorationRate overload - both the deterioration tick
    // (TryDoDeteriorate) and the inspect readout flow through it.
    public static class StashPreservationPatch
    {
        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(SteadyEnvironmentEffects), "FinalDeteriorationRate",
                    new[] { typeof(Thing), typeof(bool), typeof(bool), typeof(TerrainDef), typeof(List<string>) });
                if (m == null) { Log.Warning("[Colonist Awareness] FinalDeteriorationRate not found; stash preservation off"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(StashPreservationPatch), "Postfix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] stash preservation install failed: " + e.Message);
            }
        }

        public static void Postfix(Thing t, ref float __result)
        {
            try
            {
                if (__result <= 0f || t == null || !HiddenThingsComponent.IsStashed(t)) return;
                var map = t.Map;
                IntVec3 cell;
                if (map == null || !HiddenThingsComponent.CellOf(t, out cell)) return;
                int q = HiddenThingsComponent.QualityOf(t);
                float pres = HiddenRegistry.PreservationAt(map, cell);
                float factor = UnityEngine.Mathf.Clamp(1f - 0.14f * q * pres, 0.15f, 1f);
                __result *= factor;
            }
            catch { }
        }
    }

    // Hidden pawns are not valid threats to enemy target acquisition.
    [HarmonyPatch(typeof(Pawn), "ThreatDisabled")]
    public static class Patch_HiddenThreatDisabled
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result) return;
            var s = AwarenessMod.Settings;
            if (s == null || !s.survivalResponses) return;
            if (HiddenRegistry.IsHidden(__instance)) __result = true;
        }
    }
}
