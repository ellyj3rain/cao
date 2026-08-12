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

    // Who operates a source of provision. Distribution changes its
    // physical setup; access and funding are recorded for supporting systems.
    public enum CAProvisionOperator : byte
    {
        // The settlement authority itself: reserve stores and
        // distribution. Tax-funded arrangements use the taxation loop.
        Authority,
        // Kitchens supplied through shared work.
        Communal,
        Household
    }

    public enum CAProvisionAccess : byte
    { Universal, Members, Charitable }

    public enum CAProvisionFunding : byte
    {
        Household,
        Taxation,
        SharedWork
    }

    public enum CAProvisionDistribution : byte
    { Household, Neighborhood, Centralized }

    // How far an arrangement's provision actually reaches [layer E].
    public enum CAProvisionReach : byte
    { Household, Settlement, Region }

    public sealed class CAProvisionArrangement : IExposable
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public int key;
        // Stable causal slot. Generated provisions are reconciled by this
        // identity, so changing access, population, or faction
        // parameters refreshes the arrangement without stacking another copy.
        public string basisKey;
        public string basisLabel;
        public CAProvisionOperator operatorKind;
        // Which population group operates this arrangement; -1 means
        // the settlement as a whole.
        public int populationGroupKey = -1;
        public CAProvisionAccess access;
        public CAProvisionFunding funding;
        public CAProvisionDistribution distribution;
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
                CAProvisionOperator.Household);
            Scribe_Values.Look(ref populationGroupKey, "populationGroupKey", -1);
            Scribe_Values.Look(ref access, "access",
                CAProvisionAccess.Universal);
            Scribe_Values.Look(ref funding, "funding",
                CAProvisionFunding.Household);
            Scribe_Values.Look(ref distribution, "distribution",
                CAProvisionDistribution.Centralized);
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
            CARegionalFactionPlan owner = plan?.FactionPlan(source?.factionKey
                ?? -1);
            if (plan == null || source == null || owner == null)
                return new List<CAProvisionArrangement>();
            // Draft authoring may complete unset generated choices. A
            // confirmed plan is evidence and validation must remain pure.
            if (!plan.confirmed)
                owner.EnsureCultureAndPolitics(plan);
            string AxisOf(CARegionalFactionPlan faction, string key) =>
                CAFactionAxes.KeyOf(faction?.factionStructure, key)
                    ?? CAFactionAxes.KeyOf(faction?.politicalBeliefs?.positions,
                        key);
            var facts = new CAProvisionCausalFacts
            {
                Support = CAFactionAxes.KeyOf(owner.factionStructure,
                    CAFactionAxes.Support),
                Ownership = AxisOf(owner, CAFactionAxes.Ownership),
                Work = AxisOf(owner, CAFactionAxes.Work),
                Pattern = plan.settlementPattern,
                Authority = (int)CARegionalSettlements.SettlementAuthorityOf(
                    plan, owner),
                Role = source.realizedRole,
                Scale = source.realizedScale,
                Access = CASettlementStartingState.Access(plan, source),
                Services = CASettlementStartingState.Services(plan, source),
                Civic = CASettlementStartingState.Civic(plan, source)
            };
            foreach (CASettlementPopulationGroup population in
                source.populationGroups ?? new List<CASettlementPopulationGroup>())
            {
                if (population == null) continue;
                int factionKey = population.politicalBeliefsFactionKey >= 0
                    ? population.politicalBeliefsFactionKey
                    : population.factionKey;
                facts.Population.Add(new CAProvisionPopulationFact
                {
                    Key = population.key,
                    OtherFaction = population.kind
                        == CAPopulationGroupKind.OtherFaction,
                    Ownership = AxisOf(plan.FactionPlan(factionKey),
                        CAFactionAxes.Ownership)
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
                    populationGroupKey = spec.PopulationGroupKey,
                    access = (CAProvisionAccess)Enum.Parse(
                        typeof(CAProvisionAccess), spec.Access),
                    funding = (CAProvisionFunding)Enum.Parse(
                        typeof(CAProvisionFunding), spec.Funding),
                    distribution = (CAProvisionDistribution)Enum.Parse(
                        typeof(CAProvisionDistribution), spec.Distribution),
                    active = true, waterSecured = true,
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
                        item.populationGroupKey, item.access, item.funding,
                        item.distribution, item.active, item.inactiveReason ?? "",
                        item.waterSecured, item.nodes, item.reach)));
            if (Signature(saved) == Signature(expected)) return true;
            failure = "the saved provision arrangements do not match the "
                + "population, adopted order, settlement authority, role, "
                + "scale, access, services, and civic state";
            return false;
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

    // Realizes the social and material remainder of provision arrangements.
    // Program materialization owns kitchens, hearths, dining assets, and
    // stores. This pass creates only real communal operators, binds authority
    // reserves to the settlement authority, reconciles taxation, and stocks
    // each non-household node. Household hearths deliberately create no
    // organization.
    internal static class CAProvisionArrangements
    {
        internal const string TaxRateSource =
            "provision-arrangements:tax-rate";
        private const string TaxRelationOriginPrefix =
            "provision-arrangements:tax:";

        internal static string TaxRelationOrigin(string providerKey)
        {
            return TaxRelationOriginPrefix + providerKey;
        }

        // Every arrangement has a stable material identity. Authority and
        // communal identities also name organizations; household identities
        // deliberately do not. A :node suffix identifies one physical node.
        internal static string ProviderKey(CARegionalSettlementRecord record,
            CAProvisionArrangement arrangement)
        {
            if (record == null || arrangement == null) return null;
            string settlementKey = record.regionalId + "#" + record.slot;
            return arrangement.operatorKind == CAProvisionOperator.Authority
                ? settlementKey
                : arrangement.operatorKind == CAProvisionOperator.Communal
                    ? settlementKey + ":prov" + arrangement.key
                    : settlementKey + ":household" + arrangement.key;
        }

        internal static string NodeKey(CARegionalSettlementRecord record,
            CAProvisionArrangement arrangement, int node)
        {
            return ProviderKey(record, arrangement) + ":node"
                + Math.Max(0, node);
        }

        internal static bool IsHouseholdIdentity(string key)
        {
            return key?.Contains(":household") == true;
        }

        internal static CAProvisionArrangement ArrangementForProvider(
            CARegionalSettlementRecord record, string providerKey)
        {
            return (record?.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                .FirstOrDefault(item => item != null && item.active
                    && (ProviderKey(record, item) == providerKey
                        || providerKey?.StartsWith(ProviderKey(record, item)
                            + ":node", StringComparison.Ordinal) == true));
        }

        // Keeps provider tax links current without rebuilding kitchens or
        // creating settlement organizations ahead of their normal seed path.
        internal static void ReconcileTaxFunding(
            CARegionalSettlementRecord record)
        {
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            if (record?.provisionArrangements == null || orgs == null) return;
            foreach (CAProvisionArrangement arrangement in
                record.provisionArrangements)
            {
                if (arrangement == null || arrangement.operatorKind
                    == CAProvisionOperator.Household) continue;
                CAOrganization provider = orgs.ByKey(ProviderKey(record,
                    arrangement));
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

        internal static void RealizeOperatorsAndStock(
            CARegionalSettlementRecord record,
            Map map,
            Func<Room, string, int, string, int> stock)
        {
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            if (record.provisionArrangements == null) return;
            bool water = WaterNear(record, map);
            foreach (IGrouping<CAProvisionOperator, CAProvisionArrangement>
                kindGroup in record.provisionArrangements
                    .Where(item => item != null && item.active)
                    .GroupBy(item => item.operatorKind))
            {
                string programKey = CASettlementProgramRegistry
                    .ProgramKeyFor(kindGroup.Key);
                CASettlementProgramEntry program = record.settlementProgram
                    ?.Entry(programKey);
                if (program == null || program.materializationState
                        != "assets placed") continue;
                bool complete = true;
                int totalNodes = 0;
                int totalStocked = 0;
                foreach (CAProvisionArrangement arrangement in kindGroup)
                {
                    arrangement.waterSecured = water;
                    string identity = ProviderKey(record, arrangement);
                    CASettlementPopulationGroup populationGroup =
                        arrangement.populationGroupKey < 0 ? null
                        : record.populationGroups.FirstOrDefault(item =>
                            item != null && item.key
                                == arrangement.populationGroupKey);
                    CAOrganization op = null;
                    if (arrangement.operatorKind
                        != CAProvisionOperator.Household)
                    {
                        string orgName = (populationGroup != null
                                ? populationGroup.label + " " : "")
                            + arrangement.OperatorWords + " of "
                            + (record.name ?? "the settlement");
                        op = arrangement.operatorKind
                                == CAProvisionOperator.Authority
                            ? orgs?.ByKey(identity)
                            : orgs?.EnsureFor(identity, orgName,
                                "established with the settlement; "
                                + arrangement.access.ToString().ToLower()
                                + " access, funded by "
                                + arrangement.FundingWords,
                                CAOrganizationKind.Group);
                        if (op == null)
                        {
                            complete = false;
                            continue;
                        }
                        ReconcileTaxFunding(record, arrangement, op);
                    }
                    int nodes = Mathf.Clamp(arrangement.nodes, 1, 3);
                    int stockPer = ((arrangement.operatorKind
                            == CAProvisionOperator.Authority ? 60 : 40)
                        + (arrangement.reach == CAProvisionReach.Region
                            ? 40 : 0)) / nodes;
                    if (!water) stockPer /= 2;
                    int stocked = 0;
                    for (int n = 0; n < nodes; n++)
                    {
                        string nodeKey = NodeKey(record, arrangement, n);
                        Room room = ProgramRoom(record, map, nodeKey);
                        int placed = stock(room, "Pemmican", stockPer,
                            identity);
                        stocked += placed;
                        totalNodes++;
                        if (placed != 1) complete = false;
                    }
                    totalStocked += stocked;
                    op?.Record("provisioning", arrangement.Summary
                        + " - " + nodes + " node"
                        + (nodes == 1 ? "" : "s") + " raised"
                        + (populationGroup?.quarter == true
                            ? " in its own quarter" : "") + ", "
                        + stocked + " stocked reserve"
                        + (stocked == 1 ? "" : "s"));
                    if (!water)
                        op?.Record("provisioning", "no secured water "
                            + "within reach - the kitchens run on half "
                            + "stores until a source is secured");
                }
                program.materializationState = complete && totalNodes > 0
                    && totalStocked == totalNodes
                        ? "institutions pending" : "blocked";
                if (program.materializationState == "blocked"
                    && program.blocker.NullOrEmpty())
                    program.blocker = "the saved provision operators, node "
                        + "identity, funding, and stocked reserves could not "
                        + "all be realized";
            }
        }

        private static Room ProgramRoom(CARegionalSettlementRecord record,
            Map map, string nodeKey)
        {
            if (map == null || record?.seededAssets == null
                || nodeKey.NullOrEmpty()) return null;
            foreach (string asset in record.seededAssets)
            {
                string[] parts = asset.Split('|');
                if (parts.Length < 5 || parts[4] != nodeKey) continue;
                if (!int.TryParse(parts[1], out int x)
                    || !int.TryParse(parts[2], out int z)) continue;
                IntVec3 cell = new IntVec3(x, 0, z);
                if (cell.InBounds(map)) return cell.GetRoom(map);
            }
            return null;
        }

        // Tax-funded support has both halves of the taxation loop: the
        // provider's policy and a relation naming the settlement treasury
        // that pays it. Either half alone is only descriptive state.
        private static void ReconcileTaxFunding(
            CARegionalSettlementRecord record,
            CAProvisionArrangement arrangement, CAOrganization provider)
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
            bool selfFundedAuthority = provider.organizationKey == payerKey;
            bool linked = selfFundedAuthority || ledger != null && ledger
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

        // Final completion gate. Physical nodes and stock are necessary but
        // insufficient: non-household provision must also have its operator,
        // provider-bound holdings, enough active workers, and a realized
        // funding path. Household provision proves household-bound holdings
        // and stock without inventing an organization.
        internal static void CompleteMaterialization(
            CARegionalSettlementRecord record, Map map)
        {
            if (record?.settlementProgram == null || map == null) return;
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (IGrouping<CAProvisionOperator, CAProvisionArrangement>
                kindGroup in (record.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                    .Where(item => item != null && item.active)
                    .GroupBy(item => item.operatorKind))
            {
                CASettlementProgramEntry program = record.settlementProgram
                    .Entry(CASettlementProgramRegistry.ProgramKeyFor(
                        kindGroup.Key));
                if (program == null || program.materializationState
                        != "institutions pending") continue;
                bool complete = true;
                foreach (CAProvisionArrangement arrangement in kindGroup)
                {
                    string providerKey = ProviderKey(record, arrangement);
                    int nodes = Mathf.Clamp(arrangement.nodes, 1, 3);
                    int stocked = (record.startingStock
                            ?? new List<CAStartingStockRecord>())
                        .Count(item => item != null
                            && item.providerOrgKey == providerKey
                            && item.thingId >= 0);
                    List<CAFacilityHolding> holdings = ledger?.Holdings
                        .Where(item => item != null && item.mapId == map.uniqueID
                            && (item.operatorOrgKey == providerKey
                                || item.ownerOrgKey == providerKey))
                        .ToList() ?? new List<CAFacilityHolding>();
                    bool stockAndAccess = stocked >= nodes
                        && holdings.Count > 0
                        && holdings.All(item => !item.allocationRule.NullOrEmpty());
                    if (arrangement.operatorKind
                        == CAProvisionOperator.Household)
                    {
                        complete &= orgs?.ByKey(providerKey) == null
                            && stockAndAccess;
                        continue;
                    }
                    CAOrganization provider = orgs?.ByKey(providerKey);
                    bool staffed = provider != null
                        && provider.memberPawnIds.Distinct().Count() >= nodes;
                    bool funded = FundingRealized(record, arrangement,
                        provider, ledger, now);
                    complete &= provider != null && stockAndAccess
                        && staffed && funded;
                }
                program.materializationState = complete
                    ? "materialized" : "blocked";
                if (!complete && program.blocker.NullOrEmpty())
                    program.blocker = "the provision program lacks a complete "
                        + "operator, access, staffing, funding, or stocked-node "
                        + "contract";
            }
        }

        private static bool FundingRealized(
            CARegionalSettlementRecord record,
            CAProvisionArrangement arrangement, CAOrganization provider,
            CAOrganizationRelationsWorldComponent ledger, int now)
        {
            if (arrangement.funding == CAProvisionFunding.Household)
                return arrangement.operatorKind
                    == CAProvisionOperator.Household;
            if (provider == null) return false;
            if (arrangement.funding == CAProvisionFunding.SharedWork)
                return provider.memberPawnIds.Count >= Math.Max(1,
                    arrangement.nodes) && ledger?.RelationsIn(
                        provider.organizationKey).Any(relation =>
                            relation != null && !relation.Expired(now)
                            && relation.Delegates(CAResponsibilities.Work))
                        == true;
            if (arrangement.funding == CAProvisionFunding.Taxation)
            {
                bool policy = provider.policies.Any(item => item != null
                    && item.key == "tax rate" && item.generatedBy
                        == TaxRateSource);
                bool self = provider.organizationKey == record.regionalId
                    + "#" + record.slot;
                bool link = self || ledger?.RelationsIn(
                    provider.organizationKey).Any(relation =>
                        relation != null && !relation.Expired(now)
                        && relation.Delegates(CAResponsibilities.Taxes))
                    == true;
                return policy && link;
            }
            return false;
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
