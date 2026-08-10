using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // One named competence result for ranged contact reconstruction and squad
    // reorganization. Knowledge decides which facts exist; Authority decides
    // whether a commitment is permitted; this result changes only latency and
    // the breadth of one bounded physical stage.
    internal readonly struct CARangedCoordinationCompetence
    {
        public readonly int Shooting;
        public readonly int Intellectual;
        public readonly float TacticalSynthesis;
        public readonly int DecisionDelayTicks;
        public readonly float SearchRadius;
        public readonly float ReorganizationApproachLimit;

        public CARangedCoordinationCompetence(int shooting, int intellectual,
            float tacticalSynthesis, int decisionDelayTicks,
            float searchRadius, float reorganizationApproachLimit)
        {
            Shooting = shooting;
            Intellectual = intellectual;
            TacticalSynthesis = tacticalSynthesis;
            DecisionDelayTicks = decisionDelayTicks;
            SearchRadius = searchRadius;
            ReorganizationApproachLimit = reorganizationApproachLimit;
        }

        public string TraceText()
        {
            return "competence Shooting " + Shooting + ", Intellectual "
                + Intellectual + ", tactical synthesis "
                + TacticalSynthesis.ToString("F2") + ", decision delay "
                + DecisionDelayTicks + " ticks, search radius "
                + SearchRadius.ToString("F1") + " cells, bounded approach "
                + ReorganizationApproachLimit.ToString("F1") + " cells";
        }
    }

    internal static class CACompetence
    {
        internal static CARangedCoordinationCompetence RangedCoordination(
            Pawn pawn)
        {
            int shooting = EffectiveSkill(pawn, SkillDefOf.Shooting);
            int intellectual = EffectiveSkill(pawn, SkillDefOf.Intellectual);
            float synthesis = Mathf.Clamp01((shooting * 0.55f
                + intellectual * 0.45f) / 20f);
            int delay = Mathf.RoundToInt(Mathf.Lerp(180f, 60f, synthesis));
            float searchRadius = Mathf.Clamp(5f + shooting * 0.30f
                + intellectual * 0.15f, 5f, 12f);
            float approach = Mathf.Clamp(2.5f + shooting * 0.06f
                + intellectual * 0.07f, 2.5f, 4.5f);
            return new CARangedCoordinationCompetence(shooting,
                intellectual, synthesis, delay, searchRadius, approach);
        }

        private static int EffectiveSkill(Pawn pawn, SkillDef skill)
        {
            if (pawn?.skills == null || skill == null) return 0;
            SkillRecord record = pawn.skills.GetSkill(skill);
            return record == null || record.TotallyDisabled ? 0 : record.Level;
        }
    }
}
