using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public sealed class CAUserCultureProfile : IExposable
    {
        public const int CurrentSchemaVersion = 8;
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

    public sealed class CAUserPoliticalProfile : IExposable
    {
        public const int CurrentSchemaVersion = 8;
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

        internal CAUserPoliticalProfile CopyAs(string newKey,
            string newName)
        {
            return new CAUserPoliticalProfile
            {
                key = newKey,
                displayName = newName,
                values = values?.Copy() ?? new CAPoliticalBeliefs()
            };
        }
    }

    // Reusable profiles live in ModSettings. Applying one copies its values
    // into the world-owned draft; the library never becomes live world state.
    internal static class CAAuthoringProfileLibrary
    {
        private const string CulturePrefix = "user-culture:";
        private const string PoliticsPrefix = "user-politics:";

        internal static List<CAUserCultureProfile> Cultures =>
            AwarenessMod.Settings?.cultureProfiles
                ?? new List<CAUserCultureProfile>();

        internal static List<CAUserPoliticalProfile> Politics =>
            AwarenessMod.Settings?.politicalProfiles
                ?? new List<CAUserPoliticalProfile>();

        internal static void Normalize(AwarenessSettings settings)
        {
            if (settings == null) return;
            if (settings.cultureProfiles == null)
                settings.cultureProfiles = new List<CAUserCultureProfile>();
            if (settings.politicalProfiles == null)
                settings.politicalProfiles = new List<CAUserPoliticalProfile>();
            settings.cultureProfiles.RemoveAll(item => item == null
                || item.schemaVersion != CAUserCultureProfile.CurrentSchemaVersion
                || item.values?.schemaVersion != CACulture.CurrentSchemaVersion);
            settings.politicalProfiles.RemoveAll(item => item == null
                || item.schemaVersion != CAUserPoliticalProfile.CurrentSchemaVersion
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
            foreach (CAUserPoliticalProfile item in settings.politicalProfiles)
            {
                if (item.key.NullOrEmpty()) item.key = NewKey(PoliticsPrefix);
                if (item.displayName.NullOrEmpty()) item.displayName = "Saved political profile";
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

        internal static CAUserPoliticalProfile SavePolitics(string name,
            CAPoliticalBeliefs beliefs)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || beliefs == null
                || CAFactionAxes.CountByState(beliefs.positions,
                    CAAxisSource.Unset) != 0) return null;
            Normalize(settings);
            var profile = new CAUserPoliticalProfile
            {
                key = NewKey(PoliticsPrefix),
                displayName = UniqueName(name, settings.politicalProfiles
                    .Select(item => item.displayName)),
                values = beliefs.Copy()
            };
            profile.values.id = null;
            settings.politicalProfiles.Add(profile);
            AwarenessMod.SaveSettings();
            return profile;
        }

        internal static void Apply(CAUserCultureProfile profile,
            CACulture target)
        {
            if (profile?.values == null || target == null) return;
            target.ApplyInheritedTemplate(profile.values);
        }

        internal static void Apply(CAUserPoliticalProfile profile,
            CAPoliticalBeliefs target)
        {
            if (profile?.values == null || target == null) return;
            string worldIdentity = target.id;
            target.CopyFrom(profile.values);
            target.id = worldIdentity;
        }

        internal static void Delete(CAUserCultureProfile profile)
        {
            if (profile == null || AwarenessMod.Settings == null) return;
            AwarenessMod.Settings.cultureProfiles.Remove(profile);
            AwarenessMod.SaveSettings();
        }

        internal static void Delete(CAUserPoliticalProfile profile)
        {
            if (profile == null || AwarenessMod.Settings == null) return;
            AwarenessMod.Settings.politicalProfiles.Remove(profile);
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

        internal static CAUserPoliticalProfile Duplicate(
            CAUserPoliticalProfile source)
        {
            if (source == null || AwarenessMod.Settings == null) return null;
            Normalize(AwarenessMod.Settings);
            CAUserPoliticalProfile copy = source.CopyAs(NewKey(PoliticsPrefix),
                UniqueName(source.displayName + " copy",
                    Politics.Select(item => item.displayName)));
            AwarenessMod.Settings.politicalProfiles.Add(copy);
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

        internal static void Rename(CAUserPoliticalProfile profile,
            string name)
        {
            if (profile == null || name.NullOrEmpty()) return;
            profile.displayName = UniqueName(name,
                Politics.Where(item => item != profile)
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
