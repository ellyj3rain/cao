using System.Collections.Generic;
using System.Text;
using Verse;

namespace ColonistAwareness
{
    // Concise diagnostic trace for decisions and refusals. The session-local combat
    // flight recorder separately captures native job lifecycle and whole-pawn state
    // during active threats, so a playtest can be reconstructed without stretching
    // these prose receipts beyond what they actually establish. Neither surface judges
    // whether the result looks or plays correctly; that remains operator evidence.
    // Both are on by default through this one explicit opt-out.
    public static class CATrace
    {
        private const int SkipRepeatTicks = 600;
        private static readonly Dictionary<string, int> lastSkipTicks =
            new Dictionary<string, int>();
        private static Game skipGame;

        public static bool On
        {
            get
            {
                if (CAPassivePlayMatrix.SuppressTrace) return false;
                var s = AwarenessMod.Settings;
                return s != null && s.traceBehavior;
            }
        }

        internal static void ClearTransient()
        {
            lastSkipTicks.Clear();
            skipGame = null;
        }

        public static void Log(string msg)
        {
            if (!On) return;
            CACombatFlightRecorder.NoteSystemTrace(msg);
            Verse.Log.Message(Prefix(null, null, null, null, null, null) + " " + msg);
        }

        // Log a behavior that DIDN'T fire and the reason - the expensive-to-guess case.
        public static void Skip(Pawn p, string behavior, string reason,
            Thing target = null, IntVec3? contact = null,
            IntVec3? destination = null, IntVec3? anchor = null,
            CAIntentContext? intent = null)
        {
            if (!On) return;
            int tick = Current.Game != null ? Find.TickManager.TicksGame : -1;
            if (tick >= 0)
            {
                if (skipGame != Current.Game)
                {
                    lastSkipTicks.Clear();
                    skipGame = Current.Game;
                }
                string key = (p != null ? p.ThingID : "?") + "\u001f"
                    + (behavior ?? "?") + "\u001f" + (reason ?? "?");
                int lastTick;
                if (lastSkipTicks.TryGetValue(key, out lastTick)
                    && tick >= lastTick && tick - lastTick < SkipRepeatTicks)
                    return;
                if (lastSkipTicks.Count >= 4096) lastSkipTicks.Clear();
                lastSkipTicks[key] = tick;
            }
            CACombatFlightRecorder.NoteBehaviorTrace(p, "skipped",
                behavior, reason, target, contact, destination, anchor,
                intent);
            Verse.Log.Message(Prefix(p, target, contact, destination, anchor, intent)
                + " " + (p != null ? p.LabelShort : "?")
                + ": " + behavior + " SKIPPED - " + reason);
        }

        public static void Pawn(Pawn p, string msg, Thing target = null,
            IntVec3? contact = null, IntVec3? destination = null,
            IntVec3? anchor = null, CAIntentContext? intent = null)
        {
            if (!On) return;
            CACombatFlightRecorder.NoteBehaviorTrace(p, "fired", null,
                msg, target, contact, destination, anchor, intent);
            Verse.Log.Message(Prefix(p, target, contact, destination, anchor, intent)
                + " " + (p != null ? p.LabelShort : "?") + ": " + msg);
        }

        private static string Prefix(Pawn actor, Thing target,
            IntVec3? contact, IntVec3? destination, IntVec3? anchor,
            CAIntentContext? intent)
        {
            var text = new StringBuilder("[CA]");
            int tick = Current.Game != null ? Find.TickManager.TicksGame : -1;
            if (tick >= 0) text.Append("[tick=").Append(tick).Append(']');

            Map map = actor != null && actor.Spawned ? actor.Map
                : target != null && target.Spawned ? target.Map : null;
            if (map != null) text.Append("[map=").Append(map.uniqueID).Append(']');
            if (actor != null)
            {
                text.Append("[actor=").Append(actor.ThingID);
                if (actor.Spawned) text.Append('@').Append(Coord(actor.Position));
                text.Append(']');
            }
            if (target != null)
            {
                text.Append("[target=").Append(target.ThingID);
                if (target.Spawned) text.Append('@').Append(Coord(target.Position));
                text.Append(']');
            }
            if (contact.HasValue && contact.Value.IsValid)
                text.Append("[contact=").Append(Coord(contact.Value)).Append(']');
            if (destination.HasValue && destination.Value.IsValid)
                text.Append("[destination=").Append(Coord(destination.Value)).Append(']');
            if (anchor.HasValue && anchor.Value.IsValid)
                text.Append("[anchor=").Append(Coord(anchor.Value)).Append(']');
            if (intent.HasValue && intent.Value.IsValid)
            {
                CAIntentContext value = intent.Value;
                text.Append("[origin=").Append(Origin(value.Origin)).Append(']')
                    .Append("[controller=").Append(Controller(value.Controller)).Append(']')
                    .Append("[episode=").Append(value.EpisodeId).Append(']')
                    .Append("[behavior=").Append(value.BehaviorKey)
                    .Append(']')
                    .Append("[authority=").Append(value.AuthorityOrigin)
                    .Append(':').Append(value.AuthorityIdentity).Append(']');
                if (value.IssuerId >= 0)
                    text.Append("[issuer=").Append(value.IssuerId).Append(']');
            }
            return text.ToString();
        }

        private static string Origin(CAIntentOrigin origin)
        {
            switch (origin)
            {
                case CAIntentOrigin.OperatorDirect: return "operator-direct";
                case CAIntentOrigin.OperatorRelay: return "operator-relay";
                case CAIntentOrigin.Autonomous: return "autonomous";
                case CAIntentOrigin.AutomaticDefense: return "automatic-defense";
                case CAIntentOrigin.Continuation: return "continuation";
                case CAIntentOrigin.SaveRestore: return "save-restore";
                case CAIntentOrigin.PeerRelay: return "peer-relay";
                case CAIntentOrigin.PlayerDelegated: return "player-delegated";
                case CAIntentOrigin.NativeDuty: return "native-duty";
                case CAIntentOrigin.Institutional: return "institutional";
                case CAIntentOrigin.Household: return "household";
                case CAIntentOrigin.Organization: return "organization";
                case CAIntentOrigin.WorldAuthoring: return "world-authoring";
                default: return "unknown";
            }
        }

        private static string Controller(CAIntentController controller)
        {
            switch (controller)
            {
                case CAIntentController.Ambush: return "ambush";
                case CAIntentController.Hide: return "hide";
                case CAIntentController.Hold: return "hold";
                case CAIntentController.Formation: return "formation";
                case CAIntentController.Withdrawal: return "withdrawal";
                case CAIntentController.SelfPreservation: return "self-preservation";
                case CAIntentController.FirePosition: return "fire-position";
                case CAIntentController.ExplosiveEvasion: return "explosive-evasion";
                case CAIntentController.RaidDefense: return "raid-defense";
                case CAIntentController.FoodSafety: return "food-safety";
                case CAIntentController.Drill: return "drill";
                case CAIntentController.Shelter: return "shelter";
                case CAIntentController.CombatRecovery: return "combat-recovery";
                case CAIntentController.DraftCoordination: return "draft-coordination";
                case CAIntentController.AcousticInvestigation:
                    return "acoustic-investigation";
                case CAIntentController.Welfare: return "welfare";
                default: return "unknown";
            }
        }

        private static string Coord(IntVec3 cell)
        {
            return "(" + cell.x + "," + cell.z + ")";
        }
    }
}
