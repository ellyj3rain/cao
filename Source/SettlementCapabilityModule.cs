using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    public static class CASettlementCapabilityDomains
    {
        public const string Medicine = "medicine";
        public const string Production = "production";
        public const string Logistics = "logistics";
        public const string Civic = "civic";
        public const string Research = "research";
        public const string Security = "security";
        public const string Commerce = "commerce";
        public const string Communications = "communications";

        public static readonly string[] All =
        {
            Medicine, Production, Logistics, Civic, Research, Security,
            Commerce, Communications
        };
    }

    public sealed class CASettlementCapabilityAssessment : IExposable
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public string domain;
        public int level;
        public List<string> actorIdentities = new List<string>();
        public List<string> organizationIdentities = new List<string>();
        public List<string> operationIdentities = new List<string>();
        public List<string> knowledgeEvidence = new List<string>();
        public List<string> materialEvidence = new List<string>();
        public List<string> historicalEvidence = new List<string>();
        public float confidence;
        public int assessedTick = -1;
        // This digest proves readback equality. It is never read as a cause.
        public string evidenceSignature;
        public string blocker;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref domain, "domain");
            Scribe_Values.Look(ref level, "level", 0);
            Scribe_Collections.Look(ref actorIdentities,
                "actorIdentities", LookMode.Value);
            Scribe_Collections.Look(ref organizationIdentities,
                "organizationIdentities", LookMode.Value);
            Scribe_Collections.Look(ref operationIdentities,
                "operationIdentities", LookMode.Value);
            Scribe_Collections.Look(ref knowledgeEvidence,
                "knowledgeEvidence", LookMode.Value);
            Scribe_Collections.Look(ref materialEvidence,
                "materialEvidence", LookMode.Value);
            Scribe_Collections.Look(ref historicalEvidence,
                "historicalEvidence", LookMode.Value);
            Scribe_Values.Look(ref confidence, "confidence", 0f);
            Scribe_Values.Look(ref assessedTick, "assessedTick", -1);
            Scribe_Values.Look(ref evidenceSignature,
                "evidenceSignature");
            Scribe_Values.Look(ref blocker, "blocker");
            if (actorIdentities == null)
                actorIdentities = new List<string>();
            if (organizationIdentities == null)
                organizationIdentities = new List<string>();
            if (operationIdentities == null)
                operationIdentities = new List<string>();
            if (knowledgeEvidence == null)
                knowledgeEvidence = new List<string>();
            if (materialEvidence == null)
                materialEvidence = new List<string>();
            if (historicalEvidence == null)
                historicalEvidence = new List<string>();
        }
    }

    // Coordinator and public read contract. Domain discovery lives in the
    // adapter module; assessment is pure; inspection has its own boundary.
    internal static class CASettlementCapabilities
    {
        internal static int Level(CARegionalSettlementRecord record,
            string domain)
        {
            return record?.capabilities?.FirstOrDefault(item => item != null
                && item.domain == domain)?.level ?? 0;
        }

        internal static CASettlementCapabilityAssessment Assessment(
            CARegionalSettlementRecord record, string domain)
        {
            return record?.capabilities?.FirstOrDefault(item => item != null
                && item.domain == domain);
        }

        internal static void Reconcile(CARegionalSettlementRecord record,
            Map map, CAOrganization organization)
        {
            if (record == null || map == null) return;
            int assessedTick = Find.TickManager?.TicksGame ?? -1;
            record.capabilities = CASettlementCapabilityAdapters.All(
                    record, map, organization)
                .Select(evidence => CASettlementCapabilityAssessmentKernel
                    .Assess(evidence, assessedTick))
                .ToList();
        }
    }
}
