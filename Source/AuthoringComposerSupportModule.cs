using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    internal sealed class CAAxisGroupDef
    {
        internal string Key;
        internal string Label;
        internal string[] Axes;
    }

    internal static class CAAuthoringChoices
    {
        internal static readonly CAAxisGroupDef[] PoliticalGroups =
        {
            new CAAxisGroupDef
            {
                Key = "governance", Label = "Governance",
                Axes = new[] { CAFactionAxes.Leadership,
                    CAFactionAxes.Decisions }
            },
            new CAAxisGroupDef
            {
                Key = "civic", Label = "Civic participation",
                Axes = new[] { CAFactionAxes.Participation,
                    CAFactionAxes.Dissent }
            },
            new CAAxisGroupDef
            {
                Key = "economy", Label = "Property and economy",
                Axes = new[] { CAFactionAxes.Ownership,
                    CAFactionAxes.Economy, CAFactionAxes.Work,
                    CAFactionAxes.Support }
            },
            new CAAxisGroupDef
            {
                Key = "membership", Label = "Membership and order",
                Axes = new[] { CAFactionAxes.Membership,
                    CAFactionAxes.Status, CAFactionAxes.LocalOrder }
            },
            new CAAxisGroupDef
            {
                Key = "security", Label = "Security and conflict",
                Axes = new[] { CAFactionAxes.Defense,
                    CAFactionAxes.WarConduct }
            }
        };

        internal static List<CACreationChoice> CultureProfiles(
            CACulture culture, string seed, Action changed)
        {
            var choices = new List<CACreationChoice>();
            foreach (CACulturePreset preset in CACultureModel.Presets)
            {
                CACulturePreset local = preset;
                choices.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label,
                    Summary = local.Description,
                    CompactSummary = CultureTraits(local, 2),
                    Traits = CultureTraits(local, 4),
                    Details = CultureDetails(local),
                    Group = "Built-in",
                    Badge = "Built-in profile",
                    Icon = ContentFinder<Texture2D>.Get(local.IconPath),
                    Accent = CACreationUI.Preset,
                    Selected = CACultureModel.UsesPreset(culture, local),
                    ConfirmLabel = "Apply this profile",
                    Choose = delegate
                    {
                        CACultureModel.ApplyPreset(culture, local);
                        CACultureModel.EnsureGenerated(culture,
                            seed + ":culture-profile:" + local.Key,
                            CACultureModel.NativeDef(culture));
                        changed?.Invoke();
                    }
                });
            }
            foreach (CAUserCultureProfile profile in
                CAAuthoringProfileLibrary.Cultures.OrderBy(item =>
                    item.displayName))
            {
                CAUserCultureProfile local = profile;
                choices.Add(new CACreationChoice
                {
                    Key = local.key,
                    Name = local.displayName,
                    Summary = CACultureModel.Summary(local.values),
                    CompactSummary = "Saved culture profile",
                    Traits = CultureDetails(local.values),
                    Details = "Applying this profile copies its values into "
                        + "the current world draft. Later edits do not change "
                        + "the saved library copy.",
                    Group = "Saved",
                    Badge = "Saved profile",
                    Icon = CACultureModel.Icon(local.values),
                    Accent = CACreationUI.Authored,
                    Selected = culture?.profileKey == local.key,
                    ConfirmLabel = "Apply saved profile",
                    Choose = delegate
                    {
                        CAAuthoringProfileLibrary.Apply(local, culture);
                        changed?.Invoke();
                    }
                });
            }
            return choices;
        }

        internal static string CultureIdentity(CACulture culture)
        {
            if (culture == null) return "Culture not set";
            CAUserCultureProfile saved = CAAuthoringProfileLibrary.Cultures
                .FirstOrDefault(item => item.key == culture.profileKey);
            return saved?.displayName
                ?? CACultureModel.Preset(culture.presetName)?.Label
                ?? culture.name
                ?? "Custom culture";
        }

        internal static List<CACreationChoice> PoliticalProfiles(
            CAPoliticalBeliefs beliefs, string seed, Action changed)
        {
            var choices = new List<CACreationChoice>();
            foreach (CAFactionAxes.PoliticalPreset preset in
                CAFactionAxes.Presets)
            {
                CAFactionAxes.PoliticalPreset local = preset;
                choices.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Name,
                    Summary = local.Description,
                    CompactSummary = CAPoliticalBeliefsModel.PresetTraits(
                        local, 2),
                    Traits = PoliticalFingerprint(local),
                    Details = CAPoliticalBeliefsModel.PresetDetails(local),
                    Group = PoliticalFamily(local),
                    Badge = "Built-in profile",
                    Icon = CAPoliticalBeliefsModel.Icon(local),
                    Accent = CACreationUI.Preset,
                    Selected = CAPoliticalBeliefsModel.UsesPreset(
                        beliefs, local),
                    ConfirmLabel = "Apply these beliefs",
                    Choose = delegate
                    {
                        CAPoliticalBeliefsModel.ChoosePreset(beliefs, local,
                            seed + ":political-profile:" + local.Key);
                        changed?.Invoke();
                    }
                });
            }
            foreach (CAUserPoliticalProfile profile in
                CAAuthoringProfileLibrary.Politics.OrderBy(item =>
                    item.displayName))
            {
                CAUserPoliticalProfile local = profile;
                choices.Add(new CACreationChoice
                {
                    Key = local.key,
                    Name = local.displayName,
                    Summary = CAPoliticalBeliefsModel.Summary(local.values),
                    CompactSummary = "Saved political profile",
                    Traits = PoliticalFingerprint(local.values),
                    Details = PoliticalDetails(local.values)
                        + "\n\nApplying this profile copies its values into "
                        + "the current world draft. Later edits do not change "
                        + "the saved library copy.",
                    Group = "Saved",
                    Badge = "Saved profile",
                    Icon = CAPoliticalBeliefsModel.Icon(local.values),
                    Accent = CACreationUI.Authored,
                    Selected = beliefs?.profileKey == local.key,
                    ConfirmLabel = "Apply saved profile",
                    Choose = delegate
                    {
                        CAAuthoringProfileLibrary.Apply(local, beliefs);
                        changed?.Invoke();
                    }
                });
            }
            return choices;
        }

        internal static string PoliticalIdentity(
            CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "Political beliefs not set";
            CAUserPoliticalProfile saved = CAAuthoringProfileLibrary.Politics
                .FirstOrDefault(item => item.key == beliefs.profileKey);
            return saved?.displayName
                ?? CAFactionAxes.Preset(beliefs.presetName)?.Name
                ?? "Custom political beliefs";
        }

        internal static string CultureTraits(CACulturePreset preset,
            int count)
        {
            if (preset == null) return "No cultural practices recorded";
            return string.Join(" · ", CACultureModel.Domains
                .Where(domain => preset.Positions.ContainsKey(domain.Field))
                .Take(count)
                .Select(domain => CACultureModel.Option(domain.Field,
                    preset.Positions[domain.Field])?.Label)
                .Where(value => !value.NullOrEmpty()).ToArray());
        }

        internal static string CultureDetails(CACulturePreset preset)
        {
            if (preset == null) return "No cultural practices recorded.";
            string visualKey;
            string visual = preset.Positions.TryGetValue(
                    CACulture.SourceCultureField, out visualKey)
                ? "Visual style source: " + visualKey + "."
                : "Visual style source: chosen separately.";
            return visual + "\n" + string.Join("\n",
                CACultureModel.Domains.Select(domain =>
            {
                string key;
                if (!preset.Positions.TryGetValue(domain.Field, out key))
                    return domain.Label + ": generated when applied.";
                CACultureOptionDef option = CACultureModel.Option(
                    domain.Field, key);
                return domain.Label + ": " + (option?.Label ?? key) + ". "
                    + (option?.Summary ?? "") + " "
                    + (option?.Consumer ?? "");
            }).ToArray());
        }

        internal static string CultureDetails(CACulture culture)
        {
            if (culture == null) return "No culture recorded.";
            return string.Join("\n", CACultureModel.Domains.Select(domain =>
            {
                CACultureOptionDef option = CACultureModel.Option(
                    domain.Field, culture.Value(domain.Field));
                return domain.Label + ": "
                    + (option?.Label ?? "No custom chosen") + ". "
                    + (option?.Consumer ?? "Generated at materialization.");
            }).ToArray());
        }

        internal static string PoliticalFingerprint(
            CAFactionAxes.PoliticalPreset preset)
        {
            if (preset == null) return "No positions recorded";
            var temporary = new CAPoliticalBeliefs();
            CAPoliticalBeliefsModel.ApplyPreset(temporary, preset);
            return PoliticalFingerprint(temporary);
        }

        internal static string PoliticalFingerprint(
            CAPoliticalBeliefs beliefs)
        {
            return string.Join(" | ", PoliticalGroups.Select(group =>
            {
                int set = group.Axes.Count(axis => !CAFactionAxes.KeyOf(
                    beliefs?.positions, axis).NullOrEmpty());
                string initial = new string(group.Label.Where(char.IsLetter)
                    .Take(1).ToArray()).ToUpperInvariant();
                return initial + set;
            }).ToArray());
        }

        internal static string PoliticalDetails(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "No political positions recorded.";
            return string.Join("\n", CAFactionAxes.Axes.Select(axis =>
            {
                CAAxisOption option = CAFactionAxes.OptionOf(
                    beliefs.positions, axis.Key);
                return axis.Label + ": "
                    + (option?.Label ?? "No position chosen") + ". "
                    + (option?.Words ?? "");
            }).ToArray());
        }

        private static string PoliticalFamily(
            CAFactionAxes.PoliticalPreset preset)
        {
            string leader = preset?.Positions.ContainsKey(
                CAFactionAxes.Leadership) == true
                    ? preset.Positions[CAFactionAxes.Leadership] : null;
            if (leader == "single") return "Central rule";
            if (leader == "none") return "No central rule";
            if (preset?.Key == "common_ownership"
                || preset?.Key == "independent_communes"
                || preset?.Key == "worker_federation")
                return "Common and cooperative";
            return "Councils and federations";
        }
    }

    internal sealed class Dialog_CAProfileName : Window
    {
        private readonly string title;
        private readonly Action<string> accepted;
        private string value;

        public override Vector2 InitialSize => new Vector2(520f, 190f);

        internal Dialog_CAProfileName(string title, string initial,
            Action<string> accepted)
        {
            this.title = title;
            this.accepted = accepted;
            value = initial ?? "";
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), title);
            value = Widgets.TextField(new Rect(0f, 40f, inRect.width, 30f),
                value);
            if (Widgets.ButtonText(new Rect(inRect.width - 120f, 88f,
                    120f, 32f), "Save") && !value.NullOrEmpty())
            {
                accepted?.Invoke(value.Trim());
                Close();
            }
        }
    }

    internal sealed class Dialog_CAProfileManager : Window
    {
        private readonly CACulture culture;
        private readonly CAPoliticalBeliefs beliefs;
        private readonly Action changed;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(760f, UI.screenWidth - 48f),
            Mathf.Min(620f, UI.screenHeight - 48f));

        internal Dialog_CAProfileManager(CACulture culture,
            CAPoliticalBeliefs beliefs, Action changed)
        {
            this.culture = culture;
            this.beliefs = beliefs;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            bool cultures = culture != null;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                cultures ? "Saved culture profiles"
                    : "Saved political profiles");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, inRect.width, 42f),
                "Saved profiles are global. Loading copies values into this "
                + "draft; renaming or deleting the library copy does not alter a world.");
            Rect outer = new Rect(0f, 88f, inRect.width,
                inRect.height - 136f);
            int count = cultures ? CAAuthoringProfileLibrary.Cultures.Count
                : CAAuthoringProfileLibrary.Politics.Count;
            bool compactRows = outer.width - 18f < 560f;
            Rect view = new Rect(0f, 0f, outer.width - 18f,
                Mathf.Max(outer.height,
                    count * (compactRows ? 86f : 48f) + 8f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            rowY = 0f;
            if (cultures)
            {
                foreach (CAUserCultureProfile profile in
                    CAAuthoringProfileLibrary.Cultures.ToList())
                    DrawCultureRow(view.width, profile);
            }
            else
            {
                foreach (CAUserPoliticalProfile profile in
                    CAAuthoringProfileLibrary.Politics.ToList())
                    DrawPoliticalRow(view.width, profile);
            }
            Widgets.EndScrollView();
        }

        private float rowY;

        private void DrawCultureRow(float width,
            CAUserCultureProfile profile)
        {
            DrawProfileRow(width, profile.displayName, delegate
                {
                    CAAuthoringProfileLibrary.Apply(profile, culture);
                    changed?.Invoke();
                }, () => Find.WindowStack.Add(new Dialog_CAProfileName(
                    "Rename culture profile", profile.displayName,
                    value => CAAuthoringProfileLibrary.Rename(profile, value))),
                () => CAAuthoringProfileLibrary.Duplicate(profile),
                () => Find.WindowStack.Add(
                    Dialog_MessageBox.CreateConfirmation(
                    "Delete saved profile " + profile.displayName + "?",
                    () => CAAuthoringProfileLibrary.Delete(profile), true)));
        }

        private void DrawPoliticalRow(float width,
            CAUserPoliticalProfile profile)
        {
            DrawProfileRow(width, profile.displayName, delegate
                {
                    CAAuthoringProfileLibrary.Apply(profile, beliefs);
                    changed?.Invoke();
                }, () => Find.WindowStack.Add(new Dialog_CAProfileName(
                    "Rename political profile", profile.displayName,
                    value => CAAuthoringProfileLibrary.Rename(profile, value))),
                () => CAAuthoringProfileLibrary.Duplicate(profile),
                () => Find.WindowStack.Add(
                    Dialog_MessageBox.CreateConfirmation(
                    "Delete saved profile " + profile.displayName + "?",
                    () => CAAuthoringProfileLibrary.Delete(profile), true)));
        }

        private void DrawProfileRow(float width, string name, Action load,
            Action rename, Action duplicate, Action delete)
        {
            bool compact = width < 560f;
            float height = compact ? 78f : 40f;
            Rect row = new Rect(0f, rowY, width, height);
            Widgets.DrawAltRect(row);
            Rect[] buttons = new Rect[4];
            if (compact)
            {
                Widgets.Label(new Rect(8f, row.y + 5f, width - 16f, 26f),
                    name);
                const float gap = 5f;
                float buttonWidth = (width - gap * 3f) / 4f;
                for (int i = 0; i < buttons.Length; i++)
                    buttons[i] = new Rect(i * (buttonWidth + gap),
                        row.y + 39f, buttonWidth, 30f);
            }
            else
            {
                Widgets.Label(new Rect(8f, row.y + 8f, width - 344f, 28f),
                    name);
                buttons[0] = new Rect(width - 328f, row.y + 4f, 72f, 30f);
                buttons[1] = new Rect(width - 250f, row.y + 4f, 72f, 30f);
                buttons[2] = new Rect(width - 172f, row.y + 4f, 72f, 30f);
                buttons[3] = new Rect(width - 94f, row.y + 4f, 86f, 30f);
            }
            Action[] actions = { load, rename, duplicate, delete };
            string[] labels = { "Load", "Rename", "Copy", "Delete" };
            for (int i = 0; i < buttons.Length; i++)
                if (Widgets.ButtonText(buttons[i], labels[i])) actions[i]?.Invoke();
            rowY += compact ? 86f : 48f;
        }

    }
}
