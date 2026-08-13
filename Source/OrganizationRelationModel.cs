using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Source of a generated or player-authored record.
    public enum CAProvenance : byte
    {
        Derived = 0,
        Authored = 1
    }

    // Stores the source and the choice or parent record that created it.
    public struct CAOrigin
    {
        public CAProvenance source;
        public string originKey;

        public bool Replaceable => source == CAProvenance.Derived;

        public static CAOrigin Derived(string from)
        {
            return new CAOrigin
            {
                source = CAProvenance.Derived,
                originKey = from
            };
        }

        public static CAOrigin Authored(string from)
        {
            return new CAOrigin
            {
                source = CAProvenance.Authored,
                originKey = from
            };
        }

        public void Expose(string prefix)
        {
            Scribe_Values.Look(ref source, prefix + "Source",
                CAProvenance.Derived);
            Scribe_Values.Look(ref originKey, prefix + "Origin");
        }
    }

    // Records deletion of a generated record so regeneration does not restore
    // it.
    public sealed class CARemovedRecord : IExposable
    {
        public string recordKey;
        public string originKey;
        public int removedTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref recordKey, "recordKey");
            Scribe_Values.Look(ref originKey, "originKey");
            Scribe_Values.Look(ref removedTick, "removedTick", 0);
        }
    }

    // Responsibilities delegated to an organization or retained by a member.
    // Each key has a runtime consumer.
    public static class CAResponsibilities
    {
        public const string Work = "work";
        public const string Production = "production";
        public const string Property = "property";
        public const string Taxes = "taxes";
        public const string EmergencyTaking = "emergency taking";
        public const string Decisions = "decisions";
        public const string ArmedForce = "armed force";
        public const string Diplomacy = "diplomacy";
        public const string Defense = "defense";
        public const string Disputes = "disputes";

        public static readonly string[] All =
        {
            Work, Production, Property, Taxes, EmergencyTaking, Decisions,
            ArmedForce, Diplomacy, Defense, Disputes
        };
    }

    // Compensation and participation are stored separately.
    public static class CACompensationKinds
    {
        public const string None = "none";
        public const string Wage = "wage";
        public const string Share = "share";
        public const string Keep = "keep";        // works for themselves
        public const string Ration = "ration";
        public const string Maintenance = "maintenance"; // bed and board
        public const string Dues = "dues";        // pays IN to belong
    }

    public static class CAVoiceKinds
    {
        public const string None = "none";
        public const string Heard = "heard";      // consulted, not counted
        public const string Vote = "vote";
        public const string Veto = "veto";
    }

    // Whether anything actually protects them, and by whose word.
    public static class CAProtectionKinds
    {
        public const string None = "none";
        public const string Custom = "custom";
        public const string OrganizationRule = "organization rule";
        public const string FactionRule = "faction rule";
    }

    public static class CAEntryKinds
    {
        public const string Free = "free";
        public const string Birth = "birth";
        public const string Purchase = "purchase";
        public const string Appointment = "appointment";
        public const string Conscription = "conscription";
        public const string Capture = "capture";
    }

    public static class CAExitKinds
    {
        public const string Free = "free";
        public const string Notice = "notice";
        public const string Bonded = "bonded";    // owes before leaving
        public const string Forbidden = "forbidden";
    }

    // A relation belongs to either a pawn or an organization.
    public enum CARelationPartyKind : byte
    {
        None = 0,
        Pawn = 1,
        Organization = 2
    }

    // Typed party reference used by membership and authority queries.
    public struct CARelationPartyRef
    {
        public CARelationPartyKind kind;
        public int pawnId;
        public string orgKey;

        public bool IsPawn => kind == CARelationPartyKind.Pawn;
        public bool IsOrganization => kind == CARelationPartyKind.Organization;

        public static CARelationPartyRef OfPawn(int id)
        {
            return new CARelationPartyRef
            {
                kind = CARelationPartyKind.Pawn,
                pawnId = id,
                orgKey = null
            };
        }

        public static CARelationPartyRef OfOrganization(string key)
        {
            return new CARelationPartyRef
            {
                kind = CARelationPartyKind.Organization,
                pawnId = -1,
                orgKey = key
            };
        }

        // Well-formedness for the selected party kind.
        public bool Valid
        {
            get
            {
                switch (kind)
                {
                    case CARelationPartyKind.Pawn:
                        return pawnId >= 0 && orgKey.NullOrEmpty();
                    case CARelationPartyKind.Organization:
                        return pawnId < 0 && !orgKey.NullOrEmpty();
                    default:
                        return false;
                }
            }
        }

        public override string ToString()
        {
            switch (kind)
            {
                case CARelationPartyKind.Pawn: return "pawn:" + pawnId;
                case CARelationPartyKind.Organization: return "org:" + orgKey;
                default: return "none";
            }
        }
    }

    // Structural relation roles used by runtime queries.
    public class CARelationRoleDef : Def
    {
    }

    [DefOf]
    public static class CARelationRoleDefOf
    {
        // An organization that belongs to a federation.
        public static CARelationRoleDef CA_Role_FederationMember;
        // A settlement governed by its faction organization.
        public static CARelationRoleDef CA_Role_SettlementMember;

        static CARelationRoleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(
                typeof(CARelationRoleDefOf));
        }
    }

    // One relation records how a party belongs to an organization.
    //
    // Employment is a subtype. Political membership is a subtype. So
    // are guild membership, military service, tenancy, apprenticeship
    // and bondage. They are not different ontologies; they are
    // different values in the same fields, which is exactly why a
    // person can hold several at once and why those can contradict
    // each other. A citizen who is also an employee of a firm that the
    // administration regulates is three relations, and the friction
    // between them is the politics.
    //
    // A relation connects a pawn or organization to an organization.
    public sealed class CARelation : IExposable
    {
        public int id;
        // Backing fields for the typed party reference.
        public int partyPawnId = -1;
        public string partyOrgKey;
        public CARelationPartyKind partyKind = CARelationPartyKind.None;
        // the organization they stand in relation to
        public string orgKey;

        // Descriptive role for relations without a structural role Def.
        public string role;
        // Structural role. Organization membership uses this Def rather than
        // the descriptive string above.
        public CARelationRoleDef roleDef;
        // Status remains separate from role. A worker may still count as a
        // migrant, prisoner, or other non-member status.
        public string status;

        // What they handed over, and what they kept or were given.
        public List<string> delegatedResponsibilities = new List<string>();
        public List<string> retainedResponsibilities = new List<string>();

        public string compensation = CACompensationKinds.None;
        public float compensationRate;
        public string voice = CAVoiceKinds.None;
        public string protection = CAProtectionKinds.None;
        public string entry = CAEntryKinds.Free;
        public string exit = CAExitKinds.Free;

        public int startTick = -1;
        // A temporary agreement uses this relation with a sunset. Expired
        // terms may still be enforced by an organization that retains force;
        // the saved expiry and current practice remain separate facts.
        public int sunsetTick = -1;

        // The relation's existence and its current terms may have different
        // sources. Settlement membership follows the live settlement roster;
        // delegated responsibilities may still be explicitly authored.
        public CAOrigin origin = CAOrigin.Derived(null);
        public CAOrigin termsOrigin = CAOrigin.Derived(null);

        public bool Expired(int now)
        {
            return sunsetTick >= 0 && now >= sunsetTick;
        }

        // Typed view over the serialized party fields.
        public CARelationPartyRef Party
        {
            get
            {
                return new CARelationPartyRef
                {
                    kind = partyKind,
                    pawnId = partyPawnId,
                    orgKey = partyOrgKey
                };
            }
            set
            {
                partyKind = value.kind;
                partyPawnId = value.kind == CARelationPartyKind.Pawn
                    ? value.pawnId : -1;
                partyOrgKey = value.kind == CARelationPartyKind.Pawn
                    ? null : value.orgKey;
            }
        }

        // Convenience reads for relation consumers.
        public bool IsPawnParty => Party.IsPawn;
        public bool IsOrganizationParty => Party.IsOrganization;
        public int PawnPartyId => Party.IsPawn ? partyPawnId : -1;
        public string OrganizationPartyKey
            => Party.IsOrganization ? partyOrgKey : null;

        internal bool PartyConsistent
        {
            get
            {
                if (!Party.Valid) return false;
                switch (partyKind)
                {
                    case CARelationPartyKind.Pawn:
                        return partyPawnId >= 0 && partyOrgKey.NullOrEmpty();
                    case CARelationPartyKind.Organization:
                        return partyPawnId < 0 && !partyOrgKey.NullOrEmpty();
                    default:
                        return false;
                }
            }
        }

        public bool Delegates(string responsibility)
        {
            return delegatedResponsibilities.Contains(responsibility);
        }

        public bool Retains(string responsibility)
        {
            return retainedResponsibilities.Contains(responsibility);
        }

        public string Key => "rel:" + id;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref partyPawnId, "partyPawnId", -1);
            Scribe_Values.Look(ref partyOrgKey, "partyOrgKey");
            Scribe_Values.Look(ref partyKind, "partyKind",
                CARelationPartyKind.None);
            Scribe_Values.Look(ref orgKey, "orgKey");
            Scribe_Values.Look(ref role, "role");
            Scribe_Defs.Look(ref roleDef, "roleDef");
            Scribe_Values.Look(ref status, "status");
            Scribe_Collections.Look(ref delegatedResponsibilities,
                "delegatedResponsibilities", LookMode.Value);
            Scribe_Collections.Look(ref retainedResponsibilities,
                "retainedResponsibilities", LookMode.Value);
            Scribe_Values.Look(ref compensation, "compensation",
                CACompensationKinds.None);
            Scribe_Values.Look(ref compensationRate, "compensationRate",
                0f);
            Scribe_Values.Look(ref voice, "voice", CAVoiceKinds.None);
            Scribe_Values.Look(ref protection, "protection",
                CAProtectionKinds.None);
            Scribe_Values.Look(ref entry, "entry", CAEntryKinds.Free);
            Scribe_Values.Look(ref exit, "exit", CAExitKinds.Free);
            Scribe_Values.Look(ref startTick, "startTick", -1);
            Scribe_Values.Look(ref sunsetTick, "sunsetTick", -1);
            origin.Expose("origin");
            termsOrigin.Expose("termsOrigin");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (delegatedResponsibilities == null)
                    delegatedResponsibilities = new List<string>();
                if (retainedResponsibilities == null)
                    retainedResponsibilities = new List<string>();
            }
        }
    }

    // Facility ownership and operation are separate records.
    public static class CACapitalSources
    {
        public const string Private = "private";
        public const string Loan = "loan";
        public const string Treasury = "treasury";
        public const string Dues = "dues";
        public const string Tribute = "tribute";
        public const string SharedWork = "shared work";
    }

    public static class CAAllocationRules
    {
        public const string Sale = "sale";
        public const string Contract = "contract";
        public const string Ration = "ration";
        public const string Entitlement = "entitlement";
        public const string Assignment = "assignment";
        public const string InternalUse = "internal";
        public const string OpenAccess = "open";
    }

    public static class CAOversightKinds
    {
        public const string None = "none";
        public const string Competition = "competition";
        public const string GuildRule = "guild";
        public const string PublicOffice = "office";
        public const string Law = "law";
        public const string Patron = "patron";
    }

    public static class CALiabilityKinds
    {
        public const string Owner = "owner";
        public const string Treasury = "treasury";
        public const string Residents = "residents";
        public const string Creditors = "creditors";
    }

    public sealed class CAFacilityHolding : IExposable
    {
        public int id;
        // Physical building identity.
        public int thingId = -1;
        public int mapId = -1;
        public IntVec3 cell = IntVec3.Invalid;
        public string facilityKind;   // "well", "smokehouse", "dock"

        public string ownerOrgKey;
        public int ownerPawnId = -1;
        public string operatorOrgKey;
        public int operatorPawnId = -1;
        // Generic identities preserve domestic-unit and other typed operators
        // without pretending they are organizations or pawns.
        public string ownerIdentity;
        public string operatorIdentity;
        public int provisionArrangementKey;
        public int provisionNodeIndex = -1;
        public string programKey;
        public string programSignature;
        public string assetRole;

        public string capitalSource = CACapitalSources.Private;
        public string allocationRule = CAAllocationRules.InternalUse;
        public string oversight = CAOversightKinds.None;
        public string liability = CALiabilityKinds.Owner;
        // Statuses entitled to output; empty means no restriction.
        public List<string> beneficiaries = new List<string>();

        public CAOrigin origin = CAOrigin.Derived(null);

        public string Key => "hold:" + id;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref thingId, "thingId", -1);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref facilityKind, "facilityKind");
            Scribe_Values.Look(ref ownerOrgKey, "ownerOrgKey");
            Scribe_Values.Look(ref ownerPawnId, "ownerPawnId", -1);
            Scribe_Values.Look(ref operatorOrgKey, "operatorOrgKey");
            Scribe_Values.Look(ref operatorPawnId, "operatorPawnId", -1);
            Scribe_Values.Look(ref ownerIdentity, "ownerIdentity");
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref provisionArrangementKey,
                "provisionArrangementKey", 0);
            Scribe_Values.Look(ref provisionNodeIndex,
                "provisionNodeIndex", -1);
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref programSignature, "programSignature");
            Scribe_Values.Look(ref assetRole, "assetRole");
            Scribe_Values.Look(ref capitalSource, "capitalSource",
                CACapitalSources.Private);
            Scribe_Values.Look(ref allocationRule, "allocationRule",
                CAAllocationRules.InternalUse);
            Scribe_Values.Look(ref oversight, "oversight",
                CAOversightKinds.None);
            Scribe_Values.Look(ref liability, "liability",
                CALiabilityKinds.Owner);
            Scribe_Collections.Look(ref beneficiaries, "beneficiaries",
                LookMode.Value);
            origin.Expose("origin");
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && beneficiaries == null)
                beneficiaries = new List<string>();
        }
    }
}
