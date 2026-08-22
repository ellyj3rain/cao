using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Records acts and how people learn about them: firsthand, witnessed, or
    // reported. Reactions require knowledge of the act. Organizations spread
    // reports only when their reporting rule permits it.
    public enum CAActKnowledgeSource : byte
    {
        Firsthand = 0,
        Witnessed = 1,
        Reported = 2
    }

    public sealed class CAActRecord : IExposable
    {
        public int id;
        public int tick;
        public int mapId = -1;
        public IntVec3 cell = IntVec3.Invalid;
        // whose practice this was - the organization answerable for it
        public string orgKey;
        public int actorPawnId = -1;
        public int subjectPawnId = -1;
        // Exact act kind, such as taxation, confiscation, coercion, violence,
        // compelled labor, or refused labor. The evaluator applies political
        // beliefs after the factual act is recorded.
        public string act;
        public string detail;
        // Independent facts about authority, consent, payment, emergency,
        // procedure, and force.
        public bool authorityClaimed;
        public bool obligationRecognized;
        public bool consentGiven;
        public bool compensated;
        public bool emergencyBasis;
        public bool procedureFollowed;
        public bool forceUsed;
        // Observable context, such as mutual combat or striking someone who
        // was unarmed, unresisting, or downed.
        public string circumstance;
        // Whether the act killed its subject.
        public bool lethal;

        // who holds this fact, and how they came to (parallel lists,
        // the house idiom)
        public List<int> knownByIds = new List<int>();
        public List<byte> knownHow = new List<byte>();
        public List<int> knownSourceIds = new List<int>();
        public List<int> knownSourceHolderIds = new List<int>();
        public List<int> knownSourcePawnIds = new List<int>();
        public List<byte> knownSourceHow = new List<byte>();
        public List<int> answeredSourceHolderIds = new List<int>();
        public List<int> answeredSourcePawnIds = new List<int>();
        // response bookkeeping: each org and each pawn answers an
        // event ONCE - the event-identity half of no-duplicate-onset
        public List<string> orgsAnswered = new List<string>();
        public List<int> pawnsAnswered = new List<int>();

        public bool Knows(int pawnId)
        {
            return knownByIds.Contains(pawnId);
        }

        public void Learn(int pawnId, CAActKnowledgeSource how,
            int sourcePawnId = -1)
        {
            if (pawnId < 0) return;
            if (how == CAActKnowledgeSource.Reported
                && !CACulturalCognitionPureKernel.IsDistinctReportRoute(
                    pawnId, sourcePawnId))
                return;
            int at = knownByIds.IndexOf(pawnId);
            if (at < 0)
            {
                knownByIds.Add(pawnId);
                knownHow.Add((byte)how);
                knownSourceIds.Add(sourcePawnId);
            }
            if (sourcePawnId >= 0 && !HasSource(pawnId, sourcePawnId))
            {
                knownSourceHolderIds.Add(pawnId);
                knownSourcePawnIds.Add(sourcePawnId);
                knownSourceHow.Add((byte)how);
            }
        }

        private bool HasSource(int holderPawnId, int sourcePawnId)
        {
            for (int i = 0; i < knownSourceHolderIds.Count
                && i < knownSourcePawnIds.Count; i++)
                if (knownSourceHolderIds[i] == holderPawnId
                    && knownSourcePawnIds[i] == sourcePawnId)
                    return true;
            return false;
        }

        public IEnumerable<int> SourcePawnIdsFor(int pawnId)
        {
            for (int i = 0; i < knownSourceHolderIds.Count
                && i < knownSourcePawnIds.Count; i++)
                if (knownSourceHolderIds[i] == pawnId
                    && knownSourcePawnIds[i] >= 0)
                    yield return knownSourcePawnIds[i];
        }

        public IEnumerable<int> UnansweredSourcePawnIdsFor(int pawnId)
        {
            int[] sources = SourcePawnIdsFor(pawnId).Distinct().ToArray();
            if (sources.Length == 0) sources = new[] { -1 };
            foreach (int sourcePawnId in sources)
                if (!SourceAnswered(pawnId, sourcePawnId))
                    yield return sourcePawnId;
        }

        public bool HasUnansweredKnowledgeSource(int pawnId) =>
            UnansweredSourcePawnIdsFor(pawnId).Any();

        public CAActKnowledgeSource HowKnownFrom(int holderPawnId,
            int sourcePawnId)
        {
            for (int i = 0; i < knownSourceHolderIds.Count
                && i < knownSourcePawnIds.Count
                && i < knownSourceHow.Count; i++)
                if (knownSourceHolderIds[i] == holderPawnId
                    && knownSourcePawnIds[i] == sourcePawnId)
                    return (CAActKnowledgeSource)knownSourceHow[i];
            return HowKnown(holderPawnId);
        }

        public string KnowledgeSourceFor(int holderPawnId, int sourcePawnId) =>
            CACulturalCognitionPureKernel.ActKnowledgeChannel(
                (byte)HowKnownFrom(holderPawnId, sourcePawnId));

        public void MarkKnowledgeSourceAnswered(int pawnId, int sourcePawnId)
        {
            if (SourceAnswered(pawnId, sourcePawnId)) return;
            answeredSourceHolderIds.Add(pawnId);
            answeredSourcePawnIds.Add(sourcePawnId);
        }

        private bool SourceAnswered(int holderPawnId, int sourcePawnId)
        {
            for (int i = 0; i < answeredSourceHolderIds.Count
                && i < answeredSourcePawnIds.Count; i++)
                if (answeredSourceHolderIds[i] == holderPawnId
                    && answeredSourcePawnIds[i] == sourcePawnId)
                    return true;
            return false;
        }

        public CAActKnowledgeSource HowKnown(int pawnId)
        {
            int at = knownByIds.IndexOf(pawnId);
            return at < 0 ? CAActKnowledgeSource.Reported
                : (CAActKnowledgeSource)knownHow[at];
        }

        public string SourceIdentityFor(int pawnId)
        {
            int at = knownByIds.IndexOf(pawnId);
            if (at < 0) return null;
            int sourcePawnId = at < knownSourceIds.Count
                ? knownSourceIds[at] : -1;
            if (sourcePawnId >= 0) return sourcePawnId.ToString();
            CAActKnowledgeSource how = HowKnown(pawnId);
            if (how == CAActKnowledgeSource.Firsthand
                || how == CAActKnowledgeSource.Witnessed)
                return pawnId.ToString();
            return orgKey.NullOrEmpty()
                ? "unresolved report source" : "organization:" + orgKey;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref orgKey, "orgKey");
            Scribe_Values.Look(ref actorPawnId, "actorPawnId", -1);
            Scribe_Values.Look(ref subjectPawnId, "subjectPawnId", -1);
            Scribe_Values.Look(ref act, "act");
            Scribe_Values.Look(ref detail, "detail");
            Scribe_Values.Look(ref authorityClaimed, "authorityClaimed",
                false);
            Scribe_Values.Look(ref obligationRecognized,
                "obligationRecognized", false);
            Scribe_Values.Look(ref consentGiven, "consentGiven", false);
            Scribe_Values.Look(ref compensated, "compensated", false);
            Scribe_Values.Look(ref emergencyBasis, "emergencyBasis",
                false);
            Scribe_Values.Look(ref procedureFollowed,
                "procedureFollowed", false);
            Scribe_Values.Look(ref forceUsed, "forceUsed", false);
            Scribe_Values.Look(ref circumstance, "circumstance");
            Scribe_Values.Look(ref lethal, "lethal", false);
            Scribe_Collections.Look(ref knownByIds, "knownByIds",
                LookMode.Value);
            Scribe_Collections.Look(ref knownHow, "knownHow",
                LookMode.Value);
            Scribe_Collections.Look(ref knownSourceIds, "knownSourceIds",
                LookMode.Value);
            Scribe_Collections.Look(ref knownSourceHolderIds,
                "knownSourceHolderIds", LookMode.Value);
            Scribe_Collections.Look(ref knownSourcePawnIds,
                "knownSourcePawnIds", LookMode.Value);
            Scribe_Collections.Look(ref knownSourceHow,
                "knownSourceHow", LookMode.Value);
            Scribe_Collections.Look(ref answeredSourceHolderIds,
                "answeredSourceHolderIds", LookMode.Value);
            Scribe_Collections.Look(ref answeredSourcePawnIds,
                "answeredSourcePawnIds", LookMode.Value);
            Scribe_Collections.Look(ref orgsAnswered, "orgsAnswered",
                LookMode.Value);
            Scribe_Collections.Look(ref pawnsAnswered, "pawnsAnswered",
                LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (knownByIds == null) knownByIds = new List<int>();
                if (knownHow == null) knownHow = new List<byte>();
                if (knownSourceIds == null)
                    knownSourceIds = new List<int>();
                if (knownSourceHolderIds == null)
                    knownSourceHolderIds = new List<int>();
                if (knownSourcePawnIds == null)
                    knownSourcePawnIds = new List<int>();
                if (knownSourceHow == null)
                    knownSourceHow = new List<byte>();
                if (answeredSourceHolderIds == null)
                    answeredSourceHolderIds = new List<int>();
                if (answeredSourcePawnIds == null)
                    answeredSourcePawnIds = new List<int>();
                while (knownSourceIds.Count < knownByIds.Count)
                    knownSourceIds.Add(-1);
                if (knownSourceIds.Count > knownByIds.Count)
                    knownSourceIds.RemoveRange(knownByIds.Count,
                        knownSourceIds.Count - knownByIds.Count);
                int paired = System.Math.Min(knownSourceHolderIds.Count,
                    knownSourcePawnIds.Count);
                while (knownSourceHow.Count < paired)
                    knownSourceHow.Add((byte)CAActKnowledgeSource.Reported);
                paired = System.Math.Min(paired, knownSourceHow.Count);
                if (knownSourceHolderIds.Count > paired)
                    knownSourceHolderIds.RemoveRange(paired,
                        knownSourceHolderIds.Count - paired);
                if (knownSourcePawnIds.Count > paired)
                    knownSourcePawnIds.RemoveRange(paired,
                        knownSourcePawnIds.Count - paired);
                if (knownSourceHow.Count > paired)
                    knownSourceHow.RemoveRange(paired,
                        knownSourceHow.Count - paired);
                int answered = System.Math.Min(
                    answeredSourceHolderIds.Count,
                    answeredSourcePawnIds.Count);
                if (answeredSourceHolderIds.Count > answered)
                    answeredSourceHolderIds.RemoveRange(answered,
                        answeredSourceHolderIds.Count - answered);
                if (answeredSourcePawnIds.Count > answered)
                    answeredSourcePawnIds.RemoveRange(answered,
                        answeredSourcePawnIds.Count - answered);
                for (int i = 0; i < knownByIds.Count; i++)
                    if (knownSourceIds[i] >= 0
                        && !HasSource(knownByIds[i], knownSourceIds[i]))
                    {
                        knownSourceHolderIds.Add(knownByIds[i]);
                        knownSourcePawnIds.Add(knownSourceIds[i]);
                        knownSourceHow.Add(i < knownHow.Count
                            ? knownHow[i]
                            : (byte)CAActKnowledgeSource.Reported);
                    }
                if (orgsAnswered == null)
                    orgsAnswered = new List<string>();
                if (pawnsAnswered == null)
                    pawnsAnswered = new List<int>();
            }
        }
    }

    public sealed class CAActLedger : WorldComponent
    {
        private const int WitnessRadius = 18;
        private const int ReportDelayTicks = 60000;   // a day
        private const int RetainTicks = 15 * 60000;   // a quadrum
        private const int TickInterval = 2500;

        private List<CAActRecord> records =
            new List<CAActRecord>();
        private int nextId = 1;
        private int lastTick;

        public CAActLedger(World world) : base(world) { }

        public static CAActLedger Current =>
            Find.World?.GetComponent<CAActLedger>();

        public IReadOnlyList<CAActRecord> Records => records;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "CA_actRecords",
                LookMode.Deep);
            Scribe_Values.Look(ref nextId, "CA_actRecordNextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && records == null)
                records = new List<CAActRecord>();
        }

        // An act enters the world. Firsthand parties know at once;
        // anyone on the ground with a clear line of sight witnessed
        // it. Everyone else waits on a report or never learns.
        public CAActRecord Emit(string act, string orgKey,
            int actorPawnId, int subjectPawnId, string detail,
            Map map, IntVec3 cell,
            bool authorityClaimed = false,
            bool obligationRecognized = false,
            bool consentGiven = false,
            bool compensated = false,
            bool emergencyBasis = false,
            bool procedureFollowed = false,
            bool forceUsed = false,
            string circumstance = null,
            bool lethal = false)
        {
            var e = new CAActRecord
            {
                id = nextId++,
                tick = Find.TickManager?.TicksGame ?? 0,
                mapId = map?.uniqueID ?? -1,
                cell = cell,
                orgKey = orgKey,
                actorPawnId = actorPawnId,
                subjectPawnId = subjectPawnId,
                act = act,
                detail = detail,
                authorityClaimed = authorityClaimed,
                obligationRecognized = obligationRecognized,
                consentGiven = consentGiven,
                compensated = compensated,
                emergencyBasis = emergencyBasis,
                procedureFollowed = procedureFollowed,
                forceUsed = forceUsed,
                circumstance = circumstance,
                lethal = lethal
            };
            e.Learn(actorPawnId, CAActKnowledgeSource.Firsthand,
                actorPawnId);
            e.Learn(subjectPawnId, CAActKnowledgeSource.Firsthand,
                subjectPawnId);
            if (map != null && cell.IsValid)
            {
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p?.RaceProps == null || !p.RaceProps.Humanlike)
                        continue;
                    if (p.Dead || p.Downed || !p.Awake()) continue;
                    if (e.Knows(p.thingIDNumber)) continue;
                    if (!p.Position.InHorDistOf(cell, WitnessRadius))
                        continue;
                    if (!GenSight.LineOfSight(p.Position, cell, map,
                        true)) continue;
                    e.Learn(p.thingIDNumber, CAActKnowledgeSource.Witnessed,
                        p.thingIDNumber);
                }
            }
            records.Add(e);
            return e;
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            int now = Find.TickManager.TicksGame;
            if (now - lastTick < TickInterval) return;
            lastTick = now;
            try
            {
                SpreadReports(now);
                CAPoliticalBeliefEffects.JudgeEvents(records, now);
                Cull(now);
            }
            catch (System.Exception ex)
            {
                Log.Warning("[CA] act-record ledger tick: " + ex.Message);
            }
        }

        // Word travels along the reporting habit. A body that holds
        // "status reporting" passes what any member knows to the rest
        // a day later; a body without it does not.
        private void SpreadReports(int now)
        {
            var world = CAOrganizationWorldComponent.Current;
            if (world == null) return;
            for (int i = 0; i < records.Count; i++)
            {
                CAActRecord e = records[i];
                if (now - e.tick < ReportDelayTicks) continue;
                if (e.knownByIds.Count == 0) continue;
                foreach (CAOrganization org in world.Organizations)
                {
                    if (org?.memberPawnIds == null
                        || !org.HasCustom("status reporting"))
                        continue;
                    int[] reporterIds = org.memberPawnIds.Where(e.Knows)
                        .Distinct().ToArray();
                    if (reporterIds.Length == 0) continue;
                    for (int m = 0; m < org.memberPawnIds.Count; m++)
                        foreach (int reporterId in reporterIds)
                        {
                            if (!CACulturalCognitionPureKernel
                                .IsDistinctReportRoute(
                                    org.memberPawnIds[m], reporterId))
                                continue;
                            e.Learn(org.memberPawnIds[m],
                                CAActKnowledgeSource.Reported, reporterId);
                        }
                }
            }
        }

        private void Cull(int now)
        {
            records.RemoveAll(e => e == null
                || now - e.tick > RetainTicks);
        }
    }

    // Describes violence the same way at injury and death seams. Terms such as
    // "struck-downed" record observed circumstances without judging the act.
    public static class CAViolenceSite
    {
        public const string MutualCombat = "mutual-combat";
        public const string Unarmed = "struck-unarmed";
        public const string Unresisting = "struck-unresisting";
        public const string Downed = "struck-downed";

        // WHEN SOMEONE WENT DOWN. The engine records that a pawn IS
        // down and never when they went down, and that difference
        // decides whether a blow landed on a fighter or on someone
        // already beaten. It matters because the damage seam runs
        // AFTER the engine has applied this blow's own downing: with
        // no stamp, every knockdown in a fair fight would describe
        // itself as striking the fallen, and a people who hold that
        // the beaten are spared would be outraged by ordinary
        // defensive combat.
        private static readonly Dictionary<int, int> downedAt =
            new Dictionary<int, int>();
        private const int ClockPrune = 512;
        private const int ClockKeepTicks = 60000;

        public static void NoteDowned(Pawn p, int tick)
        {
            if (p == null) return;
            if (downedAt.Count > ClockPrune)
            {
                var stale = new List<int>();
                foreach (var kv in downedAt)
                    if (tick - kv.Value > ClockKeepTicks)
                        stale.Add(kv.Key);
                for (int i = 0; i < stale.Count; i++)
                    downedAt.Remove(stale[i]);
            }
            downedAt[p.thingIDNumber] = tick;
        }

        // Were they already down when this blow arrived? A stamp from
        // THIS tick means this blow is what put them there, so the
        // honest answer is no.
        public static bool WasAlreadyDown(Pawn p, int tick)
        {
            if (p == null || !p.Downed) return false;
            int at;
            if (!downedAt.TryGetValue(p.thingIDNumber, out at))
                return true;   // down, and we never saw it happen
            return at < tick;
        }

        // The victim's condition as the striker met them.
        public static string Describe(Pawn victim, Pawn striker,
            bool alreadyDown)
        {
            if (alreadyDown) return Downed;
            if (!GenHostility.HostileTo(victim, striker))
                return Unresisting;
            return victim.equipment?.Primary == null
                ? Unarmed : MutualCombat;
        }

        public static bool BothPeople(Pawn a, Pawn b)
        {
            return a?.RaceProps != null && a.RaceProps.Humanlike
                && b?.RaceProps != null && b.RaceProps.Humanlike;
        }

        // The body answerable for a pawn's conduct, in the keys the
        // organization ledger already uses.
        public static string OrgKeyOf(Pawn p)
        {
            if (p?.Faction == null) return null;
            if (p.Faction.IsPlayer) return "player";
            return CAHostileFactionOrganization.KeyFor(p.Faction);
        }
    }

    // THE TWO SEAMS THAT RECORD VIOLENCE.
    //
    // The damage seam already lives with the combat knowledge it also
    // feeds (KnowledgeModule). These two are purely observational: one
    // stamps when a pawn went down so the other seams can tell a
    // knockdown from a beating, and one records the moment a person
    // kills a person - which the damage seam CANNOT see, because the
    // engine returns from Pawn.PostApplyDamage before notifying a
    // mind that is already dead.
    //
    // Neither changes anything. They observe, and Political Order
    // decide what the observation was worth.
    public static class CAViolencePatches
    {
        public static void TryInstall(HarmonyLib.Harmony harmony)
        {
            try
            {
                harmony.Patch(HarmonyLib.AccessTools.Method(
                        typeof(Pawn_HealthTracker), "MakeDowned"),
                    postfix: new HarmonyLib.HarmonyMethod(
                        typeof(CAViolencePatches), nameof(DownedPostfix)));
            }
            catch (System.Exception e)
            {
                Log.Warning("[CA] downed clock stood down: " + e.Message);
            }
            try
            {
                harmony.Patch(HarmonyLib.AccessTools.Method(
                        typeof(Pawn), "Kill"),
                    prefix: new HarmonyLib.HarmonyMethod(
                        typeof(CAViolencePatches), nameof(KillPrefix)));
            }
            catch (System.Exception e)
            {
                Log.Warning("[CA] death seam stood down: " + e.Message);
            }
        }

        public static void DownedPostfix(Pawn ___pawn)
        {
            try
            {
                CAViolenceSite.NoteDowned(___pawn,
                    Find.TickManager?.TicksGame ?? 0);
            }
            catch { }
        }

        // A PREFIX, and it has to be. The engine checks ShouldBeDead
        // before it checks ShouldBeDowned, so a pawn killed outright
        // reaches Kill without ever having been downed - which means
        // Downed read HERE, before the kill runs, is exactly the fact
        // protection beliefs need: whether this person was
        // already beaten when someone ended them.
        public static void KillPrefix(Pawn __instance,
            DamageInfo? dinfo)
        {
            try
            {
                if (__instance == null || !dinfo.HasValue) return;
                Pawn striker = dinfo.Value.Instigator as Pawn;
                if (!CAViolenceSite.BothPeople(__instance, striker))
                    return;
                if (striker == __instance) return;
                Map map = __instance.MapHeld;
                if (map == null) return;
                int now = Find.TickManager?.TicksGame ?? 0;
                bool down = CAViolenceSite.WasAlreadyDown(__instance,
                    now);
                CAActLedger.Current?.Emit("violence",
                    CAViolenceSite.OrgKeyOf(striker),
                    striker.thingIDNumber, __instance.thingIDNumber,
                    striker.LabelShort + " killed "
                    + __instance.LabelShort, map,
                    __instance.PositionHeld,
                    forceUsed: true,
                    circumstance: CAViolenceSite.Describe(__instance,
                        striker, down),
                    lethal: true);
            }
            catch { }
        }
    }

    // A pawn's reaction to an act under one political belief. Matching events
    // renew one memory; different events remain separate. Memories group by
    // belief so vanilla diminishing returns bound repeated reactions.
    public class Thought_CAPoliticalBelief : Thought_Memory
    {
        public string detail;
        public string belief;
        public string eventIdentity;

        public override string LabelCap
        {
            get
            {
                string b = base.LabelCap;
                return string.IsNullOrEmpty(belief)
                    ? b : b + ": " + belief;
            }
        }

        public override string Description
        {
            get
            {
                string d = base.Description;
                return string.IsNullOrEmpty(detail) ? d
                    : d + "\n\n" + detail;
            }
        }

        // Merge only another memory of the same event.
        public override bool TryMergeWithExistingMemory(
            out bool showBubble)
        {
            showBubble = true;
            if (string.IsNullOrEmpty(eventIdentity)) return false;
            List<Thought_Memory> all =
                pawn?.needs?.mood?.thoughts?.memories?.Memories;
            if (all == null) return false;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i] as Thought_CAPoliticalBelief;
                if (other == null || ReferenceEquals(other, this))
                    continue;
                if (other.def != def) continue;
                if (other.eventIdentity != eventIdentity) continue;
                showBubble =
                    other.age > other.DurationTicks / 2;
                other.Renew();
                other.detail = detail;
                return true;
            }
            return false;
        }

        // Group reactions by political belief in the mood tab.
        public override bool GroupsWith(Thought other)
        {
            var o = other as Thought_CAPoliticalBelief;
            if (o == null) return false;
            return def == o.def && belief == o.belief;
        }

        public override void CopyFrom(Thought_Memory m)
        {
            base.CopyFrom(m);
            var c = m as Thought_CAPoliticalBelief;
            if (c == null) return;
            detail = c.detail;
            belief = c.belief;
            eventIdentity = c.eventIdentity;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref detail, "CA_detail");
            // The native-record preflight requires both fields present; a
            // thought created without a belief or identity string is a
            // handled state and must still serialize.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (belief == null) belief = "";
                if (eventIdentity == null) eventIdentity = "";
            }
            Scribe_Values.Look(ref belief, "CA_politicalBelief", null,
                forceSave: true);
            Scribe_Values.Look(ref eventIdentity, "CA_eventIdentity", null,
                forceSave: true);
        }
    }
}
