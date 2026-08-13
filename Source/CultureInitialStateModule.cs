using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // This boundary creates only Culture identity. Substantive inherited
    // meanings and practices require authored or observed historical evidence;
    // faction counts, tech levels, hostility and trade flags are not history.
    internal static class CACultureInitialState
    {
        internal static bool EnsureForFaction(CACulture culture,
            Faction faction)
        {
            if (culture == null || faction?.def == null) return false;
            return EnsureIdentity(culture, faction.Name,
                "world faction " + faction.loadID);
        }

        internal static bool EnsureForRegional(CACulture culture,
            CARegionalPlan plan, CARegionalFactionPlan faction)
        {
            if (culture == null || faction == null) return false;
            if (faction.ResolvedFactionDef == null) return false;
            return EnsureIdentity(culture,
                CARegionalPlanUtility.FactionName(faction),
                "starting-region faction " + faction.key);
        }

        private static bool EnsureIdentity(CACulture culture,
            string populationName, string owner)
        {
            CACultureModel.Normalize(culture);
            string beforeId = culture.id;
            int beforeConstituents = culture.constituents.Count;
            if (culture.name.NullOrEmpty())
                culture.name = (populationName.NullOrEmpty()
                    ? "Unnamed faction" : populationName) + " culture";
            CACultureModel.EnsureIdentity(culture,
                owner + ":culture-t0");
            if (culture.constituents.Count == 1
                && culture.constituents[0] != null)
            {
                culture.constituents[0].cultureId = culture.id;
                culture.constituents[0].label = culture.name;
                culture.constituents[0].share = 100;
            }
            if (culture.temporalBasis.NullOrEmpty())
                culture.temporalBasis = "No inherited meanings or practices "
                    + "are asserted without authored or observed history.";
            return beforeId != culture.id
                || beforeConstituents != culture.constituents.Count;
        }
    }
}
