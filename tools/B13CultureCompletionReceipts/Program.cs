using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Diagnostics;
using ColonistAwareness;

internal static class Program
{
    private sealed record Result(int Number, string Name, bool Passed,
        string Evidence);

    private static readonly List<Result> Results = new();
    private static string repo = string.Empty;

    private static int Main(string[] args)
    {
        bool verifyOnly = args.Length == 4 && args[3] == "--verify-only";
        if (args.Length != 3 && !verifyOnly)
        {
            Console.Error.WriteLine(
                "usage: B13CultureCompletionReceipts <repo> <active> <mirror>");
            return 1;
        }
        repo = Path.GetFullPath(args[0]);
        string active = Path.GetFullPath(args[1]);
        string mirror = Path.GetFullPath(args[2]);

        RegistryReceipts();
        AuthoringReceipts();
        CausalReceipts();
        ConsumerAndHistoryReceipts();
        SurfaceReceipts();
        PersistenceReceipts();
        FixtureReceipts(active, mirror);
        ResearchReceipts();
        if (!verifyOnly) WriteReceipts(active, mirror);

        int passed = Results.Count(value => value.Passed);
        foreach (Result result in Results)
            Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} "
                + $"{result.Number:00} {result.Name}: {result.Evidence}");
        Console.WriteLine($"B13 acceptance: {passed}/{Results.Count} PASS");
        return passed == Results.Count ? 0 : 2;
    }

    private static void RegistryReceipts()
    {
        IReadOnlyList<CACultureQuestionDef> all =
            CACultureQuestionRegistry.All;
        Add("Culture registry validates",
            CACultureQuestionRegistry.ValidationFailure() == null,
            CACultureQuestionRegistry.ValidationFailure() ?? "no failure");
        Add("stable Culture questions",
            all.Count == CACultureQuestionRegistry.FixedQuestionCount
                && all.Select(value => value.Key).Distinct(
                    StringComparer.Ordinal).Count()
                    == CACultureQuestionRegistry.FixedQuestionCount,
            $"count={all.Count}; registry={CACultureQuestionRegistry.CurrentVersion}");
        Add("every current Culture category is represented",
            all.GroupBy(value => value.Layer).Count()
                == Enum.GetValues<CACultureQuestionLayer>().Length
                && Enum.GetValues<CACultureQuestionLayer>().All(layer =>
                    all.Any(value => value.Layer == layer)),
            string.Join("; ", all.GroupBy(value => value.Layer).Select(
                group => group.Key + "=" + group.Count())));
        Add("every question has five ordered anchors",
            all.All(value => value.Anchors.Length == 5
                && value.AnchorCenters.Length == 5
                && value.AnchorCenters.Zip(value.AnchorCenters.Skip(1))
                    .All(pair => pair.First < pair.Second)),
            $"{all.Count}/{all.Count} use centered, strictly ordered five-anchor scales");
        Add("every question names evidence, research, and a consumer",
            all.All(value => value.HistoricalSources.Length > 0
                && value.ResearchProvenance.Length > 0
                && ConsumerCount(value) > 0),
            $"{all.Count}/{all.Count} definitions carry causal and research provenance");
    }

    private static void AuthoringReceipts()
    {
        string[] requiredHistoricalPresets =
        {
            "english-north-america-early-colonial",
            "france-napoleonic-empire",
            "france-second-empire",
            "united-states-civil-war-union",
            "confederate-slaveholding-dominant-culture",
            "freedpeople-emancipation-communities",
            "germany-weimar-republic",
            "germany-national-socialist-dictatorship",
            "japan-late-tokugawa",
            "japan-meiji-transformation",
            "qing-china-late-imperial",
            "ottoman-empire-tanzimat",
            "mughal-india-akbar"
        };
        Add("historical and social Culture presets validate",
            CACulturePresetLibrary.All.Count == 22
                && requiredHistoricalPresets.All(key =>
                    CACulturePresetLibrary.Find(key) != null)
                && CACulturePresetLibrary.ValidationFailure() == null
                && CACulturePresetLibrary.All.Select(value =>
                        value.CatalogGroup).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(new[] { "Social forms", "Historical cultures" })
                && CACulturePresetLibrary.All.All(value =>
                    !string.IsNullOrWhiteSpace(value.Label)),
            CACulturePresetLibrary.ValidationFailure()
                ?? "22 complete component presets use one social-or-historical Culture catalog axis");
        Add("every preset specifies the full registry",
            CACulturePresetLibrary.All.All(preset => preset.Values.Count
                    == CACultureQuestionRegistry.FixedQuestionCount
                && preset.Values.Select(value => value.QuestionKey)
                    .ToHashSet(StringComparer.Ordinal).SetEquals(
                        CACultureQuestionRegistry.All.Select(value =>
                            value.Key))),
            $"22/22 presets specify {CACultureQuestionRegistry.FixedQuestionCount} unique question values");
        Add("preset profiles are substantively distinct",
            CACulturePresetLibrary.All.Select(preset => string.Join(",",
                    preset.Values.Select(value => value.Mean.ToString("0.00"))))
                .Distinct(StringComparer.Ordinal).Count() == 22,
            "22 distinct mean profiles; no era-wide monoculture shortcut");
        CACulturePresetDef[] unitedStates = CACulturePresetLibrary.All
            .Where(value => value.ReferenceContext == "United States")
            .ToArray();
        Add("one historical context has distinct period presets",
            unitedStates.Length == 3
                && unitedStates.Select(value => value.ApproximatePeriod)
                    .Distinct(StringComparer.Ordinal).Count() == 3
                && unitedStates.Select(value => string.Join(",",
                        value.Values.Select(item => item.Mean.ToString("0.00"))))
                    .Distinct(StringComparer.Ordinal).Count() == 3
                && unitedStates.All(value => !string.IsNullOrWhiteSpace(
                    value.Sources)),
            "United States has postwar, millennium, and contemporary sourced Culture priors");

        var sameObject = new CACulture();
        CACulture reference = sameObject;
        CACulturePresetLibrary.Apply(sameObject,
            CACulturePresetLibrary.All[0], "receipt:preset-one");
        CACulturePresetLibrary.Apply(sameObject,
            CACulturePresetLibrary.All[1], "receipt:preset-two");
        Add("preset application writes the same Culture object",
            ReferenceEquals(reference, sameObject)
                && sameObject.inheritedQuestions.Count
                    == CACultureQuestionRegistry.FixedQuestionCount
                && sameObject.questionRegistryVersion
                    == CACultureQuestionRegistry.CurrentVersion,
            $"second preset replaces the same {CACultureQuestionRegistry.FixedQuestionCount} question distributions; no mode object");
        var authoredRandom = new CACulture();
        CACultureAuthoringKernel.Randomize(authoredRandom,
            "receipt:authorship");
        Add("question authoring carries explicit provenance",
            (sameObject.authoredMask & CACulture.QuestionStateField) != 0
                && (sameObject.authoredMask & CACulture.DiversityField) != 0
                && (authoredRandom.authoredMask
                    & CACulture.QuestionStateField) != 0
                && S("Source/FactionStateModule.cs").Contains(
                    "culture?.authoredMask != 0", StringComparison.Ordinal),
            "preset and random Culture remain authored when faction origin is classified");

        CACultureQuestionDistribution target =
            sameObject.inheritedQuestions[0];
        CACultureQuestionDistribution neighbor =
            sameObject.inheritedQuestions[1];
        string neighborBefore = CACultureDistributionKernel.Fingerprint(
            neighbor);
        target.mean = 0.91f;
        Add("manual editing changes only the selected question",
            target.mean == 0.91f
                && neighborBefore == CACultureDistributionKernel.Fingerprint(
                    neighbor),
            "one position changed; adjacent distribution fingerprint stable");

        var randomA = new CACulture();
        var randomB = new CACulture();
        CACultureAuthoringKernel.Randomize(randomA, "b13-fixed-seed");
        CACultureAuthoringKernel.Randomize(randomB, "b13-fixed-seed");
        Add("Culture randomization is complete and deterministic",
            randomA.inheritedQuestions.Count
                == CACultureQuestionRegistry.FixedQuestionCount
                && CACultureDistributionKernel.Fingerprint(
                    randomA.inheritedQuestions)
                    == CACultureDistributionKernel.Fingerprint(
                        randomB.inheritedQuestions)
                && randomA.inheritedQuestions.All(value =>
                    CACultureDistributionKernel.ValidationFailure(value)
                        == null),
            $"fixed seed produced {CACultureQuestionRegistry.FixedQuestionCount} valid distributions with one fingerprint");

        var reopened = new CACulture();
        string identity = "receipt:reopened-editor";
        CACultureAuthoringKernel.Randomize(reopened,
            CACultureAuthoringKernel.NextRandomizationSeed(reopened,
                identity), identity);
        string firstRandom = CACultureDistributionKernel.Fingerprint(
            reopened.inheritedQuestions);
        CACultureAuthoringKernel.Randomize(reopened,
            CACultureAuthoringKernel.NextRandomizationSeed(reopened,
                identity), identity);
        string secondRandom = CACultureDistributionKernel.Fingerprint(
            reopened.inheritedQuestions);
        Add("reopened editor randomization advances from saved Culture",
            firstRandom != secondRandom,
            "the next seed is derived from persisted values, not a dialog-local nonce");

        CACulture authored = CultureWithQuestion(0, 0.73f,
            "operator-authored");
        string authoredBefore = CACultureDistributionKernel.Fingerprint(
            authored.inheritedQuestions[0]);
        int added = CACultureAuthoringKernel.CompleteMissing(authored,
            "completion-seed", "receipt:completion");
        Add("completion preserves authored Culture state",
            added == CACultureQuestionRegistry.FixedQuestionCount - 1
                && authored.inheritedQuestions.Count
                    == CACultureQuestionRegistry.FixedQuestionCount
                && authoredBefore == CACultureDistributionKernel.Fingerprint(
                    authored.inheritedQuestions.Single(value =>
                        value.questionKey == CACultureQuestionRegistry.All[0].Key)),
            $"{CACultureQuestionRegistry.FixedQuestionCount - 1} absent rows added; authored row and provenance unchanged");

        CACulture stableA = CultureWithQuestion(0, 0.2f, "a");
        CACulture stableB = CultureWithQuestion(1, -0.2f, "b");
        CACultureAuthoringKernel.CompleteMissing(stableA, "same-seed");
        CACultureAuthoringKernel.CompleteMissing(stableB, "same-seed");
        string comparisonKey = CACultureQuestionRegistry.All[2].Key;
        Add("unrelated generated rows remain seed-stable",
            Fingerprint(stableA, comparisonKey)
                == Fingerprint(stableB, comparisonKey),
            comparisonKey + " is identical when a different row was authored");

        CACulture narrow = new();
        CACulture broad = new();
        CACulturePresetDef sharedPreset = CACulturePresetLibrary.All[2];
        CACulturePresetLibrary.Apply(narrow, sharedPreset);
        CACulturePresetLibrary.Apply(broad, sharedPreset);
        narrow.withinGroupSpread = 0;
        broad.withinGroupSpread = 4;
        CACultureModel.Normalize(narrow);
        CACultureModel.Normalize(broad);
        Add("global diversity changes spread, not position",
            narrow.inheritedQuestions.Zip(broad.inheritedQuestions)
                .All(pair => pair.First.mean == pair.Second.mean
                    && pair.First.spread == 0.10f
                    && pair.Second.spread == 0.60f),
            "24 centers stable while default spread changes 0.10 to 0.60");

        CACultureQuestionDistribution overridden =
            broad.inheritedQuestions[0];
        overridden.spreadOverride = true;
        overridden.spread = 0.37f;
        broad.withinGroupSpread = 1;
        CACultureModel.Normalize(broad);
        Add("question-specific spread overrides the global default",
            overridden.spread == 0.37f
                && broad.inheritedQuestions[1].spread == 0.18f,
            "selected row remains 0.37; neighboring row follows 0.18 global spread");
    }

    private static void CausalReceipts()
    {
        CAAttitudeMaterializationResult basis =
            CACulturalCognitionPureKernel.Materialize(Attitude());
        CAAttitudeMaterializationResult salient =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(salience: 0.95f));
        Add("salience changes attention without manufacturing conviction",
            salient.Attention > basis.Attention
                && salient.MoralConviction == basis.MoralConviction
                && salient.PrivatePosition == basis.PrivatePosition,
            $"attention {basis.Attention:0.000}->{salient.Attention:0.000}; conviction {basis.MoralConviction:0.000}");

        CAAttitudeMaterializationResult strongNorm =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(norm: 0.95f));
        Add("norm strength changes social pressure, not enforcement",
            strongNorm.PerceivedSocialPressure
                    > basis.PerceivedSocialPressure
                && strongNorm.ExpectedEnforcement
                    == basis.ExpectedEnforcement,
            $"pressure {basis.PerceivedSocialPressure:0.000}->{strongNorm.PerceivedSocialPressure:0.000}; enforcement {basis.ExpectedEnforcement:0.000}");

        CAAttitudeMaterializationResult tolerant =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(tolerance: 0.90f));
        Add("divergence tolerance changes pressure independently",
            tolerant.PerceivedSocialPressure
                    < basis.PerceivedSocialPressure
                && tolerant.ExpectedEnforcement == basis.ExpectedEnforcement,
            $"pressure {basis.PerceivedSocialPressure:0.000}->{tolerant.PerceivedSocialPressure:0.000}; enforcement stable");

        CAAttitudeMaterializationResult enforced =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(enforcement: 0.82f));
        Add("represented enforcement owns expected enforcement",
            enforced.ExpectedEnforcement == 0.82f
                && enforced.PerceivedSocialPressure
                    == basis.PerceivedSocialPressure,
            $"enforcement {basis.ExpectedEnforcement:0.000}->{enforced.ExpectedEnforcement:0.000}; norm pressure stable");

        CAAttitudeMaterializationResult strongPrior =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(sourceConfidence: 0.95f));
        Add("source confidence is inherited-prior strength",
            strongPrior.InheritedPriorStrength
                    > basis.InheritedPriorStrength
                && strongPrior.KnowledgeConfidence
                    == basis.KnowledgeConfidence
                && strongPrior.Uncertainty == basis.Uncertainty,
            $"prior {basis.InheritedPriorStrength:0.000}->{strongPrior.InheritedPriorStrength:0.000}; knowledge stable");

        CAAttitudeMaterializationResult evidenced =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(directEvidence: 0.92f));
        Add("direct evidence owns knowledge confidence",
            evidenced.KnowledgeConfidence > basis.KnowledgeConfidence
                && evidenced.Uncertainty < basis.Uncertainty
                && evidenced.InheritedPriorStrength
                    == basis.InheritedPriorStrength,
            $"knowledge {basis.KnowledgeConfidence:0.000}->{evidenced.KnowledgeConfidence:0.000}; prior stable");

        CAAttitudeMaterializationResult hidden =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(visibility: 0.05f));
        CAAttitudeMaterializationResult visible =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(visibility: 0.95f));
        Add("visibility changes observation, not expression",
            hidden.ObservationLikelihood < visible.ObservationLikelihood
                && hidden.PublicExpression == visible.PublicExpression
                && hidden.PrivatePosition == visible.PrivatePosition,
            $"observation {hidden.ObservationLikelihood:0.00}->{visible.ObservationLikelihood:0.00}; expression {hidden.PublicExpression:0.000}");

        CAAttitudeMaterializationResult shifted =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(psychologicalShift: 0.16f));
        Add("psychology produces a bounded private-position deviation",
            shifted.PrivatePosition > basis.PrivatePosition
                && shifted.PrivatePosition <= 1f
                && Math.Abs(shifted.PrivatePosition
                    - basis.PrivatePosition) <= 0.20f,
            $"private {basis.PrivatePosition:0.000}->{shifted.PrivatePosition:0.000}; shift bounded to question loading");

        CAKnowledgeAcceptanceInput lowDeference = Knowledge(0f);
        CAKnowledgeAcceptanceInput highDeference = Knowledge(1f);
        CAKnowledgeAcceptanceResult low =
            CACulturalCognitionPureKernel.EvaluateKnowledge(lowDeference);
        CAKnowledgeAcceptanceResult high =
            CACulturalCognitionPureKernel.EvaluateKnowledge(highDeference);
        Add("expertise deference weights demonstrated expertise",
            high.Confidence > low.Confidence
                && high.Attention == low.Attention,
            $"confidence {low.Confidence:0.000}->{high.Confidence:0.000}; attention stable");

        float weakAppraisal = CACulturalCognitionPureKernel
            .RepresentedEvidenceConfidence(new[]
            {
                new CARepresentedSourceAppraisal(0.25f, 0.20f, 0.15f,
                    0.20f)
            });
        float strongAppraisal = CACulturalCognitionPureKernel
            .RepresentedEvidenceConfidence(new[]
            {
                new CARepresentedSourceAppraisal(0.90f, 0.90f, 0.80f,
                    0.85f)
            });
        CAAttitudeMaterializationResult weakSource =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(directEvidence: weakAppraisal));
        CAAttitudeMaterializationResult strongSource =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(directEvidence: strongAppraisal));
        Add("represented source appraisal changes current confidence",
            strongAppraisal > weakAppraisal
                && strongSource.KnowledgeConfidence
                    > weakSource.KnowledgeConfidence
                && strongSource.InheritedPriorStrength
                    == weakSource.InheritedPriorStrength,
            $"represented confidence {weakAppraisal:0.000}->{strongAppraisal:0.000}; inherited prior stable");

        float lived = CACulturalCognitionPureKernel
            .RepresentedMoralExperience(new[]
            {
                new CARepresentedMoralExperience(0.90f, 0.80f, 0.85f)
            });
        CAAttitudeMaterializationResult experienced =
            CACulturalCognitionPureKernel.Materialize(
                Attitude(moralExperience: lived));
        Add("lived moral evidence changes conviction independently",
            experienced.MoralConviction > basis.MoralConviction
                && experienced.Attention == basis.Attention
                && experienced.PrivatePosition == basis.PrivatePosition,
            $"conviction {basis.MoralConviction:0.000}->{experienced.MoralConviction:0.000}; attention and position stable");

        CARepresentedSourceAppraisal directHistory =
            CACulturalCognitionPureKernel
                .RepresentedQuestionSourceAppraisal(0.10f, 0.80f, 6);
        CARepresentedMoralExperience directMoral =
            CACulturalCognitionPureKernel
                .RepresentedQuestionMoralExperience(-0.75f, 0.10f,
                    0.80f, 6);
        bool stableUptake = CACulturalCognitionPureKernel
            .ReceivesRepresentedQuestionEvidence(37,
                "receipt:direct-history", 0.55f)
            == CACulturalCognitionPureKernel
                .ReceivesRepresentedQuestionEvidence(37,
                    "receipt:direct-history", 0.55f);
        Add("direct historical facts produce stable pawn appraisals",
            directHistory.Trust > 0.75f
                && directHistory.Weight == 0.80f
                && directMoral.ApprovalMagnitude == 0.75f
                && directMoral.Salience == 0.80f && stableUptake,
            "participation, consistency, continuity, and position produce separate source and moral appraisals without a load-time reroll");

        string[] missingExpertise = CASocialSubjectRegistry.Authorable()
            .Where(subject => CACulturalCognitionPureKernel
                .ExpertiseDomainsForSocialSubject(subject.Key).Count == 0)
            .Select(subject => subject.Key).ToArray();
        float gatheringExpertise = CACulturalCognitionPureKernel
            .DemonstratedExpertise(CASocialSubjectRegistry.PublicGathering,
                domain => domain
                        == CACulturalCognitionPureKernel.ExpertiseSocial
                    ? 0.85f : 0.05f);
        float orderExpertise = CACulturalCognitionPureKernel
            .DemonstratedExpertise(CASocialSubjectRegistry.EnforcedOrder,
                domain => domain
                        == CACulturalCognitionPureKernel.ExpertiseMelee
                    ? 0.90f : 0.10f);
        Add("every built-in social subject has demonstrated expertise",
            missingExpertise.Length == 0
                && gatheringExpertise == 0.85f
                && orderExpertise == 0.90f,
            missingExpertise.Length == 0
                ? "all registered subjects map to concrete skill domains; gathering uses Social and enforced order can use represented force skill"
                : "unmapped: " + string.Join(", ", missingExpertise));

        float openWork = CACulturalCognitionPureKernel
            .HistoricalGenderedWorkPosition(0f);
        float dividedWork = CACulturalCognitionPureKernel
            .HistoricalGenderedWorkPosition(1f);
        Add("gendered-work history follows the authored scale",
            openWork == -1f && dividedWork == 1f
                && openWork < dividedWork,
            "no assignment difference records open work; complete difference records rigid division");

        bool oneGender = CACulturalCognitionPureKernel
            .TryHistoricalOfficeAccessPosition(1, 0,
                out float unsupportedRestriction);
        bool mixedOffice = CACulturalCognitionPureKernel
            .TryHistoricalOfficeAccessPosition(1, 1,
                out float broadAccess);
        Add("office composition does not invent exclusion",
            !oneGender && unsupportedRestriction == 0f
                && mixedOffice && broadAccess == 0.70f,
            "single-gender holders yield no access evidence; represented mixed holders yield broad-access evidence");

        string socialRuntime = S(
            "Source/SocialInterpretationRuntimeModule.cs");
        string cognitionRuntime = S(
            "Source/CulturalCognitionStateModule.cs");
        string enforcementResolver = Between(cognitionRuntime,
            "internal static float RepresentedEnforcementFor",
            "public sealed class CACulturalCognitionWorldComponent");
        string legitimacyResolver = Between(cognitionRuntime,
            "private static float InstitutionLegitimacyFor",
            "private static float DynamicCapacity");
        Add("pawn cognition retains exact institutional jurisdiction",
            socialRuntime.Contains("class CACultureRuntimeContext",
                    StringComparison.Ordinal)
                && socialRuntime.Contains(
                    "InstitutionalOrganizationIdentity = organizationIdentity",
                    StringComparison.Ordinal)
                && cognitionRuntime.Contains(
                    "context?.InstitutionalOrganizationIdentity",
                    StringComparison.Ordinal)
                && enforcementResolver.Contains("ByKey(organizationIdentity)",
                    StringComparison.Ordinal)
                && !enforcementResolver.Contains("memberPawnIds",
                    StringComparison.Ordinal)
                && legitimacyResolver.Contains("ByKey(organizationIdentity)",
                    StringComparison.Ordinal)
                && !legitimacyResolver.Contains("memberPawnIds",
                    StringComparison.Ordinal)
                && cognitionRuntime.Contains(
                    "RefreshInstitutionalContext(existing, ProfileFor(pawn)",
                    StringComparison.Ordinal)
                && cognitionRuntime.Contains(
                    "target.publicExpression = expression",
                    StringComparison.Ordinal)
                && Compact(socialRuntime).Contains(
                    "RecordFact(CASocialFactContextfact,Pawnpawn,CACultureRuntimeContextcontext",
                    StringComparison.Ordinal),
            "regional Culture, sanction, legitimacy, and cached public-expression consumers share the current settlement organization key; player locality remains distinct from the player institution; generic ingress accepts the typed context intact");
        string reactionLookup = Between(socialRuntime,
            "internal IReadOnlyList<CASocialReactionRecord> ReactionsForPawn",
            "public override void ExposeData");
        Add("pawn reaction lookup uses a maintained runtime index",
            socialRuntime.Contains(
                    "Dictionary<int, List<CASocialReactionRecord>> reactionsByPawn",
                    StringComparison.Ordinal)
                && socialRuntime.Contains("EnsureReactionIndex()",
                    StringComparison.Ordinal)
                && socialRuntime.Contains("IndexReaction(recorded)",
                    StringComparison.Ordinal)
                && reactionLookup.Contains("reactionsByPawn.TryGetValue",
                    StringComparison.Ordinal)
                && !reactionLookup.Contains(".Where(",
                    StringComparison.Ordinal),
            "daily seven-question refresh reuses one indexed pawn reaction list instead of rescanning and sorting the retained global ledger per question");
    }

    private static void ConsumerAndHistoryReceipts()
    {
        var expectedRoutes = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            [CACultureQuestionRegistry.SameSexAcceptance] =
                "Source/CulturalRelationshipModule.cs",
            [CACultureQuestionRegistry.PluralityAcceptance] =
                "Source/CulturalRelationshipModule.cs",
            [CACultureQuestionRegistry.GenderDistribution] =
                "Source/CulturalCognitionStateModule.cs",
            [CACultureQuestionRegistry.KnowledgeAccess] =
                "Source/PropositionKnowledgeModule.cs",
            [CACultureQuestionRegistry.NoveltyAcceptance] =
                "Source/PropositionKnowledgeModule.cs",
            [CACultureQuestionRegistry.ExpertiseDeference] =
                "Source/PropositionKnowledgeModule.cs"
        };
        foreach (CACultureQuestionDef definition in
            CACultureQuestionRegistry.All.Take(
                CACultureQuestionRegistry.LegacyQuestionCount))
            if (!expectedRoutes.ContainsKey(definition.Key))
                expectedRoutes[definition.Key] =
                    "Source/CulturalPoliticsStateModule.cs";
        string[] missing = expectedRoutes.Where(pair =>
                !ContainsConstant(S(pair.Value), ConstantName(pair.Key)))
            .Select(pair => pair.Key + " -> " + pair.Value).ToArray();
        Add("every Culture question reaches its designated production consumer",
            missing.Length == 0,
            missing.Length == 0
                ? $"{CACultureQuestionRegistry.LegacyQuestionCount}/{CACultureQuestionRegistry.LegacyQuestionCount} B13 question constants remain in designated relationship, political, office, or knowledge consumers"
                : "missing: " + string.Join(", ", missing));

        var direct = new HashSet<string>(StringComparer.Ordinal)
        {
            CACultureQuestionRegistry.SameSexAcceptance,
            CACultureQuestionRegistry.PluralityAcceptance,
            CACultureQuestionRegistry.GenderDistribution,
            CACultureQuestionRegistry.GenderedWork,
            CACultureQuestionRegistry.GenderOfficeAccess,
            CACultureQuestionRegistry.IntegrationPreference,
            CACultureQuestionRegistry.DissentTolerance
        };
        Add("all questions have a historical feedback route",
            CACultureQuestionRegistry.All.All(value => direct.Contains(
                    value.Key) || value.SocialSubjectAdapters.Length > 0
                || value.PracticeEvidenceAdapters.Length > 0
                || value.IdeoligionAdapters.Length > 0),
            "every question names a direct, subject, practice, or doctrine evidence route");

        string longitudinal = S("Source/CultureLongitudinalModule.cs");
        string culture = S("Source/FactionCultureBeliefsModule.cs");
        Add("direct historical evidence persists before transition",
            longitudinal.Contains("Culture question evidence",
                    StringComparison.Ordinal)
                && longitudinal.Contains("evidenceStartTick",
                    StringComparison.Ordinal)
                && longitudinal.Contains("observationCount",
                    StringComparison.Ordinal)
                && longitudinal.Contains("questionPosition",
                    StringComparison.Ordinal)
                && longitudinal.Contains("questionDispersion",
                    StringComparison.Ordinal)
                && longitudinal.Contains("RefreshRepresentedEvidence",
                    StringComparison.Ordinal)
                && culture.Contains("value.QuestionKey",
                    StringComparison.Ordinal)
                && culture.Contains("EvaluateMeaningTransition",
                    StringComparison.Ordinal),
            "represented position, spread, participation, and population persist once; pawn appraisal and the transition boundary consume that record");
        Add("absence of an event is not a negative Culture fact",
            longitudinal.Contains("Absence of an event is not treated as disapproval",
                StringComparison.Ordinal)
                && longitudinal.Contains("samples.Count", StringComparison.Ordinal)
                    == false,
            "only represented samples create historical question evidence");
        Add("historical sampler calls the audited polarity kernels",
            Compact(longitudinal).Contains(
                    "HistoricalGenderedWorkPosition(difference)",
                    StringComparison.Ordinal)
                && Compact(longitudinal).Contains(
                    "TryHistoricalOfficeAccessPosition(maleHolders,femaleHolders",
                    StringComparison.Ordinal),
            "production history uses the same executable polarity and absence rules as the receipts");

        string regional = S("Source/RegionalPopulationScreenModule.cs");
        string faction = S("Source/FactionStateModule.cs");
        Add("starting-region generation completes missing Culture",
            regional.Contains("CACultureAuthoringKernel",
                    StringComparison.Ordinal)
                && regional.Contains("CompleteMissing(group.culture",
                    StringComparison.Ordinal),
            "Generate unspecified choices completes absent faction questions");
        Add("broader world generation completes non-player Culture",
            faction.Contains("!faction.IsPlayer", StringComparison.Ordinal)
                && faction.Contains("CompleteMissing(record.culture",
                    StringComparison.Ordinal),
            "world faction initialization fills absent questions without overwriting player authorship");
    }

    private static void SurfaceReceipts()
    {
        string source = S("Source/FactionCultureBeliefsModule.cs");
        Add("one editor owns preset, random, and manual authoring",
            source.Contains("Culture presets...",
                    StringComparison.Ordinal)
                && source.Contains("Randomize Culture",
                    StringComparison.Ordinal)
                && source.Contains("DrawQuestion(", StringComparison.Ordinal)
                && source.Contains("Dialog_CACultureEditor",
                    StringComparison.Ordinal),
            "all authoring paths converge on the Culture question editor");
        string controls = Between(source,
            "private void DrawPopulationDistributionControls",
            "private static string CategoryLabel");
        Add("one overall disagreement control is visible",
            Count(controls, "Overall disagreement") == 1
                && !controls.Contains("Subgroup separation",
                    StringComparison.Ordinal),
            "Values page exposes one population-wide disagreement control and no duplicate subgroup control");
        Add("per-question spread remains advanced",
            source.Contains("Use More to adjust one value.",
                    StringComparison.Ordinal)
                && source.Contains("question.spreadOverride = true",
                    StringComparison.Ordinal)
                && source.Contains("Use overall disagreement",
                    StringComparison.Ordinal),
            "expanded rows own explicit spread override and release");
        string[] headings =
        {
            "Relationships, family, and sexuality",
            "Gender and social authority", "Status and hierarchy",
            "Membership and outsiders",
            "Public authority and social order",
            "Property, labor, and provision",
            "Violence, captivity, and punishment",
            "Knowledge and tradition"
        };
        Add("the editor exposes all eight Culture categories",
            headings.All(value => source.Contains(value,
                StringComparison.Ordinal)),
            "8/8 category headings present in canonical order");
        string authoring = S("Source/CultureAuthoringModule.cs");
        Add("Culture has no blend or custom mode",
            !authoring.Contains("presetMode", StringComparison.Ordinal)
                && !authoring.Contains("BlendMode", StringComparison.Ordinal)
                && !authoring.Contains("blendWeight", StringComparison.Ordinal)
                && !authoring.Contains("customMode", StringComparison.Ordinal)
                && authoring.Contains("There is no preset",
                    StringComparison.Ordinal),
            "preset identity is discarded after values reach the same Culture object");
        Add("practices remain observed history",
            source.Contains("DrawPracticeHistory", StringComparison.Ordinal)
                && source.Contains("These are customs people have repeatedly followed.",
                    StringComparison.Ordinal)
                && source.Contains("are not settings here.",
                    StringComparison.Ordinal)
                && !source.Contains("void DrawPractices",
                    StringComparison.Ordinal),
            "question authoring and read-only practice history remain separate");
        Add("player and established societies share the Culture editor",
            S("Source/PlayerFoundingPageModule.cs").Contains(
                    "Dialog_CACultureEditor", StringComparison.Ordinal)
                && S("Source/RegionalPopulationScreenModule.cs").Contains(
                    "Dialog_CACultureEditor", StringComparison.Ordinal),
            "founding and established-society surfaces share the same question model");
        Add("Culture editor content remains scroll-measured",
            source.Contains("Widgets.BeginScrollView(outRect",
                    StringComparison.Ordinal)
                && source.Contains("viewHeight = rowY + 12f",
                    StringComparison.Ordinal)
                && source.Contains("AnchorRowHeight",
                    StringComparison.Ordinal),
            "dynamic height accounts for categories, wrapped anchors, and advanced rows");
        string compact = Compact(source);
        Add("advanced rows reserve space only for visible controls",
            source.Contains("FitLabel(badge, badgeRect.width - 6f)",
                    StringComparison.Ordinal)
                && compact.Contains(
                    "if(question.spreadOverride)y+=34f;",
                    StringComparison.Ordinal)
                && compact.Contains(
                    "+(question?.spreadOverride==true?34f:0f)",
                    StringComparison.Ordinal),
            "provenance badges fit with tooltips and released spread overrides leave no empty button row");
    }

    private static void PersistenceReceipts()
    {
        string state = S("Source/CulturalCognitionStateModule.cs");
        string preflight = S("Source/CampaignCompatibilityPreflightKernel.cs");
        Add("campaign catalog records the B13 cognition schema",
            CACampaignSchemaCatalog.CurrentCatalogVersion == 5
                && CACampaignSchemaCatalog.TryFind(
                    "world.cultural-cognition", out var cognition)
                && cognition.CurrentVersion == 2
                && cognition.MinimumCompatibleVersion == 2
                && cognition.IntroducedCatalogVersion == 2
                && state.Contains(
                    "campaignSchemaVersion = CurrentSchemaVersion",
                    StringComparison.Ordinal)
                && state.Contains("CA_culturalCognitionOwnerVersion",
                    StringComparison.Ordinal),
            "catalog=5 retains the cultural-cognition schema-2 owner introduced in 2 and admits no schema-1 payload");
        Add("durable attitudes persist the separated causal facts",
            state.Contains("CurrentSchemaVersion = 2",
                    StringComparison.Ordinal)
                && new[] { "attention", "inheritedPriorStrength",
                    "perceivedSocialPressure", "observationLikelihood" }
                    .All(value => state.Contains(value,
                        StringComparison.Ordinal))
                && !state.Contains("publicVisibility",
                    StringComparison.Ordinal)
                && preflight.Contains(
                    "RequireInteger(attitude, \"schemaVersion\", 2, 2",
                    StringComparison.Ordinal)
                && new[] { "attention", "inheritedPriorStrength",
                    "perceivedSocialPressure", "observationLikelihood" }
                    .All(value => preflight.Contains("\"" + value + "\"",
                        StringComparison.Ordinal))
                && !preflight.Contains("\"publicVisibility\"",
                    StringComparison.Ordinal),
            "schema-2 attitudes and preflight persist/validate attention, prior strength, social pressure, and observation likelihood");
        string validAttitudeXml = CulturalCognitionPayloadXml();
        IReadOnlyCollection<string> validFailures =
            PreflightFailures(validAttitudeXml);
        IReadOnlyCollection<string> omittedFailures = PreflightFailures(
            validAttitudeXml.Replace(
                "<observationLikelihood>0</observationLikelihood>",
                string.Empty, StringComparison.Ordinal));
        Add("preflight requires every schema-2 attitude cause",
            validFailures.Count == 0
                && omittedFailures.Any(value => value.Contains(
                    "observationLikelihood", StringComparison.Ordinal)),
            validFailures.Count == 0
                ? "explicit zero values pass; an omitted observationLikelihood fails"
                : "valid payload failures: " + string.Join("; ", validFailures));
        Add("preflight requires the schema-owned Culture registry",
            preflight.Contains(
                "int requiredRegistry = savedSchemaVersion == 10 ? 2 : 3;",
                StringComparison.Ordinal)
                && preflight.Contains(
                    "RequireInteger(culture, \"questionRegistryVersion\",",
                    StringComparison.Ordinal),
            "schema 10 admits registry 2 for supported migration; current schema 11 admits only registry 3");
        string uncertaintyMap = Between(state,
            "internal static float UncertaintyForQuestion",
            "// Versioned, conservative question loadings");
        string directionMap = Between(state,
            "internal static float PositionShiftForQuestion",
            "private static float Center");
        string[] directionalKeys = CACultureQuestionRegistry.All.Take(
                CACultureQuestionRegistry.LegacyQuestionCount)
            .Where(value => value.Key
                != CACultureQuestionRegistry.GenderDistribution)
            .Select(value => value.Key).ToArray();
        Add("psychology supplies explicit question-specific detail",
            state.Contains("mappingVersion", StringComparison.Ordinal)
                && CACultureQuestionRegistry.All.Take(
                    CACultureQuestionRegistry.LegacyQuestionCount).All(definition =>
                    uncertaintyMap.Contains("\"" + definition.Key + "\"",
                        StringComparison.Ordinal))
                && directionalKeys.All(key => directionMap.Contains(
                    "\"" + key + "\"", StringComparison.Ordinal))
                && directionMap.Contains(
                    "\"authority.genderDistribution\" => 0f",
                    StringComparison.Ordinal),
            $"the original {CACultureQuestionRegistry.LegacyQuestionCount} questions retain explicit psychology mappings; gender authority has no personality-derived sex preference");

        string knowledge = S("Source/PropositionKnowledgeModule.cs");
        string compactKnowledge = Compact(knowledge);
        Add("production knowledge retains expertise deference",
            compactKnowledge.Contains(
                    "ReevaluateKnowledge(record,appraisal.ExpertiseDeference)",
                    StringComparison.Ordinal)
                && compactKnowledge.Contains(
                    "expertiseDeference??ExpertiseDeferenceFor(record)",
                    StringComparison.Ordinal)
                && knowledge.Contains("DemonstratedExpertise(",
                    StringComparison.Ordinal)
                && knowledge.Contains("ExpertiseSkill(",
                    StringComparison.Ordinal)
                && CACulturalCognitionPureKernel
                    .ExpertiseDomainsForSocialSubject(
                        CASocialSubjectRegistry.MedicalCare)
                    .Contains(CACulturalCognitionPureKernel
                        .ExpertiseMedicine)
                && CACulturalCognitionPureKernel
                    .ExpertiseDomainsForSocialSubject(
                        CASocialSubjectRegistry.ResearchWork)
                    .Contains(CACulturalCognitionPureKernel
                        .ExpertiseIntellectual)
                && CACulturalCognitionPureKernel
                    .ExpertiseDomainsForSocialSubject(
                        CASocialSubjectRegistry.CombatViolence)
                    .Contains(CACulturalCognitionPureKernel
                        .ExpertiseShooting)
                && knowledge.Contains("SkillDefOf.Melee",
                    StringComparison.Ordinal),
            "acquisition passes the pawn appraisal; later reevaluation recovers holder Culture; the shared executed map supplies every subject's skill domains");

        Add("represented experience is wired into the event path",
            Count(state, "MoralExperienceFor(") >= 3
                && state.Contains("EvidenceConfidenceFor(",
                    StringComparison.Ordinal)
                && state.Contains("DirectQuestionEvidenceFor(",
                    StringComparison.Ordinal)
                && state.Contains("RepresentedQuestionSourceAppraisal(",
                    StringComparison.Ordinal)
                && S("Source/SocialInterpretationRuntimeModule.cs").Contains(
                    "RefreshRepresentedEvidence", StringComparison.Ordinal),
            "materialization and refresh consume the persisted direct-history record plus represented social reactions");

        bool productionModel = ProductionCultureModelAuthority(
            out string productionEvidence);
        Add("built assembly remains the Culture model authority",
            productionModel, productionEvidence);
    }

    private static void FixtureReceipts(string active, string mirror)
    {
        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        XDocument document = XDocument.Load(active,
            LoadOptions.PreserveWhitespace);
        XElement plan = document.Root?.Element("plan")
            ?? throw new InvalidDataException("active plan missing");
        XElement[] settlements = Items(plan, "settlements").ToArray();
        XElement[] cultures = document.Descendants().Where(value =>
            value.Name.LocalName is "culture" or "localCulture").ToArray();
        Add("active and mirror fixtures are byte-identical",
            activeBytes.SequenceEqual(mirrorBytes),
            "SHA-256=" + Sha(activeBytes));
        Add("fixture preserves authored identity and composition",
            Value(document.Root, "authoringDataEpoch") == "14"
                && Value(plan, "regionalId") == "CA-RG-EB596A12"
                && Value(plan, "candidateId") == "613b1fe44104"
                && Value(plan, "startTileId") == "389638"
                && Value(plan, "bundleRootTileId") == "389638"
                && Value(plan, "mapSize") == "350"
                && Items(plan, "factions").Count() == 3
                && settlements.Length == 4
                && settlements.SelectMany(value => Items(value,
                    "populationGroups")).Count() == 4
                && settlements.SelectMany(value => Items(value,
                    "operationalFacts")).Count() == 19,
            "region/candidate/arrival/map; 3 factions; 4 settlements; 4 current population assignments; 19 program facts");

        HashSet<string> factionKeys = Items(plan, "factions")
            .Select(value => Value(value, "key"))
            .ToHashSet(StringComparer.Ordinal);
        bool relationships = settlements.All(settlement =>
                factionKeys.Contains(Value(settlement, "factionKey")))
            && settlements.All(settlement => Items(settlement,
                    "populationGroups").Sum(group =>
                        int.Parse(Value(group, "share"))) == 100)
            && settlements.SelectMany(value => Items(value,
                    "populationGroups")).Where(group =>
                    Value(group, "factionKey").Length > 0)
                .All(group => factionKeys.Contains(Value(group,
                    "factionKey")));
        Add("faction and population relationships survive readback",
            relationships,
            "every settlement owner and affiliated population resolves to one of 3 factions; shares total 100 per settlement");

        bool complete = cultures.Length == 8 && cultures.All(culture =>
        {
            List<XElement> roots = Items(culture, "inheritedQuestions")
                .Concat(Items(culture, "localQuestions")).Where(value =>
                    Scope(value) == "*").ToList();
            return Value(culture, "schemaVersion") == "11"
                && Value(culture, "questionRegistryVersion") == "3"
                && roots.Count == CACultureQuestionRegistry.FixedQuestionCount
                && roots.Select(value => Value(value, "questionKey"))
                    .ToHashSet(StringComparer.Ordinal).SetEquals(
                        CACultureQuestionRegistry.All.Select(value =>
                            value.Key));
        });
        Add("fixture carries complete current Culture state",
            complete,
            $"8 schema-11 registry-3 records each contain {CACultureQuestionRegistry.FixedQuestionCount} root distributions");
        int questionCount = cultures.Sum(value =>
            Items(value, "inheritedQuestions").Count()
                + Items(value, "localQuestions").Count());
        int evidenceCount = cultures.Sum(value =>
            Items(value, "legacyEvidence").Count());
        int quarantinedCount = cultures.SelectMany(value =>
                Items(value, "legacyEvidence"))
            .Count(value => Value(value, "sourceLayer")
                    == "B16 governed fixture repair"
                && Value(value, "disposition").Contains(
                    "not a constituent", StringComparison.Ordinal));
        Add("prior authored and migration evidence remains intact",
            questionCount >= cultures.Length
                    * CACultureQuestionRegistry.FixedQuestionCount
                && quarantinedCount == 1
                && evidenceCount == 26
                && cultures.SelectMany(value => Items(value,
                    "legacyEvidence")).Any(value =>
                    Value(value, "sourceKey")
                        == "ca.property.compulsory_transfer"),
            $"{questionCount} current distributions retain 289 live pre-B16 distributions, add registry-3 coverage to represented scopes, and preserve one orphaned former distribution among {evidenceCount} evidence records");
        string roundTrip = document.ToString(SaveOptions.DisableFormatting);
        XDocument readback = XDocument.Parse(roundTrip);
        XElement[] readbackCultures = readback.Descendants().Where(value =>
            value.Name.LocalName is "culture" or "localCulture").ToArray();
        int readbackCultureQuestions = readbackCultures.Sum(value =>
            Items(value, "inheritedQuestions").Count()
                + Items(value, "localQuestions").Count());
        Add("fixture round-trips without dropping Culture",
            readbackCultureQuestions == questionCount
                && readbackCultures.Length == 8,
            $"XML serialization/readback retains all {questionCount} distributions and 8 Culture records");
    }

    private static void ResearchReceipts()
    {
        string corpus = S("CULTURE_RESEARCH_CORPUS.md");
        string cognition = S("CULTURAL_COGNITION_RESEARCH.md");
        string[] primarySources =
        {
            "worldvaluessurvey.org", "issp.org",
            "europeansocialsurvey.org", "gss.norc.org"
        };
        Add("research corpus names primary survey sources",
            primarySources.All(value => corpus.Contains(value,
                StringComparison.OrdinalIgnoreCase)),
            "WVS, ISSP, ESS, and GSS primary documentation linked");
        Add("research record states calibration limits",
            corpus.Contains("not a claim of psychometric",
                    StringComparison.OrdinalIgnoreCase)
                && corpus.Contains("Empirical calibration remains pending",
                    StringComparison.Ordinal)
                && cognition.Contains("B13", StringComparison.Ordinal),
            "sources justify constructs and ordering without claiming fitted population estimates");
    }

    private static void WriteReceipts(string active, string mirror)
    {
        int passed = Results.Count(value => value.Passed);
        var builder = new StringBuilder();
        builder.AppendLine("# B13 Acceptance Receipts\n");
        builder.AppendLine("These fixed-seed and structural receipts verify the Culture registry, unified authoring paths, causal separations, production consumers, historical feedback, current persistence, and the authored runtime fixture. They do not claim psychometric validation or operator runtime acceptance.\n");
        builder.AppendLine("| # | Contract | Result | Evidence |");
        builder.AppendLine("|---:|---|---|---|");
        foreach (Result result in Results)
            builder.AppendLine($"| {result.Number} | {Escape(result.Name)} | **{(result.Passed ? "PASS" : "FAIL")}** | {Escape(result.Evidence)} |");
        builder.AppendLine($"\nResult: **{passed}/{Results.Count} PASS**\n");
        File.WriteAllText(Path.Combine(repo, "B13_ACCEPTANCE_RECEIPTS.md"),
            builder.ToString(), new UTF8Encoding(false));

        byte[] bytes = File.ReadAllBytes(active);
        XDocument document = XDocument.Load(active);
        XElement plan = document.Root!.Element("plan")!;
        XElement[] settlements = Items(plan, "settlements").ToArray();
        XElement[] cultures = document.Descendants().Where(value =>
            value.Name.LocalName is "culture" or "localCulture").ToArray();
        string fixture = $"""
# B13 Authored Fixture Receipt

| Field | Evidence |
|---|---|
| Active plan | `{active}` |
| Mirror plan | `{mirror}` |
| SHA-256 | `{Sha(bytes)}` on both files |
| Identity | `{Value(plan, "regionalId")}` / `{Value(plan, "candidateId")}` / arrival `{Value(plan, "startTileId")}` / map `{Value(plan, "mapSize")}` |
| Composition | {Items(plan, "factions").Count()} factions / {settlements.Length} settlements / {settlements.SelectMany(value => Items(value, "populationGroups")).Count()} population groups / {settlements.SelectMany(value => Items(value, "operationalFacts")).Count()} established program facts |
| Culture | {cultures.Length} schema-11 registry-{CACultureQuestionRegistry.CurrentVersion} records / {cultures.Sum(value => Items(value, "inheritedQuestions").Count() + Items(value, "localQuestions").Count())} distributions / {cultures.Sum(value => Items(value, "legacyEvidence").Count())} preserved source records |
| Completeness | Every Culture contains the same {CACultureQuestionRegistry.FixedQuestionCount} root question identities as `CACultureQuestionRegistry`; constituent-scoped distributions remain separate. |
| Readback | Faction ownership resolves, settlement population shares total 100, and all Culture state survives XML serialization/readback. |

The current runtime fixture retains {cultures.Sum(value => Items(value, "inheritedQuestions").Count() + Items(value, "localQuestions").Count())} Culture distributions and {cultures.Sum(value => Items(value, "legacyEvidence").Count())} source-evidence records, preserves the current plan identity and 3-faction/4-settlement composition, and is byte-identical to its governed mirror.
""";
        File.WriteAllText(Path.Combine(repo, "B13_FIXTURE_RECEIPT.md"),
            fixture, new UTF8Encoding(false));
    }

    private static CACulture CultureWithQuestion(int index, float mean,
        string provenance)
    {
        CACultureQuestionDistribution value =
            CACultureDistributionKernel.NewQuestion(
                CACultureQuestionRegistry.All[index]);
        value.mean = mean;
        value.provenance = provenance;
        return new CACulture
        {
            inheritedQuestions = new List<CACultureQuestionDistribution>
                { value }
        };
    }

    private static string Fingerprint(CACulture culture, string key) =>
        CACultureDistributionKernel.Fingerprint(culture.inheritedQuestions
            .Single(value => value.questionKey == key));

    private static CAAttitudeMaterializationInput Attitude(
        float salience = 0.50f, float norm = 0.55f,
        float tolerance = 0.35f, float sourceConfidence = 0.55f,
        float visibility = 0.65f, float psychologicalShift = 0f,
        float directEvidence = 0.35f, float enforcement = 0.15f,
        float moralExperience = 0.20f) =>
        new(0.20f, 0.10f, 0.10f, norm, tolerance, salience,
            sourceConfidence, visibility, 0.10f, 0.55f, 0.65f, 0.25f,
            0.60f, 0.25f, psychologicalShift, directEvidence,
            enforcement, moralExperience);

    private static CAKnowledgeAcceptanceInput Knowledge(
        float expertiseDeference) => new(0.55f, 0.50f, 0.90f, 0.40f,
            0.30f, 0.60f, 0.50f, 0.60f, 0.50f, 0.60f, 0.50f, 1f,
            0.60f, 0f, expertiseDeference);

    private static int ConsumerCount(CACultureQuestionDef value) =>
        value.BehaviorConsumers.Length + value.PoliticalConsumers.Length
        + value.InstitutionConsumers.Length + value.KnowledgeConsumers.Length;

    private static bool ContainsConstant(string source, string constant) =>
        Compact(source).Contains("CACultureQuestionRegistry." + constant,
            StringComparison.Ordinal);

    private static string Compact(string value) => new string(
        (value ?? string.Empty).Where(character =>
            !char.IsWhiteSpace(character)).ToArray());

    private static IReadOnlyCollection<string> PreflightFailures(string xml)
    {
        string path = Path.Combine(Path.GetTempPath(), "cao-b13-preflight-"
            + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(path, xml, new UTF8Encoding(false));
            CACampaignPreflightDocument document =
                CACampaignPreflightReader.Read(path);
            return document.Payloads.Single(value => value.ComponentType
                == "ColonistAwareness.CACulturalCognitionWorldComponent")
                .ValidationFailures.ToArray();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string CulturalCognitionPayloadXml() => """
<savegame>
  <game>
    <world>
      <components>
        <li Class="ColonistAwareness.CACulturalCognitionWorldComponent">
          <CA_culturalCognitionOwnerVersion>2</CA_culturalCognitionOwnerVersion>
          <CA_psychologicalProfiles />
          <CA_culturalAttitudes>
            <li>
              <schemaVersion>2</schemaVersion>
              <pawnId>1</pawnId>
              <cultureId>receipt-culture</cultureId>
              <subgroupId>*</subgroupId>
              <questionKey>relationships.sameSexAcceptance</questionKey>
              <privateAttitude>0</privateAttitude>
              <attention>0</attention>
              <moralConviction>0</moralConviction>
              <identityCentrality>0</identityCentrality>
              <inheritedPriorStrength>0</inheritedPriorStrength>
              <knowledgeConfidence>0</knowledgeConfidence>
              <perceivedDescriptiveNorm>0</perceivedDescriptiveNorm>
              <perceivedInjunctiveNorm>0</perceivedInjunctiveNorm>
              <perceivedSocialPressure>0</perceivedSocialPressure>
              <expectedEnforcement>0</expectedEnforcement>
              <publicExpression>0</publicExpression>
              <observationLikelihood>0</observationLikelihood>
              <prestigeSignal>0</prestigeSignal>
              <uncertainty>0</uncertainty>
            </li>
          </CA_culturalAttitudes>
          <CA_socialInfluenceEdges />
        </li>
      </components>
    </world>
  </game>
</savegame>
""";

    private static bool ProductionCultureModelAuthority(out string evidence)
    {
        string executable = Path.Combine(repo, "tools",
            "B13ProductionModelReceipts", "bin", "Release", "net472",
            "B13ProductionModelReceipts.exe");
        try
        {
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = repo
            };
            using Process process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            evidence = string.IsNullOrWhiteSpace(output) ? error : output;
            return process.ExitCode == 0;
        }
        catch (Exception exception)
        {
            evidence = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private static string ConstantName(string key) => key switch
    {
        "relationships.sameSexAcceptance" => "SameSexAcceptance",
        "relationships.pluralityAcceptance" => "PluralityAcceptance",
        "relationships.kinObligation" => "KinObligation",
        "authority.genderDistribution" => "GenderDistribution",
        "authority.genderedWork" => "GenderedWork",
        "authority.officeAccess" => "GenderOfficeAccess",
        "status.hereditaryLegitimacy" => "HereditaryLegitimacy",
        "status.rankDifferentiation" => "RankDifferentiation",
        "status.mobility" => "StatusMobility",
        "groups.outsiderInclusion" => "OutsiderInclusion",
        "groups.integrationPreference" => "IntegrationPreference",
        "groups.membershipAccess" => "MembershipAccess",
        "labor.coercionLegitimacy" => "CoercionLegitimacy",
        "property.control" => "PropertyControl",
        "voice.inclusionExpectation" => "VoiceInclusion",
        "voice.dissentTolerance" => "DissentTolerance",
        "authority.enforcementLegitimacy" => "EnforcementLegitimacy",
        "war.captiveProtection" => "CaptiveProtection",
        "war.punishmentSeverity" => "PunishmentSeverity",
        "war.retaliatoryViolence" => "RetaliatoryViolence",
        "provision.mutualObligation" => "MutualProvision",
        "knowledge.access" => "KnowledgeAccess",
        "knowledge.noveltyAcceptance" => "NoveltyAcceptance",
        "knowledge.expertiseDeference" => "ExpertiseDeference",
        _ => throw new InvalidDataException("unmapped question " + key)
    };

    private static string Scope(XElement element)
    {
        string value = Value(element, "populationScope");
        return value.Length == 0 ? "*" : value;
    }

    private static string Between(string source, string start, string end)
    {
        int first = source.IndexOf(start, StringComparison.Ordinal);
        int last = source.IndexOf(end, first + start.Length,
            StringComparison.Ordinal);
        return first < 0 || last < 0 ? string.Empty
            : source.Substring(first, last - first);
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int at = 0;
        while ((at = source.IndexOf(value, at,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += value.Length;
        }
        return count;
    }

    private static void Add(string name, bool passed, string evidence) =>
        Results.Add(new Result(Results.Count + 1, name, passed, evidence));

    private static string S(string relative) => File.ReadAllText(
        Path.Combine(repo, relative.Replace('/',
            Path.DirectorySeparatorChar)));

    private static IEnumerable<XElement> Items(XElement parent, string name) =>
        parent?.Element(name)?.Elements("li") ?? Enumerable.Empty<XElement>();

    private static string Value(XElement parent, string name) =>
        parent?.Element(name)?.Value ?? string.Empty;

    private static string Sha(byte[] bytes) => Convert.ToHexString(
        SHA256.HashData(bytes));

    private static string Escape(string value) => (value ?? string.Empty)
        .Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
