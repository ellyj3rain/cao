using System;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // HOW CLOSE THE PREVIEW ACTUALLY IS, MEASURED.
    //
    // The preview and generation run the same kernel, which guarantees the
    // same MODEL. It does not guarantee the same ANSWER per cell: the
    // preview samples that model on a coarser grid, and a warped Voronoi
    // boundary, a coast band and a beach band are all sub-cell features at
    // preview scale. Claiming pixel identity without measuring it would be
    // a declaration the code cannot keep.
    //
    // So this measures it. It builds the kernel twice from one plan - once
    // at generation resolution, once at preview resolution - maps each
    // preview cell to the full-resolution cell at the centre of the block
    // it covers, and reports agreement per axis. The result is what the
    // screen is allowed to say.
    //
    // COST. The full-resolution build is the real thing: on the 1433x1323
    // fixture that is 1.9M cells and seconds of work. This is a
    // measurement run on demand, never something a screen does while it is
    // drawing.
    internal sealed class CARegionalProjectionFidelity
    {
        internal int SampledCells;
        internal int OwnershipAgree;
        internal int BiomeAgree;
        internal int LandWaterAgree;
        internal int CoastBandAgree;

        // Boundary displacement: for each preview land/water boundary
        // cell, how far - in FULL-resolution cells - the nearest
        // full-resolution boundary cell is. Reported with its worst case,
        // because a coastline's worst case is what misleads a reader
        // rather than its average.
        internal int BoundarySamples;
        internal float BoundaryMeanCells;
        internal int BoundaryMaxCells;
        internal int BoundaryWithinOneBlock;

        internal float PreviewResolution;
        internal IntVec3 PreviewSize;
        internal IntVec3 FullSize;
        internal long BuildMilliseconds;

        private static float Share(int part, int whole)
        {
            return whole <= 0 ? 0f : 100f * part / whole;
        }

        // The coast contract's own bands, so the comparison is against the
        // classification terrain actually keys off rather than a number
        // invented for the report.
        private static int CoastBand(float coast)
        {
            if (coast < 0.4f) return 0;   // deep water
            if (coast < 0.5f) return 1;   // shallow water
            if (coast < 0.6f) return 2;   // native beach band
            return 3;                     // inland
        }

        internal static CARegionalProjectionFidelity Measure(
            CARegionalPlan plan, int targetLongestSide)
        {
            if (plan == null || plan.memberTileIds == null
                || plan.memberTileIds.Count == 0) return null;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            PlanetTile root = CARegionalPlanUtility.SurfaceTile(
                plan.bundleRootTileId);
            BiomeDef fallback = root.Valid ? root.Tile?.PrimaryBiome : null;

            CARegionalProjectionKernel full =
                CARegionalProjectionKernel.Build(
                    CARegionalProjectionRequest.ForGeneration(plan,
                        plan.BackingMapSize, fallback));
            CARegionalProjectionKernel preview =
                CARegionalProjectionKernel.Build(
                    CARegionalProjectionRequest.ForPreview(plan,
                        targetLongestSide));
            if (full.MemberByCell == null || full.MemberByCell.Length == 0
                || preview.MemberByCell == null
                || preview.MemberByCell.Length == 0) return null;

            var result = new CARegionalProjectionFidelity
            {
                PreviewResolution = preview.Resolution,
                PreviewSize = preview.Size,
                FullSize = full.Size
            };

            float scaleX = full.Size.x / (float)preview.Size.x;
            float scaleZ = full.Size.z / (float)preview.Size.z;
            int block = Mathf.CeilToInt(Math.Max(scaleX, scaleZ));
            bool[] fullBoundary = BoundaryCells(full);

            for (int pz = 0; pz < preview.Size.z; pz++)
            {
                for (int px = 0; px < preview.Size.x; px++)
                {
                    int pi = preview.Indices.CellToIndex(
                        new IntVec3(px, 0, pz));
                    // The full-resolution cell at the centre of the block
                    // this preview cell stands for.
                    int fx = Mathf.Clamp(Mathf.FloorToInt(
                        (px + 0.5f) * scaleX), 0, full.Size.x - 1);
                    int fz = Mathf.Clamp(Mathf.FloorToInt(
                        (pz + 0.5f) * scaleZ), 0, full.Size.z - 1);
                    int fi = full.Indices.CellToIndex(
                        new IntVec3(fx, 0, fz));

                    result.SampledCells++;
                    if (preview.MemberTileAtIndex(pi).tileId
                        == full.MemberTileAtIndex(fi).tileId)
                        result.OwnershipAgree++;
                    BiomeDef previewBiome = preview.BiomeAtIndex(pi);
                    BiomeDef fullBiome = full.BiomeAtIndex(fi);
                    if (previewBiome == fullBiome) result.BiomeAgree++;
                    if ((previewBiome?.isWaterBiome == true)
                        == (fullBiome?.isWaterBiome == true))
                        result.LandWaterAgree++;
                    if (CoastBand(preview.CoastValueByCell[pi])
                        == CoastBand(full.CoastValueByCell[fi]))
                        result.CoastBandAgree++;

                    if (!IsBoundary(preview, px, pz, pi)) continue;
                    result.BoundarySamples++;
                    int distance = NearestBoundaryDistance(full,
                        fullBoundary, fx, fz, block * 4);
                    result.BoundaryMeanCells += distance;
                    if (distance > result.BoundaryMaxCells)
                        result.BoundaryMaxCells = distance;
                    if (distance <= block) result.BoundaryWithinOneBlock++;
                }
            }
            if (result.BoundarySamples > 0)
                result.BoundaryMeanCells /= result.BoundarySamples;
            result.BuildMilliseconds = timer.ElapsedMilliseconds;
            return result;
        }

        private static bool IsBoundary(CARegionalProjectionKernel kernel,
            int x, int z, int index)
        {
            bool water = kernel.BiomeAtIndex(index)?.isWaterBiome == true;
            int width = kernel.Size.x;
            if (x + 1 < kernel.Size.x
                && (kernel.BiomeAtIndex(index + 1)?.isWaterBiome == true)
                    != water) return true;
            return z + 1 < kernel.Size.z
                && (kernel.BiomeAtIndex(index + width)?.isWaterBiome == true)
                    != water;
        }

        private static bool[] BoundaryCells(
            CARegionalProjectionKernel kernel)
        {
            var flags = new bool[kernel.MemberByCell.Length];
            int width = kernel.Size.x;
            for (int z = 0; z < kernel.Size.z; z++)
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    flags[index] = IsBoundary(kernel, x, z, index);
                }
            return flags;
        }

        // Bounded ring search. A preview boundary cell that finds no
        // full-resolution boundary inside the search radius is counted AT
        // the radius rather than dropped, so the report cannot flatter
        // itself by discarding its worst cases.
        private static int NearestBoundaryDistance(
            CARegionalProjectionKernel full, bool[] boundary, int x, int z,
            int maxRadius)
        {
            int width = full.Size.x;
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int cz = z + dz;
                    if (cz < 0 || cz >= full.Size.z) continue;
                    int span = radius - Math.Abs(dz);
                    for (int side = 0; side < 2; side++)
                    {
                        int cx = x + (side == 0 ? -span : span);
                        if (cx < 0 || cx >= width) continue;
                        if (boundary[cz * width + cx]) return radius;
                    }
                }
            }
            return maxRadius;
        }

        internal string Receipt()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("[CA][Regional][Fidelity] preview "
                + PreviewSize.x + "x" + PreviewSize.z + " against full "
                + FullSize.x + "x" + FullSize.z + " (resolution "
                + PreviewResolution.ToString("F3")
                + "); both built from the same kernel; "
                + BuildMilliseconds + " ms");
            text.AppendLine("  sampled " + SampledCells.ToString("N0")
                + " preview cells against the full-resolution cell at the "
                + "centre of the block each covers");
            text.AppendLine("  constituent ownership agrees "
                + Share(OwnershipAgree, SampledCells).ToString("F2") + "%");
            text.AppendLine("  biome agrees "
                + Share(BiomeAgree, SampledCells).ToString("F2") + "%");
            text.AppendLine("  land/water agrees "
                + Share(LandWaterAgree, SampledCells).ToString("F2") + "%");
            text.AppendLine("  coast band (deep/shallow/beach/inland) "
                + "agrees "
                + Share(CoastBandAgree, SampledCells).ToString("F2") + "%");
            text.AppendLine("  coastline displacement over "
                + BoundarySamples.ToString("N0")
                + " preview boundary cells: mean "
                + BoundaryMeanCells.ToString("F1")
                + " full-resolution cells, worst " + BoundaryMaxCells
                + ", within one preview block "
                + Share(BoundaryWithinOneBlock, BoundarySamples)
                    .ToString("F2") + "%");
            return text.ToString();
        }

        // One line the screen may say, written FROM the measurement rather
        // than asserted ahead of it.
        internal string ScreenPhrase()
        {
            string prefix = "measured: land/water "
                + Share(LandWaterAgree, SampledCells).ToString("F1")
                + "%, constituent ownership "
                + Share(OwnershipAgree, SampledCells).ToString("F1") + "%";
            return BoundarySamples == 0
                ? prefix + ", no coastline in the preview sample; "
                    + "displacement unmeasured"
                : prefix + ", sampled coastline within " + BoundaryMaxCells
                    + " generation cells at worst";
        }
    }

    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness",
            "Regional: measure preview fidelity",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Entry)]
        private static void MeasureRegionalPreviewFidelity()
        {
            CARegionalPlan plan = CARegionalSetupSession.PendingForCurrentWorld
                ?? CARegionalSetupSession.ActivePreviewPlan;
            if (plan == null)
            {
                Log.Warning("[CA][Regional][Fidelity] no candidate plan is "
                    + "active. Open the starting-site page and select a "
                    + "regional footprint first.");
                return;
            }
            CARegionalProjectionFidelity result =
                CARegionalProjectionFidelity.Measure(plan,
                    CARegionalProjectionPreview.TargetLongestSide);
            if (result == null)
            {
                Log.Warning("[CA][Regional][Fidelity] the active candidate "
                    + "produced no projectable geography.");
                return;
            }
            CARegionalProjectionPreview.RecordFidelity(result);
            Log.Message(result.Receipt());
        }
    }
}
