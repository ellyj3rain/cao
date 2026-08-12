using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonistAwareness;

internal static class Program
{
    private static int checks;
    private static Dictionary<string, string> source;
    private static string allSource;
    private static XDocument active;
    private static XDocument mirror;

    private static readonly string[] Axes =
    {
        "leadership", "decisions", "participation", "dissent", "ownership",
        "economy", "work", "support", "membership", "status", "localOrder",
        "defense", "warConduct"
    };

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new ArgumentException(
                "usage: B8AuthoringReceipts <repo> <active-fixture> <mirror-fixture>");
            string repo = Path.GetFullPath(args[0]);
            source = Directory.GetFiles(Path.Combine(repo, "Source"), "*.cs")
                .ToDictionary(Path.GetFileName, File.ReadAllText,
                    StringComparer.OrdinalIgnoreCase);
            allSource = string.Join("\n", source.Values);
            active = XDocument.Load(Path.GetFullPath(args[1]));
            mirror = XDocument.Load(Path.GetFullPath(args[2]));

            DataFlush(repo);
            CultureT0();
            Political();
            Sociology();
            IconsAndFlow();

            if (checks != 57) throw new InvalidOperationException(
                "expected exactly 57 B8 receipts, observed " + checks);
            Console.WriteLine("result: PASS (57/57 B8 receipts)");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("result: FAIL after " + checks + " receipts");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void DataFlush(string repo)
    {
        string forbidden = "LegacyPracticeFields|legacyPresetName|migrationEvidence|"
            + "CACultureLegacyMigration|CACultureModel.Migrate";
        C(1, "obsolete Culture migration fields are absent",
            !Regex.IsMatch(allSource, "LegacyPracticeFields|legacyPresetName|migrationEvidence"));
        C(2, "Culture migration kernel is absent",
            !Regex.IsMatch(allSource, "CACultureLegacyMigration|CACultureModel\\.Migrate"));
        C(3, "profile alias and preset migration are absent",
            !Regex.IsMatch(allSource, "presetMask|legacyPresetName|parentProfile|profileAlias"));
        C(4, "preset is not an axis source", !allSource.Contains("CAAxisSource.Preset"));
        string composition = Read("FactionCompositionModule.cs");
        C(5, "built-in political profiles do not inherit",
            !composition.Contains("Parent") && !composition.Contains("parentKey"));
        string settings = Read("ModEntry.cs");
        string profiles = Read("AuthoringPresentationModule.cs");
        C(6, "old saved profiles cannot load as B8",
            settings.Contains("CAAuthoringDataEpoch.IsCurrent")
                && profiles.Contains("item.schemaVersion != CAUserCultureProfile.CurrentSchemaVersion")
                && profiles.Contains("item.schemaVersion != CAUserPoliticalProfile.CurrentSchemaVersion"));
        string epoch = Read("B8AuthoringDataEpochModule.cs");
        C(7, "incompatible epoch clears authoring owners",
            epoch.Contains("Current = 8")
                && Count(allSource, "RecordDiscard(") >= 6
                && Read("RegionalWorldModule.cs").Contains("worldPolicy = new CARegionalWorldPolicy()"));
        C(8, "fixture retains B8 authoring state in the current regional schema",
            Value(active.Root, "authoringDataEpoch") == "8"
                && Value(Plan(active), "schemaVersion") == "8"
                && !active.ToString().Contains("migrationEvidence")
                && File.Exists(Path.Combine(repo, "tools", "B9FixtureGenerator", "Program.cs")));
        C(9, "governed history remains while compatibility runtime is gone",
            Directory.Exists(Path.Combine(repo, "Batches"))
                && File.Exists(Path.Combine(repo, "BATCH_LOG.md"))
                && !Regex.IsMatch(string.Join("\n", source.Values), forbidden));
    }

