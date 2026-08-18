using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // One explicit operational fact set. Runtime authoring and fixture
    // generation serialize this same shape. A program is active only when its
    // direct need and every required operational cause are present.
    public sealed class CASettlementProgramOperationalEvidence
    {
        public string Key;
        public string Scope = "settlement";
        public string NeedSource;
        public string OperatorIdentity;
        public string OperatorSource;
        public string LaborSource;
        public string StandingSource;
        public string ActivitySource;
        public string TargetPopulation;
        public string KnowledgeSource;
        public string FundingSource;
        public string StockSource;
        public string PolicyKey;
        public string MaterialSource;
        public string AccessSource;
        public string MaintenanceSource;
        public List<string> CulturalSubjects = new List<string>();
        public List<string[]> CandidateGroups = new List<string[]>();
        public int Count = 1;
        public int Extent = 1;
        public bool RequiresOperator;
        public bool RequiresFunding;
        public bool RequiresStock;
        public bool RequiresMaterial = true;
        public bool NativeSpatialContract;
        public bool MaterializeSpatialContract;
        public string Blocker;

        public bool Complete
        {
            get
            {
                if (!string.IsNullOrEmpty(Blocker)
                    || string.IsNullOrEmpty(Key)
                    || string.IsNullOrEmpty(NeedSource)
                    || string.IsNullOrEmpty(LaborSource)
                    || string.IsNullOrEmpty(StandingSource)
                    || string.IsNullOrEmpty(ActivitySource)
                    || string.IsNullOrEmpty(TargetPopulation)
                    || string.IsNullOrEmpty(KnowledgeSource)
                    || string.IsNullOrEmpty(AccessSource)
                    || string.IsNullOrEmpty(MaintenanceSource)) return false;
                if (RequiresOperator
                    && (string.IsNullOrEmpty(OperatorIdentity)
                        || string.IsNullOrEmpty(OperatorSource))) return false;
                if (RequiresFunding
                    && string.IsNullOrEmpty(FundingSource)) return false;
                if (RequiresStock
                    && string.IsNullOrEmpty(StockSource)) return false;
                if (RequiresMaterial
                    && string.IsNullOrEmpty(MaterialSource)) return false;
                return true;
            }
        }
    }

    public sealed class CASettlementProgramCausalSpec
    {
        public string Key;
        public string Scope = "settlement";
        public string NeedSource;
        public string OperatorIdentity;
        public string OperatorSource;
        public string LaborSource;
        public string StandingSource;
        public string ActivitySource;
        public string TargetPopulation;
        public string KnowledgeSource;
        public string FundingSource;
        public string StockSource;
        public string PolicyKey;
        public string MaterialSource;
        public string AccessSource;
        public string MaintenanceSource;
        public List<string> CulturalSubjects = new List<string>();
        public List<string[]> CandidateGroups = new List<string[]>();
        public int Count = 1;
        public int Extent = 1;
        public bool RequiresOperator;
        public bool RequiresFunding;
        public bool RequiresStock;
        public bool RequiresMaterial = true;
        public bool NativeSpatialContract;
        public bool MaterializeSpatialContract;
    }

    public static class CASettlementProgramCausalKernel
    {
        public const string Housing = "ca.settlement.housing";
        public const string FoodPreparation =
            "ca.settlement.food-preparation";
        public const string Storage = "ca.settlement.storage";
        public const string Medicine = "ca.settlement.medicine";
        public const string Production = "ca.settlement.production";
        public const string SpecializedIndustry =
            "ca.settlement.specialized-industry";
        public const string Trade = "ca.settlement.trade";
        public const string Governance = "ca.settlement.governance";
        public const string Custody = "ca.settlement.custody";
        public const string Defense = "ca.settlement.defense";
        public const string Research = "ca.settlement.research";
        public const string Religion = "ca.settlement.religion";
        public const string Gathering = "ca.settlement.gathering";
        public const string Recreation = "ca.settlement.recreation";
        public const string ArtAndMemory = "ca.settlement.art-memory";
        public const string Agriculture = "ca.settlement.agriculture";
        public const string Animals = "ca.settlement.animals";
        public const string Communications =
            "ca.settlement.communications";
        public const string Transport = "ca.settlement.transport";
        public const string CommunalProvision =
            "ca.settlement.provision.communal";
        public const string AuthorityProvision =
            "ca.settlement.provision.authority";
        public const string DomesticProvision =
            "ca.settlement.provision.domestic";

        public static List<CASettlementProgramCausalSpec> Derive(
            IEnumerable<CASettlementProgramOperationalEvidence> evidence)
        {
            return (evidence ?? Enumerable.Empty<
                    CASettlementProgramOperationalEvidence>())
                .Where(item => item != null && item.Complete)
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ThenBy(item => item.OperatorIdentity,
                    StringComparer.Ordinal)
                .Select(item => new CASettlementProgramCausalSpec
                {
                    Key = item.Key,
                    Scope = item.Scope,
                    NeedSource = item.NeedSource,
                    OperatorIdentity = item.OperatorIdentity,
                    OperatorSource = item.OperatorSource,
                    LaborSource = item.LaborSource,
                    StandingSource = item.StandingSource,
                    ActivitySource = item.ActivitySource,
                    TargetPopulation = item.TargetPopulation,
                    KnowledgeSource = item.KnowledgeSource,
                    FundingSource = item.FundingSource,
                    StockSource = item.StockSource,
                    PolicyKey = item.PolicyKey,
                    MaterialSource = item.MaterialSource,
                    AccessSource = item.AccessSource,
                    MaintenanceSource = item.MaintenanceSource,
                    CulturalSubjects = (item.CulturalSubjects
                        ?? new List<string>()).ToList(),
                    CandidateGroups = (item.CandidateGroups
                        ?? new List<string[]>()).Select(group =>
                            (group ?? new string[0]).ToArray()).ToList(),
                    Count = Math.Max(1, item.Count),
                    Extent = Math.Max(1, item.Extent),
                    RequiresOperator = item.RequiresOperator,
                    RequiresFunding = item.RequiresFunding,
                    RequiresStock = item.RequiresStock,
                    RequiresMaterial = item.RequiresMaterial,
                    NativeSpatialContract = item.NativeSpatialContract,
                    MaterializeSpatialContract =
                        item.MaterializeSpatialContract
                }).ToList();
        }

        public static string SourceFacts(
            IEnumerable<CASettlementProgramOperationalEvidence> evidence)
        {
            return string.Join("|", (evidence ?? Enumerable.Empty<
                    CASettlementProgramOperationalEvidence>())
                .OrderBy(item => item?.Key, StringComparer.Ordinal)
                .ThenBy(item => item?.OperatorIdentity,
                    StringComparer.Ordinal)
                .Select(item => item == null ? "null" : string.Join("~",
                    item.Key, item.Scope, item.NeedSource,
                    item.OperatorIdentity, item.OperatorSource,
                    item.LaborSource, item.StandingSource,
                    item.ActivitySource, item.TargetPopulation,
                    item.KnowledgeSource,
                    item.FundingSource, item.StockSource, item.PolicyKey,
                    item.MaterialSource, item.AccessSource,
                    item.MaintenanceSource,
                    string.Join(",", item.CulturalSubjects
                        ?? new List<string>()), item.Count, item.Extent,
                    item.RequiresOperator, item.RequiresFunding,
                    item.RequiresStock, item.RequiresMaterial,
                    item.NativeSpatialContract,
                    item.MaterializeSpatialContract,
                    item.Blocker)));
        }

        public static string SourceSignature(
            IEnumerable<CASettlementProgramOperationalEvidence> evidence)
        {
            return unchecked((uint)StableStringHash(SourceFacts(evidence)))
                .ToString("X8");
        }

        // A hash may choose between functionally equivalent loaded assets. It
        // never chooses whether the program, operator, worker, institution, or
        // policy exists.
        public static int StableCandidateIndex(string candidateId, int slot,
            string key, int groupIndex, int count)
        {
            if (count <= 1) return 0;
            int hash = StableStringHash((candidateId ?? "ca") + ":" + slot
                + ":" + (key ?? "program") + ":" + groupIndex)
                & int.MaxValue;
            return hash % count;
        }

        public static string EntrySignature(string source, string key,
            string scope, int count, int extent,
            IEnumerable<string> selectedCandidates, string blocker,
            string needSource = null, string operatorIdentity = null,
            string laborSource = null, string knowledgeSource = null,
            string materialSource = null, string standingSource = null,
            string activitySource = null, string targetPopulation = null,
            string accessSource = null, string maintenanceSource = null)
        {
            string facts = (source ?? "") + "|" + (key ?? "") + "|"
                + (scope ?? "") + "|" + count + "|" + extent + "|"
                + string.Join(",", selectedCandidates
                    ?? Enumerable.Empty<string>()) + "|" + (blocker ?? "")
                + "|" + (needSource ?? "") + "|"
                + (operatorIdentity ?? "") + "|" + (laborSource ?? "")
                + "|" + (knowledgeSource ?? "") + "|"
                + (materialSource ?? "") + "|" + (standingSource ?? "")
                + "|" + (activitySource ?? "") + "|"
                + (targetPopulation ?? "") + "|" + (accessSource ?? "")
                + "|" + (maintenanceSource ?? "");
            return unchecked((uint)StableStringHash(facts)).ToString("X8");
        }

        public static int StableStringHash(string value)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in value ?? "") hash = hash * 31 + c;
                return hash;
            }
        }
    }
}
