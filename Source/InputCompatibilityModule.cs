using HarmonyLib;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Unity exposes one process-global pointer through Input.mousePosition, while IMGUI
    // carries the pointer that produced the current event. Multi-pointer development
    // tools therefore reach ordinary widgets but miss map and pawn targets that ask UI
    // for the global pointer. During a button event, make that query event-local too.
    // Hover, movement, scrolling, and held-button state remain on RimWorld's native
    // input path; independent dragging is not claimed.
    //
    // The bridge covers every program state, not only Playing. RimWorld's own widgets
    // hit-test through Mouse.IsOver -> UI.MousePositionOnUI, so a play-state-only bridge
    // left the main menu and its dialogs unreachable by a second pointer: a second-seat
    // operator could play a loaded game but could not load one. Dev Mode, the two button
    // events, and the Linux/Steam Deck exclusion remain the gate.
    [HarmonyPatch(typeof(UI), nameof(UI.MousePositionOnUI), MethodType.Getter)]
    internal static class DevEventCursorBridgePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref Vector2 __result)
        {
            Event ev = Event.current;
            if (!Prefs.DevMode
                || ev == null
                || UnityGUIBugsFixer.IsSteamDeckOrLinuxBuild)
            {
                return true;
            }

            EventType rawType = ev.rawType;
            if (rawType != EventType.MouseDown && rawType != EventType.MouseUp)
            {
                return true;
            }

            Vector2 topLeft = UI.GUIToScreenPoint(ev.mousePosition);
            __result = new Vector2(topLeft.x, UI.screenHeight - topLeft.y);
            return false;
        }
    }
}
