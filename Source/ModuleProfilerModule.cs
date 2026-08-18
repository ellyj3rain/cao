using LudeonTK;
using Verse;

namespace ColonistAwareness
{
    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness", "Profiler: enable and reset",
            actionType = DebugActionType.Action)]
        private static void EnableModuleProfiler()
        {
            CAModuleProfiler.SetEnabled(true, reset: true);
            Log.Message("[CA][Profiler] enabled with fixed bounded counters; "
                + "simulation state is unchanged.");
        }

        [DebugAction("Colonist Awareness", "Profiler: disable",
            actionType = DebugActionType.Action)]
        private static void DisableModuleProfiler()
        {
            CAModuleProfiler.SetEnabled(false);
            Log.Message("[CA][Profiler] disabled; counters retained until "
                + "reset or the process exits.");
        }

        [DebugAction("Colonist Awareness", "Profiler: log snapshot",
            actionType = DebugActionType.Action)]
        private static void LogModuleProfilerSnapshot()
        {
            Log.Message(CAModuleProfiler.Snapshot().ToLogText());
        }

        [DebugAction("Colonist Awareness", "Profiler: reset counters",
            actionType = DebugActionType.Action)]
        private static void ResetModuleProfiler()
        {
            CAModuleProfiler.Reset();
            Log.Message("[CA][Profiler] counters reset.");
        }
    }
}
