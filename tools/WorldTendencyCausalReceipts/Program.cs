using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using ColonistAwareness;

public static class Program
{
    private const int Seed = unchecked((int)0xCA0201);
    private static int checks;

    private sealed class Policy
    {
        internal float StitchedBand = 1f;
        internal int StitchedMin = 4;
        internal int StitchedMax = 5;
        internal float Concentration = 0.5f;
        internal float UrbanGrowth = 0.45f;
        internal float FrontierFrequency = 0.45f;
        internal float FrontierSize = 0.5f;
        internal float Unaffiliated = 0.45f;
        internal float SourceVariety = 0.5f;
        internal float LocalFaction = 0.45f;
        internal float Conflict = 0.4f;
        internal float OffMapActivity = 0.5f;

        internal Policy Copy() => (Policy)MemberwiseClone();
    }

    public sealed class PersistedReceipt
    {
        public int MajorSettlementCount { get; set; }
        public int PlacementTile { get; set; }
        public int FrontierCount { get; set; }
        public int FrontierHousehold { get; set; }
        public int FrontierMaterial { get; set; }
        public int SettlementScale { get; set; }
        public int Relation { get; set; }
    }

    private static int Main(string[] args)
    {
        try
        {
            string repository = args.Length > 0
                ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
            string mirror = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
            string keyed = args.Length > 2 ? Path.GetFullPath(args[2]) : null;
            var report = new StringBuilder();
            report.AppendLine("WORLD TENDENCIES FIXED-SEED CAUSAL RECEIPT");
            report.AppendLine("seed: 0xCA0201");

            CheckOneVariableReceipts(report);
            Require(CAWorldTendencyCausalKernel.GeneratedRelation(Seed, 1, 2,
                0.4f)
            == CAWorldTendencyCausalKernel.GeneratedRelation(Seed, 2, 1,
                0.4f),
            "generated relation changes when faction key order reverses");
            report.AppendLine("PASS relation identity -> canonical faction pair");
            CheckPersistedConsumption(report);
            CheckSourceContracts(repository, report);
            if (mirror != null && keyed != null)
                CheckFixture(mirror, keyed, report);

            report.AppendLine("result: PASS (" + checks + " assertions)");
            Console.Write(report.ToString());
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("result: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckOneVariableReceipts(StringBuilder report)
    {
        var baseline = new Policy();
        CheckDelta(report, "Generated land", baseline,
            p => p.StitchedBand = 0.05f,
            p => p.StitchedBand = 1f,
            "realizedStitchedFrequency", "requestedExtent");
        CheckDelta(report, "Stitched region size", baseline,
            p => { p.StitchedMin = 2; p.StitchedMax = 3; },
            p => { p.StitchedMin = 6; p.StitchedMax = 7; },
            "requestedExtent");
        CheckDelta(report, "Settlement concentration", baseline,
            p => p.Concentration = 0.1f,
            p => p.Concentration = 0.9f,
            "placementTile", "settlementPattern");
        CheckDelta(report, "Urban growth propensity", baseline,
            p => p.UrbanGrowth = 0.1f,
            p => p.UrbanGrowth = 0.9f,
            "urbanThreshold", "settlementScale");
        CheckDelta(report, "Frontier holding frequency", baseline,
            p => p.FrontierFrequency = 0.05f,
            p => p.FrontierFrequency = 0.95f,
            "frontierCount", "settlementPattern");
        CheckDelta(report, "Frontier holding size", baseline,
            p => p.FrontierSize = 0.05f,
            p => p.FrontierSize = 0.95f,
            "frontierHousehold", "frontierMaterial", "frontierForm");
        CheckDelta(report, "Unaffiliated residents", baseline,
            p => p.Unaffiliated = 0.05f,
            p => p.Unaffiliated = 0.95f,
            "unaffiliatedPercent");
        CheckDelta(report, "Reallocation source variety", baseline,
            p => p.SourceVariety = 0.05f,
            p => p.SourceVariety = 0.95f,
            "differentOwner");
        CheckDelta(report, "Local faction formation", baseline,
            p => p.LocalFaction = 0.05f,
            p => p.LocalFaction = 0.95f,
            "localFaction");
        CheckDelta(report, "Regional conflict", baseline,
            p => p.Conflict = 0.05f,
            p => p.Conflict = 0.95f,
            "relation", "relationPattern", "settlementPattern");
        CheckDelta(report, "Off-map activity rate", baseline,
            p => p.OffMapActivity = 0.1f,
            p => p.OffMapActivity = 0.9f,
            "offMapBudget");
    }

    private static void CheckDelta(StringBuilder report, string label,
        Policy baseline, Action<Policy> makeLow, Action<Policy> makeHigh,
        params string[] expected)
    {
        Policy low = baseline.Copy();
        Policy high = baseline.Copy();
        makeLow(low);
        makeHigh(high);
        Dictionary<string, string> left = Evaluate(low);
        Dictionary<string, string> right = Evaluate(high);
        string[] changed = left.Keys.Where(key => left[key] != right[key])
            .OrderBy(key => key).ToArray();
        string[] allowed = expected.OrderBy(key => key).ToArray();
        Require(changed.Length > 0, label + " changed no direct output");
        Require(changed.All(key => allowed.Contains(key)),
            label + " changed unrelated outputs: "
            + string.Join(",", changed.Except(allowed)));
        Require(expected.Any(key => changed.Contains(key)),
            label + " missed its intended output");
        Require(left["majorSettlementCount"]
                == right["majorSettlementCount"],
            label + " changed RimWorld's major-settlement pool count");
        report.AppendLine("PASS " + label + " -> "
            + string.Join(", ", changed));
    }

    private static Dictionary<string, string> Evaluate(Policy policy)
    {
        float low = Math.Clamp(policy.StitchedBand - 0.15f, 0f, 1f);
        float high = Math.Clamp(policy.StitchedBand + 0.15f, 0f, 1f);
        float realized = CAWorldTendencyCausalKernel.ResolveRange(Seed, 0,
            826351197, low, high);
        int extent = CAWorldTendencyCausalKernel.RequestedExtent(Seed, 4242,
            realized, policy.StitchedMin, policy.StitchedMax, 8);

        double near = CAWorldTendencyCausalKernel.PlacementScore(Seed, 11,
            2, 2, true, 1f, 1f, policy.Concentration);
        double far = CAWorldTendencyCausalKernel.PlacementScore(Seed, 22,
            2, 2, true, 4f, 4f, policy.Concentration);
        int placement = near >= far ? 11 : 22;

        int frontierCount = CAWorldTendencyCausalKernel
            .FrontierHoldingCount(Seed, 8, policy.FrontierFrequency);
        int household = CAWorldTendencyCausalKernel.FrontierHouseholdSize(
            Seed, 0, 3, policy.FrontierSize);
        int material = CAWorldTendencyCausalKernel.FrontierMaterialLevel(
            Seed, 0, 3, policy.FrontierSize);
        int form = CAWorldTendencyCausalKernel.FrontierForm(household,
            material);
        int threshold = CAWorldTendencyCausalKernel.UrbanThreshold(
            policy.UrbanGrowth);
        int scale = CAWorldTendencyCausalKernel.SettlementScale(700, 62,
            policy.UrbanGrowth);
        int relation = CAWorldTendencyCausalKernel.GeneratedRelation(Seed,
            1, 2, policy.Conflict);
        int relationPattern = CAWorldTendencyCausalKernel.RelationPattern(2,
            relation, relation == 0 ? 1 : 0);
        int settlementPattern = CAWorldTendencyCausalKernel.SettlementPattern(
            2, placement == 11 ? 1 : 2, placement == 11 ? 2 : 1,
            0, placement == 11 ? 1f : 4f, placement == 11,
            2, relationPattern, frontierCount);

        return new Dictionary<string, string>
        {
            ["majorSettlementCount"] = "4",
            ["realizedStitchedFrequency"] = realized.ToString("F6"),
            ["requestedExtent"] = extent.ToString(),
            ["placementTile"] = placement.ToString(),
            ["settlementPattern"] = settlementPattern.ToString(),
            ["urbanThreshold"] = threshold.ToString(),
            ["settlementScale"] = scale.ToString(),
            ["frontierCount"] = frontierCount.ToString(),
            ["frontierHousehold"] = household.ToString(),
            ["frontierMaterial"] = material.ToString(),
            ["frontierForm"] = form.ToString(),
            ["unaffiliatedPercent"] = CAWorldTendencyCausalKernel
                .UnaffiliatedPercent(policy.Unaffiliated, 2).ToString(),
            ["differentOwner"] = CAWorldTendencyCausalKernel
                .UseDifferentSettlementOwner(Seed, 3,
                    policy.SourceVariety).ToString(),
            ["localFaction"] = CAWorldTendencyCausalKernel
                .CreateLocalFaction(Seed, 3,
                    policy.LocalFaction).ToString(),
            ["relation"] = relation.ToString(),
            ["relationPattern"] = relationPattern.ToString(),
            ["offMapBudget"] = CAWorldTendencyCausalKernel
                .OffMapActivityBudget(10,
                    policy.OffMapActivity).ToString()
        };
    }

    private static void CheckPersistedConsumption(StringBuilder report)
    {
        Dictionary<string, string> realized = Evaluate(new Policy());
        var saved = new PersistedReceipt
        {
            MajorSettlementCount = int.Parse(realized["majorSettlementCount"]),
            PlacementTile = int.Parse(realized["placementTile"]),
            FrontierCount = int.Parse(realized["frontierCount"]),
            FrontierHousehold = int.Parse(realized["frontierHousehold"]),
            FrontierMaterial = int.Parse(realized["frontierMaterial"]),
            SettlementScale = int.Parse(realized["settlementScale"]),
            Relation = int.Parse(realized["relation"])
        };
        var serializer = new XmlSerializer(typeof(PersistedReceipt));
        using var stream = new MemoryStream();
        serializer.Serialize(stream, saved);
        stream.Position = 0;
        var read = (PersistedReceipt)serializer.Deserialize(stream);
        Require(read.MajorSettlementCount == saved.MajorSettlementCount
            && read.PlacementTile == saved.PlacementTile
            && read.FrontierCount == saved.FrontierCount
            && read.FrontierHousehold == saved.FrontierHousehold
            && read.FrontierMaterial == saved.FrontierMaterial
            && read.SettlementScale == saved.SettlementScale
            && read.Relation == saved.Relation,
            "realized state changed during serialization readback");

        var changedPolicy = new Policy
        {
            Concentration = 0.99f,
            UrbanGrowth = 0.99f,
            FrontierFrequency = 0.01f,
            FrontierSize = 0.01f,
            Conflict = 0.99f
        };
        _ = Evaluate(changedPolicy);
        Require(read.PlacementTile == saved.PlacementTile
            && read.FrontierCount == saved.FrontierCount
            && read.SettlementScale == saved.SettlementScale
            && read.Relation == saved.Relation,
            "a later tendency changed persisted realized state");
        report.AppendLine("PASS causal snapshot -> XML roundtrip and later-policy stability");
    }

    private static void CheckSourceContracts(string repository,
        StringBuilder report)
    {
        string setup = File.ReadAllText(Path.Combine(repository, "Source",
            "RegionalSetupModule.cs"));
        string model = File.ReadAllText(Path.Combine(repository, "Source",
            "RegionalSettlementModelModule.cs"));
        string frontier = File.ReadAllText(Path.Combine(repository, "Source",
            "FrontierModule.cs"));
        string faction = File.ReadAllText(Path.Combine(repository, "Source",
            "FactionCompositionModule.cs"));
        string composition = File.ReadAllText(Path.Combine(repository,
            "Source", "SettlementCompositionModule.cs"));
        string organization = File.ReadAllText(Path.Combine(repository,
            "Source", "OrganizationModule.cs"));
        string dialog = File.ReadAllText(Path.Combine(repository, "Source",
            "WorldGenerationDialogModule.cs"));
        string world = File.ReadAllText(Path.Combine(repository, "Source",
            "RegionalWorldModule.cs"));

        string[] fields =
        {
            "stitchedRegionFrequencyMin", "stitchedRegionSizeMin",
            "settlementConcentration", "urbanGrowthPropensity",
            "frontierHoldingFrequency", "frontierHoldingSize",
            "unaffiliatedPopulationShare", "reallocationSourceVariety",
            "localFactionChance", "regionalConflictChance",
            "offMapActivityRate"
        };
        foreach (string field in fields)
        {
            Require(setup.Contains(field), field + " is not persisted");
            Require(dialog.Contains(field), field + " has no visible control");
        }
        Require(setup.Contains("CurrentSchemaVersion = 2"),
            "current plan schema is not 2");
        Require(setup.Contains("PlacementScore")
            && setup.Contains("settlementConcentration"),
            "concentration does not own placement");
        Require(setup.Contains("WorldObjects")
            && setup.Contains("Settlements")
            && setup.Contains("pool.Count")
            && setup.Contains("ReallocatedFromWorldPool"),
            "major settlements do not follow RimWorld's settlement pool");
        Require(model.Contains("urbanGrowthPropensity")
            && model.Contains("UrbanSupport")
            && model.Contains("economicCapacity")
            && world.Contains("record.economicCapacity"),
            "urban propensity is not a threshold input");
        Require(model.Contains("frontierHoldingFrequency")
            && model.Contains("frontierHoldingSize"),
            "frontier realization does not consume both independent controls");
        Require(frontier.Contains("plan.frontierHoldings")
            && !frontier.Contains("frontierHoldingFrequency")
            && !frontier.Contains("frontierHoldingSize"),
            "frontier runtime rerolls policy instead of consuming realization");
        Require(organization.Contains("CA_frontierMapPlans")
            && organization.Contains("EnsureFrontierMapPlan")
            && organization.Contains("FrontierHoldingCount")
            && organization.Contains("FrontierHouseholdSize")
            && organization.Contains("FrontierMaterialLevel"),
            "standard maps do not persist the same frontier causes");
        Require(!frontier.Contains("return new CAFrontierHoldingPlan")
            && frontier.Contains("SpawnEstablishedShell")
            && frontier.Contains("SpawnMaterialDetails")
            && frontier.Contains("holding.form"),
            "frontier runtime bypasses saved form or material state");
        Require(model.Contains("!item.authorRelation")
            && model.Contains("regionalConflictChance"),
            "starting-region relation overrides do not replace the default");
        Require(faction.Contains("FactionRelationKind.Hostile")
            && !faction.Contains("regionalConflictChance"),
            "faction structure reads a tendency instead of saved relations");
        Require(composition.Contains("UnaffiliatedPercent")
            && !composition.Contains("reallocationSourceVariety"),
            "population composition crosses ownership controls");
        Require(organization.Contains("OffMapActivityBudget")
            && organization.Contains("CA_offMapActivityCursor"),
            "off-map activity has no processing-budget consumer");
        Require(world.Contains("record.residentPopulation")
            && world.Contains("record.realizedScale")
            && world.Contains("ConsumeReallocatedSources"),
            "map generation does not consume persisted settlement state");
        Require(setup.Contains("if (pair == null) continue;")
            && !setup.Contains("pair?.authorRelation != true"),
            "runtime skips persisted generated relations");
        Require(setup.Contains("ScenarioOverride")
            && setup.Contains("authorRelation = false")
            && model.Contains("!item.authorRelation"),
            "starting-region overrides do not replace generated defaults");
        Require(setup.Contains("OperatorEdited")
            && setup.Contains("&& !plan.confirmed"),
            "restore can replace the causes of a confirmed realization");
        Require(setup.Contains("if (!Pending.confirmed")
            && setup.Contains("ApplyWorldPolicyToDraft")
            && setup.Contains("ClearPolicyGeneratedPopulation"),
            "a Back round-trip can overwrite a confirmed policy or retain "
            + "generated population shares");
        Require(composition.Contains("ClearPolicyGeneratedPopulation")
            && composition.Contains("item.authored")
            && composition.Contains("populationGroups.Clear()"),
            "unaffiliated-share replacement does not preserve authored "
            + "population while clearing generated population");
        Require(model.Contains("List<int> factionKeys = (plan.factions")
            && model.Contains("RelationPattern(factionKeys.Count"),
            "regional relation pattern omits settlementless factions");
        Require(setup.Contains("operatorAuthored ? MintCandidateId()")
            && setup.Contains(": \"auto\""),
            "automatic regions use a nondeterministic candidate identity");
        Require(model.Contains("TryValidateRealization")
            && setup.Contains("TryValidateRealization")
            && world.Contains("TryValidateRealization"),
            "completion is trusted without structural validation");
        string[] obsolete =
        {
            "settlementPatternTendency", "cityFormationChance",
            "frontierSettlementFrequency", "frontierSettlementSize",
            "nearbyFactionVariety", "newLocalFactionChance",
            "startingConflictChance", "realizedFrontierHoldings"
        };
        string all = string.Join("\n", Directory.GetFiles(
            Path.Combine(repository, "Source"), "*.cs")
            .Select(File.ReadAllText));
        foreach (string name in obsolete)
            Require(!all.Contains(name), "obsolete source term remains: " + name);
        report.AppendLine("PASS source contracts -> 11 visible controls, 11 persisted fields, 11 consumers");
    }

    private static void CheckFixture(string mirrorPath, string keyedPath,
        StringBuilder report)
    {
        byte[] mirrorBytes = File.ReadAllBytes(mirrorPath);
        byte[] keyedBytes = File.ReadAllBytes(keyedPath);
        Require(mirrorBytes.SequenceEqual(keyedBytes),
            "active and keyed fixtures differ");
        XDocument document = XDocument.Load(mirrorPath,
            LoadOptions.PreserveWhitespace);
        XElement plan = document.Root?.Element("plan")
            ?? throw new InvalidOperationException("fixture plan missing");
        Require((string)document.Root?.Element("worldIdentity")
                == "alysaliu|1|Algorab Markab",
            "fixture world identity changed");
        Require((string)plan.Element("schemaVersion") == "2",
            "fixture schema is not current");
        XElement savedPolicy = plan.Element("worldPolicy");
        Require(savedPolicy != null
                && FloatValue(savedPolicy, "stitchedRegionFrequencyMin", -1f)
                    == 0.15f
                && FloatValue(savedPolicy, "stitchedRegionFrequencyMax", -1f)
                    == 0.55f
                && IntValue(savedPolicy, "stitchedRegionSizeMin", -1) == 3
                && IntValue(savedPolicy, "stitchedRegionSizeMax", -1) == 7
                && FloatValue(savedPolicy, "settlementConcentration", -1f)
                    == 0.5f
                && FloatValue(savedPolicy, "urbanGrowthPropensity", -1f)
                    == 0.35f
                && FloatValue(savedPolicy, "frontierHoldingFrequency", -1f)
                    == 0.35f
                && FloatValue(savedPolicy, "frontierHoldingSize", -1f) == 0.6f
                && FloatValue(savedPolicy, "unaffiliatedPopulationShare", -1f)
                    == 0.4f
                && FloatValue(savedPolicy, "reallocationSourceVariety", -1f)
                    == 0.55f
                && FloatValue(savedPolicy, "localFactionChance", -1f) == 0.12f
                && FloatValue(savedPolicy, "regionalConflictChance", -1f)
                    == 0.25f
                && FloatValue(savedPolicy, "offMapActivityRate", -1f) == 0.5f,
            "fixture world-tendency preset changed or became implicit");
        Require((string)plan.Element("regionalId") == "CA-RG-EB596A12"
            && (string)plan.Element("candidateId") == "613b1fe44104"
            && (string)plan.Element("startTileId") == "389638"
            && (string)plan.Element("mapSize") == "350",
            "fixture identity, arrival area, or map scale changed");
        Require(plan.Element("factions")?.Elements("li").Count() == 3,
            "fixture faction count is not 3");
        XElement[] settlements = plan.Element("settlements")
            ?.Elements("li").ToArray() ?? Array.Empty<XElement>();
        Require(settlements.Length == 4,
            "fixture settlement count is not 4");
        var expectedSettlements = new[]
        {
            (slot: "0", tile: "488022", faction: "1", name: "Megaeth"),
            (slot: "1", tile: "389640", faction: "1", name: "Red Cervexa"),
            (slot: "2", tile: "389637", faction: "2", name: "Tascan Bramble"),
            (slot: "3", tile: "170943", faction: "3", name: "Black Delta")
        };
        foreach (var expected in expectedSettlements)
        {
            XElement settlement = settlements.SingleOrDefault(item =>
                (string)item.Element("slot") == expected.slot);
            Require(settlement != null
                && (string)settlement.Element("memberTileId") == expected.tile
                && (string)settlement.Element("factionKey") == expected.faction
                && (string)settlement.Element("customName") == expected.name,
                "fixture settlement relationship changed for slot "
                + expected.slot);
        }
        foreach (XElement settlement in settlements)
        {
            string[] facts =
            {
                "residentPopulation", "landCapacity",
                "realizedAccessInfrastructure",
                "realizedServiceInfrastructure",
                "realizedCivicInfrastructure", "economicCapacity",
                "tradeConnectivity",
                "specialization", "historicalDevelopment", "urbanSupport",
                "realizedScale"
            };
            foreach (string fact in facts)
                Require(settlement.Element(fact) != null,
                    "fixture settlement lacks " + fact);
            int authoredFacilities = IntValue(settlement,
                "startingFacilityAuthoredMask", 0)
                & IntValue(settlement, "startingFacilityValues", 0);
            int expectedEconomy = CAWorldTendencyCausalKernel.EconomicCapacity(
                IntValue(settlement, "residentPopulation", -1),
                IntValue(settlement, "realizedCivicInfrastructure", -1),
                (authoredFacilities & 8) != 0,
                (authoredFacilities & 2) != 0);
            Require(IntValue(settlement, "economicCapacity", -1)
                    == expectedEconomy,
                "fixture economic capacity does not match saved causes");
            int expectedSupport = CAWorldTendencyCausalKernel.UrbanSupport(
                IntValue(settlement, "residentPopulation", -1),
                IntValue(settlement, "landCapacity", -1),
                IntValue(settlement, "realizedAccessInfrastructure", -1),
                IntValue(settlement, "realizedServiceInfrastructure", -1),
                IntValue(settlement, "realizedCivicInfrastructure", -1),
                expectedEconomy,
                IntValue(settlement, "tradeConnectivity", -1),
                IntValue(settlement, "specialization", -1),
                IntValue(settlement, "realizedRole", 0) == 1,
                IntValue(settlement, "historicalDevelopment", -1));
            Require(IntValue(settlement, "urbanSupport", -1)
                    == expectedSupport,
                "fixture urban support does not match saved settlement facts");
            float urbanGrowth = FloatValue(plan.Element("worldPolicy"),
                "urbanGrowthPropensity", 0.35f);
            Require(IntValue(settlement, "realizedScale", -1)
                    == CAWorldTendencyCausalKernel.SettlementScale(
                        IntValue(settlement, "residentPopulation", -1),
                        expectedSupport, urbanGrowth),
                "fixture scale does not match saved settlement facts");
        }
        Require((string)plan.Element("settlementRealizationComplete")
                == "True",
            "fixture realization is not complete");
        XElement[] holdings = plan.Element("frontierHoldings")
            ?.Elements("li").ToArray() ?? Array.Empty<XElement>();
        Require(holdings.Length == 3
            && holdings.All(item => (string)item.Element("memberTileId")
                != "389638"),
            "fixture frontier realization is missing or occupies arrival land");
        int holdingSeed = CAWorldTendencyCausalKernel.StableStringHash(
            StringValue(plan, "candidateId", "ca")
                + ":settlement-realization");
        float holdingSize = FloatValue(plan.Element("worldPolicy"),
            "frontierHoldingSize", 0.60f);
        for (int i = 0; i < holdings.Length; i++)
        {
            int land = IntValue(holdings[i], "landCapacity", -1);
            int household = CAWorldTendencyCausalKernel.FrontierHouseholdSize(
                holdingSeed, i, land, holdingSize);
            int material = CAWorldTendencyCausalKernel.FrontierMaterialLevel(
                holdingSeed, i, land, holdingSize);
            Require(IntValue(holdings[i], "key", -1) == i
                    && IntValue(holdings[i], "householdSize", -1) == household
                    && IntValue(holdings[i], "materialLevel", -1) == material
                    && IntValue(holdings[i], "form", -1)
                        == CAWorldTendencyCausalKernel.FrontierForm(
                            household, material)
                    && BoolValue(holdings[i], "factionless", false)
                        == (i % 2 == 0),
                "fixture frontier holding does not match saved causes");
        }
        Require(plan.Element("relations")?.Elements("li").Count() == 3,
            "fixture relation count is not 3");
        XElement hostile = plan.Element("relations")?.Elements("li")
            .SingleOrDefault(item =>
                (string)item.Element("leftFactionKey") == "1"
                && (string)item.Element("rightFactionKey") == "3");
        Require(hostile != null
            && (string)hostile.Element("relation") == "Hostile"
            && hostile.Element("authorRelation") == null,
            "authored hostile relation between factions 1 and 3 changed");
        Require(settlements.Sum(item => item.Element("populationGroups")
                ?.Elements("li").Count() ?? 0) == 9,
            "fixture population assignments changed");
        int[][] expectedShares =
        {
            new[] { 93, 7 }, new[] { 91, 9 }, new[] { 67, 23, 10 },
            new[] { 93, 7 }
        };
        for (int i = 0; i < settlements.Length; i++)
            Require(settlements[i].Element("populationGroups")
                    ?.Elements("li").Select(item =>
                        (int)item.Element("share")).SequenceEqual(
                            expectedShares[i]) == true,
                "fixture population shares changed for slot " + i);

        Require(IntValue(plan, "settlementRealizationSourceHash", 0)
                == FixtureSourceHash(plan),
            "fixture realization hash does not match its saved causes");
        string fixtureText = File.ReadAllText(mirrorPath);
        Require(!fixtureText.Contains("realizedFrontierHoldings")
            && !fixtureText.Contains("frontierSettlement"),
            "fixture retains obsolete schema fields");
        report.AppendLine("PASS current fixture -> schema 2, causal hash, 3 factions, 4 settlements, 9 population groups, mirrored XML readback");
    }

    private static int FixtureSourceHash(XElement plan)
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
                    0.12f) * 10000f));
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
                CAWorldTendencyCausalKernel.StableStringHash(
                    StringValue(faction, "customFactionDefName", "none")));
            foreach (XElement axis in faction.Element("factionStructure")
                ?.Elements("li").OrderBy(item =>
                    StringValue(item, "axisKey", "none"))
                ?? Enumerable.Empty<XElement>())
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    CAWorldTendencyCausalKernel.StableStringHash(
                        StringValue(axis, "axisKey", "none")),
                    CAWorldTendencyCausalKernel.StableStringHash(
                        StringValue(axis, "optionKey", "none")),
                    IntValue(axis, "source", 0));
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
                IntValue(settlement, "startingFacilityAuthoredMask", 0),
                IntValue(settlement, "startingFacilityValues", 0),
                IntValue(settlement, "accessInfrastructure", -1));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                IntValue(settlement, "serviceInfrastructure", -1),
                IntValue(settlement, "civicInfrastructure", -1),
                IntValue(settlement, "authoredForm", -1));
        }
        return hash;
    }

    private static int IntValue(XElement parent, string name, int fallback)
    {
        return int.TryParse((string)parent?.Element(name), out int value)
            ? value : fallback;
    }

    private static float FloatValue(XElement parent, string name,
        float fallback)
    {
        return float.TryParse((string)parent?.Element(name),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float value) ? value : fallback;
    }

    private static bool BoolValue(XElement parent, string name, bool fallback)
    {
        return bool.TryParse((string)parent?.Element(name), out bool value)
            ? value : fallback;
    }

    private static string StringValue(XElement parent, string name,
        string fallback)
    {
        string value = (string)parent?.Element(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static int RelationValue(string value)
    {
        return value == "Hostile" ? 0 : value == "Ally" ? 2
            : int.TryParse(value, out int parsed) ? parsed : 1;
    }

    private static int FactionSourceValue(string value)
    {
        return value == "NewWorldFaction" ? 1
            : int.TryParse(value, out int parsed) ? parsed : 0;
    }

    private static int SettlementOriginValue(string value)
    {
        return value == "ReallocatedFromWorldPool" ? 1
            : value == "ScenarioOverride" ? 2
            : int.TryParse(value, out int parsed) ? parsed : 0;
    }

    private static void Require(bool condition, string failure)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(failure);
    }
}
