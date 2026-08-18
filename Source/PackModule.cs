using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Module 8: rucks. Worn packs add carrying capacity; how much of a pack a pawn can
    // actually use depends on body size, melee (strength proxy), and traits.
    public static class RuckRegistry
    {
        public static readonly Dictionary<string, float> Capacity = new Dictionary<string, float>
        {
            { "CA_RuckSmall", 45f },
            { "CA_RuckMedium", 75f },
            { "CA_RuckLarge", 100f },
            { "CA_RuckXXL", 150f },
        };

        public static float WornCapacity(Pawn p)
        {
            if (p == null || p.apparel == null) return 0f;
            float total = 0f;
            var worn = p.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                float cap;
                if (Capacity.TryGetValue(worn[i].def.defName, out cap)) total += cap;
            }
            return total;
        }

        // Utilization: 1.0 for a standard capable adult; scaled by build, strength, nerve.
        public static float Utilization(Pawn p)
        {
            if (p == null) return 0f;
            float u = 0.5f + 0.5f * UnityEngine.Mathf.Clamp(p.BodySize, 0.5f, 1.5f);
            if (p.skills != null) u += 0.015f * p.skills.GetSkill(SkillDefOf.Melee).Level;
            var traits = p.story != null ? p.story.traits : null;
            if (traits != null)
            {
                var tough = DefDatabase<TraitDef>.GetNamedSilentFail("Tough");
                if (tough != null && traits.HasTrait(tough)) u += 0.10f;
                var wimp = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
                if (wimp != null && traits.HasTrait(wimp)) u -= 0.20f;
                var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
                if (nerves != null && traits.DegreeOfTrait(nerves) < 0) u -= 0.10f;
            }
            float w = SustenanceComponent.WeightOf(p);
            if (w >= 0.85f) u -= 0.25f;
            else if (w >= 0.65f) u -= 0.10f;
            else if (w <= 0.15f) u -= 0.25f;
            else if (w <= 0.35f) u -= 0.10f;
            return UnityEngine.Mathf.Clamp(u, 0.4f, 1.3f);
        }
    }

    public class StatPart_RuckCapacity : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            var p = req.Thing as Pawn;
            if (p == null) return;
            float cap = RuckRegistry.WornCapacity(p);
            if (cap <= 0f) return;
            val += cap * RuckRegistry.Utilization(p);
        }

        public override string ExplanationPart(StatRequest req)
        {
            var p = req.Thing as Pawn;
            if (p == null) return null;
            float cap = RuckRegistry.WornCapacity(p);
            if (cap <= 0f) return null;
            float u = RuckRegistry.Utilization(p);
            return "Ruck: +" + cap + " x " + u.ToStringPercent() + " utilization = +" + (cap * u).ToString("F0");
        }
    }

    [StaticConstructorOnStartup]
    public static class RuckStatInjector
    {
        static RuckStatInjector()
        {
            var stat = StatDefOf.CarryingCapacity;
            if (stat.parts == null) stat.parts = new List<StatPart>();
            var part = new StatPart_RuckCapacity();
            part.parentStat = stat;
            stat.parts.Add(part);
        }
    }
}
