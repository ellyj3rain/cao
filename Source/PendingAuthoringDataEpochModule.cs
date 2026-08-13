using Verse;

namespace ColonistAwareness
{
    // Pending creation drafts and reusable authoring profiles may be discarded
    // when their pre-release schema is no longer supported. Realized campaign
    // state is governed separately by CACampaignCompatibility and may never be
    // deleted through this epoch.
    internal static class CAPendingAuthoringDataEpoch
    {
        internal const int Current = 11;
        private static bool diagnosticScheduled;
        private static bool diagnosticEmitted;

        internal static bool IsCurrent(int value)
        {
            return value == Current;
        }

        internal static void RecordDiscard(string owner)
        {
            if (diagnosticScheduled || diagnosticEmitted) return;
            diagnosticScheduled = true;
            LongEventHandler.ExecuteWhenFinished(EmitDiagnostic);
        }

        private static void EmitDiagnostic()
        {
            diagnosticScheduled = false;
            if (diagnosticEmitted) return;
            diagnosticEmitted = true;
            Log.Warning("[CA][Authoring] An incompatible pending creation "
                + "draft or reusable authoring profile was discarded. "
                + "Realized campaign history is governed by the durable "
                + "campaign schema and was not reset through this path.");
        }
    }
}
