using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Fortified settlements build approach posts and send armed residents on
    // scheduled patrols through them. Contact behavior follows the settlement's
    // current defense posture. Completed patrols enter organization history.

    // One settlement's security posts and patrol schedule. A saved registry
    // entry prevents duplicate post generation.
    public sealed class CAPatrolCircuit : IExposable
    {
        public string key;
        public List<IntVec3> nodes = new List<IntVec3>();
        public int nextStartTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Collections.Look(ref nodes, "nodes", LookMode.Value);
            Scribe_Values.Look(ref nextStartTick, "nextStartTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && nodes == null)
                nodes = new List<IntVec3>();
        }
    }

    // A patrol in flight: the walker, the ordered route (every node,
    // then home), and where along it the walk stands. Scribed so a
    // saved mid-circuit patrol resumes instead of stranding its pawn
    // on a stale travel duty.
    public sealed class CAPatrolAssignment : IExposable
    {
        public string key;
        public Pawn pawn;
        public List<IntVec3> route = new List<IntVec3>();
        public int waypoint;
        public int dwellUntilTick = -1;
        public int legDeadlineTick;
        public int postsChecked;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref route, "route", LookMode.Value);
            Scribe_Values.Look(ref waypoint, "waypoint", 0);
            Scribe_Values.Look(ref dwellUntilTick, "dwellUntilTick", -1);
            Scribe_Values.Look(ref legDeadlineTick, "legDeadlineTick", 0);
            Scribe_Values.Look(ref postsChecked, "postsChecked", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && route == null)
                route = new List<IntVec3>();
        }
    }

    public class CAPatrolSystemMapComponent : MapComponent
    {
        // Cadence: a light step for walks in flight, the works-pulse
        // interval for scheduling, and a settle delay so nodes rise
        // only after the settlements themselves have.
        private const int StepInterval = 250;
        private const int PulseInterval = 1500;
        private const int SettleDelay = 2500;
        private const int DwellTicks = 450;
        private const int LegDeadline = 7500;
        private const int RetryDelay = 7500;
        private const int PatrolPeriod = 30000;
        private const float ArriveRadius = 6.5f;
        private const string DutyTag = "CA_patrol";

        private List<CAPatrolCircuit> circuits =
            new List<CAPatrolCircuit>();

        // [roads] The nearest security post this settlement keeps -
        // the road lane asks so it can lay a way out to it.
        public IntVec3 NearestNodeFor(string settlementKey, IntVec3 from)
        {
            IntVec3 best = IntVec3.Invalid;
            float bestD = float.MaxValue;
            for (int i = 0; i < circuits.Count; i++)
            {
                CAPatrolCircuit circuit = circuits[i];
                if (circuit == null || circuit.key != settlementKey
                    || circuit.nodes == null) continue;
                for (int n = 0; n < circuit.nodes.Count; n++)
                {
                    float d = circuit.nodes[n].DistanceTo(from);
                    if (d < bestD)
                    { bestD = d; best = circuit.nodes[n]; }
                }
            }
            return best;
        }
        private List<CAPatrolAssignment> patrols =
            new List<CAPatrolAssignment>();
        private int bornTick = -1;
        private int nextStepTick;
        private int nextPulseTick;

        public CAPatrolSystemMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref bornTick, "bornTick", -1);
            Scribe_Collections.Look(ref circuits, "circuits",
                LookMode.Deep);
            Scribe_Collections.Look(ref patrols, "patrols", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (circuits == null)
                    circuits = new List<CAPatrolCircuit>();
                if (patrols == null)
                    patrols = new List<CAPatrolAssignment>();
                patrols.RemoveAll(a => a == null || a.route == null
                    || a.route.Count == 0);
            }
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (bornTick < 0)
            {
                bornTick = now;
                // de-sync maps from each other from the first tick
                nextStepTick = now + map.uniqueID % StepInterval;
                nextPulseTick = now + map.uniqueID % PulseInterval;
            }
            if (now >= nextStepTick)
            {
                nextStepTick = now + StepInterval;
                try { StepPatrols(now); }
                catch (Exception e)
                {
                    Log.Warning("[CA] patrol step failed: " + e.Message);
                }
            }
            if (now >= nextPulseTick)
            {
                nextPulseTick = now + PulseInterval;
                try { Pulse(now); }
                catch (Exception e)
                {
                    Log.Warning("[CA] patrol pulse failed: " + e.Message);
                }
            }
        }

        // ---- scheduling pulse: seed nodes once, then keep at most one
        // patrol walking per settlement, each on its own offset clock ----
        private void Pulse(int now)
        {
            if (now < bornTick + SettleDelay) return;
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            if (world == null) return;
            foreach (CARegionalSettlementRecord record in world.ForMap(map))
            {
                if (record?.faction == null || record.faction.IsPlayer
                    || record.localRect == CellRect.Empty) continue;
                string key = record.regionalId + "#" + record.slot;

                // THE THREAT-WATCH: the defence organization responds
                // because a threat EXISTS - armed hostiles inside the
                // settlement's approaches stand the garrison to (and
                // muster the levy) with no patrol or messenger needed
                // to have reported them first.
                WatchForThreats(record, now);

                CAPatrolCircuit circuit = CircuitFor(key);
                if (circuit == null)
                {
                    circuit = SeedNodes(record, key);
                    circuits.Add(circuit);
                }
                if (circuit.nodes.Count == 0) continue;
                if (HasPatrol(key)) continue;
                if (circuit.nextStartTick < 0)
                    circuit.nextStartTick = now + 2000
                        + Stagger(key) % 7500;
                if (now < circuit.nextStartTick) continue;
                TryStartPatrol(record, circuit, key, now);
            }
        }

        // Re-alarms after a quiet interval; not scribed - the worst a
        // reload costs is one extra stand-to against a threat that is
        // still there, which is the correct response anyway.
        private readonly Dictionary<string, int> lastAlarm =
            new Dictionary<string, int>();
        private const int AlarmCooldown = 15000;

        private void WatchForThreats(CARegionalSettlementRecord record,
            int now)
        {
            try
            {
                int last;
                string key = record.regionalId + "#" + record.slot;
                if (lastAlarm.TryGetValue(key, out last)
                    && now - last < AlarmCooldown) return;
                CellRect approaches = record.localRect.ExpandedBy(45);
                bool threatened = map.attackTargetsCache
                    .TargetsHostileToFaction(record.faction)
                    .Any(t => t.Thing is Pawn tp && !tp.Downed
                        && tp.Spawned
                        && approaches.Contains(tp.Position));
                if (!threatened) return;
                lastAlarm[key] = now;
                List<Lord> lords = map.lordManager.lords;
                for (int l = 0; l < lords.Count; l++)
                {
                    if (lords[l].faction != record.faction) continue;
                    var toil = lords[l].CurLordToil
                        as LordToil_CAOrganizationDefense;
                    if (toil == null) continue;
                    toil.StandTo();
                    CAOrganizationWorldComponent.Current?.ByKey(key)
                        ?.Record("security", "hostiles seen inside the "
                            + "approaches - the garrison stood to");
                    break;
                }
            }
            catch { }
        }

        private CAPatrolCircuit CircuitFor(string key)
        {
            for (int i = 0; i < circuits.Count; i++)
                if (circuits[i].key == key) return circuits[i];
            return null;
        }

        private bool HasPatrol(string key)
        {
            for (int i = 0; i < patrols.Count; i++)
                if (patrols[i].key == key) return true;
            return false;
        }

        private bool PawnAssigned(Pawn p)
        {
            for (int i = 0; i < patrols.Count; i++)
                if (patrols[i].pawn == p) return true;
            return false;
        }

        private static int Stagger(string key)
        {
            return GenText.StableStringHash(key ?? "") & 0x7fffffff;
        }

        // ---- node generation: 1-3 walled outposts per fortified
        // settlement, grown once, over its approaches ----
        private CAPatrolCircuit SeedNodes(CARegionalSettlementRecord record,
            string key)
        {
            var circuit = new CAPatrolCircuit { key = key };
            try
            {
                // [axes] A settlement whose organization KEEPS a watch -
                // the security axis materialized named guards - posts at
                // least one node even below the fortification tier;
                // keeping order is what the practice is for.
                int guards = CASettlementSecurityAssignments
                    .LiveGuardIds(record, map).Count;
                if (guards == 0) return circuit;
                int want = Math.Min(3, Math.Max(1, (guards + 1) / 2));

                CellRect rect = record.localRect;
                IntVec3 center = rect.CenterCell;
                List<CADir> dirs = ApproachDirections(record, rect, key);
                // node rects must stand clear of the map rim
                var interior = new CellRect(3, 3, map.Size.x - 6,
                    map.Size.z - 6);
                int grows = 0;
                // cheap filters first, the reachability flood next, the
                // morphology growth last - and every stage bounded, the
                // FrontierModule site discipline.
                for (int i = 0; i < 24 && circuit.nodes.Count < want
                    && grows < want + 3; i++)
                {
                    CADir dir = dirs[i % dirs.Count];
                    int h = Pseudo(key, i);
                    int dist = 25 + h % 36;          // 25..60 out
                    int size = 18 + (h >> 6) % 9;    // 18..26 across
                    // step off the settlement rim in this direction,
                    // then out the full standoff
                    IntVec3 probe = new IntVec3(
                        center.x + (int)Math.Round(dir.x * map.Size.x),
                        0,
                        center.z + (int)Math.Round(dir.z * map.Size.z));
                    IntVec3 rim = rect.ClosestCellTo(probe);
                    var c = new IntVec3(
                        rim.x + (int)Math.Round(dir.x * dist), 0,
                        rim.z + (int)Math.Round(dir.z * dist));
                    if (!c.InBounds(map)) continue;
                    float standoff = rect.ClosestDistanceTo(c);
                    if (standoff < 25f || standoff > 60f) continue;
                    CellRect nodeRect = CellRect.CenteredOn(c, size, size);
                    if (!nodeRect.FullyContainedWithin(interior)) continue;
                    if (TouchesSettlement(nodeRect)) continue;
                    bool crowded = false;
                    for (int n = 0; n < circuits.Count && !crowded; n++)
                        for (int m = 0; m < circuits[n].nodes.Count; m++)
                            if (c.InHorDistOf(circuits[n].nodes[m], 20f))
                            { crowded = true; break; }
                    for (int m = 0; m < circuit.nodes.Count && !crowded;
                        m++)
                        if (c.InHorDistOf(circuit.nodes[m], 20f))
                            crowded = true;
                    if (crowded) continue;
                    if (!c.Standable(map) || c.Fogged(map)
                        || c.Roofed(map)) continue;
                    if (!map.reachability.CanReach(center, c,
                        PathEndMode.OnCell,
                        TraverseParms.For(TraverseMode.PassDoors)))
                        continue;
                    grows++;
                    int seed = Gen.HashCombineInt(map.uniqueID, c.x, c.z,
                        0);
                    if (CAMorphologyAdapter.Materialize(map, nodeRect,
                        CAMorphForm.Outpost, seed, record.faction, 1))
                        circuit.nodes.Add(c);
                }
                if (circuit.nodes.Count > 0)
                    Log.Message("[CA] patrol lane: " + (record.name ?? key)
                        + " posted " + circuit.nodes.Count
                        + " security node(s) over its approaches"
                        + " (" + guards + " assigned guard"
                        + (guards == 1 ? "" : "s")
                        + ")");
            }
            catch (Exception e)
            {
                Log.Warning("[CA] patrol node seeding failed for " + key
                    + ": " + e.Message);
            }
            return circuit;
        }

        private bool TouchesSettlement(CellRect nodeRect)
        {
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            if (world == null) return false;
            foreach (CARegionalSettlementRecord r in world.ForMap(map))
            {
                if (r == null || r.localRect == CellRect.Empty) continue;
                if (nodeRect.Overlaps(r.localRect.ExpandedBy(8)))
                    return true;
            }
            return false;
        }

        // Directions a patrol post makes sense in: where the adapter's
        // streets leave the settlement first (PackedDirt, flagstones,
        // PavedTile just outside the body read as the roads out), the
        // open compass after that, rotated per settlement so the whole
        // region does not post north.
        // Where a guard post is worth standing: over the ways people
        // actually come in by. The settlement's own description of
        // itself now names them, busiest first, so the first posts go
        // over the busiest approaches. What follows is what this did
        // before the graph could answer - a scan for made ground at the
        // rim, then the compass - kept because a place that has not
        // been described yet still needs somewhere to post a guard.
        private List<CADir> ApproachDirections(
            CARegionalSettlementRecord record, CellRect rect, string key)
        {
            var dirs = new List<CADir>();
            var buckets = new HashSet<int>();
            IntVec3 center = rect.CenterCell;

            CASettlementLayout layout = record?.layout;
            if (layout != null && layout.gates.Count > 0)
            {
                IntVec3 heart = layout.core.IsValid ? layout.core : center;
                foreach (IntVec3 gate in layout.gates)
                {
                    float gx = gate.x - heart.x, gz = gate.z - heart.z;
                    float glen = (float)Math.Sqrt(gx * gx + gz * gz);
                    if (glen < 1f) continue;
                    int b = (int)((Math.Atan2(gz, gx) + Math.PI * 2.0)
                        / (Math.PI / 4.0)) % 8;
                    if (!buckets.Add(b)) continue;
                    dirs.Add(new CADir(gx / glen, gz / glen));
                    if (dirs.Count >= 4) break;
                }
            }

            foreach (IntVec3 c in rect.ExpandedBy(2).EdgeCells)
            {
                if (dirs.Count >= 4) break;
                if (!c.InBounds(map)) continue;
                TerrainDef t = c.GetTerrain(map);
                if (t == null) continue;
                string dn = t.defName;
                if (dn != "PackedDirt" && dn != "PavedTile"
                    && !dn.StartsWith("Flagstone")) continue;
                float dx = c.x - center.x;
                float dz = c.z - center.z;
                float len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len < 1f) continue;
                int bucket = (int)((Math.Atan2(dz, dx) + Math.PI * 2.0)
                    / (Math.PI / 4.0)) % 8;
                if (!buckets.Add(bucket)) continue;
                dirs.Add(new CADir(dx / len, dz / len));
                if (dirs.Count >= 4) break;
            }
            const float d = 0.70710678f;
            CADir[] compass =
            {
                new CADir(0f, 1f), new CADir(d, d), new CADir(1f, 0f),
                new CADir(d, -d), new CADir(0f, -1f), new CADir(-d, -d),
                new CADir(-1f, 0f), new CADir(-d, d)
            };
            int start = Stagger(key) % 8;
            for (int i = 0; i < 8; i++)
                dirs.Add(compass[(start + i) % 8]);
            return dirs;
        }

        private static int Pseudo(string key, int salt)
        {
            return Gen.HashCombineInt(GenText.StableStringHash(key ?? ""),
                salt) & 0x7fffffff;
        }

        // ---- starting a walk: an armed, idle garrison resident ----
        private void TryStartPatrol(CARegionalSettlementRecord record,
            CAPatrolCircuit circuit, string key, int now)
        {
            Pawn walker = FindPatroller(record);
            if (walker == null)
            {
                circuit.nextStartTick = now + RetryDelay;
                return;
            }
            // [semantic layout] the patrol walks its OWN streets first
            // and last: 1-2 way-cells inside the settlement bracket the
            // outside circuit when the record carries a layout.
            var route = new List<IntVec3>();
            IntVec3 wayOut, wayBack;
            PickWayLegs(record, key, out wayOut, out wayBack);
            if (wayOut.IsValid) route.Add(wayOut);
            route.AddRange(circuit.nodes);
            if (wayBack.IsValid) route.Add(wayBack);
            route.Add(HomePost(walker, record));
            var assignment = new CAPatrolAssignment
            {
                key = key,
                pawn = walker,
                route = route,
                waypoint = 0,
                dwellUntilTick = -1,
                legDeadlineTick = now + LegDeadline
            };
            patrols.Add(assignment);
            AssignLeg(walker, route[0]);
        }

        // The works-pulse worker guards, then the duty-machinery ones:
        // the walk is driven through ThinkNode_Duty, which only runs
        // for a pawn under a lord whose current toil assigns duties -
        // and only a pawn standing garrison (DefendBase) is borrowed,
        // never one the works pulse already sent to build.
        // [axes] The organization's NAMED guards - the people its
        // security practice actually lists - are preferred over any
        // armed resident: the watch walks its own watch.
        private Pawn FindPatroller(CARegionalSettlementRecord record)
        {
            HashSet<int> named = CASettlementSecurityAssignments
                .LiveGuardIds(record, map);
            List<Pawn> pawns = CAPopulationProjection.Residents(record, map);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Downed || p.Dead || !p.Awake()
                    || p.IsPrisoner || !p.RaceProps.Humanlike) continue;
                if (p.InMentalState || p.InAggroMentalState) continue;
                if (map.attackTargetsCache
                        .TargetsHostileToFaction(record.faction)
                        .Any(t => t.Thing is Pawn tp && !tp.Downed
                            && tp.Position.InHorDistOf(p.Position, 40f)))
                    continue;
                if (!p.Position.InHorDistOf(
                    record.localRect.CenterCell, 90f)) continue;
                Job cur = p.CurJob;
                if (cur != null && cur.def != JobDefOf.Wait
                    && cur.def != JobDefOf.Wait_Wander
                    && cur.def != JobDefOf.GotoWander
                    && cur.def != JobDefOf.Goto) continue;
                Lord lord = p.GetLord();
                if (lord?.CurLordToil == null
                    || !lord.CurLordToil.AssignsDuties) continue;
                if (p.mindState?.duty?.def != DutyDefOf.DefendBase)
                    continue;
                if (PawnAssigned(p)) continue;
                if (named.Contains(p.thingIDNumber)
                    && p.equipment?.Primary != null) return p;
            }
            return null;
        }

        private IntVec3 HomePost(Pawn p,
            CARegionalSettlementRecord record)
        {
            var defend = p.GetLord()?.CurLordToil as LordToil_DefendBase;
            return defend != null ? defend.baseCenter
                : record.localRect.CenterCell;
        }

        // Way legs come from the settlement's semantic layout (the
        // machine-readable model derived from the real map): sampled
        // street cells inside the body. Deterministic per settlement,
        // guarded per cell - an unstandable way is skipped, and losing
        // both legs just means the old straight walk out.
        private void PickWayLegs(CARegionalSettlementRecord record,
            string key, out IntVec3 wayOut, out IntVec3 wayBack)
        {
            wayOut = IntVec3.Invalid;
            wayBack = IntVec3.Invalid;
            try
            {
                CASettlementLayout layout = record.layout;
                if (layout?.ways == null || layout.ways.Count == 0)
                    return;
                int h = Stagger(key);
                wayOut = ValidWay(layout.ways, h);
                wayBack = ValidWay(layout.ways, h / 7 + 3);
                if (wayBack == wayOut) wayBack = IntVec3.Invalid;
            }
            catch { }
        }

        private IntVec3 ValidWay(List<IntVec3> ways, int salt)
        {
            if (salt < 0) salt = -salt;
            for (int t = 0; t < 6 && t < ways.Count; t++)
            {
                IntVec3 c = ways[(salt + t * 13) % ways.Count];
                if (c.IsValid && c.InBounds(map) && c.Standable(map))
                    return c;
            }
            return IntVec3.Invalid;
        }

        // TravelOrWait is the vanilla walk-there-and-linger duty
        // (JobGiver_GotoTravelDestination on duty.focus: walks to
        // within six cells, wanders on arrival) - Core, no DLC gate,
        // unlike the Anomaly-gated Goto duty. The tag marks the duty
        // OURS, so a lord or works-pulse stomp is detectable and this
        // component backs off instead of fighting over the pawn.
        private static void AssignLeg(Pawn p, IntVec3 to)
        {
            p.mindState.duty =
                new PawnDuty(DutyDefOf.TravelOrWait, to)
                { tag = DutyTag };
            p.jobs?.CheckForJobOverride();
        }

        // Handing the pawn back: never null a lorded pawn's duty
        // (ThinkNode_Duty errors on a null duty), restore the garrison
        // post the lord toil would have assigned.
        private void ReleaseToGarrison(Pawn p)
        {
            try
            {
                Lord lord = p?.GetLord();
                if (lord == null || p.mindState == null) return;
                var defend = lord.CurLordToil as LordToil_DefendBase;
                if (defend != null)
                    p.mindState.duty = new PawnDuty(DutyDefOf.DefendBase,
                        defend.baseCenter);
                else if (lord.CurLordToil != null
                    && lord.CurLordToil.AssignsDuties)
                    lord.CurLordToil.UpdateAllDuties();
                p.jobs?.CheckForJobOverride();
            }
            catch { }
        }

        // ---- walks in flight: arrive, dwell, advance, come home ----
        private void StepPatrols(int now)
        {
            for (int i = patrols.Count - 1; i >= 0; i--)
            {
                CAPatrolAssignment a = patrols[i];
                CAPatrolCircuit circuit = CircuitFor(a.key);
                Pawn p = a.pawn;
                if (p == null || p.Dead || !p.Spawned || p.Map != map
                    || p.Downed || p.InMentalState || p.Faction == null
                    || a.waypoint >= a.route.Count)
                {
                    if (p != null && !p.Dead && p.Spawned
                        && p.Map == map && !p.Downed)
                        ReleaseToGarrison(p);
                    Drop(i, circuit, now + RetryDelay);
                    continue;
                }
                // duty stomped (lord toil turned over, works pulse took
                // the pawn): the other machinery won - back off clean.
                PawnDuty duty = p.mindState?.duty;
                if (duty == null || duty.def != DutyDefOf.TravelOrWait
                    || duty.tag != DutyTag)
                {
                    Drop(i, circuit, now + RetryDelay);
                    continue;
                }
                // hostiles near the walker: CONTACT - what happens
                // next is the settlement's posture, not a constant
                if (map.attackTargetsCache
                        .TargetsHostileToFaction(p.Faction)
                        .Any(t => t.Thing is Pawn tp && !tp.Downed
                            && tp.Position.InHorDistOf(p.Position, 40f)))
                {
                    OnContact(i, a, circuit, p, now);
                    continue;
                }
                IntVec3 wp = a.route[a.waypoint];
                bool last = a.waypoint == a.route.Count - 1;
                if (a.dwellUntilTick > 0)
                {
                    if (now >= a.dwellUntilTick) Advance(a, now);
                }
                else if (p.Position.InHorDistOf(wp, ArriveRadius))
                {
                    if (last)
                    {
                        Complete(i, a, circuit, now);
                        continue;
                    }
                    // only the security nodes are POSTS - way cells
                    // inside the settlement are the walk, not the watch
                    if (circuit != null && circuit.nodes.Contains(wp))
                        a.postsChecked++;
                    a.dwellUntilTick = now + DwellTicks;
                }
                else if (now > a.legDeadlineTick)
                {
                    // leg went stale (path closed, door locked): skip
                    // ahead; a stale HOME leg ends the walk unrecorded
                    if (last)
                    {
                        ReleaseToGarrison(p);
                        Drop(i, circuit, now + RetryDelay);
                        continue;
                    }
                    Advance(a, now);
                }
            }
        }

        // Contact doctrine comes from the organization's explicit security
        // policy. With no recorded doctrine, the patrol reports the contact
        // and native garrison behavior remains in charge.
        private void OnContact(int index, CAPatrolAssignment a,
            CAPatrolCircuit circuit, Pawn p, int now)
        {
            CAOrganization org =
                CAOrganizationWorldComponent.Current?.ByKey(a.key);
            string post = PostLabel(a, circuit);
            if (MilitantPosture(org, a.key))
            {
                // MILITANT: hold the ground the patrol stands on.
                // DefendBase focused HERE, not home - the duty's
                // JobGiver_AIDefendPoint engages hostiles within 25 of
                // its focus, so this IS the order to fight in place.
                try
                {
                    p.mindState.duty = new PawnDuty(
                        DutyDefOf.DefendBase, p.Position);
                    p.jobs?.CheckForJobOverride();
                }
                catch { }
                org?.Record("security", "patrol engaged hostiles at "
                    + post);
            }
            else
            {
                // CAUTIOUS: fall back to the garrison AI and carry the
                // word home. The sighting itself already lives in the
                // knowledge layer (firsthand observation is faction-
                // blind there), so the org ledger plus the garrison
                // standing to IS the report landing - the same
                // stand-to a delivered warning fires, without the
                // player-sourced records that surface would fabricate.
                ReleaseToGarrison(p);
                org?.Record("security", "patrol sighted hostiles near "
                    + post + " - fell back and reported");
                StandGarrisonTo(p.Faction, org);
            }
            Drop(index, circuit, now + RetryDelay);
        }

        private bool MilitantPosture(CAOrganization org, string key)
        {
            if (org == null) return false;
            // [axes] The security axis IS the doctrine where one was
            // decided: a professionalized force (constabulary, ruler's
            // guard) meets contact like soldiers; a civic watch falls
            // back and reports - watching is its office, fighting is
            // the garrison's.
            CAPolicyRecord doctrine = org.policies?.FirstOrDefault(p =>
                p != null && p.key == "security");
            if (doctrine != null && !doctrine.value.NullOrEmpty())
                return doctrine.value == "constabulary"
                    || doctrine.value == "rulers";
            // Without explicit doctrine CA issues no order to stand and fight.
            // The patrol reports and native garrison behavior remains in charge.
            return false;
        }

        private CARegionalSettlementRecord RecordFor(string key)
        {
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            if (world == null) return null;
            foreach (CARegionalSettlementRecord r in world.ForMap(map))
                if (r != null && r.regionalId + "#" + r.slot == key)
                    return r;
            return null;
        }

        private static string PostLabel(CAPatrolAssignment a,
            CAPatrolCircuit circuit)
        {
            IntVec3 wp = a.waypoint >= 0 && a.waypoint < a.route.Count
                ? a.route[a.waypoint] : IntVec3.Invalid;
            if (circuit != null && wp.IsValid)
            {
                int n = circuit.nodes.IndexOf(wp);
                if (n >= 0)
                    return "post " + (n + 1) + " of "
                        + circuit.nodes.Count;
            }
            if (a.waypoint == a.route.Count - 1) return "the walk home";
            return "the settlement ways";
        }

        // The garrison reacts to its own patrol's report the way it
        // reacts to a delivered warning: sleepers wake, posts are
        // re-anchored, and the defence axis musters who else rises -
        // all of it living on StandTo itself
        // (LordToil_CAOrganizationDefense.StandTo -> CADefenceMuster),
        // so every escalation path behaves the same.
        private void StandGarrisonTo(Faction faction, CAOrganization org)
        {
            try
            {
                if (faction == null) return;
                List<Lord> lords = map.lordManager.lords;
                for (int l = 0; l < lords.Count; l++)
                {
                    if (lords[l].faction != faction) continue;
                    var toil = lords[l].CurLordToil
                        as LordToil_CAOrganizationDefense;
                    if (toil == null) continue;
                    toil.StandTo();
                    org?.Record("security",
                        "the garrison stood to at the patrol's report");
                    break;
                }
            }
            catch { }
        }

        private void Advance(CAPatrolAssignment a, int now)
        {
            a.waypoint++;
            a.dwellUntilTick = -1;
            a.legDeadlineTick = now + LegDeadline;
            if (a.waypoint < a.route.Count)
                AssignLeg(a.pawn, a.route[a.waypoint]);
        }

        private void Complete(int index, CAPatrolAssignment a,
            CAPatrolCircuit circuit, int now)
        {
            // a walk that reached no post was not a circuit - release
            // and reschedule without claiming one in the ledger
            if (a.postsChecked > 0)
            {
                CAOrganization org =
                    CAOrganizationWorldComponent.Current?.ByKey(a.key);
                org?.Record("security", "patrol circuit walked - "
                    + a.postsChecked + " post"
                    + (a.postsChecked == 1 ? "" : "s") + " checked");
            }
            ReleaseToGarrison(a.pawn);
            Drop(index, circuit,
                now + PatrolPeriod + Stagger(a.key) % 7500);
        }

        private void Drop(int index, CAPatrolCircuit circuit,
            int nextStart)
        {
            if (circuit != null) circuit.nextStartTick = nextStart;
            patrols.RemoveAt(index);
        }
    }

    internal readonly struct CADir
    {
        internal readonly float x;
        internal readonly float z;

        internal CADir(float x, float z)
        {
            this.x = x;
            this.z = z;
        }
    }
}
