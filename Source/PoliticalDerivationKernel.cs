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
        public bool Ambiguous;
        public Dictionary<string, int> Scores =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public List<string> Evidence = new List<string>();
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

    // NPC positions are an inference from explicit, discriminating evidence.
    // A tie is unresolved social meaning, not permission for a seed to choose
    // a belief. An axis with no unique evidenced answer remains unset.
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
                receipt.Ambiguous = tied.Count > 1;
                if (!receipt.Ambiguous)
                    receipt.SelectedOptionKey = tied[0];
                result.Axes.Add(receipt);
            }
            return result;
        }

    }
}
