using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Keeps RimWorld's native Ideoligion chooser in the scenario chain and
    // inserts CA's starting-arrangements page after it. The native page owns
    // preset discovery, fixed/fluid creation, load, memes, precepts, roles,
    // rituals, and Scenario.PostIdeoChosen; CA owns the adjacent founding state.
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
                    Page following = current.next;
                    var inserted = new Page_CAPlayerFounding(current,
                        following);
                    inserted.nextAct = current.nextAct;
                    current.nextAct = null;
                    current.next = inserted;
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
        private bool openedOnce;
        private bool awaitingNativeReturn;
        private readonly Page nativeIdeoChooser;

        public override string PageTitle =>
            CAPlayerFoundingModel.StartingContextTitle();

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
            nativeIdeoChooser = previous is Page_ChooseIdeoPreset
                ? previous : null;
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
            bool nativeFlowJustAccepted = ModsConfig.IdeologyActive
                && (!openedOnce || awaitingNativeReturn)
                && (nativeIdeoChooser != null
                    || prev is Page_ConfigureIdeo);
            openedOnce = true;
            awaitingNativeReturn = false;
            draft = CAPlayerFoundingSession.Current;
            EnsureNativeIdeo();
            CAPlayerFoundingModel.Ensure(draft);
            CAPlayerFoundingModel.CaptureNativeIdeo(draft,
                CAPlayerFoundingModel.NativeIdeo,
                nativeFlowJustAccepted ? (bool?)true : null);
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
            string introduction = CAPlayerFoundingModel.StartingContextSummary();
            float introductionHeight = Text.CalcHeight(introduction,
                inRect.width - 176f);
            Widgets.Label(new Rect(0f, 38f, inRect.width - 176f,
                introductionHeight), introduction);
            float flowY = 44f + introductionHeight;
            CACreationUI.DrawFlow(new Rect(0f, flowY, inRect.width, 24f), 3);
            float overviewY = flowY + 30f;
            string overview = FoundingOverview();
            float overviewHeight = Text.CalcHeight(overview, inRect.width);
            Widgets.DrawLightHighlight(new Rect(0f, overviewY, inRect.width,
                overviewHeight + 12f));
            Widgets.Label(new Rect(8f, overviewY + 6f, inRect.width - 16f,
                overviewHeight), overview);

            float top = overviewY + overviewHeight + 20f;
            float validationWidth = inRect.width * 0.48f;
            float validationHeight = validationFailure.NullOrEmpty() ? 0f
                : Text.CalcHeight(validationFailure, validationWidth);
            float bottom = Mathf.Max(52f, validationHeight + 12f);
            float gap = 12f;
            Rect cardsOut = new Rect(0f, top, inRect.width,
                inRect.height - top - bottom);
            float gridWidth = Mathf.Max(240f, cardsOut.width - 18f);
            bool twoColumns = gridWidth >= 900f;
            float cardWidth = twoColumns ? (gridWidth - gap) / 2f
                : gridWidth;
            float cultureHeight = MeasureCultureCard(cardWidth);
            float ideoHeight = MeasureIdeoCard(cardWidth);
            float politicalHeight = MeasurePoliticalCard(cardWidth);
            float arrangementHeight = MeasureArrangementCard(cardWidth);
            float gridHeight = twoColumns
                ? Mathf.Max(cultureHeight, ideoHeight) + gap
                    + Mathf.Max(politicalHeight, arrangementHeight)
                : cultureHeight + ideoHeight + politicalHeight
                    + arrangementHeight + gap * 3f;
            Rect cardsView = new Rect(0f, 0f, gridWidth, gridHeight);
            Widgets.BeginScrollView(cardsOut, ref cardScroll, cardsView);
            try
            {
                if (twoColumns)
                {
                    float firstRow = Mathf.Max(cultureHeight, ideoHeight);
                    float secondRow = Mathf.Max(politicalHeight,
                        arrangementHeight);
                    DrawCultureCard(new Rect(0f, 0f, cardWidth, firstRow));
                    DrawIdeoCard(new Rect(cardWidth + gap, 0f, cardWidth,
                        firstRow));
                    DrawPoliticalCard(new Rect(0f, firstRow + gap,
                        cardWidth, secondRow));
                    DrawArrangementCard(new Rect(cardWidth + gap,
                        firstRow + gap, cardWidth, secondRow));
                }
                else
                {
                    float cardY = 0f;
                    DrawCultureCard(new Rect(0f, cardY, gridWidth,
                        cultureHeight));
                    cardY += cultureHeight + gap;
                    DrawIdeoCard(new Rect(0f, cardY, gridWidth, ideoHeight));
                    cardY += ideoHeight + gap;
                    DrawPoliticalCard(new Rect(0f, cardY, gridWidth,
                        politicalHeight));
                    cardY += politicalHeight + gap;
                    DrawArrangementCard(new Rect(0f, cardY, gridWidth,
                        arrangementHeight));
                }
            }
            finally { Widgets.EndScrollView(); }

            if (!validationFailure.NullOrEmpty())
            {
                GUI.color = ColorLibrary.RedReadable;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(inRect.width * 0.35f,
                    inRect.height - validationHeight - 6f,
                    validationWidth, validationHeight),
                    validationFailure);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            DoBottomButtons(inRect, "Continue");
        }

        private string FoundingOverview()
        {
            string compact = "Culture: "
                + CAAuthoringChoices.CultureIdentity(draft?.culture)
                + " · Ideoligion: "
                + (CAPlayerFoundingModel.NativeIdeo?.name
                    ?? (ModsConfig.IdeologyActive ? "not set" : "inactive"));
            string political = CAAuthoringChoices.PoliticalIdentity(
                draft?.politicalBeliefs);
            string standard = compact + " · Political beliefs: " + political
                + " · Landing rules: "
                + (draft?.arrangement?.label ?? "not set");
            int tensions = CAPoliticalBeliefPractice
                .ReadAgainstPoliticalBeliefs(draft?.politicalBeliefs,
                    draft?.arrangement)
                .Count(item => item != null && !item.Silent
                    && !item.conforms);
            string expanded = standard + " · " + tensions
                + (tensions == 1 ? " belief-term tension" : " belief-term tensions");
            return expanded;
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
                CultureDescription());
            DrawSummary(rect, ref y, null,
                draft?.culture?.name ?? "Culture not set",
                CACultureModel.Summary(draft?.culture), CultureStateWords());
            DrawButtons(rect, ref y,
                new CAFoundingAction("Compose Culture", delegate
                {
                    Find.WindowStack.Add(new Dialog_CACultureEditor(
                        draft.culture, "Founders", Changed));
                }),
                new CAFoundingAction("Load saved...", OpenCulturePresets),
                new CAFoundingAction("Save Culture...", SaveCultureProfile));
        }

        private void DrawIdeoCard(Rect rect)
        {
            float y = BeginCard(rect, "Ideoligion", IdeoDescription());
            Ideo ideo = CAPlayerFoundingModel.NativeIdeo;
            string detail = IdeoDetail(ideo);
            DrawSummary(rect, ref y, ideo?.Icon, ideo?.name ?? "Not active",
                detail, !ModsConfig.IdeologyActive ? "Inactive"
                    : ideo == null ? "Not set" : "Chosen");
            if (!ModsConfig.IdeologyActive) return;
            DrawButtons(rect, ref y,
                new CAFoundingAction("Choose again...",
                    ReturnToNativeIdeo),
                new CAFoundingAction("Load saved...", LoadIdeo),
                new CAFoundingAction("Edit current...", CustomizeIdeo));
        }

        private void DrawPoliticalCard(Rect rect)
        {
            float y = BeginCard(rect, "Political beliefs",
                PoliticalDescription());
            DrawSummary(rect, ref y, null,
                CAAuthoringChoices.PoliticalIdentity(
                    draft?.politicalBeliefs),
                CAPoliticalBeliefsModel.Summary(draft?.politicalBeliefs),
                PoliticalStateWords());
            DrawButtons(rect, ref y,
                new CAFoundingAction("Set beliefs", delegate
                {
                    Find.WindowStack.Add(Dialog_CAAxisEditor.ForBeliefs(
                        draft.politicalBeliefs,
                        CAPlayerFoundingModel.Seed + ":politics-edit",
                        draft.arrangement,
                        delegate
                        {
                            RefreshSuggestedArrangement();
                            Changed();
                        }));
                }),
                new CAFoundingAction("Profiles...", OpenPoliticalPresets));
        }

        private void DrawArrangementCard(Rect rect)
        {
            float y = BeginCard(rect, "Rules at landing",
                ArrangementDescription());
            CAFoundingArrangement arrangement = draft?.arrangement;
            string title = arrangement?.label?.CapitalizeFirst()
                ?? "Arrangement not set";
            string detail = arrangement?.premise
                ?? "Choose or generate an arrangement.";
            DrawSummary(rect, ref y,
                null, title, detail, CAPlayerFoundingModel
                    .ArrangementSourceWords(draft));
            List<CAPoliticalBeliefPractice.CAFoundingBeliefReading> readings =
                CAPoliticalBeliefPractice.ReadAgainstPoliticalBeliefs(
                    draft?.politicalBeliefs, arrangement);
            Rect inner = rect.ContractedBy(14f);
            Rect comparisonArea = new Rect(inner.x, 0f, inner.width,
                rect.height);
            foreach (CAPoliticalBeliefPractice.CAFoundingBeliefReading
                reading in readings)
                DrawComparison(comparisonArea, ref y, reading);
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

        private string CultureDescription()
        {
            return "The founders bring an inherited culture. Ideoligion and "
                + "political beliefs remain separate; local practice develops "
                + "from the population, adopted rules, material conditions, "
                + "and history.";
        }

        private static string IdeoDescription()
        {
            return ModsConfig.IdeologyActive
                ? "Religious and moral belief. This is RimWorld's native "
                    + "Ideoligion and remains distinct from culture and "
                    + "political belief."
                : "The Ideology expansion is inactive. Culture, political "
                    + "beliefs, and the founding terms remain active.";
        }

        private static string IdeoDetail(Ideo ideo)
        {
            return ideo == null ? "Not set"
                : (ideo.Fluid ? "Fluid Ideoligion" : "Fixed Ideoligion")
                    + " | " + ideo.memes.Count + " meme"
                    + (ideo.memes.Count == 1 ? "" : "s");
        }

        private static string PoliticalDescription()
        {
            return "What the founders believe society should permit, "
                + "require, and protect. Belief does not automatically "
                + "become law.";
        }

        private static string ArrangementDescription()
        {
            return "The rules put in force at landing. They may follow or "
                + "contradict the founders' political beliefs.";
        }

        private float MeasureCultureCard(float width)
        {
            return MeasureCard(width, CultureDescription(),
                draft?.culture?.name ?? "Culture not set",
                CACultureModel.Summary(draft?.culture), CultureStateWords());
        }

        private float MeasureIdeoCard(float width)
        {
            Ideo ideo = CAPlayerFoundingModel.NativeIdeo;
            return MeasureCard(width, IdeoDescription(),
                ideo?.name ?? "Not active", IdeoDetail(ideo),
                !ModsConfig.IdeologyActive ? "Inactive"
                    : ideo == null ? "Not set" : "Chosen");
        }

        private float MeasurePoliticalCard(float width)
        {
            return MeasureCard(width, PoliticalDescription(),
                CAAuthoringChoices.PoliticalIdentity(
                    draft?.politicalBeliefs),
                CAPoliticalBeliefsModel.Summary(draft?.politicalBeliefs),
                PoliticalStateWords());
        }

        private float MeasureArrangementCard(float width)
        {
            CAFoundingArrangement arrangement = draft?.arrangement;
            float innerWidth = Mathf.Max(120f, width - 28f);
            float comparisons = CAPoliticalBeliefPractice
                .ReadAgainstPoliticalBeliefs(draft?.politicalBeliefs,
                    arrangement)
                .Sum(reading => ComparisonHeight(reading, innerWidth));
            return MeasureCard(width, ArrangementDescription(),
                arrangement?.label?.CapitalizeFirst()
                    ?? "Arrangement not set",
                arrangement?.premise ?? "Choose or generate an arrangement.",
                CAPlayerFoundingModel.ArrangementSourceWords(draft),
                comparisons);
        }

        private static float MeasureCard(float width, string description,
            string title, string detail, string badge, float extra = 0f)
        {
            float innerWidth = Mathf.Max(120f, width - 28f);
            GameFont prior = Text.Font;
            Text.Font = GameFont.Small;
            float descriptionHeight = Text.CalcHeight(description,
                innerWidth);
            Text.Font = prior;
            return 14f + 34f + descriptionHeight + 10f
                + SummaryHeight(innerWidth, title, detail, badge)
                + 8f + extra + 51f;
        }

        private static float SummaryHeight(float innerWidth, string title,
            string detail, string badge)
        {
            float textX = 68f;
            GameFont prior = Text.Font;
            Text.Font = GameFont.Small;
            float badgeWidth = badge.NullOrEmpty() ? 0f
                : Mathf.Min(108f, Text.CalcSize(badge).x + 16f);
            float textWidth = Mathf.Max(90f,
                innerWidth - textX - badgeWidth - 12f);
            float titleHeight = Text.CalcHeight(title ?? "Not set", textWidth);
            Text.Font = GameFont.Tiny;
            float detailHeight = Text.CalcHeight(detail ?? "Not set",
                innerWidth - textX - 7f);
            Text.Font = prior;
            return Mathf.Max(72f,
                14f + titleHeight + detailHeight + 8f);
        }

        private static void DrawSummary(Rect card, ref float y,
            Texture2D icon, string title, string detail, string badge)
        {
            Rect inner = card.ContractedBy(14f);
            float textX = inner.x + (icon == null ? 9f : 68f);
            Text.Font = GameFont.Small;
            float badgeWidth = badge.NullOrEmpty() ? 0f
                : Mathf.Min(108f, Text.CalcSize(badge).x + 16f);
            float textWidth = Mathf.Max(90f,
                inner.xMax - textX - badgeWidth - 12f);
            float titleHeight = Text.CalcHeight(title ?? "Not set", textWidth);
            Text.Font = GameFont.Tiny;
            float detailHeight = Text.CalcHeight(detail ?? "Not set",
                inner.xMax - textX - 7f);
            Text.Font = GameFont.Small;
            float summaryHeight = SummaryHeight(inner.width, title, detail,
                badge);
            Rect summary = new Rect(inner.x, y, inner.width, summaryHeight);
            Widgets.DrawLightHighlight(summary);
            if (icon != null)
                GUI.DrawTexture(new Rect(summary.x + 8f, summary.y + 10f,
                    52f, 52f), icon, ScaleMode.ScaleToFit);
            Widgets.Label(new Rect(textX, summary.y + 7f, textWidth,
                titleHeight), title ?? "Not set");
            if (!badge.NullOrEmpty())
                CACreationUI.DrawChip(new Rect(summary.xMax - badgeWidth - 6f,
                    summary.y + 6f, badgeWidth, 20f), badge,
                    StateColor(badge));
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(textX, summary.y + 11f + titleHeight,
                summary.xMax - textX - 7f, detailHeight), detail ?? "Not set");
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
            return "Inherited";
        }

        private string PoliticalStateWords()
        {
            List<CAAxisEntry> positions = draft?.politicalBeliefs?.positions;
            if (positions?.Any(item => item != null && item.source
                    == (byte)CAAxisSource.Authored) == true) return "Edited";
            return positions?.Count == CAFactionAxes.Axes.Length
                && CAFactionAxes.CountByState(positions,
                    CAAxisSource.Unset) == 0 ? "Set" : "Incomplete";
        }

        private static Color StateColor(string words)
        {
            if (words == "Preset") return CACreationUI.Preset;
            if (words == "Saved profile") return CACreationUI.Authored;
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
            CACreationUI.OpenChoices("Saved Cultures",
                "Use a saved Culture. Choose native RimWorld styles separately "
                    + "in the Culture editor under Visual tradition.",
                CAAuthoringChoices.CultureProfiles(draft?.culture,
                    CAPlayerFoundingModel.Seed, Changed));
        }

        private void SaveCultureProfile()
        {
            string failure = CACultureModel.SubstantiveFailure(
                draft?.culture);
            if (!failure.NullOrEmpty())
            {
                Messages.Message(failure, MessageTypeDefOf.RejectInput,
                    false);
                return;
            }
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Save Culture", draft?.culture?.name ?? "Saved Culture", value =>
                {
                    CAAuthoringProfileLibrary.SaveCulture(value, draft?.culture);
                    Changed();
                }));
        }

        private void OpenPoliticalPresets()
        {
            List<CACreationChoice> options =
                CAAuthoringChoices.PoliticalProfiles(
                    draft?.politicalBeliefs,
                    CAPlayerFoundingModel.Seed, delegate
                    {
                        RefreshSuggestedArrangement();
                        Changed();
                    });
            CACreationUI.OpenChoices("Political profiles",
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

        private void LoadIdeo()
        {
            Find.WindowStack.Add(new Dialog_IdeoList_Load(delegate(Ideo ideo)
            {
                CAPlayerFoundingModel.AssignNativeIdeo(draft, ideo);
                CAPlayerFoundingSession.Save();
            }));
        }

        private void ReturnToNativeIdeo()
        {
            CAPlayerFoundingModel.CaptureNativeIdeo(draft,
                CAPlayerFoundingModel.NativeIdeo, null);
            draft.confirmed = false;
            awaitingNativeReturn = true;
            CAPlayerFoundingSession.Save();
            if (nativeIdeoChooser != null) prev = nativeIdeoChooser;
            DoBack();
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

        private void EnsureNativeIdeo()
        {
            if (!ModsConfig.IdeologyActive
                || CAPlayerFoundingModel.NativeIdeo != null) return;
            // Classic is the explicit empty-state default. A preset becomes
            // authoritative only when the player chooses it.
            AssignPreset(null);
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
        private Vector2 scroll;
        private float viewHeight;

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
            const string introduction = "These rules take effect at landing. "
                + "Political beliefs state what the founders consider proper; "
                + "agreement or tension between belief and practice is retained.";
            float introductionHeight = Text.CalcHeight(introduction,
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width,
                introductionHeight), introduction);
            float bodyTop = 38f + introductionHeight + 10f;
            Rect outRect = new Rect(0f, bodyTop, inRect.width,
                Mathf.Max(80f, inRect.height - bodyTop - 55f));
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            Choice(ref y, view.width, "Settlement leadership",
                "Who may give binding orders.",
                new[] { "Founders decide", "Chosen leader" },
                value.leaderRule == "none" ? 0 : 1, index =>
                {
                    value.leaderRule = index == 0 ? "none" : "chosen";
                    Authored();
                });
            Choice(ref y, view.width, "Required work",
                "Whether work and defense may be assigned.",
                new[] { "Voluntary", "May be assigned" },
                value.workRequired ? 1 : 0, index =>
                {
                    value.workRequired = index == 1;
                    Authored();
                });
            Choice(ref y, view.width, "First decisions",
                "Who votes on decisions from the first day.",
                new[] { "No founder vote", "All founders vote" },
                value.foundersDecide ? 1 : 0, index =>
                {
                    value.foundersDecide = index == 1;
                    Authored();
                });
            Choice(ref y, view.width, "Starting supplies",
                "Whether provisions are pooled and rationed.",
                new[] { "Held separately", "Held in common" },
                value.sharedSupplies ? 1 : 0, index =>
                {
                    value.sharedSupplies = index == 1;
                    Authored();
                });
            int duration = value.durationDays == 30 ? 1
                : value.durationDays == 60 ? 2 : 0;
            Choice(ref y, view.width, "Term",
                "When these starting rules expire automatically.",
                new[] { "No fixed end", "30 days", "60 days" },
                duration, index =>
                {
                    value.durationDays = index == 1 ? 30
                        : index == 2 ? 60 : -1;
                    Authored();
                });

            string consequenceText = value.Consequence(
                CAPlayerFoundingModel.StartingPawnCount());
            float consequenceHeight = Mathf.Max(54f, Text.CalcHeight(
                consequenceText, view.width - 20f) + 20f);
            Rect consequence = new Rect(0f, y + 10f, view.width,
                consequenceHeight);
            Widgets.DrawLightHighlight(consequence);
            Widgets.Label(consequence.ContractedBy(10f),
                consequenceText);
            viewHeight = consequence.yMax + 8f;
            Widgets.EndScrollView();
        }

        private static void Choice(ref float y, float width, string label,
            string explanation, string[] choices, int selected,
            Action<int> choose)
        {
            if (width < 700f)
            {
                Text.Font = GameFont.Small;
                float labelHeight = Mathf.Max(22f,
                    Text.CalcHeight(label, width));
                Widgets.Label(new Rect(0f, y, width, labelHeight), label);
                y += labelHeight + 2f;
                Text.Font = GameFont.Tiny;
                GUI.color = ColoredText.SubtleGrayColor;
                float narrowExplanationHeight = Mathf.Max(20f,
                    Text.CalcHeight(explanation, width));
                Widgets.Label(new Rect(0f, y, width, narrowExplanationHeight),
                    explanation);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += narrowExplanationHeight + 5f;
                float segmentHeight = CACreationUI.DrawSegmentRows(
                    new Rect(0f, y, width, 34f), choices, selected, choose,
                    135f);
                y += segmentHeight + 12f;
                return;
            }
            const float labelWidth = 290f;
            Widgets.Label(new Rect(0f, y + 1f, labelWidth - 12f, 22f),
                label);
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            float explanationHeight = Mathf.Max(22f, Text.CalcHeight(
                explanation, labelWidth - 12f));
            Widgets.Label(new Rect(0f, y + 23f, labelWidth - 12f,
                explanationHeight), explanation);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            CACreationUI.DrawSegment(new Rect(labelWidth, y + 4f,
                width - labelWidth, 34f), choices, selected, choose);
            y += Mathf.Max(50f, 23f + explanationHeight + 6f);
        }

        private void Authored()
        {
            CAPlayerFoundingModel.MarkArrangementAuthored(draft);
            changed?.Invoke();
        }
    }
}
