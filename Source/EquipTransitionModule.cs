using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Module: weapon transitions. A rifleman who lets a raider walk into his muzzle dies
    // reloading - when an enemy closes inside the weapon's dead zone, Proactive+ fighters
    // switch to their sidearm or melee weapon on their own, and switch back to the primary
    // once the fight opens up. Rides Simple Sidearms' own swap machinery when that mod is
    // present (resolved by reflection); plain inventory swaps otherwise.
    [StaticConstructorOnStartup]
    public static class SidearmsBridge
    {
        public static bool Present;
        private static System.Reflection.MethodInfo cqcSwap;
        private static System.Reflection.MethodInfo equipSpecific;
        private static object dropCombat;

        static SidearmsBridge()
        {
            try
            {
                var wa = AccessTools.TypeByName("PeteTimesSix.SimpleSidearms.Utilities.WeaponAssingment");
                var en = AccessTools.TypeByName("PeteTimesSix.SimpleSidearms.Utilities.Enums+DroppingModeEnum");
                if (wa == null || en == null) return;
                cqcSwap = AccessTools.Method(wa, "tryCQCWeaponSwapToMelee");
                equipSpecific = AccessTools.Method(wa, "equipSpecificWeaponFromInventory");
                dropCombat = Enum.Parse(en, "Combat");
                Present = cqcSwap != null && equipSpecific != null;
                if (Present) Log.Message("[Colonist Awareness] Simple Sidearms bridge active");
            }
            catch (Exception e)
            {
                Log.Message("[Colonist Awareness] Simple Sidearms bridge unavailable: " + e.Message);
            }
        }

        public static bool SwapToMelee(Pawn p, Pawn enemy)
        {
            if (!Present) return false;
            try { return (bool)cqcSwap.Invoke(null, new object[] { p, enemy, dropCombat }); }
            catch { return false; }
        }

        public static bool Equip(Pawn p, ThingWithComps weapon)
        {
            if (!Present) return false;
            try { return (bool)equipSpecific.Invoke(null, new object[] { p, weapon, false, false }); }
            catch { return false; }
        }
    }

    public class EquipTransitionMapComponent : MapComponent
    {
        private readonly struct PerceivedThreat
        {
            public readonly bool Known;
            public readonly Pawn VisiblePawn;
            public readonly IntVec3 Cell;

            public PerceivedThreat(bool known, Pawn visiblePawn, IntVec3 cell)
            {
                Known = known;
                VisiblePawn = visiblePawn;
                Cell = cell;
            }
        }

        private int cooldown;
        // pawnId -> thingIDNumber of the stowed primary to return to
        private Dictionary<int, int> stowedPrimary = new Dictionary<int, int>();
        // pawnId -> thingIDNumber of the replacement CA actually selected. This
        // ownership token prevents a later auto-return from undoing a different
        // weapon the player equipped by hand.
        private Dictionary<int, int> transitionWeapon = new Dictionary<int, int>();
        // pawnId -> offhand thingIDNumber selected by this component. Manual
        // offhands are never auto-stowed or touched by kill-switch cleanup.
        private Dictionary<int, int> automaticOffhand = new Dictionary<int, int>();
        private Dictionary<int, int> calmScans = new Dictionary<int, int>();

        public EquipTransitionMapComponent(Map map) : base(map) { }

        // The stowed-primary linkage must survive a save or a pawn saved mid-fight
        // stays on the sidearm forever after load.
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref stowedPrimary, "CA_stowedPrimary", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref transitionWeapon, "CA_transitionWeapon", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref automaticOffhand, "CA_automaticOffhand", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref calmScans, "CA_calmScans", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref offhandCalm, "CA_offhandCalm", LookMode.Value, LookMode.Value);
            if (stowedPrimary == null) stowedPrimary = new Dictionary<int, int>();
            if (transitionWeapon == null) transitionWeapon = new Dictionary<int, int>();
            if (automaticOffhand == null) automaticOffhand = new Dictionary<int, int>();
            if (calmScans == null) calmScans = new Dictionary<int, int>();
            if (offhandCalm == null) offhandCalm = new Dictionary<int, int>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            var s = AwarenessMod.Settings;
            if (s == null || !s.weaponTransitions) DisableTransitions();
        }

        public override void MapComponentTick()
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.weaponTransitions)
            {
                DisableTransitions();
                return;
            }
            if (--cooldown > 0) return;
            cooldown = 30;
            try
            {
                ReconcileDepartedTransitions();
                Scan();
            }
            catch { }
        }

        private void Scan()
        {
            var actors = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < actors.Count; i++)
            {
                var p = actors[i];
                if (p.Downed || p.Dead || p.equipment == null || p.inventory == null) continue;
                if (p.IsColonistPlayerControlled && HasDirectPlayerControl(p)) continue;
                if (stowedPrimary.ContainsKey(p.thingIDNumber)
                    && !StillOwnsTransition(p))
                    ForgetTransition(p.thingIDNumber);
                if (p.WorkTagIsDisabled(WorkTags.Violent) || !MayTransition(p)
                    || HiddenRegistry.IsHidden(p))
                {
                    if (stowedPrimary.ContainsKey(p.thingIDNumber)) SwapBack(p);
                    ClearOwnedOffhand(p);
                    continue;
                }

                PerceivedThreat threat = NearestThreat(p, 12f);
                ManageOffhand(p, threat.Known);
                var primary = p.equipment.Primary;

                bool actionableThreat = threat.VisiblePawn != null;
                if (actionableThreat && primary != null
                    && primary.def.IsRangedWeapon)
                {
                    float dist = p.Position.DistanceTo(threat.Cell);
                    if (dist <= DeadZone(primary))
                    {
                        TryTransition(p, threat.VisiblePawn, primary, dist);
                        continue;
                    }
                }

                // Fight opened back up: a replacement that cannot answer the
                // presently visible range yields immediately; after total contact
                // loss, retain the prior six-scan hysteresis before restoring.
                if (stowedPrimary.ContainsKey(p.thingIDNumber))
                {
                    // A copied contact keeps the opponent in mind; it does not keep
                    // a close-combat weapon equipped after direct contact is lost.
                    // Without a visible pawn there is no target/path commitment that
                    // makes the transition actionable.
                    bool transitionStillActionable = actionableThreat
                        && TransitionRemainsActionable(p, primary,
                            threat.VisiblePawn);
                    if (!transitionStillActionable)
                    {
                        int t;
                        calmScans.TryGetValue(p.thingIDNumber, out t);
                        int requiredScans = actionableThreat ? 1 : 6;
                        if (t + 1 >= requiredScans)
                        {
                            SwapBack(p);
                            calmScans.Remove(p.thingIDNumber);
                        }
                        else calmScans[p.thingIDNumber] = t + 1;
                    }
                    else calmScans.Remove(p.thingIDNumber);
                }
            }
        }

        // How close an enemy gets before this ranged weapon is a liability: long, slow
        // pieces give up more ground than carbines; short quick sidearms barely any.
        private static float DeadZone(ThingWithComps weapon)
        {
            var verbs = weapon.def.Verbs;
            if (verbs == null || verbs.Count == 0) return 2.9f;
            float range = verbs[0].range;
            float warmup = verbs[0].warmupTime;
            if (range >= 30f || warmup >= 1.4f) return 5.9f;
            if (range < 20f && warmup < 0.9f) return 1.4f;
            return 3.4f;
        }

        private bool TransitionRemainsActionable(Pawn pawn,
            ThingWithComps replacement, Pawn visibleThreat)
        {
            if (pawn == null || replacement == null || visibleThreat == null)
                return false;
            float distance = pawn.Position.DistanceTo(visibleThreat.Position);
            if (!replacement.def.IsRangedWeapon) return distance <= 2.9f;
            int stowedId;
            if (!stowedPrimary.TryGetValue(pawn.thingIDNumber, out stowedId))
                return false;
            ThingOwner<Thing> inventory = pawn.inventory?.innerContainer;
            if (inventory == null) return false;
            for (int i = 0; i < inventory.Count; i++)
            {
                ThingWithComps original = inventory[i] as ThingWithComps;
                if (original != null && original.thingIDNumber == stowedId)
                    return distance <= DeadZone(original);
            }
            return false;
        }

        private void TryTransition(Pawn p, Pawn threat, ThingWithComps primary, float dist)
        {
            int primaryId = primary.thingIDNumber;
            bool swapped = false;

            // Skill ranks the available close-combat tools; it cannot make a melee
            // weapon useful outside immediate reach. A visible target inside the
            // muzzle envelope may justify a sidearm, while melee is reserved for an
            // actually close contact that the following combat lane can commit to.
            bool meleePreferred = threat != null && dist <= 2.9f;

            if (meleePreferred)
            {
                swapped = threat != null && SidearmsBridge.SwapToMelee(p, threat);
                if (!swapped)
                {
                    var blade = BestInventoryMelee(p);
                    if (blade != null) swapped = EquipFromInventory(p, blade);
                }
            }
            if (!swapped)
            {
                var sidearm = BestInventoryShortRanged(p, primary);
                if (sidearm != null) swapped = EquipFromInventory(p, sidearm);
                else if (!meleePreferred && dist <= 2.9f)
                {
                    swapped = threat != null && SidearmsBridge.SwapToMelee(p, threat);
                    if (!swapped)
                    {
                        var blade = BestInventoryMelee(p);
                        if (blade != null) swapped = EquipFromInventory(p, blade);
                    }
                }
            }

            if (swapped)
            {
                if (!stowedPrimary.ContainsKey(p.thingIDNumber))
                    stowedPrimary[p.thingIDNumber] = primaryId;
                ThingWithComps replacement = p.equipment.Primary;
                if (replacement != null)
                {
                    transitionWeapon[p.thingIDNumber] = replacement.thingIDNumber;
                    CATrace.Pawn(p, "weapon transition ADOPTED - "
                        + primary.LabelShort + " -> " + replacement.LabelShort
                        + " for visible " + threat.LabelShort + " at "
                        + dist.ToString("F1") + " cells",
                        target: threat, contact: threat.Position,
                        anchor: p.Position);
                }
            }
        }

        private bool SwapBack(Pawn p)
        {
            int id;
            if (!stowedPrimary.TryGetValue(p.thingIDNumber, out id)) return true;
            if (!StillOwnsTransition(p))
            {
                ForgetTransition(p.thingIDNumber);
                return true;
            }
            var inv = p.inventory.innerContainer;
            for (int i = 0; i < inv.Count; i++)
            {
                var w = inv[i] as ThingWithComps;
                if (w == null || w.thingIDNumber != id) continue;
                ThingWithComps replacement = p.equipment.Primary;
                if (!SidearmsBridge.Equip(p, w) && !EquipFromInventory(p, w)) return false;
                ForgetTransition(p.thingIDNumber);
                CATrace.Pawn(p, "weapon transition ENDED - "
                    + (replacement != null ? replacement.LabelShort : "replacement")
                    + " -> " + w.LabelShort
                    + " after direct close contact ended",
                    anchor: p.Position);
                return true;
            }
            // primary lost or dropped somewhere along the way - nothing to return to
            ForgetTransition(p.thingIDNumber);
            return true;
        }

        private bool StillOwnsTransition(Pawn pawn)
        {
            int replacementId;
            return pawn != null && pawn.equipment != null
                && pawn.equipment.Primary != null
                && transitionWeapon.TryGetValue(pawn.thingIDNumber, out replacementId)
                && pawn.equipment.Primary.thingIDNumber == replacementId;
        }

        private void ForgetTransition(int pawnId)
        {
            stowedPrimary.Remove(pawnId);
            transitionWeapon.Remove(pawnId);
            calmScans.Remove(pawnId);
        }

        public void NotifyPawnDespawning(Pawn pawn)
        {
            if (pawn == null) return;
            if (stowedPrimary.ContainsKey(pawn.thingIDNumber))
            {
                if (!StillOwnsTransition(pawn)) ForgetTransition(pawn.thingIDNumber);
                else if (!SwapBack(pawn)) ForgetTransition(pawn.thingIDNumber);
            }
            ClearOwnedOffhand(pawn);
        }

        private void ReconcileDepartedTransitions()
        {
            var pending = new List<int>(stowedPrimary.Keys);
            for (int i = 0; i < pending.Count; i++)
            {
                Pawn pawn = FindTrackedPawn(pending[i]);
                if (pawn == null || pawn.Map == map && pawn.Spawned) continue;
                if (pawn.Dead || pawn.equipment == null || pawn.inventory == null
                    || !StillOwnsTransition(pawn))
                {
                    ForgetTransition(pending[i]);
                    continue;
                }
                if (!SwapBack(pawn)) ForgetTransition(pending[i]);
            }

            var offhandPawns = new List<int>(automaticOffhand.Keys);
            for (int i = 0; i < offhandPawns.Count; i++)
            {
                Pawn pawn = FindTrackedPawn(offhandPawns[i]);
                if (pawn == null || pawn.Map == map && pawn.Spawned) continue;
                ClearOwnedOffhand(pawn);
            }
        }

        // Turning the feature off ends its claim immediately. A recorded primary is
        // restored as soon as doing so will not override a draft or forced player job;
        // until then the one pending linkage is retained solely for safe cleanup.
        private void DisableTransitions()
        {
            calmScans.Clear();
            offhandCalm.Clear();
            if (stowedPrimary.Count == 0)
            {
                transitionWeapon.Clear();
            }
            else
            {
                var pending = new List<int>(stowedPrimary.Keys);
                for (int i = 0; i < pending.Count; i++)
                {
                    int pawnId = pending[i];
                    Pawn pawn = FindTrackedPawn(pawnId);

                    if (pawn == null) continue;
                    if (pawn.Dead || pawn.equipment == null || pawn.inventory == null)
                    {
                        ForgetTransition(pawnId);
                        continue;
                    }
                    if (!StillOwnsTransition(pawn))
                    {
                        // Missing tokens from an older save and later hand-equips both
                        // surrender ownership rather than guessing against the player.
                        ForgetTransition(pawnId);
                        continue;
                    }
                    if (pawn.IsColonistPlayerControlled && HasDirectPlayerControl(pawn))
                        continue;
                    SwapBack(pawn);
                }
            }

            var offhands = new List<int>(automaticOffhand.Keys);
            for (int i = 0; i < offhands.Count; i++)
            {
                Pawn pawn = FindTrackedPawn(offhands[i]);
                if (pawn == null) continue;
                if (pawn.IsColonistPlayerControlled && HasDirectPlayerControl(pawn))
                    continue;
                ClearOwnedOffhand(pawn);
            }
        }

        private Pawn FindTrackedPawn(int pawnId)
        {
            var mapPawns = map.mapPawns.AllPawns;
            for (int i = 0; i < mapPawns.Count; i++)
                if (mapPawns[i].thingIDNumber == pawnId) return mapPawns[i];

            if (Find.WorldPawns == null) return null;
            var worldPawns = Find.WorldPawns.AllPawnsAliveOrDead;
            for (int i = 0; i < worldPawns.Count; i++)
                if (worldPawns[i].thingIDNumber == pawnId) return worldPawns[i];
            return null;
        }

        // Autonomous pawns run their own hands: a threat appears and the main hand is
        // one-handed - draw a second weapon from inventory; the fight ends - stow it.
        private Dictionary<int, int> offhandCalm = new Dictionary<int, int>();

        private void ManageOffhand(Pawn p, bool threatKnown)
        {
            var s = AwarenessMod.Settings;
            if (automaticOffhand.ContainsKey(p.thingIDNumber)
                && !OwnsCurrentOffhand(p))
                ForgetAutomaticOffhand(p.thingIDNumber);
            if (s == null || !s.dualWield
                || p.IsColonistPlayerControlled && AutonomyComponent.LevelOf(p) < 3)
            {
                ClearOwnedOffhand(p);
                return;
            }

            ThingWithComps currentOffhand = OffhandComponent.GetOffhand(p);
            bool hasOff = currentOffhand != null;
            if (threatKnown)
            {
                offhandCalm.Remove(p.thingIDNumber);
                if (hasOff) return;
                var primary = p.equipment.Primary;
                if (primary != null && !OffhandComponent.CanBeOffhand(primary.def)) return; // two-handed main
                var cand = BestOffhandCandidate(p);
                if (cand != null && OffhandComponent.SetOffhand(p, cand))
                    automaticOffhand[p.thingIDNumber] = cand.thingIDNumber;
            }
            else if (hasOff && OwnsCurrentOffhand(p))
            {
                int t;
                offhandCalm.TryGetValue(p.thingIDNumber, out t);
                // Preserve the prior 300-tick calm horizon on the new 30-tick scan.
                if (t + 1 >= 10) ClearOwnedOffhand(p);
                else offhandCalm[p.thingIDNumber] = t + 1;
            }
            else offhandCalm.Remove(p.thingIDNumber);
        }

        private bool OwnsCurrentOffhand(Pawn pawn)
        {
            int weaponId;
            ThingWithComps offhand = OffhandComponent.GetOffhand(pawn);
            return offhand != null
                && automaticOffhand.TryGetValue(pawn.thingIDNumber, out weaponId)
                && offhand.thingIDNumber == weaponId;
        }

        private void ClearOwnedOffhand(Pawn pawn)
        {
            if (pawn == null) return;
            int pawnId = pawn.thingIDNumber;
            if (!automaticOffhand.ContainsKey(pawnId)) return;
            if (OwnsCurrentOffhand(pawn)) OffhandComponent.ClearOffhand(pawn, false);
            ForgetAutomaticOffhand(pawnId);
        }

        private void ForgetAutomaticOffhand(int pawnId)
        {
            automaticOffhand.Remove(pawnId);
            offhandCalm.Remove(pawnId);
        }

        private static ThingWithComps BestOffhandCandidate(Pawn p)
        {
            var inv = p.inventory.innerContainer;
            ThingWithComps bestRanged = null, bestMelee = null;
            float bestWarmup = float.MaxValue, bestDps = 0f;
            for (int i = 0; i < inv.Count; i++)
            {
                var w = inv[i] as ThingWithComps;
                if (w == null || !OffhandComponent.CanBeOffhand(w.def)) continue;
                if (w.def.IsRangedWeapon)
                {
                    var verbs = w.def.Verbs;
                    float wu = verbs != null && verbs.Count > 0 ? verbs[0].warmupTime : 99f;
                    if (wu < bestWarmup) { bestWarmup = wu; bestRanged = w; }
                }
                else
                {
                    float dps = w.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                    if (dps > bestDps) { bestDps = dps; bestMelee = w; }
                }
            }
            if (bestRanged != null) return bestRanged;
            return bestMelee;
        }

        private static bool EquipFromInventory(Pawn p, ThingWithComps w)
        {
            // With an offhand equipped, the equipment container holds TWO Primary-type
            // items; once the true primary transfers out, the offhand becomes .Primary
            // and vanilla AddEquipment refuses the new weapon - which would void it.
            // Lift the offhand out for the duration of the swap.
            ThingWithComps offhand = OffhandComponent.GetOffhand(p);
            if (offhand != null) p.equipment.GetDirectlyHeldThings().Remove(offhand);
            try
            {
                var primary = p.equipment.Primary;
                if (primary != null
                    && !p.equipment.TryTransferEquipmentToContainer(primary, p.inventory.innerContainer))
                    return false;
                if (!p.inventory.innerContainer.Remove(w)) return false;
                p.equipment.AddEquipment(w);
                if (p.equipment.Primary != w)
                {
                    // The engine refused the equip - never strand the weapon in limbo.
                    p.inventory.innerContainer.TryAdd(w);
                    return false;
                }
                return true;
            }
            finally
            {
                if (offhand != null && !p.equipment.GetDirectlyHeldThings().Contains(offhand))
                    p.equipment.GetDirectlyHeldThings().TryAdd(offhand);
            }
        }

        private static ThingWithComps BestInventoryMelee(Pawn p)
        {
            var inv = p.inventory.innerContainer;
            ThingWithComps best = null;
            float bestDps = 0f;
            for (int i = 0; i < inv.Count; i++)
            {
                var w = inv[i] as ThingWithComps;
                if (w == null || !w.def.IsMeleeWeapon || !w.def.IsWeapon) continue;
                float dps = w.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                if (dps > bestDps) { bestDps = dps; best = w; }
            }
            return best;
        }

        private static ThingWithComps BestInventoryShortRanged(Pawn p, ThingWithComps primary)
        {
            float pRange = primary.def.Verbs.Count > 0 ? primary.def.Verbs[0].range : 999f;
            float pWarmup = primary.def.Verbs.Count > 0 ? primary.def.Verbs[0].warmupTime : 99f;
            var inv = p.inventory.innerContainer;
            ThingWithComps best = null;
            float bestWarmup = float.MaxValue;
            for (int i = 0; i < inv.Count; i++)
            {
                var w = inv[i] as ThingWithComps;
                if (w == null || !w.def.IsRangedWeapon || !w.def.IsWeapon) continue;
                var verbs = w.def.Verbs;
                if (verbs == null || verbs.Count == 0) continue;
                if (verbs[0].range >= pRange || verbs[0].warmupTime >= pWarmup) continue;
                if (verbs[0].warmupTime < bestWarmup) { bestWarmup = verbs[0].warmupTime; best = w; }
            }
            return best;
        }

        private PerceivedThreat NearestThreat(Pawn p, float radius)
        {
            var settings = AwarenessMod.Settings;
            var know = KnowledgeMapComponent.For(map);
            float bestDist = radius;
            if (settings != null && settings.knowledgeContacts)
            {
                if (know == null) return default;
                ThreatContactSnapshot best = default;
                bool found = false;
                var contacts = know.FreshContacts(p);
                for (int i = 0; i < contacts.Count; i++)
                {
                    float distance = p.Position.DistanceTo(contacts[i].Cell);
                    if (distance >= bestDist) continue;
                    bestDist = distance;
                    best = contacts[i];
                    found = true;
                }
                if (!found) return default;

                Pawn visible = VisiblePawnById(p, best.HostileId, radius);
                return new PerceivedThreat(true, visible,
                    visible != null ? visible.Position : best.Cell);
            }

            Pawn nearest = null;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn hostile = pawns[i];
                if (!KnowledgeMapComponent.CanCurrentlySeeHostile(
                    p, hostile, radius)) continue;
                float distance = p.Position.DistanceTo(hostile.Position);
                if (distance >= bestDist) continue;
                bestDist = distance;
                nearest = hostile;
            }
            return nearest != null
                ? new PerceivedThreat(true, nearest, nearest.Position) : default;
        }

        private Pawn VisiblePawnById(Pawn observer, int id, float radius)
        {
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate.thingIDNumber != id
                    || !KnowledgeMapComponent.CanCurrentlySeeHostile(
                        observer, candidate, radius)) continue;
                return candidate;
            }
            return null;
        }

        private static bool MayTransition(Pawn p)
        {
            if (p.IsColonistPlayerControlled) return AutonomyComponent.LevelOf(p) >= 2;
            var lord = p.GetLord();
            return lord != null && lord.LordJob != null
                && lord.LordJob.GetType() == typeof(LordJob_AssaultColony)
                && lord.CurLordToil != null
                && lord.CurLordToil.GetType() == typeof(LordToil_AssaultColony);
        }

        private static bool HasDirectPlayerControl(Pawn p)
        {
            if (p == null || p.Drafted) return true;
            if (p.CurJob != null && p.CurJob.playerForced) return true;
            return p.jobs != null && p.jobs.jobQueue != null
                && p.jobs.jobQueue.AnyPlayerForced;
        }
    }

    // A transition is map-owned, so restore its owned primary before the engine
    // deregisters the pawn and hands it to a caravan, shuttle, corpse, or world pawn.
    public static class EquipTransitionDespawnPatch
    {
        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var target = AccessTools.Method(typeof(Pawn), nameof(Pawn.DeSpawn),
                    new[] { typeof(DestroyMode) });
                if (target == null) throw new MissingMethodException(
                    typeof(Pawn).FullName, nameof(Pawn.DeSpawn));
                harmony.Patch(target, prefix: new HarmonyMethod(
                    typeof(EquipTransitionDespawnPatch), nameof(Prefix)));
            }
            catch (Exception e)
            {
                Log.Warning("[Colonist Awareness] transition exit cleanup stood down: "
                    + e.Message);
            }
        }

        public static void Prefix(Pawn __instance)
        {
            Map map = __instance != null ? __instance.Map : null;
            map?.GetComponent<EquipTransitionMapComponent>()
                ?.NotifyPawnDespawning(__instance);
        }
    }
}
