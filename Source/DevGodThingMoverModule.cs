using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Developer-only map editing aid. Ctrl-drag moves one existing item, including
    // a loose weapon. If the item is a stack, that stack stays intact.
    internal static class DevGodThingMover
    {
        private const float ActivationDistance = 5f;
        private const float ControlBridgeSeconds = 1f;
        private const float BridgedGestureLeaseSeconds = 1f;
        private const float NativeTopHudMinimum = 112f;
        private const float NativeResourceRailWidth = 130f;
        private const float NativeRightHudRailWidth = 300f;
        private const float NativeBottomTabHeight = 36f;

        private static readonly List<IntVec3> SourcePreview = new List<IntVec3>(1);
        private static readonly List<IntVec3> DestinationPreview = new List<IntVec3>(1);

        private static Thing item;
        private static Map map;
        private static IntVec3 source = IntVec3.Invalid;
        private static IntVec3 destination = IntVec3.Invalid;
        private static Vector2 mouseDownUi;
        private static bool active;
        private static float pendingControlUntil = -1f;
        private static bool inputBridgeClassificationPending;
        private static bool inputBridgeOwnsGesture;
        private static float inputBridgeGestureUntil = -1f;
        private static bool inputPatchReported;
        private static bool markedInputRejectionReported;

        internal static bool HandleMapInput()
        {
            Event ev = Event.current;
            if (ev == null)
                return true;

            ObserveControlBridge(ev);
            bool bridgedControl = ConsumePendingControlOnMouseDown(ev);

            if (!inputPatchReported)
            {
                inputPatchReported = true;
                Log.Message("[Colonist Awareness] God Mode item mover input patch is active.");
            }

            if (IsAgentBridgeEvent(ev))
            {
                if (!markedInputRejectionReported)
                {
                    markedInputRejectionReported = true;
                    Log.Warning("[Colonist Awareness] marked synthetic pointer events "
                        + "are not accepted by the God Mode item mover; use native "
                        + "Ctrl-drag or the exact developer verification command.");
                }
                return true;
            }

            bool releaseEvent = ev.rawType == EventType.MouseUp && ev.button == 0;
            bool pointerLeft = ev.rawType == EventType.MouseLeaveWindow
                || ev.type == EventType.MouseLeaveWindow;
            bool bridgeProtocolWasActive = item != null
                && (inputBridgeClassificationPending || inputBridgeOwnsGesture);
            bool rawDragEvent = item != null
                && ev.rawType == EventType.MouseDrag;
            bool validLeftDrag = rawDragEvent
                && ev.type == EventType.MouseDrag
                && ev.button == 0;
            bool classificationTimedOut = inputBridgeClassificationPending
                && !releaseEvent
                && !Input.GetMouseButton(0)
                && Time.realtimeSinceStartup > inputBridgeGestureUntil;
            if (inputBridgeClassificationPending
                && validLeftDrag
                && !classificationTimedOut)
            {
                bool globalLeftButton = Input.GetMouseButton(0);
                inputBridgeClassificationPending = false;
                inputBridgeOwnsGesture = true;
                inputBridgeGestureUntil = Time.realtimeSinceStartup
                    + BridgedGestureLeaseSeconds;
                Log.Message("[Colonist Awareness] God Mode item input: "
                    + "valid drag promoted to bounded independent-device hold "
                    + "tracking; globalLeftButton=" + globalLeftButton + ".");
            }
            bool validBridgeDrag = inputBridgeOwnsGesture
                && validLeftDrag
                && Time.realtimeSinceStartup <= inputBridgeGestureUntil;
            if (validBridgeDrag)
            {
                inputBridgeGestureUntil = Time.realtimeSinceStartup
                    + BridgedGestureLeaseSeconds;
            }
            bool bridgeTimedOut = inputBridgeOwnsGesture
                && Time.realtimeSinceStartup > inputBridgeGestureUntil;
            bool nativeButtonLost = !inputBridgeClassificationPending
                && !inputBridgeOwnsGesture
                && ev.rawType != EventType.MouseUp
                    && ev.rawType != EventType.MouseDown
                    && !Input.GetMouseButton(0);
            bool malformedBridgeInput = bridgeProtocolWasActive
                && (ev.rawType == EventType.MouseDown
                    || (ev.rawType == EventType.MouseDrag && !validLeftDrag)
                    || (ev.rawType == EventType.MouseUp && ev.button != 0));
            if (item != null && malformedBridgeInput)
            {
                bool consume = active;
                Log.Message("[Colonist Awareness] God Mode item drag canceled: "
                    + "the bridged gesture received a malformed or consumed "
                    + "mouse event.");
                CancelSelectionDrag();
                if (consume && ev.type != EventType.Used)
                    ev.Use();
                return !consume;
            }
            bool lostGesture = pointerLeft
                || !Application.isFocused
                || classificationTimedOut
                || bridgeTimedOut
                || nativeButtonLost;
            if (item != null && lostGesture)
            {
                string loss = pointerLeft
                    ? "the pointer left the window"
                    : !Application.isFocused
                        ? "RimWorld lost focus"
                        : classificationTimedOut
                            ? "the input-bridge classification expired before a valid drag"
                            : bridgeTimedOut
                            ? "the bridged gesture lease expired before release"
                            : "the native left-button state was released";
                Log.Message("[Colonist Awareness] God Mode item drag canceled: "
                    + loss + ".");
                CancelSelectionDrag();
                return true;
            }

            if (item != null && !CanContinue())
            {
                Log.Message("[Colonist Awareness] God Mode item drag canceled: source "
                    + "item, map, cell, or developer authority changed.");
                CancelSelectionDrag();
                return true;
            }

            bool cancelEvent = (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
                || (ev.rawType == EventType.MouseDown && ev.button == 1);
            if (item != null && cancelEvent)
            {
                // Once dragging, the mover owns cancellation. Before activation,
                // also let the cancel reach whichever tool was already selected.
                bool consume = active && ev.type != EventType.Used;
                CancelSelectionDrag();
                if (consume)
                {
                    ev.Use();
                    return false;
                }
                return true;
            }

            if (item == null)
            {
                if (!TryArm(ev, bridgedControl))
                    return true;

                // Own the complete gesture before a selected designator, targeter,
                // persistent debug tool, or the vanilla selector can consume it.
                // A competing tool remains selected across an activated drag; a
                // short click resolves as explicit item selection instead.
                Find.Selector.dragBox.active = false;
                ev.Use();
                return false;
            }

            // If a window or other interface surface consumed an owned mouse event,
            // cancel rather than moving an item behind that surface.
            if ((ev.rawType == EventType.MouseDrag || releaseEvent)
                && ev.type == EventType.Used)
            {
                Log.Message("[Colonist Awareness] God Mode item drag canceled: an "
                    + "interface surface consumed the owned mouse event.");
                CancelSelectionDrag();
                return false;
            }

            if (ev.rawType != EventType.MouseDrag && !releaseEvent)
            {
                return true;
            }

            Vector2 currentUi;
            Vector3 currentMap = EventMapPosition(ev, out currentUi);
            destination = currentMap.ToIntVec3();

            if (!active && ev.type == EventType.MouseDrag
                && Vector2.Distance(mouseDownUi, currentUi) >= ActivationDistance)
            {
                active = true;
                Find.Selector.dragBox.active = false;
                Log.Message("[Colonist Awareness] God Mode item drag activated: def="
                    + item.def.defName + ", thingID=" + item.ThingID + ", source="
                    + source + ", destination=" + destination + ".");
            }

            if (releaseEvent)
            {
                if (!active)
                {
                    Log.Message("[Colonist Awareness] God Mode item gesture remained "
                        + "a short press; selecting " + item.ThingID + ".");
                    SelectArmedItem();
                    ev.Use();
                    return false;
                }

                // A window or other UI surface may have consumed the release before
                // map input. In that case cancel instead of dropping behind the UI.
                if (ev.type != EventType.MouseUp)
                {
                    CancelSelectionDrag();
                    return true;
                }

                CommitDrop(currentUi);
                ev.Use();
                return false;
            }

            if (ev.type == EventType.MouseDrag)
            {
                Find.Selector.dragBox.active = false;
                ev.Use();
                return false;
            }

            return true;
        }

        internal static void DrawPreview()
        {
            if (!active)
                return;

            if (!CanContinue())
            {
                if (item != null)
                    CancelSelectionDrag();
                return;
            }

            SourcePreview.Clear();
            SourcePreview.Add(source);
            GenDraw.DrawFieldEdges(SourcePreview, Color.cyan);

            if (!destination.InBounds(map))
                return;

            DestinationPreview.Clear();
            DestinationPreview.Add(destination);
            string ignored;
            GenDraw.DrawFieldEdges(DestinationPreview,
                IsValidDestination(item, map, source, destination, out ignored)
                    ? Color.green
                    : Color.red);
        }

        private static bool TryArm(Event ev, bool pendingControlEvent)
        {
            bool mouseDownEvent = ev.type == EventType.MouseDown;
            bool nativeControl = ev.control
                || (ev.modifiers & EventModifiers.Control) != 0;
            bool bridgeUsed = pendingControlEvent && !nativeControl;
            // A separately observed, unconsumed Control event is independent
            // evidence that Unity's global mouse-button state may not represent
            // the device producing this IMGUI gesture. It never supplies Control
            // authorization when the mouse event already carries native Control.
            bool holdBridgeCandidate = pendingControlEvent;
            bool controlPressed = nativeControl || bridgeUsed;
            if (!mouseDownEvent || ev.button != 0 || !controlPressed)
                return false;

            string rejection;
            if (!CanUseTool(out rejection))
            {
                LogInputAttempt(ev, ev.mousePosition, "rejected: " + rejection,
                    bridgeUsed, holdBridgeCandidate);
                return false;
            }

            Vector2 uiPosition;
            Vector3 mapPosition = EventMapPosition(ev, out uiPosition);
            if (Mouse.IsInputBlockedNow)
            {
                LogInputAttempt(ev, uiPosition,
                    "rejected: an interface surface blocks map input",
                    bridgeUsed, holdBridgeCandidate);
                return false;
            }
            if (IsOverKnownNativeMapHud(uiPosition, out rejection))
            {
                LogInputAttempt(ev, uiPosition, "rejected: " + rejection,
                    bridgeUsed, holdBridgeCandidate);
                return false;
            }

            string hitEvidence;
            Thing candidate = ItemUnderPointer(mapPosition, out hitEvidence);
            if (candidate == null)
            {
                LogInputAttempt(ev, uiPosition,
                    "rejected: no eligible item; " + hitEvidence,
                    bridgeUsed, holdBridgeCandidate);
                return false;
            }

            item = candidate;
            map = candidate.Map;
            source = candidate.Position;
            destination = source;
            mouseDownUi = uiPosition;
            active = false;
            inputBridgeClassificationPending = holdBridgeCandidate;
            inputBridgeOwnsGesture = false;
            inputBridgeGestureUntil = holdBridgeCandidate
                ? Time.realtimeSinceStartup + BridgedGestureLeaseSeconds
                : -1f;
            LogInputAttempt(ev, uiPosition,
                "armed: def=" + candidate.def.defName + ", thingID="
                + candidate.ThingID + ", stackCount=" + candidate.stackCount + ", source="
                + source + "; " + hitEvidence,
                bridgeUsed, holdBridgeCandidate);
            return true;
        }

        private static Thing ItemUnderPointer(Vector3 mapPosition, out string evidence)
        {
            Map currentMap = Find.CurrentMap;
            IntVec3 cell = mapPosition.ToIntVec3();
            if (currentMap == null || !cell.InBounds(currentMap))
            {
                evidence = "eventCell=" + cell + " is outside the current map";
                return null;
            }

            TargetingParameters clickParams = new TargetingParameters
            {
                mustBeSelectable = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = true,
                mapObjectTargetsMustBeAutoAttackable = false
            };
            List<Thing> things = new List<Thing>();
            AddEligibleItemsAt(cell, mapPosition, currentMap, clickParams, things);
            IntVec3[] adjacent = GenAdj.AdjacentCells;
            for (int i = 0; i < adjacent.Length; i++)
            {
                IntVec3 adjacentCell = cell + adjacent[i];
                if (adjacentCell.InBounds(currentMap)
                    && adjacentCell.GetItemCount(currentMap) > 1)
                {
                    AddEligibleItemsAt(
                        adjacentCell,
                        mapPosition,
                        currentMap,
                        clickParams,
                        things);
                }
            }
            things.Sort((left, right) =>
            {
                float leftDistance = (left.TrueCenter() - mapPosition)
                    .MagnitudeHorizontalSquared();
                float rightDistance = (right.TrueCenter() - mapPosition)
                    .MagnitudeHorizontalSquared();
                int distanceOrder = leftDistance.CompareTo(rightDistance);
                return distanceOrder != 0
                    ? distanceOrder
                    : left.thingIDNumber.CompareTo(right.thingIDNumber);
            });
            for (int i = 0; i < things.Count; i++)
            {
                if (IsEligible(things[i]))
                {
                    evidence = HitEvidence(currentMap, cell, things);
                    return things[i];
                }
            }
            evidence = HitEvidence(currentMap, cell, things);
            return null;
        }

        private static void AddEligibleItemsAt(IntVec3 cell, Vector3 mapPosition,
            Map currentMap, TargetingParameters clickParams, List<Thing> candidates)
        {
            List<Thing> cellThings = cell.GetThingList(currentMap);
            for (int i = 0; i < cellThings.Count; i++)
            {
                Thing thing = cellThings[i];
                if (!candidates.Contains(thing)
                    && IsEligible(thing)
                    && clickParams.CanTarget(thing)
                    && (cell == mapPosition.ToIntVec3()
                        || (thing.TrueCenter() - mapPosition)
                            .MagnitudeHorizontalSquared() <= 0.25f))
                {
                    candidates.Add(thing);
                }
            }
        }

        private static string HitEvidence(Map currentMap, IntVec3 eventCell,
            List<Thing> hits)
        {
            List<Thing> cellThings = eventCell.GetThingList(currentMap);
            List<string> descriptions = new List<string>();
            for (int i = 0; i < cellThings.Count && descriptions.Count < 8; i++)
            {
                Thing thing = cellThings[i];
                descriptions.Add(thing.def.defName + "/" + thing.ThingID + "/"
                    + thing.def.category + "/selectable=" + thing.def.selectable);
            }
            return "eventCell=" + eventCell + ", eligibleHits=" + hits.Count
                + ", cellThings=["
                + string.Join(", ", descriptions) + "]";
        }

        private static void LogInputAttempt(Event ev, Vector2 eventPosition,
            string outcome, bool bridgedControl, bool holdBridgeCandidate)
        {
            Log.Message("[Colonist Awareness] God Mode item input: " + outcome
                + "; eventPosition=" + eventPosition + ", control=" + ev.control
                + ", controlBridge=" + bridgedControl
                + ", holdBridgeCandidate=" + holdBridgeCandidate
                + ", globalLeftButton=" + Input.GetMouseButton(0)
                + ", devMode=" + Prefs.DevMode + ", godMode="
                + DebugSettings.godMode + ", focused=" + Application.isFocused
                + ".");
        }

        private static bool IsEligible(Thing thing)
        {
            return thing != null
                && !thing.Destroyed
                && thing.Spawned
                && thing.Map == Find.CurrentMap
                && thing.def.category == ThingCategory.Item
                && !thing.def.AffectsRegions
                && !(thing is Corpse);
        }

        private static void ObserveControlBridge(Event ev)
        {
            if (IsAgentBridgeEvent(ev)
                || !Application.isFocused
                || ev.rawType == EventType.MouseLeaveWindow
                || !CanUseTool())
            {
                ClearPendingControlBridge();
                return;
            }

            bool controlKey = ev.keyCode == KeyCode.LeftControl
                || ev.keyCode == KeyCode.RightControl;
            if (!controlKey)
                return;

            bool keyDown = ev.type == EventType.KeyDown;
            bool keyUp = ev.rawType == EventType.KeyUp
                || ev.type == EventType.KeyUp;
            if (keyDown)
            {
                pendingControlUntil = Time.realtimeSinceStartup
                    + ControlBridgeSeconds;
                Log.Message("[Colonist Awareness] God Mode item input: Control "
                    + "key down observed; keyCode=" + ev.keyCode
                    + ", rawType=" + ev.rawType + ".");
            }
            else if (keyUp)
            {
                ClearPendingControlBridge();
                Log.Message("[Colonist Awareness] God Mode item input: Control "
                    + "key up observed; keyCode=" + ev.keyCode
                    + ", rawType=" + ev.rawType + ".");
            }
        }

        private static bool ConsumePendingControlOnMouseDown(Event ev)
        {
            if (ev.rawType != EventType.MouseDown)
                return false;

            bool bridged = pendingControlUntil >= 0f
                && Application.isFocused
                && Time.realtimeSinceStartup <= pendingControlUntil
                && ev.type == EventType.MouseDown
                && ev.button == 0;
            ClearPendingControlBridge();
            return bridged;
        }

        private static void ClearPendingControlBridge()
        {
            pendingControlUntil = -1f;
        }

        private static bool CanUseTool()
        {
            string ignored;
            return CanUseTool(out ignored);
        }

        private static bool CanUseTool(out string reason)
        {
            if (Current.ProgramState != ProgramState.Playing)
                reason = "program state is " + Current.ProgramState;
            else if (Current.Game == null)
                reason = "no game is active";
            else if (!Current.Game.PlayerHasControl)
                reason = "the player does not currently have control";
            else if (!Prefs.DevMode)
                reason = "Dev Mode is off";
            else if (!DebugSettings.godMode)
                reason = "God Mode is off";
            else if (Find.CurrentMap == null)
                reason = "no current map is available";
            else if (!WorldRendererUtility.DrawingMap)
                reason = "the world view is selected";
            else
            {
                reason = null;
                return true;
            }
            return false;
        }

        private static bool CanContinue()
        {
            return CanUseTool()
                && map == Find.CurrentMap
                && IsEligible(item)
                && item.Map == map
                && item.Position == source;
        }

        private static bool IsValidDestination(Thing movedItem, Map movedMap,
            IntVec3 movedSource, IntVec3 cell, out string reason)
        {
            if (!cell.InBounds(movedMap))
            {
                reason = "outside the map";
                return false;
            }

            if (!GenSpawn.CanSpawnAt(
                movedItem.def,
                cell,
                movedMap,
                movedItem.Rotation))
            {
                reason = "the item cannot physically occupy that cell";
                return false;
            }

            if (GenSpawn.WouldWipeAnythingWith(
                cell,
                movedItem.Rotation,
                movedItem.def,
                movedMap,
                other => other != movedItem))
            {
                reason = "placing the item there would displace or destroy something";
                return false;
            }

            List<Thing> things = cell.GetThingList(movedMap);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].def.IsBlueprint || things[i].def.IsFrame)
                {
                    reason = "a blueprint or construction frame occupies that cell";
                    return false;
                }
            }

            if (cell != movedSource
                && cell.GetItemCount(movedMap) >= cell.GetMaxItemsAllowedInCell(movedMap))
            {
                reason = "that cell has no open item slot";
                return false;
            }

            reason = null;
            return true;
        }

        private static void CommitDrop(Vector2 dropUi)
        {
            Thing movedItem = item;
            Map movedMap = map;
            IntVec3 movedSource = source;
            IntVec3 movedDestination = destination;

            if (!CanContinue())
            {
                CancelSelectionDrag();
                return;
            }

            if (movedDestination == movedSource)
            {
                CancelSelectionDrag();
                return;
            }

            string reason = null;
            if (Mouse.IsInputBlockedNow)
            {
                reason = "the pointer is over an interface surface";
                LookTargets target = movedDestination.InBounds(movedMap)
                    ? new LookTargets(movedDestination, movedMap)
                    : new LookTargets(movedItem);
                Messages.Message("God Mode item move rejected: " + reason + ".", target,
                    MessageTypeDefOf.RejectInput, historical: false);
                CancelSelectionDrag();
                return;
            }
            if (IsOverKnownNativeMapHud(dropUi, out reason))
            {
                LookTargets target = movedDestination.InBounds(movedMap)
                    ? new LookTargets(movedDestination, movedMap)
                    : new LookTargets(movedItem);
                Messages.Message("God Mode item move rejected: " + reason + ".", target,
                    MessageTypeDefOf.RejectInput, historical: false);
                Log.Message("[Colonist Awareness] God Mode item drag rejected: "
                    + reason + "; uiPosition=" + dropUi + ".");
                CancelSelectionDrag();
                return;
            }

            bool moved;
            try
            {
                moved = TryMoveItemCore(
                    movedItem,
                    movedMap,
                    movedSource,
                    movedDestination,
                    out reason);
            }
            catch
            {
                CancelSelectionDrag();
                throw;
            }
            if (!moved)
            {
                LookTargets target = movedDestination.InBounds(movedMap)
                    ? new LookTargets(movedDestination, movedMap)
                    : new LookTargets(movedItem);
                Messages.Message("God Mode item move rejected: " + reason + ".", target,
                    MessageTypeDefOf.RejectInput, historical: false);
                CancelSelectionDrag();
                return;
            }

            Messages.Message("God Mode moved " + movedItem.LabelCap + " x"
                    + movedItem.stackCount + ".", new LookTargets(movedItem),
                MessageTypeDefOf.SilentInput, historical: false);
            Log.Message("[Colonist Awareness] God Mode item drag: " + reason + ".");
            CancelSelectionDrag();
        }

        internal static bool TryMoveItemForDeveloperVerification(
            string exactThingId,
            int destinationX,
            int destinationZ,
            out string receiptOrReason)
        {
            string reason;
            if (!CanUseTool(out reason))
            {
                receiptOrReason = reason;
                return false;
            }
            if (item != null)
            {
                receiptOrReason = "a native God Mode item gesture is already active";
                return false;
            }
            if (string.IsNullOrEmpty(exactThingId))
            {
                receiptOrReason = "an exact ThingID is required";
                return false;
            }

            Map currentMap = Find.CurrentMap;
            Thing exactItem = null;
            int matches = 0;
            List<Thing> allThings = currentMap.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing candidate = allThings[i];
                if (string.Equals(candidate.ThingID, exactThingId,
                    StringComparison.Ordinal))
                {
                    exactItem = candidate;
                    matches++;
                }
            }

            if (matches != 1 || !IsEligible(exactItem))
            {
                receiptOrReason = matches == 0
                    ? "the exact ThingID is not a spawned eligible item on the current map"
                    : "the exact ThingID is not unique and eligible on the current map";
                return false;
            }

            IntVec3 exactSource = exactItem.Position;
            IntVec3 exactDestination = new IntVec3(destinationX, 0, destinationZ);
            if (!TryMoveItemCore(
                exactItem,
                currentMap,
                exactSource,
                exactDestination,
                out receiptOrReason))
            {
                return false;
            }

            Messages.Message("God Mode moved " + exactItem.LabelCap + " x"
                    + exactItem.stackCount + ".", new LookTargets(exactItem),
                MessageTypeDefOf.SilentInput, historical: false);
            Log.Message("[Colonist Awareness] God Mode item verification move: "
                + receiptOrReason + ".");
            return true;
        }

        private static bool TryMoveItemCore(Thing movedItem, Map movedMap,
            IntVec3 movedSource, IntVec3 movedDestination, out string receiptOrReason)
        {
            if (!IsEligible(movedItem)
                || movedMap != Find.CurrentMap
                || movedItem.Map != movedMap
                || movedItem.Position != movedSource)
            {
                receiptOrReason = "the exact item, map, or source cell changed";
                return false;
            }
            if (movedDestination == movedSource)
            {
                receiptOrReason = "source and destination are the same cell";
                return false;
            }
            if (!IsValidDestination(
                movedItem,
                movedMap,
                movedSource,
                movedDestination,
                out receiptOrReason))
            {
                return false;
            }

            string thingIdBefore = movedItem.ThingID;
            int stackCountBefore = movedItem.stackCount;
            MoveAndRefreshLocationCaches(
                movedItem,
                movedMap,
                movedSource,
                movedDestination);

            bool destinationContains = movedDestination
                .GetThingList(movedMap)
                .Contains(movedItem);
            bool sourceContains = movedSource
                .GetThingList(movedMap)
                .Contains(movedItem);
            bool postconditionsHold = movedItem.Spawned
                && movedItem.Map == movedMap
                && movedItem.Position == movedDestination
                && string.Equals(movedItem.ThingID, thingIdBefore,
                    StringComparison.Ordinal)
                && movedItem.stackCount == stackCountBefore
                && destinationContains
                && !sourceContains;

            receiptOrReason = "map=" + movedMap.uniqueID
                + ", source=" + movedSource
                + ", destination=" + movedDestination
                + ", def=" + movedItem.def.defName
                + ", thingID=" + movedItem.ThingID
                + ", stackCountBefore=" + stackCountBefore
                + ", stackCountAfter=" + movedItem.stackCount
                + ", spawnedAfter=" + movedItem.Spawned
                + ", positionAfter=" + movedItem.Position
                + ", destinationContains=" + destinationContains
                + ", sourceContains=" + sourceContains;
            if (!postconditionsHold)
            {
                Log.Error("[Colonist Awareness] God Mode item move postcondition "
                    + "failure: " + receiptOrReason + ".");
                throw new InvalidOperationException(
                    "God Mode item mutation outcome is indeterminate; inspect exact "
                    + "ThingID before any retry. " + receiptOrReason);
            }
            return true;
        }

        private static void MoveAndRefreshLocationCaches(Thing movedItem, Map movedMap,
            IntVec3 movedSource, IntVec3 movedDestination)
        {
            Room sourceRoom = movedMap.regionGrid.GetValidRegionAt_NoRebuild(movedSource)?.Room;
            Room destinationRoom = movedMap.regionGrid
                .GetValidRegionAt_NoRebuild(movedDestination)?.Room;
            ISlotGroupParent sourceSlots = movedSource.GetSlotGroup(movedMap)?.parent;
            ISlotGroupParent destinationSlots = movedDestination.GetSlotGroup(movedMap)?.parent;

            sourceSlots?.Notify_LostThing(movedItem);
            movedItem.Position = movedDestination;
            destinationSlots?.Notify_ReceivedThing(movedItem);

            if (sourceSlots != null)
                GenThing.TryDirtyAdjacentGroupContainers(sourceSlots, movedMap);
            if (destinationSlots != null && destinationSlots != sourceSlots)
                GenThing.TryDirtyAdjacentGroupContainers(destinationSlots, movedMap);

            sourceRoom?.Notify_ContainedThingSpawnedOrDespawned(movedItem);
            if (destinationRoom != sourceRoom)
                destinationRoom?.Notify_ContainedThingSpawnedOrDespawned(movedItem);

            movedMap.listerHaulables.RecalcAllInCell(movedSource);
            movedMap.listerHaulables.RecalcAllInCell(movedDestination);
            movedMap.listerMergeables.RecalcAllInCell(movedSource);
            movedMap.listerMergeables.RecalcAllInCell(movedDestination);
            StealAIDebugDrawer.Notify_ThingChanged(movedItem);
        }

        private static Vector3 EventMapPosition(Event ev, out Vector2 uiPosition)
        {
            Vector2 topLeft = UI.GUIToScreenPoint(ev.mousePosition);
            uiPosition = topLeft;
            Vector2 bottomLeft = new Vector2(topLeft.x, UI.screenHeight - topLeft.y);
            return UI.UIToMapPosition(bottomLeft);
        }

        private static bool IsOverKnownNativeMapHud(Vector2 uiPosition, out string reason)
        {
            if (float.IsNaN(uiPosition.x) || float.IsNaN(uiPosition.y)
                || uiPosition.x < 0f || uiPosition.y < 0f
                || uiPosition.x >= UI.screenWidth || uiPosition.y >= UI.screenHeight)
            {
                reason = "the pointer is outside the map viewport";
                return true;
            }

            WindowStack windowStack = Find.WindowStack;
            if (windowStack != null)
            {
                if (windowStack.AnyWindowAbsorbingAllInput)
                {
                    reason = "a modal interface window is active";
                    return true;
                }
                if (windowStack.GetWindowAt(uiPosition) != null)
                {
                    reason = "the pointer is over an interface window";
                    return true;
                }
            }

            if (TutorSystem.TutorialMode
                || (Find.ActiveLesson != null && Find.ActiveLesson.ActiveLessonVisible))
            {
                reason = "tutorial interface is active";
                return true;
            }

            if (Find.PlaySettings.showBeauty || Find.PlaySettings.showRoomStats)
            {
                reason = "a pointer-following map inspector is active";
                return true;
            }

            float messageAlpha;
            Rect pointerProbe = new Rect(uiPosition.x - 0.5f, uiPosition.y - 0.5f, 1f, 1f);
            if (Messages.CollidesWithAnyMessage(pointerProbe, out messageAlpha))
            {
                reason = "the pointer is over a live message";
                return true;
            }

            float topHudBottom = NativeTopHudMinimum;
            if (Find.PlaySettings.showColonistBar && Find.ColonistBar != null)
            {
                Vector2 colonistSize = Find.ColonistBar.Size;
                List<Vector2> colonistDrawLocs = Find.ColonistBar.DrawLocs;
                for (int i = 0; i < colonistDrawLocs.Count; i++)
                {
                    topHudBottom = Mathf.Max(
                        topHudBottom,
                        colonistDrawLocs[i].y + colonistSize.y * 2f + 12f);
                }
            }
            if (uiPosition.y < topHudBottom)
            {
                reason = "the pointer is over the top HUD exclusion band";
                return true;
            }

            if (uiPosition.x < NativeResourceRailWidth
                && uiPosition.y < UI.screenHeight - 200f)
            {
                reason = "the pointer is over the resource readout rail";
                return true;
            }

            if (uiPosition.x >= UI.screenWidth - NativeRightHudRailWidth)
            {
                reason = "the pointer is over the alerts and global-controls rail";
                return true;
            }

            float bottomHudHeight = Mathf.Max(
                NativeBottomTabHeight,
                GizmoGridDrawer.HeightDrawnRecently);
            if (uiPosition.y >= UI.screenHeight - bottomHudHeight)
            {
                reason = "the pointer is over the bottom tabs or command rail";
                return true;
            }

            reason = null;
            return false;
        }

        private static void SelectArmedItem()
        {
            Thing selectedItem = item;
            ClearState();
            if (!IsEligible(selectedItem))
                return;

            Find.Selector.ClearSelection();
            Find.Selector.Select(selectedItem);
        }

        private static void CancelSelectionDrag()
        {
            if (Find.MapUI != null)
                Find.Selector.dragBox.active = false;
            ClearState();
        }

        private static void ClearState()
        {
            item = null;
            map = null;
            source = IntVec3.Invalid;
            destination = IntVec3.Invalid;
            mouseDownUi = Vector2.zero;
            active = false;
            pendingControlUntil = -1f;
            inputBridgeClassificationPending = false;
            inputBridgeOwnsGesture = false;
            inputBridgeGestureUntil = -1f;
        }

        private static bool IsAgentBridgeEvent(Event ev)
        {
            return !string.IsNullOrEmpty(ev.commandName)
                && ev.commandName.StartsWith("RimWorldAgentInput.v1:",
                    StringComparison.Ordinal);
        }
    }

    [HarmonyPatch(typeof(MapInterface), nameof(MapInterface.HandleMapClicks))]
    internal static class DevGodThingMoverInputPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return DevGodThingMover.HandleMapInput();
        }
    }

    [HarmonyPatch(typeof(MapInterface), nameof(MapInterface.MapInterfaceUpdate))]
    internal static class DevGodThingMoverPreviewPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            DevGodThingMover.DrawPreview();
        }
    }
}
