using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // WHO HOLDS THE LAND, REMEMBERED. The political arrangement of a
    // partition region - unclaimed, held, divided, contested - was until
    // now recomputed from whoever happened to be standing there each time
    // anyone looked, which meant the world had arrangements but no memory
    // of them. This ledger persists the holder set per multi-area region
    // and appends a dated event whenever it changes, so a region's
    // political life becomes history the player can read: who first held
    // it, who raised a settlement into it, who lost their last one.
    // Derived from settlements actually standing - never authored, never
    // implied by arrival or membership.
    public sealed class CARegionalPoliticalEvent : IExposable
    {
        public int absTick;
        public string text;

        public void ExposeData()
        {
            Scribe_Values.Look(ref absTick, "absTick", 0);
            Scribe_Values.Look(ref text, "text");
        }
    }

    public sealed class CARegionalPoliticalRecord : IExposable
    {
        public string regionId;
        public List<string> holderFactionKeys = new List<string>();
        public List<string> holderNames = new List<string>();
        public bool contested;
        public List<CARegionalPoliticalEvent> events =
            new List<CARegionalPoliticalEvent>();

        private const int EventCap = 40;

        public void ExposeData()
        {
            Scribe_Values.Look(ref regionId, "regionId");
            Scribe_Collections.Look(ref holderFactionKeys,
                "holderFactionKeys", LookMode.Value);
            Scribe_Collections.Look(ref holderNames, "holderNames",
                LookMode.Value);
            Scribe_Values.Look(ref contested, "contested", false);
            Scribe_Collections.Look(ref events, "events", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                holderFactionKeys = holderFactionKeys
                    ?? new List<string>();
                holderNames = holderNames ?? new List<string>();
                events = events ?? new List<CARegionalPoliticalEvent>();
            }
        }

        internal string StatusLine
        {
            get
            {
                if (holderNames == null || holderNames.Count == 0)
                    return "unclaimed";
                if (holderNames.Count == 1)
                    return "held by " + holderNames[0];
                return (contested ? "contested among "
                        : "divided among ")
                    + holderNames.Count + " polities ("
                    + string.Join(", ", holderNames) + ")";
            }
        }

        internal void Append(int absTick, string text)
        {
            events.Add(new CARegionalPoliticalEvent
            {
                absTick = absTick,
                text = text
            });
            if (events.Count > EventCap)
                events.RemoveRange(0, events.Count - EventCap);
        }
    }

    internal static class CARegionalPoliticalLedger
    {
        // Recompute every multi-area region's holder set from the
        // settlements actually standing (engine settlements on member
        // tiles, the player's included, plus a covering registered
        // plan's authored settlements), diff against the persisted
        // record, and write dated events for the changes.
        internal static void Rebuild(CARegionalWorldComponent component,
            string reason)
        {
            if (component?.Topology == null
                || component.Topology.Count == 0) return;
            // During worldgen no TickManager exists yet; the founding
            // pass dates to the campaign's first moment.
            int absTick = Verse.Find.TickManager == null ? 0
                : GenTicks.TicksAbs;
            bool firstPass = component.PoliticalRecords.Count == 0;

            // One pass over the world's settlements, bucketed by region.
            var holdersByRegion =
                new Dictionary<string, List<Faction>>();
            List<Settlement> settlements =
                Verse.Find.WorldObjects?.Settlements;
            PlanetLayer surface = Verse.Find.WorldGrid?.Surface;
            foreach (Settlement settlement in settlements
                ?? new List<Settlement>())
            {
                if (settlement?.Faction == null
                    || settlement.Tile.Layer != surface) continue;
                CARegionalTopologyRecord record =
                    component.TopologyRecordAt(
                        settlement.Tile.tileId);
                if (record == null
                    || record.memberTileIds == null
                    || record.memberTileIds.Count < 2) continue;
                if (!holdersByRegion.TryGetValue(record.regionId,
                        out List<Faction> holders))
                    holdersByRegion[record.regionId] = holders =
                        new List<Faction>();
                if (!holders.Contains(settlement.Faction))
                    holders.Add(settlement.Faction);
            }

            int changes = 0;

            // ONE REGION, ONE WRITER. A plan that realized a partition
            // region ADOPTS that record's id, and CarveTopologyForRegion
            // deliberately leaves the record in place when the id already
            // matches - so the same regionId is reachable from both loops
            // below. Their holder sets differ (the plan absorbed the
            // world settlements that the topology bucket would count), so
            // letting both write flip-flopped the persisted record and
            // appended a false dated event on every sweep, forever. The
            // plan is the authority wherever one exists; the topology
            // loop skips whatever it claimed.
            var claimed = new HashSet<string>();
            foreach (CARegionalPlan plan in component.Regions
                ?? (IReadOnlyList<CARegionalPlan>)
                    new List<CARegionalPlan>())
            {
                if (plan?.settlements == null || plan.regionalId == null
                    || plan.memberTileIds == null
                    || plan.memberTileIds.Count < 2) continue;
                List<Faction> holders = plan.settlements
                    .Where(item => item != null)
                    .Select(item =>
                        plan.FactionPlan(item.OwningFactionKey)
                            ?.resolvedFaction)
                    .Where(faction => faction != null)
                    .Distinct().ToList();
                CARegionalPlan named = plan;
                if (!claimed.Add(plan.regionalId)) continue;
                if (DiffInto(component, plan.regionalId,
                        () => CARegionalPlanUtility.RegionName(named),
                        holders, absTick, firstPass))
                    changes++;
            }
            foreach (CARegionalTopologyRecord record in component.Topology)
            {
                if (record?.memberTileIds == null
                    || record.memberTileIds.Count < 2) continue;
                // A registered plan already wrote this region's history.
                if (claimed.Contains(record.regionId)) continue;
                holdersByRegion.TryGetValue(record.regionId,
                    out List<Faction> holders);
                // The region's name costs tile lookups, and this sweep
                // walks every region in the world daily; resolve it
                // only when an event is actually being written.
                CARegionalTopologyRecord named = record;
                if (DiffInto(component, record.regionId,
                        () => CARegionalGeography.TopologyName(named),
                        holders ?? new List<Faction>(), absTick,
                        firstPass))
                    changes++;
            }
            if (changes > 0)
                Log.Message("[CA][Political] " + changes
                    + " regional holding change(s) recorded at "
                    + reason);
        }

        // Diff one region's observed holders against its persisted
        // record; create the record on first sight, append a dated event
        // on change. Returns whether anything changed.
        private static bool DiffInto(CARegionalWorldComponent component,
            string regionId, Func<string> regionName,
            List<Faction> holders, int absTick, bool firstPass)
        {
            holders = holders.OrderBy(item => item.GetUniqueLoadID())
                .ToList();
            bool contested = IsContested(holders);
            var keys = holders.Select(item => item.GetUniqueLoadID())
                .ToList();
            CARegionalPoliticalRecord political =
                component.PoliticalRecordFor(regionId);
            if (political == null)
            {
                // Unclaimed land needs no record: "unclaimed" is the
                // absence of one. Persisting a row for every empty
                // region would add thousands of rows to every save for
                // state that is already implied.
                if (holders.Count == 0) return false;
                political = new CARegionalPoliticalRecord
                {
                    regionId = regionId,
                    holderFactionKeys = keys,
                    holderNames = holders.Select(item => item.Name)
                        .ToList(),
                    contested = contested
                };
                political.Append(absTick, regionName() + " begins "
                    + political.StatusLine + ".");
                component.AddPoliticalRecord(political);
                return !firstPass;
            }
            if (keys.SequenceEqual(political.holderFactionKeys)
                && contested == political.contested) return false;
            string change = ChangeText(regionName(), political, holders,
                contested);
            political.holderFactionKeys = keys;
            political.holderNames = holders
                .Select(item => item.Name).ToList();
            political.contested = contested;
            if (!change.NullOrEmpty())
                political.Append(absTick, change);
            return true;
        }

        private static bool IsContested(List<Faction> holders)
        {
            for (int i = 0; i < holders.Count; i++)
                for (int j = i + 1; j < holders.Count; j++)
                    if (holders[i].HostileTo(holders[j])) return true;
            return false;
        }

        private static string ChangeText(string name,
            CARegionalPoliticalRecord before, List<Faction> now,
            bool contested)
        {
            var beforeKeys = new HashSet<string>(
                before.holderFactionKeys);
            var nowKeys = new HashSet<string>(
                now.Select(item => item.GetUniqueLoadID()));
            List<string> arrived = now.Where(item =>
                    !beforeKeys.Contains(item.GetUniqueLoadID()))
                .Select(item => item.Name).ToList();
            List<string> departed = before.holderFactionKeys
                .Select((key, index) => nowKeys.Contains(key) ? null
                    : before.holderNames.ElementAtOrDefault(index))
                .Where(holder => !holder.NullOrEmpty()).ToList();
            var parts = new List<string>();
            if (arrived.Count > 0)
                parts.Add(string.Join(", ", arrived) + " settled into "
                    + name);
            if (departed.Count > 0)
                parts.Add(string.Join(", ", departed) + " no longer "
                    + "holds ground in " + name);
            if (parts.Count == 0 && contested != before.contested)
                parts.Add("relations in " + name + " turned "
                    + (contested ? "hostile" : "peaceable"));
            if (parts.Count == 0) return null;
            string status = now.Count == 0 ? "the land lies unclaimed"
                : now.Count == 1 ? "the land is held by " + now[0].Name
                : "the land is " + (contested ? "contested" : "divided")
                    + " among " + now.Count + " polities";
            return string.Join("; ", parts) + " — " + status + ".";
        }

        internal static string EventLine(CARegionalPoliticalEvent item)
        {
            if (item == null) return null;
            return GenDate.QuadrumDateStringAt(item.absTick, 0f)
                + " — " + item.text;
        }
    }
}
