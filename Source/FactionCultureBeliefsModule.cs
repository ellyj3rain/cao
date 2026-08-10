using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Population culture is separate from Ideoligion and faction affiliation.
    // A native CultureDef supplies style categories without owning the rest of
    // these customs.
    public sealed class CACulture : IExposable
    {
        public const int NameField = 1;
        public const int SourceCultureField = 2;
        public const int GatheringField = 8;
        public string id;
        public string name;
        public string sourceCultureDefName;
        public string gatheringKey;
        public string presetName;
        public int authoredMask;
        public int presetMask;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref sourceCultureDefName,
                "sourceCultureDefName");
            Scribe_Values.Look(ref gatheringKey, "gatheringKey");
            Scribe_Values.Look(ref presetName, "presetName");
            Scribe_Values.Look(ref authoredMask, "authoredMask", 0);
            Scribe_Values.Look(ref presetMask, "presetMask", 0);
        }

        internal CACulture Copy()
        {
            return new CACulture
            {
                id = id,
                name = name,
                sourceCultureDefName = sourceCultureDefName,
                gatheringKey = gatheringKey,
                presetName = presetName,
                authoredMask = authoredMask,
                presetMask = presetMask
            };
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
            if (!CACultureModel.PresetStillDescribes(this))
                presetName = null;
        }

        internal void Set(int field, string value)
        {
            switch (field)
            {
                case NameField: name = value; break;
                case SourceCultureField:
                    sourceCultureDefName = value; break;
                case GatheringField: gatheringKey = value; break;
            }
        }

        internal string Value(int field)
        {
            switch (field)
            {
                case NameField: return name;
                case SourceCultureField: return sourceCultureDefName;
                case GatheringField: return gatheringKey;
                default: return null;
            }
        }
    }

    // Political beliefs and faction structure use the same questions so a
    // disagreement is readable without collapsing the two persisted states.
    public sealed class CAPoliticalBeliefs : IExposable
    {
        public string id;
        public string presetName;
        public List<CAAxisEntry> positions = new List<CAAxisEntry>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref presetName, "presetName");
            Scribe_Collections.Look(ref positions, "positions", LookMode.Deep);
            if (positions == null) positions = new List<CAAxisEntry>();
        }

        internal CAPoliticalBeliefs Copy()
        {
            return new CAPoliticalBeliefs
            {
                id = id,
                presetName = presetName,
                positions = CAFactionStartingState.CopyAxes(positions)
            };
        }
    }

    internal sealed class CACulturePreset
    {
        internal string Name;
        internal string Description;
        internal string Gathering;
        internal string IconPath;
    }

    internal static class CACultureModel
    {
        internal static readonly CACulturePreset[] Presets =
        {
            new CACulturePreset
            {
                Name = "Hearth customs",
                Description = "Shared meals and meetings gather around the "
                    + "settlement hearth.",
                Gathering = "hearth",
                IconPath = "Rimshare/WorldMapIcons/flowers"
            },
            new CACulturePreset
            {
                Name = "Traveling customs",
                Description = "Gatherings are held at roads, camps, and "
                    + "meeting places used by travelers.",
                Gathering = "waymeet",
                IconPath = "Rimshare/WorldMapIcons/compass"
            },
            new CACulturePreset
            {
                Name = "Memorial customs",
                Description = "Public memory and memorial gatherings anchor "
                    + "community life.",
                Gathering = "memorial",
                IconPath = "Rimshare/WorldMapIcons/feather"
            },
            new CACulturePreset
            {
                Name = "Festival customs",
                Description = "Communal feasts and festivals mark shared "
                    + "occasions.",
                Gathering = "feast",
                IconPath = "Rimshare/WorldMapIcons/carnival-mask"
            }
        };

        internal static readonly string[] GatheringKeys =
        { "hearth", "feast", "waymeet", "memorial" };
        internal static void EnsureGenerated(CACulture culture,
            string seed, CultureDef fallback)
        {
            if (culture == null) return;
            int hash = GenText.StableStringHash(seed ?? "ca-culture");
            if (culture.id.NullOrEmpty())
                culture.id = "culture:" + Math.Abs((long)hash);
            if (culture.sourceCultureDefName.NullOrEmpty() && fallback != null)
                culture.sourceCultureDefName = fallback.defName;
            if (culture.name.NullOrEmpty())
                culture.name = fallback != null
                    ? fallback.LabelCap + " customs"
                    : "Local customs";
            Fill(ref culture.gatheringKey, GatheringKeys, hash, 17);
        }

        private static void Fill(ref string value, string[] choices,
            int hash, int salt)
        {
            if (!value.NullOrEmpty()) return;
            int index = (int)(Math.Abs((long)Gen.HashCombineInt(hash, salt))
                % choices.Length);
            value = choices[index];
        }

        internal static void ApplyPreset(CACulture culture,
            CACulturePreset preset)
        {
            if (culture == null || preset == null) return;
            culture.presetName = preset.Name;
            Apply(culture, CACulture.GatheringField,
                preset.Gathering);
        }

        private static void Apply(CACulture culture, int field,
            string value)
        {
            if (culture.Authored(field)) return;
            culture.Set(field, value);
            culture.presetMask |= field;
        }

        internal static bool PresetStillDescribes(CACulture culture)
        {
            if (culture == null || culture.presetName.NullOrEmpty())
                return true;
            CACulturePreset preset = Presets.FirstOrDefault(item =>
                item.Name == culture.presetName);
            if (preset == null) return false;
            return culture.gatheringKey == preset.Gathering;
        }

        internal static string Words(string domain, string key)
        {
            if (domain == "gathering")
            {
                if (key == "feast") return "communal feasts";
                if (key == "waymeet") return "meetings along the way";
                if (key == "memorial") return "memorial gatherings";
                return "the shared hearth";
            }
            return key ?? "not set";
        }

        internal static string Summary(CACulture culture)
        {
            if (culture == null) return "Culture not set";
            CultureDef source = NativeDef(culture);
            return (source?.LabelCap.ToString() ?? "Generated")
                + " styles · "
                + Words("gathering", culture.gatheringKey);
        }

        internal static Texture2D Icon(CACulture culture)
        {
            CACulturePreset preset = Presets.FirstOrDefault(item =>
                item.Name == culture?.presetName);
            if (preset != null)
                return ContentFinder<Texture2D>.Get(preset.IconPath);
            CultureDef native = NativeDef(culture);
            if (native != null && !native.iconPath.NullOrEmpty())
                return native.Icon;
            return ContentFinder<Texture2D>.Get(
                "Rimshare/WorldMapIcons/feather");
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
        internal static void Ensure(CAPoliticalBeliefs beliefs,
            string seed)
        {
            if (beliefs == null) return;
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
            beliefs.presetName = preset.Name;
        }

        private static void ApplyPresetLayer(
            CAPoliticalBeliefs beliefs,
            CAFactionAxes.PoliticalPreset preset)
        {
            if (preset.Parent != null)
            {
                CAFactionAxes.PoliticalPreset parent = CAFactionAxes.Presets
                    .FirstOrDefault(item => item.Name == preset.Parent);
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
            if (!PresetStillDescribes(beliefs))
                beliefs.presetName = null;
        }

        private static bool PresetStillDescribes(
            CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null
                || beliefs.presetName.NullOrEmpty()) return true;
            CAFactionAxes.PoliticalPreset preset = CAFactionAxes.Presets
                .FirstOrDefault(item => item.Name
                    == beliefs.presetName);
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
                CAFactionAxes.PoliticalPreset parent = CAFactionAxes.Presets
                    .FirstOrDefault(item => item.Name == preset.Parent);
                if (parent != null) CollectPreset(parent, expected);
            }
            foreach (KeyValuePair<string, string> pair in preset.Positions)
                expected[pair.Key] = pair.Value;
        }

        internal static string Summary(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "Political beliefs not set";
            string preset = beliefs.presetName.NullOrEmpty()
                ? "Custom political beliefs"
                : beliefs.presetName;
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
            if (preset == null) return "No preset";
            var expected = new Dictionary<string, string>();
            CollectPreset(preset, expected);
            return string.Join(", ", CAFactionAxes.Axes
                .Where(axis => expected.ContainsKey(axis.Key))
                .Take(5)
                .Select(axis => axis.Label + ": "
                    + (axis.Options.FirstOrDefault(option => option.Key
                        == expected[axis.Key])?.Label ?? expected[axis.Key]))
                .ToArray());
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

    // Applies the faction's culture to settlement objects independently of
    // its Ideoligion.
    internal static class CACultureMaterialization
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

        internal static void Furnish(CARegionalSettlementRecord record,
            Func<Room> next, Func<Room, string, string, int> place)
        {
            CACulture culture = CultureFor(record);
            if (culture == null || next == null || place == null)
                return;
            string defName;
            string stuff = null;
            switch (culture.gatheringKey)
            {
                case "feast":
                    defName = "Table3x3c";
                    stuff = "WoodLog";
                    break;
                case "waymeet":
                    defName = "HorseshoesPin";
                    break;
                case "memorial":
                    defName = "SculptureSmall";
                    stuff = "BlocksGranite";
                    break;
                default:
                    defName = "Campfire";
                    break;
            }
            int laid = place(next(), defName, stuff);
            if (laid > 0)
                Log.Message("[CA][Culture] " + (record.name ?? "settlement")
                    + " materialized " + culture.gatheringKey
                    + " custom for " + (culture.name ?? "its culture")
                    + " as " + defName + ".");
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
    // Culture and Political Beliefs remain the same models.
    internal sealed class Dialog_CAAxisEditor : Window
    {
        private readonly CAPoliticalBeliefs beliefs;
        private readonly List<CAAxisEntry> structure;
        private readonly string seed;
        private readonly Action changed;
        private Vector2 scroll;
        private float viewHeight;

        public override Vector2 InitialSize => new Vector2(840f, 720f);

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
                    : "Faction structure");
            Text.Font = old;
            string description = editingBeliefs
                ? "Preferred leadership, ownership, law, support, and "
                    + "membership. Presets remain editable."
                : "Current leadership, ownership, law, support, and "
                    + "membership. May differ from political beliefs.";
            float descriptionHeight = Text.CalcHeight(description,
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width,
                descriptionHeight), description);
            float y = 38f + descriptionHeight + 12f;
            float buttonWidth = (inRect.width - 12f) / 2f;
            if (editingBeliefs)
            {
                if (Widgets.ButtonText(new Rect(0f, y, buttonWidth, 30f),
                        "Choose preset..."))
                    OpenPoliticalPresets();
                if (Widgets.ButtonText(new Rect(buttonWidth + 12f, y,
                        buttonWidth, 30f), "Generate unset beliefs"))
                {
                    CAPoliticalBeliefsModel.GenerateUnset(beliefs,
                        seed + ":fill");
                    changed?.Invoke();
                }
            }
            else
            {
                if (Widgets.ButtonText(new Rect(0f, y, inRect.width, 30f),
                        "Generate unset structure"))
                {
                    CAFactionStructureModel.GenerateUnset(structure, beliefs,
                        seed + ":structure-fill");
                    changed?.Invoke();
                }
            }
            y += 40f;
            Rect outRect = new Rect(0f, y, inRect.width,
                inRect.height - y - 48f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Math.Max(viewHeight, outRect.height));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float rowY = 0f;
            List<CAAxisEntry> target = editingBeliefs
                ? beliefs.positions : structure;
            foreach (CAAxisDef def in CAFactionAxes.Axes)
            {
                Widgets.Label(new Rect(0f, rowY + 3f, 205f, 28f),
                    def.Label);
                CAAxisOption selected = CAFactionAxes.OptionOf(target,
                    def.Key);
                Rect valueRect = new Rect(210f, rowY,
                    view.width - 210f, 30f);
                if (Widgets.ButtonText(valueRect, selected?.Label
                        .CapitalizeFirst() ?? "Not set"))
                    OpenAxis(def, target, editingBeliefs);
                TooltipHandler.TipRegion(valueRect, def.Question
                    + (selected == null ? "" : "\n\n" + selected.Words));
                rowY += 38f;
            }
            viewHeight = rowY + 8f;
            Widgets.EndScrollView();
        }

        private void OpenAxis(CAAxisDef def, List<CAAxisEntry> target,
            bool editingBeliefs)
        {
            var options = new List<FloatMenuOption>();
            string current = CAFactionAxes.KeyOf(target, def.Key);
            foreach (CAAxisOption option in def.Options)
            {
                CAAxisOption local = option;
                options.Add(new FloatMenuOption(
                    (current == local.Key ? "✓ " : "")
                    + local.Label.CapitalizeFirst() + ": " + local.Words,
                    delegate
                    {
                        if (editingBeliefs)
                            CAPoliticalBeliefsModel.Author(beliefs,
                                def.Key, local.Key);
                        else
                            CAFactionAxes.Set(target, def.Key, local.Key,
                                CAAxisSource.Authored);
                        changed?.Invoke();
                    }));
            }
            options.Add(new FloatMenuOption(
                "Clear selection", delegate
                {
                    CAFactionAxes.Release(target, def.Key);
                    changed?.Invoke();
                }));
            Verse.Find.WindowStack.Add(new FloatMenu(options));
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
                    CAPoliticalBeliefsModel.ApplyPreset(beliefs, local);
                    changed?.Invoke();
                }));
            }
            Verse.Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    internal sealed class Dialog_CACultureEditor : Window
    {
        private readonly CACulture culture;
        private readonly string factionLabel;
        private readonly Action changed;

        public override Vector2 InitialSize => new Vector2(720f, 570f);

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
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), "Culture");
            Text.Font = previous;
            float y = 38f;
            if (!factionLabel.NullOrEmpty())
            {
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f),
                    "Faction: " + factionLabel);
                GUI.color = Color.white;
                y += 28f;
            }
            string description = "Culture controls styles and the gathering "
                + "place. Ideoligion controls rituals.";
            float descriptionHeight = Text.CalcHeight(description,
                inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width,
                descriptionHeight), description);
            y += descriptionHeight + 12f;

            Row(ref y, inRect.width, "Name",
                culture.name ?? "Local customs", delegate
                {
                    Verse.Find.WindowStack.Add(new Dialog_CARenameCulture(
                        culture, changed));
                });
            CultureDef native = CACultureModel.NativeDef(culture);
            Row(ref y, inRect.width, "Source culture",
                native?.LabelCap.ToString() ?? "None", OpenSourceCulture);
            Row(ref y, inRect.width, "Preset",
                culture.presetName ?? "Custom culture",
                OpenCulturePreset);
            Row(ref y, inRect.width, "Gathering place",
                CACultureModel.Words("gathering", culture.gatheringKey),
                () => OpenField(CACulture.GatheringField,
                    "gathering", CACultureModel.GatheringKeys));
            Rect generate = new Rect(0f, y + 4f, 220f, 30f);
            if (Widgets.ButtonText(generate, "Generate unset choices"))
            {
                CACultureModel.EnsureGenerated(culture,
                    (culture.id ?? factionLabel ?? "ca-culture") + ":fill",
                    CACultureModel.NativeDef(culture));
                changed?.Invoke();
            }
            TooltipHandler.TipRegion(generate, "Generates culture fields that "
                + "are not set. Existing choices remain unchanged.");
        }

        private static void Row(ref float y, float width, string label,
            string value, Action edit)
        {
            const float labelWidth = 210f;
            Widgets.Label(new Rect(0f, y + 5f, labelWidth - 10f, 28f),
                label);
            if (Widgets.ButtonText(new Rect(labelWidth, y,
                    width - labelWidth, 30f), value ?? "Not set"))
                edit?.Invoke();
            y += 38f;
        }

        private void OpenField(int field, string domain, string[] choices)
        {
            var options = new List<FloatMenuOption>();
            string current = culture.Value(field);
            foreach (string choice in choices)
            {
                string local = choice;
                options.Add(new FloatMenuOption(
                    (current == local ? "✓ " : "")
                    + CACultureModel.Words(domain, local), delegate
                    {
                        culture.Choose(field, local);
                        changed?.Invoke();
                    }));
            }
            Verse.Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenCulturePreset()
        {
            var options = new List<FloatMenuOption>();
            foreach (CACulturePreset preset in CACultureModel.Presets)
            {
                CACulturePreset local = preset;
                options.Add(new FloatMenuOption(local.Name + ": "
                    + local.Description, delegate
                {
                    CACultureModel.ApplyPreset(culture, local);
                    changed?.Invoke();
                }, ContentFinder<Texture2D>.Get(local.IconPath),
                    Color.white));
            }
            Verse.Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenSourceCulture()
        {
            var options = new List<FloatMenuOption>();
            foreach (CultureDef def in DefDatabase<CultureDef>
                .AllDefsListForReading.OrderBy(item => item.label))
            {
                CultureDef local = def;
                options.Add(new FloatMenuOption(local.LabelCap, delegate
                {
                    culture.Choose(CACulture.SourceCultureField,
                        local.defName);
                    changed?.Invoke();
                }, local.Icon, Color.white));
            }
            Verse.Find.WindowStack.Add(new FloatMenu(options));
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
                "Culture name");
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
