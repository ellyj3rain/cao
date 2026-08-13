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
}
