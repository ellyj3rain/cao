using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    internal static class CAPoliticalEvidenceMap
    {
        internal static bool TryFor(CASocialFactContext fact,
            out string axis, out string option, out float support)
        {
            axis = null;
            option = null;
            support = 0f;
            if (fact == null) return false;
            if (fact.SubjectKey == CASocialSubjectRegistry.PublicVoice)
            {
                axis = CAFactionAxes.Participation;
                option = "universal";
            }
            else if (fact.SubjectKey == CASocialSubjectRegistry.QuarterGiven)
            {
                axis = CAFactionAxes.WarConduct;
                option = "quarter";
            }
            else if (fact.SubjectKey
                == CASocialSubjectRegistry.SharedProvision)
            {
                axis = CAFactionAxes.Support;
                option = "public";
            }
            else if (fact.SubjectKey
                == CASocialSubjectRegistry.CompelledService)
            {
                axis = CAFactionAxes.Work;
                option = "duty";
            }
            else if (fact.SubjectKey
                == CASocialSubjectRegistry.EnforcedOrder)
            {
                axis = CAFactionAxes.LocalOrder;
                option = "constabulary";
            }
            if (axis == null) return false;
            support = fact.Realization < 0 ? -1f : 1f;
            return true;
        }
    }

    public sealed class CAPoliticalOptionSupport : IExposable
    {
        public string optionKey;
        public float support;

        public void ExposeData()
        {
            Scribe_Values.Look(ref optionKey, "optionKey");
            Scribe_Values.Look(ref support, "support", 0f);
        }
    }

    public sealed class CAPawnPoliticalAttitude : IExposable
    {
        public int pawnId = -1;
        public string axisKey;
        public List<CAPoliticalOptionSupport> options =
            new List<CAPoliticalOptionSupport>();
        public float salience;
        public float confidence;
        public float moralConviction;
        public float identityCentrality;
        public float perceivedMajority;
        public float publicExpression;
        public float materialInterest;
        public string preferredOptionKey;
        public string coalitionIdentity;
        public List<string> evidenceHistory = new List<string>();
        public string provenance;
        public int lastUpdatedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref axisKey, "axisKey");
            Scribe_Collections.Look(ref options, "options", LookMode.Deep);
            Scribe_Values.Look(ref salience, "salience", 0f);
            Scribe_Values.Look(ref confidence, "confidence", 0f);
            Scribe_Values.Look(ref moralConviction, "moralConviction", 0f);
            Scribe_Values.Look(ref identityCentrality,
                "identityCentrality", 0f);
            Scribe_Values.Look(ref perceivedMajority,
                "perceivedMajority", 0f);
            Scribe_Values.Look(ref publicExpression,
                "publicExpression", 0f);
            Scribe_Values.Look(ref materialInterest,
                "materialInterest", 0f);
            Scribe_Values.Look(ref preferredOptionKey,
                "preferredOptionKey");
            Scribe_Values.Look(ref coalitionIdentity,
                "coalitionIdentity");
            Scribe_Collections.Look(ref evidenceHistory,
                "evidenceHistory", LookMode.Value);
            Scribe_Values.Look(ref provenance, "provenance");
            Scribe_Values.Look(ref lastUpdatedTick, "lastUpdatedTick", -1);
        }
    }

    public sealed class CAPoliticalIssueLink : IExposable
    {
        public string factionBoundary;
        public string leftIssueKey;
        public string rightIssueKey;
        public float learnedCorrelation;
        public float identityAttachment;
        public float constraint;
        public int observations;
        public string evidenceSignature;
        public int lastUpdatedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionBoundary, "factionBoundary");
            Scribe_Values.Look(ref leftIssueKey, "leftIssueKey");
            Scribe_Values.Look(ref rightIssueKey, "rightIssueKey");
            Scribe_Values.Look(ref learnedCorrelation,
                "learnedCorrelation", 0f);
            Scribe_Values.Look(ref identityAttachment,
                "identityAttachment", 0f);
            Scribe_Values.Look(ref constraint, "constraint", 0f);
            Scribe_Values.Look(ref observations, "observations", 0);
            Scribe_Values.Look(ref evidenceSignature,
                "evidenceSignature");
            Scribe_Values.Look(ref lastUpdatedTick, "lastUpdatedTick", -1);
        }
    }

    public sealed class CAPoliticalCoalitionRecord : IExposable
    {
        public string identity;
        public string organizationIdentity;
        public string factionBoundary;
        public string settlementBoundary;
        public List<int> memberPawnIds = new List<int>();
        public List<string> issueKeys = new List<string>();
        public List<string> grievanceKeys = new List<string>();
        public List<string> institutionalGoals = new List<string>();
        public int leaderPawnId = -1;
        public float perceivedEfficacy;
        public float cohesion;
        public float publicSupport;
        public int formedTick = -1;
        public int lastUpdatedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref identity, "identity");
            Scribe_Values.Look(ref organizationIdentity,
                "organizationIdentity");
            Scribe_Values.Look(ref factionBoundary, "factionBoundary");
            Scribe_Values.Look(ref settlementBoundary,
                "settlementBoundary");
            Scribe_Collections.Look(ref memberPawnIds, "memberPawnIds",
                LookMode.Value);
            Scribe_Collections.Look(ref issueKeys, "issueKeys",
                LookMode.Value);
            Scribe_Collections.Look(ref grievanceKeys, "grievanceKeys",
                LookMode.Value);
            Scribe_Collections.Look(ref institutionalGoals,
                "institutionalGoals", LookMode.Value);
            Scribe_Values.Look(ref leaderPawnId, "leaderPawnId", -1);
            Scribe_Values.Look(ref perceivedEfficacy,
                "perceivedEfficacy", 0f);
            Scribe_Values.Look(ref cohesion, "cohesion", 0f);
            Scribe_Values.Look(ref publicSupport, "publicSupport", 0f);
            Scribe_Values.Look(ref formedTick, "formedTick", -1);
            Scribe_Values.Look(ref lastUpdatedTick, "lastUpdatedTick", -1);
        }
    }

    // Political cognition is a separate durable owner. Culture, psychology,
    // Ideoligion, interests, experience, threat, and knowledge are evidence;
    // current law and institutional structure remain separate facts.
    public sealed class CAPoliticalCognitionWorldComponent : WorldComponent
    {
        public const int CurrentSchemaVersion = 1;
        private int campaignSchemaVersion =
            CACampaignCompatibilityKernel.CurrentBoundaryVersion;
        private int schemaVersion = CurrentSchemaVersion;
        private int nextPoliticalTick = 2500;
        private int nextCoalitionTick = 60000;
        private int pawnCursor;
        private List<CAPawnPoliticalAttitude> politicalAttitudes =
            new List<CAPawnPoliticalAttitude>();
        private List<CAPoliticalIssueLink> issueLinks =
            new List<CAPoliticalIssueLink>();
        private List<CAPoliticalCoalitionRecord> coalitions =
            new List<CAPoliticalCoalitionRecord>();
        private Dictionary<string, CAPawnPoliticalAttitude>
            attitudeByIdentity;
        private Dictionary<int, List<CAPawnPoliticalAttitude>>
            attitudesByPawn;
        private Dictionary<string, List<CAPawnPoliticalAttitude>>
            attitudesByFactionAxis;
        private Dictionary<int, Pawn> livePawnById;
        private Dictionary<int, CAOrganization> organizationByMember;
        private Dictionary<string, List<CAPoliticalIssueLink>>
            issueLinksByFactionIssue;

        public CAPoliticalCognitionWorldComponent(World world) : base(world) { }

        internal static CAPoliticalCognitionWorldComponent Current =>
            Find.World?.GetComponent<CAPoliticalCognitionWorldComponent>();

        internal IReadOnlyList<CAPawnPoliticalAttitude> PoliticalAttitudes =>
            politicalAttitudes;
        internal IReadOnlyList<CAPoliticalIssueLink> IssueLinks => issueLinks;
        internal IReadOnlyList<CAPoliticalCoalitionRecord> Coalitions =>
            coalitions;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_politicalCognitionOwnerVersion",
                0, forceSave: true);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "world.political-cognition", campaignSchemaVersion, 0);
            if (readable)
            {
                Scribe_Values.Look(ref schemaVersion,
                    "CA_politicalCognitionSchemaVersion",
                    CurrentSchemaVersion);
                Scribe_Values.Look(ref nextPoliticalTick,
                    "CA_politicalCognitionNextTick", 2500);
                Scribe_Values.Look(ref nextCoalitionTick,
                    "CA_politicalCoalitionNextTick", 60000);
                Scribe_Values.Look(ref pawnCursor,
                    "CA_politicalCognitionPawnCursor", 0);
                Scribe_Collections.Look(ref politicalAttitudes,
                    "CA_pawnPoliticalAttitudes", LookMode.Deep);
                Scribe_Collections.Look(ref issueLinks,
                    "CA_politicalIssueLinks", LookMode.Deep);
                Scribe_Collections.Look(ref coalitions,
                    "CA_politicalCoalitions", LookMode.Deep);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.political-cognition", ref campaignSchemaVersion,
                    0, ValidateCampaignState, BootstrapMissingState);
                RebuildRuntimeIndexes();
            }
            base.ExposeData();
        }

        private string BootstrapMissingState()
        {
            if (schemaVersion == 0) schemaVersion = CurrentSchemaVersion;
            if (politicalAttitudes == null)
                politicalAttitudes = new List<CAPawnPoliticalAttitude>();
            if (issueLinks == null)
                issueLinks = new List<CAPoliticalIssueLink>();
            if (coalitions == null)
                coalitions = new List<CAPoliticalCoalitionRecord>();
            return ValidateCampaignState();
        }

        private string ValidateCampaignState()
        {
            if (schemaVersion != CurrentSchemaVersion)
                return "political-cognition schema is " + schemaVersion
                    + ", expected " + CurrentSchemaVersion;
            if (politicalAttitudes == null || issueLinks == null
                || coalitions == null)
                return "political-cognition collection is missing";
            if (politicalAttitudes.Any(value => value == null
                    || value.pawnId < 0 || value.axisKey.NullOrEmpty()
                    || CAFactionAxes.AxisDef(value.axisKey) == null
                    || value.options == null || value.evidenceHistory == null
                    || value.evidenceHistory.Count
                        > CACulturalCognitionPureKernel
                            .MaxPoliticalEvidenceHistory
                    || value.evidenceHistory.Any(item => item == null)
                    || value.options.Any(option => option == null
                        || option.optionKey.NullOrEmpty()
                        || !Signed(option.support))
                    || !CanonicalOptions(value)
                    || !Unit(value.salience) || !Unit(value.confidence)
                    || !Unit(value.moralConviction)
                    || !Unit(value.identityCentrality)
                    || !Signed(value.perceivedMajority)
                    || !Signed(value.publicExpression)
                    || !Signed(value.materialInterest)))
                return "a pawn political attitude is incomplete";
            if (politicalAttitudes.GroupBy(value => value.pawnId + "\0"
                        + value.axisKey, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
                return "a pawn political attitude is duplicated";
            if (issueLinks.Any(value => value == null
                    || value.factionBoundary.NullOrEmpty()
                    || value.leftIssueKey.NullOrEmpty()
                    || value.rightIssueKey.NullOrEmpty()
                    || !CanonicalIssueKey(value.leftIssueKey)
                    || !CanonicalIssueKey(value.rightIssueKey)
                    || string.CompareOrdinal(value.leftIssueKey,
                        value.rightIssueKey) >= 0
                    || !Signed(value.learnedCorrelation)
                    || !Unit(value.identityAttachment)
                    || !Unit(value.constraint) || value.observations < 3
                    || value.evidenceSignature.NullOrEmpty()))
                return "a political issue link is incomplete";
            if (issueLinks.GroupBy(value => value.factionBoundary + "\0"
                        + value.leftIssueKey + "\0" + value.rightIssueKey,
                        StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
                return "a political issue link is duplicated";
            if (issueLinks.GroupBy(value => value.factionBoundary,
                    StringComparer.Ordinal).Any(group => group.Count()
                    > CACulturalCognitionPureKernel
                        .MaxPoliticalIssueLinksPerFaction))
                return "a faction has too many political issue links";
            if (coalitions.Any(value => value == null
                    || value.identity.NullOrEmpty()
                    || value.factionBoundary.NullOrEmpty()
                    || value.memberPawnIds == null || value.issueKeys == null
                    || value.grievanceKeys == null
                    || value.institutionalGoals == null
                    || value.memberPawnIds.Any(id => id < 0)
                    || value.memberPawnIds.Distinct().Count()
                        != value.memberPawnIds.Count
                    || value.issueKeys.Any(key => key.NullOrEmpty()
                        || !CanonicalIssueKey(key))
                    || value.leaderPawnId < -1
                    || !Unit(value.perceivedEfficacy)
                    || !Unit(value.cohesion)
                    || !Unit(value.publicSupport)))
                return "a political coalition is incomplete";
            if (coalitions.GroupBy(value => value.identity,
                    StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "a political coalition identity is duplicated";
            return null;
        }

        private static bool CanonicalOptions(CAPawnPoliticalAttitude value)
        {
            CAAxisDef axis = CAFactionAxes.AxisDef(value?.axisKey);
            if (axis == null || value?.options == null) return false;
            string[] actual = value.options.Where(item => item != null)
                .Select(item => item.optionKey).OrderBy(item => item,
                    StringComparer.Ordinal).ToArray();
            string[] expected = axis.Options.Select(item => item.Key)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            return actual.Length == actual.Distinct(
                    StringComparer.Ordinal).Count()
                && actual.SequenceEqual(expected, StringComparer.Ordinal);
        }

        private static bool CanonicalIssueKey(string value)
        {
            if (value.NullOrEmpty()) return false;
            int split = value.IndexOf(':');
            if (split <= 0 || split >= value.Length - 1) return false;
            CAAxisDef axis = CAFactionAxes.AxisDef(value.Substring(0, split));
            string option = value.Substring(split + 1);
            return axis?.Options?.Any(item => item.Key == option) == true;
        }

        public override void WorldComponentTick()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now >= nextPoliticalTick)
            {
                nextPoliticalTick = now + 2500;
                PoliticalTick(now);
            }
            if (now >= nextCoalitionTick)
            {
                nextCoalitionTick = now + 60000;
                CoalitionTick(now);
            }
        }

        private void PoliticalTick(int now)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.PoliticalCognition))
            {
                IReadOnlyList<Pawn> pawns = LivePawnSnapshot();
                if (pawns.Count == 0) return;
                EnsureRuntimeIndexes();
                int examined = Math.Min(12, pawns.Count);
                int processed = 0;
                for (int offset = 0; offset < examined; offset++)
                {
                    Pawn pawn = pawns[(pawnCursor + offset) % pawns.Count];
                    if (pawn == null || pawn.DestroyedOrNull()
                        || !pawn.RaceProps.Humanlike) continue;
                    EnsurePoliticalAttitudes(pawn, now);
                    processed++;
                }
                pawnCursor = (pawnCursor + examined) % pawns.Count;
                CAModuleProfiler.Observe(
                    CAModuleProfileKey.PoliticalCognition,
                    objectsExamined: examined,
                    candidatesAccepted: processed);
            }
        }

        private void CoalitionTick(int now)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.PoliticalCoalition))
            {
                List<Pawn> livePawns = LivePawns();
                HashSet<int> live = livePawns.Select(value =>
                    value.thingIDNumber).ToHashSet();
                politicalAttitudes.RemoveAll(value => value == null
                    || !live.Contains(value.pawnId));
                RebuildRuntimeIndexes(livePawns);
                SyncPerceivedMajorities(now);
                SyncIssueLinks(now);
                SyncCoalitions(now);
                RebuildRuntimeIndexes(livePawns);
                CAModuleProfiler.Observe(
                    CAModuleProfileKey.PoliticalCoalition,
                    objectsExamined: politicalAttitudes.Count,
                    candidatesAccepted: coalitions.Count);
            }
        }

        private void EnsurePoliticalAttitudes(Pawn pawn, int now)
        {
            if (pawn == null) return;
            EnsureRuntimeIndexes();
            livePawnById[pawn.thingIDNumber] = pawn;
            CAPoliticalBeliefs beliefs = CAPoliticalBeliefPractice
                .PoliticalBeliefsOf(pawn);
            CAPsychologicalProfile psychology =
                CACulturalCognitionWorldComponent.Current?.ProfileFor(pawn)
                ?? new CAPsychologicalProfile();
            Dictionary<string, float> priorSupports = AttitudesForPawn(
                    pawn.thingIDNumber)
                .SelectMany(value => (value.options
                        ?? new List<CAPoliticalOptionSupport>())
                    .Where(option => option != null).Select(option =>
                        new KeyValuePair<string, float>(value.axisKey + ":"
                            + option.optionKey, option.support)))
                .GroupBy(value => value.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.Last().Value, StringComparer.Ordinal);
            foreach (CAAxisDef axis in CAFactionAxes.Axes)
            {
                CAPawnPoliticalAttitude state = AttitudeFor(
                    pawn.thingIDNumber, axis.Key);
                if (state == null)
                {
                    state = new CAPawnPoliticalAttitude
                    {
                        pawnId = pawn.thingIDNumber,
                        axisKey = axis.Key,
                        options = axis.Options.Select(option =>
                            new CAPoliticalOptionSupport
                            {
                                optionKey = option.Key
                            }).ToList(),
                        evidenceHistory = new List<string>(),
                        provenance = "pawn evidence; current order excluded"
                    };
                    politicalAttitudes.Add(state);
                    IndexAttitude(state, pawn);
                }
                if (state.evidenceHistory == null)
                    state.evidenceHistory = new List<string>();
                float threat = Mathf.Clamp01(
                    psychology.dynamicState?.groupThreat ?? 0f);
                string previous = state.preferredOptionKey;
                float best = float.MinValue;
                string bestKey = null;
                float bestConfidence = 0f;
                float bestConviction = 0f;
                float bestExpression = 0f;
                float bestMaterial = 0f;
                float bestSalience = 0f;
                float bestIdentity = 0f;
                foreach (CAPoliticalOptionSupport option in state.options)
                {
                    bool held = CAFactionAxes.HasOption(beliefs?.positions,
                        axis.Key, option.optionKey);
                    PoliticalCultureEvidence culture = CultureEvidence(pawn,
                        axis.Key, option.optionKey);
                    float psychologySupport = PsychologicalSupport(
                        psychology, axis.Key, option.optionKey);
                    float material = MaterialInterest(pawn, axis.Key,
                        option.optionKey);
                    float majority = PerceivedOptionSupport(pawn, axis.Key,
                        option.optionKey, now);
                    PoliticalKnowledgeEvidence knowledge =
                        PoliticalKnowledge(pawn, axis.Key,
                            option.optionKey, culture.Confidence,
                            psychology);
                    float network = IssueNetworkSupport(pawn, axis.Key,
                        option.optionKey, priorSupports);
                    CAPoliticalFormationResult formed =
                        CACulturalCognitionPureKernel.FormPoliticalAttitude(
                            new CAPoliticalFormationInput(
                                culture.Support,
                                psychologySupport,
                                culture.Doctrine,
                                material,
                                InstitutionalExperience(pawn, axis.Key,
                                    option.optionKey),
                                ThreatSupport(axis.Key, option.optionKey,
                                    threat),
                                held ? 1f : 0f,
                                knowledge.Support,
                                network,
                                knowledge.Confidence,
                                culture.Salience,
                                culture.IdentityCentrality,
                                majority,
                                culture.ExpectedEnforcement,
                                psychology.reactance));
                    option.support = formed.Support;
                    if (option.support <= best) continue;
                    best = option.support;
                    bestKey = option.optionKey;
                    bestConfidence = formed.Confidence;
                    bestConviction = formed.MoralConviction;
                    bestExpression = formed.PublicExpression;
                    bestMaterial = material;
                    bestSalience = culture.Salience;
                    bestIdentity = culture.IdentityCentrality;
                }
                state.preferredOptionKey = bestKey;
                state.confidence = bestConfidence;
                state.moralConviction = bestConviction;
                state.publicExpression = bestExpression;
                state.materialInterest = bestMaterial;
                state.perceivedMajority = PerceivedOptionSupport(pawn,
                    axis.Key, bestKey, now);
                state.salience = Mathf.Clamp01(bestSalience * 0.65f
                    + Math.Abs(best) * 0.35f);
                state.identityCentrality = Mathf.Clamp01(
                    bestIdentity * 0.65f
                    + psychology.groupIdentification * 0.35f);
                state.lastUpdatedTick = now;
                if (previous != bestKey)
                {
                    state.evidenceHistory.Add("tick " + now + ": "
                        + (previous ?? "unset") + " -> "
                        + (bestKey ?? "unset")
                        + " from Culture, psychology, belief, interest, "
                        + "experience, threat, and knowledge");
                    if (state.evidenceHistory.Count
                        > CACulturalCognitionPureKernel
                            .MaxPoliticalEvidenceHistory)
                        state.evidenceHistory.RemoveAt(0);
                }
            }
        }

        private void SyncPerceivedMajorities(int now)
        {
            Dictionary<int, Pawn> pawns = LivePawns().ToDictionary(value =>
                value.thingIDNumber);
            foreach (CAPawnPoliticalAttitude state in politicalAttitudes
                .Where(value => value != null))
            {
                if (!pawns.TryGetValue(state.pawnId, out Pawn pawn)) continue;
                state.perceivedMajority = PerceivedOptionSupport(pawn,
                    state.axisKey, state.preferredOptionKey, now);
            }
        }

        private void SyncIssueLinks(int now)
        {
            Dictionary<int, Pawn> pawns = LivePawns().ToDictionary(value =>
                value.thingIDNumber);
            var liveBoundaries = new HashSet<string>(StringComparer.Ordinal);
            foreach (IGrouping<string, CAPawnPoliticalAttitude> faction in
                politicalAttitudes.Where(value => value != null
                        && pawns.ContainsKey(value.pawnId)
                        && !value.preferredOptionKey.NullOrEmpty())
                    .GroupBy(value => FactionBoundary(pawns[value.pawnId]),
                        StringComparer.Ordinal))
            {
                liveBoundaries.Add(faction.Key);
                Dictionary<int, Dictionary<string, CAPawnPoliticalAttitude>>
                    byPawn = faction.GroupBy(value => value.pawnId)
                        .ToDictionary(group => group.Key, group => group
                            .GroupBy(value => value.axisKey,
                                StringComparer.Ordinal)
                            .ToDictionary(axis => axis.Key,
                                axis => axis.First(),
                                StringComparer.Ordinal));
                int[] pawnIds = byPawn.Keys.OrderBy(value => value).ToArray();
                if (pawnIds.Length < CACulturalCognitionPureKernel
                        .MinPoliticalIssueLinkObservations)
                {
                    issueLinks.RemoveAll(value => value != null
                        && value.factionBoundary == faction.Key);
                    continue;
                }
                var candidates = new List<(string Left, string Right,
                    float Correlation, float Attachment, float Constraint,
                    string Signature)>();
                int testedHypotheses = 0;
                for (int leftAxis = 0;
                    leftAxis < CAFactionAxes.Axes.Length; leftAxis++)
                for (int rightAxis = leftAxis + 1;
                    rightAxis < CAFactionAxes.Axes.Length; rightAxis++)
                    testedHypotheses += CAFactionAxes.Axes[leftAxis]
                        .Options.Length * CAFactionAxes.Axes[rightAxis]
                        .Options.Length;
                for (int leftAxis = 0;
                    leftAxis < CAFactionAxes.Axes.Length; leftAxis++)
                {
                    CAAxisDef leftDefinition =
                        CAFactionAxes.Axes[leftAxis];
                    for (int rightAxis = leftAxis + 1;
                        rightAxis < CAFactionAxes.Axes.Length; rightAxis++)
                    {
                        CAAxisDef rightDefinition =
                            CAFactionAxes.Axes[rightAxis];
                        foreach (CAAxisOption leftOption in
                            leftDefinition.Options)
                        foreach (CAAxisOption rightOption in
                            rightDefinition.Options)
                        {
                            var leftValues = new List<float>(pawnIds.Length);
                            var rightValues = new List<float>(pawnIds.Length);
                            float attachment = 0f;
                            bool complete = true;
                            foreach (int pawnId in pawnIds)
                            {
                                if (!byPawn[pawnId].TryGetValue(
                                        leftDefinition.Key,
                                        out CAPawnPoliticalAttitude left)
                                    || !byPawn[pawnId].TryGetValue(
                                        rightDefinition.Key,
                                        out CAPawnPoliticalAttitude right))
                                {
                                    complete = false;
                                    break;
                                }
                                leftValues.Add(left.options.First(value =>
                                    value.optionKey == leftOption.Key)
                                    .support);
                                rightValues.Add(right.options.First(value =>
                                    value.optionKey == rightOption.Key)
                                    .support);
                                attachment += (left.identityCentrality
                                    + right.identityCentrality) * 0.5f;
                            }
                            if (!complete) continue;
                            float correlation = CACulturalCognitionPureKernel
                                .PearsonCorrelation(leftValues, rightValues);
                            if (!CACulturalCognitionPureKernel
                                .EligiblePoliticalIssueLink(correlation,
                                        pawnIds.Length, testedHypotheses))
                                continue;
                            correlation = CACulturalCognitionPureKernel
                                .ShrunkPoliticalIssueCorrelation(correlation,
                                    pawnIds.Length, testedHypotheses);
                            attachment = Mathf.Clamp01(attachment
                                / pawnIds.Length);
                            float constraint = Mathf.Clamp01(
                                Math.Abs(correlation) * attachment
                                * CACulturalCognitionPureKernel
                                    .PoliticalIssueEvidenceWeight(
                                        pawnIds.Length));
                            string leftKey = leftDefinition.Key + ":"
                                + leftOption.Key;
                            string rightKey = rightDefinition.Key + ":"
                                + rightOption.Key;
                            if (string.CompareOrdinal(leftKey, rightKey) > 0)
                            {
                                string swap = leftKey;
                                leftKey = rightKey;
                                rightKey = swap;
                            }
                            string signature = CASocialPatternKernel
                                .StableHash(string.Join("|", pawnIds.Select(
                                    (id, index) => id + ":"
                                        + leftValues[index].ToString("R",
                                            System.Globalization
                                                .CultureInfo.InvariantCulture)
                                        + ":" + rightValues[index]
                                            .ToString("R",
                                                System.Globalization
                                                    .CultureInfo
                                                    .InvariantCulture))));
                            candidates.Add((leftKey, rightKey, correlation,
                                attachment, constraint, signature));
                        }
                    }
                }
                var selected = candidates.OrderByDescending(
                        value => value.Constraint)
                    .ThenBy(value => value.Left, StringComparer.Ordinal)
                    .ThenBy(value => value.Right, StringComparer.Ordinal)
                    .Take(CACulturalCognitionPureKernel
                        .MaxPoliticalIssueLinksPerFaction).ToArray();
                var selectedKeys = new HashSet<string>(selected.Select(value =>
                    value.Left + "\0" + value.Right), StringComparer.Ordinal);
                issueLinks.RemoveAll(value => value != null
                    && value.factionBoundary == faction.Key
                    && !selectedKeys.Contains(value.leftIssueKey + "\0"
                        + value.rightIssueKey));
                foreach (var candidate in selected)
                {
                    CAPoliticalIssueLink link = issueLinks.FirstOrDefault(
                        value => value != null
                            && value.factionBoundary == faction.Key
                            && value.leftIssueKey == candidate.Left
                            && value.rightIssueKey == candidate.Right);
                    if (link == null)
                    {
                        link = new CAPoliticalIssueLink
                        {
                            factionBoundary = faction.Key,
                            leftIssueKey = candidate.Left,
                            rightIssueKey = candidate.Right,
                            learnedCorrelation = candidate.Correlation
                        };
                        issueLinks.Add(link);
                    }
                    else if (link.evidenceSignature != candidate.Signature)
                        link.learnedCorrelation = Mathf.Lerp(
                            link.learnedCorrelation,
                            candidate.Correlation, 0.35f);
                    link.identityAttachment = candidate.Attachment;
                    link.constraint = Mathf.Clamp01(
                        Math.Abs(link.learnedCorrelation)
                        * candidate.Attachment
                        * CACulturalCognitionPureKernel
                            .PoliticalIssueEvidenceWeight(pawnIds.Length));
                    link.observations = pawnIds.Length;
                    if (link.evidenceSignature != candidate.Signature)
                        link.lastUpdatedTick = now;
                    link.evidenceSignature = candidate.Signature;
                }
                CAPoliticalIssueLink[] overflow = issueLinks.Where(value =>
                        value != null
                        && value.factionBoundary == faction.Key)
                    .OrderByDescending(value => value.constraint)
                    .ThenByDescending(value => value.lastUpdatedTick)
                    .Skip(CACulturalCognitionPureKernel
                        .MaxPoliticalIssueLinksPerFaction).ToArray();
                if (overflow.Length > 0)
                    issueLinks.RemoveAll(overflow.Contains);
            }
            issueLinks.RemoveAll(value => value == null
                || !liveBoundaries.Contains(value.factionBoundary));
        }

        private void SyncCoalitions(int now)
        {
            List<Pawn> livePawns = LivePawns();
            Dictionary<int, Pawn> pawns = livePawns.ToDictionary(value =>
                value.thingIDNumber);
            var liveIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (IGrouping<string, CAPawnPoliticalAttitude> issue in
                politicalAttitudes.Where(value => value != null
                        && pawns.ContainsKey(value.pawnId)
                        && !value.preferredOptionKey.NullOrEmpty()
                        && SupportOf(value) >= 0.55f)
                    .GroupBy(value => FactionBoundary(pawns[value.pawnId])
                        + "\0" + value.axisKey + "\0"
                        + value.preferredOptionKey, StringComparer.Ordinal))
            {
                CAPawnPoliticalAttitude[] members = issue
                    .GroupBy(value => value.pawnId)
                    .Select(group => group.First()).ToArray();
                if (members.Length < 2) continue;
                string[] parts = issue.Key.Split('\0');
                string identity = "coalition:" + parts[0] + ":"
                    + parts[1] + ":" + parts[2];
                liveIdentities.Add(identity);
                CAPoliticalCoalitionRecord coalition = coalitions
                    .FirstOrDefault(value => value != null
                        && value.identity == identity);
                if (coalition == null)
                {
                    coalition = new CAPoliticalCoalitionRecord
                    {
                        identity = identity,
                        factionBoundary = parts[0],
                        formedTick = now
                    };
                    coalitions.Add(coalition);
                }
                coalition.memberPawnIds = members.Select(value =>
                    value.pawnId).OrderBy(value => value).ToList();
                coalition.issueKeys = new List<string>
                    { parts[1] + ":" + parts[2] };
                coalition.grievanceKeys = members.Where(value =>
                        OrganizationLegitimacy(pawns[value.pawnId]) < 0.5f)
                    .Select(value => "institutional mismatch:"
                        + value.axisKey).Distinct().ToList();
                coalition.institutionalGoals = new List<string>
                    { "change " + parts[1] + " toward " + parts[2] };
                coalition.cohesion = members.Average(SupportOf);
                int boundaryPopulation = livePawns.Count(value =>
                    FactionBoundary(value) == parts[0]);
                coalition.publicSupport = members.Length
                    / (float)Math.Max(1, boundaryPopulation);
                coalition.leaderPawnId = members.Select(value =>
                        pawns[value.pawnId])
                    .OrderByDescending(value =>
                        value.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)
                    .ThenBy(value => value.thingIDNumber).First()
                    .thingIDNumber;
                coalition.perceivedEfficacy = Mathf.Clamp01(
                    coalition.publicSupport * 0.55f
                    + coalition.cohesion * 0.30f
                    + (coalition.leaderPawnId >= 0 ? 0.15f : 0f));
                coalition.organizationIdentity = CoalitionOrganization(
                    coalition.memberPawnIds);
                string[] settlements = members.Select(value =>
                        SettlementBoundary(pawns[value.pawnId]))
                    .Distinct().ToArray();
                coalition.settlementBoundary = settlements.Length == 1
                    ? settlements[0] : "faction-wide";
                coalition.lastUpdatedTick = now;
                foreach (CAPawnPoliticalAttitude member in members)
                    member.coalitionIdentity = identity;
            }
            coalitions.RemoveAll(value => value == null
                || !liveIdentities.Contains(value.identity));
            foreach (CAPawnPoliticalAttitude attitude in politicalAttitudes
                .Where(value => value != null
                    && !value.coalitionIdentity.NullOrEmpty()
                    && !liveIdentities.Contains(value.coalitionIdentity)))
                attitude.coalitionIdentity = null;
        }

        private float PerceivedOptionSupport(Pawn pawn, string axis,
            string option, int now)
        {
            if (pawn == null || axis.NullOrEmpty() || option.NullOrEmpty())
                return 0f;
            CACulturalCognitionWorldComponent cognition =
                CACulturalCognitionWorldComponent.Current;
            IReadOnlyList<CASocialInfluenceEdge> edges = cognition
                ?.InfluenceEdgesForTarget(pawn.thingIDNumber);
            List<(float support, float weight)> neighborhood =
                (edges ?? Array.Empty<CASocialInfluenceEdge>())
                .Where(value => value != null
                    && cognition.HasRecentPoliticalExposure(value, axis,
                        now))
                .Select(value =>
                {
                    CAPawnPoliticalAttitude source = AttitudeFor(
                        value.sourcePawnId, axis);
                    float support = source?.options?.FirstOrDefault(choice =>
                        choice != null && choice.optionKey == option)
                        ?.support ?? 0f;
                    float weight = Mathf.Clamp01(value.weight
                        * (0.45f + value.trust * 0.35f
                            + value.conformity * 0.20f));
                    return (support, weight);
                }).Where(value => value.weight > 0f).ToList();
            if (neighborhood.Count > 0)
            {
                float total = neighborhood.Sum(value => value.weight);
                return Mathf.Clamp(neighborhood.Sum(value =>
                    value.support * value.weight) / Math.Max(0.0001f, total),
                    -1f, 1f);
            }
            // No represented observation means no perceived-majority
            // evidence. Faction membership is not omniscience.
            return 0f;
        }

        private readonly struct PoliticalCultureEvidence
        {
            public readonly float Support;
            public readonly float Doctrine;
            public readonly float Confidence;
            public readonly float Salience;
            public readonly float IdentityCentrality;
            public readonly float ExpectedEnforcement;

            public PoliticalCultureEvidence(float support, float doctrine,
                float confidence, float salience, float identityCentrality,
                float expectedEnforcement)
            {
                Support = support;
                Doctrine = doctrine;
                Confidence = confidence;
                Salience = salience;
                IdentityCentrality = identityCentrality;
                ExpectedEnforcement = expectedEnforcement;
            }
        }

        private readonly struct PoliticalKnowledgeEvidence
        {
            public readonly float Support;
            public readonly float Confidence;

            public PoliticalKnowledgeEvidence(float support,
                float confidence)
            {
                Support = support;
                Confidence = confidence;
            }
        }

        private static PoliticalKnowledgeEvidence PoliticalKnowledge(
            Pawn pawn, string axis, string option, float cultureConfidence,
            CAPsychologicalProfile psychology)
        {
            List<CAKnowledgePropositionRecord> represented =
                (CAPropositionKnowledgeWorldComponent.Current
                        ?.PoliticalPropositions("pawn:"
                            + pawn.thingIDNumber, axis)
                    ?? Array.Empty<CAKnowledgePropositionRecord>())
                .Where(value => value != null
                    && !value.politicalOptionKey.NullOrEmpty())
                .OrderByDescending(value => value.lastConfirmedTick)
                .Take(8).ToList();
            float propositionConfidence = represented.Count == 0 ? 0.5f
                : represented.Average(value => value.confidence);
            float support = 0f;
            if (represented.Count > 0)
            {
                int alternatives = Math.Max(1,
                    (CAFactionAxes.AxisDef(axis)?.Options?.Length ?? 2) - 1);
                float total = represented.Sum(value =>
                    Math.Max(0.01f, value.confidence));
                support = represented.Sum(value =>
                {
                    float direction = value.politicalOptionKey == option
                        ? value.politicalSupport
                        : -value.politicalSupport / alternatives;
                    return direction * Math.Max(0.01f, value.confidence);
                }) / total;
            }
            float uncertainty = CAPsychologyRuntime
                .UncertaintyForPoliticalAxis(psychology, axis);
            float confidence = Mathf.Clamp01(cultureConfidence * 0.45f
                + (1f - uncertainty) * 0.25f
                + propositionConfidence * 0.30f);
            return new PoliticalKnowledgeEvidence(
                Mathf.Clamp(support, -1f, 1f), confidence);
        }

        private float IssueNetworkSupport(Pawn pawn, string axis,
            string option, IReadOnlyDictionary<string, float> priorSupports)
        {
            string target = axis + ":" + option;
            string boundary = FactionBoundary(pawn);
            var samples = new List<(float OtherSupport, float Correlation,
                float Constraint)>();
            EnsureRuntimeIndexes();
            string indexKey = boundary + "\0" + target;
            if (!issueLinksByFactionIssue.TryGetValue(indexKey,
                    out List<CAPoliticalIssueLink> relevant))
                relevant = new List<CAPoliticalIssueLink>();
            foreach (CAPoliticalIssueLink link in relevant)
            {
                string other = link.leftIssueKey == target
                    ? link.rightIssueKey : link.leftIssueKey;
                if (!priorSupports.TryGetValue(other,
                        out float otherSupport)) continue;
                samples.Add((otherSupport, link.learnedCorrelation,
                    link.constraint));
            }
            return CACulturalCognitionPureKernel.PoliticalNetworkSupport(
                samples);
        }

        private static float SupportOf(CAPawnPoliticalAttitude state)
        {
            return state?.options?.FirstOrDefault(value => value != null
                && value.optionKey == state.preferredOptionKey)?.support ?? 0f;
        }

        private static PoliticalCultureEvidence CultureEvidence(Pawn pawn,
            string axis, string option)
        {
            (string Question, float Weight)[] weights = CultureWeights(axis,
                option);
            var values = new List<(CAPawnCulturalAttitude Attitude,
                float Weight, float Doctrine)>();
            foreach ((string question, float weight) in weights)
            {
                CAPawnCulturalAttitude attitude =
                    CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                        pawn, question);
                if (attitude == null) continue;
                float doctrine = CACultureIdeoligionAdapter.Pressure(
                    pawn.Ideo, question, out _);
                values.Add((attitude, weight, doctrine));
            }
            if (values.Count == 0)
                return new PoliticalCultureEvidence(0f, 0f, 0.5f, 0.5f,
                    0.5f, 0f);
            float total = values.Sum(value => Math.Abs(value.Weight));
            return new PoliticalCultureEvidence(
                Mathf.Clamp(values.Sum(value => value.Attitude.privateAttitude
                    * value.Weight) / total, -1f, 1f),
                Mathf.Clamp(values.Sum(value => value.Doctrine
                    * value.Weight) / total, -1f, 1f),
                Mathf.Clamp01(values.Sum(value =>
                    value.Attitude.knowledgeConfidence
                        * Math.Abs(value.Weight)) / total),
                Mathf.Clamp01(values.Sum(value =>
                    value.Attitude.moralConviction
                        * Math.Abs(value.Weight)) / total),
                Mathf.Clamp01(values.Sum(value =>
                    value.Attitude.identityCentrality
                        * Math.Abs(value.Weight)) / total),
                Mathf.Clamp01(values.Sum(value =>
                    value.Attitude.expectedEnforcement
                        * Math.Abs(value.Weight)) / total));
        }

        private static (string Question, float Weight)[] CultureWeights(
            string axis, string option)
        {
            string voice = CACultureQuestionRegistry.VoiceInclusion;
            string rank = CACultureQuestionRegistry.RankDifferentiation;
            string hereditary = CACultureQuestionRegistry
                .HereditaryLegitimacy;
            string coercion = CACultureQuestionRegistry.CoercionLegitimacy;
            string provision = CACultureQuestionRegistry.MutualProvision;
            string outsider = CACultureQuestionRegistry.OutsiderInclusion;
            string integration = CACultureQuestionRegistry
                .IntegrationPreference;
            string novelty = CACultureQuestionRegistry.NoveltyAcceptance;
            string captive = CACultureQuestionRegistry.CaptiveProtection;
            string kin = CACultureQuestionRegistry.KinObligation;
            string genderWork = CACultureQuestionRegistry.GenderedWork;
            string officeAccess = CACultureQuestionRegistry
                .GenderOfficeAccess;
            string mobility = CACultureQuestionRegistry.StatusMobility;
            string membership = CACultureQuestionRegistry.MembershipAccess;
            string dissent = CACultureQuestionRegistry.DissentTolerance;
            string enforcement = CACultureQuestionRegistry
                .EnforcementLegitimacy;
            string property = CACultureQuestionRegistry.PropertyControl;
            string punishment = CACultureQuestionRegistry.PunishmentSeverity;
            string retaliation = CACultureQuestionRegistry
                .RetaliatoryViolence;
            string expertise = CACultureQuestionRegistry.ExpertiseDeference;
            if (axis == CAFactionAxes.Leadership)
            {
                if (option == "single") return W((rank, 0.55f),
                    (hereditary, 0.25f), (voice, -0.35f));
                if (option == "council") return W((voice, 0.55f),
                    (rank, 0.10f), (officeAccess, 0.20f));
                if (option == "whole") return W((voice, 0.85f),
                    (rank, -0.35f));
                if (option == "federated") return W((voice, 0.45f),
                    (integration, 0.25f), (rank, 0.10f));
                return W((rank, -0.55f), (coercion, -0.30f),
                    (voice, 0.30f));
            }
            if (axis == CAFactionAxes.Decisions)
            {
                if (option == "decree") return W((voice, -0.70f),
                    (rank, 0.40f));
                if (option == "majority") return W((voice, 0.70f),
                    (integration, 0.15f));
                if (option == "consensus") return W((voice, 0.85f),
                    (integration, 0.30f), (coercion, -0.20f),
                    (expertise, -0.10f));
                return W((hereditary, 0.35f), (rank, 0.35f),
                    (novelty, -0.30f));
            }
            if (axis == CAFactionAxes.Participation)
            {
                if (option == "universal") return W((voice, 0.90f),
                    (outsider, 0.25f), (officeAccess, 0.25f));
                if (option == "members") return W((voice, 0.45f),
                    (outsider, -0.15f));
                if (option == "standing") return W((rank, 0.45f),
                    (voice, -0.20f));
                return W((hereditary, 0.35f), (voice, -0.30f));
            }
            if (axis == CAFactionAxes.Dissent)
            {
                if (option == "plural") return W((voice, 0.65f),
                    (novelty, 0.40f), (outsider, 0.25f),
                    (dissent, 0.75f));
                if (option == "majoritarian") return W((voice, 0.25f),
                    (integration, 0.15f));
                if (option == "orthodoxy") return W((novelty, -0.60f),
                    (outsider, -0.30f), (coercion, 0.25f),
                    (dissent, -0.70f));
                return W((rank, 0.40f), (hereditary, 0.25f),
                    (dissent, -0.45f));
            }
            if (axis == CAFactionAxes.Ownership)
            {
                if (option == "private") return W((property, -0.85f),
                    (provision, -0.25f), (rank, 0.20f));
                if (option == "cooperative") return W((provision, 0.55f),
                    (voice, 0.35f), (property, 0.55f));
                if (option == "common") return W((provision, 0.85f),
                    (rank, -0.20f), (property, 0.90f));
                return W((provision, 0.45f), (coercion, 0.35f),
                    (rank, 0.30f), (property, 0.20f));
            }
            if (axis == CAFactionAxes.Economy)
            {
                if (option == "market") return W((novelty, 0.40f),
                    (provision, -0.30f));
                if (option == "planned") return W((provision, 0.40f),
                    (coercion, 0.30f), (rank, 0.20f));
                return W((provision, 0.85f), (rank, -0.20f));
            }
            if (axis == CAFactionAxes.Work)
            {
                if (option == "contract") return W((coercion, -0.75f),
                    (provision, -0.15f), (genderWork, -0.20f));
                if (option == "organized") return W((voice, 0.45f),
                    (coercion, -0.30f), (provision, 0.30f));
                if (option == "duty") return W((coercion, 0.75f),
                    (provision, 0.30f), (genderWork, 0.15f));
                return W((hereditary, 0.20f), (provision, 0.25f));
            }
            if (axis == CAFactionAxes.Support)
            {
                if (option == "private") return W((provision, -0.90f),
                    (kin, 0.20f));
                if (option == "public") return W((provision, 0.65f),
                    (coercion, 0.15f));
                if (option == "communal") return W((provision, 0.90f),
                    (voice, 0.15f), (kin, 0.20f));
                return W((provision, 0.45f));
            }
            if (axis == CAFactionAxes.Membership)
            {
                if (option == "open") return W((outsider, 0.90f),
                    (integration, 0.35f), (membership, 0.85f));
                if (option == "vetted") return W((outsider, 0.10f),
                    (integration, -0.10f));
                if (option == "hereditary") return W((hereditary, 0.55f),
                    (outsider, -0.45f), (membership, -0.70f));
                return W((outsider, -0.90f), (integration, -0.40f),
                    (membership, -0.90f));
            }
            if (axis == CAFactionAxes.Status)
            {
                if (option == "equal") return W((rank, -0.90f),
                    (hereditary, -0.35f), (mobility, 0.70f));
                if (option == "earned") return W((rank, 0.25f),
                    (hereditary, -0.20f), (mobility, 0.85f));
                if (option == "hereditary") return W((rank, 0.65f),
                    (hereditary, 0.85f), (mobility, -0.85f));
                return W((rank, 0.90f), (hereditary, 0.55f));
            }
            if (axis == CAFactionAxes.LocalOrder)
            {
                if (option == "none") return W((coercion, -0.75f),
                    (rank, -0.20f), (enforcement, -0.85f));
                if (option == "watch") return W((voice, 0.40f),
                    (coercion, -0.20f));
                if (option == "constabulary") return W((coercion, 0.25f),
                    (captive, 0.15f), (enforcement, 0.60f));
                return W((rank, 0.50f), (hereditary, 0.25f),
                    (coercion, 0.50f));
            }
            if (axis == CAFactionAxes.Defense)
            {
                if (option == "none") return W((coercion, -0.35f),
                    (captive, 0.10f));
                if (option == "levy") return W((coercion, 0.55f),
                    (provision, 0.20f));
                if (option == "militia") return W((voice, 0.30f),
                    (coercion, 0.15f));
                if (option == "professional") return W((rank, 0.25f),
                    (novelty, 0.20f));
                return W((hereditary, 0.55f), (rank, 0.65f));
            }
            if (option == "strength") return W((captive, -0.90f),
                (coercion, 0.25f), (punishment, 0.45f),
                (retaliation, 0.55f));
            if (option == "combatants") return W((captive, 0.65f),
                (punishment, -0.15f), (retaliation, 0.10f));
            return W((captive, 0.90f), (punishment, -0.45f),
                (retaliation, -0.35f));
        }

        private static (string Question, float Weight)[] W(
            params (string Question, float Weight)[] values) => values;

        private static float PsychologicalSupport(
            CAPsychologicalProfile profile, string axis, string option)
        {
            profile = profile ?? new CAPsychologicalProfile();
            float openness = TraitDirection(profile.openness);
            float agree = TraitDirection(profile.agreeableness);
            float closure = TraitDirection(profile.needForClosure);
            float reactance = TraitDirection(profile.reactance);
            float status = TraitDirection(profile.statusSeeking);
            float threat = TraitDirection(profile.dangerousWorldBelief);
            float competition = TraitDirection(
                profile.competitiveWorldBelief);
            float empathy = TraitDirection(profile.empathicConcern);
            float group = TraitDirection(profile.groupIdentification);
            float diligence = TraitDirection(profile.conscientiousness);
            if (axis == CAFactionAxes.Leadership)
            {
                if (option == "single") return Mix(closure, status, threat);
                if (option == "council") return Mix(agree, group, openness);
                if (option == "whole") return Mix(agree, openness,
                    -closure);
                if (option == "federated") return Mix(openness, group,
                    agree);
                return Mix(reactance, openness, -closure);
            }
            if (axis == CAFactionAxes.Decisions)
            {
                if (option == "decree") return Mix(closure, status, threat);
                if (option == "majority") return Mix(group, agree,
                    openness);
                if (option == "consensus") return Mix(agree, openness,
                    -closure);
                return Mix(closure, group, -openness);
            }
            if (axis == CAFactionAxes.Participation
                || axis == CAFactionAxes.Dissent)
            {
                bool open = option == "universal" || option == "members"
                    || option == "plural" || option == "majoritarian";
                return open ? Mix(openness, agree, group)
                    : Mix(closure, status, -openness);
            }
            if (axis == CAFactionAxes.Ownership
                || axis == CAFactionAxes.Economy
                || axis == CAFactionAxes.Support)
            {
                bool shared = option == "cooperative" || option == "common"
                    || option == "communal" || option == "public"
                    || option == "charitable";
                return shared ? Mix(empathy, agree, group)
                    : Mix(competition, status, -empathy);
            }
            if (axis == CAFactionAxes.Work)
            {
                if (option == "duty") return Mix(diligence, closure,
                    -reactance);
                if (option == "organized") return Mix(group, agree,
                    diligence);
                if (option == "contract") return Mix(reactance, openness,
                    diligence);
                return Mix(group, closure, agree);
            }
            if (axis == CAFactionAxes.Membership)
            {
                bool open = option == "open" || option == "vetted";
                return open ? Mix(openness, agree, -threat)
                    : Mix(group, closure, threat);
            }
            if (axis == CAFactionAxes.Status)
                return option == "equal" ? Mix(agree, -status, -competition)
                    : Mix(status, competition, closure);
            if (axis == CAFactionAxes.LocalOrder)
                return option == "none" ? Mix(reactance, openness, -threat)
                    : Mix(closure, threat, group);
            if (axis == CAFactionAxes.Defense)
                return option == "none" ? Mix(-threat, openness, reactance)
                    : Mix(threat, group, diligence);
            return option == "strength"
                ? Mix(competition, threat, -empathy)
                : Mix(empathy, agree, -competition);
        }

        private static float TraitDirection(float value) =>
            Mathf.Clamp(value * 2f - 1f, -1f, 1f);

        private static float Mix(float first, float second, float third) =>
            Mathf.Clamp(first * 0.45f + second * 0.35f
                + third * 0.20f, -1f, 1f);

        private float MaterialInterest(Pawn pawn, string axis,
            string option)
        {
            if (pawn == null) return 0f;
            if (axis == CAFactionAxes.Work)
            {
                int passion = pawn.skills?.skills?.Count(value =>
                    value != null && value.passion > Passion.None) ?? 0;
                float autonomy = Mathf.Clamp((passion - 2) / 6f,
                    -1f, 1f);
                return option == "contract" || option == "organized"
                    ? autonomy : -autonomy;
            }
            if (axis == CAFactionAxes.Status
                || axis == CAFactionAxes.Leadership
                || axis == CAFactionAxes.Participation)
            {
                bool office = CAOrganizationWorldComponent.Current
                    ?.HasOfficeHolder(pawn.thingIDNumber) == true;
                if (!office) return option == "equal"
                    || option == "universal" ? 0.25f : 0f;
                return option == "single" || option == "council"
                    || option == "standing" || option == "earned"
                    || option == "hereditary" ? 0.65f : -0.20f;
            }
            return 0f;
        }

        private static float ThreatSupport(string axis, string option,
            float threat)
        {
            float direction = 0f;
            if (axis == CAFactionAxes.Leadership)
                direction = option == "single" ? 1f
                    : option == "none" ? -1f : 0.25f;
            else if (axis == CAFactionAxes.Decisions)
                direction = option == "decree" ? 1f
                    : option == "consensus" ? -0.5f : 0f;
            else if (axis == CAFactionAxes.Dissent)
                direction = option == "orthodoxy" ? 1f
                    : option == "plural" ? -1f : 0f;
            else if (axis == CAFactionAxes.LocalOrder)
                direction = option == "none" ? -1f : 0.65f;
            else if (axis == CAFactionAxes.Defense)
                direction = option == "none" ? -1f : 0.75f;
            else if (axis == CAFactionAxes.WarConduct)
                direction = option == "strength" ? 0.55f : -0.20f;
            return Mathf.Clamp(threat * direction, -1f, 1f);
        }

        private float InstitutionalExperience(Pawn pawn,
            string axis, string option)
        {
            CAOrganization organization = OrganizationForPawn(pawn);
            if (organization == null) return 0f;
            string realized = ActualOptionFor(organization, axis);
            if (realized.NullOrEmpty()) return 0f;
            CAInstitutionLegitimacyAppraisal appraisal = organization
                .legitimacyAppraisals?.OrderByDescending(value =>
                    value?.lastUpdatedTick ?? -1).FirstOrDefault();
            float evaluation = (appraisal?.legitimacy ?? 0.5f) * 2f - 1f;
            int alternatives = Math.Max(1,
                (CAFactionAxes.AxisDef(axis)?.Options?.Length ?? 2) - 1);
            return realized == option ? evaluation
                : -evaluation / alternatives;
        }

        private float OrganizationLegitimacy(Pawn pawn)
        {
            CAOrganization organization = OrganizationForPawn(pawn);
            return organization?.legitimacyAppraisals?.Where(value =>
                    value != null).OrderByDescending(value =>
                    value.lastUpdatedTick).FirstOrDefault()?.legitimacy
                ?? 0.5f;
        }

        internal float InstitutionPoliticalFitFor(
            CAOrganization organization)
        {
            if (organization?.memberPawnIds == null) return 0.5f;
            EnsureRuntimeIndexes();
            var fits = new List<float>();
            foreach (string axis in new[] { CAFactionAxes.Leadership,
                CAFactionAxes.Status, CAFactionAxes.LocalOrder,
                CAFactionAxes.Defense })
            {
                string realized = ActualOptionFor(organization, axis);
                if (realized.NullOrEmpty()) continue;
                List<float> support = organization.memberPawnIds
                    .Select(id => AttitudeFor(id, axis))
                    .Where(value => value != null)
                    .Select(value => value.options?.FirstOrDefault(option =>
                        option != null && option.optionKey == realized)
                        ?.support ?? 0f).ToList();
                if (support.Count > 0)
                    fits.Add(Mathf.Clamp01((support.Average() + 1f) * 0.5f));
            }
            return fits.Count == 0 ? 0.5f : fits.Average();
        }

        private static string ActualOptionFor(CAOrganization organization,
            string axis)
        {
            if (organization == null) return null;
            if (axis == CAFactionAxes.Leadership)
            {
                int filled = organization.offices?.Count(value =>
                    value != null && value.holderId >= 0) ?? 0;
                return filled == 0 ? "none" : filled == 1
                    ? "single" : "council";
            }
            if (axis == CAFactionAxes.Status)
            {
                if (organization.offices?.Any(value => value != null
                        && (value.successionRule == "hereditary"
                            || value.kinSuccessions > 0)) == true)
                    return "hereditary";
                if (organization.offices?.Any(value => value != null
                        && value.seniority > 0) == true) return "earned";
                return "equal";
            }
            if (axis == CAFactionAxes.LocalOrder)
            {
                int guards = organization.securityPractices?.Sum(value =>
                    value?.guardPawnIds?.Count ?? 0) ?? 0;
                return guards > 0 ? "constabulary" : "none";
            }
            if (axis == CAFactionAxes.Defense)
            {
                int guards = organization.securityPractices?.Sum(value =>
                    value?.guardPawnIds?.Count ?? 0) ?? 0;
                return guards > 0 ? "professional" : "none";
            }
            return null;
        }

        private CAOrganization OrganizationForPawn(Pawn pawn)
        {
            if (pawn == null) return null;
            EnsureRuntimeIndexes();
            organizationByMember.TryGetValue(pawn.thingIDNumber,
                out CAOrganization organization);
            return organization;
        }

        private static string CoalitionOrganization(List<int> members)
        {
            CAOrganizationWorldComponent owner =
                CAOrganizationWorldComponent.Current;
            if (owner == null || members == null || members.Count == 0)
                return null;
            int required = members.Count / 2 + 1;
            return owner.Organizations.Where(value => value != null
                    && !value.organizationKey.NullOrEmpty())
                .Select(value => new
                {
                    Organization = value,
                    Count = members.Count(id => value.memberPawnIds?
                        .Contains(id) == true)
                })
                .Where(value => value.Count >= required)
                .OrderByDescending(value => value.Count)
                .ThenBy(value => value.Organization.organizationKey,
                    StringComparer.Ordinal).FirstOrDefault()
                ?.Organization.organizationKey;
        }

        private static string FactionBoundary(Pawn pawn)
        {
            return pawn?.Faction == null ? "unaffiliated"
                : "faction-" + pawn.Faction.loadID;
        }

        private static string SettlementBoundary(Pawn pawn)
        {
            return pawn?.MapHeld == null ? "regional"
                : "map-" + pawn.MapHeld.uniqueID;
        }

        private void EnsureRuntimeIndexes()
        {
            if (attitudeByIdentity == null || attitudesByPawn == null
                || attitudesByFactionAxis == null || livePawnById == null
                || organizationByMember == null
                || issueLinksByFactionIssue == null)
                RebuildRuntimeIndexes();
        }

        private void RebuildRuntimeIndexes(IEnumerable<Pawn> live = null)
        {
            List<Pawn> pawns = (live ?? LivePawns()).Where(value =>
                    value != null && value.RaceProps.Humanlike)
                .ToList();
            livePawnById = pawns.GroupBy(value => value.thingIDNumber)
                .ToDictionary(group => group.Key, group => group.First());
            attitudeByIdentity = (politicalAttitudes
                    ?? new List<CAPawnPoliticalAttitude>())
                .Where(value => value != null && value.pawnId >= 0
                    && !value.axisKey.NullOrEmpty())
                .GroupBy(value => PoliticalAttitudeIdentity(value.pawnId,
                    value.axisKey), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            attitudesByPawn = attitudeByIdentity.Values
                .GroupBy(value => value.pawnId)
                .ToDictionary(group => group.Key, group => group.ToList());
            attitudesByFactionAxis = attitudeByIdentity.Values
                .Where(value => livePawnById.ContainsKey(value.pawnId))
                .GroupBy(value => FactionBoundary(
                        livePawnById[value.pawnId]) + "\0" + value.axisKey,
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(),
                    StringComparer.Ordinal);
            organizationByMember = (CAOrganizationWorldComponent.Current
                    ?.Organizations ?? Array.Empty<CAOrganization>())
                .Where(value => value?.memberPawnIds != null)
                .SelectMany(value => value.memberPawnIds.Where(id => id >= 0)
                    .Select(id => new KeyValuePair<int, CAOrganization>(
                        id, value)))
                .GroupBy(value => value.Key)
                .ToDictionary(group => group.Key,
                    group => group.First().Value);
            issueLinksByFactionIssue = new Dictionary<string,
                List<CAPoliticalIssueLink>>(StringComparer.Ordinal);
            foreach (CAPoliticalIssueLink link in issueLinks.Where(value =>
                         value != null && !value.factionBoundary.NullOrEmpty()))
            {
                IndexIssueLink(link.factionBoundary, link.leftIssueKey, link);
                IndexIssueLink(link.factionBoundary, link.rightIssueKey, link);
            }
        }

        private void IndexIssueLink(string boundary, string issueKey,
            CAPoliticalIssueLink link)
        {
            if (boundary.NullOrEmpty() || issueKey.NullOrEmpty()
                || link == null) return;
            string key = boundary + "\0" + issueKey;
            if (!issueLinksByFactionIssue.TryGetValue(key,
                    out List<CAPoliticalIssueLink> values))
            {
                values = new List<CAPoliticalIssueLink>();
                issueLinksByFactionIssue[key] = values;
            }
            values.Add(link);
        }

        private CAPawnPoliticalAttitude AttitudeFor(int pawnId,
            string axisKey)
        {
            EnsureRuntimeIndexes();
            attitudeByIdentity.TryGetValue(PoliticalAttitudeIdentity(
                pawnId, axisKey), out CAPawnPoliticalAttitude value);
            return value;
        }

        private IReadOnlyList<CAPawnPoliticalAttitude> AttitudesForPawn(
            int pawnId)
        {
            EnsureRuntimeIndexes();
            return attitudesByPawn.TryGetValue(pawnId,
                out List<CAPawnPoliticalAttitude> values)
                ? values : Array.Empty<CAPawnPoliticalAttitude>();
        }

        private void IndexAttitude(CAPawnPoliticalAttitude attitude,
            Pawn pawn)
        {
            if (attitude == null) return;
            EnsureRuntimeIndexes();
            attitudeByIdentity[PoliticalAttitudeIdentity(attitude.pawnId,
                attitude.axisKey)] = attitude;
            if (!attitudesByPawn.TryGetValue(attitude.pawnId,
                    out List<CAPawnPoliticalAttitude> pawnAttitudes))
            {
                pawnAttitudes = new List<CAPawnPoliticalAttitude>();
                attitudesByPawn[attitude.pawnId] = pawnAttitudes;
            }
            if (!pawnAttitudes.Contains(attitude)) pawnAttitudes.Add(attitude);
            if (pawn == null) return;
            string factionAxis = FactionBoundary(pawn) + "\0"
                + attitude.axisKey;
            if (!attitudesByFactionAxis.TryGetValue(factionAxis,
                    out List<CAPawnPoliticalAttitude> factionAttitudes))
            {
                factionAttitudes = new List<CAPawnPoliticalAttitude>();
                attitudesByFactionAxis[factionAxis] = factionAttitudes;
            }
            if (!factionAttitudes.Contains(attitude))
                factionAttitudes.Add(attitude);
        }

        private static string PoliticalAttitudeIdentity(int pawnId,
            string axisKey) => pawnId + "\0" + (axisKey ?? "");

        private static List<Pawn> LivePawns()
        {
            return LivePawnSnapshot().Where(value => value != null
                    && !value.DestroyedOrNull() && value.RaceProps.Humanlike)
                .ToList();
        }

        private static IReadOnlyList<Pawn> LivePawnSnapshot()
        {
            return CACulturalCognitionWorldComponent.Current?.CognitionPawns()
                ?? (IReadOnlyList<Pawn>)PawnsFinder
                    .AllMapsWorldAndTemporary_Alive.Where(value =>
                        value != null && value.RaceProps.Humanlike)
                    .OrderBy(value => value.thingIDNumber).ToList();
        }

        private static bool Unit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                && value >= 0f && value <= 1f;
        }

        private static bool Signed(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                && value >= -1f && value <= 1f;
        }
    }
}
