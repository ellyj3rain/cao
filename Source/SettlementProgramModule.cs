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
        public const int CurrentSchemaVersion = 4;
        public int schemaVersion = CurrentSchemaVersion;
        public string programKey;
        public string scope;
        public string sourceReceipt;
        public string needSource;
        public string operatorIdentity;
        public string operatorSource;
        public string laborSource;
        public string standingSource;
        public string activitySource;
        public string targetPopulation;
        public string knowledgeSource;
        public string fundingSource;
        public string stockSource;
        public string policyKey;
        public string materialSource;
        public string accessSource;
        public string maintenanceSource;
        public List<string> culturalSubjects = new List<string>();
        public bool requiresOperator;
        public bool requiresFunding;
        public bool requiresStock;
        public bool requiresMaterial = true;
        public List<string> selectedCandidates = new List<string>();
        public int count = 1;
        public int extent = 1;
        public string materializationState = "pending";
        public List<string> placedThingIds = new List<string>();
        public string blocker;
        public string fallback;
        public string signature;
        public string runtimeState = "unresolved";
        public string runtimeFailure;
        public int lastRuntimeValidationTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref scope, "scope");
            Scribe_Values.Look(ref sourceReceipt, "sourceReceipt");
            Scribe_Values.Look(ref needSource, "needSource");
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref operatorSource, "operatorSource");
            Scribe_Values.Look(ref laborSource, "laborSource");
            Scribe_Values.Look(ref standingSource, "standingSource");
            Scribe_Values.Look(ref activitySource, "activitySource");
            Scribe_Values.Look(ref targetPopulation, "targetPopulation");
            Scribe_Values.Look(ref knowledgeSource, "knowledgeSource");
            Scribe_Values.Look(ref fundingSource, "fundingSource");
            Scribe_Values.Look(ref stockSource, "stockSource");
            Scribe_Values.Look(ref policyKey, "policyKey");
            Scribe_Values.Look(ref materialSource, "materialSource");
            Scribe_Values.Look(ref accessSource, "accessSource");
            Scribe_Values.Look(ref maintenanceSource, "maintenanceSource");
            Scribe_Collections.Look(ref culturalSubjects,
                "culturalSubjects", LookMode.Value);
            Scribe_Values.Look(ref requiresOperator,
                "requiresOperator", false);
            Scribe_Values.Look(ref requiresFunding,
                "requiresFunding", false);
            Scribe_Values.Look(ref requiresStock, "requiresStock", false);
            Scribe_Values.Look(ref requiresMaterial,
                "requiresMaterial", true);
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
            Scribe_Values.Look(ref runtimeState, "runtimeState",
                "unresolved");
            Scribe_Values.Look(ref runtimeFailure, "runtimeFailure");
            Scribe_Values.Look(ref lastRuntimeValidationTick,
                "lastRuntimeValidationTick", -1);
            if (selectedCandidates == null)
                selectedCandidates = new List<string>();
            if (placedThingIds == null)
                placedThingIds = new List<string>();
            if (culturalSubjects == null)
                culturalSubjects = new List<string>();
        }

        internal CASettlementProgramEntry Copy()
        {
            return new CASettlementProgramEntry
            {
                schemaVersion = schemaVersion,
                programKey = programKey,
                scope = scope,
                sourceReceipt = sourceReceipt,
                needSource = needSource,
                operatorIdentity = operatorIdentity,
                operatorSource = operatorSource,
                laborSource = laborSource,
                standingSource = standingSource,
                activitySource = activitySource,
                targetPopulation = targetPopulation,
                knowledgeSource = knowledgeSource,
                fundingSource = fundingSource,
                stockSource = stockSource,
                policyKey = policyKey,
                materialSource = materialSource,
                accessSource = accessSource,
                maintenanceSource = maintenanceSource,
                culturalSubjects = (culturalSubjects
                    ?? new List<string>()).ToList(),
                requiresOperator = requiresOperator,
                requiresFunding = requiresFunding,
                requiresStock = requiresStock,
                requiresMaterial = requiresMaterial,
                selectedCandidates = (selectedCandidates
                    ?? new List<string>()).ToList(),
                count = count,
                extent = extent,
                materializationState = materializationState,
                placedThingIds = (placedThingIds
                    ?? new List<string>()).ToList(),
                blocker = blocker,
                fallback = fallback,
                signature = signature,
                runtimeState = runtimeState,
                runtimeFailure = runtimeFailure,
                lastRuntimeValidationTick = lastRuntimeValidationTick
            };
        }
    }

    // The realized program is written once after its upstream facts and
    // provision arrangements are settled. Preview and materialization consume
    // this same object; neither rerolls candidate assets.
    public sealed class CASettlementProgram : IExposable
    {
        public const int CurrentSchemaVersion = 4;
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

        internal CASettlementProgramEntry Entry(string key,
            string operatorIdentity = null)
        {
            return entries?.FirstOrDefault(item => item != null
                && item.programKey == key
                && (operatorIdentity == null
                    || item.operatorIdentity == operatorIdentity));
        }

        internal IEnumerable<CASettlementProgramEntry> Entries(string key)
        {
            return (entries ?? new List<CASettlementProgramEntry>())
                .Where(item => item != null && item.programKey == key);
        }

        internal bool Has(string key)
        {
            return Entries(key).Any(entry => entry.blocker.NullOrEmpty());
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

    // An authored or historically observed operation which can support one
    // settlement program. This is not a tendency or score. It names the need,
    // operator, labor, knowledge, funding, stock, policy, material contract,
    // and any exact Culture subjects that modify its conduct.
    public sealed class CASettlementOperationalFact : IExposable
    {
        public const int CurrentSchemaVersion = 3;
        public int schemaVersion = CurrentSchemaVersion;
        public string factKey;
        public string programKey;
        public string needSource;
        public string operatorIdentity;
        public string operatorSource;
        public string laborSource;
        public string standingSource;
        public string activitySource;
        public string targetPopulation;
        public string knowledgeSource;
        public string fundingSource;
        public string stockSource;
        public string policyKey;
        public string materialSource;
        public string accessSource;
        public string maintenanceSource;
        public List<string> culturalSubjects = new List<string>();
        public bool active = true;
        public string provenance;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref factKey, "factKey");
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref needSource, "needSource");
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref operatorSource, "operatorSource");
            Scribe_Values.Look(ref laborSource, "laborSource");
            Scribe_Values.Look(ref standingSource, "standingSource");
            Scribe_Values.Look(ref activitySource, "activitySource");
            Scribe_Values.Look(ref targetPopulation, "targetPopulation");
            Scribe_Values.Look(ref knowledgeSource, "knowledgeSource");
            Scribe_Values.Look(ref fundingSource, "fundingSource");
            Scribe_Values.Look(ref stockSource, "stockSource");
            Scribe_Values.Look(ref policyKey, "policyKey");
            Scribe_Values.Look(ref materialSource, "materialSource");
            Scribe_Values.Look(ref accessSource, "accessSource");
            Scribe_Values.Look(ref maintenanceSource, "maintenanceSource");
            Scribe_Collections.Look(ref culturalSubjects,
                "culturalSubjects", LookMode.Value);
            Scribe_Values.Look(ref active, "active", true);
            Scribe_Values.Look(ref provenance, "provenance");
            if (culturalSubjects == null)
                culturalSubjects = new List<string>();
        }

        internal CASettlementOperationalFact Copy()
        {
            return new CASettlementOperationalFact
            {
                schemaVersion = schemaVersion,
                factKey = factKey,
                programKey = programKey,
                needSource = needSource,
                operatorIdentity = operatorIdentity,
                operatorSource = operatorSource,
                laborSource = laborSource,
                standingSource = standingSource,
                activitySource = activitySource,
                targetPopulation = targetPopulation,
                knowledgeSource = knowledgeSource,
                fundingSource = fundingSource,
                stockSource = stockSource,
                policyKey = policyKey,
                materialSource = materialSource,
                accessSource = accessSource,
                maintenanceSource = maintenanceSource,
                culturalSubjects = (culturalSubjects
                    ?? new List<string>()).ToList(),
                active = active,
                provenance = provenance
            };
        }
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
        public const string DomesticProvision =
            CASettlementProgramCausalKernel.DomesticProvision;

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

        // Domestic membership creates provision demand, not an operational
        // program. A separate provision adapter may add a program only after
        // it resolves actual operators, work, stock, access and maintenance.
        internal static void ReconcileRuntimeProvisionPrograms(
            CARegionalSettlementRecord record)
        {
            if (record?.settlementProgram == null) return;
            record.settlementProgram.entries.RemoveAll(entry => entry != null
                && entry.programKey == DomesticProvision);
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
                + "direct need, operator, labor, knowledge, funding, stock, "
                + "policy, material, route, and loaded-asset evidence";
            return false;
        }

        private static CASettlementProgram Derive(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            List<CASettlementProgramOperationalEvidence> evidence =
                BuildEvidence(plan, settlement);
            string source = CASettlementProgramCausalKernel
                .SourceSignature(evidence);
            var realized = new CASettlementProgram { sourceSignature = source };
            List<CASettlementProgramCausalSpec> causalSpecs =
                CASettlementProgramCausalKernel.Derive(evidence);
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
                    needSource = causal.NeedSource,
                    operatorIdentity = causal.OperatorIdentity,
                    operatorSource = causal.OperatorSource,
                    laborSource = causal.LaborSource,
                    standingSource = causal.StandingSource,
                    activitySource = causal.ActivitySource,
                    targetPopulation = causal.TargetPopulation,
                    knowledgeSource = causal.KnowledgeSource,
                    fundingSource = causal.FundingSource,
                    stockSource = causal.StockSource,
                    policyKey = causal.PolicyKey,
                    materialSource = causal.MaterialSource,
                    accessSource = causal.AccessSource,
                    maintenanceSource = causal.MaintenanceSource,
                    culturalSubjects = causal.CulturalSubjects.ToList(),
                    requiresOperator = causal.RequiresOperator,
                    requiresFunding = causal.RequiresFunding,
                    requiresStock = causal.RequiresStock,
                    requiresMaterial = causal.RequiresMaterial,
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
                        entry.selectedCandidates, entry.blocker,
                        entry.needSource, entry.operatorIdentity,
                        entry.laborSource, entry.knowledgeSource,
                        entry.materialSource, entry.standingSource,
                        entry.activitySource, entry.targetPopulation,
                        entry.accessSource, entry.maintenanceSource);
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
            if (entries.Count == 0)
                return "No starting settlement programs are recorded.";
            string[] domains = entries.Select(entry => Find(entry.programKey)
                    ?.Domain).Where(domain => !domain.NullOrEmpty()).Distinct()
                .OrderBy(domain => domain, StringComparer.Ordinal).ToArray();
            string[] leading = entries.Select(entry => Find(entry.programKey)
                    ?.Label ?? "Saved program").Take(4)
                .ToArray();
            return entries.Count + " established starting programs: "
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
                CAProvisionOperator.DomesticUnit => DomesticProvision,
                CAProvisionOperator.Individual => DomesticProvision,
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
                || key == FoodPreparation || key == DomesticProvision)
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
            var result = new List<CASettlementProgramDef>();

            void Add(string key, string label, string domain,
                string summary, string consumer, string fallback,
                Func<CARegionalPlan, CARegionalSettlementPlan,
                    IEnumerable<string[]>> candidates,
                Func<CARegionalPlan, CARegionalSettlementPlan, int> count = null,
                Func<CARegionalPlan, CARegionalSettlementPlan, int> extent = null,
                bool spatial = false,
                bool materializeSpatial = false,
                Func<CARegionalPlan, CARegionalSettlementPlan, bool>
                    spatiallyRealized = null,
                Func<CARegionalSettlementRecord, Map,
                    CASettlementProgramEntry, bool> spatialMaterializer = null)
            {
                result.Add(new CASettlementProgramDef
                {
                    Key = key,
                    Label = label,
                    Domain = domain,
                    OwningModule = "SettlementProgramModule",
                    SourceFacts = "complete direct operational evidence for "
                        + key,
                    Applicability = "need, operator or participants, standing, "
                        + "knowledge, labor, material, access, funding, and "
                        + "maintenance as required by the saved contract",
                    CandidateResolution = spatial
                        ? "saved native spatial contract"
                        : "loaded functional assets after operational eligibility",
                    Materialization = spatial
                        ? "consume the saved native spatial contract"
                        : "place selected functional nodes from the saved program",
                    Summary = summary,
                    Consumer = consumer,
                    Fallback = fallback,
                    CandidateGroups = candidates,
                    Count = count,
                    Extent = extent,
                    NativeSpatialContract = spatial,
                    MinimumFunctionalRoles = spatial ? 0 : 1,
                    MaterializeSpatialContract = materializeSpatial,
                    SpatiallyRealized = spatiallyRealized,
                    SpatialMaterializer = spatialMaterializer
                });
            }

            Add(Housing, "Housing", "Housing",
                "Sleeping places for the resident population.",
                "native rest and ownership", "bedroll or bed in the same program",
                (p, s) => new[] { new[] { "Bedroll", "Bed" } },
                (p, s) => Mathf.Clamp(2 + s.residentPopulation / 350, 2, 6));
            Add(FoodPreparation, "Food preparation", "Food",
                "A place to prepare the settlement's food.",
                "native cooking and butchery work",
                "campfire within the same program",
                (p, s) => new[]
                {
                    new[] { "Campfire", "FueledStove" },
                    new[] { "TableButcher" }
                });
            Add(Storage, "Stores", "Storage",
                "Storage for food, medicine, and shared goods.",
                "native storage and provision consumers",
                "shelf in the same program",
                (p, s) => new[] { new[] { "Shelf" } },
                (p, s) => Mathf.Clamp(1 + s.residentPopulation / 450, 1, 3));
            Add(Medicine, "Medical care", "Medicine",
                "A room for treatment and recovery.",
                "native tending and medical rest",
                "bed or bedroll in the same program",
                (p, s) => new[]
                    { new[] { "Bedroll", "Bed", "HospitalBed" } });
            Add(Production, "Production", "Production",
                "A place for represented local making and repair.",
                "native bill work and settlement repair",
                "crafting spot in the same program",
                (p, s) => new[]
                    { new[] { "CraftingSpot", "FueledSmithy" } });
            Add(SpecializedIndustry, "Specialized industry", "Production",
                "A represented trade practiced beyond ordinary repair work.",
                "native bills and settlement production history",
                "another work table in the same program",
                (p, s) => new[]
                {
                    new[] { "HandTailoringBench", "FueledSmithy",
                        "ElectricSmithy" }
                });
            Add(Trade, "Trade", "Trade",
                "Facilities used by an existing exchange operation.",
                "native trading and transaction history",
                "no substitute outside the represented operation",
                (p, s) => new[]
                {
                    new[] { "WoodFiredGenerator" },
                    new[] { "CommsConsole" },
                    new[] { "OrbitalTradeBeacon" }
                });
            Add(Governance, "Meeting place", "Governance",
                "A place where represented settlement business is conducted.",
                "organization meetings and decision execution",
                "table and seats in the same program",
                (p, s) => new[]
                {
                    new[] { "Table2x2c", "Table1x2c" },
                    new[] { "Stool", "DiningChair",
                        "Anon2CushionedChair" }
                });
            Add(Custody, "Custody", "Security",
                "A secure room operated under the current order.",
                "native custody work and authorized wardens",
                "bedroll in the same program",
                (p, s) => new[] { new[] { "Bedroll", "Bed" } });
            Add(Defense, "Defenses", "Defense",
                "Prepared cover used by an authorized defense operation.",
                "native combat cover, readiness, and repair",
                "barricade or sandbags in the same program",
                (p, s) => new[] { new[] { "Barricade", "Sandbags" } },
                extent: (p, s) => Mathf.Clamp(
                    2 + s.residentPopulation / 300, 2, 6));
            Add(Research, "Research", "Research",
                "A staffed place for an active research contract.",
                "native research work and represented milestones",
                "simple research bench in the same program",
                (p, s) => new[] { new[] { "SimpleResearchBench" } });
            Add(Religion, "Religious gathering", "Religion",
                "A represented site for actual Ideoligion practice.",
                "native Ideoligion ritual activity and history",
                "ritual spot in the same program",
                (p, s) => new[] { new[] { "RitualSpot", "Ideogram" } });
            Add(Gathering, "Gathering place", "Social life",
                "A common place used by represented participants.",
                "native gathering, social activity, and provision work",
                "table and seats in the same program",
                (p, s) => new[]
                {
                    new[] { "Table2x2c", "Table1x2c" },
                    new[] { "Stool", "DiningChair",
                        "Anon2CushionedChair" }
                });
            Add(Recreation, "Recreation", "Social life",
                "A place for ordinary resident recreation.",
                "native joy jobs and maintenance",
                "horseshoes pin in the same program",
                (p, s) => new[]
                {
                    new[] { "HorseshoesPin", "GameOfUrBoard",
                        "ChessTable" }
                });
            Add(ArtAndMemory, "Art and memory", "Culture",
                "Objects used by represented expression or remembrance.",
                "native art, beauty, and recorded cultural practice",
                "small sculpture in the same program",
                (p, s) => new[]
                    { new[] { "SculptureSmall", "DankPyon_Bust" } });
            Add(Agriculture, "Cultivation", "Agriculture",
                "A maintained place for represented cultivation.",
                "native growing work and harvest history",
                "no substitute outside the saved spatial contract",
                (p, s) => Enumerable.Empty<string[]>(),
                (p, s) => Mathf.Clamp(1 + s.landCapacity, 2, 4),
                spatial: true, materializeSpatial: true,
                spatiallyRealized: (p, s) =>
                    s.landCapacity >= 2 && s.residentPopulation > 0,
                spatialMaterializer: CASettlementProgramMaterializer
                    .MaterializeCultivation);
            Add(Animals, "Animal keeping", "Agriculture",
                "Shelter used by a represented animal-keeping operation.",
                "native animal rest and handling work",
                "animal sleeping box in the same program",
                (p, s) => new[]
                {
                    new[] { "AnimalSleepingBox", "AnimalBed" }
                },
                (p, s) => s.landCapacity >= 3 ? 2 : 1);
            Add(Communications, "Communications", "Communications",
                "A functioning link used by a represented operator.",
                "native communication work and history",
                "no substitute outside the represented operation",
                (p, s) => new[]
                {
                    new[] { "WoodFiredGenerator" },
                    new[] { "CommsConsole" }
                });
            Add(Transport, "Routes", "Transport",
                "Road, river, or coastal access serving this settlement.",
                "regional placement, movement, trade, and maintenance",
                "no substitute outside saved geography",
                (p, s) => Enumerable.Empty<string[]>(),
                spatial: true,
                spatiallyRealized: (p, s) => s.hasRoadAccess
                    || s.hasRiverAccess || s.hasCoastalAccess);

            void AddProvision(string key, string label,
                CAProvisionOperator kind, string domain, string summary,
                string[] primary, bool dining)
            {
                Add(key, label, domain, summary,
                    "provision work, stock access, distribution, and maintenance",
                    "no arrangement without a complete factual operator contract",
                    (p, s) => dining
                        ? new[]
                        {
                            primary,
                            new[] { "Shelf" },
                            new[] { "Table2x2c", "Table1x2c" },
                            new[] { "Stool", "DiningChair",
                                "Anon2CushionedChair" }
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
                new[] { "Campfire", "FueledStove" }, true);
            AddProvision(AuthorityProvision, "Authority reserve",
                CAProvisionOperator.Authority, "Storage",
                "A real settlement reserve with saved authority, funding, and reach.",
                new[] { "Shelf" }, false);
            AddProvision(DomesticProvision, "Domestic provision",
                CAProvisionOperator.DomesticUnit, "Food",
                "Domestic-unit and individual provision bound to factual members.",
                new[] { "Campfire", "FueledStove" }, false);
            return result;
        }

        private static List<CASettlementProgramOperationalEvidence>
            BuildEvidence(CARegionalPlan plan,
                CARegionalSettlementPlan settlement)
        {
            return CASettlementProgramOperationalResolver.Build(
                plan, settlement, Find);
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
                    || a.needSource != b.needSource
                    || a.operatorIdentity != b.operatorIdentity
                    || a.operatorSource != b.operatorSource
                    || a.laborSource != b.laborSource
                    || a.standingSource != b.standingSource
                    || a.activitySource != b.activitySource
                    || a.targetPopulation != b.targetPopulation
                    || a.knowledgeSource != b.knowledgeSource
                    || a.fundingSource != b.fundingSource
                    || a.stockSource != b.stockSource
                    || a.policyKey != b.policyKey
                    || a.materialSource != b.materialSource
                    || a.accessSource != b.accessSource
                    || a.maintenanceSource != b.maintenanceSource
                    || a.requiresOperator != b.requiresOperator
                    || a.requiresFunding != b.requiresFunding
                    || a.requiresStock != b.requiresStock
                    || a.requiresMaterial != b.requiresMaterial
                    || a.count != b.count || a.extent != b.extent
                    || (a.materializationState ?? "")
                        != (b.materializationState ?? "")
                    || (a.placedThingIds?.Count ?? 0) != 0
                    || (a.blocker ?? "") != (b.blocker ?? "")
                    || (a.fallback ?? "") != (b.fallback ?? "")
                    || a.signature != b.signature
                    || !(a.selectedCandidates ?? new List<string>())
                        .SequenceEqual(b.selectedCandidates
                            ?? new List<string>())
                    || !(a.culturalSubjects ?? new List<string>())
                        .SequenceEqual(b.culturalSubjects
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

    // Established-program authoring and inspection. Opening the window is
    // inert; explicit Establish and Remove actions own saved fact changes.
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
                "Settlement programs");
            Text.Font = GameFont.Small;
            string name = settlement == null ? "Settlement"
                : CARegionalPlanUtility.SettlementName(plan, settlement);
            float introHeight = Text.CalcHeight(name
                + ". Each program is supported by a saved operating contract: "
                + "need, operator, labor, knowledge, material, access, and "
                + "maintenance where required.",
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width, introHeight),
                name + ". Each program is supported by a saved operating "
                + "contract: need, operator, labor, knowledge, material, "
                + "access, and maintenance where required.");
            float controlsY = 46f + introHeight;
            if (Widgets.ButtonText(new Rect(0f, controlsY, 220f, 30f),
                    "Establish a program..."))
                OpenEstablishMenu();
            float top = controlsY + 38f;
            Rect outRect = new Rect(0f, top, inRect.width,
                inRect.height - top - 36f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, measuredHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            Heading(ref y, view.width, "Established at start");
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
                Note(ref y, view.width,
                    "No complete starting program contract is recorded.");

            List<CASettlementProgramAvailability> alternatives =
                CASettlementProgramRegistry.SupportedAlternatives(plan,
                    settlement).ToList();
            if (alternatives.Count > 0)
            {
                Heading(ref y, view.width, "Supported asset alternatives");
                foreach (CASettlementProgramAvailability option in alternatives)
                    Note(ref y, view.width, option.Definition.Label + ": "
                        + string.Join(", ", option.Candidates.Select(
                            CandidateLabel)));
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
                    Heading(ref y, view.width, "Unavailable programs");
                    foreach (CASettlementProgramAvailability option in
                        unavailable)
                        Note(ref y, view.width, option.Definition.Label + ": "
                            + option.Reason);
                }
            }
            measuredHeight = y + 12f;
            Widgets.EndScrollView();
        }

        private void ProgramRow(ref float y, float width,
            CASettlementProgramEntry entry,
            CARegionalSettlementPlan settlement)
        {
            CASettlementProgramDef definition =
                CASettlementProgramRegistry.Find(entry.programKey);
            string label = definition?.Label ?? "Saved program";
            string selectedAssets = entry.selectedCandidates == null
                || entry.selectedCandidates.Count == 0
                    ? "No selected asset type"
                    : string.Join(", ", entry.selectedCandidates.Select(
                        CandidateLabel));
            int placed = entry.placedThingIds?.Count ?? 0;
            string placedAssets = placed == 0
                ? "None yet"
                : placed + " recorded placed asset"
                    + (placed == 1 ? "" : "s");
            string provider = ProgramProvider(entry, settlement);
            string detail = "Scope: " + (entry.scope ?? "settlement")
                + "\nSelected asset types: " + selectedAssets
                + "\nPlaced assets: " + placedAssets
                + "\nState: " + (entry.materializationState ?? "pending")
                + (provider.NullOrEmpty() ? "" : "\nOperator: " + provider)
                + "\nNeed: " + (entry.needSource.NullOrEmpty()
                    ? "Not established" : "Established need")
                + "\nLabor: " + (entry.laborSource.NullOrEmpty()
                    ? "Not assigned" : "Assigned members")
                + "\nKnowledge: " + (entry.knowledgeSource.NullOrEmpty()
                    ? "Not established" : "Established practice")
                + "\nMaterial: " + (entry.materialSource.NullOrEmpty()
                    ? "Not established" : "Starting assets and upkeep");
            float removeWidth = 82f;
            float contentWidth = width - removeWidth - 10f;
            float labelWidth = Mathf.Min(190f, contentWidth * 0.28f);
            float height = Mathf.Max(26f, Text.CalcHeight(detail,
                contentWidth - labelWidth - 12f));
            Widgets.Label(new Rect(0f, y, labelWidth, height), label);
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(labelWidth + 12f, y,
                contentWidth - labelWidth - 12f, height), detail);
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(width - removeWidth, y,
                    removeWidth, 28f), "Remove"))
                CASettlementProgramAuthoring.Remove(plan, settlement,
                    entry);
            y += height + 6f;
        }

        private void OpenEstablishMenu()
        {
            List<FloatMenuOption> options = CASettlementProgramRegistry.All
                .Where(definition => definition != null
                    && CASettlementProgramRegistry.ProgramKeyFor(
                        CAProvisionOperator.Communal) != definition.Key
                    && CASettlementProgramRegistry.ProgramKeyFor(
                        CAProvisionOperator.Authority) != definition.Key
                    && CASettlementProgramRegistry.ProgramKeyFor(
                        CAProvisionOperator.DomesticUnit) != definition.Key)
                .OrderBy(definition => definition.Domain,
                    StringComparer.Ordinal)
                .ThenBy(definition => definition.Label,
                    StringComparer.Ordinal)
                .Select(definition => new FloatMenuOption(
                    definition.Label, () => OpenPopulationMenu(definition)))
                .ToList();
            if (options.Count == 0)
                options.Add(new FloatMenuOption(
                    "No programs are available", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenPopulationMenu(CASettlementProgramDef definition)
        {
            List<FloatMenuOption> options = (settlement?.populationGroups
                    ?? new List<CASettlementPopulationGroup>())
                .Where(group => group != null)
                .OrderBy(group => group.key)
                .Select(group => new FloatMenuOption(
                    group.label.NullOrEmpty()
                        ? "Population group " + group.key : group.label,
                    () =>
                    {
                        if (!CASettlementProgramAuthoring.Establish(plan,
                                settlement, definition.Key, group.key,
                                out string failure))
                            Messages.Message(failure,
                                MessageTypeDefOf.RejectInput, false);
                    })).ToList();
            if (options.Count == 0)
                options.Add(new FloatMenuOption(
                    "Add a population group first", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string ProgramProvider(
            CASettlementProgramEntry entry,
            CARegionalSettlementPlan settlement)
        {
            if (entry == null || entry.operatorIdentity.NullOrEmpty())
                return null;
            CAProvisionArrangement exact = (settlement
                    ?.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                .FirstOrDefault(item => item != null && item.active
                    && item.operatorIdentity == entry.operatorIdentity
                    && CASettlementProgramRegistry.ProgramKeyFor(
                        item.operatorKind) == entry.programKey);
            if (!exact?.basisLabel.NullOrEmpty() == true)
                return exact.basisLabel;
            if (entry.operatorIdentity.StartsWith("population-group:",
                    StringComparison.Ordinal)
                && int.TryParse(entry.operatorIdentity.Substring(
                    "population-group:".Length), out int groupKey))
                return settlement?.populationGroups?.FirstOrDefault(group =>
                    group != null && group.key == groupKey)?.label
                    ?? "Recorded population group";
            if (entry.operatorIdentity.StartsWith("pawn:",
                    StringComparison.Ordinal)
                || entry.operatorIdentity.StartsWith("individual:",
                    StringComparison.Ordinal))
                return "Recorded resident";
            return "Recorded operator";
        }

        private static string CandidateLabel(string candidate)
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(candidate)
                ?.LabelCap.ToString() ?? "Unavailable asset type";
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
