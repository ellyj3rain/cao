using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // PARLEY: negotiation remains possible AT THE MOMENT OF VIOLENCE.
    // That is the concept - not the horse, not the walk. The last moment
    // is still a moment: disaster can be averted, relitigated, or
    // incited, by whoever has standing to speak and a way to be heard.
    // Two channels, one arithmetic: IN PERSON (crossing the ground, which
    // risks the speaker and reads as nerve) or BY VOICE (comms console or
    // mechlink - no walk, but no presence either). Either way the answer
    // moves REAL lords: they withdraw, they take tribute, or they come on
    // harder for the insult.
    public enum CAParleyTerms
    {
        Withdraw = 0,
        Tribute = 1,
        Submission = 2
    }

    public sealed class CAParleyMission : IExposable
    {
        public int negotiatorId;
        public int factionLoadId = -1;
        public int startTick;
        public int terms;
        public IntVec3 meetCell;
        public bool inPerson = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref negotiatorId, "negotiatorId", 0);
            Scribe_Values.Look(ref factionLoadId, "factionLoadId", -1);
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref terms, "terms", 0);
            Scribe_Values.Look(ref meetCell, "meetCell");
            Scribe_Values.Look(ref inPerson, "inPerson", true);
        }
    }

    // An ENVOY: a person carrying a proposal across the ground to
    // another organization. Diplomacy is something someone DOES, in the
    // world, where you can watch them go - not a button in a tab.
    public sealed class CAEnvoyMission : IExposable
    {
        public int envoyId;
        public string targetKey;
        public string kind;
        public int startTick;
        public IntVec3 destination;
        public string scopeKind = "general";
        public int sunsetDays;
        public string direction;
        public string contribA;
        public string contribB;

        public void ExposeData()
        {
            Scribe_Values.Look(ref envoyId, "envoyId", 0);
            Scribe_Values.Look(ref targetKey, "targetKey");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref destination, "destination");
            Scribe_Values.Look(ref scopeKind, "scopeKind", "general");
            Scribe_Values.Look(ref sunsetDays, "sunsetDays", 0);
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref contribA, "contribA");
            Scribe_Values.Look(ref contribB, "contribB");
        }
    }

    public sealed class CAParleyMapComponent : MapComponent
    {
        private List<CAParleyMission> missions =
            new List<CAParleyMission>();
        private List<CAEnvoyMission> envoys = new List<CAEnvoyMission>();

        public bool HasEnvoy(Pawn p)
        {
            for (int i = 0; i < envoys.Count; i++)
                if (envoys[i].envoyId == p.thingIDNumber) return true;
            return false;
        }

        public void SendEnvoy(Pawn envoy, CAOrganization target,
            string kind, IntVec3 destination)
        {
            SendEnvoy(envoy, target, kind, destination, "general", 0,
                null, null, null);
        }

        public void SendEnvoy(Pawn envoy, CAOrganization target,
            string kind, IntVec3 destination, string scopeKind,
            int sunsetDays, string direction, string contribA,
            string contribB)
        {
            if (envoy == null || target == null) return;
            envoys.Add(new CAEnvoyMission
            {
                envoyId = envoy.thingIDNumber,
                targetKey = target.organizationKey,
                kind = kind,
                startTick = Find.TickManager.TicksGame,
                destination = destination,
                scopeKind = scopeKind ?? "general",
                sunsetDays = sunsetDays,
                direction = direction,
                contribA = contribA,
                contribB = contribB
            });
            try
            {
                Job go = JobMaker.MakeJob(JobDefOf.Goto, destination);
                go.playerForced = true;
                envoy.jobs.TryTakeOrderedJob(go);
            }
            catch { }
            CAOrganizationWorldComponent.Current?.EnsureColony().Record(
                "relations", envoy.LabelShort + " set out for "
                + target.name + " carrying a " + kind + " proposal");
            Messages.Message(envoy.LabelShort + " sets out for "
                + target.name + " with a " + kind + " proposal.",
                new LookTargets(destination, map),
                MessageTypeDefOf.NeutralEvent, false);
        }

        private void TickEnvoys()
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return;
            int now = Find.TickManager.TicksGame;
            for (int i = envoys.Count - 1; i >= 0; i--)
            {
                CAEnvoyMission e = envoys[i];
                Pawn env = FindPawn(e.envoyId);
                CAOrganization target = comp.ByKey(e.targetKey);
                if (env == null || env.Dead || target == null)
                {
                    if (env != null && env.Dead)
                        comp.EnsureColony().Record("relations",
                            "our envoy never arrived - they died on the"
                            + " road");
                    envoys.RemoveAt(i);
                    continue;
                }
                if (env.Downed) continue;
                if (now - e.startTick > 120000)
                {
                    comp.EnsureColony().Record("relations",
                        env.LabelShort + " turned back - the road to "
                        + target.name + " was too long");
                    envoys.RemoveAt(i);
                    continue;
                }
                if (!env.Position.InHorDistOf(e.destination, 12f))
                    continue;
                ResolveEnvoy(comp, env, target, e);
                envoys.RemoveAt(i);
            }
        }

        // The meeting itself: the envoy speaks, the formula answers, and
        // the whole exchange happens where the two of them are standing.
        private void ResolveEnvoy(CAOrganizationWorldComponent comp,
            Pawn env, CAOrganization target, CAEnvoyMission e)
        {
            CAOrganization colony = comp.EnsureColony();
            float score;
            string breakdown;
            string hint;
            CAOrganization broker;
            string verdict = CAWillingness.Evaluate(colony, target,
                e.kind, e.scopeKind, e.sunsetDays,
                !e.contribA.NullOrEmpty(), e.direction, false, out score,
                out breakdown, out hint, out broker);
            float voice = 0.5f;
            try
            {
                voice = env.GetStatValue(StatDefOf.NegotiationAbility);
            }
            catch { }
            if (voice > 0.9f && verdict == "counter")
            {
                verdict = "accepted";
                breakdown += ", the envoy's own weight closed it";
            }
            if (verdict == "accepted")
            {
                comp.AddAgreement(new CAAgreement
                {
                    kind = e.kind,
                    partyA = "player",
                    partyB = target.organizationKey,
                    scopeKind = e.scopeKind,
                    sunsetTick = e.sunsetDays > 0
                        ? Find.TickManager.TicksGame
                        + e.sunsetDays * 60000 : -1,
                    contributionsA = e.contribA,
                    contributionsB = e.contribB,
                    protectorKey = e.kind == "protection"
                        ? (e.direction == "they-protect"
                            ? target.organizationKey : "player") : null,
                    payerKey = e.kind == "tribute"
                        ? (e.direction == "they-pay"
                            ? target.organizationKey : "player") : null,
                    brokerKey = broker != null
                        ? broker.organizationKey : null
                });
                colony.Record("agreement-made", env.LabelShort
                    + " came to terms with " + target.name + " - "
                    + e.kind + " [" + breakdown + "]");
                target.Record("agreement-made", "we came to terms with "
                    + colony.name + " - " + e.kind);
                Messages.Message(env.LabelShort + " and " + target.name
                    + " come to terms: " + e.kind + ". (" + breakdown
                    + ")", new LookTargets(env.Position, map),
                    MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                colony.Record("agreement-refused", target.name
                    + " refused " + env.LabelShort + "'s " + e.kind
                    + " proposal [" + breakdown + "]"
                    + (hint != null ? " - " + hint : ""));
                target.Record("agreement-refused", "we refused "
                    + colony.name + "'s " + e.kind + " proposal");
                Messages.Message(target.name + " refuses the " + e.kind
                    + " proposal" + (hint != null ? " - " + hint : "")
                    + ". (" + breakdown + ")",
                    new LookTargets(env.Position, map),
                    MessageTypeDefOf.NeutralEvent, false);
            }
        }

        public CAParleyMapComponent(Map map) : base(map) { }

        public static CAParleyMapComponent For(Map map)
        {
            return map?.GetComponent<CAParleyMapComponent>();
        }

        public bool HasMission(Pawn p)
        {
            for (int i = 0; i < missions.Count; i++)
                if (missions[i].negotiatorId == p.thingIDNumber)
                    return true;
            return false;
        }

        public void Begin(Pawn negotiator, Pawn target,
            CAParleyTerms terms)
        {
            Begin(negotiator, target, terms, true);
        }

        // Can this pawn be heard without crossing the ground?
        public static bool HasVoiceChannel(Pawn p)
        {
            try
            {
                if (CommsModule.HasActiveMechlink(p)) return true;
                foreach (Building_CommsConsole c in p.Map.listerBuildings
                    .AllBuildingsColonistOfClass<Building_CommsConsole>())
                    if (c.CanUseCommsNow) return true;
            }
            catch { }
            return false;
        }

        public void Begin(Pawn negotiator, Pawn target,
            CAParleyTerms terms, bool inPerson)
        {
            if (negotiator == null || target?.Faction == null) return;
            missions.Add(new CAParleyMission
            {
                negotiatorId = negotiator.thingIDNumber,
                factionLoadId = target.Faction.loadID,
                startTick = Find.TickManager.TicksGame,
                terms = (int)terms,
                meetCell = target.Position,
                inPerson = inPerson
            });
            if (inPerson)
            {
                try
                {
                    Job walk = JobMaker.MakeJob(JobDefOf.Goto,
                        target.Position);
                    walk.playerForced = true;
                    negotiator.jobs.TryTakeOrderedJob(walk);
                }
                catch { }
            }
            CATrace.Pawn(negotiator, "PARLEY - "
                + (inPerson ? "crossing to " : "hailing ")
                + target.Faction.Name + " under "
                + TermsLabel(terms) + " terms",
                anchor: target.Position);
            Messages.Message(negotiator.LabelShort
                + (inPerson ? " goes out to parley with " : " hails ")
                + target.Faction.Name + " - "
                + TermsLabel(terms) + ".",
                new LookTargets(target.Position, map),
                MessageTypeDefOf.NeutralEvent, false);
        }

        public static string TermsLabel(CAParleyTerms t)
        {
            return t == CAParleyTerms.Withdraw ? "asking them to withdraw"
                : t == CAParleyTerms.Tribute ? "offering tribute"
                : "demanding their submission";
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager.TicksGame % 60 != 0) return;
            TickEnvoys();
            for (int i = missions.Count - 1; i >= 0; i--)
            {
                CAParleyMission m = missions[i];
                Pawn neg = FindPawn(m.negotiatorId);
                if (neg == null || neg.Dead || neg.Downed)
                {
                    if (neg != null)
                        Record(neg, m, "the negotiator fell before a word"
                            + " was heard", false);
                    missions.RemoveAt(i);
                    continue;
                }
                if (Find.TickManager.TicksGame - m.startTick > 5000)
                {
                    Record(neg, m, "the parley never reached them", false);
                    missions.RemoveAt(i);
                    continue;
                }
                Pawn counterpart = m.inPerson
                    ? NearestOf(m.factionLoadId, neg.Position, 12f)
                    : NearestOf(m.factionLoadId, neg.Position, 9999f);
                if (counterpart == null) continue;
                if (!m.inPerson
                    && Find.TickManager.TicksGame - m.startTick < 600)
                    continue;
                Resolve(neg, counterpart, m);
                missions.RemoveAt(i);
            }
        }

        private Pawn FindPawn(int id)
        {
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == id) return pawns[i];
            return null;
        }

        private Pawn NearestOf(int factionLoadId, IntVec3 from,
            float radius)
        {
            Pawn best = null;
            float bestD = radius;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p.Downed || p.Faction == null
                    || p.Faction.loadID != factionLoadId) continue;
                float d = p.Position.DistanceTo(from);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        // The arithmetic of standing on a battlefield, printed in full.
        private void Resolve(Pawn neg, Pawn counterpart,
            CAParleyMission m)
        {
            var parts = new System.Text.StringBuilder();
            float weight = 0f;
            float voice = 0.5f;
            try
            {
                voice = neg.GetStatValue(StatDefOf.NegotiationAbility);
            }
            catch { }
            weight += voice * 0.5f;
            parts.Append("voice " + (voice * 0.5f).ToString("+0.00"));

            if (CAOrganizationAuthority.HasColonyCommand(neg)
                || SquadComponent.IsLeader(neg))
            {
                weight += 0.15f;
                parts.Append(", speaks with office +0.15");
            }

            // Who is winning THIS field, counted honestly by standing
            // bodies - the enemy hears the ground, not the speech.
            int theirs = 0, ours = 0;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p.Downed || p.Faction == null) continue;
                if (!p.Position.InHorDistOf(neg.Position, 45f)) continue;
                if (p.Faction.loadID == m.factionLoadId) theirs++;
                else if (p.Faction == Faction.OfPlayer) ours++;
            }
            float field = Mathf.Clamp((ours - theirs) * 0.05f, -0.4f,
                0.4f);
            weight += field;
            parts.Append(", the field " + field.ToString("+0.00;-0.00")
                + " (" + ours + " to " + theirs + ")");

            Faction other = counterpart.Faction;
            float good = 0f;
            try
            {
                if (other.HasGoodwill)
                    good = Mathf.Clamp(other.PlayerGoodwill / 400f,
                        -0.25f, 0.25f);
            }
            catch { }
            weight += good;
            parts.Append(", their books " + good.ToString("+0.00;-0.00"));

            if (!m.inPerson)
            {
                weight -= 0.15f;
                parts.Append(", a voice on the radio, not a face -0.15");
            }
            if (m.inPerson && neg.equipment?.Primary == null)
            {
                string note;
                float read = CAFrontier.ReadUnarmed(other, out note);
                if (note != null)
                {
                    weight += read;
                    parts.Append(", " + note);
                }
            }

            CAParleyTerms terms = (CAParleyTerms)m.terms;
            float ask = terms == CAParleyTerms.Withdraw ? 0.45f
                : terms == CAParleyTerms.Tribute ? 0.25f : 0.75f;
            parts.Append("; asking " + ask.ToString("0.00"));

            bool met = weight >= ask;
            if (met)
            {
                if (terms == CAParleyTerms.Tribute)
                    PayTribute(other);
                Withdraw(m.factionLoadId);
                if (terms == CAParleyTerms.Submission)
                    TryAffect(other, 25, "submission accepted");
                Record(neg, m, terms == CAParleyTerms.Submission
                    ? "they yield the field and acknowledge our terms"
                    : terms == CAParleyTerms.Tribute
                    ? "tribute accepted - they turn and go"
                    : "they hear it and withdraw", true, parts.ToString());
            }
            else
            {
                bool insult = terms == CAParleyTerms.Submission;
                if (insult) TryAffect(other, -15, "insulted at parley");
                Record(neg, m, insult
                    ? "the demand was an insult - they come on harder"
                    : "they refuse the terms and the guns speak again",
                    false, parts.ToString());
            }
        }

        private void Withdraw(int factionLoadId)
        {
            var lords = map.lordManager.lords;
            for (int i = lords.Count - 1; i >= 0; i--)
            {
                Lord lord = lords[i];
                if (lord.faction == null
                    || lord.faction.loadID != factionLoadId) continue;
                try
                {
                    lord.SetJob(new LordJob_ExitMapBest(
                        LocomotionUrgency.Jog, false, true));
                }
                catch { }
            }
        }

        private void PayTribute(Faction other)
        {
            try
            {
                int owed = 150;
                List<Thing> silver = map.listerThings.ThingsOfDef(
                    ThingDefOf.Silver);
                for (int i = silver.Count - 1; i >= 0 && owed > 0; i--)
                {
                    Thing s = silver[i];
                    if (s.IsForbidden(Faction.OfPlayer)) continue;
                    int take = Mathf.Min(owed, s.stackCount);
                    s.SplitOff(take).Destroy();
                    owed -= take;
                }
                TryAffect(other, 8, "tribute paid at parley");
            }
            catch { }
        }

        private void TryAffect(Faction other, int delta, string why)
        {
            try
            {
                if (other != null && other.HasGoodwill)
                    other.TryAffectGoodwillWith(Faction.OfPlayer, delta,
                        true, true, null, null);
            }
            catch { }
        }

        private void Record(Pawn neg, CAParleyMission m, string outcome,
            bool success, string breakdown = null)
        {
            CAOrganization colony =
                CAOrganizationWorldComponent.Current?.EnsureColony();
            colony?.Record("relations", "PARLEY on the field ("
                + TermsLabel((CAParleyTerms)m.terms) + "): " + outcome
                + (breakdown != null ? " [" + breakdown + "]" : ""));
            CATrace.Pawn(neg, "PARLEY resolved - " + outcome,
                anchor: neg.Position);
            Messages.Message(neg.LabelShort + " at the parley: "
                + outcome + (breakdown != null
                    ? " (" + breakdown + ")" : ""),
                new LookTargets(neg.Position, map),
                success ? MessageTypeDefOf.PositiveEvent
                    : MessageTypeDefOf.NegativeEvent, false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref missions, "CA_parleyMissions",
                LookMode.Deep);
            Scribe_Collections.Look(ref envoys, "CA_envoyMissions",
                LookMode.Deep);
            if (missions == null) missions = new List<CAParleyMission>();
            if (envoys == null) envoys = new List<CAEnvoyMission>();
        }
    }
}
