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
        // Direct Culture-question evidence owns a measured population fact.
        // These fields are absent from ordinary practice observations. The
        // position remains directional; participation and dispersion describe
        // how much represented evidence supports the appraisal.
        public bool hasQuestionEvidence;
        public float questionPosition;
        public float questionDispersion;
        public int observedPawnCount;
        public int eligiblePopulation;

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
            Scribe_Values.Look(ref hasQuestionEvidence,
                "hasQuestionEvidence", false, forceSave: true);
            Scribe_Values.Look(ref questionPosition,
                "questionPosition", 0f, forceSave: true);
            Scribe_Values.Look(ref questionDispersion,
                "questionDispersion", 0f, forceSave: true);
            Scribe_Values.Look(ref observedPawnCount,
                "observedPawnCount", 0, forceSave: true);
            Scribe_Values.Look(ref eligiblePopulation,
                "eligiblePopulation", 0, forceSave: true);
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
                observationCount = observationCount,
                hasQuestionEvidence = hasQuestionEvidence,
                questionPosition = questionPosition,
                questionDispersion = questionDispersion,
                observedPawnCount = observedPawnCount,
                eligiblePopulation = eligiblePopulation
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
        public const int CurrentSchemaVersion = 10;
        public const int NameField = 1;
        public const int SourceCultureField = 2;
        public const int QuestionStateField = 4;
        public const int DiversityField = 8;
        public const int AllAuthoredFields = NameField | SourceCultureField
            | QuestionStateField | DiversityField;
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
        public int questionRegistryVersion =
            CACultureQuestionRegistry.CurrentVersion;
        // Population defaults apply unless one question supplies an explicit
        // spread or subgroup mixture. Both controls retain a nonzero variance
        // floor; they describe distributions rather than manufacturing a
        // token dissenter.
        public int withinGroupSpread = 2;
        public int subgroupSeparation = 2;
        public List<CACultureQuestionDistribution> inheritedQuestions =
            new List<CACultureQuestionDistribution>();
        public List<CACultureQuestionDistribution> localQuestions =
            new List<CACultureQuestionDistribution>();
        public List<CACultureLegacyEvidence> legacyEvidence =
            new List<CACultureLegacyEvidence>();
        // B11 meanings are a load-only migration envelope. Schema 10 never
        // writes them and no current authoring or generation path consumes
        // them directly.
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
            Scribe_Values.Look(ref questionRegistryVersion,
                "questionRegistryVersion", 0);
            Scribe_Values.Look(ref withinGroupSpread,
                "withinGroupSpread", 2);
            Scribe_Values.Look(ref subgroupSeparation,
                "subgroupSeparation", 2);
            Scribe_Collections.Look(ref inheritedQuestions,
                "inheritedQuestions", LookMode.Deep);
            Scribe_Collections.Look(ref localQuestions,
                "localQuestions", LookMode.Deep);
            Scribe_Collections.Look(ref legacyEvidence,
                "legacyEvidence", LookMode.Deep);
            if (schemaVersion <= 9)
            {
                Scribe_Collections.Look(ref inheritedMeanings,
                    "inheritedMeanings", LookMode.Deep);
                Scribe_Collections.Look(ref localMeanings,
                    "localMeanings", LookMode.Deep);
            }
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
                questionRegistryVersion =
                    CACultureQuestionRegistry.CurrentVersion,
                withinGroupSpread = withinGroupSpread,
                subgroupSeparation = subgroupSeparation,
                inheritedQuestions = CopyQuestions(inheritedQuestions),
                localQuestions = CopyQuestions(localQuestions),
                legacyEvidence = (legacyEvidence
                        ?? new List<CACultureLegacyEvidence>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
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
                authoredMask = authoredMask & AllAuthoredFields
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
            questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            withinGroupSpread = source.withinGroupSpread;
            subgroupSeparation = source.subgroupSeparation;
            inheritedQuestions = CopyQuestions(source.inheritedQuestions);
            localQuestions = CopyQuestions(source.localQuestions);
            legacyEvidence = (source.legacyEvidence
                    ?? new List<CACultureLegacyEvidence>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
            inheritedMeanings = new List<CACulturalMeaning>();
            localMeanings = new List<CACulturalMeaning>();
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
            authoredMask = source.authoredMask & AllAuthoredFields;
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
                questionRegistryVersion =
                    CACultureQuestionRegistry.CurrentVersion,
                withinGroupSpread = withinGroupSpread,
                subgroupSeparation = subgroupSeparation,
                inheritedQuestions = CopyQuestions(inheritedQuestions),
                authoredMask = authoredMask & AllAuthoredFields
            };
        }

        internal void ApplyInheritedTemplate(CACulture template)
        {
            if (template == null) return;
            name = template.name;
            sourceCultureDefName = template.sourceCultureDefName;
            questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            withinGroupSpread = template.withinGroupSpread;
            subgroupSeparation = template.subgroupSeparation;
            inheritedQuestions = CopyQuestions(template.inheritedQuestions);
            inheritedMeanings = new List<CACulturalMeaning>();
            localMeanings = new List<CACulturalMeaning>();
            authoredMask = template.authoredMask & AllAuthoredFields;
            authoredMask |= QuestionStateField | DiversityField;
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

        private static List<CACultureQuestionDistribution> CopyQuestions(
            IEnumerable<CACultureQuestionDistribution> source)
        {
            return (source
                    ?? Enumerable.Empty<CACultureQuestionDistribution>())
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
                || culture.inheritedPractices == null
                || culture.practices == null)
                return "semantic collections are incomplete";
            bool currentQuestions = expectedSchema >= 10;
            if (currentQuestions && (culture.questionRegistryVersion
                    != CACultureQuestionRegistry.CurrentVersion
                || culture.inheritedQuestions == null
                || culture.localQuestions == null
                || culture.legacyEvidence == null))
                return "Culture question state is incomplete";
            if (!currentQuestions && (culture.inheritedMeanings == null
                || culture.localMeanings == null))
                return "legacy social-meaning collections are incomplete";
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
            if (currentQuestions)
            {
                string registryFailure =
                    CACultureQuestionRegistry.ValidationFailure();
                if (!registryFailure.NullOrEmpty())
                    return "Culture question registry: " + registryFailure;
                if (culture.withinGroupSpread < 0
                    || culture.withinGroupSpread > 4
                    || culture.subgroupSeparation < 0
                    || culture.subgroupSeparation > 4)
                    return "Culture population distribution setting is invalid";
                List<CACultureQuestionDistribution> questions =
                    culture.inheritedQuestions.Concat(
                        culture.localQuestions).ToList();
                foreach (CACultureQuestionDistribution question in questions)
                {
                    string failure = CACultureDistributionKernel
                        .ValidationFailure(question);
                    if (!failure.NullOrEmpty())
                        return "Culture question "
                            + (question?.questionKey ?? "unrecorded") + ": "
                            + failure;
                }
                if (culture.inheritedQuestions.GroupBy(QuestionIdentity,
                            StringComparer.Ordinal).Any(group => group.Count() > 1)
                    || culture.localQuestions.GroupBy(QuestionIdentity,
                            StringComparer.Ordinal).Any(group => group.Count() > 1))
                    return "a Culture question identity is duplicated";
                if (culture.legacyEvidence.Any(item => item == null
                        || item.sourceKey.NullOrEmpty()
                        || item.sourceLayer.NullOrEmpty()
                        || item.disposition.NullOrEmpty()
                        || item.summary.NullOrEmpty()))
                    return "legacy Culture evidence is incomplete";
            }
            else
            {
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
            }
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
                    || item.key.NullOrEmpty()
                    || (item.sourceDomain == "Culture question evidence"
                        ? !item.key.StartsWith("question:",
                            StringComparison.Ordinal)
                            || CACultureQuestionRegistry.Find(item.key
                                .Substring("question:".Length)) == null
                        : CACulturalPracticeRegistry.Find(item.key) == null)
                    || item.sourceOwner.NullOrEmpty()
                    || item.sourceDomain.NullOrEmpty()
                    || item.sourceSignature.NullOrEmpty()
                    || item.strength < 0 || item.strength > 100
                    || item.evidenceStartTick < -1
                    || item.firstObservedTick < -1
                    || item.lastObservedTick < -1
                    || item.observationCount < 0
                    || (item.sourceDomain == "Culture question evidence"
                        && item.observationCount > 0
                        && (!item.hasQuestionEvidence
                            || item.questionPosition < -1f
                            || item.questionPosition > 1f
                            || item.questionDispersion < 0f
                            || item.questionDispersion > 1f
                            || item.observedPawnCount < 0
                            || item.eligiblePopulation < 1
                            || item.observedPawnCount
                                > item.eligiblePopulation))))
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
                && (!currentQuestions || (culture.inheritedQuestions.Count == 0
                    && culture.localQuestions.Count == 0))
                && (currentQuestions || (culture.inheritedMeanings.Count == 0
                    && culture.localMeanings.Count == 0))
                && culture.inheritedPractices.Count == 0
                && culture.practices.Count == 0
                && (!currentQuestions || culture.legacyEvidence.Count == 0))
                return "Compose Culture with at least one cultural question "
                    + "or represented historical practice.";
            return null;
        }

        internal static bool TryUpgradeToCurrent(CACulture source,
            out CACulture upgraded, out string failure)
        {
            upgraded = null;
            if (source == null)
            {
                failure = "Culture is missing";
                return false;
            }
            if (source.schemaVersion == CACulture.CurrentSchemaVersion)
            {
                failure = ValidationFailure(source, requireSubstantive: true);
                if (!failure.NullOrEmpty()) return false;
                upgraded = source.Copy();
                return true;
            }
            if (source.schemaVersion != 8 && source.schemaVersion != 9)
            {
                failure = "Culture schema " + source.schemaVersion
                    + " has no supported B12 migration";
                return false;
            }

            failure = source.schemaVersion == 8
                ? LegacyB10ValidationFailure(source)
                : ValidationFailure(source, requireSubstantive: true,
                    expectedSchema: 9, validatePractices: true);
            if (!failure.NullOrEmpty()) return false;

            CACulture candidate = source.Copy();
            candidate.schemaVersion = 9;
            if (source.schemaVersion == 8)
            {
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
                foreach (CACultureObservation observation in
                    candidate.observations)
                {
                    if (CACulturalPracticeRegistry.Find(observation.key)
                        != null) continue;
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
            }

            // Copy() intentionally excludes the obsolete authoring model.
            // Migration reads those records from the untouched source and
            // admits only exact question adapters. Every original record is
            // retained as evidence, including dimensions with no current
            // semantic equivalent.
            candidate.inheritedQuestions = MigrateQuestions(
                source.inheritedMeanings, "inherited", source.id,
                candidate.legacyEvidence);
            candidate.localQuestions = MigrateQuestions(
                source.localMeanings, "local", source.localityKey ?? source.id,
                candidate.legacyEvidence);
            var localKeys = new HashSet<string>(candidate.localQuestions
                .Select(QuestionIdentity), StringComparer.Ordinal);
            candidate.inheritedQuestions.RemoveAll(value =>
                localKeys.Contains(QuestionIdentity(value)));
            candidate.inheritedMeanings = new List<CACulturalMeaning>();
            candidate.localMeanings = new List<CACulturalMeaning>();
            candidate.questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            candidate.withinGroupSpread = 2;
            candidate.subgroupSeparation = 2;
            candidate.schemaVersion = CACulture.CurrentSchemaVersion;
            failure = ValidationFailure(candidate, requireSubstantive: true);
            if (!failure.NullOrEmpty()) return false;
            upgraded = candidate;
            return true;
        }

        internal static bool TryUpgradeFromB10(CACulture source,
            out CACulture upgraded, out string failure)
        {
            return TryUpgradeToCurrent(source, out upgraded, out failure);
        }

        private static List<CACultureQuestionDistribution> MigrateQuestions(
            IEnumerable<CACulturalMeaning> source, string layer,
            string sourceIdentity, List<CACultureLegacyEvidence> evidence)
        {
            var mapped = new List<CACultureQuestionDistribution>();
            foreach (IGrouping<string, CACulturalMeaning> group in (source
                    ?? Enumerable.Empty<CACulturalMeaning>())
                .Where(value => value != null)
                .GroupBy(value => (CACultureQuestionRegistry
                        .QuestionForSocialSubject(value.subjectKey) ?? "")
                    + "\0" + (value.populationScope.NullOrEmpty()
                        ? "*" : value.populationScope),
                    StringComparer.Ordinal))
            {
                foreach (CACulturalMeaning meaning in group)
                    evidence.Add(new CACultureLegacyEvidence
                    {
                        sourceKey = meaning.subjectKey,
                        sourceLayer = "B11 " + layer + " social meaning",
                        populationScope = meaning.populationScope.NullOrEmpty()
                            ? "*" : meaning.populationScope,
                        disposition = group.Key.StartsWith("\0",
                            StringComparison.Ordinal)
                            ? "preserved as evidence; no exact B12 question"
                            : "approval and salience mapped through exact "
                                + "subject adapter; other dimensions remain "
                                + "evidence",
                        summary = "approval=" + meaning.approval
                            + "; normality=" + meaning.normality
                            + "; prestige=" + meaning.prestige
                            + "; salience=" + meaning.salience
                            + "; weight=" + meaning.weight
                            + "; firstRecordedTick="
                            + meaning.firstRecordedTick
                            + "; lastChangedTick="
                            + meaning.lastChangedTick,
                        sourceIdentity = meaning.sourceIdentity
                            ?? sourceIdentity ?? "B11 Culture",
                        evidenceSignature = meaning.evidenceSignature,
                        firstRecordedTick = meaning.firstRecordedTick,
                        lastChangedTick = meaning.lastChangedTick
                    });
                string[] identity = group.Key.Split('\0');
                string questionKey = identity[0];
                if (questionKey.NullOrEmpty()) continue;
                CALegacyCultureQuestionAdapterResult adapted =
                    CALegacyCultureQuestionAdapter.Adapt(group.Select(value =>
                        new CALegacyCultureMeaningAdapterInput(value.approval,
                            value.salience, value.weight,
                            CACultureQuestionRegistry
                                .DirectionForSocialSubject(value.subjectKey))));
                mapped.Add(new CACultureQuestionDistribution
                {
                    questionKey = questionKey,
                    populationScope = identity.Length > 1
                        ? identity[1] : "*",
                    mean = adapted.Mean,
                    hasDescriptiveNormPrior = false,
                    descriptiveNormPrior = 0f,
                    prestigeSignal = 0f,
                    spread = 0.28f,
                    salience = adapted.Salience,
                    normStrength = 0.50f,
                    visibility = 0.65f,
                    sourceConfidence = 0.55f,
                    toleranceForDivergence = 0.50f,
                    provenance = "B12 exact adapter from B11 " + layer
                        + " approval and salience; other dimensions retained "
                        + "as evidence",
                    sourceIdentity = sourceIdentity ?? "B11 Culture",
                    evidenceSignature = CASocialPatternKernel.StableHash(
                        string.Join("|", group.Select(value =>
                            value.subjectKey + ":" + value.approval + ":"
                            + value.normality + ":" + value.prestige + ":"
                            + value.salience + ":" + value.weight))),
                    firstRecordedTick = group.Where(value =>
                            value.firstRecordedTick >= 0)
                        .Select(value => value.firstRecordedTick)
                        .DefaultIfEmpty(-1).Min(),
                    lastChangedTick = group.Where(value =>
                            value.lastChangedTick >= 0)
                        .Select(value => value.lastChangedTick)
                        .DefaultIfEmpty(-1).Max()
                });
            }
            return mapped;
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

        private static string QuestionIdentity(
            CACultureQuestionDistribution question)
        {
            return (question?.questionKey ?? "") + "\0"
                + (question?.populationScope.NullOrEmpty() == false
                    ? question.populationScope : "*");
        }

        internal static void Normalize(CACulture culture)
        {
            if (culture == null) return;
            if (culture.constituents == null)
                culture.constituents = new List<CACultureConstituent>();
            if (culture.inheritedQuestions == null)
                culture.inheritedQuestions =
                    new List<CACultureQuestionDistribution>();
            if (culture.localQuestions == null)
                culture.localQuestions =
                    new List<CACultureQuestionDistribution>();
            if (culture.legacyEvidence == null)
                culture.legacyEvidence = new List<CACultureLegacyEvidence>();
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
            NormalizeQuestions(culture.inheritedQuestions);
            NormalizeQuestions(culture.localQuestions);
            // Local rows override inherited rows with the same identity while
            // retaining the predecessor so the author can release the local
            // override without reconstructing history.
            culture.inheritedMeanings.Clear();
            culture.localMeanings.Clear();
            culture.legacyEvidence.RemoveAll(item => item == null
                || item.sourceKey.NullOrEmpty());
            foreach (CACultureLegacyEvidence evidence in culture.legacyEvidence)
                if (evidence.populationScope.NullOrEmpty())
                    evidence.populationScope = "*";
            culture.legacyEvidence = culture.legacyEvidence
                .GroupBy(item => item.sourceLayer + "\0" + item.sourceKey
                    + "\0" + item.sourceIdentity + "\0"
                    + item.evidenceSignature, StringComparer.Ordinal)
                .Select(group => group.First()).ToList();
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
            culture.questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            culture.withinGroupSpread = Mathf.Clamp(
                culture.withinGroupSpread, 0, 4);
            culture.subgroupSeparation = Mathf.Clamp(
                culture.subgroupSeparation, 0, 4);
            foreach (CACultureQuestionDistribution question in culture
                .inheritedQuestions.Concat(culture.localQuestions))
            {
                if (!question.spreadOverride)
                    question.spread = PopulationSpread(
                        culture.withinGroupSpread);
                SyncSubgroups(culture, question);
            }
            culture.schemaVersion = CACulture.CurrentSchemaVersion;
        }

        private static float PopulationSpread(int setting)
        {
            return new[] { 0.10f, 0.18f, 0.28f, 0.42f, 0.60f }
                [Mathf.Clamp(setting, 0, 4)];
        }

        private static void SyncSubgroups(CACulture culture,
            CACultureQuestionDistribution question)
        {
            if (question == null) return;
            if ((question.populationScope ?? "*") != "*")
            {
                question.subgroups.Clear();
                return;
            }
            List<CACultureConstituent> constituents = culture.constituents
                .Where(value => value != null && value.share > 0)
                .OrderBy(value => value.cultureId ?? "label:" + value.label,
                    StringComparer.Ordinal).ToList();
            bool generated = question.subgroups.Count == 0
                || question.subgroups.All(value => value?.inherited == true);
            if (!generated) return;
            if (constituents.Count <= 1)
            {
                question.subgroups.Clear();
                return;
            }
            float separation = new[] { 0f, 0.5f, 1f, 1.5f, 2f }
                [Mathf.Clamp(culture.subgroupSeparation, 0, 4)];
            List<CACultureQuestionDistribution> represented = culture
                .localQuestions.Concat(culture.inheritedQuestions)
                .Where(value => value != null
                    && value.questionKey == question.questionKey
                    && (value.populationScope ?? "*") != "*").ToList();
            // Constituent identity, share, and a constituent-scoped question
            // are factual. The separation control scales those represented
            // differences; it never assigns a sign from name or list order.
            question.subgroups = constituents.Select(value =>
            {
                string subgroupKey = value.cultureId.NullOrEmpty()
                    ? "unrecorded:" + CASocialPatternKernel.StableHash(
                        value.label ?? "population") : value.cultureId;
                CACultureQuestionDistribution scoped = represented
                    .FirstOrDefault(item => item.populationScope
                        == subgroupKey);
                float offset = scoped == null ? 0f
                    : (scoped.mean - question.mean) * separation;
                return
                new CACultureSubgroupDistribution
                {
                    subgroupKey = subgroupKey,
                    label = value.label,
                    share = value.share,
                    meanOffset = Mathf.Clamp(offset, -0.90f, 0.90f),
                    spreadMultiplier = 1f,
                    inherited = true
                };
            }).ToList();
        }

        private static void NormalizeQuestions(
            List<CACultureQuestionDistribution> values)
        {
            values.RemoveAll(item => item == null
                || CACultureQuestionRegistry.Find(item.questionKey) == null);
            foreach (CACultureQuestionDistribution item in values)
            {
                item.schemaVersion =
                    CACultureQuestionDistribution.CurrentSchemaVersion;
                if (item.populationScope.NullOrEmpty())
                    item.populationScope = "*";
                item.mean = Mathf.Clamp(item.mean, -1f, 1f);
                item.descriptiveNormPrior = Mathf.Clamp(
                    item.descriptiveNormPrior, -1f, 1f);
                item.prestigeSignal = Mathf.Clamp(item.prestigeSignal,
                    -1f, 1f);
                item.spread = Mathf.Clamp(item.spread,
                    CACultureDistributionKernel.VarianceFloor, 1f);
                item.salience = Mathf.Clamp01(item.salience);
                item.normStrength = Mathf.Clamp01(item.normStrength);
                item.visibility = Mathf.Clamp01(item.visibility);
                item.sourceConfidence = Mathf.Clamp01(
                    item.sourceConfidence);
                item.toleranceForDivergence = Mathf.Clamp01(
                    item.toleranceForDivergence);
                if (item.subgroups == null)
                    item.subgroups =
                        new List<CACultureSubgroupDistribution>();
                item.subgroups.RemoveAll(group => group == null
                    || group.subgroupKey.NullOrEmpty());
            }
            List<CACultureQuestionDistribution> unique = values
                .GroupBy(QuestionIdentity, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(item =>
                        item.lastChangedTick)
                    .ThenByDescending(item => item.firstRecordedTick)
                    .First()).ToList();
            values.Clear();
            values.AddRange(unique);
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

        internal static void EnsureIdentity(CACulture culture, string seed)
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

        internal static IEnumerable<CACultureQuestionDistribution>
            EffectiveQuestions(CACulture culture)
        {
            if (culture == null)
                return Enumerable.Empty<CACultureQuestionDistribution>();
            var localKeys = new HashSet<string>((culture.localQuestions
                    ?? new List<CACultureQuestionDistribution>())
                .Where(value => value != null).Select(QuestionIdentity),
                StringComparer.Ordinal);
            return (culture.localQuestions
                    ?? new List<CACultureQuestionDistribution>())
                .Where(value => value != null).Concat((culture
                    .inheritedQuestions
                    ?? new List<CACultureQuestionDistribution>())
                .Where(value => value != null
                    && !localKeys.Contains(QuestionIdentity(value))));
        }

        // Whole-population questions are the authored surface. Scoped rows are
        // constituent evidence used to produce subgroup mixtures; they are not
        // additional questions in summaries or player inspection.
        internal static IEnumerable<CACultureQuestionDistribution>
            PopulationQuestions(CACulture culture)
        {
            return EffectiveQuestions(culture).Where(value => value != null
                && (value.populationScope ?? "*") == "*");
        }

        internal static CACultureQuestionDistribution DistributionFor(
            CACulture culture, string questionKey, string populationScope)
        {
            List<CACultureQuestionDistribution> effective =
                EffectiveQuestions(culture).Where(value => value != null
                    && value.questionKey == questionKey).ToList();
            string scope = populationScope.NullOrEmpty()
                ? "*" : populationScope;
            CACultureQuestionDistribution exact = effective.FirstOrDefault(
                value => (value.populationScope ?? "*") == scope);
            CACultureQuestionDistribution wildcard = effective.FirstOrDefault(
                value => (value.populationScope ?? "*") == "*");
            return exact ?? wildcard;
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
            int meaningCount = PopulationQuestions(culture).Count();
            int practiceCount = culture.inheritedPractices.Count
                + culture.practices.Count;
            string practice = practiceCount == 0 ? ""
                : " · " + practiceCount + " practice"
                    + (practiceCount == 1 ? "" : "s");
            string salient = string.Join(", ", PopulationQuestions(culture)
                .Where(item => item != null)
                .OrderByDescending(item => item.salience)
                .ThenBy(item => item.questionKey)
                .Take(3)
                .Select(item => CACultureQuestionRegistry.Find(
                    item.questionKey)?.Label ?? "recorded question")
                .ToArray());
            string top = salient.NullOrEmpty() ? ""
                : " · most salient: " + salient;
            return identity + " · " + continuity + plurality + change
                + " · " + meaningCount + " cultural question"
                    + (meaningCount == 1 ? "" : "s") + practice
                + top + " · visual tradition: " + visual;
        }

        internal static CACulturalMeaningResolution Resolve(CACulture culture,
            string subjectKey, string populationScope = null)
        {
            if (culture == null)
                return CACulturalMeaningResolver.Resolve(null, subjectKey,
                    populationScope);
            string questionKey = CACultureQuestionRegistry
                .QuestionForSocialSubject(subjectKey);
            if (questionKey.NullOrEmpty())
                return CACulturalMeaningResolver.Resolve(null, subjectKey,
                    populationScope);
            CACultureQuestionDistribution applicable = DistributionFor(culture,
                questionKey, populationScope);
            if (applicable == null)
                return CACulturalMeaningResolver.Resolve(null, subjectKey,
                    populationScope);
            int direction = CACultureQuestionRegistry
                .DirectionForSocialSubject(subjectKey);
            CACultureSubgroupDistribution subgroup = applicable.subgroups?
                .FirstOrDefault(value => value != null
                    && value.subgroupKey == populationScope);
            float mean = Mathf.Clamp(applicable.mean
                + (subgroup?.meanOffset ?? 0f), -1f, 1f) * direction;
            float salience = applicable.salience;
            float confidence = applicable.sourceConfidence;
            float spread = applicable.spread
                * (subgroup?.spreadMultiplier ?? 1f);
            var result = new CACulturalMeaningResolution
            {
                SubjectKey = subjectKey,
                Approval = Mathf.RoundToInt(mean * 100f),
                Normality = Mathf.RoundToInt(((applicable
                    .hasDescriptiveNormPrior
                        ? applicable.descriptiveNormPrior : applicable.mean)
                    * direction + 1f) * 50f),
                Prestige = Mathf.RoundToInt(applicable.prestigeSignal
                    * direction * 100f),
                Salience = Mathf.RoundToInt(salience * 100f),
                Dissonance = Mathf.Clamp01(spread),
                Confidence = Mathf.Clamp01(confidence * salience)
            };
            result.Contributions.Add(new CACulturalMeaningContribution
            {
                PopulationScope = populationScope ?? applicable.populationScope,
                Provenance = applicable.provenance,
                SourceIdentity = applicable.sourceIdentity,
                Weight = Mathf.Max(1,
                    Mathf.RoundToInt(applicable.sourceConfidence * 100f)),
                Approval = Mathf.RoundToInt(mean * 100f),
                Normality = result.Normality,
                Prestige = result.Prestige,
                Salience = result.Salience
            });
            result.Provenance.Add((applicable.provenance ?? "recorded")
                + ":" + (applicable.sourceIdentity ?? "unrecorded"));
            return result;
        }

        // A sociological signature deliberately excludes the optional visual
        // tradition. Visual selection has its own signature so changing art
        // inheritance cannot masquerade as social change.
        internal static string SociologicalSignature(CACulture culture)
        {
            if (culture == null) return "unrecorded";
            string meanings = CACultureDistributionKernel.Fingerprint(
                EffectiveQuestions(culture));
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
            CACultureModel.EnsureIdentity(inherited,
                "player-culture:" + map.uniqueID);
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
                CACultureModel.EnsureIdentity(local,
                    (plan.regionalId ?? plan.candidateId ?? "region")
                        + ":settlement-culture:" + settlement.slot);
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
                SyncConstituentQuestionBaselines(plan, local);
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
            SyncConstituentQuestionBaselines(plan, settlement.localCulture);
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
            string predecessor = CACultureModel.SociologicalSignature(culture);
            var changedQuestions = new HashSet<string>(StringComparer.Ordinal);
            var changedDimensions = new HashSet<string>(StringComparer.Ordinal);
            var changedScopes = new HashSet<string>(StringComparer.Ordinal);
            var evidenceSignatures = new List<string>();
            foreach (IGrouping<string, CASocialGroupPattern> group in (patterns
                    ?? Enumerable.Empty<CASocialGroupPattern>())
                .Where(value => value != null
                    && value.EvidenceCount >= 2
                    && value.ObservedPawnCount >= 2
                    && value.EligiblePopulation > 0
                    && value.Participation >= 0.10f
                    && value.EvidenceStartTick >= 0
                    && value.LastEvidenceTick - value.EvidenceStartTick
                        >= CACulturalMeaningTransitionKernel.HistoricalPeriod)
                .Select(value => new
                {
                    Pattern = value,
                    QuestionKey = !value.QuestionKey.NullOrEmpty()
                        ? value.QuestionKey : CACultureQuestionRegistry
                            .QuestionForSocialSubject(value.SubjectKey)
                })
                .Where(value => !value.QuestionKey.NullOrEmpty())
                .GroupBy(value => value.QuestionKey + "\0"
                    + (value.Pattern.PopulationIdentity ?? "*"),
                    value => value.Pattern, StringComparer.Ordinal))
            {
                string[] identity = group.Key.Split('\0');
                string questionKey = identity[0];
                string population = identity.Length > 1 ? identity[1] : "*";
                int totalWeight = group.Sum(value => Math.Max(1,
                    value.ObservedPawnCount));
                float observedMean = group.Sum(value =>
                        value.WeightedApproval / 100f
                        * (value.QuestionKey.NullOrEmpty()
                            ? CACultureQuestionRegistry
                                .DirectionForSocialSubject(value.SubjectKey)
                            : 1)
                        * Math.Max(1, value.ObservedPawnCount))
                    / Math.Max(1, totalWeight);
                float coverage = group.Sum(value => value.Participation
                        * Math.Max(1, value.ObservedPawnCount))
                    / Math.Max(1, totalWeight);
                float dispersion = group.Sum(value => value.Dispersion
                        * Math.Max(1, value.ObservedPawnCount))
                    / Math.Max(1, totalWeight);
                float alignment = group.Sum(value => value.GroupAlignment
                        * Math.Max(1, value.ObservedPawnCount))
                    / Math.Max(1, totalWeight);
                float observedNorm = Mathf.Clamp(observedMean
                    * (0.5f + coverage * 0.5f), -1f, 1f);
                float observedSalience = Mathf.Clamp01(dispersion + coverage);
                float observedNormStrength = Mathf.Clamp01(
                    alignment * coverage);
                string evidenceSignature = CASocialPatternKernel.StableHash(
                    string.Join("|", group.OrderBy(value => value.SubjectKey,
                        StringComparer.Ordinal).Select(value =>
                        value.SubjectKey + ":" + value.EvidenceSignature)));
                evidenceSignatures.Add(questionKey + ":" + evidenceSignature);

                CACultureQuestionDistribution current = culture.localQuestions
                    .FirstOrDefault(value => value != null
                        && value.questionKey == questionKey
                        && (value.populationScope ?? "*") == population);
                CACultureQuestionDistribution inherited = culture
                    .inheritedQuestions.FirstOrDefault(value => value != null
                        && value.questionKey == questionKey
                        && (value.populationScope ?? "*") == population);
                CACultureQuestionDistribution basis = current ?? inherited;
                if (basis != null && string.Equals(basis.evidenceSignature,
                        evidenceSignature, StringComparison.Ordinal))
                    continue;
                float nextMean = basis == null ? observedMean
                    : Mathf.Lerp(basis.mean, observedMean, 0.25f);
                float nextSpread = basis == null
                    ? Mathf.Max(CACultureDistributionKernel.VarianceFloor,
                        dispersion)
                    : Mathf.Lerp(basis.spread, Mathf.Max(
                        CACultureDistributionKernel.VarianceFloor,
                        dispersion), 0.25f);
                float nextNorm = basis == null ? observedNorm
                    : Mathf.Lerp(basis.hasDescriptiveNormPrior
                        ? basis.descriptiveNormPrior : basis.mean,
                        observedNorm, 0.35f);
                float nextSalience = basis == null ? observedSalience
                    : Mathf.Lerp(basis.salience, observedSalience, 0.25f);
                float nextNormStrength = basis == null
                    ? observedNormStrength : Mathf.Lerp(basis.normStrength,
                        observedNormStrength, 0.25f);
                bool substantive = basis == null
                    || Math.Abs(nextMean - basis.mean) >= 0.01f
                    || Math.Abs(nextSpread - basis.spread) >= 0.01f
                    || Math.Abs(nextNorm - (basis.hasDescriptiveNormPrior
                        ? basis.descriptiveNormPrior : basis.mean)) >= 0.01f
                    || Math.Abs(nextSalience - basis.salience) >= 0.01f
                    || Math.Abs(nextNormStrength - basis.normStrength) >= 0.01f;
                if (!substantive)
                {
                    if (current != null)
                        current.evidenceSignature = evidenceSignature;
                    continue;
                }
                if (current == null)
                {
                    current = inherited?.Copy()
                        ?? new CACultureQuestionDistribution
                        {
                            questionKey = questionKey,
                            populationScope = population,
                            visibility = 0.65f,
                            sourceConfidence = Mathf.Clamp01(coverage),
                            toleranceForDivergence = 0.50f
                        };
                    culture.localQuestions.Add(current);
                }
                if (Math.Abs(nextMean - current.mean) >= 0.01f)
                    changedDimensions.Add("mean");
                if (Math.Abs(nextSpread - current.spread) >= 0.01f)
                    changedDimensions.Add("spread");
                if (Math.Abs(nextNorm - (current.hasDescriptiveNormPrior
                    ? current.descriptiveNormPrior : current.mean)) >= 0.01f)
                    changedDimensions.Add("descriptive norm");
                if (Math.Abs(nextSalience - current.salience) >= 0.01f)
                    changedDimensions.Add("salience");
                if (Math.Abs(nextNormStrength - current.normStrength) >= 0.01f)
                    changedDimensions.Add("norm strength");
                current.mean = Mathf.Clamp(nextMean, -1f, 1f);
                current.spread = Mathf.Clamp(nextSpread,
                    CACultureDistributionKernel.VarianceFloor, 1f);
                current.spreadOverride = true;
                current.hasDescriptiveNormPrior = true;
                current.descriptiveNormPrior = Mathf.Clamp(nextNorm, -1f, 1f);
                current.salience = Mathf.Clamp01(nextSalience);
                current.normStrength = Mathf.Clamp01(nextNormStrength);
                current.sourceConfidence = Mathf.Clamp01(Mathf.Max(
                    current.sourceConfidence, coverage));
                current.provenance = "sustained represented social response";
                current.sourceIdentity = scope ?? "represented population";
                current.evidenceSignature = evidenceSignature;
                if (current.firstRecordedTick < 0)
                    current.firstRecordedTick = tick;
                current.lastChangedTick = tick;
                changedQuestions.Add(questionKey);
                changedScopes.Add(population);
            }
            if (changedQuestions.Count == 0) return false;
            CACultureModel.Normalize(culture);
            string successor = CACultureModel.SociologicalSignature(culture);
            string subjects = string.Join(", ", changedQuestions.OrderBy(
                value => value, StringComparer.Ordinal));
            string dimensions = string.Join(", ", changedDimensions.OrderBy(
                value => value, StringComparer.Ordinal));
            string populationScopes = string.Join(", ", changedScopes.OrderBy(
                value => value, StringComparer.Ordinal));
            Record(culture, "sustained represented social response",
                "Recorded change in " + subjects + ": " + dimensions + ".",
                successor, tick, predecessor, CASocialPatternKernel.StableHash(
                    string.Join("|", evidenceSignatures.OrderBy(value => value,
                        StringComparer.Ordinal))), dimensions, subjects,
                populationScopes);
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
                    && item.sourceDomain != "Culture question evidence"
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

        private static void SyncConstituentQuestionBaselines(
            CARegionalPlan plan, CACulture local)
        {
            if (plan == null || local == null) return;
            const string provenancePrefix = "constituent Culture baseline: ";
            local.inheritedQuestions.RemoveAll(value => value != null
                && (value.provenance ?? "").StartsWith(provenancePrefix,
                    StringComparison.Ordinal));
            foreach (CACultureConstituent constituent in (local.constituents
                    ?? new List<CACultureConstituent>()).Where(value =>
                    value != null && !value.cultureId.NullOrEmpty()))
            {
                CACulture source = plan.factions?.Select(value => value?.culture)
                    .FirstOrDefault(value => value?.id == constituent.cultureId);
                if (source == null) continue;
                foreach (CACultureQuestionDistribution question in
                    CACultureModel.EffectiveQuestions(source).Where(value =>
                        value != null
                        && (value.populationScope ?? "*") == "*"))
                {
                    CACultureQuestionDistribution scoped = question.Copy();
                    scoped.populationScope = constituent.cultureId;
                    scoped.provenance = provenancePrefix
                        + (source.name ?? source.id);
                    scoped.sourceIdentity = source.id;
                    local.inheritedQuestions.Add(scoped);
                }
            }
            CACultureModel.Normalize(local);
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
            if (source?.schemaVersion
                == CAPoliticalBeliefs.CurrentSchemaVersion)
            {
                failure = ValidationFailure(source, allowExactLegacy: false);
                if (!failure.NullOrEmpty()) return false;
                upgraded = source.Copy();
                return true;
            }
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
            failure = ValidationFailure(source, allowExactLegacy: false);
            if (failure.NullOrEmpty())
            {
                upgraded = CAFactionStartingState.CopyAxes(source.ToList());
                return true;
            }
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
            CACultureModel.EnsureIdentity(group.culture, seed);
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

    internal enum CACultureIdeoligionComparison
    {
        Auto,
        Pending,
        Single,
        Mixed,
        None,
        Unresolved
    }

    internal sealed class Dialog_CACultureEditor : Window
    {
        private readonly CACulture culture;
        private readonly string factionLabel;
        private readonly Action changed;
        private readonly CACultureAuthoringBoundary boundary;
        private readonly Ideo ideoligion;
        private readonly CACultureIdeoligionComparison ideoligionComparison;
        private int section;
        private Vector2 scroll;
        private float viewHeight;
        private string expandedQuestionKey;

        private static readonly string[] SpreadLabels =
            { "Narrow", "Limited", "Mixed", "Broad", "Very broad" };
        private static readonly string[] SeparationLabels =
            { "Convergent", "Slight", "Moderate", "Strong", "Segmented" };

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(980f, UI.screenWidth - 48f),
            Mathf.Min(580f, UI.screenHeight - 48f));

        internal Dialog_CACultureEditor(CACulture culture,
            string factionLabel, Action changed,
            CACultureAuthoringBoundary boundary =
                CACultureAuthoringBoundary.Inherited,
            Ideo ideoligion = null,
            CACultureIdeoligionComparison ideoligionComparison =
                CACultureIdeoligionComparison.Auto)
        {
            this.culture = culture ?? new CACulture();
            this.factionLabel = factionLabel;
            this.changed = changed;
            this.boundary = boundary;
            this.ideoligion = ideoligion;
            this.ideoligionComparison = ideoligionComparison
                == CACultureIdeoligionComparison.Auto
                    ? ideoligion == null
                        ? CACultureIdeoligionComparison.Pending
                        : CACultureIdeoligionComparison.Single
                    : ideoligionComparison;
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
            string[] tabs = { "Overview", "Questions",
                "Practice history",
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
            else if (section == 1) DrawQuestions(ref rowY, view.width);
            else if (section == 2) DrawPracticeHistory(ref rowY, view.width);
            else DrawVisual(ref rowY, view.width);
            viewHeight = rowY + 12f;
            Widgets.EndScrollView();
        }

        private float DrawActions(Rect inRect, float y)
        {
            var labels = new List<string>
            {
                "Historical and social presets...", "Randomize Culture"
            };
            var actions = new List<Action>
            {
                OpenBuiltInPresets, RandomizeCulture
            };
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
            DrawFact(ref y, width, "Inherited questions",
                culture.inheritedQuestions.Count(value => value != null
                    && (value.populationScope ?? "*") == "*").ToString());
            DrawFact(ref y, width, "Current local questions",
                culture.localQuestions.Count(value => value != null
                    && (value.populationScope ?? "*") == "*").ToString());
            DrawFact(ref y, width, "Practice evidence",
                (culture.inheritedPractices.Count
                    + culture.practices.Count).ToString());
            DrawFact(ref y, width, "Global diversity",
                SpreadLabels[Mathf.Clamp(culture.withinGroupSpread, 0, 4)]);
            DrawFact(ref y, width, "Subgroup separation",
                HasRepresentedSubgroupDifferences
                    ? SeparationLabels[Mathf.Clamp(
                        culture.subgroupSeparation, 0, 4)]
                    : "No represented subgroup differences");
            CACultureQuestionDistribution[] salient = CACultureModel
                .PopulationQuestions(culture)
                .OrderByDescending(item => item.salience).Take(3).ToArray();
            DrawFact(ref y, width, "Most salient",
                salient.Length == 0 ? "None composed" : string.Join(", ",
                    salient.Select(item => CACultureQuestionRegistry.Find(
                            item.questionKey)?.Label ?? "Recorded question")));
            int polarized = CACultureModel.PopulationQuestions(culture)
                .Count(item => item != null && (item.spread >= 0.55f
                    || (item.subgroups?.Any(group => group != null
                        && Math.Abs(group.meanOffset) >= 0.20f) == true)));
            DrawFact(ref y, width, "Broad or divided questions",
                polarized == 0 ? "None recorded" : polarized.ToString());
            CultureDef native = CACultureModel.NativeDef(culture);
            DrawFact(ref y, width, "Visual tradition",
                native?.LabelCap.ToString() ?? "None");
            y += 6f;
            bool hasCausalFacts = culture.inheritedQuestions.Count
                    + culture.localQuestions.Count
                    + culture.inheritedPractices.Count
                    + culture.practices.Count > 0;
            if (hasCausalFacts)
            {
                if (Widgets.ButtonText(new Rect(0f, y,
                        Mathf.Min(260f, width), 32f),
                        "Inspect causal effects..."))
                    Find.WindowStack.Add(
                        new Dialog_CACultureCausalInspector(culture,
                            factionLabel, boundary));
                y += 42f;
            }
        }

        private void DrawQuestions(ref float y, float width)
        {
            DrawExplanation(ref y, width,
                "Each row sets one population distribution. The named "
                + "position is its center. Global diversity sets the default "
                + "variation among people; a question-specific spread is "
                + "available under More.");
            string comparison = IdeoligionComparisonWords();
            if (!comparison.NullOrEmpty())
                DrawExplanation(ref y, width, comparison);
            DrawPopulationDistributionControls(ref y, width);
            if (boundary == CACultureAuthoringBoundary.EstablishedLocal)
                DrawExplanation(ref y, width,
                    "Inherited population spread remains unchanged here. "
                    + "Choose a position to create a local question, then "
                    + "adjust that local distribution directly.");
            foreach (IGrouping<CACultureQuestionLayer, CACultureQuestionDef>
                category in CACultureQuestionRegistry.All.GroupBy(value =>
                    value.Layer))
            {
                y += 8f;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, y, width, 30f),
                    CategoryLabel(category.Key));
                Text.Font = GameFont.Small;
                y += 36f;
                foreach (CACultureQuestionDef definition in category)
                    DrawQuestion(ref y, width, definition);
            }
        }

        private void DrawPopulationDistributionControls(ref float y,
            float width)
        {
            Text.Font = GameFont.Small;
            if (boundary == CACultureAuthoringBoundary.Inherited)
            {
                Widgets.Label(new Rect(0f, y + 4f, 190f, 28f),
                    "Global diversity");
                float spreadHeight = CACreationUI.DrawSegmentRows(new Rect(
                    190f, y, width - 190f, 28f), SpreadLabels,
                    Mathf.Clamp(culture.withinGroupSpread, 0, 4), value =>
                    {
                        culture.withinGroupSpread = value;
                        culture.authoredMask |= CACulture.DiversityField;
                        foreach (CACultureQuestionDistribution question in
                            EditableQuestions.Where(item => item != null
                                && !item.spreadOverride))
                            question.spread = SpreadValue(value);
                        CACultureModel.Normalize(culture);
                        changed?.Invoke();
                    }, 92f);
                y += spreadHeight + 8f;
            }
            y += 6f;
        }

        private static string CategoryLabel(CACultureQuestionLayer layer)
        {
            return layer switch
            {
                CACultureQuestionLayer.RelationshipsFamilySexuality =>
                    "Relationships, family, and sexuality",
                CACultureQuestionLayer.GenderSocialAuthority =>
                    "Gender and social authority",
                CACultureQuestionLayer.StatusHierarchy =>
                    "Status and hierarchy",
                CACultureQuestionLayer.MembershipOutsiders =>
                    "Membership and outsiders",
                CACultureQuestionLayer.PublicAuthoritySocialOrder =>
                    "Public authority and social order",
                CACultureQuestionLayer.PropertyLaborProvision =>
                    "Property, labor, and provision",
                CACultureQuestionLayer.ViolenceCaptivityPunishment =>
                    "Violence, captivity, and punishment",
                CACultureQuestionLayer.KnowledgeTradition =>
                    "Knowledge and tradition",
                _ => "Culture"
            };
        }

        private bool HasRepresentedSubgroupDifferences =>
            culture.constituents.Count(value => value != null
                && value.share > 0) > 1
            && CACultureModel.EffectiveQuestions(culture).Any(value =>
                value != null && (value.populationScope ?? "*") != "*");

        private void DrawQuestion(ref float y, float width,
            CACultureQuestionDef definition)
        {
            CACultureQuestionDistribution editable = EditableQuestions
                .FirstOrDefault(value => value != null
                    && value.questionKey == definition.Key
                    && (value.populationScope ?? "*") == "*");
            CACultureQuestionDistribution question = editable;
            bool inheritedFallback = false;
            if (question == null
                && boundary == CACultureAuthoringBoundary.EstablishedLocal)
            {
                question = culture.inheritedQuestions.FirstOrDefault(value =>
                    value != null && value.questionKey == definition.Key
                    && (value.populationScope ?? "*") == "*");
                inheritedFallback = question != null;
            }
            bool expanded = expandedQuestionKey == definition.Key;
            float anchorWidth = Mathf.Max(200f, width - 24f);
            float anchorHeight = AnchorRowHeight(anchorWidth,
                definition.Anchors);
            float helpHeight = Text.CalcHeight(definition.Question,
                width - 24f);
            float directHeight = question == null ? 26f : Text.CalcHeight(
                QuestionSourceWords(inheritedFallback, definition, question),
                width - 24f);
            float boxHeight = 64f + helpHeight + anchorHeight + directHeight;
            if (expanded && question != null)
                boxHeight += inheritedFallback
                    ? InheritedFallbackHeight(width - 24f)
                    : AdvancedQuestionHeight(question, width - 24f);
            Rect box = new Rect(0f, y, width, boxHeight);
            Widgets.DrawMenuSection(box);
            float at = y + 10f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(12f, at, width - 272f, 28f),
                definition.Label);
            string badge = question == null ? "Not recorded"
                : CACultureDistributionKernel.Summarize(question,
                    culture.id ?? definition.Key).Anchor;
            GUI.color = question == null ? ColoredText.SubtleGrayColor
                : inheritedFallback
                    || boundary == CACultureAuthoringBoundary.Inherited
                        ? CACreationUI.Inherited : CACreationUI.Authored;
            Rect badgeRect = new Rect(width - 256f, at, 180f, 28f);
            string fittedBadge = FitLabel(badge, badgeRect.width - 6f);
            Widgets.Label(badgeRect, fittedBadge);
            if (fittedBadge != badge)
                TooltipHandler.TipRegion(badgeRect, badge);
            GUI.color = Color.white;
            if (question != null && Widgets.ButtonText(new Rect(width - 72f,
                    at, 60f, 28f), expanded ? "Less" : "More"))
                expandedQuestionKey = expanded ? null : definition.Key;
            at += 30f;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(12f, at, width - 24f, helpHeight),
                definition.Question);
            GUI.color = Color.white;
            at += helpHeight + 5f;
            int selected = question == null ? -1
                : CACultureDistributionKernel.NearestAnchor(question.mean);
            float rowHeight = CACreationUI.DrawSegmentRows(new Rect(12f, at,
                anchorWidth, 28f), definition.Anchors, selected, index =>
                {
                    CACultureQuestionDistribution target = editable
                        ?? AddQuestion(definition, question);
                    target.mean = (float)definition.AnchorCenters[index];
                    culture.authoredMask |= CACulture.QuestionStateField;
                    target.lastChangedTick = -1;
                    target.provenance = boundary
                        == CACultureAuthoringBoundary.EstablishedLocal
                            ? "authored established local distribution"
                            : "authored inherited distribution";
                    CACultureModel.Normalize(culture);
                    changed?.Invoke();
                }, 184f);
            at += rowHeight + 5f;
            if (question != null)
            {
                CACultureDistributionSummary summary =
                    CACultureDistributionKernel.Summarize(question,
                        culture.id ?? definition.Key);
                string direct = QuestionSourceWords(inheritedFallback,
                    definition, question);
                float directTextHeight = Text.CalcHeight(direct, width - 24f);
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(12f, at, width - 24f,
                    directTextHeight), direct);
                GUI.color = Color.white;
                at += directTextHeight + 2f;
                if (expanded && inheritedFallback)
                {
                    DrawExplanation(ref at, width - 24f,
                        "This is the inherited distribution. Create a local "
                        + "question before changing its advanced values.");
                    if (Widgets.ButtonText(new Rect(0f, at,
                            Mathf.Min(260f, width), 28f),
                            "Create local question"))
                    {
                        AddQuestion(definition, question);
                        changed?.Invoke();
                    }
                    at += 38f;
                }
                else if (expanded)
                    DrawQuestionAdvanced(ref at, width, definition, question,
                        summary);
            }
            else
            {
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(12f, at, width - 24f, 26f),
                    "No position has been recorded for this population.");
                GUI.color = Color.white;
            }
            y += boxHeight + 10f;
        }

        private CACultureQuestionDistribution AddQuestion(
            CACultureQuestionDef definition,
            CACultureQuestionDistribution inherited = null)
        {
            CACultureQuestionDistribution value = inherited?.Copy()
                ?? CACultureDistributionKernel.NewQuestion(definition);
            if (inherited == null) value.mean = 0f;
            if (inherited == null)
                value.spread = SpreadValue(culture.withinGroupSpread);
            value.provenance = boundary
                == CACultureAuthoringBoundary.EstablishedLocal
                    ? "authored established local distribution"
                    : "authored inherited distribution";
            value.sourceIdentity = boundary
                == CACultureAuthoringBoundary.EstablishedLocal
                    ? culture.localityKey ?? factionLabel
                    : culture.parentId ?? factionLabel;
            value.evidenceSignature = null;
            EditableQuestions.Add(value);
            culture.authoredMask |= CACulture.QuestionStateField;
            CACultureModel.Normalize(culture);
            return value;
        }

        private void DrawQuestionAdvanced(ref float y, float width,
            CACultureQuestionDef definition,
            CACultureQuestionDistribution question,
            CACultureDistributionSummary summary)
        {
            y += 4f;
            float mean = FloatSlider(ref y, width, "Position",
                question.mean, -1f, 1f);
            float spread = FloatSlider(ref y, width, "Spread",
                question.spread, CACultureDistributionKernel.VarianceFloor,
                1f);
            float salience = FloatSlider(ref y, width, "Salience",
                question.salience, 0f, 1f);
            float normStrength = FloatSlider(ref y, width,
                "Norm pressure", question.normStrength, 0f, 1f);
            float tolerance = FloatSlider(ref y, width,
                "Divergence tolerated", question.toleranceForDivergence,
                0f, 1f);
            float visibility = FloatSlider(ref y, width,
                "Observation likelihood",
                question.visibility, 0f, 1f);
            float confidence = FloatSlider(ref y, width,
                "Inherited prior confidence", question.sourceConfidence,
                0f, 1f);
            if (!Mathf.Approximately(mean, question.mean)
                || !Mathf.Approximately(spread, question.spread)
                || !Mathf.Approximately(salience, question.salience)
                || !Mathf.Approximately(normStrength,
                    question.normStrength)
                || !Mathf.Approximately(tolerance,
                    question.toleranceForDivergence)
                || !Mathf.Approximately(visibility, question.visibility)
                || !Mathf.Approximately(confidence,
                    question.sourceConfidence))
            {
                question.mean = mean;
                if (!Mathf.Approximately(spread, question.spread))
                    question.spreadOverride = true;
                question.spread = spread;
                question.salience = salience;
                question.normStrength = normStrength;
                question.toleranceForDivergence = tolerance;
                question.visibility = visibility;
                question.sourceConfidence = confidence;
                culture.authoredMask |= CACulture.QuestionStateField;
                question.lastChangedTick = -1;
                CACultureModel.Normalize(culture);
                changed?.Invoke();
            }
            string preview = QuestionPreview(question, summary);
            DrawExplanation(ref y, width - 24f, preview);
            if (question.spreadOverride && Widgets.ButtonText(new Rect(0f, y,
                    Mathf.Min(260f, width), 28f),
                    "Use population spread"))
            {
                question.spreadOverride = false;
                question.spread = SpreadValue(culture.withinGroupSpread);
                culture.authoredMask |= CACulture.QuestionStateField;
                changed?.Invoke();
            }
            if (question.spreadOverride) y += 34f;
            bool hasInheritedPredecessor = culture.inheritedQuestions.Any(
                value => value != null
                    && value.questionKey == question.questionKey
                    && (value.populationScope ?? "*")
                        == (question.populationScope ?? "*"));
            if (boundary == CACultureAuthoringBoundary.EstablishedLocal
                && hasInheritedPredecessor
                && Widgets.ButtonText(new Rect(0f, y,
                    Mathf.Min(260f, width), 28f), "Use inherited question"))
            {
                EditableQuestions.Remove(question);
                culture.authoredMask |= CACulture.QuestionStateField;
                expandedQuestionKey = null;
                CACultureModel.Normalize(culture);
                changed?.Invoke();
                return;
            }
            if (boundary == CACultureAuthoringBoundary.EstablishedLocal
                && hasInheritedPredecessor)
                y += 34f;
            foreach (CACultureSubgroupDistribution subgroup in
                question.subgroups.Where(value => value != null))
            {
                float offset = FloatSlider(ref y, width,
                    (subgroup.label ?? subgroup.subgroupKey) + " offset",
                    subgroup.meanOffset, -1f, 1f);
                if (!Mathf.Approximately(offset, subgroup.meanOffset))
                {
                    subgroup.meanOffset = offset;
                    subgroup.inherited = false;
                    culture.authoredMask |= CACulture.QuestionStateField;
                    changed?.Invoke();
                }
            }
            bool customSubgroups = question.subgroups.Any(value =>
                value != null && !value.inherited);
            if (customSubgroups && Widgets.ButtonText(new Rect(0f, y,
                    Mathf.Min(300f, width), 28f),
                    "Use represented subgroup differences"))
            {
                foreach (CACultureSubgroupDistribution subgroup in
                    question.subgroups.Where(value => value != null))
                    subgroup.inherited = true;
                culture.authoredMask |= CACulture.QuestionStateField;
                CACultureModel.Normalize(culture);
                changed?.Invoke();
            }
            if (customSubgroups) y += 34f;
        }

        private float AdvancedQuestionHeight(
            CACultureQuestionDistribution question, float width)
        {
            int subgroups = question?.subgroups?.Count ?? 0;
            CACultureDistributionSummary summary = question == null
                ? default : CACultureDistributionKernel.Summarize(question,
                    culture.id ?? question.questionKey);
            string preview = question == null ? "No question selected."
                : QuestionPreview(question, summary);
            bool hasInheritedPredecessor = question != null && culture
                .inheritedQuestions.Any(value => value != null
                    && value.questionKey == question.questionKey
                    && (value.populationScope ?? "*")
                        == (question.populationScope ?? "*"));
            int extraButtons = (boundary
                    == CACultureAuthoringBoundary.EstablishedLocal
                    && hasInheritedPredecessor ? 1 : 0)
                + (question?.subgroups?.Any(value => value != null
                    && !value.inherited) == true ? 1 : 0);
            return 7 * 36f + Text.CalcHeight(preview,
                    Mathf.Max(1f, width - 24f)) + 21f
                + (question?.spreadOverride == true ? 34f : 0f)
                + subgroups * 36f + extraButtons * 34f;
        }

        private static float InheritedFallbackHeight(float width)
        {
            const string message = "This is the inherited distribution. "
                + "Create a local question before changing its advanced values.";
            return Text.CalcHeight(message, width) + 50f;
        }

        private string CultureSourceWords(bool inheritedFallback)
        {
            return inheritedFallback
                || boundary == CACultureAuthoringBoundary.Inherited
                    ? "Inherited population position"
                    : "This settlement's current position";
        }

        private void DrawPracticeHistory(ref float y, float width)
        {
            DrawExplanation(ref y, width,
                "Practices are observed conduct. They may become evidence "
                + "about cultural change, but they are not Culture settings.");
            List<CACulturePractice> values = culture.inheritedPractices
                .Concat(culture.practices).Where(value => value != null)
                .OrderByDescending(value => value.strength).ToList();
            if (values.Count == 0)
            {
                DrawExplanation(ref y, width,
                    "No repeated practice is recorded at this boundary.");
                return;
            }
            foreach (CACulturePractice practice in values)
            {
                CACulturalPracticeDef definition =
                    CACulturalPracticeRegistry.Find(practice.practiceKey);
                DrawFact(ref y, width,
                    definition?.Label ?? "Recorded practice",
                    (definition?.Summary ?? practice.summary)
                    + " Strength " + practice.strength
                    + ". Evidence comes from this population's recorded "
                    + "history.");
            }
        }

        private string IdeoligionBadge(CACultureQuestionDef definition,
            CACultureQuestionDistribution question)
        {
            if (ideoligionComparison
                    != CACultureIdeoligionComparison.Single
                || ideoligion == null) return null;
            if (definition.IdeoligionAdapters == null
                || definition.IdeoligionAdapters.Length == 0)
                return "No related Ideoligion precept";
            float pressure = CACultureIdeoligionAdapter.Pressure(
                ideoligion, definition.Key, out string source);
            if (Mathf.Abs(pressure) < 0.05f)
                return "No directional Ideoligion precept";
            string label = IdeoligionSourceLabel(source);
            if (Mathf.Abs(question.mean) < 0.15f)
                return "Population position is neutral beside " + label;
            bool aligned = Math.Sign(pressure) == Math.Sign(question.mean);
            return aligned ? "Agrees with " + label
                : "Differs from " + label;
        }

        private string QuestionSourceWords(bool inheritedFallback,
            CACultureQuestionDef definition,
            CACultureQuestionDistribution question)
        {
            string source = CultureSourceWords(inheritedFallback) + ".";
            string comparison = IdeoligionBadge(definition, question);
            return comparison.NullOrEmpty() ? source
                : source + " " + comparison + ".";
        }

        private string IdeoligionComparisonWords()
        {
            switch (ideoligionComparison)
            {
                case CACultureIdeoligionComparison.Mixed:
                    return "This population does not share a single "
                        + "Ideoligion. No single doctrinal comparison applies.";
                case CACultureIdeoligionComparison.Pending:
                    return "Ideoligion has not been selected. No doctrinal "
                        + "comparison is shown.";
                case CACultureIdeoligionComparison.None:
                    return "No population-wide Ideoligion is represented. "
                        + "Culture remains independently authored.";
                case CACultureIdeoligionComparison.Unresolved:
                    return "At least one represented Ideoligion cannot be "
                        + "resolved. No doctrinal comparison is shown.";
                default:
                    return null;
            }
        }

        private static string ProvenanceWords(string provenance)
        {
            if (provenance.NullOrEmpty()) return "Not recorded";
            if (provenance.IndexOf("authored",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Authored for this population";
            if (provenance.IndexOf("transition",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || provenance.IndexOf("changed",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || provenance.IndexOf("sustained represented social response",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Changed during play";
            if (provenance.IndexOf("inherit",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || provenance.IndexOf("adapter",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || provenance.IndexOf("migration",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Inherited from the represented population";
            return "Recorded for this population";
        }

        private static string IdeoligionSourceLabel(string source)
        {
            string key = source?.Replace("precept:", "")
                .Replace("meme:", "")
                .Replace("precepts:", "");
            if (key.NullOrEmpty()) return "Ideoligion";
            string[] parts = key.Split(new[] { '+' },
                StringSplitOptions.None);
            var labels = new List<string>();
            foreach (string part in parts)
            {
                PreceptDef precept = DefDatabase<PreceptDef>.GetNamedSilentFail(
                    part);
                MemeDef meme = DefDatabase<MemeDef>.GetNamedSilentFail(part);
                labels.Add(precept?.LabelCap.ToString()
                    ?? meme?.LabelCap.ToString()
                    ?? "a related Ideoligion rule");
            }
            return string.Join(" and ", labels.Distinct());
        }

        private static float FloatSlider(ref float y, float width,
            string label, float value, float minimum, float maximum)
        {
            const float labelWidth = 190f;
            Rect labelRect = new Rect(0f, y + 3f, labelWidth, 28f);
            string fullLabel = label + " " + value.ToString("0.00");
            string fitted = FitLabel(fullLabel, labelWidth - 8f);
            Widgets.Label(labelRect, fitted);
            if (fitted != fullLabel)
                TooltipHandler.TipRegion(labelRect, fullLabel);
            float result = Widgets.HorizontalSlider(new Rect(labelWidth, y,
                width - labelWidth, 26f), value, minimum, maximum, false);
            y += 36f;
            return result;
        }

        private static string FitLabel(string value, float width)
        {
            if (value.NullOrEmpty() || Text.CalcSize(value).x <= width)
                return value;
            const string suffix = "...";
            int length = value.Length;
            while (length > 1 && Text.CalcSize(
                    value.Substring(0, length) + suffix).x > width)
                length--;
            return value.Substring(0, length).TrimEnd() + suffix;
        }

        private static string QuestionPreview(
            CACultureQuestionDistribution question,
            CACultureDistributionSummary summary)
        {
            return "Position " + Signed(summary.Median)
                + "; variation " + summary.Spread.ToString("0.00")
                + "; division " + Percent(summary.Polarization)
                + ". Source: " + ProvenanceWords(question.provenance) + ".";
        }

        private static float SpreadValue(int setting)
        {
            return new[] { 0.10f, 0.18f, 0.28f, 0.42f, 0.60f }
                [Mathf.Clamp(setting, 0, 4)];
        }

        private static string Signed(float value)
        {
            return value.ToString(value >= 0f ? "+0.00" : "0.00");
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        private static string SpreadWords(float value) => value >= 0.55f
            ? "broad" : value >= 0.30f ? "mixed" : "limited";

        private static string SalienceWords(float value) => value >= 0.70f
            ? "highly important" : value >= 0.35f ? "noticeable"
                : "rarely emphasized";

        private static string ToleranceWords(float value) => value >= 0.70f
            ? "widely tolerated" : value >= 0.35f ? "sometimes tolerated"
                : "rarely tolerated";

        private static string PressureWords(float value) => value >= 0.70f
            ? "strong" : value >= 0.35f ? "moderate" : "weak";

        private static string LegacyEvidenceLabel(CACultureLegacyEvidence value)
        {
            string question = CACultureQuestionRegistry.QuestionForSocialSubject(
                value?.sourceKey);
            return CACultureQuestionRegistry.Find(question)?.Label
                ?? "unmapped historical evidence";
        }

        private static string LegacyEvidenceDisposition(
            CACultureLegacyEvidence value)
        {
            return (value?.disposition ?? "No current question mapping")
                .StartsWith("preserved", StringComparison.OrdinalIgnoreCase)
                    ? "It has no exact current question"
                    : "Its compatible parts informed the current question";
        }

        private List<CACultureQuestionDistribution> EditableQuestions =>
            boundary == CACultureAuthoringBoundary.EstablishedLocal
                ? culture.localQuestions : culture.inheritedQuestions;

        private static float AnchorRowHeight(float width, string[] labels)
        {
            const float gap = 4f;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + gap)
                / 188f), 1, labels.Length);
            int rows = (labels.Length + columns - 1) / columns;
            return rows * 28f + (rows - 1) * gap;
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

        private void OpenBuiltInPresets()
        {
            var options = CACulturePresetLibrary.All.Select(preset =>
                new FloatMenuOption(preset.Label + " ("
                    + preset.ApproximatePeriod + ")\n" + preset.Summary,
                    delegate
                    {
                        CACulturePresetLibrary.Apply(culture, preset,
                            culture.id ?? factionLabel ?? "authored-culture");
                        expandedQuestionKey = null;
                        changed?.Invoke();
                    })).ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void RandomizeCulture()
        {
            string identity = culture.id ?? factionLabel
                ?? "authored-culture";
            CACultureAuthoringKernel.Randomize(culture,
                CACultureAuthoringKernel.NextRandomizationSeed(culture,
                    identity), identity);
            expandedQuestionKey = null;
            changed?.Invoke();
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
                    // choice. Identity setup leaves substantive state unset.
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

    internal sealed class Dialog_CACultureCausalInspector : Window
    {
        private readonly CACulture culture;
        private readonly string ownerLabel;
        private readonly CACultureAuthoringBoundary boundary;
        private Vector2 scroll;
        private float viewHeight;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(860f, UI.screenWidth - 48f),
            Mathf.Min(620f, UI.screenHeight - 48f));

        internal Dialog_CACultureCausalInspector(CACulture culture,
            string ownerLabel, CACultureAuthoringBoundary boundary)
        {
            this.culture = culture ?? new CACulture();
            this.ownerLabel = ownerLabel;
            this.boundary = boundary;
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
            const string introduction = "Population positions and their "
                + "registered effects. Ideoligion, political beliefs, and "
                + "instituted rules remain separate.";
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
            DrawFact(ref y, view.width, "Authored population",
                (ownerLabel.NullOrEmpty() ? "This population" : ownerLabel)
                + (boundary == CACultureAuthoringBoundary.EstablishedLocal
                    ? " is an established local population."
                    : " carries this inherited Culture.")
                + " Registered consumers and recorded Culture history are "
                + "listed below.");
            foreach (CACultureQuestionDistribution distribution in
                CACultureModel.PopulationQuestions(culture)
                .OrderByDescending(item => item.salience)
                .ThenBy(item => item.questionKey, StringComparer.Ordinal))
            {
                CACultureQuestionDef definition =
                    CACultureQuestionRegistry.Find(distribution.questionKey);
                CACultureDistributionSummary summary =
                    CACultureDistributionKernel.Summarize(distribution,
                        culture.id ?? distribution.questionKey);
                string consumers = string.Join(", ", new[]
                    {
                        definition?.BehaviorConsumers,
                        definition?.PoliticalConsumers,
                        definition?.InstitutionConsumers,
                        definition?.KnowledgeConsumers
                    }.Where(items => items != null)
                    .SelectMany(items => items).Distinct());
                string detail = "The population centers on "
                    + (definition == null ? Signed(distribution.mean)
                        : definition.Anchors[CACultureDistributionKernel
                            .NearestAnchor(distribution.mean)])
                    + ". Variation is " + SpreadWords(summary.Spread)
                    + "; the question is "
                    + SalienceWords(distribution.salience)
                    + "; disagreement is "
                    + ToleranceWords(distribution.toleranceForDivergence)
                    + ". Public pressure is "
                    + PressureWords(distribution.normStrength)
                    + ".\nAffects: "
                    + (consumers.NullOrEmpty() ? "none registered"
                        : consumers) + ".\nHistorical change: "
                    + HistoricalDrift(culture, distribution.questionKey)
                    + ".\nSource: "
                    + ProvenanceWords(distribution.provenance) + ".";
                DrawFact(ref y, view.width,
                    definition?.Label ?? "Recorded Culture question", detail);
            }
            foreach (CACulturePractice practice in culture.inheritedPractices
                .Concat(culture.practices)
                .OrderByDescending(item => item.strength))
            {
                CACulturalPracticeDef practiceDef =
                    CACulturalPracticeRegistry.Find(practice.practiceKey);
                DrawFact(ref y, view.width,
                    practiceDef?.Label ?? "Recorded practice",
                    "Strength " + practice.strength + ". Recorded activity: "
                    + (practiceDef?.Activity ?? "unavailable")
                    + ". This population's history supplies the evidence; "
                    + "settlement programs and Culture history use it.");
            }
            foreach (CACultureLegacyEvidence evidence in culture
                .legacyEvidence.Where(item => item != null))
                DrawFact(ref y, view.width,
                    "Earlier record: " + LegacyEvidenceLabel(evidence),
                    "Preserved for this Culture's history. "
                        + LegacyEvidenceDisposition(evidence) + ".");
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

        private static string Signed(float value)
        {
            return value.ToString(value >= 0f ? "+0.00" : "0.00");
        }

        private static string HistoricalDrift(CACulture culture,
            string questionKey)
        {
            CACultureTransition latest = culture?.transitions?
                .Where(value => value != null
                    && !value.changedSubjectKeys.NullOrEmpty()
                    && value.changedSubjectKeys.Split(new[] { ',', '|' },
                            StringSplitOptions.RemoveEmptyEntries)
                        .Any(key => string.Equals(key.Trim(), questionKey,
                            StringComparison.Ordinal)))
                .OrderByDescending(value => value.sequence).FirstOrDefault();
            if (latest == null) return "none recorded";
            return (latest.summary ?? latest.cause ?? "represented change")
                + (latest.tick >= 0 ? " on "
                    + GenDate.DateFullStringAt(
                        GenDate.TickGameToAbs(latest.tick), Vector2.zero)
                    : "");
        }

        private static string ProvenanceWords(string provenance)
        {
            if (provenance.NullOrEmpty()) return "not recorded";
            if (provenance.IndexOf("authored",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "authored for this population";
            if (provenance.IndexOf("transition",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || provenance.IndexOf("changed",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || provenance.IndexOf("sustained represented social response",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "changed during play";
            if (provenance.IndexOf("inherit",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || provenance.IndexOf("adapter",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || provenance.IndexOf("migration",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "inherited from the represented population";
            return "recorded for this population";
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        private static string SpreadWords(float value) => value >= 0.55f
            ? "broad" : value >= 0.30f ? "mixed" : "limited";

        private static string SalienceWords(float value) => value >= 0.70f
            ? "highly important" : value >= 0.35f ? "noticeable"
                : "rarely emphasized";

        private static string ToleranceWords(float value) => value >= 0.70f
            ? "widely tolerated" : value >= 0.35f ? "sometimes tolerated"
                : "rarely tolerated";

        private static string PressureWords(float value) => value >= 0.70f
            ? "strong" : value >= 0.35f ? "moderate" : "weak";

        private static string LegacyEvidenceLabel(CACultureLegacyEvidence value)
        {
            string question = CACultureQuestionRegistry.QuestionForSocialSubject(
                value?.sourceKey);
            return CACultureQuestionRegistry.Find(question)?.Label
                ?? "unmapped historical evidence";
        }

        private static string LegacyEvidenceDisposition(
            CACultureLegacyEvidence value)
        {
            return (value?.disposition ?? "No current question mapping")
                .StartsWith("preserved", StringComparison.OrdinalIgnoreCase)
                    ? "It has no exact current question"
                    : "Its compatible parts informed the current question";
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
