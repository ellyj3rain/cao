using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module: dual wielding, bespoke. Mechanics studied from the unmaintained Tacticowl /
    // Dual Wield lineage (their source read as reference; all code and assets here are
    // original, no transpilers - every seam is a plain prefix/postfix verified against
    // the 1.6 assembly).
    //
    // Architecture: the offhand weapon rides as a SECOND item in the vanilla equipment
    // container (order-kept so Primary stays the main hand). A second Pawn_StanceTracker
    // per pawn lets the offhand aim and cool independently; a SetStance reroute sends any
    // stance whose verb belongs to a registered offhand into that tracker. Starting an
    // attack with the main hand also starts the offhand's verb. Both hands pay a
    // skill-scaled cooldown penalty; offhand shots originate offset from the body.
    public class OffhandComponent : GameComponent
    {
        private Dictionary<int, int> offhandOf = new Dictionary<int, int>();
        private readonly HashSet<int> offhandThings = new HashSet<int>();
        private readonly Dictionary<int, Pawn_StanceTracker> trackers = new Dictionary<int, Pawn_StanceTracker>();
        public static OffhandComponent Instance;
        public static int ActiveCount;

        public OffhandComponent(Game game)
        {
            Instance = this;
            ActiveCount = 0;
            OffhandPatches.ClearTransientFollowups();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref offhandOf, "CA_offhandOf", LookMode.Value, LookMode.Value);
            if (offhandOf == null) offhandOf = new Dictionary<int, int>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                offhandThings.Clear();
                foreach (var kv in offhandOf) offhandThings.Add(kv.Value);
                ActiveCount = offhandOf.Count;
            }
            Instance = this;
        }

        public static bool IsOffhandThing(Thing t)
        {
            var inst = Instance;
            return t != null && inst != null && ActiveCount > 0 && inst.offhandThings.Contains(t.thingIDNumber);
        }

        public static bool HasOffhand(Pawn p)
        {
            var inst = Instance;
            return p != null && inst != null && ActiveCount > 0 && inst.offhandOf.ContainsKey(p.thingIDNumber);
        }

        // Resolves the registered offhand; silently unregisters if it left the pawn's hands.
        public static ThingWithComps GetOffhand(Pawn p)
        {
            var inst = Instance;
            if (p == null || inst == null || ActiveCount == 0) return null;
            int id;
            if (!inst.offhandOf.TryGetValue(p.thingIDNumber, out id)) return null;
            if (p.equipment != null)
            {
                var list = p.equipment.AllEquipmentListForReading;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].thingIDNumber == id) return list[i];
            }
            Unregister(p, id);
            return null;
        }

        public static Pawn_StanceTracker TrackerOf(Pawn p)
        {
            var inst = Instance;
            Pawn_StanceTracker t;
            if (!inst.trackers.TryGetValue(p.thingIDNumber, out t))
            {
                t = new Pawn_StanceTracker(p);
                inst.trackers[p.thingIDNumber] = t;
            }
            return t;
        }

        // Passive diagnostics and physical-sector readers must not create the
        // second tracker merely by observing a pawn. Runtime combat code continues
        // to use TrackerOf when it intentionally owns offhand state.
        internal static bool TryGetExistingTracker(Pawn p,
            out Pawn_StanceTracker tracker)
        {
            tracker = null;
            OffhandComponent inst = Instance;
            return p != null && inst != null && ActiveCount > 0
                && inst.trackers.TryGetValue(p.thingIDNumber, out tracker)
                && tracker != null;
        }

        public static bool SetOffhand(Pawn p, ThingWithComps w)
        {
            var inst = Instance;
            if (inst == null || p == null || w == null || p.equipment == null) return false;
            ClearOffhand(p, false);
            if (w.Spawned) w.DeSpawn();
            else if (p.inventory != null && p.inventory.innerContainer.Contains(w))
                p.inventory.innerContainer.Remove(w);
            if (!p.equipment.GetDirectlyHeldThings().TryAdd(w, false))
            {
                // could not take it - put it back on the ground rather than voiding it
                GenPlace.TryPlaceThing(w, p.Position, p.Map, ThingPlaceMode.Near);
                return false;
            }
            inst.offhandOf[p.thingIDNumber] = w.thingIDNumber;
            inst.offhandThings.Add(w.thingIDNumber);
            ActiveCount = inst.offhandOf.Count;
            return true;
        }

        // Stow to inventory (or drop if there is no room).
        public static void ClearOffhand(Pawn p, bool drop)
        {
            var inst = Instance;
            if (inst == null || p == null) return;
            var w = GetOffhand(p);
            if (w != null)
            {
                p.equipment.GetDirectlyHeldThings().Remove(w);
                bool stowed = false;
                if (!drop && p.inventory != null) stowed = p.inventory.innerContainer.TryAdd(w, true);
                if (!stowed && p.Map != null) GenPlace.TryPlaceThing(w, p.Position, p.Map, ThingPlaceMode.Near);
            }
            int id;
            if (inst.offhandOf.TryGetValue(p.thingIDNumber, out id)) Unregister(p, id);
        }

        private static void Unregister(Pawn p, int thingId)
        {
            var inst = Instance;
            inst.offhandOf.Remove(p.thingIDNumber);
            inst.offhandThings.Remove(thingId);
            inst.trackers.Remove(p.thingIDNumber);
            ActiveCount = inst.offhandOf.Count;
        }

        // One-handed-able: pistols, SMGs, and light melee. Long guns and heavy pieces never.
        public static bool CanBeOffhand(ThingDef d)
        {
            if (d == null || !d.IsWeapon || d.destroyOnDrop) return false;
            float mass = d.GetStatValueAbstract(StatDefOf.Mass);
            if (d.IsMeleeWeapon) return mass <= 3f;
            if (!d.IsRangedWeapon || d.Verbs == null || d.Verbs.Count == 0) return false;
            return d.Verbs[0].range < 26f && mass <= 3.6f;
        }
    }

    public static class OffhandPatches
    {
        // MakeRoomFor sees only the main hand; the offhand is lifted out and re-added
        // after AddEquipment so container order (main first) is preserved.
        private static readonly Dictionary<int, ThingWithComps> pendingReAdd = new Dictionary<int, ThingWithComps>();
        private sealed class AutonomousFollowupState
        {
            public Verb Verb;
            public Thing Target;
            public Stance Stance;
        }
        private static readonly Dictionary<int, AutonomousFollowupState>
            activeFollowups = new Dictionary<int, AutonomousFollowupState>();
        [System.ThreadStatic] private static Pawn followupPawn;
        [System.ThreadStatic] private static Verb followupVerb;
        [System.ThreadStatic] private static Thing followupTarget;

        internal static bool OwnsAutonomousFollowup(Pawn pawn, Thing target,
            Verb verb)
        {
            if (pawn == null || target == null || verb == null) return false;
            if (followupPawn == pawn && followupTarget == target
                && followupVerb == verb) return true;
            AutonomousFollowupState state;
            return activeFollowups.TryGetValue(pawn.thingIDNumber, out state)
                && state != null && state.Verb == verb
                && state.Target == target && state.Stance != null
                && state.Stance == state.Stance.stanceTracker?.curStance;
        }

        internal static void ClearTransientFollowups()
        {
            activeFollowups.Clear();
            followupPawn = null;
            followupVerb = null;
            followupTarget = null;
        }

        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var self = typeof(OffhandPatches);
                harmony.Patch(AccessTools.Method(typeof(Pawn), "Tick"),
                    postfix: new HarmonyMethod(self, "PawnTickPostfix"));
                harmony.Patch(AccessTools.Method(typeof(Pawn_StanceTracker), "SetStance"),
                    prefix: new HarmonyMethod(self, "SetStancePrefix"));
                harmony.Patch(AccessTools.Method(typeof(Verb), "TryStartCastOn", new[] {
                        typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }),
                    postfix: new HarmonyMethod(self, "TryStartCastOnPostfix"));
                harmony.Patch(AccessTools.PropertyGetter(typeof(Pawn_StanceTracker), "FullBodyBusy"),
                    postfix: new HarmonyMethod(self, "FullBodyBusyPostfix"));
                harmony.Patch(AccessTools.Method(typeof(VerbProperties), "AdjustedCooldown", new[] {
                        typeof(Tool), typeof(Pawn), typeof(Thing) }),
                    postfix: new HarmonyMethod(self, "AdjustedCooldownPostfix"));
                harmony.Patch(AccessTools.Method(typeof(Projectile), "Launch", new[] {
                        typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo),
                        typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef) }),
                    prefix: new HarmonyMethod(self, "ProjectileLaunchPrefix"));
                harmony.Patch(AccessTools.Method(typeof(Pawn_EquipmentTracker), "MakeRoomFor", new[] {
                        typeof(ThingWithComps), typeof(ThingWithComps).MakeByRefType() }),
                    prefix: new HarmonyMethod(self, "MakeRoomForPrefix"));
                harmony.Patch(AccessTools.Method(typeof(Pawn_EquipmentTracker), "AddEquipment"),
                    postfix: new HarmonyMethod(self, "AddEquipmentPostfix"));
                harmony.Patch(AccessTools.PropertyGetter(typeof(Pawn), "CurrentEffectiveVerb"),
                    postfix: new HarmonyMethod(self, "CurrentEffectiveVerbPostfix"));
                harmony.Patch(AccessTools.Method(typeof(Pawn), "TryGetAttackVerb", new[] {
                        typeof(Thing), typeof(bool), typeof(bool) }),
                    postfix: new HarmonyMethod(self, "TryGetAttackVerbPostfix"));
                Log.Message("[Colonist Awareness] dual wield seams installed");
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] dual wield install failed: " + e.Message);
            }
        }

        public static void PawnTickPostfix(Pawn __instance)
        {
            if (OffhandComponent.ActiveCount == 0) return;
            try
            {
                // A vanilla stun lives on the MAIN tracker's stunner; the side tracker
                // has its own that nothing ever stuns - pause it manually or a stunned
                // pawn's offhand finishes its aim and fires.
                if (OffhandComponent.HasOffhand(__instance) && __instance.Spawned && !__instance.Dead
                    && !__instance.stances.stunner.Stunned)
                    OffhandComponent.TrackerOf(__instance).StanceTrackerTick();
                AutonomousFollowupState state;
                if (activeFollowups.TryGetValue(__instance.thingIDNumber,
                        out state)
                    && (state == null || state.Stance == null
                        || state.Stance != state.Stance.stanceTracker?.curStance))
                    activeFollowups.Remove(__instance.thingIDNumber);
            }
            catch { }
        }

        // Stances raised by an offhand verb land in the offhand tracker, leaving the
        // main hand's stance untouched - the two hands cycle independently.
        public static bool SetStancePrefix(Pawn_StanceTracker __instance, Stance newStance)
        {
            if (OffhandComponent.ActiveCount == 0) return true;
            try
            {
                var busy = newStance as Stance_Busy;
                if (busy == null || busy.verb == null) return true;
                var src = busy.verb.EquipmentSource;
                if (src == null || !OffhandComponent.IsOffhandThing(src)) return true;
                var pawn = __instance.pawn;
                if (pawn == null || __instance != pawn.stances) return true; // already rerouted
                // Possession check, not just weapon identity: if this pawn is not the
                // registered dual-wielder (the weapon was dropped and picked up by
                // someone else as a primary), the stance belongs in the MAIN tracker.
                if (OffhandComponent.GetOffhand(pawn) != src) return true;
                OffhandComponent.TrackerOf(pawn).SetStance(newStance);
                return false;
            }
            catch { return true; }
        }

        // Main hand opens fire - the offhand joins in on the same target.
        public static void TryStartCastOnPostfix(Verb __instance, ref bool __result, LocalTargetInfo castTarg)
        {
            var s = AwarenessMod.Settings;
            if (!__result || s == null || !s.dualWield
                || OffhandComponent.ActiveCount == 0) return;
            try
            {
                var pawn = __instance.caster as Pawn;
                if (pawn == null || pawn.InMentalState || pawn.WorkTagIsDisabled(WorkTags.Violent)) return;
                // Only a MAIN-HAND WEAPON attack brings the offhand in. TryStartCastOn
                // is the generic cast entry for every verb - abilities, psycasts,
                // beat-fire - and none of those should make the off hand open fire.
                if (__instance.EquipmentSource == null
                    || __instance.EquipmentSource != (pawn.equipment != null ? pawn.equipment.Primary : null)) return;
                var off = OffhandComponent.GetOffhand(pawn);
                if (off == null) return;
                var st = OffhandComponent.TrackerOf(pawn).curStance;
                if (st is Stance_Warmup || st is Stance_Cooldown) return;
                var comp = off.GetComp<CompEquippable>();
                if (comp == null || comp.PrimaryVerb == null || !comp.PrimaryVerb.Available()) return;
                Pawn previousPawn = followupPawn;
                Verb previousVerb = followupVerb;
                Thing previousTarget = followupTarget;
                bool started;
                followupPawn = pawn;
                followupVerb = comp.PrimaryVerb;
                followupTarget = castTarg.Thing;
                try
                {
                    started = comp.PrimaryVerb.TryStartCastOn(castTarg);
                    if (started)
                    {
                        Pawn_StanceTracker tracker;
                        Stance stance = OffhandComponent.TryGetExistingTracker(
                                pawn, out tracker)
                            ? tracker.curStance : null;
                        activeFollowups[pawn.thingIDNumber] =
                            new AutonomousFollowupState
                            {
                                Verb = comp.PrimaryVerb,
                                Target = castTarg.Thing,
                                Stance = stance
                            };
                    }
                }
                finally
                {
                    followupPawn = previousPawn;
                    followupVerb = previousVerb;
                    followupTarget = previousTarget;
                }
                __result = __result || started;
            }
            catch { }
        }

        // An offhand mid-recoil holds the body like the main hand would - unless the
        // pawn is running moving fire, which frees the legs by design.
        public static void FullBodyBusyPostfix(Pawn_StanceTracker __instance, ref bool __result)
        {
            if (__result || OffhandComponent.ActiveCount == 0) return;
            try
            {
                var pawn = __instance.pawn;
                if (pawn == null || __instance != pawn.stances) return;
                if (!OffhandComponent.HasOffhand(pawn) || MovingFire.IsOn(pawn)) return;
                if (OffhandComponent.TrackerOf(pawn).curStance is Stance_Cooldown) __result = true;
            }
            catch { }
        }

        // Two guns at once means slower cycles on both, less so for the skilled.
        public static float AdjustedCooldownPostfix(float __result, VerbProperties __instance, Pawn attacker, Thing equipment)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.dualWield || OffhandComponent.ActiveCount == 0) return __result;
            try
            {
                if (attacker == null || !OffhandComponent.HasOffhand(attacker)) return __result;
                if (__instance.category == VerbCategory.BeatFire) return __result;
                int skill = 8;
                if (attacker.skills != null)
                    skill = __instance.IsMeleeAttack
                        ? attacker.skills.GetSkill(SkillDefOf.Melee).Level
                        : attacker.skills.GetSkill(SkillDefOf.Shooting).Level;
                bool offhand = equipment != null && OffhandComponent.IsOffhandThing(equipment);
                float penalty = (offhand ? 0.2f : 0.1f) + 0.011f * (20 - skill);
                return __result * (1f + penalty);
            }
            catch { return __result; }
        }

        // Offhand shots leave from the offhand side, not the center of the chest.
        public static void ProjectileLaunchPrefix(Thing launcher, ref Vector3 origin, Thing equipment)
        {
            if (OffhandComponent.ActiveCount == 0) return;
            try
            {
                var pawn = launcher as Pawn;
                if (pawn == null || equipment == null || !OffhandComponent.IsOffhandThing(equipment)) return;
                float x = 0f, z = 0f;
                if (pawn.Rotation == Rot4.East) z = 0.1f;
                else if (pawn.Rotation == Rot4.West) z = -0.1f;
                else if (pawn.Rotation == Rot4.South) x = 0.1f;
                else x = -0.1f;
                origin += new Vector3(x, 0f, z);
            }
            catch { }
        }

        public static void MakeRoomForPrefix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
        {
            if (OffhandComponent.ActiveCount == 0) return;
            try
            {
                var pawn = __instance.pawn;
                if (pawn == null || eq == null || OffhandComponent.IsOffhandThing(eq)) return;
                var off = OffhandComponent.GetOffhand(pawn);
                if (off == null) return;
                __instance.GetDirectlyHeldThings().Remove(off);
                pendingReAdd[pawn.thingIDNumber] = off;
            }
            catch { }
        }

        public static void AddEquipmentPostfix(Pawn_EquipmentTracker __instance)
        {
            if (pendingReAdd.Count == 0) return;
            try
            {
                var pawn = __instance.pawn;
                ThingWithComps off;
                if (pawn == null || !pendingReAdd.TryGetValue(pawn.thingIDNumber, out off)) return;
                pendingReAdd.Remove(pawn.thingIDNumber);
                __instance.GetDirectlyHeldThings().TryAdd(off, false);
            }
            catch { }
        }

        // If the main hand is melee or outranged by the offhand, the offhand's verb is
        // the pawn's effective reach.
        public static void CurrentEffectiveVerbPostfix(Pawn __instance, ref Verb __result)
        {
            if (OffhandComponent.ActiveCount == 0 || __result == null) return;
            try
            {
                if (__instance.MannedThing() != null) return;
                var off = OffhandComponent.GetOffhand(__instance);
                if (off == null || off.def.IsMeleeWeapon) return;
                var comp = off.GetComp<CompEquippable>();
                if (comp == null || comp.PrimaryVerb == null) return;
                if (__result.IsMeleeAttack || __result.verbProps.range < comp.PrimaryVerb.verbProps.range)
                    __result = comp.PrimaryVerb;
            }
            catch { }
        }

        // The execution twin of CurrentEffectiveVerbPostfix: TryStartAttack resolves its
        // verb through TryGetAttackVerb, not the property - without this, a melee-main
        // pawn's ranged offhand is advertised but can never actually fire at range.
        public static void TryGetAttackVerbPostfix(Pawn __instance, ref Verb __result, Thing target)
        {
            if (OffhandComponent.ActiveCount == 0 || __result == null) return;
            try
            {
                if (!__result.IsMeleeAttack) return;
                var off = OffhandComponent.GetOffhand(__instance);
                if (off == null || off.def.IsMeleeWeapon) return;
                var comp = off.GetComp<CompEquippable>();
                if (comp == null || comp.PrimaryVerb == null || !comp.PrimaryVerb.Available()) return;
                // Out of melee reach but inside the offhand's range: shoot instead.
                if (target != null && __instance.Spawned
                    && !__instance.Position.AdjacentTo8WayOrInside(target)
                    && __instance.Position.InHorDistOf(target.Position, comp.PrimaryVerb.verbProps.range))
                    __result = comp.PrimaryVerb;
            }
            catch { }
        }
    }

    // Right-click a weapon row in the pawn's Gear tab: equip it as offhand from where it
    // sits, or stow the current one. The ground-item path lives in the order provider.
    public static class GearTabOffhandPatch
    {
        private static System.Reflection.MethodInfo selPawnGetter;

        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(ITab_Pawn_Gear), "DrawThingRow");
                selPawnGetter = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");
                if (m == null || selPawnGetter == null)
                { Log.Warning("[Colonist Awareness] gear tab seam not found; inventory offhand off"); return; }
                harmony.Patch(m,
                    prefix: new HarmonyMethod(typeof(GearTabOffhandPatch), "Prefix"),
                    postfix: new HarmonyMethod(typeof(GearTabOffhandPatch), "Postfix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] gear tab offhand install failed: " + e.Message);
            }
        }

        public static void Prefix(ref float y, out float __state)
        {
            __state = y;
        }

        public static void Postfix(ref float y, float width, Thing thing, ITab_Pawn_Gear __instance, float __state)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.dualWield || thing == null) return;
                var ev = Event.current;
                if (ev == null || ev.type != EventType.MouseDown || ev.button != 1) return;
                var rect = new Rect(0f, __state, width, y - __state);
                if (!rect.Contains(ev.mousePosition)) return;
                var pawn = selPawnGetter.Invoke(__instance, null) as Pawn;
                if (pawn == null || !pawn.IsColonistPlayerControlled || pawn.Downed) return;
                if (pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.equipment == null) return;

                var w = thing as ThingWithComps;
                if (w == null) return;
                var opts = new List<FloatMenuOption>();
                if (OffhandComponent.IsOffhandThing(w))
                {
                    var p2 = pawn;
                    opts.Add(new FloatMenuOption("Stow offhand", delegate { OffhandComponent.ClearOffhand(p2, false); }));
                }
                else if (OffhandComponent.CanBeOffhand(w.def) && w != pawn.equipment.Primary)
                {
                    var p2 = pawn;
                    var w2 = w;
                    opts.Add(new FloatMenuOption("Equip " + w.LabelShort + " as offhand",
                        delegate { OffhandComponent.SetOffhand(p2, w2); }));
                }
                if (opts.Count == 0) return;
                Find.WindowStack.Add(new FloatMenu(opts));
                ev.Use();
            }
            catch { }
        }
    }

    public class JobDriver_EquipOffhand : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            var take = new Toil();
            take.initAction = delegate
            {
                var w = job.targetA.Thing as ThingWithComps;
                if (w == null || !w.Spawned) return;
                OffhandComponent.SetOffhand(pawn, w);
            };
            take.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return take;
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_OffhandGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (var g in gizmos) yield return g;
            var p = __instance;
            // NOT gated on the dualWield setting: with an offhand already equipped, the
            // stow control must stay reachable or toggling the setting off strands the
            // weapon in the off hand forever.
            if (p == null || !p.IsColonistPlayerControlled) yield break;
            var off = OffhandComponent.GetOffhand(p);
            if (off == null) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Stow offhand",
                defaultDesc = "Put " + off.LabelShort + " away into inventory.",
                icon = off.def.uiIcon,
                action = delegate { OffhandComponent.ClearOffhand(p, false); }
            };
        }
    }
}
