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

    public sealed class CAUserPoliticalOrderProfile : IExposable
    {
        public const int CurrentSchemaVersion = 10;
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

        internal CAUserPoliticalOrderProfile CopyAs(string newKey,
            string newName)
        {
            return new CAUserPoliticalOrderProfile
            {
                key = newKey,
                displayName = newName,
                values = values?.Copy() ?? new CAPoliticalBeliefs()
            };
        }
    }

    public sealed class CAUserSocietyProfile : IExposable
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public string key;
        public string displayName;
        public CACulture cultureValues = new CACulture();
        public CAPoliticalBeliefs politicalOrderValues =
            new CAPoliticalBeliefs();
        public CATechnologicalKnowledge technologicalKnowledgeValues =
            new CATechnologicalKnowledge();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref displayName, "displayName");
            Scribe_Deep.Look(ref cultureValues, "cultureValues");
            Scribe_Deep.Look(ref politicalOrderValues,
                "politicalOrderValues");
            Scribe_Deep.Look(ref technologicalKnowledgeValues,
                "technologicalKnowledgeValues");
            if (cultureValues == null) cultureValues = new CACulture();
            if (politicalOrderValues == null)
                politicalOrderValues = new CAPoliticalBeliefs();
            if (technologicalKnowledgeValues == null)
                technologicalKnowledgeValues =
                    new CATechnologicalKnowledge();
        }

        internal CAUserSocietyProfile CopyAs(string newKey, string newName)
        {
            var political = politicalOrderValues?.Copy()
                ?? new CAPoliticalBeliefs();
            political.id = null;
            return new CAUserSocietyProfile
            {
                key = newKey,
                displayName = newName,
                cultureValues = cultureValues?.CopyAsInheritedTemplate()
                    ?? new CACulture(),
                politicalOrderValues = political,
                technologicalKnowledgeValues =
                    technologicalKnowledgeValues?.CopyAsPreset()
                        ?? new CATechnologicalKnowledge()
            };
        }
    }

    internal sealed class CAUserSocietyPresetAdapter : CASocietyPreset
    {
        private readonly CAUserSocietyProfile profile;

        internal CAUserSocietyPresetAdapter(CAUserSocietyProfile profile)
        {
            this.profile = profile;
        }

        internal override string Key => profile?.key;
        internal override string Label => profile?.displayName;
        internal override string CatalogGroup => "Saved societies";
        internal override string ReferenceRegion => null;
        internal override string ApproximatePeriod => null;
        internal override string Summary => profile == null
            ? "Saved society"
            : CACultureModel.Summary(profile.cultureValues);
        internal override string CultureSummary => profile == null
            ? null : CACultureModel.Summary(profile.cultureValues);
        internal override string TechnologySummary => profile == null
            ? null : CATechnologicalKnowledgeModel.Summary(
                profile.technologicalKnowledgeValues);
        internal override bool Saved => true;

        internal override string ValidationFailure()
        {
            if (profile == null
                || profile.schemaVersion
                    != CAUserSocietyProfile.CurrentSchemaVersion
                || profile.key.NullOrEmpty()
                || profile.displayName.NullOrEmpty())
                return "saved society identity is incomplete";
            CACulture cultureCandidate = profile.cultureValues?.Copy();
            CACultureModel.EnsureIdentity(cultureCandidate,
                "saved-society:" + profile.key);
            string failure = CACultureModel.ValidationFailure(
                cultureCandidate, requireSubstantive: true);
            if (!failure.NullOrEmpty()) return "Culture: " + failure;
            failure = CAPoliticalOrderModel.ValidationFailure(
                profile.politicalOrderValues);
            if (!failure.NullOrEmpty())
                return "Political Order: " + failure;
            failure = CATechnologicalKnowledgeModel.ValidationFailure(
                profile.technologicalKnowledgeValues,
                requireComplete: false);
            return failure.NullOrEmpty() ? null
                : "Technological Knowledge: " + failure;
        }

        internal override void ApplyComponents(CACulture culture,
            CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge,
            string sourceIdentity)
        {
            CAAuthoringProfileLibrary.ApplyCultureValues(
                profile?.cultureValues, culture);
            CAAuthoringProfileLibrary.ApplyPoliticalValues(
                profile?.politicalOrderValues, politicalOrder);
            CAAuthoringProfileLibrary.ApplyTechnologyValues(
                profile?.technologicalKnowledgeValues,
                technologicalKnowledge, sourceIdentity);
        }

        internal override bool Matches(CACulture culture,
            CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge)
        {
            return CAAuthoringProfileLibrary.MatchesCultureValues(culture,
                    profile?.cultureValues)
                && CAPoliticalOrderModel.Matches(politicalOrder,
                    profile?.politicalOrderValues)
                && CATechnologicalKnowledgeModel.Matches(
                    technologicalKnowledge,
                    profile?.technologicalKnowledgeValues);
        }

        internal override CAPoliticalBeliefs PoliticalPreview()
        {
            return profile?.politicalOrderValues?.Copy()
                ?? new CAPoliticalBeliefs();
        }

        internal override CATechnologicalKnowledge TechnologyPreview()
        {
            return profile?.technologicalKnowledgeValues?.CopyAsPreset()
                ?? new CATechnologicalKnowledge();
        }
    }

    // Reusable presets live in ModSettings. Applying one copies its values
    // into the world-owned draft; the library never becomes live world state.
    internal static class CAAuthoringProfileLibrary
    {
        private const string CulturePrefix = "user-culture:";
        private const string PoliticsPrefix = "user-political-order:";
        private const string SocietyPrefix = "user-society:";

        internal static List<CAUserCultureProfile> Cultures =>
            AwarenessMod.Settings?.cultureProfiles
                ?? new List<CAUserCultureProfile>();

        internal static List<CAUserPoliticalOrderProfile> PoliticalOrders =>
            AwarenessMod.Settings?.politicalOrderProfiles
                ?? new List<CAUserPoliticalOrderProfile>();

        internal static List<CAUserSocietyProfile> Societies =>
            AwarenessMod.Settings?.societyProfiles
                ?? new List<CAUserSocietyProfile>();

        internal static void Normalize(AwarenessSettings settings)
        {
            if (settings == null) return;
            if (settings.cultureProfiles == null)
                settings.cultureProfiles = new List<CAUserCultureProfile>();
            if (settings.politicalOrderProfiles == null)
                settings.politicalOrderProfiles =
                    new List<CAUserPoliticalOrderProfile>();
            if (settings.societyProfiles == null)
                settings.societyProfiles = new List<CAUserSocietyProfile>();
            settings.cultureProfiles.RemoveAll(item => item == null
                || item.schemaVersion
                    != CAUserCultureProfile.CurrentSchemaVersion);
            settings.politicalOrderProfiles.RemoveAll(item => item == null
                || item.schemaVersion != CAUserPoliticalOrderProfile.CurrentSchemaVersion
                || item.values?.schemaVersion
                    != CAPoliticalBeliefs.CurrentSchemaVersion);
            settings.societyProfiles.RemoveAll(item => item == null
                || item.schemaVersion != CAUserSocietyProfile.CurrentSchemaVersion
                || item.politicalOrderValues?.schemaVersion
                    != CAPoliticalBeliefs.CurrentSchemaVersion
                || item.technologicalKnowledgeValues?.schemaVersion
                    != CATechnologicalKnowledge.CurrentSchemaVersion);
            for (int index = settings.cultureProfiles.Count - 1;
                index >= 0; index--)
            {
                CAUserCultureProfile item = settings.cultureProfiles[index];
                if (!TryNormalizeCulture(item.values,
                        out CACulture normalized))
                {
                    settings.cultureProfiles.RemoveAt(index);
                    continue;
                }
                if (item.key.NullOrEmpty()) item.key = NewKey(CulturePrefix);
                if (item.displayName.NullOrEmpty()) item.displayName = "Saved background";
                item.values = normalized;
                item.schemaVersion = CAUserCultureProfile.CurrentSchemaVersion;
            }
            foreach (CAUserPoliticalOrderProfile item in
                settings.politicalOrderProfiles)
            {
                if (item.key.NullOrEmpty()) item.key = NewKey(PoliticsPrefix);
                if (item.displayName.NullOrEmpty())
                    item.displayName = "Saved Political Order";
                if (item.values == null) item.values = new CAPoliticalBeliefs();
                CAPoliticalBeliefsModel.Normalize(item.values);
            }
            for (int index = settings.societyProfiles.Count - 1;
                index >= 0; index--)
            {
                CAUserSocietyProfile item = settings.societyProfiles[index];
                if (!TryNormalizeCulture(item.cultureValues,
                        out CACulture normalized))
                {
                    settings.societyProfiles.RemoveAt(index);
                    continue;
                }
                if (item.key.NullOrEmpty()) item.key = NewKey(SocietyPrefix);
                if (item.displayName.NullOrEmpty())
                    item.displayName = "Saved society";
                item.cultureValues = normalized;
                CAPoliticalBeliefsModel.Normalize(item.politicalOrderValues);
                item.politicalOrderValues.id = null;
                CATechnologicalKnowledgeModel.Normalize(
                    item.technologicalKnowledgeValues);
                item.technologicalKnowledgeValues =
                    item.technologicalKnowledgeValues.CopyAsPreset();
                item.schemaVersion = CAUserSocietyProfile.CurrentSchemaVersion;
            }
        }

        private static bool TryNormalizeCulture(CACulture source,
            out CACulture normalized)
        {
            normalized = null;
            if (!CACultureModel.TryUpgradeToCurrent(source,
                    out CACulture upgraded, out _))
                return false;
            normalized = upgraded.CopyAsInheritedTemplate();
            return true;
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

        internal static CAUserPoliticalOrderProfile SavePoliticalOrder(
            string name,
            CAPoliticalBeliefs beliefs)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || beliefs == null
                || !CAPoliticalOrderModel.HasVariables(beliefs))
                return null;
            Normalize(settings);
            var profile = new CAUserPoliticalOrderProfile
            {
                key = NewKey(PoliticsPrefix),
                displayName = UniqueName(name, settings.politicalOrderProfiles
                    .Select(item => item.displayName)),
                values = beliefs.Copy()
            };
            profile.values.id = null;
            settings.politicalOrderProfiles.Add(profile);
            AwarenessMod.SaveSettings();
            return profile;
        }

        internal static CAUserSocietyProfile SaveSociety(string name,
            CACulture culture, CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || culture == null || politicalOrder == null
                || technologicalKnowledge == null
                || CACultureModel.SubstantiveFailure(culture) != null
                || CAPoliticalOrderModel.ValidationFailure(politicalOrder)
                    != null
                || CATechnologicalKnowledgeModel.ValidationFailure(
                    technologicalKnowledge) != null)
                return null;
            Normalize(settings);
            var political = politicalOrder.Copy();
            political.id = null;
            var profile = new CAUserSocietyProfile
            {
                key = NewKey(SocietyPrefix),
                displayName = UniqueName(name, settings.societyProfiles
                    .Select(item => item.displayName)),
                cultureValues = culture.CopyAsInheritedTemplate(),
                politicalOrderValues = political,
                technologicalKnowledgeValues =
                    technologicalKnowledge.CopyAsPreset()
            };
            settings.societyProfiles.Add(profile);
            AwarenessMod.SaveSettings();
            return profile;
        }

        internal static void Apply(CAUserCultureProfile profile,
            CACulture target)
        {
            ApplyCultureValues(profile?.values, target);
        }

        internal static void Apply(CAUserPoliticalOrderProfile profile,
            CAPoliticalBeliefs target)
        {
            ApplyPoliticalValues(profile?.values, target);
        }

        internal static void ApplyCultureValues(CACulture values,
            CACulture target)
        {
            if (values == null || target == null) return;
            target.ApplyInheritedTemplate(values);
        }

        internal static void ApplyPoliticalValues(CAPoliticalBeliefs values,
            CAPoliticalBeliefs target)
        {
            if (values == null || target == null) return;
            string ownerId = target.id;
            target.CopyFrom(values);
            target.id = ownerId;
            foreach (CAPoliticalQuestionState state in target.questions
                ?? new List<CAPoliticalQuestionState>())
                foreach (CAPoliticalOptionShare option in state?.options
                    ?? new List<CAPoliticalOptionShare>())
                    option.source = (byte)CAAxisSource.Authored;
            CAPoliticalBeliefsModel.Normalize(target);
        }

        internal static void ApplyTechnologyValues(
            CATechnologicalKnowledge values,
            CATechnologicalKnowledge target, string sourceIdentity)
        {
            if (values == null || target == null) return;
            string ownerId = target.id;
            target.CopyFrom(values, includeDistribution: false);
            target.id = ownerId;
            target.origin = CAOrigin.Authored(sourceIdentity
                ?? "saved society");
            foreach (CATechnologyDomainKnowledge domain in target.domains)
            {
                domain.source = (byte)CAAxisSource.Authored;
                domain.provenance = sourceIdentity ?? "saved society";
            }
        }

        internal static bool MatchesCultureValues(CACulture target,
            CACulture values)
        {
            return CACultureModel.MatchesInheritedTemplate(target, values);
        }

        internal static void Delete(CAUserCultureProfile profile)
        {
            if (profile == null || AwarenessMod.Settings == null) return;
            AwarenessMod.Settings.cultureProfiles.Remove(profile);
            AwarenessMod.SaveSettings();
        }

        internal static void Delete(CAUserPoliticalOrderProfile profile)
        {
            if (profile == null || AwarenessMod.Settings == null) return;
            AwarenessMod.Settings.politicalOrderProfiles.Remove(profile);
            AwarenessMod.SaveSettings();
        }

        internal static void Delete(CAUserSocietyProfile profile)
        {
            if (profile == null || AwarenessMod.Settings == null) return;
            AwarenessMod.Settings.societyProfiles.Remove(profile);
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

        internal static CAUserPoliticalOrderProfile Duplicate(
            CAUserPoliticalOrderProfile source)
        {
            if (source == null || AwarenessMod.Settings == null) return null;
            Normalize(AwarenessMod.Settings);
            CAUserPoliticalOrderProfile copy = source.CopyAs(
                NewKey(PoliticsPrefix),
                UniqueName(source.displayName + " copy",
                    PoliticalOrders.Select(item => item.displayName)));
            AwarenessMod.Settings.politicalOrderProfiles.Add(copy);
            AwarenessMod.SaveSettings();
            return copy;
        }

        internal static CAUserSocietyProfile Duplicate(
            CAUserSocietyProfile source)
        {
            if (source == null || AwarenessMod.Settings == null) return null;
            Normalize(AwarenessMod.Settings);
            CAUserSocietyProfile copy = source.CopyAs(NewKey(SocietyPrefix),
                UniqueName(source.displayName + " copy",
                    Societies.Select(item => item.displayName)));
            AwarenessMod.Settings.societyProfiles.Add(copy);
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

        internal static void Rename(CAUserPoliticalOrderProfile profile,
            string name)
        {
            if (profile == null || name.NullOrEmpty()) return;
            profile.displayName = UniqueName(name,
                PoliticalOrders.Where(item => item != profile)
                    .Select(item => item.displayName));
            AwarenessMod.SaveSettings();
        }

        internal static void Rename(CAUserSocietyProfile profile,
            string name)
        {
            if (profile == null || name.NullOrEmpty()) return;
            profile.displayName = UniqueName(name,
                Societies.Where(item => item != profile)
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
