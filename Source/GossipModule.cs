using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Module 13: gossip. A tells B about C. Whether B's view of C actually moves depends on
    // A's silver tongue, B's trust of A, and B's own judgment and firsthand history with C.
    // Poison overheard by its subject backfires hard. All of it lands as visible social
    // memories - manipulation leaves a paper trail.
    public static class GossipUtility
    {
        public static bool HoldsPosition(Pawn c)
        {
            if (SquadComponent.IsLeader(c) || SquadComponent.IsFireteamLeader(c)) return true;
            try { if (c.Ideo != null && c.Ideo.GetRole(c) != null) return true; } catch { }
            return false;
        }

        private static bool HasTraitNamed(Pawn p, string name)
        {
            var traits = p.story != null ? p.story.traits : null;
            if (traits == null) return false;
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(name);
            return def != null && traits.HasTrait(def);
        }

        // Poison needs a MOTIVE: political envy toward a seat-holder, a genuine grievance,
        // or a personality that treats cruelty as recreation. No arbitrary malice.
        public static Pawn PickSubject(Pawn initiator, Pawn recipient, bool wantDisliked)
        {
            var map = initiator.Map;
            if (map == null) return null;
            var colonists = map.mapPawns.FreeColonistsSpawned;

            if (!wantDisliked)
            {
                Pawn bestFriend = null;
                int bestOp = int.MinValue;
                for (int i = 0; i < colonists.Count; i++)
                {
                    var c = colonists[i];
                    if (c == initiator || c == recipient) continue;
                    int op = initiator.relations.OpinionOf(c);
                    if (HoldsPosition(c) && op >= 10) op += 10; // loyalty politics: praise the leaders you back
                    if (op > bestOp) { bestOp = op; bestFriend = c; }
                }
                return bestOp >= 5 ? bestFriend : null;
            }

            bool envious = HasTraitNamed(initiator, "Jealous") || HasTraitNamed(initiator, "Greedy");
            // The game's single Psychopath label covers the whole colloquial spectrum:
            // purposeful (instrumental seat-clearing, prosocial when seated) with erratic
            // capacity retained - purposeful first, never merely arbitrary.
            bool impulsiveCruel = HasTraitNamed(initiator, "Abrasive") || HasTraitNamed(initiator, "Bloodlust")
                || HasTraitNamed(initiator, "Psychopath");
            bool instrumental = HasTraitNamed(initiator, "Psychopath") && !HoldsPosition(initiator);

            Pawn bestTarget = null;
            float bestScore = 0f;
            for (int i = 0; i < colonists.Count; i++)
            {
                var c = colonists[i];
                if (c == initiator || c == recipient) continue;
                int op = initiator.relations.OpinionOf(c);
                float score = 0f;
                if (envious && HoldsPosition(c) && op < 15) score = 30f - op;        // political envy: unseat them
                if (instrumental && HoldsPosition(c)) score = UnityEngine.Mathf.Max(score, 28f); // cold calculation, opinion irrelevant
                if (op <= -15) score = UnityEngine.Mathf.Max(score, -op);            // grievance: rivalry and revenge
                if (impulsiveCruel && op < 0) score = UnityEngine.Mathf.Max(score, 10f - op); // recreation, per their values
                if (score > bestScore) { bestScore = score; bestTarget = c; }
            }
            return bestTarget;
        }

        // Did the gossip land? Silver tongue vs trust vs skepticism.
        public static bool Lands(Pawn initiator, Pawn recipient)
        {
            float p = 0.35f;
            if (initiator.skills != null) p += 0.04f * initiator.skills.GetSkill(SkillDefOf.Social).Level;
            p += 0.003f * recipient.relations.OpinionOf(initiator);
            if (recipient.skills != null) p -= 0.025f * recipient.skills.GetSkill(SkillDefOf.Intellectual).Level;
            return Rand.Value < UnityEngine.Mathf.Clamp(p, 0.05f, 0.9f);
        }

        public static void Gossip(Pawn initiator, Pawn recipient, bool poison)
        {
            var subject = PickSubject(initiator, recipient, poison);
            if (subject == null) return;

            // Poison within the subject's earshot is a gamble.
            if (poison && subject.Spawned
                && subject.Position.InHorDistOf(initiator.Position, 9f))
            {
                var overheard = DefDatabase<ThoughtDef>.GetNamedSilentFail("CA_OverheardPoison");
                if (overheard != null && subject.needs != null && subject.needs.mood != null)
                    subject.needs.mood.thoughts.memories.TryGainMemory(overheard, initiator);
                return;
            }

            if (!Lands(initiator, recipient)) return;
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(poison ? "CA_HeardPoison" : "CA_HeardPraise");
            if (def != null && recipient.needs != null && recipient.needs.mood != null)
                recipient.needs.mood.thoughts.memories.TryGainMemory(def, subject);
        }

        public static float Weight(Pawn initiator, Pawn recipient, bool poison)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.gossip) return 0f;
            if (!initiator.IsColonist || !recipient.IsColonist) return 0f;

            if (!poison)
            {
                float w = 0.35f;
                if (initiator.skills != null) w += 0.02f * initiator.skills.GetSkill(SkillDefOf.Social).Level;
                if (HasTraitNamed(initiator, "Kind")) w += 0.3f;
                // A seated psychopath ingratiates: prosocial because it pays.
                if (HasTraitNamed(initiator, "Psychopath") && HoldsPosition(initiator)) w += 0.25f;
                return w;
            }

            // Poison only fires when a motive exists - and never from the Kind.
            if (HasTraitNamed(initiator, "Kind")) return 0f;
            if (PickSubject(initiator, recipient, wantDisliked: true) == null) return 0f;
            float pw = 0.25f;
            if (initiator.skills != null) pw += 0.02f * initiator.skills.GetSkill(SkillDefOf.Social).Level;
            if (HasTraitNamed(initiator, "Jealous") || HasTraitNamed(initiator, "Greedy")) pw += 0.2f;
            if (HasTraitNamed(initiator, "Abrasive")) pw += 0.2f;
            if (HasTraitNamed(initiator, "Bloodlust")) pw += 0.15f;
            if (HasTraitNamed(initiator, "Psychopath")) pw += HoldsPosition(initiator) ? 0.05f : 0.2f;
            return pw;
        }
    }

    public class InteractionWorker_Praise : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            return GossipUtility.Weight(initiator, recipient, poison: false);
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks,
            out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null; letterLabel = null; letterDef = null; lookTargets = null;
            GossipUtility.Gossip(initiator, recipient, poison: false);
        }
    }

    public class InteractionWorker_Poison : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            return GossipUtility.Weight(initiator, recipient, poison: true);
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks,
            out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null; letterLabel = null; letterDef = null; lookTargets = null;
            GossipUtility.Gossip(initiator, recipient, poison: true);
        }
    }
}
