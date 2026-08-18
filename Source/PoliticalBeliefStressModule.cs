using System.Collections.Generic;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Developer checks for political-belief memories. The census is
    // read-only. Stress actions deliberately add synthetic acts to the save.
    public static partial class CADebugActions
    {
        // vanilla's convergent bound for a group:
        // mean * (1 + m + m^2 + ...) = mean / (1 - m)
        private static float BeliefGroupBound(ThoughtDef def)
        {
            if (def?.stages == null || def.stages.Count == 0) return 0f;
            float m = def.stackedEffectMultiplier;
            if (m >= 1f) return float.NegativeInfinity;
            return def.stages[0].baseMoodEffect / (1f - m);
        }

        [DebugAction("Colonist Awareness", "Political belief memory census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PoliticalBeliefMemoryCensus()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[CA][PoliticalBeliefs] memory census");
            int pawnsWith = 0, worstCount = 0, problems = 0;
            string worstPawn = "-";

            foreach (Map map in Find.Maps)
            {
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                {
                    var mem = p?.needs?.mood?.thoughts?.memories;
                    if (mem == null) continue;
                    var mine = new List<Thought_CAPoliticalBelief>();
                    foreach (Thought_Memory t in mem.Memories)
                    {
                        var c = t as Thought_CAPoliticalBelief;
                        if (c != null) mine.Add(c);
                    }
                    if (mine.Count == 0) continue;
                    pawnsWith++;
                    if (mine.Count > worstCount)
                    {
                        worstCount = mine.Count;
                        worstPawn = p.LabelShort;
                    }
                    problems += ReportPawn(p, mine, sb);
                }
            }

            sb.AppendLine("pawns with political-belief memories: " + pawnsWith
                + "; most held by one pawn: " + worstCount
                + " (" + worstPawn + ")");
            sb.AppendLine(problems == 0
                ? "Checks passed: no lost words, no duplicate "
                    + "grievance, no group past its bound"
                : "Check failures: " + problems);
            Log.Message(sb.ToString());
        }

        // Check one pawn's saved, merged, and grouped belief memories.
        private static int ReportPawn(Pawn p,
            List<Thought_CAPoliticalBelief> mine, StringBuilder sb)
        {
            int problems = 0;
            var eventIdentities = new HashSet<string>();
            var byBelief = new Dictionary<string, int>();

            foreach (Thought_CAPoliticalBelief c in mine)
            {
                // Saved belief and event identities are required for merging.
                if (string.IsNullOrEmpty(c.belief)
                    || string.IsNullOrEmpty(c.eventIdentity))
                {
                    sb.AppendLine("  ! " + p.LabelShort
                        + ": a grievance has no political belief"
                        + " or event identity - scribe gap");
                    problems++;
                    continue;
                }
                // One event identity may produce only one memory.
                if (!eventIdentities.Add(c.eventIdentity))
                {
                    sb.AppendLine("  ! " + p.LabelShort
                        + ": duplicate grievance identity "
                        + c.eventIdentity);
                    problems++;
                }
                int n;
                byBelief[c.belief] =
                    byBelief.TryGetValue(c.belief, out n)
                        ? n + 1 : 1;
            }

            sb.AppendLine("  " + p.LabelShort + ": " + mine.Count
                + " grievances across " + byBelief.Count
                + " Political Orders");

            var groups = new List<Thought>();
            p.needs.mood.thoughts.GetDistinctMoodThoughtGroups(groups);
            float politicalBeliefMood = 0f;
            foreach (Thought g in groups)
            {
                var c = g as Thought_CAPoliticalBelief;
                if (c == null) continue;
                // Exercise the same grouped-mood read used by the needs tab.
                float mood = p.needs.mood.thoughts.MoodOffsetOfGroup(g);
                politicalBeliefMood += mood;
                int held;
                byBelief.TryGetValue(c.belief ?? "", out held);
                float bound = BeliefGroupBound(c.def);
                // Vanilla's geometric stacking must stay within its bound.
                bool past = c.def.stages[0].baseMoodEffect < 0f
                    ? mood < bound - 0.001f
                    : mood > bound + 0.001f;
                if (past)
                {
                    sb.AppendLine("  ! " + p.LabelShort + ": group '"
                        + c.belief + "' at " + mood.ToString("0.00")
                        + " is past its convergent bound "
                        + bound.ToString("0.00"));
                    problems++;
                }
                sb.AppendLine("    " + c.belief + " x" + held
                    + "  mood " + mood.ToString("0.00")
                    + "  (bound " + bound.ToString("0.00") + ")");
            }
            sb.AppendLine("    total political-belief mood: "
                + politicalBeliefMood.ToString("0.00"));
            return problems;
        }

        [DebugAction("Colonist Awareness",
            "Set political-belief stress fixture",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SetPoliticalBeliefFixture()
        {
            CAFactionState state = CAFactionStateWorldComponent.Current
                ?.EnsureFor(Faction.OfPlayer);
            if (state == null)
            {
                Log.Warning("[CA][PoliticalBeliefs] no player faction state.");
                return;
            }
            state.politicalBeliefs = new CAPoliticalBeliefs();
            CAPoliticalBeliefsModel.Ensure(state.politicalBeliefs,
                "political-belief-stress");
            CAFactionAxes.Set(state.politicalBeliefs.positions,
                CAFactionAxes.WarConduct, "combatants",
                CAAxisSource.Authored);
            CAFactionAxes.Set(state.politicalBeliefs.positions,
                CAFactionAxes.Ownership, "private",
                CAAxisSource.Authored);
            CAFactionAxes.Set(state.politicalBeliefs.positions,
                CAFactionAxes.Work, "contract",
                CAAxisSource.Authored);
            CAFactionAxes.Set(state.politicalBeliefs.positions,
                CAFactionAxes.Leadership, "whole",
                CAAxisSource.Authored);
            CAFactionAxes.Set(state.politicalBeliefs.positions,
                CAFactionAxes.Participation, "universal",
                CAAxisSource.Authored);
            Log.Message("[CA][PoliticalBeliefs] stress fixture "
                + "set for the player faction.");
        }

        // Add distinct synthetic acts across several Political Orders.
        [DebugAction("Colonist Awareness",
            "Political belief stress: 40 distinct acts",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PoliticalBeliefStressDistinct()
        {
            PoliticalBeliefStress(40, distinct: true);
        }

        // Repeating one act should still produce one merged grievance.
        [DebugAction("Colonist Awareness",
            "Political belief stress: repeat one act 40 times",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PoliticalBeliefStressRepeat()
        {
            PoliticalBeliefStress(40, distinct: false);
        }

        private static void PoliticalBeliefStress(int count, bool distinct)
        {
            Map map = Find.CurrentMap;
            CAActLedger ledger = CAActLedger.Current;
            if (map == null || ledger == null)
            {
                Log.Warning("[CA][PoliticalBeliefs] no map or no ledger.");
                return;
            }
            var colonists =
                new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            if (colonists.Count == 0)
            {
                Log.Warning("[CA][PoliticalBeliefs] no colonists to witness.");
                return;
            }
            Pawn actor = colonists[0];
            int before = CountPoliticalBeliefMemories(colonists);
            var made = new List<CAActRecord>();

            string orgKey = CAViolenceSite.OrgKeyOf(actor);
            for (int i = 0; i < count; i++)
            {
                // a different victim each time when distinct; the same
                // victim, act and circumstance every time when not
                int victimId = distinct ? 900000 + i : 900000;
                string detail = "[dev] synthetic act " + i;
                if (!distinct)
                {
                    made.Add(ledger.Emit("violence", orgKey,
                        actor.thingIDNumber, victimId, detail, map,
                        actor.Position, forceUsed: true,
                        circumstance: CAViolenceSite.Downed,
                        lethal: true));
                    continue;
                }
                // cycle the families so one witness accumulates
                // grievances under several unrelated Political Orders
                switch (i % 4)
                {
                    case 0:
                        made.Add(ledger.Emit("violence", orgKey,
                            actor.thingIDNumber, victimId, detail, map,
                            actor.Position, forceUsed: true,
                            circumstance: CAViolenceSite.Downed,
                            lethal: true));
                        break;
                    case 1:
                        made.Add(ledger.Emit("confiscation", orgKey,
                            actor.thingIDNumber, victimId, detail, map,
                            actor.Position, authorityClaimed: true,
                            forceUsed: true));
                        break;
                    case 2:
                        made.Add(ledger.Emit("compelled-work", orgKey,
                            actor.thingIDNumber, victimId, detail, map,
                            actor.Position, authorityClaimed: true));
                        break;
                    default:
                        made.Add(ledger.Emit("coercion", orgKey,
                            actor.thingIDNumber, victimId, detail, map,
                            actor.Position, authorityClaimed: true,
                            forceUsed: true));
                        break;
                }
            }

            CAPoliticalBeliefEffects.JudgeEvents(made,
                Find.TickManager.TicksGame);
            int after = CountPoliticalBeliefMemories(colonists);

            Log.Message("[CA][PoliticalBeliefs] stress: emitted " + count
                + (distinct ? " DISTINCT acts" : " REPEATS of one act")
                + "; political-belief memories across " + colonists.Count
                + " colonists went " + before + " -> " + after
                + (distinct
                    ? ". Distinct acts SHOULD raise this."
                    : ". Repeats SHOULD leave it near unchanged -"
                        + " event matching renews one grievance.")
                + " Run the census for the per-pawn breakdown, then"
                + " save, reload, and run it again to prove the"
                + " grievances and their identities survived.");
        }

        private static int CountPoliticalBeliefMemories(List<Pawn> pawns)
        {
            int n = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                var mem = pawns[i]?.needs?.mood?.thoughts?.memories;
                if (mem == null) continue;
                foreach (Thought_Memory t in mem.Memories)
                    if (t is Thought_CAPoliticalBelief) n++;
            }
            return n;
        }
    }
}
