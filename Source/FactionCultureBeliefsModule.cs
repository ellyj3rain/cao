using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Carried cultural background is separate from Ideoligion, political
    // belief, and the practices an established settlement has actually
    // developed. A native CultureDef may supply a visual tradition; local
    // cultural expression is derived from the population and its circumstances.
    public sealed class CACulture : IExposable
    {
        public const int CurrentSchemaVersion = 3;
        public const int NameField = 1;
        public const int SourceCultureField = 2;
        // Schema-2 recipe bits remain constants only so migration can discard
        // them without confusing their old authorship with active fields.
        private const int LegacyPracticeFields = 8 | 16 | 32 | 64;
        public int schemaVersion = CurrentSchemaVersion;
        public string id;
        public string name;
        public string sourceCultureDefName;
        // Schema-2 migration inputs. They are read, cleared in Migrate, and
        // never consulted by generation, validation, presentation, or runtime.
        private string gatheringKey;
        private string hospitalityKey;
        private string mealsKey;
        private string remembranceKey;
        public string presetName;
        public string profileKey;
        public int authoredMask;
        public int presetMask;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref sourceCultureDefName,
                "sourceCultureDefName");
            Scribe_Values.Look(ref gatheringKey, "gatheringKey");
            Scribe_Values.Look(ref hospitalityKey, "hospitalityKey");
            Scribe_Values.Look(ref mealsKey, "mealsKey");
            Scribe_Values.Look(ref remembranceKey, "remembranceKey");
            Scribe_Values.Look(ref presetName, "presetName");
            Scribe_Values.Look(ref profileKey, "profileKey");
            Scribe_Values.Look(ref authoredMask, "authoredMask", 0);
            Scribe_Values.Look(ref presetMask, "presetMask", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CACultureModel.Migrate(this);
        }

        internal CACulture Copy()
        {
            return new CACulture
            {
                schemaVersion = CurrentSchemaVersion,
                id = id,
                name = name,
                sourceCultureDefName = sourceCultureDefName,
                presetName = presetName,
                profileKey = profileKey,
                authoredMask = authoredMask & (NameField | SourceCultureField),
                presetMask = 0
            };
        }

        internal void CopyFrom(CACulture source)
        {
            if (source == null) return;
            schemaVersion = CurrentSchemaVersion;
            id = source.id;
            name = source.name;
            sourceCultureDefName = source.sourceCultureDefName;
            presetName = source.presetName;
            profileKey = source.profileKey;
            authoredMask = source.authoredMask & (NameField | SourceCultureField);
            presetMask = 0;
        }

        internal bool Authored(int field)
        {
            return (authoredMask & field) != 0;
        }

        internal void Choose(int field, string value)
        {
            Set(field, value);
            authoredMask |= field;
            presetMask &= ~field;
            profileKey = null;
            presetName = null;
        }

        internal void Set(int field, string value)
        {
            switch (field)
            {
                case NameField: name = value; break;
                case SourceCultureField:
                    sourceCultureDefName = value; break;
            }
        }

        internal void Release(int field)
        {
            Set(field, null);
            authoredMask &= ~field;
            presetMask &= ~field;
            presetName = null;
            profileKey = null;
        }

        internal string Value(int field)
        {
            switch (field)
            {
                case NameField: return name;
                case SourceCultureField: return sourceCultureDefName;
                default: return null;
            }
        }

        internal void ClearLegacyPractices()
        {
            gatheringKey = null;
            hospitalityKey = null;
            mealsKey = null;
            remembranceKey = null;
            authoredMask &= ~LegacyPracticeFields;
            presetMask = 0;
        }
    }

    // Political beliefs and faction structure use the same questions so a
    // disagreement is readable without collapsing the two persisted states.
    public sealed class CAPoliticalBeliefs : IExposable
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public string id;
        public string presetName;
        public string profileKey;
        public List<CAAxisEntry> positions = new List<CAAxisEntry>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref presetName, "presetName");
            Scribe_Values.Look(ref profileKey, "profileKey");
            Scribe_Collections.Look(ref positions, "positions", LookMode.Deep);
            if (positions == null) positions = new List<CAAxisEntry>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CAPoliticalBeliefsModel.Migrate(this);
        }

        internal CAPoliticalBeliefs Copy()
        {
            return new CAPoliticalBeliefs
            {
                schemaVersion = schemaVersion,
                id = id,
                presetName = presetName,
                profileKey = profileKey,
                positions = CAFactionStartingState.CopyAxes(positions)
            };
        }

        internal void CopyFrom(CAPoliticalBeliefs source)
        {
            if (source == null) return;
            schemaVersion = CurrentSchemaVersion;
            id = source.id;
            presetName = source.presetName;
            profileKey = source.profileKey;
            positions = CAFactionStartingState.CopyAxes(source.positions);
        }
    }

    internal static class CACultureModel
    {
        internal static string CompatibilityFailure(CACulture culture)
        {
            if (culture == null) return "No culture is recorded.";
            // Missing visual assets only disable optional styling.
            return null;
        }

        internal static void Migrate(CACulture culture)
        {
            if (culture == null) return;
            CACultureLegacyMigrationResult migrated =
                CACultureLegacyMigrationKernel.Migrate(
                    new CACultureLegacyMigrationInput
                    {
                        SchemaVersion = culture.schemaVersion,
                        PresetName = culture.presetName,
                        Name = culture.name,
                        SourceCultureDefName = culture.sourceCultureDefName,
                        NameAuthored = culture.Authored(CACulture.NameField)
                    });
            culture.schemaVersion = migrated.SchemaVersion;
            culture.presetName = migrated.PresetName;
            culture.name = migrated.Name;
            culture.sourceCultureDefName = migrated.SourceCultureDefName;
            if (migrated.ClearLegacyPractices)
                culture.ClearLegacyPractices();
        }

        internal static void EnsureGenerated(CACulture culture,
            string seed, CultureDef fallback)
        {
            if (culture == null) return;
            Migrate(culture);
            int hash = GenText.StableStringHash(seed ?? "ca-culture");
            if (culture.id.NullOrEmpty())
                culture.id = "culture:" + Math.Abs((long)hash);
            if (culture.sourceCultureDefName.NullOrEmpty()
                && !culture.Authored(CACulture.SourceCultureField)
                && fallback != null)
                culture.sourceCultureDefName = fallback.defName;
            if (culture.name.NullOrEmpty())
                culture.name = fallback != null
                    ? fallback.LabelCap + " background"
                    : "Carried background";
        }

        internal static string Summary(CACulture culture)
        {
            if (culture == null) return "Cultural background not set";
            CultureDef source = NativeDef(culture);
            string identity = CAAuthoringChoices.CultureIdentity(culture);
            string visual = source?.LabelCap.ToString()
                ?? (culture.sourceCultureDefName.NullOrEmpty()
                    ? "neutral visual fallback"
                    : "visual source unavailable; neutral fallback");
            string compact = identity + " - " + visual;
            string standard = compact
                + ". Each settlement develops its own customs.";
            string expanded = standard
                + " Beliefs, residents, local rule, work, trade, and the land "
                + "shape how this background is lived.";
            return CAInformationPresentation.Select(compact, standard,
                expanded);
        }

        internal static Texture2D Icon(CACulture culture)
        {
            CultureDef native = NativeDef(culture);
            if (native != null && !native.iconPath.NullOrEmpty())
                return native.Icon;
            return ContentFinder<Texture2D>.Get(
                    "Rimshare/WorldMapIcons/feather", false)
                ?? BaseContent.BadTex;
        }

        internal static CultureDef NativeDef(CACulture culture)
        {
            return culture == null || culture.sourceCultureDefName.NullOrEmpty()
                ? null : DefDatabase<CultureDef>.GetNamedSilentFail(
                    culture.sourceCultureDefName);
        }
    }

    internal static class CAPoliticalBeliefsModel
    {
        internal static void Migrate(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return;
            CAFactionAxes.PoliticalPreset preset =
                CAFactionAxes.Preset(beliefs.presetName);
            if (preset != null) beliefs.presetName = preset.Key;
            beliefs.schemaVersion = CAPoliticalBeliefs.CurrentSchemaVersion;
        }

        internal static void Ensure(CAPoliticalBeliefs beliefs,
            string seed)
        {
            if (beliefs == null) return;
            Migrate(beliefs);
            if (beliefs.id.NullOrEmpty())
                beliefs.id = "politics:" + Math.Abs((long)
                    GenText.StableStringHash(seed ?? "ca-politics"));
        }

        internal static int GenerateUnset(CAPoliticalBeliefs beliefs,
            string seed)
        {
            if (beliefs == null) return 0;
            int hash = GenText.StableStringHash(seed ?? beliefs.id
                ?? "ca-politics");
            int filled = 0;
            for (int i = 0; i < CAFactionAxes.Axes.Length; i++)
            {
                CAAxisDef def = CAFactionAxes.Axes[i];
                if (CAFactionAxes.StateOf(beliefs.positions, def.Key)
                    != CAAxisSource.Unset) continue;
                int index = (int)(Math.Abs((long)Gen.HashCombineInt(hash,
                    101 + i * 37)) % def.Options.Length);
                CAFactionAxes.Set(beliefs.positions, def.Key,
                    def.Options[index].Key, CAAxisSource.Generated);
                filled++;
            }
            return filled;
        }

        internal static void ApplyPreset(CAPoliticalBeliefs beliefs,
            CAFactionAxes.PoliticalPreset preset)
        {
            if (beliefs == null || preset == null) return;
            foreach (CAAxisEntry entry in beliefs.positions)
                if (entry != null && entry.source
                    == (byte)CAAxisSource.Preset)
                    entry.source = (byte)CAAxisSource.Unset;
            ApplyPresetLayer(beliefs, preset);
            beliefs.presetName = preset.Key;
            beliefs.profileKey = null;
        }

        // Choosing a preset is one explicit authoring transition. It replaces
        // the prior set, applies the preset hierarchy, and deterministically
        // fills axes the preset does not name. Both player and established-
        // faction editors use this contract.
        internal static void ChoosePreset(CAPoliticalBeliefs beliefs,
            CAFactionAxes.PoliticalPreset preset, string seed)
        {
            if (beliefs == null || preset == null) return;
            Ensure(beliefs, seed);
            beliefs.positions.Clear();
            ApplyPresetLayer(beliefs, preset);
            beliefs.presetName = preset.Key;
            beliefs.profileKey = null;
            GenerateUnset(beliefs, seed + ":preset-fill");
        }

        internal static bool UsesPreset(CAPoliticalBeliefs beliefs,
            CAFactionAxes.PoliticalPreset preset)
        {
            return beliefs != null && preset != null
                && CAFactionAxes.Preset(beliefs.presetName)?.Key == preset.Key
                && PresetStillDescribes(beliefs);
        }

        private static void ApplyPresetLayer(
            CAPoliticalBeliefs beliefs,
            CAFactionAxes.PoliticalPreset preset)
        {
            if (preset.Parent != null)
            {
                CAFactionAxes.PoliticalPreset parent =
                    CAFactionAxes.Preset(preset.Parent);
                if (parent != null) ApplyPresetLayer(beliefs, parent);
            }
            foreach (KeyValuePair<string, string> position in
                preset.Positions)
            {
                if (CAFactionAxes.StateOf(beliefs.positions, position.Key)
                    == CAAxisSource.Authored) continue;
                CAFactionAxes.Set(beliefs.positions, position.Key,
                    position.Value, CAAxisSource.Preset);
            }
        }

        internal static void Author(CAPoliticalBeliefs beliefs,
            string axisKey, string optionKey)
        {
            CAFactionAxes.Set(beliefs.positions, axisKey, optionKey,
                CAAxisSource.Authored);
            beliefs.profileKey = null;
            beliefs.presetName = null;
        }

        internal static void Release(CAPoliticalBeliefs beliefs,
            string axisKey)
        {
            if (beliefs == null) return;
            CAFactionAxes.Release(beliefs.positions, axisKey);
            beliefs.profileKey = null;
            beliefs.presetName = null;
        }

        private static bool PresetStillDescribes(
            CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null
                || beliefs.presetName.NullOrEmpty()) return true;
            CAFactionAxes.PoliticalPreset preset =
                CAFactionAxes.Preset(beliefs.presetName);
            if (preset == null) return false;
            var expected = new Dictionary<string, string>();
            CollectPreset(preset, expected);
            if (!expected.All(pair =>
                    CAFactionAxes.KeyOf(beliefs.positions, pair.Key)
                        == pair.Value)) return false;
            return true;
        }

        private static void CollectPreset(
            CAFactionAxes.PoliticalPreset preset,
            Dictionary<string, string> expected)
        {
            if (preset.Parent != null)
            {
                CAFactionAxes.PoliticalPreset parent =
                    CAFactionAxes.Preset(preset.Parent);
                if (parent != null) CollectPreset(parent, expected);
            }
            foreach (KeyValuePair<string, string> pair in preset.Positions)
                expected[pair.Key] = pair.Value;
        }

        internal static string Summary(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "Political beliefs not set";
            string preset = CAAuthoringChoices.PoliticalIdentity(beliefs);
            string leadership = CAFactionAxes.OptionOf(beliefs.positions,
                CAFactionAxes.Leadership)?.Label;
            string ownership = CAFactionAxes.OptionOf(beliefs.positions,
                CAFactionAxes.Ownership)?.Label;
            return preset + (leadership == null ? "" : " · " + leadership)
                + (ownership == null ? "" : " · " + ownership);
        }

        internal static string DescribePreset(
            CAFactionAxes.PoliticalPreset preset)
        {
            return PresetTraits(preset, 5);
        }

        internal static string PresetTraits(
            CAFactionAxes.PoliticalPreset preset, int count)
        {
            if (preset == null) return "No preset";
            var expected = new Dictionary<string, string>();
            CollectPreset(preset, expected);
            return string.Join(" · ", CAFactionAxes.Axes
                .Where(axis => expected.ContainsKey(axis.Key))
                .Take(Math.Max(1, count))
                .Select(axis => axis.Options.FirstOrDefault(option =>
                    option.Key == expected[axis.Key])?.Label
                    ?? expected[axis.Key])
                .ToArray());
        }

        internal static string PresetDetails(
            CAFactionAxes.PoliticalPreset preset)
        {
            if (preset == null) return "No political positions recorded.";
            var expected = new Dictionary<string, string>();
            CollectPreset(preset, expected);
            return string.Join("\n", CAFactionAxes.Axes
                .Where(axis => expected.ContainsKey(axis.Key))
                .Select(axis =>
                {
                    CAAxisOption option = axis.Options.FirstOrDefault(item =>
                        item.Key == expected[axis.Key]);
                    return axis.Label + ": " + (option?.Label
                        ?? expected[axis.Key]) + ". " + (option?.Words ?? "");
                }).ToArray());
        }

        internal static Texture2D Icon(
            CAFactionAxes.PoliticalPreset preset)
        {
            var temporary = new CAPoliticalBeliefs();
            ApplyPreset(temporary, preset);
            return Icon(temporary);
        }

        internal static Texture2D Icon(CAPoliticalBeliefs beliefs)
        {
            string leadership = CAFactionAxes.KeyOf(beliefs?.positions,
                CAFactionAxes.Leadership);
            string ownership = CAFactionAxes.KeyOf(beliefs?.positions,
                CAFactionAxes.Ownership);
            string path = leadership == "none"
                ? "Rimshare/WorldMapIcons/anarchy"
                : leadership == "single"
                    ? "Rimshare/WorldMapIcons/castle"
                    : ownership == "common" || ownership == "cooperative"
                        ? "Rimshare/WorldMapIcons/hammer-sickle"
                        : "Rimshare/WorldMapIcons/cog";
            return ContentFinder<Texture2D>.Get(path);
        }
    }

    internal static class CAFactionStructureModel
    {
        internal static int GenerateUnset(List<CAAxisEntry> structure,
            CAPoliticalBeliefs beliefs, string seed)
        {
            if (structure == null) return 0;
            int hash = GenText.StableStringHash(seed ?? "ca-structure");
            int filled = 0;
            for (int i = 0; i < CAFactionAxes.Axes.Length; i++)
            {
                CAAxisDef def = CAFactionAxes.Axes[i];
                if (CAFactionAxes.StateOf(structure, def.Key)
                    != CAAxisSource.Unset) continue;
                string ideal = CAFactionAxes.KeyOf(beliefs?.positions, def.Key);
                bool followsIdeal = ideal != null
                    && Math.Abs((long)Gen.HashCombineInt(hash, i * 73 + 5))
                        % 100 < 76;
                string chosen = followsIdeal ? ideal : def.Options[(int)(
                    Math.Abs((long)Gen.HashCombineInt(hash, i * 73 + 19))
                    % def.Options.Length)].Key;
                CAFactionAxes.Set(structure, def.Key, chosen,
                    CAAxisSource.Generated);
                filled++;
            }
            return filled;
        }

        internal static List<string> Tensions(
            CAPoliticalBeliefs beliefs, List<CAAxisEntry> structure)
        {
            var result = new List<string>();
            if (beliefs == null || structure == null) return result;
            foreach (CAAxisDef def in CAFactionAxes.Axes)
            {
                string ideal = CAFactionAxes.KeyOf(beliefs.positions, def.Key);
                string actual = CAFactionAxes.KeyOf(structure, def.Key);
                if (ideal.NullOrEmpty() || actual.NullOrEmpty()
                    || ideal == actual) continue;
                CAAxisOption idealOption = def.Options.FirstOrDefault(item =>
                    item.Key == ideal);
                CAAxisOption actualOption = def.Options.FirstOrDefault(item =>
                    item.Key == actual);
                result.Add(def.Label + ": preferred "
                    + (idealOption?.Label ?? ideal) + "; current "
                    + (actualOption?.Label ?? actual) + ".");
            }
            return result;
        }

        internal static string Summary(List<CAAxisEntry> structure)
        {
            if (structure == null) return "Faction structure not set";
            string leadership = CAFactionAxes.OptionOf(structure,
                CAFactionAxes.Leadership)?.Label;
            string decisions = CAFactionAxes.OptionOf(structure,
                CAFactionAxes.Decisions)?.Label;
            string ownership = CAFactionAxes.OptionOf(structure,
                CAFactionAxes.Ownership)?.Label;
            return (leadership ?? "leadership not set") + " · "
                + (decisions ?? "decisions not set") + " · "
                + (ownership ?? "ownership not set");
        }
    }

    // Applies an optional native visual tradition only after the settlement
    // system has independently selected a physical object. Missing sources
    // fall back to that object's base style.
    internal static class CAVisualTraditionStyle
    {
        internal static CACulture CultureFor(
            CARegionalSettlementRecord settlement)
        {
            return CAFactionStateWorldComponent.Current
                ?.Find(settlement?.faction)?.culture;
        }

        internal static ThingStyleDef StyleFor(CACulture culture,
            ThingDef thing)
        {
            if (culture == null || thing == null) return null;
            CultureDef native = CACultureModel.NativeDef(culture);
            if (native?.thingStyleCategories == null) return null;
            foreach (ThingStyleCategoryWithPriority source in native
                .thingStyleCategories.Where(item => item?.category != null)
                .OrderByDescending(item => item.priority))
            {
                ThingStyleDef style = source.category
                    .GetStyleForThingDef(thing, null);
                if (style != null) return style;
            }
            return null;
        }
    }

    internal static class CAFactionStartingState
    {
        internal static List<CAAxisEntry> CopyAxes(List<CAAxisEntry> source)
        {
            var result = new List<CAAxisEntry>();
            if (source == null) return result;
            foreach (CAAxisEntry entry in source)
                if (entry != null)
                    result.Add(new CAAxisEntry
                    {
                        axisKey = entry.axisKey,
                        optionKey = entry.optionKey,
                        source = entry.source
                    });
            return result;
        }

        internal static CultureDef DefaultCulture(Faction faction,
            Ideo ideo = null)
        {
            if (ideo?.culture != null) return ideo.culture;
            CultureDef allowed = faction?.def?.allowedCultures?
                .FirstOrDefault(item => item != null);
            return allowed ?? DefDatabase<CultureDef>.AllDefsListForReading
                .FirstOrDefault();
        }

        internal static void EnsureFaction(CARegionalPlan plan,
            CARegionalFactionPlan group)
        {
            if (group == null) return;
            if (group.culture == null)
                group.culture = new CACulture();
            if (group.politicalBeliefs == null)
                group.politicalBeliefs = new CAPoliticalBeliefs();
            string seed = (plan?.candidateId ?? "ca-region") + ":faction:"
                + group.key;
            CACultureModel.EnsureGenerated(group.culture, seed,
                DefaultCulture(group.resolvedFaction, group.LivingIdeo)
                    ?? group.ResolvedFactionDef?.allowedCultures?
                        .FirstOrDefault());
            CAPoliticalBeliefsModel.Ensure(group.politicalBeliefs, seed);
            if (!group.institutionalStateIncomplete)
            {
                CAPoliticalBeliefsModel.GenerateUnset(group.politicalBeliefs,
                    seed + ":beliefs");
                CAFactionStructureModel.GenerateUnset(group.factionStructure,
                    group.politicalBeliefs, seed + ":structure");
            }
        }

        internal static void ApplyPlan(Faction faction,
            CACulture culture,
            CAPoliticalBeliefs beliefs, List<CAAxisEntry> structure)
        {
            CAFactionState record = CAFactionStateWorldComponent.Current
                ?.EnsureFor(faction);
            if (record == null) return;
            if (culture != null) record.culture = culture.Copy();
            if (beliefs != null)
                record.politicalBeliefs = beliefs.Copy();
            if (structure != null)
            {
                foreach (CAAxisEntry entry in structure)
                {
                    if (entry == null || entry.source
                        == (byte)CAAxisSource.Unset) continue;
                    if (CAFactionAxes.StateOf(record.factionStructure, entry.axisKey)
                        == CAAxisSource.Authored) continue;
                    CAFactionAxes.Set(record.factionStructure, entry.axisKey,
                        entry.optionKey,
                        (CAAxisSource)entry.source);
                }
            }
        }
    }

    // The shared editor changes either a descriptive established society or
    // the player's world-owned founding draft. Surface ownership differs;
    // The carried-background compatibility envelope and Political Beliefs
    // remain shared across founding and established societies.
    internal sealed class Dialog_CAAxisEditor : Window
    {
        private readonly CAPoliticalBeliefs beliefs;
        private readonly List<CAAxisEntry> structure;
        private readonly string seed;
        private readonly Action changed;
        private Vector2 scroll;
        private float viewHeight;
        private int groupIndex;
        private bool localExpanded;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1080f, UI.screenWidth - 48f),
            Mathf.Min(790f, UI.screenHeight - 48f));

        private Dialog_CAAxisEditor(CAPoliticalBeliefs beliefs,
            List<CAAxisEntry> structure, string seed, Action changed)
        {
            this.beliefs = beliefs;
            this.structure = structure;
            this.seed = seed;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        internal static Dialog_CAAxisEditor ForBeliefs(
            CAPoliticalBeliefs beliefs, string seed, Action changed)
        {
            return new Dialog_CAAxisEditor(beliefs, null, seed, changed);
        }

        internal static Dialog_CAAxisEditor ForStructure(
            List<CAAxisEntry> structure, CAPoliticalBeliefs beliefs,
            string seed, Action changed)
        {
            return new Dialog_CAAxisEditor(beliefs, structure, seed, changed);
        }

        public override void DoWindowContents(Rect inRect)
        {
            bool editingBeliefs = structure == null;
            GameFont old = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                editingBeliefs ? "Political beliefs"
                    : "Political beliefs and current structure");
            Text.Font = old;
            string description = editingBeliefs
                ? CAInformationPresentation.Select(
                    "Set what this population considers proper.",
                    "Set preferred government, participation, property, "
                        + "membership, support, and conflict positions.",
                    "These are normative beliefs. Existing institutions may "
                        + "agree or differ; landing terms are narrower still.",
                    localExpanded)
                : CAInformationPresentation.Select(
                    "Compare preferred positions with institutions in force.",
                    "Political beliefs state what should be proper. Current "
                        + "structure records what this faction actually does.",
                    "Each side is persisted and editable independently. "
                        + "Generated structure leans toward belief without "
                        + "being forced to match it.", localExpanded);
            float descriptionHeight = Text.CalcHeight(description,
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width,
                descriptionHeight), description);
            float y = 38f + descriptionHeight + 12f;
            y = DrawPoliticalActions(inRect, y, editingBeliefs);
            string[] tabs = new[] { "Overview" }.Concat(
                CAAuthoringChoices.PoliticalGroups.Select(item => item.Label))
                .ToArray();
            float tabHeight = CACreationUI.DrawSegmentRows(new Rect(0f, y,
                inRect.width, 30f), tabs, groupIndex, value =>
                {
                    groupIndex = value;
                    scroll = Vector2.zero;
                }, 165f);
            y += tabHeight + 10f;
            Rect outRect = new Rect(0f, y, inRect.width,
                inRect.height - y - 48f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Math.Max(viewHeight, outRect.height));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float rowY = 0f;
            if (groupIndex == 0)
                DrawPoliticalOverview(ref rowY, view.width, editingBeliefs);
            else
                DrawPoliticalGroup(ref rowY, view.width,
                    CAAuthoringChoices.PoliticalGroups[groupIndex - 1],
                    editingBeliefs);
            viewHeight = rowY + 8f;
            Widgets.EndScrollView();
            CAInformationPresentation.DrawLocalExpansion(new Rect(
                inRect.width - 160f, inRect.height - 38f, 160f, 28f),
                ref localExpanded);
        }

        private float DrawPoliticalActions(Rect inRect, float y,
            bool editingBeliefs)
        {
            var labels = new List<string> { "Profiles...", "Save profile...",
                "Manage saved...", "Generate missing" };
            var actions = new List<Action>
            {
                OpenPoliticalPresets, SavePoliticalProfile,
                () => Find.WindowStack.Add(new Dialog_CAProfileManager(
                    null, beliefs, changed)),
                () =>
                {
                    CAPoliticalBeliefsModel.GenerateUnset(beliefs,
                        seed + ":beliefs-fill");
                    if (!editingBeliefs)
                        CAFactionStructureModel.GenerateUnset(structure,
                            beliefs, seed + ":structure-fill");
                    changed?.Invoke();
                }
            };
            int columns = inRect.width >= 720f ? 4 : 2;
            float gap = 6f;
            float width = (inRect.width - gap * (columns - 1)) / columns;
            int rows = (labels.Count + columns - 1) / columns;
            for (int i = 0; i < labels.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                if (Widgets.ButtonText(new Rect(column * (width + gap),
                        y + row * 36f, width, 30f), labels[i]))
                    actions[i]();
            }
            return y + rows * 36f + 4f;
        }

        private void DrawPoliticalOverview(ref float y, float width,
            bool beliefsOnly)
        {
            string fingerprint = CAAuthoringChoices.PoliticalFingerprint(
                beliefs);
            string profile = CAAuthoringChoices.PoliticalIdentity(beliefs);
            DrawText(ref y, width, profile + "\n" + fingerprint,
                GameFont.Medium, Color.white);
            if (beliefsOnly)
            {
                string set = CAFactionAxes.CountByState(beliefs.positions,
                    CAAxisSource.Unset) == 0
                    ? "All thirteen positions are set."
                    : CAFactionAxes.CountByState(beliefs.positions,
                        CAAxisSource.Unset) + " positions remain unset.";
                DrawText(ref y, width, set, GameFont.Small,
                    new Color(0.72f, 0.76f, 0.81f));
            }
            else
            {
                List<string> tensions = CAFactionStructureModel.Tensions(
                    beliefs, structure);
                DrawText(ref y, width, tensions.Count == 0
                    ? "Belief and current structure are aligned on every set axis."
                    : tensions.Count + " institutional tensions", GameFont.Small,
                    tensions.Count == 0 ? CACreationUI.Authored
                        : ColorLibrary.Yellow);
                foreach (string tension in tensions)
                    DrawText(ref y, width, "• " + tension, GameFont.Small,
                        new Color(0.86f, 0.78f, 0.52f));
            }
            foreach (CAAxisGroupDef group in
                CAAuthoringChoices.PoliticalGroups)
            {
                string summary = string.Join(" · ", group.Axes.Select(axis =>
                    CAFactionAxes.OptionOf(beliefs.positions, axis)?.Label
                        ?? "unset").ToArray());
                DrawText(ref y, width, group.Label + ": " + summary,
                    GameFont.Small, new Color(0.78f, 0.81f, 0.85f));
            }
        }

        private void DrawPoliticalGroup(ref float y, float width,
            CAAxisGroupDef group, bool beliefsOnly)
        {
            if (!beliefsOnly)
            {
                DrawText(ref y, width, "Preferred position"
                    + new string(' ', 12) + "Current institution",
                    GameFont.Tiny, ColoredText.SubtleGrayColor);
            }
            foreach (string axisKey in group.Axes)
            {
                CAAxisDef def = CAFactionAxes.AxisDef(axisKey);
                DrawAxisRow(ref y, width, def, beliefsOnly);
            }
        }

        private void DrawAxisRow(ref float y, float width, CAAxisDef def,
            bool beliefsOnly)
        {
            Widgets.DrawAltRect(new Rect(0f, y, width, 1f));
            float labelHeight = Text.CalcHeight(def.Label + " — "
                + def.Question, width);
            Widgets.Label(new Rect(0f, y + 4f, width, labelHeight),
                def.Label + " — " + def.Question);
            y += labelHeight + 8f;
            if (beliefsOnly)
            {
                DrawPositionButton(ref y, width, def, beliefs.positions, true);
            }
            else
            {
                float half = (width - 12f) / 2f;
                float leftY = y;
                float rightY = y;
                DrawPositionButton(ref leftY, half, def, beliefs.positions,
                    true, 0f);
                DrawPositionButton(ref rightY, half, def, structure, false,
                    half + 12f);
                y = Mathf.Max(leftY, rightY);
                string preferred = CAFactionAxes.KeyOf(beliefs.positions,
                    def.Key);
                string current = CAFactionAxes.KeyOf(structure, def.Key);
                Color relation = preferred.NullOrEmpty()
                    || current.NullOrEmpty() ? CACreationUI.Unset
                    : preferred == current ? CACreationUI.Authored
                        : ColorLibrary.Yellow;
                DrawText(ref y, width, preferred.NullOrEmpty()
                        || current.NullOrEmpty() ? "Comparison incomplete"
                        : preferred == current ? "Aligned"
                            : "In tension: preferred "
                                + (CAFactionAxes.OptionOf(beliefs.positions,
                                    def.Key)?.Label ?? preferred)
                                + "; current "
                                + (CAFactionAxes.OptionOf(structure,
                                    def.Key)?.Label ?? current) + ".",
                    GameFont.Tiny, relation);
            }
            y += 8f;
        }

        private void DrawPositionButton(ref float y, float width,
            CAAxisDef def, List<CAAxisEntry> target, bool belief,
            float x = 0f)
        {
            CAAxisOption selected = CAFactionAxes.OptionOf(target, def.Key);
            CAAxisSource state = CAFactionAxes.StateOf(target, def.Key);
            Rect chip = new Rect(x, y + 5f, 84f, 20f);
            CACreationUI.DrawChip(chip, CACreationUI.SourceWords(state),
                CACreationUI.SourceColor(state));
            Rect value = new Rect(x + 90f, y, width - 90f, 30f);
            if (Widgets.ButtonText(value, selected?.Label.CapitalizeFirst()
                    ?? "Choose position"))
                OpenAxis(def, target, belief);
            y += 36f;
            if (CAInformationPresentation.Shows(CAInformationDetail.Standard,
                    localExpanded) && selected != null)
            {
                float words = Text.CalcHeight(selected.Words, width);
                GUI.color = new Color(0.72f, 0.76f, 0.81f);
                Widgets.Label(new Rect(x, y, width, words), selected.Words);
                GUI.color = Color.white;
                y += words + 4f;
            }
        }

        private static void DrawText(ref float y, float width, string value,
            GameFont font, Color color)
        {
            GameFont previous = Text.Font;
            Text.Font = font;
            float height = Text.CalcHeight(value, width);
            GUI.color = color;
            Widgets.Label(new Rect(0f, y, width, height), value);
            GUI.color = Color.white;
            Text.Font = previous;
            y += height + 8f;
        }

        private void OpenAxis(CAAxisDef def, List<CAAxisEntry> target,
            bool editingBeliefs)
        {
            var options = new List<CACreationChoice>();
            string current = CAFactionAxes.KeyOf(target, def.Key);
            foreach (CAAxisOption option in def.Options)
            {
                CAAxisOption local = option;
                options.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label.CapitalizeFirst(),
                    Summary = local.Words,
                    Details = def.Question,
                    Badge = current == local.Key ? "Current" : "Position",
                    Accent = current == local.Key
                        ? CACreationUI.Authored : CACreationUI.Accent,
                    Selected = current == local.Key,
                    ConfirmLabel = "Choose this position",
                    Choose = delegate
                    {
                        if (editingBeliefs)
                            CAPoliticalBeliefsModel.Author(beliefs,
                                def.Key, local.Key);
                        else
                            CAFactionAxes.Set(target, def.Key, local.Key,
                                CAAxisSource.Authored);
                        changed?.Invoke();
                    }
                });
            }
            options.Add(new CACreationChoice
            {
                Key = "__unset__",
                Name = "Leave unset",
                Summary = "No position is chosen. Generation may fill it "
                    + "later.",
                Details = def.Question,
                Badge = current == null ? "Current" : "Unset",
                Accent = CACreationUI.Unset,
                Selected = current == null,
                ConfirmLabel = "Leave this unset",
                Choose = delegate
                {
                    if (editingBeliefs)
                        CAPoliticalBeliefsModel.Release(beliefs, def.Key);
                    else
                        CAFactionAxes.Release(target, def.Key);
                    changed?.Invoke();
                }
            });
            CACreationUI.OpenChoices(def.Label, def.Question, options);
        }

        private void OpenPoliticalPresets()
        {
            CACreationUI.OpenChoices("Political profiles",
                "Apply a built-in or saved set of preferred positions. "
                    + "Every axis remains independently editable.",
                CAAuthoringChoices.PoliticalProfiles(beliefs, seed, changed));
        }

        private void SavePoliticalProfile()
        {
            string initial = CAFactionAxes.Preset(beliefs?.presetName)?.Name
                ?? "Saved political profile";
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Save political profile", initial, value =>
                {
                    CAUserPoliticalProfile saved =
                        CAAuthoringProfileLibrary.SavePolitics(value, beliefs);
                    if (saved != null)
                    {
                        beliefs.presetName = null;
                        beliefs.profileKey = saved.key;
                    }
                    changed?.Invoke();
                }));
        }
    }

    internal sealed class Dialog_CACultureEditor : Window
    {
        private readonly CACulture culture;
        private readonly string factionLabel;
        private readonly Action changed;
        private int section;
        private bool localExpanded;
        private Vector2 scroll;
        private float viewHeight;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(980f, UI.screenWidth - 48f),
            Mathf.Min(580f, UI.screenHeight - 48f));

        internal Dialog_CACultureEditor(CACulture culture,
            string factionLabel, Action changed)
        {
            this.culture = culture ?? new CACulture();
            this.factionLabel = factionLabel;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont previous = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "Cultural background");
            Text.Font = previous;
            float y = 38f;
            if (!factionLabel.NullOrEmpty())
            {
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f),
                    "Population: " + factionLabel);
                GUI.color = Color.white;
                y += 28f;
            }
            string description = CAInformationPresentation.Select(
                "Set the carried background and visual tradition.",
                "Set the population's carried background and optional native "
                    + "visual tradition. Settlements develop their own customs.",
                "This shared background may influence visual style. The people, "
                    + "their beliefs, local rule, land, work, trade, and history "
                    + "shape how it is lived in each settlement.",
                localExpanded);
            float descriptionHeight = Text.CalcHeight(description, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, descriptionHeight),
                description);
            y += descriptionHeight + 12f;

            y = DrawActions(inRect, y);
            string[] tabs = { "Background", "Visual tradition" };
            float tabHeight = CACreationUI.DrawSegmentRows(new Rect(0f, y,
                inRect.width, 30f), tabs, section, value =>
                {
                    section = value;
                    scroll = Vector2.zero;
                }, 180f);
            y += tabHeight + 10f;

            Rect outRect = new Rect(0f, y, inRect.width,
                inRect.height - y - 34f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float rowY = 0f;
            if (section == 0) DrawBackground(ref rowY, view.width);
            else DrawVisual(ref rowY, view.width);
            viewHeight = rowY + 12f;
            Widgets.EndScrollView();
            CAInformationPresentation.DrawLocalExpansion(new Rect(
                inRect.width - 160f, inRect.height - 30f, 160f, 28f),
                ref localExpanded);
        }

        private float DrawActions(Rect inRect, float y)
        {
            string[] labels = { "Background profiles...", "Save profile...",
                "Manage saved..." };
            Action[] actions = { OpenBackgroundProfiles, SaveProfile,
                () => Find.WindowStack.Add(new Dialog_CAProfileManager(
                    culture, null, changed)) };
            int columns = inRect.width >= 700f ? 3 : 2;
            float gap = 6f;
            float width = (inRect.width - gap * (columns - 1)) / columns;
            int rows = (labels.Length + columns - 1) / columns;
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;
                if (Widgets.ButtonText(new Rect(column * (width + gap),
                        y + row * 36f, width, 30f), labels[i]))
                    actions[i]();
            }
            return y + rows * 36f + 4f;
        }

        private void DrawBackground(ref float y, float width)
        {
            Row(ref y, width, "Name",
                culture.name ?? "Carried background",
                () => Find.WindowStack.Add(new Dialog_CARenameCulture(
                    culture, changed)), FieldState(CACulture.NameField));
            if (!culture.profileKey.NullOrEmpty())
            {
                CACreationUI.DrawChip(new Rect(0f, y + 3f,
                    Mathf.Min(width, 250f), 20f), "Saved background",
                    CACreationUI.Preset);
                y += 30f;
            }
            DrawExplanation(ref y, width,
                "This names the history and visual tradition the population "
                + "brings. Local customs develop after settlement.");
        }

        private void DrawVisual(ref float y, float width)
        {
            CultureDef native = CACultureModel.NativeDef(culture);
            string value = native?.LabelCap.ToString()
                ?? (culture.sourceCultureDefName.NullOrEmpty()
                    ? "Neutral fallback"
                    : culture.sourceCultureDefName
                        + " unavailable - neutral fallback");
            Row(ref y, width, "Native visual source", value,
                OpenSourceCulture, FieldState(CACulture.SourceCultureField));
            DrawExplanation(ref y, width,
                "This optional tradition supplies native RimWorld styles. "
                + "Neutral fallback uses the ordinary object style.");
        }

        private static void DrawExplanation(ref float y, float width,
            string text)
        {
            float height = Text.CalcHeight(text, width);
            GUI.color = new Color(0.72f, 0.76f, 0.81f);
            Widgets.Label(new Rect(0f, y, width, height), text);
            GUI.color = Color.white;
            y += height + 12f;
        }

        private static void Row(ref float y, float width, string label,
            string value, Action edit, CAAxisSource source)
        {
            bool stacked = width < 580f;
            float labelWidth = stacked ? width : Mathf.Min(250f, width * 0.34f);
            float labelHeight = Mathf.Max(24f, Text.CalcHeight(label,
                Mathf.Max(80f, labelWidth - 90f)));
            Widgets.Label(new Rect(0f, y + 4f, labelWidth - 90f,
                labelHeight), label);
            CACreationUI.DrawChip(new Rect(labelWidth - 84f, y + 5f,
                84f, 20f), CACreationUI.SourceWords(source),
                CACreationUI.SourceColor(source));
            Rect valueRect = stacked
                ? new Rect(0f, y + labelHeight + 4f, width, 32f)
                : new Rect(labelWidth + 8f, y, width - labelWidth - 8f,
                    Mathf.Max(32f, labelHeight));
            if (Widgets.ButtonText(valueRect, value ?? "Choose")) edit?.Invoke();
            y = valueRect.yMax + 8f;
        }

        private void OpenBackgroundProfiles()
        {
            CACreationUI.OpenChoices("Cultural backgrounds",
                "Choose a native visual tradition or a saved background. "
                    + "Local practices remain derived at each settlement.",
                CAAuthoringChoices.CultureProfiles(culture,
                    culture.id ?? factionLabel ?? "ca-culture", changed));
        }

        private void SaveProfile()
        {
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Save background profile",
                culture.name ?? "Saved background", value =>
                {
                    CAUserCultureProfile saved =
                        CAAuthoringProfileLibrary.SaveCulture(value, culture);
                    if (saved != null)
                    {
                        culture.presetName = null;
                        culture.profileKey = saved.key;
                    }
                    changed?.Invoke();
                }));
        }

        private void OpenSourceCulture()
        {
            var options = new List<CACreationChoice>();
            options.Add(new CACreationChoice
            {
                Key = "neutral",
                Name = "Neutral fallback",
                Summary = "Use base object styles when no native source applies.",
                Badge = "Fallback",
                Accent = CACreationUI.Generated,
                Selected = culture.sourceCultureDefName.NullOrEmpty(),
                ConfirmLabel = "Use neutral fallback",
                Choose = delegate
                {
                    // Null plus authored provenance means an explicit neutral
                    // choice. EnsureGenerated only fills genuinely unset state.
                    culture.Choose(CACulture.SourceCultureField, null);
                    changed?.Invoke();
                }
            });
            foreach (CultureDef def in DefDatabase<CultureDef>
                .AllDefsListForReading.OrderBy(item => item.label))
            {
                CultureDef local = def;
                options.Add(new CACreationChoice
                {
                    Key = local.defName,
                    Name = local.LabelCap.ToString(),
                    Summary = local.description.NullOrEmpty()
                        ? "Native style categories and visual tradition."
                        : local.description,
                    Badge = "Style source",
                    Icon = local.Icon,
                    Accent = CACreationUI.Accent,
                    Selected = culture.sourceCultureDefName == local.defName,
                    ConfirmLabel = "Use this visual source",
                    Choose = delegate
                    {
                        culture.Choose(CACulture.SourceCultureField,
                            local.defName);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices("Visual tradition",
                "Choose an optional native style source. This does not define "
                    + "belief, institutions, facilities, or local practice.",
                options);
        }

        private CAAxisSource FieldState(int field)
        {
            if (culture.Authored(field)) return CAAxisSource.Authored;
            if (!culture.Value(field).NullOrEmpty())
                return CAAxisSource.Generated;
            return CAAxisSource.Unset;
        }
    }


    internal sealed class Dialog_CARenameCulture : Window
    {
        private readonly CACulture culture;
        private readonly Action changed;
        private string value;

        public override Vector2 InitialSize => new Vector2(520f, 190f);

        internal Dialog_CARenameCulture(CACulture culture,
            Action changed)
        {
            this.culture = culture;
            this.changed = changed;
            value = culture?.name ?? "";
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f),
                "Cultural background name");
            value = Widgets.TextField(new Rect(0f, 40f, inRect.width, 30f),
                value);
            if (Widgets.ButtonText(new Rect(inRect.width - 120f, 88f,
                    120f, 32f), "Save"))
            {
                if (!value.NullOrEmpty())
                    culture.Choose(CACulture.NameField, value.Trim());
                changed?.Invoke();
                Close();
            }
        }
    }

}
