using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;

namespace ColonistAwareness
{
    // Native doctrine is evidence and pressure, never a replacement for the
    // Culture distribution. Only exact precept or meme semantics map here.
    internal static class CACultureIdeoligionAdapter
    {
        internal static float Pressure(Ideo ideo, string questionKey,
            out string source)
        {
            source = null;
            if (ideo == null || questionKey == null) return 0f;
            var precepts = new HashSet<string>((ideo.PreceptsListForReading
                    ?? new List<Precept>()).Where(value => value?.def != null)
                .Select(value => value.def.defName), StringComparer.Ordinal);
            var memes = new HashSet<string>((ideo.memes
                    ?? new List<MemeDef>()).Where(value => value != null)
                .Select(value => value.defName), StringComparer.Ordinal);

            if (questionKey == CACultureQuestionRegistry
                    .GenderDistribution)
            {
                if (memes.Contains("MaleSupremacy"))
                    return Result(-0.90f, "meme:MaleSupremacy", out source);
                if (memes.Contains("FemaleSupremacy"))
                    return Result(0.90f, "meme:FemaleSupremacy", out source);
            }
            else if (questionKey == CACultureQuestionRegistry
                    .PluralityAcceptance)
            {
                int male = SpouseCount(precepts, "Male");
                int female = SpouseCount(precepts, "Female");
                // Generic plurality remains sex-neutral. One asymmetric
                // spouse-count rule is retained by Ideoligion but does not
                // become a population-wide Culture position.
                if (male > 1 && female > 1)
                    return Result(Math.Min(male, female) >= 5 ? 0.90f
                        : 0.30f + Math.Min(male, female) * 0.12f,
                        "precepts:SpouseCount_Male+Female", out source);
                if (male == 1 && female == 1)
                    return Result(-0.70f,
                        "precepts:SpouseCount_Male+Female_MaxOne",
                        out source);
            }
            else if (questionKey == CACultureQuestionRegistry
                    .CoercionLegitimacy)
            {
                if (precepts.Contains("Slavery_Abhorrent"))
                    return Result(-0.90f, "precept:Slavery_Abhorrent",
                        out source);
                if (precepts.Contains("Slavery_Horrible"))
                    return Result(-0.65f, "precept:Slavery_Horrible",
                        out source);
                if (precepts.Contains("Slavery_Disapproved"))
                    return Result(-0.35f, "precept:Slavery_Disapproved",
                        out source);
                if (precepts.Contains("Slavery_Acceptable"))
                    return Result(0.35f, "precept:Slavery_Acceptable",
                        out source);
                if (precepts.Contains("Slavery_Honorable"))
                    return Result(0.90f, "precept:Slavery_Honorable",
                        out source);
            }
            else if (questionKey == CACultureQuestionRegistry
                    .CaptiveProtection)
            {
                if (precepts.Contains("Execution_Abhorrent"))
                    return Result(0.90f, "precept:Execution_Abhorrent",
                        out source);
                if (precepts.Contains("Execution_Horrible"))
                    return Result(0.65f, "precept:Execution_Horrible",
                        out source);
                if (precepts.Contains("Execution_HorribleIfInnocent"))
                    return Result(0.35f,
                        "precept:Execution_HorribleIfInnocent", out source);
                if (precepts.Contains("Execution_DontCare"))
                    return Result(-0.35f, "precept:Execution_DontCare",
                        out source);
                if (precepts.Contains("Execution_RespectedIfGuilty"))
                    return Result(-0.55f,
                        "precept:Execution_RespectedIfGuilty", out source);
                if (precepts.Contains("Execution_Required"))
                    return Result(-0.90f, "precept:Execution_Required",
                        out source);
            }
            else if (questionKey == CACultureQuestionRegistry
                    .MutualProvision)
            {
                if (precepts.Contains("Charity_Essential"))
                    return Result(0.90f, "precept:Charity_Essential",
                        out source);
                if (precepts.Contains("Charity_Important"))
                    return Result(0.65f, "precept:Charity_Important",
                        out source);
                if (precepts.Contains("Charity_Worthwhile"))
                    return Result(0.35f, "precept:Charity_Worthwhile",
                        out source);
            }
            else if (questionKey == CACultureQuestionRegistry
                    .NoveltyAcceptance)
            {
                if (precepts.Contains("Research_None"))
                    return Result(-0.90f, "precept:Research_None",
                        out source);
                if (precepts.Contains("Research_ExtremelySlow"))
                    return Result(-0.65f, "precept:Research_ExtremelySlow",
                        out source);
                if (precepts.Contains("Research_VerySlow")
                    || precepts.Contains("Research_Slow"))
                    return Result(-0.35f, "precept:Research_Slow",
                        out source);
                if (precepts.Contains("Research_Fast"))
                    return Result(0.35f, "precept:Research_Fast",
                        out source);
                if (precepts.Contains("Research_VeryFast"))
                    return Result(0.70f, "precept:Research_VeryFast",
                        out source);
            }
            return 0f;
        }

        private static int SpouseCount(HashSet<string> precepts,
            string gender)
        {
            string prefix = "SpouseCount_" + gender + "_";
            string value = precepts.FirstOrDefault(item =>
                item.StartsWith(prefix, StringComparison.Ordinal));
            if (value == null) return 1;
            if (value.EndsWith("Unlimited", StringComparison.Ordinal))
                return 5;
            if (value.EndsWith("MaxFour", StringComparison.Ordinal)) return 4;
            if (value.EndsWith("MaxThree", StringComparison.Ordinal)) return 3;
            if (value.EndsWith("MaxTwo", StringComparison.Ordinal)) return 2;
            return 1;
        }

        private static float Result(float value, string evidence,
            out string source)
        {
            source = evidence;
            return value;
        }
    }
}
