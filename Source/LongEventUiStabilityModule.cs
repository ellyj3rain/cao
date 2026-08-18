using System;
using HarmonyLib;
using Verse;
using Verse.Sound;

namespace ColonistAwareness
{
    // Root.Update deliberately skips UIRootUpdate while a non-standard long
    // event blocks the interface. LongEventsOnGUI still draws ModSummaryWindow,
    // whose invisible buttons enqueue mouseover regions on every repaint.
    // Vanilla normally drains that list from UIRootUpdate, so an unusually long
    // regional generation grows it until List<T> capacity expansion fails.
    // Drain it at the same frame boundary only when the ordinary drain is
    // skipped. Silence prevents loading-screen controls from emitting sounds.
    [HarmonyPatch(typeof(LongEventHandler),
        nameof(LongEventHandler.LongEventsOnGUI))]
    internal static class CALongEventMouseoverDrainPatch
    {
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            try
            {
                DrainIfWaiting();
            }
            catch
            {
                // The original UI exception remains authoritative.
            }
            return __exception;
        }

        private static void DrainIfWaiting()
        {
            if (!LongEventHandler.ShouldWaitForEvent) return;
            MouseoverSounds.SilenceForNextFrame();
            MouseoverSounds.ResolveFrame();
        }
    }
}
