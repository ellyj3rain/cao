using System;
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
        // The exercise's scale, parsed from the arming value: "1" runs
        // the cheap 200x4 miniature; "350x8" runs a real play-scale
        // regional landmass. The mod's basis IS the large map, so the
        // full-scale form is what a demonstration run should use.
        internal static int SourceScale = 200;
        internal static int RegionTiles = 4;
        // Set ONLY by the quicktest postfix that authored the candidate.
        // The run-and-report component requires it, so the component can
        // never act on - and never shut down - a game a person started.
        internal static bool AuthoredThisSession;
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
                if (armed != "1" && armed.Contains("x"))
                {
                    string[] parts = armed.Split('x');
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
            Log.Warning("[CA][Exercise] BLOCKED save-load attempt for '"
                + (fileName ?? "null") + "' during the exercise session. "
                + "Caller:\n" + Environment.StackTrace);
            return false;
        }

        private static void BuildAndLaunch()
        {
            try
            {
                Root_Play.SetupForQuickTestPlay();
                AuthorCandidate();
                if (!AuthoredThisSession)
                {
                    Log.Error("[CA][Exercise] authoring did not complete; "
                        + "not launching a game");
                    Current.Game = null;
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
                Find.GameInitData.ChooseRandomStartingTile();
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

                // Two factions with different political beliefs.
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
                    .EligibleNewFactionDefs().FirstOrDefault(def =>
                        def.techLevel <= TechLevel.Industrial)
                    ?? CARegionalPlanUtility.EligibleNewFactionDefs()
                        .FirstOrDefault();
                councilFaction.customFactionDefName = template?.defName;
                tradeFaction.customFactionDefName = template?.defName;
                plan.factions.Add(councilFaction);
                plan.factions.Add(tradeFaction);
                councilFaction.EnsureCultureAndPolitics(plan);
                tradeFaction.EnsureCultureAndPolitics(plan);
                CAPoliticalBeliefsModel.ApplyProfile(
                    councilFaction.politicalBeliefs,
                    CAFactionAxes.PoliticalProfiles.First(profileItem =>
                        profileItem.Key == "worker_federation"));
                CAPoliticalBeliefsModel.ApplyProfile(
                    tradeFaction.politicalBeliefs,
                    CAFactionAxes.PoliticalProfiles.First(profileItem =>
                        profileItem.Key == "civic_council"));
                CAFactionAxes.Derive(plan, councilFaction);
                CAFactionAxes.Derive(plan, tradeFaction);

                // Three settlements: the council faction holds two, the trade
                // faction one. The first has a concentrated minority whose
                // affiliation, culture, Ideoligion, and political beliefs are
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
                    plan.settlements.Add(new CARegionalSettlementPlan
                    {
                        slot = i,
                        memberTileId = members[memberIndex],
                        factionKey = i < settlementCount - 1 ? 1 : 2,
                        siteClusterKey = i,
                        persistent = true,
                        populationOrigin =
                            CASettlementOrigin.ScenarioOverride,
                        reallocatedFromTileId = -1
                    });
                }
                CARegionalPlanUtility.EnsureRelationRows(plan);
                foreach (CARegionalSettlementPlan settlementPlan in plan.settlements)
                    CASettlementComposition.EnsureDerived(plan, settlementPlan);
                CARegionalSettlementPlan first = plan.settlements[0];
                if (!first.populationGroups.Any(c => c != null
                    && c.kind == CAPopulationGroupKind.OtherFaction))
                {
                    first.populationGroups.Insert(1, new CASettlementPopulationGroup
                    {
                        key = 7,
                        kind = CAPopulationGroupKind.OtherFaction,
                        label = "Charter Towns",
                        share = 20,
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
                CARegionalSettlements.EnsureSettlementPattern(plan);

                string failure;
                if (!CARegionalPlanUtility.TryValidateStartingSettlements(
                        plan, out failure))
                    Log.Warning("[CA][Exercise] validation: " + failure);
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
                if (Find.CurrentMap == null) return;
                float now = Time.realtimeSinceStartup;
                if (mapReadyAt < 0f)
                {
                    mapReadyAt = now;
                    if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused)
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    return;
                }
                float elapsed = now - mapReadyAt;
                if (stage == 0 && elapsed >= 15f)
                {
                    stage = 1;
                    RunReceipts();
                    Jump(0);
                }
                else if (stage == 1 && elapsed >= 22f)
                {
                    stage = 2;
                    Capture("ca-exercise-settlement-a.png");
                }
                else if (stage == 2 && elapsed >= 30f)
                {
                    stage = 3;
                    Jump(2);
                }
                else if (stage == 3 && elapsed >= 36f)
                {
                    stage = 4;
                    Capture("ca-exercise-settlement-b.png");
                }
                else if (stage == 4 && elapsed >= 44f)
                {
                    stage = 5;
                    CameraJumper.TryShowWorld();
                }
                else if (stage == 5 && elapsed >= 50f)
                {
                    stage = 6;
                    Capture("ca-exercise-world.png");
                }
                else if (stage == 6 && elapsed >= 58f)
                {
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

        private void Finish(string how)
        {
            report.AppendLine("==== exercise finished: " + how + " ====");
            Write("ca-convergence-receipts.txt");
            Log.Message("[CA][Exercise] " + how + "; shutting down");
            Root.Shutdown();
        }
    }
}
