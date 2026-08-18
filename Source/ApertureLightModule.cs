using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // DAYLIGHT THROUGH THE WALL - the functional half of window light.
    //
    // The engine only daylights unroofed cells; a glazed window in a
    // roofed room changes nothing by def alone. The TECHNIQUE here is
    // adapted from the audited MIT prior art - jptrrs's OpenTheWindows
    // (the originator of wall-window daylighting: a map-held set of
    // "window-influenced" cells and a GlowGrid.GroundGlowAt postfix
    // returning max(result, skyGlow * transmission)) and archdukejim's
    // rimworld-skylights (the clean 1.6 statement of the same patch
    // point) - reimplemented natively, no code copied: CA's footprint
    // is a bounded inward fan from the aperture cell rather than OTW's
    // facing-voted scanline, per-instance state comes from the def swap
    // rather than mutated def fields, and the lighting-overlay mesh is
    // deliberately NOT touched (OTW nulls roof-grid entries during
    // Regenerate, archdukejim transpiles the section layer; both are
    // visual-only). CONSEQUENCE: light level near a daylit aperture is
    // real for gameplay - work speed, surgery, mood, plant growth read
    // GroundGlowAt - while the overlay tint still draws the room dark.
    // That known visual gap is stated rather than hidden.
    public class CAApertureLightMapComponent : MapComponent
    {
        // cell index -> best sky-light transmission reaching it
        private Dictionary<int, float> lit = new Dictionary<int, float>();
        private bool dirty = true;

        public CAApertureLightMapComponent(Map map) : base(map) { }

        public static CAApertureLightMapComponent For(Map map)
        {
            return map?.GetComponent<CAApertureLightMapComponent>();
        }

        public void Dirty()
        {
            dirty = true;
        }

        // Light transmission BY KIND, for daylight specifically: glass
        // passes light better than it passes sound, a slit throws a
        // blade of light, a closed shutter none at all.
        private static float LightTransmission(Building_CAAperture a)
        {
            switch (a.def.defName)
            {
                case "CA_ApertureOpen": return 1f;
                case "CA_ApertureShuttered": return 0.9f;
                case "CA_WindowGlazed": return 0.85f;
                case "CA_ApertureSlit": return 0.25f;
                default: return 0f; // closed shutter
            }
        }

        // The daylit footprint: from each aperture, fan INWARD into the
        // roofed room behind it - straight in three cells, one lateral
        // at the first depth - bounded and room-guarded, so light falls
        // just inside the opening the way it does through a real one.
        private void Rebuild()
        {
            dirty = false;
            lit.Clear();
            foreach (Building building in
                map.listerBuildings.allBuildingsNonColonist)
                Contribute(building as Building_CAAperture);
            foreach (Building building in
                map.listerBuildings.allBuildingsColonist)
                Contribute(building as Building_CAAperture);
        }

        private void Contribute(Building_CAAperture aperture)
        {
            if (aperture == null || !aperture.Spawned) return;
            float transmission = LightTransmission(aperture);
            if (transmission <= 0f) return;
            IntVec3 origin = aperture.Position;
            CellIndices indices = map.cellIndices;
            for (int d = 0; d < 4; d++)
            {
                IntVec3 dir = GenAdj.CardinalDirections[d];
                IntVec3 first = origin + dir;
                if (!first.InBounds(map)) continue;
                Room room = first.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors
                    || room.IsDoorway) continue;
                IntVec3 perp = new IntVec3(dir.z, 0, dir.x);
                for (int depth = 1; depth <= 3; depth++)
                {
                    IntVec3 cell = origin + dir * depth;
                    if (!cell.InBounds(map)
                        || cell.GetRoom(map) != room) break;
                    // light thins with depth into the room
                    Note(indices, cell,
                        transmission * (1f - 0.18f * (depth - 1)));
                    if (depth == 1)
                    {
                        foreach (IntVec3 side in new[]
                            { cell + perp, cell - perp })
                            if (side.InBounds(map)
                                && side.GetRoom(map) == room)
                                Note(indices, side,
                                    transmission * 0.7f);
                    }
                    if (cell.GetEdifice(map) != null) break;
                }
            }
        }

        private void Note(CellIndices indices, IntVec3 cell, float value)
        {
            int index = indices.CellToIndex(cell);
            float existing;
            if (!lit.TryGetValue(index, out existing)
                || value > existing)
                lit[index] = value;
        }

        // O(1) per query - GroundGlowAt is one of the hottest paths in
        // the game, so the postfix must cost a dictionary probe and
        // nothing else on the miss path.
        public bool TrySkylight(IntVec3 cell, out float transmission)
        {
            if (dirty) Rebuild();
            return lit.TryGetValue(map.cellIndices.CellToIndex(cell),
                out transmission);
        }
    }

    // The patch point itself (see module header for provenance): where
    // the engine asks how bright the ground is, a cell inside an
    // aperture's daylit footprint answers with the sky's own glow
    // scaled by what the opening passes - never less than the room's
    // artificial light, never more than the sky.
    [HarmonyPatch(typeof(GlowGrid), nameof(GlowGrid.GroundGlowAt))]
    internal static class GlowGrid_GroundGlowAt_CAApertures
    {
        private static void Postfix(Map ___map, IntVec3 c,
            bool ignoreSky, ref float __result)
        {
            if (ignoreSky || __result >= 1f || ___map == null) return;
            CAApertureLightMapComponent component =
                CAApertureLightMapComponent.For(___map);
            float transmission;
            if (component == null
                || !component.TrySkylight(c, out transmission)) return;
            float sky = ___map.skyManager.CurSkyGlow * transmission;
            if (sky > __result) __result = sky;
        }
    }
}
