using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Disposable end-to-end regional generation exercise. It is armed by
    // armed by env var CA_CONVERGENCE_EXERCISE=1 or the one-shot DevOutput
    // marker file (the channel that survives Steam's stale-environment
    // pitfall), it OWNS the launch chain from the main menu - the engine's
    // own quicktest setup for stage one, then the same "Play"-scene long
    // event a New Colony queues - and authors a two-faction,
    // three-settlement region with a concentrated minority population,
    // confirmed with developerExercise stamped so a durable start can
    // refuse it.
    //
    // Once the map is running it executes every receipt against the live
    // world, captures screenshots, writes everything to DevOutput, and
    // shuts the game down. Nothing here runs unless armed; an unarmed
    // quicktest is vanilla's own.
    [StaticConstructorOnStartup]
    internal static class CAConvergenceExercise
    {
        internal static bool Armed;
        internal static bool HoldSession;
        // The exercise's scale, parsed from the arming value: "1" runs
        // the cheap 200x4 miniature; "350x8" runs a real play-scale
        // regional landmass. The mod's basis IS the large map, so the
        // full-scale form is what a demonstration run should use.
        internal static int SourceScale = 200;
        internal static int RegionTiles = 4;
        internal const string FixedWorldSeed =
            "CA-B18-PERFORMANCE-CONVERGENCE";
        internal const int FixedStartingTileSeed = 181806;
        internal const int FixedRunRandomSeed = 181814;
        internal static bool FixedRunRandomStateActive;
        // Set ONLY by the quicktest postfix that authored the candidate.
        // The run-and-report component requires it, so the component can
        // never act on - and never shut down - a game a person started.
        internal static bool AuthoredThisSession;
        private const int PoliticalProbeSlots = 2;
        private static readonly long[] PoliticalOpenTicks =
            new long[PoliticalProbeSlots];
        private static readonly long[] PoliticalFirstFrameTicks =
            new long[PoliticalProbeSlots];
        private static readonly long[] PoliticalTotalFrameTicks =
            new long[PoliticalProbeSlots];
        private static readonly long[] PoliticalMaximumFrameTicks =
            new long[PoliticalProbeSlots];
        private static readonly int[] PoliticalFrameCounts =
            new int[PoliticalProbeSlots];
        private static int politicalProbeSlot = -1;
        private const string MarkerFile =
            "ColonistAwareness.convergence-exercise.txt";

        internal static string DevOutputDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder
                        .LocalApplicationData) + "Low",
                    "Ludeon Studios", "RimWorld by Ludeon Studios",
                    "DevOutput");
            }
        }

        static CAConvergenceExercise()
        {
            try
            {
                string armed = Environment.GetEnvironmentVariable(
                    "CA_CONVERGENCE_EXERCISE");
                if (string.IsNullOrEmpty(armed))
                {
                    try
                    {
                        string marker = Path.Combine(DevOutputDir,
                            MarkerFile);
                        if (File.Exists(marker))
                            armed = File.ReadAllText(marker).Trim();
                    }
                    catch (Exception) { }
                }
                if (string.IsNullOrEmpty(armed)) return;
                if (armed.EndsWith("-hold", StringComparison.Ordinal))
                {
                    HoldSession = true;
                    armed = armed.Substring(0,
                        armed.Length - "-hold".Length);
                }
                if (armed.EndsWith("-notrace", StringComparison.Ordinal))
                {
                    CAPassivePlayMatrix.SuppressTrace = true;
                    armed = armed.Substring(0,
                        armed.Length - "-notrace".Length);
                }
                if (armed.EndsWith("-passive", StringComparison.Ordinal))
                {
                    CAPassivePlayMatrix.Requested = true;
                    armed = armed.Substring(0,
                        armed.Length - "-passive".Length);
                }
                // GEOGRAPHY IS A FIXTURE AXIS. "400x6@peninsula" lands on a
                // peninsula; without a profile the fixture keeps its old
                // seeded-random tile, which is inland and exercises none of
                // the projection kernel's coast, littoral or river paths.
                int at = armed.IndexOf('@');
                if (at >= 0)
                {
                    CAExerciseGeography.Requested =
                        armed.Substring(at + 1).Trim().ToLowerInvariant();
                    armed = armed.Substring(0, at);
                }
                if (armed != "1" && armed.Contains("x"))
                {
                    string[] parts = armed.Split(new[] { 'x' },
                        StringSplitOptions.None);
                    int size, count;
                    if (parts.Length == 2
                        && int.TryParse(parts[0], out size)
                        && int.TryParse(parts[1], out count))
                    {
                        SourceScale = size;
                        RegionTiles = Mathf.Clamp(count, 2, 12);
                    }
                    else return;
                }
                else if (armed != "1") return;
                // MUTUAL EXCLUSION with the template harness: both own
                // the new-game boundary. The exercise refuses to arm
                // rather than colliding.
                if (CARegionalTemplateArming.ArmedGrids != null)
                {
                    Log.Warning("[CA][Exercise] NOT arming: the regional "
                        + "template harness is armed. Disarm the template "
                        + "first.");
                    return;
                }
                // THE MARKER IS ONE-SHOT. Consumed at arming, so a crash
                // anywhere later can never leave a standing marker that
                // arms every future launch - the exercise must be armed
                // deliberately, every time.
                try
                {
                    File.Delete(Path.Combine(DevOutputDir, MarkerFile));
                }
                catch (Exception) { }
                // AN EXERCISE SESSION NEVER LOADS A SAVE. Runs 5 and 6
                // both died to a save-load of the empty name "Saves\.rws"
                // that fired despite gameToLoad being nulled and the
                // autostart check being marked done - the trigger is
                // outside every branch this harness models. So the armed
                // session stops modelling it: EVERY LoadGameFromSaveFileNow
                // is blocked, and each attempt logs the caller's full
                // stack, so the perpetrator names itself in the log
                // instead of being theorized about.
                var loadGuard = new HarmonyLib.Harmony(
                    "ellyj3rain.colonistawareness.exercise-loadguard");
                loadGuard.Patch(
                    HarmonyLib.AccessTools.Method(
                        typeof(SavedGameLoaderNow),
                        nameof(SavedGameLoaderNow
                            .LoadGameFromSaveFileNow)),
                    prefix: new HarmonyLib.HarmonyMethod(
                        typeof(CAConvergenceExercise),
                        nameof(BlockSaveLoads)));

                // NO PATCHES AND NO -quicktest. Three runs proved the
                // vanilla quicktest branch unusable on this modlist: it
                // is `if (Current.Game == null)`, a mod pre-creates a
                // worldless Game during load, so setup is skipped and
                // InitNewGame NREs at Find.WorldObjects - and no prefix
                // can save a body already executing on the stale
                // instance. The harness therefore OWNS stage one: at the
                // main menu it runs the engine's own setup (which
                // overwrites any pre-created Game by plain assignment),
                // authors the candidate, and then hands vanilla stage
                // two - the same "Play"-scene long event a New Colony
                // queues, whose Root_Play.Start calls InitNewGame on the
                // game WE built.
                // The convergence marker is itself the explicit developer
                // authority required by the compatibility gate. Arm that same
                // gate before the queued lifecycle can register its transient
                // candidate.
                CARegionalContentCompatibility.DeveloperExerciseUnverified =
                    true;
                Armed = true;
                CATickAttributionProbe.Arm(loadGuard);
                // a held run is a measurement run: the operator reported the
                // game feels slower, and a feeling cannot be argued with --
                // only measured against the 22.3 ms quiet baseline
                CASteadyEffectsSubProbe.Arm(loadGuard);
                CATickListAttribution.Arm(loadGuard);
                LongEventHandler.QueueLongEvent(BuildAndLaunch,
                    "GeneratingMap", doAsynchronously: false, null);
                Log.Message("[CA][Exercise] ARMED: the exercise will "
                    + "author a two-faction regional candidate at the "
                    + "menu, generate it through the native lifecycle, "
                    + "run every receipt, capture screenshots and exit");
            }
            catch (Exception e)
            {
                Log.Error("[CA][Exercise] arming failed: " + e);
            }
        }

        private static bool BlockSaveLoads(string fileName)
        {
            // The passive matrix's own disposable save is the single load
            // the exercise session models; it is allowed only inside the
            // matrix's explicit reload window and never touches an
            // operator save.
            if (CAPassivePlayMatrix.AllowDisposableLoad
                && fileName == CAPassivePlayMatrix.DisposableSaveName)
                return true;
            Log.Warning("[CA][Exercise] BLOCKED save-load attempt for '"
                + (fileName ?? "null") + "' during the exercise session. "
                + "Caller:\n" + Environment.StackTrace);
            return false;
        }

        private static void BuildAndLaunch()
        {
            try
            {
                SetupForConvergenceExercise();
                AuthorCandidate();
                if (!AuthoredThisSession)
                {
                    Log.Error("[CA][Exercise] authoring did not complete; "
                        + "not launching a game");
                    Current.Game = null;
                    ReleaseFixedRunRandomState();
                    return;
                }
                // ROOT_PLAY.START HAS THREE BRANCHES and only one of them
                // is ours. Run 5 proved the other two are live on this
                // machine: an autostart.rws exists in Saves (dev-mode
                // autostart hijacks any Play-scene entry), and SOMETHING
                // set gameToLoad to a value that resolved to an empty
                // save name ("Saves\.rws"). Both load branches are
                // suppressed explicitly, so the scene switch can only
                // fall through to InitNewGame on the game built above.
                typeof(Root).GetField("checkedAutostartSaveFile",
                        System.Reflection.BindingFlags.Static
                        | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(null, true);
                Find.GameInitData.gameToLoad = null;
                LongEventHandler.QueueLongEvent(delegate
                {
                    Find.GameInitData.PrepForMapGen();
                    Find.Scenario.PreMapGenerate();
                }, "Play", "GeneratingMap", doAsynchronously: true,
                    GameAndMapInitExceptionHandlers
                        .ErrorWhileGeneratingMap);
            }
            catch (Exception e)
            {
                Log.Error("[CA][Exercise] stage-one setup failed: " + e);
                try { Current.Game = null; } catch (Exception) { }
                ReleaseFixedRunRandomState();
            }
        }

        internal static bool PoliticalProbeEnabled => Armed
            && AuthoredThisSession && politicalProbeSlot >= 0;

        internal static void ResetPoliticalProbe()
        {
            Array.Clear(PoliticalOpenTicks, 0, PoliticalOpenTicks.Length);
            Array.Clear(PoliticalFirstFrameTicks, 0,
                PoliticalFirstFrameTicks.Length);
            Array.Clear(PoliticalTotalFrameTicks, 0,
                PoliticalTotalFrameTicks.Length);
            Array.Clear(PoliticalMaximumFrameTicks, 0,
                PoliticalMaximumFrameTicks.Length);
            Array.Clear(PoliticalFrameCounts, 0,
                PoliticalFrameCounts.Length);
            politicalProbeSlot = -1;
        }

        internal static void BeginPoliticalProbe(int slot, long openTicks)
        {
            if (slot < 0 || slot >= PoliticalProbeSlots) return;
            PoliticalOpenTicks[slot] = Math.Max(0L, openTicks);
            politicalProbeSlot = slot;
        }

        internal static void EndPoliticalProbe()
        {
            politicalProbeSlot = -1;
        }

        internal static void RecordPoliticalFrame(long elapsedTicks)
        {
            int slot = politicalProbeSlot;
            if (slot < 0 || slot >= PoliticalProbeSlots) return;
            long elapsed = Math.Max(0L, elapsedTicks);
            if (PoliticalFrameCounts[slot] == 0)
                PoliticalFirstFrameTicks[slot] = elapsed;
            PoliticalFrameCounts[slot]++;
            PoliticalTotalFrameTicks[slot] += elapsed;
            if (elapsed > PoliticalMaximumFrameTicks[slot])
                PoliticalMaximumFrameTicks[slot] = elapsed;
        }

        internal static string PoliticalProbeReport()
        {
            var text = new StringBuilder();
            for (int slot = 0; slot < PoliticalProbeSlots; slot++)
            {
                double openMs = Milliseconds(PoliticalOpenTicks[slot]);
                double firstFrameMs = Milliseconds(
                    PoliticalFirstFrameTicks[slot]);
                double maximumMs = Milliseconds(
                    PoliticalMaximumFrameTicks[slot]);
                double meanMs = PoliticalFrameCounts[slot] == 0 ? 0d
                    : Milliseconds(PoliticalTotalFrameTicks[slot])
                        / PoliticalFrameCounts[slot];
                double effectiveOpenMs = openMs + firstFrameMs;
                string name = slot == 0 ? "first open" : "reopen";
                text.Append(name).Append(": request+construction ")
                    .Append(openMs.ToString("F3",
                        System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" ms; first render ")
                    .Append(firstFrameMs.ToString("F3",
                        System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" ms; effective open ")
                    .Append(effectiveOpenMs.ToString("F3",
                        System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" ms; render calls ")
                    .Append(PoliticalFrameCounts[slot])
                    .Append("; mean/max ")
                    .Append(meanMs.ToString("F3",
                        System.Globalization.CultureInfo.InvariantCulture))
                    .Append('/')
                    .Append(maximumMs.ToString("F3",
                        System.Globalization.CultureInfo.InvariantCulture))
                    .AppendLine(" ms");
            }
            double first = Milliseconds(PoliticalOpenTicks[0]
                + PoliticalFirstFrameTicks[0]);
            double reopen = Milliseconds(PoliticalOpenTicks[1]
                + PoliticalFirstFrameTicks[1]);
            double ordinaryMax = Math.Max(
                Milliseconds(PoliticalMaximumFrameTicks[0]),
                Milliseconds(PoliticalMaximumFrameTicks[1]));
            bool firstMeasured = PoliticalFrameCounts[0] > 0;
            bool reopenMeasured = PoliticalFrameCounts[1] > 0;
            text.Append("thresholds: first open <1000 ms ")
                .Append(firstMeasured && first < 1000d ? "PASS" : "FAIL")
                .Append("; reopen <250 ms ")
                .Append(reopenMeasured && reopen < 250d ? "PASS" : "FAIL")
                .Append("; ordinary render <100 ms ")
                .Append(firstMeasured && reopenMeasured
                    && ordinaryMax < 100d ? "PASS" : "FAIL");
            if (!firstMeasured || !reopenMeasured)
                text.Append(" (OnGUI was not rendered; no UI result claimed)");
            return text.ToString();
        }

        private static double Milliseconds(long ticks)
        {
            return ticks * 1000d / Stopwatch.Frequency;
        }

        // RimWorld's developer quick-test setup chooses a fresh random world
        // seed on every launch. This exercise needs the same native setup with
        // a governed fixed seed so before/after phase timings and result
        // fingerprints are comparable across fresh processes.
        private static void SetupForConvergenceExercise()
        {
            Rand.PushState(FixedRunRandomSeed);
            FixedRunRandomStateActive = true;
            Current.ProgramState = ProgramState.Entry;
            Game.ClearCaches();
            Current.Game = new Game();
            Current.Game.InitData = new GameInitData();
            Current.Game.Scenario = ScenarioDefOf.Crashlanded.scenario;
            Find.Scenario.PreConfigure();
            Current.Game.storyteller = new Storyteller(
                StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
            Current.Game.World = WorldGenerator.GenerateWorld(0.3f,
                FixedWorldSeed, OverallRainfall.Normal,
                OverallTemperature.Normal, OverallPopulation.Normal,
                LandmarkDensity.Normal);
            Rand.PushState(FixedStartingTileSeed);
            try
            {
                PlanetTile geographic;
                if (CAExerciseGeography.TrySelect(RegionTiles, out geographic))
                    Find.GameInitData.startingTile = geographic;
                else
                {
                    if (!string.IsNullOrEmpty(CAExerciseGeography.Requested))
                        Log.Warning("[CA][Exercise] no tile satisfied "
                            + "geography profile '"
                            + CAExerciseGeography.Requested
                            + "'; falling back to the seeded random tile. "
                            + "Known profiles: "
                            + CAExerciseGeography.Profiles);
                    Find.GameInitData.ChooseRandomStartingTile();
                }
            }
            finally
            {
                Rand.PopState();
            }
            Find.GameInitData.mapSize = 250;
            Find.Scenario.PostIdeoChosen();
            Log.Message("[CA][Exercise] fixed benchmark setup: world seed "
                + FixedWorldSeed + ", starting-tile seed "
                + FixedStartingTileSeed + ", run RNG seed "
                + FixedRunRandomSeed + ", scale " + SourceScale + "x"
                + RegionTiles + ", geography " + CAExerciseGeography.Resolved);
        }

        internal static void ReleaseFixedRunRandomState()
        {
            if (!FixedRunRandomStateActive) return;
            FixedRunRandomStateActive = false;
            try { Rand.PopState(); }
            catch (Exception error)
            {
                Log.Warning("[CA][Exercise] fixed benchmark RNG cleanup "
                    + "failed during shutdown: " + error.Message);
            }
        }

        // After the engine's own setup: re-seed deterministically,
        // author the regional candidate around a valid start tile, and
        // point GameInitData at it. Vanilla then runs PrepForMapGen /
        // InitNewGame itself, and CA's own GenerateMap prefix picks the
        // Pending plan up exactly as it would a player-confirmed one.
        private static void AuthorCandidate()
        {
            try
            {
                PlanetTile root = Find.GameInitData.startingTile;
                CAExpandedLandmassProfile profile;
                if (!CAExpandedLandmassProfile.TryFor(
                        CAConvergenceExercise.SourceScale, out profile))
                {
                    Log.Error("[CA][Exercise] no "
                        + CAConvergenceExercise.SourceScale
                        + "-cell profile");
                    return;
                }
                CARegionalPlan plan = CARegionalPlanUtility.Create(
                    profile, root, true,
                    CAConvergenceExercise.RegionTiles);
                plan.worldPolicy = plan.worldPolicy
                    ?? new CARegionalWorldPolicy();
                // History axes that guarantee an interesting composition.
                plan.worldPolicy.reallocationSourceVariety = 1f;

                // Two factions with different complete Political Orders.
                var councilFaction = new CARegionalFactionPlan
                {
                    key = 1,
                    source = CARegionalFactionSource.NewWorldFaction,
                    authored = true,
                    customName = "Assembly of the Ford",
                    playerRelation = FactionRelationKind.Neutral,
                    authorPlayerRelation = true,
                    visibleInWorld = true
                };
                var tradeFaction = new CARegionalFactionPlan
                {
                    key = 2,
                    source = CARegionalFactionSource.NewWorldFaction,
                    authored = true,
                    customName = "Charter Towns",
                    playerRelation = FactionRelationKind.Neutral,
                    authorPlayerRelation = true,
                    visibleInWorld = true
                };
                FactionDef template = CARegionalPlanUtility
                    .EligibleNewFactionDefs().FirstOrDefault();
                councilFaction.customFactionDefName = template?.defName;
                tradeFaction.customFactionDefName = template?.defName;
                plan.factions.Add(councilFaction);
                plan.factions.Add(tradeFaction);
                councilFaction.EnsureCultureAndPolitics(plan);
                tradeFaction.EnsureCultureAndPolitics(plan);
                CACultureAuthoringKernel.CompleteMissing(
                    councilFaction.culture,
                    (plan.candidateId ?? "ca") + ":faction:1:culture",
                    "starting-region generation");
                CACultureAuthoringKernel.CompleteMissing(
                    tradeFaction.culture,
                    (plan.candidateId ?? "ca") + ":faction:2:culture",
                    "starting-region generation");
                CAPoliticalOrderModel.ApplyPreset(
                    councilFaction.politicalBeliefs,
                    "cooperative-commonwealth", CAAxisSource.Authored);
                CAPoliticalOrderModel.ApplyPreset(
                    tradeFaction.politicalBeliefs,
                    "civic-market", CAAxisSource.Authored);

                // Three settlements: the council faction holds two, the trade
                // faction one. The first has a concentrated minority whose
                // affiliation, Culture, Ideoligion, and Political Order are
                // explicitly sourced independently.
                // Settlements use non-root areas: the root is the
                // landing tile, and validation rightly refuses an arrival
                // on top of a settlement. The first faction's first two
                // settlements share one member - a real cluster, so the
                // realized profile can carry an actual concentration
                // rather than only singles.
                var members = plan.memberTileIds
                    .Where(id => id != root.tileId).ToList();
                if (members.Count == 0) members = plan.memberTileIds;
                int settlementCount = members.Count >= 3 ? 4 : 3;
                for (int i = 0; i < settlementCount; i++)
                {
                    int memberIndex = i <= 1 ? 0
                        : (i - 1) % members.Count;
                    // THE FIXTURE AUTHORS THE CAUSES, NEVER THE SUMMARIES.
                    // Population, ground grade, and established history are
                    // the authorable facts; the starting composition selects
                    // programs from them at realization, and economy, trade,
                    // specialization, and urban support are summaries of
                    // that composition. Run 9 proved that writing summaries
                    // here is exactly the writing derivation must overwrite,
                    // so all four settlements realized identical. The spread
                    // is deterministic per slot: a foraging hamlet, a
                    // farming village, an old producing town, and a populous
                    // coastal market.
                    int[] pops = { 18, 60, 140, 320 };
                    int[] lands = { 1, 2, 2, 3 };
                    int[] histories = { 0, 1, 3, 2 };
                    plan.settlements.Add(new CARegionalSettlementPlan
                    {
                        slot = i,
                        memberTileId = members[memberIndex],
                        OwningFactionKey = i < settlementCount - 1 ? 1 : 2,
                        siteClusterKey = i,
                        persistent = true,
                        populationOrigin =
                            CASettlementOrigin.ScenarioOverride,
                        reallocatedFromTileId = -1,
                        authoredPopulation = pops[i % 4],
                        authoredLandCapacity = lands[i % 4],
                        authoredHistoricalDevelopment = histories[i % 4]
                    });
                }
                CARegionalPlanUtility.EnsureRelationRows(plan);
                foreach (CARegionalSettlementPlan settlementPlan in plan.settlements)
                    CASettlementComposition.EnsureDerived(plan, settlementPlan);
                CARegionalSettlementPlan first = plan.settlements[0];
                if (!first.populationGroups.Any(c => c != null
                    && c.kind == CAPopulationGroupKind.OtherFaction))
                {
                    const int minorityShare = 20;
                    CASettlementPopulationGroup primary = first
                        .populationGroups.FirstOrDefault(c => c != null
                            && c.isPrimary);
                    if (primary != null)
                        primary.share = CACreationFlowContracts
                            .MainPopulationShare(minorityShare);
                    first.populationGroups.Insert(1, new CASettlementPopulationGroup
                    {
                        key = 7,
                        kind = CAPopulationGroupKind.OtherFaction,
                        label = "Charter Towns",
                        share = minorityShare,
                        factionKey = 2,
                        ideoligionFactionKey = 2,
                        politicalBeliefsFactionKey = 2,
                        ideoligionCertainty = 1,
                        ideoligionProtected = true,
                        authored = true
                    });
                    first.provisionArrangements.Clear();
                    CASettlementComposition.EnsureDerived(plan, first);
                }
                CARegionalSettlements.RealizeForConfirmation(plan);
                foreach (CARegionalSettlementPlan settlementPlan in
                    plan.settlements.Where(item => item != null))
                    CASettlementProgramRegistry.EnsureDerived(plan,
                        settlementPlan);

                string failure;
                if (!CARegionalPlanUtility.TryValidateStartingSettlements(
                        plan, out failure))
                    throw new InvalidOperationException(
                        "starting-settlement validation: " + failure);
                if (!CARegionalSettlements.TryValidateRealization(plan,
                        out failure))
                    throw new InvalidOperationException(
                        "settlement realization validation: " + failure);
                plan.confirmed = true;
                plan.developerExercise = true;
                plan.creationSummary = (plan.creationSummary ?? "")
                    + "; convergence exercise";
                CAConvergenceExercise.AuthoredThisSession = true;
                Find.GameInitData.startingTile = root;
                // The CHOSEN size must match the authored plan's, or the
                // GenerateMap prefix rightly refuses the Pending plan and
                // derives a fresh region instead - which is exactly what
                // run 8 did when this line still said 200.
                Find.GameInitData.mapSize =
                    CAConvergenceExercise.SourceScale;
                // World ownership includes the chosen map size, so adopt only
                // after the exercise has installed that final value.
                CARegionalSetupSession.AdoptPendingForCurrentWorld(plan);
                Log.Message("[CA][Exercise] authored candidate "
                    + plan.regionalId + " at tile " + root.tileId + ": "
                    + CARegionalSettlements.SettlementProfile(plan) + "; "
                    + plan.settlements.Count + " settlements, "
                    + plan.factions.Count + " factions");
            }
            catch (Exception e)
            {
                Log.Error("[CA][Exercise] candidate authoring failed: "
                    + e);
            }
        }
    }

    // The run-and-report half: waits for the generated map to settle,
    // executes every receipt, captures screenshots, writes the artifact,
    // and shuts down. A watchdog shutdown guarantees the process never
    // outlives its purpose.
    public sealed class CAConvergenceExerciseComponent : GameComponent
    {
        // REAL-TIME, NOT GAME TICKS. Run 7 hung forever because the game
        // starts PAUSED and GameComponentTick never advances while
        // paused. Update runs every frame regardless; the schedule below
        // clocks on wall time from the moment a map exists, and the
        // component unpauses the game itself so the world visibly runs
        // in the screenshots.
        private float mapReadyAt = -1f;
        private int stage;
        private readonly StringBuilder report = new StringBuilder();
        private bool pendingPoliticalOpen;
        private bool pendingPoliticalClose;
        private bool pendingWorldJump;
        private int politicalOpenSlot;
        private Window politicalWindow;

        public CAConvergenceExerciseComponent(Game game) { }

        public override void GameComponentUpdate()
        {
            // BOTH flags: armed AND this game authored by the exercise
            // harness. A person's own game is never receipted and never
            // shut down, whatever markers or env vars exist.
            if (!CAConvergenceExercise.Armed
                || !CAConvergenceExercise.AuthoredThisSession) return;
            try
            {
                // Once the passive matrix owns the session - including
                // across its disposable reload, where this component is a
                // fresh instance - the ordinary receipt schedule must not
                // restart.
                if (CAPassivePlayMatrix.Running)
                {
                    CAPassivePlayMatrix.Update(this);
                    return;
                }
                if (Find.CurrentMap == null) return;
                float now = Time.realtimeSinceStartup;
                if (mapReadyAt < 0f)
                {
                    mapReadyAt = now;
                    if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused)
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    CAConvergenceExercise.ResetPoliticalProbe();
                    politicalOpenSlot = 0;
                    pendingPoliticalOpen = true;
                    return;
                }
                float elapsed = now - mapReadyAt;
                if (stage == 0 && elapsed >= 3f)
                {
                    stage = 1;
                    pendingPoliticalClose = true;
                }
                else if (stage == 1 && elapsed >= 4f)
                {
                    stage = 2;
                    politicalOpenSlot = 1;
                    pendingPoliticalOpen = true;
                }
                else if (stage == 2 && elapsed >= 7f)
                {
                    stage = 3;
                    pendingPoliticalClose = true;
                    CAModuleProfiler.SetEnabled(true, reset: true);
                }
                else if (stage == 3 && elapsed >= 22f)
                {
                    stage = 4;
                    RunReceipts();
                    CAModuleProfiler.SetEnabled(false);
                    Jump(0);
                }
                else if (stage == 4 && elapsed >= 29f)
                {
                    stage = 5;
                    Capture("ca-exercise-settlement-a.png");
                }
                else if (stage == 5 && elapsed >= 37f)
                {
                    stage = 6;
                    Jump(2);
                }
                else if (stage == 6 && elapsed >= 43f)
                {
                    stage = 7;
                    Capture("ca-exercise-settlement-b.png");
                }
                else if (stage == 7 && elapsed >= 51f)
                {
                    stage = 8;
                    pendingWorldJump = true;
                }
                else if (stage == 8 && elapsed >= 57f)
                {
                    stage = 9;
                    Capture("ca-exercise-world.png");
                }
                else if (stage == 9 && elapsed >= 65f)
                {
                    stage = 10;
                    // Captures are banked; a held session gets the menu back
                    // up for the operator's own inspection.
                    if (CAConvergenceExercise.HoldSession)
                    {
                        politicalOpenSlot = 1;
                        pendingPoliticalOpen = true;
                    }
                    if (CAPassivePlayMatrix.Requested)
                        CAPassivePlayMatrix.Begin(report.ToString());
                    else
                        Finish("complete");
                }
                else if (elapsed >= 300f)
                {
                    Finish("watchdog timeout at stage " + stage);
                }
            }
            catch (Exception e)
            {
                report.AppendLine("EXERCISE FAILED: " + e);
                Finish("exception");
            }
        }

        public override void GameComponentOnGUI()
        {
            if (!CAConvergenceExercise.Armed
                || !CAConvergenceExercise.AuthoredThisSession) return;
            if (CAPassivePlayMatrix.Running)
            {
                CAPassivePlayMatrix.ServeGui();
                return;
            }
            if (pendingPoliticalClose)
            {
                pendingPoliticalClose = false;
                // The probe window must be down during the settlement and
                // world captures -- run 10's evidence screenshots were all
                // three blocked by this menu. A -hold run still exists to be
                // LOOKED AT, so the hold path reopens the menu once the
                // captures are done, instead of never closing it.
                if (politicalWindow != null)
                    Find.WindowStack.TryRemove(politicalWindow,
                        doCloseSound: false);
                politicalWindow = null;
                CAConvergenceExercise.EndPoliticalProbe();
            }
            if (pendingPoliticalOpen)
            {
                pendingPoliticalOpen = false;
                var beliefs = new CAPoliticalBeliefs();
                long started = Stopwatch.GetTimestamp();
                politicalWindow =
                    Dialog_CAPoliticalOrderEditor.ForEstablished(
                        beliefs, new List<CAAxisEntry>(),
                        "b18-political-menu-probe-"
                            + politicalOpenSlot, null);
                Find.WindowStack.Add(politicalWindow);
                CAConvergenceExercise.BeginPoliticalProbe(
                    politicalOpenSlot,
                    Stopwatch.GetTimestamp() - started);
            }
            if (pendingWorldJump)
            {
                pendingWorldJump = false;
                // WorldFeatures creates wrapped labels during its next
                // Update. In ordinary play the click that changes views has
                // already initialized Verse.Text inside OnGUI; the harness
                // must preserve that same lifecycle instead of jumping from
                // GameComponentUpdate.
                Text.Font = GameFont.Small;
                CameraJumper.TryShowWorld();
            }
        }

        private void RunReceipts()
        {
            Map map = Find.CurrentMap;
            report.AppendLine("==== CONVERGENCE EXERCISE "
                + "(developer-exercise route, disposable) ====");
            report.AppendLine("");
            Append("MAP COMPOSITION RECEIPT",
                () => CAConvergenceReceipt.RunMap(map));
            Append("RADICAL SHIFT RECEIPT",
                () => CAConvergenceReceipt.RunRadicalShift(map));
            Append("FACTION CULTURE AND POLITICS RECEIPT",
                () => CAFactionStateReceipt.Run());
            Append("POLITICAL ORDER MENU PERFORMANCE",
                () => CAConvergenceExercise.PoliticalProbeReport());
            Append("PASSIVE PLAY MODULE PROFILE (15 WALL SECONDS)",
                () => CAModuleProfiler.Snapshot().ToLogText());
            Write("ca-convergence-receipts.txt");
            Log.Message("[CA][Exercise] receipts written");
        }

        private void Append(string title, Func<string> run)
        {
            report.AppendLine("---- " + title + " ----");
            try { report.AppendLine(run()); }
            catch (Exception e)
            { report.AppendLine("FAILED: " + e); }
            report.AppendLine("");
        }

        private void Jump(int slot)
        {
            try
            {
                CARegionalSettlementRecord record =
                    CARegionalWorldComponent.Current
                        ?.ForMap(Find.CurrentMap)
                        .FirstOrDefault(r => r != null && r.slot == slot)
                    ?? CARegionalWorldComponent.Current
                        ?.ForMap(Find.CurrentMap).FirstOrDefault();
                if (record != null && record.localRect != CellRect.Empty)
                    CameraJumper.TryJump(record.localRect.CenterCell,
                        Find.CurrentMap);
            }
            catch (Exception) { }
        }

        private void Capture(string name)
        {
            try
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(
                    CAConvergenceExercise.DevOutputDir, name));
            }
            catch (Exception e)
            {
                report.AppendLine("screenshot " + name + " failed: " + e);
            }
        }

        private void Write(string name)
        {
            try
            {
                Directory.CreateDirectory(
                    CAConvergenceExercise.DevOutputDir);
                File.WriteAllText(Path.Combine(
                    CAConvergenceExercise.DevOutputDir, name),
                    report.ToString());
            }
            catch (Exception e)
            {
                Log.Error("[CA][Exercise] write failed: " + e);
            }
        }

        // The watchdog branch is reached on EVERY frame once elapsed passes
        // its threshold. A shutting-down run only ever saw one call because
        // Root.Shutdown ended the process; a -hold run returns instead, so
        // without this guard the watchdog re-fires every frame forever --
        // rewriting ca-convergence-receipts.txt hundreds of times a second
        // and burying Player.log, which is itself the receipt surface.
        private bool finished;

        private void Finish(string how)
        {
            if (finished) return;
            finished = true;
            report.AppendLine("==== exercise finished: " + how + " ====");
            Write("ca-convergence-receipts.txt");
            CAConvergenceExercise.ReleaseFixedRunRandomState();
            // A -hold run exists to be PLAYED after generation: the
            // operator (or an input multiplexer driving a virtual cursor)
            // takes the session from here, so the exercise ends itself
            // without ending the process.
            if (CAConvergenceExercise.HoldSession)
            {
                Log.Message("[CA][Exercise] " + how
                    + "; HOLD: session left interactive for live play");
                return;
            }
            Log.Message("[CA][Exercise] " + how + "; shutting down");
            Root.Shutdown();
        }
    }
}
