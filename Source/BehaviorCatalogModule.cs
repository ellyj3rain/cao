using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Initiative answers how much unowned action a player pawn may originate.
    // Permission, authority, knowledge and material capability remain separate.
    public enum CAInitiativeTier : byte
    {
        Standard = 0,
        Proactive = 1,
        Autonomous = 2
    }

    public enum CABehaviorDomain : byte
    {
        KnowledgeAndCommunication = 0,
        SurvivalAndImmediateSafety = 1,
        WelfareAndCare = 2,
        HazardResponse = 3,
        OperationalReadiness = 4,
        CombatSelfPreservation = 5,
        TacticalCoordination = 6,
        DomesticAndSpatial = 7,
        LogisticsAndProvision = 8,
        SocialAndPolitical = 9,
        CustodyAndAftermath = 10,
        AnimalCare = 11,
        InstitutionalDevelopment = 12,
        Presentation = 13,
        Diagnostics = 14
    }

    public enum CABehaviorForm : byte
    {
        Observation = 0,
        NativeAugmentation = 1,
        Safeguard = 2,
        ReactiveResponse = 3,
        PreventiveReadiness = 4,
        CoordinatedResponse = 5,
        PersistentObjective = 6,
        AdaptivePlanning = 7,
        InstitutionalAction = 8,
        DirectOrder = 9,
        ExecutionCapability = 10,
        Presentation = 11,
        Diagnostic = 12
    }

    [Flags]
    public enum CAActorContext : ushort
    {
        None = 0,
        PlayerPawn = 1 << 0,
        PlayerColony = 1 << 1,
        PlayerSpatialAuthority = 1 << 2,
        NPCPawn = 1 << 3,
        NPCSettlement = 1 << 4,
        NPCInstitution = 1 << 5,
        CreationAuthor = 1 << 6,
        WorldSimulation = 1 << 7
    }

    [Flags]
    public enum CAAuthorityOrigin : ushort
    {
        None = 0,
        OperatorDirect = 1 << 0,
        OperatorRelay = 1 << 1,
        PlayerDelegated = 1 << 2,
        NativeDuty = 1 << 3,
        PeerRequest = 1 << 4,
        AutomaticDefense = 1 << 5,
        Institutional = 1 << 6,
        Household = 1 << 7,
        Organization = 1 << 8,
        Continuation = 1 << 9,
        SaveRestore = 1 << 10,
        WorldAuthoring = 1 << 11
    }

    [Flags]
    public enum CADispositionAxis : byte
    {
        None = 0,
        Courage = 1 << 0,
        Discipline = 1 << 1,
        Aggression = 1 << 2,
        Empathy = 1 << 3,
        Conformity = 1 << 4,
        Initiative = 1 << 5,
        Skepticism = 1 << 6
    }

    public enum CAKnowledgeRequirement : byte
    {
        None = 0,
        CurrentFact = 1,
        FreshFact = 2,
        DirectSightAtExecution = 3,
        MissionManifest = 4,
        MaterialDeficit = 5
    }

    public enum CASettingKind : byte
    {
        AlwaysAvailable = 0,
        FeaturePermission = 1,
        CommunicationInfrastructure = 2,
        ExecutionCapability = 3,
        Presentation = 4,
        Diagnostic = 5
    }

    public enum CASettingKey : byte
    {
        None = 0,
        EatSmart,
        CriticalHauling,
        LifeSafety,
        RescueOutsiders,
        AreaDuty,
        AutoUnforbidCorpses,
        AutoDigGraves,
        RaidResponse,
        EnemyRestraint,
        BuddyCarry,
        Gossip,
        SurvivalResponses,
        FieldMedicine,
        AmbushStrikes,
        AnimalCare,
        FireResponse,
        WeaponTransitions,
        Withdrawals,
        MovingFire,
        HoldOrders,
        DragOrders,
        TrapAwareness,
        BattleDrills,
        KnowledgeContacts,
        AuthorityObedience,
        OperationalAccess,
        AutonomousHomePlanning,
        CommsSystem,
        DraftChain,
        BodyWeight,
        DualWield,
        RenderArms,
        InformationDetail,
        TraceBehavior
    }

    public sealed class CABehaviorDefinition
    {
        public int Index { get; internal set; }
        public string Key { get; }
        public string Label { get; }
        public string CompactText { get; }
        public string StandardText { get; }
        public string ExpandedText { get; }
        public CABehaviorDomain Domain { get; }
        public CABehaviorDomain[] RelatedDomains { get; }
        public CABehaviorForm Form { get; }
        public CABehaviorForm[] RelatedForms { get; }
        public CAActorContext ActorContexts { get; }
        public CASettingKey Setting { get; }
        public bool AlwaysAvailable { get; }
        public CAInitiativeTier MinimumInitiative { get; }
        public bool InitiativeIndependent { get; }
        public bool StandardMayContinue { get; }
        public bool AuthorityCeilingApplies { get; }
        public bool InstitutionalAuthority { get; }
        public CAKnowledgeRequirement KnowledgeRequirement { get; }
        public bool AcceptsRelayedKnowledge { get; }
        public bool LiveValidationRequired { get; }
        public int MaximumKnowledgeAgeTicks { get; }
        public float MinimumKnowledgeConfidence { get; }
        public bool UncertaintyAffectsRanking { get; }
        public CADispositionAxis DispositionAxes { get; }
        public CAAuthorityOrigin AllowedAuthorityOrigins { get; }
        public string AuthorityRequirement { get; }
        public string ExecutorOwner { get; }
        public string NativeExecutor { get; }
        public string Cadence { get; }
        public string IntentOwnership { get; }
        public string InterruptionPolicy { get; }
        public string PersistenceModel { get; }
        public string CompletionCondition { get; }
        public string StandDownCondition { get; }
        public string PlayerVeto { get; }
        public bool ExposeInUi { get; }
        public bool AuthorizesOrigination => Form != CABehaviorForm.Observation
            && Form != CABehaviorForm.NativeAugmentation
            && Form != CABehaviorForm.DirectOrder
            && Form != CABehaviorForm.ExecutionCapability
            && Form != CABehaviorForm.Presentation
            && Form != CABehaviorForm.Diagnostic;

        internal CABehaviorDefinition(string key, string label,
            string compactText, CABehaviorDomain domain, CABehaviorForm form,
            CAActorContext actorContexts, CASettingKey setting,
            CAInitiativeTier minimumInitiative, bool initiativeIndependent,
            bool standardMayContinue, bool authorityCeilingApplies,
            bool institutionalAuthority, CAKnowledgeRequirement knowledge,
            bool acceptsRelayed, bool liveValidation, int maximumAge,
            CADispositionAxis axes, CAAuthorityOrigin origins,
            string authorityRequirement, string executorOwner,
            string nativeExecutor, string cadence, string intentOwnership,
            string completion, string standDown, string expanded = null,
            CABehaviorDomain[] relatedDomains = null,
            CABehaviorForm[] relatedForms = null, bool exposeInUi = true,
            float minimumKnowledgeConfidence = -1f,
            bool uncertaintyAffectsRanking = false)
        {
            Key = key;
            Label = label;
            CompactText = compactText;
            Domain = domain;
            Form = form;
            ActorContexts = actorContexts;
            Setting = setting;
            AlwaysAvailable = setting == CASettingKey.None;
            MinimumInitiative = minimumInitiative;
            InitiativeIndependent = initiativeIndependent;
            StandardMayContinue = standardMayContinue;
            AuthorityCeilingApplies = authorityCeilingApplies;
            InstitutionalAuthority = institutionalAuthority;
            KnowledgeRequirement = knowledge;
            AcceptsRelayedKnowledge = acceptsRelayed;
            LiveValidationRequired = liveValidation;
            MaximumKnowledgeAgeTicks = maximumAge;
            MinimumKnowledgeConfidence = minimumKnowledgeConfidence >= 0f
                ? Mathf.Clamp01(minimumKnowledgeConfidence)
                : knowledge == CAKnowledgeRequirement.None ? 0f : 0.55f;
            UncertaintyAffectsRanking = uncertaintyAffectsRanking;
            DispositionAxes = axes;
            AllowedAuthorityOrigins = origins;
            AuthorityRequirement = authorityRequirement ?? "declared behavior owner";
            ExecutorOwner = executorOwner;
            NativeExecutor = nativeExecutor ?? "native RimWorld system";
            Cadence = cadence ?? "native evaluation";
            IntentOwnership = intentOwnership ?? "one bounded behavior episode";
            InterruptionPolicy = "Direct player work and higher-priority native duties remain authoritative.";
            PersistenceModel = standardMayContinue ? "Owned intent may continue at Standard." : "No persistence beyond its declared owner.";
            CompletionCondition = completion ?? "The declared target condition is satisfied.";
            StandDownCondition = standDown ?? "The fact, authority, capability, or material basis ceases to be valid.";
            PlayerVeto = "Direct orders, enabled colony policies, authored spaces, cancellation, and ownership remain authoritative. Per-object forbids remain authoritative unless the enabled feature is itself an explicit access-reconciliation policy.";
            RelatedDomains = relatedDomains ?? Array.Empty<CABehaviorDomain>();
            RelatedForms = relatedForms ?? Array.Empty<CABehaviorForm>();
            ExposeInUi = exposeInUi;
            StandardText = compactText + " Uses " + NativeExecutor + ".";
            ExpandedText = expanded ?? StandardText + " Authority: "
                + AuthorityRequirement + " Cadence: " + Cadence
                + " Completion: " + CompletionCondition;
        }

        public bool AppliesTo(CAActorContext context)
        {
            return (ActorContexts & context) != 0;
        }
    }

    public static class CABehaviorCatalog
    {
        private const CAActorContext Player = CAActorContext.PlayerPawn;
        private const CAActorContext Colony = CAActorContext.PlayerColony;
        private const CAActorContext Spatial = CAActorContext.PlayerSpatialAuthority;
        private const CAActorContext Npc = CAActorContext.NPCPawn;
        private const CAActorContext NpcInstitution = CAActorContext.NPCSettlement | CAActorContext.NPCInstitution;
        private const CAAuthorityOrigin PlayerOrigins = CAAuthorityOrigin.OperatorDirect
            | CAAuthorityOrigin.OperatorRelay | CAAuthorityOrigin.PlayerDelegated
            | CAAuthorityOrigin.NativeDuty | CAAuthorityOrigin.PeerRequest
            | CAAuthorityOrigin.AutomaticDefense | CAAuthorityOrigin.Continuation
            | CAAuthorityOrigin.SaveRestore;
        private const CAAuthorityOrigin NpcOrigins = CAAuthorityOrigin.NativeDuty
            | CAAuthorityOrigin.Institutional | CAAuthorityOrigin.Household
            | CAAuthorityOrigin.Organization | CAAuthorityOrigin.AutomaticDefense
            | CAAuthorityOrigin.Continuation | CAAuthorityOrigin.SaveRestore;

        public static readonly CABehaviorDefinition[] All;
        public static readonly IReadOnlyDictionary<string, CABehaviorDefinition> ByKey;
        private static readonly Dictionary<CABehaviorDomain, CABehaviorDefinition[]> byDomain;
        private static readonly Dictionary<CASettingKey, CABehaviorDefinition[]> bySetting;

        static CABehaviorCatalog()
        {
            var list = new List<CABehaviorDefinition>();

            // Knowledge and communication.
            Add(list, "knowledge.threat_observation", "Threat observation", "Records actor-private threat evidence.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.Observation, Player | Npc, CASettingKey.KnowledgeContacts, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.Skepticism, PlayerOrigins | NpcOrigins, "legitimate sight, damage, or sensor evidence", "KnowledgeMapComponent", "event-driven observation", "event-driven", "actor-private fact", "the observation is recorded", "the evidence cannot be acquired");
            Add(list, "knowledge.acoustic_investigation", "Acoustic investigation", "Checks one uncertain gunfire report from a bounded standoff position.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.ReactiveResponse, Player | Npc, CASettingKey.KnowledgeContacts, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.FreshFact, true, false, 2500, CADispositionAxis.Courage | CADispositionAxis.Discipline | CADispositionAxis.Initiative | CADispositionAxis.Skepticism, PlayerOrigins | NpcOrigins, "personal verification or current NPC duty", "CombatReactionModule", "native bounded investigation job", "constant-think after reaction delay", "one acoustic-event episode", "the vantage is checked", "the cue, route, authority, or player ownership expires");
            Add(list, "knowledge.welfare_observation", "Welfare observation", "Records gross welfare state; clinical detail requires assessment.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.Observation, Player | Npc, CASettingKey.KnowledgeContacts, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.Empathy | CADispositionAxis.Skepticism, PlayerOrigins | NpcOrigins, "legitimate observation or finite mission evidence", "WelfareKnowledgeModule", "event-driven observation", "event-driven", "actor-private welfare fact", "the observation is recorded", "the evidence cannot be acquired");
            Add(list, "communication.knowledge_relay", "Knowledge relay", "Copies an honestly held fact through a real communication route.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.NativeAugmentation, Player | Npc, CASettingKey.CommsSystem, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.CurrentFact, false, false, 0, CADispositionAxis.Skepticism, PlayerOrigins | NpcOrigins, "sender owns the fact and a physical route reaches the receiver", "CommsModule", "voice, radio, mental, or gesture route", "event-driven", "copied fact with original age and uncertainty", "delivery succeeds or fails", "the route or source fact is unavailable");
            Add(list, "communication.status_report", "Status report", "Reports one current actor-local status through an adopted group practice.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.CoordinatedResponse, Player, CASettingKey.CommsSystem, CAInitiativeTier.Proactive, false, false, false, true, CAKnowledgeRequirement.CurrentFact, true, false, 2500, CADispositionAxis.Discipline | CADispositionAxis.Conformity, PlayerOrigins | CAAuthorityOrigin.Organization, "adopted status-reporting practice and a valid communication route", "OrganizationModule", "actor-local report and communication route", "bounded institutional cadence", "one status report", "the current status is reported", "the practice, fact, route, or threat basis ends", uncertaintyAffectsRanking: true);
            Add(list, "communication.command_delivery", "Command delivery", "Delivers a command before obedience is evaluated.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.NativeAugmentation, Player | Npc, CASettingKey.DraftChain, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, PlayerOrigins | NpcOrigins, "operator, commander, or institutional command plus a physical route", "CommsModule", "command route", "on command", "one delivery receipt", "delivery succeeds or fails", "the route cannot carry the command");
            Add(list, "authority.relay_obedience", "Relayed obedience", "Separates delivered orders from compliance, reluctance, or refusal.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.NativeAugmentation, Player | Npc, CASettingKey.AuthorityObedience, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.Conformity | CADispositionAxis.Discipline, PlayerOrigins | NpcOrigins, "confirmed delivery and command standing", "AuthorityModule", "native ordered action", "after delivery", "one obedience decision", "the follower complies, complies reluctantly, or refuses", "delivery failed, so obedience is not evaluated");
            Add(list, "social.gossip_praise", "Supportive gossip", "Weights native social exchange toward earned praise.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.NativeAugmentation, Player | Npc, CASettingKey.Gossip, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.CurrentFact, true, false, 0, CADispositionAxis.Empathy | CADispositionAxis.Conformity | CADispositionAxis.Skepticism, PlayerOrigins | NpcOrigins, "native social opportunity", "GossipModule", "native social interaction", "native social cadence", "native interaction", "the interaction resolves", "no valid social opportunity");
            Add(list, "social.gossip_poison", "Hostile gossip", "Weights native social exchange toward credible reputational harm.", CABehaviorDomain.KnowledgeAndCommunication, CABehaviorForm.NativeAugmentation, Player | Npc, CASettingKey.Gossip, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.CurrentFact, true, false, 0, CADispositionAxis.Aggression | CADispositionAxis.Conformity | CADispositionAxis.Skepticism, PlayerOrigins | NpcOrigins, "native social opportunity", "GossipModule", "native social interaction", "native social cadence", "native interaction", "the interaction resolves", "no valid social opportunity");

            // Survival, welfare, hazards and animals.
            Add(list, "survival.food_choice", "Safer food choice", "Improves routine food selection without replacing native food jobs.", CABehaviorDomain.SurvivalAndImmediateSafety, CABehaviorForm.NativeAugmentation, Player, CASettingKey.EatSmart, CAInitiativeTier.Standard, false, false, false, false, CAKnowledgeRequirement.None, true, false, 0, CADispositionAxis.None, PlayerOrigins, "native food need", "EatSmartPatch", "native food job", "native think evaluation", "native food job", "food is acquired", "forced work or emergency hunger owns the action");
            Add(list, "survival.immediate_evasion", "Immediate evasion", "Escapes one directly perceived imminent hazard.", CABehaviorDomain.SurvivalAndImmediateSafety, CABehaviorForm.Safeguard, Player | Npc, CASettingKey.SurvivalResponses, CAInitiativeTier.Standard, false, false, false, false, CAKnowledgeRequirement.DirectSightAtExecution, false, true, 90, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins | NpcOrigins, "actor-local survival floor", "ImmediateCombatModule", "native movement job", "constant-think", "one hazard episode", "the pawn clears the hazard envelope", "the hazard expires or no improving route exists");
            Add(list, "survival.shelter", "Shelter", "Reaches and retains one defensible shelter objective.", CABehaviorDomain.SurvivalAndImmediateSafety, CABehaviorForm.PersistentObjective, Player | Npc, CASettingKey.SurvivalResponses, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.FreshFact, true, false, 2500, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins | NpcOrigins, "known threat and owned shelter responsibility", "SurvivalModule", "native duty and movement", "native tactical cadence", "one shelter intent", "shelter is reached and threat condition clears", "the shelter or threat basis becomes invalid");
            Add(list, "survival.raid_alarm_shelter", "Raid-alarm shelter", "Moves a civilian into enclosed Home shelter under the current native colony alarm without granting hostile identity.", CABehaviorDomain.SurvivalAndImmediateSafety, CABehaviorForm.PersistentObjective, Player | Npc, CASettingKey.SurvivalResponses, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins | NpcOrigins, "current native colony raid alarm and civilian safety responsibility", "RaidResponseModule", "native shelter job", "250-tick raid-response pass", "one alarm shelter episode", "the civilian reaches enclosed Home shelter and the alarm clears", "the alarm, route, shelter, permission, or player ownership fails");
            Add(list, "survival.freeze", "Freeze response", "Briefly freezes a vulnerable civilian under a freshly known local threat.", CABehaviorDomain.SurvivalAndImmediateSafety, CABehaviorForm.Safeguard, Player, CASettingKey.SurvivalResponses, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.FreshFact, true, false, 2500, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins, "actor-local threat response", "RaidResponseModule", "native wait job", "once per raid", "one freeze episode", "the brief freeze expires", "the threat, permission, or player ownership fails", uncertaintyAffectsRanking: true);
            Add(list, "survival.combat_recovery", "Combat recovery", "Preserves immediate viability before broader combat action.", CABehaviorDomain.SurvivalAndImmediateSafety, CABehaviorForm.Safeguard, Player | Npc, CASettingKey.RaidResponse, CAInitiativeTier.Standard, false, true, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins | NpcOrigins, "actor-local collapse threshold", "ImmediateCombatModule", "native recovery job", "constant-think", "one recovery episode", "viability is restored or the pawn reaches safety", "recovery is impossible or direct ownership resumes");
            Add(list, "welfare.local_rescue", "Local rescue", "Answers a declared life-safety case with native rescue.", CABehaviorDomain.WelfareAndCare, CABehaviorForm.ReactiveResponse, Player, CASettingKey.LifeSafety, CAInitiativeTier.Standard, false, false, false, false, CAKnowledgeRequirement.FreshFact, true, true, 2500, CADispositionAxis.Empathy, PlayerOrigins, "care responsibility", "LifeSafetyModule", "native rescue", "native think evaluation", "one rescue", "the subject reaches care", "the subject or destination becomes invalid");
            Add(list, "welfare.local_treatment", "Local casualty aid", "Treats or rescues one currently known same-team casualty.", CABehaviorDomain.WelfareAndCare, CABehaviorForm.ReactiveResponse, Player, CASettingKey.FieldMedicine, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.FreshFact, true, true, 2500, CADispositionAxis.Empathy | CADispositionAxis.Discipline, PlayerOrigins, "medical work and current casualty responsibility", "WelfareKnowledgeModule", "native tend or rescue", "native think evaluation", "one casualty-care episode", "the casualty is treated or reaches care", "knowledge, patient, route, capability, or player ownership changes");
            Add(list, "welfare.outsider_rescue", "Outsider rescue", "Allows a bounded rescue of a freshly known outsider.", CABehaviorDomain.WelfareAndCare, CABehaviorForm.ReactiveResponse, Player, CASettingKey.RescueOutsiders, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.FreshFact, true, true, 2500, CADispositionAxis.Empathy, PlayerOrigins, "player permission and current welfare evidence", "LifeSafetyModule", "native rescue", "native think evaluation", "one outsider rescue", "the subject reaches care", "the subject or safe destination becomes invalid");
            Add(list, "welfare.mission_triage", "Mission triage", "Triages only the finite incident manifest and assessed casualties.", CABehaviorDomain.WelfareAndCare, CABehaviorForm.ReactiveResponse, Player, CASettingKey.FieldMedicine, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.MissionManifest, true, false, 0, CADispositionAxis.Empathy | CADispositionAxis.Discipline, PlayerOrigins, "incident responsibility or medical role", "MissionTriageModule", "native tend and rescue", "bounded incident evaluation", "one mission casualty objective", "the finite manifest is resolved", "danger, care, or manifest authority ends");
            Add(list, "welfare.accountability_check", "Welfare accountability", "Checks one last-confirmed location for a person owed care.", CABehaviorDomain.WelfareAndCare, CABehaviorForm.ReactiveResponse, Player | NpcInstitution, CASettingKey.FieldMedicine, CAInitiativeTier.Proactive, false, false, false, true, CAKnowledgeRequirement.FreshFact, true, false, 2500, CADispositionAxis.Empathy | CADispositionAxis.Discipline, PlayerOrigins | NpcOrigins, "personal, peer, office, or institutional responsibility", "WelfareKnowledgeModule", "native movement and assessment", "bounded periodic evaluation", "one accountability check", "the person is assessed", "the responsibility or remembered place expires");
            Add(list, "welfare.threshold_support", "Threshold support", "Creates one bounded request-and-support transaction.", CABehaviorDomain.WelfareAndCare, CABehaviorForm.CoordinatedResponse, Player, CASettingKey.FieldMedicine, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.FreshFact, true, false, 2500, CADispositionAxis.Courage | CADispositionAxis.Discipline | CADispositionAxis.Empathy, PlayerOrigins, "peer request, medical role, or delegated support duty", "WelfareSupportModule", "native duties and jobs", "bounded support evaluation", "one shared episode", "crossing and treatment complete", "timeout, refusal, or invalid geometry");
            Add(list, "welfare.buddy_carry", "Buddy carry", "Adds a second capable carrier to an authorized casualty move.", CABehaviorDomain.WelfareAndCare, CABehaviorForm.ExecutionCapability, Player, CASettingKey.BuddyCarry, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.FreshFact, true, true, 2500, CADispositionAxis.Empathy | CADispositionAxis.Discipline, PlayerOrigins, "direct order, fresh casualty fact, or delegated rescue role", "BuddyCarryModule", "native carry and movement", "on casualty movement", "one assisted carry", "the casualty reaches the destination", "either carrier or casualty becomes invalid");
            Add(list, "welfare.medic_dispatch", "Medic dispatch", "Assigns at most one honestly informed medic through owned medical authority.", CABehaviorDomain.WelfareAndCare, CABehaviorForm.AdaptivePlanning, Player, CASettingKey.FieldMedicine, CAInitiativeTier.Autonomous, false, false, false, true, CAKnowledgeRequirement.FreshFact, true, true, 2500, CADispositionAxis.Empathy | CADispositionAxis.Discipline | CADispositionAxis.Initiative, PlayerOrigins, "medical office, squad responsibility, organization duty, or player delegation", "SquadSupportMapComponent", "registered medical intent and native tend/rescue", "90-tick map pass", "one assigned medical episode", "casualty is treated or rescued", "danger, knowledge, authority, or capability fails");
            Add(list, "animal.emergency_care", "Emergency animal care", "Answers directly known starvation or downing with native care.", CABehaviorDomain.AnimalCare, CABehaviorForm.ReactiveResponse, Player, CASettingKey.AnimalCare, CAInitiativeTier.Standard, false, false, false, false, CAKnowledgeRequirement.CurrentFact, false, true, 600, CADispositionAxis.Empathy, PlayerOrigins, "handler eligibility and immediate care responsibility", "AnimalCareModule", "native handling, feeding, and rescue", "native think evaluation", "one animal-care episode", "the emergency clears", "animal, handler, food, or bed becomes invalid");
            Add(list, "animal.preventive_feeding", "Preventive animal feeding", "Feeds one animal at a current bounded starvation risk.", CABehaviorDomain.AnimalCare, CABehaviorForm.PreventiveReadiness, Player, CASettingKey.AnimalCare, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.CurrentFact, false, true, 600, CADispositionAxis.Empathy | CADispositionAxis.Initiative, PlayerOrigins, "handler eligibility and animal-care permission", "AnimalCareModule", "native feeding", "native think evaluation", "one preventive feeding", "the animal is fed", "risk, food, reachability, or restriction changes");
            Add(list, "hazard.fire_response", "Fire response", "Answers a known local fire without displacing forced or high-priority work.", CABehaviorDomain.HazardResponse, CABehaviorForm.ReactiveResponse, Player, CASettingKey.FireResponse, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.CurrentFact, false, true, 600, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins, "home responsibility or delegated containment role", "FireResponseModule", "native firefighting job", "120-tick map pass", "one fire response", "the fire is extinguished", "fire, route, duty, or safety basis fails");
            Add(list, "hazard.trap_awareness", "Trap awareness", "Changes routing only for traps the actor actually knows.", CABehaviorDomain.HazardResponse, CABehaviorForm.NativeAugmentation, Player | Npc, CASettingKey.TrapAwareness, CAInitiativeTier.Standard, false, false, false, false, CAKnowledgeRequirement.CurrentFact, false, false, 0, CADispositionAxis.Skepticism, PlayerOrigins | NpcOrigins, "personally known trap evidence", "TrapAwarenessModule", "native path interpretation", "native path evaluation", "known route constraint", "the route completes", "trap knowledge expires or changes");
            Add(list, "hazard.toxic_waste_response", "Toxic waste response", "Handles waste through authored logistics or institutional responsibility.", CABehaviorDomain.HazardResponse, CABehaviorForm.InstitutionalAction, Spatial | NpcInstitution, CASettingKey.AutonomousHomePlanning, CAInitiativeTier.Autonomous, false, true, true, true, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.None, PlayerOrigins | NpcOrigins, "delegated logistics program or NPC environmental authority", "ToxicWasteLifecycleModule", "native storage and hauling", "bounded map planning cadence", "one waste lifecycle objective", "waste reaches valid custody", "policy, material, or authority fails");

            // Operational readiness and combat.
            Add(list, "operations.shared_item_access", "Shared item access", "Applies colony policy to ordinary visible non-quest items.", CABehaviorDomain.OperationalReadiness, CABehaviorForm.InstitutionalAction, Colony, CASettingKey.OperationalAccess, CAInitiativeTier.Standard, true, false, false, true, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, CAAuthorityOrigin.PlayerDelegated | CAAuthorityOrigin.OperatorDirect | CAAuthorityOrigin.SaveRestore, "player colony policy", "OperationalAccessComponent", "native forbid policy", "60-tick map policy pass", "colony policy", "eligible item policy is reconciled", "permission is disabled or an explicit player forbid owns the item");
            Add(list, "operations.arm_for_known_threat", "Arm for known threat", "Obtains one suitable available weapon before a known threat arrives.", CABehaviorDomain.OperationalReadiness, CABehaviorForm.PreventiveReadiness, Player, CASettingKey.OperationalAccess, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.FreshFact, true, false, 2500, CADispositionAxis.Initiative | CADispositionAxis.Courage, PlayerOrigins, "personal equipment choice inside player policy", "OperationalAccessModule", "native Equip job", "constant-think", "one equipment intent", "the weapon is equipped", "threat, role, ownership, forbid, reachability, or reservation fails");
            Add(list, "operations.wear_protection", "Wear protection", "Obtains compatible protection while a known threat is not yet close.", CABehaviorDomain.OperationalReadiness, CABehaviorForm.PreventiveReadiness, Player, CASettingKey.OperationalAccess, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.FreshFact, true, false, 2500, CADispositionAxis.Initiative | CADispositionAxis.Courage, PlayerOrigins, "personal equipment choice inside player policy", "OperationalAccessModule", "native Wear job", "constant-think with retry clock", "one apparel intent", "protection is worn", "danger is too close or compatibility, policy, ownership, or reachability fails");
            Add(list, "operations.weapon_transition", "Weapon transition", "Changes weapon state for one current close threat.", CABehaviorDomain.OperationalReadiness, CABehaviorForm.ReactiveResponse, Player | Npc, CASettingKey.WeaponTransitions, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.DirectSightAtExecution, false, true, 90, CADispositionAxis.Courage | CADispositionAxis.Aggression, PlayerOrigins | NpcOrigins, "personal combat response or NPC duty", "EquipTransitionModule", "native equipment transition", "constant-think", "one contact transition", "range opens or contact ends", "direct work, role, compatibility, or threat basis fails");
            Add(list, "operations.dual_wield", "Dual wield", "Makes off-hand execution available to an already authorized combat intent.", CABehaviorDomain.OperationalReadiness, CABehaviorForm.ExecutionCapability, Player | Npc, CASettingKey.DualWield, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, PlayerOrigins | NpcOrigins, "the owning combat intent", "OffhandModule", "off-hand native verb execution", "owned combat execution", "capability only", "the owning combat intent completes", "equipment or compatibility fails");
            Add(list, "operations.body_weight", "Body weight", "Makes body-weight effects available to already authorized movement and carry actions.", CABehaviorDomain.OperationalReadiness, CABehaviorForm.ExecutionCapability, Player | Npc, CASettingKey.BodyWeight, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, PlayerOrigins | NpcOrigins, "the owning native action", "CompetenceModule", "native stat and movement calculation", "native execution", "capability only", "the owning action completes", "capability is disabled");
            Add(list, "combat.local_reaction", "Local combat reaction", "Responds to one current target or fresh bounded contact without blind tracking.", CABehaviorDomain.CombatSelfPreservation, CABehaviorForm.ReactiveResponse, Player | Npc, CASettingKey.RaidResponse, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.FreshFact, true, false, 600, CADispositionAxis.Courage | CADispositionAxis.Aggression | CADispositionAxis.Discipline, PlayerOrigins | NpcOrigins, "personal defense, automatic defense, or native combat duty", "RaidResponseModule", "native combat job", "constant-think", "one local combat intent", "the contact resolves", "contact, lane safety, duty, or player ownership fails", minimumKnowledgeConfidence: 0.55f, uncertaintyAffectsRanking: true);
            Add(list, "combat.fire_position_adjustment", "Fire-position adjustment", "Selects one materially better nearby position for the current contact.", CABehaviorDomain.CombatSelfPreservation, CABehaviorForm.ReactiveResponse, Player | Npc, CASettingKey.RaidResponse, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.DirectSightAtExecution, true, true, 300, CADispositionAxis.Courage | CADispositionAxis.Discipline | CADispositionAxis.Initiative, PlayerOrigins | NpcOrigins, "owned combat intent and bounded tactical space", "ImmediateCombatModule", "native movement and combat", "constant-think", "one position episode", "a materially better firing solution is reached", "target, safety, route, or ownership fails");
            Add(list, "combat.assault_approach", "Assault approach", "Moves an assaulter toward one native-duty objective without replacing the assault plan.", CABehaviorDomain.TacticalCoordination, CABehaviorForm.ReactiveResponse, Npc, CASettingKey.None, CAInitiativeTier.Standard, true, true, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.Courage | CADispositionAxis.Discipline, CAAuthorityOrigin.NativeDuty | CAAuthorityOrigin.Continuation | CAAuthorityOrigin.SaveRestore, "current native assault duty", "AssaultApproachModule", "native movement job", "native assault think lane", "one assault approach episode", "the pawn reaches the bounded approach cell", "the native duty, objective, route, or safety basis changes");
            Add(list, "combat.hold_deviation", "Hold deviation", "Allows one declared survival deviation while preserving the authored hold purpose.", CABehaviorDomain.CombatSelfPreservation, CABehaviorForm.ReactiveResponse, Player, CASettingKey.HoldOrders, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.CurrentFact, true, true, 300, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins, "the authored hold episode", "HoldModule", "native tactical duty", "constant-think", "continuation of the hold episode", "the pawn stabilizes or returns", "the hold is released or its purpose is invalid");
            Add(list, "combat.withdrawal_self_preservation", "Self-preserving withdrawal", "Breaks contact for one actor-local injury or collapsing position.", CABehaviorDomain.CombatSelfPreservation, CABehaviorForm.ReactiveResponse, Player | Npc, CASettingKey.Withdrawals, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.CurrentFact, false, true, 300, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins | NpcOrigins, "personal survival or automatic-defense authority", "WithdrawalModule", "native bounded movement", "constant-think", "one withdrawal intent", "safe separation is reached", "route, threat, or ownership fails");
            Add(list, "combat.withdrawal_coordinated", "Coordinated withdrawal", "Coordinates a bounded covered movement through legitimate command or peer support.", CABehaviorDomain.CombatSelfPreservation, CABehaviorForm.CoordinatedResponse, Player | NpcInstitution, CASettingKey.Withdrawals, CAInitiativeTier.Autonomous, false, true, false, true, CAKnowledgeRequirement.FreshFact, true, true, 600, CADispositionAxis.Courage | CADispositionAxis.Discipline | CADispositionAxis.Conformity, PlayerOrigins | NpcOrigins, "squad, command, peer request, or institutional combat authority", "WithdrawalModule", "native bounded movement and fire", "supervised tactical tick", "one shared movement episode", "the group reaches the destination", "delivery, obedience, route, threat, or authority fails");
            Add(list, "combat.moving_fire", "Moving fire", "Makes firing during an authorized movement intent available.", CABehaviorDomain.CombatSelfPreservation, CABehaviorForm.ExecutionCapability, Player | Npc, CASettingKey.MovingFire, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.DirectSightAtExecution, false, true, 90, CADispositionAxis.None, PlayerOrigins | NpcOrigins, "an owning direct, withdrawal, hold, or combat intent", "MovingFireModule", "native combat verb", "owned movement execution", "capability only", "the owning movement completes", "target, lane, weapon, or owning intent fails");

            // Direct tactical commands and autonomous support.
            Direct(list, "order.hold", "Hold", "Defends one player-authored local position.", CASettingKey.HoldOrders, "HoldModule", "the hold is released or its tactical purpose ends");
            Direct(list, "order.ambush", "Ambush", "Stages and releases one player-authored synchronized ambush.", CASettingKey.AmbushStrikes, "ArrangementModule", "the ambush releases or aborts");
            Direct(list, "order.formation", "Formation", "Maintains one player-authored formation purpose.", CASettingKey.RaidResponse, "ArrangementModule", "the formation is released or invalid");
            Direct(list, "order.withdrawal", "Direct withdrawal", "Executes one player-authored withdrawal plan.", CASettingKey.Withdrawals, "WithdrawalModule", "the destination is reached or the order is released");
            Direct(list, "order.drag", "Drag casualty", "Moves one named casualty along the player-authored route.", CASettingKey.DragOrders, "DragModule", "the casualty reaches the destination");
            Direct(list, "order.rally", "Rally", "Moves assigned actors to one player-authored rally point.", CASettingKey.RaidResponse, "ArrangementModule", "the rally point is reached or released");
            Direct(list, "order.patrol", "Patrol", "Executes one player-authored patrol route.", CASettingKey.AreaDuty, "PatrolSystemModule", "the route is released");
            Direct(list, "order.painted_objective", "Painted objective", "Establishes one player-authored tactical objective.", CASettingKey.RaidResponse, "TacticalOverlayModule", "the objective is removed or resolved");
            Direct(list, "order.defensive_line", "Painted defensive line", "Establishes one player-authored defensive line.", CASettingKey.RaidResponse, "TacticalOverlayModule", "the line is removed or released");
            Direct(list, "order.equipment", "Direct equipment order", "Equips the exact item selected by the player.", CASettingKey.OperationalAccess, "OperationalAccessModule", "the equipment order completes");
            Direct(list, "order.build_designation", "Build designation", "Creates the exact native designation selected by the player.", CASettingKey.None, "SpatialFurnishingModule", "the designation completes or is cancelled");
            Direct(list, "order.spatial_program", "Authored spatial program", "Defines the cells and purpose of a player-authored space.", CASettingKey.None, "SpatialInitiativeModule", "the player revises or removes the program");
            Add(list, "support.fold_cover", "Fold cover", "Answers one valid request to cover a collapsing position.", CABehaviorDomain.TacticalCoordination, CABehaviorForm.CoordinatedResponse, Player, CASettingKey.Withdrawals, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.CurrentFact, true, true, 600, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins, "peer request or squad command", "SquadSupportMapComponent", "native automatic-defense duty", "90-tick pass and event request", "one support tasking", "the supported withdrawal completes", "request, contact, route, or capability fails");
            Add(list, "support.rescue_escort", "Rescue escort", "Covers one specific known casualty crossing.", CABehaviorDomain.TacticalCoordination, CABehaviorForm.CoordinatedResponse, Player, CASettingKey.BuddyCarry, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.FreshFact, true, false, 600, CADispositionAxis.Courage | CADispositionAxis.Empathy | CADispositionAxis.Discipline, PlayerOrigins, "fresh casualty fact plus rescue or medical authority", "SquadSupportMapComponent", "native automatic-defense duty", "90-tick pass", "one escort tasking", "the crossing completes", "casualty, rescuer, threat, route, or authority fails", minimumKnowledgeConfidence: 0.55f, uncertaintyAffectsRanking: true);
            Support(list, "support.objective_defense", "Objective defense", "Self-assigns one uncovered authored objective.", CASettingKey.RaidResponse);
            Support(list, "support.defensive_line", "Defensive-line assignment", "Occupies one unowned post on the authored defensive line.", CASettingKey.RaidResponse);
            Support(list, "support.flank_guard", "Flank guard", "Covers one honestly known uncovered approach.", CASettingKey.RaidResponse);
            Support(list, "support.rally_reroute", "Rally reroute", "Reroutes one straggler through a valid rally purpose.", CASettingKey.RaidResponse);
            Add(list, "support.reported_contact_reorganization", "Reported-contact reorganization", "Makes one bounded correction or maintains a delegated posture from shared honest contacts.", CABehaviorDomain.TacticalCoordination, CABehaviorForm.AdaptivePlanning, Player | NpcInstitution, CASettingKey.RaidResponse, CAInitiativeTier.Proactive, false, true, false, true, CAKnowledgeRequirement.FreshFact, true, true, 600, CADispositionAxis.Courage | CADispositionAxis.Discipline | CADispositionAxis.Initiative, PlayerOrigins | NpcOrigins, "squad, command, or institutional defensive authority", "SquadSupportMapComponent", "native tactical duties", "90-tick pass", "one defensive episode", "the uncovered condition is resolved", "knowledge, post, authority, or threat becomes invalid");
            Direct(list, "support.battle_drill", "Battle drill", "Executes one assigned player-authored drill.", CASettingKey.BattleDrills, "DrillsModule", "the drill completes or aborts");
            Add(list, "support.stack_auto_breach", "Stack auto-breach", "Releases a prepared player-authored stack when every assigned actor is set.", CABehaviorDomain.TacticalCoordination, CABehaviorForm.AdaptivePlanning, Player, CASettingKey.BattleDrills, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.CurrentFact, false, true, 600, CADispositionAxis.Discipline | CADispositionAxis.Initiative, PlayerOrigins, "the active player-authored stack episode", "DrillsModule", "native stack lord and breach duty", "stack duty cadence", "continuation of one stack episode", "every assigned actor is set and the breach releases", "the stack, door, actor readiness, or operator ownership becomes invalid");
            Add(list, "support.ambush_adaptation", "Ambush adaptation", "Preserves the authored ambush while allowing a bounded survival correction.", CABehaviorDomain.TacticalCoordination, CABehaviorForm.ReactiveResponse, Player, CASettingKey.AmbushStrikes, CAInitiativeTier.Proactive, false, true, false, false, CAKnowledgeRequirement.CurrentFact, false, true, 300, CADispositionAxis.Courage | CADispositionAxis.Discipline, PlayerOrigins, "the authored ambush episode", "ArrangementModule", "native tactical duty", "constant-think", "continuation of one ambush episode", "the actor stabilizes inside the authored purpose", "the ambush releases or survival requires abort");
            Add(list, "support.ambush_aftermath", "Ambush aftermath", "Conceals one ambush casualty and returns to the authored position after local danger clears.", CABehaviorDomain.TacticalCoordination, CABehaviorForm.AdaptivePlanning, Player, CASettingKey.AmbushStrikes, CAInitiativeTier.Autonomous, false, true, false, false, CAKnowledgeRequirement.CurrentFact, false, true, 600, CADispositionAxis.Discipline | CADispositionAxis.Initiative, PlayerOrigins, "the active player-authored ambush episode", "ArrangementModule", "native hauling and ambush duty", "bounded ambush aftermath pass", "continuation of one ambush episode", "the casualty is concealed and the actor returns", "the corpse, concealment, local safety, or authored ambush becomes invalid");

            // Custody, logistics, spatial and institutions.
            Add(list, "aftermath.secure_hostile", "Secure hostile", "Secures or stabilizes one personally confirmed downed hostile after local combat.", CABehaviorDomain.CustodyAndAftermath, CABehaviorForm.ReactiveResponse, Player, CASettingKey.RaidResponse, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.DirectSightAtExecution, false, true, 300, CADispositionAxis.Empathy | CADispositionAxis.Discipline, PlayerOrigins, "local security pair and friendly-casualty priority clear", "CombatAftermathModule", "native secure, capture, and tend jobs", "native think evaluation", "one custody intake episode", "the hostile is secured or stabilized", "local threat, security, casualty priority, or reachability fails");
            Add(list, "aftermath.custody_resolution", "Custody resolution", "Resolves irreversible custody outcomes only through law, office, or direct operator authority.", CABehaviorDomain.CustodyAndAftermath, CABehaviorForm.InstitutionalAction, Player | NpcInstitution, CASettingKey.EnemyRestraint, CAInitiativeTier.Autonomous, false, false, false, true, CAKnowledgeRequirement.DirectSightAtExecution, false, true, 300, CADispositionAxis.Empathy | CADispositionAxis.Discipline | CADispositionAxis.Aggression, PlayerOrigins | NpcOrigins, "settlement law, custody office, command authority, NPC institution, or direct operator", "CombatAftermathModule", "native custody, treatment, or execution", "after secure intake", "one custody decision", "the decision is recorded and executed", "authority, identity, care, or custody basis fails");
            Add(list, "aftermath.unlawful_execution_breach", "Unlawful custody breach", "Allows a separately recorded character-driven impulsive breach without treating it as legitimate custody authority.", CABehaviorDomain.CustodyAndAftermath, CABehaviorForm.ReactiveResponse, Player, CASettingKey.RaidResponse, CAInitiativeTier.Proactive, false, false, false, false, CAKnowledgeRequirement.DirectSightAtExecution, false, true, 300, CADispositionAxis.Aggression | CADispositionAxis.Discipline, PlayerOrigins, "character breach with explicit accountability; no institutional legitimacy", "CombatAftermathModule", "native execution and accountability record", "after secure approach", "one unlawful breach episode", "the unlawful act and accountability are recorded", "impulse, target, or direct observation is no longer valid");
            Add(list, "npc.enemy_restraint", "Enemy restraint", "Weights NPC target restraint through NPC doctrine rather than player autonomy.", CABehaviorDomain.CustodyAndAftermath, CABehaviorForm.NativeAugmentation, Npc, CASettingKey.EnemyRestraint, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.CurrentFact, false, true, 300, CADispositionAxis.Empathy | CADispositionAxis.Aggression, NpcOrigins, "NPC combat duty", "EnemyRestraintModule", "native target selection", "native combat evaluation", "NPC doctrine", "target selection resolves", "target or duty becomes invalid");
            Add(list, "npc.captive_stabilization", "Captive stabilization", "Stabilizes one captive through current raider duty and custody ownership.", CABehaviorDomain.CustodyAndAftermath, CABehaviorForm.ReactiveResponse, Npc, CASettingKey.EnemyRestraint, CAInitiativeTier.Standard, true, false, false, true, CAKnowledgeRequirement.CurrentFact, false, true, 600, CADispositionAxis.Empathy | CADispositionAxis.Discipline, NpcOrigins, "current raider duty and custody ownership", "EnemyRestraintModule", "native treatment", "250-tick map pass", "one stabilization", "the captive stabilizes", "duty, captive, medicine, or route fails");
            Add(list, "logistics.critical_haul", "Critical haul", "Raises deteriorating valuables inside native hauling priority.", CABehaviorDomain.LogisticsAndProvision, CABehaviorForm.NativeAugmentation, Player, CASettingKey.CriticalHauling, CAInitiativeTier.Standard, false, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, PlayerOrigins, "native hauling work", "CriticalHaulModule", "native hauling", "native work scan", "native haul job", "the item reaches storage or cover", "item, storage, route, or work authority fails");
            Add(list, "logistics.corpse_access", "Corpse access", "Applies calm-aftermath corpse access as an enabled colony policy.", CABehaviorDomain.LogisticsAndProvision, CABehaviorForm.NativeAugmentation, Colony, CASettingKey.AutoUnforbidCorpses, CAInitiativeTier.Standard, true, false, false, true, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, CAAuthorityOrigin.PlayerDelegated | CAAuthorityOrigin.SaveRestore, "enabled player colony access policy", "CorpseDisciplineModule", "native forbid and hauling policy", "bounded aftermath pass", "colony policy", "eligible corpse access is reconciled", "combat resumes or the colony policy is disabled");
            Add(list, "logistics.grave_planning", "Grave planning", "Extends an existing player graveyard when burial capacity is short.", CABehaviorDomain.LogisticsAndProvision, CABehaviorForm.InstitutionalAction, Colony, CASettingKey.AutoDigGraves, CAInitiativeTier.Standard, true, false, false, true, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.None, CAAuthorityOrigin.PlayerDelegated | CAAuthorityOrigin.SaveRestore, "enabled colony burial policy and an existing player-authored graveyard", "CorpseDisciplineModule", "native grave designation", "bounded 1,800-tick planning pass", "one burial-capacity demand", "capacity meets demand", "graveyard seed, land, policy, or placement feasibility fails");
            Add(list, "logistics.storage_capacity", "Storage capacity", "Adds one capacity furnishing when an authorized stockpile crosses its threshold.", CABehaviorDomain.LogisticsAndProvision, CABehaviorForm.PreventiveReadiness, Player | Spatial, CASettingKey.AutonomousHomePlanning, CAInitiativeTier.Proactive, false, true, true, false, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.Initiative, PlayerOrigins, "native stockpile initiative ceiling", "StorageProgramModule", "native blueprint and hauling", "600-tick spatial pass", "one storage-capacity intent", "capacity pressure clears", "stockpile, ceiling, material, or veto fails");
            Add(list, "logistics.material_staging", "Material staging", "Stages the exact deficit for one authorized construction objective.", CABehaviorDomain.LogisticsAndProvision, CABehaviorForm.PreventiveReadiness, Player | Spatial, CASettingKey.AutonomousHomePlanning, CAInitiativeTier.Proactive, false, true, true, false, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.Discipline | CADispositionAxis.Initiative, PlayerOrigins, "delegated spatial objective", "HomePrerequisiteModule", "native storage and hauling", "600-tick planning pass", "one material-deficit episode", "exact deficit reaches the objective", "objective, stock, storage, authority, or veto fails");
            Add(list, "logistics.material_source", "Material source", "Produces one supported material deficit through bounded native work.", CABehaviorDomain.LogisticsAndProvision, CABehaviorForm.PersistentObjective, Player | Spatial, CASettingKey.AutonomousHomePlanning, CAInitiativeTier.Proactive, false, true, true, false, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.Discipline | CADispositionAxis.Initiative, PlayerOrigins, "delegated source policy", "HomePrerequisiteModule", "native plant cutting and hauling", "600-tick planning pass", "one supported producer objective", "the exact deficit is produced", "producer unsupported, threat, area, ideology, cancellation, or reserve blocks it");
            Add(list, "spatial.home_essentials", "Home essentials", "Adds one sleeping or eating essential inside valid claimed shelter.", CABehaviorDomain.DomesticAndSpatial, CABehaviorForm.AdaptivePlanning, Player | Spatial, CASettingKey.AutonomousHomePlanning, CAInitiativeTier.Proactive, false, true, true, false, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.Initiative | CADispositionAxis.Discipline, PlayerOrigins, "delegated player home and valid authored program", "AutonomousHomeModule", "native blueprint", "600-tick home pass", "one essential deficiency", "the essential exists", "space, material, capability, cancellation, demolition veto, or authority fails");
            Add(list, "spatial.home_comfort", "Home comfort", "Adds one modest contextual furnishing after essentials are covered.", CABehaviorDomain.DomesticAndSpatial, CABehaviorForm.AdaptivePlanning, Player | Spatial, CASettingKey.AutonomousHomePlanning, CAInitiativeTier.Autonomous, false, true, true, false, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.Initiative, PlayerOrigins, "delegated player home and valid authored program", "AutonomousHomeModule", "native blueprint", "600-tick home pass", "one contextual deficiency", "the selected provision exists", "essentials, space, material, capability, veto, or authority fails");
            Add(list, "spatial.resident_roster_negotiation", "Resident roster choice", "Lets one resident choose a compatible authored sleeping program from current bed and relationship facts.", CABehaviorDomain.DomesticAndSpatial, CABehaviorForm.AdaptivePlanning, Player | Spatial, CASettingKey.AutonomousHomePlanning, CAInitiativeTier.Autonomous, false, true, true, false, CAKnowledgeRequirement.CurrentFact, false, true, 0, CADispositionAxis.Initiative | CADispositionAxis.Conformity, PlayerOrigins, "the resident's own choice inside a player-authored sleeping program", "PlannedUseMapComponent", "saved space-program roster and native bed ownership", "600-tick home pass", "one resident roster episode", "the resident has a compatible sleeping assignment", "program, relationship, capacity, player ownership, or authority changes");
            Add(list, "spatial.program_furnishing", "Program furnishing", "Furnishes one functional or contextual need inside an authored space program.", CABehaviorDomain.DomesticAndSpatial, CABehaviorForm.AdaptivePlanning, Player | Spatial, CASettingKey.AutonomousHomePlanning, CAInitiativeTier.Proactive, false, true, true, false, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.Initiative | CADispositionAxis.Discipline, PlayerOrigins, "authored space program and effective initiative ceiling", "SpatialFurnishingModule", "B5 asset registry and native blueprint", "600-tick spatial pass", "one program demand", "the demand is satisfied", "program, cells, residents, requirement, material, ceiling, or veto fails");
            Add(list, "spatial.npc_settlement_development", "NPC settlement development", "Consumes shared settlement demands through an explicit simulated institution.", CABehaviorDomain.InstitutionalDevelopment, CABehaviorForm.InstitutionalAction, Npc | NpcInstitution, CASettingKey.None, CAInitiativeTier.Standard, true, false, false, true, CAKnowledgeRequirement.MaterialDeficit, false, false, 0, CADispositionAxis.None, NpcOrigins, "named household, operator, organization, guild, religious body, military, or government", "SettlementPlanningContextModule", "B5 demand, asset, siting, and material pipeline", "world or settlement simulation cadence", "institutional proposal and commitment", "the demand is satisfied", "approver, owner, labor, funding, material, or siting fails");
            Add(list, "spatial.creation_authoring", "Starting-region authoring", "Writes confirmed creation state directly as simulation history.", CABehaviorDomain.DomesticAndSpatial, CABehaviorForm.DirectOrder, CAActorContext.CreationAuthor, CASettingKey.None, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, CAAuthorityOrigin.WorldAuthoring, "creation author confirmation", "RegionalSetupModule", "B5 authoring and materialization pipeline", "creation flow", "confirmed initial state", "the confirmed state materializes", "authoring validation blocks confirmation");
            Add(list, "institution.frontier_household_activity", "Frontier household activity", "Carries one bounded household movement under the realized frontier holding.", CABehaviorDomain.InstitutionalDevelopment, CABehaviorForm.InstitutionalAction, Npc | NpcInstitution, CASettingKey.None, CAInitiativeTier.Standard, true, true, false, true, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, CAAuthorityOrigin.Household | CAAuthorityOrigin.Continuation | CAAuthorityOrigin.SaveRestore, "realized frontier household and current household role", "FrontierModule", "native movement job", "bounded frontier household cadence", "one household episode", "the household member reaches the current site objective", "the holding, route, household, or role becomes invalid");
            Add(list, "survival.frontier_flight", "Frontier flight", "Moves a frontier resident toward a reachable edge under the current household alarm without granting hostile identity.", CABehaviorDomain.SurvivalAndImmediateSafety, CABehaviorForm.Safeguard, Npc, CASettingKey.None, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.Courage | CADispositionAxis.Discipline, CAAuthorityOrigin.Household | CAAuthorityOrigin.NativeDuty, "current frontier household alarm and actor-local survival", "FrontierModule", "native movement job", "frontier threat response", "one flight episode", "the resident clears the holding", "the alarm, route, or holding becomes invalid");

            // Presentation and diagnostics never authorize simulation.
            Add(list, "presentation.arms", "Arm rendering", "Renders arms without changing simulation state.", CABehaviorDomain.Presentation, CABehaviorForm.Presentation, Player | Npc, CASettingKey.RenderArms, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, PlayerOrigins | NpcOrigins, "presentation setting", "ArmsModule", "rendering", "draw pass", "presentation only", "the frame is drawn", "presentation is disabled", exposeInUi: false);
            Add(list, "presentation.information_detail", "Information detail", "Changes explanation density without changing simulation state.", CABehaviorDomain.Presentation, CABehaviorForm.Presentation, CAActorContext.PlayerPawn | Colony | Spatial | NpcInstitution | CAActorContext.CreationAuthor, CASettingKey.InformationDetail, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, CAAuthorityOrigin.OperatorDirect, "presentation setting", "AuthoringPresentationModule", "UI formatting", "on draw", "presentation only", "the requested explanation is shown", "the surface closes", exposeInUi: false);
            Add(list, "diagnostic.behavior_trace", "Behavior trace", "Records meaningful behavior transitions without authorizing them.", CABehaviorDomain.Diagnostics, CABehaviorForm.Diagnostic, Player | Npc | Colony | NpcInstitution, CASettingKey.TraceBehavior, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, PlayerOrigins | NpcOrigins, "diagnostic setting", "TraceModule", "diagnostic log", "meaningful transition only", "diagnostic only", "the receipt is written", "tracing is disabled", exposeInUi: false);
            Add(list, "diagnostic.behavior_census", "Behavior census", "Reads current behavior state without changing it.", CABehaviorDomain.Diagnostics, CABehaviorForm.Diagnostic, Player | Npc | Colony | Spatial | NpcInstitution, CASettingKey.None, CAInitiativeTier.Standard, true, false, false, false, CAKnowledgeRequirement.None, false, false, 0, CADispositionAxis.None, PlayerOrigins | NpcOrigins, "developer request", "DevTestModule", "read-only census", "on request", "diagnostic only", "the census is emitted", "no valid subject is selected", exposeInUi: false);

            Validate(list);
            for (int i = 0; i < list.Count; i++) list[i].Index = i;
            All = list.ToArray();
            var keyMap = new Dictionary<string, CABehaviorDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < All.Length; i++) keyMap.Add(All[i].Key, All[i]);
            ByKey = keyMap;
            byDomain = All.GroupBy(d => d.Domain).ToDictionary(g => g.Key, g => g.ToArray());
            bySetting = All.GroupBy(d => d.Setting).ToDictionary(g => g.Key, g => g.ToArray());
        }

        private static void Validate(List<CABehaviorDefinition> list)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                CABehaviorDefinition definition = list[i];
                if (definition == null
                    || string.IsNullOrWhiteSpace(definition.Key)
                    || !keys.Add(definition.Key))
                    throw new InvalidOperationException(
                        "Behavior catalog contains a missing or duplicate key.");
                if (string.IsNullOrWhiteSpace(definition.ExecutorOwner))
                    throw new InvalidOperationException(definition.Key
                        + " has no executor owner.");
                if (definition.Setting == CASettingKey.None
                    && !definition.AlwaysAvailable)
                    throw new InvalidOperationException(definition.Key
                        + " has no permission owner.");
                if (definition.MinimumInitiative
                        == CAInitiativeTier.Autonomous
                    && definition.InitiativeIndependent
                    && !definition.InstitutionalAuthority)
                    throw new InvalidOperationException(definition.Key
                        + " has no initiative or institutional authority.");
                if (string.IsNullOrWhiteSpace(definition.Cadence)
                    || string.IsNullOrWhiteSpace(definition.CompletionCondition)
                        && string.IsNullOrWhiteSpace(
                            definition.StandDownCondition))
                    throw new InvalidOperationException(definition.Key
                        + " has no cadence or termination contract.");
                if (definition.ExposeInUi
                    && (string.IsNullOrWhiteSpace(definition.CompactText)
                        || string.IsNullOrWhiteSpace(definition.StandardText)
                        || string.IsNullOrWhiteSpace(
                            definition.ExpandedText)))
                    throw new InvalidOperationException(definition.Key
                        + " has incomplete presentation text.");
            }
        }

        private static void Add(List<CABehaviorDefinition> list, string key,
            string label, string compact, CABehaviorDomain domain,
            CABehaviorForm form, CAActorContext actors, CASettingKey setting,
            CAInitiativeTier minimum, bool independent, bool continuation,
            bool ceiling, bool institutional, CAKnowledgeRequirement knowledge,
            bool relayed, bool liveValidation, int maxAge,
            CADispositionAxis axes, CAAuthorityOrigin origins,
            string authority, string owner, string executor, string cadence,
            string intentOwner, string completion, string standDown,
            string expanded = null, CABehaviorDomain[] relatedDomains = null,
            CABehaviorForm[] relatedForms = null, bool exposeInUi = true,
            float minimumKnowledgeConfidence = -1f,
            bool uncertaintyAffectsRanking = false)
        {
            list.Add(new CABehaviorDefinition(key, label, compact, domain, form,
                actors, setting, minimum, independent, continuation, ceiling,
                institutional, knowledge, relayed, liveValidation, maxAge, axes,
                origins, authority, owner, executor, cadence, intentOwner,
                completion, standDown, expanded, relatedDomains, relatedForms,
                exposeInUi, minimumKnowledgeConfidence,
                uncertaintyAffectsRanking));
        }

        private static void Direct(List<CABehaviorDefinition> list, string key,
            string label, string compact, CASettingKey setting, string owner,
            string completion)
        {
            Add(list, key, label, compact, CABehaviorDomain.TacticalCoordination,
                CABehaviorForm.DirectOrder, Player | Spatial, setting,
                CAInitiativeTier.Standard, true, true, false, false,
                CAKnowledgeRequirement.None, false, false, 0,
                CADispositionAxis.None,
                CAAuthorityOrigin.OperatorDirect | CAAuthorityOrigin.OperatorRelay
                    | CAAuthorityOrigin.Continuation | CAAuthorityOrigin.SaveRestore,
                "direct operator or delivered and accepted relay", owner,
                "native job, duty, designation, or program", "on command",
                "operator-owned episode", completion,
                "the operator releases it or execution becomes impossible");
        }

        private static void Support(List<CABehaviorDefinition> list, string key,
            string label, string compact, CASettingKey setting)
        {
            Add(list, key, label, compact, CABehaviorDomain.TacticalCoordination,
                CABehaviorForm.AdaptivePlanning, Player | NpcInstitution, setting,
                CAInitiativeTier.Autonomous, false, true, false, true,
                CAKnowledgeRequirement.FreshFact, true, false, 600,
                CADispositionAxis.Courage | CADispositionAxis.Discipline
                    | CADispositionAxis.Initiative, PlayerOrigins | NpcOrigins,
                "squad, command, player delegation, or institutional defense",
                "SquadSupportMapComponent", "native tactical duty",
                "90-tick map pass", "one supervised tasking",
                "the assigned post or objective is covered",
                "contact, post, capability, authority, or tasking lifetime fails",
                minimumKnowledgeConfidence: 0.55f,
                uncertaintyAffectsRanking: true);
        }

        public static CABehaviorDefinition Get(string key)
        {
            CABehaviorDefinition definition;
            return key != null && ByKey.TryGetValue(key, out definition)
                ? definition : null;
        }

        public static CABehaviorDefinition[] ForDomain(CABehaviorDomain domain)
        {
            CABehaviorDefinition[] definitions;
            return byDomain.TryGetValue(domain, out definitions)
                ? definitions : Array.Empty<CABehaviorDefinition>();
        }

        public static CABehaviorDefinition[] ForSetting(CASettingKey setting)
        {
            CABehaviorDefinition[] definitions;
            return bySetting.TryGetValue(setting, out definitions)
                ? definitions : Array.Empty<CABehaviorDefinition>();
        }

        public static CABehaviorDefinition[] ForTier(CAInitiativeTier tier)
        {
            return All.Where(d => d.InitiativeIndependent
                || d.MinimumInitiative <= tier).ToArray();
        }

        public static string KeyForController(CAIntentController controller,
            CAIntentOrigin origin)
        {
            switch (controller)
            {
                case CAIntentController.Ambush: return "order.ambush";
                case CAIntentController.Hide: return "survival.shelter";
                case CAIntentController.Hold: return "order.hold";
                case CAIntentController.Formation: return "order.formation";
                case CAIntentController.Withdrawal:
                    return origin == CAIntentOrigin.OperatorDirect
                        || origin == CAIntentOrigin.OperatorRelay
                        ? "order.withdrawal" : "combat.withdrawal_self_preservation";
                case CAIntentController.SelfPreservation:
                    return "combat.withdrawal_self_preservation";
                case CAIntentController.FirePosition:
                    return "combat.fire_position_adjustment";
                case CAIntentController.ExplosiveEvasion:
                    return "survival.immediate_evasion";
                case CAIntentController.RaidDefense:
                    return "combat.local_reaction";
                case CAIntentController.FoodSafety:
                    return "survival.food_choice";
                case CAIntentController.Drill: return "support.battle_drill";
                case CAIntentController.Shelter: return "survival.shelter";
                case CAIntentController.CombatRecovery:
                    return "survival.combat_recovery";
                case CAIntentController.DraftCoordination:
                    return "communication.command_delivery";
                case CAIntentController.AcousticInvestigation:
                    return "knowledge.acoustic_investigation";
                case CAIntentController.Welfare:
                    return "welfare.local_rescue";
                default: return "diagnostic.behavior_trace";
            }
        }
    }

    public enum CABehaviorBlockReason : byte
    {
        None = 0,
        UnknownBehavior,
        ActorContext,
        Permission,
        Initiative,
        AuthorityCeiling,
        Authority,
        Knowledge,
        KnowledgeConfidence,
        KnowledgeStale,
        LiveValidation,
        PlayerOwnership,
        Capability,
        Material,
        CurrentIntent,
        NonOriginating
    }

    public readonly struct CABehaviorContext
    {
        public readonly Pawn Actor;
        public readonly CAActorContext ActorContext;
        public readonly CAInitiativeTier Initiative;
        public readonly CAInitiativeTier AuthorityCeiling;
        public readonly CAAuthorityOrigin AuthorityOrigin;
        public readonly bool AuthoritySatisfied;
        public readonly bool KnowledgeSatisfied;
        public readonly bool KnowledgeFresh;
        public readonly bool KnowledgeRelayed;
        public readonly int KnowledgeAgeTicks;
        public readonly float KnowledgeConfidence;
        public readonly float KnowledgeUncertainty;
        public readonly bool LiveValidated;
        public readonly bool CapabilitySatisfied;
        public readonly bool MaterialSatisfied;
        public readonly bool CurrentIntentCompatible;
        public readonly bool DirectPlayerOwnership;
        public readonly string AuthorityBasis;
        public readonly string KnowledgeBasis;
        public readonly string Owner;

        public CABehaviorContext(Pawn actor, CAActorContext actorContext,
            CAInitiativeTier initiative, CAAuthorityOrigin authorityOrigin,
            bool authoritySatisfied = true, bool knowledgeSatisfied = true,
            bool knowledgeFresh = true, bool liveValidated = true,
            bool knowledgeRelayed = false, int knowledgeAgeTicks = 0,
            float knowledgeConfidence = 1f, float knowledgeUncertainty = 0f,
            bool capabilitySatisfied = true, bool materialSatisfied = true,
            bool currentIntentCompatible = true,
            bool directPlayerOwnership = false,
            CAInitiativeTier authorityCeiling = CAInitiativeTier.Autonomous,
            string authorityBasis = null, string knowledgeBasis = null,
            string owner = null)
        {
            Actor = actor;
            ActorContext = actorContext;
            Initiative = initiative;
            AuthorityOrigin = authorityOrigin;
            AuthoritySatisfied = authoritySatisfied;
            KnowledgeSatisfied = knowledgeSatisfied;
            KnowledgeFresh = knowledgeFresh;
            KnowledgeRelayed = knowledgeRelayed;
            KnowledgeAgeTicks = Math.Max(0, knowledgeAgeTicks);
            KnowledgeConfidence = Mathf.Clamp01(knowledgeConfidence);
            KnowledgeUncertainty = Math.Max(0f, knowledgeUncertainty);
            LiveValidated = liveValidated;
            CapabilitySatisfied = capabilitySatisfied;
            MaterialSatisfied = materialSatisfied;
            CurrentIntentCompatible = currentIntentCompatible;
            DirectPlayerOwnership = directPlayerOwnership;
            AuthorityCeiling = authorityCeiling;
            AuthorityBasis = authorityBasis;
            KnowledgeBasis = knowledgeBasis;
            Owner = owner;
        }

        public static CABehaviorContext ForPawn(Pawn pawn,
            CAAuthorityOrigin origin, bool authoritySatisfied,
            bool knowledgeSatisfied = true,
            bool knowledgeFresh = true, bool liveValidated = true,
            bool knowledgeRelayed = false, int knowledgeAgeTicks = 0,
            float knowledgeConfidence = 1f, float knowledgeUncertainty = 0f,
            bool capabilitySatisfied = true, bool materialSatisfied = true,
            bool currentIntentCompatible = true,
            bool directPlayerOwnership = false,
            CAInitiativeTier authorityCeiling = CAInitiativeTier.Autonomous,
            string authorityBasis = null, string knowledgeBasis = null,
            string owner = null)
        {
            CAActorContext context = pawn != null && pawn.Faction == Faction.OfPlayer
                ? CAActorContext.PlayerPawn : CAActorContext.NPCPawn;
            CAInitiativeTier tier = context == CAActorContext.PlayerPawn
                ? AutonomyComponent.TierOf(pawn) : CAInitiativeTier.Autonomous;
            return new CABehaviorContext(pawn, context, tier, origin,
                authoritySatisfied: authoritySatisfied,
                knowledgeSatisfied: knowledgeSatisfied,
                knowledgeFresh: knowledgeFresh, liveValidated: liveValidated,
                knowledgeRelayed: knowledgeRelayed,
                knowledgeAgeTicks: knowledgeAgeTicks,
                knowledgeConfidence: knowledgeConfidence,
                knowledgeUncertainty: knowledgeUncertainty,
                capabilitySatisfied: capabilitySatisfied,
                materialSatisfied: materialSatisfied,
                currentIntentCompatible: currentIntentCompatible,
                directPlayerOwnership: directPlayerOwnership,
                authorityCeiling: authorityCeiling,
                authorityBasis: authorityBasis, knowledgeBasis: knowledgeBasis,
                owner: owner);
        }
    }

    public readonly struct CABehaviorDecision
    {
        public readonly bool Allowed;
        public readonly string BehaviorKey;
        public readonly CAInitiativeTier CurrentTier;
        public readonly CAInitiativeTier RequiredTier;
        public readonly bool FeatureEnabled;
        public readonly bool KnowledgeSatisfied;
        public readonly bool AuthoritySatisfied;
        public readonly bool CapabilitySatisfied;
        public readonly bool MaterialSatisfied;
        public readonly bool CurrentIntentCompatible;
        public readonly bool DirectPlayerOwnership;
        public readonly CABehaviorBlockReason PrimaryBlock;
        public readonly string AuthorityBasis;
        public readonly string KnowledgeBasis;
        public readonly int KnowledgeAgeTicks;
        public readonly float KnowledgeConfidence;
        public readonly float KnowledgeUncertainty;
        public readonly string Owner;
        public readonly CAIntentOrigin SuggestedIntentOrigin;

        internal CABehaviorDecision(bool allowed, string key,
            CAInitiativeTier current, CAInitiativeTier required,
            bool feature, bool knowledge, bool authority, bool capability,
            bool material, bool intent, bool direct,
            CABehaviorBlockReason primaryBlock, string authorityBasis,
            string knowledgeBasis, int knowledgeAgeTicks,
            float knowledgeConfidence, float knowledgeUncertainty, string owner,
            CAIntentOrigin origin)
        {
            Allowed = allowed;
            BehaviorKey = key;
            CurrentTier = current;
            RequiredTier = required;
            FeatureEnabled = feature;
            KnowledgeSatisfied = knowledge;
            AuthoritySatisfied = authority;
            CapabilitySatisfied = capability;
            MaterialSatisfied = material;
            CurrentIntentCompatible = intent;
            DirectPlayerOwnership = direct;
            PrimaryBlock = primaryBlock;
            AuthorityBasis = authorityBasis;
            KnowledgeBasis = knowledgeBasis;
            KnowledgeAgeTicks = knowledgeAgeTicks;
            KnowledgeConfidence = knowledgeConfidence;
            KnowledgeUncertainty = knowledgeUncertainty;
            Owner = owner;
            SuggestedIntentOrigin = origin;
        }

        public string PrimaryReason
        {
            get { return ReasonFor(PrimaryBlock); }
        }

        internal static string ReasonFor(CABehaviorBlockReason block)
        {
            switch (block)
            {
                case CABehaviorBlockReason.None: return "allowed";
                case CABehaviorBlockReason.UnknownBehavior: return "behavior is not registered";
                case CABehaviorBlockReason.ActorContext: return "behavior does not apply to this actor";
                case CABehaviorBlockReason.Permission: return "feature permission is disabled";
                case CABehaviorBlockReason.Initiative: return "initiative tier is too low";
                case CABehaviorBlockReason.AuthorityCeiling: return "owning authority ceiling is too low";
                case CABehaviorBlockReason.Authority: return "required authority is absent";
                case CABehaviorBlockReason.Knowledge: return "required knowledge is absent";
                case CABehaviorBlockReason.KnowledgeConfidence: return "knowledge confidence is below the behavior threshold";
                case CABehaviorBlockReason.KnowledgeStale: return "knowledge is stale";
                case CABehaviorBlockReason.LiveValidation: return "current target validation failed";
                case CABehaviorBlockReason.PlayerOwnership: return "direct player work owns the pawn";
                case CABehaviorBlockReason.Capability: return "execution capability is unavailable";
                case CABehaviorBlockReason.Material: return "material execution is impossible";
                case CABehaviorBlockReason.CurrentIntent: return "current intent is incompatible";
                case CABehaviorBlockReason.NonOriginating: return "catalog entry does not originate action";
                default: return "blocked";
            }
        }

        public string[] SupportingReasons()
        {
            var reasons = new List<string>();
            if (!FeatureEnabled) reasons.Add("permission disabled");
            if (!KnowledgeSatisfied) reasons.Add("knowledge missing or invalid");
            if (!AuthoritySatisfied) reasons.Add("authority missing");
            if (!CapabilitySatisfied) reasons.Add("capability missing");
            if (!MaterialSatisfied) reasons.Add("material condition failed");
            if (!CurrentIntentCompatible) reasons.Add("current intent incompatible");
            return reasons.ToArray();
        }
    }

    public static class CABehaviorGate
    {
        // Fast stable prefilter for hot paths. Dynamic authority, knowledge,
        // ownership, capability, and material facts must still be checked at
        // origination through Evaluate; this method only replaces duplicated
        // setting/tier/role interpretation.
        public static bool StableProfileAllows(Pawn pawn,
            string behaviorKey)
        {
            return CAEffectiveBehaviorProfileCache.Of(pawn)
                ?.Includes(behaviorKey) == true;
        }

        public static CABehaviorDecision Evaluate(string behaviorKey,
            CABehaviorContext context)
        {
            CABehaviorDefinition definition = CABehaviorCatalog.Get(behaviorKey);
            if (definition == null)
                return Decision(false, behaviorKey, context,
                    CAInitiativeTier.Standard, false,
                    CABehaviorBlockReason.UnknownBehavior);
            return Evaluate(definition, context);
        }

        public static CABehaviorDecision Evaluate(
            CABehaviorDefinition definition, CABehaviorContext context)
        {
            return EvaluateCore(definition, context,
                permitNativeExecution: false);
        }

        // Native augmentation and execution-capability entries may validate an
        // already selected native action, but they never gain authority to
        // originate discretionary work. Callers must opt into this seam.
        public static CABehaviorDecision EvaluateNativeExecution(
            string behaviorKey, CABehaviorContext context)
        {
            CABehaviorDefinition definition = CABehaviorCatalog.Get(behaviorKey);
            if (definition == null)
                return Decision(false, behaviorKey, context,
                    CAInitiativeTier.Standard, false,
                    CABehaviorBlockReason.UnknownBehavior);
            if (definition.Form != CABehaviorForm.NativeAugmentation
                && definition.Form != CABehaviorForm.ExecutionCapability)
                return Evaluate(definition, context);
            return EvaluateCore(definition, context,
                permitNativeExecution: true);
        }

        private static CABehaviorDecision EvaluateCore(
            CABehaviorDefinition definition, CABehaviorContext context,
            bool permitNativeExecution)
        {
            bool feature = CABehaviorSettings.IsEnabled(definition,
                AwarenessMod.Settings);
            if (!definition.AuthorizesOrigination
                && definition.Form != CABehaviorForm.DirectOrder
                && !permitNativeExecution)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, feature,
                    CABehaviorBlockReason.NonOriginating);
            if (!definition.AppliesTo(context.ActorContext))
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, feature,
                    CABehaviorBlockReason.ActorContext);
            if (!feature)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, false,
                    CABehaviorBlockReason.Permission);

            bool direct = context.AuthorityOrigin
                == CAAuthorityOrigin.OperatorDirect;
            bool continuation = definition.StandardMayContinue
                && (context.AuthorityOrigin == CAAuthorityOrigin.Continuation
                    || context.AuthorityOrigin == CAAuthorityOrigin.SaveRestore);
            bool initiativeApplies = !definition.InitiativeIndependent
                && (context.ActorContext & (CAActorContext.PlayerPawn
                    | CAActorContext.PlayerSpatialAuthority)) != 0;
            if (initiativeApplies && !direct && !continuation
                && context.Initiative < definition.MinimumInitiative)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.Initiative);
            if (definition.AuthorityCeilingApplies && !direct
                && context.AuthorityCeiling < definition.MinimumInitiative)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.AuthorityCeiling);
            if (!context.AuthoritySatisfied
                || (definition.AllowedAuthorityOrigins & context.AuthorityOrigin) == 0)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.Authority);
            bool institutionalContext = (context.ActorContext
                & (CAActorContext.NPCSettlement
                    | CAActorContext.NPCInstitution)) != 0;
            CAAuthorityOrigin institutionalOrigins =
                CAAuthorityOrigin.Institutional
                | CAAuthorityOrigin.Household
                | CAAuthorityOrigin.Organization
                | CAAuthorityOrigin.WorldAuthoring;
            if (definition.InstitutionalAuthority && institutionalContext
                && (context.AuthorityOrigin & institutionalOrigins) == 0)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.Authority);
            if (string.IsNullOrWhiteSpace(context.AuthorityBasis)
                && !string.IsNullOrWhiteSpace(definition.AuthorityRequirement))
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.Authority);
            if (definition.KnowledgeRequirement != CAKnowledgeRequirement.None
                && !context.KnowledgeSatisfied)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.Knowledge);
            if (definition.KnowledgeRequirement != CAKnowledgeRequirement.None
                && context.KnowledgeConfidence
                    < definition.MinimumKnowledgeConfidence)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.KnowledgeConfidence);
            if (definition.KnowledgeRequirement == CAKnowledgeRequirement.FreshFact
                && !context.KnowledgeFresh)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.KnowledgeStale);
            if (context.KnowledgeRelayed && !definition.AcceptsRelayedKnowledge)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.Knowledge);
            if (definition.MaximumKnowledgeAgeTicks > 0
                && context.KnowledgeAgeTicks
                    > definition.MaximumKnowledgeAgeTicks)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.KnowledgeStale);
            if (definition.LiveValidationRequired && !context.LiveValidated)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.LiveValidation);
            if (context.DirectPlayerOwnership && !direct)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.PlayerOwnership);
            if (!context.CapabilitySatisfied)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.Capability);
            if (!context.MaterialSatisfied)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.Material);
            if (!context.CurrentIntentCompatible)
                return Decision(false, definition.Key, context,
                    definition.MinimumInitiative, true,
                    CABehaviorBlockReason.CurrentIntent);
            return Decision(true, definition.Key, context,
                definition.MinimumInitiative, true, CABehaviorBlockReason.None);
        }

        private static CABehaviorDecision Decision(bool allowed, string key,
            CABehaviorContext context, CAInitiativeTier required, bool feature,
            CABehaviorBlockReason block)
        {
            var decision = new CABehaviorDecision(allowed, key,
                context.Initiative,
                required, feature, context.KnowledgeSatisfied,
                context.AuthoritySatisfied, context.CapabilitySatisfied,
                context.MaterialSatisfied, context.CurrentIntentCompatible,
                context.DirectPlayerOwnership, block, context.AuthorityBasis,
                context.KnowledgeBasis, context.KnowledgeAgeTicks,
                context.KnowledgeConfidence, context.KnowledgeUncertainty,
                context.Owner,
                SuggestedOrigin(context.AuthorityOrigin));
            Pawn pawn = context.Actor;
            if (pawn?.Map != null)
                CABehaviorIntentMapComponent.For(pawn.Map)?.ObserveDecision(
                    pawn, decision);
            return decision;
        }

        private static CAIntentOrigin SuggestedOrigin(CAAuthorityOrigin origin)
        {
            if ((origin & CAAuthorityOrigin.OperatorDirect) != 0)
                return CAIntentOrigin.OperatorDirect;
            if ((origin & CAAuthorityOrigin.OperatorRelay) != 0)
                return CAIntentOrigin.OperatorRelay;
            if ((origin & CAAuthorityOrigin.PlayerDelegated) != 0)
                return CAIntentOrigin.PlayerDelegated;
            if ((origin & CAAuthorityOrigin.NativeDuty) != 0)
                return CAIntentOrigin.NativeDuty;
            if ((origin & CAAuthorityOrigin.PeerRequest) != 0)
                return CAIntentOrigin.PeerRelay;
            if ((origin & CAAuthorityOrigin.AutomaticDefense) != 0)
                return CAIntentOrigin.AutomaticDefense;
            if ((origin & CAAuthorityOrigin.Institutional) != 0)
                return CAIntentOrigin.Institutional;
            if ((origin & CAAuthorityOrigin.Household) != 0)
                return CAIntentOrigin.Household;
            if ((origin & CAAuthorityOrigin.Organization) != 0)
                return CAIntentOrigin.Organization;
            if ((origin & CAAuthorityOrigin.Continuation) != 0)
                return CAIntentOrigin.Continuation;
            if ((origin & CAAuthorityOrigin.SaveRestore) != 0)
                return CAIntentOrigin.SaveRestore;
            if ((origin & CAAuthorityOrigin.WorldAuthoring) != 0)
                return CAIntentOrigin.WorldAuthoring;
            return CAIntentOrigin.Unknown;
        }
    }

    public static class CABehaviorRevisions
    {
        public static int Settings { get; private set; }
        public static int Autonomy { get; private set; }
        public static int Roles { get; private set; }
        public static int SpatialAuthority { get; private set; }

        public static void SettingsChanged()
        {
            Settings++;
            CAEffectiveBehaviorProfileCache.Clear();
        }

        public static void AutonomyChanged(Pawn pawn)
        {
            Autonomy++;
            CAEffectiveBehaviorProfileCache.Invalidate(pawn);
        }

        public static void RoleChanged(Pawn pawn = null)
        {
            Roles++;
            if (pawn == null) CAEffectiveBehaviorProfileCache.Clear();
            else CAEffectiveBehaviorProfileCache.Invalidate(pawn);
        }

        public static void SpatialAuthorityChanged(Map map = null)
        {
            SpatialAuthority++;
            CAEffectiveBehaviorProfileCache.ClearMap(map);
        }
    }

    public sealed class CAEffectiveBehaviorProfile
    {
        private readonly ulong[] permitted;
        public readonly CAInitiativeTier Initiative;
        public readonly CAActorContext ActorContext;
        public readonly int SettingsRevision;
        public readonly int AutonomyRevision;
        public readonly int RoleRevision;
        public readonly int SpatialAuthorityRevision;
        public readonly int StableSignature;
        public readonly string Role;

        internal CAEffectiveBehaviorProfile(Pawn pawn, int stableSignature)
        {
            Initiative = AutonomyComponent.TierOf(pawn);
            ActorContext = pawn != null && pawn.Faction == Faction.OfPlayer
                ? CAActorContext.PlayerPawn : CAActorContext.NPCPawn;
            SettingsRevision = CABehaviorRevisions.Settings;
            AutonomyRevision = CABehaviorRevisions.Autonomy;
            RoleRevision = CABehaviorRevisions.Roles;
            SpatialAuthorityRevision = CABehaviorRevisions.SpatialAuthority;
            StableSignature = stableSignature;
            Role = SquadComponent.IsLeader(pawn) ? "squad leader"
                : SquadComponent.IsFireteamLeader(pawn) ? "fire-team leader"
                : SquadComponent.SquadOf(pawn) > 0 ? "squad member" : "none";
            permitted = new ulong[(CABehaviorCatalog.All.Length + 63) / 64];
            for (int i = 0; i < CABehaviorCatalog.All.Length; i++)
            {
                CABehaviorDefinition definition = CABehaviorCatalog.All[i];
                if (!definition.AuthorizesOrigination) continue;
                if (!definition.AppliesTo(ActorContext)) continue;
                if (!CABehaviorSettings.IsEnabled(definition,
                    AwarenessMod.Settings)) continue;
                if (!definition.InitiativeIndependent
                    && ActorContext == CAActorContext.PlayerPawn
                    && Initiative < definition.MinimumInitiative) continue;
                if (!PermanentCapabilityAllows(pawn, definition)) continue;
                permitted[i >> 6] |= 1UL << (i & 63);
            }
        }

        public bool Includes(string behaviorKey)
        {
            CABehaviorDefinition definition = CABehaviorCatalog.Get(behaviorKey);
            return definition != null && Includes(definition);
        }

        public bool Includes(CABehaviorDefinition definition)
        {
            int index = definition.Index;
            return index >= 0 && index < CABehaviorCatalog.All.Length
                && (permitted[index >> 6] & (1UL << (index & 63))) != 0;
        }

        public IEnumerable<CABehaviorDefinition> Definitions()
        {
            for (int i = 0; i < CABehaviorCatalog.All.Length; i++)
                if ((permitted[i >> 6] & (1UL << (i & 63))) != 0)
                    yield return CABehaviorCatalog.All[i];
        }

        private static bool PermanentCapabilityAllows(Pawn pawn,
            CABehaviorDefinition definition)
        {
            if (pawn == null) return false;
            switch (definition.Domain)
            {
                case CABehaviorDomain.WelfareAndCare:
                    return !pawn.WorkTagIsDisabled(WorkTags.Caring);
                case CABehaviorDomain.AnimalCare:
                    return !pawn.WorkTagIsDisabled(WorkTags.Animals);
                case CABehaviorDomain.CombatSelfPreservation:
                case CABehaviorDomain.TacticalCoordination:
                    return definition.Form == CABehaviorForm.DirectOrder
                        || !pawn.WorkTagIsDisabled(WorkTags.Violent);
                default:
                    return true;
            }
        }
    }

    public static class CAEffectiveBehaviorProfileCache
    {
        private sealed class Entry
        {
            public CAEffectiveBehaviorProfile Profile;
            public int MapId;
        }

        private static readonly Dictionary<int, Entry> cache =
            new Dictionary<int, Entry>();

        public static CAEffectiveBehaviorProfile Of(Pawn pawn)
        {
            if (pawn == null) return null;
            int signature = StableSignature(pawn);
            Entry entry;
            if (cache.TryGetValue(pawn.thingIDNumber, out entry)
                && entry.Profile.SettingsRevision == CABehaviorRevisions.Settings
                && entry.Profile.AutonomyRevision == CABehaviorRevisions.Autonomy
                && entry.Profile.RoleRevision == CABehaviorRevisions.Roles
                && entry.Profile.SpatialAuthorityRevision
                    == CABehaviorRevisions.SpatialAuthority
                && entry.Profile.StableSignature == signature)
                return entry.Profile;
            var profile = new CAEffectiveBehaviorProfile(pawn, signature);
            cache[pawn.thingIDNumber] = new Entry
            {
                Profile = profile,
                MapId = pawn.Map != null ? pawn.Map.uniqueID : -1
            };
            return profile;
        }

        public static void Invalidate(Pawn pawn)
        {
            if (pawn != null) cache.Remove(pawn.thingIDNumber);
        }

        public static void ClearMap(Map map)
        {
            if (map == null) { Clear(); return; }
            int id = map.uniqueID;
            var remove = new List<int>();
            foreach (KeyValuePair<int, Entry> pair in cache)
                if (pair.Value.MapId == id) remove.Add(pair.Key);
            for (int i = 0; i < remove.Count; i++) cache.Remove(remove[i]);
        }

        public static void Clear()
        {
            cache.Clear();
        }

        private static int StableSignature(Pawn pawn)
        {
            unchecked
            {
                int value = pawn.Faction != null ? pawn.Faction.loadID : -1;
                value = value * 397 ^ (pawn.Map != null ? pawn.Map.uniqueID : -1);
                value = value * 397 ^ (pawn.WorkTagIsDisabled(WorkTags.Caring) ? 1 : 0);
                value = value * 397 ^ (pawn.WorkTagIsDisabled(WorkTags.Violent) ? 1 : 0);
                value = value * 397 ^ (pawn.WorkTagIsDisabled(WorkTags.Animals) ? 1 : 0);
                value = value * 397 ^ (pawn.Downed ? 1 : 0);
                return value;
            }
        }
    }

    public sealed class CASettingDefinition
    {
        public readonly CASettingKey Key;
        public readonly string Label;
        public readonly string Section;
        public readonly CABehaviorDomain Domain;
        public readonly CASettingKind Kind;
        public readonly string ImmediateEffect;
        public readonly string ActorText;

        public CASettingDefinition(CASettingKey key, string label,
            string section, CABehaviorDomain domain, CASettingKind kind,
            string immediateEffect, string actorText)
        {
            Key = key;
            Label = label;
            Section = section;
            Domain = domain;
            Kind = kind;
            ImmediateEffect = immediateEffect;
            ActorText = actorText;
        }
    }

    public static class CABehaviorSettings
    {
        public static readonly CASettingDefinition[] All =
        {
            S(CASettingKey.KnowledgeContacts, "Knowledge contacts", "Knowledge and communication", CABehaviorDomain.KnowledgeAndCommunication, CASettingKind.FeaturePermission, "Pawns record and use honestly acquired private facts.", "Player and NPC pawns"),
            S(CASettingKey.CommsSystem, "Communication routes", "Knowledge and communication", CABehaviorDomain.KnowledgeAndCommunication, CASettingKind.CommunicationInfrastructure, "Voice, radio, mental, and gesture routes can carry facts.", "Player and NPC pawns"),
            S(CASettingKey.DraftChain, "Command delivery", "Knowledge and communication", CABehaviorDomain.KnowledgeAndCommunication, CASettingKind.CommunicationInfrastructure, "Relayed tactical commands require a real delivery route.", "Player tactical groups"),
            S(CASettingKey.EatSmart, "Safer food choice", "Survival and safety", CABehaviorDomain.SurvivalAndImmediateSafety, CASettingKind.FeaturePermission, "Routine food jobs avoid needlessly poor or exposed choices.", "Player pawns"),
            S(CASettingKey.LifeSafety, "Life safety", "Survival and safety", CABehaviorDomain.SurvivalAndImmediateSafety, CASettingKind.FeaturePermission, "Known colony life-safety emergencies receive bounded rescue.", "Player pawns"),
            S(CASettingKey.SurvivalResponses, "Immediate survival", "Survival and safety", CABehaviorDomain.SurvivalAndImmediateSafety, CASettingKind.FeaturePermission, "Actor-local imminent hazards can trigger bounded self-preservation.", "Player and NPC pawns"),
            S(CASettingKey.FireResponse, "Fire response", "Survival and safety", CABehaviorDomain.HazardResponse, CASettingKind.FeaturePermission, "Known local fires can interrupt low-priority activity.", "Player pawns"),
            S(CASettingKey.TrapAwareness, "Trap awareness", "Survival and safety", CABehaviorDomain.HazardResponse, CASettingKind.FeaturePermission, "Known traps affect routing; unknown traps remain unknown.", "Player and NPC pawns"),
            S(CASettingKey.RescueOutsiders, "Outsider rescue", "Welfare and care", CABehaviorDomain.WelfareAndCare, CASettingKind.FeaturePermission, "Proactive pawns may rescue one freshly known outsider.", "Player pawns"),
            S(CASettingKey.FieldMedicine, "Field medicine", "Welfare and care", CABehaviorDomain.WelfareAndCare, CASettingKind.FeaturePermission, "Known casualties can receive bounded triage, treatment, and owned dispatch.", "Player pawns and medical authority"),
            S(CASettingKey.BuddyCarry, "Buddy carry and escort", "Welfare and care", CABehaviorDomain.WelfareAndCare, CASettingKind.FeaturePermission, "Authorized casualty movement can receive one bounded helper or escort.", "Player pawns"),
            S(CASettingKey.RaidResponse, "Raid response", "Combat and tactical delegation", CABehaviorDomain.TacticalCoordination, CASettingKind.FeaturePermission, "Known threats can drive bounded combat response and delegated defense.", "Player pawns and NPC institutions"),
            S(CASettingKey.HoldOrders, "Hold", "Combat and tactical delegation", CABehaviorDomain.TacticalCoordination, CASettingKind.FeaturePermission, "Enables the authored hold command and bounded adaptation around it.", "Player pawns"),
            S(CASettingKey.Withdrawals, "Withdrawal", "Combat and tactical delegation", CABehaviorDomain.CombatSelfPreservation, CASettingKind.FeaturePermission, "Enables direct and properly authorized self-preserving withdrawal.", "Player and NPC pawns"),
            S(CASettingKey.AmbushStrikes, "Ambush", "Combat and tactical delegation", CABehaviorDomain.TacticalCoordination, CASettingKind.FeaturePermission, "Enables synchronized authored ambushes and bounded survival adaptation.", "Player pawns"),
            S(CASettingKey.BattleDrills, "Battle drills", "Combat and tactical delegation", CABehaviorDomain.TacticalCoordination, CASettingKind.FeaturePermission, "Enables authored stack, breach, and assigned drill execution.", "Player tactical groups"),
            S(CASettingKey.DragOrders, "Drag casualty", "Combat and tactical delegation", CABehaviorDomain.TacticalCoordination, CASettingKind.FeaturePermission, "Enables the direct short casualty-drag command.", "Player pawns"),
            S(CASettingKey.MovingFire, "Moving fire", "Combat and tactical delegation", CABehaviorDomain.CombatSelfPreservation, CASettingKind.ExecutionCapability, "Allows an already authorized movement intent to fire when safe.", "Authorized combat intents"),
            S(CASettingKey.OperationalAccess, "Operational access", "Operational access and equipment", CABehaviorDomain.OperationalReadiness, CASettingKind.FeaturePermission, "Applies shared-item policy and permits personal threat equipment choices.", "Player colony and Proactive pawns"),
            S(CASettingKey.WeaponTransitions, "Weapon transitions", "Operational access and equipment", CABehaviorDomain.OperationalReadiness, CASettingKind.FeaturePermission, "A current close threat can trigger a bounded weapon-state change.", "Player pawns and NPC combat doctrine"),
            S(CASettingKey.BodyWeight, "Body weight", "Operational access and equipment", CABehaviorDomain.OperationalReadiness, CASettingKind.ExecutionCapability, "Adds body-weight effects to already authorized native actions.", "Player and NPC pawns"),
            S(CASettingKey.DualWield, "Dual wield", "Operational access and equipment", CABehaviorDomain.OperationalReadiness, CASettingKind.ExecutionCapability, "Makes off-hand execution available to an owning combat intent.", "Player and NPC pawns"),
            S(CASettingKey.AreaDuty, "Area duty", "Home, spatial planning, and logistics", CABehaviorDomain.DomesticAndSpatial, CASettingKind.FeaturePermission, "Enables direct authored area and patrol responsibility.", "Player pawns"),
            S(CASettingKey.CriticalHauling, "Critical hauling", "Home, spatial planning, and logistics", CABehaviorDomain.LogisticsAndProvision, CASettingKind.FeaturePermission, "Raises deteriorating valuables inside native hauling priority.", "Player pawns"),
            S(CASettingKey.AutoUnforbidCorpses, "Corpse access", "Home, spatial planning, and logistics", CABehaviorDomain.LogisticsAndProvision, CASettingKind.FeaturePermission, "While enabled, calm-map humanlike corpses are made accessible as colony policy.", "Player colony"),
            S(CASettingKey.AutoDigGraves, "Grave planning", "Home, spatial planning, and logistics", CABehaviorDomain.LogisticsAndProvision, CASettingKind.FeaturePermission, "Extends an existing player graveyard when unburied bodies exceed open capacity.", "Player colony"),
            S(CASettingKey.AutonomousHomePlanning, "Home and spatial planning", "Home, spatial planning, and logistics", CABehaviorDomain.DomesticAndSpatial, CASettingKind.FeaturePermission, "Delegated space can answer current material and furnishing deficits.", "Player spatial authority"),
            S(CASettingKey.AnimalCare, "Animal care", "Animals", CABehaviorDomain.AnimalCare, CASettingKind.FeaturePermission, "Known animal emergencies and bounded starvation risk receive native care.", "Player handlers and spatial authority"),
            S(CASettingKey.Gossip, "Gossip", "Social and institutional simulation", CABehaviorDomain.KnowledgeAndCommunication, CASettingKind.FeaturePermission, "Native social exchanges reflect credible praise and reputational harm.", "Player and NPC pawns"),
            S(CASettingKey.AuthorityObedience, "Authority and obedience", "Social and institutional simulation", CABehaviorDomain.SocialAndPolitical, CASettingKind.FeaturePermission, "Delivered relayed orders resolve standing, compliance, reluctance, or refusal.", "Player and NPC groups"),
            S(CASettingKey.EnemyRestraint, "Enemy restraint and custody", "Social and institutional simulation", CABehaviorDomain.CustodyAndAftermath, CASettingKind.FeaturePermission, "NPC restraint and authorized custody resolution use their own authority.", "NPC doctrine and custody institutions"),
            S(CASettingKey.RenderArms, "Render arms", "Presentation", CABehaviorDomain.Presentation, CASettingKind.Presentation, "Draws arm graphics only.", "Presentation"),
            S(CASettingKey.TraceBehavior, "Behavior trace", "Diagnostics", CABehaviorDomain.Diagnostics, CASettingKind.Diagnostic, "Records meaningful causal transitions and requested diagnostics.", "Developer and operator diagnostics")
        };

        private static CASettingDefinition S(CASettingKey key, string label,
            string section, CABehaviorDomain domain, CASettingKind kind,
            string effect, string actors)
        {
            return new CASettingDefinition(key, label, section, domain, kind,
                effect, actors);
        }

        public static bool IsEnabled(CASettingKey key, AwarenessSettings s)
        {
            if (key == CASettingKey.None) return true;
            if (s == null) return false;
            switch (key)
            {
                case CASettingKey.EatSmart: return s.eatSmart;
                case CASettingKey.CriticalHauling: return s.criticalHauling;
                case CASettingKey.LifeSafety: return s.lifeSafety;
                case CASettingKey.RescueOutsiders: return s.rescueOutsiders;
                case CASettingKey.AreaDuty: return s.areaDuty;
                case CASettingKey.AutoUnforbidCorpses: return s.autoUnforbidCorpses;
                case CASettingKey.AutoDigGraves: return s.autoDigGraves;
                case CASettingKey.RaidResponse: return s.raidResponse;
                case CASettingKey.EnemyRestraint: return s.enemyRestraint;
                case CASettingKey.BuddyCarry: return s.buddyCarry;
                case CASettingKey.Gossip: return s.gossip;
                case CASettingKey.SurvivalResponses: return s.survivalResponses;
                case CASettingKey.FieldMedicine: return s.fieldMedicine;
                case CASettingKey.AmbushStrikes: return s.ambushStrikes;
                case CASettingKey.AnimalCare: return s.animalCare;
                case CASettingKey.FireResponse: return s.fireResponse;
                case CASettingKey.WeaponTransitions: return s.weaponTransitions;
                case CASettingKey.Withdrawals: return s.withdrawals;
                case CASettingKey.MovingFire: return s.movingFire;
                case CASettingKey.HoldOrders: return s.holdOrders;
                case CASettingKey.DragOrders: return s.dragOrders;
                case CASettingKey.TrapAwareness: return s.trapAwareness;
                case CASettingKey.BattleDrills: return s.battleDrills;
                case CASettingKey.KnowledgeContacts: return s.knowledgeContacts;
                case CASettingKey.AuthorityObedience: return s.authorityObedience;
                case CASettingKey.OperationalAccess: return s.operationalAccess;
                case CASettingKey.AutonomousHomePlanning: return s.autonomousHomePlanning;
                case CASettingKey.CommsSystem: return s.commsSystem;
                case CASettingKey.DraftChain: return s.draftChain;
                case CASettingKey.BodyWeight: return s.bodyWeight;
                case CASettingKey.DualWield: return s.dualWield;
                case CASettingKey.RenderArms: return s.renderArms;
                case CASettingKey.InformationDetail: return true;
                case CASettingKey.TraceBehavior: return s.traceBehavior;
                default: return false;
            }
        }

        // Cross-setting contracts live here rather than in the consumers.
        // They are intentionally few: the catalog remains the single answer
        // to which permissions own a behavior.
        public static bool IsEnabled(CABehaviorDefinition definition,
            AwarenessSettings s)
        {
            if (definition == null) return false;
            switch (definition.Key)
            {
                case "support.fold_cover":
                    return IsEnabled(CASettingKey.HoldOrders, s)
                        && IsEnabled(CASettingKey.Withdrawals, s);
                case "support.rescue_escort":
                    return IsEnabled(CASettingKey.BuddyCarry, s)
                        || IsEnabled(CASettingKey.FieldMedicine, s);
                case "communication.command_delivery":
                    return IsEnabled(CASettingKey.DraftChain, s)
                        && IsEnabled(CASettingKey.CommsSystem, s);
                default:
                    return definition.AlwaysAvailable
                        || IsEnabled(definition.Setting, s);
            }
        }

        public static void SetEnabled(CASettingKey key, AwarenessSettings s,
            bool value)
        {
            if (s == null) return;
            bool previous = IsEnabled(key, s);
            if (previous == value) return;
            switch (key)
            {
                case CASettingKey.EatSmart: s.eatSmart = value; break;
                case CASettingKey.CriticalHauling: s.criticalHauling = value; break;
                case CASettingKey.LifeSafety: s.lifeSafety = value; break;
                case CASettingKey.RescueOutsiders: s.rescueOutsiders = value; break;
                case CASettingKey.AreaDuty: s.areaDuty = value; break;
                case CASettingKey.AutoUnforbidCorpses: s.autoUnforbidCorpses = value; break;
                case CASettingKey.AutoDigGraves: s.autoDigGraves = value; break;
                case CASettingKey.RaidResponse: s.raidResponse = value; break;
                case CASettingKey.EnemyRestraint: s.enemyRestraint = value; break;
                case CASettingKey.BuddyCarry: s.buddyCarry = value; break;
                case CASettingKey.Gossip: s.gossip = value; break;
                case CASettingKey.SurvivalResponses: s.survivalResponses = value; break;
                case CASettingKey.FieldMedicine: s.fieldMedicine = value; break;
                case CASettingKey.AmbushStrikes: s.ambushStrikes = value; break;
                case CASettingKey.AnimalCare: s.animalCare = value; break;
                case CASettingKey.FireResponse: s.fireResponse = value; break;
                case CASettingKey.WeaponTransitions: s.weaponTransitions = value; break;
                case CASettingKey.Withdrawals: s.withdrawals = value; break;
                case CASettingKey.MovingFire: s.movingFire = value; break;
                case CASettingKey.HoldOrders: s.holdOrders = value; break;
                case CASettingKey.DragOrders: s.dragOrders = value; break;
                case CASettingKey.TrapAwareness: s.trapAwareness = value; break;
                case CASettingKey.BattleDrills: s.battleDrills = value; break;
                case CASettingKey.KnowledgeContacts: s.knowledgeContacts = value; break;
                case CASettingKey.AuthorityObedience: s.authorityObedience = value; break;
                case CASettingKey.OperationalAccess: s.operationalAccess = value; break;
                case CASettingKey.AutonomousHomePlanning: s.autonomousHomePlanning = value; break;
                case CASettingKey.CommsSystem: s.commsSystem = value; break;
                case CASettingKey.DraftChain: s.draftChain = value; break;
                case CASettingKey.BodyWeight: s.bodyWeight = value; break;
                case CASettingKey.DualWield: s.dualWield = value; break;
                case CASettingKey.RenderArms: s.renderArms = value; break;
                case CASettingKey.TraceBehavior: s.traceBehavior = value; break;
            }
            if (key == CASettingKey.AutonomousHomePlanning)
            {
                if (value) s.autonomousHomePlanningResetGeneration++;
                if (Current.ProgramState == ProgramState.Playing)
                {
                    for (int i = 0; i < Find.Maps.Count; i++)
                    {
                        AutonomousHomeMapComponent.For(Find.Maps[i])
                            ?.NotifyPlanningSettingChanged(value,
                                s.autonomousHomePlanningResetGeneration);
                        CAStorageProgramMapComponent.For(Find.Maps[i])
                            ?.NotifyPlanningSettingChanged(value,
                                s.autonomousHomePlanningResetGeneration);
                        CASpatialInitiativeMapComponent.For(Find.Maps[i])
                            ?.NotifyPlanningSettingChanged(value,
                                s.autonomousHomePlanningResetGeneration);
                    }
                }
            }
            CABehaviorRevisions.SettingsChanged();
        }

        public static string Description(CASettingDefinition setting,
            CAInformationDetail detail)
        {
            if (detail == CAInformationDetail.Compact)
                return setting.ImmediateEffect;
            CABehaviorDefinition[] behaviors = CABehaviorCatalog.ForSetting(
                setting.Key);
            string tiers = TierSummary(behaviors);
            string result = setting.ImmediateEffect + " " + setting.ActorText
                + "; " + KindLabel(setting.Kind) + ".";
            if (tiers.Length > 0) result += " Origination: " + tiers + ".";
            string interaction = CrossSettingNote(setting.Key);
            if (interaction.Length > 0) result += " " + interaction;
            if (detail == CAInformationDetail.Expanded && behaviors.Length > 0)
            {
                result += " Behaviors: " + string.Join(", ", behaviors
                    .Where(b => b.ExposeInUi).Select(b => b.Label).ToArray()) + ".";
            }
            return result;
        }

        private static string CrossSettingNote(CASettingKey key)
        {
            switch (key)
            {
                case CASettingKey.DraftChain:
                    return "Relayed command delivery also requires Communication routes.";
                case CASettingKey.CommsSystem:
                    return "Relayed command delivery also requires Command delivery.";
                case CASettingKey.HoldOrders:
                    return "Fold cover also requires Withdrawal.";
                case CASettingKey.Withdrawals:
                    return "Fold cover also requires Hold.";
                case CASettingKey.BuddyCarry:
                    return "Rescue escort is available when either this permission or Field medicine is enabled.";
                case CASettingKey.FieldMedicine:
                    return "Rescue escort is available when either this permission or Buddy carry and escort is enabled.";
                default:
                    return string.Empty;
            }
        }

        public static string KindLabel(CASettingKind kind)
        {
            switch (kind)
            {
                case CASettingKind.FeaturePermission: return "behavior permission";
                case CASettingKind.CommunicationInfrastructure: return "communication infrastructure";
                case CASettingKind.ExecutionCapability: return "execution capability";
                case CASettingKind.Presentation: return "presentation only";
                case CASettingKind.Diagnostic: return "diagnostic only";
                default: return "always available";
            }
        }

        private static string TierSummary(CABehaviorDefinition[] definitions)
        {
            if (definitions.Any(d => d.InitiativeIndependent))
                return "initiative-independent where stated; other actions follow their registered tier";
            if (definitions.Any(d => d.MinimumInitiative == CAInitiativeTier.Standard))
                return "Standard and above";
            if (definitions.Any(d => d.MinimumInitiative == CAInitiativeTier.Proactive))
                return "Proactive and Autonomous";
            if (definitions.Any(d => d.MinimumInitiative == CAInitiativeTier.Autonomous))
                return "Autonomous or institutional authority";
            return string.Empty;
        }
    }

    public static class CAInitiativePresentation
    {
        public static readonly CAInitiativeTier[] ActiveTiers =
        {
            CAInitiativeTier.Standard,
            CAInitiativeTier.Proactive,
            CAInitiativeTier.Autonomous
        };

        public static string Label(CAInitiativeTier tier)
        {
            switch (tier)
            {
                case CAInitiativeTier.Standard: return "Standard";
                case CAInitiativeTier.Proactive: return "Proactive";
                case CAInitiativeTier.Autonomous: return "Autonomous";
                default: return "Standard";
            }
        }

        public static string Description(CAInitiativeTier tier,
            CAInformationDetail detail, Pawn pawn = null)
        {
            string compact;
            switch (tier)
            {
                case CAInitiativeTier.Proactive:
                    compact = "Bounded responses to current known needs.";
                    break;
                case CAInitiativeTier.Autonomous:
                    compact = "Persistent planning and broader coordination inside delegated authority.";
                    break;
                default:
                    compact = "Native behavior, direct orders, and baseline safety.";
                    break;
            }
            if (detail == CAInformationDetail.Compact) return compact;
            CABehaviorDefinition[] definitions = pawn != null
                ? CAEffectiveBehaviorProfileCache.Of(pawn)?.Definitions()
                    .Where(d => d.ExposeInUi).ToArray()
                : CABehaviorCatalog.ForTier(tier).Where(d => d.ExposeInUi)
                    .ToArray();
            if (definitions == null) definitions = Array.Empty<CABehaviorDefinition>();
            string domains = string.Join(", ", definitions.Select(d => d.Domain)
                .Distinct().Take(5).Select(DomainLabel).ToArray());
            string result = compact;
            if (domains.Length > 0) result += pawn != null
                ? " Active scope: " + domains + "."
                : " Tier examples: " + domains + ".";
            if (detail == CAInformationDetail.Expanded)
            {
                string labels = string.Join(", ", definitions.Take(18)
                    .Select(d => d.Label).ToArray());
                if (labels.Length > 0) result += pawn != null
                    ? " Enabled behaviors: " + labels
                        + (definitions.Length > 18 ? ", and more." : ".")
                    : " These may originate only when their separate permission, authority, knowledge, and material conditions allow: "
                        + labels + (definitions.Length > 18 ? ", and more." : ".");
            }
            return result;
        }

        public static string DomainLabel(CABehaviorDomain domain)
        {
            switch (domain)
            {
                case CABehaviorDomain.KnowledgeAndCommunication: return "knowledge and communication";
                case CABehaviorDomain.SurvivalAndImmediateSafety: return "survival and safety";
                case CABehaviorDomain.WelfareAndCare: return "welfare and care";
                case CABehaviorDomain.HazardResponse: return "hazard response";
                case CABehaviorDomain.OperationalReadiness: return "operational readiness";
                case CABehaviorDomain.CombatSelfPreservation: return "combat self-preservation";
                case CABehaviorDomain.TacticalCoordination: return "tactical coordination";
                case CABehaviorDomain.DomesticAndSpatial: return "home and spatial work";
                case CABehaviorDomain.LogisticsAndProvision: return "logistics and provision";
                case CABehaviorDomain.SocialAndPolitical: return "social and political";
                case CABehaviorDomain.CustodyAndAftermath: return "custody and aftermath";
                case CABehaviorDomain.AnimalCare: return "animal care";
                case CABehaviorDomain.InstitutionalDevelopment: return "institutional development";
                case CABehaviorDomain.Presentation: return "presentation";
                case CABehaviorDomain.Diagnostics: return "diagnostics";
                default: return domain.ToString();
            }
        }
    }
}
