using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Module 10: enemies show restraint toward targets that pose no threat and hold value -
    // the downed, children, and owned animals - unless attacked by them. Restraint scales
    // with the attacker's intelligence and is undercut by bloodlust/psychopathy, so it
    // fails rarely and in character.
    // Raiders don't let their prize bleed out mid-extraction: a carried, badly bleeding
    // captive gets a crude battlefield dressing from the carrier.
    public class RaiderStabilizeMapComponent : MapComponent
    {
        private const int IntervalTicks = 250;

        public RaiderStabilizeMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % IntervalTicks != 0) return;
            var s = AwarenessMod.Settings;
            if (!CABehaviorSettings.IsEnabled(
                    CASettingKey.EnemyRestraint, s)) return;
            try
            {
                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    var r = pawns[i];
                    if (r == null || !r.RaceProps.Humanlike || r.Dead || r.Downed) continue;
                    if (r.Faction == null || !r.Faction.HostileTo(Faction.OfPlayer)) continue;
                    var carried = r.carryTracker != null ? r.carryTracker.CarriedThing as Pawn : null;
                    if (carried == null || carried.Dead || !carried.Downed) continue;
                    if (carried.health == null || carried.health.hediffSet.BleedRateTotal < 0.25f) continue;
                    var context = CABehaviorContext.ForPawn(r,
                        CAAuthorityOrigin.NativeDuty,
                        authoritySatisfied: r.CurJob != null,
                        knowledgeSatisfied: true, knowledgeFresh: true,
                        liveValidated: carried.Spawned || r.carryTracker
                            ?.CarriedThing == carried,
                        knowledgeRelayed: false, knowledgeAgeTicks: 0,
                        knowledgeConfidence: 1f,
                        knowledgeUncertainty: 0f,
                        capabilitySatisfied: r.health != null
                            && !r.Downed,
                        materialSatisfied: carried.health != null,
                        currentIntentCompatible: true,
                        directPlayerOwnership: false,
                        authorityBasis: "current raider custody duty",
                        knowledgeBasis:
                            "directly carried captive with current bleeding",
                        owner: "raider captive custody");
                    CABehaviorDecision decision = CABehaviorGate
                        .EvaluateForSelection(
                        "npc.captive_stabilization", context);
                    if (!decision.SelectionApproved) continue;
                    TendUtility.DoTend(r, carried, null);
                    Messages.Message(r.LabelShortCap + " field-dressed " + carried.LabelShortCap + " before carrying them off.",
                        new TargetInfo(r.Position, map), MessageTypeDefOf.NeutralEvent, historical: false);
                }
            }
            catch { }
        }
    }

    public static class EnemyRestraintModule
    {
        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(AttackTargetFinder), "GetShootingTargetScore");
                if (m == null) { Log.Warning("[Colonist Awareness] GetShootingTargetScore not found; enemy restraint off"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(EnemyRestraintModule), "ScorePostfix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] enemy restraint install failed: " + e.Message);
            }
            try
            {
                var bt = AccessTools.Method(typeof(AttackTargetFinder), "BestAttackTarget");
                if (bt == null) { Log.Warning("[Colonist Awareness] BestAttackTarget not found; melee restraint off"); return; }
                harmony.Patch(bt, prefix: new HarmonyMethod(typeof(EnemyRestraintModule), "BestTargetPrefix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] melee restraint install failed: " + e.Message);
            }
            try
            {
                var kv = AccessTools.Method(typeof(KidnapAIUtility), "TryFindGoodKidnapVictim");
                if (kv == null) { Log.Warning("[Colonist Awareness] TryFindGoodKidnapVictim not found; baby kidnap gate off"); return; }
                harmony.Patch(kv, postfix: new HarmonyMethod(typeof(EnemyRestraintModule), "KidnapPostfix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] baby kidnap gate install failed: " + e.Message);
            }
        }

        // Wrap the target validator: restraint AND concealment are hard filters for melee
        // and ranged alike. Hidden pawns are invisible to humanlike enemies here even if
        // some vanilla path skips the ThreatDisabled check; animals are exempt - scent
        // does not care about a bush.
        public static void BestTargetPrefix(IAttackTargetSearcher searcher, ref System.Predicate<Thing> validator)
        {
            var s = AwarenessMod.Settings;
            if (!CABehaviorSettings.IsEnabled(CASettingKey.EnemyRestraint, s)
                && !CABehaviorSettings.IsEnabled(
                    CASettingKey.SurvivalResponses, s)) return;
            var shooter = searcher == null ? null : searcher.Thing as Pawn;
            if (shooter == null || !shooter.RaceProps.Humanlike) return;
            if (shooter.Faction == null || !shooter.Faction.HostileTo(Faction.OfPlayer)) return;

            var original = validator;
            var atk = shooter;
            bool restraint = CABehaviorSettings.IsEnabled(
                CASettingKey.EnemyRestraint, s);
            bool concealment = CABehaviorSettings.IsEnabled(
                CASettingKey.SurvivalResponses, s);
            validator = delegate (Thing t)
            {
                if (original != null && !original(t)) return false;
                if (concealment && t is Pawn tp && HiddenRegistry.IsHidden(tp)) return false;
                if (restraint && !AllowedByRestraint(atk, t)) return false;
                return true;
            };
        }

        private static bool AllowedByRestraint(Pawn shooter, Thing t)
        {
            try
            {
                var tp = t as Pawn;
                if (tp == null) return true;
                if (tp.mindState != null && ReferenceEquals(tp.mindState.enemyTarget, shooter)) return true;

                // Children and babies are not preferred targets - but a hard "never"
                // starves the enemy AI of targets entirely (raid stall) and makes an
                // armed child fighting on the line untargetable by everyone except the
                // single raider it is shooting. A combatant child is a combatant; a
                // noncombatant child is spared by the restraint roll like other
                // protected targets, heavily weighted toward sparing.
                if (tp.RaceProps.Humanlike && tp.DevelopmentalStage != DevelopmentalStage.Adult)
                {
                    if (PawnUtility.IsFighting(tp)) return true;
                    float cf = RestraintFactor(shooter);
                    return Rand.ChanceSeeded(UnityEngine.Mathf.Clamp01(0.5f - cf),
                        (shooter.thingIDNumber * 613) ^ t.thingIDNumber);
                }

                if (tp.RaceProps.Animal && tp.Faction == Faction.OfPlayer)
                {
                    if (AnimalThreatRelevance.IsActiveThreat(tp)) return true;

                    // A visible passive animal is not ordinary assault work. A
                    // genuinely rare low-restraint exception remains possible,
                    // stable for this attacker/target incident instead of rerolled
                    // on every target scan.
                    float restraint = Mathf.Clamp01(RestraintFactor(shooter));
                    float rareChance = Mathf.Lerp(0.005f, 0.08f,
                        1f - restraint);
                    return Rand.ChanceSeeded(rareChance,
                        AnimalExceptionSeed(shooter, tp));
                }

                bool protectedTarget = tp.Downed;
                if (!protectedTarget) return true;
                float f = RestraintFactor(shooter);
                return Rand.ChanceSeeded(UnityEngine.Mathf.Clamp01(1f - f),
                    (shooter.thingIDNumber * 391) ^ t.thingIDNumber);
            }
            catch { return true; }
        }

        // Babies and children only become kidnap candidates once no adult resistance stands.
        public static void KidnapPostfix(ref bool __result, ref Pawn victim, Pawn kidnapper)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (!CABehaviorSettings.IsEnabled(
                        CASettingKey.EnemyRestraint, s)) return;
                if (!__result || victim == null) return;
                if (!victim.RaceProps.Humanlike || victim.DevelopmentalStage == DevelopmentalStage.Adult) return;
                var map = kidnapper != null ? kidnapper.Map : victim.Map;
                if (map == null) return;
                var colonists = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < colonists.Count; i++)
                {
                    var c = colonists[i];
                    if (c.Dead || c.Downed) continue;
                    if (c.DevelopmentalStage != DevelopmentalStage.Adult) continue;
                    victim = null;
                    __result = false;
                    return;
                }
            }
            catch { }
        }

        private static float RestraintFactor(Pawn shooter)
        {
            // Restraint IS empathy, shaded by deliberation - the disposition vector
            // already carries Kind/Bloodlust/Psychopath and the rest of who they are.
            float f = Disposition.Of(shooter).empathy * 1.0f;
            if (shooter.skills != null)
                f += 0.03f * shooter.skills.GetSkill(SkillDefOf.Intellectual).Level;
            return UnityEngine.Mathf.Clamp(f, 0f, 1.2f);
        }

        private static int AnimalExceptionSeed(Pawn shooter, Pawn animal)
        {
            Lord lord = shooter.GetLord();
            int incident = lord != null ? lord.loadID
                : shooter.Map != null ? shooter.Map.uniqueID : 0;
            return Gen.HashCombineInt(shooter.thingIDNumber,
                animal.thingIDNumber, incident, 948629);
        }

        public static void ScorePostfix(IAttackTarget target, IAttackTargetSearcher searcher, ref float __result)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (!CABehaviorSettings.IsEnabled(
                        CASettingKey.EnemyRestraint, s)) return;

                var shooter = searcher == null ? null : searcher.Thing as Pawn;
                if (shooter == null || !shooter.RaceProps.Humanlike) return;
                if (shooter.Faction == null || !shooter.Faction.HostileTo(Faction.OfPlayer)) return;

                var t = target == null ? null : target.Thing as Pawn;
                if (t == null) return;

                // Defensive exemption: anything currently coming at the shooter is fair game.
                if (t.mindState != null && ReferenceEquals(t.mindState.enemyTarget, shooter)) return;

                float penalty = 0f;
                if (t.Downed) penalty += 45f;
                else if (t.RaceProps.Humanlike && t.DevelopmentalStage != DevelopmentalStage.Adult) penalty += 55f;
                else if (t.RaceProps.Animal && t.Faction == Faction.OfPlayer)
                {
                    if (AnimalThreatRelevance.IsActiveThreat(t)) return;
                    penalty += 70f;
                }
                if (penalty <= 0f) return;

                __result -= penalty * RestraintFactor(shooter);
            }
            catch { }
        }
    }
}
