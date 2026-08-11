using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    internal enum CACulturalExpressionStatus : byte
    {
        Aligned,
        Adaptive,
        Constrained,
        Plural,
        Tension
    }

    internal sealed class CACulturalReading
    {
        internal string Family;
        internal string Summary;
        internal readonly List<string> Facts = new List<string>();
    }

    // A deterministic read of a population in its actual circumstances. It
    // records no authored choice and selects no physical object.
    internal sealed class CACulturalExpression
    {
        internal string Summary;
        internal CACulturalExpressionStatus Status;
        internal string SourceSignature;
        internal readonly List<CACulturalReading> Readings =
            new List<CACulturalReading>();
        internal readonly List<string> ContributingFacts =
            new List<string>();

        internal string Presented()
        {
            string compact = StatusWords(Status) + ": " + Summary;
            return compact + "\n\n" + string.Join("\n\n",
                Readings.Select(item => item.Family + "\n" + item.Summary)
                    .ToArray());
        }

        internal static string StatusWords(CACulturalExpressionStatus status)
        {
            switch (status)
            {
                case CACulturalExpressionStatus.Adaptive: return "Adaptive";
                case CACulturalExpressionStatus.Constrained: return "Constrained";
                case CACulturalExpressionStatus.Plural: return "Plural";
                case CACulturalExpressionStatus.Tension: return "In tension";
                default: return "Aligned";
            }
        }
    }

    internal static class CACulturalExpressionModel
    {
        private sealed class WeightedBeliefSource
        {
            internal string Identity;
            internal string Label;
            internal int Share;
            internal List<CAAxisEntry> Positions = new List<CAAxisEntry>();
        }

        private sealed class WeightedIdeoligionSource
        {
            internal string Identity;
            internal string Label;
            internal string Name;
            internal int Share;
            internal List<string> CommitmentKeys = new List<string>();
            internal List<string> CommitmentLabels = new List<string>();
        }

        private sealed class Inputs
        {
            internal string Scope;
            internal string CultureName;
            internal CACulture Culture;
            internal List<string> CultureFacts = new List<string>();
            internal string Ideoligion;
            internal List<string> IdeoligionCommitmentKeys =
                new List<string>();
            internal List<string> IdeoligionCommitmentLabels =
                new List<string>();
            internal List<CAAxisEntry> Beliefs = new List<CAAxisEntry>();
            internal List<CAAxisEntry> Institutions = new List<CAAxisEntry>();
            internal List<WeightedBeliefSource> PopulationBeliefs =
                new List<WeightedBeliefSource>();
            internal List<WeightedIdeoligionSource> PopulationIdeoligions =
                new List<WeightedIdeoligionSource>();
            internal List<CASettlementPopulationGroup> Populations =
                new List<CASettlementPopulationGroup>();
            internal List<CAStartingProvision> Provisions =
                new List<CAStartingProvision>();
            internal int Residents;
            internal int Land;
            internal int Access;
            internal int Services;
            internal int Civic;
            internal int Economy;
            internal int Trade;
            internal int Specialization;
            internal int History;
            internal int Facilities;
            internal int Role = -1;
            internal int Scale = -1;
            internal int Form = -1;
            internal int Fortification = -1;
            internal int Organization = -1;
            internal bool Road;
            internal bool River;
            internal bool Coast;
            internal int HostileRelations;
            internal int NeutralRelations;
            internal string RelationReceipt;
            internal bool Founding;
            internal CAFoundingArrangement Arrangement;
            internal int Buildings;
            internal int InfrastructureObjects;
        }

        internal static CACulturalExpression ForSettlement(
            CARegionalPlan plan, CARegionalSettlementPlan settlement)
        {
            if (settlement == null)
                return Missing("Settlement facts are unavailable.");
            CARegionalFactionPlan faction = plan?.FactionPlan(
                settlement.factionKey);
            TechLevel knowledge = CASettlementAxes.TemplateEraPrior(
                faction?.ResolvedFactionDef);
            int facilities = CASettlementStartingState.ResolveFacilityMask(
                plan, settlement, faction?.ResolvedFactionDef);
            int access = CASettlementStartingState.Access(plan, settlement);
            int services = CASettlementStartingState.Services(plan, settlement);
            int civic = CASettlementStartingState.Civic(plan, settlement);
            int practiceCeiling = CASettlementAxes.LocalPracticeCeiling(
                facilities, access, services, civic, knowledge);
            int hostile = 0;
            int neutral = 0;
            foreach (CARegionalRelationPlan relation in plan?.relations
                ?? new List<CARegionalRelationPlan>())
            {
                if (relation == null || (relation.leftFactionKey
                        != settlement.factionKey && relation.rightFactionKey
                        != settlement.factionKey)) continue;
                if (relation.relation == FactionRelationKind.Hostile) hostile++;
                else if (relation.relation == FactionRelationKind.Neutral)
                    neutral++;
            }
            var input = new Inputs
            {
                Scope = "settlement:" + settlement.slot,
                CultureName = settlement.localCulture?.name
                    ?? faction?.culture?.name ?? "inherited culture",
                Culture = settlement.localCulture ?? faction?.culture,
                CultureFacts = CultureFacts(settlement.localCulture),
                Ideoligion = faction?.LivingIdeo?.name ?? "local Ideoligion",
                IdeoligionCommitmentKeys = IdeoligionCommitmentKeys(
                    faction?.LivingIdeo),
                IdeoligionCommitmentLabels = IdeoligionCommitmentLabels(
                    faction?.LivingIdeo),
                Beliefs = CopyAxes(faction?.politicalBeliefs?.positions),
                Institutions = CopyAxes(faction?.factionStructure),
                Populations = CopyPopulations(settlement.populationGroups),
                PopulationBeliefs = PopulationBeliefs(plan,
                    settlement.populationGroups,
                    faction?.politicalBeliefs?.positions, false),
                PopulationIdeoligions = PopulationIdeoligions(plan,
                    settlement.populationGroups, faction?.LivingIdeo),
                Provisions = CopyProvisions(settlement.startingProvisions),
                Residents = settlement.residentPopulation,
                Land = settlement.landCapacity,
                Access = access,
                Services = services,
                Civic = civic,
                Economy = settlement.economicCapacity,
                Trade = settlement.tradeConnectivity,
                Specialization = settlement.specialization,
                History = settlement.historicalDevelopment,
                Facilities = facilities,
                Role = settlement.realizedRole,
                Scale = settlement.realizedScale,
                Form = (int)CASettlementAxes.Form(settlement.authoredForm,
                    knowledge),
                Fortification = CASettlementAxes.PracticedCapabilityBasis(
                    CASettlementAxes.CapFortification, knowledge,
                    practiceCeiling),
                Organization = CASettlementAxes.PracticedCapabilityBasis(
                    CASettlementAxes.CapOrganization, knowledge,
                    practiceCeiling),
                Road = CARegionalPlanUtility.ConstituentHasRoad(
                    settlement.memberTileId),
                River = CARegionalPlanUtility.ConstituentHasRiver(
                    settlement.memberTileId),
                Coast = CARegionalPlanUtility.ConstituentIsCoastal(
                    settlement.memberTileId),
                HostileRelations = hostile,
                NeutralRelations = neutral
            };
            return Read(input);
        }

        internal static CACulturalExpression ForMaterializedSettlement(
            CARegionalSettlementRecord settlement, Map map = null)
        {
            if (settlement == null)
                return Missing("Settlement record is unavailable.");
            CAFactionState faction = CAFactionStateWorldComponent.Current
                ?.Find(settlement.faction);
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.Regions.FirstOrDefault(item => item != null
                    && item.regionalId == settlement.regionKey);
            CARegionalFactionPlan plannedFaction = plan?.FactionPlan(
                settlement.factionKey);
            int hostile = 0;
            int neutral = 0;
            CountRelations(plan, settlement.factionKey, ref hostile,
                ref neutral);
            if (string.Equals(settlement.relationAtMaterialization,
                    FactionRelationKind.Hostile.ToString(),
                    StringComparison.OrdinalIgnoreCase)) hostile++;
            else if (string.Equals(settlement.relationAtMaterialization,
                    FactionRelationKind.Neutral.ToString(),
                    StringComparison.OrdinalIgnoreCase)) neutral++;
            var input = new Inputs
            {
                Scope = settlement.regionalId ?? "materialized-settlement",
                CultureName = settlement.culture?.name
                    ?? faction?.culture?.name ?? "inherited culture",
                Culture = settlement.culture ?? faction?.culture,
                CultureFacts = CultureFacts(settlement.culture),
                Ideoligion = settlement.faction?.ideos?.PrimaryIdeo?.name
                    ?? "local Ideoligion",
                IdeoligionCommitmentKeys = IdeoligionCommitmentKeys(
                    settlement.faction?.ideos?.PrimaryIdeo),
                IdeoligionCommitmentLabels = IdeoligionCommitmentLabels(
                    settlement.faction?.ideos?.PrimaryIdeo),
                Beliefs = CopyAxes(faction?.politicalBeliefs?.positions
                    ?? plannedFaction?.politicalBeliefs?.positions),
                Institutions = CopyAxes(faction?.factionStructure
                    ?? plannedFaction?.factionStructure),
                Populations = CopyPopulations(settlement.populationGroups),
                PopulationBeliefs = PopulationBeliefs(plan,
                    settlement.populationGroups,
                    faction?.politicalBeliefs?.positions
                        ?? plannedFaction?.politicalBeliefs?.positions, true),
                PopulationIdeoligions = MaterializedIdeoligions(settlement,
                    map) ?? PopulationIdeoligions(plan,
                        settlement.populationGroups,
                        settlement.faction?.ideos?.PrimaryIdeo),
                Provisions = CopyProvisions(settlement.startingProvisions),
                Residents = settlement.populationCurrent > 0
                    ? settlement.populationCurrent
                    : settlement.residentPopulation,
                Land = settlement.landCapacity,
                Access = settlement.accessInfrastructure,
                Services = settlement.serviceInfrastructure,
                Civic = settlement.civicInfrastructure,
                Economy = settlement.economicCapacity,
                Trade = settlement.tradeConnectivity,
                Specialization = settlement.specialization,
                History = settlement.historicalDevelopment,
                Facilities = settlement.startingFacilityMask,
                Role = settlement.realizedRole,
                Scale = settlement.realizedScale,
                Form = settlement.settlementForm,
                Fortification = settlement.fortification,
                Organization = settlement.organization,
                Road = CARegionalPlanUtility.ConstituentHasRoad(
                    settlement.memberTileId),
                River = CARegionalPlanUtility.ConstituentHasRiver(
                    settlement.memberTileId),
                Coast = CARegionalPlanUtility.ConstituentIsCoastal(
                    settlement.memberTileId),
                HostileRelations = hostile,
                NeutralRelations = neutral,
                RelationReceipt = (settlement.relationAtMaterialization
                    ?? "unrecorded") + " goodwill "
                    + settlement.goodwillAtMaterialization,
                Buildings = settlement.buildingCount,
                InfrastructureObjects = settlement.infrastructureCount
            };
            return Read(input);
        }

        internal static CACulturalExpression ForFounders(
            CAPlayerFoundingPlan founding)
        {
            if (founding == null)
                return Missing("Founding state is unavailable.");
            Ideo nativeIdeo = CAPlayerFoundingModel.NativeIdeo;
            var input = new Inputs
            {
                Scope = "player-founding",
                CultureName = founding.culture?.name ?? "inherited culture",
                Culture = founding.culture,
                CultureFacts = CultureFacts(founding.culture),
                Ideoligion = founding.nativeIdeoName ?? "founders' Ideoligion",
                IdeoligionCommitmentKeys = IdeoligionCommitmentKeys(nativeIdeo),
                IdeoligionCommitmentLabels =
                    IdeoligionCommitmentLabels(nativeIdeo),
                Beliefs = CopyAxes(founding.politicalBeliefs?.positions),
                Founding = true,
                Arrangement = founding.arrangement,
                Residents = CAPlayerFoundingModel.StartingPawnCount(),
                Land = -1,
                Access = -1,
                Services = -1,
                Civic = -1,
                Economy = -1,
                Trade = -1,
                Specialization = -1,
                History = 0,
                Facilities = 0
            };
            return Read(input);
        }

        internal static IReadOnlyList<CACulturalExpression> ForFaction(
            CARegionalPlan plan, CARegionalFactionPlan faction)
        {
            if (plan?.settlements == null || faction == null)
                return new List<CACulturalExpression>();
            return plan.settlements.Where(item => item != null
                    && item.factionKey == faction.key)
                .OrderBy(item => item.slot)
                .Select(item => ForSettlement(plan, item)).ToList();
        }

        private static CACulturalExpression Read(Inputs input)
        {
            var result = new CACulturalExpression();
            List<string> tensions = PoliticalTensions(input);
            List<CASettlementPopulationGroup> populations = input.Populations
                .Where(item => item != null && item.share > 0)
                .OrderByDescending(item => item.share).ThenBy(item => item.key)
                .ToList();
            AddReading(result, "Continuity and change",
                input.CultureFacts.Count == 0
                    ? "No cultural history is recorded."
                    : input.CultureFacts[0],
                input.CultureFacts.Skip(1).ToArray());

            AddReading(result, "Lived practices",
                CACultureHistory.PracticeSummary(input.Culture));

            AddReading(result, "Population and inheritance",
                PopulationCultureSummary(input.Culture, populations),
                "Resident groups: " + PopulationWords(populations),
                "Resident belief sources: "
                    + WeightedBeliefWords(input.PopulationBeliefs),
                "Resident Ideoligions: "
                    + WeightedIdeoligionWords(input.PopulationIdeoligions));

            AddReading(result, "Belief and instituted order",
                tensions.Count > 0
                    ? tensions.Count + " preferred positions differ from current practice."
                    : input.Founding
                        ? "The founders carry beliefs; only the landing arrangement is adopted."
                        : "Recorded political beliefs and institutions are aligned.",
                "Beliefs held: " + AxisSetWords(input.Beliefs),
                input.Founding
                    ? "Rules adopted at landing: "
                        + FoundingWords(input.Arrangement)
                    : "Current order: " + AxisSetWords(input.Institutions),
                "Ideoligion: " + input.Ideoligion);
            result.Readings[result.Readings.Count - 1].Facts.Add(
                "Ideoligion commitments: "
                    + IdeoligionCommitmentWords(input));
            if (tensions.Count > 0)
                result.Readings[result.Readings.Count - 1].Facts.AddRange(
                    tensions.Select(item => "Conflict: " + item));

            AddReading(result, "Present setting",
                input.Founding
                    ? "The settlement has not yet accumulated local material history."
                    : "Current ground and institutions constrain how culture is lived; they do not define it.",
                "Residents: " + input.Residents,
                "Ground and routes: " + GeographyWords(input) + ".",
                "Provision arrangements: " + ProvisionWords(input.Provisions),
                "Current order: " + InstitutionSummary(input) + ".",
                "Material setting: " + MaterialSummary(input) + ".");

            CACulturalExpressionCausalResult causal =
                CACulturalExpressionCausalKernel.Evaluate(
                    new CACulturalExpressionCausalInput
                    {
                        Scope = input.Scope,
                        CultureName = input.CultureName,
                        CultureMaturity = input.Culture?.maturity.ToString(),
                        CultureRevision = input.Culture?.revision ?? 0,
                        CultureTransitionCount = input.Culture?.transitions?
                            .Count(item => item != null) ?? 0,
                        CultureConstituents = CausalConstituents(
                            input.Culture),
                        CulturePractices = CausalPractices(input.Culture),
                        Ideoligion = input.Ideoligion,
                        IdeoligionCommitments = input.IdeoligionCommitmentKeys
                            .ToList(),
                        PoliticalBeliefs = CausalAxes(input.Beliefs),
                        InstitutionalPractice = CausalAxes(input.Institutions),
                        Populations = populations.Select(item =>
                            new CACulturalPopulationCause
                            {
                                Key = item.key.ToString(),
                                Share = item.share,
                                IdeoligionSource = IdeoligionSource(item),
                                BeliefSource = BeliefSource(item),
                                SeparateQuarter = item.quarter
                            }).ToList(),
                        WeightedBeliefs = input.PopulationBeliefs.Select(item =>
                            new CACulturalWeightedBeliefCause
                            {
                                Identity = item.Identity,
                                Share = item.Share,
                                Positions = CausalAxes(item.Positions)
                            }).ToList(),
                        PopulationIdeoligionIdentities =
                            input.PopulationIdeoligions.Select(item =>
                                item.Identity).ToList(),
                        Residents = input.Residents,
                        Land = input.Land,
                        Access = input.Access,
                        Services = input.Services,
                        Civic = input.Civic,
                        Economy = input.Economy,
                        Trade = input.Trade,
                        Specialization = input.Specialization,
                        History = input.History,
                        Facilities = input.Facilities,
                        Role = input.Role,
                        Scale = input.Scale,
                        Form = input.Form,
                        Fortification = input.Fortification,
                        Organization = input.Organization,
                        Road = input.Road,
                        River = input.River,
                        Coast = input.Coast,
                        HostileRelations = input.HostileRelations,
                        NeutralRelations = input.NeutralRelations,
                        RelationReceipt = input.RelationReceipt,
                        Founding = input.Founding,
                        Tension = tensions.Count > 0,
                        FoundingArrangement = FoundingWords(input.Arrangement),
                        Buildings = input.Buildings,
                        InfrastructureObjects = input.InfrastructureObjects,
                        ProvisionFingerprint = ProvisionFingerprint(
                            input.Provisions),
                        InstitutionSummary = InstitutionSummary(input)
                            .CapitalizeFirst(),
                        MaterialSummary = MaterialSummary(input),
                        ReadingFacts = result.Readings.SelectMany(item =>
                            item.Facts).Concat(input.CultureFacts).ToList()
                    });
            result.Status = (CACulturalExpressionStatus)causal.Status;
            result.Summary = causal.Summary;
            result.ContributingFacts.AddRange(causal.Facts);
            result.SourceSignature = causal.Signature;
            return result;
        }

        private static List<string> CultureFacts(CACulture culture)
        {
            if (culture == null) return new List<string>();
            var result = new List<string>
            {
                CACultureHistory.ContinuitySummary(culture),
                "culture identity=" + (culture.id ?? "unrecorded"),
                "culture maturity=" + culture.maturity,
                "culture composition="
                    + (culture.compositionSignature ?? "unrecorded")
            };
            foreach (CACultureTransition transition in (culture.transitions
                ?? new List<CACultureTransition>()).Where(item => item != null)
                .OrderBy(item => item.sequence))
                result.Add("culture transition " + transition.sequence + ": "
                    + (transition.cause ?? "change") + " - "
                    + (transition.summary ?? "no summary"));
            foreach (CACulturePractice practice in (culture.practices
                    ?? new List<CACulturePractice>()).Where(item => item != null)
                .OrderByDescending(item => item.strength)
                .ThenBy(item => item.key))
                result.Add("culture practice " + (practice.key ?? "unrecorded")
                    + ": " + practice.strength + "/100 - "
                    + (practice.sourceSignature ?? "unrecorded source"));
            return result;
        }

        private static List<CACulturalConstituentState> CausalConstituents(
            CACulture culture)
        {
            return (culture?.constituents
                    ?? new List<CACultureConstituent>())
                .Where(item => item != null && item.share > 0)
                .Select(item => new CACulturalConstituentState
                {
                    CultureId = item.cultureId,
                    Label = item.label,
                    Share = item.share,
                    Inherited = item.inherited,
                    SeparateQuarter = item.separateQuarter
                }).ToList();
        }

        private static List<CACulturalPracticeState> CausalPractices(
            CACulture culture)
        {
            return (culture?.practices ?? new List<CACulturePractice>())
                .Where(item => item != null && item.strength > 0)
                .Select(item => new CACulturalPracticeState
                {
                    Key = item.key,
                    Summary = item.summary,
                    Strength = item.strength,
                    SourceSignature = item.sourceSignature
                }).ToList();
        }

        private static List<CACulturalAxisCause> CausalAxes(
            IEnumerable<CAAxisEntry> positions)
        {
            return (positions ?? Enumerable.Empty<CAAxisEntry>())
                .Where(item => item != null)
                .Select(item => new CACulturalAxisCause
                {
                    Axis = item.axisKey,
                    Option = item.optionKey
                }).ToList();
        }

        private static void AddReading(CACulturalExpression result,
            string family, string summary, params string[] facts)
        {
            var reading = new CACulturalReading
            {
                Family = family,
                Summary = summary
            };
            reading.Facts.AddRange(facts.Where(item => !item.NullOrEmpty()));
            result.Readings.Add(reading);
        }

        private static string InstitutionSummary(Inputs input)
        {
            return Axis(input, CAFactionAxes.Leadership)
                + " leadership uses " + Axis(input, CAFactionAxes.Decisions)
                + " decisions";
        }

        private static string MaterialSummary(Inputs input)
        {
            if (input.Founding)
                return "No local buildings or routes have yet become history";
            string facilities = FacilityWords(input.Facilities);
            string counts = input.Buildings > 0 || input.InfrastructureObjects > 0
                ? "; " + input.Buildings + " buildings and "
                    + input.InfrastructureObjects + " route or utility objects"
                : "";
            return "facilities: " + facilities + "; routes: "
                + GeographyWords(input) + counts;
        }

        private static string Axis(Inputs input, string key)
        {
            string current = CAFactionAxes.KeyOf(input.Institutions, key);
            if (current.NullOrEmpty())
                current = CAFactionAxes.KeyOf(input.Beliefs, key);
            return OptionLabel(key, current);
        }

        private static string PreferredAxis(Inputs input, string key)
        {
            var weighted = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (WeightedBeliefSource source in input.PopulationBeliefs)
            {
                string option = CAFactionAxes.KeyOf(source.Positions, key);
                if (option.NullOrEmpty()) continue;
                weighted[option] = weighted.TryGetValue(option,
                    out int current) ? current + source.Share : source.Share;
            }
            if (weighted.Count == 0)
                return OptionLabel(key, CAFactionAxes.KeyOf(
                    input.Beliefs, key));
            if (weighted.Count == 1)
                return OptionLabel(key, weighted.Keys.First());
            return string.Join(", ", weighted.OrderByDescending(item =>
                    item.Value).ThenBy(item => item.Key)
                .Select(item => OptionLabel(key, item.Key) + " "
                    + item.Value + "%")
                .ToArray());
        }

        private static List<string> PoliticalTensions(Inputs input)
        {
            if (input.Founding)
                return CAPoliticalBeliefPractice.ReadAgainstPoliticalBeliefs(
                        new CAPoliticalBeliefs { positions = input.Beliefs },
                        input.Arrangement)
                    .Where(item => item != null && !item.Silent
                        && !item.conforms)
                    .Select(item => item.title + ": " + item.belief
                        + "; adopted " + item.adopted + ".").ToList();
            if (input.PopulationBeliefs.Count == 0)
                return CAFactionStructureModel.Tensions(
                    new CAPoliticalBeliefs { positions = input.Beliefs },
                    input.Institutions);
            var result = new List<string>();
            foreach (WeightedBeliefSource source in input.PopulationBeliefs)
                foreach (string tension in CAFactionStructureModel.Tensions(
                    new CAPoliticalBeliefs { positions = source.Positions },
                    input.Institutions))
                    result.Add(source.Share + "% " + source.Label + ": "
                        + tension);
            return result;
        }

        private static string OptionLabel(string axisKey, string optionKey)
        {
            if (optionKey.NullOrEmpty()) return "not recorded";
            CAAxisDef axis = CAFactionAxes.AxisDef(axisKey);
            return axis?.Options.FirstOrDefault(item =>
                    item.Key == optionKey)?.Label
                ?? optionKey;
        }

        private static string AxisSetWords(List<CAAxisEntry> axes)
        {
            string[] words = (axes ?? new List<CAAxisEntry>())
                .Where(item => item != null
                    && item.source != (byte)CAAxisSource.Unset)
                .OrderBy(item => item.axisKey)
                .Select(item => (CAFactionAxes.AxisDef(item.axisKey)?.Label
                        ?? item.axisKey) + " — "
                    + OptionLabel(item.axisKey, item.optionKey)).ToArray();
            return words.Length == 0 ? "not recorded"
                : string.Join("; ", words);
        }

        private static string WeightedBeliefWords(
            List<WeightedBeliefSource> sources)
        {
            string[] words = (sources ?? new List<WeightedBeliefSource>())
                .OrderByDescending(item => item.Share)
                .ThenBy(item => item.Label)
                .Select(item => item.Label + " (" + item.Share + "%): "
                    + AxisSetWords(item.Positions)).ToArray();
            return words.Length == 0 ? "the shared founding beliefs"
                : string.Join("; ", words);
        }

        private static string WeightedIdeoligionWords(
            List<WeightedIdeoligionSource> sources)
        {
            string[] words = (sources
                    ?? new List<WeightedIdeoligionSource>())
                .OrderByDescending(item => item.Share)
                .ThenBy(item => item.Label)
                .Select(item => item.Label + " (" + item.Share + "%): "
                    + (item.Name ?? "Ideoligion not recorded"))
                .ToArray();
            return words.Length == 0 ? "the shared founding Ideoligion"
                : string.Join("; ", words);
        }

        private static string PopulationCultureSummary(CACulture culture,
            List<CASettlementPopulationGroup> populations)
        {
            CACultureConstituent[] sources = (culture?.constituents
                    ?? new List<CACultureConstituent>())
                .Where(item => item != null && item.share > 0)
                .OrderByDescending(item => item.share)
                .ThenBy(item => item.label).ToArray();
            if (sources.Length > 0)
                return string.Join("; ", sources.Select(item =>
                    (item.label ?? item.cultureId ?? "Culture not recorded")
                    + " (" + item.share + "%"
                    + (item.separateQuarter ? ", separate quarter" : "")
                    + ")"));
            return populations.Count == 0
                ? "No constituent population history is recorded."
                : populations.Count + " resident population group"
                    + (populations.Count == 1 ? " is" : "s are")
                    + " recorded; inherited Culture identities are not yet "
                    + "resolved.";
        }

        private static string IdeoligionCommitmentWords(Inputs input)
        {
            string[] communities = (input.PopulationIdeoligions
                    ?? new List<WeightedIdeoligionSource>())
                .OrderByDescending(item => item.Share)
                .ThenBy(item => item.Label)
                .Select(item => item.Label + " (" + item.Share + "%): "
                    + item.Name + " — "
                    + (item.CommitmentLabels.Count == 0
                        ? "no specific commitment recorded"
                        : string.Join(", ", item.CommitmentLabels)))
                .ToArray();
            if (communities.Length > 0) return string.Join("; ", communities);
            return input.IdeoligionCommitmentLabels.Count == 0
                ? "no specific commitment recorded"
                : string.Join(", ", input.IdeoligionCommitmentLabels);
        }

        private static string FoundingWords(CAFoundingArrangement terms)
        {
            if (terms == null) return "not yet chosen";
            return (terms.leaderRule == "chosen"
                    ? "one founder decides" : "founders decide together")
                + ", " + (terms.workRequired
                    ? "work may be assigned" : "work is voluntary")
                + ", " + (terms.foundersDecide
                    ? "every founder votes" : "founders do not all vote")
                + ", and " + (terms.sharedSupplies
                    ? "supplies are pooled" : "supplies remain separate");
        }

        private static string PopulationWords(
            List<CASettlementPopulationGroup> populations)
        {
            string[] words = populations.Select(item =>
                (item.label.NullOrEmpty()
                    ? "Population group " + item.key : item.label)
                + " (" + item.share + "%, "
                + (item.independentIdeoligionKey >= 0
                    ? "independent Ideoligion"
                    : "faction Ideoligion") + ", "
                + item.CertaintyWords.ToLowerInvariant()
                + (item.quarter ? ", separate quarters" : "") + ")")
                .ToArray();
            return words.Length == 0 ? "not recorded"
                : string.Join("; ", words);
        }

        private static string ProvisionWords(
            List<CAStartingProvision> provisions)
        {
            string[] words = (provisions ?? new List<CAStartingProvision>())
                .Where(item => item != null)
                .OrderBy(item => item.basisKey)
                .Select(item => (item.basisLabel.NullOrEmpty()
                        ? item.basisKey ?? "provision" : item.basisLabel)
                    + ": " + (item.active ? item.OperatorWords
                        : "inactive — " + (item.inactiveReason
                            ?? "current conditions do not support it")) + ", "
                    + item.access.ToString().ToLowerInvariant() + " access, "
                    + item.funding.ToString().ToLowerInvariant() + " funding, "
                    + item.distribution.ToString().ToLowerInvariant()
                    + " distribution, " + item.nodes + " node"
                    + (item.nodes == 1 ? "" : "s") + ", "
                    + item.reach.ToString().ToLowerInvariant() + " reach"
                    + (item.waterSecured ? "" : ", water constrained"))
                .ToArray();
            return words.Length == 0 ? "none recorded"
                : string.Join("; ", words);
        }

        private static string FacilityWords(int mask)
        {
            if (mask < 0) return "not yet realized";
            string[] labels = CAStartingFacilityCatalog.All
                .Where(item => (mask & item.Bit) != 0)
                .Select(item => item.Label).ToArray();
            return labels.Length == 0 ? "none"
                : string.Join(", ", labels);
        }

        private static string GeographyWords(Inputs input)
        {
            string[] links = new[] { input.Road ? "road" : null,
                input.River ? "river" : null, input.Coast ? "coast" : null }
                .Where(item => item != null).ToArray();
            return links.Length == 0 ? "no major route recorded"
                : string.Join(", ", links);
        }

        private static string AxisFingerprint(List<CAAxisEntry> axes)
        {
            return string.Join(",", (axes ?? new List<CAAxisEntry>())
                .Where(item => item != null).OrderBy(item => item.axisKey)
                .Select(item => item.axisKey + "=" + item.optionKey).ToArray());
        }

        private static string WeightedBeliefFingerprint(
            List<WeightedBeliefSource> sources)
        {
            return string.Join(",", (sources
                    ?? new List<WeightedBeliefSource>())
                .OrderByDescending(item => item.Share)
                .ThenBy(item => item.Identity)
                .Select(item => item.Share + "%:" + item.Identity + "["
                    + AxisFingerprint(item.Positions) + "]").ToArray());
        }

        private static string IdeoligionCommitmentFingerprint(Inputs input)
        {
            string shared = string.Join(",", input.IdeoligionCommitmentKeys
                ?? new List<string>());
            string populations = string.Join(",",
                (input.PopulationIdeoligions
                    ?? new List<WeightedIdeoligionSource>())
                .OrderByDescending(item => item.Share)
                .ThenBy(item => item.Identity)
                .Select(item => item.Share + "%:" + item.Identity + "["
                    + string.Join(";", item.CommitmentKeys) + "]"));
            return shared + (populations.Length == 0 ? "" : "|" + populations);
        }

        private static string FoundingFingerprint(CAFoundingArrangement terms)
        {
            if (terms == null) return "not adopted";
            return "leader=" + terms.leaderRule + ",work=" + terms.workRequired
                + ",voice=" + terms.foundersDecide + ",shared="
                + terms.sharedSupplies;
        }

        private static string PopulationFingerprint(
            List<CASettlementPopulationGroup> populations)
        {
            return string.Join(",", populations.Select(item => item.share + "%:"
                + BeliefSource(item) + ":" + IdeoligionSource(item)
                + ":certainty=" + item.ideoligionCertainty
                + (item.quarter ? ":quarter" : "")).ToArray());
        }

        private static string BeliefSource(CASettlementPopulationGroup group)
        {
            return !group.politicalBeliefsId.NullOrEmpty()
                ? group.politicalBeliefsId
                : "faction:" + (group.politicalBeliefsFactionKey >= 0
                    ? group.politicalBeliefsFactionKey : group.factionKey);
        }

        private static string IdeoligionSource(CASettlementPopulationGroup group)
        {
            return group.independentIdeoligionKey >= 0
                ? "independent:" + group.independentIdeoligionKey
                : "faction:" + (group.ideoligionFactionKey >= 0
                    ? group.ideoligionFactionKey : group.factionKey);
        }

        private static string ProvisionFingerprint(
            List<CAStartingProvision> provisions)
        {
            return string.Join(",", (provisions ?? new List<CAStartingProvision>())
                .Where(item => item != null)
                .OrderBy(item => item.basisKey)
                .Select(item => item.key + ":" + item.basisKey + "="
                    + item.operatorKind + "/group=" + item.populationGroupKey
                    + "/" + item.access + "/" + item.funding + "/"
                    + item.distribution + "/active=" + item.active
                    + "/reason=" + (item.inactiveReason ?? "none")
                    + "/water=" + item.waterSecured + "/nodes=" + item.nodes
                    + "/reach=" + item.reach).ToArray());
        }

        private static int AverageKnown(params int[] values)
        {
            int[] known = values.Where(value => value >= 0).ToArray();
            return known.Length == 0 ? 2 : known.Sum() / known.Length;
        }

        private static string Signature(IEnumerable<string> facts)
        {
            return CACulturalExpressionCausalKernel.Signature(facts);
        }

        private static List<WeightedBeliefSource> PopulationBeliefs(
            CARegionalPlan plan, List<CASettlementPopulationGroup> groups,
            List<CAAxisEntry> fallback, bool includeWorldState)
        {
            var result = new List<WeightedBeliefSource>();
            foreach (CASettlementPopulationGroup group in groups
                ?? new List<CASettlementPopulationGroup>())
            {
                if (group == null || group.share <= 0) continue;
                CARegionalFactionPlan source = null;
                if (!group.politicalBeliefsId.NullOrEmpty())
                    source = plan?.factions?.FirstOrDefault(item =>
                        item?.politicalBeliefs?.id
                            == group.politicalBeliefsId);
                int factionKey = group.politicalBeliefsFactionKey >= 0
                    ? group.politicalBeliefsFactionKey : group.factionKey;
                if (source == null) source = plan?.FactionPlan(factionKey);
                List<CAAxisEntry> positions = CopyAxes(
                    source?.politicalBeliefs?.positions);
                if (includeWorldState && positions.Count == 0
                    && !group.politicalBeliefsId.NullOrEmpty())
                    positions = CopyAxes(CAFactionStateWorldComponent.Current
                        ?.FactionStates.FirstOrDefault(item =>
                            item?.politicalBeliefs?.id
                                == group.politicalBeliefsId)
                        ?.politicalBeliefs?.positions);
                if (positions.Count == 0) positions = CopyAxes(fallback);
                result.Add(new WeightedBeliefSource
                {
                    Identity = BeliefSource(group),
                    Label = group.label.NullOrEmpty()
                        ? "Population group " + group.key : group.label,
                    Share = group.share,
                    Positions = positions
                });
            }
            return result;
        }

        private static List<WeightedIdeoligionSource> PopulationIdeoligions(
            CARegionalPlan plan, List<CASettlementPopulationGroup> groups,
            Ideo fallback)
        {
            var result = new List<WeightedIdeoligionSource>();
            foreach (CASettlementPopulationGroup group in groups
                ?? new List<CASettlementPopulationGroup>())
            {
                if (group == null || group.share <= 0) continue;
                Ideo source = null;
                if (group.independentIdeoligionKey < 0)
                {
                    int factionKey = group.ideoligionFactionKey >= 0
                        ? group.ideoligionFactionKey : group.factionKey;
                    source = plan?.FactionPlan(factionKey)?.LivingIdeo;
                    if (source == null) source = fallback;
                }
                List<string> keys = source == null
                    ? new List<string>() : IdeoligionCommitmentKeys(source);
                List<string> labels = source == null
                    ? new List<string>() : IdeoligionCommitmentLabels(source);
                if (group.independentIdeoligionKey >= 0)
                {
                    keys.Add("independent:"
                        + group.independentIdeoligionKey);
                    labels.Add("separate local belief generated when the "
                        + "community is materialized");
                }
                result.Add(new WeightedIdeoligionSource
                {
                    Identity = IdeoligionSource(group),
                    Label = group.label.NullOrEmpty()
                        ? "Population group " + group.key : group.label,
                    Name = source?.name ?? (group.independentIdeoligionKey >= 0
                        ? "Independent Ideoligion" : "Ideoligion not recorded"),
                    Share = group.share,
                    CommitmentKeys = keys,
                    CommitmentLabels = labels
                });
            }
            return result;
        }

        private static List<WeightedIdeoligionSource>
            MaterializedIdeoligions(CARegionalSettlementRecord record,
                Map map)
        {
            if (record == null || map == null) return null;
            List<Pawn> residents = CAPopulationProjection.Residents(record,
                map).Where(item => item != null).ToList();
            if (residents.Count == 0) return null;
            var grouped = residents.GroupBy(item => item.Ideo)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key?.id ?? -1).ToList();
            var result = new List<WeightedIdeoligionSource>();
            int assigned = 0;
            for (int index = 0; index < grouped.Count; index++)
            {
                IGrouping<Ideo, Pawn> group = grouped[index];
                Ideo ideo = group.Key;
                int share = index == grouped.Count - 1 ? 100 - assigned
                    : (int)Math.Round(group.Count() * 100d / residents.Count,
                        MidpointRounding.AwayFromZero);
                assigned += share;
                result.Add(new WeightedIdeoligionSource
                {
                    Identity = ideo == null ? "none" : "ideo:" + ideo.id,
                    Label = ideo == null ? "Residents without an Ideoligion"
                        : "Residents following " + ideo.name,
                    Name = ideo?.name ?? "No Ideoligion",
                    Share = share,
                    CommitmentKeys = IdeoligionCommitmentKeys(ideo),
                    CommitmentLabels = IdeoligionCommitmentLabels(ideo)
                });
            }
            return result;
        }

        private static List<string> IdeoligionCommitmentKeys(Ideo ideo)
        {
            if (ideo == null) return new List<string>();
            return (ideo.memes ?? new List<MemeDef>())
                .Where(item => item != null)
                .Select(item => "meme:" + item.defName)
                .Concat((ideo.PreceptsListForReading
                        ?? new List<Precept>())
                    .Where(item => item?.def != null)
                    .Select(item => "precept:" + item.def.defName))
                .Distinct().OrderBy(item => item).ToList();
        }

        private static List<string> IdeoligionCommitmentLabels(Ideo ideo)
        {
            if (ideo == null) return new List<string>();
            return (ideo.memes ?? new List<MemeDef>())
                .Where(item => item != null)
                .Select(item => item.label)
                .Concat((ideo.PreceptsListForReading
                        ?? new List<Precept>())
                    .Where(item => item != null && !item.Label.NullOrEmpty())
                    .Select(item => item.Label))
                .Where(item => !item.NullOrEmpty())
                .Distinct().OrderBy(item => item).ToList();
        }

        private static void CountRelations(CARegionalPlan plan,
            int factionKey, ref int hostile, ref int neutral)
        {
            foreach (CARegionalRelationPlan relation in plan?.relations
                ?? new List<CARegionalRelationPlan>())
            {
                if (relation == null || (relation.leftFactionKey != factionKey
                        && relation.rightFactionKey != factionKey)) continue;
                if (relation.relation == FactionRelationKind.Hostile) hostile++;
                else if (relation.relation == FactionRelationKind.Neutral)
                    neutral++;
            }
        }

        private static List<CAAxisEntry> CopyAxes(List<CAAxisEntry> source)
        {
            return (source ?? new List<CAAxisEntry>()).Where(item => item != null)
                .Select(item => new CAAxisEntry
                {
                    axisKey = item.axisKey,
                    optionKey = item.optionKey,
                    source = item.source
                }).ToList();
        }

        private static List<CASettlementPopulationGroup> CopyPopulations(
            List<CASettlementPopulationGroup> source)
        {
            return (source ?? new List<CASettlementPopulationGroup>())
                .Where(item => item != null).Select(item => new
                    CASettlementPopulationGroup
                    {
                        key = item.key,
                        kind = item.kind,
                        label = item.label,
                        share = item.share,
                        factionKey = item.factionKey,
                        politicalBeliefsFactionKey =
                            item.politicalBeliefsFactionKey,
                        politicalBeliefsId = item.politicalBeliefsId,
                        ideoligionCertainty = item.ideoligionCertainty,
                        ideoligionFactionKey = item.ideoligionFactionKey,
                        independentIdeoligionKey =
                            item.independentIdeoligionKey,
                        quarter = item.quarter
                    }).ToList();
        }

        private static List<CAStartingProvision> CopyProvisions(
            List<CAStartingProvision> source)
        {
            return (source ?? new List<CAStartingProvision>())
                .Where(item => item != null).Select(item => new CAStartingProvision
                {
                    key = item.key,
                    basisKey = item.basisKey,
                    basisLabel = item.basisLabel,
                    operatorKind = item.operatorKind,
                    populationGroupKey = item.populationGroupKey,
                    access = item.access,
                    funding = item.funding,
                    distribution = item.distribution,
                    active = item.active,
                    inactiveReason = item.inactiveReason,
                    waterSecured = item.waterSecured,
                    nodes = item.nodes,
                    reach = item.reach
                }).ToList();
        }

        private static CACulturalExpression Missing(string reason)
        {
            return new CACulturalExpression
            {
                Summary = reason,
                Status = CACulturalExpressionStatus.Constrained,
                SourceSignature = "00000000"
            };
        }
    }
}
