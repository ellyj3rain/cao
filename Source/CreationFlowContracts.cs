using System;

namespace ColonistAwareness
{
    // Small causal rules shared by authoring surfaces and standalone receipts.
    // They contain no Verse state, so the exact transitions can be exercised
    // without opening RimWorld.
    internal static class CACreationFlowContracts
    {
        internal const int MinimumMainPopulationShare = 20;

        internal static int MaximumMinorityShare(int otherMinorityShares)
        {
            return Math.Max(0, 100 - MinimumMainPopulationShare
                - Math.Max(0, otherMinorityShares));
        }

        internal static int ClampMinorityShare(int requestedShare,
            int otherMinorityShares)
        {
            return Math.Max(0, Math.Min(Math.Max(0, requestedShare),
                MaximumMinorityShare(otherMinorityShares)));
        }

        internal static int MainPopulationShare(int minorityShares)
        {
            return 100 - Math.Min(100 - MinimumMainPopulationShare,
                Math.Max(0, minorityShares));
        }

        internal static bool FollowsAffiliation(int explicitSourceKey)
        {
            return explicitSourceKey < 0;
        }

        internal static int EffectiveSourceKey(int explicitSourceKey,
            int affiliationKey)
        {
            return FollowsAffiliation(explicitSourceKey)
                ? affiliationKey : explicitSourceKey;
        }
    }
}
