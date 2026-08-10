using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    public enum MissionCasualtyState
    {
        Unassessed,
        Assessed,
        Deceased,
        Missing,
        Resolved
    }

    public class MissionCasualtyFact : IExposable
    {
        public Pawn rescuer;
        public Pawn beneficiary;
        public IntVec3 lastKnownCell = IntVec3.Invalid;
        public int eventSourceTick = -1;
        public MissionCasualtyState state = MissionCasualtyState.Unassessed;
        public int assessedTick = -1;
        public int estimatedDeathTick = int.MaxValue;
        public int diagnosticTier;
        public bool observedNeedsTend;
        public bool observedDowned = true;
        public string lastAnnouncedDiagnosis;

        public bool Actionable
        {
            get
            {
                return state == MissionCasualtyState.Unassessed
                    || state == MissionCasualtyState.Assessed;
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref rescuer, "rescuer");
            Scribe_References.Look(ref beneficiary, "beneficiary");
            Scribe_Values.Look(ref lastKnownCell, "lastKnownCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref eventSourceTick, "eventSourceTick", -1);
            Scribe_Values.Look(ref state, "state",
                MissionCasualtyState.Unassessed);
            Scribe_Values.Look(ref assessedTick, "assessedTick", -1);
            Scribe_Values.Look(ref estimatedDeathTick, "estimatedDeathTick",
                int.MaxValue);
            Scribe_Values.Look(ref diagnosticTier, "diagnosticTier", 0);
            Scribe_Values.Look(ref observedNeedsTend, "observedNeedsTend", false);
            Scribe_Values.Look(ref observedDowned, "observedDowned", true);
            Scribe_Values.Look(ref lastAnnouncedDiagnosis,
                "lastAnnouncedDiagnosis");
        }
    }

    // The event supplies a finite manifest and each beneficiary's arrival cell.
    // Medical severity is not part of that grant: the rescuer must physically
    // assess it, and the saved estimate is all later triage may compare.
    public class MissionCasualtyKnowledgeMapComponent : MapComponent
    {
        private List<MissionCasualtyFact> facts =
            new List<MissionCasualtyFact>();

        public MissionCasualtyKnowledgeMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref facts, "CA_missionCasualtyFacts",
                LookMode.Deep);
            if (facts == null) facts = new List<MissionCasualtyFact>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                for (int i = facts.Count - 1; i >= 0; i--)
                    if (facts[i] == null || facts[i].rescuer == null
                        || facts[i].beneficiary == null) facts.RemoveAt(i);
                KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
                if (knowledge != null)
                    for (int i = 0; i < facts.Count; i++)
                        knowledge.ImportMissionWelfare(facts[i].rescuer,
                            facts[i]);
            }
        }

        public static MissionCasualtyKnowledgeMapComponent For(Map map)
        {
            return map != null
                ? map.GetComponent<MissionCasualtyKnowledgeMapComponent>() : null;
        }

        public void Seed(Pawn rescuer, List<Pawn> casualties)
        {
            if (rescuer == null || casualties == null) return;
            Complete(rescuer);
            int eventTick = Find.TickManager.TicksGame;
            for (int i = 0; i < casualties.Count; i++)
            {
                Pawn casualty = casualties[i];
                if (casualty == null || casualty == rescuer || casualty.Dead) continue;
                facts.Add(new MissionCasualtyFact
                {
                    rescuer = rescuer,
                    beneficiary = casualty,
                    lastKnownCell = casualty.Position,
                    eventSourceTick = eventTick,
                    observedDowned = true
                });
                KnowledgeMapComponent.For(map)?.NoteMissionWelfare(rescuer,
                    casualty.thingIDNumber, casualty.Position,
                    eventTick);
            }
        }

        public bool TryGetFacts(Pawn rescuer,
            List<MissionCasualtyFact> result)
        {
            result.Clear();
            if (rescuer == null) return false;
            for (int i = 0; i < facts.Count; i++)
            {
                MissionCasualtyFact fact = facts[i];
                if (fact != null && fact.rescuer == rescuer)
                    result.Add(fact);
            }
            return result.Count > 0;
        }

        public bool TryGetBeneficiaries(Pawn rescuer, List<Pawn> result)
        {
            result.Clear();
            if (rescuer == null) return false;
            for (int i = 0; i < facts.Count; i++)
            {
                MissionCasualtyFact fact = facts[i];
                if (fact != null && fact.rescuer == rescuer && fact.Actionable
                    && fact.beneficiary != null) result.Add(fact.beneficiary);
            }
            return result.Count > 0;
        }

        public bool HasBeneficiaries(Pawn rescuer)
        {
            if (rescuer == null) return false;
            for (int i = 0; i < facts.Count; i++)
                if (facts[i] != null && facts[i].rescuer == rescuer
                    && facts[i].beneficiary != null) return true;
            return false;
        }

        public MissionCasualtyFact FindFact(Pawn rescuer, Pawn beneficiary)
        {
            for (int i = 0; i < facts.Count; i++)
                if (facts[i] != null && facts[i].rescuer == rescuer
                    && facts[i].beneficiary == beneficiary) return facts[i];
            return null;
        }

        public void Complete(Pawn rescuer)
        {
            if (rescuer == null) return;
            for (int i = facts.Count - 1; i >= 0; i--)
                if (facts[i] != null && facts[i].rescuer == rescuer)
                    facts.RemoveAt(i);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_WandererJoin),
        nameof(IncidentWorker_WandererJoin.SpawnJoiner))]
    public static class Patch_CAStrangerInBlackMissionKnowledge
    {
        public static void Postfix(IncidentWorker_WandererJoin __instance,
            Map map, Pawn pawn)
        {
            if (__instance == null || __instance.def == null
                || __instance.def.defName != "StrangerInBlackJoin"
                || map == null || pawn == null || pawn.Dead) return;

            var casualties = new List<Pawn>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn casualty = colonists[i];
                if (casualty == pawn || casualty.Dead || !casualty.Downed
                    || casualty.RaceProps == null
                    || !casualty.RaceProps.Humanlike) continue;
                casualties.Add(casualty);
            }

            MissionCasualtyKnowledgeMapComponent mission =
                MissionCasualtyKnowledgeMapComponent.For(map);
            if (mission == null || casualties.Count == 0) return;
            mission.Seed(pawn, casualties);
            CATrace.Log("mission knowledge: " + pawn.LabelShort
                + " arrived to save " + casualties.Count + " downed colonist"
                + (casualties.Count == 1 ? "" : "s"));
        }
    }

    internal readonly struct CATriageCandidate
    {
        public readonly Pawn Patient;
        public readonly bool IsSelf;
        public readonly int BleedDeathTicks;
        public readonly bool UrgentTend;
        public readonly bool NeedsTend;
        public readonly bool Downed;
        public readonly MissionCasualtyFact Fact;

        public CATriageCandidate(Pawn patient, bool isSelf,
            int perceivedBleedDeathTicks, bool needsTend, bool downed,
            MissionCasualtyFact fact = null)
        {
            Patient = patient;
            IsSelf = isSelf;
            BleedDeathTicks = perceivedBleedDeathTicks;
            UrgentTend = perceivedBleedDeathTicks <= 45000;
            NeedsTend = needsTend;
            Downed = downed;
            Fact = fact;
        }
    }

    internal readonly struct CATriageAssessment
    {
        public readonly bool HasMission;
        public readonly bool ActiveDanger;
        public readonly Pawn PriorityPatient;
        public readonly int SelfBleedDeathTicks;
        public readonly int PriorityBleedDeathTicks;
        public readonly string Decision;

        public CATriageAssessment(bool hasMission, bool activeDanger,
            Pawn priorityPatient, int selfBleedDeathTicks,
            int priorityBleedDeathTicks, string decision)
        {
            HasMission = hasMission;
            ActiveDanger = activeDanger;
            PriorityPatient = priorityPatient;
            SelfBleedDeathTicks = selfBleedDeathTicks;
            PriorityBleedDeathTicks = priorityBleedDeathTicks;
            Decision = decision;
        }
    }

    // This lane sits ahead of combat posture but yields to perceived active danger.
    // It compares only the rescuer and the incident's saved beneficiary identities.
    public class JobGiver_CAMissionTriage : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CATriageAssessment assessment;
            return CAMissionTriage.TryGiveJob(pawn, out assessment);
        }
    }

    internal static class CAMissionTriage
    {
        private static readonly List<MissionCasualtyFact> factBuffer =
            new List<MissionCasualtyFact>();
        private static readonly List<CATriageCandidate> candidateBuffer =
            new List<CATriageCandidate>();

        internal static Job TryGiveJob(Pawn pawn,
            out CATriageAssessment assessment,
            bool allowDraftedIdle = false,
            bool currentOwnedByDraftedResponse = false)
        {
            assessment = new CATriageAssessment(false, false, null,
                int.MaxValue, int.MaxValue, "not a mission rescuer");
            AwarenessSettings settings = AwarenessMod.Settings;
            if (pawn == null || !pawn.IsColonistPlayerControlled
                || !pawn.Spawned || pawn.Dead || pawn.Downed
                || pawn.InMentalState || !pawn.Awake() || pawn.jobs == null)
                return null;
            if (pawn.CurJob != null && pawn.CurJob.playerForced) return null;
            if (pawn.jobs.jobQueue != null
                && pawn.jobs.jobQueue.AnyPlayerForced) return null;

            // Release is ownership cleanup, not acquisition. Once this component
            // started an exact drafted casualty/welfare job, renewed actor-local
            // danger may cancel it even if Field Medicine, autonomy, Caring,
            // manipulation, mission facts, or combat-response settings changed.
            // Facts are consulted only to preserve an interrupted carry location.
            if (currentOwnedByDraftedResponse
                && CACombatThreat.PerceivesActiveThreat(pawn))
            {
                int ownedSelfTicks = PerceivedBleedDeathTicks(pawn, pawn);
                assessment = new CATriageAssessment(true, true, null,
                    ownedSelfTicks, int.MaxValue, "active danger first");
                MissionCasualtyKnowledgeMapComponent ownedMission =
                    MissionCasualtyKnowledgeMapComponent.For(pawn.Map);
                if (ownedMission != null
                    && ownedMission.TryGetFacts(pawn, factBuffer)
                    && IsMissionRelevantTriageJob(
                        pawn, pawn.CurJob, factBuffer))
                    RecordInterruptedCarryLocation(pawn, pawn.CurJob,
                        factBuffer);
                Job ownedRelease = JobMaker.MakeJob(JobDefOf.Wait);
                ownedRelease.expiryInterval = 31;
                ownedRelease.checkOverrideOnExpire = true;
                return ownedRelease;
            }

            if (settings == null || !settings.fieldMedicine
                || pawn.Drafted && !allowDraftedIdle
                || pawn.WorkTagIsDisabled(WorkTags.Caring)
                || pawn.health == null || pawn.health.capacities == null
                || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                || AutonomyComponent.LevelOf(pawn) < 2)
                return null;
            WithdrawalMapComponent withdrawal =
                pawn.Map.GetComponent<WithdrawalMapComponent>();
            if (withdrawal != null && withdrawal.InPlan(pawn)) return null;
            Lord lord = pawn.GetLord();
            if (lord != null && lord.LordJob is LordJob_CAStackBreach) return null;
            if (HiddenRegistry.IsHiddenOrOrdered(pawn)
                || CATactical.HasExplicitOrder(pawn)) return null;

            MissionCasualtyKnowledgeMapComponent mission =
                MissionCasualtyKnowledgeMapComponent.For(pawn.Map);
            if (mission == null
                || !mission.TryGetFacts(pawn, factBuffer))
                return null;

            int selfTicks = PerceivedBleedDeathTicks(pawn, pawn);
            bool danger = PerceivesActiveDanger(pawn, settings);
            if (danger)
            {
                assessment = new CATriageAssessment(true, true, null,
                    selfTicks,
                    int.MaxValue, "active danger first");
                // With combat/survival response disabled, the following CA combat
                // giver may intentionally return null. Still cancel a triage job
                // that began before this danger was perceived. Keep the release
                // job alive through the next 30-tick constant-think pulse so this
                // giver can yield and the following combat/vanilla nodes can act.
                if (pawn.CurJob != null
                    && (currentOwnedByDraftedResponse
                        || IsMissionRelevantTriageJob(
                            pawn, pawn.CurJob, factBuffer)))
                {
                    RecordInterruptedCarryLocation(pawn, pawn.CurJob,
                        factBuffer);
                    Job release = JobMaker.MakeJob(JobDefOf.Wait);
                    release.expiryInterval = 31;
                    release.checkOverrideOnExpire = true;
                    return release;
                }
                return null;
            }

            // Preserve this giver's finite treatment/carry job as one decision.
            // A newly perceived active danger above still releases it so the next
            // constant-think pulse can reach the immediate-combat giver.
            if (pawn.CurJob != null
                && pawn.CurJob.jobGiver is JobGiver_CAMissionTriage
                && IsProtectedTriageJob(pawn.CurJob)) return pawn.CurJob;

            BuildCandidates(pawn, factBuffer, candidateBuffer);
            candidateBuffer.Sort(CompareUrgency);

            // Once an honestly assessed patient is in the native urgent-tend
            // window, stabilize them before spending time examining another.
            for (int i = 0; i < candidateBuffer.Count; i++)
            {
                CATriageCandidate candidate = candidateBuffer[i];
                if (!candidate.UrgentTend) break;
                Job urgent = CandidateJob(pawn, candidate);
                if (urgent != null)
                    return ReportCandidateJob(pawn, candidate, urgent,
                        selfTicks, out assessment);
            }

            MissionCasualtyFact unknown = NearestUnassessedFact(pawn, factBuffer);
            MissionCasualtyFact anyUnknown = unknown
                ?? NearestUnassessedFact(pawn, factBuffer,
                    requireReachable: false);
            if (unknown != null)
            {
                Job examine = AssessmentJob(pawn, unknown);
                if (examine != null)
                {
                    assessment = new CATriageAssessment(true, false,
                        unknown.beneficiary, selfTicks, int.MaxValue,
                        "locate and assess mission casualty");
                    CATrace.Pawn(pawn, "locate and assess "
                        + unknown.beneficiary.LabelShort + " at "
                        + unknown.lastKnownCell);
                    return examine;
                }
            }

            for (int i = 0; i < candidateBuffer.Count; i++)
            {
                CATriageCandidate candidate = candidateBuffer[i];
                Job job = CandidateJob(pawn, candidate);
                if (job == null) continue;
                return ReportCandidateJob(pawn, candidate, job,
                    selfTicks, out assessment);
            }

            if (candidateBuffer.Count == 0 && unknown == null)
            {
                if (anyUnknown != null)
                {
                    assessment = new CATriageAssessment(true, false,
                        anyUnknown.beneficiary, selfTicks, int.MaxValue,
                        "mission casualty location unreachable");
                    return null;
                }
                mission.Complete(pawn);
                assessment = new CATriageAssessment(true, false, null,
                    selfTicks, int.MaxValue, "mission casualties stable or resolved");
                return null;
            }

            CATriageCandidate priority = candidateBuffer.Count > 0
                ? candidateBuffer[0] : default(CATriageCandidate);
            assessment = new CATriageAssessment(true, false,
                priority.Patient ?? unknown?.beneficiary, selfTicks,
                priority.Patient != null ? priority.BleedDeathTicks : int.MaxValue,
                anyUnknown != null ? "mission casualty location unreachable"
                    : "assessed casualty currently unreachable");
            return null;
        }

        internal static bool TryAssess(Pawn pawn, out CATriageAssessment assessment)
        {
            assessment = new CATriageAssessment(false, false, null,
                int.MaxValue, int.MaxValue, "not a mission rescuer");
            if (pawn == null || pawn.Map == null) return false;
            MissionCasualtyKnowledgeMapComponent mission =
                MissionCasualtyKnowledgeMapComponent.For(pawn.Map);
            if (mission == null
                || !mission.TryGetFacts(pawn, factBuffer))
                return false;

            int selfTicks = PerceivedBleedDeathTicks(pawn, pawn);
            AwarenessSettings settings = AwarenessMod.Settings;
            bool danger = settings != null && PerceivesActiveDanger(pawn, settings);
            if (danger)
            {
                assessment = new CATriageAssessment(true, true, null,
                    selfTicks, int.MaxValue, "active danger first");
                return true;
            }

            BuildCandidates(pawn, factBuffer, candidateBuffer);
            MissionCasualtyFact unknown = NearestUnassessedFact(
                pawn, factBuffer, requireReachable: false);
            if (candidateBuffer.Count == 0)
            {
                assessment = new CATriageAssessment(true, false, null,
                    selfTicks, int.MaxValue, unknown != null
                        ? "unassessed mission casualty"
                        : "mission casualties stable or resolved");
                return true;
            }
            candidateBuffer.Sort(CompareUrgency);
            CATriageCandidate priority = candidateBuffer[0];
            string decision;
            if (!priority.UrgentTend && unknown != null)
                decision = "unassessed mission casualty before nonurgent care";
            else if (priority.IsSelf) decision = "clinical priority: self";
            else if (priority.NeedsTend)
                decision = "clinical priority: stabilize mission casualty";
            else decision = "clinical priority: rescue mission casualty";
            assessment = new CATriageAssessment(true, false, priority.Patient,
                selfTicks, priority.BleedDeathTicks, decision);
            return true;
        }

        private static void BuildCandidates(Pawn rescuer,
            List<MissionCasualtyFact> facts, List<CATriageCandidate> result)
        {
            result.Clear();
            if (HealthAIUtility.ShouldBeTendedNowByPlayer(rescuer)
                && rescuer.health.HasHediffsNeedingTend())
                result.Add(new CATriageCandidate(rescuer, true,
                    PerceivedBleedDeathTicks(rescuer, rescuer), true,
                    rescuer.Downed));

            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < facts.Count; i++)
            {
                MissionCasualtyFact fact = facts[i];
                if (fact == null || fact.state != MissionCasualtyState.Assessed
                    || fact.beneficiary == null
                    || (!fact.observedNeedsTend && !fact.observedDowned)) continue;
                int remaining = fact.estimatedDeathTick == int.MaxValue
                    ? int.MaxValue : Mathf.Max(0, fact.estimatedDeathTick - now);
                result.Add(new CATriageCandidate(fact.beneficiary, false,
                    remaining, fact.observedNeedsTend,
                    fact.observedDowned, fact));
            }
        }

        private static int CompareUrgency(CATriageCandidate a,
            CATriageCandidate b)
        {
            int compare = a.BleedDeathTicks.CompareTo(b.BleedDeathTicks);
            if (compare != 0) return compare;
            compare = b.UrgentTend.CompareTo(a.UrgentTend);
            if (compare != 0) return compare;
            compare = b.Downed.CompareTo(a.Downed);
            if (compare != 0) return compare;
            // Once observable medical evidence ties, the incident's beneficiary
            // comes before self; no hidden health percentage breaks the tie.
            compare = a.IsSelf.CompareTo(b.IsSelf);
            if (compare != 0) return compare;
            return a.Patient.thingIDNumber.CompareTo(b.Patient.thingIDNumber);
        }

        private static Job CandidateJob(Pawn rescuer,
            CATriageCandidate candidate)
        {
            return candidate.IsSelf
                ? TrySelfTendJob(rescuer, candidate)
                : TryBeneficiaryJob(rescuer, candidate);
        }

        private static Job ReportCandidateJob(Pawn rescuer,
            CATriageCandidate candidate, Job job, int selfTicks,
            out CATriageAssessment assessment)
        {
            string decision = candidate.IsSelf ? "stabilize self"
                : job.def == JobDefOf.Rescue ? "rescue mission casualty"
                : "stabilize mission casualty";
            assessment = new CATriageAssessment(true, false,
                candidate.Patient, selfTicks, candidate.BleedDeathTicks,
                decision);
            CATrace.Pawn(rescuer, decision + " " + candidate.Patient.LabelShort
                + " (assessed bleed death "
                + TickLabel(candidate.BleedDeathTicks) + "; self estimate "
                + TickLabel(selfTicks) + ")");
            return job;
        }

        private static MissionCasualtyFact NearestUnassessedFact(Pawn rescuer,
            List<MissionCasualtyFact> facts, bool requireReachable = true)
        {
            MissionCasualtyFact best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < facts.Count; i++)
            {
                MissionCasualtyFact fact = facts[i];
                if (fact == null || fact.state != MissionCasualtyState.Unassessed
                    || fact.beneficiary == null || !fact.lastKnownCell.IsValid
                    || !fact.lastKnownCell.InBounds(rescuer.Map)) continue;
                if (requireReachable
                    && (fact.lastKnownCell.IsForbidden(rescuer)
                        || !rescuer.CanReach(fact.lastKnownCell,
                            PathEndMode.Touch, Danger.Deadly))) continue;
                float distance = rescuer.Position.DistanceToSquared(
                    fact.lastKnownCell);
                if (best == null || distance < bestDistance
                    || (Mathf.Approximately(distance, bestDistance)
                        && fact.beneficiary.thingIDNumber
                            < best.beneficiary.thingIDNumber))
                {
                    best = fact;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static Job AssessmentJob(Pawn rescuer,
            MissionCasualtyFact fact)
        {
            if (fact == null || fact.beneficiary == null
                || !fact.lastKnownCell.IsValid
                || !fact.lastKnownCell.InBounds(rescuer.Map)
                || fact.lastKnownCell.IsForbidden(rescuer)
                || !rescuer.CanReach(fact.lastKnownCell,
                    PathEndMode.Touch, Danger.Deadly)) return null;
            Job job = JobMaker.MakeJob(CA_Defs.AssessCasualty,
                fact.lastKnownCell, fact.beneficiary);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }

        internal static int AssessmentDuration(Pawn rescuer)
        {
            int medicine = SkillLevel(rescuer, SkillDefOf.Medicine);
            int intellectual = SkillLevel(rescuer, SkillDefOf.Intellectual);
            return Mathf.Clamp(120 - medicine * 4 - intellectual * 2, 30, 120);
        }

        internal static void CompleteAssessmentJob(Pawn rescuer,
            Pawn beneficiary, IntVec3 expectedCell)
        {
            if (rescuer == null || rescuer.Map == null || beneficiary == null)
                return;
            MissionCasualtyKnowledgeMapComponent mission =
                MissionCasualtyKnowledgeMapComponent.For(rescuer.Map);
            MissionCasualtyFact fact = mission?.FindFact(rescuer, beneficiary);
            if (fact == null || !fact.Actionable) return;

            Thing visible;
            if (CanClinicallyObserve(rescuer, beneficiary, out visible))
            {
                fact.lastKnownCell = visible.Position;
                RecordClinicalAssessment(rescuer, fact, visible,
                    announce: true);
                return;
            }

            fact.state = MissionCasualtyState.Missing;
            KnowledgeMapComponent.For(rescuer.Map)?.MarkMissionWelfareMissing(
                rescuer, beneficiary.thingIDNumber, fact.eventSourceTick,
                expectedCell);
            ShowAssessment(rescuer, fact, null,
                "not found at last-known position", Color.white,
                expectedCell);
        }

        private static void RecordClinicalAssessment(Pawn rescuer,
            MissionCasualtyFact fact, Thing visible, bool announce)
        {
            Pawn patient = fact.beneficiary;
            int now = Find.TickManager.TicksGame;
            fact.assessedTick = now;
            fact.diagnosticTier = DiagnosticTier(rescuer);
            KnowledgeMapComponent.For(rescuer.Map)?.NoteClinicalWelfare(
                rescuer, patient, visible, announce: false);

            if (patient.Dead)
            {
                fact.state = MissionCasualtyState.Deceased;
                fact.observedNeedsTend = false;
                fact.observedDowned = true;
                fact.estimatedDeathTick = int.MaxValue;
                if (announce) ShowAssessment(rescuer, fact, visible,
                    "deceased", Color.gray);
                return;
            }

            fact.observedNeedsTend = patient.health != null
                && HealthAIUtility.ShouldBeTendedNowByPlayer(patient)
                && patient.health.HasHediffsNeedingTend();
            fact.observedDowned = patient.Downed;
            if (!fact.observedNeedsTend && !fact.observedDowned)
            {
                fact.state = MissionCasualtyState.Resolved;
                fact.estimatedDeathTick = int.MaxValue;
                if (announce) ShowAssessment(rescuer, fact, visible,
                    "no immediate aid apparent", Color.white);
                return;
            }

            fact.state = MissionCasualtyState.Assessed;
            int estimated = PerceivedBleedDeathTicks(rescuer, patient);
            fact.estimatedDeathTick = estimated == int.MaxValue
                ? int.MaxValue : (int)System.Math.Min((long)int.MaxValue - 1L,
                    (long)now + estimated);
            if (announce) ShowAssessment(rescuer, fact, visible,
                DiagnosisLabel(fact, estimated), DiagnosisColor(estimated));
        }

        private static bool CanClinicallyObserve(Pawn observer, Pawn patient,
            out Thing visible)
        {
            return CAClinicalObservation.CanClinicallyObserve(observer,
                patient, out visible);
        }

        private static int PerceivedBleedDeathTicks(Pawn assessor, Pawn patient)
        {
            return CAClinicalObservation.PerceivedBleedDeathTicks(assessor,
                patient);
        }

        private static int DiagnosticTier(Pawn pawn)
        {
            return CAClinicalObservation.DiagnosticTier(pawn);
        }

        private static int SkillLevel(Pawn pawn, SkillDef skill)
        {
            return CAClinicalObservation.SkillLevel(pawn, skill);
        }

        private static string DiagnosisLabel(MissionCasualtyFact fact,
            int estimated)
        {
            if (estimated == int.MaxValue)
                return fact.observedDowned
                    ? "downed; no bleed-out apparent"
                    : "no bleed-out apparent";
            if (fact.diagnosticTier == 0)
                return estimated <= 45000
                    ? "heavy bleeding; exact severity unclear"
                    : "bleeding; exact severity unclear";
            if (estimated <= 7500) return "immediate bleed-out risk";
            if (estimated <= 15000) return "critical bleeding";
            if (estimated <= 45000) return "serious bleeding";
            return "bleeding; not immediately critical";
        }

        private static Color DiagnosisColor(int estimated)
        {
            if (estimated <= 15000) return Color.red;
            if (estimated <= 45000) return new Color(1f, 0.65f, 0.2f);
            return Color.white;
        }

        private static void ShowAssessment(Pawn rescuer,
            MissionCasualtyFact fact, Thing visible, string diagnosis,
            Color color, IntVec3 fallback = default(IntVec3))
        {
            Map map = rescuer.Map;
            IntVec3 cell = visible != null ? visible.Position
                : fallback.IsValid ? fallback : fact.lastKnownCell;
            if (cell.IsValid && cell.InBounds(map))
                MoteMaker.ThrowText(cell.ToVector3Shifted(), map,
                    diagnosis, color, 3.5f);
            if (fact.lastAnnouncedDiagnosis != diagnosis)
            {
                fact.lastAnnouncedDiagnosis = diagnosis;
                Messages.Message(rescuer.LabelShort + " assesses "
                    + fact.beneficiary.LabelShort + ": " + diagnosis + ".",
                    new LookTargets(cell, map), MessageTypeDefOf.NeutralEvent,
                    historical: false);
            }
            CATrace.Pawn(rescuer, "assesses " + fact.beneficiary.LabelShort
                + ": " + diagnosis + " (Medical "
                + SkillLevel(rescuer, SkillDefOf.Medicine)
                + ", Intellectual "
                + SkillLevel(rescuer, SkillDefOf.Intellectual) + ")");
        }

        private static Job TrySelfTendJob(Pawn rescuer,
            CATriageCandidate candidate)
        {
            return candidate.NeedsTend
                ? MakeEmergencySelfTendJob(rescuer) : null;
        }

        // Recovery may use the same finite self-stabilization action after the
        // actor-local fight has gone quiet. This is an acquisition gate, not a
        // global self-tend policy change, and direct player work is excluded by the
        // recovery caller before this method is reached.
        internal static Job TryEmergencySelfTendJob(Pawn rescuer)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.fieldMedicine
                || rescuer == null || rescuer.Map == null || rescuer.health == null
                || rescuer.Dead || rescuer.Downed || !rescuer.Awake()
                || rescuer.InMentalState
                || AutonomyComponent.LevelOf(rescuer) < 2
                || rescuer.WorkTagIsDisabled(WorkTags.Caring)
                || rescuer.health.capacities == null
                || !rescuer.health.capacities.CapableOf(
                    PawnCapacityDefOf.Manipulation)
                || !HealthAIUtility.ShouldBeTendedNowByPlayer(rescuer)
                || !rescuer.health.HasHediffsNeedingTend()) return null;
            return MakeEmergencySelfTendJob(rescuer);
        }

        private static Job MakeEmergencySelfTendJob(Pawn rescuer)
        {
            if (rescuer?.Map == null
                || !rescuer.Map.reservationManager.CanReserve(rescuer, rescuer))
                return null;
            Thing medicine = HealthAIUtility.FindBestMedicine(rescuer,
                rescuer, onlyUseInventory: true);
            Job job = medicine != null
                ? JobMaker.MakeJob(CA_Defs.EmergencySelfTend, rescuer, medicine)
                : JobMaker.MakeJob(CA_Defs.EmergencySelfTend, rescuer);
            job.count = 1;
            return job;
        }

        private static Job TryBeneficiaryJob(Pawn rescuer,
            CATriageCandidate candidate)
        {
            Pawn patient = candidate.Patient;
            if (patient == null || candidate.Fact == null) return null;
            Thing visible;
            if (!CanClinicallyObserve(rescuer, patient, out visible))
            {
                // The saved diagnosis does not confer live tracking. Revisit the
                // last honestly observed cell before targeting this pawn again.
                candidate.Fact.state = MissionCasualtyState.Unassessed;
                return null;
            }
            candidate.Fact.lastKnownCell = visible.Position;
            if (patient.Dead)
            {
                candidate.Fact.state = MissionCasualtyState.Deceased;
                return null;
            }
            bool currentNeedsTend = patient.health != null
                && HealthAIUtility.ShouldBeTendedNowByPlayer(patient)
                && patient.health.HasHediffsNeedingTend();
            bool currentDowned = patient.Downed;
            if (currentNeedsTend != candidate.NeedsTend
                || currentDowned != candidate.Downed)
            {
                candidate.Fact.state = MissionCasualtyState.Unassessed;
                return null;
            }
            candidate.Fact.observedNeedsTend = currentNeedsTend;
            candidate.Fact.observedDowned = currentDowned;
            if (!candidate.Fact.observedNeedsTend
                && (!candidate.Fact.observedDowned || patient.InBed()))
            {
                candidate.Fact.state = MissionCasualtyState.Resolved;
                return null;
            }

            bool acute = candidate.UrgentTend
                || candidate.BleedDeathTicks != int.MaxValue;
            if (candidate.NeedsTend && acute)
            {
                Job tend = TendJob(rescuer, patient);
                if (tend != null)
                {
                    // One native tend can materially change bleeding. Require a
                    // new visible examination before making the next comparison.
                    candidate.Fact.state = MissionCasualtyState.Unassessed;
                    return tend;
                }
            }

            if (candidate.Downed && !patient.InBed()
                && patient.Faction == rescuer.Faction
                && HealthAIUtility.WantsToBeRescued(patient)
                && !patient.IsForbidden(rescuer))
            {
                Building_Bed bed = RestUtility.FindBedFor(patient, rescuer,
                    checkSocialProperness: false, ignoreOtherReservations: false,
                    patient.GuestStatus);
                if (bed != null && patient.CanReserve(bed)
                    && rescuer.CanReserveAndReach(patient, PathEndMode.OnCell,
                        Danger.Deadly))
                {
                    Job rescue = JobMaker.MakeJob(JobDefOf.Rescue, patient, bed);
                    rescue.count = 1;
                    return rescue;
                }
            }

            if (!candidate.NeedsTend) return null;
            Job fallback = TendJob(rescuer, patient);
            if (fallback != null)
                candidate.Fact.state = MissionCasualtyState.Unassessed;
            return fallback;
        }

        private static Job TendJob(Pawn doctor, Pawn patient)
        {
            bool carriedByDoctor = doctor.IsCarryingPawn(patient);
            if (!carriedByDoctor && (patient.IsForbidden(doctor)
                || !doctor.CanReserveAndReach(patient, PathEndMode.ClosestTouch,
                    Danger.Deadly))) return null;
            if (patient.InAggroMentalState
                && !patient.health.hediffSet.HasHediff(HediffDefOf.Scaria))
                return null;
            Thing medicine = HealthAIUtility.FindBestMedicine(doctor, patient,
                onlyUseInventory: true);
            Job tend = medicine != null
                ? JobMaker.MakeJob(JobDefOf.TendPatient, patient, medicine)
                : JobMaker.MakeJob(JobDefOf.TendPatient, patient);
            tend.count = 1;
            tend.endAfterTendedOnce = true;
            return tend;
        }

        private static bool PerceivesActiveDanger(Pawn pawn,
            AwarenessSettings settings)
        {
            return CACombatThreat.PerceivesActiveThreat(pawn);
        }

        private static bool IsProtectedTriageJob(Job job)
        {
            return job != null && (job.def == JobDefOf.TendPatient
                || job.def == JobDefOf.Rescue
                || job.def == CA_Defs.AssessCasualty
                || job.def == CA_Defs.EmergencySelfTend);
        }

        private static bool IsMissionRelevantTriageJob(Pawn rescuer, Job job,
            List<MissionCasualtyFact> facts)
        {
            if (!IsProtectedTriageJob(job)) return false;
            if (job.jobGiver is JobGiver_CAMissionTriage) return true;
            Pawn patient = job.targetA.Pawn;
            if (patient == rescuer && job.def == CA_Defs.EmergencySelfTend)
                return true;
            for (int i = 0; i < facts.Count; i++)
                if (facts[i] != null && facts[i].beneficiary == patient)
                    return true;
            return false;
        }

        private static void RecordInterruptedCarryLocation(Pawn rescuer, Job job,
            List<MissionCasualtyFact> facts)
        {
            if (rescuer == null || job == null || job.def != JobDefOf.Rescue)
                return;
            Pawn carried = job.targetA.Pawn;
            if (carried == null || !rescuer.IsCarryingPawn(carried)) return;
            for (int i = 0; i < facts.Count; i++)
            {
                MissionCasualtyFact fact = facts[i];
                if (fact == null || fact.beneficiary != carried) continue;
                // JobDriver_TakeToBed drops the carried pawn at this cell when
                // replacement ends Rescue. Carrying is direct physical knowledge,
                // so preserve the impending drop location before combat moves on.
                fact.lastKnownCell = rescuer.Position;
                return;
            }
        }

        private static string TickLabel(int ticks)
        {
            return ticks == int.MaxValue ? "not projected"
                : ticks.ToString() + " ticks";
        }
    }

    public class JobDriver_CAAssessCasualty : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Do not reserve the patient: this observation must not block a
            // concurrent rescue or treatment job.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(delegate
            {
                return pawn == null || pawn.Map == null
                    || !job.targetA.Cell.IsValid
                    || !job.targetA.Cell.InBounds(pawn.Map)
                    || job.targetA.Cell.IsForbidden(pawn);
            });

            if (pawn.Position != job.targetA.Cell)
                yield return Toils_Goto.GotoCell(TargetIndex.A,
                    PathEndMode.Touch);

            Toil examine = Toils_General.Wait(
                CAMissionTriage.AssessmentDuration(pawn));
            examine.WithProgressBarToilDelay(TargetIndex.A);
            examine.activeSkill = () => SkillDefOf.Medicine;
            examine.handlingFacing = true;
            examine.tickAction = delegate
            {
                pawn.rotationTracker.FaceCell(job.targetA.Cell);
            };
            yield return examine;

            Toil record = ToilMaker.MakeToil("CARecordCasualtyAssessment");
            record.initAction = delegate
            {
                CAMissionTriage.CompleteAssessmentJob(pawn,
                    job.targetB.Pawn, job.targetA.Cell);
            };
            record.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return record;
        }
    }

    // Native TendPatient refuses a player self-job while the global self-tend
    // toggle is off. This one finite casualty/recovery-owned action leaves that
    // policy untouched and uses the same native tend calculation and medicine
    // semantics.
    public class JobDriver_CAEmergencySelfTend : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(pawn, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(delegate
            {
                return pawn != null && !pawn.Dead && pawn.health != null
                    && HealthAIUtility.ShouldBeTendedNowByPlayer(pawn)
                    && pawn.health.HasHediffsNeedingTend()
                    ? JobCondition.Ongoing : JobCondition.Succeeded;
            });
            this.FailOnAggroMentalState(TargetIndex.A);
            this.FailOn(delegate
            {
                if (pawn.health == null || pawn.health.capacities == null
                    || !pawn.health.capacities.CapableOf(
                        PawnCapacityDefOf.Manipulation)) return true;
                Thing medicine = job.targetB.Thing;
                if (medicine == null) return false;
                if (medicine.Destroyed || pawn.inventory == null
                    || !pawn.inventory.Contains(medicine)) return true;
                return pawn.playerSettings != null
                    && !pawn.playerSettings.medCare.AllowsMedicine(medicine.def);
            });

            int ticks = Mathf.RoundToInt(600f
                / pawn.GetStatValue(StatDefOf.MedicalTendSpeed));
            Toil wait = Toils_General.Wait(ticks);
            wait.WithProgressBarToilDelay(TargetIndex.A)
                .PlaySustainerOrSound(SoundDefOf.Interact_Tend);
            wait.activeSkill = () => SkillDefOf.Medicine;
            wait.handlingFacing = true;
            yield return wait;
            yield return Toils_Tend.FinalizeTend(pawn);
        }
    }
}
