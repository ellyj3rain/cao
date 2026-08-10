using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Native tools pushed to their limit before bespoke logic: the patrol
    // base is a real ZONE (drawn, named, renamed, scribed, and deleted with
    // the game's own zone machinery), and facility identity rides the
    // native ROOM ROLE system - a room qualifies as a command post or an
    // armory exactly the way vanilla qualifies a lab or a kitchen: by what
    // it contains. Bespoke logic begins only where native surfaces end -
    // membership, duties, and cross-map persistence stay with the
    // organization layer.

    public class Zone_CAPatrolBase : Zone
    {
        public Zone_CAPatrolBase() { }

        public Zone_CAPatrolBase(ZoneManager zoneManager)
            : base("patrol base", zoneManager) { }

        protected override Color NextZoneColor
        {
            get { return new Color(0.55f, 0.58f, 0.32f, 0.09f); }
        }

        public int ArmedOccupants()
        {
            int armed = 0;
            var colonists = Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (!ContainsCell(p.Position)) continue;
                if (p.equipment?.Primary != null) armed++;
            }
            return armed;
        }

        public int Occupants()
        {
            int n = 0;
            var colonists = Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                if (ContainsCell(colonists[i].Position)) n++;
            return n;
        }

        public bool ContainsCell(IntVec3 c)
        {
            return c.InBounds(Map) && Map.zoneManager.ZoneAt(c) == this;
        }

        // FURNISH CAMP: the map is large enough that traversal itself
        // demands shelter - tents and rolls whether or not you have the
        // organizational merit for a proper patrol base. Blueprints, not
        // spawns: colonists raise their own camp. The kit prefers a tent
        // mod's shelter when one is installed (candidate chain, filled
        // against the actual defNames at install time); the vanilla
        // fallback is bedrolls, a campfire, and stools - a real camp
        // either way.
        // Verified against the acquired Camping Stuff source (GPL-2.0,
        // Nandonalt/Alias44 - see CREDITS.md): tents deploy as minified
        // TentBag items, so once ported they join the kit through the
        // install path; the buildable-def chain remains for any mod that
        // ships a directly buildable tent.
        private static readonly string[] TentCandidates =
        {
            "NCS_TentBag", "NCS_MiniTentBag", "Tent"
        };

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            yield return new Command_Action
            {
                defaultLabel = "furnish camp",
                defaultDesc = "Blueprint a traveling camp inside this"
                    + " patrol base: shelter if a tent mod provides one,"
                    + " otherwise bedrolls, a campfire, and stools."
                    + " Colonists build it with their own hands.",
                icon = ContentFinder<Texture2D>.Get(
                    "UI/Designators/ZoneCreate_Growing"),
                action = FurnishCamp
            };
        }

        private void FurnishCamp()
        {
            var spots = new List<IntVec3>();
            List<IntVec3> zcells = Cells;
            for (int i = 0; i < zcells.Count && spots.Count < 8; i++)
            {
                IntVec3 c = zcells[i];
                if (!c.Standable(Map) || c.GetEdifice(Map) != null)
                    continue;
                bool blocked = false;
                List<Thing> things = c.GetThingList(Map);
                for (int t = 0; t < things.Count; t++)
                    if (things[t] is Blueprint || things[t] is Frame)
                    { blocked = true; break; }
                if (!blocked) spots.Add(c);
            }
            if (spots.Count == 0)
            {
                Messages.Message("No open ground in the patrol base to"
                    + " furnish.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            int placed = 0;
            int spot = 0;
            // TENT-FIRST, through the ported Camping Stuff flow: a PACKED
            // tent in stock (a minified tent bag) gets an INSTALL
            // blueprint at the camp - haulers carry it out, builders raise
            // it, walls and roof included. Their own PlaceWorker validates
            // the full footprint through CanPlaceBlueprintAt.
            bool tentPlaced = false;
            ThingDef miniDef = DefDatabase<ThingDef>.GetNamedSilentFail(
                "NCS_MiniTentBag");
            if (miniDef != null)
            {
                MinifiedThing bag = null;
                List<Thing> bags = Map.listerThings.ThingsOfDef(miniDef);
                for (int i = 0; i < bags.Count; i++)
                    if (bags[i] is MinifiedThing mt && mt.Spawned)
                    { bag = mt; break; }
                if (bag != null)
                {
                    List<IntVec3> zc = Cells;
                    for (int i = 0; i < zc.Count && !tentPlaced; i++)
                    {
                        IntVec3 c = zc[i];
                        try
                        {
                            if (!GenConstruct.CanPlaceBlueprintAt(
                                bag.InnerThing.def, c, Rot4.North, Map)
                                .Accepted) continue;
                            GenConstruct.PlaceBlueprintForInstall(bag, c,
                                Map, Rot4.North, Faction.OfPlayer);
                            tentPlaced = true;
                            placed++;
                        }
                        catch { break; }
                    }
                }
            }
            ThingDef tent = null;
            if (!tentPlaced)
                for (int i = 0; i < TentCandidates.Length; i++)
                {
                    tent = DefDatabase<ThingDef>.GetNamedSilentFail(
                        TentCandidates[i]);
                    if (tent != null && tent.BuildableByPlayer) break;
                    tent = null;
                }
            if (tent != null && spot < spots.Count)
            {
                if (TryBlueprint(tent, spots[spot], null)) placed++;
                spot++;
            }
            ThingDef bedroll = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Bedroll");
            ThingDef cloth = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Cloth");
            for (int i = 0; i < 3 && spot < spots.Count; i++, spot++)
                if (bedroll != null
                    && TryBlueprint(bedroll, spots[spot], cloth)) placed++;
            ThingDef fire = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Campfire");
            if (fire != null && spot < spots.Count)
            {
                if (TryBlueprint(fire, spots[spot], null)) placed++;
                spot++;
            }
            ThingDef stool = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Stool");
            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail(
                "WoodLog");
            for (int i = 0; i < 2 && spot < spots.Count; i++, spot++)
                if (stool != null
                    && TryBlueprint(stool, spots[spot], wood)) placed++;
            Messages.Message("Camp blueprinted - " + placed
                + " pieces"
                + (tentPlaced
                    ? " (packed tent from stock - install ordered)"
                    : tent != null ? " (tent shelter included)"
                    : " (no packed tent in stock - craft tent poles and a"
                    + " cover, pack a tent bag, and furnish again for"
                    + " shelter)") + ".",
                MessageTypeDefOf.TaskCompletion, false);
            CAOrganizationWorldComponent.Current?.EnsureColony().Record(
                "security", "camp furnished at " + label + " - " + placed
                + " pieces blueprinted");
        }

        private bool TryBlueprint(ThingDef def, IntVec3 cell,
            ThingDef stuff)
        {
            try
            {
                GenConstruct.PlaceBlueprintForBuild(def, cell, Map,
                    Rot4.North, Faction.OfPlayer,
                    def.MadeFromStuff
                        ? (stuff ?? GenStuff.DefaultStuffFor(def)) : null);
                return true;
            }
            catch { return false; }
        }

        public override string GetInspectString()
        {
            int occ = Occupants();
            int armed = ArmedOccupants();
            string s = "Patrol base - a concealed position held short and"
                + " quiet.";
            s += "\nOccupants: " + occ + " (" + armed + " armed)";
            s += "\nSecurity: " + (armed > 0 ? "POSTED" : "not posted");
            return s;
        }
    }

    public class Designator_ZoneAdd_CAPatrolBase : Designator_ZoneAdd
    {
        protected override string NewZoneLabel
        {
            get { return "patrol base"; }
        }

        public Designator_ZoneAdd_CAPatrolBase()
        {
            zoneTypeToPlace = typeof(Zone_CAPatrolBase);
            defaultLabel = "patrol base zone";
            defaultDesc = "Designate a patrol base - a concealed position a"
                + " team occupies briefly. The organization registers it as"
                + " a standing security practice; security reads as posted"
                + " while armed colonists hold it.";
            icon = ContentFinder<Texture2D>.Get(
                "UI/Designators/ZoneCreate_Growing");
            soundSucceeded = SoundDefOf.Designate_ZoneAdd_Stockpile;
        }

        protected override Zone MakeNewZone()
        {
            return new Zone_CAPatrolBase(Verse.Find.CurrentMap.zoneManager);
        }
    }

    // A room with working comms is a command post; a room warehousing real
    // weaponry is an armory. Scored the native way, shown in the native
    // room readout, available to ANY faction's rooms - including generated
    // settlement interiors, which self-identify with these roles the same
    // as player rooms.
    public class RoomRoleWorker_CACommandPost : RoomRoleWorker
    {
        public override float GetScore(Room room)
        {
            int consoles = 0;
            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Building_CommsConsole) consoles++;
            return consoles * 90f;
        }
    }

    public class RoomRoleWorker_CAArmory : RoomRoleWorker
    {
        public override float GetScore(Room room)
        {
            int weapons = 0;
            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t.def != null && t.def.IsWeapon && t.Spawned
                    && t.def.category == ThingCategory.Item)
                    weapons++;
            }
            if (weapons < 4) return 0f;
            return weapons * 30f;
        }
    }

    // The garrison's behavioral half: where a settlement's organization owns
    // a line, its armed residents MAN it - duties spread along the line's
    // cells instead of the engine's single center flag. Falls back to the
    // stock defend-base behavior whenever no organizational line exists.
    public class LordToil_CAOrganizationDefense : LordToil_DefendBase
    {
        private readonly IntVec3 center;

        public LordToil_CAOrganizationDefense(IntVec3 baseCenter)
            : base(baseCenter)
        {
            center = baseCenter;
        }

        // A received warning is ACTED ON: sleepers wake, drifters re-anchor
        // to their posts. Word arriving in time changes the picture.
        // And THE DEFENCE AXIS DECIDES WHO ELSE RISES: the muster lives
        // here, on the stand-to itself, so the defence organization
        // responds because a threat exists - whichever subsystem raised
        // the alarm (a patrol's report, a delivered warning, the
        // threat-watch seeing hostiles approach).
        public void StandTo()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn p = lord.ownedPawns[i];
                if (p.equipment?.Primary == null) continue;
                try
                {
                    if (!p.Awake()) RestUtility.WakeUp(p);
                    p.jobs?.CheckForJobOverride();
                }
                catch { }
            }
            UpdateAllDuties();
            CADefenceMuster.Muster(lord, center);
        }

        public override void UpdateAllDuties()
        {
            base.UpdateAllDuties();
            List<IntVec3> line = FindOrganizationLine();
            if (line == null || line.Count == 0) return;

            var armed = new List<Pawn>();
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn p = lord.ownedPawns[i];
                if (p.equipment?.Primary != null
                    && p.RaceProps.Humanlike) armed.Add(p);
            }
            if (armed.Count == 0) return;
            for (int i = 0; i < armed.Count; i++)
            {
                IntVec3 post = line[(i * line.Count) / armed.Count];
                armed[i].mindState.duty = new PawnDuty(
                    DutyDefOf.DefendBase, post);
            }
        }

        private List<IntVec3> FindOrganizationLine()
        {
            try
            {
                CARegionalWorldComponent regional =
                    CARegionalWorldComponent.Current;
                CAOrganizationWorldComponent orgs =
                    CAOrganizationWorldComponent.Current;
                var arrComp = CAArrangementMapComponent.For(Map);
                if (regional == null || orgs == null || arrComp == null)
                    return null;
                for (int i = 0; i < regional.Records.Count; i++)
                {
                    CARegionalSettlementRecord record = regional.Records[i];
                    if (record.faction != lord.faction
                        || record.localRect == CellRect.Empty
                        || !record.localRect.Contains(center)) continue;
                    CAOrganization org = orgs.ByKey(
                        record.regionalId + "#" + record.slot);
                    if (org == null) return null;
                    for (int j = 0; j < org.securityPractices.Count; j++)
                    {
                        CASecurityPractice sp = org.securityPractices[j];
                        if (sp.mapId != Map.uniqueID
                            || sp.kindLabel != "line") continue;
                        for (int k = 0; k < arrComp.All.Count; k++)
                            if (arrComp.All[k].id == sp.arrangementId)
                                return arrComp.All[k].cells;
                    }
                    return null;
                }
            }
            catch (Exception) { }
            return null;
        }
    }

    // WHO ELSE RISES WHEN THE GARRISON STANDS TO. The defence axis is
    // the answer: a levy faction calls every able member to arms, a
    // militia turns out half, a professional force or warrior caste
    // answers with the standing body it already keeps, and a faction
    // with no defence form musters nobody beyond the garrison AI. The
    // muster binds MEMBERS - obligation and entitlement are the same
    // rolls. Lives on the stand-to itself so every escalation path -
    // patrol report, delivered warning, the threat-watch - musters the
    // same way; a cooldown keeps repeated alarms from re-recording.
    internal static class CADefenceMuster
    {
        private static readonly Dictionary<int, int> lastMuster =
            new Dictionary<int, int>();
        private const int Cooldown = 10000;

        internal static void Muster(Lord lord, IntVec3 post)
        {
            try
            {
                if (lord?.faction == null || lord.Map == null) return;
                int now = Find.TickManager.TicksGame;
                int last;
                if (lastMuster.TryGetValue(lord.loadID, out last)
                    && now - last < Cooldown) return;

                CAOrganization org = OrgOf(lord);
                if (org == null) return;
                CAPolicyRecord form = null;
                for (int i = 0; i < org.policies.Count; i++)
                    if (org.policies[i] != null
                        && org.policies[i].key == "defence")
                    { form = org.policies[i]; break; }
                float share = form?.value == "levy" ? 1f
                    : form?.value == "militia" ? 0.5f : 0f;
                if (share <= 0f) return;

                var able = new List<Pawn>();
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Pawn p = lord.ownedPawns[i];
                    if (p == null || p.Dead || p.Downed || !p.Spawned
                        || !p.RaceProps.Humanlike || p.InMentalState)
                        continue;
                    if (p.mindState?.duty?.def == DutyDefOf.DefendBase)
                        continue; // already standing
                    if (!org.memberPawnIds.Contains(p.thingIDNumber))
                        continue;
                    able.Add(p);
                }
                int called = (int)Math.Round(able.Count * share);
                if (called <= 0) return;
                if (!post.IsValid && lord.ownedPawns.Count > 0)
                    post = lord.ownedPawns[0].Position;
                if (!post.IsValid) return;
                int raised = 0;
                for (int i = 0; i < called && i < able.Count; i++)
                {
                    Pawn p = able[i];
                    try
                    {
                        p.mindState.duty = new PawnDuty(
                            DutyDefOf.DefendBase, post);
                        p.jobs?.CheckForJobOverride();
                        raised++;
                    }
                    catch { }
                }
                if (raised > 0)
                {
                    lastMuster[lord.loadID] = now;
                    org.Record("defence", form.value == "levy"
                        ? "the levy was called - " + raised
                            + " member(s) rose to arms"
                        : "the militia turned out - " + raised
                            + " member(s) mustered");
                }
            }
            catch (Exception) { }
        }

        private static CAOrganization OrgOf(Lord lord)
        {
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            if (world == null || orgs == null) return null;
            CARegionalSettlementRecord best = null;
            float bestDistance = float.MaxValue;
            IntVec3 anchor = lord.ownedPawns.Count > 0
                ? lord.ownedPawns[0].Position
                : lord.Map.Center;
            foreach (CARegionalSettlementRecord record in
                world.ForMap(lord.Map))
            {
                if (record?.faction != lord.faction
                    || record.localRect == CellRect.Empty) continue;
                float distance = record.localRect.CenterCell
                    .DistanceTo(anchor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = record;
                }
            }
            return best == null ? null
                : orgs.ByKey(best.regionalId + "#" + best.slot);
        }
    }
}
