using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // CA state attached directly to a native RimWorld faction. Inherited
    // Culture, political beliefs, and current faction structure remain
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
            if (culture == null) culture = new CACulture();
            if (politicalBeliefs == null)
                politicalBeliefs = new CAPoliticalBeliefs();
            if (factionStructure == null)
                factionStructure = new List<CAAxisEntry>();
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
        private int authoringDataEpoch = CAAuthoringDataEpoch.Current;
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
            Scribe_Values.Look(ref authoringDataEpoch,
                "CA_authoringDataEpoch", 0);
            bool current = Scribe.mode == LoadSaveMode.Saving
                || CAAuthoringDataEpoch.IsCurrent(authoringDataEpoch);
            if (current)
                Scribe_Collections.Look(ref factionStates,
                    "CA_factionStates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !current)
            {
                factionStates = new List<CAFactionState>();
                authoringDataEpoch = CAAuthoringDataEpoch.Current;
                CAAuthoringDataEpoch.RecordDiscard("faction authoring");
            }
            if (factionStates == null)
                factionStates = new List<CAFactionState>();
            base.ExposeData();
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
    // positions with real scored causes, and completes current structure for
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
            int cultures = 0, beliefSets = 0, beliefFields = 0;
            int structureFields = 0, skipped = 0;

            foreach (Faction faction in Find.FactionManager
                         .AllFactionsListForReading)
            {
                if (!UsesFactionState(faction)) { skipped++; continue; }
                Ideo ideo = faction.ideos?.PrimaryIdeo;
                CAFactionState record = store.EnsureFor(faction);
                string seed = (Find.World?.info?.persistentRandomValue ?? 0)
                    + ":" + (faction.def?.defName ?? "none") + ":"
                    + faction.loadID;

                bool cultureMissing = record.culture == null
                    || record.culture.id.NullOrEmpty();
                if (record.culture == null) record.culture = new CACulture();
                CACultureModel.EnsureGenerated(record.culture, seed,
                    CAFactionStartingState.DefaultCulture(faction, ideo));
                bool cultureGenerated = CACultureInitialState.EnsureForFaction(
                    record.culture, faction);
                if (cultureMissing || cultureGenerated) cultures++;

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
                    structureFields += CAFactionStructureModel
                        .GenerateEstablishedUnset(record.factionStructure,
                            record.politicalBeliefs, seed + ":structure");
                if (record.generatedAtTick < 0)
                {
                    record.generatedAtTick = GenTicks.TicksAbs;
                    if (!record.AnyAuthoredState)
                        record.origin = CAOrigin.Derived(reason);
                }
            }

            return "[CA][Faction] setup pass (" + reason + "): "
                + cultures + " Cultures, " + beliefSets
                + " political-belief sets, " + beliefFields
                + " political-belief fields, " + structureFields
                + " faction-structure fields generated; " + skipped
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
