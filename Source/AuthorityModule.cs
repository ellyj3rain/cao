using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Relayed group orders use the giver's authority, the follower's
    // disposition, and their opinion of the giver. Direct player orders comply.
    public enum Obedience { Complies, Reluctant, Refuses }

    public static class Authority
    {
        // Why is this person obeyed? Assigned squad command ranks first, assigned
        // fire-team command next, then generic standing, all shaded by opinion.
        public static float CommandStanding(Pawn giver, Pawn follower)
        {
            if (giver == null || follower == null) return 0f;
            float baseStanding = 0.4f;
            if (SquadComponent.HasExplicitCommandAuthorityOver(giver,
                follower.thingIDNumber))
                baseStanding = SquadComponent.IsLeader(giver) ? 0.8f : 0.65f;
            float opinion = 0f;
            if (follower.relations != null)
                opinion = follower.relations.OpinionOf(giver) / 200f; // -0.5 .. +0.5
            return UnityEngine.Mathf.Clamp01(baseStanding + opinion);
        }

        public static Obedience Check(Pawn giver, Pawn follower, out string reason)
        {
            reason = null;
            var s = AwarenessMod.Settings;
            if (s == null || !s.authorityObedience || giver == null || follower == null
                || giver == follower)
                return Obedience.Complies;

            float standing = CommandStanding(giver, follower);
            var d = Disposition.Of(follower);
            float score = standing * 0.55f + d.conformity * 0.25f
                + d.discipline * 0.20f;

            // Band tuning: the DEFAULT pawn under a peer relayer scores 0.445 and must
            // COMPLY cleanly - reluctance and refusal are playable events reserved for
            // genuinely low conformity/discipline or real dislike, not ambient noise.
            if (score >= 0.44f) return Obedience.Complies;

            int opinion = follower.relations != null ? follower.relations.OpinionOf(giver) : 0;
            if (opinion <= -15) reason = "thinks little of " + giver.LabelShort;
            else if (standing < 0.45f)
                reason = "sees no authority in " + giver.LabelShort;
            else reason = "won't be told";

            return score >= 0.34f ? Obedience.Reluctant : Obedience.Refuses;
        }

        // Filter a relayed group order through the obedience gate. Refusals drop out
        // with a message; reluctant compliers stay in but say so. The relayer
        // themselves is never filtered - the player's hand is on them directly.
        public static List<Pawn> FilterObedient(Pawn relayer, List<Pawn> team, string orderLabel)
        {
            var result = new List<Pawn>();
            for (int i = 0; i < team.Count; i++)
            {
                var member = team[i];
                if (member == relayer)
                {
                    result.Add(member);
                    CATrace.Pawn(member, "OPERATOR-DIRECT " + orderLabel
                        + " - selected actor complies; no relay or obedience gate");
                    continue;
                }
                CommandRouteReceipt routeReceipt;
                if (!CommsModule.CanRelayOrder(relayer, member, team,
                    out routeReceipt))
                {
                    Messages.Message(member.LabelShort + " does not receive the "
                        + orderLabel + ": " + routeReceipt.Detail + ".",
                        member, MessageTypeDefOf.NeutralEvent, false);
                    CATrace.Pawn(member, "DELIVERY FAILED " + orderLabel + " from "
                        + relayer.LabelShort + " [" + routeReceipt.Failure + "] - "
                        + routeReceipt.Detail + "; OBEDIENCE NOT EVALUATED");
                    continue;
                }
                CATrace.Pawn(member, "DELIVERED " + orderLabel + " from "
                    + relayer.LabelShort + " via "
                    + routeReceipt.Channel.ToString().ToLowerInvariant() + " - "
                    + routeReceipt.Detail + "; evaluating obedience separately");
                string reason;
                var verdict = Check(relayer, member, out reason);
                if (verdict == Obedience.Refuses)
                {
                    Messages.Message(member.LabelShort + " refuses the " + orderLabel + ": "
                        + reason + ".", member, MessageTypeDefOf.NegativeEvent, false);
                    CATrace.Pawn(member, "OBEDIENCE REFUSED " + orderLabel + " from " + relayer.LabelShort
                        + " - " + reason + " (command standing "
                        + CommandStanding(relayer, member).ToString("F2") + ")");
                    continue;
                }
                if (verdict == Obedience.Reluctant)
                {
                    Messages.Message(member.LabelShort + " complies reluctantly ("
                        + reason + ").", member, MessageTypeDefOf.SilentInput, false);
                    CATrace.Pawn(member, "OBEDIENCE RELUCTANT " + orderLabel + " from " + relayer.LabelShort
                        + " - " + reason + " (command standing "
                        + CommandStanding(relayer, member).ToString("F2") + ")");
                }
                else
                {
                    CATrace.Pawn(member, "OBEDIENCE COMPLIES for " + orderLabel
                        + " from " + relayer.LabelShort + " after confirmed delivery"
                        + " (command standing "
                        + CommandStanding(relayer, member).ToString("F2") + ")");
                }
                result.Add(member);
            }
            return result;
        }
    }
}
