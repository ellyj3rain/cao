using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // End-to-end receipt from authored composition to the loaded game:
    //
    //   authored composition -> settlement record -> native Faction /
    //   Pawn.Faction / Pawn.Ideo / Certainty -> operator organizations ->
    //   physical kitchens, stores and tables -> tax-rate policies in
    //   taxation -> water constraints from the map.
    //
    // The draft receipt checks deterministic derivation and
    // authored-survival on the pending draft without touching any world.
    // The MAP receipt reads the current map's settlement records and
    // reports expected and actual values for every link. Both
    // are read-only: they mutate nothing and can run on any world.
    internal static class CAConvergenceReceipt
    {
        // ---- entry: the draft derives deterministically ----------------

        internal static string RunEntry()
        {
            var text = new StringBuilder();
            text.AppendLine("[CA][Convergence] draft receipt");
            CARegionalPlan plan = CARegionalSetupSession.PendingForCurrentWorld
                ?? CARegionalSetupSession.ActivePreviewPlan;
            if (plan == null || plan.settlements.Count == 0)
                return text.Append("  no pending plan with settlements - "
                    + "open the Starting Region screen and add a settlement "
                    + "first").ToString();

            CARegionalSettlementPlan settlementPlan = plan.settlements.First(b => b != null);
            var firstPopulationGroups = new List<CASettlementPopulationGroup>();
            var firstProv = new List<CAProvisionArrangement>();
            var probe = new CARegionalSettlementPlan
            {
                slot = settlementPlan.slot,
                factionKey = settlementPlan.factionKey,
                memberTileId = settlementPlan.memberTileId,
                populationGroups = firstPopulationGroups,
                provisionArrangements = firstProv
            };
            CASettlementComposition.EnsureDerived(plan, probe);
            var again = new CARegionalSettlementPlan
            {
                slot = settlementPlan.slot,
                factionKey = settlementPlan.factionKey,
                memberTileId = settlementPlan.memberTileId
            };
            CASettlementComposition.EnsureDerived(plan, again);

            text.AppendLine("  [1] DETERMINISM: same candidate, same slot, "
                + "derived twice");
            text.AppendLine("      first : " + CASettlementComposition
                .DescribePopulationGroups(probe.populationGroups));
            text.AppendLine("      second: " + CASettlementComposition
                .DescribePopulationGroups(again.populationGroups));
            text.AppendLine("      identical: "
                + (CASettlementComposition.DescribePopulationGroups(probe.populationGroups)
                    == CASettlementComposition.DescribePopulationGroups(
                        again.populationGroups)) + "   EXPECT True");
            text.AppendLine("      arrangements: " + probe.provisionArrangements
                .Count + " vs " + again.provisionArrangements.Count
                + "   EXPECT equal");

            text.AppendLine("  [2] THE ACTUAL DRAFT of "
                + CARegionalPlanUtility.SettlementName(plan, settlementPlan) + ":");
            text.AppendLine("      people: " + CASettlementComposition
                .DescribePopulationGroups(settlementPlan.populationGroups));
            foreach (CAProvisionArrangement arrangement in
                settlementPlan.provisionArrangements.Where(a => a != null && a.active))
                text.AppendLine("      provision arrangement: "
                    + arrangement.Summary);

            CARegionalFactionPlan owner = plan.FactionPlan(
                settlementPlan.factionKey);
            if (owner != null)
            {
                owner.EnsureCultureAndPolitics(plan);
                text.AppendLine("  [3] FACTION STATE for "
                    + CARegionalPlanUtility.FactionName(owner) + ":");
                text.AppendLine("      political beliefs: "
                    + CAPoliticalBeliefsModel.Summary(
                        owner.politicalBeliefs));
                text.AppendLine("      current structure: "
                    + CAFactionAxes.Characterize(plan, owner));
            }

            // ---- TERRITORY: realized state and orthogonality ------------
            CARegionalSettlements.EnsureSettlementPattern(plan);
            text.AppendLine("  [4] SETTLEMENT PATTERN: "
                + CARegionalSettlements.SettlementProfile(plan)
                + "  (persisted on the candidate; the world priors are "
                + "not consulted again)");
            foreach (CARegionalSettlementPlan place in plan.settlements
                .Where(b => b != null).OrderBy(b => b.slot))
                text.AppendLine("      " + CARegionalPlanUtility
                    .SettlementName(plan, place) + ": "
                    + CARegionalSettlements.RoleWords(
                        (CASettlementRole)place.realizedRole));
            foreach (CARegionalFactionPlan group in
                plan.factions.Where(g => g != null))
            {
                CASettlementAuthority authority =
                    CARegionalSettlements.SettlementAuthorityOf(plan, group);
                string[] delegated = CARegionalSettlements.SharedResponsibilities(authority);
                text.AppendLine("      "
                    + CARegionalPlanUtility.FactionName(group) + ": "
                    + CARegionalSettlements.SettlementAuthorityWords(authority)
                    + (group.settlementAuthorityExplicit ? " (authored)"
                        : " (derived)")
                    + " -> shared responsibilities ["
                    + (delegated.Length == 0 ? "none"
                        : string.Join(", ", delegated)) + "]");
                text.AppendLine("        composed: "
                    + CARegionalSettlements.Characterize(plan, group));
            }

            if (owner != null)
            {
                text.AppendLine("  [5] STARTING POPULATION AND FACTION:");
                text.AppendLine("      culture: "
                    + CACultureModel.Summary(owner.culture));
                text.AppendLine("      Ideoligion: "
                    + (owner.LivingIdeo?.name ?? "generated with faction"));
                text.AppendLine("      political beliefs: "
                    + CAPoliticalBeliefsModel.Summary(
                        owner.politicalBeliefs));
                text.AppendLine("      faction structure: "
                    + CAFactionAxes.Characterize(plan, owner));
                text.AppendLine("      settlement authority: "
                    + CARegionalSettlements.SettlementAuthorityWords(
                        CARegionalSettlements.SettlementAuthorityOf(plan, owner)));
            }
            return text.ToString();
        }

        // ---- map: the chain resolved in the world ----------------------

        internal static string RunMap(Map map)
        {
            var text = new StringBuilder();
            text.AppendLine("[CA][Convergence] map receipt");
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            if (world == null || map == null)
                return text.Append("  no world/map").ToString();
            List<CARegionalSettlementRecord> records =
                world.ForMap(map).ToList();
            if (records.Count == 0)
                return text.Append("  no CA settlement records on this "
                    + "map - generate a regional start with authored "
                    + "settlements").ToString();

            foreach (CARegionalSettlementRecord record in records)
            {
                text.AppendLine("  ==== " + (record.name ?? "settlement")
                    + " (" + record.regionalId + ") ====");
                text.AppendLine("  native Settlement faction: "
                    + (record.faction?.Name ?? "NONE")
                    + " / " + (record.faction?.def?.defName ?? "-")
                    + "; relations with you: "
                    + (record.faction?.PlayerRelationKind.ToString()
                        ?? "-"));

                // [A] population groups: expected vs actual
                if (record.populationGroups == null || record.populationGroups.Count == 0)
                {
                    text.AppendLine("  population groups: NONE RECORDED - this "
                        + "record predates composition");
                    continue;
                }
                text.AppendLine("  [A] PEOPLE - expected "
                    + CASettlementComposition.DescribePopulationGroups(
                        record.populationGroups));
                var byPopulationGroup = new Dictionary<string, int>();
                foreach (string entry in record.populationAssignments
                    ?? new List<string>())
                {
                    string key = entry.Split(':')[0];
                    int count;
                    byPopulationGroup.TryGetValue(key, out count);
                    byPopulationGroup[key] = count + 1;
                }
                foreach (CASettlementPopulationGroup populationGroup in record.populationGroups)
                {
                    if (populationGroup == null) continue;
                    int assigned;
                    byPopulationGroup.TryGetValue(populationGroup.key.ToString(),
                        out assigned);
                    text.AppendLine("      " + populationGroup.label + " ("
                        + PopulationGroupWords(populationGroup) + "): expected "
                        + populationGroup.share + "%, assigned " + assigned
                        + " resident(s)");
                }

                // [B] the native facts of the actual living residents
                var factionTally = new Dictionary<string, int>();
                var ideoTally = new Dictionary<string, int>();
                var certainty = new List<float>();
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn == null || !pawn.RaceProps.Humanlike)
                        continue;
                    if (!pawn.Position.InHorDistOf(
                        record.localRect.CenterCell, 60f)) continue;
                    if (pawn.Faction == null
                        || pawn.Faction == Faction.OfPlayerSilentFail)
                        continue;
                    string populationGroupKey = CAPopulationProjection
                        .AssignedPopulationGroupOf(record, pawn);
                    if (populationGroupKey == null) continue;
                    Tally(factionTally, pawn.Faction.Name);
                    Tally(ideoTally, pawn.Ideo?.name ?? "none");
                    if (pawn.ideo != null)
                        certainty.Add(pawn.ideo.Certainty);
                }
                text.AppendLine("  [B] NATIVE FACTS of assigned "
                    + "residents:");
                text.AppendLine("      Pawn.Faction: "
                    + Join(factionTally)
                    + "   EXPECT >1 faction when a resident minority "
                    + "population group resolved");
                text.AppendLine("      Pawn.Ideo:    " + Join(ideoTally)
                    + "   EXPECT >1 ideo when a distinct Ideoligion group "
                    + "resolved");
                if (certainty.Count > 0)
                    text.AppendLine("      Certainty: min "
                        + certainty.Min().ToString("F2") + ", max "
                        + certainty.Max().ToString("F2")
                        + "   EXPECT spread when cohesion differs");

                // [C] provision arrangements: operators, assets, funding, water
                text.AppendLine("  [C] PROVISIONING:");
                if (record.provisionArrangements == null
                    || record.provisionArrangements.Count == 0)
                    text.AppendLine("      none recorded");
                foreach (CAProvisionArrangement arrangement in
                    record.provisionArrangements ?? new List<CAProvisionArrangement>())
                {
                    if (arrangement == null
                        || arrangement.operatorKind
                            == CAProvisionOperator.DomesticUnit
                        || arrangement?.operatorKind
                            == CAProvisionOperator.Individual) continue;
                    string orgKey = CAProvisionArrangements.ProviderKey(record,
                        arrangement);
                    CAOrganization op = orgs?.ByKey(orgKey);
                    text.AppendLine("      " + arrangement.Summary);
                    text.AppendLine("        operator org: "
                        + (op != null ? op.name + " (treasury "
                            + op.treasury.ToString("F0") + ")"
                            : "MISSING") + "   EXPECT present");
                    if (arrangement.funding
                        == CAProvisionFunding.Taxation)
                    {
                        bool policy = op != null && op.policies.Any(p =>
                            p != null && p.key == arrangement.policyKey
                            && p.value == arrangement.policyValue);
                        int now = Find.TickManager?.TicksGame ?? 0;
                        string payerKey = record.regionalId + "#"
                            + record.slot;
                        int taxLinks = op == null ? 0
                            : CAOrganizationRelationsWorldComponent.Current
                                ?.RelationsIn(op.organizationKey).Count(link =>
                                    link != null && !link.Expired(now)
                                    && link.IsOrganizationParty
                                    && link.OrganizationPartyKey == payerKey
                                    && link.Delegates(CAResponsibilities.Taxes)) ?? 0;
                        bool refused = op?.decisionHistory.Any(entry =>
                            entry != null && entry.kind == "decision"
                            && entry.text?.StartsWith(
                                "starting tax rate refused ") == true)
                            == true;
                        bool adopted = op?.decisionHistory.Any(entry =>
                            entry != null && entry.kind == "decision"
                            && entry.text?.StartsWith(
                                "starting tax rate adopted ") == true)
                            == true;
                        bool collectionSeen = op?.decisionHistory.Any(entry =>
                            entry != null && entry.kind == "organization"
                            && entry.text?.StartsWith(
                                "tax collected ") == true) == true;
                        bool consistent = refused
                            ? !policy && taxLinks == 0
                            : policy && taxLinks == 1;
                        text.AppendLine("        starting decision: "
                            + (refused ? "refused" : adopted ? "adopted"
                                : "not recorded"));
                        text.AppendLine("        tax rate: "
                            + (policy ? "active" : "none"));
                        text.AppendLine("        settlement tax link: "
                            + taxLinks + " active");
                        text.AppendLine("        tax setup: "
                            + (consistent ? "consistent" : "MISMATCH")
                            + "   EXPECT policy + one exact link, or neither "
                            + "after refusal");
                        text.AppendLine("        collection receipt: "
                            + (collectionSeen ? "observed"
                                : "not yet observed"));
                    }
                }
                int stoves = 0, tables = 0, foodStacks = 0;
                foreach (IntVec3 cell in record.localRect)
                {
                    if (!cell.InBounds(map)) continue;
                    foreach (Thing thing in cell.GetThingList(map))
                    {
                        if (thing.def.defName == "FueledStove"
                            || thing.def.defName == "Campfire") stoves++;
                        else if (thing.def.defName == "Table2x2c")
                            tables++;
                        else if (thing.def.defName == "Pemmican")
                            foodStacks++;
                    }
                }
                text.AppendLine("      physical: " + stoves
                    + " hearth/stove(s), " + tables + " dining table(s), "
                    + foodStacks + " food stack(s) standing in the rect"
                    + "   EXPECT one hearth+table per non-household node");

                // [D] conserved consequences visible to the ledger
                CATransactionLedger ledger = CATransactionLedger.Current;
                if (ledger != null)
                    text.AppendLine("  [D] LEDGER: "
                        + ledger.Transactions.Count + " transaction(s), "
                        + ledger.Debts.Count + " debt(s) world-wide; "
                        + "tax collections update treasuries and record "
                        + "acts as they occur");
            }

            // [F] Current faction structure in generated settlement state:
            // named officeholders, membership, ownership, staffed posts, and
            // local order.
            CAOrganizationWorldComponent orgComp =
                CAOrganizationWorldComponent.Current;
            CAOrganizationRelationsWorldComponent axisLedger =
                CAOrganizationRelationsWorldComponent.Current;
            foreach (CARegionalSettlementRecord record in records)
            {
                CAOrganization org = orgComp?.ByKey(record.regionalId
                    + "#" + record.slot);
                if (org == null) continue;
                text.AppendLine("  [F] " + (record.name ?? "settlement")
                    + " FACTION STRUCTURE:");
                foreach (string key in new[] { "decisions", "participation",
                    "dissent", "membership", "work", "local order" })
                {
                    CAPolicyRecord policy = org.policies.FirstOrDefault(
                        p => p != null && p.key == key);
                    if (policy != null)
                        text.AppendLine("      " + key + ": "
                            + policy.value);
                }
                foreach (CAOffice office in org.offices.Where(o =>
                    o != null && o.sourceKey != null
                    && o.sourceKey.StartsWith("axis-")))
                    text.AppendLine("      office " + office.name
                        + (office.holderId >= 0
                            ? " held by " + office.holderLabel
                            : " VACANT")
                        + " (seniority " + office.seniority + ")");
                text.AppendLine("      members: "
                    + org.memberPawnIds.Count + " enrolled");
                if (axisLedger != null)
                {
                    int holdings = axisLedger.Holdings.Count(h =>
                        h != null && h.mapId == map.uniqueID);
                    if (holdings > 0)
                        text.AppendLine("      holdings on this map: "
                            + holdings + " (tenure per the ownership "
                            + "axis; see ledger)");
                }
                foreach (CASecurityPractice practice in
                    org.securityPractices.Where(p => p != null
                        && p.mapId == map.uniqueID))
                    text.AppendLine("      security: " + practice.name
                        + " [" + practice.kindLabel + "]"
                        + (practice.assignedOffice != null
                            ? " answering to " + practice.assignedOffice
                            : ""));
                // [G] apertures cut for this settlement, by kind - the
                // low-tech / fortified / elite / industrial examples
                // print here from whatever was actually generated.
                var apertureCounts = new Dictionary<string, int>();
                foreach (string asset in record.seededAssets
                    ?? new List<string>())
                {
                    string kind = asset.Split('|')[0];
                    if (!kind.StartsWith("CA_Aperture")
                        && kind != "CA_WindowGlazed") continue;
                    int count;
                    apertureCounts.TryGetValue(kind, out count);
                    apertureCounts[kind] = count + 1;
                }
                if (apertureCounts.Count > 0)
                    text.AppendLine("      apertures: " + string.Join(
                        ", ", apertureCounts.Select(pair =>
                            pair.Value + " " + pair.Key).ToArray()));
            }

            // [E] Settlement authority stored as faction-to-settlement
            // organization relations.
            CARegionalPlan regionPlan = world.FindRegionForMap(map);
            CAOrganizationRelationsWorldComponent relations =
                CAOrganizationRelationsWorldComponent.Current;
            if (regionPlan != null && relations != null)
            {
                text.AppendLine("  [E] SETTLEMENT AUTHORITY RELATIONS:");
                text.AppendLine("      region: "
                    + CARegionalSettlements.SettlementProfile(regionPlan));
                var factions = records
                    .Where(r => r.faction != null)
                    .Select(r => r.faction).Distinct().ToList();
                foreach (Faction faction in factions)
                {
                    string factionKey = "faction:" + faction.loadID;
                    List<CARelation> members =
                        relations.SettlementMemberships(factionKey)
                            .ToList();
                    if (members.Count == 0)
                    {
                        text.AppendLine("      " + faction.Name
                            + ": no member relations (single "
                            + "settlement or loose association)");
                        continue;
                    }
                    text.AppendLine("      " + faction.Name + ": "
                        + members.Count + " member(s)");
                    foreach (CARelation relation in members)
                        text.AppendLine("        " + relation.partyOrgKey
                            + " shares ["
                            + string.Join(", ",
                                relation.delegatedResponsibilities.ToArray())
                            + "] (roster " + relation.origin.source
                            + ", terms " + relation.termsOrigin.source + ")");
                }
            }
            return text.ToString();
        }

        // ---- radical shift: authority moves, streets do not -------------

        // Changes the first multi-settlement faction's settlement authority,
        // re-materializes the relations, and proves that shared
        // responsibilities change while physical settlement facts stay
        // identical, and restores the original. Reversible by
        // construction.
        internal static string RunRadicalShift(Map map)
        {
            var text = new StringBuilder();
            text.AppendLine("[CA][Convergence] radical-shift receipt");
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            CAOrganizationRelationsWorldComponent political =
                CAOrganizationRelationsWorldComponent.Current;
            CARegionalPlan plan = world?.FindRegionForMap(map);
            if (plan == null || political == null)
                return text.Append("  no regional plan on this map")
                    .ToString();
            List<CARegionalSettlementRecord> records =
                world.ForMap(map).Where(r => r?.faction != null).ToList();
            CARegionalFactionPlan group = plan.factions
                .FirstOrDefault(g => g != null && records.Count(r =>
                    r.factionKey == g.key) >= 2);
            if (group == null)
                return text.Append("  no faction holds two settlements "
                    + "here - the shift needs a multi-settlement faction")
                    .ToString();
            Faction faction = records.First(r =>
                r.factionKey == group.key).faction;
            string factionKey = "faction:" + faction.loadID;

            Func<string> relationWords = delegate
            {
                List<CARelation> members = political
                    .SettlementMemberships(factionKey).ToList();
                return members.Count + " member settlement(s), sharing ["
                    + string.Join(", ", members
                        .SelectMany(r => r.delegatedResponsibilities).Distinct()
                        .ToArray()) + "]";
            };
            Func<string> physicalWords = delegate
            {
                int buildings = 0;
                foreach (CARegionalSettlementRecord record in records)
                    if (record.factionKey == group.key)
                        foreach (IntVec3 cell in record.localRect)
                            if (cell.InBounds(map)
                                && cell.GetEdifice(map) != null)
                                buildings++;
                return buildings + " standing edifices across "
                    + records.Count(r => r.factionKey == group.key)
                    + " settlement rect(s)";
            };

            byte savedSettlementAuthority = group.settlementAuthority;
            bool savedAuthored = group.settlementAuthorityExplicit;
            CASettlementAuthority before =
                CARegionalSettlements.SettlementAuthorityOf(plan, group);
            CASettlementAuthority flipped =
                before == CASettlementAuthority.Central
                    ? CASettlementAuthority.IndependentWithSharedDefense
                    : CASettlementAuthority.Central;
            try
            {
                text.AppendLine("  faction: " + faction.Name + " ("
                    + CARegionalSettlements.SettlementAuthorityWords(before) + ")");
                text.AppendLine("  BEFORE: " + relationWords());
                text.AppendLine("  physical: " + physicalWords());

                group.settlementAuthority = (byte)flipped;
                group.settlementAuthorityExplicit = true;
                CASettlementAuthorityWriter.Materialize(plan, records);
                text.AppendLine("  SHIFTED to "
                    + CARegionalSettlements.SettlementAuthorityWords(flipped) + ":");
                text.AppendLine("  AFTER:  " + relationWords()
                    + "   EXPECT responsibilities changed");
                text.AppendLine("  physical: " + physicalWords()
                    + "   EXPECT identical - authority moved, streets "
                    + "did not");
            }
            finally
            {
                group.settlementAuthority = savedSettlementAuthority;
                group.settlementAuthorityExplicit = savedAuthored;
                CASettlementAuthorityWriter.Materialize(plan, records);
                text.AppendLine("  RESTORED: " + relationWords());
            }
            return text.ToString();
        }

        private static string PopulationGroupWords(CASettlementPopulationGroup populationGroup)
        {
            return populationGroup.kind.ToString() + ", " + populationGroup.CertaintyWords;
        }

        private static void Tally(Dictionary<string, int> tally,
            string key)
        {
            int count;
            tally.TryGetValue(key, out count);
            tally[key] = count + 1;
        }

        private static string Join(Dictionary<string, int> tally)
        {
            return tally.Count == 0 ? "none"
                : string.Join(", ", tally.Select(pair =>
                    pair.Key + " x" + pair.Value).ToArray());
        }
    }

    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness",
            "Convergence: draft composition receipt",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Entry)]
        private static void ConvergenceDraftReceipt()
        {
            Log.Message(CAConvergenceReceipt.RunEntry());
        }

        [DebugAction("Colonist Awareness",
            "Convergence: map composition receipt",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ConvergenceMapReceipt()
        {
            Log.Message(CAConvergenceReceipt.RunMap(Find.CurrentMap));
        }

        [DebugAction("Colonist Awareness",
            "Settlement pattern: radical shift receipt (reversible)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SettlementPatternShiftReceipt()
        {
            Log.Message(CAConvergenceReceipt.RunRadicalShift(
                Find.CurrentMap));
        }
    }
}
