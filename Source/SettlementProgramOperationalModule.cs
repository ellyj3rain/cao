using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    // Converts directly authored or historically observed operating contracts
    // into the pure program kernel shape. Registry metadata and asset
    // materialization do not own operational truth.
    internal static class CASettlementProgramOperationalResolver
    {
        internal static List<CASettlementProgramOperationalEvidence> Build(
            CARegionalPlan plan, CARegionalSettlementPlan settlement,
            Func<string, CASettlementProgramDef> definitionFor)
        {
            var result = new List<CASettlementProgramOperationalEvidence>();
            foreach (CASettlementOperationalFact fact in
                settlement?.operationalFacts
                    ?? new List<CASettlementOperationalFact>())
            {
                CASettlementProgramDef definition = fact == null
                    ? null : definitionFor?.Invoke(fact.programKey);
                if (fact == null || !fact.active || definition == null)
                    continue;
                var evidence = new CASettlementProgramOperationalEvidence
                {
                    Key = fact.programKey,
                    NeedSource = fact.needSource,
                    OperatorIdentity = fact.operatorIdentity,
                    OperatorSource = fact.operatorSource,
                    LaborSource = fact.laborSource,
                    StandingSource = fact.standingSource,
                    ActivitySource = fact.activitySource,
                    TargetPopulation = fact.targetPopulation,
                    KnowledgeSource = fact.knowledgeSource,
                    FundingSource = fact.fundingSource,
                    StockSource = fact.stockSource,
                    PolicyKey = fact.policyKey,
                    MaterialSource = fact.materialSource,
                    AccessSource = fact.accessSource,
                    MaintenanceSource = fact.maintenanceSource,
                    CulturalSubjects = (fact.culturalSubjects
                        ?? new List<string>()).Where(subject =>
                            CASocialSubjectRegistry.Find(subject) != null)
                        .Distinct().ToList(),
                    CandidateGroups = (definition.CandidateGroups
                            ?.Invoke(plan, settlement)
                            ?? Enumerable.Empty<string[]>())
                        .Select(group => (group ?? Array.Empty<string>())
                            .ToArray()).ToList(),
                    Count = IsProvisionProgram(fact.programKey)
                        ? ProvisionNodeCount(settlement, fact)
                        : Math.Max(1,
                            definition.Count?.Invoke(plan, settlement) ?? 1),
                    Extent = Math.Max(1,
                        definition.Extent?.Invoke(plan, settlement) ?? 1),
                    NativeSpatialContract = definition.NativeSpatialContract,
                    MaterializeSpatialContract =
                        definition.MaterializeSpatialContract,
                    RequiresOperator = true,
                    RequiresFunding = RequiresFunding(fact.programKey),
                    RequiresStock = RequiresStock(fact.programKey),
                    RequiresMaterial = true
                };
                if (!DirectlyGrounded(fact))
                    evidence.Blocker = "the operational contract does not "
                        + "identify authored or observed evidence";
                else if (IsProvisionProgram(fact.programKey)
                    && !MatchesProvision(settlement, fact))
                    evidence.Blocker = "the provision program has no exact "
                        + "arrangement with the same operator identity";
                result.RemoveAll(existing => existing.Key == evidence.Key
                    && existing.OperatorIdentity
                        == evidence.OperatorIdentity);
                result.Add(evidence);
            }
            return result.OrderBy(item => item.Key,
                    StringComparer.Ordinal)
                .ThenBy(item => item.OperatorIdentity,
                    StringComparer.Ordinal).ToList();
        }

        internal static bool DirectlyGrounded(CASettlementOperationalFact fact)
        {
            string provenance = fact.provenance ?? "";
            return !fact.factKey.NullOrEmpty() && Direct(provenance)
                && Direct(fact.needSource) && Direct(fact.operatorSource)
                && Direct(fact.laborSource) && Direct(fact.standingSource)
                && Direct(fact.activitySource) && Direct(fact.knowledgeSource)
                && Direct(fact.accessSource)
                && Direct(fact.maintenanceSource)
                && (!RequiresFunding(fact.programKey)
                    || Direct(fact.fundingSource))
                && (!RequiresStock(fact.programKey)
                    || Direct(fact.stockSource))
                && Direct(fact.materialSource);
        }

        private static bool Direct(string source)
        {
            return source?.StartsWith("authored:",
                       StringComparison.Ordinal) == true
                || source?.StartsWith("observed:",
                       StringComparison.Ordinal) == true;
        }

        internal static bool IsProvisionProgram(string key)
        {
            return key == CASettlementProgramRegistry.CommunalProvision
                || key == CASettlementProgramRegistry.AuthorityProvision
                || key == CASettlementProgramRegistry.DomesticProvision;
        }

        private static bool MatchesProvision(
            CARegionalSettlementPlan settlement,
            CASettlementOperationalFact fact)
        {
            CAProvisionOperator expected = fact.programKey
                    == CASettlementProgramRegistry.CommunalProvision
                ? CAProvisionOperator.Communal
                : fact.programKey
                    == CASettlementProgramRegistry.AuthorityProvision
                    ? CAProvisionOperator.Authority
                    : CAProvisionOperator.DomesticUnit;
            return settlement?.provisionArrangements?.Any(item => item != null
                && item.active && item.operatorIdentity
                    == fact.operatorIdentity
                && (item.operatorKind == expected
                    || expected == CAProvisionOperator.DomesticUnit
                        && item.operatorKind
                            == CAProvisionOperator.Individual)) == true;
        }

        private static int ProvisionNodeCount(
            CARegionalSettlementPlan settlement,
            CASettlementOperationalFact fact)
        {
            CAProvisionOperator expected = fact.programKey
                    == CASettlementProgramRegistry.CommunalProvision
                ? CAProvisionOperator.Communal
                : fact.programKey
                    == CASettlementProgramRegistry.AuthorityProvision
                    ? CAProvisionOperator.Authority
                    : CAProvisionOperator.DomesticUnit;
            return Math.Max(1, (settlement?.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                .Where(item => item != null && item.active
                    && item.operatorIdentity == fact.operatorIdentity
                    && (item.operatorKind == expected
                        || expected == CAProvisionOperator.DomesticUnit
                            && item.operatorKind
                                == CAProvisionOperator.Individual))
                .Sum(item => Math.Max(1, item.nodes)));
        }

        private static bool RequiresFunding(string key)
        {
            return key == CASettlementProgramRegistry.Trade
                || key == CASettlementProgramRegistry.Research
                || key == CASettlementProgramRegistry.CommunalProvision
                || key == CASettlementProgramRegistry.AuthorityProvision
                || key == CASettlementProgramRegistry.DomesticProvision;
        }

        private static bool RequiresStock(string key)
        {
            return key == CASettlementProgramRegistry.Storage
                || key == CASettlementProgramRegistry.Trade
                || key == CASettlementProgramRegistry.CommunalProvision
                || key == CASettlementProgramRegistry.AuthorityProvision
                || key == CASettlementProgramRegistry.DomesticProvision;
        }
    }
}
