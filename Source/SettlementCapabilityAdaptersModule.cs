using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    internal interface ICASettlementCapabilityAdapter
    {
        CASettlementCapabilityEvidence Read(CARegionalSettlementRecord record,
            Map map, CAOrganization organization);
    }

    internal static class CASettlementCapabilityAdapters
    {
        private static readonly ICASettlementCapabilityAdapter[] Adapters =
        {
            new CAMedicalCapabilityAdapter(),
            new CAProductionCapabilityAdapter(),
            new CALogisticsCapabilityAdapter(),
            new CACivicCapabilityAdapter(),
            new CAResearchCapabilityAdapter(),
            new CASecurityCapabilityAdapter(),
            new CACommerceCapabilityAdapter(),
            new CACommunicationCapabilityAdapter()
        };

        internal static IEnumerable<CASettlementCapabilityEvidence> All(
            CARegionalSettlementRecord record, Map map,
            CAOrganization organization)
        {
            return Adapters.Select(adapter => adapter.Read(record, map,
                organization));
        }

        internal static CASettlementCapabilityEvidence Absent(string domain,
            string blocker)
        {
            return new CASettlementCapabilityEvidence
                { Domain = domain, Blocker = blocker };
        }

        internal static string Party(CAParty party)
        {
            if (party == null) return null;
            return party.pawnId >= 0 ? "pawn:" + party.pawnId
                : party.orgKey.NullOrEmpty() ? null : party.orgKey;
        }
    }

    internal sealed class CAMedicalCapabilityAdapter : ICASettlementCapabilityAdapter
    {
        public CASettlementCapabilityEvidence Read(CARegionalSettlementRecord r,
            Map m, CAOrganization o) => CASettlementCapabilityAdapters.Absent(
            CASettlementCapabilityDomains.Medicine,
            "no represented treatment operation and treatment history");
    }

    internal sealed class CAProductionCapabilityAdapter : ICASettlementCapabilityAdapter
    {
        public CASettlementCapabilityEvidence Read(CARegionalSettlementRecord r,
            Map m, CAOrganization o) => CASettlementCapabilityAdapters.Absent(
            CASettlementCapabilityDomains.Production,
            "no represented production operation and completed work history");
    }

    internal sealed class CALogisticsCapabilityAdapter : ICASettlementCapabilityAdapter
    {
        public CASettlementCapabilityEvidence Read(CARegionalSettlementRecord r,
            Map m, CAOrganization o) => CASettlementCapabilityAdapters.Absent(
            CASettlementCapabilityDomains.Logistics,
            "no represented stock-movement operation and distribution history");
    }

    internal sealed class CACommunicationCapabilityAdapter : ICASettlementCapabilityAdapter
    {
        public CASettlementCapabilityEvidence Read(CARegionalSettlementRecord r,
            Map m, CAOrganization o) => CASettlementCapabilityAdapters.Absent(
            CASettlementCapabilityDomains.Communications,
            "no represented relay operation and communication history");
    }

    internal sealed class CACivicCapabilityAdapter : ICASettlementCapabilityAdapter
    {
        public CASettlementCapabilityEvidence Read(CARegionalSettlementRecord r,
            Map map, CAOrganization org)
        {
            var evidence = CASettlementCapabilityAdapters.Absent(
                CASettlementCapabilityDomains.Civic,
                "no occupied office, executed decision, administrative site, and membership");
            List<CAOffice> offices = org?.offices?.Where(value => value != null
                && value.holderId >= 0).ToList() ?? new List<CAOffice>();
            List<CADecisionEntry> decisions = org?.decisionHistory?
                .Where(value => value != null
                    && IsAdministrativeDecision(value.kind))
                .ToList() ?? new List<CADecisionEntry>();
            CASettlementProgramEntry program = r?.settlementProgram?
                .Entry(CASettlementProgramRegistry.Governance);
            if (offices.Count == 0 || decisions.Count == 0
                || (org?.memberPawnIds?.Count ?? 0) == 0 || program == null
                || program.placedThingIds.Count == 0) return evidence;
            evidence.LevelHint = Math.Min(5, 1 + decisions.Count / 3);
            evidence.Actors.AddRange(offices.Select(value =>
                "pawn:" + value.holderId));
            evidence.Organizations.Add(org.organizationKey);
            evidence.Operations.AddRange(decisions.Select(value =>
                "decision:" + value.tick + ":" + value.kind));
            evidence.Knowledge.AddRange(offices.Select(value =>
                "office-standing:" + value.sourceKey));
            evidence.Material.AddRange(program.placedThingIds);
            evidence.History.AddRange(decisions.Select(value =>
                "decision:" + value.tick + ":" + value.kind));
            return evidence;
        }

        private static bool IsAdministrativeDecision(string kind)
        {
            return kind == "policy" || kind == "policy-imposed"
                || kind == "taxation" || kind == "agreement-made"
                || kind == "office";
        }
    }

    internal sealed class CAResearchCapabilityAdapter : ICASettlementCapabilityAdapter
    {
        public CASettlementCapabilityEvidence Read(CARegionalSettlementRecord r,
            Map map, CAOrganization org)
        {
            var evidence = CASettlementCapabilityAdapters.Absent(
                CASettlementCapabilityDomains.Research,
                "no active research contract with completed work history");
            CASettlementResearchWork work;
            if (map?.GetComponent<CASettlementWorksMapComponent>()
                    ?.TryActiveResearchContract(r?.regionalId + "#" + r?.slot,
                        out work) != true || work == null) return evidence;
            // An active contract proves current work, but practiced capability
            // also requires a recorded completed milestone. No such ledger is
            // represented yet, so the result remains absent.
            evidence.Actors.Add("pawn:" + work.pawnId);
            evidence.Organizations.Add(work.authorityIdentity);
            evidence.Operations.Add("research-episode:" + work.episodeId);
            evidence.Knowledge.Add("research-behavior:" + work.behaviorKey);
            evidence.Material.Add("bench:" + work.bench);
            return evidence;
        }
    }

    internal sealed class CASecurityCapabilityAdapter : ICASettlementCapabilityAdapter
    {
        public CASettlementCapabilityEvidence Read(CARegionalSettlementRecord r,
            Map map, CAOrganization org)
        {
            var evidence = CASettlementCapabilityAdapters.Absent(
                CASettlementCapabilityDomains.Security,
                "no live assigned guards, equipment, practice, and security history");
            if (org == null) return evidence;
            List<int> guards = org.securityPractices.Where(value =>
                    value?.guardPawnIds != null)
                .SelectMany(value => value.guardPawnIds).Distinct().ToList();
            List<Pawn> pawns = map.mapPawns.AllPawnsSpawned.Where(pawn =>
                pawn != null && !pawn.Dead
                && guards.Contains(pawn.thingIDNumber)).ToList();
            List<CADecisionEntry> history = org.decisionHistory
                .Where(value => value != null && value.kind == "security")
                .ToList();
            if (pawns.Count == 0 || history.Count == 0) return evidence;
            evidence.LevelHint = Math.Min(5, 1 + pawns.Count / 2);
            evidence.Actors.AddRange(pawns.Select(pawn =>
                "pawn:" + pawn.thingIDNumber));
            evidence.Organizations.Add(org.organizationKey);
            evidence.Operations.AddRange(org.securityPractices.Where(value =>
                value != null && value.guardPawnIds?.Count > 0).Select(value =>
                    "security-practice:" + (value.name ?? value.kindLabel)));
            evidence.Knowledge.AddRange(pawns.Select(pawn =>
                "assigned-guard:" + pawn.thingIDNumber));
            evidence.Material.AddRange(pawns.SelectMany(pawn =>
                pawn.equipment?.AllEquipmentListForReading
                    ?? new List<ThingWithComps>()).Select(thing =>
                        "equipment:" + thing.thingIDNumber));
            evidence.History.AddRange(history.Select(value =>
                "security-decision:" + value.tick));
            return evidence;
        }
    }

    internal sealed class CACommerceCapabilityAdapter : ICASettlementCapabilityAdapter
    {
        public CASettlementCapabilityEvidence Read(CARegionalSettlementRecord r,
            Map map, CAOrganization org)
        {
            var evidence = CASettlementCapabilityAdapters.Absent(
                CASettlementCapabilityDomains.Commerce,
                "no represented exchange organization and transaction history");
            if (org == null) return evidence;
            string settlement = r.regionalId + "#" + r.slot;
            List<CATransaction> transactions = (CATransactionLedger.Current?
                    .Transactions ?? new List<CATransaction>())
                .Where(value => value != null && new[] { value.buyer,
                    value.seller, value.owner, value.runBy }.Any(party =>
                        party != null && (party.orgKey == settlement
                            || party.orgKey == org.organizationKey)))
                .ToList();
            if (transactions.Count == 0) return evidence;
            evidence.LevelHint = Math.Min(5, 1 + transactions.Count / 3);
            evidence.Actors.AddRange(transactions.SelectMany(value =>
                new[] { value.buyer, value.seller }).Select(
                    CASettlementCapabilityAdapters.Party));
            evidence.Organizations.Add(org.organizationKey);
            evidence.Operations.AddRange(transactions.Select(value =>
                "transaction:" + value.id));
            evidence.Knowledge.AddRange(transactions.Select(value =>
                "exchange-rule:" + value.cause));
            evidence.Material.AddRange(transactions.Select(value =>
                !value.goodDefName.NullOrEmpty()
                    ? "good:" + value.goodDefName
                    : "service:" + value.serviceKind));
            evidence.History.AddRange(transactions.Select(value =>
                "transaction:" + value.id + ":tick:" + value.tick));
            return evidence;
        }
    }
}
