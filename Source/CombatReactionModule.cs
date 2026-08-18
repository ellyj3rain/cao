using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // The reaction lane is actor-neutral. RimWorld calls it from HumanlikeConstant
    // every 30 ticks; it decides only the immediate response to a fact. Deliberate
    // colony cover plans and hostile assault objectives remain in their own duties.
    public class JobGiver_CACombatReaction : JobGiver_AIFightEnemies
    {
        private struct SightEpisode
        {
            public int hostileId;
            public int firstSeenTick;
            public int lastSeenTick;
        }

        private static readonly Dictionary<int, SightEpisode> currentSight =
            new Dictionary<int, SightEpisode>();
        private const int SightContinuityTicks = 45;

        internal static void ClearTransientSight()
        {
            currentSight.Clear();
            CAImmediateCombat.ClearTransient();
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            // Immediate survival must be evaluated before standing CA ownership.
            // A stack, ambush, or concealment job can be the thing that needs to be
            // aborted after a hit or visible explosive. Direct native player work,
            // emergency care, and an already-running flee remain sovereign below.
            if (!CanObserveImmediateSurvival(pawn)) return null;
            bool mayRunOrdinaryReaction = CanThink(pawn);

            bool player = pawn.IsColonistPlayerControlled;
            bool assault = IsGenericAssaulter(pawn);
            if (!player && !assault) return null;

            if (assault)
            {
                // Arson is not a suicide pact. This is the game's own fire-flee
                // machinery, promoted from panic-only behavior to anticipatory
                // combat reaction for a pawn actively assaulting a settlement.
                // CA_SafeIgnite already owns a precomputed open-air exit in target C;
                // do not tear that compound action apart while its exit remains safe.
                if (SafeIgniteEgressStillValid(pawn)) return null;
                Job fire = FleeUtility.FleeLargeFireJob(pawn, 1, 5, 12);
                if (fire != null) return fire;
            }

            var settings = AwarenessMod.Settings;
            if (settings == null) return null;
            bool playerMayFight = !player || PlayerMaySelfReact(pawn, settings);
            bool actorMayFight = player ? playerMayFight
                : assault && AssaulterMayFight(pawn);
            bool playerMayEvade = player
                && PlayerMayImmediateEvasion(pawn, settings);
            bool playerMayPreserve = player
                && PlayerMaySelfPreserve(pawn, settings);
            if (player && !playerMayFight && !playerMayEvade
                && !playerMayPreserve) return null;

            ThreatContactSnapshot contact;
            bool hasContact = TryContact(pawn, settings, out contact);
            Pawn target = hasContact ? PawnById(pawn.Map, contact.HostileId) : null;
            bool visible = target != null
                && KnowledgeMapComponent.CanCurrentlySeeHostile(pawn, target);
            Pawn impactTarget;
            bool directImpactContact = CAImmediateCombat.TryVisibleHarmInstigator(
                pawn, out impactTarget);
            if (directImpactContact)
            {
                target = impactTarget;
                visible = true;
                contact = ContactFromVisibleTarget(impactTarget);
                hasContact = true;
            }

            // Damage is already an immediate, pawn-private fact. RimWorld asks for
            // a job override before it updates Pawn_MindState.lastHarmTick, so the
            // paired native-seam patch records that stimulus early enough for this
            // same pass. Survival pressure bypasses recognition delay; being hit is
            // not an ambiguous report that needs further interpretation.
            if (settings.survivalResponses
                && (assault || playerMayEvade || playerMayPreserve))
            {
                // A visible grenade or other explosive already in flight is a more
                // immediate fact than target selection. It may interrupt an automatic
                // weapon commitment, but CanThink above still protects direct or
                // queued player-forced work.
                if (assault || playerMayEvade)
                {
                    Job evasion;
                    CAIncomingEvasionResult evasionState = CACombatIntent
                        .ResolveIncomingHazardEvasion(pawn, out evasion);
                    if (evasionState == CAIncomingEvasionResult.Active)
                    {
                        if (evasion != null && evasion != pawn.CurJob)
                        {
                            CAImmediateCombat
                                .PrepareStandingCombatForImmediateEvasion(pawn);
                            return evasion;
                        }
                        // Returning the already-running route is the native think-tree
                        // ownership token; lower nodes cannot replace it while the
                        // physical deadline remains active.
                        return evasion ?? pawn.CurJob;
                    }
                    if (evasionState == CAIncomingEvasionResult.Pending)
                        return pawn.CurJob ?? PendingEvasionSearchJob();
                }

                if (assault || playerMayPreserve)
                {
                    Job survival;
                    CASelfPreservationAssessment assessment;
                    if (CAImmediateCombat.TrySelfPreservationJob(pawn,
                        visible ? target : null,
                        hasContact ? contact.Cell : IntVec3.Invalid,
                        actorMayFight, out survival, out assessment))
                    {
                        // Retire any standing Hold, automatic station, concealment, or
                        // stack membership so it cannot pull the pawn straight back into
                        // the position they just judged untenable.
                        CAImmediateCombat.ReleaseStandingCombatForSurvival(pawn);
                        return survival;
                    }
                }
            }

            if (!mayRunOrdinaryReaction) return null;

            // Health-adjusted recovery owns the interval between the immediate
            // survival response and ordinary fire-position/target selection. It is
            // actor-local and yields naturally to direct player orders in CanThink.
            if (player)
            {
                Job recovery;
                if (CAImmediateCombat.TryPersistentRecoveryJob(pawn,
                    out recovery)) return recovery;
            }

            // A fire-position or target-specific attack issued by this giver is a
            // bounded decision, not a fresh request every 30 ticks. Let it arrive or
            // complete its firing commitment unless damage above has just produced a
            // stronger survival response.
            if (pawn.CurJob != null && pawn.CurJob.jobGiver == this
                && (pawn.CurJob.def == CA_Defs.CombatMove
                    || pawn.CurJob.def == CA_Defs.BoundedMeleeDefense
                    || pawn.CurJob.def == CA_AudibleDefs.InvestigateGunfire
                        && !visible && !directImpactContact
                    || pawn.CurJob.def == JobDefOf.AttackStatic
                    || pawn.CurJob.def == JobDefOf.AttackMelee))
                return pawn.CurJob;

            // A target-specific duty attack owns its short native weapon cycle. The
            // actor-local position judgment may run between commitments, never through
            // a warmup, burst, cooldown, or bounded melee intercept.
            if (pawn.CurJobDef == CA_Defs.BoundedRangedDefense
                || pawn.CurJobDef == CA_Defs.BoundedMeleeDefense) return null;
            Stance_Busy busy = pawn.stances != null
                ? pawn.stances.curStance as Stance_Busy : null;
            if (busy != null && busy.verb != null) return null;

            if (!hasContact)
            {
                // A standing tactical order keeps its authored post when there is no
                // visible/remembered hostile fact to justify actor-local movement.
                if (CATactical.IsAutomaticDefense(pawn)
                    || CATactical.IsHold(pawn)) return null;
                return settings.knowledgeContacts
                    ? TryAudibleCueResponse(pawn) : null;
            }

            // Recognition and action are separate. A composed, alert pawn reacts
            // faster; pain slows the hand. The evaluation channel is still the
            // engine's fixed 30-tick constant-think pass.
            if (!directImpactContact
                && Find.TickManager.TicksGame - contact.AcquiredTick < ReactionDelay(pawn))
                return null;

            if (player && !playerMayFight) return null;

            if (visible)
            {
                Job position;
                CACombatCellAssessment current;
                CACombatCellAssessment better;
                if (CAImmediateCombat.TryFirePositionJob(pawn, target,
                    out position, out current, out better))
                    return position;
            }

            if (player && !visible)
            {
                Job reacquire;
                if (CAImmediateCombat.TryCloseKnownContactPositionJob(
                        pawn, contact, out reacquire))
                    return reacquire;
            }

            // Hold and automatic-defense duties remain the owners after a bounded
            // local reposition. If no materially better cell exists, their native
            // point-defense giver supplies the target-committed attack or posture.
            if (CATactical.IsAutomaticDefense(pawn)
                || CATactical.IsHold(pawn)) return null;

            if (assault)
            {
                if (visible)
                {
                    if (ShouldBreakContact(pawn, target, contact))
                    {
                        Job withdraw = FleeUtility.FleeJob(pawn, target, 10);
                        if (withdraw != null) return withdraw;
                    }
                    return NativeFightJob(pawn);
                }

                // A newly learned defender invalidates a stale vandalism or blind
                // approach job. Pause once; the normal CA_AssaultAware duty then
                // chooses reconnaissance from the remembered cell.
                if (IsObjectiveJob(pawn.CurJob)
                    && contact.SourceTick >= pawn.CurJob.startTick)
                    return PostureJob(pawn, contact.Cell, 90);
                return null;
            }

            if (visible)
            {
                Job immediate = PlayerImmediateFight(pawn, target, contact);
                if (immediate != null) return immediate;
            }
            // Once the slow planner owns the pawn, its duty supplies movement and
            // the armed station. Do not replace a cover move with the transient
            // current-cell posture merely because contact is out of sight.
            if (!CACombatThreat.PerceivesActiveThreat(pawn))
                return null;
            return PostureJob(pawn, contact.Cell, 91);
        }

        private static bool CanThink(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed
                || pawn.InMentalState || !pawn.Awake()) return false;
            if (CATactical.HasForeignPlayerForcedJob(pawn)) return false;
            if (CATactical.HasPendingOwnedPlayerForcedJob(pawn)) return false;
            if (pawn.CurJob != null && pawn.CurJob.playerForced
                && (!CATactical.IsHold(pawn)
                    || !CABehaviorGate.StableProfileAllows(pawn,
                        "combat.hold_deviation")
                    || !CAImmediateCombat.HarmedRecently(pawn))) return false;
            if (pawn.CurJob != null && IsFleeJob(pawn.CurJob)) return false;
            if (pawn.jobs == null) return false;

            var withdrawal = pawn.Map.GetComponent<WithdrawalMapComponent>();
            if (withdrawal != null && withdrawal.InPlan(pawn)) return false;

            var lord = pawn.GetLord();
            if (lord != null && lord.LordJob is LordJob_CAStackBreach) return false;
            if (HiddenRegistry.IsHiddenOrOrdered(pawn)) return false;
            if (CATactical.HasExplicitOrder(pawn))
            {
                // Ordinary explicit orders still own the pawn. A Proactive Hold is a
                // tactical envelope rather than a command to suppress all judgment:
                // actor-local movement may improve ground inside it, while the Hold
                // remains the standing owner. Actual harm may still trigger the
                // existing explicit break-contact path above.
                if (!CATactical.IsHold(pawn)
                    || !CABehaviorGate.StableProfileAllows(pawn,
                        "combat.hold_deviation")) return false;
            }
            return true;
        }

        private static bool CanObserveImmediateSurvival(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed
                || pawn.InMentalState || !pawn.Awake() || pawn.jobs == null)
                return false;
            // CATactical distinguishes a later untagged direct job from the
            // player-forced job that executes the current CA order. The former
            // remains sovereign; the latter may be aborted by actual danger.
            Lord lord = pawn.GetLord();
            Job current = pawn.CurJob;
            if (current != null && current.playerForced
                && (lord == null || current.lord != lord)) return false;
            if (CATactical.HasForeignPlayerForcedJob(pawn)) return false;
            return !IsEmergencyJob(pawn, pawn.CurJob)
                && !IsFleeJob(pawn.CurJob);
        }

        private static bool IsFleeJob(Job job)
        {
            return job != null && (job.def == JobDefOf.Flee
                || job.def == JobDefOf.FleeAndCower
                || job.def == JobDefOf.FleeAndCowerShort);
        }

        private static bool IsEmergencyJob(Pawn pawn, Job job)
        {
            // Mission triage normally owns its finite job, but when that giver has
            // just yielded to newly perceived danger this reaction lane must be
            // able to supersede it. Other medical work remains protected.
            if (job != null && job.jobGiver is JobGiver_CAMissionTriage)
                return false;
            if (job != null && (job.def == JobDefOf.BeatFire
                    || job.def == JobDefOf.ExtinguishFiresNearby))
            {
                RaidResponseMapComponent response = pawn?.Map
                    ?.GetComponent<RaidResponseMapComponent>();
                string ignored;
                return response == null
                    || !response.BlocksOptionalFireResponse(pawn, job,
                        out ignored);
            }
            return job != null && (job.def == JobDefOf.TendPatient
                || job.def == JobDefOf.TendEntity || job.def == JobDefOf.Rescue
                || job.def == CA_Defs.EmergencySelfTend
                || job.def == JobDefOf.ExtinguishSelf
                );
        }

        internal static bool PlayerMaySelfReact(Pawn pawn, AwarenessSettings settings)
        {
            if (settings == null || !settings.raidResponse
                || !PlayerMayUseReactionLane(pawn,
                    "combat.local_reaction")) return false;
            if (pawn.WorkTagIsDisabled(WorkTags.Violent)) return false;
            if (pawn.equipment == null || pawn.equipment.Primary == null) return false;
            if (SquadComponent.CombatLiability(pawn)) return false;
            if (pawn.Drafted && pawn.drafter != null
                && !pawn.drafter.FireAtWill) return false;

            return true;
        }

        internal static bool PlayerMaySelfPreserve(Pawn pawn,
            AwarenessSettings settings)
        {
            return settings != null && settings.survivalResponses
                && PlayerMayUseReactionLane(pawn,
                    "combat.withdrawal_self_preservation");
        }

        internal static bool PlayerMayImmediateEvasion(Pawn pawn,
            AwarenessSettings settings)
        {
            return settings != null && settings.survivalResponses
                && PlayerMayUseReactionLane(pawn,
                    "survival.immediate_evasion");
        }

        private static bool PlayerMayUseReactionLane(Pawn pawn,
            string behaviorKey)
        {
            if (pawn == null) return false;
            if (!CABehaviorGate.StableProfileAllows(pawn, behaviorKey))
                return false;
            Job cur = pawn.CurJob;
            if (cur == null) return true;
            return !IsEmergencyJob(pawn, cur) && !IsFleeJob(cur);
        }

        private static bool AssaulterMayFight(Pawn pawn)
        {
            return pawn != null && !pawn.WorkTagIsDisabled(WorkTags.Violent)
                && pawn.TryGetAttackVerb(null,
                    CAImmediateCombat.AllowManualCombatVerb(pawn)) != null;
        }

        private static bool IsGenericAssaulter(Pawn pawn)
        {
            var lord = pawn != null ? pawn.GetLord() : null;
            return lord != null
                && lord.LordJob != null
                && lord.LordJob.GetType() == typeof(LordJob_AssaultColony)
                && lord.CurLordToil != null
                && lord.CurLordToil.GetType() == typeof(LordToil_AssaultColony);
        }

        private static bool TryContact(Pawn pawn, AwarenessSettings settings,
            out ThreatContactSnapshot contact)
        {
            var know = KnowledgeMapComponent.For(pawn.Map);
            if (settings.knowledgeContacts)
            {
                currentSight.Remove(pawn.thingIDNumber);
                contact = default;
                if (know == null) return false;
                ThreatContactSnapshot freshest;
                if (!know.TryGetFreshestContact(pawn, out freshest)) return false;
                List<ThreatContactSnapshot> contacts = know.FreshContacts(pawn);
                ThreatContactSnapshot visibleSelection;
                contact = TrySelectVisibleContact(pawn, contacts,
                    out visibleSelection) ? visibleSelection : freshest;
                return true;
            }

            Pawn nearest = null;
            float best = float.MaxValue;
            var all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (!KnowledgeMapComponent.CanCurrentlySeeHostile(pawn, other))
                    continue;
                float d = pawn.Position.DistanceToSquared(other.Position);
                if (d < best) { best = d; nearest = other; }
            }
            if (nearest != null)
            {
                ContactWeaponCategory weapon = ContactWeaponCategory.Unknown;
                if (nearest.equipment != null)
                {
                    var held = nearest.equipment.Primary;
                    weapon = held == null ? ContactWeaponCategory.Unarmed
                        : held.def.IsRangedWeapon ? ContactWeaponCategory.Ranged
                        : held.def.IsMeleeWeapon ? ContactWeaponCategory.Melee
                        : ContactWeaponCategory.Unknown;
                }
                int now = Find.TickManager.TicksGame;
                SightEpisode episode;
                if (!currentSight.TryGetValue(pawn.thingIDNumber, out episode)
                    || episode.hostileId != nearest.thingIDNumber
                    || now - episode.lastSeenTick > SightContinuityTicks)
                {
                    episode.hostileId = nearest.thingIDNumber;
                    episode.firstSeenTick = now;
                }
                episode.lastSeenTick = now;
                currentSight[pawn.thingIDNumber] = episode;
                contact = new ThreatContactSnapshot(nearest.thingIDNumber,
                    nearest.Position, now, episode.firstSeenTick, true, weapon);
                return true;
            }
            currentSight.Remove(pawn.thingIDNumber);
            contact = default;
            return false;
        }

        // Freshness answers whether a fact is still actionable; it is not a target
        // priority. Among contacts the actor can currently see, prefer one their
        // present verb can actually engage, then preserve an existing native target,
        // then use proximity and stable fact ordering. This remains a provisional
        // actor-local choice until the shared multi-axis combat assessor is built.
        private static bool TrySelectVisibleContact(Pawn pawn,
            List<ThreatContactSnapshot> contacts,
            out ThreatContactSnapshot selected)
        {
            selected = default;
            if (pawn == null || pawn.Map == null || contacts == null) return false;

            bool found = false;
            bool bestEngageable = false;
            bool bestCurrent = false;
            float bestDistance = float.MaxValue;
            int bestSourceTick = int.MinValue;
            int bestId = int.MaxValue;
            Thing currentTarget = pawn.mindState != null
                ? pawn.mindState.enemyTarget : null;

            for (int i = 0; i < contacts.Count; i++)
            {
                ThreatContactSnapshot snapshot = contacts[i];
                Pawn candidate = PawnById(pawn.Map, snapshot.HostileId);
                if (candidate == null
                    || !KnowledgeMapComponent.CanCurrentlySeeHostile(
                        pawn, candidate)) continue;

                bool engageable = false;
                Verb verb = pawn.TryGetAttackVerb(candidate,
                    CAImmediateCombat.AllowManualCombatVerb(pawn));
                if (verb != null)
                {
                    if (!verb.verbProps.IsMeleeAttack)
                        engageable = verb.CanHitTarget(candidate);
                    else
                    {
                        int radius;
                        int allies;
                        int hostiles;
                        engageable = TryMeleeCommitEnvelope(pawn, candidate,
                            snapshot, out radius, out allies, out hostiles);
                    }
                }

                bool isCurrent = candidate == currentTarget;
                float distance = pawn.Position.DistanceToSquared(candidate.Position);
                bool better = !found
                    || engageable && !bestEngageable
                    || engageable == bestEngageable && isCurrent && !bestCurrent
                    || engageable == bestEngageable && isCurrent == bestCurrent
                        && distance < bestDistance - 0.001f
                    || engageable == bestEngageable && isCurrent == bestCurrent
                        && UnityEngine.Mathf.Abs(distance - bestDistance) <= 0.001f
                        && snapshot.SourceTick > bestSourceTick
                    || engageable == bestEngageable && isCurrent == bestCurrent
                        && UnityEngine.Mathf.Abs(distance - bestDistance) <= 0.001f
                        && snapshot.SourceTick == bestSourceTick
                        && snapshot.HostileId < bestId;
                if (!better) continue;
                found = true;
                selected = snapshot;
                bestEngageable = engageable;
                bestCurrent = isCurrent;
                bestDistance = distance;
                bestSourceTick = snapshot.SourceTick;
                bestId = snapshot.HostileId;
            }
            return found;
        }

        internal static int ReactionDelay(Pawn pawn)
        {
            var d = Disposition.Of(pawn);
            float readiness = d.initiative * 0.45f + d.discipline * 0.30f
                + d.courage * 0.25f;
            float pain = pawn.health != null ? pawn.health.hediffSet.PainTotal : 0f;
            return UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Lerp(45f, 15f, readiness)
                + UnityEngine.Mathf.Clamp01(pain) * 20f);
        }

        // HumanlikeConstant's native conditional excludes drafted pawns before this
        // giver is reached. The drafted initiative map component calls the same
        // actor-local contact selection through this narrow seam; it receives no map-
        // global target list and honors the identical recognition delay.
        internal static bool TryVisiblePositionContact(Pawn pawn,
            AwarenessSettings settings, out Pawn target,
            out ThreatContactSnapshot contact)
        {
            target = null;
            contact = default(ThreatContactSnapshot);
            if (pawn == null || settings == null
                || !TryContact(pawn, settings, out contact)) return false;
            target = PawnById(pawn.Map, contact.HostileId);
            if (target == null
                || !KnowledgeMapComponent.CanCurrentlySeeHostile(
                    pawn, target)) return false;
            return Find.TickManager.TicksGame - contact.AcquiredTick
                >= ReactionDelay(pawn);
        }

        private static Job TryAudibleCueResponse(Pawn pawn)
        {
            AudibleCueMapComponent cues = AudibleCueMapComponent.For(pawn.Map);
            AudibleCueSnapshot cue;
            if (cues == null || !cues.TryGetPendingCue(pawn, out cue)) return null;
            int now = Find.TickManager.TicksGame;
            if (now - cue.AcquiredTick < ReactionDelay(pawn))
                return null;

            bool player = pawn.Faction == Faction.OfPlayer;
            CAAuthorityOrigin origin = player
                ? CAAuthorityOrigin.PlayerDelegated
                : CAAuthorityOrigin.NativeDuty;
            bool relayed = cue.Provenance != CueProvenance.DirectHearing;
            var gateContext = CABehaviorContext.ForPawn(pawn, origin,
                authoritySatisfied: true, knowledgeSatisfied: true,
                knowledgeFresh: now - cue.SourceTick <= 2500,
                liveValidated: true, knowledgeRelayed: relayed,
                knowledgeAgeTicks: now - cue.SourceTick,
                knowledgeConfidence: cue.Confidence,
                knowledgeUncertainty: cue.UncertaintyRadius,
                capabilitySatisfied: cue.ApproximateCell.IsValid
                    && cue.ApproximateCell.InBounds(pawn.Map),
                materialSatisfied: true,
                directPlayerOwnership: pawn.CurJob != null
                    && pawn.CurJob.playerForced,
                authorityBasis: player ? "personal verification initiative"
                    : "current NPC duty",
                knowledgeBasis: relayed
                    ? "relayed acoustic report with preserved source age"
                    : "directly heard acoustic event",
                owner: "one acoustic investigation");
            CABehaviorDecision gate = CABehaviorGate.EvaluateForSelection(
                "knowledge.acoustic_investigation", gateContext);
            if (!gate.SelectionApproved)
            {
                cues.MarkConsidered(pawn, cue.EventId);
                CATrace.Skip(pawn, "gunfire investigation",
                    gate.PrimaryReason, contact: cue.ApproximateCell);
                return null;
            }

            float score;
            bool investigate = AudibleCueResponse.ShouldInvestigate(
                pawn, cue, out score);
            if (!investigate)
            {
                CAIntentContext cueIntent = CACombatIntent.ActorInitiated(pawn,
                    CAIntentController.AcousticInvestigation);
                cues.MarkConsidered(pawn, cue.EventId);
                CATrace.Skip(pawn, "gunfire response",
                    "unidentified report remains inconclusive ("
                    + score.ToString("F2") + ")",
                    contact: cue.ApproximateCell, intent: cueIntent);
                return null;
            }

            Job job = AudibleCueResponse.InvestigationJob(pawn, cue,
                default(CAIntentContext));
            CABehaviorDecision decision;
            CAIntentContext investigationIntent;
            if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(pawn, job,
                "knowledge.acoustic_investigation",
                CAIntentController.AcousticInvestigation, gateContext,
                out decision, out investigationIntent,
                "acoustic event " + cue.EventId,
                "one acoustic investigation", 1200)) return null;
            AudibleCueResponse.StampInvestigationIntent(job,
                investigationIntent);
            CATrace.Pawn(pawn,
                "gunfire investigation ADOPTED for acoustic event "
                + cue.EventId + " near " + cue.ApproximateCell
                + " (score " + score.ToString("F2") + ")",
                contact: cue.ApproximateCell,
                destination: job.targetA.Cell, anchor: pawn.Position,
                intent: investigationIntent);
            return job;
        }

        private static Pawn PawnById(Map map, int id)
        {
            if (map == null) return null;
            var all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
                if (all[i].thingIDNumber == id) return all[i];
            return null;
        }

        private static ThreatContactSnapshot ContactFromVisibleTarget(Pawn target)
        {
            ContactWeaponCategory weapon = ContactWeaponCategory.Unknown;
            if (target != null && target.equipment != null)
            {
                ThingWithComps held = target.equipment.Primary;
                weapon = held == null ? ContactWeaponCategory.Unarmed
                    : held.def.IsRangedWeapon ? ContactWeaponCategory.Ranged
                    : held.def.IsMeleeWeapon ? ContactWeaponCategory.Melee
                    : ContactWeaponCategory.Unknown;
            }
            int now = Find.TickManager.TicksGame;
            return new ThreatContactSnapshot(target.thingIDNumber,
                target.Position, now, now, true, weapon);
        }

        // Unknown training is a real uncertainty, not permission to assume zero.
        // A lone melee attacker gives ground from an armed defender unless their
        // own courage/aggression is exceptional or nearby allies change the odds.
        private static bool ShouldBreakContact(Pawn pawn, Pawn target,
            ThreatContactSnapshot contact)
        {
            Verb own = pawn.TryGetAttackVerb(target, allowManualCastWeapons: true);
            if (own == null || !own.verbProps.IsMeleeAttack) return false;

            int allies = 0;
            var all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var ally = all[i];
                if (ally == pawn || ally.Dead || ally.Downed || !ally.Awake()
                    || ally.InMentalState || ally.Faction != pawn.Faction
                    || !ally.HostileTo(target)
                    || ally.WorkTagIsDisabled(WorkTags.Violent)
                    || ally.TryGetAttackVerb(null, allowManualCastWeapons: true) == null)
                    continue;
                if (!ally.Position.InHorDistOf(pawn.Position, 12f)) continue;

                // A channel proves that a report could travel; it does not itself
                // tell this pawn where the ally is or that they remain combat-ready.
                // Until ally-status facts exist, only directly observed support can
                // change immediate melee resolve.
                if (GenSight.LineOfSight(pawn.Position,
                    ally.Position, pawn.Map, true)) allies++;
            }

            var d = Disposition.Of(pawn);
            float resolve = d.courage * 0.52f + d.aggression * 0.48f
                + UnityEngine.Mathf.Min(allies, 2) * 0.16f;
            float perceived = contact.WeaponCategory == ContactWeaponCategory.Ranged ? 0.72f
                : contact.WeaponCategory == ContactWeaponCategory.Melee ? 0.66f
                : contact.WeaponCategory == ContactWeaponCategory.Unarmed ? 0.42f
                : 0.62f;
            return resolve < perceived + 0.12f;
        }

        private Job NativeFightJob(Pawn pawn)
        {
            return base.TryGiveJob(pawn);
        }

        internal static Job PlayerImmediateFight(Pawn pawn, Pawn target,
            ThreatContactSnapshot contact)
        {
            Verb verb = pawn.TryGetAttackVerb(target, allowManualCastWeapons: false);
            if (verb == null) return null;
            if (verb.verbProps.IsMeleeAttack)
            {
                int commitRadius;
                int visibleAllies;
                int visibleHostiles;
                if (!TryMeleeCommitEnvelope(pawn, target, contact,
                    out commitRadius, out visibleAllies, out visibleHostiles)) return null;
                Job melee = JobMaker.MakeJob(CA_Defs.BoundedMeleeDefense, target);
                // target C and count are the scribed origin and radius for this one
                // free-standing commitment. Duty-bound intercepts instead use their
                // authored Lord focus and radius.
                melee.targetC = pawn.Position;
                melee.count = commitRadius;
                melee.maxNumMeleeAttacks = 1;
                melee.expiryInterval = 600;
                melee.checkOverrideOnExpire = false;
                CABehaviorDecision decision;
                CAIntentContext intent;
                if (!CAImmediateCombat.TryRegisterCombatJob(pawn, melee,
                        "combat.local_reaction", CAIntentController.RaidDefense,
                        liveValidated: true, knowledgeAgeTicks: 0,
                        knowledgeConfidence: 1f, knowledgeUncertainty: 0f,
                        knowledgeRelayed: false,
                        knowledgeBasis: "direct current sight",
                        targetOrDemand: target.LabelShort + " at "
                            + target.Position, out decision, out intent))
                    return null;
                CATrace.Pawn(pawn, "commits to melee contact " + target.LabelShort
                    + " within " + commitRadius + " cells (Melee "
                    + pawn.skills.GetSkill(SkillDefOf.Melee).Level + ", local support "
                    + visibleAllies + ", visible hostiles " + visibleHostiles + ")",
                    target: target, contact: contact.Cell,
                    destination: target.Position, anchor: pawn.Position,
                    intent: intent);
                return melee;
            }
            if (!verb.CanHitTarget(target)) return null;
            Job fire = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
            fire.maxNumStaticAttacks = 2;
            fire.expiryInterval = 900;
            fire.checkOverrideOnExpire = false;
            fire.endIfCantShootTargetFromCurPos = true;
            fire.preventFriendlyFire = true;
            CABehaviorDecision rangedDecision;
            CAIntentContext rangedIntent;
            if (!CAImmediateCombat.TryRegisterCombatJob(pawn, fire,
                    "combat.local_reaction", CAIntentController.RaidDefense,
                    liveValidated: true, knowledgeAgeTicks: 0,
                    knowledgeConfidence: 1f, knowledgeUncertainty: 0f,
                    knowledgeRelayed: false,
                    knowledgeBasis: "direct current sight",
                    targetOrDemand: target.LabelShort + " at "
                        + target.Position, out rangedDecision,
                    out rangedIntent))
                return null;
            CATrace.Pawn(pawn, "commits to ranged contact " + target.LabelShort
                + " for two native attack cycles",
                target: target, contact: contact.Cell, anchor: pawn.Position,
                intent: rangedIntent);
            return fire;
        }

        private static bool TryMeleeCommitEnvelope(Pawn pawn, Pawn target,
            ThreatContactSnapshot contact, out int radius, out int visibleAllies,
            out int visibleHostiles)
        {
            radius = 0;
            visibleAllies = 0;
            visibleHostiles = 0;
            if (pawn == null || target == null || !target.Spawned
                || target.Map != pawn.Map || !target.HostileTo(pawn)
                || pawn.skills == null) return false;

            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == null || other == pawn || other.Dead || other.Downed
                    || !other.Awake()
                    || other.InMentalState) continue;
                if (other.Faction == pawn.Faction)
                {
                    if (other.WorkTagIsDisabled(WorkTags.Violent)
                        || !other.HostileTo(target)
                        || other.TryGetAttackVerb(target,
                            allowManualCastWeapons: true) == null
                        || !other.Position.InHorDistOf(pawn.Position, 12f)
                        || !GenSight.LineOfSight(pawn.Position, other.Position,
                            pawn.Map, true)) continue;
                    visibleAllies++;
                }
                else if (other.HostileTo(pawn)
                    && other.Position.InHorDistOf(pawn.Position, 14f)
                    && KnowledgeMapComponent.CanCurrentlySeeHostile(pawn, other))
                    visibleHostiles++;
            }

            int melee = pawn.skills.GetSkill(SkillDefOf.Melee).Level;
            DispositionProfile disposition = Disposition.Of(pawn);
            int technicalReach = 4 + UnityEngine.Mathf.RoundToInt(melee * 0.45f);
            int nerveReach = 4 + UnityEngine.Mathf.RoundToInt(
                (disposition.courage + disposition.aggression) * 3.5f);
            radius = UnityEngine.Mathf.Min(technicalReach, nerveReach);

            // Local pressure changes how far this individual will leave their current
            // ground; it does not invent knowledge of unseen attackers. Ranged contact
            // modestly favors closing because standing exposed without a shot is worse.
            int pressure = UnityEngine.Mathf.Max(0,
                visibleHostiles - UnityEngine.Mathf.Max(1, visibleAllies));
            radius -= UnityEngine.Mathf.Min(pressure, 3);
            if (contact.WeaponCategory == ContactWeaponCategory.Ranged) radius++;
            float pain = pawn.health != null ? pawn.health.hediffSet.PainTotal : 0f;
            radius -= UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Clamp01(pain) * 3f);
            radius = UnityEngine.Mathf.Clamp(radius, 2, 12);
            if (pawn.Position.DistanceTo(target.Position) > radius) return false;
            return PathStaysInsideMeleeEnvelope(
                pawn, target, pawn.Position, radius);
        }

        internal static bool PathStaysInsideMeleeEnvelope(Pawn pawn, Thing target,
            IntVec3 anchor, float radius)
        {
            if (pawn == null || pawn.Map == null || target == null
                || !target.Spawned || anchor.IsValid == false || radius < 1f)
                return false;
            using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                pawn.Position, target, pawn, null, PathEndMode.Touch))
            {
                if (path == null || !path.Found) return false;
                List<IntVec3> nodes = path.NodesReversed;
                for (int i = 0; i < nodes.Count; i++)
                    if (!nodes[i].InHorDistOf(anchor, radius + 0.5f))
                        return false;
                return true;
            }
        }

        private static bool IsObjectiveJob(Job job)
        {
            if (job == null) return false;
            if (job.def == JobDefOf.Ignite || job.def == JobDefOf.Goto
                || job.def == CA_AssaultDefs.SafeIgnite) return true;
            return job.def == JobDefOf.AttackMelee && job.targetA.HasThing
                && job.targetA.Thing is Building;
        }

        private static bool SafeIgniteEgressStillValid(Pawn pawn)
        {
            Job job = pawn != null ? pawn.CurJob : null;
            if (job == null || job.def != CA_AssaultDefs.SafeIgnite
                || pawn.Map == null || !job.targetC.IsValid) return false;

            IntVec3 exit = job.targetC.Cell;
            if (!exit.InBounds(pawn.Map) || !exit.WalkableBy(pawn.Map, pawn)
                || exit.Roofed(pawn.Map) || exit.ContainsStaticFire(pawn.Map))
                return false;

            Thing ignitionTarget = job.targetA.Thing;
            if (ignitionTarget == null || ignitionTarget.Destroyed
                || !ignitionTarget.IsBurning()) return false;
            return ignitionTarget.OccupiedRect().ClosestCellTo(exit).DistanceTo(exit) >= 6f;
        }

        private static Job PostureJob(Pawn pawn, IntVec3 watch, int expiry)
        {
            Job job = JobMaker.MakeJob(CA_Defs.CombatPosture);
            job.overrideFacing = watch.IsValid
                ? LordJob_CAStackBreach.FacingFromTo(pawn.Position, watch)
                : Rot4.Invalid;
            if (expiry > 0)
            {
                job.expiryInterval = expiry;
                job.checkOverrideOnExpire = true;
            }
            return job;
        }

        private static Job PendingEvasionSearchJob()
        {
            // A multi-tick path search is itself immediate-survival ownership.
            // When RimWorld is selecting after clearing curJob, returning null
            // would let lower-priority food or work givers run before the next
            // bounded search slice. This one-tick native wait blocks that fallthrough
            // and expires immediately so route evaluation resumes; it cannot become
            // an indefinite combat-watch posture.
            Job job = JobMaker.MakeJob(JobDefOf.Wait);
            job.expiryInterval = 1;
            job.checkOverrideOnExpire = true;
            return job;
        }
    }

    // Transient recognition posture. It is created by the constant think tree,
    // not by a MapComponent reissue loop. The unchanged source node makes it a
    // continuation on later 30-tick passes; deliberate duties may supersede it.
    public class JobDriver_CACombatPosture : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            Toil assess = ToilMaker.MakeToil("CACombatPosture");
            assess.initAction = delegate
            {
                pawn.pather?.StopDead();
                FaceAssignedOrKnownContact();
            };
            assess.tickAction = delegate
            {
                if (job.overrideFacing != Rot4.Invalid)
                    pawn.rotationTracker.FaceTarget(pawn.Position + job.overrideFacing.FacingCell);
                else if (pawn.IsHashIntervalTick(15)) FaceKnownContact();
                if (pawn.IsHashIntervalTick(30) && ShouldEnd())
                    EndJobWith(JobCondition.Succeeded);
            };
            assess.handlingFacing = true;
            assess.socialMode = RandomSocialMode.Off;
            assess.defaultCompleteMode = ToilCompleteMode.Never;
            yield return assess;
        }

        private void FaceAssignedOrKnownContact()
        {
            if (job.overrideFacing != Rot4.Invalid)
                pawn.rotationTracker.FaceTarget(
                    pawn.Position + job.overrideFacing.FacingCell);
            else
                FaceKnownContact();
        }

        private void FaceKnownContact()
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.knowledgeContacts) return;
            var know = KnowledgeMapComponent.For(pawn.Map);
            ThreatContactSnapshot contact;
            if (know != null && know.TryGetFreshestContact(pawn, out contact)
                && contact.Cell.IsValid)
                pawn.rotationTracker.FaceTarget(contact.Cell);
        }

        private bool ShouldEnd()
        {
            var settings = AwarenessMod.Settings;
            if (settings == null) return true;
            if (pawn.IsColonistPlayerControlled
                && !CACombatThreat.PerceivesActiveThreat(pawn)) return true;
            if (settings.knowledgeContacts)
            {
                var know = KnowledgeMapComponent.For(pawn.Map);
                if (know == null || !know.KnowsAnyThreat(pawn)) return true;
            }
            else if (!AnyCurrentlyVisibleHostile()) return true;

            // Contact resolution is actor-neutral. The remaining release gates
            // govern only the player's transient autonomous posture.
            if (!pawn.IsColonistPlayerControlled) return false;
            if (!JobGiver_CACombatReaction.PlayerMaySelfReact(pawn, settings)
                || pawn.Drafted) return true;
            if (pawn.jobs != null && pawn.jobs.jobQueue != null
                && pawn.jobs.jobQueue.AnyPlayerForced) return true;
            var withdrawal = pawn.Map.GetComponent<WithdrawalMapComponent>();
            if (withdrawal != null && withdrawal.InPlan(pawn)) return true;
            if (CATactical.IsAutomaticDefense(pawn) || CATactical.HasExplicitOrder(pawn)
                || HiddenRegistry.IsHiddenOrOrdered(pawn)) return true;
            return false;
        }

        private bool AnyCurrentlyVisibleHostile()
        {
            var all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (KnowledgeMapComponent.CanCurrentlySeeHostile(
                    pawn, other)) return true;
            }
            return false;
        }
    }
}