    private static void CultureT0()
    {
        string culture = Read("FactionCultureBeliefsModule.cs");
        string founding = Read("PlayerFoundingPageModule.cs");
        string region = Read("RegionalPopulationScreenModule.cs");
        C(10, "name and visual alone fail Culture validation",
            culture.Contains("inheritedMeanings.Count == 0")
                && culture.Contains("inheritedPractices.Count == 0")
                && culture.Contains("Compose Culture with at least one social meaning"));

        var before = new List<CACulturalMeaningState>();
        string signatureA = CACulturalMeaningResolver.Fingerprint(before);
        before.Add(Meaning("demo.subject", "*", 40, 50, 20, 70));
        string signatureB = CACulturalMeaningResolver.Fingerprint(before);
        C(11, "one meaning changes Culture state signature", signatureA != signatureB);
        C(12, "visual signature is separate from sociological signature",
            culture.Contains("SociologicalSignature")
                && culture.Contains("VisualSignature")
                && Slice(culture, "internal static string SociologicalSignature",
                    "internal static string VisualSignature")
                    .IndexOf("sourceCultureDefName", StringComparison.Ordinal) < 0);
        C(13, "Culture profile contract round-trips meanings and practices",
            profilesRoundTrip());
        C(14, "Culture profile application preserves locality and history",
            Slice(culture, "internal void ApplyInheritedTemplate",
                    "internal bool Authored")
                .IndexOf("localityKey", StringComparison.Ordinal) < 0
                && Slice(culture, "internal void ApplyInheritedTemplate",
                    "internal bool Authored").IndexOf("transitions",
                        StringComparison.Ordinal) < 0);
        C(15, "same-name Cultures remain distinct by meaning",
            CACulturalMeaningResolver.Fingerprint(new[]
                { Meaning("demo.subject", "*", 50, 50, 0, 50) })
            != CACulturalMeaningResolver.Fingerprint(new[]
                { Meaning("demo.subject", "*", -50, 50, 0, 50) }));
        XElement playerCulture = Plan(active).Element("playerFounding")?.Element("culture");
        C(16, "Astropolitan is only the fixture visual tradition",
            Value(playerCulture, "sourceCultureDefName") == "Astropolitan"
                && Value(playerCulture, "name") != "Astropolitan"
                && Value(playerCulture, "name") != "Astropolitan culture");
        C(17, "player founding opens full Culture composer",
            founding.Contains("Compose Culture")
                && founding.Contains("Dialog_CACultureEditor")
                && culture.Contains("\"Social meanings\"")
                && culture.Contains("\"Inherited practices\"")
                && culture.Contains("\"Inspect causal effects...\""));
        C(18, "established factions use the same Culture composer",
            region.Contains("Compose Culture...")
                && region.Contains("Compose local Culture...")
                && region.Contains("CACultureAuthoringBoundary.EstablishedLocal")
                && culture.Contains("? culture.localMeanings : culture.inheritedMeanings")
                && culture.Contains("? culture.practices : culture.inheritedPractices")
                && region.Contains("new Dialog_CACultureEditor"));
        var inert = new CASocialSubjectDef
        {
            Owner = "demo", Key = "demo.social.inert", Label = "Inert",
            Description = "No source or consumer",
            SourceDomain = "", AuthoritativeSource = "", FactualCondition = "",
            Consumers = new List<string>()
        };
        C(19, "inert subject cannot enter Culture authoring",
            !inert.IsAuthorable(out _)
                && CASocialSubjectRegistry.Find("demo.social.inert") == null);
    }

