using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    public sealed class CAPoliticalDerivationContext
    {
        public Dictionary<string, Dictionary<string, int>> AxisScores =
            new Dictionary<string, Dictionary<string, int>>(
                StringComparer.Ordinal);
        public List<string> Evidence = new List<string>();

        public bool Score(string axisKey, string optionKey, int amount,
            string evidence)
        {
            if (string.IsNullOrWhiteSpace(axisKey)
                || string.IsNullOrWhiteSpace(optionKey)
                || amount == 0 || string.IsNullOrWhiteSpace(evidence))
                return false;
            Dictionary<string, int> options;
            if (!AxisScores.TryGetValue(axisKey, out options))
            {
                options = new Dictionary<string, int>(StringComparer.Ordinal);
                AxisScores[axisKey] = options;
            }
            int prior;
            options.TryGetValue(optionKey, out prior);
            options[optionKey] = prior + amount;
            if (!string.IsNullOrWhiteSpace(evidence))
                Evidence.Add(axisKey + ":" + optionKey + ":" + amount + ":"
                    + evidence);
            return true;
        }
    }

    public sealed class CAPoliticalAxisDerivation
    {
        public string AxisKey;
        public string SelectedOptionKey;
        public bool TieBroken;
        public Dictionary<string, int> Scores =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public List<string> Evidence = new List<string>();
    }

    public sealed class CAPoliticalEvidenceFacts
    {
        public string Source;
        public bool MarketExchangeObserved;
        public bool OrganizedWorkObserved;
        public bool ProfessionalDefenseObserved;
        public bool LevyDefenseObserved;
        public bool OpenRecruitment;
        public bool HereditaryStatus;
        public bool NoQuarterPolicyObserved;
    }

    // Common causal vocabulary used by both world and Starting Region
    // adapters. Only an affirmative, discriminating fact contributes a score;
    // absence never becomes a political claim. No seed contributes an answer.
    public static class CAPoliticalEvidenceContext
    {
        public static CAPoliticalDerivationContext FromFacts(
            CAPoliticalEvidenceFacts facts)
        {
            facts = facts ?? new CAPoliticalEvidenceFacts();
            string source = string.IsNullOrWhiteSpace(facts.Source)
                ? "faction evidence" : facts.Source;
            string Evidence(string fact) => source + ": " + fact;
            var context = new CAPoliticalDerivationContext();
            if (facts.MarketExchangeObserved)
                context.Score("economy", "market", 9,
                    Evidence("market exchange is established practice"));
            if (facts.OrganizedWorkObserved)
                context.Score("work", "organized", 8,
                    Evidence("work is assigned through organized groups"));
            if (facts.ProfessionalDefenseObserved)
                context.Score("defense", "professional", 10,
                    Evidence("professional soldiers are established"));
            else if (facts.LevyDefenseObserved)
                context.Score("defense", "levy", 8,
                    Evidence("general war service is established"));
            if (facts.OpenRecruitment)
                context.Score("membership", "open", 8,
                    Evidence("the faction accepts rescuees as members"));
            if (facts.HereditaryStatus)
                context.Score("status", "hereditary", 8,
                    Evidence("the faction carries inheritable title structure"));
            if (facts.NoQuarterPolicyObserved)
                context.Score("warConduct", "strength", 8,
                    Evidence("recorded policy denies protection after defeat"));
            return context;
        }
    }

    public sealed class CAPoliticalDerivationResult
    {
        public List<CAPoliticalAxisDerivation> Axes =
            new List<CAPoliticalAxisDerivation>();
    }

    // The owning product model supplies this contract from its canonical
    // question definitions. The pure derivation kernel deliberately owns no
    // second political-axis catalog.
    public sealed class CAPoliticalAxisContract
    {
        public string AxisKey;
        public List<string> OptionKeys = new List<string>();
    }

    // NPC positions are a reproducible inference from explicit evidence. The
    // stable hash resolves exact ties between positively evidenced options. An
    // axis with no valid differentiating evidence remains unset.
    public static class CAPoliticalDerivationKernel
    {
        public static CAPoliticalDerivationResult Derive(string seed,
            CAPoliticalDerivationContext context,
            IEnumerable<CAPoliticalAxisContract> axisContracts)
        {
            context = context ?? new CAPoliticalDerivationContext();
            var result = new CAPoliticalDerivationResult();
            foreach (CAPoliticalAxisContract axis in (axisContracts
                ?? Enumerable.Empty<CAPoliticalAxisContract>()).Where(value =>
                    value != null && !string.IsNullOrWhiteSpace(value.AxisKey)
                    && value.OptionKeys != null && value.OptionKeys.Count > 0))
            {
                var receipt = new CAPoliticalAxisDerivation
                {
                    AxisKey = axis.AxisKey,
                    Evidence = context.Evidence.Where(value =>
                        value.StartsWith(axis.AxisKey + ":",
                            StringComparison.Ordinal)).ToList()
                };
                Dictionary<string, int> supplied;
                context.AxisScores.TryGetValue(axis.AxisKey, out supplied);
                foreach (string option in axis.OptionKeys.Distinct(
                    StringComparer.Ordinal))
                {
                    int score = 0;
                    supplied?.TryGetValue(option, out score);
                    receipt.Scores[option] = score;
                }
                if (supplied == null || !axis.OptionKeys.Any(value =>
                        supplied.ContainsKey(value)))
                {
                    result.Axes.Add(receipt);
                    continue;
                }
                int best = receipt.Scores.Values.Max();
                if (best <= 0)
                {
                    result.Axes.Add(receipt);
                    continue;
                }
                List<string> tied = axis.OptionKeys.Where(value =>
                    receipt.Scores[value] == best).ToList();
                receipt.TieBroken = tied.Count > 1;
                receipt.SelectedOptionKey = tied.OrderBy(value =>
                    StableHash((seed ?? "ca-politics") + "|" + axis.AxisKey
                        + "|" + value)).ThenBy(value => value,
                            StringComparer.Ordinal).First();
                result.Axes.Add(receipt);
            }
            return result;
        }

        public static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }
    }
}
