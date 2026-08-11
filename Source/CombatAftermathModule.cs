using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // The first field-custody stage after a local matchup resolves. Friendly
    // casualties remain prior; one fighter secures the downed hostile while an
    // armed peer retains local security. Transport, formal prisoner housing, and
    // treatment remain later outcomes rather than being faked through Capture's
    // prisoner-bed prerequisite.
    internal static class CACombatAftermath
    {
        private const float LocalOutcomeRadius = 36f;
        private const float BuddyRadius = 15.9f;

        internal static Job TryGiveJob(Pawn actor, bool allowDraftedIdle)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (actor == null || settings == null
                || !actor.IsColonistPlayerControlled || !actor.Spawned
                || actor.Dead || actor.Downed || !actor.Awake()
                || actor.InMentalState || actor.jobs == null
                || actor.Drafted && !allowDraftedIdle
                || !CABehaviorGate.StableProfileAllows(actor,
                    "aftermath.secure_hostile")
                || CAImmediateCombat.RequiresCombatRecoveryNow(actor)
                || HiddenRegistry.IsHiddenOrOrdered(actor)) return null;
            bool proactiveHold = CATactical.IsHold(actor)
                && !CATactical.HasForeignPlayerForcedJob(actor);
            if (CATactical.HasForeignPlayerForcedJob(actor)
                || CATactical.HasExplicitOrder(actor) && !proactiveHold)
                return null;
            if (actor.CurJob != null && actor.CurJob.def == CA_Defs.SecureEPW)
                return actor.CurJob;

            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(actor.Map);
            if (knowledge == null || HasPriorityFriendlyCasualty(actor,
                    knowledge)) return null;
            List<ThreatContactSnapshot> remembered =
                knowledge.RememberedContacts(actor);
            Pawn best = null;
            Pawn bestBuddy = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < remembered.Count; i++)
            {
                ThreatContactSnapshot contact = remembered[i];
                if (contact.State != ThreatContactState.Downed) continue;
                Pawn target;
                if (!knowledge.TryFindPawnById(contact.HostileId, out target)
                    || target == null || target.Dead || !target.Downed
                    || !target.Spawned || target.Map != actor.Map
                    || target.guest == null || target.IsPrisonerOfColony
                    || !target.HostileTo(actor)
                    || target.IsForbidden(actor)
                    || actor.Position.DistanceTo(target.Position)
                        > LocalOutcomeRadius
                    || !actor.CanReserveAndReach(target,
                        PathEndMode.ClosestTouch, Danger.Deadly)) continue;
                CAVisualPerception perception;
                if (!CABattlefieldPerception.TryObserveVisibleThing(actor,
                        target, target, LocalOutcomeRadius, out perception))
                    continue;
                if (HasUnresolvedLocalThreat(actor, target.Position,
                        knowledge)) continue;
                Pawn buddy = SecurityBuddy(actor, target);
                if (buddy == null || !ActorOwnsSecureStep(actor, target, buddy))
                    continue;
                if (RecentlyAbandonedBy(actor, target)) continue;
                float distance = actor.Position.DistanceTo(target.Position);
                if (best == null || distance < bestDistance)
                {
                    best = target;
                    bestBuddy = buddy;
                    bestDistance = distance;
                }
            }
            if (best == null) return null;
            CABehaviorContext secureContext = CABehaviorContext.ForPawn(actor,
                CAAuthorityOrigin.PlayerDelegated,
                authoritySatisfied: true,
                knowledgeSatisfied: true, knowledgeFresh: true,
                liveValidated: best.Spawned && best.Downed
                    && GenSight.LineOfSight(actor.Position, best.Position,
                        actor.Map, true),
                capabilitySatisfied: bestBuddy != null,
                currentIntentCompatible:
                    !CATactical.HasForeignPlayerForcedJob(actor),
                directPlayerOwnership:
                    CATactical.HasForeignPlayerForcedJob(actor),
                authorityBasis: "delegated local field security",
                knowledgeBasis: "personally confirmed downed hostile",
                owner: "field custody intake");
            CABehaviorDecision secureDecision = CABehaviorGate.Evaluate(
                "aftermath.secure_hostile", secureContext);
            if (!secureDecision.Allowed)
            {
                CATrace.Pawn(actor, "field custody BLOCKED - "
                    + secureDecision.PrimaryReason,
                    contact: best.Position, anchor: actor.Position);
                return null;
            }
            // Custody has character: who this pawn IS decides what "handling
            // the enemy" means before the job is even taken. A cold pawn that
            // walks away leaves the captive for someone warmer - abandonment
            // is per-actor, never per-captive.
            string custodyTrace;
            CACustodyIntent intent = ResolveCustodyIntent(actor, best,
                out custodyTrace);
            string behaviorKey = "aftermath.secure_hostile";
            string authorityIdentity = "delegated local field security";
            CAAuthorityOrigin authorityOrigin =
                CAAuthorityOrigin.PlayerDelegated;
            bool irreversible = intent == CACustodyIntent.Abandon
                || intent == CACustodyIntent.Execute
                || intent == CACustodyIntent.ImpulsiveExecute
                || intent == CACustodyIntent.MercyKill;
            if (irreversible)
            {
                if (intent == CACustodyIntent.ImpulsiveExecute)
                {
                    behaviorKey = "aftermath.unlawful_execution_breach";
                    authorityIdentity = "unlawful character breach; no "
                        + "institutional legitimacy";
                    CABehaviorDecision breachDecision = CABehaviorGate
                        .Evaluate(behaviorKey, CABehaviorContext.ForPawn(actor,
                            CAAuthorityOrigin.PlayerDelegated,
                            authoritySatisfied: true,
                            knowledgeSatisfied: true, knowledgeFresh: true,
                            liveValidated: best.Spawned && best.Downed,
                            capabilitySatisfied: true,
                            authorityBasis: authorityIdentity,
                            knowledgeBasis:
                                "personally confirmed captive at execution",
                            owner: "character accountability"));
                    if (!breachDecision.Allowed)
                    {
                        custodyTrace += "; unlawful breach blocked: "
                            + breachDecision.PrimaryReason;
                        intent = CACustodyIntent.Secure;
                        behaviorKey = "aftermath.secure_hostile";
                        authorityIdentity =
                            "delegated local field security";
                    }
                    else
                        custodyTrace = "UNLAWFUL CHARACTER BREACH; "
                            + custodyTrace;
                }
                else
                {
                    string authorityBasis;
                    bool authority = HasIrreversibleCustodyAuthority(actor,
                        intent, out authorityBasis);
                    CABehaviorDecision resolutionDecision = CABehaviorGate
                        .Evaluate("aftermath.custody_resolution",
                            CABehaviorContext.ForPawn(actor,
                                CAAuthorityOrigin.PlayerDelegated,
                                authoritySatisfied: authority,
                                knowledgeSatisfied: true,
                                knowledgeFresh: true,
                                liveValidated: best.Spawned && best.Downed,
                                capabilitySatisfied: true,
                                authorityBasis: authorityBasis,
                                knowledgeBasis:
                                    "personally confirmed captive at resolution",
                                owner: "custody authority",
                                authorityCeiling:
                                    CAInitiativeTier.Autonomous));
                    if (!authority || !resolutionDecision.Allowed)
                    {
                        CACustodyIntent proposed = intent;
                        intent = proposed == CACustodyIntent.MercyKill
                            ? CACustodyIntent.SecureStabilize
                            : CACustodyIntent.Secure;
                        custodyTrace += "; ordinary irreversible outcome "
                            + proposed + " blocked: "
                            + (!authority ? authorityBasis
                                : resolutionDecision.PrimaryReason)
                            + "; secure intake retained";
                    }
                    else
                    {
                        behaviorKey = "aftermath.custody_resolution";
                        authorityIdentity = authorityBasis;
                        authorityOrigin = CAAuthorityOrigin.Institutional;
                    }
                }
            }
            CAIntentContext ownedIntent = CACombatIntent.Authorized(actor,
                CAIntentController.RaidDefense, behaviorKey,
                authorityOrigin, authorityIdentity,
                behaviorKey == "aftermath.custody_resolution"
                    ? "custody institution" : "field custody intake",
                "custody-intent=" + intent
                    + "; captive=" + best.thingIDNumber);
            if (intent == CACustodyIntent.Abandon)
            {
                abandonedCustody[ActorCaptiveKey(actor, best)] =
                    Find.TickManager.TicksGame + 20000;
                CATrace.Pawn(actor, "field custody DECLINED - leaves "
                    + best.LabelShort + " to their wounds (" + custodyTrace
                    + "); another pair of hands may still choose otherwise",
                    contact: best.Position, anchor: actor.Position,
                    intent: ownedIntent);
                return null;
            }
            custodyIntents[best.thingIDNumber] =
                new PendingCustody
                {
                    ActorId = actor.thingIDNumber,
                    Intent = intent,
                    Trace = custodyTrace,
                    Tick = Find.TickManager.TicksGame,
                    Context = ownedIntent
                };
            Job job = JobMaker.MakeJob(CA_Defs.SecureEPW, best);
            job.count = bestBuddy.thingIDNumber;
            job.expiryInterval = 750;
            bool finalAuthority = behaviorKey
                    != "aftermath.custody_resolution"
                || HasIrreversibleCustodyAuthority(actor, intent,
                    out authorityIdentity);
            CABehaviorContext finalContext = CABehaviorContext.ForPawn(actor,
                authorityOrigin, authoritySatisfied: finalAuthority,
                knowledgeSatisfied: true, knowledgeFresh: true,
                liveValidated: best.Spawned && best.Downed,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                capabilitySatisfied: bestBuddy != null,
                materialSatisfied: true,
                currentIntentCompatible:
                    !CATactical.HasForeignPlayerForcedJob(actor),
                directPlayerOwnership:
                    CATactical.HasForeignPlayerForcedJob(actor),
                authorityBasis: authorityIdentity,
                knowledgeBasis: "personally confirmed captive",
                owner: behaviorKey == "aftermath.custody_resolution"
                    ? "custody institution" : "field custody intake",
                authorityCeiling: CAInitiativeTier.Autonomous);
            CABehaviorDecision finalDecision;
            if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(actor, job,
                behaviorKey, CAIntentController.RaidDefense, finalContext,
                out finalDecision, out ownedIntent,
                targetOrDemand: "custody-intent=" + intent
                    + "; captive=" + best.thingIDNumber,
                ownershipScope: finalContext.Owner,
                lifetimeTicks: 5000))
            {
                custodyIntents.Remove(best.thingIDNumber);
                CATrace.Pawn(actor, "field custody BLOCKED - "
                    + finalDecision.PrimaryReason,
                    contact: best.Position, anchor: actor.Position);
                return null;
            }
            custodyIntents[best.thingIDNumber].Context = ownedIntent;
            CATrace.Pawn(actor, "post-contact successor SELECTED: secure downed hostile "
                + best.LabelShort + " while " + bestBuddy.LabelShort
                + " retains local security; friendly casualty priority clear"
                + "; custody resolution " + intent + " (" + custodyTrace + ")",
                contact: best.Position, destination: best.Position,
                anchor: actor.Position, intent: ownedIntent);
            return job;
        }

        // ---- custody character resolution ----

        internal enum CACustodyIntent
        {
            Secure,
            SecureStabilize,
            Abandon,
            Execute,
            ImpulsiveExecute,
            MercyKill
        }

        internal sealed class PendingCustody
        {
            public int ActorId;
            public CACustodyIntent Intent;
            public string Trace;
            public int Tick;
            public CAIntentContext Context;
        }

        internal static readonly Dictionary<int, PendingCustody> custodyIntents =
            new Dictionary<int, PendingCustody>();
        private static readonly Dictionary<long, int> abandonedCustody =
            new Dictionary<long, int>();
        // Redemption: an impulsive execution leaves a weight the pawn carries
        // into the next resolution - for roughly four days the appetite paths
        // are braked and the caring path is easier to reach. The weight only
        // lands on someone whose conscience actually objects (ideoligion
        // unwilling, or real empathy); a true psychopath carries nothing.
        internal static void RecordExecutionWeight(Pawn actor, string victim,
            bool conscienceObjects, int episodeId, string cause)
        {
            if (!conscienceObjects) return;
            CAAftermathAccountabilityMapComponent.For(actor.Map)?.Record(actor,
                episodeId, victim, cause, 1f, 240000);
            CATrace.Pawn(actor, "carries the weight of " + victim
                + "'s death - the next custody will feel it",
                anchor: actor.Position);
        }

        private static bool CarriesExecutionWeight(Pawn actor, out string victim)
        {
            victim = null;
            float ignored;
            return CAAftermathAccountabilityMapComponent.For(actor?.Map)
                ?.TryActive(actor, out victim, out ignored) == true;
        }

        internal static bool RevalidateIrreversibleCustody(Pawn actor,
            Pawn captive, Job job, CACustodyIntent intent,
            CAIntentContext context, out string reason)
        {
            reason = "current custody authority is valid";
            if (actor == null || actor.Dead || actor.Downed || !actor.Spawned
                || captive == null || captive.Dead || !captive.Downed
                || !captive.Spawned || captive.Map != actor.Map)
            {
                reason = "actor or captive is no longer valid";
                return false;
            }
            CAIntentContext persisted;
            if (!context.IsValid || job == null
                || CABehaviorIntentMapComponent.For(actor.Map)?.TryGet(actor,
                    job, out persisted) != true
                || persisted.EpisodeId != context.EpisodeId
                || persisted.BehaviorKey != context.BehaviorKey)
            {
                reason = "the persisted custody episode no longer owns this job";
                return false;
            }
            if (context.TargetOrDemand == null
                || !context.TargetOrDemand.Contains("captive="
                    + captive.thingIDNumber))
            {
                reason = "the persisted custody target does not match";
                return false;
            }

            bool impulsive = intent == CACustodyIntent.ImpulsiveExecute;
            string key = impulsive
                ? "aftermath.unlawful_execution_breach"
                : "aftermath.custody_resolution";
            if (context.BehaviorKey != key)
            {
                reason = "the persisted behavior does not authorize this outcome";
                return false;
            }
            string authorityBasis = null;
            bool authority = impulsive || HasIrreversibleCustodyAuthority(
                actor, intent, out authorityBasis);
            if (impulsive)
                authorityBasis = "unlawful character breach with explicit accountability";
            CABehaviorContext gateContext = CABehaviorContext.ForPawn(actor,
                impulsive ? CAAuthorityOrigin.PlayerDelegated
                    : CAAuthorityOrigin.Institutional,
                authoritySatisfied: authority,
                knowledgeSatisfied: true, knowledgeFresh: true,
                liveValidated: GenSight.LineOfSight(actor.Position,
                    captive.Position, actor.Map, true),
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                capabilitySatisfied: true, materialSatisfied: true,
                currentIntentCompatible: actor.CurJob == job,
                directPlayerOwnership: false,
                authorityBasis: authorityBasis,
                knowledgeBasis: "personally confirmed captive at execution",
                owner: impulsive ? "character accountability"
                    : "custody institution",
                authorityCeiling: CAInitiativeTier.Autonomous);
            CABehaviorDecision decision = CABehaviorGate.Evaluate(key,
                gateContext);
            if (!decision.Allowed)
            {
                reason = decision.PrimaryReason;
                return false;
            }
            return true;
        }

        internal static long ActorCaptiveKey(Pawn actor, Pawn captive)
        {
            return ((long)actor.thingIDNumber << 32)
                | (uint)captive.thingIDNumber;
        }

        internal static bool RecentlyAbandonedBy(Pawn actor, Pawn captive)
        {
            int expiry;
            return abandonedCustody.TryGetValue(ActorCaptiveKey(actor, captive),
                    out expiry)
                && Find.TickManager.TicksGame < expiry;
        }

        private static bool HasIrreversibleCustodyAuthority(Pawn actor,
            CACustodyIntent intent, out string basis)
        {
            if (!CABehaviorGate.StableProfileAllows(actor,
                    "aftermath.custody_resolution"))
            {
                basis = "initiative below Autonomous";
                return false;
            }
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.enemyRestraint)
            {
                basis = "custody-resolution permission disabled";
                return false;
            }
            string law = CAPolicyLookup.Colony("prisoner treatment");
            if (law == "forbid execution"
                && (intent == CACustodyIntent.Execute
                    || intent == CACustodyIntent.MercyKill))
            {
                basis = "colony law forbids execution";
                return false;
            }
            if (law == "prefer capture")
            {
                basis = "colony law requires capture when possible";
                return false;
            }
            if (CAOrganizationAuthority.HasColonyCommand(actor))
            {
                basis = "held colony command office";
                return true;
            }
            if (SquadComponent.IsLeader(actor)
                && SquadComponent.SquadOf(actor) > 0)
            {
                basis = "held squad command authority";
                return true;
            }
            basis = "no custody office, command role, or binding law "
                + "authorizes the irreversible outcome";
            return false;
        }

        // The outcome is conditional on who the pawn is, not just what the
        // procedure says: a disciplined psychopath under command secures the
        // prisoner because capture is useful; unleashed appetite finishes
        // them; cold arithmetic walks away from the mortally wounded;
        // compassion or medicine stabilizes; rage strikes and answers for it
        // later. Incremental and relative - never a single trait flipping a
        // switch.
        internal static CACustodyIntent ResolveCustodyIntent(Pawn actor,
            Pawn captive, out string trace)
        {
            DispositionProfile d = Disposition.Of(actor);
            bool psychopath = actor.story?.traits?.HasTrait(
                TraitDefOf.Psychopath) ?? false;
            bool bloodlust = actor.story?.traits?.HasTrait(
                TraitDefOf.Bloodlust) ?? false;
            int medicine = actor.skills != null
                ? actor.skills.GetSkill(SkillDefOf.Medicine).Level : 0;
            float mood = actor.needs?.mood?.CurLevelPercentage ?? 0.5f;
            bool ideoTolerant = false;
            try
            {
                ideoTolerant = actor.Ideo != null
                    && actor.Ideo.MemberWillingToDo(new HistoryEvent(
                        HistoryEventDefOf.ExecutedPrisonerGuilty,
                        actor.Named(HistoryEventArgsNames.Doer)));
            }
            catch { }
            Pawn squadLeader = SquadComponent.LeaderPawn(
                SquadComponent.SquadOf(actor), actor.Map);
            bool underCommand = squadLeader != null && squadLeader != actor
                && !squadLeader.Dead && !squadLeader.Downed
                && CommsModule.CanCommand(squadLeader, actor);
            bool mortallyWounded = false;
            try
            {
                mortallyWounded = captive.health.hediffSet.BleedRateTotal
                        > 0.01f
                    && HealthUtility.TicksUntilDeathDueToBloodLoss(captive)
                        < 30000;
            }
            catch { }
            // Colony capacity is part of the moral arithmetic: custody
            // without a place to hold them or hands to heal them is a false
            // promise, and it changes what compassion honestly means.
            bool hasPrisonerBed = false;
            try
            {
                hasPrisonerBed = RestUtility.FindBedFor(captive, actor,
                    checkSocialProperness: false,
                    ignoreOtherReservations: false,
                    GuestStatus.Prisoner) != null;
            }
            catch { }
            bool hasCareCapacity = false;
            var colonists = actor.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn doc = colonists[i];
                if (doc.Downed || doc.WorkTagIsDisabled(WorkTags.Caring))
                    continue;
                if (doc.workSettings == null
                    || !doc.workSettings.WorkIsActive(WorkTypeDefOf.Doctor))
                    continue;
                if (doc.skills != null && doc.skills.GetSkill(
                        SkillDefOf.Medicine).Level >= 4)
                { hasCareCapacity = true; break; }
            }
            bool colonyCanCare = hasPrisonerBed && hasCareCapacity;

            // Redemption's mechanical half: recent remorse brakes the dark
            // paths and warms the hands. Effective-empathy shifts, never a
            // hard rule - the same incremental grammar as everything else.
            string remorseVictim;
            bool carryingWeight = CarriesExecutionWeight(actor,
                out remorseVictim);
            float effectiveEmpathy = carryingWeight
                ? UnityEngine.Mathf.Min(1f, d.empathy + 0.25f) : d.empathy;

            string basis = "discipline " + d.discipline.ToString("0.00")
                + ", empathy " + d.empathy.ToString("0.00")
                + ", aggression " + d.aggression.ToString("0.00")
                + (psychopath ? ", psychopath" : "")
                + (bloodlust ? ", bloodlust" : "")
                + (ideoTolerant ? ", ideoligion tolerates execution" : "")
                + (underCommand ? ", under command" : ", no command contact")
                + ", mood " + mood.ToString("0.00")
                + ", medicine " + medicine
                + (mortallyWounded ? ", captive mortally wounded" : "")
                + (hasPrisonerBed ? "" : ", no prisoner accommodation")
                + (hasCareCapacity ? "" : ", no colony care capacity")
                + (carryingWeight ? ", carrying the weight of "
                    + remorseVictim + "'s death" : "");

            // The third input the character branch was missing: colony law.
            // Disposition and ideoligion are the first two voices; what the
            // settlement has legislated is the third. Law binds deliberation;
            // rage can still defy it - and answers for it.
            string custodyLaw = CAPolicyLookup.Colony("prisoner treatment");
            if (custodyLaw != "individual choice")
                basis += ", colony law: " + custodyLaw;

            // Rage first: appetite plus a boiling mood plus loose discipline.
            // Fresh remorse is a brake even on rage.
            if (bloodlust && mood < 0.25f && d.discipline < 0.55f
                && !carryingWeight)
            {
                trace = "impulsive - rage over restraint"
                    + (custodyLaw == "forbid execution"
                        ? " IN DEFIANCE OF COLONY LAW" : "")
                    + "; " + basis;
                return CACustodyIntent.ImpulsiveExecute;
            }
            // Deliberate finish: appetite or permissive conscience, with no
            // command presence and little empathy or discipline to brake it.
            // Legislated prohibition closes this path outright - deliberation
            // is exactly where law binds.
            if ((psychopath || bloodlust || ideoTolerant)
                && !underCommand && d.discipline < 0.45f
                && effectiveEmpathy < 0.35f
                && custodyLaw != "forbid execution")
            {
                trace = "deliberate finish - nothing braking the appetite; "
                    + basis;
                return CACustodyIntent.Execute;
            }
            // Mercy: when the colony cannot honestly care for the dying,
            // a compassionate pawn may choose the quick end over the slow
            // one - if their conscience permits it (tolerant ideoligion, or
            // compassion strong enough to carry the weight).
            if (mortallyWounded && !colonyCanCare
                && effectiveEmpathy >= 0.60f
                && (ideoTolerant || effectiveEmpathy >= 0.75f))
            {
                trace = "mercy - care cannot reach them; " + basis;
                return CACustodyIntent.MercyKill;
            }
            // Cold arithmetic: the dying are not worth the hands. Absent
            // capacity, even lukewarm pawns reach the same sum.
            if (mortallyWounded
                && (psychopath
                    || effectiveEmpathy < (colonyCanCare ? 0.25f : 0.35f))
                && d.discipline >= 0.40f
                && !(custodyLaw == "prefer capture" && colonyCanCare))
            {
                trace = "cold arithmetic - not worth the time; " + basis;
                return CACustodyIntent.Abandon;
            }
            // Warm hands or skilled ones stabilize what they restrain.
            if (effectiveEmpathy >= 0.60f || medicine >= 6)
            {
                trace = "restrain then stabilize; " + basis;
                return CACustodyIntent.SecureStabilize;
            }
            trace = "procedure holds; " + basis;
            return CACustodyIntent.Secure;
        }

        // Whether this pawn's conscience would object to an execution they
        // themselves commit - the gate on whether the act leaves a weight.
        internal static bool ConscienceObjects(Pawn actor)
        {
            bool psychopath = actor.story?.traits?.HasTrait(
                TraitDefOf.Psychopath) ?? false;
            if (psychopath) return false;
            DispositionProfile d = Disposition.Of(actor);
            bool ideoTolerant = false;
            try
            {
                ideoTolerant = actor.Ideo != null
                    && actor.Ideo.MemberWillingToDo(new HistoryEvent(
                        HistoryEventDefOf.ExecutedPrisonerGuilty,
                        actor.Named(HistoryEventArgsNames.Doer)));
            }
            catch { }
            return !ideoTolerant || d.empathy >= 0.30f;
        }

        // The substitution form: not just "is there a casualty" but the
        // actual job answering them - tend first, then rescue-to-bed. Used by
        // the rest gate so a pawn heading inside carries the casualty in on
        // the way instead of walking past them to bed.
        internal static bool TryBuildPriorityRescueJob(Pawn actor,
            KnowledgeMapComponent knowledge, out Job job, out Pawn subject)
        {
            job = null;
            subject = null;
            List<WelfareFactSnapshot> facts = knowledge.RememberedWelfare(actor);
            for (int i = 0; i < facts.Count; i++)
            {
                WelfareFactSnapshot fact = facts[i];
                if (!fact.Actionable) continue;
                Pawn candidate;
                WelfareFactSnapshot fresh;
                if (!knowledge.TryFindPawnById(fact.SubjectId, out candidate)
                    || candidate == null || candidate == actor
                    || candidate.Faction != actor.Faction
                    || !candidate.Spawned || candidate.Map != actor.Map
                    || !knowledge.TryGetFreshWelfare(actor, fact.SubjectId,
                        out fresh) || !fresh.Actionable) continue;
                if (actor.WorkTagIsDisabled(WorkTags.Caring)) continue;
                if (fresh.ObservedNeedsTend)
                {
                    Job tend = CAClinicalObservation.TendJob(actor, candidate);
                    if (tend != null) { job = tend; subject = candidate; return true; }
                }
                if (!fresh.ObservedDowned || !candidate.Downed
                    || candidate.InBed()
                    || !HealthAIUtility.WantsToBeRescued(candidate)
                    || candidate.IsForbidden(actor)
                    || !actor.CanReserveAndReach(candidate,
                        PathEndMode.OnCell, Danger.Deadly)) continue;
                Building_Bed bed = RestUtility.FindBedFor(candidate, actor,
                    checkSocialProperness: false,
                    ignoreOtherReservations: false, candidate.GuestStatus);
                if (bed == null || !candidate.CanReserve(bed)) continue;
                Job rescue = JobMaker.MakeJob(JobDefOf.Rescue, candidate, bed);
                rescue.count = 1;
                job = rescue;
                subject = candidate;
                return true;
            }
            return false;
        }

        internal static bool HasPriorityFriendlyCasualty(Pawn actor,
            KnowledgeMapComponent knowledge)
        {
            List<WelfareFactSnapshot> facts = knowledge.RememberedWelfare(actor);
            for (int i = 0; i < facts.Count; i++)
            {
                WelfareFactSnapshot fact = facts[i];
                if (!fact.Actionable) continue;
                Pawn subject;
                WelfareFactSnapshot fresh;
                if (!knowledge.TryFindPawnById(fact.SubjectId, out subject)
                    || subject == null || subject == actor
                    || subject.Faction != actor.Faction
                    || !subject.Spawned || subject.Map != actor.Map
                    || !knowledge.TryGetFreshWelfare(actor, fact.SubjectId,
                        out fresh) || !fresh.Actionable) continue;
                if (actor.WorkTagIsDisabled(WorkTags.Caring)) continue;
                if (fresh.ObservedNeedsTend
                    && CAClinicalObservation.TendJob(actor, subject) != null)
                    return true;
                if (!fresh.ObservedDowned || !subject.Downed
                    || subject.InBed()
                    || !HealthAIUtility.WantsToBeRescued(subject)
                    || subject.IsForbidden(actor)
                    || !actor.CanReserveAndReach(subject,
                        PathEndMode.OnCell, Danger.Deadly)) continue;
                Building_Bed bed = RestUtility.FindBedFor(subject, actor,
                    checkSocialProperness: false,
                    ignoreOtherReservations: false, subject.GuestStatus);
                if (bed != null && subject.CanReserve(bed)) return true;
            }
            return false;
        }

        private static bool HasUnresolvedLocalThreat(Pawn actor,
            IntVec3 outcomeCell, KnowledgeMapComponent knowledge)
        {
            List<ThreatContactSnapshot> active = knowledge.FreshContacts(actor);
            for (int i = 0; i < active.Count; i++)
            {
                IntVec3 cell = active[i].Cell;
                if (cell.InHorDistOf(actor.Position, LocalOutcomeRadius)
                    || cell.InHorDistOf(outcomeCell, LocalOutcomeRadius))
                    return true;
            }
            return false;
        }

        private static Pawn SecurityBuddy(Pawn actor, Pawn target)
        {
            IReadOnlyList<Pawn> pawns = actor.Map.mapPawns.FreeColonistsSpawned;
            Pawn best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == null || candidate == actor || candidate.Dead
                    || candidate.Downed || !candidate.Awake()
                    || candidate.InMentalState || candidate.Faction != actor.Faction
                    || candidate.equipment?.Primary == null
                    || candidate.WorkTagIsDisabled(WorkTags.Violent)
                    || CAImmediateCombat.RequiresCombatRecoveryNow(candidate)
                    || !candidate.Position.InHorDistOf(actor.Position,
                        BuddyRadius)
                    || !GenSight.LineOfSight(candidate.Position,
                        target.Position, actor.Map, true)) continue;
                float distance = candidate.Position.DistanceTo(target.Position);
                if (best == null || distance < bestDistance
                    || UnityEngine.Mathf.Approximately(distance, bestDistance)
                        && candidate.thingIDNumber < best.thingIDNumber)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static bool ActorOwnsSecureStep(Pawn actor, Pawn target,
            Pawn buddy)
        {
            float actorDistance = actor.Position.DistanceToSquared(target.Position);
            float buddyDistance = buddy.Position.DistanceToSquared(target.Position);
            return actorDistance < buddyDistance - 0.01f
                || UnityEngine.Mathf.Abs(actorDistance - buddyDistance) <= 0.01f
                    && actor.thingIDNumber < buddy.thingIDNumber;
        }
    }

    public sealed class CAExecutionAccountabilityRecord : IExposable
    {
        public int ActorId;
        public int EpisodeId;
        public string Victim;
        public string Cause;
        public float Weight;
        public int CreatedTick;
        public int ExpiryTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ActorId, "actorId", -1);
            Scribe_Values.Look(ref EpisodeId, "episodeId", 0);
            Scribe_Values.Look(ref Victim, "victim");
            Scribe_Values.Look(ref Cause, "cause");
            Scribe_Values.Look(ref Weight, "weight", 0f);
            Scribe_Values.Look(ref CreatedTick, "createdTick", 0);
            Scribe_Values.Look(ref ExpiryTick, "expiryTick", 0);
        }
    }

    public sealed class CAAftermathAccountabilityMapComponent : MapComponent
    {
        private List<CAExecutionAccountabilityRecord> records =
            new List<CAExecutionAccountabilityRecord>();
        private int pruneCooldown;

        public CAAftermathAccountabilityMapComponent(Map map) : base(map) { }

        public static CAAftermathAccountabilityMapComponent For(Map map)
        {
            return map?.GetComponent<CAAftermathAccountabilityMapComponent>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records,
                "CA_executionAccountability", LookMode.Deep);
            if (records == null)
                records = new List<CAExecutionAccountabilityRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                for (int i = 0; i < records.Count; i++)
                    if (records[i] != null)
                        CACombatIntent.ObserveEpisode(records[i].EpisodeId);
        }

        public override void MapComponentTick()
        {
            if (--pruneCooldown > 0) return;
            pruneCooldown = 2500;
            int now = Find.TickManager.TicksGame;
            records.RemoveAll(record => record == null
                || record.ActorId < 0 || record.ExpiryTick <= now);
        }

        internal void Record(Pawn actor, int episodeId, string victim,
            string cause, float weight, int lifetimeTicks)
        {
            if (actor == null || actor.Map != map || episodeId <= 0) return;
            int now = Find.TickManager.TicksGame;
            records.Add(new CAExecutionAccountabilityRecord
            {
                ActorId = actor.thingIDNumber,
                EpisodeId = episodeId,
                Victim = victim,
                Cause = cause,
                Weight = UnityEngine.Mathf.Max(0f, weight),
                CreatedTick = now,
                ExpiryTick = now + System.Math.Max(1, lifetimeTicks)
            });
        }

        internal bool TryActive(Pawn actor, out string victim,
            out float weight)
        {
            victim = null;
            weight = 0f;
            if (actor == null) return false;
            int now = Find.TickManager.TicksGame;
            CAExecutionAccountabilityRecord latest = null;
            for (int i = 0; i < records.Count; i++)
            {
                CAExecutionAccountabilityRecord record = records[i];
                if (record == null || record.ActorId != actor.thingIDNumber
                    || record.ExpiryTick <= now) continue;
                weight += record.Weight;
                if (latest == null
                    || record.CreatedTick > latest.CreatedTick)
                    latest = record;
            }
            if (latest == null) return false;
            victim = latest.Victim;
            return true;
        }
    }

    public class JobGiver_CACombatAftermath : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return CACombatAftermath.TryGiveJob(pawn,
                allowDraftedIdle: false);
        }
    }

    public class JobDriver_CASecureEPW : JobDriver
    {
        private Pawn Captive => job.targetA.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Captive, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(delegate
            {
                Pawn captive = Captive;
                return captive == null || captive.Dead || !captive.Downed
                    || captive.IsPrisonerOfColony || captive.guest == null;
            });
            this.FailOn(delegate
            {
                if (CACombatThreat.PerceivesActiveThreat(pawn)) return true;
                KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(
                    pawn.Map);
                return knowledge != null
                    && CACombatAftermath.HasPriorityFriendlyCasualty(pawn,
                        knowledge);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A,
                PathEndMode.ClosestTouch);
            Toil secure = Toils_General.Wait(120);
            secure.WithProgressBarToilDelay(TargetIndex.A);
            secure.handlingFacing = true;
            secure.tickAction = delegate
            {
                if (Captive != null)
                    pawn.rotationTracker.FaceCell(Captive.Position);
            };
            yield return secure;
            Toil record = ToilMaker.MakeToil("CAFieldCustodyRecord");
            record.initAction = delegate
            {
                Pawn captive = Captive;
                if (captive == null || captive.Dead || !captive.Downed
                    || captive.guest == null || captive.IsPrisonerOfColony)
                    return;
                CACombatAftermath.PendingCustody pending;
                CACombatAftermath.CACustodyIntent intent =
                    CACombatAftermath.custodyIntents.TryGetValue(
                        captive.thingIDNumber, out pending)
                    && pending.ActorId == pawn.thingIDNumber
                        ? pending.Intent
                        : CACombatAftermath.CACustodyIntent.Secure;
                CAIntentContext ownedIntent = default(CAIntentContext);
                bool hasOwnedIntent = pending != null
                    && pending.Context.IsValid;
                if (hasOwnedIntent) ownedIntent = pending.Context;
                else hasOwnedIntent = CABehaviorIntentMapComponent
                    .For(pawn.Map)?.TryGet(pawn, job,
                        out ownedIntent) == true;
                if (pending == null && hasOwnedIntent
                    && ownedIntent.TargetOrDemand != null)
                {
                    const string prefix = "custody-intent=";
                    int start = ownedIntent.TargetOrDemand.IndexOf(prefix,
                        System.StringComparison.Ordinal);
                    int end = ownedIntent.TargetOrDemand.IndexOf(';');
                    if (start >= 0)
                    {
                        string value = ownedIntent.TargetOrDemand.Substring(
                            start + prefix.Length,
                            end > start ? end - start - prefix.Length
                                : ownedIntent.TargetOrDemand.Length - start
                                    - prefix.Length);
                        CACombatAftermath.CACustodyIntent restored;
                        if (System.Enum.TryParse(value, out restored))
                            intent = restored;
                    }
                }
                CACombatAftermath.custodyIntents.Remove(captive.thingIDNumber);

                if (intent == CACombatAftermath.CACustodyIntent.Execute
                    || intent == CACombatAftermath.CACustodyIntent
                        .ImpulsiveExecute
                    || intent == CACombatAftermath.CACustodyIntent.MercyKill)
                {
                    string authorityFailure = "no persisted custody intent";
                    if (!hasOwnedIntent
                        || !CACombatAftermath.RevalidateIrreversibleCustody(
                            pawn, captive, job, intent, ownedIntent,
                            out authorityFailure))
                    {
                        CATrace.Pawn(pawn,
                            "irreversible custody STANDS DOWN - "
                            + authorityFailure,
                            contact: captive.Position,
                            anchor: pawn.Position,
                            intent: hasOwnedIntent
                                ? (CAIntentContext?)ownedIntent : null);
                        return;
                    }
                    // The finish, through the native execution pipeline so
                    // ideoligion and witnesses answer organically - mercy and
                    // rage alike are paid for in the coin the colony's
                    // beliefs actually use.
                    bool impulsive = intent == CACombatAftermath
                        .CACustodyIntent.ImpulsiveExecute;
                    bool mercy = intent == CACombatAftermath
                        .CACustodyIntent.MercyKill;
                    string label = captive.LabelShort;
                    try
                    {
                        ExecutionUtility.DoExecutionByCut(pawn, captive);
                        Find.HistoryEventsManager.RecordEvent(new HistoryEvent(
                            HistoryEventDefOf.ExecutedPrisonerGuilty,
                            pawn.Named(HistoryEventArgsNames.Doer)));
                    }
                    catch (System.Exception ex)
                    {
                        Log.ErrorOnce("[Colonist Awareness] custody execution failed: "
                            + ex, 937542311);
                        return;
                    }
                    CATrace.Pawn(pawn, (mercy
                        ? "field custody ended in a MERCY END for "
                        : impulsive
                        ? "field custody became an IMPULSIVE EXECUTION of "
                        : "field custody became a DELIBERATE EXECUTION of ")
                        + label + (pending != null
                            ? " (" + pending.Trace + ")" : ""),
                        contact: captive.Position, anchor: pawn.Position,
                        intent: hasOwnedIntent
                            ? (CAIntentContext?)ownedIntent : null);
                    // Redemption's seed: rage that a conscience objects to
                    // becomes a carried weight - mercy leaves none.
                    if (impulsive
                        && CACombatAftermath.ConscienceObjects(pawn))
                        CACombatAftermath.RecordExecutionWeight(pawn, label,
                            true, ownedIntent.EpisodeId,
                            "unlawful impulsive execution");
                    return;
                }

                captive.guest.CapturedBy(Faction.OfPlayer, pawn);
                captive.guest.WaitInsteadOfEscapingForDefaultTicks();
                CATrace.Pawn(pawn, "EPW FIELD-CUSTODY RECORDED for "
                    + captive.LabelShort
                    + "; native capture disarmed the captive and finite compliance began; transport and treatment remain follow-on outcomes"
                    + (pending != null ? " (" + pending.Trace + ")" : ""),
                    contact: captive.Position, anchor: pawn.Position,
                    intent: hasOwnedIntent
                        ? (CAIntentContext?)ownedIntent : null);
                if (intent == CACombatAftermath.CACustodyIntent
                    .SecureStabilize)
                {
                    Job tend = CAClinicalObservation.TendJob(pawn, captive);
                    if (tend != null)
                    {
                        pawn.jobs.jobQueue.EnqueueFirst(tend, JobTag.Misc);
                        CATrace.Pawn(pawn,
                            "custody STABILIZATION queued for "
                            + captive.LabelShort
                            + " - warm hands or skilled ones tend what they restrain",
                            contact: captive.Position, anchor: pawn.Position);
                    }
                }
            };
            record.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return record;
        }
    }
}
