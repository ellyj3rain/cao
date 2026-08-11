using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Faction structure determines who decides, who may take part, and how a
    // decision becomes binding. Settlement records carry membership and rank.
    // Ownership and work choices control stores, rations, sales, assigned
    // stock, wages, maintenance, and keep through their existing ledgers.
    internal static class CAAxisMaterialization
    {
        internal static void Apply(CAOrganization org,
            CARegionalSettlementRecord record, Map map)
        {
            try
            {
                if (org == null || record?.faction == null || map == null)
                    return;
                CAFactionState factionState = CAFactionStateWorldComponent.Current
                    ?.Find(record.faction);
                if (factionState == null) return;

                List<Pawn> residents = ResidentsOf(record, map);
                string status = Axis(factionState,
                    CAFactionAxes.Status);
                // Persist status assignments and settlement wealth first.
                CAStatusAssignmentSet statusSet = CAStatusAssignments.Ensure(record,
                    residents, status);

                // Membership determines participation, member-only access,
                // and who may serve as a guard.
                Membership(org, factionState, record, residents);
                IssueCoin(org, factionState, record, residents, statusSet);
                GoverningBody(org, factionState, record, residents, status,
                    statusSet);
                Holdings(org, factionState, record, map, residents);
                WorkRules(org, factionState, record, map, residents,
                    statusSet);
                LocalOrderAndDefense(org, factionState, record, map, residents,
                    status, statusSet);
                RecordBeliefConflicts(org, factionState, record);
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Axes] materialization failed for "
                    + (record?.name ?? "?") + ": " + e.Message);
            }
        }

        private static string Axis(CAFactionState factionState, string key)
        {
            return CAFactionAxes.KeyOf(factionState.factionStructure, key);
        }

        private static CAAxisEntry AxisEntry(List<CAAxisEntry> structure,
            CAPoliticalBeliefs beliefs, string key)
        {
            CAAxisEntry entry = CAFactionAxes.EntryOf(structure, key);
            return entry != null && !entry.optionKey.NullOrEmpty()
                ? entry : CAFactionAxes.EntryOf(beliefs?.positions, key);
        }

        private static CAOrigin TermsOrigin(CAAxisEntry entry,
            string origin)
        {
            CAAxisSource source = entry == null ? CAAxisSource.Unset
                : (CAAxisSource)entry.source;
            return source == CAAxisSource.Authored
                ? CAOrigin.Authored(origin) : CAOrigin.Derived(origin);
        }

        private static List<Pawn> ResidentsOf(
            CARegionalSettlementRecord record, Map map)
        {
            return CAPopulationProjection.Residents(record, map);
        }

        private static int SkillTotal(Pawn pawn)
        {
            if (pawn?.skills?.skills == null) return 0;
            int total = 0;
            foreach (SkillRecord skill in pawn.skills.skills)
                if (skill != null && !skill.TotallyDisabled)
                    total += skill.Level;
            return total;
        }

        // ---- personal funds ---------------------------------------------

        // Personal silver follows settlement wealth, economy, and status.
        private static void IssueCoin(CAOrganization org,
            CAFactionState factionState, CARegionalSettlementRecord record,
            List<Pawn> residents, CAStatusAssignmentSet statusSet)
        {
            string economy = Axis(factionState, CAFactionAxes.Economy);
            if (economy == "communal")
            {
                org.Record("economy", "no personal silver; food and supplies"
                    + " are shared");
                return;
            }
            float scale = economy == "planned" ? 0.5f : 1f;
            int[] baseByWealth = { 3, 8, 15, 25 };
            int wealthBand = Math.Max(0, Math.Min(3, record.wealth));
            int issued = 0;
            Rand.PushState(Gen.HashCombineInt(GenText.StableStringHash(
                (record.regionalId ?? "r") + ":coin"), record.slot * 53));
            try
            {
                foreach (Pawn resident in residents)
                {
                    if (resident.inventory?.innerContainer == null)
                        continue;
                    if (resident.inventory.innerContainer.Any(t =>
                        t.def == ThingDefOf.Silver)) continue; // once
                    int amount = (int)Math.Round(baseByWealth[wealthBand]
                        * scale * Rand.Range(0.7f, 1.3f));
                    if (statusSet.Elite.Contains(resident.thingIDNumber))
                        amount *= 2;
                    if (amount <= 0) continue;
                    Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = amount;
                    if (resident.inventory.innerContainer.TryAdd(silver))
                        issued += amount;
                    else silver.Destroy();
                }
            }
            finally { Rand.PopState(); }
            if (issued > 0)
                org.Record("economy", "personal coin in circulation: "
                    + issued + " silver across " + residents.Count
                    + " resident(s) (settlement wealth " + record.wealth
                    + ")");
        }

        // ---- leadership, participation, and decisions --------------------

        private static void GoverningBody(CAOrganization org,
            CAFactionState factionState, CARegionalSettlementRecord record,
            List<Pawn> residents, string status, CAStatusAssignmentSet statusSet)
        {
            string leadership = Axis(factionState, CAFactionAxes.Leadership);
            string decisions = Axis(factionState, CAFactionAxes.Decisions);
            string participation = Axis(factionState, CAFactionAxes.Participation);
            string dissent = Axis(factionState, CAFactionAxes.Dissent);
            if (decisions != null) SetPolicy(org, "decisions", decisions);
            if (participation != null) SetPolicy(org, "participation", participation);
            if (dissent != null) SetPolicy(org, "dissent", dissent);
            if (leadership != null) SetPolicy(org, "leadership", leadership);
            if (status != null) SetPolicy(org, "status", status);

            // Seat holders follow recorded status: hereditary houses, castes,
            // demonstrated skill, or no rank order.
            List<Pawn> seatable;
            string succession;
            if (statusSet.Stratified)
            {
                seatable = residents.Where(p =>
                    statusSet.Elite.Contains(p.thingIDNumber)).ToList();
                if (seatable.Count == 0) seatable = residents;
                succession = "hereditary";
            }
            else if (status == "earned")
            {
                seatable = residents
                    .OrderByDescending(SkillTotal).ToList();
                succession = "merit";
                if (seatable.Count > 0)
                    org.Record("standing", "standing is earned - seats "
                        + "follow demonstrated skill, voice follows "
                        + "office and service");
            }
            else
            {
                seatable = residents;
                succession = "merit";
            }

            int cursor = 0;
            if (leadership == "single")
                Seat(org, seatable, ref cursor, "axis-executive",
                    "Executive of " + (record.name ?? "the town"), 900,
                    CAResponsibilities.Decisions + "," + CAResponsibilities.ArmedForce + ","
                    + CAResponsibilities.Disputes, succession);
            else if (leadership == "council")
                for (int i = 0; i < 3; i++)
                    Seat(org, seatable, ref cursor,
                        "axis-councillor-" + i, "Councillor", 700,
                        CAResponsibilities.Decisions, succession);
            else if (leadership == "federated")
                Seat(org, seatable, ref cursor, "axis-delegate",
                    "Delegate upward", 600, CAResponsibilities.Diplomacy,
                    succession);
            else if (leadership == "whole" || leadership == "none")
                org.Record("leadership", leadership == "whole"
                    ? "all members decide; no permanent offices"
                    : "no permanent leader");
            if (statusSet.Stratified && (leadership == "single"
                || leadership == "council" || leadership == "federated"))
                org.Record("leadership", "seats are reserved to the "
                    + (status == "castes" ? "ruling caste"
                        : "noble houses") + " - " + statusSet.Elite.Count
                    + " of " + residents.Count + " residents");

            // Tax-funded starting provisions create a tax rate for each
            // provider. The settlement makes one opening decision for those
            // policies.
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            var taxProviders = new List<CAOrganization>();
            foreach (CAStartingProvision arrangement in
                record.startingProvisions ?? new List<CAStartingProvision>())
            {
                if (arrangement == null || arrangement.funding
                    != CAProvisionFunding.Taxation) continue;
                CAOrganization provider = orgs?.ByKey(record.regionalId
                    + "#" + record.slot + ":prov" + arrangement.key);
                if (provider != null && provider.policies.Any(policy =>
                    policy != null && policy.key == "tax rate"
                    && policy.generatedBy
                        == CAStartingProvisions.TaxRateSource))
                    taxProviders.Add(provider);
            }
            if (taxProviders.Count == 0) return;
            CADecisionOutcome outcome = CAOrganizationDecisions.Decide(
                org, factionState,
                record, residents, "tax-funded starting provisions");
            if (!outcome.adopted)
            {
                foreach (CAOrganization provider in taxProviders)
                {
                    provider.policies.RemoveAll(policy => policy != null
                        && policy.key == "tax rate"
                        && policy.generatedBy
                            == CAStartingProvisions.TaxRateSource);
                    CAStartingProvisions.RefuseTaxFunding(
                        provider.organizationKey);
                    provider.Record("decision", "starting tax rate "
                        + "refused by the settlement - " + outcome.words);
                }
                foreach (CAStartingProvision arrangement in
                    record.startingProvisions)
                    if (arrangement != null && arrangement.funding
                        == CAProvisionFunding.Taxation)
                        arrangement.funding = arrangement.operatorKind
                            == CAProvisionOperator.Communal
                                ? CAProvisionFunding.SharedWork
                                : CAProvisionFunding.Dues;
                org.Record("decision", "tax rate refused - "
                    + outcome.words + "; starting provisions now use dues "
                    + "or shared work");
            }
            else
            {
                foreach (CAOrganization provider in taxProviders)
                    provider.Record("decision", "starting tax rate "
                        + "adopted by the settlement - " + outcome.words);
                org.Record("decision", "tax rate adopted - "
                    + outcome.words);
            }
        }

        private static void Seat(CAOrganization org, List<Pawn> residents,
            ref int cursor, string sourceKey, string name, int seniority,
            string grants, string succession = "merit")
        {
            if (org.offices.Any(o => o != null
                && o.sourceKey == sourceKey)) return;
            var office = new CAOffice
            {
                sourceKey = sourceKey,
                name = name,
                seniority = seniority,
                grants = grants,
                successionRule = succession
            };
            if (cursor < residents.Count)
            {
                Pawn holder = residents[cursor++];
                office.holderId = holder.thingIDNumber;
                office.holderLabel = holder.LabelShortCap;
            }
            org.offices.Add(office);
            org.Record("leadership", name + (office.holderId >= 0
                ? " seated: " + office.holderLabel : " stands vacant"));
        }

        internal static void SetPolicy(CAOrganization org, string key,
            string value)
        {
            CAPolicyRecord policy = org.policies.FirstOrDefault(p =>
                p != null && p.key == key);
            if (policy == null)
                org.policies.Add(new CAPolicyRecord
                {
                    key = key,
                    value = value,
                    adoptedTick = Find.TickManager.TicksGame
                });
            else policy.value = value;
        }

        // ---- ownership and economy --------------------------------------

        private static void Holdings(CAOrganization org,
            CAFactionState factionState, CARegionalSettlementRecord record,
            Map map, List<Pawn> residents)
        {
            string ownership = Axis(factionState, CAFactionAxes.Ownership);
            string economy = Axis(factionState,
                CAFactionAxes.Economy);
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null || record.seededAssets == null) return;

            string hostAllocation =
                economy == "market" ? CAAllocationRules.Sale
                : economy == "planned" ? CAAllocationRules.Assignment
                : economy == "communal" ? CAAllocationRules.OpenAccess
                : CAAllocationRules.Ration;
            string holdingOrigin = "axis:ownership:" + org.organizationKey;
            ledger.ClearDerived(holdingOrigin);
            int written = 0;
            var posts = new List<CAFacilityHolding>();
            foreach (string asset in record.seededAssets)
            {
                string[] parts = asset.Split('|');
                if (parts.Length < 3) continue;
                bool productive = parts[0] == "FueledStove"
                    || parts[0] == "Campfire" || parts[0] == "Shelf"
                    || parts[0] == "FueledSmithy"
                    || parts[0] == "CraftingSpot"
                    || parts[0] == "TableButcher";
                if (!productive) continue;
                int x, z;
                if (!int.TryParse(parts[1], out x)
                    || !int.TryParse(parts[2], out z)) continue;
                var cell = new IntVec3(x, 0, z);
                if (!cell.InBounds(map)) continue;
                Thing thing = cell.GetThingList(map).FirstOrDefault(t =>
                    t.def.defName == parts[0]);
                if (thing == null) continue;
                string providerKey = parts.Length >= 5 ? parts[4] : null;
                CAStartingProvision provision = providerKey.NullOrEmpty()
                    ? null : (record.startingProvisions
                        ?? new List<CAStartingProvision>()).FirstOrDefault(item =>
                            item != null && providerKey == record.regionalId
                                + "#" + record.slot + ":prov" + item.key);
                string assetAllocation = AllocationForProvision(provision,
                    hostAllocation);
                string assetOwnership = OwnershipForProvision(record, map,
                    provision, ownership);
                List<Pawn> assetResidents = provision != null
                        && provision.populationGroupKey >= 0
                    ? CAPopulationProjection.ResidentsInPopulationGroup(record,
                        map, provision.populationGroupKey)
                    : residents;

                var holding = new CAFacilityHolding
                {
                    thingId = thing.thingIDNumber,
                    mapId = map.uniqueID,
                    cell = cell,
                    facilityKind = parts[0] == "Shelf" ? "stores"
                        : parts[0] == "FueledSmithy"
                            || parts[0] == "CraftingSpot" ? "workshop"
                        : "kitchen",
                    allocationRule = assetAllocation,
                    operatorOrgKey = providerKey.NullOrEmpty()
                        ? org.organizationKey : providerKey,
                    origin = CAOrigin.Derived(holdingOrigin)
                };
                switch (assetOwnership)
                {
                    case "common":
                        holding.ownerOrgKey = provision != null
                                && provision.populationGroupKey >= 0
                            ? providerKey : org.organizationKey;
                        holding.capitalSource =
                            CACapitalSources.SharedWork;
                        break;
                    case "cooperative":
                        holding.ownerOrgKey = providerKey.NullOrEmpty()
                            ? org.organizationKey : providerKey;
                        holding.capitalSource = CACapitalSources.Dues;
                        holding.oversight = CAOversightKinds.GuildRule;
                        break;
                    case "state":
                        holding.ownerOrgKey = "faction:"
                            + record.faction.loadID;
                        holding.capitalSource = CACapitalSources.Treasury;
                        holding.oversight = CAOversightKinds.PublicOffice;
                        holding.liability = CALiabilityKinds.Treasury;
                        break;
                    default:
                        Pawn keeper = assetResidents.Count > 0
                            ? assetResidents[written % assetResidents.Count]
                            : null;
                        holding.ownerPawnId = keeper?.thingIDNumber ?? -1;
                        holding.capitalSource = CACapitalSources.Private;
                        break;
                }
                if (ledger.Add(holding))
                {
                    written++;
                    posts.Add(holding);
                }
            }
            if (written > 0)
                org.Record("economy", written + " productive holding(s) "
                    + "under " + (ownership ?? "unspecified")
                    + " tenure with provider-specific allocation");

            AllocateProviderStores(org, record, map, hostAllocation,
                residents, posts);
        }

        private static string OwnershipForProvision(
            CARegionalSettlementRecord record, Map map,
            CAStartingProvision provision, string fallback)
        {
            if (provision == null || provision.populationGroupKey < 0)
                return fallback;
            CASettlementPopulationGroup populationGroup = record.populationGroups?
                .FirstOrDefault(item => item != null
                    && item.key == provision.populationGroupKey);
            if (populationGroup == null) return fallback;
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            int factionKey = populationGroup.politicalBeliefsFactionKey >= 0
                ? populationGroup.politicalBeliefsFactionKey
                : populationGroup.factionKey;
            CARegionalFactionPlan faction = plan?.FactionPlan(factionKey);
            return CAFactionAxes.KeyOf(faction?.factionStructure,
                    CAFactionAxes.Ownership)
                ?? CAFactionAxes.KeyOf(faction?.politicalBeliefs?.positions,
                    CAFactionAxes.Ownership)
                ?? fallback;
        }

        private static string AllocationForProvision(
            CAStartingProvision provision, string fallback)
        {
            if (provision == null) return fallback;
            if (provision.operatorKind == CAProvisionOperator.Vendor
                || provision.access == CAProvisionAccess.Fee
                || provision.funding == CAProvisionFunding.Fees)
                return CAAllocationRules.Sale;
            if (provision.operatorKind == CAProvisionOperator.Communal)
                return provision.access == CAProvisionAccess.Universal
                    ? CAAllocationRules.OpenAccess : CAAllocationRules.Ration;
            if (provision.operatorKind == CAProvisionOperator.Authority
                || provision.operatorKind == CAProvisionOperator.Religious
                || provision.operatorKind == CAProvisionOperator.Household)
                return CAAllocationRules.Ration;
            return fallback;
        }

        private static void AllocateProviderStores(CAOrganization settlement,
            CARegionalSettlementRecord record, Map map,
            string hostAllocation, List<Pawn> residents,
            List<CAFacilityHolding> posts)
        {
            if (record.startingStock == null
                || record.startingStock.Count == 0) return;

            var stockByThingId = record.startingStock
                .Where(item => item != null && item.thingId >= 0)
                .GroupBy(item => item.thingId)
                .ToDictionary(group => group.Key, group => group.First());
            if (stockByThingId.Count == 0) return;
            var thingsById = map.listerThings.AllThings
                .Where(thing => thing != null
                    && stockByThingId.ContainsKey(thing.thingIDNumber)
                    && thing.def.IsNutritionGivingIngestible)
                .GroupBy(thing => thing.thingIDNumber)
                .ToDictionary(group => group.Key, group => group.First());
            if (thingsById.Count == 0) return;

            CAOrganizationWorldComponent organizations =
                CAOrganizationWorldComponent.Current;
            foreach (IGrouping<string, CAStartingStockRecord> group in
                stockByThingId.Values.Where(item =>
                        thingsById.ContainsKey(item.thingId))
                    .GroupBy(item => item.providerOrgKey.NullOrEmpty()
                        ? settlement.organizationKey : item.providerOrgKey))
            {
                string providerKey = group.Key;
                CAOrganization provider = organizations?.ByKey(providerKey)
                    ?? settlement;
                CAStartingProvision provision = (record.startingProvisions
                        ?? new List<CAStartingProvision>())
                    .FirstOrDefault(item => item != null
                        && providerKey == record.regionalId + "#"
                            + record.slot + ":prov" + item.key);
                List<Pawn> providerResidents = ProvisionConsumers(settlement,
                    record, map, provision, residents);
                List<Thing> providerStores = group
                    .Select(item => thingsById[item.thingId])
                    .Distinct().ToList();
                List<CAFacilityHolding> providerPosts = (posts
                        ?? new List<CAFacilityHolding>())
                    .Where(post => post != null
                        && post.facilityKind != "workshop"
                        && (post.operatorOrgKey.NullOrEmpty()
                            ? settlement.organizationKey
                            : post.operatorOrgKey) == providerKey).ToList();
                AllocateStores(provider, record, map,
                    AllocationForProvision(provision, hostAllocation),
                    providerResidents, providerPosts, providerStores);
            }
        }

        private static List<Pawn> ProvisionConsumers(
            CAOrganization settlement, CARegionalSettlementRecord record,
            Map map, CAStartingProvision provision, List<Pawn> residents)
        {
            if (provision != null && provision.populationGroupKey >= 0)
                return CAPopulationProjection.ResidentsInPopulationGroup(record,
                    map, provision.populationGroupKey);
            if (provision == null
                || provision.access == CAProvisionAccess.Members)
                return residents.Where(resident =>
                    settlement.memberPawnIds.Count == 0
                    || settlement.memberPawnIds.Contains(
                        resident.thingIDNumber)).ToList();
            return residents;
        }

        private static void AllocateStores(CAOrganization org,
            CARegionalSettlementRecord record, Map map, string allocation,
            List<Pawn> residents, List<CAFacilityHolding> posts,
            List<Thing> stacks)
        {
            if (stacks == null || stacks.Count == 0) return;
            switch (allocation)
            {
                case CAAllocationRules.OpenAccess:
                    foreach (Thing stack in stacks)
                        stack.SetForbidden(false, false);
                    org.Record("provisions", "the stores are open - "
                        + "anyone may take from the common stock");
                    break;
                case CAAllocationRules.Ration:
                    // The caller supplies the provision's eligible population:
                    // settlement members, a named population group, or all
                    // residents when access is universal or charitable.
                    int handed = 0;
                    foreach (Pawn resident in residents)
                    {
                        Thing source = stacks.FirstOrDefault(t =>
                            t.stackCount > 2 && t.Spawned);
                        if (source == null) break;
                        Thing portion = source.SplitOff(2);
                        if (resident.inventory != null
                            && resident.inventory.innerContainer
                                .TryAdd(portion))
                            handed++;
                        else portion.Destroy();
                    }
                    if (handed > 0)
                        org.Record("provisions", "rations handed out - "
                            + handed + " recipient(s) carry their portion"
                            + "; the store keeps the remainder");
                    break;
                case CAAllocationRules.Sale:
                    SetPolicy(org, "stores", "sale");
                    MarketDay(org, record, map, residents, stacks);
                    break;
                default:
                    AssignToPosts(org, record, map, stacks, posts);
                    break;
            }
        }

        // Market sales move a buyer's silver to the till and goods to the
        // buyer through the transaction ledger. Failed purchases are recorded,
        // and the till settles into the organization's treasury.
        private static void MarketDay(CAOrganization org,
            CARegionalSettlementRecord record, Map map,
            List<Pawn> residents, List<Thing> stacks)
        {
            CATransactionLedger ledger = CATransactionLedger.Current;
            if (ledger == null) return;
            var till = new ThingOwner<Thing>();
            CAParty seller = CAParty.Of(org);
            int sold = 0;
            int refused = 0;
            int acts = 0;
            foreach (Pawn buyer in residents)
            {
                if (acts >= 10) break;
                Thing stock = stacks.FirstOrDefault(t =>
                    t.Spawned && t.stackCount > 2);
                if (stock == null) break;
                acts++;
                CATransaction sale = ledger.ExecuteSale(buyer, till,
                    stock, 2, seller, seller, seller, null,
                    "starting market of "
                    + (record.name ?? org.organizationKey));
                if (sale != null && sale.settlementStatus == "settled")
                    sold++;
                else refused++;
            }
            int takings = 0;
            for (int i = 0; i < till.Count; i++)
                if (till[i]?.def == ThingDefOf.Silver)
                    takings += till[i].stackCount;
            if (takings > 0) org.treasury += takings;
            till.ClearAndDestroyContents();
            if (sold > 0 || refused > 0)
                org.Record("economy", "market held - " + sold
                    + " purchase(s), " + takings
                    + " silver taken into the treasury"
                    + (refused > 0
                        ? "; " + refused + " buyer(s) refused for want "
                            + "of coin" : "")
                    + "; every act stands in the transaction ledger");
        }

        // Under planned distribution, stock is carried to staffed posts,
        // kept under custody, and recorded as a transfer.
        private static void AssignToPosts(CAOrganization org,
            CARegionalSettlementRecord record, Map map,
            List<Thing> stacks, List<CAFacilityHolding> posts)
        {
            SetPolicy(org, "stores", "assignment");
            if (posts == null || posts.Count == 0)
            {
                org.Record("provisions", "stores assigned by "
                    + "plan, but no staffed post stands to receive them "
                    + "yet");
                return;
            }
            int moved = 0;
            int value = 0;
            int cursor = 0;
            foreach (Thing stack in stacks)
            {
                if (moved >= 12) break;
                if (stack == null || !stack.Spawned) continue;
                CAFacilityHolding post = posts[cursor++ % posts.Count];
                if (stack.Position.InHorDistOf(post.cell, 3f))
                {
                    stack.SetForbidden(true, false);
                    continue;
                }
                stack.DeSpawn();
                if (GenPlace.TryPlaceThing(stack, post.cell, map,
                    ThingPlaceMode.Near))
                {
                    moved++;
                    value += (int)Math.Ceiling(
                        stack.MarketValue * stack.stackCount);
                    stack.SetForbidden(true, false);
                }
                else
                    GenPlace.TryPlaceThing(stack,
                        record.localRect.CenterCell, map,
                        ThingPlaceMode.Near);
            }
            if (moved > 0)
            {
                CATransactionLedger.Current?.ExecuteTransfer(
                    CAParty.Of(org),
                    CAParty.OfSettlement(org.organizationKey + ":posts",
                        "the staffed posts of "
                        + (record.name ?? "the town")),
                    CATransferCause.Custody,
                    "stores directed to the posts by plan", moved, value,
                    authorityClaimed: true, consentGiven: true,
                    compensationOwed: 0, compensationBasis: null,
                    dueTick: -1,
                    provenance: "axis:economy=planned");
                org.Record("provisions", "stores assigned: "
                    + moved + " stock(s) carried to the staffed posts "
                    + "and held under custody there");
            }
        }

        // Work relations used by taxation and other obligation consumers.

        private static void WorkRules(CAOrganization org,
            CAFactionState factionState, CARegionalSettlementRecord record,
            Map map, List<Pawn> residents,
            CAStatusAssignmentSet statusSet)
        {
            string work = Axis(factionState, CAFactionAxes.Work);
            if (work == null) return;
            SetPolicy(org, "work", work);
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (orgs == null) return;

            int staffed = 0;
            int related = 0;
            var assigned = new HashSet<int>();
            foreach (CAStartingProvision arrangement in
                record.startingProvisions ?? new List<CAStartingProvision>())
            {
                if (arrangement == null || arrangement.operatorKind
                    == CAProvisionOperator.Household) continue;
                CAOrganization op = orgs.ByKey(record.regionalId + "#"
                    + record.slot + ":prov" + arrangement.key);
                if (op == null) continue;
                CARegionalFactionPlan providerFaction = FactionForProvision(
                    record, map, arrangement);
                CAAxisEntry providerWorkEntry = AxisEntry(
                    providerFaction?.factionStructure,
                    providerFaction?.politicalBeliefs, CAFactionAxes.Work);
                CAAxisEntry workEntry = providerWorkEntry ?? AxisEntry(
                    factionState.factionStructure,
                    factionState.politicalBeliefs, CAFactionAxes.Work);
                string providerWork = providerWorkEntry?.optionKey ?? work;
                string compensation = providerWork == "contract"
                        || providerWork == "organized"
                    ? CACompensationKinds.Wage
                    : providerWork == "duty"
                        ? CACompensationKinds.Maintenance
                        : CACompensationKinds.Keep;
                List<Pawn> candidates = arrangement.populationGroupKey >= 0
                    ? CAPopulationProjection.ResidentsInPopulationGroup(record,
                        map, arrangement.populationGroupKey)
                    : residents.ToList();
                if (statusSet.Stratified
                    && candidates.Count > statusSet.Elite.Count)
                    candidates = candidates.Where(pawn => !statusSet.Elite
                            .Contains(pawn.thingIDNumber))
                        .OrderBy(pawn => statusSet.Arms.Contains(
                            pawn.thingIDNumber) ? 1 : 0).ToList();
                candidates = candidates
                    .OrderBy(pawn => assigned.Contains(pawn.thingIDNumber)
                        ? 1 : 0)
                    .ThenBy(pawn => pawn.thingIDNumber).ToList();
                string relationOrigin = "axis:work:"
                    + op.organizationKey;
                ledger?.ClearDerived(relationOrigin);
                int want = Math.Max(1, arrangement.nodes);
                for (int i = 0; i < want && i < candidates.Count; i++)
                {
                    Pawn worker = candidates[i];
                    assigned.Add(worker.thingIDNumber);
                    if (!op.memberPawnIds.Contains(worker.thingIDNumber))
                    {
                        op.memberPawnIds.Add(worker.thingIDNumber);
                        staffed++;
                    }
                    // The work relation is what personal taxation and later
                    // obligation consumers read: wage for
                    // contract/organized, bed-and-board for duty, keep
                    // for household work.
                    if (ledger != null && ledger.Add(new CARelation
                    {
                        orgKey = op.organizationKey,
                        partyPawnId = worker.thingIDNumber,
                        partyKind = CARelationPartyKind.Pawn,
                        role = "worker",
                        status = "active",
                        compensation = compensation,
                        compensationRate = compensation
                            == CACompensationKinds.Wage ? 1f : 0f,
                        origin = CAOrigin.Derived(relationOrigin),
                        termsOrigin = TermsOrigin(workEntry, relationOrigin),
                        delegatedResponsibilities = new List<string>
                            { CAResponsibilities.Work }
                    })) related++;
                }
                SetPolicy(op, "work", providerWork);
                CAPoliticalBeliefPractice.ReconcileCurrentStructure(op,
                    providerFaction?.factionStructure
                        ?? factionState.factionStructure);
            }

            if (work == "organized" && staffed > 0)
            {
                CAOrganization union = orgs.EnsureFor(
                    org.organizationKey + ":work",
                    "Workers of " + (record.name ?? "the town"),
                    "organized work of the settlement",
                    CAOrganizationKind.Group);
                foreach (CAStartingProvision arrangement in
                    record.startingProvisions)
                {
                    if (arrangement == null) continue;
                    CAOrganization op = orgs.ByKey(record.regionalId + "#"
                        + record.slot + ":prov" + arrangement.key);
                    if (op == null) continue;
                    foreach (int id in op.memberPawnIds)
                        if (!union.memberPawnIds.Contains(id))
                            union.memberPawnIds.Add(id);
                }
                union.Record("work", "the workers' group staffs the posts");
            }
            if (staffed > 0)
                org.Record("work", staffed + " resident(s) staff the "
                    + "posts under " + work + " rules; " + related
                    + " work relation(s) recorded");
        }

        private static CARegionalFactionPlan FactionForProvision(
            CARegionalSettlementRecord record, Map map,
            CAStartingProvision provision)
        {
            if (provision == null || provision.populationGroupKey < 0)
                return null;
            CASettlementPopulationGroup populationGroup = record.populationGroups?
                .FirstOrDefault(item => item != null
                    && item.key == provision.populationGroupKey);
            if (populationGroup == null) return null;
            int factionKey = populationGroup.politicalBeliefsFactionKey >= 0
                ? populationGroup.politicalBeliefsFactionKey
                : populationGroup.factionKey;
            return CARegionalWorldComponent.Current?.FindRegionForMap(map)
                ?.FactionPlan(factionKey);
        }

        // ---- membership -------------------------------------------------

        private static void Membership(CAOrganization org,
            CAFactionState factionState, CARegionalSettlementRecord record,
            List<Pawn> residents)
        {
            string membershipRule = Axis(factionState,
                CAFactionAxes.Membership);
            if (membershipRule != null)
                SetPolicy(org, "membership", membershipRule);
            if (membershipRule == null) return;

            bool restrictive = membershipRule == "hereditary"
                || membershipRule == "closed";
            var dominantIds = new HashSet<int>();
            foreach (string entry in record.populationAssignments
                ?? new List<string>())
            {
                string[] parts = entry.Split(':');
                int populationGroupKey, pawnId;
                if (parts.Length != 2
                    || !int.TryParse(parts[0], out populationGroupKey)
                    || !int.TryParse(parts[1], out pawnId)) continue;
                CASettlementPopulationGroup populationGroup = record.populationGroups
                    ?.FirstOrDefault(c => c != null
                        && c.key == populationGroupKey);
                if (populationGroup != null
                    && populationGroup.kind == CAPopulationGroupKind.Main)
                    dominantIds.Add(pawnId);
            }
            int enrolled = 0;
            int excluded = 0;
            var eligibleIds = new HashSet<int>();
            foreach (Pawn pawn in residents)
            {
                bool eligible = !restrictive
                    || dominantIds.Contains(pawn.thingIDNumber)
                    || dominantIds.Count == 0;
                if (eligible)
                {
                    eligibleIds.Add(pawn.thingIDNumber);
                    enrolled++;
                }
                else excluded++;
            }
            org.memberPawnIds = eligibleIds.OrderBy(id => id).ToList();
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            string origin = "axis:membership:" + org.organizationKey;
            CAOrigin membershipTerms = TermsOrigin(AxisEntry(
                factionState.factionStructure,
                factionState.politicalBeliefs, CAFactionAxes.Membership),
                origin);
            ledger?.ClearDerived(origin);
            if (ledger != null)
                foreach (int pawnId in org.memberPawnIds)
                    ledger.Add(new CARelation
                    {
                        orgKey = org.organizationKey,
                        partyKind = CARelationPartyKind.Pawn,
                        partyPawnId = pawnId,
                        role = "member",
                        status = membershipRule,
                        origin = CAOrigin.Derived(origin),
                        termsOrigin = membershipTerms
                    });
            if (restrictive && excluded > 0)
                org.Record("membership", excluded + " resident(s) are not "
                    + "members under the " + membershipRule + " rule");
            else if (enrolled > 0)
                org.Record("membership", enrolled + " resident(s) joined "
                    + "under the " + membershipRule + " rule");
        }

        // ---- local order and defense ------------------------------------

        private static void LocalOrderAndDefense(CAOrganization org,
            CAFactionState factionState, CARegionalSettlementRecord record,
            Map map, List<Pawn> residents, string status,
            CAStatusAssignmentSet statusSet)
        {
            string localOrder = Axis(factionState, CAFactionAxes.LocalOrder);
            string defense = Axis(factionState, CAFactionAxes.Defense);
            string warConduct = Axis(factionState, CAFactionAxes.WarConduct);
            if (localOrder != null) SetPolicy(org, "local order", localOrder);
            if (defense != null) SetPolicy(org, "defense", defense);
            if (warConduct != null)
                SetPolicy(org, "treatment in war", warConduct);

            // Restrictive factions arm only members. Castes restrict arms to
            // ruling and warrior castes; earned rank selects the best shots.
            string membershipRule = Axis(factionState,
                CAFactionAxes.Membership);
            bool restrictive = membershipRule == "hereditary"
                || membershipRule == "closed";
            List<Pawn> eligible = restrictive
                ? residents.Where(p =>
                    org.memberPawnIds.Contains(p.thingIDNumber)).ToList()
                : residents.ToList();
            if (status == "castes" && statusSet.Arms.Count > 0)
            {
                List<Pawn> casteEligible = eligible.Where(p =>
                    statusSet.Arms.Contains(p.thingIDNumber)).ToList();
                if (casteEligible.Count > 0) eligible = casteEligible;
            }
            if (status == "earned")
                eligible = eligible.OrderByDescending(p =>
                    p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0)
                    .ToList();

            if (localOrder != null && localOrder != "none")
            {
                int guards = Math.Max(1, eligible.Count / 6);
                var practice = new CASecurityPractice
                {
                    mapId = map.uniqueID,
                    kindLabel = localOrder == "watch" ? "civic watch"
                        : localOrder == "constabulary" ? "constabulary"
                        : "ruler's guard",
                    name = (record.name ?? "settlement") + " "
                        + (localOrder == "watch" ? "watch" : "guard"),
                    assignedOffice = localOrder == "rulers"
                        ? "axis-executive" : null
                };
                var names = new List<string>();
                for (int i = 0; i < guards && i < eligible.Count; i++)
                {
                    // Earned standing took the best shots first; other
                    // factions take from the end of the rolls so the
                    // seated names and the armed names differ.
                    Pawn guard = status == "earned" ? eligible[i]
                        : eligible[eligible.Count - 1 - i];
                    practice.guardPawnIds.Add(guard.thingIDNumber);
                    names.Add(guard.LabelShortCap);
                    if (localOrder == "constabulary"
                        && !org.memberPawnIds.Contains(
                            guard.thingIDNumber))
                        org.memberPawnIds.Add(guard.thingIDNumber);
                }
                org.securityPractices.Add(practice);
                org.Record("local order", practice.kindLabel + " keeps "
                    + "internal order: "
                    + string.Join(", ", names.ToArray())
                    + (status == "castes" && statusSet.Arms.Count > 0
                        ? " (arms belong to the warrior and ruling "
                            + "castes)"
                        : status == "earned"
                            ? " (the best shots serve - standing is "
                                + "earned)"
                        : restrictive
                            ? " (members only - the membership rule arms who "
                                + "it trusts)" : ""));
            }
            else if (localOrder == "none")
                org.Record("local order", "no standing watch; residents "
                    + "respond as needed");

            if (defense != null)
                org.Record("defense", defense == "none"
                    ? "no defense body - resistance assembles ad hoc"
                    : defense == "levy"
                        ? "every able member owes war service when the "
                            + "levy is called"
                    : defense == "militia"
                        ? "a trained militia musters at need"
                    : defense == "professional"
                        ? "a standing force under the leadership"
                        : "war belongs to the warrior caste");
            if (warConduct != null)
                org.Record("treatment in war", warConduct == "quarter"
                    ? "surrender is accepted and defeated enemies are spared"
                    : warConduct == "combatants"
                        ? "force is reserved for combatants"
                        : "victors decide the fate of defeated enemies");
        }

        // ---- political beliefs vs current faction structure -------------

        private static void RecordBeliefConflicts(CAOrganization org,
            CAFactionState factionState, CARegionalSettlementRecord record)
        {
            var shim = new CARegionalFactionPlan
            {
                factionStructure = factionState.factionStructure,
                politicalBeliefs = factionState.politicalBeliefs
            };
            foreach (string conflict in CAFactionAxes.Conflicts(shim))
            {
                if (!org.openBeliefConflicts.Contains(conflict))
                    org.openBeliefConflicts.Add(conflict);
                org.Record("political-belief", conflict);
            }
        }
    }

    // Derives starting wealth and settlement age from the same deterministic
    // inputs for setup preview and record creation. Later reads use the stored
    // result.
    internal static class CASettlementWealth
    {
        internal static void Derive(string regionalId, int slot,
            int startingFacilityMask,
            int accessInfrastructure, int serviceInfrastructure,
            int civicInfrastructure, TechLevel knowledge,
            out int wealth, out int constructionEra)
        {
            int ceiling = CASettlementAxes.LocalPracticeCeiling(
                startingFacilityMask, accessInfrastructure,
                serviceInfrastructure, civicInfrastructure, knowledge);
            int production = CASettlementAxes.PracticedCapabilityBasis(
                CASettlementAxes.CapProduction, knowledge, ceiling);
            int fortification = CASettlementAxes.PracticedCapabilityBasis(
                CASettlementAxes.CapFortification, knowledge, ceiling);
            int tier = CASettlementAxes.Tier(knowledge);
            int h = Gen.HashCombineInt(GenText.StableStringHash(
                (regionalId ?? "r") + ":wealth"), slot * 89)
                & 0x7fffffff;
            wealth = Math.Max(0, Math.Min(3,
                (production + 1) / 2 + (fortification >= 4 ? 1 : 0)
                + (accessInfrastructure >= 2 ? 1 : 0)
                + (serviceInfrastructure >= 2 ? 1 : 0)
                + (h & 1) - (tier == 0 ? 1 : 0)
                - (civicInfrastructure <= 0 ? 1 : 0)));
            constructionEra = Math.Min(tier, (h >> 1) % (tier + 1));
        }

        internal static string WealthWords(int wealth)
        {
            return wealth <= 0 ? "poor" : wealth == 1 ? "modest"
                : wealth == 2 ? "prosperous" : "rich";
        }

        internal static string TierWords(int tier)
        {
            return tier <= 0 ? "tribal" : tier == 1 ? "medieval"
                : "industrial";
        }

    }

    // Stored resident ranks. Hereditary rank assigns houses; caste rank assigns
    // ruling, warrior, or common status. A stable hash creates the initial
    // assignments and later reads use the settlement record.
    internal sealed class CAStatusAssignmentSet
    {
        internal readonly HashSet<int> Elite = new HashSet<int>();
        internal readonly HashSet<int> Arms = new HashSet<int>();
        internal bool Stratified;
    }

    internal static class CAStatusAssignments
    {
        internal static CAStatusAssignmentSet Ensure(
            CARegionalSettlementRecord record, List<Pawn> residents,
            string status)
        {
            var set = new CAStatusAssignmentSet();
            bool hereditary = status == "hereditary";
            bool castes = status == "castes";
            if (!hereditary && !castes || record == null
                || residents == null) return set;
            set.Stratified = true;
            if (record.statusAssignments == null)
                record.statusAssignments = new List<string>();

            // Read what history already wrote.
            var assigned = new HashSet<int>();
            foreach (string entry in record.statusAssignments)
            {
                string[] parts = entry?.Split(':');
                int id;
                if (parts == null || parts.Length != 3
                    || !int.TryParse(parts[0], out id)) continue;
                assigned.Add(id);
                if (parts[2] == "noble" || parts[2] == "ruling")
                {
                    set.Elite.Add(id);
                    set.Arms.Add(id);
                }
                else if (parts[2] == "warrior") set.Arms.Add(id);
            }

            // Initialize whoever history has not placed yet - the
            // initial residents at first materialization, newcomers
            // at the bottom of the order afterwards.
            int salt = GenText.StableStringHash(
                (record.regionalId ?? "r") + ":status" + record.slot);
            int houses = Math.Max(2, residents.Count / 4);
            foreach (Pawn pawn in residents)
            {
                int id = pawn.thingIDNumber;
                if (assigned.Contains(id)) continue;
                int roll = Gen.HashCombineInt(salt, id) & 0x7fffffff;
                if (hereditary)
                {
                    int house = roll % houses;
                    bool noble = house == 0
                        || houses >= 8 && house == 1;
                    record.statusAssignments.Add(id + ":h" + house + ":"
                        + (noble ? "noble" : "commoner"));
                    if (noble)
                    {
                        set.Elite.Add(id);
                        set.Arms.Add(id);
                    }
                }
                else
                {
                    int caste = roll % 6;
                    string name = caste == 0 ? "ruling"
                        : caste == 1 ? "warrior" : "common";
                    record.statusAssignments.Add(id + ":caste:" + name);
                    if (name == "ruling")
                    {
                        set.Elite.Add(id);
                        set.Arms.Add(id);
                    }
                    else if (name == "warrior") set.Arms.Add(id);
                }
            }

            // If generation produces no ruling group, promote the first resident
            // and store that assignment.
            if (set.Elite.Count == 0 && residents.Count > 0)
            {
                int id = residents[0].thingIDNumber;
                record.statusAssignments.RemoveAll(entry =>
                    entry != null && entry.StartsWith(id + ":"));
                record.statusAssignments.Add(hereditary
                    ? id + ":h0:noble" : id + ":caste:ruling");
                set.Elite.Add(id);
                set.Arms.Add(id);
            }
            return set;
        }
    }

    // Resolves a decision from leadership, participation, and decisions. A
    // decision may fail when its requirements are not met.
    internal struct CADecisionOutcome
    {
        internal bool adopted;
        internal string words;
    }

    internal static class CAOrganizationDecisions
    {
        internal static CADecisionOutcome Decide(CAOrganization org,
            CAFactionState factionState, CARegionalSettlementRecord record,
            List<Pawn> residents, string subject)
        {
            string leadership = CAFactionAxes.KeyOf(factionState.factionStructure,
                CAFactionAxes.Leadership);
            string decisions = CAFactionAxes.KeyOf(factionState.factionStructure,
                CAFactionAxes.Decisions);
            string participation = CAFactionAxes.KeyOf(factionState.factionStructure,
                CAFactionAxes.Participation);

            // Participation selects the actual voters. Standing includes seat
            // holders and serving guards.
            int participants;
            string who;
            if (participation == "members")
            {
                participants = org.memberPawnIds.Count;
                who = participants + " enrolled member(s)";
            }
            else if (participation == "standing")
            {
                var standing = new HashSet<int>();
                foreach (CAOffice office in org.offices)
                    if (office != null && office.holderId >= 0)
                        standing.Add(office.holderId);
                foreach (CASecurityPractice practice in
                    org.securityPractices)
                    if (practice?.guardPawnIds != null)
                        foreach (int id in practice.guardPawnIds)
                            standing.Add(id);
                participants = Math.Max(1, standing.Count);
                who = participants
                    + " of earned standing (office and service)";
            }
            else if (participation == "heads")
            {
                participants = Math.Max(1, residents.Count / 3);
                who = participants + " household head(s)";
            }
            else
            {
                participants = residents.Count;
                who = participants + " resident(s)";
            }

            // Deterministic per settlement and subject - the same
            // assembly does not flip its vote on reload.
            Rand.PushState(Gen.HashCombineInt(GenText.StableStringHash(
                (record.regionalId ?? "r") + ":" + subject), 71));
            try
            {
                switch (decisions)
                {
                    case "decree":
                    {
                        CAOffice seat = org.offices.FirstOrDefault(o =>
                            o != null && o.seniority >= 900);
                        if (seat == null || seat.holderId < 0)
                            return new CADecisionOutcome
                            {
                                adopted = false,
                                words = "no seated leadership exists to "
                                    + "decree it"
                            };
                        return new CADecisionOutcome
                        {
                            adopted = true,
                            words = "decreed by " + seat.holderLabel
                                + " on " + subject
                        };
                    }
                    case "consensus":
                    {
                        bool formed = participants <= 1
                            || Rand.Chance(0.8f);
                        return new CADecisionOutcome
                        {
                            adopted = formed,
                            words = formed
                                ? "consensus of " + who + " formed on "
                                    + subject
                                : "consensus among " + who
                                    + " did not form"
                        };
                    }
                    case "majority":
                    {
                        int yes = participants <= 0 ? 0
                            : Rand.RangeInclusive(
                                participants * 2 / 5, participants);
                        bool carried = yes * 2 > participants;
                        return new CADecisionOutcome
                        {
                            adopted = carried,
                            words = "vote of " + who + ": " + yes
                                + " for, " + (participants - yes)
                                + " against - "
                                + (carried ? "carried" : "lost")
                        };
                    }
                    default:
                        return new CADecisionOutcome
                        {
                            adopted = leadership != "none",
                            words = leadership == "none"
                                ? "no custom binds a people without "
                                    + "standing leadership to a standing "
                                    + "levy"
                                : "settled by custom and standing among "
                                    + who
                        };
                }
            }
            finally { Rand.PopState(); }
        }
    }
}
