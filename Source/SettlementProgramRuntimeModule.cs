using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    internal sealed class CASettlementProgramRuntimeResolution
    {
        internal CASettlementOperationalFact Fact;
        internal CAOrganization Organization;
        internal List<Pawn> Workers = new List<Pawn>();
        internal string Failure;
        internal bool Resolved => Failure.NullOrEmpty();
    }

    // Periodic live contract for one exact persisted program entry. The
    // source fact remains historical truth; current operator, labor, and
    // material state decide whether the program can act now.
    internal static class CASettlementProgramRuntimeContract
    {
        internal static bool TryResolve(CARegionalSettlementRecord record,
            Map map, CASettlementProgramEntry entry,
            bool requireMaterializedAssets,
            out CASettlementProgramRuntimeResolution resolution)
        {
            resolution = new CASettlementProgramRuntimeResolution();
            if (record == null || map == null || entry == null
                || entry.operatorIdentity.NullOrEmpty()
                || entry.signature.NullOrEmpty())
                return Fail(resolution,
                    "program lacks an exact saved identity");

            CASettlementOperationalFact fact = record.operationalFacts?
                .FirstOrDefault(item => item != null && item.active
                    && item.programKey == entry.programKey
                    && item.operatorIdentity == entry.operatorIdentity);
            resolution.Fact = fact;
            if (fact == null)
                return Fail(resolution,
                    "program has no matching operational fact");
            if (!CASettlementProgramOperationalResolver
                    .DirectlyGrounded(fact))
                return Fail(resolution,
                    "operational fact lacks direct authored or observed evidence");
            if (!Matches(entry, fact))
                return Fail(resolution,
                    "saved program no longer matches its operational fact");

            if (CASettlementProgramOperationalResolver.IsProvisionProgram(
                    entry.programKey))
            {
                CAProvisionArrangement arrangement = (record
                        .provisionArrangements
                        ?? new List<CAProvisionArrangement>())
                    .FirstOrDefault(item => item != null && item.active
                        && item.operatorIdentity == entry.operatorIdentity
                        && CASettlementProgramRegistry.ProgramKeyFor(
                            item.operatorKind) == entry.programKey);
                if (arrangement == null)
                    return Fail(resolution,
                        "no exact provision arrangement exists for this program kind and operator");
                CAResolvedProvisionOperator resolved =
                    CAProvisionOperatorResolver.Resolve(record, map,
                        arrangement);
                if (!resolved.Resolved)
                    return Fail(resolution, resolved.Failure);
                resolution.Organization = resolved.Organization;
                resolution.Workers = LiveEligible(record, map,
                    resolved.Consumers ?? new List<Pawn>());
            }
            else if (!ResolveOperator(record, map, entry,
                    resolution))
                return false;

            List<Pawn> labor = ResolveLabor(record, map, fact,
                resolution.Organization);
            if (labor.Count == 0)
                return Fail(resolution,
                    "the recorded labor source has no current eligible workers");
            resolution.Workers = labor;

            if (requireMaterializedAssets && entry.requiresMaterial)
            {
                CASettlementProgramDef definition =
                    CASettlementProgramRegistry.Find(entry.programKey);
                bool spatial = definition?.NativeSpatialContract == true
                    && !definition.MaterializeSpatialContract
                    && entry.materializationState
                        == "present in saved geography";
                if (!spatial && !CASettlementProgramAssets.Complete(record,
                        map, entry))
                    return Fail(resolution,
                        "required program assets are missing or no longer live");
            }
            return true;
        }

        internal static bool TryResolveOperator(
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramEntry entry, out string failure)
        {
            bool resolved = TryResolve(record, map, entry,
                requireMaterializedAssets: false,
                out CASettlementProgramRuntimeResolution result);
            failure = result.Failure;
            return resolved;
        }

        internal static bool WorkerAuthorized(
            CASettlementProgramRuntimeResolution resolution, Pawn worker)
        {
            return worker != null && resolution?.Workers?.Any(item =>
                item == worker) == true;
        }

        internal static void Reconcile(CARegionalSettlementRecord record,
            Map map)
        {
            if (record?.settlementProgram?.entries == null || map == null)
                return;
            int tick = Find.TickManager?.TicksGame ?? -1;
            foreach (CASettlementProgramEntry entry in
                record.settlementProgram.entries.Where(item => item != null))
            {
                bool requireAssets = entry.materializationState
                    == "materialized" || entry.materializationState
                    == "present in saved geography";
                bool resolved = TryResolve(record, map, entry,
                    requireAssets, out CASettlementProgramRuntimeResolution
                        result);
                entry.runtimeState = resolved ? "operating" : "suspended";
                entry.runtimeFailure = result.Failure;
                entry.lastRuntimeValidationTick = tick;
            }
        }

        private static bool ResolveOperator(
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramEntry entry,
            CASettlementProgramRuntimeResolution resolution)
        {
            CAOrganization organization =
                CAOrganizationWorldComponent.Current
                    ?.ByKey(entry.operatorIdentity);
            if (organization != null)
            {
                resolution.Organization = organization;
                return true;
            }
            if (TryTypedId(entry.operatorIdentity, "pawn:", out int pawnId))
            {
                Pawn pawn = CAPopulationProjection.Residents(record, map)
                    .FirstOrDefault(item => item.thingIDNumber == pawnId);
                if (pawn == null)
                    return Fail(resolution,
                        "pawn operator is not a current resident");
                resolution.Workers.Add(pawn);
                return true;
            }
            if (TryTypedId(entry.operatorIdentity, "population-group:",
                    out int groupKey))
            {
                List<Pawn> participants = LiveEligible(record, map,
                    CAPopulationProjection.ResidentsInPopulationGroup(record,
                        map, groupKey));
                if (participants.Count == 0)
                    return Fail(resolution,
                        "population operator has no current participants");
                resolution.Workers = participants;
                return true;
            }
            return Fail(resolution,
                "operator identity does not resolve to an organization, resident, or population group");
        }

        private static List<Pawn> ResolveLabor(
            CARegionalSettlementRecord record, Map map,
            CASettlementOperationalFact fact, CAOrganization organization)
        {
            string source = fact?.laborSource ?? "";
            if (TryEmbeddedId(source, "population-group:", out int groupKey))
                return LiveEligible(record, map,
                    CAPopulationProjection.ResidentsInPopulationGroup(record,
                        map, groupKey));
            if (TryEmbeddedId(source, "pawn:", out int pawnId))
                return LiveEligible(record, map,
                    CAPopulationProjection.Residents(record, map).Where(
                        pawn => pawn.thingIDNumber == pawnId));
            if (source.Contains("organization:") && organization != null)
            {
                var memberIds = new HashSet<int>(
                    organization.memberPawnIds ?? new List<int>());
                return LiveEligible(record, map,
                    CAPopulationProjection.Residents(record, map).Where(
                        pawn => memberIds.Contains(pawn.thingIDNumber)));
            }
            return new List<Pawn>();
        }

        private static List<Pawn> LiveEligible(
            CARegionalSettlementRecord record, Map map,
            IEnumerable<Pawn> candidates)
        {
            var residents = new HashSet<int>(CAPopulationProjection
                .Residents(record, map).Where(pawn => pawn != null)
                .Select(pawn => pawn.thingIDNumber));
            return (candidates ?? Enumerable.Empty<Pawn>())
                .Where(pawn => pawn != null && pawn.Spawned
                    && pawn.Map == map && !pawn.Dead && !pawn.Downed
                    && !pawn.IsPrisoner && pawn.RaceProps.Humanlike
                    && residents.Contains(pawn.thingIDNumber))
                .Distinct().ToList();
        }

        private static bool Matches(CASettlementProgramEntry entry,
            CASettlementOperationalFact fact)
        {
            return entry.needSource == fact.needSource
                && entry.operatorIdentity == fact.operatorIdentity
                && entry.operatorSource == fact.operatorSource
                && entry.laborSource == fact.laborSource
                && entry.standingSource == fact.standingSource
                && entry.activitySource == fact.activitySource
                && entry.targetPopulation == fact.targetPopulation
                && entry.knowledgeSource == fact.knowledgeSource
                && entry.fundingSource == fact.fundingSource
                && entry.stockSource == fact.stockSource
                && entry.policyKey == fact.policyKey
                && entry.materialSource == fact.materialSource
                && entry.accessSource == fact.accessSource
                && entry.maintenanceSource == fact.maintenanceSource;
        }

        private static bool TryTypedId(string value, string prefix,
            out int result)
        {
            result = -1;
            return value?.StartsWith(prefix, StringComparison.Ordinal) == true
                && int.TryParse(value.Substring(prefix.Length), out result)
                && result >= 0;
        }

        private static bool TryEmbeddedId(string value, string marker,
            out int result)
        {
            result = -1;
            int start = value?.IndexOf(marker,
                StringComparison.Ordinal) ?? -1;
            return start >= 0 && int.TryParse(value.Substring(
                start + marker.Length), out result) && result >= 0;
        }

        private static bool Fail(
            CASettlementProgramRuntimeResolution resolution,
            string failure)
        {
            resolution.Failure = failure
                ?? "the runtime program contract is incomplete";
            return false;
        }
    }
}
