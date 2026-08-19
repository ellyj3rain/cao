using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // ARCHITECTURAL APERTURES - the wall stops being a binary.
    //
    // Solid wall, open hole, shuttered hole, glazed window and defensive
    // slit are five different awareness and environment surfaces, and the
    // engine already distinguishes most of that from the defs alone:
    // blockLight false + fillPercent < 1 means sight and fire pass where
    // a wall would stop both, fillPercent IS the cover value (a slit
    // protects harder than an open hole), and partial fillage lets gas
    // and smoke propagate through the cell where full fillage stops
    // them. What the defs cannot carry lives here:
    //
    //   AIRFLOW   an open aperture (and an open shutter) equalizes
    //             temperature through the wall at the engine's own vent
    //             rate; glazing, slits and closed shutters do not.
    //   SHUTTER   open and closed are DIFFERENT DEFS, swapped in place
    //             with stuff, faction and damage preserved - so a closed
    //             shutter genuinely blocks engine light, sight, gas and
    //             cover the way a wall does, with no def-state mutation.
    //             (Prior art: OpenTheWindows toggles def.blockLight on
    //             the SHARED def at runtime - the known wart this
    //             per-instance swap avoids; ED-Embrasures proves the
    //             def-only slit.)
    //   HABIT     a settlement's people work their own shutters: closed
    //             for the night and against the cold, open with the day.
    //   AWARENESS AwarenessTransparency is the seam other CA systems
    //             consult - the audible-cue layer runs sound through it,
    //             so shuttering a window changes what is heard inside.
    public sealed class Building_CAAperture : Building
    {
        private const string ShutterOpenDef = "CA_ApertureShuttered";
        private const string ShutterClosedDef =
            "CA_ApertureShutteredClosed";

        private bool IsShuttered
        {
            get
            {
                return def.defName == ShutterOpenDef
                    || def.defName == ShutterClosedDef;
            }
        }

        private bool ShutterIsOpen
        {
            get { return def.defName == ShutterOpenDef; }
        }

        private bool AirflowNow
        {
            get
            {
                return def.defName == "CA_ApertureOpen"
                    || def.defName == ShutterOpenDef;
            }
        }

        // How much of what happens beyond this opening reaches awareness:
        // 1 is an open hole, a closed shutter is nearly a wall, glass
        // passes sight but muffles sound, a slit shows a sliver.
        public float AwarenessTransparency
        {
            get
            {
                switch (def.defName)
                {
                    case "CA_ApertureOpen": return 1f;
                    case ShutterOpenDef: return 0.9f;
                    case ShutterClosedDef: return 0.15f;
                    case "CA_WindowGlazed": return 0.7f;
                    case "CA_ApertureSlit": return 0.4f;
                    default: return 0f;
                }
            }
        }

        // The swap that IS the state change: destroy this building and
        // raise its counterpart def in the same cell with the same
        // stuff, faction and damage fraction. Everything the engine
        // derives from the def - light blocking, seen-over, cover, gas,
        // beauty - follows natively. `this` is dead afterwards; callers
        // must return immediately.
        public void SetShutter(bool open)
        {
            if (!IsShuttered || open == ShutterIsOpen || !Spawned) return;
            ThingDef target = DefDatabase<ThingDef>.GetNamedSilentFail(
                open ? ShutterOpenDef : ShutterClosedDef);
            if (target == null) return;
            Map map = Map;
            IntVec3 cell = Position;
            ThingDef stuff = Stuff;
            Faction faction = Faction;
            float damage = MaxHitPoints > 0
                ? (float)HitPoints / MaxHitPoints : 1f;
            Destroy(DestroyMode.Vanish);
            Thing next = ThingMaker.MakeThing(target,
                target.MadeFromStuff
                    ? stuff ?? GenStuff.DefaultStuffFor(target) : null);
            next.HitPoints = Mathf.Max(1,
                Mathf.RoundToInt(next.MaxHitPoints * damage));
            GenSpawn.Spawn(next, cell, map);
            if (faction != null) next.SetFaction(faction);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            CAApertureLightMapComponent.For(map)?.Dirty();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = Map;
            base.DeSpawn(mode);
            CAApertureLightMapComponent.For(map)?.Dirty();
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!Spawned) return;
            if (AirflowNow)
                GenTemperature.EqualizeTemperaturesThroughBuilding(this,
                    14f, true);

            // THE PEOPLE WORK THEIR OWN SHUTTERS. Non-player settlements
            // close for the night and against real cold, open with the
            // day - so a town seen at dusk genuinely darkens, quiets,
            // and holds its warmth. Player shutters stay the player's.
            if (IsShuttered && Faction != null
                && Faction != Faction.OfPlayerSilentFail)
            {
                int hour = GenLocalDate.HourInteger(Map);
                bool cold = Map.mapTemperature.OutdoorTemp < 2f;
                bool wantOpen = hour >= 7 && hour < 22 && !cold;
                if (wantOpen != ShutterIsOpen)
                {
                    SetShutter(wantOpen);
                    return; // this instance is destroyed
                }
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!IsShuttered || Faction != Faction.OfPlayerSilentFail)
                yield break;
            bool open = ShutterIsOpen;
            yield return new Command_Action
            {
                defaultLabel = open ? "Close shutter" : "Open shutter",
                defaultDesc = "Open, the aperture passes air, light, "
                    + "sight and sound. Closed, it is nearly a wall: "
                    + "warmth, light and awareness stay on their own "
                    + "sides.",
                icon = TexCommand.ForbidOff,
                action = delegate { SetShutter(!open); }
            };
        }

        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();
            string mine = IsShuttered
                ? (ShutterIsOpen ? "Shutter open." : "Shutter closed.")
                : null;
            return mine == null ? baseString
                : baseString.NullOrEmpty() ? mine
                    : baseString + "\n" + mine;
        }
    }

    // GENERATION CHOOSES APERTURES FROM ACTUAL FACTS - technology tier
    // (the era the walls were raised in), settlement form, the CLIMATE
    // the walls stand in, and WHO built and uses them - never
    // as a universal default. Runs after morphology has raised the walls.
    internal static class CAApertures
    {
        internal static void CutApertures(Map map,
            CARegionalSettlementRecord record)
        {
            try
            {
                if (map == null || record == null
                    || record.localRect == CellRect.Empty) return;
                ThingDef wall = ThingDefOf.Wall;
                // Initial settlement fabric is realized before its residents
                // exist on the map. Use the faction's canonical authored
                // capability here; local distributed availability governs
                // later operation and maintenance after carriers exist.
                int tier = CASettlementProgramMaterializer
                    .CanonicalTechTier(record);
                CAMorphForm form = CAMorphologyAdapter.FormFor(record);
                bool defensive = form == CAMorphForm.Outpost;

                // GENERATION INPUTS beyond the walls themselves:
                //   climate - the tile's own mean temperature: a cold
                //     country shutters or glazes what a warm one leaves
                //     open, and a hot one throws wider;
                //   the faction - a militarized faction (standing
                //     force or warrior caste) pierces its walls for
                //     fire, not air; a ruler-guarded seat keeps slits
                //     where an open faction glazes its hall;
                //   WEALTH - the settlement's own stored wealth band:
                //     glass follows money, and a poor town shutters
                //     what a rich one glazes;
                //   CONSTRUCTION HISTORY - the stored construction era: a
                //     town that grew through eras keeps old fabric, and
                //     an old wall carries the openings of the era that
                //     raised it, not of the town it ended up in.
                float climate = map.TileInfo.temperature;
                CAFactionState factionState = CAFactionStateWorldComponent.Current
                    ?.Find(record.faction);
                List<CAAxisEntry> currentOrder = record.faction == null
                    ? record.localSociety?.institutions
                    : factionState?.factionStructure;
                bool rulerGuard = CAFactionAxes.HasOption(currentOrder,
                    CAFactionAxes.LocalOrder, "rulers");
                bool pluralDissent = CAFactionAxes.HasOption(currentOrder,
                    CAFactionAxes.Dissent, "plural");
                bool militarized = CAFactionAxes.HasOption(currentOrder,
                        CAFactionAxes.Defense, "professional")
                    || CAFactionAxes.HasOption(currentOrder,
                        CAFactionAxes.Defense, "caste");
                bool cold = climate < 0f;
                bool hot = climate > 28f;
                int wealth = Math.Max(0, record.wealth);
                // How much fabric survives from the construction era: a town
                // still in that era is almost all old walls; one
                // that climbed since keeps roughly half.
                float oldFabric = record.constructionEra >= tier ? 0.85f
                    : record.constructionEra == tier - 1 ? 0.5f : 0.35f;

                // Candidate wall cells: this settlement's own walls with
                // a real interior on one side and the open air on the
                // other - the only places an aperture means anything.
                var candidates = new List<Thing>();
                foreach (IntVec3 cell in record.localRect.ExpandedBy(2))
                {
                    if (!cell.InBounds(map)) continue;
                    Building edifice = cell.GetEdifice(map);
                    if (edifice == null || edifice.def != wall
                        || edifice.Faction != record.faction) continue;
                    bool interior = false;
                    bool exterior = false;
                    foreach (IntVec3 side in GenAdj.CardinalDirections)
                    {
                        IntVec3 neighbor = cell + side;
                        if (!neighbor.InBounds(map)) continue;
                        Room room = neighbor.GetRoom(map);
                        if (room == null) continue;
                        if (room.PsychologicallyOutdoors) exterior = true;
                        else if (!room.IsDoorway) interior = true;
                    }
                    if (interior && exterior) candidates.Add(edifice);
                }
                if (candidates.Count == 0) return;

                // The largest interior room marks the settlement's chief
                // building - where standing shows, and where tier-1
                // glazing is materially justified if anywhere is.
                Room chief = null;
                foreach (Thing candidate in candidates)
                    foreach (IntVec3 side in GenAdj.CardinalDirections)
                    {
                        Room room = (candidate.Position + side)
                            .GetRoom(map);
                        if (room != null && !room.PsychologicallyOutdoors
                            && !room.IsDoorway
                            && (chief == null
                                || room.CellCount > chief.CellCount))
                            chief = room;
                    }

                Rand.PushState(Gen.HashCombineInt(
                    GenText.StableStringHash(record.regionalId), 9127));
                int cut = 0;
                var byKind = new Dictionary<string, int>();
                try
                {
                    // Roughly one aperture per six eligible wall cells.
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (i % 6 != 3) continue;
                        Thing wallThing = candidates[i];
                        bool onChief = chief != null && GenAdj
                            .CardinalDirections.Any(side =>
                                (wallThing.Position + side)
                                    .GetRoom(map) == chief);
                        // CONSTRUCTION HISTORY acts per wall: this
                        // opening was cut in whichever era raised the
                        // wall it sits in.
                        int wallTier = Rand.Chance(oldFabric)
                            ? record.constructionEra : tier;
                        string kind = ChooseKind(wallTier, defensive,
                            militarized, onChief, rulerGuard, pluralDissent,
                            cold, hot, wealth);
                        ThingDef apertureDef = DefDatabase<ThingDef>
                            .GetNamedSilentFail(kind);
                        if (apertureDef == null) continue;
                        IntVec3 cell = wallThing.Position;
                        ThingDef stuff = wallThing.Stuff
                            ?? GenStuff.DefaultStuffFor(apertureDef);
                        wallThing.Destroy(DestroyMode.Vanish);
                        Thing aperture = ThingMaker.MakeThing(apertureDef,
                            apertureDef.MadeFromStuff ? stuff : null);
                        GenSpawn.Spawn(aperture, cell, map);
                        aperture.SetFaction(record.faction);
                        record.seededAssets?.Add(kind + "|" + cell.x + "|"
                            + cell.z + "|" + (stuff?.defName ?? ""));
                        cut++;
                        int count;
                        byKind.TryGetValue(kind, out count);
                        byKind[kind] = count + 1;
                    }
                }
                finally { Rand.PopState(); }
                if (cut > 0)
                    Log.Message("[CA][Apertures] " + (record.name
                        ?? "settlement") + " (tier " + tier
                        + ", construction era " + record.constructionEra
                        + ", wealth " + record.wealth + ", " + form
                        + ", " + climate.ToString("0") + "C"
                        + (militarized ? ", militarized" : "") + "): "
                        + cut + " aperture(s) - "
                        + string.Join(", ", byKind.Select(pair =>
                            pair.Value + " " + pair.Key).ToArray()));
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Apertures] cut failed for "
                    + (record?.name ?? "?") + ": " + e.Message);
            }
        }

        // Defensive and militarized settlements favor firing slits. Local
        // order and dissent affect the main hall. Wealth and climate affect
        // ordinary windows. Tier is the wall's own construction era.
        private static string ChooseKind(int tier, bool defensive,
            bool militarized, bool onChief, bool rulerGuard,
            bool pluralDissent, bool cold, bool hot, int wealth)
        {
            if (defensive) return "CA_ApertureSlit";
            if (militarized && Rand.Chance(0.5f))
                return "CA_ApertureSlit";
            if (onChief)
            {
                if (rulerGuard && Rand.Chance(0.7f))
                    return "CA_ApertureSlit";
                float glaze = tier >= 2 ? 1f
                    : tier == 1 ? (pluralDissent ? 0.8f : 0.6f)
                    : 0f;
                glaze += wealth * 0.1f;
                if (Rand.Chance(glaze)) return "CA_WindowGlazed";
            }
            if (tier >= 2)
            {
                // glass follows money: at wealth 3 nearly every wall,
                // at wealth 0 only where the cold forces it
                float glaze = 0.35f + wealth * 0.2f + (cold ? 0.2f : 0f)
                    - (hot ? 0.15f : 0f);
                return Rand.Chance(glaze) ? "CA_WindowGlazed"
                    : "CA_ApertureShuttered";
            }
            if (tier == 1)
            {
                if (wealth >= 2 && Rand.Chance(0.25f + wealth * 0.1f))
                    return "CA_WindowGlazed"; // bought, not made here
                return hot && Rand.Chance(0.5f) ? "CA_ApertureOpen"
                    : "CA_ApertureShuttered";
            }
            if (cold) return "CA_ApertureShuttered";
            return Rand.Chance(hot ? 0.15f : 0.35f + wealth * 0.1f)
                ? "CA_ApertureShuttered" : "CA_ApertureOpen";
        }

        // The one seam CA awareness consults: how transparent the
        // opening at this cell is to sight and sound. A wall is 0, an
        // open hole is 1, everything else sits between per its state.
        internal static float TransparencyAt(Map map, IntVec3 cell)
        {
            Building edifice = cell.InBounds(map)
                ? cell.GetEdifice(map) : null;
            var aperture = edifice as Building_CAAperture;
            if (aperture != null) return aperture.AwarenessTransparency;
            return edifice != null && edifice.def.blockLight ? 0f : 1f;
        }
    }
}
