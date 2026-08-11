using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Scenario starts put each player pawn and a share of the starting cargo in one
    // group, then pass every group through this one drop-pod call with forbid=true.
    // Narrow the exception to that exact map-generation shape: a live GameInitData,
    // the map currently being generated, and a pawn from its starting-pawn list.
    [HarmonyPatch(typeof(DropPodUtility), nameof(DropPodUtility.DropThingGroupsNear))]
    public static class Patch_CAStartingCargoOperationalAccess
    {
        public static void Prefix(Map map, List<List<Thing>> thingsGroups,
            ref bool forbid, out System.Diagnostics.Stopwatch __state)
        {
            __state = null;
            if (!forbid) return;
            AwarenessSettings settings = AwarenessMod.Settings;
            GameInitData init = Find.GameInitData;
            if (!CABehaviorSettings.IsEnabled(
                    CASettingKey.OperationalAccess, settings)
                || init == null || map == null
                || Current.ProgramState != ProgramState.MapInitializing
                || MapGenerator.mapBeingGenerated != map
                || thingsGroups == null || init.startingAndOptionalPawns == null)
                return;

            for (int i = 0; i < thingsGroups.Count; i++)
            {
                List<Thing> group = thingsGroups[i];
                if (group == null) continue;
                for (int j = 0; j < group.Count; j++)
                {
                    Pawn pawn = group[j] as Pawn;
                    if (pawn == null || pawn.Faction != Faction.OfPlayer
                        || !init.startingAndOptionalPawns.Contains(pawn))
                        continue;
                    forbid = false;
                    __state = System.Diagnostics.Stopwatch.StartNew();
                    CATrace.Log("operational access: starting cargo arrives allowed");
                    return;
                }
            }
        }

        public static void Postfix(Map map,
            System.Diagnostics.Stopwatch __state)
        {
            if (__state == null) return;
            Log.Message("[CA][Regional][Timing] starting cargo drop placement for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID
                + " took " + __state.ElapsedMilliseconds + " ms");
        }
    }

    // Vanilla's starting-pod search reads center.GetRoom before it examines the
    // reachability flag. On a regional map that unconditional read can rebuild
    // the whole dirty region graph even when reachability was disabled. Starting
    // cargo needs only the native 5-16-cell bounds, terrain, roof, occupancy, and
    // local-radius checks, so run that exact bounded search without touching the
    // global region graph before play begins.
    [HarmonyPatch]
    internal static class CARegionalStartingCargoDropSearchPatch
    {
        private static Map lastLoggedMap;

        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DropCellFinder),
                nameof(DropCellFinder.TryFindDropSpotNear), new Type[]
                {
                    typeof(IntVec3), typeof(Map),
                    typeof(IntVec3).MakeByRefType(), typeof(bool), typeof(bool),
                    typeof(int), typeof(bool), typeof(IntVec2?), typeof(bool)
                });
        }

        [HarmonyPrefix]
        private static bool Prefix(IntVec3 center, Map map,
            ref IntVec3 result, bool allowFogged, bool canRoofPunch,
            int maxRadius, bool allowIndoors, IntVec2? size,
            bool mustBeReachableFromCenter, ref bool __result)
        {
            if (!mustBeReachableFromCenter || allowFogged || !canRoofPunch
                || map == null
                || Current.ProgramState != ProgramState.MapInitializing
                || MapGenerator.mapBeingGenerated != map
                || Find.GameInitData == null
                || !MapGenerator.PlayerStartSpotValid
                || center != MapGenerator.PlayerStartSpot
                || maxRadius != 16 || !allowIndoors || size.HasValue
                || map.GetComponent<CARegionalProjectionMapComponent>()?.Active
                    != true)
                return true;

            Predicate<IntVec3> validator = cell => cell.InBounds(map)
                && DropCellFinder.IsGoodDropSpot(cell, map, allowFogged,
                    canRoofPunch, true);
            int radius = 5;
            while (radius <= maxRadius)
            {
                if (CellFinder.TryFindRandomCellNear(center, map, radius,
                        validator, out result))
                {
                    __result = true;
                    LogOnce(map);
                    return false;
                }
                radius++;
            }

            result = center;
            __result = false;
            LogOnce(map);
            return false;
        }

        private static void LogOnce(Map map)
        {
            if (ReferenceEquals(lastLoggedMap, map)) return;
            lastLoggedMap = map;
            Log.Message("[CA][Regional] starting cargo uses bounded local drop "
                + "validation without reading or rebuilding the global region "
                + "graph");
        }
    }

    // Item policy and the apparel retry clock are game-owned and scribed. A one-time
    // migration clears the former blanket protection of originless red Xs; after
    // that boundary, only observed player Forbid actions enter protected policy.
    public class OperationalAccessComponent : GameComponent
    {
        private Dictionary<int, int> nextApparelTick = new Dictionary<int, int>();
        private HashSet<int> playerForbidden = new HashSet<int>();
        private bool playerPolicyInitialized;
        private int playerPolicyVersion;
        public static OperationalAccessComponent Instance;

        private const int ApparelRetryCooldown = 2500;
        private const int AccessScanInterval = 60;
        private const int CurrentPlayerPolicyVersion = 1;

        public OperationalAccessComponent(Game game)
        {
            Instance = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref nextApparelTick, "CA_operationalApparelNext",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref playerForbidden,
                "CA_operationalPlayerForbidden", LookMode.Value);
            Scribe_Values.Look(ref playerPolicyInitialized,
                "CA_operationalPolicyInitialized", false);
            Scribe_Values.Look(ref playerPolicyVersion,
                "CA_operationalPolicyVersion", 0);
            if (nextApparelTick == null)
                nextApparelTick = new Dictionary<int, int>();
            if (playerForbidden == null)
                playerForbidden = new HashSet<int>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                UpgradePlayerPolicy();
            Instance = this;
        }

        public override void StartedNewGame()
        {
            playerPolicyInitialized = true;
            playerPolicyVersion = CurrentPlayerPolicyVersion;
            MaintainAllMaps();
        }

        public override void LoadedGame()
        {
            UpgradePlayerPolicy();
            playerPolicyInitialized = true;
            MaintainAllMaps();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % AccessScanInterval != 0) return;
            if (!playerPolicyInitialized)
            {
                playerPolicyInitialized = true;
                playerPolicyVersion = CurrentPlayerPolicyVersion;
            }
            AwarenessSettings settings = AwarenessMod.Settings;
            if (!CABehaviorSettings.IsEnabled(
                    CASettingKey.OperationalAccess, settings)) return;
            MaintainAllMaps();
        }

        private void MaintainAllMaps()
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (!CABehaviorSettings.IsEnabled(
                    CASettingKey.OperationalAccess, settings)) return;
            List<Map> maps = Find.Maps;
            if (maps == null) return;
            for (int i = 0; i < maps.Count; i++) MaintainMap(maps[i]);
        }

        private void UpgradePlayerPolicy()
        {
            if (playerPolicyVersion >= CurrentPlayerPolicyVersion) return;
            playerForbidden.Clear();
            playerPolicyInitialized = true;
            playerPolicyVersion = CurrentPlayerPolicyVersion;
            CATrace.Log("operational access migrated originless red-X policy");
        }

        private void MaintainMap(Map map)
        {
            if (map == null) return;
            CABehaviorDecision policyDecision = CABehaviorGate.Evaluate(
                "operations.shared_item_access", new CABehaviorContext(
                    actor: null, actorContext: CAActorContext.PlayerColony,
                    initiative: CAInitiativeTier.Standard,
                    authorityOrigin: CAAuthorityOrigin.PlayerDelegated,
                    authoritySatisfied: true, knowledgeSatisfied: true,
                    knowledgeFresh: true, liveValidated: true,
                    capabilitySatisfied: true, materialSatisfied: true,
                    currentIntentCompatible: true,
                    directPlayerOwnership: false,
                    authorityBasis: "enabled player colony access policy",
                    knowledgeBasis: "ordinary visible item state",
                    owner: "player colony access policy"));
            if (!policyDecision.Allowed) return;
            List<Thing> things = map.listerThings
                .ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                CompForbiddable comp = thing != null
                    ? thing.TryGetComp<CompForbiddable>() : null;
                if (thing == null || comp == null || !comp.Forbidden
                    || playerForbidden.Contains(thing.thingIDNumber)
                    || !OrdinaryVisibleItem(thing, map))
                    continue;

                thing.SetForbidden(false, warnOnFail: false);
                CATrace.Log("operational access allowed " + thing.LabelShort
                    + " under player colony policy");
            }
        }

        internal static void NotifyAutonomyChanged(Pawn pawn)
        {
            OperationalAccessComponent component = Instance;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (component == null || !CABehaviorSettings.IsEnabled(
                    CASettingKey.OperationalAccess, settings) || pawn == null
                || pawn.Map == null) return;
            component.MaintainMap(pawn.Map);
        }

        internal string Census(Map map)
        {
            if (map == null) return "[CA] operational access: no current map";
            int proactive = 0;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                if (AutonomyComponent.AtLeast(colonists[i], CAInitiativeTier.Proactive)) proactive++;

            int visible = 0;
            int allowed = 0;
            int systemForbidden = 0;
            int playerDenied = 0;
            List<Thing> things = map.listerThings
                .ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!OrdinaryVisibleItem(thing, map)) continue;
                visible++;
                CompForbiddable comp = thing.TryGetComp<CompForbiddable>();
                if (comp == null || !comp.Forbidden) allowed++;
                else if (playerForbidden.Contains(thing.thingIDNumber)) playerDenied++;
                else systemForbidden++;
            }
            return "[CA] operational access: colony policy enabled, Proactive+ personal equipment actors "
                + proactive + ", ordinary visible items " + visible
                + ", allowed " + allowed
                + ", system-forbidden " + systemForbidden + ", player-forbidden "
                + playerDenied + ", policy version " + playerPolicyVersion;
        }

        internal string CategorizedCensus(Map map)
        {
            if (map == null) return "[CA] operational access: no current map";
            string[] labels =
            {
                "food", "medicine", "weapons", "apparel", "raw materials",
                "other"
            };
            int[,] counts = new int[labels.Length, 4];
            List<Thing> things = map.listerThings
                .ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!OrdinaryVisibleItem(thing, map)) continue;
                int category = OperationalCategory(thing);
                counts[category, 0]++;
                CompForbiddable comp = thing.TryGetComp<CompForbiddable>();
                if (comp == null || !comp.Forbidden) counts[category, 1]++;
                else if (playerForbidden.Contains(thing.thingIDNumber))
                    counts[category, 3]++;
                else counts[category, 2]++;
            }

            var text = new StringBuilder(Census(map));
            for (int i = 0; i < labels.Length; i++)
            {
                text.Append('\n').Append(labels[i]).Append(": visible ")
                    .Append(counts[i, 0]).Append(", allowed ")
                    .Append(counts[i, 1]).Append(", system-forbidden ")
                    .Append(counts[i, 2]).Append(", player-forbidden ")
                    .Append(counts[i, 3]);
            }
            return text.ToString();
        }

        private static int OperationalCategory(Thing thing)
        {
            ThingDef def = thing.def;
            if (CAStorageProfile.IsMedicalSupply(def)) return 1;
            if (def.IsWeapon) return 2;
            if (thing is Apparel) return 3;
            if (def.IsNutritionGivingIngestible) return 0;
            if (def.IsStuff) return 4;
            return 5;
        }

        internal static bool OrdinaryVisibleItem(Thing thing, Map map)
        {
            return thing.Spawned && thing.Map == map
                && thing.def.category == ThingCategory.Item
                && !(thing is Corpse) && !HiddenThingsComponent.IsStashed(thing)
                && thing.questTags.NullOrEmpty()
                && !thing.IsBurning() && !thing.Position.Fogged(map)
                && (thing.Faction == null || thing.Faction == Faction.OfPlayer);
        }

        internal static bool IsPlayerStorage(Thing thing)
        {
            if (thing == null || !thing.Spawned) return false;
            IHaulDestination destination = StoreUtility.CurrentHaulDestinationOf(thing);
            if (destination is Zone_Stockpile) return true;
            Building building = destination as Building;
            return building != null && building.Faction == Faction.OfPlayer;
        }

        internal static void RecordPlayerPolicy(Thing thing, bool forbidden)
        {
            OperationalAccessComponent component = Instance;
            if (component == null || thing == null
                || thing.def.category != ThingCategory.Item) return;
            if (forbidden) component.playerForbidden.Add(thing.thingIDNumber);
            else component.playerForbidden.Remove(thing.thingIDNumber);
        }

        internal static bool IsPlayerForbidden(Thing thing)
        {
            OperationalAccessComponent component = Instance;
            return component != null && thing != null
                && component.playerForbidden.Contains(thing.thingIDNumber);
        }

        internal static bool MayIssueApparel(Pawn pawn)
        {
            OperationalAccessComponent component = Instance;
            if (component == null || pawn == null) return false;
            int id = pawn.thingIDNumber;
            int next;
            return !component.nextApparelTick.TryGetValue(id, out next)
                || Find.TickManager.TicksGame >= next;
        }

        internal static void MarkApparelIssued(Pawn pawn)
        {
            OperationalAccessComponent component = Instance;
            if (component == null || pawn == null) return;
            int id = pawn.thingIDNumber;
            component.nextApparelTick[id] = Find.TickManager.TicksGame
                + ApparelRetryCooldown;
        }
    }

    internal static class OperationalForbidInput
    {
        [ThreadStatic]
        internal static int ToggleDepth;
    }

    // The item gizmo writes CompForbiddable directly inside Command_Toggle's
    // action. This short UI-only context captures that native player path without
    // treating simulation code that calls SetForbidden as player policy.
    [HarmonyPatch(typeof(Command_Toggle), nameof(Command_Toggle.ProcessInput))]
    public static class Patch_CAForbidToggleContext
    {
        public static void Prefix(Command_Toggle __instance, out bool __state)
        {
            __state = __instance != null && __instance.tutorTag == "ToggleForbidden";
            if (__state) OperationalForbidInput.ToggleDepth++;
        }

        public static void Postfix(bool __state)
        {
            if (__state && OperationalForbidInput.ToggleDepth > 0)
                OperationalForbidInput.ToggleDepth--;
        }

        public static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__exception != null && __state && OperationalForbidInput.ToggleDepth > 0)
                OperationalForbidInput.ToggleDepth--;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(CompForbiddable), "set_Forbidden")]
    public static class Patch_CARecordForbidToggle
    {
        public static void Postfix(CompForbiddable __instance, bool value)
        {
            if (OperationalForbidInput.ToggleDepth > 0 && __instance != null)
                OperationalAccessComponent.RecordPlayerPolicy(__instance.parent, value);
        }
    }

    [HarmonyPatch(typeof(Designator_Forbid), nameof(Designator_Forbid.DesignateThing))]
    public static class Patch_CARecordForbidDesignator
    {
        public static void Postfix(Thing t)
        {
            OperationalAccessComponent.RecordPlayerPolicy(t, true);
        }
    }

    [HarmonyPatch(typeof(Designator_Unforbid), nameof(Designator_Unforbid.DesignateThing))]
    public static class Patch_CARecordUnforbidDesignator
    {
        public static void Postfix(Thing t)
        {
            OperationalAccessComponent.RecordPlayerPolicy(t, false);
        }
    }

    [HarmonyPatch(typeof(CompForbiddable), nameof(CompForbiddable.PostSplitOff))]
    public static class Patch_CAPropagatePlayerForbidToSplit
    {
        public static void Postfix(CompForbiddable __instance, Thing piece)
        {
            if (__instance != null
                && OperationalAccessComponent.IsPlayerForbidden(__instance.parent))
                OperationalAccessComponent.RecordPlayerPolicy(piece, true);
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.TryAbsorbStack))]
    public static class Patch_CAPropagatePlayerForbidToMerge
    {
        public struct MergeState
        {
            public bool denied;
            public int receiverCount;
            public int donorCount;
        }

        public static void Prefix(ThingWithComps __instance, Thing other,
            out MergeState __state)
        {
            __state = new MergeState
            {
                denied = OperationalAccessComponent.IsPlayerForbidden(__instance)
                    || OperationalAccessComponent.IsPlayerForbidden(other),
                receiverCount = __instance != null ? __instance.stackCount : -1,
                donorCount = other != null ? other.stackCount : -1
            };
        }

        public static void Postfix(ThingWithComps __instance, Thing other,
            MergeState __state)
        {
            bool changed = (__instance != null
                    && __instance.stackCount != __state.receiverCount)
                || (other != null && (other.Destroyed
                    || other.stackCount != __state.donorCount));
            if (!__state.denied || !changed) return;
            if (__instance != null && !__instance.Destroyed)
            {
                OperationalAccessComponent.RecordPlayerPolicy(__instance, true);
                __instance.SetForbidden(true, warnOnFail: false);
            }
            if (other != null && !other.Destroyed && other.stackCount > 0)
            {
                OperationalAccessComponent.RecordPlayerPolicy(other, true);
                other.SetForbidden(true, warnOnFail: false);
            }
        }
    }

    // Runs immediately before CA's combat reaction giver. It does not assign jobs
    // from a map tick and does not create an equipment-management loop: it returns
    // at most one native Equip or Wear job through RimWorld's constant-think lane.
    public class JobGiver_CAOperationalEquipment : ThinkNode_JobGiver
    {
        private readonly struct ThreatBasis
        {
            public readonly ThreatContactSnapshot Contact;
            public readonly bool Found;

            public ThreatBasis(ThreatContactSnapshot contact)
            {
                Contact = contact;
                Found = true;
            }
        }

        internal const float EquipmentSearchRadius = 36f;
        private const float ApparelSafetyRadius = 8f;
        private const float MinimumNetProtection = 0.15f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Job current = pawn != null ? pawn.CurJob : null;
            bool ownEquipmentJob = current != null && current.jobGiver == this
                && (current.def == JobDefOf.Equip || current.def == JobDefOf.Wear);
            if (!CanAct(pawn, ownEquipmentJob)) return null;

            CACombatConditionSnapshot condition =
                CACombatConditionSnapshot.Capture(pawn);
            if (condition.RequiresCombatRecovery(ranged: true))
            {
                if (pawn.IsHashIntervalTick(300))
                    CATrace.Skip(pawn, "operational equipment",
                        "health-adjusted combat recovery outranks equipment acquisition ("
                        + condition.TraceText() + ")", anchor: pawn.Position);
                return null;
            }

            ThreatBasis threat;
            if (!TryThreatBasis(pawn, out threat)) return null;
            if (ownEquipmentJob)
            {
                if (current.def == JobDefOf.Wear
                    && PerceivedThreatWithin(pawn, ApparelSafetyRadius))
                    return null;
                CAIntentContext existing;
                bool ownsReceipt = CABehaviorIntentMapComponent.For(pawn.Map)
                    ?.TryGet(pawn, current, out existing) == true;
                string continuingKey = current.def == JobDefOf.Wear
                    ? "operations.wear_protection"
                    : "operations.arm_for_known_threat";
                CABehaviorDecision continuing = CABehaviorGate.Evaluate(
                    continuingKey, EquipmentContext(pawn, threat,
                        CAAuthorityOrigin.Continuation,
                        ownsReceipt,
                        capabilitySatisfied: true,
                        materialSatisfied: current.targetA.IsValid));
                if (!continuing.Allowed) return null;
                return current;
            }

            if (pawn.equipment.Primary == null && MayAcquireWeapon(pawn))
            {
                string rejection;
                ThingWithComps weapon = BestPermittedWeapon(pawn, out rejection);
                if (weapon != null)
                {
                    Job equip = JobGiver_PickupDroppedWeapon.PickupWeaponJob(
                        pawn, weapon, false);
                    if (equip != null)
                    {
                        equip.expiryInterval = 600;
                        equip.locomotionUrgency = LocomotionUrgency.Jog;
                        CABehaviorDecision decision;
                        CAIntentContext intent;
                        if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(
                            pawn, equip,
                            "operations.arm_for_known_threat",
                            CAIntentController.RaidDefense,
                            EquipmentContext(pawn, threat,
                                CAAuthorityOrigin.PlayerDelegated,
                                authoritySatisfied: true,
                                capabilitySatisfied: true,
                                materialSatisfied: weapon.Spawned),
                            out decision, out intent,
                            targetOrDemand: weapon.LabelShort,
                            ownershipScope: "personal threat equipment",
                            lifetimeTicks: 2500))
                        {
                            CATrace.Skip(pawn, "operational equipment",
                                decision.PrimaryReason);
                            return null;
                        }
                        CATrace.Pawn(pawn, "takes permitted weapon " + weapon.LabelShort);
                        return equip;
                    }
                    rejection = "selected weapon no longer reservable and reachable";
                }
                CATrace.Skip(pawn, "operational equipment", rejection);
            }

            if (PerceivedThreatWithin(pawn, ApparelSafetyRadius))
            {
                CATrace.Skip(pawn, "protective apparel",
                    "perceived contact is within 8 cells");
                return null;
            }
            if (!OperationalAccessComponent.MayIssueApparel(pawn)) return null;

            string apparelRejection;
            Apparel apparel = BestPermittedApparel(pawn, out apparelRejection);
            if (apparel == null)
            {
                CATrace.Skip(pawn, "protective apparel", apparelRejection);
                return null;
            }

            Job wear = JobMaker.MakeJob(JobDefOf.Wear, apparel);
            wear.expiryInterval = 600;
            wear.locomotionUrgency = LocomotionUrgency.Jog;
            CABehaviorDecision wearDecision;
            CAIntentContext wearIntent;
            if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(pawn, wear,
                "operations.wear_protection",
                CAIntentController.RaidDefense,
                EquipmentContext(pawn, threat,
                    CAAuthorityOrigin.PlayerDelegated,
                    authoritySatisfied: true,
                    capabilitySatisfied: true,
                    materialSatisfied: apparel.Spawned),
                out wearDecision, out wearIntent,
                targetOrDemand: apparel.LabelShort,
                ownershipScope: "personal threat equipment",
                lifetimeTicks: 2500))
            {
                CATrace.Skip(pawn, "protective apparel",
                    wearDecision.PrimaryReason);
                return null;
            }
            OperationalAccessComponent.MarkApparelIssued(pawn);
            CATrace.Pawn(pawn, "wears permitted protective apparel "
                + apparel.LabelShort);
            return wear;
        }

        private static CABehaviorContext EquipmentContext(Pawn pawn,
            ThreatBasis threat, CAAuthorityOrigin origin,
            bool authoritySatisfied, bool capabilitySatisfied,
            bool materialSatisfied)
        {
            int now = Find.TickManager.TicksGame;
            Job current = pawn?.CurJob;
            return CABehaviorContext.ForPawn(pawn, origin,
                authoritySatisfied: authoritySatisfied,
                knowledgeSatisfied: threat.Found
                    && threat.Contact.State == ThreatContactState.Active,
                knowledgeFresh: threat.Found,
                liveValidated: true,
                knowledgeRelayed: threat.Found
                    && !threat.Contact.Evidence.IsDirect,
                knowledgeAgeTicks: threat.Found
                    ? System.Math.Max(0, now - threat.Contact.SourceTick)
                    : int.MaxValue,
                capabilitySatisfied: capabilitySatisfied,
                materialSatisfied: materialSatisfied,
                currentIntentCompatible: current == null
                    || origin == CAAuthorityOrigin.Continuation
                    || !current.playerForced,
                directPlayerOwnership: origin
                        != CAAuthorityOrigin.Continuation
                    && current != null && current.playerForced,
                authorityBasis: origin == CAAuthorityOrigin.Continuation
                    ? "persisted equipment intent"
                    : "personal equipment choice inside enabled colony policy",
                knowledgeBasis: threat.Found
                    ? (threat.Contact.Evidence.IsDirect
                        ? "direct current threat fact"
                        : "physically relayed threat fact")
                    : "no current threat fact",
                owner: nameof(JobGiver_CAOperationalEquipment));
        }

        private static bool CanAct(Pawn pawn, bool ownEquipmentJob)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            CAEffectiveBehaviorProfile profile =
                CAEffectiveBehaviorProfileCache.Of(pawn);
            if (!CABehaviorSettings.IsEnabled(
                    CASettingKey.OperationalAccess, settings) || pawn == null
                || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.InMentalState
                || !pawn.IsColonistPlayerControlled || pawn.Drafted || !pawn.Awake()
                || profile == null
                || !profile.Includes("operations.arm_for_known_threat")
                || pawn.jobs == null
                || pawn.equipment == null || pawn.health == null
                || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                return false;

            // Equipment readiness runs immediately ahead of combat reaction. Once
            // the pawn has actually been harmed, do not let a continuing Equip or
            // Wear package mask the native damage-driven survival override.
            if (CAImmediateCombat.HarmedRecently(pawn)) return false;

            Job current = pawn.CurJob;
            if (current != null && (current.playerForced || IsEmergency(current)
                || !ownEquipmentJob && (current.def == JobDefOf.Equip
                    || current.def == JobDefOf.Wear)))
                return false;
            if (pawn.jobs.jobQueue != null && pawn.jobs.jobQueue.AnyPlayerForced)
                return false;

            WithdrawalMapComponent withdrawal =
                pawn.Map.GetComponent<WithdrawalMapComponent>();
            if (withdrawal != null && withdrawal.InPlan(pawn)) return false;
            Lord lord = pawn.GetLord();
            if (lord != null && lord.LordJob is LordJob_CAStackBreach) return false;
            if (HiddenRegistry.IsHiddenOrOrdered(pawn)) return false;
            if (CATactical.HasExplicitOrder(pawn)) return false;
            return true;
        }

        private static bool MayAcquireWeapon(Pawn pawn)
        {
            return pawn != null && pawn.equipment != null
                && !pawn.WorkTagIsDisabled(WorkTags.Violent)
                && !SquadComponent.CombatLiability(pawn);
        }

        private static bool IsEmergency(Job job)
        {
            if (job == null) return false;
            return job.def == JobDefOf.TendPatient || job.def == JobDefOf.TendEntity
                || job.def == JobDefOf.Rescue || job.def == CA_Defs.EmergencySelfTend
                || job.def == JobDefOf.BeatFire
                || job.def == JobDefOf.ExtinguishSelf
                || job.def == JobDefOf.ExtinguishFiresNearby
                || job.def == JobDefOf.Flee || job.def == JobDefOf.FleeAndCower
                || job.def == JobDefOf.FleeAndCowerShort;
        }

        internal static bool ThreatKnown(Pawn pawn)
        {
            ThreatBasis basis;
            return TryThreatBasis(pawn, out basis);
        }

        private static bool TryThreatBasis(Pawn pawn,
            out ThreatBasis basis)
        {
            basis = default(ThreatBasis);
            if (pawn == null || pawn.Map == null) return false;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings != null && settings.knowledgeContacts)
            {
                KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
                ThreatContactSnapshot contact;
                if (knowledge == null
                    || !knowledge.TryGetFreshestContact(pawn, out contact))
                    return false;
                basis = new ThreatBasis(contact);
                return true;
            }

            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (KnowledgeMapComponent.CanCurrentlySeeHostile(
                    pawn, pawns[i], 42f))
                {
                    int now = Find.TickManager.TicksGame;
                    Pawn hostile = pawns[i];
                    var evidence = new ContactEvidence(
                        ContactEvidenceSource.Visual,
                        hostile.thingIDNumber, 1f, 0f,
                        CommunicationChannel.None,
                        pawn.thingIDNumber);
                    basis = new ThreatBasis(new ThreatContactSnapshot(
                        hostile.thingIDNumber, hostile.Position, now, now,
                        ContactWeaponCategory.Unknown, evidence,
                        ThreatContactState.Active, now));
                    return true;
                }
            return false;
        }

        internal static bool PerceivedThreatWithin(Pawn pawn, float radius)
        {
            if (pawn == null || pawn.Map == null) return false;
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings != null && settings.knowledgeContacts)
            {
                KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
                if (knowledge == null) return false;
                List<ThreatContactSnapshot> contacts = knowledge.FreshContacts(pawn);
                for (int i = 0; i < contacts.Count; i++)
                    if (contacts[i].Cell.IsValid
                        && pawn.Position.DistanceTo(contacts[i].Cell) <= radius)
                        return true;
                return false;
            }

            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (KnowledgeMapComponent.CanCurrentlySeeHostile(
                    pawn, pawns[i], radius)) return true;
            return false;
        }

        internal static ThingWithComps BestPermittedWeapon(Pawn pawn,
            out string rejection)
        {
            rejection = "no permitted reachable ground weapon within 36 cells";
            if (pawn == null || pawn.Map == null || pawn.equipment == null)
                return null;

            List<Thing> weapons = pawn.Map.listerThings
                .ThingsInGroup(ThingRequestGroup.Weapon);
            ThingWithComps best = null;
            float bestScore = float.MinValue;
            int nearby = 0, forbidden = 0, incompatible = 0, unreachable = 0;
            float radiusSq = EquipmentSearchRadius * EquipmentSearchRadius;

            for (int i = 0; i < weapons.Count; i++)
            {
                ThingWithComps weapon = weapons[i] as ThingWithComps;
                if (weapon == null || !weapon.Spawned || weapon.Map != pawn.Map
                    || !weapon.def.IsWeapon
                    || (!weapon.def.IsMeleeWeapon && !weapon.def.IsRangedWeapon)
                    || pawn.Position.DistanceToSquared(weapon.Position) > radiusSq
                    || !KnownOperationalLocation(pawn, weapon))
                    continue;
                nearby++;
                if (weapon.IsForbidden(pawn)) { forbidden++; continue; }
                if (weapon.IsBurning()) { incompatible++; continue; }
                if (weapon.def.IsRangedWeapon
                    && pawn.WorkTagIsDisabled(WorkTags.Shooting))
                {
                    incompatible++;
                    continue;
                }
                string cantReason;
                if (!EquipmentUtility.CanEquip(weapon, pawn, out cantReason))
                {
                    incompatible++;
                    continue;
                }
                if (!pawn.CanReserveAndReach(weapon, PathEndMode.ClosestTouch,
                    Danger.Some))
                {
                    unreachable++;
                    continue;
                }

                float score = WeaponScore(pawn, weapon)
                    - pawn.Position.DistanceTo(weapon.Position) * 0.02f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = weapon;
                }
            }

            if (best == null && nearby > 0)
                rejection = "no usable nearby weapon (" + forbidden
                    + " forbidden, " + incompatible + " incompatible, "
                    + unreachable + " unreachable or reserved)";
            return best;
        }

        private static float WeaponScore(Pawn pawn, ThingWithComps weapon)
        {
            int shooting = pawn.skills != null
                ? pawn.skills.GetSkill(SkillDefOf.Shooting).Level : 0;
            int melee = pawn.skills != null
                ? pawn.skills.GetSkill(SkillDefOf.Melee).Level : 0;
            DispositionProfile disposition = Disposition.Of(pawn);

            if (weapon.def.IsMeleeWeapon)
            {
                float dps = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                float fit = 0.75f + melee / 40f
                    + disposition.aggression * 0.25f
                    + disposition.courage * 0.15f;
                return dps * fit;
            }

            CompEquippable equippable = weapon.TryGetComp<CompEquippable>();
            Verb verb = equippable != null ? equippable.PrimaryVerb : null;
            VerbProperties props = verb != null ? verb.verbProps : null;
            float damage = 1f;
            if (props != null && props.defaultProjectile != null
                && props.defaultProjectile.projectile != null)
                damage = props.defaultProjectile.projectile.GetDamageAmount(weapon)
                    * UnityEngine.Mathf.Max(1, props.burstShotCount);
            float cycle = (props != null ? props.warmupTime : 1f)
                + UnityEngine.Mathf.Max(0.1f,
                    weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            float range = props != null ? props.range : 0f;
            float fitRanged = 0.75f + shooting / 40f
                + disposition.discipline * 0.15f
                + (1f - disposition.aggression) * 0.10f;
            return damage / cycle * fitRanged + range * 0.02f;
        }

        internal static Apparel BestPermittedApparel(Pawn pawn,
            out string rejection)
        {
            rejection = "no permitted compatible protective apparel within 36 cells";
            if (pawn == null || pawn.Map == null || pawn.apparel == null
                || pawn.outfits == null || pawn.outfits.CurrentApparelPolicy == null
                || pawn.IsMutant && pawn.mutant.Def.disableApparel)
                return null;

            List<Thing> apparelThings = pawn.Map.listerThings
                .ThingsInGroup(ThingRequestGroup.Apparel);
            Apparel best = null;
            float bestScore = float.MinValue;
            int nearby = 0, forbidden = 0, policy = 0, incompatible = 0,
                unreachable = 0, tooLittleProtection = 0;
            float radiusSq = EquipmentSearchRadius * EquipmentSearchRadius;

            for (int i = 0; i < apparelThings.Count; i++)
            {
                Apparel apparel = apparelThings[i] as Apparel;
                if (apparel == null || !apparel.Spawned || apparel.Map != pawn.Map
                    || pawn.Position.DistanceToSquared(apparel.Position) > radiusSq
                    || !KnownOperationalLocation(pawn, apparel))
                    continue;
                nearby++;
                if (apparel.IsForbidden(pawn)) { forbidden++; continue; }
                if (!pawn.outfits.CurrentApparelPolicy.filter.Allows(apparel))
                {
                    policy++;
                    continue;
                }
                if (apparel.IsBurning() || !apparel.PawnCanWear(pawn)
                    || !ApparelUtility.HasPartsToWear(pawn, apparel.def)
                    || pawn.apparel.WouldReplaceLockedApparel(apparel))
                {
                    incompatible++;
                    continue;
                }
                string cantReason;
                if (!EquipmentUtility.CanEquip(apparel, pawn, out cantReason)
                    || WouldReplaceForcedApparel(pawn, apparel))
                {
                    incompatible++;
                    continue;
                }
                if (!pawn.CanReserveAndReach(apparel, PathEndMode.ClosestTouch,
                    Danger.Some))
                {
                    unreachable++;
                    continue;
                }

                float gain = NetProtectionGain(pawn, apparel);
                if (gain < MinimumNetProtection)
                {
                    tooLittleProtection++;
                    continue;
                }
                float score = gain
                    - pawn.Position.DistanceTo(apparel.Position) * 0.002f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = apparel;
                }
            }

            if (best == null && nearby > 0)
                rejection = "no suitable nearby apparel (" + forbidden
                    + " forbidden, " + policy + " outside apparel policy, "
                    + incompatible + " incompatible with locked or forced gear, "
                    + unreachable + " unreachable or reserved, "
                    + tooLittleProtection + " not meaningfully protective)";
            return best;
        }

        private static bool WouldReplaceForcedApparel(Pawn pawn, Apparel candidate)
        {
            if (pawn.outfits == null || pawn.outfits.forcedHandler == null)
                return false;
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
                if (!ApparelUtility.CanWearTogether(candidate.def, worn[i].def,
                        pawn.RaceProps.body)
                    && !pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(worn[i]))
                    return true;
            return false;
        }

        private static float NetProtectionGain(Pawn pawn, Apparel candidate)
        {
            float gain = Protection(candidate);
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
                if (!ApparelUtility.CanWearTogether(candidate.def, worn[i].def,
                    pawn.RaceProps.body)) gain -= Protection(worn[i]);
            return gain;
        }

        internal static bool IsOperationalSupply(Thing thing)
        {
            if (thing == null) return false;
            if (thing.def.IsWeapon
                || CAStorageProfile.IsMedicalSupply(thing.def)) return true;
            Apparel apparel = thing as Apparel;
            return apparel != null && Protection(apparel) >= MinimumNetProtection;
        }

        private static bool KnownOperationalLocation(Pawn pawn, Thing thing)
        {
            if (pawn == null || thing == null || !thing.Spawned
                || pawn.Map != thing.Map || thing.Position.Fogged(pawn.Map))
                return false;
            return OperationalAccessComponent.IsPlayerStorage(thing)
                || pawn.Map.areaManager.Home[thing.Position]
                || thing.Position.InHorDistOf(pawn.Position, EquipmentSearchRadius);
        }

        internal static float Protection(Apparel apparel)
        {
            return apparel.GetStatValue(StatDefOf.ArmorRating_Sharp) * 0.55f
                + apparel.GetStatValue(StatDefOf.ArmorRating_Blunt) * 0.35f
                + apparel.GetStatValue(StatDefOf.ArmorRating_Heat) * 0.10f;
        }
    }
}
