using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    internal sealed class CAResolvedProvisionOperator
    {
        internal string Identity;
        internal CAProvisionOperator Kind;
        internal CAOrganization Organization;
        internal CADomesticUnit DomesticUnit;
        internal Pawn Individual;
        internal List<Pawn> Consumers = new List<Pawn>();
        internal string Failure;
        internal bool Resolved => Failure.NullOrEmpty();
    }

    // Resolves the exact persisted operator to an existing social entity. It
    // never creates an organization or redirects a missing identity to the
    // settlement.
    internal static class CAProvisionOperatorResolver
    {
        internal static CAResolvedProvisionOperator Resolve(
            CARegionalSettlementRecord record, Map map,
            CAProvisionArrangement arrangement)
        {
            var result = new CAResolvedProvisionOperator
            {
                Identity = arrangement?.operatorIdentity,
                Kind = arrangement?.operatorKind
                    ?? CAProvisionOperator.Individual
            };
            if (record == null || arrangement == null || map == null)
            {
                result.Failure = "operator resolution lacks settlement or map";
                return result;
            }
            if (arrangement.operatorIdentity.NullOrEmpty()
                || !DirectSource(arrangement.operatorSource))
            {
                result.Failure = "operator identity lacks authored or observed provenance";
                return result;
            }

            switch (arrangement.operatorKind)
            {
                case CAProvisionOperator.DomesticUnit:
                    result.DomesticUnit = record.domesticUnits?.FirstOrDefault(
                        unit => unit != null && unit.active
                            && unit.unitIdentity
                                == arrangement.operatorIdentity);
                    if (result.DomesticUnit == null)
                    {
                        result.Failure = "domestic unit does not exist";
                        return result;
                    }
                    result.Consumers = CADomesticResidenceAdapter.Consumers(
                        record, map, arrangement.operatorIdentity);
                    if (result.Consumers.Count == 0)
                        result.Failure = "domestic unit has no current resident members";
                    return result;

                case CAProvisionOperator.Individual:
                    if (!TryPawnId(arrangement.operatorIdentity,
                            out int pawnId))
                    {
                        result.Failure = "individual operator identity is not pawn-scoped";
                        return result;
                    }
                    result.Individual = CAPopulationProjection.Residents(record,
                        map).FirstOrDefault(pawn =>
                            pawn.thingIDNumber == pawnId);
                    if (result.Individual == null)
                    {
                        result.Failure = "individual operator is not a current resident";
                        return result;
                    }
                    result.Consumers.Add(result.Individual);
                    return result;

                case CAProvisionOperator.Authority:
                case CAProvisionOperator.Communal:
                    result.Organization = CAOrganizationWorldComponent.Current
                        ?.ByKey(arrangement.operatorIdentity);
                    if (result.Organization == null)
                    {
                        result.Failure = "operator organization does not exist";
                        return result;
                    }
                    if (arrangement.operatorKind
                            == CAProvisionOperator.Authority
                        && result.Organization.organizationKind
                            != CAOrganizationKind.Settlement
                        && result.Organization.organizationKind
                            != CAOrganizationKind.Faction
                        && result.Organization.organizationKind
                            != CAOrganizationKind.Colony)
                    {
                        result.Failure = "authority provision does not resolve to an authority-bearing organization";
                        return result;
                    }
                    result.Consumers = CAProvisionAccessService
                        .EligibleResidents(record, map, arrangement,
                            result.Organization);
                    return result;
            }
            result.Failure = "unsupported operator kind";
            return result;
        }

        private static bool DirectSource(string source)
        {
            return source?.StartsWith("authored:",
                       StringComparison.Ordinal) == true
                || source?.StartsWith("observed:",
                       StringComparison.Ordinal) == true;
        }

        private static bool TryPawnId(string identity, out int pawnId)
        {
            pawnId = -1;
            if (identity.NullOrEmpty()) return false;
            string prefix = identity.StartsWith("pawn:",
                StringComparison.Ordinal) ? "pawn:"
                : identity.StartsWith("individual:",
                    StringComparison.Ordinal) ? "individual:" : null;
            return prefix != null && int.TryParse(identity.Substring(
                prefix.Length), out pawnId) && pawnId >= 0;
        }
    }

    // Placement-authored provisions bind to the settlement organization and
    // the exact residents recorded as their initial workers. This realizes
    // saved operator and funding facts before program preflight; it does not
    // infer an operator from Culture, Political Order, or nearby assets.
    internal static class CAProvisionPlacementOperatorMaterializer
    {
        private const string PlacementSourcePrefix =
            "authored:regional-placement:";
        private const string RelationOriginPrefix =
            "provision-placement:";

        internal static CAOrganization Materialize(
            CARegionalSettlementRecord record, Map map)
        {
            CAOrganization organization = CASettlementAuthorityWriter
                .EnsureSettlementOrganization(record);
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (organization == null || ledger == null || record == null
                || map == null) return organization;

            string recordOrigin = RelationOriginPrefix
                + record.regionalId + "#" + record.slot + ":";
            var desired = new Dictionary<string, HashSet<int>>(
                StringComparer.Ordinal);
            foreach (CAProvisionArrangement arrangement in
                record.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
            {
                if (arrangement == null || !arrangement.active
                    || arrangement.operatorIdentity
                        != organization.organizationKey
                    || arrangement.operatorSource?.StartsWith(
                        PlacementSourcePrefix,
                        StringComparison.Ordinal) != true
                    || arrangement.funding
                        != CAProvisionFunding.SharedWork)
                    continue;
                List<Pawn> workers = arrangement.populationGroupKey >= 0
                    ? CAPopulationProjection.ResidentsInPopulationGroup(
                        record, map, arrangement.populationGroupKey)
                    : CAPopulationProjection.Residents(record, map);
                desired[recordOrigin + arrangement.key] =
                    new HashSet<int>(workers.Where(pawn => pawn != null)
                        .Select(pawn => pawn.thingIDNumber));
            }

            List<CARelation> existing = ledger.RelationsIn(
                organization.organizationKey);
            foreach (CARelation relation in existing.Where(item =>
                item != null && item.origin.Replaceable
                && item.origin.originKey?.StartsWith(recordOrigin,
                    StringComparison.Ordinal) == true).ToList())
            {
                if (!desired.TryGetValue(relation.origin.originKey,
                        out HashSet<int> wanted)
                    || !relation.IsPawnParty
                    || !wanted.Contains(relation.PawnPartyId))
                    ledger.RemoveDerivedRelation(relation);
            }

            foreach (KeyValuePair<string, HashSet<int>> group in desired)
            {
                int arrangementKey = int.TryParse(group.Key.Substring(
                    recordOrigin.Length), out int parsed) ? parsed : 0;
                string role = "starting provision work " + arrangementKey;
                foreach (int pawnId in group.Value.OrderBy(value => value))
                {
                    CARelation relation = ledger.RelationsIn(
                            organization.organizationKey)
                        .FirstOrDefault(item => item != null
                            && item.IsPawnParty
                            && item.PawnPartyId == pawnId
                            && item.role == role);
                    if (relation == null)
                    {
                        relation = new CARelation
                        {
                            orgKey = organization.organizationKey,
                            role = role,
                            compensation = CACompensationKinds.Ration,
                            protection =
                                CAProtectionKinds.OrganizationRule,
                            entry = CAEntryKinds.Free,
                            exit = CAExitKinds.Free,
                            startTick = Find.TickManager?.TicksGame ?? 0,
                            sunsetTick = -1,
                            origin = CAOrigin.Derived(group.Key),
                            termsOrigin = CAOrigin.Authored(group.Key)
                        };
                        relation.Party = CARelationPartyRef.OfPawn(pawnId);
                        relation.delegatedResponsibilities.Add(
                            CAResponsibilities.Work);
                        ledger.Add(relation);
                    }
                    else if (relation.origin.Replaceable
                        && relation.origin.originKey == group.Key)
                    {
                        relation.delegatedResponsibilities.Clear();
                        relation.delegatedResponsibilities.Add(
                            CAResponsibilities.Work);
                        relation.compensation =
                            CACompensationKinds.Ration;
                        relation.protection =
                            CAProtectionKinds.OrganizationRule;
                        relation.termsOrigin = CAOrigin.Authored(group.Key);
                    }
                }
            }
            return organization;
        }
    }
}
