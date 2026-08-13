using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // CA state attached directly to a native RimWorld faction. Inherited
    // Culture, political beliefs, and the current order remain
    // separate from the faction's native Ideoligion.
    public sealed class CAFactionState : IExposable
    {
        public int factionLoadId = -1;
        public string factionName;
        public string engineTemplateDefName;
        public CACulture culture = new CACulture();
        public CAPoliticalBeliefs politicalBeliefs =
            new CAPoliticalBeliefs();
        public List<CAAxisEntry> factionStructure = new List<CAAxisEntry>();
        // An explicit unknown-institution boundary is itself durable state.
        // Later setup passes must not turn missing evidence into institutions.
        public bool institutionalStateIncomplete;
        public CAOrigin origin = CAOrigin.Derived("ungenerated");
        public int generatedAtTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionLoadId, "factionLoadId", -1);
            Scribe_Values.Look(ref factionName, "factionName");
            Scribe_Values.Look(ref engineTemplateDefName,
                "engineTemplateDefName");
            Scribe_Deep.Look(ref culture, "culture");
            Scribe_Deep.Look(ref politicalBeliefs, "politicalBeliefs");
            Scribe_Collections.Look(ref factionStructure, "factionStructure",
                LookMode.Deep);
            Scribe_Values.Look(ref institutionalStateIncomplete,
                "institutionalStateIncomplete", false);
            origin.Expose("origin");
            Scribe_Values.Look(ref generatedAtTick, "generatedAtTick", -1);
        }

        internal Faction Faction
        {
            get
            {
                if (factionLoadId < 0 || Find.FactionManager == null)
                    return null;
                foreach (Faction faction in Find.FactionManager
                             .AllFactionsListForReading)
                    if (faction != null && faction.loadID == factionLoadId)
                        return faction;
                return null;
            }
        }

        internal bool AnyAuthoredState
        {
            get
            {
                if (culture?.authoredMask != 0) return true;
                if (politicalBeliefs?.positions != null
                    && politicalBeliefs.positions.Any(entry => entry != null
                        && entry.source
                            == (byte)CAAxisSource.Authored)) return true;
                return factionStructure != null
                    && factionStructure.Any(entry => entry != null
                        && entry.source == (byte)CAAxisSource.Authored);
            }
        }
    }

    public sealed class CAFactionStateWorldComponent : WorldComponent
    {
        private int campaignSchemaVersion = 2;
        private int legacyAuthoringDataEpoch =
            CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch;
        private List<CAFactionState> factionStates =
            new List<CAFactionState>();

        public CAFactionStateWorldComponent(World world) : base(world) { }

        internal static CAFactionStateWorldComponent Current
        {
            get { return Verse.Find.World
                ?.GetComponent<CAFactionStateWorldComponent>(); }
        }

        internal IReadOnlyList<CAFactionState> FactionStates
        {
            get { return factionStates; }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_factionStateSchemaVersion", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyAuthoringDataEpoch,
                    "CA_authoringDataEpoch", 0);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "world.faction-state", campaignSchemaVersion,
                legacyAuthoringDataEpoch);
            if (readable)
                Scribe_Collections.Look(ref factionStates,
                    "CA_factionStates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.faction-state", ref campaignSchemaVersion,
                    legacyAuthoringDataEpoch, ValidateCampaignState,
                    MigrateB10State);
            }
            base.ExposeData();
        }

        private string ValidateCampaignState()
        {
            string identityFailure = ValidateOwnerIdentities();
            if (!identityFailure.NullOrEmpty()) return identityFailure;
            for (int i = 0; i < factionStates.Count; i++)
            {
                CAFactionState state = factionStates[i];
                if (state.culture == null || state.politicalBeliefs == null
                    || state.factionStructure == null)
                    return "native faction " + state.factionLoadId
                        + " has incomplete social state";
                if (state.culture.schemaVersion != CACulture.CurrentSchemaVersion)
                    return "native faction " + state.factionLoadId
                        + " has Culture schema " + state.culture.schemaVersion;
                string cultureFailure = CACultureModel.ValidationFailure(
                    state.culture, requireSubstantive: true);
                if (!cultureFailure.NullOrEmpty())
                    return "native faction " + state.factionLoadId
                        + " has invalid Culture: " + cultureFailure;
                if (state.politicalBeliefs.schemaVersion
                    != CAPoliticalBeliefs.CurrentSchemaVersion)
                    return "native faction " + state.factionLoadId
                        + " has political-belief schema "
                        + state.politicalBeliefs.schemaVersion;
                string beliefFailure = CAPoliticalBeliefsModel
                    .ValidationFailure(state.politicalBeliefs,
                        allowExactLegacy: false);
                if (!beliefFailure.NullOrEmpty())
                    return "native faction " + state.factionLoadId
                        + " has unsupported political beliefs: "
                        + beliefFailure;
                string orderFailure = CAPoliticalBeliefsModel
                    .ValidationFailure(state.factionStructure);
                if (!orderFailure.NullOrEmpty())
                    return "native faction " + state.factionLoadId
                        + " has unsupported current order: " + orderFailure;
            }
            return null;
        }

        private string ValidateOwnerIdentities()
        {
            if (factionStates == null)
                return "faction-state owner collection is missing";
            var ids = new HashSet<int>();
            for (int i = 0; i < factionStates.Count; i++)
            {
                CAFactionState state = factionStates[i];
                if (state == null) return "faction state " + i + " is null";
                if (state.factionLoadId < 0)
                    return "faction state " + i + " has no native faction id";
                if (!ids.Add(state.factionLoadId))
                    return "native faction " + state.factionLoadId
                        + " has duplicate CA state";
            }
            return null;
        }

        private string MigrateB10State()
        {
            string identityFailure = ValidateOwnerIdentities();
            if (!identityFailure.NullOrEmpty()) return identityFailure;
            var candidates = new List<CAFactionState>();
            for (int i = 0; i < factionStates.Count; i++)
            {
                CAFactionState state = factionStates[i];
                if (state == null) return "faction state " + i + " is null";
                if (!CACultureModel.TryUpgradeFromB10(state.culture,
                        out CACulture culture, out string cultureFailure))
                    return "faction " + state.factionLoadId + " Culture: "
                        + cultureFailure;
                if (!CAPoliticalBeliefsModel.TryUpgradeFromB10(
                        state.politicalBeliefs, out CAPoliticalBeliefs beliefs,
                        out string beliefFailure))
                    return "faction " + state.factionLoadId
                        + " political beliefs: " + beliefFailure;
                if (!CAPoliticalBeliefsModel.TryUpgradeMechanismsFromB10(
                        state.factionStructure, out List<CAAxisEntry> order,
                        out string orderFailure))
                    return "faction " + state.factionLoadId
                        + " current order: " + orderFailure;
                candidates.Add(new CAFactionState
                {
                    factionLoadId = state.factionLoadId,
                    factionName = state.factionName,
                    engineTemplateDefName = state.engineTemplateDefName,
                    culture = culture,
                    politicalBeliefs = beliefs,
                    factionStructure = order,
                    institutionalStateIncomplete =
                        state.institutionalStateIncomplete,
                    origin = state.origin,
                    generatedAtTick = state.generatedAtTick
                });
            }
            List<CAFactionState> prior = factionStates;
            factionStates = candidates;
            string candidateFailure = ValidateCampaignState();
            factionStates = prior;
            if (!candidateFailure.NullOrEmpty()) return candidateFailure;
            factionStates = candidates;
            return null;
        }

        internal CAFactionState Find(Faction faction)
        {
            if (faction == null) return null;
            return factionStates.FirstOrDefault(record => record != null
                && record.factionLoadId == faction.loadID);
        }

        internal CAFactionState EnsureFor(Faction faction)
        {
            if (faction == null) return null;
            CAFactionState found = Find(faction);
            if (found != null)
            {
                found.factionName = faction.Name;
                found.engineTemplateDefName = faction.def?.defName;
                return found;
            }
            var record = new CAFactionState
            {
                factionLoadId = faction.loadID,
                factionName = faction.Name,
                engineTemplateDefName = faction.def?.defName
            };
            factionStates.Add(record);
            return record;
        }

        internal void Remove(CAFactionState record)
        {
            if (record != null) factionStates.Remove(record);
        }

        internal int PruneOrphans()
        {
            return factionStates.RemoveAll(record => record == null
                || record.Faction == null);
        }
    }

    // Establishes evidence-backed inherited Culture, derives only political
    // positions with real scored causes, and completes the current order for
    // every humanlike faction. Native Ideoligion remains untouched.
    internal static class CAFactionStateGenerator
    {
        internal static string RunWorldPass(CARegionalWorldPolicy policy,
            string reason)
        {
            CAFactionStateWorldComponent store =
                CAFactionStateWorldComponent.Current;
            if (store == null || Find.FactionManager == null)
                return "[CA][Faction] no world - faction setup skipped";

            store.PruneOrphans();
            int cultures = 0, cultureQuestions = 0, beliefSets = 0,
                beliefFields = 0;
            int structureFields = 0, skipped = 0;

            foreach (Faction faction in Find.FactionManager
                         .AllFactionsListForReading)
            {
                if (!UsesFactionState(faction)) { skipped++; continue; }
                Ideo ideo = faction.ideos?.PrimaryIdeo;
                CAFactionState record = store.EnsureFor(faction);
                string seed = CAPlayerFoundingSession.WorldIdentity()
                    + ":faction:" + (faction.def?.defName ?? "none") + ":"
                    + faction.loadID;

                bool cultureMissing = record.culture == null
                    || record.culture.id.NullOrEmpty();
                if (record.culture == null) record.culture = new CACulture();
                CACultureModel.EnsureIdentity(record.culture, seed);
                bool cultureGenerated = CACultureInitialState.EnsureForFaction(
                    record.culture, faction);
                if (cultureMissing || cultureGenerated) cultures++;
                if (!faction.IsPlayer)
                    cultureQuestions += CACultureAuthoringKernel
                        .CompleteMissing(record.culture, seed + ":culture",
                            "world faction generation");

                if (record.politicalBeliefs == null)
                    record.politicalBeliefs = new CAPoliticalBeliefs();
                bool beliefsMissing = record.politicalBeliefs.positions == null
                    || record.politicalBeliefs.positions.Count == 0;
                CAPoliticalBeliefsModel.Ensure(record.politicalBeliefs, seed);
                int filled = CAPoliticalBeliefsModel.DeriveUnset(
                    record.politicalBeliefs, seed + ":beliefs",
                    CAPoliticalContext.ForFaction(faction, record.culture,
                        record.factionStructure));
                if (beliefsMissing) beliefSets++;
                beliefFields += filled;

                // An established faction begins with a realized structure.
                // The player faction begins with only the arrangement adopted
                // at founding; its remaining institutions emerge through play.
                if (!faction.IsPlayer && !record.institutionalStateIncomplete)
                {
                    structureFields += CAFactionStructureModel
                        .PreserveEstablishedUnset(record.factionStructure);
                    if (CAFactionAxes.Axes.Any(axis => CAFactionAxes.StateOf(
                            record.factionStructure, axis.Key)
                            == CAAxisSource.Unset))
                        record.institutionalStateIncomplete = true;
                }
                if (record.generatedAtTick < 0)
                {
                    record.generatedAtTick = GenTicks.TicksAbs;
                    if (!record.AnyAuthoredState)
                        record.origin = CAOrigin.Derived(reason);
                }
            }

            return "[CA][Faction] setup pass (" + reason + "): "
                + cultures + " Culture identities, " + cultureQuestions
                + " Culture questions, " + beliefSets
                + " political-belief sets, " + beliefFields
                + " political-belief fields, " + structureFields
                + " current-order mechanisms generated; " + skipped
                + " non-humanlike factions skipped; unsupported political "
                + "fields remain unset; Ideoligions unchanged";
        }

        internal static bool UsesFactionState(Faction faction)
        {
            return faction?.def != null && !faction.temporary
                && faction.def.humanlikeFaction;
        }
    }
}
