using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Applies political beliefs to current practices and reported acts.
    // Factions lose public support when their rules conflict with practice.
    // Informed pawns receive positive or negative belief memories.
    internal static class CAPoliticalBeliefEffects
    {
        internal const int PulseInterval = 60000;   // daily

        // Timing for one unresolved conflict: initial loss, daily loss,
        // habituation, repeat records, escalation, and recovery.
        internal struct CAResponseTiming
        {
            public float onset;
            public float drain;
            public int habituateAfterTicks;
            public float habituatedFactor;
            public int reinforceEveryTicks;
            public float escalatePerDayHeld;
            public float recoveryPerDay;

            public static CAResponseTiming Default => new CAResponseTiming
            {
                onset = 0.02f,
                drain = 0.005f,
                habituateAfterTicks = 15 * 60000,
                habituatedFactor = 0.5f,
                reinforceEveryTicks = 0,
                escalatePerDayHeld = 0f,
                recoveryPerDay = 0.0025f
            };
        }

        internal const float Floor = 0.25f;

        internal enum CAVerdict : byte
        {
            Silent = 0,
            Conforms = 1,
            Offends = 2
        }

        private delegate string CurrentConflictCheck(List<CARelation> bound,
            int now);
        private delegate CAVerdict EventJudge(CAActRecord e,
            out string meaning);

        private sealed class Row
        {
            public CurrentConflictCheck currentConflict;
            public EventJudge judge;
        }

        // Per-belief timing overrides. Protection failures are recorded again
        // every five days while they remain unresolved.
        internal static readonly Dictionary<string, CAResponseTiming>
            TimingOverrides = new Dictionary<string, CAResponseTiming>
            {
                ["surrender accepted"] = new CAResponseTiming
                {
                    onset = 0.03f,
                    drain = 0.005f,
                    habituateAfterTicks = 15 * 60000,
                    habituatedFactor = 0.5f,
                    reinforceEveryTicks = 5 * 60000,
                    escalatePerDayHeld = 0f,
                    recoveryPerDay = 0.0025f
                },
                ["combatants only"] = new CAResponseTiming
                {
                    onset = 0.03f,
                    drain = 0.005f,
                    habituateAfterTicks = 15 * 60000,
                    habituatedFactor = 0.5f,
                    reinforceEveryTicks = 5 * 60000,
                    escalatePerDayHeld = 0f,
                    recoveryPerDay = 0.0025f
                }
            };

        private static CAResponseTiming TimingFor(string key)
        {
            CAResponseTiming timing;
            return TimingOverrides.TryGetValue(key, out timing)
                ? timing : CAResponseTiming.Default;
        }

        // Each row describes what one political belief accepts or rejects.
        private static readonly Dictionary<string, Row> Table =
            new Dictionary<string, Row>
            {
                ["every voice counts"] = new Row
                {
                    currentConflict = delegate (List<CARelation> bound, int now)
                    {
                        int silenced = 0;
                        foreach (CARelation r in Persons(bound, now))
                            if (r.voice == CAVoiceKinds.None) silenced++;
                        return silenced > 0
                            ? silenced + " of our own have no voice,"
                                + " and we hold that every voice counts"
                            : null;
                    }
                },

                ["voluntary work"] = new Row
                {
                    currentConflict = delegate (List<CARelation> bound, int now)
                    {
                        int compelled = 0;
                        foreach (CARelation r in Persons(bound, now))
                            if (r.Delegates(CAResponsibilities.Work)
                                && !r.Retains(CAResponsibilities.Decisions))
                                compelled++;
                        return compelled > 0
                            ? compelled + " of our own can be ordered to work,"
                                + " against our belief in voluntary work"
                            : null;
                    },
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (e.act != "compelled-work")
                            return CAVerdict.Silent;
                        meaning = "work was compelled - " + e.detail;
                        return CAVerdict.Offends;
                    }
                },

                // Refusing required work is an observed act, not a continuing
                // rule conflict.
                ["required work"] = new Row
                {
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (e.act != "work-refused")
                            return CAVerdict.Silent;
                        meaning = "required work was refused - " + e.detail;
                        return CAVerdict.Offends;
                    }
                },

                ["property in common"] = new Row
                {
                    currentConflict = delegate (List<CARelation> bound, int now)
                    {
                        int keeping = 0;
                        foreach (CARelation r in Persons(bound, now))
                            if (r.compensation == CACompensationKinds.Keep)
                                keeping++;
                        return keeping > 0
                            ? "each keeps their own while we hold"
                                + " things in common"
                            : null;
                    },
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (!IsTaking(e.act)) return CAVerdict.Silent;
                        // to a communal people, an orderly taking into
                        // the common stock is order kept; a taking by
                        // naked force with no claimed authority is not
                        // commonality, it is plunder
                        if (e.forceUsed && !e.authorityClaimed)
                        {
                            meaning = "taken by force under no"
                                + " authority - " + e.detail;
                            return CAVerdict.Offends;
                        }
                        meaning = "the common stock was fed - "
                            + e.detail;
                        return CAVerdict.Conforms;
                    }
                },

                ["private holdings"] = new Row
                {
                    currentConflict = delegate (List<CARelation> bound, int now)
                    {
                        int pooled = 0;
                        foreach (CARelation r in Persons(bound, now))
                            if (r.Delegates(CAResponsibilities.Production)
                                && !r.Retains(CAResponsibilities.Decisions)) pooled++;
                        return pooled > 0
                            ? "what each makes is taken into the"
                                + " common stock, against our custom"
                                + " of private holdings"
                            : null;
                    },
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (!IsTaking(e.act)) return CAVerdict.Silent;
                        // free consent settles it outright
                        if (e.consentGiven) return CAVerdict.Silent;
                        // taxation under recognized obligation is
                        // jurisdiction - owed, not "taken" - and a
                        // private-holdings people abides it
                        if (e.act == "taxation"
                            && e.obligationRecognized)
                            return CAVerdict.Silent;
                        // a compensated emergency requisition is a
                        // hard bargain, not a robbery
                        if (e.act == "requisition" && e.emergencyBasis
                            && e.compensated) return CAVerdict.Silent;
                        meaning = (e.act == "confiscation"
                            || e.forceUsed
                            ? "what was theirs was seized"
                            : "what was theirs was taken without"
                                + " obligation or consent")
                            + " - " + e.detail;
                        return CAVerdict.Offends;
                    }
                },

                ["shared leadership"] = new Row
                {
                    currentConflict = delegate (List<CARelation> bound, int now)
                    {
                        foreach (CARelation r in Persons(bound, now))
                            if (r.Retains(CAResponsibilities.ArmedForce))
                                return "one among us gives binding"
                                    + " orders despite shared leadership";
                        return null;
                    },
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (e.act != "coercion") return CAVerdict.Silent;
                        meaning = "an order was enforced - " + e.detail;
                        return CAVerdict.Offends;
                    }
                },

                // This belief permits an enforced order and creates no current
                // conflict when no command exists.
                ["single leader"] = new Row
                {
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (e.act != "coercion") return CAVerdict.Silent;
                        meaning = "order was kept - " + e.detail;
                        return CAVerdict.Conforms;
                    }
                },

                // These beliefs judge force against a person who cannot or
                // does not fight back. Ordinary combat between armed fighters
                // creates no political-belief memory.
                ["victors decide"] = new Row
                {
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (e.act != "violence") return CAVerdict.Silent;
                        if (e.circumstance == CAViolenceSite.Downed)
                        {
                            meaning = (e.lethal
                                ? "a beaten enemy was finished"
                                : "one already down was put in their"
                                    + " place") + " - " + e.detail;
                            return CAVerdict.Conforms;
                        }
                        if (e.circumstance == CAViolenceSite.Unresisting)
                        {
                            meaning = "one who would not fight was"
                                + " dealt with - " + e.detail;
                            return CAVerdict.Conforms;
                        }
                        return CAVerdict.Silent;
                    }
                },

                ["surrender accepted"] = new Row
                {
                    currentConflict = delegate (List<CARelation> bound,
                        int now)
                    {
                        return Unprotected(bound, now,
                            "and we hold that the beaten are spared");
                    },
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (e.act != "violence") return CAVerdict.Silent;
                        if (e.circumstance != CAViolenceSite.Downed)
                            return CAVerdict.Silent;
                        meaning = (e.lethal
                            ? "someone already beaten was killed"
                            : "someone already beaten was struck again")
                            + " - " + e.detail;
                        return CAVerdict.Offends;
                    }
                },

                // Wider circle, same axis: the downed AND those who
                // never raised a hand. "struck-unarmed" is recorded by
                // the sites but judged by NO row - carrying no weapon
                // does not mean not fighting, and a brawler's hands
                // are their weapon. The fact is kept; the claim is
                // not made.
                ["combatants only"] = new Row
                {
                    currentConflict = delegate (List<CARelation> bound,
                        int now)
                    {
                        return Unprotected(bound, now,
                            "and we hold that only fighters may be"
                            + " struck");
                    },
                    judge = delegate (CAActRecord e,
                        out string meaning)
                    {
                        meaning = null;
                        if (e.act != "violence") return CAVerdict.Silent;
                        if (e.circumstance == CAViolenceSite.Downed)
                        {
                            meaning = (e.lethal
                                ? "someone already beaten was killed"
                                : "someone already beaten was struck"
                                    + " again") + " - " + e.detail;
                            return CAVerdict.Offends;
                        }
                        if (e.circumstance == CAViolenceSite.Unresisting)
                        {
                            meaning = (e.lethal
                                ? "someone who was not fighting was"
                                    + " killed"
                                : "someone who was not fighting was"
                                    + " struck") + " - " + e.detail;
                            return CAVerdict.Offends;
                        }
                        return CAVerdict.Silent;
                    }
                }
            };

        // Checks whether current relations provide the protection required by
        // this political belief.
        private static string Unprotected(List<CARelation> bound,
            int now, string tail)
        {
            int owed = 0;
            foreach (CARelation r in Persons(bound, now))
                if (r.protection == CAProtectionKinds.None) owed++;
            return owed > 0
                ? owed + (owed == 1 ? " of our own is" : " of our own are")
                    + " protected by nothing but themselves, " + tail
                : null;
        }

        // Four forms of taking that a property belief may
        // judge differently: ordinary taxation, wage withholding,
        // emergency requisition, outright confiscation.
        private static bool IsTaking(string act)
        {
            return act == "taxation" || act == "withholding"
                || act == "requisition" || act == "confiscation";
        }

        private static IEnumerable<CARelation> Persons(
            List<CARelation> bound, int now)
        {
            for (int i = 0; i < bound.Count; i++)
            {
                CARelation r = bound[i];
                if (!r.IsPawnParty) continue;
                if (r.status == "chattel") continue;
                if (r.Expired(now)) continue;
                yield return r;
            }
        }

        // ---- current-rule conflicts ------------------------------------

        // Distant settlements process missed daily updates when selected.
        // Catch-up is capped at one quadrum.
        private const int CatchUpCapDays = 15;
        // Limit work per selection; remaining days carry forward.
        private const int CatchUpDaysPerSelection = 5;

        internal static void Pulse(CAOrganization org, int now)
        {
            if (org == null || org.customs == null) return;
            if (org.lastBeliefCheckTick < 0)
            {
                org.lastBeliefCheckTick = now;
                PulseOnce(org, now);
                return;
            }
            int gap = (now - org.lastBeliefCheckTick) / PulseInterval;
            if (gap <= 0) return;
            if (gap > CatchUpCapDays)
            {
                // Days beyond the cap are skipped by moving the check time.
                org.lastBeliefCheckTick =
                    now - CatchUpCapDays * PulseInterval;
                gap = CatchUpCapDays;
            }
            int process = Mathf.Min(gap, CatchUpDaysPerSelection);
            for (int d = 1; d <= process; d++)
                PulseOnce(org,
                    org.lastBeliefCheckTick + d * PulseInterval);
            org.lastBeliefCheckTick += process * PulseInterval;
        }

        private static void PulseOnce(CAOrganization org, int now)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null) return;
            List<CARelation> bound =
                ledger.RelationsIn(org.organizationKey);
            if (org.openBeliefConflicts == null)
                org.openBeliefConflicts = new List<string>();
            if (org.beliefConflictStartTicks == null)
                org.beliefConflictStartTicks = new List<int>();
            while (org.beliefConflictStartTicks.Count
                < org.openBeliefConflicts.Count)
                org.beliefConflictStartTicks.Add(now);

            bool anyOpen = false;
            for (int i = 0; i < org.customs.Count; i++)
            {
                string key = org.customs[i]?.key;
                if (key == null) continue;
                Row row;
                if (!Table.TryGetValue(key, out row)
                    || row.currentConflict == null) continue;
                CAResponseTiming timing = TimingFor(key);
                string offence = row.currentConflict(bound, now);
                int at = org.openBeliefConflicts.IndexOf(key);

                if (offence != null && at < 0)
                {
                    // Record and apply the initial public-support loss once.
                    org.openBeliefConflicts.Add(key);
                    org.beliefConflictStartTicks.Add(now);
                    Cost(org, timing.onset);
                    org.Record("political-belief", offence);
                    if (org.organizationKey == "player")
                        Messages.Message("[CA] " + offence + ".",
                            MessageTypeDefOf.NegativeEvent, false);
                    anyOpen = true;
                }
                else if (offence != null)
                {
                    // Apply continuing loss, habituation, and repeat records.
                    int held = now - org.beliefConflictStartTicks[at];
                    float heldDays = held / 60000f;
                    float drain = timing.drain
                        * (1f + timing.escalatePerDayHeld * heldDays);
                    if (held >= timing.habituateAfterTicks)
                        drain *= timing.habituatedFactor;
                    Cost(org, drain);
                    if (timing.reinforceEveryTicks > 0
                        && held / timing.reinforceEveryTicks
                            != (held - PulseInterval)
                                / timing.reinforceEveryTicks)
                        org.Record("political-belief", "still unresolved: "
                            + offence);
                    anyOpen = true;
                }
                else if (at >= 0)
                {
                    // Record when the conflicting rule is removed.
                    org.openBeliefConflicts.RemoveAt(at);
                    org.beliefConflictStartTicks.RemoveAt(at);
                    org.Record("political-belief", "resolved: " + key);
                    if (org.organizationKey == "player")
                        Messages.Message("[CA] Set right: " + key + ".",
                            MessageTypeDefOf.PositiveEvent, false);
                }
                else if (at < 0 && org.openBeliefConflicts.Count > 0)
                    anyOpen = true;
            }

            // Recover only public support lost to resolved belief conflicts.
            if (!anyOpen && org.openBeliefConflicts.Count == 0
                && org.unrecoveredSupportLoss > 0f)
            {
                float repay = Mathf.Min(org.unrecoveredSupportLoss,
                    CAResponseTiming.Default.recoveryPerDay);
                org.publicSupport = Mathf.Min(1f, org.publicSupport + repay);
                org.unrecoveredSupportLoss -= repay;
            }
        }

        private static void Cost(CAOrganization org, float amount)
        {
            float before = org.publicSupport;
            org.publicSupport = Mathf.Max(Floor, org.publicSupport - amount);
            org.unrecoveredSupportLoss += before - org.publicSupport;
        }

        // ---- observed acts ---------------------------------------------

        // Each informed organization and pawn judges an act once.
        // Organizations change public support or record a grievance; pawns
        // receive memories from their own beliefs.
        internal static void JudgeEvents(
            IReadOnlyList<CAActRecord> events, int now)
        {
            if (events == null || events.Count == 0) return;
            var world = CAOrganizationWorldComponent.Current;
            if (world == null) return;

            for (int i = 0; i < events.Count; i++)
            {
                CAActRecord e = events[i];
                if (e == null || e.knownByIds.Count == 0) continue;
                JudgeForOrganizations(e, world);
                JudgeForPawns(e, now);
            }
        }

        // Each informed organization judges an act once against every
        // relevant custom. Its own violations reduce public support. Violations
        // by another organization become grievances.
        private static void JudgeForOrganizations(CAActRecord e,
            CAOrganizationWorldComponent world)
        {
            foreach (CAOrganization org in world.Organizations)
            {
                if (org?.customs == null
                    || org.memberPawnIds == null) continue;
                if (e.orgsAnswered.Contains(org.organizationKey)) continue;
                bool informed = false;
                for (int m = 0; m < org.memberPawnIds.Count; m++)
                    if (e.Knows(org.memberPawnIds[m]))
                    { informed = true; break; }
                if (!informed) continue;
                // Mark before judging so silent customs do not cause the same
                // event to be reconsidered on every update.
                e.orgsAnswered.Add(org.organizationKey);

                for (int c = 0; c < org.customs.Count; c++)
                {
                    string key = org.customs[c]?.key;
                    Row row;
                    string meaning;
                    if (key == null
                        || !Table.TryGetValue(key, out row)
                        || row.judge == null) continue;
                    CAVerdict verdict = row.judge(e, out meaning);
                    if (verdict == CAVerdict.Silent) continue;

                    if (verdict == CAVerdict.Offends
                        && e.orgKey == org.organizationKey)
                    {
                        Cost(org, TimingFor(key).onset);
                        org.Record("political-belief", meaning);
                        if (org.organizationKey == "player")
                            Messages.Message("[CA] " + meaning + ".",
                                MessageTypeDefOf.NegativeEvent, false);
                    }
                    else if (verdict == CAVerdict.Offends)
                        org.Record("political-belief",
                            "we hold improper what was done by "
                            + (e.orgKey ?? "unknown hands") + ": "
                            + meaning);
                    // a conforming act by one's own is simply order
                    // kept; it needs no record to be real
                }
            }
        }

        // Each informed pawn judges from their political beliefs, not from
        // organization-wide customs. Uninformed pawns carry no memory of
        // the event.
        private static void JudgeForPawns(CAActRecord e, int now)
        {
            for (int k = 0; k < e.knownByIds.Count; k++)
            {
                int pawnId = e.knownByIds[k];
                if (e.pawnsAnswered.Contains(pawnId)) continue;
                Pawn pawn = FindPawn(pawnId);
                if (pawn == null || pawn.needs?.mood == null)
                    continue;
                e.pawnsAnswered.Add(pawnId);

                foreach (string key in
                    CAPoliticalCustoms.CustomsHeldBy(pawn))
                {
                    Row row;
                    string meaning;
                    if (!Table.TryGetValue(key, out row)
                        || row.judge == null) continue;
                    CAVerdict verdict = row.judge(e, out meaning);
                    if (verdict == CAVerdict.Silent) continue;
                    // Judge every applicable belief. The saved faction model
                    // supplies one answer per political-belief axis, so this
                    // loop cannot judge the same axis twice.
                    GainPoliticalBeliefThought(pawn,
                        verdict == CAVerdict.Offends, meaning, key,
                        EventIdentity(key, e));
                }
            }
        }

        // Group repeated instances of the same act against the same subject.
        private static string EventIdentity(string key,
            CAActRecord e)
        {
            return key + "|" + e.act + "|" + (e.circumstance ?? "-")
                + "|" + (e.lethal ? "lethal" : "survived") + "|"
                + e.subjectPawnId + "|" + (e.orgKey ?? "-");
        }

        private static void GainPoliticalBeliefThought(Pawn pawn,
            bool violated, string meaning, string belief,
            string eventIdentity)
        {
            try
            {
                ThoughtDef def = DefDatabase<ThoughtDef>
                    .GetNamedSilentFail(violated
                        ? "CA_PoliticalBeliefViolated"
                        : "CA_PoliticalBeliefUpheld");
                if (def == null) return;
                var thought = (Thought_CAPoliticalBelief)
                    ThoughtMaker.MakeThought(def);
                thought.detail = meaning;
                thought.belief = belief;
                thought.eventIdentity = eventIdentity;
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
            }
            catch { }
        }

        private static Pawn FindPawn(int pawnId)
        {
            foreach (Map map in Find.Maps)
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                    if (p.thingIDNumber == pawnId) return p;
            return null;
        }
    }
}
