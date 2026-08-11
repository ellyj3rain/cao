using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal readonly struct CAWelfareSupportDiagnosticState
    {
        public readonly bool RequesterRole;
        public readonly int RequesterId;
        public readonly int PartnerId;
        public readonly int SubjectId;
        public readonly int EpisodeId;
        public readonly int CreatedTick;
        public readonly int ReadyTick;
        public readonly int RequesterJobId;
        public readonly IntVec3 WelfareCell;
        public readonly IntVec3 ThresholdCell;
        public readonly IntVec3 FirstExteriorCell;
        public readonly IntVec3 SupportCell;
        public readonly CommunicationChannel Channel;
        public readonly bool Ready;

        public CAWelfareSupportDiagnosticState(bool requesterRole,
            CAWelfareSupportRequest request)
        {
            RequesterRole = requesterRole;
            RequesterId = request.requesterId;
            PartnerId = request.partnerId;
            SubjectId = request.subjectId;
            EpisodeId = request.episodeId;
            CreatedTick = request.createdTick;
            ReadyTick = request.readyTick;
            RequesterJobId = request.requesterJobId;
            WelfareCell = request.welfareCell;
            ThresholdCell = request.thresholdCell;
            FirstExteriorCell = request.firstExteriorCell;
            SupportCell = request.supportCell;
            Channel = request.channel;
            Ready = request.ready;
        }
    }

    internal sealed class CAWelfareSupportRequest : IExposable
    {
        public int requesterId = -1;
        public int partnerId = -1;
        public int subjectId = -1;
        public int sourceTick = -1;
        public int stateTick = -1;
        public int revision = -1;
        public int uncertaintyTicks;
        public float confidence;
        public int createdTick = -1;
        public int readyTick = -1;
        public int requesterJobId = -1;
        public int requesterReleasedTick = -1;
        public int episodeId = -1;
        public IntVec3 welfareCell = IntVec3.Invalid;
        public IntVec3 thresholdCell = IntVec3.Invalid;
        public IntVec3 firstExteriorCell = IntVec3.Invalid;
        public IntVec3 supportCell = IntVec3.Invalid;
        public CommunicationChannel channel = CommunicationChannel.None;
        public bool ready;

        public void ExposeData()
        {
            Scribe_Values.Look(ref requesterId, "requesterId", -1);
            Scribe_Values.Look(ref partnerId, "partnerId", -1);
            Scribe_Values.Look(ref subjectId, "subjectId", -1);
            Scribe_Values.Look(ref sourceTick, "sourceTick", -1);
            Scribe_Values.Look(ref stateTick, "stateTick", -1);
            Scribe_Values.Look(ref revision, "revision", -1);
            Scribe_Values.Look(ref uncertaintyTicks, "uncertaintyTicks", 0);
            Scribe_Values.Look(ref confidence, "confidence", 0f);
            Scribe_Values.Look(ref createdTick, "createdTick", -1);
            Scribe_Values.Look(ref readyTick, "readyTick", -1);
            Scribe_Values.Look(ref requesterJobId, "requesterJobId", -1);
            Scribe_Values.Look(ref requesterReleasedTick,
                "requesterReleasedTick", -1);
            Scribe_Values.Look(ref episodeId, "episodeId", -1);
            Scribe_Values.Look(ref welfareCell, "welfareCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref thresholdCell, "thresholdCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref firstExteriorCell, "firstExteriorCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref supportCell, "supportCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref channel, "channel",
                CommunicationChannel.None);
            Scribe_Values.Look(ref ready, "ready", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && episodeId > 0)
                CACombatIntent.ObserveEpisode(episodeId);
        }

        public bool Matches(WelfareFactSnapshot fact)
        {
            return subjectId == fact.SubjectId && sourceTick == fact.SourceTick
                && stateTick == fact.StateTick && revision == fact.Revision
                && welfareCell == fact.Cell;
        }

        public bool TracksSameConcern(WelfareFactSnapshot fact)
        {
            return subjectId == fact.SubjectId && welfareCell == fact.Cell;
        }

        public void RefreshConcern(WelfareFactSnapshot fact)
        {
            sourceTick = fact.SourceTick;
            stateTick = fact.StateTick;
            revision = fact.Revision;
        }
    }

    // A nonurgent welfare check that leaves a protected interior is a two-person
    // transaction while the requester knows combat is active. The requester waits;
    // a communicated, drafted ranged partner occupies a covered threshold post;
    // only then may the remembered-cell check begin. No pawn is drafted and no
    // cross-pawn StartJob call is made by the requester's think-tree evaluation.
    public class CAWelfareThresholdSupportMapComponent : MapComponent
    {
        private List<CAWelfareSupportRequest> requests =
            new List<CAWelfareSupportRequest>();
        private readonly Dictionary<int, int> nextFailureTraceTicks =
            new Dictionary<int, int>();

        private const int PendingTimeoutTicks = 600;
        private const int ReadyTimeoutTicks = 1200;
        private const float VoiceRange = 15f;

        public CAWelfareThresholdSupportMapComponent(Map map) : base(map) { }

        internal static CAWelfareThresholdSupportMapComponent For(Map map)
        {
            return map?.GetComponent<CAWelfareThresholdSupportMapComponent>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref requests, "CA_welfareThresholdSupport",
                LookMode.Deep);
            if (requests == null) requests = new List<CAWelfareSupportRequest>();
        }

        internal bool AuthorizesOrRequests(Pawn requester,
            WelfareFactSnapshot fact)
        {
            if (!RequesterStillViable(requester)) return false;
            if (RequestForPartner(requester) != null)
            {
                CATrace.Skip(requester, "outward welfare check",
                    "actor is already the bounded covering partner for another welfare transition",
                    anchor: requester.Position);
                return false;
            }
            IntVec3 threshold;
            IntVec3 exterior;
            if (!TryFindOutwardTransition(requester, fact.Cell,
                    out threshold, out exterior))
            {
                RemoveForRequester(requester.thingIDNumber, null);
                return true;
            }
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
            if (knowledge == null || !knowledge.KnowsAnyThreat(requester))
            {
                RemoveForRequester(requester.thingIDNumber, null);
                return true;
            }

            CAWelfareSupportRequest existing = RequestForRequester(
                requester.thingIDNumber);
            bool routeChanged = existing != null
                && (existing.thresholdCell != threshold
                    || existing.firstExteriorCell != exterior);
            if (existing != null && (!existing.TracksSameConcern(fact)
                    || routeChanged))
            {
                Remove(existing, "remembered welfare fact or outward route changed");
                existing = null;
            }
            else if (existing != null && !existing.Matches(fact))
            {
                // Direct or relayed observation may refresh source/state/revision
                // while the same pawn remains at the same remembered welfare cell.
                // That is newer evidence for the existing transaction, not a new
                // concern. Replacing the request here previously made a ready
                // covering pair abort and recreate itself every think pulse.
                existing.RefreshConcern(fact);
            }
            if (existing != null)
            {
                Pawn partner = FindPawn(existing.partnerId);
                if (!PartnerStillViable(partner)
                    || !CanCoverFrom(partner, existing.supportCell,
                        existing.firstExteriorCell))
                {
                    Remove(existing,
                        "assigned partner or threshold coverage became unavailable");
                    existing = null;
                }
                else if (partner.Position == existing.supportCell)
                {
                    MarkReady(existing, requester, partner);
                    return true;
                }
                else return false;
            }

            Pawn selected;
            IntVec3 supportCell;
            CommunicationChannel channel;
            if (!TrySelectPartner(requester, threshold, exterior,
                    out selected, out supportCell, out channel))
            {
                TraceFailure(requester, fact, threshold, exterior);
                return false;
            }

            var request = new CAWelfareSupportRequest
            {
                requesterId = requester.thingIDNumber,
                partnerId = selected.thingIDNumber,
                subjectId = fact.SubjectId,
                sourceTick = fact.SourceTick,
                stateTick = fact.StateTick,
                revision = fact.Revision,
                uncertaintyTicks = fact.Evidence.UncertaintyTicks,
                confidence = fact.Evidence.Confidence,
                createdTick = Find.TickManager.TicksGame,
                episodeId = CACombatIntent.NewEpisode(),
                welfareCell = fact.Cell,
                thresholdCell = threshold,
                firstExteriorCell = exterior,
                supportCell = supportCell,
                channel = channel
            };
            int now = Find.TickManager.TicksGame;
            var requestContext = CABehaviorContext.ForPawn(requester,
                CAAuthorityOrigin.PlayerDelegated,
                authoritySatisfied: selected != null
                    && channel != CommunicationChannel.None,
                knowledgeSatisfied: fact.Actionable,
                knowledgeFresh: fact.Actionable,
                liveValidated: true,
                knowledgeRelayed: !fact.Evidence.IsDirect,
                knowledgeAgeTicks: Math.Max(0, now - fact.SourceTick),
                knowledgeConfidence: fact.Evidence.Confidence,
                knowledgeUncertainty: fact.Evidence.UncertaintyTicks,
                capabilitySatisfied: supportCell.IsValid,
                materialSatisfied: supportCell.IsValid,
                currentIntentCompatible: true,
                directPlayerOwnership: requester.CurJob != null
                    && requester.CurJob.playerForced,
                authorityBasis: "bounded welfare-support request via "
                    + channel.ToString().ToLowerInvariant(),
                knowledgeBasis: fact.Evidence.IsDirect
                    ? "direct welfare concern"
                    : "relayed welfare concern with preserved source age",
                owner: "one threshold-support transaction");
            CABehaviorDecision requestDecision = CABehaviorGate.Evaluate(
                "welfare.threshold_support", requestContext);
            if (!requestDecision.Allowed)
            {
                CATrace.Skip(requester, "threshold support request",
                    requestDecision.PrimaryReason,
                    contact: exterior, destination: fact.Cell,
                    anchor: requester.Position);
                return false;
            }
            requests.Add(request);
            CATrace.Pawn(requester,
                "outward welfare check WAITS for " + selected.LabelShort
                + " to cover the protected threshold via "
                + channel.ToString().ToLowerInvariant(),
                contact: exterior, destination: fact.Cell,
                anchor: requester.Position,
                intent: new CAIntentContext(request.episodeId,
                    CAIntentOrigin.PeerRelay,
                    CAIntentController.Welfare, requester.thingIDNumber,
                    behaviorKey: "welfare.threshold_support",
                    authorityOrigin: CAAuthorityOrigin.PlayerDelegated,
                    authorityIdentity: requestContext.AuthorityBasis,
                    ownershipScope: requestContext.Owner,
                    targetOrDemand: "welfare concern " + fact.SubjectId));
            CATrace.Pawn(selected,
                "receives bounded threshold-support request from "
                + requester.LabelShort + "; support post " + supportCell
                + ", first exterior cell " + exterior,
                contact: exterior, destination: supportCell,
                anchor: selected.Position,
                intent: new CAIntentContext(request.episodeId,
                    CAIntentOrigin.PeerRelay,
                    CAIntentController.Welfare, requester.thingIDNumber));
            return false;
        }

        internal bool TryGiveSupportMove(Pawn partner, out Job result)
        {
            result = null;
            CAWelfareSupportRequest request = RequestForPartner(partner);
            if (request == null || request.ready) return false;
            Pawn requester = FindPawn(request.requesterId);
            if (!RequesterStillViable(requester)
                || !ProtectedInterior(requester.Position, map)
                || !PartnerStillViable(partner))
            {
                Remove(request, "requester or assigned partner became unavailable");
                return false;
            }
            if (partner.Position == request.supportCell)
            {
                MarkReady(request, requester, partner);
                return false;
            }
            result = CAImmediateCombat.MakeLockedCombatMoveJob(partner,
                request.supportCell, 240, null);
            if (result == null)
            {
                Remove(request, "assigned support post no longer has an executable route");
                return false;
            }
            result.count = request.episodeId;
            var supportContext = SupportContext(partner, request,
                CAAuthorityOrigin.PeerRequest,
                "accepted peer-support assignment");
            CABehaviorDecision decision;
            CAIntentContext supportIntent;
            if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(partner, result,
                "welfare.threshold_support", CAIntentController.Welfare,
                supportContext, request.episodeId, out decision,
                out supportIntent, "support pawn " + request.requesterId,
                "one threshold-support transaction", ReadyTimeoutTicks))
            {
                Remove(request, decision.PrimaryReason);
                result = null;
                return false;
            }
            CATrace.Pawn(partner,
                "threshold support MOVES to cover " + requester.LabelShort
                + " before exterior welfare travel",
                contact: request.firstExteriorCell,
                destination: request.supportCell, anchor: partner.Position,
                intent: supportIntent);
            return true;
        }

        internal bool TryMakeRequesterWaitJob(Pawn requester, out Job result)
        {
            result = null;
            CAWelfareSupportRequest request = RequestForRequester(
                requester?.thingIDNumber ?? -1);
            if (request == null || request.ready
                || !RequesterStillViable(requester)) return false;
            if (requester.CurJob != null
                && requester.CurJob.def == CA_Defs.WaitForWelfareSupport
                && requester.CurJob.count == request.episodeId)
            {
                result = requester.CurJob;
                return true;
            }
            result = JobMaker.MakeJob(CA_Defs.WaitForWelfareSupport);
            result.count = request.episodeId;
            result.expiryInterval = PendingTimeoutTicks;
            result.checkOverrideOnExpire = true;
            var waitContext = SupportContext(requester, request,
                CAAuthorityOrigin.Continuation,
                "retained threshold-support request");
            CABehaviorDecision decision;
            CAIntentContext waitIntent;
            if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(requester,
                result, "welfare.threshold_support",
                CAIntentController.Welfare, waitContext, request.episodeId,
                out decision, out waitIntent,
                "support pawn " + request.partnerId,
                "one threshold-support transaction", PendingTimeoutTicks))
            {
                Remove(request, decision.PrimaryReason);
                result = null;
                return false;
            }
            CATrace.Pawn(requester,
                "holds the protected interior while threshold support forms",
                contact: request.firstExteriorCell,
                destination: request.thresholdCell,
                anchor: requester.Position,
                intent: waitIntent);
            return true;
        }

        private CABehaviorContext SupportContext(Pawn actor,
            CAWelfareSupportRequest request, CAAuthorityOrigin origin,
            string authorityBasis)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return CABehaviorContext.ForPawn(actor, origin,
                authoritySatisfied: request != null
                    && request.episodeId > 0,
                knowledgeSatisfied: request != null
                    && request.welfareCell.IsValid,
                knowledgeFresh: request != null
                    && now - request.sourceTick <= 2500,
                liveValidated: true, knowledgeRelayed: true,
                knowledgeAgeTicks: request == null ? int.MaxValue
                    : Math.Max(0, now - request.sourceTick),
                knowledgeConfidence: request?.confidence ?? 0f,
                knowledgeUncertainty: request?.uncertaintyTicks ?? 0,
                capabilitySatisfied: actor != null && actor.Spawned
                    && !actor.Downed,
                materialSatisfied: request != null
                    && request.supportCell.IsValid,
                currentIntentCompatible: true,
                directPlayerOwnership: actor?.CurJob != null
                    && actor.CurJob.playerForced,
                authorityBasis: authorityBasis,
                knowledgeBasis: "communicated welfare concern; source age preserved",
                owner: "one threshold-support transaction");
        }

        internal bool RequesterWaitStillPending(Pawn requester, Job job)
        {
            CAWelfareSupportRequest request = RequestForRequester(
                requester?.thingIDNumber ?? -1);
            if (request == null || job == null
                || job.def != CA_Defs.WaitForWelfareSupport
                || job.count != request.episodeId || request.ready)
                return false;
            if (RequesterStillViable(requester)
                && ProtectedInterior(requester.Position, map)) return true;
            Remove(request,
                "requester no longer remains viable for exterior welfare travel");
            return false;
        }

        internal bool RetainsSupportPost(Pawn partner)
        {
            CAWelfareSupportRequest request = RequestForPartner(partner);
            if (request == null || !request.ready
                || partner.Position != request.supportCell) return false;
            Pawn requester = FindPawn(request.requesterId);
            if (!RequesterStillViable(requester)
                || !PartnerStillViable(partner))
            {
                Remove(request, "requester or covering partner became unavailable");
                return false;
            }
            return true;
        }

        internal bool TryGetDiagnosticState(Pawn pawn,
            out CAWelfareSupportDiagnosticState state)
        {
            state = default(CAWelfareSupportDiagnosticState);
            if (pawn == null) return false;
            for (int i = 0; i < requests.Count; i++)
            {
                CAWelfareSupportRequest request = requests[i];
                if (request == null) continue;
                if (request.requesterId == pawn.thingIDNumber)
                {
                    state = new CAWelfareSupportDiagnosticState(true, request);
                    return true;
                }
                if (request.partnerId == pawn.thingIDNumber)
                {
                    state = new CAWelfareSupportDiagnosticState(false, request);
                    return true;
                }
            }
            return false;
        }

        internal void MarkRequesterReleased(Pawn requester, Job job)
        {
            CAWelfareSupportRequest request = RequestForRequester(
                requester?.thingIDNumber ?? -1);
            if (request == null || !request.ready || job == null) return;
            request.requesterJobId = job.loadID;
            request.requesterReleasedTick = Find.TickManager.TicksGame;
            CATrace.Pawn(requester,
                "covered welfare transition RELEASES after "
                + FindPawn(request.partnerId)?.LabelShort
                + " establishes threshold support",
                contact: request.firstExteriorCell,
                destination: request.welfareCell, anchor: requester.Position,
                intent: new CAIntentContext(request.episodeId,
                    CAIntentOrigin.Continuation,
                    CAIntentController.Welfare, requester.thingIDNumber));
        }

        internal void AbortRequester(Pawn requester, string reason)
        {
            CAWelfareSupportRequest request = RequestForRequester(
                requester?.thingIDNumber ?? -1);
            if (request != null) Remove(request, reason);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager.TicksGame;
            if (now % 60 != 0) return;
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                CAWelfareSupportRequest request = requests[i];
                if (request == null)
                {
                    requests.RemoveAt(i);
                    continue;
                }
                Pawn requester = FindPawn(request.requesterId);
                Pawn partner = FindPawn(request.partnerId);
                if (!request.ready && RequesterStillViable(requester)
                    && ProtectedInterior(requester.Position, map)
                    && PartnerStillViable(partner)
                    && partner.Position == request.supportCell)
                    MarkReady(request, requester, partner);
                bool expired = !request.ready
                    ? now - request.createdTick > PendingTimeoutTicks
                    : now - request.readyTick > ReadyTimeoutTicks;
                bool releasedJobEnded = request.requesterJobId > 0
                    && now - request.requesterReleasedTick > 120
                    && (requester?.CurJob == null
                        || requester.CurJob.loadID != request.requesterJobId);
                bool requesterInvalid = !RequesterStillViable(requester)
                    || !request.ready
                        && !ProtectedInterior(requester.Position, map);
                bool partnerInvalid = !PartnerStillViable(partner);
                if (requesterInvalid || partnerInvalid || expired
                    || releasedJobEnded)
                    RemoveAt(i, expired ? "bounded support interval expired"
                        : releasedJobEnded
                            ? "covered welfare job ended"
                            : HasDirectWork(requester) || HasDirectWork(partner)
                                ? "direct operator work superseded the transaction"
                                : "request participant became unavailable");
            }
        }

        private bool TrySelectPartner(Pawn requester, IntVec3 threshold,
            IntVec3 exterior, out Pawn selected, out IntVec3 supportCell,
            out CommunicationChannel channel)
        {
            selected = null;
            supportCell = IntVec3.Invalid;
            channel = CommunicationChannel.None;
            float best = float.MinValue;
            IReadOnlyList<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == requester || !candidate.Drafted
                    || !CABehaviorGate.StableProfileAllows(candidate,
                        "welfare.threshold_support")
                    || !PartnerStillViable(candidate)
                    || !CADraftedCombatInitiativeMapComponent
                        .IsIdleDraftedWatch(candidate)
                    || RequestForPartner(candidate) != null
                    || RequestForRequester(candidate.thingIDNumber) != null)
                    continue;
                float distance = requester.Position.DistanceTo(candidate.Position);
                if (distance > 24f) continue;
                CommunicationChannel candidateChannel;
                if (!CommsModule.TryGetStrategicChannel(requester, candidate,
                        VoiceRange, out candidateChannel)) continue;
                IntVec3 candidateCell;
                float postScore;
                if (!TryFindSupportCell(candidate, requester, threshold,
                        exterior, out candidateCell, out postScore)) continue;
                float role = SquadComponent.SquadOf(requester) != 0
                        && SquadComponent.SquadOf(requester)
                            == SquadComponent.SquadOf(candidate) ? 0.8f : 0f;
                if (role > 0f && SquadComponent.FireteamOf(requester) != 0
                    && SquadComponent.FireteamOf(requester)
                        == SquadComponent.FireteamOf(candidate)) role += 0.8f;
                float score = postScore + role - distance * 0.035f;
                if (selected != null && score <= best) continue;
                selected = candidate;
                supportCell = candidateCell;
                channel = candidateChannel;
                best = score;
            }
            return selected != null;
        }

        private bool TryFindSupportCell(Pawn partner, Pawn requester,
            IntVec3 threshold, IntVec3 exterior, out IntVec3 result,
            out float resultScore)
        {
            result = IntVec3.Invalid;
            resultScore = float.MinValue;
            int limit = GenRadial.NumCellsInRadius(4.5f);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 cell = threshold + GenRadial.RadialPattern[i];
                if (cell == threshold || cell == requester.Position
                    || !cell.InBounds(map) || !ProtectedInterior(cell, map)
                    || !cell.WalkableBy(map, partner)
                    || !cell.InAllowedArea(partner)
                    || !map.pawnDestinationReservationManager.CanReserve(
                        cell, partner)
                    || !partner.CanReach(cell, PathEndMode.OnCell, Danger.Some)
                    || !CASpatialCombatMemoryMapComponent.KnowsCell(partner,
                        cell) || CellOccupied(cell, partner)) continue;
                if (!CanCoverFrom(partner, cell, exterior)) continue;
                float cover = CoverUtility.CalculateOverallBlockChance(cell,
                    exterior, map);
                float distance = cell.DistanceTo(threshold);
                float requesterSpacing = cell.DistanceTo(requester.Position);
                float score = cover * 2f - distance * 0.18f
                    + (requesterSpacing >= 2f && requesterSpacing <= 7f
                        ? 0.45f : 0f);
                if (result.IsValid && score <= resultScore) continue;
                result = cell;
                resultScore = score;
            }
            return result.IsValid;
        }

        private bool CanCoverFrom(Pawn partner, IntVec3 cell,
            IntVec3 exterior)
        {
            if (partner == null || !cell.IsValid || !exterior.IsValid
                || !cell.InBounds(map) || !exterior.InBounds(map)) return false;
            Verb verb = RangedVerb(partner);
            return verb != null && GenSight.LineOfSight(cell, exterior, map,
                    true)
                && verb.CanHitTargetFrom(cell, new LocalTargetInfo(exterior));
        }

        private static Verb RangedVerb(Pawn pawn)
        {
            Verb primary = pawn?.equipment?.Primary
                ?.TryGetComp<CompEquippable>()?.PrimaryVerb;
            if (primary != null && !primary.verbProps.IsMeleeAttack)
                return primary;
            ThingWithComps offhand = OffhandComponent.GetOffhand(pawn);
            Verb secondary = offhand?.TryGetComp<CompEquippable>()?.PrimaryVerb;
            return secondary != null && !secondary.verbProps.IsMeleeAttack
                ? secondary : null;
        }

        private bool TryFindOutwardTransition(Pawn requester,
            IntVec3 destination, out IntVec3 threshold,
            out IntVec3 firstExterior)
        {
            threshold = IntVec3.Invalid;
            firstExterior = IntVec3.Invalid;
            if (!destination.IsValid || !destination.InBounds(map)
                || !ProtectedInterior(requester.Position, map)) return false;
            using (PawnPath path = map.pathFinder.FindPathNow(
                requester.Position, destination, requester,
                PathFinderCostTuning.For(requester), PathEndMode.Touch))
            {
                if (path == null || !path.Found) return false;
                List<IntVec3> nodes = path.NodesReversed;
                IntVec3 previous = requester.Position;
                bool previousProtected = true;
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    IntVec3 cell = nodes[i];
                    if (!cell.InBounds(map)) continue;
                    bool protectedCell = ProtectedInterior(cell, map);
                    if (previousProtected && !protectedCell)
                    {
                        threshold = previous;
                        firstExterior = cell;
                        return threshold.IsValid && firstExterior.IsValid;
                    }
                    previous = cell;
                    previousProtected = protectedCell;
                }
            }
            return false;
        }

        private static bool ProtectedInterior(IntVec3 cell, Map map)
        {
            if (!cell.IsValid || map == null || !cell.InBounds(map)
                || !cell.Roofed(map)) return false;
            Room room = cell.GetRoom(map);
            return room != null && room.ProperRoom && !room.TouchesMapEdge
                && !room.PsychologicallyOutdoors;
        }

        private bool PartnerStillViable(Pawn pawn)
        {
            if (pawn == null || pawn.Map != map || !pawn.Spawned || pawn.Dead
                || pawn.Downed || !pawn.Drafted || !pawn.Awake()
                || pawn.InMentalState
                || pawn.WorkTagIsDisabled(WorkTags.Violent)
                || !CAImmediateCombat.HasRangedWeaponRole(pawn)
                || CAImmediateCombat.RequiresCombatRecoveryNow(pawn))
                return false;
            if (pawn.CurJob != null && pawn.CurJob.playerForced) return false;
            return pawn.jobs?.jobQueue == null
                || !pawn.jobs.jobQueue.AnyPlayerForced;
        }

        private bool RequesterStillViable(Pawn pawn)
        {
            return pawn != null && pawn.Map == map && pawn.Spawned && !pawn.Dead
                && !pawn.Downed && pawn.Awake() && !pawn.InMentalState
                && !HasDirectWork(pawn)
                && !CACombatThreat.PerceivesActiveThreat(pawn)
                && (!CAImmediateCombat.RequiresCombatRecoveryNow(pawn)
                    || CAImmediateCombat.CanPerformBoundedWelfareResponse(pawn));
        }

        private static bool HasDirectWork(Pawn pawn)
        {
            return (pawn?.CurJob != null && pawn.CurJob.playerForced)
                || (pawn?.jobs?.jobQueue != null
                    && pawn.jobs.jobQueue.AnyPlayerForced);
        }

        private bool CellOccupied(IntVec3 cell, Pawn partner)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Pawn pawn = things[i] as Pawn;
                if (pawn != null && pawn != partner && !pawn.Dead) return true;
            }
            return false;
        }

        private void MarkReady(CAWelfareSupportRequest request,
            Pawn requester, Pawn partner)
        {
            if (request.ready) return;
            request.ready = true;
            request.readyTick = Find.TickManager.TicksGame;
            CATrace.Pawn(partner,
                "threshold support ESTABLISHED; holds the protected side while "
                + requester.LabelShort + " evaluates exterior welfare travel",
                contact: request.firstExteriorCell,
                destination: request.supportCell, anchor: partner.Position,
                intent: new CAIntentContext(request.episodeId,
                    CAIntentOrigin.Continuation,
                    CAIntentController.Welfare, request.requesterId));
        }

        private CAWelfareSupportRequest RequestForRequester(int requesterId)
        {
            for (int i = 0; i < requests.Count; i++)
                if (requests[i] != null
                    && requests[i].requesterId == requesterId)
                    return requests[i];
            return null;
        }

        private CAWelfareSupportRequest RequestForPartner(Pawn partner)
        {
            if (partner == null) return null;
            for (int i = 0; i < requests.Count; i++)
                if (requests[i] != null
                    && requests[i].partnerId == partner.thingIDNumber)
                    return requests[i];
            return null;
        }

        private Pawn FindPawn(int id)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i] != null && pawns[i].thingIDNumber == id)
                    return pawns[i];
            return null;
        }

        private void RemoveForRequester(int requesterId, string reason)
        {
            for (int i = requests.Count - 1; i >= 0; i--)
                if (requests[i] != null
                    && requests[i].requesterId == requesterId)
                    RemoveAt(i, reason);
        }

        private void Remove(CAWelfareSupportRequest request, string reason)
        {
            int index = requests.IndexOf(request);
            if (index >= 0) RemoveAt(index, reason);
        }

        private void RemoveAt(int index, string reason)
        {
            CAWelfareSupportRequest request = requests[index];
            Pawn requester = FindPawn(request.requesterId);
            Pawn partner = FindPawn(request.partnerId);
            if (!string.IsNullOrEmpty(reason) && requester != null)
                CATrace.Pawn(requester,
                    "covered welfare transition ABORTS - " + reason,
                    contact: request.firstExteriorCell,
                    destination: request.welfareCell,
                    anchor: requester.Position,
                    intent: new CAIntentContext(request.episodeId,
                        CAIntentOrigin.Continuation,
                        CAIntentController.Welfare, request.requesterId));
            if (!string.IsNullOrEmpty(reason) && partner != null)
                CATrace.Pawn(partner,
                    "threshold support RELEASES - " + reason,
                    contact: request.firstExteriorCell,
                    destination: request.supportCell, anchor: partner.Position,
                    intent: new CAIntentContext(request.episodeId,
                        CAIntentOrigin.Continuation,
                        CAIntentController.Welfare, request.requesterId));
            if (partner?.CurJob != null
                && partner.CurJob.def == CA_Defs.CombatMove
                && partner.CurJob.count == request.episodeId
                && !partner.CurJob.playerForced)
            {
                partner.pather?.StopDead();
                partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            requests.RemoveAt(index);
        }

        private void TraceFailure(Pawn requester, WelfareFactSnapshot fact,
            IntVec3 threshold, IntVec3 exterior)
        {
            int now = Find.TickManager.TicksGame;
            int next;
            if (nextFailureTraceTicks.TryGetValue(requester.thingIDNumber,
                    out next) && now < next) return;
            nextFailureTraceTicks[requester.thingIDNumber] = now + 300;
            CATrace.Skip(requester, "outward welfare check",
                "known combat route leaves protected interior at " + exterior
                + " but no communicated drafted ranged partner can cover threshold "
                + threshold + "; remembered concern " + fact.SubjectId
                + " remains pending", anchor: requester.Position);
        }
    }

    public class JobDriver_CAWelfareSupportWait : JobDriver_Wait
    {
        public override bool IsContinuation(Job candidate)
        {
            return candidate != null && candidate.def == job.def
                && candidate.count == job.count;
        }

        public override void DecorateWaitToil(Toil wait)
        {
            base.DecorateWaitToil(wait);
            wait.tickIntervalAction += delegate(int delta)
            {
                if (!GenTicks.IsTickIntervalDelta(pawn.thingIDNumber, 15,
                        delta)) return;
                CAWelfareThresholdSupportMapComponent support =
                    CAWelfareThresholdSupportMapComponent.For(pawn.Map);
                if (support == null
                    || !support.RequesterWaitStillPending(pawn, job))
                    EndJobWith(JobCondition.Succeeded);
            };
        }
    }
}
