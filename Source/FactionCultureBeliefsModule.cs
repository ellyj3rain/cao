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
        public const int CurrentSchemaVersion = 11;
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
            // The campaign preflight requires both spread fields exactly
            // once per culture; a default-valued spread (2 is one of the
            // five authorable values) must still serialize or the emitted
            // save fails its own completeness seal.
            Scribe_Values.Look(ref withinGroupSpread,
                "withinGroupSpread", 2, forceSave: true);
            Scribe_Values.Look(ref subgroupSeparation,
                "subgroupSeparation", 2, forceSave: true);
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
            CACultureModel.SynchronizeOwnIdentityLabel(this);
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
                case NameField:
                    name = value;
                    CACultureModel.SynchronizeOwnIdentityLabel(this);
                    break;
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

    // Political questions record what a population considers proper. The
    // generated identity and account are consequences of these variables.
    // Instituted rules and observed practice remain separate runtime facts.
    public sealed class CAPoliticalBeliefs : IExposable
    {
        public const int CurrentSchemaVersion = 10;
        public int schemaVersion = CurrentSchemaVersion;
        public string id;
        public string name;
        public bool nameAuthored;
        public int nameRoll;
        public int generationRoll;
        public List<CAPoliticalQuestionState> questions =
            new List<CAPoliticalQuestionState>();
        public List<CAAxisEntry> positions = new List<CAAxisEntry>();
        public List<CAPoliticalDerivationReceipt> derivationReceipts =
            new List<CAPoliticalDerivationReceipt>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref id, "id");
            // The campaign preflight requires the four name/roll fields
            // exactly once per political order; their idle defaults (an
            // unauthored generated name, roll zero) are ordinary states
            // that must still serialize.
            Scribe_Values.Look(ref name, "name", null, forceSave: true);
            Scribe_Values.Look(ref nameAuthored, "nameAuthored", false,
                forceSave: true);
            Scribe_Values.Look(ref nameRoll, "nameRoll", 0,
                forceSave: true);
            Scribe_Values.Look(ref generationRoll, "generationRoll", 0,
                forceSave: true);
            Scribe_Collections.Look(ref questions, "questions",
                LookMode.Deep);
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
                name = name,
                nameAuthored = nameAuthored,
                nameRoll = nameRoll,
                generationRoll = generationRoll,
                questions = (questions
                        ?? new List<CAPoliticalQuestionState>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
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
            name = source.name;
            nameAuthored = source.nameAuthored;
            nameRoll = source.nameRoll;
            generationRoll = source.generationRoll;
            questions = (source.questions
                    ?? new List<CAPoliticalQuestionState>())
                .Where(item => item != null).Select(item => item.Copy())
                .ToList();
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

        // Preset matching is value equality over the inherited Culture
        // component, not a loose comparison of a few headline axes. Local
        // history remains outside the reusable component by design.
        internal static bool MatchesInheritedTemplate(CACulture target,
            CACulture template)
        {
            if (target == null || template == null
                || !string.Equals(target.name, template.name,
                    StringComparison.Ordinal)
                || !string.Equals(target.sourceCultureDefName,
                    template.sourceCultureDefName, StringComparison.Ordinal)
                || target.withinGroupSpread != template.withinGroupSpread
                || target.subgroupSeparation != template.subgroupSeparation
                || target.questionRegistryVersion
                    != template.questionRegistryVersion)
                return false;
            List<CACultureQuestionDistribution> actual =
                target.inheritedQuestions
                    ?? new List<CACultureQuestionDistribution>();
            List<CACultureQuestionDistribution> expected =
                template.inheritedQuestions
                    ?? new List<CACultureQuestionDistribution>();
            if (actual.Count != expected.Count) return false;
            return string.Equals(
                CACultureDistributionKernel.Fingerprint(actual),
                CACultureDistributionKernel.Fingerprint(expected),
                StringComparison.Ordinal);
        }

        // The state a plan is born with before anyone has authored a
        // Culture: no identity because no content. A pending draft carrying
        // one of these is mid-composition, not corrupt -- there is nothing
        // in it to lose. Anything WITH content but without identity is
        // still corruption and still refused by ValidationFailure.
        internal static bool IsUnauthoredShell(CACulture culture)
        {
            return culture == null
                || (culture.id.NullOrEmpty()
                    && culture.name.NullOrEmpty()
                    && (culture.constituents?.Count ?? 0) == 0
                    && (culture.inheritedQuestions?.Count ?? 0) == 0
                    && (culture.localQuestions?.Count ?? 0) == 0
                    && (culture.inheritedMeanings?.Count ?? 0) == 0
                    && (culture.localMeanings?.Count ?? 0) == 0
                    && (culture.inheritedPractices?.Count ?? 0) == 0
                    && (culture.practices?.Count ?? 0) == 0
                    && (culture.observations?.Count ?? 0) == 0
                    && (culture.transitions?.Count ?? 0) == 0
                    && (culture.legacyEvidence?.Count ?? 0) == 0);
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
                    return "Culture disagreement settings are invalid";
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
                string[] representedScopes = questions.Where(item =>
                        item != null).Select(item => item.populationScope
                            .NullOrEmpty() ? "*" : item.populationScope)
                    .Distinct(StringComparer.Ordinal).ToArray();
                if (representedScopes.Length == 0)
                    return "Culture has no represented population scope";
                foreach (string representedScope in representedScopes)
                {
                    var present = new HashSet<string>(questions.Where(item =>
                            item != null && string.Equals(
                                item.populationScope.NullOrEmpty()
                                    ? "*" : item.populationScope,
                                representedScope, StringComparison.Ordinal))
                        .Select(item => item.questionKey),
                        StringComparer.Ordinal);
                    if (CACultureQuestionRegistry.All.Any(definition =>
                            !present.Contains(definition.Key)))
                        return "Culture population scope " + representedScope
                            + " does not contain the complete question registry";
                }
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
                return "Choose a Culture preset or set the cultural values "
                    + "before continuing.";
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
                if (source.questionRegistryVersion
                    != CACultureQuestionRegistry.CurrentVersion)
                {
                    failure = "Culture question registry "
                        + source.questionRegistryVersion
                        + " has no supported migration to "
                        + CACultureQuestionRegistry.CurrentVersion;
                    return false;
                }
                failure = ValidationFailure(source, requireSubstantive: true);
                if (!failure.NullOrEmpty()) return false;
                upgraded = source.Copy();
                return true;
            }
            if (source.schemaVersion == 10)
            {
                if (source.questionRegistryVersion != 2)
                {
                    failure = "Culture schema 10 requires question registry 2";
                    return false;
                }
                return TryUpgradeQuestionRegistry(source, out upgraded,
                    out failure);
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
            AddMissingRegistryQuestions(candidate,
                candidate.inheritedQuestions.Concat(candidate.localQuestions)
                    .Where(item => item != null)
                    .Select(item => item.populationScope.NullOrEmpty()
                        ? "*" : item.populationScope)
                    .Distinct(StringComparer.Ordinal).DefaultIfEmpty("*"));
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

        // B16 extends the existing distribution model. Registry-2 positions
        // remain exact; newly admitted constructs begin neutral and
        // low-confidence for each represented population scope. This records
        // absence of evidence without inventing a historical opinion.
        internal static bool TryUpgradeQuestionRegistry(CACulture source,
            out CACulture upgraded, out string failure)
        {
            upgraded = null;
            if (source == null)
            {
                failure = "Culture is missing";
                return false;
            }
            if (source.schemaVersion != 10
                || source.questionRegistryVersion != 2)
            {
                failure = "Culture is not a schema-10 registry-2 record";
                return false;
            }
            if (source.inheritedQuestions == null
                || source.localQuestions == null
                || source.legacyEvidence == null)
            {
                failure = "Culture question state is incomplete";
                return false;
            }

            var legacyKeys = new HashSet<string>(
                CACultureQuestionRegistry.All.Take(
                    CACultureQuestionRegistry.LegacyQuestionCount)
                    .Select(item => item.Key), StringComparer.Ordinal);
            List<CACultureQuestionDistribution> all = source
                .inheritedQuestions.Concat(source.localQuestions)
                .Where(item => item != null).ToList();
            if (all.Any(item => !legacyKeys.Contains(item.questionKey)))
            {
                failure = "registry-2 Culture contains a non-legacy question";
                return false;
            }
            foreach (CACultureQuestionDistribution item in all)
            {
                string itemFailure = CACultureDistributionKernel
                    .ValidationFailure(item);
                if (!itemFailure.NullOrEmpty())
                {
                    failure = "Culture question " + item.questionKey + ": "
                        + itemFailure;
                    return false;
                }
            }
            if (source.inheritedQuestions.GroupBy(QuestionIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1)
                || source.localQuestions.GroupBy(QuestionIdentity,
                        StringComparer.Ordinal).Any(group => group.Count() > 1))
            {
                failure = "a registry-2 Culture question identity is duplicated";
                return false;
            }

            string[] representedScopes = all.Select(item =>
                    item.populationScope.NullOrEmpty()
                        ? "*" : item.populationScope)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (representedScopes.Length == 0)
            {
                failure = "registry-2 Culture has no represented population scope";
                return false;
            }
            foreach (string scope in representedScopes)
            {
                var present = new HashSet<string>(all.Where(item =>
                        string.Equals(item.populationScope.NullOrEmpty()
                                ? "*" : item.populationScope,
                            scope, StringComparison.Ordinal))
                    .Select(item => item.questionKey), StringComparer.Ordinal);
                if (legacyKeys.SetEquals(present)) continue;
                failure = "registry-2 Culture scope " + scope
                    + " is missing legacy questions: "
                    + string.Join(", ", legacyKeys.Except(present)
                        .OrderBy(value => value, StringComparer.Ordinal));
                return false;
            }

            CACulture candidate = source.Copy();
            AddMissingRegistryQuestions(candidate, representedScopes);
            candidate.questionRegistryVersion =
                CACultureQuestionRegistry.CurrentVersion;
            candidate.schemaVersion = CACulture.CurrentSchemaVersion;
            failure = ValidationFailure(candidate, requireSubstantive: true);
            if (!failure.NullOrEmpty()) return false;
            upgraded = candidate;
            return true;
        }

        private static void AddMissingRegistryQuestions(CACulture culture,
            IEnumerable<string> representedScopes)
        {
            string[] scopes = (representedScopes
                    ?? Enumerable.Empty<string>()).Distinct(
                        StringComparer.Ordinal).ToArray();
            foreach (string scope in scopes)
            {
                var present = new HashSet<string>(culture
                    .inheritedQuestions.Concat(culture.localQuestions)
                    .Where(item => item != null && string.Equals(
                        item.populationScope.NullOrEmpty()
                            ? "*" : item.populationScope,
                        scope, StringComparison.Ordinal))
                    .Select(item => item.questionKey), StringComparer.Ordinal);
                foreach (CACultureQuestionDef definition in
                    CACultureQuestionRegistry.All)
                {
                    if (present.Contains(definition.Key)) continue;
                    CACultureQuestionDistribution added =
                        NewRegistryUpgradeQuestion(culture, scope,
                            definition);
                    added.spread = PopulationSpread(
                        culture.withinGroupSpread);
                    culture.inheritedQuestions.Add(added);
                    SyncSubgroups(culture, added);
                }
            }
        }

        // This is the one production projection used by live migration and
        // the governed pending-plan converter. It owns the exact default,
        // provenance, source identity, and evidence identity for every newly
        // admitted Culture question.
        internal static CACultureQuestionDistribution
            NewRegistryUpgradeQuestion(CACulture culture, string scope,
                CACultureQuestionDef definition)
        {
            CACultureQuestionDistribution added =
                CACultureDistributionKernel.NewQuestion(definition, scope);
            added.salience = Math.Min(0.18f, definition.DefaultSalience);
            added.sourceConfidence = 0.20f;
            added.provenance = "B16 playable-ontology coverage; neutral "
                + "because no authored or historical evidence establishes "
                + "this population's position";
            added.sourceIdentity = scope == "*" ? culture?.id : scope;
            added.evidenceSignature = CASocialPatternKernel.StableHash(
                (culture?.id ?? "Culture") + "|" + scope + "|"
                    + definition.Key);
            return added;
        }

        private static List<CACultureQuestionDistribution> MigrateQuestions(
            IEnumerable<CACulturalMeaning> source, string layer,
            string sourceIdentity, List<CACultureLegacyEvidence> evidence)
        {
            var mapped = new List<CACultureQuestionDistribution>();
            List<CACulturalMeaning> sourceValues = (source
                    ?? Enumerable.Empty<CACulturalMeaning>())
                .Where(value => value != null).ToList();
            foreach (CACulturalMeaning meaning in sourceValues)
            {
                bool hasAdapters = CACultureQuestionRegistry
                    .AdaptersForSocialSubject(meaning.subjectKey).Count > 0;
                evidence.Add(new CACultureLegacyEvidence
                {
                    sourceKey = meaning.subjectKey,
                    sourceLayer = "B11 " + layer + " social meaning",
                    populationScope = meaning.populationScope.NullOrEmpty()
                        ? "*" : meaning.populationScope,
                    disposition = !hasAdapters
                        ? "preserved as evidence; no exact current question"
                        : "approval and salience mapped through every exact "
                            + "signed subject adapter; other dimensions remain "
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
            }
            foreach (var group in sourceValues.SelectMany(meaning =>
                    CACultureQuestionRegistry.AdaptersForSocialSubject(
                            meaning.subjectKey)
                        .Select(adapter => new { Meaning = meaning, Adapter = adapter }))
                .GroupBy(value => value.Adapter.QuestionKey + "\0"
                    + (value.Meaning.populationScope.NullOrEmpty()
                        ? "*" : value.Meaning.populationScope),
                    StringComparer.Ordinal))
            {
                string[] identity = group.Key.Split('\0');
                string questionKey = identity[0];
                CALegacyCultureQuestionAdapterResult adapted =
                    CALegacyCultureQuestionAdapter.Adapt(group.Select(value =>
                        new CALegacyCultureMeaningAdapterInput(
                            value.Meaning.approval,
                            value.Meaning.salience, value.Meaning.weight,
                            value.Adapter.Direction)));
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
                        + " approval and salience through every signed "
                        + "question mapping; other dimensions retained as evidence",
                    sourceIdentity = sourceIdentity ?? "B11 Culture",
                    evidenceSignature = CASocialPatternKernel.StableHash(
                        string.Join("|", group.Select(value =>
                            value.Meaning.subjectKey + ":"
                            + value.Meaning.approval + ":"
                            + value.Meaning.normality + ":"
                            + value.Meaning.prestige + ":"
                            + value.Meaning.salience + ":"
                            + value.Meaning.weight + ":"
                            + value.Adapter.QuestionKey + ":"
                            + value.Adapter.Direction))),
                    firstRecordedTick = group.Where(value =>
                            value.Meaning.firstRecordedTick >= 0)
                        .Select(value => value.Meaning.firstRecordedTick)
                        .DefaultIfEmpty(-1).Min(),
                    lastChangedTick = group.Where(value =>
                            value.Meaning.lastChangedTick >= 0)
                        .Select(value => value.Meaning.lastChangedTick)
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
            SynchronizeOwnIdentityLabel(culture);
        }

        // A constituent row whose identity is the Culture itself is a
        // presentation reference to that Culture, not an independent
        // historical source. Keep its fallback label synchronized without
        // rewriting genuinely distinct parent or constituent cultures.
        internal static bool SynchronizeOwnIdentityLabel(CACulture culture)
        {
            if (culture == null || culture.id.NullOrEmpty()
                || culture.constituents == null)
                return false;
            string current = culture.name.NullOrEmpty()
                ? "Unnamed culture" : culture.name;
            bool changed = false;
            foreach (CACultureConstituent constituent in culture.constituents
                .Where(item => item != null
                    && item.cultureId == culture.id))
            {
                if (constituent.label == current) continue;
                constituent.label = current;
                changed = true;
            }
            return changed;
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
                    ? "neutral visual style"
                    : "visual style unavailable");
            string continuity = culture.localityKey.NullOrEmpty()
                ? "brought by the population"
                : culture.maturity == CACultureMaturity.Established
                    ? "local to " + culture.localityKey
                    : "developing in " + culture.localityKey;
            string plurality = culture.constituents.Count <= 1 ? ""
                : " · " + culture.constituents.Count
                    + " cultural roots";
            string change = culture.transitions.Count == 0 ? ""
                : " · " + culture.transitions.Count + " recorded change"
                    + (culture.transitions.Count == 1 ? "" : "s");
            int meaningCount = PopulationQuestions(culture).Count();
            int practiceCount = culture.inheritedPractices.Count
                + culture.practices.Count;
            string practice = practiceCount == 0 ? ""
                : " · " + practiceCount + " observed practice"
                    + (practiceCount == 1 ? "" : "s");
            string salient = string.Join(", ", PopulationQuestions(culture)
                .Where(item => item != null)
                .OrderByDescending(item => item.salience)
                .ThenBy(item => item.questionKey)
                .Take(3)
                .Select(item => CACultureQuestionRegistry.Find(
                    item.questionKey)?.Label ?? "recorded value")
                .ToArray());
            string top = salient.NullOrEmpty() ? ""
                : " · main values: " + salient;
            return identity + " · " + continuity + plurality + change
                + " · " + meaningCount + " value"
                    + (meaningCount == 1 ? "" : "s") + practice
                + top + " · visual style: " + visual;
        }

        internal static CACulturalMeaningResolution Resolve(CACulture culture,
            string subjectKey, string populationScope = null)
        {
            if (culture == null)
                return CACulturalMeaningResolver.Resolve(null, subjectKey,
                    populationScope);
            IReadOnlyList<CACultureQuestionSubjectAdapterDef> adapters =
                CACultureQuestionRegistry.AdaptersForSocialSubject(subjectKey);
            if (adapters.Count == 0)
                return CACulturalMeaningResolver.Resolve(null, subjectKey,
                    populationScope);
            var values = adapters.Select(adapter => new
                {
                    Adapter = adapter,
                    Distribution = DistributionFor(culture,
                        adapter.QuestionKey, populationScope)
                }).Where(value => value.Distribution != null).Select(value =>
                {
                    CACultureSubgroupDistribution subgroup = value.Distribution
                        .subgroups?.FirstOrDefault(item => item != null
                            && item.subgroupKey == populationScope);
                    float mean = Mathf.Clamp(value.Distribution.mean
                        + (subgroup?.meanOffset ?? 0f), -1f, 1f)
                        * value.Adapter.Direction;
                    float descriptive = (value.Distribution
                        .hasDescriptiveNormPrior
                            ? value.Distribution.descriptiveNormPrior
                            : value.Distribution.mean)
                        * value.Adapter.Direction;
                    float spread = value.Distribution.spread
                        * (subgroup?.spreadMultiplier ?? 1f);
                    float weight = Mathf.Max(0.01f,
                        value.Distribution.salience
                            * value.Distribution.sourceConfidence);
                    return new
                    {
                        value.Adapter,
                        value.Distribution,
                        Mean = mean,
                        Descriptive = descriptive,
                        Spread = spread,
                        Weight = weight
                    };
                }).ToList();
            if (values.Count == 0)
                return CACulturalMeaningResolver.Resolve(null, subjectKey,
                    populationScope);
            float total = values.Sum(value => value.Weight);
            float mean = values.Sum(value => value.Mean * value.Weight)
                / total;
            float descriptive = values.Sum(value => value.Descriptive
                    * value.Weight) / total;
            float prestige = values.Sum(value => value.Distribution
                    .prestigeSignal * value.Adapter.Direction * value.Weight)
                / total;
            float salience = values.Sum(value => value.Distribution.salience
                    * value.Weight) / total;
            float confidence = values.Sum(value => value.Distribution
                    .sourceConfidence * value.Weight) / total;
            float withinSpread = values.Sum(value => value.Spread
                    * value.Weight) / total;
            float crossQuestion = Mathf.Sqrt(values.Sum(value =>
                    (value.Mean - mean) * (value.Mean - mean) * value.Weight)
                / total);
            var result = new CACulturalMeaningResolution
            {
                SubjectKey = subjectKey,
                Approval = Mathf.RoundToInt(mean * 100f),
                Normality = Mathf.RoundToInt((descriptive + 1f) * 50f),
                Prestige = Mathf.RoundToInt(prestige * 100f),
                Salience = Mathf.RoundToInt(salience * 100f),
                Dissonance = Mathf.Clamp01(Mathf.Max(withinSpread,
                    crossQuestion)),
                Confidence = Mathf.Clamp01(confidence * salience)
            };
            foreach (var value in values)
            {
                result.Contributions.Add(new CACulturalMeaningContribution
                {
                    PopulationScope = populationScope
                        ?? value.Distribution.populationScope,
                    Provenance = value.Adapter.QuestionKey + "; "
                        + value.Distribution.provenance,
                    SourceIdentity = value.Distribution.sourceIdentity,
                    Weight = Mathf.Max(1,
                        Mathf.RoundToInt(value.Weight * 100f)),
                    Approval = Mathf.RoundToInt(value.Mean * 100f),
                    Normality = Mathf.RoundToInt(
                        (value.Descriptive + 1f) * 50f),
                    Prestige = Mathf.RoundToInt(value.Distribution
                        .prestigeSignal * value.Adapter.Direction * 100f),
                    Salience = Mathf.RoundToInt(value.Distribution.salience
                        * 100f)
                });
                string provenance = (value.Distribution.provenance
                        ?? "recorded") + ":"
                    + (value.Distribution.sourceIdentity ?? "unrecorded")
                    + ":" + value.Adapter.QuestionKey;
                if (!result.Provenance.Contains(provenance))
                    result.Provenance.Add(provenance);
            }
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
            local.authoredMask &= ~CACulture.NameField;
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
            CARegionalSettlementPlan settlement,
            bool refreshInheritedState = false)
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
                // This is a derived local label, not a second authored name.
                // A later explicit rename sets NameField and severs only this
                // dependency; parent identity and constituent references
                // continue to follow their owning cultures.
                local.authoredMask &= ~CACulture.NameField;
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
            bool followsInheritedName = !existing.Authored(
                    CACulture.NameField)
                || MatchesRecordedParentName(existing);
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
            bool compositionChanged = existing.compositionSignature != signature;
            bool referencesChanged = ConstituentReferenceSignature(
                    existing.constituents)
                != ConstituentReferenceSignature(sources);
            if (!compositionChanged && !referencesChanged
                && !refreshInheritedState)
                return;
            // Authoring replaces the draft baseline. It is not lived history,
            // and two edit paths that end on the same population must produce
            // the same settlement state.
            CACulture inheritedNow = DominantCulture(plan, settlement);
            existing.parentId = inheritedNow?.id;
            existing.localityKey = (plan.regionName
                    ?? plan.regionalId ?? "region")
                + "/" + CARegionalPlanUtility.SettlementName(plan,
                    settlement);
            if (followsInheritedName)
            {
                existing.name = LocalName(inheritedNow?.name);
                existing.authoredMask &= ~CACulture.NameField;
            }
            settlement.localCulture.constituents = sources;
            SyncConstituentQuestionBaselines(plan, settlement.localCulture);
            settlement.localCulture.compositionSignature = signature;
            if (compositionChanged)
            {
                settlement.localCulture.lastEvidence = null;
                settlement.localCulture.observations.Clear();
            }
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
            foreach (var group in (patterns
                    ?? Enumerable.Empty<CASocialGroupPattern>())
                .Where(value => value != null
                    && value.EvidenceCount >= 2
                    && value.ObservedPawnCount >= 2
                    && value.EligiblePopulation > 0
                    && value.Participation >= 0.10f
                    && value.EvidenceStartTick >= 0
                    && value.LastEvidenceTick - value.EvidenceStartTick
                        >= CACulturalMeaningTransitionKernel.HistoricalPeriod)
                .SelectMany(value => !value.QuestionKey.NullOrEmpty()
                    ? new[] { new { Pattern = value,
                        QuestionKey = value.QuestionKey, Direction = 1 } }
                    : CACultureQuestionRegistry.AdaptersForSocialSubject(
                            value.SubjectKey)
                        .Select(adapter => new { Pattern = value,
                            QuestionKey = adapter.QuestionKey,
                            Direction = adapter.Direction }))
                .Where(value => !value.QuestionKey.NullOrEmpty())
                .GroupBy(value => value.QuestionKey + "\0"
                    + (value.Pattern.PopulationIdentity ?? "*"),
                    StringComparer.Ordinal))
            {
                string[] identity = group.Key.Split('\0');
                string questionKey = identity[0];
                string population = identity.Length > 1 ? identity[1] : "*";
                int totalWeight = group.Sum(value => Math.Max(1,
                    value.Pattern.ObservedPawnCount));
                bool hasAppraisal = group.Any(value =>
                    value.Pattern.AppraisalEvidence);
                int appraisalWeight = group.Where(value =>
                        value.Pattern.AppraisalEvidence)
                    .Sum(value => Math.Max(1,
                        value.Pattern.ObservedPawnCount));
                float observedPosition = group.Sum(value =>
                        value.Pattern.WeightedPosition / 100f
                        * value.Direction
                        * Math.Max(1, value.Pattern.ObservedPawnCount))
                    / Math.Max(1, totalWeight);
                float observedAppraisal = hasAppraisal ? group.Where(value =>
                        value.Pattern.AppraisalEvidence).Sum(value =>
                            value.Pattern.WeightedPosition / 100f
                            * value.Direction * Math.Max(1,
                                value.Pattern.ObservedPawnCount))
                        / Math.Max(1, appraisalWeight) : 0f;
                float coverage = group.Sum(value => value.Pattern.Participation
                        * Math.Max(1, value.Pattern.ObservedPawnCount))
                    / Math.Max(1, totalWeight);
                float dispersion = group.Sum(value => value.Pattern.Dispersion
                        * Math.Max(1, value.Pattern.ObservedPawnCount))
                    / Math.Max(1, totalWeight);
                float alignment = group.Sum(value => value.Pattern.GroupAlignment
                        * Math.Max(1, value.Pattern.ObservedPawnCount))
                    / Math.Max(1, totalWeight);
                float observedNorm = Mathf.Clamp(observedPosition
                    * (0.5f + coverage * 0.5f), -1f, 1f);
                float observedSalience = Mathf.Clamp01(dispersion + coverage);
                float observedNormStrength = Mathf.Clamp01(
                    alignment * coverage);
                string evidenceSignature = CASocialPatternKernel.StableHash(
                    string.Join("|", group.OrderBy(value => value.Pattern.SubjectKey,
                        StringComparer.Ordinal).Select(value =>
                        value.Pattern.SubjectKey + ":"
                            + value.Pattern.EvidenceSignature + ":"
                            + value.QuestionKey + ":" + value.Direction
                            + ":" + value.Pattern.AppraisalEvidence)));
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
                float nextMean = !hasAppraisal ? basis?.mean ?? 0f
                    : basis == null ? observedAppraisal
                    : Mathf.Lerp(basis.mean, observedAppraisal, 0.25f);
                float nextSpread = !hasAppraisal ? basis?.spread
                        ?? CACultureDistributionKernel.VarianceFloor
                    : basis == null
                    ? Mathf.Max(CACultureDistributionKernel.VarianceFloor,
                        dispersion)
                    : Mathf.Lerp(basis.spread, Mathf.Max(
                        CACultureDistributionKernel.VarianceFloor,
                        dispersion), 0.25f);
                float nextNorm = basis == null ? observedNorm
                    : Mathf.Lerp(basis.hasDescriptiveNormPrior
                        ? basis.descriptiveNormPrior : basis.mean,
                        observedNorm, 0.35f);
                float nextSalience = !hasAppraisal ? basis?.salience ?? 0f
                    : basis == null ? observedSalience
                    : Mathf.Lerp(basis.salience, observedSalience, 0.25f);
                float nextNormStrength = !hasAppraisal
                    ? basis?.normStrength ?? 0f : basis == null
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
                            sourceConfidence = hasAppraisal
                                ? Mathf.Clamp01(coverage) : 0.20f,
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
                if (hasAppraisal)
                    current.sourceConfidence = Mathf.Clamp01(Mathf.Max(
                        current.sourceConfidence, coverage));
                current.provenance = hasAppraisal
                    ? "sustained represented social response"
                    : "sustained represented state";
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
            Record(culture, changedDimensions.Any(value => value == "mean")
                    ? "sustained represented social response"
                    : "sustained represented state",
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
            string origin = inheritedSource != null
                ? "Brought from " + inheritedSource.label
                : culture.parentId.NullOrEmpty()
                    ? "No earlier Culture is known"
                    : "Developed from an earlier Culture";
            string maturity = culture.maturity
                == CACultureMaturity.Established ? "Established"
                : culture.maturity == CACultureMaturity.Forming
                    ? "Still forming" : "Brought by this population";
            CACultureTransition latest = culture.transitions?
                .Where(item => item != null)
                .OrderByDescending(item => item.sequence).FirstOrDefault();
            string transition = latest == null
                ? "No later change is known"
                : "Latest change: " + (latest.summary
                    ?? latest.cause ?? "local practice changed");
            return origin + ". " + maturity + ". " + transition + ".";
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
                    ? population.factionKey : settlement.OwningFactionKey;
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
                ? dominant.factionKey : settlement.OwningFactionKey;
            return plan?.FactionPlan(key)?.culture
                ?? plan?.FactionPlan(settlement.OwningFactionKey)?.culture;
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

        private static string ConstituentReferenceSignature(
            IEnumerable<CACultureConstituent> sources)
        {
            return string.Join("|", (sources
                    ?? Enumerable.Empty<CACultureConstituent>())
                .Where(item => item != null)
                .OrderBy(item => item.cultureId ?? "label:" + item.label,
                    StringComparer.Ordinal)
                .Select(item => (item.cultureId ?? "unrecorded") + ":"
                    + (item.label ?? "unnamed") + ":" + item.share + ":"
                    + (item.inherited ? "inherited" : "local") + ":"
                    + (item.ideoligionProtected
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

        private static bool MatchesRecordedParentName(CACulture local)
        {
            if (local == null || local.parentId.NullOrEmpty()
                || local.name.NullOrEmpty()) return false;
            CACultureConstituent recordedParent = local.constituents?
                .FirstOrDefault(item => item != null
                    && item.cultureId == local.parentId);
            return recordedParent != null
                && string.Equals(local.name, LocalName(recordedParent.label),
                    StringComparison.OrdinalIgnoreCase);
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
            CAPoliticalOrderModel.Normalize(beliefs);
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
            if (!CAPoliticalOrderModel.HasVariables(beliefs))
                return "political order variables are missing";
            string orderFailure = CAPoliticalOrderModel
                .ValidationFailure(beliefs);
            if (!orderFailure.NullOrEmpty()) return orderFailure;
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
            CAPoliticalOrderModel.Ensure(beliefs, seed);
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

        internal static string Summary(CAPoliticalBeliefs beliefs)
        {
            return CAPoliticalOrderModel.HasVariables(beliefs)
                ? CAPoliticalOrderModel.ShortSummary(beliefs)
                : "Political Order not set";
        }

    }

    internal static class CAFactionStructureModel
    {
        // Established institutions are represented facts, not a second copy
        // of Political Order. Without authored or observed institutional
        // evidence, unset subjects remain unset.
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
            if (structure == null || !structure.Any(item => item != null
                    && item.source != (byte)CAAxisSource.Unset))
                return "No institutions set";
            string leadership = OptionLabels(structure,
                CAFactionAxes.Leadership);
            string decisions = OptionLabels(structure,
                CAFactionAxes.Decisions);
            string ownership = OptionLabels(structure,
                CAFactionAxes.Ownership);
            return (leadership ?? "leadership not set") + " · "
                + (decisions ?? "decision rules not set") + " · "
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
            if (group.technologicalKnowledge == null)
                group.technologicalKnowledge =
                    new CATechnologicalKnowledge();
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
            if (group.technologicalKnowledge.domains == null
                || group.technologicalKnowledge.domains.Count == 0)
            {
                Faction existing = group.source
                    == CARegionalFactionSource.ExistingWorldFaction
                    ? CARegionalPlanUtility.FactionByLoadId(
                        group.existingFactionLoadId) : null;
                CATechnologicalKnowledge live = existing == null ? null
                    : CAFactionStateWorldComponent.Current?.Find(existing)
                        ?.technologicalKnowledge;
                if (live?.domains?.Count > 0)
                    group.technologicalKnowledge = live.Copy();
                else
                    CATechnologicalKnowledgeModel.SeedFromEngineTemplate(
                        group.technologicalKnowledge,
                        group.ResolvedFactionDef, seed + ":technology");
            }
            CATechnologicalKnowledgeModel.Ensure(
                group.technologicalKnowledge, seed + ":technology");
        }

        internal static void ApplyPlan(Faction faction,
            CACulture culture,
            CAPoliticalBeliefs beliefs,
            CATechnologicalKnowledge technologicalKnowledge,
            List<CAAxisEntry> structure,
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
            if (technologicalKnowledge != null)
                record.technologicalKnowledge =
                    technologicalKnowledge.Copy();
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
            doCloseX = false;
            doCloseButton = false;
            doWindowBackground = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        protected override float Margin => 0f;

        public override void DoWindowContents(Rect inRect)
        {
            inRect = CAOpeningTheme.BeginWindowSurface(inRect);
            try
            {
                DoEditorContents(inRect);
            }
            finally
            {
                CAOpeningTheme.EndWindowSurface();
            }
        }

        private void DoEditorContents(Rect inRect)
        {
            GameFont previous = Text.Font;
            Text.Font = GameFont.Medium;
            GUI.color = CAOpeningTheme.TextHi;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "Culture");
            GUI.color = Color.white;
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
                ? "Set this settlement's Culture at scenario start. The "
                    + "Culture its population arrived with remains part of "
                    + "its history."
                : "Set the customs and values this population brings to the "
                    + "world. Settlements may change them during play.";
            float descriptionHeight = Text.CalcHeight(description, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, descriptionHeight),
                description);
            y += descriptionHeight + 12f;

            if (boundary == CACultureAuthoringBoundary.Inherited)
                y = DrawActions(inRect, y);
            string[] tabs = { "Overview", "Values", "Practices",
                "Visual style" };
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

            if (CAOpeningTheme.PrimaryButton(new Rect(
                    inRect.width - 150f, inRect.height - 32f, 150f, 30f),
                    "Done"))
                Close();
        }

        private float DrawActions(Rect inRect, float y)
        {
            var labels = new List<string>
            {
                "Culture presets...", "Randomize Culture"
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
                if (CAOpeningTheme.GhostButton(
                        new Rect(column * (width + gap), y + row * 36f,
                            width, 30f), labels[i]))
                    actions[i]();
            }
            return y + rows * 36f + 4f;
        }

        private void DrawOverview(ref float y, float width)
        {
            Row(ref y, width, "Name",
                culture.name ?? "Unnamed Culture",
                () => Find.WindowStack.Add(new Dialog_CARenameCulture(
                    culture, changed)), FieldState(CACulture.NameField));
            string earlierCulture = EarlierCultureName(culture);
            if (!earlierCulture.NullOrEmpty())
                DrawFact(ref y, width,
                    boundary == CACultureAuthoringBoundary.EstablishedLocal
                        ? "Culture brought by residents" : "Developed from",
                    earlierCulture);
            if (culture.constituents.Count(value => value != null
                    && value.share > 0) > 1)
                DrawFact(ref y, width, "Cultural mix",
                    string.Join(", ", culture.constituents
                        .Where(item => item != null && item.share > 0)
                        .Select(item => item.share + "% "
                            + (item.label ?? "unnamed"))));
            CACulturePractice[] practices = culture.inheritedPractices
                .Concat(culture.practices).Where(item => item != null)
                .OrderByDescending(item => item.strength).ToArray();
            if (practices.Length > 0)
                DrawFact(ref y, width, "Practices", string.Join(", ",
                    practices.Take(4).Select(PracticeName))
                    + (practices.Length > 4
                        ? " and " + (practices.Length - 4) + " more" : ""));
            DrawFact(ref y, width, "Overall disagreement",
                SpreadLabels[Mathf.Clamp(culture.withinGroupSpread, 0, 4)]);
            if (HasRepresentedSubgroupDifferences)
                DrawFact(ref y, width, "Differences between groups",
                    SeparationLabels[Mathf.Clamp(
                        culture.subgroupSeparation, 0, 4)]);
            CACultureQuestionDistribution[] salient = CACultureModel
                .PopulationQuestions(culture)
                .OrderByDescending(item => item.salience).Take(3).ToArray();
            DrawFact(ref y, width, "Main values",
                salient.Length == 0 ? "None chosen" : string.Join(", ",
                    salient.Select(item => ValueSummary(culture, item))));
            CACultureQuestionDistribution[] polarized = CACultureModel
                .PopulationQuestions(culture)
                .Where(item => item != null && (item.spread >= 0.55f
                    || (item.subgroups?.Any(group => group != null
                        && Math.Abs(group.meanOffset) >= 0.20f) == true)))
                .OrderByDescending(item => item.spread).Take(3).ToArray();
            if (polarized.Length > 0)
                DrawFact(ref y, width, "Disputed values",
                    string.Join(", ", polarized.Select(item =>
                        CACultureQuestionRegistry.Find(item.questionKey)?.Label
                            ?? "Unnamed value")));
            CultureDef native = CACultureModel.NativeDef(culture);
            DrawFact(ref y, width, "Visual style",
                native?.LabelCap.ToString() ?? "None");
            y += 6f;
            bool hasCausalFacts = culture.inheritedQuestions.Count
                    + culture.localQuestions.Count
                    + culture.inheritedPractices.Count
                    + culture.practices.Count > 0;
            if (hasCausalFacts)
            {
                if (CAOpeningTheme.GhostButton(new Rect(0f, y,
                        Mathf.Min(260f, width), 32f),
                        "Gameplay effects..."))
                    Find.WindowStack.Add(
                        new Dialog_CACultureCausalInspector(culture,
                            factionLabel, boundary));
                y += 42f;
            }
        }

        private void DrawQuestions(ref float y, float width)
        {
            DrawExplanation(ref y, width,
                "Choose what most people believe. Overall disagreement sets "
                + "how much opinions vary. Use More to adjust one value.");
            string comparison = IdeoligionComparisonWords();
            if (!comparison.NullOrEmpty())
                DrawExplanation(ref y, width, comparison);
            DrawPopulationDistributionControls(ref y, width);
            if (boundary == CACultureAuthoringBoundary.EstablishedLocal)
                DrawExplanation(ref y, width,
                    "The Culture brought by residents remains in their "
                    + "history. Set a local view to change what this "
                    + "settlement believes now.");
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
                    "Overall disagreement");
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
                CACultureQuestionLayer.BodyHealthDeath =>
                    "Body, health, and death",
                CACultureQuestionLayer.FoodSubstances =>
                    "Food and substances",
                CACultureQuestionLayer.AnimalsEnvironment =>
                    "Animals and the environment",
                CACultureQuestionLayer.DailyLifeTechnology =>
                    "Daily life and technology",
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
            Widgets.DrawBoxSolid(box, CAOpeningTheme.Surface);
            CAOpeningTheme.Border(box, CAOpeningTheme.Hairline);
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
            if (question != null && CAOpeningTheme.GhostButton(
                    new Rect(width - 72f, at, 60f, 28f),
                    expanded ? "Less" : "More"))
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
                        "This is the view residents brought with them. Set a "
                        + "local view before changing it for this settlement.");
                    if (CAOpeningTheme.GhostButton(new Rect(0f, at,
                            Mathf.Min(260f, width), 28f),
                            "Set local view"))
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
                    "No view has been chosen for this population.");
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
            float mean = FloatSlider(ref y, width,
                "View (" + summary.Anchor + ")",
                question.mean, -1f, 1f);
            float spread = FloatSlider(ref y, width, "Disagreement",
                question.spread, CACultureDistributionKernel.VarianceFloor,
                1f);
            float salience = FloatSlider(ref y, width, "Importance",
                question.salience, 0f, 1f);
            float normStrength = FloatSlider(ref y, width,
                "Pressure to conform", question.normStrength, 0f, 1f);
            float tolerance = FloatSlider(ref y, width,
                "Tolerance of disagreement",
                question.toleranceForDivergence,
                0f, 1f);
            float visibility = FloatSlider(ref y, width,
                "How visible this value is",
                question.visibility, 0f, 1f);
            float confidence = FloatSlider(ref y, width,
                "Confidence in this view",
                question.sourceConfidence,
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
            if (question.spreadOverride && CAOpeningTheme.GhostButton(
                    new Rect(0f, y, Mathf.Min(260f, width), 28f),
                    "Use overall disagreement"))
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
                && CAOpeningTheme.GhostButton(new Rect(0f, y,
                    Mathf.Min(260f, width), 28f), "Use residents' view"))
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
                    (subgroup.label ?? subgroup.subgroupKey) + " difference",
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
            if (customSubgroups && CAOpeningTheme.GhostButton(
                    new Rect(0f, y, Mathf.Min(300f, width), 28f),
                    "Use group differences"))
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
            string preview = question == null ? "No value selected."
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
            const string message = "This is the view residents brought with "
                + "them. Set a local view before changing it for this "
                + "settlement.";
            return Text.CalcHeight(message, width) + 50f;
        }

        private string CultureSourceWords(bool inheritedFallback)
        {
            return inheritedFallback
                || boundary == CACultureAuthoringBoundary.Inherited
                    ? "Brought by this population"
                    : "This settlement's view";
        }

        private void DrawPracticeHistory(ref float y, float width)
        {
            DrawExplanation(ref y, width,
                "These are customs people have repeatedly followed. They can "
                + "change Culture during play, but are not settings here.");
            List<CACulturePractice> values = culture.inheritedPractices
                .Concat(culture.practices).Where(value => value != null)
                .OrderByDescending(value => value.strength).ToList();
            if (values.Count == 0)
            {
                DrawExplanation(ref y, width,
                    "No established practices yet.");
                return;
            }
            foreach (CACulturePractice practice in values)
            {
                CACulturalPracticeDef definition =
                    CACulturalPracticeRegistry.Find(practice.practiceKey);
                DrawFact(ref y, width,
                    definition?.Label ?? "Unnamed practice",
                    (definition?.Summary ?? practice.summary)
                    + " " + PracticeStrengthWords(practice.strength)
                    + " This practice is part of the population's history.");
            }
        }

        private string IdeoligionBadge(CACultureQuestionDef definition,
            CACultureQuestionDistribution question)
        {
            if (ideoligionComparison
                    != CACultureIdeoligionComparison.Single
                || ideoligion == null) return null;
            if (!CAIdeoligionSemanticAdapterRegistry.HasQuestion(
                    definition.Key))
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
                    return "This population follows more than one Ideoligion, "
                        + "so no single comparison is shown.";
                case CACultureIdeoligionComparison.Pending:
                    return "Choose an Ideoligion to compare it with these "
                        + "values.";
                case CACultureIdeoligionComparison.None:
                    return "This population has no shared Ideoligion. Culture "
                        + "can still be set independently.";
                case CACultureIdeoligionComparison.Unresolved:
                    return "One or more Ideoligions could not be loaded, so no "
                        + "comparison is shown.";
                default:
                    return null;
            }
        }

        private static string IdeoligionSourceLabel(string source)
        {
            return CAIdeoligionSemanticAdapterRegistry.SourceLabel(source);
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
            return summary.Anchor + ". Views are "
                + SpreadWords(summary.Spread) + "; "
                + Percent(summary.Polarization)
                + " of the population is sharply divided.";
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
            string[] labels = CACultureQuestionRegistry
                .AdaptersForSocialSubject(value?.sourceKey)
                .Select(adapter => CACultureQuestionRegistry
                    .Find(adapter.QuestionKey)?.Label)
                .Where(label => !label.NullOrEmpty())
                .Distinct(StringComparer.Ordinal).ToArray();
            return labels.Length == 0 ? "unmapped historical evidence"
                : string.Join("; ", labels);
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
                    : "Saved visual style unavailable - using neutral style");
            Row(ref y, width, "Visual style", value,
                OpenSourceCulture, FieldState(CACulture.SourceCultureField));
            DrawExplanation(ref y, width,
                "Choose the RimWorld styles this Culture uses. Neutral uses "
                + "the ordinary object styles.");
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

        private static string EarlierCultureName(CACulture value)
        {
            if (value == null || value.parentId.NullOrEmpty())
                return null;
            CACultureConstituent parent = value.constituents
                ?.FirstOrDefault(item => item != null
                    && item.cultureId == value.parentId);
            if (parent != null && !parent.label.NullOrEmpty())
                return parent.label;
            return "An earlier Culture";
        }

        private static string PracticeName(CACulturePractice practice)
        {
            return CACulturalPracticeRegistry.Find(practice?.practiceKey)?.Label
                ?? practice?.summary ?? "Unnamed practice";
        }

        private static string PracticeStrengthWords(int strength)
        {
            return strength >= 75 ? "It is widely practiced."
                : strength >= 40 ? "It is regularly practiced."
                : "It is occasionally practiced.";
        }

        private static string ValueSummary(CACulture culture,
            CACultureQuestionDistribution value)
        {
            CACultureQuestionDef definition = CACultureQuestionRegistry.Find(
                value?.questionKey);
            if (definition == null || value == null) return "Unnamed value";
            CACultureDistributionSummary summary =
                CACultureDistributionKernel.Summarize(value,
                    culture?.id ?? value.questionKey);
            return definition.Label + ": " + summary.Anchor;
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
            if (CAOpeningTheme.GhostButton(valueRect, value ?? "Choose"))
                edit?.Invoke();
            y = valueRect.yMax + 8f;
        }

        private void OpenCultureProfiles()
        {
            if (CAAuthoringProfileLibrary.Cultures.Count == 0) return;
            CACreationUI.OpenChoices("Saved Cultures",
                "Use a saved Culture. Visual style is chosen separately.",
                CAAuthoringChoices.CultureProfiles(culture,
                    culture.id ?? factionLabel ?? "ca-culture", changed));
        }

        private void OpenBuiltInPresets()
        {
            CACreationUI.OpenChoices("Culture presets",
                "Choose a complete historical or social starting point. "
                    + "The preset sets every cultural value. You can change "
                    + "any result afterward.",
                CAAuthoringChoices.CulturePresets(culture,
                    culture.id ?? factionLabel ?? "authored-culture", delegate
                    {
                        expandedQuestionKey = null;
                        changed?.Invoke();
                    }));
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
                Summary = "Use ordinary object styles.",
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
                        ? "RimWorld object styles."
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
            CACreationUI.OpenChoices("Visual style",
                "Choose the RimWorld styles this Culture uses.",
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
            doCloseX = false;
            doCloseButton = false;
            doWindowBackground = false;
            absorbInputAroundWindow = true;
        }

        protected override float Margin => 0f;

        public override void DoWindowContents(Rect inRect)
        {
            inRect = CAOpeningTheme.BeginWindowSurface(inRect);
            try
            {
                DoInspectorContents(inRect);
            }
            finally
            {
                CAOpeningTheme.EndWindowSurface();
            }
        }

        private void DoInspectorContents(Rect inRect)
        {
            GameFont prior = Text.Font;
            CAOpeningTheme.Heading(0f, 0f, inRect.width,
                "Gameplay effects");
            const string introduction = "How these values affect people and "
                + "settlements. Ideoligion, Political Order, and institutions "
                + "are set separately.";
            float introHeight = CAOpeningTheme.Fine(0f, 36f, inRect.width,
                introduction);
            float top = 46f + introHeight;
            if (CAOpeningTheme.PrimaryButton(new Rect(
                    inRect.width - 150f, inRect.height - 32f, 150f, 30f),
                    "Done"))
                Close();
            Rect outRect = new Rect(0f, top, inRect.width,
                inRect.height - top - 38f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            DrawFact(ref y, view.width, "Population",
                (ownerLabel.NullOrEmpty() ? "This population" : ownerLabel)
                + (boundary == CACultureAuthoringBoundary.EstablishedLocal
                    ? " already lives here."
                    : " brings this Culture with it."));
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
                string consumers = CACultureQuestionExecutionRoutes
                    .PlayerSummary(distribution.questionKey);
                string detail = "Most people favor "
                    + (definition == null ? Signed(distribution.mean)
                        : definition.Anchors[CACultureDistributionKernel
                            .NearestAnchor(distribution.mean)])
                    + ". Opinions are " + SpreadWords(summary.Spread)
                    + "; this value is "
                    + SalienceWords(distribution.salience)
                    + "; disagreement is "
                    + ToleranceWords(distribution.toleranceForDivergence)
                    + ". Pressure to conform is "
                    + PressureWords(distribution.normStrength)
                    + ".\nObserved through: "
                    + (consumers.NullOrEmpty() ? "no represented evidence yet"
                        : consumers) + ".\nChange during play: "
                    + HistoricalDrift(culture, distribution.questionKey)
                    + ".";
                DrawFact(ref y, view.width,
                    definition?.Label ?? "Cultural value", detail);
            }
            foreach (CACulturePractice practice in culture.inheritedPractices
                .Concat(culture.practices)
                .OrderByDescending(item => item.strength))
            {
                CACulturalPracticeDef practiceDef =
                    CACulturalPracticeRegistry.Find(practice.practiceKey);
                DrawFact(ref y, view.width,
                    practiceDef?.Label ?? "Practice",
                    PracticeStrengthWords(practice.strength) + " Activity: "
                    + (practiceDef?.Activity ?? "Not available")
                    + ". This comes from the population's history and can "
                    + "affect settlements and later Culture changes.");
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
            if (latest == null) return "No change yet";
            return (latest.summary ?? latest.cause ?? "Culture changed")
                + (latest.tick >= 0 ? " on "
                    + GenDate.DateFullStringAt(
                        GenDate.TickGameToAbs(latest.tick), Vector2.zero)
                    : "");
        }

        private static string PracticeStrengthWords(int strength)
        {
            return strength >= 75 ? "Widely practiced."
                : strength >= 40 ? "Regularly practiced."
                : "Occasionally practiced.";
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
            string[] labels = CACultureQuestionRegistry
                .AdaptersForSocialSubject(value?.sourceKey)
                .Select(adapter => CACultureQuestionRegistry
                    .Find(adapter.QuestionKey)?.Label)
                .Where(label => !label.NullOrEmpty())
                .Distinct(StringComparer.Ordinal).ToArray();
            return labels.Length == 0 ? "unmapped historical evidence"
                : string.Join("; ", labels);
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
            doCloseX = false;
            doWindowBackground = false;
            absorbInputAroundWindow = true;
        }

        protected override float Margin => 0f;

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.DrawBoxSolid(inRect, CAOpeningTheme.Ink);
            CAOpeningTheme.SurfacePanel(inRect);
            Rect inner = inRect.ContractedBy(14f);
            GUI.color = CAOpeningTheme.TextHi;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 30f),
                "Culture name");
            GUI.color = Color.white;
            value = Widgets.TextField(new Rect(inner.x, inner.y + 36f,
                inner.width, 30f), value);
            if (CAOpeningTheme.GhostButton(new Rect(inner.x,
                    inner.yMax - 34f, 110f, 32f), "Cancel"))
                Close();
            if (CAOpeningTheme.PrimaryButton(new Rect(inner.xMax - 120f,
                    inner.yMax - 34f, 120f, 32f), "Save"))
            {
                if (!value.NullOrEmpty())
                    culture.Choose(CACulture.NameField, value.Trim());
                changed?.Invoke();
                Close();
            }
        }
    }

}
