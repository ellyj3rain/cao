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

    public class CASettlementPatternDef : Def { }

    public class CASettlementScaleDef : Def
    {
        public int minSettlements = -1;
    }

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
            if (plan.settlementPattern
                != (byte)CASettlementPattern.Unsettled) return;
            DeriveSettlementPattern(plan);
        }

        internal static void DeriveSettlementPattern(CARegionalPlan plan)
        {
            var settlements = plan.settlements.Where(b => b != null).ToList();
            CARegionalWorldPolicy policy = plan.worldPolicy
                ?? new CARegionalWorldPolicy();
            Rand.PushState(Gen.HashCombineInt(GenText.StableStringHash(
                (plan.candidateId ?? "ca") + ":settlements"),
                settlements.Count * 131));
            try
            {
                // Derive the settlement pattern from settlements, their world
                // tiles, faction mixture, and conflict.
                var byTile = settlements.GroupBy(b => b.memberTileId).ToList();
                int groups = settlements.Select(b => b.factionKey)
                    .Distinct().Count();
                bool mixed = groups > 1;
                bool conflictFormed = mixed
                    && Rand.Chance(policy.startingConflictChance);

                // Roll frontier holdings once from suitable tiles and persist
                // the result for map generation.
                int suitable = plan.memberTileIds.Count(id =>
                {
                    PlanetTile tile =
                        CARegionalPlanUtility.SurfaceTile(id);
                    return tile.Valid && tile.Tile != null
                        && !tile.Tile.WaterCovered
                        && tile.Tile.hilliness != Hilliness.Impassable;
                });
                int frontier = 0;
                for (int s = 0; s < suitable * 2; s++)
                    if (Rand.Chance(Mathf.Clamp01(policy.frontierSettlementFrequency)))
                        frontier++;
                plan.realizedFrontierHoldings = Mathf.Clamp(frontier, 0, 8);

                CASettlementPattern topology;
                if (settlements.Count == 0)
                    topology = CASettlementPattern.Unsettled;
                else if (settlements.Count <= 2
                    && plan.realizedFrontierHoldings >= settlements.Count * 2
                    && plan.realizedFrontierHoldings >= 4)
                    // A couple of small settlements in a countryside of
                    // holdings that answer to none of them.
                    topology = CASettlementPattern.FrontierHinterland;
                else if (settlements.Count == 1)
                    topology = CASettlementPattern.Isolated;
                else if (conflictFormed)
                    topology = CASettlementPattern.ContestedMixed;
                else if (byTile.Count == 1)
                    topology = CASettlementPattern.Clustered;
                else
                {
                    // Center-form bias resolves only the ambiguous remainder:
                    // whether an existing grouping reads as a shared cluster
                    // or one dominant center. A corridor still needs settlements
                    // actually strung across distinct constituents.
                    int largest = byTile.Max(g => g.Count());
                    if (largest >= Math.Max(2, settlements.Count - 1))
                        topology = CASettlementPattern.DominantCenter;
                    else if (byTile.Count >= 3
                        && byTile.All(g => g.Count() == 1))
                        topology = Rand.Chance(0.4f)
                            ? CASettlementPattern.Corridor
                            : CASettlementPattern.Dispersed;
                    else if (byTile.Count(g => g.Count() > 1) >= 2)
                        topology = CASettlementPattern.ComparableCenters;
                    else
                        topology = Rand.Chance(policy.settlementPatternTendency)
                            ? CASettlementPattern.DominantCenter
                            : CASettlementPattern.Clustered;
                }

                // City formation is considered only when settlement count,
                // concentration, and connections can support a large urban
                // region.
                int concentration = settlements.Count == 0 ? 0
                    : byTile.Max(g => g.Count());
                CASettlementScale scale;
                if (settlements.Count == 0)
                    scale = CASettlementScale.DispersedSettlement;
                else if (settlements.Count == 1)
                    scale = CASettlementScale.LocalCenter;
                else if (concentration <= 1)
                    scale = settlements.Count >= 4
                        ? CASettlementScale.LocalCenter
                        : CASettlementScale.HamletNetwork;
                else if (concentration == 2)
                    scale = CASettlementScale.RegionalCenter;
                else
                {
                    // minSettlements controls the required settlement count.
                    CASettlementScaleDef metroDef =
                        DefDatabase<CASettlementScaleDef>.GetNamedSilentFail(
                            "CA_Scale_LargeUrbanRegion");
                    int minSettlements = metroDef != null
                        && metroDef.minSettlements > 0
                            ? metroDef.minSettlements : 4;
                    bool supported = settlements.Count >= minSettlements
                        && (topology == CASettlementPattern.DominantCenter
                            || topology
                                == CASettlementPattern.ComparableCenters
                            || topology == CASettlementPattern.Clustered);
                    scale = supported
                        && Rand.Chance(policy.cityFormationChance)
                        ? CASettlementScale.LargeUrbanRegion
                        : CASettlementScale.UrbanCenter;
                }

                plan.settlementPattern = (byte)topology;
                plan.settlementScale = (byte)scale;

                // Each settlement's role is persisted with the plan.
                int centerTile = settlements.Count == 0 ? -1
                    : byTile.OrderByDescending(g => g.Count())
                        .First().Key;
                foreach (CARegionalSettlementPlan settlement in settlements)
                {
                    if (settlements.Count == 1)
                        settlement.realizedRole =
                            (byte)CASettlementRole.Standalone;
                    else if (settlement.memberTileId == centerTile)
                        settlement.realizedRole =
                            (byte)CASettlementRole.Center;
                    else
                        settlement.realizedRole =
                            (byte)CASettlementRole.Satellite;
                }
            }
            finally { Rand.PopState(); }
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

        // Settlement authority is separate from faction structure. It derives
        // from leadership and participation unless explicitly authored.
        internal static CASettlementAuthority DeriveSettlementAuthority(
            CARegionalPlan plan, CARegionalFactionPlan group)
        {
            if (plan == null || group == null)
                return CASettlementAuthority.Central;
            group.EnsureCultureAndPolitics(plan);
            int held = plan.settlements.Count(b => b != null
                && b.factionKey == group.key);
            if (held <= 1)
                return CASettlementAuthority.Central;

            string leadership = CAFactionAxes.KeyOf(group.factionStructure,
                CAFactionAxes.Leadership);
            string participation = CAFactionAxes.KeyOf(group.factionStructure,
                CAFactionAxes.Participation);
            if (leadership.NullOrEmpty())
                leadership = CAFactionAxes.KeyOf(
                    group.politicalBeliefs?.positions, CAFactionAxes.Leadership);
            if (participation.NullOrEmpty())
                participation = CAFactionAxes.KeyOf(
                    group.politicalBeliefs?.positions, CAFactionAxes.Participation);

            switch (leadership)
            {
                case "none":
                    return CASettlementAuthority.Independent;
                case "federated":
                    return CASettlementAuthority.IndependentWithSharedDefense;
                case "whole":
                    return CASettlementAuthority.Independent;
                case "council":
                    return participation == "universal"
                        ? CASettlementAuthority.Shared
                        : CASettlementAuthority.CentralWithLocalRule;
                case "single":
                    return participation == "universal" && held >= 3
                        ? CASettlementAuthority.CentralWithLocalRule
                        : CASettlementAuthority.Central;
                default:
                    return held >= 3 ? CASettlementAuthority.CentralWithLocalRule
                        : CASettlementAuthority.Central;
            }
        }

        internal static CASettlementAuthority SettlementAuthorityOf(
            CARegionalPlan plan, CARegionalFactionPlan group)
        {
            if (group.settlementAuthorityExplicit)
            {
                CASettlementAuthority authored = NormalizeAuthority(
                    (CASettlementAuthority)group.settlementAuthority);
                group.settlementAuthority = (byte)authored;
                return authored;
            }
            CASettlementAuthority derived =
                DeriveSettlementAuthority(plan, group);
            group.settlementAuthority = (byte)derived;
            return derived;
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
                + "Stands inside the expanded regional map.";
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
        private const string AuthorityTaxRateSource =
            "settlement-authority:tax-rate";

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
                    CASettlementAuthority authority =
                        CARegionalSettlements.SettlementAuthorityOf(plan, group);
                    string factionKey = "faction:" + faction.loadID;
                    var desiredMembers = new HashSet<string>();
                    if (mine.Count >= 2
                        && authority != CASettlementAuthority.Independent)
                        foreach (CARegionalSettlementRecord record in mine)
                            desiredMembers.Add(record.regionalId + "#"
                                + record.slot);
                    ReconcileDerivedMembers(factionKey, desiredMembers);
                    if (desiredMembers.Count == 0)
                    {
                        RemoveDerivedTaxRate(orgs.ByKey(factionKey));
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

                    // Shared taxation follows the faction's current decision
                    // rule and records which settlements agreed.
                    if (delegated.Contains(CAResponsibilities.Taxes))
                        SetSharedTaxRate(plan, group, factionBody, authority,
                            mine, now);
                    else
                        RemoveDerivedTaxRate(factionBody);

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

        // A deterministic faction decision made by its member settlements.
        private static void SetSharedTaxRate(CARegionalPlan plan,
            CARegionalFactionPlan group, CAOrganization factionBody,
            CASettlementAuthority authority,
            List<CARegionalSettlementRecord> settlements, int now)
        {
            CAPolicyRecord existing = factionBody.policies.FirstOrDefault(p =>
                p != null && p.key == "tax rate");
            // An authored or imposed tax policy owns the key. Regional
            // generation never overwrites it.
            if (existing != null
                && existing.generatedBy != AuthorityTaxRateSource) return;
            string decisions = CAFactionAxes.KeyOf(group.factionStructure,
                CAFactionAxes.Decisions);
            string level = authority
                == CASettlementAuthority.Central
                ? "standard" : "light";
            int count = settlements.Count;
            bool adopted;
            string words;
            Rand.PushState(Gen.HashCombineInt(GenText.StableStringHash(
                (plan.regionalId ?? "r") + ":shared-tax-rate"),
                group.key * 131));
            try
            {
                if (decisions == "consensus")
                {
                    adopted = Rand.Chance(0.8f);
                    words = adopted
                        ? "all " + count + " member settlements ("
                            + SettlementNames(settlements, null)
                            + ") agreed to a " + level + " tax rate"
                        : "the member settlements did not agree - the "
                            + "center collects nothing";
                }
                else if (decisions == "majority")
                {
                    int yes = Rand.RangeInclusive(count * 2 / 5, count);
                    adopted = yes * 2 > count;
                    var forVotes = new List<string>();
                    var against = new List<string>();
                    for (int i = 0; i < count; i++)
                        (i < yes ? forVotes : against).Add(
                            settlements[i].name ?? "settlement "
                            + settlements[i].slot);
                    words = "member settlements voted " + yes + " to "
                        + (count - yes) + " on a " + level
                        + " tax rate - "
                        + (adopted ? "carried" : "REFUSED")
                        + " (for: " + string.Join(", ",
                            forVotes.ToArray())
                        + (against.Count > 0
                            ? "; against: " + string.Join(", ",
                                against.ToArray()) : "") + ")";
                }
                else
                {
                    adopted = true;
                    words = (decisions == "decree"
                        ? "set by the center: " : "set: ")
                        + "a " + level
                        + " tax rate for "
                        + SettlementNames(settlements, null);
                }
            }
            finally { Rand.PopState(); }
            if (adopted)
            {
                if (existing == null)
                {
                    existing = new CAPolicyRecord { key = "tax rate" };
                    factionBody.policies.Add(existing);
                }
                existing.value = level;
                existing.adoptedTick = now;
                existing.generatedBy = AuthorityTaxRateSource;
            }
            else if (existing != null)
                factionBody.policies.Remove(existing);
            factionBody.Record("decision", words);
        }

        private static void RemoveDerivedTaxRate(
            CAOrganization factionBody)
        {
            if (factionBody?.policies == null) return;
            factionBody.policies.RemoveAll(policy => policy != null
                && policy.key == "tax rate"
                && policy.generatedBy == AuthorityTaxRateSource);
        }

        private static string SettlementNames(
            List<CARegionalSettlementRecord> settlements, string skip)
        {
            var names = new List<string>();
            foreach (CARegionalSettlementRecord record in settlements)
            {
                string name = record.name ?? "settlement " + record.slot;
                if (name != skip) names.Add(name);
            }
            return string.Join(", ", names.ToArray());
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
