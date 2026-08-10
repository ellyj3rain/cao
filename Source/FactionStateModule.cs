using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // CA state attached directly to a native RimWorld faction. Culture,
    // political beliefs, and current faction structure remain separate from
    // the faction's native Ideoligion.
    public sealed class CAFactionState : IExposable
    {
        public int factionLoadId = -1;
        public string factionName;
        public string engineTemplateDefName;
        public CACulture culture = new CACulture();
        public CAPoliticalBeliefs politicalBeliefs =
            new CAPoliticalBeliefs();
        public List<CAAxisEntry> factionStructure = new List<CAAxisEntry>();
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
            Scribe_Collections.Look(ref factionStates, "CA_factionStates",
                LookMode.Deep);
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

    // Completes culture, political beliefs, and current structure for every
    // humanlike faction. Native Ideoligion remains untouched.
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
                if (cultureMissing) cultures++;

                if (record.politicalBeliefs == null)
                    record.politicalBeliefs = new CAPoliticalBeliefs();
                bool beliefsMissing = record.politicalBeliefs.positions == null
                    || record.politicalBeliefs.positions.Count == 0;
                CAPoliticalBeliefsModel.Ensure(record.politicalBeliefs, seed);
                int filled = CAPoliticalBeliefsModel.GenerateUnset(
                    record.politicalBeliefs, seed + ":beliefs");
                if (beliefsMissing) beliefSets++;
                beliefFields += filled;

                // An established faction begins with a realized structure.
                // The player faction begins with only the arrangement adopted
                // at founding; its remaining institutions emerge through play.
                if (!faction.IsPlayer)
                    structureFields += CAFactionStructureModel.GenerateUnset(
                        record.factionStructure, record.politicalBeliefs,
                        seed + ":structure");
                if (record.generatedAtTick < 0)
                {
                    record.generatedAtTick = GenTicks.TicksAbs;
                    if (!record.AnyAuthoredState)
                        record.origin = CAOrigin.Derived(reason);
                }
            }

            return "[CA][Faction] setup pass (" + reason + "): "
                + cultures + " cultures, " + beliefSets
                + " political-belief sets, " + beliefFields
                + " political-belief fields, " + structureFields
                + " faction-structure fields generated; " + skipped
                + " non-humanlike factions skipped; Ideoligions unchanged";
        }

        internal static bool UsesFactionState(Faction faction)
        {
            return faction?.def != null && !faction.temporary
                && faction.def.humanlikeFaction;
        }
    }
}
