using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    // Formation and removal lifecycle for directly authored established
    // programs. The saved operational fact is authoritative; the registry
    // derives the material program from it.
    internal static class CASettlementProgramAuthoring
    {
        // A newly placed inhabited settlement needs a real starting operation,
        // not merely the abstract capacity to create one later. Placement is
        // the authoring boundary: ground, population, and faction knowledge
        // select a minimum coherent composition, and the exact resulting facts
        // are persisted before confirmation. Any existing active or inactive
        // fact is operational history and prevents this initializer from
        // replacing an operator's later edits.
        internal static bool EnsureStartingComposition(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, out string failure)
        {
            failure = null;
            if (plan == null || settlement == null || plan.confirmed
                || !plan.operatorAuthored)
                return false;
            if (settlement.operationalFacts == null)
                settlement.operationalFacts =
                    new List<CASettlementOperationalFact>();
            if (settlement.operationalFacts.Any(item => item != null))
                return false;
            if (settlement.provisionArrangements == null)
                settlement.provisionArrangements =
                    new List<CAProvisionArrangement>();
            if (settlement.provisionArrangements.Any(item => item != null))
                return false;
            if (plan.regionalId.NullOrEmpty())
            {
                failure = "the region has no stable identity for its "
                    + "starting operators";
                return false;
            }

            CASettlementPopulationGroup group = (settlement.populationGroups
                    ?? new List<CASettlementPopulationGroup>())
                .Where(item => item != null && item.share > 0)
                .OrderByDescending(item => item.isPrimary)
                .ThenByDescending(item => item.share)
                .ThenBy(item => item.key).FirstOrDefault();
            if (group == null)
            {
                failure = "the settlement has no population to operate its "
                    + "starting programs";
                return false;
            }

            CASettlementEnvironmentFacts environment =
                CASettlementEnvironment.ForTile(settlement.memberTileId);
            CAProvisionArrangement provision = PlacementProvision(plan,
                settlement, group, out failure);
            if (provision == null) return false;
            List<string> programKeys = StartingProgramKeys(plan, settlement,
                environment);
            foreach (string programKey in programKeys)
                if (!CanMaterialize(plan, settlement, programKey,
                        out failure))
                    return false;

            var additions = new List<CASettlementOperationalFact>();
            foreach (string programKey in programKeys.Distinct(
                StringComparer.Ordinal))
            {
                bool isProvision = programKey
                    == CASettlementProgramRegistry.CommunalProvision;
                CAEstablishedProgramFactSpec spec =
                    CASettlementOperationalFactAuthoringKernel
                        .EstablishForPlacement(programKey, group.key, 1,
                            isProvision ? provision.operatorIdentity : null,
                            isProvision ? provision.fundingSource : null,
                            isProvision ? provision.stockSource : null,
                            isProvision ? provision.materialSource : null,
                            isProvision ? provision.accessSource : null);
                additions.Add(FromSpec(spec));
            }
            settlement.provisionArrangements.Add(provision);
            settlement.operationalFacts.AddRange(additions);
            CASettlementProgramRegistry.EnsureDerived(plan, settlement,
                force: true);
            CASettlementProgramEntry provisionProgram = settlement
                .settlementProgram?.Entry(
                    CASettlementProgramRegistry.CommunalProvision,
                    provision.operatorIdentity);
            if (!CASettlementComposition.TryValidateProvisionArrangements(
                    plan, settlement, out failure)
                || provisionProgram == null)
            {
                if (failure.NullOrEmpty())
                    failure = "the starting provision program did not "
                        + "preserve its exact operator";
                var factKeys = new HashSet<string>(additions.Select(item =>
                    item.factKey), StringComparer.Ordinal);
                settlement.operationalFacts.RemoveAll(item => item != null
                    && factKeys.Contains(item.factKey));
                settlement.provisionArrangements.RemoveAll(item =>
                    item != null && item.basisKey == provision.basisKey);
                CASettlementProgramRegistry.EnsureDerived(plan, settlement,
                    force: true);
                return false;
            }
            return additions.Count > 0;
        }

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
            List<CASettlementOperationalFact> matches =
                (settlement.operationalFacts
                    ?? new List<CASettlementOperationalFact>())
                .Where(item => item != null && item.active
                    && item.programKey == entry.programKey
                    && item.operatorIdentity == entry.operatorIdentity)
                .ToList();
            if (matches.Count == 0) return false;
            foreach (CASettlementOperationalFact fact in matches)
                fact.active = false;
            CASettlementProgramRegistry.EnsureDerived(plan, settlement,
                force: true);
            plan.operatorAuthored = true;
            plan.confirmed = false;
            CARegionalSetupSession.SavePending();
            return true;
        }

        private static List<string> StartingProgramKeys(CARegionalPlan plan,
            CARegionalSettlementPlan settlement,
            CASettlementEnvironmentFacts environment)
        {
            var result = new List<string>
            {
                CASettlementProgramRegistry.Housing,
                CASettlementProgramRegistry.CommunalProvision
            };
            CAHabitatRequirementProfile requirements =
                CAHabitatViability.Requirements(environment);
            CATechnologicalKnowledge knowledge = CASiteState.Knowledge(plan,
                settlement);
            bool canProtectCultivation = settlement.landCapacity >= 2
                && CATechnologicalKnowledgeModel.Rank(knowledge,
                    CATechnologyDomains.Agriculture,
                    CATechnologyCompetencies.Operate) >= 2;

            switch (environment?.FoodRoute
                ?? CAHabitatFoodRoute.OutdoorCultivation)
            {
                case CAHabitatFoodRoute.ForageAndHunt:
                    result.Add(CASettlementProgramRegistry.Gathering);
                    break;
                case CAHabitatFoodRoute.ProtectedLowLightCultivation:
                case CAHabitatFoodRoute.ProtectedCultivation:
                    if (canProtectCultivation)
                    {
                        result.Add(CASettlementProgramRegistry.Agriculture);
                        result.Add(CASettlementProgramRegistry.Production);
                    }
                    else
                    {
                        result.Add(CASettlementProgramRegistry.Trade);
                        result.Add(CASettlementProgramRegistry.Storage);
                    }
                    break;
                case CAHabitatFoodRoute.StoredAndSupported:
                    result.Add(CASettlementProgramRegistry.Trade);
                    result.Add(CASettlementProgramRegistry.Storage);
                    break;
                default:
                    result.Add(settlement.landCapacity >= 2
                        ? CASettlementProgramRegistry.Agriculture
                        : CASettlementProgramRegistry.Gathering);
                    break;
            }

            if (requirements.Requires(CAHabitatRequirement.FoodReserve)
                || requirements.Requires(
                    CAHabitatRequirement.WaterTreatment))
                result.Add(CASettlementProgramRegistry.Storage);
            if (requirements.Requires(CAHabitatRequirement.MedicalCare)
                || requirements.Requires(
                    CAHabitatRequirement.HazardProtection))
                result.Add(CASettlementProgramRegistry.Medicine);
            if (requirements.Requires(CAHabitatRequirement.ThermalControl)
                || requirements.Requires(
                    CAHabitatRequirement.HazardProtection)
                || requirements.Requires(
                    CAHabitatRequirement.BreathableInterior))
                result.Add(CASettlementProgramRegistry.Production);

            // Standing composes function. A population past the town
            // threshold operates production beyond subsistence; past the
            // urban threshold it operates a market and its stores; an
            // established history keeps stores; a market on coastal ground
            // reaches for water transport. Each is a real starting contract,
            // so it joins only where its assets can materialize on this
            // ground -- environment constrains the authoring act without
            // owning the program.
            if (settlement.residentPopulation >= 140)
                AddMaterializable(plan, settlement, result,
                    CASettlementProgramRegistry.Production);
            if (settlement.residentPopulation >= 280)
            {
                AddMaterializable(plan, settlement, result,
                    CASettlementProgramRegistry.Trade);
                AddMaterializable(plan, settlement, result,
                    CASettlementProgramRegistry.Storage);
            }
            if (settlement.historicalDevelopment >= 2)
                AddMaterializable(plan, settlement, result,
                    CASettlementProgramRegistry.Storage);
            if (settlement.hasCoastalAccess
                && result.Contains(CASettlementProgramRegistry.Trade))
                AddMaterializable(plan, settlement, result,
                    CASettlementProgramRegistry.Transport);

            // A DECLARED ROLE IS A FACT OF THE SAME KIND. Environment,
            // habitat requirement, standing, history and coast all
            // compose this list; the operational roles an operator
            // declared for this settlement did not, so a place marked
            // as a combat outpost or a casualty collection point was
            // built exactly like one marked as nothing. The mask was
            // authored, saved, summarized and drawn on the globe, and
            // changed nothing on the ground.
            //
            // Each role enters as the program it IS when built: a
            // patrol base, an observation post and a combat outpost are
            // all defensive works; a logistics point holds and moves
            // goods; a relay is a communications installation; a
            // casualty collection point is medical; and a place people
            // are meant to fall back TO keeps reserves for the people
            // who reach it with nothing.
            //
            // Through AddMaterializable, so a role still cannot conjure
            // works this ground and these people could not raise. A
            // declaration is a commitment, not an exemption.
            foreach (string programKey in CAOperationalRoleProgramKernel
                .ProgramKeysFor(settlement.operationalRoleMask))
                if (CanBuild(plan, settlement, knowledge, programKey))
                    AddMaterializable(plan, settlement, result,
                        programKey);
            return result.Distinct(StringComparer.Ordinal).ToList();
        }

        // AddMaterializable screens whether a program's assets EXIST and
        // can stand on this ground; it does not ask whether these people
        // know how to build them. The materializer does ask, and treats
        // a shortfall as a blocker that stops the settlement being
        // created at all - so a role admitted here on asset grounds
        // alone could turn a declaration into a failed materialization.
        //
        // A role commits a settlement to works within its people's
        // reach. Beyond that reach the declaration is simply not
        // honoured in fabric, which is the same answer the rest of the
        // substrate gives when knowledge runs out.
        private static bool CanBuild(CARegionalPlan plan,
            CARegionalSettlementPlan settlement,
            CATechnologicalKnowledge knowledge, string programKey)
        {
            CASettlementProgramDef definition =
                CASettlementProgramRegistry.Find(programKey);
            if (definition == null) return false;
            // Saved geography rather than built assets; nothing to know.
            if (definition.NativeSpatialContract) return true;
            foreach (string[] group in definition.CandidateGroups
                ?.Invoke(plan, settlement)
                    ?? Enumerable.Empty<string[]>())
            {
                bool any = false;
                foreach (string name in CASettlementProgramRegistry
                    .LoadedFunctionalCandidates(definition, group))
                {
                    ThingDef candidate = DefDatabase<ThingDef>
                        .GetNamedSilentFail(name);
                    if (candidate == null) continue;
                    if (CATechnologicalKnowledgeRuntime
                        .CanConstructCanonical(knowledge, candidate,
                            out _))
                    {
                        any = true;
                        break;
                    }
                }
                if (!any) return false;
            }
            return true;
        }

        private static void AddMaterializable(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, List<string> keys,
            string programKey)
        {
            if (!keys.Contains(programKey)
                && CanMaterialize(plan, settlement, programKey, out _))
                keys.Add(programKey);
        }

        private static CAProvisionArrangement PlacementProvision(
            CARegionalPlan plan, CARegionalSettlementPlan settlement,
            CASettlementPopulationGroup group, out string failure)
        {
            failure = null;
            ThingDef stock = DefDatabase<ThingDef>
                .GetNamedSilentFail("Pemmican");
            if (stock == null || stock.category != ThingCategory.Item
                || stock.stackLimit < 40)
            {
                failure = "the starting provision stock is not loaded";
                return null;
            }
            string operatorIdentity = CASettlementAuthorityWriter
                .OrganizationKey(plan, settlement.slot);
            if (operatorIdentity.NullOrEmpty())
            {
                failure = "the settlement has no stable organization identity";
                return null;
            }
            string source = "authored:regional-placement:"
                + CASettlementProgramRegistry.CommunalProvision;
            var arrangement = new CAProvisionArrangement
            {
                key = 1,
                basisKey = "regional-placement:" + plan.regionalId + ":"
                    + settlement.slot + ":provision:1",
                basisLabel = "Shared kitchen",
                operatorKind = CAProvisionOperator.Communal,
                operatorIdentity = operatorIdentity,
                operatorSource = source + ":operator",
                populationGroupKey = group.key,
                access = CAProvisionAccess.Universal,
                accessSource = source + ":access",
                funding = CAProvisionFunding.SharedWork,
                fundingSource = source + ":funding",
                distribution = CAProvisionDistribution.Centralized,
                distributionSource = source + ":distribution",
                laborSource = source + ":labor",
                knowledgeSource = source + ":knowledge",
                materialSource = source + ":materials",
                stockSource = "authored:Pemmican:40",
                nodes = 1,
                reach = CAProvisionReach.Settlement,
                active = true,
                operational = false,
                waterSecured = true
            };
            var fact = new CAProvisionOperatorFact
            {
                Key = arrangement.key,
                BasisKey = arrangement.basisKey,
                BasisLabel = arrangement.basisLabel,
                Operator = arrangement.operatorKind.ToString(),
                OperatorIdentity = arrangement.operatorIdentity,
                OperatorSource = arrangement.operatorSource,
                PopulationGroupKey = arrangement.populationGroupKey,
                Access = arrangement.access.ToString(),
                AccessSource = arrangement.accessSource,
                Funding = arrangement.funding.ToString(),
                FundingSource = arrangement.fundingSource,
                Distribution = arrangement.distribution.ToString(),
                DistributionSource = arrangement.distributionSource,
                LaborSource = arrangement.laborSource,
                KnowledgeSource = arrangement.knowledgeSource,
                MaterialSource = arrangement.materialSource,
                StockSource = arrangement.stockSource,
                Nodes = arrangement.nodes,
                Reach = arrangement.reach.ToString()
            };
            if (CAProvisionCausalKernel.Complete(fact)) return arrangement;
            failure = "the starting provision arrangement is incomplete";
            return null;
        }

        private static bool CanMaterialize(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, string programKey,
            out string failure)
        {
            failure = null;
            CASettlementProgramDef definition =
                CASettlementProgramRegistry.Find(programKey);
            if (definition == null)
            {
                failure = "starting program " + programKey
                    + " is not registered";
                return false;
            }
            if (definition.NativeSpatialContract)
            {
                if (definition.SpatiallyRealized?.Invoke(plan, settlement)
                    == true) return true;
                failure = definition.Label
                    + " cannot be established on the selected ground";
                return false;
            }
            int roles = 0;
            foreach (string[] group in definition.CandidateGroups
                ?.Invoke(plan, settlement)
                    ?? Enumerable.Empty<string[]>())
            {
                roles++;
                if (CASettlementProgramRegistry.LoadedFunctionalCandidates(
                        definition, group).Count > 0) continue;
                failure = definition.Label
                    + " has no loaded functional asset for one required role";
                return false;
            }
            if (roles >= definition.MinimumFunctionalRoles) return true;
            failure = definition.Label
                + " has no complete material contract";
            return false;
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
