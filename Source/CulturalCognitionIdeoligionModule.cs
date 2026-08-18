using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    internal enum CAIdeoligionSemanticDefKind : byte
    {
        Precept,
        Meme
    }

    internal enum CAIdeoligionPairAggregation : byte
    {
        None,
        Average,
        FemaleMinusMale
    }

    internal sealed class CAIdeoligionSemanticAdapterDef
    {
        internal string PackageId;
        internal CAIdeoligionSemanticDefKind Kind;
        internal string DefName;
        internal string QuestionKey;
        internal float Pressure;
        internal string PairKey;
        internal string PairSide;
        internal CAIdeoligionPairAggregation PairAggregation;

        internal string Identity => PackageId + "|" + Kind + "|" + DefName;
        internal string SourceToken => PackageId + "|" + Kind + "|"
            + DefName;

        internal string ValidationFailure()
        {
            if (PackageId.NullOrEmpty() || DefName.NullOrEmpty())
                return "exact package and def identity are required";
            if (CACultureQuestionRegistry.Find(QuestionKey) == null)
                return "unknown Culture question " + QuestionKey;
            if (Pressure < -1f || Pressure > 1f)
                return "pressure is outside -1..1";
            if (PairKey.NullOrEmpty() != PairSide.NullOrEmpty())
                return "paired semantics require both a pair and side";
            if (!PairKey.NullOrEmpty()
                && PairAggregation == CAIdeoligionPairAggregation.None)
                return "paired semantics require an aggregation rule";
            if (PairKey.NullOrEmpty()
                && PairAggregation != CAIdeoligionPairAggregation.None)
                return "unpaired semantics cannot name an aggregation rule";
            return null;
        }
    }

    internal readonly struct CAIdeoligionPressureProfile
    {
        internal readonly float Center;
        internal readonly float Intensity;
        internal readonly float Conflict;
        internal readonly string Source;

        internal CAIdeoligionPressureProfile(float center, float intensity,
            float conflict, string source)
        {
            Center = center;
            Intensity = intensity;
            Conflict = conflict;
            Source = source;
        }
    }

    // Exact package-plus-def mappings are the only route from native doctrine
    // into Culture. Unknown definitions remain native Ideoligion facts. No
    // defName prefix, label, description, or mod title is interpreted.
    internal static class CAIdeoligionSemanticAdapterRegistry
    {
        internal const string Core = "ludeon.rimworld";
        internal const string Ideology = "ludeon.rimworld.ideology";
        internal const string Biotech = "ludeon.rimworld.biotech";
        internal const string Odyssey = "ludeon.rimworld.odyssey";
        internal const string MorePrecepts = "llunak.MorePrecepts";

        private static readonly CAIdeoligionSemanticAdapterDef[] Definitions =
            Build().ToArray();

        internal static IReadOnlyList<CAIdeoligionSemanticAdapterDef> All =>
            Definitions;

        internal static IReadOnlyList<CAIdeoligionSemanticAdapterDef> Find(
            string packageId, CAIdeoligionSemanticDefKind kind,
            string defName)
        {
            string identity = Identity(packageId, kind, defName);
            return Definitions.Where(value => string.Equals(value.Identity,
                identity, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        internal static bool HasQuestion(string questionKey) =>
            !questionKey.NullOrEmpty() && Definitions.Any(value =>
                value.QuestionKey == questionKey);

        internal static string SourceLabel(string source)
        {
            const string unknown = "a related Ideoligion rule";
            if (source.NullOrEmpty()) return "Ideoligion";
            var labels = new List<string>();
            foreach (string encoded in source.Split(new[] { ';' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = encoded.Split('|');
                if (parts.Length != 3
                    || !Enum.TryParse(parts[1], true,
                        out CAIdeoligionSemanticDefKind kind)
                    || Find(parts[0], kind, parts[2]).Count == 0)
                {
                    labels.Add(unknown);
                    continue;
                }

                Def definition = kind == CAIdeoligionSemanticDefKind.Precept
                    ? (Def)DefDatabase<PreceptDef>.GetNamedSilentFail(parts[2])
                    : DefDatabase<MemeDef>.GetNamedSilentFail(parts[2]);
                if (definition == null || !string.Equals(
                        definition.modContentPack?.PackageId, parts[0],
                        StringComparison.OrdinalIgnoreCase))
                {
                    labels.Add(unknown);
                    continue;
                }
                labels.Add(definition.LabelCap.ToString());
            }
            return string.Join(" and ", labels.Distinct());
        }

        internal static string ValidationFailure()
        {
            foreach (CAIdeoligionSemanticAdapterDef definition in Definitions)
            {
                string failure = definition.ValidationFailure();
                if (!failure.NullOrEmpty())
                    return definition.Identity + " -> "
                        + definition.QuestionKey + ": " + failure;
            }
            if (Definitions.GroupBy(value => value.Identity + "|"
                        + value.QuestionKey + "|" + (value.PairSide ?? ""),
                    StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() != 1))
                return "the exact Ideoligion semantic registry contains a duplicate";
            return null;
        }

        internal static CAIdeoligionPressureProfile Profile(Ideo ideo,
            string questionKey)
        {
            if (ideo == null || questionKey.NullOrEmpty())
                return new CAIdeoligionPressureProfile(0f, 0f, 0f, null);
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Precept precept in ideo.PreceptsListForReading
                ?? new List<Precept>())
            {
                if (precept?.def == null) continue;
                selected.Add(Identity(precept.def.modContentPack?.PackageId,
                    CAIdeoligionSemanticDefKind.Precept,
                    precept.def.defName));
            }
            foreach (MemeDef meme in ideo.memes ?? new List<MemeDef>())
            {
                if (meme == null) continue;
                selected.Add(Identity(meme.modContentPack?.PackageId,
                    CAIdeoligionSemanticDefKind.Meme, meme.defName));
            }

            List<CAIdeoligionSemanticAdapterDef> matches = Definitions
                .Where(value => value.QuestionKey == questionKey
                    && selected.Contains(value.Identity)).ToList();
            List<float> components = matches
                .Where(value => value.PairKey.NullOrEmpty())
                .Select(value => value.Pressure).ToList();
            var sources = matches.Where(value => value.PairKey.NullOrEmpty())
                .ToList();
            foreach (IGrouping<string, CAIdeoligionSemanticAdapterDef> pair in
                matches.Where(value => !value.PairKey.NullOrEmpty())
                    .GroupBy(value => value.PairKey, StringComparer.Ordinal))
            {
                CAIdeoligionSemanticAdapterDef[] values = pair.ToArray();
                if (values.Length != 2
                    || values.Select(value => value.PairSide).Distinct(
                            StringComparer.Ordinal).Count() != 2
                    || values.Select(value => value.PairAggregation).Distinct()
                        .Count() != 1)
                    continue;
                CAIdeoligionSemanticAdapterDef male = values.FirstOrDefault(
                    value => value.PairSide == "male");
                CAIdeoligionSemanticAdapterDef female = values.FirstOrDefault(
                    value => value.PairSide == "female");
                if (male == null || female == null) continue;
                components.Add(CombinePair(values[0].PairAggregation,
                    male.Pressure, female.Pressure));
                sources.AddRange(values);
            }
            if (components.Count == 0)
                return new CAIdeoligionPressureProfile(0f, 0f, 0f, null);
            string source = string.Join(";", sources
                .Select(value => value.SourceToken)
                .Distinct(StringComparer.Ordinal));
            CAIdeoligionPressureSummary summary =
                CAIdeoligionPressureKernel.Aggregate(components);
            return new CAIdeoligionPressureProfile(summary.Center,
                summary.Intensity, summary.Conflict, source);
        }

        internal static float Pressure(Ideo ideo, string questionKey,
            out string source)
        {
            CAIdeoligionPressureProfile profile = Profile(ideo, questionKey);
            source = profile.Source;
            return profile.Center;
        }

        internal static float CombinePair(
            CAIdeoligionPairAggregation aggregation, float male,
            float female)
        {
            float result = aggregation switch
            {
                CAIdeoligionPairAggregation.Average =>
                    (male + female) * 0.5f,
                CAIdeoligionPairAggregation.FemaleMinusMale =>
                    female - male,
                _ => 0f
            };
            return Math.Max(-1f, Math.Min(1f, result));
        }

        private static string Identity(string packageId,
            CAIdeoligionSemanticDefKind kind, string defName) =>
            (packageId ?? "") + "|" + kind + "|" + (defName ?? "");

        private static IEnumerable<CAIdeoligionSemanticAdapterDef> Build()
        {
            var values = new List<CAIdeoligionSemanticAdapterDef>();

            Add(values, Core, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.HumanFleshAcceptance, -0.70f,
                "Cannibalism_Classic");
            Add(values, Core, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.HumanRemainsTreatment, 0.55f,
                "Corpses_Ugly");
            Add(values, Core, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.BodilyIntegrity, 0.65f,
                "OrganUse_Classic");
            Add(values, Core, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.CoercionLegitimacy, -0.55f,
                "Slavery_Classic");
            Add(values, Core, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.CaptiveProtection, 0.55f,
                "Execution_Classic");
            Add(values, Core, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.PunishmentSeverity, -0.45f,
                "Execution_Classic");

            Add(values, Ideology, CAIdeoligionSemanticDefKind.Meme,
                CACultureQuestionRegistry.GenderDistribution, -0.90f,
                "MaleSupremacy");
            Add(values, Ideology, CAIdeoligionSemanticDefKind.Meme,
                CACultureQuestionRegistry.GenderDistribution, 0.90f,
                "FemaleSupremacy");

            Pair(values, Core, CACultureQuestionRegistry.PluralityAcceptance,
                -0.70f, "spouse-count", "male",
                "SpouseCount_Male_MaxOne");
            Pair(values, Core, CACultureQuestionRegistry.PluralityAcceptance,
                -0.70f, "spouse-count", "female",
                "SpouseCount_Female_MaxOne");
            AddSpousePairs(values, "MaxTwo", 0.42f);
            AddSpousePairs(values, "MaxThree", 0.58f);
            AddSpousePairs(values, "MaxFour", 0.72f);
            AddSpousePairs(values, "Unlimited", 0.90f);
            AddSpouseGenderPair(values, "MaxOne", 0f);
            AddSpouseGenderPair(values, "MaxTwo", 0.35f);
            AddSpouseGenderPair(values, "MaxThree", 0.55f);
            AddSpouseGenderPair(values, "MaxFour", 0.70f);
            AddSpouseGenderPair(values, "Unlimited", 0.90f);

            Scale(values, Ideology, CACultureQuestionRegistry.CoercionLegitimacy,
                (-0.90f, "Slavery_Abhorrent"),
                (-0.65f, "Slavery_Horrible"),
                (-0.35f, "Slavery_Disapproved"),
                (0.35f, "Slavery_Acceptable"),
                (0.90f, "Slavery_Honorable"));
            Scale(values, Ideology, CACultureQuestionRegistry.CaptiveProtection,
                (0.90f, "Execution_Abhorrent"),
                (0.65f, "Execution_Horrible"),
                (0.35f, "Execution_HorribleIfInnocent"),
                (-0.35f, "Execution_DontCare"),
                (-0.55f, "Execution_RespectedIfGuilty"),
                (-0.90f, "Execution_Required"));
            Scale(values, Ideology, CACultureQuestionRegistry.PunishmentSeverity,
                (-0.75f, "Execution_Abhorrent"),
                (-0.50f, "Execution_Horrible"),
                (-0.20f, "Execution_HorribleIfInnocent"),
                (0.30f, "Execution_DontCare"),
                (0.55f, "Execution_RespectedIfGuilty"),
                (0.90f, "Execution_Required"));
            Scale(values, Ideology, CACultureQuestionRegistry.MutualProvision,
                (0.35f, "Charity_Worthwhile"),
                (0.65f, "Charity_Important"),
                (0.90f, "Charity_Essential"));
            Add(values, Core, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.SexualConduct, 0.60f,
                "Lovin_Free");
            Scale(values, Ideology, CACultureQuestionRegistry.SexualConduct,
                (-0.90f, "Lovin_Prohibited"),
                (-0.70f, "Lovin_Horrible"),
                (-0.48f, "Lovin_SpouseOnly_Strict"),
                (-0.34f, "Lovin_SpouseOnly_Moderate"),
                (-0.18f, "Lovin_SpouseOnly_Mild"),
                (0.75f, "Lovin_FreeApproved"));
            Scale(values, Core, CACultureQuestionRegistry.MarriageNaming,
                (-0.45f, "MarriageName_UsuallyMans"));
            Scale(values, Ideology, CACultureQuestionRegistry.MarriageNaming,
                (-0.90f, "MarriageName_AlwaysMans"),
                (0f, "MarriageName_Random"),
                (0f, "MarriageName_KeepNames"),
                (0.45f, "MarriageName_UsuallyWomans"),
                (0.90f, "MarriageName_AlwaysWomans"));
            Scale(values, Biotech, CACultureQuestionRegistry.ChildhoodProtection,
                (0.55f, "ChildLabor_Disapproved"),
                (-0.55f, "ChildLabor_Encouraged"));
            Scale(values, MorePrecepts, CACultureQuestionRegistry.AgeStanding,
                (0.90f, "Elderly_Revered"),
                (0.55f, "Elderly_Respected"),
                (-0.45f, "Elderly_Disrespected"),
                (-0.85f, "Elderly_Abhorred"));
            Scale(values, Ideology, CACultureQuestionRegistry.DoctrinalPluralism,
                (-0.90f, "IdeoDiversity_Abhorrent"),
                (-0.70f, "IdeoDiversity_Horrible"),
                (-0.45f, "IdeoDiversity_Disapproved"),
                (0f, "IdeoDiversity_Standard"),
                (0.35f, "IdeoDiversity_Approved"),
                (0.65f, "IdeoDiversity_Respected"),
                (0.90f, "IdeoDiversity_Exalted"),
                (-0.90f, "Apostasy_Abhorrent"),
                (-0.65f, "Apostasy_Horrible"),
                (-0.35f, "Apostasy_Disapproved"));
            Add(values, Biotech, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.XenotypeHierarchy, 0.70f,
                "PreferredXenotype");
            Scale(values, Ideology, CACultureQuestionRegistry.PredatoryAcquisition,
                (0.55f, "Raiding_Respected"),
                (0.90f, "Raiding_Required"));
            Scale(values, MorePrecepts,
                CACultureQuestionRegistry.PredatoryAcquisition,
                (-0.90f, "TakingFromDowned_Abhorrent"),
                (-0.65f, "TakingFromDowned_Horrible"),
                (-0.35f, "TakingFromDowned_Disapproved"));
            Scale(values, MorePrecepts, CACultureQuestionRegistry.OutsiderInclusion,
                (0.80f, "MP_NewcomerAttitude_Excited"),
                (0.30f, "MP_NewcomerAttitude_Careful"),
                (-0.35f, "MP_NewcomerAttitude_Wary"),
                (-0.75f, "MP_NewcomerAttitude_Fearful"));
            Scale(values, MorePrecepts, CACultureQuestionRegistry.ViolenceAcceptance,
                (-0.95f, "Violence_Pacifism"),
                (-0.75f, "Violence_Horrible"),
                (-0.45f, "Violence_Disapproved"),
                (-0.10f, "Violence_Avoided"),
                (0.10f, "Violence_Defense"),
                (0.25f, "Violence_DefenseMild"),
                (0.65f, "Violence_Wanted"),
                (0.95f, "Violence_Essential"));
            AddNudity(values, "Male", CACultureQuestionRegistry.MaleBodyExposure);
            AddNudity(values, "Female", CACultureQuestionRegistry.FemaleBodyExposure);
            Scale(values, Ideology, CACultureQuestionRegistry.BodilyAlteration,
                (-0.75f, "BodyMod_Abhorrent"),
                (-0.40f, "BodyMod_Disapproved"),
                (0.55f, "BodyMod_Approved"),
                (0.30f, "AgeReversal_Demanded"),
                (-0.65f, "Blinding_Horrible"),
                (0.35f, "Blindness_Respected"),
                (0.60f, "Blindness_Elevated"),
                (0.85f, "Blindness_Sublime"),
                (-0.65f, "Scarification_Horrible"),
                (0.25f, "Scarification_Minor"),
                (0.60f, "Scarification_Heavy"),
                (0.90f, "Scarification_Extreme"));
            Scale(values, Ideology, CACultureQuestionRegistry.BodilyIntegrity,
                (0.90f, "OrganUse_Abhorrent"),
                (0.65f, "OrganUse_HorribleNoSell"),
                (0.35f, "OrganUse_HorribleSellOK"),
                (-0.35f, "OrganUse_Acceptable"));
            Add(values, Ideology, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.PainMeaning, 0.90f,
                "Pain_Idealized");
            Scale(values, Ideology,
                CACultureQuestionRegistry.HumanRemainsTreatment,
                (-0.35f, "Corpses_DontCare"),
                (-0.55f, "Skullspike_Desired"),
                (0.45f, "Skullspike_Disapproved"));

            Scale(values, Ideology,
                CACultureQuestionRegistry.HumanFleshAcceptance,
                (-0.95f, "Cannibalism_Abhorrent"),
                (-0.75f, "Cannibalism_Horrible"),
                (-0.45f, "Cannibalism_Disapproved"),
                (0.25f, "Cannibalism_Acceptable"),
                (0.55f, "Cannibalism_Preferred"),
                (0.80f, "Cannibalism_RequiredStrong"),
                (0.95f, "Cannibalism_RequiredRavenous"));
            Scale(values, Ideology,
                CACultureQuestionRegistry.AnimalFoodAcceptance,
                (-0.90f, "MeatEating_Abhorrent"),
                (-0.65f, "MeatEating_Horrible"),
                (-0.35f, "MeatEating_Disapproved"),
                (0.90f, "MeatEating_NonMeat_Abhorrent"),
                (0.65f, "MeatEating_NonMeat_Horrible"),
                (0.35f, "MeatEating_NonMeat_Disapproved"),
                (0.65f, "Ranching_Central"));
            Scale(values, Ideology, CACultureQuestionRegistry.FoodAdaptability,
                (-0.55f, "FungusEating_Despised"),
                (0.55f, "FungusEating_Preferred"),
                (0.60f, "InsectMeatEating_Loved"),
                (0.35f, "NutrientPasteEating_DontMind"));
            Add(values, Core, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.FoodAdaptability, -0.45f,
                "InsectMeatEating_Despised_Classic",
                "NutrientPasteEating_Disgusting");
            Scale(values, Ideology,
                CACultureQuestionRegistry.RecreationalDrugUse,
                (-0.90f, "DrugUse_Prohibited"),
                (-0.55f, "DrugUse_MedicalOnly"),
                (-0.20f, "DrugUse_MedicalOrSocial"),
                (0.85f, "DrugUse_Essential"));
            Scale(values, MorePrecepts,
                CACultureQuestionRegistry.RecreationalDrugUse,
                (-0.90f, "Alcohol_Prohibited"),
                (-0.45f, "Alcohol_Disapproved"),
                (0f, "Alcohol_Neutral"),
                (0.55f, "Alcohol_Wanted"),
                (0.90f, "Alcohol_Essential"));

            Scale(values, Ideology, CACultureQuestionRegistry.AnimalMoralStanding,
                (0.75f, "AnimalSlaughter_Disapproved"),
                (0.90f, "AnimalSlaughter_Horrible"),
                (0.98f, "AnimalSlaughter_Prohibited"),
                (0.45f, "KillingInnocentAnimals_Disapproved"),
                (0.70f, "KillingInnocentAnimals_Horrible"),
                (0.90f, "KillingInnocentAnimals_Abhorrent"));
            Add(values, Ideology, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.AnimalMoralStanding, 0.75f,
                "AnimalConnection_Strong");
            Scale(values, Ideology, CACultureQuestionRegistry.ResourceStewardship,
                (0.35f, "TreeCutting_Disapproved"),
                (0.65f, "TreeCutting_Horrible"),
                (0.90f, "TreeCutting_Prohibited"),
                (0.35f, "Mining_Disapproved"),
                (0.65f, "Mining_Horrible"),
                (0.90f, "Mining_Prohibited"));
            Add(values, Odyssey, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.SettlementPermanence, -0.55f,
                "Nomadic_Preferred");
            Scale(values, MorePrecepts,
                CACultureQuestionRegistry.SettlementPermanence,
                (0.75f, "Nomadism_Horrible"),
                (0.45f, "Nomadism_Disapproved"),
                (-0.35f, "Nomadism_Wanted"),
                (-0.65f, "Nomadism_Important"),
                (-0.90f, "Nomadism_Essential"));
            Scale(values, Ideology, CACultureQuestionRegistry.ComfortExpectation,
                (-0.75f, "Comfort_Ignored"),
                (-0.55f, "RoughLiving_Welcomed"),
                (-0.25f, "SlabBed_Preferred"));
            Scale(values, MorePrecepts,
                CACultureQuestionRegistry.ComfortExpectation,
                (0.35f, "Comfort_Wanted"),
                (0.65f, "Comfort_Important"),
                (0.90f, "Comfort_Essential"));
            Add(values, Biotech, CAIdeoligionSemanticDefKind.Precept,
                CACultureQuestionRegistry.MachineDelegation, 0.75f,
                "MechanoidLabor_Enhanced");
            return values;
        }

        private static void AddSpousePairs(
            List<CAIdeoligionSemanticAdapterDef> values, string suffix,
            float pressure)
        {
            Pair(values, Ideology,
                CACultureQuestionRegistry.PluralityAcceptance, pressure,
                "spouse-count", "male", "SpouseCount_Male_" + suffix);
            Pair(values, Ideology,
                CACultureQuestionRegistry.PluralityAcceptance, pressure,
                "spouse-count", "female", "SpouseCount_Female_" + suffix);
        }

        private static void AddSpouseGenderPair(
            List<CAIdeoligionSemanticAdapterDef> values, string suffix,
            float allowance)
        {
            string packageId = suffix == "MaxOne" ? Core : Ideology;
            Pair(values, packageId,
                CACultureQuestionRegistry.GenderDistribution, allowance,
                "spouse-count-gender", "male",
                "SpouseCount_Male_" + suffix,
                CAIdeoligionPairAggregation.FemaleMinusMale);
            Pair(values, packageId,
                CACultureQuestionRegistry.GenderDistribution, allowance,
                "spouse-count-gender", "female",
                "SpouseCount_Female_" + suffix,
                CAIdeoligionPairAggregation.FemaleMinusMale);
        }

        private static void AddNudity(
            List<CAIdeoligionSemanticAdapterDef> values, string gender,
            string question)
        {
            Scale(values, Ideology, question,
                (0.95f, "Nudity_" + gender + "_Mandatory"),
                (0.65f, "Nudity_" + gender + "_CoveringAnythingButGroinDisapproved"),
                (0.55f, "Nudity_" + gender + "_NoRules"),
                (0.10f, "Nudity_" + gender + "_UncoveredGroinDisapproved"),
                (-0.25f, "Nudity_" + gender + "_UncoveredGroinOrChestDisapproved"),
                (-0.55f, "Nudity_" + gender + "_UncoveredGroinChestOrHairDisapproved"),
                (-0.85f, "Nudity_" + gender + "_UncoveredGroinChestHairOrFaceDisapproved"));
        }

        private static void Pair(List<CAIdeoligionSemanticAdapterDef> values,
            string packageId, string question, float pressure, string pairKey,
            string pairSide, string defName,
            CAIdeoligionPairAggregation aggregation =
                CAIdeoligionPairAggregation.Average)
        {
            values.Add(new CAIdeoligionSemanticAdapterDef
            {
                PackageId = packageId,
                Kind = CAIdeoligionSemanticDefKind.Precept,
                DefName = defName,
                QuestionKey = question,
                Pressure = pressure,
                PairKey = pairKey,
                PairSide = pairSide,
                PairAggregation = aggregation
            });
        }

        private static void Scale(List<CAIdeoligionSemanticAdapterDef> values,
            string packageId, string question,
            params (float Pressure, string DefName)[] points)
        {
            foreach ((float pressure, string defName) in points)
                Add(values, packageId, CAIdeoligionSemanticDefKind.Precept,
                    question, pressure, defName);
        }

        private static void Add(List<CAIdeoligionSemanticAdapterDef> values,
            string packageId, CAIdeoligionSemanticDefKind kind,
            string question, float pressure, params string[] defNames)
        {
            foreach (string defName in defNames)
                values.Add(new CAIdeoligionSemanticAdapterDef
                {
                    PackageId = packageId,
                    Kind = kind,
                    DefName = defName,
                    QuestionKey = question,
                    Pressure = pressure
                });
        }
    }

    // Native doctrine is evidence and pressure, never a replacement for the
    // Culture distribution. Native precepts, memes, roles, and rituals remain
    // Ideoligion-owned and continue to execute through RimWorld.
    internal static class CACultureIdeoligionAdapter
    {
        internal static CAIdeoligionPressureProfile Profile(Ideo ideo,
            string questionKey) => CAIdeoligionSemanticAdapterRegistry.Profile(
                ideo, questionKey);

        internal static float Pressure(Ideo ideo, string questionKey,
            out string source) => CAIdeoligionSemanticAdapterRegistry.Pressure(
                ideo, questionKey, out source);
    }
}