    private static void Political()
    {
        string composition = Read("FactionCompositionModule.cs");
        string beliefs = Read("FactionCultureBeliefsModule.cs");
        string founding = Read("PlayerFoundingPageModule.cs");
        List<Dictionary<string, string>> profiles = ParseProfiles(composition);
        C(20, "every built-in political profile sets 13 axes",
            profiles.Count == 5 && profiles.All(value => value.Count == 13));
        C(21, "built-in profiles have no parent",
            !Slice(composition, "internal sealed class PoliticalProfile",
                "internal static List<string> Conflicts").Contains("Parent"));
        C(22, "built-in profiles contain no missing positions",
            profiles.All(value => Axes.All(value.ContainsKey)));
        C(23, "built-in profiles do not invoke random completion",
            !Slice(composition, "internal static readonly PoliticalProfile[] PoliticalProfiles",
                "internal static PoliticalProfile PoliticalProfileByKey").Contains("Generate"));
        C(24, "political profile vectors are unique",
            profiles.Select(Vector).Distinct(StringComparer.Ordinal).Count() == profiles.Count);
        C(25, "applying a profile authors every answer",
            beliefs.Contains("ApplyProfile")
                && Slice(beliefs, "internal static void ApplyProfile",
                    "internal static bool UsesProfile")
                    .Contains("CAAxisSource.Authored"));
        C(26, "world state stores vectors rather than profile identity",
            !Slice(beliefs, "public sealed class CAPoliticalBeliefs",
                "internal static class CACultureModel").Contains("profileKey")
                && !Slice(beliefs, "public sealed class CAPoliticalBeliefs",
                    "internal static class CACultureModel").Contains("presetName"));
        C(27, "political summary derives from axis vector",
            Slice(beliefs, "internal static string Summary(CAPoliticalBeliefs",
                "internal static string ProfileTraits").Contains("beliefs.positions"));
        C(28, "player has no Generate missing action",
            !founding.Contains("Generate missing") && !beliefs.Contains("Generate missing"));
        CAPoliticalDerivationContext context = CAPoliticalEvidenceContext.FromFacts(
            new CAPoliticalEvidenceFacts
            {
                Source = "receipt faction facts",
                MarketExchangeObserved = true,
                OrganizedWorkObserved = true,
                ProfessionalDefenseObserved = true,
                OpenRecruitment = true, HereditaryStatus = false
            });
        CAPoliticalDerivationResult generated = CAPoliticalDerivationKernel.Derive(
            "b8", context, PoliticalContracts());
        C(29, "NPC derivation records evidence for every generated axis",
            generated.Axes.Count == 13
                && generated.Axes.Any(value => value.SelectedOptionKey != null)
                && generated.Axes.Where(value => value.SelectedOptionKey != null)
                    .All(value => value.Evidence.Count > 0));
        var tie = new CAPoliticalDerivationContext();
        tie.Score("leadership", "council", 4, "equal council evidence");
        tie.Score("leadership", "whole", 4, "equal assembly evidence");
        CAPoliticalAxisDerivation tie1 = CAPoliticalDerivationKernel
            .Derive("stable", tie, PoliticalContracts()).Axes[0];
        CAPoliticalAxisDerivation tie2 = CAPoliticalDerivationKernel
            .Derive("stable", tie, PoliticalContracts()).Axes[0];
        bool unsupportedRemainsUnset = CAPoliticalDerivationKernel
            .Derive("stable", CAPoliticalEvidenceContext.FromFacts(
                new CAPoliticalEvidenceFacts()), PoliticalContracts()).Axes.All(value =>
                    value.SelectedOptionKey == null);
        string politicalContext = Read("B8PoliticalContextModule.cs");
        C(30, "ties are stable and neutral Culture is non-discriminating",
            tie1.TieBroken && tie1.SelectedOptionKey == tie2.SelectedOptionKey
                && unsupportedRemainsUnset
                && politicalContext.Contains("meaning.Approval == 0")
                && politicalContext.Contains("meaning.Approval > 0"));
        C(31, "all political axes stay independently editable",
            beliefs.Contains("foreach (string axisKey in group.Axes)")
                && beliefs.Contains("OpenAxis(def, target, belief)")
                && !Read("B8PoliticalDerivationKernel.cs")
                    .Contains("TradeNetwork")
                && !Read("B8PoliticalDerivationKernel.cs")
                    .Contains("PermanentEnemy")
                && !Read("B8PoliticalDerivationKernel.cs")
                    .Contains("CanRaid")
                && !politicalContext.Contains("techLevel")
                && !politicalContext.Contains("FactionRelationKind.Hostile"));
    }

