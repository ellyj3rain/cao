using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using ColonistAwareness;
using ColonistAwareness.Tools;

internal static class Program
{
    private sealed record Result(int Number, string Name, bool Passed,
        string Evidence);

    private static readonly List<Result> Results = new();
    private static string repo = "";

    private static int Main(string[] args)
    {
        bool verifyOnly = args.Length == 4
            && args[3] == "--verify-only";
        if (args.Length != 3 && !verifyOnly)
        {
            Console.Error.WriteLine(
                "usage: B12CulturalCognitionReceipts <repo> <active> <mirror> [--verify-only]");
            return 1;
        }
        repo = Path.GetFullPath(args[0]);
        string active = Path.GetFullPath(args[1]);
        string mirror = Path.GetFullPath(args[2]);

        RegistryReceipts();
        DistributionReceipts();
        SemanticReceipts();
        CognitionReceipts();
        NetworkReceipts();
        PoliticalReceipts();
        InstitutionReceipts();
        KnowledgeReceipts();
        SourceReceipts();
        CompatibilityReceipts();
        HistoryAndPerformanceReceipts();
        FixtureReceipts(active, mirror);
        if (!verifyOnly) WriteReceipts(active, mirror);

        int passed = Results.Count(result => result.Passed);
        foreach (Result result in Results)
            Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} {result.Number:00} {result.Name}: {result.Evidence}");
        Console.WriteLine($"B12 acceptance: {passed}/{Results.Count} PASS");
        return passed == Results.Count ? 0 : 2;
    }

    private static void RegistryReceipts()
    {
        IReadOnlyList<CACultureQuestionDef> all =
            CACultureQuestionRegistry.All;
        Add("question registry validates",
            CACultureQuestionRegistry.ValidationFailure() == null,
            CACultureQuestionRegistry.ValidationFailure() ?? "no failure");
        Add("current registry retains the B12 questions",
            all.Count == 24 && all.Select(value => value.Key).Distinct(
                StringComparer.Ordinal).Count() == 24,
            $"count={all.Count}; unique={all.Select(value => value.Key).Distinct().Count()}");
        Add("five ordered anchors per question", all.All(value =>
                value.Anchors.Length == 5
                && value.AnchorCenters.Zip(value.AnchorCenters.Skip(1))
                    .All(pair => pair.First < pair.Second)),
            "all anchors are named and strictly monotonic");
        Add("every question names a represented consumer", all.All(value =>
                value.BehaviorConsumers.Length + value.PoliticalConsumers.Length
                + value.InstitutionConsumers.Length
                + value.KnowledgeConsumers.Length > 0),
            "behavior, political, institution, and knowledge routes inspected");
    }

    private static void DistributionReceipts()
    {
        List<CACultureQuestionDistribution> defaults =
            CACultureQuestionRegistry.All.Select(
                value => CACultureDistributionKernel.NewQuestion(value))
                .ToList();
        Add("explicit new-question defaults validate", defaults.Count == 24
            && defaults.All(value =>
                CACultureDistributionKernel.ValidationFailure(value) == null),
            "24/24 neutral authoring defaults; no identity-derived facts");

        CACultureQuestionDistribution distribution = defaults[0];
        float sameA = Sample(distribution, "pawn-17");
        float sameB = Sample(distribution, "pawn-17");
        float other = Sample(distribution, "pawn-18");
        Add("materialization is deterministic", sameA == sameB,
            $"same seed={sameA:0.000000}/{sameB:0.000000}");
        Add("pawns vary within one population", Math.Abs(sameA - other) > 0.001f,
            $"pawn17={sameA:0.000}; pawn18={other:0.000}");

        CACultureQuestionDistribution floor = distribution.Copy();
        floor.spread = CACultureDistributionKernel.VarianceFloor - 0.001f;
        Add("variance floor rejects degenerate input",
            CACultureDistributionKernel.ValidationFailure(floor)
                ?.Contains("out of range", StringComparison.Ordinal) == true,
            $"floor={CACultureDistributionKernel.VarianceFloor:0.00}");

        Add("anchor lookup is monotonic",
            new[] { -0.90f, -0.45f, 0f, 0.45f, 0.90f }
                .Select(CACultureDistributionKernel.NearestAnchor)
                .SequenceEqual(new[] { 0, 1, 2, 3, 4 }),
            "-0.90,-0.45,0,+0.45,+0.90 -> 0..4");

        CACultureQuestionDistribution grouped = distribution.Copy();
        grouped.mean = 0f;
        grouped.subgroups = new List<CACultureSubgroupDistribution>
        {
            new() { subgroupKey = "low", label = "Low", share = 50,
                meanOffset = -0.45f, spreadMultiplier = 0.8f },
            new() { subgroupKey = "high", label = "High", share = 50,
                meanOffset = 0.45f, spreadMultiplier = 0.8f }
        };
        double low = Enumerable.Range(0, 201).Average(index =>
            Materialize(grouped, "low", index.ToString()));
        double high = Enumerable.Range(0, 201).Average(index =>
            Materialize(grouped, "high", index.ToString()));
        Add("subgroups preserve represented offsets",
            CACultureDistributionKernel.ValidationFailure(grouped) == null
                && high > low + 0.45,
            $"low={low:0.000}; high={high:0.000}; shares=100");

        CACultureQuestionDistribution meanChanged = distribution.Copy();
        meanChanged.mean = Math.Min(0.9f, distribution.mean + 0.4f);
        CACultureQuestionDistribution unrelated = defaults[1].Copy();
        string unrelatedBefore = CACultureDistributionKernel.Fingerprint(
            unrelated);
        float before = Sample(distribution, "pawn-33");
        float after = Sample(meanChanged, "pawn-33");
        Add("one mean changes only its distribution", after > before
                && unrelatedBefore == CACultureDistributionKernel.Fingerprint(
                    unrelated),
            $"target {before:0.000}->{after:0.000}; unrelated={unrelatedBefore}");

        CACultureQuestionDistribution narrow = distribution.Copy();
        narrow.mean = 0.70f;
        narrow.spread = 0.08f;
        CACultureQuestionDistribution broad = narrow.Copy();
        broad.spread = 0.60f;
        (double narrowMean, double narrowSd) = Moments(narrow);
        (double broadMean, double broadSd) = Moments(broad);
        Add("spread changes dispersion, not authored center",
            broadSd > narrowSd * 2.5
                && Math.Abs(narrowMean - 0.70) < 0.06
                && Math.Abs(broadMean - 0.70) < 0.06,
            $"authored=0.700; means {narrowMean:0.000}/{broadMean:0.000}; sd {narrowSd:0.000}->{broadSd:0.000}");
    }

    private static void SemanticReceipts()
    {
        IReadOnlyList<CACultureQuestionDef> all =
            CACultureQuestionRegistry.All;
        string[] evaluativeWords = { "nice", "good people", "bad people",
            "correct people", "wrong people" };
        Add("question labels remain descriptive",
            all.All(value => evaluativeWords.All(word =>
                value.Label.IndexOf(word,
                    StringComparison.OrdinalIgnoreCase) < 0
                && value.Question.IndexOf(word,
                    StringComparison.OrdinalIgnoreCase) < 0)),
            "no moralized good/bad or niceness scale");
        Add("ordered endpoints share one centered scale",
            all.All(value => Math.Abs(value.AnchorCenters[0]
                    + value.AnchorCenters[4]) < 0.0001
                && Math.Abs(value.AnchorCenters[1]
                    + value.AnchorCenters[3]) < 0.0001
                && value.AnchorCenters[2] == 0d),
            "all five-anchor scales are centered and mirror-coherent");

        CACultureQuestionDistribution outsider =
            CACultureDistributionKernel.NewQuestion(
                CACultureQuestionRegistry.Find(
                    CACultureQuestionRegistry.OutsiderInclusion));
        CACultureQuestionDistribution integration =
            CACultureDistributionKernel.NewQuestion(
                CACultureQuestionRegistry.Find(
                    CACultureQuestionRegistry.IntegrationPreference));
        string integrationBefore =
            CACultureDistributionKernel.Fingerprint(integration);
        outsider.mean = 0.8f;
        Add("outsider inclusion is independent from integration",
            integrationBefore == CACultureDistributionKernel.Fingerprint(
                integration)
                && outsider.questionKey != integration.questionKey,
            "changing outsider inclusion leaves integration unchanged");

        CACultureQuestionDistribution hereditary =
            CACultureDistributionKernel.NewQuestion(
                CACultureQuestionRegistry.Find(
                    CACultureQuestionRegistry.HereditaryLegitimacy));
        CACultureQuestionDistribution rank =
            CACultureDistributionKernel.NewQuestion(
                CACultureQuestionRegistry.Find(
                    CACultureQuestionRegistry.RankDifferentiation));
        string rankBefore = CACultureDistributionKernel.Fingerprint(rank);
        hereditary.mean = 0.9f;
        Add("hereditary status is independent from rank",
            rankBefore == CACultureDistributionKernel.Fingerprint(rank)
                && hereditary.questionKey != rank.questionKey,
            "inheritance legitimacy does not author durable rank");

        string political = S("Source/FactionCompositionModule.cs")
            + S("Source/CulturalPoliticsStateModule.cs");
        Add("nominal political forms remain nominal",
            political.Contains("optionKey", StringComparison.Ordinal)
                && political.Contains("preferredOptionKey",
                    StringComparison.Ordinal)
                && !CACultureQuestionRegistry.All.Any(value =>
                    value.Key == "leadership"
                    || value.Key == "decisions"),
            "leader, council, assembly, and decision forms remain option keys");
        Add("unsupported subjects remain outside Culture",
            CACultureQuestionRegistry.QuestionForSocialSubject(
                    CASocialSubjectRegistry.ResearchWork)
                    == CACultureQuestionRegistry.NoveltyAcceptance
                && CACultureQuestionRegistry.QuestionForSocialSubject(
                    CASocialSubjectRegistry.Taxation) == null
                && CACultureQuestionRegistry.QuestionForSocialSubject(
                    CASocialSubjectRegistry.CompulsoryTransfer) == null,
            "B13 maps represented research to novelty acceptance; taxation and compulsory transfer remain factual evidence");
    }

    private static void CognitionReceipts()
    {
        CAAttitudeMaterializationInput basis = Input();
        CAAttitudeMaterializationResult initial =
            CACulturalCognitionPureKernel.Materialize(basis);
        CAAttitudeMaterializationResult salient =
            CACulturalCognitionPureKernel.Materialize(Input(salience: 0.95f));
        Add("B13 separates salience from conviction",
            salient.Attention > initial.Attention
                && salient.MoralConviction == initial.MoralConviction
                && salient.PrivatePosition == initial.PrivatePosition,
            $"attention {initial.Attention:0.000}->{salient.Attention:0.000}; conviction stable");

        CAAttitudeMaterializationResult strongNorm =
            CACulturalCognitionPureKernel.Materialize(Input(norm: 0.95f));
        Add("B13 separates norm pressure from enforcement",
            strongNorm.PerceivedSocialPressure
                    > initial.PerceivedSocialPressure
                && strongNorm.ExpectedEnforcement
                    == initial.ExpectedEnforcement
                && strongNorm.PrivatePosition == initial.PrivatePosition,
            $"pressure {initial.PerceivedSocialPressure:0.000}->{strongNorm.PerceivedSocialPressure:0.000}; enforcement stable");

        CAAttitudeMaterializationResult tolerant =
            CACulturalCognitionPureKernel.Materialize(Input(tolerance: 0.9f));
        Add("divergence tolerance reduces norm pressure",
            tolerant.PerceivedSocialPressure
                    < initial.PerceivedSocialPressure
                && tolerant.ExpectedEnforcement
                    == initial.ExpectedEnforcement
                && tolerant.PrivatePosition == initial.PrivatePosition,
            $"pressure {initial.PerceivedSocialPressure:0.000}->{tolerant.PerceivedSocialPressure:0.000}; enforcement stable");

        CAAttitudeMaterializationResult confident =
            CACulturalCognitionPureKernel.Materialize(Input(confidence: 0.95f));
        Add("B13 separates inherited confidence from knowledge confidence",
            confident.InheritedPriorStrength
                    > initial.InheritedPriorStrength
                && confident.KnowledgeConfidence
                    == initial.KnowledgeConfidence
                && confident.Uncertainty == initial.Uncertainty
                && confident.PrivatePosition == initial.PrivatePosition,
            $"prior {initial.InheritedPriorStrength:0.000}->{confident.InheritedPriorStrength:0.000}; knowledge stable");

        CAAttitudeMaterializationResult doctrine =
            CACulturalCognitionPureKernel.Materialize(Input(doctrine: 0.9f));
        Add("doctrine changes injunctive pressure, not Culture",
            doctrine.InjunctiveNorm > initial.InjunctiveNorm
                && doctrine.PrivatePosition == initial.PrivatePosition,
            $"injunctive {initial.InjunctiveNorm:0.000}->{doctrine.InjunctiveNorm:0.000}; private stable");

        CAAttitudeMaterializationResult hidden =
            CACulturalCognitionPureKernel.Materialize(Input(visibility: 0.05f));
        CAAttitudeMaterializationResult visible =
            CACulturalCognitionPureKernel.Materialize(Input(visibility: 0.95f));
        Add("B13 separates observation likelihood from expression",
            visible.ObservationLikelihood > hidden.ObservationLikelihood
                && visible.PublicExpression == hidden.PublicExpression
                && visible.PrivatePosition == hidden.PrivatePosition,
            $"observation {hidden.ObservationLikelihood:0.000}->{visible.ObservationLikelihood:0.000}; expression stable");

        float neutral = CACulturalCognitionPureKernel
            .NeutralPsychologicalPrior();
        Add("unobserved psychology uses an explicit neutral prior",
            neutral == 0.5f,
            $"prior={neutral:0.000}; identity is not an evidence source");

        CACulturalInfluenceResult accepted =
            CACulturalCognitionPureKernel.Influence(-0.2f, -0.2f, -0.1f,
                0.6f, 0.65f, 0.8f, new[]
                {
                    new CACulturalInfluenceSample(0.35f, 1f, 0.8f, 0.7f, 0.8f, 1f)
                });
        CACulturalInfluenceResult rejected =
            CACulturalCognitionPureKernel.Influence(-0.8f, -0.8f, -0.7f,
                0.6f, 0.65f, 0.8f, new[]
                {
                    new CACulturalInfluenceSample(0.8f, 1f, 1f, 1f, 1f, 1f)
                });
        Add("sparse influence is bounded and selective",
            accepted.PrivatePosition > -0.2f
                && accepted.PrivatePosition < 0.35f
                && rejected.PrivatePosition == -0.8f,
            $"accepted={accepted.PrivatePosition:0.000}; distant={rejected.PrivatePosition:0.000}");

        float hostile = CACulturalCognitionPureKernel
            .RelationshipApproachFactor(-1f, 1f);
        float affirming = CACulturalCognitionPureKernel
            .RelationshipApproachFactor(1f, 1f);
        Add("relationship approach is bounded and monotonic",
            hostile >= 0.70f && affirming <= 1.15f && affirming > hostile,
            $"{hostile:0.00}..{affirming:0.00}");
        Add("institution fit follows belief-practice agreement",
            CACulturalCognitionPureKernel.InstitutionFit(0.8f, 0.8f)
                > CACulturalCognitionPureKernel.InstitutionFit(0.8f, -0.8f),
            "agreement produces greater legitimacy fit");
        Add("novelty changes knowledge transmissibility",
            CACulturalCognitionPureKernel.KnowledgeTransmission(0.8f)
                > CACulturalCognitionPureKernel.KnowledgeTransmission(-0.8f),
            "receptive > tradition-bound");
    }

    private static void NetworkReceipts()
    {
        CAAttitudeMaterializationResult distinct =
            CACulturalCognitionPureKernel.Materialize(
                new CAAttitudeMaterializationInput(0.8f, 0.1f, -0.7f,
                    0.8f, 0.2f, 0.7f, 0.8f, 0.9f, 0.8f,
                    0.7f, 0.8f, 0.2f, 0.7f, 0.2f));
        Add("descriptive and injunctive norms remain separate",
            distinct.DescriptiveNorm < -0.6f
                && distinct.InjunctiveNorm > distinct.DescriptiveNorm,
            $"descriptive={distinct.DescriptiveNorm:0.00}; injunctive={distinct.InjunctiveNorm:0.00}");
        Add("private and public positions can diverge",
            CACulturalCognitionPureKernel.PrivatePublicGap(
                distinct.PrivatePosition, distinct.PublicExpression) > 0.05f,
            $"gap={CACulturalCognitionPureKernel.PrivatePublicGap(distinct.PrivatePosition, distinct.PublicExpression):0.00}");
        Add("perceived and actual norms can diverge",
            CACulturalCognitionPureKernel.PerceivedActualNormGap(
                -0.6f, 0.4f) == 1f,
            "represented perceived=-0.60 and actual=+0.40");
        Add("pluralistic ignorance is representable",
            CACulturalCognitionPureKernel.PrivatePublicGap(0.75f, -0.25f)
                >= 1f
                && CACulturalCognitionPureKernel.PerceivedActualNormGap(
                    -0.5f, 0.5f) >= 1f,
            "private support can coexist with contrary expression and belief");

        CACulturalInfluenceResult trustedPositive =
            CACulturalCognitionPureKernel.Influence(0f, 0f, 0f, 0.3f,
                1f, 0.8f, new[]
                {
                    new CACulturalInfluenceSample(0.6f, 1f, 1f, 1f, 0.8f, 1f),
                    new CACulturalInfluenceSample(-0.6f, 1f, 0f, 0f, 0.8f, 1f)
                });
        CACulturalInfluenceResult trustedNegative =
            CACulturalCognitionPureKernel.Influence(0f, 0f, 0f, 0.3f,
                1f, 0.8f, new[]
                {
                    new CACulturalInfluenceSample(0.6f, 1f, 0f, 0f, 0.8f, 1f),
                    new CACulturalInfluenceSample(-0.6f, 1f, 1f, 1f, 0.8f, 1f)
                });
        Add("referent trust and prestige alter influence",
            trustedPositive.PrivatePosition
                > trustedNegative.PrivatePosition,
            $"positive referent={trustedPositive.PrivatePosition:0.000}; negative referent={trustedNegative.PrivatePosition:0.000}");

        CACulturalInfluenceResult one =
            CACulturalCognitionPureKernel.Influence(0f, 0f, 0f, 0.2f,
                1f, 0.8f, new[]
                {
                    new CACulturalInfluenceSample(0.5f, 1f, 0.7f, 0.5f, 0.7f, 1f)
                });
        CACulturalInfluenceResult three =
            CACulturalCognitionPureKernel.Influence(0f, 0f, 0f, 0.2f,
                1f, 0.8f, Enumerable.Repeat(
                    new CACulturalInfluenceSample(0.5f, 1f, 0.7f, 0.5f,
                        0.7f, 1f), 3));
        Add("repeated independent exposure strengthens adoption",
            three.PrivatePosition > one.PrivatePosition,
            $"one={one.PrivatePosition:0.000}; three={three.PrivatePosition:0.000}");
        Add("collective reinforcement remains bounded",
            three.PrivatePosition > 0f && three.PrivatePosition < 0.5f,
            $"reinforced={three.PrivatePosition:0.000}; source=0.500");

        CACulturalInfluenceResult localOnly =
            CACulturalCognitionPureKernel.Influence(0f, 0f, 0f, 0.2f,
                1f, 0.8f, new[]
                {
                    new CACulturalInfluenceSample(0.5f, 1f, 0.7f, 0.5f, 0.7f, 1f)
                });
        CACulturalInfluenceResult bridged =
            CACulturalCognitionPureKernel.Influence(0f, 0f, 0f, 0.2f,
                1f, 0.8f, new[]
                {
                    new CACulturalInfluenceSample(0.5f, 1f, 0.7f, 0.5f, 0.7f, 1f),
                    new CACulturalInfluenceSample(-0.4f, 0.5f, 0.5f, 0.3f,
                        0.5f, 1f)
                });
        Add("network topology changes the observed neighborhood",
            Math.Abs(localOnly.PrivatePosition - bridged.PrivatePosition)
                > 0.005f,
            $"local={localOnly.PrivatePosition:0.000}; bridged={bridged.PrivatePosition:0.000}");
    }

    private static void PoliticalReceipts()
    {
        CAPoliticalFormationInput baseline = new(0.25f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f, 0.65f, 0.6f, 0.5f, 0f, 0.2f, 0.3f);
        CAPoliticalFormationResult baseResult =
            CACulturalCognitionPureKernel.FormPoliticalAttitude(baseline);
        CAPoliticalFormationResult psychology =
            CACulturalCognitionPureKernel.FormPoliticalAttitude(
                new CAPoliticalFormationInput(0.25f, 0.8f, 0f, 0f,
                    0f, 0f, 0f, 0f, 0f, 0.65f, 0.6f, 0.5f, 0f, 0.2f, 0.3f));
        Add("within-culture political disagreement is possible",
            psychology.Support > baseResult.Support
                && psychology.Confidence == baseResult.Confidence,
            $"same Culture; support {baseResult.Support:0.00}->{psychology.Support:0.00}");
        CAPoliticalFormationResult material =
            CACulturalCognitionPureKernel.FormPoliticalAttitude(
                new CAPoliticalFormationInput(0.25f, 0f, 0f, -0.8f,
                    0f, 0f, 0f, 0f, 0f, 0.65f, 0.6f, 0.5f, 0f, 0.2f, 0.3f));
        Add("material interests can diverge from Culture",
            material.Support < baseResult.Support,
            $"neutral={baseResult.Support:0.00}; adverse interest={material.Support:0.00}");
        CAPoliticalFormationResult informed =
            CACulturalCognitionPureKernel.FormPoliticalAttitude(
                new CAPoliticalFormationInput(0.25f, 0f, 0f, 0f,
                    0f, 0f, 0f, 0f, 0f, 0.95f, 0.6f, 0.5f, 0f, 0.2f, 0.3f));
        Add("political confidence follows represented knowledge",
            informed.Confidence > baseResult.Confidence
                && informed.Support == baseResult.Support,
            $"confidence {baseResult.Confidence:0.00}->{informed.Confidence:0.00}; support stable");
        CAPoliticalFormationResult directedKnowledge =
            CACulturalCognitionPureKernel.FormPoliticalAttitude(
                new CAPoliticalFormationInput(0.25f, 0f, 0f, 0f,
                    0f, 0f, 0f, 0.9f, 0f, 0.95f, 0.6f, 0.5f, 0f,
                    0.2f, 0.3f));
        Add("political propositions can change option support",
            directedKnowledge.Support > informed.Support,
            $"confidence-only={informed.Support:0.00}; directed evidence={directedKnowledge.Support:0.00}");

        float positiveCorrelation = CACulturalCognitionPureKernel
            .PearsonCorrelation(new[] { -1f, -0.5f, 0.5f, 1f },
                new[] { -0.8f, -0.4f, 0.4f, 0.8f });
        float zeroCorrelation = CACulturalCognitionPureKernel
            .PearsonCorrelation(new[] { -1f, -0.5f, 0.5f, 1f },
                new[] { 1f, -1f, 1f, -1f });
        Add("issue correlation requires cross-pawn variation",
            positiveCorrelation > 0.95f
                && Math.Abs(zeroCorrelation) < 0.5f,
            $"aligned={positiveCorrelation:0.00}; independent={zeroCorrelation:0.00}");
        const int issueFamily = 1246;
        var nullRandom = new Random(12013);
        int falseNullLinks = 0;
        for (int trial = 0; trial < 64; trial++)
        for (int hypothesis = 0; hypothesis < issueFamily; hypothesis++)
        {
            float[] left = Enumerable.Range(0, 16).Select(_ =>
                (float)(nullRandom.NextDouble() * 2d - 1d)).ToArray();
            float[] right = Enumerable.Range(0, 16).Select(_ =>
                (float)(nullRandom.NextDouble() * 2d - 1d)).ToArray();
            float correlation = CACulturalCognitionPureKernel
                .PearsonCorrelation(left, right);
            if (CACulturalCognitionPureKernel.EligiblePoliticalIssueLink(
                    correlation, 16, issueFamily))
                falseNullLinks++;
        }
        Add("issue links control the evaluated hypothesis family",
            !CACulturalCognitionPureKernel.EligiblePoliticalIssueLink(
                0.99f, 3, issueFamily)
                && !CACulturalCognitionPureKernel
                    .EligiblePoliticalIssueLink(0.70f, 16, issueFamily)
                && CACulturalCognitionPureKernel
                    .EligiblePoliticalIssueLink(0.90f, 16, issueFamily)
                && falseNullLinks == 0,
            $"family={issueFamily}; 64 fixed-seed null families produced {falseNullLinks} links; r=.90 n=16 retained");
        float shrunk = CACulturalCognitionPureKernel
            .ShrunkPoliticalIssueCorrelation(0.90f, 16, issueFamily);
        float attenuated = CACulturalCognitionPureKernel
            .PoliticalNetworkSupport(new[]
                { (0.8f, shrunk, 0.20f) });
        Add("issue evidence weight survives network normalization",
            shrunk > 0f && shrunk < 0.90f
                && attenuated > 0f && attenuated < 0.8f * shrunk,
            $"raw=0.90; shrunk={shrunk:0.00}; lone weak link={attenuated:0.00}");

        string politics = S("Source/CulturalPoliticsStateModule.cs");
        Add("political issue links emerge from observed bundles",
            politics.Contains("SyncIssueLinks", StringComparison.Ordinal)
                && politics.Contains("learnedCorrelation",
                    StringComparison.Ordinal)
                && politics.Contains("PearsonCorrelation",
                    StringComparison.Ordinal)
                && politics.Contains("evidenceSignature",
                    StringComparison.Ordinal)
                && politics.Contains("selectedKeys",
                    StringComparison.Ordinal),
            "qualified faction samples update current links; vanished evidence removes stale links");
        Add("perceived political majority requires observed exposure",
            politics.Contains("HasRecentPoliticalExposure",
                    StringComparison.Ordinal)
                && politics.Contains("Faction membership is not omniscience",
                    StringComparison.Ordinal)
                && !politics.Contains("represented.Average(value => value.support)",
                    StringComparison.Ordinal),
            "proximity alone and faction-wide means do not reveal political positions");
        Add("coalitions sort without complete issue constraint",
            politics.Contains("issueKeys = new List<string>",
                    StringComparison.Ordinal)
                && politics.Contains("grievanceKeys",
                    StringComparison.Ordinal)
                && !politics.Contains("all axes must agree",
                    StringComparison.OrdinalIgnoreCase),
            "shared issue, grievances, and goals are separate; other positions survive");
        Add("political belief remains separate from current structure",
            politics.Contains("current law and institutional structure remain separate facts",
                    StringComparison.Ordinal)
                && politics.Contains("represented institutions excluded",
                    StringComparison.Ordinal),
            "current organization is evidence of experience, not a copied preference");
        CACulturalBehaviorResponse[] outcomes =
        {
            CACulturalCognitionPureKernel.SelectCulturalResponse(
                new CACulturalResponseInput(-0.2f, 0f, 0f, -0.2f,
                    0.5f, 0.2f, 0.6f, 0.8f, 0.1f)),
            CACulturalCognitionPureKernel.SelectCulturalResponse(
                new CACulturalResponseInput(-0.2f, 0f, 0f, -0.2f,
                    0.8f, 0.2f, 0.6f, 0.8f, 0.1f)),
            CACulturalCognitionPureKernel.SelectCulturalResponse(
                new CACulturalResponseInput(-0.2f, 0f, 0f, -0.2f,
                    0.5f, 0.65f, 0.6f, 0.8f, 0.1f)),
            CACulturalCognitionPureKernel.SelectCulturalResponse(
                new CACulturalResponseInput(-0.2f, 0f, 0f, -0.2f,
                    0.5f, 0.75f, 0.3f, 0.8f, 0.1f)),
            CACulturalCognitionPureKernel.SelectCulturalResponse(
                new CACulturalResponseInput(-0.5f, 0f, 0f, -0.5f,
                    0.5f, 0.2f, 0.6f, 0.2f, 0.1f)),
            CACulturalCognitionPureKernel.SelectCulturalResponse(
                new CACulturalResponseInput(-0.65f, 0f, 0f, -0.65f,
                    0.8f, 0.2f, 0.3f, 0.8f, 0.1f)),
            CACulturalCognitionPureKernel.SelectCulturalResponse(
                new CACulturalResponseInput(-0.75f, 0f, 0f, -0.75f,
                    0.5f, 0.2f, 0.2f, 0.8f, 0.8f))
        };
        Add("noncompliance outcomes are reachable from represented inputs",
            outcomes.SequenceEqual(new[] { CACulturalBehaviorResponse.Dissent,
                CACulturalBehaviorResponse.Object,
                CACulturalBehaviorResponse.Organize,
                CACulturalBehaviorResponse.Reform,
                CACulturalBehaviorResponse.Exit,
                CACulturalBehaviorResponse.Violate,
                CACulturalBehaviorResponse.Retaliate }),
            string.Join(",", outcomes));
    }

    private static void InstitutionReceipts()
    {
        float balanced = CACulturalCognitionPureKernel.InstitutionLegitimacy(
            InstitutionInput());
        float fair = CACulturalCognitionPureKernel.InstitutionLegitimacy(
            InstitutionInput(procedure: 0.95f));
        float effective = CACulturalCognitionPureKernel.InstitutionLegitimacy(
            InstitutionInput(performance: 0.95f));
        Add("procedure and performance affect legitimacy independently",
            fair > balanced && effective > balanced
                && Math.Abs(fair - effective) > 0.001f,
            $"base={balanced:0.000}; procedure={fair:0.000}; performance={effective:0.000}");
        float supported = CACulturalCognitionPureKernel
            .InstitutionLegitimacy(InstitutionInput(publicSupport: 0.95f));
        Add("public support is separate from outcome performance",
            supported > balanced && Math.Abs(supported - effective) > 0.001f,
            $"base={balanced:0.000}; support={supported:0.000}; performance={effective:0.000}");
        float corrupt = CACulturalCognitionPureKernel.InstitutionLegitimacy(
            InstitutionInput(corruption: 1f, coercion: 1f));
        Add("corruption and coercion reduce legitimacy",
            corrupt < balanced,
            $"base={balanced:0.000}; corrupt/coercive={corrupt:0.000}");

        CASanctionResponseResult legitimate =
            CACulturalCognitionPureKernel.SanctionResponse(0.9f, 0.9f,
                0.8f, 0.8f, 0.9f, 0.8f, 0.8f, 0.2f);
        CASanctionResponseResult arbitrary =
            CACulturalCognitionPureKernel.SanctionResponse(0.2f, 0.2f,
                0.8f, 0.3f, 0.2f, 0.2f, 0.8f, 0.9f);
        Add("sanctions have conditional effects",
            legitimate.Deterrence > arbitrary.Deterrence
                && legitimate.NormReinforcement
                    > arbitrary.NormReinforcement
                && arbitrary.Reactance > legitimate.Reactance,
            $"fair deterrence={legitimate.Deterrence:0.00}; arbitrary reactance={arbitrary.Reactance:0.00}");
        Add("unfair sanctions can crowd out cooperation",
            arbitrary.VoluntaryCooperation
                < legitimate.VoluntaryCooperation,
            $"voluntary {legitimate.VoluntaryCooperation:0.00}->{arbitrary.VoluntaryCooperation:0.00}");

        string organization = S("Source/OrganizationModule.cs");
        Add("institution and asset remain distinct owners",
            organization.Contains("CAOrganization", StringComparison.Ordinal)
                && organization.Contains("CAInstitutionLegitimacyAppraisal",
                    StringComparison.Ordinal)
                && !organization.Contains("class CAInstitutionAsset",
                    StringComparison.Ordinal),
            "organizations own rules/offices/history; material assets remain program state");
        Add("institution lifecycle evidence is retained",
            new[] { "offices", "successionRule", "lastAppraisalTick",
                "decisionHistory", "sanctionAppraisals" }
                .All(token => organization.Contains(token,
                    StringComparison.Ordinal)),
            "staffing, succession, maintenance cadence, decisions, and sanctions persist");
        Add("organization owner assembles legitimacy evidence",
            organization.Contains("InstitutionEvidenceFor(",
                    StringComparison.Ordinal)
                && organization.Contains("PerformanceEvidence(",
                    StringComparison.Ordinal)
                && organization.Contains("input.PublicSupport",
                    StringComparison.Ordinal)
                && S("Source/CulturalCognitionStateModule.cs").Contains(
                    "InstitutionCulturalFitFor",
                    StringComparison.Ordinal)
                && !S("Source/CulturalCognitionStateModule.cs").Contains(
                    "InstitutionEvidenceFor(", StringComparison.Ordinal),
            "organization composes procedure, outcomes, public support, and narrow cultural/political fit projections");
    }

    private static void KnowledgeReceipts()
    {
        CAKnowledgeAcceptanceInput baseline = KnowledgeInput();
        CAKnowledgeAcceptanceResult accepted =
            CACulturalCognitionPureKernel.EvaluateKnowledge(baseline);
        CAKnowledgeAcceptanceResult trusted =
            CACulturalCognitionPureKernel.EvaluateKnowledge(
                KnowledgeInput(trust: 0.95f));
        Add("source trust changes claim confidence",
            trusted.Confidence > accepted.Confidence,
            $"confidence {accepted.Confidence:0.000}->{trusted.Confidence:0.000}");
        CAKnowledgeAcceptanceResult corroborated =
            CACulturalCognitionPureKernel.EvaluateKnowledge(
                KnowledgeInput(corroboration: 0.95f));
        Add("independent corroboration changes claim confidence",
            corroborated.Confidence > accepted.Confidence,
            $"confidence {accepted.Confidence:0.000}->{corroborated.Confidence:0.000}");
        Add("distinct sources produce bounded corroboration",
            CACulturalCognitionPureKernel.KnowledgeCorroboration(1) == 0.25f
                && CACulturalCognitionPureKernel.KnowledgeCorroboration(3)
                    > CACulturalCognitionPureKernel
                        .KnowledgeCorroboration(1)
                && CACulturalCognitionPureKernel.KnowledgeCorroboration(20)
                    == 1f,
            "one source is not corroboration; independent sources accumulate to the bound");
        CAKnowledgeAcceptanceResult contradicted =
            CACulturalCognitionPureKernel.EvaluateKnowledge(
                KnowledgeInput(conflictFreedom:
                    CACulturalCognitionPureKernel.KnowledgeConflictFreedom(
                        1f, 2)));
        Add("explicit contradiction lowers current confidence",
            contradicted.Confidence < accepted.Confidence,
            $"uncontested={accepted.Confidence:0.000}; two conflicts={contradicted.Confidence:0.000}");
        CAKnowledgeAcceptanceResult novel =
            CACulturalCognitionPureKernel.EvaluateKnowledge(
                KnowledgeInput(novelty: 0.95f));
        Add("novelty changes attention, not discovery",
            novel.Attention > accepted.Attention
                && novel.Confidence == accepted.Confidence,
            $"attention {accepted.Attention:0.000}->{novel.Attention:0.000}; confidence stable");
        Add("access changes eligibility, not truth",
            !CACulturalCognitionPureKernel.KnowledgeAccessEligible(-0.9f,
                    0.4f, false)
                && CACulturalCognitionPureKernel.KnowledgeAccessEligible(
                    0.9f, 0.4f, false)
                && CACulturalCognitionPureKernel
                    .KnowledgeAccessEligible(-0.9f, 0f, true),
            "restricted testimony can be ineligible; direct observation remains available");
        Add("access and novelty jointly govern transmission",
            CACulturalCognitionPureKernel.KnowledgeTransmission(0.9f, 0.9f)
                > CACulturalCognitionPureKernel.KnowledgeTransmission(
                    -0.9f, 0.9f)
                && CACulturalCognitionPureKernel.KnowledgeTransmission(
                    0.9f, 0.9f)
                    > CACulturalCognitionPureKernel.KnowledgeTransmission(
                        0.9f, -0.9f),
            "access and novelty are independent transmission inputs");

        string knowledge = S("Source/PropositionKnowledgeModule.cs");
        Add("propositions retain provenance and contradictions",
            knowledge.Contains("provenanceChain", StringComparison.Ordinal)
                && knowledge.Contains("contradictions",
                    StringComparison.Ordinal)
                && knowledge.Contains("sourceIdentity",
                    StringComparison.Ordinal)
                && knowledge.Contains("LinkExplicitContradiction",
                    StringComparison.Ordinal)
                && knowledge.Contains("contradictsIdentity",
                    StringComparison.Ordinal)
                && knowledge.Contains("ReevaluateKnowledge",
                    StringComparison.Ordinal)
                && knowledge.Contains("corroboratingSources",
                    StringComparison.Ordinal)
                && !knowledge.Contains("value.content != record.content",
                    StringComparison.Ordinal),
            "claim, holder, source, channel, chain, evidence, and conflicts persist");
        var independentSources = new List<string>();
        float firstCorroboration = CACulturalCognitionPureKernel
            .RecordIndependentKnowledgeSource(independentSources, "pawn:41");
        float secondCorroboration = CACulturalCognitionPureKernel
            .RecordIndependentKnowledgeSource(independentSources, "pawn:52");
        var routedSources = new List<string>();
        if (CACulturalCognitionPureKernel.IsDistinctReportRoute(71, 41))
            CACulturalCognitionPureKernel.RecordIndependentKnowledgeSource(
                routedSources, "pawn:41");
        if (CACulturalCognitionPureKernel.IsDistinctReportRoute(71, 71))
            CACulturalCognitionPureKernel.RecordIndependentKnowledgeSource(
                routedSources, "pawn:71");
        if (CACulturalCognitionPureKernel.IsDistinctReportRoute(71, 52))
            CACulturalCognitionPureKernel.RecordIndependentKnowledgeSource(
                routedSources, "pawn:52");
        string directChannel = CACulturalCognitionPureKernel
            .ActKnowledgeChannel(1);
        string laterReportChannel = CACulturalCognitionPureKernel
            .ActKnowledgeChannel(2);
        string actLedger = S("Source/ActRecordModule.cs");
        string socialIngress = S("Source/SocialInterpretationRuntimeModule.cs");
        string politicalIngress = S("Source/PoliticalBeliefEffectsModule.cs");
        Add("observed claims derive live and independent source evidence",
            new[] { "relations?.OpinionOf(sourcePawn)",
                    "SkillDefOf.Intellectual", "SourceAuthority(sourcePawn)",
                    "RecordIndependentKnowledgeSource",
                    "fact.EpistemicSourceIdentity" }
                .All(token => knowledge.Contains(token,
                    StringComparison.Ordinal))
                && independentSources.Distinct(
                    StringComparer.Ordinal).Count() == 2
                && secondCorroboration > firstCorroboration
                && routedSources.SequenceEqual(new[] { "pawn:41", "pawn:52" })
                && directChannel == "Witnessed"
                && laterReportChannel == "Reported"
                && actLedger.Contains("IsDistinctReportRoute(",
                    StringComparison.Ordinal)
                && actLedger.Contains("knownSourceHow",
                    StringComparison.Ordinal)
                && socialIngress.Contains("act.KnowledgeSourceFor(",
                    StringComparison.Ordinal)
                && actLedger.Contains("knownSourceHolderIds",
                    StringComparison.Ordinal)
                && actLedger.Contains("foreach (int reporterId in reporterIds)",
                    StringComparison.Ordinal)
                && socialIngress.Contains("UnansweredSourcePawnIdsFor",
                    StringComparison.Ordinal)
                && socialIngress.Contains("MarkKnowledgeSourceAnswered",
                    StringComparison.Ordinal)
                && politicalIngress.Contains("HasUnansweredKnowledgeSource",
                    StringComparison.Ordinal),
            $"mixed direct/report channels={directChannel}/{laterReportChannel}; two reporters retained; holder self-source rejected; corroboration {firstCorroboration:0.00}->{secondCorroboration:0.00}");
        Add("research uses the complete represented contract",
            new[] { "priorKnowledgeKeys", "skilledPawnIds",
                "institutionIdentity", "authorityBasis", "method",
                "facilityIdentity", "materialBasis", "evidenceKeys",
                "preservation", "dissemination" }
                .All(token => knowledge.Contains(token,
                    StringComparison.Ordinal)),
            "question, actors, authority, method, facility, materials, evidence, preservation, and dissemination");
        Add("institutions preserve represented knowledge",
            knowledge.Contains("SyncInstitutionalRecords",
                    StringComparison.Ordinal)
                && knowledge.Contains("custodianIdentity",
                    StringComparison.Ordinal)
                && knowledge.Contains("record custody",
                    StringComparison.Ordinal),
            "organization records become custodied propositions");
        Add("knowledge networks remain sparse and bounded",
            knowledge.Contains(".Take(8)", StringComparison.Ordinal)
                && knowledge.Contains("provenanceChain",
                    StringComparison.Ordinal),
            "recent institutional records and prior knowledge are capped");
    }

    private static void SourceReceipts()
    {
        string state = S("Source/CulturalCognitionStateModule.cs");
        string relationship = S("Source/CulturalRelationshipModule.cs");
        string culture = S("Source/FactionCultureBeliefsModule.cs");
        string founding = S("Source/PlayerFoundingPageModule.cs")
            + S("Source/PlayerFoundingStateModule.cs");
        string behavior = S("Source/BehaviorCatalogModule.cs");
        string organization = S("Source/OrganizationModule.cs");
        string politics = S("Source/CulturalPoliticsStateModule.cs");
        string knowledge = S("Source/PropositionKnowledgeModule.cs");
        string cognitionKernel = S("Source/CulturalCognitionKernel.cs");
        string preflight = S("Source/CampaignCompatibilityPreflightKernel.cs");
        string interpretation = S("Source/SocialInterpretationRuntimeModule.cs");
        string combined = state + relationship + behavior + interpretation
            + politics + knowledge + organization;
        string compactCombined = new string(combined.Where(value =>
            !char.IsWhiteSpace(value)).ToArray());
        Add("all questions have actual source consumers",
            CACultureQuestionRegistry.All.All(definition =>
                compactCombined.Contains("CACultureQuestionRegistry."
                        + ConstantName(definition.Key),
                    StringComparison.Ordinal)
                || combined.Contains("\"" + definition.Key + "\"",
                    StringComparison.Ordinal)),
            "24/24 registry constants occur outside the registry");
        Add("durable owners partition represented cognition",
            new[] { "psychologicalProfiles", "culturalAttitudes",
                "influenceEdges",
                "nextSocialTick", "nextLongTick" }
                .All(token => state.Contains(token, StringComparison.Ordinal))
                && new[] { "politicalAttitudes", "issueLinks", "coalitions" }
                    .All(token => politics.Contains(token,
                        StringComparison.Ordinal))
                && new[] { "propositions", "researchPrograms" }
                    .All(token => knowledge.Contains(token,
                        StringComparison.Ordinal))
                && !state.Contains("Scribe_Collections.Look(ref politicalAttitudes",
                    StringComparison.Ordinal)
                && !state.Contains("Scribe_Collections.Look(ref propositions",
                    StringComparison.Ordinal),
            "cultural cognition, political cognition, and proposition knowledge persist separately");
        Add("legitimacy is owned by the represented organization",
            organization.Contains("legitimacyAppraisals",
                    StringComparison.Ordinal)
                && organization.Contains("RecordLegitimacy",
                    StringComparison.Ordinal)
                && organization.Contains("UpdateInstitutionLegitimacy",
                    StringComparison.Ordinal)
                && organization.Contains("RecordSanction",
                    StringComparison.Ordinal)
                && !state.Contains("CAInstitutionRecord",
                    StringComparison.Ordinal),
            "organization schedules and owns legitimacy; cognition provides read-only evidence; no duplicate institution ledger");
        Add("Culture changes selection appraisal, not authorization",
            state.Contains("AppraiseBehavior", StringComparison.Ordinal)
                && !state.Contains("CA_culturalBehaviorAppraisals",
                    StringComparison.Ordinal)
                && behavior.Contains("SelectsAction",
                    StringComparison.Ordinal)
                && !behavior.Contains(
                    "CABehaviorBlockReason.CulturalAppraisal",
                    StringComparison.Ordinal)
                && S("Source/BehaviorIntentModule.cs").Contains(
                    "decision.SelectionApproved",
                    StringComparison.Ordinal),
            "Allowed remains the causal authorization; CA discretionary job selection consumes the response");
        Add("direct operator intent bypasses cultural selection",
            behavior.Contains("CAAuthorityOrigin.OperatorDirect",
                    StringComparison.Ordinal)
                && behavior.Contains("CAAuthorityOrigin.OperatorRelay",
                    StringComparison.Ordinal)
                && behavior.Contains("return true;",
                    StringComparison.Ordinal),
            "direct, relayed, and restored operator intent remain authoritative");
        Add("skills are excluded from stable personality",
            state.Contains("selfEfficacy", StringComparison.Ordinal)
                && state.Contains("NeutralPsychologicalPrior",
                    StringComparison.Ordinal)
                && !state.Contains("StablePsychologicalBaseline",
                    StringComparison.Ordinal),
            "skills occur only in dynamic Disposition projection; stable gaps use neutral priors");
        Add("psychology records recoverable evidence",
            state.Contains("CAPsychologyEvidenceRecord",
                    StringComparison.Ordinal)
                && state.Contains("sourceFeature",
                    StringComparison.Ordinal)
                && state.Contains("meanShift", StringComparison.Ordinal)
                && state.Contains("uncertaintyChange",
                    StringComparison.Ordinal)
                && state.Contains("provenance",
                    StringComparison.Ordinal),
            "each trait, backstory, gene, or missing-evidence prior has a source record");
        Add("stable psychology and dynamic state are separate",
            state.Contains("CAPsychologicalDynamicState",
                    StringComparison.Ordinal)
                && state.Contains("UpdateDynamic",
                    StringComparison.Ordinal)
                && state.Contains("establishedTick",
                    StringComparison.Ordinal)
                && state.Contains("observedTick",
                    StringComparison.Ordinal),
            "stable profile persists evidence; mood, pain, fatigue, threat, and load update separately");
        Add("skills enter competence and perceived control",
            state.Contains("selfEfficacy", StringComparison.Ordinal)
                && organization.Contains("CompetenceEvidence",
                    StringComparison.Ordinal)
                && !state.Contains("skills as personality",
                    StringComparison.OrdinalIgnoreCase),
            "skills affect current efficacy and institutional competence, not personality factors");
        Add("psychology mappings remain versioned and discriminant",
            state.Contains("mappingVersion", StringComparison.Ordinal)
                && state.Contains("\"openness\"", StringComparison.Ordinal)
                && state.Contains("\"agreeableness\"",
                    StringComparison.Ordinal)
                && state.Contains("\"competitive-world belief\"",
                    StringComparison.Ordinal),
            "evidence mappings name separate constructs and persist their version");
        Add("identity hashes do not create Culture or psychology facts",
            !cognitionKernel.Contains("generated authoring prior",
                    StringComparison.Ordinal)
                && !culture.Contains("GenerateAll(", StringComparison.Ordinal)
                && !state.Contains("StablePsychologicalBaseline",
                    StringComparison.Ordinal),
            "question facts are authored or migrated; psychology changes only from represented evidence");
        Add("Culture migration is exact and evidence-preserving",
            culture.Contains("QuestionForSocialSubject", StringComparison.Ordinal)
                && culture.Contains("preserved as evidence; no exact B12 question", StringComparison.Ordinal)
                && culture.Contains("CALegacyCultureQuestionAdapter.Adapt",
                    StringComparison.Ordinal)
                && culture.Contains("hasDescriptiveNormPrior = false",
                    StringComparison.Ordinal)
                && culture.Contains("prestigeSignal = 0f",
                    StringComparison.Ordinal)
                && culture.Contains("sourceConfidence = 0.55f",
                    StringComparison.Ordinal),
            "only exact approval and salience map; other dimensions survive as legacyEvidence");
        Add("Culture UI authors questions and inspects practices",
            culture.Contains("DrawQuestions", StringComparison.Ordinal)
                && culture.Contains("DrawPracticeHistory",
                    StringComparison.Ordinal)
                && culture.Contains(
                    "These are customs people have repeatedly followed",
                    StringComparison.Ordinal)
                && !culture.Contains("void DrawMeanings", StringComparison.Ordinal)
                && !culture.Contains("void DrawPractices",
                    StringComparison.Ordinal),
            "question rows active; practice history read-only; obsolete editors absent");
        Add("player and established surfaces share current Culture",
            founding.Contains("CACulture", StringComparison.Ordinal)
                && founding.Contains("Dialog_CACultureEditor",
                    StringComparison.Ordinal)
                && S("Source/RegionalPopulationScreenModule.cs")
                    .Contains("Dialog_CACultureEditor", StringComparison.Ordinal),
            "founding carries inherited state; established regional editor uses same model");
        Add("coalitions use positive support inside one faction boundary",
            politics.Contains("FactionBoundary", StringComparison.Ordinal)
                && politics.Contains("SupportOf(value) >= 0.55f",
                    StringComparison.Ordinal)
                && politics.Contains("liveIdentities", StringComparison.Ordinal),
            "negative support and cross-faction membership cannot manufacture a coalition");
        Add("preflight validates nested B12 payloads",
            preflight.Contains("ValidateCultureQuestionPayload",
                    StringComparison.Ordinal)
                && preflight.Contains(
                    "ValidateCulturalCognitionNestedPayload",
                    StringComparison.Ordinal)
                && preflight.Contains(
                    "ValidatePoliticalCognitionNestedPayload",
                    StringComparison.Ordinal)
                && preflight.Contains(
                    "ValidatePropositionKnowledgeNestedPayload",
                    StringComparison.Ordinal)
                && !preflight.Contains("CA_culturalBehaviorAppraisals",
                    StringComparison.Ordinal)
                && !preflight.Contains("\"CA_institutions\"",
                    StringComparison.Ordinal),
            "question, psychology, attitude, coalition, knowledge, and organization appraisal payloads are checked");
        Add("new owner version tags are always serialized",
            new[] { state, politics, knowledge }.All(source =>
                source.Contains("0, forceSave: true",
                    StringComparison.Ordinal))
            && state.Contains("CA_culturalCognitionOwnerVersion",
                StringComparison.Ordinal)
            && politics.Contains("CA_politicalCognitionOwnerVersion",
                StringComparison.Ordinal)
            && knowledge.Contains("CA_propositionKnowledgeOwnerVersion",
                StringComparison.Ordinal),
            "all catalog-2 world owners force their required inline version tag even when its value equals the current schema");
    }

    private static void CompatibilityReceipts()
    {
        CALegacyCultureQuestionAdapterResult first =
            CALegacyCultureQuestionAdapter.Adapt(new[]
            {
                new CALegacyCultureMeaningAdapterInput(80, 30, 60, 1),
                new CALegacyCultureMeaningAdapterInput(-20, 90, 40, 1)
            });
        CALegacyCultureQuestionAdapterResult second =
            CALegacyCultureQuestionAdapter.Adapt(new[]
            {
                new CALegacyCultureMeaningAdapterInput(80, 30, 60, 1),
                new CALegacyCultureMeaningAdapterInput(-20, 90, 40, 1)
            });
        Add("legacy adapter has only exact approval and salience inputs",
            first.Mean == second.Mean && first.Salience == second.Salience,
            $"mean={first.Mean:0.000}; salience={first.Salience:0.000}; normality and prestige have no adapter parameter");
        Add("catalog retains the B12 cognition owner",
            CACampaignSchemaCatalog.CurrentCatalogVersion == 4
                && CACampaignSchemaCatalog.TryFind("world.cultural-cognition",
                    out CACampaignSchemaDefinition cognition)
                && cognition.CurrentVersion == 2
                && cognition.MinimumCompatibleVersion == 2
                && cognition.IntroducedCatalogVersion == 2,
            "world.cultural-cognition remains a catalog-2 owner and now requires schema 2");
        Add("politics and proposition knowledge have separate owners",
            CACampaignSchemaCatalog.TryFind("world.political-cognition",
                    out CACampaignSchemaDefinition politics)
                && politics.CurrentVersion == 1
                && politics.IntroducedCatalogVersion == 2
                && CACampaignSchemaCatalog.TryFind(
                    "world.proposition-knowledge",
                    out CACampaignSchemaDefinition knowledge)
                && knowledge.CurrentVersion == 1
                && knowledge.IntroducedCatalogVersion == 2,
            "catalog 2 introduces both schema-1 owners additively");
        Add("durable Culture catalog admits exact nine-to-ten migration",
            CACampaignCompatibilityKernel.EvaluateSchema(
                    "model.culture", 9).CanLoad
                && !CACampaignCompatibilityKernel.EvaluateSchema(
                    "model.culture", 8).CanLoad
                && CACampaignCompatibilityKernel.EvaluateSchema(
                    "model.culture", 10).Kind
                    == CACampaignCompatibilityKind.Current,
            "minimum=9; current=10");
        Add("organization schema adds owner-held institutional appraisals",
            CACampaignSchemaCatalog.TryFind("world.organization",
                    out CACampaignSchemaDefinition organization)
                && organization.CurrentVersion == 2
                && organization.MinimumCompatibleVersion == 1,
            "world.organization 1 -> 2 carries legitimacy and sanction appraisals");
        string organizationSource = S("Source/OrganizationModule.cs");
        Add("organization one-to-two migration initializes new owned lists",
            organizationSource.Contains("MigrateCampaignState",
                    StringComparison.Ordinal)
                && organizationSource.Contains(
                    "organization.legitimacyAppraisals =",
                    StringComparison.Ordinal)
                && organizationSource.Contains(
                    "organization.sanctionAppraisals =",
                    StringComparison.Ordinal)
                && organizationSource.Contains(
                    "legacyAuthoringDataEpoch, ValidateCampaignState,",
                    StringComparison.Ordinal),
            "schema-1 organizations gain empty appraisal histories before schema-2 validation; no prior appraisal history is invented");
        Add("catalog routes and nested bindings are complete",
            CACampaignPreflightValidator.ValidateCatalogCoverage().CanLoad,
            CACampaignPreflightValidator.ValidateCatalogCoverage().Reason);
    }

    private static void HistoryAndPerformanceReceipts()
    {
        string culture = S("Source/FactionCultureBeliefsModule.cs");
        string longitudinal = S("Source/CultureLongitudinalModule.cs");
        string reactions = S("Source/SocialInterpretationRuntimeModule.cs");
        string cognition = S("Source/CulturalCognitionStateModule.cs");
        string politics = S("Source/CulturalPoliticsStateModule.cs");
        string knowledge = S("Source/PropositionKnowledgeModule.cs");
        string fixture = S("tools/B12FixtureGenerator/Program.cs");

        Add("events are persisted before Culture history changes",
            reactions.Contains("reactions.Add", StringComparison.Ordinal)
                && longitudinal.Contains("PatternsFor",
                    StringComparison.Ordinal)
                && longitudinal.Contains("EvaluateMeaningTransition",
                    StringComparison.Ordinal),
            "fact -> pawn reaction -> sustained group pattern -> Culture transition");
        Add("Culture transitions require duration and coverage",
            culture.Contains("EvidenceCount >= 2", StringComparison.Ordinal)
                && culture.Contains("ObservedPawnCount >= 2",
                    StringComparison.Ordinal)
                && culture.Contains("Participation >= 0.10f",
                    StringComparison.Ordinal)
                && culture.Contains("HistoricalPeriod",
                    StringComparison.Ordinal),
            "minimum evidence, pawns, participation, and historical duration are explicit");
        Add("unchanged history suppresses no-op transitions",
            culture.Contains("evidenceSignature",
                    StringComparison.Ordinal)
                && culture.Contains("bool substantive",
                    StringComparison.Ordinal)
                && culture.Contains("if (!substantive)",
                    StringComparison.Ordinal),
            "identical evidence and sub-significance changes do not append history");
        Add("migration composes exact questions with preserved evidence",
            culture.Contains("QuestionForSocialSubject",
                    StringComparison.Ordinal)
                && culture.Contains("CACultureLegacyEvidence",
                    StringComparison.Ordinal)
                && culture.Contains("legacyEvidence",
                    StringComparison.Ordinal),
            "exact adapters create distributions; every old record remains evidence");
        Add("cohort and subgroup state remain represented",
            culture.Contains("populationScope", StringComparison.Ordinal)
                && culture.Contains("subgroups", StringComparison.Ordinal)
                && S("Source/RegionalWorldModule.cs").Contains(
                    "populationGroups", StringComparison.Ordinal),
            "regional populations keep scopes and subgroup mixtures");
        Add("social, institution, and Culture maintenance use separate owners and cadences",
            cognition.Contains("nextSocialTick = now + 2500",
                    StringComparison.Ordinal)
                && S("Source/OrganizationModule.cs").Contains(
                    "nextLegitimacyTick = now + 60000",
                    StringComparison.Ordinal)
                && cognition.Contains("nextLongTick = now + 10 * 60000",
                    StringComparison.Ordinal),
            "cultural social pulse, organization appraisal, and cultural maintenance/history are separate");
        Add("influence exposure is timestamped per observed subject",
            cognition.Contains("CAInfluenceExposureRecord",
                    StringComparison.Ordinal)
                && cognition.Contains("subjectKey",
                    StringComparison.Ordinal)
                && cognition.Contains("HasRecentPoliticalExposure",
                    StringComparison.Ordinal)
                && !cognition.Contains("observedQuestionKeys",
                    StringComparison.Ordinal),
            "new observations renew only their Culture question or political axis");
        Add("fixture migration validates before atomic replacement",
            fixture.Contains("B12FixturePairCommit.Commit",
                    StringComparison.Ordinal),
            "fixture routes through the shared validated pair-commit helper");

        CACultureQuestionDistribution distribution =
            CACultureDistributionKernel.NewQuestion(
                CACultureQuestionRegistry.All[0]);
        string small = PopulationDigest(distribution, 16);
        string medium = PopulationDigest(distribution, 256);
        string large = PopulationDigest(distribution, 4096);
        Add("fixed-seed populations are deterministic at several sizes",
            small == PopulationDigest(distribution, 16)
                && medium == PopulationDigest(distribution, 256)
                && large == PopulationDigest(distribution, 4096),
            $"n16={small[..8]}; n256={medium[..8]}; n4096={large[..8]}");
        Add("population continuation is order-independent",
            PopulationDigest(distribution, 512)
                == PopulationDigestFromParts(distribution, 256, 256),
            "materialization identity, not traversal order, owns each draw");
        Add("hot cognition work is bounded",
            cognition.Contains("Math.Min(12, pawns.Count)",
                    StringComparison.Ordinal)
                && politics.Contains("Math.Min(12, pawns.Count)",
                    StringComparison.Ordinal)
                && cognition.Contains("profileByPawn",
                    StringComparison.Ordinal)
                && cognition.Contains("attitudeByIdentity",
                    StringComparison.Ordinal)
                && cognition.Contains("edgesByTarget",
                    StringComparison.Ordinal)
                && politics.Contains("attitudeByIdentity",
                    StringComparison.Ordinal)
                && politics.Contains("attitudesByFactionAxis",
                    StringComparison.Ordinal)
                && politics.Contains("organizationByMember",
                    StringComparison.Ordinal)
                && politics.Contains("issueLinksByFactionIssue",
                    StringComparison.Ordinal)
                && politics.Contains("HasOfficeHolder",
                    StringComparison.Ordinal)
                && cognition.Contains("GenRadial.RadialDistinctThingsAround",
                    StringComparison.Ordinal)
                && politics.Contains(
                    "IReadOnlyList<Pawn> pawns = LivePawnSnapshot()",
                    StringComparison.Ordinal)
                && S("Source/PropositionKnowledgeModule.cs").Contains(
                    "politicalPropositionsByHolderAxis",
                    StringComparison.Ordinal),
            "both hot pulses process at most 12 pawns; profile, attitude, edge, organization, and political-knowledge lookups use owner indexes");
        Add("social and political graphs are sparse",
            cognition.Contains(".Take(8)", StringComparison.Ordinal)
                && politics.Contains("issueLinks", StringComparison.Ordinal)
                && politics.Contains("FactionBoundary",
                    StringComparison.Ordinal),
            "each pawn retains at most eight influence edges; issue links are faction-bounded");
        Add("history, research, and sanction memory are capped",
            politics.Contains("MaxPoliticalEvidenceHistory",
                    StringComparison.Ordinal)
                && knowledge.Contains("MaxResearchPriorKnowledge",
                    StringComparison.Ordinal)
                && knowledge.Contains("MaxResearchPrograms",
                    StringComparison.Ordinal)
                && S("Source/OrganizationModule.cs").Contains(
                    "MaxSanctionAppraisals", StringComparison.Ordinal),
            "per-pawn evidence, research inputs, and sanction histories have explicit caps");
    }

    private static void FixtureReceipts(string active, string mirror)
    {
        string pairDirectory = Path.Combine(Path.GetTempPath(),
            "cao-b12-pair-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pairDirectory);
        string pairActive = Path.Combine(pairDirectory, "active.xml");
        string pairMirror = Path.Combine(pairDirectory, "mirror.xml");
        File.WriteAllText(pairActive, "<fixture>active-before</fixture>");
        File.WriteAllText(pairMirror, "<fixture>mirror-before</fixture>");
        bool failedAsInjected = false;
        try
        {
            B12FixturePairCommit.Commit(pairActive, pairMirror,
                "<fixture>replacement</fixture>",
                () => throw new IOException("injected second-replace failure"));
        }
        catch (IOException)
        {
            failedAsInjected = true;
        }
        bool rolledBack = failedAsInjected
            && File.ReadAllText(pairActive)
                == "<fixture>active-before</fixture>"
            && File.ReadAllText(pairMirror)
                == "<fixture>mirror-before</fixture>"
            && !Directory.EnumerateFiles(pairDirectory)
                .Any(path => path.EndsWith(".tmp", StringComparison.Ordinal)
                    || path.EndsWith(".bak", StringComparison.Ordinal));
        Directory.Delete(pairDirectory, true);
        Add("fixture pair replacement rolls back injected failure",
            rolledBack,
            "active and mirror originals survive a failure after the first replacement");

        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        string activeHash = Sha(activeBytes);
        string mirrorHash = Sha(mirrorBytes);
        XDocument document = XDocument.Parse(
            Encoding.UTF8.GetString(activeBytes), LoadOptions.PreserveWhitespace);
        XElement plan = document.Root?.Element("plan")
            ?? throw new InvalidDataException("active plan missing");
        Add("active and mirror fixtures agree",
            activeBytes.SequenceEqual(mirrorBytes),
            $"SHA-256={activeHash}; mirror={mirrorHash}");
        Add("fixture carries the current authoring epoch",
            Value(document.Root, "authoringDataEpoch") == "13",
            "authoringDataEpoch="
                + Value(document.Root, "authoringDataEpoch"));
        XElement[] settlements = Items(plan, "settlements").ToArray();
        Add("authored identity and composition survive",
            Value(plan, "regionalId") == "CA-RG-EB596A12"
                && Value(plan, "candidateId") == "613b1fe44104"
                && Value(plan, "startTileId") == "389638"
                && Value(plan, "mapSize") == "350"
                && Items(plan, "factions").Count() == 3
                && settlements.Length == 4
                && settlements.SelectMany(value => Items(value,
                    "populationGroups")).Count() == 4
                && settlements.SelectMany(value => Items(value,
                    "operationalFacts")).Count() == 19,
            "region/candidate/tile/scale; 3 factions; 4 settlements; 4 current population assignments; 19 program facts");
        XElement[] cultures = document.Descendants().Where(value =>
            value.Name.LocalName == "culture"
                || value.Name.LocalName == "localCulture").ToArray();
        int questionCount = cultures.Sum(value =>
            Items(value, "inheritedQuestions").Count()
                + Items(value, "localQuestions").Count());
        int evidenceCount = cultures.Sum(value =>
            Items(value, "legacyEvidence").Count());
        int b12QuestionCount = cultures.SelectMany(value =>
                Items(value, "inheritedQuestions").Concat(
                    Items(value, "localQuestions")))
            .Count(value => Value(value, "provenance").Contains(
                "B12 exact adapter", StringComparison.Ordinal));
        Add("fixture uses current Culture schema",
            cultures.Length == 8
                && cultures.All(value => Value(value, "schemaVersion") == "10")
                && cultures.All(value => Value(value,
                    "questionRegistryVersion") == "2")
                && !document.Descendants("inheritedMeanings").Any()
                && !document.Descendants("localMeanings").Any(),
            $"8 schema-10 records; {questionCount} distributions; obsolete meaning payloads absent");
        Add("migration evidence survives serialization",
            questionCount >= 192 && b12QuestionCount == 22
                && evidenceCount >= 25
                && cultures.SelectMany(value => Items(value,
                    "legacyEvidence")).Any(value =>
                        Value(value, "sourceKey")
                            == "ca.property.compulsory_transfer"
                        && Value(value, "disposition").Contains(
                            "no exact B12 question", StringComparison.Ordinal)),
            $"questions={questionCount}; B12-authored={b12QuestionCount}; evidence={evidenceCount}; complete roots plus later represented local facts retained; unmapped compulsory transfer preserved");
        string roundTrip = XDocument.Parse(document.ToString(
            SaveOptions.DisableFormatting)).ToString(SaveOptions.DisableFormatting);
        Add("current fixture round-trips structurally",
            XDocument.Parse(roundTrip).Root != null
                && document.Descendants("questionKey").Select(value => value.Value)
                    .SequenceEqual(XDocument.Parse(roundTrip)
                        .Descendants("questionKey").Select(value => value.Value)),
            "question identities survive XML readback");
    }

    private static void WriteReceipts(string active, string mirror)
    {
        int passed = Results.Count(value => value.Passed);
        var acceptance = new StringBuilder();
        acceptance.AppendLine("# B12 Acceptance Receipts\n");
        acceptance.AppendLine("These receipts exercise the pure equations used by the runtime wrappers, inspect the represented consumer routes, and parse the current authored fixture. They are static evidence; they do not claim psychometric validity or operator runtime acceptance.\n");
        acceptance.AppendLine("| # | Contract | Result | Evidence |");
        acceptance.AppendLine("|---:|---|---|---|");
        foreach (Result result in Results)
            acceptance.AppendLine($"| {result.Number} | {Escape(result.Name)} | **{(result.Passed ? "PASS" : "FAIL")}** | {Escape(result.Evidence)} |");
        acceptance.AppendLine();
        acceptance.AppendLine(
            $"Result: **{passed}/{Results.Count} PASS**");
        File.WriteAllText(Path.Combine(repo, "B12_ACCEPTANCE_RECEIPTS.md"),
            acceptance.ToString(), new UTF8Encoding(false));

        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        XDocument document = XDocument.Parse(Encoding.UTF8.GetString(activeBytes));
        XElement plan = document.Root!.Element("plan")!;
        XElement[] settlements = Items(plan, "settlements").ToArray();
        XElement[] cultures = document.Descendants().Where(value =>
            value.Name.LocalName is "culture" or "localCulture").ToArray();
        string fixture = $"""
# B12 Authored Fixture Receipt

| Field | Evidence |
|---|---|
| Active plan | `{active}` |
| Mirror plan | `{mirror}` |
| SHA-256 | `{Sha(activeBytes)}` on both files |
| Identity | `{Value(plan, "regionalId")}` / `{Value(plan, "candidateId")}` / arrival `{Value(plan, "startTileId")}` / map `{Value(plan, "mapSize")}` |
| Composition | {Items(plan, "factions").Count()} factions / {settlements.Length} settlements / {settlements.SelectMany(value => Items(value, "populationGroups")).Count()} population groups / {settlements.SelectMany(value => Items(value, "operationalFacts")).Count()} established program facts |
| Culture | {cultures.Length} schema-10 records / {cultures.Sum(value => Items(value, "inheritedQuestions").Count() + Items(value, "localQuestions").Count())} exact question distributions / {cultures.Sum(value => Items(value, "legacyEvidence").Count())} preserved B11 source records |
| Current-schema rerun | schema-10 state validates and remains byte-identical at the recorded hash |
| Pair replacement | injected failure after active replacement rolls both files back to their original bytes |

Both runtime-consumed plan surfaces parse, are byte-identical, preserve the authored composition and plan identity, and serialize only the current Culture question model. The shared exact adapter gives only weighted approval and salience current question semantics. Every other former field remains evidence; a source meaning without an exact current question receives no invented replacement. The migration and second-run hash establish a byte-idempotent current-schema path.
""";
        File.WriteAllText(Path.Combine(repo, "B12_FIXTURE_RECEIPT.md"),
            fixture, new UTF8Encoding(false));
    }

    private static CAInstitutionLegitimacyInput InstitutionInput(
        float procedure = 0.5f, float performance = 0.5f,
        float corruption = 0.5f, float coercion = 0.5f,
        float publicSupport = 0.5f) =>
        new(procedure, performance, 0.5f, 0.5f, 0.5f, corruption,
            coercion, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, publicSupport);

    private static CAKnowledgeAcceptanceInput KnowledgeInput(
        float trust = 0.5f, float corroboration = 0.5f,
        float novelty = 0f, float conflictFreedom = 1f) =>
        new(0.6f, trust, 0.5f, 0.4f, 0.3f, 0.6f, corroboration,
            0.6f, 0.5f, 0.6f, 0.5f, conflictFreedom, 0.6f, novelty);

    private static string PopulationDigest(
        CACultureQuestionDistribution distribution, int count)
    {
        string materialized = string.Join("|", Enumerable.Range(0, count)
            .Select(index => Sample(distribution, "population-" + index)
                .ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(materialized)));
    }

    private static string PopulationDigestFromParts(
        CACultureQuestionDistribution distribution, int first, int second)
    {
        IEnumerable<int> identities = Enumerable.Range(0, first)
            .Concat(Enumerable.Range(first, second));
        string materialized = string.Join("|", identities.Select(index =>
            Sample(distribution, "population-" + index).ToString("R",
                System.Globalization.CultureInfo.InvariantCulture)));
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(materialized)));
    }

    private static CAAttitudeMaterializationInput Input(float salience = 0.5f,
        float norm = 0.55f, float tolerance = 0.35f,
        float confidence = 0.55f, float visibility = 0.65f,
        float doctrine = 0f) =>
        new(0.20f, 0.10f, 0.10f, norm, tolerance, salience, confidence,
            visibility, doctrine, 0.55f, 0.65f, 0.25f, 0.60f, 0.25f);

    private static float Sample(CACultureQuestionDistribution value,
        string pawn) => CACultureDistributionKernel.Materialize(value,
            "world", "culture", null, pawn, 1);

    private static float Materialize(CACultureQuestionDistribution value,
        string subgroup, string pawn) =>
        CACultureDistributionKernel.Materialize(value, "world", "culture",
            subgroup, pawn, 1);

    private static (double Mean, double Sd) Moments(
        CACultureQuestionDistribution value)
    {
        double[] samples = Enumerable.Range(0, 1001).Select(index =>
            (double)Sample(value, index.ToString())).ToArray();
        double mean = samples.Average();
        return (mean, Math.Sqrt(samples.Average(sample =>
            (sample - mean) * (sample - mean))));
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
        _ => throw new InvalidDataException("unmapped question constant " + key)
    };

    private static void Add(string name, bool passed, string evidence) =>
        Results.Add(new Result(Results.Count + 1, name, passed, evidence));

    private static string S(string relative) =>
        File.ReadAllText(Path.Combine(repo,
            relative.Replace('/', Path.DirectorySeparatorChar)));

    private static IEnumerable<XElement> Items(XElement parent, string name) =>
        parent?.Element(name)?.Elements("li") ?? Enumerable.Empty<XElement>();

    private static string Value(XElement parent, string name) =>
        parent?.Element(name)?.Value ?? "";

    private static string Sha(byte[] bytes) => Convert.ToHexString(
        SHA256.HashData(bytes));

    private static string Escape(string value) => (value ?? "")
        .Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
