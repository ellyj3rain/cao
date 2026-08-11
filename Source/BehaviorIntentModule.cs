using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Every CA-owned native job crosses this boundary exactly once. The helper
    // evaluates dynamic causal facts, creates the episode with the same authority
    // origin, and persists the receipt before the caller may return or start it.
    public static class CABehaviorJobOrigin
    {
        public static bool TryAuthorizeAndRegister(Pawn pawn, Job job,
            string behaviorKey, CAIntentController controller,
            CABehaviorContext context, out CABehaviorDecision decision,
            out CAIntentContext intent, string targetOrDemand = null,
            string ownershipScope = null, int lifetimeTicks = 60000)
        {
            intent = default(CAIntentContext);
            decision = CABehaviorGate.Evaluate(behaviorKey, context);
            if (!decision.Allowed || pawn == null || job == null
                || pawn.Map == null || context.Actor != pawn)
                return false;

            intent = CACombatIntent.Authorized(pawn, controller, behaviorKey,
                context.AuthorityOrigin,
                context.AuthorityBasis ?? decision.AuthorityBasis,
                ownershipScope ?? context.Owner ?? "pawn behavior",
                targetOrDemand);
            CABehaviorIntentMapComponent component =
                CABehaviorIntentMapComponent.For(pawn.Map);
            if (component == null
                || component.Register(pawn, job, intent,
                    lifetimeTicks) == null)
            {
                intent = default(CAIntentContext);
                return false;
            }
            return true;
        }

        // A native WorkGiver may attach provenance to the job it has already
        // selected through a registered NativeAugmentation. This seam validates
        // permission, actor, authority, capability, material, and ownership
        // without turning the augmentation into discretionary origination.
        public static bool TryRegisterNativeExecution(Pawn pawn, Job job,
            string behaviorKey, CAIntentController controller,
            CABehaviorContext context, out CABehaviorDecision decision,
            out CAIntentContext intent, string targetOrDemand = null,
            string ownershipScope = null, int lifetimeTicks = 60000)
        {
            intent = default(CAIntentContext);
            decision = CABehaviorGate.EvaluateNativeExecution(behaviorKey,
                context);
            if (!decision.Allowed || pawn == null || job == null
                || pawn.Map == null || context.Actor != pawn)
                return false;

            intent = CACombatIntent.Authorized(pawn, controller, behaviorKey,
                context.AuthorityOrigin,
                context.AuthorityBasis ?? decision.AuthorityBasis,
                ownershipScope ?? context.Owner ?? "native behavior",
                targetOrDemand);
            CABehaviorIntentMapComponent component =
                CABehaviorIntentMapComponent.For(pawn.Map);
            if (component == null
                || component.Register(pawn, job, intent,
                    lifetimeTicks) == null)
            {
                intent = default(CAIntentContext);
                return false;
            }
            return true;
        }

        public static bool TryAuthorizeAndRegister(Pawn pawn, Job job,
            string behaviorKey, CAIntentController controller,
            CABehaviorContext context, int episodeId,
            out CABehaviorDecision decision, out CAIntentContext intent,
            string targetOrDemand = null, string ownershipScope = null,
            int lifetimeTicks = 60000)
        {
            intent = default(CAIntentContext);
            decision = CABehaviorGate.Evaluate(behaviorKey, context);
            if (!decision.Allowed || pawn == null || job == null
                || pawn.Map == null || context.Actor != pawn
                || episodeId <= 0) return false;
            intent = CACombatIntent.Authorized(pawn, controller, behaviorKey,
                context.AuthorityOrigin,
                context.AuthorityBasis ?? decision.AuthorityBasis,
                ownershipScope ?? context.Owner ?? "pawn behavior",
                targetOrDemand, episodeId);
            CABehaviorIntentMapComponent component =
                CABehaviorIntentMapComponent.For(pawn.Map);
            if (component == null
                || component.Register(pawn, job, intent,
                    lifetimeTicks) == null)
            {
                intent = default(CAIntentContext);
                return false;
            }
            return true;
        }

        // A multi-actor or restored commitment keeps the shared episode and
        // original issuer while each executor receives its own evaluated
        // authority, knowledge, capability, and owned-job receipt.
        public static bool TryAuthorizeAndRegister(Pawn pawn, Job job,
            string behaviorKey, CAIntentController controller,
            CABehaviorContext context, CAIntentContext prototype,
            out CABehaviorDecision decision, out CAIntentContext intent,
            string targetOrDemand = null, string ownershipScope = null,
            int lifetimeTicks = 60000)
        {
            intent = default(CAIntentContext);
            decision = CABehaviorGate.Evaluate(behaviorKey, context);
            if (!decision.Allowed || pawn == null || job == null
                || pawn.Map == null || context.Actor != pawn
                || !prototype.IsValid) return false;
            CAAuthorityOrigin authority = context.AuthorityOrigin;
            intent = new CAIntentContext(prototype.EpisodeId,
                CAIntentAuthority.ToIntentOrigin(authority), controller,
                prototype.IssuerId, behaviorKey: behaviorKey,
                authorityOrigin: authority,
                authorityIdentity: context.AuthorityBasis
                    ?? prototype.AuthorityIdentity,
                ownershipScope: ownershipScope ?? context.Owner
                    ?? prototype.OwnershipScope,
                ownerId: pawn.thingIDNumber,
                creationTier: pawn.Faction == RimWorld.Faction.OfPlayer
                    ? AutonomyComponent.TierOf(pawn)
                    : CAInitiativeTier.Standard,
                targetOrDemand: targetOrDemand
                    ?? prototype.TargetOrDemand,
                terminationCondition: CABehaviorCatalog.Get(
                    behaviorKey)?.CompletionCondition,
                createdTick: prototype.CreatedTick);
            CABehaviorIntentMapComponent component =
                CABehaviorIntentMapComponent.For(pawn.Map);
            if (component == null
                || component.Register(pawn, job, intent,
                    lifetimeTicks) == null)
            {
                intent = default(CAIntentContext);
                return false;
            }
            return true;
        }
    }

    // Save-stable ownership receipt for CA-originated native jobs. It does not
    // schedule or supervise work. The native JobTracker remains the executor;
    // this component only answers why CA was allowed to originate that job and
    // when the bounded ownership record expires.
    public sealed class CAOwnedJobIntent : IExposable
    {
        public int PawnId = -1;
        public int JobId = -1;
        public int EpisodeId;
        public int IntentOrigin;
        public int IntentController;
        public int IssuerId = -1;
        public string BehaviorKey;
        public int AuthorityOrigin;
        public string AuthorityIdentity;
        public string OwnershipScope;
        public int OwnerId = -1;
        public int CreatedTick;
        public int CreationTier;
        public string TargetOrDemand;
        public string TerminationCondition;
        public int ExpiryTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref PawnId, "pawnId", -1);
            Scribe_Values.Look(ref JobId, "jobId", -1);
            Scribe_Values.Look(ref EpisodeId, "episodeId");
            Scribe_Values.Look(ref IntentOrigin, "intentOrigin");
            Scribe_Values.Look(ref IntentController, "intentController");
            Scribe_Values.Look(ref IssuerId, "issuerId", -1);
            Scribe_Values.Look(ref BehaviorKey, "behaviorKey");
            Scribe_Values.Look(ref AuthorityOrigin, "authorityOrigin");
            Scribe_Values.Look(ref AuthorityIdentity, "authorityIdentity");
            Scribe_Values.Look(ref OwnershipScope, "ownershipScope");
            Scribe_Values.Look(ref OwnerId, "ownerId", -1);
            Scribe_Values.Look(ref CreatedTick, "createdTick");
            Scribe_Values.Look(ref CreationTier, "creationTier");
            Scribe_Values.Look(ref TargetOrDemand, "targetOrDemand");
            Scribe_Values.Look(ref TerminationCondition,
                "terminationCondition");
            Scribe_Values.Look(ref ExpiryTick, "expiryTick");
        }

        public CAIntentContext Context()
        {
            return new CAIntentContext(EpisodeId,
                (CAIntentOrigin)IntentOrigin,
                (CAIntentController)IntentController, IssuerId,
                behaviorKey: BehaviorKey,
                authorityOrigin: (CAAuthorityOrigin)AuthorityOrigin,
                authorityIdentity: AuthorityIdentity,
                ownershipScope: OwnershipScope, ownerId: OwnerId,
                creationTier: (CAInitiativeTier)CreationTier,
                targetOrDemand: TargetOrDemand,
                terminationCondition: TerminationCondition,
                createdTick: CreatedTick);
        }
    }

    public sealed class CABehaviorIntentMapComponent : MapComponent
    {
        private const int DefaultReceiptLifetime = 60000;
        private const int DecisionObservationLifetime = 2500;
        private List<CAOwnedJobIntent> intents =
            new List<CAOwnedJobIntent>();
        private readonly Dictionary<int, CABehaviorDecisionObservation>
            recentDecisions =
                new Dictionary<int, CABehaviorDecisionObservation>();
        private int pruneCooldown;

        public CABehaviorIntentMapComponent(Map map) : base(map) { }

        public static CABehaviorIntentMapComponent For(Map map)
        {
            return map?.GetComponent<CABehaviorIntentMapComponent>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref intents, "CA_ownedJobIntents",
                LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (intents == null) intents = new List<CAOwnedJobIntent>();
                for (int i = 0; i < intents.Count; i++)
                    CACombatIntent.ObserveEpisode(intents[i].EpisodeId);
            }
        }

        public override void MapComponentTick()
        {
            if (--pruneCooldown > 0) return;
            pruneCooldown = 250;
            int now = Find.TickManager.TicksGame;
            for (int i = intents.Count - 1; i >= 0; i--)
            {
                CAOwnedJobIntent intent = intents[i];
                if (intent == null || intent.PawnId < 0
                    || intent.JobId < 0 || now > intent.ExpiryTick)
                    intents.RemoveAt(i);
            }
            if (recentDecisions.Count > 0)
            {
                var expired = new List<int>();
                foreach (KeyValuePair<int, CABehaviorDecisionObservation> pair
                    in recentDecisions)
                    if (now - pair.Value.ObservedTick
                        > DecisionObservationLifetime)
                        expired.Add(pair.Key);
                for (int i = 0; i < expired.Count; i++)
                    recentDecisions.Remove(expired[i]);
            }
        }

        // Diagnostic observation only. This never authorizes, schedules, or
        // persists behavior; it lets the selected-pawn census report the last
        // real candidate considered by a production lane instead of inventing
        // a synthetic refusal for display.
        public void ObserveDecision(Pawn pawn, CABehaviorDecision decision)
        {
            if (pawn == null || pawn.Map != map
                || string.IsNullOrEmpty(decision.BehaviorKey)) return;
            recentDecisions[pawn.thingIDNumber] =
                new CABehaviorDecisionObservation(decision.BehaviorKey,
                    decision.Allowed, decision.PrimaryBlock,
                    decision.SuggestedIntentOrigin, decision.AuthorityBasis,
                    Find.TickManager?.TicksGame ?? 0);
        }

        public bool TryGetRecentDecision(Pawn pawn,
            out CABehaviorDecisionObservation observation)
        {
            observation = default(CABehaviorDecisionObservation);
            if (pawn == null || pawn.Map != map
                || !recentDecisions.TryGetValue(pawn.thingIDNumber,
                    out observation)) return false;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now - observation.ObservedTick
                <= DecisionObservationLifetime) return true;
            recentDecisions.Remove(pawn.thingIDNumber);
            observation = default(CABehaviorDecisionObservation);
            return false;
        }

        public CAOwnedJobIntent Register(Pawn pawn, Job job,
            CAIntentContext context, int lifetimeTicks =
                DefaultReceiptLifetime)
        {
            if (pawn == null || job == null || !context.IsValid
                || string.IsNullOrEmpty(context.BehaviorKey)) return null;
            for (int i = intents.Count - 1; i >= 0; i--)
                if (intents[i].PawnId == pawn.thingIDNumber
                    && intents[i].JobId == job.loadID)
                    intents.RemoveAt(i);
            int now = Find.TickManager.TicksGame;
            var record = new CAOwnedJobIntent
            {
                PawnId = pawn.thingIDNumber,
                JobId = job.loadID,
                EpisodeId = context.EpisodeId,
                IntentOrigin = (int)context.Origin,
                IntentController = (int)context.Controller,
                IssuerId = context.IssuerId,
                BehaviorKey = context.BehaviorKey,
                AuthorityOrigin = (int)context.AuthorityOrigin,
                AuthorityIdentity = context.AuthorityIdentity,
                OwnershipScope = context.OwnershipScope,
                OwnerId = context.OwnerId,
                CreatedTick = context.CreatedTick,
                CreationTier = (int)context.CreationTier,
                TargetOrDemand = context.TargetOrDemand,
                TerminationCondition = context.TerminationCondition,
                ExpiryTick = now + lifetimeTicks
            };
            intents.Add(record);
            return record;
        }

        public bool TryGet(Pawn pawn, Job job, out CAIntentContext context)
        {
            context = default(CAIntentContext);
            if (pawn == null || job == null) return false;
            for (int i = intents.Count - 1; i >= 0; i--)
            {
                CAOwnedJobIntent record = intents[i];
                if (record.PawnId != pawn.thingIDNumber
                    || record.JobId != job.loadID) continue;
                context = record.Context();
                return context.IsValid;
            }
            return false;
        }

        public void Unregister(Pawn pawn, Job job)
        {
            if (pawn == null || job == null) return;
            for (int i = intents.Count - 1; i >= 0; i--)
                if (intents[i].PawnId == pawn.thingIDNumber
                    && intents[i].JobId == job.loadID)
                    intents.RemoveAt(i);
        }

        public string Census(Pawn pawn)
        {
            if (pawn == null) return "no pawn";
            Job job = pawn.CurJob;
            CAIntentContext context;
            if (!TryGet(pawn, job, out context))
                return "no registered CA-owned native job";
            return context.BehaviorKey + "; episode " + context.EpisodeId
                + "; authority " + context.AuthorityIdentity
                + "; owner " + context.OwnershipScope
                + "; ends when " + context.TerminationCondition;
        }
    }

    public readonly struct CABehaviorDecisionObservation
    {
        public readonly string BehaviorKey;
        public readonly bool Allowed;
        public readonly CABehaviorBlockReason PrimaryBlock;
        public readonly CAIntentOrigin SuggestedIntentOrigin;
        public readonly string AuthorityBasis;
        public readonly int ObservedTick;

        public CABehaviorDecisionObservation(string behaviorKey, bool allowed,
            CABehaviorBlockReason primaryBlock,
            CAIntentOrigin suggestedIntentOrigin, string authorityBasis,
            int observedTick)
        {
            BehaviorKey = behaviorKey;
            Allowed = allowed;
            PrimaryBlock = primaryBlock;
            SuggestedIntentOrigin = suggestedIntentOrigin;
            AuthorityBasis = authorityBasis;
            ObservedTick = observedTick;
        }

        public string PrimaryReason
        {
            get
            {
                if (Allowed) return "candidate was authorized";
                return CABehaviorDecision.ReasonFor(PrimaryBlock);
            }
        }
    }
}
