using System;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Creation wealth and construction era summarize persisted operational
    // programs. They do not create those programs or their capabilities.
    internal static class CASettlementWealth
    {
        internal static void Derive(CASettlementProgram settlementProgram,
            int accessInfrastructure, int serviceInfrastructure,
            int civicInfrastructure, TechLevel knowledge,
            out int wealth, out int constructionEra)
        {
            int tier = CASettlementAxes.Tier(knowledge);
            int materialPrograms = settlementProgram?.entries?.Count(entry =>
                entry != null && entry.blocker.NullOrEmpty()
                && entry.requiresMaterial
                && !entry.materialSource.NullOrEmpty()) ?? 0;
            int stockedPrograms = settlementProgram?.entries?.Count(entry =>
                entry != null && entry.blocker.NullOrEmpty()
                && !entry.stockSource.NullOrEmpty()) ?? 0;
            wealth = Math.Max(0, Math.Min(3,
                materialPrograms >= 8 ? 3
                : materialPrograms >= 4 || stockedPrograms >= 2 ? 2
                : materialPrograms >= 2 ? 1 : 0));
            constructionEra = materialPrograms > 0 ? tier
                : Math.Max(0, tier - 1);
        }

        internal static string WealthWords(int wealth)
        {
            return wealth <= 0 ? "poor" : wealth == 1 ? "modest"
                : wealth == 2 ? "prosperous" : "rich";
        }

        internal static string TierWords(int tier)
        {
            return tier <= 0 ? "tribal" : tier == 1 ? "medieval"
                : "industrial";
        }
    }
}
