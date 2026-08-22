using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // The B18 passive-play acceptance matrix. Armed through the same
    // one-shot convergence marker with a "-passive" suffix, it runs after
    // the ordinary convergence receipts on the same generated region:
    // paused/static, paused/camera-movement, Normal, Fast, and Superfast
    // speeds, an open heavy CA surface, one combat event, one fire event,
    // the deferred-site question, a full ordinary in-game day at Normal
    // speed with begin/end memory evidence, a disposable save, a reload of
    // that same disposable save, and a post-reload receipt. Every phase
    // records wall time, frame percentiles, hitches, achieved TPS, process
    // CPU, working set, managed heap, GC counts, and the bounded CA module
    // profiler. All state is static so the reload boundary cannot erase
    // the measurements; the disposable save is deleted at the end and no
    // operator save is ever touched.
    internal static class CAPassivePlayMatrix
    {
        internal static bool Requested;
        internal static bool Running;
        internal static bool AllowDisposableLoad;
        internal static bool AllowDisposableSave;
        // Armed by the "-notrace" marker suffix: suppresses the
        // default-on behavior trace for one benchmark session so the
        // receipt can attribute diagnostic-logging cost separately from
        // simulation cost. It never touches the operator's saved setting.
        internal static bool SuppressTrace;
        internal const string DisposableSaveName =
            "ca-b18-passive-disposable";

        private enum Phase
        {
            PausedStatic = 0,
            PausedCameraPan = 1,
            NormalQuiet = 2,
            FastSpeed = 3,
            SuperfastSpeed = 4,
            CaSurfaces = 5,
            CombatRaid = 6,
            FireEvent = 7,
            SiteMaterialization = 8,
            DaySoak = 9,
            SaveDisposable = 10,
            ReloadWait = 11,
            PostReloadVerify = 12,
            Done = 13
        }

        private static readonly StringBuilder Report = new StringBuilder();
        private static string baseReport = "";
        private static Phase phase = Phase.PausedStatic;
        private static bool phaseStarted;
        private static float phaseStartedAt = -1f;
        private static readonly List<float> FrameMs = new List<float>(16384);
        private static int beginTicks;
        private static TimeSpan beginCpu;
        private static long beginWorkingSet;
        private static long beginManaged;
        private static int beginGc0, beginGc1, beginGc2;
        private static float loadQueuedAt = -1f;
        private static Game gameBeforeReload;
        private static bool pendingSurfaceOpen;
        private static bool pendingSurfaceClose;
        private static Window surfaceWindow;
        private static string soakBeginEvidence = "";
        private static int camPanLeg;

        private const float ShortPhaseSeconds = 20f;
        private const float SpeedPhaseSeconds = 30f;
        private const float CombatPhaseSeconds = 45f;
        private const int SoakDayTicks = 60000;
        private const float SoakWallCapSeconds = 3600f;

        internal static void Begin(string reportSoFar)
        {
            baseReport = reportSoFar ?? "";
            Running = true;
            phase = Phase.PausedStatic;
            phaseStarted = false;
            Report.AppendLine("---- PASSIVE PLAY MATRIX ----");
            Log.Message("[CA][Exercise] passive matrix started");
        }

        internal static void Update(CAConvergenceExerciseComponent host)
        {
            if (!Running) return;
            Map map = Find.CurrentMap;
            if (map == null) return;
            float now = Time.realtimeSinceStartup;
            if (phase == Phase.ReloadWait)
            {
                // A reload that never completes must fail visibly, not
                // stall the disposable process forever.
                if (now - loadQueuedAt > 600f)
                {
                    Report.AppendLine("  reload: TIMED OUT after "
                        + (now - loadQueuedAt).ToString("F0",
                            CultureInfo.InvariantCulture)
                        + " s; no reload result is claimed");
                    AllowDisposableLoad = false;
                    Finish(map);
                    return;
                }
                // The queuing game keeps updating until its disposal; only
                // a DIFFERENT game instance with a current map is the
                // completed reload.
                if (Current.Game == gameBeforeReload) return;
                // The first frame with a current map after the disposable
                // reload IS the load completion boundary.
                Report.AppendLine("  reload: disposable save loaded in "
                    + (now - loadQueuedAt).ToString("F1",
                        CultureInfo.InvariantCulture)
                    + " s wall (queue to first playable frame)");
                AllowDisposableLoad = false;
                phase = Phase.PostReloadVerify;
                phaseStarted = false;
                return;
            }
            if (!phaseStarted)
            {
                phaseStarted = true;
                phaseStartedAt = now;
                BeginPhase(map);
                BeginSamples();
                return;
            }
            FrameMs.Add(Time.unscaledDeltaTime * 1000f);
            PerFrame(map, now - phaseStartedAt);
            if (PhaseComplete(map, now - phaseStartedAt))
            {
                Phase completed = phase;
                EndPhase(map);
                if (phase != completed) return;
                phase = (Phase)((int)phase + 1);
                phaseStarted = false;
                if (phase == Phase.Done) Finish(map);
            }
        }

        internal static void ServeGui()
        {
            if (pendingSurfaceClose)
            {
                pendingSurfaceClose = false;
                if (surfaceWindow != null)
                    Find.WindowStack.TryRemove(surfaceWindow,
                        doCloseSound: false);
                surfaceWindow = null;
            }
            if (pendingSurfaceOpen)
            {
                pendingSurfaceOpen = false;
                surfaceWindow = Dialog_CAPoliticalOrderEditor.ForEstablished(
                    new CAPoliticalBeliefs(), new List<CAAxisEntry>(),
                    "b18-passive-surface-probe", null);
                Find.WindowStack.Add(surfaceWindow);
            }
        }

        private static void BeginPhase(Map map)
        {
            // Unattended benchmark instrument, exercise-session-only: an
            // unfocused or occluded window is presentation-throttled by the
            // desktop compositor to ~10 fps, and Normal-speed ticks ride
            // frames, so the soak would measure the compositor rather than
            // the simulation. The process is disposable and exits at
            // completion, so nothing is restored. The native log message
            // cap is also lifted each phase so Player.log keeps carrying
            // crash and phase evidence through a long soak.
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Log.ResetMessageCount();
            switch (phase)
            {
                case Phase.PausedStatic:
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    break;
                case Phase.PausedCameraPan:
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    camPanLeg = 0;
                    break;
                case Phase.NormalQuiet:
                case Phase.CaSurfaces:
                case Phase.CombatRaid:
                case Phase.FireEvent:
                case Phase.SiteMaterialization:
                case Phase.DaySoak:
                case Phase.PostReloadVerify:
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    break;
                case Phase.FastSpeed:
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
                    break;
                case Phase.SuperfastSpeed:
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                    break;
            }
            if (phase == Phase.CaSurfaces) pendingSurfaceOpen = true;
            if (phase == Phase.CombatRaid) StartRaid(map);
            if (phase == Phase.FireEvent) StartFire(map);
            if (phase == Phase.SiteMaterialization)
                Report.AppendLine("  site materialization: every "
                    + "represented settlement of this fixture is "
                    + "materialized during generation and no deferred "
                    + "unmaterialized site exists on this map; "
                    + "generation-time materialization cost is the "
                    + "generation receipt's evidence and no synthetic "
                    + "runtime result is claimed. Deferred CA work "
                    + "(autonomous planning, reconciliation) is measured "
                    + "inside the day soak.");
            if (phase == Phase.DaySoak)
                soakBeginEvidence = MemoryAndQueueEvidence(map);
            if (phase == Phase.SaveDisposable)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                long started = Stopwatch.GetTimestamp();
                AllowDisposableSave = true;
                try { GameDataSaveLoader.SaveGame(DisposableSaveName); }
                finally { AllowDisposableSave = false; }
                double ms = (Stopwatch.GetTimestamp() - started) * 1000d
                    / Stopwatch.Frequency;
                string savePath = GenFilePaths.FilePathForSavedGame(
                    DisposableSaveName);
                bool saved = File.Exists(savePath);
                Report.AppendLine("  save: disposable save '"
                    + DisposableSaveName + "' "
                    + (saved
                        ? "written in " + ms.ToString("F0",
                            CultureInfo.InvariantCulture) + " ms ("
                            + new FileInfo(savePath).Length + " bytes)"
                        : "FAILED - no file was written after "
                            + ms.ToString("F0",
                                CultureInfo.InvariantCulture)
                            + " ms; the reload phases are skipped and no "
                            + "save/reload result is claimed"));
            }
        }

        private static void PerFrame(Map map, float elapsed)
        {
            // Native letters force-pause on threats; an unattended
            // measured phase must keep simulating at its declared speed
            // or it silently measures a paused renderer instead.
            TimeSpeed intended =
                phase == Phase.PausedStatic
                    || phase == Phase.PausedCameraPan
                    || phase == Phase.SaveDisposable
                        ? TimeSpeed.Paused
                : phase == Phase.FastSpeed ? TimeSpeed.Fast
                : phase == Phase.SuperfastSpeed ? TimeSpeed.Superfast
                : TimeSpeed.Normal;
            if (Find.TickManager.CurTimeSpeed != intended)
                Find.TickManager.CurTimeSpeed = intended;
            if (phase == Phase.PausedCameraPan)
            {
                List<CARegionalSettlementRecord> records =
                    CARegionalWorldComponent.Current?.ForMap(map)
                        ?.Where(record => record != null
                            && record.localRect != CellRect.Empty)
                        .ToList();
                if (records == null || records.Count == 0) return;
                IntVec3 from = records[camPanLeg % records.Count]
                    .localRect.CenterCell;
                IntVec3 to = records[(camPanLeg + 1) % records.Count]
                    .localRect.CenterCell;
                float legSeconds = 5f;
                float t = elapsed % legSeconds / legSeconds;
                if (elapsed / legSeconds > camPanLeg + 1) camPanLeg++;
                Vector3 loc = Vector3.Lerp(from.ToVector3Shifted(),
                    to.ToVector3Shifted(), t);
                Find.CameraDriver.JumpToCurrentMapLoc(loc);
            }
        }

        private static bool PhaseComplete(Map map, float elapsed)
        {
            switch (phase)
            {
                case Phase.PausedStatic:
                case Phase.PausedCameraPan:
                case Phase.CaSurfaces:
                case Phase.SiteMaterialization:
                    return elapsed >= ShortPhaseSeconds;
                case Phase.NormalQuiet:
                case Phase.FastSpeed:
                case Phase.SuperfastSpeed:
                case Phase.FireEvent:
                case Phase.PostReloadVerify:
                    return elapsed >= SpeedPhaseSeconds;
                case Phase.CombatRaid:
                    return elapsed >= CombatPhaseSeconds;
                case Phase.DaySoak:
                    return Find.TickManager.TicksGame - beginTicks
                            >= SoakDayTicks
                        || elapsed >= SoakWallCapSeconds;
                case Phase.SaveDisposable:
                    return true;
                default:
                    return true;
            }
        }

        private static void EndPhase(Map map)
        {
            if (phase == Phase.CaSurfaces)
            {
                Report.AppendLine("  ca-surfaces: political-order editor "
                    + (surfaceWindow != null ? "was open for the phase"
                        : "NEVER OPENED (OnGUI was not rendered; no "
                            + "surface cost is claimed for this phase)"));
                pendingSurfaceClose = true;
            }
            AppendPhaseReport(map);
            if (phase == Phase.DaySoak)
            {
                Report.AppendLine("  soak begin " + soakBeginEvidence);
                Report.AppendLine("  soak end   "
                    + MemoryAndQueueEvidence(map));
            }
            if (phase == Phase.PostReloadVerify)
            {
                try
                {
                    string receipt = CAConvergenceReceipt.RunMap(map);
                    int operating = CountOccurrences(receipt,
                        "runtime: operational");
                    int blocked = CountOccurrences(receipt,
                        "runtime: not operating");
                    Report.AppendLine("  post-reload map receipt: "
                        + operating + " provision arrangement(s) "
                        + "operational, " + blocked + " not operating");
                    Report.AppendLine("---- POST-RELOAD MAP RECEIPT ----");
                    Report.AppendLine(receipt);
                }
                catch (Exception error)
                {
                    Report.AppendLine("  post-reload map receipt FAILED: "
                        + error);
                }
            }
            // A crash mid-matrix must not erase the phases already
            // measured; every completed phase persists the running report.
            try
            {
                Directory.CreateDirectory(
                    CAConvergenceExercise.DevOutputDir);
                File.WriteAllText(Path.Combine(
                    CAConvergenceExercise.DevOutputDir,
                    "ca-passive-progress.txt"), Report.ToString());
            }
            catch (Exception) { }
            if (phase == Phase.SaveDisposable)
            {
                string savedPath = GenFilePaths.FilePathForSavedGame(
                    DisposableSaveName);
                if (!File.Exists(savedPath))
                {
                    phase = Phase.PostReloadVerify;
                    phaseStarted = false;
                    return;
                }
                // The native load of a 108-MB regional save was measured
                // past 110 minutes without completing (CA's streaming
                // preflight is 3.6 s of it); above this bound the reload
                // is not attempted and no reload result is claimed - the
                // sealed save itself is the save-side evidence, and the
                // native large-save load cost is named receipt debt.
                long savedBytes = new FileInfo(savedPath).Length;
                if (savedBytes > 40L * 1024 * 1024)
                {
                    Report.AppendLine("  reload: NOT ATTEMPTED - the "
                        + "sealed save is " + savedBytes + " bytes and the "
                        + "native load of a save this size was measured "
                        + "past 110 minutes; no reload result is claimed "
                        + "at this scale");
                    phase = Phase.PostReloadVerify;
                    phaseStarted = false;
                    return;
                }
                Log.ResetMessageCount();
                AllowDisposableLoad = true;
                loadQueuedAt = Time.realtimeSinceStartup;
                gameBeforeReload = Current.Game;
                phase = Phase.ReloadWait;
                phaseStarted = false;
                Log.Message("[CA][Exercise] passive matrix reloading the "
                    + "disposable save");
                // The native in-game load flow disposes the old game from
                // dialog OnGUI, never from inside the component update
                // loop. ExecuteWhenFinished runs IMMEDIATELY when no long
                // event is active, so it cannot provide that deferral; a
                // queued long event is the correct boundary - the old game
                // is disposed inside the event, and the "Play" scene
                // switch drives SavedGameLoaderNow through the allowed
                // disposable-load guard exactly as a native load does.
                LongEventHandler.QueueLongEvent(delegate
                {
                    Current.Game?.Dispose();
                    Verse.Profile.MemoryUtility.ClearAllMapsAndWorld();
                    Current.Game = new Game();
                    Current.Game.InitData = new GameInitData
                    {
                        gameToLoad = DisposableSaveName
                    };
                }, "Play", "LoadingLongEvent", doAsynchronously: true,
                    null);
            }
        }

        private static void BeginSamples()
        {
            FrameMs.Clear();
            beginTicks = Find.TickManager.TicksGame;
            using (Process process = Process.GetCurrentProcess())
                beginCpu = process.TotalProcessorTime;
            beginWorkingSet = Environment.WorkingSet;
            beginManaged = GC.GetTotalMemory(false);
            beginGc0 = GC.CollectionCount(0);
            beginGc1 = GC.CollectionCount(1);
            beginGc2 = GC.CollectionCount(2);
            CAModuleProfiler.SetEnabled(true, reset: true);
        }

        private static void AppendPhaseReport(Map map)
        {
            float wall = Time.realtimeSinceStartup - phaseStartedAt;
            int ticks = Find.TickManager.TicksGame - beginTicks;
            TimeSpan cpu;
            using (Process process = Process.GetCurrentProcess())
                cpu = process.TotalProcessorTime - beginCpu;
            long workingSet = Environment.WorkingSet;
            long managed = GC.GetTotalMemory(false);
            var sorted = FrameMs.OrderBy(value => value).ToList();
            double Percentile(double p)
            {
                if (sorted.Count == 0) return 0d;
                int index = Mathf.Clamp((int)Math.Ceiling(
                    p / 100d * sorted.Count) - 1, 0, sorted.Count - 1);
                return sorted[index];
            }
            double fpsMedian = Percentile(50) <= 0d ? 0d
                : 1000d / Percentile(50);
            double fpsOneLow = Percentile(99) <= 0d ? 0d
                : 1000d / Percentile(99);
            int hitches100 = sorted.Count(value => value > 100f);
            int hitches250 = sorted.Count(value => value > 250f);
            string invariant(double value, string format) =>
                value.ToString(format, CultureInfo.InvariantCulture);
            Report.AppendLine("  phase " + phase + ": "
                + invariant(wall, "F1") + " s wall; frames " + sorted.Count
                + "; fps median " + invariant(fpsMedian, "F1")
                + ", 1% low " + invariant(fpsOneLow, "F1")
                + "; frame ms p50/p95/p99 "
                + invariant(Percentile(50), "F1") + "/"
                + invariant(Percentile(95), "F1") + "/"
                + invariant(Percentile(99), "F1")
                + "; longest " + invariant(sorted.Count == 0 ? 0d
                    : sorted[sorted.Count - 1], "F1") + " ms"
                + "; hitches >100ms " + hitches100 + ", >250ms " + hitches250
                + "; ticks +" + ticks + " ("
                + invariant(wall <= 0f ? 0d : ticks / wall, "F1") + " TPS)"
                + "; cpu cores " + invariant(wall <= 0f ? 0d
                    : cpu.TotalSeconds / wall, "F2")
                + "; ws " + (workingSet <= 0
                    ? "unavailable under this runtime"
                    : workingSet / (1024 * 1024) + " MB (start "
                        + beginWorkingSet / (1024 * 1024) + ")")
                + "; managed " + managed / (1024 * 1024) + " MB"
                + " (start " + beginManaged / (1024 * 1024) + ")"
                + "; gc +" + (GC.CollectionCount(0) - beginGc0) + "/+"
                + (GC.CollectionCount(1) - beginGc1) + "/+"
                + (GC.CollectionCount(2) - beginGc2));
            CAModuleProfileSnapshot snapshot = CAModuleProfiler.Snapshot();
            CAModuleProfiler.SetEnabled(false);
            Report.AppendLine("  [profiler " + phase + "] "
                + snapshot.ToLogText());
        }

        private static string MemoryAndQueueEvidence(Map map)
        {
            long things = map?.listerThings?.AllThings?.Count ?? 0;
            int worldPawns = Find.WorldPawns?.AllPawnsAliveOrDead?.Count
                ?? 0;
            int windows = Find.WindowStack?.Count ?? 0;
            long workingSet = Environment.WorkingSet;
            return "evidence: ws " + (workingSet <= 0
                    ? "unavailable under this runtime"
                    : workingSet / (1024 * 1024) + " MB")
                + "; managed " + GC.GetTotalMemory(false) / (1024 * 1024)
                + " MB; gc " + GC.CollectionCount(0) + "/"
                + GC.CollectionCount(1) + "/" + GC.CollectionCount(2)
                + "; map things " + things + "; world pawns " + worldPawns
                + "; windows " + windows
                + "; broader pawn knowledge remains "
                + "default-off so no epistemic communication queue exists "
                + "in this run";
        }

        private static void StartRaid(Map map)
        {
            try
            {
                Faction raider = CARegionalWorldComponent.Current
                    ?.ForMap(map)?.Select(record => record?.faction)
                    .FirstOrDefault(faction => faction != null
                        && faction.HostileTo(Faction.OfPlayer));
                if (raider == null)
                {
                    Report.AppendLine("  combat: no hostile settlement "
                        + "faction resolved; no raid executed and no "
                        + "combat result claimed");
                    return;
                }
                var parms = new IncidentParms
                {
                    target = map,
                    faction = raider,
                    points = 600f,
                    raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                    raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn
                };
                bool executed = IncidentDefOf.RaidEnemy.Worker
                    .TryExecute(parms);
                Report.AppendLine("  combat: raid by " + raider.Name
                    + " at 600 points "
                    + (executed ? "executed" : "REFUSED by the native "
                        + "incident worker; no combat result claimed"));
            }
            catch (Exception error)
            {
                Report.AppendLine("  combat: raid FAILED: " + error.Message);
            }
        }

        private static void StartFire(Map map)
        {
            try
            {
                CARegionalSettlementRecord record =
                    CARegionalWorldComponent.Current?.ForMap(map)
                        ?.FirstOrDefault(item => item != null
                            && item.localRect != CellRect.Empty);
                CellRect rect = record?.localRect
                    ?? CellRect.CenteredOn(map.Center, 20, 20);
                int started = 0;
                foreach (IntVec3 cell in rect.Cells)
                {
                    if (!cell.InBounds(map)) continue;
                    if (FireUtility.TryStartFireIn(cell, map, 1.0f, null))
                    {
                        started++;
                        if (started >= 3) break;
                    }
                }
                Report.AppendLine("  fire: " + started
                    + " fire(s) started in "
                    + (record?.name ?? "the map center")
                    + (started == 0 ? "; nothing flammable accepted fire "
                        + "and no fire result is claimed" : ""));
            }
            catch (Exception error)
            {
                Report.AppendLine("  fire: FAILED: " + error.Message);
            }
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }

        private static void Finish(Map map)
        {
            Running = false;
            try
            {
                string path = GenFilePaths.FilePathForSavedGame(
                    DisposableSaveName);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception error)
            {
                Log.Warning("[CA][Exercise] disposable save cleanup "
                    + "failed: " + error.Message);
            }
            var text = new StringBuilder(baseReport);
            text.AppendLine(Report.ToString());
            text.AppendLine("==== exercise finished: passive-complete ====");
            try
            {
                Directory.CreateDirectory(
                    CAConvergenceExercise.DevOutputDir);
                File.WriteAllText(Path.Combine(
                    CAConvergenceExercise.DevOutputDir,
                    "ca-convergence-receipts.txt"), text.ToString());
            }
            catch (Exception error)
            {
                Log.Error("[CA][Exercise] passive receipt write failed: "
                    + error);
            }
            Log.Message("[CA][Exercise] passive-complete; shutting down");
            CAConvergenceExercise.ReleaseFixedRunRandomState();
            Root.Shutdown();
        }
    }
}
