using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // A standard map saves its frontier realization the first time that map is
    // processed. Regional maps use the holding rows saved on their region plan.
    public sealed class CAFrontierMapPlan : IExposable
    {
        public int mapId = -1;
        public int mapTileId = -1;
        public int mapWidth;
        public int mapHeight;
        public int realizationSourceHash;
        public List<CAFrontierHoldingPlan> holdings =
            new List<CAFrontierHoldingPlan>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref mapTileId, "mapTileId", -1);
            Scribe_Values.Look(ref mapWidth, "mapWidth", 0);
            Scribe_Values.Look(ref mapHeight, "mapHeight", 0);
            Scribe_Values.Look(ref realizationSourceHash,
                "realizationSourceHash", 0);
            Scribe_Collections.Look(ref holdings, "holdings", LookMode.Deep);
            if (holdings == null)
                holdings = new List<CAFrontierHoldingPlan>();
        }
    }

    public static class CAFrontier
    {
        private static int HoldingsFor(CAOrganizationWorldComponent comp,
            Map map)
        {
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            if (plan?.frontierHoldings != null)
                return Mathf.Clamp(plan.frontierHoldings.Count, 0, 8);
            CAFrontierMapPlan mapPlan = comp?.EnsureFrontierMapPlan(map);
            return Mathf.Clamp(mapPlan?.holdings?.Count ?? 0, 0, 8);
        }

        private static CAFrontierHoldingPlan HoldingPlanFor(
            CAOrganizationWorldComponent comp, Map map, int index)
        {
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            if (plan?.frontierHoldings != null
                && index >= 0 && index < plan.frontierHoldings.Count)
                return plan.frontierHoldings[index];
            CAFrontierMapPlan mapPlan = comp?.EnsureFrontierMapPlan(map);
            return mapPlan?.holdings != null && index >= 0
                && index < mapPlan.holdings.Count
                    ? mapPlan.holdings[index] : null;
        }

        public static void EnsureHoldings(CAOrganizationWorldComponent comp)
        {
            if (comp == null || Current.Game == null) return;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!map.IsPlayerHome) continue;
                int existing = 0;
                for (int i = 0; i < comp.Organizations.Count; i++)
                    if (comp.Organizations[i].organizationKey.StartsWith(
                        "frontier:" + map.uniqueID + ":")) existing++;
                int want = HoldingsFor(comp, map);
                // New maps seed saved holdings promptly. Older maps add one
                // saved holding per pulse after the initial delay.
                if (existing < want && Find.TickManager.TicksGame
                    - map.generationTick < 20000)
                {
                    // [perf] Frontier holdings are still generated with the map,
                    // but two holdings per pulse instead of eight in
                    // one tick: each site costs a reachability flood
                    // across the whole map, and on a two-million-cell
                    // region eight of those in a single frame is a
                    // visible freeze. Four pulses inside the window
                    // seat the full complement.
                    var taken = new List<IntVec3>();
                    for (int k = 0; k < 2 && existing + k < want; k++)
                    {
                        int index = existing + k;
                        if (!TrySeedHolding(comp, map, index,
                                HoldingPlanFor(comp, map, index), taken)) break;
                    }
                }
                else if (existing < want
                    && Find.TickManager.TicksGame > 120000)
                    TrySeedHolding(comp, map, existing,
                        HoldingPlanFor(comp, map, existing));
                Parley(comp, map);
            }
        }

        // A threatened holding chooses whether to pay, withdraw, or defend.
        // The result governs the holding's people and remains open to player
        // intervention; it does not issue orders to the attacking faction.
        private static void Parley(CAOrganizationWorldComponent comp,
            Map map)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < comp.Organizations.Count; i++)
            {
                CAOrganization org = comp.Organizations[i];
                if (!org.organizationKey.StartsWith(
                    "frontier:" + map.uniqueID + ":")) continue;
                if (org.memberPawnIds.Count == 0) continue;

                Pawn householder = null;
                var folk = new List<Pawn>();
                var spawned = map.mapPawns.AllPawnsSpawned;
                for (int p = 0; p < spawned.Count; p++)
                    if (org.memberPawnIds.Contains(
                        spawned[p].thingIDNumber) && !spawned[p].Dead)
                    {
                        folk.Add(spawned[p]);
                        if (org.offices.Count > 0
                            && spawned[p].thingIDNumber
                                == org.offices[0].holderId)
                            householder = spawned[p];
                    }
                if (folk.Count == 0) continue;
                if (householder == null) householder = folk[0];
                IntVec3 hearth = householder.Position;

                var enemies = new List<Pawn>();
                for (int p = 0; p < spawned.Count; p++)
                {
                    Pawn e = spawned[p];
                    if (e.Downed || e.Faction == null
                        || folk.Contains(e)) continue;
                    if (!e.Position.InHorDistOf(hearth, 26f)) continue;
                    try
                    {
                        bool againstThem = householder.Faction != null
                            ? e.Faction.HostileTo(householder.Faction)
                            : e.Faction.HostileTo(Faction.OfPlayer);
                        if (againstThem && e.Faction != Faction.OfPlayer)
                            enemies.Add(e);
                    }
                    catch { }
                }

                // Record whether the colony arrived before the deadline.
                if (org.pendingAidDeadline > 0)
                {
                    bool relieved = false;
                    var cols = map.mapPawns.FreeColonistsSpawned;
                    for (int c = 0; c < cols.Count; c++)
                        if (cols[c].Position.InHorDistOf(hearth, 30f))
                        { relieved = true; break; }
                    if (enemies.Count == 0)
                    {
                        org.pendingAidDeadline = -1;
                        org.Record("relations",
                            "the threat passed us by");
                        continue;
                    }
                    if (relieved)
                    {
                        // Relief CHANGES THE FACTS; it does not end the
                        // confrontation by decree. Both sides reassess -
                        // and a fanatical or overwhelming enemy may press
                        // on regardless of who just rode up.
                        CAOrganization colonyR = comp.EnsureColony();
                        org.Record("warning-honored",
                            colonyR.name + " rode out and stood with us -"
                            + " the ground has changed");
                        colonyR.Record("warning-honored",
                            "relieved " + org.name
                            + " under threat - they will not forget");
                        org.publicSupport = Mathf.Min(1f,
                            org.publicSupport + 0.1f);
                    }
                    if (now < org.pendingAidDeadline) continue;
                    Resolve(comp, org, householder, folk, enemies, map,
                        relieved);
                    org.pendingAidDeadline = -1;
                    continue;
                }

                if (enemies.Count == 0) continue;
                if (now - org.lastWarningTick < 60000) continue;
                org.lastWarningTick = now;
                org.pendingAidDeadline = now + 1250;
                org.Record("relations", "an armed party stands on our"
                    + " ground - " + enemies.Count + " of them, "
                    + folk.Count + " of us");
                comp.EnsureColony().Record("warning-received",
                    org.name + " is confronted by " + enemies.Count
                    + " armed strangers");
                // WHETHER ANYONE WALKS OUT IS AGENCY, never a posture
                // the system forces. Nerve, a voice worth using, the
                // weight of responsibility for the household, and the
                // sheer odds all argue - and plenty of people go to
                // ground instead, which is its own honest answer.
                string walkBasis;
                bool walks = WillWalk(householder, org, folk.Count,
                    enemies.Count, out walkBasis);
                if (!walks)
                {
                    org.pendingAidDeadline = -1;
                    org.Record("relations", "no one walked out - the"
                        + " household went to ground [" + walkBasis
                        + "]");
                    Flee(folk, map);
                    Messages.Message(org.name + " goes to ground before "
                        + enemies.Count + " armed strangers - nobody"
                        + " walks out. (" + walkBasis + ")",
                        new LookTargets(hearth, map),
                        MessageTypeDefOf.ThreatSmall, false);
                    continue;
                }
                try
                {
                    Job walk = JobMaker.MakeJob(JobDefOf.Goto,
                        enemies[0].Position);
                    walk.playerForced = false;
                    var context = CABehaviorContext.ForPawn(householder,
                        CAAuthorityOrigin.Household,
                        authoritySatisfied: true,
                        capabilitySatisfied: householder.CanReach(
                            enemies[0].Position, PathEndMode.OnCell,
                            Danger.Deadly),
                        materialSatisfied: enemies[0].Position
                            .Standable(map),
                        currentIntentCompatible: householder.CurJob == null
                            || !householder.CurJob.playerForced,
                        directPlayerOwnership: householder.CurJob != null
                            && householder.CurJob.playerForced,
                        authorityBasis: "frontier household " + org.name,
                        knowledgeBasis:
                            "householder directly confronts the visible armed party",
                        owner: org.name + " household");
                    CABehaviorDecision decision;
                    CAIntentContext intent;
                    if (CABehaviorJobOrigin.TryAuthorizeAndRegister(
                            householder, walk,
                            "institution.frontier_household_activity",
                            CAIntentController.Frontier, context,
                            out decision, out intent,
                            "meet armed party at " + enemies[0].Position,
                            org.name + " household", 1800))
                        householder.jobs.TryTakeOrderedJob(walk);
                }
                catch { }
                Messages.Message(householder.LabelShort + " walks out to"
                    + " meet " + enemies.Count + " armed strangers at "
                    + org.name + " (" + walkBasis + ") - there is time to"
                    + " ride out.",
                    new LookTargets(hearth, map),
                    MessageTypeDefOf.ThreatSmall, false);
            }
        }

        // Agency: does this person walk into the open at all?
        private static bool WillWalk(Pawn who, CAOrganization org,
            int ours, int theirs, out string basis)
        {
            float nerve = 0.5f;
            float voice = 0.5f;
            try
            {
                DispositionProfile d = Disposition.Of(who);
                nerve = d.discipline * 0.5f + d.aggression * 0.3f
                    + 0.2f;
            }
            catch { }
            try
            {
                voice = who.GetStatValue(StatDefOf.NegotiationAbility);
            }
            catch { }
            bool responsible = org.offices.Count > 0
                && org.offices[0].holderId == who.thingIDNumber;
            float odds = Mathf.Clamp((theirs - ours) * 0.05f, 0f, 0.4f);
            float will = nerve * 0.5f + voice * 0.3f
                + (responsible ? 0.2f : 0f) - odds;
            basis = "nerve " + nerve.ToString("0.00") + ", voice "
                + voice.ToString("0.00")
                + (responsible ? ", theirs to answer for" : "")
                + ", odds -" + odds.ToString("0.00");
            return will >= 0.5f;
        }

        // The armed faction may read an unarmed approach as good faith or
        // weakness.
        internal static float ReadUnarmed(Faction aggressor,
            out string note)
        {
            note = null;
            if (aggressor?.def == null) return 0f;
            try
            {
                if (aggressor.def.permanentEnemy
                    || !aggressor.def.humanlikeFaction)
                {
                    note = "they read an unarmed figure as prey -0.10";
                    return -0.1f;
                }
                if (aggressor.def.naturalEnemy)
                {
                    note = "they read it as weakness -0.05";
                    return -0.05f;
                }
                Ideo ideo = aggressor.ideos?.PrimaryIdeo;
                if (ideo != null)
                    for (int i = 0; i < ideo.memes.Count; i++)
                    {
                        string dn = ideo.memes[i].defName;
                        if (dn == "Raider" || dn == "Supremacist")
                        {
                            note = "their creed reads it as weakness"
                                + " -0.10";
                            return -0.1f;
                        }
                    }
                note = "they read it as good faith +0.10";
                return 0.1f;
            }
            catch { note = null; return 0f; }
        }

        private static void Resolve(CAOrganizationWorldComponent comp,
            CAOrganization org, Pawn householder, List<Pawn> folk,
            List<Pawn> enemies, Map map, bool relieved)
        {
            CAOrganization colony = comp.EnsureColony();
            var parts = new System.Text.StringBuilder();
            float standing = 0f;
            float negotiation = 0.5f;
            try
            {
                negotiation = householder.GetStatValue(
                    StatDefOf.NegotiationAbility);
            }
            catch { }
            standing += negotiation * 0.5f;
            parts.Append("their voice " + (negotiation * 0.5f)
                .ToString("+0.00"));
            float supportScore = (org.publicSupport - 0.5f) * 0.4f;
            standing += supportScore;
            parts.Append(", public support "
                + supportScore.ToString("+0.00;-0.00"));
            bool protectedByPlayer = comp.HasActiveAgreement("player",
                org.organizationKey, "defense", "protection")
                || org.affiliatedWithPlayer;
            if (protectedByPlayer)
            {
                standing += 0.3f;
                parts.Append(", the colony's protection +0.30");
            }
            float disparity = Mathf.Min(0.5f,
                (enemies.Count - folk.Count) * 0.08f);
            if (disparity > 0f)
            {
                standing -= disparity;
                parts.Append(", against " + enemies.Count + " of them -"
                    + disparity.ToString("0.00"));
            }
            // The unarmed signal, read by THEM.
            if (householder.equipment?.Primary == null)
            {
                string note;
                float read = ReadUnarmed(enemies[0].Faction, out note);
                if (note != null)
                {
                    standing += read;
                    parts.Append(", " + note);
                }
            }
            // Relief is a FACT ON THE GROUND, weighed - not a verdict.
            // Against a fanatical or overwhelming aggressor it may not be
            // enough, and that refusal is theirs to make.
            if (relieved)
            {
                float reliefWeight = 0.35f;
                string temper = "";
                try
                {
                    Faction agg = enemies[0].Faction;
                    if (agg.def.permanentEnemy
                        || !agg.def.humanlikeFaction)
                    {
                        reliefWeight = 0.05f;
                        temper = " (they do not bargain)";
                    }
                    else if (enemies.Count >= folk.Count * 4)
                    {
                        reliefWeight = 0.15f;
                        temper = " (they still hold the numbers)";
                    }
                }
                catch { }
                standing += reliefWeight;
                parts.Append(", guns at our back +"
                    + reliefWeight.ToString("0.00") + temper);
            }

            string outcome;
            if (standing >= 0.5f)
            {
                int paid = (int)Mathf.Min(org.treasury, 60f);
                org.treasury -= paid;
                outcome = "bought their lives - " + paid
                    + " silver and a promise";
                Flee(folk, map);
            }
            else if (standing >= 0.25f)
            {
                int paid = (int)org.treasury;
                org.treasury = 0f;
                outcome = "stripped bare - " + paid
                    + " silver, everything they had";
                org.publicSupport = Mathf.Max(0.2f, org.publicSupport - 0.1f);
                Flee(folk, map);
            }
            else
            {
                outcome = "the parley failed - they stand on their ground";
                org.publicSupport = Mathf.Max(0.2f, org.publicSupport - 0.05f);
            }
            org.Record("relations", "PARLEY: " + outcome + " ["
                + parts + "]");
            colony.Record("relations", org.name + " parleyed with armed"
                + " strangers - " + outcome);
            Messages.Message(householder.LabelShort + " at " + org.name
                + ": " + outcome + ". (" + parts + ")",
                new LookTargets(householder.Position, map),
                standing >= 0.25f
                    ? MessageTypeDefOf.NeutralEvent
                    : MessageTypeDefOf.NegativeEvent, false);
        }

        private static void Flee(List<Pawn> folk, Map map)
        {
            for (int i = 0; i < folk.Count; i++)
            {
                try
                {
                    IntVec3 edge;
                    if (!CellFinder.TryFindRandomEdgeCellWith(
                        c => c.Standable(map)
                            && map.reachability.CanReach(
                                folk[i].Position, c, PathEndMode.OnCell,
                                TraverseParms.For(folk[i])),
                        map, CellFinder.EdgeRoadChance_Neutral,
                        out edge)) continue;
                    Job go = JobMaker.MakeJob(JobDefOf.Goto, edge);
                    Pawn resident = folk[i];
                    var context = CABehaviorContext.ForPawn(resident,
                        CAAuthorityOrigin.Household,
                        authoritySatisfied: true,
                        capabilitySatisfied: resident.CanReach(edge,
                            PathEndMode.OnCell, Danger.Deadly),
                        materialSatisfied: edge.Standable(map),
                        currentIntentCompatible: resident.CurJob == null
                            || !resident.CurJob.playerForced,
                        directPlayerOwnership: resident.CurJob != null
                            && resident.CurJob.playerForced,
                        authorityBasis: "current frontier household alarm",
                        knowledgeBasis:
                            "household alarm; no hostile identity granted",
                        owner: "frontier household survival");
                    CABehaviorDecision decision;
                    CAIntentContext intent;
                    if (CABehaviorJobOrigin.TryAuthorizeAndRegister(resident,
                            go, "survival.frontier_flight",
                            CAIntentController.Frontier, context,
                            out decision, out intent,
                            "clear holding toward edge " + edge,
                            "frontier household survival", 1800))
                        resident.jobs.TryTakeOrderedJob(go);
                }
                catch { }
            }
        }

        private static bool TrySeedHolding(
            CAOrganizationWorldComponent comp, Map map, int index,
            CAFrontierHoldingPlan holding, List<IntVec3> taken = null)
        {
            if (holding == null) return false;
            bool factionless = holding.factionless;
            Faction flag = factionless ? null : PickFlag();
            if (!factionless && flag == null) factionless = true;
            IntVec3 site;
            IntVec3 preferred = IntVec3.Invalid;
            if (holding != null && holding.memberTileId >= 0)
                preferred = map.GetComponent<CARegionalProjectionMapComponent>()
                    ?.CenterForMember(holding.memberTileId)
                    ?? IntVec3.Invalid;
            int siteSeed = Gen.HashCombineInt(map.uniqueID,
                holding.key, holding.memberTileId,
                509203);
            Rand.PushState(siteSeed);
            try
            {
                if (!TryFindSite(map, out site, taken, preferred)) return false;
            }
            finally { Rand.PopState(); }

            var folk = new List<Pawn>();
            int count = Mathf.Clamp(holding.householdSize, 1, 6);
            PawnKindDef kind = PawnKindDefOf.Villager;
            if (flag != null)
                try
                {
                    PawnKindDef fk = flag.RandomPawnKind();
                    if (fk != null && fk.RaceProps != null
                        && fk.RaceProps.Humanlike) kind = fk;
                }
                catch { }
            for (int i = 0; i < count; i++)
            {
                try
                {
                    Pawn p = PawnGenerator.GeneratePawn(kind, flag);
                    IntVec3 spot;
                    if (!CellFinder.TryFindRandomCellNear(site, map, 4,
                        c => c.Standable(map) && !c.Fogged(map),
                        out spot)) spot = site;
                    GenSpawn.Spawn(p, spot, map);
                    folk.Add(p);
                }
                catch { }
            }
            if (folk.Count == 0) return false;
            taken?.Add(site);

            // Form selects the physical layout. Material level adds saved
            // furnishings and storage independently of faction ownership.
            bool materialized = false;
            if (flag != null)
                try
                {
                    CAMorphologyAdapter.Materialize(map,
                        CellRect.CenteredOn(site, 26, 26)
                            .ClipInsideMap(map),
                        holding.form == 1
                            ? CAMorphForm.FrontierHomestead
                            : CAMorphForm.Cabin,
                        site.GetHashCode(), flag);
                    materialized = true;
                }
                catch { }
            if (materialized)
                SpawnMaterialDetails(map, site, flag, holding.materialLevel);
            else
                SpawnHomestead(map, site, flag, folk.Count,
                    holding.materialLevel, holding.form);

            string family = folk[0].Name != null
                ? folk[0].Name.ToStringShort : "frontier";
            CAOrganization org = comp.EnsureFor(
                "frontier:" + map.uniqueID + ":" + index,
                family + "'s holding",
                factionless
                    ? "Unaffiliated frontier household."
                    : "Frontier household affiliated with " + flag.Name
                    + ".", CAOrganizationKind.Household);
            org.offices.Add(new CAOffice
            {
                sourceKey = "householder",
                name = "householder",
                seniority = 200,
                holderId = folk[0].thingIDNumber,
                holderLabel = folk[0].LabelShort,
                grants = "represents this household"
            });
            var handIds = new List<int>();
            for (int i = 0; i < folk.Count; i++)
                handIds.Add(folk[i].thingIDNumber);
            org.groups.Add(new CAOrganizationGroup
            {
                name = "labor",
                memberIds = handIds,
                standing = 1f
            });
            org.claims.Add(new CAClaim
            {
                kind = "core",
                label = "homestead ground",
                area = 49,
                mapId = map.uniqueID
            });
            org.treasury = 60f;
            org.lastPopulation = folk.Count;
            org.memberPawnIds = new List<int>(handIds);
            org.Record("relations", "Holding established: "
                + folk.Count + (factionless
                    ? " unaffiliated residents"
                    : " residents affiliated with " + flag.Name)
                + "; material level " + (holding?.materialLevel ?? 1)
                + "; " + ((holding?.form ?? 0) == 1
                    ? "established homestead" : "cabin"));

            CAOrganization colony = comp.EnsureColony();
            colony.Record("relations", "Nearby frontier holding: " + org.name);
            Messages.Message((factionless
                ? "Unaffiliated frontier folk have raised a holding"
                + " nearby: " : "Frontier folk have raised a holding"
                + " nearby: ") + org.name
                + (factionless ? " (no flag - they may be invited to"
                    + " affiliate)" : "") + ".",
                new LookTargets(site, map),
                MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        private static Faction PickFlag()
        {
            List<Faction> all = Find.FactionManager
                .AllFactionsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                Faction f = all[i];
                if (f.IsPlayer || f.defeated || f.Hidden
                    || f.temporary) continue;
                if (f.def == null || !f.def.humanlikeFaction) continue;
                try
                {
                    if (f.PlayerRelationKind
                        == FactionRelationKind.Hostile) continue;
                }
                catch { continue; }
                return f;
            }
            return null;
        }

        private static bool TryFindSite(Map map, out IntVec3 site,
            List<IntVec3> taken = null,
            IntVec3 preferredCenter = default(IntVec3))
        {
            site = IntVec3.Invalid;
            IntVec3 home = map.Center;
            try
            {
                var cols = map.mapPawns.FreeColonistsSpawned;
                if (cols.Count > 0) home = cols[0].Position;
            }
            catch { }
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            // Reachability floods are the expensive check on a giant map:
            // fewer attempts there, and every cheap filter runs first.
            int maxTries = map.Size.x * map.Size.z > 1000000 ? 80 : 220;
            for (int tries = 0; tries < maxTries; tries++)
            {
                IntVec3 c = IntVec3.Invalid;
                if (preferredCenter.IsValid
                    && !CellFinder.TryFindRandomCellNear(preferredCenter,
                        map, Math.Max(35,
                            Math.Min(map.Size.x, map.Size.z) / 4),
                        cell => cell.InBounds(map), out c))
                    c = CellFinder.RandomCell(map);
                else if (!preferredCenter.IsValid)
                    c = CellFinder.RandomCell(map);
                if (!c.Standable(map) || c.Fogged(map)) continue;
                if (c.Roofed(map)) continue;
                if (c.DistanceTo(home) < 60f) continue;
                // Birth batches pick several sites in one pass: keep
                // sibling homesteads off each other's ground.
                bool crowded = false;
                if (taken != null)
                    for (int i = 0; i < taken.Count; i++)
                        if (c.InHorDistOf(taken[i], 40f))
                        { crowded = true; break; }
                if (crowded) continue;
                bool nearSettlement = false;
                if (regional != null)
                    for (int i = 0; i < regional.Records.Count; i++)
                    {
                        CARegionalSettlementRecord r = regional.Records[i];
                        if (r.lastMapId != map.uniqueID
                            || r.localRect == CellRect.Empty) continue;
                        if (r.localRect.ExpandedBy(35).Contains(c))
                        { nearSettlement = true; break; }
                    }
                if (nearSettlement) continue;
                if (!map.reachability.CanReachMapEdge(c,
                    TraverseParms.For(TraverseMode.PassDoors))) continue;
                site = c;
                return true;
            }
            return false;
        }

        private static void SpawnHomestead(Map map, IntVec3 site,
            Faction flag, int householdSize, int materialLevel, int form)
        {
            if (form == 1) SpawnEstablishedShell(map, site, flag);
            TrySpawn(map, site, "Campfire", flag, null, 20f);
            ThingDef bedroll = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Bedroll");
            ThingDef cloth = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Cloth");
            for (int i = 1; i <= Math.Max(1, householdSize); i++)
            {
                int radial = Math.Min(GenRadial.NumCellsInRadius(4f) - 1,
                    i * 2);
                IntVec3 c = site + GenRadial.RadialPattern[radial];
                if (bedroll != null) TrySpawnDef(map, c, bedroll, flag,
                    cloth, 0f);
            }
            SpawnMaterialDetails(map, site, flag, materialLevel);
            ThingDef mini = DefDatabase<ThingDef>.GetNamedSilentFail(
                "NCS_TentBag");
            if (mini != null)
            {
                ThingDef pole = DefDatabase<ThingDef>.GetNamedSilentFail(
                    "NCS_TentPart_Pole");
                ThingDef cover = DefDatabase<ThingDef>.GetNamedSilentFail(
                    "NCS_TentPart_Cover_Small");
                IntVec3 c = site + GenRadial.RadialPattern[7];
                if (pole != null) TrySpawnDef(map, c, pole, null, null, 0f);
                if (cover != null) TrySpawnDef(map,
                    site + GenRadial.RadialPattern[8], cover, null, null,
                    0f);
            }
        }

        private static void SpawnEstablishedShell(Map map, IntVec3 site,
            Faction flag)
        {
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
            if (wall == null) return;
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (Math.Abs(dx) != 3 && Math.Abs(dz) != 3) continue;
                    IntVec3 cell = site + new IntVec3(dx, 0, dz);
                    if (dx == 0 && dz == -3 && door != null)
                        TrySpawnDef(map, cell, door, flag, wood, 0f);
                    else
                        TrySpawnDef(map, cell, wall, flag, wood, 0f);
                }
        }

        private static void SpawnMaterialDetails(Map map, IntVec3 site,
            Faction flag, int materialLevel)
        {
            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail(
                "WoodLog");
            if (materialLevel >= 1)
                TrySpawnNearby(map, site, "Stool", flag, wood, 0f, 5);
            if (materialLevel >= 2)
                TrySpawnNearby(map, site, "Table1x2c", flag, wood, 0f, 11);
            if (materialLevel >= 3)
            {
                TrySpawnNearby(map, site, "Shelf", flag, wood, 0f, 17);
                TrySpawnNearby(map, site, "TorchLamp", flag, wood, 20f, 23);
            }
        }

        private static bool TrySpawn(Map map, IntVec3 c, string defName,
            Faction faction, ThingDef stuff, float fuel)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(
                defName);
            return def != null
                && TrySpawnDef(map, c, def, faction, stuff, fuel);
        }

        private static void TrySpawnNearby(Map map, IntVec3 center,
            string defName, Faction faction, ThingDef stuff, float fuel,
            int start)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            int cells = GenRadial.NumCellsInRadius(7f);
            for (int offset = 0; offset < cells; offset++)
            {
                int index = 1 + (start + offset) % Math.Max(1, cells - 1);
                if (TrySpawnDef(map, center + GenRadial.RadialPattern[index],
                        def, faction, stuff, fuel))
                    return;
            }
        }

        private static bool TrySpawnDef(Map map, IntVec3 c, ThingDef def,
            Faction faction, ThingDef stuff, float fuel)
        {
            try
            {
                if (!c.InBounds(map) || !c.Standable(map)) return false;
                if (c.GetEdifice(map) != null) return false;
                Thing t = def.MadeFromStuff
                    ? ThingMaker.MakeThing(def, stuff
                        ?? GenStuff.DefaultStuffFor(def))
                    : ThingMaker.MakeThing(def);
                if (faction != null && def.CanHaveFaction)
                    t.SetFaction(faction);
                GenSpawn.Spawn(t, c, map);
                if (fuel > 0f)
                {
                    var comp = t.TryGetComp<CompRefuelable>();
                    if (comp != null) comp.Refuel(fuel);
                }
                return true;
            }
            catch { return false; }
        }
    }
}
