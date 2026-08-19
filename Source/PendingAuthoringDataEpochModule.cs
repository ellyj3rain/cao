using System.Collections.Generic;
using Verse;

namespace ColonistAwareness
{
    // Pending creation drafts and reusable authoring profiles may be discarded
    // when their pre-release schema is no longer supported. Realized campaign
    // state is governed separately by CACampaignCompatibility and may never be
    // deleted through this epoch.
    internal static class CAPendingAuthoringDataEpoch
    {
        // B17 makes ownership, support, and population affiliation explicit
        // site relationships. Epoch 14 is the one supported adjacent source;
        // its B16 Culture, Political Order, Technological Knowledge, and
        // authored fixture remain the factual input to the atomic conversion.
        internal const int Current = 15;
        internal const int Previous = 14;
        private static bool diagnosticScheduled;
        private static bool diagnosticEmitted;

        internal static bool IsCurrent(int value)
        {
            return value == Current;
        }

        internal static bool CanRead(int value)
        {
            return value == Current || value == Previous;
        }

        // Pending regional drafts are not campaign state, but the current
        // authored fixture is valuable operator work. The adjacent B16 draft
        // therefore receives one explicit, atomic conversion. Culture and the
        // complete site candidate graph validate before any assignment; a
        // failure leaves the source graph and schema stamp intact.
        internal static bool TryUpgradeRegionalPlan(int sourceEpoch,
            CARegionalPlan plan, out string failure)
        {
            failure = null;
            if (plan == null)
            {
                failure = "regional plan is missing";
                return false;
            }
            if (sourceEpoch == Current)
            {
                if (plan.schemaVersion != CARegionalPlan.CurrentSchemaVersion)
                {
                    failure = "current authoring epoch contains regional plan "
                        + "schema " + plan.schemaVersion + ", expected "
                        + CARegionalPlan.CurrentSchemaVersion;
                    return false;
                }
                if (!ValidateCurrentCultureGraph(plan, out failure))
                    return false;
                failure = CASiteAffiliationMigration
                    .ValidateCurrentRegionalPlan(plan,
                        requireCompleteSites: false);
                return failure.NullOrEmpty();
            }
            if (sourceEpoch != Previous || plan.schemaVersion != 15)
            {
                failure = "unsupported pending authoring epoch/schema "
                    + sourceEpoch + "/" + plan.schemaVersion;
                return false;
            }
            if (plan.factions == null || plan.settlements == null
                || plan.playerFounding == null)
            {
                failure = "regional plan ownership graph is incomplete";
                return false;
            }

            if (!ValidateCurrentCultureGraph(plan, out failure))
                return false;
            if (!CASiteAffiliationMigration.TryPrepareRegionalPlan(plan, 15,
                    requireCompleteSites: false,
                    out List<System.Action> commits, out failure))
                return false;
            foreach (System.Action commit in commits) commit();
            plan.schemaVersion = CARegionalPlan.CurrentSchemaVersion;
            return true;
        }

        private static bool ValidateCurrentCultureGraph(CARegionalPlan plan,
            out string failure)
        {
            failure = null;
            if (plan.factions == null || plan.settlements == null
                || plan.playerFounding == null)
            {
                failure = "regional plan ownership graph is incomplete";
                return false;
            }
            foreach (CARegionalFactionPlan faction in plan.factions)
            {
                if (faction == null)
                {
                    failure = "regional plan contains a null faction";
                    return false;
                }
                string cultureFailure = CACultureModel.ValidationFailure(
                    faction.culture, requireSubstantive: true);
                if (!cultureFailure.NullOrEmpty())
                {
                    failure = "faction " + faction.key + " Culture: "
                        + cultureFailure;
                    return false;
                }
            }
            foreach (CARegionalSettlementPlan settlement in plan.settlements)
            {
                if (settlement == null)
                {
                    failure = "regional plan contains a null settlement";
                    return false;
                }
                if (settlement.localCulture == null) continue;
                string cultureFailure = CACultureModel.ValidationFailure(
                    settlement.localCulture, requireSubstantive: true);
                if (!cultureFailure.NullOrEmpty())
                {
                    failure = "settlement " + settlement.slot + " Culture: "
                        + cultureFailure;
                    return false;
                }
            }
            string foundingFailure = CACultureModel.ValidationFailure(
                plan.playerFounding.culture, requireSubstantive: true);
            if (!foundingFailure.NullOrEmpty())
            {
                failure = "player founding Culture: " + foundingFailure;
                return false;
            }
            return true;
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
