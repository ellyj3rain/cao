using System;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    // Validates an existing resource-flow contract. Saved labels never create
    // policies, taxpayers, work relations, or collection paths.
    internal static class CAProvisionFundingResolver
    {
        internal static bool IsFunded(CARegionalSettlementRecord record,
            CAProvisionArrangement arrangement,
            CAResolvedProvisionOperator resolved, out string failure)
        {
            failure = null;
            if (record == null || arrangement == null
                || resolved?.Resolved != true)
            {
                failure = resolved?.Failure ?? "operator is unresolved";
                return false;
            }
            if (!DirectSource(arrangement.fundingSource))
            {
                failure = "funding lacks authored or observed evidence";
                return false;
            }
            if (arrangement.funding == CAProvisionFunding.Domestic)
            {
                bool domestic = arrangement.operatorKind
                        == CAProvisionOperator.DomesticUnit
                    || arrangement.operatorKind
                        == CAProvisionOperator.Individual;
                if (!domestic || resolved.Consumers.Count == 0)
                    failure = "domestic funding lacks a resident operator";
                return failure == null;
            }

            CAOrganization provider = resolved.Organization;
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (provider == null || ledger == null)
            {
                failure = "organizational funding ledger is unavailable";
                return false;
            }
            if (arrangement.funding == CAProvisionFunding.SharedWork)
            {
                bool work = ledger.RelationsIn(provider.organizationKey)
                    .Any(relation => relation != null
                        && !relation.Expired(now)
                        && relation.Delegates(CAResponsibilities.Work));
                if (!work) failure = "no active work contribution exists";
                return work;
            }

            bool policy = !arrangement.policyKey.NullOrEmpty()
                && !arrangement.policyValue.NullOrEmpty()
                && provider.policies.Any(item => item != null
                    && item.key == arrangement.policyKey
                    && item.value == arrangement.policyValue);
            bool collection = !arrangement.collectionPath.NullOrEmpty()
                && ledger.RelationsIn(provider.organizationKey).Any(
                    relation => relation != null
                        && !relation.Expired(now)
                        && relation.role == arrangement.collectionPath
                        && relation.Delegates(CAResponsibilities.Taxes));
            if (!policy || !collection)
                failure = !policy ? "tax policy is not instituted"
                    : "tax collection pathway is not operating";
            return policy && collection;
        }

        private static bool DirectSource(string source)
        {
            return source?.StartsWith("authored:",
                       StringComparison.Ordinal) == true
                || source?.StartsWith("observed:",
                       StringComparison.Ordinal) == true;
        }
    }
}
