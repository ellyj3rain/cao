using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    public sealed class CASocialSubjectDef
    {
        public string Owner;
        public string Key;
        public string Label;
        public string Description;
        public string SourceDomain;
        public string AuthoritativeSource;
        public string FactualCondition;
        public string Applicability;
        public string CulturalEffect;
        public List<string> Consumers = new List<string>();

        public bool IsAuthorable(out string failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(Owner))
                failure = "the subject needs an owning module";
            else if (!CASocialSubjectRegistry.ValidKey(Key))
                failure = "the subject needs a namespaced key";
            else if (!Key.StartsWith(Owner + ".", StringComparison.Ordinal))
                failure = "the subject key must begin with its owner";
            else if (string.IsNullOrWhiteSpace(Label)
                || string.IsNullOrWhiteSpace(Description))
                failure = "the subject needs a player-facing account";
            else if (string.IsNullOrWhiteSpace(SourceDomain)
                || string.IsNullOrWhiteSpace(AuthoritativeSource)
                || string.IsNullOrWhiteSpace(FactualCondition))
                failure = "the subject needs one factual source";
            else if (Consumers == null
                || !Consumers.Any(value => !string.IsNullOrWhiteSpace(value)))
                failure = "the subject needs one real consumer";
            return failure == null;
        }
    }

    // The registry is open to later CA modules and explicit integration
    // adapters.
    // Culture persistence stores a namespaced key rather than an enum ordinal,
    // so a valid unknown key survives even when its owner is not currently
    // loaded. Only registered source-and-consumer pairs enter an editor.
    public static class CASocialSubjectRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, CASocialSubjectDef> Values =
            new Dictionary<string, CASocialSubjectDef>(StringComparer.Ordinal);

        public const string PublicGathering = "ca.space.public_gathering";
        public const string OutsiderContact = "ca.exchange.outsider_contact";
        public const string DefendedBoundary = "ca.space.defended_boundary";
        public const string ResearchWork = "ca.knowledge.research_work";
        public const string PublicVoice = "ca.politics.public_voice";
        public const string CompelledService = "ca.work.compelled_service";
        public const string EnforcedOrder = "ca.politics.enforced_order";
        public const string CompulsoryTransfer =
            "ca.property.compulsory_transfer";
        public const string SharedProvision = "ca.support.shared_provision";
        public const string InheritedRank = "ca.status.inherited_rank";
        public const string HumaneCustody = "ca.custody.humane_treatment";
        public const string QuarterGiven = "ca.war.quarter_given";
        public const string MedicalCare = "ca.care.medical_care";
        public const string CustodyPunishment = "ca.custody.punishment";
        public const string VoluntaryTrade = "ca.exchange.voluntary_trade";
        public const string MealPreparation = "ca.food.meal_preparation";
        public const string KnowledgeTransmission =
            "ca.knowledge.knowledge_transmission";
        public const string LongRangeCommunication =
            "ca.knowledge.long_range_communication";
        public const string FactionMembership =
            "ca.membership.faction_membership";
        public const string HouseholdMembership =
            "ca.membership.household_membership";
        public const string ArtAndRemembrance =
            "ca.memory.art_and_remembrance";
        public const string DelegatedAuthority =
            "ca.politics.delegated_authority";
        public const string OfficeGovernance =
            "ca.politics.office_governance";
        public const string AnimalTending = "ca.production.animal_tending";
        public const string Cultivation = "ca.production.cultivation";
        public const string GeneralCraft = "ca.production.general_craft";
        public const string RepairAndRebuilding =
            "ca.production.repair_rebuilding";
        public const string SpecializedCraft =
            "ca.production.specialized_craft";
        public const string CommonOwnership =
            "ca.property.common_ownership";
        public const string Confiscation = "ca.property.confiscation";
        public const string PrivateOwnership =
            "ca.property.private_ownership";
        public const string Taxation = "ca.property.taxation";
        public const string SharedRecreation =
            "ca.recreation.shared_recreation";
        public const string VoluntaryAgreement =
            "ca.relations.voluntary_agreement";
        public const string ReligiousObservance =
            "ca.ritual.religious_observance";
        public const string SecurityService =
            "ca.security.security_service";
        public const string MaintainedHousing =
            "ca.shelter.maintained_housing";
        public const string PublicWorks = "ca.space.public_works";
        public const string KinSuccession = "ca.status.kin_succession";
        public const string OfficeHolding = "ca.status.office_holding";
        public const string StoredReserves = "ca.stores.stored_reserves";
        public const string AuthorityProvision =
            "ca.support.authority_provision";
        public const string HouseholdProvision =
            "ca.support.household_provision";
        public const string RouteUse = "ca.transport.route_use";
        public const string CombatViolence = "ca.war.combat_violence";

        static CASocialSubjectRegistry()
        {
            RegisterBuiltIn(PublicGathering, "Public gatherings",
                "People meet in shared places to deliberate, trade, or spend time.",
                "space and organization", "space programs and gatherings",
                "a gathering occurs at a known shared place",
                "participants and nearby residents",
                "Culture may rank gathering places and shape social response.",
                "spatial planning", "ordinary social interpretation",
                "political development");
            RegisterBuiltIn(OutsiderContact, "Contact with outsiders",
                "Residents exchange goods, information, or hospitality with outsiders.",
                "exchange and relations", "agreements, trade, and known contact",
                "an exchange or sustained outside relation occurs",
                "participants, hosts, guests, and informed residents",
                "Culture may rank exchange routes and interpret contact.",
                "settlement development", "ordinary social interpretation");
            RegisterBuiltIn(DefendedBoundary, "Defended boundaries",
                "A settlement marks and actively defends an inside and outside.",
                "security and space", "security practices and built defenses",
                "a boundary is patrolled, fortified, or defended",
                "residents, defenders, entrants, and neighbors",
                "Culture may rank defensive spatial choices; it grants no authority.",
                "spatial planning", "NPC settlement development");
            RegisterBuiltIn(ResearchWork, "Research work",
                "Residents sustain organized inquiry and preserve its results.",
                "knowledge and institutions", "research work and milestones",
                "research is performed or institutionally supported",
                "researchers, beneficiaries, and informed residents",
                "Culture may rank otherwise available research work.",
                "institutional development", "settlement development");
            RegisterBuiltIn(PublicVoice, "Public voice",
                "Residents speak and participate in decisions that bind them.",
                "politics and organization", "decisions, policies, and offices",
                "a binding decision includes or excludes public participation",
                "members, residents, officeholders, and affected groups",
                "Culture may affect legitimacy and the persistence of conflict.",
                "political development", "institutional legitimacy");
            RegisterBuiltIn(CompelledService, "Compelled service",
                "A person is required to work or serve without a freely chosen bargain.",
                "work and acts", "CAActRecord and work obligations",
                "work is compelled, refused, or enforced",
                "worker, authority, household, and informed residents",
                "Culture may shape approval, stigma, and surprise.",
                "ordinary social interpretation", "political conflict");
            RegisterBuiltIn(EnforcedOrder, "Enforced orders",
                "An order is imposed through institutional or personal force.",
                "politics and acts", "CAActRecord coercion facts",
                "an order is enforced against resistance",
                "ordered person, actor, witnesses, and informed residents",
                "Culture may shape approval, prestige, and surprise; it grants no command authority.",
                "ordinary social interpretation", "political conflict");
            RegisterBuiltIn(CompulsoryTransfer, "Compulsory transfers",
                "Property or payment is taken under a claimed obligation or by force.",
                "property and acts",
                "CAActRecord taxation, requisition, and confiscation facts",
                "a transfer occurs without a freely chosen exchange",
                "owner, claimant, witnesses, and informed residents",
                "Culture may shape response; it neither creates ownership nor authorizes a taking.",
                "ordinary social interpretation", "political conflict");
            RegisterBuiltIn(SharedProvision, "Shared provision",
                "Food, shelter, medicine, or supplies are pooled for those in need.",
                "support and provision", "provision records and material distribution",
                "shared stores or public support provide for a population",
                "providers, recipients, residents, and member groups",
                "Culture may shape legitimacy and later provision preferences.",
                "institutional legitimacy", "settlement development");
            RegisterBuiltIn(InheritedRank, "Inherited rank",
                "Standing or office passes through family descent.",
                "status and office", "status assignments and offices",
                "rank is assigned or defended on hereditary grounds",
                "officeholders, families, members, and excluded residents",
                "Culture may confer prestige or stigma without creating an office.",
                "political development", "ordinary social interpretation");
            RegisterBuiltIn(HumaneCustody, "Humane custody",
                "Captives are protected from unnecessary harm while held.",
                "custody and aftermath", "custody outcomes and known acts",
                "a captive is protected, neglected, coerced, or harmed",
                "captives, custodians, witnesses, and informed groups",
                "Culture may shape response; custody authority remains separate.",
                "ordinary social interpretation", "political conflict");
            RegisterBuiltIn(QuarterGiven, "Quarter for defeated enemies",
                "Surrendered or defeated enemies are spared.",
                "war and aftermath", "combat and custody outcomes",
                "a defeated combatant is spared or deliberately killed",
                "combatants, witnesses, related groups, and informed residents",
                "Culture may affect approval and prestige without controlling combat.",
                "ordinary social interpretation", "political conflict");

            // Production vocabulary follows concrete facts already owned by
            // programs, organizations, relations, residence, and the act
            // ledger. These are possible objects of cultural interpretation;
            // registering one never asserts that the fact exists.
            RegisterMechanic(MedicalCare, "Medical care",
                "People provide represented treatment to sick or injured patients.",
                "CASettlementOperationalFact medicine and live care work",
                "a supported medical program and a caregiver treat a patient",
                "Culture may shape the standing of care without supplying medicine or skill.",
                "settlement programs", "ordinary social interpretation");
            RegisterMechanic(CustodyPunishment, "Punishment in custody",
                "Custodians impose represented punishment on a captive.",
                "CAActRecord coercion and custody outcomes",
                "a known captive is punished by a represented custodian",
                "Culture may shape approval and stigma; it grants no custody authority.",
                "ordinary social interpretation", "political conflict");
            RegisterMechanic(VoluntaryTrade, "Voluntary trade",
                "People exchange represented goods through a voluntary bargain.",
                "trade programs, agreements, and exchange acts",
                "named parties exchange goods under an active bargain",
                "Culture may rank voluntary exchange without creating goods or a route.",
                "settlement development", "ordinary social interpretation");
            RegisterMechanic(MealPreparation, "Meal preparation",
                "Cooks prepare represented food for a known population.",
                "CASettlementOperationalFact food-preparation and live cooking",
                "a supported kitchen operator prepares food for residents",
                "Culture may shape participation and prestige without creating food.",
                "settlement programs", "ordinary social interpretation");
            RegisterMechanic(KnowledgeTransmission, "Knowledge transmission",
                "People teach or preserve represented knowledge for others.",
                "research milestones, instruction, and knowledge records",
                "knowledge passes between represented actors or persists in an institution",
                "Culture may value transmission without inventing knowledge.",
                "knowledge development", "institutional development");
            RegisterMechanic(LongRangeCommunication,
                "Long-range communication",
                "Operators exchange represented information over distance.",
                "communications programs and known counterparties",
                "a supported communications operator reaches a named counterparty",
                "Culture may interpret outside communication without creating contact.",
                "relations", "settlement development");
            RegisterMechanic(FactionMembership, "Faction membership",
                "A person belongs to a represented faction.",
                "native faction membership and CA population affiliation",
                "a represented person or population group has a faction identity",
                "Culture may shape belonging without assigning membership.",
                "population composition", "political development");
            RegisterMechanic(HouseholdMembership, "Household membership",
                "People belong to a represented domestic unit.",
                "CADomesticUnit membership and residence assignments",
                "partner, kin, residence, or explicit co-residence evidence links members",
                "Culture may interpret household life without fabricating kinship or residence.",
                "domestic provision", "ordinary social interpretation");
            RegisterMechanic(ArtAndRemembrance, "Art and remembrance",
                "People create, preserve, or gather around represented works of memory.",
                "art-memory programs, assets, and recorded events",
                "a supported maker or keeper maintains a work or memorial",
                "Culture may shape prestige and salience without creating the work.",
                "settlement development", "cultural expression");
            RegisterMechanic(DelegatedAuthority, "Delegated authority",
                "An officeholder grants limited represented authority to another actor.",
                "CAOrganization offices, groups, and decision records",
                "a current office or decision records a delegation",
                "Culture may shape legitimacy without creating jurisdiction.",
                "political development", "institutional legitimacy");
            RegisterMechanic(OfficeGovernance, "Government by office",
                "Current officeholders administer represented decisions and policies.",
                "CAOrganization offices, policies, and decision history",
                "a current officeholder acts within represented jurisdiction",
                "Culture may shape legitimacy without creating an office.",
                "political development", "institutional legitimacy");
            RegisterMechanic(AnimalTending, "Animal tending",
                "Handlers repeatedly care for represented domesticated animals.",
                "animal programs, assigned workers, and live animal care",
                "a supported operator and handler care for represented animals",
                "Culture may rank the work without creating animals or skill.",
                "settlement programs", "ordinary social interpretation");
            RegisterMechanic(Cultivation, "Cultivation",
                "Workers plant, tend, and harvest represented land.",
                "agriculture programs, growing zones, and live work",
                "a supported agricultural operator works accessible cultivable ground",
                "Culture may rank cultivation without creating land, seed, or labor.",
                "settlement programs", "ordinary social interpretation");
            RegisterMechanic(GeneralCraft, "General craft",
                "Workers make represented ordinary tools and goods.",
                "production programs, workstations, materials, and completed work",
                "a supported production operator completes general craft work",
                "Culture may rank craft without creating material or skill.",
                "settlement programs", "ordinary social interpretation");
            RegisterMechanic(RepairAndRebuilding, "Repair and rebuilding",
                "Workers restore represented damaged assets and places.",
                "repair records, program assets, and rebuilding completion",
                "assigned workers restore a damaged represented asset",
                "Culture may rank rebuilding without creating labor or material.",
                "settlement programs", "cultural history");
            RegisterMechanic(SpecializedCraft, "Specialized craft",
                "Skilled workers make represented specialized goods.",
                "specialized-industry programs, assets, knowledge, and completed work",
                "a supported specialist completes represented production",
                "Culture may rank specialization without creating knowledge or material.",
                "settlement programs", "ordinary social interpretation");
            RegisterMechanic(CommonOwnership, "Common ownership",
                "Represented productive property is held in common.",
                "property claims, organizational rules, and operational funding",
                "a named group currently holds a represented asset in common",
                "Culture may legitimize the arrangement without creating title.",
                "property acts", "institutional legitimacy");
            RegisterMechanic(Confiscation, "Confiscation",
                "Property is taken from a represented holder by force or authority.",
                "CAActRecord confiscation and property claims",
                "a named actor takes a represented asset without voluntary exchange",
                "Culture may shape response without authorizing the taking.",
                "ordinary social interpretation", "political conflict");
            RegisterMechanic(PrivateOwnership, "Private ownership",
                "A represented person or household holds productive property.",
                "property claims, program assets, and organizational rules",
                "a named private holder currently controls a represented asset",
                "Culture may confer legitimacy without creating title.",
                "property acts", "institutional legitimacy");
            RegisterMechanic(Taxation, "Tax collection",
                "A represented authority collects an assessed contribution.",
                "CAActRecord taxation and CAOrganization treasury",
                "a named authority collects from a named liable party",
                "Culture may shape legitimacy without creating authority or debt.",
                "ordinary social interpretation", "political conflict");
            RegisterMechanic(SharedRecreation, "Shared recreation",
                "Residents take recreation together in represented settings.",
                "recreation programs, assets, and live participation",
                "a supported activity occurs with represented participants",
                "Culture may shape access and prestige without creating facilities.",
                "settlement development", "ordinary social interpretation");
            RegisterMechanic(VoluntaryAgreement, "Voluntary agreements",
                "Represented parties accept and maintain reciprocal terms.",
                "CAAgreementRecord parties, terms, contributions, and state",
                "an active non-broken agreement binds named parties",
                "Culture may shape legitimacy without inventing consent or terms.",
                "relations", "political development");
            RegisterMechanic(ReligiousObservance, "Religious observance",
                "People perform represented observance under a native Ideoligion.",
                "native Ideoligion precepts, rituals, roles, and religion programs",
                "a supported observance occurs among represented believers",
                "Culture may shape expression while Ideoligion owns doctrine.",
                "native Ideoligion", "cultural expression");
            RegisterMechanic(SecurityService, "Security service",
                "Assigned residents perform represented watch or guard duty.",
                "security practices, defense programs, and guard assignments",
                "an eligible armed resident performs a current assignment",
                "Culture may rank service without assigning guards or command.",
                "security runtime", "ordinary social interpretation");
            RegisterMechanic(MaintainedHousing, "Maintained housing",
                "Residents maintain represented inhabited shelter.",
                "housing programs, residence assignments, and repair work",
                "a supported operator maintains occupied accessible housing",
                "Culture may value upkeep without creating shelter or labor.",
                "settlement programs", "domestic life");
            RegisterMechanic(PublicWorks, "Public works",
                "A represented operator builds or maintains shared infrastructure.",
                "roads, facilities, program assets, and public-work records",
                "assigned workers maintain a represented shared asset",
                "Culture may rank the work without creating authority, labor, or material.",
                "settlement development", "cultural history");
            RegisterMechanic(KinSuccession, "Kin succession",
                "A represented office or rank passes through family descent.",
                "office succession, kin relations, and status assignments",
                "a current succession record names kinship as its basis",
                "Culture may confer prestige without creating kinship or office.",
                "political development", "ordinary social interpretation");
            RegisterMechanic(OfficeHolding, "Office holding",
                "A represented person occupies a current office.",
                "CAOrganization office records and native role assignment",
                "a named actor currently holds a represented office",
                "Culture may shape prestige without creating office or authority.",
                "political development", "institutional legitimacy");
            RegisterMechanic(StoredReserves, "Stored reserves",
                "People keep represented goods for later need.",
                "storage programs, stock records, and accessible assets",
                "a supported storage operator maintains reachable stock",
                "Culture may shape legitimacy and priority without creating goods.",
                "settlement programs", "provision runtime");
            RegisterMechanic(AuthorityProvision, "Provision by authority",
                "A represented governing operator supplies necessities.",
                "authority provision arrangements, stock, nodes, and access",
                "a current authority operator distributes reachable stock to an eligible population",
                "Culture may shape legitimacy without creating stock or authority.",
                "provision runtime", "institutional legitimacy");
            RegisterMechanic(HouseholdProvision, "Household provision",
                "A represented domestic unit supplies necessities to its members.",
                "domestic provision arrangements, membership, stock, and access",
                "a current domestic unit distributes reachable stock to its factual members",
                "Culture may shape meaning without creating a household or supplies.",
                "provision runtime", "domestic life");
            RegisterMechanic(RouteUse, "Route use",
                "People move represented goods or travelers along known routes.",
                "transport programs, roads, access, and completed journeys",
                "a supported operator uses an accessible represented route",
                "Culture may rank travel without creating a route or transport.",
                "settlement development", "relations");
            RegisterMechanic(CombatViolence, "Combat violence",
                "A represented actor uses violence against another in combat.",
                "CAActRecord violence and native combat outcomes",
                "a named attacker harms a named target in represented combat",
                "Culture may shape interpretation without selecting targets or granting force.",
                "ordinary social interpretation", "political conflict");
        }

        public static bool ValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            string[] parts = key.Split('.');
            if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
                return false;
            return parts.All(part => part.All(value => char.IsLetterOrDigit(value)
                || value == '_' || value == '-'));
        }

        public static bool Register(CASocialSubjectDef value,
            out string failure)
        {
            if (value == null)
            {
                failure = "the subject is missing";
                return false;
            }
            if (!value.IsAuthorable(out failure)) return false;
            lock (Gate)
            {
                if (Values.ContainsKey(value.Key))
                {
                    failure = "the subject key is already registered";
                    return false;
                }
                Values.Add(value.Key, value);
            }
            return true;
        }

        public static CASocialSubjectDef Find(string key)
        {
            lock (Gate)
            {
                CASocialSubjectDef value;
                return key != null && Values.TryGetValue(key, out value)
                    ? value : null;
            }
        }

        public static IReadOnlyList<CASocialSubjectDef> Authorable()
        {
            lock (Gate)
                return Values.Values.OrderBy(value => value.Label,
                    StringComparer.Ordinal).ToList();
        }

        private static void RegisterBuiltIn(string key, string label,
            string description, string sourceDomain,
            string authoritativeSource, string factualCondition,
            string applicability, string culturalEffect,
            params string[] consumers)
        {
            string ignored;
            Register(new CASocialSubjectDef
            {
                Owner = "ca",
                Key = key,
                Label = label,
                Description = description,
                SourceDomain = sourceDomain,
                AuthoritativeSource = authoritativeSource,
                FactualCondition = factualCondition,
                Applicability = applicability,
                CulturalEffect = culturalEffect,
                Consumers = consumers.ToList()
            }, out ignored);
        }

        private static void RegisterMechanic(string key, string label,
            string description, string authoritativeSource,
            string factualCondition, string culturalEffect,
            params string[] consumers)
        {
            string domain = key.Split('.')[1].Replace('_', ' ');
            RegisterBuiltIn(key, label, description, domain,
                authoritativeSource, factualCondition,
                "represented actors and informed populations",
                culturalEffect, consumers);
        }
    }

    public sealed class CACulturalMeaningState
    {
        public string SubjectKey;
        public string PopulationScope;
        public int Approval;
        public int Normality;
        public int Prestige;
        public int Salience;
        public string Provenance;
        public string SourceIdentity;
        public string EvidenceSignature;
        public int FirstRecordedTick = -1;
        public int LastChangedTick = -1;
        public int Weight = 100;

        public CACulturalMeaningState Copy()
        {
            return (CACulturalMeaningState)MemberwiseClone();
        }
    }

    public sealed class CACulturalMeaningContribution
    {
        public string PopulationScope;
        public string Provenance;
        public string SourceIdentity;
        public int Weight;
        public int Approval;
        public int Normality;
        public int Prestige;
        public int Salience;
    }

    public sealed class CACulturalMeaningResolution
    {
        public string SubjectKey;
        public int Approval;
        public int Normality;
        public int Prestige;
        public int Salience;
        public float Dissonance;
        public float Confidence;
        public List<CACulturalMeaningContribution> Contributions =
            new List<CACulturalMeaningContribution>();
        public List<string> Provenance = new List<string>();
    }

    public static class CACulturalMeaningResolver
    {
        public static IEnumerable<CACulturalMeaningState>
            ApplyConstituentShares(
                IEnumerable<CACulturalMeaningState> meanings,
                IReadOnlyDictionary<string, int> shares)
        {
            foreach (CACulturalMeaningState source in meanings
                ?? Enumerable.Empty<CACulturalMeaningState>())
            {
                if (source == null) continue;
                CACulturalMeaningState value = source.Copy();
                int share;
                if (!string.IsNullOrWhiteSpace(value.PopulationScope)
                    && value.PopulationScope != "*" && shares != null
                    && shares.TryGetValue(value.PopulationScope, out share))
                    value.Weight = Math.Max(1, (int)Math.Round(
                        Math.Max(1, value.Weight)
                            * Math.Max(0, Math.Min(100, share)) / 100d,
                        MidpointRounding.AwayFromZero));
                yield return value;
            }
        }

        public static CACulturalMeaningResolution Resolve(
            IEnumerable<CACulturalMeaningState> meanings,
            string subjectKey, string populationScope = null)
        {
            var result = new CACulturalMeaningResolution
                { SubjectKey = subjectKey };
            List<CACulturalMeaningState> applicable = (meanings
                    ?? Enumerable.Empty<CACulturalMeaningState>())
                .Where(value => value != null
                    && string.Equals(value.SubjectKey, subjectKey,
                        StringComparison.Ordinal)
                    && ScopeApplies(value.PopulationScope, populationScope))
                .OrderBy(value => value.PopulationScope,
                    StringComparer.Ordinal)
                .ThenBy(value => value.Provenance,
                    StringComparer.Ordinal).ToList();
            if (applicable.Count == 0) return result;
            int total = applicable.Sum(value => Math.Max(1, value.Weight));
            result.Approval = Weighted(applicable, total,
                value => Clamp(value.Approval, -100, 100));
            result.Normality = Weighted(applicable, total,
                value => Clamp(value.Normality, 0, 100));
            result.Prestige = Weighted(applicable, total,
                value => Clamp(value.Prestige, -100, 100));
            result.Salience = Weighted(applicable, total,
                value => Clamp(value.Salience, 0, 100));
            double variance = applicable.Sum(value =>
            {
                double delta = Clamp(value.Approval, -100, 100)
                    - result.Approval;
                return Math.Max(1, value.Weight) * delta * delta;
            }) / Math.Max(1, total);
            result.Dissonance = (float)Math.Min(1d,
                Math.Sqrt(variance) / 100d);
            result.Confidence = Math.Min(1f, total / 100f)
                * (0.35f + result.Salience / 100f * 0.65f);
            foreach (CACulturalMeaningState value in applicable)
            {
                result.Contributions.Add(new CACulturalMeaningContribution
                {
                    PopulationScope = value.PopulationScope,
                    Provenance = value.Provenance,
                    SourceIdentity = value.SourceIdentity,
                    Weight = Math.Max(1, value.Weight),
                    Approval = value.Approval,
                    Normality = value.Normality,
                    Prestige = value.Prestige,
                    Salience = value.Salience
                });
                string source = (value.Provenance ?? "recorded") + ":"
                    + (value.SourceIdentity ?? "unrecorded");
                if (!result.Provenance.Contains(source))
                    result.Provenance.Add(source);
            }
            return result;
        }

        public static string Fingerprint(
            IEnumerable<CACulturalMeaningState> meanings)
        {
            return string.Join("|", (meanings
                    ?? Enumerable.Empty<CACulturalMeaningState>())
                .Where(value => value != null
                    && !string.IsNullOrWhiteSpace(value.SubjectKey))
                .OrderBy(value => value.SubjectKey, StringComparer.Ordinal)
                .ThenBy(value => value.PopulationScope,
                    StringComparer.Ordinal)
                .Select(value => value.SubjectKey + ":"
                    + (value.PopulationScope ?? "all") + ":"
                    + Clamp(value.Approval, -100, 100) + ":"
                    + Clamp(value.Normality, 0, 100) + ":"
                    + Clamp(value.Prestige, -100, 100) + ":"
                    + Clamp(value.Salience, 0, 100) + ":"
                    + (value.Provenance ?? "recorded") + ":"
                    + (value.SourceIdentity ?? "unrecorded")));
        }

        private static bool ScopeApplies(string recorded, string requested)
        {
            return string.IsNullOrWhiteSpace(recorded) || recorded == "*"
                || string.IsNullOrWhiteSpace(requested)
                || string.Equals(recorded, requested,
                    StringComparison.Ordinal);
        }

        private static int Weighted(List<CACulturalMeaningState> values,
            int total, Func<CACulturalMeaningState, int> selector)
        {
            return (int)Math.Round(values.Sum(value =>
                    selector(value) * (double)Math.Max(1, value.Weight))
                / Math.Max(1, total), MidpointRounding.AwayFromZero);
        }

        private static int Clamp(int value, int low, int high)
        {
            return Math.Max(low, Math.Min(high, value));
        }
    }

    public sealed class CASocialContribution
    {
        public string Source;
        public int Approval;
        public int Prestige;
        public int Normality;
        public int Salience;
        public string Reason;
    }

    public sealed class CASocialFactContext
    {
        public string SubjectKey;
        public string FactIdentity;
        public string ActorIdentity;
        public string TargetIdentity;
        public string OrganizationIdentity;
        public string KnowledgeSource;
        public bool Known;
        // +1 means the registered subject occurred; -1 means the recorded
        // fact is its concrete absence or violation. Culture interprets the
        // fact that happened, not an unqualified subject label.
        public int Realization = 1;
        public int Tick = -1;
    }

    public sealed class CAPawnSocialInterpretationInput
    {
        public CASocialFactContext Fact;
        public string PawnIdentity;
        public string PopulationIdentity;
        public string OrganizationIdentity;
        public int InfluenceWeight = 100;
        public CACulturalMeaningResolution Culture;
        public List<CASocialContribution> OtherContributions =
            new List<CASocialContribution>();
    }

    public sealed class CAPawnSocialInterpretation
    {
        public string SubjectKey;
        public string FactIdentity;
        public string PawnIdentity;
        public string PopulationIdentity;
        public string OrganizationIdentity;
        public string KnowledgeSource;
        public int Tick = -1;
        public int InfluenceWeight = 100;
        public int Approval;
        public int Prestige;
        public int Surprise;
        public int Salience;
        public bool InternalContradiction;
        public List<CASocialContribution> Contributions =
            new List<CASocialContribution>();
    }

    // Portable persisted form of one informed pawn's interpretation. Source
    // adapters produce facts; this generic kernel validates the registered
    // subject, interprets the fact, and creates the state that the runtime
    // world component serializes. It owns no source-specific switch.
    public sealed class CAPersistedSocialReaction
    {
        public string SubjectKey;
        public string FactIdentity;
        public string PawnIdentity;
        public string PopulationIdentity;
        public string OrganizationIdentity;
        public int Approval;
        public int Prestige;
        public int Surprise;
        public int Salience;
        public string KnowledgeSource;
        public int Tick = -1;
        public int InfluenceWeight = 100;
        public bool InternalContradiction;
        public string Contributions;

        public CAPawnSocialInterpretation ToInterpretation()
        {
            return new CAPawnSocialInterpretation
            {
                SubjectKey = SubjectKey,
                FactIdentity = FactIdentity,
                PawnIdentity = PawnIdentity,
                PopulationIdentity = PopulationIdentity,
                OrganizationIdentity = OrganizationIdentity,
                Approval = Approval,
                Prestige = Prestige,
                Surprise = Surprise,
                Salience = Salience,
                KnowledgeSource = KnowledgeSource,
                Tick = Tick,
                InfluenceWeight = InfluenceWeight,
                InternalContradiction = InternalContradiction
            };
        }
    }

    public static class CASocialReactionPersistenceKernel
    {
        public static CAPersistedSocialReaction Record(
            CASocialFactContext fact, string pawnIdentity,
            string populationIdentity, string organizationIdentity,
            int influenceWeight, CACulturalMeaningResolution culture,
            IEnumerable<CASocialContribution> otherContributions,
            IEnumerable<string> existingFactPawnKeys = null)
        {
            if (fact == null || !fact.Known
                || string.IsNullOrWhiteSpace(fact.FactIdentity)
                || string.IsNullOrWhiteSpace(fact.KnowledgeSource)
                || string.IsNullOrWhiteSpace(pawnIdentity)
                || CASocialSubjectRegistry.Find(fact.SubjectKey) == null)
                return null;
            string key = fact.FactIdentity + "|" + pawnIdentity;
            if ((existingFactPawnKeys ?? Enumerable.Empty<string>())
                .Any(value => string.Equals(value, key,
                    StringComparison.Ordinal))) return null;
            CAPawnSocialInterpretation response = CASocialInterpretationKernel
                .Interpret(new CAPawnSocialInterpretationInput
                {
                    Fact = fact,
                    PawnIdentity = pawnIdentity,
                    PopulationIdentity = populationIdentity,
                    OrganizationIdentity = organizationIdentity,
                    InfluenceWeight = influenceWeight,
                    Culture = culture,
                    OtherContributions = (otherContributions
                        ?? Enumerable.Empty<CASocialContribution>()).ToList()
                });
            if (response == null) return null;
            return new CAPersistedSocialReaction
            {
                SubjectKey = response.SubjectKey,
                FactIdentity = response.FactIdentity,
                PawnIdentity = response.PawnIdentity,
                PopulationIdentity = response.PopulationIdentity,
                OrganizationIdentity = response.OrganizationIdentity,
                Approval = response.Approval,
                Prestige = response.Prestige,
                Surprise = response.Surprise,
                Salience = response.Salience,
                KnowledgeSource = response.KnowledgeSource,
                Tick = response.Tick,
                InfluenceWeight = response.InfluenceWeight,
                InternalContradiction = response.InternalContradiction,
                Contributions = string.Join(" | ", response.Contributions
                    .Select(value => value.Source + ":" + value.Reason))
            };
        }
    }

    public static class CASocialInterpretationKernel
    {
        public static CAPawnSocialInterpretation Interpret(
            CAPawnSocialInterpretationInput input)
        {
            if (input?.Fact == null || !input.Fact.Known) return null;
            var contributions = new List<CASocialContribution>();
            if (input.Culture != null
                && input.Culture.Contributions.Count > 0)
            {
                int direction = input.Fact.Realization < 0 ? -1 : 1;
                contributions.Add(new CASocialContribution
                {
                    Source = "Culture",
                    Approval = input.Culture.Approval * direction,
                    Prestige = input.Culture.Prestige * direction,
                    Normality = direction > 0 ? input.Culture.Normality
                        : 100 - input.Culture.Normality,
                    Salience = input.Culture.Salience,
                    Reason = (direction > 0 ? "subject realized; "
                        : "subject contradicted; ")
                        + string.Join(", ", input.Culture.Provenance)
                });
            }
            contributions.AddRange((input.OtherContributions
                    ?? new List<CASocialContribution>())
                .Where(value => value != null));
            if (contributions.Count == 0) return null;
            int approval = Average(contributions, value => value.Approval,
                -100, 100);
            int prestige = Average(contributions, value => value.Prestige,
                -100, 100);
            int normality = Average(contributions, value => value.Normality,
                0, 100);
            int salience = Average(contributions, value => value.Salience,
                0, 100);
            bool positive = contributions.Any(value => value.Approval > 15);
            bool negative = contributions.Any(value => value.Approval < -15);
            return new CAPawnSocialInterpretation
            {
                SubjectKey = input.Fact.SubjectKey,
                FactIdentity = input.Fact.FactIdentity,
                PawnIdentity = input.PawnIdentity,
                PopulationIdentity = input.PopulationIdentity,
                OrganizationIdentity = input.OrganizationIdentity,
                KnowledgeSource = input.Fact.KnowledgeSource,
                Tick = input.Fact.Tick,
                InfluenceWeight = Math.Max(1, input.InfluenceWeight),
                Approval = approval,
                Prestige = prestige,
                Surprise = 100 - normality,
                Salience = salience,
                InternalContradiction = positive && negative,
                Contributions = contributions
            };
        }

        private static int Average(List<CASocialContribution> values,
            Func<CASocialContribution, int> selector, int low, int high)
        {
            return Math.Max(low, Math.Min(high, (int)Math.Round(
                values.Average(value => selector(value)),
                MidpointRounding.AwayFromZero)));
        }
    }

    public sealed class CASocialGroupPattern
    {
        public string SubjectKey;
        public string PopulationIdentity;
        public int WeightedApproval;
        public float Dispersion;
        public float Polarization;
        public float Participation;
        public bool InfluentialMinority;
        public float GroupAlignment;
        public float CrossGroupDissonance;
        public int EvidenceCount;
        public int ObservedPawnCount;
        public int EligiblePopulation;
        public int EvidenceStartTick = -1;
        public int LastEvidenceTick = -1;
        public string EvidenceSignature;
    }

    public static class CASocialPatternKernel
    {
        public static List<CASocialGroupPattern> Aggregate(
            IEnumerable<CAPawnSocialInterpretation> responses,
            int eligiblePopulation)
        {
            var values = (responses
                    ?? Enumerable.Empty<CAPawnSocialInterpretation>())
                .Where(value => value != null
                    && !string.IsNullOrWhiteSpace(value.SubjectKey))
                .ToList();
            var result = new List<CASocialGroupPattern>();
            foreach (var group in values.GroupBy(value => new
                { value.SubjectKey, Scope = value.PopulationIdentity ?? "all" }))
            {
                List<CAPawnSocialInterpretation> items = group.ToList();
                int totalWeight = items.Sum(value =>
                    Math.Max(1, value.InfluenceWeight));
                float mean = (float)(items.Sum(value => value.Approval
                        * (double)Math.Max(1, value.InfluenceWeight))
                    / Math.Max(1, totalWeight));
                float variance = (float)(items.Sum(value =>
                {
                    double delta = value.Approval - mean;
                    return Math.Max(1, value.InfluenceWeight) * delta * delta;
                }) / Math.Max(1, totalWeight));
                int positive = items.Count(value => value.Approval >= 35);
                int negative = items.Count(value => value.Approval <= -35);
                int observedPawns = items.Select(value => value.PawnIdentity)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal).Count();
                int distinctFacts = items.Select(value => value.FactIdentity)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal).Count();
                bool influentialMinority = items.Any(value =>
                    Math.Sign(value.Approval) != Math.Sign(mean)
                    && value.InfluenceWeight * 4 >= totalWeight);
                result.Add(new CASocialGroupPattern
                {
                    SubjectKey = group.Key.SubjectKey,
                    PopulationIdentity = group.Key.Scope,
                    WeightedApproval = (int)Math.Round(mean,
                        MidpointRounding.AwayFromZero),
                    Dispersion = Math.Min(1f,
                        (float)Math.Sqrt(variance) / 100f),
                    Polarization = items.Count == 0 ? 0f
                        : Math.Min(positive, negative)
                            / (float)Math.Max(1, items.Count),
                    Participation = Math.Min(1f, observedPawns
                        / (float)Math.Max(1, eligiblePopulation)),
                    InfluentialMinority = influentialMinority,
                    GroupAlignment = Math.Min(1f, Math.Abs(mean) / 100f),
                    // Witnesses describe a fact's distribution of response;
                    // they do not turn that one fact into several historical
                    // events. Cultural qualification counts distinct facts.
                    EvidenceCount = distinctFacts,
                    ObservedPawnCount = observedPawns,
                    EligiblePopulation = Math.Max(0, eligiblePopulation),
                    EvidenceStartTick = items.Min(value => value.Tick),
                    LastEvidenceTick = items.Max(value => value.Tick),
                    EvidenceSignature = StableSignature(items)
                });
            }
            foreach (IGrouping<string, CASocialGroupPattern> subject in result
                .GroupBy(value => value.SubjectKey))
            {
                float[] means = subject.Select(value =>
                    (float)value.WeightedApproval).ToArray();
                float cross = means.Length <= 1 ? 0f
                    : (means.Max() - means.Min()) / 200f;
                foreach (CASocialGroupPattern pattern in subject)
                    pattern.CrossGroupDissonance = cross;
            }
            return result;
        }

        private static string StableSignature(
            IEnumerable<CAPawnSocialInterpretation> values)
        {
            string text = string.Join("|", values.OrderBy(value =>
                    value.FactIdentity, StringComparer.Ordinal)
                .ThenBy(value => value.PawnIdentity, StringComparer.Ordinal)
                .Select(value => (value.FactIdentity ?? "fact") + ":"
                    + (value.PawnIdentity ?? "pawn") + ":" + value.Tick
                    + ":" + value.Approval + ":" + value.Prestige + ":"
                    + value.Surprise + ":" + value.Salience + ":"
                    + value.InfluenceWeight));
            return StableHash(text);
        }

        public static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in value ?? "")
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return hash.ToString("X8");
            }
        }
    }

    public sealed class CACulturalMeaningTransitionResult
    {
        public bool Changed;
        public string Cause;
        public string EvidenceSignature;
        public List<string> ChangedSubjectKeys = new List<string>();
        public List<string> ChangedDimensions = new List<string>();
        public List<CACulturalMeaningState> Meanings =
            new List<CACulturalMeaningState>();
    }

    public static class CACulturalMeaningTransitionKernel
    {
        public const int HistoricalPeriod = 60000;

        public static CACulturalMeaningTransitionResult Evaluate(
            IEnumerable<CACulturalMeaningState> prior,
            IEnumerable<CASocialGroupPattern> patterns, int tick)
        {
            List<CACulturalMeaningState> result = (prior
                    ?? Enumerable.Empty<CACulturalMeaningState>())
                .Where(value => value != null)
                .Select(value => value.Copy()).ToList();
            var changedSubjects = new HashSet<string>(StringComparer.Ordinal);
            var changedDimensions = new HashSet<string>(StringComparer.Ordinal);
            var evidence = new List<string>();
            foreach (CASocialGroupPattern pattern in (patterns
                ?? Enumerable.Empty<CASocialGroupPattern>()).Where(value =>
                    value != null
                    && CASocialSubjectRegistry.ValidKey(value.SubjectKey)))
            {
                evidence.Add(pattern.SubjectKey + ":"
                    + pattern.EvidenceSignature);
                bool qualified = pattern.EvidenceCount >= 2
                    && pattern.EvidenceStartTick >= 0
                    && pattern.LastEvidenceTick - pattern.EvidenceStartTick
                        >= HistoricalPeriod;
                if (!qualified) continue;
                CACulturalMeaningState current = result.FirstOrDefault(value =>
                    value.SubjectKey == pattern.SubjectKey
                    && (value.PopulationScope ?? "all")
                        == (pattern.PopulationIdentity ?? "all"));
                if (current == null)
                {
                    current = new CACulturalMeaningState
                    {
                        SubjectKey = pattern.SubjectKey,
                        PopulationScope = pattern.PopulationIdentity,
                        Provenance = "current",
                        SourceIdentity = "social response",
                        FirstRecordedTick = tick,
                        LastChangedTick = tick,
                        Weight = Math.Max(1, Math.Min(100,
                            pattern.ObservedPawnCount)),
                        Normality = (int)Math.Round(pattern.Participation * 100f),
                        Salience = (int)Math.Round(Math.Min(1f,
                            pattern.Dispersion + pattern.Participation) * 100f),
                        Approval = pattern.WeightedApproval,
                        Prestige = pattern.WeightedApproval / 2,
                        EvidenceSignature = pattern.EvidenceSignature
                    };
                    result.Add(current);
                    changedSubjects.Add(pattern.SubjectKey);
                    changedDimensions.UnionWith(new[]
                        { "approval", "normality", "prestige", "salience" });
                    continue;
                }
                // The exact same response distribution is one evidentiary
                // run, not a fresh historical cause on every evaluation.
                if (string.Equals(current.EvidenceSignature,
                        pattern.EvidenceSignature,
                        StringComparison.Ordinal)) continue;
                int approval = Blend(current.Approval,
                    pattern.WeightedApproval);
                int normality = Blend(current.Normality,
                    (int)Math.Round(pattern.Participation * 100f));
                int prestige = Blend(current.Prestige,
                    pattern.WeightedApproval / 2);
                int salience = Blend(current.Salience,
                    (int)Math.Round(Math.Min(1f, pattern.Dispersion
                        + pattern.Participation) * 100f));
                if (approval != current.Approval)
                    changedDimensions.Add("approval");
                if (normality != current.Normality)
                    changedDimensions.Add("normality");
                if (prestige != current.Prestige)
                    changedDimensions.Add("prestige");
                if (salience != current.Salience)
                    changedDimensions.Add("salience");
                if (approval == current.Approval
                    && normality == current.Normality
                    && prestige == current.Prestige
                    && salience == current.Salience) continue;
                current.Approval = approval;
                current.Normality = normality;
                current.Prestige = prestige;
                current.Salience = salience;
                current.Provenance = "current";
                current.SourceIdentity = "social response";
                current.EvidenceSignature = pattern.EvidenceSignature;
                current.LastChangedTick = tick;
                current.Weight = Math.Max(1, Math.Min(100,
                    pattern.ObservedPawnCount));
                changedSubjects.Add(pattern.SubjectKey);
            }
            return new CACulturalMeaningTransitionResult
            {
                Changed = changedSubjects.Count > 0,
                Cause = changedSubjects.Count == 0 ? null
                    : "sustained social interpretation",
                EvidenceSignature = CASocialPatternKernel.StableHash(
                    string.Join("|", evidence.OrderBy(value => value,
                        StringComparer.Ordinal))),
                ChangedSubjectKeys = changedSubjects.OrderBy(value => value,
                    StringComparer.Ordinal).ToList(),
                ChangedDimensions = changedDimensions.OrderBy(value => value,
                    StringComparer.Ordinal).ToList(),
                Meanings = result
            };
        }

        private static int Blend(int before, int observed)
        {
            return (int)Math.Round((before * 3d + observed) / 4d,
                MidpointRounding.AwayFromZero);
        }
    }
}
