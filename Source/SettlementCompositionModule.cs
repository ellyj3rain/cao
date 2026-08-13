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
    // share, and starting certainty. Inherited Culture belongs to its source
    // populations; local Culture records what the weighted population lives
    // through. Faction and Ideoligion use RimWorld's
    // native pawn fields where possible; political beliefs remain durable CA
    // state.
    // Provision arrangements are generated from population, current order,
    // infrastructure, and settlement role.
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
        // An exact native Ideoligion identity, independent of faction
        // affiliation. The referenced Ideoligion must already exist; a
        // population row never mints belief from a key or random seed.
        public int nativeIdeoligionId = -1;
        // Keeps a distinct Ideoligion from being suppressed by local
        // orthodoxy. This is belief protection, not residential geometry.
        public bool ideoligionProtected;
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
            Scribe_Values.Look(ref nativeIdeoligionId,
                "nativeIdeoligionId", -1);
            Scribe_Values.Look(ref ideoligionProtected,
                "ideoligionProtected", false);
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

    // Who operates a source of provision. Distribution changes its
    // physical setup; access and funding are recorded for supporting systems.
    public enum CAProvisionOperator : byte
    {
        // The settlement authority itself: reserve stores and
        // distribution. Tax-funded arrangements use the taxation loop.
        Authority,
        // Kitchens supplied through shared work.
        Communal,
        DomesticUnit,
        Individual
    }

    public enum CAProvisionAccess : byte
    { Universal, Members, Charitable }

    public enum CAProvisionFunding : byte
    {
        Domestic,
        Taxation,
        SharedWork
    }

    public enum CAProvisionDistribution : byte
    { Domestic, Neighborhood, Centralized }

    // How far an arrangement's provision actually reaches [layer E].
    public enum CAProvisionReach : byte
    { Domestic, Settlement, Region }

    public sealed class CAProvisionArrangement : IExposable
    {
        public const int CurrentSchemaVersion = 5;
        public int schemaVersion = CurrentSchemaVersion;
        public int key;
        // Stable causal slot. Generated provisions are reconciled by this
        // identity, so changing access, population, or faction
        // parameters refreshes the arrangement without stacking another copy.
        public string basisKey;
        public string basisLabel;
        public CAProvisionOperator operatorKind;
        // Exact operator and provenance. Labels summarize these fields; they
        // never create the arrangement.
        public string operatorIdentity;
        public string operatorSource;
        // Which population group operates this arrangement; -1 means
        // the settlement as a whole.
        public int populationGroupKey = -1;
        public CAProvisionAccess access;
        public string accessSource;
        public CAProvisionFunding funding;
        public string fundingSource;
        public CAProvisionDistribution distribution;
        public string distributionSource;
        public string laborSource;
        public string knowledgeSource;
        public string materialSource;
        public string stockSource;
        public string policyKey;
        public string policyValue;
        public string collectionPath;
        public bool operational;
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
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref key, "key", 0);
            Scribe_Values.Look(ref basisKey, "basisKey");
            Scribe_Values.Look(ref basisLabel, "basisLabel");
            Scribe_Values.Look(ref operatorKind, "operatorKind",
                CAProvisionOperator.Individual);
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref operatorSource, "operatorSource");
            Scribe_Values.Look(ref populationGroupKey, "populationGroupKey", -1);
            Scribe_Values.Look(ref access, "access",
                CAProvisionAccess.Universal);
            Scribe_Values.Look(ref accessSource, "accessSource");
            Scribe_Values.Look(ref funding, "funding",
                CAProvisionFunding.Domestic);
            Scribe_Values.Look(ref fundingSource, "fundingSource");
            Scribe_Values.Look(ref distribution, "distribution",
                CAProvisionDistribution.Centralized);
            Scribe_Values.Look(ref distributionSource,
                "distributionSource");
            Scribe_Values.Look(ref laborSource, "laborSource");
            Scribe_Values.Look(ref knowledgeSource, "knowledgeSource");
            Scribe_Values.Look(ref materialSource, "materialSource");
            Scribe_Values.Look(ref stockSource, "stockSource");
            Scribe_Values.Look(ref policyKey, "policyKey");
            Scribe_Values.Look(ref policyValue, "policyValue");
            Scribe_Values.Look(ref collectionPath, "collectionPath");
            Scribe_Values.Look(ref operational, "operational", false);
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
                    case CAProvisionOperator.DomesticUnit:
                        return "domestic hearth";
                    default: return "individual provision";
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
                    case CAProvisionFunding.Domestic:
                        return "the members";
                    default: return funding.ToString().ToLower();
                }
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
            if (settlementPlan.provisionArrangements == null)
                settlementPlan.provisionArrangements =
                    new List<CAProvisionArrangement>();
            if (settlementPlan.populationGroups.Count == 0)
                DerivePopulationGroups(plan, settlementPlan);
            CADomesticProvisionAdapter.EnsureDemands(settlementPlan);
            CACultureHistory.EnsureSettlementCulture(plan, settlementPlan);
            ReconcileProvisionArrangements(plan, settlementPlan);
        }

        internal static void ReconcileProvisionArrangements(CARegionalPlan plan,
            CARegionalSettlementPlan settlementPlan)
        {
            if (plan == null || settlementPlan == null) return;
            if (settlementPlan.provisionArrangements == null)
                settlementPlan.provisionArrangements =
                    new List<CAProvisionArrangement>();
            settlementPlan.provisionArrangements =
                GenerateProvisionArrangements(plan, settlementPlan);
        }

        internal static List<CAProvisionArrangement> GenerateProvisionArrangements(
            CARegionalPlan plan, CARegionalSettlementPlan source)
        {
            if (plan == null || source == null)
                return new List<CAProvisionArrangement>();
            // The current arrangement is the authoritative fact. This pass
            // validates its complete causal contract; it does not derive an
            // operator from Culture, Political Beliefs, tendencies, scale, or
            // infrastructure summaries.
            var facts = new CAProvisionCausalFacts();
            foreach (CAProvisionArrangement arrangement in
                source.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
            {
                if (arrangement == null || !arrangement.active) continue;
                facts.Operators.Add(new CAProvisionOperatorFact
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
                    PolicyKey = arrangement.policyKey,
                    PolicyValue = arrangement.policyValue,
                    CollectionPath = arrangement.collectionPath,
                    Nodes = arrangement.nodes,
                    Reach = arrangement.reach.ToString()
                });
            }
            return CAProvisionCausalKernel.Derive(facts).Select(spec =>
                new CAProvisionArrangement
                {
                    schemaVersion = CAProvisionArrangement.CurrentSchemaVersion,
                    key = spec.Key, basisKey = spec.BasisKey,
                    basisLabel = spec.BasisLabel,
                    operatorKind = (CAProvisionOperator)Enum.Parse(
                        typeof(CAProvisionOperator), spec.Operator),
                    operatorIdentity = spec.OperatorIdentity,
                    operatorSource = spec.OperatorSource,
                    populationGroupKey = spec.PopulationGroupKey,
                    access = (CAProvisionAccess)Enum.Parse(
                        typeof(CAProvisionAccess), spec.Access),
                    accessSource = spec.AccessSource,
                    funding = (CAProvisionFunding)Enum.Parse(
                        typeof(CAProvisionFunding), spec.Funding),
                    fundingSource = spec.FundingSource,
                    distribution = (CAProvisionDistribution)Enum.Parse(
                        typeof(CAProvisionDistribution), spec.Distribution),
                    distributionSource = spec.DistributionSource,
                    laborSource = spec.LaborSource,
                    knowledgeSource = spec.KnowledgeSource,
                    materialSource = spec.MaterialSource,
                    stockSource = spec.StockSource,
                    policyKey = spec.PolicyKey,
                    policyValue = spec.PolicyValue,
                    collectionPath = spec.CollectionPath,
                    active = true, operational = false,
                    waterSecured = true,
                    nodes = spec.Nodes,
                    reach = (CAProvisionReach)Enum.Parse(
                        typeof(CAProvisionReach), spec.Reach)
                }).ToList();
        }

        internal static bool TryValidateProvisionArrangements(
            CARegionalPlan plan, CARegionalSettlementPlan settlement,
            out string failure)
        {
            failure = null;
            List<CAProvisionArrangement> expected =
                GenerateProvisionArrangements(plan, settlement);
            List<CAProvisionArrangement> saved = settlement
                ?.provisionArrangements ?? new List<CAProvisionArrangement>();
            string Signature(IEnumerable<CAProvisionArrangement> values) =>
                string.Join("|", values.Select(item => item == null ? "null"
                    : string.Join(":", item.schemaVersion, item.key,
                        item.basisKey, item.basisLabel, item.operatorKind,
                        item.operatorIdentity, item.operatorSource,
                        item.populationGroupKey, item.access,
                        item.accessSource, item.funding,
                        item.fundingSource, item.distribution,
                        item.distributionSource, item.laborSource,
                        item.knowledgeSource, item.materialSource,
                        item.stockSource, item.policyKey,
                        item.policyValue, item.collectionPath,
                        item.active, item.nodes, item.reach)));
            if (Signature(saved) == Signature(expected)) return true;
            failure = "the saved provision arrangements do not match their "
                + "authored or observed operator, funding, labor, stock, "
                + "material, access, and distribution contracts";
            return false;
        }

        // Unset composition creates only the factual owning population.
        // Minority, unaffiliated, and belief-protected populations are authored
        // settlement facts; a tendency or random draw cannot create them.
        private static void DerivePopulationGroups(CARegionalPlan plan,
            CARegionalSettlementPlan settlementPlan)
        {
            CARegionalFactionPlan owner = plan.FactionPlan(
                settlementPlan.factionKey);
            settlementPlan.populationGroups.Add(
                new CASettlementPopulationGroup
            {
                key = 1,
                kind = CAPopulationGroupKind.Main,
                label = CARegionalPlanUtility.FactionName(owner),
                share = 100,
                factionKey = settlementPlan.factionKey,
                ideoligionCertainty = 1
            });
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
                CASettlementResidenceState.Normalize(record);

                List<Pawn> residents = map.mapPawns
                    .SpawnedPawnsInFaction(record.faction)
                    .Where(p => p != null && p.RaceProps.Humanlike
                        && p.Position.InHorDistOf(
                            record.localRect.CenterCell, 60f)
                        && CASettlementResidenceState.Active(record,
                            p.thingIDNumber) == null)
                    .OrderBy(p => p.thingIDNumber).ToList();
                if (residents.Count == 0) return;

                // [rights axis] How this faction treats difference decides
                // what happens to distinct-Ideoligion population groups at projection -
                // the axis' first behavioral consumer.
                string rights = CAFactionAxes.KeyOf(
                    CAFactionStateWorldComponent.Current
                        ?.Find(record.faction)?.factionStructure, CAFactionAxes.Dissent);

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
                if (populationGroup.nativeIdeoligionId >= 0
                    || populationGroup.ideoligionFactionKey >= 0)
                {
                    Ideo ideoligion = populationGroup.nativeIdeoligionId >= 0
                        ? IdeoligionById(populationGroup.nativeIdeoligionId)
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
        // majoritarian rule permits it without faction support; protection
        // preserves it under orthodoxy. Unprotected groups may be suppressed.
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
            Ideo target = populationGroup.nativeIdeoligionId >= 0
                ? IdeoligionById(populationGroup.nativeIdeoligionId)
                : populationGroup.kind == CAPopulationGroupKind.Unaffiliated
                    && populationGroup.ideoligionFactionKey < 0 ? null
                : FactionForGroup(map, ideoligionFactionKey)?.ideos?.PrimaryIdeo;
            Ideo dominant = record.faction.ideos?.PrimaryIdeo;
            bool suppress = rights == "orthodoxy"
                && !populationGroup.ideoligionProtected
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
                        || (rights == "customary"
                            && populationGroup.ideoligionProtected)
                        || (rights == "orthodoxy"
                            && populationGroup.ideoligionProtected))
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
            CASettlementResidenceState.Assign(record, pawn,
                populationGroup.key, "initial population realization",
                "saved population group " + populationGroup.key
                    + " materialized as a resident");
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

        private static Ideo IdeoligionById(int id)
        {
            try
            {
                return Find.IdeoManager?.IdeosListForReading?
                    .FirstOrDefault(ideo => ideo != null && ideo.id == id);
            }
            catch { return null; }
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
            if (record == null || pawn == null) return null;
            CASettlementResidenceAssignment assignment =
                CASettlementResidenceState.Active(record,
                    pawn.thingIDNumber);
            return assignment?.populationGroupKey.ToString();
        }

        // Current residents are live humanlike pawns with an active typed
        // residence assignment. Faction identity and proximity alone cannot
        // admit a passerby.
        internal static List<Pawn> Residents(CARegionalSettlementRecord record,
            Map map)
        {
            var result = new List<Pawn>();
            if (record == null || map?.mapPawns == null) return result;
            CASettlementResidenceState.Normalize(record);
            var assigned = new HashSet<int>(record.residenceAssignments
                .Where(item => item != null && item.active)
                .Select(item => item.pawnId));
            CellRect reach = record.localRect == CellRect.Empty
                ? CellRect.Empty : record.localRect.ExpandedBy(12);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn.Dead || pawn.RaceProps == null
                    || !pawn.RaceProps.Humanlike) continue;
                if (reach != CellRect.Empty && !reach.Contains(pawn.Position))
                    continue;
                if (assigned.Contains(pawn.thingIDNumber))
                    result.Add(pawn);
            }
            return result.OrderBy(pawn => pawn.thingIDNumber).ToList();
        }

        internal static List<Pawn> ResidentsInPopulationGroup(
            CARegionalSettlementRecord record, Map map, int populationGroupKey)
        {
            var ids = new HashSet<int>((record?.residenceAssignments
                    ?? new List<CASettlementResidenceAssignment>())
                .Where(item => item != null && item.active
                    && item.populationGroupKey == populationGroupKey)
                .Select(item => item.pawnId));
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

    // Realizes the social and material remainder of provision arrangements.
    // Program materialization owns kitchens, hearths, dining assets, and
    // stores. This pass creates only real communal operators, binds authority
    // reserves to the settlement authority, reconciles taxation, and stocks
    // each non-household node. Household hearths deliberately create no
    // organization.
    // Persistence/query facade. Runtime resolution, funding, material nodes,
    // and access each have a separate subordinate owner.
    internal static class CAProvisionArrangements
    {
        internal static string ProviderKey(CARegionalSettlementRecord record,
            CAProvisionArrangement arrangement)
        {
            return record == null || arrangement == null
                ? null : arrangement.operatorIdentity;
        }

        internal static string NodeKey(CARegionalSettlementRecord record,
            CAProvisionArrangement arrangement, int node)
        {
            string provider = ProviderKey(record, arrangement);
            return provider.NullOrEmpty() ? null : provider + ":node"
                + Math.Max(0, node);
        }

        internal static bool IsDomesticIdentity(
            CARegionalSettlementRecord record, string key)
        {
            if (record == null || key.NullOrEmpty()) return false;
            if (record.domesticUnits?.Any(unit => unit != null && unit.active
                    && unit.unitIdentity == key) == true)
                return true;
            if (!key.StartsWith("pawn:", StringComparison.Ordinal)
                && !key.StartsWith("individual:",
                    StringComparison.Ordinal)) return false;
            int separator = key.IndexOf(':');
            return separator >= 0
                && int.TryParse(key.Substring(separator + 1), out int pawnId)
                && CASettlementResidenceState.Active(record, pawnId) != null;
        }
    }
}
