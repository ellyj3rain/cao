using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // Pure assessment: missing constitutive evidence blocks the capability.
    // The signature is a readback receipt, never a cause.
    public static class CASettlementCapabilityAssessmentKernel
    {
        public static CASettlementCapabilityAssessment Assess(
            CASettlementCapabilityEvidence evidence, int assessedTick)
        {
            evidence = evidence ?? new CASettlementCapabilityEvidence();
            var result = new CASettlementCapabilityAssessment
            {
                domain = evidence.Domain,
                actorIdentities = Clean(evidence.Actors),
                organizationIdentities = Clean(evidence.Organizations),
                operationIdentities = Clean(evidence.Operations),
                knowledgeEvidence = Clean(evidence.Knowledge),
                materialEvidence = Clean(evidence.Material),
                historicalEvidence = Clean(evidence.History),
                assessedTick = assessedTick
            };
            bool complete = result.actorIdentities.Count > 0
                && result.organizationIdentities.Count > 0
                && result.operationIdentities.Count > 0
                && result.knowledgeEvidence.Count > 0
                && result.materialEvidence.Count > 0
                && result.historicalEvidence.Count > 0;
            result.level = complete ? Math.Max(1,
                Math.Min(5, evidence.LevelHint)) : 0;
            result.confidence = complete ? 1f : 0f;
            result.blocker = complete ? null : (evidence.Blocker
                ?? "one or more constitutive evidence families are absent");
            string facts = result.domain + "|" + result.level + "|"
                + string.Join(",", result.actorIdentities) + "|"
                + string.Join(",", result.organizationIdentities) + "|"
                + string.Join(",", result.operationIdentities) + "|"
                + string.Join(",", result.knowledgeEvidence) + "|"
                + string.Join(",", result.materialEvidence) + "|"
                + string.Join(",", result.historicalEvidence) + "|"
                + (result.blocker ?? "");
            result.evidenceSignature = unchecked((uint)
                CASettlementProgramCausalKernel.StableStringHash(facts))
                .ToString("X8");
            return result;
        }

        private static List<string> Clean(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrEmpty(value)).Distinct()
                .OrderBy(value => value, StringComparer.Ordinal).ToList();
        }
    }
}
