using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // Persistent organization relations and physical facility holdings.
    // Generated records retain their source so scoped regeneration can
    // replace only records it owns.
    public sealed class CAOrganizationRelationsWorldComponent : WorldComponent
    {
        private int campaignSchemaVersion =
            CACampaignCompatibilityKernel.CurrentBoundaryVersion;
        private int legacyAuthoringDataEpoch =
            CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch;
        private List<CARelation> relations = new List<CARelation>();
        private List<CAFacilityHolding> holdings =
            new List<CAFacilityHolding>();
        private List<CARemovedRecord> removedRecords =
            new List<CARemovedRecord>();
        private int nextId = 1;

        public CAOrganizationRelationsWorldComponent(World world)
            : base(world) { }

        public static CAOrganizationRelationsWorldComponent Current =>
            Find.World?.GetComponent<CAOrganizationRelationsWorldComponent>();

        public IReadOnlyList<CARelation> Relations => relations;
        public IReadOnlyList<CAFacilityHolding> Holdings => holdings;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_organizationRelationsSchemaVersion", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyAuthoringDataEpoch,
                    "CA_authoringDataEpoch", 0);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "world.organization-relations", campaignSchemaVersion,
                legacyAuthoringDataEpoch);
            if (readable)
            {
                Scribe_Collections.Look(ref relations, "CA_relations",
                    LookMode.Deep);
                Scribe_Collections.Look(ref holdings, "CA_holdings",
                    LookMode.Deep);
                Scribe_Collections.Look(ref removedRecords,
                    "CA_removedRecords", LookMode.Deep);
                Scribe_Values.Look(ref nextId, "CA_nextRelationId", 1);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.organization-relations",
                    ref campaignSchemaVersion,
                    legacyAuthoringDataEpoch, ValidateCampaignState);
            }
        }

        private string ValidateCampaignState()
        {
            if (relations == null || holdings == null
                || removedRecords == null)
                return "organization-relation owner collections are missing";
            var ids = new HashSet<int>();
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            int maxId = 0;
            for (int i = 0; i < relations.Count; i++)
            {
                CARelation relation = relations[i];
                if (relation == null) return "relation " + i + " is null";
                if (relation.id <= 0 || !ids.Add(relation.id))
                    return "relation id " + relation.id + " is invalid or duplicate";
                if (relation.orgKey.NullOrEmpty() || !relation.PartyConsistent)
                    return "relation " + relation.id + " has an invalid party";
                if (relation.delegatedResponsibilities == null
                    || relation.retainedResponsibilities == null)
                    return "relation " + relation.id
                        + " has missing responsibility collections";
                if (!signatures.Add(Signature(relation)))
                    return "relation " + relation.id + " duplicates a stable identity";
                maxId = Math.Max(maxId, relation.id);
            }
            for (int i = 0; i < holdings.Count; i++)
            {
                CAFacilityHolding holding = holdings[i];
                if (holding == null) return "holding " + i + " is null";
                if (holding.id <= 0 || !ids.Add(holding.id))
                    return "holding id " + holding.id + " is invalid or duplicate";
                if (!signatures.Add(Signature(holding)))
                    return "holding " + holding.id + " duplicates a stable identity";
                if (holding.beneficiaries == null)
                    return "holding " + holding.id
                        + " has a missing beneficiary collection";
                maxId = Math.Max(maxId, holding.id);
            }
            if (nextId <= maxId)
                return "next relation id " + nextId
                    + " does not follow persisted id " + maxId;
            var removed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < removedRecords.Count; i++)
            {
                CARemovedRecord record = removedRecords[i];
                if (record == null || record.recordKey.NullOrEmpty())
                    return "removed relation record " + i + " is incomplete";
                if (!removed.Add(record.recordKey))
                    return "removed relation identity " + record.recordKey
                        + " is duplicated";
            }
            return null;
        }

        public int TakeId() { return nextId++; }

        // Organization membership is typed by its structural role. Settlement
        // authority and federation membership therefore share storage without
        // sharing behavior.
        private IEnumerable<CARelation> Memberships(string parentKey,
            CARelationRoleDef roleDef)
        {
            if (parentKey.NullOrEmpty() || roleDef == null) yield break;
            for (int i = 0; i < relations.Count; i++)
            {
                CARelation relation = relations[i];
                if (relation == null || relation.orgKey != parentKey) continue;
                if (relation.roleDef != roleDef) continue;
                if (relation.Party.kind != CARelationPartyKind.Organization)
                    continue;
                if (relation.partyOrgKey.NullOrEmpty()) continue;
                if (relation.Expired(Find.TickManager?.TicksGame ?? 0))
                    continue;
                yield return relation;
            }
        }

        private CARelation Membership(string parentKey, string memberKey,
            CARelationRoleDef roleDef)
        {
            if (memberKey.NullOrEmpty()) return null;
            foreach (CARelation relation in Memberships(parentKey, roleDef))
                if (relation.partyOrgKey == memberKey) return relation;
            return null;
        }

        internal CARelation OrganizationMembership(string parentKey,
            string memberKey, CARelationRoleDef roleDef)
        {
            return Membership(parentKey, memberKey, roleDef);
        }

        private List<string> MemberKeys(string parentKey,
            CARelationRoleDef roleDef)
        {
            var keys = new List<string>();
            foreach (CARelation relation in Memberships(parentKey, roleDef))
                if (!keys.Contains(relation.partyOrgKey))
                    keys.Add(relation.partyOrgKey);
            return keys;
        }

        private bool Manages(string parentKey, string memberKey,
            string responsibility, CARelationRoleDef roleDef)
        {
            foreach (CARelation relation in Memberships(parentKey, roleDef))
                if (relation.partyOrgKey == memberKey
                    && relation.Delegates(responsibility)) return true;
            return false;
        }

        private List<string> SharedResponsibilities(string parentKey,
            CARelationRoleDef roleDef)
        {
            var responsibilities = new List<string>();
            foreach (CARelation relation in Memberships(parentKey, roleDef))
                for (int i = 0;
                    i < relation.delegatedResponsibilities.Count; i++)
                    if (!responsibilities.Contains(
                            relation.delegatedResponsibilities[i]))
                        responsibilities.Add(
                            relation.delegatedResponsibilities[i]);
            responsibilities.Sort(StringComparer.Ordinal);
            return responsibilities;
        }

        public IEnumerable<CARelation> FederationMemberships(
            string federationKey)
        {
            return Memberships(federationKey,
                CARelationRoleDefOf.CA_Role_FederationMember);
        }

        public CARelation FederationMembership(string federationKey,
            string memberKey)
        {
            return Membership(federationKey, memberKey,
                CARelationRoleDefOf.CA_Role_FederationMember);
        }

        public bool IsFederationMember(string memberKey, string federationKey)
        {
            return FederationMembership(federationKey, memberKey) != null;
        }

        public List<string> FederationMemberKeys(string federationKey)
        {
            return MemberKeys(federationKey,
                CARelationRoleDefOf.CA_Role_FederationMember);
        }

        public bool FederationManages(string federationKey,
            string memberKey, string responsibility)
        {
            return Manages(federationKey, memberKey, responsibility,
                CARelationRoleDefOf.CA_Role_FederationMember);
        }

        public List<string> FederationSharedResponsibilities(
            string federationKey)
        {
            return SharedResponsibilities(federationKey,
                CARelationRoleDefOf.CA_Role_FederationMember);
        }

        public IEnumerable<CARelation> SettlementMemberships(
            string factionOrganizationKey)
        {
            return Memberships(factionOrganizationKey,
                CARelationRoleDefOf.CA_Role_SettlementMember);
        }

        public CARelation SettlementMembership(string factionOrganizationKey,
            string settlementKey)
        {
            return Membership(factionOrganizationKey, settlementKey,
                CARelationRoleDefOf.CA_Role_SettlementMember);
        }

        public List<string> SettlementMemberKeys(string factionOrganizationKey)
        {
            return MemberKeys(factionOrganizationKey,
                CARelationRoleDefOf.CA_Role_SettlementMember);
        }

        public List<string> SettlementSharedResponsibilities(
            string factionOrganizationKey)
        {
            return SharedResponsibilities(factionOrganizationKey,
                CARelationRoleDefOf.CA_Role_SettlementMember);
        }

        // Stable identity used to prevent regeneration from restoring a
        // removed record or duplicating an authored record.
        internal static string Signature(CARelation r)
        {
            string baseKey = "rel|" + r.orgKey + "|"
                + (r.partyOrgKey ?? r.partyPawnId.ToString()) + "|"
                + r.role;
            return r.roleDef == null ? baseKey
                : baseKey + "|" + r.roleDef.defName;
        }

        internal static string Signature(CAFacilityHolding h)
        {
            return "hold|" + h.facilityKind + "|" + h.mapId + "|"
                + h.cell + "|" + (h.programSignature ?? "") + "|"
                + (h.assetRole ?? "");
        }

        private bool WasRemoved(string signature)
        {
            for (int i = 0; i < removedRecords.Count; i++)
                if (removedRecords[i].recordKey == signature) return true;
            return false;
        }

        // Authored records prevent generation from creating a duplicate with
        // the same identity.
        private bool ExistingAuthoredRecord(string signature)
        {
            for (int i = 0; i < relations.Count; i++)
                if (!relations[i].origin.Replaceable
                    && Signature(relations[i]) == signature) return true;
            for (int i = 0; i < holdings.Count; i++)
                if (!holdings[i].origin.Replaceable
                    && Signature(holdings[i]) == signature) return true;
            return false;
        }

        private bool AlreadyHandled(string signature)
        {
            return WasRemoved(signature) || ExistingAuthoredRecord(signature);
        }

        // Do not restore removed generated records or duplicate authored ones.
        public bool Add(CARelation r)
        {
            if (AlreadyHandled(Signature(r))) return false;
            if (r.id == 0) r.id = TakeId();
            relations.Add(r);
            return true;
        }

        public bool Add(CAFacilityHolding h)
        {
            if (AlreadyHandled(Signature(h))) return false;
            if (h.id == 0) h.id = TakeId();
            holdings.Add(h);
            return true;
        }

        // Record authored removal so regeneration does not restore the item.
        public void Remove(CARelation r)
        {
            RecordRemoval(Signature(r), r.origin.originKey);
            relations.Remove(r);
        }

        public void Remove(CAFacilityHolding h)
        {
            RecordRemoval(Signature(h), h.origin.originKey);
            holdings.Remove(h);
        }

        // Remove a generated relation without recording an authored removal.
        internal void RemoveDerivedRelation(CARelation r)
        {
            relations.Remove(r);
        }

        internal void RemoveDerivedHolding(CAFacilityHolding holding)
        {
            holdings.Remove(holding);
        }

        // The relations owner alone mutates holding identity. Program-asset
        // rebuilding asks for one validated atomic rebind rather than editing
        // the exposed read-only collection.
        internal bool TryRebindProgramAsset(string programSignature,
            string assetRole, Thing rebuilt, out string failure)
        {
            failure = null;
            if (programSignature.NullOrEmpty() || assetRole.NullOrEmpty()
                || rebuilt == null || !rebuilt.Spawned)
            {
                failure = "holding rebind lacks an exact live program asset";
                return false;
            }
            List<CAFacilityHolding> matches = holdings.Where(item =>
                item != null && item.programSignature == programSignature
                && item.assetRole == assetRole).ToList();
            if (matches.Count > 1)
            {
                failure = "several holdings claim one program asset role";
                return false;
            }
            if (matches.Count == 0) return true;
            CAFacilityHolding holding = matches[0];
            string targetSignature = "hold|" + holding.facilityKind + "|"
                + rebuilt.Map.uniqueID + "|" + rebuilt.Position + "|"
                + programSignature + "|" + assetRole;
            if (holdings.Any(item => item != null && item != holding
                    && Signature(item) == targetSignature))
            {
                failure = "the rebuilt holding identity already exists";
                return false;
            }
            holding.thingId = rebuilt.thingIDNumber;
            holding.mapId = rebuilt.Map.uniqueID;
            holding.cell = rebuilt.Position;
            return true;
        }

        private void RecordRemoval(string signature, string originKey)
        {
            if (WasRemoved(signature)) return;
            removedRecords.Add(new CARemovedRecord
            {
                recordKey = signature,
                originKey = originKey,
                removedTick = Find.TickManager?.TicksGame ?? 0
            });
        }

        // Remove only records generated by this source. Authored records remain.
        public int ClearDerived(string originKey)
        {
            int removed = 0;
            int relationsRemoved = relations.RemoveAll(r =>
                r.origin.Replaceable && r.origin.originKey == originKey);
            removed += relationsRemoved;
            removed += holdings.RemoveAll(h =>
                h.origin.Replaceable && h.origin.originKey == originKey);
            return removed;
        }

        // ---- reading -----------------------------------------------

        public List<CARelation> RelationsOf(int pawnId)
        {
            var found = new List<CARelation>();
            for (int i = 0; i < relations.Count; i++)
                if (relations[i].PawnPartyId == pawnId)
                    found.Add(relations[i]);
            return found;
        }

        public List<CARelation> RelationsIn(string orgKey)
        {
            var found = new List<CARelation>();
            for (int i = 0; i < relations.Count; i++)
                if (relations[i].orgKey == orgKey)
                    found.Add(relations[i]);
            return found;
        }

        public CAFacilityHolding HoldingOf(int thingId)
        {
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].thingId == thingId) return holdings[i];
            return null;
        }

    }
}
