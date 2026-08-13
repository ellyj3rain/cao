using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public sealed class CAUserCultureProfile : IExposable
    {
        public const int CurrentSchemaVersion = 9;
        public int schemaVersion = CurrentSchemaVersion;
        public string key;
        public string displayName;
        public CACulture values = new CACulture();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref displayName, "displayName");
            Scribe_Deep.Look(ref values, "values");
            if (values == null) values = new CACulture();
        }

        internal CAUserCultureProfile CopyAs(string newKey,
            string newName)
        {
            return new CAUserCultureProfile
            {
                key = newKey,
                displayName = newName,
                values = values?.CopyAsInheritedTemplate()
                    ?? new CACulture()
            };
        }
    }

    public sealed class CAUserPoliticalBeliefSet : IExposable
    {
        public const int CurrentSchemaVersion = 9;
        public int schemaVersion = CurrentSchemaVersion;
        public string key;
        public string displayName;
        public CAPoliticalBeliefs values = new CAPoliticalBeliefs();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref displayName, "displayName");
            Scribe_Deep.Look(ref values, "values");
            if (values == null) values = new CAPoliticalBeliefs();
        }

        internal CAUserPoliticalBeliefSet CopyAs(string newKey,
            string newName)
        {
            return new CAUserPoliticalBeliefSet
            {
                key = newKey,
                displayName = newName,
                values = values?.Copy() ?? new CAPoliticalBeliefs()
            };
        }
    }

    // Reusable presets live in ModSettings. Applying one copies its values
    // into the world-owned draft; the library never becomes live world state.
    internal static class CAAuthoringProfileLibrary
    {
        private const string CulturePrefix = "user-culture:";
        private const string PoliticsPrefix = "user-political-beliefs:";

        internal static List<CAUserCultureProfile> Cultures =>
            AwarenessMod.Settings?.cultureProfiles
                ?? new List<CAUserCultureProfile>();

        internal static List<CAUserPoliticalBeliefSet> PoliticalBeliefSets =>
            AwarenessMod.Settings?.politicalBeliefSets
                ?? new List<CAUserPoliticalBeliefSet>();

        internal static void Normalize(AwarenessSettings settings)
        {
            if (settings == null) return;
            if (settings.cultureProfiles == null)
                settings.cultureProfiles = new List<CAUserCultureProfile>();
            if (settings.politicalBeliefSets == null)
                settings.politicalBeliefSets =
                    new List<CAUserPoliticalBeliefSet>();
            settings.cultureProfiles.RemoveAll(item => item == null
                || item.schemaVersion != CAUserCultureProfile.CurrentSchemaVersion
                || item.values?.schemaVersion != CACulture.CurrentSchemaVersion);
            settings.politicalBeliefSets.RemoveAll(item => item == null
                || item.schemaVersion != CAUserPoliticalBeliefSet.CurrentSchemaVersion
                || item.values?.schemaVersion
                    != CAPoliticalBeliefs.CurrentSchemaVersion);
            foreach (CAUserCultureProfile item in settings.cultureProfiles)
            {
                if (item.key.NullOrEmpty()) item.key = NewKey(CulturePrefix);
                if (item.displayName.NullOrEmpty()) item.displayName = "Saved background";
                if (item.values == null) item.values = new CACulture();
                CACultureModel.Normalize(item.values);
                item.values = item.values.CopyAsInheritedTemplate();
                item.schemaVersion = CAUserCultureProfile.CurrentSchemaVersion;
            }
            foreach (CAUserPoliticalBeliefSet item in
                settings.politicalBeliefSets)
            {
                if (item.key.NullOrEmpty()) item.key = NewKey(PoliticsPrefix);
                if (item.displayName.NullOrEmpty())
                    item.displayName = "Saved political belief set";
                if (item.values == null) item.values = new CAPoliticalBeliefs();
                CAPoliticalBeliefsModel.Normalize(item.values);
            }
        }

        internal static CAUserCultureProfile SaveCulture(string name,
            CACulture culture)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || culture == null
                || CACultureModel.SubstantiveFailure(culture) != null)
                return null;
            Normalize(settings);
            var profile = new CAUserCultureProfile
            {
                key = NewKey(CulturePrefix),
                displayName = UniqueName(name, settings.cultureProfiles
                    .Select(item => item.displayName)),
                values = culture.CopyAsInheritedTemplate()
            };
            settings.cultureProfiles.Add(profile);
            AwarenessMod.SaveSettings();
            return profile;
        }

        internal static CAUserPoliticalBeliefSet SavePoliticalBeliefs(
            string name,
            CAPoliticalBeliefs beliefs)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || beliefs == null
                || !(beliefs.positions ?? new List<CAAxisEntry>()).Any(
                    item => item != null
                        && item.source != (byte)CAAxisSource.Unset))
                return null;
            Normalize(settings);
            var profile = new CAUserPoliticalBeliefSet
            {
                key = NewKey(PoliticsPrefix),
                displayName = UniqueName(name, settings.politicalBeliefSets
                    .Select(item => item.displayName)),
                values = beliefs.Copy()
            };
            profile.values.id = null;
            settings.politicalBeliefSets.Add(profile);
            AwarenessMod.SaveSettings();
            return profile;
        }

        internal static void Apply(CAUserCultureProfile profile,
            CACulture target)
        {
            if (profile?.values == null || target == null) return;
            target.ApplyInheritedTemplate(profile.values);
        }

        internal static void Apply(CAUserPoliticalBeliefSet profile,
            CAPoliticalBeliefs target)
        {
            if (profile?.values == null || target == null) return;
            foreach (CAAxisEntry entry in profile.values.positions
                ?? new List<CAAxisEntry>())
                if (entry != null
                    && entry.source != (byte)CAAxisSource.Unset)
                {
                    CAFactionAxes.Add(target.positions, entry.axisKey,
                        entry.optionKey, CAAxisSource.Authored);
                    CAPoliticalBeliefsModel.RemoveReceipt(target,
                        entry.axisKey, entry.optionKey);
                }
            CAPoliticalBeliefsModel.ReconcileReceipts(target);
        }

        internal static void Delete(CAUserCultureProfile profile)
        {
            if (profile == null || AwarenessMod.Settings == null) return;
            AwarenessMod.Settings.cultureProfiles.Remove(profile);
            AwarenessMod.SaveSettings();
        }

        internal static void Delete(CAUserPoliticalBeliefSet profile)
        {
            if (profile == null || AwarenessMod.Settings == null) return;
            AwarenessMod.Settings.politicalBeliefSets.Remove(profile);
            AwarenessMod.SaveSettings();
        }

        internal static CAUserCultureProfile Duplicate(
            CAUserCultureProfile source)
        {
            if (source == null || AwarenessMod.Settings == null) return null;
            Normalize(AwarenessMod.Settings);
            CAUserCultureProfile copy = source.CopyAs(NewKey(CulturePrefix),
                UniqueName(source.displayName + " copy",
                    Cultures.Select(item => item.displayName)));
            AwarenessMod.Settings.cultureProfiles.Add(copy);
            AwarenessMod.SaveSettings();
            return copy;
        }

        internal static CAUserPoliticalBeliefSet Duplicate(
            CAUserPoliticalBeliefSet source)
        {
            if (source == null || AwarenessMod.Settings == null) return null;
            Normalize(AwarenessMod.Settings);
            CAUserPoliticalBeliefSet copy = source.CopyAs(
                NewKey(PoliticsPrefix),
                UniqueName(source.displayName + " copy",
                    PoliticalBeliefSets.Select(item => item.displayName)));
            AwarenessMod.Settings.politicalBeliefSets.Add(copy);
            AwarenessMod.SaveSettings();
            return copy;
        }

        internal static void Rename(CAUserCultureProfile profile,
            string name)
        {
            if (profile == null || name.NullOrEmpty()) return;
            profile.displayName = UniqueName(name,
                Cultures.Where(item => item != profile)
                    .Select(item => item.displayName));
            AwarenessMod.SaveSettings();
        }

        internal static void Rename(CAUserPoliticalBeliefSet profile,
            string name)
        {
            if (profile == null || name.NullOrEmpty()) return;
            profile.displayName = UniqueName(name,
                PoliticalBeliefSets.Where(item => item != profile)
                    .Select(item => item.displayName));
            AwarenessMod.SaveSettings();
        }

        private static string NewKey(string prefix)
        {
            return prefix + Guid.NewGuid().ToString("N");
        }

        private static string UniqueName(string requested,
            IEnumerable<string> existing)
        {
            string root = requested.NullOrEmpty() ? "Saved profile"
                : requested.Trim();
            var used = new HashSet<string>(existing
                .Where(item => !item.NullOrEmpty()),
                StringComparer.OrdinalIgnoreCase);
            if (!used.Contains(root)) return root;
            for (int i = 2; ; i++)
            {
                string candidate = root + " " + i;
                if (!used.Contains(candidate)) return candidate;
            }
        }
    }
}
