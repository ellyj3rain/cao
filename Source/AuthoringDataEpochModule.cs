using Verse;

namespace ColonistAwareness
{
    // The current causal authoring contract deliberately breaks incompatible
    // pre-release state. The
    // epoch is checked only by containers that own complete authoring state.
    // Nested serializers therefore describe one current schema and never act
    // as compatibility readers for abandoned development objects.
    internal static class CAAuthoringDataEpoch
    {
        internal const int Current = 10;
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
            Log.Warning("[CA][Authoring] Incompatible pre-current CA state "
                + "was discarded, not migrated. Intentional Culture, "
                + "Political Beliefs, founding state, and starting-region "
                + "facts remain authoring inputs; obsolete derived social "
                + "state must be regenerated.");
        }
    }
}
