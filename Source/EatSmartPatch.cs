using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module 1: a merely-hungry colonist waits for the meal actively being cooked instead
    // of falling back on pocket garbage. HONEST SCOPE (verified against 1.6 source):
    // vanilla already refuses map kibble/raw/corpses below MealAwful for a merely-hungry
    // human - what it does NOT do is skip the unconditional inventory fallback
    // (BestFoodInInventory at DesperateOnly), which happily eats pocketed kibble or raw
    // rice while soup is minutes away. Deferring THAT fallback is this patch's real work.
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    public static class Patch_EatSmart
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.eatSmart) return true;
                if (pawn == null || pawn.Map == null || pawn.Downed) return true;
                if (!pawn.IsColonist || pawn.IsPrisoner) return true;
                // Eat smart is part of the survival floor and still runs at Directed.
                var food = pawn.needs == null ? null : pawn.needs.food;
                if (food == null) return true;

                // Never delay anyone actually in trouble.
                if (food.CurCategory >= HungerCategory.UrgentlyHungry) return true;

                // Formation discipline: a pawn holding an active assembly/
                // defense assignment - or ANY standing order while the threat
                // picture is live - does not wander off for a routine snack.
                // The formation stands until hunger is genuinely urgent.
                // Player-forced jobs never reach this giver, so an ordered
                // re-equip still completes and the order then reclaims the
                // pawn. Peacetime holds keep their needs-within-the-leash
                // duty behavior; this gate closes only when threats are known.
                // Colony policy is the third voice: legislated meal
                // discipline can relax the gate entirely or extend it to
                // any pawn under a known threat, ordered or not.
                string mealPolicy = CAPolicyLookup.Colony("meal rules");
                if (mealPolicy != "relaxed"
                    && (CATactical.IsAutomaticDefense(pawn)
                        || ((CATactical.HasExplicitOrder(pawn)
                                || mealPolicy == "strict")
                            && StandingThreatKnown(pawn))))
                {
                    CATrace.Pawn(pawn, "routine meal DEFERRED - standing "
                        + "order holds until hunger is urgent"
                        + (CATactical.IsAutomaticDefense(pawn)
                            ? " (assembly/defense assignment)"
                            : CATactical.HasExplicitOrder(pawn)
                            ? " (explicit order under a live threat picture)"
                            : " (colony policy: strict meal rules)"),
                        anchor: pawn.Position);
                    __result = null;
                    return false;
                }

                // Anticipation via the Layer-0 blackboard: is a proper meal expected soon
                // enough to be worth waiting for? The horizon is 1500-3500 ticks by the
                // pawn's discipline - the disciplined wait out a slow cook, the impatient
                // barely defer. (The cache is maintained by ColonyContextComponent; we
                // don't rescan the colony here.)
                // How long a merely-hungry pawn will wait is discipline, not a constant:
                // the disciplined wait out a slow cook, the impatient eat pocket kibble.
                int horizon = 1500 + (int)(Disposition.Of(pawn).discipline * 2000f);
                var ctx = ColonyContextComponent.For(pawn.Map);
                if (ctx == null || !ctx.MealExpectedWithin(horizon)) return true;

                // If a decent meal already exists (or a paste dispenser can serve), eat normally.
                var sources = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
                for (int i = 0; i < sources.Count; i++)
                {
                    var t = sources[i];
                    if (t == null || t.def == null) continue;
                    var disp = t as Building_NutrientPasteDispenser;
                    if (disp != null)
                    {
                        if (disp.CanDispenseNow) return true;
                        continue;
                    }
                    if (t.def.ingestible == null) continue;
                    if (t.def.ingestible.preferability < FoodPreferability.MealAwful) continue;
                    if (t.IsForbidden(pawn)) continue;
                    if (t.Position.Fogged(t.Map)) continue;
                    return true;
                }

                // Meal is coming, nothing decent on the shelf: wait instead of eating garbage.
                __result = null;
                return false;
            }
            catch (Exception)
            {
                // Any surprise: stand down and let vanilla behave.
                return true;
            }
        }

        internal static bool StandingThreatKnown(Pawn pawn)
        {
            if (CACombatThreat.PerceivesActiveThreat(pawn)) return true;
            var know = KnowledgeMapComponent.For(pawn.Map);
            if (know == null) return false;
            if (know.FreshContacts(pawn).Count > 0) return true;
            // Mechlink doctrine: linked colonists share the threat picture
            // instantly - a linked pawn is threat-aware the moment ANY linked
            // squadmate is. No personal-sighting window for the fridge.
            if (!CommsModule.HasActiveMechlink(pawn)) return false;
            var colonists = pawn.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn mate = colonists[i];
                if (mate == pawn || mate.Downed) continue;
                if (!CommsModule.HasActiveMechlink(mate)) continue;
                if (know.FreshContacts(mate).Count > 0) return true;
            }
            return false;
        }

        // A routine meal is not allowed to pull a colonist to an exposed destination
        // during a live local firefight. This operates on the job vanilla actually selected, so the trace
        // can distinguish a CA safety veto from an unobserved or inferred food desire.
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            try
            {
                ThreatContactSnapshot contact;
                Thing food;
                if (!UnsafeRoutineMeal(pawn, __result, out contact, out food))
                    return;
                TraceFoodVeto(pawn, contact, food);
                __result = null;
            }
            catch (Exception)
            {
                // Food selection is a fail-open engine seam.
            }
        }

        internal static bool CancelUnsafeCurrentMeal(Pawn pawn)
        {
            try
            {
                Job job = pawn != null ? pawn.CurJob : null;
                if (job == null || job.playerForced || pawn.jobs == null) return false;
                ThreatContactSnapshot contact;
                Thing food;
                if (!UnsafeRoutineMeal(pawn, job, out contact, out food))
                    return false;
                TraceFoodVeto(pawn, contact, food);
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                return true;
            }
            catch { return false; }
        }

        private static bool UnsafeRoutineMeal(Pawn pawn, Job job,
            out ThreatContactSnapshot contact, out Thing food)
        {
            contact = default(ThreatContactSnapshot);
            food = null;
            var s = AwarenessMod.Settings;
            if (s == null || !s.eatSmart || job == null
                || job.def != JobDefOf.Ingest || pawn == null
                || pawn.Map == null || pawn.needs?.food == null)
                return false;

            RaidResponseMapComponent raid = pawn.Map
                .GetComponent<RaidResponseMapComponent>();
            bool hasShelter = raid != null && raid.HasShelter(pawn)
                && CACombatThreat.MapHasActiveThreat(pawn.Map);
            // Urgent hunger may resume native food choice when no shelter controller
            // owns the pawn. It may not punch a hole through an active raid shelter
            // and send the pawn across the battlefield for an outside meal.
            if (pawn.needs.food.CurCategory >= HungerCategory.UrgentlyHungry
                && !hasShelter) return false;

            food = job.targetA.Thing;
            if (food == null) return false;
            Pawn_InventoryTracker holder = food.ParentHolder
                as Pawn_InventoryTracker;
            if (holder != null && holder.pawn == pawn) return false;
            if (!food.Spawned || food.Map != pawn.Map) return false;

            if (hasShelter)
            {
                // The meal is safe only when its pickup cell remains inside the
                // actor's owned shelter room. Proximity outside that envelope is
                // not safety.
                if (raid.IsInsideShelterEnvelope(pawn, food.Position))
                    return false;
                TryFoodContact(pawn, s, out contact);
                return true;
            }

            if (!TryFoodContact(pawn, s, out contact)
                || !contact.Cell.IsValid
                || pawn.Position.DistanceTo(contact.Cell) > 45f
                    && food.Position.DistanceTo(contact.Cell) > 45f) return false;

            bool sheltered = food.Position.Roofed(pawn.Map)
                && !GenSight.LineOfSight(food.Position, contact.Cell,
                    pawn.Map, true);
            return !sheltered || PathExposesPickup(pawn, food.Position,
                contact.Cell);
        }

        private static bool PathExposesPickup(Pawn pawn, IntVec3 food,
            IntVec3 contact)
        {
            using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                pawn.Position, food, pawn, null, PathEndMode.Touch))
            {
                if (path == null || !path.Found) return true;
                List<IntVec3> nodes = path.NodesReversed;
                for (int i = 0; i < nodes.Count; i++)
                {
                    IntVec3 cell = nodes[i];
                    if (!cell.InBounds(pawn.Map)) continue;
                    if (!cell.Roofed(pawn.Map)
                        && GenSight.LineOfSight(cell, contact,
                            pawn.Map, true)) return true;
                }
                return false;
            }
        }

        private static bool TryFoodContact(Pawn pawn, AwarenessSettings settings,
            out ThreatContactSnapshot contact)
        {
            contact = default(ThreatContactSnapshot);
            if (settings.knowledgeContacts)
            {
                KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
                return knowledge != null
                    && knowledge.TryGetFreshestContact(pawn, out contact);
            }

            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            Pawn closest = null;
            float best = 45f;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn hostile = pawns[i];
                if (hostile == null || hostile.Dead || !pawn.HostileTo(hostile)
                    || !KnowledgeMapComponent.CanCurrentlySeeHostile(
                        pawn, hostile, 45f)) continue;
                float distance = pawn.Position.DistanceTo(hostile.Position);
                if (distance >= best) continue;
                best = distance;
                closest = hostile;
            }
            if (closest == null) return false;
            int now = Find.TickManager.TicksGame;
            contact = new ThreatContactSnapshot(closest.thingIDNumber,
                closest.Position, now, now, true, ContactWeaponCategory.Unknown);
            return true;
        }

        private static void TraceFoodVeto(Pawn pawn,
            ThreatContactSnapshot contact, Thing food)
        {
            RaidResponseMapComponent raid = pawn.Map
                .GetComponent<RaidResponseMapComponent>();
            bool shelterOwned = raid != null && raid.HasShelter(pawn)
                && CACombatThreat.MapHasActiveThreat(pawn.Map);
            CAIntentContext intent = CACombatIntent.Autonomous(pawn,
                CAIntentController.FoodSafety);
            CATrace.Skip(pawn, "routine meal pickup",
                shelterOwned
                    ? "owned active-threat shelter excludes the food destination"
                    : "fresh hostile contact makes the exposed food route nonessential",
                contact: contact.Cell.IsValid
                    ? (IntVec3?)contact.Cell : null,
                destination: food.Position,
                anchor: pawn.Position, intent: intent);
        }
    }

    // Battle discipline for recreation: nobody meditates through a war or a
    // dying colleague. Joy defers while the pawn holds a standing order under
    // a live threat picture, OR while ANY actionable friendly casualty is
    // known to them - the second clause needs no order at all: a known
    // colleague crawling toward death outranks a joy bar on any autonomy.
    // Receipts rate-limited; the joy giver polls constantly.
    [HarmonyPatch(typeof(JobGiver_GetJoy), "TryGiveJob")]
    public static class Patch_JoyDiscipline
    {
        private static readonly Dictionary<int, int> lastReceipt =
            new Dictionary<int, int>();

        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.eatSmart) return true;
                if (pawn == null || pawn.Map == null || pawn.Downed
                    || !pawn.IsColonist || pawn.IsPrisoner) return true;

                string joyPolicy =
                    CAPolicyLookup.Colony("recreation rules");
                bool ordered = joyPolicy != "relaxed"
                    && (CATactical.HasExplicitOrder(pawn)
                        || CATactical.IsAutomaticDefense(pawn)
                        || joyPolicy == "strict")
                    && Patch_EatSmart.StandingThreatKnown(pawn);
                bool casualty = false;
                if (!ordered)
                {
                    KnowledgeMapComponent knowledge =
                        KnowledgeMapComponent.For(pawn.Map);
                    casualty = knowledge != null
                        && CACombatAftermath.HasPriorityFriendlyCasualty(
                            pawn, knowledge);
                }
                if (!ordered && !casualty) return true;

                int now = Find.TickManager.TicksGame;
                int last;
                if (!lastReceipt.TryGetValue(pawn.thingIDNumber, out last)
                    || now - last > 2500)
                {
                    lastReceipt[pawn.thingIDNumber] = now;
                    CATrace.Pawn(pawn, "recreation DEFERRED - "
                        + (ordered
                            ? "standing order under a live threat picture"
                            : "an actionable friendly casualty is known"),
                        anchor: pawn.Position);
                }
                __result = null;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    // Wander discipline: idle drifting obeys the same two clauses as
    // recreation - a standing order under a live (shared) threat picture, or
    // a known actionable casualty, roots the pawn. Wandering off mid-war is
    // the tell of an unowned moment; this closes it.
    // Target the DECLARING class: JobGiver_Wander (Verse.AI) owns
    // TryGiveJob; subclasses like WanderColony only override the wander
    // roots. The IsColonist guard keeps animals and visitors native.
    [HarmonyPatch(typeof(Verse.AI.JobGiver_Wander), "TryGiveJob")]
    public static class Patch_WanderDiscipline
    {
        private static readonly Dictionary<int, int> lastReceipt =
            new Dictionary<int, int>();

        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.eatSmart) return true;
                if (pawn == null || pawn.Map == null || pawn.Downed
                    || !pawn.IsColonist || pawn.IsPrisoner) return true;
                bool ordered = (CATactical.HasExplicitOrder(pawn)
                        || CATactical.IsAutomaticDefense(pawn))
                    && Patch_EatSmart.StandingThreatKnown(pawn);
                bool casualty = false;
                if (!ordered)
                {
                    KnowledgeMapComponent knowledge =
                        KnowledgeMapComponent.For(pawn.Map);
                    casualty = knowledge != null
                        && CACombatAftermath.HasPriorityFriendlyCasualty(
                            pawn, knowledge);
                }
                if (!ordered && !casualty) return true;
                int now = Find.TickManager.TicksGame;
                int last;
                if (!lastReceipt.TryGetValue(pawn.thingIDNumber, out last)
                    || now - last > 2500)
                {
                    lastReceipt[pawn.thingIDNumber] = now;
                    CATrace.Pawn(pawn, "idle wander DEFERRED - "
                        + (ordered
                            ? "standing order under a live threat picture"
                            : "an actionable friendly casualty is known"),
                        anchor: pawn.Position);
                }
                __result = null;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    // Rest discipline by SUBSTITUTION: a pawn heading to bed past a known
    // casualty takes them along - tend or carry-to-bed replaces the rest job;
    // rest comes right after through the ordinary need. A genuinely
    // collapsing pawn (rest below 0.12) is exempt - you cannot carry anyone
    // while falling over.
    [HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
    public static class Patch_RestDiscipline
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.eatSmart) return true;
                if (pawn == null || pawn.Map == null || pawn.Downed
                    || !pawn.IsColonist || pawn.IsPrisoner) return true;
                var rest = pawn.needs?.rest;
                if (rest == null || rest.CurLevel < 0.12f) return true;
                // Relaxed sleep rules allow this exception.
                // substitution duty entirely - bed comes first by law.
                if (CAPolicyLookup.Colony("sleep rules") == "relaxed")
                    return true;
                KnowledgeMapComponent knowledge =
                    KnowledgeMapComponent.For(pawn.Map);
                if (knowledge == null) return true;
                Job carry;
                Pawn subject;
                if (!CACombatAftermath.TryBuildPriorityRescueJob(pawn,
                    knowledge, out carry, out subject)) return true;
                // Comparative mortality is the real gate: a rescuer whose own
                // death clock is shorter than the casualty's - or too short
                // to finish the carry - cannot save her and is exempt. Eleven
                // hours against two is a duty; one against two is not.
                int ownClock = int.MaxValue;
                int subjectClock = int.MaxValue;
                try
                {
                    ownClock = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
                    subjectClock = HealthUtility
                        .TicksUntilDeathDueToBloodLoss(subject);
                }
                catch { }
                if (ownClock < subjectClock || ownClock < 5000)
                {
                    CATrace.Pawn(pawn, "rest substitution EXEMPT - own death "
                        + "clock (" + ownClock + ") shorter than "
                        + subject.LabelShort + "'s (" + subjectClock
                        + "); cannot save who outlives you",
                        anchor: pawn.Position);
                    return true;
                }
                CATrace.Pawn(pawn, "rest SUBSTITUTED - carrying/tending "
                    + subject.LabelShort
                    + " on the way in (own clock " + (ownClock == int.MaxValue
                        ? "healthy" : ownClock.ToString())
                    + " vs " + subjectClock + "); bed comes right after",
                    destination: subject.Position, anchor: pawn.Position);
                __result = carry;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
