using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // One realized part of an established settlement. The key is namespaced
    // and open-ended; consumers never infer a fixed enum position from it.
    public sealed class CASettlementProgramEntry : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string programKey;
        public string scope;
        public string sourceReceipt;
        public List<string> selectedCandidates = new List<string>();
        public int count = 1;
        public int extent = 1;
        public string materializationState = "pending";
        public List<string> placedThingIds = new List<string>();
        public string blocker;
        public string fallback;
        public string signature;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref scope, "scope");
            Scribe_Values.Look(ref sourceReceipt, "sourceReceipt");
            Scribe_Collections.Look(ref selectedCandidates,
                "selectedCandidates", LookMode.Value);
            Scribe_Values.Look(ref count, "count", 1);
            Scribe_Values.Look(ref extent, "extent", 1);
            Scribe_Values.Look(ref materializationState,
                "materializationState", "pending");
            Scribe_Collections.Look(ref placedThingIds,
                "placedThingIds", LookMode.Value);
            Scribe_Values.Look(ref blocker, "blocker");
            Scribe_Values.Look(ref fallback, "fallback");
            Scribe_Values.Look(ref signature, "signature");
            if (selectedCandidates == null)
                selectedCandidates = new List<string>();
            if (placedThingIds == null)
                placedThingIds = new List<string>();
        }

        internal CASettlementProgramEntry Copy()
        {
            return new CASettlementProgramEntry
            {
                schemaVersion = schemaVersion,
                programKey = programKey,
                scope = scope,
                sourceReceipt = sourceReceipt,
                selectedCandidates = (selectedCandidates
                    ?? new List<string>()).ToList(),
                count = count,
                extent = extent,
                materializationState = materializationState,
                placedThingIds = (placedThingIds
                    ?? new List<string>()).ToList(),
                blocker = blocker,
                fallback = fallback,
                signature = signature
            };
        }
    }

    // The realized program is written once after its upstream facts and
    // provision arrangements are settled. Preview and materialization consume
    // this same object; neither rerolls candidate assets.
    public sealed class CASettlementProgram : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string sourceSignature;
        public List<CASettlementProgramEntry> entries =
            new List<CASettlementProgramEntry>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref sourceSignature, "sourceSignature");
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            if (entries == null) entries = new List<CASettlementProgramEntry>();
        }

        internal CASettlementProgramEntry Entry(string key)
        {
            return entries?.FirstOrDefault(item => item != null
                && item.programKey == key);
        }

        internal bool Has(string key)
        {
            CASettlementProgramEntry entry = Entry(key);
            return entry != null && entry.blocker.NullOrEmpty();
        }

        internal CASettlementProgram Copy()
        {
            return new CASettlementProgram
            {
                schemaVersion = schemaVersion,
                sourceSignature = sourceSignature,
                entries = (entries ?? new List<CASettlementProgramEntry>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList()
            };
        }
    }

    public sealed class CASettlementProgramDef
    {
        public string Key;
        public string Label;
        public string Domain;
        public string OwningModule;
        public string SourceFacts;
        public string Applicability;
        public string CandidateResolution;
        public string Materialization;
        public string Summary;
        public string Consumer;
        public string Fallback;
        public Func<CARegionalPlan, CARegionalSettlementPlan, bool> Applies;
        public Func<CARegionalPlan, CARegionalSettlementPlan, string>
            InapplicableReason;
        // Each returned array is one required asset role. Members of an array
        // are equivalent loaded candidates for that role.
        public Func<CARegionalPlan, CARegionalSettlementPlan,
            IEnumerable<string[]>> CandidateGroups;
        public Func<CARegionalPlan, CARegionalSettlementPlan, int> Count;
        public Func<CARegionalPlan, CARegionalSettlementPlan, int> Extent;
        public bool NativeSpatialContract;
        public bool MaterializeSpatialContract;
        public int MinimumFunctionalRoles;
        public Func<CARegionalPlan, CARegionalSettlementPlan, bool>
            SpatiallyRealized;
        public Func<CARegionalSettlementRecord, Map,
            CASettlementProgramEntry, bool> SpatialMaterializer;
        // Functional programs own a positive runtime contract. Registered
        // programs provide this validator instead of being forced into the
        // built-in program-key table.
        public Func<ThingDef, bool> FunctionalContract;
    }

    internal sealed class CASettlementProgramAvailability
    {
        internal CASettlementProgramDef Definition;
        internal string Reason;
        internal List<string> Candidates = new List<string>();
    }

    // Registry entries describe functional or native spatial contracts. A
    // decorative object can accompany a program, but cannot make one valid.
    public static class CASettlementProgramRegistry
    {
        public const string Housing = CASettlementProgramCausalKernel.Housing;
        public const string FoodPreparation =
            CASettlementProgramCausalKernel.FoodPreparation;
        public const string Storage = CASettlementProgramCausalKernel.Storage;
        public const string Medicine = CASettlementProgramCausalKernel.Medicine;
        public const string Production = CASettlementProgramCausalKernel.Production;
        public const string SpecializedIndustry =
            CASettlementProgramCausalKernel.SpecializedIndustry;
        public const string Trade = CASettlementProgramCausalKernel.Trade;
        public const string Governance = CASettlementProgramCausalKernel.Governance;
        public const string Custody = CASettlementProgramCausalKernel.Custody;
        public const string Defense = CASettlementProgramCausalKernel.Defense;
        public const string Research = CASettlementProgramCausalKernel.Research;
        public const string Religion = CASettlementProgramCausalKernel.Religion;
        public const string Gathering = CASettlementProgramCausalKernel.Gathering;
        public const string Recreation = CASettlementProgramCausalKernel.Recreation;
        public const string ArtAndMemory = CASettlementProgramCausalKernel.ArtAndMemory;
        public const string Agriculture = CASettlementProgramCausalKernel.Agriculture;
        public const string Animals = CASettlementProgramCausalKernel.Animals;
        public const string Communications =
            CASettlementProgramCausalKernel.Communications;
        public const string Transport = CASettlementProgramCausalKernel.Transport;
        public const string CommunalProvision =
            CASettlementProgramCausalKernel.CommunalProvision;
        public const string AuthorityProvision =
            CASettlementProgramCausalKernel.AuthorityProvision;
        public const string HouseholdProvision =
            CASettlementProgramCausalKernel.HouseholdProvision;

        private static readonly List<CASettlementProgramDef> Definitions =
            BuildDefinitions();

        public static IReadOnlyList<CASettlementProgramDef> All =>
            Definitions;

        public static CASettlementProgramDef Find(string key)
        {
            return Definitions.FirstOrDefault(def => def.Key == key);
        }

        // Content modules may register a program without editing this table.
        // Registration requires a complete causal contract and either
        // functional candidate resolution or a native spatial contract.
        public static bool Register(CASettlementProgramDef definition,
            out string failure)
        {
            failure = null;
            if (definition == null) failure = "definition is null";
            else if (definition.Key.NullOrEmpty()
                || !definition.Key.Contains("."))
                failure = "program key must be stable and namespaced";
            else if (Definitions.Any(item => item.Key == definition.Key))
                failure = "program key is already registered";
            else if (definition.Label.NullOrEmpty()
                || definition.Domain.NullOrEmpty()
                || definition.OwningModule.NullOrEmpty()
                || definition.SourceFacts.NullOrEmpty()
                || definition.Applicability.NullOrEmpty()
                || definition.CandidateResolution.NullOrEmpty()
                || definition.Materialization.NullOrEmpty()
                || definition.Summary.NullOrEmpty()
                || definition.Consumer.NullOrEmpty()
                || definition.Fallback.NullOrEmpty())
                failure = "program contract is incomplete";
            else if (definition.Applies == null
                || definition.InapplicableReason == null)
                failure = "program applicability contract is incomplete";
            else if (!definition.NativeSpatialContract
                && definition.CandidateGroups == null)
                failure = "program has no functional candidate contract";
            else if (!definition.NativeSpatialContract
                && definition.MinimumFunctionalRoles < 1)
                failure = "program has no declared functional candidate roles";
            else if (!definition.NativeSpatialContract
                && definition.FunctionalContract == null)
                failure = "program has no functional asset validator";
            else if (definition.NativeSpatialContract
                && definition.SpatiallyRealized == null)
                failure = "program has no native spatial realization contract";
            else if (definition.MaterializeSpatialContract
                && !definition.NativeSpatialContract)
                failure = "materialized spatial programs require a native spatial contract";
            else if (definition.MaterializeSpatialContract
                && definition.SpatialMaterializer == null)
                failure = "materialized spatial program has no executable materializer";
            if (failure != null) return false;
            Definitions.Add(definition);
            return true;
        }

        internal static bool EnsureDerived(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, bool force = false)
        {
            if (plan == null || settlement == null) return false;
            CASettlementProgram expected = Derive(plan, settlement);
            if (!force && ProgramsEquivalent(settlement.settlementProgram,
                    expected)) return false;
            settlement.settlementProgram = expected;
            return true;
        }

        // Confirmed plans never heal or regenerate here. They prove that the
        // saved program is the exact structural result of the saved causes and
        // current loaded functional contracts, or fail before generation.
        internal static bool TryValidateSaved(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, out string failure)
        {
            failure = null;
            if (plan == null || settlement == null)
            {
                failure = "the settlement or regional plan is missing";
                return false;
            }
            CASettlementProgram expected = Derive(plan, settlement);
            if (ProgramsEquivalent(settlement.settlementProgram, expected))
                return true;
            failure = "the saved settlement program does not match its "
                + "population, ground, access, order, economy, Culture, "
                + "relations, provision arrangements, and loaded assets";
            return false;
        }

        private static CASettlementProgram Derive(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            CASettlementProgramFacts facts = BuildFacts(plan, settlement);
            string source = CASettlementProgramCausalKernel
                .SourceSignature(facts);
            var realized = new CASettlementProgram { sourceSignature = source };
            List<CASettlementProgramCausalSpec> causalSpecs =
                CASettlementProgramCausalKernel.Derive(facts);
            var nativeKeys = new HashSet<string>(causalSpecs.Select(item =>
                item.Key), StringComparer.Ordinal);
            var registeredSpecs = new List<CASettlementProgramCausalSpec>();
            foreach (CASettlementProgramDef definition in Definitions
                .Where(item => item != null
                    && !nativeKeys.Contains(item.Key)
                    && item.Applies?.Invoke(plan, settlement) == true)
                .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                registeredSpecs.Add(new CASettlementProgramCausalSpec
                {
                    Key = definition.Key,
                    Scope = "settlement",
                    CandidateGroups = (definition.CandidateGroups
                            ?.Invoke(plan, settlement)
                            ?? Enumerable.Empty<string[]>())
                        .Select(group => (group ?? Array.Empty<string>())
                            .ToArray()).ToList(),
                    Count = Math.Max(1, definition.Count?.Invoke(plan,
                        settlement) ?? 1),
                    Extent = Math.Max(1, definition.Extent?.Invoke(plan,
                        settlement) ?? 1),
                    NativeSpatialContract = definition.NativeSpatialContract,
                    MaterializeSpatialContract =
                        definition.MaterializeSpatialContract
                });
            }
            if (registeredSpecs.Count > 0)
            {
                string registered = string.Join("|", registeredSpecs.Select(
                    item => item.Key + ":" + item.Count + ":" + item.Extent
                        + ":" + string.Join(",", item.CandidateGroups
                            .SelectMany(group => group))));
                source += "-" + unchecked((uint)
                    CASettlementProgramCausalKernel.StableStringHash(
                        registered)).ToString("X8");
                realized.sourceSignature = source;
                causalSpecs.AddRange(registeredSpecs);
            }
            foreach (CASettlementProgramCausalSpec causal in causalSpecs)
            {
                CASettlementProgramDef definition = Find(causal.Key);
                if (definition == null) continue;
                var entry = new CASettlementProgramEntry
                {
                    programKey = causal.Key,
                    scope = causal.Scope,
                    sourceReceipt = definition.SourceFacts + " ["
                        + source + "]",
                    count = causal.Count,
                    extent = causal.Extent,
                    fallback = definition.Fallback
                };

                if (causal.NativeSpatialContract)
                {
                    bool present = definition.SpatiallyRealized
                        ?.Invoke(plan, settlement) == true;
                    entry.materializationState = !present ? "blocked"
                        : causal.MaterializeSpatialContract
                            ? "pending" : "present in saved geography";
                    if (!present)
                        entry.blocker = "the required route or geographic "
                            + "contract is absent from this settlement area";
                }
                else
                {
                    int groupIndex = 0;
                    foreach (string[] group in causal.CandidateGroups)
                    {
                        List<string> loaded = LoadedFunctionalCandidates(
                            definition, group);
                        if (loaded.Count == 0)
                        {
                            entry.blocker = "no loaded functional asset "
                                + "satisfies " + definition.Label;
                            entry.materializationState = "blocked";
                            break;
                        }
                        int choice = CASettlementProgramCausalKernel
                            .StableCandidateIndex(plan.candidateId,
                                settlement.slot, causal.Key, groupIndex++,
                                loaded.Count);
                        entry.selectedCandidates.Add(loaded[choice]);
                    }
                    if (groupIndex < definition.MinimumFunctionalRoles
                        && entry.blocker.NullOrEmpty())
                        entry.blocker = "the program did not supply its declared "
                            + "functional candidate roles";
                    if (entry.blocker.NullOrEmpty())
                        entry.materializationState = "pending";
                }
                entry.signature = CASettlementProgramCausalKernel
                    .EntrySignature(source, entry.programKey, entry.scope,
                        entry.count, entry.extent,
                        entry.selectedCandidates, entry.blocker);
                realized.entries.Add(entry);
            }
            return realized;
        }

        internal static IEnumerable<CASettlementProgramAvailability>
            SupportedAlternatives(CARegionalPlan plan,
                CARegionalSettlementPlan settlement)
        {
            if (plan == null || settlement == null) yield break;
            foreach (CASettlementProgramEntry current in settlement
                .settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>())
            {
                if (current == null || !current.blocker.NullOrEmpty())
                    continue;
                CASettlementProgramDef definition = Find(
                    current.programKey);
                if (definition == null || definition.NativeSpatialContract)
                    continue;
                var alternatives = new List<string>();
                foreach (string[] group in definition.CandidateGroups
                    ?.Invoke(plan, settlement)
                    ?? Enumerable.Empty<string[]>())
                    alternatives.AddRange(LoadedFunctionalCandidates(
                        definition, group));
                alternatives = alternatives.Distinct()
                    .Except(current.selectedCandidates
                        ?? new List<string>()).OrderBy(item => item,
                            StringComparer.Ordinal).ToList();
                if (alternatives.Count == 0) continue;
                yield return new CASettlementProgramAvailability
                {
                    Definition = definition,
                    Candidates = alternatives
                };
            }
        }

        internal static IEnumerable<CASettlementProgramAvailability>
            Unavailable(CARegionalPlan plan,
                CARegionalSettlementPlan settlement)
        {
            if (plan == null || settlement == null) yield break;
            var present = new HashSet<string>((settlement.settlementProgram
                    ?.entries ?? new List<CASettlementProgramEntry>())
                .Where(entry => entry != null)
                .Select(entry => entry.programKey), StringComparer.Ordinal);
            CASettlementProgram expected = Derive(plan, settlement);
            var applicable = new HashSet<string>((expected.entries
                    ?? new List<CASettlementProgramEntry>())
                .Where(entry => entry != null)
                .Select(entry => entry.programKey), StringComparer.Ordinal);
            foreach (CASettlementProgramDef definition in Definitions)
            {
                CASettlementProgramEntry current = settlement
                    .settlementProgram?.Entry(definition.Key);
                if (current != null && current.blocker.NullOrEmpty()) continue;
                if (!applicable.Contains(definition.Key)
                    && present.Contains(definition.Key)) continue;
                yield return new CASettlementProgramAvailability
                {
                    Definition = definition,
                    Reason = !current?.blocker.NullOrEmpty() == true
                        ? current.blocker
                        : "the settlement's saved facts do not support this program"
                };
            }
        }

        internal static string Summary(CARegionalSettlementPlan settlement)
        {
            List<CASettlementProgramEntry> entries = settlement
                ?.settlementProgram?.entries?.Where(entry => entry != null
                    && entry.blocker.NullOrEmpty()).ToList()
                ?? new List<CASettlementProgramEntry>();
            if (entries.Count == 0) return "No settlement composition saved.";
            string[] domains = entries.Select(entry => Find(entry.programKey)
                    ?.Domain).Where(domain => !domain.NullOrEmpty()).Distinct()
                .OrderBy(domain => domain, StringComparer.Ordinal).ToArray();
            string[] leading = entries.Select(entry => Find(entry.programKey)
                    ?.Label ?? "Saved program").Take(4)
                .ToArray();
            return entries.Count + " starting programs: "
                + string.Join(", ", leading)
                + (entries.Count > leading.Length ? ", and "
                    + (entries.Count - leading.Length) + " more" : "")
                + ". Covers " + string.Join(", ", domains) + ".";
        }

        internal static bool Has(CARegionalSettlementPlan settlement,
            string key)
        {
            return settlement?.settlementProgram?.Has(key) == true;
        }

        internal static bool Has(CARegionalSettlementRecord settlement,
            string key)
        {
            return settlement?.settlementProgram?.Has(key) == true;
        }

        internal static string ProgramKeyFor(CAProvisionOperator kind)
        {
            return kind switch
            {
                CAProvisionOperator.Communal => CommunalProvision,
                CAProvisionOperator.Authority => AuthorityProvision,
                CAProvisionOperator.Household => HouseholdProvision,
                _ => null
            };
        }

        internal static List<string> LoadedFunctionalCandidates(
            CASettlementProgramDef definition, IEnumerable<string> names)
        {
            var result = new List<string>();
            foreach (string name in names ?? Enumerable.Empty<string>())
            {
                ThingDef def = DefDatabase<ThingDef>
                    .GetNamedSilentFail(name);
                if (def == null || def.category != ThingCategory.Building
                    || !def.BuildableByPlayer || def.blueprintDef == null
                    || !SupportsFunctionalContract(definition, def))
                    continue;
                result.Add(def.defName);
            }
            return result.Distinct().ToList();
        }

        private static bool SupportsFunctionalContract(
            CASettlementProgramDef definition, ThingDef def)
        {
            if (definition == null || def?.thingClass == null) return false;
            if (definition.FunctionalContract != null)
                return definition.FunctionalContract(def);
            string key = definition.Key;
            Type type = def.thingClass;
            bool bed = typeof(Building_Bed).IsAssignableFrom(type);
            bool workTable = typeof(Building_WorkTable).IsAssignableFrom(type);
            bool storage = typeof(Building_Storage).IsAssignableFrom(type);
            if (key == Housing || key == Medicine || key == Custody)
                return bed;
            if (key == Production || key == SpecializedIndustry
                || key == FoodPreparation || key == HouseholdProvision)
                return workTable || def.defName == "Campfire";
            if (key == Storage || key == AuthorityProvision)
                return storage;
            if (key == Research)
                return workTable && def.defName.Contains("ResearchBench");
            if (key == Animals)
                return bed && (def.defName.Contains("Animal")
                    || def.defName.Contains("SleepingBox"));
            if (key == Defense)
                return def.fillPercent >= 0.2f;
            if (key == Recreation)
                return def.defName == "ChessTable"
                    || def.defName == "GameOfUrBoard"
                    || def.defName == "HorseshoesPin";
            if (key == ArtAndMemory)
                return def.comps?.Any(comp => comp.compClass
                    == typeof(CompArt)) == true;
            if (key == Religion)
                return def.defName == "RitualSpot"
                    || def.defName == "Ideogram";
            if (key == Trade || key == Communications)
                return def.defName == "CommsConsole"
                    || def.defName == "OrbitalTradeBeacon"
                    || def.defName == "WoodFiredGenerator";
            if (key == Governance || key == Gathering)
                return def.HasComp(typeof(CompGatherSpot))
                    || def.surfaceType == SurfaceType.Eat
                    || def.building?.isSittable == true;
            if (key == CommunalProvision)
                return workTable || storage || def.defName == "Campfire"
                    || def.HasComp(typeof(CompGatherSpot))
                    || def.surfaceType == SurfaceType.Eat
                    || def.building?.isSittable == true;
            return false;
        }

        private static List<CASettlementProgramDef> BuildDefinitions()
        {
            bool Populated(CARegionalPlan p, CARegionalSettlementPlan s) =>
                s.residentPopulation > 0;
            bool TierAtLeast(CARegionalPlan p, CARegionalSettlementPlan s,
                int tier) => CASettlementAxes.Tier(
                    CASettlementAxes.TemplateEraPrior(
                        p?.FactionPlan(s.factionKey)?.ResolvedFactionDef))
                    >= tier;
            string Axis(CARegionalPlan p, CARegionalSettlementPlan s,
                string key)
            {
                CARegionalFactionPlan faction = p?.FactionPlan(s.factionKey);
                return CAFactionAxes.KeyOf(faction?.factionStructure, key)
                    ?? CAFactionAxes.KeyOf(
                        faction?.politicalBeliefs?.positions, key);
            }
            bool HasProvision(CARegionalSettlementPlan settlement,
                CAProvisionOperator kind) => (settlement
                    .provisionArrangements
                    ?? new List<CAProvisionArrangement>()).Any(item =>
                        item != null && item.active
                        && item.operatorKind == kind);

            var result = new List<CASettlementProgramDef>();
            void Add(string key, string label, string domain,
                string source, string applicability, string resolution,
                string materialization, string summary, string consumer,
                string fallback,
                Func<CARegionalPlan, CARegionalSettlementPlan, bool> applies,
                Func<CARegionalPlan, CARegionalSettlementPlan, string> reason,
                Func<CARegionalPlan, CARegionalSettlementPlan,
                    IEnumerable<string[]>> candidates,
                Func<CARegionalPlan, CARegionalSettlementPlan, int> count = null,
                Func<CARegionalPlan, CARegionalSettlementPlan, int> extent = null,
                bool spatial = false,
                bool materializeSpatial = false,
                Func<CARegionalPlan, CARegionalSettlementPlan, bool>
                    spatiallyRealized = null,
                Func<ThingDef, bool> functionalContract = null,
                Func<CARegionalSettlementRecord, Map,
                    CASettlementProgramEntry, bool> spatialMaterializer = null)
            {
                result.Add(new CASettlementProgramDef
                {
                    Key = key, Label = label, Domain = domain,
                    OwningModule = "SettlementProgramModule",
                    SourceFacts = source, Applicability = applicability,
                    CandidateResolution = resolution,
                    Materialization = materialization, Summary = summary,
                    Consumer = consumer, Fallback = fallback,
                    Applies = applies, InapplicableReason = reason,
                    CandidateGroups = candidates, Count = count,
                    Extent = extent, NativeSpatialContract = spatial,
                    MinimumFunctionalRoles = spatial ? 0 : 1,
                    MaterializeSpatialContract = materializeSpatial,
                    SpatiallyRealized = spatiallyRealized,
                    FunctionalContract = functionalContract,
                    SpatialMaterializer = spatialMaterializer
                });
            }

            Add(Housing, "Housing", "Housing",
                "resident population, scale, and faction knowledge",
                "every populated established settlement",
                "loaded human bed definitions appropriate to faction knowledge",
                "beds placed in existing rooms and owned by the faction",
                "Sleeping places for the resident population.",
                "native rest and ownership", "bedroll or bed in the same program",
                Populated, (p, s) => "the settlement has no resident population",
                (p, s) => new[] { TierAtLeast(p, s, 1)
                    ? new[] { "Bed", "Bedroll" }
                    : new[] { "Bedroll", "Bed" } },
                (p, s) => Mathf.Clamp(2 + s.residentPopulation / 350, 2, 6));
            Add(FoodPreparation, "Food preparation", "Food",
                "resident population and faction knowledge",
                "every populated established settlement",
                "loaded stove or hearth plus a food-preparation table",
                "a working kitchen or hearth in an existing room",
                "A place to prepare the settlement's food.",
                "native cooking and butchery work", "campfire within the same program",
                Populated, (p, s) => "the settlement has no resident population",
                (p, s) => new[]
                {
                    TierAtLeast(p, s, 1)
                        ? new[] { "FueledStove", "Campfire" }
                        : new[] { "Campfire", "FueledStove" },
                    new[] { "TableButcher" }
                });
            Add(Storage, "Stores", "Storage",
                "resident population, access, services, and provision arrangements",
                "populated settlements that keep shared goods or reserves",
                "loaded native storage buildings with a real storage contract",
                "shelves and starting stock placed in an existing room",
                "Storage for food, medicine, and shared goods.",
                "native storage and provision consumers", "shelf in the same program",
                (p, s) => Populated(p, s) && (s.realizedAccessInfrastructure > 0
                    || s.realizedServiceInfrastructure > 0
                    || (s.provisionArrangements?.Any(item => item != null
                        && item.active) ?? false)),
                (p, s) => "no shared reserve, service, or access fact requires stores",
                (p, s) => new[] { new[] { "Shelf" } },
                (p, s) => Mathf.Clamp(1 + s.residentPopulation / 450, 1, 3));
            Add(Medicine, "Medical care", "Medicine",
                "resident population, services, history, and faction knowledge",
                "populated settlements with realized medical service",
                "loaded beds that support native medical designation",
                "medical beds and medicine placed together",
                "A room for treatment and recovery.",
                "native tending and medical rest", "bed or bedroll in the same program",
                (p, s) => Populated(p, s)
                    && (s.realizedServiceInfrastructure > 0
                        || s.historicalDevelopment > 0),
                (p, s) => "local service and historical state do not support dedicated care",
                (p, s) => new[] { TierAtLeast(p, s, 2)
                    ? new[] { "HospitalBed", "Bed", "Bedroll" }
                    : TierAtLeast(p, s, 1)
                        ? new[] { "Bed", "Bedroll" }
                        : new[] { "Bedroll", "Bed" } },
                (p, s) => s.realizedServiceInfrastructure >= 2 ? 2 : 1);
            Add(Production, "Production", "Production",
                "economic capacity, civic development, specialization, and knowledge",
                "settlements with local productive capacity",
                "loaded work tables with native bill contracts",
                "a work table placed in an existing room",
                "A place for local making and repair.",
                "native bill work and settlement repair", "crafting spot in the same program",
                (p, s) => Populated(p, s) && (s.economicCapacity > 0
                    || s.specialization > 0 || s.historicalDevelopment > 0),
                (p, s) => "the settlement has no realized productive capacity",
                (p, s) => new[] { TierAtLeast(p, s, 1)
                    ? new[] { "FueledSmithy", "CraftingSpot" }
                    : new[] { "CraftingSpot", "FueledSmithy" } });
            Add(SpecializedIndustry, "Specialized industry", "Production",
                "specialization, economic capacity, history, and knowledge",
                "specialized settlements with enough economy to sustain a trade",
                "loaded specialized native work tables",
                "a specialized work table placed in a production room",
                "A trade practiced beyond ordinary repair work.",
                "native bills and settlement economy", "another work table in the same program",
                (p, s) => s.specialization >= 2 && s.economicCapacity >= 2,
                (p, s) => "specialization or economic capacity is too low",
                (p, s) => new[] { TierAtLeast(p, s, 2)
                    ? new[] { "ElectricSmithy", "HandTailoringBench",
                        "FueledSmithy" }
                    : new[] { "HandTailoringBench", "FueledSmithy" } });
            Add(Trade, "Trade", "Trade",
                "trade connectivity, access, economic capacity, and knowledge",
                "industrial settlements with strong trade links",
                "loaded communications, power, and orbital trade assets",
                "a powered trade room and beacon area",
                "Facilities for long-distance trade.",
                "native orbital trading", "none outside this program",
                (p, s) => TierAtLeast(p, s, 2)
                    && s.tradeConnectivity >= 2 && s.economicCapacity >= 1,
                (p, s) => "trade links, economy, or faction knowledge are insufficient",
                (p, s) => new[]
                {
                    new[] { "WoodFiredGenerator" },
                    new[] { "CommsConsole" },
                    new[] { "OrbitalTradeBeacon" }
                });
            Add(Governance, "Meeting place", "Governance",
                "population, civic development, regional role, and current authority",
                "settlements with an established public decision place",
                "loaded gathering table and sittable furniture",
                "a meeting room near the settlement core",
                "A place where settlement business is conducted.",
                "organization and native social gathering", "table and stools in the same program",
                (p, s) => Populated(p, s) && (s.realizedCivicInfrastructure > 0
                    || (CASettlementRole)s.realizedRole == CASettlementRole.Center),
                (p, s) => "no civic or regional role supports a meeting place",
                (p, s) => new[]
                {
                    new[] { "Table2x2c", "Table1x2c" },
                    TierAtLeast(p, s, 2)
                        ? new[] { "Anon2CushionedChair", "DiningChair", "Stool" }
                        : new[] { "Stool", "DiningChair" }
                });
            Add(Custody, "Custody", "Security",
                "current local order, civic development, and population",
                "settlements whose current order maintains formal custody",
                "loaded human beds and a separable room",
                "a prisoner bed placed in a separate room",
                "A secure room for people held under the current order.",
                "native prisoner beds and faction institutions", "bedroll in the same program",
                (p, s) => s.realizedCivicInfrastructure >= 2
                    && (Axis(p, s, CAFactionAxes.LocalOrder) == "constabulary"
                        || Axis(p, s, CAFactionAxes.LocalOrder) == "rulers"),
                (p, s) => "the current order does not support formal custody",
                (p, s) => new[] { TierAtLeast(p, s, 1)
                    ? new[] { "Bed", "Bedroll" }
                    : new[] { "Bedroll", "Bed" } });
            Add(Defense, "Defenses", "Defense",
                "hostile relations, regional role, population, and faction defense",
                "settlements responsible for defense or exposed to hostile factions",
                "loaded cover buildings with native cover values",
                "a bounded defensive line on settlement ground",
                "Prepared cover around an exposed or defended settlement.",
                "native combat cover and settlement security", "barricade or sandbags",
                (p, s) => Populated(p, s) && (p.factions.Any(other =>
                        other != null && other.key != s.factionKey
                        && p.RelationBetween(s.factionKey, other.key)
                            == FactionRelationKind.Hostile)
                    || Axis(p, s, CAFactionAxes.Defense) == "standing"
                    || (CASettlementRole)s.realizedRole
                        == CASettlementRole.Center),
                (p, s) => "no hostile relation, defense rule, or regional role requires defenses",
                (p, s) => new[] { new[] { "Barricade", "Sandbags" } },
                extent: (p, s) => Mathf.Clamp(2 + s.residentPopulation / 300,
                    2, 6));
            Add(Research, "Research", "Research",
                "industrial knowledge, services, civic development, economy, history, and role",
                "mature industrial settlements able to staff a research bench",
                "loaded native research benches",
                "a research bench in its own work room",
                "A staffed place for local research.",
                "native research work and settlement research milestones",
                "simple research bench in the same program",
                (p, s) => TierAtLeast(p, s, 2)
                    && s.realizedServiceInfrastructure >= 2
                    && s.realizedCivicInfrastructure >= 2
                    && s.economicCapacity >= 2
                    && (s.historicalDevelopment >= 2
                        || (CASettlementRole)s.realizedRole
                            == CASettlementRole.Center),
                (p, s) => "knowledge, services, civic support, economy, or staffing is insufficient",
                (p, s) => new[] { new[] { "SimpleResearchBench" } });
            Add(Religion, "Religious gathering", "Religion",
                "the owning faction's realized Ideoligion and settlement population",
                "populated settlements whose faction has a realized Ideoligion",
                "loaded native Ideoligion ritual targets",
                "a ritual spot or ideogram in a gathering room",
                "A place for the settlement's Ideoligion rituals.",
                "native Ideoligion ritual targeting", "ritual spot in the same program",
                (p, s) => ModsConfig.IdeologyActive && Populated(p, s)
                    && p.FactionPlan(s.factionKey)?.ResolvedFactionDef != null,
                (p, s) => ModsConfig.IdeologyActive
                    ? "the owning faction has no realized Ideoligion"
                    : "Ideology is not active",
                (p, s) => new[] { new[] { "RitualSpot", "Ideogram" } });
            Add(Gathering, "Gathering place", "Social life",
                "population, services, Culture, and provision arrangements",
                "settlements with shared meals or a public-gathering meaning",
                "loaded gathering table and sittable furniture",
                "a shared table and seats in an existing room",
                "A common place for meals and social gathering.",
                "native gathering spots, joy, and provision operators", "table and stools",
                (p, s) => Populated(p, s) && (s.realizedServiceInfrastructure > 0
                    || (s.provisionArrangements?.Any(item => item != null
                        && item.active && item.operatorKind
                            != CAProvisionOperator.Household) ?? false)
                    || CACultureModel.Resolve(s.localCulture,
                        CASocialSubjectRegistry.PublicGathering).Salience > 0),
                (p, s) => "no shared-meal, service, or cultural gathering fact supports it",
                (p, s) => new[]
                {
                    new[] { "Table2x2c", "Table1x2c" },
                    TierAtLeast(p, s, 2)
                        ? new[] { "Anon2CushionedChair", "DiningChair", "Stool" }
                        : new[] { "Stool", "DiningChair" }
                });
            Add(Recreation, "Recreation", "Social life",
                "population, services, economy, and historical development",
                "established settlements with time and space for recreation",
                "loaded native joy buildings",
                "a recreation object placed on usable settlement ground",
                "A place for ordinary recreation.",
                "native joy jobs", "horseshoes pin in the same program",
                (p, s) => Populated(p, s) && (s.realizedServiceInfrastructure > 0
                    || s.historicalDevelopment > 0),
                (p, s) => "the settlement lacks service or historical support for a recreation place",
                (p, s) => new[] { TierAtLeast(p, s, 2)
                    ? new[] { "ChessTable", "GameOfUrBoard", "HorseshoesPin" }
                    : TierAtLeast(p, s, 1)
                        ? new[] { "GameOfUrBoard", "HorseshoesPin" }
                        : new[] { "HorseshoesPin" } });
            Add(ArtAndMemory, "Art and memory", "Culture",
                "Culture, historical development, civic development, and regional role",
                "historically or civically significant settlements",
                "loaded buildings with native art contracts",
                "an art object placed in a public or meeting room",
                "Objects through which the settlement marks memory and status.",
                "native beauty, art, and cultural history", "small sculpture in the same program",
                (p, s) => s.historicalDevelopment >= 2
                    || s.realizedCivicInfrastructure >= 2
                    || (CASettlementRole)s.realizedRole
                        == CASettlementRole.Center,
                (p, s) => "history, civic development, and regional role do not support a public memorial",
                (p, s) => new[] { new[] { "DankPyon_Bust",
                    "SculptureSmall" } });
            Add(Agriculture, "Cultivation", "Agriculture",
                "land capacity, population, economy, and settlement role",
                "populated settlements with workable land",
                "a native growing-zone contract on workable soil",
                "a native growing zone placed on suitable ground",
                "A maintained place for local cultivation.",
                "native plant growing", "none outside this program",
                (p, s) => Populated(p, s) && s.landCapacity >= 2,
                (p, s) => "this settlement lacks enough workable land",
                (p, s) => Enumerable.Empty<string[]>(),
                (p, s) => Mathf.Clamp(1 + s.landCapacity, 2, 4),
                spatial: true, materializeSpatial: true,
                spatiallyRealized: (p, s) =>
                    s.landCapacity >= 2 && s.residentPopulation > 0,
                spatialMaterializer: CASettlementProgramMaterializer
                    .MaterializeCultivation);
            Add(Animals, "Animal keeping", "Agriculture",
                "land capacity, population, Culture, and economic specialization",
                "land-rich settlements able to maintain animal sleeping places",
                "loaded native animal beds",
                "animal beds placed near worked ground",
                "Shelter for animals kept by the settlement.",
                "native animal rest", "animal sleeping box in the same program",
                (p, s) => Populated(p, s) && s.landCapacity >= 2
                    && (s.specialization > 0 || s.historicalDevelopment > 0),
                (p, s) => "land, specialization, or history does not support animal keeping",
                (p, s) => new[] { TierAtLeast(p, s, 1)
                    ? new[] { "AnimalBed", "AnimalSleepingBox" }
                    : new[] { "AnimalSleepingBox", "AnimalBed" } },
                (p, s) => s.landCapacity >= 3 ? 2 : 1);
            Add(Communications, "Communications", "Communications",
                "industrial knowledge, access, organization, and regional role",
                "organized industrial settlements with broad access",
                "loaded communications console and local power source",
                "a communications console and generator placed together",
                "A link for long-distance communication.",
                "native comms and organization", "none outside this program",
                (p, s) => TierAtLeast(p, s, 2)
                    && s.realizedAccessInfrastructure >= 2
                    && (s.realizedCivicInfrastructure >= 2
                        || (CASettlementRole)s.realizedRole
                            == CASettlementRole.Center),
                (p, s) => "knowledge, access, civic support, or regional role is insufficient",
                (p, s) => new[]
                {
                    new[] { "WoodFiredGenerator" },
                    new[] { "CommsConsole" }
                });
            Add(Transport, "Routes", "Transport",
                "saved roads, rivers, coast, and access infrastructure",
                "settlements connected by a realized route",
                "the saved region's native road, river, or coast contract",
                "the existing route remains part of the settlement ground",
                "Road, river, or coastal access serving this settlement.",
                "regional placement, trade, and travel", "none",
                (p, s) => s.hasRoadAccess || s.hasRiverAccess
                    || s.hasCoastalAccess,
                (p, s) => "no road, river, or coast reaches this settlement area",
                (p, s) => Enumerable.Empty<string[]>(), spatial: true,
                spatiallyRealized: (p, s) => s.hasRoadAccess
                    || s.hasRiverAccess || s.hasCoastalAccess);

            void AddProvision(string key, string label,
                CAProvisionOperator kind, string domain, string summary,
                string[] primary, bool dining)
            {
                Add(key, label, domain,
                    "realized provision operator, access, funding, distribution, and reach",
                    "a valid provision arrangement with this real operator",
                    "loaded food, storage, and gathering assets required by the operator",
                    "operator-owned nodes and stock placed from the saved arrangement",
                    summary,
                    "provision organizations, stock, access, and taxation",
                    "same-program stove, shelf, table, or seat",
                    (p, s) => HasProvision(s, kind),
                    (p, s) => "no valid " + label.ToLowerInvariant()
                        + " arrangement exists",
                    (p, s) => dining
                        ? new[]
                        {
                            primary,
                            new[] { "Shelf" },
                            new[] { "Table2x2c", "Table1x2c" },
                            TierAtLeast(p, s, 2)
                                ? new[] { "Anon2CushionedChair", "DiningChair", "Stool" }
                                : new[] { "Stool", "DiningChair" }
                        }
                        : new[] { primary, new[] { "Shelf" } },
                    (p, s) => Math.Max(1, (s.provisionArrangements
                        ?? new List<CAProvisionArrangement>()).Where(item =>
                            item != null && item.active
                            && item.operatorKind == kind)
                        .Sum(item => Math.Max(1, item.nodes))));
            }
            AddProvision(CommunalProvision, "Communal provision",
                CAProvisionOperator.Communal, "Food",
                "Shared-work kitchens and stores operated by a real communal organization.",
                new[] { "FueledStove", "Campfire" }, true);
            AddProvision(AuthorityProvision, "Authority reserve",
                CAProvisionOperator.Authority, "Storage",
                "A real settlement reserve with its saved funding and reach.",
                new[] { "Shelf" }, false);
            AddProvision(HouseholdProvision, "Household provision",
                CAProvisionOperator.Household, "Food",
                "Household-scoped hearths with no separate operator organization.",
                new[] { "FueledStove", "Campfire" }, false);
            return result;
        }

        private static CASettlementProgramFacts BuildFacts(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            CARegionalFactionPlan faction = plan.FactionPlan(
                settlement.factionKey);
            string axes = string.Join(",", (faction?.factionStructure
                    ?? new List<CAAxisEntry>()).Where(item => item != null)
                .OrderBy(item => item.axisKey, StringComparer.Ordinal)
                .Select(item => item.axisKey + "=" + item.optionKey));
            string beliefs = string.Join(",", (faction?.politicalBeliefs
                    ?.positions ?? new List<CAAxisEntry>())
                .Where(item => item != null)
                .OrderBy(item => item.axisKey, StringComparer.Ordinal)
                .Select(item => item.axisKey + "=" + item.optionKey));
            string provisions = string.Join(",", (settlement
                    .provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                .Where(item => item != null && item.active)
                .OrderBy(item => item.basisKey, StringComparer.Ordinal)
                .Select(item => item.basisKey + ":" + item.operatorKind
                    + ":" + item.access + ":" + item.funding + ":"
                    + item.distribution + ":" + item.nodes + ":"
                    + item.reach));
            string relations = string.Join(",", (plan.relations
                    ?? new List<CARegionalRelationPlan>())
                .Where(item => item != null
                    && (item.leftFactionKey == settlement.factionKey
                        || item.rightFactionKey == settlement.factionKey))
                .OrderBy(item => item.leftFactionKey)
                .ThenBy(item => item.rightFactionKey)
                .Select(item => item.leftFactionKey + "-"
                    + item.rightFactionKey + "=" + item.relation));
            int tier = CASettlementAxes.Tier(
                CASettlementAxes.TemplateEraPrior(faction?.ResolvedFactionDef));
            var facts = new CASettlementProgramFacts
            {
                CandidateId = plan.candidateId,
                Slot = settlement.slot,
                TileId = settlement.memberTileId,
                FactionKey = settlement.factionKey,
                PopulationOrigin = settlement.populationOrigin.ToString(),
                Population = settlement.residentPopulation,
                Land = settlement.landCapacity,
                Access = settlement.realizedAccessInfrastructure,
                Services = settlement.realizedServiceInfrastructure,
                Civic = settlement.realizedCivicInfrastructure,
                Economy = settlement.economicCapacity,
                Trade = settlement.tradeConnectivity,
                Specialization = settlement.specialization,
                History = settlement.historicalDevelopment,
                Role = settlement.realizedRole,
                Scale = settlement.realizedScale,
                Operations = settlement.operationalRoleMask,
                TechnologyTier = tier,
                Technology = faction?.TechnologySummary ?? "none",
                Axes = axes,
                Beliefs = beliefs,
                Culture = CACultureModel.SociologicalSignature(
                    settlement.localCulture),
                // The presence of a resolved humanlike faction template is
                // the pre-confirmation fact. The later Ideo instance identity
                // cannot alter a confirmed program.
                IdeoligionPlanned = ModsConfig.IdeologyActive
                    && faction?.ResolvedFactionDef != null,
                Provisions = provisions,
                Relations = relations,
                DefenseRule = CAFactionAxes.KeyOf(
                    faction?.factionStructure, CAFactionAxes.Defense)
                    ?? CAFactionAxes.KeyOf(faction?.politicalBeliefs
                        ?.positions, CAFactionAxes.Defense),
                LocalOrderRule = CAFactionAxes.KeyOf(
                    faction?.factionStructure, CAFactionAxes.LocalOrder)
                    ?? CAFactionAxes.KeyOf(faction?.politicalBeliefs
                        ?.positions, CAFactionAxes.LocalOrder),
                HostileRelation = (plan.factions
                        ?? new List<CARegionalFactionPlan>()).Any(other =>
                            other != null && other.key != settlement.factionKey
                            && plan.RelationBetween(settlement.factionKey,
                                other.key) == FactionRelationKind.Hostile),
                PublicGatheringMeaning = CACultureModel.Resolve(
                    settlement.localCulture,
                    CASocialSubjectRegistry.PublicGathering).Salience > 0,
                HasRoad = settlement.hasRoadAccess,
                HasRiver = settlement.hasRiverAccess,
                HasCoast = settlement.hasCoastalAccess
            };
            foreach (CAProvisionArrangement provision in settlement
                .provisionArrangements ?? new List<CAProvisionArrangement>())
                if (provision != null && provision.active)
                    facts.ProvisionFacts.Add(
                        new CASettlementProgramProvisionFact
                        {
                            OperatorKind = provision.operatorKind.ToString(),
                            Nodes = Math.Max(1, provision.nodes)
                        });
            return facts;
        }

        private static bool ProgramsEquivalent(CASettlementProgram saved,
            CASettlementProgram expected)
        {
            if (saved == null || expected == null
                || saved.schemaVersion != CASettlementProgram.CurrentSchemaVersion
                || saved.sourceSignature != expected.sourceSignature)
                return false;
            List<CASettlementProgramEntry> left = saved.entries
                ?? new List<CASettlementProgramEntry>();
            List<CASettlementProgramEntry> right = expected.entries
                ?? new List<CASettlementProgramEntry>();
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                CASettlementProgramEntry a = left[i];
                CASettlementProgramEntry b = right[i];
                if (a == null || b == null
                    || a.schemaVersion
                        != CASettlementProgramEntry.CurrentSchemaVersion
                    || a.programKey != b.programKey || a.scope != b.scope
                    || a.count != b.count || a.extent != b.extent
                    || (a.materializationState ?? "")
                        != (b.materializationState ?? "")
                    || (a.placedThingIds?.Count ?? 0) != 0
                    || (a.blocker ?? "") != (b.blocker ?? "")
                    || (a.fallback ?? "") != (b.fallback ?? "")
                    || a.signature != b.signature
                    || !(a.selectedCandidates ?? new List<string>())
                        .SequenceEqual(b.selectedCandidates
                            ?? new List<string>()))
                    return false;
            }
            return true;
        }
    }

    // Shared rendering rule for contextual decisions. Ordinary screens get a
    // control only when there is a real choice, an essential readout when one
    // valid value remains, and a warning only when no value blocks progress.
    internal static class CAContextualChoicePresentation
    {
        internal enum Mode : byte
        {
            Omit,
            Readout,
            Control,
            Warning
        }

        internal static Mode Resolve(int choiceCount, bool essentialWhenFixed,
            bool blockingWhenEmpty)
        {
            if (choiceCount > 1) return Mode.Control;
            if (choiceCount == 1)
                return essentialWhenFixed ? Mode.Readout : Mode.Omit;
            return blockingWhenEmpty ? Mode.Warning : Mode.Omit;
        }

        internal static float Draw(Rect rect, string label,
            IReadOnlyList<string> validChoices, int selected,
            Action<int> choose, bool essentialWhenFixed,
            bool blockingWhenEmpty, string emptyReason)
        {
            IReadOnlyList<string> choices = validChoices
                ?? Array.Empty<string>();
            Mode mode = Resolve(choices.Count, essentialWhenFixed,
                blockingWhenEmpty);
            if (mode == Mode.Control)
            {
                float labelHeight = Text.CalcHeight(label, rect.width);
                Widgets.Label(new Rect(rect.x, rect.y, rect.width,
                    labelHeight), label);
                float choiceWidth = rect.width / Math.Max(1, choices.Count);
                float choiceHeight = Math.Max(34f, choices.Max(choice =>
                    Text.CalcHeight(choice ?? "", Math.Max(24f,
                        choiceWidth - 12f)) + 10f));
                CACreationUI.DrawSegment(new Rect(rect.x,
                    rect.y + labelHeight + 4f, rect.width, choiceHeight),
                    choices.ToArray(),
                    Mathf.Clamp(selected, 0, choices.Count - 1), choose);
                return labelHeight + choiceHeight + 10f;
            }
            if (mode == Mode.Readout)
            {
                if (rect.width < 390f)
                {
                    float labelHeight = Text.CalcHeight(label, rect.width);
                    Widgets.Label(new Rect(rect.x, rect.y, rect.width,
                        labelHeight), label);
                    float valueHeight = Text.CalcHeight(choices[0],
                        rect.width);
                    GUI.color = ColoredText.SubtleGrayColor;
                    Widgets.Label(new Rect(rect.x,
                        rect.y + labelHeight + 1f, rect.width, valueHeight),
                        choices[0]);
                    GUI.color = Color.white;
                    return labelHeight + valueHeight + 6f;
                }
                float labelWidth = rect.width * 0.38f;
                float valueWidth = rect.width - labelWidth - 8f;
                float rowHeight = Math.Max(
                    Text.CalcHeight(label, labelWidth),
                    Text.CalcHeight(choices[0], valueWidth));
                Widgets.Label(new Rect(rect.x, rect.y, labelWidth,
                    rowHeight), label);
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(rect.x + labelWidth + 8f, rect.y,
                    valueWidth, rowHeight), choices[0]);
                GUI.color = Color.white;
                return rowHeight + 4f;
            }
            if (mode == Mode.Warning)
            {
                GUI.color = new Color(1f, 0.58f, 0.42f);
                float height = Text.CalcHeight(emptyReason ?? label,
                    rect.width);
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, height),
                    emptyReason ?? label);
                GUI.color = Color.white;
                return height + 6f;
            }
            return 0f;
        }
    }

    // Read-only account of the realized composition. The owning facts are
    // edited on their own surfaces; opening this window never regenerates or
    // mutates the saved program.
    internal sealed class Dialog_CASettlementProgram : Window
    {
        private readonly CARegionalPlan plan;
        private readonly CARegionalSettlementPlan settlement;
        private Vector2 scroll;
        private float measuredHeight = 600f;
        private bool showUnavailable;

        internal Dialog_CASettlementProgram(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            this.plan = plan;
            this.settlement = settlement;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(920f, UI.screenWidth - 48f),
            Mathf.Min(740f, UI.screenHeight - 48f));

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "Settlement Composition");
            Text.Font = GameFont.Small;
            string name = settlement == null ? "Settlement"
                : CARegionalPlanUtility.SettlementName(plan, settlement);
            float introHeight = Text.CalcHeight(name
                + ". These programs follow from the saved population, ground, "
                + "access, services, order, economy, role, and history.",
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width, introHeight),
                name + ". These programs follow from the saved population, "
                + "ground, access, services, order, economy, role, and history.");
            float top = 46f + introHeight;
            Rect outRect = new Rect(0f, top, inRect.width,
                inRect.height - top - 36f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, measuredHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            Heading(ref y, view.width, "Present at Start");
            List<CASettlementProgramEntry> present = (settlement?
                    .settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>())
                .Where(entry => entry != null && entry.blocker.NullOrEmpty())
                .ToList();
            foreach (IGrouping<string, CASettlementProgramEntry> domain in
                present.GroupBy(entry => CASettlementProgramRegistry
                        .Find(entry.programKey)?.Domain ?? "Other")
                    .OrderBy(group => group.Key))
            {
                Heading(ref y, view.width, domain.Key);
                foreach (CASettlementProgramEntry entry in domain)
                    ProgramRow(ref y, view.width, entry, settlement);
            }
            if (present.Count == 0)
                Note(ref y, view.width, "No supported program is present.");

            List<CASettlementProgramAvailability> alternatives =
                CASettlementProgramRegistry.SupportedAlternatives(plan,
                    settlement).ToList();
            if (alternatives.Count > 0)
            {
                Heading(ref y, view.width, "Supported Alternatives");
                foreach (CASettlementProgramAvailability option in alternatives)
                    Note(ref y, view.width, option.Definition.Label + ": "
                        + string.Join(", ", option.Candidates));
            }

            List<CASettlementProgramAvailability> unavailable =
                CASettlementProgramRegistry.Unavailable(plan, settlement)
                    .ToList();
            if (unavailable.Count > 0)
            {
                if (Widgets.ButtonText(new Rect(0f, y, view.width, 28f),
                        showUnavailable ? "Hide unavailable programs"
                            : "Show unavailable programs"))
                    showUnavailable = !showUnavailable;
                y += 36f;
                if (showUnavailable)
                {
                    Heading(ref y, view.width, "Unavailable Programs");
                    foreach (CASettlementProgramAvailability option in
                        unavailable)
                        Note(ref y, view.width, option.Definition.Label + ": "
                            + option.Reason);
                }
            }
            measuredHeight = y + 12f;
            Widgets.EndScrollView();
        }

        private static void ProgramRow(ref float y, float width,
            CASettlementProgramEntry entry,
            CARegionalSettlementPlan settlement)
        {
            CASettlementProgramDef definition =
                CASettlementProgramRegistry.Find(entry.programKey);
            string label = definition?.Label ?? "Saved program";
            string assets = entry.selectedCandidates == null
                || entry.selectedCandidates.Count == 0
                    ? "No placed asset"
                    : string.Join(", ", entry.selectedCandidates.Select(
                        candidate => DefDatabase<ThingDef>
                            .GetNamedSilentFail(candidate)?.LabelCap.ToString()
                            ?? "Unavailable asset"));
            string provider = ProgramProvider(entry, settlement);
            string detail = "Scope: " + (entry.scope ?? "settlement")
                + "\nAssets: " + assets
                + "\nState: " + (entry.materializationState ?? "pending")
                + (provider.NullOrEmpty() ? "" : "\nOperator: " + provider)
                + "\nCause: " + (definition?.SourceFacts
                    ?? "saved settlement facts");
            float labelWidth = Mathf.Min(190f, width * 0.28f);
            float height = Mathf.Max(26f, Text.CalcHeight(detail,
                width - labelWidth - 12f));
            Widgets.Label(new Rect(0f, y, labelWidth, height), label);
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(labelWidth + 12f, y,
                width - labelWidth - 12f, height), detail);
            GUI.color = Color.white;
            y += height + 6f;
        }

        private static string ProgramProvider(
            CASettlementProgramEntry entry,
            CARegionalSettlementPlan settlement)
        {
            CAProvisionOperator? kind = entry?.programKey
                    == CASettlementProgramRegistry.HouseholdProvision
                ? CAProvisionOperator.Household
                : entry?.programKey
                    == CASettlementProgramRegistry.CommunalProvision
                    ? CAProvisionOperator.Communal
                : entry?.programKey
                    == CASettlementProgramRegistry.AuthorityProvision
                    ? CAProvisionOperator.Authority
                    : (CAProvisionOperator?)null;
            if (!kind.HasValue) return null;
            List<CAProvisionArrangement> arrangements = (settlement
                    ?.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                .Where(item => item != null && item.active
                    && item.operatorKind == kind.Value).ToList();
            string[] labels = arrangements.Select(item => item.basisLabel)
                .Where(label => !label.NullOrEmpty()).Distinct().ToArray();
            if (labels.Length > 0) return string.Join(", ", labels);
            if (kind.Value == CAProvisionOperator.Household)
                return "Households";
            if (kind.Value == CAProvisionOperator.Communal)
                return "Communal kitchen group";
            if (kind.Value == CAProvisionOperator.Authority)
                return "Settlement authority";
            return null;
        }

        private static void Heading(ref float y, float width, string text)
        {
            if (y > 0f) y += 8f;
            Text.Font = GameFont.Medium;
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            Text.Font = GameFont.Small;
            y += height + 6f;
        }

        private static void Note(ref float y, float width, string text)
        {
            GUI.color = ColoredText.SubtleGrayColor;
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            GUI.color = Color.white;
            y += height + 6f;
        }
    }
}
