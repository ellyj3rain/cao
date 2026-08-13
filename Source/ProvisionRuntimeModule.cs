using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    // Ordered runtime validation over the four subordinate provision owners.
    // It changes only arrangement/program operating status.
    internal static class CAProvisionRuntimeResolver
    {
        internal static void Reconcile(CARegionalSettlementRecord record,
            Map map)
        {
            if (record == null || map == null) return;
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.ProvisionResolution))
            {
            int examined = 0;
            int operating = 0;
            foreach (CAProvisionArrangement arrangement in
                record.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
            {
                if (arrangement == null || !arrangement.active) continue;
                examined++;
                CAResolvedProvisionOperator resolved =
                    CAProvisionOperatorResolver.Resolve(record, map,
                        arrangement);
                CAProvisionMaterialEvidence material =
                    CAProvisionMaterializationAdapter.Observe(record, map,
                        arrangement);
                bool funded = CAProvisionFundingResolver.IsFunded(record,
                    arrangement, resolved, out string fundingFailure);
                bool access = CAProvisionAccessService.HasAccess(record, map,
                    arrangement, resolved, material,
                    out string accessFailure);
                arrangement.operational = resolved.Resolved && funded
                    && material.Complete && access;
                arrangement.inactiveReason = arrangement.operational ? null
                    : resolved.Failure ?? fundingFailure ?? material.Failure
                        ?? accessFailure;
                if (arrangement.operational) operating++;
            }

            foreach (string key in new[]
                {
                    CASettlementProgramRegistry.DomesticProvision,
                    CASettlementProgramRegistry.CommunalProvision,
                    CASettlementProgramRegistry.AuthorityProvision
                })
            {
                foreach (CASettlementProgramEntry program in record
                    .settlementProgram?.Entries(key)
                    ?? Enumerable.Empty<CASettlementProgramEntry>())
                {
                    List<CAProvisionArrangement> arrangements = (record
                            .provisionArrangements
                            ?? new List<CAProvisionArrangement>())
                        .Where(item => item != null && item.active
                            && item.operatorIdentity
                                == program.operatorIdentity
                            && CASettlementProgramRegistry.ProgramKeyFor(
                                item.operatorKind) == key).ToList();
                    bool complete = arrangements.Count > 0
                        && arrangements.All(item => item.operational);
                    bool requireAssets = program.materializationState
                        == "materialized" || program.materializationState
                        == "present in saved geography";
                    bool baseResolved =
                        CASettlementProgramRuntimeContract.TryResolve(record,
                            map, program, requireAssets,
                            out CASettlementProgramRuntimeResolution
                                programResolution);
                    program.runtimeState = baseResolved && complete
                        ? "operating" : "suspended";
                    program.runtimeFailure = !baseResolved
                        ? programResolution.Failure
                        : complete ? null : (
                            arrangements.Count == 0
                                ? "no active provision arrangement exists for the recorded operator"
                                : arrangements.Select(item =>
                                        item.inactiveReason)
                                    .FirstOrDefault(reason =>
                                        !reason.NullOrEmpty())
                                    ?? "provision contract is incomplete");
                    program.lastRuntimeValidationTick =
                        Find.TickManager?.TicksGame ?? -1;
                }
            }
            CAModuleProfiler.Observe(
                CAModuleProfileKey.ProvisionResolution,
                objectsExamined: examined,
                candidatesAccepted: operating,
                workSkippedOrDeferred: examined - operating);
            }
        }
    }
}
