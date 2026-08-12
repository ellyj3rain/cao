using Verse;

namespace ColonistAwareness
{
    // B8 deliberately breaks the pre-release authoring data contract. The
    // epoch is checked only by containers that own complete authoring state.
    // Nested serializers therefore describe one current schema and never act
    // as compatibility readers for abandoned development objects.
    internal static class CAAuthoringDataEpoch
    {
        internal const int Current = 8;
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
            Log.Warning("[CA][B8] Incompatible pre-B8 CA authoring state "
                + "was discarded, not migrated. Re-author Culture, Political Beliefs, "
                + "founding state, and any affected starting region.");
        }
    }
}
