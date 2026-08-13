using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // Read-only diagnostics over persisted assessments. Inspection never
    // discovers evidence, recalculates a capability, or mutates settlement
    // state.
    internal static class CASettlementCapabilityInspection
    {
        internal static string Summary(CARegionalSettlementRecord record)
        {
            return string.Join(", ", CASettlementCapabilityDomains.All
                .Select(domain => domain + " " + Level(record, domain)));
        }

        internal static string Detail(CARegionalSettlementRecord record,
            string domain)
        {
            CASettlementCapabilityAssessment assessment = record?.capabilities
                ?.FirstOrDefault(item => item != null
                    && string.Equals(item.domain, domain,
                        StringComparison.Ordinal));
            if (assessment == null)
                return (domain ?? "capability") + ": no assessment recorded";
            var facts = new List<string>
            {
                assessment.domain + " " + assessment.level,
                "actors=" + Join(assessment.actorIdentities),
                "organizations=" + Join(assessment.organizationIdentities),
                "operations=" + Join(assessment.operationIdentities),
                "knowledge=" + Join(assessment.knowledgeEvidence),
                "material=" + Join(assessment.materialEvidence),
                "history=" + Join(assessment.historicalEvidence),
                "signature=" + (assessment.evidenceSignature ?? "none")
            };
            if (!string.IsNullOrEmpty(assessment.blocker))
                facts.Add("blocker=" + assessment.blocker);
            return string.Join("; ", facts);
        }

        private static int Level(CARegionalSettlementRecord record,
            string domain)
        {
            return record?.capabilities?.FirstOrDefault(item => item != null
                && item.domain == domain)?.level ?? 0;
        }

        private static string Join(IEnumerable<string> values)
        {
            string[] clean = (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrEmpty(value)).ToArray();
            return clean.Length == 0 ? "none" : string.Join("|", clean);
        }
    }
}
