using System.Globalization;
using System.Xml.Linq;
using ColonistAwareness;

// XML-side mirror of CARegionalSettlements.RealizationSourceHash. Fixture
// converters and receipts share this one implementation so an offline schema
// conversion cannot stamp current schema around a stale realization receipt.
internal static class CARegionalFixtureContracts
{
    internal static int RealizationSourceHash(XElement plan)
    {
        int hash = CAWorldTendencyCausalKernel.StableStringHash(
            StringValue(plan, "candidateId", "ca"));
        XElement policy = plan.Element("worldPolicy");
        hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
            (int)MathF.Round(FloatValue(policy, "urbanGrowthPropensity",
                0.45f) * 10000f));
        hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
            (int)MathF.Round(FloatValue(policy, "frontierHoldingFrequency",
                0.45f) * 10000f));
        hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
            (int)MathF.Round(FloatValue(policy, "frontierHoldingSize",
                0.50f) * 10000f));
        XElement[] relations = plan.Element("relations")?.Elements("li")
            .OrderBy(item => IntValue(item, "leftFactionKey", 0))
            .ThenBy(item => IntValue(item, "rightFactionKey", 0)).ToArray()
            ?? Array.Empty<XElement>();
        if (relations.Any(item => !BoolValue(item, "authorRelation", true)))
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                (int)MathF.Round(FloatValue(policy, "regionalConflictChance",
                    0.4f) * 10000f));
        foreach (XElement relation in relations)
        {
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                IntValue(relation, "leftFactionKey", 0),
                IntValue(relation, "rightFactionKey", 0),
                RelationValue(StringValue(relation, "relation", "Neutral")));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                BoolValue(relation, "authorRelation", true) ? 1 : 0);
        }
        foreach (XElement faction in plan.Element("factions")?.Elements("li")
            .OrderBy(item => IntValue(item, "key", 0))
            ?? Enumerable.Empty<XElement>())
        {
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                IntValue(faction, "key", 0),
                FactionSourceValue(StringValue(faction, "source",
                    "ExistingWorldFaction")),
                IntValue(faction, "existingFactionLoadId", -1));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                BoolValue(faction, "institutionalStateIncomplete", false)
                    ? 1 : 0);
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                CAWorldTendencyCausalKernel.StableStringHash(
                    StringValue(faction, "customFactionDefName", "none")));
            HashAxes(ref hash, faction.Element("politicalBeliefs")
                ?.Element("positions"));
            HashAxes(ref hash, faction.Element("factionStructure"));
        }
        foreach (XElement settlement in plan.Element("settlements")
            ?.Elements("li").OrderBy(item => IntValue(item, "slot", -1))
            ?? Enumerable.Empty<XElement>())
        {
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                IntValue(settlement, "slot", -1),
                IntValue(settlement, "memberTileId", -1),
                IntValue(settlement, "factionKey", 0));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                SettlementOriginValue(StringValue(settlement,
                    "populationOrigin", "Unset")),
                IntValue(settlement, "reallocatedFromTileId", -1),
                IntValue(settlement, "operationalRoleMask", 0));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                IntValue(settlement, "authoredForm", -1), 0, 0);
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                BoolValue(settlement, "hasRoadAccess", false) ? 1 : 0,
                BoolValue(settlement, "hasRiverAccess", false) ? 1 : 0,
                BoolValue(settlement, "hasCoastalAccess", false) ? 1 : 0);
        }
        return hash;
    }

    internal static void StampRealizationSourceHash(XElement plan)
    {
        XElement receipt = plan.Element("settlementRealizationSourceHash");
        string value = RealizationSourceHash(plan).ToString(
            CultureInfo.InvariantCulture);
        if (receipt == null)
            plan.Add(new XElement("settlementRealizationSourceHash", value));
        else receipt.Value = value;
    }

    private static void HashAxes(ref int hash, XElement parent)
    {
        foreach (XElement axis in parent?.Elements("li").OrderBy(item =>
            StringValue(item, "axis", "none"))
            ?? Enumerable.Empty<XElement>())
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                CAWorldTendencyCausalKernel.StableStringHash(
                    StringValue(axis, "axis", "none")),
                CAWorldTendencyCausalKernel.StableStringHash(
                    StringValue(axis, "option", "none")),
                IntValue(axis, "source", 0));
    }

    private static int IntValue(XElement parent, string name, int fallback) =>
        int.TryParse((string)parent?.Element(name), out int value)
            ? value : fallback;

    private static float FloatValue(XElement parent, string name,
        float fallback) => float.TryParse((string)parent?.Element(name),
            NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value : fallback;

    private static bool BoolValue(XElement parent, string name,
        bool fallback) => bool.TryParse((string)parent?.Element(name),
            out bool value) ? value : fallback;

    private static string StringValue(XElement parent, string name,
        string fallback)
    {
        string value = (string)parent?.Element(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static int RelationValue(string value) =>
        value == "Hostile" ? 0 : value == "Ally" ? 2
            : int.TryParse(value, out int parsed) ? parsed : 1;

    private static int FactionSourceValue(string value) =>
        value == "NewWorldFaction" ? 1
            : int.TryParse(value, out int parsed) ? parsed : 0;

    private static int SettlementOriginValue(string value) =>
        value == "ReallocatedFromWorldPool" ? 1
            : value == "ScenarioOverride" ? 2
            : int.TryParse(value, out int parsed) ? parsed : 0;
}
