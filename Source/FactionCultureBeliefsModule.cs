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
        public const int CurrentSchemaVersion = 2;
        public const int NameField = 1;
        public const int SourceCultureField = 2;
        public const int GatheringField = 8;
        public const int HospitalityField = 16;
        public const int MealsField = 32;
        public const int RemembranceField = 64;
        public int schemaVersion = CurrentSchemaVersion;
        public string id;
        public string name;
        public string sourceCultureDefName;
        public string gatheringKey;
        public string hospitalityKey;
        public string mealsKey;
        public string remembranceKey;
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
                schemaVersion = schemaVersion,
                id = id,
                name = name,
                sourceCultureDefName = sourceCultureDefName,
                gatheringKey = gatheringKey,
                hospitalityKey = hospitalityKey,
                mealsKey = mealsKey,
                remembranceKey = remembranceKey,
                presetName = presetName,
                profileKey = profileKey,
                authoredMask = authoredMask,
                presetMask = presetMask
            };
        }

        internal void CopyFrom(CACulture source)
        {
            if (source == null) return;
            schemaVersion = CurrentSchemaVersion;
            id = source.id;
            name = source.name;
            sourceCultureDefName = source.sourceCultureDefName;
            gatheringKey = source.gatheringKey;
            hospitalityKey = source.hospitalityKey;
            mealsKey = source.mealsKey;
            remembranceKey = source.remembranceKey;
            presetName = source.presetName;
            profileKey = source.profileKey;
            authoredMask = source.authoredMask;
            presetMask = source.presetMask;
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
                case GatheringField: gatheringKey = value; break;
                case HospitalityField: hospitalityKey = value; break;
                case MealsField: mealsKey = value; break;
                case RemembranceField: remembranceKey = value; break;
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
                case GatheringField: return gatheringKey;
                case HospitalityField: return hospitalityKey;
                case MealsField: return mealsKey;
                case RemembranceField: return remembranceKey;
                default: return null;
            }
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

    internal sealed class CACultureOptionDef
    {
        internal string Key;
        internal string Label;
        internal string Summary;
        internal string Consumer;
        internal string ThingDefName;
        internal string StuffDefName;
    }

    internal sealed class CACultureDomainDef
    {
        internal int Field;
        internal string Key;
        internal string Label;
        internal string Question;
        internal CACultureOptionDef[] Options;
    }

    internal sealed class CACulturePreset
    {
        internal string Key;
        internal string Label;
        internal string[] Aliases;
        internal string Description;
        internal string IconPath;
        internal Dictionary<int, string> Positions =
            new Dictionary<int, string>();
    }

    internal static class CACultureModel
    {
        internal static readonly CACultureDomainDef[] Domains =
        {
            new CACultureDomainDef
            {
                Field = CACulture.GatheringField, Key = "gathering",
                Label = "Public gathering",
                Question = "Where do people normally gather in public?",
                Options = new[]
                {
                    OptionDef("hearth", "Shared hearth",
                        "Meetings gather around a common fire.",
                        "Adds a communal hearth.", "Campfire"),
                    OptionDef("feast", "Festival ground",
                        "Large shared meals anchor public occasions.",
                        "Adds a large communal table.", "Table3x3c", "WoodLog"),
                    OptionDef("waymeet", "Roadside meeting place",
                        "Travel routes and open meeting grounds anchor gatherings.",
                        "Adds an outdoor meeting game.", "HorseshoesPin"),
                    OptionDef("memorial", "Memorial ground",
                        "Public remembrance provides the usual meeting place.",
                        "Adds a carved public memorial.", "SculptureSmall",
                        "BlocksGranite")
                }
            },
            new CACultureDomainDef
            {
                Field = CACulture.HospitalityField, Key = "hospitality",
                Label = "Hospitality",
                Question = "What space is normally kept for visitors?",
                Options = new[]
                {
                    OptionDef("guest_bedding", "Guest bedding",
                        "Visitors are offered a place to sleep.",
                        "Adds a guest bedroll.", "Bedroll", "Cloth"),
                    OptionDef("open_seating", "Open seating",
                        "Simple seats are kept for whoever arrives.",
                        "Adds a plain visitor seat.", "Stool", "WoodLog"),
                    OptionDef("hosted_seating", "Hosted seating",
                        "Visitors are received at a prepared seat.",
                        "Adds a dining chair for receiving guests.",
                        "DiningChair", "WoodLog"),
                    OptionDef("private_receiving", "Private receiving place",
                        "Guests are received in a quieter furnished place.",
                        "Adds a comfortable receiving chair.", "Armchair", "Cloth")
                }
            },
            new CACultureDomainDef
            {
                Field = CACulture.MealsField, Key = "meals",
                Label = "Shared meals",
                Question = "How are ordinary shared meals arranged?",
                Options = new[]
                {
                    OptionDef("household_tables", "Household tables",
                        "Small groups normally eat together.",
                        "Adds a small dining table.", "Table2x2c", "WoodLog"),
                    OptionDef("common_table", "Common table",
                        "The larger group normally eats together.",
                        "Adds a large dining table.", "Table3x3c", "WoodLog"),
                    OptionDef("fireside_meals", "Fireside meals",
                        "Food is commonly shared around an open fire.",
                        "Adds a cooking and eating fire.", "Campfire")
                }
            },
            new CACultureDomainDef
            {
                Field = CACulture.RemembranceField, Key = "remembrance",
                Label = "Public memory",
                Question = "How is shared history marked in settlement space?",
                Options = new[]
                {
                    OptionDef("carved_memory", "Carved memorials",
                        "Shared history is marked with public art.",
                        "Adds a small stone sculpture.", "SculptureSmall",
                        "BlocksGranite"),
                    OptionDef("burial_memory", "Burial memorials",
                        "The dead and the past are marked at a formal tomb.",
                        "Adds a stone sarcophagus.", "Sarcophagus",
                        "BlocksGranite"),
                    OptionDef("assembly_marker", "Assembly marker",
                        "Public occasions themselves keep memory alive.",
                        "Adds a designated gathering place.", "PartySpot")
                }
            }
        };

        private static CACultureOptionDef OptionDef(string key,
            string label, string summary, string consumer, string thingDef,
            string stuffDef = null)
        {
            return new CACultureOptionDef
            {
                Key = key, Label = label, Summary = summary,
                Consumer = consumer, ThingDefName = thingDef,
                StuffDefName = stuffDef
            };
        }

        private static Dictionary<int, string> ProfilePositions(
            string visualStyle, string gathering, string hospitality,
            string meals, string remembrance)
        {
            return new Dictionary<int, string>
            {
                { CACulture.SourceCultureField, visualStyle },
                { CACulture.GatheringField, gathering },
                { CACulture.HospitalityField, hospitality },
                { CACulture.MealsField, meals },
                { CACulture.RemembranceField, remembrance }
            };
        }
        internal static readonly CACulturePreset[] Presets =
        {
            new CACulturePreset
            {
                Key = "hearth_common", Label = "Hearth commons",
                Aliases = new[] { "Hearth customs" },
                Description = "A settled communal tradition centered on a "
                    + "shared hearth, common table, guest bedding, and carved memory.",
                Positions = ProfilePositions("Rustican", "hearth",
                    "guest_bedding", "common_table", "carved_memory"),
                IconPath = "Rimshare/WorldMapIcons/flowers"
            },
            new CACulturePreset
            {
                Key = "road_exchange", Label = "Road exchange",
                Aliases = new[] { "Traveling customs" },
                Description = "An itinerant tradition of roadside meetings, "
                    + "open hospitality, fireside meals, and remembered assemblies.",
                Positions = ProfilePositions("Corunan", "waymeet",
                    "open_seating", "fireside_meals", "assembly_marker"),
                IconPath = "Rimshare/WorldMapIcons/compass"
            },
            new CACulturePreset
            {
                Key = "memorial_households", Label = "Memorial households",
                Aliases = new[] { "Memorial customs" },
                Description = "Household meals and private hospitality gather "
                    + "around memorial grounds and formal remembrance.",
                Positions = ProfilePositions("Sophian", "memorial",
                    "private_receiving", "household_tables", "burial_memory"),
                IconPath = "Rimshare/WorldMapIcons/feather"
            },
            new CACulturePreset
            {
                Key = "festival_market", Label = "Festival market",
                Aliases = new[] { "Festival customs" },
                Description = "Large feasts, prepared hospitality, common "
                    + "tables, and recurring assemblies shape public life.",
                Positions = ProfilePositions("Astropolitan", "feast",
                    "hosted_seating", "common_table", "assembly_marker"),
                IconPath = "Rimshare/WorldMapIcons/carnival-mask"
            }
        };

        internal static CACultureDomainDef Domain(string key)
        {
            return Domains.FirstOrDefault(item => item.Key == key);
        }

        internal static CACultureDomainDef Domain(int field)
        {
            return Domains.FirstOrDefault(item => item.Field == field);
        }

        internal static string[] Keys(string domain)
        {
            return Domain(domain)?.Options.Select(item => item.Key).ToArray()
                ?? new string[0];
        }

        internal static CACultureOptionDef Option(int field, string key)
        {
            return Domain(field)?.Options.FirstOrDefault(item => item.Key == key);
        }

        private static string StableOptionKey(CACultureDomainDef domain,
            string value)
        {
            if (domain == null || value.NullOrEmpty()) return value;
            CACultureOptionDef direct = domain.Options.FirstOrDefault(item =>
                item.Key == value);
            if (direct != null) return direct.Key;
            CACultureOptionDef byLabel = domain.Options.FirstOrDefault(item =>
                string.Equals(item.Label, value,
                    StringComparison.OrdinalIgnoreCase));
            if (byLabel != null) return byLabel.Key;
            if (domain.Field == CACulture.GatheringField
                && string.Equals(value, "meetings along the way",
                    StringComparison.OrdinalIgnoreCase))
                return "waymeet";
            return value;
        }

        internal static string CompatibilityFailure(CACulture culture)
        {
            if (culture == null) return "No culture is recorded.";
            if (!culture.sourceCultureDefName.NullOrEmpty()
                && NativeDef(culture) == null)
                return "Visual style source '"
                    + culture.sourceCultureDefName + "' is unavailable.";
            foreach (CACultureDomainDef domain in Domains)
            {
                string key = culture.Value(domain.Field);
                if (key.NullOrEmpty())
                    return domain.Label + " has not been chosen.";
                if (Option(domain.Field, key) == null)
                    return domain.Label + " uses unavailable practice '"
                        + key + "'.";
            }
            return null;
        }

        internal static CACulturePreset Preset(string keyOrAlias)
        {
            if (keyOrAlias.NullOrEmpty()) return null;
            return Presets.FirstOrDefault(item => item.Key == keyOrAlias
                || item.Label == keyOrAlias
                || (item.Aliases?.Contains(keyOrAlias) ?? false));
        }

        internal static void Migrate(CACulture culture)
        {
            if (culture == null) return;
            CACulturePreset preset = Preset(culture.presetName);
            if (preset != null)
            {
                culture.presetName = preset.Key;
                foreach (KeyValuePair<int, string> pair in preset.Positions)
                {
                    if (!culture.Value(pair.Key).NullOrEmpty()) continue;
                    culture.Set(pair.Key, pair.Value);
                    culture.presetMask |= pair.Key;
                }
            }
            foreach (CACultureDomainDef domain in Domains)
            {
                string value = culture.Value(domain.Field);
                string stable = StableOptionKey(domain, value);
                if (stable != value) culture.Set(domain.Field, stable);
            }
            culture.schemaVersion = CACulture.CurrentSchemaVersion;
        }

        internal static void EnsureGenerated(CACulture culture,
            string seed, CultureDef fallback)
        {
            if (culture == null) return;
            Migrate(culture);
            int hash = GenText.StableStringHash(seed ?? "ca-culture");
            if (culture.id.NullOrEmpty())
                culture.id = "culture:" + Math.Abs((long)hash);
            if (culture.sourceCultureDefName.NullOrEmpty() && fallback != null)
                culture.sourceCultureDefName = fallback.defName;
            if (culture.name.NullOrEmpty())
                culture.name = fallback != null
                    ? fallback.LabelCap + " customs"
                    : "Local customs";
            for (int i = 0; i < Domains.Length; i++)
            {
                CACultureDomainDef domain = Domains[i];
                if (!culture.Value(domain.Field).NullOrEmpty()) continue;
                int index = (int)(Math.Abs((long)Gen.HashCombineInt(hash,
                    17 + i * 43)) % domain.Options.Length);
                culture.Set(domain.Field, domain.Options[index].Key);
            }
        }

        internal static void ApplyPreset(CACulture culture,
            CACulturePreset preset)
        {
            if (culture == null || preset == null) return;
            culture.presetName = preset.Key;
            culture.profileKey = null;
            foreach (KeyValuePair<int, string> pair in preset.Positions)
            {
                culture.Set(pair.Key, pair.Value);
                culture.authoredMask &= ~pair.Key;
                culture.presetMask |= pair.Key;
            }
        }

        internal static bool PresetStillDescribes(CACulture culture)
        {
            if (culture == null || culture.presetName.NullOrEmpty())
                return true;
            CACulturePreset preset = Preset(culture.presetName);
            if (preset == null) return false;
            return preset.Positions.All(pair =>
                culture.Value(pair.Key) == pair.Value);
        }

        internal static bool UsesPreset(CACulture culture,
            CACulturePreset preset)
        {
            return culture != null && preset != null
                && Preset(culture.presetName)?.Key == preset.Key
                && PresetStillDescribes(culture);
        }

        internal static string Words(string domain, string key)
        {
            CACultureDomainDef def = Domain(domain);
            return def?.Options.FirstOrDefault(item => item.Key == key)?.Label
                ?? (key.NullOrEmpty() ? "No custom chosen" : key);
        }

        internal static string Summary(CACulture culture)
        {
            if (culture == null) return "Culture not set";
            CultureDef source = NativeDef(culture);
            string compact = CAAuthoringChoices.CultureIdentity(culture)
                + " · "
                + (source?.LabelCap.ToString() ?? "Generated")
                + " visual style";
            string standard = compact + " · "
                + Words("gathering", culture.gatheringKey) + " · "
                + Words("meals", culture.mealsKey);
            string expanded = standard + " · "
                + Words("hospitality", culture.hospitalityKey) + " · "
                + Words("remembrance", culture.remembranceKey);
            return CAInformationPresentation.Select(compact, standard,
                expanded);
        }

        internal static Texture2D Icon(CACulture culture)
        {
            CACulturePreset preset = Preset(culture?.presetName);
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
            string incompatibility = CACultureModel.CompatibilityFailure(
                culture);
            if (!incompatibility.NullOrEmpty())
            {
                Log.Warning("[CA][Culture] "
                    + (record.name ?? "settlement")
                    + " cannot materialize culture: " + incompatibility);
                return;
            }
            int laid = 0;
            foreach (CACultureDomainDef domain in CACultureModel.Domains)
            {
                CACultureOptionDef option = CACultureModel.Option(
                    domain.Field, culture.Value(domain.Field));
                if (option == null || option.ThingDefName.NullOrEmpty())
                    continue;
                try
                {
                    Room room = next();
                    int placed = place(room, option.ThingDefName,
                        option.StuffDefName);
                    laid += placed;
                    if (placed > 0)
                        Log.Message("[CA][Culture] "
                            + (record.name ?? "settlement")
                            + " materialized " + domain.Key + "="
                            + option.Key + " as " + option.ThingDefName
                            + ".");
                    else
                        Log.Warning("[CA][Culture] "
                            + (record.name ?? "settlement")
                            + " had no valid place for " + domain.Label
                            + " (" + option.ThingDefName + ").");
                }
                catch (Exception exception)
                {
                    Log.Warning("[CA][Culture] "
                        + (record.name ?? "settlement") + " could not place "
                        + domain.Label + ": " + exception.Message);
                }
            }
            if (laid == 0)
                Log.Warning("[CA][Culture] "
                    + (record.name ?? "settlement")
                    + " had cultural practices but no furnishing could be placed.");
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
    // Culture and Political Beliefs remain the same models.
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
            string description = CAInformationPresentation.Select(
                "Set cultural practices and visual tradition.",
                "Set cultural practices and the native visual style source. "
                    + "Ideoligion separately owns memes, precepts, rituals, and roles.",
                "These practices materialize settlement furnishings for public "
                    + "gathering, hospitality, shared meals, and public memory. "
                    + "Political beliefs and Ideoligion remain separate owners.",
                localExpanded);
            float descriptionHeight = Text.CalcHeight(description,
                inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width,
                descriptionHeight), description);
            y += descriptionHeight + 12f;

            y = DrawActions(inRect, y);
            string[] tabs = { "Overview", "Visual", "Gathering",
                "Hospitality", "Meals", "Memory" };
            float tabHeight = CACreationUI.DrawSegmentRows(new Rect(0f, y,
                inRect.width, 30f), tabs, section, value =>
                {
                    section = value;
                    scroll = Vector2.zero;
                }, 112f);
            y += tabHeight + 10f;

            Rect outRect = new Rect(0f, y, inRect.width,
                inRect.height - y - 34f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float rowY = 0f;
            if (section == 0) DrawOverview(ref rowY, view.width);
            else if (section == 1) DrawVisual(ref rowY, view.width);
            else
            {
                CACultureDomainDef domain = CACultureModel.Domains[section - 2];
                DrawDomain(ref rowY, view.width, domain);
            }
            viewHeight = rowY + 12f;
            Widgets.EndScrollView();
            CAInformationPresentation.DrawLocalExpansion(new Rect(
                inRect.width - 160f, inRect.height - 30f, 160f, 28f),
                ref localExpanded);
        }

        private float DrawActions(Rect inRect, float y)
        {
            string[] labels = { "Profiles...", "Save profile...",
                "Manage saved...", "Generate missing" };
            Action[] actions = { OpenCulturePreset, SaveProfile,
                () => Find.WindowStack.Add(new Dialog_CAProfileManager(
                    culture, null, changed)), GenerateMissing };
            int columns = inRect.width >= 700f ? 4 : 2;
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

        private void DrawOverview(ref float y, float width)
        {
            Row(ref y, width, "Name", culture.name ?? "Local customs",
                () => Find.WindowStack.Add(new Dialog_CARenameCulture(
                    culture, changed)), FieldState(CACulture.NameField));
            CACulturePreset preset = CACultureModel.Preset(culture.presetName);
            CAUserCultureProfile saved = CAAuthoringProfileLibrary.Cultures
                .FirstOrDefault(item => item.key == culture.profileKey);
            string profile = CAAuthoringChoices.CultureIdentity(culture);
            Row(ref y, width, "Profile", profile, OpenCulturePreset,
                saved != null ? CAAxisSource.Authored
                    : preset != null ? CAAxisSource.Preset
                        : CAAxisSource.Authored);
            CultureDef native = CACultureModel.NativeDef(culture);
            Row(ref y, width, "Visual style source",
                native?.LabelCap.ToString() ?? "No source chosen",
                OpenSourceCulture, FieldState(CACulture.SourceCultureField));
            foreach (CACultureDomainDef domain in CACultureModel.Domains)
                Row(ref y, width, domain.Label,
                    CACultureModel.Words(domain.Key,
                        culture.Value(domain.Field)),
                    () => OpenField(domain), FieldState(domain.Field));
        }

        private void DrawVisual(ref float y, float width)
        {
            CultureDef native = CACultureModel.NativeDef(culture);
            Row(ref y, width, "Native visual style source",
                native?.LabelCap.ToString() ?? "No source chosen",
                OpenSourceCulture, FieldState(CACulture.SourceCultureField));
            DrawExplanation(ref y, width,
                "This selects RimWorld's native ThingStyleDef categories. "
                + "It does not define religious, moral, or political belief.");
        }

        private void DrawDomain(ref float y, float width,
            CACultureDomainDef domain)
        {
            CACultureOptionDef option = CACultureModel.Option(domain.Field,
                culture.Value(domain.Field));
            Row(ref y, width, domain.Label,
                option?.Label ?? "Choose a practice", () => OpenField(domain),
                FieldState(domain.Field));
            DrawExplanation(ref y, width, domain.Question + "\n\n"
                + (option?.Summary ?? "No practice is currently set.")
                + (CAInformationPresentation.Shows(
                    CAInformationDetail.Standard, localExpanded)
                        ? "\n\nDirect result: " + (option?.Consumer
                            ?? "Generate the missing practice before materialization.")
                        : ""));
            if (Widgets.ButtonText(new Rect(0f, y, 220f, 30f),
                    "Clear for generation"))
            {
                culture.Release(domain.Field);
                changed?.Invoke();
            }
            TooltipHandler.TipRegion(new Rect(0f, y, 220f, 30f),
                "Removes this position. Generate missing can fill it without "
                + "changing other authored practices.");
            y += 40f;
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

        private void OpenField(CACultureDomainDef domain)
        {
            var options = new List<CACreationChoice>();
            string current = culture.Value(domain.Field);
            foreach (CACultureOptionDef option in domain.Options)
            {
                CACultureOptionDef local = option;
                options.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label,
                    Summary = local.Summary,
                    Traits = local.Consumer,
                    Details = domain.Question,
                    Badge = current == local.Key ? "Current" : "Practice",
                    Accent = current == local.Key
                        ? CACreationUI.Authored : CACreationUI.Accent,
                    Selected = current == local.Key,
                    ConfirmLabel = "Use this practice",
                    Choose = delegate
                    {
                        culture.Choose(domain.Field, local.Key);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices(domain.Label, domain.Question, options);
        }

        private void OpenCulturePreset()
        {
            CACreationUI.OpenChoices("Culture profiles",
                "Apply a built-in or saved set of practices. Every field "
                    + "remains editable after values are copied into this draft.",
                CAAuthoringChoices.CultureProfiles(culture,
                    culture.id ?? factionLabel ?? "ca-culture", changed));
        }

        private void SaveProfile()
        {
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Save culture profile", culture.name ?? "Saved culture",
                value =>
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

        private void GenerateMissing()
        {
            CACultureModel.EnsureGenerated(culture,
                (culture.id ?? factionLabel ?? "ca-culture") + ":fill",
                CACultureModel.NativeDef(culture));
            changed?.Invoke();
        }

        private void OpenSourceCulture()
        {
            var options = new List<CACreationChoice>();
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
                    ConfirmLabel = "Use this style source",
                    Choose = delegate
                    {
                        culture.Choose(CACulture.SourceCultureField,
                            local.defName);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices("Visual style source",
                "Choose the native RimWorld culture that supplies visual "
                + "styles. This does not set Ideoligion or political belief.",
                options);
        }

        private CAAxisSource FieldState(int field)
        {
            if (culture.Authored(field)) return CAAxisSource.Authored;
            if ((culture.presetMask & field) != 0) return CAAxisSource.Preset;
            return culture.Value(field).NullOrEmpty()
                ? CAAxisSource.Unset : CAAxisSource.Generated;
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
