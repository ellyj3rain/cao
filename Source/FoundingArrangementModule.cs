using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Creates the colony's initial ownership, work, decision, and membership
    // records. An authored arrangement wins; otherwise the scenario, faction
    // structure, and political beliefs determine the starting choice.
    [HarmonyPatch(typeof(GameComponentUtility),
        nameof(GameComponentUtility.StartedNewGame))]
    internal static class CAFoundingArrangementPatch
    {
        private static void Postfix()
        {
            try { CAFoundingArrangementRuntime.OnNewGame(); }
            catch (System.Exception e)
            {
                Log.Warning("[CA] founding arrangement failed: " + e);
            }
        }
    }

    // A founding arrangement states who leads, who must work, who decides,
    // how supplies are held, and whether those terms expire. The scenario
    // selects a default but does not silently establish those rules.
    public sealed class CAFoundingArrangement : IExposable
    {
        public string id;
        public string label;
        public string premise;

        public string leaderRule = "none";
        public bool workRequired;
        public bool foundersDecide = true;
        public bool sharedSupplies = true;
        public int durationDays = -1;

        public string OriginKey(string scenario)
        {
            return "founding:" + id + ":" + scenario;
        }

        public CAFoundingArrangement Copy()
        {
            return new CAFoundingArrangement
            {
                id = id,
                label = label,
                premise = premise,
                leaderRule = leaderRule,
                workRequired = workRequired,
                foundersDecide = foundersDecide,
                sharedSupplies = sharedSupplies,
                durationDays = durationDays
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref premise, "premise");
            Scribe_Values.Look(ref leaderRule, "leaderRule", "none");
            Scribe_Values.Look(ref workRequired, "workRequired", false);
            Scribe_Values.Look(ref foundersDecide, "foundersDecide", true);
            Scribe_Values.Look(ref sharedSupplies, "sharedSupplies", true);
            Scribe_Values.Look(ref durationDays, "durationDays", -1);
        }

        // Generated from the saved fields so the description matches the
        // arrangement that will be applied.
        public string Consequence(int founders)
        {
            var s = new System.Text.StringBuilder();
            s.Append(leaderRule == "none"
                ? "No permanent leader."
                : "One " + leaderRule + " founder leads.");
            s.Append(workRequired
                ? " Founders work and defend the settlement when ordered."
                : " Work is voluntary.");
            s.Append(foundersDecide
                ? " All " + founders + " founders take part in decisions."
                : leaderRule == "none"
                    ? " No decision rule is set."
                    : " The leader decides.");
            s.Append(sharedSupplies
                ? " Starting supplies are shared and rationed."
                : " Each founder keeps their own supplies.");
            s.Append(durationDays > 0
                ? " These terms last " + durationDays + " days."
                : " These terms do not expire.");
            return s.ToString();
        }
    }

    public static class CAFoundingArrangements
    {
        // Default for a group that arrives after a disaster.
        public static readonly CAFoundingArrangement SharedSurvival =
            new CAFoundingArrangement
            {
                id = "shared-survival",
                label = "shared survival",
                premise = "The survivors share supplies and decide together.",
                leaderRule = "none",
                workRequired = false,
                foundersDecide = true,
                sharedSupplies = true,
                durationDays = 30
            };

        public static readonly CAFoundingArrangement EmergencyCommand =
            new CAFoundingArrangement
            {
                id = "emergency-command",
                label = "emergency command",
                premise = "One founder leads until the emergency passes.",
                leaderRule = "chosen",
                workRequired = true,
                foundersDecide = false,
                sharedSupplies = true,
                durationDays = 30
            };

        public static readonly CAFoundingArrangement AncestralCommons =
            new CAFoundingArrangement
            {
                id = "ancestral-commons",
                label = "ancestral commons",
                premise = "The group carries inherited customs into the "
                    + "rules adopted at landing.",
                leaderRule = "none",
                workRequired = false,
                foundersDecide = true,
                sharedSupplies = true,
                durationDays = -1
            };

        public static readonly CAFoundingArrangement SingleFounder =
            new CAFoundingArrangement
            {
                id = "single-founder",
                label = "single founder",
                premise = "One founder owns the settlement and its supplies.",
                leaderRule = "none",
                workRequired = false,
                foundersDecide = true,
                sharedSupplies = false,
                durationDays = -1
            };

        public static readonly CAFoundingArrangement[] All =
        {
            SharedSurvival, EmergencyCommand, AncestralCommons,
            SingleFounder
        };

        // Select the initial choice from the scenario. The setup surface may
        // replace it with an authored arrangement.
        public static CAFoundingArrangement DefaultFor(bool alone,
            bool ancestral)
        {
            if (alone) return SingleFounder;
            if (ancestral) return AncestralCommons;
            return SharedSurvival;
        }
    }

    internal static class CAFoundingArrangementRuntime
    {
        private const int Day = 60000;

        internal static void OnNewGame()
        {
            CAPlayerFoundingWorldComponent foundingOwner =
                CAPlayerFoundingWorldComponent.Current;
            if (foundingOwner?.Applied == true)
            {
                Log.Message("[CA][Founding] founding state was already "
                    + "applied at tick " + foundingOwner.AppliedAtTick
                    + "; replay skipped");
                return;
            }
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            CAOrganizationWorldComponent orgs =
                CAOrganizationWorldComponent.Current;
            if (ledger == null || orgs == null) return;
            CAOrganization colony = orgs.EnsureColony();
            if (colony == null) return;

            Map map = Find.CurrentMap;
            List<Pawn> founders = map?.mapPawns?.FreeColonistsSpawned
                ?.ToList() ?? new List<Pawn>();
            if (founders.Count == 0) return;

            string scenario = Find.Scenario?.name ?? "unknown";
            int now = Find.TickManager.TicksGame;

            // The founders carry culture and political beliefs. Their wider
            // faction structure remains empty until institutions actually
            // develop; only the exact arrangement below is instituted now.
            CAPlayerFoundingPlan founding = CAPlayerFoundingSession
                .ConfirmedForRuntime();
            CAPlayerFoundingModel.ApplyCarriedState(founding,
                Faction.OfPlayer);
            CACultureLongitudinalMapComponent.For(map)
                ?.EstablishPlayerCulture(founding, now);

            // Scenario facts used to choose a default arrangement.
            bool fellFromSky = ArrivedViolently();
            bool alone = founders.Count == 1;
            bool ancestral = !fellFromSky && founders.Count >= 4
                && (Faction.OfPlayer?.def?.techLevel
                    ?? TechLevel.Industrial) <= TechLevel.Neolithic;

            // Use the authored arrangement when present. Otherwise adjust the
            // scenario default using the current faction state.
            CAFoundingArrangement preset = null;
            bool arrangementAuthored = false;
            if (founding?.arrangement != null && founding.confirmed)
            {
                preset = founding.arrangement;
                arrangementAuthored = founding.ArrangementSource
                    == CAAxisSource.Authored;
            }
            if (preset == null)
                preset = CAPoliticalBeliefPractice.ShapeDefault(
                    CAPoliticalBeliefPractice.PoliticalBeliefsOf(
                        Faction.OfPlayer),
                    CAFactionStateWorldComponent.Current?.Find(Faction.OfPlayer)
                        ?.factionStructure,
                    CAFoundingArrangements.DefaultFor(alone, ancestral));
            string origin = preset.OriginKey(scenario);

            ApplyArrangement(ledger, colony, founders, preset, origin,
                arrangementAuthored, fellFromSky, now);

            DirectedNonPersons(ledger, colony, map, origin, now);
            CAPoliticalBeliefs playerBeliefs = CAPoliticalBeliefPractice
                .PoliticalBeliefsOf(Faction.OfPlayer);

            if (founding != null)
                colony.Record("founding", "Culture: "
                    + (founding.culture?.name ?? "not recorded")
                    + ". Ideoligion: "
                    + (founding.nativeIdeoName ?? "not active")
                    + ". Political beliefs: "
                    + CAPoliticalBeliefsModel.Summary(playerBeliefs) + ".");

            // Record the applied arrangement and its effect.
            colony.Record("organization", "founding terms: "
                + preset.label + " - " + preset.premise);

            // Record only differences between the starting arrangement and
            // the faction's political beliefs. The founders, Ideoligion, and
            // site are all final at this point.
            foreach (CAPoliticalBeliefPractice.CAFoundingBeliefReading axis
                in CAPoliticalBeliefPractice.ReadAgainstPoliticalBeliefs(
                    playerBeliefs, preset))
            {
                if (axis.Silent || axis.conforms) continue;
                colony.Record("organization", "the founding terms"
                    + " differs from"
                    + " political beliefs on " + axis.title.ToLower()
                    + ": they hold " + axis.belief
                    + ", and landed under " + axis.adopted);
            }
            Log.Message("[CA][Founding] preset '" + preset.id
                + "' applied to scenario '" + scenario + "': "
                + preset.Consequence(founders.Count)
                + " | relation ledger now holds "
                + ledger.Relations.Count + " records.");
            foundingOwner?.MarkApplied(now);
        }

        private static bool ArrivedViolently()
        {
            if (Find.Scenario == null) return false;
            foreach (ScenPart part in Find.Scenario.AllParts)
            {
                var arrive = part as ScenPart_PlayerPawnsArriveMethod;
                if (arrive == null) continue;
                var method = Traverse.Create(arrive).Field("method")
                    .GetValue<PlayerPawnsArriveMethod>();
                if (method != PlayerPawnsArriveMethod.Standing)
                    return true;
            }
            return false;
        }

        // Apply the selected arrangement to every founder. Starting provisions
        // and facilities are materialized by their own saved parameters.
        private static void ApplyArrangement(
            CAOrganizationRelationsWorldComponent ledger,
            CAOrganization colony, List<Pawn> founders,
            CAFoundingArrangement preset, string origin,
            bool arrangementAuthored, bool fellFromSky, int now)
        {
            int sunset = preset.durationDays > 0
                ? now + preset.durationDays * Day : -1;

            // Until setup supports choosing a specific leader, a leader-based
            // arrangement assigns the first founder without a skill ranking.
            Pawn commander = preset.leaderRule == "none"
                ? null
                : founders[0];
            string memberStatus = sunset >= 0
                ? "temporary member" : "member";
            CAOrigin termsOrigin = arrangementAuthored
                ? CAOrigin.Authored(origin) : CAOrigin.Derived(origin);

            foreach (Pawn p in founders)
            {
                bool leads = commander != null && p == commander;
                if (!colony.memberPawnIds.Contains(p.thingIDNumber))
                    colony.memberPawnIds.Add(p.thingIDNumber);
                ledger.Add(new CARelation
                {
                    partyKind = CARelationPartyKind.Pawn,
                    partyPawnId = p.thingIDNumber,
                    orgKey = colony.organizationKey,
                    role = leads ? "leader" : "founder",
                    status = memberStatus,
                    delegatedResponsibilities = DelegatedResponsibilities(preset),
                    retainedResponsibilities = leads
                        ? new List<string>
                        {
                            CAResponsibilities.Decisions, CAResponsibilities.ArmedForce,
                            CAResponsibilities.EmergencyTaking, CAResponsibilities.Diplomacy
                        }
                        : commander == null || preset.foundersDecide
                            ? new List<string> { CAResponsibilities.Decisions }
                            : new List<string>(),
                    compensation = preset.sharedSupplies
                        ? CACompensationKinds.Ration
                        : CACompensationKinds.Keep,
                    voice = preset.foundersDecide ? CAVoiceKinds.Vote
                        : leads ? CAVoiceKinds.Veto : CAVoiceKinds.None,
                    protection = CAProtectionKinds.OrganizationRule,
                    entry = CAEntryKinds.Free,
                    exit = CAExitKinds.Free,
                    startTick = now,
                    sunsetTick = sunset,
                    origin = CAOrigin.Derived(origin),
                    termsOrigin = termsOrigin
                });
            }

            colony.Record("organization", "founding terms: "
                + (commander != null
                    ? commander.LabelShort + " commands"
                    : "no permanent leader")
                + ", " + preset.Consequence(founders.Count)
                + (fellFromSky
                    ? " The region saw us come down." : ""));
        }

        // What a founder hands over depends on the arrangement. Required work
        // exists only where the arrangement says it does; shared supplies are
        // a separate question from whether anyone may be ordered to work.
        private static List<string> DelegatedResponsibilities(
            CAFoundingArrangement preset)
        {
            var delegated = new List<string>();
            if (preset.sharedSupplies) delegated.Add(CAResponsibilities.Production);
            if (preset.workRequired)
            {
                delegated.Add(CAResponsibilities.Work);
                delegated.Add(CAResponsibilities.Defense);
            }
            return delegated;
        }

        // Player-controlled mechanoids work under direction and have no
        // membership, compensation, or right to leave.
        private static void DirectedNonPersons(
            CAOrganizationRelationsWorldComponent ledger,
            CAOrganization colony, Map map, string origin, int now)
        {
            if (map?.mapPawns == null) return;
            List<Pawn> mechs = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p.Faction == Faction.OfPlayer
                    && p.RaceProps != null && p.RaceProps.IsMechanoid)
                .ToList();
            if (mechs.Count == 0) return;
            foreach (Pawn m in mechs)
                ledger.Add(new CARelation
                {
                    partyKind = CARelationPartyKind.Pawn,
                    partyPawnId = m.thingIDNumber,
                    orgKey = colony.organizationKey,
                    role = "directed",
                    status = "chattel",
                    delegatedResponsibilities = new List<string>
                    {
                        CAResponsibilities.Work, CAResponsibilities.Production,
                        CAResponsibilities.Property, CAResponsibilities.Defense
                    },
                    retainedResponsibilities = new List<string>(),
                    compensation = CACompensationKinds.None,
                    voice = CAVoiceKinds.None,
                    protection = CAProtectionKinds.None,
                    entry = CAEntryKinds.Appointment,
                    exit = CAExitKinds.Forbidden,
                    startTick = now,
                    origin = CAOrigin.Derived(origin)
                });
            colony.Record("organization", mechs.Count
                + " directed workers that own nothing and cannot leave");
        }
    }
}
