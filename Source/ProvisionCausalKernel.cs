using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // Pure authority for provision institutions. Runtime authoring and the
    // governed fixture translator provide the same social facts and receive
    // the same arrangements; serialization is only a representation of this
    // result, never a second source of truth.
    public sealed class CAProvisionPopulationFact
    {
        public int Key;
        public bool OtherFaction;
        public string Ownership;
    }

    public sealed class CAProvisionCausalFacts
    {
        public string Support;
        public string Ownership;
        public string Work;
        public int Pattern;
        public int Authority;
        public int Role;
        public int Scale;
        public int Access;
        public int Services;
        public int Civic;
        public List<CAProvisionPopulationFact> Population =
            new List<CAProvisionPopulationFact>();
    }

    public sealed class CAProvisionCausalSpec
    {
        public int Key;
        public string BasisKey;
        public string BasisLabel;
        public string Operator;
        public int PopulationGroupKey = -1;
        public string Access;
        public string Funding;
        public string Distribution;
        public int Nodes = 1;
        public string Reach = "Settlement";
    }

    public static class CAProvisionCausalKernel
    {
        public static List<CAProvisionCausalSpec> Derive(
            CAProvisionCausalFacts facts)
        {
            var result = new List<CAProvisionCausalSpec>();
            if (facts == null) return result;
            int neighborhoodNodes = facts.Scale >= 5 ? 3
                : facts.Scale >= 4 ? 2
                : facts.Scale >= 3 && facts.Role == 1 ? 2 : 1;
            if (facts.Services >= 2 || facts.Civic >= 2)
                neighborhoodNodes = Math.Max(2, neighborhoodNodes);
            bool independent = facts.Authority == 3 || facts.Authority == 4;
            bool locallyFundedReserve = (facts.Pattern == 2
                    || facts.Pattern == 3) && independent;
            bool common = facts.Ownership == "common"
                || facts.Ownership == "cooperative";
            int next = 1;

            void Add(string basis, string label, string op, string access,
                string funding, string distribution, int nodes,
                string reach, int populationGroupKey = -1,
                int? fixedKey = null)
            {
                result.Add(new CAProvisionCausalSpec
                {
                    Key = fixedKey ?? next++, BasisKey = basis,
                    BasisLabel = label, Operator = op,
                    PopulationGroupKey = populationGroupKey,
                    Access = access, Funding = funding,
                    Distribution = distribution,
                    Nodes = Math.Max(1, nodes), Reach = reach
                });
            }

            string authorityReach = facts.Role == 1 && facts.Access >= 2
                ? "Region" : "Settlement";
            if (facts.Support == "public")
                Add("authority:1", "Faction reserve", "Authority",
                    "Universal", "Taxation", "Neighborhood",
                    neighborhoodNodes, authorityReach);
            else if (facts.Support == "charitable")
            {
                Add("communal:1", "Communal kitchens", "Communal",
                    "Charitable", "SharedWork",
                    "Centralized", 1, "Settlement");
                Add("authority:1", "Faction reserve", "Authority",
                    "Universal", locallyFundedReserve
                        ? "SharedWork" : "Taxation",
                    "Centralized", 1, authorityReach);
            }
            else if (facts.Support == "communal"
                || facts.Support == null && common)
            {
                Add("communal:1", "Communal kitchens", "Communal",
                    "Universal", "SharedWork",
                    "Neighborhood", neighborhoodNodes, "Settlement");
                Add("authority:1", "Faction reserve", "Authority",
                    "Universal", locallyFundedReserve
                        ? "SharedWork" : "Taxation",
                    "Centralized", 1, authorityReach);
            }
            else if (facts.Support == "private")
                Add("household:1", "Household provision", "Household",
                    "Members", "Household", "Household", 1, "Household");
            else
            {
                Add("household:1", "Household provision", "Household",
                    "Members", "Household", "Household", 1, "Household");
                if (facts.Support == "mixed")
                    Add("communal:1", "Communal kitchens", "Communal",
                        "Members", "SharedWork", "Neighborhood",
                        neighborhoodNodes, "Settlement");
                Add("authority:1", "Faction reserve", "Authority",
                    "Universal", locallyFundedReserve
                        ? "SharedWork" : "Taxation",
                    "Centralized", 1, authorityReach);
            }

            foreach (CAProvisionPopulationFact population in
                (facts.Population ?? new List<CAProvisionPopulationFact>())
                    .Where(item => item != null && item.OtherFaction)
                    .OrderBy(item => item.Key))
            {
                if (string.IsNullOrEmpty(population.Ownership)
                    || population.Ownership == facts.Ownership) continue;
                bool shared = population.Ownership == "common"
                    || population.Ownership == "cooperative";
                Add("populationGroup:" + population.Key,
                    shared ? "Communal kitchens" : "Household provision",
                    shared ? "Communal" : "Household", "Members",
                    shared ? "SharedWork" : "Household", "Neighborhood",
                    neighborhoodNodes, "Settlement", population.Key,
                    1000 + population.Key);
            }
            return result;
        }

        public static string Signature(IEnumerable<CAProvisionCausalSpec> specs)
        {
            return string.Join("|", (specs ?? Enumerable.Empty<
                    CAProvisionCausalSpec>()).Select(item => string.Join(":",
                    item.Key, item.BasisKey, item.Operator,
                    item.PopulationGroupKey, item.Access, item.Funding,
                    item.Distribution, item.Nodes, item.Reach)));
        }
    }
}
