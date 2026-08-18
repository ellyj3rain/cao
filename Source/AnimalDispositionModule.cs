using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Pillar 1 projected onto animals. These are animal terms, not reduced human
    // psychology: species priors, stable individual variation, learned training,
    // actual relationships, and current condition resolve into one legible vector.
    public struct AnimalDispositionProfile
    {
        public float vigilance;       // notices and acts on ambiguous disturbance
        public float nerve;           // holds composure instead of scattering
        public float attachment;      // anchors on a trusted master or bonded pawn
        public float defensiveDrive;  // stands alert when retreat is not compelled
    }

    public static class AnimalDisposition
    {
        private struct Cached
        {
            public AnimalDispositionProfile profile;
            public int tick;
        }

        private static readonly Dictionary<int, Cached> cache =
            new Dictionary<int, Cached>();
        private const int CacheTicks = 600;

        public static AnimalDispositionProfile Of(Pawn pawn)
        {
            if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Animal)
                return Default();

            int now = Find.TickManager.TicksGame;
            Cached cached;
            if (cache.TryGetValue(pawn.thingIDNumber, out cached)
                && now - cached.tick < CacheTicks)
                return cached.profile;

            AnimalDispositionProfile profile = Compute(pawn);
            cache[pawn.thingIDNumber] = new Cached
            {
                profile = profile,
                tick = now
            };
            return profile;
        }

        public static Pawn AnchorFor(Pawn animal)
        {
            if (animal == null) return null;

            Pawn master = animal.playerSettings != null
                ? animal.playerSettings.RespectedMaster : null;
            if (UsableAnchor(animal, master)) return master;

            Pawn bonded = animal.relations != null
                ? animal.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond)
                : null;
            return UsableAnchor(animal, bonded) ? bonded : null;
        }

        public static void ClearCache()
        {
            cache.Clear();
        }

        private static AnimalDispositionProfile Default()
        {
            return new AnimalDispositionProfile
            {
                vigilance = 0.5f,
                nerve = 0.5f,
                attachment = 0.5f,
                defensiveDrive = 0.5f
            };
        }

        private static AnimalDispositionProfile Compute(Pawn pawn)
        {
            RaceProperties race = pawn.RaceProps;
            float wildness = Mathf.Clamp01(pawn.GetStatValue(StatDefOf.Wildness));
            TrainabilityDef trainability = TrainableUtility.GetTrainability(pawn);
            float cognition = trainability != null
                ? Mathf.Clamp01(trainability.intelligenceOrder / 30f) : 0f;
            float body = Mathf.InverseLerp(0.15f, 2.5f, pawn.BodySize);
            float pain = pawn.health != null
                ? Mathf.Clamp01(pawn.health.hediffSet.PainTotal) : 0f;
            float moodShift = pawn.needs != null && pawn.needs.mood != null
                ? (pawn.needs.mood.CurLevelPercentage - 0.5f) : 0f;

            bool obedience = pawn.training != null
                && pawn.training.HasLearned(TrainableDefOf.Obedience);
            bool release = pawn.training != null
                && pawn.training.HasLearned(TrainableDefOf.Release);
            bool directedAttack = pawn.training != null
                && pawn.training.attackTarget != null;
            bool hasMaster = pawn.playerSettings != null
                && pawn.playerSettings.RespectedMaster != null;
            bool bonded = pawn.relations != null
                && pawn.relations.GetDirectRelationsCount(PawnRelationDefOf.Bond) > 0;

            AnimalDispositionProfile result = new AnimalDispositionProfile
            {
                vigilance = 0.32f
                    + wildness * 0.25f
                    + (race.predator ? 0.12f : 0f)
                    + (race.herdAnimal ? 0.08f : 0f)
                    + cognition * 0.10f,

                nerve = 0.28f
                    + body * 0.24f
                    + (race.predator ? 0.20f : 0f)
                    + Mathf.Clamp01(race.manhunterOnDamageChance) * 0.22f
                    + Mathf.Clamp01(race.petness) * 0.06f
                    + cognition * 0.05f
                    - (race.herdAnimal ? 0.06f : 0f)
                    - pain * 0.28f
                    + moodShift * 0.12f,

                attachment = 0.04f
                    + Mathf.Clamp01(race.petness) * 0.38f
                    + cognition * 0.12f
                    + (obedience ? 0.14f : 0f)
                    + (hasMaster ? 0.14f : 0f)
                    + (bonded ? 0.20f : 0f)
                    - wildness * 0.12f,

                defensiveDrive = 0.12f
                    + body * 0.14f
                    + (race.predator ? 0.22f : 0f)
                    + Mathf.Clamp01(race.manhunterOnDamageChance) * 0.30f
                    + (release ? 0.18f : 0f)
                    + (directedAttack ? 0.12f : 0f)
                    + (bonded ? 0.06f : 0f)
                    - pain * 0.08f
            };

            result.vigilance = Mathf.Clamp01(result.vigilance);
            result.nerve = Mathf.Clamp01(result.nerve);
            result.attachment = Mathf.Clamp01(result.attachment);
            result.defensiveDrive = Mathf.Clamp01(result.defensiveDrive);
            return result;
        }

        private static bool UsableAnchor(Pawn animal, Pawn anchor)
        {
            return anchor != null && !anchor.Dead && anchor.Spawned
                && animal.Spawned && anchor.Map == animal.Map;
        }
    }

    // Relevance follows conduct, never latent temperament. In particular, do not
    // use Pawn.IsCombatant(): RimWorld marks a pawn recently combatant when an
    // attacker merely selects it, which would make passive-animal protection
    // circular.
    public static class AnimalThreatRelevance
    {
        public static bool IsActiveThreat(Pawn animal)
        {
            if (animal == null || animal.RaceProps == null
                || !animal.RaceProps.Animal || animal.Dead || animal.Downed)
                return false;
            if (animal.InAggroMentalState || animal.IsFighting()) return true;
            if (animal.training != null && animal.training.attackTarget != null)
                return true;
            Pawn_MindState mind = animal.mindState;
            return mind != null
                && Find.TickManager.TicksGame - mind.lastEngageTargetTick <= 360;
        }

        public static bool IsRelevantContact(Pawn observer, Pawn target)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.enemyRestraint || observer == null
                || target == null || target.RaceProps == null
                || !target.RaceProps.Animal
                || target.Faction != Faction.OfPlayer
                || observer.Faction == null
                || !observer.Faction.HostileTo(Faction.OfPlayer))
                return true;
            return IsActiveThreat(target);
        }
    }
}
