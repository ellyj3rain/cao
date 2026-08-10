using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Module 7: squads. A squad is a named group (fire team = small squad, same primitive)
    // with a leader. Up to 4 squads, ~7 members each by doctrine. Raid response anchors
    // squad fighters on their leader.
    public class SquadComponent : GameComponent
    {
        private Dictionary<int, int> squadOf = new Dictionary<int, int>();   // pawnId -> squad 1..4
        private Dictionary<int, int> leaderOf = new Dictionary<int, int>();  // squad -> pawnId
        private Dictionary<int, int> fireteamOf = new Dictionary<int, int>(); // pawnId -> 0 none, 1 A, 2 B
        private Dictionary<int, int> ftLeaderOf = new Dictionary<int, int>(); // squad*10+team -> pawnId
        private Dictionary<int, int> propagatedDraftLeader =
            new Dictionary<int, int>(); // memberId -> leaderId
        private Dictionary<int, int> propagatedDraftEpisode =
            new Dictionary<int, int>(); // memberId -> causal episode
        public static SquadComponent Instance;

        public SquadComponent(Game game) { Instance = this; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref squadOf, "CA_squadOf", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref leaderOf, "CA_leaderOf", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref fireteamOf, "CA_fireteamOf", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref ftLeaderOf, "CA_ftLeaderOf", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref propagatedDraftLeader,
                "CA_propagatedDraftLeader", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref propagatedDraftEpisode,
                "CA_propagatedDraftEpisode", LookMode.Value, LookMode.Value);
            if (squadOf == null) squadOf = new Dictionary<int, int>();
            if (leaderOf == null) leaderOf = new Dictionary<int, int>();
            if (fireteamOf == null) fireteamOf = new Dictionary<int, int>();
            if (ftLeaderOf == null) ftLeaderOf = new Dictionary<int, int>();
            if (propagatedDraftLeader == null)
                propagatedDraftLeader = new Dictionary<int, int>();
            if (propagatedDraftEpisode == null)
                propagatedDraftEpisode = new Dictionary<int, int>();
            Instance = this;
            CommsModule.InvalidateTopologyCache();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (int episode in propagatedDraftEpisode.Values)
                CACombatIntent.ObserveEpisode(episode);
        }

        public static int SquadOf(Pawn p)
        {
            var inst = Instance;
            if (p == null || inst == null) return 0;
            int s;
            return inst.squadOf.TryGetValue(p.thingIDNumber, out s) ? s : 0;
        }

        public static void Assign(Pawn p, int squad)
        {
            if (p == null || Instance == null) return;
            Instance.squadOf[p.thingIDNumber] = squad;
            if (!Instance.leaderOf.ContainsKey(squad)) Instance.leaderOf[squad] = p.thingIDNumber;
            CommsModule.InvalidateTopologyCache();
        }

        public static void Leave(Pawn p)
        {
            if (p == null || Instance == null) return;
            int s = SquadOf(p);
            Instance.squadOf.Remove(p.thingIDNumber);
            int lid;
            if (s > 0 && Instance.leaderOf.TryGetValue(s, out lid) && lid == p.thingIDNumber)
                Instance.leaderOf.Remove(s);
            CommsModule.InvalidateTopologyCache();
        }

        public static void SetLeader(Pawn p)
        {
            if (p == null || Instance == null) return;
            int s = SquadOf(p);
            if (s > 0)
            {
                Instance.leaderOf[s] = p.thingIDNumber;
                CommsModule.InvalidateTopologyCache();
            }
        }

        public static bool IsLeader(Pawn p)
        {
            int s = SquadOf(p);
            if (s == 0 || Instance == null) return false;
            int lid;
            return Instance.leaderOf.TryGetValue(s, out lid) && lid == p.thingIDNumber;
        }

        // Stored responsibility, not LeaderPawn's competence fallback. These are
        // the only edges that may create an accountability expectation.
        public static bool HasExplicitResponsibilityFor(Pawn owner, int subjectId)
        {
            var inst = Instance;
            if (owner == null || inst == null || subjectId == owner.thingIDNumber)
                return false;
            int squad = SquadOf(owner);
            int subjectSquad;
            if (squad == 0 || !inst.squadOf.TryGetValue(subjectId,
                out subjectSquad) || subjectSquad != squad) return false;

            int squadLeaderId;
            bool haveSquadLeader = inst.leaderOf.TryGetValue(squad,
                out squadLeaderId);
            if (haveSquadLeader && squadLeaderId == owner.thingIDNumber)
            {
                int subjectTeam;
                if (!inst.fireteamOf.TryGetValue(subjectId, out subjectTeam)
                    || subjectTeam == 0) return true;
                int teamLeaderId;
                return inst.ftLeaderOf.TryGetValue(squad * 10 + subjectTeam,
                    out teamLeaderId) && teamLeaderId == subjectId;
            }
            if (haveSquadLeader && squadLeaderId == subjectId) return false;

            int ownerTeam;
            if (!inst.fireteamOf.TryGetValue(owner.thingIDNumber, out ownerTeam)
                || ownerTeam == 0) return false;
            int ownerTeamLeaderId;
            if (!inst.ftLeaderOf.TryGetValue(squad * 10 + ownerTeam,
                out ownerTeamLeaderId)
                || ownerTeamLeaderId != owner.thingIDNumber) return false;
            int memberTeam;
            return inst.fireteamOf.TryGetValue(subjectId, out memberTeam)
                && memberTeam == ownerTeam;
        }

        // Stored command authority may cross one explicitly assigned fire-team
        // edge. Accountability remains direct-report-only above: the squad leader
        // commands the whole assigned squad, while a fire-team leader commands the
        // members explicitly assigned to that fire team.
        public static bool HasExplicitCommandAuthorityOver(Pawn commander,
            int subjectId)
        {
            var inst = Instance;
            if (commander == null || inst == null
                || subjectId == commander.thingIDNumber) return false;
            int squad = SquadOf(commander);
            int subjectSquad;
            if (squad == 0 || !inst.squadOf.TryGetValue(subjectId,
                out subjectSquad) || subjectSquad != squad) return false;

            int squadLeaderId;
            bool haveSquadLeader = inst.leaderOf.TryGetValue(squad,
                out squadLeaderId);
            if (haveSquadLeader && squadLeaderId == commander.thingIDNumber)
                return true;
            if (haveSquadLeader && squadLeaderId == subjectId) return false;

            int team;
            if (!inst.fireteamOf.TryGetValue(commander.thingIDNumber, out team)
                || team == 0) return false;
            int teamLeaderId;
            if (!inst.ftLeaderOf.TryGetValue(squad * 10 + team,
                out teamLeaderId) || teamLeaderId != commander.thingIDNumber)
                return false;
            int subjectTeam;
            return inst.fireteamOf.TryGetValue(subjectId, out subjectTeam)
                && subjectTeam == team;
        }

        public static void CopyExplicitDirectReportIds(Pawn owner,
            List<int> result)
        {
            result.Clear();
            var inst = Instance;
            if (owner == null || inst == null) return;
            foreach (KeyValuePair<int, int> assignment in inst.squadOf)
                if (HasExplicitResponsibilityFor(owner, assignment.Key))
                    result.Add(assignment.Key);
            result.Sort();
        }

        // Leader pawn for a squad; falls back to the best member (Shooting + Intellectual).
        public static Pawn LeaderPawn(int squad, Map map)
        {
            if (squad == 0 || Instance == null || map == null) return null;
            int lid;
            Pawn best = null;
            float bestScore = -1f;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            bool haveLid = Instance.leaderOf.TryGetValue(squad, out lid);
            for (int i = 0; i < colonists.Count; i++)
            {
                var p = colonists[i];
                if (SquadOf(p) != squad || p.Dead || p.Downed) continue;
                if (haveLid && p.thingIDNumber == lid) return p;
                float score = 0f;
                if (p.skills != null)
                    score = p.skills.GetSkill(SkillDefOf.Shooting).Level + p.skills.GetSkill(SkillDefOf.Intellectual).Level;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public static int FireteamOf(Pawn p)
        {
            var inst = Instance;
            if (p == null || inst == null) return 0;
            int t;
            return inst.fireteamOf.TryGetValue(p.thingIDNumber, out t) ? t : 0;
        }

        public static void SetFireteam(Pawn p, int team)
        {
            if (p == null || Instance == null) return;
            Instance.fireteamOf[p.thingIDNumber] = team;
            if (team > 0)
            {
                int key = SquadOf(p) * 10 + team;
                if (!Instance.ftLeaderOf.ContainsKey(key)) Instance.ftLeaderOf[key] = p.thingIDNumber;
            }
            CommsModule.InvalidateTopologyCache();
        }

        public static void SetFireteamLeader(Pawn p)
        {
            if (p == null || Instance == null) return;
            int sq = SquadOf(p);
            int t = FireteamOf(p);
            if (sq > 0 && t > 0)
            {
                Instance.ftLeaderOf[sq * 10 + t] = p.thingIDNumber;
                CommsModule.InvalidateTopologyCache();
            }
        }

        public static bool IsFireteamLeader(Pawn p)
        {
            var inst = Instance;
            if (p == null || inst == null) return false;
            int sq = SquadOf(p);
            int t = FireteamOf(p);
            if (sq == 0 || t == 0) return false;
            int lid;
            return inst.ftLeaderOf.TryGetValue(sq * 10 + t, out lid) && lid == p.thingIDNumber;
        }

        public static Pawn FireteamLeadPawn(int squad, int team, Map map)
        {
            var inst = Instance;
            if (inst == null || squad == 0 || team == 0 || map == null) return null;
            int lid;
            bool have = inst.ftLeaderOf.TryGetValue(squad * 10 + team, out lid);
            Pawn best = null;
            float bestScore = -1f;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var p = colonists[i];
                if (SquadOf(p) != squad || FireteamOf(p) != team || p.Dead || p.Downed) continue;
                if (have && p.thingIDNumber == lid) return p;
                float score = 0f;
                if (p.skills != null)
                    score = p.skills.GetSkill(SkillDefOf.Shooting).Level + p.skills.GetSkill(SkillDefOf.Intellectual).Level;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public static void RecordPropagatedDraft(Pawn leader, Pawn member,
            int episode)
        {
            if (Instance == null || leader == null || member == null
                || episode <= 0) return;
            Instance.propagatedDraftLeader[member.thingIDNumber] =
                leader.thingIDNumber;
            Instance.propagatedDraftEpisode[member.thingIDNumber] = episode;
        }

        public static bool TryGetPropagatedDraft(Pawn member,
            out int leaderId, out int episode)
        {
            leaderId = -1;
            episode = -1;
            return Instance != null && member != null
                && Instance.propagatedDraftLeader.TryGetValue(
                    member.thingIDNumber, out leaderId)
                && Instance.propagatedDraftEpisode.TryGetValue(
                    member.thingIDNumber, out episode);
        }

        public static void ClearPropagatedDraftForDirectToggle(Pawn member,
            bool newValue)
        {
            int leaderId, episode;
            if (!TryGetPropagatedDraft(member, out leaderId, out episode)) return;
            Instance.propagatedDraftLeader.Remove(member.thingIDNumber);
            Instance.propagatedDraftEpisode.Remove(member.thingIDNumber);
            CATrace.Pawn(member, "draft-chain ownership ENDED by direct pawn toggle to "
                + (newValue ? "drafted" : "undrafted"),
                anchor: member.Spawned ? (IntVec3?)member.Position : null,
                intent: new CAIntentContext(episode,
                    CAIntentOrigin.OperatorDirect,
                    CAIntentController.DraftCoordination,
                    member.thingIDNumber));
        }

        public static bool ReleasePropagatedDraft(Pawn leader, Pawn member,
            string reason)
        {
            int owner, episode;
            if (!TryGetPropagatedDraft(member, out owner, out episode)
                || leader != null && owner != leader.thingIDNumber) return false;
            Instance.propagatedDraftLeader.Remove(member.thingIDNumber);
            Instance.propagatedDraftEpisode.Remove(member.thingIDNumber);
            CATrace.Pawn(member, "draft-chain RELEASED - " + reason,
                anchor: member.Spawned ? (IntVec3?)member.Position : null,
                intent: new CAIntentContext(episode,
                    CAIntentOrigin.Continuation,
                    CAIntentController.DraftCoordination, owner));
            if (member.drafter != null && member.drafter.Drafted)
                member.drafter.Drafted = false;
            return true;
        }

        public static void ReleaseDraftsOwnedBy(Pawn leader, string reason)
        {
            if (Instance == null || leader == null) return;
            var memberIds = new List<int>(
                Instance.propagatedDraftLeader.Keys);
            for (int i = 0; i < memberIds.Count; i++)
            {
                int owner;
                if (!Instance.propagatedDraftLeader.TryGetValue(memberIds[i],
                    out owner) || owner != leader.thingIDNumber) continue;
                Pawn member = PawnByIdAnyMap(memberIds[i]);
                if (member != null)
                    ReleasePropagatedDraft(leader, member, reason);
                else
                {
                    Instance.propagatedDraftLeader.Remove(memberIds[i]);
                    Instance.propagatedDraftEpisode.Remove(memberIds[i]);
                }
            }
        }

        public static void MaintainPropagatedDrafts(Map map,
            bool featureEnabled)
        {
            if (Instance == null || map == null
                || Instance.propagatedDraftLeader.Count == 0) return;
            var memberIds = new List<int>(Instance.propagatedDraftLeader.Keys);
            for (int i = 0; i < memberIds.Count; i++)
            {
                Pawn member = PawnById(map, memberIds[i]);
                if (member == null) continue;
                int leaderId = Instance.propagatedDraftLeader[memberIds[i]];
                Pawn leader = PawnByIdAnyMap(leaderId);
                if (!featureEnabled || leader == null || leader.Map != map
                    || leader.Dead || leader.Downed
                    || leader.drafter == null || !leader.drafter.Drafted)
                    ReleasePropagatedDraft(leader ?? PawnById(map, leaderId),
                        member, !featureEnabled ? "draft-chain setting disabled"
                            : "originating leader is no longer an active same-map draft controller");
            }
        }

        private static Pawn PawnById(Map map, int id)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == id) return pawns[i];
            return null;
        }

        private static Pawn PawnByIdAnyMap(int id)
        {
            if (Find.Maps == null) return null;
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Pawn pawn = PawnById(Find.Maps[i], id);
                if (pawn != null) return pawn;
            }
            return null;
        }

        // A pawn who is a genuine liability on a firing line: unskilled or breaks under nerves.
        public static bool CombatLiability(Pawn p)
        {
            if (p == null) return true;
            if (p.skills != null)
            {
                int shoot = p.skills.GetSkill(SkillDefOf.Shooting).Level;
                int melee = p.skills.GetSkill(SkillDefOf.Melee).Level;
                if (shoot < 4 && melee < 4) return true;
            }
            var traits = p.story != null ? p.story.traits : null;
            if (traits != null)
            {
                var wimp = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
                if (wimp != null && traits.HasTrait(wimp)) return true;
                var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
                if (nerves != null && traits.DegreeOfTrait(nerves) < 0) return true;
            }
            return false;
        }
    }
}
