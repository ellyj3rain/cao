using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    public static class CATechnologyCompetencies
    {
        public const string Understand = "understand";
        public const string Construct = "construct";
        public const string Operate = "operate";
        public const string Maintain = "maintain";

        public static readonly string[] All =
        {
            Understand, Construct, Operate, Maintain
        };
    }

    public sealed class CATechnologyDomainDef
    {
        public readonly string Key;
        public readonly string Label;
        public readonly string Description;

        public CATechnologyDomainDef(string key, string label,
            string description)
        {
            Key = key;
            Label = label;
            Description = description;
        }
    }

    public static class CATechnologyDomains
    {
        public const string Medicine = "medicine";
        public const string Agriculture = "agriculture";
        public const string Construction = "construction";
        public const string Manufacturing = "manufacturing";
        public const string Metallurgy = "metallurgy-materials";
        public const string Electrical = "electrical-systems";
        public const string Chemistry = "chemistry";
        public const string Logistics = "logistics-preservation";
        public const string Weapons = "weapons-defense";

        public static readonly CATechnologyDomainDef[] All =
        {
            new CATechnologyDomainDef(Medicine, "Medicine",
                "Diagnosis, treatment, surgery, and medical production."),
            new CATechnologyDomainDef(Agriculture, "Agriculture",
                "Cultivation, husbandry, and dependable food production."),
            new CATechnologyDomainDef(Construction, "Construction",
                "Buildings, shelter, civil works, and structural repair."),
            new CATechnologyDomainDef(Manufacturing, "Manufacturing",
                "Worktables, tooling, repeatable production, and fabrication."),
            new CATechnologyDomainDef(Metallurgy,
                "Metallurgy and materials",
                "Smithing, machining, fabrication, and material processing."),
            new CATechnologyDomainDef(Electrical, "Electrical systems",
                "Power, electronics, batteries, and powered machinery."),
            new CATechnologyDomainDef(Chemistry, "Chemistry",
                "Medicines, fuels, drugs, and chemical processing."),
            new CATechnologyDomainDef(Logistics,
                "Logistics and preservation",
                "Storage, refrigeration, transport, and supply handling."),
            new CATechnologyDomainDef(Weapons, "Weapons and defense",
                "Weapons, armor, fortification, and defensive systems.")
        };

        public static CATechnologyDomainDef Find(string key)
        {
            return All.FirstOrDefault(item => item.Key == key);
        }
    }

    public sealed class CATechnologyDomainKnowledge : IExposable
    {
        public string domainKey;
        public int understand;
        public int construct;
        public int operate;
        public int maintain;
        public byte source;
        public string provenance;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainKey, "domainKey");
            Scribe_Values.Look(ref understand, "understand", 0);
            Scribe_Values.Look(ref construct, "construct", 0);
            Scribe_Values.Look(ref operate, "operate", 0);
            Scribe_Values.Look(ref maintain, "maintain", 0);
            Scribe_Values.Look(ref source, "source", (byte)0);
            Scribe_Values.Look(ref provenance, "provenance");
        }

        internal int Rank(string competency)
        {
            switch (competency)
            {
                case CATechnologyCompetencies.Construct: return construct;
                case CATechnologyCompetencies.Operate: return operate;
                case CATechnologyCompetencies.Maintain: return maintain;
                default: return understand;
            }
        }

        internal void SetRank(string competency, int rank)
        {
            rank = Math.Max(0, Math.Min(5, rank));
            switch (competency)
            {
                case CATechnologyCompetencies.Construct:
                    construct = rank;
                    break;
                case CATechnologyCompetencies.Operate:
                    operate = rank;
                    break;
                case CATechnologyCompetencies.Maintain:
                    maintain = rank;
                    break;
                default:
                    understand = rank;
                    break;
            }
        }

        internal CATechnologyDomainKnowledge Copy()
        {
            return new CATechnologyDomainKnowledge
            {
                domainKey = domainKey,
                understand = understand,
                construct = construct,
                operate = operate,
                maintain = maintain,
                source = source,
                provenance = provenance
            };
        }
    }

    public enum CATechnologyCustodyKind : byte
    {
        Pawn = 0,
        Institution = 1,
        Record = 2
    }

    public sealed class CATechnologyCustodyRecord : IExposable
    {
        public string domainKey;
        public string competencyKey;
        public int rank;
        public CATechnologyCustodyKind kind;
        public int pawnThingId = -1;
        public string custodianKey;
        // Persistent stores are available where they physically exist. A
        // pawn's current map is resolved from the pawn itself, so these fields
        // apply only to represented institutions and records.
        public int mapId = -1;
        public int tileId = -1;
        public bool accessible = true;
        public int acquiredAtTick = -1;
        public int unavailableAtTick = -1;
        public string unavailableReason;
        public string provenance;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainKey, "domainKey");
            Scribe_Values.Look(ref competencyKey, "competencyKey");
            Scribe_Values.Look(ref rank, "rank", 0);
            Scribe_Values.Look(ref kind, "kind",
                CATechnologyCustodyKind.Pawn);
            Scribe_Values.Look(ref pawnThingId, "pawnThingId", -1);
            Scribe_Values.Look(ref custodianKey, "custodianKey");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref tileId, "tileId", -1);
            Scribe_Values.Look(ref accessible, "accessible", true);
            Scribe_Values.Look(ref acquiredAtTick, "acquiredAtTick", -1);
            Scribe_Values.Look(ref unavailableAtTick,
                "unavailableAtTick", -1);
            Scribe_Values.Look(ref unavailableReason, "unavailableReason");
            Scribe_Values.Look(ref provenance, "provenance");
        }

        internal CATechnologyCustodyRecord Copy()
        {
            return new CATechnologyCustodyRecord
            {
                domainKey = domainKey,
                competencyKey = competencyKey,
                rank = rank,
                kind = kind,
                pawnThingId = pawnThingId,
                custodianKey = custodianKey,
                mapId = mapId,
                tileId = tileId,
                accessible = accessible,
                acquiredAtTick = acquiredAtTick,
                unavailableAtTick = unavailableAtTick,
                unavailableReason = unavailableReason,
                provenance = provenance
            };
        }
    }

    public sealed class CATechnologyAvailabilityReceipt : IExposable
    {
        public string domainKey;
        public string competencyKey;
        public int rank;
        public string eventKind;
        public string custodian;
        public string reason;
        public int mapId = -1;
        public int tileId = -1;
        public int tick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainKey, "domainKey");
            Scribe_Values.Look(ref competencyKey, "competencyKey");
            Scribe_Values.Look(ref rank, "rank", 0);
            Scribe_Values.Look(ref eventKind, "eventKind");
            Scribe_Values.Look(ref custodian, "custodian");
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref tileId, "tileId", -1);
            Scribe_Values.Look(ref tick, "tick", -1);
        }

        internal CATechnologyAvailabilityReceipt Copy()
        {
            return new CATechnologyAvailabilityReceipt
            {
                domainKey = domainKey,
                competencyKey = competencyKey,
                rank = rank,
                eventKind = eventKind,
                custodian = custodian,
                reason = reason,
                mapId = mapId,
                tileId = tileId,
                tick = tick
            };
        }
    }

    // This value is a sibling of Culture and Political Order on the faction.
    // Society authoring composes it; Society presets copy it. Pawn and archive
    // records describe where the same knowledge is available and never become
    // a second technology authority.
    public sealed class CATechnologicalKnowledge : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string id;
        public List<CATechnologyDomainKnowledge> domains =
            new List<CATechnologyDomainKnowledge>();
        public List<string> knownResearchProjects = new List<string>();
        public List<CATechnologyCustodyRecord> custody =
            new List<CATechnologyCustodyRecord>();
        public List<CATechnologyAvailabilityReceipt> availabilityHistory =
            new List<CATechnologyAvailabilityReceipt>();
        // Once initial pawn custody has been assigned, loss of every carrier is
        // real state. A later query must never recreate the starting carriers.
        public bool distributionInitialized;
        public CAOrigin origin = CAOrigin.Derived("ungenerated");
        public int revision;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref id, "id");
            Scribe_Collections.Look(ref domains, "domains", LookMode.Deep);
            Scribe_Collections.Look(ref knownResearchProjects,
                "knownResearchProjects", LookMode.Value);
            Scribe_Collections.Look(ref custody, "custody", LookMode.Deep);
            Scribe_Collections.Look(ref availabilityHistory,
                "availabilityHistory", LookMode.Deep);
            Scribe_Values.Look(ref distributionInitialized,
                "distributionInitialized", false);
            origin.Expose("origin");
            Scribe_Values.Look(ref revision, "revision", 0);
        }

        internal CATechnologicalKnowledge Copy(bool includeDistribution = true)
        {
            return new CATechnologicalKnowledge
            {
                schemaVersion = CurrentSchemaVersion,
                id = id,
                domains = (domains ?? new List<CATechnologyDomainKnowledge>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList(),
                knownResearchProjects = new List<string>(
                    knownResearchProjects ?? new List<string>()),
                custody = includeDistribution
                    ? (custody ?? new List<CATechnologyCustodyRecord>())
                        .Where(item => item != null)
                        .Select(item => item.Copy()).ToList()
                    : new List<CATechnologyCustodyRecord>(),
                availabilityHistory = includeDistribution
                    ? (availabilityHistory
                        ?? new List<CATechnologyAvailabilityReceipt>())
                        .Where(item => item != null)
                        .Select(item => item.Copy()).ToList()
                    : new List<CATechnologyAvailabilityReceipt>(),
                distributionInitialized = includeDistribution
                    && distributionInitialized,
                origin = origin,
                revision = includeDistribution ? revision : 0
            };
        }

        internal CATechnologicalKnowledge CopyAsPreset()
        {
            CATechnologicalKnowledge result = Copy(includeDistribution: false);
            result.id = null;
            return result;
        }

        internal void CopyFrom(CATechnologicalKnowledge source,
            bool includeDistribution = true)
        {
            if (source == null) return;
            string ownerId = id;
            CATechnologicalKnowledge copy = source.Copy(includeDistribution);
            schemaVersion = CurrentSchemaVersion;
            id = ownerId ?? copy.id;
            domains = copy.domains;
            knownResearchProjects = copy.knownResearchProjects;
            custody = copy.custody;
            availabilityHistory = copy.availabilityHistory;
            distributionInitialized = copy.distributionInitialized;
            origin = copy.origin;
            revision = copy.revision;
        }
    }

    public sealed class CATechnologyProfile
    {
        public readonly string Key;
        public readonly string Label;
        public readonly Dictionary<string, int> DomainRanks;

        public CATechnologyProfile(string key, string label,
            params object[] pairs)
        {
            Key = key;
            Label = label;
            DomainRanks = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                DomainRanks[(string)pairs[i]] = Convert.ToInt32(pairs[i + 1]);
        }
    }

    internal static class CATechnologicalKnowledgeModel
    {
        internal static readonly CATechnologyProfile[] Profiles =
        {
            P("subsistence", "Subsistence knowledge",
                1, 2, 1, 1, 0, 0, 0, 1, 1),
            P("agrarian", "Agrarian knowledge",
                1, 2, 2, 1, 1, 0, 1, 2, 2),
            P("early-modern", "Early modern knowledge",
                2, 2, 2, 2, 2, 0, 1, 2, 2),
            P("industrial", "Industrial knowledge",
                2, 3, 3, 3, 3, 2, 2, 3, 3),
            P("electrified-industrial", "Electrified industrial knowledge",
                3, 3, 3, 3, 3, 3, 3, 3, 3),
            P("advanced-industrial", "Advanced industrial knowledge",
                4, 4, 4, 4, 4, 4, 4, 4, 4),
            P("spacer", "Spacer knowledge",
                5, 5, 5, 5, 5, 5, 5, 5, 5)
        };

        internal static void Ensure(CATechnologicalKnowledge value,
            string identity, string defaultProfile = "subsistence")
        {
            if (value == null) return;
            if (value.id.NullOrEmpty()) value.id = StableId(identity);
            if (value.domains == null)
                value.domains = new List<CATechnologyDomainKnowledge>();
            if (value.domains.Count == 0)
                ApplyProfile(value, defaultProfile, CAAxisSource.Generated,
                    "generated starting knowledge");
            Normalize(value);
        }

        internal static void Normalize(CATechnologicalKnowledge value)
        {
            if (value == null) return;
            value.schemaVersion = CATechnologicalKnowledge.CurrentSchemaVersion;
            value.domains = (value.domains
                    ?? new List<CATechnologyDomainKnowledge>())
                .Where(item => item != null
                    && CATechnologyDomains.Find(item.domainKey) != null)
                .GroupBy(item => item.domainKey, StringComparer.Ordinal)
                .Select(group => group.First()).ToList();
            foreach (CATechnologyDomainDef definition in CATechnologyDomains.All)
            {
                CATechnologyDomainKnowledge state = value.domains
                    .FirstOrDefault(item => item.domainKey == definition.Key);
                if (state == null)
                {
                    state = new CATechnologyDomainKnowledge
                    {
                        domainKey = definition.Key,
                        source = (byte)CAAxisSource.Generated,
                        provenance = "missing domain initialized explicitly"
                    };
                    value.domains.Add(state);
                }
                foreach (string competency in CATechnologyCompetencies.All)
                    state.SetRank(competency, state.Rank(competency));
            }
            value.domains = value.domains.OrderBy(item => Array.FindIndex(
                CATechnologyDomains.All, def => def.Key == item.domainKey))
                .ToList();
            value.knownResearchProjects = (value.knownResearchProjects
                    ?? new List<string>()).Where(item => !item.NullOrEmpty())
                .Distinct(StringComparer.Ordinal).OrderBy(item => item,
                    StringComparer.Ordinal).ToList();
            value.custody = value.custody
                ?? new List<CATechnologyCustodyRecord>();
            value.availabilityHistory = (value.availabilityHistory
                    ?? new List<CATechnologyAvailabilityReceipt>())
                .Where(item => item != null).ToList();
        }

        internal static string ValidationFailure(
            CATechnologicalKnowledge value, bool requireComplete = true)
        {
            if (value == null) return "technological knowledge is missing";
            if (value.schemaVersion != CATechnologicalKnowledge.CurrentSchemaVersion)
                return "technological knowledge schema is "
                    + value.schemaVersion;
            if (requireComplete && value.id.NullOrEmpty())
                return "technological knowledge has no faction identity";
            if (value.domains == null)
                return "technological knowledge domains are missing";
            foreach (CATechnologyDomainDef definition in CATechnologyDomains.All)
            {
                List<CATechnologyDomainKnowledge> matches = value.domains
                    .Where(item => item?.domainKey == definition.Key).ToList();
                if (matches.Count != 1)
                    return definition.Label + " must occur exactly once";
                foreach (string competency in CATechnologyCompetencies.All)
                {
                    int rank = matches[0].Rank(competency);
                    if (rank < 0 || rank > 5)
                        return definition.Label + " " + competency
                            + " rank is outside 0-5";
                }
            }
            if (value.domains.Any(item => item == null
                    || CATechnologyDomains.Find(item.domainKey) == null))
                return "technological knowledge contains an unknown domain";
            var custodyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CATechnologyCustodyRecord item in value.custody
                ?? new List<CATechnologyCustodyRecord>())
            {
                if (!ValidCustodyIdentity(item))
                    return "technological knowledge contains invalid custody";
                string key = item.domainKey + "|" + item.competencyKey + "|"
                    + item.kind + "|" + item.pawnThingId + "|"
                    + item.custodianKey + "|" + item.mapId + "|"
                    + item.tileId + "|" + item.acquiredAtTick;
                if (!custodyKeys.Add(key))
                    return "technological knowledge contains duplicate custody";
                bool unavailable = item.unavailableAtTick >= 0;
                if (item.accessible == unavailable)
                    return "technological knowledge custody availability is inconsistent";
                if (unavailable && item.unavailableReason.NullOrEmpty())
                    return "unavailable technological knowledge has no reason";
            }
            foreach (CATechnologyAvailabilityReceipt item in
                value.availabilityHistory
                    ?? new List<CATechnologyAvailabilityReceipt>())
            {
                if (item == null
                    || CATechnologyDomains.Find(item.domainKey) == null
                    || !CATechnologyCompetencies.All.Contains(
                        item.competencyKey)
                    || item.rank < 0 || item.rank > 5
                    || item.eventKind.NullOrEmpty()
                    || item.custodian.NullOrEmpty() || item.tick < 0)
                    return "technological knowledge contains an invalid availability receipt";
            }
            return null;
        }

        internal static CATechnologyProfile FindProfile(string key)
        {
            return Profiles.FirstOrDefault(item => item.Key == key);
        }

        internal static void ApplyProfile(CATechnologicalKnowledge target,
            string profileKey, CAAxisSource source, string provenance)
        {
            if (target == null) return;
            CATechnologyProfile profile = FindProfile(profileKey)
                ?? FindProfile("subsistence");
            string ownerId = target.id;
            target.domains = CATechnologyDomains.All.Select(definition =>
            {
                int rank = profile.DomainRanks.TryGetValue(definition.Key,
                    out int configured) ? configured : 0;
                return new CATechnologyDomainKnowledge
                {
                    domainKey = definition.Key,
                    understand = rank,
                    construct = rank,
                    operate = rank,
                    maintain = rank,
                    source = (byte)source,
                    provenance = provenance ?? profile.Label
                };
            }).ToList();
            target.id = ownerId;
            target.knownResearchProjects = new List<string>();
            target.custody = new List<CATechnologyCustodyRecord>();
            target.availabilityHistory =
                new List<CATechnologyAvailabilityReceipt>();
            target.distributionInitialized = false;
            target.origin = source == CAAxisSource.Authored
                ? CAOrigin.Authored(provenance ?? profile.Key)
                : CAOrigin.Derived(provenance ?? profile.Key);
            target.revision++;
            Normalize(target);
        }

        internal static void SeedFromEngineTemplate(
            CATechnologicalKnowledge target, FactionDef template,
            string identity)
        {
            if (target == null || target.domains?.Count > 0) return;
            string profile = ProfileFor(template?.techLevel
                ?? TechLevel.Neolithic);
            ApplyProfile(target, profile, CAAxisSource.Generated,
                "initial knowledge inferred once from engine template "
                    + (template?.defName ?? "unknown"));
            Ensure(target, identity, profile);
        }

        // THE TWO LADDERS MUST AGREE. RankFor maps a TechLevel to a rank and
        // TechLevelForRank maps it back; seeding goes the long way round, from
        // a TechLevel to a profile to that profile's ranks. Those have to be
        // the same journey, and they were not: Spacer took the "spacer"
        // profile, whose nine domains are rank 5, and rank 5 reads back as
        // Ultra. So every Spacer faction was seeded as an Ultra builder --
        // observed on CannibalPirate, def techLevel Spacer, resolving
        // CanonicalBuildTechLevel = Ultra -- while "advanced-industrial",
        // the rank-4 profile that actually means Spacer, was unreachable
        // from any TechLevel.
        //
        // Ultra keeps the rank-5 profile; Spacer takes rank 4; and the
        // round-trip TechLevel -> profile -> rank -> TechLevel is identity
        // at every tier.
        internal static string ProfileFor(TechLevel level)
        {
            if (level >= TechLevel.Ultra) return "spacer";
            if (level >= TechLevel.Spacer) return "advanced-industrial";
            if (level >= TechLevel.Industrial)
                return "electrified-industrial";
            if (level >= TechLevel.Medieval) return "early-modern";
            return "subsistence";
        }

        internal static int Rank(CATechnologicalKnowledge value,
            string domain, string competency)
        {
            return value?.domains?.FirstOrDefault(item =>
                item?.domainKey == domain)?.Rank(competency) ?? 0;
        }

        internal static void SetRank(CATechnologicalKnowledge value,
            string domain, string competency, int rank,
            CAAxisSource source, string provenance)
        {
            if (value == null) return;
            Ensure(value, value.id ?? "technology:edited");
            CATechnologyDomainKnowledge state = value.domains
                .First(item => item.domainKey == domain);
            state.SetRank(competency, rank);
            state.source = (byte)source;
            state.provenance = provenance;
            value.origin = source == CAAxisSource.Authored
                ? CAOrigin.Authored(provenance)
                : CAOrigin.Derived(provenance);
            value.revision++;
        }

        internal static bool Matches(CATechnologicalKnowledge left,
            CATechnologicalKnowledge right)
        {
            if (left == null || right == null) return false;
            foreach (CATechnologyDomainDef domain in CATechnologyDomains.All)
                foreach (string competency in CATechnologyCompetencies.All)
                    if (Rank(left, domain.Key, competency)
                        != Rank(right, domain.Key, competency)) return false;
            IEnumerable<string> leftProjects = (left.knownResearchProjects
                    ?? new List<string>()).Where(item => !item.NullOrEmpty())
                .Distinct(StringComparer.Ordinal).OrderBy(item => item,
                    StringComparer.Ordinal);
            IEnumerable<string> rightProjects = (right.knownResearchProjects
                    ?? new List<string>()).Where(item => !item.NullOrEmpty())
                .Distinct(StringComparer.Ordinal).OrderBy(item => item,
                    StringComparer.Ordinal);
            return leftProjects.SequenceEqual(rightProjects,
                StringComparer.Ordinal);
        }

        internal static int CompatibilityTier(CATechnologicalKnowledge value)
        {
            if (value == null) return 0;
            int[] ranks = CATechnologyDomains.All.Select(domain =>
                Math.Min(Rank(value, domain.Key,
                        CATechnologyCompetencies.Construct),
                    Rank(value, domain.Key,
                        CATechnologyCompetencies.Maintain))).OrderBy(x => x)
                .ToArray();
            return ranks.Length == 0 ? 0 : Math.Max(0,
                Math.Min(3, ranks[ranks.Length / 2] - 1));
        }

        internal static TechLevel CompatibilityTechLevel(
            CATechnologicalKnowledge value)
        {
            int tier = CompatibilityTier(value);
            return tier >= 3 ? TechLevel.Spacer
                : tier >= 2 ? TechLevel.Industrial
                : tier >= 1 ? TechLevel.Medieval : TechLevel.Neolithic;
        }

        // The translation boundary's actual answer at these ranks - how
        // much of what a player can order built this knowledge supports,
        // and what the nearest gaps demand - computed through the same
        // resolver the runtime enforces and cached by the knowledge's own
        // revision. Shown wherever the knowledge is authored or chosen so
        // changing a rank has an immediately observable consequence.
        private static string constructionConsequence;
        private static int constructionConsequenceRevision = int.MinValue;
        private static object constructionConsequenceOwner;

        internal static string ConstructionConsequence(
            CATechnologicalKnowledge knowledge)
        {
            if (knowledge == null)
                return "Knowledge not set: everything orderable would "
                    + "fail its construction check.";
            if (ReferenceEquals(constructionConsequenceOwner, knowledge)
                && knowledge.revision == constructionConsequenceRevision
                && constructionConsequence != null)
                return constructionConsequence;
            int supported = 0;
            int blocked = 0;
            var blockedExamples = new System.Collections.Generic
                .List<string>();
            void Consider(BuildableDef definition)
            {
                if (definition?.designationCategory == null) return;
                if (CATechnologicalKnowledgeRuntime.CanConstructCanonical(
                        knowledge, definition,
                        out CATechnologyRequirement missing))
                    supported++;
                else
                {
                    blocked++;
                    if (blockedExamples.Count < 3 && missing != null)
                        blockedExamples.Add(definition.label + " (needs "
                            + (missing.DomainKey.NullOrEmpty()
                                ? "" : missing.DomainKey + " ")
                            + missing.CompetencyKey + " "
                            + missing.Rank + ")");
                }
            }
            foreach (ThingDef definition in
                DefDatabase<ThingDef>.AllDefsListForReading)
                Consider(definition);
            foreach (TerrainDef definition in
                DefDatabase<TerrainDef>.AllDefsListForReading)
                Consider(definition);
            constructionConsequence = "At these ranks: " + supported
                + " of " + (supported + blocked) + " orderable "
                + "constructions are supported"
                + (blockedExamples.Count == 0 ? "."
                    : "; among the gaps: "
                        + string.Join("; ", blockedExamples) + ".");
            constructionConsequenceRevision = knowledge.revision;
            constructionConsequenceOwner = knowledge;
            return constructionConsequence;
        }

        internal static string Summary(CATechnologicalKnowledge value)
        {
            if (value == null || value.domains == null
                || value.domains.Count == 0) return "Not set";
            var groups = value.domains.GroupBy(item => new[]
                {
                    item.understand, item.construct, item.operate,
                    item.maintain
                }.Min()).OrderByDescending(group => group.Key).ToList();
            var parts = new List<string>();
            foreach (IGrouping<int, CATechnologyDomainKnowledge> group in groups)
            {
                string labels = group.Select(item =>
                    CATechnologyDomains.Find(item.domainKey)?.Label)
                    .Where(item => !item.NullOrEmpty()).Take(3)
                    .ToCommaList(useAnd: true);
                if (!labels.NullOrEmpty())
                    parts.Add(RankLabel(group.Key) + " " + labels.ToLower());
                if (parts.Count == 2) break;
            }
            return string.Join("; ", parts).CapitalizeFirst();
        }

        internal static string RankLabel(int rank)
        {
            switch (rank)
            {
                case 0: return "no established";
                case 1: return "basic";
                case 2: return "developed";
                case 3: return "industrial";
                case 4: return "advanced";
                default: return "spacer";
            }
        }

        private static bool ValidCustodyIdentity(
            CATechnologyCustodyRecord item)
        {
            if (item == null || CATechnologyDomains.Find(item.domainKey) == null
                || !CATechnologyCompetencies.All.Contains(item.competencyKey)
                || item.rank < 0 || item.rank > 5) return false;
            if (item.kind == CATechnologyCustodyKind.Pawn)
                return item.pawnThingId >= 0;
            return !item.custodianKey.NullOrEmpty()
                && (item.mapId >= 0 || item.tileId >= 0);
        }

        private static CATechnologyProfile P(string key, string label,
            params int[] ranks)
        {
            var pairs = new List<object>();
            for (int i = 0; i < CATechnologyDomains.All.Length; i++)
            {
                pairs.Add(CATechnologyDomains.All[i].Key);
                pairs.Add(i < ranks.Length ? ranks[i] : 0);
            }
            return new CATechnologyProfile(key, label, pairs.ToArray());
        }

        private static string StableId(string seed)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in seed ?? "technology")
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return "CA-TK-" + hash.ToString("X8");
            }
        }
    }

    public sealed class CATechnologyRequirement
    {
        public string DomainKey;
        public string CompetencyKey;
        public int Rank;
        public string SourceKey;
        public bool ExplicitlyMapped;
        // Requirements with the same non-empty alternative group are OR
        // routes. Requirements without a group are all required.
        public string AlternativeGroupKey;
    }

    // Every consumer reaches technological knowledge through this translation
    // boundary. Native research and tech metadata describe requirements or
    // acquisition evidence; FactionDef never supplies effective capability.
    internal static class CATechnologyRequirementResolver
    {
        // This table is intentionally exact. A lexical match can make an
        // unrelated modded project grant capability merely because its name
        // happens to contain "ship", "gun", or "plant". New native or modded
        // projects therefore fail closed until their domain contract is added
        // here and covered by the mapping receipt.
        private static readonly Dictionary<string, string[]> ResearchDomains =
            BuildResearchDomains();

        internal static IReadOnlyList<CATechnologyRequirement> ForResearch(
            ResearchProjectDef project)
        {
            if (project == null) return Array.Empty<CATechnologyRequirement>();
            string[] domains = null;
            bool explicitMap = !project.defName.NullOrEmpty()
                && ResearchDomains.TryGetValue(project.defName, out domains)
                && domains != null && domains.Length > 0;
            int rank = RankFor(project.techLevel);
            if (!explicitMap)
                return new[] { new CATechnologyRequirement
                {
                    DomainKey = null,
                    CompetencyKey = CATechnologyCompetencies.Understand,
                    Rank = rank,
                    SourceKey = project.defName,
                    ExplicitlyMapped = false
                }};
            return domains.Select(domain => new CATechnologyRequirement
            {
                DomainKey = domain,
                CompetencyKey = CATechnologyCompetencies.Understand,
                Rank = rank,
                SourceKey = project.defName,
                ExplicitlyMapped = explicitMap
            }).GroupBy(item => item.DomainKey).Select(group => group.First())
                .ToList();
        }

        internal static IReadOnlyList<CATechnologyRequirement> ForBuildable(
            BuildableDef definition)
        {
            if (definition == null)
                return Array.Empty<CATechnologyRequirement>();
            int practicalRank = Math.Max(
                SkillRank(definition.constructionSkillPrerequisite),
                SkillRank(definition.artisticSkillPrerequisite));
            var result = new List<CATechnologyRequirement>
            {
                Direct(CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Construct,
                    Math.Max(practicalRank,
                        RankFor(definition.minTechLevelToBuild)),
                    definition.defName)
            };
            foreach (ResearchProjectDef project in
                definition.researchPrerequisites
                    ?? new List<ResearchProjectDef>())
                result.AddRange(ForResearch(project).Select(item =>
                    Requirement(item, CATechnologyCompetencies.Construct)));
            return Merge(result);
        }

        internal static IReadOnlyList<CATechnologyRequirement> ForWeapon(
            ThingDef weapon)
        {
            if (weapon?.IsWeapon != true)
                return Array.Empty<CATechnologyRequirement>();
            var result = new List<CATechnologyRequirement>
            {
                Direct(CATechnologyDomains.Weapons,
                    CATechnologyCompetencies.Operate,
                    RankFor(weapon.techLevel), weapon.defName)
            };
            foreach (ResearchProjectDef project in
                weapon.researchPrerequisites
                    ?? new List<ResearchProjectDef>())
                result.AddRange(ForResearch(project).Select(item =>
                    Requirement(item, CATechnologyCompetencies.Operate)));
            return Merge(result);
        }

        internal static IReadOnlyList<CATechnologyRequirement>
            ForManufacturedThing(ThingDef definition)
        {
            if (definition == null)
                return Array.Empty<CATechnologyRequirement>();
            string primaryDomain = definition.IsWeapon
                ? CATechnologyDomains.Weapons
                : definition.IsMedicine
                    ? CATechnologyDomains.Medicine
                    : CATechnologyDomains.Manufacturing;
            int practicalRank = definition.recipeMaker?.skillRequirements
                ?.Count > 0
                ? SkillRank(definition.recipeMaker.skillRequirements
                    .Max(item => item?.minLevel ?? 0))
                : 1;
            var result = new List<CATechnologyRequirement>
            {
                Direct(primaryDomain,
                    CATechnologyCompetencies.Construct,
                    Math.Max(practicalRank, RankFor(definition.techLevel)),
                    definition.defName)
            };
            var projects = new List<ResearchProjectDef>();
            if (definition.recipeMaker?.researchPrerequisite != null)
                projects.Add(definition.recipeMaker.researchPrerequisite);
            if (definition.recipeMaker?.researchPrerequisites != null)
                projects.AddRange(
                    definition.recipeMaker.researchPrerequisites);
            if (definition.researchPrerequisites != null)
                projects.AddRange(definition.researchPrerequisites);
            foreach (ResearchProjectDef project in projects.Distinct())
                result.AddRange(ForResearch(project).Select(item =>
                    Requirement(item, CATechnologyCompetencies.Construct)));
            return Merge(result);
        }

        internal static IReadOnlyList<CATechnologyRequirement> ForMaintenance(
            BuildableDef definition)
        {
            return ForBuildable(definition).Select(item => Requirement(item,
                CATechnologyCompetencies.Maintain)).ToList();
        }

        internal static IReadOnlyList<CATechnologyRequirement> ForMedicalCare()
        {
            return new[] { Direct(CATechnologyDomains.Medicine,
                CATechnologyCompetencies.Operate, 1, "ordinary medical care") };
        }

        internal static IReadOnlyList<CATechnologyRequirement> ForRecipe(
            RecipeDef recipe)
        {
            if (recipe == null) return Array.Empty<CATechnologyRequirement>();
            var projects = new List<ResearchProjectDef>();
            if (recipe.researchPrerequisite != null)
                projects.Add(recipe.researchPrerequisite);
            if (recipe.researchPrerequisites != null)
                projects.AddRange(recipe.researchPrerequisites);
            var result = projects.Distinct().SelectMany(ForResearch)
                .Select(item => Requirement(item,
                    CATechnologyCompetencies.Operate)).ToList();
            foreach (string domain in RecipeDomains(recipe))
                result.Add(Direct(domain,
                    CATechnologyCompetencies.Operate,
                    RecipeRank(recipe), recipe.defName));
            return Merge(result);
        }

        internal static IReadOnlyList<CATechnologyRequirement> ForPlant(
            ThingDef plant)
        {
            if (plant?.plant == null)
                return Array.Empty<CATechnologyRequirement>();
            IEnumerable<ResearchProjectDef> projects =
                plant.plant.sowResearchPrerequisites
                    ?? Enumerable.Empty<ResearchProjectDef>();
            var result = projects.Distinct().SelectMany(ForResearch)
                .Select(item => Requirement(item,
                    CATechnologyCompetencies.Operate)).ToList();
            result.Add(Direct(CATechnologyDomains.Agriculture,
                CATechnologyCompetencies.Operate,
                SkillRank(plant.plant.sowMinSkill), plant.defName));
            return Merge(result);
        }

        internal static IReadOnlyList<CATechnologyRequirement> ForHabitat(
            CAHabitatRequirement requirement)
        {
            switch (requirement)
            {
                case CAHabitatRequirement.EnclosedShelter:
                    return One(CATechnologyDomains.Construction,
                        CATechnologyCompetencies.Construct, 1, requirement);
                case CAHabitatRequirement.ThermalControl:
                    return Either(CATechnologyDomains.Construction,
                        CATechnologyDomains.Electrical,
                        CATechnologyCompetencies.Maintain, 2, requirement);
                case CAHabitatRequirement.ReliableFood:
                    return Either(CATechnologyDomains.Agriculture,
                        CATechnologyDomains.Logistics,
                        CATechnologyCompetencies.Operate, 1, requirement);
                case CAHabitatRequirement.SecuredFoodSupply:
                    return Either(CATechnologyDomains.Agriculture,
                        CATechnologyDomains.Logistics,
                        CATechnologyCompetencies.Operate, 2, requirement);
                case CAHabitatRequirement.FoodReserve:
                    return One(CATechnologyDomains.Logistics,
                        CATechnologyCompetencies.Maintain, 1, requirement);
                case CAHabitatRequirement.MedicalCare:
                    return One(CATechnologyDomains.Medicine,
                        CATechnologyCompetencies.Operate, 2, requirement);
                case CAHabitatRequirement.WaterTreatment:
                    return Either(CATechnologyDomains.Chemistry,
                        CATechnologyDomains.Construction,
                        CATechnologyCompetencies.Operate, 2, requirement);
                case CAHabitatRequirement.ArtificialLight:
                    return One(CATechnologyDomains.Electrical,
                        CATechnologyCompetencies.Maintain, 2, requirement);
                case CAHabitatRequirement.HazardProtection:
                    return Two(CATechnologyDomains.Medicine,
                        CATechnologyDomains.Chemistry,
                        CATechnologyCompetencies.Operate, 3, requirement);
                case CAHabitatRequirement.BreathableInterior:
                    return Two(CATechnologyDomains.Construction,
                        CATechnologyDomains.Electrical,
                        CATechnologyCompetencies.Maintain, 4, requirement);
                default:
                    return Array.Empty<CATechnologyRequirement>();
            }
        }

        internal static int RankFor(TechLevel level)
        {
            if (level >= TechLevel.Ultra) return 5;
            if (level >= TechLevel.Spacer) return 4;
            if (level >= TechLevel.Industrial) return 3;
            if (level >= TechLevel.Medieval) return 2;
            return 1;
        }

        private static CATechnologyRequirement Direct(string domain,
            string competency, int rank, string source)
        {
            return new CATechnologyRequirement
            {
                DomainKey = domain,
                CompetencyKey = competency,
                Rank = Math.Max(1, Math.Min(5, rank)),
                SourceKey = source,
                ExplicitlyMapped = true
            };
        }

        private static int SkillRank(int skill)
        {
            if (skill >= 16) return 4;
            if (skill >= 11) return 3;
            if (skill >= 6) return 2;
            return 1;
        }

        private static int RecipeRank(RecipeDef recipe)
        {
            int skill = recipe?.skillRequirements?.Count > 0
                ? recipe.skillRequirements.Max(item => item?.minLevel ?? 0)
                : 0;
            return SkillRank(skill);
        }

        private static IEnumerable<string> RecipeDomains(RecipeDef recipe)
        {
            if (recipe?.IsSurgery == true)
                return new[] { CATechnologyDomains.Medicine };
            string work = recipe?.requiredGiverWorkType?.defName
                ?? recipe?.workSkill?.defName ?? string.Empty;
            switch (work)
            {
                case "Doctor":
                case "Medicine":
                    return new[] { CATechnologyDomains.Medicine };
                case "Cooking":
                    return new[] { CATechnologyDomains.Logistics };
                case "Growing":
                case "PlantCutting":
                case "Handling":
                case "Plants":
                case "Animals":
                    return new[] { CATechnologyDomains.Agriculture };
                case "Construction":
                    return new[] { CATechnologyDomains.Construction };
                case "Mining":
                    return new[] { CATechnologyDomains.Metallurgy };
                case "Smithing":
                    return new[]
                    {
                        CATechnologyDomains.Metallurgy,
                        CATechnologyDomains.Manufacturing
                    };
                case "Hunting":
                case "Shooting":
                case "Melee":
                    return new[] { CATechnologyDomains.Weapons };
                case "Hauling":
                case "Cleaning":
                    return new[] { CATechnologyDomains.Logistics };
                default:
                    return new[] { CATechnologyDomains.Manufacturing };
            }
        }

        private static Dictionary<string, string[]> BuildResearchDomains()
        {
            var result = new Dictionary<string, List<string>>(
                StringComparer.Ordinal);
            AddResearch(result, CATechnologyDomains.Agriculture,
                "Brewing", "Cocoa", "Devilstrand", "FertilityProcedures",
                "Fishing", "Hydroponics", "Pemmican", "PsychiteRefining",
                "PsychoidBrewing", "TreeSowing");
            AddResearch(result, CATechnologyDomains.Chemistry,
                "BiofuelRefining", "DrugProduction", "Firefoam",
                "GasOperation", "MedicineProduction", "ToxFiltration",
                "ToxGas", "ToxifierGenerator", "VenomSynthesis",
                "WastepackAtomizer");
            AddResearch(result, CATechnologyDomains.Construction,
                "ComplexFurniture", "HeavyBridges", "MoisturePump",
                "PassiveCooler", "Stonecutting");
            AddResearch(result, CATechnologyDomains.Electrical,
                "AdvancedGravtech", "AirConditioning", "Autodoors",
                "BasicGravtech", "BasicMechtech", "Batteries",
                "ColoredLights", "Electricity", "FlatscreenTelevision",
                "GeothermalPower", "GroundPenetratingScanner",
                "HighMechtech", "HunterDrones", "MicroelectronicsBasics",
                "OrbitalTech", "PoweredArmor", "ShipBasics",
                "ShipComputerCore", "ShipCryptosleep", "ShipEngine",
                "ShipReactor", "ShipSensorCluster", "Shuttles",
                "SolarPanels", "StandardGravtech", "StandardMechtech",
                "TubeTelevision", "UltraMechtech", "WatermillGenerator");
            AddResearch(result, CATechnologyDomains.Logistics,
                "NutrientPaste", "OrbitalTech", "PackagedSurvivalMeal",
                "ShipBasics", "ShipComputerCore", "ShipCryptosleep",
                "ShipEngine", "ShipReactor", "ShipSensorCluster",
                "Shuttles", "TransportPod");
            AddResearch(result, CATechnologyDomains.Manufacturing,
                "AdvancedFabrication", "BasicMechtech", "CarpetMaking",
                "ComplexClothing", "DrugProduction", "Fabrication",
                "GoJuiceProduction", "Harp", "Harpsichord",
                "HighMechtech", "MedicineProduction", "NobleApparel",
                "PenoxycylineProduction", "Piano", "RoyalApparel",
                "StandardMechtech", "UltraMechtech",
                "WakeUpProduction");
            AddResearch(result, CATechnologyDomains.Medicine,
                "Archogenetics", "ArtificialMetabolism", "Bionics",
                "Bioregeneration", "Biosculpting", "BrainWiring",
                "CircadianInfluence", "Cryptosleep", "Deathrest",
                "FleshShaping", "GeneProcessor", "GrowthVats",
                "HealingFactors", "HospitalBed", "MedicineProduction",
                "NeuralComputation", "NeuralSupercharger", "Prosthetics",
                "ShipCryptosleep", "SkinHardening", "SpecializedLimbs",
                "VitalsMonitor", "Xenogermination");
            AddResearch(result, CATechnologyDomains.Metallurgy,
                "AdvancedFabrication", "CataphractArmor", "DeepDrilling",
                "Fabrication", "FlakArmor", "GroundPenetratingScanner",
                "Gunsmithing", "LongRangeMineralScanner", "Machining",
                "MolecularAnalysis", "MultiAnalyzer", "PlateArmor",
                "PoweredArmor", "ReconArmor", "Smithing",
                "SterileMaterials");
            AddResearch(result, CATechnologyDomains.Weapons,
                "BeamWeapons", "BlowbackOperation", "CataphractArmor",
                "ChargedShot", "CompactWeaponry", "FlakArmor",
                "FoamTurret", "Greatbow", "Gunlink", "Gunsmithing",
                "GunTurrets", "HeavyTurrets", "HunterDrones", "IEDs",
                "JumpPack", "LongBlades", "Mortars",
                "MultibarrelWeapons", "PlateArmor", "PoweredArmor",
                "PrecisionRifling", "ReconArmor", "RecurveBow",
                "RocketswarmLauncher", "ShieldBelt", "SmokepopBelt",
                "SniperTurret");
            return result.ToDictionary(pair => pair.Key,
                pair => pair.Value.Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        }

        private static void AddResearch(
            IDictionary<string, List<string>> result, string domain,
            params string[] projects)
        {
            foreach (string project in projects)
            {
                List<string> domains;
                if (!result.TryGetValue(project, out domains))
                {
                    domains = new List<string>();
                    result.Add(project, domains);
                }
                domains.Add(domain);
            }
        }

        private static CATechnologyRequirement Requirement(
            CATechnologyRequirement source, string competency)
        {
            return new CATechnologyRequirement
            {
                DomainKey = source.DomainKey,
                CompetencyKey = competency,
                Rank = source.Rank,
                SourceKey = source.SourceKey,
                ExplicitlyMapped = source.ExplicitlyMapped,
                AlternativeGroupKey = source.AlternativeGroupKey
            };
        }

        private static IReadOnlyList<CATechnologyRequirement> One(
            string domain, string competency, int rank,
            CAHabitatRequirement source)
        {
            return new[] { new CATechnologyRequirement
            {
                DomainKey = domain,
                CompetencyKey = competency,
                Rank = rank,
                SourceKey = source.ToString(),
                ExplicitlyMapped = true
            }};
        }

        private static IReadOnlyList<CATechnologyRequirement> Two(
            string first, string second, string competency, int rank,
            CAHabitatRequirement source)
        {
            return new[]
            {
                new CATechnologyRequirement
                {
                    DomainKey = first,
                    CompetencyKey = competency,
                    Rank = rank,
                    SourceKey = source.ToString(),
                    ExplicitlyMapped = true
                },
                new CATechnologyRequirement
                {
                    DomainKey = second,
                    CompetencyKey = competency,
                    Rank = rank,
                    SourceKey = source.ToString(),
                    ExplicitlyMapped = true
                }
            };
        }

        private static IReadOnlyList<CATechnologyRequirement> Either(
            string first, string second, string competency, int rank,
            CAHabitatRequirement source)
        {
            string group = "habitat:" + source;
            return Two(first, second, competency, rank, source)
                .Select(item =>
                {
                    item.AlternativeGroupKey = group;
                    return item;
                }).ToArray();
        }

        private static IReadOnlyList<CATechnologyRequirement> Merge(
            IEnumerable<CATechnologyRequirement> requirements)
        {
            return requirements.GroupBy(item => item.DomainKey + "|"
                    + item.CompetencyKey, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(item => item.Rank)
                    .First()).ToList();
        }
    }

    internal static class CATechnologicalKnowledgeAvailability
    {
        internal static int EffectiveRank(CATechnologicalKnowledge knowledge,
            string domain, string competency, bool distributed,
            Func<int, bool> pawnAvailable = null)
        {
            return EffectiveRank(knowledge, domain, competency, distributed,
                pawnAvailable, null);
        }

        internal static int EffectiveRank(CATechnologicalKnowledge knowledge,
            string domain, string competency, bool distributed,
            Func<int, bool> pawnAvailable,
            Func<CATechnologyCustodyRecord, bool> retainedAvailable)
        {
            int canonical = CATechnologicalKnowledgeModel.Rank(knowledge,
                domain, competency);
            if (!distributed) return canonical;
            IEnumerable<CATechnologyCustodyRecord> records =
                (knowledge?.custody ?? new List<CATechnologyCustodyRecord>())
                .Where(item => item != null && item.accessible
                    && item.unavailableAtTick < 0
                    && item.domainKey == domain
                    && item.competencyKey == competency);
            int available = 0;
            foreach (CATechnologyCustodyRecord item in records)
            {
                if (item.kind == CATechnologyCustodyKind.Pawn
                    && (pawnAvailable == null
                        || !pawnAvailable(item.pawnThingId))) continue;
                if (item.kind != CATechnologyCustodyKind.Pawn
                    && retainedAvailable != null
                    && !retainedAvailable(item)) continue;
                available = Math.Max(available, item.rank);
            }
            return Math.Min(canonical, available);
        }

        internal static bool Satisfies(CATechnologicalKnowledge knowledge,
            IEnumerable<CATechnologyRequirement> requirements,
            bool distributed, Func<int, bool> pawnAvailable,
            out CATechnologyRequirement missing)
        {
            return Satisfies(knowledge, requirements, distributed,
                pawnAvailable, null, out missing);
        }

        internal static bool Satisfies(CATechnologicalKnowledge knowledge,
            IEnumerable<CATechnologyRequirement> requirements,
            bool distributed, Func<int, bool> pawnAvailable,
            Func<CATechnologyCustodyRecord, bool> retainedAvailable,
            out CATechnologyRequirement missing)
        {
            missing = null;
            List<CATechnologyRequirement> all = (requirements
                    ?? Enumerable.Empty<CATechnologyRequirement>()).ToList();
            CATechnologyRequirement unmapped = all.FirstOrDefault(item =>
                item == null || !item.ExplicitlyMapped
                    || CATechnologyDomains.Find(item.DomainKey) == null);
            if (unmapped != null)
            {
                missing = unmapped;
                return false;
            }
            foreach (CATechnologyRequirement requirement in all.Where(item =>
                item != null && item.AlternativeGroupKey.NullOrEmpty()))
                if (EffectiveRank(knowledge, requirement.DomainKey,
                        requirement.CompetencyKey, distributed, pawnAvailable,
                        retainedAvailable)
                    < requirement.Rank)
                {
                    missing = requirement;
                    return false;
                }
            foreach (IGrouping<string, CATechnologyRequirement> alternatives
                in all.Where(item => item != null
                    && !item.AlternativeGroupKey.NullOrEmpty())
                    .GroupBy(item => item.AlternativeGroupKey,
                        StringComparer.Ordinal))
            {
                if (alternatives.Any(requirement => EffectiveRank(knowledge,
                        requirement.DomainKey, requirement.CompetencyKey,
                        distributed, pawnAvailable, retainedAvailable)
                    >= requirement.Rank))
                    continue;
                missing = alternatives.First();
                return false;
            }
            return true;
        }
    }

    internal static class CATechnologicalKnowledgeRuntime
    {
        internal static bool DistributedEnabled =>
            AwarenessMod.Settings?.experimentalDistributedKnowledge == true;

        internal static void InitializeCurrentFactionDistribution()
        {
            if (!DistributedEnabled || Find.FactionManager == null) return;
            foreach (Faction faction in Find.FactionManager
                .AllFactionsListForReading)
            {
                if (!CAFactionStateGenerator.UsesFactionState(faction))
                    continue;
                CATechnologicalKnowledge knowledge = ForFaction(faction);
                EnsurePawnDistribution(faction, knowledge);
            }
        }

        internal static CATechnologicalKnowledge ForFaction(Faction faction,
            bool ensure = true)
        {
            if (!CAFactionStateGenerator.UsesFactionState(faction))
                return null;
            CAFactionStateWorldComponent owner =
                CAFactionStateWorldComponent.Current;
            CAFactionState state = ensure ? owner?.EnsureFor(faction)
                : owner?.Find(faction);
            if (state == null) return null;
            if (state.technologicalKnowledge == null)
                state.technologicalKnowledge = new CATechnologicalKnowledge();
            if (ensure)
            {
                CATechnologicalKnowledgeModel.SeedFromEngineTemplate(
                    state.technologicalKnowledge, faction.def,
                    "faction:" + faction.loadID);
                CATechnologicalKnowledgeModel.Ensure(
                    state.technologicalKnowledge,
                    "faction:" + faction.loadID);
            }
            return state.technologicalKnowledge;
        }

        internal static int EffectiveRank(Faction faction, string domain,
            string competency, Map map = null)
        {
            if (!CAFactionStateGenerator.UsesFactionState(faction))
                return CATechnologyRequirementResolver.RankFor(
                    faction?.def?.techLevel ?? TechLevel.Neolithic);
            CATechnologicalKnowledge knowledge = ForFaction(faction);
            return CATechnologicalKnowledgeAvailability.EffectiveRank(
                knowledge, domain, competency, DistributedEnabled,
                pawnId => PawnAvailable(faction, pawnId, map),
                record => RetainedKnowledgeAvailable(record, map));
        }

        internal static bool Satisfies(Faction faction,
            IEnumerable<CATechnologyRequirement> requirements,
            out CATechnologyRequirement missing, Map map = null)
        {
            if (!CAFactionStateGenerator.UsesFactionState(faction))
            {
                missing = null;
                return true;
            }
            CATechnologicalKnowledge knowledge = ForFaction(faction);
            return CATechnologicalKnowledgeAvailability.Satisfies(knowledge,
                requirements, DistributedEnabled,
                pawnId => PawnAvailable(faction, pawnId, map),
                record => RetainedKnowledgeAvailable(record, map),
                out missing);
        }

        internal static bool SatisfiesCanonical(Faction faction,
            IEnumerable<CATechnologyRequirement> requirements,
            out CATechnologyRequirement missing)
        {
            if (!CAFactionStateGenerator.UsesFactionState(faction))
            {
                missing = null;
                return true;
            }
            return CATechnologicalKnowledgeAvailability.Satisfies(
                ForFaction(faction), requirements, false, null, null,
                out missing);
        }

        internal static bool SatisfiesCanonical(
            CATechnologicalKnowledge knowledge,
            IEnumerable<CATechnologyRequirement> requirements,
            out CATechnologyRequirement missing)
        {
            return CATechnologicalKnowledgeAvailability.Satisfies(
                knowledge, requirements, false, null, null, out missing);
        }

        internal static int CanonicalRank(Faction faction, string domain,
            string competency)
        {
            if (!CAFactionStateGenerator.UsesFactionState(faction))
                return CATechnologyRequirementResolver.RankFor(
                    faction?.def?.techLevel ?? TechLevel.Neolithic);
            return CATechnologicalKnowledgeModel.Rank(ForFaction(faction),
                domain, competency);
        }

        internal static int CanonicalRank(
            CATechnologicalKnowledge knowledge, string domain,
            string competency)
        {
            return CATechnologicalKnowledgeModel.Rank(knowledge, domain,
                competency);
        }

        internal static int CompatibilityTier(Faction faction,
            Map map = null)
        {
            CATechnologicalKnowledge knowledge = ForFaction(faction);
            if (knowledge == null)
                return Math.Max(0, Math.Min(3,
                    CATechnologyRequirementResolver.RankFor(
                        faction?.def?.techLevel ?? TechLevel.Neolithic) - 1));
            if (!DistributedEnabled)
                return CATechnologicalKnowledgeModel.CompatibilityTier(
                    knowledge);
            int[] ranks = CATechnologyDomains.All.Select(domain => Math.Min(
                EffectiveRank(faction, domain.Key,
                    CATechnologyCompetencies.Construct, map),
                EffectiveRank(faction, domain.Key,
                    CATechnologyCompetencies.Maintain, map)))
                .OrderBy(value => value).ToArray();
            return ranks.Length == 0 ? 0 : Math.Max(0,
                Math.Min(3, ranks[ranks.Length / 2] - 1));
        }

        internal static int CanonicalCompatibilityTier(Faction faction)
        {
            CATechnologicalKnowledge knowledge = ForFaction(faction);
            return knowledge == null
                ? Math.Max(0, Math.Min(3,
                    CATechnologyRequirementResolver.RankFor(
                        faction?.def?.techLevel ?? TechLevel.Neolithic) - 1))
                : CATechnologicalKnowledgeModel.CompatibilityTier(knowledge);
        }

        internal static bool CanConstruct(Faction faction,
            BuildableDef definition, out CATechnologyRequirement missing,
            Map map = null)
        {
            return Satisfies(faction,
                CATechnologyRequirementResolver.ForBuildable(definition),
                out missing, map);
        }

        internal static bool CanConstructCanonical(Faction faction,
            BuildableDef definition, out CATechnologyRequirement missing)
        {
            return SatisfiesCanonical(faction,
                CATechnologyRequirementResolver.ForBuildable(definition),
                out missing);
        }

        internal static bool CanConstructCanonical(
            CATechnologicalKnowledge knowledge, BuildableDef definition,
            out CATechnologyRequirement missing)
        {
            return SatisfiesCanonical(knowledge,
                CATechnologyRequirementResolver.ForBuildable(definition),
                out missing);
        }

        internal static bool CanOperate(Faction faction, RecipeDef recipe,
            out CATechnologyRequirement missing, Map map = null)
        {
            return Satisfies(faction,
                CATechnologyRequirementResolver.ForRecipe(recipe),
                out missing, map);
        }

        internal static bool CanManufacture(Faction faction,
            ThingDef definition, out CATechnologyRequirement missing,
            Map map = null)
        {
            return Satisfies(faction,
                CATechnologyRequirementResolver.ForManufacturedThing(
                    definition), out missing, map);
        }

        internal static bool CanManufactureCanonical(Faction faction,
            ThingDef definition, out CATechnologyRequirement missing)
        {
            return SatisfiesCanonical(faction,
                CATechnologyRequirementResolver.ForManufacturedThing(
                    definition), out missing);
        }

        internal static bool CanGrow(Faction faction, ThingDef plant,
            out CATechnologyRequirement missing, Map map = null)
        {
            return Satisfies(faction,
                CATechnologyRequirementResolver.ForPlant(plant),
                out missing, map);
        }

        internal static bool CanGrowCanonical(Faction faction, ThingDef plant,
            out CATechnologyRequirement missing)
        {
            return SatisfiesCanonical(faction,
                CATechnologyRequirementResolver.ForPlant(plant),
                out missing);
        }

        internal static bool CanGrowCanonical(
            CATechnologicalKnowledge knowledge, ThingDef plant,
            out CATechnologyRequirement missing)
        {
            return SatisfiesCanonical(knowledge,
                CATechnologyRequirementResolver.ForPlant(plant),
                out missing);
        }

        internal static bool CanUseWeapon(Faction faction, ThingDef weapon,
            out CATechnologyRequirement missing, Map map = null)
        {
            return Satisfies(faction,
                CATechnologyRequirementResolver.ForWeapon(weapon),
                out missing, map);
        }

        internal static bool CanMaintain(Faction faction,
            BuildableDef definition, out CATechnologyRequirement missing,
            Map map = null)
        {
            return Satisfies(faction,
                CATechnologyRequirementResolver.ForMaintenance(definition),
                out missing, map);
        }

        internal static bool CanMaintainCanonical(Faction faction,
            BuildableDef definition, out CATechnologyRequirement missing)
        {
            return SatisfiesCanonical(faction,
                CATechnologyRequirementResolver.ForMaintenance(definition),
                out missing);
        }

        internal static bool CanProvideMedicalCare(Faction faction,
            out CATechnologyRequirement missing, Map map = null)
        {
            return Satisfies(faction,
                CATechnologyRequirementResolver.ForMedicalCare(),
                out missing, map);
        }

        internal static bool Understands(Faction faction,
            ResearchProjectDef project, Map map = null)
        {
            return Satisfies(faction,
                CATechnologyRequirementResolver.ForResearch(project),
                out _, map);
        }

        internal static TechLevel EffectivePlayerTechLevel(
            FactionDef nativeTemplate)
        {
            Faction player = Faction.OfPlayer;
            return player == null ? nativeTemplate?.techLevel
                    ?? TechLevel.Undefined
                : EffectiveTechLevel(player);
        }

        internal static TechLevel EffectivePlayerBuildTechLevel(
            FactionDef nativeTemplate)
        {
            Faction player = Faction.OfPlayer;
            if (player == null) return nativeTemplate?.techLevel
                ?? TechLevel.Undefined;
            if (!CAFactionStateGenerator.UsesFactionState(player))
                return nativeTemplate?.techLevel
                    ?? player.def?.techLevel ?? TechLevel.Undefined;
            return EffectiveBuildTechLevel(player, Find.CurrentMap);
        }

        internal static TechLevel EffectiveBuildTechLevel(Faction faction,
            Map map = null)
        {
            int rank = Math.Min(
                EffectiveRank(faction, CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Construct, map),
                EffectiveRank(faction, CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Maintain, map));
            return TechLevelForRank(rank);
        }

        internal static TechLevel CanonicalBuildTechLevel(Faction faction)
        {
            int rank = Math.Min(
                CanonicalRank(faction, CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Construct),
                CanonicalRank(faction, CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Maintain));
            return TechLevelForRank(rank);
        }

        internal static TechLevel EffectiveResearchTechLevel(Faction faction,
            ResearchProjectDef project, Map map = null)
        {
            if (!CAFactionStateGenerator.UsesFactionState(faction))
                return faction?.def?.techLevel ?? TechLevel.Undefined;
            List<CATechnologyRequirement> requirements =
                CATechnologyRequirementResolver.ForResearch(project).ToList();
            if (requirements.Count == 0 || requirements.Any(item => item == null
                    || !item.ExplicitlyMapped
                    || CATechnologyDomains.Find(item.DomainKey) == null))
                return EffectiveTechLevel(faction);
            int rank = requirements.Min(item => EffectiveRank(faction,
                item.DomainKey, CATechnologyCompetencies.Understand, map));
            return TechLevelForRank(rank);
        }

        internal static TechLevel EffectiveTechLevel(Faction faction)
        {
            CATechnologicalKnowledge knowledge = ForFaction(faction,
                ensure: faction != null);
            if (knowledge == null)
                return faction?.def?.techLevel ?? TechLevel.Undefined;
            if (!DistributedEnabled)
                return CATechnologicalKnowledgeModel
                    .CompatibilityTechLevel(knowledge);
            int tier = CompatibilityTier(faction, Find.CurrentMap);
            return tier >= 3 ? TechLevel.Spacer
                : tier >= 2 ? TechLevel.Industrial
                : tier >= 1 ? TechLevel.Medieval : TechLevel.Neolithic;
        }

        private static TechLevel TechLevelForRank(int rank)
        {
            return rank >= 5 ? TechLevel.Ultra
                : rank >= 4 ? TechLevel.Spacer
                : rank >= 3 ? TechLevel.Industrial
                : rank >= 2 ? TechLevel.Medieval : TechLevel.Neolithic;
        }

        internal static void EnsurePawnDistribution(Faction faction,
            CATechnologicalKnowledge knowledge)
        {
            if (faction == null || knowledge == null) return;
            if (knowledge.distributionInitialized) return;
            List<Pawn> pawns = AllFactionPawns(faction)
                .Where(IsUsableCarrier).OrderBy(item => item.thingIDNumber)
                .ToList();
            if (pawns.Count == 0) return;
            knowledge.custody = knowledge.custody
                ?? new List<CATechnologyCustodyRecord>();
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (CATechnologyDomainDef domain in CATechnologyDomains.All)
            {
                List<Pawn> ordered = pawns.OrderByDescending(pawn =>
                        RelevantSkill(pawn, domain.Key))
                    .ThenBy(pawn => StableCarrierOrder(faction, pawn,
                        domain.Key)).ThenBy(pawn => pawn.thingIDNumber)
                    .ToList();
                foreach (string competency in CATechnologyCompetencies.All)
                {
                    int rank = CATechnologicalKnowledgeModel.Rank(knowledge,
                        domain.Key, competency);
                    if (rank <= 0) continue;
                    int carrierCount = rank >= 4 ? 1 : Math.Min(2,
                        ordered.Count);
                    for (int i = 0; i < carrierCount; i++)
                        AddCustody(knowledge, domain.Key, competency, rank,
                            CATechnologyCustodyKind.Pawn,
                            ordered[i].thingIDNumber, ordered[i].LabelShort,
                            now, "initial faction distribution");
                }
            }
            knowledge.distributionInitialized = true;
            knowledge.revision++;
        }

        internal static void AssignNewMemberCustody(Faction faction,
            Pawn pawn, CATechnologicalKnowledge knowledge)
        {
            if (faction == null || pawn?.Faction != faction
                || knowledge?.distributionInitialized != true
                || !IsUsableCarrier(pawn)) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            bool changed = false;
            foreach (CATechnologyDomainDef domain in CATechnologyDomains.All)
            {
                int skill = RelevantSkill(pawn, domain.Key);
                int qualifiedRank = Math.Max(1, Math.Min(5,
                    1 + skill / 5));
                foreach (string competency in CATechnologyCompetencies.All)
                {
                    int canonical = CATechnologicalKnowledgeModel.Rank(
                        knowledge, domain.Key, competency);
                    int rank = Math.Min(canonical, qualifiedRank);
                    if (rank <= 0) continue;
                    changed |= AddCustody(knowledge, domain.Key, competency, rank,
                        CATechnologyCustodyKind.Pawn, pawn.thingIDNumber,
                        pawn.LabelShort, now,
                        "member knowledge represented at entry");
                }
            }
            if (changed) knowledge.revision++;
        }

        internal static void RecordResearch(Faction faction,
            ResearchProjectDef project, Pawn researcher)
        {
            if (faction == null || project == null) return;
            CATechnologicalKnowledge knowledge = ForFaction(faction);
            if (knowledge == null) return;
            int originalRevision = knowledge.revision;
            bool changed = false;
            if (!knowledge.knownResearchProjects.Contains(project.defName))
            {
                knowledge.knownResearchProjects.Add(project.defName);
                changed = true;
            }
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (CATechnologyRequirement requirement in
                CATechnologyRequirementResolver.ForResearch(project))
            {
                if (!requirement.ExplicitlyMapped
                    || CATechnologyDomains.Find(requirement.DomainKey) == null)
                {
                    Log.ErrorOnce("[CA] No technological-knowledge mapping "
                        + "exists for research " + project.defName + ".",
                        project.defName.GetHashCode() ^ 0x4341544b);
                    continue;
                }
                foreach (string competency in CATechnologyCompetencies.All)
                {
                    int rank = requirement.Rank;
                    if (CATechnologicalKnowledgeModel.Rank(knowledge,
                            requirement.DomainKey, competency) < rank)
                    {
                        CATechnologicalKnowledgeModel.SetRank(knowledge,
                            requirement.DomainKey, competency, rank,
                            CAAxisSource.Generated,
                            "research completed: " + project.defName);
                        changed = true;
                    }
                    if (DistributedEnabled && researcher != null
                        && researcher.Faction == faction)
                        changed |= AddCustody(knowledge, requirement.DomainKey,
                            competency, rank, CATechnologyCustodyKind.Pawn,
                            researcher.thingIDNumber, researcher.LabelShort,
                            now, "researcher completed " + project.defName);
                    Map retainedAt = researcher?.Map;
                    if (retainedAt != null)
                        changed |= AddCustody(knowledge, requirement.DomainKey,
                            competency, rank,
                            CATechnologyCustodyKind.Record, -1,
                            "research:" + project.defName, now,
                            "faction research record",
                            retainedAt.uniqueID, retainedAt.Tile);
                }
            }
            if (!changed) return;
            // One completed project is one canonical state transition even
            // when it raises several domain/competency cells. Replaying
            // finished native research on load is therefore idempotent.
            knowledge.revision = originalRevision + 1;
            CATechnologicalKnowledgeModel.Normalize(knowledge);
        }

        internal static void SynchronizeFinishedResearch(Faction faction)
        {
            if (!CAFactionStateGenerator.UsesFactionState(faction)) return;
            foreach (ResearchProjectDef project in
                DefDatabase<ResearchProjectDef>.AllDefsListForReading)
                if (project?.IsFinished == true)
                    RecordResearch(faction, project, null);
        }

        internal static void TransferPawn(Pawn pawn, Faction oldFaction,
            Faction newFaction)
        {
            if (!DistributedEnabled
                || pawn?.RaceProps?.Humanlike != true
                || oldFaction == newFaction) return;
            if (oldFaction != null
                && !CAFactionStateGenerator.UsesFactionState(oldFaction))
                oldFaction = null;
            if (newFaction != null
                && !CAFactionStateGenerator.UsesFactionState(newFaction))
                newFaction = null;
            int now = Find.TickManager?.TicksGame ?? 0;
            CATechnologicalKnowledge oldKnowledge = oldFaction == null
                ? null : ForFaction(oldFaction, ensure: false);
            CATechnologicalKnowledge newKnowledge = newFaction == null
                ? null : ForFaction(newFaction);
            var sources = new List<CATechnologicalKnowledge>();
            if (oldKnowledge != null) sources.Add(oldKnowledge);
            else if (newKnowledge != null)
                sources.AddRange((CAFactionStateWorldComponent.Current
                        ?.FactionStates ?? Array.Empty<CAFactionState>())
                    .Select(state => state?.technologicalKnowledge)
                    .Where(value => value != null));
            List<CATechnologyCustodyRecord> carried = sources.SelectMany(
                    value => value.custody
                        ?? new List<CATechnologyCustodyRecord>())
                .Where(item => item?.kind == CATechnologyCustodyKind.Pawn
                    && item.pawnThingId == pawn.thingIDNumber
                    && (item.unavailableAtTick < 0
                        || item.unavailableReason == "departed"))
                .ToList();
            var changedSources = new HashSet<CATechnologicalKnowledge>();
            foreach (CATechnologyCustodyRecord record in carried)
            {
                CATechnologicalKnowledge source = sources.FirstOrDefault(
                    value => value.custody?.Contains(record) == true);
                if (record.unavailableAtTick < 0)
                {
                    record.unavailableAtTick = now;
                    record.unavailableReason = "departed";
                    record.accessible = false;
                    AppendReceipt(source, record, "departed",
                        "pawn changed faction", now);
                    if (source != null) changedSources.Add(source);
                }
                if (newKnowledge == null) continue;
                CATechnologicalKnowledgeModel.Ensure(newKnowledge,
                    "faction:" + newFaction.loadID);
                if (CATechnologicalKnowledgeModel.Rank(newKnowledge,
                        record.domainKey, record.competencyKey) < record.rank)
                    CATechnologicalKnowledgeModel.SetRank(newKnowledge,
                        record.domainKey, record.competencyKey, record.rank,
                        CAAxisSource.Generated,
                        "knowledge brought by recruited pawn "
                            + pawn.thingIDNumber);
                AddCustody(newKnowledge, record.domainKey,
                    record.competencyKey, record.rank,
                    CATechnologyCustodyKind.Pawn, pawn.thingIDNumber,
                    pawn.LabelShort, now, "recruited carrier");
                changedSources.Add(newKnowledge);
            }
            foreach (CATechnologicalKnowledge source in changedSources)
            {
                source.revision++;
                CATechnologicalKnowledgeModel.Normalize(source);
            }
            if (DistributedEnabled && newKnowledge?.distributionInitialized
                    == true && pawn.Faction == newFaction)
                AssignNewMemberCustody(newFaction, pawn, newKnowledge);
        }

        internal static void RecordCarrierLoss(Pawn pawn, Faction faction,
            string reason)
        {
            if (pawn?.RaceProps?.Humanlike != true
                || !CAFactionStateGenerator.UsesFactionState(faction)) return;
            CATechnologicalKnowledge knowledge = ForFaction(faction,
                ensure: false);
            if (knowledge == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            bool changed = false;
            foreach (CATechnologyCustodyRecord record in knowledge.custody
                .Where(item => item?.kind == CATechnologyCustodyKind.Pawn
                    && item.pawnThingId == pawn.thingIDNumber
                    && item.unavailableAtTick < 0))
            {
                record.unavailableAtTick = now;
                record.unavailableReason = "lost";
                record.accessible = false;
                AppendReceipt(knowledge, record, "lost", reason, now);
                changed = true;
            }
            if (changed) knowledge.revision++;
        }

        internal static bool PawnAvailable(Faction faction, int pawnId,
            Map map = null)
        {
            Pawn pawn = AllFactionPawns(faction).FirstOrDefault(item =>
                item.thingIDNumber == pawnId);
            return IsUsableCarrier(pawn) && (map == null || pawn.Map == map);
        }

        internal static bool RetainedKnowledgeAvailable(
            CATechnologyCustodyRecord record, Map map)
        {
            if (record == null || record.kind == CATechnologyCustodyKind.Pawn)
                return false;
            // Faction-wide summaries may ask without a place. Map consumers
            // only receive knowledge from a represented store on that map (or
            // at its world tile); an unlocated archive is not omnipresent.
            if (map == null) return true;
            if (record.mapId >= 0) return record.mapId == map.uniqueID;
            if (record.tileId >= 0) return record.tileId == map.Tile;
            return false;
        }

        private static IEnumerable<Pawn> AllFactionPawns(Faction faction)
        {
            if (faction == null) return Enumerable.Empty<Pawn>();
            return PawnsFinder.AllMapsWorldAndTemporary_Alive
                .ToList().Where(item => item?.Faction == faction);
        }

        private static bool IsUsableCarrier(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.RaceProps?.Humanlike == true
                && !pawn.Downed && !pawn.Suspended
                && (pawn.health?.capacities?.GetLevel(
                    PawnCapacityDefOf.Consciousness) ?? 0f) > 0.10f;
        }

        private static int RelevantSkill(Pawn pawn, string domain)
        {
            if (pawn?.skills == null) return 0;
            SkillDef first = domain == CATechnologyDomains.Medicine
                ? SkillDefOf.Medicine
                : domain == CATechnologyDomains.Agriculture
                    ? SkillDefOf.Plants
                    : domain == CATechnologyDomains.Construction
                        || domain == CATechnologyDomains.Electrical
                        ? SkillDefOf.Construction
                        : domain == CATechnologyDomains.Logistics
                            ? SkillDefOf.Intellectual : SkillDefOf.Crafting;
            return pawn.skills.GetSkill(first)?.Level ?? 0;
        }

        private static int StableCarrierOrder(Faction faction, Pawn pawn,
            string domain)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (faction?.loadID ?? 0);
                hash = hash * 31 + (pawn?.thingIDNumber ?? 0);
                foreach (char c in domain ?? string.Empty)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        private static bool AddCustody(CATechnologicalKnowledge knowledge,
            string domain, string competency, int rank,
            CATechnologyCustodyKind kind, int pawnId, string custodian,
            int now, string provenance, int mapId = -1, int tileId = -1)
        {
            CATechnologyCustodyRecord existing = knowledge.custody
                .FirstOrDefault(item => item != null
                    && item.domainKey == domain
                    && item.competencyKey == competency
                    && item.kind == kind && item.pawnThingId == pawnId
                    && item.custodianKey == custodian
                    && item.mapId == mapId && item.tileId == tileId
                    && item.unavailableAtTick < 0);
            if (existing != null)
            {
                if (existing.rank >= rank) return false;
                existing.rank = rank;
                existing.provenance = provenance;
                return true;
            }
            knowledge.custody.Add(new CATechnologyCustodyRecord
            {
                domainKey = domain,
                competencyKey = competency,
                rank = rank,
                kind = kind,
                pawnThingId = pawnId,
                custodianKey = custodian,
                mapId = mapId,
                tileId = tileId,
                acquiredAtTick = now,
                provenance = provenance
            });
            return true;
        }

        private static void AppendReceipt(CATechnologicalKnowledge knowledge,
            CATechnologyCustodyRecord record, string eventKind,
            string reason, int now)
        {
            if (knowledge == null || record == null) return;
            knowledge.availabilityHistory.Add(
                new CATechnologyAvailabilityReceipt
                {
                    domainKey = record.domainKey,
                    competencyKey = record.competencyKey,
                    rank = record.rank,
                    eventKind = eventKind,
                    custodian = record.kind == CATechnologyCustodyKind.Pawn
                        ? "pawn:" + record.pawnThingId
                        : record.custodianKey,
                    reason = reason,
                    mapId = record.mapId,
                    tileId = record.tileId,
                    tick = now
                });
        }
    }

    // Distribution is captured after the game's starting or loaded pawn
    // population exists. Queries and loss events only read that custody and
    // can never reassign it to whichever pawns happen to be visible later.
    internal sealed class CATechnologicalKnowledgeLifecycleComponent
        : GameComponent
    {
        private int nextDistributionSweep;

        public CATechnologicalKnowledgeLifecycleComponent(Game game) { }

        public override void StartedNewGame()
        {
            // The founding patch applies the confirmed carried state after
            // GameComponentUtility invokes this callback. Initializing here
            // would distribute an engine seed and then have that custody
            // replaced by the authored state.
            nextDistributionSweep = 60;
        }

        public override void LoadedGame()
        {
            CATechnologicalKnowledgeRuntime.SynchronizeFinishedResearch(
                Faction.OfPlayer);
            InitializeCurrentFactions();
        }

        public override void GameComponentTick()
        {
            if (!CATechnologicalKnowledgeRuntime.DistributedEnabled) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextDistributionSweep) return;
            nextDistributionSweep = now + 60;
            InitializeCurrentFactions();
        }

        private static void InitializeCurrentFactions()
        {
            CATechnologicalKnowledgeRuntime
                .InitializeCurrentFactionDistribution();
        }
    }

    // Initial cohorts are finalized together by the founding callback or the
    // delayed lifecycle sweep. Once that boundary exists, later members gain
    // an idempotent skill-bounded custody projection instead of rerolling the
    // original distribution.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class CATechnologicalKnowledgeNewMemberPatch
    {
        private static void Postfix(Pawn __instance)
        {
            Faction faction = __instance?.Faction;
            if (__instance?.RaceProps?.Humanlike != true
                || !CAFactionStateGenerator.UsesFactionState(faction)) return;
            CATechnologicalKnowledge knowledge =
                CATechnologicalKnowledgeRuntime.ForFaction(faction,
                    ensure: false);
            if (CATechnologicalKnowledgeRuntime.DistributedEnabled
                && knowledge?.distributionInitialized == true)
                CATechnologicalKnowledgeRuntime.AssignNewMemberCustody(
                    faction, __instance, knowledge);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    internal static class CATechnologicalKnowledgeFactionChangePatch
    {
        private static void Prefix(Pawn __instance, out Faction __state)
        {
            __state = __instance?.Faction;
        }

        private static void Postfix(Pawn __instance, Faction newFaction,
            Faction __state)
        {
            CATechnologicalKnowledgeRuntime.TransferPawn(__instance,
                __state, newFaction);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class CATechnologicalKnowledgeDeathPatch
    {
        private static void Prefix(Pawn __instance, out Faction __state)
        {
            __state = __instance?.Faction;
        }

        private static void Postfix(Pawn __instance, Faction __state)
        {
            CATechnologicalKnowledgeRuntime.RecordCarrierLoss(__instance,
                __state, "pawn died");
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    internal static class CATechnologicalKnowledgeResearchPatch
    {
        private static void Postfix(ResearchProjectDef proj, Pawn researcher)
        {
            Faction faction = researcher?.Faction ?? Faction.OfPlayer;
            CATechnologicalKnowledgeRuntime.RecordResearch(faction, proj,
                researcher);
        }
    }

    internal static class CATechnologicalKnowledgeNativeGate
    {
        internal static bool ProjectAvailable(ResearchProjectDef project,
            Map map = null)
        {
            if (project == null) return true;
            return project.IsFinished
                || CATechnologicalKnowledgeRuntime.Understands(
                    Faction.OfPlayer, project, map);
        }

        internal static bool RecipeConditionsApartFromResearch(
            RecipeDef recipe)
        {
            Faction player = Faction.OfPlayer;
            if (recipe == null || player == null) return false;
            if (recipe.memePrerequisitesAny != null
                && !recipe.memePrerequisitesAny.Any(meme =>
                    player.ideos?.HasAnyIdeoWithMeme(meme) == true))
                return false;
            if (recipe.factionPrerequisiteTags != null
                && recipe.factionPrerequisiteTags.Any(tag =>
                    player.def.recipePrerequisiteTags == null
                    || !player.def.recipePrerequisiteTags.Contains(tag))
                && !UnlockedByIdeologyRole(recipe, player))
                return false;
            return !recipe.fromIdeoBuildingPreceptOnly
                || (ModsConfig.IdeologyActive
                    && IdeoUtility.PlayerHasPreceptForBuilding(
                        recipe.ProducedThingDef));
        }

        internal static bool PlantConditionsApartFromResearch(ThingDef plant,
            Map map)
        {
            if (plant?.plant == null) return false;
            // Preserve the native branch exactly: plants without a research
            // list return before the darkness and wild-plant checks.
            if (plant.plant.sowResearchPrerequisites == null) return true;
            if (plant.plant.mustBePermanentDarknessToSow
                && (map == null
                    || !map.gameConditionManager.IsAlwaysDarkOutside))
                return false;
            if (plant.plant.mustBeWildToSow
                && (map == null
                    || !map.wildPlantSpawner.AllWildPlants.Contains(plant)))
                return false;
            return true;
        }

        private static bool UnlockedByIdeologyRole(RecipeDef recipe,
            Faction player)
        {
            if (player?.ideos?.AllIdeos == null) return false;
            foreach (Ideo ideo in player.ideos.AllIdeos)
                foreach (Precept_Role role in ideo.RolesListForReading)
                {
                    if (role.apparelRequirements == null) continue;
                    foreach (PreceptApparelRequirement requirement in
                        role.apparelRequirements)
                    {
                        ThingDef required = requirement.requirement
                            .AllRequiredApparel().FirstOrDefault();
                        if (required == null) continue;
                        if ((recipe.products ?? new List<ThingDefCountClass>())
                                .Any(product =>
                                product.thingDef == required))
                            return true;
                    }
                }
            return false;
        }
    }

    // Selection availability is only an authoring convenience. These gates
    // sit on the work path so an existing bill, blueprint/frame, or grow zone
    // cannot keep operating after the required local knowledge disappears.
    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    internal static class CATechnologicalKnowledgeBillWorkGate
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            RecipeDef recipe = __result?.RecipeDef;
            if (recipe == null || pawn?.Faction == null) return;
            if (!CATechnologicalKnowledgeRuntime.CanOperate(pawn.Faction,
                    recipe, out _, pawn.Map))
                __result = null;
        }
    }

    [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
    internal static class CATechnologicalKnowledgeBillDriverGate
    {
        [HarmonyPostfix]
        private static void Postfix(JobDriver_DoBill __instance,
            ref IEnumerable<Toil> __result)
        {
            __result = Guard(__instance, __result);
        }

        private static IEnumerable<Toil> Guard(JobDriver_DoBill driver,
            IEnumerable<Toil> source)
        {
            foreach (Toil toil in source)
            {
                toil.FailOn(() => driver.pawn?.Faction != null
                    && !CATechnologicalKnowledgeRuntime.CanOperate(
                        driver.pawn.Faction, driver.job?.RecipeDef, out _,
                        driver.pawn.Map));
                yield return toil;
            }
        }
    }

    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanConstruct),
        new[] { typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool),
            typeof(JobDef) })]
    internal static class CATechnologicalKnowledgeConstructWorkGate
    {
        [HarmonyPostfix]
        private static void Postfix(Thing t, Pawn p, ref bool __result)
        {
            if (!__result || t?.def == null || p?.Faction == null) return;
            BuildableDef built = GenConstruct.BuiltDefOf(t.def);
            if (!CATechnologicalKnowledgeRuntime.CanConstruct(p.Faction,
                    built, out _, p.Map))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_ConstructFinishFrame), "MakeNewToils")]
    internal static class CATechnologicalKnowledgeConstructDriverGate
    {
        [HarmonyPostfix]
        private static void Postfix(JobDriver_ConstructFinishFrame __instance,
            ref IEnumerable<Toil> __result)
        {
            __result = Guard(__instance, __result);
        }

        private static IEnumerable<Toil> Guard(
            JobDriver_ConstructFinishFrame driver, IEnumerable<Toil> source)
        {
            foreach (Toil toil in source)
            {
                toil.FailOn(() =>
                {
                    Thing target = driver.job?.targetA.Thing;
                    BuildableDef built = target?.def == null ? null
                        : GenConstruct.BuiltDefOf(target.def);
                    return driver.pawn?.Faction != null && built != null
                        && !CATechnologicalKnowledgeRuntime.CanConstruct(
                            driver.pawn.Faction, built, out _, driver.pawn.Map);
                });
                yield return toil;
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_GrowerSow),
        nameof(WorkGiver_GrowerSow.JobOnCell))]
    internal static class CATechnologicalKnowledgeSowingWorkGate
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            ThingDef plant = __result?.plantDefToSow;
            if (plant == null || pawn?.Faction == null) return;
            if (!CATechnologicalKnowledgeRuntime.CanGrow(pawn.Faction,
                    plant, out _, pawn.Map))
                __result = null;
        }
    }

    [HarmonyPatch(typeof(JobDriver_PlantSow), "MakeNewToils")]
    internal static class CATechnologicalKnowledgeSowingDriverGate
    {
        [HarmonyPostfix]
        private static void Postfix(JobDriver_PlantSow __instance,
            ref IEnumerable<Toil> __result)
        {
            __result = Guard(__instance, __result);
        }

        private static IEnumerable<Toil> Guard(JobDriver_PlantSow driver,
            IEnumerable<Toil> source)
        {
            foreach (Toil toil in source)
            {
                toil.FailOn(() => driver.pawn?.Faction != null
                    && driver.job?.plantDefToSow != null
                    && !CATechnologicalKnowledgeRuntime.CanGrow(
                        driver.pawn.Faction, driver.job.plantDefToSow,
                        out _, driver.pawn.Map));
                yield return toil;
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Tend),
        nameof(WorkGiver_Tend.HasJobOnThing))]
    internal static class CATechnologicalKnowledgeMedicalWorkGate
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result || pawn?.Faction == null) return;
            __result = CATechnologicalKnowledgeRuntime
                .CanProvideMedicalCare(pawn.Faction, out _, pawn.Map);
        }
    }

    [HarmonyPatch(typeof(JobDriver_TendPatient), "MakeNewToils")]
    internal static class CATechnologicalKnowledgeMedicalDriverGate
    {
        [HarmonyPostfix]
        private static void Postfix(JobDriver_TendPatient __instance,
            ref IEnumerable<Toil> __result)
        {
            __result = Guard(__instance, __result);
        }

        private static IEnumerable<Toil> Guard(JobDriver_TendPatient driver,
            IEnumerable<Toil> source)
        {
            foreach (Toil toil in source)
            {
                toil.FailOn(() => driver.pawn?.Faction != null
                    && !CATechnologicalKnowledgeRuntime
                        .CanProvideMedicalCare(driver.pawn.Faction, out _,
                            driver.pawn.Map));
                yield return toil;
            }
        }
    }

    internal static class CATechnologicalKnowledgeMaintenanceGate
    {
        internal static bool CanMaintain(Pawn pawn, Thing target)
        {
            if (pawn?.Faction == null || target?.def == null) return true;
            BuildableDef built = GenConstruct.BuiltDefOf(target.def);
            return CATechnologicalKnowledgeRuntime.CanMaintain(pawn.Faction,
                built, out _, pawn.Map);
        }

        internal static IEnumerable<Toil> Guard(JobDriver driver,
            IEnumerable<Toil> source)
        {
            foreach (Toil toil in source)
            {
                toil.FailOn(() => !CanMaintain(driver.pawn,
                    driver.job?.targetA.Thing));
                yield return toil;
            }
        }
    }

    // A weapon already equipped before local knowledge is lost cannot remain
    // usable merely because selection-time checks were bypassed. Natural,
    // hediff, and apparel verbs remain native because they have no equipped
    // weapon definition to translate.
    [HarmonyPatch(typeof(Verb), nameof(Verb.Available))]
    internal static class CATechnologicalKnowledgeWeaponUseGate
    {
        [HarmonyPostfix]
        private static void Postfix(Verb __instance, ref bool __result)
        {
            if (!__result) return;
            Pawn pawn = __instance?.CasterPawn;
            ThingDef weapon = __instance?.EquipmentSource?.def;
            if (pawn?.Faction == null || weapon?.IsWeapon != true) return;
            if (!CATechnologicalKnowledgeRuntime.CanUseWeapon(pawn.Faction,
                    weapon, out _, pawn.Map))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Repair),
        nameof(WorkGiver_Repair.HasJobOnThing))]
    internal static class CATechnologicalKnowledgeRepairWorkGate
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, Thing t, ref bool __result)
        {
            if (__result && !CATechnologicalKnowledgeMaintenanceGate
                    .CanMaintain(pawn, t))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Repair), "MakeNewToils")]
    internal static class CATechnologicalKnowledgeRepairDriverGate
    {
        [HarmonyPostfix]
        private static void Postfix(JobDriver_Repair __instance,
            ref IEnumerable<Toil> __result)
        {
            __result = CATechnologicalKnowledgeMaintenanceGate.Guard(
                __instance, __result);
        }
    }

    [HarmonyPatch(typeof(WorkGiver_FixBrokenDownBuilding),
        nameof(WorkGiver_FixBrokenDownBuilding.HasJobOnThing))]
    internal static class CATechnologicalKnowledgeBreakdownWorkGate
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, Thing t, ref bool __result)
        {
            if (__result && !CATechnologicalKnowledgeMaintenanceGate
                    .CanMaintain(pawn, t))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_FixBrokenDownBuilding), "MakeNewToils")]
    internal static class CATechnologicalKnowledgeBreakdownDriverGate
    {
        [HarmonyPostfix]
        private static void Postfix(
            JobDriver_FixBrokenDownBuilding __instance,
            ref IEnumerable<Toil> __result)
        {
            __result = CATechnologicalKnowledgeMaintenanceGate.Guard(
                __instance, __result);
        }
    }

    [HarmonyPatch(typeof(BuildableDef), "get_IsResearchFinished")]
    internal static class CATechnologicalKnowledgeConstructionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BuildableDef __instance,
            ref bool __result)
        {
            Faction player = Faction.OfPlayer;
            if (!CAFactionStateGenerator.UsesFactionState(player)) return;
            __result = CATechnologicalKnowledgeRuntime.CanConstruct(
                player, __instance, out _, Find.CurrentMap);
        }
    }

    [HarmonyPatch(typeof(RecipeDef), "get_AvailableNow")]
    internal static class CATechnologicalKnowledgeProductionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(RecipeDef __instance, ref bool __result)
        {
            Faction player = Faction.OfPlayer;
            if (!CAFactionStateGenerator.UsesFactionState(player)) return;
            Map map = Find.CurrentMap;
            IEnumerable<ResearchProjectDef> projects =
                (__instance.researchPrerequisites
                    ?? new List<ResearchProjectDef>())
                .Concat(__instance.researchPrerequisite == null
                    ? Enumerable.Empty<ResearchProjectDef>()
                    : new[] { __instance.researchPrerequisite });
            __result = projects.All(project =>
                    CATechnologicalKnowledgeNativeGate.ProjectAvailable(
                        project, map))
                && CATechnologicalKnowledgeNativeGate
                    .RecipeConditionsApartFromResearch(__instance)
                && CATechnologicalKnowledgeRuntime.CanOperate(player,
                    __instance, out _, map);
        }
    }

    [HarmonyPatch(typeof(Command_SetPlantToGrow), nameof(
        Command_SetPlantToGrow.IsPlantAvailable))]
    internal static class CATechnologicalKnowledgeAgriculturePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ThingDef plantDef, Map map,
            ref bool __result)
        {
            Faction player = Faction.OfPlayer;
            if (!CAFactionStateGenerator.UsesFactionState(player)) return;
            IEnumerable<ResearchProjectDef> projects = plantDef?.plant
                ?.sowResearchPrerequisites
                ?? Enumerable.Empty<ResearchProjectDef>();
            __result = projects.All(project =>
                    CATechnologicalKnowledgeNativeGate.ProjectAvailable(
                        project, map))
                && CATechnologicalKnowledgeNativeGate
                    .PlantConditionsApartFromResearch(plantDef, map)
                && CATechnologicalKnowledgeRuntime.CanGrow(player, plantDef,
                    out _, map);
        }
    }

    // Research completion remains the native project fact. Only the native
    // cost projection and speed penalty are replaced by the authored faction's
    // effective technological knowledge.
    [HarmonyPatch(typeof(ResearchProjectDef), "get_CostApparent")]
    internal static class CATechnologicalKnowledgeResearchCostPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ResearchProjectDef __instance,
            ref float __result)
        {
            Faction player = Faction.OfPlayer;
            if (!CAFactionStateGenerator.UsesFactionState(player)) return;
            __result = __instance.Cost * __instance.CostFactor(
                CATechnologicalKnowledgeRuntime.EffectiveResearchTechLevel(
                    player, __instance, Find.CurrentMap));
        }
    }

    [HarmonyPatch(typeof(ResearchProjectDef), "get_ProgressApparent")]
    internal static class CATechnologicalKnowledgeResearchProgressPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ResearchProjectDef __instance,
            ref float __result)
        {
            Faction player = Faction.OfPlayer;
            if (!CAFactionStateGenerator.UsesFactionState(player)) return;
            __result = __instance.ProgressReal * __instance.CostFactor(
                CATechnologicalKnowledgeRuntime.EffectiveResearchTechLevel(
                    player, __instance, Find.CurrentMap));
        }
    }

    [HarmonyPatch(typeof(ResearchManager),
        nameof(ResearchManager.ResearchPerformed))]
    internal static class CATechnologicalKnowledgeResearchSpeedPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref float amount, Pawn researcher,
            ResearchProjectDef ___currentProj)
        {
            Faction faction = researcher?.Faction;
            if (faction == null || ___currentProj == null
                || !CAFactionStateGenerator.UsesFactionState(faction)) return;
            float native = ___currentProj.CostFactor(faction.def.techLevel);
            float effective = ___currentProj.CostFactor(
                CATechnologicalKnowledgeRuntime.EffectiveResearchTechLevel(
                    faction, ___currentProj, researcher.Map));
            if (native > 0f && effective > 0f)
                amount *= native / effective;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research),
        "DrawProjectScrollView")]
    internal static class CATechnologicalKnowledgeResearchUiPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> buffer = instructions.ToList();
            FieldInfo nativeLevel = AccessTools.Field(typeof(FactionDef),
                nameof(FactionDef.techLevel));
            MethodInfo effective = AccessTools.Method(
                typeof(CATechnologicalKnowledgeRuntime),
                nameof(CATechnologicalKnowledgeRuntime
                    .EffectivePlayerTechLevel));
            int replacements = 0;
            foreach (CodeInstruction instruction in buffer)
            {
                if (instruction.opcode != OpCodes.Ldfld
                    || !Equals(instruction.operand, nativeLevel)) continue;
                instruction.opcode = OpCodes.Call;
                instruction.operand = effective;
                replacements++;
            }
            if (replacements != 3)
                Log.Error("[CA][Technology] Expected three native research "
                    + "tech-level reads, found " + replacements
                    + "; research presentation integration is incomplete.");
            return buffer;
        }
    }

    // Designator_Build has two direct FactionDef.techLevel comparisons in
    // addition to its research query. Replace only those field reads so every
    // other vanilla visibility condition remains in place.
    [HarmonyPatch(typeof(Designator_Build), "get_Visible")]
    internal static class CATechnologicalKnowledgeBuildLevelPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> buffer = instructions.ToList();
            FieldInfo nativeLevel = AccessTools.Field(typeof(FactionDef),
                nameof(FactionDef.techLevel));
            MethodInfo effective = AccessTools.Method(
                typeof(CATechnologicalKnowledgeRuntime),
                nameof(CATechnologicalKnowledgeRuntime
                    .EffectivePlayerBuildTechLevel));
            int replacements = 0;
            for (int i = 0; i < buffer.Count; i++)
            {
                CodeInstruction instruction = buffer[i];
                if (instruction.opcode != OpCodes.Ldfld
                    || !Equals(instruction.operand, nativeLevel)) continue;
                instruction.opcode = OpCodes.Call;
                instruction.operand = effective;
                replacements++;
            }
            if (replacements != 2)
                Log.Error("[CA][Technology] Expected two native build-level "
                    + "reads, found " + replacements
                    + "; build visibility integration is incomplete.");
            return buffer;
        }
    }
}
