using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    public sealed class CACulturePresetValue
    {
        public string QuestionKey;
        public float Mean;
        public float Salience;
        public float SourceConfidence;
        public string Evidence;

        public CACulturePresetValue(string questionKey, float mean,
            float salience, float sourceConfidence, string evidence)
        {
            QuestionKey = questionKey;
            Mean = mean;
            Salience = salience;
            SourceConfidence = sourceConfidence;
            Evidence = evidence;
        }
    }

    public sealed class CACulturePresetDef
    {
        public string Key;
        public string Label;
        public string CatalogGroup;
        public string ReferenceContext;
        public string ReferenceRegion;
        public string ApproximatePeriod;
        public string Sources;
        public string Summary;
        public string Rationale;
        public int GlobalDiversity;
        public float NormStrength;
        public float DivergenceTolerance;
        public IReadOnlyList<CACulturePresetValue> Values;

        public string ValidationFailure()
        {
            if (string.IsNullOrWhiteSpace(Key)
                || string.IsNullOrWhiteSpace(Label)
                || string.IsNullOrWhiteSpace(CatalogGroup)
                || string.IsNullOrWhiteSpace(ReferenceContext)
                || string.IsNullOrWhiteSpace(Sources)
                || string.IsNullOrWhiteSpace(Summary)
                || string.IsNullOrWhiteSpace(Rationale))
                return "preset identity or rationale is incomplete";
            if (CatalogGroup == "Historical cultures"
                && (string.IsNullOrWhiteSpace(ReferenceRegion)
                    || string.IsNullOrWhiteSpace(ApproximatePeriod)))
                return "historical Culture metadata is incomplete";
            if (GlobalDiversity < 0 || GlobalDiversity > 4
                || NormStrength < 0f || NormStrength > 1f
                || DivergenceTolerance < 0f
                || DivergenceTolerance > 1f)
                return "preset population setting is outside range";
            if (Values == null
                || Values.Count != CACultureQuestionRegistry.FixedQuestionCount)
                return "preset does not specify every Culture question";
            if (Values.Any(value => value == null
                    || CACultureQuestionRegistry.Find(value.QuestionKey) == null
                    || value.Mean < -1f || value.Mean > 1f
                    || value.Salience < 0f || value.Salience > 1f
                    || value.SourceConfidence < 0f
                    || value.SourceConfidence > 1f
                    || string.IsNullOrWhiteSpace(value.Evidence))
                || Values.GroupBy(value => value.QuestionKey,
                    StringComparer.Ordinal).Any(group => group.Count() != 1))
                return "preset contains an invalid or duplicated question";
            return null;
        }
    }

    // Built-in presets, manual editing, saved profiles, and randomization all
    // write the same CACulture question distributions. There is no preset
    // mode, blend state, or hidden identity attached to a Culture after use.
    public static class CACulturePresetLibrary
    {
        private static readonly CACulturePresetDef[] Definitions = Build();

        public static IReadOnlyList<CACulturePresetDef> All => Definitions;

        public static CACulturePresetDef Find(string key)
        {
            return Definitions.FirstOrDefault(value => value.Key == key);
        }

        public static string ValidationFailure()
        {
            if (Definitions.Length != 22)
                return "the built-in Culture preset library is incomplete";
            if (Definitions.GroupBy(value => value.Key, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
                return "Culture preset keys are duplicated";
            foreach (CACulturePresetDef definition in Definitions)
            {
                string failure = definition.ValidationFailure();
                if (!string.IsNullOrWhiteSpace(failure))
                    return definition.Key + ": " + failure;
            }
            return null;
        }

        public static bool Matches(CACulture culture,
            CACulturePresetDef preset)
        {
            if (culture == null || preset == null) return false;
            var expected = new CACulture();
            Apply(expected, preset, "preset-match:" + preset.Key);
            return CACultureModel.MatchesInheritedTemplate(culture, expected);
        }

        public static void Apply(CACulture culture,
            CACulturePresetDef preset, string sourceIdentity = null)
        {
            if (culture == null || preset == null) return;
            if (!string.IsNullOrWhiteSpace(preset.ValidationFailure())) return;
            culture.withinGroupSpread = preset.GlobalDiversity;
            culture.questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            culture.inheritedQuestions = preset.Values.Select(value =>
            {
                CACultureQuestionDistribution distribution =
                    CACultureDistributionKernel.NewQuestion(
                        CACultureQuestionRegistry.Find(value.QuestionKey));
                distribution.mean = value.Mean;
                distribution.hasDescriptiveNormPrior = true;
                distribution.descriptiveNormPrior = value.Mean;
                distribution.spread = CACultureAuthoringKernel.SpreadFor(
                    preset.GlobalDiversity);
                distribution.spreadOverride = false;
                distribution.salience = value.Salience;
                distribution.normStrength = preset.NormStrength;
                distribution.toleranceForDivergence =
                    preset.DivergenceTolerance;
                distribution.visibility = 0.65f;
                distribution.sourceConfidence = value.SourceConfidence;
                distribution.provenance = "built-in Culture authoring prior: "
                    + preset.Key;
                distribution.sourceIdentity = sourceIdentity
                    ?? "preset:" + preset.Key;
                distribution.evidenceSignature =
                    CASocialPatternKernel.StableHash(preset.Key + "|"
                        + value.QuestionKey + "|" + value.Mean.ToString(
                            "0.000", System.Globalization.CultureInfo
                                .InvariantCulture) + "|" + value.Evidence);
                return distribution;
            }).ToList();
            culture.localQuestions = culture.localQuestions
                ?? new List<CACultureQuestionDistribution>();
            culture.authoredMask |= CACulture.QuestionStateField
                | CACulture.DiversityField;
            culture.name = preset.Label;
            culture.authoredMask &= ~CACulture.NameField;
            CACultureModel.Normalize(culture);
            CACultureModel.SynchronizeOwnIdentityLabel(culture);
        }

        private static CACulturePresetDef[] Build()
        {
            return new[]
            {
                Preset("mobile-kin-band", "Mobile kin culture",
                    "Strong kin duties, little durable rank, and practical knowledge carried through the group.",
                    "Small mobile groups organized around kin reciprocity and limited formal office.",
                    1, 0.62f, 0.34f,
                    -0.05f, -0.10f, 0.82f, 0.12f, 0.18f, 0.22f,
                    -0.70f, -0.62f, 0.58f, -0.20f, -0.12f, -0.28f,
                    0.22f, 0.10f, -0.48f, -0.48f, 0.72f, 0.30f,
                    0.42f, -0.22f, 0.52f, 0.38f, 0.22f, 0.46f),
                Preset("ranked-agrarian-households",
                    "Ranked agrarian culture",
                    "Landed households, inherited standing, customary authority, and divided work.",
                    "Agrarian household orders with inherited status, local custom, and restricted office.",
                    2, 0.74f, 0.25f,
                    -0.34f, -0.42f, 0.78f, -0.46f, 0.62f, -0.55f,
                    0.78f, 0.70f, -0.60f, -0.38f, -0.48f, -0.58f,
                    -0.52f, -0.60f, 0.48f, 0.45f, 0.42f, -0.48f,
                    0.22f, 0.44f, 0.40f, -0.36f, -0.44f, 0.18f),
                Preset("civic-market-town", "Civic market-town culture",
                    "Mixed households, public gathering, voluntary trade, and respected practical expertise.",
                    "Town society organized through exchange, negotiated office, and guild-like skill.",
                    2, 0.54f, 0.52f,
                    0.34f, 0.18f, 0.22f, 0.28f, -0.18f, 0.38f,
                    -0.30f, -0.22f, 0.56f, 0.52f, 0.42f, 0.48f,
                    0.58f, 0.46f, 0.18f, -0.46f, 0.34f, -0.20f,
                    0.44f, -0.12f, -0.28f, 0.58f, 0.46f, 0.72f),
                Preset("central-court-society", "Central court culture",
                    "Formal rank, concentrated office, managed public order, and specialized knowledge.",
                    "A central court with durable status, restricted voice, and administered authority.",
                    2, 0.78f, 0.22f,
                    -0.22f, -0.18f, 0.52f, -0.42f, 0.36f, -0.42f,
                    0.76f, 0.82f, -0.64f, -0.38f, -0.52f, -0.62f,
                    -0.58f, -0.68f, 0.76f, 0.38f, 0.44f, -0.68f,
                    0.18f, 0.62f, 0.48f, -0.44f, -0.24f, 0.74f),
                Preset("frontier-mutual-aid", "Frontier mutual-aid culture",
                    "Strong practical cooperation, open mobility, and cautious contact under uncertain conditions.",
                    "A recently established population depending on shared provision and adaptable skill.",
                    3, 0.52f, 0.58f,
                    0.24f, 0.10f, 0.58f, 0.36f, -0.28f, 0.42f,
                    -0.72f, -0.58f, 0.78f, 0.08f, 0.32f, 0.52f,
                    0.46f, 0.38f, 0.28f, -0.52f, 0.82f, 0.18f,
                    0.34f, 0.06f, 0.12f, 0.54f, 0.70f, 0.74f),
                Preset("industrial-civic-association",
                    "Industrial civic culture",
                    "Broad membership, formal procedure, specialist work, and open technical knowledge.",
                    "A civic association structured by public procedure, skilled production, and institutional provision.",
                    3, 0.50f, 0.62f,
                    0.58f, 0.28f, 0.18f, 0.62f, -0.62f, 0.72f,
                    -0.72f, -0.48f, 0.74f, 0.62f, 0.72f, 0.68f,
                    0.78f, 0.68f, 0.26f, -0.70f, 0.68f, 0.22f,
                    0.58f, -0.38f, -0.42f, 0.82f, 0.78f, 0.84f),
                HistoricalPreset("united-states-postwar-mid-century",
                    "Postwar American culture",
                    "Americas", "United States", "1946-1964",
                    "GSS longitudinal social-change series; WVS United States samples; ISSP Family and Changing Gender Roles modules",
                    "Strong conventional family norms, gendered work, private property, civic participation, and confidence in enforcement.",
                    "A research-informed authoring prior for the United States after World War II and before the major late-century shifts recorded by GSS, WVS, and ISSP series.",
                    3, 0.70f, 0.38f,
                    -0.85f, -0.80f, 0.20f, -0.55f, 0.62f, -0.45f,
                    -0.45f, -0.10f, 0.65f, 0.00f, -0.30f, 0.25f,
                    -0.70f, -0.60f, 0.35f, 0.30f, 0.45f, 0.40f,
                    0.55f, 0.40f, -0.15f, 0.55f, 0.35f, 0.50f),
                HistoricalPreset("united-states-turn-millennium",
                    "Turn-of-the-millennium American culture",
                    "Americas", "United States", "1995-2005",
                    "GSS longitudinal social-change series; WVS United States samples; ISSP Family and Changing Gender Roles modules",
                    "Broader relationship and office acceptance, open membership, strong mobility, private property, and accessible technical knowledge.",
                    "A research-informed authoring prior centered on the longitudinal social changes visible around the millennium in GSS, WVS, and ISSP series.",
                    3, 0.56f, 0.55f,
                    0.25f, -0.55f, 0.00f, -0.10f, 0.10f, 0.45f,
                    -0.60f, -0.05f, 0.70f, 0.20f, 0.10f, 0.40f,
                    -0.80f, -0.65f, 0.55f, 0.55f, 0.35f, 0.50f,
                    0.45f, 0.35f, -0.15f, 0.65f, 0.55f, 0.55f),
                HistoricalPreset("united-states-contemporary",
                    "Contemporary American culture",
                    "Americas", "United States", "2017-2024",
                    "GSS longitudinal social-change series; WVS United States samples; ISSP Family and Changing Gender Roles modules",
                    "Broad relationship and office acceptance, open work, civic voice, accessible knowledge, and substantial internal disagreement.",
                    "A research-informed authoring prior for recent United States responses in GSS, WVS, and ISSP series; it is not a fitted national distribution.",
                    4, 0.50f, 0.66f,
                    0.75f, -0.15f, 0.00f, 0.00f, -0.45f, 0.80f,
                    -0.70f, -0.10f, 0.55f, 0.20f, 0.25f, 0.45f,
                    -0.85f, -0.45f, 0.65f, 0.55f, 0.25f, 0.55f,
                    0.20f, 0.25f, 0.05f, 0.75f, 0.45f, 0.45f),
                HistoricalPreset("english-north-america-early-colonial",
                    "Early English colonial culture",
                    "Americas", "English North American colonies",
                    "1607-1700",
                    "Library of Congress colonial North America collections; Library of Congress Africans in America historical overview",
                    "Kin and household obligation, restricted public standing, private property, coerced labor, severe punishment, and guarded membership.",
                    "A prior for dominant English colonial settlements. It does not stand for Indigenous nations, enslaved Africans, other European colonies, or every colony within the period.",
                    4, 0.76f, 0.22f,
                    -0.90f, -0.85f, 0.78f, -0.72f, 0.72f, -0.78f,
                    0.30f, 0.48f, -0.10f, -0.72f, -0.72f, -0.68f,
                    -0.55f, -0.58f, 0.55f, 0.82f, 0.15f, -0.75f,
                    -0.40f, 0.62f, 0.55f, -0.30f, -0.25f, 0.42f),
                HistoricalPreset("united-states-civil-war-union",
                    "Civil War Union culture",
                    "Americas", "United States (Union)", "1861-1865",
                    "Library of Congress Civil War primary-source timeline; Abraham Lincoln Papers on emancipation",
                    "Anti-aristocratic civic identity, broad white male participation, private property, wartime enforcement, and rapidly changing membership claims.",
                    "A prior for the Union's dominant public culture during the war. It leaves Black, immigrant, Indigenous, dissenting, and local cultures available as separate populations rather than averaging them away.",
                    4, 0.65f, 0.38f,
                    -0.88f, -0.85f, 0.52f, -0.62f, 0.58f, -0.68f,
                    -0.70f, -0.15f, 0.52f, -0.15f, -0.25f, 0.05f,
                    0.20f, 0.15f, 0.55f, 0.35f, 0.20f, -0.75f,
                    0.22f, 0.35f, 0.40f, 0.48f, 0.55f, 0.55f),
                HistoricalPreset("confederate-slaveholding-dominant-culture",
                    "Confederate slaveholding culture",
                    "Americas", "Confederate States", "1861-1865",
                    "Library of Congress Civil War primary-source timeline; Library of Congress Africans in America historical overview",
                    "Hereditary racial caste, concentrated property, enslaved labor, restricted voice, exclusion, and severe punishment.",
                    "Represents the Confederacy's dominant slaveholding culture. It does not describe enslaved people, free Black communities, Indigenous nations, Unionists, or every white Southerner.",
                    4, 0.82f, 0.12f,
                    -0.90f, -0.85f, 0.68f, -0.78f, 0.78f, -0.82f,
                    0.55f, 0.82f, -0.68f, -0.85f, -0.88f, -0.82f,
                    -0.58f, -0.70f, 0.80f, 0.95f, -0.20f, -0.92f,
                    -0.55f, 0.82f, 0.72f, -0.35f, -0.18f, 0.30f),
                HistoricalPreset("freedpeople-emancipation-communities",
                    "Freedpeople emancipation culture",
                    "Americas", "United States freedpeople communities",
                    "1863-1877",
                    "Library of Congress Freedmen primary-source timeline; Library of Congress Abraham Lincoln and emancipation collections",
                    "Family reunification, freedom from forced labor, public voice, mutual aid, education, mobility, and equal citizenship.",
                    "Represents freedpeople building families and institutions during emancipation and Reconstruction. It is not a single profile for every Black American.",
                    4, 0.68f, 0.45f,
                    -0.75f, -0.75f, 0.85f, -0.25f, 0.25f, -0.15f,
                    -0.90f, -0.70f, 0.75f, 0.15f, 0.45f, 0.60f,
                    0.80f, 0.65f, 0.05f, -0.90f, 0.75f, -0.25f,
                    0.65f, -0.25f, -0.20f, 0.85f, 0.68f, 0.55f),
                HistoricalPreset("france-napoleonic-empire",
                    "Napoleonic French culture", "Europe", "France",
                    "1804-1815",
                    "Fondation Napoleon Code civil dossiers; Napoleonic legal, educational, and administrative records",
                    "Patriarchal family law, protected private property, merit-linked office, conscription, centralized enforcement, and legal-administrative innovation.",
                    "A prior for metropolitan French public culture under the First Empire; occupied territories, colonies, classes, and political opponents remain distinct populations.",
                    3, 0.72f, 0.24f,
                    -0.85f, -0.82f, 0.55f, -0.72f, 0.62f, -0.70f,
                    -0.25f, 0.42f, 0.35f, -0.20f, 0.05f, 0.05f,
                    -0.65f, -0.75f, 0.80f, 0.75f, 0.05f, -0.82f,
                    -0.10f, 0.50f, 0.45f, 0.25f, 0.58f, 0.65f),
                HistoricalPreset("france-second-empire",
                    "Second-Empire French culture", "Europe", "France",
                    "1852-1870",
                    "Fondation Napoleon Second Empire legal and education dossiers; French social and labor legislation of the period",
                    "Plebiscitary executive rule, private enterprise, industrial mobility, public works, regulated dissent, and expanding technical knowledge.",
                    "A prior spanning the authoritarian and liberal phases of the Second Empire; class, region, religion, and opposition movements remain internally diverse.",
                    3, 0.62f, 0.36f,
                    -0.82f, -0.78f, 0.48f, -0.62f, 0.55f, -0.62f,
                    -0.15f, 0.30f, 0.45f, 0.05f, 0.18f, 0.20f,
                    -0.30f, -0.25f, 0.70f, 0.35f, 0.28f, -0.72f,
                    0.05f, 0.45f, 0.35f, 0.45f, 0.72f, 0.72f),
                HistoricalPreset("germany-weimar-republic",
                    "Weimar German culture", "Europe", "Germany",
                    "1919-1933",
                    "German History in Documents and Images, Weimar Constitution and period documents",
                    "Universal suffrage, legal gender equality, abolished birth privilege, protected dissent, private property, social insurance, and plural public life.",
                    "The constitutional and civic center is paired with high diversity because Weimar Germany contained sharp regional, religious, class, and antidemocratic divisions.",
                    4, 0.50f, 0.62f,
                    -0.70f, -0.70f, 0.42f, -0.15f, 0.28f, 0.25f,
                    -0.72f, -0.25f, 0.62f, -0.05f, 0.20f, 0.35f,
                    0.75f, 0.75f, 0.35f, -0.45f, 0.70f, -0.48f,
                    0.45f, 0.10f, 0.10f, 0.72f, 0.78f, 0.72f),
                HistoricalPreset("germany-national-socialist-dictatorship",
                    "Nazi regime culture", "Europe",
                    "Germany", "1933-1945",
                    "United States Holocaust Memorial Museum Holocaust Encyclopedia and Nazi law, education, labor, gender, and persecution collections",
                    "Racial heredity, exclusion, forced integration into state organizations, suppressed dissent, coerced labor, severe punishment, and ideological control of knowledge.",
                    "This represents the regime's promoted dominant culture and coercive public norms. It does not assign those positions to victims, resisters, occupied peoples, or every German.",
                    2, 0.95f, 0.02f,
                    -0.95f, -0.90f, 0.78f, -0.85f, 0.72f, -0.88f,
                    0.72f, 0.85f, -0.62f, -0.98f, -0.98f, -0.96f,
                    -0.92f, -0.98f, 0.95f, 0.98f, 0.25f, -0.55f,
                    -0.98f, 0.96f, 0.95f, -0.82f, 0.15f, 0.30f),
                HistoricalPreset("japan-late-tokugawa",
                    "Late Tokugawa Japanese culture", "Asia", "Japan", "1800-1867",
                    "National Diet Library histories of Edo education and social institutions; Asia for Educators Tokugawa materials",
                    "Binding household duty, hereditary status, divided work, restricted membership, customary enforcement, and respected practical and classical expertise.",
                    "A prior for late Tokugawa public culture; domains, classes, cities, villages, outcast communities, Ainu, and Ryukyuan populations remain distinct.",
                    3, 0.82f, 0.18f,
                    -0.85f, -0.65f, 0.80f, -0.68f, 0.70f, -0.72f,
                    0.82f, 0.90f, -0.72f, -0.82f, -0.75f, -0.80f,
                    -0.72f, -0.70f, 0.75f, 0.55f, 0.20f, -0.25f,
                    -0.15f, 0.60f, 0.35f, -0.15f, -0.35f, 0.60f),
                HistoricalPreset("japan-meiji-transformation",
                    "Meiji Japanese culture", "Asia", "Japan",
                    "1868-1912",
                    "National Diet Library Modern Japan in Archives and histories of the Meiji education system",
                    "Abolished formal estates, rapid mobility, conscription, centralized enforcement, mass education, technical expertise, and experimental institutional change.",
                    "A prior for the transformation's dominant national project; women, classes, regions, political movements, and colonized populations remain distinct.",
                    3, 0.74f, 0.24f,
                    -0.82f, -0.70f, 0.68f, -0.62f, 0.55f, -0.65f,
                    0.20f, 0.35f, 0.50f, -0.20f, 0.10f, 0.15f,
                    -0.25f, -0.35f, 0.80f, 0.60f, 0.15f, -0.62f,
                    -0.15f, 0.45f, 0.38f, 0.68f, 0.88f, 0.82f),
                HistoricalPreset("qing-china-late-imperial",
                    "Late Qing Chinese culture", "Asia", "Qing China",
                    "1800-1911",
                    "Asia for Educators late-imperial social order, Confucian classics, and civil-service examination materials",
                    "Extended-kin duty, entrenched status, examination-linked mobility, restricted voice, literati expertise, customary enforcement, and guarded membership.",
                    "A high-diversity prior for an immense empire; region, ethnicity, class, religion, gender, rebellion, and late reform cannot be collapsed into one population.",
                    4, 0.85f, 0.15f,
                    -0.88f, -0.55f, 0.90f, -0.78f, 0.72f, -0.85f,
                    0.50f, 0.82f, 0.20f, -0.60f, -0.35f, -0.55f,
                    -0.82f, -0.78f, 0.82f, 0.62f, 0.10f, -0.45f,
                    -0.40f, 0.70f, 0.55f, -0.25f, -0.55f, 0.82f),
                HistoricalPreset("ottoman-empire-tanzimat",
                    "Tanzimat Ottoman culture", "Asia", "Ottoman Empire",
                    "1839-1876",
                    "Law Library of Congress Ottoman legal history; Library of Congress Federal Research Division; Tanzimat decrees and nationality law",
                    "Imperial reform, qualified legal equality, retained communal difference, conscription, central administration, secular schools, and changing property rules.",
                    "A high-diversity prior for reform amid persistent millet, regional, ethnic, religious, gender, and class differences; proclaimed equality and realized practice remain separate.",
                    4, 0.65f, 0.35f,
                    -0.84f, -0.45f, 0.78f, -0.72f, 0.65f, -0.62f,
                    0.25f, 0.50f, 0.30f, -0.10f, -0.05f, 0.15f,
                    -0.35f, -0.45f, 0.75f, 0.45f, 0.25f, -0.38f,
                    0.20f, 0.15f, 0.25f, 0.35f, 0.60f, 0.70f),
                HistoricalPreset("mughal-india-akbar",
                    "Akbar-era Mughal culture", "Asia", "Mughal India",
                    "1556-1605",
                    "Metropolitan Museum of Art Mughal histories; Library of Congress India and Pakistan country studies",
                    "Dynastic rank, imperial service, cross-religious court inclusion, strong enforcement, commercial exchange, translation, and expert administration.",
                    "A high-diversity prior for an imperial center and its promoted synthesis; caste, locality, religion, gender, class, and communities beyond the court remain distinct.",
                    4, 0.70f, 0.38f,
                    -0.85f, -0.50f, 0.82f, -0.72f, 0.65f, -0.68f,
                    0.52f, 0.78f, 0.15f, 0.20f, 0.25f, 0.05f,
                    -0.72f, -0.35f, 0.78f, 0.55f, 0.20f, -0.20f,
                    -0.10f, 0.50f, 0.35f, 0.05f, 0.55f, 0.70f)
            };
        }

        // Values are deliberately positional here and validated against the
        // canonical registry order. This compact form keeps every preset
        // complete while making source drift visible to receipts.
        private static CACulturePresetDef Preset(string key, string label,
            string summary, string rationale, int diversity,
            float normStrength, float tolerance, params float[] means)
        {
            return Preset(key, label, "Social forms", "social form", null,
                null,
                "WVS7; ISSP22; ESS; GSS; Schwartz; social-norm and legitimacy literature",
                summary, rationale, diversity, normStrength, tolerance, means);
        }

        private static CACulturePresetDef HistoricalPreset(string key,
            string label, string referenceRegion, string referenceContext,
            string approximatePeriod, string sources, string summary,
            string rationale, int diversity, float normStrength, float tolerance,
            params float[] means)
        {
            return Preset(key, label, "Historical cultures", referenceContext,
                referenceRegion,
                approximatePeriod, sources, summary, rationale, diversity,
                normStrength, tolerance, means);
        }

        private static CACulturePresetDef Preset(string key, string label,
            string catalogGroup, string referenceContext,
            string referenceRegion, string approximatePeriod,
            string sources, string summary, string rationale, int diversity,
            float normStrength, float tolerance, params float[] means)
        {
            if (means.Length == CACultureQuestionRegistry.LegacyQuestionCount)
                means = means.Concat(B16Means(key)).ToArray();
            if (means.Length != CACultureQuestionRegistry.FixedQuestionCount)
                throw new InvalidOperationException("Culture preset " + key
                    + " has " + means.Length + " means, expected "
                    + CACultureQuestionRegistry.FixedQuestionCount + ".");
            IReadOnlyList<CACultureQuestionDef> questions =
                CACultureQuestionRegistry.All;
            var values = new List<CACulturePresetValue>(
                CACultureQuestionRegistry.FixedQuestionCount);
            for (int i = 0; i < means.Length; i++)
            {
                float salience = 0.48f + Math.Abs(means[i]) * 0.28f;
                values.Add(new CACulturePresetValue(questions[i].Key,
                    means[i], Math.Min(0.90f, salience), 0.34f,
                    "Contextual authoring estimate informed by the preset source list; not a fitted question-level measurement."));
            }
            return new CACulturePresetDef
            {
                Key = key,
                Label = label,
                CatalogGroup = catalogGroup,
                ReferenceContext = referenceContext,
                ReferenceRegion = referenceRegion,
                ApproximatePeriod = approximatePeriod,
                Sources = sources,
                Summary = summary,
                Rationale = rationale,
                GlobalDiversity = diversity,
                NormStrength = normStrength,
                DivergenceTolerance = tolerance,
                Values = values
            };
        }

        // Registry order 24..47: sexual conduct, marriage naming, childhood,
        // age, doctrine, xenotype, work, acquisition, violence, male and
        // female exposure, alteration, integrity, pain, remains, human flesh,
        // animal food, food adaptability, drugs, animal standing, resources,
        // settlement, comfort, machines. These are authored priors, not
        // measurements of every person represented by a historical label.
        private static float[] B16Means(string key)
        {
            switch (key)
            {
                case "mobile-kin-band": return V(
                    .15f, 0f, .30f, .55f, .15f, -.10f, .60f, .15f,
                    .20f, .35f, .25f, .10f, .20f, .15f, .60f, -.75f,
                    .70f, .75f, .20f, .40f, .65f, -.45f, -.35f, -.70f);
                case "ranked-agrarian-households": return V(
                    -.35f, -.65f, -.05f, .70f, -.30f, .45f, .65f, .15f,
                    .25f, -.45f, -.65f, -.15f, .10f, .20f, .70f, -.80f,
                    .65f, .25f, -.15f, .15f, .15f, .80f, .05f, -.90f);
                case "civic-market-town": return V(
                    .30f, 0f, .55f, .10f, .45f, -.25f, .50f, -.65f,
                    -.25f, -.15f, -.15f, .25f, .65f, -.25f, .55f, -.90f,
                    .45f, .60f, .15f, .40f, .30f, .75f, .55f, .10f);
                case "central-court-society": return V(
                    -.40f, -.70f, .10f, .70f, -.35f, .50f, .65f, .45f,
                    .45f, -.55f, -.75f, .20f, -.10f, .45f, .75f, -.80f,
                    .65f, .45f, .25f, .05f, -.10f, .75f, .55f, .05f);
                case "frontier-mutual-aid": return V(
                    .25f, 0f, .45f, .20f, .40f, -.20f, .75f, -.10f,
                    .15f, .25f, .15f, .15f, .55f, -.25f, .55f, -.85f,
                    .70f, .80f, .25f, .35f, .40f, .70f, -.10f, -.20f);
                case "industrial-civic-association": return V(
                    .20f, -.10f, .60f, .05f, .45f, -.30f, .75f, -.70f,
                    -.30f, -.20f, -.25f, .45f, .70f, -.35f, .50f, -.90f,
                    .50f, .65f, .15f, .35f, .25f, .85f, .65f, .75f);
                case "united-states-postwar-mid-century": return V(
                    -.55f, -.70f, .55f, .35f, .15f, -.45f, .70f, -.75f,
                    -.20f, -.65f, -.85f, .20f, .60f, -.35f, .65f, -.95f,
                    .70f, .45f, -.15f, .20f, .15f, .85f, .65f, .50f);
                case "united-states-turn-millennium": return V(
                    .20f, -.10f, .70f, 0f, .55f, -.35f, .65f, -.80f,
                    -.30f, -.15f, -.20f, .45f, .75f, -.40f, .60f, -.95f,
                    .45f, .70f, .10f, .45f, .35f, .80f, .75f, .80f);
                case "united-states-contemporary": return V(
                    .55f, .05f, .80f, 0f, .70f, -.30f, .60f, -.85f,
                    -.25f, .10f, .10f, .55f, .80f, -.45f, .65f, -.95f,
                    .35f, .80f, .25f, .55f, .55f, .70f, .80f, .90f);
                case "english-north-america-early-colonial": return V(
                    -.80f, -.85f, -.10f, .55f, -.65f, .45f, .80f, .15f,
                    .35f, -.80f, -.90f, -.30f, .35f, .25f, .85f, -.95f,
                    .75f, -.20f, -.45f, .05f, .25f, .90f, -.15f, -.95f);
                case "united-states-civil-war-union": return V(
                    -.75f, -.80f, .15f, .25f, -.10f, -.35f, .75f, .10f,
                    .55f, -.70f, -.85f, -.20f, .45f, .35f, .75f, -.95f,
                    .75f, .10f, -.35f, .10f, .10f, .90f, -.10f, -.90f);
                case "confederate-slaveholding-dominant-culture": return V(
                    -.85f, -.90f, -.55f, .65f, -.75f, .90f, .80f, .55f,
                    .70f, -.80f, -.95f, -.25f, -.55f, .45f, .70f, -.90f,
                    .75f, -.25f, -.40f, -.15f, -.20f, .95f, .15f, -.95f);
                case "freedpeople-emancipation-communities": return V(
                    -.45f, -.45f, .70f, .45f, .65f, -.80f, .85f, -.75f,
                    -.10f, -.65f, -.75f, -.20f, .80f, -.35f, .85f, -.95f,
                    .65f, .20f, -.35f, .25f, .30f, .80f, -.20f, -.90f);
                case "france-napoleonic-empire": return V(
                    -.75f, -.85f, .10f, .40f, -.45f, -.20f, .85f, .25f,
                    .55f, -.75f, -.90f, .15f, .30f, .25f, .75f, -.95f,
                    .70f, .15f, -.15f, .05f, .05f, .85f, .25f, -.85f);
                case "france-second-empire": return V(
                    -.60f, -.75f, .30f, .25f, -.25f, -.30f, .80f, -.40f,
                    .25f, -.65f, -.80f, .25f, .45f, -.20f, .65f, -.95f,
                    .65f, .35f, -.05f, .15f, .10f, .85f, .50f, -.60f);
                case "germany-weimar-republic": return V(
                    .20f, -.20f, .70f, 0f, .70f, -.55f, .65f, -.65f,
                    -.35f, -.20f, -.30f, .45f, .70f, -.30f, .60f, -.95f,
                    .50f, .65f, .10f, .40f, .35f, .75f, .55f, .55f);
                case "germany-national-socialist-dictatorship": return V(
                    -.90f, -.85f, -.95f, .65f, -.98f, .98f, .95f, .85f,
                    .95f, -.65f, -.85f, .55f, -.90f, .85f, -.20f, -.85f,
                    .70f, -.10f, .20f, -.80f, -.70f, .90f, .20f, .45f);
                case "japan-late-tokugawa": return V(
                    -.80f, -.90f, .05f, .80f, -.65f, .50f, .75f, -.25f,
                    .35f, -.70f, -.90f, -.10f, .30f, .35f, .80f, -.95f,
                    .70f, .35f, -.20f, .15f, .35f, .90f, .10f, -.90f);
                case "japan-meiji-transformation": return V(
                    -.70f, -.80f, .35f, .40f, -.20f, -.15f, .90f, -.20f,
                    .55f, -.65f, -.85f, .25f, .40f, .30f, .65f, -.95f,
                    .65f, .55f, -.05f, .15f, .20f, .85f, .30f, .40f);
                case "qing-china-late-imperial": return V(
                    -.85f, -.90f, -.10f, .85f, -.60f, .55f, .80f, -.20f,
                    .35f, -.80f, -.95f, -.20f, .20f, .40f, .85f, -.95f,
                    .70f, .25f, -.25f, .10f, .30f, .90f, .05f, -.90f);
                case "ottoman-empire-tanzimat": return V(
                    -.75f, -.80f, .20f, .55f, -.15f, .25f, .85f, -.15f,
                    .45f, -.75f, -.90f, .10f, .35f, .35f, .75f, -.95f,
                    .65f, .40f, -.10f, .15f, .15f, .85f, .25f, -.45f);
                case "mughal-india-akbar": return V(
                    -.70f, -.75f, .15f, .65f, .35f, .20f, .80f, -.05f,
                    .35f, -.70f, -.85f, .10f, .30f, .30f, .80f, -.95f,
                    .60f, .50f, -.10f, .20f, .25f, .85f, .30f, -.80f);
                default:
                    throw new InvalidOperationException("Culture preset " + key
                        + " has no B16 mechanical-coverage composition.");
            }
        }

        private static float[] V(params float[] values)
        {
            if (values.Length != CACultureQuestionRegistry.FixedQuestionCount
                    - CACultureQuestionRegistry.LegacyQuestionCount)
                throw new InvalidOperationException(
                    "B16 Culture preset completion has " + values.Length
                        + " values, expected 24.");
            return values;
        }
    }

    public static class CACultureAuthoringKernel
    {
        private static readonly float[] SpreadValues =
            { 0.10f, 0.18f, 0.28f, 0.42f, 0.60f };

        public static float SpreadFor(int diversity)
        {
            return SpreadValues[Math.Max(0, Math.Min(4, diversity))];
        }

        public static void Randomize(CACulture culture, string seed,
            string sourceIdentity = null)
        {
            if (culture == null) return;
            uint state = Hash(seed ?? "culture-random");
            culture.withinGroupSpread = (int)(Next(ref state) % 5u);
            culture.questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            culture.inheritedQuestions = CACultureQuestionRegistry.All
                .Select(definition => RandomQuestion(definition, ref state,
                    culture.withinGroupSpread, sourceIdentity ?? seed,
                    "randomized Culture"))
                .ToList();
            culture.localQuestions = culture.localQuestions
                ?? new List<CACultureQuestionDistribution>();
            culture.authoredMask |= CACulture.QuestionStateField
                | CACulture.DiversityField;
            CACultureModel.Normalize(culture);
        }

        // The next explicit randomization is derived from the current Culture
        // state. Closing and reopening the editor therefore cannot replay its
        // first dialog-local nonce and silently produce the same values.
        public static string NextRandomizationSeed(CACulture culture,
            string identity)
        {
            string owner = string.IsNullOrWhiteSpace(identity)
                ? "authored-culture" : identity;
            string questions = CACultureDistributionKernel.Fingerprint(
                (culture?.inheritedQuestions
                    ?? new List<CACultureQuestionDistribution>())
                .Concat(culture?.localQuestions
                    ?? new List<CACultureQuestionDistribution>()));
            return owner + ":editor-random:"
                + (culture?.withinGroupSpread ?? 2) + ":" + questions;
        }

        // World generation completes only absent questions. Existing authored
        // and inherited distributions keep their values and provenance.
        public static int CompleteMissing(CACulture culture, string seed,
            string sourceIdentity = null)
        {
            if (culture == null) return 0;
            culture.inheritedQuestions = culture.inheritedQuestions
                ?? new List<CACultureQuestionDistribution>();
            culture.localQuestions = culture.localQuestions
                ?? new List<CACultureQuestionDistribution>();
            var present = new HashSet<string>(culture.inheritedQuestions
                .Concat(culture.localQuestions).Where(value => value != null
                    && (value.populationScope ?? "*") == "*")
                .Select(value => value.questionKey), StringComparer.Ordinal);
            uint state = Hash(seed ?? "culture-completion");
            int added = 0;
            foreach (CACultureQuestionDef definition in
                CACultureQuestionRegistry.All)
            {
                // Advance once per canonical row so unrelated rows remain
                // stable when a different question was already authored.
                uint rowState = Next(ref state) ^ Hash(definition.Key);
                if (present.Contains(definition.Key)) continue;
                culture.inheritedQuestions.Add(RandomQuestion(definition,
                    ref rowState, culture.withinGroupSpread,
                    sourceIdentity ?? seed,
                    "generated missing Culture question"));
                added++;
            }
            culture.questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            CACultureModel.Normalize(culture);
            return added;
        }

        private static CACultureQuestionDistribution RandomQuestion(
            CACultureQuestionDef definition, ref uint state, int diversity,
            string sourceIdentity, string provenance)
        {
            CACultureQuestionDistribution value =
                CACultureDistributionKernel.NewQuestion(definition);
            value.mean = Range(ref state, -0.78f, 0.78f);
            value.hasDescriptiveNormPrior = true;
            value.descriptiveNormPrior = Clamp(value.mean
                + Range(ref state, -0.18f, 0.18f), -1f, 1f);
            value.spread = SpreadFor(diversity);
            value.spreadOverride = false;
            value.salience = Range(ref state, 0.36f, 0.86f);
            value.normStrength = Range(ref state, 0.28f, 0.78f);
            value.toleranceForDivergence = Range(ref state, 0.24f, 0.78f);
            value.visibility = Range(ref state, 0.35f, 0.90f);
            value.sourceConfidence = Range(ref state, 0.46f, 0.84f);
            value.prestigeSignal = Range(ref state, -0.20f, 0.20f);
            value.provenance = provenance;
            value.sourceIdentity = sourceIdentity;
            value.evidenceSignature = CASocialPatternKernel.StableHash(
                (sourceIdentity ?? "generated") + "|" + definition.Key
                    + "|" + value.mean.ToString("0.000",
                        System.Globalization.CultureInfo.InvariantCulture));
            return value;
        }

        private static float Range(ref uint state, float minimum,
            float maximum)
        {
            float unit = (Next(ref state) & 0x00FFFFFFu) / 16777215f;
            return minimum + (maximum - minimum) * unit;
        }

        private static uint Next(ref uint state)
        {
            if (state == 0u) state = 0x9E3779B9u;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private static uint Hash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
