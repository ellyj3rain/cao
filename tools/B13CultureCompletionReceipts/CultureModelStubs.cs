using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // The receipt executable links the production pure kernels without the
    // RimWorld UI assembly. This authoring-harness subset carries only the
    // fields exercised by those kernels; production-model authority is checked
    // separately against the built assembly and campaign preflight reader.
    public sealed class CACulture
    {
        public const int QuestionStateField = 4;
        public const int DiversityField = 8;

        public int questionRegistryVersion =
            CACultureQuestionRegistry.CurrentVersion;
        public int withinGroupSpread = 2;
        public int authoredMask;
        public List<CACultureQuestionDistribution> inheritedQuestions =
            new List<CACultureQuestionDistribution>();
        public List<CACultureQuestionDistribution> localQuestions =
            new List<CACultureQuestionDistribution>();
    }

    internal static class CACultureModel
    {
        internal static void Normalize(CACulture culture)
        {
            if (culture == null) return;
            culture.inheritedQuestions ??=
                new List<CACultureQuestionDistribution>();
            culture.localQuestions ??=
                new List<CACultureQuestionDistribution>();
            culture.inheritedQuestions.RemoveAll(value => value == null);
            culture.localQuestions.RemoveAll(value => value == null);
            culture.withinGroupSpread = Math.Max(0,
                Math.Min(4, culture.withinGroupSpread));
            culture.questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            foreach (CACultureQuestionDistribution question in culture
                .inheritedQuestions.Concat(culture.localQuestions))
            {
                question.populationScope ??= "*";
                question.subgroups ??=
                    new List<CACultureSubgroupDistribution>();
                if (!question.spreadOverride)
                    question.spread = CACultureAuthoringKernel.SpreadFor(
                        culture.withinGroupSpread);
            }
        }
    }
}
