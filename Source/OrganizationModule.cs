using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse.AI;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Settlement organization supplies policies, plans, and assigned work.
    // The player's colony starts from existing squad leads, Ideoligion roles,
    // and standing arrangements. NPC settlements receive the same persistent
    // structure during generation.

    public sealed class CAOffice : IExposable
    {
        // Stable derivation key ("ideo-leader", "squad-2", "squad-2-team-1")
        // so player renames survive re-sync while holders track reality.
        public string sourceKey;
        public string name;
        public int seniority;
        public int holderId = -1;
        public string holderLabel;
        public string grants;
        // Succession begins as merit-based. A successful inheritance makes
        // the office hereditary. An unrelated seizure marks the office as
        // usurped until one of the usurper's relatives inherits it.
        public string successionRule = "merit";
        public int lastHolderId = -1;
        public int kinSuccessions;
        public bool usurped;
        public int vacantSinceTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sourceKey, "sourceKey");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref seniority, "seniority", 0);
            Scribe_Values.Look(ref holderId, "holderId", -1);
            Scribe_Values.Look(ref holderLabel, "holderLabel");
            Scribe_Values.Look(ref grants, "grants");
            Scribe_Values.Look(ref successionRule, "successionRule",
                "merit");
            Scribe_Values.Look(ref lastHolderId, "lastHolderId", -1);
            Scribe_Values.Look(ref kinSuccessions, "kinSuccessions", 0);
            Scribe_Values.Look(ref usurped, "usurped", false);
            Scribe_Values.Look(ref vacantSinceTick, "vacantSinceTick", -1);
        }
    }

    public sealed class CAOrganizationGroup : IExposable
    {
        public string name;
        public List<int> memberIds = new List<int>();
        public float standing;

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref memberIds, "memberIds", LookMode.Value);
            Scribe_Values.Look(ref standing, "standing", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && memberIds == null)
                memberIds = new List<int>();
        }
    }

    public sealed class CAOrganizationCustom : IExposable
    {
        public string key;
        public string source;
        public int adoptedTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref adoptedTick, "adoptedTick", 0);
        }
    }

    public sealed class CASecurityPractice : IExposable
    {
        public int mapId = -1;
        public int arrangementId = -1;
        public string kindLabel;
        public string name;
        public string assignedOffice;
        public string programKey;
        public string operatorIdentity;
        public string programSignature;
        // The actual guards, by pawn id - a practice without named
        // people is a label; with them, patrol behavior has someone to
        // send around the walls.
        public List<int> guardPawnIds = new List<int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref arrangementId, "arrangementId", -1);
            Scribe_Values.Look(ref kindLabel, "kindLabel");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref assignedOffice, "assignedOffice");
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref programSignature, "programSignature");
            Scribe_Collections.Look(ref guardPawnIds, "guardPawnIds",
                LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && guardPawnIds == null)
                guardPawnIds = new List<int>();
        }
    }

    // Typed organization history supports specific conduct checks without
    // reducing them to one reputation score.
    public sealed class CADecisionEntry : IExposable
    {
        public int tick;
        public string kind = "note";
        public string text;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref kind, "kind", "note");
            Scribe_Values.Look(ref text, "text");
        }
    }

    // A scoped agreement between organizations. It records duration,
    // contributions, command, breach terms, and exit terms separately from
    // the factions' overall relations.
    public sealed class CAAgreement : IExposable
    {
        public int id;
        public string kind;
        public string partyA;
        public string partyB;
        public string brokerKey;
        public int sinceTick;
        public string scopeKind = "general";
        public string scopeValue;
        public int sunsetTick = -1;
        public string contributionsA;
        public string contributionsB;
        public string commandOffice;
        public string breachConditions;
        public string exitProcedure;
        public int dissolvedTick = -1;
        // Tribute records its payer. Protection records its protector.
        public string payerKey;
        public string protectorKey;
        public int lastNpcPayTick = -1;
        // A committed delivery in transit: deducted at dispatch, delivered
        // (or intercepted) at arrival. Form follows the payer's technology.
        public int pendingAmount;
        public int pendingArriveTick = -1;
        public string pendingForm;
        // Set when an agreement was signed without the protector's consent.
        public bool unauthorized;
        // A completed term and a broken agreement affect later negotiations
        // differently.
        public bool broken;

        public bool Active
        {
            get { return dissolvedTick < 0; }
        }

        public bool Involves(string key)
        {
            return partyA == key || partyB == key;
        }

        public string OtherParty(string key)
        {
            return partyA == key ? partyB : partyA;
        }

        public string Summary()
        {
            string s = kind + " with scope " + scopeKind
                + (scopeValue.NullOrEmpty() ? "" : " (" + scopeValue + ")");
            if (sunsetTick > 0)
                s += ", sunset day " + (sunsetTick / 60000);
            if (!brokerKey.NullOrEmpty()) s += ", brokered";
            if (kind == "protection")
                s += protectorKey == "player" ? ", we protect them"
                    : ", they protect us";
            else if (kind == "tribute")
                s += payerKey == "player" ? ", we pay" : ", they pay";
            return s;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref partyA, "partyA");
            Scribe_Values.Look(ref partyB, "partyB");
            Scribe_Values.Look(ref brokerKey, "brokerKey");
            Scribe_Values.Look(ref sinceTick, "sinceTick", 0);
            Scribe_Values.Look(ref scopeKind, "scopeKind", "general");
            Scribe_Values.Look(ref scopeValue, "scopeValue");
            Scribe_Values.Look(ref sunsetTick, "sunsetTick", -1);
            Scribe_Values.Look(ref contributionsA, "contributionsA");
            Scribe_Values.Look(ref contributionsB, "contributionsB");
            Scribe_Values.Look(ref commandOffice, "commandOffice");
            Scribe_Values.Look(ref breachConditions, "breachConditions");
            Scribe_Values.Look(ref exitProcedure, "exitProcedure");
            Scribe_Values.Look(ref dissolvedTick, "dissolvedTick", -1);
            Scribe_Values.Look(ref broken, "broken", false);
            Scribe_Values.Look(ref payerKey, "payerKey");
            Scribe_Values.Look(ref protectorKey, "protectorKey");
            Scribe_Values.Look(ref lastNpcPayTick, "lastNpcPayTick", -1);
            Scribe_Values.Look(ref unauthorized, "unauthorized", false);
            Scribe_Values.Look(ref pendingAmount, "pendingAmount", 0);
            Scribe_Values.Look(ref pendingArriveTick, "pendingArriveTick",
                -1);
            Scribe_Values.Look(ref pendingForm, "pendingForm");
        }
    }

    // Breach cases move from obligation through evidence, explanation,
    // judgment, and remedy. A missed obligation is not automatically a
    // deliberate breach.
    public sealed class CABreachCase : IExposable
    {
        public int id;
        public int agreementId = -1;
        public string kind = "response";
        public int baselineGoodwill = int.MinValue;
        public string obligation;
        public string obligedKey;
        public string claimantKey;
        public int openedTick;
        public int deadlineTick;
        public string stage = "obligation";
        public string evidence = "";
        public int allegationTick = -1;
        public string explanation;
        public string resolution;
        public int resolvedTick = -1;

        public bool Open
        {
            get { return resolvedTick < 0; }
        }

        public void AddEvidence(string e)
        {
            if (evidence.Length > 300) return;
            evidence = evidence.Length == 0 ? e : evidence + "; " + e;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref agreementId, "agreementId", -1);
            Scribe_Values.Look(ref kind, "kind", "response");
            Scribe_Values.Look(ref baselineGoodwill, "baselineGoodwill",
                int.MinValue);
            Scribe_Values.Look(ref obligation, "obligation");
            Scribe_Values.Look(ref obligedKey, "obligedKey");
            Scribe_Values.Look(ref claimantKey, "claimantKey");
            Scribe_Values.Look(ref openedTick, "openedTick", 0);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick", 0);
            Scribe_Values.Look(ref stage, "stage", "obligation");
            Scribe_Values.Look(ref evidence, "evidence", "");
            Scribe_Values.Look(ref allegationTick, "allegationTick", -1);
            Scribe_Values.Look(ref explanation, "explanation");
            Scribe_Values.Look(ref resolution, "resolution");
            Scribe_Values.Look(ref resolvedTick, "resolvedTick", -1);
        }
    }

    // Records who acted against an agreement party and whether the act was
    // authorized by the organization. Capture occurs at the act so later
    // interpretation does not have to infer it.
    public sealed class CAHostileActRecord : IExposable
    {
        public int tick;
        public int pawnId;
        public string pawnLabel;
        public int factionLoadId = -1;
        public bool authorized;
        public bool mentalBreak;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref pawnId, "pawnId", 0);
            Scribe_Values.Look(ref pawnLabel, "pawnLabel");
            Scribe_Values.Look(ref factionLoadId, "factionLoadId", -1);
            Scribe_Values.Look(ref authorized, "authorized", false);
            Scribe_Values.Look(ref mentalBreak, "mentalBreak", false);
        }
    }

    // An incoming proposal: an NPC organization spoke first. Held for
    // the player's decision - accept, decline, or let it lapse - with
    // the initiator's motive argued in the open.
    public sealed class CAAgreementOffer : IExposable
    {
        public int id;
        public string fromKey;
        public string kind;
        public string protectorKey;
        public string payerKey;
        public string scopeKind = "general";
        public int sunsetDays;
        public string reason;
        public int createdTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref fromKey, "fromKey");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref protectorKey, "protectorKey");
            Scribe_Values.Look(ref payerKey, "payerKey");
            Scribe_Values.Look(ref scopeKind, "scopeKind", "general");
            Scribe_Values.Look(ref sunsetDays, "sunsetDays", 0);
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
        }
    }

    public static class CAAgreementCatalog
    {
        public static readonly string[] Kinds =
        {
            "recognized claim", "shared warnings", "non-aggression",
            "tribute", "military access", "defense", "protection"
        };
        public static readonly string[] ScopeKinds =
        {
            "general", "one enemy", "one area", "one campaign"
        };
    }

    // Reads current faction relations for organization-facing decisions and
    // display. No parallel stance is saved on the organization.
    internal static class CAOrganizationStances
    {
        internal static string Between(CAOrganization left,
            CAOrganization right)
        {
            if (left == null || right == null) return "Neutral";
            if (left == right || left.organizationKey == right.organizationKey)
                return "Same organization";
            if ((left.organizationKey == "player"
                    && right.affiliatedWithPlayer)
                || (right.organizationKey == "player"
                    && left.affiliatedWithPlayer)) return "Ally";
            Faction leftFaction = FactionOf(left);
            Faction rightFaction = FactionOf(right);
            if (leftFaction == null || rightFaction == null) return "Neutral";
            if (leftFaction == rightFaction) return "Same faction";
            try { return leftFaction.RelationKindWith(rightFaction).ToString(); }
            catch { return "Neutral"; }
        }

        internal static List<CAOrganization> Neighbors(CAOrganization source,
            CAOrganizationWorldComponent world)
        {
            var result = new List<CAOrganization>();
            if (source == null || world == null) return result;
            if (source.IsFederation || source.IsFactionOrganization
                || source.organizationKind == CAOrganizationKind.Group)
                return result;
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            if (regional == null) return result;

            var regionKeys = new HashSet<string>();
            var settlementKeys = new HashSet<string>();
            bool includeColony = false;
            CARegionalSettlementRecord sourceRecord = RecordOf(source,
                regional);
            if (sourceRecord != null)
            {
                regionKeys.Add(sourceRecord.regionKey ?? "");
                includeColony = PlayerHasRegion(sourceRecord.regionKey,
                    sourceRecord.mapSize, regional);
            }
            else
            {
                foreach (Map map in RelevantMaps(source))
                {
                    if (map == null) continue;
                    includeColony |= map.IsPlayerHome;
                    foreach (CARegionalSettlementRecord record in
                        regional.ForMap(map))
                        if (record != null)
                            regionKeys.Add(record.regionKey ?? "");
                }
            }

            for (int i = 0; i < regional.Records.Count; i++)
            {
                CARegionalSettlementRecord record = regional.Records[i];
                if (record == null
                    || !regionKeys.Contains(record.regionKey ?? "")) continue;
                settlementKeys.Add(record.regionalId + "#" + record.slot);
            }
            foreach (string key in settlementKeys)
            {
                CAOrganization candidate = world.ByKey(key);
                if (candidate != null && candidate != source
                    && candidate.IsSettlement) result.Add(candidate);
            }
            if (includeColony && source.organizationKey != "player")
            {
                CAOrganization colony = world.EnsureColony();
                if (colony != source) result.Add(colony);
            }
            return result;
        }

        private static CARegionalSettlementRecord RecordOf(
            CAOrganization organization, CARegionalWorldComponent regional)
        {
            if (organization == null || regional == null) return null;
            for (int i = 0; i < regional.Records.Count; i++)
            {
                CARegionalSettlementRecord record = regional.Records[i];
                if (record != null && record.regionalId + "#" + record.slot
                    == organization.organizationKey) return record;
            }
            return null;
        }

        private static IEnumerable<Map> RelevantMaps(CAOrganization source)
        {
            if (source.organizationKey == "player")
            {
                foreach (Map map in Find.Maps)
                    if (map != null && map.IsPlayerHome) yield return map;
                yield break;
            }
            if (source.organizationKind != CAOrganizationKind.Household
                || source.organizationKey.NullOrEmpty()) yield break;
            string[] parts = source.organizationKey.Split(':');
            if (parts.Length < 3 || !int.TryParse(parts[1], out int mapId))
                yield break;
            foreach (Map map in Find.Maps)
                if (map != null && map.uniqueID == mapId)
                {
                    yield return map;
                    yield break;
                }
        }

        private static bool PlayerHasRegion(string regionKey, int mapSize,
            CARegionalWorldComponent regional)
        {
            foreach (Map map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;
                foreach (CARegionalSettlementRecord record in
                    regional.ForMap(map))
                    if (record != null && record.regionKey == regionKey
                        && record.mapSize == mapSize) return true;
            }
            return false;
        }

        internal static int HostileNeighborCount(CAOrganization source,
            CAOrganizationWorldComponent world)
        {
            var counted = new HashSet<string>();
            int hostile = 0;
            List<CAOrganization> neighbors = Neighbors(source, world);
            for (int i = 0; i < neighbors.Count; i++)
            {
                CAOrganization neighbor = neighbors[i];
                if (Between(source, neighbor) != "Hostile") continue;
                Faction faction = FactionOf(neighbor);
                string key = faction != null ? "faction:" + faction.loadID
                    : neighbor.organizationKey;
                if (counted.Add(key)) hostile++;
            }
            return hostile;
        }

        internal static Faction FactionOf(CAOrganization organization)
        {
            if (organization == null) return null;
            if (organization.organizationKey == "player")
                return Faction.OfPlayer;
            const string factionPrefix = "faction:";
            if (organization.organizationKey != null
                && organization.organizationKey.StartsWith(factionPrefix,
                    StringComparison.Ordinal)
                && int.TryParse(organization.organizationKey.Substring(
                    factionPrefix.Length), out int loadId))
                return CARegionalPlanUtility.FactionByLoadId(loadId);
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            if (regional == null) return null;
            for (int i = 0; i < regional.Records.Count; i++)
            {
                CARegionalSettlementRecord record = regional.Records[i];
                if (record.regionalId + "#" + record.slot
                    == organization.organizationKey) return record.faction;
            }
            return null;
        }
    }

    public sealed class CAClaim : IExposable
    {
        public string kind;
        public string label;
        public int area;
        public int mapId = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref area, "area", 0);
            Scribe_Values.Look(ref mapId, "mapId", -1);
        }
    }

    public sealed class CAPolicyRecord : IExposable
    {
        public string key;
        public string value;
        public int adoptedTick;
        // Null means the policy was authored or imposed through ordinary play.
        // Generated policies identify the system that may update or remove
        // them without touching authored policy.
        public string generatedBy;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref adoptedTick, "adoptedTick", 0);
            Scribe_Values.Look(ref generatedBy, "generatedBy");
        }
    }

    // Only policies consumed by current systems appear here. The first value
    // is the default.
    public static class CAPolicyCatalog
    {
        public static readonly string[] Keys =
        {
            "prisoner treatment", "meal rules", "recreation rules",
            "sleep rules", "defense posture", "defense construction",
            "tax rate"
        };

        public static string[] ValuesOf(string key)
        {
            switch (key)
            {
                case "prisoner treatment":
                    return new[] { "individual choice",
                        "forbid execution", "prefer capture" };
                case "meal rules":
                    return new[] { "default", "relaxed", "strict" };
                case "recreation rules":
                    return new[] { "default", "relaxed", "strict" };
                case "sleep rules":
                    return new[] { "default", "relaxed" };
                case "defense posture":
                    return new[] { "standard", "cautious", "tenacious" };
                case "defense construction":
                    return new[] { "manual", "maintain defenses" };
                case "tax rate":
                    return new[] { "none", "light", "standard", "heavy" };
                default: return new[] { "default" };
            }
        }
    }

    public static class CAPolicyLookup
    {
        // Hot path: called from job-giver prefixes on every meal/joy/rest
        // think for every colonist. Cached per pulse - policies change at
        // gatherings, not per frame.
        private static readonly Dictionary<string, string> cache =
            new Dictionary<string, string>();
        private static int cacheBucket = -1;
        private static World cacheWorld;

        public static string Colony(string key)
        {
            int bucket = Find.TickManager != null
                ? Find.TickManager.TicksGame / 2500 : -2;
            if (!ReferenceEquals(cacheWorld, Find.World)
                || bucket != cacheBucket)
            {
                cacheWorld = Find.World;
                cacheBucket = bucket;
                cache.Clear();
                CAOrganization org = CAOrganizationWorldComponent
                    .Current?.ByKey("player");
                if (org != null)
                    for (int i = 0; i < org.policies.Count; i++)
                        cache[org.policies[i].key] =
                            org.policies[i].value;
            }
            string value;
            if (cache.TryGetValue(key, out value)) return value;
            return CAPolicyCatalog.ValuesOf(key)[0];
        }
    }

    // The act-level hook: Notify_MemberTookDamage fires on the event,
    // not per tick - we capture the instigating pawn and their command
    // status for regional settlement factions at the moment of the
    // act. Drafted, explicitly ordered, or on defense assignment = the
    // organization's hand. A mental break = a dissident hand beyond any
    // authorization. Everything else = an individual acting alone.
    [HarmonyPatch(typeof(Faction),
        nameof(Faction.Notify_MemberTookDamage))]
    internal static class Patch_CAHostileActAttribution
    {
        private static void Postfix(Faction __instance, Pawn member,
            DamageInfo dinfo)
        {
            try
            {
                Pawn actor = dinfo.Instigator as Pawn;
                if (actor == null || actor.Faction != Faction.OfPlayer)
                    return;
                if (__instance == null
                    || __instance == Faction.OfPlayer) return;
                CARegionalWorldComponent regional =
                    CARegionalWorldComponent.Current;
                if (regional == null) return;
                bool regionalFaction = false;
                for (int i = 0; i < regional.Records.Count; i++)
                    if (regional.Records[i].faction == __instance)
                    { regionalFaction = true; break; }
                if (!regionalFaction) return;
                CAOrganizationWorldComponent comp =
                    CAOrganizationWorldComponent.Current;
                if (comp == null) return;
                bool authorized = actor.Drafted
                    || CATactical.HasExplicitOrder(actor)
                    || CATactical.IsAutomaticDefense(actor);
                comp.AddHostileAct(new CAHostileActRecord
                {
                    tick = Find.TickManager.TicksGame,
                    pawnId = actor.thingIDNumber,
                    pawnLabel = actor.LabelShort,
                    factionLoadId = __instance.loadID,
                    authorized = authorized,
                    mentalBreak = actor.InAggroMentalState
                });
            }
            catch (Exception) { }
        }
    }

    // Command authority comes from squad or team leadership, or from the
    // colony's Ideoligion leader office.
    public static class CAOrganizationAuthority
    {
        public static bool HasColonyCommand(Pawn p)
        {
            if (p == null) return false;
            CAOrganization org =
                CAOrganizationWorldComponent.Current?.ByKey("player");
            if (org == null) return false;
            for (int i = 0; i < org.offices.Count; i++)
                if (org.offices[i].holderId == p.thingIDNumber
                    && org.offices[i].seniority >= 900)
                    return true;
            return false;
        }
    }

    // Agreement willingness uses recorded conduct and current conditions.
    // Every evaluation returns the factor breakdown shown to the player and
    // written to both organizations' histories.
    //
    //   score = stance + conduct + need + affinity + public support
    //           + terms - cost (+ mediation when routed)
    //   accept >= 0.50; counter 0.35-0.50 (names the closing sweetener);
    //   refuse below. A head under 40% public support cannot bind at all.
    public static class CAWillingness
    {
        public static string Evaluate(CAOrganization proposer,
            CAOrganization target, string kind, string scopeKind,
            int sunsetDays, bool weContribute, string direction,
            bool unauthorizedProtection, out float score, out string breakdown,
            out string counterHint, out CAOrganization broker)
        {
            string proposerKey = proposer.organizationKey;
            broker = null;
            counterHint = null;
            var parts = new StringBuilder();
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            score = 0f;

            // Binding power first: a collapsed head cannot commit anyone.
            if (target.publicSupport < 0.4f)
            {
                breakdown = "their leadership cannot bind the settlement"
                    + " (public support " + target.publicSupport.ToStringPercent()
                    + ")";
                return "refused";
            }

            // Hostile parties can only be reached
            // through a mediator both sides can hear.
            string stance = CAOrganizationStances.Between(proposer, target);
            float f;
            if (stance == "Ally") f = 0.45f;
            else if (stance == "Hostile")
            {
                broker = FindMediator(comp, proposer, target);
                if (broker == null)
                {
                    breakdown = "hostile, and no mediator exists that both"
                        + " sides can hear";
                    return "refused";
                }
                f = -0.10f;
                parts.Append("mediated by " + broker.name + " -0.10");
            }
            else f = 0.15f;
            score += f;
            if (broker == null)
                parts.Append("stance " + stance + " "
                    + f.ToString("+0.00;-0.00"));

            // Recorded conduct with this settlement weighs most. Conduct elsewhere
            // has a smaller effect.
            int kept = 0, completed = 0, brokenN = 0, keptAnywhere = 0;
            if (comp != null)
                for (int i = 0; i < comp.Agreements.Count; i++)
                {
                    CAAgreement c = comp.Agreements[i];
                    if (!c.Involves(proposerKey)) continue;
                    if (c.Active) keptAnywhere++;
                    if (!c.Involves(target.organizationKey)) continue;
                    if (c.Active) kept++;
                    else if (c.broken) brokenN++;
                    else completed++;
                }
            bool proposerIsPlayer = proposerKey == "player";
            int honored = proposerIsPlayer
                ? target.CountKind("warning-honored", 0) : 0;
            int ignored = proposerIsPlayer
                ? target.CountKind("warning-ignored", 0) : 0;
            int shared = proposerIsPlayer
                ? target.CountKind("warning-shared", 0) : 0;
            int imposed = proposerIsPlayer
                ? target.CountKind("policy-imposed", 0) : 0;
            float conduct = 0.06f * Mathf.Min(3, kept)
                + 0.05f * Mathf.Min(3, completed)
                - 0.18f * brokenN
                + 0.02f * Mathf.Min(3, keptAnywhere)
                + 0.04f * Mathf.Min(3, honored)
                + 0.04f * Mathf.Min(3, shared)
                - 0.06f * Mathf.Min(3, ignored)
                - 0.05f * Mathf.Min(3, imposed);
            score += conduct;
            parts.Append(", conduct " + conduct.ToString("+0.00;-0.00")
                + " (with them: " + kept + " kept, " + completed
                + " completed, " + brokenN + " broken; " + honored
                + " warnings honored, " + shared + " shared, " + ignored
                + " ignored, " + imposed + " imposed on them; "
                + keptAnywhere + " kept anywhere)");

            // Hostile neighbors increase demand for protective agreements.
            bool protective = kind == "defense" || kind == "protection"
                || kind == "shared warnings";
            if (protective)
            {
                int hostiles = CAOrganizationStances.HostileNeighborCount(
                    target, comp);
                float anxiety = Mathf.Min(0.30f, hostiles * 0.08f);
                score += anxiety;
                if (anxiety > 0f)
                    parts.Append(", anxiety +" + anxiety.ToString("0.00")
                        + " (" + hostiles + " hostile neighbors)");
            }

            // A shared Ideoligion increases acceptance.
            Ideo proposerIdeo = IdeoOf(proposer);
            if (proposerIdeo != null && IdeoOf(target) == proposerIdeo)
            {
                score += 0.06f;
                parts.Append(", shared Ideoligion +0.06");
            }

            // Current public support affects whether leadership can bind the
            // settlement.
            float supportScore = (target.publicSupport - 0.7f) * 0.2f;
            score += supportScore;
            parts.Append(", their public support "
                + supportScore.ToString("+0.00;-0.00"));

            // A settlement signing without its protector's consent is less
            // likely to be trusted as a valid counterparty.
            if (unauthorizedProtection)
            {
                score -= 0.15f;
                parts.Append(", doubts your standing to sign -0.15");
            }

            // Narrow scope, a short term, and offered contributions make an
            // agreement easier to accept.
            float terms = 0f;
            if (scopeKind != "general") terms += 0.05f;
            if (sunsetDays > 0 && sunsetDays <= 15) terms += 0.05f;
            if (weContribute) terms += 0.05f;
            score += terms;
            if (terms > 0f)
                parts.Append(", terms +" + terms.ToString("0.00"));

            // Protection limits one party's independence and obliges the other
            // to provide aid. A weak settlement may refuse to protect.
            float cost;
            if (kind == "protection")
            {
                if (direction == "they-protect")
                {
                    cost = 0.05f;
                    int fort, train;
                    CASettlementSecurityFacts.Read(target.organizationKey,
                        out fort,
                        out train);
                    if (fort + train >= 5)
                    {
                        score += 0.10f;
                        parts.Append(", strength to grant protection"
                            + " +0.10");
                    }
                    else
                    {
                        score -= 0.10f;
                        parts.Append(", cannot afford to protect -0.10");
                    }
                }
                else cost = 0.30f;
            }
            else if (kind == "tribute")
                cost = direction == "we-pay" ? 0.02f : 0.12f;
            else cost = kind == "defense" ? 0.10f
                : kind == "military access" ? 0.08f
                : kind == "non-aggression" ? 0.03f
                : kind == "shared warnings" ? 0.02f : 0f;
            score -= cost;
            if (cost > 0f)
                parts.Append(", agreement burden -" + cost.ToString("0.00"));

            breakdown = parts.ToString();
            if (score >= 0.50f) return "accepted";
            if (score >= 0.35f)
            {
                if (!weContribute)
                    counterHint = "an offered contribution would close it";
                else if (scopeKind == "general")
                    counterHint = "a narrower scope would close it";
                else if (sunsetDays <= 0 || sunsetDays > 15)
                    counterHint = "a bounded sunset would close it";
                else counterHint = "a lesser kind might be accepted";
                return "counter";
            }
            return "refused";
        }

        // A party on speaking terms with both sides can mediate.
        private static CAOrganization FindMediator(
            CAOrganizationWorldComponent comp, CAOrganization colony,
            CAOrganization target)
        {
            if (comp == null) return null;
            List<CAOrganization> nearby = CAOrganizationStances.Neighbors(
                colony, comp);
            for (int i = 0; i < nearby.Count; i++)
            {
                CAOrganization m = nearby[i];
                if (m == colony || m == target || !m.IsSettlement
                    || CAOrganizationStances.FactionOf(m) == null) continue;
                string toUs = CAOrganizationStances.Between(colony, m);
                if (toUs == "Hostile") continue;
                string toThem = CAOrganizationStances.Between(target, m);
                if (toThem == "Hostile") continue;
                return m;
            }
            return null;
        }

        private static Ideo IdeoOf(CAOrganization organization)
        {
            try
            {
                return CAOrganizationStances.FactionOf(organization)
                    ?.ideos?.PrimaryIdeo;
            }
            catch { return null; }
        }
    }

    // Customs the player may establish directly. Other customs come from
    // current social order, experience, and training.
    public static class CACustomCatalog
    {
        public static readonly string[] Adoptable =
        {
            "formation", "line", "ambush", "status reporting"
        };
    }

    public enum CAOrganizationKind
    {
        Colony,
        Settlement,
        Faction,
        Federation,
        Household,
        Group
    }

    public sealed class CAOrganization : IExposable
    {
        public string organizationKey;
        public CAOrganizationKind organizationKind =
            CAOrganizationKind.Settlement;
        public string name;
        public float publicSupport = 1f;
        public string standingNote;
        public List<CAOffice> offices = new List<CAOffice>();
        public List<CAOrganizationGroup> groups =
            new List<CAOrganizationGroup>();
        public List<CAOrganizationCustom> customs =
            new List<CAOrganizationCustom>();
        public List<CASecurityPractice> securityPractices =
            new List<CASecurityPractice>();
        public List<CAClaim> claims = new List<CAClaim>();
        public List<CAPolicyRecord> policies = new List<CAPolicyRecord>();
        public List<CADecisionEntry> decisionHistory =
            new List<CADecisionEntry>();
        public int lastPopulation = -1;
        public int lastWarningTick = -999999;
        public int lastImproveTick = -999999;
        // A warning opens a response deadline. The outcome is recorded as
        // honored, ignored, or a broken agreement.
        public int pendingAidDeadline = -1;
        public int lastWarnedByPlayerTick = -999999;
        // Production adds to the treasury. Obligations spend it. Settlements
        // with insufficient funds pay only what they can afford.
        public float treasury;
        public int lastIncomeTick = -1;
        public int lastAppraisalTick = -1;
        // Frontier holdings keep their own residents and identity when they
        // affiliate with the colony.
        public List<int> memberPawnIds = new List<int>();
        public bool affiliatedWithPlayer;
        // Conflicts remain open until current practice matches the faction's
        // political beliefs. Parallel lists store each key and start tick.
        public List<string> openBeliefConflicts = new List<string>();
        public List<int> beliefConflictStartTicks = new List<int>();
        public int lastBeliefCheckTick = -1;
        // Outstanding public support loss from unresolved belief conflicts.
        public float unrecoveredSupportLoss;

        // Each federation membership records what that member delegated.
        public int sunsetTick = -1;

        public bool IsFederation
        {
            get { return organizationKind == CAOrganizationKind.Federation; }
        }

        public bool IsFactionOrganization
        {
            get { return organizationKind == CAOrganizationKind.Faction; }
        }

        public bool IsSettlement
        {
            get { return organizationKind == CAOrganizationKind.Settlement; }
        }

        public List<string> MemberKeys
        {
            get { return CAFederation.MemberKeys(this); }
        }

        public List<string> SharedResponsibilities
        {
            get { return CAFederation.SharedResponsibilities(this); }
        }

        public List<string> SettlementMemberKeys
        {
            get { return CASettlementAuthorityMembership.MemberKeys(this); }
        }

        public List<string> SharedSettlementResponsibilities
        {
            get
            {
                return CASettlementAuthorityMembership
                    .SharedResponsibilities(this);
            }
        }

        public bool HasCustom(string key)
        {
            for (int i = 0; i < customs.Count; i++)
                if (customs[i].key == key) return true;
            return false;
        }

        public void Record(string text)
        {
            Record("note", text);
        }

        public void Record(string kind, string text)
        {
            decisionHistory.Add(new CADecisionEntry
            {
                tick = Find.TickManager.TicksGame,
                kind = kind,
                text = text
            });
            if (decisionHistory.Count > 200)
                decisionHistory.RemoveAt(0);
        }

        // Count recorded decisions of one kind within a time span.
        public int CountKind(string kind, int sinceTick)
        {
            int n = 0;
            for (int i = 0; i < decisionHistory.Count; i++)
                if (decisionHistory[i].kind == kind
                    && decisionHistory[i].tick >= sinceTick) n++;
            return n;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref organizationKey, "organizationKey");
            Scribe_Values.Look(ref organizationKind, "organizationKind",
                CAOrganizationKind.Settlement);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref publicSupport, "publicSupport", 1f);
            Scribe_Values.Look(ref standingNote, "standingNote");
            Scribe_Collections.Look(ref offices, "offices", LookMode.Deep);
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
            Scribe_Collections.Look(ref customs, "customs",
                LookMode.Deep);
            Scribe_Collections.Look(ref securityPractices,
                "securityPractices", LookMode.Deep);
            Scribe_Collections.Look(ref claims, "claims", LookMode.Deep);
            Scribe_Collections.Look(ref policies, "policies", LookMode.Deep);
            Scribe_Collections.Look(ref decisionHistory, "decisionHistory",
                LookMode.Deep);
            Scribe_Values.Look(ref lastPopulation, "lastPopulation", -1);
            Scribe_Values.Look(ref lastWarningTick, "lastWarningTick",
                -999999);
            Scribe_Values.Look(ref lastImproveTick, "lastImproveTick",
                -999999);
            Scribe_Values.Look(ref pendingAidDeadline, "pendingAidDeadline",
                -1);
            Scribe_Values.Look(ref lastWarnedByPlayerTick,
                "lastWarnedByPlayerTick", -999999);
            Scribe_Values.Look(ref treasury, "treasury", 0f);
            Scribe_Values.Look(ref lastIncomeTick, "lastIncomeTick", -1);
            Scribe_Values.Look(ref lastAppraisalTick, "lastAppraisalTick",
                -1);
            Scribe_Collections.Look(ref memberPawnIds, "memberPawnIds",
                LookMode.Value);
            Scribe_Collections.Look(ref openBeliefConflicts,
                "openBeliefConflicts", LookMode.Value);
            Scribe_Collections.Look(ref beliefConflictStartTicks,
                "beliefConflictStartTicks", LookMode.Value);
            Scribe_Values.Look(ref lastBeliefCheckTick,
                "lastBeliefCheckTick", -1);
            Scribe_Values.Look(ref unrecoveredSupportLoss,
                "unrecoveredSupportLoss", 0f);
            Scribe_Values.Look(ref affiliatedWithPlayer,
                "affiliatedWithPlayer", false);
            Scribe_Values.Look(ref sunsetTick, "sunsetTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (offices == null) offices = new List<CAOffice>();
                if (groups == null)
                    groups = new List<CAOrganizationGroup>();
                if (customs == null)
                    customs = new List<CAOrganizationCustom>();
                if (securityPractices == null)
                    securityPractices = new List<CASecurityPractice>();
                if (claims == null) claims = new List<CAClaim>();
                if (policies == null) policies = new List<CAPolicyRecord>();
                if (decisionHistory == null)
                    decisionHistory = new List<CADecisionEntry>();
                if (memberPawnIds == null)
                    memberPawnIds = new List<int>();
                if (openBeliefConflicts == null)
                    openBeliefConflicts = new List<string>();
                if (beliefConflictStartTicks == null)
                    beliefConflictStartTicks = new List<int>();
            }
        }
    }

    // Writes one typed organization-membership relation. The role Def keeps
    // settlement authority and federation membership behaviorally separate.
    internal static class CAOrganizationMembership
    {
        internal static CARelation Upsert(CAOrganization parent,
            string memberKey, IEnumerable<string> delegatedResponsibilities,
            int sunsetTick, CAOrigin origin, CAOrigin termsOrigin,
            CARelationRoleDef roleDef, string roleWords)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null || parent == null || memberKey.NullOrEmpty()
                || roleDef == null) return null;

            var wanted = new List<string>();
            if (delegatedResponsibilities != null)
                foreach (string responsibility in delegatedResponsibilities)
                    if (!responsibility.NullOrEmpty()
                        && !wanted.Contains(responsibility))
                        wanted.Add(responsibility);

            CARelation existing = ledger.OrganizationMembership(
                parent.organizationKey, memberKey, roleDef);
            if (existing != null)
            {
                if (SameTerms(existing, wanted, sunsetTick))
                {
                    existing.origin = Stronger(existing.origin, origin);
                    existing.termsOrigin = Stronger(existing.termsOrigin,
                        termsOrigin);
                    return existing;
                }
                if (!existing.termsOrigin.Replaceable
                    && termsOrigin.source != CAProvenance.Authored)
                {
                    Log.Warning("[CA][Organization] refused to change "
                        + roleWords + " for " + memberKey + " in "
                        + parent.organizationKey + ": the existing relation is "
                        + existing.termsOrigin.source
                        + " and authored terms are not overwritten. Existing "
                        + "responsibilities ["
                        + string.Join(", ", existing
                            .delegatedResponsibilities.ToArray())
                        + "], requested ["
                        + string.Join(", ", wanted.ToArray()) + "]");
                    return null;
                }
                existing.delegatedResponsibilities.Clear();
                existing.delegatedResponsibilities.AddRange(wanted);
                existing.sunsetTick = sunsetTick;
                existing.origin = Stronger(existing.origin, origin);
                existing.termsOrigin = Stronger(existing.termsOrigin,
                    termsOrigin);
                return existing;
            }

            var relation = new CARelation
            {
                orgKey = parent.organizationKey,
                roleDef = roleDef,
                role = roleWords,
                sunsetTick = sunsetTick,
                startTick = Find.TickManager?.TicksGame ?? 0,
                origin = origin,
                termsOrigin = termsOrigin
            };
            relation.Party = CARelationPartyRef.OfOrganization(memberKey);
            relation.delegatedResponsibilities.AddRange(wanted);
            return ledger.Add(relation) ? relation : null;
        }

        private static bool SameTerms(CARelation relation,
            List<string> wanted, int sunsetTick)
        {
            if (relation.sunsetTick != sunsetTick) return false;
            if (relation.delegatedResponsibilities.Count != wanted.Count)
                return false;
            for (int i = 0; i < wanted.Count; i++)
                if (!relation.delegatedResponsibilities.Contains(wanted[i]))
                    return false;
            return true;
        }

        private static CAOrigin Stronger(CAOrigin current,
            CAOrigin incoming)
        {
            if (current.source == CAProvenance.Authored
                && incoming.source != CAProvenance.Authored) return current;
            if (incoming.source == CAProvenance.Authored) return incoming;
            return incoming.originKey.NullOrEmpty() ? current : incoming;
        }
    }

    internal static class CAFederation
    {
        internal static List<string> MemberKeys(CAOrganization federation)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            return ledger == null || federation == null
                ? new List<string>()
                : ledger.FederationMemberKeys(federation.organizationKey);
        }

        internal static CARelation Admit(CAOrganization federation,
            string memberKey, IEnumerable<string> delegatedResponsibilities,
            int sunsetTick, CAOrigin origin)
        {
            return CAOrganizationMembership.Upsert(federation, memberKey,
                delegatedResponsibilities, sunsetTick, origin, origin,
                CARelationRoleDefOf.CA_Role_FederationMember,
                "federation member");
        }

        internal static List<string> SharedResponsibilities(
            CAOrganization federation)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            return ledger == null || federation == null
                ? new List<string>()
                : ledger.FederationSharedResponsibilities(
                    federation.organizationKey);
        }
    }

    internal static class CASettlementAuthorityMembership
    {
        internal static CARelation Admit(CAOrganization factionOrganization,
            string settlementKey,
            IEnumerable<string> delegatedResponsibilities,
            CAOrigin membershipOrigin, CAOrigin termsOrigin)
        {
            return CAOrganizationMembership.Upsert(factionOrganization,
                settlementKey, delegatedResponsibilities, -1,
                membershipOrigin, termsOrigin,
                CARelationRoleDefOf.CA_Role_SettlementMember,
                "settlement member");
        }

        internal static List<string> MemberKeys(
            CAOrganization factionOrganization)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            return ledger == null || factionOrganization == null
                ? new List<string>()
                : ledger.SettlementMemberKeys(
                    factionOrganization.organizationKey);
        }

        internal static List<string> SharedResponsibilities(
            CAOrganization factionOrganization)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            return ledger == null || factionOrganization == null
                ? new List<string>()
                : ledger.SettlementSharedResponsibilities(
                    factionOrganization.organizationKey);
        }
    }

    public sealed class CAOrganizationWorldComponent : WorldComponent
    {
        private int authoringDataEpoch = CAAuthoringDataEpoch.Current;
        private List<CAOrganization> organizations = new List<CAOrganization>();
        private List<CAFrontierMapPlan> frontierMapPlans =
            new List<CAFrontierMapPlan>();
        private List<CAAgreement> agreements = new List<CAAgreement>();
        private int nextAgreementId = 1;
        private int lastSyncTick = -99999;

        public IReadOnlyList<CAAgreement> Agreements
        {
            get { return agreements; }
        }

        public List<CAAgreement> ActiveAgreementsInvolving(string key)
        {
            var result = new List<CAAgreement>();
            for (int i = 0; i < agreements.Count; i++)
                if (agreements[i].Active && agreements[i].Involves(key))
                    result.Add(agreements[i]);
            return result;
        }

        // [roads] Any live tie at all - a road follows a relationship,
        // whatever kind of relationship it happens to be.
        public bool HasAnyActiveAgreement(string keyA, string keyB)
        {
            for (int i = 0; i < agreements.Count; i++)
            {
                CAAgreement c = agreements[i];
                if (c.Active && c.Involves(keyA) && c.Involves(keyB))
                    return true;
            }
            return false;
        }

        public bool HasActiveAgreement(string keyA, string keyB,
            params string[] kinds)
        {
            for (int i = 0; i < agreements.Count; i++)
            {
                CAAgreement c = agreements[i];
                if (!c.Active || !c.Involves(keyA) || !c.Involves(keyB))
                    continue;
                for (int k = 0; k < kinds.Length; k++)
                    if (c.kind == kinds[k]) return true;
            }
            return false;
        }

        public CAAgreement AddAgreement(CAAgreement agreement)
        {
            agreement.id = nextAgreementId++;
            agreement.sinceTick = Find.TickManager.TicksGame;
            agreements.Add(agreement);
            return agreement;
        }

        private List<CABreachCase> breachCases = new List<CABreachCase>();
        private int nextCaseId = 1;
        private List<CAHostileActRecord> hostileActs =
            new List<CAHostileActRecord>();
        private List<CAAgreementOffer> offers = new List<CAAgreementOffer>();
        private List<CAPendingGathering> pendingGatherings =
            new List<CAPendingGathering>();

        public void StageGathering(CAPendingGathering g)
        {
            for (int i = 0; i < pendingGatherings.Count; i++)
                if (pendingGatherings[i].key == g.key)
                { pendingGatherings[i] = g; return; }
            pendingGatherings.Add(g);
        }

        private void TickGatherings(int now)
        {
            for (int i = pendingGatherings.Count - 1; i >= 0; i--)
            {
                CAPendingGathering g = pendingGatherings[i];
                Map map = null;
                List<Map> maps = Find.Maps;
                for (int m = 0; m < maps.Count; m++)
                    if (maps[m].uniqueID == g.mapId)
                    { map = maps[m]; break; }
                if (map == null || now - g.startTick > 30000)
                {
                    pendingGatherings.RemoveAt(i);
                    continue;
                }
                Pawn speaker = null;
                var cols = map.mapPawns.FreeColonistsSpawned;
                for (int c = 0; c < cols.Count; c++)
                    if (cols[c].thingIDNumber == g.speakerId)
                    { speaker = cols[c]; break; }
                if (speaker == null || speaker.Downed)
                {
                    pendingGatherings.RemoveAt(i);
                    continue;
                }
                if (!speaker.Position.InHorDistOf(g.spot, 4f)) continue;
                string why;
                if (CAGathering.CanConvene(speaker, g.spot, map, out why))
                    CAGathering.Convene(speaker, g.spot, map, g.key,
                        g.value);
                else
                    Messages.Message("The gathering dissolved - " + why
                        + ". The law was not spoken.",
                        MessageTypeDefOf.NeutralEvent, false);
                pendingGatherings.RemoveAt(i);
            }
        }
        private int nextOfferId = 1;
        private int lastInitiativeTick = -999999;

        public IReadOnlyList<CAAgreementOffer> Offers
        {
            get { return offers; }
        }

        public void RemoveOffer(CAAgreementOffer o) { offers.Remove(o); }

        public void AddHostileAct(CAHostileActRecord act)
        {
            hostileActs.Add(act);
            if (hostileActs.Count > 24) hostileActs.RemoveAt(0);
        }

        public CAHostileActRecord RecentActAgainst(int factionLoadId,
            int withinTicks)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = hostileActs.Count - 1; i >= 0; i--)
                if (hostileActs[i].factionLoadId == factionLoadId
                    && now - hostileActs[i].tick <= withinTicks)
                    return hostileActs[i];
            return null;
        }

        public IReadOnlyList<CABreachCase> BreachCases
        {
            get { return breachCases; }
        }

        public CABreachCase OpenBreachCase(string obligation,
            string obligedKey, string claimantKey, int deadlineTick,
            int agreementId)
        {
            var c = new CABreachCase
            {
                id = nextCaseId++,
                agreementId = agreementId,
                obligation = obligation,
                obligedKey = obligedKey,
                claimantKey = claimantKey,
                openedTick = Find.TickManager.TicksGame,
                deadlineTick = deadlineTick
            };
            breachCases.Add(c);
            if (breachCases.Count > 60) breachCases.RemoveAt(0);
            return c;
        }

        public CABreachCase OpenCaseFor(string claimantKey)
        {
            for (int i = 0; i < breachCases.Count; i++)
                if (breachCases[i].Open
                    && breachCases[i].claimantKey == claimantKey)
                    return breachCases[i];
            return null;
        }

        public List<CABreachCase> CasesInvolving(string key)
        {
            var result = new List<CABreachCase>();
            for (int i = 0; i < breachCases.Count; i++)
                if (breachCases[i].obligedKey == key
                    || breachCases[i].claimantKey == key)
                    result.Add(breachCases[i]);
            return result;
        }

        public CAOrganizationWorldComponent(World world) : base(world) { }

        public static CAOrganizationWorldComponent Current
        {
            get
            {
                return Verse.Find.World
                    ?.GetComponent<CAOrganizationWorldComponent>();
            }
        }

        public IReadOnlyList<CAOrganization> Organizations
        {
            get { return organizations; }
        }

        internal CAFrontierMapPlan EnsureFrontierMapPlan(Map map)
        {
            if (map == null) return null;
            CARegionalPlan regional = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            if (regional != null) return null;

            int tileId = map.Tile.Valid ? map.Tile.tileId : -1;
            CAFrontierMapPlan existing = frontierMapPlans.FirstOrDefault(item =>
                item != null && item.mapId == map.uniqueID);
            if (ValidFrontierMapPlan(existing, map, tileId))
                return existing;
            if (existing != null) frontierMapPlans.Remove(existing);

            CARegionalWorldPolicy policy = CARegionalWorldComponent.Current
                ?.WorldPolicy ?? new CARegionalWorldPolicy();
            int worldSeed = 0;
            try { worldSeed = Verse.Find.World.info.Seed; }
            catch { }
            int seed = Gen.HashCombineInt(worldSeed, tileId,
                map.Size.x, map.Size.z);
            int suitableCapacity = Mathf.Clamp(
                map.Size.x * map.Size.z / 40000, 2, 8);
            int count = CAWorldTendencyCausalKernel.FrontierHoldingCount(
                suitableCapacity, policy.frontierHoldingFrequency);
            int landCapacity = FrontierLandCapacity(map);
            var created = new CAFrontierMapPlan
            {
                mapId = map.uniqueID,
                mapTileId = tileId,
                mapWidth = map.Size.x,
                mapHeight = map.Size.z,
                realizationSourceHash = CAWorldTendencyCausalKernel
                    .HashCombineInt(seed,
                        Mathf.RoundToInt(
                            policy.frontierHoldingFrequency * 10000f),
                        Mathf.RoundToInt(
                            policy.frontierHoldingSize * 10000f),
                        suitableCapacity)
            };
            for (int i = 0; i < count; i++)
            {
                int residents = CAWorldTendencyCausalKernel
                    .FrontierResidentCount(landCapacity,
                        policy.frontierHoldingSize);
                int material = CAWorldTendencyCausalKernel
                    .FrontierMaterialLevel(landCapacity,
                        policy.frontierHoldingSize);
                created.holdings.Add(new CAFrontierHoldingPlan
                {
                    key = i,
                    memberTileId = tileId,
                    residentCount = residents,
                    landCapacity = landCapacity,
                    materialLevel = material,
                    form = CAWorldTendencyCausalKernel.FrontierForm(
                        residents, material),
                    factionless = true
                });
            }
            frontierMapPlans.Add(created);
            return created;
        }

        private static bool ValidFrontierMapPlan(CAFrontierMapPlan plan,
            Map map, int tileId)
        {
            if (plan == null || plan.mapTileId != tileId
                || plan.mapWidth != map.Size.x
                || plan.mapHeight != map.Size.z
                || plan.holdings == null || plan.holdings.Count > 8)
                return false;

            var keys = new HashSet<int>();
            for (int i = 0; i < plan.holdings.Count; i++)
            {
                CAFrontierHoldingPlan holding = plan.holdings[i];
                if (holding == null || holding.memberTileId != tileId
                    || !keys.Add(holding.key)
                    || holding.landCapacity < 1
                    || holding.landCapacity > 3
                    || holding.residentCount < 1
                    || holding.residentCount > 6
                    || holding.materialLevel < 0
                    || holding.materialLevel > holding.landCapacity
                    || holding.form != CAWorldTendencyCausalKernel
                        .FrontierForm(holding.residentCount,
                            holding.materialLevel))
                    return false;
            }
            return true;
        }

        private static int FrontierLandCapacity(Map map)
        {
            Hilliness hilliness = map.TileInfo?.hilliness ?? Hilliness.Flat;
            switch (hilliness)
            {
                case Hilliness.Impassable:
                case Hilliness.Mountainous:
                    return 1;
                case Hilliness.LargeHills:
                    return 2;
                default:
                    return 3;
            }
        }

        public CAOrganization EnsureColony()
        {
            for (int i = 0; i < organizations.Count; i++)
                if (organizations[i].organizationKey == "player")
                {
                    organizations[i].organizationKind =
                        CAOrganizationKind.Colony;
                    return organizations[i];
                }
            CAOrganization org = new CAOrganization
            {
                organizationKey = "player",
                organizationKind = CAOrganizationKind.Colony,
                name = Faction.OfPlayer != null
                    ? Faction.OfPlayer.Name : "colony",
                standingNote = "Player colony. Colonists follow direct player"
                    + " orders."
            };
            org.Record("organization record established - inherited from"
                + " the colony's standing structure");
            organizations.Add(org);
            CAOrganizationInheritance.SyncColony(org);
            return org;
        }

        public CAOrganization ByKey(string key)
        {
            for (int i = 0; i < organizations.Count; i++)
                if (organizations[i].organizationKey == key)
                    return organizations[i];
            return null;
        }

        public CAOrganization EnsureFor(string key, string name,
            string standingNote, CAOrganizationKind organizationKind)
        {
            CAOrganization org = ByKey(key);
            if (org != null)
            {
                org.organizationKind = organizationKind;
                return org;
            }
            org = new CAOrganization
            {
                organizationKey = key,
                organizationKind = organizationKind,
                name = name,
                standingNote = standingNote
            };
            organizations.Add(org);
            return org;
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            int now = Find.TickManager.TicksGame;
            if (now - lastSyncTick < 2500) return;
            lastSyncTick = now;
            CAOrganization colony = EnsureColony();
            CAOrganizationInheritance.SyncColony(colony);
            CAOrganizationInheritance.ColonyPulse(colony);
            CAOrganizationInheritance.SyncRegionalSettlements(this);
            CAFrontier.EnsureHoldings(this);
            ProcessAgreementsAndFederations(now);
            TickGatherings(now);
            PulsePoliticalBeliefsUnderBudget(now);
        }

        // Player-facing and loaded organizations update every world pulse.
        // Off-map organizations share the saved activity budget and cursor.
        private int offMapActivityCursor;

        private void PulsePoliticalBeliefsUnderBudget(int now)
        {
            float offMapActivityRate = CARegionalWorldComponent.Current
                ?.WorldPolicy?.offMapActivityRate ?? 0.5f;
            var background = new List<CAOrganization>();
            for (int i = 0; i < organizations.Count; i++)
            {
                CAOrganization org = organizations[i];
                if (org == null) continue;
                if (AlwaysLive(org))
                    CAPoliticalBeliefEffects.Pulse(org, now);
                else background.Add(org);
            }
            if (background.Count == 0) return;
            int allowance = CAWorldTendencyCausalKernel
                .OffMapActivityBudget(background.Count, offMapActivityRate);
            for (int n = 0; n < allowance; n++)
            {
                CAOrganization org = background[
                    offMapActivityCursor++ % background.Count];
                CAPoliticalBeliefEffects.Pulse(org, now);
            }
        }

        private bool AlwaysLive(CAOrganization org)
        {
            if (org.organizationKey == "player"
                || org.affiliatedWithPlayer) return true;
            if (org.pendingAidDeadline > 0) return true;
            try
            {
                var regional = CARegionalWorldComponent.Current;
                if (regional != null)
                    foreach (Map map in Find.Maps)
                        foreach (CARegionalSettlementRecord r in
                            regional.ForMap(map))
                            if (r != null && r.regionalId + "#" + r.slot
                                == org.organizationKey) return true;
            }
            catch { }
            return false;
        }

        // End timed agreements and update federation seats.
        private void ProcessAgreementsAndFederations(int now)
        {
            ProcessTributeObligations(now);
            for (int i = 0; i < agreements.Count; i++)
            {
                CAAgreement c = agreements[i];
                if (!c.Active || c.sunsetTick < 0 || now < c.sunsetTick)
                    continue;
                c.dissolvedTick = now;
                CAOrganization a = ByKey(c.partyA);
                CAOrganization b = ByKey(c.partyB);
                if (a != null) a.Record("agreement-expired",
                    "agreement expired - " + c.kind + " with "
                    + (b != null ? b.name : c.partyB));
                if (b != null) b.Record("agreement-expired",
                    "agreement expired - " + c.kind + " with "
                    + (a != null ? a.name : c.partyA));
            }
            for (int i = organizations.Count - 1; i >= 0; i--)
            {
                CAOrganization fed = organizations[i];
                if (!fed.IsFederation) continue;
                // Federation membership is read from current relations.
                List<string> federationMembers = CAFederation.MemberKeys(fed);
                if (fed.sunsetTick > 0 && now >= fed.sunsetTick)
                {
                    for (int m = 0; m < federationMembers.Count; m++)
                    {
                        CAOrganization member = ByKey(federationMembers[m]);
                        if (member != null)
                            member.Record("agreement-expired", "federation dissolved"
                                + " at sunset - " + fed.name);
                    }
                    organizations.RemoveAt(i);
                    continue;
                }
                for (int m = 0; m < federationMembers.Count; m++)
                {
                    CAOrganization member = ByKey(federationMembers[m]);
                    if (member == null) continue;
                    string seatKey = "seat-" + member.organizationKey;
                    CAOffice seat = null;
                    for (int o = 0; o < fed.offices.Count; o++)
                        if (fed.offices[o].sourceKey == seatKey)
                        { seat = fed.offices[o]; break; }
                    CAOffice head = member.offices.Count > 0
                        ? member.offices[0] : null;
                    if (seat == null)
                    {
                        fed.offices.Add(new CAOffice
                        {
                            sourceKey = seatKey,
                            name = member.name + " seat",
                            seniority = 500,
                            holderId = head != null ? head.holderId : -1,
                            holderLabel = head != null
                                ? head.holderLabel : null,
                            grants = "one voice among the members"
                        });
                    }
                    else if (head != null)
                    {
                        seat.holderId = head.holderId;
                        seat.holderLabel = head.holderLabel;
                    }
                }
            }
        }

        // Each settlement periodically considers an agreement with the
        // player. The recorded reason follows its security and resources.
        private void RunDiplomaticAppraisals(int now)
        {
            if (now - lastInitiativeTick < 300000) return;
            CAOrganization colony = EnsureColony();
            for (int i = 0; i < organizations.Count; i++)
            {
                CAOrganization org = organizations[i];
                if (org.organizationKey == "player" || org.IsFederation
                    || org.IsFactionOrganization)
                    continue;
                if (org.lastPopulation == 0) continue;
                if (org.lastAppraisalTick < 0)
                {
                    org.lastAppraisalTick = now
                        + Math.Abs(org.organizationKey.GetHashCode()) % 60000;
                    continue;
                }
                if (now - org.lastAppraisalTick < 900000) continue;
                org.lastAppraisalTick = now;
                string toPlayer = CAOrganizationStances.Between(org, colony);
                if (toPlayer == "Hostile") continue;
                bool alreadyBound = HasActiveAgreement("player",
                    org.organizationKey, "protection");
                for (int j = 0; j < offers.Count; j++)
                    if (offers[j].fromKey == org.organizationKey)
                    { alreadyBound = true; break; }
                if (alreadyBound) continue;

                int hostiles = CAOrganizationStances.HostileNeighborCount(
                    org, this);
                int fort = 0, train = 0;
                CARegionalWorldComponent regional =
                    CARegionalWorldComponent.Current;
                if (regional != null)
                    for (int r = 0; r < regional.Records.Count; r++)
                        if (regional.Records[r].regionalId + "#"
                                + regional.Records[r].slot
                            == org.organizationKey)
                        {
                            CASettlementSecurityFacts.Read(
                                org.organizationKey, out fort, out train);
                            break;
                        }

                string kind = null, reason = null, protector = null;
                float fear = hostiles * 0.3f + (fort <= 2 ? 0.3f : 0f);
                bool prudence = org.CountKind("warning-honored", 0)
                        + org.CountKind("warning-shared", 0) >= 1
                    && !HasActiveAgreement("player", org.organizationKey,
                        "shared warnings", "defense", "protection");
                if (fear >= 0.9f)
                {
                    kind = "protection";
                    protector = "player";
                    reason = "fear - " + hostiles + " hostile neighbors"
                        + " against fortification " + fort
                        + "; they ask you to protect them";
                }
                else if (fear >= 0.6f)
                {
                    kind = "defense";
                    reason = "fear - " + hostiles + " hostile neighbors"
                        + " against fortification " + fort;
                }
                else if (prudence)
                {
                    kind = "shared warnings";
                    reason = "prudence - warnings between you have been"
                        + " honored; they propose a shared warnings"
                        + " agreement";
                }
                else if (fort + train >= 5 && org.treasury >= 400f
                    && !HasActiveAgreement("player", org.organizationKey,
                        "protection"))
                {
                    kind = "protection";
                    protector = org.organizationKey;
                    reason = "ambition - strength "
                        + (fort + train) + " and a treasury of "
                        + (int)org.treasury
                        + "; they offer to protect you in return for tribute"
                        + " and approval of outside agreements";
                }
                if (kind == null) continue;

                lastInitiativeTick = now;
                var offer = new CAAgreementOffer
                {
                    id = nextOfferId++,
                    fromKey = org.organizationKey,
                    kind = kind,
                    protectorKey = protector,
                    scopeKind = "general",
                    sunsetDays = kind == "defense" ? 15 : 0,
                    reason = reason,
                    createdTick = now
                };
                offers.Add(offer);
                org.Record("relations", "proposed a " + kind
                    + " agreement to " + colony.name + " - " + reason);
                colony.Record("relations", org.name + " proposes a "
                    + kind + " agreement - " + reason);
                Messages.Message(org.name + " proposes a " + kind
                    + " agreement - review it in the organization tab.",
                    MessageTypeDefOf.NeutralEvent, false);
                break;
            }
            for (int i = offers.Count - 1; i >= 0; i--)
                if (now - offers[i].createdTick > 900000)
                {
                    CAOrganization from = ByKey(offers[i].fromKey);
                    from?.Record("relations", "our " + offers[i].kind
                        + " offer to the colony lapsed unanswered");
                    offers.RemoveAt(i);
                }
        }

        private void ProcessTributeObligations(int now)
        {
            RunDiplomaticAppraisals(now);
            CAOrganization colony = EnsureColony();
            // Treasuries change only through represented transactions and
            // adopted collection policy. A capability summary never mints
            // money. Organizations with no rate or taxable parties collect
            // nothing.
            for (int i = 0; i < organizations.Count; i++)
            {
                CAOrganization o = organizations[i];
                if (o.lastIncomeTick < 0)
                {
                    o.lastIncomeTick = now;
                    continue;
                }
                if (now - o.lastIncomeTick < 900000) continue;
                o.lastIncomeTick = now;
                if (CATaxation.RateOf(o) <= 0f) continue;
                CATaxation.Collect(o, this, now);
            }
            for (int i = 0; i < agreements.Count; i++)
            {
                CAAgreement agreement = agreements[i];
                if (!agreement.Active || !agreement.Involves("player")) continue;
                if (agreement.kind != "tribute"
                    && agreement.kind != "protection")
                    continue;
                string other = agreement.OtherParty("player");
                CABreachCase open = null;
                CABreachCase lastClosed = null;
                for (int k = 0; k < breachCases.Count; k++)
                {
                    CABreachCase bc = breachCases[k];
                    if (bc.kind != "tribute"
                        || bc.agreementId != agreement.id)
                        continue;
                    if (bc.Open) open = bc;
                    else lastClosed = bc;
                }
                Faction fac = FactionOfKey(other);
                if (fac == null) continue;

                // Protection has two payment directions: the protected
                // settlement owes tribute and the protector owes support.
                // Player payments use the trade ledger; NPC payments arrive
                // as physical deliveries.
                bool playerIsProtector = agreement.kind == "protection"
                    && agreement.protectorKey == "player";
                bool playerPays = agreement.kind == "protection"
                    || agreement.payerKey == null
                    || agreement.payerKey == "player";
                NpcPaymentFlow(agreement, other, now, colony);
                if (!playerPays) continue;
                string obligationLabel = playerIsProtector
                    ? "deliver support payment" : "deliver tribute";

                if (open == null)
                {
                    int lastDue = lastClosed != null
                        ? lastClosed.openedTick : agreement.sinceTick;
                    if (now - lastDue < 900000) continue;
                    CABreachCase c = OpenBreachCase(obligationLabel,
                        "player", other, now + 300000, agreement.id);
                    c.kind = "tribute";
                    try { c.baselineGoodwill = fac.PlayerGoodwill; }
                    catch { }
                    c.AddEvidence("books stood at " + c.baselineGoodwill);
                    CAOrganization claimant = ByKey(other);
                    claimant?.Record("relations",
                        (playerIsProtector ? "support payment" : "tribute")
                        + " falls due under the " + agreement.kind
                        + " agreement");
                    continue;
                }

                // Evidence accrues: the payer's ground burning excuses.
                if (open.explanation.NullOrEmpty())
                {
                    List<Map> maps = Find.Maps;
                    for (int m = 0; m < maps.Count; m++)
                    {
                        if (!maps[m].IsPlayerHome) continue;
                        try
                        {
                            if (GenHostility.AnyHostileActiveThreatToPlayer(
                                maps[m], false))
                            {
                                open.explanation = "payment impossible -"
                                    + " their ground was under attack";
                                break;
                            }
                        }
                        catch { }
                    }
                }
                int goodwillNow = open.baselineGoodwill;
                try { goodwillNow = fac.PlayerGoodwill; } catch { }
                int delta = open.baselineGoodwill == int.MinValue
                    ? 0 : goodwillNow - open.baselineGoodwill;
                if (delta >= 2)
                {
                    open.stage = "closed";
                    open.resolvedTick = now;
                    open.resolution = "performed - tribute rendered"
                        + " (books rose " + delta + ")";
                    ByKey(other)?.Record("relations",
                        "tribute rendered by " + colony.name);
                    colony.Record("relations", "tribute rendered to "
                        + (ByKey(other)?.name ?? other));
                    continue;
                }
                if (now < open.deadlineTick) continue;
                open.stage = "judgment";
                open.allegationTick = now;
                CAOrganization cl = ByKey(other);
                if (delta >= 1)
                {
                    open.resolution = "excused - partial payment made";
                    open.AddEvidence("books rose only " + delta);
                    cl?.Record("relations",
                        "partial tribute accepted from " + colony.name);
                }
                else if (!open.explanation.NullOrEmpty())
                {
                    open.resolution = "excused - " + open.explanation;
                    cl?.Record("relations", "tribute excused - "
                        + open.explanation);
                }
                else
                {
                    open.resolution = "breach - deliberately withheld";
                    agreement.dissolvedTick = now;
                    agreement.broken = true;
                    cl?.Record("agreement-broken", "tribute deliberately"
                        + " withheld by " + colony.name
                        + " - the agreement is void");
                    colony.Record("agreement-broken",
                        "withheld tribute from "
                        + (cl?.name ?? other) + " - agreement void");
                    Messages.Message((cl?.name ?? other)
                        + " declares the " + agreement.kind
                        + " agreement void - tribute withheld.",
                        MessageTypeDefOf.NegativeEvent, false);
                }
                open.stage = "closed";
                open.resolvedTick = now;
            }
        }

        // NPC protectors send support; protected settlements and tribute
        // payers send tribute. The amount follows actual production and a
        // settlement with no active population sends nothing.
        private void NpcPaymentFlow(CAAgreement comp, string npcKey, int now,
            CAOrganization colony)
        {
            bool npcOwes;
            string label;
            if (comp.kind == "protection")
            {
                bool npcIsProtector = comp.protectorKey == npcKey;
                npcOwes = true;
                label = npcIsProtector ? "support payment" : "tribute";
            }
            else
            {
                npcOwes = comp.payerKey == npcKey;
                label = "tribute";
            }
            if (!npcOwes) return;
            int last = comp.lastNpcPayTick > 0
                ? comp.lastNpcPayTick : comp.sinceTick;
            if (now - last < 900000) return;
            CAOrganization payer = ByKey(npcKey);
            if (payer == null || payer.lastPopulation == 0) return;
            // A protector withholds support while the protected settlement
            // maintains an agreement made without consent.
            if (label == "support payment")
            {
                bool sanction = false;
                for (int i = 0; i < agreements.Count; i++)
                    if (agreements[i].Active && agreements[i].unauthorized
                        && agreements[i].Involves("player"))
                    { sanction = true; break; }
                if (sanction)
                {
                    comp.lastNpcPayTick = now;
                    payer.Record("relations", "support payment withheld -"
                        + " sanctions over unauthorized diplomacy");
                    colony.Record("relations", payer.name
                        + " withholds support payment - sanctions over our"
                        + " unauthorized diplomacy");
                    return;
                }
            }
            Map home = null;
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
                if (maps[i].IsPlayerHome) { home = maps[i]; break; }
            if (home == null) return;
            // The agreement creates the obligation; actual stored treasury
            // constrains what can be dispatched. Settlement capability is a
            // read model and never changes the amount.
            int amount = 80;
            // A protected settlement with enough public support may withhold
            // tribute after imposed policies or broken agreements.
            if (label == "tribute" && comp.kind == "protection"
                && comp.protectorKey == "player"
                && comp.pendingArriveTick <= 0)
            {
                int grievances = payer.CountKind("policy-imposed", 0)
                    + payer.CountKind("agreement-broken", 0);
                int last2 = comp.lastNpcPayTick > 0
                    ? comp.lastNpcPayTick : comp.sinceTick;
                if (grievances >= 2 && payer.publicSupport >= 0.5f
                    && now - last2 >= 900000)
                {
                    comp.lastNpcPayTick = now;
                    CABreachCase dc = OpenBreachCase("deliver tribute",
                        npcKey, "player", now, comp.id);
                    dc.kind = "tribute";
                    dc.AddEvidence("treasury bore it; grievances cited - "
                        + payer.CountKind("policy-imposed", 0)
                        + " imposed policies, "
                        + payer.CountKind("agreement-broken", 0)
                        + " broken agreements");
                    dc.stage = "closed";
                    dc.resolvedTick = now;
                    dc.resolution = "breach - deliberately withheld";
                    payer.Record("agreement-broken",
                        "tribute withheld from " + colony.name
                        + " - grievances cited");
                    colony.Record("relations", payer.name
                        + " withholds tribute deliberately - grievances"
                        + " cited");
                    Messages.Message(payer.name + " withholds tribute -"
                        + " grievances cited. It demands greater independence.",
                        MessageTypeDefOf.NegativeEvent,
                        false);
                    return;
                }
            }

            // A committed delivery arrives or records an interception.
            if (comp.pendingArriveTick > 0)
            {
                if (now < comp.pendingArriveTick) return;
                int arriving = comp.pendingAmount;
                string form = comp.pendingForm ?? "delivery";
                comp.pendingArriveTick = -1;
                comp.pendingAmount = 0;
                Map home0 = null;
                List<Map> maps0 = Find.Maps;
                for (int i = 0; i < maps0.Count; i++)
                    if (maps0[i].IsPlayerHome) { home0 = maps0[i]; break; }
                if (home0 == null) return;
                bool ground = form != "drop pods";
                bool intercepted = false;
                if (ground)
                {
                    try
                    {
                        intercepted = GenHostility
                            .AnyHostileActiveThreatToPlayer(home0, false);
                    }
                    catch { }
                }
                if (intercepted)
                {
                    CABreachCase ic = OpenBreachCase("deliver " + label,
                        npcKey, "player", now, comp.id);
                    ic.kind = "tribute";
                    ic.AddEvidence("the " + form + " was intercepted on"
                        + " the road - goods lost");
                    ic.stage = "closed";
                    ic.resolvedTick = now;
                    ic.resolution = "excused - intercepted in transit";
                    payer.Record("relations", "our " + form + " to "
                        + colony.name + " was intercepted - " + arriving
                        + " silver lost");
                    colony.Record("relations", payer.name + "'s " + form
                        + " was intercepted on the road - " + arriving
                        + " silver lost");
                    Messages.Message(payer.name + "'s " + form
                        + " was intercepted - the " + label + " is lost"
                        + " on the road.", MessageTypeDefOf.NegativeEvent,
                        false);
                    return;
                }
                try
                {
                    Thing silver0 = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver0.stackCount = arriving;
                    IntVec3 spot0 = DropCellFinder.TradeDropSpot(home0);
                    if (form == "drop pods")
                        DropPodUtility.DropThingsNear(spot0, home0,
                            Gen.YieldSingle(silver0));
                    else
                        GenPlace.TryPlaceThing(silver0, spot0, home0,
                            ThingPlaceMode.Near);
                    payer.Record("relations", label + " delivered to "
                        + colony.name + " by " + form + " - " + arriving
                        + " silver");
                    colony.Record("relations", label + " received from "
                        + payer.name + " by " + form + " - " + arriving
                        + " silver");
                    Messages.Message(payer.name + "'s " + form
                        + " arrives: " + arriving + " silver (" + label
                        + ", " + comp.kind + " agreement).",
                        MessageTypeDefOf.PositiveEvent, false);
                }
                catch (Exception) { }
                return;
            }

            // Dispatch deducts the payment before travel begins.
            comp.lastNpcPayTick = now;
            int affordable = (int)Mathf.Min(amount, payer.treasury);
            if (affordable < amount / 4)
            {
                CABreachCase ec = OpenBreachCase("deliver " + label,
                    npcKey, "player", now, comp.id);
                ec.kind = "tribute";
                ec.AddEvidence("their treasury stood empty ("
                    + (int)payer.treasury + ")");
                ec.stage = "closed";
                ec.resolvedTick = now;
                ec.resolution = "excused - their treasury stood empty";
                payer.Record("relations", label + " unpaid - the treasury"
                    + " stands empty");
                colony.Record("relations", payer.name + " could not pay "
                    + label + " - their treasury stands empty");
                return;
            }
            payer.treasury -= affordable;
            if (affordable < amount)
            {
                CABreachCase pc = OpenBreachCase("deliver " + label,
                    npcKey, "player", now, comp.id);
                pc.kind = "tribute";
                pc.AddEvidence("owed " + amount + ", treasury bore "
                    + affordable);
                pc.stage = "closed";
                pc.resolvedTick = now;
                pc.resolution = "excused - partial from a thin treasury";
            }
            TechLevel tech = TechLevel.Neolithic;
            try
            {
                Faction f = FactionOfKey(npcKey);
                if (f != null) tech = f.def.techLevel;
            }
            catch { }
            string dispatchForm;
            int transit;
            if ((int)tech >= 5) { dispatchForm = "drop pods"; transit = 2500; }
            else if ((int)tech >= 4)
            { dispatchForm = "supply caravan"; transit = 15000; }
            else { dispatchForm = "pack train"; transit = 25000; }
            comp.pendingAmount = affordable;
            comp.pendingForm = dispatchForm;
            comp.pendingArriveTick = now + transit;
            payer.Record("relations", label + " dispatched to "
                + colony.name + " by " + dispatchForm + " - " + affordable
                + " silver" + (affordable < amount ? " (partial)" : ""));
            colony.Record("relations", payer.name + " dispatches " + label
                + " by " + dispatchForm + " - " + affordable + " silver"
                + (affordable < amount ? " (partial)" : ""));
        }

        private static Faction FactionOfKey(string key)
        {
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            if (regional == null) return null;
            for (int i = 0; i < regional.Records.Count; i++)
            {
                CARegionalSettlementRecord r = regional.Records[i];
                if (r.regionalId + "#" + r.slot == key) return r.faction;
            }
            return null;
        }

        // [arrival signature] the player's entrance, recorded once at
        // new game and consumed by settlement organizations as they seed
        // (seeding is staggered; the context waits for them).
        public bool arrivalLoud;
        public int arrivalTick = -1;
        public IntVec3 arrivalCell = IntVec3.Invalid;

        public void RecordArrival(bool loud, IntVec3 cell)
        {
            arrivalLoud = loud;
            arrivalTick = Find.TickManager.TicksGame;
            arrivalCell = cell;
        }

        // Disposable-fixture cleanup. Removes only organizations whose key
        // carries the given prefix, so a receipt cannot touch real records.
        internal int RemoveOrganizationsWithPrefix(string prefix)
        {
            return prefix.NullOrEmpty() ? 0
                : organizations.RemoveAll(o => o != null
                    && o.organizationKey != null
                    && o.organizationKey.StartsWith(prefix));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref authoringDataEpoch,
                "CA_authoringDataEpoch", 0);
            bool current = Scribe.mode == LoadSaveMode.Saving
                || CAAuthoringDataEpoch.IsCurrent(authoringDataEpoch);
            if (current)
            {
                Scribe_Values.Look(ref arrivalLoud, "CA_arrivalLoud", false);
                Scribe_Values.Look(ref arrivalTick, "CA_arrivalTick", -1);
                Scribe_Values.Look(ref arrivalCell, "CA_arrivalCell",
                    IntVec3.Invalid);
                Scribe_Collections.Look(ref organizations,
                    "CA_organizations", LookMode.Deep);
                Scribe_Collections.Look(ref frontierMapPlans,
                    "CA_frontierMapPlans", LookMode.Deep);
                Scribe_Collections.Look(ref agreements, "CA_agreements",
                    LookMode.Deep);
                Scribe_Values.Look(ref nextAgreementId,
                    "CA_nextAgreementId", 1);
                Scribe_Collections.Look(ref breachCases, "CA_breachCases",
                    LookMode.Deep);
                Scribe_Values.Look(ref nextCaseId, "CA_nextCaseId", 1);
                Scribe_Collections.Look(ref hostileActs, "CA_hostileActs",
                    LookMode.Deep);
                Scribe_Collections.Look(ref offers, "CA_offers",
                    LookMode.Deep);
                Scribe_Collections.Look(ref pendingGatherings,
                    "CA_pendingGatherings", LookMode.Deep);
                Scribe_Values.Look(ref nextOfferId, "CA_nextOfferId", 1);
                Scribe_Values.Look(ref lastInitiativeTick,
                    "CA_lastInitiativeTick", -999999);
                Scribe_Values.Look(ref offMapActivityCursor,
                    "CA_offMapActivityCursor", 0);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !current)
            {
                organizations = new List<CAOrganization>();
                frontierMapPlans = new List<CAFrontierMapPlan>();
                agreements = new List<CAAgreement>();
                breachCases = new List<CABreachCase>();
                hostileActs = new List<CAHostileActRecord>();
                offers = new List<CAAgreementOffer>();
                pendingGatherings = new List<CAPendingGathering>();
                nextAgreementId = 1;
                nextCaseId = 1;
                nextOfferId = 1;
                lastInitiativeTick = -999999;
                offMapActivityCursor = 0;
                arrivalLoud = false;
                arrivalTick = -1;
                arrivalCell = IntVec3.Invalid;
                authoringDataEpoch = CAAuthoringDataEpoch.Current;
                CAAuthoringDataEpoch.RecordDiscard("organization state");
            }
            if (organizations == null)
                organizations = new List<CAOrganization>();
            if (frontierMapPlans == null)
                frontierMapPlans = new List<CAFrontierMapPlan>();
            if (agreements == null)
                agreements = new List<CAAgreement>();
            if (breachCases == null) breachCases = new List<CABreachCase>();
            if (hostileActs == null)
                hostileActs = new List<CAHostileActRecord>();
            if (offers == null) offers = new List<CAAgreementOffer>();
            if (pendingGatherings == null)
                pendingGatherings = new List<CAPendingGathering>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && current)
                CAMembershipValidation.Run(organizations);
        }
    }

    // Checks member relations after loading a world.
    internal static class CAMembershipValidation
    {
        internal static void Run(
            IReadOnlyList<CAOrganization> organizations)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null || organizations == null) return;
            int problems = 0;
            int unresolved = 0;

            // Every member relation must identify both organizations.
            var seen = new HashSet<string>();
            for (int i = 0; i < ledger.Relations.Count; i++)
            {
                CARelation r = ledger.Relations[i];
                if (r == null) continue;
                bool federationMembership = r.roleDef
                    == CARelationRoleDefOf.CA_Role_FederationMember;
                bool settlementMembership = r.roleDef
                    == CARelationRoleDefOf.CA_Role_SettlementMember;
                if (!federationMembership && !settlementMembership)
                    continue;
                string parentNoun = federationMembership
                    ? "federation" : "faction organization";
                string memberNoun = federationMembership
                    ? "member organization" : "settlement organization";
                if (r.Party.kind != CARelationPartyKind.Organization)
                {
                    Log.Error("[CA][Organization] member relation " + r.id
                        + " has party kind " + r.Party.kind
                        + "; membership requires an Organization party");
                    problems++;
                }
                if (r.partyOrgKey.NullOrEmpty() || r.orgKey.NullOrEmpty())
                {
                    Log.Error("[CA][Organization] member relation " + r.id
                        + " is missing a " + memberNoun + " or "
                        + parentNoun + " reference");
                    problems++;
                }
                else
                {
                    // A relation is unusable if either organization is gone.
                    bool memberOk = false;
                    bool parentOk = false;
                    for (int o = 0; o < organizations.Count; o++)
                    {
                        string key = organizations[o]?.organizationKey;
                        if (key == r.partyOrgKey) memberOk = true;
                        if (key == r.orgKey) parentOk = true;
                    }
                    if (!memberOk && unresolved < 12)
                    {
                        Log.Warning("[CA][Organization] member relation "
                            + r.id + " names member organization \""
                            + r.partyOrgKey + "\" which does not exist");
                        unresolved++;
                        problems++;
                    }
                    if (!parentOk && unresolved < 12)
                    {
                        Log.Warning("[CA][Organization] member relation "
                            + r.id + " names " + parentNoun + " \""
                            + r.orgKey + "\" which does not exist");
                        unresolved++;
                        problems++;
                    }
                }
                if (!r.PartyConsistent)
                {
                    Log.Error("[CA][Organization] relation " + r.id
                        + " has inconsistent party fields");
                    problems++;
                }
                for (int d = 0; d < r.delegatedResponsibilities.Count; d++)
                    if (Array.IndexOf(CAResponsibilities.All,
                        r.delegatedResponsibilities[d]) < 0)
                    {
                        Log.Error("[CA][Organization] relation " + r.id
                            + " uses unknown responsibility \""
                            + r.delegatedResponsibilities[d] + "\"");
                        problems++;
                    }
                string edge = r.roleDef.defName + ":" + r.orgKey + "<-"
                    + r.partyOrgKey;
                if (!seen.Add(edge))
                {
                    Log.Warning("[CA][Organization] duplicate member edge "
                        + edge + "; only one active relation is supported");
                    problems++;
                }
            }

            if (problems > 0)
                Log.Warning("[CA][Organization] membership validation found "
                    + problems + " contract violation(s); membership and "
                    + "shared-responsibility answers may be unreliable until resolved");
        }

        // Reports each member's shared responsibilities without changing state.
        internal static string AsymmetryReceipt(string federationKey)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null) return "no organization relations";
            var text = new System.Text.StringBuilder();
            text.AppendLine("[CA][Organization] federation " + federationKey
                + " - shared responsibilities by member");
            foreach (CARelation r in ledger.FederationMemberships(
                federationKey))
            {
                var held = new List<string>();
                for (int i = 0; i < CAResponsibilities.All.Length; i++)
                    if (ledger.FederationManages(federationKey,
                        r.partyOrgKey, CAResponsibilities.All[i]))
                        held.Add(CAResponsibilities.All[i]);
                text.AppendLine("  " + r.partyOrgKey + ": federation holds "
                    + (held.Count == 0 ? "nothing"
                        : string.Join(", ", held.ToArray())));
            }
            text.AppendLine("  aggregate union (DIAGNOSTIC ONLY, never "
                + "authorization): " + string.Join(", ",
                    ledger.FederationSharedResponsibilities(
                        federationKey).ToArray()));
            return text.ToString();
        }
    }

    // Build the colony's organization from existing squad leads,
    // Ideoligion roles, arrangements, plans, and training.
    internal static class CAOrganizationInheritance
    {
        public static void SyncColony(CAOrganization org)
        {
            if (org == null || Current.Game == null) return;
            if (!ReferenceEquals(transientWorld, Find.World))
            {
                transientWorld = Find.World;
                laceLast.Clear();
                patrolPosted.Clear();
            }
            List<Pawn> colonists = PawnsFinder
                .AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
            var currentMembers = new List<int>();
            for (int i = 0; i < colonists.Count; i++)
                if (!currentMembers.Contains(colonists[i].thingIDNumber))
                    currentMembers.Add(colonists[i].thingIDNumber);
            currentMembers.Sort();
            org.memberPawnIds = currentMembers;

            SyncOffices(org, colonists);
            SyncGroups(org, colonists);
            SyncCustoms(org);
            CAPoliticalBeliefPractice.ReconcileCurrentStructure(org,
                CAFactionStateWorldComponent.Current
                    ?.Find(Faction.OfPlayer)?.factionStructure);
            SyncSecurityPractices(org);
        }

        private static void SyncOffices(CAOrganization org, List<Pawn> colonists)
        {
            var live = new Dictionary<string, CAOffice>();
            bool authorityChanged = false;

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                Precept_Role role = null;
                try { if (p.Ideo != null) role = p.Ideo.GetRole(p); }
                catch { }
                if (role != null && role.def != null)
                {
                    if (role.def.leaderRole)
                        AddOffice(live, "ideo-leader", role.LabelCap, 900, p,
                            "native leader role - speech, command abilities");
                    else if (role.def.roleTags != null
                        && role.def.roleTags.Contains("Moralist"))
                        AddOffice(live, "ideo-moralguide", role.LabelCap, 600,
                            p, "native moral guidance role");
                }
                if (SquadComponent.IsLeader(p))
                {
                    int sq = SquadComponent.SquadOf(p);
                    AddOffice(live, "squad-" + sq, "Squad " + sq + " lead",
                        300, p,
                        "may issue squad orders (relayed, command-standing checked)");
                }
                else if (SquadComponent.IsFireteamLeader(p))
                {
                    int sq2 = SquadComponent.SquadOf(p);
                    int ft = SquadComponent.FireteamOf(p);
                    AddOffice(live,
                        "squad-" + sq2 + "-team-" + ft,
                        "Squad " + sq2 + " fire team "
                        + (ft == 1 ? "A" : "B") + " lead",
                        100, p,
                        "may issue fire-team orders (relayed,"
                        + " command-standing checked)");
                }
            }

            for (int i = org.offices.Count - 1; i >= 0; i--)
            {
                CAOffice held = org.offices[i];
                CAOffice current;
                if (!live.TryGetValue(held.sourceKey, out current))
                {
                    org.Record("office dissolved - " + held.name
                        + (held.holderLabel != null
                            ? " (last held by " + held.holderLabel + ")" : ""));
                    org.offices.RemoveAt(i);
                    authorityChanged = true;
                    continue;
                }
                if (held.holderId != current.holderId)
                {
                    org.Record("office changed hands - " + held.name + ": "
                        + (held.holderLabel ?? "vacant") + " -> "
                        + current.holderLabel);
                    held.holderId = current.holderId;
                    authorityChanged = true;
                }
                held.holderLabel = current.holderLabel;
                held.seniority = current.seniority;
                held.grants = current.grants;
                live.Remove(held.sourceKey);
            }
            foreach (var pair in live)
            {
                org.Record("office established - " + pair.Value.name
                    + ", held by " + pair.Value.holderLabel);
                org.offices.Add(pair.Value);
                authorityChanged = true;
            }
            org.offices.SortByDescending(o => o.seniority);
            if (authorityChanged) CABehaviorRevisions.RoleChanged();
        }

        private static void AddOffice(Dictionary<string, CAOffice> live,
            string sourceKey, string defaultName, int seniority, Pawn holder,
            string grants)
        {
            if (live.ContainsKey(sourceKey)) return;
            live[sourceKey] = new CAOffice
            {
                sourceKey = sourceKey,
                name = defaultName,
                seniority = seniority,
                holderId = holder.thingIDNumber,
                holderLabel = holder.LabelShort,
                grants = grants
            };
        }

        private static void SyncGroups(CAOrganization org, List<Pawn> colonists)
        {
            // One generated group is the armed group. "Which faction controls
            // a settlement's armed group" is a stated cause; membership =
            // squad members. Other groups form through play.
            CAOrganizationGroup armed = null;
            for (int i = 0; i < org.groups.Count; i++)
                if (org.groups[i].name == "armed group")
                { armed = org.groups[i]; break; }
            var members = new List<int>();
            for (int i = 0; i < colonists.Count; i++)
                if (SquadComponent.SquadOf(colonists[i]) > 0)
                    members.Add(colonists[i].thingIDNumber);
            if (members.Count == 0)
            {
                if (armed != null) org.groups.Remove(armed);
                return;
            }
            if (armed == null)
            {
                armed = new CAOrganizationGroup { name = "armed group" };
                org.groups.Add(armed);
            }
            armed.memberIds = members;
            armed.standing = colonists.Count > 0
                ? (float)members.Count / colonists.Count : 0f;

            // The labor group contains non-squad colonists assigned to
            // construction, mining, growing, or crafting.
            CAOrganizationGroup labor = null;
            for (int i = 0; i < org.groups.Count; i++)
                if (org.groups[i].name == "labor")
                { labor = org.groups[i]; break; }
            var hands = new List<int>();
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (SquadComponent.SquadOf(p) > 0) continue;
                if (p.workSettings == null) continue;
                bool works = false;
                try
                {
                    works = p.workSettings.WorkIsActive(
                            WorkTypeDefOf.Construction)
                        || p.workSettings.WorkIsActive(
                            WorkTypeDefOf.Mining)
                        || p.workSettings.WorkIsActive(
                            WorkTypeDefOf.Growing)
                        || p.workSettings.WorkIsActive(
                            WorkTypeDefOf.Crafting);
                }
                catch { }
                if (works) hands.Add(p.thingIDNumber);
            }
            if (hands.Count == 0)
            {
                if (labor != null) org.groups.Remove(labor);
                return;
            }
            if (labor == null)
            {
                labor = new CAOrganizationGroup { name = "labor" };
                org.groups.Add(labor);
            }
            labor.memberIds = hands;
            labor.standing = colonists.Count > 0
                ? (float)hands.Count / colonists.Count : 0f;
        }

        private static void SyncCustoms(CAOrganization org)
        {
            bool anyPlans = false;
            var kindsSeen = new HashSet<CAArrangementKind>();
            bool stackActive = false;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var arr = maps[m].GetComponent<CAArrangementMapComponent>();
                if (arr != null)
                    for (int i = 0; i < arr.All.Count; i++)
                        kindsSeen.Add((CAArrangementKind)arr.All[i].kind);
                var plans = maps[m].GetComponent<CAPlanMapComponent>();
                if (plans != null && plans.All.Count > 0) anyPlans = true;
                var drills = maps[m].GetComponent<DrillsMapComponent>();
                if (drills != null && drills.StackCount() > 0)
                    stackActive = true;
            }
            bool anySquads = false;
            List<Pawn> colonists = PawnsFinder
                .AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
            for (int i = 0; i < colonists.Count; i++)
                if (SquadComponent.SquadOf(colonists[i]) > 0)
                { anySquads = true; break; }

            if (kindsSeen.Contains(CAArrangementKind.Formation))
                Adopt(org, "formation", "practiced");
            if (kindsSeen.Contains(CAArrangementKind.Line))
                Adopt(org, "line", "practiced");
            if (kindsSeen.Contains(CAArrangementKind.Hide))
                Adopt(org, "hide", "practiced");
            if (kindsSeen.Contains(CAArrangementKind.Ambush))
                Adopt(org, "ambush", "practiced");
            if (anyPlans) Adopt(org, "contingency planning", "practiced");
            if (anySquads) Adopt(org, "squad organization", "practiced");
            if (stackActive) Adopt(org, "stack and breach", "practiced");

        }

        private static void Adopt(CAOrganization org, string key,
            string source)
        {
            if (org.HasCustom(key)) return;
            org.customs.Add(new CAOrganizationCustom
            {
                key = key,
                source = source,
                adoptedTick = Find.TickManager.TicksGame
            });
            org.Record("custom established - " + key + " (" + source
                + ")");
        }

        private static void SyncSecurityPractices(CAOrganization org)
        {
            var liveKeys = new HashSet<long>();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var comp = maps[m].GetComponent<CAArrangementMapComponent>();
                if (comp == null) continue;
                for (int i = 0; i < comp.All.Count; i++)
                {
                    CAArrangement a = comp.All[i];
                    long key = ((long)maps[m].uniqueID << 24) | (uint)a.id;
                    liveKeys.Add(key);
                    if (Registered(org, maps[m].uniqueID, a.id)) continue;
                    string office = OfficeOf(org, a.ownerId);
                    org.securityPractices.Add(new CASecurityPractice
                    {
                        mapId = maps[m].uniqueID,
                        arrangementId = a.id,
                        kindLabel = CAArrangementMapComponent.KindNoun(
                            (CAArrangementKind)a.kind),
                        name = a.name,
                        assignedOffice = office
                    });
                    org.Record("security practice registered - " + a.name
                        + (office != null ? " (" + office + ")" : ""));
                }
            }
            for (int i = org.securityPractices.Count - 1; i >= 0; i--)
            {
                CASecurityPractice sp = org.securityPractices[i];
                if (sp.kindLabel == "patrol base") continue;
                long key = ((long)sp.mapId << 24) | (uint)sp.arrangementId;
                if (liveKeys.Contains(key)) continue;
                org.Record("security practice dissolved - " + sp.name);
                org.securityPractices.RemoveAt(i);
            }
        }

        private static bool Registered(CAOrganization org, int mapId, int id)
        {
            for (int i = 0; i < org.securityPractices.Count; i++)
                if (org.securityPractices[i].mapId == mapId
                    && org.securityPractices[i].arrangementId == id)
                    return true;
            return false;
        }

        // Colony pulse: claims derived from what the colony holds, public
        // support after losses, and automatic status reporting where the
        // organization adopted it. Doctrine is an
        // organization: Proactive pawns report because their colony
        // legislated reporting into existence, never because a tier says so.
        public static void ColonyPulse(CAOrganization org)
        {
            if (org == null || Current.Game == null) return;
            List<Pawn> colonists = PawnsFinder
                .AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;

            // Claims: core = home area; worked land = growing zones.
            // Display data - re-derived every 4th pulse, not every pulse.
            List<Map> maps = Find.Maps;
            if (Find.TickManager.TicksGame % 10000 < 2500)
            {
            org.claims.Clear();
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!map.IsPlayerHome) continue;
                int home = map.areaManager.Home.TrueCount;
                if (home > 0)
                    org.claims.Add(new CAClaim
                    {
                        kind = "core",
                        label = "home ground",
                        area = home,
                        mapId = map.uniqueID
                    });
                int worked = 0;
                List<Zone> zones = map.zoneManager.AllZones;
                for (int z = 0; z < zones.Count; z++)
                    if (zones[z] is RimWorld.Zone_Growing)
                        worked += zones[z].CellCount;
                if (worked > 0)
                    org.claims.Add(new CAClaim
                    {
                        kind = "worked land",
                        label = "growing zones",
                        area = worked,
                        mapId = map.uniqueID
                    });
            }
            }

            // Fold-back: losses cost the organization standing; quiet time
            // slowly restores it. "Resources and casualties alter internal
            // public support."
            int pop = colonists.Count;
            if (org.lastPopulation >= 0 && pop < org.lastPopulation)
            {
                int lost = org.lastPopulation - pop;
                org.publicSupport = Mathf.Max(0.3f,
                    org.publicSupport - 0.05f * lost);
                org.Record("losses", "losses suffered - " + lost
                    + " fewer than last accounting; public support now "
                    + org.publicSupport.ToStringPercent());
            }
            else if (org.publicSupport < 1f)
                org.publicSupport = Mathf.Min(1f, org.publicSupport + 0.002f);
            org.lastPopulation = pop;

            // Patrol bases: native zones registered as security practices,
            // posted/lapsed transitions logged. The zone is the surface;
            // the organization is the memory.
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!map.IsPlayerHome) continue;
                List<Zone> zones = map.zoneManager.AllZones;
                for (int z = 0; z < zones.Count; z++)
                {
                    Zone_CAPatrolBase pb = zones[z] as Zone_CAPatrolBase;
                    if (pb == null) continue;
                    bool found = false;
                    for (int i = 0; i < org.securityPractices.Count; i++)
                        if (org.securityPractices[i].kindLabel
                                == "patrol base"
                            && org.securityPractices[i].mapId == map.uniqueID
                            && org.securityPractices[i].arrangementId
                                == pb.ID)
                        {
                            found = true;
                            org.securityPractices[i].name = pb.label;
                            break;
                        }
                    if (!found)
                    {
                        org.securityPractices.Add(new CASecurityPractice
                        {
                            mapId = map.uniqueID,
                            arrangementId = pb.ID,
                            kindLabel = "patrol base",
                            name = pb.label
                        });
                        org.Record("security practice registered - "
                            + pb.label + " (patrol base)");
                    }
                    bool posted = pb.ArmedOccupants() > 0;
                    bool wasPosted;
                    int stateKey = map.uniqueID * 100000 + pb.ID;
                    if (!patrolPosted.TryGetValue(stateKey, out wasPosted))
                        wasPosted = false;
                    if (posted != wasPosted)
                    {
                        patrolPosted[stateKey] = posted;
                        if (posted)
                            org.Record(pb.label + " - SECURITY POSTED");
                        else if (wasPosted)
                            org.Record(pb.label + " - security lapsed");
                    }
                }
                for (int i = org.securityPractices.Count - 1; i >= 0; i--)
                {
                    CASecurityPractice sp = org.securityPractices[i];
                    if (sp.kindLabel != "patrol base"
                        || sp.mapId != map.uniqueID) continue;
                    bool alive = false;
                    for (int z = 0; z < zones.Count; z++)
                        if (zones[z] is Zone_CAPatrolBase
                            && ((Zone_CAPatrolBase)zones[z]).ID
                                == sp.arrangementId)
                        { alive = true; break; }
                    if (!alive)
                    {
                        org.Record("security practice dissolved - "
                            + sp.name + " (patrol base removed)");
                        org.securityPractices.RemoveAt(i);
                    }
                }
            }

            // Defensive works follow current construction policy. The colony's
            // organization maintains its drawn lines by blueprint, so
            // colonists perform the construction; nothing appears from
            // nowhere on the player's side.
            if (CAPolicyLookup.Colony("defense construction")
                == "maintain defenses")
            {
                int placed = 0;
                for (int m = 0; m < maps.Count && placed < 3; m++)
                {
                    Map map = maps[m];
                    if (!map.IsPlayerHome) continue;
                    var arrComp = CAArrangementMapComponent.For(map);
                    if (arrComp == null) continue;
                    for (int a = 0; a < arrComp.All.Count && placed < 3; a++)
                    {
                        CAArrangement arr = arrComp.All[a];
                        if (arr.ownerOrgKey != null
                            || arr.kind != (int)CAArrangementKind.Line)
                            continue;
                        for (int c = 0; c < arr.cells.Count && placed < 3;
                            c++)
                        {
                            IntVec3 cell = arr.cells[c];
                            if (!cell.InBounds(map) || !cell.Standable(map))
                                continue;
                            if (cell.GetEdifice(map) != null) continue;
                            if (AnyBlueprintAt(map, cell)) continue;
                            try
                            {
                                GenConstruct.PlaceBlueprintForBuild(
                                    ThingDefOf.Sandbags, cell, map,
                                    Rot4.North, Faction.OfPlayer,
                                    ThingDefOf.Cloth);
                                placed++;
                            }
                            catch { break; }
                        }
                    }
                }
                if (placed > 0)
                    org.Record("defense construction ordered - " + placed
                        + " sandbag positions blueprinted along the lines");
            }

            // Automatic status reporting runs only where adopted, with
            // Proactive or Autonomous behavior during an active threat.
            if (!org.HasCustom("status reporting")) return;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!map.IsPlayerHome) continue;
                bool threat;
                try
                {
                    threat = GenHostility.AnyHostileActiveThreatToPlayer(
                        map, countDormantPawnsAsHostile: false);
                }
                catch { threat = false; }
                if (!threat) continue;
                var pawns = map.mapPawns.FreeColonistsSpawned;
                int now = Find.TickManager.TicksGame;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (p.Downed || !CABehaviorGate.StableProfileAllows(p,
                            "communication.status_report"))
                        continue;
                    int last;
                    if (laceLast.TryGetValue(p.thingIDNumber, out last)
                        && now - last < 15000) continue;
                    ThreatContactSnapshot fact = default(
                        ThreatContactSnapshot);
                    bool hasFact = KnowledgeMapComponent.For(map)
                        ?.TryGetFreshestContact(p, out fact) == true;
                    if (!hasFact) continue;
                    var reportContext = CABehaviorContext.ForPawn(p,
                        CAAuthorityOrigin.Organization,
                        authoritySatisfied: org.HasCustom(
                            "status reporting"),
                        knowledgeSatisfied: fact.State
                            == ThreatContactState.Active,
                        knowledgeFresh: now - fact.SourceTick <= 2500,
                        liveValidated: true,
                        knowledgeRelayed: !fact.Evidence.IsDirect,
                        knowledgeAgeTicks: System.Math.Max(0,
                            now - fact.SourceTick),
                        knowledgeConfidence: fact.Evidence.Confidence,
                        knowledgeUncertainty: fact.Evidence.Uncertainty,
                        capabilitySatisfied: true, materialSatisfied: true,
                        currentIntentCompatible: true,
                        directPlayerOwnership: false,
                        authorityBasis:
                            "adopted colony status-reporting practice",
                        knowledgeBasis: "actor-held threat fact",
                        owner: "colony organization");
                    CABehaviorDecision reportDecision =
                        CABehaviorGate.Evaluate(
                            "communication.status_report", reportContext);
                    if (!reportDecision.Allowed) continue;
                    laceLast[p.thingIDNumber] = now;
                    CATrace.Pawn(p, "LACE (adopted practice): "
                        + CAStatusReport.For(p), anchor: p.Position);
                }
            }
        }

        private static readonly Dictionary<int, int> laceLast =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, bool> patrolPosted =
            new Dictionary<int, bool>();
        private static World transientWorld;

        private static bool AnyBlueprintAt(Map map, IntVec3 cell)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Blueprint || things[i] is Frame)
                    return true;
            return false;
        }

        // This cadence updates organizations that already exist as represented
        // institutions. A settlement record, map, resident population, or
        // capability assessment does not constitute an organization and may
        // not create one here.
        public static void SyncRegionalSettlements(
            CAOrganizationWorldComponent comp)
        {
            CARegionalWorldComponent regional = CARegionalWorldComponent
                .Current;
            if (regional == null) return;
            IReadOnlyList<CARegionalSettlementRecord> records =
                regional.Records;
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                if (record.lastMapId < 0 || record.faction == null) continue;
                Map map = FindMap(record.lastMapId);
                string key = record.regionalId + "#" + record.slot;
                CAOrganization org = comp.ByKey(key);
                if (org == null) continue;
                if (map != null)
                    RefreshSettlementOrg(org, record, map);
            }
            ReconcileHostileAgreements(comp.EnsureColony(), records);
        }

        private static Map FindMap(int uniqueId)
        {
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
                if (maps[i].uniqueID == uniqueId) return maps[i];
            return null;
        }

        // [arrival signature] What this settlement knows about the
        // player's entrance. A loud arrival was seen by everyone; a quiet
        // one is known only near the landing - the rest of the region
        // meets the newcomers when contact actually happens.
        private static void NoteArrival(CAOrganization org,
            CARegionalSettlementRecord record)
        {
            var comp = CAOrganizationWorldComponent.Current;
            if (comp == null || comp.arrivalTick < 0) return;
            if (Find.TickManager.TicksGame - comp.arrivalTick
                > 60000 * 15) return;
            if (comp.arrivalLoud)
            {
                org.Record("arrival", "saw fire streak across the sky - "
                    + "strangers fell onto this land");
                return;
            }
            if (record == null || record.localRect == CellRect.Empty
                || !comp.arrivalCell.IsValid) return;
            if (record.localRect.CenterCell.DistanceTo(comp.arrivalCell)
                <= 250f)
                org.Record("arrival",
                    "travelers arrived quietly and settled nearby");
        }

        internal static void RefreshDevelopmentAuthority(CAOrganization org,
            CARegionalSettlementRecord record, Map map)
        {
            CASettlementDevelopmentProposal proposal =
                CASettlementAssetRegistry.BuildInstitutionalProposal(record,
                    org, map);
            bool sitingFeasible = CASettlementAssetRegistry
                .CanExerciseInstitutionalDevelopment(map, record, proposal,
                    out string sitingBlocker);
            CASettlementAssetRegistry.RecordInstitutionalFacts(record,
                proposal, sitingFeasible, sitingBlocker);
            bool developmentAuthorized =
                CASettlementInstitutionalAuthorization
                    .TryAuthorizeLaterDevelopment(record, org, proposal,
                        out CABehaviorDecision developmentDecision,
                        out CAIntentContext developmentIntent);
            record.developmentBehaviorKey = developmentAuthorized
                ? developmentIntent.BehaviorKey : null;
            record.developmentEpisodeId = developmentAuthorized
                ? developmentIntent.EpisodeId : 0;
            record.developmentAuthorityOrigin = developmentAuthorized
                ? (int)developmentIntent.AuthorityOrigin : 0;
            record.developmentAuthorityIdentity = developmentAuthorized
                ? developmentIntent.AuthorityIdentity : null;
            record.developmentOwner = developmentAuthorized
                ? developmentIntent.OwnershipScope : org?.organizationKey;
            record.developmentProposer = org?.name ?? org?.organizationKey;
            record.developmentApprover = developmentAuthorized
                ? developmentIntent.AuthorityIdentity : null;
            record.developmentLaborSource = proposal.LaborBasis;
            record.developmentBeneficiaries = map == null
                ? new List<string>()
                : CAPopulationProjection.Residents(record, map)
                    .Where(pawn => pawn != null && !pawn.Dead
                        && pawn.Faction == record.faction
                        && pawn.RaceProps.Humanlike && !pawn.IsPrisoner)
                    .Select(pawn => pawn.LabelShort)
                    .Distinct().OrderBy(label => label,
                        StringComparer.Ordinal).ToList()
                ;
            if (record.developmentBeneficiaries.Count == 0)
                record.developmentBeneficiaries.Add(
                    "current settlement residents");
            record.developmentTargetOrDemand = developmentAuthorized
                ? developmentIntent.TargetOrDemand : proposal.StableSignature();
            record.developmentCreatedTick = developmentAuthorized
                ? developmentIntent.CreatedTick : -1;
            record.developmentAuthorized = developmentAuthorized;
            record.developmentExecutable = developmentAuthorized
                && proposal.FundingFeasible && proposal.MaterialFeasible
                && sitingFeasible;
            record.developmentBlocker = record.developmentExecutable
                ? null : !developmentAuthorized
                    ? developmentDecision.PrimaryReason
                    : !proposal.FundingFeasible
                        ? proposal.FundingBasis
                        : !proposal.MaterialFeasible
                            ? proposal.MaterialBasis
                            : sitingBlocker
                                ?? "later institutional development is not executable";
        }

        // Prints the initial layout so missing entrances or rooms are visible
        // in the generation receipt.
        private static void LogGraph(CARegionalSettlementRecord record,
            Map map)
        {
            try
            {
                CASettlementLayout l = record?.layout;
                if (l == null) return;
                var ways = new System.Text.StringBuilder();
                for (int i = 0; i < l.gates.Count; i++)
                {
                    if (i > 0) ways.Append(" ");
                    ways.Append(l.gates[i].x).Append(",")
                        .Append(l.gates[i].z).Append("w")
                        .Append(l.GateWidth(i));
                }
                Log.Message("[CA][Graph] " + (record.name ?? "?")
                    + " rect " + record.localRect + ": "
                    + l.gates.Count + " ways in ["
                    + ways + "], " + l.ways.Count + " street cells, "
                    + l.roomRoles.Count + " rooms ["
                    + string.Join(", ", System.Linq.Enumerable.ToArray(
                        System.Linq.Enumerable.Distinct(l.roomRoles)))
                    + "], " + l.utilityKinds.Count + " utilities, "
                    + l.roads.Count + " road cells, "
                    + l.approaches.Count + " approaches, core "
                    + l.core);
            }
            catch { }
        }



        private static void RefreshSettlementOrg(CAOrganization org,
            CARegionalSettlementRecord record, Map map)
        {
            int now = Find.TickManager.TicksGame;
            List<Pawn> residents = CAPopulationProjection.Residents(record,
                map);
            RefreshDevelopmentAuthority(org, record, map);

            // Population loss and warnings are observations of represented
            // events. This cadence does not appoint successors, create armed
            // groups, improve defenses, or infer an institution from a score.
            if (org.lastPopulation >= 0
                && residents.Count < org.lastPopulation)
            {
                int lost = org.lastPopulation - residents.Count;
                org.publicSupport = Mathf.Max(0.2f,
                    org.publicSupport - 0.06f * lost);
                org.Record("losses", "losses suffered - " + lost
                    + " residents fewer; public support now "
                    + org.publicSupport.ToStringPercent());
                if (residents.Count == 0)
                    NotifySettlementSilent(org, record);
            }
            org.lastPopulation = residents.Count;

            if (AlliedToPlayer(org) && now - org.lastWarningTick > 60000
                && record.localRect != CellRect.Empty
                && UnderAttack(record, map))
            {
                CAOrganizationWorldComponent world =
                    CAOrganizationWorldComponent.Current;
                if (world != null
                    && world.OpenCaseFor(org.organizationKey) == null)
                {
                    org.lastWarningTick = now;
                    world.OpenBreachCase("answer the warning", "player",
                        org.organizationKey, now + 60000, -1);
                    Messages.Message("Warning from " + record.name
                        + " (warning agreement): hostiles at their settlement.",
                        new LookTargets(record.localRect.CenterCell, map),
                        MessageTypeDefOf.ThreatSmall, false);
                    world.EnsureColony().Record("warning-received",
                        "warning received from " + record.name
                        + " - hostiles at their settlement; relief expected");
                }
            }
            ProcessBreachCase(org, record, map, now);
        }
        private static void NotifySettlementSilent(CAOrganization fallen,
            CARegionalSettlementRecord record)
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return;
            for (int i = 0; i < comp.Organizations.Count; i++)
            {
                CAOrganization other = comp.Organizations[i];
                if (other == fallen) continue;
                bool related = other.organizationKey == "player";
                if (!related && record.regionalId != null)
                    related = other.organizationKey.StartsWith(
                        record.regionalId + "#");
                if (!related) continue;
                other.Record("relations", "neighbor " + fallen.name
                    + " has gone silent - their ground stands unheld");
            }
        }

        // Breach handling records evidence, allegation, explanation, judgment,
        // and remedy. Not every failure is
        // betrayal - a colony fighting for its own life is excused, and the
        // case file says so in the open.
        private static void ProcessBreachCase(CAOrganization org,
            CARegionalSettlementRecord record, Map map, int now)
        {
            CAOrganizationWorldComponent wc =
                CAOrganizationWorldComponent.Current;
            if (wc == null) return;
            CABreachCase c = wc.OpenCaseFor(org.organizationKey);
            if (c == null) return;
            CAOrganization colony = wc.EnsureColony();

            // Evidence from both parties.
            c.stage = "evidence";
            bool performed = false;
            if (record.localRect != CellRect.Empty)
            {
                CellRect near = record.localRect.ExpandedBy(30);
                var cols = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < cols.Count; i++)
                    if (near.Contains(cols[i].Position))
                    { performed = true; break; }
            }
            if (performed)
            {
                int strength = 0;
                if (record.localRect != CellRect.Empty)
                {
                    CellRect near2 = record.localRect.ExpandedBy(30);
                    var cols2 = map.mapPawns.FreeColonistsSpawned;
                    for (int i = 0; i < cols2.Count; i++)
                        if (near2.Contains(cols2[i].Position)) strength++;
                }
                string degree = strength >= 3 ? "substantial relief ("
                    + strength + ")" : "token relief (" + strength + ")";
                c.AddEvidence("relief arrived in person - " + degree);
                c.stage = "closed";
                c.resolution = "performed - " + degree;
                c.resolvedTick = now;
                org.Record("warning-honored",
                    colony.name + " answered our warning - " + degree);
                colony.Record("warning-honored",
                    "answered " + record.name + "'s warning - " + degree);
                return;
            }
            // The obliged side's circumstances - observable excuses.
            bool ownGroundBurning = false;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                if (!maps[m].IsPlayerHome) continue;
                try
                {
                    if (GenHostility.AnyHostileActiveThreatToPlayer(
                        maps[m], countDormantPawnsAsHostile: false))
                    { ownGroundBurning = true; break; }
                }
                catch { }
            }
            if (ownGroundBurning
                && (c.explanation.NullOrEmpty()))
            {
                c.explanation = "their own ground was under attack during"
                    + " the window";
                c.AddEvidence("obliged party engaged at home");
            }

            if (now < c.deadlineTick) return;

            // The claimant alleges from what reached its settlement.
            c.stage = "allegation";
            c.allegationTick = now;
            c.AddEvidence("no relief seen by the deadline");
            org.Record("relations", "alleges the warning went unanswered");

            // A standing excuse, or a
            // materially incapable obliged party, defeats the allegation.
            bool incapable = true;
            var colonists = PawnsFinder
                .AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
            for (int i = 0; i < colonists.Count; i++)
                if (!colonists[i].Downed && !colonists[i]
                        .WorkTagIsDisabled(WorkTags.Violent))
                { incapable = false; break; }
            if (incapable && c.explanation.NullOrEmpty())
                c.explanation = "no able residents could respond";

            // The claimant records its judgment.
            c.stage = "judgment";
            bool excused = !c.explanation.NullOrEmpty();

            // Apply the remedy.
            c.stage = "closed";
            c.resolvedTick = now;
            if (excused)
            {
                c.resolution = "excused - " + c.explanation;
                org.Record("relations", "excused the unanswered warning - "
                    + c.explanation);
                colony.Record("relations", record.name
                    + " excused our absence - " + c.explanation);
                return;
            }
            bool bound = wc.HasActiveAgreement("player", org.organizationKey,
                "defense", "protection");
            if (bound)
            {
                var list = wc.ActiveAgreementsInvolving(org.organizationKey);
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Involves("player")
                        && (list[i].kind == "defense"
                            || list[i].kind == "protection"))
                    {
                        list[i].dissolvedTick = now;
                        list[i].broken = true;
                    }
                c.resolution = "breach - agreement void";
                org.Record("agreement-broken", "abandoned under a defense"
                    + " obligation - the agreement is void");
                colony.Record("agreement-broken", "abandoned " + record.name
                    + " under a defense obligation - agreement void");
                Messages.Message(record.name + " declares the defense"
                    + " agreement void - their warning went unanswered.",
                    MessageTypeDefOf.NegativeEvent, false);
            }
            else
            {
                c.resolution = "grievance recorded";
                org.Record("warning-ignored",
                    "our warning went unanswered");
            }
        }

        private static bool UnderAttack(CARegionalSettlementRecord record,
            Map map)
        {
            if (record.localRect == CellRect.Empty) return false;
            var allPawns = map.mapPawns.AllPawnsSpawned;
            CellRect reach = record.localRect.ExpandedBy(20);
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn h = allPawns[i];
                if (h.Downed || h.Faction == null
                    || !reach.Contains(h.Position)) continue;
                try
                {
                    if (record.faction != null
                        && h.Faction.HostileTo(record.faction)
                        && h.Faction != Faction.OfPlayer) return true;
                }
                catch { }
            }
            return false;
        }

        private static bool AlliedToPlayer(CAOrganization org)
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return false;
            // Warnings travel through an allied relation or an agreement.
            if (comp.HasActiveAgreement("player", org.organizationKey,
                "shared warnings", "defense", "protection")) return true;
            CAOrganization colony = comp.ByKey("player");
            if (colony == null) return false;
            return CAOrganizationStances.Between(colony, org) == "Ally";
        }

        private static void ReconcileHostileAgreements(
            CAOrganization colony,
            IReadOnlyList<CARegionalSettlementRecord> records)
        {
            CAOrganizationWorldComponent world =
                CAOrganizationWorldComponent.Current;
            if (world == null || colony == null || records == null) return;
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                if (record?.faction == null || record.lastMapId < 0
                    || record.faction.PlayerRelationKind
                        != FactionRelationKind.Hostile) continue;
                string key = record.regionalId + "#" + record.slot;
                CAOrganization other = world.ByKey(key);
                List<CAAgreement> active =
                    world.ActiveAgreementsInvolving(key);
                for (int agreementIndex = 0;
                    agreementIndex < active.Count; agreementIndex++)
                {
                    CAAgreement agreement = active[agreementIndex];
                    if (!agreement.Involves("player")) continue;
                    int now = Find.TickManager.TicksGame;
                    agreement.dissolvedTick = now;
                    agreement.broken = true;
                    CABreachCase breach = world.OpenBreachCase(
                        agreement.kind == "non-aggression"
                            ? "refrain from hostile acts"
                            : agreement.kind == "military access"
                                ? "keep passage unmolested"
                                : "keep the peace the agreement stood on",
                        "player", key, now, agreement.id);
                    breach.kind = agreement.kind;
                    CAHostileActRecord act = world.RecentActAgainst(
                        record.faction.loadID, 5000);
                    string attribution;
                    if (act == null)
                        attribution = "initiated by player conduct (engine "
                            + "attribution); no acting hand identified";
                    else if (act.mentalBreak)
                        attribution = "initiated by " + act.pawnLabel
                            + " in a mental break - a dissident act beyond "
                            + "any authorization";
                    else if (act.authorized)
                        attribution = "initiated by " + act.pawnLabel
                            + " acting under command - an authorized act";
                    else
                        attribution = "initiated by " + act.pawnLabel
                            + " acting alone - no command in evidence";
                    breach.AddEvidence(agreement.kind == "military access"
                        ? "access attacked contrary to terms; " + attribution
                        : attribution);
                    if (act != null && !act.authorized)
                    {
                        colony.Record("relations", "the act against "
                            + record.name + " was unauthorized - "
                            + act.pawnLabel + "'s hand, not the organization's");
                        other?.Record("relations", "the hostile act is claimed "
                            + "unauthorized - a dispute remains on record");
                    }
                    breach.stage = "closed";
                    breach.resolvedTick = now;
                    breach.resolution = "breach - voided by hostility";
                    colony.Record("agreement-broken",
                        "agreement voided by hostility with " + record.name
                        + " - " + agreement.kind);
                    other?.Record("agreement-broken",
                        "agreement voided by hostility - " + agreement.kind);
                }
            }
        }

        private static string OfficeOf(CAOrganization org, int pawnId)
        {
            for (int i = 0; i < org.offices.Count; i++)
                if (org.offices[i].holderId == pawnId)
                    return org.offices[i].name;
            return null;
        }
    }

    // Choosing a policy sends a speaker to a gathering. The same action can
    // begin by right-clicking a valid gathering place.
    public sealed class CAPendingGathering : IExposable
    {
        public int speakerId;
        public string key;
        public string value;
        public IntVec3 spot;
        public int mapId;
        public int startTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref speakerId, "speakerId", 0);
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref spot, "spot");
            Scribe_Values.Look(ref mapId, "mapId", 0);
            Scribe_Values.Look(ref startTick, "startTick", 0);
        }
    }

    // Policy is adopted where people assemble: a
    // leader at a table with the colony around them. The act belongs to
    // a pawn and a place; the tab only remembers what was decided.
    public static class CAGathering
    {
        public static bool CanConvene(Pawn speaker, IntVec3 spot,
            Map map, out string why)
        {
            why = null;
            if (speaker == null || map == null) { why = "no one"; return false; }
            if (!CAOrganizationAuthority.HasColonyCommand(speaker)
                && !SquadComponent.IsLeader(speaker))
            {
                why = "only someone who speaks for the colony can call"
                    + " a gathering";
                return false;
            }
            bool seat = false;
            List<Thing> here = spot.GetThingList(map);
            for (int i = 0; i < here.Count; i++)
            {
                ThingDef d = here[i].def;
                if (d == null) continue;
                if (d.surfaceType == SurfaceType.Eat
                    || d.defName == "Campfire"
                    || (d.building != null && d.building.isSittable))
                { seat = true; break; }
            }
            if (!seat)
            {
                why = "gatherings need a table or a fire to stand around";
                return false;
            }
            int near = 0;
            var cols = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < cols.Count; i++)
                if (cols[i] != speaker
                    && cols[i].Position.InHorDistOf(spot, 12f)) near++;
            if (near < 1)
            {
                why = "no one is here to hear it";
                return false;
            }
            return true;
        }

        // Stage from the menu: find a speaker and a live gathering spot;
        // send them walking. Returns false with the reason when the act
        // cannot happen - and then it just does not happen.
        public static bool Stage(string key, string value, out string why)
        {
            why = null;
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) { why = "no world"; return false; }
            CAOrganization org = comp.EnsureColony();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!map.IsPlayerHome) continue;
                Pawn speaker = null;
                var cols = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < cols.Count; i++)
                    if (!cols[i].Downed
                        && (CAOrganizationAuthority.HasColonyCommand(
                                cols[i])
                            || SquadComponent.IsLeader(cols[i])))
                    { speaker = cols[i]; break; }
                if (speaker == null)
                {
                    why = "no one with standing to speak";
                    continue;
                }
                CACulture culture = CACultureLongitudinalMapComponent.For(map)
                    ?.PlayerLocalCulture;
                List<IntVec3> candidates = map.listerThings.AllThings
                    .Where(thing => thing != null && thing.Spawned
                        && (thing.def?.surfaceType == SurfaceType.Eat
                            || thing.def?.defName == "Campfire"
                            || (thing.def?.building != null
                                && thing.def.building.isSittable)))
                    .Select(thing => thing.Position).Distinct()
                    .Where(cell =>
                    {
                        string reason;
                        return CanConvene(speaker, cell, map, out reason);
                    })
                    .OrderByDescending(cell => GatheringScore(cell, speaker,
                        cols, culture))
                    .ThenBy(cell => cell.x).ThenBy(cell => cell.z).ToList();
                IntVec3 spot = candidates.Count == 0
                    ? IntVec3.Invalid : candidates[0];
                if (!spot.IsValid)
                {
                    why = "no gathering is possible - it needs a table or"
                        + " fire with people around it";
                    continue;
                }
                comp.StageGathering(new CAPendingGathering
                {
                    speakerId = speaker.thingIDNumber,
                    key = key,
                    value = value,
                    spot = spot,
                    mapId = map.uniqueID,
                    startTick = Find.TickManager.TicksGame
                });
                try
                {
                    Job go = JobMaker.MakeJob(JobDefOf.Goto, spot);
                    go.playerForced = true;
                    speaker.jobs.TryTakeOrderedJob(go);
                }
                catch { }
                Messages.Message(speaker.LabelShort + " sets out to speak"
                    + " the law - " + key + ": " + value + ".",
                    new LookTargets(spot, map),
                    MessageTypeDefOf.NeutralEvent, false);
                return true;
            }
            if (why == null) why = "no gathering is possible";
            return false;
        }

        private static float GatheringScore(IntVec3 cell, Pawn speaker,
            List<Pawn> colonists, CACulture culture)
        {
            int near = colonists.Count(pawn => pawn != null
                && pawn.Position.InHorDistOf(cell, 12f));
            CACulturalMeaningResolution meaning = CACultureModel.Resolve(
                culture, CASocialSubjectRegistry.PublicGathering);
            // Culture ranks real gathering places only. Standing, attendance,
            // reachability, and the player-authored policy remain unchanged.
            float affinity = Mathf.Clamp01((meaning.Approval + 100f) / 200f)
                * Mathf.Clamp01(meaning.Salience / 100f);
            return near * (1f + affinity * 0.8f)
                - cell.DistanceTo(speaker.Position) * 0.05f;
        }

        public static void Convene(Pawn speaker, IntVec3 spot, Map map,
            string key, string value)
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return;
            CAOrganization org = comp.EnsureColony();
            var heard = new List<Pawn>();
            var cols = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < cols.Count; i++)
                if (cols[i].Position.InHorDistOf(spot, 12f))
                    heard.Add(cols[i]);
            CAPolicyRecord rec = null;
            for (int i = 0; i < org.policies.Count; i++)
                if (org.policies[i].key == key)
                { rec = org.policies[i]; break; }
            if (rec == null)
            {
                rec = new CAPolicyRecord { key = key };
                org.policies.Add(rec);
            }
            rec.value = value;
            rec.adoptedTick = Find.TickManager.TicksGame;
            rec.generatedBy = null;
            org.Record("policy", "law adopted at a gathering of "
                + heard.Count + " - " + key + ": " + value + " (spoken"
                + " by " + speaker.LabelShort + ")");
            for (int i = 0; i < heard.Count; i++)
                CATrace.Pawn(heard[i], "heard the law spoken - " + key
                    + ": " + value, anchor: spot);
            Messages.Message(speaker.LabelShort + " speaks the law to "
                + heard.Count + ": " + key + " is now " + value + ".",
                new LookTargets(spot, map),
                MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // A practice enters an organization when an instructor teaches it to an
    // audience at a real place.
    public static class CATeaching
    {
        public static List<string> Teachable(Pawn teacher,
            CAOrganization org)
        {
            var result = new List<string>();
            if (teacher == null || org == null) return result;
            int skill = 0;
            try
            {
                var sk = teacher.skills;
                if (sk != null)
                    skill = Mathf.Max(
                        sk.GetSkill(SkillDefOf.Shooting).Level,
                        sk.GetSkill(SkillDefOf.Melee).Level);
            }
            catch { }
            string[] all = CACustomCatalog.Adoptable;
            for (int i = 0; i < all.Length; i++)
            {
                if (org.HasCustom(all[i])) continue;
                // What a teacher can teach is what they could plausibly
                // know: advanced tactics require combat skill; reporting is
                // a habit anyone disciplined can instill.
                int need = all[i] == "ambush" ? 10
                    : all[i] == "line" ? 6
                    : all[i] == "formation" ? 4 : 3;
                if (all[i] == "status reporting")
                {
                    bool disciplined = false;
                    try
                    {
                        disciplined = Disposition.Of(teacher).discipline
                            >= 0.55f;
                    }
                    catch { }
                    if (!disciplined) continue;
                }
                else if (skill < need) continue;
                result.Add(all[i]);
            }
            return result;
        }

        public static void Teach(Pawn teacher, string key, IntVec3 spot,
            Map map)
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return;
            CAOrganization org = comp.EnsureColony();
            var learners = new List<Pawn>();
            var cols = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < cols.Count; i++)
                if (cols[i] != teacher
                    && cols[i].Position.InHorDistOf(spot, 14f)
                    && !cols[i].Downed) learners.Add(cols[i]);
            if (learners.Count == 0)
            {
                Messages.Message("No one is close enough to be taught.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (org.HasCustom(key)) return;
            org.customs.Add(new CAOrganizationCustom
            {
                key = key,
                source = "taught by " + teacher.LabelShort,
                adoptedTick = Find.TickManager.TicksGame
            });
            org.Record("custom", key + " was taught to "
                + learners.Count + " by " + teacher.LabelShort
                + " - the practice is ours now");
            CATrace.Pawn(teacher, "taught " + key + " to "
                + learners.Count + " pawn(s)", anchor: spot);
            for (int i = 0; i < learners.Count; i++)
                CATrace.Pawn(learners[i], "learned " + key + " from "
                    + teacher.LabelShort, anchor: spot);
            Messages.Message(teacher.LabelShort + " teaches " + key
                + " to " + learners.Count + " - the colony knows it now.",
                new LookTargets(spot, map),
                MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // Affiliated frontier residents join the colony without relocating.
    // Their holding keeps its name, organization, and claims.
    public static class CAFrontierAffiliation
    {
        public static void InviteInPerson(Pawn caller,
            CAOrganization org)
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null || caller == null) return;
            comp.EnsureColony().Record("relations", caller.LabelShort
                + " called on " + org.name + " to ask them in");
            Invite(comp, org);
        }

        public static void Invite(CAOrganizationWorldComponent comp,
            CAOrganization org)
        {
            if (comp == null || org == null) return;
            CAOrganization colony = comp.EnsureColony();
            float score;
            string breakdown;
            string hint;
            CAOrganization broker;
            string verdict = CAWillingness.Evaluate(colony, org,
                "defense", "general", 0, true, null, false, out score,
                out breakdown, out hint, out broker);
            bool flagless = org.standingNote != null
                && org.standingNote.StartsWith("unaffiliated");
            bool accepted = verdict == "accepted"
                || (flagless && verdict == "counter");
            if (!accepted)
            {
                colony.Record("relations", "the " + org.name
                    + " declined to affiliate [" + breakdown + "]");
                org.Record("relations", "declined to throw in with "
                    + colony.name);
                Messages.Message(org.name + " declines to affiliate. ("
                    + breakdown + ")", MessageTypeDefOf.NeutralEvent,
                    false);
                return;
            }
            int converted = 0;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var pawns = maps[m].mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (!org.memberPawnIds.Contains(p.thingIDNumber))
                        continue;
                    if (p.Dead || p.Faction == Faction.OfPlayer) continue;
                    try
                    {
                        p.SetFaction(Faction.OfPlayer);
                        converted++;
                    }
                    catch { }
                }
            }
            org.affiliatedWithPlayer = true;
            org.Record("relations", "affiliated with " + colony.name
                + "; residents joined without relocating");
            colony.Record("agreement-made", org.name
                + " affiliated; " + converted + " residents joined"
                + " without relocating");
            Messages.Message(org.name + " affiliates with the colony - "
                + converted + " frontier folk join without leaving their"
                + " ground.", MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // A player warning requires a known threat approaching the settlement.
    // The warning is recorded and can satisfy a warning agreement.
    public static class CAWarningSurface
    {
        public static bool CanWarn(CAOrganization org, out string whyNot)
        {
            whyNot = null;
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            if (regional == null)
            { whyNot = "no regional world"; return false; }
            CARegionalSettlementRecord record = null;
            for (int i = 0; i < regional.Records.Count; i++)
            {
                CARegionalSettlementRecord r = regional.Records[i];
                if (r.regionalId + "#" + r.slot == org.organizationKey)
                { record = r; break; }
            }
            if (record == null || record.localRect == CellRect.Empty)
            { whyNot = "their ground is unknown"; return false; }
            Map map = null;
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
                if (maps[i].uniqueID == record.lastMapId)
                { map = maps[i]; break; }
            if (map == null)
            { whyNot = "their ground is not materialized"; return false; }
            int now = Find.TickManager.TicksGame;
            if (now - org.lastWarnedByPlayerTick < 60000)
            { whyNot = "already warned recently"; return false; }

            // The threat must be approaching their ground: near but not
            // already upon them (a warning about the axe mid-swing is no
            // warning).
            CellRect approach = record.localRect.ExpandedBy(45);
            CellRect upon = record.localRect.ExpandedBy(20);
            var approachingIds = new HashSet<int>();
            var all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn h = all[i];
                if (h.Downed || h.Faction == null
                    || h.Faction == Faction.OfPlayer) continue;
                if (!approach.Contains(h.Position)
                    || upon.Contains(h.Position)) continue;
                try
                {
                    if (record.faction != null
                        && h.Faction.HostileTo(record.faction))
                        approachingIds.Add(h.thingIDNumber);
                }
                catch { }
            }
            if (approachingIds.Count == 0)
            {
                whyNot = "no hostiles approaching their ground";
                return false;
            }

            // The colony must hold a fresh contact on one of those hostiles;
            // knowing that
            // trouble exists somewhere is not knowing who is walking
            // toward the neighbor. The mechlink shared picture already
            // lives inside per-pawn contacts, so scanning every colonist
            // honors comms doctrine without a side channel.
            KnowledgeMapComponent know = KnowledgeMapComponent.For(map);
            if (know == null)
            { whyNot = "no known threat to speak of"; return false; }
            bool identified = false;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count && !identified; i++)
            {
                List<ThreatContactSnapshot> contacts =
                    know.FreshContacts(colonists[i]);
                for (int c = 0; c < contacts.Count; c++)
                    if (approachingIds.Contains(contacts[c].HostileId))
                    { identified = true; break; }
            }
            if (!identified)
            {
                whyNot = "the approaching force is not among our known"
                    + " contacts";
                return false;
            }
            return true;
        }

        public static void Send(CAOrganization org, Pawn carrier)
        {
            Send(org);
            if (carrier != null)
                CATrace.Pawn(carrier, "carried the warning to "
                    + org.name, anchor: carrier.Position);
        }

        public static void Send(CAOrganization org)
        {
            CAOrganizationWorldComponent wc =
                CAOrganizationWorldComponent.Current;
            if (wc == null) return;
            CAOrganization colony = wc.EnsureColony();
            int now = Find.TickManager.TicksGame;
            org.lastWarnedByPlayerTick = now;
            org.Record("warning-shared", colony.name
                + " warned us of hostiles approaching our ground");
            colony.Record("warning-shared", "warned "
                + org.name + " of hostiles approaching their ground");
            // The garrison reacts: their lord stands the armed residents
            // to - sleepers wake, posts are re-anchored. A warning that
            // changes nothing is theater; this one moves people.
            try
            {
                CARegionalWorldComponent regional =
                    CARegionalWorldComponent.Current;
                if (regional != null)
                    for (int i = 0; i < regional.Records.Count; i++)
                    {
                        CARegionalSettlementRecord r = regional.Records[i];
                        if (r.regionalId + "#" + r.slot
                            != org.organizationKey) continue;
                        List<Map> maps2 = Find.Maps;
                        for (int m = 0; m < maps2.Count; m++)
                        {
                            if (maps2[m].uniqueID != r.lastMapId) continue;
                            var lords = maps2[m].lordManager.lords;
                            for (int l = 0; l < lords.Count; l++)
                            {
                                if (lords[l].faction != r.faction) continue;
                                var toil = lords[l].CurLordToil
                                    as LordToil_CAOrganizationDefense;
                                if (toil == null) continue;
                                toil.StandTo();
                                org.Record("security",
                                    "the garrison stood to at the warning");
                                break;
                            }
                            break;
                        }
                        break;
                    }
            }
            catch { }
            if (wc.HasActiveAgreement("player", org.organizationKey,
                "shared warnings", "protection", "defense"))
            {
                CABreachCase c = wc.OpenBreachCase("share what is known",
                    "player", org.organizationKey, now, -1);
                c.kind = "shared warnings";
                c.AddEvidence("information was known, sharing was"
                    + " required, a channel existed - and it was used");
                c.stage = "closed";
                c.resolution = "performed";
                c.resolvedTick = now;
            }
            Messages.Message("Warning sent to " + org.name + ".",
                MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // The visible surface for offices, groups, customs, security, and
    // decision readable in-game. Offices are renameable - the player names
    // things in this fiction, the mod never does.
    public class MainTabWindow_CAOrganization : MainTabWindow
    {
        private Vector2 scroll;
        private Vector2 listScroll;
        private string selectedKey = "player";
        private readonly HashSet<string> collapsed = new HashSet<string>();
        // The tab redraws every frame while open; these lists change at
        // most per tick. Cache them so a frame is lookups, not allocs.
        private int uiCacheTick = -1;
        private string uiCacheKey;
        private List<CAAgreement> cachedAgreements;
        private List<CABreachCase> cachedCases;

        private void RefreshUiCache(CAOrganizationWorldComponent comp,
            string key)
        {
            int now = Find.TickManager.TicksGame;
            if (now == uiCacheTick && key == uiCacheKey) return;
            uiCacheTick = now;
            uiCacheKey = key;
            cachedAgreements = comp.ActiveAgreementsInvolving(key);
            cachedCases = comp.CasesInvolving(key);
        }

        private bool Section(ref float y, float width, string label,
            int count)
        {
            y += 4f;
            Rect r = new Rect(0f, y, width, 26f);
            y += 26f;
            Widgets.DrawLightHighlight(r);
            bool isCollapsed = collapsed.Contains(label);
            Widgets.Label(r, ((isCollapsed ? "+ " : "- ") + label
                + (count >= 0 ? " (" + count + ")" : ""))
                .Colorize(ColoredText.TipSectionTitleColor));
            if (Widgets.ButtonInvisible(r))
            {
                if (isCollapsed) collapsed.Remove(label);
                else collapsed.Add(label);
            }
            return !isCollapsed;
        }

        public static string ProtectorOf(
            CAOrganizationWorldComponent comp, string protectedKey)
        {
            var list = comp.ActiveAgreementsInvolving(protectedKey);
            for (int i = 0; i < list.Count; i++)
                if (list[i].kind == "protection"
                    && list[i].Involves("player"))
                    return list[i].protectorKey;
            return null;
        }

        // An imposed policy takes effect, records a breach of the protection
        // agreement, and reduces the settlement's public support.
        public static void ImposePolicy(CAOrganizationWorldComponent comp,
            CAOrganization protectedSettlement, string key, string value)
        {
            CAOrganization colony = comp.EnsureColony();
            int now = Find.TickManager.TicksGame;
            CAPolicyRecord rec = null;
            for (int i = 0; i < protectedSettlement.policies.Count; i++)
                if (protectedSettlement.policies[i].key == key)
                { rec = protectedSettlement.policies[i]; break; }
            if (rec == null)
            {
                rec = new CAPolicyRecord { key = key };
                protectedSettlement.policies.Add(rec);
            }
            rec.value = value;
            rec.adoptedTick = now;
            rec.generatedBy = null;
            protectedSettlement.Record("policy-imposed", "policy imposed by "
                + colony.name + " - " + key + ": " + value);
            colony.Record("relations", "imposed " + key + " (" + value
                + ") on " + protectedSettlement.name);
            protectedSettlement.publicSupport = Mathf.Max(0.2f,
                protectedSettlement.publicSupport - 0.1f);
            var list = comp.ActiveAgreementsInvolving(protectedSettlement.organizationKey);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].kind != "protection"
                    || !list[i].Involves("player")) continue;
                CABreachCase bc = comp.OpenBreachCase(
                    "respect internal autonomy", "player",
                    protectedSettlement.organizationKey, now, list[i].id);
                bc.kind = "protection";
                bc.AddEvidence("policy " + key
                    + " was imposed by the protector");
                bc.stage = "closed";
                bc.resolvedTick = now;
                bc.resolution = "breach - internal autonomy violated";
                break;
            }
            Messages.Message(protectedSettlement.name + " accepts the imposed "
                + key + ". Public support falls to "
                + protectedSettlement.publicSupport.ToStringPercent() + ".",
                MessageTypeDefOf.NegativeEvent, false);
        }

        private static Color StanceColor(string stance)
        {
            if (stance == "Ally") return new Color(0.5f, 0.95f, 0.5f);
            if (stance == "Hostile") return new Color(0.95f, 0.45f, 0.4f);
            return Color.white;
        }

        private static string OrganizationKind(CAOrganization organization)
        {
            if (organization == null) return "organization";
            if (organization.organizationKey == "player") return "colony";
            if (organization.IsFederation) return "federation";
            if (organization.IsFactionOrganization)
                return "faction organization";
            if (organization.organizationKind == CAOrganizationKind.Household)
                return "household";
            if (organization.organizationKind == CAOrganizationKind.Group)
                return "group";
            return "settlement";
        }

        public override Vector2 RequestedTabSize
        {
            get { return new Vector2(980f, 640f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return;
            comp.EnsureColony();

            // Left: every organization the world knows - the colony first,
            // then each seeded settlement. The regional world is already
            // socially organized; this is where that shows.
            Rect listRect = new Rect(0f, 0f, 230f, inRect.height);
            IReadOnlyList<CAOrganization> all = comp.Organizations;
            Rect listView = new Rect(0f, 0f, listRect.width - 16f,
                all.Count * 42f + 4f);
            Widgets.BeginScrollView(listRect, ref listScroll, listView);
            float ly = 0f;
            for (int i = 0; i < all.Count; i++)
            {
                Rect row = new Rect(0f, ly, listView.width, 40f);
                if (all[i].organizationKey == selectedKey)
                    Widgets.DrawHighlightSelected(row);
                else Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(row.x + 4f, row.y + 2f,
                    row.width - 8f, 20f), all[i].name);
                GUI.color = Color.grey;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(row.x + 4f, row.y + 21f,
                    row.width - 8f, 18f),
                    all[i].organizationKey == "player" ? "your colony"
                        : all[i].IsFederation
                            ? "federation · " + all[i].MemberKeys.Count
                                + " members"
                        : all[i].IsFactionOrganization
                            ? "faction organization · "
                                + all[i].SettlementMemberKeys.Count
                                + " settlements"
                        : all[i].offices.Count + " office(s), "
                            + all[i].securityPractices.Count + " practice(s)");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row))
                    selectedKey = all[i].organizationKey;
                ly += 42f;
            }
            Widgets.EndScrollView();

            CAOrganization org = comp.ByKey(selectedKey)
                ?? comp.EnsureColony();

            Rect detail = new Rect(listRect.xMax + 10f, 0f,
                inRect.width - listRect.width - 10f, inRect.height);
            GUI.BeginGroup(detail);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, detail.width - 100f, 34f),
                org.name + " - " + OrganizationKind(org));
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(detail.width - 92f, 2f, 88f,
                26f), "Manual"))
                Find.WindowStack.Add(new Dialog_CAManual());

            Rect outRect = new Rect(0f, 40f, detail.width,
                detail.height - 44f);
            float viewH = ContentHeight(org, outRect.width - 20f);
            Rect view = new Rect(0f, 0f, outRect.width - 20f, viewH);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;

            Widgets.Label(Row(ref y, view.width, 24f), org.standingNote);
            Widgets.Label(Row(ref y, view.width, 24f),
                "Public support: " + org.publicSupport.ToStringPercent());
            if (org.organizationKey != "player" && !org.IsFederation
                && !org.IsFactionOrganization)
                Widgets.Label(Row(ref y, view.width, 24f),
                    "Treasury: " + (int)org.treasury
                    + " (production income minus obligations)");
            y += 6f;

            if (org.IsFactionOrganization)
            {
                List<string> settlementKeys = org.SettlementMemberKeys;
                if (Section(ref y, view.width, "Settlements",
                    settlementKeys.Count))
                {
                    CAOrganizationRelationsWorldComponent membershipLedger =
                        CAOrganizationRelationsWorldComponent.Current;
                    for (int i = 0; i < settlementKeys.Count; i++)
                    {
                        string settlementKey = settlementKeys[i];
                        CAOrganization settlement = comp.ByKey(settlementKey);
                        CARelation membership = membershipLedger?
                            .SettlementMembership(org.organizationKey,
                                settlementKey);
                        string shared = membership?.delegatedResponsibilities
                            == null || membership.delegatedResponsibilities.Count == 0
                                ? "none"
                                : string.Join(", ", membership
                                    .delegatedResponsibilities.ToArray());
                        Rect memberRow = Row(ref y, view.width, 26f);
                        Widgets.DrawHighlightIfMouseover(memberRow);
                        Widgets.Label(memberRow,
                            (settlement?.name ?? settlementKey)
                            + " · shared: " + shared);
                        if (settlement != null
                            && Widgets.ButtonInvisible(memberRow))
                            selectedKey = settlement.organizationKey;
                    }
                    Grey(ref y, view.width, "Faction-wide responsibilities: "
                        + (org.SharedSettlementResponsibilities.Count == 0
                            ? "none"
                            : string.Join(", ", org
                                .SharedSettlementResponsibilities.ToArray())));
                }
                y += 6f;
            }

            if (!org.IsFactionOrganization)
            {
            if (Section(ref y, view.width, "Offices",
                org.offices.Count))
            {
            if (org.offices.Count == 0)
                Grey(ref y, view.width, "No offices.");
            for (int i = 0; i < org.offices.Count; i++)
            {
                CAOffice o = org.offices[i];
                Rect r = Row(ref y, view.width, 26f);
                Widgets.Label(new Rect(r.x, r.y, r.width - 90f, r.height),
                    o.name + " - " + (o.holderLabel ?? "vacant")
                    + "  (seniority " + o.seniority + "; " + o.grants + ")");
                if (org.organizationKey == "player" && Widgets.ButtonText(
                    new Rect(r.xMax - 84f, r.y, 80f, 24f), "Rename"))
                    Find.WindowStack.Add(new Dialog_CARenameOffice(o));
            }
            }
            y += 6f;

            if (Section(ref y, view.width, "Groups", org.groups.Count))
            {
            if (org.groups.Count == 0)
                Grey(ref y, view.width, "No groups.");
            for (int i = 0; i < org.groups.Count; i++)
                Widgets.Label(Row(ref y, view.width, 24f),
                    org.groups[i].name + " - " + org.groups[i].memberIds.Count
                    + " members, standing "
                    + org.groups[i].standing.ToStringPercent());
            }
            y += 6f;

            if (Section(ref y, view.width,
                "Customs",
                org.customs.Count))
            {
            if (org.customs.Count == 0)
                Grey(ref y, view.width, "No established practices.");
            for (int i = 0; i < org.customs.Count; i++)
                Widgets.Label(Row(ref y, view.width, 24f),
                    org.customs[i].key + " ("
                    + org.customs[i].source + ")");
            if (org.organizationKey == "player")
            {
                string[] adoptable = CACustomCatalog.Adoptable;
                var missing = new List<string>();
                for (int i = 0; i < adoptable.Length; i++)
                    if (!org.HasCustom(adoptable[i]))
                        missing.Add(adoptable[i]);
                if (missing.Count > 0)
                    Grey(ref y, view.width, "Not established: "
                        + string.Join(", ", missing.ToArray())
                        + ". A pawn who knows one can teach it.");
            }
            }
            y += 6f;

            if (Section(ref y, view.width, "Security",
                org.securityPractices.Count))
            {
            if (org.securityPractices.Count == 0)
                Grey(ref y, view.width, "No security practices.");
            for (int i = 0; i < org.securityPractices.Count; i++)
            {
                CASecurityPractice sp = org.securityPractices[i];
                Widgets.Label(Row(ref y, view.width, 24f),
                    sp.name + " - " + sp.kindLabel
                    + (sp.assignedOffice != null
                        ? ", assigned: " + sp.assignedOffice : ""));
            }
            }
            y += 6f;
            }

            RefreshUiCache(comp, org.organizationKey);
            List<CAAgreement> myAgreementsCount = cachedAgreements;
            if (Section(ref y, view.width, "Agreements",
                myAgreementsCount.Count))
            {
            List<CAAgreement> myAgreements = myAgreementsCount;
            if (myAgreements.Count == 0)
                Grey(ref y, view.width, "No active agreements.");
            for (int i = 0; i < myAgreements.Count; i++)
            {
                CAAgreement c = myAgreements[i];
                CAOrganization other = comp.ByKey(
                    c.OtherParty(org.organizationKey));
                Widgets.Label(Row(ref y, view.width, 24f),
                    (other != null ? other.name : "unknown") + " - "
                    + c.Summary());
            }
            if (org.IsFederation)
            {
                Grey(ref y, view.width, "Federation: "
                    + org.MemberKeys.Count + " members; shared: "
                    + string.Join(", ",
                        org.SharedResponsibilities.ToArray())
                    + (org.sunsetTick > 0
                        ? "; provisional until day "
                        + (org.sunsetTick / 60000) : ""));
            }
            if (org.organizationKey != "player" && !org.IsFederation
                && !org.IsFactionOrganization)
            {
                // The tab reads state. Proposals are
                // carried by a person: select them and right-click the
                // neighbor's ground.
                Grey(ref y, view.width, "Select a pawn and right-click this"
                    + " settlement to propose an agreement.");
                // Frontier residents can join the colony without relocating.
                // The holding remains a separate site.
                if (org.organizationKey.StartsWith("frontier:")
                    && !org.affiliatedWithPlayer)
                {
                    Grey(ref y, view.width, "Send an envoy to invite this"
                        + " holding to join the colony.");
                }
                else if (org.affiliatedWithPlayer)
                {
                    Grey(ref y, view.width, "Residents joined the colony;"
                        + " the holding remains separate.");
                }

                // Sharing a warning requires known hostiles actually
                // approaching their ground, and it is remembered.
                string whyNot;
                bool canWarn = CAWarningSurface.CanWarn(org, out whyNot);
                Grey(ref y, view.width, canWarn
                    ? "Select a pawn and right-click this settlement to send"
                    + " a warning."
                    : "No warning available: " + whyNot);
            }
            }
            List<CABreachCase> cases = cachedCases;
            if (cases.Count > 0
                && Section(ref y, view.width, "Obligations and disputes",
                    cases.Count))
            {
                int shown = 0;
                for (int i = cases.Count - 1; i >= 0 && shown < 6; i--)
                {
                    CABreachCase bc = cases[i];
                    shown++;
                    Widgets.Label(Row(ref y, view.width, 24f),
                        bc.obligation + " - "
                        + (bc.Open
                            ? "OPEN, stage " + bc.stage + ", deadline day "
                            + (bc.deadlineTick / 60000)
                            : bc.resolution)
                        + (bc.evidence.NullOrEmpty()
                            ? "" : " (" + bc.evidence + ")"));
                }
                y += 6f;
            }
            if (org.organizationKey == "player")
            {
                List<CAOrganization> federationMembers =
                    Dialog_CAFoundFederation.EligibleMembers();
                Grey(ref y, view.width, federationMembers.Count > 0
                    ? "Eligible federation members: "
                        + federationMembers.Count
                    : "No nearby defense or protection partner currently "
                        + "accepts a federation.");
                Rect fr = Row(ref y, view.width, 28f);
                if (federationMembers.Count > 0 && Widgets.ButtonText(
                    new Rect(fr.x, fr.y + 2f, 240f, 24f),
                    "Found federation..."))
                    Find.WindowStack.Add(new Dialog_CAFoundFederation());
                IReadOnlyList<CAAgreementOffer> incoming = comp.Offers;
                if (incoming.Count > 0
                    && Section(ref y, view.width, "Incoming proposals",
                        incoming.Count))
                {
                    for (int i = incoming.Count - 1; i >= 0; i--)
                    {
                        CAAgreementOffer off = incoming[i];
                        CAOrganization from = comp.ByKey(off.fromKey);
                        Rect orow = Row(ref y, view.width, 26f);
                        Widgets.Label(new Rect(orow.x, orow.y,
                            orow.width - 180f, orow.height),
                            (from != null ? from.name : off.fromKey)
                            + ": " + off.kind
                            + (off.protectorKey == "player"
                                ? " (you protect them)"
                                : off.protectorKey != null
                                ? " (they protect you)" : ""));
                        if (Widgets.ButtonText(new Rect(orow.xMax - 172f,
                            orow.y, 80f, 24f), "Accept"))
                        {
                            comp.AddAgreement(new CAAgreement
                            {
                                kind = off.kind,
                                partyA = "player",
                                partyB = off.fromKey,
                                scopeKind = off.scopeKind,
                                protectorKey = off.protectorKey,
                                payerKey = off.payerKey,
                                sunsetTick = off.sunsetDays > 0
                                    ? Find.TickManager.TicksGame
                                    + off.sunsetDays * 60000 : -1
                            });
                            org.Record("agreement-made",
                                "accepted " + off.kind + " proposed by "
                                + (from != null ? from.name : off.fromKey));
                            from?.Record("agreement-made",
                                "our " + off.kind + " proposal was"
                                + " accepted by " + org.name);
                            comp.RemoveOffer(off);
                        }
                        else if (Widgets.ButtonText(
                            new Rect(orow.xMax - 84f, orow.y, 80f, 24f),
                            "Decline"))
                        {
                            org.Record("agreement-refused",
                                "declined " + off.kind + " proposed by "
                                + (from != null ? from.name : off.fromKey));
                            from?.Record("agreement-refused",
                                "our " + off.kind + " proposal was"
                                + " declined by " + org.name);
                            comp.RemoveOffer(off);
                        }
                        Grey(ref y, view.width, "Reason: " + off.reason);
                    }
                }
            }
            y += 6f;

            List<CAOrganization> neighbors =
                CAOrganizationStances.Neighbors(org, comp);
            if (Section(ref y, view.width, "Neighbor relations",
                neighbors.Count))
            {
            if (neighbors.Count == 0)
                Grey(ref y, view.width, "No known neighbors.");
            for (int i = 0; i < neighbors.Count; i++)
            {
                CAOrganization neighbor = neighbors[i];
                string stance = CAOrganizationStances.Between(org, neighbor);
                GUI.color = StanceColor(stance);
                Widgets.Label(Row(ref y, view.width, 24f),
                    neighbor.name + " - " + stance);
                GUI.color = Color.white;
            }
            }
            y += 6f;

            if (Section(ref y, view.width, "Policies",
                org.policies.Count))
            {
            if (org.organizationKey == "player")
            {
                for (int i = 0; i < CAPolicyCatalog.Keys.Length; i++)
                {
                    string key = CAPolicyCatalog.Keys[i];
                    string current = null;
                    for (int j = 0; j < org.policies.Count; j++)
                        if (org.policies[j].key == key)
                        { current = org.policies[j].value; break; }
                    Rect pr = Row(ref y, view.width, 26f);
                    Widgets.Label(new Rect(pr.x, pr.y, pr.width - 90f,
                        pr.height), key + ": "
                        + (current ?? CAPolicyCatalog.ValuesOf(key)[0]
                        + " (default)"));
                    if (Widgets.ButtonText(
                        new Rect(pr.xMax - 84f, pr.y, 80f, 24f), "Change"))
                    {
                        var vopts = new List<FloatMenuOption>();
                        string[] vals = CAPolicyCatalog.ValuesOf(key);
                        for (int v = 0; v < vals.Length; v++)
                        {
                            string val = vals[v];
                            vopts.Add(new FloatMenuOption(val, delegate
                            {
                                string why;
                                if (!CAGathering.Stage(key, val, out why))
                                    Messages.Message("Cannot propose policy: "
                                        + why + ".",
                                        MessageTypeDefOf.RejectInput,
                                        false);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(vopts));
                    }
                }
                Grey(ref y, view.width, "Policy changes require a gathering.");
            }
            else if (org.policies.Count == 0)
                Grey(ref y, view.width, "No policies recorded.");
            for (int i = 0; i < org.policies.Count; i++)
                Widgets.Label(Row(ref y, view.width, 24f),
                    org.policies[i].key + ": " + org.policies[i].value
                    + (org.organizationKey == "player" ? ""
                        : " (imposed by protector)"));
            if (org.organizationKey != "player"
                && !org.IsFactionOrganization)
            {
                bool playerProtectsThem = comp.HasActiveAgreement(
                    "player", org.organizationKey, "protection")
                    && ProtectorOf(comp, org.organizationKey) == "player";
                if (playerProtectsThem)
                    Grey(ref y, view.width, "Send an envoy to impose a colony"
                        + " policy under the protection agreement.");
            }
            }
            y += 6f;

            if (Section(ref y, view.width, "Claims", org.claims.Count))
            {
            if (org.claims.Count == 0)
                Grey(ref y, view.width, "No claims.");
            for (int i = 0; i < org.claims.Count; i++)
                Widgets.Label(Row(ref y, view.width, 24f),
                    org.claims[i].kind + " - " + org.claims[i].label + ", "
                    + org.claims[i].area + " cells");
            }
            y += 6f;

            if (Section(ref y, view.width, "Decision history",
                org.decisionHistory.Count))
            {
            int start = Mathf.Max(0, org.decisionHistory.Count - 30);
            if (org.decisionHistory.Count == 0)
                Grey(ref y, view.width, "empty");
            for (int i = org.decisionHistory.Count - 1; i >= start; i--)
            {
                CADecisionEntry e = org.decisionHistory[i];
                Widgets.Label(Row(ref y, view.width, 24f),
                    "day " + (e.tick / 60000) + " [" + e.kind + "]: "
                    + e.text);
            }
            }

            Widgets.EndScrollView();
            GUI.EndGroup();
        }

        private float ContentHeight(CAOrganization org, float width)
        {
            int rows = 4 + org.offices.Count + org.groups.Count
                + org.customs.Count + CACustomCatalog.Adoptable.Length
                + org.securityPractices.Count
                + CAOrganizationStances.Neighbors(org,
                    CAOrganizationWorldComponent.Current).Count
                + org.claims.Count + CAPolicyCatalog.Keys.Length
                + org.SettlementMemberKeys.Count
                + Mathf.Min(30, org.decisionHistory.Count) + 22;
            return rows * 26f + 80f;
        }

        private Rect Row(ref float y, float width, float h)
        {
            Rect r = new Rect(0f, y, width, h);
            y += h;
            return r;
        }

        private void Header(ref float y, float width, string label)
        {
            y += 4f;
            Rect r = Row(ref y, width, 26f);
            Widgets.DrawLightHighlight(r);
            Widgets.Label(r, label.Colorize(ColoredText.TipSectionTitleColor));
        }

        private void Grey(ref float y, float width, string text)
        {
            GUI.color = Color.grey;
            Widgets.Label(Row(ref y, width, 24f), text);
            GUI.color = Color.white;
        }
    }

    public class Dialog_CARenameOffice : Window
    {
        private readonly CAOffice office;
        private string current;

        public Dialog_CARenameOffice(CAOffice office)
        {
            this.office = office;
            current = office.name;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(360f, 140f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, 24f),
                "Rename office");
            current = Widgets.TextField(
                new Rect(0f, 30f, inRect.width, 30f), current);
            if (Widgets.ButtonText(
                new Rect(inRect.width - 110f, inRect.height - 34f, 110f, 30f),
                "OK"))
            {
                if (!current.NullOrEmpty()) office.name = current;
                Close();
            }
        }
    }

    // In-game reference for the organization mechanics.
    public class Dialog_CAManual : Window
    {
        private Vector2 topicScroll;
        private Vector2 bodyScroll;
        private int selected;

        private static readonly string[][] Topics =
        {
            new[] { "Settlement generation",
                "Settlement layout follows the available land. Roads divide "
                + "the site, buildings fill usable blocks, and walls follow "
                + "the resulting boundary. Materials, paving, and defenses "
                + "depend on local resources and development." },
            new[] { "Settlement composition",
                "A starting program exists only where its saved contract names "
                + "the need, operator, labor, knowledge, material, access, and "
                + "maintenance it requires." },
            new[] { "Provision arrangements",
                "Each provision arrangement names its actual operator, funding, "
                + "stock, material nodes, access rule, and eligible residents. "
                + "Missing parts block that arrangement." },
            new[] { "Repairs and research",
                "Residents repair damaged settlement buildings and replace "
                + "destroyed program assets through normal work. Active "
                + "research programs can produce equipment and medicine." },
            new[] { "Guards and patrols",
                "Fortified settlements can maintain outposts and scheduled "
                + "patrol routes. Patrol policy determines whether guards "
                + "report contact, fall back, or fight." },
            new[] { "Roads",
                "Settlements build roads toward outposts and allied neighbors. "
                + "Construction uses settlement funds and available workers." },
            new[] { "Water and boats",
                "Shore settlements can build piers and boats. Boat type follows "
                + "local development. Boats carry pawns and cargo and can be "
                + "traded, captured, or destroyed." },
            new[] { "Frontier holdings",
                "Frontier sites range from cabins to developed homesteads. "
                + "They may belong to a faction or remain unaffiliated. "
                + "Frequency controls holding count. Size controls residents "
                + "and material form within local land limits." },
            new[] { "Organizations",
                "Settlements track offices, policies, claims, security "
                + "practices, funds, relations, public support, and decisions. "
                + "The player colony uses the same system." },
            new[] { "Leadership",
                "Offices have a holder, seniority, and granted authority. "
                + "Succession selects a living resident and records the change. "
                + "Low public support can block settlement agreements." },
            new[] { "Customs and policies",
                "Practiced or adopted customs unlock organization actions. "
                + "Policies govern custody, schedules, defensive posture, and "
                + "construction. Changes are recorded in the decision history." },
            new[] { "Claims",
                "Claims record core ground, worked land, and road interests. "
                + "Map borders show friendly, neutral, and hostile claims." },
            new[] { "Agreements",
                "Organizations can recognize claims, share warnings, grant "
                + "military access, or sign non-aggression, tribute, defense, "
                + "and protection agreements. Agreements record "
                + "scope, duration, contributions, and direction." },
            new[] { "Negotiation",
                "Acceptance uses relations, prior conduct, security, shared "
                + "Ideoligion, public support, and the proposed terms. "
                + "A near refusal may produce a counteroffer." },
            new[] { "Protection agreements",
                "A protected settlement keeps its offices, policies, and claims. "
                + "The agreement can require tribute, defense, support, or "
                + "approval for external agreements." },
            new[] { "Obligations",
                "Warnings, defense, and tribute create tracked obligations. "
                + "Evidence and available explanations determine whether a "
                + "failure becomes a recorded breach." },
            new[] { "Deliveries",
                "Payments travel by drop pod, caravan, or pack train. Travel "
                + "time and interception depend on the delivery method and "
                + "conditions at the destination." },
            new[] { "Faction structure",
                "Political beliefs and current faction structure are separate. "
                + "The fields are leadership, decisions, participation, "
                + "dissent, ownership, economy, work, support, membership, "
                + "status, local order, defense, and war conduct. Settlement "
                + "authority is set separately." },
            new[] { "Warnings",
                "Allies and agreement partners can share hostile contacts. "
                + "Garrisons respond according to current policy, and the "
                + "response affects later negotiations." },
        };

        public Dialog_CAManual()
        {
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(760f, 560f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f),
                "Organization guide");
            Text.Font = GameFont.Small;
            Rect left = new Rect(0f, 36f, 220f, inRect.height - 40f);
            Rect leftView = new Rect(0f, 0f, left.width - 16f,
                Topics.Length * 30f);
            Widgets.BeginScrollView(left, ref topicScroll, leftView);
            for (int i = 0; i < Topics.Length; i++)
            {
                Rect row = new Rect(0f, i * 30f, leftView.width, 28f);
                if (i == selected) Widgets.DrawHighlightSelected(row);
                else Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(row.x + 4f, row.y + 4f,
                    row.width - 8f, 22f), Topics[i][0]);
                if (Widgets.ButtonInvisible(row)) selected = i;
            }
            Widgets.EndScrollView();
            Rect right = new Rect(left.xMax + 12f, 36f,
                inRect.width - left.width - 12f, inRect.height - 40f);
            string body = Topics[selected][1];
            float h = Text.CalcHeight(body, right.width - 16f) + 10f;
            Rect rightView = new Rect(0f, 0f, right.width - 16f, h);
            Widgets.BeginScrollView(right, ref bodyScroll, rightView);
            Widgets.Label(new Rect(0f, 0f, rightView.width, h), body);
            Widgets.EndScrollView();
        }
    }

    // Compose an agreement with every term visible.
    public class Dialog_CAComposeAgreement : Window
    {
        private readonly CAOrganization target;
        private string kind = CAAgreementCatalog.Kinds[0];
        private string scopeKind = CAAgreementCatalog.ScopeKinds[0];
        private string scopeValue = "";
        private string sunsetDays = "0";
        private string contribA = "";
        private string contribB = "";
        private string direction;
        private string deniedReason;

        private readonly Pawn envoy;
        private readonly IntVec3 envoyDest;

        public Dialog_CAComposeAgreement(CAOrganization target)
        {
            this.target = target;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        // Envoy mode: the agreement is composed here, carried
        // in person, answered where the envoy stands.
        public Dialog_CAComposeAgreement(CAOrganization target, Pawn envoy,
            IntVec3 dest) : this(target)
        {
            this.envoy = envoy;
            envoyDest = dest;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(480f, 380f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, inRect.width, 30f),
                "Propose agreement - " + target.name);
            Text.Font = GameFont.Small;
            y += 36f;

            if (Widgets.ButtonText(new Rect(0f, y, 220f, 26f),
                "kind: " + kind))
                Pick(CAAgreementCatalog.Kinds, v => kind = v);
            y += 30f;
            if (Widgets.ButtonText(new Rect(0f, y, 220f, 26f),
                "scope: " + scopeKind))
                Pick(CAAgreementCatalog.ScopeKinds, v => scopeKind = v);
            y += 30f;
            if (kind == "protection")
            {
                if (direction != "we-protect"
                    && direction != "they-protect")
                    direction = "we-protect";
                if (Widgets.ButtonText(new Rect(0f, y, 220f, 26f),
                    direction == "we-protect"
                        ? "direction: we protect them"
                        : "direction: they protect us"))
                    Pick(new[] { "we-protect", "they-protect" },
                        v => direction = v);
                y += 30f;
            }
            else if (kind == "tribute")
            {
                if (direction != "we-pay" && direction != "they-pay")
                    direction = "we-pay";
                if (Widgets.ButtonText(new Rect(0f, y, 220f, 26f),
                    direction == "we-pay" ? "direction: we pay them"
                        : "direction: they pay us"))
                    Pick(new[] { "we-pay", "they-pay" },
                        v => direction = v);
                y += 30f;
            }
            else direction = null;
            Widgets.Label(new Rect(0f, y, 130f, 26f), "scope detail:");
            scopeValue = Widgets.TextField(
                new Rect(134f, y, inRect.width - 138f, 26f), scopeValue);
            y += 30f;
            Widgets.Label(new Rect(0f, y, 130f, 26f), "sunset (days):");
            sunsetDays = Widgets.TextField(
                new Rect(134f, y, 80f, 26f), sunsetDays);
            y += 30f;
            Widgets.Label(new Rect(0f, y, 130f, 26f), "we contribute:");
            contribA = Widgets.TextField(
                new Rect(134f, y, inRect.width - 138f, 26f), contribA);
            y += 30f;
            Widgets.Label(new Rect(0f, y, 130f, 26f), "they contribute:");
            contribB = Widgets.TextField(
                new Rect(134f, y, inRect.width - 138f, 26f), contribB);
            y += 40f;

            if (deniedReason != null)
            {
                GUI.color = Color.grey;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f),
                    "consent denied - " + deniedReason);
                GUI.color = Color.white;
                y += 26f;
                if (Widgets.ButtonText(new Rect(0f, y, 200f, 30f),
                    "sign without consent"))
                {
                    ProposeCore(true);
                    Close();
                }
                if (Widgets.ButtonText(new Rect(210f, y, 120f, 30f),
                    "withdraw"))
                    Close();
                return;
            }
            if (envoy != null)
            {
                if (Widgets.ButtonText(new Rect(0f, y, 160f, 30f),
                    "send the envoy"))
                {
                    int days2 = 0;
                    int.TryParse(sunsetDays, out days2);
                    var pc = CAParleyMapComponent.For(envoy.Map);
                    pc?.SendEnvoy(envoy, target, kind, envoyDest,
                        scopeKind, days2, direction, contribA, contribB);
                    Close();
                }
                return;
            }
            if (Widgets.ButtonText(new Rect(0f, y, 140f, 30f), "propose"))
            {
                if (Propose()) Close();
            }
        }

        private void Pick(string[] values, Action<string> set)
        {
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < values.Length; i++)
            {
                string v = values[i];
                opts.Add(new FloatMenuOption(v, delegate { set(v); }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        // A protected settlement asks its protector before signing an outside
        // agreement. It may sign after a refusal, but the breach is recorded
        // and support payments can be withheld.
        private bool Propose()
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return true;
            string protectorKey = null;
            var mine = comp.ActiveAgreementsInvolving("player");
            for (int i = 0; i < mine.Count; i++)
                if (mine[i].kind == "protection"
                    && mine[i].protectorKey != null
                    && mine[i].protectorKey != "player")
                { protectorKey = mine[i].protectorKey; break; }
            if (protectorKey != null)
            {
                CAOrganization protector = comp.ByKey(protectorKey);
                CAOrganization colonyC = comp.EnsureColony();
                bool approved = kind != "protection";
                string reason = "a protected settlement cannot take another protector";
                if (approved && protector != null)
                {
                    string their = CAOrganizationStances.Between(
                        protector, target);
                    if (their == "Hostile")
                    {
                        approved = false;
                        reason = protector.name
                            + " counts them an enemy";
                    }
                }
                if (approved)
                {
                    colonyC.Record("relations",
                        "external consent granted by "
                        + (protector?.name ?? protectorKey) + " for the "
                        + kind + " proposal");
                    protector?.Record("relations", "consented to "
                        + colonyC.name + "'s " + kind + " proposal to "
                        + target.name);
                }
                else
                {
                    deniedReason = reason;
                    return false;
                }
            }
            ProposeCore(false);
            return true;
        }

        private void ProposeCore(bool unauthorized)
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return;
            CAOrganization colony = comp.EnsureColony();
            int days = 0;
            int.TryParse(sunsetDays, out days);
            float score;
            string breakdown;
            string counterHint;
            CAOrganization broker;
            string verdict = CAWillingness.Evaluate(colony, target, kind,
                scopeKind, days, !contribA.NullOrEmpty(), direction,
                unauthorized, out score, out breakdown, out counterHint,
                out broker);

            if (verdict == "refused")
            {
                colony.Record("agreement-refused", "agreement refused by "
                    + target.name + " - " + kind + " [" + breakdown + "]");
                target.Record("agreement-refused", "agreement from "
                    + colony.name + " refused - " + kind);
                Messages.Message(target.name + " refuses the " + kind
                    + " agreement. (" + breakdown + ")",
                    MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            if (verdict == "counter")
            {
                Messages.Message(target.name + " would consider the "
                    + kind + " agreement - " + counterHint + ". ("
                    + breakdown + ")", MessageTypeDefOf.NeutralEvent,
                    false);
                return;
            }
            comp.AddAgreement(new CAAgreement
            {
                kind = kind,
                partyA = "player",
                partyB = target.organizationKey,
                brokerKey = broker != null ? broker.organizationKey : null,
                scopeKind = scopeKind,
                scopeValue = scopeValue,
                sunsetTick = days > 0
                    ? Find.TickManager.TicksGame + days * 60000 : -1,
                contributionsA = contribA,
                contributionsB = contribB,
                protectorKey = kind == "protection"
                    ? (direction == "they-protect"
                        ? target.organizationKey : "player")
                    : null,
                payerKey = kind == "tribute"
                    ? (direction == "they-pay"
                        ? target.organizationKey : "player")
                    : null,
                unauthorized = unauthorized
            });
            if (unauthorized)
            {
                var mine2 = comp.ActiveAgreementsInvolving("player");
                for (int i = 0; i < mine2.Count; i++)
                {
                    CAAgreement sz = mine2[i];
                    if (sz.kind != "protection" || sz.protectorKey == null
                        || sz.protectorKey == "player") continue;
                    CAOrganization protector = comp.ByKey(sz.protectorKey);
                    CABreachCase cc = comp.OpenBreachCase(
                        "seek consent for external agreements", "player",
                        sz.protectorKey, Find.TickManager.TicksGame, sz.id);
                    cc.kind = "protection";
                    cc.AddEvidence("signed with " + target.name
                        + " after consent was denied");
                    cc.stage = "closed";
                    cc.resolvedTick = Find.TickManager.TicksGame;
                    cc.resolution = "breach - unauthorized diplomacy";
                    protector?.Record("relations", colony.name
                        + " signed with " + target.name
                        + " without consent - the breach is recorded");
                    colony.Record("relations", "signed with " + target.name
                        + " without " + (protector?.name ?? "the protector")
                        + "'s consent - sanctions will follow");
                    Messages.Message((protector?.name ?? "The protector")
                        + " records the breach - support payment will be"
                        + " withheld while the unauthorized agreement"
                        + " stands.", MessageTypeDefOf.NegativeEvent,
                        false);
                    break;
                }
            }
            colony.Record("agreement-made", "agreement made with " + target.name
                + " - " + kind + ", scope " + scopeKind
                + (broker != null ? ", brokered by " + broker.name : "")
                + " [" + breakdown + "]");
            target.Record("agreement-made", "agreement made with " + colony.name
                + " - " + kind + ", scope " + scopeKind);
            if (broker != null)
                broker.Record("agreement-made", "brokered a " + kind
                    + " agreement between " + colony.name + " and "
                    + target.name);
            Messages.Message(target.name + " accepts the " + kind
                + " agreement" + (broker != null
                    ? " (mediated by " + broker.name + ")" : "") + ". ("
                + breakdown + ")", MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // Found a temporary defense federation from willing allied settlements.
    // Members share defense and the player names the federation.
    public class Dialog_CAFoundFederation : Window
    {
        private string name = "";
        private string sunsetDays = "15";

        public Dialog_CAFoundFederation()
        {
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(440f, 260f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, inRect.width, 30f),
                "Found defensive federation");
            Text.Font = GameFont.Small;
            y += 36f;
            Widgets.Label(new Rect(0f, y, 100f, 26f), "Name");
            name = Widgets.TextField(
                new Rect(104f, y, inRect.width - 108f, 26f), name);
            y += 30f;
            Widgets.Label(new Rect(0f, y, 100f, 26f), "Duration (days)");
            sunsetDays = Widgets.TextField(
                new Rect(104f, y, 80f, 26f), sunsetDays);
            y += 30f;
            List<CAOrganization> willing = EligibleMembers();
            GUI.color = Color.grey;
            Widgets.Label(new Rect(0f, y, inRect.width, 48f),
                willing.Count == 0
                    ? "No eligible members. A defense or protection "
                        + "agreement is required."
                    : "Eligible members: " + Names(willing));
            GUI.color = Color.white;
            y += 52f;
            if (willing.Count > 0 && !name.NullOrEmpty()
                && Widgets.ButtonText(new Rect(0f, y, 170f, 30f),
                    "Found federation"))
            {
                Found(willing);
                Close();
            }
        }

        private static string Names(List<CAOrganization> orgs)
        {
            var names = new List<string>();
            for (int i = 0; i < orgs.Count; i++) names.Add(orgs[i].name);
            return string.Join(", ", names.ToArray());
        }

        internal static List<CAOrganization> EligibleMembers()
        {
            // Membership uses the same willingness check as a defense
            // agreement.
            var result = new List<CAOrganization>();
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return result;
            CAOrganization colony = comp.EnsureColony();
            List<CAOrganization> neighbors =
                CAOrganizationStances.Neighbors(colony, comp);
            for (int i = 0; i < neighbors.Count; i++)
            {
                CAOrganization org = neighbors[i];
                if (!comp.HasActiveAgreement("player", org.organizationKey,
                    "defense", "protection")) continue;
                float score;
                string breakdown;
                string hint;
                CAOrganization broker;
                if (CAWillingness.Evaluate(colony, org, "defense",
                        "one campaign", 15, true, null, false, out score,
                        out breakdown, out hint, out broker) == "accepted")
                    result.Add(org);
            }
            return result;
        }

        private void Found(List<CAOrganization> willing)
        {
            CAOrganizationWorldComponent comp =
                CAOrganizationWorldComponent.Current;
            if (comp == null) return;
            CAOrganization colony = comp.EnsureColony();
            int days = 15;
            int.TryParse(sunsetDays, out days);
            int now = Find.TickManager.TicksGame;
            CAOrganization fed = comp.EnsureFor(
                "federation:" + now, name,
                "temporary defense federation",
                CAOrganizationKind.Federation);
            if (days > 0) fed.sunsetTick = now + days * 60000;
            // Each member relation records the responsibilities it shares.
            var sharedResponsibilities = new[]
                { CAResponsibilities.Defense };
            CAOrigin federationOrigin = CAOrigin.Authored(
                "federation:" + fed.organizationKey);
            CAFederation.Admit(fed, "player", sharedResponsibilities,
                fed.sunsetTick, federationOrigin);
            for (int i = 0; i < willing.Count; i++)
                CAFederation.Admit(fed, willing[i].organizationKey,
                    sharedResponsibilities, fed.sunsetTick, federationOrigin);
            fed.Record("agreement-made", "federation founded - " + name + ", "
                + CAFederation.MemberKeys(fed).Count
                + " members, defense shared"
                + (days > 0 ? ", sunset in " + days + " days" : ""));
            colony.Record("agreement-made", "founded federation " + name);
            for (int i = 0; i < willing.Count; i++)
                willing[i].Record("agreement-made", "joined federation " + name);
            Messages.Message("Federation founded: " + name + " ("
                + CAFederation.MemberKeys(fed).Count + " members).",
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
