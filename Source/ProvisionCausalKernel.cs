using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // Explicit facts supplied by the owning settlement surface. The kernel
    // validates and serializes them; it never translates Culture, Political
    // Beliefs, broad tendencies, scale, or infrastructure summaries into an
    // institution.
    public sealed class CAProvisionOperatorFact
    {
        public int Key;
        public string BasisKey;
        public string BasisLabel;
        public string Operator;
        public string OperatorIdentity;
        public string OperatorSource;
        public int PopulationGroupKey = -1;
        public string Access;
        public string AccessSource;
        public string Funding;
        public string FundingSource;
        public string Distribution;
        public string DistributionSource;
        public string LaborSource;
        public string KnowledgeSource;
        public string MaterialSource;
        public string StockSource;
        public string PolicyKey;
        public string PolicyValue;
        public string CollectionPath;
        public int Nodes = 1;
        public string Reach = "Settlement";
    }

    public sealed class CAProvisionCausalFacts
    {
        public List<CAProvisionOperatorFact> Operators =
            new List<CAProvisionOperatorFact>();
    }

    public sealed class CAProvisionCausalSpec
    {
        public int Key;
        public string BasisKey;
        public string BasisLabel;
        public string Operator;
        public string OperatorIdentity;
        public string OperatorSource;
        public int PopulationGroupKey = -1;
        public string Access;
        public string AccessSource;
        public string Funding;
        public string FundingSource;
        public string Distribution;
        public string DistributionSource;
        public string LaborSource;
        public string KnowledgeSource;
        public string MaterialSource;
        public string StockSource;
        public string PolicyKey;
        public string PolicyValue;
        public string CollectionPath;
        public int Nodes = 1;
        public string Reach = "Settlement";
    }

    public static class CAProvisionCausalKernel
    {
        public static List<CAProvisionCausalSpec> Derive(
            CAProvisionCausalFacts facts)
        {
            var result = new List<CAProvisionCausalSpec>();
            foreach (CAProvisionOperatorFact fact in facts?.Operators
                ?? new List<CAProvisionOperatorFact>())
            {
                if (!Complete(fact)) continue;
                result.Add(new CAProvisionCausalSpec
                {
                    Key = fact.Key,
                    BasisKey = fact.BasisKey,
                    BasisLabel = fact.BasisLabel,
                    Operator = fact.Operator,
                    OperatorIdentity = fact.OperatorIdentity,
                    OperatorSource = fact.OperatorSource,
                    PopulationGroupKey = fact.PopulationGroupKey,
                    Access = fact.Access,
                    AccessSource = fact.AccessSource,
                    Funding = fact.Funding,
                    FundingSource = fact.FundingSource,
                    Distribution = fact.Distribution,
                    DistributionSource = fact.DistributionSource,
                    LaborSource = fact.LaborSource,
                    KnowledgeSource = fact.KnowledgeSource,
                    MaterialSource = fact.MaterialSource,
                    StockSource = fact.StockSource,
                    PolicyKey = fact.PolicyKey,
                    PolicyValue = fact.PolicyValue,
                    CollectionPath = fact.CollectionPath,
                    Nodes = Math.Max(1, fact.Nodes),
                    Reach = fact.Reach
                });
            }
            return result.OrderBy(item => item.Key).ToList();
        }

        public static bool Complete(CAProvisionOperatorFact fact)
        {
            if (fact == null || fact.Key <= 0
                || string.IsNullOrEmpty(fact.BasisKey)
                || string.IsNullOrEmpty(fact.Operator)
                || string.IsNullOrEmpty(fact.OperatorIdentity)
                || string.IsNullOrEmpty(fact.OperatorSource)
                || string.IsNullOrEmpty(fact.Access)
                || string.IsNullOrEmpty(fact.AccessSource)
                || string.IsNullOrEmpty(fact.Funding)
                || string.IsNullOrEmpty(fact.FundingSource)
                || string.IsNullOrEmpty(fact.Distribution)
                || string.IsNullOrEmpty(fact.DistributionSource)
                || string.IsNullOrEmpty(fact.LaborSource)
                || string.IsNullOrEmpty(fact.KnowledgeSource)
                || string.IsNullOrEmpty(fact.MaterialSource)
                || string.IsNullOrEmpty(fact.StockSource)
                || string.IsNullOrEmpty(fact.Reach)) return false;
            if (!DirectSource(fact.OperatorSource)
                || !DirectSource(fact.AccessSource)
                || !DirectSource(fact.FundingSource)
                || !DirectSource(fact.DistributionSource)
                || !DirectSource(fact.LaborSource)
                || !DirectSource(fact.KnowledgeSource)
                || !DirectSource(fact.MaterialSource)
                || !DirectSource(fact.StockSource)) return false;
            if (fact.Operator != "Authority"
                && fact.Operator != "Communal"
                && fact.Operator != "DomesticUnit"
                && fact.Operator != "Individual") return false;
            bool domestic = fact.Operator == "DomesticUnit"
                || fact.Operator == "Individual";
            if (domestic && fact.Funding != "Domestic") return false;
            if (!domestic && fact.Funding == "Domestic") return false;
            if (fact.Funding == "Taxation"
                && (string.IsNullOrEmpty(fact.PolicyKey)
                    || string.IsNullOrEmpty(fact.PolicyValue)
                    || string.IsNullOrEmpty(fact.CollectionPath)))
                return false;
            return true;
        }

        private static bool DirectSource(string value)
        {
            return value?.StartsWith("authored:",
                       StringComparison.Ordinal) == true
                || value?.StartsWith("observed:",
                       StringComparison.Ordinal) == true;
        }

        public static string Signature(
            IEnumerable<CAProvisionCausalSpec> specs)
        {
            return string.Join("|", (specs ?? Enumerable.Empty<
                    CAProvisionCausalSpec>()).Select(item => string.Join(":",
                    item.Key, item.BasisKey, item.Operator,
                    item.OperatorIdentity, item.OperatorSource,
                    item.PopulationGroupKey, item.Access, item.AccessSource,
                    item.Funding,
                    item.FundingSource, item.Distribution,
                    item.DistributionSource, item.LaborSource,
                    item.KnowledgeSource, item.MaterialSource,
                    item.StockSource, item.PolicyKey, item.CollectionPath,
                    item.PolicyValue,
                    item.Nodes, item.Reach)));
        }
    }
}
