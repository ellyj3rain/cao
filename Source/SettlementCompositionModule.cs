using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // A settlement population consists of population groups. Each group
    // carries faction affiliation, Ideoligion, political beliefs, population
    // share, and starting certainty. Culture is set for the settlement
    // population as a whole. Faction and Ideoligion use RimWorld's native pawn
    // fields where possible; political beliefs remain durable CA state.
    // Starting provisions are generated from population, faction structure,
    // facilities, infrastructure, and settlement role. Each generated basis
    // may be overridden independently.
    public enum CAPopulationGroupKind : byte
    {
        Main,
        // Real pawns of another faction living here. Converted to a local
        // political group when the two factions are hostile.
        OtherFaction,
        // A distinct local group whose pawns belong to the settlement faction.
        // Its Ideoligion or political beliefs may differ.
        LocalResidents,
        Unaffiliated
    }

    public sealed class CASettlementPopulationGroup : IExposable
    {
        public int key;
        public CAPopulationGroupKind kind;
        public string label;
        // Percent of the settlement's population. A percentage is the
        // right shape here because a real denominator exists.
        public int share;
        // Faction affiliation; -1 for unaffiliated. Resolved to a RimWorld
        // faction at materialization where residency permits it.
        public int factionKey = -1;
        // Political beliefs are independent of faction affiliation and
        // Ideoligion. -1 inherits from factionKey.
        public int politicalBeliefsFactionKey = -1;
        // Stable political-belief identity stamped when the draft becomes a
        // settlement record, so later faction changes do not erase it.
        public string politicalBeliefsId;
        // Starting Ideoligion certainty applied to this population group.
        // 0 low, 1 normal, 2 high.
        public int ideoligionCertainty = 1;
        // Ideoligion source, independent of faction affiliation. -1 inherits
        // from factionKey.
        public int ideoligionFactionKey = -1;
        // >= 0 creates a deterministic independent Ideoligion at
        // materialization and registers it as a minor Ideoligion.
        public int independentIdeoligionKey = -1;
        // A concentrated population group receives a local quarter and, when
        // applicable, separate provision rooms.
        public bool quarter;
        public bool authored;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key", 0);
            Scribe_Values.Look(ref kind, "kind",
                CAPopulationGroupKind.Main);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref share, "share", 0);
            Scribe_Values.Look(ref factionKey, "factionKey", -1);
            Scribe_Values.Look(ref politicalBeliefsFactionKey,
                "politicalBeliefsFactionKey", -1);
            Scribe_Values.Look(ref politicalBeliefsId,
                "politicalBeliefsId");
            Scribe_Values.Look(ref ideoligionCertainty,
                "ideoligionCertainty", 1);
            Scribe_Values.Look(ref ideoligionFactionKey,
                "ideoligionFactionKey", -1);
            Scribe_Values.Look(ref independentIdeoligionKey, "independentIdeoligionKey", -1);
            Scribe_Values.Look(ref quarter, "quarter", false);
            Scribe_Values.Look(ref authored, "authored", false);
        }

        internal string CertaintyWords
        {
            get
            {
                return ideoligionCertainty <= 0 ? "low"
                    : ideoligionCertainty == 1 ? "normal" : "high";
            }
        }

        internal float CertaintyTarget
        {
            get
            {
                return ideoligionCertainty <= 0 ? 0.45f
                    : ideoligionCertainty == 1 ? 0.70f : 0.92f;
            }
        }
    }

    // Who operates a source of starting provisions. Distribution changes its
    // physical setup; access and funding are recorded for supporting systems.
    public enum CAProvisionOperator : byte
    {
        // The settlement authority itself: reserve stores and
        // distribution. Tax-funded arrangements use the taxation loop.
        Authority,
        // Kitchens supplied through shared work.
        Communal,
        // Private sellers of food. Fee-funded stores sell through the
        // transaction ledger when the settlement materializes.
        Vendor,
        // A religious community's meal hall.
        Religious,
        Household
    }

    public enum CAProvisionAccess : byte
    { Universal, Members, Fee, Charitable }

    public enum CAProvisionFunding : byte
    {
        Household,
        Taxation,
        Dues,
        Fees,
        SharedWork,
        Charity
    }

    public enum CAProvisionDistribution : byte
    { Household, Neighborhood, Centralized }

    // How far an arrangement's provision actually reaches [layer E].
    public enum CAProvisionReach : byte
    { Household, Settlement, Region }

    public sealed class CAStartingProvision : IExposable
    {
        public int key;
        // Stable causal slot. Generated provisions are reconciled by this
        // identity, so changing access, facilities, population, or faction
        // parameters refreshes the generated plan without stacking another
        // copy. A saved distribution with the same basis overrides that field.
        public string basisKey;
        public string basisLabel;
        public CAProvisionOperator operatorKind;
        // Which population group operates this arrangement; -1 means
        // the settlement as a whole.
        public int populationGroupKey = -1;
        public CAProvisionAccess access;
        public CAProvisionFunding funding;
        public CAProvisionDistribution distribution;
        // The operator's saved distribution preference is separate from the
        // currently feasible distribution. A household fallback can therefore
        // become active without erasing the chosen neighborhood/central form.
        public bool distributionAuthored;
        public int authoredDistribution = -1;
        public bool active = true;
        public string inactiveReason;
        // Stamped when the settlement materializes: whether this
        // arrangement's kitchen actually secured water. A kitchen without
        // water operates
        // degraded - the constraint is real, not descriptive.
        public bool waterSecured = true;
        // Node count and reach follow the regional settlement scale and this
        // settlement's role. A
        // metropolitan neighborhood network raises more kitchens than a
        // hamlet, and only a regional seat's reserve serves surrounding
        // settlements. Each provision keeps its own reach.
        public int nodes = 1;
        public CAProvisionReach reach = CAProvisionReach.Settlement;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key", 0);
            Scribe_Values.Look(ref basisKey, "basisKey");
            Scribe_Values.Look(ref basisLabel, "basisLabel");
            Scribe_Values.Look(ref operatorKind, "operatorKind",
                CAProvisionOperator.Household);
            Scribe_Values.Look(ref populationGroupKey, "populationGroupKey", -1);
            Scribe_Values.Look(ref access, "access",
                CAProvisionAccess.Universal);
            Scribe_Values.Look(ref funding, "funding",
                CAProvisionFunding.Household);
            Scribe_Values.Look(ref distribution, "distribution",
                CAProvisionDistribution.Centralized);
            Scribe_Values.Look(ref distributionAuthored,
                "distributionAuthored", false);
            Scribe_Values.Look(ref authoredDistribution,
                "authoredDistribution", -1);
            Scribe_Values.Look(ref active, "active", true);
            Scribe_Values.Look(ref inactiveReason, "inactiveReason");
            Scribe_Values.Look(ref waterSecured, "waterSecured", true);
            Scribe_Values.Look(ref nodes, "nodes", 1);
            Scribe_Values.Look(ref reach, "reach",
                CAProvisionReach.Settlement);
        }

        internal string OperatorWords
        {
            get
            {
                switch (operatorKind)
                {
                    case CAProvisionOperator.Authority:
                        return "authority reserve";
                    case CAProvisionOperator.Communal:
                        return "communal kitchens";
                    case CAProvisionOperator.Vendor:
                        return "private vendors";
                    case CAProvisionOperator.Religious:
                        return "religious meal hall";
                    default: return "household hearths";
                }
            }
        }

        internal string Summary
        {
            get
            {
                return OperatorWords + " · " + access.ToString().ToLower()
                    + " access · funded by "
                    + FundingWords + " · "
                    + distribution.ToString().ToLower()
                    + (nodes > 1 ? " x" + nodes : "")
                    + (reach == CAProvisionReach.Region
                        ? " · serves the surrounding region" : "")
                    + (waterSecured ? "" : " · no secured water");
            }
        }

        internal string FundingWords
        {
            get
            {
                switch (funding)
                {
                    case CAProvisionFunding.SharedWork:
                        return "shared work";
                    case CAProvisionFunding.Household:
                        return "the household";
                    default: return funding.ToString().ToLower();
                }
            }
        }

        internal CAProvisionDistribution PreferredDistribution
        {
            get
            {
                return authoredDistribution >= 0
                    ? (CAProvisionDistribution)authoredDistribution
                    : distribution;
            }
        }

    }

    // Generated settlement composition fills unset fields from saved facts.
    // Authored entries are preserved.
    internal static class CASettlementComposition
    {
        // A world-policy change owns generated population shares. Once any
        // population row is authored, the settlement composition itself is a
        // Starting Region override and remains intact.
        internal static void ClearPolicyGeneratedPopulation(
            CARegionalPlan plan)
        {
            if (plan?.settlements == null) return;
            foreach (CARegionalSettlementPlan settlement in plan.settlements)
            {
                if (settlement?.populationGroups == null
                    || settlement.populationGroups.Any(item => item != null
                        && item.authored))
                    continue;
                settlement.populationGroups.Clear();
            }
        }

        internal static void EnsureDerived(CARegionalPlan plan,
            CARegionalSettlementPlan settlementPlan)
        {
            if (plan == null || settlementPlan == null) return;
            if (settlementPlan.populationGroups == null)
                settlementPlan.populationGroups = new List<CASettlementPopulationGroup>();
            if (settlementPlan.startingProvisions == null)
                settlementPlan.startingProvisions = new List<CAStartingProvision>();
            if (settlementPlan.populationGroups.Count == 0)
                DerivePopulationGroups(plan, settlementPlan);
            ReconcileStartingProvisions(plan, settlementPlan);
        }

        internal static void ReconcileStartingProvisions(CARegionalPlan plan,
            CARegionalSettlementPlan settlementPlan)
        {
            if (plan == null || settlementPlan == null) return;
            if (settlementPlan.startingProvisions == null)
                settlementPlan.startingProvisions = new List<CAStartingProvision>();
            List<CAStartingProvision> generated = GenerateStartingProvisions(
                plan, settlementPlan);

            var distributionByBasis = settlementPlan.startingProvisions.Where(item =>
                    item != null && item.distributionAuthored
                    && IsGeneratedBasis(item.basisKey))
                .GroupBy(item => item.basisKey)
                .ToDictionary(group => group.Key, group => group.First());
            var reconciled = new List<CAStartingProvision>();
            var consumed = new HashSet<CAStartingProvision>();
            int facilities = CASettlementStartingState.Sync(plan, settlementPlan,
                plan.FactionPlan(settlementPlan.factionKey)?.ResolvedFactionDef);
            int access = CASettlementStartingState.Access(plan, settlementPlan);
            int services = CASettlementStartingState.Services(plan, settlementPlan);
            int civic = CASettlementStartingState.Civic(plan, settlementPlan);
            foreach (CAStartingProvision derived in generated)
            {
                CAStartingProvision saved;
                if (distributionByBasis.TryGetValue(derived.basisKey,
                        out saved))
                {
                    // Start from a newly derived basis so operator identity and
                    // facility preconditions cannot go stale. Overlay only the
                    // saved distribution, then recompute its consequences.
                    if (saved.distributionAuthored)
                    {
                        derived.authoredDistribution = (int)saved
                            .PreferredDistribution;
                        derived.distribution = saved
                            .PreferredDistribution;
                    }
                    derived.distributionAuthored =
                        saved.distributionAuthored;
                    derived.waterSecured = saved.waterSecured;
                    derived.active = true;
                    derived.inactiveReason = null;
                    ApplyProvisionConsequences(plan, settlementPlan, derived,
                        facilities, access, services, civic);
                    reconciled.Add(derived);
                    consumed.Add(saved);
                }
                else reconciled.Add(derived);
            }
            // A generated basis whose cause disappeared is not materialized,
            // but its authored distribution remains as one inactive override.
            // If stores or the population group returns, the normal overlay
            // above consumes
            // it and reactivates the same basis without stacking a duplicate.
            foreach (CAStartingProvision saved in settlementPlan.startingProvisions
                .Where(item => item != null && item.distributionAuthored
                    && !consumed.Contains(item)
                    && IsGeneratedBasis(item.basisKey)))
            {
                saved.active = false;
                saved.inactiveReason = saved.basisKey == "reserve"
                    ? "no emergency reserve is generated"
                    : "the community that caused this basis is absent";
                reconciled.Add(saved);
            }
            settlementPlan.startingProvisions = reconciled
                .Where(item => item != null
                    && IsGeneratedBasis(item.basisKey))
                .GroupBy(item => item.basisKey)
                .Select(group => group.First()).ToList();
        }

        private static List<CAStartingProvision> GenerateStartingProvisions(
            CARegionalPlan plan, CARegionalSettlementPlan source)
        {
            var target = new CARegionalSettlementPlan
            {
                slot = source.slot,
                memberTileId = source.memberTileId,
                factionKey = source.factionKey,
                realizedRole = source.realizedRole,
                realizedScale = source.realizedScale,
                residentPopulation = source.residentPopulation,
                landCapacity = source.landCapacity,
                realizedAccessInfrastructure =
                    source.realizedAccessInfrastructure,
                realizedServiceInfrastructure =
                    source.realizedServiceInfrastructure,
                realizedCivicInfrastructure =
                    source.realizedCivicInfrastructure,
                economicCapacity = source.economicCapacity,
                tradeConnectivity = source.tradeConnectivity,
                specialization = source.specialization,
                historicalDevelopment = source.historicalDevelopment,
                urbanSupport = source.urbanSupport,
                populationGroups = source.populationGroups,
                startingProvisions = new List<CAStartingProvision>(),
                startingFacilityMask = source.startingFacilityMask,
                startingFacilityAuthoredMask =
                    source.startingFacilityAuthoredMask,
                startingFacilityValues = source.startingFacilityValues,
                accessInfrastructure = source.accessInfrastructure,
                serviceInfrastructure = source.serviceInfrastructure,
                civicInfrastructure = source.civicInfrastructure
            };
            DeriveStartingProvisions(plan, target);
            List<CAStartingProvision> result = target.startingProvisions;
            AssignGeneratedBases(result);

            int facilities = CASettlementStartingState.Sync(plan, source,
                plan.FactionPlan(source.factionKey)?.ResolvedFactionDef);
            int access = CASettlementStartingState.Access(plan, source);
            int services = CASettlementStartingState.Services(plan, source);
            int civic = CASettlementStartingState.Civic(plan, source);
            if ((facilities & CAStartingFacilities.MaskStores) == 0)
                result.RemoveAll(item => item.basisKey == "reserve");

            foreach (CAStartingProvision item in result)
                ApplyProvisionConsequences(plan, source, item, facilities,
                    access, services, civic);
            return result;
        }

        private static bool IsGeneratedBasis(string basisKey)
        {
            return basisKey == "everyday" || basisKey == "reserve"
                || basisKey == "mixed:shared"
                || basisKey?.StartsWith("populationGroup:") == true;
        }

        private static void ApplyProvisionConsequences(CARegionalPlan plan,
            CARegionalSettlementPlan source, CAStartingProvision item,
            int facilities, int access, int services, int civic)
        {
            if (item == null) return;
            bool hasKitchen = (facilities
                & (CAStartingFacilities.MaskHearth
                    | CAStartingFacilities.MaskDining)) != 0;
            if (item.basisKey == "everyday" && !hasKitchen)
            {
                item.operatorKind = CAProvisionOperator.Household;
                item.access = CAProvisionAccess.Members;
                item.funding = CAProvisionFunding.Household;
                item.distribution = CAProvisionDistribution.Household;
            }

            CARegionalSettlements.EnsureSettlementPattern(plan);
            var scale = CARegionalSettlements.RealizedScaleOf(plan, source);
            var role = (CASettlementRole)source.realizedRole;
            int neighborhoodNodes = scale >= CASettlementScale.LargeUrbanRegion
                ? 3 : scale >= CASettlementScale.UrbanCenter ? 2
                    : scale >= CASettlementScale.RegionalCenter
                        && role == CASettlementRole.Center ? 2 : 1;
            if (services >= 2 || civic >= 2)
                neighborhoodNodes = Math.Max(2, neighborhoodNodes);

            switch (item.distribution)
            {
                case CAProvisionDistribution.Household:
                    item.nodes = 1;
                    item.reach = CAProvisionReach.Household;
                    break;
                case CAProvisionDistribution.Neighborhood:
                    item.nodes = Mathf.Clamp(neighborhoodNodes, 1, 3);
                    item.reach = item.basisKey == "reserve" && access >= 2
                            && role == CASettlementRole.Center
                        ? CAProvisionReach.Region
                        : CAProvisionReach.Settlement;
                    break;
                default:
                    item.nodes = 1;
                    item.reach = item.basisKey == "reserve" && access >= 2
                            && role == CASettlementRole.Center
                        ? CAProvisionReach.Region
                        : CAProvisionReach.Settlement;
                    break;
            }
        }

        private static void AssignGeneratedBases(
            List<CAStartingProvision> arrangements)
        {
            int whole = 0;
            foreach (CAStartingProvision item in arrangements)
            {
                if (item == null) continue;
                if (!item.basisKey.NullOrEmpty()) continue;
                if (item.populationGroupKey >= 0)
                {
                    item.basisKey = "populationGroup:" + item.populationGroupKey;
                    item.basisLabel = "Community provision";
                    item.key = 1000 + item.populationGroupKey;
                }
                else if (whole++ == 0)
                {
                    item.basisKey = "everyday";
                    item.basisLabel = "Everyday provision";
                    item.key = 1;
                }
                else
                {
                    item.basisKey = "reserve";
                    item.basisLabel = "Emergency reserve";
                    item.key = 2;
                }
            }
        }

        private static int Seed(CARegionalPlan plan,
            CARegionalSettlementPlan settlementPlan, string salt)
        {
            return Gen.HashCombineInt(GenText.StableStringHash(
                (plan.candidateId ?? "ca-candidate") + ":" + salt),
                settlementPlan.slot * 7919);
        }

        // Settlement population: the main population, residents affiliated
        // with another faction, and unaffiliated residents. Historical
        // disruption influences the generated mix.
        private static void DerivePopulationGroups(CARegionalPlan plan,
            CARegionalSettlementPlan settlementPlan)
        {
            CARegionalWorldPolicy policy = plan.worldPolicy
                ?? new CARegionalWorldPolicy();
            CARegionalFactionPlan owner = plan.FactionPlan(
                settlementPlan.factionKey);
            Rand.PushState(Seed(plan, settlementPlan, "populationGroups"));
            try
            {
                int unaffiliated = CAWorldTendencyCausalKernel
                    .UnaffiliatedPercent(policy.unaffiliatedPopulationShare,
                        Rand.RangeInclusive(0, 3));
                int minorityShare = 0;
                CARegionalFactionPlan minoritySource = null;
                List<CARegionalFactionPlan> others =
                    plan.factions.Where(item => item != null
                        && item.key != settlementPlan.factionKey
                        && plan.settlements.Any(place => place != null
                            && place.factionKey == item.key)
                        && plan.RelationBetween(settlementPlan.factionKey,
                            item.key) != FactionRelationKind.Hostile)
                    .OrderBy(item => item.key).ToList();
                // Minority residence follows actual neighboring ownership and
                // saved relations. Settlement-ownership variety has already
                // done its work when owners were assigned.
                if (others.Count > 0 && Rand.Chance(0.55f))
                {
                    minoritySource = others[Rand.Range(0, others.Count)];
                    minorityShare = 8 + Rand.RangeInclusive(0, 22);
                }
                int dominant = 100 - unaffiliated - minorityShare;

                settlementPlan.populationGroups.Add(new CASettlementPopulationGroup
                {
                    key = 1,
                    kind = CAPopulationGroupKind.Main,
                    label = CARegionalPlanUtility.FactionName(owner),
                    share = dominant,
                    factionKey = settlementPlan.factionKey,
                    ideoligionCertainty = 1 + (Rand.Chance(0.35f) ? 1 : 0)
                });
                if (minoritySource != null)
                    settlementPlan.populationGroups.Add(new CASettlementPopulationGroup
                    {
                        key = 2,
                        kind = CAPopulationGroupKind.OtherFaction,
                        label = CARegionalPlanUtility.FactionName(
                            minoritySource),
                        share = minorityShare,
                        factionKey = minoritySource.key,
                        ideoligionCertainty = Rand.Chance(0.5f) ? 1 : 2,
                        // A substantial minority tends to have its own
                        // quarter; a small one lives distributed.
                        quarter = minorityShare >= 18
                    });
                settlementPlan.populationGroups.Add(new CASettlementPopulationGroup
                {
                    key = 3,
                    kind = CAPopulationGroupKind.Unaffiliated,
                    label = "Unaffiliated",
                    share = unaffiliated,
                    factionKey = -1,
                    ideoligionCertainty = 0
                });
            }
            finally { Rand.PopState(); }
        }

        // Generate starting provisions from arrangements in force. A group
        // with different ownership beliefs may receive a separate provider.
        private static void DeriveStartingProvisions(CARegionalPlan plan,
            CARegionalSettlementPlan settlementPlan)
        {
            CARegionalFactionPlan owner = plan.FactionPlan(
                settlementPlan.factionKey);
            if (owner == null) return;
            owner.EnsureCultureAndPolitics(plan);
            // Realized settlement pattern and scale determine provision nodes
            // and reach; world tendencies do not replace the saved result.
            CARegionalSettlements.EnsureSettlementPattern(plan);
            var scale = CARegionalSettlements.RealizedScaleOf(plan,
                settlementPlan);
            var topology = (CASettlementPattern)plan.settlementPattern;
            var role = (CASettlementRole)settlementPlan.realizedRole;
            int neighborhoodNodes =
                scale >= CASettlementScale.LargeUrbanRegion ? 3
                : scale >= CASettlementScale.UrbanCenter ? 2
                : scale >= CASettlementScale.RegionalCenter
                    && role == CASettlementRole.Center ? 2 : 1;
            CASettlementAuthority authority =
                CARegionalSettlements.SettlementAuthorityOf(plan, owner);
            bool locallyFundedReserve =
                (topology == CASettlementPattern.Dispersed
                    || topology == CASettlementPattern.Corridor)
                && (authority == CASettlementAuthority.IndependentWithSharedDefense
                    || authority == CASettlementAuthority.Independent);
            string ownershipAxis = CAFactionAxes.KeyOf(owner.factionStructure,
                CAFactionAxes.Ownership) ?? CAFactionAxes.KeyOf(
                    owner.politicalBeliefs?.positions, CAFactionAxes.Ownership);
            string workRule = CAFactionAxes.KeyOf(owner.factionStructure,
                CAFactionAxes.Work) ?? CAFactionAxes.KeyOf(
                    owner.politicalBeliefs?.positions, CAFactionAxes.Work);
            bool common = ownershipAxis == "common"
                || ownershipAxis == "cooperative";
            bool requiredWork = workRule == "duty";
            // Current faction structure determines provision; political
            // beliefs are used only while a structure field remains open.
            string provisionAxis = CAFactionAxes.KeyOf(owner.factionStructure,
                CAFactionAxes.Support);
            int next = 1;

            if (provisionAxis == "public")
                settlementPlan.startingProvisions.Add(new CAStartingProvision
                {
                    key = next++,
                    operatorKind = CAProvisionOperator.Authority,
                    access = CAProvisionAccess.Universal,
                    funding = CAProvisionFunding.Taxation,
                    distribution = CAProvisionDistribution.Neighborhood,
                    nodes = neighborhoodNodes,
                    reach = role == CASettlementRole.Center
                            && scale >= CASettlementScale.RegionalCenter
                        ? CAProvisionReach.Region
                        : CAProvisionReach.Settlement
                });
            else if (provisionAxis == "charitable")
                settlementPlan.startingProvisions.Add(new CAStartingProvision
                {
                    key = next++,
                    operatorKind = CAProvisionOperator.Religious,
                    access = CAProvisionAccess.Charitable,
                    funding = CAProvisionFunding.Charity,
                    distribution = CAProvisionDistribution.Centralized
                });
            else if (provisionAxis == "communal"
                || provisionAxis == null && common)
                settlementPlan.startingProvisions.Add(new CAStartingProvision
                {
                    key = next++,
                    operatorKind = CAProvisionOperator.Communal,
                    access = CAProvisionAccess.Universal,
                    funding = requiredWork
                        ? CAProvisionFunding.SharedWork
                        : CAProvisionFunding.Dues,
                    distribution = CAProvisionDistribution.Neighborhood,
                    nodes = neighborhoodNodes
                });
            else if (provisionAxis == "private")
                settlementPlan.startingProvisions.Add(new CAStartingProvision
                {
                    key = next++,
                    operatorKind = CAProvisionOperator.Household,
                    access = CAProvisionAccess.Members,
                    funding = CAProvisionFunding.Household,
                    distribution = CAProvisionDistribution.Household,
                    reach = CAProvisionReach.Household
                });
            else
            {
                settlementPlan.startingProvisions.Add(new CAStartingProvision
                {
                    key = next++,
                    operatorKind = CAProvisionOperator.Vendor,
                    access = CAProvisionAccess.Fee,
                    funding = CAProvisionFunding.Fees,
                    distribution = CAProvisionDistribution.Centralized,
                    nodes = scale >= CASettlementScale.UrbanCenter ? 2 : 1
                });
                if (provisionAxis == "mixed")
                    settlementPlan.startingProvisions.Add(new CAStartingProvision
                    {
                        key = 3,
                        basisKey = "mixed:shared",
                        basisLabel = "Shared support",
                        operatorKind = CAProvisionOperator.Communal,
                        access = CAProvisionAccess.Members,
                        funding = CAProvisionFunding.Dues,
                        distribution = CAProvisionDistribution.Neighborhood,
                        nodes = neighborhoodNodes
                    });
            }

            // Emergency reserve. A regional seat may serve surrounding
            // settlements. Independent or confederal settlements keep their
            // reserves local and fund them through member dues. Self-provided
            // settlements do not receive an authority reserve by implication.
            if (provisionAxis != "private")
                settlementPlan.startingProvisions.Add(new CAStartingProvision
                {
                    key = next++,
                    operatorKind = CAProvisionOperator.Authority,
                    access = CAProvisionAccess.Universal,
                    funding = locallyFundedReserve ? CAProvisionFunding.Dues
                        : CAProvisionFunding.Taxation,
                    distribution = CAProvisionDistribution.Centralized,
                    reach = role == CASettlementRole.Center
                            && scale >= CASettlementScale.RegionalCenter
                        ? CAProvisionReach.Region
                        : CAProvisionReach.Settlement
                });

            // Every other-faction population group with different ownership
            // gets its own provider.
            foreach (CASettlementPopulationGroup minority in
                settlementPlan.populationGroups.Where(item => item != null
                    && item.kind == CAPopulationGroupKind.OtherFaction)
                    .OrderBy(item => item.key))
            {
                CARegionalFactionPlan source = plan.FactionPlan(
                    minority.politicalBeliefsFactionKey >= 0
                        ? minority.politicalBeliefsFactionKey
                        : minority.factionKey);
                if (source != null)
                {
                    source.EnsureCultureAndPolitics(plan);
                    string minorityOwnership = CAFactionAxes.KeyOf(
                        source.factionStructure, CAFactionAxes.Ownership)
                        ?? CAFactionAxes.KeyOf(
                            source.politicalBeliefs?.positions,
                            CAFactionAxes.Ownership);
                    bool minorityCommon = minorityOwnership == "common"
                        || minorityOwnership == "cooperative";
                    if (!minorityOwnership.NullOrEmpty()
                        && minorityOwnership != ownershipAxis)
                        settlementPlan.startingProvisions.Add(
                            new CAStartingProvision
                            {
                                key = next++,
                                operatorKind = minorityCommon
                                    ? CAProvisionOperator.Communal
                                    : CAProvisionOperator.Vendor,
                                populationGroupKey = minority.key,
                                access = CAProvisionAccess.Members,
                                funding = minorityCommon
                                    ? CAProvisionFunding.SharedWork
                                    : CAProvisionFunding.Fees,
                                distribution =
                                    CAProvisionDistribution.Neighborhood
                            });
                }
            }
        }

        internal static string DescribePopulationGroups(
            List<CASettlementPopulationGroup> populationGroups)
        {
            if (populationGroups == null || populationGroups.Count == 0) return "underived";
            return string.Join(", ", populationGroups.Where(item => item != null)
                .Select(item => item.label + " " + item.share + "%")
                .ToArray());
        }
    }

    // Materializes saved population groups into native game state.
    internal static class CAPopulationProjection
    {
        // Applied after the settlement's resident pawn group has spawned.
        // Reads the record's population groups, produces real engine facts,
        // and writes an assignment table so the group of any
        // resident is a persisted CA fact.
        internal static void Apply(CARegionalSettlementRecord record,
            Map map)
        {
            try
            {
                if (record?.populationGroups == null || record.populationGroups.Count == 0
                    || map == null || record.faction == null) return;
                if (record.populationAssignments == null)
                    record.populationAssignments = new List<string>();
                record.populationAssignments.Clear();

                List<Pawn> residents = map.mapPawns
                    .SpawnedPawnsInFaction(record.faction)
                    .Where(p => p != null && p.RaceProps.Humanlike
                        && p.Position.InHorDistOf(
                            record.localRect.CenterCell, 60f))
                    .OrderBy(p => p.thingIDNumber).ToList();
                if (residents.Count == 0) return;

                int seed = Gen.HashCombineInt(
                    GenText.StableStringHash(record.regionalId), 4483);
                // [rights axis] How this faction treats difference decides
                // what happens to distinct-Ideoligion population groups at projection -
                // the axis' first behavioral consumer.
                string rights = CAFactionAxes.KeyOf(
                    CAFactionStateWorldComponent.Current
                        ?.Find(record.faction)?.factionStructure, CAFactionAxes.Dissent);

                Rand.PushState(seed);
                try
                {
                    int externalShare = record.populationGroups
                        .Where(item => IsProjectableOtherFaction(record, map,
                            item))
                        .Sum(item => item.share);
                    int residentShare = Math.Max(1, 100 - externalShare);
                    int projectedTotal = Math.Max(residents.Count,
                        Mathf.RoundToInt(residents.Count * 100f
                            / residentShare));
                    int cursor = 0;
                    foreach (CASettlementPopulationGroup populationGroup in record.populationGroups)
                    {
                        if (populationGroup == null) continue;
                        switch (populationGroup.kind)
                        {
                            case CAPopulationGroupKind.Main:
                                break; // the remainder, tagged at the end
                            case CAPopulationGroupKind.OtherFaction:
                                ProjectMinority(record, map, populationGroup,
                                    Math.Max(1, Mathf.RoundToInt(
                                        projectedTotal
                                        * populationGroup.share / 100f)));
                                // A demotion samples from within instead.
                                if (populationGroup.kind
                                    == CAPopulationGroupKind.LocalResidents)
                                    ProjectWithin(record, map, populationGroup,
                                        residents, ref cursor, rights,
                                        projectedTotal);
                                break;
                            case CAPopulationGroupKind.LocalResidents:
                            case CAPopulationGroupKind.Unaffiliated:
                                ProjectWithin(record, map, populationGroup,
                                    residents, ref cursor, rights,
                                    projectedTotal);
                                break;
                        }
                    }
                    // A hostile external affiliation becomes a distinct local
                    // group. Whatever remains is the dominant population group.
                    CASettlementPopulationGroup dominant = record.populationGroups
                        .FirstOrDefault(item => item != null && item.kind
                            == CAPopulationGroupKind.Main);
                    for (; cursor < residents.Count; cursor++)
                        Tag(record, dominant, residents[cursor]);
                }
                finally { Rand.PopState(); }

                Log.Message("[CA][Population] " + (record.name ?? "settlement")
                    + ": " + record.populationAssignments.Count
                    + " residents across " + record.populationGroups.Count
                    + " population groups (" + CASettlementComposition
                        .DescribePopulationGroups(record.populationGroups) + ")");
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Population] projection failed for "
                    + (record?.name ?? "?") + ": " + e.Message);
            }
        }

        // Residents belonging to another faction. Hostile faction pairs are
            // recorded as a local group with the settlement faction instead.
        private static void ProjectMinority(
            CARegionalSettlementRecord record, Map map,
            CASettlementPopulationGroup populationGroup, int count)
        {
            Faction minor = FactionForGroup(map, populationGroup.factionKey);
            if (!IsProjectableOtherFaction(record, map, populationGroup))
            {
                int formerFactionKey = populationGroup.factionKey;
                if (populationGroup.ideoligionFactionKey < 0)
                    populationGroup.ideoligionFactionKey = formerFactionKey;
                if (populationGroup.politicalBeliefsFactionKey < 0)
                    populationGroup.politicalBeliefsFactionKey = formerFactionKey;
                populationGroup.factionKey = record.factionKey;
                populationGroup.kind = CAPopulationGroupKind.LocalResidents;
                if (minor != null)
                    populationGroup.label += " local community";
                return;
            }
            var parms = new PawnGroupMakerParms
            {
                tile = map.Tile,
                faction = minor,
                groupKind = PawnGroupKindDefOf.Settlement,
                inhabitants = true,
                points = Mathf.Clamp(count * 160f, 240f, 5000f),
                seed = Gen.HashCombineInt(
                    GenText.StableStringHash(record.regionalId),
                    populationGroup.key * 271)
            };
            List<Pawn> spawned = PawnGroupMakerUtility
                .GeneratePawns(parms, false).Take(count).ToList();
            if (spawned.Count == 0) return;
            Lord lord = LordMaker.MakeNewLord(minor,
                new LordJob_CARegionalSettlement(minor,
                    record.localRect.CenterCell), map);
            foreach (Pawn pawn in spawned)
            {
                IntVec3 cell;
                if (!CellFinder.TryFindRandomCellNear(
                        record.localRect.CenterCell, map, 14,
                        c => c.Standable(map), out cell))
                    cell = CellFinder.RandomClosewalkCellNear(
                        record.localRect.CenterCell, map, 18);
                GenSpawn.Spawn(pawn, cell, map);
                lord.AddPawn(pawn);
                if (populationGroup.independentIdeoligionKey >= 0
                    || populationGroup.ideoligionFactionKey >= 0)
                {
                    Ideo ideoligion = populationGroup.independentIdeoligionKey >= 0
                        ? IndependentIdeoligionFor(record, populationGroup)
                        : FactionForGroup(map, populationGroup.ideoligionFactionKey)
                            ?.ideos?.PrimaryIdeo;
                    if (ideoligion != null && pawn.ideo != null
                        && pawn.Ideo != ideoligion)
                    {
                        pawn.ideo.SetIdeo(ideoligion);
                        RegisterMinorIdeo(minor, ideoligion);
                    }
                }
                Tag(record, populationGroup, pawn);
                NudgeCertainty(pawn, populationGroup);
            }
        }

        // A population group within the dominant faction may keep a different
        // Ideoligion. Plural rule registers it as a minor Ideoligion;
        // majoritarian rule permits it without faction support; customary rule
        // registers established quarters; orthodoxy suppresses unquartered
        // groups and records that suppression. A quartered group keeps its
        // Ideoligion.
        private static void ProjectWithin(
            CARegionalSettlementRecord record, Map map,
            CASettlementPopulationGroup populationGroup, List<Pawn> residents,
            ref int cursor, string rights, int projectedTotal)
        {
            int count = Mathf.RoundToInt(projectedTotal
                * populationGroup.share / 100f);
            if (populationGroup.share > 0 && count == 0) count = 1;
            int ideoligionFactionKey = populationGroup.ideoligionFactionKey >= 0
                ? populationGroup.ideoligionFactionKey : populationGroup.factionKey;
            Ideo target = populationGroup.independentIdeoligionKey >= 0
                ? IndependentIdeoligionFor(record, populationGroup)
                : populationGroup.kind == CAPopulationGroupKind.Unaffiliated
                    && populationGroup.ideoligionFactionKey < 0 ? null
                : FactionForGroup(map, ideoligionFactionKey)?.ideos?.PrimaryIdeo;
            Ideo dominant = record.faction.ideos?.PrimaryIdeo;
            bool suppress = rights == "orthodoxy" && !populationGroup.quarter
                && target != null && dominant != null
                && target != dominant;
            int suppressed = 0;
            for (int i = 0; i < count && cursor < residents.Count;
                i++, cursor++)
            {
                Pawn pawn = residents[cursor];
                if (suppress)
                {
                    // Enforced conformity assigns the dominant Ideoligion
                    // with low certainty.
                    if (pawn.ideo != null && pawn.Ideo != dominant)
                        pawn.ideo.SetIdeo(dominant);
                    try
                    {
                        if (pawn.ideo != null)
                            pawn.ideo.OffsetCertainty(
                                0.35f - pawn.ideo.Certainty);
                    }
                    catch { }
                    suppressed++;
                    Tag(record, populationGroup, pawn);
                    continue;
                }
                if (target != null && pawn.ideo != null
                    && pawn.Ideo != target)
                {
                    pawn.ideo.SetIdeo(target);
                    if (rights == null || rights == "plural"
                        || (rights == "customary" && populationGroup.quarter)
                        || (rights == "orthodoxy" && populationGroup.quarter))
                        RegisterMinorIdeo(record.faction, target);
                }
                Tag(record, populationGroup, pawn);
                NudgeCertainty(pawn, populationGroup);
            }
            if (suppressed > 0
                && !populationGroup.label.NullOrEmpty()
                && !populationGroup.label.Contains("suppressed"))
            {
                populationGroup.label += " (Ideoligion suppressed)";
                Log.Message("[CA][Rights] " + (record.name ?? "settlement")
                    + ": " + suppressed + " members of a population group "
                    + "were assigned the faction Ideoligion under enforced "
                    + "orthodoxy");
            }
        }

        private static void Tag(CARegionalSettlementRecord record,
            CASettlementPopulationGroup populationGroup, Pawn pawn)
        {
            if (record == null || populationGroup == null || pawn == null) return;
            record.populationAssignments.Add(populationGroup.key + ":"
                + pawn.thingIDNumber);
        }

        // Population cohesion sets the native Ideoligion certainty value.
        private static void NudgeCertainty(Pawn pawn,
            CASettlementPopulationGroup populationGroup)
        {
            try
            {
                if (pawn?.ideo == null || pawn.Ideo == null) return;
                pawn.ideo.OffsetCertainty(
                    populationGroup.CertaintyTarget - pawn.ideo.Certainty);
            }
            catch { }
        }

        // Creates an Ideoligion for an unaffiliated population group. Region
        // and group keys make generation deterministic, and later settlements
        // reuse the existing Ideoligion by name.
        private static Ideo IndependentIdeoligionFor(
            CARegionalSettlementRecord record, CASettlementPopulationGroup populationGroup)
        {
            try
            {
                if (record?.faction?.def == null) return null;
                int seed = Gen.HashCombineInt(GenText.StableStringHash(
                    (record.regionalId ?? "r") + ":independent-ideoligion"),
                    populationGroup.independentIdeoligionKey * 613);
                Rand.PushState(seed);
                try
                {
                    Ideo minted = IdeoGenerator.GenerateIdeo(
                        new IdeoGenerationParms(record.faction.def));
                    if (minted == null) return null;
                    Ideo existing = Find.IdeoManager.IdeosListForReading
                        .FirstOrDefault(i => i != null && i != minted
                            && i.name == minted.name);
                    if (existing != null) return existing;
                    Find.IdeoManager.Add(minted);
                    Log.Message("[CA][Ideoligion] minted independent "
                        + "ideoligion '" + minted.name + "' for a "
                        + "community of "
                        + (record.name ?? "a settlement")
                        + " - no faction affiliation");
                    return minted;
                }
                finally { Rand.PopState(); }
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Ideoligion] independent Ideoligion generation failed: "
                    + e.Message);
                return null;
            }
        }

        // The engine's own concept for "this faction contains followers
        // of that ideoligion" - vanilla generates minor-ideo believers at
        // a 4:1 weight from exactly this list.
        private static void RegisterMinorIdeo(Faction faction, Ideo ideo)
        {
            try
            {
                if (faction?.ideos == null || ideo == null) return;
                if (faction.ideos.PrimaryIdeo == ideo) return;
                List<Ideo> minors = faction.ideos.IdeosMinorListForReading;
                if (minors != null && !minors.Contains(ideo)
                    && minors.Count < 4)
                    minors.Add(ideo);
            }
            catch { }
        }

        internal static string AssignedPopulationGroupOf(
            CARegionalSettlementRecord record, Pawn pawn)
        {
            if (record?.populationAssignments == null || pawn == null)
                return null;
            string suffix = ":" + pawn.thingIDNumber;
            for (int i = 0; i < record.populationAssignments.Count; i++)
                if (record.populationAssignments[i].EndsWith(suffix))
                    return record.populationAssignments[i].Split(':')[0];
            return null;
        }

        // Current residents are live humanlike pawns in the settlement area.
        // Population assignments admit authored minority factions; owner-
        // faction pawns in the same area cover births and later arrivals.
        internal static List<Pawn> Residents(CARegionalSettlementRecord record,
            Map map)
        {
            var result = new List<Pawn>();
            if (record == null || map?.mapPawns == null) return result;
            var assigned = new HashSet<int>();
            foreach (string entry in record.populationAssignments
                ?? new List<string>())
            {
                string[] parts = entry.Split(':');
                int pawnId;
                if (parts.Length == 2 && int.TryParse(parts[1], out pawnId))
                    assigned.Add(pawnId);
            }
            CellRect reach = record.localRect == CellRect.Empty
                ? CellRect.Empty : record.localRect.ExpandedBy(12);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn.Dead || pawn.RaceProps == null
                    || !pawn.RaceProps.Humanlike) continue;
                if (reach != CellRect.Empty && !reach.Contains(pawn.Position))
                    continue;
                if (pawn.Faction == record.faction
                    || assigned.Contains(pawn.thingIDNumber))
                    result.Add(pawn);
            }
            return result.OrderBy(pawn => pawn.thingIDNumber).ToList();
        }

        internal static List<Pawn> ResidentsInPopulationGroup(
            CARegionalSettlementRecord record, Map map, int populationGroupKey)
        {
            var ids = new HashSet<int>();
            foreach (string entry in record?.populationAssignments
                ?? new List<string>())
            {
                string[] parts = entry.Split(':');
                int key, pawnId;
                if (parts.Length == 2 && int.TryParse(parts[0], out key)
                    && key == populationGroupKey
                    && int.TryParse(parts[1], out pawnId)) ids.Add(pawnId);
            }
            return Residents(record, map)
                .Where(pawn => ids.Contains(pawn.thingIDNumber)).ToList();
        }

        private static bool IsProjectableOtherFaction(
            CARegionalSettlementRecord record, Map map,
            CASettlementPopulationGroup populationGroup)
        {
            if (populationGroup == null || populationGroup.kind
                != CAPopulationGroupKind.OtherFaction) return false;
            Faction minor = FactionForGroup(map, populationGroup.factionKey);
            return minor != null && minor != record.faction
                && !minor.HostileTo(record.faction);
        }

        internal static Faction FactionForGroup(Map map, int factionKey)
        {
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            CARegionalFactionPlan group = plan?.FactionPlan(factionKey);
            if (group == null) return null;
            Faction resolved = group.resolvedFaction;
            if (resolved != null) return resolved;
            if (group.source
                == CARegionalFactionSource.ExistingWorldFaction)
                return CARegionalPlanUtility.FactionByLoadId(
                    group.existingFactionLoadId);
            return null;
        }
    }

    // Creates kitchens and dining areas for starting provisions. Each node is
    // owned by the settlement faction and registered to its operator. Funding
    // controls taxation, distribution controls node count, and water access
    // controls whether a kitchen begins degraded.
    internal static class CAStartingProvisions
    {
        internal const string TaxRateSource =
            "starting-provisions:tax-rate";
        private const string TaxRelationOriginPrefix =
            "starting-provisions:tax:";

        internal static string TaxRelationOrigin(string providerKey)
        {
            return TaxRelationOriginPrefix + providerKey;
        }

        // Keeps provider tax links current without rebuilding kitchens or
        // creating settlement organizations ahead of their normal seed path.
        internal static void ReconcileTaxFunding(
            CARegionalSettlementRecord record)
        {
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            if (record?.startingProvisions == null || orgs == null) return;
            foreach (CAStartingProvision arrangement in
                record.startingProvisions)
            {
                if (arrangement == null || arrangement.operatorKind
                    == CAProvisionOperator.Household) continue;
                CAOrganization provider = orgs.ByKey(record.regionalId + "#"
                    + record.slot + ":prov" + arrangement.key);
                if (provider != null)
                    ReconcileTaxFunding(record, arrangement, provider);
            }
        }

        // A refused starting tax rate is a saved decision. Removing the
        // generated link through the normal ledger path records its removal,
        // so later reconciliation cannot silently restore it.
        internal static void RefuseTaxFunding(string providerKey)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null || providerKey.NullOrEmpty()) return;
            string origin = TaxRelationOrigin(providerKey);
            List<CARelation> links = ledger.RelationsIn(providerKey)
                .Where(relation => relation != null
                    && relation.origin.Replaceable
                    && relation.origin.originKey == origin).ToList();
            foreach (CARelation link in links)
                ledger.Remove(link);
        }

        internal static void Furnish(CARegionalSettlementRecord record,
            Map map, int tier, Func<Room> next, Func<Room> nextFar,
            Func<Room, string, string, string, int> place,
            Func<Room, string, int, string, int> stock)
        {
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            if (orgs == null || record.startingProvisions == null) return;
            bool water = WaterNear(record, map);

            foreach (CAStartingProvision arrangement in
                record.startingProvisions)
            {
                if (arrangement == null) continue;
                arrangement.waterSecured = water;
                if (arrangement.operatorKind
                    == CAProvisionOperator.Household)
                    continue; // household hearths are not an organization

                string orgKey = record.regionalId + "#" + record.slot
                    + ":prov" + arrangement.key;
                CASettlementPopulationGroup populationGroup = arrangement.populationGroupKey < 0
                    ? null : record.populationGroups.FirstOrDefault(item =>
                        item != null && item.key == arrangement.populationGroupKey);
                string orgName = (populationGroup != null
                        ? populationGroup.label + " " : "")
                    + arrangement.OperatorWords + " of "
                    + (record.name ?? "the settlement");
                CAOrganization op = orgs.EnsureFor(orgKey, orgName,
                    "established with the settlement; "
                    + arrangement.access.ToString().ToLower()
                    + " access, funded by " + arrangement.FundingWords,
                    CAOrganizationKind.Group);
                ReconcileTaxFunding(record, arrangement, op);

                // Node count is the arrangement's own derived fact -
                // metropolitan neighborhood networks raise more kitchens
                // than hamlets - and a region-reach reserve carries deeper
                // stores, because it feeds more than this settlement.
                int nodes = Mathf.Clamp(arrangement.nodes, 1, 3);
                int stockPer = ((arrangement.operatorKind
                        == CAProvisionOperator.Authority ? 60 : 40)
                    + (arrangement.reach == CAProvisionReach.Region
                        ? 40 : 0)) / nodes;
                if (!water) stockPer /= 2;
                // A quartered population group's provision stands in its own part
                // of the settlement - rooms drawn from the far end of the
                // room order, spatially apart from the front-cursor rooms
                // the settlement's own organizations occupy.
                bool inQuarter = populationGroup != null && populationGroup.quarter;
                Func<Room> roomSource = inQuarter ? nextFar : next;
                int laid = 0;
                for (int n = 0; n < nodes; n++)
                {
                    Room room = roomSource();
                    laid += place(room, tier == 0 ? "Campfire"
                        : "FueledStove", null, orgKey);
                    if (tier > 0)
                        laid += place(room, "TableButcher", "WoodLog",
                            orgKey);
                    laid += place(room, "Table2x2c", "WoodLog", orgKey);
                    laid += place(room, tier >= 2 ? "DiningChair"
                        : "Stool", "WoodLog", orgKey);
                    laid += stock(room, "Pemmican", stockPer, orgKey);
                }
                if (laid > 0)
                {
                    op.Record("starting provisions", arrangement.Summary
                        + " - " + nodes + " node"
                        + (nodes == 1 ? "" : "s") + " raised"
                        + (inQuarter ? " in its own quarter" : "") + ", "
                        + laid + " assets");
                    if (!water)
                        op.Record("starting provisions", "no secured water "
                            + "within reach - the kitchens run on half "
                            + "stores until a source is secured");
                }
            }
        }

        // Tax-funded support has both halves of the taxation loop: the
        // provider's policy and a relation naming the settlement treasury
        // that pays it. Either half alone is only descriptive state.
        private static void ReconcileTaxFunding(
            CARegionalSettlementRecord record,
            CAStartingProvision arrangement, CAOrganization provider)
        {
            string relationOrigin = TaxRelationOrigin(
                provider.organizationKey);
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (arrangement.funding != CAProvisionFunding.Taxation)
            {
                ledger?.ClearDerived(relationOrigin);
                provider.policies.RemoveAll(policy => policy != null
                    && policy.key == "tax rate"
                    && policy.generatedBy == TaxRateSource);
                return;
            }

            string payerKey = record.regionalId + "#" + record.slot;
            int now = Find.TickManager?.TicksGame ?? 0;
            bool linked = ledger != null && ledger
                .RelationsIn(provider.organizationKey).Any(relation =>
                    relation != null && !relation.Expired(now)
                    && relation.IsOrganizationParty
                    && relation.OrganizationPartyKey == payerKey
                    && relation.Delegates(CAResponsibilities.Taxes));
            if (!linked && ledger != null)
            {
                ledger.ClearDerived(relationOrigin);
                var relation = new CARelation
                {
                    orgKey = provider.organizationKey,
                    role = "support contributor",
                    status = "settlement",
                    startTick = now,
                    origin = CAOrigin.Derived(relationOrigin),
                    delegatedResponsibilities = new List<string>
                        { CAResponsibilities.Taxes }
                };
                relation.Party = CARelationPartyRef.OfOrganization(payerKey);
                linked = ledger.Add(relation);
            }
            if (!linked)
            {
                provider.policies.RemoveAll(policy => policy != null
                    && policy.key == "tax rate"
                    && policy.generatedBy == TaxRateSource);
                return;
            }

            CAPolicyRecord taxRate = provider.policies.FirstOrDefault(
                policy => policy != null && policy.key == "tax rate");
            if (taxRate == null)
            {
                provider.policies.Add(new CAPolicyRecord
                {
                    key = "tax rate",
                    value = "light",
                    adoptedTick = Find.TickManager.TicksGame,
                    generatedBy = TaxRateSource
                });
            }
        }

        // Water is secured when water terrain exists within reach of the
        // settlement. Plumbing is not inferred.
        private static bool WaterNear(CARegionalSettlementRecord record,
            Map map)
        {
            CellRect scan = record.localRect.ExpandedBy(16)
                .ClipInsideMap(map);
            for (int x = scan.minX; x <= scan.maxX; x += 4)
                for (int z = scan.minZ; z <= scan.maxZ; z += 4)
                {
                    TerrainDef terrain =
                        new IntVec3(x, 0, z).GetTerrain(map);
                    if (terrain != null && terrain.IsWater) return true;
                }
            return false;
        }
    }
}
