using HarmonyLib;
using RimWorld;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.Steam;

namespace ColonistAwareness
{
    internal static class HalfSpeedState
    {
        private sealed class State
        {
            internal bool Active;
        }

        private static readonly ConditionalWeakTable<TickManager, State> States =
            new ConditionalWeakTable<TickManager, State>();
        private static bool settingHalfSpeed;

        internal static bool IsActive(TickManager manager)
        {
            return manager != null &&
                States.TryGetValue(manager, out State state) &&
                state.Active &&
                manager.CurTimeSpeed == TimeSpeed.Normal;
        }

        internal static void Select(TickManager manager)
        {
            if (manager == null ||
                manager.ForcePaused ||
                Current.Game == null ||
                !Current.Game.PlayerHasControl)
            {
                return;
            }

            settingHalfSpeed = true;
            try
            {
                manager.CurTimeSpeed = TimeSpeed.Normal;
            }
            finally
            {
                settingHalfSpeed = false;
            }

            States.GetOrCreateValue(manager).Active =
                manager.CurTimeSpeed == TimeSpeed.Normal;
        }

        internal static void Clear(TickManager manager)
        {
            if (manager != null && States.TryGetValue(manager, out State state))
            {
                state.Active = false;
            }
        }

        internal static void NotifyTimeSpeedSet(
            TickManager manager,
            TimeSpeed requestedSpeed)
        {
            if (settingHalfSpeed)
            {
                return;
            }

            // Some UI mods pause temporarily and restore the saved vanilla
            // speed through the property. Normal is the backing value for our
            // half-speed state, so preserve it across that pause/restore seam.
            if (manager != null &&
                manager.CurTimeSpeed == TimeSpeed.Paused &&
                requestedSpeed == TimeSpeed.Normal &&
                States.TryGetValue(manager, out State state) &&
                state.Active)
            {
                return;
            }

            Clear(manager);
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickRateMultiplier), MethodType.Getter)]
    internal static class HalfSpeedTickRatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(TickManager __instance, ref float __result)
        {
            if (HalfSpeedState.IsActive(__instance))
            {
                __result = 0.5f;
            }
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.CurTimeSpeed), MethodType.Setter)]
    internal static class HalfSpeedSelectionResetPatch
    {
        [HarmonyPrefix]
        private static void Prefix(TickManager __instance, TimeSpeed value)
        {
            HalfSpeedState.NotifyTimeSpeedSet(__instance, value);
        }
    }

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(TimeControls), nameof(TimeControls.DoTimeControlsGUI))]
    internal static class HalfSpeedTimeControlsPatch
    {
        private const int ButtonCount = 5;

        private enum ControlSpeed
        {
            Paused,
            Half,
            Normal,
            Fast,
            Superfast
        }

        private static Texture2D halfSpeedTexture;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(Rect timerRect)
        {
            TickManager tickManager = Find.TickManager;
            if (tickManager == null)
            {
                return false;
            }

            // Keep the native strip's right edge fixed and make room on its left.
            timerRect.x -= TimeControls.TimeButSize.x;
            timerRect.width = TimeControls.TimeButSize.x * ButtonCount;

            Widgets.BeginGroup(timerRect);
            Rect buttonRect = new Rect(
                0f,
                0f,
                TimeControls.TimeButSize.x,
                TimeControls.TimeButSize.y);

            DrawButton(buttonRect, ControlSpeed.Paused, tickManager);
            buttonRect.x += buttonRect.width;
            DrawButton(buttonRect, ControlSpeed.Half, tickManager);
            buttonRect.x += buttonRect.width;
            DrawButton(buttonRect, ControlSpeed.Normal, tickManager);
            buttonRect.x += buttonRect.width;
            DrawButton(buttonRect, ControlSpeed.Fast, tickManager);
            buttonRect.x += buttonRect.width;
            DrawButton(buttonRect, ControlSpeed.Superfast, tickManager);

            if (tickManager.slower.ForcedNormalSpeed)
            {
                Widgets.DrawLineHorizontal(
                    TimeControls.TimeButSize.x * 3f,
                    TimeControls.TimeButSize.y / 2f,
                    TimeControls.TimeButSize.x * 2f);
            }
            if (tickManager.ForcePaused)
            {
                Widgets.DrawLineHorizontal(
                    TimeControls.TimeButSize.x,
                    TimeControls.TimeButSize.y / 2f,
                    TimeControls.TimeButSize.x * 4f);
            }

            Widgets.EndGroup();
            GenUI.AbsorbClicksInRect(timerRect);
            UIHighlighter.HighlightOpportunity(timerRect, "TimeControls");
            HandleKeyboard(tickManager);
            return false;
        }

        private static void DrawButton(
            Rect rect,
            ControlSpeed controlSpeed,
            TickManager tickManager)
        {
            Texture2D texture = TextureFor(controlSpeed);
            string tooltip = TooltipFor(controlSpeed);
            bool clicked = Widgets.ButtonImage(
                rect,
                texture,
                doMouseoverSound: true,
                tooltip);

            if (clicked && !tickManager.ForcePaused)
            {
                Select(controlSpeed, tickManager);
            }

            if (IsSelected(controlSpeed, tickManager))
            {
                GUI.DrawTexture(rect, TexUI.HighlightTex);
            }
        }

        private static void Select(
            ControlSpeed controlSpeed,
            TickManager tickManager)
        {
            switch (controlSpeed)
            {
                case ControlSpeed.Paused:
                    tickManager.TogglePaused();
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(
                        ConceptDefOf.Pause,
                        KnowledgeAmount.SpecificInteraction);
                    break;
                case ControlSpeed.Half:
                    HalfSpeedState.Select(tickManager);
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(
                        ConceptDefOf.TimeControls,
                        KnowledgeAmount.SpecificInteraction);
                    break;
                case ControlSpeed.Normal:
                    SetVanillaSpeed(tickManager, TimeSpeed.Normal);
                    break;
                case ControlSpeed.Fast:
                    SetVanillaSpeed(tickManager, TimeSpeed.Fast);
                    break;
                case ControlSpeed.Superfast:
                    SetVanillaSpeed(tickManager, TimeSpeed.Superfast);
                    break;
            }

            PlaySoundOf(tickManager.CurTimeSpeed);
        }

        private static void SetVanillaSpeed(
            TickManager tickManager,
            TimeSpeed speed)
        {
            HalfSpeedState.Clear(tickManager);
            tickManager.CurTimeSpeed = speed;
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(
                ConceptDefOf.TimeControls,
                KnowledgeAmount.SpecificInteraction);
        }

        private static bool IsSelected(
            ControlSpeed controlSpeed,
            TickManager tickManager)
        {
            if (tickManager.ForcePaused)
            {
                return controlSpeed == ControlSpeed.Paused;
            }

            switch (controlSpeed)
            {
                case ControlSpeed.Paused:
                    return tickManager.CurTimeSpeed == TimeSpeed.Paused;
                case ControlSpeed.Half:
                    return HalfSpeedState.IsActive(tickManager);
                case ControlSpeed.Normal:
                    return tickManager.CurTimeSpeed == TimeSpeed.Normal &&
                        !HalfSpeedState.IsActive(tickManager);
                case ControlSpeed.Fast:
                    return tickManager.CurTimeSpeed == TimeSpeed.Fast;
                case ControlSpeed.Superfast:
                    return tickManager.CurTimeSpeed == TimeSpeed.Superfast;
                default:
                    return false;
            }
        }

        private static Texture2D TextureFor(ControlSpeed controlSpeed)
        {
            switch (controlSpeed)
            {
                case ControlSpeed.Paused:
                    return TexButton.SpeedButtonTextures[(uint)TimeSpeed.Paused];
                case ControlSpeed.Half:
                    return HalfSpeedTexture;
                case ControlSpeed.Normal:
                    return TexButton.SpeedButtonTextures[(uint)TimeSpeed.Normal];
                case ControlSpeed.Fast:
                    return TexButton.SpeedButtonTextures[(uint)TimeSpeed.Fast];
                case ControlSpeed.Superfast:
                    return TexButton.SpeedButtonTextures[(uint)TimeSpeed.Superfast];
                default:
                    return BaseContent.BadTex;
            }
        }

        private static string TooltipFor(ControlSpeed controlSpeed)
        {
            if (controlSpeed == ControlSpeed.Half)
            {
                return "Half speed (0.5x)";
            }

            KeyBindingDef binding = BindingFor(controlSpeed);
            string key = KeyPrefs.KeyPrefsData
                .GetBoundKeyCode(binding, KeyPrefs.BindingSlot.A)
                .ToStringReadable();
            return string.Format("{0}: {1}", "HotKeyTip".Translate(), key);
        }

        private static KeyBindingDef BindingFor(ControlSpeed controlSpeed)
        {
            switch (controlSpeed)
            {
                case ControlSpeed.Paused:
                    return KeyBindingDefOf.TogglePause;
                case ControlSpeed.Normal:
                    return KeyBindingDefOf.TimeSpeed_Normal;
                case ControlSpeed.Fast:
                    return KeyBindingDefOf.TimeSpeed_Fast;
                case ControlSpeed.Superfast:
                    return KeyBindingDefOf.TimeSpeed_Superfast;
                default:
                    return null;
            }
        }

        private static Texture2D HalfSpeedTexture
        {
            get
            {
                if (halfSpeedTexture == null)
                {
                    halfSpeedTexture = BuildHalfSpeedTexture();
                }
                return halfSpeedTexture;
            }
        }

        private static Texture2D BuildHalfSpeedTexture()
        {
            const int width = 32;
            const int height = 24;
            var pixels = new Color32[width * height];
            Color32 white = new Color32(255, 255, 255, 255);

            // A native-sized Play triangle, separated into two pieces by a
            // narrow diagonal transparent cut through its middle.
            for (int y = 3; y <= 20; y++)
            {
                float distanceFromCenter = Mathf.Abs(y - 11.5f);
                int rightEdge = Mathf.RoundToInt(24f - distanceFromCenter * 1.65f);
                int cutX = 13 + Mathf.RoundToInt((y - 3) * 0.28f);
                for (int x = 8; x <= rightEdge; x++)
                {
                    if (Mathf.Abs(x - cutX) <= 1)
                    {
                        continue;
                    }
                    pixels[y * width + x] = white;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                name = "CA_HalfSpeed",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void HandleKeyboard(TickManager tickManager)
        {
            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }

            if (KeyBindingDefOf.TogglePause.KeyDownEvent)
            {
                tickManager.TogglePaused();
                PlaySoundOf(tickManager.CurTimeSpeed);
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(
                    ConceptDefOf.Pause,
                    KnowledgeAmount.SpecificInteraction);
                Event.current.Use();
            }

            if (!Find.WindowStack.WindowsForcePause)
            {
                if (KeyBindingDefOf.TimeSpeed_Normal.KeyDownEvent)
                {
                    SetVanillaSpeed(tickManager, TimeSpeed.Normal);
                    PlaySoundOf(tickManager.CurTimeSpeed);
                    Event.current.Use();
                }
                if (KeyBindingDefOf.TimeSpeed_Fast.KeyDownEvent)
                {
                    SetVanillaSpeed(tickManager, TimeSpeed.Fast);
                    PlaySoundOf(tickManager.CurTimeSpeed);
                    Event.current.Use();
                }
                if (KeyBindingDefOf.TimeSpeed_Superfast.KeyDownEvent)
                {
                    SetVanillaSpeed(tickManager, TimeSpeed.Superfast);
                    PlaySoundOf(tickManager.CurTimeSpeed);
                    Event.current.Use();
                }
                if (KeyBindingDefOf.TimeSpeed_Slower.KeyDownEvent &&
                    tickManager.CurTimeSpeed != TimeSpeed.Paused)
                {
                    SelectSlower(tickManager);
                    PlaySoundOf(tickManager.CurTimeSpeed);
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(
                        ConceptDefOf.TimeControls,
                        KnowledgeAmount.SpecificInteraction);
                    Event.current.Use();
                }
                if (KeyBindingDefOf.TimeSpeed_Faster.KeyDownEvent &&
                    (int)tickManager.CurTimeSpeed < (int)TimeSpeed.Superfast)
                {
                    SelectFaster(tickManager);
                    PlaySoundOf(tickManager.CurTimeSpeed);
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(
                        ConceptDefOf.TimeControls,
                        KnowledgeAmount.SpecificInteraction);
                    Event.current.Use();
                }
            }

            if (Prefs.DevMode)
            {
                if (KeyBindingDefOf.TimeSpeed_Ultrafast.KeyDownEvent)
                {
                    SetVanillaSpeed(tickManager, TimeSpeed.Ultrafast);
                    PlaySoundOf(tickManager.CurTimeSpeed);
                    Event.current.Use();
                }
                if (KeyBindingDefOf.Dev_TickOnce.KeyDownEvent &&
                    tickManager.CurTimeSpeed == TimeSpeed.Paused)
                {
                    tickManager.DoSingleTick();
                    SoundDefOf.Clock_Stop.PlayOneShotOnCamera();
                }
            }
        }

        private static void SelectSlower(TickManager tickManager)
        {
            if (HalfSpeedState.IsActive(tickManager))
            {
                tickManager.TogglePaused();
                return;
            }
            if (tickManager.CurTimeSpeed == TimeSpeed.Normal)
            {
                HalfSpeedState.Select(tickManager);
                return;
            }

            SetVanillaSpeed(
                tickManager,
                tickManager.CurTimeSpeed - 1);
        }

        private static void SelectFaster(TickManager tickManager)
        {
            if (tickManager.CurTimeSpeed == TimeSpeed.Paused)
            {
                HalfSpeedState.Select(tickManager);
                return;
            }
            if (HalfSpeedState.IsActive(tickManager))
            {
                SetVanillaSpeed(tickManager, TimeSpeed.Normal);
                return;
            }

            SetVanillaSpeed(
                tickManager,
                tickManager.CurTimeSpeed + 1);
        }

        private static void PlaySoundOf(TimeSpeed speed)
        {
            SoundDef sound = null;
            switch (speed)
            {
                case TimeSpeed.Paused:
                    sound = SoundDefOf.Clock_Stop;
                    break;
                case TimeSpeed.Normal:
                    sound = SoundDefOf.Clock_Normal;
                    break;
                case TimeSpeed.Fast:
                    sound = SoundDefOf.Clock_Fast;
                    break;
                case TimeSpeed.Superfast:
                case TimeSpeed.Ultrafast:
                    sound = SoundDefOf.Clock_Superfast;
                    break;
            }

            sound?.PlayOneShotOnCamera();
            SteamDeck.Vibrate();
        }
    }
}