    private static void Sociology()
    {
        var arbitrary = new CASocialSubjectDef
        {
            Owner = "demo", Key = "demo.social.reciprocal_feast",
            Label = "Reciprocal feast",
            Description = "A household repays food with a later feast.",
            SourceDomain = "demo", AuthoritativeSource = "demo event ledger",
            FactualCondition = "a reciprocal feast occurs",
            Applicability = "participants", CulturalEffect = "interpret reciprocity",
            Consumers = new List<string> { "demo institution" }
        };
        bool registered = CASocialSubjectRegistry.Register(arbitrary, out _);
        bool duplicateRejected = !CASocialSubjectRegistry.Register(arbitrary,
            out string duplicateFailure) && duplicateFailure.Contains("already");
        var resolved = CACulturalMeaningResolver.Resolve(new[]
            { Meaning(arbitrary.Key, "group:a", 55, 60, 30, 70) }, arbitrary.Key,
            "group:a");
        CAPersistedSocialReaction persisted = CASocialReactionPersistenceKernel.Record(
            Fact(arbitrary.Key, true, 0), "p1", "group:a", "demo:household",
            100, resolved, null);
        CAPawnSocialInterpretation response = persisted?.ToInterpretation();
        List<CASocialGroupPattern> pattern = CASocialPatternKernel.Aggregate(
            new[] { response, Interpret(arbitrary.Key, "p2", "group:a", 60000,
                true, resolved) }, 2);
        CACulturalMeaningTransitionResult transitioned =
            CACulturalMeaningTransitionKernel.Evaluate(Array.Empty<CACulturalMeaningState>(),
                pattern, 60000);
        C(32, "arbitrary subject crosses the generic pipeline",
            registered && duplicateRejected && persisted != null && response != null
                && persisted.FactIdentity == response.FactIdentity
                && pattern.Count == 1 && transitioned.Changed
                && Read("B8SocialInterpretationRuntimeModule.cs")
                    .Contains("CASocialReactionPersistenceKernel.Record")
                && !Read("B8SocialMeaningKernel.cs")
                    .Contains("demo.social.reciprocal_feast"));
        C(33, "uninformed pawn produces no response",
            Interpret(arbitrary.Key, "uninformed", "group:a", 1, false, resolved) == null);
        var opposed = new CACulturalMeaningResolution
        {
            SubjectKey = arbitrary.Key, Approval = -60, Normality = 40,
            Prestige = -30, Salience = 70,
            Contributions = new List<CACulturalMeaningContribution>
                { new() { Weight = 100 } },
            Provenance = new List<string> { "other:group" }
        };
        CAPawnSocialInterpretation response2 = Interpret(arbitrary.Key, "p3", "group:b",
            0, true, opposed);
        C(34, "different informed inputs yield different responses",
            response.Approval != response2.Approval);
        CAPawnSocialInterpretation conflict = CASocialInterpretationKernel.Interpret(
            new CAPawnSocialInterpretationInput
            {
                Fact = Fact(arbitrary.Key, true, 0), PawnIdentity = "p4",
                PopulationIdentity = "group:a", Culture = resolved,
                OtherContributions = new List<CASocialContribution>
                {
                    new() { Source = "Political Beliefs", Approval = -70,
                        Normality = 20, Prestige = -30, Salience = 70 }
                }
            });
        C(35, "contradictory contributions remain visible",
            conflict.InternalContradiction && conflict.Contributions.Count == 2);
        List<CASocialGroupPattern> divergent = CASocialPatternKernel.Aggregate(
            new[] { response, response2 }, 2);
        CAPawnSocialInterpretation repeatedPawn = Interpret(arbitrary.Key,
            "p1", "group:a", 120000, true, resolved);
        CASocialGroupPattern distinctParticipation = CASocialPatternKernel
            .Aggregate(new[] { response, repeatedPawn }, 10).Single();
        C(36, "aggregate dissonance follows response distribution",
            divergent.Any(value => value.CrossGroupDissonance > 0)
                && Math.Abs(distinctParticipation.Participation - 0.1f) < 0.001f);
        var minority = response2;
        minority.PopulationIdentity = "group:a";
        minority.InfluenceWeight = 500;
        List<CASocialGroupPattern> weighted = CASocialPatternKernel.Aggregate(
            new[] { response, Interpret(arbitrary.Key, "p5", "group:a", 0, true, resolved), minority }, 3);
        CACulturalMeaningResolution constituentWeighted =
            CACulturalMeaningResolver.Resolve(
                CACulturalMeaningResolver.ApplyConstituentShares(new[]
                {
                    Meaning(arbitrary.Key, "majority", 70, 50, 20, 60),
                    Meaning(arbitrary.Key, "minority", -70, 50, -20, 60)
                }, new Dictionary<string, int>
                    { ["majority"] = 91, ["minority"] = 9 }), arbitrary.Key);
        C(37, "high-status minority remains visible",
            (weighted.Single().InfluentialMinority
                || weighted.Single().Dispersion > 0.3f)
                && constituentWeighted.Approval > 0
                && constituentWeighted.Contributions.Count == 2);
        var prior = new[] { Meaning(arbitrary.Key, "group:a", 0, 10, 0, 10) };
        CACulturalMeaningTransitionResult changed =
            CACulturalMeaningTransitionKernel.Evaluate(prior, pattern, 60000);
        C(38, "repeated evidence can alter Culture T+1", changed.Changed);
        CASocialGroupPattern isolated = CASocialPatternKernel.Aggregate(
            new[]
            {
                Interpret(arbitrary.Key, "witness-a", "group:a", 0, true,
                    resolved),
                Interpret(arbitrary.Key, "witness-b", "group:a", 0, true,
                    resolved)
            }, 2).Single();
        C(39, "one isolated event does not rewrite Culture",
            isolated.EvidenceCount == 1
                && !CACulturalMeaningTransitionKernel.Evaluate(prior,
                    new[] { isolated }, 60000).Changed);
        C(40, "unchanged evidence creates no second transition",
            !CACulturalMeaningTransitionKernel.Evaluate(changed.Meanings, pattern, 120000).Changed);
        string runtime = Read("B8SocialInterpretationRuntimeModule.cs");
        C(41, "broken evidence run must qualify again",
            runtime.Contains("EvidenceRunGap = 2 * 60000")
                && runtime.Contains("runStart = index")
                && Read("CultureLongitudinalModule.cs")
                    .Contains("culture.localityKey"));
        var pluralPrior = new[]
        {
            Meaning(arbitrary.Key, "group:a", 0, 10, 0, 10),
            Meaning(arbitrary.Key, "group:b", -40, 70, -20, 65)
        };
        CACulturalMeaningTransitionResult plural =
            CACulturalMeaningTransitionKernel.Evaluate(pluralPrior, pattern, 60000);
        C(42, "plural constituent meaning survives transition",
            plural.Meanings.Any(value => value.PopulationScope == "group:b"));
        string effects = Read("PoliticalBeliefEffectsModule.cs");
        string spatial = Read("SettlementPlanningContextModule.cs");
        string organization = Read("OrganizationModule.cs");
        C(43, "Culture affects conflict without rewriting beliefs",
            effects.Contains("CASocialReactionWorldComponent")
                && !Read("B8SocialInterpretationRuntimeModule.cs").Contains("beliefs.CopyFrom"));
        C(44, "Culture ranks space without granting authority",
            spatial.Contains("CASocialSubjectRegistry.PublicGathering")
                && spatial.Contains("CASocialSubjectRegistry.DefendedBoundary")
                && !spatial.Contains("CreateOffice"));
        C(45, "Culture affects legitimacy without creating office",
            organization.Contains("CASocialSubjectRegistry.PublicGathering")
                && !Read("B8SocialMeaningKernel.cs").Contains("ThingDef"));
        C(46, "Culture affects response without creating knowledge",
            runtime.Contains("KnowledgeSource")
                && Read("PoliticalBeliefEffectsModule.cs")
                    .Contains("if (e.pawnsAnswered.Contains(pawnId)) continue")
                && runtime.Contains("act.Knows(knower.thingIDNumber)")
                && runtime.Contains("act.HowKnown(knower.thingIDNumber)")
                && Read("B8SocialMeaningKernel.cs").Contains("!fact.Known")
                && !runtime.Contains("pawnsAnswered.Add"));
        C(47, "native Ideoligion contributes on overlap",
            effects.Contains("Ideoligion")
                && runtime.Contains("CASocialSubjectRegistry.QuarterGiven")
                && Read("B8SocialMeaningKernel.cs")
                    .Contains("RegisterBuiltIn(QuarterGiven"));
        C(48, "non-Ideoligion subjects remain available",
            CASocialSubjectRegistry.Find(CASocialSubjectRegistry.PublicGathering) != null
                && CASocialSubjectRegistry.Find(CASocialSubjectRegistry.CompelledService) != null);
    }

