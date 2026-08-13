using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Regional settlement pattern, settlement scale, faction authority, and
    // local facilities are separate saved facts. World tendencies generate
    // them but do not replace the realized result.

    // The realized spatial structure of one region's settlement network.
    public enum CASettlementPattern : byte
    {
        Unsettled,
        Isolated,          // one settlement, far from the others
        Dispersed,         // settlements spread with no dominant center
        Corridor,          // strung along a road or river line
        Clustered,         // grouped, no single dominant centre
        DominantCenter,    // monocentric: one centre, satellites
        ComparableCenters, // polycentric: several comparable centres
        FrontierHinterland,
        ContestedMixed     // politically mixed geography formed through conflict
    }

    // Realized scale of a settlement or the region's principal settlement.
    public enum CASettlementScale : byte
    {
        DispersedSettlement,
        HamletNetwork,
        LocalCenter,
        RegionalCenter,
        UrbanCenter,
        LargeUrbanRegion
    }

    // How a faction divides authority between its settlements.
    public enum CASettlementAuthority : byte
    {
        Central = 0,
        CentralWithLocalRule = 1,
        Shared = 2,
        IndependentWithSharedDefense = 3,
        Independent = 4
    }

    // A settlement's position within the realized regional hierarchy.
    public enum CASettlementRole : byte
    {
        Standalone,
        Center,
        Satellite,
        FrontierEdge
    }

    // Regional political condition derived once from persisted faction
    // relations. Spatial classification and faction generation consume this
    // saved fact; neither rerolls the relation tendency.
    public enum CARegionalRelationPattern : byte
    {
        Peaceful,
        Rival,
        Contested
    }

    public class CASettlementPatternDef : Def { }

    public class CASettlementScaleDef : Def { }

    public class CASettlementAuthorityDef : Def
    {
        public List<string> sharedResponsibilities;
    }

    internal static class CARegionalSettlements
    {
        internal static readonly CASettlementAuthority[]
            ActiveSettlementAuthorities =
        {
            CASettlementAuthority.Central,
            CASettlementAuthority.CentralWithLocalRule,
            CASettlementAuthority.Shared,
            CASettlementAuthority.IndependentWithSharedDefense,
            CASettlementAuthority.Independent
        };

        internal static CASettlementAuthority NormalizeAuthority(
            CASettlementAuthority authority)
        {
            return ActiveSettlementAuthorities.Contains(authority)
                ? authority : CASettlementAuthority.Central;
        }

        // ---- def-backed lookups -----------------------------------------

        private static string PatternLabel(string defName, string fallback)
        {
            CASettlementPatternDef def = DefDatabase<CASettlementPatternDef>
                .GetNamedSilentFail(defName);
            return def != null && !def.label.NullOrEmpty()
                ? def.label : fallback;
        }

        private static string ScaleLabel(string defName, string fallback)
        {
            CASettlementScaleDef def = DefDatabase<CASettlementScaleDef>
                .GetNamedSilentFail(defName);
            return def != null && !def.label.NullOrEmpty()
                ? def.label : fallback;
        }

        private static string AuthorityLabel(string defName, string fallback)
        {
            CASettlementAuthorityDef def =
                DefDatabase<CASettlementAuthorityDef>
                    .GetNamedSilentFail(defName);
            return def != null && !def.label.NullOrEmpty()
                ? def.label : fallback;
        }

        // ---- words ------------------------------------------------------

        internal static string PatternWords(CASettlementPattern t)
        {
            return PatternLabel("CA_Pattern_" + t, PatternFallback(t));
        }

        private static string PatternFallback(CASettlementPattern t)
        {
            switch (t)
            {
                case CASettlementPattern.Isolated: return "isolated";
                case CASettlementPattern.Dispersed: return "dispersed";
                case CASettlementPattern.Corridor: return "corridor";
                case CASettlementPattern.Clustered: return "clustered";
                case CASettlementPattern.DominantCenter:
                    return "one main settlement";
                case CASettlementPattern.ComparableCenters:
                    return "several main settlements";
                case CASettlementPattern.FrontierHinterland:
                    return "frontier";
                case CASettlementPattern.ContestedMixed:
                    return "contested";
                default: return "unsettled";
            }
        }

        internal static string ScaleWords(CASettlementScale band)
        {
            return ScaleLabel("CA_Scale_" + band, ScaleFallback(band));
        }

        private static string ScaleFallback(CASettlementScale band)
        {
            switch (band)
            {
                case CASettlementScale.HamletNetwork: return "hamlet network";
                case CASettlementScale.LocalCenter: return "local center";
                case CASettlementScale.RegionalCenter: return "regional center";
                case CASettlementScale.UrbanCenter: return "urban center";
                case CASettlementScale.LargeUrbanRegion:
                    return "large urban region";
                default: return "dispersed settlement";
            }
        }

        internal static string SettlementAuthorityWords(
            CASettlementAuthority c)
        {
            c = NormalizeAuthority(c);
            return AuthorityLabel("CA_Authority_" + c,
                SettlementAuthorityFallback(c));
        }

        private static string SettlementAuthorityFallback(
            CASettlementAuthority c)
        {
            switch (c)
            {
                case CASettlementAuthority.CentralWithLocalRule:
                    return "central rule with local authority";
                case CASettlementAuthority.Shared:
                    return "shared central and local authority";
                case CASettlementAuthority.IndependentWithSharedDefense:
                    return "independent settlements, shared defense";
                case CASettlementAuthority.Independent:
                    return "independent settlements";
                default: return "central authority";
            }
        }

        internal static string RoleWords(CASettlementRole role)
        {
            switch (role)
            {
                case CASettlementRole.Center: return "regional seat";
                case CASettlementRole.Satellite: return "satellite";
                case CASettlementRole.FrontierEdge: return "frontier edge";
                default: return "standalone";
            }
        }

        // ---- B: the realized regional pattern ---------------------------

        // Authored settings are preserved. Generated settings are saved on the
        // plan so confirmation carries exactly what the preview showed.
        internal static void EnsureSettlementPattern(CARegionalPlan plan)
        {
            if (plan == null) return;
            string validationFailure = null;
            if (plan.settlementRealizationComplete
                && TryValidateRealization(plan, out validationFailure)) return;
            if (plan.confirmed)
            {
                Log.Error("[CA][WorldTendencies] confirmed plan "
                    + (plan.candidateId ?? "unknown")
                    + " has invalid realized settlement state ("
                    + (validationFailure ?? "unknown failure") + "); "
                    + "generation will not reroll its tendencies.");
                return;
            }
            DeriveSettlementPattern(plan);
        }

        // Confirmation is the last authoring boundary. It may replace stale
        // preview results, then the confirmed plan becomes read-only input to
        // generation.
        internal static void RealizeForConfirmation(CARegionalPlan plan)
        {
            if (plan == null) return;
            DeriveSettlementPattern(plan);
        }

        internal static void Invalidate(CARegionalPlan plan)
        {
            if (plan == null) return;
            plan.settlementRealizationComplete = false;
            plan.settlementRealizationSourceHash = 0;
        }

        // A completion flag is not evidence by itself. Confirmed plans are
        // read-only inputs, so malformed or partial realized state is rejected
        // instead of being silently regenerated during map generation.
        internal static bool TryValidateRealization(CARegionalPlan plan,
            out string failure)
        {
            failure = null;
            if (plan == null)
            {
                failure = "the regional plan is missing";
                return false;
            }
            if (!plan.settlementRealizationComplete)
            {
                failure = "settlement realization is incomplete";
                return false;
            }
            if (plan.worldPolicy == null)
            {
                failure = "the realized plan has no world-policy snapshot";
                return false;
            }
            if (plan.settlementRealizationSourceHash
                != RealizationSourceHash(plan))
            {
                failure = "the realized facts do not match their saved causes";
                return false;
            }

            List<CARegionalSettlementPlan> settlements = (plan.settlements
                    ?? new List<CARegionalSettlementPlan>())
                .Where(item => item != null).OrderBy(item => item.slot)
                .ToList();
            if (settlements.Count != (plan.settlements?.Count ?? 0))
            {
                failure = "the settlement realization contains a null row";
                return false;
            }
            foreach (CARegionalSettlementPlan settlement in settlements)
            {
                if (settlement.residentPopulation < 18
                    || settlement.landCapacity < 1
                    || settlement.landCapacity > 3
                    || settlement.realizedAccessInfrastructure < 0
                    || settlement.realizedAccessInfrastructure > 3
                    || settlement.realizedServiceInfrastructure < 0
                    || settlement.realizedServiceInfrastructure > 3
                    || settlement.realizedCivicInfrastructure < 0
                    || settlement.realizedCivicInfrastructure > 3
                    || settlement.economicCapacity < 0
                    || settlement.economicCapacity > 3
                    || settlement.tradeConnectivity < 0
                    || settlement.tradeConnectivity > 3
                    || settlement.specialization < 0
                    || settlement.specialization > 3
                    || settlement.historicalDevelopment < 0
                    || settlement.historicalDevelopment > 3
                    || settlement.realizedRole > (byte)
                        CASettlementRole.FrontierEdge
                    || settlement.realizedScale > (byte)
                        CASettlementScale.LargeUrbanRegion)
                {
                    failure = "settlement " + settlement.slot
                        + " has an incomplete or out-of-range realized fact";
                    return false;
                }
                int expectedLand = LandCapacity(settlement.memberTileId);
                int expectedHistory = HistoricalDevelopment(settlement);
                int expectedPopulation = CAWorldTendencyCausalKernel
                    .PopulationFromFacts(expectedLand, expectedHistory,
                        TechTier(plan.FactionPlan(settlement.factionKey)),
                        settlement.populationOrigin
                            == CASettlementOrigin.ScenarioOverride);
                int expectedAccess = CASettlementStartingState
                    .ExpectedAccess(settlement);
                int expectedServices = CASettlementStartingState
                    .ExpectedServices(plan, settlement);
                int expectedCivic = CASettlementStartingState
                    .ExpectedCivic(plan, settlement);
                int expectedSpecialization = Specialization(plan, settlement);
                int expectedEconomy = EconomicCapacity(plan, settlement);
                int expectedTrade = TradeConnectivity(plan, settlement);
                int expectedSupport = CAWorldTendencyCausalKernel.UrbanSupport(
                    expectedPopulation, expectedLand, expectedAccess,
                    expectedServices, expectedCivic, expectedEconomy,
                    expectedTrade, expectedSpecialization,
                    (CASettlementRole)settlement.realizedRole
                        == CASettlementRole.Center,
                    expectedHistory);
                int expectedScale = CAWorldTendencyCausalKernel
                    .SettlementScale(expectedPopulation,
                        expectedSupport,
                        plan.worldPolicy.urbanGrowthPropensity);
                if (settlement.landCapacity != expectedLand
                    || settlement.historicalDevelopment != expectedHistory
                    || settlement.residentPopulation != expectedPopulation
                    || settlement.realizedAccessInfrastructure
                        != expectedAccess
                    || settlement.realizedServiceInfrastructure
                        != expectedServices
                    || settlement.realizedCivicInfrastructure != expectedCivic
                    || settlement.specialization != expectedSpecialization
                    || settlement.economicCapacity != expectedEconomy
                    || settlement.tradeConnectivity != expectedTrade
                    || settlement.urbanSupport != expectedSupport
                    || settlement.realizedScale != expectedScale)
                {
                    failure = "settlement " + settlement.slot
                        + " has a realized read model inconsistent with its "
                        + "saved causes";
                    return false;
                }
                string savedProgramSignature = settlement
                    .settlementProgram?.sourceSignature;
                if (savedProgramSignature.NullOrEmpty())
                {
                    failure = "settlement " + settlement.slot
                        + " has no realized settlement program";
                    return false;
                }
                if (!CASettlementProgramRegistry.TryValidateSaved(plan,
                        settlement, out string programFailure))
                {
                    failure = "settlement " + settlement.slot
                        + " has a program inconsistent with its saved facts: "
                        + programFailure;
                    return false;
                }
            }
            if ((settlements.Count == 1
                    && settlements[0].realizedRole
                        != (byte)CASettlementRole.Standalone)
                || (settlements.Count > 1
                && (settlements.Count(item => item.realizedRole
                        == (byte)CASettlementRole.Center) != 1
                    || settlements.Any(item => item.realizedRole
                        != (byte)CASettlementRole.Center
                        && item.realizedRole
                            != (byte)CASettlementRole.Satellite))))
            {
                failure = "the realized settlement hierarchy is incomplete";
                return false;
            }
            if (settlements.Count > 1)
            {
                CARegionalSettlementPlan expectedCenter = settlements
                    .OrderByDescending(item => item.residentPopulation)
                    .ThenByDescending(item => item.landCapacity)
                    .ThenBy(item => item.slot).First();
                if (expectedCenter.realizedRole
                    != (byte)CASettlementRole.Center)
                {
                    failure = "the realized settlement center does not match "
                        + "population and land capacity";
                    return false;
                }
            }
            byte expectedScaleMaximum = settlements.Count == 0 ? (byte)0
                : settlements.Max(item => item.realizedScale);
            if (plan.settlementScale != expectedScaleMaximum)
            {
                failure = "the regional scale does not match its settlements";
                return false;
            }

            List<int> factionKeys = (plan.factions
                    ?? new List<CARegionalFactionPlan>())
                .Where(item => item != null).Select(item => item.key)
                .Distinct().OrderBy(key => key).ToList();
            int expectedPairs = factionKeys.Count * (factionKeys.Count - 1)
                / 2;
            if (plan.relations == null || plan.relations.Count != expectedPairs)
            {
                failure = "the realized faction relation table is incomplete";
                return false;
            }
            for (int i = 0; i < factionKeys.Count; i++)
                for (int j = i + 1; j < factionKeys.Count; j++)
                {
                    CARegionalRelationPlan relation = plan.RelationPlanBetween(
                        factionKeys[i], factionKeys[j]);
                    if (relation == null || !Enum.IsDefined(
                            typeof(FactionRelationKind), relation.relation))
                    {
                        failure = "the realized relation for faction pair "
                            + factionKeys[i] + "-" + factionKeys[j]
                            + " is missing or invalid";
                        return false;
                    }
                    CARegionalFactionPlan left = plan.FactionPlan(
                        factionKeys[i]);
                    CARegionalFactionPlan right = plan.FactionPlan(
                        factionKeys[j]);
                    bool nativePair = left?.source
                            == CARegionalFactionSource.ExistingWorldFaction
                        && right?.source
                            == CARegionalFactionSource.ExistingWorldFaction;
                    if (!nativePair && !relation.authorRelation)
                    {
                        failure = "the realized relation for faction pair "
                            + factionKeys[i] + "-" + factionKeys[j]
                            + " has no native or authored source";
                        return false;
                    }
                }

            int hostilePairs = 0;
            int neutralPairs = 0;
            for (int i = 0; i < factionKeys.Count; i++)
                for (int j = i + 1; j < factionKeys.Count; j++)
                {
                    FactionRelationKind relation = plan.RelationBetween(
                        factionKeys[i], factionKeys[j]);
                    if (relation == FactionRelationKind.Hostile) hostilePairs++;
                    else if (relation == FactionRelationKind.Neutral)
                        neutralPairs++;
                }
            int expectedRelationPattern = CAWorldTendencyCausalKernel
                .RelationPattern(factionKeys.Count, hostilePairs,
                    neutralPairs);
            if (plan.regionalRelationPattern != expectedRelationPattern)
            {
                failure = "the regional relation pattern does not match its "
                    + "saved faction relations";
                return false;
            }

            int seed = GenText.StableStringHash((plan.candidateId ?? "ca")
                + ":settlement-realization");
            List<CAFrontierHoldingPlan> expectedHoldings =
                BuildFrontierHoldings(plan, plan.worldPolicy, seed);
            List<CAFrontierHoldingPlan> holdings = plan.frontierHoldings
                ?? new List<CAFrontierHoldingPlan>();
            if (holdings.Count != expectedHoldings.Count)
            {
                failure = "the frontier holding count does not match its "
                    + "saved causes";
                return false;
            }
            for (int i = 0; i < holdings.Count; i++)
            {
                CAFrontierHoldingPlan saved = holdings[i];
                if (saved == null || saved.key != i
                    || !(plan.memberTileIds?.Contains(saved.memberTileId)
                        ?? false)
                    || saved.memberTileId == plan.startTileId
                    || settlements.Any(item => item.memberTileId
                        == saved.memberTileId)
                    || holdings.Take(i).Any(item => item != null
                        && item.memberTileId == saved.memberTileId)
                    || saved.landCapacity < 1 || saved.landCapacity > 3
                    || saved.residentCount != CAWorldTendencyCausalKernel
                        .FrontierResidentCount(saved.landCapacity,
                            plan.worldPolicy.frontierHoldingSize)
                    || saved.materialLevel != CAWorldTendencyCausalKernel
                        .FrontierMaterialLevel(saved.landCapacity,
                            plan.worldPolicy.frontierHoldingSize)
                    || saved.form != CAWorldTendencyCausalKernel.FrontierForm(
                        saved.residentCount, saved.materialLevel)
                    || !saved.factionless)
                {
                    failure = "frontier holding " + i
                        + " is incomplete or inconsistent with its saved causes";
                    return false;
                }
            }

            List<IGrouping<int, CARegionalSettlementPlan>> byTile = settlements
                .GroupBy(item => item.memberTileId).ToList();
            int occupiedTiles = byTile.Count;
            int largestTileGroup = byTile.Count == 0 ? 0
                : byTile.Max(group => group.Count());
            int routeEdges = RouteEdges(byTile.Select(group => group.Key));
            float averageDistance = AverageDistance(settlements);
            List<CARegionalSettlementPlan> byPopulation = settlements
                .OrderByDescending(item => item.residentPopulation).ToList();
            bool dominant = byPopulation.Count > 1
                && byPopulation[0].residentPopulation
                    >= byPopulation[1].residentPopulation * 1.35f;
            int settlementFactionCount = settlements
                .Select(item => item.factionKey).Distinct().Count();
            int expectedPattern = CAWorldTendencyCausalKernel
                .SettlementPattern(settlements.Count, occupiedTiles,
                    largestTileGroup, routeEdges, averageDistance, dominant,
                    settlementFactionCount, expectedRelationPattern,
                    holdings.Count);
            if (plan.settlementPattern != expectedPattern)
            {
                failure = "the settlement pattern does not match its saved "
                    + "placement and relations";
                return false;
            }
            return true;
        }

        internal static void DeriveSettlementPattern(CARegionalPlan plan)
        {
            var settlements = (plan.settlements
                    ?? new List<CARegionalSettlementPlan>())
                .Where(item => item != null).OrderBy(item => item.slot)
                .ToList();
            CARegionalWorldPolicy policy = plan.worldPolicy
                ?? new CARegionalWorldPolicy();
            int seed = GenText.StableStringHash((plan.candidateId ?? "ca")
                + ":settlement-realization");

            // Population source and ground exist before urban scale. History
            // records only the established boundary and explicit evidence;
            // no origin category or hash invents additional development.
            foreach (CARegionalSettlementPlan settlement in settlements)
            {
                settlement.realizedAccessInfrastructure = -1;
                settlement.realizedServiceInfrastructure = -1;
                settlement.realizedCivicInfrastructure = -1;
                settlement.economicCapacity = -1;
                settlement.tradeConnectivity = -1;
                settlement.specialization = -1;
                settlement.urbanSupport = -1;
                settlement.landCapacity = LandCapacity(
                    settlement.memberTileId);
                settlement.historicalDevelopment = HistoricalDevelopment(
                    settlement);
                CACultureHistory.EnsureSettlementCulture(plan, settlement);
                int tier = TechTier(plan.FactionPlan(settlement.factionKey));
                settlement.residentPopulation =
                    CAWorldTendencyCausalKernel.PopulationFromFacts(
                        settlement.landCapacity,
                        settlement.historicalDevelopment, tier,
                        settlement.populationOrigin
                            == CASettlementOrigin.ScenarioOverride);
            }

            CARegionalSettlementPlan center = settlements
                .OrderByDescending(item => item.residentPopulation)
                .ThenByDescending(item => item.landCapacity)
                .ThenBy(item => item.slot).FirstOrDefault();
            foreach (CARegionalSettlementPlan settlement in settlements)
                settlement.realizedRole = (byte)(settlements.Count == 1
                    ? CASettlementRole.Standalone
                    : settlement == center ? CASettlementRole.Center
                    : CASettlementRole.Satellite);

            RealizeRelations(plan);
            List<int> factionKeys = (plan.factions
                    ?? new List<CARegionalFactionPlan>())
                .Where(item => item != null).Select(item => item.key)
                .Distinct().OrderBy(key => key).ToList();
            int hostilePairs = 0;
            int neutralPairs = 0;
            for (int i = 0; i < factionKeys.Count; i++)
                for (int j = i + 1; j < factionKeys.Count; j++)
                {
                    FactionRelationKind relation = plan.RelationBetween(
                        factionKeys[i], factionKeys[j]);
                    if (relation == FactionRelationKind.Hostile) hostilePairs++;
                    else if (relation == FactionRelationKind.Neutral)
                        neutralPairs++;
                }
            plan.regionalRelationPattern = (byte)
                CAWorldTendencyCausalKernel.RelationPattern(factionKeys.Count,
                    hostilePairs, neutralPairs);

            foreach (CARegionalSettlementPlan settlement in settlements)
            {
                settlement.hasRoadAccess =
                    CARegionalPlanUtility.ConstituentHasRoad(
                        settlement.memberTileId);
                settlement.hasRiverAccess =
                    CARegionalPlanUtility.ConstituentHasRiver(
                        settlement.memberTileId);
                settlement.hasCoastalAccess =
                    CARegionalPlanUtility.ConstituentIsCoastal(
                        settlement.memberTileId);
                settlement.realizedAccessInfrastructure =
                    CASettlementStartingState.Access(plan, settlement);
                settlement.realizedServiceInfrastructure =
                    CASettlementStartingState.Services(plan, settlement);
                settlement.realizedCivicInfrastructure =
                    CASettlementStartingState.Civic(plan, settlement);
                settlement.specialization = Specialization(plan,
                    settlement);
                settlement.economicCapacity = EconomicCapacity(plan,
                    settlement);
                settlement.tradeConnectivity = TradeConnectivity(plan,
                    settlement);
                settlement.urbanSupport =
                    CAWorldTendencyCausalKernel.UrbanSupport(
                        settlement.residentPopulation,
                        settlement.landCapacity,
                        settlement.realizedAccessInfrastructure,
                        settlement.realizedServiceInfrastructure,
                        settlement.realizedCivicInfrastructure,
                        settlement.economicCapacity,
                        settlement.tradeConnectivity,
                        settlement.specialization,
                        (CASettlementRole)settlement.realizedRole
                            == CASettlementRole.Center,
                        settlement.historicalDevelopment);
                settlement.realizedScale = (byte)
                    CAWorldTendencyCausalKernel.SettlementScale(
                        settlement.residentPopulation,
                        settlement.urbanSupport,
                        policy.urbanGrowthPropensity);
            }
            plan.settlementScale = settlements.Count == 0 ? (byte)0
                : settlements.Max(item => item.realizedScale);

            RealizeFrontierHoldings(plan, policy, seed);

            List<IGrouping<int, CARegionalSettlementPlan>> byTile = settlements
                .GroupBy(item => item.memberTileId).ToList();
            int occupiedTiles = byTile.Count;
            int largestTileGroup = byTile.Count == 0 ? 0
                : byTile.Max(group => group.Count());
            int routeEdges = RouteEdges(byTile.Select(group => group.Key));
            float averageDistance = AverageDistance(settlements);
            List<CARegionalSettlementPlan> byPopulation = settlements
                .OrderByDescending(item => item.residentPopulation).ToList();
            bool dominant = byPopulation.Count > 1
                && byPopulation[0].residentPopulation
                    >= byPopulation[1].residentPopulation * 1.35f;
            int settlementFactionCount = settlements
                .Select(item => item.factionKey).Distinct().Count();
            plan.settlementPattern = (byte)
                CAWorldTendencyCausalKernel.SettlementPattern(
                    settlements.Count, occupiedTiles, largestTileGroup,
                    routeEdges, averageDistance, dominant,
                    settlementFactionCount, plan.regionalRelationPattern,
                    plan.frontierHoldings?.Count ?? 0);
            // Provision operators derive from the realized social order;
            // settlement programs then derive from those arrangements and the
            // complete saved settlement facts.
            foreach (CARegionalSettlementPlan settlement in settlements)
            {
                CASettlementComposition.EnsureDerived(plan, settlement);
                CASettlementProgramRegistry.EnsureDerived(plan, settlement,
                    force: true);
            }
            plan.settlementRealizationSourceHash = RealizationSourceHash(plan);
            plan.settlementRealizationComplete = true;
        }

        internal static CASettlementScale RealizedScaleOf(
            CARegionalPlan plan, CARegionalSettlementPlan settlement)
        {
            if (settlement != null)
                return (CASettlementScale)settlement.realizedScale;
            return (CASettlementScale)(plan?.settlementScale ?? 0);
        }

        private static void RealizeFrontierHoldings(CARegionalPlan plan,
            CARegionalWorldPolicy policy, int seed)
        {
            plan.frontierHoldings = BuildFrontierHoldings(plan, policy, seed);
        }

        private static List<CAFrontierHoldingPlan> BuildFrontierHoldings(
            CARegionalPlan plan, CARegionalWorldPolicy policy, int seed)
        {
            var result = new List<CAFrontierHoldingPlan>();
            var occupied = new HashSet<int>((plan.settlements
                    ?? new List<CARegionalSettlementPlan>())
                .Where(item => item != null)
                .Select(item => item.memberTileId));
            List<int> suitable = (plan.memberTileIds ?? new List<int>())
                .Where(id => id != plan.startTileId && !occupied.Contains(id)
                    && LandCapacity(id) > 0)
                .OrderBy(id => CAWorldTendencyCausalKernel.Unit(seed, id,
                    2718283)).ThenBy(id => id).ToList();
            int count = CAWorldTendencyCausalKernel.FrontierHoldingCount(
                suitable.Count, policy.frontierHoldingFrequency);
            for (int i = 0; i < count && i < suitable.Count; i++)
            {
                int land = LandCapacity(suitable[i]);
                int residents = CAWorldTendencyCausalKernel
                    .FrontierResidentCount(land,
                        policy.frontierHoldingSize);
                int material = CAWorldTendencyCausalKernel
                    .FrontierMaterialLevel(land,
                        policy.frontierHoldingSize);
                result.Add(new CAFrontierHoldingPlan
                {
                    key = i,
                    memberTileId = suitable[i],
                    residentCount = residents,
                    landCapacity = land,
                    materialLevel = material,
                    form = CAWorldTendencyCausalKernel.FrontierForm(
                        residents, material),
                    factionless = true
                });
            }
            return result;
        }

        internal static string RelationPatternWords(
            CARegionalRelationPattern pattern)
        {
            switch (pattern)
            {
                case CARegionalRelationPattern.Rival: return "rival";
                case CARegionalRelationPattern.Contested: return "contested";
                default: return "peaceful";
            }
        }

        private static int RealizationSourceHash(CARegionalPlan plan)
        {
            int hash = CAWorldTendencyCausalKernel.StableStringHash(
                plan?.candidateId ?? "ca");
            CARegionalWorldPolicy policy = plan?.worldPolicy
                ?? new CARegionalWorldPolicy();
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                Mathf.RoundToInt(policy.urbanGrowthPropensity * 10000f));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                Mathf.RoundToInt(policy.frontierHoldingFrequency * 10000f));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                Mathf.RoundToInt(policy.frontierHoldingSize * 10000f));
            foreach (CARegionalRelationPlan relation in (plan?.relations
                ?? new List<CARegionalRelationPlan>()).Where(item => item != null)
                .OrderBy(item => item.leftFactionKey)
                .ThenBy(item => item.rightFactionKey))
            {
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    relation.leftFactionKey,
                    relation.rightFactionKey, (int)relation.relation);
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    relation.authorRelation ? 1 : 0);
            }
            foreach (CARegionalFactionPlan faction in (plan?.factions
                ?? new List<CARegionalFactionPlan>()).Where(item => item != null)
                .OrderBy(item => item.key))
            {
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    faction.key,
                    (int)faction.source, faction.existingFactionLoadId);
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    faction.institutionalStateIncomplete ? 1 : 0);
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    CAWorldTendencyCausalKernel.StableStringHash(
                        faction.customFactionDefName
                        ?? "none"));
                foreach (CAAxisEntry axis in (faction.politicalBeliefs
                        ?.positions ?? new List<CAAxisEntry>())
                    .Where(item => item != null)
                    .OrderBy(item => item.axisKey))
                    hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                        CAWorldTendencyCausalKernel.StableStringHash(
                            axis.axisKey ?? "none"),
                        CAWorldTendencyCausalKernel.StableStringHash(
                            axis.optionKey ?? "none"),
                        axis.source);
                foreach (CAAxisEntry axis in (faction.factionStructure
                    ?? new List<CAAxisEntry>()).Where(item => item != null)
                    .OrderBy(item => item.axisKey))
                    hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                        CAWorldTendencyCausalKernel.StableStringHash(
                            axis.axisKey ?? "none"),
                        CAWorldTendencyCausalKernel.StableStringHash(
                            axis.optionKey ?? "none"),
                        axis.source);
            }
            foreach (CARegionalSettlementPlan settlement in (plan?.settlements
                ?? new List<CARegionalSettlementPlan>()).Where(item => item != null)
                .OrderBy(item => item.slot))
            {
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    settlement.slot,
                    settlement.memberTileId, settlement.factionKey);
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    (int)settlement.populationOrigin,
                    settlement.reallocatedFromTileId,
                    settlement.operationalRoleMask);
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    settlement.authoredForm, 0, 0);
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    settlement.hasRoadAccess ? 1 : 0,
                    settlement.hasRiverAccess ? 1 : 0,
                    settlement.hasCoastalAccess ? 1 : 0);
            }
            return hash;
        }

        private static void RealizeRelations(CARegionalPlan plan)
        {
            CARegionalPlanUtility.EnsureRelationRows(plan);
            foreach (CARegionalRelationPlan relation in plan.relations
                .Where(item => item != null && !item.authorRelation))
            {
                CARegionalFactionPlan leftPlan = plan.FactionPlan(
                    relation.leftFactionKey);
                CARegionalFactionPlan rightPlan = plan.FactionPlan(
                    relation.rightFactionKey);
                Faction left = leftPlan?.source
                        == CARegionalFactionSource.ExistingWorldFaction
                    ? CARegionalPlanUtility.FactionByLoadId(
                        leftPlan.existingFactionLoadId) : null;
                Faction right = rightPlan?.source
                        == CARegionalFactionSource.ExistingWorldFaction
                    ? CARegionalPlanUtility.FactionByLoadId(
                        rightPlan.existingFactionLoadId) : null;
                if (left != null && right != null)
                    relation.relation = left.RelationKindWith(right);
                else if (!relation.authorRelation)
                    Log.Warning("[CA][Region] faction relation "
                        + relation.leftFactionKey + "-"
                        + relation.rightFactionKey + " remains unset: no "
                        + "native relation or Starting Region decision exists");
            }
        }

        private static int LandCapacity(int tileId)
        {
            PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
            if (!tile.Valid || tile.Tile == null || tile.Tile.WaterCovered)
                return 0;
            switch (tile.Tile.hilliness)
            {
                case Hilliness.Impassable: return 0;
                case Hilliness.Mountainous: return 1;
                case Hilliness.LargeHills: return 2;
                default: return 3;
            }
        }

        private static int HistoricalDevelopment(
            CARegionalSettlementPlan settlement)
        {
            if (settlement == null) return 0;
            int explicitEvidence = (settlement.localCulture?.transitions
                    ?? new List<CACultureTransition>()).Count(item =>
                        item != null)
                + (settlement.localCulture?.observations
                    ?? new List<CACultureObservation>()).Count(item =>
                        item != null)
                + (settlement.operationalFacts
                    ?? new List<CASettlementOperationalFact>()).Count(item =>
                        item != null && item.active
                        && item.provenance?.StartsWith("observed:",
                            StringComparison.Ordinal) == true);
            // Starting-region settlements are established societies at the
            // scenario boundary. Higher values require recorded evidence.
            return explicitEvidence >= 2 ? 3 : explicitEvidence == 1 ? 2 : 1;
        }

        private static int TechTier(CARegionalFactionPlan group)
        {
            TechLevel tech = group?.ResolvedFactionDef?.techLevel
                ?? TechLevel.Neolithic;
            return (int)tech >= (int)TechLevel.Spacer ? 3
                : (int)tech >= (int)TechLevel.Industrial ? 2
                : (int)tech >= (int)TechLevel.Medieval ? 1 : 0;
        }

        private static int TradeConnectivity(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            int trade = CompleteProgramCount(plan, settlement,
                CASettlementProgramRegistry.Trade);
            if (trade == 0) return 0;
            bool route = settlement.hasRoadAccess || settlement.hasRiverAccess
                || settlement.hasCoastalAccess;
            int support = CompleteProgramCount(plan, settlement,
                CASettlementProgramRegistry.Transport,
                CASettlementProgramRegistry.Communications);
            return Mathf.Clamp(1 + (route ? 1 : 0)
                + (support > 0 ? 1 : 0), 0, 3);
        }

        private static int EconomicCapacity(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            return CompleteProgramCount(plan, settlement,
                CASettlementProgramRegistry.Production,
                CASettlementProgramRegistry.SpecializedIndustry,
                CASettlementProgramRegistry.Trade,
                CASettlementProgramRegistry.Agriculture,
                CASettlementProgramRegistry.Animals,
                CASettlementProgramRegistry.CommunalProvision,
                CASettlementProgramRegistry.AuthorityProvision,
                CASettlementProgramRegistry.DomesticProvision);
        }

        private static int Specialization(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            return CompleteProgramCount(plan, settlement,
                CASettlementProgramRegistry.SpecializedIndustry,
                CASettlementProgramRegistry.Medicine,
                CASettlementProgramRegistry.Research,
                CASettlementProgramRegistry.Defense,
                CASettlementProgramRegistry.Agriculture,
                CASettlementProgramRegistry.Animals,
                CASettlementProgramRegistry.ArtAndMemory,
                CASettlementProgramRegistry.Religion);
        }

        private static int CompleteProgramCount(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, params string[] keys)
        {
            if (settlement == null || keys == null || keys.Length == 0)
                return 0;
            HashSet<string> wanted = new HashSet<string>(keys);
            int count = CASettlementProgramOperationalResolver.Build(plan,
                    settlement, CASettlementProgramRegistry.Find)
                .Count(item => item != null && item.Complete
                    && wanted.Contains(item.Key));
            return Mathf.Clamp(count, 0, 3);
        }

        private static int RouteEdges(IEnumerable<int> tileIds)
        {
            List<int> ids = tileIds.Distinct().ToList();
            int edges = 0;
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                    if (CARegionalPlanUtility.ConstituentsShareRoute(
                            ids[i], ids[j])) edges++;
            return edges;
        }

        private static float AverageDistance(
            List<CARegionalSettlementPlan> settlements)
        {
            if (settlements.Count < 2) return 0f;
            float total = 0f;
            int pairs = 0;
            for (int i = 0; i < settlements.Count; i++)
                for (int j = i + 1; j < settlements.Count; j++)
                {
                    PlanetTile left = CARegionalPlanUtility.SurfaceTile(
                        settlements[i].memberTileId);
                    PlanetTile right = CARegionalPlanUtility.SurfaceTile(
                        settlements[j].memberTileId);
                    try
                    {
                        total += Verse.Find.WorldGrid.ApproxDistanceInTiles(
                            left, right);
                    }
                    catch { total += 2f; }
                    pairs++;
                }
            return pairs == 0 ? 0f : total / pairs;
        }

        internal static string SettlementProfile(CARegionalPlan plan)
        {
            var topology = (CASettlementPattern)plan.settlementPattern;
            var scale = (CASettlementScale)plan.settlementScale;
            if (topology == CASettlementPattern.Unsettled)
                return plan.settlements.Any(b => b != null)
                    ? "not set"
                    : "unsettled land";
            return PatternWords(topology) + " · " + ScaleWords(scale);
        }

        // Settlement authority is separate from Political Beliefs. It may be
        // authored directly or summarized from complete instituted leadership
        // and participation facts. Settlement count never supplies authority.
        internal static bool TrySettlementAuthorityOf(CARegionalPlan plan,
            CARegionalFactionPlan group, out CASettlementAuthority authority)
        {
            authority = CASettlementAuthority.Central;
            if (group == null) return false;
            if (group.settlementAuthorityExplicit)
            {
                authority = NormalizeAuthority(
                    (CASettlementAuthority)group.settlementAuthority);
                return true;
            }

            string leadership = CAFactionAxes.KeyOf(group.factionStructure,
                CAFactionAxes.Leadership);
            string participation = CAFactionAxes.KeyOf(group.factionStructure,
                CAFactionAxes.Participation);
            if (leadership.NullOrEmpty() || participation.NullOrEmpty())
                return false;

            switch (leadership)
            {
                case "none":
                    authority = CASettlementAuthority.Independent;
                    break;
                case "federated":
                    authority = CASettlementAuthority.IndependentWithSharedDefense;
                    break;
                case "whole":
                    authority = CASettlementAuthority.Independent;
                    break;
                case "council":
                    authority = participation == "universal"
                        ? CASettlementAuthority.Shared
                        : CASettlementAuthority.CentralWithLocalRule;
                    break;
                case "single":
                    authority = participation == "universal"
                        ? CASettlementAuthority.CentralWithLocalRule
                        : CASettlementAuthority.Central;
                    break;
                default:
                    return false;
            }
            return true;
        }

        internal static CASettlementAuthority SettlementAuthorityOf(
            CARegionalPlan plan, CARegionalFactionPlan group)
        {
            CASettlementAuthority authority;
            return TrySettlementAuthorityOf(plan, group, out authority)
                ? authority : CASettlementAuthority.Central;
        }

        // Responsibilities shared by member settlements at faction level.
        internal static string[] SharedResponsibilities(
            CASettlementAuthority authority)
        {
            authority = NormalizeAuthority(authority);
            // Unknown responsibility keys are ignored.
            CASettlementAuthorityDef def =
                DefDatabase<CASettlementAuthorityDef>.GetNamedSilentFail(
                    "CA_Authority_" + authority);
            if (def?.sharedResponsibilities != null)
                return def.sharedResponsibilities.Where(responsibility =>
                    Array.IndexOf(CAResponsibilities.All, responsibility) >= 0).ToArray();
            switch (authority)
            {
                case CASettlementAuthority.Central:
                    return new[] { CAResponsibilities.Defense, CAResponsibilities.Diplomacy,
                        CAResponsibilities.Taxes, CAResponsibilities.Decisions,
                        CAResponsibilities.Disputes };
                case CASettlementAuthority.CentralWithLocalRule:
                    return new[] { CAResponsibilities.Defense, CAResponsibilities.Diplomacy,
                        CAResponsibilities.Taxes };
                case CASettlementAuthority.Shared:
                    return new[] { CAResponsibilities.Defense,
                        CAResponsibilities.Diplomacy };
                case CASettlementAuthority.IndependentWithSharedDefense:
                    return new[] { CAResponsibilities.Defense };
                default:
                    // These arrangements keep all responsibilities local.
                    return new string[0];
            }
        }

        // Faction presence counts owned settlements and resident populations.
        internal static string PresenceWords(CARegionalPlan plan,
            CARegionalFactionPlan group)
        {
            int held = plan.settlements.Count(b => b != null
                && b.factionKey == group.key);
            int minority = plan.settlements.Count(b => b != null
                && b.factionKey != group.key
                && b.populationGroups != null
                && b.populationGroups.Any(c => c != null
                    && c.factionKey == group.key));
            var text = new StringBuilder();
            text.Append(held == 0 ? "no settlements"
                : "present in " + held + " settlement"
                    + (held == 1 ? "" : "s"));
            if (minority > 0)
                text.Append(" · resident population in " + minority
                    + " more");
            CASettlementAuthority authority =
                SettlementAuthorityOf(plan, group);
            if (held > 1)
                text.Append(authority
                        == CASettlementAuthority.IndependentWithSharedDefense
                        || authority == CASettlementAuthority.Independent
                    ? " · no single capital"
                    : " · seat at its "
                        + (plan.settlements.FirstOrDefault(b => b != null
                            && b.factionKey == group.key
                            && b.realizedRole
                                == (byte)CASettlementRole.Center)
                            != null ? "regional seat" : "first settlement"));
            return text.ToString();
        }

        // A direct summary of current faction structure and settlement pattern.
        internal static string Characterize(CARegionalPlan plan,
            CARegionalFactionPlan group)
        {
            string structure = CAFactionAxes.Characterize(plan, group);
            CASettlementAuthority authority =
                SettlementAuthorityOf(plan, group);
            int held = plan.settlements.Count(b => b != null
                && b.factionKey == group.key);
            if (held == 0) return structure + " · no settlements";
            if (held == 1) return structure + " · one settlement";
            return structure + " · " + SettlementAuthorityWords(authority)
                + " · " + PatternWords(
                    (CASettlementPattern)plan.settlementPattern)
                + " " + ScaleWords(
                    (CASettlementScale)plan.settlementScale);
        }
    }

    // World-map marker for a regional settlement. Shows its name, faction
    // color, and regional role.
    public sealed class WorldObject_CARegionalSettlement : WorldObject
    {
        public string regionalId;
        public int slot = -1;
        public string settlementName;
        public string roleWords;
        public string culturalExpressionSummary;
        public byte culturalExpressionStatus;

        public override string Label
        {
            get { return settlementName.NullOrEmpty()
                ? base.Label : settlementName; }
        }

        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();
            string mine = (roleWords.NullOrEmpty() ? "" : roleWords
                    .CapitalizeFirst() + " of this region.\n")
                + "Stands inside the expanded regional map."
                + (culturalExpressionSummary.NullOrEmpty() ? "" : "\n"
                    + CACulturalExpression.StatusWords(
                        (CACulturalExpressionStatus)culturalExpressionStatus)
                    + " cultural expression: "
                    + culturalExpressionSummary);
            return baseString.NullOrEmpty() ? mine
                : baseString + "\n" + mine;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref regionalId, "regionalId");
            Scribe_Values.Look(ref slot, "slot", -1);
            Scribe_Values.Look(ref settlementName, "settlementName");
            Scribe_Values.Look(ref roleWords, "roleWords");
            Scribe_Values.Look(ref culturalExpressionSummary,
                "culturalExpressionSummary");
            Scribe_Values.Look(ref culturalExpressionStatus,
                "culturalExpressionStatus", (byte)0);
        }
    }

    internal static class CARegionalSettlementMarkers
    {
        // Idempotent: one marker per (regionalId, slot); reconciles name,
        // faction and role on every pass; removes markers whose record no
        // longer exists.
        internal static void Ensure(CARegionalPlan plan,
            IEnumerable<CARegionalSettlementRecord> records)
        {
            try
            {
                WorldObjectDef def =
                    DefDatabase<WorldObjectDef>
                        .GetNamedSilentFail("CA_RegionalSettlementMarker");
                if (def == null || Find.WorldObjects == null) return;
                List<CARegionalSettlementRecord> live = records
                    ?.Where(r => r != null && r.faction != null).ToList()
                    ?? new List<CARegionalSettlementRecord>();
                var existing = Find.WorldObjects.AllWorldObjects
                    .OfType<WorldObject_CARegionalSettlement>().ToList();
                foreach (WorldObject_CARegionalSettlement marker in
                    existing)
                {
                    bool still = live.Any(r =>
                        r.regionalId == marker.regionalId
                        && r.slot == marker.slot);
                    if (!still && marker.regionalId != null
                        && plan != null && plan.regionalId != null
                        && marker.regionalId.StartsWith("CA-RS-"))
                        marker.Destroy();
                }
                foreach (CARegionalSettlementRecord record in live)
                {
                    WorldObject_CARegionalSettlement marker = existing
                        .FirstOrDefault(m =>
                            m.regionalId == record.regionalId
                            && m.slot == record.slot);
                    if (marker == null)
                    {
                        marker = (WorldObject_CARegionalSettlement)
                            WorldObjectMaker.MakeWorldObject(def);
                        marker.regionalId = record.regionalId;
                        marker.slot = record.slot;
                        marker.Tile = CARegionalPlanUtility.SurfaceTile(
                            record.memberTileId);
                        Find.WorldObjects.Add(marker);
                    }
                    marker.settlementName = record.name;
                    marker.roleWords = RoleWordsFor(plan, record);
                    marker.culturalExpressionSummary =
                        record.culturalExpressionSummary;
                    marker.culturalExpressionStatus =
                        record.culturalExpressionStatus;
                    if (marker.Faction != record.faction)
                        marker.SetFaction(record.faction);
                }
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Settlements] settlement markers failed: "
                    + e.Message);
            }
        }

        private static string RoleWordsFor(CARegionalPlan plan,
            CARegionalSettlementRecord record)
        {
            CARegionalSettlementPlan settlementPlan = plan?.settlements.FirstOrDefault(b =>
                b != null && b.slot == record.slot);
            return settlementPlan == null ? null
                : CARegionalSettlements.RoleWords(
                    (CASettlementRole)settlementPlan.realizedRole);
        }
    }

    // Writes faction-to-settlement authority into the organization relation
    // model after settlement records exist.
    internal static class CASettlementAuthorityWriter
    {
        private const string AuthorityOriginPrefix =
            "settlement-authority:";
        private const string AuthorityRosterOriginPrefix =
            "settlement-roster:";
        internal static void Materialize(CARegionalPlan plan,
            IEnumerable<CARegionalSettlementRecord> records)
        {
            try
            {
                CAOrganizationWorldComponent orgs =
                    CAOrganizationWorldComponent.Current;
                if (orgs == null || plan == null) return;
                List<CARegionalSettlementRecord> held =
                    records?.Where(r => r != null && r.faction != null)
                        .ToList()
                    ?? new List<CARegionalSettlementRecord>();
                foreach (CARegionalFactionPlan group in
                    plan.factions)
                {
                    if (group == null) continue;
                    List<CARegionalSettlementRecord> mine = held
                        .Where(r => r.factionKey == group.key)
                        .ToList();
                    Faction faction = mine.FirstOrDefault()?.faction
                        ?? group.resolvedFaction
                        ?? CARegionalPlanUtility.FactionByLoadId(
                            group.resolvedFactionLoadId)
                        ?? CARegionalPlanUtility.FactionByLoadId(
                            group.existingFactionLoadId);
                    if (faction == null) continue;
                    // Every established settlement owns its local operating
                    // organization. Shared faction authority is a separate
                    // relation and may legitimately be absent.
                    foreach (CARegionalSettlementRecord record in mine)
                    {
                        string settlementKey = record.regionalId + "#"
                            + record.slot;
                        orgs.EnsureFor(settlementKey,
                            record.name ?? settlementKey,
                            "local organization of "
                                + (record.name ?? settlementKey),
                            CAOrganizationKind.Settlement);
                    }
                    CASettlementAuthority authority;
                    bool authorityKnown = CARegionalSettlements
                        .TrySettlementAuthorityOf(plan, group, out authority);
                    string factionKey = "faction:" + faction.loadID;
                    var desiredMembers = new HashSet<string>();
                    if (authorityKnown && mine.Count >= 2
                        && authority != CASettlementAuthority.Independent)
                        foreach (CARegionalSettlementRecord record in mine)
                            desiredMembers.Add(record.regionalId + "#"
                                + record.slot);
                    ReconcileDerivedMembers(factionKey, desiredMembers);
                    if (desiredMembers.Count == 0)
                    {
                        continue;
                    }

                    CAOrganization factionBody = orgs.EnsureFor(
                        factionKey, faction.Name,
                        CARegionalSettlements.SettlementAuthorityWords(authority)
                        + " for " + faction.Name,
                        CAOrganizationKind.Faction);
                    string[] delegated =
                        CARegionalSettlements.SharedResponsibilities(authority);
                    int now = Find.TickManager.TicksGame;
                    foreach (CARegionalSettlementRecord record in mine)
                    {
                        string memberKey = record.regionalId + "#"
                            + record.slot;
                        orgs.EnsureFor(memberKey,
                            record.name ?? memberKey,
                            "member settlement of " + faction.Name,
                            CAOrganizationKind.Settlement);
                        CASettlementAuthorityMembership.Admit(factionBody,
                            memberKey, delegated,
                            CAOrigin.Derived(AuthorityRosterOriginPrefix
                                + faction.loadID),
                            group.settlementAuthorityExplicit
                                ? CAOrigin.Authored(AuthorityOriginPrefix
                                    + CARegionalSettlements
                                        .SettlementAuthorityWords(authority))
                                : CAOrigin.Derived(AuthorityOriginPrefix
                                    + CARegionalSettlements
                                        .SettlementAuthorityWords(authority)));
                    }
                    factionBody.Record("relations",
                        CARegionalSettlements.SettlementAuthorityWords(authority)
                        + ": " + mine.Count + " member settlement"
                        + (mine.Count == 1 ? "" : "s")
                        + (delegated.Length == 0
                            ? ", all responsibilities remain local"
                            : ", shared responsibilities: "
                                + string.Join(", ", delegated)));

                    Log.Message("[CA][Settlements] " + faction.Name + ": "
                        + CARegionalSettlements.SettlementAuthorityWords(authority)
                        + ", " + mine.Count
                        + " member settlements, shared ["
                        + string.Join(", ", delegated) + "]");
                }
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Settlements] settlement authority "
                    + "materialization failed: " + e.Message);
            }
            try
            {
                MaterializeFederations(plan, records);
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Settlements] federation materialization "
                    + "failed: " + e.Message);
            }
        }

        private static void ReconcileDerivedMembers(string factionKey,
            HashSet<string> desiredMembers)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null || factionKey.NullOrEmpty()) return;
            List<CARelation> staleRelations = ledger
                .SettlementMemberships(factionKey)
                .Where(relation => relation.origin.Replaceable
                    && !relation.origin.originKey.NullOrEmpty()
                    && relation.origin.originKey.StartsWith(
                        AuthorityRosterOriginPrefix, StringComparison.Ordinal)
                    && !desiredMembers.Contains(relation.partyOrgKey))
                .ToList();
            foreach (CARelation relation in staleRelations)
                ledger.RemoveDerivedRelation(relation);
        }

        // A federation joins otherwise independent factions for the selected
        // responsibilities.
        internal static string[] FederationResponsibilities(string kind)
        {
            switch (kind)
            {
                case "taxes": return new[] { CAResponsibilities.Taxes };
                case "diplomacy":
                    return new[] { CAResponsibilities.Diplomacy };
                case "defense and diplomacy":
                    return new[] { CAResponsibilities.Defense,
                        CAResponsibilities.Diplomacy };
                default: return new[] { CAResponsibilities.Defense };
            }
        }

        internal static string FederationKindName(string kind)
        {
            switch (kind)
            {
                case "taxes": return "tax federation";
                case "diplomacy": return "diplomatic federation";
                case "defense and diplomacy":
                    return "defense and diplomacy federation";
                default: return "defense federation";
            }
        }

        internal static string FederationKindWords(string kind)
        {
            return FederationKindName(kind) + " - shared " + kind;
        }

        private static void MaterializeFederations(CARegionalPlan plan,
            IEnumerable<CARegionalSettlementRecord> records)
        {
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            if (orgs == null || plan == null) return;
            List<CARegionalSettlementRecord> held = records
                ?.Where(r => r != null && r.faction != null).ToList()
                ?? new List<CARegionalSettlementRecord>();
            foreach (IGrouping<int, CARegionalFactionPlan> federation in
                plan.factions.Where(g => g != null
                    && g.federationKey >= 0)
                    .GroupBy(g => g.federationKey))
            {
                var factions = new List<Faction>();
                foreach (CARegionalFactionPlan member in federation)
                {
                    Faction faction = held.FirstOrDefault(r =>
                        r.factionKey == member.key)?.faction
                        ?? member.resolvedFaction;
                    if (faction != null && !factions.Contains(faction))
                        factions.Add(faction);
                }
                if (factions.Count < 2) continue;
                string kind = federation.Select(g => g.federationKind)
                    .FirstOrDefault(k => !k.NullOrEmpty()) ?? "defense";
                string[] sharedResponsibilities =
                    FederationResponsibilities(kind);
                CAOrganization federationOrg = orgs.EnsureFor(
                    "federation:" + plan.regionalId + ":" + federation.Key,
                    factions[0].Name + " federation",
                    FederationKindWords(kind),
                    CAOrganizationKind.Federation);
                foreach (Faction faction in factions)
                {
                    orgs.EnsureFor("faction:" + faction.loadID,
                        faction.Name, "faction organization",
                        CAOrganizationKind.Faction);
                    CAFederation.Admit(federationOrg,
                        "faction:" + faction.loadID, sharedResponsibilities,
                        -1, CAOrigin.Authored("federation:" + kind));
                }
                federationOrg.Record("relations", FederationKindWords(kind)
                    + " materialized: " + factions.Count
                    + " independent factions sharing "
                    + string.Join(", ", sharedResponsibilities));
                Log.Message("[CA][Settlements] federation "
                    + federation.Key + " ("
                    + kind + "): " + factions.Count + " factions under "
                    + federationOrg.name);
            }
        }
    }
}
