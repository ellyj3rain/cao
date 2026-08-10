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
        private Vector2 cardScroll;
        private Vector2 arrangementScroll;

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
            const string introduction = "The founders arrive with a culture, "
                + "an Ideoligion, and political beliefs. Choose the first "
                + "rules of the settlement separately; institutions develop "
                + "through play.";
            Widgets.Label(new Rect(0f, 38f, inRect.width, 42f), introduction);
            CACreationUI.DrawFlow(new Rect(0f, 82f, inRect.width, 24f), 3);

            float top = 114f;
            float bottom = 52f;
            float gap = 12f;
            Rect cardsOut = new Rect(0f, top, inRect.width,
                inRect.height - top - bottom);
            float gridHeight = Mathf.Max(cardsOut.height, 752f);
            bool needsScroll = gridHeight > cardsOut.height + 0.5f;
            float gridWidth = cardsOut.width - (needsScroll ? 18f : 0f);
            Rect cardsView = new Rect(0f, 0f, gridWidth, gridHeight);
            Widgets.BeginScrollView(cardsOut, ref cardScroll, cardsView);
            try
            {
                float cardWidth = (gridWidth - gap) / 2f;
                float cardHeight = (gridHeight - gap) / 2f;
                DrawCultureCard(new Rect(0f, 0f, cardWidth, cardHeight));
                DrawIdeoCard(new Rect(cardWidth + gap, 0f, cardWidth,
                    cardHeight));
                DrawPoliticalCard(new Rect(0f, cardHeight + gap,
                    cardWidth, cardHeight));
                DrawArrangementCard(new Rect(cardWidth + gap,
                    cardHeight + gap, cardWidth, cardHeight));
            }
            finally { Widgets.EndScrollView(); }

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
                "Shared styles, customs, and gathering places. Culture is "
                + "separate from Ideoligion and political belief.");
            DrawSummary(rect, ref y, CACultureModel.Icon(draft?.culture),
                draft?.culture?.name ?? "Culture not set",
                CACultureModel.Summary(draft?.culture), CultureStateWords());
            DrawButtons(rect, ref y,
                new CAFoundingAction("Presets...",
                    OpenCulturePresets),
                new CAFoundingAction("Generate missing", delegate
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
                    + "beliefs, and the founding terms remain active.";
            float y = BeginCard(rect, "Ideoligion", description);
            Ideo ideo = CAPlayerFoundingModel.NativeIdeo;
            string detail = ideo == null ? "Not set"
                : (ideo.Fluid ? "Fluid Ideoligion" : "Fixed Ideoligion")
                    + " | " + ideo.memes.Count + " meme"
                    + (ideo.memes.Count == 1 ? "" : "s");
            DrawSummary(rect, ref y, ideo?.Icon, ideo?.name ?? "Not active",
                detail, ModsConfig.IdeologyActive ? "RimWorld system"
                    : "Inactive");
            if (!ModsConfig.IdeologyActive) return;
            DrawButtons(rect, ref y,
                new CAFoundingAction("Presets...",
                    OpenIdeoPresets),
                new CAFoundingAction("Load saved...", LoadIdeo),
                new CAFoundingAction("Customize...", OpenIdeoEditorMenu));
        }

        private void DrawPoliticalCard(Rect rect)
        {
            float y = BeginCard(rect, "Political beliefs",
                "What the founders believe society should permit, require, "
                + "and protect. Belief does not automatically become law.");
            DrawSummary(rect, ref y,
                CAPoliticalBeliefsModel.Icon(draft?.politicalBeliefs),
                draft?.politicalBeliefs?.presetName
                    ?? "Custom political beliefs",
                CAPoliticalBeliefsModel.Summary(draft?.politicalBeliefs),
                PoliticalStateWords());
            DrawButtons(rect, ref y,
                new CAFoundingAction("Presets...",
                    OpenPoliticalPresets),
                new CAFoundingAction("Generate missing", delegate
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
            float y = BeginCard(rect, "Founding terms",
                "The rules put in force at landing. They may follow or "
                + "contradict the founders' political beliefs.");
            CAFoundingArrangement arrangement = draft?.arrangement;
            string title = arrangement?.label?.CapitalizeFirst()
                ?? "Arrangement not set";
            string detail = arrangement?.premise
                ?? "Choose or generate an arrangement.";
            DrawSummary(rect, ref y,
                ContentFinder<Texture2D>.Get(
                    "Rimshare/WorldMapIcons/divided-square"),
                title, detail, CAPlayerFoundingModel
                    .ArrangementSourceWords(draft));
            List<CAPoliticalBeliefPractice.CAFoundingBeliefReading> readings =
                CAPoliticalBeliefPractice.ReadAgainstPoliticalBeliefs(
                    draft?.politicalBeliefs, arrangement);
            Rect inner = rect.ContractedBy(14f);
            float comparisonBottom = rect.yMax - 54f;
            Rect comparisonOut = new Rect(inner.x, y, inner.width,
                Mathf.Max(44f, comparisonBottom - y));
            float comparisonWidth = Mathf.Max(120f, comparisonOut.width - 18f);
            float comparisonHeight = readings.Sum(reading =>
                ComparisonHeight(reading, comparisonWidth));
            Rect comparisonView = new Rect(0f, 0f, comparisonWidth,
                Mathf.Max(comparisonOut.height, comparisonHeight));
            Widgets.BeginScrollView(comparisonOut, ref arrangementScroll,
                comparisonView);
            try
            {
                float comparisonY = 0f;
                foreach (CAPoliticalBeliefPractice.CAFoundingBeliefReading
                    reading in readings)
                    DrawComparison(comparisonView, ref comparisonY, reading);
            }
            finally { Widgets.EndScrollView(); }
            y = rect.yMax - 51f;
            DrawButtons(rect, ref y,
                new CAFoundingAction("Use suggested", delegate
                {
                    CAPlayerFoundingModel.UseSuggestedArrangement(draft);
                    Changed();
                }),
                new CAFoundingAction("Presets...",
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
            Texture2D icon, string title, string detail, string badge)
        {
            Rect inner = card.ContractedBy(14f);
            Rect summary = new Rect(inner.x, y, inner.width, 72f);
            Widgets.DrawLightHighlight(summary);
            if (icon != null)
                GUI.DrawTexture(new Rect(summary.x + 8f, summary.y + 10f,
                    52f, 52f), icon, ScaleMode.ScaleToFit);
            float textX = icon == null ? summary.x + 9f : summary.x + 68f;
            Text.Font = GameFont.Small;
            float badgeWidth = badge.NullOrEmpty() ? 0f
                : Mathf.Min(108f, Text.CalcSize(badge).x + 16f);
            Widgets.Label(new Rect(textX, summary.y + 7f,
                summary.xMax - textX - badgeWidth - 12f, 23f),
                title ?? "Not set");
            if (!badge.NullOrEmpty())
                CACreationUI.DrawChip(new Rect(summary.xMax - badgeWidth - 6f,
                    summary.y + 6f, badgeWidth, 20f), badge,
                    StateColor(badge));
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(textX, summary.y + 31f,
                summary.xMax - textX - 7f, 36f), detail ?? "Not set");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y = summary.yMax + 8f;
        }

        private static float ComparisonHeight(
            CAPoliticalBeliefPractice.CAFoundingBeliefReading reading,
            float width)
        {
            const float labelWidth = 82f;
            const float stateWidth = 92f;
            float textWidth = Mathf.Max(80f,
                width - labelWidth - stateWidth - 10f);
            bool known = reading != null && !reading.Silent;
            string belief = "Belief: "
                + (known ? reading.belief : "belief not set");
            string adopted = "At landing: "
                + (reading?.adopted ?? "term not set");
            GameFont prior = Text.Font;
            Text.Font = GameFont.Tiny;
            float titleHeight = Text.CalcHeight(reading?.title ?? "Rule",
                labelWidth);
            float textHeight = Text.CalcHeight(belief, textWidth)
                + Text.CalcHeight(adopted, textWidth) + 2f;
            Text.Font = prior;
            return Mathf.Max(38f, Mathf.Max(titleHeight, textHeight) + 8f);
        }

        private static void DrawComparison(Rect area, ref float y,
            CAPoliticalBeliefPractice.CAFoundingBeliefReading reading)
        {
            const float labelWidth = 82f;
            const float stateWidth = 92f;
            float textX = area.x + labelWidth + 4f;
            float textWidth = Mathf.Max(80f,
                area.width - labelWidth - stateWidth - 10f);
            float rowHeight = ComparisonHeight(reading, area.width);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(area.x, y + 3f, labelWidth, rowHeight - 6f),
                reading?.title ?? "Rule");
            bool known = reading != null && !reading.Silent;
            string belief = known ? reading.belief : "belief not set";
            string adopted = reading?.adopted ?? "term not set";
            float beliefHeight = Text.CalcHeight("Belief: " + belief,
                textWidth);
            float adoptedHeight = Text.CalcHeight("At landing: " + adopted,
                textWidth);
            GUI.color = new Color(0.72f, 0.76f, 0.81f);
            Widgets.Label(new Rect(textX, y, textWidth, beliefHeight),
                "Belief: " + belief);
            GUI.color = Color.white;
            Widgets.Label(new Rect(textX, y + beliefHeight + 2f,
                textWidth, adoptedHeight), "At landing: " + adopted);
            string state = !known ? "Unsettled"
                : reading.conforms ? "Aligned" : "In tension";
            CACreationUI.DrawChip(new Rect(area.xMax - 88f,
                y + (rowHeight - 20f) * 0.5f, 88f, 20f), state,
                !known ? CACreationUI.Unset
                    : reading.conforms ? CACreationUI.Authored
                        : ColorLibrary.Yellow);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += rowHeight;
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

        private string CultureStateWords()
        {
            if ((draft?.culture?.authoredMask ?? 0) != 0) return "Edited";
            if (draft?.culture != null
                && !draft.culture.presetName.NullOrEmpty())
                return "Preset";
            return "Generated";
        }

        private string PoliticalStateWords()
        {
            List<CAAxisEntry> positions = draft?.politicalBeliefs?.positions;
            if (positions?.Any(item => item != null && item.source
                    == (byte)CAAxisSource.Authored) == true) return "Edited";
            if (positions?.Any(item => item != null && item.source
                    == (byte)CAAxisSource.Preset) == true) return "Preset";
            return "Generated";
        }

        private static Color StateColor(string words)
        {
            if (words == "Preset") return CACreationUI.Preset;
            if (words == "Generated") return CACreationUI.Generated;
            if (words == "Inactive" || words == "Not set")
                return CACreationUI.Unset;
            return CACreationUI.Authored;
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
            var options = new List<CACreationChoice>();
            foreach (CACulturePreset preset in CACultureModel.Presets)
            {
                CACulturePreset local = preset;
                options.Add(new CACreationChoice
                {
                    Key = local.Name,
                    Name = local.Name,
                    Summary = local.Description,
                    Traits = CACultureModel.Words("gathering",
                        local.Gathering),
                    Details = "Sets the founders' gathering custom. Native "
                        + "style categories remain editable separately.",
                    Badge = "Culture preset",
                    Icon = ContentFinder<Texture2D>.Get(local.IconPath),
                    Accent = CACreationUI.Preset,
                    Selected = CACultureModel.UsesPreset(draft?.culture,
                        local),
                    ConfirmLabel = "Use this culture",
                    Choose = delegate
                    {
                        CACultureModel.ApplyPreset(draft.culture, local);
                        Changed();
                    }
                });
            }
            CACreationUI.OpenChoices("Culture presets",
                "Choose a starting set of customs. Every field remains "
                + "editable after the preset is applied.", options);
        }

        private void OpenPoliticalPresets()
        {
            var options = new List<CACreationChoice>();
            foreach (CAFactionAxes.PoliticalPreset preset in
                CAFactionAxes.Presets)
            {
                CAFactionAxes.PoliticalPreset local = preset;
                options.Add(new CACreationChoice
                {
                    Key = local.Name,
                    Name = local.Name,
                    Summary = PoliticalPresetIdentity(local),
                    Traits = CAPoliticalBeliefsModel.PresetTraits(local, 3),
                    Details = CAPoliticalBeliefsModel.PresetDetails(local),
                    Badge = "Political preset",
                    Icon = CAPoliticalBeliefsModel.Icon(local),
                    Accent = CACreationUI.Preset,
                    Selected = CAPoliticalBeliefsModel.UsesPreset(
                        draft?.politicalBeliefs, local),
                    ConfirmLabel = "Use these beliefs",
                    Choose = delegate
                    {
                        CAPoliticalBeliefsModel.ChoosePreset(
                            draft.politicalBeliefs, local,
                            CAPlayerFoundingModel.Seed + ":preset:"
                                + local.Name);
                        RefreshSuggestedArrangement();
                        Changed();
                    }
                });
            }
            CACreationUI.OpenChoices("Political-belief presets",
                "Choose what the founders broadly consider proper. The "
                + "individual positions remain editable, and these beliefs "
                + "do not automatically become settlement rules.", options);
        }

        private void OpenArrangementPresets()
        {
            var options = new List<CACreationChoice>();
            foreach (CAFoundingArrangement arrangement in
                CAFoundingArrangements.All)
            {
                CAFoundingArrangement local = arrangement;
                options.Add(new CACreationChoice
                {
                    Key = local.id,
                    Name = local.label.CapitalizeFirst(),
                    Summary = local.premise,
                    Traits = FoundingTermsTraits(local),
                    Details = local.Consequence(
                        CAPlayerFoundingModel.StartingPawnCount()),
                    Badge = "Starting rules",
                    Icon = FoundingTermsIcon(local),
                    Accent = CACreationUI.Preset,
                    Selected = draft?.arrangement?.id == local.id,
                    ConfirmLabel = "Use these terms",
                    Choose = delegate
                    {
                        CAPlayerFoundingModel.ChooseArrangement(draft, local);
                        Changed();
                    }
                });
            }
            CACreationUI.OpenChoices("Founding terms",
                "Choose the rules put in force when the founders land. "
                + "Agreement or tension with their political beliefs is "
                + "retained as part of the colony's state.", options);
        }

        private void OpenIdeoPresets()
        {
            var options = new List<CACreationChoice>();
            foreach (IdeoPresetDef preset in DefDatabase<IdeoPresetDef>
                .AllDefsListForReading.OrderBy(item => item.categoryDef?.label)
                .ThenBy(item => item.label))
            {
                IdeoPresetDef local = preset;
                string traits = string.Join(" · ", local.memes
                    .Take(3).Select(item => item.LabelCap.ToString())
                    .ToArray());
                options.Add(new CACreationChoice
                {
                    Key = local.defName,
                    Name = local.LabelCap.ToString(),
                    Summary = local.description.NullOrEmpty()
                        ? "A native RimWorld Ideoligion preset."
                        : local.description,
                    Traits = traits,
                    Details = local.memes.Count == 0 ? null
                        : "Memes\n" + string.Join("\n", local.memes
                            .Select(item => "- " + item.LabelCap)
                            .ToArray()),
                    Group = local.categoryDef?.LabelCap.ToString(),
                    Badge = "Ideoligion preset",
                    Icon = local.Icon,
                    Accent = CACreationUI.Preset,
                    Selected = IdeoMatches(local),
                    ConfirmLabel = "Use this Ideoligion",
                    Choose = delegate
                    {
                        AssignPreset(local);
                        Changed();
                    }
                });
            }
            CACreationUI.OpenChoices("Ideoligion presets",
                "Choose a native RimWorld Ideoligion as a starting point. "
                + "Its memes, precepts, roles, and rituals remain available "
                + "through the native editor.", options);
        }

        private static string PoliticalPresetIdentity(
            CAFactionAxes.PoliticalPreset preset)
        {
            var temporary = new CAPoliticalBeliefs();
            CAPoliticalBeliefsModel.ApplyPreset(temporary, preset);
            string leadership = CAFactionAxes.OptionOf(temporary.positions,
                CAFactionAxes.Leadership)?.Label;
            string decisions = CAFactionAxes.OptionOf(temporary.positions,
                CAFactionAxes.Decisions)?.Label;
            if (!leadership.NullOrEmpty() && !decisions.NullOrEmpty())
                return leadership.CapitalizeFirst() + "; " + decisions + ".";
            return "A coherent starting set of political positions.";
        }

        private static string FoundingTermsTraits(
            CAFoundingArrangement terms)
        {
            if (terms == null) return "Not set";
            return (terms.leaderRule == "none" ? "No permanent leader"
                    : "Chosen leader") + " · "
                + (terms.workRequired ? "Required work" : "Voluntary work")
                + " · " + (terms.sharedSupplies
                    ? "Shared supplies" : "Separate supplies");
        }

        private static Texture2D FoundingTermsIcon(
            CAFoundingArrangement terms)
        {
            string path = terms?.id == "emergency-command"
                ? "Rimshare/WorldMapIcons/crenulated-shield"
                : terms?.id == "single-founder"
                    ? "Rimshare/WorldMapIcons/corporal"
                    : terms?.id == "established-settlement"
                        ? "Rimshare/WorldMapIcons/castle"
                        : "Rimshare/WorldMapIcons/divided-square";
            return CACreationUI.Icon(path);
        }

        private static bool IdeoMatches(IdeoPresetDef preset)
        {
            Ideo current = CAPlayerFoundingModel.NativeIdeo;
            if (current == null || preset == null) return false;
            return current.memes.Count == preset.memes.Count
                && preset.memes.All(current.memes.Contains);
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
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "edit",
                    Name = "Edit current Ideoligion",
                    Summary = "Open RimWorld's native editor for the current belief.",
                    Traits = CAPlayerFoundingModel.NativeIdeo?.name
                        ?? "Current Ideoligion",
                    Badge = "Native editor",
                    Icon = CAPlayerFoundingModel.NativeIdeo?.Icon,
                    Accent = CACreationUI.Authored,
                    ConfirmLabel = "Open editor",
                    Choose = CustomizeIdeo
                },
                new CACreationChoice
                {
                    Key = "fixed",
                    Name = "Create fixed Ideoligion",
                    Summary = "Create a complete Ideoligion before play begins.",
                    Traits = "Fixed memes and precepts",
                    Badge = "New Ideoligion",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/cog"),
                    Accent = CACreationUI.Preset,
                    ConfirmLabel = "Create fixed Ideoligion",
                    Choose = delegate { CreateIdeoEditor(false); }
                },
                new CACreationChoice
                {
                    Key = "fluid",
                    Name = "Create fluid Ideoligion",
                    Summary = "Begin simply and develop the Ideoligion through play.",
                    Traits = "Fluid development",
                    Badge = "New Ideoligion",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/forward-sun"),
                    Accent = CACreationUI.Preset,
                    ConfirmLabel = "Create fluid Ideoligion",
                    Choose = delegate { CreateIdeoEditor(true); }
                }
            };
            CACreationUI.OpenChoices("Customize Ideoligion",
                "Use RimWorld's native Ideology editor. Returning from the "
                + "editor restores the Founding society page and preserves "
                + "the culture, political beliefs, and founding terms.",
                options);
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

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(820f, UI.screenWidth - 48f),
            Mathf.Min(570f, UI.screenHeight - 48f));

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
                "Founding terms");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, inRect.width, 48f),
                "These rules take effect at landing. Political beliefs state "
                + "what the founders consider proper; agreement or tension "
                + "between belief and practice is retained.");
            float y = 98f;
            Choice(ref y, inRect.width, "Settlement leadership",
                "Who may give binding orders.",
                new[] { "Founders decide", "Chosen leader" },
                value.leaderRule == "none" ? 0 : 1, index =>
                {
                    value.leaderRule = index == 0 ? "none" : "chosen";
                    Authored();
                });
            Choice(ref y, inRect.width, "Required work",
                "Whether work and defense may be assigned.",
                new[] { "Voluntary", "May be assigned" },
                value.workRequired ? 1 : 0, index =>
                {
                    value.workRequired = index == 1;
                    Authored();
                });
            Choice(ref y, inRect.width, "First decisions",
                "Who votes on decisions from the first day.",
                new[] { "No founder vote", "All founders vote" },
                value.foundersDecide ? 1 : 0, index =>
                {
                    value.foundersDecide = index == 1;
                    Authored();
                });
            Choice(ref y, inRect.width, "Starting supplies",
                "Whether provisions are pooled and rationed.",
                new[] { "Held separately", "Held in common" },
                value.sharedSupplies ? 1 : 0, index =>
                {
                    value.sharedSupplies = index == 1;
                    Authored();
                });
            int duration = value.durationDays == 30 ? 1
                : value.durationDays == 60 ? 2 : 0;
            Choice(ref y, inRect.width, "Term",
                "When these starting rules expire automatically.",
                new[] { "No fixed end", "30 days", "60 days" },
                duration, index =>
                {
                    value.durationDays = index == 1 ? 30
                        : index == 2 ? 60 : -1;
                    Authored();
                });

            Rect consequence = new Rect(0f, y + 10f, inRect.width, 82f);
            Widgets.DrawLightHighlight(consequence);
            Widgets.Label(consequence.ContractedBy(10f),
                value.Consequence(CAPlayerFoundingModel.StartingPawnCount()));
        }

        private static void Choice(ref float y, float width, string label,
            string explanation, string[] choices, int selected,
            Action<int> choose)
        {
            const float labelWidth = 290f;
            Widgets.Label(new Rect(0f, y + 1f, labelWidth - 12f, 22f),
                label);
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(0f, y + 23f, labelWidth - 12f, 22f),
                explanation);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            CACreationUI.DrawSegment(new Rect(labelWidth, y + 4f,
                width - labelWidth, 34f), choices, selected, choose);
            y += 50f;
        }

        private void Authored()
        {
            CAPlayerFoundingModel.MarkArrangementAuthored(draft);
            changed?.Invoke();
        }
    }
}
