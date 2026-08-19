using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ColonistAwareness;

internal static class Program
{
    private sealed record Result(string Name, bool Passed, string Evidence);
    private static readonly List<Result> Results = new();

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: B14SettlementEnvironmentReceipts "
                + "<repo> <decompiled-root>");
            return 1;
        }
        string repo = Path.GetFullPath(args[0]);
        string decompiled = Path.GetFullPath(args[1]);
        Run(repo, decompiled);
        int passed = Results.Count(result => result.Passed);
        foreach (Result result in Results)
            Console.WriteLine((result.Passed ? "PASS " : "FAIL ")
                + result.Name + ": " + result.Evidence);
        Console.WriteLine($"B14 settlement environment acceptance: {passed}/"
            + $"{Results.Count} PASS");
        WriteReport(repo);
        return passed == Results.Count ? 0 : 3;
    }

    private static void Run(string repo, string decompiled)
    {
        float temperateFood = CASettlementEnvironmentCausalKernel.FoodSupport(
            6, 0.65f, 1f);
        float desertFood = CASettlementEnvironmentCausalKernel.FoodSupport(
            12, 0.05f, 0.25f);
        float extremeFood = CASettlementEnvironmentCausalKernel.FoodSupport(
            12, 0.002f, 0f);
        int temperate = CASettlementEnvironmentCausalKernel.Capacity(3, 6,
            0.65f, 1f, temperateFood, 0.2f, false);
        int desert = CASettlementEnvironmentCausalKernel.Capacity(3, 12,
            0.05f, 0.25f, desertFood, 0.4f, false);
        int extreme = CASettlementEnvironmentCausalKernel.Capacity(3, 12,
            0.002f, 0f, extremeFood, 0.6f, true);
        int ice = CASettlementEnvironmentCausalKernel.Capacity(3, 0,
            0f, 0f, 0f, 0.8f, true);
        Add("same terrain responds to different environments",
            temperate == 3 && desert == 2 && extreme == 1 && ice == 1,
            $"temperate={temperate}; desert={desert}; extreme={extreme}; "
            + $"ice={ice}");

        int mountain = CASettlementEnvironmentCausalKernel.Capacity(1, 6,
            0.65f, 1f, temperateFood, 0.2f, false);
        int water = CASettlementEnvironmentCausalKernel.Capacity(0, 12,
            1f, 1f, 1f, 0f, false);
        Add("terrain and water remain independent hard constraints",
            mountain == 1 && water == 0,
            $"mountain={mountain}; unusable={water}");

        float mild = CASettlementEnvironmentCausalKernel
            .SeasonalThermalPressure(-5f, 32f);
        float severe = CASettlementEnvironmentCausalKernel
            .SeasonalThermalPressure(-45f, 32f);
        int severeCapacity = CASettlementEnvironmentCausalKernel.Capacity(3,
            6, 0.65f, 1f, temperateFood, severe, false);
        Add("seasonal exposure changes the intended capacity only",
            mild > 0f && mild < severe && severeCapacity == 1
            && Math.Abs(temperateFood
                - CASettlementEnvironmentCausalKernel.FoodSupport(
                    6, 0.65f, 1f)) < 0.00001f,
            $"mild={mild:F2}; severe={severe:F2}; capacity={severeCapacity}; "
            + $"food={temperateFood:F4}");

        CAHabitatRequirementProfile mildHabitat =
            CAHabitatViabilityCausalKernel.Requirements(
                new CAHabitatEnvironmentInput
                {
                    GrowingTwelfths = 8,
                    FoodSupport = 0.75f
                });
        CAHabitatRequirementProfile darkHabitat =
            CAHabitatViabilityCausalKernel.Requirements(
                new CAHabitatEnvironmentInput
                {
                    GrowingTwelfths = 0,
                    FoodSupport = 0.05f,
                    PermanentDarkness = true
                });
        CAHabitatRequirementProfile vacuumHabitat =
            CAHabitatViabilityCausalKernel.Requirements(
                new CAHabitatEnvironmentInput
                {
                    GrowingTwelfths = 0,
                    FoodSupport = 0f,
                    Vacuum = true
                });
        CAHabitatViabilityResult mildFrontier =
            CAHabitatViabilityCausalKernel.Evaluate(mildHabitat,
                CAHabitatViabilityCausalKernel.FrontierCapability(0));
        CAHabitatViabilityResult darkPrimitive =
            CAHabitatViabilityCausalKernel.Evaluate(darkHabitat,
                CAHabitatViabilityCausalKernel.FrontierCapability(0));
        CAHabitatViabilityResult darkIndustrial =
            CAHabitatViabilityCausalKernel.Evaluate(darkHabitat,
                CAHabitatViabilityCausalKernel.FrontierCapability(2));
        CAHabitatViabilityResult vacuumSpacer =
            CAHabitatViabilityCausalKernel.Evaluate(vacuumHabitat,
                CAHabitatViabilityCausalKernel.FrontierCapability(3));
        Add("habitat requirements gate frontier capability",
            mildFrontier.Viable && !darkPrimitive.Viable
            && darkIndustrial.Viable && !vacuumSpacer.Viable
            && (darkHabitat.RequirementMask
                & (int)CAHabitatRequirement.ArtificialLight) != 0
            && (vacuumHabitat.RequirementMask
                & (int)CAHabitatRequirement.BreathableInterior) != 0,
            "mild tier-0=" + mildFrontier.Viable
                + "; dark tier-0=" + darkPrimitive.Viable
                + "; dark tier-2=" + darkIndustrial.Viable
                + "; vacuum tier-3 without materialized life support="
                + vacuumSpacer.Viable);

        CAHabitatRequirementProfile endemicDisease =
            CAHabitatViabilityCausalKernel.Requirements(
                new CAHabitatEnvironmentInput
                {
                    GrowingTwelfths = 8,
                    FoodSupport = 0.75f,
                    DiseasePerYear = 2.2f
                });
        int diseaseDelta = endemicDisease.RequirementMask
            & ~mildHabitat.RequirementMask;
        Add("one environmental cause adds only its owned requirement",
            diseaseDelta == (int)CAHabitatRequirement.MedicalCare,
            "endemic disease delta mask=" + diseaseDelta
                + "; expected medical="
                + (int)CAHabitatRequirement.MedicalCare);

        CAHabitatRequirementProfile pollutedHabitat =
            CAHabitatViabilityCausalKernel.Requirements(
                new CAHabitatEnvironmentInput
                {
                    GrowingTwelfths = 8,
                    FoodSupport = 0.75f,
                    PollutionPressure = 0.25f
                });
        CAHabitatViabilityResult pollutedIndustrial =
            CAHabitatViabilityCausalKernel.Evaluate(pollutedHabitat,
                CAHabitatViabilityCausalKernel.FrontierCapability(2));
        Add("unmaterialized hazard protection fails closed",
            !pollutedIndustrial.Viable
            && pollutedIndustrial.Missing(
                CAHabitatRequirement.HazardProtection),
            "industrial capability without apparel, filtration, or a "
                + "defended envelope remains nonviable");

        CAHabitatViabilityResult pollutedSettlementPotential =
            CAHabitatViabilityCausalKernel.Evaluate(pollutedHabitat,
                CAHabitatViabilityCausalKernel
                    .SettlementPotentialCapability(2));
        Add("advanced settlement potential remains distinct from frontier",
            pollutedSettlementPotential.Viable
            && !pollutedIndustrial.Viable,
            "industrial populations may author the required established "
                + "settlement programs, while the current compact frontier "
                + "materializer cannot claim unbuilt hazard protection");

        var merelyCompact = new CAAutonomousBuildingPatternEvidence
        {
            ValidGround = true,
            TerrainFit = 80,
            FunctionalAdjacency = 90,
            Throughput = 4,
            Circulation = 65,
            Expansion = 35,
            VisualOrder = 25,
            MaterialCost = 12,
            StableOrder = 2
        };
        var composed = new CAAutonomousBuildingPatternEvidence
        {
            ValidGround = true,
            TerrainFit = 80,
            FunctionalAdjacency = 90,
            Throughput = 4,
            Circulation = 78,
            Expansion = 50,
            EnvironmentalBuffer = 12,
            DefensiveSeparation = 10,
            VisualOrder = 90,
            MaterialCost = 12,
            StableOrder = 1
        };
        Add("efficient and ordered construction evidence composes",
            CAAutonomousBuildingPatternKernel.PreferFrontier(composed,
                merelyCompact),
            "composed candidate preserves equal terrain, adjacency, "
                + "throughput, and cost while improving circulation, "
                + "expansion, buffering, defense, and visual order");

        string pure = File.ReadAllText(Path.Combine(repo, "Source",
            "SettlementEnvironmentCausalKernel.cs"));
        Add("environment kernel cannot author social state",
            !pure.Contains("CACulture") && !pure.Contains("Ideo")
            && !pure.Contains("Political") && !pure.Contains("Program")
            && !pure.Contains("Faction"),
            "pure kernel accepts terrain, growing, ecology, and temperature "
            + "facts only");

        string adapter = File.ReadAllText(Path.Combine(repo, "Source",
            "SettlementEnvironmentModule.cs"));
        string model = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalSettlementModelModule.cs"));
        string setup = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalSetupModule.cs"));
        Add("world facts feed one shared settlement capacity",
            adapter.Contains("TwelfthsInAverageTemperatureRange")
            && adapter.Contains("biome.plantDensity")
            && adapter.Contains("biome.forageability")
            && adapter.Contains("biome.diseaseMtbDays")
            && adapter.Contains("surface.rainfall")
            && model.Contains("CASettlementEnvironment.ForTile(tileId)"
                + ".LandCapacity")
            && setup.Contains("CASettlementEnvironment.ForTile(tileId)"
                + ".LandCapacity"),
            "RimWorld season and biome facts -> shared environment adapter "
            + "-> realization and placement capacity");

        Add("environment participates in deterministic realization identity",
            model.Contains("settlement.memberTileId).SourceHash")
            && adapter.Contains("surface.Mutators.Select")
            && adapter.Contains("facts.SourceHash = SourceHash"),
            "member biome, climate, ecology, and mutators enter the saved "
            + "settlement realization source hash; no random draw exists");

        string planning = File.ReadAllText(Path.Combine(repo, "Source",
            "SettlementPlanningContextModule.cs"));
        string home = File.ReadAllText(Path.Combine(repo, "Source",
            "AutonomousHomeModule.cs"));
        Add("live regional planning reads represented source tiles",
            adapter.Contains("projection?.MemberTileAt(cell)")
            && planning.Contains("CASettlementEnvironment.ForMap(map, "
                + "environmentalCells)")
            && planning.Contains("environmentalThermalPressure")
            && planning.Contains("room.UsesOutdoorTemperature"),
            "projected cells -> constituent tiles -> saved planning context "
            + "-> thermally informed placement");
        Add("existing proactive behavior consumes the profile",
            home.Contains("PreferIndoorActivity(planner)")
            && home.Contains("&& !preferIndoor")
            && home.Contains("spatial.home_essentials")
            && planning.Contains("pawn.SafeTemperatureRange()"),
            "Home planning prefers indoor recreation under seasonal exposure "
            + "and ranks essential sleep placement by current room safety");

        string frontier = File.ReadAllText(Path.Combine(repo, "Source",
            "FrontierModule.cs"));
        string habitat = File.ReadAllText(Path.Combine(repo, "Source",
            "HabitatViabilityModule.cs"));
        string organization = File.ReadAllText(Path.Combine(repo, "Source",
            "OrganizationModule.cs"));
        string pattern = File.ReadAllText(Path.Combine(repo, "Source",
            "AutonomousBuildingPatternKernel.cs"));
        string corpus = File.ReadAllText(Path.Combine(repo,
            "PLAYER_BASE_PATTERN_CORPUS.md"));
        string layoutExtractor = File.ReadAllText(Path.Combine(repo, "tools",
            "PlayerBaseLayoutExtractor", "Program.cs"));
        string realRuinsExtractor = File.ReadAllText(Path.Combine(repo,
            "tools", "PlayerBaseLayoutExtractor",
            "RealRuinsBlueprintExtractor.cs"));
        string externalLayoutManifest = File.ReadAllText(Path.Combine(repo,
            "Corpus", "PlayerBaseLayouts", "external",
            "real-ruins-screening-manifest.json"));
        string broadLayoutManifestPath = Path.Combine(repo, "Corpus",
            "PlayerBaseLayouts", "external",
            "real-ruins-broad-clean-manifest.json");
        string broadLayoutProfilePath = Path.Combine(repo, "Corpus",
            "PlayerBaseLayouts", "external",
            "real-ruins-broad-profile.json");
        using JsonDocument broadManifest = JsonDocument.Parse(
            File.ReadAllText(broadLayoutManifestPath));
        using JsonDocument broadProfile = JsonDocument.Parse(
            File.ReadAllText(broadLayoutProfilePath));
        JsonElement broadEntries = broadManifest.RootElement.GetProperty(
            "entries");
        JsonElement profileRoot = broadProfile.RootElement;
        int broadEntryCount = broadEntries.GetArrayLength();
        int broadUniqueSources = broadEntries.EnumerateArray()
            .Select(entry => entry.GetProperty("sourceSha256").GetString())
            .Distinct(StringComparer.Ordinal).Count();
        int broadUniqueLineages = broadEntries.EnumerateArray()
            .Select(entry => entry.GetProperty("lineageSha256").GetString())
            .Distinct(StringComparer.Ordinal).Count();
        Add("frontier occupancy follows verified habitat materialization",
            frontier.IndexOf("TryMaterializeHabitat(map, site",
                    StringComparison.Ordinal)
                < frontier.IndexOf("PawnGenerator.GeneratePawn",
                    StringComparison.Ordinal)
            && frontier.Contains("ResolveSupporter")
            && frontier.Contains("BlockMaterialization")
            && frontier.Contains("TrySpawnStock")
            && frontier.Contains("TrySowFoodPatch"),
            "saved supporter and exact tile requirements are validated; "
                + "shelter, food route, stores, and required functions are "
                + "materialized before residents");
        Add("frontier capability belongs to exact saved site state",
            habitat.Contains("CATechnologicalKnowledge knowledge = CASiteState.Knowledge(plan,")
            && frontier.Contains("holding.localSociety?.technologicalKnowledge")
            && frontier.Contains("supportDeclared")
            && frontier.Contains("ownerDeclared")
            && frontier.Contains("resolvedTier != holding.capabilityTier"),
            "the canonical local knowledge supplies capability; optional owner and support references are resolved separately and materialization rechecks the saved tier");
        Add("frontier realization identity rejects stale causes",
            organization.Contains("FrontierRealizationSourceHash")
            && organization.Contains("plan.realizationSourceHash")
            && organization.Contains("plan.holdings.Count != expectedCount")
            && organization.Contains("supportingFactionLoadId"),
            "saved policy, environment, capability, supporter, count, and "
                + "map identity are revalidated before a plan is reused");
        Add("autonomous building uses composable pattern evidence",
            frontier.Contains("FrontierPatternEvidence")
            && frontier.Contains("PreferFrontier")
            && pattern.Contains("FunctionalAdjacency")
            && pattern.Contains("Circulation")
            && pattern.Contains("Expansion")
            && pattern.Contains("VisualOrder")
            && !pattern.Contains("MinmaxMode")
            && !pattern.Contains("FengShuiMode")
            && corpus.Contains("OP-001")
            && corpus.Contains("tools/PlayerBaseLayoutExtractor")
            && corpus.Contains("Complementary mature corpus")
            && corpus.Contains("Real Ruins")
            && layoutExtractor.Contains("\".bp\"")
            && realRuinsExtractor.Contains("RecoveredTruncation")
            && realRuinsExtractor.Contains("TerrainCells")
            && externalLayoutManifest.Contains("completeCandidates")
            && externalLayoutManifest.Contains("rejectedOrRestricted"),
            "one multi-axis evidence vector ranks whole-site candidates; "
                + "the unified extractor reads full saves and structured "
                + "snapshots while preserving source limits, exact ground, "
                + "and rejection evidence; the corpus admits relationships, "
                + "not copied layouts or a style toggle");
        Add("broad native layout corpus is substantial and lineage safe",
            broadEntryCount == 4238
            && broadUniqueSources == broadEntryCount
            && broadUniqueLineages == broadEntryCount
            && profileRoot.GetProperty("completeUnique").GetInt32()
                == broadEntryCount
            && profileRoot.GetProperty("biomeCount").GetInt32() == 80
            && profileRoot.GetProperty("partitionCounts")
                .EnumerateObject().Sum(property => property.Value.GetInt32())
                == broadEntryCount,
            "4,238 complete source-unique and lineage-unique native layouts "
                + "span 80 biome definitions; deterministic partitions cover "
                + "every clean row");

        Add("environmental realization survives save and readback",
            planning.Contains("CA_settlementContextEnvironmentalBiomes")
            && planning.Contains(
                "CA_settlementContextEnvironmentalGrowingTwelfths")
            && planning.Contains(
                "CA_settlementContextEnvironmentalHabitatRequirementMask")
            && planning.Contains(
                "CA_settlementContextEnvironmentalRequiredCapabilityTier")
            && planning.Contains(
                "CA_settlementContextEnvironmentalFoodRoute")
            && planning.Contains(
                "CA_settlementContextEnvironmentalSourceHash")
            && planning.Contains(".Append(environmentalSourceHash)"),
            "planning context Scribes environmental facts and includes them "
            + "in its stable evidence signature");

        string engineTemperature = File.ReadAllText(Path.Combine(decompiled,
            "Verse", "GenTemperature.cs"));
        string engineBiome = File.ReadAllText(Path.Combine(decompiled,
            "RimWorld", "BiomeDef.cs"));
        string engineTerrain = File.ReadAllText(Path.Combine(decompiled,
            "RimWorld", "Planet", "WITab_Terrain.cs"));
        Add("environment contract matches RimWorld terrain facts",
            engineTemperature.Contains("TwelfthsInAverageTemperatureRange")
            && engineBiome.Contains("public float plantDensity")
            && engineBiome.Contains("public float forageability")
            && engineBiome.Contains("public float diseaseMtbDays")
            && engineTerrain.Contains("OutdoorGrowingPeriod")
            && engineTerrain.Contains("AverageDiseaseFrequency"),
            "decompiled terrain UI and engine expose the same season, plant, "
            + "forage, and disease inputs");

        string assembly = Path.Combine(repo, "Assemblies",
            "ColonistAwareness.dll");
        Add("production assembly exists after compilation",
            File.Exists(assembly) && new FileInfo(assembly).Length > 0,
            File.Exists(assembly)
                ? $"bytes={new FileInfo(assembly).Length}; SHA256="
                    + Hash(File.ReadAllBytes(assembly))
                : "assembly missing");
    }

    private static void Add(string name, bool passed, string evidence) =>
        Results.Add(new Result(name, passed, evidence));

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static void WriteReport(string repo)
    {
        string path = Path.Combine(repo, "Receipts", "B14",
            "B14_SETTLEMENT_ENVIRONMENT_STATIC_RECEIPT.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = new StringBuilder()
            .AppendLine("# B14 settlement environment static receipt")
            .AppendLine()
            .AppendLine("This executable receipt varies one environmental "
                + "input set at a time, inspects the production wiring, and "
                + "anchors the facts in RimWorld's decompiled terrain path. "
                + "Operator runtime acceptance remains separate.")
            .AppendLine()
            .AppendLine("| Contract | Result | Evidence |")
            .AppendLine("|---|---|---|");
        foreach (Result result in Results)
            text.AppendLine("| " + Escape(result.Name) + " | **"
                + (result.Passed ? "PASS" : "FAIL") + "** | "
                + Escape(result.Evidence) + " |");
        text.AppendLine().AppendLine("Overall: **"
            + (Results.All(value => value.Passed) ? "PASS" : "FAIL")
            + "**");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static string Escape(string value) => value.Replace("|", "\\|")
        .Replace("\r", " ").Replace("\n", " ");
}
