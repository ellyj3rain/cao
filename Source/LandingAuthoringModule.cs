using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // A gravship landing on unoccupied ground is the colony choosing its next
    // home region. The visit-scoped derived-region fallback exists for maps
    // the world visits; a landing the colony lives in enters the same
    // regional authoring model as a new game: the region is selected by the
    // established extent and orientation rules over the world partition's
    // geographic evidence, composed by the same derivation machinery, and
    // registered only at materialization - an interrupted flight never
    // leaves a ghost region, and skipping authoring preserves the derived
    // behavior exactly. Landing on ground a realized region already governs
    // keeps the existing anchor semantics and is untouched here.
    internal static class CALandingAuthoring
    {
        private sealed class PreparedLanding
        {
            internal CARegionalPlan candidate;
            internal CAExpandedLandmassProfile profile;
            internal int destinationTileId = -1;
        }

        private static PreparedLanding prepared;

        // The candidate open in the landing dialog or waiting to be consumed
        // by map generation. The world map's footprint layer draws it while
        // it exists; it is never persisted, so a save, crash, or abandoned
        // flight between confirmation and landing simply lands through the
        // derived path as before.
        internal static CARegionalPlan Candidate => prepared?.candidate;

        // The map-generation prefix consults this before falling back to the
        // derived region. Match is exact: same destination tile, same scale.
        internal static CARegionalPlan ConsumePrepared(PlanetTile mapTile,
            CAExpandedLandmassProfile profile)
        {
            PreparedLanding pending = prepared;
            prepared = null;
            if (pending == null || pending.candidate == null
                || pending.destinationTileId != mapTile.tileId
                || pending.profile.Size != profile.Size)
                return null;
            return pending.candidate;
        }

        internal static bool TryOpen(PlanetTile tile, Action settleAction)
        {
            if (!CALandingAuthoringSupport.CanAuthorAt(tile)) return false;
            if (!CAExpandedLandmassProfileResolver.TryResolveLandingProfile(
                    out CAExpandedLandmassProfile profile))
                return false;
            CARegionalPlan candidate = CARegionalPlanUtility.CreateAuthoredAt(
                profile, tile, 0, 0);
            if (candidate?.memberTileIds == null
                || candidate.memberTileIds.Count == 0)
                return false;
            candidate.creationSummary = "gravship landing region authored at "
                + "site confirmation";
            Verse.Find.WindowStack.Add(new Dialog_CAGravshipLandingRegion(
                tile, profile, candidate, settleAction));
            return true;
        }

        internal static void Confirm(CAExpandedLandmassProfile profile,
            CARegionalPlan candidate, int destinationTileId)
        {
            prepared = new PreparedLanding
            {
                candidate = candidate,
                profile = profile,
                destinationTileId = destinationTileId
            };
        }
    }

    // Site eligibility for landing-authoring entry: fresh, unoccupied,
    // ungoverned surface ground. Existing maps, registered-region ground,
    // non-surface layers, and missing world state all keep the prior flow.
    internal static class CALandingAuthoringSupport
    {
        internal static bool CanAuthorAt(PlanetTile tile)
        {
            if (!tile.Valid || tile.Layer == null
                || tile.Layer != Verse.Find.WorldGrid?.Surface)
                return false;
            if (Verse.Find.WorldObjects?.MapParentAt(tile)?.HasMap == true)
                return false;
            if (CARegionalWorldComponent.Current == null) return false;
            return CARegionalGeography.RegionAt(tile) == null;
        }
    }

    // The map scale a landing region materializes at. Mirrors the overview's
    // resolution order minus the creation-flow pending size: this game's
    // chosen size, then the world's initial size, then the first registered
    // region's scale, then the profile floor. Guessing otherwise previews
    // and materializes at scales the engine never uses.
    internal static class CAExpandedLandmassProfileResolver
    {
        internal static bool TryResolveLandingProfile(
            out CAExpandedLandmassProfile profile)
        {
            int chosen = Verse.Find.GameInitData?.mapSize ?? 0;
            if (chosen > 0 && CAExpandedLandmassProfile.TryFor(chosen,
                    out profile))
                return true;
            IntVec3 initial = Verse.Find.World?.info?.initialMapSize
                ?? IntVec3.Zero;
            if (initial.x > 0 && initial.x == initial.z
                && CAExpandedLandmassProfile.TryFor(initial.x, out profile))
                return true;
            int registeredScale = CARegionalWorldComponent.Current?.Regions
                .FirstOrDefault()?.mapSize ?? 0;
            if (registeredScale > 0 && CAExpandedLandmassProfile.TryFor(
                    registeredScale, out profile))
                return true;
            return CAExpandedLandmassProfile.TryFor(250, out profile);
        }
    }

    // The gravship landing-confirm seam: vanilla confirms the landing site,
    // and this wraps the confirmed action so fresh ground first offers the
    // regional authoring host. Caravan settling and the creation flow reach
    // CheckConfirmSettle with a null engine and are untouched.
    [HarmonyPatch(typeof(SettlementProximityGoodwillUtility),
        nameof(SettlementProximityGoodwillUtility.CheckConfirmSettle))]
    internal static class CAGravshipLandingAuthoringPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlanetTile tile, ref Action settleAction,
            Building_GravEngine gravEngine)
        {
            if (gravEngine == null || settleAction == null) return;
            if (!CALandingAuthoringSupport.CanAuthorAt(tile)) return;
            Action original = settleAction;
            settleAction = delegate
            {
                if (!CALandingAuthoring.TryOpen(tile, original))
                    original();
            };
        }
    }

    // The landing-authoring host: a compact window over the world map. The
    // candidate draws on the globe's footprint layer behind it, so the
    // geography being authored is the geography on the globe - one
    // ontology, no separate preview model.
    internal sealed class Dialog_CAGravshipLandingRegion : Window
    {
        private readonly PlanetTile tile;
        private readonly CAExpandedLandmassProfile profile;
        private readonly Action settleAction;
        private CARegionalPlan candidate;
        private int extent;
        private int rotation;

        public Dialog_CAGravshipLandingRegion(PlanetTile tile,
            CAExpandedLandmassProfile profile, CARegionalPlan candidate,
            Action settleAction)
        {
            this.tile = tile;
            this.profile = profile;
            this.candidate = candidate;
            this.settleAction = settleAction;
            extent = candidate.RequestedRegionTileCount;
            rotation = candidate.footprintRotation;
            doCloseX = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(560f, 500f);

        private void Rebuild(int nextExtent, int nextRotation)
        {
            extent = nextExtent;
            rotation = ((nextRotation % 6) + 6) % 6;
            PlanetTile root = CARegionalPlanUtility.SurfaceTile(
                candidate.bundleRootTileId);
            if (!root.Valid) return;
            CARegionalPlan rebuilt = CARegionalPlanUtility.Create(profile,
                root, true, extent, rotation);
            if (rebuilt?.memberTileIds == null
                || rebuilt.memberTileIds.Count == 0) return;
            if (rebuilt.memberTileIds.Contains(candidate.startTileId))
                rebuilt.startTileId = candidate.startTileId;
            candidate = rebuilt;
            CALandingAuthoring.Confirm(profile, candidate, tile.tileId);
        }

        private string GeographySummary()
        {
            List<int> members = candidate.memberTileIds
                ?? new List<int>();
            var biomes = new List<string>();
            var relief = new List<string>();
            int roads = 0;
            int rivers = 0;
            var features = new List<string>();
            int residents = 0;
            var memberSet = new HashSet<int>(members);
            foreach (int id in members)
            {
                PlanetTile area = CARegionalPlanUtility.SurfaceTile(id);
                if (!area.Valid || area.Tile == null) continue;
                string biome = area.Tile.PrimaryBiome?.label;
                if (!biome.NullOrEmpty() && !biomes.Contains(biome))
                    biomes.Add(biome);
                string hill = area.Tile.hilliness.GetLabel();
                if (!hill.NullOrEmpty() && !relief.Contains(hill))
                    relief.Add(hill);
                if (area.Tile is SurfaceTile surface)
                {
                    if (surface.Roads != null) roads += surface.Roads.Count;
                    if (surface.Rivers != null) rivers += surface.Rivers.Count;
                }
                string feature = area.Tile.feature?.name;
                if (!feature.NullOrEmpty() && !features.Contains(feature))
                    features.Add(feature);
                if (area.Tile.Mutators != null)
                    foreach (TileMutatorDef mutator in area.Tile.Mutators)
                        if (mutator != null && !mutator.label.NullOrEmpty()
                            && !features.Contains(mutator.label))
                            features.Add(mutator.label);
            }
            List<Settlement> settlements = Verse.Find.WorldObjects
                ?.Settlements ?? new List<Settlement>();
            residents = settlements.Count(settlement =>
                settlement != null && memberSet.Contains(settlement.Tile.tileId));

            string text = "Land: " + members.Count + " connected areas"
                + (biomes.Count > 0 ? " - " + string.Join(", ", biomes) : "")
                + ".";
            if (relief.Count > 0)
                text += "\nRelief: " + string.Join(", ", relief) + ".";
            if (roads + rivers > 0)
                text += "\nRoutes: " + (roads > 0 ? roads + " road link"
                    + (roads == 1 ? "" : "s") : "")
                    + (roads > 0 && rivers > 0 ? ", " : "")
                    + (rivers > 0 ? rivers + " river link"
                    + (rivers == 1 ? "" : "s") : "") + ".";
            if (features.Count > 0)
                text += "\nFeatures here: " + string.Join(", ", features) + ".";
            if (residents > 0)
                text += "\n" + residents + " existing settlement"
                    + (residents == 1 ? "" : "s")
                    + " stand inside these areas; they anchor the region.";
            return text;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f),
                "Landing region: "
                + CARegionalPlanUtility.TileWords(tile.tileId));
            Text.Font = GameFont.Small;
            var listing = new Listing_Standard();
            listing.Begin(new Rect(0f, 42f, inRect.width,
                inRect.height - 42f - 46f));

            listing.Label("The gravship lands on unclaimed ground. The "
                + "region it joins is authored now: the footprint drawn on "
                + "the globe is the selection, and the world composes the "
                + "region the same way as any authored starting region.");
            listing.Gap();

            listing.Label("Extent: seeks " + extent + " areas, realized "
                + candidate.RegionTileCount + " (" + candidate.regionalId
                + ")");
            float row = listing.CurHeight;
            float x = 0f;
            foreach (int choice in CARegionalGeographyComposition
                .SupportedExtents)
            {
                if (Widgets.ButtonText(new Rect(x, row, 88f, 28f),
                        choice == extent ? "[" + choice + " areas]"
                        : choice + " areas"))
                    Rebuild(choice, rotation);
                x += 94f;
            }
            listing.Gap(36f);

            if (listing.ButtonText("Turn region shape (orientation "
                    + (rotation + 1) + " of 6)"))
                Rebuild(extent, rotation + 1);
            if (listing.ButtonText("Arrival area: " + CARegionalPlanUtility
                    .TileWords(candidate.startTileId)))
            {
                List<int> members = candidate.memberTileIds;
                int index = members.IndexOf(candidate.startTileId);
                if (members.Count > 0)
                    candidate.startTileId = members[(index + 1)
                        % members.Count];
                CALandingAuthoring.Confirm(profile, candidate, tile.tileId);
            }
            listing.Gap();
            listing.Label(GeographySummary());
            listing.End();

            float bottom = inRect.height - 40f;
            if (Widgets.ButtonText(new Rect(0f, bottom, 224f, 36f),
                    "Author this region and land"))
            {
                CALandingAuthoring.Confirm(profile, candidate, tile.tileId);
                Close();
                settleAction();
            }
            if (Widgets.ButtonText(new Rect(232f, bottom, 184f, 36f),
                    "Land without authoring"))
            {
                Close();
                settleAction();
            }
            if (Widgets.ButtonText(new Rect(inRect.width - 130f, bottom,
                    130f, 36f), "Cancel"))
            {
                Close();
            }
        }
    }
}