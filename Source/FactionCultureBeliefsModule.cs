using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public enum CACultureMaturity : byte
    {
        Inherited,
        Forming,
        Established
    }

    public sealed class CACultureConstituent : IExposable
    {
        public string cultureId;
        public string label;
        public int share;
        public bool inherited = true;
        public bool separateQuarter;

        public void ExposeData()
        {
            Scribe_Values.Look(ref cultureId, "cultureId");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref share, "share", 0);
            Scribe_Values.Look(ref inherited, "inherited", true);
            Scribe_Values.Look(ref separateQuarter,
                "separateQuarter", false);
        }

        internal CACultureConstituent Copy()
        {
            return new CACultureConstituent
            {
                cultureId = cultureId,
                label = label,
                share = share,
                inherited = inherited,
                separateQuarter = separateQuarter
            };
        }
    }

    public sealed class CACulturePractice : IExposable
    {
        public string key;
        public string summary;
        public int strength;
        public int firstRecordedTick = -1;
        public int lastObservedTick = -1;
        public string sourceSignature;
        public string sourcePeriod;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Values.Look(ref strength, "strength", 0);
            Scribe_Values.Look(ref firstRecordedTick,
                "firstRecordedTick", -1);
            Scribe_Values.Look(ref lastObservedTick,
                "lastObservedTick", -1);
            Scribe_Values.Look(ref sourceSignature,
                "sourceSignature");
            Scribe_Values.Look(ref sourcePeriod, "sourcePeriod");
        }

        internal CACulturePractice Copy()
        {
            return new CACulturePractice
            {
                key = key,
                summary = summary,
                strength = strength,
                firstRecordedTick = firstRecordedTick,
                lastObservedTick = lastObservedTick,
                sourceSignature = sourceSignature,
                sourcePeriod = sourcePeriod
            };
        }
    }

    // An observation is evidence about one practice at one owning surface.
    // It is not itself a cultural practice. Repetition over lived time may
    // qualify it for the longitudinal kernel; a single snapshot may not.
    public sealed class CACultureObservation : IExposable
    {
        public string key;
        public string summary;
        public string sourceOwner;
        public string sourceDomain;
        public string sourceSignature;
        public int strength;
        public int evidenceStartTick = -1;
        public int firstObservedTick = -1;
        public int lastObservedTick = -1;
        public int observationCount;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Values.Look(ref sourceOwner, "sourceOwner");
            Scribe_Values.Look(ref sourceDomain, "sourceDomain");
            Scribe_Values.Look(ref sourceSignature, "sourceSignature");
            Scribe_Values.Look(ref strength, "strength", 0);
            Scribe_Values.Look(ref evidenceStartTick,
                "evidenceStartTick", -1);
            Scribe_Values.Look(ref firstObservedTick,
                "firstObservedTick", -1);
            Scribe_Values.Look(ref lastObservedTick,
                "lastObservedTick", -1);
            Scribe_Values.Look(ref observationCount,
                "observationCount", 0);
        }

        internal CACultureObservation Copy()
        {
            return new CACultureObservation
            {
                key = key,
                summary = summary,
                sourceOwner = sourceOwner,
                sourceDomain = sourceDomain,
                sourceSignature = sourceSignature,
                strength = strength,
                evidenceStartTick = evidenceStartTick,
                firstObservedTick = firstObservedTick,
                lastObservedTick = lastObservedTick,
                observationCount = observationCount
            };
        }
    }

    public sealed class CACultureEvidenceSnapshot : IExposable
    {
        public int tick = -1;
        public string population;
        public string spatial;
        public string social;
        public string institutional;
        public string political;
        public string material;
        public string signature;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref population, "population");
            Scribe_Values.Look(ref spatial, "spatial");
            Scribe_Values.Look(ref social, "social");
            Scribe_Values.Look(ref institutional, "institutional");
            Scribe_Values.Look(ref political, "political");
            Scribe_Values.Look(ref material, "material");
            Scribe_Values.Look(ref signature, "signature");
        }

        internal CACultureEvidenceSnapshot Copy()
        {
            return new CACultureEvidenceSnapshot
            {
                tick = tick,
                population = population,
                spatial = spatial,
                social = social,
                institutional = institutional,
                political = political,
                material = material,
                signature = signature
            };
        }
    }

    public sealed class CACultureTransition : IExposable
    {
        public int sequence;
        public int tick = -1;
        public string cause;
        public string summary;
        public string sourceSignature;
        public string predecessorCultureSignature;
        public string evidenceSignature;
        public string changedDomains;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sequence, "sequence", 0);
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref cause, "cause");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Values.Look(ref sourceSignature, "sourceSignature");
            Scribe_Values.Look(ref predecessorCultureSignature,
                "predecessorCultureSignature");
            Scribe_Values.Look(ref evidenceSignature,
                "evidenceSignature");
            Scribe_Values.Look(ref changedDomains, "changedDomains");
        }

        internal CACultureTransition Copy()
        {
            return new CACultureTransition
            {
                sequence = sequence,
                tick = tick,
                cause = cause,
                summary = summary,
                sourceSignature = sourceSignature,
                predecessorCultureSignature =
                    predecessorCultureSignature,
                evidenceSignature = evidenceSignature,
                changedDomains = changedDomains
            };
        }
    }

    // Inherited Culture is separate from Ideoligion, political belief, and
    // the practices an established settlement has actually developed. A
    // native CultureDef may supply a visual tradition; local Culture persists
    // what the population has lived through and changes only at explicit
    // historical transition boundaries.
    public sealed class CACulture : IExposable
    {
        public const int CurrentSchemaVersion = 5;
        public const int NameField = 1;
        public const int SourceCultureField = 2;
        // Schema-2 recipe bits remain constants only so migration can discard
        // them without confusing their old authorship with active fields.
        private const int LegacyPracticeFields = 8 | 16 | 32 | 64;
        public int schemaVersion = CurrentSchemaVersion;
        public string id;
        public string name;
        public string sourceCultureDefName;
        // A culture is a stable historical identity, not a generated prose
        // profile. Local cultures retain their inherited parent, place,
        // constituent populations, and explicit transitions.
        public string parentId;
        public string localityKey;
        public string temporalBasis;
        public CACultureMaturity maturity = CACultureMaturity.Inherited;
        public int formedTick = -1;
        public int lastTransitionTick = -1;
        public int revision;
        public string compositionSignature;
        public string lastTransitionCause;
        public List<CACultureConstituent> constituents =
            new List<CACultureConstituent>();
        public List<CACultureTransition> transitions =
            new List<CACultureTransition>();
        public List<CACulturePractice> practices =
            new List<CACulturePractice>();
        public List<CACultureObservation> observations =
            new List<CACultureObservation>();
        public CACultureEvidenceSnapshot lastEvidence;
        public string migrationEvidence;
        // Schema-2 migration inputs. They are read, cleared in Migrate, and
        // never consulted by generation, validation, presentation, or runtime.
        private string gatheringKey;
        private string hospitalityKey;
        private string mealsKey;
        private string remembranceKey;
        // Pre-B7 visual-source profiles were serialized as Culture presets.
        // Load that key only so migration can retain the identity/visual source
        // and discard the obsolete category boundary.
        internal string legacyPresetName;
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
            Scribe_Values.Look(ref parentId, "parentId");
            Scribe_Values.Look(ref localityKey, "localityKey");
            Scribe_Values.Look(ref temporalBasis, "temporalBasis");
            Scribe_Values.Look(ref maturity, "maturity",
                CACultureMaturity.Inherited);
            Scribe_Values.Look(ref formedTick, "formedTick", -1);
            Scribe_Values.Look(ref lastTransitionTick,
                "lastTransitionTick", -1);
            Scribe_Values.Look(ref revision, "revision", 0);
            Scribe_Values.Look(ref compositionSignature,
                "compositionSignature");
            Scribe_Values.Look(ref lastTransitionCause,
                "lastTransitionCause");
            Scribe_Collections.Look(ref constituents, "constituents",
                LookMode.Deep);
            Scribe_Collections.Look(ref transitions, "transitions",
                LookMode.Deep);
            Scribe_Collections.Look(ref practices, "practices",
                LookMode.Deep);
            Scribe_Collections.Look(ref observations, "observations",
                LookMode.Deep);
            Scribe_Deep.Look(ref lastEvidence, "lastEvidence");
            Scribe_Values.Look(ref migrationEvidence,
                "migrationEvidence");
            Scribe_Values.Look(ref gatheringKey, "gatheringKey");
            Scribe_Values.Look(ref hospitalityKey, "hospitalityKey");
            Scribe_Values.Look(ref mealsKey, "mealsKey");
            Scribe_Values.Look(ref remembranceKey, "remembranceKey");
            Scribe_Values.Look(ref legacyPresetName, "presetName");
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
                parentId = parentId,
                localityKey = localityKey,
                temporalBasis = temporalBasis,
                maturity = maturity,
                formedTick = formedTick,
                lastTransitionTick = lastTransitionTick,
                revision = revision,
                compositionSignature = compositionSignature,
                lastTransitionCause = lastTransitionCause,
                constituents = (constituents
                        ?? new List<CACultureConstituent>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
                transitions = (transitions
                        ?? new List<CACultureTransition>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
                practices = (practices ?? new List<CACulturePractice>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
                observations = (observations
                        ?? new List<CACultureObservation>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
                lastEvidence = lastEvidence?.Copy(),
                migrationEvidence = migrationEvidence,
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
            parentId = source.parentId;
            localityKey = source.localityKey;
            temporalBasis = source.temporalBasis;
            maturity = source.maturity;
            formedTick = source.formedTick;
            lastTransitionTick = source.lastTransitionTick;
            revision = source.revision;
            compositionSignature = source.compositionSignature;
            lastTransitionCause = source.lastTransitionCause;
            constituents = (source.constituents
                    ?? new List<CACultureConstituent>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
            transitions = (source.transitions
                    ?? new List<CACultureTransition>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
            practices = (source.practices
                    ?? new List<CACulturePractice>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
            observations = (source.observations
                    ?? new List<CACultureObservation>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
            lastEvidence = source.lastEvidence?.Copy();
            migrationEvidence = source.migrationEvidence;
            legacyPresetName = null;
            profileKey = source.profileKey;
            authoredMask = source.authoredMask & (NameField | SourceCultureField);
            presetMask = 0;
        }

        // Reusable profiles carry inherited authorship only. World identity,
        // locality, constituents, evidence, practices, and history belong to
        // the target world and are deliberately excluded.
        internal CACulture CopyAsInheritedTemplate()
        {
            return new CACulture
            {
                schemaVersion = CurrentSchemaVersion,
                name = name,
                sourceCultureDefName = sourceCultureDefName,
                maturity = CACultureMaturity.Inherited,
                authoredMask = authoredMask & (NameField
                    | SourceCultureField),
                presetMask = 0
            };
        }

        internal void ApplyInheritedTemplate(CACulture template)
        {
            if (template == null) return;
            name = template.name;
            sourceCultureDefName = template.sourceCultureDefName;
            authoredMask = template.authoredMask & (NameField
                | SourceCultureField);
            presetMask = 0;
            legacyPresetName = null;
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
            legacyPresetName = null;
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
            legacyPresetName = null;
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
            int priorSchema = culture.schemaVersion;
            CACultureLegacyMigrationResult migrated =
                CACultureLegacyMigrationKernel.Migrate(
                    new CACultureLegacyMigrationInput
                    {
                        SchemaVersion = culture.schemaVersion,
                        PresetName = culture.legacyPresetName,
                        Name = culture.name,
                        SourceCultureDefName = culture.sourceCultureDefName,
                        NameAuthored = culture.Authored(CACulture.NameField)
                    });
            culture.schemaVersion = migrated.SchemaVersion;
            culture.legacyPresetName = migrated.PresetName;
            culture.name = migrated.Name;
            culture.sourceCultureDefName = migrated.SourceCultureDefName;
            if (migrated.ClearLegacyPractices)
                culture.ClearLegacyPractices();
            if (culture.constituents == null)
                culture.constituents = new List<CACultureConstituent>();
            if (culture.transitions == null)
                culture.transitions = new List<CACultureTransition>();
            if (culture.practices == null)
                culture.practices = new List<CACulturePractice>();
            if (culture.observations == null)
                culture.observations = new List<CACultureObservation>();
            culture.constituents.RemoveAll(item => item == null);
            culture.transitions.RemoveAll(item => item == null);
            culture.practices.RemoveAll(item => item == null
                || item.key.NullOrEmpty());
            culture.observations.RemoveAll(item => item == null
                || item.key.NullOrEmpty() || item.sourceOwner.NullOrEmpty()
                || item.sourceDomain.NullOrEmpty());
            culture.revision = Math.Max(culture.revision,
                culture.transitions.Count == 0 ? 0
                    : culture.transitions.Max(item => item.sequence));
            culture.schemaVersion = CACulture.CurrentSchemaVersion;
            if (priorSchema > 0 && priorSchema < 5
                && culture.migrationEvidence.NullOrEmpty())
                culture.migrationEvidence = "B5/B6 cultural identity and "
                    + "visual inheritance preserved. No longitudinal "
                    + "practice or local history was invented during B7 "
                    + "migration.";
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
                    ? fallback.LabelCap + " culture"
                    : "Inherited culture";
            if (culture.constituents.Count == 0)
                culture.constituents.Add(new CACultureConstituent
                {
                    cultureId = culture.id,
                    label = culture.name,
                    share = 100,
                    inherited = true
                });
        }

        internal static string Summary(CACulture culture)
        {
            if (culture == null) return "Culture not set";
            CultureDef source = NativeDef(culture);
            string identity = CAAuthoringChoices.CultureIdentity(culture);
            string visual = source?.LabelCap.ToString()
                ?? (culture.sourceCultureDefName.NullOrEmpty()
                    ? "no visual tradition"
                    : "visual tradition unavailable");
            string continuity = culture.localityKey.NullOrEmpty()
                ? "inherited"
                : culture.maturity == CACultureMaturity.Established
                    ? "established at " + culture.localityKey
                    : "forming at " + culture.localityKey;
            string plurality = culture.constituents.Count <= 1 ? ""
                : " · " + culture.constituents.Count
                    + " cultural sources";
            string change = culture.transitions.Count == 0 ? ""
                : " · " + culture.transitions.Count + " recorded change"
                    + (culture.transitions.Count == 1 ? "" : "s");
            string practice = culture.practices.Count == 0 ? ""
                : " · " + culture.practices.Count + " retained practice"
                    + (culture.practices.Count == 1 ? "" : "s");
            return identity + " · " + continuity + plurality + change
                + practice
                + " · visual tradition: " + visual;
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

    internal static class CACultureHistory
    {
        private const int PracticeQualificationTicks = 60000;
        private const int LocalEstablishmentTicks = 10 * 60000;

        internal static CACulture EnsurePlayerLocalCulture(
            CACulture inherited, CACulture current, Map map,
            bool establishedStart, string temporalBasis, int formedTick)
        {
            if (map == null) return current;
            if (inherited == null) inherited = new CACulture();
            CACultureModel.EnsureGenerated(inherited,
                "player-culture:" + map.uniqueID,
                CACultureModel.NativeDef(inherited));
            string locality = "player-settlement:" + map.uniqueID;
            if (current != null && current.localityKey == locality)
                return current;
            CACulture local = inherited.Copy();
            string inheritedId = inherited.id;
            local.parentId = inheritedId;
            local.id = "culture:player-local:"
                + Math.Abs((long)GenText.StableStringHash(
                    (Find.World?.info?.persistentRandomValue ?? 0)
                        + ":" + map.uniqueID));
            local.localityKey = locality;
            local.name = LocalName(inherited.name);
            local.maturity = establishedStart
                ? CACultureMaturity.Established
                : CACultureMaturity.Forming;
            local.formedTick = establishedStart ? -1 : Math.Max(0, formedTick);
            local.lastTransitionTick = -1;
            local.revision = 0;
            local.transitions = new List<CACultureTransition>();
            local.observations = new List<CACultureObservation>();
            local.lastEvidence = null;
            local.temporalBasis = establishedStart
                ? (temporalBasis ?? "The scenario begins with an existing "
                    + "player settlement.")
                : "The founders brought an inherited culture to a new "
                    + "settlement.";
            local.constituents = new List<CACultureConstituent>
            {
                new CACultureConstituent
                {
                    cultureId = inheritedId,
                    label = inherited.name ?? "Founders' culture",
                    share = 100,
                    inherited = true
                }
            };
            local.compositionSignature = CompositionSignature(
                local.constituents);
            if (local.practices != null)
                foreach (CACulturePractice practice in local.practices)
                    if (practice != null)
                        practice.sourcePeriod = "carried into the landing";
            return local;
        }

        internal static void EnsureSettlementCulture(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            if (plan == null || settlement == null) return;
            List<CACultureConstituent> sources = Constituents(plan,
                settlement);
            string signature = CompositionSignature(sources);
            if (settlement.localCulture == null)
            {
                CACulture inherited = DominantCulture(plan, settlement);
                CACulture local = inherited?.Copy() ?? new CACulture();
                CACultureModel.EnsureGenerated(local,
                    (plan.regionalId ?? plan.candidateId ?? "region")
                        + ":settlement-culture:" + settlement.slot,
                    inherited == null ? null
                        : CACultureModel.NativeDef(inherited));
                local.parentId = inherited?.id;
                local.id = "culture:settlement:"
                    + Math.Abs((long)GenText.StableStringHash(
                        (plan.regionalId ?? plan.candidateId ?? "region")
                            + ":" + settlement.slot));
                local.localityKey = (plan.regionName
                        ?? plan.regionalId ?? "region")
                    + "/" + CARegionalPlanUtility.SettlementName(plan,
                        settlement);
                local.name = LocalName(inherited?.name ?? local.name);
                local.maturity = IsEstablished(settlement)
                    ? CACultureMaturity.Established
                    : CACultureMaturity.Forming;
                local.formedTick = -1;
                local.lastTransitionTick = -1;
                local.revision = 0;
                local.transitions = new List<CACultureTransition>();
                local.observations = new List<CACultureObservation>();
                local.lastEvidence = null;
                local.constituents = sources;
                local.compositionSignature = signature;
                local.temporalBasis = IsEstablished(settlement)
                    ? "This settlement and its local culture existed before "
                        + "the scenario began. No unobserved event history was "
                        + "invented during authoring."
                    : "This settlement begins forming its local culture when "
                        + "the scenario starts.";
                local.lastTransitionCause = null;
                settlement.localCulture = local;
                return;
            }

            CACultureModel.Migrate(settlement.localCulture);
            CACulture existing = settlement.localCulture;
            if (existing.temporalBasis.NullOrEmpty())
                existing.temporalBasis = IsEstablished(settlement)
                    ? "This settlement and its local culture existed before "
                        + "the scenario began. No unobserved event history was "
                        + "invented during authoring."
                    : "This settlement begins forming its local culture when "
                        + "the scenario starts.";
            int removedInventedHistory = existing.transitions.RemoveAll(
                item => item != null && item.cause
                    == "history before game start");
            if (removedInventedHistory > 0)
            {
                CACultureTransition latest = existing.transitions
                    .OrderByDescending(item => item.sequence).FirstOrDefault();
                existing.revision = latest?.sequence ?? 0;
                existing.lastTransitionTick = latest?.tick ?? -1;
                existing.lastTransitionCause = latest?.cause;
            }
            if (settlement.localCulture.compositionSignature == signature)
                return;
            // Authoring replaces the draft baseline. It is not lived history,
            // and two edit paths that end on the same population must produce
            // the same settlement state.
            settlement.localCulture.constituents = sources;
            settlement.localCulture.compositionSignature = signature;
            settlement.localCulture.lastEvidence = null;
            settlement.localCulture.observations.Clear();
        }

        internal static void MarkEstablished(CACulture culture,
            string cause, string summary, int tick = -1,
            string evidence = null)
        {
            if (culture == null) return;
            CACultureModel.Migrate(culture);
            if (culture.maturity == CACultureMaturity.Established) return;
            string predecessor = StateSignature(culture);
            culture.maturity = CACultureMaturity.Established;
            string successor = StateSignature(culture);
            Record(culture, cause ?? "local continuity established",
                summary ?? "Local practice became an established history.",
                successor, tick >= 0 ? tick : CurrentTick(), predecessor,
                evidence ?? culture.lastEvidence?.signature,
                "cultural continuity");
        }

        internal static int PracticeStrength(CACulture culture, string key)
        {
            if (culture?.practices == null || key.NullOrEmpty()) return 0;
            return culture.practices.Where(item => item != null
                    && item.key == key)
                .Select(item => Mathf.Clamp(item.strength, 0, 100))
                .DefaultIfEmpty(0).Max();
        }

        internal static int PracticeStrengthPrefix(CACulture culture,
            string prefix)
        {
            if (culture?.practices == null || prefix.NullOrEmpty()) return 0;
            return culture.practices.Where(item => item != null
                    && item.key?.StartsWith(prefix,
                        StringComparison.Ordinal) == true)
                .Select(item => Mathf.Clamp(item.strength, 0, 100))
                .DefaultIfEmpty(0).Max();
        }

        internal static bool EvaluateTransition(CACulture culture,
            CACultureEvidenceSnapshot current,
            IEnumerable<CACulturalPracticeEvidence> observedPractices,
            string scope, int tick)
        {
            if (culture == null || current == null) return false;
            CACultureModel.Migrate(culture);
            CACulturalHistoryEvidence previous = Evidence(
                culture.lastEvidence, null);
            List<CACulturalPracticeEvidence> raw =
                (observedPractices
                    ?? Enumerable.Empty<CACulturalPracticeEvidence>())
                .Where(item => item != null && !item.Key.NullOrEmpty()
                    && !item.SourceOwner.NullOrEmpty()
                    && !item.SourceDomain.NullOrEmpty()).ToList();
            List<CACulturalPracticeEvidence> qualified =
                UpdateObservations(culture, raw, tick);
            CACulturalHistoryEvidence evidence = Evidence(current,
                qualified);
            current.signature = evidence.StableSignature();
            List<CACulturalPracticeState> prior = culture.practices
                .Where(item => item != null).Select(item =>
                    new CACulturalPracticeState
                    {
                        Key = item.key,
                        Summary = item.summary,
                        Strength = item.strength,
                        SourceSignature = item.sourceSignature
                    }).ToList();
            string predecessor = StateSignature(culture);
            CACulturalTransitionEvaluation evaluated =
                CACultureLongitudinalKernel.Evaluate(previous, evidence,
                    prior, predecessor);
            var qualifiedKeys = new HashSet<string>(qualified.Select(item =>
                item.Key), StringComparer.Ordinal);
            foreach (CACulturePractice practice in culture.practices
                .Where(item => item != null
                    && qualifiedKeys.Contains(item.key)))
                practice.lastObservedTick = tick;
            if (culture.lastEvidence == null)
            {
                culture.lastEvidence = current.Copy();
                return TryEstablishLocalContinuity(culture, tick,
                    current.signature);
            }
            culture.lastEvidence = current.Copy();
            if (evaluated.Changed)
            {
                var priorByKey = culture.practices
                    .Where(item => item != null)
                    .ToDictionary(item => item.key, item => item,
                        StringComparer.Ordinal);
                culture.practices = evaluated.Practices.Select(item =>
                {
                    CACulturePractice old;
                    priorByKey.TryGetValue(item.Key, out old);
                    return new CACulturePractice
                    {
                        key = item.Key,
                        summary = item.Summary,
                        strength = item.Strength,
                        firstRecordedTick = old?.firstRecordedTick ?? tick,
                        lastObservedTick = qualified.Any(value =>
                            value.Key == item.Key) ? tick
                                : old?.lastObservedTick ?? -1,
                        sourceSignature = item.SourceSignature,
                        sourcePeriod = old?.sourcePeriod
                            ?? (scope ?? "lived history")
                    };
                }).ToList();
                string domains = string.Join(", ",
                    evaluated.ChangedDomains);
                Record(culture, "lived history evaluated",
                    domains.NullOrEmpty()
                        ? "Lived practice changed over time."
                        : "Recorded change in " + domains + ".",
                    evaluated.TransitionSignature, tick, predecessor,
                    evaluated.EvidenceSignature, domains);
            }
            if (culture.maturity == CACultureMaturity.Inherited)
                culture.maturity = CACultureMaturity.Forming;
            bool established = TryEstablishLocalContinuity(culture, tick,
                evaluated.EvidenceSignature);
            return evaluated.Changed || established;
        }

        private static bool TryEstablishLocalContinuity(CACulture culture,
            int tick, string evidenceSignature)
        {
            if (culture == null
                || culture.maturity != CACultureMaturity.Forming
                || culture.formedTick < 0
                || tick - culture.formedTick < LocalEstablishmentTicks)
                return false;
            var practiceKeys = new HashSet<string>((culture.practices
                    ?? new List<CACulturePractice>())
                .Where(item => item != null && item.strength > 0)
                .Select(item => item.key), StringComparer.Ordinal);
            bool sustained = (culture.observations
                    ?? new List<CACultureObservation>())
                .Where(item => item != null
                    && practiceKeys.Contains(item.key)
                    && item.observationCount >= 2)
                .Any(item =>
                {
                    int beginning = item.evidenceStartTick >= 0
                        ? Math.Min(item.firstObservedTick,
                            item.evidenceStartTick)
                        : item.firstObservedTick;
                    return beginning >= 0
                        && tick - Math.Max(culture.formedTick, beginning)
                            >= LocalEstablishmentTicks;
                });
            if (!sustained) return false;
            MarkEstablished(culture, "local continuity established",
                "Sustained local practice has become established over the "
                    + "settlement's lived history.", tick,
                evidenceSignature);
            return true;
        }

        private static List<CACulturalPracticeEvidence> UpdateObservations(
            CACulture culture, List<CACulturalPracticeEvidence> raw,
            int tick)
        {
            if (culture.observations == null)
                culture.observations = new List<CACultureObservation>();
            var current = raw.GroupBy(item => ObservationIdentity(item),
                    StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(item => item.Strength)
                    .First()).ToList();
            var currentIds = new HashSet<string>(current.Select(
                ObservationIdentity), StringComparer.Ordinal);
            foreach (CACultureObservation absent in culture.observations
                .Where(item => item != null
                    && !currentIds.Contains(ObservationIdentity(item))))
            {
                // A broken run of practice evidence must qualify again from
                // the new observation period; old observations remain only as
                // a typed audit record.
                absent.evidenceStartTick = -1;
                absent.firstObservedTick = -1;
                absent.lastObservedTick = -1;
                absent.observationCount = 0;
            }
            foreach (CACulturalPracticeEvidence evidence in current)
            {
                string identity = ObservationIdentity(evidence);
                CACultureObservation observation = culture.observations
                    .FirstOrDefault(item => item != null
                        && ObservationIdentity(item) == identity);
                if (observation == null)
                {
                    observation = new CACultureObservation
                    {
                        key = evidence.Key,
                        summary = evidence.Summary,
                        sourceOwner = evidence.SourceOwner,
                        sourceDomain = evidence.SourceDomain,
                        sourceSignature = evidence.SourceSignature,
                        strength = Mathf.Clamp(evidence.Strength, 0, 100),
                        evidenceStartTick = evidence.EvidenceStartTick,
                        firstObservedTick = tick,
                        lastObservedTick = tick,
                        observationCount = 1
                    };
                    culture.observations.Add(observation);
                    continue;
                }
                observation.summary = evidence.Summary;
                observation.sourceSignature = evidence.SourceSignature;
                observation.strength = Mathf.Clamp(evidence.Strength, 0, 100);
                if (evidence.EvidenceStartTick >= 0
                    && (observation.evidenceStartTick < 0
                        || evidence.EvidenceStartTick
                            < observation.evidenceStartTick))
                    observation.evidenceStartTick = evidence.EvidenceStartTick;
                if (observation.lastObservedTick != tick)
                {
                    if (observation.firstObservedTick < 0)
                        observation.firstObservedTick = tick;
                    observation.lastObservedTick = tick;
                    observation.observationCount++;
                }
            }

            var result = new List<CACulturalPracticeEvidence>();
            foreach (CACultureObservation observation in culture.observations
                .Where(item => item != null
                    && currentIds.Contains(ObservationIdentity(item))))
            {
                int beginning = observation.evidenceStartTick >= 0
                    ? Math.Min(observation.firstObservedTick,
                        observation.evidenceStartTick)
                    : observation.firstObservedTick;
                if (observation.observationCount < 2 || beginning < 0
                    || tick - beginning < PracticeQualificationTicks)
                    continue;
                result.Add(new CACulturalPracticeEvidence
                {
                    Key = observation.key,
                    Summary = observation.summary,
                    Strength = observation.strength,
                    SourceOwner = observation.sourceOwner,
                    SourceDomain = observation.sourceDomain,
                    SourceSignature = observation.sourceSignature,
                    EvidenceStartTick = beginning,
                    ObservedTick = tick
                });
            }
            return result;
        }

        private static string ObservationIdentity(
            CACulturalPracticeEvidence evidence)
        {
            return (evidence?.Key ?? "") + "|"
                + (evidence?.SourceOwner ?? "") + "|"
                + (evidence?.SourceDomain ?? "");
        }

        private static string ObservationIdentity(CACultureObservation value)
        {
            return (value?.key ?? "") + "|"
                + (value?.sourceOwner ?? "") + "|"
                + (value?.sourceDomain ?? "");
        }

        internal static string StateSignature(CACulture culture)
        {
            if (culture == null) return "unrecorded";
            return new CACulturalPersistedStateInput
            {
                Identity = culture.id,
                ParentIdentity = culture.parentId,
                Locality = culture.localityKey,
                Maturity = culture.maturity.ToString(),
                Revision = culture.revision,
                CompositionSignature = culture.compositionSignature,
                VisualTradition = culture.sourceCultureDefName,
                Practices = (culture.practices
                        ?? new List<CACulturePractice>())
                    .Where(item => item != null)
                    .Select(item => new CACulturalPracticeState
                    {
                        Key = item.key,
                        Summary = item.summary,
                        Strength = item.strength,
                        SourceSignature = item.sourceSignature
                    }).ToList(),
                Transitions = (culture.transitions
                        ?? new List<CACultureTransition>())
                    .Where(item => item != null)
                    .Select(item => new CACulturalTransitionState
                    {
                        Sequence = item.sequence,
                        Tick = item.tick,
                        Cause = item.cause,
                        Summary = item.summary,
                        SuccessorSignature = item.sourceSignature,
                        PredecessorCultureSignature =
                            item.predecessorCultureSignature,
                        EvidenceSignature = item.evidenceSignature,
                        ChangedDomains = item.changedDomains
                    }).ToList()
            }.HistoricalSignature();
        }

        internal static string PracticeSummary(CACulture culture)
        {
            CACulturePractice[] practices = (culture?.practices
                    ?? new List<CACulturePractice>())
                .Where(item => item != null && item.strength > 0)
                .OrderByDescending(item => item.strength)
                .ThenBy(item => item.key).ToArray();
            return practices.Length == 0
                ? "No lived practice has crossed a historical transition."
                : string.Join("\n", practices.Select(item =>
                    item.summary).Distinct().ToArray());
        }

        internal static string ContinuitySummary(CACulture culture)
        {
            if (culture == null) return "No local culture is recorded.";
            CACultureConstituent inheritedSource = culture.constituents?
                .Where(item => item != null && item.inherited
                    && !item.label.NullOrEmpty())
                .OrderByDescending(item => item.share).FirstOrDefault();
            string inherited = inheritedSource != null
                ? "Inherited from " + inheritedSource.label
                : culture.parentId.NullOrEmpty()
                    ? "No inherited source is recorded"
                    : "Inherited from an earlier culture";
            string maturity = culture.maturity
                == CACultureMaturity.Established ? "established"
                : culture.maturity == CACultureMaturity.Forming
                    ? "forming" : "inherited";
            CACultureTransition latest = culture.transitions?
                .Where(item => item != null)
                .OrderByDescending(item => item.sequence).FirstOrDefault();
            string transition = latest == null
                ? "no later change is recorded"
                : "latest change: " + (latest.summary
                    ?? latest.cause ?? "local practice changed");
            return inherited + "; " + maturity + "; " + transition + ".";
        }

        private static void Record(CACulture culture, string cause,
            string summary, string signature, int tick,
            string predecessor = null, string evidence = null,
            string changedDomains = null)
        {
            if (culture.transitions == null)
                culture.transitions = new List<CACultureTransition>();
            int sequence = culture.transitions.Count == 0 ? 1
                : culture.transitions.Max(item => item?.sequence ?? 0) + 1;
            culture.transitions.Add(new CACultureTransition
            {
                sequence = sequence,
                tick = tick,
                cause = cause,
                summary = summary,
                sourceSignature = signature,
                predecessorCultureSignature = predecessor,
                evidenceSignature = evidence,
                changedDomains = changedDomains
            });
            culture.revision = sequence;
            culture.lastTransitionTick = tick;
            culture.lastTransitionCause = cause;
        }

        private static List<CACultureConstituent> Constituents(
            CARegionalPlan plan, CARegionalSettlementPlan settlement)
        {
            var result = new Dictionary<string, CACultureConstituent>();
            foreach (CASettlementPopulationGroup population in
                settlement.populationGroups
                    ?? new List<CASettlementPopulationGroup>())
            {
                if (population == null || population.share <= 0) continue;
                int sourceKey = population.factionKey >= 0
                    ? population.factionKey : settlement.factionKey;
                CACulture source = population.kind
                        == CAPopulationGroupKind.Unaffiliated
                    ? null : plan.FactionPlan(sourceKey)?.culture;
                string identity = source?.id
                    ?? "culture:unrecorded:group:" + population.key;
                CACultureConstituent entry;
                if (!result.TryGetValue(identity, out entry))
                {
                    entry = new CACultureConstituent
                    {
                        cultureId = source?.id,
                        label = source?.name ?? "Culture not recorded",
                        inherited = true
                    };
                    result.Add(identity, entry);
                }
                entry.share += population.share;
                entry.separateQuarter |= population.quarter;
            }
            if (result.Count == 0)
            {
                CACulture source = DominantCulture(plan, settlement);
                result.Add(source?.id ?? "culture:unrecorded", new
                    CACultureConstituent
                    {
                        cultureId = source?.id,
                        label = source?.name ?? "Culture not recorded",
                        share = 100,
                        inherited = true
                    });
            }
            return result.Values.OrderByDescending(item => item.share)
                .ThenBy(item => item.label).ToList();
        }

        private static CACulture DominantCulture(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            CASettlementPopulationGroup dominant = settlement
                .populationGroups?.Where(item => item != null)
                .OrderByDescending(item => item.share).FirstOrDefault();
            int key = dominant != null && dominant.factionKey >= 0
                ? dominant.factionKey : settlement.factionKey;
            return plan?.FactionPlan(key)?.culture
                ?? plan?.FactionPlan(settlement.factionKey)?.culture;
        }

        private static string CompositionSignature(
            IEnumerable<CACultureConstituent> sources)
        {
            return CACulturalExpressionCausalKernel.Signature((sources
                    ?? Enumerable.Empty<CACultureConstituent>())
                .Where(item => item != null)
                .OrderBy(item => item.cultureId ?? item.label)
                .Select(item => (item.cultureId ?? "unrecorded") + ":"
                    + item.share + ":" + (item.separateQuarter
                        ? "separate" : "shared")));
        }

        private static CACulturalHistoryEvidence Evidence(
            CACultureEvidenceSnapshot snapshot,
            IEnumerable<CACulturalPracticeEvidence> practices)
        {
            if (snapshot == null) return null;
            return new CACulturalHistoryEvidence
            {
                Tick = snapshot.tick,
                RecordedSignature = practices == null
                    ? snapshot.signature : null,
                Population = snapshot.population,
                Spatial = snapshot.spatial,
                Social = snapshot.social,
                Institutional = snapshot.institutional,
                Political = snapshot.political,
                Material = snapshot.material,
                Practices = (practices
                    ?? Enumerable.Empty<CACulturalPracticeEvidence>())
                    .Where(item => item != null).ToList()
            };
        }

        private static string LocalName(string inherited)
        {
            if (inherited.NullOrEmpty()) return "Local culture";
            if (inherited.EndsWith(" background",
                    StringComparison.OrdinalIgnoreCase))
                return inherited.Substring(0,
                    inherited.Length - " background".Length) + " culture";
            return inherited.EndsWith(" culture",
                StringComparison.OrdinalIgnoreCase)
                ? inherited : inherited + " culture";
        }

        private static bool IsEstablished(
            CARegionalSettlementPlan settlement)
        {
            // Every non-player settlement authored on the Starting Region
            // surface is an existing society at the scenario boundary. Its
            // exact past may be unrecorded, but it is not a new player
            // landing. Population-pool provenance and a generic development
            // score cannot change that temporal fact.
            return settlement != null;
        }

        private static int CurrentTick()
        {
            try { return Find.TickManager?.TicksGame ?? -1; }
            catch { return -1; }
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
                ? "Set what this population considers proper: government, "
                    + "participation, property, membership, support, and "
                    + "conflict. Existing institutions may agree or differ."
                : "Political beliefs state what should be proper. Current "
                    + "structure records what this faction actually does; "
                    + "each remains independent and disagreement is preserved.";
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
                float half = (width - 12f) / 2f;
                Text.Font = GameFont.Tiny;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(0f, y, half, 20f),
                    "Preferred position");
                Widgets.Label(new Rect(half + 12f, y, half, 20f),
                    "Current institution");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 22f;
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
            if (selected != null)
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
                "Culture");
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
            string description = "Set the Culture this population inherits. "
                + "Settlements retain that history and develop lived "
                + "practices through play. Visual tradition is optional.";
            float descriptionHeight = Text.CalcHeight(description, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, descriptionHeight),
                description);
            y += descriptionHeight + 12f;

            y = DrawActions(inRect, y);
            string[] tabs = { "Culture", "Visual tradition" };
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
            if (section == 0) DrawCulture(ref rowY, view.width);
            else DrawVisual(ref rowY, view.width);
            viewHeight = rowY + 12f;
            Widgets.EndScrollView();
        }

        private float DrawActions(Rect inRect, float y)
        {
            string[] labels = { "Saved Cultures...", "Save Culture...",
                "Manage saved..." };
            Action[] actions = { OpenCultureProfiles, SaveProfile,
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

        private void DrawCulture(ref float y, float width)
        {
            Row(ref y, width, "Name",
                culture.name ?? "Inherited Culture",
                () => Find.WindowStack.Add(new Dialog_CARenameCulture(
                    culture, changed)), FieldState(CACulture.NameField));
            if (!culture.profileKey.NullOrEmpty())
            {
                CACreationUI.DrawChip(new Rect(0f, y + 3f,
                    Mathf.Min(width, 250f), 20f), "Saved Culture",
                    CACreationUI.Preset);
                y += 30f;
            }
            DrawExplanation(ref y, width,
                "This is the inherited historical identity. Lived practices "
                + "are recorded as settlements change; the visual tradition "
                + "is edited separately.");
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

        private void OpenCultureProfiles()
        {
            CACreationUI.OpenChoices("Saved Cultures",
                "Use a saved Culture. Native RimWorld style sources are "
                    + "chosen separately under Visual tradition.",
                CAAuthoringChoices.CultureProfiles(culture,
                    culture.id ?? factionLabel ?? "ca-culture", changed));
        }

        private void SaveProfile()
        {
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Save Culture",
                culture.name ?? "Saved Culture", value =>
                {
                    CAUserCultureProfile saved =
                        CAAuthoringProfileLibrary.SaveCulture(value, culture);
                    if (saved != null)
                        culture.profileKey = saved.key;
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