    private static void IconsAndFlow()
    {
        string founding = Read("PlayerFoundingPageModule.cs");
        string authoring = Read("AuthoringComposerSupportModule.cs");
        string composition = Read("FactionCompositionModule.cs");
        string culture = Read("FactionCultureBeliefsModule.cs");
        string region = Read("RegionalPopulationScreenModule.cs");
        C(49, "Culture card has no abstract identity icon",
            Slice(founding, "private void DrawCultureCard", "private void DrawIdeoCard")
                .Contains("DrawSummary(rect, ref y, null"));
        C(50, "Political Beliefs card has no abstract identity icon",
            Slice(founding, "private void DrawPoliticalCard", "private void DrawArrangementCard")
                .Contains("DrawSummary(rect, ref y, null"));
        C(51, "political profiles have no derived stock icon",
            !Slice(authoring, "internal static List<CACreationChoice> PoliticalProfiles",
                "internal static string PoliticalIdentity").Contains("Icon =")
                && !Slice(composition, "internal sealed class PoliticalProfile",
                    "internal static List<string> Conflicts").Contains("Icon"));
        C(52, "Culture profiles omit native icons outside visual selector",
            !Slice(authoring, "internal static List<CACreationChoice> CultureProfiles",
                "internal static string CultureIdentity").Contains("Icon =")
                && Slice(culture, "private void OpenSourceCulture",
                    "private CAAxisSource FieldState").Contains("Icon = local.Icon"));
        C(53, "native Ideoligion retains its real icon",
            Slice(founding, "private void DrawIdeoCard", "private void DrawPoliticalCard")
                .Contains("ideo?.Icon"));
        C(54, "concrete icons remain available",
            Read("RegionalPopulationEditorsModule.cs").Contains("LivingIdeo?.Icon")
                && region.Contains("RelationIcon")
                && Read("CreationFlowUiModule.cs").Contains("Icon"));
        C(55, "founding confirmation validates visible objects",
            founding.Contains("DrawCultureCard") && founding.Contains("DrawPoliticalCard")
                && Read("PlayerFoundingStateModule.cs").Contains("TryValidate")
                && Read("PlayerFoundingStateModule.cs").Contains("Answer every political-belief question"));
        C(56, "all four founding drafts survive page navigation",
            founding.Contains("CAPlayerFoundingSession.Save()")
                && Read("PlayerFoundingStateModule.cs").Contains("public CACulture culture")
                && Read("PlayerFoundingStateModule.cs").Contains("nativeIdeoSignature")
                && Read("PlayerFoundingStateModule.cs").Contains("CAPoliticalBeliefs politicalBeliefs")
                && Read("PlayerFoundingStateModule.cs").Contains("CAFoundingArrangement arrangement"));
        C(57, "Starting Region retains map and confirmation path",
            region.Contains("CARegionMapWidget.Draw")
                && region.Contains("protected override bool CanDoNext()")
                && region.Contains("PrepareNext(false)")
                && Factions(active).Count == 3 && Settlements(active).Count == 4
                && Cultures(active).Count == 8
                && SettlementConstituentsAreCurrent(active)
                && HasSameSubjectConstituentDisagreement(active)
                && !Cultures(active).SelectMany(value => value
                    .Element("transitions")?.Elements("li")
                        ?? Enumerable.Empty<XElement>()).Any()
                && Canonical(active) == Canonical(mirror));
    }

