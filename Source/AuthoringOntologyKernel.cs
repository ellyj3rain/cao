using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    public sealed class CAPoliticalLegacyReceiptExpansion
    {
        public CAPoliticalLegacyReceiptExpansion(string optionKey,
            string scores, string evidence, bool tieBroken)
        {
            OptionKey = optionKey;
            Scores = scores;
            Evidence = evidence;
            TieBroken = tieBroken;
        }

        public string OptionKey { get; }
        public string Scores { get; }
        public string Evidence { get; }
        public bool TieBroken { get; }
    }

    public static class CAPoliticalLegacyMechanisms
    {
        // The only supported B10 pseudo-options. Conversion is permitted only
        // when the historical option named every mechanism it represented.
        // B10's support value said merely that "several systems" coexisted;
        // choosing a subset now would invent state, so that value remains an
        // explicit incompatibility at the pre-campaign correction boundary.
        public static IReadOnlyList<string> Expand(string axisKey,
            string optionKey)
        {
            if (!string.Equals(optionKey, "mixed",
                    StringComparison.Ordinal))
                return new[] { optionKey };
            if (axisKey == "ownership")
                return new[] { "private", "cooperative", "common" };
            if (axisKey == "economy")
                return new[] { "market", "planned", "communal" };
            return Array.Empty<string>();
        }

        public static string UnsupportedReason(string axisKey,
            string optionKey)
        {
            if (axisKey == "support" && optionKey == "mixed")
                return "B10 mixed support recorded only that several systems "
                    + "coexisted; it did not identify which support systems";
            return null;
        }

        // Pure receipt migration shared by production normalization and the
        // executable compatibility suite. Every replacement retains the exact
        // causal payload while naming its new mechanism and expansion boundary.
        public static IReadOnlyList<CAPoliticalLegacyReceiptExpansion>
            ExpandReceipt(string axisKey, string optionKey, string scores,
                string evidence, bool tieBroken)
        {
            IReadOnlyList<string> replacements = Expand(axisKey, optionKey);
            if (replacements.Count == 0) return Array.Empty<
                CAPoliticalLegacyReceiptExpansion>();
            string provenance = (string.IsNullOrWhiteSpace(evidence)
                    ? "" : evidence + " | ")
                + "B10 " + axisKey + " " + optionKey
                + " explicitly represented "
                + string.Join(", ", replacements)
                + "; expanded at the B11 correction boundary";
            return replacements.Select(replacement =>
                new CAPoliticalLegacyReceiptExpansion(replacement, scores,
                    provenance, tieBroken)).ToList();
        }
    }

    public enum CAAuthoringSemanticKind : byte
    {
        ExclusiveCategorical = 0,
        MultiValuedSet = 1,
        ScalarContinuous = 2,
        Relational = 3,
        StructuredComposition = 4,
        OptionalUnset = 5,
        Derived = 6,
        ReadOnlyRealized = 7,
        PartialPatchPreset = 8
    }

    public enum CAAuthoringTemporalStatus : byte
    {
        Normative = 0,
        Current = 1,
        Historical = 2,
        Derived = 3
    }

    public enum CAProductionFactClassification : byte
    {
        CulturallyInterpretableSubject = 0,
        ConcreteRepeatedPractice = 1,
        PoliticalInstitutionalFact = 2,
        DerivedSummaryOnly = 3,
        Excluded = 4,
        NotImplementedNotPlayerFacing = 5
    }

    public sealed class CAAuthoringControlContract
    {
        public string Key;
        public CAAuthoringSemanticKind SemanticKind;
        public string AuthoritativeOwner;
        public string Scope;
        public CAAuthoringTemporalStatus TemporalStatus;
        public string Cardinality;
        public string Coexistence;
        public string ExclusivityInvariant;
        public string Authorship;
        public List<string> FieldsWritten = new List<string>();
        public List<string> Consumers = new List<string>();

        public bool IsComplete(out string failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(Key)) failure = "control key missing";
            else if (string.IsNullOrWhiteSpace(AuthoritativeOwner))
                failure = Key + " has no owner";
            else if (string.IsNullOrWhiteSpace(Scope))
                failure = Key + " has no scope";
            else if (string.IsNullOrWhiteSpace(Cardinality))
                failure = Key + " has no cardinality";
            else if (string.IsNullOrWhiteSpace(Coexistence))
                failure = Key + " has no coexistence rule";
            else if (string.IsNullOrWhiteSpace(Authorship))
                failure = Key + " has no authorship rule";
            else if (FieldsWritten == null || FieldsWritten.Count == 0)
                failure = Key + " declares no exact field";
            else if (Consumers == null || Consumers.Count == 0)
                failure = Key + " declares no consumer";
            else if (SemanticKind == CAAuthoringSemanticKind.ExclusiveCategorical
                && string.IsNullOrWhiteSpace(ExclusivityInvariant))
                failure = Key + " has no exclusivity invariant";
            return failure == null;
        }
    }

    public sealed class CACulturalPracticeDef
    {
        public string Owner;
        public string Key;
        public string Label;
        public string Summary;
        public string Activity;
        public string ActorRole;
        public string TargetRole;
        public string Trigger;
        public string Cadence;
        public string Operator;
        public string AuthorityBasis;
        public string Setting;
        public string MaterialRequirements;
        public string Conditions;
        public string EvidenceSource;
        public string RuntimeConsumer;
        public string PrimaryFacet;
        public string ProgramKey;
        public string ActKind;
        public List<string> SubjectKeys = new List<string>();

        public bool IsProduction(out string failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(Owner))
                failure = "practice owner missing";
            else if (!CACulturalPracticeRegistry.ValidKey(Key)
                || !Key.StartsWith(Owner + ".", StringComparison.Ordinal))
                failure = "practice key must be stable, namespaced, and owner-scoped";
            else if (new[] { Label, Summary, Activity, ActorRole, Trigger,
                    Cadence, Operator, AuthorityBasis, Setting,
                    MaterialRequirements, Conditions, EvidenceSource,
                    RuntimeConsumer }
                .Any(string.IsNullOrWhiteSpace))
                failure = Key + " is missing concrete conduct or realization data";
            else if (SubjectKeys == null || SubjectKeys.Count == 0
                || SubjectKeys.Any(value =>
                    !CASocialSubjectRegistry.ValidKey(value)))
                failure = Key + " needs valid implicated social subjects";
            else if (string.IsNullOrWhiteSpace(PrimaryFacet)
                || PrimaryFacet.Contains("."))
                failure = Key + " needs a human-facing non-key facet";
            else if (!string.IsNullOrWhiteSpace(ProgramKey)
                && !ProgramKey.StartsWith("ca.settlement.",
                    StringComparison.Ordinal))
                failure = Key + " has an invalid settlement-program key";
            else if (string.IsNullOrWhiteSpace(ProgramKey)
                && string.IsNullOrWhiteSpace(ActKind)
                && !EvidenceSource.StartsWith("CAOrganization",
                    StringComparison.Ordinal)
                && !EvidenceSource.StartsWith("CARegionalSettlementRecord",
                    StringComparison.Ordinal)
                && !EvidenceSource.StartsWith("HistoryEventsManager",
                    StringComparison.Ordinal))
                failure = Key + " has no executable evidence adapter";
            return failure == null;
        }
    }

    public static class CACulturalPracticeRegistry
    {
        private static readonly Dictionary<string, CACulturalPracticeDef> Values =
            Build().ToDictionary(value => value.Key, value => value,
                StringComparer.Ordinal);

        public const string HousingUpkeep = "ca.practice.housing_upkeep";
        public const string MealPreparation = "ca.practice.meal_preparation";
        public const string ReserveStorage = "ca.practice.reserve_storage";
        public const string MedicalCare = "ca.practice.medical_care";
        public const string GeneralCraft = "ca.practice.general_craft";
        public const string SpecializedCraft = "ca.practice.specialized_craft";
        public const string TradeExchange = "ca.practice.trade_exchange";
        public const string InstitutionalGovernance =
            "ca.practice.institutional_governance";
        public const string CustodialCare = "ca.practice.custodial_care";
        public const string BoundaryDefense = "ca.practice.boundary_defense";
        public const string OrganizedResearch = "ca.practice.organized_research";
        public const string ReligiousObservance =
            "ca.practice.religious_observance";
        public const string PublicGathering = "ca.practice.public_gathering";
        public const string SharedRecreation = "ca.practice.shared_recreation";
        public const string ArtAndRemembrance =
            "ca.practice.art_and_remembrance";
        public const string Cultivation = "ca.practice.cultivation";
        public const string AnimalTending = "ca.practice.animal_tending";
        public const string Communications = "ca.practice.communications";
        public const string TransportService = "ca.practice.transport_service";
        public const string CommunalProvision =
            "ca.practice.communal_provision";
        public const string AuthorityProvision =
            "ca.practice.authority_provision";
        public const string HouseholdProvision =
            "ca.practice.household_provision";
        public const string PublicDeliberation =
            "ca.practice.public_deliberation";
        public const string ExternalAgreement =
            "ca.practice.external_agreement";
        public const string BoundaryPatrol = "ca.practice.boundary_patrol";
        public const string TaxCollection = "ca.practice.tax_collection";
        public const string CombatConduct = "ca.practice.combat_conduct";
        public const string OfficeSuccession = "ca.practice.office_succession";
        public const string DelegatedGovernance =
            "ca.practice.delegated_governance";
        public const string RepairAndRebuilding =
            "ca.practice.repair_and_rebuilding";
        public const string HumanFleshConsumption =
            "ca.practice.human_flesh_consumption";
        public const string AnimalFoodConsumption =
            "ca.practice.animal_food_consumption";
        public const string UnfamiliarFoodConsumption =
            "ca.practice.unfamiliar_food_consumption";
        public const string HumanButchery = "ca.practice.human_butchery";
        public const string CorpseExposure = "ca.practice.corpse_exposure";
        public const string OrganExtraction = "ca.practice.organ_extraction";
        public const string OrganTrade = "ca.practice.organ_trade";
        public const string BodyModification =
            "ca.practice.body_modification";
        public const string RitualInjury = "ca.practice.ritual_injury";
        public const string ExclusiveMarriage =
            "ca.practice.exclusive_marriage";
        public const string PluralMarriage = "ca.practice.plural_marriage";
        public const string NonSpousalIntimacy =
            "ca.practice.nonspousal_intimacy";
        public const string DrugUse = "ca.practice.drug_use";
        public const string DrugAdministration =
            "ca.practice.drug_administration";
        public const string DoctrinalChange =
            "ca.practice.doctrinal_change";
        public const string CrossIdeoligionObservance =
            "ca.practice.cross_ideoligion_observance";
        public const string AnimalSlaughter = "ca.practice.animal_slaughter";
        public const string InnocentAnimalKilling =
            "ca.practice.innocent_animal_killing";
        public const string ResourceExtraction =
            "ca.practice.resource_extraction";
        public const string SettlementAbandonment =
            "ca.practice.settlement_abandonment";
        public const string Raiding = "ca.practice.raiding";
        public const string DownedPersonStripping =
            "ca.practice.downed_person_stripping";
        public const string InterpersonalViolence =
            "ca.practice.interpersonal_violence";
        public const string Enslavement = "ca.practice.enslavement";
        public const string Execution = "ca.practice.execution";

        public static IReadOnlyList<CACulturalPracticeDef> All => Values.Values
            .OrderBy(value => value.Label, StringComparer.Ordinal).ToList();

        public static bool ValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            string[] parts = key.Split(new[] { '.' },
                StringSplitOptions.None);
            return parts.Length == 3 && parts.All(part => part.Length > 0
                && part.All(value => char.IsLetterOrDigit(value)
                    || value == '_' || value == '-'));
        }

        public static CACulturalPracticeDef Find(string key)
        {
            CACulturalPracticeDef value;
            return key != null && Values.TryGetValue(key, out value)
                ? value : null;
        }

        public static CACulturalPracticeDef ForProgram(string programKey)
        {
            return programKey == null ? null : Values.Values.FirstOrDefault(
                value => string.Equals(value.ProgramKey, programKey,
                    StringComparison.Ordinal));
        }

        public static CACulturalPracticeDef ForAct(string actKind)
        {
            return actKind == null ? null : Values.Values.FirstOrDefault(
                value => string.Equals(value.ActKind, actKind,
                    StringComparison.Ordinal));
        }

        // Act-backed practices are more specific than an act-kind lookup.
        // In particular, ordinary mutual combat is not conduct toward a
        // defeated person. The longitudinal collector and executable receipts
        // share this exact predicate.
        public static bool MatchesActEvidence(CACulturalPracticeDef practice,
            string actKind, string circumstance)
        {
            if (practice == null || !string.Equals(practice.ActKind, actKind,
                    StringComparison.Ordinal)) return false;
            if (practice.Key == TaxCollection)
                return string.Equals(actKind, "taxation",
                    StringComparison.Ordinal);
            if (practice.Key == CombatConduct)
                return string.Equals(actKind, "violence",
                        StringComparison.Ordinal)
                    && (string.Equals(circumstance, "struck-unresisting",
                            StringComparison.Ordinal)
                        || string.Equals(circumstance, "struck-downed",
                            StringComparison.Ordinal));
            return false;
        }

        // A security label is not patrol conduct. Production evidence requires
        // named guards plus a represented route, post, or program assignment
        // on an actual map. This pure predicate is shared by the longitudinal
        // collector and the executable ontology receipts.
        public static bool HasBoundaryPatrolEvidence(int mapId,
            int arrangementId, int guardCount, bool programBacked)
        {
            return mapId >= 0 && guardCount > 0
                && (arrangementId >= 0 || programBacked);
        }

        // This is the executable observation contract consumed by Culture's
        // longitudinal collector. A prose evidence label alone never makes a
        // practice production-backed.
        public static bool HasExecutableObservationAdapter(
            CACulturalPracticeDef practice)
        {
            if (practice == null) return false;
            if (!string.IsNullOrWhiteSpace(practice.ProgramKey))
                return ForProgram(practice.ProgramKey)?.Key == practice.Key;
            if (!string.IsNullOrWhiteSpace(practice.ActKind))
                return practice.Key == TaxCollection
                    ? MatchesActEvidence(practice, "taxation", null)
                    : practice.Key == CombatConduct
                        && MatchesActEvidence(practice, "violence",
                            "struck-downed")
                        && !MatchesActEvidence(practice, "violence",
                            "mutual-combat");
            return practice.Key == PublicDeliberation
                || practice.Key == ExternalAgreement
                || (practice.Key == BoundaryPatrol
                    && HasBoundaryPatrolEvidence(3, 7, 2, false)
                    && !HasBoundaryPatrolEvidence(3, 7, 0, false))
                || practice.Key == OfficeSuccession
                || practice.Key == DelegatedGovernance
                || practice.Key == RepairAndRebuilding
                || IsNativeEventPractice(practice.Key);
        }

        public static bool IsNativeEventPractice(string practiceKey)
        {
            return CANativeCultureEventAdapterRegistry.HasPractice(practiceKey);
        }

        // B10 longitudinal observations wrote one of four evidence-backed
        // social subjects into the practice slot. Only records carrying an
        // actual runtime evidence signature and observation tick have a
        // truthful concrete-practice equivalent. Authored subject-only rows
        // remain invalid and are discarded at the B11 boundary.
        public static string FromB10LongitudinalEvidence(string subjectKey,
            string evidenceSignature, int firstRecordedTick)
        {
            if (string.IsNullOrWhiteSpace(evidenceSignature)
                || firstRecordedTick < 0) return null;
            switch (subjectKey)
            {
                case "ca.space.public_gathering":
                    return PublicDeliberation;
                case "ca.exchange.outsider_contact":
                    return ExternalAgreement;
                // B10's defended-boundary observation counted unstaffed
                // security labels. It cannot prove the named guards and live
                // patrol assignment required by the B11 practice contract.
                case "ca.space.defended_boundary":
                    return null;
                case "ca.knowledge.research_work":
                    return OrganizedResearch;
                default:
                    return null;
            }
        }

        public static bool Validate(out string failure)
        {
            foreach (CACulturalPracticeDef value in Values.Values)
            {
                if (!value.IsProduction(out failure)) return false;
                if (!HasExecutableObservationAdapter(value))
                {
                    failure = value.Key
                        + " has no executable Culture observation adapter";
                    return false;
                }
            }
            failure = null;
            return true;
        }

        private static IEnumerable<CACulturalPracticeDef> Build()
        {
            yield return Program(HousingUpkeep, "Maintaining homes",
                "Residents repeatedly maintain inhabited shelter.",
                "maintain and repair occupied housing", "household workers",
                "occupied households", "housing need remains active",
                "as repairs or upkeep are required", "households or the recorded housing operator",
                "recognized occupancy and maintenance responsibility",
                "homes and residential space", "housing assets, labor, and access",
                "a functional housing program and assigned residents exist",
                "ca.settlement.housing", "Homes and shelter",
                "ca.shelter.maintained_housing", "ca.membership.household_membership");
            yield return Program(MealPreparation, "Preparing meals",
                "Cooks repeatedly prepare food for a represented population.",
                "prepare meals", "cooks and food workers", "resident diners",
                "food demand and ingredients are present", "at recurring meal needs",
                "the recorded food operator", "standing food-provision rules",
                "kitchens and meal areas", "food, cooking assets, labor, and access",
                "a functional food-preparation program exists",
                "ca.settlement.food-preparation", "Food and care",
                "ca.food.meal_preparation", "ca.support.shared_provision");
            yield return Program(ReserveStorage, "Keeping reserves",
                "Workers preserve and maintain stored goods for later use.",
                "store and preserve reserves", "storekeepers and assigned workers",
                "the represented settlement", "goods enter or leave reserve storage",
                "as stock changes", "the recorded storage operator",
                "recognized custody of stored goods", "stores and stockpiles",
                "storage space, access, and maintenance", "a functional storage program exists",
                "ca.settlement.storage", "Work and exchange",
                "ca.stores.stored_reserves", "ca.property.private_ownership",
                "ca.property.common_ownership");
            yield return Program(MedicalCare, "Providing medical care",
                "Caregivers repeatedly treat sick or injured people.",
                "provide medical treatment", "caregivers and medical workers",
                "patients", "a patient needs represented care", "as care is needed",
                "the recorded medical operator", "recognized care responsibility",
                "medical rooms and patient spaces", "medicine, beds, skill, labor, and access",
                "a functional medical program and eligible caregiver exist",
                "ca.settlement.medicine", "Food and care",
                "ca.care.medical_care", "ca.support.shared_provision");
            yield return Program(GeneralCraft, "General production",
                "Workers repeatedly produce ordinary tools and goods.",
                "make general goods", "production workers", "settlement users and exchange partners",
                "a represented production demand exists", "as work orders and demand recur",
                "the recorded production operator", "recognized work and property rules",
                "workshops", "workstations, material, knowledge, labor, and access",
                "a functional production program exists", "ca.settlement.production",
                "Work and exchange", "ca.production.general_craft");
            yield return Program(SpecializedCraft, "Specialized production",
                "Skilled workers repeatedly produce specialized goods.",
                "make specialized goods", "specialist workers", "settlement users and exchange partners",
                "specialized demand and knowledge exist", "as specialized orders recur",
                "the recorded industry operator", "recognized work and property rules",
                "specialized workshops", "specialist assets, material, knowledge, labor, and access",
                "a functional specialized-industry program exists",
                "ca.settlement.specialized-industry", "Work and exchange",
                "ca.production.specialized_craft");
            yield return Program(TradeExchange, "Trading with outsiders",
                "Traders repeatedly exchange goods with represented outsiders.",
                "exchange goods by agreement", "traders and carriers", "outside counterparties",
                "an exchange route and counterparty exist", "when bargains or deliveries occur",
                "the recorded trade operator", "recognized agreement and property rules",
                "markets, depots, and trade routes", "goods, route access, labor, and communication",
                "a functional trade program and route exist", "ca.settlement.trade",
                "Work and exchange", "ca.exchange.voluntary_trade",
                "ca.exchange.outsider_contact", "ca.relations.voluntary_agreement");
            yield return Program(InstitutionalGovernance, "Administering public decisions",
                "Officeholders repeatedly carry out represented decisions and policies.",
                "administer decisions and policy", "officeholders and delegated workers",
                "members and residents subject to the decision", "a represented decision requires administration",
                "as decisions and policies require action", "the recorded governing organization",
                "current offices, jurisdiction, and decision procedure", "meeting, record, and communication space",
                "staffed offices, records, and communication access",
                "a functional governance program and current authority exist",
                "ca.settlement.governance", "Authority and obligation",
                "ca.politics.office_governance", "ca.politics.public_voice");
            yield return Program(CustodialCare, "Maintaining custody",
                "Custodians repeatedly hold and care for represented captives.",
                "maintain custody", "custodians", "captives", "a lawful or factual custody relation exists",
                "while custody continues", "the recorded custody operator", "current custody authority and war conduct",
                "secure holding and care space", "secure rooms, food, care, labor, and access",
                "a functional custody program and captive relation exist",
                "ca.settlement.custody", "Authority and obligation",
                "ca.custody.humane_treatment", "ca.custody.punishment");
            yield return Program(BoundaryDefense, "Organized defense",
                "Defenders repeatedly prepare and respond through a represented defense organization.",
                "prepare and respond to threats", "assigned defenders", "settlement residents and defended ground",
                "a represented threat or readiness duty exists", "through drills, watches, or alerts",
                "the recorded defense operator", "current defense responsibility",
                "defensive approaches, posts, and muster areas", "weapons, posts, communication, labor, and access",
                "a functional defense program and eligible defenders exist",
                "ca.settlement.defense", "Authority and obligation",
                "ca.space.defended_boundary", "ca.security.security_service");
            yield return Program(OrganizedResearch, "Organized research",
                "Researchers repeatedly investigate questions and preserve results.",
                "conduct and preserve research", "researchers", "the represented settlement and its institutions",
                "research work and knowledge needs exist", "as projects and milestones recur",
                "the recorded research operator", "recognized research standing and policy",
                "research rooms and archives", "research assets, knowledge, labor, power, and access",
                "a functional research program exists", "ca.settlement.research",
                "Work and exchange", "ca.knowledge.research_work",
                "ca.knowledge.knowledge_transmission");
            yield return Program(ReligiousObservance, "Religious observance",
                "Participants repeatedly gather for represented Ideoligion observance.",
                "conduct religious observance", "ritual participants and leaders", "the participating population",
                "a native Ideoligion and supported observance exist", "at represented ritual occasions",
                "the represented Ideoligion community", "native Ideoligion precepts and roles",
                "ritual and gathering space", "participants, ritual objects, place, and access",
                "a functional religion program and native Ideoligion exist",
                "ca.settlement.religion", "Daily and shared life",
                "ca.ritual.religious_observance");
            yield return Program(PublicGathering, "Public gatherings",
                "Residents repeatedly assemble in shared places.",
                "assemble for shared public activity", "participants and conveners", "the attending population",
                "a gathering purpose and reachable place exist", "when public activity is convened",
                "the recorded gathering operator", "recognized access and convening rules",
                "shared gathering places", "reachable shared space, participants, and access",
                "a functional gathering program exists", "ca.settlement.gathering",
                "Daily and shared life", "ca.space.public_gathering");
            yield return Program(SharedRecreation, "Shared recreation",
                "Residents repeatedly take recreation in represented shared settings.",
                "take shared recreation", "resident participants", "the participating population",
                "recreation need and usable facilities exist", "during recurring leisure time",
                "participants or the recorded recreation operator", "recognized access to the activity",
                "recreation areas", "usable recreation assets, time, and access",
                "a functional recreation program exists", "ca.settlement.recreation",
                "Daily and shared life", "ca.recreation.shared_recreation");
            yield return Program(ArtAndRemembrance, "Art and remembrance",
                "Makers and participants repeatedly create or maintain represented works of memory.",
                "create, preserve, or gather around art and memorials", "makers, keepers, and participants",
                "remembered people, events, or shared identity", "a represented commemorative or artistic purpose exists",
                "when works are made, maintained, or visited", "the recorded cultural operator",
                "recognized stewardship of the work", "art, memorial, and performance settings",
                "works, material, skill, labor, and access", "a functional art-and-memory program exists",
                "ca.settlement.art-memory", "Daily and shared life",
                "ca.memory.art_and_remembrance");
            yield return Program(Cultivation, "Cultivation",
                "Workers repeatedly cultivate represented land and harvest it.",
                "plant, tend, and harvest crops", "cultivators", "resident consumers and exchange partners",
                "cultivable ground and food or production demand exist", "through growing and harvest cycles",
                "the recorded agricultural operator", "recognized land, work, and distribution rules",
                "fields and growing areas", "land, seed, labor, knowledge, and access",
                "a functional agriculture program exists", "ca.settlement.agriculture",
                "Work and exchange", "ca.production.cultivation");
            yield return Program(AnimalTending, "Animal tending",
                "Handlers repeatedly care for represented domesticated animals.",
                "feed, shelter, and handle animals", "animal handlers", "kept animals and settlement beneficiaries",
                "represented animals and husbandry demand exist", "through recurring feeding and care",
                "the recorded animal operator", "recognized animal custody and work rules",
                "pens, barns, and grazing areas", "animals, feed, shelter, labor, skill, and access",
                "a functional animal program exists", "ca.settlement.animals",
                "Work and exchange", "ca.production.animal_tending");
            yield return Program(Communications, "Long-range communication",
                "Operators repeatedly exchange represented information over distance.",
                "send and receive long-range information", "communications operators", "known outside counterparts",
                "a represented communication need and counterpart exist", "when reports or negotiations occur",
                "the recorded communications operator", "recognized reporting and contact rules",
                "communications stations", "communications assets, power, knowledge, labor, and access",
                "a functional communications program exists", "ca.settlement.communications",
                "Work and exchange", "ca.knowledge.long_range_communication",
                "ca.exchange.outsider_contact");
            yield return Program(TransportService, "Transport service",
                "Workers repeatedly move people or goods along represented routes.",
                "move people and goods", "drivers, carriers, and route workers", "travelers, residents, and recipients",
                "a represented route and transport demand exist", "as journeys and deliveries recur",
                "the recorded transport operator", "recognized route, access, and property rules",
                "roads, depots, and route nodes", "routes, vehicles or carriers, labor, and access",
                "a functional transport program and route exist", "ca.settlement.transport",
                "Work and exchange", "ca.transport.route_use");
            yield return Program(CommunalProvision, "Communal provision",
                "A communal operator repeatedly distributes represented necessities.",
                "distribute necessities from common stores", "communal provision workers", "the recorded eligible population",
                "represented need and common stock exist", "as needs recur", "the recorded communal operator",
                "current communal access and distribution rules", "common stores and distribution points",
                "stock, labor, nodes, funding where required, and access",
                "a matching active communal provision arrangement exists",
                "ca.settlement.provision.communal", "Food and care",
                "ca.support.shared_provision", "ca.property.common_ownership");
            yield return Program(AuthorityProvision, "Authority provision",
                "A governing operator repeatedly distributes represented necessities.",
                "distribute necessities through public authority", "public provision workers", "the recorded eligible population",
                "represented need, authority, and stock exist", "as needs recur", "the recorded authority operator",
                "current public support and access rules", "public stores and distribution points",
                "stock, labor, authority, nodes, funding, and access",
                "a matching active authority provision arrangement exists",
                "ca.settlement.provision.authority", "Food and care",
                "ca.support.authority_provision", "ca.politics.office_governance");
            yield return Program(HouseholdProvision, "Household provision",
                "Households repeatedly provide represented necessities to their members.",
                "provide necessities within households", "household providers", "recorded household members",
                "represented household need and stock exist", "as household needs recur", "the recorded domestic unit",
                "current domestic membership and provision responsibility", "homes and household stores",
                "household stock, labor, residence, and access",
                "a matching active domestic provision arrangement exists",
                "ca.settlement.provision.domestic", "Food and care",
                "ca.support.household_provision", "ca.membership.household_membership");

            yield return Organization(PublicDeliberation, "Public deliberation",
                "Residents repeatedly take part in decisions that bind their community.",
                "deliberate and decide public questions", "eligible participants and conveners",
                "members and residents subject to the decision", "a policy question is convened",
                "across repeated recorded decisions", "the represented governing organization",
                "current participation and decision rules", "reachable shared meeting places",
                "participants, records, and meeting access", "at least two policy decisions are recorded in the period",
                "CAOrganization.decisionHistory policy entries",
                "CACultureLongitudinalMapComponent and political legitimacy",
                "Authority and obligation", "ca.space.public_gathering",
                "ca.politics.public_voice", "ca.politics.office_governance");
            yield return Organization(ExternalAgreement, "Maintaining outside agreements",
                "Represented organizations repeatedly maintain reciprocal relations with outsiders.",
                "perform an outside agreement", "members and assigned representatives", "the named counterparty",
                "an active agreement creates an obligation or exchange", "while the agreement remains active",
                "the agreement parties and any recorded broker", "the agreement's recorded terms",
                "routes, meeting places, or communication links", "the agreement's represented contributions and access",
                "an active non-broken agreement exists", "CAOrganizationWorldComponent active agreements",
                "agreement runtime, road expansion, and Culture history", "Relations and support",
                "ca.relations.voluntary_agreement", "ca.exchange.outsider_contact",
                "ca.exchange.voluntary_trade");
            yield return Organization(BoundaryPatrol, "Boundary patrols",
                "Assigned defenders repeatedly patrol represented approaches and posts.",
                "patrol defended ground", "assigned guards", "the defended population and ground",
                "a security practice assigns a route or post", "through recurring patrol duty",
                "the represented security organization", "current security assignment",
                "patrol routes, posts, and settlement approaches", "guards, routes, posts, and access",
                "a current security practice and eligible guards exist",
                "CAOrganization.securityPractices",
                "CAPatrolSystemMapComponent and Culture history", "Authority and obligation",
                "ca.space.defended_boundary", "ca.security.security_service");
            yield return Act(TaxCollection, "Tax collection",
                "An authority repeatedly collects represented obligations.",
                "collect a claimed tax", "tax collectors or claiming authority", "represented payers",
                "a tax claim becomes due", "as recorded tax acts recur", "the organization claiming the tax",
                "claimed authority, obligation, procedure, and current property rules",
                "the payer's or authority's represented property setting", "transferable property and collection labor",
                "CAActRecord carries the exact authority, consent, procedure, compensation, and force facts",
                "taxation", "Authority and obligation", "ca.property.taxation",
                "ca.property.compulsory_transfer");
            yield return Act(CombatConduct, "Violence against defeated enemies",
                "Combatants repeatedly harm downed or unresisting enemies.",
                "harm defeated combatants", "combatants and commanders", "downed or unresisting enemies",
                "an enemy is defeated or offers no resistance", "as recorded combat outcomes recur",
                "the combatant's represented organization", "current war-conduct rules and immediate combat facts",
                "the battlefield and aftermath", "weapons, custody capacity, and combat access",
                "CAActRecord identifies circumstance and lethal outcome",
                "violence", "Authority and obligation", "ca.war.quarter_given",
                "ca.war.combat_violence", "ca.custody.humane_treatment");
            yield return Organization(OfficeSuccession, "Office succession",
                "A represented office repeatedly transfers authority through its recorded succession rule.",
                "transfer an office", "officeholders and selectors", "the organization and affected members",
                "an office becomes vacant or changes holder", "at each recorded succession",
                "the organization owning the office", "the office's succession and jurisdiction",
                "the office's represented institutional setting", "a persistent office and eligible participants",
                "an office records prior holder, succession rule, and current holder",
                "CAOrganization.offices and CAOffice succession fields",
                "office authority, political conflict, and Culture history", "Authority and obligation",
                "ca.status.office_holding", "ca.status.kin_succession",
                "ca.politics.office_governance");
            yield return Organization(DelegatedGovernance, "Delegated governance",
                "Member organizations repeatedly exercise and review represented delegated responsibilities.",
                "delegate and exercise limited authority", "member and coordinating organizations", "represented members and jurisdictions",
                "a membership relation delegates named responsibilities", "while the relation remains active",
                "the federation or faction organization", "the relation's exact delegated responsibilities and exit terms",
                "member settlements and coordinating institutions", "persistent organizations, relations, records, and communication",
                "an active organization-membership relation names delegated responsibilities",
                "CAOrganizationRelationsWorldComponent membership relations",
                "federation authority and agreement runtime", "Authority and obligation",
                "ca.politics.delegated_authority", "ca.membership.faction_membership");
            yield return Organization(RepairAndRebuilding, "Repair and rebuilding",
                "Workers repeatedly repair damage and rebuild represented settlement assets.",
                "repair and rebuild settlement assets", "assigned builders and maintainers", "damaged structures and their users",
                "a material asset is damaged or missing", "as repair and rebuilding needs recur",
                "the settlement development organization", "current development authority and work rules",
                "the damaged settlement and work sites", "materials, knowledge, labor, funding, and access",
                "authorized executable development and a real damaged or missing asset exist",
                "CARegionalSettlementRecord development receipts and settlement work history",
                "CASettlementWorksMapComponent", "Work and exchange",
                "ca.production.repair_rebuilding", "ca.space.public_works");
            yield return Native(HumanFleshConsumption, "Eating human flesh",
                "People repeatedly eat human flesh.",
                "consume human flesh directly or as an ingredient",
                "eaters", "human remains", "a represented meal is eaten",
                "as exact native events recur", "Food and daily life",
                CASocialSubjectRegistry.MealPreparation);
            yield return Native(AnimalFoodConsumption, "Eating animal food",
                "People repeatedly eat animal-derived food.",
                "consume animal-derived food", "eaters", "prepared food",
                "an exact native meal event occurs",
                "as exact native events recur", "Animals and food",
                CASocialSubjectRegistry.MealPreparation,
                CASocialSubjectRegistry.AnimalTending);
            yield return Native(UnfamiliarFoodConsumption,
                "Eating unfamiliar foods",
                "People repeatedly eat fungal, insect, or processed food.",
                "consume fungal, insect, or processed food", "eaters",
                "prepared food", "an exact native meal event occurs",
                "as exact native events recur", "Food and daily life",
                CASocialSubjectRegistry.MealPreparation);
            yield return Native(HumanButchery, "Butchering human remains",
                "People repeatedly butcher human remains.",
                "butcher a human corpse", "butchers", "human remains",
                "a human corpse is butchered", "as exact native events recur",
                "Death and remembrance", CASocialSubjectRegistry.ArtAndRemembrance);
            yield return Native(CorpseExposure, "Leaving human remains exposed",
                "Residents repeatedly encounter human remains left exposed.",
                "leave or encounter exposed human remains", "residents",
                "human remains", "a corpse remains in lived space",
                "as exact native events recur", "Death and remembrance",
                CASocialSubjectRegistry.ArtAndRemembrance);
            yield return Native(OrganExtraction, "Extracting human organs",
                "People repeatedly extract human organs.",
                "extract a human organ", "medical actors",
                "patients or captives", "an exact organ-extraction event occurs",
                "as exact native events recur", "Body and care",
                CASocialSubjectRegistry.MedicalCare,
                CASocialSubjectRegistry.HumaneCustody);
            yield return Native(OrganTrade, "Trading human organs",
                "People repeatedly buy or sell human organs.",
                "buy or sell a human organ", "traders", "human organs",
                "an exact organ-trade event occurs",
                "as exact native events recur", "Body and exchange",
                CASocialSubjectRegistry.VoluntaryTrade,
                CASocialSubjectRegistry.MedicalCare);
            yield return Native(BodyModification, "Altering bodies",
                "People repeatedly alter bodies through implants or biosculpting.",
                "deliberately alter a body", "medical actors and recipients",
                "living bodies", "a represented alteration occurs",
                "as exact native events recur", "Body and care",
                CASocialSubjectRegistry.MedicalCare);
            yield return Native(RitualInjury, "Ritual bodily injury",
                "People repeatedly undergo scarification or blinding.",
                "scarify or blind a participant", "participants and operators",
                "living bodies", "a represented injury is completed",
                "as exact native events recur", "Body and ritual",
                CASocialSubjectRegistry.ReligiousObservance,
                CASocialSubjectRegistry.MedicalCare);
            yield return Native(ExclusiveMarriage, "Exclusive marriage",
                "People repeatedly form marriages with one spouse.",
                "form a marriage with one spouse", "spouses", "households",
                "an exact native marriage event records one spouse",
                "as exact native events recur", "Relationships and family",
                CASocialSubjectRegistry.HouseholdMembership);
            yield return Native(PluralMarriage, "Plural marriage",
                "People repeatedly form marriages with multiple spouses.",
                "form a marriage with multiple spouses", "spouses",
                "households",
                "an exact native marriage event records multiple spouses",
                "as exact native events recur", "Relationships and family",
                CASocialSubjectRegistry.HouseholdMembership);
            yield return Native(NonSpousalIntimacy, "Intimacy outside marriage",
                "People repeatedly form intimate relationships outside marriage.",
                "begin or share consensual intimacy outside marriage",
                "partners", "partners", "an exact native relationship event occurs",
                "as exact native events recur", "Relationships and family",
                CASocialSubjectRegistry.HouseholdMembership);
            yield return Native(DrugUse, "Recreational drug use",
                "People repeatedly ingest recreational drugs.",
                "ingest a recreational drug", "users",
                "living people", "an exact native drug event occurs",
                "as exact native events recur", "Food and daily life",
                CASocialSubjectRegistry.MedicalCare,
                CASocialSubjectRegistry.VoluntaryTrade);
            yield return Native(DrugAdministration,
                "Administering recreational drugs",
                "People repeatedly administer recreational drugs to others.",
                "administer a recreational drug", "medical actors",
                "living people", "an exact native administration event occurs",
                "as exact native events recur", "Body and care",
                CASocialSubjectRegistry.MedicalCare);
            yield return Native(DoctrinalChange, "Changing Ideoligion",
                "People repeatedly change Ideoligion.",
                "change Ideoligion", "believers", "Ideoligion communities",
                "an exact native doctrinal event occurs",
                "as exact native events recur", "Belief and membership",
                CASocialSubjectRegistry.ReligiousObservance,
                CASocialSubjectRegistry.FactionMembership);
            yield return Native(CrossIdeoligionObservance,
                "Joining another Ideoligion's ritual",
                "People repeatedly participate in another Ideoligion's ritual.",
                "participate in another Ideoligion's ritual", "participants",
                "another Ideoligion community",
                "an exact native cross-Ideoligion ritual event occurs",
                "as exact native events recur", "Belief and membership",
                CASocialSubjectRegistry.ReligiousObservance,
                CASocialSubjectRegistry.OutsiderContact);
            yield return Native(AnimalSlaughter, "Slaughtering animals",
                "People repeatedly slaughter animals for represented use.",
                "slaughter an animal for food or use", "handlers",
                "animals", "an exact native slaughter event occurs",
                "as exact native events recur", "Animals and food",
                CASocialSubjectRegistry.AnimalTending,
                CASocialSubjectRegistry.MealPreparation);
            yield return Native(InnocentAnimalKilling,
                "Killing non-hostile animals",
                "People repeatedly kill animals that were not hostile.",
                "kill a non-hostile animal", "attackers", "animals",
                "an exact native innocent-animal event occurs",
                "as exact native events recur", "Animals and violence",
                CASocialSubjectRegistry.AnimalTending,
                CASocialSubjectRegistry.CombatViolence);
            yield return Native(ResourceExtraction, "Extracting local resources",
                "People repeatedly cut trees or mine local ground.",
                "cut trees or mine material", "workers", "local land",
                "an exact native extraction event occurs",
                "as exact native events recur", "Land and resources",
                CASocialSubjectRegistry.Cultivation,
                CASocialSubjectRegistry.GeneralCraft);
            yield return Native(SettlementAbandonment,
                "Abandoning settlements",
                "Communities repeatedly abandon established settlements.",
                "abandon an established settlement", "resident communities",
                "settled places", "an exact native abandonment event occurs",
                "as exact native events recur", "Settlement and migration",
                CASocialSubjectRegistry.HouseholdMembership);
            yield return Native(Raiding, "Raiding other settlements",
                "People repeatedly raid other settlements.",
                "raid an outside settlement", "raiders",
                "outside people and property", "an exact native raid occurs",
                "as exact native events recur", "Violence and property",
                CASocialSubjectRegistry.CombatViolence,
                CASocialSubjectRegistry.Confiscation);
            yield return Native(DownedPersonStripping,
                "Stripping downed people",
                "People repeatedly strip possessions from downed people.",
                "strip a downed person", "takers", "downed people",
                "an exact native downed-person stripping event occurs",
                "as exact native events recur", "Violence and property",
                CASocialSubjectRegistry.Confiscation,
                CASocialSubjectRegistry.HumaneCustody);
            yield return Native(InterpersonalViolence,
                "Interpersonal violence",
                "People repeatedly attack other or non-hostile people.",
                "attack another person", "attackers", "other people",
                "an exact supported interpersonal-violence event occurs",
                "as exact native events recur", "Violence and order",
                CASocialSubjectRegistry.CombatViolence);
            yield return Native(Enslavement, "Enslaving people",
                "People repeatedly enslave or sell other people.",
                "enslave or sell a person", "captors and traders", "captives",
                "an exact native slavery event occurs",
                "as exact native events recur", "Work and captivity",
                CASocialSubjectRegistry.CompelledService,
                CASocialSubjectRegistry.HumaneCustody);
            yield return Native(Execution, "Executing captives",
                "People repeatedly execute colonists, guests, or captives.",
                "execute a person in custody", "executioners and authorities",
                "condemned people", "an exact native execution event occurs",
                "as exact native events recur", "Violence and captivity",
                CASocialSubjectRegistry.HumaneCustody,
                CASocialSubjectRegistry.CustodyPunishment,
                CASocialSubjectRegistry.CombatViolence);
        }

        private static CACulturalPracticeDef Program(string key, string label,
            string summary, string activity, string actor, string target,
            string trigger, string cadence, string op, string authority,
            string setting, string material, string conditions,
            string programKey, string facet, params string[] subjects)
        {
            return Def(key, label, summary, activity, actor, target, trigger,
                cadence, op, authority, setting, material, conditions,
                "CASettlementOperationalFact " + programKey,
                "CASettlementProgramCausalKernel, runtime contract, and Culture history",
                facet, subjects, programKey, null);
        }

        private static CACulturalPracticeDef Act(string key, string label,
            string summary, string activity, string actor, string target,
            string trigger, string cadence, string op, string authority,
            string setting, string material, string conditions,
            string actKind, string facet, params string[] subjects)
        {
            return Def(key, label, summary, activity, actor, target, trigger,
                cadence, op, authority, setting, material, conditions,
                "CAActLedger CAActRecord kind " + actKind,
                "CASocialActAdapter, political response, and Culture history",
                facet, subjects, null, actKind);
        }

        private static CACulturalPracticeDef Organization(string key,
            string label, string summary, string activity, string actor,
            string target, string trigger, string cadence, string op,
            string authority, string setting, string material,
            string conditions, string evidence, string consumer,
            string facet, params string[] subjects)
        {
            return Def(key, label, summary, activity, actor, target, trigger,
                cadence, op, authority, setting, material, conditions,
                evidence, consumer, facet, subjects, null, null);
        }

        private static CACulturalPracticeDef Native(string key, string label,
            string summary, string activity, string actor, string target,
            string trigger, string cadence, string facet,
            params string[] subjects)
        {
            return Def(key, label, summary, activity, actor, target, trigger,
                cadence, "the people who perform the recorded act",
                "native Ideoligion, institutions, and Culture remain separate",
                "the exact map where the event occurred",
                "the native action and its actual material requirements",
                "HistoryEventsManager recorded the exact supported event",
                "HistoryEventsManager exact semantic adapter registry",
                "Culture history and social interpretation", facet, subjects,
                null, null);
        }

        private static CACulturalPracticeDef Def(string key, string label,
            string summary, string activity, string actor, string target,
            string trigger, string cadence, string op, string authority,
            string setting, string material, string conditions,
            string evidence, string consumer, string facet, string[] subjects,
            string programKey, string actKind)
        {
            return new CACulturalPracticeDef
            {
                Owner = "ca",
                Key = key,
                Label = label,
                Summary = summary,
                Activity = activity,
                ActorRole = actor,
                TargetRole = target,
                Trigger = trigger,
                Cadence = cadence,
                Operator = op,
                AuthorityBasis = authority,
                Setting = setting,
                MaterialRequirements = material,
                Conditions = conditions,
                EvidenceSource = evidence,
                RuntimeConsumer = consumer,
                PrimaryFacet = facet,
                SubjectKeys = subjects.ToList(),
                ProgramKey = programKey,
                ActKind = actKind
            };
        }
    }

    public static class CAAuthoringCategoryPolicy
    {
        public const int TopLevelMinimumItems = 4;

        public static IReadOnlyList<string> NavigableGroups(
            IEnumerable<KeyValuePair<string, int>> cardinalities)
        {
            List<string> groups = (cardinalities
                    ?? Enumerable.Empty<KeyValuePair<string, int>>())
                .Where(value => !string.IsNullOrWhiteSpace(value.Key)
                    && value.Value >= TopLevelMinimumItems)
                .Select(value => value.Key).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToList();
            return groups.Count >= 2 ? groups : new List<string>();
        }
    }

    public static class CAAuthoringControlContracts
    {
        public static readonly IReadOnlyList<CAAuthoringControlContract> All =
            new[]
            {
                Control("culture.identity", CAAuthoringSemanticKind.OptionalUnset,
                    "CACulture", "population Culture",
                    CAAuthoringTemporalStatus.Current, "zero or one name",
                    "coexists with every social meaning, practice, constituent, and visual tradition",
                    "player-authored name", new[] { "CACulture.name" },
                    "Culture summaries and identity continuity"),
                Control("culture.visual-tradition",
                    CAAuthoringSemanticKind.ExclusiveCategorical,
                    "CACulture", "population Culture",
                    CAAuthoringTemporalStatus.Current,
                    "zero or one native CultureDef",
                    "coexists with social state; only visual traditions are mutually exclusive",
                    "one native visual tradition supplies styles at a time",
                    "player choice or native faction/Ideoligion source",
                    new[] { "CACulture.sourceCultureDefName" },
                    "native RimWorld style resolution"),
                Control("culture.question-distributions",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CACulture", "represented population",
                    CAAuthoringTemporalStatus.Normative,
                    "zero or one distribution per registered question and population scope; many distinct pairs may coexist",
                    "whole-population and subgroup rows may coexist; an exact scoped row owns its population",
                    "player-authored initial distribution or evidenced local change",
                    new[] { "CACultureQuestionDistribution.questionKey",
                        "CACultureQuestionDistribution.populationScope",
                        "CACultureQuestionDistribution.mean",
                        "CACultureQuestionDistribution.spread",
                        "CACultureQuestionDistribution.salience",
                        "CACultureQuestionDistribution.normStrength",
                        "CACultureQuestionDistribution.visibility",
                        "CACultureQuestionDistribution.sourceConfidence",
                        "CACultureQuestionDistribution.toleranceForDivergence",
                        "CACultureQuestionDistribution.subgroups[]" },
                    "pawn attitudes, social influence, political support, legitimacy, knowledge access, and behavior appraisal"),
                Control("culture.practice",
                    CAAuthoringSemanticKind.ReadOnlyRealized,
                    "CACulture", "population or established settlement Culture",
                    CAAuthoringTemporalStatus.Historical,
                    "zero or more registered concrete practices",
                    "several and even contradictory practices may coexist when factual evidence supports them",
                    "longitudinal observation adapter; read-only at the Culture authoring surface",
                    new[] { "CACulturePractice.practiceKey",
                        "CACulturePractice.strength",
                        "CACulturePractice.sourceOwner",
                        "CACulturePractice.sourceSignature" },
                    "Culture history, expression, and interpretation"),
                Control("culture.constituents",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CACulture", "established local Culture",
                    CAAuthoringTemporalStatus.Current,
                    "one or more population-linked constituent rows whose shares form the represented whole",
                    "several inherited and local sources may coexist",
                    "scenario authoring or realized population composition",
                    new[] { "CACulture.constituents[].cultureId",
                        "CACulture.constituents[].label",
                        "CACulture.constituents[].share",
                        "CACulture.constituents[].inherited" },
                    "meaning resolution, expression, and longitudinal continuity"),
                Control("culture.transitions",
                    CAAuthoringSemanticKind.ReadOnlyRealized,
                    "CACultureHistory", "realized local Culture",
                    CAAuthoringTemporalStatus.Historical,
                    "zero or more ordered transition receipts",
                    "successive factual transitions coexist as history",
                    "derived from repeated evidence; read-only at the authoring surface",
                    new[] { "CACulture.transitions[]" },
                    "Culture history, expression, and operator inspection"),
                Control("politics.order",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CAPoliticalBeliefs", "faction or founding population",
                    CAAuthoringTemporalStatus.Normative,
                    "one complete political question state per registered subject",
                    "exclusive subjects select one position; blendable subjects distribute 100 points across compatible positions",
                    "player choice, complete preset, or evidenced generation",
                    new[] { "CAPoliticalBeliefs.questions[].questionKey",
                        "CAPoliticalBeliefs.questions[].options[].optionKey",
                        "CAPoliticalBeliefs.questions[].options[].share",
                        "CAPoliticalBeliefs.questions[].options[].source" },
                    "generated political identity and account, founding suggestions, legitimacy, conflict, ownership, provision, and comparison with represented institutions"),
                Control("politics.represented-institutions",
                    CAAuthoringSemanticKind.MultiValuedSet,
                    "CAFactionState", "established faction",
                    CAAuthoringTemporalStatus.Current,
                    "zero or more instituted mechanisms per political subject",
                    "mechanisms coexist except an explicit absence mechanism excludes standing mechanisms on the same subject",
                    "scenario authoring or represented institutional evidence",
                    new[] { "CAFactionState.factionStructure[].axis",
                        "CAFactionState.factionStructure[].option" },
                    "organizations, offices, security, work, property, and belief-practice tension"),
                Control("politics.order-identity",
                    CAAuthoringSemanticKind.Derived,
                    "CAPoliticalBeliefs", "political order",
                    CAAuthoringTemporalStatus.Normative,
                    "one generated name and one generated account",
                    "the complete political variables remain authoritative; an optional custom name does not replace them",
                    "read-only deterministic projection of the saved political variables; an optional display-name override changes identity text only",
                    new[] { "CAPoliticalBeliefs.name",
                        "CAPoliticalBeliefs.nameAuthored",
                        "CAPoliticalBeliefs.nameRoll" },
                    "player-facing identity, summaries, presets, and inspection"),
                Control("faction.name",
                    CAAuthoringSemanticKind.OptionalUnset,
                    "CARegionalFactionPlan", "established faction",
                    CAAuthoringTemporalStatus.Current,
                    "zero or one authored display name",
                    "coexists with type, Culture, Ideoligion, Political Order, and represented institutions",
                    "scenario authoring",
                    new[] { "CARegionalFactionPlan.customName" },
                    "native faction materialization and summaries"),
                Control("faction.type",
                    CAAuthoringSemanticKind.ExclusiveCategorical,
                    "CARegionalFactionPlan", "established faction",
                    CAAuthoringTemporalStatus.Current,
                    "exactly one existing or new native faction source and one resolved FactionDef",
                    "independent of authored social state",
                    "one native RimWorld faction has one FactionDef",
                    "scenario authoring or selected existing faction",
                    new[] { "CARegionalFactionPlan.source",
                        "CARegionalFactionPlan.existingFactionLoadId",
                        "CARegionalFactionPlan.customFactionDefName" },
                    "world-faction creation and native integration"),
                Control("faction.player-relation",
                    CAAuthoringSemanticKind.ExclusiveCategorical,
                    "CARegionalFactionPlan", "player-to-faction relation",
                    CAAuthoringTemporalStatus.Current,
                    "one ally, neutral, or hostile relation",
                    "independent of every inter-NPC relation row",
                    "RimWorld stores one FactionRelationKind for one faction pair",
                    "scenario authoring or retained native relation",
                    new[] { "CARegionalFactionPlan.playerRelation",
                        "CARegionalFactionPlan.authorPlayerRelation" },
                    "native goodwill/relation materialization and regional outcomes"),
                Control("faction.federation-membership",
                    CAAuthoringSemanticKind.Relational,
                    "CARegionalFactionPlan", "faction-to-federation relation",
                    CAAuthoringTemporalStatus.Current,
                    "zero or one federation membership per faction; several factions per federation",
                    "coexists with local authority and independent faction identity",
                    "scenario authoring",
                    new[] { "CARegionalFactionPlan.federationKey",
                        "CARegionalFactionPlan.federationKind" },
                    "federation organizations, shared responsibilities, and summaries"),
                Control("region.faction-relation",
                    CAAuthoringSemanticKind.Relational,
                    "CARegionalRelationPlan", "two established factions",
                    CAAuthoringTemporalStatus.Current,
                    "one persisted row per unordered faction pair",
                    "different pairs coexist; a pair has one current relation",
                    "scenario authoring or retained generated relation",
                    new[] { "CARegionalRelationPlan.leftFactionKey",
                        "CARegionalRelationPlan.rightFactionKey",
                        "CARegionalRelationPlan.relation",
                        "CARegionalRelationPlan.source" },
                    "native relations, regional pattern, conflict, and connections"),
                Control("settlement.owner",
                    CAAuthoringSemanticKind.Relational,
                    "CARegionalSettlementPlan", "settlement-to-faction relation",
                    CAAuthoringTemporalStatus.Current,
                    "zero or one owning faction per inhabited site",
                    "ownership is independent of support, resident affiliation, and local authority",
                    "direct scenario authoring or an authorized world-pool source",
                    new[] {
                        "CARegionalSettlementPlan.factionLinks.ownership",
                        "CARegionalSettlementPlan.factionLinks.ownerRegionalFactionKey",
                        "CARegionalSettlementPlan.factionLinks.ownerWorldFactionLoadId"
                    },
                    "native ownership adapters, relations, summaries, and regional hierarchy"),
                Control("settlement.support",
                    CAAuthoringSemanticKind.Relational,
                    "CARegionalSettlementPlan", "settlement-to-faction support relation",
                    CAAuthoringTemporalStatus.Current,
                    "zero or one supporting faction per inhabited site",
                    "support may coexist with no owner and never implies ownership or resident affiliation",
                    "direct scenario authoring or represented material support",
                    new[] {
                        "CARegionalSettlementPlan.factionLinks.support",
                        "CARegionalSettlementPlan.factionLinks.supportRegionalFactionKey",
                        "CARegionalSettlementPlan.factionLinks.supportWorldFactionLoadId"
                    },
                    "habitat viability, material support, summaries, and epistemic reports"),
                Control("settlement.local-society",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CARegionalSettlementPlan or CAFrontierHoldingPlan",
                    "ownerless inhabited site or explicit local divergence",
                    CAAuthoringTemporalStatus.Current,
                    "one complete local Political Order, Technological Knowledge composition, and represented institution set",
                    "local Culture and population Ideoligions retain their existing owners",
                    "direct scenario authoring, detachment snapshot, or represented local history",
                    new[] {
                        "CARegionalSettlementPlan.localCulture",
                        "CASiteLocalSocietyState.explicitLocalDivergence",
                        "CASiteLocalSocietyState.politicalOrder",
                        "CASiteLocalSocietyState.technologicalKnowledge",
                        "CASiteLocalSocietyState.institutions"
                    },
                    "political behavior, knowledge-gated viability and production, organizations, offices, programs, provision, and summaries"),
                Control("settlement.location",
                    CAAuthoringSemanticKind.Relational,
                    "CARegionalSettlementPlan", "settlement-to-region ground",
                    CAAuthoringTemporalStatus.Current,
                    "one assigned member tile and one optional physical cluster per settlement",
                    "several settlements may share a represented cluster only when explicitly grouped",
                    "direct map authoring or deterministic placement",
                    new[] { "CARegionalSettlementPlan.memberTileId",
                        "CARegionalSettlementPlan.siteClusterKey" },
                    "map projection, access, distance, connections, and generation"),
                Control("settlement.population-group",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CASettlementPopulationGroup", "established settlement population",
                    CAAuthoringTemporalStatus.Current,
                    "one or more identified groups whose shares form the population",
                    "different faction, political-belief, Culture, and Ideoligion affiliations may coexist; Unaffiliated is explicit and independent of site ownership",
                    "scenario authoring or population-source realization",
                    new[] { "CASettlementPopulationGroup.kind",
                        "CASettlementPopulationGroup.isPrimary",
                        "CASettlementPopulationGroup.label",
                        "CASettlementPopulationGroup.factionKey",
                        "CASettlementPopulationGroup.politicalBeliefsFactionKey" },
                    "pawn materialization, Culture composition, beliefs, provisions, and settlement politics"),
                Control("settlement.population-share",
                    CAAuthoringSemanticKind.ScalarContinuous,
                    "CASettlementPopulationGroup", "one settlement population group",
                    CAAuthoringTemporalStatus.Current,
                    "integer percentage from zero to one hundred; represented groups total one hundred",
                    "several group shares coexist within the fixed total",
                    "scenario authoring or population realization",
                    new[] { "CASettlementPopulationGroup.share" },
                    "pawn counts, Culture weighting, provisions, and political composition"),
                Control("settlement.population-ideoligion",
                    CAAuthoringSemanticKind.Relational,
                    "CASettlementPopulationGroup", "population-to-native-Ideoligion relation",
                    CAAuthoringTemporalStatus.Current,
                    "one inherited faction source or exact existing native Ideoligion identity per population group",
                    "different population groups may hold different Ideoligions",
                    "scenario authoring from an existing native Ideoligion",
                    new[] { "CASettlementPopulationGroup.ideoligionFactionKey",
                        "CASettlementPopulationGroup.nativeIdeoligionId",
                        "CASettlementPopulationGroup.ideoligionCertainty",
                        "CASettlementPopulationGroup.ideoligionProtected" },
                    "native pawn Ideoligion assignment, certainty, and settlement composition"),
                Control("settlement.established-programs",
                    CAAuthoringSemanticKind.MultiValuedSet,
                    "CASettlementOperationalFact", "established settlement",
                    CAAuthoringTemporalStatus.Historical,
                    "zero or more complete operating facts keyed by program and operator",
                    "distinct programs and operators coexist",
                    "scenario authoring of represented pre-start operation",
                    new[] { "CARegionalSettlementPlan.operationalFacts[]" },
                    "program realization, material assets, services, work, provisions, and Culture practice evidence"),
                Control("settlement.provision-arrangement",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CAProvisionArrangement", "established settlement",
                    CAAuthoringTemporalStatus.Current,
                    "zero or more provision arrangements with operator, access, funding, distribution, stock, and reach",
                    "several arrangements may serve different populations or needs",
                    "generated from facts and optionally customized per arrangement",
                    new[] { "CARegionalSettlementPlan.provisionArrangements[]" },
                    "starting stock, facilities, access, taxation, shared work, and household provision"),
                Control("settlement.realized-scale",
                    CAAuthoringSemanticKind.Derived,
                    "CASettlementRealization", "established settlement",
                    CAAuthoringTemporalStatus.Derived,
                    "one persisted scale and support receipt per settlement",
                    "coexists with independently authoritative population, land, access, services, trade, role, and history",
                    "read-only derivation from represented causes",
                    new[] { "CARegionalSettlementPlan.realizedScale",
                        "CARegionalSettlementPlan.urbanSupport" },
                    "map generation, summaries, provisions, and regional role"),
                Control("region.arrival-area",
                    CAAuthoringSemanticKind.Relational,
                    "CARegionalPlan", "player landing-to-region ground",
                    CAAuthoringTemporalStatus.Current,
                    "one selected starting member tile within the region",
                    "independent of faction and settlement placement",
                    "direct landing selection",
                    new[] { "CARegionalPlan.startTileId" },
                    "arrival projection and map generation"),
                Control("world.stitched-region-frequency",
                    CAAuthoringSemanticKind.ScalarContinuous,
                    "CARegionalWorldPolicy", "world generation",
                    CAAuthoringTemporalStatus.Normative,
                    "one authored probability range and one read-only realized world frequency",
                    "independent of stitched-region size",
                    "world-tendency authoring; realized once from world seed",
                    new[] { "CARegionalWorldPolicy.stitchedRegionFrequencyMin",
                        "CARegionalWorldPolicy.stitchedRegionFrequencyMax" },
                    "connected-region membership realization"),
                Control("world.stitched-region-size",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CARegionalWorldPolicy", "world generation",
                    CAAuthoringTemporalStatus.Normative,
                    "one minimum and maximum requested connected-region extent",
                    "independent of stitched-region frequency",
                    "world-tendency authoring",
                    new[] { "CARegionalWorldPolicy.stitchedRegionSizeMin",
                        "CARegionalWorldPolicy.stitchedRegionSizeMax" },
                    "connected-region extent realization"),
                Control("world.settlement-concentration",
                    CAAuthoringSemanticKind.ScalarContinuous,
                    "CARegionalWorldPolicy", "major-settlement placement",
                    CAAuthoringTemporalStatus.Normative,
                    "one zero-to-one placement propensity",
                    "independent of settlement count and realized pattern",
                    "world-tendency authoring",
                    new[] { "CARegionalWorldPolicy.settlementConcentration" },
                    "candidate placement before pattern classification"),
                Control("world.urban-growth",
                    CAAuthoringSemanticKind.ScalarContinuous,
                    "CARegionalWorldPolicy", "settlement-scale derivation",
                    CAAuthoringTemporalStatus.Normative,
                    "one zero-to-one threshold modifier",
                    "cannot create population, land, access, service, trade, role, or history",
                    "world-tendency authoring",
                    new[] { "CARegionalWorldPolicy.worldDevelopment" },
                    "urban-support threshold only"),
                Control("world.frontier-frequency",
                    CAAuthoringSemanticKind.ScalarContinuous,
                    "CARegionalWorldPolicy", "frontier-holding placement",
                    CAAuthoringTemporalStatus.Normative,
                    "one zero-to-one suitability acceptance propensity",
                    "independent of each realized holding's size",
                    "world-tendency authoring",
                    new[] { "CARegionalWorldPolicy.frontierHoldingFrequency" },
                    "number of suitable frontier holdings"),
                Control("world.frontier-size",
                    CAAuthoringSemanticKind.ScalarContinuous,
                    "CARegionalWorldPolicy", "realized frontier holding",
                    CAAuthoringTemporalStatus.Normative,
                    "one zero-to-one household and material-form propensity",
                    "applies only after a suitable holding exists",
                    "world-tendency authoring",
                    new[] { "CARegionalWorldPolicy.frontierHoldingSize" },
                    "resident count and material form per holding"),
                Control("world.source-variety",
                    CAAuthoringSemanticKind.ScalarContinuous,
                    "CARegionalWorldPolicy", "world-pool settlement reallocation",
                    CAAuthoringTemporalStatus.Normative,
                    "one zero-to-one source-owner variety propensity",
                    "never creates a faction or settlement",
                    "world-tendency authoring",
                    new[] { "CARegionalWorldPolicy.reallocationSourceVariety" },
                    "selection among authorized world-settlement sources"),
                Control("world.off-map-activity",
                    CAAuthoringSemanticKind.ScalarContinuous,
                    "CARegionalWorldPolicy", "distant represented societies",
                    CAAuthoringTemporalStatus.Normative,
                    "one zero-to-one founding cadence modifier",
                    "does not author political or institutional state; political-belief pulse cadence is now derived",
                    "world-tendency authoring",
                    new[] { "CARegionalWorldPolicy.distantFoundingRate" },
                    "frequency of distant faction founding of new settlements"),
                Control("founding.arrangement",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CAFoundingArrangement", "player founding moment",
                    CAAuthoringTemporalStatus.Current,
                    "one adopted initial arrangement with independently authored terms",
                    "coexists with inherited Culture, Ideoligion, and Political Order and may disagree with it",
                    "player choice or explicit generated suggestion",
                    new[] { "CAFoundingArrangement.leaderRule",
                        "CAFoundingArrangement.workRequired",
                        "CAFoundingArrangement.foundersDecide",
                        "CAFoundingArrangement.sharedSupplies",
                        "CAFoundingArrangement.durationDays" },
                    "initial organization, supplies, work, authority, and belief-practice readings")
            };

        public static bool Validate(out string failure)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CAAuthoringControlContract contract in All)
            {
                if (!keys.Add(contract.Key))
                {
                    failure = "duplicate authoring control " + contract.Key;
                    return false;
                }
                if (!contract.IsComplete(out failure)) return false;
            }
            failure = null;
            return true;
        }

        private static CAAuthoringControlContract Control(string key,
            CAAuthoringSemanticKind kind, string owner, string scope,
            CAAuthoringTemporalStatus temporal, string cardinality,
            string coexistence, string authorship, IEnumerable<string> fields,
            params string[] consumers)
        {
            return Control(key, kind, owner, scope, temporal, cardinality,
                coexistence, null, authorship, fields, consumers);
        }

        private static CAAuthoringControlContract Control(string key,
            CAAuthoringSemanticKind kind, string owner, string scope,
            CAAuthoringTemporalStatus temporal, string cardinality,
            string coexistence, string exclusivity, string authorship,
            IEnumerable<string> fields, params string[] consumers)
        {
            return new CAAuthoringControlContract
            {
                Key = key,
                SemanticKind = kind,
                AuthoritativeOwner = owner,
                Scope = scope,
                TemporalStatus = temporal,
                Cardinality = cardinality,
                Coexistence = coexistence,
                ExclusivityInvariant = exclusivity,
                Authorship = authorship,
                FieldsWritten = fields.ToList(),
                Consumers = consumers.ToList()
            };
        }
    }
}
