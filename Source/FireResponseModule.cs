using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module: the grid explodes, the fire spreads, and nobody leaves the pool. Vanilla
    // pawns on joy time do not take emergency work - a home-area fire burns while
    // colonists walk and swim. Proactive+ colonists with native emergency firefighting
    // available drop unforced leisure so that lane can choose the response. Only off-home
    // colony buildings need a direct job.
    public class FireResponseMapComponent : MapComponent
    {
        private int cooldown;

        public FireResponseMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (--cooldown > 0) return;
            cooldown = 120; // fire cannot wait for a lazy cadence
            var s = AwarenessMod.Settings;
            if (s == null || !s.fireResponse) return;
            try { Respond(); }
            catch (System.Exception ex)
            {
                if (Prefs.DevMode)
                    Log.ErrorOnce("[Colonist Awareness] fire response failed: " + ex,
                        124907231);
            }
        }

        private void Respond()
        {
            var fires = map.listerThings.ThingsInGroup(ThingRequestGroup.Fire);
            if (fires == null || fires.Count == 0) return;
            var home = map.areaManager.Home;

            var homeFires = new List<Fire>();
            var outsideBuildingFires = new List<Fire>();
            for (int i = 0; i < fires.Count; i++)
            {
                var fire = fires[i] as Fire;
                if (fire == null || !fire.Spawned) continue;
                if (home[fire.Position])
                {
                    homeFires.Add(fire);
                    continue;
                }

                // RimWorld attaches fire only to pawns. A pawn standing on a colony
                // building is not evidence that the building itself is burning.
                if (fire.parent != null) continue;
                var building = fire.Position.GetFirstBuilding(map);
                if (building != null && building.Faction == Faction.OfPlayer)
                    outsideBuildingFires.Add(fire);
            }
            if (homeFires.Count == 0 && outsideBuildingFires.Count == 0) return;

            var fireWorker = WorkGiverDefOf.FightFires.Worker as WorkGiver_Scanner;

            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var p = colonists[i];
                if (p.Downed || p.Drafted || p.InMentalState || !p.Awake()) continue;
                if (CAEffectiveBehaviorProfileCache.Of(p)?
                    .Includes("hazard.fire_response") != true) continue;
                if (p.WorkTagIsDisabled(WorkTags.Firefighting)) continue;
                // The player's work priorities stand: Firefighter set to 0 means never.
                if (p.workSettings == null
                    || p.workSettings.GetPriority(WorkTypeDefOf.Firefighter) == 0) continue;
                if (PawnUtility.PlayerForcedJobNowOrSoon(p)
                    || (p.jobs != null && p.jobs.jobQueue != null
                        && p.jobs.jobQueue.AnyPlayerForced)) continue;
                var cur = p.CurJob;
                // A pawn with no current job has not completed native arbitration yet.
                // Duty-issued jobs carry their Lord; only HighPriority duties precede
                // emergency work in Humanlike.xml, while emergency work correctly
                // outranks MediumPriority duties.
                if (cur == null) continue;
                if (cur.lord != null
                    && p.mindState?.duty?.def?.hook == ThinkTreeDutyHook.HighPriority)
                    continue;
                if (cur.def == JobDefOf.BeatFire) continue;
                // Only yank pawns off LEISURE - walks, swims, recreation, idling. Pawns on
                // real work already respond through the game's own emergency firefighting.
                bool leisure = cur.def.joyKind != null
                    || (p.mindState != null && p.mindState.IsIdle);
                if (!leisure) continue;

                // Hand home-area fire response back to the native emergency-work lane.
                // HasJobOnThing supplies vanilla's faction, reservation, and handled-fire
                // gates; reachability and forbiddance mirror JobGiver_Work's scanner.
                bool nativeHomeResponse = false;
                if (fireWorker != null
                    && p.workSettings.WorkGiversInOrderEmergency.Contains(fireWorker))
                {
                    for (int j = 0; j < homeFires.Count; j++)
                    {
                        var fire = homeFires[j];
                        if (fire.IsForbidden(p)) continue;
                        if (!fireWorker.HasJobOnThing(p, fire, false)) continue;
                        if (!p.CanReach(fire, fireWorker.PathEndMode,
                            fireWorker.MaxPathDanger(p))) continue;
                        nativeHomeResponse = true;
                        break;
                    }
                }
                if (nativeHomeResponse)
                {
                    Fire observed = homeFires.Find(f => f != null
                        && f.Spawned && !f.IsForbidden(p)
                        && fireWorker.HasJobOnThing(p, f, false));
                    CABehaviorDecision decision = CABehaviorGate.Evaluate(
                        "hazard.fire_response", FireContext(p, observed,
                            capabilitySatisfied: observed != null));
                    if (decision.Allowed)
                        p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    continue;
                }

                // Vanilla refuses fires outside Home. Preserve the narrow CA extension for
                // a player building that is already burning, while respecting vanilla's
                // duplicate-handler test exactly.
                Fire best = null;
                float bestDist = float.MaxValue;
                for (int j = 0; j < outsideBuildingFires.Count; j++)
                {
                    var fire = outsideBuildingFires[j];
                    if (fire.IsForbidden(p)) continue;
                    if (FireIsBeingHandled(fire, p)) continue;
                    if ((p.Position - fire.Position).LengthHorizontalSquared > 225
                        && !p.CanReserve(fire)) continue;
                    if (!p.CanReach(fire, PathEndMode.Touch, Danger.Some)) continue;
                    float d = p.Position.DistanceTo(fire.Position);
                    if (d < bestDist) { bestDist = d; best = fire; }
                }
                if (best == null) continue;
                var job = JobMaker.MakeJob(JobDefOf.BeatFire, best);
                job.workGiverDef = WorkGiverDefOf.FightFires;
                CABehaviorDecision fireDecision;
                CAIntentContext fireIntent;
                if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(p, job,
                    "hazard.fire_response", CAIntentController.Welfare,
                    FireContext(p, best, capabilitySatisfied: true),
                    out fireDecision, out fireIntent,
                    targetOrDemand: "fire at " + best.Position,
                    ownershipScope: "delegated fire response",
                    lifetimeTicks: 2500))
                {
                    CATrace.Pawn(p, "fire response BLOCKED - "
                        + fireDecision.PrimaryReason,
                        contact: best.Position, anchor: p.Position);
                    continue;
                }
                p.jobs.StartJob(job, JobCondition.InterruptForced,
                    tag: WorkGiverDefOf.FightFires.tagToGive);
            }
        }

        private static CABehaviorContext FireContext(Pawn pawn, Fire fire,
            bool capabilitySatisfied)
        {
            Job current = pawn?.CurJob;
            return CABehaviorContext.ForPawn(pawn,
                CAAuthorityOrigin.PlayerDelegated,
                authoritySatisfied: pawn != null
                    && pawn.workSettings != null
                    && pawn.workSettings.GetPriority(
                        WorkTypeDefOf.Firefighter) > 0,
                knowledgeSatisfied: fire != null && fire.Spawned,
                knowledgeFresh: fire != null && fire.Spawned,
                liveValidated: fire != null && fire.Spawned
                    && pawn != null && pawn.Map == fire.Map,
                knowledgeRelayed: false, knowledgeAgeTicks: 0,
                capabilitySatisfied: capabilitySatisfied,
                materialSatisfied: true,
                currentIntentCompatible: current == null
                    || !current.playerForced,
                directPlayerOwnership: current != null
                    && current.playerForced,
                authorityBasis: "enabled firefighting work and local home responsibility",
                knowledgeBasis: "current local fire",
                owner: nameof(FireResponseMapComponent));
        }

        // WorkGiver_FightFires is internal. Keep this byte-for-byte behavioral mirror of
        // its FireIsBeingHandled gate rather than reaching through reflection.
        private static bool FireIsBeingHandled(Fire f, Pawn potentialHandler)
        {
            if (!f.Spawned) return false;
            return f.Map.reservationManager.FirstRespectedReserver(f, potentialHandler)
                ?.Position.InHorDistOf(f.Position, 5f) ?? false;
        }
    }
}
