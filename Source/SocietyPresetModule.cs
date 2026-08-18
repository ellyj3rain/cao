using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // Society presets are authoring inputs, never world-owned state. Built-in
    // recipes and saved snapshots implement this same contract so every
    // interaction reaches one atomic Culture + Political Order +
    // Technological Knowledge application.
    internal abstract class CASocietyPreset
    {
        internal abstract string Key { get; }
        internal abstract string Label { get; }
        internal abstract string CatalogGroup { get; }
        internal abstract string ReferenceRegion { get; }
        internal abstract string ApproximatePeriod { get; }
        internal abstract string Summary { get; }
        internal abstract string CultureSummary { get; }
        internal abstract string TechnologySummary { get; }
        internal abstract bool Saved { get; }

        internal abstract string ValidationFailure();
        internal abstract void ApplyComponents(CACulture culture,
            CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge,
            string sourceIdentity);
        internal abstract bool Matches(CACulture culture,
            CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge);
        internal abstract CAPoliticalBeliefs PoliticalPreview();
        internal abstract CATechnologicalKnowledge TechnologyPreview();
    }

    // A built-in society preset owns its identity and catalog metadata. Its
    // Culture reference and complete Political Order recipe are components,
    // not aliases for that identity.
    internal sealed class CASocietyPresetDef : CASocietyPreset
    {
        internal readonly string PresetKey;
        internal readonly string PresetLabel;
        internal readonly string PresetCatalogGroup;
        internal readonly string PresetReferenceRegion;
        internal readonly string PresetApproximatePeriod;
        internal readonly string PresetSummary;
        internal readonly string CulturePresetKey;
        // Recorded only as construction provenance. The Society recipe owns
        // the materialized complete composition below and never consults the
        // component-preset catalog when it is applied.
        internal readonly string PoliticalOrderPresetKey;
        internal readonly CASocietyPoliticalOverride[] PoliticalOverrides;
        internal readonly CAPoliticalBeliefs PoliticalOrderValues;
        internal readonly string TechnologyProfileKey;
        internal readonly CATechnologicalKnowledge TechnologicalKnowledgeValues;

        internal CASocietyPresetDef(string key, string label,
            string catalogGroup, string referenceRegion,
            string approximatePeriod, string summary,
            string culturePresetKey, string politicalOrderPresetKey,
            params CASocietyPoliticalOverride[] politicalOverrides)
        {
            PresetKey = key;
            PresetLabel = label;
            PresetCatalogGroup = catalogGroup;
            PresetReferenceRegion = referenceRegion;
            PresetApproximatePeriod = approximatePeriod;
            PresetSummary = summary;
            CulturePresetKey = culturePresetKey;
            PoliticalOrderPresetKey = politicalOrderPresetKey;
            PoliticalOverrides = politicalOverrides
                ?? Array.Empty<CASocietyPoliticalOverride>();
            PoliticalOrderValues = BuildPoliticalOrder(
                PoliticalOrderPresetKey, PoliticalOverrides);
            TechnologyProfileKey = TechnologyProfileFor(PresetKey);
            TechnologicalKnowledgeValues = BuildTechnologicalKnowledge(
                TechnologyProfileKey, PresetKey);
        }

        internal CACulturePresetDef Culture =>
            CACulturePresetLibrary.Find(CulturePresetKey);

        internal override string Key => PresetKey;
        internal override string Label => PresetLabel;
        internal override string CatalogGroup => PresetCatalogGroup;
        internal override string ReferenceRegion => PresetReferenceRegion;
        internal override string ApproximatePeriod =>
            PresetApproximatePeriod;
        internal override string Summary => PresetSummary;
        internal override string CultureSummary => Culture?.Summary;
        internal override string TechnologySummary =>
            CATechnologicalKnowledgeModel.Summary(
                TechnologicalKnowledgeValues);
        internal override bool Saved => false;

        internal void ApplyPoliticalOrder(CAPoliticalBeliefs politicalOrder,
            CAAxisSource source)
        {
            if (politicalOrder == null || PoliticalOrderValues == null) return;
            string ownerId = politicalOrder.id;
            politicalOrder.CopyFrom(PoliticalOrderValues);
            politicalOrder.id = ownerId;
            foreach (CAPoliticalQuestionState state in politicalOrder.questions
                ?? new List<CAPoliticalQuestionState>())
                foreach (CAPoliticalOptionShare option in state?.options
                    ?? new List<CAPoliticalOptionShare>())
                    option.source = (byte)source;
            politicalOrder.nameAuthored = false;
            CAPoliticalOrderModel.Normalize(politicalOrder);
        }

        internal override CAPoliticalBeliefs PoliticalPreview()
        {
            return PoliticalOrderValues?.Copy() ?? new CAPoliticalBeliefs();
        }

        internal override CATechnologicalKnowledge TechnologyPreview()
        {
            return TechnologicalKnowledgeValues?.CopyAsPreset()
                ?? new CATechnologicalKnowledge();
        }

        internal override string ValidationFailure()
        {
            if (string.IsNullOrWhiteSpace(PresetKey)
                || string.IsNullOrWhiteSpace(PresetLabel)
                || string.IsNullOrWhiteSpace(PresetCatalogGroup)
                || string.IsNullOrWhiteSpace(PresetSummary))
                return "society preset identity or metadata is incomplete";
            if (PresetCatalogGroup == "Historical societies"
                && (string.IsNullOrWhiteSpace(PresetReferenceRegion)
                    || string.IsNullOrWhiteSpace(PresetApproximatePeriod)))
                return "historical society metadata is incomplete";
            if (Culture == null)
                return CulturePresetKey + " is not a Culture preset";
            if (string.IsNullOrWhiteSpace(PoliticalOrderPresetKey))
                return "political construction provenance is incomplete";
            string cultureFailure = Culture.ValidationFailure();
            if (!string.IsNullOrWhiteSpace(cultureFailure))
                return "Culture: " + cultureFailure;
            string overrideFailure = PoliticalOverrides
                .Select(value => value == null
                    ? "a political override is missing"
                    : value.ValidationFailure())
                .FirstOrDefault(value => value != null);
            if (overrideFailure != null) return overrideFailure;
            string politicalFailure = CAPoliticalOrderModel.ValidationFailure(
                PoliticalPreview());
            if (politicalFailure != null)
                return "Political Order: " + politicalFailure;
            string technologyFailure = CATechnologicalKnowledgeModel
                .ValidationFailure(TechnologyPreview(),
                    requireComplete: false);
            return technologyFailure == null ? null
                : "Technological Knowledge: " + technologyFailure;
        }

        private static CAPoliticalBeliefs BuildPoliticalOrder(string baseKey,
            IEnumerable<CASocietyPoliticalOverride> overrides)
        {
            if (CAPoliticalOrderModel.FindPreset(baseKey) == null) return null;
            var result = new CAPoliticalBeliefs();
            CAPoliticalOrderModel.ApplyPreset(result, baseKey,
                CAAxisSource.Generated);
            foreach (CASocietyPoliticalOverride item in overrides
                ?? Enumerable.Empty<CASocietyPoliticalOverride>())
                item?.Apply(result, CAAxisSource.Generated);
            result.id = null;
            result.nameAuthored = false;
            CAPoliticalOrderModel.Normalize(result);
            return result;
        }

        internal bool MatchesPoliticalOrder(CAPoliticalBeliefs value)
        {
            return CAPoliticalOrderModel.Matches(value, PoliticalPreview());
        }

        internal override void ApplyComponents(CACulture culture,
            CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge,
            string sourceIdentity)
        {
            CACulturePresetLibrary.Apply(culture, Culture,
                sourceIdentity ?? "society:" + Key);
            ApplyPoliticalOrder(politicalOrder, CAAxisSource.Authored);
            if (technologicalKnowledge != null)
            {
                string ownerId = technologicalKnowledge.id;
                technologicalKnowledge.CopyFrom(
                    TechnologicalKnowledgeValues,
                    includeDistribution: false);
                technologicalKnowledge.id = ownerId;
                technologicalKnowledge.origin = CAOrigin.Authored(
                    sourceIdentity ?? "society:" + Key);
                foreach (CATechnologyDomainKnowledge domain in
                    technologicalKnowledge.domains)
                {
                    domain.source = (byte)CAAxisSource.Authored;
                    domain.provenance = "society preset " + Key;
                }
            }
        }

        internal override bool Matches(CACulture culture,
            CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge)
        {
            return CACulturePresetLibrary.Matches(culture, Culture)
                && string.Equals(culture?.name, Culture?.Label,
                    StringComparison.Ordinal)
                && MatchesPoliticalOrder(politicalOrder)
                && CATechnologicalKnowledgeModel.Matches(
                    technologicalKnowledge, TechnologicalKnowledgeValues);
        }

        private static CATechnologicalKnowledge BuildTechnologicalKnowledge(
            string profileKey, string presetKey)
        {
            var result = new CATechnologicalKnowledge();
            CATechnologicalKnowledgeModel.ApplyProfile(result, profileKey,
                CAAxisSource.Generated,
                "built-in society preset " + presetKey);
            result.id = null;
            return result;
        }

        private static string TechnologyProfileFor(string presetKey)
        {
            switch (presetKey)
            {
                case "society-mobile-kin": return "subsistence";
                case "society-ranked-agrarian": return "agrarian";
                case "society-civic-market-town":
                case "society-central-court":
                case "society-english-colonies":
                case "society-first-french-empire":
                case "society-late-tokugawa-japan":
                case "society-late-qing-china":
                case "society-mughal-empire":
                    return "early-modern";
                case "society-frontier-mutual-aid":
                case "society-industrial-civic":
                case "society-civil-war-union":
                case "society-confederate-states":
                case "society-freedpeople-emancipation":
                case "society-second-french-empire":
                case "society-meiji-japan":
                case "society-tanzimat-ottoman-empire":
                    return "industrial";
                case "society-weimar-republic":
                case "society-nazi-germany":
                    return "electrified-industrial";
                case "society-us-postwar":
                case "society-us-millennium":
                case "society-us-contemporary":
                    return "advanced-industrial";
                default: return "agrarian";
            }
        }
    }

    // A recipe override writes ordinary Political Order questions. A single
    // override may address every property question so historical profiles do
    // not need to repeat the same ownership mixture twelve times.
    internal sealed class CASocietyPoliticalOverride
    {
        internal readonly string[] QuestionKeys;
        internal readonly KeyValuePair<string, int>[] Values;

        internal CASocietyPoliticalOverride(IEnumerable<string> questionKeys,
            IEnumerable<KeyValuePair<string, int>> values)
        {
            QuestionKeys = (questionKeys ?? Enumerable.Empty<string>())
                .ToArray();
            Values = (values
                    ?? Enumerable.Empty<KeyValuePair<string, int>>())
                .ToArray();
        }

        internal void Apply(CAPoliticalBeliefs politicalOrder,
            CAAxisSource source)
        {
            foreach (string questionKey in QuestionKeys)
                CAPoliticalOrderModel.SetQuestion(politicalOrder,
                    questionKey, Values, source);
        }

        internal string ValidationFailure()
        {
            if (QuestionKeys.Length == 0) return "an override has no question";
            if (QuestionKeys.Distinct(StringComparer.Ordinal).Count()
                != QuestionKeys.Length)
                return "an override repeats a question";
            if (Values.Length == 0 || Values.Any(item => item.Value <= 0)
                || Values.Sum(item => item.Value) != 100)
                return "an override does not define a complete distribution";
            if (Values.Select(item => item.Key)
                    .Distinct(StringComparer.Ordinal).Count()
                != Values.Length)
                return "an override repeats a position";
            foreach (string questionKey in QuestionKeys)
            {
                CAPoliticalQuestionDef question =
                    CAPoliticalQuestionRegistry.Find(questionKey);
                if (question == null)
                    return questionKey + " is not a Political Order question";
                if (Values.Any(value => !question.Options.Any(option =>
                        option.Key == value.Key)))
                    return questionKey + " contains an unknown position";
                if (!question.Blendable && Values.Length != 1)
                    return questionKey + " does not permit a mixture";
            }
            return null;
        }
    }

    internal static class CASocietyPresetLibrary
    {
        private static readonly CASocietyPresetDef[] Definitions =
        {
            S("society-mobile-kin", "Mobile kin society", "Social forms",
                null, null, "Kin reciprocity, mobile households, and communal decision-making.",
                "mobile-kin-band", "communal-assembly"),
            S("society-ranked-agrarian", "Ranked agrarian society",
                "Social forms", null, null,
                "Landed households, inherited standing, and customary authority.",
                "ranked-agrarian-households", "customary-landed"),
            S("society-civic-market-town", "Civic market town",
                "Social forms", null, null,
                "Negotiated civic office, mixed households, and market exchange.",
                "civic-market-town", "civic-market"),
            S("society-central-court", "Central court society",
                "Social forms", null, null,
                "Concentrated office, formal rank, and administered public order.",
                "central-court-society", "developmental-executive"),
            S("society-frontier-mutual-aid", "Frontier mutual-aid settlement",
                "Social forms", null, null,
                "A new settlement sustained by practical cooperation and shared provision.",
                "frontier-mutual-aid", "communal-assembly"),
            S("society-industrial-civic", "Industrial civic association",
                "Social forms", null, null,
                "Formal civic procedure, specialist work, and institutional provision.",
                "industrial-civic-association", "progressive-civic-mix"),

            S("society-us-postwar", "Postwar United States",
                "Historical societies", "Americas", "1946-1964",
                "Postwar American culture with a civic market Political Order.",
                "united-states-postwar-mid-century", "civic-market"),
            S("society-us-millennium", "Turn-of-the-millennium United States",
                "Historical societies", "Americas", "1995-2005",
                "Late twentieth-century American culture with a civic market Political Order.",
                "united-states-turn-millennium", "civic-market"),
            S("society-us-contemporary", "Contemporary United States",
                "Historical societies", "Americas", "2017-2024",
                "Recent American culture with a mixed progressive civic order.",
                "united-states-contemporary", "progressive-civic-mix"),
            S("society-english-colonies", "English North American colonies",
                "Historical societies", "Americas", "1607-1700",
                "English colonial culture joined to landed customary rule.",
                "english-north-america-early-colonial", "customary-landed",
                Q("civic.status", "caste", 45, "hereditary", 35,
                    "merit", 20),
                Q("civic.membership", "descent", 70, "closed", 20,
                    "vetted", 10),
                Q("economy.work", "household", 35, "coerced", 35,
                    "contract", 20, "duty", 10),
                Q("economy.provision", "household", 70,
                    "voluntary", 20, "public", 10),
                Q("security.conflict", "unrestricted", 55,
                    "quarter", 25, "combatants", 20)),
            S("society-civil-war-union", "Civil War Union",
                "Historical societies", "Americas", "1861-1865",
                "Union public culture and its wartime federal civic order.",
                "united-states-civil-war-union", "civic-market",
                Q("authority.leadership", "executive", 40,
                    "council", 35, "federal", 25),
                Q("authority.decisions", "majority", 40,
                    "directive", 35, "review", 25),
                Q("civic.participation", "citizens", 70,
                    "residents", 20, "standing", 10),
                Q("civic.status", "equal", 70, "merit", 30),
                Q("economy.work", "contract", 45, "duty", 30,
                    "household", 15, "cooperative", 10),
                Q("security.defense", "professional", 45,
                    "militia", 30, "levy", 25)),
            S("society-confederate-states", "Confederate States",
                "Historical societies", "Americas", "1861-1865",
                "The Confederacy's dominant slaveholding culture and governing order.",
                "confederate-slaveholding-dominant-culture", "customary-landed",
                P("private", 85, "public", 10, "common", 5),
                Q("authority.leadership", "executive", 45,
                    "customary", 35, "council", 20),
                Q("authority.decisions", "directive", 55,
                    "custom", 45),
                Q("civic.participation", "citizens", 50,
                    "households", 30, "standing", 20),
                Q("civic.liberty", "orthodox", 100),
                Q("civic.status", "caste", 80, "hereditary", 20),
                Q("civic.membership", "descent", 90, "closed", 10),
                Q("economy.exchange", "market", 80, "regulated", 20),
                Q("economy.credit", "private", 90, "restricted", 10),
                Q("economy.rent", "open", 100),
                Q("economy.work", "coerced", 50, "household", 25,
                    "contract", 15, "duty", 10),
                Q("economy.provision", "household", 75,
                    "voluntary", 15, "public", 10),
                Q("security.conflict", "unrestricted", 75,
                    "quarter", 15, "combatants", 10)),
            S("society-freedpeople-emancipation",
                "Freedpeople during emancipation", "Historical societies",
                "Americas", "1863-1877",
                "Freedpeople building families, institutions, and equal civic standing.",
                "freedpeople-emancipation-communities", "progressive-civic-mix",
                P("private", 30, "cooperative", 30,
                    "public", 15, "common", 25),
                Q("authority.leadership", "assembly", 40,
                    "council", 35, "federal", 25),
                Q("authority.decisions", "majority", 45,
                    "consensus", 35, "review", 20),
                Q("civic.participation", "residents", 55,
                    "citizens", 30, "workers", 15),
                Q("civic.status", "equal", 100),
                Q("civic.membership", "open", 70, "vetted", 30),
                Q("economy.credit", "cooperative", 50,
                    "public", 30, "private", 20),
                Q("economy.rent", "noExtraction", 100),
                Q("economy.work", "cooperative", 35,
                    "contract", 30, "household", 20, "public", 15),
                Q("economy.provision", "common", 40, "public", 35,
                    "voluntary", 15, "household", 10),
                Q("security.local", "watch", 60,
                    "professional", 25, "adhoc", 15)),
            S("society-first-french-empire", "First French Empire",
                "Historical societies", "Europe", "1804-1815",
                "Napoleonic French culture and a centralized developmental order.",
                "france-napoleonic-empire", "developmental-executive",
                P("private", 55, "public", 30,
                    "cooperative", 10, "common", 5),
                Q("authority.leadership", "executive", 75,
                    "council", 15, "customary", 10),
                Q("authority.decisions", "directive", 65,
                    "review", 20, "custom", 15),
                Q("civic.liberty", "restricted", 100),
                Q("civic.status", "merit", 55, "equal", 25,
                    "hereditary", 20),
                Q("economy.work", "contract", 40, "duty", 35,
                    "public", 15, "household", 10),
                Q("security.defense", "professional", 55,
                    "levy", 35, "hereditary", 10)),
            S("society-second-french-empire", "Second French Empire",
                "Historical societies", "Europe", "1852-1870",
                "Second-Empire French culture and plebiscitary developmental rule.",
                "france-second-empire", "developmental-executive",
                P("private", 60, "public", 25,
                    "cooperative", 10, "common", 5),
                Q("authority.leadership", "executive", 70,
                    "council", 20, "assembly", 10),
                Q("authority.decisions", "directive", 50,
                    "review", 30, "majority", 20),
                Q("civic.liberty", "restricted", 100),
                Q("civic.status", "merit", 45, "equal", 35,
                    "hereditary", 20),
                Q("economy.exchange", "market", 50,
                    "regulated", 40, "planned", 10),
                Q("economy.work", "contract", 60, "public", 15,
                    "cooperative", 15, "duty", 10)),
            S("society-weimar-republic", "Weimar Republic",
                "Historical societies", "Europe", "1919-1933",
                "Plural Weimar culture and a constitutional social-market order.",
                "germany-weimar-republic", "progressive-civic-mix",
                P("private", 50, "public", 25,
                    "cooperative", 20, "common", 5),
                Q("authority.leadership", "council", 45,
                    "executive", 35, "federal", 20),
                Q("authority.decisions", "majority", 55,
                    "review", 35, "consensus", 10),
                Q("civic.participation", "residents", 65,
                    "citizens", 35),
                Q("civic.status", "equal", 85, "merit", 15),
                Q("economy.exchange", "regulated", 55,
                    "market", 35, "planned", 10),
                Q("economy.provision", "public", 50,
                    "household", 20, "common", 20, "voluntary", 10)),
            S("society-nazi-germany", "Nazi Germany",
                "Historical societies", "Europe", "1933-1945",
                "The Nazi regime's promoted dominant culture and totalitarian party-state order.",
                "germany-national-socialist-dictatorship", "central-party",
                P("private", 55, "public", 35,
                    "cooperative", 5, "common", 5),
                Q("civic.status", "caste", 75, "hereditary", 25),
                Q("civic.membership", "descent", 85, "closed", 15),
                Q("property.infrastructure", "public", 70,
                    "private", 25, "common", 5),
                Q("property.security", "public", 85,
                    "private", 15),
                Q("property.knowledge", "public", 70,
                    "private", 25, "common", 5),
                Q("economy.exchange", "regulated", 55,
                    "planned", 30, "market", 15),
                Q("economy.credit", "public", 55,
                    "private", 35, "restricted", 10),
                Q("economy.work", "coerced", 35, "duty", 30,
                    "contract", 20, "public", 15),
                Q("security.conflict", "unrestricted", 90,
                    "combatants", 10)),
            S("society-late-tokugawa-japan", "Late Tokugawa Japan",
                "Historical societies", "Asia", "1800-1867",
                "Late Tokugawa culture and hereditary customary rule.",
                "japan-late-tokugawa", "customary-landed",
                Q("civic.status", "caste", 70, "hereditary", 30),
                Q("civic.membership", "descent", 90, "closed", 10),
                Q("economy.work", "household", 50, "duty", 30,
                    "contract", 15, "coerced", 5),
                Q("economy.credit", "private", 45,
                    "restricted", 35, "cooperative", 20),
                Q("security.defense", "hereditary", 55,
                    "levy", 25, "professional", 20)),
            S("society-meiji-japan", "Meiji Japan",
                "Historical societies", "Asia", "1868-1912",
                "Meiji cultural transformation and centralized developmental rule.",
                "japan-meiji-transformation", "developmental-executive",
                P("private", 55, "public", 35,
                    "cooperative", 5, "common", 5),
                Q("civic.liberty", "restricted", 100),
                Q("civic.status", "merit", 50, "equal", 30,
                    "hereditary", 20),
                Q("civic.membership", "descent", 50,
                    "vetted", 40, "open", 10),
                Q("economy.work", "contract", 45, "duty", 30,
                    "public", 20, "household", 5),
                Q("security.defense", "professional", 55,
                    "levy", 40, "hereditary", 5)),
            S("society-late-qing-china", "Late Qing China",
                "Historical societies", "Asia", "1800-1911",
                "Late Qing culture and imperial customary administration.",
                "qing-china-late-imperial", "customary-landed",
                Q("authority.leadership", "customary", 45,
                    "executive", 40, "council", 15),
                Q("civic.status", "hereditary", 40, "caste", 35,
                    "merit", 25),
                Q("civic.membership", "descent", 75,
                    "closed", 15, "vetted", 10),
                Q("economy.work", "household", 50, "duty", 25,
                    "contract", 20, "coerced", 5),
                Q("security.defense", "levy", 40,
                    "professional", 30, "hereditary", 30)),
            S("society-tanzimat-ottoman-empire", "Tanzimat Ottoman Empire",
                "Historical societies", "Asia", "1839-1876",
                "Ottoman reform culture and a centralizing developmental order.",
                "ottoman-empire-tanzimat", "developmental-executive",
                P("private", 50, "public", 30,
                    "common", 15, "cooperative", 5),
                Q("civic.liberty", "restricted", 100),
                Q("civic.status", "merit", 35, "equal", 25,
                    "hereditary", 20, "caste", 20),
                Q("civic.membership", "descent", 45,
                    "vetted", 35, "open", 20),
                Q("economy.exchange", "regulated", 45,
                    "market", 40, "planned", 15),
                Q("economy.provision", "household", 45,
                    "public", 30, "voluntary", 20, "common", 5),
                Q("security.defense", "professional", 50,
                    "levy", 35, "hereditary", 15)),
            S("society-mughal-empire", "Mughal Empire",
                "Historical societies", "Asia", "1556-1605",
                "Akbar-era Mughal court culture and imperial service order.",
                "mughal-india-akbar", "customary-landed",
                P("private", 55, "public", 25,
                    "common", 15, "cooperative", 5),
                Q("authority.leadership", "executive", 55,
                    "customary", 30, "council", 15),
                Q("authority.decisions", "directive", 50,
                    "custom", 35, "review", 15),
                Q("civic.participation", "standing", 55,
                    "households", 30, "citizens", 15),
                Q("civic.status", "hereditary", 40, "merit", 35,
                    "caste", 25),
                Q("civic.membership", "vetted", 45,
                    "descent", 40, "open", 15),
                Q("economy.exchange", "market", 50,
                    "regulated", 40, "planned", 10),
                Q("economy.work", "household", 40,
                    "contract", 30, "duty", 25, "public", 5),
                Q("security.defense", "professional", 45,
                    "hereditary", 35, "levy", 20))
        };

        internal static IReadOnlyList<CASocietyPresetDef> All => Definitions;

        internal static IReadOnlyList<CASocietyPreset> Available
        {
            get
            {
                var result = new List<CASocietyPreset>(Definitions);
                result.AddRange(CAAuthoringProfileLibrary.Societies
                    .Where(item => item != null)
                    .Select(item => (CASocietyPreset)
                        new CAUserSocietyPresetAdapter(item))
                    .Where(item => item.ValidationFailure() == null));
                return result;
            }
        }

        internal static string ValidationFailure()
        {
            if (Definitions.Length == 0)
                return "the built-in society catalog is empty";
            if (Definitions.GroupBy(item => item.Key,
                    StringComparer.Ordinal).Any(group => group.Count() != 1))
                return "society preset keys are duplicated";
            var cultureKeys = new HashSet<string>(CACulturePresetLibrary.All
                .Select(item => item.Key), StringComparer.Ordinal);
            if (Definitions.Any(item => cultureKeys.Contains(item.Key)))
                return "a society preset reuses a Culture preset key";
            foreach (CASocietyPresetDef item in Definitions)
            {
                string failure = item.ValidationFailure();
                if (failure != null) return item.Key + ": " + failure;
            }
            return null;
        }

        internal static bool TryApply(CASocietyPreset preset,
            CACulture culture, CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge,
            string sourceIdentity, out string failure)
        {
            failure = null;
            if (preset == null)
            {
                failure = "No society preset was selected.";
                return false;
            }
            if (culture == null || politicalOrder == null
                || technologicalKnowledge == null)
            {
                failure = "This faction does not have all three Society components to edit.";
                return false;
            }
            failure = preset.ValidationFailure();
            if (!string.IsNullOrWhiteSpace(failure)) return false;

            // Apply into copies first. A preset is one action over the three
            // faction-owned components; all complete records change or none do.
            CACulture cultureCandidate = culture.Copy();
            CAPoliticalBeliefs politicalCandidate = politicalOrder.Copy();
            CATechnologicalKnowledge technologyCandidate =
                technologicalKnowledge.Copy(includeDistribution: false);
            string owner = sourceIdentity ?? "society:" + preset.Key;
            preset.ApplyComponents(cultureCandidate, politicalCandidate,
                technologyCandidate, owner);
            CACultureModel.EnsureIdentity(cultureCandidate, owner);
            CACultureModel.SynchronizeOwnIdentityLabel(cultureCandidate);
            politicalCandidate.id = politicalOrder.id;
            technologyCandidate.id = technologicalKnowledge.id;
            CATechnologicalKnowledgeModel.Ensure(technologyCandidate,
                owner + ":technology");

            failure = CACultureModel.ValidationFailure(cultureCandidate,
                requireSubstantive: true);
            if (!string.IsNullOrWhiteSpace(failure))
            {
                failure = "Culture: " + failure;
                return false;
            }
            failure = CAPoliticalOrderModel.ValidationFailure(
                politicalCandidate);
            if (!string.IsNullOrWhiteSpace(failure))
            {
                failure = "Political Order: " + failure;
                return false;
            }
            failure = CATechnologicalKnowledgeModel.ValidationFailure(
                technologyCandidate);
            if (!string.IsNullOrWhiteSpace(failure))
            {
                failure = "Technological Knowledge: " + failure;
                return false;
            }
            if (!preset.Matches(cultureCandidate, politicalCandidate,
                    technologyCandidate))
            {
                failure = "The selected preset did not survive application.";
                return false;
            }

            CACulture cultureBefore = culture.Copy();
            CAPoliticalBeliefs politicalBefore = politicalOrder.Copy();
            CATechnologicalKnowledge technologyBefore =
                technologicalKnowledge.Copy();
            try
            {
                culture.CopyFrom(cultureCandidate);
                politicalOrder.CopyFrom(politicalCandidate);
                technologicalKnowledge.CopyFrom(technologyCandidate,
                    includeDistribution: false);
                failure = null;
                return true;
            }
            catch (Exception exception)
            {
                // CopyFrom is intentionally deterministic, but this boundary
                // still guarantees that an exceptional commit cannot leave
                // the three faction-owned components in different revisions.
                try { culture.CopyFrom(cultureBefore); }
                catch { }
                try { politicalOrder.CopyFrom(politicalBefore); }
                catch { }
                try { technologicalKnowledge.CopyFrom(technologyBefore); }
                catch { }
                failure = "The society preset could not be committed: "
                    + exception.Message;
                return false;
            }
        }

        internal static CASocietyPreset Match(CACulture culture,
            CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge)
        {
            return Available.FirstOrDefault(item => item.Matches(culture,
                politicalOrder, technologicalKnowledge));
        }

        private static CASocietyPresetDef S(string key, string label,
            string catalogGroup, string referenceRegion,
            string approximatePeriod, string summary,
            string culturePresetKey, string politicalOrderPresetKey,
            params CASocietyPoliticalOverride[] politicalOverrides)
        {
            return new CASocietyPresetDef(key, label, catalogGroup,
                referenceRegion, approximatePeriod, summary,
                culturePresetKey, politicalOrderPresetKey,
                politicalOverrides);
        }

        private static CASocietyPoliticalOverride Q(string questionKey,
            params object[] pairs)
        {
            return new CASocietyPoliticalOverride(new[] { questionKey },
                Shares(pairs));
        }

        private static CASocietyPoliticalOverride P(params object[] pairs)
        {
            return new CASocietyPoliticalOverride(
                CAPoliticalQuestionRegistry.Questions.Where(item =>
                        item.Group == CAPoliticalQuestionRegistry.Property)
                    .Select(item => item.Key), Shares(pairs));
        }

        private static IEnumerable<KeyValuePair<string, int>> Shares(
            object[] pairs)
        {
            for (int index = 0; index + 1 < (pairs?.Length ?? 0); index += 2)
                yield return new KeyValuePair<string, int>(
                    (string)pairs[index], (int)pairs[index + 1]);
        }
    }
}
