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

        public static IReadOnlyList<CACulturalPracticeDef> All => Values.Values
            .OrderBy(value => value.Label, StringComparer.Ordinal).ToList();

        public static bool ValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            string[] parts = key.Split('.');
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
                || practice.Key == RepairAndRebuilding;
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

    public enum CAPoliticalPatchTarget : byte
    {
        NormativeBeliefs = 0,
        CurrentOrder = 1
    }

    public sealed class CAPoliticalPatchTemplate
    {
        public string Key;
        public string Label;
        public string Summary;
        public CAPoliticalPatchTarget Target;
        public string Domain;
        public Dictionary<string, List<string>> Mechanisms =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public string MergeBehavior =
            "Add the listed mechanisms; preserve every unlisted and already-authored mechanism.";

        public IEnumerable<string> ExactWrites()
        {
            return Mechanisms.OrderBy(value => value.Key,
                    StringComparer.Ordinal)
                .SelectMany(value => value.Value.OrderBy(item => item,
                    StringComparer.Ordinal).Select(item => value.Key + "=" + item));
        }
    }

    public static class CAPoliticalPatchTemplates
    {
        public static readonly IReadOnlyList<CAPoliticalPatchTemplate> Beliefs =
            new[]
            {
                Template("shared_council", "Shared council",
                    "Adds a standing council, majority decisions, broad participation, and protected dissent.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Authority",
                    P("leadership", "council"), P("decisions", "majority"),
                    P("participation", "universal"), P("dissent", "plural")),
                Template("delegated_federation", "Delegated federation",
                    "Adds delegated leaders, consensus decisions, and member participation.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Authority",
                    P("leadership", "federated"), P("decisions", "consensus"),
                    P("participation", "members")),
                Template("central_executive", "Central executive",
                    "Adds a single executive and decree as legitimate authority mechanisms.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Authority",
                    P("leadership", "single"), P("decisions", "decree")),
                Template("customary_standing", "Customary standing",
                    "Adds customary decisions, household participation, customary dissent rules, and hereditary status.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Authority",
                    P("decisions", "custom"), P("participation", "heads"),
                    P("dissent", "customary"), P("status", "hereditary")),
                Template("cooperative_production", "Cooperative production",
                    "Adds cooperative ownership and organized worker control without selecting authority or membership.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Property and work",
                    P("ownership", "cooperative"), P("work", "organized")),
                Template("common_provision", "Common provision",
                    "Adds common ownership, shared stores, and communal support.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Property and work",
                    P("ownership", "common"), P("economy", "communal"),
                    P("support", "communal")),
                Template("private_trade", "Private trade",
                    "Adds private ownership, market exchange, hired work, and household provision.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Property and work",
                    P("ownership", "private"), P("economy", "market"),
                    P("work", "contract"), P("support", "private")),
                Template("public_service", "Public service",
                    "Adds required public work and authority provision without choosing ownership.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Property and work",
                    P("work", "duty"), P("support", "public")),
                Template("open_equal_membership", "Open and equal membership",
                    "Adds open admission and broad equality.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Membership",
                    P("membership", "open"), P("status", "equal")),
                Template("hereditary_membership", "Hereditary membership",
                    "Adds inherited membership and hereditary standing.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Membership",
                    P("membership", "hereditary"), P("status", "hereditary")),
                Template("community_defense", "Community defense",
                    "Adds a community watch, militia service, and quarter for defeated enemies.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Security",
                    P("localOrder", "watch"), P("defense", "militia"),
                    P("warConduct", "quarter")),
                Template("professional_security", "Professional security",
                    "Adds guards, professional defense, and combatant-only force.",
                    CAPoliticalPatchTarget.NormativeBeliefs, "Security",
                    P("localOrder", "constabulary"), P("defense", "professional"),
                    P("warConduct", "combatants"))
            };

        public static readonly IReadOnlyList<CAPoliticalPatchTemplate> CurrentOrder =
            new[]
            {
                Template("standing_council", "Standing council",
                    "Adds a council, majority procedure, and member participation to current authority.",
                    CAPoliticalPatchTarget.CurrentOrder, "Authority",
                    P("leadership", "council"), P("decisions", "majority"),
                    P("participation", "members")),
                Template("single_executive", "Single executive",
                    "Adds a single executive and decree to current authority.",
                    CAPoliticalPatchTarget.CurrentOrder, "Authority",
                    P("leadership", "single"), P("decisions", "decree")),
                Template("delegated_councils", "Delegated councils",
                    "Adds delegated leaders and consensus procedure to current authority.",
                    CAPoliticalPatchTarget.CurrentOrder, "Authority",
                    P("leadership", "federated"), P("decisions", "consensus")),
                Template("public_assembly", "Public assembly",
                    "Adds all-member leadership, consensus, and resident participation.",
                    CAPoliticalPatchTarget.CurrentOrder, "Authority",
                    P("leadership", "whole"), P("decisions", "consensus"),
                    P("participation", "universal")),
                Template("cooperative_workplaces", "Cooperative workplaces",
                    "Adds cooperative ownership and organized work to the current economy.",
                    CAPoliticalPatchTarget.CurrentOrder, "Property and work",
                    P("ownership", "cooperative"), P("work", "organized")),
                Template("common_stores", "Common stores",
                    "Adds common ownership, shared distribution, and communal support.",
                    CAPoliticalPatchTarget.CurrentOrder, "Property and work",
                    P("ownership", "common"), P("economy", "communal"),
                    P("support", "communal")),
                Template("private_market", "Private market",
                    "Adds private ownership, market exchange, and hired work.",
                    CAPoliticalPatchTarget.CurrentOrder, "Property and work",
                    P("ownership", "private"), P("economy", "market"),
                    P("work", "contract")),
                Template("public_distribution", "Public distribution",
                    "Adds faction ownership, planned distribution, required service, and public support.",
                    CAPoliticalPatchTarget.CurrentOrder, "Property and work",
                    P("ownership", "state"), P("economy", "planned"),
                    P("work", "duty"), P("support", "public")),
                Template("community_watch", "Community watch",
                    "Adds resident watch and militia defense to current security.",
                    CAPoliticalPatchTarget.CurrentOrder, "Security",
                    P("localOrder", "watch"), P("defense", "militia")),
                Template("professional_force", "Professional force",
                    "Adds guards and professional soldiers to current security.",
                    CAPoliticalPatchTarget.CurrentOrder, "Security",
                    P("localOrder", "constabulary"), P("defense", "professional"))
            };

        private static KeyValuePair<string, string[]> P(string axis,
            params string[] mechanisms)
        {
            return new KeyValuePair<string, string[]>(axis, mechanisms);
        }

        private static CAPoliticalPatchTemplate Template(string key,
            string label, string summary, CAPoliticalPatchTarget target,
            string domain, params KeyValuePair<string, string[]>[] patches)
        {
            return new CAPoliticalPatchTemplate
            {
                Key = "ca.template." + key,
                Label = label,
                Summary = summary,
                Target = target,
                Domain = domain,
                Mechanisms = patches.ToDictionary(value => value.Key,
                    value => value.Value.Distinct(StringComparer.Ordinal)
                        .ToList(), StringComparer.Ordinal)
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
                Control("politics.belief-mechanisms",
                    CAAuthoringSemanticKind.MultiValuedSet,
                    "CAPoliticalBeliefs", "faction or founding population",
                    CAAuthoringTemporalStatus.Normative,
                    "zero or more mechanisms per political subject",
                    "mechanisms coexist except an explicit absence mechanism excludes standing mechanisms on the same subject",
                    "player choice, scenario authoring, or evidenced generation",
                    new[] { "CAPoliticalBeliefs.positions[].axis",
                        "CAPoliticalBeliefs.positions[].option" },
                    "founding suggestions, legitimacy, conflict, and comparison with current order"),
                Control("politics.current-order-mechanisms",
                    CAAuthoringSemanticKind.MultiValuedSet,
                    "CAFactionState", "established faction",
                    CAAuthoringTemporalStatus.Current,
                    "zero or more instituted mechanisms per political subject",
                    "mechanisms coexist except an explicit absence mechanism excludes standing mechanisms on the same subject",
                    "scenario authoring or represented institutional evidence",
                    new[] { "CAFactionState current-order mechanisms: axis",
                        "CAFactionState current-order mechanisms: option" },
                    "organizations, offices, security, work, property, and belief-practice tension"),
                Control("politics.belief-set",
                    CAAuthoringSemanticKind.PartialPatchPreset,
                    "CAPoliticalPatchTemplate", "political beliefs",
                    CAAuthoringTemporalStatus.Normative,
                    "one partial patch per application",
                    "adds listed mechanisms and preserves unlisted and existing mechanisms",
                    "built-in or user-saved partial copy",
                    new[] { "CAPoliticalBeliefs.positions[] listed writes" },
                    "political composer"),
                Control("politics.current-order-set",
                    CAAuthoringSemanticKind.PartialPatchPreset,
                    "CAPoliticalPatchTemplate", "established faction current order",
                    CAAuthoringTemporalStatus.Current,
                    "one partial patch per application",
                    "adds listed instituted mechanisms and preserves unlisted and existing mechanisms",
                    "built-in partial copy",
                    new[] { "CAFactionState current-order listed writes" },
                    "established-society composer"),
                Control("faction.name",
                    CAAuthoringSemanticKind.OptionalUnset,
                    "CARegionalFactionPlan", "established faction",
                    CAAuthoringTemporalStatus.Current,
                    "zero or one authored display name",
                    "coexists with type, Culture, Ideoligion, beliefs, and current order",
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
                        "CARegionalRelationPlan.authorRelation" },
                    "native relations, regional pattern, conflict, and connections"),
                Control("settlement.owner",
                    CAAuthoringSemanticKind.Relational,
                    "CARegionalSettlementPlan", "settlement-to-faction relation",
                    CAAuthoringTemporalStatus.Current,
                    "one owning faction per major settlement",
                    "a faction may own several settlements",
                    "scenario authoring or authorized world-pool source",
                    new[] { "CARegionalSettlementPlan.factionKey" },
                    "population affiliation, current order, materialization, and regional hierarchy"),
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
                    "different faction, political-belief, Culture, and Ideoligion affiliations may coexist",
                    "scenario authoring or population-source realization",
                    new[] { "CASettlementPopulationGroup.kind",
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
                    new[] { "CARegionalWorldPolicy.urbanGrowthPropensity" },
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
                    "one zero-to-one activity cadence modifier",
                    "does not author political or institutional state",
                    "world-tendency authoring",
                    new[] { "CARegionalWorldPolicy.offMapActivityRate" },
                    "frequency of distant faction and settlement activity"),
                Control("founding.arrangement",
                    CAAuthoringSemanticKind.StructuredComposition,
                    "CAFoundingArrangement", "player founding moment",
                    CAAuthoringTemporalStatus.Current,
                    "one adopted initial arrangement with independently authored terms",
                    "coexists with inherited Culture, Ideoligion, and Political Beliefs and may disagree with them",
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
