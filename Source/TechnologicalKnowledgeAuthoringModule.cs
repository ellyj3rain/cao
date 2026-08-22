using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    internal sealed class Dialog_CATechnologicalKnowledgeEditor : Window
    {
        private readonly CATechnologicalKnowledge knowledge;
        private readonly string identity;
        private readonly Action changed;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1060f, UI.screenWidth - 48f),
            Mathf.Min(790f, UI.screenHeight - 48f));

        internal Dialog_CATechnologicalKnowledgeEditor(
            CATechnologicalKnowledge knowledge, string identity,
            Action changed)
        {
            this.knowledge = knowledge ?? new CATechnologicalKnowledge();
            this.identity = identity ?? "technological-knowledge";
            this.changed = changed;
            CATechnologicalKnowledgeModel.Ensure(this.knowledge,
                this.identity);
            doCloseX = false;
            doCloseButton = false;
            doWindowBackground = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        protected override float Margin => 0f;

        public override void DoWindowContents(Rect inRect)
        {
            inRect = CAOpeningTheme.BeginWindowSurface(inRect);
            try
            {
                DoEditorContents(inRect);
            }
            finally
            {
                CAOpeningTheme.EndWindowSurface();
            }
        }

        private void DoEditorContents(Rect inRect)
        {
            CAOpeningTheme.Heading(0f, 0f, inRect.width,
                "Technological knowledge");
            const string introduction = "Set what this faction can understand, build, operate, and maintain. These levels govern settlement viability, production, research, and autonomous construction.";
            float introductionHeight = CAOpeningTheme.Fine(0f, 36f,
                inRect.width, introduction);

            float y = 44f + introductionHeight;
            float profileWidth = Mathf.Min(250f, inRect.width * 0.32f);
            if (CAOpeningTheme.GhostButton(new Rect(0f, y, profileWidth,
                    30f), "Use a starting level..."))
                OpenProfiles();
            GUI.color = CAOpeningTheme.TextLo;
            string summary = CATechnologicalKnowledgeModel.Summary(knowledge);
            Widgets.Label(new Rect(profileWidth + 12f, y + 5f,
                inRect.width - profileWidth - 12f, 25f), summary);
            GUI.color = Color.white;
            y += 36f;
            // CHANGE A RANK, SEE THE CONSEQUENCE: the same boundary answer
            // the founding card states, recomputed live as ranks change.
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.58f, 0.70f, 0.82f);
            string consequence = CATechnologicalKnowledgeModel
                .ConstructionConsequence(knowledge);
            float consequenceHeight = Text.CalcHeight(consequence,
                inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width,
                consequenceHeight), consequence);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += consequenceHeight + 8f;

            const float domainWidth = 250f;
            float competencyWidth = Mathf.Max(112f,
                (inRect.width - domainWidth - 18f) / 4f);
            float tableWidth = domainWidth + competencyWidth * 4f;
            DrawHeader(new Rect(0f, y, tableWidth, 30f), domainWidth,
                competencyWidth);
            y += 34f;

            Rect outer = new Rect(0f, y, inRect.width,
                inRect.height - y - 46f);
            float rowHeight = 61f;
            Rect view = new Rect(0f, 0f, tableWidth,
                CATechnologyDomains.All.Length * rowHeight);
            Widgets.BeginScrollView(outer, ref scroll, view);
            for (int i = 0; i < CATechnologyDomains.All.Length; i++)
                DrawDomainRow(new Rect(0f, i * rowHeight, tableWidth,
                    rowHeight - 4f), CATechnologyDomains.All[i],
                    domainWidth, competencyWidth);
            Widgets.EndScrollView();

            CAOpeningTheme.Divider(0f, inRect.height - 42f, inRect.width);
            if (CAOpeningTheme.PrimaryButton(new Rect(
                    inRect.width - 150f, inRect.height - 36f, 150f, 34f),
                    "Done"))
                Close();
        }

        private static void DrawHeader(Rect rect, float domainWidth,
            float competencyWidth)
        {
            Widgets.DrawBoxSolid(rect, CAOpeningTheme.Raised);
            CAOpeningTheme.Border(rect, CAOpeningTheme.Hairline);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, domainWidth - 8f,
                rect.height), "Knowledge domain");
            string[] labels = { "Understand", "Build", "Operate",
                "Maintain" };
            for (int i = 0; i < labels.Length; i++)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x + domainWidth
                    + i * competencyWidth, rect.y, competencyWidth,
                    rect.height), labels[i]);
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawDomainRow(Rect rect,
            CATechnologyDomainDef domain, float domainWidth,
            float competencyWidth)
        {
            Widgets.DrawBoxSolid(rect, CAOpeningTheme.Surface);
            CAOpeningTheme.Border(rect, CAOpeningTheme.Hairline);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f,
                domainWidth - 14f, 24f), domain.Label);
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 29f,
                domainWidth - 14f, 23f), domain.Description);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            for (int i = 0; i < CATechnologyCompetencies.All.Length; i++)
            {
                string competency = CATechnologyCompetencies.All[i];
                int rank = CATechnologicalKnowledgeModel.Rank(knowledge,
                    domain.Key, competency);
                Rect button = new Rect(rect.x + domainWidth
                    + i * competencyWidth + 4f, rect.y + 10f,
                    competencyWidth - 8f, 36f);
                if (CAOpeningTheme.GhostButton(button,
                        RankButtonLabel(rank)))
                    OpenRanks(domain, competency, rank);
                TooltipHandler.TipRegion(button, domain.Label + ": "
                    + CompetencySentence(competency, rank));
            }
        }

        private void OpenProfiles()
        {
            var menu = CATechnologicalKnowledgeModel.Profiles.Select(profile =>
            {
                CATechnologyProfile local = profile;
                return new FloatMenuOption(local.Label, delegate
                {
                    CATechnologicalKnowledgeModel.ApplyProfile(knowledge,
                        local.Key, CAAxisSource.Authored,
                        "selected knowledge starting level: " + local.Key);
                    MarkChanged();
                });
            }).ToList();
            Find.WindowStack.Add(new FloatMenu(menu));
        }

        private void OpenRanks(CATechnologyDomainDef domain,
            string competency, int current)
        {
            var menu = Enumerable.Range(0, 6).Select(rank =>
            {
                int localRank = rank;
                string label = RankButtonLabel(localRank);
                if (localRank == current) label += " (current)";
                return new FloatMenuOption(label, delegate
                {
                    CATechnologicalKnowledgeModel.SetRank(knowledge,
                        domain.Key, competency, localRank,
                        CAAxisSource.Authored,
                        "authored " + domain.Key + " " + competency);
                    MarkChanged();
                });
            }).ToList();
            Find.WindowStack.Add(new FloatMenu(menu));
        }

        private void MarkChanged()
        {
            // Authoring changes the faction-owned composition before runtime.
            // Runtime custody is generated from the finished composition.
            knowledge.custody.Clear();
            knowledge.availabilityHistory.Clear();
            knowledge.distributionInitialized = false;
            CATechnologicalKnowledgeModel.Ensure(knowledge, identity);
            changed?.Invoke();
        }

        private static string RankButtonLabel(int rank)
        {
            switch (rank)
            {
                case 0: return "None";
                case 1: return "Basic";
                case 2: return "Developed";
                case 3: return "Industrial";
                case 4: return "Advanced";
                default: return "Spacer";
            }
        }

        private static string CompetencySentence(string competency, int rank)
        {
            string action = competency == CATechnologyCompetencies.Construct
                ? "build" : competency == CATechnologyCompetencies.Operate
                    ? "operate" : competency
                        == CATechnologyCompetencies.Maintain
                        ? "maintain" : "understand";
            return rank <= 0 ? "No established ability to " + action + "."
                : RankButtonLabel(rank) + " ability to " + action + ".";
        }
    }
}
