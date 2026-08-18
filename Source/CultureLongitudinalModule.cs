using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Culture is historical state. This component observes one bounded period
    // of lived population, space, social conduct, institutions, political
    // practice, and material settlement state. It records a transition only
    // when that evidence changes; it grants no permission and performs no
    // work on behalf of either the player or an NPC institution.
    public sealed class CACultureLongitudinalMapComponent : MapComponent
    {
        private int campaignSchemaVersion = 2;
        private int legacyAuthoringDataEpoch =
            CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch;
        private const int EvaluationCadence = 60000;
        private const int RecentPracticeTicks = 10 * 60000;
        private int nextEvaluationTick = 5000;
        private CACulture playerLocalCulture;

        public CACultureLongitudinalMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_cultureHistorySchemaVersion", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyAuthoringDataEpoch,
                    "CA_authoringDataEpoch", 0);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "map.culture-longitudinal", campaignSchemaVersion,
                legacyAuthoringDataEpoch);
            if (readable)
            {
                Scribe_Values.Look(ref nextEvaluationTick,
                    "CA_cultureNextEvaluationTick", 5000);
                Scribe_Deep.Look(ref playerLocalCulture,
                    "CA_playerLocalCulture");
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "map.culture-longitudinal", ref campaignSchemaVersion,
                    legacyAuthoringDataEpoch, ValidateCampaignState,
                    MigrateB10State);
            }
        }

        private string ValidateCampaignState()
        {
            if (playerLocalCulture == null) return null;
            return CACultureModel.ValidationFailure(playerLocalCulture,
                requireSubstantive: true);
        }

        private string MigrateB10State()
        {
            if (playerLocalCulture == null) return null;
            if (!CACultureModel.TryUpgradeFromB10(playerLocalCulture,
                    out CACulture upgraded, out string failure))
                return "player local Culture: " + failure;
            playerLocalCulture = upgraded;
            return null;
        }

        internal static CACultureLongitudinalMapComponent For(Map map)
        {
            return map?.GetComponent<CACultureLongitudinalMapComponent>();
        }

        internal CACulture PlayerLocalCulture => playerLocalCulture;

        internal void EstablishPlayerCulture(CAPlayerFoundingPlan founding,
            int formedTick)
        {
            CAFactionState state = CAFactionStateWorldComponent.Current
                ?.EnsureFor(Faction.OfPlayer);
            if (state == null) return;
            playerLocalCulture = CACultureHistory.EnsurePlayerLocalCulture(
                state.culture, playerLocalCulture, map,
                founding?.establishedStart == true,
                founding?.temporalBasis, formedTick);
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextEvaluationTick) return;
            nextEvaluationTick = now + EvaluationCadence
                + Math.Abs(map.uniqueID * 173) % 1800;
            try { Evaluate(now); }
            catch (Exception exception)
            {
                CAModuleProfiler.RecordFailure(
                    CAModuleProfileKey.CultureLongitudinalUpdate);
                Log.Warning("[CA][Culture] historical evaluation failed: "
                    + exception.Message);
            }
        }

        private void Evaluate(int now)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.CultureLongitudinalUpdate))
            {
            CAModuleProfiler.Observe(
                CAModuleProfileKey.CultureLongitudinalUpdate,
                objectsExamined: 1);
            CAOrganizationWorldComponent organizations =
                CAOrganizationWorldComponent.Current;
            if (map.IsPlayerHome)
                EvaluatePlayer(now, organizations);

            CARegionalWorldComponent regional = CARegionalWorldComponent
                .Current;
            if (regional == null) return;
            foreach (CARegionalSettlementRecord settlement in
                regional.ForMap(map))
            {
                if (settlement == null || settlement.faction?.IsPlayer == true
                    || settlement.localRect == CellRect.Empty) continue;
                string organizationKey = settlement.regionalId + "#"
                    + settlement.slot;
                CAOrganization organization = organizations?.ByKey(
                    organizationKey);
                CACulture culture = settlement.culture;
                if (culture == null) continue;
                CACultureEvidenceSnapshot evidence = SettlementEvidence(
                    settlement, organization, organizations, now);
                List<CACulturalPracticeEvidence> practices =
                    SettlementPractices(settlement, organization,
                        organizations, evidence);
                bool practiceChanged = CACultureHistory.EvaluateTransition(
                    culture, evidence, practices,
                    "settlement " + settlement.name, now);
                List<Pawn> residents = CAPopulationProjection.Residents(
                    settlement, map);
                int eligible = residents.Count;
                List<CASocialGroupPattern> patterns =
                    CASocialReactionWorldComponent.Current?.PatternsFor(
                        organizationKey, eligible, now)
                    ?? new List<CASocialGroupPattern>();
                patterns.AddRange(DirectQuestionPatterns(culture, residents,
                    organization, settlement.populationGroups,
                    "settlement " + settlement.name, now));
                bool meaningChanged = CACultureHistory
                    .EvaluateMeaningTransition(culture, patterns,
                        "settlement " + settlement.name, now);
                if (!practiceChanged && !meaningChanged)
                    continue;
                settlement.culturalExpressionSourceSignature =
                    CACultureHistory.StateSignature(culture);
                settlement.culturalExpressionSummary =
                    CACultureHistory.PracticeSummary(culture);
                Log.Message("[CA][Culture] " + (settlement.name
                    ?? organizationKey) + " advanced to revision "
                    + culture.revision + " from "
                    + culture.lastTransitionCause + ".");
            }
            }
        }

        private void EvaluatePlayer(int now,
            CAOrganizationWorldComponent organizations)
        {
            CAFactionState state = CAFactionStateWorldComponent.Current
                ?.EnsureFor(Faction.OfPlayer);
            if (state == null) return;
            CAPlayerFoundingPlan founding = CAPlayerFoundingSession
                .ConfirmedForRuntime();
            int formedTick = CAPlayerFoundingWorldComponent.Current
                    ?.AppliedAtTick ?? -1;
            EstablishPlayerCulture(founding,
                formedTick >= 0 ? formedTick : now);
            CACulture culture = playerLocalCulture;
            if (culture == null) return;
            CAOrganization colony = organizations?.EnsureColony();
            CACultureEvidenceSnapshot evidence = PlayerEvidence(state,
                colony, organizations, now);
            List<CACulturalPracticeEvidence> practices = PlayerPractices(
                state, colony, organizations, evidence);
            bool practiceChanged = CACultureHistory.EvaluateTransition(culture,
                evidence, practices, "player settlement " + map.uniqueID,
                now);
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
                .Where(value => value != null).ToList();
            List<CASocialGroupPattern> patterns =
                CASocialReactionWorldComponent.Current?.PatternsFor(
                    culture.localityKey, colonists.Count, now)
                ?? new List<CASocialGroupPattern>();
            patterns.AddRange(DirectQuestionPatterns(culture, colonists,
                colony, null,
                "player settlement " + map.uniqueID, now));
            bool meaningChanged = CACultureHistory.EvaluateMeaningTransition(
                culture, patterns,
                "player settlement " + map.uniqueID, now);
            if (!practiceChanged && !meaningChanged)
                return;
            colony?.Record("culture", "local culture revision "
                + culture.revision + " recorded from changed lived history: "
                + culture.lastTransitionCause);
            Log.Message("[CA][Culture] player settlement advanced to "
                + "revision " + culture.revision + ".");
        }

        private CACultureEvidenceSnapshot PlayerEvidence(
            CAFactionState state, CAOrganization organization,
            CAOrganizationWorldComponent organizations, int now)
        {
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn != null).ToList();
            List<Thing> buildings = Buildings(Faction.OfPlayer, null);
            List<Building_Bed> beds = buildings.OfType<Building_Bed>()
                .ToList();
            int sharedTables = buildings.Count(thing =>
                thing.def?.surfaceType == SurfaceType.Eat);
            int defenses = buildings.Count(IsDefensiveBuilding);
            int agreements = organizations?.ActiveAgreementsInvolving(
                "player").Count ?? 0;
            string population = "residents=" + colonists.Count
                + ";ids=" + string.Join(",", colonists.Select(pawn =>
                    pawn.thingIDNumber).OrderBy(id => id));
            string spatial = "buildings=" + Bucket(buildings.Count, 5)
                + ";beds=" + beds.Count + ";bedrooms="
                + DistinctBedRooms(beds) + ";shared-tables=" + sharedTables
                + ";defenses=" + defenses;
            string social = "policies=" + StablePolicies(organization)
                + ";gatherings=" + (organization?.CountKind("policy", 0)
                    ?? 0) + ";agreements=" + agreements;
            string institutional = "offices="
                + StableOffices(organization) + ";customs="
                + StableCustoms(organization) + ";support="
                + Bucket(Mathf.RoundToInt((organization?.publicSupport
                    ?? 1f) * 100f), 10);
            string political = "beliefs=" + StableAxes(
                    state.politicalBeliefs?.positions)
                + ";practice=" + StableAxes(state.factionStructure)
                + ";conflicts=" + StableStrings(
                    organization?.openBeliefConflicts);
            string material = "buildings=" + Bucket(buildings.Count, 5)
                + ";wealth=" + Bucket(Mathf.RoundToInt(map.wealthWatcher
                    ?.WealthTotal ?? 0f), 5000) + ";agreements="
                + agreements;
            return Snapshot(now, population, spatial, social,
                institutional, political, material);
        }

        private CACultureEvidenceSnapshot SettlementEvidence(
            CARegionalSettlementRecord settlement,
            CAOrganization organization,
            CAOrganizationWorldComponent organizations, int now)
        {
            List<Thing> buildings = Buildings(settlement.faction,
                settlement.localRect);
            int agreements = organizations?.ActiveAgreementsInvolving(
                settlement.regionalId + "#" + settlement.slot).Count ?? 0;
            string population = "residents=" + settlement.populationCurrent
                + ";groups=" + string.Join(",", (settlement.populationGroups
                    ?? new List<CASettlementPopulationGroup>())
                    .Where(item => item != null).OrderBy(item => item.key)
                    .Select(item => item.key + ":" + item.share + ":"
                        + (item.ideoligionProtected
                            ? "Ideoligion-protected" : "unprotected")));
            string spatial = "rooms=" + (settlement.layout?.roomCells?.Count
                    ?? 0) + ";roads=" + (settlement.layout?.roads?.Count
                    ?? 0) + ";gates=" + (settlement.layout?.gates?.Count
                    ?? 0) + ";facilities=" + StableStrings(
                    settlement.layout?.facilityKinds);
            string social = "groups=" + (settlement.populationGroups?.Count
                    ?? 0) + ";agreements=" + agreements + ";policies="
                + StablePolicies(organization) + ";provision="
                + StableProvisions(settlement.provisionArrangements);
            CAFactionState factionState = CAFactionStateWorldComponent.Current
                ?.Find(settlement.faction);
            string institutional = "offices=" + StableOffices(organization)
                + ";customs=" + StableCustoms(organization)
                + ";development=" + (settlement.developmentAuthorized
                    ? "authorized" : "not-authorized") + ":"
                + (settlement.developmentExecutable
                    ? "executable" : "not-executable");
            string political = "beliefs=" + StableAxes(
                    factionState?.politicalBeliefs?.positions)
                + ";practice=" + StableAxes(factionState?.factionStructure)
                + ";conflicts=" + StableStrings(
                    organization?.openBeliefConflicts)
                + ";relation=" + (settlement.relationAtMaterialization
                    ?? "unrecorded");
            string material = "buildings=" + Bucket(buildings.Count, 5)
                + ";infrastructure=" + Bucket(
                    settlement.infrastructureCount, 3)
                + ";wealth=" + Bucket(settlement.wealth, 1000)
                + ";programs=" + (settlement.settlementProgram?.sourceSignature
                    ?? "none");
            return Snapshot(now, population, spatial, social,
                institutional, political, material);
        }

        private List<CACulturalPracticeEvidence> PlayerPractices(
            CAFactionState state, CAOrganization organization,
            CAOrganizationWorldComponent organizations,
            CACultureEvidenceSnapshot evidence)
        {
            var result = new List<CACulturalPracticeEvidence>();
            List<Thing> buildings = Buildings(Faction.OfPlayer, null);
            int tables = buildings.Count(thing =>
                thing.def?.surfaceType == SurfaceType.Eat);
            int recentStart = Math.Max(0,
                evidence.tick - RecentPracticeTicks);
            int gatherings = organization?.CountKind("policy", recentStart)
                ?? 0;
            string owner = "player-settlement:" + map.uniqueID;
            if (gatherings >= 2)
                Add(result, CACulturalPracticeRegistry.PublicDeliberation,
                    "Public gatherings remain a common part of settlement "
                        + "life.",
                    Mathf.Clamp(25 + tables * 8 + gatherings * 10, 0, 100),
                    "gatherings=" + gatherings + ";tables=" + tables,
                    owner, "social", FirstDecisionTick(organization,
                        "policy", recentStart), evidence.tick);
            int agreements = organizations?.ActiveAgreementsInvolving(
                "player").Count ?? 0;
            if (agreements > 0)
                Add(result, CACulturalPracticeRegistry.ExternalAgreement,
                    "Agreements with outsiders remain part of local life.",
                    Mathf.Clamp(30 + agreements * 15, 0, 100),
                    "agreements=" + agreements, owner, "social", -1,
                    evidence.tick);
            int defenses = StaffedBoundaryPatrols(organization);
            if (defenses > 0)
                Add(result, CACulturalPracticeRegistry.BoundaryPatrol,
                    "Defended boundaries remain a repeated settlement "
                        + "practice.", Mathf.Clamp(25 + defenses * 6, 0, 100),
                    "defenses=" + defenses, owner, "spatial", -1,
                    evidence.tick);
            AddActPractices(result, organization?.organizationKey ?? "player",
                owner, recentStart, evidence.tick);
            AddInstitutionalPractices(result, organization,
                organization?.organizationKey ?? "player", owner,
                recentStart, evidence.tick);
            return result;
        }

        private List<CACulturalPracticeEvidence> SettlementPractices(
            CARegionalSettlementRecord settlement,
            CAOrganization organization,
            CAOrganizationWorldComponent organizations,
            CACultureEvidenceSnapshot evidence)
        {
            var result = new List<CACulturalPracticeEvidence>();
            string owner = settlement.regionalId + "#" + settlement.slot;
            int recentStart = Math.Max(0,
                evidence.tick - RecentPracticeTicks);
            int gatherings = organization?.CountKind("policy", recentStart)
                ?? 0;
            if (gatherings >= 2)
                Add(result, CACulturalPracticeRegistry.PublicDeliberation,
                    "Public gatherings remain a common part of settlement "
                        + "life.", Mathf.Clamp(25 + gatherings * 10, 0, 100),
                    "gatherings=" + gatherings, owner, "social",
                    FirstDecisionTick(organization, "policy", recentStart),
                    evidence.tick);
            int agreements = organizations?.ActiveAgreementsInvolving(owner)
                .Count ?? 0;
            if (agreements > 0)
                Add(result, CACulturalPracticeRegistry.ExternalAgreement,
                    "Exchange with other settlements remains part of local "
                        + "life.", Mathf.Clamp(25 + agreements * 15, 0, 100),
                    "agreements=" + agreements, owner, "social", -1,
                    evidence.tick);
            int defenses = StaffedBoundaryPatrols(organization);
            if (defenses > 0)
                Add(result, CACulturalPracticeRegistry.BoundaryPatrol,
                    "Watch posts and defended approaches remain part of "
                        + "settlement life.",
                    Mathf.Clamp(20 + defenses * 12, 0, 100),
                    "defenses=" + defenses, owner, "spatial", -1,
                    evidence.tick);
            if (settlement.researchMilestones > 0
                && settlement.lastResearchActivityTick >= recentStart)
                Add(result, CACulturalPracticeRegistry.OrganizedResearch,
                    "Repeated study has become part of the settlement's "
                        + "institutional life.",
                    Mathf.Clamp(20 + settlement.researchMilestones * 20,
                        0, 100),
                    "milestones=" + settlement.researchMilestones, owner,
                    "institutional", -1, evidence.tick);
            foreach (CASettlementOperationalFact fact in
                (settlement.operationalFacts
                    ?? new List<CASettlementOperationalFact>())
                .Where(item => item != null && item.active))
            {
                CACulturalPracticeDef practice =
                    CACulturalPracticeRegistry.ForProgram(fact.programKey);
                if (practice == null || result.Any(item =>
                        item.Key == practice.Key)) continue;
                Add(result, practice.Key, practice.Summary,
                    40, "fact=" + (fact.factKey ?? "unrecorded")
                        + ";operator="
                        + (fact.operatorIdentity ?? "unrecorded")
                        + ";activity="
                        + (fact.activitySource ?? "unrecorded"),
                    owner, practice.PrimaryFacet, -1, evidence.tick);
            }
            AddActPractices(result, owner, owner, recentStart,
                evidence.tick);
            AddInstitutionalPractices(result, organization, owner, owner,
                recentStart, evidence.tick);
            return result;
        }

        private static int StaffedBoundaryPatrols(
            CAOrganization organization)
        {
            return organization?.securityPractices?.Count(practice =>
                practice != null && CACulturalPracticeRegistry
                    .HasBoundaryPatrolEvidence(practice.mapId,
                        practice.arrangementId,
                        practice.guardPawnIds?.Distinct().Count() ?? 0,
                        !practice.programKey.NullOrEmpty()
                            && !practice.programSignature.NullOrEmpty())) ?? 0;
        }

        private void AddActPractices(
            List<CACulturalPracticeEvidence> result,
            string organizationKey, string evidenceOwner, int recentStart,
            int observedTick)
        {
            IEnumerable<CAActRecord> records = CAActLedger.Current?.Records
                ?? Enumerable.Empty<CAActRecord>();
            foreach (IGrouping<string, CAActRecord> acts in records
                .Where(item => item != null && item.tick >= recentStart
                    && item.orgKey == organizationKey
                    && (item.mapId < 0 || item.mapId == map.uniqueID)
                    && CACulturalPracticeRegistry.MatchesActEvidence(
                        CACulturalPracticeRegistry.ForAct(item.act),
                        item.act, item.circumstance))
                .GroupBy(item => item.act, StringComparer.Ordinal))
            {
                CAActRecord[] repeated = acts.OrderBy(item => item.tick)
                    .ToArray();
                if (repeated.Length < 2) continue;
                CACulturalPracticeDef practice =
                    CACulturalPracticeRegistry.ForAct(acts.Key);
                if (practice == null || result.Any(item =>
                        item.Key == practice.Key)) continue;
                Add(result, practice.Key, practice.Summary,
                    Mathf.Clamp(20 + repeated.Length * 12, 0, 100),
                    "act=" + acts.Key + ";count=" + repeated.Length
                        + ";first=" + repeated[0].tick + ";last="
                        + repeated[repeated.Length - 1].tick
                        + ";circumstances=" + string.Join(",",
                            repeated.Select(item => item.circumstance
                                    ?? "unrecorded")
                                .Distinct(StringComparer.Ordinal)
                                .OrderBy(value => value,
                                    StringComparer.Ordinal)),
                    evidenceOwner, "political", repeated[0].tick,
                    observedTick);
            }
        }

        private void AddInstitutionalPractices(
            List<CACulturalPracticeEvidence> result,
            CAOrganization organization, string organizationKey,
            string evidenceOwner, int recentStart, int observedTick)
        {
            if (organization == null) return;
            List<CADecisionEntry> recent = (organization.decisionHistory
                    ?? new List<CADecisionEntry>())
                .Where(item => item != null && item.tick >= recentStart)
                .ToList();

            CADecisionEntry[] successions = recent.Where(item =>
                    item.text?.StartsWith("office changed hands - ",
                        StringComparison.Ordinal) == true)
                .OrderBy(item => item.tick).ToArray();
            if (successions.Length > 0 && organization.offices.Any(office =>
                    office != null && office.lastHolderId >= 0))
                Add(result, CACulturalPracticeRegistry.OfficeSuccession,
                    "Recorded offices transfer represented authority through their succession rules.",
                    Mathf.Clamp(30 + successions.Length * 15, 0, 100),
                    "changes=" + successions.Length + ";offices="
                        + organization.offices.Count,
                    evidenceOwner, "institutional", successions[0].tick,
                    observedTick);

            CAOrganizationRelationsWorldComponent relations =
                CAOrganizationRelationsWorldComponent.Current;
            CARelation[] delegated = (relations?.Relations
                    ?? new List<CARelation>())
                .Where(item => item != null && !item.Expired(observedTick)
                    && item.delegatedResponsibilities != null
                    && item.delegatedResponsibilities.Count > 0
                    && (item.orgKey == organizationKey
                        || item.partyOrgKey == organizationKey))
                .ToArray();
            if (delegated.Length > 0)
                Add(result, CACulturalPracticeRegistry.DelegatedGovernance,
                    "Represented organizations exercise named delegated responsibilities.",
                    Mathf.Clamp(30 + delegated.Sum(item =>
                        item.delegatedResponsibilities.Count) * 8, 0, 100),
                    "relations=" + delegated.Length + ";responsibilities="
                        + string.Join(",", delegated.SelectMany(item =>
                                item.delegatedResponsibilities)
                            .Distinct().OrderBy(value => value)),
                    evidenceOwner, "institutional",
                    delegated.Select(item => item.startTick)
                        .DefaultIfEmpty(-1).Min(), observedTick);

            CADecisionEntry[] repairs = recent.Where(item =>
                    item.kind == "works" && (item.text?.StartsWith(
                            "repair completed - ", StringComparison.Ordinal)
                        == true || item.text?.StartsWith(
                            "rebuild completed - ", StringComparison.Ordinal)
                        == true))
                .OrderBy(item => item.tick).ToArray();
            if (repairs.Length >= 2)
                Add(result, CACulturalPracticeRegistry.RepairAndRebuilding,
                    "Residents repeatedly repair and rebuild represented settlement assets.",
                    Mathf.Clamp(25 + repairs.Length * 12, 0, 100),
                    "completed=" + repairs.Length + ";last="
                        + repairs[repairs.Length - 1].tick,
                    evidenceOwner, "material", repairs[0].tick,
                    observedTick);
        }

        private List<Thing> Buildings(Faction faction, CellRect? bounds)
        {
            return map.listerThings.AllThings.Where(thing => thing != null
                    && thing.Spawned && thing.def?.building != null
                    && (faction == null || thing.Faction == faction)
                    && (!bounds.HasValue || bounds.Value.Contains(
                        thing.Position)))
                .ToList();
        }

        private static bool IsDefensiveBuilding(Thing thing)
        {
            string name = thing?.def?.defName ?? "";
            return name.IndexOf("Turret", StringComparison.OrdinalIgnoreCase)
                    >= 0
                || name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase)
                    >= 0
                || name.IndexOf("Barricade",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Trap", StringComparison.OrdinalIgnoreCase)
                    >= 0;
        }

        private static int DistinctBedRooms(IEnumerable<Building_Bed> beds)
        {
            var rooms = new HashSet<int>();
            foreach (Building_Bed bed in beds
                ?? Enumerable.Empty<Building_Bed>())
            {
                Room room = bed?.GetRoom();
                if (room != null) rooms.Add(room.ID);
            }
            return rooms.Count;
        }

        private sealed class DirectQuestionSample
        {
            internal string QuestionKey;
            internal string Summary;
            internal float Mean;
            internal float Dispersion;
            internal float Participation;
            internal int ObservedPawns;
            internal int EligiblePopulation;
            internal string Signature;
        }

        private static readonly string[] DirectQuestionKeys =
        {
            CACultureQuestionRegistry.SameSexAcceptance,
            CACultureQuestionRegistry.PluralityAcceptance,
            CACultureQuestionRegistry.GenderDistribution,
            CACultureQuestionRegistry.GenderedWork,
            CACultureQuestionRegistry.GenderOfficeAccess,
            CACultureQuestionRegistry.IntegrationPreference,
            CACultureQuestionRegistry.DissentTolerance
        };

        // Seven questions need facts more specific than a generic social
        // subject. These samples are computed only from represented relations,
        // work settings, offices, population composition, and sanction history.
        // Absence of an event is not treated as disapproval.
        private static List<CASocialGroupPattern> DirectQuestionPatterns(
            CACulture culture, List<Pawn> pawns, CAOrganization organization,
            List<CASettlementPopulationGroup> populationGroups,
            string scope, int tick)
        {
            pawns = (pawns ?? new List<Pawn>()).Where(value => value != null)
                .ToList();
            var samples = new List<DirectQuestionSample>();
            AddRelationshipSamples(samples, pawns);
            AddGenderSamples(samples, pawns, organization);
            AddIntegrationSample(samples, pawns, populationGroups);
            AddDissentSample(samples, organization);
            List<CASocialGroupPattern> result = ObserveDirectQuestions(
                culture, samples, scope, tick);
            CACulturalCognitionWorldComponent cognition =
                CACulturalCognitionWorldComponent.Current;
            if (cognition != null)
                foreach (Pawn pawn in pawns)
                    foreach (string questionKey in DirectQuestionKeys)
                        cognition.RefreshRepresentedEvidence(pawn,
                            questionKey, tick, culture);
            return result;
        }

        private static void AddRelationshipSamples(
            List<DirectQuestionSample> samples, List<Pawn> pawns)
        {
            var pawnIds = new HashSet<int>(pawns.Select(value =>
                value.thingIDNumber));
            var sameSexPairs = new HashSet<string>(StringComparer.Ordinal);
            var pluralParticipants = new HashSet<int>();
            foreach (Pawn pawn in pawns)
            {
                List<Pawn> partners = pawn.relations?.DirectRelations
                    ?.Where(relation => relation?.otherPawn != null
                        && pawnIds.Contains(relation.otherPawn.thingIDNumber)
                        && (relation.def == PawnRelationDefOf.Lover
                            || relation.def == PawnRelationDefOf.Fiance
                            || relation.def == PawnRelationDefOf.Spouse))
                    .Select(relation => relation.otherPawn).Distinct().ToList()
                    ?? new List<Pawn>();
                foreach (Pawn partner in partners.Where(value =>
                    pawn.gender != Gender.None && pawn.gender == value.gender))
                    sameSexPairs.Add(Math.Min(pawn.thingIDNumber,
                            partner.thingIDNumber) + ":"
                        + Math.Max(pawn.thingIDNumber,
                            partner.thingIDNumber));
                if (partners.Count > 1)
                {
                    pluralParticipants.Add(pawn.thingIDNumber);
                    foreach (Pawn partner in partners)
                        pluralParticipants.Add(partner.thingIDNumber);
                }
            }
            if (sameSexPairs.Count > 0)
                samples.Add(Sample(CACultureQuestionRegistry
                        .SameSexAcceptance,
                    "represented same-sex unions", 0.65f, 0.18f,
                    sameSexPairs.Count * 2, pawns.Count,
                    string.Join(",", sameSexPairs.OrderBy(value => value,
                        StringComparer.Ordinal))));
            if (pluralParticipants.Count >= 2)
                samples.Add(Sample(CACultureQuestionRegistry
                        .PluralityAcceptance,
                    "represented plural unions", 0.65f, 0.20f,
                    pluralParticipants.Count, pawns.Count,
                    string.Join(",", pluralParticipants.OrderBy(value =>
                        value))));
        }

        private static void AddGenderSamples(
            List<DirectQuestionSample> samples, List<Pawn> pawns,
            CAOrganization organization)
        {
            List<Pawn> gendered = pawns.Where(value => value.gender
                    != Gender.None).ToList();
            List<Pawn> holders = (organization?.offices
                    ?? new List<CAOffice>()).Where(value => value != null
                        && value.holderId >= 0)
                .Select(office => gendered.FirstOrDefault(value =>
                    value.thingIDNumber == office.holderId))
                .Where(value => value != null).Distinct().ToList();
            if (holders.Count >= 2)
            {
                float femaleShare = holders.Count(value => value.gender
                    == Gender.Female) / (float)holders.Count;
                samples.Add(Sample(CACultureQuestionRegistry
                        .GenderDistribution,
                    "gender distribution among represented officeholders",
                    femaleShare * 2f - 1f, 0.16f, holders.Count,
                    Math.Max(holders.Count, gendered.Count),
                    string.Join(",", holders.OrderBy(value =>
                        value.thingIDNumber).Select(value =>
                            value.thingIDNumber + ":" + value.gender))));
                int maleHolders = holders.Count(value => value.gender
                    == Gender.Male);
                int femaleHolders = holders.Count(value => value.gender
                    == Gender.Female);
                if (CACulturalCognitionPureKernel
                    .TryHistoricalOfficeAccessPosition(maleHolders,
                        femaleHolders, out float officeAccess))
                    samples.Add(Sample(CACultureQuestionRegistry
                            .GenderOfficeAccess,
                        "represented access to public office", officeAccess,
                        0.22f, holders.Count,
                        gendered.Count, "holders:"
                            + string.Join(",", holders.Select(value =>
                                value.gender).OrderBy(value => value))));
            }

            List<Pawn> men = gendered.Where(value => value.gender
                == Gender.Male && value.workSettings != null).ToList();
            List<Pawn> women = gendered.Where(value => value.gender
                == Gender.Female && value.workSettings != null).ToList();
            if (men.Count < 2 || women.Count < 2) return;
            List<WorkTypeDef> workTypes = DefDatabase<WorkTypeDef>
                .AllDefsListForReading.Where(value => value != null).ToList();
            if (workTypes.Count == 0) return;
            float difference = workTypes.Average(work => Math.Abs(
                men.Count(value => value.workSettings.WorkIsActive(work))
                    / (float)men.Count
                - women.Count(value => value.workSettings.WorkIsActive(work))
                    / (float)women.Count));
            samples.Add(Sample(CACultureQuestionRegistry.GenderedWork,
                "represented work assignments by gender",
                CACulturalCognitionPureKernel
                    .HistoricalGenderedWorkPosition(difference),
                Mathf.Clamp01(difference), men.Count + women.Count,
                gendered.Count, "difference=" + difference.ToString("0.00")));
        }

        private static void AddIntegrationSample(
            List<DirectQuestionSample> samples, List<Pawn> pawns,
            List<CASettlementPopulationGroup> populationGroups)
        {
            int representedGroups = (populationGroups
                    ?? new List<CASettlementPopulationGroup>())
                .Count(value => value != null && value.share > 0);
            int ideoligions = pawns.Select(value => value.Ideo?.id ?? -1)
                .Where(value => value >= 0).Distinct().Count();
            int groups = Math.Max(representedGroups, ideoligions);
            if (groups < 2 || pawns.Count < 2) return;
            samples.Add(Sample(CACultureQuestionRegistry
                    .IntegrationPreference,
                "multiple represented populations share one settlement",
                0.55f, 0.24f, pawns.Count, pawns.Count,
                "groups=" + groups + ";pawns=" + string.Join(",",
                    pawns.Select(value => value.thingIDNumber)
                        .OrderBy(value => value))));
        }

        private static void AddDissentSample(
            List<DirectQuestionSample> samples, CAOrganization organization)
        {
            List<CAInstitutionSanctionAppraisal> sanctions = (organization
                    ?.sanctionAppraisals
                    ?? new List<CAInstitutionSanctionAppraisal>())
                .Where(value => value != null
                    && (value.subjectKey
                            == CASocialSubjectRegistry.EnforcedOrder
                        || value.subjectKey
                            == CASocialSubjectRegistry.PublicVoice))
                .OrderByDescending(value => value.tick).Take(24).ToList();
            int pawns = sanctions.Select(value => value.pawnId).Distinct()
                .Count();
            if (pawns < 2) return;
            float mean = sanctions.Average(value =>
                value.voluntaryCooperation - value.deterrence);
            samples.Add(Sample(CACultureQuestionRegistry.DissentTolerance,
                "represented dissent and institutional response",
                Mathf.Clamp(mean, -1f, 1f), 0.28f, pawns,
                Math.Max(pawns, organization?.memberPawnIds?.Count ?? pawns),
                string.Join(",", sanctions.Select(value =>
                    value.factIdentity).OrderBy(value => value,
                        StringComparer.Ordinal))));
        }

        private static DirectQuestionSample Sample(string key, string summary,
            float mean, float dispersion, int observed, int eligible,
            string signature)
        {
            return new DirectQuestionSample
            {
                QuestionKey = key,
                Summary = summary,
                Mean = Mathf.Clamp(mean, -1f, 1f),
                Dispersion = Mathf.Clamp01(dispersion),
                ObservedPawns = Math.Max(0, observed),
                EligiblePopulation = Math.Max(1, eligible),
                Participation = Mathf.Clamp01(observed
                    / (float)Math.Max(1, eligible)),
                Signature = CASocialPatternKernel.StableHash(key + "|"
                    + (signature ?? "represented"))
            };
        }

        private static List<CASocialGroupPattern> ObserveDirectQuestions(
            CACulture culture, List<DirectQuestionSample> samples,
            string scope, int tick)
        {
            const string domain = "Culture question evidence";
            culture.observations = culture.observations
                ?? new List<CACultureObservation>();
            var current = new HashSet<string>(samples.Select(value =>
                "question:" + value.QuestionKey), StringComparer.Ordinal);
            foreach (CACultureObservation absent in culture.observations
                .Where(value => value != null && value.sourceDomain == domain
                    && !current.Contains(value.key)))
            {
                absent.evidenceStartTick = -1;
                absent.firstObservedTick = -1;
                absent.lastObservedTick = -1;
                absent.observationCount = 0;
                absent.hasQuestionEvidence = false;
                absent.questionPosition = 0f;
                absent.questionDispersion = 0f;
                absent.observedPawnCount = 0;
                absent.eligiblePopulation = 0;
            }
            var result = new List<CASocialGroupPattern>();
            foreach (DirectQuestionSample sample in samples)
            {
                string key = "question:" + sample.QuestionKey;
                CACultureObservation observation = culture.observations
                    .FirstOrDefault(value => value != null && value.key == key
                        && value.sourceDomain == domain);
                bool continuous = observation != null
                    && observation.sourceSignature == sample.Signature
                    && observation.lastObservedTick >= 0
                    && tick - observation.lastObservedTick <= 2 * 60000;
                if (observation == null)
                {
                    observation = new CACultureObservation
                    {
                        key = key,
                        sourceDomain = domain
                    };
                    culture.observations.Add(observation);
                }
                if (!continuous)
                {
                    observation.evidenceStartTick = tick;
                    observation.firstObservedTick = tick;
                    observation.observationCount = 1;
                }
                else observation.observationCount++;
                observation.summary = sample.Summary;
                observation.sourceOwner = scope;
                observation.sourceSignature = sample.Signature;
                observation.strength = Mathf.RoundToInt(
                    sample.Participation * 100f);
                observation.lastObservedTick = tick;
                observation.hasQuestionEvidence = true;
                observation.questionPosition = sample.Mean;
                observation.questionDispersion = sample.Dispersion;
                observation.observedPawnCount = sample.ObservedPawns;
                observation.eligiblePopulation = sample.EligiblePopulation;
                result.Add(new CASocialGroupPattern
                {
                    QuestionKey = sample.QuestionKey,
                    PopulationIdentity = "*",
                    WeightedApproval = Mathf.RoundToInt(sample.Mean * 100f),
                    Dispersion = sample.Dispersion,
                    Polarization = sample.Dispersion,
                    Participation = sample.Participation,
                    GroupAlignment = 1f - sample.Dispersion,
                    EvidenceCount = observation.observationCount,
                    ObservedPawnCount = sample.ObservedPawns,
                    EligiblePopulation = sample.EligiblePopulation,
                    EvidenceStartTick = observation.evidenceStartTick,
                    LastEvidenceTick = tick,
                    EvidenceSignature = sample.Signature
                });
            }
            return result;
        }

        private static CACultureEvidenceSnapshot Snapshot(int tick,
            string population, string spatial, string social,
            string institutional, string political, string material)
        {
            return new CACultureEvidenceSnapshot
            {
                tick = tick,
                population = population,
                spatial = spatial,
                social = social,
                institutional = institutional,
                political = political,
                material = material
            };
        }

        private static void Add(List<CACulturalPracticeEvidence> result,
            string key, string summary, int strength, string signature,
            string sourceOwner, string sourceDomain, int evidenceStartTick,
            int observedTick)
        {
            result.Add(new CACulturalPracticeEvidence
            {
                Key = key,
                Summary = summary,
                Strength = Mathf.Clamp(strength, 0, 100),
                SourceOwner = sourceOwner,
                SourceDomain = sourceDomain,
                EvidenceStartTick = evidenceStartTick,
                ObservedTick = observedTick,
                SourceSignature = CACulturalExpressionCausalKernel.Signature(
                    new[] { key, signature ?? "unrecorded" })
            });
        }

        private static int FirstDecisionTick(CAOrganization organization,
            string kind, int sinceTick)
        {
            return (organization?.decisionHistory
                    ?? new List<CADecisionEntry>())
                .Where(item => item != null && item.kind == kind
                    && item.tick >= sinceTick)
                .Select(item => item.tick).DefaultIfEmpty(-1).Min();
        }

        private static string StableAxes(IEnumerable<CAAxisEntry> axes)
        {
            return string.Join(",", (axes ?? Enumerable.Empty<CAAxisEntry>())
                .Where(item => item != null)
                .OrderBy(item => item.axisKey)
                .Select(item => (item.axisKey ?? "unrecorded") + "="
                    + (item.optionKey ?? "unrecorded")));
        }

        private static string StablePolicies(CAOrganization organization)
        {
            return string.Join(",", (organization?.policies
                    ?? new List<CAPolicyRecord>())
                .Where(item => item != null).OrderBy(item => item.key)
                .Select(item => (item.key ?? "unrecorded") + "="
                    + (item.value ?? "unrecorded")));
        }

        private static string StableCustoms(CAOrganization organization)
        {
            return string.Join(",", (organization?.customs
                    ?? new List<CAOrganizationCustom>())
                .Where(item => item != null).OrderBy(item => item.key)
                .Select(item => item.key ?? "unrecorded"));
        }

        private static string StableOffices(CAOrganization organization)
        {
            return string.Join(",", (organization?.offices
                    ?? new List<CAOffice>())
                .Where(item => item != null).OrderBy(item => item.sourceKey)
                .Select(item => (item.sourceKey ?? item.name ?? "unrecorded")
                    + ":" + item.holderId));
        }

        private static string StableProvisions(
            IEnumerable<CAProvisionArrangement> provisions)
        {
            return string.Join(",", (provisions
                    ?? Enumerable.Empty<CAProvisionArrangement>())
                .Where(item => item != null && item.active)
                .OrderBy(item => item.key)
                .Select(item => item.operatorKind + ":" + item.access + ":"
                    + item.funding + ":" + item.distribution + ":"
                    + item.nodes));
        }

        private static string StableStrings(IEnumerable<string> values)
        {
            return string.Join(",", (values ?? Enumerable.Empty<string>())
                .Where(value => !value.NullOrEmpty())
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static int Bucket(int value, int size)
        {
            if (size <= 1) return value;
            return Math.Max(0, value) / size;
        }
    }
}
