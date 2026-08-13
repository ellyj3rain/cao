using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    // Forms and maintains security assignments from one live Defense program.
    // The saved program owns the practice; only its exact labor participants
    // with live equipment may patrol or count as guards.
    internal static class CASettlementSecurityAssignments
    {
        private const string Kind = "settlement defense";

        internal static void Reconcile(CARegionalSettlementRecord record,
            Map map, CAOrganization organization)
        {
            if (record == null || map == null || organization == null)
                return;
            if (organization.securityPractices == null)
                organization.securityPractices =
                    new List<CASecurityPractice>();
            var retained = new HashSet<string>();
            foreach (CASettlementProgramEntry entry in record
                .settlementProgram?.Entries(
                    CASettlementProgramRegistry.Defense)
                    ?? Enumerable.Empty<CASettlementProgramEntry>())
            {
                if (!CASettlementProgramRuntimeContract.TryResolve(record,
                        map, entry, requireMaterializedAssets: true,
                        out CASettlementProgramRuntimeResolution result))
                    continue;
                List<int> guards = result.Workers.Where(pawn => pawn != null
                        && pawn.equipment?.Primary != null)
                    .Select(pawn => pawn.thingIDNumber).Distinct()
                    .OrderBy(value => value).ToList();
                if (guards.Count == 0) continue;
                retained.Add(entry.signature);
                CASecurityPractice practice = organization.securityPractices
                    .FirstOrDefault(item => item != null
                        && item.kindLabel == Kind
                        && item.programSignature == entry.signature);
                if (practice == null)
                {
                    practice = new CASecurityPractice
                    {
                        mapId = map.uniqueID,
                        kindLabel = Kind,
                        name = "Settlement defense",
                        assignedOffice = organization.organizationKey,
                        programKey = entry.programKey,
                        operatorIdentity = entry.operatorIdentity,
                        programSignature = entry.signature
                    };
                    organization.securityPractices.Add(practice);
                }
                practice.mapId = map.uniqueID;
                practice.operatorIdentity = entry.operatorIdentity;
                practice.guardPawnIds = guards;
            }
            organization.securityPractices.RemoveAll(item => item != null
                && item.kindLabel == Kind
                && !retained.Contains(item.programSignature));
        }

        internal static HashSet<int> LiveGuardIds(
            CARegionalSettlementRecord record, Map map)
        {
            var result = new HashSet<int>();
            if (record == null || map == null) return result;
            CAOrganization organization =
                CAOrganizationWorldComponent.Current?.ByKey(
                    record.regionalId + "#" + record.slot);
            foreach (CASecurityPractice practice in
                organization?.securityPractices
                    ?? new List<CASecurityPractice>())
            {
                if (practice == null || practice.kindLabel != Kind
                    || practice.mapId != map.uniqueID) continue;
                CASettlementProgramEntry entry = record.settlementProgram
                    ?.Entries(CASettlementProgramRegistry.Defense)
                    .FirstOrDefault(item => item.signature
                        == practice.programSignature
                        && item.operatorIdentity
                            == practice.operatorIdentity);
                if (!CASettlementProgramRuntimeContract.TryResolve(record,
                        map, entry, requireMaterializedAssets: true,
                        out CASettlementProgramRuntimeResolution resolved))
                    continue;
                var allowed = new HashSet<int>(resolved.Workers
                    .Where(pawn => pawn.equipment?.Primary != null)
                    .Select(pawn => pawn.thingIDNumber));
                foreach (int id in practice.guardPawnIds
                    ?? new List<int>())
                    if (allowed.Contains(id)) result.Add(id);
            }
            return result;
        }
    }
}