    private static bool profilesRoundTrip()
    {
        var root = new XElement("profile",
            new XElement("meanings", new XElement("li",
                new XElement("subjectKey", "demo.subject"), new XElement("approval", 42))),
            new XElement("practices", new XElement("li",
                new XElement("subjectKey", "demo.practice"), new XElement("strength", 51))));
        XElement read = XElement.Parse(root.ToString(SaveOptions.DisableFormatting));
        return read.Element("meanings")?.Elements("li").Count() == 1
            && read.Element("practices")?.Elements("li").Count() == 1;
    }

    private static CAPawnSocialInterpretation Interpret(string subject,
        string pawn, string population, int tick, bool known,
        CACulturalMeaningResolution culture) => CASocialInterpretationKernel.Interpret(
            new CAPawnSocialInterpretationInput
            {
                Fact = Fact(subject, known, tick), PawnIdentity = pawn,
                PopulationIdentity = population, Culture = culture
            });
    private static CASocialFactContext Fact(string subject, bool known, int tick)
        => new() { SubjectKey = subject, FactIdentity = subject + ":" + tick,
            Known = known, Tick = tick, KnowledgeSource = known ? "witness" : null };
    private static CACulturalMeaningState Meaning(string key, string scope,
        int approval, int normality, int prestige, int salience) => new()
        { SubjectKey = key, PopulationScope = scope, Approval = approval,
          Normality = normality, Prestige = prestige, Salience = salience,
          Weight = 100, Provenance = "receipt", SourceIdentity = "B8" };

