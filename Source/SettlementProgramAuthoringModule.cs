using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // Formation and removal lifecycle for directly authored established
    // programs. The saved operational fact is authoritative; the registry
    // derives the material program from it.
    internal static class CASettlementProgramAuthoring
    {
        internal static bool Establish(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, string programKey,
            int populationGroupKey, out string failure)
        {
            failure = null;
            if (plan == null || settlement == null)
            {
                failure = "No settlement is selected.";
                return false;
            }
            if (CASettlementProgramRegistry.Find(programKey) == null)
            {
                failure = "That program is not registered.";
                return false;
            }
            CASettlementPopulationGroup group = (settlement.populationGroups
                    ?? new List<CASettlementPopulationGroup>())
                .FirstOrDefault(item => item != null
                    && item.key == populationGroupKey);
            if (group == null)
            {
                failure = "Choose an existing population group to operate the program.";
                return false;
            }
            string operatorIdentity = "population-group:" + group.key;
            if ((settlement.operationalFacts
                    ?? new List<CASettlementOperationalFact>())
                .Any(item => item != null && item.active
                    && item.programKey == programKey
                    && item.operatorIdentity == operatorIdentity))
            {
                failure = "That population already operates this program.";
                return false;
            }

            CAProvisionArrangement arrangement = ExactProvisionArrangement(
                settlement, programKey, operatorIdentity);
            bool provision = IsProvision(programKey);
            if (provision && arrangement == null)
            {
                failure = "Create the matching provision arrangement before establishing this program.";
                return false;
            }
            int sequence = NextSequence(settlement, programKey,
                operatorIdentity);
            CAEstablishedProgramFactSpec spec =
                CASettlementOperationalFactAuthoringKernel.Establish(
                    programKey, group.key, sequence,
                    provision ? arrangement.fundingSource : null,
                    provision ? arrangement.stockSource : null,
                    provision ? arrangement.materialSource : null,
                    provision ? arrangement.accessSource : null);
            var fact = FromSpec(spec);
            if (settlement.operationalFacts == null)
                settlement.operationalFacts =
                    new List<CASettlementOperationalFact>();
            settlement.operationalFacts.Add(fact);
            CASettlementProgramRegistry.EnsureDerived(plan, settlement,
                force: true);
            plan.operatorAuthored = true;
            plan.confirmed = false;
            CARegionalSetupSession.SavePending();
            return true;
        }

        internal static bool Remove(CARegionalPlan plan,
            CARegionalSettlementPlan settlement,
            CASettlementProgramEntry entry)
        {
            if (plan == null || settlement == null || entry == null)
                return false;
            int removed = (settlement.operationalFacts
                    ?? new List<CASettlementOperationalFact>())
                .RemoveAll(item => item != null
                    && item.programKey == entry.programKey
                    && item.operatorIdentity == entry.operatorIdentity);
            if (removed == 0) return false;
            CASettlementProgramRegistry.EnsureDerived(plan, settlement,
                force: true);
            plan.operatorAuthored = true;
            plan.confirmed = false;
            CARegionalSetupSession.SavePending();
            return true;
        }

        internal static CASettlementOperationalFact FromSpec(
            CAEstablishedProgramFactSpec spec)
        {
            return new CASettlementOperationalFact
            {
                factKey = spec.FactKey,
                programKey = spec.ProgramKey,
                needSource = spec.NeedSource,
                operatorIdentity = spec.OperatorIdentity,
                operatorSource = spec.OperatorSource,
                laborSource = spec.LaborSource,
                standingSource = spec.StandingSource,
                activitySource = spec.ActivitySource,
                targetPopulation = spec.TargetPopulation,
                knowledgeSource = spec.KnowledgeSource,
                fundingSource = spec.FundingSource,
                stockSource = spec.StockSource,
                policyKey = spec.PolicyKey,
                materialSource = spec.MaterialSource,
                accessSource = spec.AccessSource,
                maintenanceSource = spec.MaintenanceSource,
                active = true,
                provenance = spec.Provenance
            };
        }

        private static int NextSequence(CARegionalSettlementPlan settlement,
            string programKey, string operatorIdentity)
        {
            string prefix = "established:" + programKey + ":"
                + operatorIdentity + ":";
            return (settlement.operationalFacts
                    ?? new List<CASettlementOperationalFact>())
                .Where(item => item?.factKey?.StartsWith(prefix,
                    StringComparison.Ordinal) == true)
                .Select(item => int.TryParse(item.factKey.Substring(
                    prefix.Length), out int value) ? value : 0)
                .DefaultIfEmpty(0).Max() + 1;
        }

        private static bool IsProvision(string programKey)
        {
            return programKey == CASettlementProgramRegistry.DomesticProvision
                || programKey == CASettlementProgramRegistry.CommunalProvision
                || programKey == CASettlementProgramRegistry.AuthorityProvision;
        }

        private static CAProvisionArrangement ExactProvisionArrangement(
            CARegionalSettlementPlan settlement, string programKey,
            string operatorIdentity)
        {
            return (settlement?.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                .FirstOrDefault(item => item != null && item.active
                    && item.operatorIdentity == operatorIdentity
                    && CASettlementProgramRegistry.ProgramKeyFor(
                        item.operatorKind) == programKey);
        }
    }
}
