using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    internal static class CANativeCultureEventPersistenceContract
    {
        internal const int CurrentRecordSchemaVersion = 2;
    }

    internal enum CANativeCultureOccurrenceScope
    {
        Actor,
        SharedAct,
        SharedTargetAtTick
    }

    internal enum CANativeCultureEventProvenance
    {
        Unspecified,
        Actor,
        Observer,
        Participant,
        Recipient
    }

    internal sealed class CANativeCultureEventAdapterDef
    {
        internal string PackageId;
        internal string EventDefName;
        internal string PracticeKey;
        // Native emitters vary: some report one actor, some report every
        // participant in one act, and PawnObserver reports every observer of
        // one target. The explicit scope prevents emitter multiplicity from
        // becoming false repeated-practice evidence.
        internal CANativeCultureOccurrenceScope OccurrenceScope;
        internal CANativeCultureEventProvenance Provenance;
        internal string Identity => PackageId + "|" + EventDefName;
    }

    // One portable registry owns the exact native-event semantics used by
    // runtime observation, streaming save preflight, and receipt tools.
    // Native events remain the executors; this registry only interprets an
    // event after it has occurred.
    internal static class CANativeCultureEventAdapterRegistry
    {
        private const string Core = "ludeon.rimworld";
        private const string Ideology = "ludeon.rimworld.ideology";
        private const string Odyssey = "ludeon.rimworld.odyssey";
        private const string MorePrecepts = "llunak.MorePrecepts";

        private static readonly CANativeCultureEventAdapterDef[] Definitions =
            Build().ToArray();
        private static readonly Dictionary<string,
            CANativeCultureEventAdapterDef> ByIdentity = Definitions
                .ToDictionary(value => value.Identity, value => value,
                    StringComparer.OrdinalIgnoreCase);

        internal static IReadOnlyList<CANativeCultureEventAdapterDef> All =>
            Definitions;

        internal static CANativeCultureEventAdapterDef Find(string packageId,
            string eventDefName)
        {
            ByIdentity.TryGetValue((packageId ?? "") + "|"
                + (eventDefName ?? ""), out CANativeCultureEventAdapterDef value);
            return value;
        }

        internal static bool HasPractice(string practiceKey) =>
            !string.IsNullOrWhiteSpace(practiceKey) && Definitions.Any(value =>
                value.PracticeKey == practiceKey);

        internal static string ValidationFailure()
        {
            if (Definitions.GroupBy(value => value.Identity,
                    StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() != 1))
                return "native Culture event identities are duplicated";
            foreach (CANativeCultureEventAdapterDef definition in Definitions)
            {
                if (string.IsNullOrWhiteSpace(definition.PackageId)
                    || string.IsNullOrWhiteSpace(definition.EventDefName))
                    return "native Culture event identity is incomplete";
                if (CACulturalPracticeRegistry.Find(definition.PracticeKey)
                    == null)
                    return definition.Identity + " names an unknown practice";
                if (definition.Provenance
                    == CANativeCultureEventProvenance.Unspecified)
                    return definition.Identity
                        + " has no audited native-event provenance";
            }
            return null;
        }

        private static IEnumerable<CANativeCultureEventAdapterDef> Build()
        {
            var values = new List<CANativeCultureEventAdapterDef>();
            Add(values, Core, CACulturalPracticeRegistry.HumanFleshConsumption,
                "AteHumanMeat", "AteHumanMeatAsIngredient",
                "AteHumanMeatDirect");
            Add(values, Core, CACulturalPracticeRegistry.HumanButchery,
                "ButcheredHuman");
            AddTargetShared(values, Core,
                CACulturalPracticeRegistry.CorpseExposure,
                "ObservedLayingCorpse", "ObservedLayingRottingCorpse");
            Add(values, Core, CACulturalPracticeRegistry.OrganExtraction,
                "HarvestedOrganFromColonist", "HarvestedOrganFromGuest");
            Add(values, Core, CACulturalPracticeRegistry.BodyModification,
                "InstalledProsthetic");
            AddShared(values, Core,
                CACulturalPracticeRegistry.NonSpousalIntimacy,
                "GotLovin_NonSpouse");
            AddShared(values, Core,
                CACulturalPracticeRegistry.ExclusiveMarriage,
                "GotMarried_SpouseCount_OneOrFewer");
            AddShared(values, Core,
                CACulturalPracticeRegistry.PluralMarriage,
                "GotMarried_SpouseCount_Two");
            Add(values, Core, CACulturalPracticeRegistry.DrugUse,
                "IngestedRecreationalDrug");
            Add(values, Core, CACulturalPracticeRegistry.DrugAdministration,
                "AdministeredRecreationalDrug");
            Add(values, Core,
                CACulturalPracticeRegistry.UnfamiliarFoodConsumption,
                "AteInsectMeatAsIngredient", "AteNutrientPaste");
            Add(values, Core, CACulturalPracticeRegistry.ResourceExtraction,
                "CutTree", "Mined");
            Add(values, Core, CACulturalPracticeRegistry.Cultivation,
                "SowedPlant");
            Add(values, Core, CACulturalPracticeRegistry.Execution,
                "ExecutedColonist", "ExecutedGuest", "ExecutedPrisoner",
                "ExecutedPrisonerGuilty", "ExecutedPrisonerInnocent");
            AddShared(values, Core, CACulturalPracticeRegistry.Enslavement,
                "SoldSlave");

            Add(values, Ideology,
                CACulturalPracticeRegistry.UnfamiliarFoodConsumption,
                "AteFungus", "AteFungusAsIngredient");
            Add(values, Ideology,
                CACulturalPracticeRegistry.AnimalFoodConsumption,
                "AteMeat", "AteVeneratedAnimalMeat");
            AddRecipient(values, Ideology,
                CACulturalPracticeRegistry.RitualInjury,
                "GotBlinded", "GotScarified");
            Add(values, Ideology, CACulturalPracticeRegistry.OrganExtraction,
                "HarvestedOrgan");
            Add(values, Ideology, CACulturalPracticeRegistry.OrganTrade,
                "TradedOrgan", "SoldOrgan");
            Add(values, Ideology, CACulturalPracticeRegistry.BodyModification,
                "UsedBiosculpterPod", "InstalledOrgan");
            AddRecipient(values, Ideology,
                CACulturalPracticeRegistry.DoctrinalChange,
                "ChangedIdeo", "ConvertedNewMember");
            AddShared(values, Ideology,
                CACulturalPracticeRegistry.PluralMarriage,
                "GotMarried_SpouseCount_Three",
                "GotMarried_SpouseCount_Four",
                "GotMarried_SpouseCount_FiveOrMore");
            AddShared(values, Ideology,
                CACulturalPracticeRegistry.CrossIdeoligionObservance,
                "ParticipatedInOthersRitual");
            Add(values, Ideology, CACulturalPracticeRegistry.AnimalSlaughter,
                "SlaughteredAnimal");
            Add(values, Ideology,
                CACulturalPracticeRegistry.InnocentAnimalKilling,
                "KilledInnocentAnimal");
            AddShared(values, Ideology, CACulturalPracticeRegistry.Raiding,
                "Raided");
            Add(values, Ideology, CACulturalPracticeRegistry.Enslavement,
                "EnslavedPrisoner", "EnslavedPrisonerNotPreviouslyEnslaved");

            Add(values, Odyssey, CACulturalPracticeRegistry.AnimalSlaughter,
                "SlaughteredFish");

            Add(values, MorePrecepts, CACulturalPracticeRegistry.DrugUse,
                "IngestedAlcohol");
            Add(values, MorePrecepts,
                CACulturalPracticeRegistry.DrugAdministration,
                "AdministeredAlcohol");
            AddShared(values, MorePrecepts,
                CACulturalPracticeRegistry.SettlementAbandonment,
                "Nomadism_AbandonedSettlement");
            Add(values, MorePrecepts,
                CACulturalPracticeRegistry.DownedPersonStripping,
                "TakingFromDowned_DownedStripped");
            Add(values, MorePrecepts,
                CACulturalPracticeRegistry.InterpersonalViolence,
                "Violence_AttackedPerson",
                "Violence_AttackedNonHostilePerson");
            return values;
        }

        private static void Add(List<CANativeCultureEventAdapterDef> values,
            string packageId, string practiceKey, params string[] eventDefNames)
        {
            foreach (string eventDefName in eventDefNames)
                values.Add(new CANativeCultureEventAdapterDef
                {
                    PackageId = packageId,
                    EventDefName = eventDefName,
                    PracticeKey = practiceKey,
                    OccurrenceScope = CANativeCultureOccurrenceScope.Actor,
                    Provenance = CANativeCultureEventProvenance.Actor
                });
        }

        private static void AddShared(
            List<CANativeCultureEventAdapterDef> values,
            string packageId, string practiceKey,
            params string[] eventDefNames)
        {
            foreach (string eventDefName in eventDefNames)
                values.Add(new CANativeCultureEventAdapterDef
                {
                    PackageId = packageId,
                    EventDefName = eventDefName,
                    PracticeKey = practiceKey,
                    OccurrenceScope = CANativeCultureOccurrenceScope.SharedAct,
                    Provenance = CANativeCultureEventProvenance.Participant
                });
        }

        private static void AddTargetShared(
            List<CANativeCultureEventAdapterDef> values,
            string packageId, string practiceKey,
            params string[] eventDefNames)
        {
            foreach (string eventDefName in eventDefNames)
                values.Add(new CANativeCultureEventAdapterDef
                {
                    PackageId = packageId,
                    EventDefName = eventDefName,
                    PracticeKey = practiceKey,
                    OccurrenceScope =
                        CANativeCultureOccurrenceScope.SharedTargetAtTick,
                    Provenance = CANativeCultureEventProvenance.Observer
                });
        }

        private static void AddRecipient(
            List<CANativeCultureEventAdapterDef> values,
            string packageId, string practiceKey,
            params string[] eventDefNames)
        {
            foreach (string eventDefName in eventDefNames)
                values.Add(new CANativeCultureEventAdapterDef
                {
                    PackageId = packageId,
                    EventDefName = eventDefName,
                    PracticeKey = practiceKey,
                    OccurrenceScope = CANativeCultureOccurrenceScope.Actor,
                    Provenance = CANativeCultureEventProvenance.Recipient
                });
        }
    }
}
