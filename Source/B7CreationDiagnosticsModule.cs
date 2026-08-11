using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Focused B7 evidence. These actions report the saved founding boundary
    // and persistent Culture history directly; they do not mutate either.
    internal static class CAB7CreationDiagnostics
    {
        internal static string FoundingState()
        {
            CAPlayerFoundingPlan founding = CAPlayerFoundingSession
                .CurrentOrNull();
            CARegionalPlan region = CARegionalSetupSession
                .CurrentPlanOrNull();
            var text = new StringBuilder("[CA][B7] founding state");
            if (founding == null)
                return text.Append("\n  unavailable").ToString();

            text.Append("\n  confirmed=").Append(founding.confirmed)
                .Append("; schema=").Append(founding.schemaVersion)
                .Append("; temporal=")
                .Append(founding.establishedStart
                    ? "established scenario" : "new landing")
                .Append("\n  temporal basis=")
                .Append(founding.temporalBasis ?? "unrecorded")
                .Append("\n  culture=")
                .Append(CultureLine(founding.culture))
                .Append("\n  Ideoligion=")
                .Append(founding.nativeIdeoName ?? "not recorded")
                .Append("; signature=")
                .Append(founding.nativeIdeoSignature ?? "not recorded")
                .Append("\n  political beliefs=")
                .Append(Axes(founding.politicalBeliefs?.positions))
                .Append("\n  adopted terms=")
                .Append(founding.arrangement?.label ?? "not chosen")
                .Append("; source=")
                .Append(founding.ArrangementSource)
                .Append("\n  belief-practice tensions=");
            List<CAPoliticalBeliefPractice.CAFoundingBeliefReading> tensions =
                CAPoliticalBeliefPractice.ReadAgainstPoliticalBeliefs(
                    founding.politicalBeliefs, founding.arrangement)
                .Where(item => item != null && !item.Silent
                    && !item.conforms).ToList();
            text.Append(tensions.Count == 0 ? "none" : string.Join(" | ",
                tensions.Select(item => item.title + ": " + item.belief
                    + " -> " + item.adopted)));

            if (region != null)
            {
                text.Append("\n  region=").Append(region.regionalId)
                    .Append("; candidate=").Append(region.candidateId)
                    .Append("; factions=").Append(region.factions?.Count ?? 0)
                    .Append("; settlements=")
                    .Append(region.settlements?.Count ?? 0);
                foreach (CARegionalSettlementPlan settlement in
                    region.settlements ?? new List<CARegionalSettlementPlan>())
                    if (settlement != null)
                        text.Append("\n    settlement ")
                            .Append(settlement.slot).Append(": ")
                            .Append(CultureLine(settlement.localCulture));
            }
            return text.ToString();
        }

        internal static string CultureHistory(Map map)
        {
            var text = new StringBuilder("[CA][B7] Culture history");
            if (map == null)
                return text.Append("\n  no current map").ToString();

            CACulture player = CACultureLongitudinalMapComponent.For(map)
                ?.PlayerLocalCulture;
            text.Append("\n  player settlement: ")
                .Append(CultureLine(player));
            AppendHistory(text, player, "    ");

            foreach (CARegionalSettlementRecord settlement in
                CARegionalWorldComponent.Current?.ForMap(map)
                    ?? Enumerable.Empty<CARegionalSettlementRecord>())
            {
                if (settlement == null) continue;
                text.Append("\n  settlement ")
                    .Append(settlement.name ?? settlement.regionalId)
                    .Append(": ").Append(CultureLine(settlement.culture));
                AppendHistory(text, settlement.culture, "    ");
            }
            return text.ToString();
        }

        private static void AppendHistory(StringBuilder text,
            CACulture culture, string indent)
        {
            if (culture == null) return;
            text.Append("\n").Append(indent).Append("state signature=")
                .Append(CACultureHistory.StateSignature(culture))
                .Append("; evidence=")
                .Append(culture.lastEvidence?.signature ?? "not observed")
                .Append("; migration=")
                .Append(culture.migrationEvidence ?? "none");
            CACultureTransition transition = culture.transitions?
                .Where(item => item != null)
                .OrderByDescending(item => item.sequence).FirstOrDefault();
            text.Append("\n").Append(indent).Append("latest transition=")
                .Append(transition == null ? "none" : transition.sequence
                    + " " + transition.cause + "; domains="
                    + (transition.changedDomains ?? "not recorded")
                    + "; predecessor="
                    + (transition.predecessorCultureSignature
                        ?? "not recorded") + "; evidence="
                    + (transition.evidenceSignature ?? "not recorded"));
            text.Append("\n").Append(indent).Append("practices=")
                .Append(culture.practices == null
                    || culture.practices.Count == 0 ? "none"
                    : string.Join(" | ", culture.practices
                        .Where(item => item != null)
                        .OrderByDescending(item => item.strength)
                        .ThenBy(item => item.key)
                        .Select(item => item.key + "=" + item.strength
                            + " from "
                            + (item.sourcePeriod ?? "unrecorded period"))));
        }

        private static string CultureLine(CACulture culture)
        {
            if (culture == null) return "not recorded";
            return (culture.name ?? "unnamed") + " [" + (culture.id
                    ?? "no identity") + "]; parent="
                + (culture.parentId ?? "none") + "; locality="
                + (culture.localityKey ?? "inherited") + "; maturity="
                + culture.maturity + "; revision=" + culture.revision
                + "; constituents=" + (culture.constituents?.Count ?? 0);
        }

        private static string Axes(IEnumerable<CAAxisEntry> axes)
        {
            string[] values = (axes ?? Enumerable.Empty<CAAxisEntry>())
                .Where(item => item != null).OrderBy(item => item.axisKey)
                .Select(item => item.axisKey + "=" + item.optionKey)
                .ToArray();
            return values.Length == 0 ? "not recorded"
                : string.Join(", ", values);
        }
    }

    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness", "B7: founding-state census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Entry)]
        private static void B7FoundingStateCensus()
        {
            Log.Message(CAB7CreationDiagnostics.FoundingState());
        }

        [DebugAction("Colonist Awareness", "B7: Culture-history census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void B7CultureHistoryCensus()
        {
            Log.Message(CAB7CreationDiagnostics.CultureHistory(
                Find.CurrentMap));
        }
    }
}
