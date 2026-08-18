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
        // B16 expands the Culture question registry without changing who owns
        // authored state. Epoch 13 remains the one supported adjacent source:
        // its Society profiles and regional plans already own the B15
        // Culture, Political Order, and Technological Knowledge composition.
        internal const int Current = 14;
        internal const int Previous = 13;
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
        // authored fixture is valuable operator work. The adjacent B15 draft
        // therefore receives one explicit, atomic conversion. Every Culture
        // owned by the draft is validated and projected before any assignment
        // occurs; a failure leaves the source graph and schema stamp intact.
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
                return ValidateCurrentCultureGraph(plan, out failure);
            }
            if (sourceEpoch != Previous || plan.schemaVersion != 14)
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

            var factions = new Dictionary<CARegionalFactionPlan, CACulture>();
            foreach (CARegionalFactionPlan faction in plan.factions)
            {
                if (faction == null)
                {
                    failure = "regional plan contains a null faction";
                    return false;
                }
                if (!CACultureModel.TryUpgradeToCurrent(faction.culture,
                        out CACulture culture, out string cultureFailure))
                {
                    failure = "faction " + faction.key + " Culture: "
                        + cultureFailure;
                    return false;
                }
                factions.Add(faction, culture);
            }

            var settlements =
                new Dictionary<CARegionalSettlementPlan, CACulture>();
            foreach (CARegionalSettlementPlan settlement in plan.settlements)
            {
                if (settlement == null)
                {
                    failure = "regional plan contains a null settlement";
                    return false;
                }
                if (settlement.localCulture == null) continue;
                if (!CACultureModel.TryUpgradeToCurrent(
                        settlement.localCulture, out CACulture culture,
                        out string cultureFailure))
                {
                    failure = "settlement " + settlement.slot + " Culture: "
                        + cultureFailure;
                    return false;
                }
                settlements.Add(settlement, culture);
            }

            if (!CACultureModel.TryUpgradeToCurrent(
                    plan.playerFounding.culture, out CACulture founding,
                    out string foundingFailure))
            {
                failure = "player founding Culture: " + foundingFailure;
                return false;
            }

            foreach (KeyValuePair<CARegionalFactionPlan, CACulture> pair in
                factions)
                pair.Key.culture = pair.Value;
            foreach (KeyValuePair<CARegionalSettlementPlan, CACulture> pair in
                settlements)
                pair.Key.localCulture = pair.Value;
            plan.playerFounding.culture = founding;
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