    private static List<CAPoliticalAxisContract> PoliticalContracts()
        => Axes.Select(axis => new CAPoliticalAxisContract
        {
            AxisKey = axis,
            OptionKeys = axis switch
            {
                "leadership" => new() { "single", "council", "whole", "federated", "none" },
                "decisions" => new() { "decree", "majority", "consensus", "custom" },
                "participation" => new() { "universal", "members", "standing", "heads" },
                "dissent" => new() { "plural", "majoritarian", "orthodoxy", "customary" },
                "ownership" => new() { "private", "cooperative", "common", "state", "mixed" },
                "economy" => new() { "market", "planned", "communal", "mixed" },
                "work" => new() { "contract", "organized", "duty", "household" },
                "support" => new() { "private", "public", "communal", "charitable", "mixed" },
                "membership" => new() { "open", "vetted", "hereditary", "closed" },
                "status" => new() { "equal", "earned", "hereditary", "castes" },
                "localOrder" => new() { "none", "watch", "constabulary", "rulers" },
                "defense" => new() { "none", "levy", "militia", "professional", "caste" },
                _ => new() { "quarter", "strength", "combatants" }
            }
        }).ToList();

    private static List<Dictionary<string, string>> ParseProfiles(string text)
    {
        string block = Slice(text,
            "internal static readonly PoliticalProfile[] PoliticalProfiles",
            "private static PoliticalProfile Profile");
        var matches = Regex.Matches(block,
            "Profile\\(\"[^\"]+\",\\s*\"[^\"]+\",\\s*\"[^\"]+\",(?<values>.*?)\\)",
            RegexOptions.Singleline);
        var result = new List<Dictionary<string, string>>();
        foreach (Match match in matches)
        {
            string[] values = Regex.Matches(match.Groups["values"].Value, "\"([^\"]+)\"")
                .Select(value => value.Groups[1].Value).ToArray();
            if (values.Length != Axes.Length) continue;
            result.Add(Axes.Zip(values).ToDictionary(pair => pair.First,
                pair => pair.Second, StringComparer.Ordinal));
        }
        return result;
    }
    private static string Vector(Dictionary<string, string> profile)
        => string.Join("|", Axes.Select(axis => profile[axis]));
    private static string DefaultOption(string axis) => axis switch
    {
        "decisions" => "majority", "participation" => "universal",
        "dissent" => "plural", "ownership" => "mixed", "economy" => "mixed",
        "work" => "organized", "support" => "public", "membership" => "open",
        "status" => "equal", "localOrder" => "watch", "defense" => "militia",
        "warConduct" => "quarter", _ => "council"
    };

