using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    public partial class KnowledgeMapComponent
    {
        // Developer observation must not refresh, reconcile, or expire a fact.
        // These counts therefore read the actor-private stores exactly as they
        // stand at the requested tick.
        internal void BehaviorCensusCounts(Pawn pawn, out int threats,
            out int welfare)
        {
            threats = 0;
            welfare = 0;
            if (pawn == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            List<Contact> contacts;
            if (known.TryGetValue(pawn.thingIDNumber, out contacts))
            {
                for (int i = 0; i < contacts.Count; i++)
                {
                    Contact contact = contacts[i];
                    if (contact == null
                        || contact.state != ThreatContactState.Active) continue;
                    bool weak = contact.weaponCategory
                            == ContactWeaponCategory.Unknown
                        && contact.directlyObservedPawn == null;
                    int window = weak ? StaleTicks / 2 : StaleTicks;
                    if (now - contact.tick <= window) threats++;
                }
            }
            List<WelfareFact> facts;
            if (knownWelfare.TryGetValue(pawn.thingIDNumber, out facts))
                for (int i = 0; i < facts.Count; i++)
                    if (facts[i] != null
                        && now - facts[i].lastRefreshTick
                            <= WelfareStaleTicks) welfare++;
        }
    }

    internal static class CABehaviorCensus
    {
        internal static string PawnReport(Pawn pawn)
        {
            if (pawn == null) return "[CA] behavior census: no pawn";
            CAEffectiveBehaviorProfile profile =
                CAEffectiveBehaviorProfileCache.Of(pawn);
            AwarenessSettings settings = AwarenessMod.Settings;
            int threats = 0, welfare = 0;
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(
                pawn.Map);
            knowledge?.BehaviorCensusCounts(pawn, out threats, out welfare);

            CAIntentContext intent;
            bool hasIntent = CATactical.TryGetContext(pawn, out intent)
                && intent.IsValid;
            if (!hasIntent)
                hasIntent = CABehaviorIntentMapComponent.For(pawn.Map)?.TryGet(
                    pawn, pawn.CurJob, out intent) == true && intent.IsValid;

            CABehaviorDefinition candidate = hasIntent
                ? CABehaviorCatalog.Get(intent.BehaviorKey) : null;
            CABehaviorDecisionObservation observedDecision =
                default(CABehaviorDecisionObservation);
            bool hasObservedDecision = !hasIntent
                && CABehaviorIntentMapComponent.For(pawn.Map)
                    ?.TryGetRecentDecision(pawn, out observedDecision) == true;
            if (hasObservedDecision)
                candidate = CABehaviorCatalog.Get(
                    observedDecision.BehaviorKey);

            string[] permissions = CABehaviorSettings.All
                .Where(item => item.Kind == CASettingKind.FeaturePermission
                    && CABehaviorSettings.IsEnabled(item.Key, settings)
                    && CABehaviorCatalog.ForSetting(item.Key).Any(definition =>
                        profile != null
                        && definition.AppliesTo(profile.ActorContext)))
                .Select(item => item.Label).OrderBy(item => item,
                    StringComparer.Ordinal).ToArray();
            string[] stable = profile?.Definitions().Where(item =>
                    item.Form != CABehaviorForm.Presentation
                    && item.Form != CABehaviorForm.Diagnostic)
                .Select(item => item.Key).OrderBy(item => item,
                    StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
            DispositionProfile disposition = Disposition.Of(pawn);
            string authority = hasIntent
                ? (intent.AuthorityIdentity ?? intent.AuthorityOrigin.ToString())
                : hasObservedDecision
                    ? (observedDecision.AuthorityBasis
                        ?? observedDecision.SuggestedIntentOrigin.ToString())
                    : "none; no owned intent or recent production candidate";
            string current = hasIntent
                ? intent.BehaviorKey + " episode " + intent.EpisodeId
                    + "; target " + (intent.TargetOrDemand ?? "unspecified")
                    + "; ends when "
                    + (intent.TerminationCondition ?? "owner releases it")
                : "none";
            string candidateText = hasIntent
                ? (candidate?.Key ?? intent.BehaviorKey ?? "unregistered")
                    + " active"
                : hasObservedDecision
                    ? (candidate?.Key ?? observedDecision.BehaviorKey
                        ?? "unregistered") + (observedDecision.Allowed
                            ? " authorized" : " blocked")
                        + (observedDecision.CulturalAppraisalApplied
                            ? "; response "
                                + observedDecision.CulturalResponse.ToString()
                                    .ToLowerInvariant()
                                + " ("
                                + observedDecision.CulturalSupport
                                    .ToString("0.00") + ")"
                            : "")
                    : "none observed";
            string primaryReason = hasIntent
                ? "registered owned intent is active"
                : hasObservedDecision
                    ? observedDecision.PrimaryReason
                    : "no production behavior candidate was recently evaluated";
            string cadence = candidate?.Cadence ?? "native work cadence";
            string ceilings = CASpatialInitiativeMapComponent.For(pawn.Map)
                ?.CeilingsForObservation(pawn) ?? "none";
            string initiative = pawn.Faction == Faction.OfPlayer
                ? CAInitiativePresentation.Label(
                    AutonomyComponent.TierOf(pawn))
                : "not applicable (NPC authority)";

            var report = new StringBuilder();
            report.Append("[CA] behavior census - ").Append(pawn.LabelShort)
                .Append(" #").Append(pawn.thingIDNumber).AppendLine()
                .Append("  initiative: ").Append(initiative).AppendLine()
                .Append("  active feature permissions: ").Append(
                    permissions.Length == 0 ? "none" : string.Join(", ",
                        permissions)).AppendLine()
                .Append("  stable effective profile: ").Append(
                    stable.Length == 0 ? "none" : string.Join(", ", stable))
                    .AppendLine()
                .Append("  current intent: ").Append(current).AppendLine()
                .Append("  intent behavior key: ").Append(hasIntent
                    ? intent.BehaviorKey : "none").AppendLine()
                .Append("  authority: ").Append(authority).AppendLine()
                .Append("  known threats / welfare: ").Append(threats)
                    .Append(" / ").Append(welfare).AppendLine()
                .Append("  squad position: ").Append(profile?.Role ?? "none")
                    .AppendLine()
                .Append("  spatial ceilings: ").Append(ceilings).AppendLine()
                .Append("  observed active or blocked candidate: ")
                    .Append(candidateText).AppendLine()
                .Append("  primary reason: ").Append(primaryReason)
                    .AppendLine()
                .Append("  disposition: courage ")
                    .Append(disposition.courage.ToString("F2"))
                    .Append(", discipline ")
                    .Append(disposition.discipline.ToString("F2"))
                    .Append(", aggression ")
                    .Append(disposition.aggression.ToString("F2"))
                    .Append(", empathy ")
                    .Append(disposition.empathy.ToString("F2"))
                    .Append(", conformity ")
                    .Append(disposition.conformity.ToString("F2"))
                    .Append(", initiative ")
                    .Append(disposition.initiative.ToString("F2"))
                    .Append(", skepticism ")
                    .Append(disposition.skepticism.ToString("F2"))
                    .AppendLine()
                .Append("  cadence owner: ").Append(cadence);
            return report.ToString();
        }

        internal static string SettlementReport(Map map)
        {
            if (map == null)
                return "[CA] settlement behavior census: no current map";
            PlannedUseMapComponent planned = PlannedUseMapComponent.For(map);
            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> programs =
                planned?.ProgramsForObservation
                ?? Array.Empty<CASpaceProgram>();
            CAOrganization organization = CAOrganizationWorldComponent.Current
                ?.ByKey("player");
            CAFactionState state = CAFactionStateWorldComponent.Current
                ?.Find(Faction.OfPlayer);
            AutonomousHomeMapComponent home = AutonomousHomeMapComponent.For(
                map);
            CAHomePrerequisiteMapComponent materials =
                CAHomePrerequisiteMapComponent.For(map);

            string[] programRows = programs.Where(item => item != null)
                .OrderBy(item => item.id).Select(item => "#" + item.id + " "
                    + (item.label ?? item.purpose.ToString()) + " ["
                    + item.purpose + "; owner " + item.author + "; ceiling "
                    + CAInitiativePresentation.Label(spatial?.TierFor(item)
                        ?? CAInitiativeTier.Standard) + "; beneficiaries "
                    + (item.residents == null || item.residents.Count == 0
                        ? "none assigned" : string.Join(", ", item.residents
                            .Where(pawn => pawn != null)
                            .OrderBy(pawn => pawn.thingIDNumber)
                            .Select(pawn => pawn.LabelShort).ToArray())) + "]")
                .ToArray();
            string authority = organization == null ? "none"
                : (organization.name ?? organization.organizationKey) + "; "
                    + organization.offices.Count + " offices ["
                    + string.Join(", ", organization.offices
                        .Where(item => item != null)
                        .OrderBy(item => item.sourceKey,
                            StringComparer.Ordinal)
                        .Select(item => (item.name ?? item.sourceKey) + ": "
                            + (item.holderLabel ?? "vacant") + " - "
                            + (item.grants ?? "no recorded grant")).ToArray())
                    + "]";
            string proposals = "home " + (home == null
                ? "unavailable" : home.HasPendingPlanForVerification
                    ? home.PendingPlanForVerification : "none")
                + "; spatial " + (spatial?.PendingPlanForObservation
                    ?? "unavailable");
            string culture = state?.culture == null ? "not materialized"
                : CACultureModel.Summary(state.culture);
            string politics = state?.politicalBeliefs == null
                ? "not materialized" : CAPoliticalBeliefsModel.Summary(
                    state.politicalBeliefs);
            string demand = materials?.CensusForObservation()
                ?? "no material-demand component";

            return new StringBuilder()
                .Append("[CA] settlement behavior census - map #")
                .Append(map.uniqueID).AppendLine()
                .Append("  spatial programs: ").Append(programRows.Length == 0
                    ? "none" : string.Join("; ", programRows)).AppendLine()
                .Append("  institutional authority: ").Append(authority)
                    .AppendLine()
                .Append("  active demands: ").Append(demand).AppendLine()
                .Append("  proposals: ").Append(proposals).AppendLine()
                .Append("  owner/operator: player-authored programs delegate "
                    + "only to their recorded initiative ceilings; native jobs "
                    + "remain executors").AppendLine()
                .Append("  beneficiaries: ").Append(programRows.Length == 0
                    ? "none assigned" : "program resident rosters and current "
                        + "settlement population").AppendLine()
                .Append("  cultural basis: ").Append(culture).AppendLine()
                .Append("  political basis: ").Append(politics).AppendLine()
                .Append("  material blockers: ").Append(demand)
                .AppendLine()
                .Append("  institutional state: policies ")
                    .Append(organization?.policies?.Count ?? 0)
                    .Append(", open belief/practice tensions ")
                    .Append(organization?.openBeliefConflicts?.Count ?? 0)
                .ToString();
        }
    }

    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness", "Behavior census (selected)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BehaviorCensusSelected()
        {
            int count = 0;
            foreach (object selected in Find.Selector.SelectedObjects)
            {
                Pawn pawn = selected as Pawn;
                if (pawn == null || pawn.Map == null) continue;
                count++;
                Log.Message(CABehaviorCensus.PawnReport(pawn));
            }
            if (count == 0)
                Log.Message("[CA] behavior census: select at least one pawn");
        }

        [DebugAction("Colonist Awareness", "Settlement behavior census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SettlementBehaviorCensus()
        {
            Log.Message(CABehaviorCensus.SettlementReport(Find.CurrentMap));
        }

        [DebugAction("Colonist Awareness", "Behavior runtime matrix",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BehaviorRuntimeMatrix()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Message("[CA] behavior runtime matrix: no current map");
                return;
            }

            CABehaviorIntentMapComponent intents =
                CABehaviorIntentMapComponent.For(map);
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn != null)
                .OrderBy(pawn => pawn.thingIDNumber).ToList();
            int standard = 0, proactive = 0, autonomous = 0;
            var rows = new List<string>(colonists.Count);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                switch (AutonomyComponent.TierOf(pawn))
                {
                    case CAInitiativeTier.Proactive: proactive++; break;
                    case CAInitiativeTier.Autonomous: autonomous++; break;
                    default: standard++; break;
                }
                rows.Add(pawn.LabelShort + " #" + pawn.thingIDNumber + ": "
                    + CAInitiativePresentation.Label(
                        AutonomyComponent.TierOf(pawn)) + "; "
                    + (intents?.Census(pawn)
                        ?? "behavior-intent component unavailable"));
            }

            Log.Message(new StringBuilder()
                .Append("[CA] behavior runtime matrix - map #")
                .Append(map.uniqueID).Append(" tick ")
                .Append(Find.TickManager?.TicksGame ?? 0).AppendLine()
                .Append("  components: knowledge ")
                .Append(map.GetComponent<KnowledgeMapComponent>() != null
                    ? "ready" : "missing")
                .Append(", behavior intent ").Append(intents != null
                    ? "ready" : "missing")
                .Append(", spatial ").Append(
                    CASpatialInitiativeMapComponent.For(map) != null
                        ? "ready" : "missing")
                .Append(", settlement program ").Append(
                    PlannedUseMapComponent.For(map) != null
                        ? "ready" : "missing").AppendLine()
                .Append("  player initiative: Standard ").Append(standard)
                .Append(", Proactive ").Append(proactive)
                .Append(", Autonomous ").Append(autonomous).AppendLine()
                .Append("  pawns: ").Append(rows.Count == 0 ? "none"
                    : string.Join(" | ", rows)).AppendLine()
                .Append("  operator cases: verify native work and direct orders; "
                    + "observe one bounded Proactive response and one persistent "
                    + "Autonomous objective; save/reload and repeat this readout; "
                    + "inspect Player.log for repeated exceptions")
                .ToString());
        }
    }
}
