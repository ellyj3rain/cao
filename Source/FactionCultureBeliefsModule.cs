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
        public bool ideoligionProtected;

        public void ExposeData()
        {
            Scribe_Values.Look(ref cultureId, "cultureId");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref share, "share", 0);
            Scribe_Values.Look(ref inherited, "inherited", true);
            Scribe_Values.Look(ref ideoligionProtected,
                "ideoligionProtected", false);
        }

        internal CACultureConstituent Copy()
        {
            return new CACultureConstituent
            {
                cultureId = cultureId,
                label = label,
                share = share,
                inherited = inherited,
                ideoligionProtected = ideoligionProtected
            };
        }
    }

    // One durable interpretation of one concrete social subject. Culture may
    // carry several records for the same subject when constituent populations
    // disagree; scope is therefore part of the record's identity.
    public sealed class CACulturalMeaning : IExposable
    {
        public string subjectKey;
        public string populationScope;
        public int approval;
        public int normality;
        public int prestige;
        public int salience;
        public string provenance;
        public string sourceIdentity;
        public string evidenceSignature;
        public int firstRecordedTick = -1;
        public int lastChangedTick = -1;
        public int weight = 100;

        public void ExposeData()
        {
            Scribe_Values.Look(ref subjectKey, "subjectKey");
            Scribe_Values.Look(ref populationScope, "populationScope");
            Scribe_Values.Look(ref approval, "approval", 0);
            Scribe_Values.Look(ref normality, "normality", 0);
            Scribe_Values.Look(ref prestige, "prestige", 0);
            Scribe_Values.Look(ref salience, "salience", 0);
            Scribe_Values.Look(ref provenance, "provenance");
            Scribe_Values.Look(ref sourceIdentity, "sourceIdentity");
            Scribe_Values.Look(ref evidenceSignature,
                "evidenceSignature");
            Scribe_Values.Look(ref firstRecordedTick,
                "firstRecordedTick", -1);
            Scribe_Values.Look(ref lastChangedTick,
                "lastChangedTick", -1);
            Scribe_Values.Look(ref weight, "weight", 100);
        }

        internal CACulturalMeaning Copy()
        {
            return new CACulturalMeaning
            {
                subjectKey = subjectKey,
                populationScope = populationScope,
                approval = approval,
                normality = normality,
                prestige = prestige,
                salience = salience,
                provenance = provenance,
                sourceIdentity = sourceIdentity,
                evidenceSignature = evidenceSignature,
                firstRecordedTick = firstRecordedTick,
                lastChangedTick = lastChangedTick,
                weight = weight
            };
        }

        internal CACulturalMeaningState ToState()
        {
            return new CACulturalMeaningState
            {
                SubjectKey = subjectKey,
                PopulationScope = populationScope,
                Approval = approval,
                Normality = normality,
                Prestige = prestige,
                Salience = salience,
                Provenance = provenance,
                SourceIdentity = sourceIdentity,
                EvidenceSignature = evidenceSignature,
                FirstRecordedTick = firstRecordedTick,
                LastChangedTick = lastChangedTick,
                Weight = weight
            };
        }

        internal static CACulturalMeaning FromState(
            CACulturalMeaningState value)
        {
            return value == null ? null : new CACulturalMeaning
            {
                subjectKey = value.SubjectKey,
                populationScope = value.PopulationScope,
                approval = value.Approval,
                normality = value.Normality,
                prestige = value.Prestige,
                salience = value.Salience,
                provenance = value.Provenance,
                sourceIdentity = value.SourceIdentity,
                evidenceSignature = value.EvidenceSignature,
                firstRecordedTick = value.FirstRecordedTick,
                lastChangedTick = value.LastChangedTick,
                weight = value.Weight
            };
        }
    }

    public sealed class CACulturePractice : IExposable
    {
        // A practice is concrete repeated conduct. Its definition names the
        // activity, actors, trigger, cadence, operator, authority, setting,
        // material conditions, evidence adapter, consumers, and implicated
        // social subjects. Meanings continue to own subjectKey separately.
        public string practiceKey;
        public string summary;
        public int strength;
        public int firstRecordedTick = -1;
        public int lastObservedTick = -1;
        public string sourceOwner;
        public string sourceSignature;
        public string sourcePeriod;
        private string b10SubjectKey;

        public void ExposeData()
        {
            Scribe_Values.Look(ref practiceKey, "practiceKey");
            if (Scribe.mode == LoadSaveMode.LoadingVars
                && practiceKey.NullOrEmpty())
                Scribe_Values.Look(ref b10SubjectKey, "subjectKey");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Values.Look(ref strength, "strength", 0);
            Scribe_Values.Look(ref firstRecordedTick,
                "firstRecordedTick", -1);
            Scribe_Values.Look(ref lastObservedTick,
                "lastObservedTick", -1);
            Scribe_Values.Look(ref sourceOwner, "sourceOwner");
            Scribe_Values.Look(ref sourceSignature,
                "sourceSignature");
            Scribe_Values.Look(ref sourcePeriod, "sourcePeriod");
            // The containing campaign owner performs evidence-gated B10
            // conversion after the complete Culture record validates.
        }

        internal string LegacyB10SubjectKey => b10SubjectKey;

        internal CACulturePractice Copy()
        {
            return new CACulturePractice
            {
                practiceKey = practiceKey,
                summary = summary,
                strength = strength,
                firstRecordedTick = firstRecordedTick,
                lastObservedTick = lastObservedTick,
                sourceOwner = sourceOwner,
                sourceSignature = sourceSignature,
                sourcePeriod = sourcePeriod,
                b10SubjectKey = b10SubjectKey
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
        public string changedSubjectKeys;
        public string changedDimensions;
        public string populationScope;
        public string successorCultureSignature;

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
            Scribe_Values.Look(ref changedSubjectKeys,
                "changedSubjectKeys");
            Scribe_Values.Look(ref changedDimensions,
                "changedDimensions");
            Scribe_Values.Look(ref populationScope, "populationScope");
            Scribe_Values.Look(ref successorCultureSignature,
                "successorCultureSignature");
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
                changedSubjectKeys = changedSubjectKeys,
                changedDimensions = changedDimensions,
                populationScope = populationScope,
                successorCultureSignature = successorCultureSignature
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
        public const int CurrentSchemaVersion = 9;
        public const int NameField = 1;
        public const int SourceCultureField = 2;
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
        public List<CACulturalMeaning> inheritedMeanings =
            new List<CACulturalMeaning>();
        public List<CACulturalMeaning> localMeanings =
            new List<CACulturalMeaning>();
        public List<CACultureTransition> transitions =
            new List<CACultureTransition>();
        public List<CACulturePractice> inheritedPractices =
            new List<CACulturePractice>();
        public List<CACulturePractice> practices =
            new List<CACulturePractice>();
        public List<CACultureObservation> observations =
            new List<CACultureObservation>();
        public CACultureEvidenceSnapshot lastEvidence;
        public int authoredMask;

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
            Scribe_Collections.Look(ref inheritedMeanings,
                "inheritedMeanings", LookMode.Deep);
            Scribe_Collections.Look(ref localMeanings,
                "localMeanings", LookMode.Deep);
            Scribe_Collections.Look(ref transitions, "transitions",
                LookMode.Deep);
            Scribe_Collections.Look(ref inheritedPractices,
                "inheritedPractices", LookMode.Deep);
            Scribe_Collections.Look(ref practices, "practices",
                LookMode.Deep);
            Scribe_Collections.Look(ref observations, "observations",
                LookMode.Deep);
            Scribe_Deep.Look(ref lastEvidence, "lastEvidence");
            Scribe_Values.Look(ref authoredMask, "authoredMask", 0);
            // Live migration is owned by the containing campaign component.
            // Opening nested state never repairs or stamps it current here.
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
                inheritedMeanings = CopyMeanings(inheritedMeanings),
                localMeanings = CopyMeanings(localMeanings),
                transitions = (transitions
                        ?? new List<CACultureTransition>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
                inheritedPractices = (inheritedPractices
                        ?? new List<CACulturePractice>())
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
                authoredMask = authoredMask & (NameField | SourceCultureField)
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
            inheritedMeanings = CopyMeanings(source.inheritedMeanings);
            localMeanings = CopyMeanings(source.localMeanings);
            transitions = (source.transitions
                    ?? new List<CACultureTransition>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
            inheritedPractices = (source.inheritedPractices
                    ?? new List<CACulturePractice>())
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
            authoredMask = source.authoredMask & (NameField | SourceCultureField);
        }

        // Reusable profiles carry inherited meaning and practice only. World
        // identity, locality, constituents, observations, and transitions
        // belong to the target world and are deliberately excluded.
        internal CACulture CopyAsInheritedTemplate()
        {
            return new CACulture
            {
                schemaVersion = CurrentSchemaVersion,
                name = name,
                sourceCultureDefName = sourceCultureDefName,
                maturity = CACultureMaturity.Inherited,
                inheritedMeanings = CopyMeanings(inheritedMeanings),
                inheritedPractices = (inheritedPractices
                        ?? new List<CACulturePractice>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
                authoredMask = authoredMask & (NameField
                    | SourceCultureField)
            };
        }

        internal void ApplyInheritedTemplate(CACulture template)
        {
            if (template == null) return;
            name = template.name;
            sourceCultureDefName = template.sourceCultureDefName;
            inheritedMeanings = CopyMeanings(template.inheritedMeanings);
            inheritedPractices = (template.inheritedPractices
                    ?? new List<CACulturePractice>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
            authoredMask = template.authoredMask & (NameField
                | SourceCultureField);
        }

        internal bool Authored(int field)
        {
            return (authoredMask & field) != 0;
        }

        internal void Choose(int field, string value)
        {
            Set(field, value);
            authoredMask |= field;
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

        private static List<CACulturalMeaning> CopyMeanings(
            IEnumerable<CACulturalMeaning> source)
        {
            return (source ?? Enumerable.Empty<CACulturalMeaning>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
        }
    }

    public sealed class CAPoliticalDerivationReceipt : IExposable
    {
        public string axisKey;
        public string selectedOptionKey;
        public string scores;
        public string evidence;
        public bool tieBroken;

        public void ExposeData()
        {
            Scribe_Values.Look(ref axisKey, "axisKey");
            Scribe_Values.Look(ref selectedOptionKey, "selectedOptionKey");
            Scribe_Values.Look(ref scores, "scores");
            Scribe_Values.Look(ref evidence, "evidence");
            Scribe_Values.Look(ref tieBroken, "tieBroken", false);
        }

        internal CAPoliticalDerivationReceipt Copy()
        {
            return (CAPoliticalDerivationReceipt)MemberwiseClone();
        }
    }

    // Political beliefs and current order use the same questions so a
    // disagreement is readable without collapsing the two persisted states.
    public sealed class CAPoliticalBeliefs : IExposable
    {
        public const int CurrentSchemaVersion = 9;
        public int schemaVersion = CurrentSchemaVersion;
        public string id;
        public List<CAAxisEntry> positions = new List<CAAxisEntry>();
        public List<CAPoliticalDerivationReceipt> derivationReceipts =
            new List<CAPoliticalDerivationReceipt>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref id, "id");
            Scribe_Collections.Look(ref positions, "positions", LookMode.Deep);
            Scribe_Collections.Look(ref derivationReceipts,
                "derivationReceipts", LookMode.Deep);
            // Campaign owners perform B10 migration after the complete nested
            // record has loaded and passed source-schema validation. Opening a
            // record never normalizes it in place.
        }

        internal CAPoliticalBeliefs Copy()
        {
            return new CAPoliticalBeliefs
            {
                schemaVersion = schemaVersion,
                id = id,
                positions = CAFactionStartingState.CopyAxes(positions),
                derivationReceipts = derivationReceipts.Where(item =>
                    item != null).Select(item => item.Copy()).ToList()
            };
        }

        internal void CopyFrom(CAPoliticalBeliefs source)
        {
            if (source == null) return;
            schemaVersion = CurrentSchemaVersion;
            id = source.id;
            positions = CAFactionStartingState.CopyAxes(source.positions);
            derivationReceipts = (source.derivationReceipts
                    ?? new List<CAPoliticalDerivationReceipt>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
        }
    }

    internal static class CACultureModel
    {
        internal static string SubstantiveFailure(CACulture culture)
        {
            if (culture == null) return "No culture is recorded.";
            Normalize(culture);
            return ValidationFailure(culture, requireSubstantive: true);
        }

        // Pure validation for durable owners. It does not prune, generate,
        // deduplicate, or otherwise repair serialized state during load.
        internal static string ValidationFailure(CACulture culture,
            bool requireSubstantive)
        {
            return ValidationFailure(culture, requireSubstantive,
                CACulture.CurrentSchemaVersion, validatePractices: true);
        }

        private static string ValidationFailure(CACulture culture,
            bool requireSubstantive, int expectedSchema,
            bool validatePractices)
        {
            if (culture == null) return "Culture is missing";
            if (culture.schemaVersion != expectedSchema)
                return "schema is " + culture.schemaVersion + ", expected "
                    + expectedSchema;
            if (culture.id.NullOrEmpty()) return "identity is missing";
            if (!Enum.IsDefined(typeof(CACultureMaturity), culture.maturity))
                return "maturity is invalid";
            if (culture.formedTick < -1 || culture.lastTransitionTick < -1
                || culture.revision < 0)
                return "timeline counters are invalid";
            if (culture.constituents == null
                || culture.transitions == null
                || culture.observations == null
                || culture.inheritedMeanings == null
                || culture.localMeanings == null
                || culture.inheritedPractices == null
                || culture.practices == null)
                return "semantic collections are incomplete";
            if (culture.constituents.Any(item => item == null
                    || item.label.NullOrEmpty() || item.share < 0
                    || item.share > 100))
                return "a Culture constituent is incomplete or out of range";
            if (culture.constituents.Count > 0
                && culture.constituents.Sum(item => item.share) != 100)
                return "Culture constituent shares do not total 100";
            if (culture.constituents.GroupBy(item => item.cultureId
                        ?? "label:" + item.label, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
                return "Culture constituent identity is duplicated";
            List<CACulturalMeaning> meanings = culture.inheritedMeanings
                .Concat(culture.localMeanings).ToList();
            if (meanings.Any(item => item == null
                    || !CASocialSubjectRegistry.ValidKey(item.subjectKey)
                    || item.approval < -100 || item.approval > 100
                    || item.normality < 0 || item.normality > 100
                    || item.prestige < -100 || item.prestige > 100
                    || item.salience < 0 || item.salience > 100
                    || item.weight < 1 || item.weight > 100
                    || item.firstRecordedTick < -1
                    || item.lastChangedTick < -1))
                return "a social meaning has an invalid referent";
            if (culture.inheritedMeanings.GroupBy(MeaningIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1)
                || culture.localMeanings.GroupBy(MeaningIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "a social meaning identity is duplicated";
            var inheritedMeaningKeys = new HashSet<string>(
                culture.inheritedMeanings.Select(MeaningIdentity),
                StringComparer.Ordinal);
            if (culture.localMeanings.Any(item => inheritedMeaningKeys
                    .Contains(MeaningIdentity(item))))
                return "a local meaning duplicates its inherited predecessor";
            if (validatePractices && culture.inheritedPractices
                .Concat(culture.practices)
                .Any(item => item == null
                    || CACulturalPracticeRegistry.Find(item.practiceKey)
                        == null
                    || item.sourceOwner.NullOrEmpty()
                    || item.summary.NullOrEmpty()
                    || item.sourceSignature.NullOrEmpty()
                    || item.strength < 0 || item.strength > 100
                    || item.firstRecordedTick < -1
                    || item.lastObservedTick < -1))
                return "a cultural practice has no supported identity or owner";
            if (validatePractices && (culture.inheritedPractices.GroupBy(
                        PracticeIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1)
                || culture.practices.GroupBy(PracticeIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1)))
                return "a cultural practice identity is duplicated";
            if (culture.transitions.Any(item => item == null
                    || item.sequence < 1 || item.tick < -1
                    || item.cause.NullOrEmpty() || item.summary.NullOrEmpty()))
                return "a Culture transition is incomplete";
            if (culture.transitions.GroupBy(item => item.sequence)
                    .Any(group => group.Count() > 1))
                return "a Culture transition sequence is duplicated";
            if (culture.transitions.Count > 0 && culture.revision
                < culture.transitions.Max(item => item.sequence))
                return "Culture revision precedes its transition ledger";
            if (validatePractices && culture.observations.Any(item => item == null
                    || CACulturalPracticeRegistry.Find(item.key) == null
                    || item.sourceOwner.NullOrEmpty()
                    || item.sourceDomain.NullOrEmpty()
                    || item.sourceSignature.NullOrEmpty()
                    || item.strength < 0 || item.strength > 100
                    || item.evidenceStartTick < -1
                    || item.firstObservedTick < -1
                    || item.lastObservedTick < -1
                    || item.observationCount < 0))
                return "a Culture observation is incomplete";
            if (validatePractices && culture.observations.GroupBy(
                        ObservationIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "a Culture observation identity is duplicated";
            if (culture.lastEvidence != null
                && (culture.lastEvidence.tick < -1
                    || culture.lastEvidence.signature.NullOrEmpty()))
                return "the last Culture evidence snapshot is incomplete";
            if (requireSubstantive
                && culture.inheritedMeanings.Count == 0
                && culture.localMeanings.Count == 0
                && culture.inheritedPractices.Count == 0
                && culture.practices.Count == 0)
                return "Compose Culture with at least one social meaning or "
                    + "inherited practice.";
            return null;
        }

        internal static bool TryUpgradeFromB10(CACulture source,
            out CACulture upgraded, out string failure)
        {
            upgraded = null;
            failure = LegacyB10ValidationFailure(source);
            if (!failure.NullOrEmpty()) return false;
            CACulture candidate = source.Copy();
            foreach (CACulturePractice practice in candidate
                         .inheritedPractices.Concat(candidate.practices))
            {
                if (!practice.practiceKey.NullOrEmpty()) continue;
                practice.practiceKey = CACulturalPracticeRegistry
                    .FromB10LongitudinalEvidence(
                        practice.LegacyB10SubjectKey,
                        practice.sourceSignature,
                        practice.firstRecordedTick);
                if (practice.practiceKey.NullOrEmpty())
                {
                    failure = "a B10 subject-shaped practice has no "
                        + "evidence-backed B11 practice equivalent";
                    return false;
                }
                if (practice.sourceOwner.NullOrEmpty())
                    practice.sourceOwner = "b10-longitudinal-evidence";
            }
            foreach (CACultureObservation observation in candidate.observations)
            {
                if (CACulturalPracticeRegistry.Find(observation.key) != null)
                    continue;
                observation.key = CACulturalPracticeRegistry
                    .FromB10LongitudinalEvidence(observation.key,
                        observation.sourceSignature,
                        observation.evidenceStartTick >= 0
                            ? observation.evidenceStartTick
                            : observation.firstObservedTick);
                if (observation.key.NullOrEmpty())
                {
                    failure = "a B10 subject-shaped observation has no "
                        + "evidence-backed B11 practice equivalent";
                    return false;
                }
            }
            candidate.schemaVersion = CACulture.CurrentSchemaVersion;
            failure = ValidationFailure(candidate, requireSubstantive: true);
            if (!failure.NullOrEmpty()) return false;
            upgraded = candidate;
            return true;
        }

        private static string LegacyB10ValidationFailure(CACulture culture)
        {
            if (culture == null) return "Culture is missing";
            if (culture.schemaVersion != 8)
                return "B10 Culture schema is " + culture.schemaVersion
                    + ", expected 8";
            // Validate the unchanged structural families under the current
            // rules, then separately admit only evidence-gated legacy practice
            // identities. No collection is pruned to make the record pass.
            List<CACulturePractice> inherited = culture.inheritedPractices;
            List<CACulturePractice> lived = culture.practices;
            string failure = ValidationFailure(culture,
                requireSubstantive: false, expectedSchema: 8,
                validatePractices: false);
            if (!failure.NullOrEmpty()) return failure;
            if (inherited == null || lived == null)
                return "B10 practice collections are incomplete";
            foreach (CACulturePractice practice in inherited.Concat(lived))
            {
                if (practice == null) return "B10 practice entry is null";
                bool current = CACulturalPracticeRegistry.Find(
                    practice.practiceKey) != null;
                bool convertible = practice.practiceKey.NullOrEmpty()
                    && !CACulturalPracticeRegistry
                        .FromB10LongitudinalEvidence(
                            practice.LegacyB10SubjectKey,
                            practice.sourceSignature,
                            practice.firstRecordedTick).NullOrEmpty();
                if (!current && !convertible)
                    return "B10 practice identity is unsupported";
                if (practice.summary.NullOrEmpty()
                    || practice.sourceSignature.NullOrEmpty()
                    || practice.strength < 0 || practice.strength > 100
                    || practice.firstRecordedTick < -1
                    || practice.lastObservedTick < -1)
                    return "B10 practice evidence is incomplete";
                if (current && practice.sourceOwner.NullOrEmpty())
                    return "B10 current practice has no owner";
            }
            if (inherited.GroupBy(PracticeIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1)
                || lived.GroupBy(PracticeIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "B10 practice identity is duplicated";
            if (culture.observations == null)
                return "B10 observation collection is incomplete";
            foreach (CACultureObservation observation in culture.observations)
            {
                if (observation == null)
                    return "B10 observation entry is null";
                bool current = CACulturalPracticeRegistry.Find(
                    observation.key) != null;
                int start = observation.evidenceStartTick >= 0
                    ? observation.evidenceStartTick
                    : observation.firstObservedTick;
                bool convertible = !CACulturalPracticeRegistry
                    .FromB10LongitudinalEvidence(observation.key,
                        observation.sourceSignature, start).NullOrEmpty();
                if (!current && !convertible)
                    return "B10 observation identity is unsupported";
                if (observation.sourceOwner.NullOrEmpty()
                    || observation.sourceDomain.NullOrEmpty()
                    || observation.sourceSignature.NullOrEmpty()
                    || observation.strength < 0 || observation.strength > 100
                    || observation.evidenceStartTick < -1
                    || observation.firstObservedTick < -1
                    || observation.lastObservedTick < -1
                    || observation.observationCount < 0)
                    return "B10 observation evidence is incomplete";
            }
            if (culture.observations.GroupBy(ObservationIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "B10 observation identity is duplicated";
            return null;
        }

        private static string PracticeIdentity(CACulturePractice practice)
        {
            return (practice?.practiceKey
                    ?? "legacy:" + practice?.LegacyB10SubjectKey) + "\0"
                + (practice?.sourceOwner ?? "b10-longitudinal-evidence");
        }

        private static string ObservationIdentity(
            CACultureObservation observation)
        {
            return (observation?.key ?? "") + "\0"
                + (observation?.sourceOwner ?? "") + "\0"
                + (observation?.sourceDomain ?? "");
        }

        internal static void Normalize(CACulture culture)
        {
            if (culture == null) return;
            if (culture.constituents == null)
                culture.constituents = new List<CACultureConstituent>();
            if (culture.inheritedMeanings == null)
                culture.inheritedMeanings = new List<CACulturalMeaning>();
            if (culture.localMeanings == null)
                culture.localMeanings = new List<CACulturalMeaning>();
            if (culture.transitions == null)
                culture.transitions = new List<CACultureTransition>();
            if (culture.inheritedPractices == null)
                culture.inheritedPractices = new List<CACulturePractice>();
            if (culture.practices == null)
                culture.practices = new List<CACulturePractice>();
            if (culture.observations == null)
                culture.observations = new List<CACultureObservation>();
            culture.constituents.RemoveAll(item => item == null);
            NormalizeMeanings(culture.inheritedMeanings);
            NormalizeMeanings(culture.localMeanings);
            // One current interpretation exists for each social subject and
            // population scope. A local interpretation supersedes the same
            // inherited identity; the transition ledger retains the earlier
            // historical state without double-weighting current resolution.
            var localMeaningKeys = new HashSet<string>(
                culture.localMeanings.Select(MeaningIdentity),
                StringComparer.Ordinal);
            culture.inheritedMeanings.RemoveAll(item =>
                localMeaningKeys.Contains(MeaningIdentity(item)));
            culture.transitions.RemoveAll(item => item == null);
            culture.inheritedPractices.RemoveAll(item => item == null
                || CACulturalPracticeRegistry.Find(item.practiceKey) == null);
            culture.practices.RemoveAll(item => item == null
                || CACulturalPracticeRegistry.Find(item.practiceKey) == null);
            culture.observations.RemoveAll(item => item == null
                || item.key.NullOrEmpty() || item.sourceOwner.NullOrEmpty()
                || item.sourceDomain.NullOrEmpty());
            culture.revision = Math.Max(culture.revision,
                culture.transitions.Count == 0 ? 0
                    : culture.transitions.Max(item => item.sequence));
            culture.schemaVersion = CACulture.CurrentSchemaVersion;
        }

        private static void NormalizeMeanings(List<CACulturalMeaning> values)
        {
            values.RemoveAll(item => item == null
                || !CASocialSubjectRegistry.ValidKey(item.subjectKey));
            foreach (CACulturalMeaning item in values)
            {
                item.approval = Mathf.Clamp(item.approval, -100, 100);
                item.normality = Mathf.Clamp(item.normality, 0, 100);
                item.prestige = Mathf.Clamp(item.prestige, -100, 100);
                item.salience = Mathf.Clamp(item.salience, 0, 100);
                item.weight = Mathf.Clamp(item.weight, 1, 100);
            }
            List<CACulturalMeaning> unique = values
                .GroupBy(MeaningIdentity, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(item =>
                        item.lastChangedTick)
                    .ThenByDescending(item => item.firstRecordedTick)
                    .First())
                .ToList();
            values.Clear();
            values.AddRange(unique);
        }

        internal static string MeaningIdentity(CACulturalMeaning meaning)
        {
            return (meaning?.subjectKey ?? "") + "\0"
                + (meaning?.populationScope.NullOrEmpty() == false
                    ? meaning.populationScope : "*");
        }

        internal static void EnsureGenerated(CACulture culture,
            string seed, CultureDef fallback)
        {
            if (culture == null) return;
            Normalize(culture);
            if (culture.id.NullOrEmpty())
                culture.id = "culture:" + (seed ?? "unowned");
            if (culture.constituents.Count == 0)
                culture.constituents.Add(new CACultureConstituent
                {
                    cultureId = culture.id,
                    label = culture.name ?? "Unnamed culture",
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
            int meaningCount = culture.inheritedMeanings.Count
                + culture.localMeanings.Count;
            int practiceCount = culture.inheritedPractices.Count
                + culture.practices.Count;
            string practice = practiceCount == 0 ? ""
                : " · " + practiceCount + " practice"
                    + (practiceCount == 1 ? "" : "s");
            string salient = string.Join(", ", culture.inheritedMeanings
                .Concat(culture.localMeanings)
                .Where(item => item != null)
                .OrderByDescending(item => item.salience)
                .ThenBy(item => item.subjectKey)
                .Take(3)
                .Select(item => CASocialSubjectRegistry.Find(item.subjectKey)
                    ?.Label ?? "recorded social meaning").ToArray());
            string top = salient.NullOrEmpty() ? ""
                : " · most salient: " + salient;
            return identity + " · " + continuity + plurality + change
                + " · " + meaningCount + " social meaning"
                    + (meaningCount == 1 ? "" : "s") + practice
                + top + " · visual tradition: " + visual;
        }

        internal static CACulturalMeaningResolution Resolve(CACulture culture,
            string subjectKey, string populationScope = null)
        {
            if (culture == null)
                return CACulturalMeaningResolver.Resolve(null, subjectKey,
                    populationScope);
            Normalize(culture);
            IEnumerable<CACulturalMeaningState> states = culture.inheritedMeanings
                .Concat(culture.localMeanings).Select(item => item.ToState());
            // A named constituent's own interpretation applies directly to
            // that constituent. An unscoped settlement summary instead
            // weights constituent records by their realized population share.
            if (string.IsNullOrWhiteSpace(populationScope))
            {
                Dictionary<string, int> shares = culture.constituents
                    .Where(item => item != null
                        && !item.cultureId.NullOrEmpty())
                    .GroupBy(item => item.cultureId, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key,
                        group => Mathf.Clamp(group.Sum(item => item.share),
                            0, 100), StringComparer.Ordinal);
                states = CACulturalMeaningResolver.ApplyConstituentShares(
                    states, shares);
            }
            return CACulturalMeaningResolver.Resolve(states, subjectKey,
                populationScope);
        }

        // A sociological signature deliberately excludes the optional visual
        // tradition. Visual selection has its own signature so changing art
        // inheritance cannot masquerade as social change.
        internal static string SociologicalSignature(CACulture culture)
        {
            if (culture == null) return "unrecorded";
            Normalize(culture);
            string meanings = CACulturalMeaningResolver.Fingerprint(
                culture.inheritedMeanings.Concat(culture.localMeanings)
                    .Select(item => item.ToState()));
            string practices = string.Join("|", culture.inheritedPractices
                .Concat(culture.practices).Where(item => item != null)
                .OrderBy(item => item.practiceKey)
                .Select(item => item.practiceKey + ":" + item.strength + ":"
                    + (item.sourceSignature ?? "authored")));
            return CASocialPatternKernel.StableHash((culture.id ?? "") + "|"
                + (culture.parentId ?? "") + "|" + (culture.localityKey ?? "")
                + "|" + meanings + "|" + practices);
        }

        internal static string VisualSignature(CACulture culture)
        {
            return CASocialPatternKernel.StableHash(
                culture?.sourceCultureDefName ?? "none");
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
                + CAPlayerFoundingSession.WorldIdentity()
                + ":map:" + map.uniqueID;
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
                    + (plan.regionalId ?? plan.candidateId ?? "region")
                    + ":" + settlement.slot;
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

            CACultureModel.Normalize(settlement.localCulture);
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
            CACultureModel.Normalize(culture);
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
            return culture.practices.Concat(culture.inheritedPractices
                    ?? new List<CACulturePractice>())
                .Where(item => item != null
                    && item.practiceKey == key)
                .Select(item => Mathf.Clamp(item.strength, 0, 100))
                .DefaultIfEmpty(0).Max();
        }

        internal static int PracticeStrengthPrefix(CACulture culture,
            string prefix)
        {
            if (culture?.practices == null || prefix.NullOrEmpty()) return 0;
            return culture.practices.Concat(culture.inheritedPractices
                    ?? new List<CACulturePractice>())
                .Where(item => item != null
                    && item.practiceKey?.StartsWith(prefix,
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
            CACultureModel.Normalize(culture);
            CACulturalHistoryEvidence previous = Evidence(
                culture.lastEvidence, null);
            List<CACulturalPracticeEvidence> raw =
                (observedPractices
                    ?? Enumerable.Empty<CACulturalPracticeEvidence>())
                .Where(item => item != null && !item.Key.NullOrEmpty()
                    && CACulturalPracticeRegistry.Find(item.Key) != null
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
                        Key = item.practiceKey,
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
                    && qualifiedKeys.Contains(item.practiceKey)))
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
                    .ToDictionary(item => item.practiceKey, item => item,
                        StringComparer.Ordinal);
                culture.practices = evaluated.Practices.Select(item =>
                {
                    CACulturePractice old;
                    priorByKey.TryGetValue(item.Key, out old);
                    return new CACulturePractice
                    {
                        practiceKey = item.Key,
                        summary = item.Summary,
                        strength = item.Strength,
                        firstRecordedTick = old?.firstRecordedTick ?? tick,
                        lastObservedTick = qualified.Any(value =>
                            value.Key == item.Key) ? tick
                                : old?.lastObservedTick ?? -1,
                        sourceOwner = qualified.FirstOrDefault(value =>
                            value.Key == item.Key)?.SourceOwner
                                ?? old?.sourceOwner,
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

        internal static bool EvaluateMeaningTransition(CACulture culture,
            IEnumerable<CASocialGroupPattern> patterns, string scope, int tick)
        {
            if (culture == null) return false;
            CACultureModel.Normalize(culture);
            List<CACulturalMeaningState> prior = (culture.localMeanings
                    ?? new List<CACulturalMeaning>())
                .Where(item => item != null).Select(item => item.ToState())
                .ToList();
            string predecessor = CACultureModel.SociologicalSignature(culture);
            CACulturalMeaningTransitionResult evaluated =
                CACulturalMeaningTransitionKernel.Evaluate(prior, patterns,
                    tick);
            if (!evaluated.Changed) return false;
            culture.localMeanings = evaluated.Meanings
                .Where(item => item != null)
                .Select(CACulturalMeaning.FromState).ToList();
            string successor = CACultureModel.SociologicalSignature(culture);
            string subjects = string.Join(", ", evaluated.ChangedSubjectKeys);
            string dimensions = string.Join(", ",
                evaluated.ChangedDimensions);
            string populationScopes = string.Join(", ", evaluated.Meanings
                .Where(item => item != null
                    && evaluated.ChangedSubjectKeys.Contains(item.SubjectKey))
                .Select(item => item.PopulationScope ?? "all")
                .Distinct().OrderBy(value => value));
            Record(culture, evaluated.Cause
                    ?? "sustained social interpretation",
                "Recorded change in " + subjects + ": " + dimensions + ".",
                successor, tick, predecessor, evaluated.EvidenceSignature,
                dimensions, subjects, populationScopes);
            return true;
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
                .Select(item => item.practiceKey), StringComparer.Ordinal);
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
            return CASocialPatternKernel.StableHash(
                CACultureModel.SociologicalSignature(culture)
                + "|visual:" + CACultureModel.VisualSignature(culture)
                + "|maturity:" + culture.maturity
                + "|revision:" + culture.revision);
        }

        internal static string PracticeSummary(CACulture culture)
        {
            CACulturePractice[] practices = (culture?.practices
                    ?? new List<CACulturePractice>())
                .Where(item => item != null && item.strength > 0)
                .OrderByDescending(item => item.strength)
                .ThenBy(item => item.practiceKey).ToArray();
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
            string changedDimensions = null,
            string changedSubjectKeys = null,
            string populationScope = null)
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
                changedDimensions = changedDimensions,
                changedSubjectKeys = changedSubjectKeys,
                populationScope = populationScope,
                successorCultureSignature = signature
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
                entry.ideoligionProtected |=
                    population.ideoligionProtected;
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
                    + item.share + ":" + (item.ideoligionProtected
                        ? "Ideoligion-protected" : "unprotected")));
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
        internal static void Normalize(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return;
            if (beliefs.positions == null)
                beliefs.positions = new List<CAAxisEntry>();
            if (beliefs.derivationReceipts == null)
                beliefs.derivationReceipts =
                    new List<CAPoliticalDerivationReceipt>();
            NormalizeMechanisms(beliefs.positions,
                beliefs.derivationReceipts);
            ReconcileReceipts(beliefs);
            beliefs.schemaVersion = CAPoliticalBeliefs.CurrentSchemaVersion;
        }

        internal static void ReconcileReceipts(CAPoliticalBeliefs beliefs)
        {
            if (beliefs?.derivationReceipts == null) return;
            beliefs.derivationReceipts.RemoveAll(receipt => receipt == null
                || receipt.axisKey.NullOrEmpty()
                || receipt.selectedOptionKey.NullOrEmpty()
                || !beliefs.positions.Any(entry => entry != null
                    && entry.axisKey == receipt.axisKey
                    && entry.optionKey == receipt.selectedOptionKey
                    && entry.source == (byte)CAAxisSource.Generated));
            beliefs.derivationReceipts = beliefs.derivationReceipts
                .GroupBy(receipt => receipt.axisKey + "\0"
                    + receipt.selectedOptionKey, StringComparer.Ordinal)
                .Select(group => group.First()).ToList();
        }

        // B10 encoded some coexistence as one synthetic "mixed" option. B11
        // expands only values whose old description named the exact mechanisms.
        // Ambiguous evidence remains present and invalid so validation can stop
        // visibly; it is never guessed or silently erased.
        internal static void NormalizeMechanisms(List<CAAxisEntry> positions)
        {
            NormalizeMechanisms(positions, null);
        }

        internal static void NormalizeMechanisms(List<CAAxisEntry> positions,
            List<CAPoliticalDerivationReceipt> receipts)
        {
            if (positions == null) return;
            // Preserve the whole record when any entry cannot be understood.
            // Validation must see unsupported state exactly as loaded; a
            // successful exact expansion may not partially rewrite a list that
            // will subsequently fail.
            if (!ValidationFailure(positions, allowExactLegacy: true)
                    .NullOrEmpty())
                return;
            foreach (CAAxisEntry mixed in positions.Where(entry =>
                    entry != null && entry.optionKey == "mixed").ToList())
            {
                IReadOnlyList<string> replacements =
                    CAPoliticalLegacyMechanisms.Expand(mixed.axisKey,
                        mixed.optionKey);
                if (replacements.Count == 0) continue;
                CAPoliticalDerivationReceipt prior = receipts?.FirstOrDefault(
                    receipt => receipt != null
                        && receipt.axisKey == mixed.axisKey
                        && receipt.selectedOptionKey == mixed.optionKey);
                IReadOnlyList<CAPoliticalLegacyReceiptExpansion>
                    receiptExpansions = prior == null
                        ? Array.Empty<CAPoliticalLegacyReceiptExpansion>()
                        : CAPoliticalLegacyMechanisms.ExpandReceipt(
                            mixed.axisKey, mixed.optionKey, prior.scores,
                            prior.evidence, prior.tieBroken);
                foreach (string replacement in replacements)
                {
                    CAFactionAxes.Add(positions, mixed.axisKey, replacement,
                        (CAAxisSource)mixed.source);
                    if (prior != null && mixed.source
                            == (byte)CAAxisSource.Generated
                        && !receipts.Any(receipt => receipt != null
                            && receipt.axisKey == mixed.axisKey
                            && receipt.selectedOptionKey == replacement))
                    {
                        CAPoliticalDerivationReceipt migrated = prior.Copy();
                        migrated.selectedOptionKey = replacement;
                        CAPoliticalLegacyReceiptExpansion expansion =
                            receiptExpansions.First(item => item.OptionKey
                                == replacement);
                        migrated.scores = expansion.Scores;
                        migrated.evidence = expansion.Evidence;
                        migrated.tieBroken = expansion.TieBroken;
                        receipts.Add(migrated);
                    }
                }
                positions.Remove(mixed);
                receipts?.RemoveAll(receipt => receipt != null
                    && receipt.axisKey == mixed.axisKey
                    && receipt.selectedOptionKey == mixed.optionKey);
            }
        }

        internal static string ValidationFailure(
            IEnumerable<CAAxisEntry> positions)
        {
            return ValidationFailure(positions, allowExactLegacy: false);
        }

        internal static string ValidationFailure(
            IEnumerable<CAAxisEntry> positions, bool allowExactLegacy)
        {
            if (positions == null)
                return "political mechanism collection is missing";
            List<CAAxisEntry> entries = positions.ToList();
            foreach (CAAxisEntry entry in entries)
            {
                if (entry == null) return "political mechanism entry is null";
                CAAxisDef axis = CAFactionAxes.AxisDef(entry.axisKey);
                if (axis == null)
                    return "unknown political subject '"
                        + (entry.axisKey ?? "unrecorded") + "'";
                if (entry.source != (byte)CAAxisSource.Generated
                    && entry.source != (byte)CAAxisSource.Authored)
                    return "political mechanism '" + entry.axisKey + ":"
                        + (entry.optionKey ?? "unrecorded")
                        + "' has invalid source " + entry.source;
                if (axis.Options.Any(option => option.Key == entry.optionKey))
                    continue;
                if (allowExactLegacy && CAPoliticalLegacyMechanisms.Expand(
                        entry.axisKey, entry.optionKey).Count > 0)
                    continue;
                string legacy = CAPoliticalLegacyMechanisms.UnsupportedReason(
                    entry.axisKey, entry.optionKey);
                if (!legacy.NullOrEmpty())
                    return legacy + ". Choose the actual mechanisms before "
                        + "continuing.";
                return "unknown " + axis.Label.ToLowerInvariant()
                    + " mechanism '" + (entry.optionKey ?? "unrecorded") + "'";
            }
            if (entries.GroupBy(entry => entry.axisKey + "\0"
                        + entry.optionKey, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
                return "political mechanism collection contains duplicate facts";
            foreach (IGrouping<string, CAAxisEntry> axis in entries.GroupBy(
                         entry => entry.axisKey, StringComparer.Ordinal))
                if (axis.Any(entry => entry.optionKey == "none")
                    && axis.Count() > 1)
                    return axis.Key + " absence cannot coexist with a standing "
                        + "mechanism";
            return null;
        }

        internal static string ValidationFailure(CAPoliticalBeliefs beliefs,
            bool allowExactLegacy)
        {
            if (beliefs == null) return "political-belief record is missing";
            int expected = allowExactLegacy ? 8
                : CAPoliticalBeliefs.CurrentSchemaVersion;
            if (beliefs.schemaVersion != expected)
                return "political-belief schema is " + beliefs.schemaVersion
                    + ", expected " + expected;
            string mechanismFailure = ValidationFailure(beliefs.positions,
                allowExactLegacy);
            if (!mechanismFailure.NullOrEmpty()) return mechanismFailure;
            if (beliefs.derivationReceipts == null)
                return "political derivation receipts are missing";
            if (beliefs.derivationReceipts.Any(receipt => receipt == null
                    || receipt.axisKey.NullOrEmpty()
                    || receipt.selectedOptionKey.NullOrEmpty()
                    || receipt.scores.NullOrEmpty()
                    || receipt.evidence.NullOrEmpty()))
                return "political derivation receipt is incomplete";
            if (beliefs.derivationReceipts.GroupBy(receipt => receipt.axisKey
                        + "\0" + receipt.selectedOptionKey,
                        StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "political derivation receipt is duplicated";
            if (beliefs.derivationReceipts.Any(receipt =>
                    !beliefs.positions.Any(entry => entry != null
                        && entry.axisKey == receipt.axisKey
                        && entry.optionKey == receipt.selectedOptionKey
                        && entry.source == (byte)CAAxisSource.Generated)))
                return "political derivation receipt has no generated fact";
            return null;
        }

        internal static bool TryUpgradeFromB10(
            CAPoliticalBeliefs source, out CAPoliticalBeliefs upgraded,
            out string failure)
        {
            upgraded = null;
            failure = ValidationFailure(source, allowExactLegacy: true);
            if (!failure.NullOrEmpty()) return false;
            CAPoliticalBeliefs candidate = source.Copy();
            NormalizeMechanisms(candidate.positions,
                candidate.derivationReceipts);
            ReconcileReceipts(candidate);
            candidate.schemaVersion = CAPoliticalBeliefs.CurrentSchemaVersion;
            failure = ValidationFailure(candidate, allowExactLegacy: false);
            if (!failure.NullOrEmpty()) return false;
            upgraded = candidate;
            return true;
        }

        internal static bool TryUpgradeMechanismsFromB10(
            IEnumerable<CAAxisEntry> source, out List<CAAxisEntry> upgraded,
            out string failure)
        {
            upgraded = null;
            failure = ValidationFailure(source, allowExactLegacy: true);
            if (!failure.NullOrEmpty()) return false;
            List<CAAxisEntry> candidate = CAFactionStartingState.CopyAxes(
                source.ToList());
            NormalizeMechanisms(candidate);
            failure = ValidationFailure(candidate, allowExactLegacy: false);
            if (!failure.NullOrEmpty()) return false;
            upgraded = candidate;
            return true;
        }

        internal static void Ensure(CAPoliticalBeliefs beliefs, string seed)
        {
            if (beliefs == null) return;
            Normalize(beliefs);
            if (beliefs.id.NullOrEmpty())
                beliefs.id = "politics:" + (seed ?? "unowned");
        }

        internal static int DeriveUnset(CAPoliticalBeliefs beliefs,
            string seed, CAPoliticalDerivationContext context = null)
        {
            if (beliefs == null) return 0;
            CAPoliticalDerivationResult result =
                CAPoliticalDerivationKernel.Derive(
                    seed ?? beliefs.id ?? "ca-politics", context,
                    CAFactionAxes.Axes.Select(def =>
                        new CAPoliticalAxisContract
                        {
                            AxisKey = def.Key,
                            OptionKeys = def.Options.Select(option =>
                                option.Key).ToList()
                        }));
            int filled = 0;
            foreach (CAPoliticalAxisDerivation axis in result.Axes)
            {
                if (CAFactionAxes.StateOf(beliefs.positions, axis.AxisKey)
                    != CAAxisSource.Unset) continue;
                if (axis.SelectedOptionKey.NullOrEmpty()
                    || axis.Evidence.Count == 0) continue;
                CAFactionAxes.Set(beliefs.positions, axis.AxisKey,
                    axis.SelectedOptionKey, CAAxisSource.Generated);
                beliefs.derivationReceipts.RemoveAll(item => item != null
                    && item.axisKey == axis.AxisKey);
                beliefs.derivationReceipts.Add(
                    new CAPoliticalDerivationReceipt
                    {
                        axisKey = axis.AxisKey,
                        selectedOptionKey = axis.SelectedOptionKey,
                        scores = string.Join(",", axis.Scores.OrderBy(pair =>
                            pair.Key).Select(pair => pair.Key + "="
                                + pair.Value)),
                        evidence = string.Join(" | ", axis.Evidence),
                        tieBroken = false
                    });
                filled++;
            }
            return filled;
        }

        internal static void ApplyTemplate(CAPoliticalBeliefs beliefs,
            CAPoliticalPatchTemplate template)
        {
            if (beliefs == null || template == null
                || template.Target != CAPoliticalPatchTarget.NormativeBeliefs)
                return;
            foreach (KeyValuePair<string, List<string>> patch in
                template.Mechanisms)
            {
                foreach (string mechanism in patch.Value)
                {
                    CAFactionAxes.Add(beliefs.positions, patch.Key,
                        mechanism, CAAxisSource.Authored);
                    RemoveReceipt(beliefs, patch.Key, mechanism);
                }
                ReconcileReceipts(beliefs);
            }
        }

        internal static bool UsesTemplate(CAPoliticalBeliefs beliefs,
            CAPoliticalPatchTemplate template)
        {
            return beliefs != null && template != null
                && template.Mechanisms.All(pair => pair.Value.All(value =>
                    CAFactionAxes.HasOption(beliefs.positions, pair.Key,
                        value)));
        }

        internal static void Author(CAPoliticalBeliefs beliefs,
            string axisKey, string optionKey)
        {
            if (beliefs == null) return;
            CAFactionAxes.Add(beliefs.positions, axisKey, optionKey,
                CAAxisSource.Authored);
            RemoveReceipt(beliefs, axisKey, optionKey);
            ReconcileReceipts(beliefs);
        }

        internal static void Remove(CAPoliticalBeliefs beliefs,
            string axisKey, string optionKey)
        {
            if (beliefs == null) return;
            CAFactionAxes.Remove(beliefs.positions, axisKey, optionKey);
            RemoveReceipt(beliefs, axisKey, optionKey);
            ReconcileReceipts(beliefs);
        }

        internal static void Release(CAPoliticalBeliefs beliefs,
            string axisKey)
        {
            if (beliefs == null) return;
            CAFactionAxes.Release(beliefs.positions, axisKey);
            beliefs.derivationReceipts.RemoveAll(item => item != null
                && item.axisKey == axisKey);
        }

        internal static void RemoveReceipt(CAPoliticalBeliefs beliefs,
            string axisKey, string optionKey)
        {
            beliefs?.derivationReceipts?.RemoveAll(item => item != null
                && item.axisKey == axisKey
                && item.selectedOptionKey == optionKey);
        }

        internal static string Summary(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "Political beliefs not set";
            return string.Join(" · ", CAAuthoringChoices.PoliticalGroups
                .Select(group => group.Label + ": " + string.Join(", ",
                    group.Axes.Select(axis =>
                    {
                        string values = string.Join(" + ",
                            CAFactionAxes.OptionsOf(beliefs.positions, axis)
                                .Select(option => option.Label));
                        return values.NullOrEmpty() ? "unset" : values;
                    })))
                .ToArray());
        }

    }

    internal static class CAFactionStructureModel
    {
        // Current order is an established fact, not a randomized expression
        // of what the population believes ought to be true. Without authored
        // or observed institutional evidence, unset axes remain unset.
        internal static int PreserveEstablishedUnset(
            List<CAAxisEntry> structure)
        {
            return 0;
        }

        internal static List<string> Tensions(
            CAPoliticalBeliefs beliefs, List<CAAxisEntry> structure)
        {
            var result = new List<string>();
            if (beliefs == null || structure == null) return result;
            foreach (CAAxisDef def in CAFactionAxes.Axes)
            {
                IReadOnlyList<string> ideal = CAFactionAxes.KeysOf(
                    beliefs.positions, def.Key);
                IReadOnlyList<string> actual = CAFactionAxes.KeysOf(
                    structure, def.Key);
                if (ideal.Count == 0 || actual.Count == 0
                    || ideal.SequenceEqual(actual)) continue;
                result.Add(def.Label + ": preferred "
                    + AxisLabels(def, ideal) + "; current "
                    + AxisLabels(def, actual) + ".");
            }
            return result;
        }

        private static string AxisLabels(CAAxisDef def,
            IEnumerable<string> keys)
        {
            var selected = new HashSet<string>(keys,
                StringComparer.Ordinal);
            return string.Join(" + ", def.Options.Where(option =>
                selected.Contains(option.Key)).Select(option => option.Label));
        }

        internal static string Summary(List<CAAxisEntry> structure)
        {
            if (structure == null) return "Current order not set";
            string leadership = OptionLabels(structure,
                CAFactionAxes.Leadership);
            string decisions = OptionLabels(structure,
                CAFactionAxes.Decisions);
            string ownership = OptionLabels(structure,
                CAFactionAxes.Ownership);
            return (leadership ?? "leadership not set") + " · "
                + (decisions ?? "decisions not set") + " · "
                + (ownership ?? "ownership not set");
        }

        private static string OptionLabels(List<CAAxisEntry> values,
            string axisKey)
        {
            string result = string.Join(" + ", CAFactionAxes.OptionsOf(
                values, axisKey).Select(option => option.Label));
            return result.NullOrEmpty() ? null : result;
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
            CACultureInitialState.EnsureForRegional(group.culture, plan,
                group);
            CAPoliticalBeliefsModel.Ensure(group.politicalBeliefs, seed);
            CAPoliticalDerivationContext context =
                CAPoliticalContext.ForFaction(plan, group);
            CAPoliticalBeliefsModel.DeriveUnset(group.politicalBeliefs,
                seed + ":beliefs", context);
        }

        internal static void ApplyPlan(Faction faction,
            CACulture culture,
            CAPoliticalBeliefs beliefs, List<CAAxisEntry> structure,
            bool institutionalStateIncomplete = false)
        {
            CAFactionState record = CAFactionStateWorldComponent.Current
                ?.EnsureFor(faction);
            if (record == null) return;
            record.institutionalStateIncomplete =
                institutionalStateIncomplete;
            if (culture != null) record.culture = culture.Copy();
            if (beliefs != null)
                record.politicalBeliefs = beliefs.Copy();
            if (structure != null)
            {
                foreach (IGrouping<string, CAAxisEntry> subject in structure
                    .Where(entry => entry != null && entry.source
                        != (byte)CAAxisSource.Unset)
                    .GroupBy(entry => entry.axisKey, StringComparer.Ordinal))
                {
                    if (CAFactionAxes.StateOf(record.factionStructure,
                            subject.Key)
                        == CAAxisSource.Authored) continue;
                    CAFactionAxes.Release(record.factionStructure,
                        subject.Key);
                    foreach (CAAxisEntry entry in subject)
                        CAFactionAxes.Add(record.factionStructure,
                            subject.Key, entry.optionKey,
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
        private readonly CAFoundingArrangement foundingArrangement;
        private readonly string seed;
        private readonly Action changed;
        private Vector2 scroll;
        private float viewHeight;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1080f, UI.screenWidth - 48f),
            Mathf.Min(790f, UI.screenHeight - 48f));

        private Dialog_CAAxisEditor(CAPoliticalBeliefs beliefs,
            List<CAAxisEntry> structure, string seed,
            CAFoundingArrangement foundingArrangement, Action changed)
        {
            this.beliefs = beliefs;
            this.structure = structure;
            this.seed = seed;
            this.foundingArrangement = foundingArrangement;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        internal static Dialog_CAAxisEditor ForBeliefs(
            CAPoliticalBeliefs beliefs, string seed,
            CAFoundingArrangement foundingArrangement, Action changed)
        {
            return new Dialog_CAAxisEditor(beliefs, null, seed,
                foundingArrangement, changed);
        }

        internal static Dialog_CAAxisEditor ForStructure(
            List<CAAxisEntry> structure, CAPoliticalBeliefs beliefs,
            string seed, Action changed)
        {
            return new Dialog_CAAxisEditor(beliefs, structure, seed,
                null, changed);
        }

        public override void DoWindowContents(Rect inRect)
        {
            bool editingBeliefs = structure == null;
            GameFont old = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                editingBeliefs ? "Political beliefs"
                    : "Political beliefs and current order");
            Text.Font = old;
            string description = editingBeliefs
                ? "Set what this population considers proper: government, "
                    + "participation, property, membership, support, and "
                    + "conflict. Existing institutions may agree or differ."
                : "Political beliefs state what should be proper. Current "
                    + "order records what this faction actually does; "
                    + "each remains independent and disagreement is preserved.";
            float descriptionHeight = Text.CalcHeight(description,
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width,
                descriptionHeight), description);
            float y = 38f + descriptionHeight + 12f;
            Rect outRect = new Rect(0f, y, inRect.width,
                inRect.height - y - 48f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Math.Max(viewHeight, outRect.height));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float rowY = 0f;
            DrawPoliticalOverview(ref rowY, view.width, editingBeliefs);
            foreach (CAAxisGroupDef group in
                CAAuthoringChoices.PoliticalGroups)
            {
                rowY += 10f;
                Widgets.DrawLineHorizontal(0f, rowY, view.width);
                rowY += 12f;
                DrawText(ref rowY, view.width, group.Label,
                    GameFont.Medium, Color.white);
                DrawPoliticalGroup(ref rowY, view.width,
                    group, editingBeliefs);
            }
            viewHeight = rowY + 8f;
            Widgets.EndScrollView();
        }

        private float DrawPoliticalActions(Rect inRect, float y,
            bool editingBeliefs)
        {
            var labels = new List<string> { "Belief sets...",
                "Save belief set..." };
            var actions = new List<Action>
            {
                OpenPoliticalBeliefSets, SavePoliticalBeliefSet
            };
            if (CAAuthoringProfileLibrary.PoliticalBeliefSets.Count > 0)
            {
                labels.Add("Manage saved...");
                actions.Add(() => Find.WindowStack.Add(
                    new Dialog_CAProfileManager(null, beliefs, changed)));
            }
            if (!editingBeliefs)
            {
                labels.Add("Current-order sets...");
                actions.Add(OpenCurrentOrderSets);
            }
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
            string profile = CAAuthoringChoices.PoliticalIdentity(beliefs);
            DrawText(ref y, width, profile,
                GameFont.Medium, Color.white);
            y = DrawPoliticalActions(new Rect(0f, 0f, width, 1f), y,
                beliefsOnly);
            if (beliefsOnly)
            {
                int mechanismCount = (beliefs.positions
                        ?? new List<CAAxisEntry>()).Count(item => item != null
                            && item.source != (byte)CAAxisSource.Unset);
                string set = mechanismCount == 0
                    ? "No political commitments are set. Unset subjects remain open."
                    : mechanismCount + " political mechanism"
                        + (mechanismCount == 1 ? " is" : "s are")
                        + " set; unmentioned subjects remain open.";
                DrawText(ref y, width, set, GameFont.Small,
                    new Color(0.72f, 0.76f, 0.81f));
                List<CAPoliticalBeliefPractice.CAFoundingBeliefReading> readings =
                    CAPoliticalBeliefPractice.ReadAgainstPoliticalBeliefs(
                        beliefs, foundingArrangement);
                if (readings.Count > 0)
                {
                    int tensions = readings.Count(item => !item.Silent
                        && !item.conforms);
                    DrawText(ref y, width, tensions == 0
                            ? "Political beliefs and current rules at landing are aligned where they overlap."
                            : tensions + " belief-rule tension"
                                + (tensions == 1 ? "" : "s")
                                + " at landing.",
                        GameFont.Small, tensions == 0
                            ? CACreationUI.Authored : ColorLibrary.Yellow);
                    foreach (CAPoliticalBeliefPractice.CAFoundingBeliefReading
                        reading in readings.Where(item => !item.Silent
                            && !item.conforms))
                        DrawText(ref y, width, "In tension on "
                                + reading.title.ToLowerInvariant() + ": "
                                + reading.belief + "; current rule: "
                                + reading.adopted + ".", GameFont.Tiny,
                            new Color(0.86f, 0.78f, 0.52f));
                }
            }
            else
            {
                List<string> tensions = CAFactionStructureModel.Tensions(
                    beliefs, structure);
                DrawText(ref y, width, tensions.Count == 0
                    ? "Belief and current order are aligned on every set subject."
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
                {
                    string mechanisms = string.Join(" + ",
                        CAFactionAxes.OptionsOf(beliefs.positions, axis)
                            .Select(option => option.Label));
                    return mechanisms.NullOrEmpty() ? "open" : mechanisms;
                }).ToArray());
                DrawText(ref y, width, group.Label + ": " + summary,
                    GameFont.Small, new Color(0.78f, 0.81f, 0.85f));
            }
        }

        private void DrawPoliticalGroup(ref float y, float width,
            CAAxisGroupDef group, bool beliefsOnly)
        {
            if (!beliefsOnly && width >= 720f)
            {
                float half = (width - 12f) / 2f;
                Text.Font = GameFont.Tiny;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(0f, y, half, 20f),
                    "Beliefs about what is proper");
                Widgets.Label(new Rect(half + 12f, y, half, 20f),
                    "Current order");
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
            else if (width < 720f)
            {
                DrawText(ref y, width, "Beliefs about what is proper",
                    GameFont.Tiny, ColoredText.SubtleGrayColor);
                DrawPositionButton(ref y, width, def, beliefs.positions,
                    true);
                DrawText(ref y, width, "Current order", GameFont.Tiny,
                    ColoredText.SubtleGrayColor);
                DrawPositionButton(ref y, width, def, structure, false);
                DrawPoliticalRelation(ref y, width, def);
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
                DrawPoliticalRelation(ref y, width, def);
            }
            y += 8f;
        }

        private void DrawPoliticalRelation(ref float y, float width,
            CAAxisDef def)
        {
            IReadOnlyList<string> preferred = CAFactionAxes.KeysOf(
                beliefs.positions, def.Key);
            IReadOnlyList<string> current = CAFactionAxes.KeysOf(
                structure, def.Key);
            Color relation = preferred.Count == 0 || current.Count == 0
                ? CACreationUI.Unset
                : preferred.SequenceEqual(current)
                    ? CACreationUI.Authored : ColorLibrary.Yellow;
            DrawText(ref y, width, preferred.Count == 0
                    || current.Count == 0
                        ? "No comparison on this subject"
                    : preferred.SequenceEqual(current) ? "Aligned"
                        : "In tension: beliefs include "
                            + OptionLabels(beliefs.positions, def.Key)
                            + "; current order includes "
                            + OptionLabels(structure, def.Key) + ".",
                GameFont.Tiny, relation);
        }

        private void DrawPositionButton(ref float y, float width,
            CAAxisDef def, List<CAAxisEntry> target, bool belief,
            float x = 0f)
        {
            IReadOnlyList<CAAxisOption> selected =
                CAFactionAxes.OptionsOf(target, def.Key);
            CAAxisSource state = CAFactionAxes.StateOf(target, def.Key);
            string selectedWords = string.Join(" + ", selected.Select(
                option => option.Label));
            string buttonLabel = selectedWords.NullOrEmpty()
                ? "Add a mechanism" : selectedWords.CapitalizeFirst();
            float textWidth = Mathf.Max(80f, width - 90f);
            float buttonHeight = Mathf.Max(30f,
                Text.CalcHeight(buttonLabel, textWidth - 14f) + 8f);
            Rect chip = new Rect(x, y + (buttonHeight - 20f) * 0.5f,
                84f, 20f);
            CACreationUI.DrawChip(chip, CACreationUI.SourceWords(state),
                CACreationUI.SourceColor(state));
            Rect value = new Rect(x + 90f, y, textWidth, buttonHeight);
            if (Widgets.ButtonText(value, buttonLabel))
                OpenAxis(def, target, belief);
            if (!selectedWords.NullOrEmpty())
                TooltipHandler.TipRegion(value, selectedWords);
            y += buttonHeight + 6f;
            if (selected.Count > 0)
            {
                string explanation = string.Join("; ", selected.Select(
                    option => option.Words));
                float words = Text.CalcHeight(explanation, width);
                GUI.color = new Color(0.72f, 0.76f, 0.81f);
                Widgets.Label(new Rect(x, y, width, words), explanation);
                GUI.color = Color.white;
                y += words + 4f;
            }
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            float consumerHeight = Text.CalcHeight("Used by: "
                + def.Consumers, width);
            Widgets.Label(new Rect(x, y, width, consumerHeight),
                "Used by: " + def.Consumers);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += consumerHeight + 4f;
        }

        private static string OptionLabels(List<CAAxisEntry> values,
            string axisKey)
        {
            string labels = string.Join(" + ", CAFactionAxes.OptionsOf(
                values, axisKey).Select(option => option.Label));
            return labels.NullOrEmpty() ? "none recorded" : labels;
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
            foreach (CAAxisOption option in def.Options)
            {
                CAAxisOption local = option;
                bool current = CAFactionAxes.HasOption(target, def.Key,
                    local.Key);
                options.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label.CapitalizeFirst(),
                    Summary = local.Words,
                    Details = def.Question + "\n\nMechanisms on the same "
                        + "subject may coexist unless one explicitly records "
                        + "the absence of a standing arrangement.",
                    Badge = current ? "Included" : "Available",
                    Accent = current
                        ? CACreationUI.Authored : CACreationUI.Accent,
                    Selected = current,
                    ConfirmLabel = current ? "Remove this mechanism"
                        : "Add this mechanism",
                    Choose = delegate
                    {
                        if (editingBeliefs)
                        {
                            if (current) CAPoliticalBeliefsModel.Remove(
                                beliefs, def.Key, local.Key);
                            else CAPoliticalBeliefsModel.Author(beliefs,
                                def.Key, local.Key);
                        }
                        else
                        {
                            if (current) CAFactionAxes.Remove(target,
                                def.Key, local.Key);
                            else CAFactionAxes.Add(target, def.Key,
                                local.Key, CAAxisSource.Authored);
                        }
                        changed?.Invoke();
                    }
                });
            }
            options.Add(new CACreationChoice
            {
                Key = "__clear__",
                Name = "Clear this subject",
                Summary = "Remove every recorded mechanism on this subject. "
                    + "An unset subject remains open rather than becoming a hidden default.",
                Details = def.Question,
                Badge = CAFactionAxes.StateOf(target, def.Key)
                    == CAAxisSource.Unset ? "Already open" : "Clear",
                Accent = CACreationUI.Unset,
                Selected = CAFactionAxes.StateOf(target, def.Key)
                    == CAAxisSource.Unset,
                ConfirmLabel = "Clear this subject",
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

        private void OpenPoliticalBeliefSets()
        {
            CACreationUI.OpenChoices("Political belief sets",
                "Apply a partial set of commitments. It adds only the listed "
                    + "mechanisms and preserves every unlisted choice.",
                CAAuthoringChoices.PoliticalBeliefSets(beliefs, seed,
                    changed));
        }

        private void OpenCurrentOrderSets()
        {
            var options = new List<CACreationChoice>();
            foreach (CAPoliticalPatchTemplate template in
                CAPoliticalPatchTemplates.CurrentOrder)
            {
                CAPoliticalPatchTemplate local = template;
                bool selected = local.Mechanisms.All(pair =>
                    pair.Value.All(value => CAFactionAxes.HasOption(
                        structure, pair.Key, value)));
                options.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label,
                    Summary = local.Summary,
                    CompactSummary = local.Domain,
                    Traits = string.Join(" · ", local.Mechanisms
                        .SelectMany(pair => pair.Value.Select(value =>
                            CAFactionAxes.AxisDef(pair.Key)?.Options
                                .FirstOrDefault(option => option.Key == value)
                                ?.Label ?? value))),
                    Details = "This is a partial current-order patch. It adds "
                        + "only the listed instituted mechanisms and preserves "
                        + "every unlisted fact.",
                    Group = local.Domain,
                    Badge = selected ? "Included" : "Current-order set",
                    Selected = selected,
                    Accent = CACreationUI.Authored,
                    ConfirmLabel = "Add to current order",
                    Choose = delegate
                    {
                        foreach (KeyValuePair<string, List<string>> patch in
                            local.Mechanisms)
                            foreach (string mechanism in patch.Value)
                                CAFactionAxes.Add(structure, patch.Key,
                                    mechanism, CAAxisSource.Authored);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices("Current-order sets",
                "Add partial instituted arrangements to this established "
                    + "society. Political beliefs remain separate.", options);
        }

        private void SavePoliticalBeliefSet()
        {
            int count = (beliefs.positions ?? new List<CAAxisEntry>())
                .Count(item => item != null
                    && item.source != (byte)CAAxisSource.Unset);
            if (count == 0)
            {
                Messages.Message("Add at least one political commitment "
                    + "before saving a belief set.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            string initial = "Saved political belief set";
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Save political belief set", initial, value =>
                {
                    CAAuthoringProfileLibrary.SavePoliticalBeliefs(value,
                        beliefs);
                    changed?.Invoke();
                }));
        }
    }

    internal enum CACultureAuthoringBoundary
    {
        Inherited,
        EstablishedLocal
    }

    internal sealed class Dialog_CACultureEditor : Window
    {
        private readonly CACulture culture;
        private readonly string factionLabel;
        private readonly Action changed;
        private readonly CACultureAuthoringBoundary boundary;
        private int section;
        private Vector2 scroll;
        private float viewHeight;
        private CACulturalMeaning editingMeaning;
        private CACulturePractice editingPractice;

        private static readonly string[] ApprovalAnchors =
            { "Condemned", "Disfavored", "Tolerated", "Approved", "Celebrated" };
        private static readonly string[] NormalityAnchors =
            { "Rare", "Unusual", "Ordinary", "Expected", "Pervasive" };
        private static readonly string[] PrestigeAnchors =
            { "Stigmatized", "Low status", "Neutral", "Respected", "Prestigious" };
        private static readonly string[] SalienceAnchors =
            { "Peripheral", "Noticeable", "Important", "Central", "Identity-defining" };
        private static readonly string[] PracticeAnchors =
            { "Occasional", "Repeated", "Established", "Strong", "Defining" };
        private static readonly int[] SignedAnchorValues =
            { -100, -50, 0, 50, 100 };
        private static readonly int[] UnsignedAnchorValues =
            { 0, 25, 50, 75, 100 };

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(980f, UI.screenWidth - 48f),
            Mathf.Min(580f, UI.screenHeight - 48f));

        internal Dialog_CACultureEditor(CACulture culture,
            string factionLabel, Action changed,
            CACultureAuthoringBoundary boundary =
                CACultureAuthoringBoundary.Inherited)
        {
            this.culture = culture ?? new CACulture();
            this.factionLabel = factionLabel;
            this.changed = changed;
            this.boundary = boundary;
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
            string description = boundary
                == CACultureAuthoringBoundary.EstablishedLocal
                ? "Set this established settlement's current local Culture "
                    + "at scenario start. Inherited Culture remains separate. "
                    + "These are direct initial facts; no transition is created."
                : "Set the Culture this population inherits. Settlements "
                    + "retain that history and develop lived practices through "
                    + "play. Visual tradition is optional.";
            float descriptionHeight = Text.CalcHeight(description, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, descriptionHeight),
                description);
            y += descriptionHeight + 12f;

            if (boundary == CACultureAuthoringBoundary.Inherited)
                y = DrawActions(inRect, y);
            string[] tabs = { "Overview", "Social meanings",
                boundary == CACultureAuthoringBoundary.EstablishedLocal
                    ? "Local practices" : "Inherited practices",
                "Visual tradition" };
            float tabHeight = CACreationUI.DrawSegmentRows(new Rect(0f, y,
                inRect.width, 30f), tabs, section, value =>
                {
                    section = value;
                    scroll = Vector2.zero;
                }, 165f);
            y += tabHeight + 10f;

            Rect outRect = new Rect(0f, y, inRect.width,
                inRect.height - y - 34f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float rowY = 0f;
            if (section == 0) DrawOverview(ref rowY, view.width);
            else if (section == 1) DrawMeanings(ref rowY, view.width);
            else if (section == 2) DrawPractices(ref rowY, view.width);
            else DrawVisual(ref rowY, view.width);
            viewHeight = rowY + 12f;
            Widgets.EndScrollView();
        }

        private float DrawActions(Rect inRect, float y)
        {
            var labels = new List<string>();
            var actions = new List<Action>();
            if (CAAuthoringProfileLibrary.Cultures.Count > 0)
            {
                labels.Add("Saved Cultures...");
                actions.Add(OpenCultureProfiles);
            }
            labels.Add("Save Culture...");
            actions.Add(SaveProfile);
            if (CAAuthoringProfileLibrary.Cultures.Count > 0)
            {
                labels.Add("Manage saved...");
                actions.Add(() => Find.WindowStack.Add(
                    new Dialog_CAProfileManager(culture, null, changed)));
            }
            int columns = inRect.width >= 700f ? 3 : 2;
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

        private void DrawOverview(ref float y, float width)
        {
            Row(ref y, width, "Name",
                culture.name ?? "Inherited Culture",
                () => Find.WindowStack.Add(new Dialog_CARenameCulture(
                    culture, changed)), FieldState(CACulture.NameField));
            DrawFact(ref y, width, "Inherited source",
                InheritedCultureName(culture));
            DrawFact(ref y, width, "Constituent cultures",
                culture.constituents.Count == 0 ? "Not recorded"
                    : string.Join(", ", culture.constituents.Select(item =>
                        item.share + "% " + (item.label ?? "unnamed"))));
            DrawFact(ref y, width, "Inherited social meanings",
                culture.inheritedMeanings.Count.ToString());
            DrawFact(ref y, width, "Inherited practices",
                culture.inheritedPractices.Count.ToString());
            DrawFact(ref y, width, "Current local meanings",
                culture.localMeanings.Count.ToString());
            DrawFact(ref y, width, "Current local practices",
                culture.practices.Count.ToString());
            CACulturalMeaning[] salient = culture.inheritedMeanings
                .Concat(culture.localMeanings)
                .OrderByDescending(item => item.salience).Take(3).ToArray();
            DrawFact(ref y, width, "Most salient",
                salient.Length == 0 ? "None composed" : string.Join(", ",
                    salient.Select(item => SubjectLabel(item.subjectKey)
                        + " - " + SalienceAnchors[NearestAnchor(
                            item.salience, UnsignedAnchorValues)]
                            .ToLowerInvariant())));
            int disputed = culture.inheritedMeanings.Concat(
                culture.localMeanings).GroupBy(item =>
                    item.subjectKey).Count(group => group.Min(item =>
                        item.approval) < 0 && group.Max(item =>
                            item.approval) > 0);
            DrawFact(ref y, width, "Internal disputes",
                disputed == 0 ? "None recorded" : disputed.ToString());
            CultureDef native = CACultureModel.NativeDef(culture);
            DrawFact(ref y, width, "Visual tradition",
                native?.LabelCap.ToString() ?? "None");
            y += 6f;
            bool hasCausalFacts = culture.inheritedMeanings.Count
                    + culture.localMeanings.Count
                    + culture.inheritedPractices.Count
                    + culture.practices.Count > 0;
            if (hasCausalFacts)
            {
                if (Widgets.ButtonText(new Rect(0f, y,
                        Mathf.Min(260f, width), 32f),
                        "Inspect causal effects..."))
                    Find.WindowStack.Add(
                        new Dialog_CACultureCausalInspector(culture));
                y += 42f;
            }
        }

        private void DrawMeanings(ref float y, float width)
        {
            if (Widgets.ButtonText(new Rect(0f, y, 220f, 32f),
                    "Add social meaning...")) OpenMeaningSubject();
            y += 44f;
            if (EditableMeanings.Count == 0)
            {
                DrawExplanation(ref y, width,
                    boundary == CACultureAuthoringBoundary.EstablishedLocal
                        ? "No direct local meaning is authored yet. Inherited "
                            + "meanings remain visible in Overview and the causal inspector."
                        : "Add a concrete registered subject. A name and visual "
                            + "tradition alone are not substantive Culture.");
                return;
            }
            foreach (CACulturalMeaning meaning in EditableMeanings
                .ToList()) DrawMeaning(ref y, width, meaning);
        }

        private void DrawMeaning(ref float y, float width,
            CACulturalMeaning meaning)
        {
            CASocialSubjectDef subject = CASocialSubjectRegistry.Find(
                meaning.subjectKey);
            bool editing = editingMeaning == meaning;
            string scopeLabel = MeaningScopeLabel(meaning);
            string title = (subject?.Label ?? "Recorded social meaning") + " - "
                + scopeLabel;
            string interpretation = MeaningInterpretation(meaning);
            float textWidth = Mathf.Max(120f, width - 24f);
            float interpretationHeight = Text.CalcHeight(interpretation,
                textWidth);
            float boxHeight = 52f + interpretationHeight;
            if (editing)
            {
                float segmentWidth = Mathf.Max(120f, width - 150f);
                boxHeight += 42f;
                boxHeight += AnchorRowHeight(segmentWidth, ApprovalAnchors) + 8f;
                boxHeight += AnchorRowHeight(segmentWidth, NormalityAnchors) + 8f;
                boxHeight += AnchorRowHeight(segmentWidth, PrestigeAnchors) + 8f;
                boxHeight += AnchorRowHeight(segmentWidth, SalienceAnchors) + 8f;
                boxHeight += 46f;
            }
            Rect box = new Rect(0f, y, width, boxHeight);
            Widgets.DrawMenuSection(box);
            float innerWidth = width - 24f;
            float at = y + 10f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(12f, at, innerWidth - 178f, 28f), title);
            if (Widgets.ButtonText(new Rect(width - 170f, at, 76f, 28f),
                    editing ? "Done" : "Edit"))
                editingMeaning = editing ? null : meaning;
            if (Widgets.ButtonText(new Rect(width - 88f, at, 76f, 28f),
                    "Remove"))
            {
                EditableMeanings.Remove(meaning);
                if (editingMeaning == meaning) editingMeaning = null;
                changed?.Invoke();
                y += boxHeight + 10f;
                return;
            }
            at += 34f;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(12f, at, innerWidth,
                interpretationHeight), interpretation);
            GUI.color = Color.white;
            at += interpretationHeight + 8f;
            if (editing)
            {
                Widgets.Label(new Rect(12f, at, 126f, 28f), "Population");
                if (Widgets.ButtonText(new Rect(142f, at,
                        innerWidth - 130f, 28f), scopeLabel))
                    OpenMeaningScope(meaning);
                at += 40f;
                DrawMeaningAnchor(ref at, width, "Approval", ApprovalAnchors,
                    meaning.approval, SignedAnchorValues, value =>
                    {
                        meaning.approval = value;
                        MeaningChanged(meaning);
                    });
                DrawMeaningAnchor(ref at, width, "Normality", NormalityAnchors,
                    meaning.normality, UnsignedAnchorValues, value =>
                    {
                        meaning.normality = value;
                        MeaningChanged(meaning);
                    });
                DrawMeaningAnchor(ref at, width, "Prestige", PrestigeAnchors,
                    meaning.prestige, SignedAnchorValues, value =>
                    {
                        meaning.prestige = value;
                        MeaningChanged(meaning);
                    });
                DrawMeaningAnchor(ref at, width, "Salience", SalienceAnchors,
                    meaning.salience, UnsignedAnchorValues, value =>
                    {
                        meaning.salience = value;
                        MeaningChanged(meaning);
                    });
                if (Widgets.ButtonText(new Rect(12f, at,
                        Mathf.Min(230f, innerWidth), 30f),
                        "Fine-tune values..."))
                    Find.WindowStack.Add(new Dialog_CACultureValueFineTune(
                        meaning, changed));
            }
            y += boxHeight + 10f;
        }

        private void OpenMeaningScope(CACulturalMeaning meaning)
        {
            bool allOccupied = MeaningScopeOccupied(meaning, "*");
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "*", Name = "All constituent populations",
                    Summary = "This meaning applies across the composed Culture.",
                    Badge = (meaning.populationScope ?? "*") == "*"
                        ? "Current scope" : null,
                    Selected = (meaning.populationScope ?? "*") == "*",
                    Disabled = allOccupied,
                    DisabledReason = allOccupied
                        ? "This social meaning already has a population-wide record."
                        : null,
                    ConfirmLabel = "Use this population scope",
                    Choose = delegate
                    {
                        meaning.populationScope = "*";
                        meaning.lastChangedTick = -1;
                        CACultureModel.Normalize(culture);
                        changed?.Invoke();
                    }
                }
            };
            foreach (CACultureConstituent constituent in culture.constituents
                .Where(item => item != null && !item.cultureId.NullOrEmpty())
                .OrderByDescending(item => item.share)
                .ThenBy(item => item.label))
            {
                CACultureConstituent local = constituent;
                bool occupied = MeaningScopeOccupied(meaning,
                    local.cultureId);
                options.Add(new CACreationChoice
                {
                    Key = local.cultureId,
                    Name = local.label ?? local.cultureId,
                    Summary = local.share + "% of the recorded population.",
                    Badge = meaning.populationScope == local.cultureId
                        ? "Current scope" : "Constituent Culture",
                    Selected = meaning.populationScope == local.cultureId,
                    Disabled = occupied,
                    DisabledReason = occupied
                        ? "This social meaning already has a record for this population."
                        : null,
                    ConfirmLabel = "Use this population scope",
                    Choose = delegate
                    {
                        meaning.populationScope = local.cultureId;
                        meaning.lastChangedTick = -1;
                        CACultureModel.Normalize(culture);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices("Meaning population",
                "Choose exactly which constituent population carries this "
                + "interpretation. This changes scope, not population share.",
                options);
        }

        private bool MeaningScopeOccupied(CACulturalMeaning current,
            string scope)
        {
            string normalized = scope.NullOrEmpty() ? "*" : scope;
            return EditableMeanings.Any(item => item != null
                && item != current && item.subjectKey == current.subjectKey
                && (item.populationScope.NullOrEmpty()
                    ? "*" : item.populationScope) == normalized);
        }

        private void DrawMeaningAnchor(ref float y, float width, string label,
            string[] labels, int value, int[] values, Action<int> choose)
        {
            const float labelWidth = 130f;
            Widgets.Label(new Rect(12f, y + 4f, labelWidth - 8f, 28f), label);
            float segmentWidth = Mathf.Max(120f, width - labelWidth - 24f);
            float height = CACreationUI.DrawSegmentRows(new Rect(
                labelWidth + 12f, y, segmentWidth, 28f), labels,
                ExactAnchor(value, values), index => choose(values[index]), 92f);
            y += height + 8f;
        }

        private static float AnchorRowHeight(float width, string[] labels)
        {
            const float gap = 4f;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + gap)
                / 96f), 1, labels.Length);
            int rows = (labels.Length + columns - 1) / columns;
            return rows * 28f + (rows - 1) * gap;
        }

        private static int ExactAnchor(int value, int[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (value == values[i]) return i;
            return -1;
        }

        private static int NearestAnchor(int value, int[] values)
        {
            int best = 0;
            int distance = int.MaxValue;
            for (int i = 0; i < values.Length; i++)
            {
                int candidate = Math.Abs(value - values[i]);
                if (candidate >= distance) continue;
                best = i;
                distance = candidate;
            }
            return best;
        }

        private string MeaningScopeLabel(CACulturalMeaning meaning)
        {
            return (meaning.populationScope ?? "*") == "*"
                ? "All constituent populations"
                : culture.constituents.FirstOrDefault(item => item != null
                    && item.cultureId == meaning.populationScope)?.label
                    ?? meaning.populationScope;
        }

        private static string MeaningInterpretation(CACulturalMeaning meaning)
        {
            string normality = NormalityAnchors[NearestAnchor(
                meaning.normality, UnsignedAnchorValues)];
            string approval = ApprovalAnchors[NearestAnchor(
                meaning.approval, SignedAnchorValues)].ToLowerInvariant();
            string prestige = PrestigeAnchors[NearestAnchor(
                meaning.prestige, SignedAnchorValues)].ToLowerInvariant();
            string salience = SalienceAnchors[NearestAnchor(
                meaning.salience, UnsignedAnchorValues)].ToLowerInvariant();
            return normality + " and " + approval + ". It carries "
                + prestige + " standing and is " + salience
                + " in daily life.";
        }

        private void MeaningChanged(CACulturalMeaning meaning)
        {
            meaning.lastChangedTick = -1;
            CACultureModel.Normalize(culture);
            changed?.Invoke();
        }

        private void DrawPractices(ref float y, float width)
        {
            if (Widgets.ButtonText(new Rect(0f, y, 230f, 32f),
                    boundary == CACultureAuthoringBoundary.EstablishedLocal
                        ? "Add local practice..." : "Add inherited practice..."))
                OpenPracticeSubject();
            y += 44f;
            if (EditablePractices.Count == 0)
                DrawExplanation(ref y, width,
                    boundary == CACultureAuthoringBoundary.EstablishedLocal
                        ? "No current local practice is declared at the scenario boundary."
                        : "No repeated practice is claimed at the starting boundary.");
            foreach (CACulturePractice practice in EditablePractices
                .ToList())
            {
                CACulturalPracticeDef practiceDef =
                    CACulturalPracticeRegistry.Find(practice.practiceKey);
                bool editing = editingPractice == practice;
                string summary = PracticeAnchors[NearestAnchor(
                    practice.strength, UnsignedAnchorValues)]
                    + " practice. " + (practiceDef?.Summary
                        ?? practice.summary ?? "Repeated conduct is recorded.");
                float summaryHeight = Text.CalcHeight(summary, width - 24f);
                float boxHeight = 52f + summaryHeight;
                if (editing)
                    boxHeight += AnchorRowHeight(width - 154f,
                        PracticeAnchors) + 50f;
                Rect box = new Rect(0f, y, width, boxHeight);
                Widgets.DrawMenuSection(box);
                Widgets.Label(new Rect(12f, y + 8f, width - 192f, 28f),
                    practiceDef?.Label ?? "Recorded practice");
                if (Widgets.ButtonText(new Rect(width - 170f, y + 8f, 76f,
                        28f), editing ? "Done" : "Edit"))
                    editingPractice = editing ? null : practice;
                if (Widgets.ButtonText(new Rect(width - 88f, y + 8f, 76f,
                        28f), "Remove"))
                {
                    EditablePractices.Remove(practice);
                    if (editingPractice == practice) editingPractice = null;
                    changed?.Invoke();
                    y += boxHeight + 10f;
                    continue;
                }
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(12f, y + 40f, width - 24f,
                    summaryHeight), summary);
                GUI.color = Color.white;
                if (editing)
                {
                    float at = y + 48f + summaryHeight;
                    DrawMeaningAnchor(ref at, width, "Strength",
                        PracticeAnchors, practice.strength,
                        UnsignedAnchorValues, value =>
                        {
                            practice.strength = value;
                            changed?.Invoke();
                        });
                    if (Widgets.ButtonText(new Rect(12f, at,
                            Mathf.Min(230f, width - 24f), 30f),
                            "Fine-tune value..."))
                        Find.WindowStack.Add(
                            new Dialog_CACultureValueFineTune(
                                practice, changed));
                }
                y += boxHeight + 10f;
            }
        }

        private void DrawVisual(ref float y, float width)
        {
            CultureDef native = CACultureModel.NativeDef(culture);
            string value = native?.LabelCap.ToString()
                ?? (culture.sourceCultureDefName.NullOrEmpty()
                    ? "Neutral fallback"
                    : "Saved visual tradition unavailable - neutral fallback");
            Row(ref y, width, "Visual tradition", value,
                OpenSourceCulture, FieldState(CACulture.SourceCultureField));
            DrawExplanation(ref y, width,
                "This optional tradition supplies native RimWorld styles. "
                + "Neutral fallback uses the ordinary object style.");
        }

        private void OpenMeaningSubject()
        {
            var options = new List<CACreationChoice>();
            foreach (CASocialSubjectDef subject in
                CASocialSubjectRegistry.Authorable())
            {
                CASocialSubjectDef local = subject;
                bool active = EditableMeanings.Any(item =>
                    item.subjectKey == local.Key
                    && (item.populationScope ?? "*") == "*");
                options.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label,
                    Summary = local.Description,
                    CompactSummary = local.Applicability,
                    Details = local.CulturalEffect + "\n\nObserved through: "
                        + local.AuthoritativeSource + ".\n\nUsed by: "
                        + string.Join(", ", local.Consumers) + ".",
                    Badge = active ? "Already present" : null,
                    Disabled = active,
                    DisabledReason = active ? "This population-wide meaning is already active." : null,
                    Accent = CACreationUI.Authored,
                    ConfirmLabel = "Add this social meaning",
                    Choose = delegate
                    {
                        EditableMeanings.Add(new CACulturalMeaning
                        {
                            subjectKey = local.Key,
                            populationScope = "*",
                            provenance = boundary
                                == CACultureAuthoringBoundary.EstablishedLocal
                                    ? "authored initial local state" : "inherited",
                            sourceIdentity = boundary
                                == CACultureAuthoringBoundary.EstablishedLocal
                                    ? culture.localityKey
                                        ?? "authored settlement baseline"
                                    : culture.parentId ?? "authored before scenario",
                            firstRecordedTick = -1,
                            lastChangedTick = -1,
                            weight = 100,
                            normality = 50,
                            salience = 50
                        });
                        CACultureModel.Normalize(culture);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices("Add social meaning",
                "Choose something this Culture regards as ordinary, proper, "
                + "honorable, or important.", options);
        }

        private void OpenPracticeSubject()
        {
            var options = new List<CACreationChoice>();
            foreach (CACulturalPracticeDef practiceDef in
                CACulturalPracticeRegistry.All)
            {
                CACulturalPracticeDef local = practiceDef;
                bool active = EditablePractices.Any(item =>
                    item.practiceKey == local.Key);
                options.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label,
                    Summary = local.Summary,
                    CompactSummary = local.Activity,
                    Group = local.PrimaryFacet,
                    Details = (boundary
                        == CACultureAuthoringBoundary.EstablishedLocal
                            ? "Record this as the established settlement's "
                                + "current local practice at scenario start. "
                                + "No transition event is created."
                            : "Record this as a repeated practice inherited "
                                + "before the scenario boundary. Meaning and "
                                + "practice remain separate records.")
                        + "\n\nActivity: " + local.Activity
                        + ".\nActors: " + local.ActorRole
                        + (local.TargetRole.NullOrEmpty() ? "." : "; target: "
                            + local.TargetRole + ".")
                        + "\nTrigger and cadence: " + local.Trigger + "; "
                            + local.Cadence + "."
                        + "\nOperator and authority: " + local.Operator
                            + "; " + local.AuthorityBasis + "."
                        + "\nSetting and material: " + local.Setting + "; "
                            + local.MaterialRequirements + "."
                        + "\nConditions: " + local.Conditions + "."
                        + "\nObserved through: " + local.EvidenceSource + "."
                        + "\nUsed by: " + local.RuntimeConsumer + ".",
                    Badge = active ? "Already present" : null,
                    Disabled = active,
                    DisabledReason = active
                        ? boundary == CACultureAuthoringBoundary.EstablishedLocal
                            ? "This local practice is already active."
                            : "This inherited practice is already active."
                        : null,
                    Accent = boundary
                        == CACultureAuthoringBoundary.EstablishedLocal
                            ? CACreationUI.Authored : CACreationUI.Inherited,
                    ConfirmLabel = boundary
                        == CACultureAuthoringBoundary.EstablishedLocal
                            ? "Add local practice" : "Add inherited practice",
                    Choose = delegate
                    {
                        EditablePractices.Add(new CACulturePractice
                        {
                            practiceKey = local.Key,
                            summary = local.Summary,
                            strength = 50,
                            firstRecordedTick = -1,
                            lastObservedTick = -1,
                            sourceOwner = boundary
                                == CACultureAuthoringBoundary.EstablishedLocal
                                    ? culture.localityKey
                                        ?? "authored settlement baseline"
                                    : culture.parentId
                                        ?? "authored inherited background",
                            sourceSignature = (boundary
                                == CACultureAuthoringBoundary.EstablishedLocal
                                    ? "authored-initial-local:"
                                    : "authored-inherited:") + local.Key,
                            sourcePeriod = boundary
                                == CACultureAuthoringBoundary.EstablishedLocal
                                    ? "established before scenario start"
                                    : "before scenario start"
                        });
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices(boundary
                    == CACultureAuthoringBoundary.EstablishedLocal
                        ? "Add local practice" : "Add inherited practice",
                "Choose a concrete repeated practice known at the starting boundary.",
                options);
        }

        private List<CACulturalMeaning> EditableMeanings => boundary
            == CACultureAuthoringBoundary.EstablishedLocal
                ? culture.localMeanings : culture.inheritedMeanings;

        private List<CACulturePractice> EditablePractices => boundary
            == CACultureAuthoringBoundary.EstablishedLocal
                ? culture.practices : culture.inheritedPractices;

        private static void DrawFact(ref float y, float width, string label,
            string value)
        {
            float labelWidth = Mathf.Min(220f, width * 0.30f);
            float height = Mathf.Max(26f, Text.CalcHeight(value ?? "Not recorded",
                width - labelWidth - 12f));
            Widgets.Label(new Rect(0f, y, labelWidth, height), label);
            GUI.color = new Color(0.76f, 0.79f, 0.83f);
            Widgets.Label(new Rect(labelWidth + 12f, y,
                width - labelWidth - 12f, height), value ?? "Not recorded");
            GUI.color = Color.white;
            y += height + 9f;
        }

        private static string SubjectLabel(string key)
        {
            return CASocialSubjectRegistry.Find(key)?.Label
                ?? "Recorded social meaning";
        }

        private string InheritedCultureName(CACulture value)
        {
            if (value == null || value.parentId.NullOrEmpty())
                return "No earlier Culture recorded";
            CACultureConstituent parent = value.constituents
                ?.FirstOrDefault(item => item != null
                    && item.cultureId == value.parentId);
            if (parent != null && !parent.label.NullOrEmpty())
                return parent.label;
            return "An earlier Culture recorded in this history";
        }

        private static int CurrentTick()
        {
            try { return Find.TickManager?.TicksGame ?? -1; }
            catch { return -1; }
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
            // Provenance remains inspectable in causal details. It is not a
            // default-view status chip because it does not change the action.
            Rect valueRect = stacked
                ? new Rect(0f, y + labelHeight + 4f, width, 32f)
                : new Rect(labelWidth + 8f, y, width - labelWidth - 8f,
                    Mathf.Max(32f, labelHeight));
            if (Widgets.ButtonText(valueRect, value ?? "Choose")) edit?.Invoke();
            y = valueRect.yMax + 8f;
        }

        private void OpenCultureProfiles()
        {
            if (CAAuthoringProfileLibrary.Cultures.Count == 0) return;
            CACreationUI.OpenChoices("Saved Cultures",
                "Use a saved Culture. RimWorld style sources are "
                    + "chosen separately under Visual tradition.",
                CAAuthoringChoices.CultureProfiles(culture,
                    culture.id ?? factionLabel ?? "ca-culture", changed));
        }

        private void SaveProfile()
        {
            string failure = CACultureModel.SubstantiveFailure(culture);
            if (!failure.NullOrEmpty())
            {
                Messages.Message(failure, MessageTypeDefOf.RejectInput,
                    false);
                return;
            }
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Save Culture",
                culture.name ?? "Saved Culture", value =>
                {
                    CAAuthoringProfileLibrary.SaveCulture(value, culture);
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
                Summary = "Use base object styles when no visual tradition applies.",
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
                        ? "RimWorld style categories and visual tradition."
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

    internal sealed class Dialog_CACultureValueFineTune : Window
    {
        private readonly CACulturalMeaning meaning;
        private readonly CACulturePractice practice;
        private readonly Action changed;

        public override Vector2 InitialSize => new Vector2(620f,
            meaning == null ? 210f : 350f);

        internal Dialog_CACultureValueFineTune(CACulturalMeaning meaning,
            Action changed)
        {
            this.meaning = meaning;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        internal Dialog_CACultureValueFineTune(CACulturePractice practice,
            Action changed)
        {
            this.practice = practice;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont prior = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "Fine-tune values");
            Text.Font = GameFont.Small;
            float y = 46f;
            if (meaning != null)
            {
                int approval = Slider(ref y, inRect.width, "Approval",
                    meaning.approval, -100, 100);
                int normality = Slider(ref y, inRect.width, "Normality",
                    meaning.normality, 0, 100);
                int prestige = Slider(ref y, inRect.width, "Prestige",
                    meaning.prestige, -100, 100);
                int salience = Slider(ref y, inRect.width, "Salience",
                    meaning.salience, 0, 100);
                if (approval != meaning.approval
                    || normality != meaning.normality
                    || prestige != meaning.prestige
                    || salience != meaning.salience)
                {
                    meaning.approval = approval;
                    meaning.normality = normality;
                    meaning.prestige = prestige;
                    meaning.salience = salience;
                    meaning.lastChangedTick = -1;
                    changed?.Invoke();
                }
            }
            else if (practice != null)
            {
                int strength = Slider(ref y, inRect.width, "Strength",
                    practice.strength, 0, 100);
                if (strength != practice.strength)
                {
                    practice.strength = strength;
                    changed?.Invoke();
                }
            }
            Text.Font = prior;
        }

        private static int Slider(ref float y, float width, string label,
            int value, int minimum, int maximum)
        {
            const float labelWidth = 150f;
            Widgets.Label(new Rect(0f, y + 4f, labelWidth, 28f),
                label + " " + value);
            float result = Widgets.HorizontalSlider(new Rect(labelWidth, y,
                width - labelWidth, 28f), value, minimum, maximum, false);
            y += 42f;
            return Mathf.RoundToInt(result);
        }
    }

    internal sealed class Dialog_CACultureCausalInspector : Window
    {
        private readonly CACulture culture;
        private Vector2 scroll;
        private float viewHeight;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(860f, UI.screenWidth - 48f),
            Mathf.Min(620f, UI.screenHeight - 48f));

        internal Dialog_CACultureCausalInspector(CACulture culture)
        {
            this.culture = culture ?? new CACulture();
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont prior = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "Causal effects");
            Text.Font = GameFont.Small;
            const string introduction = "These saved meanings and practices "
                + "shape interpretation where the listed facts occur.";
            float introHeight = Text.CalcHeight(introduction, inRect.width);
            Widgets.Label(new Rect(0f, 40f, inRect.width, introHeight),
                introduction);
            float top = 50f + introHeight;
            Rect outRect = new Rect(0f, top, inRect.width,
                inRect.height - top - 34f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            foreach (CACulturalMeaning meaning in culture.inheritedMeanings
                .Concat(culture.localMeanings)
                .OrderByDescending(item => item.salience))
            {
                CASocialSubjectDef subject = CASocialSubjectRegistry.Find(
                    meaning.subjectKey);
                string detail = "Approval " + meaning.approval
                    + "; normality " + meaning.normality + "; prestige "
                    + meaning.prestige + "; salience " + meaning.salience
                    + ".\nObserved through: "
                    + (subject?.AuthoritativeSource ?? "source unavailable")
                    + ". Used by: " + string.Join(", ",
                        subject?.Consumers ?? new List<string>()) + ".";
                DrawFact(ref y, view.width,
                    subject?.Label ?? "Recorded social meaning", detail);
            }
            foreach (CACulturePractice practice in culture.inheritedPractices
                .Concat(culture.practices)
                .OrderByDescending(item => item.strength))
            {
                CACulturalPracticeDef practiceDef =
                    CACulturalPracticeRegistry.Find(practice.practiceKey);
                DrawFact(ref y, view.width,
                    practiceDef?.Label ?? "Recorded practice",
                    "Strength " + practice.strength + ". Activity: "
                    + (practiceDef?.Activity ?? "unavailable")
                    + ". Evidence: "
                    + (practiceDef?.EvidenceSource ?? "unavailable")
                    + ". Used by: "
                    + (practiceDef?.RuntimeConsumer ?? "unavailable") + ".");
            }
            viewHeight = y + 12f;
            Widgets.EndScrollView();
            Text.Font = prior;
        }

        private static void DrawFact(ref float y, float width, string label,
            string detail)
        {
            float labelHeight = Text.CalcHeight(label, width);
            GUI.color = Color.white;
            Widgets.Label(new Rect(0f, y, width, labelHeight), label);
            y += labelHeight + 3f;
            GUI.color = ColoredText.SubtleGrayColor;
            float detailHeight = Text.CalcHeight(detail, width);
            Widgets.Label(new Rect(0f, y, width, detailHeight), detail);
            GUI.color = Color.white;
            y += detailHeight + 14f;
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
