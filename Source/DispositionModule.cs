using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Pillar 1 - Disposition. Who this person IS, as parameters: every threshold in
    // the mod reads this vector instead of a scattered constant, so two colonists in
    // the same situation behave like two different people. Values are 0..1 centered
    // on 0.5, computed from traits, skills, and current mood/pain - one legible
    // place, no hidden knobs. The autonomy dial stays the player's control surface;
    // disposition is texture WITHIN what the tier permits, never over the player's
    // hand.
    public struct DispositionProfile
    {
        public float courage;     // holds under fire, resists freezing and fleeing
        public float discipline;  // follows through, waits, keeps to the order
        public float aggression;  // closes in, springs early, shoots first
        public float empathy;     // spares the downed, the young, the animal
        public float conformity;  // accepts direction from legitimate authority
        public float initiative;  // self-starts within the permitted (consumers later)
        public float skepticism;  // doubts what they're told (knowledge trust, later)
    }

    public static class Disposition
    {
        private struct Cached
        {
            public DispositionProfile profile;
            public int tick;
        }

        private static readonly Dictionary<int, Cached> cache = new Dictionary<int, Cached>();
        private const int CacheTicks = 600; // mood drifts slowly; recompute ~4x/hour

        public static DispositionProfile Of(Pawn p)
        {
            if (p == null) return Default();
            Cached c;
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(p.thingIDNumber, out c) && now - c.tick < CacheTicks)
                return c.profile;
            var profile = Compute(p);
            cache[p.thingIDNumber] = new Cached { profile = profile, tick = now };
            return profile;
        }

        public static void ClearCache()
        {
            cache.Clear();
        }

        internal static void Invalidate(Pawn p)
        {
            if (p != null) cache.Remove(p.thingIDNumber);
        }

        private static DispositionProfile Default()
        {
            return new DispositionProfile
            {
                courage = 0.5f, discipline = 0.5f, aggression = 0.5f,
                empathy = 0.5f, conformity = 0.5f, initiative = 0.5f, skepticism = 0.5f
            };
        }

        private static DispositionProfile Compute(Pawn p)
        {
            CACulturalCognitionWorldComponent cognition =
                CACulturalCognitionWorldComponent.Current;
            if (cognition != null)
                return cognition.DispositionFor(p);

            var d = Default();

            // Skills: competence is confidence.
            if (p.skills != null)
            {
                int melee = p.skills.GetSkill(SkillDefOf.Melee).Level;
                int shoot = p.skills.GetSkill(SkillDefOf.Shooting).Level;
                int intel = p.skills.GetSkill(SkillDefOf.Intellectual).Level;
                d.courage += 0.012f * UnityEngine.Mathf.Max(melee, shoot);   // up to +0.24
                d.skepticism += 0.015f * intel;                              // up to +0.30
            }

            // Traits (vanilla defNames, verified against Core TraitDefs).
            var traits = p.story != null ? p.story.traits : null;
            if (traits != null)
            {
                int nerves = DegreeOf(traits, "Nerves");           // 2 iron-willed .. -2 volatile
                d.courage += 0.10f * nerves;
                d.discipline += 0.08f * nerves;

                int mood = DegreeOf(traits, "NaturalMood");        // 2 sanguine .. -2 depressive
                d.courage += 0.04f * mood;

                int industry = DegreeOf(traits, "Industriousness"); // 2 industrious .. -2 slothful
                d.discipline += 0.06f * industry;
                d.initiative += 0.08f * industry;

                if (Has(traits, "Wimp")) { d.courage -= 0.30f; }
                if (Has(traits, "Tough")) { d.courage += 0.15f; }
                if (Has(traits, "Brawler")) { d.aggression += 0.20f; }
                if (Has(traits, "Bloodlust")) { d.aggression += 0.30f; d.empathy -= 0.35f; }
                if (Has(traits, "Psychopath")) { d.empathy -= 0.40f; d.courage += 0.05f; }
                if (Has(traits, "Kind")) { d.empathy += 0.30f; }
                if (Has(traits, "Abrasive")) { d.conformity -= 0.12f; }
                if (Has(traits, "Neurotic")) { d.discipline += 0.05f; d.courage -= 0.08f; }
                if (Has(traits, "TooSmart")) { d.skepticism += 0.15f; d.conformity -= 0.08f; }
            }

            // The current person, not just the permanent one: mood and pain.
            if (p.needs != null && p.needs.mood != null)
            {
                float mood = p.needs.mood.CurLevelPercentage; // 0..1
                d.courage += (mood - 0.5f) * 0.25f;
                d.discipline += (mood - 0.5f) * 0.15f;
            }
            if (p.health != null && p.health.hediffSet.PainTotal > 0.2f)
                d.courage -= p.health.hediffSet.PainTotal * 0.25f;

            d.courage = UnityEngine.Mathf.Clamp01(d.courage);
            d.discipline = UnityEngine.Mathf.Clamp01(d.discipline);
            d.aggression = UnityEngine.Mathf.Clamp01(d.aggression);
            d.empathy = UnityEngine.Mathf.Clamp01(d.empathy);
            d.conformity = UnityEngine.Mathf.Clamp01(d.conformity);
            d.initiative = UnityEngine.Mathf.Clamp01(d.initiative);
            d.skepticism = UnityEngine.Mathf.Clamp01(d.skepticism);
            return d;
        }

        private static bool Has(TraitSet traits, string defName)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            return def != null && traits.HasTrait(def);
        }

        private static int DegreeOf(TraitSet traits, string defName)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (def == null || !traits.HasTrait(def)) return 0;
            var t = traits.GetTrait(def);
            return t != null ? t.Degree : 0;
        }
    }
}
