using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Settlements build roads toward their security posts and agreement
    // partners. Construction spends treasury funds and assigned labor.
    public sealed class CARoadProject : IExposable
    {
        public string ownerKey;
        public IntVec3 from = IntVec3.Invalid;
        public IntVec3 to = IntVec3.Invalid;
        public List<IntVec3> pending = new List<IntVec3>();
        public int laid;
        public string reason;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ownerKey, "ownerKey");
            Scribe_Values.Look(ref from, "from", IntVec3.Invalid);
            Scribe_Values.Look(ref to, "to", IntVec3.Invalid);
            Scribe_Collections.Look(ref pending, "pending",
                LookMode.Value);
            Scribe_Values.Look(ref laid, "laid", 0);
            Scribe_Values.Look(ref reason, "reason");
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && pending == null) pending = new List<IntVec3>();
        }
    }

    public class CARoadExpansionMapComponent : MapComponent
    {
        private List<CARoadProject> projects = new List<CARoadProject>();
        private int nextTick;

        // A segment of road costs the settlement real money and is
        // laid by a real pawn standing on it.
        private const int SilverPerSegment = 4;
        private const int SegmentsPerVisit = 3;

        public CARoadExpansionMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref projects, "CA_roadProjects",
                LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && projects == null)
                projects = new List<CARoadProject>();
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame < nextTick) return;
            nextTick = Find.TickManager.TicksGame + 3000
                + (map.uniqueID * 137) % 900;
            try { Pulse(); }
            catch (Exception e)
            {
                Log.Warning("[CA] road pulse failed: " + e.Message);
            }
        }

        private void Pulse()
        {
            var world = CARegionalWorldComponent.Current;
            var comp = CAOrganizationWorldComponent.Current;
            if (world == null || comp == null) return;

            foreach (CARegionalSettlementRecord record in
                world.ForMap(map))
            {
                if (record?.faction == null || record.faction.IsPlayer
                    || record.localRect == CellRect.Empty) continue;
                string key = record.regionalId + "#" + record.slot;
                CAOrganization org = comp.ByKey(key);
                if (org == null) continue;

                CARoadProject project = projects
                    .FirstOrDefault(p => p != null && p.ownerKey == key);
                if (project == null)
                {
                    project = Propose(record, org, key, world);
                    if (project == null) continue;
                    projects.Add(project);
                    org.Record("works", "road ordered - " + project.reason);
                }
                Advance(record, org, project);
            }
            projects.RemoveAll(p => p == null
                || (p.pending != null && p.pending.Count == 0
                    && p.laid > 0));
        }

        // What a settlement wants to reach: first its own security
        // posts, then the nearest settlement with an active agreement.
        private CARoadProject Propose(CARegionalSettlementRecord record,
            CAOrganization org, string key,
            CARegionalWorldComponent world)
        {
            if (org.treasury < SilverPerSegment * 12) return null;
            IntVec3 start = record.layout?.gates != null
                && record.layout.gates.Count > 0
                ? record.layout.gates[0]
                : record.localRect.CenterCell;
            if (!start.InBounds(map)) return null;

            IntVec3 target = IntVec3.Invalid;
            string why = null;

            var patrols = map.GetComponent<CAPatrolSystemMapComponent>();
            IntVec3 post = patrols != null
                ? patrols.NearestNodeFor(key, start) : IntVec3.Invalid;
            if (post.IsValid && post.DistanceTo(start) > 12f)
            {
                target = post;
                why = "a way out to their own watch post";
            }
            else
            {
                float best = float.MaxValue;
                foreach (CARegionalSettlementRecord other in
                    world.ForMap(map))
                {
                    if (other == record || other?.faction == null
                        || other.localRect == CellRect.Empty) continue;
                    string otherKey = other.regionalId + "#" + other.slot;
                    var wc = CAOrganizationWorldComponent.Current;
                    if (wc == null || !wc.HasAnyActiveAgreement(key,
                        otherKey)) continue;
                    float d = other.localRect.CenterCell
                        .DistanceTo(start);
                    if (d < best && d > 15f)
                    {
                        best = d;
                        target = other.localRect.CenterCell;
                        why = "a road toward " + other.name
                            + ", who they are bound to";
                    }
                }
            }
            if (!target.IsValid) return null;

            var line = new List<IntVec3>();
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(start,
                target))
            {
                if (!c.InBounds(map)) break;
                if (record.localRect.Contains(c)) continue;
                TerrainDef t = c.GetTerrain(map);
                if (t == null || t.IsWater) continue;
                if (c.GetEdifice(map) != null) continue;
                line.Add(c);
                if (line.Count >= 120) break;
            }
            if (line.Count < 8) return null;
            return new CARoadProject
            {
                ownerKey = key, from = start, to = target,
                pending = line, reason = why
            };
        }

        // The work itself: a settlement pawn walks to the head of the
        // road and the next few cells become road, paid for out of the
        // treasury. No worker, no money - no road.
        private void Advance(CARegionalSettlementRecord record,
            CAOrganization org, CARoadProject project)
        {
            if (project.pending == null || project.pending.Count == 0)
                return;
            if (org.treasury < SilverPerSegment) return;
            IntVec3 head = project.pending[0];
            if (!head.InBounds(map)) { project.pending.RemoveAt(0); return; }

            Pawn worker = FindWorker(record, head);
            if (worker == null) return;
            if (!worker.Position.InHorDistOf(head, 4f))
            {
                if (worker.CanReach(head, PathEndMode.OnCell,
                    Danger.Some))
                    worker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto,
                        head), JobCondition.InterruptForced);
                return;
            }

            int tier = CAStartingFacilities.TechTier(record);
            string terrainName = tier >= 2 ? "PavedTile"
                : tier == 1 ? "PackedDirt" : "PackedDirt";
            TerrainDef road = DefDatabase<TerrainDef>
                .GetNamedSilentFail(terrainName);
            if (road == null) return;

            int done = 0;
            while (done < SegmentsPerVisit && project.pending.Count > 0
                && org.treasury >= SilverPerSegment)
            {
                IntVec3 c = project.pending[0];
                project.pending.RemoveAt(0);
                if (!c.InBounds(map)) continue;
                TerrainDef existing = c.GetTerrain(map);
                if (existing == road || existing.IsWater) continue;
                try
                {
                    map.terrainGrid.SetTerrain(c, road);
                    // the ground changed: whoever reads this place
                    // should see the new way
                    map.GetComponent<CASettlementGraphMapComponent>()
                        ?.MarkDirtyAt(c);
                    org.treasury -= SilverPerSegment;
                    project.laid++;
                    done++;
                }
                catch { }
            }
            if (project.pending.Count == 0 && project.laid > 0)
                org.Record("works", "road finished - " + project.laid
                    + " segments laid, " + project.reason);
        }

        private Pawn FindWorker(CARegionalSettlementRecord record,
            IntVec3 head)
        {
            var pawns = map.mapPawns
                .SpawnedPawnsInFaction(record.faction);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead || p.Downed || !p.Awake()
                    || p.IsPrisoner || !p.RaceProps.Humanlike) continue;
                if (p.InMentalState) continue;
                if (map.attackTargetsCache
                        .TargetsHostileToFaction(record.faction)
                        .Any(t => t.Thing is Pawn tp && !tp.Downed
                            && tp.Position.InHorDistOf(p.Position, 40f)))
                    continue;
                Job cur = p.CurJob;
                if (cur != null && cur.def != JobDefOf.Wait
                    && cur.def != JobDefOf.Wait_Wander
                    && cur.def != JobDefOf.GotoWander
                    && cur.def != JobDefOf.Goto) continue;
                if (!p.Position.InHorDistOf(head, 120f)) continue;
                return p;
            }
            return null;
        }
    }
}
