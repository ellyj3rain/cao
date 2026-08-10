using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Replaces vanilla's preset-only Ideoligion page with one founding
    // authoring surface. Native Ideoligion remains native RimWorld state;
    // Culture, Political Beliefs, and the adopted arrangement use CA's shared
    // faction models.
    [HarmonyPatch(typeof(Scenario), nameof(Scenario.GetFirstConfigPage))]
    internal static class CAPlayerFoundingPageChainPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref Page __result)
        {
            if (__result == null) return;
            Page previous = null;
            Page current = __result;
            Page insertionAnchor = null;
            while (current != null)
            {
                if (current is Page_CAPlayerFounding) return;
                if (current is Page_SelectStartingSite
                    || (insertionAnchor == null
                        && current is Page_CreateWorldParams))
                    insertionAnchor = current;
                if (current is Page_ChooseIdeoPreset)
                {
                    var replacement = new Page_CAPlayerFounding(previous,
                        current.next);
                    replacement.nextAct = current.nextAct;
                    if (previous == null) __result = replacement;
                    else previous.next = replacement;
                    return;
                }
                previous = current;
                current = current.next;
            }

            // Culture, political beliefs, and the founding arrangement still
            // exist when the Ideology expansion is inactive.
            if (insertionAnchor != null)
            {
                var inserted = new Page_CAPlayerFounding(insertionAnchor,
                    insertionAnchor.next);
                inserted.nextAct = insertionAnchor.nextAct;
                insertionAnchor.nextAct = null;
                insertionAnchor.next = inserted;
            }
        }
    }

    internal sealed class Page_CAPlayerFounding : Page
    {
        private CAPlayerFoundingPlan draft;
        private string validationFailure;

        public override string PageTitle => "Founding society";

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(Mathf.Min(1500f, UI.screenWidth - 16f),
                    Mathf.Min(930f, UI.screenHeight - 16f));
            }
        }

        internal Page_CAPlayerFounding(Page previous, Page following)
        {
            prev = previous;
            next = following;
            if (following != null) following.prev = this;
            doCloseX = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = false;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            draft = CAPlayerFoundingSession.Current;
            EnsureEstablishedIdeos();
            EnsureNativeIdeo();
            CAPlayerFoundingModel.Ensure(draft);
            CAPlayerFoundingModel.CaptureNativeIdeo(draft,
                CAPlayerFoundingModel.NativeIdeo, null);
            CAPlayerFoundingSession.Save();
        }

        public override void PostClose()
        {
            CAPlayerFoundingModel.CaptureNativeIdeo(draft,
                CAPlayerFoundingModel.NativeIdeo, null);
            CAPlayerFoundingSession.Save();
            base.PostClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Text.Font = GameFont.Small;
            const string introduction = "Set the culture, Ideoligion, and "
                + "political beliefs the founders bring. The founding "
                + "arrangement is the order they establish at landing; later "
                + "institutions develop through play.";
            Widgets.Label(new Rect(0f, 38f, inRect.width, 42f), introduction);

            float top = 84f;
            float bottom = 52f;
            float gap = 12f;
            float cardWidth = (inRect.width - gap) / 2f;
            float cardHeight = (inRect.height - top - bottom - gap) / 2f;
            DrawCultureCard(new Rect(0f, top, cardWidth, cardHeight));
            DrawIdeoCard(new Rect(cardWidth + gap, top, cardWidth,
                cardHeight));
            DrawPoliticalCard(new Rect(0f, top + cardHeight + gap,
                cardWidth, cardHeight));
            DrawArrangementCard(new Rect(cardWidth + gap,
                top + cardHeight + gap, cardWidth, cardHeight));

            if (!validationFailure.NullOrEmpty())
            {
                GUI.color = ColorLibrary.RedReadable;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(inRect.width * 0.35f,
                    inRect.height - 42f, inRect.width * 0.48f, 38f),
                    validationFailure);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            DoBottomButtons(inRect, "Continue");
        }

        protected override bool CanDoNext()
        {
            if (!base.CanDoNext()) return false;
            CAPlayerFoundingModel.CaptureNativeIdeo(draft,
                CAPlayerFoundingModel.NativeIdeo, null);
            if (!CAPlayerFoundingModel.TryValidate(draft,
                    out validationFailure))
            {
                Messages.Message(validationFailure,
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }

        protected override void DoNext()
        {
            if (ModsConfig.IdeologyActive && !draft.nativeIdeoNotified)
            {
                Find.Scenario?.PostIdeoChosen();
                draft.nativeIdeoNotified = true;
            }
            CAPlayerFoundingModel.CaptureNativeIdeo(draft,
                CAPlayerFoundingModel.NativeIdeo, true);
            CAPlayerFoundingSession.Confirm(draft);
            CAPlayerFoundingModel.ApplyCarriedState(draft,
                Faction.OfPlayer);
            base.DoNext();
        }

        protected override void DoBack()
        {
            draft.confirmed = false;
            CAPlayerFoundingSession.Save();
            base.DoBack();
        }

        private void DrawCultureCard(Rect rect)
        {
            float y = BeginCard(rect, "Culture",
                "Shared styles and customs. Culture sets the settlement's "
                + "visual tradition and gathering place; it does not set "
                + "religious or political belief.");
            DrawSummary(rect, ref y, CACultureModel.Icon(draft?.culture),
                draft?.culture?.name ?? "Culture not set",
                CACultureModel.Summary(draft?.culture));
            DrawButtons(rect, ref y,
                new CAFoundingAction("Culture presets...",
                    OpenCulturePresets),
                new CAFoundingAction("Generate remaining", delegate
                {
                    CACultureModel.EnsureGenerated(draft.culture,
                        CAPlayerFoundingModel.Seed + ":culture-fill",
                        CAPlayerFoundingModel.PlayerCultureDef());
                    Changed();
                }),
                new CAFoundingAction("Edit", delegate
                {
                    Find.WindowStack.Add(new Dialog_CACultureEditor(
                        draft.culture, "Founders", Changed));
                }));
        }

        private void DrawIdeoCard(Rect rect)
        {
            string description = ModsConfig.IdeologyActive
                ? "Religious and moral belief. This is RimWorld's native "
                    + "Ideoligion and remains distinct from culture and "
                    + "political belief."
                : "The Ideology expansion is inactive. Culture, political "
                    + "beliefs, and the founding arrangement remain active.";
            float y = BeginCard(rect, "Ideoligion", description);
            Ideo ideo = CAPlayerFoundingModel.NativeIdeo;
            string detail = ideo == null ? "Not set"
                : (ideo.Fluid ? "Fluid Ideoligion" : "Fixed Ideoligion")
                    + " | " + ideo.memes.Count + " meme"
                    + (ideo.memes.Count == 1 ? "" : "s");
            DrawSummary(rect, ref y, ideo?.Icon, ideo?.name ?? "Not active",
                detail);
            if (!ModsConfig.IdeologyActive) return;
            DrawButtons(rect, ref y,
                new CAFoundingAction("Ideoligion presets...",
                    OpenIdeoPresets),
                new CAFoundingAction("Load...", LoadIdeo),
                new CAFoundingAction("Customize...", OpenIdeoEditorMenu));
        }

        private void DrawPoliticalCard(Rect rect)
        {
            float y = BeginCard(rect, "Political beliefs",
                "What the founders consider proper: authority, decisions, "
                + "ownership, work, support, membership, and conduct. These "
                + "beliefs do not silently become institutions.");
            DrawSummary(rect, ref y,
                CAPoliticalBeliefsModel.Icon(draft?.politicalBeliefs),
                draft?.politicalBeliefs?.presetName
                    ?? "Custom political beliefs",
                CAPoliticalBeliefsModel.Summary(draft?.politicalBeliefs));
            DrawButtons(rect, ref y,
                new CAFoundingAction("Belief presets...",
                    OpenPoliticalPresets),
                new CAFoundingAction("Generate remaining", delegate
                {
                    CAPoliticalBeliefsModel.GenerateUnset(
                        draft.politicalBeliefs,
                        CAPlayerFoundingModel.Seed + ":politics-fill");
                    RefreshSuggestedArrangement();
                    Changed();
                }),
                new CAFoundingAction("Edit", delegate
                {
                    Find.WindowStack.Add(Dialog_CAAxisEditor.ForBeliefs(
                        draft.politicalBeliefs,
                        CAPlayerFoundingModel.Seed + ":politics-edit",
                        delegate
                        {
                            RefreshSuggestedArrangement();
                            Changed();
                        }));
                }));
        }

        private void DrawArrangementCard(Rect rect)
        {
            float y = BeginCard(rect, "Founding arrangement",
                "The rules adopted at landing. Agreement or disagreement "
                + "with political belief is saved as meaningful state. Only "
                + "this initial order is established before play.");
            CAFoundingArrangement arrangement = draft?.arrangement;
            string title = arrangement?.label?.CapitalizeFirst()
                ?? "Arrangement not set";
            string detail = arrangement?.premise
                ?? "Choose or generate an arrangement.";
            DrawSummary(rect, ref y,
                ContentFinder<Texture2D>.Get(
                    "Rimshare/WorldMapIcons/handshake"),
                title + " | "
                    + CAPlayerFoundingModel.ArrangementSourceWords(draft),
                detail);
            DrawComparison(rect, ref y, "Authority",
                Belief(CAFactionAxes.Leadership), arrangement == null
                    ? null : arrangement.leaderRule == "none"
                        ? "no permanent leader" : "chosen leader");
            DrawComparison(rect, ref y, "Labour",
                Belief(CAFactionAxes.Work), arrangement == null ? null
                    : arrangement.workRequired ? "required work"
                        : "voluntary work");
            DrawComparison(rect, ref y, "Voice",
                Belief(CAFactionAxes.Participation), arrangement == null
                    ? null : arrangement.foundersDecide
                        ? "all founders decide" : "leader decides");
            DrawComparison(rect, ref y, "Supplies",
                Belief(CAFactionAxes.Ownership), arrangement == null ? null
                    : arrangement.sharedSupplies ? "held in common"
                        : "held separately");
            DrawButtons(rect, ref y,
                new CAFoundingAction("Use suggestion", delegate
                {
                    CAPlayerFoundingModel.UseSuggestedArrangement(draft);
                    Changed();
                }),
                new CAFoundingAction("Arrangement presets...",
                    OpenArrangementPresets),
                new CAFoundingAction("Edit", delegate
                {
                    Find.WindowStack.Add(new Dialog_CAFoundingArrangementEditor(
                        draft, Changed));
                }));
        }

        private static float BeginCard(Rect rect, string title,
            string description)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(14f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 30f), title);
            Text.Font = GameFont.Small;
            float height = Text.CalcHeight(description, inner.width);
            Widgets.Label(new Rect(inner.x, inner.y + 34f, inner.width,
                height), description);
            return inner.y + 34f + height + 10f;
        }

        private static void DrawSummary(Rect card, ref float y,
            Texture2D icon, string title, string detail)
        {
            Rect inner = card.ContractedBy(14f);
            Rect summary = new Rect(inner.x, y, inner.width, 60f);
            Widgets.DrawLightHighlight(summary);
            if (icon != null)
                GUI.DrawTexture(new Rect(summary.x + 7f, summary.y + 7f,
                    46f, 46f), icon, ScaleMode.ScaleToFit);
            float textX = icon == null ? summary.x + 9f : summary.x + 62f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(textX, summary.y + 7f,
                summary.xMax - textX - 7f, 23f), title ?? "Not set");
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(textX, summary.y + 31f,
                summary.xMax - textX - 7f, 24f), detail ?? "Not set");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y = summary.yMax + 8f;
        }

        private static void DrawComparison(Rect card, ref float y,
            string label, string belief, string adopted)
        {
            Rect inner = card.ContractedBy(14f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, y + 2f, 74f, 20f), label);
            string words = (belief ?? "not set") + " -> "
                + (adopted ?? "not set");
            bool differs = !belief.NullOrEmpty() && !adopted.NullOrEmpty()
                && !Comparable(belief, adopted);
            GUI.color = differs ? ColorLibrary.Yellow : Color.white;
            Widgets.Label(new Rect(inner.x + 78f, y + 2f,
                inner.width - 78f, 20f), words);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 21f;
        }

        private static bool Comparable(string belief, string adopted)
        {
            if (belief == adopted) return true;
            string left = belief.ToLowerInvariant();
            string right = adopted.ToLowerInvariant();
            if ((left.Contains("all") || left.Contains("majority")
                    || left.Contains("consensus"))
                && right.Contains("all")) return true;
            if ((left.Contains("common") || left.Contains("communal")
                    || left.Contains("cooperative"))
                && right.Contains("common")) return true;
            if ((left.Contains("required") || left.Contains("duty"))
                && right.Contains("required")) return true;
            if ((left.Contains("single") || left.Contains("leader"))
                && right.Contains("leader")) return true;
            return false;
        }

        private static void DrawButtons(Rect card, ref float y,
            params CAFoundingAction[] actions)
        {
            if (actions == null || actions.Length == 0) return;
            Rect inner = card.ContractedBy(14f);
            float rowY = Mathf.Min(card.yMax - 46f, y + 5f);
            float gap = 6f;
            float width = (inner.width - gap * (actions.Length - 1))
                / actions.Length;
            for (int i = 0; i < actions.Length; i++)
                if (Widgets.ButtonText(new Rect(inner.x + i * (width + gap),
                        rowY, width, 32f), actions[i].Label))
                    actions[i].Action?.Invoke();
            y = rowY + 38f;
        }

        private string Belief(string axis)
        {
            return CAFactionAxes.OptionOf(draft?.politicalBeliefs?.positions,
                axis)?.Label;
        }

        private void Changed()
        {
            validationFailure = null;
            if (draft != null) draft.confirmed = false;
            CAPlayerFoundingSession.Save();
        }

        private void RefreshSuggestedArrangement()
        {
            if (draft?.ArrangementSource == CAAxisSource.Generated)
                CAPlayerFoundingModel.UseSuggestedArrangement(draft);
        }

        private void OpenCulturePresets()
        {
            var options = new List<FloatMenuOption>();
            foreach (CACulturePreset preset in CACultureModel.Presets)
            {
                CACulturePreset local = preset;
                options.Add(new FloatMenuOption(local.Name + ": "
                    + local.Description, delegate
                {
                    CACultureModel.ApplyPreset(draft.culture, local);
                    Changed();
                }, ContentFinder<Texture2D>.Get(local.IconPath), Color.white));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenPoliticalPresets()
        {
            var options = new List<FloatMenuOption>();
            foreach (CAFactionAxes.PoliticalPreset preset in
                CAFactionAxes.Presets)
            {
                CAFactionAxes.PoliticalPreset local = preset;
                options.Add(new FloatMenuOption(local.Name + ": "
                    + CAPoliticalBeliefsModel.DescribePreset(local), delegate
                {
                    CAPoliticalBeliefsModel.ApplyPreset(
                        draft.politicalBeliefs, local);
                    CAPoliticalBeliefsModel.GenerateUnset(
                        draft.politicalBeliefs,
                        CAPlayerFoundingModel.Seed + ":preset-fill");
                    RefreshSuggestedArrangement();
                    Changed();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenArrangementPresets()
        {
            var options = new List<FloatMenuOption>();
            foreach (CAFoundingArrangement arrangement in
                CAFoundingArrangements.All)
            {
                CAFoundingArrangement local = arrangement;
                options.Add(new FloatMenuOption(
                    local.label.CapitalizeFirst() + ": " + local.premise,
                    delegate
                    {
                        CAPlayerFoundingModel.ChooseArrangement(draft, local);
                        Changed();
                    }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenIdeoPresets()
        {
            var options = new List<FloatMenuOption>();
            foreach (IdeoPresetDef preset in DefDatabase<IdeoPresetDef>
                .AllDefsListForReading.OrderBy(item => item.categoryDef?.label)
                .ThenBy(item => item.label))
            {
                IdeoPresetDef local = preset;
                string description = local.description.NullOrEmpty()
                    ? local.LabelCap.ToString()
                    : local.LabelCap + ": " + local.description;
                options.Add(new FloatMenuOption(description, delegate
                {
                    AssignPreset(local);
                    Changed();
                }, local.Icon, Color.white));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void LoadIdeo()
        {
            Find.WindowStack.Add(new Dialog_IdeoList_Load(delegate(Ideo ideo)
            {
                CAPlayerFoundingModel.AssignNativeIdeo(draft, ideo);
                CAPlayerFoundingSession.Save();
            }));
        }

        private void OpenIdeoEditorMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Edit current Ideoligion",
                    CustomizeIdeo),
                new FloatMenuOption("Create fixed Ideoligion", delegate
                { CreateIdeoEditor(false); }),
                new FloatMenuOption("Create fluid Ideoligion", delegate
                { CreateIdeoEditor(true); })
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void CustomizeIdeo()
        {
            EnsureNativeIdeo();
            IdeoUIUtility.selected = CAPlayerFoundingModel.NativeIdeo;
            Page editor = CAPlayerFoundingModel.NativeIdeo?.Fluid == true
                ? (Page)new Page_CAConfigureFoundingFluidIdeo(draft)
                : new Page_CAConfigureFoundingIdeo(draft);
            editor.prev = this;
            editor.next = this;
            Find.WindowStack.Add(editor);
            Close();
        }

        private void CreateIdeoEditor(bool fluid)
        {
            foreach (Ideo item in Find.IdeoManager.IdeosListForReading)
                item.initialPlayerIdeo = false;
            Faction.OfPlayer.ideos.RemoveAll();
            Find.IdeoManager.RemoveUnusedStartingIdeos();
            Page_ConfigureIdeo editor = fluid
                ? (Page_ConfigureIdeo)new Page_CAConfigureFoundingFluidIdeo(
                    draft)
                : new Page_CAConfigureFoundingIdeo(draft);
            editor.SelectOrMakeNewIdeo();
            editor.ideo.Fluid = fluid;
            editor.prev = this;
            editor.next = this;
            Find.WindowStack.Add(editor);
            Close();
        }

        private void EnsureNativeIdeo()
        {
            if (!ModsConfig.IdeologyActive
                || CAPlayerFoundingModel.NativeIdeo != null) return;
            List<IdeoPresetDef> presets = DefDatabase<IdeoPresetDef>
                .AllDefsListForReading.OrderBy(item => item.defName).ToList();
            IdeoPresetDef chosen = null;
            if (presets.Count > 0)
            {
                int index = (int)(Math.Abs((long)GenText.StableStringHash(
                    CAPlayerFoundingModel.Seed + ":ideo")) % presets.Count);
                chosen = presets[index];
            }
            AssignPreset(chosen);
        }

        private static void EnsureEstablishedIdeos()
        {
            if (!ModsConfig.IdeologyActive || Find.FactionManager == null)
                return;
            foreach (Faction faction in Find.FactionManager
                .AllFactionsListForReading)
            {
                if (faction == null || faction == Faction.OfPlayer
                    || faction.ideos == null) continue;
                Ideo primary = faction.ideos.PrimaryIdeo;
                if (primary != null && !primary.memes.NullOrEmpty()) continue;
                FactionDef def = faction.def;
                if (def.fixedIdeo)
                {
                    faction.ideos.ChooseOrGenerateIdeo(
                        new IdeoGenerationParms(def, false, null, null,
                            def.forcedMemes, false, false, false, true,
                            def.ideoName, def.styles, def.deityPresets,
                            def.hiddenIdeo, def.ideoDescription,
                            def.requiredPreceptsOnly));
                }
                else
                    faction.ideos.ChooseOrGenerateIdeo(
                        new IdeoGenerationParms(def));
            }
        }

        private void AssignPreset(IdeoPresetDef preset)
        {
            Faction player = Faction.OfPlayer;
            if (player == null) return;
            int seed = GenText.StableStringHash(CAPlayerFoundingModel.Seed
                + ":ideo:" + (preset?.defName ?? "classic"));
            Rand.PushState(seed);
            try
            {
                Ideo ideo;
                if (preset == null)
                {
                    ideo = IdeoGenerator.GenerateClassicIdeo(
                        CAPlayerFoundingModel.PlayerCultureDef(),
                        new IdeoGenerationParms(player.def), false);
                }
                else
                {
                    var memes = preset.memes?.ToList()
                        ?? new List<MemeDef>();
                    if (!memes.Any(item => item.category
                            == MemeCategory.Structure))
                    {
                        MemeDef structure = DefDatabase<MemeDef>
                            .AllDefsListForReading
                            .Where(item => item.category
                                == MemeCategory.Structure
                                && IdeoUtility.IsMemeAllowedFor(item,
                                    player.def))
                            .OrderBy(item => item.defName).FirstOrDefault();
                        if (structure != null) memes.Add(structure);
                    }
                    ideo = IdeoGenerator.GenerateIdeo(
                        new IdeoGenerationParms(player.def, false, null,
                            null, memes, preset.classicPlus, true));
                }
                CAPlayerFoundingModel.AssignNativeIdeo(draft, ideo);
            }
            finally { Rand.PopState(); }
        }
    }

    internal readonly struct CAFoundingAction
    {
        internal readonly string Label;
        internal readonly Action Action;
        internal CAFoundingAction(string label, Action action)
        { Label = label; Action = action; }
    }

    // The native editor remains authoritative for Ideoligion. Returning from
    // it records that Scenario.PostIdeoChosen has already run, preventing a
    // second scenario notification when the full founding page is confirmed.
    internal sealed class Page_CAConfigureFoundingIdeo : Page_ConfigureIdeo
    {
        private readonly CAPlayerFoundingPlan draft;

        internal Page_CAConfigureFoundingIdeo(CAPlayerFoundingPlan draft)
        {
            this.draft = draft;
        }

        protected override bool CanDoNext()
        {
            bool accepted = base.CanDoNext();
            if (!accepted) return false;
            CAPlayerFoundingModel.CaptureNativeIdeo(draft, ideo, true);
            CAPlayerFoundingSession.Save();
            return true;
        }

        protected override void DoBack()
        {
            // Native editing is live even when this page is left through
            // Back. Refresh the content signature so a changed Ideoligion is
            // notified to the scenario from the founding page before play.
            CAPlayerFoundingModel.CaptureNativeIdeo(draft, ideo, null);
            CAPlayerFoundingSession.Save();
            base.DoBack();
        }
    }

    internal sealed class Page_CAConfigureFoundingFluidIdeo
        : Page_ConfigureFluidIdeo
    {
        private readonly CAPlayerFoundingPlan draft;

        internal Page_CAConfigureFoundingFluidIdeo(
            CAPlayerFoundingPlan draft)
        {
            this.draft = draft;
        }

        protected override bool CanDoNext()
        {
            bool accepted = base.CanDoNext();
            if (!accepted) return false;
            CAPlayerFoundingModel.CaptureNativeIdeo(draft, ideo, true);
            CAPlayerFoundingSession.Save();
            return true;
        }

        protected override void DoBack()
        {
            CAPlayerFoundingModel.CaptureNativeIdeo(draft, ideo, null);
            CAPlayerFoundingSession.Save();
            base.DoBack();
        }
    }

    internal sealed class Dialog_CAFoundingArrangementEditor : Window
    {
        private readonly CAPlayerFoundingPlan draft;
        private readonly Action changed;

        public override Vector2 InitialSize => new Vector2(760f, 520f);

        internal Dialog_CAFoundingArrangementEditor(
            CAPlayerFoundingPlan draft, Action changed)
        {
            this.draft = draft;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (draft.arrangement == null)
                CAPlayerFoundingModel.UseSuggestedArrangement(draft);
            CAFoundingArrangement value = draft.arrangement;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "Founding arrangement");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, inRect.width, 48f),
                "These are the rules instituted at landing. Political beliefs "
                + "describe what the founders consider proper; differences "
                + "between the two are retained.");
            float y = 98f;
            Choice(ref y, inRect.width, "Who may give binding orders?",
                value.leaderRule == "none" ? "No permanent leader"
                    : "One chosen leader",
                new FloatMenuOption("No permanent leader", delegate
                { value.leaderRule = "none"; Authored(); }),
                new FloatMenuOption("One chosen leader", delegate
                { value.leaderRule = "chosen"; Authored(); }));
            Choice(ref y, inRect.width,
                "May founders be ordered to work?",
                value.workRequired ? "Yes" : "No",
                BoolOption("No", delegate { value.workRequired = false; }),
                BoolOption("Yes", delegate { value.workRequired = true; }));
            Choice(ref y, inRect.width,
                "Do founders have a voice from day one?",
                value.foundersDecide ? "Yes" : "No",
                BoolOption("No", delegate { value.foundersDecide = false; }),
                BoolOption("Yes", delegate { value.foundersDecide = true; }));
            Choice(ref y, inRect.width,
                "Are starting supplies held in common?",
                value.sharedSupplies ? "Yes" : "No",
                BoolOption("No", delegate { value.sharedSupplies = false; }),
                BoolOption("Yes", delegate { value.sharedSupplies = true; }));
            Choice(ref y, inRect.width, "Duration",
                value.durationDays > 0
                    ? value.durationDays + " days" : "No fixed end",
                new FloatMenuOption("No fixed end", delegate
                { value.durationDays = -1; Authored(); }),
                new FloatMenuOption("30 days", delegate
                { value.durationDays = 30; Authored(); }),
                new FloatMenuOption("60 days", delegate
                { value.durationDays = 60; Authored(); }));

            Rect consequence = new Rect(0f, y + 10f, inRect.width, 82f);
            Widgets.DrawLightHighlight(consequence);
            Widgets.Label(consequence.ContractedBy(10f),
                value.Consequence(CAPlayerFoundingModel.StartingPawnCount()));
        }

        private FloatMenuOption BoolOption(string label, Action set)
        {
            return new FloatMenuOption(label, delegate
            {
                set();
                Authored();
            });
        }

        private static void Choice(ref float y, float width, string label,
            string value, params FloatMenuOption[] choices)
        {
            const float labelWidth = 360f;
            Widgets.Label(new Rect(0f, y + 5f, labelWidth - 12f, 28f),
                label);
            if (Widgets.ButtonText(new Rect(labelWidth, y,
                    width - labelWidth, 30f), value))
                Find.WindowStack.Add(new FloatMenu(choices.ToList()));
            y += 42f;
        }

        private void Authored()
        {
            CAPlayerFoundingModel.MarkArrangementAuthored(draft);
            changed?.Invoke();
        }
    }
}
