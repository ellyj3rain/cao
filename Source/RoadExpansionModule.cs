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
        public string behaviorKey;
        public int episodeId;
        public int authorityOrigin;
        public string authorityIdentity;
        public string owner;
        public int createdTick;
        public string terminationCondition;
        public int laborPawnId = -1;
        public int laborJobId = -1;
        public int laborAuthorityOrigin;
        public string laborTerrainDefName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ownerKey, "ownerKey");
            Scribe_Values.Look(ref from, "from", IntVec3.Invalid);
            Scribe_Values.Look(ref to, "to", IntVec3.Invalid);
            Scribe_Collections.Look(ref pending, "pending",
                LookMode.Value);
            Scribe_Values.Look(ref laid, "laid", 0);
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref behaviorKey, "behaviorKey");
            Scribe_Values.Look(ref episodeId, "episodeId", 0);
            Scribe_Values.Look(ref authorityOrigin, "authorityOrigin", 0);
            Scribe_Values.Look(ref authorityIdentity, "authorityIdentity");
            Scribe_Values.Look(ref owner, "owner");
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
            Scribe_Values.Look(ref terminationCondition,
                "terminationCondition");
            Scribe_Values.Look(ref laborPawnId, "laborPawnId", -1);
            Scribe_Values.Look(ref laborJobId, "laborJobId", -1);
            Scribe_Values.Look(ref laborAuthorityOrigin,
                "laborAuthorityOrigin", 0);
            Scribe_Values.Look(ref laborTerrainDefName,
                "laborTerrainDefName");
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && pending == null) pending = new List<IntVec3>();
        }
    }

    public class CARoadExpansionMapComponent : MapComponent
    {
        private List<CARoadProject> projects = new List<CARoadProject>();
        private int nextTick;
        private bool needsRestoreValidation;

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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (projects == null) projects = new List<CARoadProject>();
                needsRestoreValidation = true;
            }
        }

        public override void MapComponentTick()
        {
            if (needsRestoreValidation)
            {
                needsRestoreValidation = false;
                RevalidateRestoredProjects();
            }
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
                    || record.localRect == CellRect.Empty
                    || !record.developmentExecutable) continue;
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
                else if (!ProjectAuthorityValid(record, project))
                {
                    CancelLabor(project);
                    projects.Remove(project);
                    continue;
                }
                Advance(record, org, project);
            }
            projects.RemoveAll(p => p == null
                || (p.pending != null && p.pending.Count == 0
                    && p.laid > 0));
        }

        private void RevalidateRestoredProjects()
        {
            for (int i = projects.Count - 1; i >= 0; i--)
            {
                CARoadProject project = projects[i];
                CARegionalSettlementRecord record = RecordFor(
                    project?.ownerKey);
                if (project == null || !ProjectAuthorityValid(record, project))
                {
                    CancelLabor(project);
                    projects.RemoveAt(i);
                    continue;
                }
                if (project.laborJobId <= 0) continue;
                Pawn worker = PawnFor(project.laborPawnId);
                Job job = worker?.CurJob;
                CABehaviorDecision decision;
                bool validLabor = job != null
                    && job.loadID == project.laborJobId
                    && CASettlementInstitutionalAuthorization
                        .TryReauthorizeCompletion(record, map, worker, job,
                            project.behaviorKey, project.episodeId,
                            (CAAuthorityOrigin)project.laborAuthorityOrigin,
                            project.authorityIdentity, project.owner,
                            "lay road from " + project.from + " toward "
                                + project.to, out decision);
                if (!validLabor) CancelLabor(project);
            }
        }

        private bool ProjectAuthorityValid(CARegionalSettlementRecord record,
            CARoadProject project)
        {
            if (project == null) return false;
            CABehaviorDecision decision;
            return CASettlementInstitutionalAuthorization
                .TryReauthorizeCompletion(record, map, null, null,
                    project.behaviorKey, project.episodeId,
                    (CAAuthorityOrigin)project.authorityOrigin,
                    project.authorityIdentity, project.owner,
                    project.reason, out decision);
        }

        private void CancelLabor(CARoadProject project)
        {
            if (project == null) return;
            int pawnId = project.laborPawnId;
            int jobId = project.laborJobId;
            project.laborPawnId = -1;
            project.laborJobId = -1;
            project.laborAuthorityOrigin = 0;
            project.laborTerrainDefName = null;
            Pawn worker = PawnFor(pawnId);
            Job job = worker?.CurJob;
            if (job == null || job.loadID != jobId) return;
            CABehaviorIntentMapComponent.For(map)?.Unregister(worker, job);
            if (!job.playerForced)
                worker.jobs.EndCurrentJob(JobCondition.Incompletable);
        }

        // Culture ranks two already valid institutional projects: a way to a
        // watch post or a road toward an agreement partner. It does not create
        // either target, funding, labor, authority, or a passable route.
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
            if (!post.IsValid || post.DistanceTo(start) <= 12f)
                post = IntVec3.Invalid;

            IntVec3 agreementTarget = IntVec3.Invalid;
            string agreementWhy = null;
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
                float d = other.localRect.CenterCell.DistanceTo(start);
                if (d < best && d > 15f)
                {
                    best = d;
                    agreementTarget = other.localRect.CenterCell;
                    agreementWhy = "a road toward " + other.name
                        + ", who they are bound to";
                }
            }

            CACulturalMeaningResolution defense = CACultureModel.Resolve(
                record.culture, CASocialSubjectRegistry.DefendedBoundary);
            CACulturalMeaningResolution exchange = CACultureModel.Resolve(
                record.culture, CASocialSubjectRegistry.OutsiderContact);
            int exchangeRank = exchange.Approval + exchange.Prestige
                + exchange.Salience;
            int defenseRank = defense.Approval + defense.Prestige
                + defense.Salience;
            if (agreementTarget.IsValid && (!post.IsValid
                    || exchangeRank > defenseRank))
            {
                target = agreementTarget;
                why = agreementWhy + "; retained exchange practice "
                    + exchangeRank;
            }
            else if (post.IsValid)
            {
                target = post;
                why = "a way out to their own watch post; retained defensive "
                    + "meaning " + defenseRank;
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
            CAAuthorityOrigin institutionalOrigin =
                (CAAuthorityOrigin)record.developmentAuthorityOrigin;
            var proposalContext = new CABehaviorContext(null,
                CAActorContext.NPCSettlement | CAActorContext.NPCInstitution,
                CAInitiativeTier.Standard, institutionalOrigin,
                authoritySatisfied: record.developmentAuthorized
                    && record.developmentEpisodeId > 0,
                knowledgeSatisfied: true, knowledgeFresh: true,
                liveValidated: true, knowledgeRelayed: false,
                knowledgeAgeTicks: 0, knowledgeConfidence: 1f,
                knowledgeUncertainty: 0f, capabilitySatisfied: true,
                materialSatisfied: org.treasury
                    >= SilverPerSegment * 12,
                currentIntentCompatible: true,
                directPlayerOwnership: false,
                authorityBasis: record.developmentAuthorityIdentity,
                knowledgeBasis: why + "; route has " + line.Count
                    + " native terrain cells",
                owner: record.developmentOwner);
            CABehaviorDecision projectDecision = CABehaviorGate.Evaluate(
                "spatial.npc_settlement_development", proposalContext);
            if (!projectDecision.Allowed) return null;
            return new CARoadProject
            {
                ownerKey = key, from = start, to = target,
                pending = line, reason = why,
                behaviorKey = record.developmentBehaviorKey,
                episodeId = record.developmentEpisodeId,
                authorityOrigin = record.developmentAuthorityOrigin,
                authorityIdentity = record.developmentAuthorityIdentity,
                owner = record.developmentOwner,
                createdTick = record.developmentCreatedTick,
                terminationCondition = CABehaviorCatalog.Get(
                    "spatial.npc_settlement_development")
                    ?.CompletionCondition
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
            if (project.laborJobId > 0) return;
            if (org.treasury < SilverPerSegment) return;
            IntVec3 head = project.pending[0];
            if (!head.InBounds(map)) { project.pending.RemoveAt(0); return; }

            Pawn worker = FindWorker(record, head);
            if (worker == null) return;
            if (!worker.Position.InHorDistOf(head, 4f))
            {
                if (worker.CanReach(head, PathEndMode.OnCell,
                    Danger.Some))
                {
                    Job approach = JobMaker.MakeJob(JobDefOf.Goto, head);
                    if (CASettlementInstitutionalAuthorization.TryAuthorizeJob(
                            record, worker, approach,
                            "road approach " + head,
                            out CABehaviorDecision _, out CAIntentContext _))
                        worker.jobs.StartJob(approach,
                            JobCondition.InterruptForced);
                }
                return;
            }

            int tier = CASettlementProgramMaterializer.TechTier(record);
            string terrainName = tier >= 2 ? "PavedTile"
                : tier == 1 ? "PackedDirt" : "PackedDirt";
            TerrainDef road = DefDatabase<TerrainDef>
                .GetNamedSilentFail(terrainName);
            if (road == null) return;
            Job roadWork = JobMaker.MakeJob(JobDefOf.Wait, head);
            roadWork.expiryInterval = 300;
            if (!CASettlementInstitutionalAuthorization.TryAuthorizeJob(record,
                    worker, roadWork, "lay road from " + project.from
                        + " toward " + project.to,
                    out CABehaviorDecision _,
                    out CAIntentContext laborIntent)) return;
            worker.jobs.StartJob(roadWork, JobCondition.InterruptForced);
            if (worker.CurJob != roadWork)
            {
                CABehaviorIntentMapComponent.For(map)?.Unregister(worker,
                    roadWork);
                return;
            }
            project.laborPawnId = worker.thingIDNumber;
            project.laborJobId = roadWork.loadID;
            project.laborAuthorityOrigin = (int)laborIntent.AuthorityOrigin;
            project.laborTerrainDefName = road.defName;
        }

        // Called from the native Pawn_JobTracker completion seam. Terrain and
        // treasury remain unchanged unless the exact CA-owned labor job reports
        // Succeeded.
        internal void CompleteNativeLabor(Pawn worker, Job job,
            JobCondition condition)
        {
            if (worker == null || job == null) return;
            CARoadProject project = projects.FirstOrDefault(candidate =>
                candidate != null
                && candidate.laborPawnId == worker.thingIDNumber
                && candidate.laborJobId == job.loadID);
            if (project == null) return;
            CARegionalSettlementRecord record = RecordFor(project.ownerKey);
            CABehaviorDecision completionDecision;
            bool authorized = condition == JobCondition.Succeeded
                && CASettlementInstitutionalAuthorization
                    .TryReauthorizeCompletion(record, map, worker, job,
                        project.behaviorKey, project.episodeId,
                        (CAAuthorityOrigin)project.laborAuthorityOrigin,
                        project.authorityIdentity, project.owner,
                        "lay road from " + project.from + " toward "
                            + project.to, out completionDecision);
            CABehaviorIntentMapComponent.For(map)?.Unregister(worker, job);
            project.laborPawnId = -1;
            project.laborJobId = -1;
            project.laborAuthorityOrigin = 0;
            string terrainName = project.laborTerrainDefName;
            project.laborTerrainDefName = null;
            if (!authorized) return;
            CAOrganization org = CAOrganizationWorldComponent.Current
                ?.ByKey(project.ownerKey);
            TerrainDef road = DefDatabase<TerrainDef>
                .GetNamedSilentFail(terrainName);
            if (org == null || road == null) return;
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

        private CARegionalSettlementRecord RecordFor(string settlementKey)
        {
            return CARegionalWorldComponent.Current?.ForMap(map)
                .FirstOrDefault(record => record != null
                    && record.regionalId + "#" + record.slot
                        == settlementKey);
        }

        private Pawn PawnFor(int pawnId)
        {
            return map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(pawn => pawn.thingIDNumber == pawnId);
        }
    }
}