    private static string Read(string name) => source.TryGetValue(name, out string value)
        ? value : throw new InvalidDataException("missing source " + name);
    private static int Count(string value, string token)
        => Regex.Matches(value, Regex.Escape(token)).Count;
    private static string Slice(string value, string start, string end)
    {
        int from = value.IndexOf(start, StringComparison.Ordinal);
        int to = from < 0 ? -1 : value.IndexOf(end, from + start.Length,
            StringComparison.Ordinal);
        if (from < 0 || to <= from) throw new InvalidDataException(
            "missing source slice " + start);
        return value.Substring(from, to - from);
    }
    private static XElement Plan(XDocument document) => document.Root?.Element("plan")
        ?? throw new InvalidDataException("fixture plan missing");
    private static List<XElement> Factions(XDocument document)
        => Plan(document).Element("factions")?.Elements("li").ToList() ?? new();
    private static List<XElement> Settlements(XDocument document)
        => Plan(document).Element("settlements")?.Elements("li").ToList() ?? new();
    private static List<XElement> Cultures(XDocument document)
        => Plan(document).Descendants().Where(value => value.Name.LocalName
            is "culture" or "localCulture").ToList();
    private static bool SettlementConstituentsAreCurrent(XDocument document)
    {
        List<XElement> local = Settlements(document).Select(value =>
            value.Element("localCulture")).Where(value => value != null).ToList();
        return local.Count == 4 && local.All(culture =>
        {
            List<XElement> values = culture.Element("constituents")?
                .Elements("li").ToList() ?? new List<XElement>();
            return values.Count > 0
                && values.Sum(value => int.TryParse(Value(value, "share"),
                    out int share) ? share : 0) == 100
                && values.All(value => !Value(value, "cultureId")
                    .StartsWith("population:", StringComparison.Ordinal));
        }) && local.Any(culture => culture.Element("constituents")
            ?.Elements("li").Count() > 1);
    }
    private static bool HasSameSubjectConstituentDisagreement(
        XDocument document)
    {
        foreach (XElement culture in Settlements(document).Select(value =>
            value.Element("localCulture")).Where(value => value != null))
        {
            HashSet<string> constituentIds = (culture.Element("constituents")?
                    .Elements("li") ?? Enumerable.Empty<XElement>())
                .Select(value => Value(value, "cultureId"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
            foreach (IGrouping<string, XElement> subject in
                (culture.Element("localMeanings")?.Elements("li")
                    ?? Enumerable.Empty<XElement>()).GroupBy(value =>
                        Value(value, "subjectKey")))
            {
                List<XElement> scoped = subject.Where(value => constituentIds
                    .Contains(Value(value, "populationScope"))).ToList();
                if (scoped.Select(value => Value(value, "populationScope"))
                        .Distinct(StringComparer.Ordinal).Count() >= 2
                    && scoped.Select(value => Value(value, "approval"))
                        .Distinct(StringComparer.Ordinal).Count() >= 2)
                    return true;
            }
        }
        return false;
    }
    private static string Value(XElement parent, string name)
        => (string)parent?.Element(name) ?? "";
    private static string Canonical(XDocument document)
        => document.Root?.ToString(SaveOptions.DisableFormatting) ?? "";
    private static void C(int number, string label, bool condition)
    {
        checks++;
        if (number != checks) throw new InvalidOperationException(
            "receipt order mismatch: " + number + " after " + checks);
        if (!condition) throw new InvalidOperationException(
            "B8-" + number.ToString("D2") + " failed: " + label);
        Console.WriteLine("PASS B8-" + number.ToString("D2") + " " + label);
    }
}
